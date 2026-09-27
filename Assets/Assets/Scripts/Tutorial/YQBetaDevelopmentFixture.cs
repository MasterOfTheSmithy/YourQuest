using System;

// note: This fixture defines one repeatable development identity without changing ordinary player-created profile behavior.
public static class YQBetaDevelopmentFixture
{
    // note: A stable profile id makes clean reset and save/reload checks addressable without guessing a generated GUID.
    public const string CanonicalProfileId = "beta-dev-canonical";

    // note: The display and world names are inputs to the deterministic fallback seed contract.
    public const string CanonicalDisplayName = "Beta Dev";
    public const string CanonicalWorldName = "YourQuest Beta Fixture";
    public const string CanonicalOriginSeed = "beta-origin-v1";
    public const string CanonicalDirectionKey = "wanderer";
    public const string CanonicalOriginQuestName = "The Repeatable Horizon";

    // note: These answers are fixed evidence for a development fixture and never become player-facing production defaults.
    public static readonly string[] CanonicalAnswers =
    {
        "I choose a repeatable path so every beta check begins from the same evidence.",
        "I keep the seed fixed until the production baseline is trusted."
    };

    // note: This mirrors the production world-seed hash so the expected fixture seed can be printed before a world loads.
    public static string CanonicalWorldSeed => StableHex(
        CanonicalProfileId + "|" + CanonicalWorldName + "|" + CanonicalOriginSeed + "|" +
        CanonicalDirectionKey + "|" + string.Join("|", CanonicalAnswers));

    // note: The helper uses the same versioned algorithm as the production fallback seed builder.
    private static string StableHex(string value)
    {
        unchecked
        {
            int hash = 23;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
                hash = hash * 31 + text[index];
            return (hash & 0x7fffffff).ToString("x8");
        }
    }
}
