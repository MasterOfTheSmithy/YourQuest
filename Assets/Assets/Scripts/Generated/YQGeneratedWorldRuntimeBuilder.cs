using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public enum YQWorldMaterializationPath
{
    LegacyScatterComparison = 0,
    CompiledWorld = 1
}

/// <summary>
/// Declares which physical world-generation architecture is allowed to run.
/// WG0 keeps the existing builder available only as a measurable comparison
/// while the compiled-world path is constructed behind this boundary.
/// </summary>
public static class YQWorldGenerationArchitecture
{
    // note: Reviewed semantic sites now own settlement materialization; the legacy scatter path remains compiled only as a comparison fallback for development.
    public const YQWorldMaterializationPath ActiveMaterializationPath =
        YQWorldMaterializationPath.CompiledWorld;

    // note: The first golden-master benchmark uses one coherent source family instead of the universal runtime asset pool.
    public const string FirstBenchmarkId =
        "WG0_VIKING_VALLEY_001";

    public const string FirstBenchmarkWorldSeed =
        "YQ-WG0-VIKING-VALLEY-001";

    public const string FirstBenchmarkPrimaryKitTag =
        "medievalvikingvillage";

    public const string FirstBenchmarkSourceRoot =
        "Assets/BefourStudios/MedievalVikingVillage";

    public static bool AllowsLegacyRuntimeBuilder =>
        ActiveMaterializationPath ==
        YQWorldMaterializationPath.LegacyScatterComparison;

    public static bool UsesCompiledWorld =>
        ActiveMaterializationPath ==
        YQWorldMaterializationPath.CompiledWorld;
}

[DisallowMultipleComponent]
public sealed class YQGeneratedWorldRuntimeBuilder : MonoBehaviour
{
    private static bool _initialGenerationLifecycleLocked;

    private static bool _initialGenerationLifecycleLatched;
    private static string _initialGenerationStartingWorldSeed =
    string.Empty;
    private static float _initialGenerationLockStartedAt = -1f;
    private static bool _initialGenerationDeadlineWarningIssued;
    private const float MaximumInitialGenerationLockSeconds = 180f;
    private const float StartupHierarchyFrameBudgetSeconds = 0.0015f;

    private const int MaxSkippedMissingScriptPrefabLogs =
        8;

    private const int MaxSkippedUnsuitableSettlementAssetLogs =
        12;

    private static int _skippedMissingScriptPrefabLogs;

    private static int _skippedUnsuitableSettlementAssetLogs;
    private static Vector3 _generatedOriginSpawnOverride;
    private static bool _hasGeneratedOriginSpawnOverride;
    private static Vector3 _generatedOriginFacingOverride;
    private static bool _hasGeneratedOriginFacingOverride;
    private static readonly Vector3 OriginGoddessSummitOffset =
        new Vector3(30.6f, 0f, 14.6f);
    private static readonly Vector3 OriginWitchHouseOffset =
        new Vector3(15f, 0f, -16f);
    private const string OriginGoddessStatueAssetPath =
        "Assets/HIVEMIND/HDRP/TheMessengerMountain/Art/Prefabs/SM_AngelStatue_02.prefab";

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInitialGenerationGameplayLock()
    {
        /*
         * Normal save loads begin unlocked.
         *
         * The first time InitialWorldGeneration becomes the active
         * exclusive sequence, the gameplay lock latches ON and remains
         * on until INITIAL GENERATION READY.
         */
        _initialGenerationLifecycleLocked =
        false;

        _initialGenerationLifecycleLatched =
            false;

        _initialGenerationStartingWorldSeed =
            string.Empty;
        _initialGenerationLockStartedAt = -1f;
        _initialGenerationDeadlineWarningIssued = false;

        _skippedUnsuitableSettlementAssetLogs =
            0;
        _generatedOriginSpawnOverride = Vector3.zero;
        _hasGeneratedOriginSpawnOverride = false;
        _generatedOriginFacingOverride = Vector3.forward;
        _hasGeneratedOriginFacingOverride = false;
    }
    public static YQGeneratedWorldRuntimeBuilder Instance
    {
        get;
        private set;
    }

    public bool InitialGenerationRecoveryRequired =>
        _initialGenerationWatchdogAborted;

    public bool HasMaterializedCurrentWorld
    {
        get
        {
            if (_buildInProgress ||
                _runtimeRoot == null ||
                !_runtimeRoot.activeInHierarchy ||
                _generatedTerrain == null ||
                _generatedTerrain.terrainData == null ||
                _worldMaterializationFailed)
                return false;

            WorldStateManager manager = WorldStateManager.Instance;
            WorldState world = manager != null ? manager.State : null;
            GeneratedWorldPlanRecord plan = world != null
                ? world.generatedWorldPlan
                : null;
            if (world == null || plan == null)
                return false;

            plan.EnsureCollections();
            int settlementCount = plan.settlements != null
                ? plan.settlements.Count
                : 0;
            int generatedNpcCount = plan.generatedNpcs != null
                ? plan.generatedNpcs.Count
                : 0;
            bool populationReady = generatedNpcCount == 0 ||
                _materializedGeneratedNpcCount == generatedNpcCount;
            // note: Startup reveals gameplay only when the accepted save plan, seed, and every settlement match the physical runtime world.
            return ReferenceEquals(_builtWorldState, world) &&
                   ReferenceEquals(_builtPlan, plan) &&
                   string.Equals(_builtWorldSeed, plan.worldSeed, StringComparison.Ordinal) &&
                   _builtSettlementCount == settlementCount &&
                   populationReady;
        }
    }

    /*
     * Initial generation owns gameplay input until the entire canonical
     * world has been physically materialized and revealed.
     *
     * Ordinary save loading and manual world rebuilding do not use this
     * lock unless they are explicitly running inside InitialWorldGeneration.
     */
    public static bool IsInitialGenerationGameplayLocked
    {
        get
        {
            return
                _initialGenerationLifecycleLocked;
        }
    }

    public static bool IsLegacyComparisonPathActive =>
        YQWorldGenerationArchitecture
            .AllowsLegacyRuntimeBuilder;

