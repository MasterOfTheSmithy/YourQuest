// Assets/Assets/Scripts/Tutorial/YQInvestorDirector.cs
using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQInvestorDirector : MonoBehaviour
{
    public Transform player;
    public SituationSnapshotBuilder snapshotBuilder;
    public ProgressionDecisionApplier progressionDecisionApplier;
    public WorldDeltaApplier worldDeltaApplier;
    public GeneratedRpgContentService contentService;

    [Header("Cadence")]
    public float smallUpdateIntervalSeconds = 90f;
    public float majorUpdateIntervalSeconds = 420f;
    public float notableOverrideCooldownSeconds = 30f;

    [Header("Offer Dedupe")]
    [Range(0.5f, 1f)] public float classDuplicateThreshold = 0.94f;
    [Range(0.5f, 1f)] public float titleDuplicateThreshold = 0.95f;
    [Range(0.5f, 1f)] public float questDuplicateThreshold = 0.92f;
    [Range(0f, 1f)] public float minimumOfferConfidence = 0.82f;

    public string CurrentObjective { get; private set; } = "Talk to Archivist Vey in the hub.";
    public string LastDirectorMessage { get; private set; } = string.Empty;

    private int _killCount;
    private bool _talkedToArchivist;
    private bool _talkedToWarden;
    private bool _talkedToCardinalMentor;
    private bool _usedShrine;
    private bool _seededOpeners;
    private float _nextSmallUpdateTime;
    private float _nextMajorUpdateTime;
    private float _nextNotableAllowedTime;

    private readonly HashSet<string> _pendingTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _usedGoddessLines = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        ResolveReferences();
        _nextSmallUpdateTime = Time.time + smallUpdateIntervalSeconds;
        _nextMajorUpdateTime = Time.time + majorUpdateIntervalSeconds;
    }

    private void Update()
    {
        ResolveReferences();
        if (!_seededOpeners)
        {
            _seededOpeners = true;
            SeedBaselineOnly();
        }

        UpdateObjectiveState();

        if (RuntimeModalUiBlocker.IsBlocked || RuntimeModalUiBlocker.IsDialogueOpen)
            return;

        if (Time.time >= _nextSmallUpdateTime)
        {
            _nextSmallUpdateTime = Time.time + smallUpdateIntervalSeconds;
            TryRunSmallUpdate();
        }

        if (Time.time >= _nextMajorUpdateTime)
        {
            _nextMajorUpdateTime = Time.time + majorUpdateIntervalSeconds;
            TryRunMajorUpdate();
        }
    }

    public void NotifyEnemyKilled(YQInvestorEnemy enemy)
    {
        _killCount++;
        contentService?.GrantEnemyLoot(enemy);

        if (Time.time < _nextNotableAllowedTime)
            return;

        _nextNotableAllowedTime = Time.time + notableOverrideCooldownSeconds;
        if (_killCount == 1)
            RequestWorldLore("enemy_first_lore", "Add one concise lore consequence for the first enemy defeat in this region.");
        else if (_killCount == 2)
            RequestProgressionDecision("earned_skill_notable", "Grant one grounded skill or spell only if the observed combat evidence clearly supports it.");
        else if (_killCount == 3)
            RequestTitle("earned_title_notable", "Grant one grounded title only if the observed combat evidence clearly supports it.");
        else if (_killCount == 4)
            RequestItem("earned_item_notable", "Offer one useful item that answers the player's observed combat behavior. Pick a semantic equipment type only; Unity binds the actual approved asset.");
    }

    public void NotifyShrineUsed(YQInvestorShrine shrine)
    {
        if (_usedShrine)
            return;

        _usedShrine = true;
        _nextNotableAllowedTime = Time.time + notableOverrideCooldownSeconds;
        RequestWorldEvent("shrine_event_notable", "Create one world-state consequence for shrine use, concise and grounded.");
    }

    public void NotifyDialogueOpened(string npcId)
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm != null && psm.state != null && !string.IsNullOrWhiteSpace(npcId))
        {
            psm.state.IncCounter("dialogue:" + npcId.Trim(), 1f);
            psm.state.AddLedgerLine("The player spoke with " + npcId.Trim() + ".");
            psm.Save();
        }

        if (npcId == "npc_archivist_01")
        {
            if (_talkedToArchivist)
                return;
            _talkedToArchivist = true;
            RequestQuest("archivist_first_quest", "Create one grounded immediate objective from Archivist Vey.");
        }
        else if (npcId == "npc_warden_01")
        {
            if (_talkedToWarden)
                return;
            _talkedToWarden = true;
            RequestPlayerEvent("warden_first_ack", "Record one concise acknowledgement from Warden Thorne based on observed player progress.");
        }
        else if (npcId == "npc_cinder_01" || npcId == "npc_root_sibyl_01" || npcId == "npc_tide_cartographer_01")
        {
            if (_talkedToCardinalMentor)
                return;
            _talkedToCardinalMentor = true;
            RequestQuest("cardinal_mentor_first_quest", "Create one grounded objective from the road mentor. It must answer the player's observed stimulus, not the road name.");
        }
    }

    private void SeedBaselineOnly()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        WorldStateManager wsm = WorldStateManager.Instance;
        if (psm == null || wsm == null || psm.state == null || wsm.State == null)
            return;

        contentService?.EnsureBaselineGeneratedState(psm.state, wsm.State);
        YQWorldGenerationService.Instance?.EnsureWorldPlan(psm.state, wsm.State, false);
    }

    private void TryRunSmallUpdate()
    {
        if (_pendingTags.Count > 0)
            return;

        PlayerStateManager psm = PlayerStateManager.Instance;
        WorldStateManager wsm = WorldStateManager.Instance;
        if (psm == null || wsm == null || psm.state == null || wsm.State == null)
            return;

        if (ActiveQuestCount(psm.state) == 0)
        {
            RequestQuest("small_missing_quest", "Create one grounded active quest that fits the current location and recent behavior.");
            return;
        }

        if (_talkedToArchivist && CountPendingOrAcceptedClasses(psm.state) == 0)
        {
            RequestClass("small_missing_class", "Grant one class only if current observed behavior strongly supports it.");
            return;
        }

        RequestWorldLore("small_world_lore", "Add one concise grounded world note if recent observed events justify it.");
    }

    private void TryRunMajorUpdate()
    {
        if (_pendingTags.Count > 0)
            return;

        PlayerStateManager psm = PlayerStateManager.Instance;
        WorldStateManager wsm = WorldStateManager.Instance;
        if (psm == null || wsm == null || psm.state == null || wsm.State == null)
            return;

        if (wsm.State.GetCanonLines().Count < 6)
        {
            RequestWorldEvent("major_world_event", "Create one meaningful but concise world event grounded in the last several minutes of player behavior.");
            return;
        }

        if (_killCount >= 2 && CountPendingOrAcceptedTitles(psm.state) == 0)
        {
            RequestTitle("major_title", "Grant one title only if the current play history clearly supports it.");
            return;
        }

        if (_talkedToArchivist && CountPendingOrAcceptedClasses(psm.state) == 0)
            RequestClass("major_class", "Grant one class only if the current play history clearly supports it.");
    }

    private void UpdateObjectiveState()
    {
        if (!_talkedToArchivist)
            CurrentObjective = "Talk to Archivist Vey in the hub.";
        else if (_killCount < 1)
            CurrentObjective = "Clear the first echo in the trial yard, then loot the residue.";
        else if (!_usedShrine)
            CurrentObjective = "Use any shrine to stabilize your run.";
        else if (!_talkedToWarden)
            CurrentObjective = "Choose a cardinal road and report to Warden Thorne at the north gate.";
        else if (!_talkedToCardinalMentor)
            CurrentObjective = "Visit Mael, Ivara, or Sera to see how each road frames your play style.";
        else
            CurrentObjective = "Open the menu and review equipment, skills, quests, and your generated identity.";
    }

    private void RequestWorldLore(string tag, string hint)
    {
        string task = "Create one concise world-lore entry. Return JSON: {\"canonLine\":string,\"rationale\":string}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"canonLine\":\"...\",\"rationale\":\"...\"}");
        Request(tag, task + " " + hint, schema, ApplyWorldLore);
    }

    private void RequestQuest(string tag, string hint)
    {
        string task = "Create one grounded, player-facing quest that responds to what the player did. Do not name it after a region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"tags\":[string],\"confidence\":0.0}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"tags\":[\"player_response\"],\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyQuest);
    }

    private void RequestClass(string tag, string hint)
    {
        string task = "Grant one player-facing class identity only if evidence strongly supports it. Name the player's pattern, not the region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"confidence\":0.0}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyClass);
    }

    private void RequestTitle(string tag, string hint)
    {
        string task = "Grant one title only if evidence strongly supports it. The title must name the player's repeated response, not the current region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"confidence\":0.0}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyTitle);
    }

    private void RequestItem(string tag, string hint)
    {
        string task = "Offer one player-facing item only if the observed behavior clearly earned it. Return JSON: {\"name\":string,\"itemType\":\"weapon|offhand|head|chest|gloves|legs|boots|belt|cloak|ring|earring|necklace|trinket|consumable\",\"stimulus\":string,\"description\":string,\"tags\":[string],\"confidence\":0.0}. Do not choose Unity paths, materials, models, stats, or rarity.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"itemType\":\"weapon\",\"stimulus\":\"...\",\"description\":\"...\",\"tags\":[\"player_response\"],\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyItem);
    }

    private void RequestWorldEvent(string tag, string hint)
    {
        string task = "Create one concise world event. Return JSON: {\"canonLine\":string,\"rationale\":string,\"tensionDelta\":number}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"canonLine\":\"...\",\"rationale\":\"...\",\"tensionDelta\":0.05}");
        Request(tag, task + " " + hint, schema, ApplyWorldEvent);
    }

    private void RequestPlayerEvent(string tag, string hint)
    {
        string task = "Create one concise acknowledgement line. Return JSON: {\"message\":string}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"message\":\"...\"}");
        Request(tag, task + " " + hint, schema, ApplyPlayerEvent);
    }

    private void RequestProgressionDecision(string tag, string hint)
    {
        ResolveReferences();
        if (progressionDecisionApplier == null)
            return;

        string task = "Return progression JSON matching the progression applier schema. One decision only. Prefer none if evidence is weak. Skill rewards must respond directly to player stimulus; region names are context only. For jungle/nature evidence, use Auralith, the First Green, as optional lore anchor instead of region naming.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"decision\":\"skill\",\"confidence\":0.82,\"reason\":\"...\",\"payload\":{\"skillSeedName\":\"...\",\"skillType\":\"combat\",\"stimulus\":\"...\",\"hook\":\"...\",\"loreAnchor\":\"optional\"}}");
        Request(tag, task + " " + hint, schema, raw =>
        {
            // note: Progression status belongs in gameplay state and logs; the Goddess presentation channel accepts only the response's authored goddessLine.
            progressionDecisionApplier.TryApply(raw, out _, out _);
        });
    }

    private void Request(string tag, string task, string schema, Action<string> apply)
    {
        if (_pendingTags.Contains(tag))
            return;

        if (Time.unscaledTime -
            YQGeneratedWorldRuntimeBuilder
                .LastInitialGenerationGameplayUnlockTime <
            90f)
        {
            // note: Give the first playable moments their GPU/CPU back before background curation asks the local model for more text.
            return;
        }

        _pendingTags.Add(tag);
        string directive = "Player-oriented curation rules: every generated offer must answer the player's observed stimulus directly. Use regions as pressure/context only. Do not name skills, classes, or titles after region ids or biomes. The tutorial fiction begins at the Goddess statue beside Archivist Vey's witch hut, with four cardinal mentor roads. For nature evidence, use Auralith, the First Green, as an optional lore anchor while keeping the skill or quest about the player. Avoid generic fantasy filler. Every root JSON object must also contain goddessLine: 2-3 short present-tense sentences spoken by one persistent, benevolent but high-strung Goddess who is trying to make the perfect world for this player. She is intelligent, controlling, dryly sarcastic, occasionally bratty, and quietly possessive of her creation. Ground the line in the accepted event and one relevant detail from GODDESS_PERSISTENT_CONTEXT. If the player has repeatedly behaved in difficult or contradictory ways, let her composure fray gradually through a clipped clause, defensive aside, or sharper joke; keep the underlying care and never become cruel. Do not mention AI, LLM, model, generation, phase, validation, JSON, code, Unity, director, queue, delay, system status, or hidden machinery.";
        PlayerState currentPlayer = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (string.IsNullOrWhiteSpace(LastDirectorMessage) && currentPlayer != null && currentPlayer.goddessVoiceMemory != null && currentPlayer.goddessVoiceMemory.Count > 0)
            LastDirectorMessage = currentPlayer.goddessVoiceMemory[currentPlayer.goddessVoiceMemory.Count - 1] ?? string.Empty;
        string priorVoice = string.IsNullOrWhiteSpace(LastDirectorMessage) ? "No prior spoken line." : "Previous spoken line—do not reuse its wording or sentence machinery: " + LastDirectorMessage;
        string journeyContext = BuildGoddessPersistentContext(currentPlayer);
        string prompt = PromptContextBuilder.BuildContext(directive + "\n" + priorVoice + "\n" + journeyContext + "\n" + task, schema, BuildRecentSummary(), BuildBehaviorLedger());
        if (LLMClient.Instance == null)
        {
            _pendingTags.Remove(tag);
            Debug.LogWarning("[YQInvestorDirector] Local narration unavailable for " + tag + ".");
            return;
        }

        // note: Investor-facing curation must pass the same JSON gate as the production progression pipeline.
        LLMClient.Instance.Submit(new YQLlmRequest
        {
            prompt = prompt,
            debugTag = "InvestorDirector:" + tag,
            category = LLMGenerationCategory.StructuredState,
            priority = YQLlmRequestPriority.Background,
            requireJson = true
        }, result =>
        {
            // note: Failed or malformed responses leave the current accepted game state untouched.
            if (!result.success && (result.outcome == YQLlmTerminalOutcome.Cancelled ||
                result.outcome == YQLlmTerminalOutcome.Superseded ||
                result.outcome == YQLlmTerminalOutcome.Evicted))
            {
                // note: Lifecycle terminal results are observable through the scheduler but cannot mutate a replacement profile.
                _pendingTags.Remove(tag);
                return;
            }
            string raw = result.success ? result.text : null;
            _pendingTags.Remove(tag);
            if (string.IsNullOrWhiteSpace(raw))
            {
                Debug.LogWarning("[YQInvestorDirector] No accepted local response for " + tag + ".");
                return;
            }

            try
            {
                apply(raw);
                PublishGeneratedGoddessLine(raw);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[YQInvestorDirector] Apply failed for " + tag + ":\n" + ex);
            }
        });
    }

    private void PublishGeneratedGoddessLine(string raw)
    {
        JObject root = Parse(raw);
        string line = (root.Value<string>("goddessLine") ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(line))
            return;

        // note: Reject exact repeats against the persisted rolling memory as well as this session's set, so a reload cannot replay the same thought verbatim.
        PlayerState persistedState = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (persistedState != null && persistedState.goddessVoiceMemory != null)
        {
            for (int memoryIndex = 0; memoryIndex < persistedState.goddessVoiceMemory.Count; memoryIndex++)
            {
                if (string.Equals(persistedState.goddessVoiceMemory[memoryIndex], line, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        if (!_usedGoddessLines.Add(line))
        {
            // note: Repeated model prose is discarded so the visible Goddess never loops a canned response within a play session.
            return;
        }

        string lower = line.ToLowerInvariant();
        if (lower.Contains("llm") || lower.Contains("model") || lower.Contains("generation") ||
            lower.Contains("validation") || lower.Contains("json") || lower.Contains("director") ||
            lower.Contains("queue") || lower.Contains("system status"))
        {
            // note: Internal workflow text is logged, never exposed as Goddess dialogue when a local response ignores the prose contract.
            Debug.LogWarning("[YQInvestorDirector] Rejected out-of-character Goddess line: " + line);
            return;
        }

        if (!YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(line, 12))
        {
            // note: Live curation can fail safely without exposing a narrator sentence or diagnostic fragment as Goddess speech.
            Debug.LogWarning("[YQInvestorDirector] Rejected non-spoken Goddess line: " + line);
            return;
        }

        LastDirectorMessage = line;
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm != null && psm.state != null)
        {
            // note: Keep a tiny rolling voice memory in the save so reloads preserve character continuity without making dialogue canonical world state.
            psm.state.EnsureCollections();
            if (psm.state.goddessVoiceMemory == null)
                psm.state.goddessVoiceMemory = new List<string>();
            psm.state.goddessVoiceMemory.Add(line);
            while (psm.state.goddessVoiceMemory.Count > 8)
                psm.state.goddessVoiceMemory.RemoveAt(0);
            psm.Save();
        }
    }

    // note: Build a compact journey memory from persisted player state so each live Goddess thought can reference the player's actual history without dumping a save file into the prompt.
    private string BuildGoddessPersistentContext(PlayerState state)
    {
        if (state == null)
            return "GODDESS_PERSISTENT_CONTEXT\n- No persistent player context is available yet.\n- goddessLine must stay grounded in the accepted event.\n";

        StringBuilder sb = new StringBuilder(1800);
        sb.AppendLine("GODDESS_PERSISTENT_CONTEXT");
        sb.AppendLine("- This is remembered evidence from the player's journey, not new canon. Use at most one or two relevant details naturally.");
        sb.AppendLine("- player=" + SafeContext(state.displayName, "the player"));
        sb.AppendLine("- direction=" + SafeContext(state.characterLifeDirection, "unspecified"));
        sb.AppendLine("- vow=" + SafeContext(state.characterVow, "unspecified"));
        sb.AppendLine("- currentPlace=" + SafeContext(state.currentRegionName, "unknown") + " | scene=" + SafeContext(state.currentScene, "unknown"));
        sb.AppendLine("- level=" + state.level + " | currency=" + state.currency);

        if (state.generatedOrigin != null)
        {
            sb.AppendLine("- originClass=" + SafeContext(state.generatedOrigin.className, "unspecified") +
                         " | title=" + SafeContext(state.generatedOrigin.titleName, "unspecified") +
                         " | ability=" + SafeContext(state.generatedOrigin.abilityName, "unspecified"));
        }

        if (state.activeQuestId != null && state.quests != null)
        {
            for (int i = 0; i < state.quests.Count; i++)
            {
                QuestRecord quest = state.quests[i];
                if (quest != null && string.Equals(quest.questId, state.activeQuestId, StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("- activeQuest=" + SafeContext(quest.name, "unnamed") + " | status=" + SafeContext(quest.status, "active"));
                    break;
                }
            }
        }

        if (state.quests != null)
        {
            int rememberedQuests = 0;
            for (int i = state.quests.Count - 1; i >= 0 && rememberedQuests < 2; i--)
            {
                QuestRecord quest = state.quests[i];
                if (quest == null || string.Equals(quest.questId, state.activeQuestId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(quest.status, "complete", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(quest.status, "completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(quest.status, "failed", StringComparison.OrdinalIgnoreCase))
                {
                    sb.AppendLine("- resolvedQuest=" + SafeContext(quest.name, "unnamed") + " | outcome=" + SafeContext(quest.status, "resolved"));
                    rememberedQuests++;
                }
            }
        }

        if (state.classes != null && state.classes.Count > 0)
            sb.AppendLine("- unlockedClass=" + SafeContext(state.classes[state.classes.Count - 1]?.name, "unspecified"));
        if (state.skills != null && state.skills.Count > 0)
            sb.AppendLine("- unlockedSkill=" + SafeContext(state.skills[state.skills.Count - 1]?.name, "unspecified"));

        if (state.reputation != null && state.reputation.Count > 0)
        {
            int writtenReputation = 0;
            foreach (KeyValuePair<string, float> pair in state.reputation)
            {
                if (writtenReputation++ >= 3) break;
                sb.AppendLine("- relationship=" + SafeContext(pair.Key, "faction") + ":" + pair.Value.ToString("0.00"));
            }
        }

        if (state.equippedItemBySlot != null && state.equippedItemBySlot.Count > 0)
        {
            int written = 0;
            foreach (KeyValuePair<string, string> pair in state.equippedItemBySlot)
            {
                if (written++ >= 3) break;
                sb.AppendLine("- equipped=" + SafeContext(pair.Key, "slot") + ":" + SafeContext(pair.Value, "item"));
            }
        }

        if (state.behaviorLedger != null && state.behaviorLedger.Count > 0)
        {
            int start = Mathf.Max(0, state.behaviorLedger.Count - 5);
            for (int i = start; i < state.behaviorLedger.Count; i++)
                sb.AppendLine("- recentMemory=" + SafeContext(state.behaviorLedger[i], "unrecorded action"));
        }

        if (state.goddessVoiceMemory != null && state.goddessVoiceMemory.Count > 0)
        {
            int start = Mathf.Max(0, state.goddessVoiceMemory.Count - 3);
            for (int i = start; i < state.goddessVoiceMemory.Count; i++)
                sb.AppendLine("- priorGoddessThought=" + SafeContext(state.goddessVoiceMemory[i], "unrecorded thought"));
        }

        sb.AppendLine("- stability=" + ResolveGoddessStability(state));
        sb.AppendLine("- Match the player's history with the current accepted event; do not recite this block or invent relationships, discoveries, or consequences.");
        return sb.ToString();
    }

    // note: Stability is derived from persisted repeated behavior, making frustration accumulate with evidence instead of random line selection.
    private static string ResolveGoddessStability(PlayerState state)
    {
        int difficult = 0;
        int steady = 0;
        if (state != null && state.behaviorLedger != null)
        {
            for (int i = 0; i < state.behaviorLedger.Count; i++)
            {
                string entry = (state.behaviorLedger[i] ?? string.Empty).ToLowerInvariant();
                if (ContainsAny(entry, "kill", "steal", "destroy", "break", "fail", "ignore", "refuse", "contradict", "clip", "fall")) difficult++;
                if (ContainsAny(entry, "protect", "complete", "discover", "talk", "help", "craft", "repair")) steady++;
            }
        }
        if (state != null && state.behaviorCounters != null)
        {
            foreach (KeyValuePair<string, float> pair in state.behaviorCounters)
            {
                string key = (pair.Key ?? string.Empty).ToLowerInvariant();
                if (ContainsAny(key, "fail", "break", "steal", "kill", "ignore", "contradict"))
                    difficult += Mathf.Clamp(Mathf.RoundToInt(pair.Value), 0, 4);
            }
        }
        int strain = Mathf.Clamp(difficult - steady / 2, 0, 8);
        if (strain >= 6) return "slipping: concise, defensive, sharper sarcasm, still protective";
        if (strain >= 3) return "frayed: mild impatience and self-correction, still benevolent";
        if (strain >= 1) return "watchful: precise and slightly tense, quietly caring";
        return "composed: clever, benevolent, mildly smug";
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens == null) return false;
        for (int i = 0; i < tokens.Length; i++)
            if (value.Contains(tokens[i])) return true;
        return false;
    }

    private static string SafeContext(string value, string fallback)
    {
        string clean = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (clean.Length > 180) clean = clean.Substring(0, 177) + "...";
        return string.IsNullOrWhiteSpace(clean) ? fallback : clean;
    }

    private string BuildRecentSummary()
    {
        return EventSummarizer.Summarize(EventAccumulator.Instance != null ? new List<ActionEvent>(EventAccumulator.Instance.GetEvents()) : new List<ActionEvent>());
    }

    private string BuildBehaviorLedger()
    {
        return ActionRegistry.Instance != null ? ActionRegistry.Instance.BuildBehaviorSummary(12) : "No behavior recorded.";
    }

    private void ApplyWorldLore(string raw)
    {
        JObject j = Parse(raw);
        string canon = (j.Value<string>("canonLine") ?? string.Empty).Trim();
        string rationale = (j.Value<string>("rationale") ?? canon).Trim();
        if (string.IsNullOrWhiteSpace(canon) || WorldStateManager.Instance == null)
            return;

        WorldState state = WorldStateManager.Instance.State;
        List<string> canonLines = state.GetCanonLines();
        if (canonLines.Contains(canon))
            return;

        WorldStateManager.Instance.AddCanonLine(canon);
        state.lastLLMRationale = rationale;
        WorldStateManager.Instance.Save();
    }

    private void ApplyQuest(string raw)
    {
        JObject j = Parse(raw);
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return;

        string name = (j.Value<string>("name") ?? j.Value<string>("questName") ?? string.Empty).Trim();
        string description = (j.Value<string>("description") ?? string.Empty).Trim();
        string stimulus = (j.Value<string>("stimulus") ?? string.Empty).Trim();
        // note: Reject incomplete model output before curation so static safety text never becomes a normal live quest.
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }
        name = YQGeneratedContentCuration.CuratePlayerFacingName(manager.state, "quest", name, "quest", false, stimulus);
        description = YQGeneratedContentCuration.CuratePlayerFacingDescription(manager.state, "quest", name, description, "quest", false, stimulus);
        string[] tags = YQGeneratedContentCuration.BuildPlayerResponseTags(j["tags"] != null ? j["tags"].ToObject<string[]>() : Array.Empty<string>(), "quest", false, name + " " + description + " " + stimulus);
        float confidence = Mathf.Clamp01(j.Value<float?>("confidence") ?? 0.82f);
        if (string.IsNullOrWhiteSpace(name) || confidence < minimumOfferConfidence)
            return;
        if (!YQGeneratedContentCuration.PassesOfferQuality(manager.state, "quest", name, description, tags, confidence, true, out string rejectReason))
        {
            return;
        }

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            offerKind = "quest",
            name = name,
            description = description,
            confidence = confidence,
            reason = string.IsNullOrWhiteSpace(stimulus) ? "Director quest hook from observed player behavior." : "Director quest hook from player stimulus: " + stimulus,
            tags = tags,
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            payloadJson = j.ToString(Formatting.None)
        };

        PendingProgressionOfferRecord queued = manager.state.QueueOrRefreshOffer(offer, questDuplicateThreshold);
        manager.Save();
    }

    private void ApplyClass(string raw)
    {
        JObject j = Parse(raw);
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return;

        string name = (j.Value<string>("name") ?? j.Value<string>("className") ?? string.Empty).Trim();
        string description = (j.Value<string>("description") ?? string.Empty).Trim();
        string stimulus = (j.Value<string>("stimulus") ?? string.Empty).Trim();
        // note: Reject incomplete model output before curation so static safety text never becomes a normal live class.
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }
        name = YQGeneratedContentCuration.CuratePlayerFacingName(manager.state, "class", name, "class", false, stimulus);
        description = YQGeneratedContentCuration.CuratePlayerFacingDescription(manager.state, "class", name, description, "class", false, stimulus);
        float confidence = Mathf.Clamp01(j.Value<float?>("confidence") ?? 0.82f);
        if (string.IsNullOrWhiteSpace(name) || confidence < minimumOfferConfidence)
            return;
        if (!YQGeneratedContentCuration.PassesOfferQuality(manager.state, "class", name, description, Array.Empty<string>(), confidence, true, out string rejectReason))
        {
            return;
        }

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            offerKind = "class",
            name = name,
            description = description,
            confidence = confidence,
            reason = string.IsNullOrWhiteSpace(stimulus) ? "Director class identity from observed player behavior." : "Director class identity from player stimulus: " + stimulus,
            tags = YQGeneratedContentCuration.BuildPlayerResponseTags(Array.Empty<string>(), "class", false, name + " " + description + " " + stimulus),
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            payloadJson = j.ToString(Formatting.None)
        };

        PendingProgressionOfferRecord queued = manager.state.QueueOrRefreshOffer(offer, classDuplicateThreshold);
        manager.Save();
    }

    private void ApplyTitle(string raw)
    {
        JObject j = Parse(raw);
        PlayerStateManager manager = PlayerStateManager.Instance;
        if (manager == null || manager.state == null)
            return;

        string name = (j.Value<string>("name") ?? j.Value<string>("titleName") ?? string.Empty).Trim();
        string description = (j.Value<string>("description") ?? string.Empty).Trim();
        string stimulus = (j.Value<string>("stimulus") ?? string.Empty).Trim();
        // note: Reject incomplete model output before curation so static safety text never becomes a normal live title.
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(description))
        {
            return;
        }
        name = YQGeneratedContentCuration.CuratePlayerFacingName(manager.state, "title", name, "title", false, stimulus);
        description = YQGeneratedContentCuration.CuratePlayerFacingDescription(manager.state, "title", name, description, "title", false, stimulus);
        float confidence = Mathf.Clamp01(j.Value<float?>("confidence") ?? 0.82f);
        if (string.IsNullOrWhiteSpace(name) || confidence < minimumOfferConfidence)
            return;
        if (!YQGeneratedContentCuration.PassesOfferQuality(manager.state, "title", name, description, Array.Empty<string>(), confidence, true, out string rejectReason))
        {
            return;
        }

        PendingProgressionOfferRecord offer = new PendingProgressionOfferRecord
        {
            offerKind = "title",
            name = name,
            description = description,
            confidence = confidence,
            reason = string.IsNullOrWhiteSpace(stimulus) ? "Director title acknowledgement from observed player behavior." : "Director title acknowledgement from player stimulus: " + stimulus,
            tags = YQGeneratedContentCuration.BuildPlayerResponseTags(Array.Empty<string>(), "title", false, name + " " + description + " " + stimulus),
            offeredUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            payloadJson = j.ToString(Formatting.None)
        };

        PendingProgressionOfferRecord queued = manager.state.QueueOrRefreshOffer(offer, titleDuplicateThreshold);
        manager.Save();
    }

    private void ApplyItem(string raw)
    {
        ResolveReferences();
        if (progressionDecisionApplier == null)
            return;

        JObject item = Parse(raw);
        JObject decision = new JObject
        {
            // note: Item offers reuse the validated progression contract before PlayerState asks the curated service to materialize them.
            ["decision"] = "item",
            ["confidence"] = item.Value<float?>("confidence") ?? 0f,
            ["reason"] = "Director item offer from observed player behavior.",
            ["payload"] = item
        };

        progressionDecisionApplier.TryApply(decision.ToString(Formatting.None), out _, out _);
    }

    private void ApplyWorldEvent(string raw)
    {
        JObject j = Parse(raw);
        string canon = (j.Value<string>("canonLine") ?? string.Empty).Trim();
        string rationale = (j.Value<string>("rationale") ?? canon).Trim();
        float tensionDelta = j.Value<float?>("tensionDelta") ?? 0.05f;
        if (string.IsNullOrWhiteSpace(canon) || WorldStateManager.Instance == null)
            return;

        WorldStateManager.Instance.AddCanonLine(canon);
        WorldStateManager.Instance.SetTension(WorldStateManager.Instance.State.tension + tensionDelta);
        WorldStateManager.Instance.State.lastLLMRationale = rationale;
        WorldStateManager.Instance.Save();
    }

    private void ApplyPlayerEvent(string raw)
    {
        JObject j = Parse(raw);
        string message = (j.Value<string>("message") ?? string.Empty).Trim();
        // note: The structured message remains gameplay data; only goddessLine is allowed into the Goddess presentation channel.
    }

    private static JObject Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return new JObject();

        int start = raw.IndexOf('{');
        int end = raw.LastIndexOf('}');
        if (start >= 0 && end > start)
            raw = raw.Substring(start, end - start + 1);
        return JObject.Parse(raw);
    }

    private static int ActiveQuestCount(PlayerState state)
    {
        if (state == null || state.quests == null)
            return 0;
        int count = 0;
        for (int i = 0; i < state.quests.Count; i++)
        {
            QuestRecord quest = state.quests[i];
            string status = quest != null ? (quest.status ?? string.Empty) : string.Empty;
            if (quest != null &&
                !status.Equals("complete", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("completed", StringComparison.OrdinalIgnoreCase) &&
                !status.Equals("failed", StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static int CountPendingOrAcceptedClasses(PlayerState state)
    {
        int count = 0;
        if (state != null && state.classes != null)
            count += state.classes.Count;
        if (state != null && state.pendingOffers != null)
        {
            for (int i = 0; i < state.pendingOffers.Count; i++)
            {
                PendingProgressionOfferRecord offer = state.pendingOffers[i];
                if (offer != null && offer.IsPending && string.Equals(offer.offerKind, "class", StringComparison.OrdinalIgnoreCase))
                    count++;
            }
        }
        return count;
    }

    private static int CountPendingOrAcceptedTitles(PlayerState state)
    {
        int count = 0;
        if (state != null && state.titles != null)
            count += state.titles.Count;
        if (state != null && state.pendingOffers != null)
        {
            for (int i = 0; i < state.pendingOffers.Count; i++)
            {
                PendingProgressionOfferRecord offer = state.pendingOffers[i];
                if (offer != null && offer.IsPending && string.Equals(offer.offerKind, "title", StringComparison.OrdinalIgnoreCase))
                    count++;
            }
        }
        return count;
    }

    private void ResolveReferences()
    {
        if (player == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        if (snapshotBuilder == null)
            snapshotBuilder = FindFirstObjectByType<SituationSnapshotBuilder>();
        if (progressionDecisionApplier == null)
            progressionDecisionApplier = FindFirstObjectByType<ProgressionDecisionApplier>();
        if (worldDeltaApplier == null)
            worldDeltaApplier = FindFirstObjectByType<WorldDeltaApplier>();
        if (contentService == null)
            contentService = GeneratedRpgContentService.Instance != null ? GeneratedRpgContentService.Instance : FindFirstObjectByType<GeneratedRpgContentService>();
    }
}
