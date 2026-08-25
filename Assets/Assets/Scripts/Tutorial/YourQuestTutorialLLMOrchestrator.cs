// Assets/Assets/Scripts/Tutorial/YourQuestTutorialLLMOrchestrator.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class YourQuestTutorialLLMOrchestrator : MonoBehaviour
{
    public SituationSnapshotBuilder snapshotBuilder;
    public ProgressionDecisionApplier progressionDecisionApplier;
    public WorldDeltaApplier worldDeltaApplier;
    public float thinkInterval = 22f;

    private float _nextThink;
    private int _step;
    private readonly Queue<string> _debug = new Queue<string>();

    public string DebugSummary
    {
        get
        {
            if (_debug.Count == 0)
                return "No tutorial generation yet.";

            return string.Join("\n", _debug.ToArray());
        }
    }

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        _nextThink = Time.time + 6f;
    }

    private void Update()
    {
        if (Time.time < _nextThink)
            return;

        _nextThink = Time.time + thinkInterval;
        TryGenerate();
    }

    private void TryGenerate()
    {
        var psm = PlayerStateManager.Instance;
        var wsm = WorldStateManager.Instance;
        if (psm == null || wsm == null)
            return;

        string recentSummary = EventSummarizer.Summarize(
            EventAccumulator.Instance != null
                ? new List<ActionEvent>(EventAccumulator.Instance.GetEvents())
                : new List<ActionEvent>());

        string behaviorLedger = ActionRegistry.Instance != null
            ? ActionRegistry.Instance.BuildBehaviorSummary(12)
            : "No behavior recorded.";

        switch (_step % 5)
        {
            case 0:
                GenerateWorldLore(recentSummary, behaviorLedger);
                break;
            case 1:
                GenerateTitle(recentSummary, behaviorLedger);
                break;
            case 2:
                GenerateClass(recentSummary, behaviorLedger);
                break;
            case 3:
                GenerateQuest(recentSummary, behaviorLedger);
                break;
            default:
                GenerateSkill(recentSummary, behaviorLedger);
                break;
        }

        _step++;
    }

    private void GenerateWorldLore(string recentSummary, string behaviorLedger)
    {
        string task = "Invent one region-scale world note grounded in the current player behavior. Return JSON: {\"canonLine\":string,\"regionId\":string,\"locationId\":string,\"stateText\":string,\"importanceDelta\":number,\"tensionDelta\":number,\"rationale\":string}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"canonLine\":\"...\",\"regionId\":\"...\",\"locationId\":\"...\",\"stateText\":\"...\",\"importanceDelta\":0.1,\"tensionDelta\":0.1,\"rationale\":\"...\"}");
        string prompt = PromptContextBuilder.BuildContext(task, schema, recentSummary, behaviorLedger);
        Request(prompt, FallbackWorldLore(), ApplyWorldLore, "world-lore");
    }

    private void GenerateTitle(string recentSummary, string behaviorLedger)
    {
        string task = "Grant one earned title based strictly on observed behavior. Return JSON: {\"name\":string,\"description\":string}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"description\":\"...\"}");
        string prompt = PromptContextBuilder.BuildContext(task, schema, recentSummary, behaviorLedger);
        Request(prompt, FallbackTitle(), ApplyTitle, "title");
    }

    private void GenerateClass(string recentSummary, string behaviorLedger)
    {
        string task = "Propose one deterministic class identity grounded in observed player behavior. Name the player's repeated pattern, not the region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\"}");
        string prompt = PromptContextBuilder.BuildContext(task, schema, recentSummary, behaviorLedger);
        Request(prompt, FallbackClass(), ApplyClass, "class");
    }

    private void GenerateQuest(string recentSummary, string behaviorLedger)
    {
        string task = "Create one immediate quest hook grounded in the player's recent actions. It must have a concrete objective and stakes. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"tags\":[string]}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"tags\":[\"player_response\",\"deterministic\",\"meaningful\"]}");
        string prompt = PromptContextBuilder.BuildContext(task, schema, recentSummary, behaviorLedger);
        Request(prompt, FallbackQuest(), ApplyQuest, "quest");
    }

    private void GenerateSkill(string recentSummary, string behaviorLedger)
    {
        string task = "Grant one deterministic skill decision only if it has a concrete trigger and effect. Goofy/silly ideas must be marked as oddities and allowed to incubate, not granted immediately. Output JSON matching the progression applier schema: {\"decision\":\"skill\",\"confidence\":0.8,\"reason\":\"...\",\"payload\":{\"skillSeedName\":\"...\",\"skillType\":\"combat|movement|utility|craft|social\",\"stimulus\":\"...\",\"hook\":\"...\"}}";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"decision\":\"skill\",\"confidence\":0.8,\"reason\":\"...\",\"payload\":{\"skillSeedName\":\"...\",\"skillType\":\"combat\",\"stimulus\":\"...\",\"hook\":\"...\"}}");
        string prompt = PromptContextBuilder.BuildContext(task, schema, recentSummary, behaviorLedger);
        Request(prompt, FallbackSkill(), ApplySkill, "skill");
    }

    private void Request(string prompt, string fallbackJson, Action<string> apply, string tag)
    {
        var llm = LLMClient.Instance;
        if (llm == null)
        {
            apply(fallbackJson);
            return;
        }

        // note: Tutorial offers mutate persisted state, so require a normalized JSON object before applying them.
        llm.Submit(new YQLlmRequest
        {
            prompt = prompt,
            debugTag = "Tutorial:" + tag,
            category = LLMGenerationCategory.StructuredState,
            priority = YQLlmRequestPriority.PlayerFacing,
            requireJson = true
        }, result =>
        {
            // note: A failed request uses the established deterministic tutorial fallback.
            string raw = result.success ? result.text : null;
            if (string.IsNullOrWhiteSpace(raw))
                apply(fallbackJson);
            else
                apply(raw);
        });
    }

    private void ApplyWorldLore(string raw)
    {
        try
        {
            JObject j = Parse(raw);
            var wsm = WorldStateManager.Instance;
            if (wsm == null)
                return;

            string canon = (j.Value<string>("canonLine") ?? "The tutorial lands shifted under observation.").Trim();
            string regionId = (j.Value<string>("regionId") ?? wsm.State.currentRegionId).Trim();
            string locationId = (j.Value<string>("locationId") ?? (regionId + "_node")).Trim();
            string stateText = (j.Value<string>("stateText") ?? "active").Trim();
            float importanceDelta = Mathf.Clamp(j.Value<float?>("importanceDelta") ?? 0.1f, -2f, 2f);
            float tensionDelta = Mathf.Clamp(j.Value<float?>("tensionDelta") ?? 0.05f, -0.25f, 0.25f);
            string rationale = (j.Value<string>("rationale") ?? canon).Trim();

            wsm.State.AppendCanon(canon);
            wsm.State.ApplyLocationDelta(locationId, "add", importanceDelta, stateText, rationale);
            wsm.State.tension = Mathf.Clamp01(wsm.State.tension + tensionDelta);
            wsm.State.lastLLMRationale = rationale;
            wsm.State.lastLLMConfidence = 0.75f;
            wsm.Save();
            Log("world: " + canon);
        }
        catch (Exception ex)
        {
            Log("world parse failed: " + ex.Message);
        }
    }

    private void ApplyTitle(string raw)
    {
        try
        {
            JObject j = Parse(raw);
            var psm = PlayerStateManager.Instance;
            if (psm == null)
                return;

            string name = (j.Value<string>("name") ?? "Path-Touched").Trim();
            string stimulus = (j.Value<string>("stimulus") ?? "the player's repeated tutorial choices").Trim();
            name = YQGeneratedContentCuration.CuratePlayerFacingName(psm.state, "title", name, "title", false, stimulus);
            string desc = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                psm.state,
                "title",
                name,
                (j.Value<string>("description") ?? "A title shaped by tutorial actions.").Trim(),
                "title",
                false,
                stimulus);
            string[] tags = YQGeneratedContentCuration.BuildPlayerResponseTags(Array.Empty<string>(), "title", false, name + " " + desc + " " + stimulus);
            if (!YQGeneratedContentCuration.PassesOfferQuality(psm.state, "title", name, desc, tags, 0.84f, true, out string rejectReason))
            {
                Log("title rejected: " + rejectReason);
                return;
            }
            psm.state.AwardTitle(name, desc);
            psm.Save();
            Log("title: " + name);
        }
        catch (Exception ex)
        {
            Log("title parse failed: " + ex.Message);
        }
    }

    private void ApplyClass(string raw)
    {
        try
        {
            JObject j = Parse(raw);
            var psm = PlayerStateManager.Instance;
            if (psm == null)
                return;

            string name = (j.Value<string>("name") ?? "Ruin Walker").Trim();
            string stimulus = (j.Value<string>("stimulus") ?? "the player's repeated tutorial choices").Trim();
            name = YQGeneratedContentCuration.CuratePlayerFacingName(psm.state, "class", name, "class", false, stimulus);
            string desc = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                psm.state,
                "class",
                name,
                (j.Value<string>("description") ?? "A class identity emerging from observed movement and conflict.").Trim(),
                "class",
                false,
                stimulus);
            string[] tags = YQGeneratedContentCuration.BuildPlayerResponseTags(Array.Empty<string>(), "class", false, name + " " + desc + " " + stimulus);
            if (!YQGeneratedContentCuration.PassesOfferQuality(psm.state, "class", name, desc, tags, 0.84f, true, out string rejectReason))
            {
                Log("class rejected: " + rejectReason);
                return;
            }
            psm.state.AwardClass(name, desc);
            psm.Save();
            Log("class: " + name);
        }
        catch (Exception ex)
        {
            Log("class parse failed: " + ex.Message);
        }
    }

    private void ApplyQuest(string raw)
    {
        try
        {
            JObject j = Parse(raw);
            var psm = PlayerStateManager.Instance;
            if (psm == null)
                return;

            string name = (j.Value<string>("name") ?? "Stir the Tutorial Wilds").Trim();
            string stimulus = (j.Value<string>("stimulus") ?? "the player's repeated tutorial choices").Trim();
            name = YQGeneratedContentCuration.CuratePlayerFacingName(psm.state, "quest", name, "quest", false, stimulus);
            string desc = YQGeneratedContentCuration.CuratePlayerFacingDescription(
                psm.state,
                "quest",
                name,
                (j.Value<string>("description") ?? "Clear enemies and trigger world reactions across multiple regions.").Trim(),
                "quest",
                false,
                stimulus);
            string[] rawTags = j["tags"] is JArray arr ? arr.ToObject<string[]>() : new[] { "tutorial" };
            string[] tags = YQGeneratedContentCuration.BuildPlayerResponseTags(rawTags, "quest", false, name + " " + desc + " " + stimulus);
            if (!YQGeneratedContentCuration.PassesOfferQuality(psm.state, "quest", name, desc, tags, 0.84f, true, out string rejectReason))
            {
                Log("quest rejected: " + rejectReason);
                return;
            }
            psm.state.OfferQuest(name, desc, tags);
            psm.Save();
            Log("quest: " + name);
        }
        catch (Exception ex)
        {
            Log("quest parse failed: " + ex.Message);
        }
    }

    private void ApplySkill(string raw)
    {
        if (progressionDecisionApplier == null)
        {
            Log("skill applier missing");
            return;
        }

        if (progressionDecisionApplier.TryApply(raw, out var applied, out var reason))
            Log("skill: " + applied + " | " + reason);
        else
            Log("skill skipped: " + reason);
    }

    private JObject Parse(string raw)
    {
        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            raw = raw.Substring(start, end - start + 1);

        return JObject.Parse(raw);
    }

    private void Log(string line)
    {
        _debug.Enqueue("[" + DateTime.Now.ToString("HH:mm:ss") + "] " + line);
        while (_debug.Count > 8)
            _debug.Dequeue();
    }

    private string FallbackWorldLore()
    {
        string region = PlayerStateManager.Instance != null
            ? PlayerStateManager.Instance.state.currentRegionId
            : "region_tutorial_hub";

        var payload = new
        {
            canonLine = "The tutorial lands are beginning to remember the player by pattern, not by name.",
            regionId = region,
            locationId = region + "_focus",
            stateText = "watchful",
            importanceDelta = 0.15f,
            tensionDelta = 0.04f,
            rationale = "Repeated movement and combat made the region react."
        };

        return JsonConvert.SerializeObject(payload);
    }

    private string FallbackTitle()
    {
        var payload = new
        {
            name = "First Pattern",
            description = "The world began recording your behavior as a coherent thread."
        };

        return JsonConvert.SerializeObject(payload);
    }

    private string FallbackClass()
    {
        var payload = new
        {
            name = "Linebreaker Vanguard",
            stimulus = "the player's repeated close-range pressure and recovery windows",
            description = "A class identity shaped by repeated close-range pressure. It matters by steering future offers toward committed strikes, guarded recovery, and tested courage."
        };

        return JsonConvert.SerializeObject(payload);
    }

    private string FallbackQuest()
    {
        var payload = new
        {
            name = "Prove the Pattern",
            stimulus = "the player's first repeated tutorial combat and exploration pattern",
            description = "Objective: take one concrete risk, survive the result, and return with proof. The stakes are whether this pattern becomes part of the player's identity or stays noise.",
            tags = new[] { "tutorial", "player_response", "deterministic", "meaningful" }
        };

        return JsonConvert.SerializeObject(payload);
    }

    private string FallbackSkill()
    {
        var payload = new
        {
            decision = "skill",
            confidence = 0.82f,
            reason = "The player showed repeated direct combat behavior in a bounded tutorial encounter.",
            payload = new
            {
                skillSeedName = "Rend Step",
                skillType = "combat",
                stimulus = "the player's repeated direct combat behavior in a bounded tutorial encounter",
                hook = "When triggered by repeated direct combat pressure, it turns the next committed movement into a clearer strike, guard, or recovery."
            }
        };

        return JsonConvert.SerializeObject(payload);
    }
}
