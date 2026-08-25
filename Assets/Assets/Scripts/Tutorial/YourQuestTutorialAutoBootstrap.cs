// Assets/Assets/Scripts/Tutorial/YourQuestTutorialAutoBootstrap.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public sealed class YourQuestTutorialAutoBootstrap : MonoBehaviour
{
    public static bool GameplayRuntimeReady { get; private set; }
    public static bool GameplayPresentationReleased { get; private set; }

    private static bool s_created;
    private static readonly object StartupBlockToken = new object();
    private const string TreePrefabA = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Tree.prefab";
    private const string TreePrefabB = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_TreeNeedles01.prefab";
    private const string TreePrefabC = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Tree_03.prefab";
    private const string BushPrefab = "Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab";
    private const string GrassPrefab = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Grass04.prefab";
    private const string ChestPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestSimpleSmall.prefab";
    private const string OrnateChestPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestOrnateMedium.prefab";
    private const string MimicPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleSmall.prefab";
    private const string SpiderPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Spiders/_Prefabs/Spider 1.prefab";
    private const string PlantMonsterPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Plant Monster/_Prefabs/PlantMonster.prefab";
    private const string MushroomMonsterPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mushroom Monster/_Prefabs/Mushroom_v2.prefab";
    private const string DragonPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Dragons/_Prefabs/Dragon.prefab";
    private const string DemonPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Demons/_Prefabs/Demons.prefab";
    private const string HumanMalePrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4.1.1).prefab";
    private const string AmbientWindAudio = "";
    private const string AmbientHumAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Generic/Humming & Pulsing/Humming_Loop_4_S.wav";
    private const string FireLoopAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Fire/Loop/Fire_Loop_Small_S.wav";
    private const string RuntimeWorldVersionMarker = "YQ_World_FinishedTutorial_v9";
    private const string TutorialProgressCounter = "tutorial:finished_level:v2";
    private static readonly Dictionary<string, AudioClip> s_audioClipCache = new Dictionary<string, AudioClip>(StringComparer.OrdinalIgnoreCase);
    private bool _bootStarted;

    public static void RestartAfterGenerationFailure()
    {
        YourQuestTutorialAutoBootstrap bootstrap =
            FindFirstObjectByType<YourQuestTutorialAutoBootstrap>();

        if (bootstrap == null)
            return;

        // note: Returning from a failed generation attempt restarts the one authoritative startup coroutine instead of opening a title screen with no owner waiting for its result.
        bootstrap.StopAllCoroutines();
        RuntimeModalUiBlocker.Release(StartupBlockToken);
        GameplayRuntimeReady = false;
        GameplayPresentationReleased = false;
        bootstrap.StartCoroutine(bootstrap.BootstrapRoutine());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeStatics()
    {
        // note: Enter Play Mode options may skip domain reload; reset startup authority so every session creates one fresh production bootstrap.
        s_created = false;
        GameplayRuntimeReady = false;
        GameplayPresentationReleased = false;
        s_audioClipCache.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateBootstrap()
    {
        if (s_created)
            return;

        GameObject root = new GameObject("__YQ_InvestorBootstrap");
        DontDestroyOnLoad(root);
        root.AddComponent<YourQuestTutorialAutoBootstrap>();
        s_created = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void QuarantineLegacySceneContentBeforePresentation()
    {
        // note: Disable obsolete test roots before the first rendered frame so an accidentally opened prototype scene can never flash behind production startup.
        if (s_created)
        {
            YourQuestTutorialAutoBootstrap bootstrap =
                FindFirstObjectByType<YourQuestTutorialAutoBootstrap>();
            if (bootstrap != null)
                bootstrap.CleanupLegacyObjects();
        }
    }

    private void Awake()
    {
        if (_bootStarted)
            return;

        _bootStarted = true;
        GameplayRuntimeReady = false;
        GameplayPresentationReleased = false;
        YQTitleScreenUI.PrepareStartupGate();
        EnsureStartupServices();
        RuntimeModalUiBlocker.Acquire(StartupBlockToken);
        StartCoroutine(BootstrapRoutine());
    }

    private IEnumerator BootstrapRoutine()
    {
        // note: Production startup presents the title stage first; no terrain, player, tutorial world, gameplay HUD, or LLM coordinator is constructed behind it.
        yield return null;

        YQTitleScreenUI titleScreen = FindFirstObjectByType<YQTitleScreenUI>();
        if (titleScreen != null)
            titleScreen.OpenAtStartup();
        yield return null;

        RuntimeModalUiBlocker.Release(StartupBlockToken);

        while (!YQTitleScreenUI.StartupFlowComplete)
            yield return null;

        EnsureOriginPhaseServices();
        YQOriginQuestionnaireUI origin =
            EnsureSingleton<YQOriginQuestionnaireUI>("YQOriginQuestionnaireUI");
        bool originOpened = origin != null && origin.OpenIfNeededAfterTitle();
        if (originOpened)
        {
            // note: The playable world remains nonexistent while the player answers beneath the authored Goddess threshold camera.
            while (origin != null && !origin.StartupPhaseResolved)
                yield return null;
        }

        YQTitleEnvironmentLoader.Hide();

        // note: Save confirmation transitions directly into a blocked loading phase; gameplay input never gets a stray frame before the player exists.
        RuntimeModalUiBlocker.Acquire(StartupBlockToken);
        bool ownsOrdinaryLoading = !YQStartupLoadingScreen.IsGenerationVisible;
        YQStartupLoadingScreen loading = ownsOrdinaryLoading
            ? YQStartupLoadingScreen.Show("YourQuest", "Preparing selected save")
            : YQStartupLoadingScreen.Current;

        if (ownsOrdinaryLoading && loading != null)
            loading.SetStage("Starting gameplay services", 0.12f);
        EnsureGameplayServices();
        yield return null;

        if (ownsOrdinaryLoading && loading != null)
            loading.SetStage("Loading the playable world", 0.32f);
        EnsureRuntimeWorld();
        yield return null;

        if (ownsOrdinaryLoading && loading != null)
            loading.SetStage("Preparing gameplay interface", 0.58f);
        EnsureGameplayPresentationServices();
        EnsureRuntimeUi();
        ForceCloseStartupUi();
        RuntimeModalUiBlocker.Acquire(StartupBlockToken);
        yield return null;

        if (ownsOrdinaryLoading && loading != null)
            loading.SetStage("Seeding selected-save progression", 0.74f);
        SeedData();
        yield return null;

        if (ownsOrdinaryLoading && loading != null)
            loading.SetStage("Linking the authoritative player", 0.84f);
        WireReferences();
        yield return null;

        /*
         * The reviewed generated-world compiler is now the only production
         * world source. Allow it to run while the startup modal remains in
         * place, then reveal only after its accepted save has materialized.
         */
        GameplayRuntimeReady = true;
        YQGeneratedWorldRuntimeBuilder worldBuilder =
            YQGeneratedWorldRuntimeBuilder.Instance != null
                ? YQGeneratedWorldRuntimeBuilder.Instance
                : EnsureSingleton<YQGeneratedWorldRuntimeBuilder>(
                    "YQGeneratedWorldRuntimeBuilder");
        worldBuilder.BuildGeneratedWorld();

        if (ownsOrdinaryLoading && loading != null)
            loading.SetStage("Materializing the authored world", 0.92f);

        while (worldBuilder == null ||
               !worldBuilder.HasMaterializedCurrentWorld)
        {
            if (worldBuilder == null)
            {
                worldBuilder = YQGeneratedWorldRuntimeBuilder.Instance != null
                    ? YQGeneratedWorldRuntimeBuilder.Instance
                    : EnsureSingleton<YQGeneratedWorldRuntimeBuilder>(
                        "YQGeneratedWorldRuntimeBuilder");
            }

            // note: The world builder owns the single bounded startup watchdog and its interactive recovery UI; bootstrap remains dormant until Retry or Return resolves that state.
            if (worldBuilder != null &&
                worldBuilder.InitialGenerationRecoveryRequired)
            {
                yield return null;
                continue;
            }

            yield return null;
        }

        // note: Materialization and the canonical initial-generation release are separate gates; neither gate is bypassed by elapsed wall-clock time.
        while (YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
        {
            yield return null;
        }

        if (ownsOrdinaryLoading && loading != null &&
            !YQStartupLoadingScreen.IsGenerationVisible)
            yield return loading.FinishAndHide();

        while (YQStartupLoadingScreen.IsVisible)
        {
            // note: The player remains startup-blocked until the Goddess camera and its closing generation line have completely handed presentation back to gameplay.
            yield return null;
        }

        RuntimeModalUiBlocker.Release(StartupBlockToken);
        // note: This is the single presentation-release edge consumed by gameplay HUDs; service readiness alone is intentionally insufficient.
        GameplayPresentationReleased = true;
    }

    private void EnsureStartupServices()
    {
        // note: The title phase owns only save discovery, core persisted state, input UI, and modal coordination.
        EnsureEventSystem();
        EnsureSingleton<PlayerStateManager>("PlayerStateManager");
        EnsureSingleton<WorldStateManager>("WorldStateManager");
        EnsureSingleton<RuntimeModalUiBlocker>("RuntimeModalUiBlocker");
        EnsureSingleton<YQProfileSaveSystem>("YQProfileSaveSystem");
        EnsureSingleton<YQTitleScreenUI>("YQTitleScreenUI");
    }

    private void EnsureGameplayServices()
    {
        EnsureEventSystem();
        EnsureSingleton<PlayerStateManager>("PlayerStateManager");
        EnsureSingleton<WorldStateManager>("WorldStateManager");
        EnsureSingleton<ActionRegistry>("ActionRegistry");
        EnsureSingleton<EventAccumulator>("EventAccumulator");
        EnsureSingleton<PlayerContext>("PlayerContext");
        EnsureSingleton<RuntimeModalUiBlocker>("RuntimeModalUiBlocker");

        LLMClient llm = EnsureSingleton<LLMClient>("LLMClient");
        // note: Use the balanced local instruct model unless the scene already names a specific installed model.
        llm.model = string.IsNullOrWhiteSpace(llm.model) ? "llama3.1" : llm.model;

        SituationSnapshotBuilder snapshot = EnsureSingleton<SituationSnapshotBuilder>("SituationSnapshotBuilder");

        DialogueThinkService dialogue = EnsureSingleton<DialogueThinkService>("DialogueThinkService");
        dialogue.situationSnapshotBuilder = snapshot;

        WorldDeltaApplier worldDeltaApplier = EnsureSingleton<WorldDeltaApplier>("WorldDeltaApplier");
        worldDeltaApplier.worldStateManager = WorldStateManager.Instance;

        ProgressionDecisionApplier progression = EnsureSingleton<ProgressionDecisionApplier>("ProgressionDecisionApplier");
        progression.snapshotBuilder = snapshot;
        progression.minConfidence = 0.76f;
        progression.minSkillConfidence = 0.80f;
        progression.minTitleConfidence = 0.80f;
        progression.minClassConfidence = 0.82f;
        progression.minQuestConfidence = 0.82f;
        progression.gateSkillsToCalmLowThreat = false;
        progression.requirePlayerEvidenceForSkills = true;
        progression.requirePlayerEvidenceForQuests = true;
        progression.minSkillEvidenceScore = 0.34f;
        progression.minQuestEvidenceScore = 0.30f;

        LLMThinkCycle worldThink = EnsureSingleton<LLMThinkCycle>("LLMThinkCycle");
        worldThink.worldDeltaApplier = worldDeltaApplier;
        worldThink.situationSnapshotBuilder = snapshot;
        worldThink.thinkEverySeconds = 18f;
        worldThink.minTotalSignificance = 5f;
        worldThink.requireIdleLLM = true;
        worldThink.retrySoonWhenBusy = true;
        worldThink.busyRetryDelay = 0.35f;
        // note: Release play records bounded request summaries, not full generated world-delta payloads.
        worldThink.logRawResponse = false;

        ProgressionThinkCycle progressionThink = EnsureSingleton<ProgressionThinkCycle>("ProgressionThinkCycle");
        progressionThink.applier = progression;
        // note: Runtime offers must use the local model before any optional deterministic emergency path.
        progressionThink.preferDeterministicProgression = false;
        progressionThink.allowLlmProgressionFallback = true;
        if (progressionThink.balance == null)
        {
            progressionThink.balance = Resources.Load<ProgressionBalanceConfig>("ProgressionBalanceConfig");
            if (progressionThink.balance == null)
            {
                ProgressionBalanceConfig fallback = ScriptableObject.CreateInstance<ProgressionBalanceConfig>();
                fallback.thinkEverySeconds = 14f;
                fallback.maxRecentEvents = 160;
                fallback.minScoreToConsider = 12f;
                fallback.scoreForSkillCandidate = 24f;
                fallback.scoreForTitleCandidate = 34f;
                fallback.scoreForQuestCandidate = 38f;
                fallback.skillCooldown = 420f;
                fallback.titleCooldown = 900f;
                fallback.questCooldown = 720f;
                progressionThink.balance = fallback;
            }
        }

        GeneratedRpgContentService content = EnsureSingleton<GeneratedRpgContentService>("GeneratedRpgContentService");
        EnsureSingleton<YQOriginGenerationService>("YQOriginGenerationService");
        EnsureSingleton<YQWorldGenerationService>("YQWorldGenerationService");
        EnsureSingleton<YQVisualStabilityDirector>("YQVisualStabilityDirector");
        EnsureSingleton<YQGeneratedRuntimeVfx>("YQGeneratedRuntimeVfx");
        EnsureSingleton<YQActiveQuestWorldHighlight>("YQActiveQuestWorldHighlight");
        EnsureSingleton<YQQuestCompletionDirector>("YQQuestCompletionDirector");
        EnsureSingleton<YQStillnessProgressionTracker>("YQStillnessProgressionTracker");

        PlayerBehaviorRollup rollup = EnsureSingleton<PlayerBehaviorRollup>("PlayerBehaviorRollup");
        rollup.accumulator = EventAccumulator.Instance;
        rollup.playerStateManager = PlayerStateManager.Instance;

        YQInvestorDirector director = EnsureSingleton<YQInvestorDirector>("YQInvestorDirector");
        director.snapshotBuilder = snapshot;
        director.progressionDecisionApplier = progression;
        director.worldDeltaApplier = worldDeltaApplier;
        director.contentService = content;
        director.minimumOfferConfidence = Mathf.Max(director.minimumOfferConfidence, 0.82f);
    }

    private void EnsureOriginPhaseServices()
    {
        // note: The threshold phase starts only the typed LLM/content contracts needed to resolve the questionnaire; world simulation remains deferred.
        EnsureSingleton<ActionRegistry>("ActionRegistry");
        EnsureSingleton<EventAccumulator>("EventAccumulator");
        EnsureSingleton<PlayerContext>("PlayerContext");
        LLMClient llm = EnsureSingleton<LLMClient>("LLMClient");
        llm.model = string.IsNullOrWhiteSpace(llm.model)
            ? "llama3.1"
            : llm.model;
        EnsureSingleton<GeneratedRpgContentService>("GeneratedRpgContentService");
        EnsureSingleton<YQOriginGenerationService>("YQOriginGenerationService");
        EnsureSingleton<YQWorldGenerationService>("YQWorldGenerationService");
        EnsureSingleton<YQGeneratedRuntimeVfx>("YQGeneratedRuntimeVfx");
    }

    private void EnsureGameplayPresentationServices()
    {
        // note: World-dependent presentation and planning are created only after terrain and the authoritative player exist.
        EnsureSingleton<YQGeneratedWorldMinimap>("YQGeneratedWorldMinimap");
        EnsureSingleton<YQGeneratedNpcPlanningService>("YQGeneratedNpcPlanningService");
        EnsureSingleton<YQRuntimeUrpMaterialRepair>("YQRuntimeUrpMaterialRepair");
    }

    private void CleanupLegacyObjects()
    {
        // note: No gameplay presentation belongs behind the title; each production UI is recreated after the selected save enters world loading.
        DestroyAll<YQInvestorDialogueUI>();
        DestroyAll<DialogueBoxUI>();
        DestroyAll<YourQuestTutorialHud>();
        DestroyAll<YourQuestTutorialMenuUI>();
        DestroyAll<YourQuestProgressionOfferUI>();
        DestroyAll<YQLockpickUi>();
        DestroyAll<YQGeneratedWorldMinimap>();
        DestroyAll<YQOriginQuestionnaireUI>();
        DestroyAll<YQPrototypeHUD>();
        DestroyAll<YourQuestTestHud>();
        DestroyAll<YourQuestTestCombat>();
        DestroyAll<YourQuestTestEnemy>();
        DestroyAll<YourQuestTestShrine>();
        DestroyAll<YourQuestTutorialLLMOrchestrator>();
        DestroyAll<YourQuestTestSceneRoot>();
        DestroyAll<YQAssetTestStation>();
        DestroyNamedSceneRoot("03__World_AssetTest");
        DestroyNamedSceneRoot("YourQuest_AssetTest");
        DestroyNamedSceneRoot("YQ_AssetTest");
        DestroyNamedSceneRoot("__YQ_AssetTest");
        DestroyNamedSceneRoot("__YQ_TestWorld");
        DestroyNamedSceneRoot("__YQ_TestSystems");
        DestroyNamedSceneRoot("YQ_InvestorWorldRoot");
        DestroyNamedSceneRoot("YQ_InvestorWorldRoot_Deprecated");
        DestroyNamedSceneRoot("PrototypeTutorial");
        DestroyNamedSceneRoot("PrototypeBuilder");
        DestroyNamedSceneRoot("YQ_PrototypeWorld");
        DestroySceneRootsByNameFragments("AssetTest", "TestScene", "TestStation", "PrototypeScene", "PlaceholderGallery");
        QuarantineAllPlayerObjects();
    }

    private void EnsureRuntimeWorld()
    {
        GameObject existingWorld = GameObject.Find("YQ_InvestorWorldRoot");
        if (existingWorld != null)
        {
            // note: The former hand-built investor test world is never a production fallback; quarantine it before constructing the authoritative player.
            existingWorld.name = "YQ_InvestorWorldRoot_Deprecated";
            existingWorld.SetActive(false);
            Destroy(existingWorld);
        }

        GameObject player = EnsureSingleAuthoritativePlayer();
        if (player == null)
            BuildPlayer();

        EnsureOriginActorStaging();
        EnsureSingleAuthoritativePlayer();

        // note: The player camera is created after the title scene during production startup, so hand it to the title stage before Unity can run two cameras or listeners together.
        YQTitleEnvironmentLoader.SuppressGameplayPresentationUntilRelease(
            Camera.main);
    }

    private static void EnsureOriginActorStaging()
    {
        List<GameObject> staleStagingRoots = new List<GameObject>(2);
        NpcDialogueAgent[] agents = FindObjectsByType<NpcDialogueAgent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < agents.Length; index++)
        {
            if (agents[index] != null &&
                string.Equals(agents[index].npcId, "npc_archivist_01", StringComparison.OrdinalIgnoreCase))
            {
                // note: Scene-authored/debug copies are not accepted as runtime authority; the persisted NPC record is rebound to one clean staged actor below.
                Transform parent = agents[index].transform.parent;
                if (parent != null &&
                    string.Equals(parent.name, "__YQ_OriginActorStaging", StringComparison.Ordinal))
                {
                    AddUniqueCandidate(staleStagingRoots, parent.gameObject);
                }
                agents[index].gameObject.SetActive(false);
                Destroy(agents[index].gameObject);
            }
        }

        for (int index = 0; index < staleStagingRoots.Count; index++)
        {
            GameObject previousStaging = staleStagingRoots[index];
            if (previousStaging == null)
                continue;
            previousStaging.SetActive(false);
            Destroy(previousStaging);
        }

        GameObject staging = new GameObject("__YQ_OriginActorStaging");
        DontDestroyOnLoad(staging);
        CreateNpc(
            staging.transform,
            "npc_archivist_01",
            "Archivist Vey",
            Vector3.zero,
            "origin_archivists",
            new[] { "friendly", "guide", "archivist", "talk", "tutorial", "questgiver", "quest_giver" },
            "Start with proof, not prophecy. I will mark what you actually do.");
        staging.SetActive(false);
        // note: Only Vey's structured NPC identity is staged; the reviewed WitchHouse site supplies every visible origin asset.
    }

    private static void QuarantineAllPlayerObjects()
    {
        YQInvestorPlayerMotor[] motors = FindObjectsByType<YQInvestorPlayerMotor>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int index = 0; index < motors.Length; index++)
        {
            if (motors[index] != null)
                DisableDuplicatePlayerObject(motors[index].gameObject);
        }

        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        for (int index = 0; index < taggedPlayers.Length; index++)
        {
            if (taggedPlayers[index] != null)
                DisableDuplicatePlayerObject(taggedPlayers[index]);
        }
    }

    private void EnsureRuntimeUi()
    {
        EnsureSingleton<YourQuestTutorialHud>("YourQuestTutorialHud");
        EnsureSingleton<YourQuestTutorialMenuUI>("YourQuestTutorialMenuUI");
        EnsureSingleton<YourQuestProgressionOfferUI>("YourQuestProgressionOfferUI");
        EnsureSingleton<YQInvestorDialogueUI>("YQInvestorDialogueUI");
        EnsureSingleton<YQLockpickUi>("YQLockpickUi");
        EnsureSingleton<YourQuestPauseMenuUI>("YourQuestPauseMenuUI");
        EnsureSingleton<YQProfileSaveSystem>("YQProfileSaveSystem");
        EnsureSingleton<YQTitleScreenUI>("YQTitleScreenUI");
        EnsureSingleton<YQProfileMenuUI>("YQProfileMenuUI");
        EnsureSingleton<YQOriginQuestionnaireUI>("YQOriginQuestionnaireUI");
    }


    private void ForceCloseStartupUi()
    {
        RuntimeModalUiBlocker.ClearAll();

        YourQuestTutorialMenuUI menu = FindFirstObjectByType<YourQuestTutorialMenuUI>();
        if (menu != null)
            menu.ForceCloseFromBootstrap();

        YQInvestorDialogueUI dialogue = FindFirstObjectByType<YQInvestorDialogueUI>();
        if (dialogue != null)
            dialogue.ForceCloseFromBootstrap();

        YourQuestPauseMenuUI pause = FindFirstObjectByType<YourQuestPauseMenuUI>();
        if (pause != null)
            pause.ForceCloseFromBootstrap();

        YourQuestProgressionOfferUI offer = FindFirstObjectByType<YourQuestProgressionOfferUI>();
        if (offer != null)
            offer.ForceHideFromBootstrap();

        YQProfileMenuUI profileMenu = FindFirstObjectByType<YQProfileMenuUI>();
        if (profileMenu != null)
            profileMenu.ForceCloseFromBootstrap();

        Time.timeScale = 1f;
        AudioListener.pause = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void WireReferences()
    {
        GameObject player = GetAuthoritativePlayer();
        if (player == null)
            return;

        SituationSnapshotBuilder snapshot = FindFirstObjectByType<SituationSnapshotBuilder>();
        if (snapshot != null)
        {
            snapshot.player = player.transform;
            snapshot.playerContext = PlayerContext.Instance;
            snapshot.playerStateManager = PlayerStateManager.Instance;
        }

        ProgressionDecisionApplier progression = FindFirstObjectByType<ProgressionDecisionApplier>();
        if (progression != null)
        {
            progression.snapshotBuilder = snapshot;
            progression.playerProfile = player.GetComponent<PlayerProfile>();
        }

        YQInvestorDialogueUI dialogueUi = FindFirstObjectByType<YQInvestorDialogueUI>();
        if (dialogueUi != null)
        {
            dialogueUi.playerRoot = player.transform;
            dialogueUi.viewCamera = Camera.main;
            dialogueUi.talkRadius = 2.65f;
        }

        YQInvestorDirector director = FindFirstObjectByType<YQInvestorDirector>();
        if (director != null)
            director.player = player.transform;

        NpcDialogueAgent[] agents = FindObjectsByType<NpcDialogueAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] != null)
                agents[i].RefreshIdentityAndSession();
        }
    }

    private static GameObject GetAuthoritativePlayer()
    {
        if (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative)
        {
            GameObject keep = YQInvestorPlayerMotor.ActiveMotor.gameObject;
            DisableTaggedPlayerObjectsExcept(keep);
            DisableNonAuthoritativePlayerMotorsExcept(keep);
            return keep;
        }

        return EnsureSingleAuthoritativePlayer();
    }

    private static GameObject EnsureSingleAuthoritativePlayer()
    {
        List<GameObject> candidates = new List<GameObject>(8);
        YQInvestorPlayerMotor[] motors = FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < motors.Length; i++)
        {
            if (motors[i] != null)
                AddUniqueCandidate(candidates, motors[i].gameObject);
        }

        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < taggedPlayers.Length; i++)
            AddUniqueCandidate(candidates, taggedPlayers[i]);

        if (candidates.Count == 0)
            return null;

        GameObject keep = null;
        YQInvestorPlayerMotor keepMotor = null;
        int bestScore = int.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate == null)
                continue;

            YQInvestorPlayerMotor motor = candidate.GetComponent<YQInvestorPlayerMotor>();
            if (motor == null)
                continue;

            int score = ScorePlayerCandidate(candidate, motor);
            if (score > bestScore)
            {
                bestScore = score;
                keep = candidate;
                keepMotor = motor;
            }
        }

        if (keep == null || keepMotor == null)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                GameObject candidate = candidates[i];
                if (candidate != null && candidate.CompareTag("Player"))
                    DisableDuplicatePlayerObject(candidate);
            }
            return null;
        }

        keep.name = "Player";
        keep.SetActive(true);
        if (!keep.CompareTag("Player"))
            keep.tag = "Player";
        DontDestroyOnLoad(keep);
        YQInvestorPlayerMotor.ForceAuthority(keepMotor);

        for (int i = 0; i < candidates.Count; i++)
        {
            GameObject candidate = candidates[i];
            if (candidate != null && candidate != keep)
                DisableDuplicatePlayerObject(candidate);
        }
        DisableNonAuthoritativePlayerMotorsExcept(keep);

        return keep;
    }

    private static void DisableTaggedPlayerObjectsExcept(GameObject keep)
    {
        if (keep == null)
            return;

        GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
        for (int i = 0; i < taggedPlayers.Length; i++)
        {
            GameObject tagged = taggedPlayers[i];
            if (tagged == null || tagged == keep)
                continue;

            DisableDuplicatePlayerObject(tagged);
        }
    }

    private static void DisableNonAuthoritativePlayerMotorsExcept(GameObject keep)
    {
        YQInvestorPlayerMotor[] motors = FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < motors.Length; i++)
        {
            YQInvestorPlayerMotor motor = motors[i];
            if (motor == null || motor.gameObject == keep)
                continue;

            DisableDuplicatePlayerObject(motor.gameObject);
        }
    }

    private static void AddUniqueCandidate(List<GameObject> candidates, GameObject candidate)
    {
        if (candidate == null || candidates == null)
            return;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] == candidate)
                return;
        }
        candidates.Add(candidate);
    }

    private static int ScorePlayerCandidate(GameObject candidate, YQInvestorPlayerMotor motor)
    {
        int score = 0;
        if (candidate.activeInHierarchy)
            score += 16;
        if (candidate.CompareTag("Player"))
            score += 18;
        if (string.Equals(candidate.name, "Player", StringComparison.OrdinalIgnoreCase))
            score += 12;
        if (motor.enabled)
            score += 10;
        if (motor.cameraPivot != null)
            score += 10;
        if (motor.playerCamera != null)
            score += 12;

        Camera main = Camera.main;
        if (main != null)
        {
            if (motor.playerCamera == main)
                score += 35;
            Vector3 anchor = motor.cameraPivot != null ? motor.cameraPivot.position : candidate.transform.position + Vector3.up * 1.6f;
            float distance = Vector3.Distance(main.transform.position, anchor);
            score += Mathf.RoundToInt(Mathf.Clamp(24f - distance, -32f, 24f));
        }

        string name = candidate.name ?? string.Empty;
        if (name.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("deprecated", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("test", StringComparison.OrdinalIgnoreCase) >= 0)
            score -= 60;
        return score;
    }

    private static void DisableDuplicatePlayerObject(GameObject duplicate)
    {
        if (duplicate == null)
            return;

        try
        {
            if (duplicate.CompareTag("Player"))
                duplicate.tag = "Untagged";
        }
        catch { }

        duplicate.name = duplicate.name + "_DuplicateDisabled";
        Behaviour[] behaviours = duplicate.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;
            behaviour.enabled = false;
        }

        duplicate.SetActive(false);
        Destroy(duplicate);
    }

    private void SeedData()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        WorldStateManager wsm = WorldStateManager.Instance;
        GeneratedRpgContentService content = GeneratedRpgContentService.Instance;
        if (psm == null || psm.state == null || wsm == null || wsm.State == null)
            return;

        psm.state.EnsureCollections();
        YQGeneratedContentCuration.CleanExistingState(psm.state);
        bool originComplete = GeneratedRpgContentService.HasCompletedOrigin(psm.state);
        psm.state.displayName = string.IsNullOrWhiteSpace(psm.state.displayName) ? "The Player" : psm.state.displayName;
        psm.state.stats.maxHealth = Mathf.Max(100, psm.state.stats.maxHealth);
        psm.state.stats.maxStamina = Mathf.Max(100, psm.state.stats.maxStamina);
        psm.state.stats.maxMana = Mathf.Max(50, psm.state.stats.maxMana);
        psm.state.stats.moveSpeed = Mathf.Max(6.5f, psm.state.stats.moveSpeed);
        if (string.IsNullOrWhiteSpace(psm.state.currentRegionId) || psm.state.currentRegionId == "region_unknown")
        {
            psm.state.currentRegionId = "origin_forest";
            psm.state.currentRegionName = originComplete ? "Whisperroot Clearing" : "Goddess Threshold";
        }

        if (string.IsNullOrWhiteSpace(wsm.State.currentRegionId) || wsm.State.currentRegionId == "region_unknown")
            wsm.SetCurrentRegion("origin_forest", originComplete ? "Whisperroot Clearing" : "Goddess Threshold");

        content?.EnsureBaselineGeneratedState(psm.state, wsm.State);
        YQWorldGenerationService.Instance?.EnsureWorldPlan(psm.state, wsm.State, originComplete);
        YQGeneratedContentCuration.CleanExistingState(psm.state);
        if (originComplete)
            EnsureTutorialQuestChain(psm.state);
        else
            RemoveObsoleteTutorialQuests(psm.state, true);
        psm.state.GetActiveQuest();
        psm.Save();
        wsm.Save();
    }

    private static void EnsureTutorialQuestChain(PlayerState state)
    {
        if (state == null)
            return;

        state.EnsureCollections();
        bool resetForThisTutorial = state.behaviorCounters == null || !state.behaviorCounters.ContainsKey(TutorialProgressCounter);
        RemoveObsoleteTutorialQuests(state, resetForThisTutorial);
        if (resetForThisTutorial)
        {
            ClearTutorialCounters(state);
        }

        long now = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        UpsertTutorialQuest(state, "tutorial_01_talk_archivist", "Speak With Archivist Vey",
            "Talk to Archivist Vey between the Goddess statue and witch hut. She marks the first trial around what you do, not where you stand.",
            new[] { "tutorial_main", "dialogue", "talk", "archivist", "guide", "quest_giver" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_02_claim_training_kit", "Inspect and Equip Manifested Gear",
            "Use the gear the goddess manifested from your answers. Equip a weapon or armor piece before the route turns hostile.",
            new[] { "tutorial_main", "origin_manifest", "item", "gear", "equip", "weapon" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_03_restore_at_shrine", "Recover at the Shrine",
            "Activate the Shrine of First Breath. Restore health, stamina, and mana before committing to the locked path.",
            new[] { "tutorial_main", "shrine", "restore", "recover" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_04_open_practice_lock", "Open the Practice Lock",
            "Pick the Practice Lock Gate or the Practice Locked Cache. The first lock is deterministic so the lesson is clear.",
            new[] { "tutorial_main", "lockpick", "door", "chest", "practice" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_05_wake_mimic", "Open the Too-Quiet Chest",
            "Open the too-quiet chest in the side alcove. Some rewards wait until your hand proves the risk.",
            new[] { "tutorial_main", "mimic", "chest" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_06_cast_spell", "Cast Into the Trial Yard",
            "Cast a spell once with right click at the focus stone. Watch for the mana read and impact feedback.",
            new[] { "tutorial_main", "spell", "cast", "mana", "trial" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_07_defeat_and_loot", "Defeat and Loot the First Echo",
            "Defeat one echo enemy in the trial yard, then loot the residue it leaves behind for a concrete reward.",
            new[] { "tutorial_main", "combat", "defeat", "echo", "loot", "corpse" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_08_choose_offer", "Choose a Progression Offer",
            "Accept or decline the progression offer when it appears. Skills, spells, and titles answer your stimulus.",
            new[] { "tutorial_main", "progression", "offer", "accept", "decline", "skill" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_09_cross_snow_gate", "Cross the First Snow Gate",
            "Step through the north gate into the First Snow Trial. The region matters, but the next response still starts with you.",
            new[] { "tutorial_main", "region", "first road", "snow", "trial", "north" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_10_report_warden", "Report to Warden Thorne",
            "Talk to Warden Thorne at the snow gate to finish the tutorial loop. Save and profile tools are in pause.",
            new[] { "tutorial_main", "dialogue", "report", "warden", "save", "profile", "quest_giver" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_11_north_oath", "North Road: Hold the Frostglass Oath",
            "Speak with Warden Thorne, defeat one frost hostile, and claim the Frostglass Ward. The north road tests whether your first answer becomes a guard, a counter, or a retreat.",
            new[] { "tutorial_main", "cardinal", "north", "frost", "dialogue", "combat", "item", "ward" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_12_east_cinder", "East Road: Temper the Cinder Vow",
            "Speak with Cinder Prefect Mael, survive one ember monster, and take the Cinder Trial Blade. The east road burns away bravado until only repeatable courage is left.",
            new[] { "tutorial_main", "cardinal", "east", "fire", "dialogue", "combat", "item", "weapon" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_13_south_auralith", "South Road: Answer Auralith's Root",
            "Speak with Root-Sibyl Ivara, face one living-terrain monster, and recover the Auralith Seed Charm. The south road belongs to the old natural precursor, but the skill must belong to you.",
            new[] { "tutorial_main", "cardinal", "south", "nature", "auralith", "dialogue", "combat", "item", "trinket" }, now, resetForThisTutorial);
        UpsertTutorialQuest(state, "tutorial_14_west_tide", "West Road: Map the Tideglass Step",
            "Speak with Tide Cartographer Sera, defeat one shore monster, and claim the Tideglass Step Boots. The west road measures how you reposition when the floor refuses to stay still.",
            new[] { "tutorial_main", "cardinal", "west", "water", "dialogue", "combat", "item", "boots" }, now, resetForThisTutorial);

        if (resetForThisTutorial)
            state.behaviorCounters[TutorialProgressCounter] = 1f;

        QuestRecord generatedOriginQuest = FindFirstQuestWithTag(state, "origin_generated");
        if (generatedOriginQuest != null && !IsCompletedQuest(generatedOriginQuest))
        {
            state.SetActiveQuest(generatedOriginQuest.questId);
            state.Touch();
            return;
        }

        SelectFirstIncompleteTutorialQuest(state);
        state.Touch();
    }

    private static void UpsertTutorialQuest(PlayerState state, string questId, string name, string description, string[] tags, long now, bool reset)
    {
        QuestRecord quest = FindQuestById(state, questId);
        if (quest == null)
        {
            quest = new QuestRecord
            {
                questId = questId,
                createdUnix = now,
                status = "offer"
            };
            state.quests.Add(quest);
        }
        else if (reset)
        {
            quest.status = "offer";
            quest.completedUnix = 0;
            quest.rewardGold = 0;
            quest.rewardXp = 0;
        }

        quest.name = name;
        quest.description = description;
        quest.tags = tags ?? System.Array.Empty<string>();
        quest.updatedUnix = now;
    }

    private static QuestRecord FindQuestById(PlayerState state, string questId)
    {
        if (state == null || state.quests == null || string.IsNullOrWhiteSpace(questId))
            return null;

        for (int i = 0; i < state.quests.Count; i++)
        {
            QuestRecord quest = state.quests[i];
            if (quest != null && string.Equals(quest.questId, questId, System.StringComparison.OrdinalIgnoreCase))
                return quest;
        }

        return null;
    }

    private static QuestRecord FindFirstQuestWithTag(PlayerState state, string tag)
    {
        if (state == null || state.quests == null || string.IsNullOrWhiteSpace(tag))
            return null;

        for (int i = 0; i < state.quests.Count; i++)
        {
            QuestRecord quest = state.quests[i];
            if (quest != null && HasTag(quest, tag))
                return quest;
        }

        return null;
    }

    private static void SelectFirstIncompleteTutorialQuest(PlayerState state)
    {
        string[] orderedQuestIds =
        {
            "tutorial_01_talk_archivist",
            "tutorial_02_claim_training_kit",
            "tutorial_03_restore_at_shrine",
            "tutorial_04_open_practice_lock",
            "tutorial_05_wake_mimic",
            "tutorial_06_cast_spell",
            "tutorial_07_defeat_and_loot",
            "tutorial_08_choose_offer",
            "tutorial_09_cross_snow_gate",
            "tutorial_10_report_warden",
            "tutorial_11_north_oath",
            "tutorial_12_east_cinder",
            "tutorial_13_south_auralith",
            "tutorial_14_west_tide"
        };

        for (int i = 0; i < orderedQuestIds.Length; i++)
        {
            QuestRecord quest = FindQuestById(state, orderedQuestIds[i]);
            if (quest == null || IsCompletedQuest(quest))
                continue;

            state.SetActiveQuest(quest.questId);
            return;
        }
    }

    private static bool IsCompletedQuest(QuestRecord quest)
    {
        if (quest == null)
            return true;

        if (quest.completedUnix > 0)
            return true;

        string status = quest.status ?? string.Empty;
        return status.Equals("complete", System.StringComparison.OrdinalIgnoreCase) ||
               status.Equals("completed", System.StringComparison.OrdinalIgnoreCase) ||
               status.Equals("failed", System.StringComparison.OrdinalIgnoreCase);
    }

    private static void RemoveObsoleteTutorialQuests(PlayerState state, bool includeCurrentTutorialQuests)
    {
        if (state == null || state.quests == null)
            return;

        for (int i = state.quests.Count - 1; i >= 0; i--)
        {
            QuestRecord quest = state.quests[i];
            if (quest == null)
                continue;

            string name = quest.name ?? string.Empty;
            if (name.Equals("Wake Beneath the Green Roof", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Read the Four Roads", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Wake in Vey's Forest Hut", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Read the Four Thresholds", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Hold the Frostglass Oath", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Temper the Cinder Vow", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Answer Auralith's Root", System.StringComparison.OrdinalIgnoreCase) ||
                name.Equals("Map the Tideglass Step", System.StringComparison.OrdinalIgnoreCase) ||
                (includeCurrentTutorialQuests && HasTag(quest, "tutorial_main")))
            {
                state.quests.RemoveAt(i);
            }
        }

        state.activeQuestId = string.Empty;
    }

    private static bool HasTag(QuestRecord quest, string tag)
    {
        if (quest == null || quest.tags == null || string.IsNullOrWhiteSpace(tag))
            return false;

        for (int i = 0; i < quest.tags.Length; i++)
        {
            if (string.Equals(quest.tags[i], tag, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void ClearTutorialCounters(PlayerState state)
    {
        if (state == null || state.behaviorCounters == null)
            return;

        string[] prefixes =
        {
            "dialogue:npc_archivist_01",
            "dialogue:npc_warden_01",
            "dialogue:npc_cinder_01",
            "dialogue:npc_root_sibyl_01",
            "dialogue:npc_tide_cartographer_01",
            "pickup:",
            "item:equip",
            "item:consume",
            "interact:shrine",
            "lockpick:",
            "mimic:",
            "cast:",
            "combat:",
            "kill:region_ice_north",
            "kill:region_fire_east",
            "kill:region_jungle_south",
            "kill:region_water_west",
            "loot:"
        };

        List<string> keys = new List<string>(state.behaviorCounters.Keys);
        for (int i = 0; i < keys.Count; i++)
        {
            string key = keys[i] ?? string.Empty;
            for (int p = 0; p < prefixes.Length; p++)
            {
                if (key.StartsWith(prefixes[p], System.StringComparison.OrdinalIgnoreCase))
                {
                    state.behaviorCounters.Remove(key);
                    break;
                }
            }
        }
    }

    private static T EnsureSingleton<T>(string name) where T : Component
    {
        T existing = FindFirstObjectByType<T>();
        if (existing != null)
        {
            DontDestroyOnLoad(existing.gameObject);
            return existing;
        }

        GameObject go = new GameObject(name);
        DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }

    private static void DestroyAll<T>() where T : Component
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null)
            {
                all[i].gameObject.SetActive(false);
                Destroy(all[i].gameObject);
            }
        }
    }

    private static void DestroyAllButFirst<T>() where T : Component
    {
        T[] all = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 1; i < all.Length; i++)
        {
            if (all[i] != null)
            {
                all[i].gameObject.SetActive(false);
                Destroy(all[i].gameObject);
            }
        }
    }

    private static void DestroyNamedSceneRoot(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        GameObject root = GameObject.Find(name);
        if (root == null)
            return;

        root.SetActive(false);
        Destroy(root);
    }

    private static void DestroySceneRootsByNameFragments(params string[] fragments)
    {
        if (fragments == null || fragments.Length == 0)
            return;

        GameObject[] roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];
            if (root == null || ShouldKeepSceneRoot(root))
                continue;

            string rootName = root.name ?? string.Empty;
            for (int j = 0; j < fragments.Length; j++)
            {
                string fragment = fragments[j];
                if (string.IsNullOrWhiteSpace(fragment) || rootName.IndexOf(fragment, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                root.SetActive(false);
                Destroy(root);
                break;
            }
        }
    }

    private static bool ShouldKeepSceneRoot(GameObject root)
    {
        if (root == null)
            return true;

        string name = root.name ?? string.Empty;
        return string.Equals(name, "Player", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(name, "__YQ_InvestorBootstrap", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("YQ_InvestorWorldRoot", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("PlayerStateManager", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("WorldStateManager", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("EventSystem", StringComparison.OrdinalIgnoreCase);
    }

    private static bool NeedsWorldRebuild(GameObject worldRoot)
    {
        if (worldRoot == null)
            return true;

        Transform t = worldRoot.transform;
        return t.Find("Region_Hub") != null ||
               t.Find(RuntimeWorldVersionMarker) == null ||
               t.Find("Origin_Hut") == null ||
               t.Find("Region_IceNorth") == null ||
               t.Find("Region_FireEast") == null ||
               t.Find("Region_JungleSouth") == null ||
               t.Find("Region_WaterWest") == null;
    }

    private static void EnsureEventSystem()
    {
        EventSystem[] existing = FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (existing.Length > 0)
        {
            for (int i = 0; i < existing.Length; i++)
                DontDestroyOnLoad(existing[i].gameObject);
            return;
        }

        GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        DontDestroyOnLoad(go);
    }

    private void BuildWorld()
    {
        GameObject root = new GameObject("YQ_InvestorWorldRoot");
        DontDestroyOnLoad(root);

        GameObject versionMarker = new GameObject(RuntimeWorldVersionMarker);
        versionMarker.transform.SetParent(root.transform, false);

        GameObject lightGo = new GameObject("Directional Light");
        lightGo.transform.SetParent(root.transform, false);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.34f;

        if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
        {
            // note: shadowResolution only applies to the Built-In Render Pipeline; URP logs a compatibility warning.
            light.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;
        }

        light.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

        CreateRuntimeTerrainOrUseSaved(root.transform);
        CreateBoundary(root.transform, new Vector3(0f, 3f, 148f), new Vector3(296f, 6f, 2f));
        CreateBoundary(root.transform, new Vector3(0f, 3f, -148f), new Vector3(296f, 6f, 2f));
        CreateBoundary(root.transform, new Vector3(148f, 3f, 0f), new Vector3(2f, 6f, 296f));
        CreateBoundary(root.transform, new Vector3(-148f, 3f, 0f), new Vector3(2f, 6f, 296f));

        CreateAmbientLoop(root.transform, "Forest_AmbientWindBed", AmbientWindAudio, new Vector3(0f, 2.2f, 14f), 0.0f, 0.92f, 0f, 0f);
        CreateAmbientLoop(root.transform, "Hut_FireplaceLoop", FireLoopAudio, new Vector3(2.6f, 1.2f, 1.1f), 0.09f, 0.96f, 2f, 9f);
        CreateAmbientLoop(root.transform, "Shrine_AuralithHum", AmbientHumAudio, new Vector3(-8f, 1.2f, 13f), 0.004f, 0.66f, 2f, 4.5f);
        CreateAmbientLoop(root.transform, "SnowGate_WindBed", AmbientWindAudio, new Vector3(0f, 2.2f, 60f), 0.0f, 0.82f, 0f, 0f);
        CreateAmbientLoop(root.transform, "EmberGate_FireBed", FireLoopAudio, new Vector3(56f, 1.4f, 4f), 0.075f, 0.90f, 4f, 10f);
        CreateAmbientLoop(root.transform, "RootGate_AuralithHum", AmbientHumAudio, new Vector3(0f, 1.4f, -56f), 0.003f, 0.61f, 2f, 4f);
        CreateAmbientLoop(root.transform, "TideGate_LowHum", string.Empty, new Vector3(-56f, 1.4f, 4f), 0.0f, 1.0f, 0f, 0f);

        CreateRegion(root.transform, "Region_OriginForest", new Vector3(0f, 2f, 14f), new Vector3(50f, 4f, 76f), "origin_forest", "Goddess Threshold", new List<string> { "origin", "forest", "safe_zone", "tutorial", "goddess", "witch_hut", "guide" });
        CreateRegion(root.transform, "Region_IceNorth", new Vector3(0f, 2f, 68f), new Vector3(38f, 4f, 32f), "region_ice_north", "North Road: Frostglass Reach", new List<string> { "ice", "north", "trial", "frost", "first road", "cardinal" });
        CreateRegion(root.transform, "Region_FireEast", new Vector3(56f, 2f, 4f), new Vector3(30f, 4f, 34f), "region_fire_east", "East Road: Cinderfall Crucible", new List<string> { "fire", "east", "trial", "ember", "cardinal" });
        CreateRegion(root.transform, "Region_JungleSouth", new Vector3(0f, 2f, -54f), new Vector3(34f, 4f, 32f), "region_jungle_south", "South Road: Auralith Root", new List<string> { "jungle", "south", "trial", "grass", "auralith", "cardinal" });
        CreateRegion(root.transform, "Region_WaterWest", new Vector3(-56f, 2f, 4f), new Vector3(30f, 4f, 34f), "region_water_west", "West Road: Tideglass Step", new List<string> { "water", "west", "trial", "tide", "cardinal" });

        CreateRoadMarker(root.transform, new Vector3(0f, 0.07f, 8f), new Vector3(18f, 0.10f, 18f), new Color(0.31f, 0.28f, 0.21f, 1f));
        CreateRoadMarker(root.transform, new Vector3(0f, 0.076f, 34f), new Vector3(5.2f, 0.10f, 48f), new Color(0.30f, 0.27f, 0.21f, 1f));
        CreateRoadMarker(root.transform, new Vector3(0f, 0.084f, 63f), new Vector3(16f, 0.10f, 12f), new Color(0.45f, 0.64f, 0.70f, 1f));
        CreateRoadMarker(root.transform, new Vector3(34f, 0.073f, 4f), new Vector3(42f, 0.10f, 4.2f), new Color(0.30f, 0.25f, 0.19f, 1f));
        CreateRoadMarker(root.transform, new Vector3(-34f, 0.073f, 4f), new Vector3(42f, 0.10f, 4.2f), new Color(0.20f, 0.26f, 0.28f, 1f));
        CreateRoadMarker(root.transform, new Vector3(0f, 0.073f, -27f), new Vector3(4.2f, 0.10f, 36f), new Color(0.19f, 0.29f, 0.20f, 1f));

        // note: The reviewed WitchHouse origin site owns the hut; creating legacy primitive scaffolding here exposes a purple placeholder while generation runs.
        YourQuestTutorialWorldHelpers.CreateForestScatter(root.transform);
        YourQuestTutorialWorldHelpers.CreateRegionDressing(root.transform);

        CreateNpc(root.transform, "npc_archivist_01", "Archivist Vey", new Vector3(4.8f, 0f, 7.4f), "origin_archivists", new[] { "friendly", "guide", "archivist", "talk", "tutorial", "questgiver", "quest_giver" }, "Start with proof, not prophecy. I will mark what you actually do: speak, equip, recover, open, fight, loot, choose.");
        CreateNpc(root.transform, "npc_warden_01", "Warden Thorne", new Vector3(6.2f, 0f, 64f), "first_gate_wardens", new[] { "friendly", "warden", "north", "frost", "cardinal", "tutorial", "save", "profile", "progression", "quest_giver" }, "The north road crowns no hero. It records whether pressure makes you guard, counter, or break.");
        CreateNpc(root.transform, "npc_cinder_01", "Cinder Prefect Mael", new Vector3(51.4f, 0f, 13.2f), "cinder_vanguard", new[] { "friendly", "mentor", "east", "fire", "cinder", "cardinal", "tutorial", "quest_giver" }, "The east road is a forge with teeth. Bring me courage you can repeat after the flame answers back.");
        CreateNpc(root.transform, "npc_root_sibyl_01", "Root-Sibyl Ivara", new Vector3(8.8f, 0f, -49.2f), "auralith_keepers", new[] { "friendly", "mentor", "south", "nature", "auralith", "jungle", "cardinal", "tutorial", "quest_giver" }, "Auralith was older than kingdoms and hungrier than mercy. If the root teaches you anything, make it your answer, not the forest's.");
        CreateNpc(root.transform, "npc_tide_cartographer_01", "Tide Cartographer Sera", new Vector3(-51.4f, 0f, 13.2f), "tide_cartographers", new[] { "friendly", "mentor", "west", "water", "tide", "cardinal", "tutorial", "quest_giver" }, "The west road maps people by what they do when footing changes. Step, recover, then make the tide remember you.");

        CreateEquipmentBench(root.transform, new Vector3(9.1f, 0f, 12.5f));
        CreateLessonBeacon(root.transform, "Station_01_SpeakBeacon", new Vector3(2.3f, 0f, 6.3f), "Archivist's First Mark", new Color(0.95f, 0.78f, 0.34f, 1f), new[] { "tutorial", "talk", "archivist", "guide", "dialogue" });
        CreateLessonBeacon(root.transform, "Station_02_GearBeacon", new Vector3(9.1f, 0f, 9.2f), "Training Kit Bench", new Color(0.88f, 0.66f, 0.28f, 1f), new[] { "tutorial", "pickup", "gear", "equip", "weapon" });
        CreateLessonBeacon(root.transform, "Station_03_ShrineBeacon", new Vector3(-9.8f, 0f, 12.8f), "First Breath Marker", new Color(0.46f, 0.82f, 1f, 1f), new[] { "tutorial", "shrine", "restore", "recover" });
        CreateLessonBeacon(root.transform, "Station_04_LockBeacon", new Vector3(-3.5f, 0f, 20f), "Practice Lock Marker", new Color(0.78f, 0.64f, 0.34f, 1f), new[] { "tutorial", "lockpick", "door", "practice" });
        CreateLessonBeacon(root.transform, "Station_05_MimicBeacon", new Vector3(13.6f, 0f, 23.7f), "Quiet Cache Marker", new Color(0.62f, 0.48f, 0.78f, 1f), new[] { "tutorial", "mimic", "chest", "risk" });
        CreateLessonBeacon(root.transform, "Station_06_SpellFocus", new Vector3(0f, 0f, 36.9f), "Trial Focus Stone", new Color(0.58f, 0.78f, 1f, 1f), new[] { "tutorial", "spell", "cast", "mana", "trial" });
        CreateLessonBeacon(root.transform, "Station_08_OfferBeacon", new Vector3(-7.4f, 0f, 51.5f), "Choice Marker", new Color(1f, 0.84f, 0.32f, 1f), new[] { "tutorial", "progression", "offer", "accept", "decline" });

        CreateTutorialPickup(root.transform, "Frostglass Ward Pickup", new Vector3(-7.2f, 0.78f, 65.2f), CreateTutorialItem("tutorial_frostglass_ward", "Frostglass Ward", "offhand", "offhand", "A north-road guard focus that rewards patient blocking, clean counters, and choosing not to panic first.", 1, 0, 4, 0, 0, 8, 0, 0f, 0), new Color(0.60f, 0.86f, 0.96f, 1f), new[] { "pickup", "gear", "offhand", "north", "frost", "ward", "cardinal" });
        CreateTutorialPickup(root.transform, "Cinder Trial Blade Pickup", new Vector3(52.6f, 0.78f, 8.2f), CreateTutorialItem("tutorial_cinder_trial_blade", "Cinder Trial Blade", "weapon", "weapon", "An east-road blade for players who keep advancing after impact. It makes aggression legible instead of noisy.", 1, 8, 0, 0, 0, 0, 0, 0f, 0), new Color(1f, 0.48f, 0.24f, 1f), new[] { "pickup", "gear", "weapon", "east", "fire", "cinder", "cardinal" });
        CreateTutorialPickup(root.transform, "Auralith Seed Charm Pickup", new Vector3(11.8f, 0.78f, -50.2f), CreateTutorialItem("tutorial_auralith_seed_charm", "Auralith Seed Charm", "trinket", "trinket", "A living charm from the First Green. It answers survival around roots, poison, shelter, and terrain with player-shaped mana.", 1, 1, 1, 0, 6, 10, 0, 0.01f, 0), new Color(0.48f, 0.90f, 0.46f, 1f), new[] { "pickup", "gear", "trinket", "south", "nature", "auralith", "cardinal" });
        CreateTutorialPickup(root.transform, "Tideglass Step Boots Pickup", new Vector3(-52.6f, 0.78f, 8.2f), CreateTutorialItem("tutorial_tideglass_step_boots", "Tideglass Step Boots", "armor", "boots", "West-road boots that reward repositioning when the ground changes under you.", 1, 0, 2, 0, 8, 0, 0, 0.05f, 0), new Color(0.38f, 0.78f, 0.92f, 1f), new[] { "pickup", "gear", "boots", "west", "water", "tide", "cardinal" });

        CreateTutorialGate(root.transform, "North_Trial_Gate", new Vector3(0f, 0f, 55f), true, new Color(0.62f, 0.82f, 0.88f, 1f));
        CreateTutorialGate(root.transform, "East_Trial_Gate", new Vector3(45f, 0f, 4f), false, new Color(0.54f, 0.40f, 0.26f, 1f));
        CreateTutorialGate(root.transform, "South_Trial_Gate", new Vector3(0f, 0f, -45f), true, new Color(0.30f, 0.50f, 0.27f, 1f));
        CreateTutorialGate(root.transform, "West_Trial_Gate", new Vector3(-45f, 0f, 4f), false, new Color(0.28f, 0.45f, 0.52f, 1f));

        CreateTutorialFence(root.transform, "Start_Courtyard_LeftRail", new Vector3(-10.6f, 0.55f, 11f), new Vector3(0.35f, 1.1f, 18f), new Color(0.22f, 0.18f, 0.12f, 1f));
        CreateTutorialFence(root.transform, "Start_Courtyard_RightRail", new Vector3(14.8f, 0.55f, 14f), new Vector3(0.35f, 1.1f, 15f), new Color(0.22f, 0.18f, 0.12f, 1f));
        CreateTutorialFence(root.transform, "Mimic_Alcove_BackRail", new Vector3(13.5f, 0.55f, 27.5f), new Vector3(10f, 1.1f, 0.35f), new Color(0.22f, 0.18f, 0.12f, 1f));
        CreateTutorialFence(root.transform, "Arena_LeftRail", new Vector3(-13f, 0.55f, 43f), new Vector3(0.35f, 1.1f, 19f), new Color(0.22f, 0.18f, 0.12f, 1f));
        CreateTutorialFence(root.transform, "Arena_RightRail", new Vector3(13f, 0.55f, 43f), new Vector3(0.35f, 1.1f, 19f), new Color(0.22f, 0.18f, 0.12f, 1f));
        CreateTutorialFence(root.transform, "Offer_Gate_LeftRail", new Vector3(-13.4f, 0.55f, 52.8f), new Vector3(0.35f, 1.1f, 7.2f), new Color(0.22f, 0.18f, 0.12f, 1f));
        CreateTutorialFence(root.transform, "Offer_Gate_RightRail", new Vector3(2.4f, 0.55f, 52.8f), new Vector3(0.35f, 1.1f, 7.2f), new Color(0.22f, 0.18f, 0.12f, 1f));

        CreateTraversalBlock(root.transform, "Practice_LogBalance", new Vector3(-7f, 0.38f, 17f), new Vector3(6f, 0.76f, 1.0f), new Color(0.34f, 0.24f, 0.14f, 1f));
        CreateTraversalBlock(root.transform, "Practice_LowStone", new Vector3(-7f, 0.42f, 25f), new Vector3(3.2f, 0.84f, 3.2f), new Color(0.29f, 0.31f, 0.29f, 1f));
        CreateTraversalBlock(root.transform, "Practice_ClimbBoulder", new Vector3(-7f, 1.05f, 33f), new Vector3(3.6f, 2.1f, 2.4f), new Color(0.22f, 0.26f, 0.25f, 1f));
        CreatePracticeLockGate(root.transform, "Practice Lock Gate", new Vector3(0f, 1.22f, 20f), new Vector3(4.8f, 2.44f, 0.28f), 0.06f);

        CreateShrine(root.transform, new Vector3(-7.8f, 0.75f, 12.5f), "Shrine of First Breath", new Color(0.62f, 0.86f, 1f, 1f));
        CreateShrine(root.transform, new Vector3(-13f, 0.75f, -10f), "Auralith Memory Stone", new Color(0.62f, 1f, 0.70f, 1f));

        CreateTutorialSign(root.transform, "Sign_Start", "01 Speak", new Vector3(-2.8f, 0f, 9.0f), 18f);
        CreateTutorialSign(root.transform, "Sign_Inventory", "02 Gear", new Vector3(12.6f, 0f, 7.8f), -48f);
        CreateTutorialSign(root.transform, "Sign_Shrine", "03 Restore", new Vector3(-12.6f, 0f, 11.8f), 50f);
        CreateTutorialSign(root.transform, "Sign_PracticeLine", "04 Lock", new Vector3(-8.8f, 0f, 22.6f), 40f);
        CreateTutorialSign(root.transform, "Sign_Combat", "05 Trial Yard", new Vector3(5.4f, 0f, 35.0f), -25f);
        CreateTutorialSign(root.transform, "Sign_Save", "06 Save", new Vector3(7.2f, 0f, 56.0f), -35f);

        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Trail Supply Cache", new Vector3(4.8f, 0.1f, 15.2f), "origin_forest", false, 0.05f, 16, ChestPrefab);
        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Practice Locked Cache", new Vector3(-4.8f, 0.1f, 24f), "origin_forest", false, 0.12f, 18, OrnateChestPrefab);
        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Too-Quiet Cache", new Vector3(10.8f, 0.1f, 24.4f), "origin_forest", true, 0.18f, 14, ChestPrefab);
        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Frost Trial Cache", new Vector3(-8.2f, 0.1f, 62f), "region_ice_north", false, 0.24f, 24, OrnateChestPrefab);
        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Ember Temper Cache", new Vector3(58.5f, 0.1f, 11.2f), "region_fire_east", false, 0.34f, 30, OrnateChestPrefab);
        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Auralith Seed Cache", new Vector3(8.6f, 0.1f, -58.2f), "region_jungle_south", false, 0.28f, 26, ChestPrefab);
        YourQuestTutorialWorldHelpers.CreateLockpickChest(root.transform, "Tideglass Coffer", new Vector3(-58.5f, 0.1f, -8.8f), "region_water_west", false, 0.32f, 28, OrnateChestPrefab);

        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(-5.2f, 1f, 41.5f), "tutorial_echoes", "origin_forest", "Training Echo Wisp", 1, string.Empty, new Color(0.78f, 0.66f, 0.42f, 1f), new Color(0.94f, 0.86f, 0.62f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(-4.8f, 1f, 58.5f), "frost_wilds", "region_ice_north", "Frostglass Spider", 1, SpiderPrefab, new Color(0.52f, 0.82f, 0.88f, 1f), new Color(0.82f, 0.95f, 1f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(6.4f, 1f, 63.2f), "frost_wilds", "region_ice_north", "Frostcap Lurker", 1, MushroomMonsterPrefab, new Color(0.46f, 0.72f, 0.82f, 1f), new Color(0.82f, 0.95f, 1f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(55f, 1f, -4.2f), "ember_wilds", "region_fire_east", "Ember Imp Scout", 1, DemonPrefab, new Color(0.90f, 0.38f, 0.20f, 1f), new Color(1f, 0.74f, 0.36f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(64f, 1f, 8.5f), "ember_wilds", "region_fire_east", "Cinder Drake Whelp", 1, DragonPrefab, new Color(0.74f, 0.28f, 0.18f, 1f), new Color(1f, 0.70f, 0.32f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(-5.6f, 1f, -52.5f), "verdant_wilds", "region_jungle_south", "Rootbound Sprout", 1, PlantMonsterPrefab, new Color(0.28f, 0.62f, 0.32f, 1f), new Color(0.72f, 0.86f, 0.42f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(6.6f, 1f, -61.5f), "verdant_wilds", "region_jungle_south", "Sporecap Bulwark", 1, MushroomMonsterPrefab, new Color(0.34f, 0.62f, 0.34f, 1f), new Color(0.80f, 0.94f, 0.44f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(-54.5f, 1f, 9.2f), "tide_wilds", "region_water_west", "Tideglass Skitter", 1, SpiderPrefab, new Color(0.32f, 0.70f, 0.86f, 1f), new Color(0.72f, 0.92f, 1f, 1f));
        YourQuestTutorialWorldHelpers.CreateSpawner(root.transform, new Vector3(-63.5f, 1f, -4.8f), "tide_wilds", "region_water_west", "Brinecap Lurker", 1, MushroomMonsterPrefab, new Color(0.24f, 0.54f, 0.62f, 1f), new Color(0.58f, 0.88f, 0.94f, 1f));
    }

    private void BuildPlayer()
    {
        GameObject player = new GameObject("Player");
        DontDestroyOnLoad(player);
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1.25f, -2.2f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.38f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.minMoveDistance = 0f;

        PlayerProfile profile = player.AddComponent<PlayerProfile>();
        ActionRecorder recorder = player.AddComponent<ActionRecorder>();
        player.AddComponent<PlayerLocationReporter>();
        YQInvestorVitals vitals = player.AddComponent<YQInvestorVitals>();
        YQInvestorCombat combat = player.AddComponent<YQInvestorCombat>();
        combat.interactRange = 2.55f;
        combat.interactAimRadius = 0.02f;
        YQInvestorPlayerMotor motor = player.AddComponent<YQInvestorPlayerMotor>();
        player.AddComponent<YQPlayerEquipmentVisual>();

        Transform pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0f, 1.64f, 0.04f);

        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
        }
        cam.transform.position = player.transform.position + new Vector3(0f, 1.6f, -3.5f);

        motor.cameraPivot = pivot;
        motor.playerCamera = cam;
        motor.actionRecorder = recorder;
        motor.vitals = vitals;
        motor.cameraPivotLocalPosition = new Vector3(0f, 1.64f, 0.04f);
        motor.firstPersonCameraLocalOffset = new Vector3(0f, 0.03f, 0.03f);
        motor.thirdPersonDistance = 4.15f;
        motor.thirdPersonShoulderOffset = new Vector3(0.58f, 0.12f, 0f);
        motor.thirdPersonLookAtOffset = new Vector3(0.18f, 0.10f, 0.34f);
        motor.thirdPersonPositionSharpness = 9.5f;
        motor.thirdPersonRotationSharpness = 13.5f;
        combat.gameObject.name = combat.gameObject.name;
        profile.gameObject.name = profile.gameObject.name;
    }

    private static void CreateRuntimeTerrainOrUseSaved(Transform parent)
    {
        if (GameObject.Find("YQ_PlaySafe_EditableTerrain_HeightmapSplatmap") != null)
            return;

        const int heightResolution = 129;
        const int alphaResolution = 128;
        const float terrainSize = 296f;
        const float terrainHalf = terrainSize * 0.5f;
        const float terrainHeight = 34f;
        const float terrainBaseY = -0.22f;
        TerrainData terrainData = new TerrainData
        {
            heightmapResolution = heightResolution,
            alphamapResolution = alphaResolution,
            size = new Vector3(terrainSize, terrainHeight, terrainSize)
        };

        terrainData.terrainLayers = new[]
        {
            CreateRuntimeTerrainLayer("RuntimeForestMoss", new Color(0.17f, 0.30f, 0.18f, 1f), new Color(0.30f, 0.44f, 0.25f, 1f), 7f),
            CreateRuntimeTerrainLayer("RuntimePackedTrail", new Color(0.27f, 0.24f, 0.19f, 1f), new Color(0.42f, 0.36f, 0.27f, 1f), 5f),
            CreateRuntimeTerrainLayer("RuntimeFrostGlass", new Color(0.50f, 0.76f, 0.88f, 1f), new Color(0.82f, 0.96f, 1f, 1f), 8f),
            CreateRuntimeTerrainLayer("RuntimeCinderSoil", new Color(0.24f, 0.21f, 0.18f, 1f), new Color(0.42f, 0.36f, 0.28f, 1f), 6f),
            CreateRuntimeTerrainLayer("RuntimeTideStone", new Color(0.10f, 0.25f, 0.34f, 1f), new Color(0.22f, 0.43f, 0.54f, 1f), 7f),
            CreateRuntimeTerrainLayer("RuntimeRidgeStone", new Color(0.23f, 0.25f, 0.23f, 1f), new Color(0.42f, 0.43f, 0.38f, 1f), 10f)
        };

        float[,] heights = new float[heightResolution, heightResolution];
        for (int y = 0; y < heightResolution; y++)
        {
            for (int x = 0; x < heightResolution; x++)
            {
                float nx = x / (float)(heightResolution - 1);
                float nz = y / (float)(heightResolution - 1);
                float wx = nx * terrainSize - terrainHalf;
                float wz = nz * terrainSize - terrainHalf;
                heights[y, x] = SampleTutorialTerrainHeight01(wx, wz);
            }
        }
        terrainData.SetHeights(0, 0, heights);

        float[,,] splats = new float[alphaResolution, alphaResolution, terrainData.terrainLayers.Length];
        for (int y = 0; y < alphaResolution; y++)
        {
            for (int x = 0; x < alphaResolution; x++)
            {
                float wx = x / (float)(alphaResolution - 1) * terrainSize - terrainHalf;
                float wz = y / (float)(alphaResolution - 1) * terrainSize - terrainHalf;
                float[] weights = { 1f, 0f, 0f, 0f, 0f, 0f };
                float path = TutorialPathFlatten(wx, wz);
                float height01 = SampleTutorialTerrainHeight01(wx, wz);
                float mountain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.11f, 0.31f, height01));
                weights[0] += Mathf.Clamp01(1f - mountain) * 0.65f;
                weights[1] += path * 3.3f;
                weights[5] += mountain * 3.2f;
                if (wz > 44f)
                    weights[2] += Mathf.InverseLerp(44f, terrainHalf, wz) * 2.7f;
                if (wx > 44f)
                    weights[3] += Mathf.InverseLerp(44f, terrainHalf, wx) * 2.7f;
                if (wx < -44f)
                    weights[4] += Mathf.InverseLerp(-44f, -terrainHalf, wx) * 2.7f;
                if (wz < -44f)
                    weights[0] += Mathf.InverseLerp(-44f, -terrainHalf, wz) * 1.7f;

                float total = 0f;
                for (int layer = 0; layer < weights.Length; layer++)
                    total += weights[layer];
                for (int layer = 0; layer < weights.Length; layer++)
                    splats[y, x, layer] = weights[layer] / Mathf.Max(0.0001f, total);
            }
        }
        terrainData.SetAlphamaps(0, 0, splats);

        GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
        terrainGo.name = "Runtime_EditableTerrain_HeightmapSplatmap";
        terrainGo.transform.SetParent(parent, false);
        terrainGo.transform.position = new Vector3(-terrainHalf, terrainBaseY, -terrainHalf);

        Terrain terrain = terrainGo.GetComponent<Terrain>();
        if (terrain != null)
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 9f;
            terrain.basemapDistance = 120f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static float SampleTutorialTerrainHeight01(float wx, float wz)
    {
        float centerDistance = Mathf.Sqrt(wx * wx + wz * wz);
        float nx = (wx + 148f) / 296f;
        float nz = (wz + 148f) / 296f;
        float broadRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(44f, 132f, centerDistance)) * 0.075f;
        float mountainRing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(82f, 148f, centerDistance)) * 0.17f;
        float wave = Mathf.PerlinNoise(nx * 3.4f + 2.1f, nz * 3.4f + 6.7f) * 0.026f;
        wave += Mathf.PerlinNoise(nx * 8.2f + 19.4f, nz * 8.2f + 3.5f) * 0.010f;
        float peaks =
            MountainPeak(wx, wz, -118f, 112f, 38f, 0.23f) +
            MountainPeak(wx, wz, 118f, 104f, 42f, 0.20f) +
            MountainPeak(wx, wz, -126f, -112f, 42f, 0.21f) +
            MountainPeak(wx, wz, 124f, -116f, 46f, 0.19f) +
            MountainPeak(wx, wz, 0f, 138f, 34f, 0.15f) +
            MountainPeak(wx, wz, 138f, 0f, 38f, 0.13f) +
            MountainPeak(wx, wz, -138f, 0f, 38f, 0.13f);
        float raw = Mathf.Clamp01(0.009f + broadRise + mountainRing + wave + peaks);
        return Mathf.Lerp(raw, 0.006f, TutorialPathFlatten(wx, wz));
    }

    private static float MountainPeak(float wx, float wz, float px, float pz, float radius, float height)
    {
        float dx = (wx - px) / Mathf.Max(1f, radius);
        float dz = (wz - pz) / Mathf.Max(1f, radius);
        return Mathf.Exp(-(dx * dx + dz * dz)) * height;
    }

    private static float TutorialPathFlatten(float wx, float wz)
    {
        float northSouthRoad = Mathf.SmoothStep(1f, 0f, Mathf.Abs(wx) / 8.0f);
        float eastWestRoad = Mathf.SmoothStep(1f, 0f, Mathf.Abs(wz - 4f) / 7.4f);
        float startClearing = CircularFlatten(wx, wz, 0f, 8f, 20f);
        float trainingYard = CircularFlatten(wx, wz, 0f, 42f, 24f);
        float northTrial = CircularFlatten(wx, wz, 0f, 64f, 21f);
        float eastTrial = CircularFlatten(wx, wz, 56f, 4f, 20f);
        float southTrial = CircularFlatten(wx, wz, 0f, -55f, 20f);
        float westTrial = CircularFlatten(wx, wz, -56f, 4f, 20f);
        float flatten = Mathf.Max(northSouthRoad * 0.92f, eastWestRoad * 0.90f);
        flatten = Mathf.Max(flatten, startClearing);
        flatten = Mathf.Max(flatten, trainingYard * 0.96f);
        flatten = Mathf.Max(flatten, northTrial * 0.94f);
        flatten = Mathf.Max(flatten, eastTrial * 0.94f);
        flatten = Mathf.Max(flatten, southTrial * 0.94f);
        flatten = Mathf.Max(flatten, westTrial * 0.94f);
        return Mathf.Clamp01(flatten);
    }

    private static float CircularFlatten(float wx, float wz, float cx, float cz, float radius)
    {
        float dx = wx - cx;
        float dz = wz - cz;
        return Mathf.SmoothStep(1f, 0f, Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(1f, radius));
    }

    private static TerrainLayer CreateRuntimeTerrainLayer(string name, Color baseColor, Color detailColor, float tileSize)
    {
        TerrainLayer layer = new TerrainLayer();
        layer.name = name;
        layer.diffuseTexture = CreateRuntimeTerrainTexture(name + "_Texture", baseColor, detailColor);
        layer.tileSize = Vector2.one * Mathf.Max(1f, tileSize);
        layer.smoothness = 0.12f;
        layer.metallic = 0f;
        return layer;
    }

    private static Texture2D CreateRuntimeTerrainTexture(string name, Color baseColor, Color detailColor)
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
        texture.name = name;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float grain = Mathf.PerlinNoise(x * 0.055f, y * 0.055f) * 0.46f;
                grain += Mathf.PerlinNoise(x * 0.19f + 31.5f, y * 0.19f + 12.7f) * 0.18f;
                float fine = Mathf.PerlinNoise(x * 0.53f + 4.2f, y * 0.53f + 9.8f) * 0.08f;
                texture.SetPixel(x, y, Color.Lerp(baseColor, detailColor, Mathf.Clamp01(grain + fine)));
            }
        }

        texture.Apply(true, false);
        return texture;
    }

    private static void CreateGround(Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.position = position;
        ground.transform.localScale = scale;
        Renderer renderer = ground.GetComponent<Renderer>();
        YQInvestorRuntimeVisuals.SetRendererColor(renderer, color);
        YQVisualStabilityDirector.StabilizeHierarchy(ground);
    }

    private static void CreateBoundary(Transform parent, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Boundary";
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        Renderer renderer = wall.GetComponent<Renderer>();
        YQInvestorRuntimeVisuals.SetRendererColor(renderer, new Color(0.16f, 0.17f, 0.19f, 1f));
    }

    private static void CreateRoadMarker(Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "RoadMarker";
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
        marker.transform.localScale = scale;
        Renderer renderer = marker.GetComponent<Renderer>();
        YQInvestorRuntimeVisuals.SetRendererColor(renderer, color);
    }

    private static void CreateTraversalBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(block.GetComponent<Renderer>(), color);
    }

    private static void CreateTutorialFence(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        CreateTraversalBlock(parent, name, position, scale, color);
    }

    private static void CreateEquipmentBench(Transform parent, Vector3 origin)
    {
        Color timber = new Color(0.30f, 0.21f, 0.13f, 1f);
        Color darkIron = new Color(0.18f, 0.18f, 0.16f, 1f);

        CreateTraversalBlock(parent, "Gear_Bench_Table", origin + new Vector3(0f, 0.36f, 0f), new Vector3(6.4f, 0.28f, 1.75f), timber);
        CreateTraversalBlock(parent, "Gear_Bench_LeftLeg", origin + new Vector3(-2.65f, 0.17f, -0.55f), new Vector3(0.22f, 0.34f, 0.22f), timber);
        CreateTraversalBlock(parent, "Gear_Bench_RightLeg", origin + new Vector3(2.65f, 0.17f, 0.55f), new Vector3(0.22f, 0.34f, 0.22f), timber);
        CreateTraversalBlock(parent, "Gear_Bench_BackRail", origin + new Vector3(0f, 0.72f, 0.98f), new Vector3(6.8f, 0.18f, 0.18f), darkIron);
        CreateTraversalBlock(parent, "Gear_Bench_LeftRack", origin + new Vector3(-3.15f, 0.98f, 0.98f), new Vector3(0.18f, 0.9f, 0.18f), darkIron);
        CreateTraversalBlock(parent, "Gear_Bench_RightRack", origin + new Vector3(3.15f, 0.98f, 0.98f), new Vector3(0.18f, 0.9f, 0.18f), darkIron);
    }

    private static void CreateLessonBeacon(Transform parent, string name, Vector3 position, string displayName, Color color, string[] tags)
    {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        GameObject baseStone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseStone.name = name + "_Base";
        baseStone.transform.SetParent(root.transform, false);
        baseStone.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        baseStone.transform.localScale = new Vector3(0.78f, 0.12f, 0.78f);
        YQInvestorRuntimeVisuals.SetRendererColor(baseStone.GetComponent<Renderer>(), new Color(0.22f, 0.23f, 0.21f, 1f));
        DisableCollider(baseStone);

        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = name + "_Pillar";
        pillar.transform.SetParent(root.transform, false);
        pillar.transform.localPosition = new Vector3(0f, 0.68f, 0f);
        pillar.transform.localScale = new Vector3(0.26f, 0.56f, 0.26f);
        YQInvestorRuntimeVisuals.SetRendererColor(pillar.GetComponent<Renderer>(), new Color(0.18f, 0.18f, 0.17f, 1f));
        DisableCollider(pillar);

        GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cap.name = name + "_Glow";
        cap.transform.SetParent(root.transform, false);
        cap.transform.localPosition = new Vector3(0f, 1.32f, 0f);
        cap.transform.localScale = Vector3.one * 0.34f;
        YQInvestorRuntimeVisuals.SetRendererColor(cap.GetComponent<Renderer>(), color);
        DisableCollider(cap);

        GameObject lightGo = new GameObject(name + "_Light");
        lightGo.transform.SetParent(root.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.32f, 0f);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 0.22f;
        light.range = 2.4f;
        light.shadows = LightShadows.None;

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = "tutorial_" + NormalizeIdentifier(name);
        info.displayName = displayName;
        info.factionId = "tutorial";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = tags ?? new[] { "tutorial", "landmark" };
    }

    private static void DisableCollider(GameObject go)
    {
        Collider collider = go != null ? go.GetComponent<Collider>() : null;
        if (collider != null)
            Destroy(collider);
    }

    private static void CreatePracticeLockGate(Transform parent, string displayName, Vector3 position, Vector3 scale, float difficulty)
    {
        GameObject gate = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gate.name = displayName;
        gate.transform.SetParent(parent, false);
        gate.transform.position = position;
        gate.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(gate.GetComponent<Renderer>(), new Color(0.24f, 0.18f, 0.11f, 1f));

        YQLockpickableDoor door = gate.AddComponent<YQLockpickableDoor>();
        door.displayName = displayName;
        door.regionId = "origin_forest";
        door.locked = true;
        door.lockDifficulty = Mathf.Clamp01(difficulty);
        door.openEuler = new Vector3(0f, -86f, 0f);

        EntityInfo info = gate.AddComponent<EntityInfo>();
        info.entityId = "tutorial_practice_lock_gate";
        info.displayName = displayName;
        info.factionId = "tutorial";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = new[] { "lockpick", "door", "gate", "practice", "tutorial" };
    }

    private static void CreateTutorialPickup(Transform parent, string name, Vector3 position, InventoryItemRecord item, Color color, string[] tags)
    {
        GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickup.name = name;
        pickup.transform.SetParent(parent, false);
        pickup.transform.position = position;
        pickup.transform.localScale = Vector3.one * 0.52f;
        YQInvestorRuntimeVisuals.SetRendererColor(pickup.GetComponent<Renderer>(), color);

        YQInvestorWorldPickup worldPickup = pickup.AddComponent<YQInvestorWorldPickup>();
        worldPickup.Initialize(item, 0);

        EntityInfo info = pickup.AddComponent<EntityInfo>();
        info.entityId = "tutorial_" + NormalizeIdentifier(name);
        info.displayName = item != null && !string.IsNullOrWhiteSpace(item.displayName) ? item.displayName : name;
        info.factionId = "tutorial";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = tags ?? new[] { "pickup", "tutorial" };
    }

    private static string NormalizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "entity";

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static InventoryItemRecord CreateTutorialItem(
        string templateId,
        string displayName,
        string itemType,
        string equipSlot,
        string description,
        int quantity,
        int attackBonus,
        int defenseBonus,
        int healthBonus,
        int staminaBonus,
        int manaBonus,
        int restoreMana,
        float moveSpeedBonus,
        int healAmount)
    {
        return new InventoryItemRecord
        {
            itemId = System.Guid.NewGuid().ToString("N"),
            templateId = templateId,
            displayName = displayName,
            itemType = itemType,
            equipSlot = equipSlot,
            rarity = "Tutorial",
            description = description,
            quantity = Mathf.Max(1, quantity),
            stackable = string.Equals(itemType, "consumable", System.StringComparison.OrdinalIgnoreCase),
            powerScore = Mathf.Max(attackBonus, defenseBonus, healthBonus, staminaBonus, manaBonus, restoreMana, healAmount),
            attackBonus = attackBonus,
            defenseBonus = defenseBonus,
            healthBonus = healthBonus,
            staminaBonus = staminaBonus,
            manaBonus = manaBonus,
            restoreManaAmount = restoreMana,
            restoreStaminaAmount = staminaBonus,
            healAmount = healAmount,
            moveSpeedBonus = moveSpeedBonus,
            iconKey = "icon_tutorial",
            prefabKey = string.Empty,
            effectKey = itemType + ":tutorial",
            familyKey = "tutorial_main",
            generatedAtUnixString = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()
        };
    }

    private static void CreateAmbientLoop(Transform parent, string name, string clipPath, Vector3 position, float volume, float pitch, float minDistance, float maxDistance)
    {
        AudioClip clip = LoadAudioClip(clipPath);
        if (clip == null || volume <= 0.001f)
            return;

        GameObject audioGo = new GameObject(name);
        audioGo.transform.SetParent(parent, false);
        audioGo.transform.position = position;
        AudioSource source = audioGo.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = true;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
        source.spatialBlend = minDistance > 0f ? 0.78f : 0.35f;
        source.dopplerLevel = 0f;
        source.priority = 190;
        source.minDistance = Mathf.Max(0f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 1f, maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        if (clip.length > 0.25f)
            source.time = UnityEngine.Random.Range(0f, clip.length * 0.85f);
        source.Play();
    }

    private static AudioClip LoadAudioClip(string clipPath)
    {
#if UNITY_EDITOR
        string normalizedPath = string.IsNullOrWhiteSpace(clipPath) ? string.Empty : clipPath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return null;
        if (s_audioClipCache.TryGetValue(normalizedPath, out AudioClip cached))
            return cached;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(normalizedPath);
        if (clip != null)
        {
            s_audioClipCache[normalizedPath] = clip;
            return clip;
        }

        string leaf = normalizedPath;
        int slash = leaf.LastIndexOf('/');
        if (slash >= 0)
            leaf = leaf.Substring(slash + 1);
        int dot = leaf.LastIndexOf('.');
        string wantedName = dot > 0 ? leaf.Substring(0, dot) : leaf;
        if (string.IsNullOrWhiteSpace(wantedName))
        {
            s_audioClipCache[normalizedPath] = null;
            return null;
        }

        string[] guids = AssetDatabase.FindAssets(wantedName + " t:AudioClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace('\\', '/');
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                s_audioClipCache[normalizedPath] = clip;
                return clip;
            }
        }
        s_audioClipCache[normalizedPath] = null;
#endif
        return null;
    }

    private static void CreateTutorialGate(Transform parent, string name, Vector3 center, bool spanX, Color color)
    {
        Vector3 leftOffset = spanX ? new Vector3(-4.2f, 0f, 0f) : new Vector3(0f, 0f, -4.2f);
        Vector3 rightOffset = spanX ? new Vector3(4.2f, 0f, 0f) : new Vector3(0f, 0f, 4.2f);
        Vector3 lintelScale = spanX ? new Vector3(9.2f, 0.5f, 0.5f) : new Vector3(0.5f, 0.5f, 9.2f);

        CreateTraversalBlock(parent, name + "_LeftPost", center + leftOffset + new Vector3(0f, 1.35f, 0f), new Vector3(0.5f, 2.7f, 0.5f), color);
        CreateTraversalBlock(parent, name + "_RightPost", center + rightOffset + new Vector3(0f, 1.35f, 0f), new Vector3(0.5f, 2.7f, 0.5f), color);
        CreateTraversalBlock(parent, name + "_Lintel", center + new Vector3(0f, 2.85f, 0f), lintelScale, color);
    }

    private static void CreateTutorialSign(Transform parent, string name, string label, Vector3 position, float yaw)
    {
        string displayLabel = WrapSignLabel(label, 12);
        int lineCount = CountLines(displayLabel);
        float boardHeight = Mathf.Clamp(0.30f + lineCount * 0.22f, 0.58f, 1.02f);
        float boardWidth = Mathf.Clamp(1.22f + Mathf.Min(displayLabel.Length, 18) * 0.075f, 1.55f, 2.55f);
        float textSize = lineCount <= 1 ? 0.048f : lineCount <= 2 ? 0.042f : 0.036f;

        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
        post.name = name + "_Post";
        post.transform.SetParent(root.transform, false);
        post.transform.localPosition = new Vector3(0f, 0.52f, 0f);
        post.transform.localScale = new Vector3(0.10f, 1.04f, 0.10f);
        YQInvestorRuntimeVisuals.SetRendererColor(post.GetComponent<Renderer>(), new Color(0.30f, 0.22f, 0.14f, 1f));
        DisableCollider(post);

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = name + "_Board";
        board.transform.SetParent(root.transform, false);
        board.transform.localPosition = new Vector3(0f, 1.15f, 0f);
        board.transform.localScale = new Vector3(boardWidth, boardHeight, 0.08f);
        YQInvestorRuntimeVisuals.SetRendererColor(board.GetComponent<Renderer>(), new Color(0.18f, 0.16f, 0.12f, 1f));
        DisableCollider(board);

        GameObject textGo = new GameObject(name + "_Text");
        textGo.transform.SetParent(root.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 1.155f, -0.058f);
        textGo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        TextMesh text = textGo.AddComponent<TextMesh>();
        text.text = displayLabel;
        text.fontSize = 24;
        text.characterSize = textSize;
        text.lineSpacing = 0.74f;
        text.alignment = TextAlignment.Center;
        text.anchor = TextAnchor.MiddleCenter;
        text.color = new Color(0.86f, 0.82f, 0.66f, 1f);

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = "tutorial_" + NormalizeIdentifier(name);
        info.displayName = label;
        info.factionId = "tutorial";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = new[] { "sign", "tutorial", label };
    }

    private static string WrapSignLabel(string label, int maxCharsPerLine)
    {
        if (string.IsNullOrWhiteSpace(label))
            return string.Empty;

        string[] words = label.Trim().Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
            return label.Trim();

        System.Text.StringBuilder builder = new System.Text.StringBuilder(label.Length + 8);
        int lineLength = 0;
        for (int i = 0; i < words.Length; i++)
        {
            string word = words[i];
            if (lineLength > 0 && lineLength + 1 + word.Length > maxCharsPerLine)
            {
                builder.Append('\n');
                lineLength = 0;
            }
            else if (lineLength > 0)
            {
                builder.Append(' ');
                lineLength++;
            }

            builder.Append(word);
            lineLength += word.Length;
        }

        return builder.ToString();
    }

    private static int CountLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 1;

        int lines = 1;
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\n')
                lines++;
        }

        return lines;
    }

    private static void CreateRegion(Transform parent, string name, Vector3 position, Vector3 scale, string regionId, string regionName, List<string> tags)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = scale;
        RegionVolume volume = go.AddComponent<RegionVolume>();
        volume.regionId = regionId;
        volume.regionName = regionName;
        volume.tags = tags;
    }

    private static void CreateHut(Transform parent)
    {
        GameObject hut = new GameObject("Origin_Hut");
        hut.transform.SetParent(parent, false);

        CreateHutPart(hut.transform, "Hut_Floor", new Vector3(0f, 0.08f, 0f), new Vector3(7.4f, 0.16f, 6.2f), new Color(0.34f, 0.23f, 0.14f, 1f));
        CreateHutPart(hut.transform, "Hut_BackWall", new Vector3(0f, 1.3f, -3.05f), new Vector3(7.4f, 2.6f, 0.25f), new Color(0.40f, 0.27f, 0.16f, 1f));
        CreateHutPart(hut.transform, "Hut_LeftWall", new Vector3(-3.7f, 1.3f, 0f), new Vector3(0.25f, 2.6f, 6.2f), new Color(0.38f, 0.25f, 0.15f, 1f));
        CreateHutPart(hut.transform, "Hut_RightWall", new Vector3(3.7f, 1.3f, 0f), new Vector3(0.25f, 2.6f, 6.2f), new Color(0.38f, 0.25f, 0.15f, 1f));
        CreateHutPart(hut.transform, "Hut_FrontLeft", new Vector3(-2.7f, 1.3f, 3.05f), new Vector3(2.0f, 2.6f, 0.25f), new Color(0.40f, 0.27f, 0.16f, 1f));
        CreateHutPart(hut.transform, "Hut_FrontRight", new Vector3(2.7f, 1.3f, 3.05f), new Vector3(2.0f, 2.6f, 0.25f), new Color(0.40f, 0.27f, 0.16f, 1f));
        CreateHutPart(hut.transform, "Hut_Roof", new Vector3(0f, 2.85f, 0f), new Vector3(8.0f, 0.38f, 7.0f), new Color(0.18f, 0.16f, 0.12f, 1f));
        CreateHutPart(hut.transform, "Hut_Bed", new Vector3(-2.2f, 0.42f, -1.9f), new Vector3(1.8f, 0.48f, 1.1f), new Color(0.25f, 0.30f, 0.34f, 1f));
        CreateHutPart(hut.transform, "Hut_Table", new Vector3(2.1f, 0.55f, -1.3f), new Vector3(1.2f, 0.30f, 0.85f), new Color(0.30f, 0.20f, 0.12f, 1f));

        GameObject door = CreateHutPart(hut.transform, "Hut_Door", new Vector3(0f, 1.1f, 3.12f), new Vector3(1.3f, 2.2f, 0.14f), new Color(0.24f, 0.15f, 0.09f, 1f));
        YQLockpickableDoor lockpickDoor = door.AddComponent<YQLockpickableDoor>();
        lockpickDoor.displayName = "Hut Door";
        lockpickDoor.regionId = "origin_forest";
        lockpickDoor.locked = false;
        lockpickDoor.openEuler = new Vector3(0f, -86f, 0f);
    }

    private static GameObject CreateHutPart(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(part.GetComponent<Renderer>(), color);
        return part;
    }

    private static void CreateForestScatter(Transform parent)
    {
        YourQuestTutorialWorldHelpers.CreateForestScatter(parent);
    }

    private static void CreateRegionDressing(Transform parent)
    {
        YourQuestTutorialWorldHelpers.CreateRegionDressing(parent);
    }

    private static void CreateLockpickChest(Transform parent, string displayName, Vector3 position, string regionId, bool mimic, float difficulty, int gold, string prefabPath)
    {
        GameObject chest = CreateAssetProp(parent, displayName, prefabPath, position, Vector3.zero, Vector3.one, new Color(0.48f, 0.30f, 0.16f, 1f), PrimitiveType.Cube, true);
        if (chest == null)
            return;

        chest.name = displayName;
        YQLockpickableLoot loot = chest.GetComponent<YQLockpickableLoot>();
        if (loot == null)
            loot = chest.AddComponent<YQLockpickableLoot>();
        loot.displayName = displayName;
        loot.regionId = regionId;
        loot.mimic = mimic;
        loot.revealedMimicPrefabPath = mimic ? MimicPrefab : string.Empty;
        loot.locked = true;
        loot.lockDifficulty = difficulty;
        loot.gold = gold;
        loot.PrimeVisualState();
    }

    private static GameObject CreateAssetProp(
        Transform parent,
        string name,
        string prefabPath,
        Vector3 position,
        Vector3 euler,
        Vector3 scale,
        Color fallbackColor,
        PrimitiveType fallbackType,
        bool keepColliders)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && !string.IsNullOrWhiteSpace(prefabPath))
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    instance.name = name;
                    instance.transform.SetParent(parent, false);
                    instance.transform.position = position;
                    instance.transform.rotation = Quaternion.Euler(euler);
                    instance.transform.localScale = scale;
                    PrepareWorldProp(instance, keepColliders);
                    return instance;
                }
            }
        }
#endif

        GameObject fallback = GameObject.CreatePrimitive(fallbackType);
        fallback.name = name;
        fallback.transform.SetParent(parent, false);
        fallback.transform.position = position;
        fallback.transform.rotation = Quaternion.Euler(euler);
        fallback.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(fallback.GetComponent<Renderer>(), fallbackColor);
        if (!keepColliders)
        {
            Collider collider = fallback.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);
        }
        return fallback;
    }

    private static void PrepareWorldProp(GameObject root, bool keepColliders)
    {
        if (root == null)
            return;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                bodies[i].isKinematic = true;
        }

        if (!keepColliders)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
        }

        YQRuntimeUrpMaterialRepair.RepairHierarchy(root);
        YQVisualStabilityDirector.StabilizeHierarchy(root);
    }

    private static void CreateNpc(Transform parent, string npcId, string displayName, Vector3 position, string factionId, string[] tags, string line)
    {
        GameObject root = new GameObject(displayName);
        root.transform.SetParent(parent, false);
        root.transform.position = position;

        Color bodyColor = HasTag(tags, "warden")
            ? new Color(0.46f, 0.60f, 0.70f, 1f)
            : new Color(0.86f, 0.76f, 0.45f, 1f);
        Color accentColor = HasTag(tags, "warden")
            ? new Color(0.70f, 0.88f, 1f, 1f)
            : HasTag(tags, "east")
                ? new Color(1f, 0.56f, 0.26f, 1f)
                : HasTag(tags, "south")
                    ? new Color(0.56f, 0.92f, 0.44f, 1f)
                    : HasTag(tags, "west")
                        ? new Color(0.46f, 0.82f, 0.94f, 1f)
            : new Color(0.96f, 0.84f, 0.38f, 1f);

        CapsuleCollider interactCollider = root.AddComponent<CapsuleCollider>();
        interactCollider.height = 1.9f;
        interactCollider.radius = 0.42f;
        interactCollider.center = new Vector3(0f, 0.95f, 0f);

        GameObject plinth = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plinth.name = displayName + "_Base";
        plinth.transform.SetParent(root.transform, false);
        plinth.transform.localPosition = new Vector3(0f, 0.06f, 0f);
        plinth.transform.localScale = new Vector3(0.72f, 0.06f, 0.72f);
        YQInvestorRuntimeVisuals.SetRendererColor(plinth.GetComponent<Renderer>(), new Color(0.18f, 0.17f, 0.14f, 1f));
        DisableCollider(plinth);

        bool importedVisual = TryCreateImportedNpcVisual(root.transform, displayName, bodyColor, accentColor);
        if (!importedVisual)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = displayName + "_Body";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, 1f, 0f);
            body.transform.localScale = new Vector3(0.72f, 0.92f, 0.54f);
            YQInvestorRuntimeVisuals.SetRendererColor(body.GetComponent<Renderer>(), bodyColor);
            DisableCollider(body);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = displayName + "_Head";
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.76f, 0f);
            head.transform.localScale = new Vector3(0.34f, 0.34f, 0.34f);
            YQInvestorRuntimeVisuals.SetRendererColor(head.GetComponent<Renderer>(), new Color(0.76f, 0.62f, 0.50f, 1f));
            DisableCollider(head);
        }

        GameObject accent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        accent.name = displayName + "_RoleAccent";
        accent.transform.SetParent(root.transform, false);
        accent.transform.localPosition = new Vector3(0f, 1.24f, -0.38f);
        accent.transform.localScale = new Vector3(0.72f, 0.12f, 0.06f);
        YQInvestorRuntimeVisuals.SetRendererColor(accent.GetComponent<Renderer>(), accentColor);
        DisableCollider(accent);

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = npcId;
        info.displayName = displayName;
        info.factionId = factionId;
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = tags;

        NpcDialogueAgent agent = root.AddComponent<NpcDialogueAgent>();
        agent.npcId = npcId;
        agent.npcName = displayName;
        agent.personaSummary = BuildNpcPersona(displayName, tags);
        agent.tagsOverride.Clear();
        if (tags != null)
        {
            for (int i = 0; i < tags.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(tags[i]) && !agent.tagsOverride.Contains(tags[i]))
                    agent.tagsOverride.Add(tags[i]);
            }
        }
        if (agent.GetRecentTurnsSnapshot(1).Count == 0)
            agent.CommitNpcLine(line);
    }

    private static bool TryCreateImportedNpcVisual(Transform parent, string displayName, Color bodyColor, Color accentColor)
    {
#if UNITY_EDITOR
        GameObject prefab = string.IsNullOrWhiteSpace(HumanMalePrefab) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(HumanMalePrefab);
        if (prefab == null)
            return false;

        GameObject avatar = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (avatar == null)
            return false;

        avatar.name = displayName + "_Avatar";
        avatar.transform.SetParent(parent, false);
        avatar.transform.localPosition = Vector3.zero;
        avatar.transform.localRotation = Quaternion.identity;
        avatar.transform.localScale = Vector3.one;
        PrepareNpcVisual(avatar);
        NormalizeNpcVisual(avatar, parent.position, 1.76f);

        GameObject sash = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sash.name = displayName + "_MentorSash";
        sash.transform.SetParent(parent, false);
        sash.transform.localPosition = new Vector3(0f, 1.18f, -0.42f);
        sash.transform.localScale = new Vector3(0.72f, 0.10f, 0.05f);
        YQInvestorRuntimeVisuals.SetRendererColor(sash.GetComponent<Renderer>(), accentColor);
        DisableCollider(sash);

        return true;
#else
        return false;
#endif
    }

    private static void PrepareNpcVisual(GameObject root)
    {
        if (root == null)
            return;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                bodies[i].isKinematic = true;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
        }

        YQRuntimeUrpMaterialRepair.RepairHierarchy(root);
        YQVisualStabilityDirector.StabilizeHierarchy(root);
    }

    private static void NormalizeNpcVisual(GameObject root, Vector3 anchor, float targetHeight)
    {
        if (root == null || !TryGetRendererBounds(root, out Bounds bounds))
            return;

        float height = Mathf.Max(0.1f, bounds.size.y);
        float scale = Mathf.Clamp(Mathf.Max(0.1f, targetHeight) / height, 0.08f, 2.2f);
        root.transform.localScale *= scale;

        if (!TryGetRendererBounds(root, out bounds))
            return;

        Vector3 offset = new Vector3(anchor.x - bounds.center.x, anchor.y - bounds.min.y, anchor.z - bounds.center.z);
        root.transform.position += offset;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root != null ? root.GetComponentsInChildren<Renderer>(true) : Array.Empty<Renderer>();
        bounds = default;
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return initialized;
    }

    private static string BuildNpcPersona(string displayName, string[] tags)
    {
        if (HasTag(tags, "warden"))
            return displayName + " is a terse gate warden who respects proven action, clear choices, and saving before danger. He speaks like a person guarding a real threshold, not a tutorial panel.";
        if (HasTag(tags, "archivist"))
            return displayName + " is a warm but exact archivist of Auralith's old trial grounds. She frames every lesson around what the player personally does under pressure.";
        if (HasTag(tags, "east"))
            return displayName + " is a severe cinder-road mentor who frames fire as a repeatable test of courage, timing, and restraint.";
        if (HasTag(tags, "south"))
            return displayName + " is a root-sibyl of Auralith, the First Green, and treats nature as an ancient precursor force that must still answer the player's choices.";
        if (HasTag(tags, "west"))
            return displayName + " is a tide-road cartographer who studies how the player recovers footing when the world shifts.";
        return displayName + " is a grounded local who answers from lived knowledge and keeps advice concrete.";
    }

    private static bool HasTag(string[] tags, string expected)
    {
        if (tags == null || string.IsNullOrWhiteSpace(expected))
            return false;

        for (int i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i], expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void CreateShrine(Transform parent, Vector3 position, string displayName, Color color)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = displayName;
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.localScale = new Vector3(1.2f, 0.75f, 1.2f);
        Renderer renderer = root.GetComponent<Renderer>();
        YQInvestorRuntimeVisuals.SetRendererColor(renderer, color);
        root.AddComponent<YQInvestorShrine>();

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = "tutorial_" + NormalizeIdentifier(displayName);
        info.displayName = displayName;
        info.factionId = "tutorial";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = new[] { "shrine", "restore", "recover", "tutorial" };
    }

    private static void CreateSpawner(Transform parent, Vector3 position, string factionId, string regionId, string displayName, int count, string prefabPath, Color primary, Color secondary)
    {
        GameObject root = new GameObject(displayName + " Spawner");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        YQInvestorEnemySpawner spawner = root.AddComponent<YQInvestorEnemySpawner>();
        spawner.factionId = factionId;
        spawner.semanticRegionId = regionId;
        spawner.enemyDisplayName = displayName;
        spawner.enemyCount = count;
        spawner.enemyPrefabPath = prefabPath;
        spawner.allowImportedPrefabModelsInPlay = true;
        spawner.spawnRadius = 1.8f;
        spawner.requirePlayerNear = true;
        spawner.playerActivationDistance = string.Equals(regionId, "origin_forest", System.StringComparison.OrdinalIgnoreCase) ? 32f : 38f;
        spawner.playerFarDespawnDistance = spawner.playerActivationDistance + 24f;
        spawner.gatedSpawnRetryInterval = 1.65f;
        spawner.primaryColor = primary;
        spawner.secondaryColor = secondary;
        if (string.Equals(regionId, "origin_forest", System.StringComparison.OrdinalIgnoreCase))
            spawner.requiredCounter = "dialogue:npc_archivist_01";
        spawner.PrimeSpawnGate();
    }

}
