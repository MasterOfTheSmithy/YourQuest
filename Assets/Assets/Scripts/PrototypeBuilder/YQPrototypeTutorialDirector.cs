// Assets/Assets/Scripts/PrototypeBuilder/YQPrototypeTutorialDirector.cs
using System;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class YQPrototypeTutorialDirector : MonoBehaviour
{
    public SituationSnapshotBuilder snapshotBuilder;
    public WorldDeltaApplier worldDeltaApplier;
    public ProgressionDecisionApplier progressionDecisionApplier;

    [TextArea(2, 6)] public string lastWorldLore;
    [TextArea(2, 6)] public string lastRegionEvent;
    [TextArea(2, 6)] public string lastPlayerEvent;
    [TextArea(2, 6)] public string lastClassResult;
    [TextArea(2, 6)] public string lastSkillResult;
    [TextArea(2, 6)] public string lastTitleResult;
    [TextArea(2, 6)] public string lastQuestResult;
    [TextArea(2, 6)] public string lastError;

    public void GenerateWorldLore()
    {
        string prompt = BuildSimplePrompt("world lore");
        Send(prompt, text => lastWorldLore = text, "PrototypeWorldLore");
    }

    public void GenerateRegionEvent()
    {
        string prompt = BuildSimplePrompt("region event");
        Send(prompt, text => lastRegionEvent = text, "PrototypeRegionEvent");
    }

    public void GeneratePlayerEvent()
    {
        string prompt = BuildSimplePrompt("player event");
        Send(prompt, text => lastPlayerEvent = text, "PrototypePlayerEvent");
    }

    public void GenerateClass()
    {
        string prompt = BuildSimplePrompt("class concept");
        Send(prompt, text => lastClassResult = text, "PrototypeClass");
    }

    public void GenerateSkill()
    {
        string prompt = BuildSimplePrompt("skill concept");
        Send(prompt, text => lastSkillResult = text, "PrototypeSkill");
    }

    public void GenerateTitle()
    {
        string prompt = BuildSimplePrompt("title concept");
        Send(prompt, text => lastTitleResult = text, "PrototypeTitle");
    }

    public void GenerateQuest()
    {
        string prompt = BuildSimplePrompt("quest concept");
        Send(prompt, text => lastQuestResult = text, "PrototypeQuest");
    }

    public void TriggerWorldThinkNow()
    {
        string situation = snapshotBuilder != null ? snapshotBuilder.BuildSnapshot() : "<no snapshot>";
        string behaviorLedger = BuildBehaviorLedgerBlock();

        string prompt = PromptContextBuilder.BuildContext(
            "Return a single compact JSON world delta that changes one faction or one location based on the current tutorial situation.",
            PromptContextBuilder.WrapJsonSchema("{ \"rationale\": \"...\", \"confidence\": 0.5, \"flags\": [], \"factions\": [], \"locations\": [] }"),
            situation,
            behaviorLedger
        );

        Send(prompt, raw =>
        {
            if (worldDeltaApplier == null)
            {
                lastError = "WorldDeltaApplier missing.";
                return;
            }

            if (worldDeltaApplier.TryApply(raw, out string err))
                lastWorldLore = raw;
            else
                lastError = err;
        }, "PrototypeWorldThink");
    }

    public void TriggerProgressionThinkNow()
    {
        string prompt = BuildSimplePrompt("progression reward");
        Send(prompt, raw =>
        {
            if (progressionDecisionApplier == null)
            {
                lastError = "ProgressionDecisionApplier missing.";
                return;
            }

            if (TryBuildProgressionWrapper(raw, out string wrapped))
            {
                if (progressionDecisionApplier.TryApply(wrapped, out string applied, out string reason))
                    lastSkillResult = applied + ": " + reason;
                else
                    lastError = reason;
            }
            else
            {
                lastError = "Could not wrap progression response.";
            }
        }, "PrototypeProgressionThink");
    }

    private string BuildSimplePrompt(string category)
    {
        string snapshot = snapshotBuilder != null ? snapshotBuilder.BuildSnapshot() : "<no snapshot>";
        string player = PlayerStateManager.Instance != null ? PlayerMemoryRenderer.Render(PlayerStateManager.Instance.state) : "<no player>";
        string world = WorldStateManager.Instance != null ? WorldMemoryRenderer.Render(WorldStateManager.Instance.state) : "<no world>";

        return $@"You are generating tutorial prototype content for category: {category}.
Return a compact plain-English answer in 2 to 5 lines.
Ground it in this current game state.

SITUATION
{snapshot}

PLAYER
{player}

WORLD
{world}";
    }

    private void Send(string prompt, Action<string> onResponse, string tag)
    {
        if (LLMClient.Instance == null)
        {
            lastError = "LLMClient missing.";
            return;
        }

        lastError = string.Empty;
        LLMClient.Instance.Enqueue(prompt, raw =>
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                lastError = tag + " returned empty response.";
                return;
            }

            onResponse?.Invoke(raw.Trim());
        }, tag);
    }

    private string BuildBehaviorLedgerBlock()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (state == null)
            return "<none>";

        var sb = new StringBuilder(512);

        if (state.behaviorLedger != null && state.behaviorLedger.Count > 0)
        {
            int start = Mathf.Max(0, state.behaviorLedger.Count - 12);
            for (int i = start; i < state.behaviorLedger.Count; i++)
            {
                string line = state.behaviorLedger[i];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                sb.Append("- ");
                sb.AppendLine(line.Trim());
            }
        }

        if (state.behaviorCounters != null && state.behaviorCounters.Count > 0)
        {
            sb.AppendLine("COUNTERS");
            foreach (var kv in state.behaviorCounters)
            {
                sb.Append("- ");
                sb.Append(kv.Key);
                sb.Append(": ");
                sb.AppendLine(kv.Value.ToString("0.##"));
            }
        }

        if (sb.Length == 0)
            sb.Append("<none>");

        return sb.ToString();
    }

    private bool TryBuildProgressionWrapper(string raw, out string wrapped)
    {
        wrapped = null;

        try
        {
            JObject payload = new JObject
            {
                ["skillSeedName"] = "Prototype Insight",
                ["skillType"] = "utility",
                ["hook"] = raw.Trim()
            };

            JObject root = new JObject
            {
                ["decision"] = "skill",
                ["confidence"] = 0.65f,
                ["reason"] = "Prototype manual trigger.",
                ["payload"] = payload
            };

            wrapped = root.ToString();
            return true;
        }
        catch (Exception ex)
        {
            lastError = ex.Message;
            return false;
        }
    }
}
