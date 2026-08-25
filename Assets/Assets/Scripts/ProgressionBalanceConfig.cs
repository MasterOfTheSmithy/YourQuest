using UnityEngine;

[CreateAssetMenu(fileName = "ProgressionBalanceConfig", menuName = "YourQuest/Progression Balance Config")]
public class ProgressionBalanceConfig : ScriptableObject
{
    [Header("Think Timing")]
    public float thinkEverySeconds = 10f;

    [Header("Evidence Windows")]
    [Tooltip("How many recent ActionEvents from EventAccumulator to consider as the short-term buffer.")]
    public int maxRecentEvents = 180;

    [Header("Thresholds (earned-ness score)")]
    [Tooltip("If the score is below this, do nothing (no LLM call).")]
    public float minScoreToConsider = 12f;

    [Tooltip("Skill-tier reward consideration threshold.")]
    public float scoreForSkillCandidate = 24f;

    [Tooltip("Title candidate threshold.")]
    public float scoreForTitleCandidate = 34f;

    [Tooltip("Quest hook threshold.")]
    public float scoreForQuestCandidate = 38f;

    [Header("Cooldowns (seconds)")]
    public float skillCooldown = 420f;   // 7m
    public float titleCooldown = 900f;   // 15m
    public float questCooldown = 720f;   // 12m

    [Header("Diminishing Returns")]
    [Range(0.05f, 1f)]
    public float repeatPenaltyPerSameVerb = 0.15f;

    [Tooltip("After this many repeats in the short window, penalties get harsh.")]
    public int harshRepeatAfter = 40;

    [Range(0.05f, 1f)]
    public float harshRepeatPenalty = 0.35f;

    [Header("Bonuses")]
    [Tooltip("Bonus multiplier when actions occur under threat / danger context.")]
    public float dangerBonusMultiplier = 1.25f;

    [Tooltip("Legacy context multiplier for semantic regions. Keep near 1 so player behavior, not location names, drives progression.")]
    public float semanticRegionBonus = 1.00f;

    [Tooltip("Bonus when the player’s actions show variety (not only one verb).")]
    public float varietyBonusMultiplier = 1.15f;
}
