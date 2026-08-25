using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQQuestCompletionDirector : MonoBehaviour
{
    public float evaluationIntervalSeconds = 0.5f;

    private float _nextEvaluationTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQQuestCompletionDirector>() != null)
            return;

        GameObject go = new GameObject("00__Runtime_QuestCompletionDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<YQQuestCompletionDirector>();
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _nextEvaluationTime = Time.unscaledTime + 0.25f;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextEvaluationTime)
            return;

        _nextEvaluationTime = Time.unscaledTime + Mathf.Max(0.1f, evaluationIntervalSeconds);
        Evaluate();
    }

    private void Evaluate()
    {
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return;

        PlayerState state = manager.state;
        state.EnsureCollections();
        bool changed = false;

        if (state.quests != null)
        {
            for (int i = 0; i < state.quests.Count; i++)
            {
                QuestRecord quest = state.quests[i];
                if (quest != null && quest.completedUnix <= 0 && IsCompletionStatus(quest.status))
                    changed |= CompleteQuest(state, quest);
            }
        }

        QuestRecord active = state.GetActiveQuest();
        if (active != null && active.completedUnix <= 0 && ShouldCompleteFromProgress(state, active))
            changed |= CompleteQuest(state, active);

        if (changed)
            manager.Save();
    }

    private static bool CompleteQuest(PlayerState state, QuestRecord quest)
    {
        if (state == null || quest == null || string.IsNullOrWhiteSpace(quest.questId))
            return false;

        if (!state.TryCompleteQuest(quest.questId, out string message))
            return false;

        GeneratedRpgContentService.Instance?.SetInventoryMessage(message);
        GameObject player = GameObject.FindWithTag("Player");
        Vector3 position = player != null ? player.transform.position : Vector3.zero;
        YQRuntimeAudioFeedback.PlayQuestComplete(position);
        return true;
    }

    private static bool ShouldCompleteFromProgress(PlayerState state, QuestRecord quest)
    {
        if (quest != null && quest.objectives != null && quest.objectives.Count > 0)
            return ShouldCompleteFromObjectives(state, quest);

        // note: Quest prose is presentation only; objective-less legacy records cannot infer completion mechanics from words.
        return false;
    }

    private static bool ShouldCompleteFromObjectives(PlayerState state, QuestRecord quest)
    {
        if (state == null || quest == null)
            return false;

        quest.EnsureCollections();
        if (quest.objectives.Count == 0)
            return false;

        bool allComplete = true;
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        for (int i = 0; i < quest.objectives.Count; i++)
        {
            QuestObjectiveRecord objective = quest.objectives[i];
            if (objective == null)
                continue;

            if (objective.completed)
                continue;

            if (IsObjectiveComplete(state, objective))
            {
                objective.completed = true;
                objective.completedUnix = now;
            }
            else
            {
                allComplete = false;
            }
        }

        return allComplete;
    }

    private static bool IsObjectiveComplete(PlayerState state, QuestObjectiveRecord objective)
    {
        if (objective == null)
            return false;

        float required = Mathf.Max(1f, objective.requiredCount <= 0f ? 1f : objective.requiredCount);
        if (!string.IsNullOrWhiteSpace(objective.counterKey) && ReadCounter(state, objective.counterKey) >= required)
            return true;
        if (!string.IsNullOrWhiteSpace(objective.counterPrefix) && SumCountersByPrefix(state, objective.counterPrefix) >= required)
            return true;

        string type = (objective.type ?? string.Empty).Trim().ToLowerInvariant();
        string targetId = objective.targetId ?? string.Empty;
        switch (type)
        {
            case "origin_manifested":
                return ReadCounter(state, "origin:equipment_manifested") >= required;
            case "equip_item":
                return SumCountersByPrefix(state, "item:equip") >= required;
            case "talk_to_npc":
                return SumCountersByPrefix(state, "dialogue:" + targetId, "dialogue:") >= required;
            case "cast_spell":
                return SumCountersByPrefix(state, "cast:projectile", "cast:pulse") >= required;
            case "defeat_enemy":
            case "kill_enemy":
                return !string.IsNullOrWhiteSpace(targetId)
                    ? SumCountersByPrefix(state, "kill:" + targetId) >= required
                    : SumCountersByPrefix(state, "kill:") >= required;
            case "loot_item":
            case "loot":
                return SumCountersByPrefix(state, "loot:", "pickup:item") >= required;
            case "pickup_item":
                return !string.IsNullOrWhiteSpace(targetId)
                    ? SumCountersByPrefix(state, "pickup:item:" + targetId) >= required
                    : SumCountersByPrefix(state, "pickup:item") >= required;
            case "lockpick":
            case "open_lock":
                return SumCountersByPrefix(state, "lockpick:success") >= required;
            case "mimic_reveal":
                return SumCountersByPrefix(state, "mimic:revealed") >= required;
            case "shrine":
            case "use_shrine":
                return SumCountersByPrefix(state, "interact:shrine", "shrine:") >= required;
            case "enter_region":
                return !string.IsNullOrWhiteSpace(targetId) && string.Equals(state.currentRegionId, targetId, StringComparison.OrdinalIgnoreCase);
            case "wait_seconds":
                return ReadCounter(state, "idle:still_seconds") >= required;
            default:
                return false;
        }
    }

    private static float ReadCounter(PlayerState state, string key)
    {
        if (state == null || state.behaviorCounters == null || string.IsNullOrWhiteSpace(key))
            return 0f;
        return state.behaviorCounters.TryGetValue(key.Trim(), out float value) ? value : 0f;
    }

    private static float SumCountersByPrefix(PlayerState state, params string[] prefixes)
    {
        if (state == null || state.behaviorCounters == null || prefixes == null)
            return 0f;

        float total = 0f;
        foreach (KeyValuePair<string, float> pair in state.behaviorCounters)
        {
            string key = pair.Key ?? string.Empty;
            for (int i = 0; i < prefixes.Length; i++)
            {
                string prefix = prefixes[i];
                if (!string.IsNullOrWhiteSpace(prefix) && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    total += pair.Value;
                    break;
                }
            }
        }

        return total;
    }

    private static int EstimateRequiredCount(string text)
    {
        if (ContainsAny(text, "five", "5", "many"))
            return 5;
        if (ContainsAny(text, "four", "4"))
            return 4;
        if (ContainsAny(text, "three", "3", "several", "few"))
            return 3;
        if (ContainsAny(text, "two", "2", "couple"))
            return 2;
        return 1;
    }

    private static bool IsCompletionStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return false;

        return status.Equals("complete", StringComparison.OrdinalIgnoreCase) ||
               status.Equals("completed", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildQuestText(QuestRecord quest)
    {
        if (quest == null)
            return string.Empty;

        string tags = quest.tags != null ? string.Join(" ", quest.tags) : string.Empty;
        return ((quest.name ?? string.Empty) + " " + (quest.description ?? string.Empty) + " " + tags).ToLowerInvariant();
    }

    private static bool HasTag(QuestRecord quest, string expected)
    {
        if (quest == null || quest.tags == null || string.IsNullOrWhiteSpace(expected))
            return false;

        for (int i = 0; i < quest.tags.Length; i++)
        {
            if (string.Equals(quest.tags[i], expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        if (string.IsNullOrWhiteSpace(text) || needles == null)
            return false;

        for (int i = 0; i < needles.Length; i++)
        {
            string needle = needles[i];
            if (!string.IsNullOrWhiteSpace(needle) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}
