using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class YQOriginQuestionnaireUI : MonoBehaviour
{
    private const string CompletionCounter = "origin:questionnaire_complete";

    private static readonly string[] BaseQuestions =
    {
        "When danger blocks your road, what do you do first?",
        "What kind of work feels honest to you?",
        "What do you want strangers to notice before they know your name?",
        "If you woke in a forest with nothing but time, what would you build first?",
        "Which weapon, tool, or sign would you reach for by instinct?",
        "What kind of magic frightens you least?",
        "When someone lies to your face, how do you respond?",
        "What do you owe to people weaker than you?",
        "What would make you leave home without looking back?",
        "Which place sounds most like a beginning: road, forge, shrine, market, or wilds?",
        "Do you prefer precision, force, patience, charm, or secrecy?",
        "What do you protect when no one is watching?",
        "What kind of rival would sharpen you instead of breaking you?",
        "What do you do with a locked chest in an empty room?",
        "What rumor would you follow even if it sounded foolish?",
        "When a plan fails, what part of you takes over?",
        "Which element feels most familiar: fire, ice, storm, earth, water, shadow, or light?",
        "What trade would you survive on if no one cared about heroics?",
        "What kind of teacher would you trust?",
        "What is worse: hunger, boredom, debt, shame, or being powerless?",
        "What would you refuse to sell?",
        "How do you want victory to feel?",
        "What do you do when a stranger asks for mercy?",
        "What kind of door should never be opened?",
        "What name would a campfire story give you?"
    };

    private readonly List<string> _answers = new List<string>(128);
    private Canvas _canvas;
    private RectTransform _panel;
    private TMP_Text _title;
    private TMP_Text _body;
    private TMP_InputField _input;
    private Button _nextButton;
    private CanvasGroup _panelCanvasGroup;
    private Coroutine _panelRevealRoutine;
    private int _targetQuestions;
    private int _questionIndex;
    private int _modeButtonCount;
    private string _mode = string.Empty;
    private bool _isCompleting;
    private bool _startupPhaseResolved = true;

    public bool StartupPhaseResolved => _startupPhaseResolved;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQOriginQuestionnaireUI>() != null)
            return;

        GameObject go = new GameObject("00__YQ_OriginQuestionnaireUI");
        DontDestroyOnLoad(go);
        go.AddComponent<YQOriginQuestionnaireUI>();
    }

    private IEnumerator Start()
    {
        yield return null;
        BuildUi();
        if (ShouldOpen())
            OpenModeSelection();
        else
            SetVisible(false);
    }

    private bool ShouldOpen()
    {
        if (!YQTitleScreenUI.CanOpenOriginQuestionnaire)
            return false;

        PlayerState state = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (state == null)
            return false;

        state.EnsureCollections();
        return !state.behaviorCounters.TryGetValue(CompletionCounter, out float completed) || completed <= 0f;
    }

    public bool OpenIfNeededAfterTitle()
    {
        if (_canvas == null)
            BuildUi();

        if (ShouldOpen())
        {
            _startupPhaseResolved = false;
            OpenModeSelection();
            return true;
        }
        else
        {
            _startupPhaseResolved = true;
            SetVisible(false);
            RuntimeModalUiBlocker.SetMenuOpen(false);
            return false;
        }
    }

    private void OpenModeSelection()
    {
        _answers.Clear();
        _questionIndex = 0;
        _mode = string.Empty;
        SetVisible(true);
        RuntimeModalUiBlocker.SetMenuOpen(true);

        _title.text = "The Goddess at the Threshold";
        _body.text = "Before the road gives you a name, choose how deeply she reads you.";
        _input.gameObject.SetActive(false);
        _nextButton.gameObject.SetActive(false);

        ClearModeButtons();
        CreateModeButton("Casual", "10 questions", 10);
        CreateModeButton("In Depth", "25 questions", 25);
        CreateModeButton("Hardcore", "100 questions", 100);
    }

    private void BeginQuestions(string mode, int count)
    {
        _mode = mode;
        _targetQuestions = Mathf.Clamp(count, 1, 100);
        _questionIndex = 0;
        _isCompleting = false;
        _answers.Clear();
        ClearModeButtons();
        _input.gameObject.SetActive(true);
        _nextButton.gameObject.SetActive(true);
        RenderQuestion();
    }

    private void RenderQuestion()
    {
        _title.text = _mode + " Origin";
        _body.text = "Question " + (_questionIndex + 1) + " / " + _targetQuestions + "\n\n" + GetQuestion(_questionIndex);
        _input.text = string.Empty;
        _input.ActivateInputField();
        _input.Select();
    }

    private string GetQuestion(int index)
    {
        if (index < BaseQuestions.Length)
            return BaseQuestions[index];

        string seed = BaseQuestions[index % BaseQuestions.Length];
        int layer = index / BaseQuestions.Length + 1;
        return seed + "\nAnswer it as your future self, layer " + layer + ".";
    }

    private void SubmitAnswer()
    {
        if (_targetQuestions <= 0)
            return;
        if (_isCompleting)
            return;

        string answer = _input != null ? _input.text.Trim() : string.Empty;
        if (string.IsNullOrWhiteSpace(answer))
        {
            _input.ActivateInputField();
            return;
        }

        _answers.Add(answer);
        _questionIndex++;
        if (_questionIndex >= _targetQuestions)
        {
            CompleteQuestionnaire();
            return;
        }

        RenderQuestion();
    }

    private void CompleteQuestionnaire()
    {
        if (_isCompleting)
            return;

        PlayerStateManager psm = PlayerStateManager.Instance;
        PlayerState state = psm != null ? psm.state : null;
        if (state == null)
        {
            Close();
            return;
        }

        state.EnsureCollections();
        _isCompleting = true;
        ShowCompletionWait();
        List<string> answerSnapshot = new List<string>(_answers);
        YQOriginGenerationService originGenerator = YQOriginGenerationService.Instance != null
            ? YQOriginGenerationService.Instance
            : FindAnyObjectByType<YQOriginGenerationService>();
        if (originGenerator != null && originGenerator.TryRequestOrigin(state, _mode, answerSnapshot, generated =>
            {
                _isCompleting = false;
                FinalizeQuestionnaire(generated);
            }))
        {
            return;
        }

        _isCompleting = false;
        FinalizeQuestionnaire(null);
    }

    private void FinalizeQuestionnaire(YQOriginGenerationDto generated)
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        PlayerState state = psm != null ? psm.state : null;
        if (state == null)
        {
            Close();
            return;
        }

        state.EnsureCollections();

        // note: Progress can advance without injecting static Goddess text into the generated transcript.
        YQStartupLoadingScreen.SetGenerationStage(
            string.Empty,
            0.25f);

        OriginResult result =
            generated != null
                ? AnalyzeGeneratedOrigin(
                    state,
                    generated)
                : AnalyzeAnswers(
                    state);

        long now =
            DateTimeOffset.UtcNow
                .ToUnixTimeSeconds();

        state.originQuestionnaireMode = _mode;
        state.originQuestionnaireAnswers = new List<string>(_answers);
        state.identityKeywords = result.identityKeywords;
        state.generatedOrigin = BuildGeneratedOriginRecord(result, generated, _mode, now);
        ApplyOriginStats(state, result);
        state.AwardClass(result.className, result.classDescription);
        state.AwardTitle(result.title, result.titleDescription);
        state.UpsertSkill(new SkillRecord
        {
            skillId = Guid.NewGuid().ToString("N"),
            familyId = "origin_" + Mathf.Abs(StableHash(result.directionKey + ":" + result.abilityName)).ToString("x"),
            rank = 1,
            unlocked = true,
            context = "origin:" + result.directionKey,
            environment = "goddess_questionnaire",
            learnedUnix = now,
            acquiredUnix = now,
            name = result.abilityName,
            type = result.isSpell ? "spell" : "skill",
            tier = 1,
            description = result.abilityDescription,
            isSpell = result.isSpell,
            targetingMode = result.targetingMode,
            resourceType = result.resourceType,
            resourceCost = result.resourceCost,
            cooldownSeconds = result.cooldownSeconds,
            vfxFamily = result.vfxFamily,
            animationIntent = result.animationIntent,
            payloadJson = result.generationPayloadJson
        });

        SkillRecord granted = state.FindSkillByName(result.abilityName);
        if (granted != null)
            state.equippedSkillBySlot[result.isSpell ? "spell" : "active"] = granted.skillId;

        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        List<InventoryItemRecord> loadout = content != null
            ? content.GrantOriginStartingLoadout(state, result.directionKey, result.stimulus, result.identityKeywords != null ? result.identityKeywords.ToArray() : null, result.loadoutHints)
            : new List<InventoryItemRecord>();

        state.OfferQuest(result.questName, result.questDescription, result.questTags);
        QuestRecord originQuest = FindQuestByName(state, result.questName);
        if (originQuest != null)
        {
            originQuest.EnsureCollections();
            originQuest.generationSource = result.generationSource;
            originQuest.generatorPromptHash = result.seed;
            originQuest.payloadJson = result.generationPayloadJson;
            originQuest.objectives = result.questObjectives ?? BuildDefaultOriginObjectives();
            state.SetActiveQuest(originQuest.questId);
        }

        state.IncCounter(CompletionCounter, 1f);
        state.IncCounter("tutorial:stage:goddess_questionnaire", 1f);
        Vector3 manifestPosition = ResolveManifestPosition();
        YQGeneratedRuntimeVfx.SpawnOriginManifestation(manifestPosition, result.abilityName + " " + result.abilityDescription + " " + result.directionKey);
        YQRuntimeAudioFeedback.PlayOriginManifest(manifestPosition);
        content?.SetInventoryMessage("The goddess manifested " + loadout.Count + " origin items and named " + result.className + ".");
        state.AddLedgerLine("The goddess read the player's origin, named them " + result.title + ", and manifested a " + result.directionKey + " loadout.");
        WorldStateManager wsm = WorldStateManager.Instance;
        if (wsm != null && wsm.State != null)
        {
            YQWorldGenerationService worldGenerator =
    YQWorldGenerationService.Instance != null
        ? YQWorldGenerationService.Instance
        : FindAnyObjectByType<
            YQWorldGenerationService>();

            YQStartupLoadingScreen.SetGenerationStage(
    YQGoddessGenerationDialogue
        .TakeOriginTransition(
            string.Empty),
    0.40f);

            worldGenerator?.RegenerateAfterOrigin(
                state,
                wsm.State,
                true);

            wsm.Save();

        }
        psm.Save();
        Close();
    }

    private void ShowCompletionWait()
    {
        /*
         * Questionnaire is complete.
         *
         * Hand presentation over to the shared full-screen generation
         * overlay instead of leaving the questionnaire looking frozen
         * while Ollama works.
         */
        // note: Connection status is animated by the loading screen until the first model-authored line arrives.
        // note: The completed questionnaire hands its authored Goddess portrait directly to the generation HUD; the stage remains loaded while Ollama and deterministic compilation work.
        YQTitleEnvironmentLoader.HoldForWorldGeneration();
        YQStartupLoadingScreen.SetGenerationStage(
            "Securing connection...",
            0.10f);

        if (_input != null)
            _input.gameObject.SetActive(false);

        if (_nextButton != null)
            _nextButton.gameObject.SetActive(false);

        /*
         * Hide the questionnaire canvas completely.
         * YQStartupLoadingScreen now owns the black screen.
         */
        SetVisible(false);
    }

    private OriginResult AnalyzeGeneratedOrigin(PlayerState state, YQOriginGenerationDto generated)
    {
        OriginResult result = new OriginResult();
        YQOriginGeneratedAbilityDto ability = generated != null ? generated.ability : null;
        YQOriginGeneratedQuestDto quest = generated != null ? generated.quest : null;
        YQOriginGeneratedStatsDto stats = generated != null ? generated.stats : null;

        result.directionKey = SafeString(generated != null ? generated.directionKey : null, "custom");
        result.seed = SafeString(generated != null ? generated.seed : null, string.Empty);
        result.generationSource = SafeString(generated != null ? generated.source : null, "llm_origin_v1");
        result.generationPayloadJson = generated != null ? generated.rawJson : string.Empty;
        result.loadoutHints = generated != null ? generated.loadout : null;
        result.stimulus = SafeString(generated != null ? generated.stimulus : null, "the player's questionnaire answers");
        result.isSpell = ability != null && string.Equals(ability.kind, "spell", StringComparison.OrdinalIgnoreCase);
        result.identityKeywords = new List<string>(generated != null && generated.identityKeywords != null ? generated.identityKeywords : Array.Empty<string>());
        EnsureIdentityKeyword(result.identityKeywords, "origin_generated");
        EnsureIdentityKeyword(result.identityKeywords, result.directionKey);

        result.className = YQGeneratedContentCuration.CuratePlayerFacingName(state, "class", generated != null ? generated.className : string.Empty, "origin", false, result.stimulus);
        result.classDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(state, "class", result.className, generated != null ? generated.classDescription : string.Empty, "origin", false, result.stimulus);
        result.title = YQGeneratedContentCuration.CuratePlayerFacingName(state, "title", generated != null ? generated.titleName : string.Empty, "origin", false, result.stimulus);
        result.titleDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(state, "title", result.title, generated != null ? generated.titleDescription : string.Empty, "origin", false, result.stimulus);
        result.abilityName = YQGeneratedContentCuration.CuratePlayerFacingName(state, result.isSpell ? "spell" : "skill", ability != null ? ability.name : string.Empty, "origin", result.isSpell, result.stimulus);
        result.abilityDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(state, result.isSpell ? "spell" : "skill", result.abilityName, ability != null ? ability.description : string.Empty, "origin", result.isSpell, result.stimulus);
        result.questName = YQGeneratedContentCuration.CuratePlayerFacingName(state, "quest", quest != null ? quest.name : string.Empty, "origin", false, result.stimulus);
        result.questDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(state, "quest", result.questName, quest != null ? quest.description : string.Empty, "origin", false, result.stimulus);
        result.questTags = YQGeneratedContentCuration.BuildPlayerResponseTags(quest != null ? quest.tags : Array.Empty<string>(), "origin", result.isSpell, result.questName + " " + result.questDescription + " " + result.stimulus);
        result.questObjectives = ConvertQuestObjectives(quest != null ? quest.objectives : null, result.seed);

        result.vitality = stats != null ? stats.vitality : 10;
        result.strength = stats != null ? stats.strength : 10;
        result.dexterity = stats != null ? stats.dexterity : 10;
        result.intelligence = stats != null ? stats.intelligence : 10;
        result.targetingMode = SafeString(ability != null ? ability.targetingMode : null, result.isSpell ? "projectile" : "melee");
        result.resourceType = SafeString(ability != null ? ability.resourceType : null, result.isSpell ? "mana" : "stamina");
        result.resourceCost = Mathf.Clamp(ability != null ? ability.resourceCost : 15, 0, 60);
        result.cooldownSeconds = Mathf.Clamp(ability != null ? ability.cooldownSeconds : 0.65f, 0.15f, 12f);
        result.vfxFamily = SafeString(ability != null ? ability.vfxFamily : null, "arcane");
        result.animationIntent = SafeString(ability != null ? ability.animationIntent : null, result.isSpell ? "cast" : "melee");

        if (string.IsNullOrWhiteSpace(result.className) ||
            string.IsNullOrWhiteSpace(result.title) ||
            string.IsNullOrWhiteSpace(result.abilityName) ||
            string.IsNullOrWhiteSpace(result.questName))
        {
            return AnalyzeAnswers(state);
        }

        return result;
    }

    private static GeneratedOriginRecord BuildGeneratedOriginRecord(OriginResult result, YQOriginGenerationDto generated, string mode, long now)
    {
        return new GeneratedOriginRecord
        {
            source = result != null ? result.generationSource : "deterministic_origin_fallback",
            seed = result != null ? result.seed : string.Empty,
            mode = generated != null ? generated.mode : SafeString(mode, string.Empty),
            directionKey = result != null ? result.directionKey : string.Empty,
            stimulus = result != null ? result.stimulus : string.Empty,
            className = result != null ? result.className : string.Empty,
            titleName = result != null ? result.title : string.Empty,
            abilityName = result != null ? result.abilityName : string.Empty,
            abilityKind = result != null && result.isSpell ? "spell" : "skill",
            questName = result != null ? result.questName : string.Empty,
            tags = result != null && result.identityKeywords != null ? result.identityKeywords.ToArray() : Array.Empty<string>(),
            rawJson = result != null ? result.generationPayloadJson : string.Empty,
            generatedUnix = now
        };
    }

    private static List<QuestObjectiveRecord> ConvertQuestObjectives(YQOriginGeneratedObjectiveDto[] objectives, string seed)
    {
        if (objectives == null || objectives.Length == 0)
            return BuildDefaultOriginObjectives();

        List<QuestObjectiveRecord> records = new List<QuestObjectiveRecord>();
        for (int i = 0; i < objectives.Length; i++)
        {
            YQOriginGeneratedObjectiveDto objective = objectives[i];
            if (objective == null || string.IsNullOrWhiteSpace(objective.type))
                continue;

            records.Add(new QuestObjectiveRecord
            {
                objectiveId = BuildObjectiveId(seed, objective.type, i),
                type = objective.type.Trim(),
                targetId = SafeString(objective.targetId, string.Empty),
                targetName = SafeString(objective.targetName, string.Empty),
                counterKey = SafeString(objective.counterKey, string.Empty),
                counterPrefix = SafeString(objective.counterPrefix, string.Empty),
                requiredCount = Mathf.Max(1f, objective.requiredCount <= 0f ? 1f : objective.requiredCount),
                description = SafeString(objective.description, objective.type.Trim())
            });
        }

        if (records.Count == 0)
            return BuildDefaultOriginObjectives();
        EnsureObjective(records, "origin_manifested", "origin:equipment_manifested", string.Empty, string.Empty, "The goddess manifests gear from the player's answers.");
        EnsureObjective(records, "equip_item", string.Empty, "item:equip", string.Empty, "Equip one manifested item.");
        EnsureObjective(records, "talk_to_npc", string.Empty, "dialogue:npc_archivist_01", "npc_archivist_01", "Speak with Archivist Vey.");
        return records;
    }

    private static List<QuestObjectiveRecord> BuildDefaultOriginObjectives()
    {
        List<QuestObjectiveRecord> records = new List<QuestObjectiveRecord>();
        EnsureObjective(records, "origin_manifested", "origin:equipment_manifested", string.Empty, string.Empty, "The goddess manifests gear from the player's answers.");
        EnsureObjective(records, "equip_item", string.Empty, "item:equip", string.Empty, "Equip one manifested item.");
        EnsureObjective(records, "talk_to_npc", string.Empty, "dialogue:npc_archivist_01", "npc_archivist_01", "Speak with Archivist Vey.");
        return records;
    }

    private static void EnsureObjective(List<QuestObjectiveRecord> records, string type, string counterKey, string counterPrefix, string targetId, string description)
    {
        if (records == null)
            return;

        for (int i = 0; i < records.Count; i++)
        {
            QuestObjectiveRecord existing = records[i];
            if (existing == null)
                continue;
            if (!string.Equals(existing.type, type, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(targetId) && !string.Equals(existing.targetId, targetId, StringComparison.OrdinalIgnoreCase))
                continue;
            return;
        }

        records.Add(new QuestObjectiveRecord
        {
            objectiveId = BuildObjectiveId("origin", type, records.Count),
            type = type,
            targetId = targetId,
            targetName = targetId == "npc_archivist_01" ? "Archivist Vey" : string.Empty,
            counterKey = counterKey,
            counterPrefix = counterPrefix,
            requiredCount = 1f,
            description = description
        });
    }

    private static string BuildObjectiveId(string seed, string type, int index)
    {
        return "objective_" + Mathf.Abs(StableHash((seed ?? string.Empty) + ":" + (type ?? string.Empty) + ":" + index)).ToString("x");
    }

    private static void EnsureIdentityKeyword(List<string> keywords, string keyword)
    {
        if (keywords == null || string.IsNullOrWhiteSpace(keyword))
            return;

        for (int i = 0; i < keywords.Count; i++)
        {
            if (string.Equals(keywords[i], keyword, StringComparison.OrdinalIgnoreCase))
                return;
        }

        keywords.Add(keyword);
    }

    private OriginResult AnalyzeAnswers(PlayerState state)
    {
        string characterText = BuildCharacterCreationText(state);
        string text = (string.Join(" ", _answers) + " " + characterText).ToLowerInvariant();
        Dictionary<string, int> scores = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        ScoreBucket(scores, "martial", text, 2, "fight", "fist", "kick", "blade", "weapon", "force", "victory", "rival", "hero", "protect");
        ScoreBucket(scores, "craft", text, 2, "wood", "tree", "build", "axe", "lumber", "forge", "craft", "tool", "honest", "work");
        ScoreBucket(scores, "trade", text, 2, "merchant", "market", "coin", "debt", "sell", "trade", "bargain", "ledger", "shop", "profit");
        ScoreBucket(scores, "magic", text, 2, "magic", "spell", "rune", "witch", "sorcerer", "warlock", "shrine", "mana", "light", "storm", "ice", "fire");
        ScoreBucket(scores, "shadow", text, 3, "demon", "demonlord", "abyss", "shadow", "curse", "dark", "dominion", "overlord", "fear");
        ScoreBucket(scores, "guard", text, 2, "guard", "shield", "protect", "mercy", "weaker", "defend", "oath", "rescue", "save");
        ScoreBucket(scores, "patience", text, 3, "wait", "still", "patient", "lazy", "afk", "stone", "rest", "boredom", "watch");
        ScoreBucket(scores, "mobility", text, 1, "road", "run", "dash", "dodge", "wander", "travel", "leave", "escape", "move");
        ScoreBucket(scores, "curiosity", text, 1, "rumor", "door", "locked", "chest", "secret", "question", "learn", "read", "teacher");
        ScoreBucket(scores, "survival", text, 1, "hunger", "survive", "shelter", "heal", "safe", "forest", "wilds", "water", "earth");

        string direction = ResolveOriginDirection(scores, text);
        string seed = _mode + ":" + _targetQuestions + ":" + text + ":" + (state != null ? state.characterCreationSeed : string.Empty);
        bool isSpell = DirectionPrefersSpell(direction, scores);
        string stimulus = ResolveOriginStimulus(direction, text);

        OriginResult result = new OriginResult();
        result.directionKey = direction;
        result.seed = Mathf.Abs(StableHash(seed)).ToString("x");
        result.generationSource = "deterministic_origin_fallback";
        result.generationPayloadJson = string.Empty;
        result.stimulus = stimulus;
        result.isSpell = isSpell;
        result.identityKeywords = BuildIdentityKeywords(direction, scores, text);
        AddCharacterCreationKeywords(result.identityKeywords, state);
        result.className = BuildOriginClassName(direction, seed);
        result.title = BuildOriginTitle(direction, seed);
        result.abilityName = BuildOriginAbilityName(direction, isSpell, seed);
        result.classDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            "class",
            result.className,
            "A first class generated from the player's repeated answers before the road assigns any regional label.",
            "origin",
            false,
            stimulus);
        result.titleDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            "title",
            result.title,
            "A starting title granted because the same instinct appeared under several different questions.",
            "origin",
            false,
            stimulus);
        result.abilityDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            isSpell ? "spell" : "skill",
            result.abilityName,
            BuildOriginAbilityDescription(direction, isSpell),
            "origin",
            isSpell,
            stimulus);
        result.questName = BuildOriginQuestName(direction, seed);
        result.questDescription = YQGeneratedContentCuration.CuratePlayerFacingDescription(
            state,
            "quest",
            result.questName,
            "Speak with Archivist Vey, equip the gear that manifested from your answers, and prove one first action that matches your chosen life direction.",
            "origin",
            false,
            stimulus);
        result.questTags = YQGeneratedContentCuration.BuildPlayerResponseTags(
            result.identityKeywords != null ? result.identityKeywords.ToArray() : new[] { "origin" },
            "origin",
            result.isSpell,
            result.questName + " " + result.questDescription + " " + stimulus);
        result.questObjectives = BuildDefaultOriginObjectives();
        result.targetingMode = result.isSpell ? "projectile" : "melee";
        result.resourceType = result.isSpell ? "mana" : "stamina";
        result.resourceCost = result.isSpell ? 15 : 10;
        result.cooldownSeconds = result.isSpell ? 0.65f : 0.35f;
        result.vfxFamily = result.isSpell ? "arcane" : "physical";
        result.animationIntent = result.isSpell ? "cast" : "melee";
        ApplyStatDistribution(scores, direction, result);
        return result;
    }

    private static string BuildCharacterCreationText(PlayerState state)
    {
        if (state == null)
            return string.Empty;

        return
            SafeString(state.displayName, string.Empty) + " " +
            SafeString(state.characterPronouns, string.Empty) + " " +
            SafeString(state.characterBodyFrame, string.Empty) + " " +
            SafeString(state.characterLifeDirection, string.Empty) + " " +
            SafeString(state.characterVow, string.Empty) + " " +
            SafeString(state.characterAppearanceSummary, string.Empty);
    }

    private static void AddCharacterCreationKeywords(List<string> keywords, PlayerState state)
    {
        if (keywords == null || state == null)
            return;

        EnsureIdentityKeyword(keywords, "character_created");
        if (!string.IsNullOrWhiteSpace(state.characterLifeDirection))
            EnsureIdentityKeyword(keywords, NormalizeKey(state.characterLifeDirection));
        if (!string.IsNullOrWhiteSpace(state.characterBodyFrame))
            EnsureIdentityKeyword(keywords, NormalizeKey(state.characterBodyFrame));
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }
        return new string(chars).Trim('_');
    }

    private static void ScoreBucket(Dictionary<string, int> scores, string bucket, string text, int weight, params string[] terms)
    {
        if (scores == null || string.IsNullOrWhiteSpace(bucket))
            return;

        int score = 0;
        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(terms[i]) && text.Contains(terms[i]))
                score += Mathf.Max(1, weight);
        }

        if (score <= 0)
            return;

        scores.TryGetValue(bucket, out int current);
        scores[bucket] = current + score;
    }

    private static string ResolveOriginDirection(Dictionary<string, int> scores, string text)
    {
        if (ContainsAny(text, "demonlord", "demon lord", "overlord"))
            return "demonlord";
        if (ContainsAny(text, "merchant", "shopkeeper", "trader"))
            return "merchant";
        if (ContainsAny(text, "lumberjack", "woodcutter"))
            return "lumberjack";

        string[] priority =
        {
            "shadow", "patience", "trade", "craft", "guard", "magic", "martial", "mobility", "curiosity", "survival"
        };

        string best = "wayfinder";
        int bestScore = 0;
        for (int i = 0; i < priority.Length; i++)
        {
            string key = priority[i];
            int value = GetScore(scores, key);
            if (value <= bestScore)
                continue;

            bestScore = value;
            switch (key)
            {
                case "shadow": best = "demonlord"; break;
                case "patience": best = "stillness"; break;
                case "trade": best = "merchant"; break;
                case "craft": best = "lumberjack"; break;
                case "guard": best = "warden"; break;
                case "magic": best = "arcanist"; break;
                case "martial": best = "hero"; break;
                case "mobility": best = "wanderer"; break;
                default: best = "wayfinder"; break;
            }
        }

        return bestScore <= 0 ? "wayfinder" : best;
    }

    private static bool DirectionPrefersSpell(string direction, Dictionary<string, int> scores)
    {
        if (string.Equals(direction, "arcanist", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(direction, "demonlord", StringComparison.OrdinalIgnoreCase))
            return true;

        return GetScore(scores, "magic") > Mathf.Max(GetScore(scores, "martial"), GetScore(scores, "craft"));
    }

    private static List<string> BuildIdentityKeywords(string direction, Dictionary<string, int> scores, string text)
    {
        List<string> keywords = new List<string> { "origin_generated", "goddess_read", direction };
        if (GetScore(scores, "patience") > 0)
            keywords.Add("stillness_response");
        if (GetScore(scores, "guard") > 0)
            keywords.Add("protective_response");
        if (GetScore(scores, "trade") > 0)
            keywords.Add("risk_value_response");
        if (GetScore(scores, "craft") > 0)
            keywords.Add("tool_work_response");
        if (GetScore(scores, "magic") > 0)
            keywords.Add("mana_response");
        if (ContainsAny(text, "tree", "forest", "root", "leaf", "nature", "wood"))
        {
            keywords.Add("nature_precursor");
            keywords.Add("auralith");
        }

        return keywords;
    }

    private static string BuildOriginClassName(string direction, string seed)
    {
        return Pick(OriginClassPrefixes(direction), "Threshold", seed, 17) + " " +
               Pick(OriginClassNouns(direction), "Wayfarer", seed, 23);
    }

    private static string BuildOriginTitle(string direction, string seed)
    {
        return Pick(OriginTitlePrefixes(direction), "First", seed, 31) + " " +
               Pick(OriginTitleNouns(direction), "Answer", seed, 37);
    }

    private static string BuildOriginAbilityName(string direction, bool isSpell, string seed)
    {
        string action = Pick(isSpell ? OriginSpellVerbs(direction) : OriginSkillVerbs(direction), isSpell ? "Invoke" : "Answer", seed, 41);
        string aspect = Pick(OriginAspects(direction), "First Pressure", seed, 43);
        return action + " " + aspect;
    }

    private static string BuildOriginQuestName(string direction, string seed)
    {
        return Pick(new[] { "Prove", "Awaken", "Temper", "Name", "Measure" }, "Prove", seed, 47) + " the " +
               Pick(OriginAspects(direction), "First Pressure", seed, 53);
    }

    private static string BuildOriginAbilityDescription(string direction, bool isSpell)
    {
        string normalized = direction ?? "wayfinder";
        if (normalized == "lumberjack")
            return "A player-shaped pattern drawn through tool grip, rough timber, and Auralith's older memory of living terrain.";
        if (normalized == "merchant")
            return "A player-shaped pattern that reads value, danger, leverage, and exit routes before committing.";
        if (normalized == "demonlord")
            return "A player-shaped pattern that turns ambition into controlled pressure instead of random spectacle.";
        if (normalized == "stillness")
            return "A player-shaped pattern that rewards patient refusal, waiting, and becoming harder to move.";
        if (normalized == "warden")
            return "A player-shaped pattern that converts protection, mercy, and guarded timing into repeatable defense.";
        if (normalized == "arcanist" || isSpell)
            return "A player-shaped spell keyed to the first pressure the goddess heard repeating in your answers.";
        return "A player-shaped technique keyed to the first pressure the goddess heard repeating in your answers.";
    }

    private static string ResolveOriginStimulus(string direction, string text)
    {
        if (string.Equals(direction, "stillness", StringComparison.OrdinalIgnoreCase))
            return "waiting so deliberately the world has to respond";
        if (string.Equals(direction, "merchant", StringComparison.OrdinalIgnoreCase))
            return "measuring risk, value, and leverage before acting";
        if (string.Equals(direction, "lumberjack", StringComparison.OrdinalIgnoreCase))
            return "turning honest tool work and the old green into survival";
        if (string.Equals(direction, "demonlord", StringComparison.OrdinalIgnoreCase))
            return "wanting command without letting power become noise";
        if (string.Equals(direction, "warden", StringComparison.OrdinalIgnoreCase))
            return "standing between pressure and someone weaker";
        if (string.Equals(direction, "arcanist", StringComparison.OrdinalIgnoreCase))
            return "answering fear with shaped mana";
        if (string.Equals(direction, "hero", StringComparison.OrdinalIgnoreCase))
            return "meeting danger directly enough that hesitation has to move";
        if (ContainsAny(text, "locked", "chest", "door", "secret"))
            return "following a hidden answer before the road explains itself";
        return "letting repeated choices name the player before a region does";
    }

    private static void ApplyStatDistribution(Dictionary<string, int> scores, string direction, OriginResult result)
    {
        int guard = GetScore(scores, "guard");
        int survival = GetScore(scores, "survival");
        int martial = GetScore(scores, "martial");
        int craft = GetScore(scores, "craft");
        int trade = GetScore(scores, "trade");
        int magic = GetScore(scores, "magic");
        int mobility = GetScore(scores, "mobility");
        int patience = GetScore(scores, "patience");

        result.vitality = 10 + Mathf.Clamp(survival + guard + patience, 0, 8);
        result.strength = 10 + Mathf.Clamp(martial + craft, 0, 8);
        result.dexterity = 10 + Mathf.Clamp(mobility + trade, 0, 8);
        result.intelligence = 10 + Mathf.Clamp(magic + trade + GetScore(scores, "curiosity"), 0, 8);

        if (direction == "demonlord" || direction == "arcanist")
            result.intelligence += 2;
        if (direction == "lumberjack")
            result.strength += 2;
        if (direction == "warden" || direction == "stillness")
            result.vitality += 2;
        if (direction == "merchant" || direction == "wanderer")
            result.dexterity += 2;
    }

    private static void ApplyOriginStats(PlayerState state, OriginResult result)
    {
        if (state == null || result == null)
            return;

        state.stats ??= new StatBlock();
        state.stats.vitality = Mathf.Clamp(result.vitality, 8, 22);
        state.stats.strength = Mathf.Clamp(result.strength, 8, 22);
        state.stats.dexterity = Mathf.Clamp(result.dexterity, 8, 22);
        state.stats.intelligence = Mathf.Clamp(result.intelligence, 8, 22);
        state.stats.maxHealth = 70 + state.stats.vitality * 7;
        state.stats.maxStamina = 72 + state.stats.dexterity * 5 + state.stats.strength * 2;
        state.stats.maxMana = 24 + state.stats.intelligence * 5;
        state.stats.attack = 5 + state.stats.strength + state.stats.dexterity / 2;
        state.stats.defense = 3 + state.stats.vitality / 2 + state.stats.strength / 4;
        state.stats.critChance = Mathf.Clamp(0.035f + state.stats.dexterity * 0.0035f, 0.04f, 0.16f);
        state.stats.moveSpeed = Mathf.Clamp(5.65f + state.stats.dexterity * 0.055f, 5.8f, 7.15f);
    }

    private static int GetScore(Dictionary<string, int> scores, string key)
    {
        if (scores == null || string.IsNullOrWhiteSpace(key))
            return 0;
        return scores.TryGetValue(key, out int value) ? value : 0;
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        if (string.IsNullOrWhiteSpace(text) || terms == null)
            return false;
        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(terms[i]) && text.Contains(terms[i].ToLowerInvariant()))
                return true;
        }
        return false;
    }

    private static string Pick(string[] options, string fallback, string seed, int salt)
    {
        if (options == null || options.Length == 0)
            return fallback;
        int index = Mathf.Abs(StableHash((seed ?? string.Empty) + ":" + salt)) % options.Length;
        string picked = options[index];
        return string.IsNullOrWhiteSpace(picked) ? fallback : picked.Trim();
    }

    private static string SafeString(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 23;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
                hash = hash * 31 + text[i];
            return hash;
        }
    }

    private static Vector3 ResolveManifestPosition()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            return player.transform.position + player.transform.forward * 1.15f + Vector3.up * 1.15f;
        return new Vector3(0f, 1.2f, -1.15f);
    }

    private static string[] OriginClassPrefixes(string direction)
    {
        switch (direction)
        {
            case "merchant": return new[] { "Keenhand", "Roadledger", "Riskwise" };
            case "lumberjack": return new[] { "First-Green", "Auralith", "Rootwake" };
            case "demonlord": return new[] { "Black-Crown", "Sovereign", "Abyssal" };
            case "arcanist": return new[] { "Threshold", "Runebound", "Star-Read" };
            case "warden": return new[] { "Oathbound", "Mercyguard", "Shieldwake" };
            case "stillness": return new[] { "Stone-Quiet", "Stillwake", "Unmoving" };
            case "hero": return new[] { "Linebreaker", "Dawnsworn", "First-Risk" };
            case "wanderer": return new[] { "Farstep", "Wayworn", "Roadsalt" };
            default: return new[] { "Threshold", "Wayfinder", "First-Risk" };
        }
    }

    private static string[] OriginClassNouns(string direction)
    {
        switch (direction)
        {
            case "merchant": return new[] { "Broker", "Peddler", "Factor" };
            case "lumberjack": return new[] { "Woodcutter", "Greenhand", "Timber-Sworn" };
            case "demonlord": return new[] { "Heir", "Invoker", "Pretender" };
            case "arcanist": return new[] { "Adept", "Caller", "Reader" };
            case "warden": return new[] { "Sentinel", "Bulwark", "Vowkeeper" };
            case "stillness": return new[] { "Anchor", "Watcher", "Monolith" };
            case "hero": return new[] { "Aspirant", "Striker", "Vanguard" };
            case "wanderer": return new[] { "Wayfarer", "Scout", "Drifter" };
            default: return new[] { "Wayfarer", "Aspirant", "Witness" };
        }
    }

    private static string[] OriginTitlePrefixes(string direction)
    {
        switch (direction)
        {
            case "merchant": return new[] { "Ledger", "Keen", "Coin-Wise" };
            case "lumberjack": return new[] { "Root", "Green", "Timber" };
            case "demonlord": return new[] { "Crownless", "Abyss", "Black" };
            case "arcanist": return new[] { "Runic", "Star", "Threshold" };
            case "warden": return new[] { "Oath", "Mercy", "Shield" };
            case "stillness": return new[] { "Still", "Stone", "Unmoved" };
            case "hero": return new[] { "Unbowed", "Dawn", "First" };
            case "wanderer": return new[] { "Far", "Road", "Wayward" };
            default: return new[] { "First", "Named", "Threshold" };
        }
    }

    private static string[] OriginTitleNouns(string direction)
    {
        switch (direction)
        {
            case "merchant": return new[] { "Hand", "Eye", "Bargain" };
            case "lumberjack": return new[] { "Axe", "Witness", "Sapling" };
            case "demonlord": return new[] { "Spark", "Heir", "Vow" };
            case "arcanist": return new[] { "Spark", "Initiate", "Sign" };
            case "warden": return new[] { "Vow", "Wall", "Hand" };
            case "stillness": return new[] { "Watcher", "Anchor", "Stone" };
            case "hero": return new[] { "Fist", "Step", "Blade" };
            case "wanderer": return new[] { "Boot", "Compass", "Road" };
            default: return new[] { "Answer", "Witness", "Name" };
        }
    }

    private static string[] OriginAspects(string direction)
    {
        switch (direction)
        {
            case "merchant": return new[] { "Measured Risk", "Hidden Price", "Honest Debt" };
            case "lumberjack": return new[] { "First Green", "Auralith Root", "Tool-Oath" };
            case "demonlord": return new[] { "Crownless Pressure", "Abyssal Vow", "Quiet Dominion" };
            case "arcanist": return new[] { "Threshold Rune", "Shaped Mana", "Starward Sign" };
            case "warden": return new[] { "Mercy Wall", "Guarded Breath", "Shield Vow" };
            case "stillness": return new[] { "Unmoving Hour", "Stone Patience", "Waiting Godseed" };
            case "hero": return new[] { "First Strike", "Dawn Pressure", "Unbowed Step" };
            case "wanderer": return new[] { "Far Road", "Unwritten Path", "Leaving Step" };
            default: return new[] { "First Pressure", "Threshold Answer", "Unwritten Name" };
        }
    }

    private static string[] OriginSpellVerbs(string direction)
    {
        switch (direction)
        {
            case "demonlord": return new[] { "Bind", "Invoke", "Crown" };
            case "arcanist": return new[] { "Shape", "Invoke", "Read" };
            case "stillness": return new[] { "Still", "Root", "Hold" };
            case "lumberjack": return new[] { "Wake", "Root", "Call" };
            default: return new[] { "Invoke", "Shape", "Cast" };
        }
    }

    private static string[] OriginSkillVerbs(string direction)
    {
        switch (direction)
        {
            case "merchant": return new[] { "Appraise", "Leverage", "Read" };
            case "lumberjack": return new[] { "Cleave", "Brace", "Hew" };
            case "warden": return new[] { "Guard", "Interpose", "Brace" };
            case "stillness": return new[] { "Endure", "Anchor", "Refuse" };
            case "hero": return new[] { "Break", "Answer", "Drive" };
            case "wanderer": return new[] { "Step", "Slip", "Cross" };
            default: return new[] { "Answer", "Read", "Press" };
        }
    }

    private static QuestRecord FindQuestByName(PlayerState state, string questName)
    {
        if (state == null || state.quests == null || string.IsNullOrWhiteSpace(questName))
            return null;

        for (int i = state.quests.Count - 1; i >= 0; i--)
        {
            QuestRecord quest = state.quests[i];
            if (quest != null && string.Equals(quest.name, questName, StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    private void Close()
    {
        _startupPhaseResolved = true;
        SetVisible(false);
        RuntimeModalUiBlocker.SetMenuOpen(false);
    }

    private void BuildUi()
    {
        if (_canvas != null)
            return;

        GameObject canvasGo = new GameObject("YQOriginQuestionnaireCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        _canvas = canvasGo.GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 6200;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        // note: The origin questionnaire uses the same reference resolution as the rest of the runtime UI.
        YQUITheme.ApplyCanvasScaler(scaler);

        /*
         * The Goddess questionnaire is a pre-world presentation.
         *
         * Grade the authored Goddess threshold while leaving its statue and
         * upward camera transition visible around the questionnaire panel.
         */
        GameObject blackoutGo = new GameObject(
            "Goddess_Blackout",
            typeof(RectTransform),
            typeof(Image));

        blackoutGo.transform.SetParent(
            canvasGo.transform,
            false);

        RectTransform blackout =
            blackoutGo.GetComponent<RectTransform>();

        blackout.anchorMin = Vector2.zero;
        blackout.anchorMax = Vector2.one;
        blackout.pivot = new Vector2(0.5f, 0.5f);
        blackout.offsetMin = Vector2.zero;
        blackout.offsetMax = Vector2.zero;

        Image blackoutImage =
            blackoutGo.GetComponent<Image>();

        // note: Keep the Goddess and threshold scene clearly visible behind the lower-screen data console.
        blackoutImage.color = new Color(0.002f, 0.008f, 0.018f, 0.08f);
        blackoutImage.raycastTarget = true;

        _panel = CreatePanel(
            canvasGo.transform,
            "Panel",
            new Vector2(0.5f, 0.5f),
            new Vector2(760f, 520f),
            Vector2.zero,
            YQUITheme.PanelSolid);

        // note: The questionnaire reads as a restrained lower-screen conversation rail while the Goddess remains the visual subject.
        _panel.anchorMin = new Vector2(0.025f, 0.025f);
        _panel.anchorMax = new Vector2(0.975f, 0.520f);
        _panel.pivot = new Vector2(0.5f, 0.5f);
        _panel.offsetMin = Vector2.zero;
        _panel.offsetMax = Vector2.zero;
        YQUITheme.ApplyPanel(_panel.GetComponent<Image>());
        _panel.GetComponent<Image>().color = new Color(0.002f, 0.012f, 0.028f, 0.36f);
        _panelCanvasGroup = _panel.gameObject.AddComponent<CanvasGroup>();

        _title = CreateText(_panel, "Title", 32f, FontStyles.Bold, TextAlignmentOptions.TopLeft, new Vector2(48f, -24f), new Vector2(704f, 48f));
        // note: Larger pearl typography stays legible after reference-resolution scaling on 720p and ultrawide displays.
        _title.rectTransform.anchorMax = new Vector2(1f, 1f);
        _title.rectTransform.sizeDelta = new Vector2(-96f, 48f);
        _title.color = YQUITheme.Gold;
        _body = CreateText(_panel, "Body", 21f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(48f, -78f), new Vector2(704f, 92f));
        _body.rectTransform.anchorMax = new Vector2(1f, 1f);
        _body.rectTransform.sizeDelta = new Vector2(-96f, 92f);
        _body.textWrappingMode = TextWrappingModes.Normal;

        _input = CreateInput(_panel, new Vector2(48f, -182f), new Vector2(704f, 76f));
        _input.GetComponent<RectTransform>().anchorMax = new Vector2(1f, 1f);
        _input.GetComponent<RectTransform>().sizeDelta = new Vector2(-96f, 76f);
        // note: MultiLineSubmit routes Enter through TMP's UI event system without adding a polling Update loop.
        _input.onSubmit.AddListener(_ => SubmitAnswer());
        _nextButton = CreateButton(_panel, "Next", new Vector2(1f, 0f), new Vector2(-48f, 24f), new Vector2(250f, 58f), "Submit  [Enter]", SubmitAnswer);
    }

    private void SetVisible(bool value)
    {
        if (_canvas == null)
            return;

        if (_panelRevealRoutine != null)
        {
            StopCoroutine(_panelRevealRoutine);
            _panelRevealRoutine = null;
        }

        _canvas.enabled = value;
        if (!value || _panelCanvasGroup == null)
            return;

        // note: The lower conversation rail settles in on unscaled time because the startup modal intentionally pauses gameplay.
        _panelRevealRoutine = StartCoroutine(RevealPanelRoutine());
    }

    private IEnumerator RevealPanelRoutine()
    {
        const float duration = 0.40f;
        float startedAt = Time.unscaledTime;
        Vector2 destination = Vector2.zero;
        Vector2 origin = destination + Vector2.down * 18f;
        _panelCanvasGroup.alpha = 0f;
        _panelCanvasGroup.interactable = false;
        _panelCanvasGroup.blocksRaycasts = false;

        while (Time.unscaledTime - startedAt < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - startedAt) / duration));
            _panelCanvasGroup.alpha = t;
            _panel.anchoredPosition = Vector2.LerpUnclamped(origin, destination, t);
            yield return null;
        }

        _panel.anchoredPosition = destination;
        _panelCanvasGroup.alpha = 1f;
        _panelCanvasGroup.interactable = true;
        _panelCanvasGroup.blocksRaycasts = true;
        _panelRevealRoutine = null;
    }

    private void ClearModeButtons()
    {
        _modeButtonCount = 0;
        for (int i = _panel.childCount - 1; i >= 0; i--)
        {
            Transform child = _panel.GetChild(i);
            if (child != null && child.name.StartsWith("Mode_", StringComparison.OrdinalIgnoreCase))
                Destroy(child.gameObject);
        }
    }

    private void CreateModeButton(string label, string subtitle, int count)
    {
        int index = _modeButtonCount++;
        Button button = CreateButton(_panel, "Mode_" + label.Replace(" ", ""), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(0f, 82f), label + "\n" + subtitle, () => BeginQuestions(label, count));
        RectTransform rect = button.GetComponent<RectTransform>();
        float left = 0.115f + index * 0.26f;
        // note: Each reading depth occupies a responsive panel column so ultrawide and narrow displays retain equal visual weight without overlap.
        rect.anchorMin = new Vector2(left, 0f);
        rect.anchorMax = new Vector2(left + 0.25f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 30f);
        rect.sizeDelta = new Vector2(0f, 82f);
        TMP_Text text = button.GetComponentInChildren<TMP_Text>();
        if (text != null)
            text.fontSize = 22f;
    }

    private static RectTransform CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 size, Vector2 anchoredPosition, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPosition;
        // note: Panels are positioned here while theme helpers handle the repeated visual styling.
        go.GetComponent<Image>().color = color;
        return rt;
    }

    private static TMP_Text CreateText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions alignment, Vector2 position, Vector2 dimensions)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        YQUITheme.ApplyText(text);
        return text;
    }

    private static TMP_InputField CreateInput(Transform parent, Vector2 position, Vector2 dimensions)
    {
        GameObject root = new GameObject("AnswerInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
        RectTransform rt = root.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = position;
        rt.sizeDelta = dimensions;
        // note: The written-answer box gets a framed, quieter surface than the main modal.
        YQUITheme.ApplySoftPanel(root.GetComponent<Image>());
        YQUITheme.AddFrame(root);

        TMP_Text text = CreateText(root.transform, "Text", 18f, FontStyles.Normal, TextAlignmentOptions.TopLeft, new Vector2(14f, -12f), new Vector2(dimensions.x - 28f, dimensions.y - 24f));
        TMP_Text placeholder = CreateText(root.transform, "Placeholder", 18f, FontStyles.Italic, TextAlignmentOptions.TopLeft, new Vector2(14f, -12f), new Vector2(dimensions.x - 28f, dimensions.y - 24f));
        placeholder.text = "Answer in your own words...";
        placeholder.color = YQUITheme.Muted;

        // note: Input copy follows the responsive width of the lower-screen questionnaire console.
        StretchInputText(text.rectTransform);
        StretchInputText(placeholder.rectTransform);

        TMP_InputField input = root.GetComponent<TMP_InputField>();
        input.textComponent = text as TextMeshProUGUI;
        input.placeholder = placeholder;
        // note: Enter submits the current answer; normal text wrapping still permits long natural-language responses.
        input.lineType = TMP_InputField.LineType.MultiLineSubmit;
        input.characterLimit = 800;
        return input;
    }

    private static void StretchInputText(RectTransform rect)
    {
        if (rect == null)
            return;

        // note: Preserve a readable inset while allowing the field to span any supported aspect ratio.
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = new Vector2(14f, 12f);
        rect.offsetMax = new Vector2(-14f, -12f);
    }

    private static Button CreateButton(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
        Button button = go.GetComponent<Button>();
        button.onClick.AddListener(onClick);

        TMP_Text text = CreateText(go.transform, "Label", 20f, FontStyles.Bold, TextAlignmentOptions.Center, Vector2.zero, size);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        text.rectTransform.offsetMin = Vector2.zero;
        text.rectTransform.offsetMax = Vector2.zero;
        text.text = label;
        // note: Button theming runs after label creation so TMP colors and frame accents are applied together.
        YQUITheme.ApplyButton(button);
        return button;
    }

    private sealed class OriginResult
    {
        public string directionKey;
        public string seed;
        public string generationSource;
        public string generationPayloadJson;
        public YQOriginGeneratedItemDto[] loadoutHints;
        public string stimulus;
        public string className;
        public string classDescription;
        public string title;
        public string titleDescription;
        public string abilityName;
        public string abilityDescription;
        public bool isSpell;
        public string questName;
        public string questDescription;
        public string[] questTags;
        public List<QuestObjectiveRecord> questObjectives;
        public List<string> identityKeywords;
        public int vitality;
        public int strength;
        public int dexterity;
        public int intelligence;
        public string targetingMode;
        public string resourceType;
        public int resourceCost;
        public float cooldownSeconds;
        public string vfxFamily;
        public string animationIntent;
    }
}
