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

    // note: Presentation dedupe belongs to the active player/session, not every profile visited by this persistent component.
    private string _voicePlayerId = string.Empty;
    private int _voiceEpoch = -1;
    private int _voiceSequence;
    private YQRepairEpisode _voiceEpisode;
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

    private void OnDisable()
    {
        // note: Retire the exact pending speech children when this presentation owner is disabled or destroyed.
        _voiceSequence++;
        if (_voiceEpisode == null || _voiceEpisode.IsTerminal) return;
        _voiceEpisode.Finish("Superseded");
        LLMClient.Instance?.CancelRepairEpisode(_voiceEpisode);
        _voiceEpisode = null;
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
        Request(tag, task + " " + hint, schema, ApplyWorldLore, YQGoddessSpeechPlan.Purpose.WorldDescription);
    }

    private void RequestQuest(string tag, string hint)
    {
        string task = "Create one grounded, player-facing quest that responds to what the player did. Do not name it after a region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"tags\":[string],\"confidence\":0.0}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"tags\":[\"player_response\"],\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyQuest, YQGoddessSpeechPlan.Purpose.Guidance);
    }

    private void RequestClass(string tag, string hint)
    {
        string task = "Grant one player-facing class identity only if evidence strongly supports it. Name the player's pattern, not the region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"confidence\":0.0}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyClass, YQGoddessSpeechPlan.Purpose.Progression);
    }

    private void RequestTitle(string tag, string hint)
    {
        string task = "Grant one title only if evidence strongly supports it. The title must name the player's repeated response, not the current region. Return JSON: {\"name\":string,\"stimulus\":string,\"description\":string,\"confidence\":0.0}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"name\":\"...\",\"stimulus\":\"...\",\"description\":\"...\",\"confidence\":0.82}");
        Request(tag, task + " " + hint, schema, ApplyTitle, YQGoddessSpeechPlan.Purpose.Progression);
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
        Request(tag, task + " " + hint, schema, ApplyWorldEvent, YQGoddessSpeechPlan.Purpose.WorldDescription);
    }

    private void RequestPlayerEvent(string tag, string hint)
    {
        string task = "Create one concise acknowledgement line. Return JSON: {\"message\":string}.";
        string schema = PromptContextBuilder.WrapJsonSchema("{\"message\":\"...\"}");
        Request(tag, task + " " + hint, schema, ApplyPlayerEvent, YQGoddessSpeechPlan.Purpose.AcknowledgeChoice);
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
            // note: Progression status stays in gameplay state and logs; the separate speech transaction reads only post-apply records.
            progressionDecisionApplier.TryApply(raw, out _, out _);
        }, YQGoddessSpeechPlan.Purpose.Progression);
    }

    private void Request(string tag, string task, string schema, Action<string> apply,
        YQGoddessSpeechPlan.Purpose speechPurpose = YQGoddessSpeechPlan.Purpose.Curation)
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
        // note: Gameplay proposals retain their existing authority; speech is generated separately from the post-apply accepted snapshot.
        string directive = "Player-oriented curation rules: every generated offer must answer the player's observed stimulus directly. Use regions as pressure/context only. Do not name skills, classes, or titles after region ids or biomes. The tutorial fiction begins at the Goddess statue beside Archivist Vey's witch hut, with four cardinal mentor roads. For nature evidence, use Auralith, the First Green, as an optional lore anchor while keeping the skill or quest about the player. Avoid generic fantasy filler.";
        string prompt = PromptContextBuilder.BuildContext(directive + "\n" + task, schema, BuildRecentSummary(), BuildBehaviorLedger());
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
            // note: A retired or disabled voice owner cannot publish a late thought into another presentation.
            ownerStillCurrent = () => this != null && isActiveAndEnabled,
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
                RequestGoddessSpeech(tag, task, speechPurpose);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[YQInvestorDirector] Apply failed for " + tag + ":\n" + ex);
            }
        });
    }

    private void RequestGoddessSpeech(string tag, string task, YQGoddessSpeechPlan.Purpose purpose)
    {
        BindGoddessVoiceOwner();
        if (_voiceEpisode != null && !_voiceEpisode.IsTerminal)
        {
            _voiceEpisode.Finish("Superseded");
            LLMClient.Instance?.CancelRepairEpisode(_voiceEpisode);
        }
        int sequence = ++_voiceSequence;
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        JObject evidence = YQGoddessLoadingVoice.CaptureKnownContext(state, world);
        string playerId = state?.playerId ?? string.Empty;
        string worldId = world?.worldIdentity?.worldId ?? string.Empty;
        long playerRevision = state?.stateRevision ?? -1, worldRevision = world?.stateRevision ?? -1;
        int epoch = YQServiceLifecycle.RequestEpoch;
        // note: An unapplied reward is not a voice fact. The current objective and typed post-apply records supply guidance.
        string speechTask = "React to the current curation request without announcing an unaccepted outcome. Use relevant accepted facts; guide toward the unfinished objective when useful. Curation request: " + task;
        _voiceEpisode = YQGoddessSpeech.Request(LLMClient.Instance, evidence, speechTask, tag,
            () => this != null && isActiveAndEnabled && sequence == _voiceSequence && epoch == YQServiceLifecycle.RequestEpoch &&
                string.Equals(PlayerStateManager.Instance?.state?.playerId ?? string.Empty, playerId, StringComparison.Ordinal) &&
                string.Equals(WorldStateManager.Instance?.State?.worldIdentity?.worldId ?? string.Empty, worldId, StringComparison.Ordinal) &&
                PlayerStateManager.Instance?.state?.stateRevision == playerRevision && WorldStateManager.Instance?.State?.stateRevision == worldRevision,
            result =>
            {
                // note: Background curation failure is not a player question; keep the last accepted thought instead of repeating an unrelated abstention.
                if (result.isFallback)
                {
                    Debug.LogWarning("[YQInvestorDirector] Goddess speech " + result.disposition);
                    return;
                }
                PublishGeneratedGoddessLine(JsonConvert.SerializeObject(new { goddessLine = result.line }));
            }, purpose);
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

        if (!YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(line, 12))
        {
            // note: Live curation can fail safely without exposing a narrator sentence or diagnostic fragment as Goddess speech.
            Debug.LogWarning("[YQInvestorDirector] Rejected non-spoken Goddess line: " + line);
            return;
        }

        // note: Rejected speech must not poison the session's accepted-speech index; accepted repeats remain silent.
        if (!_usedGoddessLines.Add(line)) return;
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
        // note: Read the selected player's paired world only; prior speech supplies cadence, never new factual authority.
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        return YQGoddessLoadingVoice.BuildKnownContextForPrompt(state, world) +
            "COMPOSURE_HINT: " + ResolveGoddessStability(state) + "\n" +
            "This hint affects phrasing only. Never diagnose the player or invent an event to justify it.\n";
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
                if (!float.IsNaN(pair.Value) && !float.IsInfinity(pair.Value) &&
                    ContainsAny(key, "fail", "break", "steal", "kill", "ignore", "contradict"))
                    difficult += Mathf.Clamp(Mathf.RoundToInt(pair.Value), 0, 4);
            }
        }
        int strain = Mathf.Clamp(difficult - steady / 2, 0, 8);
        if (strain >= 6) return "slipping: a hurried correction covers panic; she recovers precision and stays invested in the player";
        if (strain >= 3) return "frayed: clinical precision becomes over-specific; one defensive aside reveals effort";
        if (strain >= 1) return "watchful: precise, controlling and privately concerned; dry humor covers the concern";
        return "composed: clever, clinical and mildly smug; one concrete detail shows she knows this person";
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(value) || tokens == null) return false;
        for (int i = 0; i < tokens.Length; i++)
        {
            int offset = 0;
            while ((offset = value.IndexOf(tokens[i], offset, StringComparison.Ordinal)) >= 0)
            {
                // note: A skill record is not a kill, and a class name is not a record of misconduct.
                int end = offset + tokens[i].Length;
                if ((offset == 0 || !char.IsLetter(value[offset - 1])) &&
                    (end == value.Length || !char.IsLetter(value[end]))) return true;
                offset = end;
            }
        }
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

    private void BindGoddessVoiceOwner()
    {
        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        string playerId = state?.playerId ?? string.Empty;
        int epoch = YQServiceLifecycle.RequestEpoch;
        if (_voiceEpoch == epoch && string.Equals(_voicePlayerId, playerId, StringComparison.Ordinal)) return;
        // note: Retire transient work on an owner change, then restore only this player's previously accepted presentation memory.
        _voicePlayerId = playerId;
        _voiceEpoch = epoch;
        _voiceSequence++;
        if (_voiceEpisode != null && !_voiceEpisode.IsTerminal)
        {
            _voiceEpisode.Finish("Superseded");
            LLMClient.Instance?.CancelRepairEpisode(_voiceEpisode);
        }
        _voiceEpisode = null;
        LastDirectorMessage = string.Empty;
        _usedGoddessLines.Clear();
        if (state?.goddessVoiceMemory == null) return;
        for (int index = state.goddessVoiceMemory.Count - 1; index >= 0; index--)
        {
            string remembered = state.goddessVoiceMemory[index];
            if (!YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(remembered, 12)) continue;
            LastDirectorMessage = remembered;
            break;
        }
    }

    private void ResolveReferences()
    {
        BindGoddessVoiceOwner();
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