    public static float LastInitialGenerationGameplayUnlockTime
    {
        get;
        private set;
    } =
        -9999f;
    public static void BeginInitialGenerationGameplayLock()
    {
        if (_initialGenerationLifecycleLatched &&
            _initialGenerationLifecycleLocked)
        {
            return;
        }

        _initialGenerationLifecycleLatched =
            true;

        _initialGenerationLifecycleLocked =
            true;
        // note: Every new initial-generation transaction owns one deadline warning; the warning never unlocks or hides an incomplete world.
        _initialGenerationDeadlineWarningIssued = false;
        // note: One absolute diagnostic deadline reports a stalled transaction once while preserving the fail-closed gameplay lock.
        _initialGenerationLockStartedAt = Time.unscaledTime;
        YQGoddessGenerationDialogue
    .ResetForNewGeneration();

        /*
         * Capture any deterministic scaffold that existed before the
         * canonical new-world plan is generated.
         */
        _initialGenerationStartingWorldSeed =
            string.Empty;

        WorldStateManager worldManager =
            WorldStateManager.Instance;

        WorldState world =
            worldManager != null
                ? worldManager.State
                : null;

        if (world != null)
        {
            world.EnsureCollections();

            GeneratedWorldPlanRecord plan =
                world.generatedWorldPlan;

            if (plan != null)
            {
                plan.EnsureCollections();

                _initialGenerationStartingWorldSeed =
                    plan.worldSeed ??
                    string.Empty;
            }
        }

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] " +
            "INITIAL GENERATION GAMEPLAY LOCK ACQUIRED. " +
            "Starting world seed=" +
            (string.IsNullOrWhiteSpace(
                _initialGenerationStartingWorldSeed)
                ? "<none>"
                : _initialGenerationStartingWorldSeed));

        // note: This is neutral connection UI, not Goddess dialogue; her first words arrive with the accepted origin response.
        YQStartupLoadingScreen.SetGenerationStage(
            "Securing connection...",
            0.03f);
    }
    private static void ReleaseInitialGenerationGameplayLock()
    {
        if (!_initialGenerationLifecycleLocked)
            return;

        _initialGenerationLifecycleLocked =
            false;
        _initialGenerationLockStartedAt = -1f;

        // note: Background generation systems use this timestamp to avoid stealing the first playable frames.
        LastInitialGenerationGameplayUnlockTime =
            Time.unscaledTime;

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] " +
            "Initial-generation gameplay lock RELEASED.");
    }
    [Header("Generated World")]
    [Tooltip(
        "Automatically materialize the persisted generated world " +
        "after a selected save has completed origin generation.")]
    public bool buildAutomatically = true;

    [Header("Generation Presentation")]
    [Range(0.35f, 0.75f)]
    [Tooltip(
        "Minimum real-time display duration for static grab-bag Goddess " +
        "creation messages during initial generation. This affects " +
        "presentation only and never world determinism.")]
    public float physicalStageMessageHoldSeconds = 0.55f;

    [Range(1.5f, 2f)]
    [Tooltip(
        "Minimum real-time display duration for Ollama-authored Goddess " +
        "creation messages during initial generation. This affects " +
        "presentation only and never world determinism.")]
    public float generatedStageMessageHoldSeconds = 1.75f;

    [Header("Settlement Layout")]
    [Range(1, 8)]
    public int buildingLotCount = 4;

    [Range(3, 12)]
    public int pathPieceCount = 7;

    [Range(0, 20)]
    public int decorationCount = 8;

    [Range(0, 20)]
    public int vegetationCount = 8;

    [Header("Debug Compatibility")]
    [Tooltip(
        "Retained only for compatibility with the old single-settlement " +
        "prototype. Automatic generation now builds every persisted settlement.")]
    public int settlementIndex = 0;

    private const string RuntimeRootName =
        "YQ_GENERATED_WORLD_RUNTIME";

    private const string InitialGenerationOwner =
        "InitialWorldGeneration";

    private const int MaximumGeneratedBuildingMeshColliderTriangles =
        30000;

    private const int MaximumGeneratedBuildingMeshColliders =
        8;

    private GameObject _runtimeRoot;

    private Terrain _generatedTerrain;

    private WorldState _builtWorldState;

    private GeneratedWorldPlanRecord _builtPlan;

    private string _builtWorldSeed =
        string.Empty;

    private int _builtSettlementCount;

    private bool _worldMaterializationFailed;
    private bool _initialGenerationWatchdogAborted;

    private bool _lastSettlementMaterialized;

    private string _builtVisualSignature =
        string.Empty;

    /*
     * -1 = population materialization failed/not established
     *  0 = world built but canonical population has not arrived yet
     * >0 = exact number of canonical NPC plan records materialized
     */
    private int _materializedGeneratedNpcCount =
        -1;

    private float _nextPopulationMaterializationRetryAt;

    private Coroutine _populationBuildCoroutine;

    private bool _populationBuildInProgress;

    private string _revealedInitialGenerationSeed =
        string.Empty;

    /*
     * Physical construction is staged through a coroutine during initial
     * generation so loading dialogue can actually render between terrain,
     * environment, settlement and building phases.
     */
    private bool _buildInProgress;

    private bool _compiledBindingsChangedDuringBuild;

    private Coroutine _buildCoroutine;

    private float _nextAutomaticBuildCheckTime;

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(
                gameObject);

            return;
        }

        Instance =
            this;

        // note: Apply strict loading, upload, and GC budgets before the first terrain, site, or local-model materialization task can begin.
        YQGeneratedWorldPerformanceDirector
            .ConfigureStartupFrameBudget();

        // note: The coordinator remains the single world-build owner while the selected architecture decides whether settlements use reviewed sites or the legacy comparison path.
        buildAutomatically =
            YQWorldGenerationArchitecture.AllowsLegacyRuntimeBuilder ||
            YQWorldGenerationArchitecture.UsesCompiledWorld;

        if (YQWorldGenerationArchitecture.AllowsLegacyRuntimeBuilder)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "WG0 LEGACY SCATTER COMPARISON PATH ACTIVE. " +
                "This builder remains playable for baseline comparison " +
                "but is not the production AAA world compiler. " +
                "Benchmark=" +
                YQWorldGenerationArchitecture
                    .FirstBenchmarkId +
                " kit=" +
                YQWorldGenerationArchitecture
                    .FirstBenchmarkPrimaryKitTag +
                ".");
        }
        else if (YQWorldGenerationArchitecture.UsesCompiledWorld)
        {
            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] REVIEWED COMPILED WORLD PATH ACTIVE. " +
                "Settlement geometry will be selected from the validated runtime semantic catalog.");
        }
    }

    private void Update()
    {
        if (IsInitialGenerationGameplayLocked &&
            _initialGenerationLockStartedAt >= 0f &&
            !_initialGenerationDeadlineWarningIssued &&
            Time.unscaledTime - _initialGenerationLockStartedAt >=
                MaximumInitialGenerationLockSeconds)
        {
            ReportInitialGenerationDeadlineExceeded();
        }

        if (_initialGenerationWatchdogAborted)
            return;

        float automaticCheckInterval =
            IsInitialGenerationGameplayLocked
                ? 0.10f
                : 0.75f;

        if (Time.unscaledTime <
            _nextAutomaticBuildCheckTime)
        {
            return;
        }

        // note: World-plan normalization traverses every palette entry, so lifecycle checks run on a paced coordinator tick instead of every rendered frame.
        _nextAutomaticBuildCheckTime =
            Time.unscaledTime +
            automaticCheckInterval;

        /*
         * Population may have completed on the previous frame and
         * the runtime world may now be ready for final reveal.
         */
        TryCompleteInitialGenerationReveal();

        if (!buildAutomatically)
            return;

        /*
         * Never launch a second physical-world build while the first
         * staged build is still executing.
         */
        if (_buildInProgress)
            return;

        if (!YourQuestTutorialAutoBootstrap.GameplayRuntimeReady)
            return;

        PlayerStateManager playerManager =
            PlayerStateManager.Instance;

        PlayerState playerState =
            playerManager != null
                ? playerManager.state
                : null;

        if (playerState == null)
            return;

        playerState.EnsureCollections();

        if (!GeneratedRpgContentService
                .HasCompletedOrigin(
                    playerState))
        {
            return;
        }

        /*
         * Origin completion can occur in the same frame in which
         * player/world bootstrap is still settling.
         */
        GameObject authoritativePlayer =
            null;

        YQInvestorPlayerMotor activeMotor =
            YQInvestorPlayerMotor.ActiveMotor;
        if (activeMotor != null && activeMotor.IsAuthoritative)
        {
            // note: The authoritative motor already owns a stable singleton reference; avoid a scene-wide tag lookup on every coordinator tick.
            authoritativePlayer = activeMotor.gameObject;
        }

        try
        {
            if (authoritativePlayer == null)
            {
                authoritativePlayer =
                    GameObject.FindGameObjectWithTag(
                        "Player");
            }
        }
        catch
        {
        }

        if (authoritativePlayer == null)
            return;

        if (!CanBuildCurrentSave())
            return;

        WorldStateManager manager =
            WorldStateManager.Instance;

        if (manager == null ||
            manager.State == null)
        {
            return;
        }

        WorldState world =
            manager.State;

        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        if (plan == null)
            return;

        plan.EnsureCollections();

        /*
         * CRITICAL:
         *
         * RegenerateAfterOrigin() writes a deterministic scaffold
         * immediately and then requests the authored LLM plan.
         *
         * Never physically construct that temporary scaffold while
         * WorldPlanGeneration is still running.
         */
        YQWorldGenerationService worldGeneration =
            YQWorldGenerationService.Instance;

        if (worldGeneration != null &&
            worldGeneration.IsRequestInFlight)
        {
            return;
        }

        int settlementCount =
            plan.settlements != null
                ? plan.settlements.Count
                : 0;

        int generatedNpcCount =
            plan.generatedNpcs != null
                ? plan.generatedNpcs.Count
                : 0;

        bool sameBuiltPlanIdentity =
            _runtimeRoot != null &&
            _generatedTerrain != null &&
            _builtWorldState == world &&
            _builtPlan == plan &&
            string.Equals(
                _builtWorldSeed,
                plan.worldSeed,
                StringComparison.Ordinal) &&
            _builtSettlementCount == settlementCount;

        // note: Accepted world plans are immutable save records; reuse their compact signature instead of allocating and traversing it every 0.75 seconds.
        string visualSignature = sameBuiltPlanIdentity
            ? _builtVisualSignature
            : BuildVisualSignature(plan);

        /*
         * The terrain and settlements may already exist before canonical
         * population generation finishes.
         *
         * Materialize the population in-place rather than rebuilding
         * the entire generated world.
         */
        bool sameBuiltWorld =
            sameBuiltPlanIdentity &&
            string.Equals(
                _builtVisualSignature,
                visualSignature,
                StringComparison.Ordinal);

        if (sameBuiltWorld)
        {
            if (generatedNpcCount > 0 &&
                generatedNpcCount !=
                    _materializedGeneratedNpcCount)
            {
                if (_populationBuildInProgress)
                    return;

                if (Time.unscaledTime < _nextPopulationMaterializationRetryAt)
                    return;

                YQRuntimeWorldAssetRegistry registry =
                    YQRuntimeWorldAssetRegistry.Instance;

                if (registry == null)
                    return;

                /*
                 * Canonical identities have now been committed.
                 *
                 * The NPC planner has already presented its completion line.
                 * This is the actual physical population pass.
                 */
                if (IsInitialGenerationGameplayLocked)
                {
                    YQStartupLoadingScreen.SetGenerationStage(
                        YQGoddessGenerationDialogue
                            .PopulationReadout(
                                plan),
                        0.94f);
                }

                // note: Late-arriving NPC plans use the same cooperative population path as startup so prefab setup cannot monopolize a live gameplay frame.
                _populationBuildCoroutine = StartCoroutine(
                    BuildPopulationInPlaceRoutine(
                        world,
                        plan,
                        generatedNpcCount,
                        registry));
            }

            return;
        }

        bool sameFailedAttempt =
            _worldMaterializationFailed &&
            _runtimeRoot != null &&
            _builtWorldState == world &&
            _builtPlan == plan &&
            string.Equals(
                _builtWorldSeed,
                plan.worldSeed,
                StringComparison.Ordinal) &&
            string.Equals(
                _builtVisualSignature,
                visualSignature,
                StringComparison.Ordinal);

        if (sameFailedAttempt)
        {
            // note: A rejected site must not trigger a rebuild every frame; an explicit rebuild or changed persisted plan is required for another attempt.
            return;
        }

        bool needsBuild =
            _runtimeRoot == null ||
            _builtWorldState != world ||
            _builtPlan != plan ||
            !string.Equals(
                _builtWorldSeed,
                plan.worldSeed,
                StringComparison.Ordinal) ||
            _builtSettlementCount !=
                settlementCount ||
            !string.Equals(
                _builtVisualSignature,
                visualSignature,
                StringComparison.Ordinal);

        if (!needsBuild)
            return;

        BuildGeneratedWorld();
    }

    private IEnumerator BuildPopulationInPlaceRoutine(
        WorldState expectedWorld,
        GeneratedWorldPlanRecord expectedPlan,
        int expectedNpcCount,
        YQRuntimeWorldAssetRegistry registry)
    {
        _populationBuildInProgress = true;
        GameObject expectedRuntimeRoot = _runtimeRoot;
        Terrain expectedTerrain = _generatedTerrain;
        bool populationBuilt = false;

        yield return YQGeneratedWorldPopulation.BuildRoutine(
            expectedRuntimeRoot != null
                ? expectedRuntimeRoot.transform
                : null,
            expectedTerrain,
            expectedPlan,
            registry,
            success => populationBuilt = success);

        bool contextStillCurrent =
            IsCurrentBuildContext(expectedWorld, expectedPlan) &&
            _runtimeRoot == expectedRuntimeRoot &&
            _generatedTerrain == expectedTerrain;
        int currentNpcCount =
            expectedPlan != null && expectedPlan.generatedNpcs != null
                ? expectedPlan.generatedNpcs.Count
                : 0;

        if (populationBuilt && contextStillCurrent &&
            currentNpcCount == expectedNpcCount)
        {
            _materializedGeneratedNpcCount = expectedNpcCount;
            _nextPopulationMaterializationRetryAt = 0f;

            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Canonical population materialized cooperatively in-place: " +
                expectedNpcCount +
                " NPC plan records.");
        }
        else if (contextStillCurrent)
        {
            _materializedGeneratedNpcCount = -1;
            // note: A failed or superseded population pass retries at a bounded cadence without rebuilding actors every frame.
            _nextPopulationMaterializationRetryAt =
                Time.unscaledTime + 2f;

            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Canonical NPC records exist, but cooperative runtime " +
                "population materialization did not complete.");
        }

        _populationBuildInProgress = false;
        _populationBuildCoroutine = null;
    }

    // ------------------------------------------------------------
    // WORLD BUILD
    // ------------------------------------------------------------

    [ContextMenu("Build Generated World")]
    public void BuildGeneratedWorld()
    {
        if (!YQWorldGenerationArchitecture.AllowsLegacyRuntimeBuilder &&
            !YQWorldGenerationArchitecture.UsesCompiledWorld)
        {
            // note: Manual context-menu calls may not bypass the architecture boundary after the compiled-world path becomes authoritative.
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Legacy scatter build refused because the active " +
                "materialization path is " +
                YQWorldGenerationArchitecture
                    .ActiveMaterializationPath +
                ".");

            return;
        }

        if (_buildInProgress)
        {
            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Generated-world build is already in progress.");

            return;
        }

        WorldStateManager worldStateManager =
            WorldStateManager.Instance;

        if (worldStateManager == null ||
            worldStateManager.State == null)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "WorldStateManager or active WorldState is missing.");

            return;
        }

        WorldState world =
            worldStateManager.State;

        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        if (plan == null)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Active save has no generated world plan.");

            return;
        }

        plan.EnsureCollections();

        if (string.IsNullOrWhiteSpace(
                plan.worldSeed))
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Generated world plan has no world seed.");

            return;
        }
        /*
 * InitialWorldGeneration began while a deterministic fallback/scaffold
 * plan could already exist in the save.
 *
 * That pre-generation plan is NOT the newly authored canonical world.
 * Never allow it to satisfy the final reveal gate.
 */
        if (_initialGenerationLifecycleLatched &&
            !string.IsNullOrWhiteSpace(
                _initialGenerationStartingWorldSeed) &&
            string.Equals(
                plan.worldSeed,
                _initialGenerationStartingWorldSeed,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        if (plan.settlements == null ||
            plan.settlements.Count == 0)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Generated world plan contains no settlements.");

            return;
        }

        /*
         * Defensive duplicate-build guard for direct/manual calls.
         */
        YQWorldGenerationService worldGeneration =
            YQWorldGenerationService.Instance;

        if (worldGeneration != null &&
            worldGeneration.IsRequestInFlight)
        {
            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "World-plan generation is still in flight. " +
                "Physical materialization deferred.");

            return;
        }

        /*
         * Palettes are derived presentation data.
         */
        YQWorldAssetCatalog.EnsureAssetPalettes(
            plan);

        YQRuntimeWorldAssetRegistry registry =
            YQRuntimeWorldAssetRegistry.Instance;

        if (registry == null)
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "YQRuntimeWorldAssetRegistry could not be loaded.");

            return;
        }

        CancelPopulationBuildRoutine();

        _worldMaterializationFailed = false;

        _buildCoroutine =
            StartCoroutine(
                BuildGeneratedWorldRoutine(
                    world,
                    plan,
                    registry));
    }

    private IEnumerator BuildGeneratedWorldRoutine(
        WorldState world,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry)
    {
        _buildInProgress =
            true;

        // note: A rebuild may follow a previously successful origin; clear its landing point before this transaction can fail and accidentally reuse stale world height.
        _generatedOriginSpawnOverride = Vector3.zero;
        _hasGeneratedOriginSpawnOverride = false;
        _generatedOriginFacingOverride = Vector3.forward;
        _hasGeneratedOriginFacingOverride = false;
        // note: Hostile relocation belongs to one materialization transaction; a rebuild recomputes the same deterministic anchors from the accepted plan and current reviewed footprints.
        YQGeneratedWorldLayout.ClearRuntimeEncampmentAnchors();

        /*
         * Only initial new-game generation gets intentionally paced
         * Goddess narration.
         *
         * Ordinary save loads/manual rebuilds retain effectively
         * synchronous construction behavior.
         */
        bool narrate =
            IsInitialGenerationGameplayLocked;

        try
        {
            // note: Warm only the accepted plan's palette packs; all unrelated genre libraries remain unloaded.
            yield return
                registry.PreloadAssetPathsRoutine(
                    CollectActivePaletteAssetPaths(
                        plan));

            // note: Read the accepted plan before physical construction so deterministic scaffold worlds receive the same factual narration as LLM-authored worlds.
            YQGoddessGenerationDialogue
                .SetWorldReadout(
                    plan);

            if (narrate)
            {
                yield return
                    PresentInitialGenerationStage(
                        YQGoddessGenerationDialogue
                            .TakeWorldCompletion(
                                string.Empty),
                        0.60f);
            }

            if (narrate)
            {
                yield return
                    PresentInitialGenerationStage(
                        YQGoddessGenerationDialogue
                            .TerrainReadout(
                                plan),
                        0.64f);
            }

            if (!IsCurrentBuildContext(
                    world,
                    plan))
            {
                yield break;
            }

            /*
             * Extract the only fixed narrative world objects before
             * destroying either the previous generated runtime or the
             * old pre-title tutorial world.
             */
            DetachOriginObjectsForRebuild(
                out GameObject preservedHut,
                out GameObject preservedVey);

            DestroyRuntimeRootOnly();

            _runtimeRoot =
                new GameObject(
                    RuntimeRootName);

            /*
             * Terrain is deterministic from the persisted world seed.
             */
            _generatedTerrain =
                null;

            // note: Terrain height synthesis and upload are frame-budgeted so the Goddess presentation never waits behind one monolithic terrain build frame.
            yield return
                YQGeneratedWorldTerrain.BuildRoutine(
                    _runtimeRoot.transform,
                    plan.worldSeed,
                    terrain => _generatedTerrain = terrain);

            if (_generatedTerrain == null)
            {
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] Generated terrain build did not produce a runtime terrain.");
                yield break;
            }

            EnsureGeneratedWorldSun(
                _runtimeRoot.transform);

            _compiledBindingsChangedDuringBuild = false;

            if (narrate)
            {
                yield return
                    PresentInitialGenerationStage(
                        YQGoddessGenerationDialogue
                            .EnvironmentReadout(
                                plan),
                        0.67f);
            }

            if (!IsCurrentBuildContext(
                    world,
                    plan))
            {
                yield break;
            }

            // note: Terrain geometry/layers become canonical first; reviewed construction pads are then applied in plan order before any wilderness object samples a height.
            yield return
                YQGeneratedWorldEnvironment.BuildTerrainFoundationRoutine(
                    _generatedTerrain,
                    plan,
                    registry);

            bool constructionTerrainPrepared =
                false;

            // note: Construction pads are authored incrementally so no settlement set can interrupt the loading-screen camera and typewriter for one long frame.
            yield return PrepareDeterministicConstructionTerrainRoutine(
                plan,
                _generatedTerrain,
                prepared => constructionTerrainPrepared = prepared);

            // note: Dressing is a read-only consumer of the final canonical heightfield. Streamed sites can no longer reshape terrain after this point.
            yield return
                YQGeneratedWorldEnvironment.BuildWildernessRoutine(
                    _runtimeRoot.transform,
                    _generatedTerrain,
                    plan,
                    registry);

            // note: The Goddess statue, Vey's witch hut, and Vey form the fixed narrative origin and must explicitly join the build transaction.
            bool originMaterialized = false;
            yield return
                AdoptVeyOriginIntoGeneratedWorldRoutine(
                    _runtimeRoot.transform,
                    _generatedTerrain,
                    registry,
                    preservedHut,
                    preservedVey,
                    success => originMaterialized = success);

            // note: A new world begins at the authored threshold; ordinary loads retain the save's horizontal location and receive only terrain-height safety correction.
            PlacePlayerAtGeneratedOrigin(
                _generatedTerrain,
                narrate);

            int settlementsBuilt =
                0;

            int settlementsSkipped =
                0;

            int settlementTotal =
                plan.settlements != null
                    ? plan.settlements.Count
                    : 0;

            /*
             * Materialize every persisted settlement.
             */
            for (int i = 0;
                 i < settlementTotal;
                 i++)
            {
                if (!IsCurrentBuildContext(
                        world,
                        plan))
                {
                    yield break;
                }

                GeneratedSettlementRecord settlement =
                    plan.settlements[i];

                if (settlement == null)
                {
                    settlementsSkipped++;

                    continue;
                }

                settlement.EnsureCollections();

                GeneratedRegionRecord region =
                    FindRegion(
                        plan,
                        settlement.regionId);

                if (region == null)
                {
                    Debug.LogWarning(
                        "[YQGeneratedWorldRuntimeBuilder] " +
                        "Skipping settlement '" +
                        settlement.displayName +
                        "' because region '" +
                        settlement.regionId +
                        "' could not be found.");

                    settlementsSkipped++;

                    continue;
                }

                region.EnsureCollections();

                GeneratedRegionAssetPaletteRecord palette =
                    FindPalette(
                        plan,
                        region);

                if (palette == null)
                {
                    Debug.LogWarning(
                        "[YQGeneratedWorldRuntimeBuilder] " +
                        "Skipping settlement '" +
                        settlement.displayName +
                        "' because region '" +
                        region.regionId +
                        "' has no generated asset palette.");

                    settlementsSkipped++;

                    continue;
                }

                palette.EnsureCollections();

                float settlementStartProgress =
                    Mathf.Lerp(
                        0.69f,
                        0.77f,
                        settlementTotal > 0
                            ? i /
                              (float)settlementTotal
                            : 0f);

                float settlementEndProgress =
                    Mathf.Lerp(
                        0.69f,
                        0.77f,
                        settlementTotal > 0
                            ? (i + 1) /
                              (float)settlementTotal
                            : 1f);

                if (narrate)
                {
                    yield return
                        PresentInitialGenerationStage(
                            YQGoddessGenerationDialogue
    .Settlement(
        settlement.settlementId,
        settlement.displayName,
        string.Empty),
                            settlementStartProgress);
                }

                if (!IsCurrentBuildContext(
                        world,
                        plan))
                {
                    yield break;
                }

                _lastSettlementMaterialized = false;
                yield return
                    BuildSettlementRoutine(
                        plan,
                        settlement,
                        region,
                        palette,
                        registry,
                        narrate,
                        settlementStartProgress,
                        settlementEndProgress);

                if (_lastSettlementMaterialized)
                    settlementsBuilt++;
                else
                    settlementsSkipped++;
            }

            int hostileSitesBuilt = 0;
            int hostileSitesExpected = 0;

            if (YQWorldGenerationArchitecture.UsesCompiledWorld)
            {
                yield return BuildCompiledHostileSitesRoutine(
                    plan,
                    (built, expected) =>
                    {
                        hostileSitesBuilt = built;
                        hostileSitesExpected = expected;
                    });
            }

            if (!IsCurrentBuildContext(
                    world,
                    plan))
            {
                yield break;
            }

            /*
             * Population is WORLD-level materialization.
             *
             * Every settlement must already exist before residents and
             * hostile leaders are placed.
             */
            int generatedNpcCount =
                plan.generatedNpcs != null
                    ? plan.generatedNpcs.Count
                    : 0;

            if (generatedNpcCount > 0)
            {
                if (narrate)
                {
                    yield return
                        PresentInitialGenerationStage(
                            YQGoddessGenerationDialogue
                                .PopulationReadout(
                                    plan),
                            0.93f);
                }

                bool populationBuilt = false;
                // note: Initial population materialization is cooperative; title/Goddess presentation keeps receiving frames while settlements and encounters acquire their actors.
                yield return YQGeneratedWorldPopulation.BuildRoutine(
                    _runtimeRoot.transform,
                    _generatedTerrain,
                    plan,
                    registry,
                    success => populationBuilt = success);

                _materializedGeneratedNpcCount =
                    populationBuilt
                        ? generatedNpcCount
                        : -1;

                _nextPopulationMaterializationRetryAt = populationBuilt
                    ? 0f
                    : Time.unscaledTime + 2f;

                if (populationBuilt)
                {
                    Debug.Log(
                        "[YQGeneratedWorldRuntimeBuilder] " +
                        "Canonical population materialized after world build: " +
                        generatedNpcCount +
                        " NPC plan records.");
                }
                else
                {
                    Debug.LogWarning(
                        "[YQGeneratedWorldRuntimeBuilder] " +
                        "Canonical NPC records exist, but runtime population " +
                        "materialization did not complete.");
                }
            }
            else
            {
                /*
                 * Canonical population has not arrived yet.
                 *
                 * Update() will perform one in-place population pass when
                 * generatedNpcs becomes available.
                 */
                _materializedGeneratedNpcCount =
                    0;
            }

            /*
             * Record the exact persisted plan that has now been physically
             * materialized.
             */
            _builtWorldState =
                world;

            _builtPlan =
                plan;

            _builtWorldSeed =
                plan.worldSeed;

            _builtSettlementCount =
                settlementsBuilt;

            _worldMaterializationFailed =
                _generatedTerrain == null ||
                !constructionTerrainPrepared ||
                !originMaterialized ||
                settlementsBuilt != settlementTotal ||
                settlementsSkipped > 0 ||
                hostileSitesBuilt != hostileSitesExpected;

            // note: Persist the semantic presentation fingerprint so a later curated genre/palette shift triggers one deterministic rebuild.
            _builtVisualSignature =
                BuildVisualSignature(
                    plan);

            if (_worldMaterializationFailed)
            {
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] WORLD MATERIALIZATION REJECTED\n" +
                    "Expected settlements: " + settlementTotal + "\n" +
                    "Materialized settlements: " + settlementsBuilt + "\n" +
                    "Skipped settlements: " + settlementsSkipped + "\n" +
                    "Origin ready: " + originMaterialized + "\n" +
                    "Hostile sites: " + hostileSitesBuilt + "/" +
                    hostileSitesExpected);
            }
            else if (_compiledBindingsChangedDuringBuild)
            {
                // note: Semantic bindings become accepted save authority only after the complete physical transaction validates; failed builds must not immediately persist partial rebinding.
                WorldStateManager.Instance?.Save();
            }

            // note: The generated hierarchy is the only runtime content subject to the distance and shadow budget.
            YQGeneratedWorldPerformanceDirector
                .ConfigureForGeneratedWorld(
                    _runtimeRoot.transform);

            Debug.Log(
                (_worldMaterializationFailed
                    ? "[YQGeneratedWorldRuntimeBuilder] GENERATED WORLD DIAGNOSTIC TRANSACTION FINISHED (NOT PLAYABLE)\n"
                    : "[YQGeneratedWorldRuntimeBuilder] GENERATED WORLD BUILT\n") +
                "World seed: " +
                plan.worldSeed +
                "\n" +
                "Terrain version: " +
                YQGeneratedWorldTerrain.TerrainGenerationVersion +
                "\n" +
                "Layout version: " +
                YQGeneratedWorldLayout.LayoutVersion +
                "\n" +
                "Regions in plan: " +
                (plan.regions != null
                    ? plan.regions.Count
                    : 0) +
                "\n" +
                "Settlements in plan: " +
                plan.settlements.Count +
                "\n" +
                "Settlements materialized: " +
                settlementsBuilt +
                "\n" +
                "Settlements skipped: " +
                settlementsSkipped +
                "\n" +
                "Canonical NPC records: " +
                generatedNpcCount +
                "\n" +
                "Canonical NPCs materialized: " +
                _materializedGeneratedNpcCount +
                "\n" +
                "Terrain size: " +
                YQGeneratedWorldTerrain.WorldSize +
                " x " +
                YQGeneratedWorldTerrain.WorldSize +
                "\n" +
                "Origin: Goddess statue and Vey's witch hut");
        }
        finally
        {
            _buildInProgress =
                false;

            _buildCoroutine =
                null;
        }
    }

    /*
     * During initial generation, hold each physical-world creation line
     * long enough to survive more than a single rendered frame.
     *
     * This is presentation-only delay and uses unscaled time.
     */
    private IEnumerator PresentInitialGenerationStage(
        string message,
        float progress)
    {
        if (!IsInitialGenerationGameplayLocked)
            yield break;

        if (string.IsNullOrWhiteSpace(
                message))
        {
            yield break;
        }

        YQStartupLoadingScreen.SetGenerationStage(
            message,
            progress);

        // note: Fallback grab-bag lines move quickly; Ollama-authored lines remain readable longer.
        float hold =
            ResolveInitialGenerationStageHold();

        if (hold > 0f)
        {
            yield return
                new WaitForSecondsRealtime(
                    hold);
        }
        else
        {
            /*
             * Even with a zero configured hold, permit at least one
             * rendered frame before overwriting the line.
             */
            yield return null;
        }
    }

    private void ReportInitialGenerationDeadlineExceeded()
    {
        if (!IsInitialGenerationGameplayLocked ||
            _initialGenerationDeadlineWarningIssued)
            return;

        _initialGenerationDeadlineWarningIssued = true;

        // note: A deadline is an actual circuit breaker: stop owned coroutines and prevent the coordinator from immediately relaunching the same wedged transaction.
        _initialGenerationWatchdogAborted = true;
        CancelActiveBuildRoutine();
        _worldMaterializationFailed = true;

        YQStartupLoadingScreen.ShowGenerationFailure(
            "I—yes, I meant to stop there. The world took too long to hold together, so I cut the spell before it froze everything. Retry when you're ready; I'll try to be less ambitious.",
            RetryAfterGenerationWatchdog,
            ReturnToTitleAfterGenerationWatchdog);

        Debug.LogError(
            "[YQGeneratedWorldRuntimeBuilder] INITIAL GENERATION SAFETY DEADLINE REACHED. " +
            "The active materialization coroutines were stopped and the loading screen entered a responsive recovery state. " +
            "Gameplay was not released against incomplete state.");
    }

    private void RetryAfterGenerationWatchdog()
    {
        // note: A player-requested retry receives a fresh deadline and rebuilds only from the accepted persisted world plan.
        _initialGenerationWatchdogAborted = false;
        _initialGenerationDeadlineWarningIssued = false;
        _initialGenerationLockStartedAt = Time.unscaledTime;
        YQStartupLoadingScreen.SetGenerationStage(
            "Right. Smaller gestures. Fewer dramatic pauses. Rebuilding the world...",
            0.70f);
        RebuildGeneratedWorld();
    }

    private void ReturnToTitleAfterGenerationWatchdog()
    {
        // note: Abandon the incomplete runtime hierarchy before handing modal ownership back to the title screen.
        CancelActiveBuildRoutine();
        DestroyRuntimeRootOnly();
        ReleaseInitialGenerationGameplayLock();
        YQTitleEnvironmentLoader.ReleaseWorldGeneration();
        YourQuestTutorialAutoBootstrap.RestartAfterGenerationFailure();
    }

    private float ResolveInitialGenerationStageHold()
    {
        if (YQGoddessGenerationDialogue
                .LastSelectionWasGenerated)
        {
            return
                Mathf.Clamp(
                    generatedStageMessageHoldSeconds,
                    1.5f,
                    2f);
        }

        return
            Mathf.Clamp(
                physicalStageMessageHoldSeconds,
                0.35f,
                0.75f);
    }

    private static bool IsCurrentBuildContext(
        WorldState expectedWorld,
        GeneratedWorldPlanRecord expectedPlan)
    {
        if (expectedWorld == null ||
            expectedPlan == null)
        {
            return false;
        }

        WorldStateManager manager =
            WorldStateManager.Instance;

        if (manager == null ||
            manager.State != expectedWorld)
        {
            return false;
        }

        expectedWorld.EnsureCollections();

        return
            expectedWorld.generatedWorldPlan ==
            expectedPlan;
    }

    // ------------------------------------------------------------
    // INITIAL GENERATION REVEAL
    // ------------------------------------------------------------

    private void TryCompleteInitialGenerationReveal()
    {
        if (_buildInProgress)
            return;

        LLMClient llm =
            LLMClient.Instance;

        /*
         * This path applies only to the special new-save generation
         * sequence.
         *
         * Ordinary loads and manual rebuilds must not release unrelated
         * LLM sequence owners.
         */
        if (llm == null ||
            !llm.IsExclusiveSequenceActive ||
            !string.Equals(
                llm.ExclusiveSequenceOwner,
                InitialGenerationOwner,
                StringComparison.Ordinal))
        {
            return;
        }

        PlayerStateManager playerManager =
            PlayerStateManager.Instance;

        WorldStateManager worldManager =
            WorldStateManager.Instance;

        if (playerManager == null ||
            playerManager.state == null ||
            worldManager == null ||
            worldManager.State == null)
        {
            return;
        }

        if (!GeneratedRpgContentService
                .HasCompletedOrigin(
                    playerManager.state))
        {
            return;
        }

        WorldState world =
            worldManager.State;

        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        if (plan == null)
            return;

        plan.EnsureCollections();

        if (string.IsNullOrWhiteSpace(
                plan.worldSeed))
        {
            return;
        }
        /*
 * Never reveal the world that existed when InitialWorldGeneration
 * started. That plan is the pre-generation deterministic scaffold,
 * not the newly authored canonical world.
 */
        if (_initialGenerationLifecycleLatched &&
            !string.IsNullOrWhiteSpace(
                _initialGenerationStartingWorldSeed) &&
            string.Equals(
                plan.worldSeed,
                _initialGenerationStartingWorldSeed,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        /*
         * Never finish against an old or temporary physical build.
         */
        if (_runtimeRoot == null ||
            _generatedTerrain == null ||
            _builtWorldState != world ||
            _builtPlan != plan)
        {
            return;
        }

        if (!string.Equals(
                _builtWorldSeed,
                plan.worldSeed,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (_worldMaterializationFailed)
        {
            // note: A physically rejected transaction may have instantiated diagnostic geometry, but it is never a playable world and must not release the exclusive generation lock.
            return;
        }

        if (plan.settlements == null ||
            _builtSettlementCount <
                plan.settlements.Count)
        {
            return;
        }

        /*
         * If WorldPlanGeneration is still executing, the deterministic
         * scaffold is temporary and must remain hidden.
         */
        YQWorldGenerationService worldGeneration =
            YQWorldGenerationService.Instance;

        if (worldGeneration != null &&
            worldGeneration.IsRequestInFlight)
        {
            return;
        }

        int canonicalNpcCount =
            plan.generatedNpcs != null
                ? plan.generatedNpcs.Count
                : 0;

        YQGeneratedNpcPlanningService npcPlanner =
            YQGeneratedNpcPlanningService.Instance;

        if (canonicalNpcCount > 0 &&
            _materializedGeneratedNpcCount !=
                canonicalNpcCount)
        {
            // note: NPC records are not enough; generated people/threats must be physically placed before reveal.
            YQStartupLoadingScreen.SetGenerationStage(
                string.Empty,
                0.94f);

            return;
        }

        // note: An empty canonical population no longer holds the tutorial hostage; the planner fills it through Ollama after the playable reveal.

        /*
         * Prevent duplicate completion/reveal for the same generated
         * world seed.
         */
        if (string.Equals(
                _revealedInitialGenerationSeed,
                plan.worldSeed,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _revealedInitialGenerationSeed =
            plan.worldSeed;

        YQStartupLoadingScreen.SetGenerationStage(
            YQGoddessGenerationDialogue
                .RevealReadout(
                    plan),
            0.97f);

        // note: Capture the reveal source before later UI calls can overwrite the dialogue selection flag.
        float revealHoldSeconds =
            ResolveInitialGenerationStageHold();

        Debug.Log(
    "[YQGeneratedWorldRuntimeBuilder] " +
    "INITIAL GENERATION READY\n" +
    "World seed: " +
    plan.worldSeed +
    "\nSettlements materialized: " +
    _builtSettlementCount +
    "\nCanonical NPCs materialized: " +
    _materializedGeneratedNpcCount);

        /*
 * This is the ONLY successful initial-generation unlock point.
 *
 * Log every condition that permitted gameplay to unlock. This must
 * never execute until the complete current physical world has been
 * built and the accepted plan is no longer temporary.
 */
        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] GAMEPLAY UNLOCK EXECUTING\n" +
            "WorldSeed=" +
            plan.worldSeed +
            "\nStartingWorldSeed=" +
            _initialGenerationStartingWorldSeed +
            "\nBuiltWorldSeed=" +
            _builtWorldSeed +
            "\nCanonicalNpcCount=" +
            canonicalNpcCount +
            "\nMaterializedNpcCount=" +
            _materializedGeneratedNpcCount +
            "\nNpcGenerationEnabled=" +
            (npcPlanner != null &&
             npcPlanner.enableNpcGeneration) +
            "\nNpcPlannerComplete=" +
            (npcPlanner != null &&
             npcPlanner.HasCompletedCanonicalPopulation) +
            "\nNpcPlannerTerminalFailure=" +
            (npcPlanner != null &&
             npcPlanner.HasTerminalPopulationFailure) +
            "\nWorldPlanRequestInFlight=" +
            (worldGeneration != null &&
             worldGeneration.IsRequestInFlight) +
            "\nExclusiveActive=" +
            llm.IsExclusiveSequenceActive +
            "\nExclusiveOwner=" +
            llm.ExclusiveSequenceOwner);

        // note: Route all successful unlocks through the guarded release path so it logs once.
        ReleaseInitialGenerationGameplayLock();

        GameObject player = null;

        try
        {
            player =
                GameObject.FindGameObjectWithTag(
                    "Player");
        }
        catch
        {
        }

        if (player == null)
            Debug.LogError("[YQGeneratedWorldRuntimeBuilder] GENERATION LOCK: No GameObject tagged Player exists.");
        else
            // note: Release diagnostics stay compact; component-by-component dumps previously created large avoidable strings at the first playable frame.
            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] AUTHORITATIVE PLAYER READY " +
                "Player=" + player.name +
                " Active=" + player.activeInHierarchy +
                " Position=" + player.transform.position);

        /*
         * Finish the black generation presentation.
         */
        YQStartupLoadingScreen loading =
            YQStartupLoadingScreen.Current;

        if (loading != null)
        {
            StartCoroutine(
                loading.FinishGenerationAndHide(
                    revealHoldSeconds));
        }

        /*
         * Physical generation is now genuinely complete.
         *
         * Ending this sequence also releases the player movement lock.
         */
        llm.EndExclusiveSequence(
            InitialGenerationOwner);
    }

    // ------------------------------------------------------------
    // SETTLEMENT BUILD
    // ------------------------------------------------------------

    private IEnumerator BuildSettlementRoutine(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        bool narrate,
        float startProgress,
        float endProgress)
    {
        if (_runtimeRoot == null)
            yield break;

        if (YQWorldGenerationArchitecture.UsesCompiledWorld)
        {
            yield return
                BuildCompiledSettlementRoutine(
                    plan,
                    settlement,
                    region,
                    palette);
            yield break;
        }

        // note: The canonical terrain prepass already graded this settlement before wilderness placement; construction only samples its finalized elevation once.
        Vector3 center =
            YQGeneratedWorldLayout
                .GetSettlementAnchor(
                    plan,
                    settlement,
                    _generatedTerrain);

        GameObject settlementRoot =
            new GameObject(
                "Settlement__" +
                SafeName(
                    settlement.displayName) +
                "__" +
                settlement.settlementId);

        settlementRoot.transform.SetParent(
            _runtimeRoot.transform,
            false);

        settlementRoot.transform.position =
            center;

        // note: Rotate the complete civic plan as one unit; individual façades keep their authored relationship to roads while settlements stop sharing one world-axis silhouette.
        settlementRoot.transform.rotation =
            Quaternion.Euler(
                0f,
                DeterministicQuarterTurn(
                    SettlementSeed(settlement) +
                    ":civic_orientation"),
                0f);

        BuildRegionVolume(
            settlementRoot.transform,
            region,
            settlement);

        BuildSettlementLabel(
            settlementRoot.transform,
            settlement,
            region,
            palette);

        /*
         * Roads establish the settlement's basic spatial shape.
         */
        BuildMainPath(
            settlementRoot.transform,
            plan,
            settlement,
            palette,
            registry);

        // note: The two starter reference cells receive a readable perimeter with one intentional player entrance.
        BuildSettlementPerimeter(
            settlementRoot.transform,
            plan,
            settlement,
            palette,
            registry);

        // note: Roads/perimeter, buildings, and dressing are separate frame-budget phases during live palette transitions.
        yield return null;

        if (narrate)
        {
            float buildingProgress =
                Mathf.Lerp(
                    startProgress,
                    endProgress,
                    0.48f);

            yield return
                PresentInitialGenerationStage(
                            YQGoddessGenerationDialogue
    .Buildings(
        settlement.settlementId,
        settlement.displayName,
        string.Empty),
                    buildingProgress);
        }

        /*
         * Actual marketplace building prefabs.
         */
        BuildBuildingLots(
            settlementRoot.transform,
            plan,
            settlement,
            palette,
            registry);

        yield return null;

        /*
         * Settlement dressing is intentionally kept in the same
         * construction transaction.
         */
        BuildDecorations(
            settlementRoot.transform,
            plan,
            settlement,
            palette,
            registry);

        yield return null;

        BuildVegetation(
            settlementRoot.transform,
            plan,
            settlement,
            palette,
            registry);

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] SETTLEMENT BUILT\n" +
            "Settlement: " +
            settlement.displayName +
            " (" +
            settlement.settlementId +
            ")\n" +
            "Kind: " +
            settlement.kind +
            "\n" +
            "Population: " +
            settlement.approxPopulation +
            "\n" +
            "Region: " +
            region.displayName +
            " (" +
            region.regionId +
            ")\n" +
            "Palette: " +
            palette.styleKey +
            "\n" +
            "Settlement seed: " +
            settlement.deterministicSeed +
            "\n" +
            "Generated grid: (" +
            settlement.gridX +
            ", " +
            settlement.gridY +
            ")\n" +
            "Anchor: " +
            center);

        _lastSettlementMaterialized = true;
    }

    private IEnumerator BuildCompiledSettlementRoutine(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette)
    {
        if (!YQCompiledWorldSiteBindingService.TryResolveSettlementSite(
                plan,
                settlement,
                region,
                palette,
                out YQRuntimeWorldSiteRecord siteRecord,
                out bool bindingChanged))
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] COMPILED SETTLEMENT REJECTED\n" +
                "Settlement: " + settlement.displayName + "\n" +
                "Reason: no compatible reviewed runtime site is available.");
            yield break;
        }

        _compiledBindingsChangedDuringBuild |= bindingChanged;
        // note: One shared reviewed footprint was graded during the canonical terrain prepass; streaming itself is geometry-only and samples that anchor once.
        Vector3 center = YQGeneratedWorldLayout.GetSettlementAnchor(
            plan,
            settlement,
            _generatedTerrain);
        GameObject settlementRoot = new GameObject(
            "CompiledSettlement__" + SafeName(settlement.displayName) +
            "__" + settlement.settlementId);
        settlementRoot.transform.SetParent(_runtimeRoot.transform, false);
        settlementRoot.transform.position = center;
        settlementRoot.transform.rotation = Quaternion.Euler(
            0f,
            DeterministicQuarterTurn(
                SettlementSeed(settlement) + ":compiled_site_orientation"),
            0f);
        BuildRegionVolume(settlementRoot.transform, region, settlement);
        BuildSettlementLabel(
            settlementRoot.transform,
            settlement,
            region,
            palette);
        bool materialized = false;
        yield return
            YQCompiledWorldSiteInstance.MaterializeRoutine(
                settlementRoot.transform,
                settlement,
                siteRecord,
                success => materialized = success);

        if (!materialized)
        {
            settlementRoot.SetActive(false);
            Destroy(settlementRoot);
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] COMPILED SETTLEMENT LOAD FAILED\n" +
                "Settlement: " + settlement.displayName + "\n" +
                "Reviewed site: " + siteRecord.kitId);
            yield break;
        }

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] COMPILED SETTLEMENT READY\n" +
            "Settlement: " + settlement.displayName + " (" +
            settlement.settlementId + ")\n" +
            "Reviewed site: " + siteRecord.kitId + "\n" +
            "Semantic style: " + siteRecord.semanticStyleKey + "\n" +
            "Topology: " + siteRecord.topology + "\n" +
            "Anchor: " + center);

        _lastSettlementMaterialized = true;
    }

    private IEnumerator BuildCompiledHostileSitesRoutine(
        GeneratedWorldPlanRecord plan,
        Action<int, int> completed)
    {
        int built = 0;
        int expected = 0;

        if (_runtimeRoot == null || plan == null || plan.encampments == null)
        {
            completed?.Invoke(built, expected);
            yield break;
        }

        GameObject hostileRoot = new GameObject(
            "Generated_CompiledHostileSites");
        hostileRoot.transform.SetParent(_runtimeRoot.transform, false);

        for (int index = 0; index < plan.encampments.Count; index++)
        {
            GeneratedEncampmentRecord encampment = plan.encampments[index];

            if (encampment == null)
                continue;

            expected++;

            GeneratedRegionRecord region = FindRegion(
                plan,
                encampment.regionId);
            GeneratedRegionAssetPaletteRecord palette = FindPalette(
                plan,
                region);

            if (region == null || palette == null ||
                !YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(
                    plan,
                    encampment,
                    region,
                    palette,
                    out YQRuntimeWorldSiteRecord siteRecord,
                    out bool bindingChanged))
            {
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] COMPILED HOSTILE SITE REJECTED\n" +
                    "Site: " + encampment.displayName + "\n" +
                    "Reason: no compatible reviewed exterior site is available.");
                continue;
            }

            _compiledBindingsChangedDuringBuild |= bindingChanged;
            // note: Hostile sites consume the same finalized heightfield and deterministic anchor used by the terrain prepass.
            Vector3 center = YQGeneratedWorldLayout.GetEncampmentAnchor(
                plan,
                encampment,
                _generatedTerrain);
            GameObject siteRoot = new GameObject(
                "CompiledHostileSite__" + SafeName(encampment.displayName) +
                "__" + encampment.encampmentId);
            siteRoot.transform.SetParent(hostileRoot.transform, false);
            siteRoot.transform.position = center;
            siteRoot.transform.rotation = Quaternion.Euler(
                0f,
                DeterministicQuarterTurn(
                    encampment.deterministicSeed +
                    ":compiled_hostile_orientation"),
                0f);
            bool materialized = false;
            yield return
                YQCompiledWorldSiteInstance.MaterializeRoutine(
                    siteRoot.transform,
                    encampment.encampmentId,
                    siteRecord,
                    success => materialized = success);

            if (!materialized)
            {
                siteRoot.SetActive(false);
                Destroy(siteRoot);
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] COMPILED HOSTILE SITE LOAD FAILED\n" +
                    "Site: " + encampment.displayName + "\n" +
                    "Reviewed site: " + siteRecord.kitId);
                continue;
            }

            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] COMPILED HOSTILE SITE READY\n" +
                "Site: " + encampment.displayName + " (" +
                encampment.encampmentId + ")\n" +
                "Reviewed site: " + siteRecord.kitId + "\n" +
                "Semantic style: " + siteRecord.semanticStyleKey + "\n" +
                "Anchor: " + center);
            built++;
        }

        completed?.Invoke(built, expected);
    }

    /*
     * Compatibility aliases.
     *
     * Anything that still invokes the previous prototype context-menu
     * methods now builds/rebuilds the complete generated world.
     */
    [ContextMenu("Build First Generated Settlement")]
    public void BuildFirstSettlement()
    {
        BuildGeneratedWorld();
    }

    [ContextMenu("Rebuild Generated World")]
    public void RebuildGeneratedWorld()
    {
        CancelActiveBuildRoutine();

        _initialGenerationWatchdogAborted = false;
        YQStartupLoadingScreen.ClearGenerationFailure();

        _builtWorldState =
            null;

        _builtPlan =
            null;

        _builtWorldSeed =
            string.Empty;

        _builtSettlementCount =
            0;

        _worldMaterializationFailed = false;

        _builtVisualSignature =
            string.Empty;

        _materializedGeneratedNpcCount =
            -1;

        _nextPopulationMaterializationRetryAt = 0f;

        BuildGeneratedWorld();
    }

    [ContextMenu("Rebuild First Generated Settlement")]
    public void RebuildFirstSettlement()
    {
        RebuildGeneratedWorld();
    }

    [ContextMenu("Destroy Generated Runtime World")]
    public void DestroyExistingRuntimeWorld()
    {
        CancelActiveBuildRoutine();

        DestroyRuntimeRootOnly();

        _builtWorldState =
            null;

        _builtPlan =
            null;

        _builtWorldSeed =
            string.Empty;

        _builtSettlementCount =
            0;

        _worldMaterializationFailed = false;

        _builtVisualSignature =
            string.Empty;

        _materializedGeneratedNpcCount =
            -1;

        _nextPopulationMaterializationRetryAt = 0f;
    }

    private void CancelActiveBuildRoutine()
    {
        if (_buildCoroutine != null)
        {
            StopCoroutine(
                _buildCoroutine);

            _buildCoroutine =
                null;
        }

        _buildInProgress =
            false;

        CancelPopulationBuildRoutine();
    }

    private void CancelPopulationBuildRoutine()
    {
        if (_populationBuildCoroutine != null)
        {
            StopCoroutine(_populationBuildCoroutine);
            _populationBuildCoroutine = null;
        }

        // note: Cancellation clears ownership immediately so a changed save or explicit rebuild can start a fresh deterministic population transaction.
        _populationBuildInProgress = false;
    }

    private void DestroyRuntimeRootOnly()
    {
        YQGeneratedWorldTerrain.DestroyExisting();

        if (_runtimeRoot != null)
        {
            GameObject root =
                _runtimeRoot;

            _runtimeRoot =
                null;

            root.SetActive(
                false);

            Destroy(
                root);
        }

        GameObject existing =
            GameObject.Find(
                RuntimeRootName);

        if (existing != null)
        {
            existing.SetActive(
                false);

            Destroy(
                existing);
        }

        _generatedTerrain =
            null;
    }

    // ------------------------------------------------------------
    // BUILD ELIGIBILITY
    // ------------------------------------------------------------

    private static List<string> CollectActivePaletteAssetPaths(
        GeneratedWorldPlanRecord plan)
    {
        List<string> paths =
            new List<string>();

        if (plan == null ||
            plan.assetPalettes == null)
        {
            return paths;
        }

        HashSet<string> unique =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        // note: Warm the single curated character shard asynchronously so NPC materialization cannot introduce a synchronous population hitch.
        unique.Add(
            YQRuntimeCreatureAssetIndex
                .CreaturePackAnchorPath);

        paths.Add(
            YQRuntimeCreatureAssetIndex
                .CreaturePackAnchorPath);

        string[] slots =
        {
            YQWorldAssetCatalog.SlotTerrain,
            YQWorldAssetCatalog.SlotFloor,
            YQWorldAssetCatalog.SlotWall,
            YQWorldAssetCatalog.SlotRoof,
            YQWorldAssetCatalog.SlotDoor,
            YQWorldAssetCatalog.SlotPath,
            YQWorldAssetCatalog.SlotSettlementBuilding,
            YQWorldAssetCatalog.SlotLargeStructure,
            YQWorldAssetCatalog.SlotFloorDeco,
            YQWorldAssetCatalog.SlotWallDeco,
            YQWorldAssetCatalog.SlotVegetation,
            YQWorldAssetCatalog.SlotRock,
            YQWorldAssetCatalog.SlotLighting,
            YQWorldAssetCatalog.SlotLootContainer,
            YQWorldAssetCatalog.SlotEnemySite,
            YQWorldAssetCatalog.SlotInteriorDeco,
            YQWorldAssetCatalog.SlotExteriorDeco
        };

        for (int paletteIndex = 0;
             paletteIndex < plan.assetPalettes.Count;
             paletteIndex++)
        {
            GeneratedRegionAssetPaletteRecord palette =
                plan.assetPalettes[paletteIndex];

            if (palette == null)
                continue;

            for (int slotIndex = 0;
                 slotIndex < slots.Length;
                 slotIndex++)
            {
                List<GeneratedAssetReferenceRecord> references =
                    YQWorldAssetCatalog.GetSlotList(
                        palette,
                        slots[slotIndex]);

                if (references == null)
                    continue;

                for (int referenceIndex = 0;
                     referenceIndex < references.Count;
                     referenceIndex++)
                {
                    GeneratedAssetReferenceRecord reference =
                        references[referenceIndex];

                    if (reference != null &&
                        !string.IsNullOrWhiteSpace(reference.assetPath) &&
                        unique.Add(reference.assetPath))
                    {
                        paths.Add(reference.assetPath);
                    }
                }
            }
        }

        return paths;
    }

    private static string BuildVisualSignature(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return string.Empty;

        StringBuilder signature =
            new StringBuilder(
                256);

        if (plan.regions != null)
        {
            for (int i = 0;
                 i < plan.regions.Count;
                 i++)
            {
                GeneratedRegionRecord region =
                    plan.regions[i];

                if (region == null)
                    continue;

                signature
                    .Append(region.regionId)
                    .Append(':')
                    .Append(region.assetStyleKey)
                    .Append('|');
            }
        }

        if (plan.assetPalettes != null)
        {
            for (int i = 0;
                 i < plan.assetPalettes.Count;
                 i++)
            {
                GeneratedRegionAssetPaletteRecord palette =
                    plan.assetPalettes[i];

                if (palette == null)
                    continue;

                signature
                    .Append(palette.regionId)
                    .Append(':')
                    .Append(palette.styleKey)
                    .Append(':')
                    .Append(palette.layoutRuleProfile)
                    .Append('|');
            }
        }

        if (plan.settlements != null)
        {
            for (int i = 0; i < plan.settlements.Count; i++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[i];

                if (settlement == null)
                    continue;

                signature
                    .Append(settlement.settlementId)
                    .Append(':')
                    .Append(settlement.runtimeSiteKitId)
                    .Append('|');
            }
        }

        if (plan.encampments != null)
        {
            for (int i = 0; i < plan.encampments.Count; i++)
            {
                GeneratedEncampmentRecord encampment = plan.encampments[i];

                if (encampment == null)
                    continue;

                signature
                    .Append(encampment.encampmentId)
                    .Append(':')
                    .Append(encampment.runtimeSiteKitId)
                    .Append('|');
            }
        }

        // note: The signature contains only compact semantic intent, never thousands of palette asset records.
        return
            signature.ToString();
    }

    private bool CanBuildCurrentSave()
    {
        PlayerStateManager playerStateManager =
            PlayerStateManager.Instance;

        WorldStateManager worldStateManager =
            WorldStateManager.Instance;

        if (playerStateManager == null ||
            playerStateManager.state == null ||
            worldStateManager == null ||
            worldStateManager.State == null)
        {
            return false;
        }

        if (!GeneratedRpgContentService
                .HasCompletedOrigin(
                    playerStateManager.state))
        {
            return false;
        }

        WorldState world =
            worldStateManager.State;

        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        if (plan == null)
            return false;

        plan.EnsureCollections();

        return
            !string.IsNullOrWhiteSpace(
                plan.worldSeed) &&
            plan.settlements != null &&
            plan.settlements.Count > 0;
    }

    // ------------------------------------------------------------
    // SETTLEMENT PATH
    // ------------------------------------------------------------

    private void BuildMainPath(
        Transform parent,
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        YQGeneratedSettlementCellLayout.Node[] nodes =
            YQGeneratedSettlementCellLayout.GetPathNodes(
                plan,
                settlement,
                palette.layoutRuleProfile);

        int count =
            YQGeneratedSettlementCellLayout.IsComprehensive(
                plan,
                settlement)
                ? nodes.Length
                : Mathf.Min(
                    Mathf.Max(1, pathPieceCount),
                    nodes.Length);

        for (int i = 0;
             i < count;
             i++)
        {
            string seed =
                SettlementSeed(
                    settlement) +
                ":main_path:" +
                i;

            GeneratedAssetReferenceRecord reference =
                YQWorldAssetCatalog
                    .PickAssetForSlot(
                        palette,
                        YQWorldAssetCatalog.SlotPath,
                        seed);

            if (reference == null)
                continue;

            YQGeneratedSettlementCellLayout.Node node =
                nodes[i];

            SpawnRegisteredAsset(
                parent,
                "Path_" +
                i,
                reference,
                node.position,
                Quaternion.Euler(
                    0f,
                    node.yaw,
                    0f),
                registry,
                false);
        }
    }

    // note: Perimeter pieces are limited to the two complete reference cells so structural readability does not become a renderer-cost multiplier everywhere.
    private void BuildSettlementPerimeter(
        Transform parent,
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        YQGeneratedSettlementCellLayout.Node[] nodes =
            YQGeneratedSettlementCellLayout.GetPerimeterNodes(
                plan,
                settlement,
                palette.layoutRuleProfile);

        if (nodes == null || nodes.Length == 0)
            return;

        // note: One settlement owns one defensive construction family; changing the seed per segment produced the previous mismatched-panel ring.
        GeneratedAssetReferenceRecord reference =
            PickSettlementPerimeterReference(
                palette,
                SettlementSeed(settlement) + ":perimeter_family");

        if (reference == null)
            return;

        for (int i = 0; i < nodes.Length; i++)
        {
            YQGeneratedSettlementCellLayout.Node node = nodes[i];
            SpawnRegisteredAsset(
                parent,
                "Perimeter_" + i,
                reference,
                node.position,
                Quaternion.Euler(0f, node.yaw, 0f),
                registry,
                true);
        }
    }

    // note: Use registered perimeter-like props where a palette owns them; a modular wall is the reliable architectural fallback.
    private static GeneratedAssetReferenceRecord PickSettlementPerimeterReference(
        GeneratedRegionAssetPaletteRecord palette,
        string seed)
    {
        if (palette != null && palette.exteriorDeco != null)
        {
            List<GeneratedAssetReferenceRecord> candidates =
                new List<GeneratedAssetReferenceRecord>();

            for (int i = 0; i < palette.exteriorDeco.Count; i++)
            {
                GeneratedAssetReferenceRecord candidate =
                    palette.exteriorDeco[i];

                if (MatchesStructuralPerimeter(candidate))
                    candidates.Add(candidate);
            }

            if (candidates.Count > 0)
            {
                int index = Mathf.Clamp(
                    Mathf.FloorToInt(
                        Deterministic01(seed) * candidates.Count),
                    0,
                    candidates.Count - 1);

                return candidates[index];
            }
        }

        return YQWorldAssetCatalog.PickAssetForSlot(
            palette,
            YQWorldAssetCatalog.SlotWall,
            seed + ":wall_fallback");
    }

    private static bool MatchesStructuralPerimeter(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null)
            return false;

        string text =
            (reference.assetPath + " " + reference.notes + " " +
             string.Join(" ", reference.subTags ?? new List<string>()))
            .ToLowerInvariant();

        return text.Contains("fence") ||
               text.Contains("barrier") ||
               text.Contains("wall") ||
               text.Contains("hedge") ||
               text.Contains("palisade");
    }

    // ------------------------------------------------------------
    // SETTLEMENT BUILDINGS
    // ------------------------------------------------------------

    private void BuildBuildingLots(
        Transform parent,
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        YQGeneratedSettlementCellLayout.Node[] lots =
            YQGeneratedSettlementCellLayout.GetBuildingLots(
                plan,
                settlement,
                palette.layoutRuleProfile);

        bool comprehensive =
            YQGeneratedSettlementCellLayout.IsComprehensive(
                plan,
                settlement);

        int count =
            Mathf.Clamp(
                comprehensive
                    ? Mathf.Max(
                        buildingLotCount,
                        lots.Length)
                    : buildingLotCount,
                1,
                lots.Length);

        if (palette.settlementBuilding == null ||
            palette.settlementBuilding.Count == 0)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Palette '" +
                palette.styleKey +
                "' has no complete settlement_building assets. " +
                "Using its curated modular building cell.");
        }

        int spawned =
            0;

        HashSet<string> usedWholeBuildingPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < count;
             i++)
        {
            string seed =
                SettlementSeed(
                    settlement) +
                ":building:" +
                i;

            YQGeneratedSettlementCellLayout.Node lot =
                lots[i];

            if (IsOrganicSettlementProfile(
                    palette.layoutRuleProfile))
            {
                // note: Small deterministic setbacks break the parade-line silhouette without sacrificing road frontage or allowing arbitrary scatter.
                lot = new YQGeneratedSettlementCellLayout.Node(
                    lot.position +
                    new Vector3(
                        (Deterministic01(seed + ":setback_x") * 2f - 1f) * 0.9f,
                        0f,
                        (Deterministic01(seed + ":setback_z") * 2f - 1f) * 0.7f),
                    lot.yaw +
                    (Deterministic01(seed + ":frontage_yaw") * 2f - 1f) * 4f,
                    lot.purpose);
            }

            string lotPurpose =
                ResolveSettlementLotPurpose(
                    settlement,
                    lot.purpose,
                    i);

            // note: LLM-authored service slots determine district identity while the layout grammar remains deterministic and collision-safe.
            lot = new YQGeneratedSettlementCellLayout.Node(
                lot.position,
                lot.yaw,
                lotPurpose);

            // note: Packs with verified modular recipes bypass noisy pseudo-building discovery entries and assemble only their coherent authored cell.
            GeneratedAssetReferenceRecord reference =
                UsesCuratedModularBuildingCells(
                    palette)
                    ? null
                    : PickSettlementBuildingForPurpose(
                        palette,
                        lotPurpose,
                        seed);

            reference =
                PreferUnusedWholeBuilding(
                    palette,
                    reference,
                    usedWholeBuildingPaths,
                    seed);

            if (reference == null)
            {
                // note: A palette with only modular source pieces still produces a complete, readable lot.
                BuildModularBuildingLot(
                    parent,
                    settlement,
                    palette,
                    registry,
                    seed,
                    i,
                    lot);

                BuildLotPurposeDressing(
                    parent,
                    settlement,
                    palette,
                    registry,
                    lot,
                    i);

                spawned++;
                continue;
            }

            GameObject instance =
                SpawnRegisteredAsset(
                    parent,
                    "SettlementBuilding_" +
                    i +
                    "__" +
                    lot.purpose,
                    reference,
                    lot.position,
                    Quaternion.Euler(
                        0f,
                        lot.yaw,
                        0f),
                    registry,
                    true,
                    true);

            if (instance == null)
            {
                // note: Bounds validation can reject a discovered pseudo-building after selection; recover with the palette's own modular construction kit.
                BuildModularBuildingLot(
                    parent,
                    settlement,
                    palette,
                    registry,
                    seed,
                    i,
                    lot);

                BuildLotPurposeDressing(
                    parent,
                    settlement,
                    palette,
                    registry,
                    lot,
                    i);

                spawned++;
                continue;
            }

            usedWholeBuildingPaths.Add(
                reference.assetPath);

            NormalizeWholeBuildingToLot(
                instance);

            // note: Building prefabs are scaled after spawn, so remove invalid primitive colliders before physics can warn about mirrored children.
            DisableSolidPrimitiveCollidersInHierarchy(
                instance);

            ConfigureBuildingMeshColliders(
                instance);

            /*
             * Convert actual imported door meshes into runtime doors.
             *
             * Unlocked:
             *     E -> open / close
             *
             * Locked:
             *     E -> lockpick UI
             */
            YQGeneratedWorldPopulation
                .ConfigureBuildingDoors(
                    instance,
                    settlement);

            GroundInstance(
                instance);

            BuildLotPurposeDressing(
                parent,
                settlement,
                palette,
                registry,
                lot,
                i);

            spawned++;
        }

        BuildSettlementLandmark(
            parent,
            plan,
            settlement,
            palette,
            registry);

        ValidateSettlementPresentation(
            parent,
            settlement,
            palette,
            count);

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] " +
            "Settlement buildings spawned: " +
            spawned +
            "/" +
            count +
            " using palette " +
            palette.styleKey);
    }

    private static void ValidateSettlementPresentation(
        Transform parent,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        int expectedBuildings)
    {
        if (parent == null)
            return;

        List<Bounds> buildingBounds =
            new List<Bounds>();

        int modularAssemblies =
            0;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null || !child.name.StartsWith("SettlementBuilding_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (child.name.IndexOf("__Modular", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                // note: Fragment-built cells are fallback diagnostics, not release-quality authored buildings, and cannot satisfy the presentation gate.
                modularAssemblies++;
                continue;
            }

            if (TryGetRenderableBounds(child.gameObject, out Bounds bounds) &&
                bounds.size.x >= 1.5f && bounds.size.y >= 1.5f && bounds.size.z >= 1.5f)
            {
                buildingBounds.Add(bounds);
            }
        }

        int overlaps =
            0;

        for (int first = 0; first < buildingBounds.Count; first++)
        {
            Bounds a = buildingBounds[first];
            a.Expand(new Vector3(-0.8f, 0f, -0.8f));

            for (int second = first + 1; second < buildingBounds.Count; second++)
            {
                Bounds b = buildingBounds[second];
                b.Expand(new Vector3(-0.8f, 0f, -0.8f));

                // note: Presentation validation ignores vertical terrain separation and checks only whether reserved architectural footprints collide.
                bool horizontalOverlap =
                    a.min.x < b.max.x && a.max.x > b.min.x &&
                    a.min.z < b.max.z && a.max.z > b.min.z;

                if (horizontalOverlap)
                    overlaps++;
            }
        }

        if (buildingBounds.Count < expectedBuildings || overlaps > 0 || modularAssemblies > 0)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] SETTLEMENT PRESENTATION QUALITY GATE FAILED\n" +
                "Settlement: " + (settlement != null ? settlement.displayName : "<unknown>") + "\n" +
                "Palette: " + (palette != null ? palette.styleKey : "<unknown>") + "\n" +
                "Renderable buildings: " + buildingBounds.Count + "/" + expectedBuildings + "\n" +
                "Fragment-built fallback cells: " + modularAssemblies + "\n" +
                "Overlapping building footprints: " + overlaps);
            return;
        }

        // note: A successful gate proves the settlement contains the full planned building count and no colliding architectural cells before population spawns.
        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] Settlement presentation validated: " +
            buildingBounds.Count + " coherent buildings, 0 overlaps, palette " +
            (palette != null ? palette.styleKey : "<unknown>"));
    }

    private static string ResolveSettlementLotPurpose(
        GeneratedSettlementRecord settlement,
        string layoutFallback,
        int lotIndex)
    {
        if (settlement != null && settlement.serviceSlots != null && lotIndex < settlement.serviceSlots.Count)
        {
            string service = settlement.serviceSlots[lotIndex];
            if (!string.IsNullOrWhiteSpace(service))
                return service.Trim();
        }

        return string.IsNullOrWhiteSpace(layoutFallback) ? "residence" : layoutFallback.Trim();
    }

    private static bool IsOrganicSettlementProfile(
        string layoutRuleProfile)
    {
        string profile =
            (layoutRuleProfile ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        // note: Grid, interior, and monumental packs retain exact axes; villages alone receive restrained hand-authored-looking setbacks.
        return string.IsNullOrWhiteSpace(profile) ||
               profile.Contains("organic") ||
               profile.Contains("rural") ||
               profile.Contains("village");
    }

    private static GeneratedAssetReferenceRecord PickSettlementBuildingForPurpose(
        GeneratedRegionAssetPaletteRecord palette,
        string purpose,
        string seed)
    {
        if (palette == null)
            return null;

        string role = (purpose ?? string.Empty).ToLowerInvariant();
        GeneratedAssetReferenceRecord match = null;

        if (ContainsAny(role, "market", "merchant", "trade", "shop", "vendor", "supply", "barter"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":commerce", "shop", "store", "market", "bank", "saloon", "trader", "merchant");
        else if (ContainsAny(role, "smith", "forge", "workshop", "craft", "alchemy"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":craft", "smith", "forge", "workshop", "stable", "foundry");
        else if (ContainsAny(role, "inn", "tavern", "clinic", "healer", "apothecary"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":hospitality", "inn", "hotel", "saloon", "clinic", "hospital", "apothecary");
        else if (ContainsAny(role, "guard", "command", "watch", "civic", "shrine", "temple"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":civic", "sheriff", "guard", "townhall", "hall", "church", "temple", "tower");
        else
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":residence", "house", "home", "hut", "cabin", "shack", "residence");

        // note: Semantic purpose wins when the pack exposes it; deterministic whole-building selection remains the safe fallback for abstract kits.
        return match ??
               YQWorldAssetCatalog.PickAssetForSlot(
                   palette,
                   YQWorldAssetCatalog.SlotSettlementBuilding,
                   seed + ":whole_building");
    }

    private static GeneratedAssetReferenceRecord PreferUnusedWholeBuilding(
        GeneratedRegionAssetPaletteRecord palette,
        GeneratedAssetReferenceRecord preferred,
        HashSet<string> usedPaths,
        string seed)
    {
        if (preferred == null || palette == null || palette.settlementBuilding == null || usedPaths == null ||
            !usedPaths.Contains(preferred.assetPath) || usedPaths.Count >= palette.settlementBuilding.Count)
        {
            return preferred;
        }

        int start =
            Mathf.Clamp(
                Mathf.FloorToInt(Deterministic01(seed + ":unused_building") * palette.settlementBuilding.Count),
                0,
                palette.settlementBuilding.Count - 1);

        for (int offset = 0; offset < palette.settlementBuilding.Count; offset++)
        {
            // note: A settlement exhausts its authored building variants before repeating one, while selection remains deterministic for the save seed.
            GeneratedAssetReferenceRecord candidate =
                palette.settlementBuilding[(start + offset) % palette.settlementBuilding.Count];

            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.assetPath) && !usedPaths.Contains(candidate.assetPath))
                return candidate;
        }

        return preferred;
    }

    private static bool UsesCuratedModularBuildingCells(
        GeneratedRegionAssetPaletteRecord palette)
    {
        if (palette != null && palette.settlementBuilding != null && palette.settlementBuilding.Count > 0)
        {
            // note: Complete authored cells always outrank procedural fragment assembly, including compatible donor cells registered by the palette curator.
            return false;
        }

        string style =
            palette != null
                ? palette.styleKey ?? string.Empty
                : string.Empty;

        // note: These palettes have explicit compatible floor/wall/door/roof recipes; arbitrary discovery matches are individual kit pieces, not whole buildings.
        return string.Equals(
                   style,
                   "nordic_forest",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   style,
                   "viking_rural",
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   style,
                   "hivemind_rural_town",
                   StringComparison.OrdinalIgnoreCase);
    }

    private void BuildLotPurposeDressing(
        Transform parent,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        YQGeneratedSettlementCellLayout.Node lot,
        int lotIndex)
    {
        if (parent == null || palette == null || registry == null)
            return;

        string purpose = (lot.purpose ?? string.Empty).ToLowerInvariant();
        string seed = SettlementSeed(settlement) + ":district:" + lotIndex;
        GeneratedAssetReferenceRecord anchor = null;
        GeneratedAssetReferenceRecord accent = null;

        if (ContainsAny(purpose, "market", "merchant", "trade", "shop", "vendor", "barter"))
        {
            anchor = FindSemanticPaletteAsset(palette.exteriorDeco, seed + ":storefront", "awning", "stall", "sign", "counter");
            anchor ??= FindSemanticPaletteAsset(palette.floorDeco, seed + ":stock", "crate", "barrel", "basket", "cart");
            accent = FindSemanticPaletteAsset(palette.floorDeco, seed + ":goods", "crate", "barrel", "basket", "sack", "food");
        }
        else if (ContainsAny(purpose, "smith", "forge", "workshop", "craft", "alchemy"))
        {
            anchor = FindSemanticPaletteAsset(palette.floorDeco, seed + ":workyard", "anvil", "forge", "tool", "workbench", "hammer");
            accent = FindSemanticPaletteAsset(palette.lighting, seed + ":worklight", "fire", "torch", "lantern", "brazier");
        }
        else if (ContainsAny(purpose, "inn", "tavern", "clinic", "healer", "apothecary"))
        {
            anchor = FindSemanticPaletteAsset(palette.exteriorDeco, seed + ":public_house", "sign", "awning", "bench", "table");
            accent = FindSemanticPaletteAsset(palette.lighting, seed + ":welcome_light", "lantern", "torch", "fire", "candle");
        }
        else if (ContainsAny(purpose, "guard", "command", "barrack", "watch", "civic", "shrine", "temple"))
        {
            anchor = FindSemanticPaletteAsset(palette.exteriorDeco, seed + ":authority", "banner", "flag", "shield", "statue", "weapon");
            accent = FindSemanticPaletteAsset(palette.lighting, seed + ":authority_light", "brazier", "torch", "lantern", "fire");
        }

        if (anchor == null && accent == null)
            return;

        GameObject districtRoot = new GameObject("DistrictFrontage_" + lotIndex + "__" + SafeName(lot.purpose));
        districtRoot.transform.SetParent(parent, false);
        districtRoot.transform.localPosition = lot.position;
        districtRoot.transform.localRotation = Quaternion.Euler(0f, lot.yaw, 0f);

        // note: A frontage uses at most two role-readable props at fixed authored sockets; it is not an ambient scatter pass.
        SpawnRegisteredAsset(
            districtRoot.transform,
            "POI_Anchor__" + SafeName(lot.purpose),
            anchor,
            new Vector3(-1.35f, 0f, -3.15f),
            Quaternion.identity,
            registry,
            false);

        if (accent != null && !ReferenceEquals(anchor, accent))
        {
            SpawnRegisteredAsset(
                districtRoot.transform,
                "POI_Accent__" + SafeName(lot.purpose),
                accent,
                new Vector3(1.45f, 0f, -3.05f),
                Quaternion.identity,
                registry,
                false);
        }
    }

    private static GeneratedAssetReferenceRecord FindSemanticPaletteAsset(
        List<GeneratedAssetReferenceRecord> references,
        string seed,
        params string[] keywords)
    {
        if (references == null || references.Count == 0 || keywords == null || keywords.Length == 0)
            return null;

        List<GeneratedAssetReferenceRecord> matches = new List<GeneratedAssetReferenceRecord>();
        for (int i = 0; i < references.Count; i++)
        {
            GeneratedAssetReferenceRecord reference = references[i];
            if (reference == null)
                continue;

            string semanticText =
                ((reference.assetPath ?? string.Empty) + " " +
                 (reference.notes ?? string.Empty) + " " +
                 string.Join(" ", reference.subTags ?? new List<string>())).ToLowerInvariant();

            for (int keywordIndex = 0; keywordIndex < keywords.Length; keywordIndex++)
            {
                if (!semanticText.Contains(keywords[keywordIndex]))
                    continue;

                matches.Add(reference);
                break;
            }
        }

        if (matches.Count == 0)
            return null;

        int index = Mathf.Clamp(Mathf.FloorToInt(Deterministic01(seed) * matches.Count), 0, matches.Count - 1);
        return matches[index];
    }

    private void BuildModularBuildingLot(
        Transform parent,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        string seed,
        int lotIndex,
        YQGeneratedSettlementCellLayout.Node lot)
    {
        // note: This root owns one coherent fallback building and preserves the same lot anchor used by complete imported prefabs.
        GameObject modularRoot =
            new GameObject(
                "SettlementBuilding_" +
                lotIndex +
                "__" +
                lot.purpose +
                "__Modular");

        modularRoot.transform.SetParent(
            parent,
            false);

        modularRoot.transform.localPosition =
            lot.position;

        modularRoot.transform.localRotation =
            Quaternion.identity;

        BuildModularBuilding(
            modularRoot.transform,
            settlement,
            palette,
            registry,
            seed,
            lot.purpose);

        // note: Assemble against unrotated rendered bounds first, then turn the complete authored cell toward its assigned street frontage.
        modularRoot.transform.localRotation =
            Quaternion.Euler(
                0f,
                lot.yaw,
                0f);

        NormalizeCuratedBuildingCellScale(
            modularRoot);

        DisableSolidPrimitiveCollidersInHierarchy(
            modularRoot);

        ConfigureBuildingMeshColliders(
            modularRoot);

        YQGeneratedWorldPopulation
            .ConfigureBuildingDoors(
                modularRoot,
                settlement);

        GroundInstance(modularRoot);
    }

    private static void NormalizeCuratedBuildingCellScale(
        GameObject modularRoot)
    {
        if (modularRoot == null ||
            !TryGetRenderableBounds(
                modularRoot,
                out Bounds bounds))
        {
            return;
        }

        const float MaximumCellWidth =
            9.5f;

        const float MaximumCellHeight =
            7.5f;

        float horizontal =
            Mathf.Max(
                Mathf.Abs(bounds.size.x),
                Mathf.Abs(bounds.size.z));

        float height =
            Mathf.Abs(bounds.size.y);

        float scale =
            Mathf.Min(
                1f,
                horizontal > 0.01f
                    ? MaximumCellWidth / horizontal
                    : 1f,
                height > 0.01f
                    ? MaximumCellHeight / height
                    : 1f);

        if (scale >= 0.999f)
            return;

        // note: Imported kit units vary by pack; cap the completed cell as one object so doors, roofs, and walls keep their authored proportions and fit their street lot.
        modularRoot.transform.localScale *=
            Mathf.Clamp(
                scale,
                0.15f,
                    1f);
    }

    private static void NormalizeWholeBuildingToLot(
        GameObject building)
    {
        if (building == null || !TryGetRenderableBounds(building, out Bounds bounds))
            return;

        const float MaximumLotWidth = 13.5f;
        const float MaximumLotHeight = 18f;

        float horizontal =
            Mathf.Max(
                Mathf.Abs(bounds.size.x),
                Mathf.Abs(bounds.size.z));

        float height =
            Mathf.Abs(bounds.size.y);

        float fit =
            Mathf.Min(
                1f,
                horizontal > 0.01f ? MaximumLotWidth / horizontal : 1f,
                height > 0.01f ? MaximumLotHeight / height : 1f);

        if (fit >= 0.999f)
            return;

        // note: Complete prefabs keep enough authored scale to read as architecture; the district grammar now reserves real building-sized parcels.
        building.transform.localScale *=
            Mathf.Clamp(
                fit,
                0.35f,
                1f);
    }

    private static void ConfigureBuildingMeshColliders(
        GameObject root)
    {
        if (root == null)
            return;

        /*
         * Generated settlement buildings are static architecture.
         *
         * Imported Rigidbody components are removed so non-convex
         * MeshColliders can preserve doors, passages and interiors.
         */
        Rigidbody[] bodies =
            root.GetComponentsInChildren<Rigidbody>(
                true);

        for (int i = 0;
             i < bodies.Length;
             i++)
        {
            if (bodies[i] != null)
            {
                UnityEngine.Object.Destroy(
                    bodies[i]);
            }
        }

        MeshFilter[] meshFilters =
            root.GetComponentsInChildren<MeshFilter>(
                true);

        int meshColliderCount =
            0;

        int primitiveColliderCount =
            0;

        for (int i = 0;
             i < meshFilters.Length;
             i++)
        {
            MeshFilter filter =
                meshFilters[i];

            if (filter == null ||
                filter.sharedMesh == null)
            {
                continue;
            }

            if (IsRedundantLodCollisionMesh(
                    filter.transform))
            {
                continue;
            }

            int triangleCount =
                EstimateTriangleCount(
                    filter.sharedMesh);

            if (triangleCount >
                MaximumGeneratedBuildingMeshColliderTriangles)
            {
                // note: Huge imported architecture meshes can freeze physics setup; use one bounds collider instead.
                AddApproximateBoundsCollider(
                    root);

                continue;
            }

            if (meshColliderCount >=
                MaximumGeneratedBuildingMeshColliders)
            {
                // note: Dense imported buildings keep one inexpensive root bounds collider after the detailed collision budget is exhausted.
                AddApproximateBoundsCollider(
                    root);

                continue;
            }

            GameObject meshObject =
                filter.gameObject;

            Collider[] existingColliders =
                meshObject.GetComponents<Collider>();

            MeshCollider meshCollider =
                null;

            for (int colliderIndex = 0;
                 colliderIndex <
                    existingColliders.Length;
                 colliderIndex++)
            {
                Collider collider =
                    existingColliders[
                        colliderIndex];

                if (collider == null)
                    continue;

                if (collider is
                    MeshCollider existingMeshCollider)
                {
                    if (meshCollider == null)
                    {
                        meshCollider =
                            existingMeshCollider;
                    }
                    else
                    {
                        UnityEngine.Object.Destroy(
                            existingMeshCollider);
                    }

                    continue;
                }

                /*
                 * Preserve triggers because imported building
                 * interactions may depend on them.
                 */
                if (collider.isTrigger)
                    continue;

                if (collider is BoxCollider ||
    collider is SphereCollider ||
    collider is CapsuleCollider)
                {
                    collider.enabled =
                        false;

                    UnityEngine.Object.Destroy(
                        collider);

                    primitiveColliderCount++;
                }
            }

            if (meshCollider == null)
            {
                meshCollider =
                    meshObject.AddComponent<
                        MeshCollider>();
            }

            meshCollider.sharedMesh =
                null;

            meshCollider.convex =
                false;

            meshCollider.isTrigger =
                false;

            meshCollider.enabled =
                true;

            meshCollider.sharedMesh =
                filter.sharedMesh;

            meshColliderCount++;
        }

        if (primitiveColliderCount > 0 &&
            meshColliderCount == 0)
        {
            // note: Only warn when collider cleanup left a building without mesh collision; normal success is intentionally quiet.
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Building collision removed " +
                primitiveColliderCount +
                " primitive collider(s) but found no mesh colliders for " +
                root.name);
        }
    }

    private static bool IsRedundantLodCollisionMesh(
        Transform meshTransform)
    {
        Transform current =
            meshTransform;

        while (current != null)
        {
            string name =
                current.name ?? string.Empty;

            bool lowerDetailLevel =
                false;

            for (int level = 1; level <= 8; level++)
            {
                if (name.IndexOf("LOD" + level, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    name.IndexOf("LOD_" + level, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    lowerDetailLevel = true;
                    break;
                }
            }

            if (lowerDetailLevel)
            {
                // note: Visual LOD1+ meshes overlap LOD0 and must never become duplicate physical collision surfaces.
                return true;
            }

            current =
                current.parent;
        }

        return false;
    }

    private static int EstimateTriangleCount(
        Mesh mesh)
    {
        if (mesh == null)
            return 0;

        long indexCount =
            0L;

        int subMeshCount =
            Mathf.Max(
                1,
                mesh.subMeshCount);

        for (int i = 0;
             i < subMeshCount;
             i++)
        {
            indexCount +=
                (long)mesh.GetIndexCount(
                    i);
        }

        return
            (int)Mathf.Min(
                int.MaxValue,
                indexCount / 3L);
    }

    private static void AddApproximateBoundsCollider(
        GameObject root)
    {
        if (root == null)
            return;

        BoxCollider existingRootBox =
            root.GetComponent<BoxCollider>();

        if (existingRootBox != null && existingRootBox.enabled && !existingRootBox.isTrigger)
        {
            return;
        }

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        if (renderers == null ||
            renderers.Length == 0)
        {
            return;
        }

        Bounds bounds =
            default;

        bool hasBounds =
            false;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds =
                    renderer.bounds;

                hasBounds =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        if (!hasBounds)
            return;

        BoxCollider collider =
            root.AddComponent<BoxCollider>();

        collider.center =
            root.transform.InverseTransformPoint(
                bounds.center);

        Vector3 localSize =
            root.transform.InverseTransformVector(
                bounds.size);

        // note: BoxCollider size must be positive even when imported meshes use mirrored child transforms.
        collider.size =
            new Vector3(
                Mathf.Abs(localSize.x),
                Mathf.Abs(localSize.y),
                Mathf.Abs(localSize.z));
    }

    // ------------------------------------------------------------
    // CURATED MODULAR BUILDING CELLS
    // ------------------------------------------------------------

    private void BuildModularBuilding(
        Transform parent,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        string seed,
        string purpose)
    {
        CuratedBuildingCellRecipe recipe =
            ResolveCuratedBuildingCellRecipe(
                palette,
                seed,
                purpose);

        // note: A recipe is selected as a complete construction family; individual structural slots are never randomized across incompatible kits.
        GameObject floor = SpawnRegisteredAsset(
            parent,
            "Floor",
            recipe.floor,
            Vector3.zero,
            Quaternion.identity,
            registry,
            true,
            groundInstance: false);

        GameObject back = SpawnRegisteredAsset(
            parent,
            "Wall_Back",
            recipe.backWall,
            Vector3.zero,
            Quaternion.Euler(
                0f,
                180f,
                0f),
            registry,
            true,
            groundInstance: false);

        GameObject left = SpawnRegisteredAsset(
            parent,
            "Wall_Left",
            recipe.sideWall,
            Vector3.zero,
            Quaternion.Euler(
                0f,
                90f,
                0f),
            registry,
            true,
            groundInstance: false);

        GameObject right = SpawnRegisteredAsset(
            parent,
            "Wall_Right",
            recipe.sideWall,
            Vector3.zero,
            Quaternion.Euler(
                0f,
                -90f,
                0f),
            registry,
            true,
            groundInstance: false);

        GameObject front = SpawnRegisteredAsset(
            parent,
            "Wall_Front",
            recipe.frontWall ?? recipe.backWall,
            Vector3.zero,
            Quaternion.identity,
            registry,
            true,
            groundInstance: false);

        GameObject door = SpawnRegisteredAsset(
            parent,
            "Door_Front",
            recipe.door,
            Vector3.zero,
            Quaternion.identity,
            registry,
            true,
            groundInstance: false);

        GameObject roof = SpawnRegisteredAsset(
            parent,
            "Roof",
            recipe.roof,
            Vector3.zero,
            Quaternion.identity,
            registry,
            false,
            groundInstance: false);

        ArrangeCuratedBuildingCell(
            parent,
            floor,
            back,
            left,
            right,
            front,
            door,
            roof);
    }

    private sealed class CuratedBuildingCellRecipe
    {
        public GeneratedAssetReferenceRecord floor;
        public GeneratedAssetReferenceRecord backWall;
        public GeneratedAssetReferenceRecord sideWall;
        public GeneratedAssetReferenceRecord frontWall;
        public GeneratedAssetReferenceRecord door;
        public GeneratedAssetReferenceRecord roof;
    }

    private static CuratedBuildingCellRecipe ResolveCuratedBuildingCellRecipe(
        GeneratedRegionAssetPaletteRecord palette,
        string seed,
        string purpose)
    {
        string style = palette != null ? palette.styleKey ?? string.Empty : string.Empty;
        string role = purpose ?? string.Empty;

        if (string.Equals(style, "nordic_forest", StringComparison.OrdinalIgnoreCase))
        {
            bool tall = ContainsAny(role, "civic", "inn", "clinic", "command");
            bool log = !tall && Deterministic01(seed + ":nordic_family") < 0.55f;
            string wallName = tall ? "SM_WallTall01.prefab" : log ? "SM_LogWall01.prefab" : "SM_Wall01.prefab";
            string frontName = tall ? "SM_WallTallDoor.prefab" : log ? "SM_LogWallDoor.prefab" : "SM_WallDoor.prefab";
            string roofName = tall ? "SM_RoofGableTall01.prefab" : log ? "SM_LogRoofGable01.prefab" : "SM_ThatchRoof01.prefab";

            // note: Nordic variants remain within one log, plaster, or tall civic vocabulary for the entire lot.
            GeneratedAssetReferenceRecord wall = FindPaletteAssetEndingWith(palette.wall, wallName);
            return new CuratedBuildingCellRecipe
            {
                floor = FindPaletteAssetEndingWith(palette.floor, "SM_SingleTile.prefab"),
                backWall = wall,
                sideWall = wall,
                frontWall = FindPaletteAssetEndingWith(palette.wall, frontName),
                roof = FindPaletteAssetEndingWith(palette.roof, roofName)
            };
        }

        if (string.Equals(style, "viking_rural", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(style, "hivemind_modular_viking_village", StringComparison.OrdinalIgnoreCase))
        {
            // note: The imported House2 modules are an authored matching set and therefore stay together as one deterministic construction cell.
            return new CuratedBuildingCellRecipe
            {
                floor = FindPaletteAssetEndingWith(palette.floor, "SM_House1_Floor.prefab"),
                backWall = FindPaletteAssetEndingWith(palette.wall, "SM_House2_BackWall.prefab"),
                sideWall = FindPaletteAssetEndingWith(palette.wall, "SM_House2_SideWall.prefab"),
                frontWall = FindPaletteAssetEndingWith(palette.wall, "SM_House2_FrontWall.prefab"),
                door = FindPaletteAssetEndingWith(palette.door, "SM_House2_Door.prefab"),
                roof = FindPaletteAssetEndingWith(palette.roof, "SM_House2_Roof.prefab")
            };
        }

        if (string.Equals(style, "hivemind_rural_town", StringComparison.OrdinalIgnoreCase))
        {
            // note: RuralTown's four-metre wall system supplies its own opening, leaf, floor, and roof instead of borrowing another palette.
            return new CuratedBuildingCellRecipe
            {
                floor = FindPaletteAssetEndingWith(palette.floor, "SM_Floor_4m.prefab"),
                backWall = FindPaletteAssetEndingWith(palette.wall, "SM_Wall_4m.prefab"),
                sideWall = FindPaletteAssetEndingWith(palette.wall, "SM_Wall_4m.prefab"),
                frontWall = FindPaletteAssetEndingWith(palette.door, "SM_Wall_Door_4m.prefab"),
                door = FindPaletteAssetEndingWith(palette.door, "SM_Door_01.prefab"),
                roof = FindPaletteAssetEndingWith(palette.roof, "SM_Roof_5m_01.prefab") ??
                       FindPaletteAssetEndingWith(palette.roof, "SM_Roof.prefab")
            };
        }

        if (string.Equals(style, "hivemind_pirate_island", StringComparison.OrdinalIgnoreCase))
        {
            // note: Pirate settlements use one six-metre shack vocabulary instead of inheriting a random Viking fallback skeleton.
            return new CuratedBuildingCellRecipe
            {
                floor = FindPaletteAssetEndingWith(palette.floor, "SM_FloorWood6x6m_01.prefab"),
                backWall = FindPaletteAssetEndingWith(palette.wall, "SM_ShackSide6m_01.prefab"),
                sideWall = FindPaletteAssetEndingWith(palette.wall, "SM_ShackSide6m_01.prefab"),
                frontWall = FindPaletteAssetEndingWith(palette.wall, "SM_ShackFront6m_01.prefab"),
                door = FindPaletteAssetEndingWith(palette.door, "SM_DoorShack_01.prefab"),
                roof = FindPaletteAssetEndingWith(palette.roof, "SM_Roof6x6m_01.prefab")
            };
        }

        GeneratedAssetReferenceRecord fallbackWall =
            YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotWall, seed + ":coherent_wall");

        // note: Unknown palettes still repeat one structural wall rather than rolling four unrelated architectural fragments.
        return new CuratedBuildingCellRecipe
        {
            floor = YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotFloor, seed + ":floor"),
            backWall = fallbackWall,
            sideWall = fallbackWall,
            frontWall = fallbackWall,
            door = YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotDoor, seed + ":door"),
            roof = YQWorldAssetCatalog.PickAssetForSlot(palette, YQWorldAssetCatalog.SlotRoof, seed + ":roof")
        };
    }

    private static void ArrangeCuratedBuildingCell(
        Transform parent,
        GameObject floor,
        GameObject back,
        GameObject left,
        GameObject right,
        GameObject front,
        GameObject door,
        GameObject roof)
    {
        float backSpan = RenderedSpan(back, true, 4.5f);
        float frontSpan = RenderedSpan(front, true, backSpan);
        float sideSpan = RenderedSpan(left, false, backSpan);
        float width = Mathf.Clamp(Mathf.Max(backSpan, frontSpan), 3.5f, 9f);
        float depth = Mathf.Clamp(sideSpan, 3.5f, 9f);

        // note: Real rendered dimensions, not assumed prefab pivots, determine the footprint shared by every structural part.
        ScaleModuleAlongLocalX(back, width, true);
        ScaleModuleAlongLocalX(front, width, true);
        ScaleModuleAlongLocalX(left, depth, false);
        ScaleModuleAlongLocalX(right, depth, false);

        float floorTop = 0f;
        FitHorizontalFootprint(floor, width, depth);
        AlignModule(parent, floor, 0f, 0f, 0f);
        if (TryGetRenderableBounds(floor, out Bounds floorBounds))
            floorTop = parent.InverseTransformPoint(floorBounds.max).y;

        float backThickness = RenderedThickness(back, false, 0.18f);
        float frontThickness = RenderedThickness(front, false, backThickness);
        float leftThickness = RenderedThickness(left, true, 0.18f);
        float rightThickness = RenderedThickness(right, true, leftThickness);

        AlignModule(parent, back, 0f, floorTop, depth * 0.5f - backThickness * 0.5f);
        AlignModule(parent, front, 0f, floorTop, -depth * 0.5f + frontThickness * 0.5f);
        AlignModule(parent, left, -width * 0.5f + leftThickness * 0.5f, floorTop, 0f);
        AlignModule(parent, right, width * 0.5f - rightThickness * 0.5f, floorTop, 0f);

        if (door != null)
        {
            // note: A separate leaf is centered in the authored front opening and remains the only movable door object.
            AlignModule(parent, door, 0f, floorTop, -depth * 0.5f - RenderedThickness(door, false, 0.08f) * 0.25f);
        }

        float wallTop = Mathf.Max(RenderedTop(parent, back, floorTop + 2.5f), RenderedTop(parent, front, floorTop + 2.5f));
        wallTop = Mathf.Max(wallTop, RenderedTop(parent, left, wallTop));
        wallTop = Mathf.Max(wallTop, RenderedTop(parent, right, wallTop));

        FitHorizontalFootprint(roof, width * 1.12f, depth * 1.12f);
        AlignModule(parent, roof, 0f, wallTop - 0.08f, 0f);
    }

    private static void AlignModule(Transform parent, GameObject module, float centerX, float bottomY, float centerZ)
    {
        if (parent == null || module == null || !TryGetRenderableBounds(module, out Bounds bounds))
            return;

        Vector3 targetCenter = parent.TransformPoint(new Vector3(centerX, 0f, centerZ));
        float targetBottom = parent.TransformPoint(new Vector3(0f, bottomY, 0f)).y;
        module.transform.position += new Vector3(targetCenter.x - bounds.center.x, targetBottom - bounds.min.y, targetCenter.z - bounds.center.z);
    }

    private static void ScaleModuleAlongLocalX(GameObject module, float targetSpan, bool measureWorldX)
    {
        if (module == null || !TryGetRenderableBounds(module, out Bounds bounds))
            return;

        float currentSpan = measureWorldX ? bounds.size.x : bounds.size.z;
        if (currentSpan <= 0.01f)
            return;

        // note: Modular kits are allowed a small horizontal snap correction while their vertical authored proportions remain untouched.
        Vector3 scale = module.transform.localScale;
        scale.x *= Mathf.Clamp(targetSpan / currentSpan, 0.75f, 1.35f);
        module.transform.localScale = scale;
    }

    private static void FitHorizontalFootprint(GameObject module, float targetWidth, float targetDepth)
    {
        if (module == null || !TryGetRenderableBounds(module, out Bounds bounds) || bounds.size.x <= 0.01f || bounds.size.z <= 0.01f)
            return;

        Vector3 scale = module.transform.localScale;
        scale.x *= Mathf.Clamp(targetWidth / bounds.size.x, 0.5f, 2.5f);
        scale.z *= Mathf.Clamp(targetDepth / bounds.size.z, 0.5f, 2.5f);
        module.transform.localScale = scale;
    }

    private static float RenderedSpan(GameObject module, bool worldX, float fallback)
    {
        return module != null && TryGetRenderableBounds(module, out Bounds bounds)
            ? (worldX ? bounds.size.x : bounds.size.z)
            : fallback;
    }

    private static float RenderedThickness(GameObject module, bool worldX, float fallback)
    {
        return module != null && TryGetRenderableBounds(module, out Bounds bounds)
            ? Mathf.Max(0.04f, worldX ? bounds.size.x : bounds.size.z)
            : fallback;
    }

    private static float RenderedTop(Transform parent, GameObject module, float fallback)
    {
        return parent != null && module != null && TryGetRenderableBounds(module, out Bounds bounds)
            ? parent.InverseTransformPoint(bounds.max).y
            : fallback;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        string text = value != null ? value.ToLowerInvariant() : string.Empty;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (text.Contains(tokens[i]))
                return true;
        }

        return false;
    }

    private static GeneratedAssetReferenceRecord FindPaletteAssetEndingWith(
        List<GeneratedAssetReferenceRecord> references,
        string pathSuffix)
    {
        if (references == null ||
            string.IsNullOrWhiteSpace(pathSuffix))
        {
            return null;
        }

        for (int i = 0;
             i < references.Count;
             i++)
        {
            GeneratedAssetReferenceRecord reference =
                references[i];

            if (reference != null &&
                !string.IsNullOrWhiteSpace(reference.assetPath) &&
                reference.assetPath.EndsWith(
                    pathSuffix,
                    StringComparison.OrdinalIgnoreCase))
            {
                // note: Exact module suffixes bind a curated cell without exposing Unity paths to the LLM.
                return reference;
            }
        }

        return null;
    }

    // ------------------------------------------------------------
    // LANDMARK
    // ------------------------------------------------------------

    private void BuildSettlementLandmark(
        Transform parent,
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        string seed =
            SettlementSeed(
                settlement) +
            ":landmark";

        GeneratedAssetReferenceRecord reference =
            YQWorldAssetCatalog
                .PickAssetForSlot(
                    palette,
                    YQWorldAssetCatalog
                        .SlotLargeStructure,
                    seed);

        if (reference == null)
            return;

        YQGeneratedSettlementCellLayout.Template template =
            YQGeneratedSettlementCellLayout.ResolveTemplate(
                plan,
                settlement);

        if (!IsCoherentSettlementLandmark(
                reference,
                template))
        {
            // note: Cave mouths, generic ruins, and military props remain valid POIs but cannot be dropped into a civilian plaza as civic landmarks.
            return;
        }

        SpawnRegisteredAsset(
            parent,
            "SettlementLandmark",
            reference,
            YQGeneratedSettlementCellLayout
                .GetLandmarkPosition(
                    plan,
                    settlement),
            Quaternion.Euler(
                0f,
                DeterministicQuarterTurn(
                    seed),
                0f),
            registry,
            true);
    }

    private static bool IsCoherentSettlementLandmark(
        GeneratedAssetReferenceRecord reference,
        YQGeneratedSettlementCellLayout.Template template)
    {
        if (reference == null)
            return false;

        string semantic =
            ((reference.assetPath ?? string.Empty) + " " +
             (reference.notes ?? string.Empty) + " " +
             string.Join(" ", reference.subTags ?? new List<string>()))
                .ToLowerInvariant();

        if (ContainsAny(
                semantic,
                "cave",
                "underground",
                "sewer",
                "tunnel",
                "dungeon",
                "debris",
                "rock pile"))
        {
            return false;
        }

        switch (template)
        {
            case YQGeneratedSettlementCellLayout.Template.DenseCity:
                return ContainsAny(semantic, "building", "tower", "cathedral", "temple", "hospital", "arena", "villa", "hall", "palace", "keep");

            case YQGeneratedSettlementCellLayout.Template.MarketVillage:
                return ContainsAny(semantic, "fountain", "statue");

            case YQGeneratedSettlementCellLayout.Template.FortifiedOutpost:
                return ContainsAny(semantic, "watch", "tower", "gate", "fort", "camp", "barrack", "command", "palisade");

            default:
                return false;
        }
    }

    // ------------------------------------------------------------
    // DECORATION
    // ------------------------------------------------------------

    private void BuildDecorations(
        Transform parent,
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        YQGeneratedSettlementCellLayout.Node[] civicNodes =
            YQGeneratedSettlementCellLayout
                .GetCivicDecorationNodes(
                    plan,
                    settlement,
                    palette.layoutRuleProfile);

        bool comprehensive =
            civicNodes != null &&
            civicNodes.Length > 0;

        int count =
            comprehensive
                ? Mathf.Min(
                    Mathf.Max(
                        decorationCount,
                        civicNodes.Length),
                    civicNodes.Length)
                : Mathf.Max(
                    0,
                    decorationCount);

        for (int i = 0;
             i < count;
             i++)
        {
            string seed =
                SettlementSeed(
                    settlement) +
                ":deco:" +
                i;

            string slot =
                i % 4 == 0
                    ? YQWorldAssetCatalog.SlotLighting
                    : comprehensive && i % 4 == 1
                        ? YQWorldAssetCatalog.SlotExteriorDeco
                        : YQWorldAssetCatalog.SlotFloorDeco;

            GeneratedAssetReferenceRecord reference =
                PickAmbientSettlementDecoration(
                    palette,
                    slot,
                    seed);

            if (reference == null)
                continue;

            if (!IsAmbientSettlementDecoration(
                    reference))
            {
                // note: Storefront cells and whole structures belong to purposeful lots, never the generic civic-prop pass.
                continue;
            }

            Vector3 local;
            float yaw;
            if (comprehensive)
            {
                // note: Civic props sit on authored procedural anchors instead of randomly blocking the market lane.
                YQGeneratedSettlementCellLayout.Node node =
                    civicNodes[i];
                local = node.position;
                yaw = node.yaw;
            }
            else
            {
                float angle = Deterministic01(seed + ":angle") * Mathf.PI * 2f;
                float radius = Mathf.Lerp(6f, 17f, Deterministic01(seed + ":radius"));
                local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                yaw = DeterministicQuarterTurn(seed);
            }

            SpawnRegisteredAsset(
                parent,
                "Deco_" +
                i,
                reference,
                local,
                Quaternion.Euler(
                    0f,
                    yaw,
                    0f),
                registry,
                false);
        }
    }

    private static bool IsAmbientSettlementDecoration(
        GeneratedAssetReferenceRecord reference)
    {
        if (reference == null || string.IsNullOrWhiteSpace(reference.assetPath))
            return false;

        string normalized =
            reference.assetPath.Replace('\\', '/');

        int slash =
            normalized.LastIndexOf('/');

        string fileName =
            (slash >= 0 ? normalized.Substring(slash + 1) : normalized).ToLowerInvariant();

        string semantic =
            (fileName + " " +
             (reference.notes ?? string.Empty) + " " +
             string.Join(" ", reference.subTags ?? new List<string>()))
                .ToLowerInvariant();

        if (ContainsAny(
                semantic,
                "weapon",
                "sword",
                "axe",
                "arrow",
                "shield",
                "helmet",
                "armor",
                "bottle",
                "cup",
                "vase",
                "bone",
                "skull",
                "house",
                "building",
                "townhall",
                "church",
                "temple",
                "hospital",
                "arena",
                "complete",
                "merged"))
        {
            return false;
        }

        // note: Ambient sockets accept only street-scale furniture and lighting; handheld clutter belongs inside purposeful building frontage cells.
        return ContainsAny(
            semantic,
            "torch",
            "lamp",
            "lantern",
            "fire",
            "brazier",
            "candle",
            "fence",
            "barrel",
            "crate",
            "box",
            "cart",
            "wagon",
            "bench",
            "table",
            "chair",
            "well",
            "hay",
            "trough",
            "planter",
            "sign",
            "stall",
            "awning",
            "post",
            "banner");
    }

    private static GeneratedAssetReferenceRecord PickAmbientSettlementDecoration(
        GeneratedRegionAssetPaletteRecord palette,
        string slot,
        string seed)
    {
        List<GeneratedAssetReferenceRecord> source =
            YQWorldAssetCatalog.GetSlotList(
                palette,
                slot);

        if (source == null || source.Count == 0)
            return null;

        List<GeneratedAssetReferenceRecord> eligible =
            new List<GeneratedAssetReferenceRecord>();

        for (int i = 0; i < source.Count; i++)
        {
            if (IsAmbientSettlementDecoration(source[i]))
                eligible.Add(source[i]);
        }

        if (eligible.Count == 0)
            return null;

        int index =
            Mathf.Clamp(
                Mathf.FloorToInt(
                    Deterministic01(seed + ":street_furniture") *
                    eligible.Count),
                0,
                eligible.Count - 1);

        return eligible[index];
    }

    // ------------------------------------------------------------
    // VEGETATION
    // ------------------------------------------------------------

    private void BuildVegetation(
        Transform parent,
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        YQGeneratedSettlementCellLayout.Node[] shrubNodes =
            YQGeneratedSettlementCellLayout
                .GetShrubNodes(
                    plan,
                    settlement,
                    palette.layoutRuleProfile);

        bool comprehensive =
            shrubNodes != null &&
            shrubNodes.Length > 0;

        int count =
            comprehensive
                ? Mathf.Min(
                    Mathf.Max(
                        vegetationCount,
                        shrubNodes.Length),
                    shrubNodes.Length)
                : Mathf.Max(
                    0,
                    vegetationCount);

        for (int i = 0;
             i < count;
             i++)
        {
            string seed =
                SettlementSeed(
                    settlement) +
                ":vegetation:" +
                i;

            string slot =
                comprehensive
                    ? YQWorldAssetCatalog.SlotVegetation
                    : i % 4 == 0
                        ? YQWorldAssetCatalog.SlotRock
                        : YQWorldAssetCatalog.SlotVegetation;

            GeneratedAssetReferenceRecord reference =
                YQWorldAssetCatalog
                    .PickAssetForSlot(
                        palette,
                        slot,
                        seed);
            if (reference == null)
                continue;

            /*
             * Large terrain features are represented by the physical Terrain
             * landform system, never settlement-edge decorative scatter.
             */
            if (slot ==
                    YQWorldAssetCatalog.SlotRock &&
                YQGeneratedWorldEnvironment
                    .IsLargeTerrainFeatureReference(
                        reference))
            {
                continue;
            }

            
                

            Vector3 local;
            float yaw;
            if (comprehensive)
            {
                // note: These are low-density curb shrubs, never a forest ring that hides buildings or overwhelms the GPU.
                YQGeneratedSettlementCellLayout.Node node =
                    shrubNodes[i];
                local = node.position;
                yaw = node.yaw;
            }
            else
            {
                float angle = Deterministic01(seed + ":angle") * Mathf.PI * 2f;
                float radius = Mathf.Lerp(30f, 40f, Deterministic01(seed + ":radius"));
                local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                yaw = Deterministic01(seed + ":yaw") * 360f;
            }

            SpawnRegisteredAsset(
                parent,
                "OuterDressing_" +
                i,
                reference,
                local,
                Quaternion.Euler(
                    0f,
                    yaw,
                    0f),
                registry,
                false);
        }
    }

    // ------------------------------------------------------------
    // REGISTERED ASSET SPAWNING
    // ------------------------------------------------------------

    private GameObject SpawnRegisteredAsset(
    Transform parent,
    string objectName,
    GeneratedAssetReferenceRecord reference,
    Vector3 localPosition,
    Quaternion localRotation,
    YQRuntimeWorldAssetRegistry registry,
    bool keepColliders,
    bool suppressNegativeScaleBoxWarnings = false,
    bool groundInstance = true)
    {
        if (reference == null ||
            registry == null ||
            string.IsNullOrWhiteSpace(
                reference.assetPath))
        {
            return null;
        }

        GameObject prefab =
            registry.ResolvePrefab(
                reference.assetPath);

        if (prefab == null)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Runtime registry could not resolve: " +
                reference.assetPath);

            return null;
        }

        if (!keepColliders &&
            HasMissingMonoBehaviourInHierarchy(
                prefab))
        {
            LogSkippedMissingScriptPrefab(
                reference.assetPath);

            return null;
        }

        List<BoxCollider> temporarilyDisabledBoxes =
            DisableNegativeScaleSolidPrefabBoxColliders(
                prefab);

        bool replacedInvalidPrefabCollision =
            temporarilyDisabledBoxes != null &&
            temporarilyDisabledBoxes.Count > 0;

        GameObject instance =
            null;

        try
        {
            instance =
                Instantiate(
                    prefab,
                    parent);
        }
        finally
        {
            RestoreTemporarilyDisabledPrefabBoxColliders(
                temporarilyDisabledBoxes);
        }

        if (instance == null)
            return null;

        if (suppressNegativeScaleBoxWarnings)
        {
            // note: Disable cloned primitive colliders before assigning generated scale; mesh collision is configured later for buildings.
            DisableSolidPrimitiveCollidersInHierarchy(
                instance);
        }
        else if (replacedInvalidPrefabCollision && keepColliders)
        {
            // note: Invalid mirrored prefab boxes were cloned disabled; one positive root approximation preserves landmark collision without Console spam.
            AddApproximateBoundsCollider(
                instance);
        }

        // note: Runtime-selected marketplace prefabs are curated visually, but their bundled demo audio is never procedural gameplay.
        YQImportedDemoAudioFirewall
            .SanitizeGeneratedPrefabAudio(
                instance,
                nameof(YQGeneratedWorldRuntimeBuilder));

        instance.name =
            objectName +
            "__" +
            prefab.name;

        instance.transform.localPosition =
            localPosition;

        instance.transform.localRotation =
            localRotation;

        float scale =
            Mathf.Lerp(
                Mathf.Max(
                    0.01f,
                    reference.scaleMin),
                Mathf.Max(
                    reference.scaleMin,
                    reference.scaleMax),
                Deterministic01(
                    reference.assetPath +
                    ":" +
                    objectName +
                    ":scale"));

        // note: Imported prefabs frequently encode unit conversion in their root scale; multiply the authored value instead of replacing it with a generated uniform scale.
        instance.transform.localScale =
            Vector3.Scale(
                instance.transform.localScale,
                Vector3.one * scale);

        if (ShouldRejectSettlementPlacement(
                reference,
                instance,
                out string placementReason))
        {
            LogSkippedUnsuitableSettlementAsset(
                reference.assetPath,
                reference.slotTag,
                placementReason);

            Destroy(instance);

            return null;
        }

        /*
         * Apply editor-baked source-material correction BEFORE
         * URP runtime repair.
         */
        int materialOverridesApplied =
            registry.ApplyMaterialOverrides(
                reference.assetPath,
                instance);

        if (materialOverridesApplied > 0)
        {
            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] Applied " +
                materialOverridesApplied +
                " baked material override(s) to " +
                instance.name);
        }

        PrepareEnvironmentInstance(
            instance,
            keepColliders);

        if (groundInstance)
        {
            // note: Whole objects ground immediately; curated modular cells preserve their authored local Y offsets and ground once at the cell root.
            GroundInstance(
                instance);
        }

        return instance;
    }

    private static bool ShouldRejectSettlementPlacement(
        GeneratedAssetReferenceRecord reference,
        GameObject instance,
        out string reason)
    {
        reason = string.Empty;
        if (reference == null || instance == null ||
            !TryGetRenderableBounds(instance, out Bounds bounds))
        {
            return false;
        }

        string slot = (reference.slotTag ?? string.Empty).Trim().ToLowerInvariant();
        float width = Mathf.Abs(bounds.size.x);
        float height = Mathf.Abs(bounds.size.y);
        float depth = Mathf.Abs(bounds.size.z);
        float smallestHorizontal = Mathf.Min(width, depth);
        float largestHorizontal = Mathf.Max(width, depth);

        if (slot == YQWorldAssetCatalog.SlotSettlementBuilding)
        {
            // note: Complete lots need a usable footprint and height; walls, façades, and giant scene prefabs fail this contract.
            if (smallestHorizontal < 2f || largestHorizontal < 5f || height < 2.5f ||
                largestHorizontal > 28f || height > 20f)
            {
                reason = "not a human-scale complete building";
                return true;
            }
        }
        else if (slot == YQWorldAssetCatalog.SlotVegetation)
        {
            // note: Settlement-edge dressing must not become a forest canopy or obscure playable buildings.
            if (largestHorizontal > 12f || height > 22f)
            {
                reason = "vegetation exceeds settlement-edge scale";
                return true;
            }
        }
        else if (slot == YQWorldAssetCatalog.SlotFloorDeco ||
                 slot == YQWorldAssetCatalog.SlotLighting)
        {
            // note: Loose dressing is intentionally small so a prop cannot masquerade as a landmark.
            if (largestHorizontal > 7f || height > 9f)
            {
                reason = "decoration exceeds prop scale";
                return true;
            }
        }
        else if (slot == YQWorldAssetCatalog.SlotExteriorDeco ||
                 slot == YQWorldAssetCatalog.SlotInteriorDeco)
        {
            // note: Generic discovered dressing remains eligible, but scene-scale roots cannot occupy a single procedural decoration anchor.
            if (largestHorizontal > 12f || height > 16f)
            {
                reason = "decoration exceeds a single settlement anchor";
                return true;
            }
        }

        return false;
    }

    private static void LogSkippedUnsuitableSettlementAsset(
        string assetPath,
        string slot,
        string reason)
    {
        if (_skippedUnsuitableSettlementAssetLogs >=
            MaxSkippedUnsuitableSettlementAssetLogs)
        {
            return;
        }

        _skippedUnsuitableSettlementAssetLogs++;

        Debug.LogWarning(
            "[YQGeneratedWorldRuntimeBuilder] Skipped " +
            (string.IsNullOrWhiteSpace(slot) ? "world" : slot) +
            " asset " +
            (string.IsNullOrWhiteSpace(reason) ? "outside placement contract" : reason) +
            ": " + assetPath);

        if (_skippedUnsuitableSettlementAssetLogs ==
            MaxSkippedUnsuitableSettlementAssetLogs)
        {
            // note: One misclassified imported pack must not flood the console during a full world rebuild.
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] Further unsuitable settlement asset warnings suppressed.");
        }
    }

    private static List<BoxCollider>
        DisableNegativeScaleSolidPrefabBoxColliders(
            GameObject prefab)
    {
        List<BoxCollider> disabled =
            new List<BoxCollider>();

        if (prefab == null)
            return disabled;

        BoxCollider[] boxes =
            prefab.GetComponentsInChildren<
                BoxCollider>(
                    true);

        for (int i = 0;
             i < boxes.Length;
             i++)
        {
            BoxCollider box =
                boxes[i];

            if (box == null ||
                !box.enabled ||
                box.isTrigger)
            {
                continue;
            }

            if (!HasNegativeScaleInPrefabHierarchy(
                    box.transform,
                    prefab.transform))
            {
                continue;
            }

            box.enabled =
                false;

            disabled.Add(
                box);
        }

        return disabled;
    }

    private static void
        RestoreTemporarilyDisabledPrefabBoxColliders(
            List<BoxCollider> boxes)
    {
        if (boxes == null)
            return;

        for (int i = 0;
             i < boxes.Count;
             i++)
        {
            BoxCollider box =
                boxes[i];

            if (box != null)
            {
                box.enabled =
                    true;
            }
        }
    }

    private static int DisableSolidPrimitiveCollidersInHierarchy(
        GameObject root)
    {
        if (root == null)
            return 0;

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(
                true);

        int disabled =
            0;

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider collider =
                colliders[i];

            if (collider == null ||
                collider.isTrigger)
            {
                continue;
            }

            if (collider is BoxCollider ||
                collider is SphereCollider ||
                collider is CapsuleCollider)
            {
                // note: Disable immediately; Destroy is delayed until Unity's safe destruction point.
                collider.enabled =
                    false;

                UnityEngine.Object.Destroy(
                    collider);

                disabled++;
            }
        }

        return disabled;
    }

    private static bool HasMissingMonoBehaviourInHierarchy(
        GameObject prefab)
    {
        if (prefab == null)
            return false;

        MonoBehaviour[] behaviours =
            prefab.GetComponentsInChildren<MonoBehaviour>(
                true);

        for (int i = 0;
             i < behaviours.Length;
             i++)
        {
            if (behaviours[i] == null)
            {
                return true;
            }
        }

        return false;
    }

    private static void LogSkippedMissingScriptPrefab(
        string assetPath)
    {
        if (_skippedMissingScriptPrefabLogs >=
            MaxSkippedMissingScriptPrefabLogs)
        {
            return;
        }

        _skippedMissingScriptPrefabLogs++;

        Debug.LogWarning(
            "[YQGeneratedWorldRuntimeBuilder] " +
            "Skipped decorative prefab with missing script reference: " +
            assetPath);

        if (_skippedMissingScriptPrefabLogs ==
            MaxSkippedMissingScriptPrefabLogs)
        {
            // note: Missing-script prefab warnings are capped because one bad decoration family can be selected many times.
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Further decorative missing-script prefab warnings suppressed.");
        }
    }

    private static bool HasNegativeScaleInPrefabHierarchy(
     Transform child,
     Transform prefabRoot)
    {
        if (child == null)
            return false;

        Transform current =
            child;

        while (current != null)
        {
            Vector3 localScale =
                current.localScale;

            // note: Primitive colliders cannot safely pass through any mirrored transform, including double-negative chains whose accumulated scale appears positive.
            if (localScale.x < 0f ||
                localScale.y < 0f ||
                localScale.z < 0f)
            {
                return true;
            }

            if (current ==
                prefabRoot)
            {
                break;
            }

            current =
                current.parent;
        }

        return false;
    }

    private static void PrepareEnvironmentInstance(
        GameObject root,
        bool keepColliders)
    {
        if (root == null)
            return;

        Rigidbody[] rigidbodies =
            root.GetComponentsInChildren<Rigidbody>(
                true);

        for (int i = 0;
             i < rigidbodies.Length;
             i++)
        {
            Rigidbody body =
                rigidbodies[i];

            if (body == null)
                continue;

            body.isKinematic =
                true;

            body.useGravity =
                false;
        }

        if (!keepColliders)
        {
            Collider[] colliders =
                root.GetComponentsInChildren<Collider>(
                    true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled =
                        false;
                }
            }
        }

        /*
         * Important:
         * Do NOT run YQVisualStabilityDirector over marketplace
         * generated-world prefabs. It can replace their actual
         * marketplace floor/ground materials.
         */
        YQRuntimeUrpMaterialRepair
            .RepairHierarchy(
                root);
    }

    // ------------------------------------------------------------
    // GROUNDING
    // ------------------------------------------------------------

    private IEnumerator PrepareDeterministicConstructionTerrainRoutine(
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        Action<bool> completed)
    {
        if (plan == null || terrain == null || terrain.terrainData == null)
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] CONSTRUCTION TERRAIN PREPASS REJECTED: " +
                "the persisted plan or generated terrain is missing.");
            completed?.Invoke(false);
            yield break;
        }

        bool prepared = true;
        List<ConstructionFootprintReservation> reservations =
            new List<ConstructionFootprintReservation>();
        Vector3 originAnchor = YQGeneratedWorldLayout.GetVeyOriginAnchor();

        // note: Messenger Mountain authored the Goddess 23.7 metres above its source datum; reproduce that relief instead of flattening the shrine and leaving its statue suspended in the air.
        prepared &= GradeOriginGoddessRelief(
            terrain,
            originAnchor);
        yield return null;
        // note: The furnished Witch House occupies a compact shelf cut into the approach face of the Goddess ridge instead of a detached oversized pad.
        prepared &= GradeTerrainPad(
            terrain,
            originAnchor + OriginWitchHouseOffset,
            15f,
            24f);
        yield return null;
        reservations.Add(new ConstructionFootprintReservation(
            "Goddess threshold",
            originAnchor,
            YQGeneratedWorldLayout.OriginReserveRadius));

        if (plan.settlements != null)
        {
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[index];

                if (settlement == null)
                {
                    prepared = false;
                    continue;
                }

                float footprintRadius;
                float flatRadius;
                float outerRadius;

                if (YQWorldGenerationArchitecture.UsesCompiledWorld)
                {
                    GeneratedRegionRecord region = FindRegion(
                        plan,
                        settlement.regionId);
                    GeneratedRegionAssetPaletteRecord palette = FindPalette(
                        plan,
                        region);

                    if (region == null || palette == null ||
                        !YQCompiledWorldSiteBindingService.TryResolveSettlementSite(
                            plan,
                            settlement,
                            region,
                            palette,
                            out YQRuntimeWorldSiteRecord site,
                            out bool bindingChanged) ||
                        !TryResolveConstructionRadii(
                            site,
                            out footprintRadius,
                            out flatRadius,
                            out outerRadius))
                    {
                        Debug.LogError(
                            "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED SETTLEMENT\n" +
                            "Settlement: " + settlement.displayName + "\n" +
                            "Reason: no spatially validated reviewed site is available.");
                        prepared = false;
                        continue;
                    }

                    _compiledBindingsChangedDuringBuild |= bindingChanged;
                }
                else
                {
                    YQGeneratedSettlementCellLayout.Template template =
                        YQGeneratedSettlementCellLayout.ResolveTemplate(
                            plan,
                            settlement);
                    outerRadius = ResolveLegacySettlementOuterRadius(template);
                    flatRadius = outerRadius * 0.72f;
                    footprintRadius = flatRadius;
                }

                Vector3 center = YQGeneratedWorldLayout.GetSettlementAnchor(
                    plan,
                    settlement,
                    terrain);
                string label = "settlement " + settlement.displayName;

                if (!TryReserveConstructionFootprint(
                        reservations,
                        label,
                        center,
                        footprintRadius,
                        out string conflict) ||
                    !GradeTerrainPad(
                        terrain,
                        center,
                        flatRadius,
                        outerRadius))
                {
                    Debug.LogError(
                        "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED SETTLEMENT\n" +
                        "Settlement: " + settlement.displayName + "\n" +
                        "Reason: " + conflict);
                    prepared = false;
                    continue;
                }

                reservations.Add(new ConstructionFootprintReservation(
                    label,
                    center,
                    footprintRadius));

                yield return null;
            }
        }

        if (YQWorldGenerationArchitecture.UsesCompiledWorld &&
            plan.encampments != null)
        {
            for (int index = 0; index < plan.encampments.Count; index++)
            {
                GeneratedEncampmentRecord encampment = plan.encampments[index];

                if (encampment == null)
                {
                    prepared = false;
                    continue;
                }

                GeneratedRegionRecord region = FindRegion(
                    plan,
                    encampment.regionId);
                GeneratedRegionAssetPaletteRecord palette = FindPalette(
                    plan,
                    region);

                if (region == null || palette == null ||
                    !YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(
                        plan,
                        encampment,
                        region,
                        palette,
                        out YQRuntimeWorldSiteRecord site,
                        out bool bindingChanged) ||
                    !TryResolveConstructionRadii(
                        site,
                        out float footprintRadius,
                        out float flatRadius,
                        out float outerRadius))
                {
                    Debug.LogError(
                        "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED HOSTILE SITE\n" +
                        "Site: " + encampment.displayName + "\n" +
                        "Reason: no compatible spatially validated exterior site is available.");
                    prepared = false;
                    continue;
                }

                _compiledBindingsChangedDuringBuild |= bindingChanged;
                Vector3 requestedCenter = YQGeneratedWorldLayout.GetEncampmentAnchor(
                    plan,
                    encampment,
                    terrain);
                string label = "hostile site " + encampment.displayName;

                if (!TryResolveHostileConstructionCenter(
                        reservations,
                        label,
                        requestedCenter,
                        footprintRadius,
                        outerRadius,
                        terrain,
                        plan.worldSeed + "|" + encampment.encampmentId,
                        out Vector3 center,
                        out string conflict) ||
                    !GradeTerrainPad(
                        terrain,
                        center,
                        flatRadius,
                        outerRadius))
                {
                    Debug.LogError(
                        "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED HOSTILE SITE\n" +
                        "Site: " + encampment.displayName + "\n" +
                        "Reason: " + conflict);
                    prepared = false;
                    continue;
                }

                YQGeneratedWorldLayout.SetRuntimeEncampmentAnchor(
                    encampment.encampmentId,
                    center);

                if ((center - requestedCenter).sqrMagnitude > 0.01f)
                {
                    Debug.LogWarning(
                        "[YQGeneratedWorldRuntimeBuilder] HOSTILE SITE RELOCATED\n" +
                        "Site: " + encampment.displayName + "\n" +
                        "Requested anchor: " + requestedCenter + "\n" +
                        "Reserved anchor: " + center + "\n" +
                        "Reason: " + conflict);
                }

                reservations.Add(new ConstructionFootprintReservation(
                    label,
                    center,
                    footprintRadius));

                yield return null;
            }
        }

        // note: Every pad uses delayed writes; publish height LOD and terrain rendering separately while deferring the global physics rebuild to final player handoff.
        yield return null;
        terrain.terrainData.SyncHeightmap();
        yield return null;
        terrain.Flush();
        completed?.Invoke(prepared);
    }

    private static bool TryResolveConstructionRadii(
        YQRuntimeWorldSiteRecord site,
        out float footprintRadius,
        out float flatRadius,
        out float outerRadius)
    {
        footprintRadius = 0f;
        flatRadius = 0f;
        outerRadius = 0f;

        if (site == null || !site.spatiallyValidated ||
            !site.seamlessPlacementEligible ||
            float.IsNaN(site.authoredFootprintRadius) ||
            float.IsInfinity(site.authoredFootprintRadius) ||
            site.authoredFootprintRadius <= 0f)
        {
            return false;
        }

        footprintRadius = site.authoredFootprintRadius;
        flatRadius = footprintRadius + 3f;
        outerRadius = footprintRadius + 18f;
        return outerRadius < YQGeneratedWorldTerrain.WorldSize * 0.5f;
    }

    private static float ResolveLegacySettlementOuterRadius(
        YQGeneratedSettlementCellLayout.Template template)
    {
        return template == YQGeneratedSettlementCellLayout.Template.DenseCity
            ? 82f
            : template == YQGeneratedSettlementCellLayout.Template.MarketVillage
                ? 68f
                : template == YQGeneratedSettlementCellLayout.Template.FortifiedOutpost
                    ? 62f
                    : 48f;
    }

    private static bool TryReserveConstructionFootprint(
        List<ConstructionFootprintReservation> reservations,
        string label,
        Vector3 center,
        float radius,
        out string failure)
    {
        failure = string.Empty;

        if (float.IsNaN(center.x) || float.IsInfinity(center.x) ||
            float.IsNaN(center.z) || float.IsInfinity(center.z) ||
            float.IsNaN(radius) || float.IsInfinity(radius) || radius <= 0f)
        {
            failure = "its deterministic footprint is invalid.";
            return false;
        }

        for (int index = 0; index < reservations.Count; index++)
        {
            ConstructionFootprintReservation existing = reservations[index];
            float requiredDistance = existing.radius + radius + 12f;
            float actualDistance = Vector2.Distance(
                new Vector2(center.x, center.z),
                new Vector2(existing.center.x, existing.center.z));

            if (actualDistance < requiredDistance)
            {
                failure = label + " overlaps " + existing.label +
                    " (" + actualDistance.ToString("F1") + "m available, " +
                    requiredDistance.ToString("F1") + "m required).";
                return false;
            }
        }

        return true;
    }

    private static bool TryResolveHostileConstructionCenter(
        List<ConstructionFootprintReservation> reservations,
        string label,
        Vector3 requestedCenter,
        float footprintRadius,
        float outerRadius,
        Terrain terrain,
        string deterministicSeed,
        out Vector3 resolvedCenter,
        out string initialConflict)
    {
        resolvedCenter = requestedCenter;
        if (TryReserveConstructionFootprint(
                reservations,
                label,
                requestedCenter,
                footprintRadius,
                out initialConflict))
        {
            return true;
        }

        if (terrain == null || terrain.terrainData == null)
            return false;

        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        float boundaryMargin = Mathf.Max(footprintRadius, outerRadius) + 8f;
        float minimumX = terrainOrigin.x + boundaryMargin;
        float maximumX = terrainOrigin.x + terrainSize.x - boundaryMargin;
        float minimumZ = terrainOrigin.z + boundaryMargin;
        float maximumZ = terrainOrigin.z + terrainSize.z - boundaryMargin;
        if (minimumX > maximumX || minimumZ > maximumZ)
            return false;

        bool found = false;
        float bestScore = float.PositiveInfinity;
        int tieBreaker = PositiveHash(deterministicSeed);
        for (int gridZ = 0; gridZ < 5; gridZ++)
        {
            for (int gridX = 0; gridX < 5; gridX++)
            {
                Vector3 candidate = new Vector3(
                    Mathf.Lerp(minimumX, maximumX, gridX / 4f),
                    requestedCenter.y,
                    Mathf.Lerp(minimumZ, maximumZ, gridZ / 4f));
                if (!TryReserveConstructionFootprint(
                        reservations,
                        label,
                        candidate,
                        footprintRadius,
                        out _))
                {
                    continue;
                }

                float score = (candidate - requestedCenter).sqrMagnitude +
                    ((gridX + gridZ * 5 + tieBreaker) % 25) * 0.001f;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                resolvedCenter = candidate;
                found = true;
            }
        }

        if (!found)
            return false;

        // note: The deterministic grid search changes only X/Z; the canonical terrain remains the authority for the relocated site's support height.
        resolvedCenter.y = YQGeneratedWorldTerrain.SampleWorldHeight(
            terrain,
            resolvedCenter);
        return true;
    }

    internal static bool GradeTerrainPad(
        Terrain terrain,
        Vector3 center,
        float flatRadius,
        float outerRadius)
    {
        if (terrain == null || terrain.terrainData == null ||
            !terrain.gameObject.activeInHierarchy)
        {
            return false;
        }

        flatRadius = Mathf.Max(2f, flatRadius);
        outerRadius = Mathf.Max(flatRadius + 2f, outerRadius);
        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float minimumX = origin.x + outerRadius;
        float maximumX = origin.x + size.x - outerRadius;
        float minimumZ = origin.z + outerRadius;
        float maximumZ = origin.z + size.z - outerRadius;

        if (center.x < minimumX || center.x > maximumX ||
            center.z < minimumZ || center.z > maximumZ)
        {
            return false;
        }

        int resolution = data.heightmapResolution;
        float normalizedX = Mathf.InverseLerp(
            origin.x,
            origin.x + size.x,
            center.x);
        float normalizedZ = Mathf.InverseLerp(
            origin.z,
            origin.z + size.z,
            center.z);
        int centerX = Mathf.RoundToInt(normalizedX * (resolution - 1));
        int centerZ = Mathf.RoundToInt(normalizedZ * (resolution - 1));
        int radiusX = Mathf.CeilToInt(
            outerRadius / size.x * (resolution - 1));
        int radiusZ = Mathf.CeilToInt(
            outerRadius / size.z * (resolution - 1));
        int startX = Mathf.Clamp(centerX - radiusX, 0, resolution - 1);
        int startZ = Mathf.Clamp(centerZ - radiusZ, 0, resolution - 1);
        int endX = Mathf.Clamp(centerX + radiusX, 0, resolution - 1);
        int endZ = Mathf.Clamp(centerZ + radiusZ, 0, resolution - 1);
        int width = endX - startX + 1;
        int height = endZ - startZ + 1;

        if (width <= 1 || height <= 1)
            return false;

        float[,] heights = data.GetHeights(startX, startZ, width, height);
        float sampledWorldHeight = terrain.SampleHeight(center) + origin.y;
        float targetHeight = Mathf.Clamp01(
            (sampledWorldHeight - origin.y) / Mathf.Max(0.001f, size.y));
        Vector2 horizontalCenter = new Vector2(center.x, center.z);

        for (int z = 0; z < height; z++)
        {
            float worldZ = origin.z +
                (startZ + z) / (float)(resolution - 1) * size.z;

            for (int x = 0; x < width; x++)
            {
                float worldX = origin.x +
                    (startX + x) / (float)(resolution - 1) * size.x;
                float distance = Vector2.Distance(
                    new Vector2(worldX, worldZ),
                    horizontalCenter);

                if (distance >= outerRadius)
                    continue;

                float blend = distance <= flatRadius
                    ? 1f
                    : 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(
                            flatRadius,
                            outerRadius,
                            distance));
                heights[z, x] = Mathf.Lerp(
                    heights[z, x],
                    targetHeight,
                    blend);
            }
        }

        // note: The caller batches all delayed pad writes and publishes one final heightmap, avoiding settlement-by-settlement terrain rebuild stalls.
        data.SetHeightsDelayLOD(startX, startZ, heights);
        return true;
    }

    private static bool GradeOriginGoddessRelief(
        Terrain terrain,
        Vector3 originAnchor)
    {
        if (terrain == null || terrain.terrainData == null ||
            !terrain.gameObject.activeInHierarchy)
        {
            return false;
        }

        const float summitRadius = 7f;
        const float sideRadius = 42f;
        const float approachRadius = 64f;
        const float rearRadius = 46f;
        const float authoredSummitRise = 17.5f;
        TerrainData data = terrain.terrainData;
        Vector3 terrainOrigin = terrain.transform.position;
        Vector3 terrainSize = data.size;
        Vector3 summit = originAnchor + OriginGoddessSummitOffset;
        int resolution = data.heightmapResolution;
        int centerX = Mathf.RoundToInt(
            Mathf.InverseLerp(
                terrainOrigin.x,
                terrainOrigin.x + terrainSize.x,
                summit.x) * (resolution - 1));
        int centerZ = Mathf.RoundToInt(
            Mathf.InverseLerp(
                terrainOrigin.z,
                terrainOrigin.z + terrainSize.z,
                summit.z) * (resolution - 1));
        float outerRadius = Mathf.Max(sideRadius, approachRadius);
        int radiusX = Mathf.CeilToInt(
            outerRadius / terrainSize.x * (resolution - 1));
        int radiusZ = Mathf.CeilToInt(
            outerRadius / terrainSize.z * (resolution - 1));
        int startX = Mathf.Clamp(centerX - radiusX, 0, resolution - 1);
        int startZ = Mathf.Clamp(centerZ - radiusZ, 0, resolution - 1);
        int endX = Mathf.Clamp(centerX + radiusX, 0, resolution - 1);
        int endZ = Mathf.Clamp(centerZ + radiusZ, 0, resolution - 1);
        int width = endX - startX + 1;
        int height = endZ - startZ + 1;

        if (width <= 1 || height <= 1)
            return false;

        float[,] heights = data.GetHeights(startX, startZ, width, height);
        Vector2 horizontalSummit = new Vector2(summit.x, summit.z);
        Vector2 approachPoint = new Vector2(
            originAnchor.x,
            originAnchor.z - 76f);
        Vector2 approachDirection = (approachPoint - horizontalSummit).normalized;
        Vector2 sideDirection = new Vector2(
            -approachDirection.y,
            approachDirection.x);
        float baseWorldHeight = terrain.SampleHeight(
            new Vector3(
                horizontalSummit.x + approachDirection.x * approachRadius,
                summit.y,
                horizontalSummit.y + approachDirection.y * approachRadius)) +
            terrainOrigin.y;

        for (int z = 0; z < height; z++)
        {
            float worldZ = terrainOrigin.z +
                (startZ + z) / (float)(resolution - 1) * terrainSize.z;

            for (int x = 0; x < width; x++)
            {
                float worldX = terrainOrigin.x +
                    (startX + x) / (float)(resolution - 1) * terrainSize.x;
                Vector2 offset = new Vector2(worldX, worldZ) - horizontalSummit;
                float forward = Vector2.Dot(offset, approachDirection);
                float side = Vector2.Dot(offset, sideDirection);
                float directionalRadius = forward >= 0f
                    ? approachRadius
                    : rearRadius;
                float ellipticalDistance = Mathf.Sqrt(
                    side * side / (sideRadius * sideRadius) +
                    forward * forward / (directionalRadius * directionalRadius));

                if (ellipticalDistance >= 1f)
                    continue;

                float distance = offset.magnitude;
                float profile = distance <= summitRadius
                    ? 1f
                    : 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.12f, 1f, ellipticalDistance));
                float naturalVariation = 1f +
                    Mathf.Sin(worldX * 0.085f + worldZ * 0.041f) *
                    Mathf.Sin(worldZ * 0.067f - worldX * 0.029f) *
                    0.055f * (1f - profile);
                float targetWorldHeight =
                    baseWorldHeight + authoredSummitRise * profile * naturalVariation;

                float pathWeight = 1f - Mathf.SmoothStep(
                    3.5f,
                    8.5f,
                    Mathf.Abs(side));
                if (forward >= 0f && forward <= approachRadius && pathWeight > 0f)
                {
                    float climb = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        forward / approachRadius);
                    float pathWorldHeight = baseWorldHeight +
                        authoredSummitRise * climb;
                    // note: The approach corridor follows a gradual terrain ramp, replacing the circular mound silhouette while preserving a walkable line to the summit.
                    targetWorldHeight = Mathf.Lerp(
                        targetWorldHeight,
                        pathWorldHeight,
                        pathWeight * 0.82f);
                }
                float normalizedTarget = Mathf.Clamp01(
                    (targetWorldHeight - terrainOrigin.y) /
                    Mathf.Max(0.001f, terrainSize.y));
                heights[z, x] = Mathf.Max(
                    heights[z, x],
                    Mathf.Lerp(
                        heights[z, x],
                        normalizedTarget,
                        Mathf.Clamp01(profile + pathWeight * 0.45f)));
            }
        }

        // note: The heightmap remains part of the single construction prepass, so the shrine, wilderness, colliders, and player all consume the same final terrain authority.
        data.SetHeightsDelayLOD(startX, startZ, heights);
        return true;
    }

    private sealed class ConstructionFootprintReservation
    {
        public readonly string label;
        public readonly Vector3 center;
        public readonly float radius;

        public ConstructionFootprintReservation(
            string label,
            Vector3 center,
            float radius)
        {
            // note: The reservation is temporary build-transaction state; persisted world coordinates remain the sole deterministic authority.
            this.label = label ?? string.Empty;
            this.center = center;
            this.radius = radius;
        }
    }

    // ------------------------------------------------------------
    // GROUNDING
    // ------------------------------------------------------------

    private static void GroundInstance(
        GameObject instance)
    {
        if (instance == null ||
            YQTerrainSupportComposer.IsExplicitlySuspended(
                instance))
            return;

        if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                instance,
                out Bounds bounds,
                out float structuralBottom))
        {
            return;
        }

        if (!TryFindGroundHeight(
                instance,
                bounds,
                out float targetGroundHeight))
        {
            return;
        }

        // note: Fallback/modular assemblies share the same filtered structural bottom as the compiled-world path; decorative renderers and imported pivots cannot lift the result.
        float embedDepth =
            Mathf.Clamp(
                bounds.size.y * 0.01f,
                0.015f,
                0.15f);
        float verticalOffset =
            targetGroundHeight -
            structuralBottom -
            embedDepth;

        if (Mathf.Abs(
                verticalOffset) <
            0.001f)
        {
            return;
        }

        Vector3 position =
            instance.transform.position;

        position.y +=
            verticalOffset;

        instance.transform.position =
            position;

        // note: Grounding consumes renderer bounds; the final player handoff owns physics publication instead of forcing it for each moved authored instance.
    }

    private static bool TryGetRenderableBounds(
        GameObject root,
        out Bounds bounds)
    {
        bounds =
            new Bounds();

        if (root == null)
            return false;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        bool initialized =
            false;

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer renderer =
                renderers[i];

            if (renderer == null ||
                renderer is ParticleSystemRenderer)
            {
                continue;
            }

            if (!initialized)
            {
                bounds =
                    renderer.bounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        if (initialized)
            return true;

        Collider[] colliders =
            root.GetComponentsInChildren<Collider>(
                true);

        for (int i = 0;
             i < colliders.Length;
             i++)
        {
            Collider collider =
                colliders[i];

            if (collider == null ||
                collider.isTrigger)
            {
                continue;
            }

            if (!initialized)
            {
                bounds =
                    collider.bounds;

                initialized =
                    true;
            }
            else
            {
                bounds.Encapsulate(
                    collider.bounds);
            }
        }

        return initialized;
    }

    private static bool TryFindGroundHeight(
        GameObject instance,
        Bounds bounds,
        out float groundHeight)
    {
        groundHeight =
            0f;

        Vector3 center =
            bounds.center;

        Terrain[] terrains =
            Terrain.activeTerrains;

        bool foundTerrain =
            false;

        float terrainContact =
            float.MinValue;

        for (int terrainIndex = 0;
             terrainIndex < terrains.Length;
             terrainIndex++)
        {
            Terrain terrain =
                terrains[terrainIndex];

            if (terrain == null ||
                terrain.terrainData == null ||
                !terrain.gameObject.activeInHierarchy ||
                !YQGeneratedWorldTerrain.TrySampleFootprintHeight(
                    terrain,
                    bounds,
                    out float candidateContact,
                    out _,
                    out _))
            {
                continue;
            }

            // note: Overlapping active terrains are unusual, but the upper valid surface remains the safe authority while each surface uses the shared footprint percentile instead of its highest corner.
            terrainContact =
                !foundTerrain
                    ? candidateContact
                    : Mathf.Max(
                        terrainContact,
                        candidateContact);
            foundTerrain =
                true;
        }

        if (foundTerrain)
        {
            groundHeight =
                terrainContact;

            return true;
        }

        Vector3 rayOrigin =
            new Vector3(
                center.x,
                Mathf.Max(
                    bounds.max.y +
                        100f,
                    center.y +
                        100f),
                center.z);

        RaycastHit[] hits =
            Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                1000f,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore);

        if (hits == null ||
            hits.Length == 0)
        {
            return false;
        }

        Array.Sort(
            hits,
            (a, b) =>
                a.distance.CompareTo(
                    b.distance));

        for (int i = 0;
             i < hits.Length;
             i++)
        {
            Collider collider =
                hits[i].collider;

            if (collider == null)
                continue;

            Transform hitTransform =
                collider.transform;

            if (hitTransform != null &&
                (hitTransform ==
                    instance.transform ||
                 hitTransform.IsChildOf(
                     instance.transform)))
            {
                continue;
            }

            groundHeight =
                hits[i].point.y;

            return true;
        }

        return false;
    }

    private static Vector3 GroundPosition(
        Vector3 position)
    {
        position.y =
            SampleGroundHeight(
                position);

        return position;
    }

    private static float SampleGroundHeight(
        Vector3 position)
    {
        Terrain[] terrains =
            Terrain.activeTerrains;

        for (int i = 0;
             i < terrains.Length;
             i++)
        {
            Terrain terrain =
                terrains[i];

            if (terrain == null ||
                terrain.terrainData == null ||
                !terrain.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 terrainPosition =
                terrain.transform.position;

            Vector3 size =
                terrain.terrainData.size;

            if (position.x <
                    terrainPosition.x ||
                position.x >
                    terrainPosition.x +
                    size.x ||
                position.z <
                    terrainPosition.z ||
                position.z >
                    terrainPosition.z +
                    size.z)
            {
                continue;
            }

            return
                terrain.SampleHeight(
                    position) +
                terrainPosition.y;
        }

        if (Physics.Raycast(
                new Vector3(
                    position.x,
                    1000f,
                    position.z),
                Vector3.down,
                out RaycastHit hit,
                2000f,
                ~0,
                QueryTriggerInteraction.Ignore))
        {
            return
                hit.point.y;
        }

        return
            position.y;
    }

    // ------------------------------------------------------------
    // REGION VOLUME / METADATA
    // ------------------------------------------------------------

    private static void BuildRegionVolume(
        Transform parent,
        GeneratedRegionRecord region,
        GeneratedSettlementRecord settlement)
    {
        GameObject volume =
            new GameObject(
                "GeneratedSettlementRegionVolume");

        volume.transform.SetParent(
            parent,
            false);

        volume.transform.localPosition =
            new Vector3(
                0f,
                3f,
                0f);

        BoxCollider collider =
            volume.AddComponent<
                BoxCollider>();

        collider.isTrigger =
            true;

        collider.size =
            new Vector3(
                52f,
                8f,
                52f);

        RegionVolume regionVolume =
            volume.AddComponent<
                RegionVolume>();

        regionVolume.regionId =
            region.regionId;

        regionVolume.regionName =
            settlement.displayName;

        regionVolume.tags =
            new List<string>
            {
                "generated",
                "settlement",
                settlement.kind,
                region.assetStyleKey
            };
    }

    private static void BuildSettlementLabel(
        Transform parent,
        GeneratedSettlementRecord settlement,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette)
    {
        GameObject metadata =
            new GameObject(
                "META__" +
                SafeName(
                    settlement.displayName));

        metadata.transform.SetParent(
            parent,
            false);

        metadata.transform.localPosition =
            Vector3.zero;

        metadata.name =
            "META__" +
            SafeName(
                settlement.displayName) +
            "__" +
            settlement.kind +
            "__" +
            palette.styleKey;
    }

    // ------------------------------------------------------------
    // ORIGIN TRANSITION
    // ------------------------------------------------------------

    private static void DetachOriginObjectsForRebuild(
        out GameObject hut,
        out GameObject vey)
    {
        hut =
            null;

        vey =
            null;

        Transform originStagingParent =
            null;

        GameObject generatedRoot =
            GameObject.Find(
                RuntimeRootName);

        if (generatedRoot != null)
        {
            Transform generatedHut =
                FindDescendantByName(
                    generatedRoot.transform,
                    "Origin_Hut");

            if (generatedHut != null)
            {
                hut =
                    generatedHut.gameObject;
            }

            Transform generatedVey =
                FindDescendantByName(
                    generatedRoot.transform,
                    "Archivist Vey");

            if (generatedVey != null)
            {
                vey =
                    generatedVey.gameObject;
            }
        }

        GameObject legacyWorld =
            GameObject.Find(
                "YQ_InvestorWorldRoot");

        if (legacyWorld != null)
        {
            if (hut == null)
            {
                Transform legacyHut =
                    FindDescendantByName(
                        legacyWorld.transform,
                        "Origin_Hut");

                if (legacyHut != null)
                {
                    hut =
                        legacyHut.gameObject;
                }
            }

            if (vey == null)
            {
                Transform legacyVey =
                    FindDescendantByName(
                        legacyWorld.transform,
                        "Archivist Vey");

                if (legacyVey != null)
                {
                    vey =
                        legacyVey.gameObject;
                }
            }
        }

        if (vey == null)
        {
            NpcDialogueAgent[] agents =
                UnityEngine.Object
                    .FindObjectsByType<
                        NpcDialogueAgent>(
                            FindObjectsInactive.Include,
                            FindObjectsSortMode.None);

            for (int i = 0;
                 i < agents.Length;
                 i++)
            {
                NpcDialogueAgent agent =
                    agents[i];

                if (agent == null)
                    continue;

                if (string.Equals(
                        agent.npcId,
                        "npc_archivist_01",
                        StringComparison.OrdinalIgnoreCase))
                {
                    vey =
                        agent.gameObject;

                    originStagingParent =
                        vey.transform.parent;

                    break;
                }
            }
        }

        if (hut != null)
        {
            hut.transform.SetParent(
                null,
                true);
        }

        if (vey != null)
        {
            vey.transform.SetParent(
                null,
                true);
        }

        // note: The inactive staging shell exists only to keep Vey structured during startup; discard it once the generated world takes ownership.
        if (originStagingParent != null &&
            originStagingParent.childCount == 0 &&
            string.Equals(
                originStagingParent.name,
                "__YQ_OriginActorStaging",
                StringComparison.Ordinal))
        {
            UnityEngine.Object.Destroy(
                originStagingParent.gameObject);
        }
    }

    private IEnumerator AdoptVeyOriginIntoGeneratedWorldRoutine(
        Transform generatedRoot,
        Terrain generatedTerrain,
        YQRuntimeWorldAssetRegistry registry,
        GameObject hut,
        GameObject vey,
        Action<bool> completed)
    {
        if (generatedRoot == null)
        {
            completed?.Invoke(false);
            yield break;
        }

        GameObject legacyWorld =
            GameObject.Find(
                "YQ_InvestorWorldRoot");

        if (legacyWorld != null)
        {
            legacyWorld.name =
                "YQ_InvestorWorldRoot_Deprecated";

            legacyWorld.SetActive(
                false);
        }

        if (hut != null)
        {
            // note: The PlaySafe bootstrap hut is compatibility scaffolding; the reviewed Messenger Mountain and WitchHouse sites own generated presentation.
            hut.SetActive(false);
            UnityEngine.Object.Destroy(hut);
        }

        YQRuntimeWorldSiteCatalog siteCatalog =
            Resources.Load<YQRuntimeWorldSiteCatalog>(
                "YQRuntimeWorldSiteCatalog");
        YQRuntimeWorldSiteRecord witchHouseRecord =
            siteCatalog != null
                ? siteCatalog.FindByKitId("witch_house")
                : null;
        GameObject goddessStatuePrefab = registry != null
            ? registry.ResolvePrefab(OriginGoddessStatueAssetPath)
            : null;

        if (goddessStatuePrefab == null || witchHouseRecord == null)
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] ORIGIN AUTHORED SITES MISSING. " +
                "The curated Messenger statue and WitchHouse must both be available at runtime.");
            completed?.Invoke(false);
            yield break;
        }

        Vector3 originAnchor = YQGeneratedWorldLayout.GetVeyOriginAnchor();

        if (generatedTerrain != null)
        {
            // note: The canonical terrain prepass already prepared the landmark pad before wilderness placement; origin adoption only samples the finalized elevation.
            originAnchor.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                generatedTerrain,
                originAnchor) + 0.15f;
        }
        GameObject mountainRoot = new GameObject(
            "Origin_Goddess_MessengerMountain");
        mountainRoot.transform.SetParent(generatedRoot, false);
        mountainRoot.transform.position = originAnchor;
        GameObject statue = null;
        AsyncInstantiateOperation<GameObject> statueOperation =
            UnityEngine.Object.InstantiateAsync(
                goddessStatuePrefab,
                mountainRoot.transform);
        // note: The origin consumes the reviewed Angel statue directly; cloning 6,072 source-map objects and deleting 6,009 of them exhausted tens of gigabytes during Goddess loading.
        statueOperation.priority = -1;
        yield return statueOperation;
        if (statueOperation.Result != null && statueOperation.Result.Length > 0)
            statue = statueOperation.Result[0];

        bool mountainPrepared = statue != null;
        if (mountainPrepared)
        {
            statue.name = "SM_AngelStatue_Origin";
            statue.transform.localPosition = OriginGoddessSummitOffset;
            statue.transform.localRotation = Quaternion.identity;
            registry.ApplyMaterialOverrides(
                OriginGoddessStatueAssetPath,
                statue);
            YQRuntimeUrpMaterialRepair.RepairMaterialHierarchy(statue);
        }

        GameObject witchHouseRoot = new GameObject(
            "Origin_Vey_WitchHouse");
        witchHouseRoot.transform.SetParent(generatedRoot, false);
        // note: Surface the single reviewed furnished Witch House cell beside the origin path; Vey's home must be a visible physical destination, not an invisible portal to geometry hidden below the terrain.
        witchHouseRoot.transform.position = GroundOriginApproachPoint(
            generatedTerrain,
            originAnchor + OriginWitchHouseOffset,
            0.10f);
        bool witchHousePrepared = false;
        yield return
            YQCompiledWorldSiteInstance.MaterializeSemanticSliceRoutine(
                witchHouseRoot.transform,
                "origin_vey_witch_house",
                witchHouseRecord,
                new[] { "poi" },
                success => witchHousePrepared = success);

        float loadDeadline = Time.unscaledTime + 45f;

        while (Time.unscaledTime < loadDeadline &&
               !YQCompiledWorldSiteInstance.IsSiteLoaded(
                    "origin_vey_witch_house"))
        {
            yield return null;
        }

        if (!mountainPrepared || !witchHousePrepared ||
            !YQCompiledWorldSiteInstance.IsSiteLoaded(
                "origin_vey_witch_house"))
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] ORIGIN AUTHORED SITE LOAD FAILED. " +
                "The generated world will not substitute scattered props for the reviewed origin composition.");
            completed?.Invoke(false);
            yield break;
        }

        if (!CurateAndGroundGoddessLandmark(
                mountainRoot,
                generatedTerrain,
                out Bounds statueBounds,
                out List<YQTerrainSupportStamp> originSupportStamps))
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] ORIGIN SPATIAL CURATION FAILED. " +
                "The authored statue cluster or furnished Witch House could not be grounded safely.");
            completed?.Invoke(false);
            yield break;
        }

        int raisedOriginSupports = 0;
        // note: The canonical terrain prepass remains immutable after wilderness generation; the Witch House streaming pass lowers its reviewed cell onto that surface instead of reshaping terrain late.

        // note: Let Unity release rejected source-map objects before the forced origin material pass so thousands of discarded route/wilderness renderers are never converted unnecessarily.
        yield return null;
        // note: Only retained origin geometry receives the stronger compatibility pass; process it cooperatively so the Goddess presentation never loses animation/typewriter frames.
        yield return YQRuntimeUrpMaterialRepair.ForceRepairHierarchyRoutine(
            mountainRoot,
            null);
        yield return YQRuntimeUrpMaterialRepair.ForceRepairHierarchyRoutine(
            witchHouseRoot,
            null);
        yield return ConfigureOriginParticlePresentationRoutine(mountainRoot);
        yield return DisableUnsupportedOriginCollidersRoutine(mountainRoot, 48f);
        yield return DisableUnsupportedOriginCollidersRoutine(witchHouseRoot, 60f);
        // note: Renderer-bound grounding is complete immediately; collider publication is deferred to the final world handoff to avoid per-instance global synchronization.

        bool hasWitchHouseBounds = false;
        Bounds witchHouseBounds = new Bounds();
        yield return TryGetRenderableBoundsRoutine(
            witchHouseRoot,
            (success, bounds) =>
            {
                hasWitchHouseBounds = success;
                witchHouseBounds = bounds;
            });
        if (!hasWitchHouseBounds)
        {
            witchHouseBounds = new Bounds(
                witchHouseRoot.transform.position,
                Vector3.one);
        }

        Vector3 originFocus = Vector3.Lerp(
            statueBounds.center,
            witchHouseBounds.center,
            0.24f);
        Vector3 authoredApproach =
            originAnchor + new Vector3(0f, 0f, -76f);
        Vector3 approachDirection = authoredApproach - originFocus;
        approachDirection.y = 0f;
        if (approachDirection.sqrMagnitude < 0.01f)
            approachDirection = Vector3.back;
        approachDirection.Normalize();
        // note: The opening composition frames both the Goddess and Vey's visible hut; the player begins on the authored approach looking at their shared focal area.
        Vector3 exteriorLanding = GroundOriginApproachPoint(
            generatedTerrain,
            originFocus + approachDirection * 42f,
            0.25f);
        _generatedOriginSpawnOverride =
            exteriorLanding + Vector3.up * 0.20f;
        _hasGeneratedOriginSpawnOverride = true;
        _generatedOriginFacingOverride = originFocus - exteriorLanding;
        _generatedOriginFacingOverride.y = 0f;
        _hasGeneratedOriginFacingOverride =
            _generatedOriginFacingOverride.sqrMagnitude > 0.01f;

        if (vey != null)
        {
            vey.transform.SetParent(
                witchHouseRoot.transform,
                true);

            Vector3 veyPosition = witchHouseRoot.transform.position +
                new Vector3(0f, 0.10f, 0f);
            if (YQCompiledWorldSiteInstance.TryResolveWorldActorPosition(
                    "origin_vey_witch_house",
                    "alchemy service room circulation poi",
                    "origin|archivist_vey",
                    2,
                    out Vector3 resolvedVeyPosition))
            {
                veyPosition = resolvedVeyPosition;
            }

            vey.transform.position =
                veyPosition;
        }
        else
        {
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Archivist Vey could not be preserved from the " +
                "startup world. The generated world was built, " +
                "but the required origin NPC is missing.");
            completed?.Invoke(false);
            yield break;
        }

        if (legacyWorld != null)
        {
            UnityEngine.Object.Destroy(
                legacyWorld);
        }

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] AUTHORED GODDESS THRESHOLD READY\n" +
            "Shrine: curated Messenger Angel statue (bounded origin asset)\n" +
            "Vey: visible furnished witch_house at the surface origin\n" +
            "Terrain-supported authored assemblies: " +
            raisedOriginSupports);
        completed?.Invoke(true);
    }

    private static bool CurateAndGroundGoddessLandmark(
        GameObject mountainRoot,
        Terrain terrain,
        out Bounds groundedStatueBounds,
        out List<YQTerrainSupportStamp> supportStamps)
    {
        groundedStatueBounds = default;
        supportStamps = new List<YQTerrainSupportStamp>();

        if (mountainRoot == null || terrain == null)
            return false;

        Transform[] descendants = mountainRoot.GetComponentsInChildren<Transform>(
            true);
        Transform statue = null;

        for (int index = 0; index < descendants.Length; index++)
        {
            Transform candidate = descendants[index];

            if (candidate != null &&
                candidate.name.IndexOf(
                    "AngelStatue",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                statue = candidate;
                break;
            }
        }

        if (statue == null ||
            !TryGetRenderableBounds(statue.gameObject, out Bounds statueBounds))
        {
            return false;
        }

        Vector3 desiredSummit =
            YQGeneratedWorldLayout.GetVeyOriginAnchor() +
            OriginGoddessSummitOffset;
        Vector3 horizontalCorrection = new Vector3(
            desiredSummit.x - statueBounds.center.x,
            0f,
            desiredSummit.z - statueBounds.center.z);
        // note: Adding the reviewed route changes the selected aggregate origin; lock the actual statue back onto the deterministic summit before judging route proximity or elevation.
        mountainRoot.transform.position += horizontalCorrection;
        if (!TryGetRenderableBounds(statue.gameObject, out statueBounds))
            return false;

        List<Transform> cellRoots = new List<Transform>();

        for (int index = 0; index < descendants.Length; index++)
        {
            Transform candidate = descendants[index];

            if (candidate != null && candidate.name.StartsWith(
                    "CompiledCell__",
                    StringComparison.Ordinal))
            {
                cellRoots.Add(candidate);
            }
        }

        // note: The bounded origin path parents the curated statue directly instead of wrapping it in legacy CompiledCell roots, so the statue itself is the one retained authored assembly.
        int retained = cellRoots.Count == 0 ? 1 : 0;
        int excluded = 0;
        Vector2 statueHorizontal = new Vector2(
            statueBounds.center.x,
            statueBounds.center.z);
        Vector2 approachHorizontal = new Vector2(
            YQGeneratedWorldLayout.GetVeyOriginAnchor().x,
            YQGeneratedWorldLayout.GetVeyOriginAnchor().z - 76f);

        for (int cellIndex = 0; cellIndex < cellRoots.Count; cellIndex++)
        {
            Transform cell = cellRoots[cellIndex];

            for (int childIndex = 0; childIndex < cell.childCount; childIndex++)
            {
                Transform authoredObject = cell.GetChild(childIndex);
                bool containsStatue = statue == authoredObject ||
                    statue.IsChildOf(authoredObject);
                bool keep = containsStatue;
                string objectName = authoredObject.name.ToLowerInvariant();
                bool routeCell = cell.name.EndsWith(
                    "p0_p1_p01",
                    StringComparison.OrdinalIgnoreCase);

                if (!keep &&
                    TryGetRenderableBounds(
                        authoredObject.gameObject,
                        out Bounds bounds))
                {
                    float horizontalDistance = Vector2.Distance(
                        new Vector2(bounds.center.x, bounds.center.z),
                        statueHorizontal);
                    float largestDimension = Mathf.Max(
                        bounds.size.x,
                        Mathf.Max(bounds.size.y, bounds.size.z));
                    bool vegetation = ContainsOriginCurationToken(
                        objectName,
                        "grass", "flower", "tree", "bush", "leaf",
                        "branch", "fern", "weed");
                    bool shrineFeature = ContainsOriginCurationToken(
                        objectName,
                        "statue", "angel", "pedestal", "altar", "column",
                        "pillar", "stair", "step", "brazier", "torch",
                        "light", "particle", "vfx");
                    bool routeFeature = ContainsOriginCurationToken(
                        objectName,
                        "path", "road", "trail", "bridge", "plank",
                        "stair", "step", "torch");
                    bool unsupportedHydrology = ContainsOriginCurationToken(
                        objectName,
                        "waterfall", "water_fall", "waterspout", "water",
                        "mist", "spray", "foam", "splash");
                    bool oversizedLooseRock = ContainsOriginCurationToken(
                        objectName,
                        "rock", "boulder", "cliff") &&
                        largestDimension > 10f;
                    float routeDistance = DistanceToHorizontalSegment(
                        new Vector2(bounds.center.x, bounds.center.z),
                        approachHorizontal,
                        statueHorizontal);
                    float localTerrainHeight =
                        YQGeneratedWorldTerrain.SampleWorldHeight(
                            terrain,
                            bounds.center);
                    bool coherentRouteObject = routeCell &&
                        routeDistance <= 16f &&
                        Mathf.Abs(bounds.min.y - localTerrainHeight) <= 12f &&
                        largestDimension <= (routeFeature ? 60f : 24f) &&
                        (!vegetation || largestDimension <= 12f);

                    // note: The source landmark zone contains thousands of terrain-dependent wilderness placements; retain only the compact authored shrine cluster and reject oversized missing-terrain fragments.
                    keep = !unsupportedHydrology && !oversizedLooseRock &&
                        (coherentRouteObject ||
                        (!vegetation && horizontalDistance <= 30f &&
                         Mathf.Abs(bounds.center.y - statueBounds.center.y) <= 30f &&
                         largestDimension <= (shrineFeature ? 48f : 22f)));
                }

                if (!keep &&
                    TryGetParticleBounds(
                        authoredObject.gameObject,
                        out Bounds particleBounds))
                {
                    float shrineDistance = Vector2.Distance(
                        new Vector2(
                            particleBounds.center.x,
                            particleBounds.center.z),
                        statueHorizontal);
                    float routeDistance = DistanceToHorizontalSegment(
                        new Vector2(
                            particleBounds.center.x,
                            particleBounds.center.z),
                        approachHorizontal,
                        statueHorizontal);
                    bool unsupportedHydrology = ContainsOriginCurationToken(
                        objectName,
                        "waterfall", "water_fall", "waterspout", "water",
                        "mist", "spray", "foam", "splash");
                    // note: Magical shrine atmosphere remains eligible, but source-map waterfall spray is rejected until a reviewed water source and catch basin exist in the same landmark.
                    keep = !unsupportedHydrology &&
                        (shrineDistance <= 30f ||
                         (routeCell && routeDistance <= 12f));
                }

                if (keep)
                {
                    authoredObject.gameObject.SetActive(true);
                    retained++;
                }
                else
                {
                    // note: Unsupported terrain-dependent dressing is disabled immediately and released after the frame so the pinned origin does not retain thousands of invisible source-map objects for the whole session.
                    authoredObject.gameObject.SetActive(false);
                    UnityEngine.Object.Destroy(authoredObject.gameObject);
                    excluded++;
                }
            }
        }

        if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(
                statue.gameObject,
                out statueBounds,
                out float statueStructuralBottom))
            return false;

        if (!YQGeneratedWorldTerrain.TrySampleFootprintHeight(
                terrain,
                statueBounds,
                out float terrainHeight,
                out _,
                out _))
        {
            return false;
        }

        // note: The retained Goddess shrine settles by its structural footprint, so its pedestal cannot hover when the imported statue pivot is offset.
        float verticalCorrection =
            terrainHeight -
            statueStructuralBottom -
            0.015f;

        if (float.IsNaN(verticalCorrection) ||
            float.IsInfinity(verticalCorrection) ||
            Mathf.Abs(verticalCorrection) > 80f)
        {
            return false;
        }

        mountainRoot.transform.position += Vector3.up * verticalCorrection;

        for (int cellIndex = 0; cellIndex < cellRoots.Count; cellIndex++)
        {
            Transform cell = cellRoots[cellIndex];

            if (cell == null)
                continue;

            for (int childIndex = 0; childIndex < cell.childCount; childIndex++)
            {
                Transform assembly = cell.GetChild(childIndex);

                if (assembly == null || !assembly.gameObject.activeInHierarchy ||
                    assembly == statue || statue.IsChildOf(assembly))
                {
                    continue;
                }

                if (YQTerrainSupportComposer.TryCreateAssemblyStamp(
                        assembly.gameObject,
                        terrain,
                        4f,
                        out YQTerrainSupportStamp supportStamp))
                {
                    supportStamps.Add(supportStamp);
                }
            }
        }

        if (!TryGetRenderableBounds(statue.gameObject, out groundedStatueBounds))
            return false;
        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] GODDESS SHRINE CURATED\n" +
            "Retained authored shrine objects: " + retained + "\n" +
            "Excluded unsupported wilderness objects: " + excluded + "\n" +
            "Terrain support stamps queued: " + supportStamps.Count + "\n" +
            "Statue grounding correction: " +
            verticalCorrection.ToString("F2") + "m");
        return retained > 0;
    }

    private static bool TryGetParticleBounds(
        GameObject root,
        out Bounds bounds)
    {
        bounds = default;

        if (root == null)
            return false;

        ParticleSystemRenderer[] renderers =
            root.GetComponentsInChildren<ParticleSystemRenderer>(true);
        bool initialized = false;

        for (int index = 0; index < renderers.Length; index++)
        {
            ParticleSystemRenderer renderer = renderers[index];

            if (renderer == null)
                continue;

            string particleContext = renderer.name + " " +
                (renderer.transform.parent != null
                    ? renderer.transform.parent.name
                    : string.Empty);
            if (ContainsOriginCurationToken(
                    particleContext,
                    "waterfall", "water_fall", "waterspout", "water",
                    "mist", "spray", "foam", "splash"))
            {
                ParticleSystem particleSystem =
                    renderer.GetComponent<ParticleSystem>();
                if (particleSystem != null)
                {
                    particleSystem.Stop(
                        true,
                        ParticleSystemStopBehavior.StopEmittingAndClear);
                }
                renderer.enabled = false;
                continue;
            }

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

    private static float DistanceToHorizontalSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= 0.001f)
            return Vector2.Distance(point, start);

        float t = Mathf.Clamp01(
            Vector2.Dot(point - start, segment) / lengthSquared);
        return Vector2.Distance(point, start + segment * t);
    }

    private static IEnumerator TryGetRenderableBoundsRoutine(
        GameObject root,
        Action<bool, Bounds> completed)
    {
        if (root == null)
        {
            completed?.Invoke(false, new Bounds());
            yield break;
        }

        bool rendererInitialized = false;
        Bounds rendererBounds = new Bounds();
        bool colliderInitialized = false;
        Bounds colliderBounds = new Bounds();
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;

            Renderer[] renderers = current.GetComponents<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || renderer is ParticleSystemRenderer)
                    continue;

                if (!rendererInitialized)
                {
                    rendererBounds = renderer.bounds;
                    rendererInitialized = true;
                }
                else
                {
                    rendererBounds.Encapsulate(renderer.bounds);
                }
            }

            Collider[] colliders = current.GetComponents<Collider>();
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || collider.isTrigger)
                    continue;

                if (!colliderInitialized)
                {
                    colliderBounds = collider.bounds;
                    colliderInitialized = true;
                }
                else
                {
                    colliderBounds.Encapsulate(collider.bounds);
                }
            }

            for (int index = 0; index < current.childCount; index++)
                pending.Push(current.GetChild(index));

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StartupHierarchyFrameBudgetSeconds)
            {
                // note: Large reviewed sites yield before hierarchy inspection can consume a visible loading frame.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        completed?.Invoke(
            rendererInitialized || colliderInitialized,
            rendererInitialized ? rendererBounds : colliderBounds);
    }

    private static IEnumerator ConfigureOriginParticlePresentationRoutine(
        GameObject root)
    {
        if (root == null)
            yield break;

        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;

            ParticleSystemRenderer[] renderers =
                current.GetComponents<ParticleSystemRenderer>();

            for (int index = 0; index < renderers.Length; index++)
            {
                ParticleSystemRenderer renderer = renderers[index];

                if (renderer == null)
                    continue;

                // note: Waterfall mist and magical atmosphere are translucent presentation layers; shadow casting/receiving turns their billboards into dark cards.
                renderer.shadowCastingMode =
                    UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode =
                    MotionVectorGenerationMode.ForceNoMotion;
            }

            for (int index = 0; index < current.childCount; index++)
                pending.Push(current.GetChild(index));

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StartupHierarchyFrameBudgetSeconds)
            {
                // note: Particle cleanup shares the same hard per-frame startup budget as material and grounding work.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }
    }

    private static IEnumerator DisableUnsupportedOriginCollidersRoutine(
        GameObject root,
        float maximumDimension)
    {
        if (root == null)
            yield break;

        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;

        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            if (current == null)
                continue;

            Collider[] colliders = current.GetComponents<Collider>();

            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];

                if (collider == null || collider.isTrigger || !collider.enabled)
                    continue;

                Bounds bounds = collider.bounds;
                float largestDimension = Mathf.Max(
                    bounds.size.x,
                    Mathf.Max(bounds.size.y, bounds.size.z));
                string semanticName = collider.name.ToLowerInvariant();
                bool explicitBlocker = ContainsOriginCurationToken(
                    semanticName,
                    "invisible", "blocker", "boundary", "killvolume",
                    "collisionvolume");

                if (explicitBlocker || largestDimension > maximumDimension)
                {
                    // note: Generated terrain owns broad traversal collision; imported scene-wide boundary volumes cannot survive as invisible walls in the curated origin.
                    collider.enabled = false;
                }
            }

            for (int index = 0; index < current.childCount; index++)
                pending.Push(current.GetChild(index));

            if (Time.realtimeSinceStartup - frameStartedAt >=
                StartupHierarchyFrameBudgetSeconds)
            {
                // note: Collider validation may inspect hundreds of imported objects but never as one uninterrupted loading-frame pass.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }
    }

    private static bool ContainsOriginCurationToken(
        string text,
        params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(text) || tokens == null)
            return false;

        for (int index = 0; index < tokens.Length; index++)
        {
            if (!string.IsNullOrWhiteSpace(tokens[index]) &&
                text.IndexOf(
                    tokens[index],
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 GroundOriginApproachPoint(
        Terrain terrain,
        Vector3 point,
        float clearance)
    {
        if (terrain != null)
        {
            // note: Origin interaction points follow the generated terrain while the authored landmark retains its reviewed internal composition.
            point.y = YQGeneratedWorldTerrain.SampleWorldHeight(
                terrain,
                point) + Mathf.Max(0.05f, clearance);
        }

        return point;
    }

    private static Transform FindDescendantByName(
        Transform root,
        string objectName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(
                objectName))
        {
            return null;
        }

        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(
                true);

        for (int i = 0;
             i < transforms.Length;
             i++)
        {
            Transform candidate =
                transforms[i];

            if (candidate == null)
                continue;

            if (string.Equals(
                    candidate.name,
                    objectName,
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void EnsureGeneratedWorldSun(
        Transform parent)
    {
        if (parent == null)
            return;

        Transform existing =
            parent.Find(
                "GeneratedWorldSun");

        if (existing != null)
            return;

        GameObject lightObject =
            new GameObject(
                "GeneratedWorldSun");

        lightObject.transform.SetParent(
            parent,
            false);

        Light light =
            lightObject.AddComponent<Light>();

        light.type =
            LightType.Directional;

        light.intensity =
            1.15f;

        light.shadows =
            LightShadows.Soft;

        light.shadowStrength =
            0.34f;

        light.transform.rotation =
            Quaternion.Euler(
                48f,
                -35f,
                0f);
    }

    private static void PlacePlayerAtGeneratedOrigin(
        Terrain terrain,
        bool useGeneratedOrigin)
    {
        GameObject player =
            null;

        try
        {
            player =
                GameObject.FindGameObjectWithTag(
                    "Player");
        }
        catch
        {
        }

        if (player == null)
        {
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Could not find authoritative Player while " +
                "positioning the generated origin.");

            return;
        }

        Vector3 spawn;

        if (useGeneratedOrigin)
        {
            spawn = _hasGeneratedOriginSpawnOverride
                ? _generatedOriginSpawnOverride
                : new Vector3(0f, 0f, -2.2f);
        }
        else
        {
            PlayerStateManager playerStateManager =
                PlayerStateManager.Instance;

            PlayerState savedState =
                playerStateManager != null
                    ? playerStateManager.state
                    : null;

            // note: Persisted position is authoritative on ordinary loads/rebuilds; the live transform is only a fallback when no save state exists.
            spawn = savedState != null
                ? savedState.lastPosition
                : player.transform.position;
        }

        if (terrain != null)
        {
            float terrainSafeHeight = YQGeneratedWorldTerrain
                .SampleWorldHeight(terrain, spawn) + 0.45f;

            // note: Generated terrain is the absolute safety floor even when an authored semantic projection is malformed or unavailable.
            spawn.y = Mathf.Max(spawn.y, terrainSafeHeight);
        }

        /*
         * Support either legacy CharacterController movement or the
         * current Rigidbody-based player without making assumptions
         * about which one is present.
         */
        CharacterController controller =
            player.GetComponent<
                CharacterController>();

        bool controllerWasEnabled =
            controller != null &&
            controller.enabled;

        Rigidbody body =
            player.GetComponent<
                Rigidbody>();

        if (controller != null)
        {
            controller.enabled =
                false;
        }

        if (body != null)
        {
            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;
        }

        player.transform.position =
            spawn;

        if (useGeneratedOrigin && _hasGeneratedOriginFacingOverride)
        {
            // note: New characters enter already facing the shared Goddess-and-hut composition instead of inheriting an arbitrary bootstrap yaw.
            player.transform.rotation = Quaternion.LookRotation(
                _generatedOriginFacingOverride.normalized,
                Vector3.up);
        }

        if (body != null)
        {
            body.position =
                spawn;

            if (useGeneratedOrigin && _hasGeneratedOriginFacingOverride)
                body.rotation = player.transform.rotation;

            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;
        }

        if (controller != null)
        {
            controller.enabled =
                controllerWasEnabled;
        }

        // note: Origin validation below consumes renderer bounds, so it does not need to force the entire physics world to synchronize during loading.
    }

    // ------------------------------------------------------------
    // PLAN LOOKUPS
    // ------------------------------------------------------------

    private static GeneratedRegionRecord FindRegion(
        GeneratedWorldPlanRecord plan,
        string regionId)
    {
        if (plan == null ||
            plan.regions == null)
        {
            return null;
        }

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region == null)
                continue;

            if (string.Equals(
                    region.regionId,
                    regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return region;
            }
        }

        return null;
    }

    private static GeneratedRegionAssetPaletteRecord
        FindPalette(
            GeneratedWorldPlanRecord plan,
            GeneratedRegionRecord region)
    {
        if (plan == null ||
            region == null ||
            plan.assetPalettes == null)
        {
            return null;
        }

        for (int i = 0;
             i < plan.assetPalettes.Count;
             i++)
        {
            GeneratedRegionAssetPaletteRecord palette =
                plan.assetPalettes[i];

            if (palette == null)
                continue;

            if (!string.IsNullOrWhiteSpace(
                    region.assetPaletteId) &&
                string.Equals(
                    palette.paletteId,
                    region.assetPaletteId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }

            if (string.Equals(
                    palette.regionId,
                    region.regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return palette;
            }
        }

        return null;
    }

    // ------------------------------------------------------------
    // DETERMINISM
    // ------------------------------------------------------------

    private static string SettlementSeed(
        GeneratedSettlementRecord settlement)
    {
        if (settlement == null)
            return "settlement";

        if (!string.IsNullOrWhiteSpace(
                settlement.deterministicSeed))
        {
            return
                settlement.deterministicSeed;
        }

        return
            settlement.settlementId ??
            "settlement";
    }

    private static float DetermineLotFacing(
        Vector3 position)
    {
        if (Mathf.Abs(
                position.x) >
            Mathf.Abs(
                position.z))
        {
            return
                position.x < 0f
                    ? 90f
                    : -90f;
        }

        return
            position.z < 0f
                ? 0f
                : 180f;
    }

    private static float DeterministicQuarterTurn(
        string seed)
    {
        int value =
            PositiveHash(
                seed);

        return
            (value % 4) *
            90f;
    }

    private static float Deterministic01(
        string seed)
    {
        int value =
            PositiveHash(
                seed);

        return
            (value % 100000) /
            99999f;
    }

    private static int PositiveHash(
        string text)
    {
        unchecked
        {
            int hash =
                23;

            string value =
                text ??
                string.Empty;

            for (int i = 0;
                 i < value.Length;
                 i++)
            {
                hash =
                    hash *
                    31 +
                    value[i];
            }

            return
                hash &
                0x7fffffff;
        }
    }

    private static string SafeName(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return
                "GeneratedSettlement";
        }

        char[] chars =
            value
                .Trim()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            char c =
                chars[i];

            if (!char.IsLetterOrDigit(
                    c) &&
                c != '_' &&
                c != '-')
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(
                chars);
    }
}

// note: This deterministic civic layout is co-located with the runtime builder so Unity's generated assembly project always compiles both sides of the placement contract together.
public static class YQGeneratedSettlementCellLayout
{
    public enum Template { Compact, MarketVillage, FortifiedOutpost, DenseCity }
    private enum LayoutFamily { Village, StreetGrid, Courtyard, Interior }

    public struct Node
    {
        public readonly Vector3 position;
        public readonly float yaw;
        public readonly string purpose;

        public Node(Vector3 position, float yaw, string purpose)
        {
            this.position = position;
            this.yaw = yaw;
            this.purpose = purpose;
        }
    }

    // note: Settlement scale is canonical content; a generated city must remain a city even when it is third or later in the persisted world list.
    public static Template ResolveTemplate(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        string kind =
            settlement != null
                ? (settlement.kind ?? string.Empty).Trim().ToLowerInvariant()
                : string.Empty;

        int population =
            settlement != null
                ? settlement.approxPopulation
                : 0;

        int services =
            settlement != null && settlement.serviceSlots != null
                ? settlement.serviceSlots.Count
                : 0;

        if (Contains(kind, "city", "metropolis", "capital") || population >= 80 || services >= 9)
            return Template.DenseCity;

        if (Contains(kind, "town", "market", "riverhold") || population >= 24 || services >= 6)
            return Template.MarketVillage;

        if (Contains(kind, "outpost", "waystation", "fort", "stronghold"))
            return Template.FortifiedOutpost;

        int index = FindSettlementIndex(plan, settlement);
        return index == 0 ? Template.MarketVillage : index == 1 ? Template.FortifiedOutpost : Template.Compact;
    }

    public static bool IsComprehensive(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        return ResolveTemplate(plan, settlement) != Template.Compact;
    }

    public static Node[] GetPathNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        return GetPathNodes(plan, settlement, string.Empty);
    }

    public static Node[] GetPathNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement, string layoutRuleProfile)
    {
        if (!IsComprehensive(plan, settlement))
            return CompactPath;

        // note: The same gameplay cell size receives a different circulation plan for streets, courtyards, and interior packs.
        LayoutFamily family = ResolveLayoutFamily(layoutRuleProfile);
        if (ResolveTemplate(plan, settlement) == Template.DenseCity && family != LayoutFamily.Interior && family != LayoutFamily.Courtyard)
            return DenseCityPath;

        switch (family)
        {
            case LayoutFamily.StreetGrid: return StreetGridPath;
            case LayoutFamily.Courtyard: return CourtyardPath;
            case LayoutFamily.Interior: return InteriorPath;
        }

        return ResolveTemplate(plan, settlement) == Template.MarketVillage ? MarketPath : OutpostPath;
    }

    public static Node[] GetBuildingLots(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        return GetBuildingLots(plan, settlement, string.Empty);
    }

    public static Node[] GetBuildingLots(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement, string layoutRuleProfile)
    {
        if (!IsComprehensive(plan, settlement))
            return CompactLots;

        // note: Lots face the local circulation pattern so shop fronts, rooms, and courtyards do not spawn as a generic loose ring.
        LayoutFamily family = ResolveLayoutFamily(layoutRuleProfile);
        if (ResolveTemplate(plan, settlement) == Template.DenseCity && family != LayoutFamily.Interior && family != LayoutFamily.Courtyard)
            return DenseCityLots;

        switch (family)
        {
            case LayoutFamily.StreetGrid: return StreetGridLots;
            case LayoutFamily.Courtyard: return CourtyardLots;
            case LayoutFamily.Interior: return InteriorLots;
        }

        return ResolveTemplate(plan, settlement) == Template.MarketVillage ? MarketLots : OutpostLots;
    }

    public static Node[] GetPerimeterNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        return GetPerimeterNodes(plan, settlement, string.Empty);
    }

    public static Node[] GetPerimeterNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement, string layoutRuleProfile)
    {
        if (!IsComprehensive(plan, settlement))
            return EmptyNodes;

        LayoutFamily family = ResolveLayoutFamily(layoutRuleProfile);
        if (ResolveTemplate(plan, settlement) == Template.DenseCity || family == LayoutFamily.StreetGrid || family == LayoutFamily.Interior)
            return EmptyNodes;

        // note: An ordinary market village is visually open; only the explicitly fortified template receives authored defensive cells.
        return ResolveTemplate(plan, settlement) == Template.FortifiedOutpost ? OutpostPerimeter : EmptyNodes;
    }

    public static Node[] GetCivicDecorationNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        return GetCivicDecorationNodes(plan, settlement, string.Empty);
    }

    public static Node[] GetCivicDecorationNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement, string layoutRuleProfile)
    {
        if (!IsComprehensive(plan, settlement))
            return EmptyNodes;

        LayoutFamily family = ResolveLayoutFamily(layoutRuleProfile);
        if (ResolveTemplate(plan, settlement) == Template.DenseCity && family != LayoutFamily.Interior && family != LayoutFamily.Courtyard)
            return DenseCityDecorations;

        switch (family)
        {
            case LayoutFamily.StreetGrid: return StreetGridDecorations;
            case LayoutFamily.Courtyard: return CourtyardDecorations;
            case LayoutFamily.Interior: return InteriorDecorations;
        }

        return ResolveTemplate(plan, settlement) == Template.MarketVillage ? MarketDecorations : OutpostDecorations;
    }

    public static Node[] GetShrubNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        return GetShrubNodes(plan, settlement, string.Empty);
    }

    public static Node[] GetShrubNodes(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement, string layoutRuleProfile)
    {
        if (!IsComprehensive(plan, settlement))
            return EmptyNodes;

        LayoutFamily family = ResolveLayoutFamily(layoutRuleProfile);
        if (ResolveTemplate(plan, settlement) == Template.DenseCity || family == LayoutFamily.StreetGrid || family == LayoutFamily.Interior)
            return EmptyNodes;

        return ResolveTemplate(plan, settlement) == Template.MarketVillage ? MarketShrubs : OutpostShrubs;
    }

    // note: These service-aware stations make a merchant, guard, and notable quest source readable from the lane rather than a random ring around town.
    public static Vector3 GetResidentLocalPosition(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement, GeneratedNpcPlanRecord npc, int index, string seed)
    {
        if (!IsComprehensive(plan, settlement))
            return CompactResident(seed, index);

        string role = (npc != null ? npc.role : string.Empty) ?? string.Empty;
        role = role.ToLowerInvariant();
        bool merchant = (npc != null && npc.merchant) || Contains(role, "merchant", "vendor", "trader", "innkeeper");
        bool guard = (npc != null && npc.guard) || Contains(role, "guard", "warden", "captain", "watch");
        bool quest = (npc != null && npc.notable) || Contains(role, "chief", "reeve", "mayor", "elder", "guide", "scout", "scholar", "scribe", "healer");
        Template template = ResolveTemplate(plan, settlement);
        bool marketLike = template == Template.MarketVillage || template == Template.DenseCity;
        Vector3 roleSocketOffset =
            ResidentRoleSocketOffset(
                index,
                seed);

        if (merchant)
            return (marketLike ? new Vector3(-5.25f, 0f, -1.5f) : new Vector3(5.5f, 0f, -4.5f)) + roleSocketOffset;
        if (guard)
            return new Vector3(0f, 0f, -18f) + roleSocketOffset;
        if (quest)
            return (marketLike ? new Vector3(5.5f, 0f, 6f) : new Vector3(-5f, 0f, 7.5f)) + roleSocketOffset;

        Node[] lots = GetBuildingLots(plan, settlement);
        Node lot = lots[Mathf.Abs(index) % lots.Length];
        return lot.position + new Vector3(DeterministicSigned(seed + ":resident_x") * 1.6f, 0f, DeterministicSigned(seed + ":resident_z") * 1.6f);
    }

    public static Vector3 GetLandmarkPosition(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        Template template = ResolveTemplate(plan, settlement);
        return template == Template.DenseCity ? new Vector3(0f, 0f, 42f) : template == Template.MarketVillage ? Vector3.zero : template == Template.FortifiedOutpost ? new Vector3(0f, 0f, 27f) : new Vector3(0f, 0f, 20f);
    }

    private static readonly Node[] EmptyNodes = Array.Empty<Node>();
    private static readonly Node[] CompactPath = Lane(12f, 8, 4f, 0f, "lane");
    private static readonly Node[] MarketPath = Combine(Lane(30f, 11, 6f, 0f, "main_street"), Combine(CrossLane(24f, 9, 6f, -7f, "market_street"), CrossLane(18f, 7, 6f, 11f, "civic_lane")));
    private static readonly Node[] OutpostPath = Combine(Lane(27f, 10, 6f, 0f, "gate_lane"), Combine(CrossLane(18f, 7, 6f, -5f, "supply_yard"), CrossLane(14f, 5, 7f, 12f, "command_yard")));
    private static readonly Node[] StreetGridPath = Combine(Lane(30f, 11, 6f, 0f, "avenue"), Combine(CrossLane(24f, 9, 6f, -12f, "south_street"), CrossLane(24f, 9, 6f, 12f, "north_street")));
    private static readonly Node[] DenseCityPath = Combine(VerticalLane(-8f, 34f, 13, 5.7f, "west_avenue"), Combine(VerticalLane(8f, 34f, 13, 5.7f, "east_avenue"), Combine(CrossLane(30f, 11, 6f, -16f, "south_cross_street"), Combine(CrossLane(30f, 11, 6f, 0f, "market_cross_street"), CrossLane(30f, 11, 6f, 16f, "north_cross_street")))));
    private static readonly Node[] CourtyardPath = new[] { new Node(new Vector3(-12f, 0f, -10f), 0f, "courtyard_edge"), new Node(new Vector3(0f, 0f, -10f), 0f, "courtyard_edge"), new Node(new Vector3(12f, 0f, -10f), 0f, "courtyard_edge"), new Node(new Vector3(12f, 0f, 0f), 90f, "courtyard_edge"), new Node(new Vector3(12f, 0f, 10f), 90f, "courtyard_edge"), new Node(new Vector3(0f, 0f, 10f), 0f, "courtyard_edge"), new Node(new Vector3(-12f, 0f, 10f), 0f, "courtyard_edge"), new Node(new Vector3(-12f, 0f, 0f), 90f, "courtyard_edge") };
    private static readonly Node[] InteriorPath = Combine(Lane(10f, 5, 5f, 0f, "interior_hall"), Lane(8f, 4, 5f, 90f, "interior_cross_hall"));
    private static readonly Node[] CompactLots = new[] { new Node(new Vector3(-10f, 0f, -9f), 270f, "residence"), new Node(new Vector3(10f, 0f, -9f), 90f, "residence"), new Node(new Vector3(-10f, 0f, 9f), 270f, "residence"), new Node(new Vector3(10f, 0f, 9f), 90f, "residence") };
    // note: The village is a plaza with arrival, trade, craft, residential, and civic edges—not two rows of interchangeable houses.
    private static readonly Node[] MarketLots = new[] { new Node(new Vector3(-14f, 0f, -24f), 270f, "trade_house"), new Node(new Vector3(14f, 0f, -24f), 90f, "supply_house"), new Node(new Vector3(-20f, 0f, -10f), 270f, "market_shop"), new Node(new Vector3(20f, 0f, -10f), 90f, "market_shop"), new Node(new Vector3(-25f, 0f, 7f), 270f, "forge_or_workshop"), new Node(new Vector3(25f, 0f, 7f), 90f, "inn_or_clinic"), new Node(new Vector3(-20f, 0f, 21f), 315f, "residence"), new Node(new Vector3(20f, 0f, 21f), 45f, "residence"), new Node(new Vector3(-9f, 0f, 32f), 0f, "residence"), new Node(new Vector3(9f, 0f, 32f), 0f, "civic_house"), new Node(new Vector3(-32f, 0f, -23f), 270f, "garden_house"), new Node(new Vector3(32f, 0f, -23f), 90f, "service_house") };
    private static readonly Node[] OutpostLots = new[] { new Node(new Vector3(-14f, 0f, -23f), 270f, "guardhouse"), new Node(new Vector3(14f, 0f, -23f), 90f, "supply_house"), new Node(new Vector3(-20f, 0f, -8f), 270f, "workshop"), new Node(new Vector3(20f, 0f, -8f), 90f, "trader_post"), new Node(new Vector3(-20f, 0f, 8f), 270f, "quarters"), new Node(new Vector3(20f, 0f, 8f), 90f, "quarters"), new Node(new Vector3(-14f, 0f, 21f), 315f, "barracks"), new Node(new Vector3(14f, 0f, 21f), 45f, "watch_house"), new Node(new Vector3(-9f, 0f, 29f), 0f, "command_house"), new Node(new Vector3(9f, 0f, 29f), 0f, "armory") };
    private static readonly Node[] StreetGridLots = new[] { new Node(new Vector3(-22f, 0f, -24f), 270f, "street_shop"), new Node(new Vector3(22f, 0f, -24f), 90f, "street_shop"), new Node(new Vector3(-27f, 0f, -9f), 270f, "workshop"), new Node(new Vector3(27f, 0f, -9f), 90f, "inn_or_clinic"), new Node(new Vector3(-27f, 0f, 9f), 270f, "service_house"), new Node(new Vector3(27f, 0f, 9f), 90f, "market_shop"), new Node(new Vector3(-20f, 0f, 24f), 315f, "residence"), new Node(new Vector3(20f, 0f, 24f), 45f, "civic_house"), new Node(new Vector3(-8f, 0f, 32f), 0f, "residence"), new Node(new Vector3(8f, 0f, 32f), 0f, "archive_or_temple"), new Node(new Vector3(-8f, 0f, -32f), 180f, "gate_service"), new Node(new Vector3(8f, 0f, -32f), 180f, "guardhouse") };
    // note: Dense cities occupy four district edges around two avenues and three cross streets, producing blocks and intersections instead of one infinitely repeated corridor.
    private static readonly Node[] DenseCityLots = new[] { new Node(new Vector3(-29f, 0f, -24f), 270f, "gate_service"), new Node(new Vector3(-29f, 0f, -8f), 270f, "street_shop"), new Node(new Vector3(-29f, 0f, 8f), 270f, "forge_or_workshop"), new Node(new Vector3(-29f, 0f, 24f), 270f, "residence"), new Node(new Vector3(29f, 0f, -24f), 90f, "guardhouse"), new Node(new Vector3(29f, 0f, -8f), 90f, "street_shop"), new Node(new Vector3(29f, 0f, 8f), 90f, "market_shop"), new Node(new Vector3(29f, 0f, 24f), 90f, "residence"), new Node(new Vector3(-22f, 0f, -34f), 180f, "warehouse"), new Node(new Vector3(-7f, 0f, -34f), 180f, "inn_or_clinic"), new Node(new Vector3(7f, 0f, -34f), 180f, "guild_service"), new Node(new Vector3(22f, 0f, -34f), 180f, "street_shop"), new Node(new Vector3(-22f, 0f, 34f), 0f, "residence"), new Node(new Vector3(-7f, 0f, 34f), 0f, "civic_house"), new Node(new Vector3(7f, 0f, 34f), 0f, "temple_or_archive"), new Node(new Vector3(22f, 0f, 34f), 0f, "residence") };
    private static readonly Node[] CourtyardLots = new[] { new Node(new Vector3(-18f, 0f, -14f), 315f, "gate_house"), new Node(new Vector3(18f, 0f, -14f), 45f, "gate_house"), new Node(new Vector3(-20f, 0f, 4f), 270f, "workshop"), new Node(new Vector3(20f, 0f, 4f), 90f, "residence"), new Node(new Vector3(-15f, 0f, 19f), 315f, "residence"), new Node(new Vector3(15f, 0f, 19f), 45f, "residence"), new Node(new Vector3(0f, 0f, 25f), 0f, "shrine_or_hall"), new Node(new Vector3(0f, 0f, -25f), 180f, "market_gate") };
    private static readonly Node[] InteriorLots = Lots(new[] { new Vector3(-7f, 0f, -7f), new Vector3(7f, 0f, -7f), new Vector3(-7f, 0f, 1f), new Vector3(7f, 0f, 1f), new Vector3(-7f, 0f, 9f), new Vector3(7f, 0f, 9f) }, new[] { "entry_room", "service_room", "workshop", "archive_or_clinic", "quarters", "ritual_or_command_room" });
    private static readonly Node[] OutpostPerimeter = Perimeter(24f, 23f);
    private static readonly Node[] MarketDecorations = CivicNodes(12f, "market");
    private static readonly Node[] OutpostDecorations = CivicNodes(13f, "outpost");
    private static readonly Node[] StreetGridDecorations = CivicNodes(14f, "street_corner");
    private static readonly Node[] DenseCityDecorations = new[] { new Node(new Vector3(-6f, 0f, -24f), 90f, "city_corner"), new Node(new Vector3(6f, 0f, -24f), 270f, "city_corner"), new Node(new Vector3(-6f, 0f, -13f), 90f, "city_corner"), new Node(new Vector3(6f, 0f, -13f), 270f, "city_corner"), new Node(new Vector3(-6f, 0f, -2f), 90f, "market_corner"), new Node(new Vector3(6f, 0f, -2f), 270f, "market_corner"), new Node(new Vector3(-6f, 0f, 9f), 90f, "city_corner"), new Node(new Vector3(6f, 0f, 9f), 270f, "city_corner"), new Node(new Vector3(-6f, 0f, 20f), 90f, "city_corner"), new Node(new Vector3(6f, 0f, 20f), 270f, "city_corner"), new Node(new Vector3(-6f, 0f, 30f), 90f, "civic_corner"), new Node(new Vector3(6f, 0f, 30f), 270f, "civic_corner") };
    private static readonly Node[] CourtyardDecorations = CivicNodes(10f, "courtyard");
    private static readonly Node[] InteriorDecorations = CivicNodes(7f, "interior_landmark");
    private static readonly Node[] MarketShrubs = Shrubs(27f, 25f);
    private static readonly Node[] OutpostShrubs = Shrubs(28f, 26f);

    private static LayoutFamily ResolveLayoutFamily(string layoutRuleProfile)
    {
        string profile = (layoutRuleProfile ?? string.Empty).Trim().ToLowerInvariant();

        // note: Pack-level profiles keep dense cities, rings, and interiors from inheriting a rural-village footprint.
        if (profile.Contains("grid") || profile.Contains("dock"))
            return LayoutFamily.StreetGrid;
        if (profile.Contains("arena") || profile.Contains("ruin") || profile.Contains("dungeon") || profile.Contains("mountain"))
            return LayoutFamily.Courtyard;
        if (profile.Contains("interior") || profile.Contains("room") || profile.Contains("tunnel") || profile.Contains("crypt") || profile.Contains("clinic"))
            return LayoutFamily.Interior;
        return LayoutFamily.Village;
    }

    private static Node[] Lane(float start, int count, float spacing, float yaw, string purpose)
    {
        Node[] result = new Node[count];
        for (int i = 0; i < count; i++)
            result[i] = yaw == 0f ? new Node(new Vector3(0f, 0f, -start + i * spacing), yaw, purpose) : new Node(new Vector3(-start + i * spacing, 0f, 2f), yaw, purpose);
        return result;
    }

    private static Node[] CrossLane(float start, int count, float spacing, float z, string purpose)
    {
        Node[] result = new Node[count];
        for (int i = 0; i < count; i++)
        {
            // note: City cross streets keep their own longitudinal coordinate so separate blocks never stack the same path prefabs.
            result[i] = new Node(new Vector3(-start + i * spacing, 0f, z), 90f, purpose);
        }

        return result;
    }

    private static Node[] VerticalLane(float x, float start, int count, float spacing, string purpose)
    {
        Node[] result = new Node[count];
        for (int i = 0; i < count; i++)
        {
            // note: Independent avenue offsets let a dense city form actual blocks rather than stacking every road tile on one center line.
            result[i] = new Node(new Vector3(x, 0f, -start + i * spacing), 0f, purpose);
        }

        return result;
    }

    private static Node[] Lots(Vector3[] positions, string purpose)
    {
        string[] purposes = new string[positions.Length];
        for (int i = 0; i < purposes.Length; i++) purposes[i] = purpose;
        return Lots(positions, purposes);
    }

    private static Node[] Lots(Vector3[] positions, string[] purposes)
    {
        Node[] result = new Node[positions.Length];
        for (int i = 0; i < positions.Length; i++)
            result[i] = new Node(positions[i], positions[i].x < 0f ? 0f : 180f, purposes[i]);
        return result;
    }

    // note: The south side deliberately has a central break, giving every cell a clear approach and preventing a sealed procedural wall ring.
    private static Node[] Perimeter(float width, float depth)
    {
        return new[]
        {
            new Node(new Vector3(-width, 0f, -depth), 90f, "perimeter"), new Node(new Vector3(-width * .5f, 0f, -depth), 90f, "perimeter"), new Node(new Vector3(width * .5f, 0f, -depth), 90f, "perimeter"), new Node(new Vector3(width, 0f, -depth), 90f, "perimeter"),
            new Node(new Vector3(-width, 0f, -depth * .3f), 0f, "perimeter"), new Node(new Vector3(-width, 0f, depth * .35f), 0f, "perimeter"), new Node(new Vector3(-width, 0f, depth), 90f, "perimeter"), new Node(new Vector3(-width * .5f, 0f, depth), 90f, "perimeter"), new Node(new Vector3(0f, 0f, depth), 90f, "perimeter"), new Node(new Vector3(width * .5f, 0f, depth), 90f, "perimeter"), new Node(new Vector3(width, 0f, depth), 90f, "perimeter"), new Node(new Vector3(width, 0f, depth * .35f), 0f, "perimeter"), new Node(new Vector3(width, 0f, -depth * .3f), 0f, "perimeter")
        };
    }

    private static Node[] CivicNodes(float width, string purpose)
    {
        return new[]
        {
            new Node(new Vector3(-5f, 0f, -2f), 90f, purpose), new Node(new Vector3(5f, 0f, -2f), 270f, purpose), new Node(new Vector3(-6f, 0f, 4f), 0f, purpose), new Node(new Vector3(6f, 0f, 4f), 180f, purpose), new Node(new Vector3(-width, 0f, 1f), 90f, "street_light"), new Node(new Vector3(width, 0f, 1f), 270f, "street_light"), new Node(new Vector3(-8f, 0f, 12f), 0f, purpose), new Node(new Vector3(8f, 0f, 12f), 180f, purpose)
        };
    }

    private static Node[] Shrubs(float width, float depth)
    {
        return new[]
        {
            new Node(new Vector3(-width, 0f, -depth * .55f), 0f, "shrub"), new Node(new Vector3(width, 0f, -depth * .55f), 0f, "shrub"), new Node(new Vector3(-width * .78f, 0f, depth), 0f, "shrub"), new Node(new Vector3(width * .78f, 0f, depth), 0f, "shrub"), new Node(new Vector3(-width, 0f, depth * .35f), 0f, "shrub"), new Node(new Vector3(width, 0f, depth * .35f), 0f, "shrub"), new Node(new Vector3(-width * .38f, 0f, depth + 3f), 0f, "shrub"), new Node(new Vector3(width * .38f, 0f, depth + 3f), 0f, "shrub")
        };
    }

    private static Node[] Combine(Node[] first, Node[] second)
    {
        Node[] result = new Node[first.Length + second.Length];
        Array.Copy(first, result, first.Length);
        Array.Copy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static int FindSettlementIndex(GeneratedWorldPlanRecord plan, GeneratedSettlementRecord settlement)
    {
        if (plan == null || plan.settlements == null || settlement == null) return -1;
        for (int i = 0; i < plan.settlements.Count; i++)
        {
            GeneratedSettlementRecord candidate = plan.settlements[i];
            if (candidate == settlement || candidate != null && string.Equals(candidate.settlementId, settlement.settlementId, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    private static Vector3 CompactResident(string seed, int index)
    {
        float angle = Deterministic01(seed + ":angle") * Mathf.PI * 2f;
        float radius = Mathf.Lerp(index % 2 == 0 ? 4.5f : 9f, index % 2 == 0 ? 10.5f : 18f, Deterministic01(seed + ":radius"));
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    private static Vector3 ResidentRoleSocketOffset(
        int index,
        string seed)
    {
        // note: Role anchors are small authored clusters rather than a single shared coordinate, preventing multiple merchants, guards, or quest NPCs from materializing inside one another.
        int stableIndex =
            Mathf.Abs(index);

        float lateral =
            (stableIndex % 3 - 1) *
            1.75f;

        float depth =
            ((stableIndex / 3) % 2) *
            1.6f;

        return new Vector3(
            lateral + DeterministicSigned(seed + ":role_x") * 0.25f,
            0f,
            depth + DeterministicSigned(seed + ":role_z") * 0.25f);
    }

    private static bool Contains(string value, params string[] parts)
    {
        for (int i = 0; i < parts.Length; i++) if (value.Contains(parts[i])) return true;
        return false;
    }

    private static float DeterministicSigned(string value) { return Deterministic01(value) * 2f - 1f; }

    private static float Deterministic01(string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++) { hash ^= text[i]; hash *= 16777619; }
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
