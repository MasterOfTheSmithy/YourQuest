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

public enum YQSpatialPlanningMode
{
    PersistedV1 = 0,
    V2Shadow = 1,
    V2Authoritative = 2,
    V2Preferred = 3
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

    // note: Prefer the reviewed V2 transaction; automatic V1 fallback is limited to the legacy materialization path, never the compiled-world startup.
    public const YQSpatialPlanningMode ActiveSpatialPlanningMode =
        YQSpatialPlanningMode.V2Preferred;

    private static GeneratedWorldPlanRecord _runtimeAuthorityPlan;
    private static YQSpatialPlanAuthority _runtimeAuthority =
        YQSpatialPlanAuthority.None;
    private static GeneratedSpatialWorldPlanV2Record _runtimeAuthorityArtifact;
    private static string _runtimeAuthorityHash = string.Empty;

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

    public static bool RunsV2Shadow =>
        ActiveSpatialPlanningMode ==
        YQSpatialPlanningMode.V2Shadow;

    public static bool UsesV2SpatialRuntime =>
        _runtimeAuthority == YQSpatialPlanAuthority.AcceptedV2 ||
        (_runtimeAuthorityPlan == null &&
         ActiveSpatialPlanningMode == YQSpatialPlanningMode.V2Authoritative);

    public static bool UsesV2SpatialRuntimeFor(
        GeneratedWorldPlanRecord plan)
    {
        return TryResolveRuntimeAuthority(
                   plan, out YQSpatialPlanAuthority authority, out _) &&
               authority == YQSpatialPlanAuthority.AcceptedV2;
    }

    public static bool TryResolveRuntimeAuthority(
        GeneratedWorldPlanRecord plan,
        out YQSpatialPlanAuthority authority,
        out string reason)
    {
        if (ReferenceEquals(_runtimeAuthorityPlan, plan))
        {
            authority = _runtimeAuthority;
            reason = authority == YQSpatialPlanAuthority.None
                ? "The runtime spatial authority was not prepared."
                : string.Empty;
            return authority != YQSpatialPlanAuthority.None;
        }

        // note: Before a build freezes its transaction, resolve normally; every later consumer receives the exact same cached authority.
        return YQSpatialPlanVersionRouter.TryResolveActive(
            plan,
            out authority,
            out reason);
    }

    public static void LockRuntimeAuthority(
        GeneratedWorldPlanRecord plan,
        YQSpatialPlanAuthority authority)
    {
        // note: Every consumer in one build observes one frozen authority; a background artifact cannot switch terrain/sites halfway through construction.
        _runtimeAuthorityPlan = plan;
        _runtimeAuthority = authority;
        _runtimeAuthorityArtifact =
            authority == YQSpatialPlanAuthority.AcceptedV2
                ? plan?.spatialPlanV2
                : null;
        _runtimeAuthorityHash =
            _runtimeAuthorityArtifact?.contentHash ?? string.Empty;
    }

    public static bool IsRuntimeAuthorityCurrent(
        GeneratedWorldPlanRecord plan)
    {
        if (!ReferenceEquals(_runtimeAuthorityPlan, plan) ||
            _runtimeAuthority == YQSpatialPlanAuthority.None)
        {
            return false;
        }

        if (_runtimeAuthority != YQSpatialPlanAuthority.AcceptedV2)
            return true;

        // note: A V2 transaction aborts if another owner swaps its accepted artifact or claimed hash while terrain and sites are being constructed.
        return ReferenceEquals(
                   _runtimeAuthorityArtifact,
                   plan?.spatialPlanV2) &&
               string.Equals(
                   _runtimeAuthorityHash,
                   plan?.spatialPlanV2?.contentHash,
                   StringComparison.Ordinal);
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeAuthority()
    {
        _runtimeAuthorityPlan = null;
        _runtimeAuthority = YQSpatialPlanAuthority.None;
        _runtimeAuthorityArtifact = null;
        _runtimeAuthorityHash = string.Empty;
    }
}

[DisallowMultipleComponent]
public sealed class YQGeneratedWorldRuntimeBuilder : MonoBehaviour
{
    private const float MaximumAsyncInstantiateIntegrationMilliseconds = 2f;

    private static bool _initialGenerationLifecycleLocked;

    private static bool _initialGenerationLifecycleLatched;
    private static string _initialGenerationStartingWorldSeed =
    string.Empty;
    private static float _initialGenerationLockStartedAt = -1f;
    private static float _initialGenerationLastProgressAt = -1f;
    private static float _initialGenerationLastWatchdogUpdateAt = -1f;
    private static bool _initialGenerationDeadlineWarningIssued;
    private const float MaximumInitialGenerationStallSeconds = 120f;
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
        YQGeneratedWorldLayout.OriginGoddessSummitOffset;
    private static readonly Vector3 OriginWitchHouseOffset =
        YQGeneratedWorldLayout.OriginWitchHouseOffset;
    // note: Keep Archivist Vey on a reviewed interior socket so the hand-curated origin never relocates him through seeded role sampling.
    private const float OriginVeySocketNormalizedX = 0.56f;
    private const float OriginVeySocketNormalizedZ = 0.42f;
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
        _initialGenerationLastProgressAt = -1f;
        _initialGenerationLastWatchdogUpdateAt = -1f;
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

    // note: Expose only read-only materialization diagnostics so editor verification can distinguish active work from a rejected or incomplete build.
    public bool IsMaterializationInProgress =>
        _buildInProgress;

    public bool HasMaterializationFailed =>
        _worldMaterializationFailed;

    public bool LastProfileTerrainRestoreSucceeded =>
        _lastProfileTerrainRestoreSucceeded;

    // note: Surface the exact rejected snapshot gate so Continue diagnostics can distinguish stale identity from corrupt persisted terrain.
    public string LastProfileTerrainRestoreFailureReason =>
        _lastProfileTerrainRestoreFailureReason;

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
            int settlementCount = ExpectedStartupSettlementCount(plan);
            int generatedNpcCount = plan.generatedNpcs != null
                ? plan.generatedNpcs.Count
                : 0;
            bool populationReady = generatedNpcCount == 0 ||
                _materializedGeneratedNpcCount == generatedNpcCount;
            // note: Startup matches physically required settlements; V2 distant settlements remain owned by streaming.
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
        // note: The watchdog measures time since meaningful progress rather than total creation time; a large but advancing world must never be mistaken for a hang.
        _initialGenerationLockStartedAt = Time.unscaledTime;
        _initialGenerationLastProgressAt = Time.unscaledTime;
        _initialGenerationLastWatchdogUpdateAt = Time.unscaledTime;
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
        _initialGenerationLastProgressAt = -1f;
        _initialGenerationLastWatchdogUpdateAt = -1f;

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

    [Header("World Construction Validation")]
    [Tooltip("Require V2 preparation and preflight to succeed. Enable for production world-generation verification; disabled preserves existing V1 compatibility.")]
    // note: Strict verification rejects V2 fallback before replacement; existing scenes keep their explicit compatibility behavior.
    public bool requireV2ConstructionSuccess;

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

    private string _builtProfileId =
        string.Empty;

    private string _builtWorldId =
        string.Empty;

    private string _profileTerrainSnapshotJson =
        string.Empty;

    private bool _lastProfileTerrainRestoreSucceeded;
    private string _lastProfileTerrainRestoreFailureReason = string.Empty;

    private YQProfileSaveSystem _terrainSnapshotProviderOwner;

    private int _builtSettlementCount;

    private bool _worldMaterializationFailed;
    private bool _initialGenerationWatchdogAborted;
    private WorldState _failedBuildWorld;
    private GeneratedWorldPlanRecord _failedBuildPlan;
    private string _failedBuildSeed = string.Empty;
    private string _failedBuildSignature = string.Empty;
    private bool _cancellingBuild;

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
    private IEnumerator _populationExecution;

    private bool _populationBuildInProgress;
    private bool _cancellingPopulation;

    private string _revealedInitialGenerationSeed =
        string.Empty;

    /*
     * Physical construction is staged through a coroutine during initial
     * generation so loading dialogue can actually render between terrain,
     * environment, settlement and building phases.
     */
    private bool _buildInProgress;

    private bool _compiledBindingsChangedDuringBuild;

    private bool _spatialPlanChangedDuringBuild;


    private readonly Dictionary<string, string>
        _resolvedSemanticCompositionSeedsV2 =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

    private Coroutine _buildCoroutine;
    private IEnumerator _buildExecution;
    // note: A manual rebuild is an explicit recovery request and may re-materialize the currently persisted plan after an interrupted initial-generation latch.
    private bool _forceNextBuild;
    // note: Keep the latest materialization admission decision observable so a startup wait identifies its blocking guard instead of looking like a healthy idle builder.
    private string _lastMaterializationDecision = "not-attempted";

    public string LastMaterializationDecision =>
        _lastMaterializationDecision;

    private static bool HasPersistedCanonicalWorld(GeneratedWorldPlanRecord plan)
    {
        // note: An accepted spatial artifact or populated semantic frontier is durable world state, even when its seed matches the scaffold captured by the startup lock.
        return plan != null &&
            ((plan.spatialPlanV2 != null &&
              plan.spatialPlanV2.acceptanceState == GeneratedSpatialPlanAcceptanceState.Accepted) ||
             (plan.semanticChunks != null && plan.semanticChunks.Count > 0 &&
              plan.spatialPlan != null &&
             !string.IsNullOrWhiteSpace(plan.spatialPlan.semanticFingerprint)));
    }

    private bool CanAttemptLegacySpatialFallback(GeneratedWorldPlanRecord plan)
    {
        // note: A persisted V1 plan certifies data, not physically buildable settlements. Compiled-world startup must repair V2 or fail before replacement instead of downgrading to an unverified construction path.
        return !YQWorldGenerationArchitecture.UsesCompiledWorld &&
            !HasPersistedCanonicalWorld(plan) &&
            !requireV2ConstructionSuccess &&
            YQWorldGenerationArchitecture.ActiveSpatialPlanningMode == YQSpatialPlanningMode.V2Preferred;
    }
    // note: The final camera fade belongs to the build transaction and must not outlive a retry or profile change.
    private Coroutine _revealCoroutine;
    private YQStartupLoadingScreen _revealPresentation;

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

        // note: Unity integrates async-created objects and calls Awake on the main thread; cap that shared phase so streamed props cannot monopolize a frame.
        float asyncInstantiateIntegrationMilliseconds =
            UnityEngine.AsyncInstantiateOperation.GetIntegrationTimeMS();
        if (!(asyncInstantiateIntegrationMilliseconds > 0f) ||
            asyncInstantiateIntegrationMilliseconds > MaximumAsyncInstantiateIntegrationMilliseconds)
        {
            UnityEngine.AsyncInstantiateOperation.SetIntegrationTimeMS(
                MaximumAsyncInstantiateIntegrationMilliseconds);
        }

        // note: Register terrain as a profile-owned auxiliary snapshot before creation/continue saves can publish their next paired revision.
        EnsureProfileTerrainSnapshotProvider();

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

    private void EnsureProfileTerrainSnapshotProvider()
    {
        YQProfileSaveSystem profileSystem = YQProfileSaveSystem.Instance;
        if (profileSystem == null || _terrainSnapshotProviderOwner == profileSystem)
            return;

        // note: Profile commits retain the latest accepted heightfield as a checksummed auxiliary document beside player and world state.
        if (profileSystem.RegisterAuxiliaryDocument(
                YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId,
                CreateProfileTerrainSnapshotForCommit))
            _terrainSnapshotProviderOwner = profileSystem;
    }

    private string CreateProfileTerrainSnapshotForCommit()
    {
        YQProfileSaveSystem profileSystem = YQProfileSaveSystem.Instance;
        WorldStateManager worldManager = WorldStateManager.Instance;
        WorldState world = worldManager != null ? worldManager.State : null;
        GeneratedWorldPlanRecord plan = world != null ? world.generatedWorldPlan : null;
        if (profileSystem == null || string.IsNullOrWhiteSpace(profileSystem.ActiveProfileId))
            return YQGeneratedWorldTerrain.CreateEmptyProfileSnapshotJson();

        string activeProfileId = profileSystem.ActiveProfileId;
        profileSystem.TryGetLoadedAuxiliaryDocument(
            YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId,
            out string loadedCandidate);
        string[] candidates = { _profileTerrainSnapshotJson, loadedCandidate };
        bool hasCurrentWorldIdentity = world != null && world.worldIdentity != null && plan != null;
        string expectedFingerprint = hasCurrentWorldIdentity
            ? YQStateContract.Sha256Hex(BuildVisualSignature(plan))
            : string.Empty;

        // note: Profile saves can run during shutdown or startup before world services are fully available; retain an owned snapshot then and let restore validate its world identity.
        for (int index = 0; index < candidates.Length; index++)
        {
            string candidate = candidates[index];
            if (IsCompatibleProfileTerrainSnapshot(
                    candidate,
                    activeProfileId,
                    world,
                    plan,
                    expectedFingerprint))
                return candidate;
        }

        if (profileSystem.TryGetPriorAuxiliaryDocument(
                YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId,
                candidate => IsCompatibleProfileTerrainSnapshot(
                    candidate,
                    activeProfileId,
                    world,
                    plan,
                    expectedFingerprint),
                out string priorSnapshot))
        {
            // note: Recover a compatible committed heightfield when a later early-startup save replaced it with an empty placeholder.
            _profileTerrainSnapshotJson = priorSnapshot;
            return priorSnapshot;
        }

        // note: Empty clears prior terrain only when no committed snapshot matches the active profile's current world identity and plan.
        return YQGeneratedWorldTerrain.CreateEmptyProfileSnapshotJson();
    }

    // note: Carry terrain forward only when profile ownership and the exact accepted world plan still match.
    private static bool IsCompatibleProfileTerrainSnapshot(
        string candidate,
        string activeProfileId,
        WorldState world,
        GeneratedWorldPlanRecord plan,
        string expectedFingerprint)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(activeProfileId))
            return false;

        YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord record = null;
        try
        {
            record = JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(candidate);
        }
        catch (ArgumentException)
        {
            // note: A malformed optional cache is skipped so another committed profile copy can still be considered.
        }

        if (record == null || record.schemaVersion != 1 || !record.hasTerrain ||
            string.IsNullOrWhiteSpace(record.heightmapChecksum) ||
            !string.Equals(record.ownerProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase))
            return false;

        bool hasCurrentWorldIdentity = world != null && world.worldIdentity != null && plan != null;
        if (!hasCurrentWorldIdentity)
            return true;

        return string.Equals(world.worldIdentity.ownerProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.worldId, world.worldIdentity.worldId, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(record.worldSeed, plan.worldSeed, StringComparison.Ordinal) &&
            string.Equals(record.planFingerprint, expectedFingerprint, StringComparison.Ordinal);
    }

    private void Update()
    {
        float watchdogNow = Time.unscaledTime;
        if (IsInitialGenerationGameplayLocked &&
            _initialGenerationLastWatchdogUpdateAt >= 0f)
        {
            float updateGap =
                watchdogNow - _initialGenerationLastWatchdogUpdateAt;

            if (updateGap > 1f &&
                _initialGenerationLastProgressAt >= 0f)
            {
                // note: Editor pauses, suspended windows, and a single hard frame cannot consume the no-progress allowance while generation coroutines are unable to advance.
                _initialGenerationLastProgressAt += updateGap;
            }
        }

        _initialGenerationLastWatchdogUpdateAt = watchdogNow;

        if (IsInitialGenerationGameplayLocked &&
            _initialGenerationLastProgressAt >= 0f &&
            !_initialGenerationDeadlineWarningIssued &&
            watchdogNow - _initialGenerationLastProgressAt >=
                MaximumInitialGenerationStallSeconds)
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
        catch (Exception exception)
        {
            Debug.LogError(
                "[WORLDGEN ERROR] Authoritative player lookup failed during world-build coordination. " +
                ", reason=" + exception.Message);
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

        int settlementCount = ExpectedStartupSettlementCount(plan);

        int generatedNpcCount =
            plan.generatedNpcs != null
                ? plan.generatedNpcs.Count
                : 0;

        bool sameBuiltPlanIdentity =
            _runtimeRoot != null &&
            _generatedTerrain != null &&
            _builtWorldState == world &&
            _builtPlan == plan &&
            // note: Reusing the same semantic-plan object cannot hide replacement of its accepted spatial artifact from the rebuild check.
            YQWorldGenerationArchitecture.IsRuntimeAuthorityCurrent(plan) &&
            string.Equals(
                _builtWorldSeed,
                plan.worldSeed,
                StringComparison.Ordinal) &&
            _builtSettlementCount == settlementCount;

        // note: Accepted world plans are immutable save records; reuse their compact signature instead of allocating and traversing it every 0.75 seconds.
        string visualSignature = sameBuiltPlanIdentity
            ? _builtVisualSignature
            : BuildVisualSignature(plan);

        // note: A profile reload replaces managed save objects; matching the persisted world identity and complete build fingerprint keeps its already-materialized terrain alive on Continue.
        if (!sameBuiltPlanIdentity && !_worldMaterializationFailed &&
            _runtimeRoot != null && _generatedTerrain != null && world.worldIdentity != null)
        {
            YQProfileSaveSystem activeProfile = YQProfileSaveSystem.Instance;
            bool sameProfileWorld = activeProfile != null &&
                string.Equals(activeProfile.ActiveProfileId, _builtProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(world.worldIdentity.ownerProfileId, _builtProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(world.worldIdentity.worldId, _builtWorldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(_builtWorldSeed, plan.worldSeed, StringComparison.Ordinal) &&
                _builtSettlementCount == settlementCount &&
                string.Equals(_builtVisualSignature, visualSignature, StringComparison.Ordinal);
            if (sameProfileWorld && YQWorldGenerationArchitecture.TryResolveRuntimeAuthority(
                    plan, out YQSpatialPlanAuthority reloadedAuthority, out _))
            {
                YQWorldGenerationArchitecture.LockRuntimeAuthority(plan, reloadedAuthority);
                _builtWorldState = world;
                _builtPlan = plan;
                sameBuiltPlanIdentity = true;
                Debug.Log("[YQGeneratedWorldRuntimeBuilder] CONTINUE WORLD REUSE PASS profile=" + _builtProfileId +
                          " world=" + _builtWorldId + " seed=" + _builtWorldSeed + " terrain=retained");
            }
        }

        /*
         * The terrain and settlements may already exist before canonical
         * population generation finishes.
         *
         * Materialize the population in-place rather than rebuilding
         * the entire generated world.
         */
        bool sameBuiltWorld =
            // note: Diagnostic geometry from a rejected build must not trigger late population work as though the world were playable.
            !_worldMaterializationFailed &&
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
                _populationExecution = RunOwnedGenerationRoutine(
                    BuildPopulationInPlaceRoutine(
                        world,
                        plan,
                        generatedNpcCount,
                        registry),
                    exception => ReportPopulationExecutionFailure(world, plan, exception));
                Coroutine populationCoroutine = StartCoroutine(_populationExecution);
                // note: An immediate completion must not leave a stale coroutine handle behind.
                _populationBuildCoroutine = _populationBuildInProgress ? populationCoroutine : null;
            }

            return;
        }

        bool sameFailedAttempt =
            _worldMaterializationFailed &&
            _failedBuildWorld == world &&
            _failedBuildPlan == plan &&
            string.Equals(
                _failedBuildSeed,
                plan.worldSeed,
                StringComparison.Ordinal) &&
            string.Equals(
                _failedBuildSignature,
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

    // note: Own ordinary nested iterators explicitly, preserving Unity wait objects and bounding immediate scheduling work per frame.
    public static IEnumerator RunOwnedGenerationRoutine(IEnumerator routine, Action<Exception> failed)
    {
        if (routine == null)
            yield break;
        Stack<IEnumerator> pending = new Stack<IEnumerator>();
        pending.Push(routine);
        Exception failure = null;
        int immediateSteps = 0;
        try
        {
            while (pending.Count > 0)
            {
                if (++immediateSteps > 64)
                {
                    immediateSteps = 0;
                    yield return null;
                }
                IEnumerator current = pending.Peek();
                bool advanced;
                object yielded;
                try
                {
                    advanced = current.MoveNext();
                    yielded = advanced ? current.Current : null;
                }
                catch (Exception exception)
                {
                    failure = exception;
                    break;
                }

                if (!advanced)
                {
                    pending.Pop();
                    try { (current as IDisposable)?.Dispose(); }
                    catch (Exception exception) { failure = exception; break; }
                    continue;
                }
                if (yielded is IEnumerator nested && !(yielded is CustomYieldInstruction))
                {
                    pending.Push(nested);
                    // note: A malformed recursive iterator must not grow an unlimited stack across frames.
                    if (pending.Count > 128)
                    {
                        failure = new InvalidOperationException("World generation exceeded the nested coroutine depth limit.");
                        break;
                    }
                    continue;
                }

                // note: Null, async operations, WaitForSeconds and custom Unity waits retain their original scheduling semantics.
                immediateSteps = 0;
                yield return yielded;
            }
        }
        finally
        {
            // note: Dispose innermost work first on exception or cancellation; one broken cleanup must not strand the remaining parents.
            while (pending.Count > 0)
            {
                try { (pending.Pop() as IDisposable)?.Dispose(); }
                catch (Exception exception)
                {
                    failure = failure == null ? exception : new AggregateException(failure, exception);
                }
            }
            if (failure != null)
            {
                if (failed != null)
                    failed(failure);
                else
                    Debug.LogException(failure);
            }
        }
    }

    private void RecordFailedWorldBuild(WorldState world, GeneratedWorldPlanRecord plan)
    {
        // note: Failure identity is independent of how far physical construction got, so exceptions cannot trigger an automatic rebuild loop.
        _worldMaterializationFailed = true;
        // note: Preserve a specific gate reason when one exists, but never leave the editor heartbeat claiming that a failed transaction merely started.
        if (string.IsNullOrWhiteSpace(_lastMaterializationDecision) ||
            _lastMaterializationDecision.StartsWith("started:", StringComparison.OrdinalIgnoreCase))
            _lastMaterializationDecision = "rejected: world construction failed before the terminal gate";
        _failedBuildWorld = world;
        _failedBuildPlan = plan;
        _failedBuildSeed = plan.worldSeed;
        _failedBuildSignature = BuildVisualSignature(plan);
        if (IsInitialGenerationGameplayLocked && !_initialGenerationDeadlineWarningIssued)
        {
            _initialGenerationDeadlineWarningIssued = true;
            YQStartupLoadingScreen.ShowGenerationFailure(
                "World construction could not finish safely. Retry, or return to the title screen.",
                RetryAfterGenerationWatchdog, ReturnToTitleAfterGenerationWatchdog);
        }
    }

    private void ReportPopulationExecutionFailure(WorldState world, GeneratedWorldPlanRecord plan, Exception exception)
    {
        Debug.LogException(exception, this);
        _populationBuildInProgress = false;
        _populationBuildCoroutine = null;
        if (_cancellingBuild || _cancellingPopulation || !IsCurrentBuildContext(world, plan))
            return;

        // note: Unexpected NPC construction errors require explicit retry instead of repeating the same exception every two seconds.
        _materializedGeneratedNpcCount = -1;
        _nextPopulationMaterializationRetryAt = float.PositiveInfinity;
        if (IsInitialGenerationGameplayLocked)
        {
            _initialGenerationDeadlineWarningIssued = true;
            _initialGenerationWatchdogAborted = true;
            YQStartupLoadingScreen.ShowGenerationFailure(
                "The world is built, but its inhabitants could not be placed safely. Retry their placement, or return to the title screen.",
                RetryPopulationAfterFailure, ReturnToTitleAfterGenerationWatchdog, "Retry inhabitants");
        }
    }

    private void RetryPopulationAfterFailure()
    {
        // note: Keep accepted terrain and cells; the coordinator will retry only the in-place population pass.
        CancelPopulationBuildRoutine();
        YQStartupLoadingScreen.ClearGenerationFailure();
        _nextPopulationMaterializationRetryAt = 0f;
        _initialGenerationWatchdogAborted = false;
        _initialGenerationDeadlineWarningIssued = false;
        TouchInitialGenerationWatchdog();
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

        try
        {
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
        }
        finally
        {
            // note: Terminal success, rejection and explicit cancellation all release population ownership.
            _populationBuildInProgress = false;
            _populationBuildCoroutine = null;
        }
    }

    // ------------------------------------------------------------
    // WORLD BUILD
    // ------------------------------------------------------------

    [ContextMenu("Build Generated World")]
    public void BuildGeneratedWorld()
    {
        EnsureProfileTerrainSnapshotProvider();
        if (!YQWorldGenerationArchitecture.AllowsLegacyRuntimeBuilder &&
            !YQWorldGenerationArchitecture.UsesCompiledWorld)
        {
            _lastMaterializationDecision = "rejected: materialization architecture unavailable";
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
            _lastMaterializationDecision = "deferred: materialization already in progress";
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
            _lastMaterializationDecision = "rejected: world state unavailable";
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
            _lastMaterializationDecision = "rejected: generated world plan unavailable";
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Active save has no generated world plan.");

            return;
        }

        plan.EnsureCollections();

        if (string.IsNullOrWhiteSpace(
                plan.worldSeed))
        {
            _lastMaterializationDecision = "rejected: generated world seed unavailable";
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
                StringComparison.OrdinalIgnoreCase) &&
            !_forceNextBuild &&
            !HasPersistedCanonicalWorld(plan))
        {
            _lastMaterializationDecision = "deferred: active plan is still the startup scaffold";
            return;
        }
        if (plan.settlements == null ||
            plan.settlements.Count == 0)
        {
            _lastMaterializationDecision = "rejected: generated plan has no settlements";
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
            _lastMaterializationDecision = "deferred: world-plan generation request is in flight";
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
            _lastMaterializationDecision = "rejected: runtime asset registry unavailable";
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "YQRuntimeWorldAssetRegistry could not be loaded.");

            return;
        }

        CancelPopulationBuildRoutine();

        // note: Consume the one-shot recovery override only after all build inputs are present and the transaction can actually start.
        _forceNextBuild = false;

        _worldMaterializationFailed = false;

        // note: Direct build calls, not only explicit rebuilds, invalidate any previously scheduled gameplay reveal.
        CancelInitialGenerationReveal();

        _buildExecution = RunOwnedGenerationRoutine(
                BuildGeneratedWorldRoutine(
                    world,
                    plan,
                    registry),
                exception =>
                {
                    // note: This also covers setup exceptions before the construction iterator enters its own finally block.
                    Debug.LogException(exception, this);
                    _buildInProgress = false;
                    _buildCoroutine = null;
                    if (!_cancellingBuild && IsCurrentWorldPlanReference(world, plan))
                    {
                        _lastMaterializationDecision =
                            "rejected: coroutine exception: " + exception.Message;
                        RecordFailedWorldBuild(world, plan);
                    }
                });
        _lastMaterializationDecision = "started: materialization coroutine scheduled";
        Coroutine buildCoroutine = StartCoroutine(_buildExecution);
        _buildCoroutine = _buildInProgress ? buildCoroutine : null;
    }

    private IEnumerator BuildGeneratedWorldRoutine(
        WorldState world,
        GeneratedWorldPlanRecord plan,
        YQRuntimeWorldAssetRegistry registry)
    {
        _buildInProgress =
            true;
        _lastProfileTerrainRestoreSucceeded = false;
        _lastProfileTerrainRestoreFailureReason = string.Empty;

        // note: A rebuild may follow a previously successful origin; clear its landing point before this transaction can fail and accidentally reuse stale world height.
        _generatedOriginSpawnOverride = Vector3.zero;
        _hasGeneratedOriginSpawnOverride = false;
        _generatedOriginFacingOverride = Vector3.forward;
        _hasGeneratedOriginFacingOverride = false;
        _spatialPlanChangedDuringBuild = false;
        // note: Binding and composition decisions belong to this transaction, including the preflight that runs before terrain construction.
        _compiledBindingsChangedDuringBuild = false;
        _resolvedSemanticCompositionSeedsV2.Clear();
        GeneratedSpatialWorldPlanV2Record previousSpatialArtifact = plan.spatialPlanV2;
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
        bool reachedAcceptanceGate = false;
        // note: Failed-build continuation may reuse only canonical state that existed before this transaction began; newly compiled artifacts stay behind the commit boundary.
        bool canonicalWorldAtBuildStart = HasPersistedCanonicalWorld(plan);
        // note: Candidate terrain remains separately owned until synthesis succeeds and this build still owns the active plan.
        GameObject terrainStagingRoot = null;
        Terrain stagedTerrain = null;
        bool restoredProfileTerrain = false;
        // note: A compatible prior revision must be republished after restoration so repeated Continue loads stop depending on history fallback.
        bool restoredProfileTerrainFromPriorRevision = false;
        string loadedProfileTerrainJson = string.Empty;
        string loadedProfileTerrainChecksum = string.Empty;
        if (narrate)
        {
            // note: Every deliberately started attempt gets a fresh no-progress window, including manual rebuild after a terminal failure.
            _initialGenerationDeadlineWarningIssued = false;
            TouchInitialGenerationWatchdog();
        }

        try
        {
            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Planning the world",
                1,
                9,
                "Validating terrain, water, routes, and site reserves before construction",
                0.56f);
            bool spatialAuthorityReady = false;
            // note: Compile and prepare the complete spatial plan before the old world is detached, so a rejected V2 candidate can fall back without leaving an empty or half-built scene.
            yield return PrepareSpatialAuthorityRoutine(
                world,
                plan,
                success => spatialAuthorityReady = success);
            TouchInitialGenerationWatchdog();

            if (!spatialAuthorityReady ||
                !IsCurrentBuildContext(world, plan))
            {
                _worldMaterializationFailed = true;
                _lastMaterializationDecision =
                    "rejected: spatial planning did not produce a safe runtime authority";
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] Spatial planning did not produce a safe runtime authority; the existing world was preserved.");
                yield break;
            }

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Preparing approved assets",
                2,
                9,
                "Loading only the palettes selected by the accepted world plan",
                0.59f);
            // note: Warm only the accepted plan's palette packs; all unrelated genre libraries remain unloaded.
            yield return
                registry.PreloadAssetPathsRoutine(
                    CollectActivePaletteAssetPaths(
                        plan));
            TouchInitialGenerationWatchdog();

            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            {
                bool cellsReady = false;
                string cellFailure = string.Empty;
                // note: Validate real reviewed cell functions and footprints before destroying the prior runtime hierarchy; a mathematically valid plan alone is not a buildable world.
                yield return PreflightSpatialCellsV2Routine(
                    world,
                    plan,
                    (success, failure) =>
                    {
                        cellsReady = success;
                        cellFailure = failure;
                    });
                if (!IsCurrentBuildContext(world, plan))
                    yield break;

                if (!cellsReady)
                {
                    // note: A canonical generated save must never downgrade to the legacy tree-and-rock renderer when V2 cell realization fails; fail the build so the missing physical contract remains visible.
                    if (!CanAttemptLegacySpatialFallback(plan) ||
                        !YQSpatialPlanVersionRouter.TryResolve(
                            plan, YQSpatialPlanningMode.PersistedV1,
                            out YQSpatialPlanAuthority fallback, out _))
                    {
                        // note: Strict preflight rejection must discard this build's uncommitted candidate, preserving the previous accepted save authority.
                        if (_spatialPlanChangedDuringBuild)
                            plan.spatialPlanV2 = previousSpatialArtifact;
                        _spatialPlanChangedDuringBuild = false;
                        _resolvedSemanticCompositionSeedsV2.Clear();
                        _worldMaterializationFailed = true;
                        _lastMaterializationDecision =
                            "rejected: V2 cell preflight failed: " + cellFailure;
                        Debug.LogError("[YQGeneratedWorldRuntimeBuilder] V2 cell preflight failed before world replacement: " + cellFailure);
                        yield break;
                    }

                    // note: Reject the whole V2 candidate, not individual V2 sites; the fallback terrain, routes and geometry all return to the persisted V1 authority.
                    if (_spatialPlanChangedDuringBuild)
                        plan.spatialPlanV2 = previousSpatialArtifact;
                    _spatialPlanChangedDuringBuild = false;
                    _resolvedSemanticCompositionSeedsV2.Clear();
                    YQWorldGenerationArchitecture.LockRuntimeAuthority(plan, fallback);
                    Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] V2 CELL PREFLIGHT FALLBACK TO V1: " + cellFailure);
                }
            }

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

            terrainStagingRoot = new GameObject("YQ_TerrainBuildStaging");
            terrainStagingRoot.SetActive(false);
            EnsureProfileTerrainSnapshotProvider();
            YQProfileSaveSystem profileSystem = YQProfileSaveSystem.Instance;
            string profileId = profileSystem != null ? profileSystem.ActiveProfileId : string.Empty;
            string terrainPlanFingerprint = YQStateContract.Sha256Hex(BuildVisualSignature(plan));
            bool hasSavedTerrain = profileSystem != null && profileSystem.TryGetLoadedAuxiliaryDocument(
                YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId, out loadedProfileTerrainJson);
            if (!IsCompatibleProfileTerrainSnapshot(
                    loadedProfileTerrainJson,
                    profileId,
                    world,
                    plan,
                    terrainPlanFingerprint) &&
                profileSystem != null &&
                profileSystem.TryGetPriorAuxiliaryDocument(
                    YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId,
                    candidate => IsCompatibleProfileTerrainSnapshot(
                        candidate,
                        profileId,
                        world,
                        plan,
                        terrainPlanFingerprint),
                    out string priorProfileTerrainJson))
            {
                loadedProfileTerrainJson = priorProfileTerrainJson;
                hasSavedTerrain = true;
                restoredProfileTerrainFromPriorRevision = true;
                Debug.LogWarning(
                    "[YQGeneratedWorldRuntimeBuilder] Active profile revision had no compatible terrain snapshot; recovering the matching heightfield from an earlier committed revision.");
            }
            if (hasSavedTerrain)
            {
                YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord savedTerrainRecord = null;
                try
                {
                    savedTerrainRecord = JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(loadedProfileTerrainJson);
                }
                catch (ArgumentException)
                {
                    // note: Invalid optional terrain data falls back to the selected profile's persisted world plan.
                }
                loadedProfileTerrainChecksum = savedTerrainRecord != null ? savedTerrainRecord.heightmapChecksum : string.Empty;
                yield return YQGeneratedWorldTerrain.BuildCandidateFromProfileSnapshotRoutine(
                    terrainStagingRoot.transform,
                    loadedProfileTerrainJson,
                    profileId,
                    world,
                    terrainPlanFingerprint,
                    terrain =>
                    {
                        stagedTerrain = terrain;
                        restoredProfileTerrain = terrain != null;
                    },
                    failure => _lastProfileTerrainRestoreFailureReason = failure);
                TouchInitialGenerationWatchdog();
                if (!IsCurrentBuildContext(world, plan)) yield break;
                if (restoredProfileTerrain)
                {
                    _lastProfileTerrainRestoreSucceeded = true;
                    _profileTerrainSnapshotJson = loadedProfileTerrainJson;
                    Debug.Log("[YQGeneratedWorldRuntimeBuilder] PROFILE TERRAIN RESTORE PASS profile=" + profileId +
                              " world=" + world.worldIdentity.worldId + " seed=" + plan.worldSeed +
                              " checksum=" + loadedProfileTerrainChecksum);
                }
                else
                {
                    Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Saved terrain snapshot was rejected (" +
                                     (string.IsNullOrWhiteSpace(_lastProfileTerrainRestoreFailureReason)
                                         ? "unknown reason"
                                         : _lastProfileTerrainRestoreFailureReason) +
                                     "); restoring terrain from that profile's persisted plan.");
                }
            }

            if (stagedTerrain == null)
            {
                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Forming the terrain",
                    2,
                    9,
                    "Preparing the accepted profile terrain",
                    0.60f);
                yield return YQGeneratedWorldTerrain.BuildCandidateRoutine(
                    terrainStagingRoot.transform, plan, terrain => stagedTerrain = terrain);
                TouchInitialGenerationWatchdog();
                if (!IsCurrentBuildContext(world, plan)) yield break;
            }
            if (stagedTerrain == null)
            {
                Debug.LogError("[YQGeneratedWorldRuntimeBuilder] Candidate terrain failed; existing runtime retained.");
                yield break;
            }
            if (!restoredProfileTerrain)
            {
                // note: First materialization creates the accepted landform foundation; profile snapshots already contain this finalized terrain.
                yield return YQGeneratedWorldEnvironment.BuildTerrainFoundationRoutine(stagedTerrain, plan, registry, true);
                TouchInitialGenerationWatchdog();
                if (!IsCurrentBuildContext(world, plan)) yield break;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // note: Selecting the runtime root exposes regions, macro features, footprints, roads, and bridge crossings without affecting release generation.
            _runtimeRoot.AddComponent<YQGeneratedWorldDebugOverlay>().Configure(plan);
#endif

            /*
             * Terrain is deterministic from the persisted world seed.
             */
            // note: Transfer the already-built candidate only after successful synthesis; the old root was preserved throughout all yielded uploads.
            stagedTerrain.transform.SetParent(_runtimeRoot.transform, true);
            stagedTerrain.gameObject.name = YQGeneratedWorldTerrain.RuntimeTerrainObjectName;
            _generatedTerrain = stagedTerrain;
            stagedTerrain = null;
            Destroy(terrainStagingRoot);
            terrainStagingRoot = null;

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Forming the terrain",
                3,
                9,
                "Synthesizing the deterministic heightfield",
                0.61f);
            // note: Terrain height synthesis and upload are frame-budgeted so the Goddess presentation never waits behind one monolithic terrain build frame.
            // note: Terrain synthesis was completed in staging before replacing the previous runtime hierarchy.
            TouchInitialGenerationWatchdog();

            if (_generatedTerrain == null)
            {
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] Generated terrain build did not produce a runtime terrain.");
                yield break;
            }

            EnsureGeneratedWorldSun(
                _runtimeRoot.transform);

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

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Shaping landforms and roads",
                4,
                9,
                "Stamping regional hills, mountains, basins, and navigable approaches",
                0.68f);
            // note: Terrain geometry becomes canonical first; construction pads are finalized before surface painting so visual slopes and physical slopes cannot disagree.
            // note: Regional height shaping already completed under staging ownership; do not stamp it twice after transfer.
            TouchInitialGenerationWatchdog();

            bool constructionTerrainPrepared =
                restoredProfileTerrain;

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Shaping landforms and roads",
                4,
                9,
                "Grading deterministic support pads beneath generated sites",
                0.70f);
            if (!restoredProfileTerrain)
            {
                // note: Construction pads are authored incrementally on first materialization; loaded profile terrain already includes the accepted pads.
                yield return PrepareDeterministicConstructionTerrainRoutine(
                    plan,
                    _generatedTerrain,
                    prepared => constructionTerrainPrepared = prepared);
                TouchInitialGenerationWatchdog();
            }

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Painting the terrain",
                5,
                9,
                "Applying biome textures and lived-area paths to the final heightfield",
                0.74f);
            bool mandatoryRoadsReady = false;
            // note: Paint the finalized construction-aware heightfield once; repainting before and after grading doubled GPU uploads and left every late pad with the wrong material mask.
            yield return
                YQGeneratedWorldEnvironment.BuildTerrainSurfaceRoutine(
                    _generatedTerrain,
                    plan,
                    registry,
                    success => mandatoryRoadsReady = success);
            TouchInitialGenerationWatchdog();

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Curating the wilderness",
                6,
                9,
                "Beginning water, foliage, rocks, caves, and regional encounters",
                0.76f);
            // note: Dressing is a read-only consumer of the final canonical heightfield. Streamed sites can no longer reshape terrain after this point.
            yield return
                YQGeneratedWorldEnvironment.BuildWildernessRoutine(
                    _runtimeRoot.transform,
                    _generatedTerrain,
                    plan,
                    registry);
            TouchInitialGenerationWatchdog();

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Building the starting scene",
                7,
                9,
                "Grounding the Goddess stage, Vey's hut, Vey, and the player threshold",
                0.87f);
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
            TouchInitialGenerationWatchdog();

            // note: Only the initial new-world transaction uses the authored threshold; rebuilds and spatial upgrades retain the live continuation coordinate.
            PlacePlayerAtGeneratedOrigin(
                _generatedTerrain,
                plan,
                narrate);

            int settlementsBuilt =
                0;

            int settlementsSkipped =
                0;

            int settlementTotal =
                plan.settlements != null
                    ? plan.settlements.Count
                    : 0;

            int requiredMinimumSettlements =
                settlementTotal > 0
                    ? Mathf.Min(settlementTotal, 2)
                    : 0;
            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            {
                // note: Only V2 settlements whose accepted footprint is inside the startup terrain are required in the initial scene; distant owners are admitted by the semantic streamer.
                requiredMinimumSettlements = CountInitialTerrainSettlements(
                    plan,
                    _generatedTerrain);
            }
            int settlementsInstantiated = 0;
            int settlementCandidates = plan.spatialPlan != null &&
                                       plan.spatialPlan.metrics != null
                ? plan.spatialPlan.metrics.candidateCount
                : settlementTotal;

            Debug.Log(
                "[YQGeneratedWorldRuntimeBuilder] SETTLEMENT GENERATION STARTED\n" +
                "Seed: " + plan.worldSeed + "\n" +
                "Minimum: " + requiredMinimumSettlements + "\n" +
                "Target: " + settlementTotal + "\n" +
                "Maximum: " + settlementTotal + "\n" +
                "Scored candidates: " + settlementCandidates);

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

                if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
                    !IsSettlementInsideGeneratedTerrain(plan, settlement, _generatedTerrain))
                {
                    // note: Do not run the legacy startup settlement builder against a distant V2 owner; its physical realization is a streamed-cell responsibility.
                    Debug.Log(
                        "[YQGeneratedWorldRuntimeBuilder] V2 SETTLEMENT DEFERRED TO STREAMING: " +
                        settlement.displayName);
                    continue;
                }

                settlement.EnsureCollections();

                GeneratedRegionRecord region =
                    FindRegion(
                        plan,
                        settlement.regionId);

                if (region == null)
                {
                    Debug.LogError(
                        "[WORLDGEN ERROR] Settlement region binding missing; deterministic first-region fallback engaged. " +
                        "Settlement='" +
                        settlement.displayName +
                        "', requestedRegion='" +
                        settlement.regionId +
                        "', seed=" + plan.worldSeed + ".");
                    region = plan.regions != null && plan.regions.Count > 0
                        ? plan.regions[0]
                        : null;
                    if (region == null)
                    {
                        settlementsSkipped++;
                        continue;
                    }
                }

                region.EnsureCollections();

                GeneratedRegionAssetPaletteRecord palette =
                    FindPalette(
                        plan,
                        region);

                if (palette == null)
                {
                    Debug.LogError(
                        "[WORLDGEN ERROR] Settlement palette binding missing; deterministic first-palette fallback engaged. " +
                        "Settlement='" +
                        settlement.displayName +
                        "', region='" +
                        region.regionId +
                        "', seed=" + plan.worldSeed + ".");
                    palette = plan.assetPalettes != null && plan.assetPalettes.Count > 0
                        ? plan.assetPalettes[0]
                        : null;
                    if (palette == null)
                    {
                        settlementsSkipped++;
                        continue;
                    }
                }

                palette.EnsureCollections();

                float settlementStartProgress =
                    Mathf.Lerp(
                        0.89f,
                        0.92f,
                        settlementTotal > 0
                            ? i /
                              (float)settlementTotal
                            : 0f);

                float settlementEndProgress =
                    Mathf.Lerp(
                        0.89f,
                        0.92f,
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

                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Preparing settlements and hostile sites",
                    8,
                    9,
                    "Validating semantic settlement " + (i + 1) + " of " +
                    settlementTotal + ": " + settlement.displayName,
                    Mathf.Lerp(
                        0.89f,
                        0.92f,
                        settlementTotal > 0 ? i / (float)settlementTotal : 1f));

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
                TouchInitialGenerationWatchdog();

                if (!_lastSettlementMaterialized &&
                    !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
                {
                    // note: Missing reviewed bindings or rejected slices fall back to the existing palette-driven civic builder; a mandatory settlement record may never silently vanish.
                    YQCompiledWorldSiteInstance.RemovePreparedSite(
                        settlement.settlementId);
                    yield return BuildSettlementRoutine(
                        plan,
                        settlement,
                        region,
                        palette,
                        registry,
                        false,
                        settlementStartProgress,
                        settlementEndProgress,
                        true);
                    TouchInitialGenerationWatchdog();
                }

                if (_lastSettlementMaterialized)
                {
                    settlementsBuilt++;
                    bool geometryReady =
                        !YQWorldGenerationArchitecture.UsesCompiledWorld ||
                        !YQCompiledWorldSiteInstance.HasSite(settlement.settlementId);

                    if (!geometryReady &&
                        settlementsInstantiated < requiredMinimumSettlements)
                    {
                        yield return YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine(
                            settlement.settlementId,
                            success => geometryReady = success);
                        TouchInitialGenerationWatchdog();

                        if (!geometryReady &&
                            !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
                        {
                            // note: A prepared-but-unloadable reviewed settlement is replaced deterministically by the palette civic fallback before world acceptance.
                            YQCompiledWorldSiteInstance.RemovePreparedSite(
                                settlement.settlementId);
                            _lastSettlementMaterialized = false;
                            yield return BuildSettlementRoutine(
                                plan,
                                settlement,
                                region,
                                palette,
                                registry,
                                false,
                                settlementStartProgress,
                                settlementEndProgress,
                                true);
                            geometryReady = _lastSettlementMaterialized;
                        }
                    }

                    if (!geometryReady)
                    {
                        // note: Surface the stable site identity when streaming admission fails after a successful settlement binding, separating geometry load from content selection.
                        string siteFailure;
                        _lastMaterializationDecision =
                            "rejected: compiled settlement geometry unavailable; settlement=" +
                            settlement.settlementId +
                            (YQCompiledWorldSiteInstance.TryGetLastLoadFailure(
                                settlement.settlementId,
                                out siteFailure)
                                ? ", reason=" + siteFailure
                                : string.Empty);
                    }

                    if (geometryReady)
                        settlementsInstantiated++;
                }
                else
                    settlementsSkipped++;
            }

            // note: A rejected mandatory settlement cannot become playable by constructing more camps or NPCs; end this retry promptly through the existing recovery/finally path.
            if (settlementsSkipped > 0 || settlementsInstantiated < requiredMinimumSettlements)
            {
                _worldMaterializationFailed = true;
                _lastMaterializationDecision =
                    "rejected: required settlement construction " +
                    settlementsInstantiated + "/" + requiredMinimumSettlements +
                    ", skipped=" + settlementsSkipped;
                Debug.LogError("[YQGeneratedWorldRuntimeBuilder] REQUIRED SETTLEMENT CONSTRUCTION FAILED: " +
                    "instantiated=" + settlementsInstantiated + "/" + requiredMinimumSettlements +
                    ", skipped=" + settlementsSkipped + ". Generation stopped before downstream population work.");
                yield break;
            }

            int hostileSitesBuilt = 0;
            int hostileSitesExpected = 0;

            if (YQWorldGenerationArchitecture.UsesCompiledWorld)
            {
                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Preparing settlements and hostile sites",
                    8,
                    9,
                    "Validating hostile semantic cells for off-camera streaming",
                    0.925f);
                yield return BuildCompiledHostileSitesRoutine(
                    plan,
                    (built, expected) =>
                    {
                        hostileSitesBuilt = built;
                        hostileSitesExpected = expected;
                    });
                TouchInitialGenerationWatchdog();
            }

            if (!IsCurrentBuildContext(
                    world,
                    plan))
            {
                yield break;
            }

            if (narrate && YQWorldGenerationArchitecture.UsesCompiledWorld &&
                settlementTotal > 0)
            {
                // note: Prepared settlement cells stream sequentially after unlock and only while off-camera; cloning dense authored towns here previously held the loading screen past its safety deadline.
                Debug.Log(
                    "[YQGeneratedWorldRuntimeBuilder] SEMANTIC SETTLEMENT STREAMING ARMED: " +
                    settlementTotal +
                    " prepared site(s) will materialize off-camera after gameplay unlock.");
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

                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Placing inhabitants and finalizing",
                    9,
                    9,
                    "Materializing persisted NPC records and their starting assignments",
                    0.94f);
                bool populationBuilt = false;
                // note: Initial population materialization is cooperative; title/Goddess presentation keeps receiving frames while settlements and encounters acquire their actors.
                yield return YQGeneratedWorldPopulation.BuildRoutine(
                    _runtimeRoot.transform,
                    _generatedTerrain,
                    plan,
                    registry,
                    success => populationBuilt = success);
                TouchInitialGenerationWatchdog();

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

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Placing inhabitants and finalizing",
                9,
                9,
                "Validating walkable collision, renderer bounds, LODs, and critical ground",
                0.972f);
            YQGeneratedWorldIntegrityValidator.Report worldIntegrity = null;
            yield return YQGeneratedWorldIntegrityValidator.ValidateAndRepairRoutine(
                _runtimeRoot,
                plan.worldSeed,
                "final_world",
                report => worldIntegrity = report);
            int criticalGroundFailures = 0;
            // note: Raycast sampling validates origin and every settlement anchor after generated colliders are enabled but before control is released.
            yield return YQGeneratedWorldIntegrityValidator.ValidateCriticalGroundRoutine(
                _generatedTerrain,
                plan,
                failures => criticalGroundFailures = failures);
            YQGeneratedWorldIntegrityValidator.RouteReport routeIntegrity = null;
            // note: Persisted travel routes must retain continuous collision and traversable terrain before the loading screen can hand control to the player.
            yield return YQGeneratedWorldIntegrityValidator.ValidateRouteTraversalRoutine(
                _generatedTerrain,
                plan,
                report => routeIntegrity = report);
            TouchInitialGenerationWatchdog();

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

            YQStartupLoadingScreen.SetGenerationWorkStage(
                "Placing inhabitants and finalizing",
                9,
                9,
                "Verifying the completed world and preparing the camera handoff",
                0.985f);

            _builtSettlementCount =
                settlementsBuilt;

            bool terrainReady =
    _generatedTerrain != null;

            bool roadsReady =
                mandatoryRoadsReady;

            bool constructionReady =
                constructionTerrainPrepared;

            bool originReady =
                originMaterialized;

            bool settlementsReady =
                settlementsBuilt == requiredMinimumSettlements &&
                settlementsSkipped == 0 &&
                settlementsInstantiated >= requiredMinimumSettlements;

            bool worldIntegrityReady =
                worldIntegrity != null &&
                worldIntegrity.IsValid;

            bool groundReady =
                criticalGroundFailures == 0;

            bool routeIntegrityReady =
                routeIntegrity != null &&
                routeIntegrity.IsValid;

            bool hostileSitesReady =
                hostileSitesBuilt == hostileSitesExpected;

            _worldMaterializationFailed =
                !terrainReady ||
                !roadsReady ||
                !constructionReady ||
                !originReady ||
                !settlementsReady ||
                !worldIntegrityReady ||
                !groundReady ||
                !routeIntegrityReady ||
                !hostileSitesReady;
            // note: Reaching the terminal gate is distinct from early coroutine exit; both rejected outcomes must stop automatic retries.
            reachedAcceptanceGate = true;

            // note: Persist the semantic presentation fingerprint so a later curated genre/palette shift triggers one deterministic rebuild.
            _builtVisualSignature =
                BuildVisualSignature(
                    plan);

            if (_worldMaterializationFailed)
            {
                // note: Publish the final gate vector through the existing editor heartbeat so a rejected transaction identifies its actual blocker without relying on a locked Unity log.
                _lastMaterializationDecision =
                    "rejected: final gate terrain=" + terrainReady +
                    ", roads=" + roadsReady +
                    ", construction=" + constructionReady +
                    ", origin=" + originReady +
                    ", settlements=" + settlementsReady +
                    ", integrity=" + worldIntegrityReady +
                    ", ground=" + groundReady +
                    ", routes=" + routeIntegrityReady +
                    // note: Include route evidence counters in the editor heartbeat so a route-only rejection names the failed measurement category.
                    ", routeSamples=" + (routeIntegrity != null ? routeIntegrity.sampledPoints : -1) +
                    ", routeMissingGround=" + (routeIntegrity != null ? routeIntegrity.missingGroundPoints : -1) +
                    ", routeHeightSteps=" + (routeIntegrity != null ? routeIntegrity.impassableHeightSteps : -1) +
                    ", routeTraversalSamples=" + (routeIntegrity != null ? routeIntegrity.measuredTraversalSamples : -1) +
                    ", routeTraversalIssues=" + (routeIntegrity != null ? routeIntegrity.traversalIssueSamples : -1) +
                    ", routeClearanceSaturations=" + (routeIntegrity != null ? routeIntegrity.clearanceQuerySaturations : -1) +
                    ", routeBlockers=" + (routeIntegrity != null ? routeIntegrity.unresolvedStructuralBlockers : -1) +
                    ", routeMeasurementComplete=" + (routeIntegrity != null && routeIntegrity.traversalMeasurementComplete) +
                    ", hostile=" + hostileSitesReady;
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] WORLD MATERIALIZATION REJECTED\n" +

                    "Terrain ready: " +
                    terrainReady + "\n" +

                    "Mandatory roads ready: " +
                    roadsReady + "\n" +

                    "Construction terrain prepared: " +
                    constructionReady + "\n" +

                    "Origin ready: " +
                    originReady + "\n" +

                    "Settlements ready: " +
                    settlementsReady + "\n" +

                    "Expected settlements: " +
                    settlementTotal + "\n" +

                    "Prepared settlement stream roots: " +
                    settlementsBuilt + "\n" +

                    "Skipped settlements: " +
                    settlementsSkipped + "\n" +

                    "Required instantiated settlements: " +
                    requiredMinimumSettlements + "\n" +

                    "Instantiated settlement geometry: " +
                    settlementsInstantiated + "\n" +

                    "World integrity report exists: " +
                    (worldIntegrity != null) + "\n" +

                    "World integrity valid: " +
                    worldIntegrityReady + "\n" +

                    "Missing walkable colliders: " +
                    (worldIntegrity != null
                        ? worldIntegrity.missingColliders
                        : -1) + "\n" +

                    "Critical ground valid: " +
                    groundReady + "\n" +

                    "Critical ground failures: " +
                    criticalGroundFailures + "\n" +

                    "Route integrity report exists: " +
                    (routeIntegrity != null) + "\n" +

                    "Route integrity valid: " +
                    routeIntegrityReady + "\n" +

                    "Hostile sites ready: " +
                    hostileSitesReady + "\n" +

                    "Hostile sites: " +
                    hostileSitesBuilt + "/" +
                    hostileSitesExpected
                );
            }
            else if (_compiledBindingsChangedDuringBuild ||
                     _spatialPlanChangedDuringBuild)
            {
                // note: Semantic bindings and a replacement spatial artifact become save authority only after the complete physical transaction validates; failed builds must not persist partial rebinding.
                var saveOwner = WorldStateManager.Instance;
                string saveFailure = "World save owner is unavailable.";
                // note: Successful geometry cannot be accepted when its resolved bindings failed to reach durable saved state.
                if (saveOwner != null && !ReferenceEquals(saveOwner.State, world))
                    saveFailure = "World save owner changed during generation.";
                if (saveOwner == null || !ReferenceEquals(saveOwner.State, world) || !saveOwner.TrySave(out saveFailure))
                {
                    _worldMaterializationFailed = true;
                    _lastMaterializationDecision = "rejected: world commit failed: " + saveFailure;
                    Debug.LogError("[YQGeneratedWorldRuntimeBuilder] WORLD COMMIT FAILED: " + saveFailure);
                }
            }

            if (!_worldMaterializationFailed)
            {
                EnsureProfileTerrainSnapshotProvider();
                PlayerStateManager playerManager = PlayerStateManager.Instance;
                PlayerState activePlayer = playerManager != null ? playerManager.state : null;
                bool profileOwnsWorld = profileSystem != null && activePlayer != null && world.worldIdentity != null &&
                    string.Equals(profileSystem.ActiveProfileId, activePlayer.playerId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(world.worldIdentity.ownerProfileId, profileSystem.ActiveProfileId, StringComparison.OrdinalIgnoreCase);
                if (profileOwnsWorld)
                {
                    string currentFingerprint = YQStateContract.Sha256Hex(BuildVisualSignature(plan));
                    string capturedTerrainJson = string.Empty;
                    yield return YQGeneratedWorldTerrain.CaptureProfileSnapshotRoutine(
                        _generatedTerrain,
                        profileSystem.ActiveProfileId,
                        world,
                        currentFingerprint,
                        json => capturedTerrainJson = json);
                    YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord capturedRecord = null;
                    try
                    {
                        capturedRecord = JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(capturedTerrainJson);
                    }
                    catch (ArgumentException)
                    {
                        // note: Invalid snapshot output rejects materialization before gameplay handoff.
                    }

                    if (capturedRecord == null || !capturedRecord.hasTerrain || string.IsNullOrWhiteSpace(capturedRecord.heightmapChecksum))
                    {
                        _worldMaterializationFailed = true;
                        _lastMaterializationDecision = "rejected: completed terrain could not be captured for its owning profile";
                        Debug.LogError("[YQGeneratedWorldRuntimeBuilder] Profile terrain snapshot capture failed; gameplay handoff remains gated.");
                    }
                    else
                    {
                        _profileTerrainSnapshotJson = capturedTerrainJson;
                        bool profileTerrainChanged = restoredProfileTerrainFromPriorRevision ||
                            !restoredProfileTerrain ||
                            !string.Equals(loadedProfileTerrainChecksum, capturedRecord.heightmapChecksum, StringComparison.OrdinalIgnoreCase);
                        if (profileTerrainChanged && !profileSystem.SaveActiveProfile())
                        {
                            _worldMaterializationFailed = true;
                            _lastMaterializationDecision = "rejected: profile terrain commit failed: " + profileSystem.LastFailure;
                            Debug.LogError("[YQGeneratedWorldRuntimeBuilder] Profile terrain was built but could not be committed: " + profileSystem.LastFailure);
                        }
                        else
                        {
                            _builtProfileId = profileSystem.ActiveProfileId;
                            _builtWorldId = world.worldIdentity.worldId;
                            Debug.Log("[YQGeneratedWorldRuntimeBuilder] PROFILE TERRAIN SAVE PASS profile=" + _builtProfileId +
                                      " world=" + _builtWorldId + " seed=" + plan.worldSeed +
                                      " checksum=" + capturedRecord.heightmapChecksum +
                                      " revision=" + profileSystem.ActiveRevision);
                        }
                    }
                }
            }

            // note: The generated hierarchy is the only runtime content subject to the distance and shadow budget.
            YQGeneratedWorldPerformanceDirector
                .ConfigureForGeneratedWorld(
                    _runtimeRoot.transform);
            bool canonicalContinuationReady =
                _generatedTerrain != null &&
                _generatedTerrain.terrainData != null &&
                canonicalWorldAtBuildStart &&
                !_spatialPlanChangedDuringBuild &&
                !_compiledBindingsChangedDuringBuild;
            // note: A persisted canonical plan with valid terrain must keep its continuation owner even when a downstream route/site gate rejects playability; otherwise the original 1024m terrain becomes an artificial world edge.
            if (!_worldMaterializationFailed || canonicalContinuationReady)
            {
                YQPlayerFollowingSemanticChunkStreamer.Attach(
                    _runtimeRoot.transform,
                    world,
                    plan,
                    _generatedTerrain);
                if (_worldMaterializationFailed)
                    Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Continuation streamer retained after downstream validation rejection; canonical terrain remains explorable while the rejected gate is repaired.");
            }
            // note: Install one shared underwater presentation cue after the generated hierarchy is accepted and before player control is released.
            YQUnderwaterVisual.Attach(_runtimeRoot.transform);

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
                "Settlement stream roots prepared: " +
                settlementsBuilt +
                "\nSettlement geometry instantiated: " +
                settlementsInstantiated + "/" + requiredMinimumSettlements +
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
            // note: Failed or cancelled staging owns its TerrainData; destroying a GameObject alone does not release that native allocation.
            if (stagedTerrain != null && stagedTerrain.terrainData != null) Destroy(stagedTerrain.terrainData);
            if (terrainStagingRoot != null) Destroy(terrainStagingRoot);
            if (!_cancellingBuild && IsCurrentWorldPlanReference(world, plan))
            {
                // note: Record early failures independently of a runtime root or previously successful build; preflight/terrain failure can occur before either exists.
                if (!reachedAcceptanceGate)
                    _worldMaterializationFailed = true;
                if (_worldMaterializationFailed)
                {
                    RecordFailedWorldBuild(world, plan);
                }
                else
                {
                    _failedBuildWorld = null;
                    _failedBuildPlan = null;
                    _failedBuildSeed = string.Empty;
                    _failedBuildSignature = string.Empty;
                }
            }
            _buildInProgress =
                false;

            _buildCoroutine =
                null;
        }
    }

    private IEnumerator PreflightSpatialCellsV2Routine(
        WorldState world,
        GeneratedWorldPlanRecord plan,
        Action<bool, string> completed)
    {
        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan, out YQPreparedSpatialMaterializationV2 prepared,
                out string failure))
        {
            completed?.Invoke(false, failure);
            yield break;
        }

        // note: One owner set proves that every settlement and hostile site has a distinct, fully functional reviewed composition inside its accepted reserve.
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // note: Alternative bindings become authoritative together only after every required site passes; a failed V2 probe leaves V1 bindings intact.
        var acceptedBindings = new List<Action>();
        _resolvedSemanticCompositionSeedsV2.Clear();
        for (int index = 0; index < prepared.SiteCount; index++)
        {
            // note: A profile/plan switch invalidates this cooperative preflight before it can bind another old-world site.
            if (!IsCurrentBuildContext(world, plan))
            {
                completed?.Invoke(false, "The active world changed during cell preflight.");
                yield break;
            }
            YQSpatialMaterializationSiteV2 anchor = prepared.GetSite(index);
            if (anchor.kind != YQSiteKindV2.Settlement &&
                anchor.kind != YQSiteKindV2.HostileSite)
                continue;

            YQRuntimeWorldSiteRecord record = null;
            string[] tags = null;
            string seed = string.Empty;
            string displayName = anchor.sourceSemanticId;
            bool bound = false;
            bool bindingChanged = false;
            bool allowAlternativeBinding = false;
            GeneratedSettlementRecord settlementOwner = null;
            GeneratedEncampmentRecord encampmentOwner = null;
            GeneratedRegionRecord siteRegion = FindRegion(plan, anchor.parentRegionId);
            GeneratedRegionAssetPaletteRecord sitePalette = FindPalette(plan, siteRegion);
            if (anchor.kind == YQSiteKindV2.Settlement)
            {
                foreach (GeneratedSettlementRecord settlement in plan.settlements)
                {
                    if (settlement == null || !string.Equals(settlement.settlementId,
                            anchor.sourceSemanticId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    GeneratedRegionRecord region = FindRegion(plan, settlement.regionId);
                    GeneratedRegionAssetPaletteRecord palette = FindPalette(plan, region);
                    settlementOwner = settlement;
                    // note: Keep a valid saved kit first; a kit that fails primary construction evidence may be repaired without changing narrative identity or the accepted spatial reserve.
                    allowAlternativeBinding = true;
                    siteRegion = region;
                    sitePalette = palette;
                    bound = region != null && palette != null &&
                        YQCompiledWorldSiteBindingService.TryResolveSettlementSite(
                            plan, settlement, region, palette, out record, out bindingChanged, persistBinding: false);
                    tags = YQCompiledWorldSiteBindingService.BuildSettlementSemanticSliceTags(settlement);
                    seed = SettlementSeed(settlement);
                    displayName = settlement.displayName;
                    break;
                }
            }
            else
            {
                foreach (GeneratedEncampmentRecord encampment in plan.encampments)
                {
                    if (encampment == null || !string.Equals(encampment.encampmentId,
                            anchor.sourceSemanticId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    GeneratedRegionRecord region = FindRegion(plan, encampment.regionId);
                    GeneratedRegionAssetPaletteRecord palette = FindPalette(plan, region);
                    encampmentOwner = encampment;
                    // note: Earlier failed builds could persist unvalidated kit IDs; a rejected physical binding is not an accepted playable assembly.
                    allowAlternativeBinding = true;
                    siteRegion = region;
                    sitePalette = palette;
                    bound = region != null && palette != null &&
                        YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(
                            plan, encampment, region, palette, out record, out bindingChanged, persistBinding: false);
                    tags = YQCompiledWorldSiteBindingService.BuildEncampmentSemanticSliceTags(encampment);
                    seed = !string.IsNullOrWhiteSpace(encampment.deterministicSeed)
                        ? encampment.deterministicSeed : encampment.encampmentId;
                    displayName = encampment.displayName;
                    break;
                }
            }

            if (!bound && anchor.kind == YQSiteKindV2.Settlement)
            {
                // note: Mirror streamed materialization for both synthetic and stale narrative bindings; preserve narrative identity while using the accepted reviewed catalog contract.
                record = YQContinuousWorldFeatureMaterializer.FindReviewedSiteForFunctions(
                    YQAuthoredSiteKind.Settlement,
                    new[]
                    {
                        YQAssetFunctionV2.Habitation,
                        YQAssetFunctionV2.Circulation,
                        YQAssetFunctionV2.Service,
                        YQAssetFunctionV2.Commerce
                    },
                    anchor.siteId, sitePalette?.styleKey, siteRegion?.assetStyleRationale);
                if (settlementOwner == null)
                {
                    tags = new[] { "poi", "civic", "residential", "service", "circulation" };
                    seed = anchor.sourceSemanticId;
                }
                bound = record != null;
                if (bound)
                    Debug.Log("[YQGeneratedWorldRuntimeBuilder] V2 PREFLIGHT REVIEWED FALLBACK " + anchor.siteId + " kit=" + record.kitId + " kind=settlement");
            }
            else if (!bound && anchor.kind == YQSiteKindV2.HostileSite)
            {
                // note: Mirror streamed materialization for both synthetic and stale narrative bindings; preflight the reviewed camp composition that the materializer will use.
                record = YQContinuousWorldFeatureMaterializer.FindReviewedSiteForFunctions(
                    YQAuthoredSiteKind.Camp,
                    new[]
                    {
                        YQAssetFunctionV2.Encounter,
                        YQAssetFunctionV2.Reward,
                        YQAssetFunctionV2.Security
                    },
                    anchor.siteId, sitePalette?.styleKey, siteRegion?.assetStyleRationale);
                if (encampmentOwner == null)
                {
                    tags = new[] { "poi", "perimeter", "circulation", "encounter", "reward" };
                    seed = anchor.sourceSemanticId;
                }
                bound = record != null;
                if (bound)
                    Debug.Log("[YQGeneratedWorldRuntimeBuilder] V2 PREFLIGHT REVIEWED FALLBACK " + anchor.siteId + " kit=" + record.kitId + " kind=hostile");
            }

            // note: Preflight may resolve a previously missing persisted kit; retain its change flag through the final successful-build save gate.
            _compiledBindingsChangedDuringBuild |= bindingChanged;
            if (!bound || !TryResolveConstructionRadii(record, out _, out _, out _))
            {
                completed?.Invoke(false, "No eligible reviewed cell binding for " + displayName);
                yield break;
            }

            bool resolved = false;
            string compositionFailure = string.Empty;
            YQRuntimeWorldSiteRecord initialRecord = record;
            var rejectedKits = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // note: Try another primary-library kit only after the saved choice fails; every replacement must satisfy the same function and footprint gates.
            while (record != null && rejectedKits.Add(record.kitId))
            {
                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Preparing approved assets", 2, 9,
                    "Checking " + displayName + ": " + record.kitId, 0.595f);
                yield return ResolveUniqueSemanticCompositionV2Routine(
                    record, tags, seed, anchor.sourceSemanticId, displayName,
                    owners, prepared,
                    (success, radius, message) =>
                    {
                        resolved = success;
                        compositionFailure = message;
                    });
                TouchInitialGenerationWatchdog();
                // note: Do not commit or inspect another old-world binding after a profile switch during asynchronous asset loading.
                if (!IsCurrentBuildContext(world, plan))
                {
                    completed?.Invoke(false, "The active world changed during cell selection.");
                    yield break;
                }
                if (resolved || !allowAlternativeBinding)
                    break;

                // note: Candidate probes preserve style/kind eligibility and do not write to the save; normal composition still checks functions, uniqueness and the reserved footprint.
                bool hasAlternative = settlementOwner != null
                    ? YQCompiledWorldSiteBindingService.TryResolveSettlementSite(
                        plan, settlementOwner, siteRegion, sitePalette, out record, out _, rejectedKits, false)
                    : YQCompiledWorldSiteBindingService.TryResolveEncampmentSite(
                        plan, encampmentOwner, siteRegion, sitePalette, out record, out _, rejectedKits, false);
                if (!hasAlternative)
                    break;
                yield return null;
            }
            if (!resolved)
            {
                completed?.Invoke(false, displayName + ": " + compositionFailure +
                    " Checked " + rejectedKits.Count + " eligible kit(s)." +
                    (allowAlternativeBinding ? string.Empty : " Existing binding preserved."));
                yield break;
            }
            if (allowAlternativeBinding || !ReferenceEquals(record, initialRecord))
            {
                YQRuntimeWorldSiteRecord acceptedRecord = record;
                acceptedBindings.Add(() =>
                {
                    // note: Unvalidated probes never become saved bindings; commit the initial candidate too, but only after all required sites pass.
                    _compiledBindingsChangedDuringBuild = true;
                    // note: Commit only the validated kit metadata; semantic identity, quests, seed and accepted spatial reservation stay unchanged.
                    if (settlementOwner != null)
                    {
                        settlementOwner.runtimeSiteKitId = acceptedRecord.kitId;
                        settlementOwner.runtimeSiteSemanticStyle = acceptedRecord.semanticStyleKey;
                        settlementOwner.runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion;
                    }
                    else
                    {
                        encampmentOwner.runtimeSiteKitId = acceptedRecord.kitId;
                        encampmentOwner.runtimeSiteSemanticStyle = acceptedRecord.semanticStyleKey;
                        encampmentOwner.runtimeSiteBindingVersion = YQCompiledWorldSiteBindingService.BindingVersion;
                    }
                    Debug.Log("[YQGeneratedWorldRuntimeBuilder] VALIDATED CELL KIT ALTERNATIVE: " +
                        displayName + ": " + initialRecord.kitId + " -> " + acceptedRecord.kitId);
                });
            }
            yield return null;
        }
        // note: The complete V2 preflight succeeded, so later terrain grading and streaming may now consume the validated replacement bindings.
        foreach (Action accept in acceptedBindings)
            accept();
        completed?.Invoke(true, string.Empty);
    }

    private IEnumerator PrepareSpatialAuthorityRoutine(
        WorldState world,
        GeneratedWorldPlanRecord plan,
        Action<bool> completed)
    {
        if (!IsCurrentWorldPlanReference(world, plan))
        {
            completed?.Invoke(false);
            yield break;
        }

        YQSpatialPlanningMode mode =
            YQWorldGenerationArchitecture.ActiveSpatialPlanningMode;
        if (mode == YQSpatialPlanningMode.PersistedV1 ||
            mode == YQSpatialPlanningMode.V2Shadow)
        {
            if (!YQSpatialPlanVersionRouter.TryResolve(
                    plan,
                    mode,
                    out YQSpatialPlanAuthority v1Authority,
                    out string v1Failure))
            {
                Debug.LogError(
                    "[YQGeneratedWorldRuntimeBuilder] V1 spatial preparation failed: " +
                    v1Failure);
                completed?.Invoke(false);
                yield break;
            }

            YQWorldGenerationArchitecture.LockRuntimeAuthority(
                plan,
                v1Authority);
            completed?.Invoke(true);
            yield break;
        }

        GeneratedSpatialWorldPlanV2Record previousArtifact =
            plan.spatialPlanV2;
        bool acceptedV2 =
            YQSpatialPlanVersionRouter.TryValidateAcceptedV2(
                plan,
                out string v2Failure);
        if (acceptedV2)
        {
            // note: Separate validation and immutable projection across frames so Continue never receives both costs in one loading-screen frame.
            yield return null;
            TouchInitialGenerationWatchdog();

            string preparationFailure = string.Empty;
            if (IsCurrentWorldPlanReference(world, plan) &&
                ReferenceEquals(plan.spatialPlanV2, previousArtifact) &&
                YQSpatialMaterializationResolverV2.TryGetPrepared(
                    plan,
                    out _,
                    out preparationFailure))
            {
                YQWorldGenerationArchitecture.LockRuntimeAuthority(
                    plan,
                    YQSpatialPlanAuthority.AcceptedV2);
                completed?.Invoke(true);
                yield break;
            }

            v2Failure = string.IsNullOrWhiteSpace(preparationFailure)
                ? "The accepted V2 artifact changed during preparation."
                : preparationFailure;
        }

        bool mayCompileReplacement =
            previousArtifact == null ||
            previousArtifact.acceptanceState !=
                GeneratedSpatialPlanAcceptanceState.Accepted ||
            YQSpatialPlanVersionRouter
                .IsKnownLegacyNonAuthoritativeArtifact(previousArtifact);

        if (!acceptedV2 && mayCompileReplacement)
        {
            GeneratedSpatialWorldPlanV2Record compiled = null;
            string compileFailure = string.Empty;
            IEnumerator compiler =
                YQSpatialBlueprintCompilerV2.CompileRoutine(
                    plan,
                    (candidate, message) =>
                    {
                        compiled = candidate;
                        compileFailure = message ?? string.Empty;
                    });

            while (true)
            {
                bool hasNext;
                try
                {
                    hasNext = compiler.MoveNext();
                }
                catch (Exception exception)
                {
                    compileFailure =
                        "V2 spatial planning threw an exception: " +
                        exception.Message;
                    break;
                }

                if (!hasNext)
                    break;

                yield return compiler.Current;
                TouchInitialGenerationWatchdog();
                if (!IsCurrentWorldPlanReference(world, plan))
                {
                    completed?.Invoke(false);
                    yield break;
                }
            }

            if (IsCurrentWorldPlanReference(world, plan) &&
                ReferenceEquals(plan.spatialPlanV2, previousArtifact) &&
                compiled != null &&
                string.IsNullOrWhiteSpace(compileFailure))
            {
                // note: Yield while the old artifact is still attached; cancelled builds and autosaves must not observe a candidate before projection succeeds.
                yield return null;
                TouchInitialGenerationWatchdog();

                bool replacementReady =
                    IsCurrentWorldPlanReference(world, plan) &&
                    ReferenceEquals(plan.spatialPlanV2, previousArtifact) &&
                    YQSpatialMaterializationCompilerV2.TryPrepareCandidate(
                        plan,
                        compiled,
                        out _,
                        out compileFailure);
                if (replacementReady)
                {
                    // note: Commit only after the complete detached projection passes; there is no yield between assignment and authority locking.
                    plan.spatialPlanV2 = compiled;
                    _spatialPlanChangedDuringBuild = true;
                    // note: The replacement spatial artifact keeps the same world-space coordinate system, so the active player remains where they are.
                    YQWorldGenerationArchitecture.LockRuntimeAuthority(
                        plan,
                        YQSpatialPlanAuthority.AcceptedV2);
                    Debug.Log(
                        "[YQGeneratedWorldRuntimeBuilder] PLAYABLE V2 SPATIAL PLAN PREPARED\n" +
                        "Generation: " + compiled.generationVersion + "\n" +
                        "Hash: " + compiled.contentHash);
                    completed?.Invoke(true);
                    yield break;
                }

            }

            if (!string.IsNullOrWhiteSpace(compileFailure))
                v2Failure = compileFailure;
        }

        string fallbackFailure = string.Empty;
        // note: Both planning and cell-preflight rejection use the same fallback boundary; neither treats V1 data validity as physical startup readiness.
        if (CanAttemptLegacySpatialFallback(plan) &&
            YQSpatialPlanVersionRouter.TryResolve(
                plan,
                YQSpatialPlanningMode.PersistedV1,
                out YQSpatialPlanAuthority fallbackAuthority,
                out fallbackFailure))
        {
            // note: Legacy compatibility construction still has to pass every required settlement, collision, material and reveal gate below.
            YQWorldGenerationArchitecture.LockRuntimeAuthority(
                plan,
                fallbackAuthority);
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] V2 spatial preparation selected legacy V1 compatibility data; physical startup validation is still required: " +
                (string.IsNullOrWhiteSpace(v2Failure)
                    ? "no accepted V2 candidate was available."
                    : v2Failure));
            completed?.Invoke(true);
            yield break;
        }

        Debug.LogError(
            "[YQGeneratedWorldRuntimeBuilder] No playable spatial authority is available. V2: " +
            (string.IsNullOrWhiteSpace(v2Failure)
                ? "unavailable"
                : v2Failure) +
            "; fallback: " +
            (string.IsNullOrWhiteSpace(fallbackFailure)
                ? "disabled"
                : fallbackFailure));
        completed?.Invoke(false);
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
        TouchInitialGenerationWatchdog();

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

    private static void TouchInitialGenerationWatchdog()
    {
        if (!IsInitialGenerationGameplayLocked)
            return;

        // note: Every accepted phase boundary proves the transaction is alive; only a phase with no progress for the full stall window may trip recovery.
        _initialGenerationLastProgressAt = Time.unscaledTime;
    }

    internal static void ReportInitialGenerationProgress()
    {
        // note: Valid transport handoffs are progress too; origin-generation time must not consume the following world request's independent stall allowance.
        TouchInitialGenerationWatchdog();
    }

    private void ReportInitialGenerationDeadlineExceeded()
    {
        if (!IsInitialGenerationGameplayLocked ||
            _initialGenerationDeadlineWarningIssued)
            return;

        _initialGenerationDeadlineWarningIssued = true;

        // note: A true no-progress stall is a circuit breaker: stop owned coroutines and prevent the coordinator from immediately relaunching the same wedged transaction.
        _initialGenerationWatchdogAborted = true;
        // note: Publish the watchdog rejection before cancellation disposes the owned coroutine, so the editor heartbeat cannot retain the stale scheduled-start decision.
        _lastMaterializationDecision =
            "rejected: initial-generation watchdog timeout after " +
            MaximumInitialGenerationStallSeconds.ToString("0") + " seconds";
        CancelActiveBuildRoutine();
        _worldMaterializationFailed = true;

        YQStartupLoadingScreen.ShowGenerationFailure(
            "I lost my hold on that part. I stopped before it could trap you in an unfinished world. Give me another try; I still have everything you told me.",
            RetryAfterGenerationWatchdog,
            ReturnToTitleAfterGenerationWatchdog);

        Debug.LogError(
            "[YQGeneratedWorldRuntimeBuilder] INITIAL GENERATION SAFETY DEADLINE REACHED. " +
            "No materialization phase completed for " +
            MaximumInitialGenerationStallSeconds.ToString("0") +
            " seconds. The active coroutines were stopped and the loading screen entered a responsive recovery state. " +
            "Gameplay was not released against incomplete state.");
    }

    private void RetryAfterGenerationWatchdog()
    {
        // note: A player-requested retry receives a fresh deadline and rebuilds only from the accepted persisted world plan.
        _initialGenerationWatchdogAborted = false;
        _initialGenerationDeadlineWarningIssued = false;
        _initialGenerationLockStartedAt = Time.unscaledTime;
        _initialGenerationLastProgressAt = Time.unscaledTime;
        _initialGenerationLastWatchdogUpdateAt = Time.unscaledTime;
        YQStartupLoadingScreen.SetGenerationStage(
            "All right. I still know who you are. Let me put the ground back beneath this carefully...",
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
        return IsCurrentWorldPlanReference(
                   expectedWorld,
                   expectedPlan) &&
               YQWorldGenerationArchitecture
                   .IsRuntimeAuthorityCurrent(expectedPlan);
    }

    private static bool IsCurrentWorldPlanReference(
        WorldState expectedWorld,
        GeneratedWorldPlanRecord expectedPlan)
    {
        if (expectedWorld == null || expectedPlan == null)
            return false;

        WorldStateManager manager = WorldStateManager.Instance;
        if (manager == null || manager.State != expectedWorld)
            return false;

        expectedWorld.EnsureCollections();
        // note: Spatial preparation uses only semantic-plan identity; the stronger build-context check adds the frozen V2 artifact after authority selection.
        return ReferenceEquals(
            expectedWorld.generatedWorldPlan,
            expectedPlan);
    }

    // ------------------------------------------------------------
    // INITIAL GENERATION REVEAL
    // ------------------------------------------------------------

    private void TryCompleteInitialGenerationReveal()
    {
        // note: The gameplay transaction outlives the bounded LLM lease; only its own lock and recovery state authorize a reveal.
        if (_buildInProgress || !IsInitialGenerationGameplayLocked ||
            _initialGenerationWatchdogAborted)
            return;

        LLMClient llm =
            LLMClient.Instance;

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
            _builtPlan != plan ||
            // note: The no-loading-screen path must enforce the same frozen spatial authority as the delayed handoff.
            !IsCurrentBuildContext(world, plan))
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
            _builtSettlementCount < ExpectedStartupSettlementCount(plan))
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

        YQProfileSaveSystem profileSystem = YQProfileSaveSystem.Instance;
        if (profileSystem != null && !profileSystem.SaveActiveProfile())
        {
            // note: Shared active state is already durable; report a failed profile copy without converting an I/O problem into a permanent gameplay lock.
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] INITIAL GENERATION PROFILE SNAPSHOT FAILED. " +
                "The runtime world is complete, but the profile copy must succeed on the next manual or automatic save before Continue is reliable.");
        }

        // note: Mark this seed revealed only after the profile-copy attempt so this one-shot completion path cannot save or announce the same world repeatedly.
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
    "\nSettlement stream roots prepared: " +
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
            (llm != null && llm.IsExclusiveSequenceActive) +
            "\nExclusiveOwner=" +
            (llm != null ? llm.ExclusiveSequenceOwner : string.Empty));

        GameObject player = null;

        try
        {
            player =
                GameObject.FindGameObjectWithTag(
                    "Player");
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[WORLDGEN ERROR] Authoritative player lookup failed during spawn placement. " +
                "Seed=" + (plan != null ? plan.worldSeed : "<missing>") +
                ", reason=" + exception.Message);
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
            // note: Keep gameplay locked through the black camera handoff; unlock only when the generated-origin camera owns presentation.
            GameObject revealRoot = _runtimeRoot;
            _revealPresentation = loading;
            _revealCoroutine = StartCoroutine(
                loading.FinishGenerationAndHide(
                    revealHoldSeconds,
                    () =>
                    {
                        ReleaseInitialGenerationGameplayLock();
                        EndInitialGenerationLlmSequence();
                    },
                    // note: Revalidate the gameplay transaction after every camera wait; an expired transport lease cannot invalidate a completed world.
                    () => IsInitialGenerationGameplayLocked &&
                        !_initialGenerationWatchdogAborted &&
                        !_buildInProgress && !_worldMaterializationFailed &&
                        IsCurrentBuildContext(world, plan) && _builtPlan == plan &&
                        revealRoot != null && _runtimeRoot == revealRoot,
                    ReportInitialGenerationHandoffFailure));
        }
        else
        {
            // note: Headless and recovery paths have no cinematic owner, so release the same guarded state immediately.
            ReleaseInitialGenerationGameplayLock();
            EndInitialGenerationLlmSequence();
        }
    }

    private static void EndInitialGenerationLlmSequence()
    {
        // note: Release only a surviving initial-generation lease; expiry or a different scheduler owner must not affect gameplay completion.
        LLMClient llm = LLMClient.Instance;
        if (llm != null && llm.IsExclusiveSequenceActive &&
            string.Equals(llm.ExclusiveSequenceOwner, InitialGenerationOwner, StringComparison.Ordinal))
        {
            llm.EndExclusiveSequence(InitialGenerationOwner);
        }
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
        float endProgress,
        bool forcePaletteFallback = false)
    {
        if (_runtimeRoot == null)
            yield break;

        if (YQWorldGenerationArchitecture.UsesCompiledWorld &&
            !forcePaletteFallback)
        {
            yield return
                BuildCompiledSettlementRoutine(
                    plan,
                    settlement,
                    region,
                    palette,
                    registry);
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

        // note: Rotate the complete civic plan toward its persisted entrance; individual façades and authored streets now agree with the world travel graph.
        settlementRoot.transform.rotation =
            Quaternion.Euler(
                0f,
                ResolveSettlementHeading(plan, settlement),
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
        // note: Reject the local candidate before dressing or marking it materialized when its architecture fails validation.
        if (!BuildBuildingLots(
            settlementRoot.transform,
            plan,
            settlement,
            palette,
            registry))
        {
            _lastSettlementMaterialized = false;
            settlementRoot.SetActive(false);
            Destroy(settlementRoot);
            yield break;
        }

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
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (!YQCompiledWorldSiteBindingService.TryResolveSettlementSite(
                plan,
                settlement,
                region,
                palette,
                out YQRuntimeWorldSiteRecord siteRecord,
                out bool bindingChanged))
        {
            // note: Surface the rejected settlement identity through the heartbeat so a failed compiled binding can be diagnosed without unlocking editor-log access.
            _lastMaterializationDecision =
                "rejected: compiled settlement binding unavailable; settlement=" +
                settlement.settlementId;
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
            ResolveSettlementHeading(plan, settlement),
            0f);
        BuildRegionVolume(settlementRoot.transform, region, settlement);
        BuildSettlementLabel(
            settlementRoot.transform,
            settlement,
            region,
            palette);
        bool materialized = false;
        string[] semanticSliceTags =
            YQCompiledWorldSiteBindingService.BuildSettlementSemanticSliceTags(
                settlement);
        // note: Compiled settlements are assembled from a seeded semantic district slice; the reviewed pack is an approved source library, not a single golden scene to clone wholesale.
        yield return
            YQCompiledWorldSiteInstance.MaterializeSemanticSliceRoutine(
                settlementRoot.transform,
                settlement.settlementId,
                siteRecord,
                semanticSliceTags,
                ResolveSemanticCompositionSeedV2(
                    plan,
                    settlement.settlementId,
                    SettlementSeed(settlement)),
                success => materialized = success);

        if (!materialized)
        {
            // note: Preserve the reviewed-site identity when its assembled slice fails admission, distinguishing a load failure from a missing binding.
            _lastMaterializationDecision =
                "rejected: compiled settlement load failed; settlement=" +
                settlement.settlementId + ", site=" + siteRecord.kitId;
            settlementRoot.SetActive(false);
            Destroy(settlementRoot);
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] COMPILED SETTLEMENT LOAD FAILED\n" +
                "Settlement: " + settlement.displayName + "\n" +
                "Reviewed site: " + siteRecord.kitId);
            yield break;
        }

        // note: Complete semantic buildings remain the authoritative settlement core; this pass adds only deterministic, terrain-grounded countryside cues outside that reviewed footprint.
        BuildCompiledSettlementOutskirts(
            settlementRoot.transform,
            settlement,
            siteRecord,
            palette,
            registry);

        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] COMPILED SETTLEMENT PREPARED\n" +
            "Settlement: " + settlement.displayName + " (" +
            settlement.settlementId + ")\n" +
            "Reviewed site: " + siteRecord.kitId + "\n" +
            "Semantic style: " + siteRecord.semanticStyleKey + "\n" +
            "Topology: " + siteRecord.topology + "\n" +
            "Semantic slice: " + string.Join(", ", semanticSliceTags) + "\n" +
            "Anchor: " + center);

        _lastSettlementMaterialized = true;
    }

    private void BuildCompiledSettlementOutskirts(
        Transform parent,
        GeneratedSettlementRecord settlement,
        YQRuntimeWorldSiteRecord siteRecord,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry)
    {
        if (parent == null || settlement == null || siteRecord == null ||
            palette == null || registry == null)
            return;

        string meaning =
            (settlement.kind ?? string.Empty) + " " +
            (settlement.siteRoleIntent ?? string.Empty) + " " +
            (settlement.marketBias ?? string.Empty) + " " +
            string.Join(" ", settlement.serviceSlots ?? new List<string>()) + " " +
            string.Join(" ", settlement.cellRoleIntents ?? new List<string>());
        string seed = SettlementSeed(settlement) + ":compiled_outskirts";
        bool rural = ContainsAny(meaning, "village", "hamlet", "rural", "farm", "fishing", "fisher", "mill", "pasture", "orchard");
        bool waterside = ContainsAny(meaning, "fishing", "fisher", "river", "lake", "dock", "mill");

        float radius = Mathf.Clamp(
            Mathf.Max(siteRecord.authoredFootprintRadius + 8f, 18f),
            18f,
            96f);
        Transform outskirts = new GameObject("SettlementOutskirts__LandUse").transform;
        outskirts.SetParent(parent, false);

        GeneratedAssetReferenceRecord boundary = FindSemanticPaletteAsset(
            palette.exteriorDeco,
            seed + ":boundary",
            "fence", "gate", "hedge", "post", "palisade", "wall");
        GeneratedAssetReferenceRecord fieldMarker = FindSemanticPaletteAsset(
            palette.floorDeco,
            seed + ":field",
            "hay", "trough", "cart", "wagon", "barrel", "crate", "log", "basket");
        GeneratedAssetReferenceRecord orchardTree = FindSemanticPaletteAsset(
            palette.vegetation,
            seed + ":orchard",
            "tree", "orchard", "bush", "shrub");
        GeneratedAssetReferenceRecord watersideMarker = FindSemanticPaletteAsset(
            palette.exteriorDeco,
            seed + ":waterside",
            "dock", "pier", "net", "boat", "post", "fence");

        int boundaryCount = rural ? 5 : 2;
        for (int index = 0; index < boundaryCount; index++)
        {
            if (boundary == null)
                break;
            float t = index / (float)Mathf.Max(1, boundaryCount - 1);
            float angle = Mathf.Lerp(-0.85f, 0.85f, t) +
                Mathf.Lerp(-0.12f, 0.12f, Deterministic01(seed + ":boundary_angle:" + index));
            float distance = radius + 5f +
                Deterministic01(seed + ":boundary_radius:" + index) * 5f;
            Vector3 local = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            SpawnRegisteredAsset(
                outskirts,
                "OutskirtsBoundary__" + index,
                boundary,
                local,
                Quaternion.Euler(0f, angle * Mathf.Rad2Deg + 90f, 0f),
                registry,
                false);
        }

        if (rural && fieldMarker != null)
        {
            for (int index = 0; index < 3; index++)
            {
                float angle = 1.35f + index * 0.34f +
                    Deterministic01(seed + ":field_angle:" + index) * 0.16f;
                float distance = radius + 11f +
                    Deterministic01(seed + ":field_radius:" + index) * 9f;
                SpawnRegisteredAsset(
                    outskirts,
                    "LandUseFieldMarker__" + index,
                    fieldMarker,
                    new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance),
                    Quaternion.Euler(0f, DeterministicQuarterTurn(seed + ":field_yaw:" + index), 0f),
                    registry,
                    false);
            }
        }
        else if (!rural && fieldMarker != null)
        {
            // note: Civic and cave towns still receive a sparse countryside threshold, while farm-specific density stays reserved for rural semantics.
            for (int index = 0; index < 2; index++)
            {
                float angle = 2.15f + index * 0.32f +
                    Deterministic01(seed + ":transition_angle:" + index) * 0.14f;
                float distance = radius + 8f +
                    Deterministic01(seed + ":transition_radius:" + index) * 5f;
                SpawnRegisteredAsset(
                    outskirts,
                    "LandUseTransitionMarker__" + index,
                    fieldMarker,
                    new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance),
                    Quaternion.Euler(0f, DeterministicQuarterTurn(seed + ":transition_yaw:" + index), 0f),
                    registry,
                    false);
            }
        }

        if (rural && orchardTree != null)
        {
            for (int index = 0; index < 4; index++)
            {
                float angle = -1.15f + index * 0.38f +
                    Deterministic01(seed + ":orchard_angle:" + index) * 0.14f;
                float distance = radius + 12f +
                    Deterministic01(seed + ":orchard_radius:" + index) * 7f;
                SpawnRegisteredAsset(
                    outskirts,
                    "LandUseOrchardTree__" + index,
                    orchardTree,
                    new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance),
                    Quaternion.Euler(0f, Deterministic01(seed + ":orchard_yaw:" + index) * 360f, 0f),
                    registry,
                    false);
            }
        }

        if (waterside && watersideMarker != null)
        {
            // note: Waterside settlements receive a small service edge marker while leaving the accepted road corridor clear.
            SpawnRegisteredAsset(
                outskirts,
                "LandUseWatersideMarker",
                watersideMarker,
                new Vector3(radius + 12f, 0f, 4f),
                Quaternion.Euler(0f, DeterministicQuarterTurn(seed + ":waterside_yaw"), 0f),
                registry,
                false);
        }
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

            if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
                !IsEncampmentInsideGeneratedTerrain(plan, encampment, _generatedTerrain))
            {
                // note: Distant accepted V2 hostile owners are streamed with their cells instead of being loaded into the finite origin scene.
                Debug.Log(
                    "[YQGeneratedWorldRuntimeBuilder] V2 HOSTILE SITE DEFERRED TO STREAMING: " +
                    encampment.displayName);
                continue;
            }

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
                ResolveSpatialSiteHeading(
                    plan,
                    encampment.encampmentId,
                    DeterministicQuarterTurn(
                        encampment.deterministicSeed +
                        ":compiled_hostile_orientation")),
                0f);
            bool materialized = false;
            string[] semanticSliceTags =
                YQCompiledWorldSiteBindingService
                    .BuildEncampmentSemanticSliceTags(encampment);
            // note: A hostile town, camp, or lair consumes a seeded semantic approach slice; the reviewed source map is never materialized wholesale as an encounter.
            yield return
                YQCompiledWorldSiteInstance.MaterializeSemanticSliceRoutine(
                    siteRoot.transform,
                    encampment.encampmentId,
                    siteRecord,
                    semanticSliceTags,
                    ResolveSemanticCompositionSeedV2(
                        plan,
                        encampment.encampmentId,
                        !string.IsNullOrWhiteSpace(
                            encampment.deterministicSeed)
                            ? encampment.deterministicSeed
                            : encampment.encampmentId),
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
                "Semantic slice: " + string.Join(", ", semanticSliceTags) + "\n" +
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

        // note: Context-menu and focused verification rebuilds must not be mistaken for the stale scaffold guard used by automatic startup.
        _forceNextBuild = true;

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
        CancelInitialGenerationReveal();
        // note: Explicit rebuild/profile teardown is cancellation, not a failed generation attempt; suppress terminal recovery while StopCoroutine disposes owned work.
        _cancellingBuild = true;
        try
        {
            if (_buildCoroutine != null)
                StopCoroutine(_buildCoroutine);
            // note: Explicit disposal unwinds all nested iterators even when Unity only stops scheduling the outer coroutine.
            (_buildExecution as IDisposable)?.Dispose();
        }
        finally
        {
            _buildCoroutine = null;
            _buildExecution = null;
            _buildInProgress = false;
            _cancellingBuild = false;
        }

        CancelPopulationBuildRoutine();
    }

    private void CancelInitialGenerationReveal()
    {
        // note: Stop the owner's coroutine before clearing its visual state; an old completion callback cannot unlock a replacement world.
        if (_revealCoroutine != null)
            StopCoroutine(_revealCoroutine);
        _revealCoroutine = null;
        if (_revealPresentation != null)
            _revealPresentation.CancelGenerationHandoff();
        _revealPresentation = null;
        _revealedInitialGenerationSeed = string.Empty;
    }

    private void ReportInitialGenerationHandoffFailure()
    {
        // note: A presentation failure is not a failed world build; keep accepted geometry and prevent automatic reveal retry loops.
        _initialGenerationDeadlineWarningIssued = true;
        _initialGenerationWatchdogAborted = true;
        YQStartupLoadingScreen.ShowGenerationFailure(
            "The world is ready, but the loading-stage camera has not released control. Retry the handoff, or return to the title screen.",
            RetryInitialGenerationHandoff,
            ReturnToTitleAfterGenerationWatchdog,
            "Retry handoff");
    }

    private void RetryInitialGenerationHandoff()
    {
        // note: Retry presentation only. The accepted world and generated content must not be rebuilt for a camera timeout.
        CancelInitialGenerationReveal();
        YQStartupLoadingScreen.ClearGenerationFailure();
        _initialGenerationWatchdogAborted = false;
        _initialGenerationDeadlineWarningIssued = false;
        _initialGenerationLastProgressAt = Time.unscaledTime;
        _initialGenerationLastWatchdogUpdateAt = Time.unscaledTime;
        TryCompleteInitialGenerationReveal();
    }

    private void CancelPopulationBuildRoutine()
    {
        _cancellingPopulation = true;
        try
        {
            if (_populationBuildCoroutine != null)
                StopCoroutine(_populationBuildCoroutine);
            // note: Release child iterator finally blocks even if Unity only stops scheduling the wrapper.
            (_populationExecution as IDisposable)?.Dispose();
        }
        finally
        {
            // note: Cancellation clears ownership immediately so a changed save or explicit retry can start a fresh transaction.
            _populationBuildCoroutine = null;
            _populationExecution = null;
            _populationBuildInProgress = false;
            _cancellingPopulation = false;
        }
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

        if (YQWorldGenerationArchitecture.TryResolveRuntimeAuthority(
                plan,
                out YQSpatialPlanAuthority authority,
                out _))
        {
            // note: A spatial cutover changes build identity even when the enclosing semantic plan object stays the same.
            signature
                .Append("spatial:")
                .Append((int)authority)
                .Append(':');
            if (authority == YQSpatialPlanAuthority.AcceptedV2)
                signature.Append(plan.spatialPlanV2?.contentHash);
            else
                signature.Append(plan.spatialPlan?.semanticFingerprint);
            signature.Append('|');
        }

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
        YQAssetPlacementContextV2 placementContext = CreateSettlementPerimeterContext();
        GeneratedAssetReferenceRecord reference =
            PickSettlementPerimeterReference(
                palette,
                SettlementSeed(settlement) + ":perimeter_family",
                placementContext);

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
                true,
                placementContext: placementContext);
        }
    }

    // note: Use registered perimeter-like props where a palette owns them; a modular wall is the reliable architectural fallback.
    private static GeneratedAssetReferenceRecord PickSettlementPerimeterReference(
        GeneratedRegionAssetPaletteRecord palette,
        string seed,
        YQAssetPlacementContextV2 placementContext)
    {
        if (palette != null && palette.exteriorDeco != null)
        {
            List<GeneratedAssetReferenceRecord> candidates =
                new List<GeneratedAssetReferenceRecord>();

            for (int i = 0; i < palette.exteriorDeco.Count; i++)
            {
                GeneratedAssetReferenceRecord candidate =
                    palette.exteriorDeco[i];

                if (MatchesStructuralPerimeter(candidate) &&
                    YQWorldAssetCatalog.IsAllowedInPlacementContext(candidate, placementContext))
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
            seed + ":wall_fallback",
            placementContext);
    }

    private static YQAssetPlacementContextV2 CreateSettlementPerimeterContext()
    {
        // note: Defensive segments stand outdoors on ground and retain collision; they are not walkable navigation surfaces.
        return new YQAssetPlacementContextV2
        {
            requiredRole = YQAssetRoleV2.StructuralModule,
            requiredFunction = YQAssetFunctionV2.Security,
            requiredEnvironment = YQAssetEnvironmentV2.Exterior,
            requiredSupportMode = YQAssetSupportModeV2.Ground,
            requireTerrainSupport = true,
            requireCollider = true,
            requireNavigation = false
        };
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

    private bool BuildBuildingLots(
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
                "Checking for a complete supported modular recipe.");
        }

        int spawned =
            0;

        HashSet<string> usedWholeBuildingPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
        // note: A rejected prefab is excluded for the rest of this settlement, preventing repeated failed clones at every lot.
        var rejectedWholeBuildingPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
            if (reference != null && rejectedWholeBuildingPaths.Contains(reference.assetPath))
                reference = FindAlternativeWholeBuilding(palette.settlementBuilding,
                    rejectedWholeBuildingPaths, usedWholeBuildingPaths, seed);

            if (reference == null)
            {
                // note: Expected content exhaustion rejects this candidate through its owner's cleanup path, before creating a partial modular shell.
                if (!HasSupportedFallbackRecipe(settlement, palette, seed, lot.purpose))
                    return false;
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

            GameObject instance = null;
            // note: One unsuitable catalog entry cannot force a supported palette into fragment assembly while other whole buildings remain available.
            while (reference != null)
            {
                instance = SpawnRegisteredAsset(
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
                if (instance != null) break;
                rejectedWholeBuildingPaths.Add(reference.assetPath);
                reference = FindAlternativeWholeBuilding(palette.settlementBuilding,
                    rejectedWholeBuildingPaths, usedWholeBuildingPaths, seed);
            }

            if (instance == null)
            {
                // note: Only after every whole-building candidate fails may a complete supported modular recipe be attempted.
                if (!HasSupportedFallbackRecipe(settlement, palette, seed, lot.purpose))
                    return false;
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

        bool presentationValid = ValidateSettlementPresentation(
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
        // note: The caller owns rejecting and disposing a failed settlement candidate.
        return presentationValid;
    }

    private static bool ValidateSettlementPresentation(
        Transform parent,
        GeneratedSettlementRecord settlement,
        GeneratedRegionAssetPaletteRecord palette,
        int expectedBuildings)
    {
        if (parent == null)
            return false;

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
            return false;
        }

        // note: This gate proves counts and footprint separation only; it does not certify entrances, interiors, or structural support.
        Debug.Log(
            "[YQGeneratedWorldRuntimeBuilder] Settlement presentation validated: " +
            buildingBounds.Count + " whole-building candidates, 0 overlaps, palette " +
            (palette != null ? palette.styleKey : "<unknown>"));
        return true;
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
        // note: Whole-building selection carries the authored V2 support contract so semantic service matches remain spawn-safe.
        YQAssetPlacementContextV2 placementContext = new YQAssetPlacementContextV2
        {
            requiredRole = YQAssetRoleV2.CompleteStructure,
            requiredEnvironment = YQAssetEnvironmentV2.Exterior,
            // note: Approved houses may use a Ground or Foundation support profile; terrain support and its reviewed polygon are the invariant.
            requiredSupportMode = YQAssetSupportModeV2.Unspecified,
            requireTerrainSupport = true,
            requireCollider = true,
            requireNavigation = true,
            requiredFunction = ContainsAny(role, "market", "merchant", "trade", "shop", "vendor", "supply", "barter")
                ? YQAssetFunctionV2.Commerce
                : ContainsAny(role, "smith", "forge", "workshop", "craft", "alchemy")
                    ? YQAssetFunctionV2.Manufacturing
                    : ContainsAny(role, "guard", "command", "watch")
                        ? YQAssetFunctionV2.Security
                        : ContainsAny(role, "civic", "shrine", "temple")
                            ? YQAssetFunctionV2.Civic
                            : ContainsAny(role, "inn", "tavern", "clinic", "healer", "apothecary")
                                ? YQAssetFunctionV2.Service
                                : YQAssetFunctionV2.Habitation
        };

        if (ContainsAny(role, "market", "merchant", "trade", "shop", "vendor", "supply", "barter"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":commerce", placementContext, "shop", "store", "market", "bank", "saloon", "trader", "merchant");
        else if (ContainsAny(role, "smith", "forge", "workshop", "craft", "alchemy"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":craft", placementContext, "smith", "forge", "workshop", "stable", "foundry");
        else if (ContainsAny(role, "inn", "tavern", "clinic", "healer", "apothecary"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":hospitality", placementContext, "inn", "hotel", "saloon", "clinic", "hospital", "apothecary");
        else if (ContainsAny(role, "guard", "command", "watch", "civic", "shrine", "temple"))
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":civic", placementContext, "sheriff", "guard", "townhall", "hall", "church", "temple", "tower");
        else
            match = FindSemanticPaletteAsset(palette.settlementBuilding, seed + ":residence", placementContext, "house", "home", "hut", "cabin", "shack", "residence");

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

            if (candidate != null && !string.IsNullOrWhiteSpace(candidate.assetPath) && !usedPaths.Contains(candidate.assetPath) &&
                YQWorldAssetCatalog.IsAllowedWorldReferenceForSlot(candidate, YQWorldAssetCatalog.SlotSettlementBuilding))
                return candidate;
        }

        return preferred;
    }

    private static GeneratedAssetReferenceRecord FindAlternativeWholeBuilding(
        IReadOnlyList<GeneratedAssetReferenceRecord> candidates, ISet<string> rejected,
        ISet<string> used, string seed)
    {
        if (candidates == null || candidates.Count == 0) return null;
        // note: Stable candidate traversal first seeks an unused building, then permits a previously successful variant; rejected or missing identities are never retried.
        int start = Mathf.Min(candidates.Count - 1,
            Mathf.FloorToInt(Deterministic01(seed + ":remaining_whole_buildings") * candidates.Count));
        for (int pass = 0; pass < 2; pass++)
            for (int offset = 0; offset < candidates.Count; offset++)
            {
                var candidate = candidates[(start + offset) % candidates.Count];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.assetPath) ||
                    !YQWorldAssetCatalog.IsAllowedWorldReferenceForSlot(candidate, YQWorldAssetCatalog.SlotSettlementBuilding) ||
                    rejected.Contains(candidate.assetPath) ||
                    (pass == 0 && used.Contains(candidate.assetPath))) continue;
                return candidate;
            }
        return null;
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
        else if (ContainsAny(purpose, "farm", "field", "orchard", "pasture", "stable", "fisher", "fishing", "mill") ||
                 (ContainsAny((settlement != null ? settlement.kind : string.Empty).ToLowerInvariant(), "village", "hamlet", "rural", "farm") && lotIndex % 3 == 0))
        {
            // note: Rural cells receive land-use cues from the approved palette so a village edge reads as worked countryside rather than a ring of bare houses.
            anchor = FindSemanticPaletteAsset(palette.exteriorDeco, seed + ":land_use", "fence", "gate", "field", "orchard", "pasture", "mill", "dock");
            anchor ??= FindSemanticPaletteAsset(palette.floorDeco, seed + ":land_use_ground", "hay", "trough", "cart", "wagon", "barrel", "basket", "crate");
            accent = FindSemanticPaletteAsset(palette.vegetation, seed + ":land_use_planting", "tree", "bush", "shrub", "grass", "reed");
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
        return FindSemanticPaletteAsset(references, seed, null, keywords);
    }

    private static GeneratedAssetReferenceRecord FindSemanticPaletteAsset(
        List<GeneratedAssetReferenceRecord> references,
        string seed,
        YQAssetPlacementContextV2 placementContext,
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

            // note: Semantic POI and settlement picks must come from the same reviewed runtime catalog as slot-based picks; raw palette text cannot reintroduce quarantined prefabs.
            if (!YQWorldAssetCatalog.IsSpatiallyApprovedForRuntime(reference.assetPath))
                continue;
            if (placementContext != null &&
                !YQWorldAssetCatalog.IsAllowedInPlacementContext(reference, placementContext))
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

        // note: Missing construction contracts must stop this build, not turn arbitrary catalog scenery into a claimed house.
        if (!IsCompleteBuildingCellRecipe(recipe))
            throw new InvalidOperationException("Settlement construction failed for '" + settlement.displayName +
                "': palette '" + palette.styleKey + "' has no complete supported modular recipe for '" + purpose +
                "'. Supply a complete building or compatible floor, walls, entrance and roof; arbitrary slot fallback is disabled.");

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

    private static bool HasSupportedFallbackRecipe(
        GeneratedSettlementRecord settlement, GeneratedRegionAssetPaletteRecord palette,
        string seed, string purpose)
    {
        // note: Missing approved content is a construction rejection, not an unexpected exception or permission to spawn arbitrary fragments.
        if (IsCompleteBuildingCellRecipe(ResolveCuratedBuildingCellRecipe(palette, seed, purpose)))
            return true;
        Debug.LogError("[YQGeneratedWorldRuntimeBuilder] SETTLEMENT FALLBACK REJECTED: location=" +
            settlement.settlementId + ", palette=" + palette.styleKey + ", purpose=" + purpose +
            ". No complete building or supported modular recipe is available; candidate will be removed.");
        return false;
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

    private static bool IsCompleteBuildingCellRecipe(CuratedBuildingCellRecipe recipe)
    {
        // note: A separate door leaf is optional, but the named opening-bearing front and every structural shell component are mandatory.
        return recipe != null && recipe.floor != null && recipe.backWall != null &&
            recipe.sideWall != null && recipe.frontWall != null && recipe.roof != null;
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

        // note: A slot label does not establish mating geometry or a usable entrance. Live capture showed sand floors and ruin roofs produced by this generic fallback.
        return null;
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
        // note: Street furniture is grounded outdoors without retained collision; selection and spawning share this support contract.
        var placementContext = new YQAssetPlacementContextV2
        {
            requiredEnvironment = YQAssetEnvironmentV2.Exterior,
            requiredSupportMode = YQAssetSupportModeV2.Ground,
            requireTerrainSupport = true,
            requireCollider = false,
            requireNavigation = false
        };
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
                    seed,
                    placementContext);

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
                false,
                placementContext: placementContext);
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
        string seed,
        YQAssetPlacementContextV2 placementContext)
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
            if (IsAmbientSettlementDecoration(source[i]) &&
                YQWorldAssetCatalog.IsAllowedInPlacementContext(source[i], placementContext))
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
        // note: Settlement-edge vegetation is exterior ground dressing; it does not require a walkable navigation surface or retained collider.
        var placementContext = new YQAssetPlacementContextV2
        {
            requiredEnvironment = YQAssetEnvironmentV2.Exterior,
            requiredSupportMode = YQAssetSupportModeV2.Ground,
            requireTerrainSupport = true,
            requireCollider = false,
            requireNavigation = false
        };
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
                        seed,
                        placementContext);
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
                false,
                placementContext: placementContext);
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
    bool groundInstance = true,
    YQAssetPlacementContextV2 placementContext = null)
    {
        if (reference == null ||
            registry == null ||
            string.IsNullOrWhiteSpace(
                reference.assetPath))
        {
            return null;
        }

        // note: Keep a final runtime defense at the common spawn boundary so direct palette selectors cannot bypass spatial approval.
        if (!YQWorldAssetCatalog.IsAllowedInPlacementContext(reference, placementContext))
        {
            Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Skipping unreviewed or context-incompatible spatial asset: " + reference.assetPath);
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
        YQPreparedSpatialMaterializationV2 materializationV2 = null;
        YQSpatialBlueprintTerrainSamplerV2 constructionTerrainSampler = null;
        // note: Keep the preflight's exact choices: terrain construction must not reroll districts in a different settlement/hostile iteration order.
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
            !YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out materializationV2,
                out string materializationFailure))
        {
            // note: Construction cannot fall back to V1 anchors after a V2 cutover; reject before mutating a single terrain sample.
            Debug.LogError(
                "[YQGeneratedWorldRuntimeBuilder] V2 CONSTRUCTION INPUT REJECTED: " +
                materializationFailure);
            completed?.Invoke(false);
            yield break;
        }
        // note: Reuse the accepted spatial masks while grading settlement shoulders so route and water corridors remain authoritative.
        if (materializationV2 != null)
            YQSpatialBlueprintTerrainSamplerV2.TryPrepare(
                plan,
                out constructionTerrainSampler,
                out _);

        List<ConstructionFootprintReservation> reservations =
            new List<ConstructionFootprintReservation>();
        Dictionary<string, string> v2CompositionOwners =
            materializationV2 != null
                ? new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                : null;
        List<YQGeneratedWorldEnvironment.LivedPathTerrainReservation>
            pathTerrainReservations =
                new List<YQGeneratedWorldEnvironment.LivedPathTerrainReservation>();
        Vector3 originAnchor = YQGeneratedWorldLayout.GetVeyOriginAnchor();

        pathTerrainReservations.Add(
            new YQGeneratedWorldEnvironment.LivedPathTerrainReservation(
                originAnchor,
                0f));
        // note: The origin's occupancy reserve is not a foundation; only the authored summit and house core prevent road earthworks.
        pathTerrainReservations.Add(new YQGeneratedWorldEnvironment.LivedPathTerrainReservation(
            originAnchor + OriginGoddessSummitOffset, 7f));
        pathTerrainReservations.Add(
            new YQGeneratedWorldEnvironment.LivedPathTerrainReservation(
                originAnchor + OriginWitchHouseOffset,
                15f));

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

                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Shaping landforms and roads",
                    4,
                    9,
                    "Resolving settlement support shelf " + (index + 1) +
                    " of " + plan.settlements.Count,
                    Mathf.Lerp(
                        0.70f,
                        0.715f,
                        plan.settlements.Count > 0
                            ? index / (float)plan.settlements.Count
                            : 1f));

                if (settlement == null)
                {
                    prepared = false;
                    continue;
                }

                if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
                    !IsSettlementInsideGeneratedTerrain(plan, settlement, terrain))
                {
                    // note: Distant accepted V2 settlements belong to streamed cells; the 1024m origin TerrainData must not reject their off-terrain construction shelves.
                    Debug.Log(
                        "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS DEFERRED V2 SETTLEMENT: " +
                        settlement.displayName + " is outside the authored origin terrain.");
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

                    string[] semanticSliceTags =
                        YQCompiledWorldSiteBindingService
                            .BuildSettlementSemanticSliceTags(settlement);
                    bool sliceRadiusResolved = false;
                    float sliceRadius = 0f;
                    string compositionFailure = string.Empty;
                    if (materializationV2 != null)
                    {
                        yield return ResolveUniqueSemanticCompositionV2Routine(
                            site,
                            semanticSliceTags,
                            SettlementSeed(settlement),
                            settlement.settlementId,
                            settlement.displayName,
                            v2CompositionOwners,
                            materializationV2,
                            (success, radius, failure) =>
                            {
                                sliceRadiusResolved = success;
                                sliceRadius = radius;
                                compositionFailure = failure;
                            });
                    }
                    else
                    {
                        yield return YQCompiledWorldSiteInstance
                            .ResolveSemanticSliceRadiusRoutine(
                                site,
                                semanticSliceTags,
                                SettlementSeed(settlement),
                                (success, radius) =>
                                {
                                    sliceRadiusResolved = success;
                                    sliceRadius = radius;
                                });
                    }
                    if (!sliceRadiusResolved ||
                        !TryResolveConstructionRadii(
                            sliceRadius,
                            out footprintRadius,
                            out flatRadius,
                            out outerRadius))
                    {
                        // note: Fail before terrain mutation when the exact deterministic district slice has no valid support envelope.
                        Debug.LogError(
                            "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED SETTLEMENT\n" +
                            "Settlement: " + settlement.displayName + "\n" +
                            "Reason: " +
                            (!string.IsNullOrWhiteSpace(compositionFailure)
                                ? compositionFailure
                                : "its selected semantic slice has no valid runtime footprint."));
                        prepared = false;
                        continue;
                    }

                    if (materializationV2 != null &&
                        !materializationV2.TryValidateFootprint(
                            settlement.settlementId,
                            footprintRadius,
                            out string footprintFailure))
                    {
                        Debug.LogError(
                            "[YQGeneratedWorldRuntimeBuilder] V2 SETTLEMENT FOOTPRINT REJECTED\n" +
                            "Settlement: " + settlement.displayName + "\n" +
                            "Reason: " + footprintFailure);
                        prepared = false;
                        continue;
                    }
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

                Vector3 requestedCenter = YQGeneratedWorldLayout.GetSettlementAnchor(
                    plan,
                    settlement,
                    terrain);
                string label = "settlement " + settlement.displayName;

                // note: An accepted V2 site already owns an immutable reserve, frontage, and route endpoint; the legacy circular-spacing grid is stricter than that contract and must not relocate it after acceptance.
                Vector3 center = requestedCenter;
                string conflict = string.Empty;
                bool centerResolved = materializationV2 != null || TryResolveConstructionCenter(
                    reservations,
                    label,
                    requestedCenter,
                    footprintRadius,
                    outerRadius,
                    terrain,
                    plan.worldSeed + "|" + settlement.settlementId,
                    out center,
                    out conflict);
                bool acceptedV2CenterPreserved =
                    materializationV2 == null ||
                    HorizontalDistanceSquared(center, requestedCenter) <= 0.01f;

                if (!centerResolved ||
                    !acceptedV2CenterPreserved ||
                    !GradeTerrainPad(
                        terrain,
                        center,
                        flatRadius,
                        outerRadius,
                        ResolveMinimumConstructionWorldHeight(
                            plan.worldSeed,
                            terrain,
                            center,
                            footprintRadius),
                        YQProceduralSettlementLayout.Get(ResolveSemanticCompositionSeedV2(
                            plan, settlement.settlementId, SettlementSeed(settlement))),
                        ResolveSettlementHeading(plan, settlement),
                        constructionTerrainSampler))
                {
                    Debug.LogError(
                        "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED SETTLEMENT\n" +
                        "Settlement: " + settlement.displayName + "\n" +
                        "Reason: " +
                        (!acceptedV2CenterPreserved
                            ? "the accepted V2 anchor would need relocation, which would detach it from its road and terrain reserve."
                            : conflict));
                    prepared = false;
                    continue;
                }

                // note: Publish the finished construction shelf as settlement authority before roads, foliage, geometry, residents, and quests resolve the location.
                center.y = SampleTerrainDataWorldHeight(
                    terrain,
                    center);
                YQGeneratedWorldLayout.SetRuntimeSettlementAnchor(
                    settlement.settlementId,
                    center);

                Vector2 centerHorizontal = new Vector2(center.x, center.z);
                Vector2 requestedHorizontal = new Vector2(
                    requestedCenter.x,
                    requestedCenter.z);

                if ((centerHorizontal - requestedHorizontal).sqrMagnitude > 0.01f)
                {
                    Debug.LogWarning(
                        "[YQGeneratedWorldRuntimeBuilder] SETTLEMENT RELOCATED\n" +
                        "Settlement: " + settlement.displayName + "\n" +
                        "Requested anchor: " + requestedCenter + "\n" +
                        "Reserved anchor: " + center + "\n" +
                        "Reason: " + conflict);
                }

                // note: Persist only the candidate whose actual footprint was fitted successfully; legacy seeds have no candidate to commit.
                string layoutSeed = ResolveSemanticCompositionSeedV2(plan, settlement.settlementId, SettlementSeed(settlement));
                if (YQProceduralSettlementLayout.Get(layoutSeed) != null)
                {
                    YQProceduralSettlementLayout.Commit(settlement, layoutSeed);
                    _compiledBindingsChangedDuringBuild = true;
                    Debug.Log("[WORLDGEN] PROCEDURAL CELL STREETS ACTIVE: settlement=" + settlement.settlementId +
                        ", blocks=" + settlement.proceduralLayout.cells.Count + ", streets=" + settlement.proceduralLayout.streets.Count +
                        ", radius=" + settlement.proceduralLayout.radius + ", geometry=" +
                        YQProceduralSettlementLayout.GeometrySignature(settlement.proceduralLayout));
                }
                reservations.Add(new ConstructionFootprintReservation(
                    label,
                    center,
                    footprintRadius));
                // note: Protect the actual parcel foundations, not empty space and streets inside the district's circular envelope.
                var roadProtectedLayout = settlement.proceduralLayout;
                if (roadProtectedLayout != null && roadProtectedLayout.cells.Count > 0)
                {
                    Quaternion districtRotation = Quaternion.Euler(0f, ResolveSettlementHeading(plan, settlement), 0f);
                    foreach (var cell in roadProtectedLayout.cells)
                    {
                        Vector3 parcelCenter = center + districtRotation * (cell.boundsCenter - roadProtectedLayout.origin);
                        pathTerrainReservations.Add(new YQGeneratedWorldEnvironment.LivedPathTerrainReservation(
                            parcelCenter, new Vector2(cell.boundsSize.x, cell.boundsSize.z) * .5f + Vector2.one * 3f,
                            // note: The reservation follows the cell's authored yaw as well as the district heading; district-only axes left road repair free to overwrite rotated aprons.
                            Mathf.Repeat(ResolveSettlementHeading(plan, settlement) + cell.yaw, 360f)));
                    }
                }
                else
                    pathTerrainReservations.Add(new YQGeneratedWorldEnvironment.LivedPathTerrainReservation(center, flatRadius));

                yield return null;
            }
        }

        if (YQWorldGenerationArchitecture.UsesCompiledWorld &&
            plan.encampments != null)
        {
            for (int index = 0; index < plan.encampments.Count; index++)
            {
                GeneratedEncampmentRecord encampment = plan.encampments[index];

                YQStartupLoadingScreen.SetGenerationWorkStage(
                    "Shaping landforms and roads",
                    4,
                    9,
                    "Resolving hostile-site support shelf " + (index + 1) +
                    " of " + plan.encampments.Count,
                    Mathf.Lerp(
                        0.715f,
                        0.725f,
                        plan.encampments.Count > 0
                            ? index / (float)plan.encampments.Count
                            : 1f));

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
                string[] semanticSliceTags =
                    YQCompiledWorldSiteBindingService
                        .BuildEncampmentSemanticSliceTags(encampment);
                string semanticSliceSeed =
                    !string.IsNullOrWhiteSpace(encampment.deterministicSeed)
                        ? encampment.deterministicSeed
                        : encampment.encampmentId;
                bool sliceRadiusResolved = false;
                float sliceRadius = 0f;
                string compositionFailure = string.Empty;
                if (materializationV2 != null)
                {
                    yield return ResolveUniqueSemanticCompositionV2Routine(
                        site,
                        semanticSliceTags,
                        semanticSliceSeed,
                        encampment.encampmentId,
                        encampment.displayName,
                        v2CompositionOwners,
                        materializationV2,
                        (success, radius, failure) =>
                        {
                            sliceRadiusResolved = success;
                            sliceRadius = radius;
                            compositionFailure = failure;
                        });
                }
                else
                {
                    yield return YQCompiledWorldSiteInstance
                        .ResolveSemanticSliceRadiusRoutine(
                            site,
                            semanticSliceTags,
                            semanticSliceSeed,
                            (success, radius) =>
                            {
                                sliceRadiusResolved = success;
                                sliceRadius = radius;
                            });
                }
                if (!sliceRadiusResolved ||
                    !TryResolveConstructionRadii(
                        sliceRadius,
                        out footprintRadius,
                        out flatRadius,
                        out outerRadius))
                {
                    // note: Hostile construction reserves exactly the curated encounter slice that streaming will later instantiate.
                    Debug.LogError(
                        "[YQGeneratedWorldRuntimeBuilder] TERRAIN PREPASS REJECTED HOSTILE SITE\n" +
                        "Site: " + encampment.displayName + "\n" +
                        "Reason: " +
                        (!string.IsNullOrWhiteSpace(compositionFailure)
                            ? compositionFailure
                            : "its selected semantic slice has no valid runtime footprint."));
                    prepared = false;
                    continue;
                }

                if (materializationV2 != null &&
                    !materializationV2.TryValidateFootprint(
                        encampment.encampmentId,
                        footprintRadius,
                        out string footprintFailure))
                {
                    Debug.LogError(
                        "[YQGeneratedWorldRuntimeBuilder] V2 HOSTILE FOOTPRINT REJECTED\n" +
                        "Site: " + encampment.displayName + "\n" +
                        "Reason: " + footprintFailure);
                    prepared = false;
                    continue;
                }

                Vector3 requestedCenter = YQGeneratedWorldLayout.GetEncampmentAnchor(
                    plan,
                    encampment,
                    terrain);
                string label = "hostile site " + encampment.displayName;

                // note: V2 hostile sites share the same accepted-reserve rule as settlements; rerunning the legacy relocation grid would detach their saved route frontage.
                Vector3 center = requestedCenter;
                string conflict = string.Empty;
                bool centerResolved = materializationV2 != null || TryResolveConstructionCenter(
                    reservations,
                    label,
                    requestedCenter,
                    footprintRadius,
                    outerRadius,
                    terrain,
                    plan.worldSeed + "|" + encampment.encampmentId,
                    out center,
                    out conflict);
                bool acceptedV2CenterPreserved =
                    materializationV2 == null ||
                    HorizontalDistanceSquared(center, requestedCenter) <= 0.01f;
                bool terrainPadGraded = centerResolved &&
                    acceptedV2CenterPreserved &&
                    GradeTerrainPad(
                        terrain,
                        center,
                        flatRadius,
                        outerRadius,
                        ResolveMinimumConstructionWorldHeight(
                            plan.worldSeed,
                            terrain,
                            center,
                            footprintRadius));
                if (!terrainPadGraded)
                {
                    // note: Hostile camps already run the same canonical-terrain grounding pass when their reviewed cells stream in; a pad that cannot be pre-graded is a degraded camp placement, not grounds to imprison a fully materialized saved world behind Continue forever.
                    Debug.LogWarning(
                        "[YQGeneratedWorldRuntimeBuilder] HOSTILE TERRAIN PREPASS DEFERRED TO RUNTIME GROUNDING\n" +
                        "Site: " + encampment.displayName + "\n" +
                        "Reason: " +
                        (!acceptedV2CenterPreserved
                            ? "the accepted V2 anchor would need relocation"
                            : centerResolved
                                ? "the requested support shelf could not be graded safely"
                                : conflict));
                    continue;
                }

                // note: The relocated runtime anchor adopts the finished shelf elevation, including any lake-clearance raise, before hostile-site content binds to it.
                center.y = SampleTerrainDataWorldHeight(
                    terrain,
                    center);
                YQGeneratedWorldLayout.SetRuntimeEncampmentAnchor(
                    encampment.encampmentId,
                    center);

                Vector2 centerHorizontal = new Vector2(center.x, center.z);
                Vector2 requestedHorizontal = new Vector2(
                    requestedCenter.x,
                    requestedCenter.z);

                if ((centerHorizontal - requestedHorizontal).sqrMagnitude > 0.01f)
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
                // note: Keep only the actual hostile foundation protected; the outer presentation shelf must remain gradeable so the accepted route gate cannot step off a cliff at the site boundary.
                pathTerrainReservations.Add(
                    new YQGeneratedWorldEnvironment.LivedPathTerrainReservation(
                        center,
                        footprintRadius + 3f));

                yield return null;
            }
        }

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Shaping landforms and roads",
            4,
            9,
            "Repairing roads, cave climbs, and crossings around finished sites",
            0.727f);
        // note: Restore bank support before the final road pass; otherwise a shoreline lift can reintroduce a cliff on a route that was already grade-constrained.
        yield return YQGeneratedWorldEnvironment.RepairWaterBanksRoutine(terrain, plan, pathTerrainReservations);
        // note: Roads, cave climbs, and water crossings are the final terrain authority over their narrow corridors, including crossings raised above accepted water surfaces.
        yield return YQGeneratedWorldEnvironment
            .RepairLivedPathTerrainRoutine(
                terrain,
                plan,
                pathTerrainReservations);
        // note: Construction can refill an accepted channel; recut wet cores once after roads so the runtime water ribbon cannot disappear into final terrain.
        yield return YQGeneratedWorldEnvironment
            .RepairAcceptedWaterChannelsRoutine(
                terrain,
                plan,
                pathTerrainReservations);

        if (YQWorldGenerationArchitecture.UsesCompiledWorld && plan.settlements != null)
        {
            // note: Reassert committed parcel datums after bank, road and water passes; accepted cell approaches must see the same terrain that their foundations were reviewed against.
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[index];
                YQProceduralSettlementLayoutRecord layout = settlement?.proceduralLayout;
                if (settlement == null || layout == null || layout.cells == null || layout.cells.Count == 0)
                    continue;
                Vector3 center = YQGeneratedWorldLayout.GetSettlementAnchor(plan, settlement, terrain);
                float outerRadius = Mathf.Max(8f, layout.radius + 2f);
                float flatRadius = Mathf.Max(4f, outerRadius * .72f);
                if (!GradeTerrainPad(
                        terrain,
                        center,
                        flatRadius,
                        outerRadius,
                        ResolveMinimumConstructionWorldHeight(plan.worldSeed, terrain, center, outerRadius),
                        layout,
                        ResolveSettlementHeading(plan, settlement),
                        constructionTerrainSampler))
                {
                    Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] ACCEPTED PARCEL REASSERTION DEFERRED: " + settlement.displayName);
                }
            }
        }

        // note: Reconcile the route graph once after parcel datums are restored so settlement gates and regional connectors cannot retain a terrain step from an earlier water-bank pass.
        yield return YQGeneratedWorldEnvironment.RepairLivedPathTerrainRoutine(
            terrain,
            plan,
            pathTerrainReservations);

        YQStartupLoadingScreen.SetGenerationWorkStage(
            "Shaping landforms and roads",
            4,
            9,
            "Synchronizing the final terrain collider once",
            0.729f);
        // note: Every pad and lived path uses delayed writes; one required heightmap synchronization publishes render/physics authority, while the redundant full Terrain.Flush pass is deliberately avoided.
        yield return null;
        terrain.terrainData.SyncHeightmap();
        yield return null;
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
        // note: Give accepted settlement earthworks a broad deterministic shoulder so flat parcels ease into hills instead of ending in a sheer artificial cut; the extra reach is still bounded well inside the reviewed world envelope.
        // note: Give accepted settlement earthworks a long, route-aware shoulder so flat parcels ease into hills instead of ending at a hard cut.
        outerRadius = footprintRadius + 54f;
        return outerRadius < YQGeneratedWorldTerrain.WorldSize * 0.5f;
    }

    private static bool TryResolveConstructionRadii(
        float validatedSliceRadius,
        out float footprintRadius,
        out float flatRadius,
        out float outerRadius)
    {
        footprintRadius = 0f;
        flatRadius = 0f;
        outerRadius = 0f;
        if (float.IsNaN(validatedSliceRadius) ||
            float.IsInfinity(validatedSliceRadius) ||
            validatedSliceRadius <= 0f)
        {
            return false;
        }

        // note: The validated semantic aggregate is the physical runtime site; small grading shoulders blend that exact footprint into surrounding terrain.
        footprintRadius = validatedSliceRadius;
        flatRadius = footprintRadius + 3f;
        // note: Semantic slice earthworks use the same broad shoulder as reviewed sites, preserving the authored footprint while removing abrupt settlement edges.
        // note: Semantic slice earthworks use the same broad shoulder as reviewed sites, preserving the authored footprint while removing abrupt settlement edges.
        outerRadius = footprintRadius + 54f;
        return outerRadius < YQGeneratedWorldTerrain.WorldSize * 0.5f;
    }

    private IEnumerator ResolveUniqueSemanticCompositionV2Routine(
        YQRuntimeWorldSiteRecord site,
        string[] semanticTags,
        string baseSeed,
        string locationId,
        string displayName,
        Dictionary<string, string> compositionOwners,
        YQPreparedSpatialMaterializationV2 materialization,
        Action<bool, float, string> completed)
    {
        // note: Once preflight has chosen a variant, later terrain/streaming checks revalidate only that choice rather than selecting different geometry for the same site.
        bool hasPreparedSeed = !string.IsNullOrWhiteSpace(locationId) &&
            _resolvedSemanticCompositionSeedsV2.ContainsKey(locationId);
        string preparedSeed = hasPreparedSeed
            ? _resolvedSemanticCompositionSeedsV2[locationId]
            : string.Empty;
        int maximumVariants = hasPreparedSeed ? 1 : 12;
        string lastFailure = string.Empty;
        if (materialization == null ||
            !materialization.TryGetSiteBySemanticId(locationId, out YQSpatialMaterializationSiteV2 acceptedSite) ||
            acceptedSite.RequiredFunctions.Count == 0)
        {
            completed?.Invoke(false, 0f, "Accepted V2 site functional requirements are missing.");
            yield break;
        }

        for (int variant = 0; variant < maximumVariants; variant++)
        {
            string candidateSeed = hasPreparedSeed
                ? preparedSeed
                : variant == 0
                ? baseSeed
                : (baseSeed ?? string.Empty) + "|semantic_variant|" +
                  variant;
            bool resolved = false;
            float radius = 0f;
            YQSemanticSiteCompositionV2 composition = default;
            string resolutionFailure = string.Empty;
            bool variantIndependentFailure = false;
            yield return YQCompiledWorldSiteInstance
                .ResolveSemanticCompositionV2Routine(
                    site,
                    semanticTags,
                    candidateSeed,
                    (success, resolvedRadius, candidate, failure) =>
                    {
                        resolved = success;
                        radius = resolvedRadius;
                        composition = candidate;
                        resolutionFailure = failure;
                    },
                    acceptedSite.RequiredFunctions,
                    unavailable => variantIndependentFailure = unavailable);

            if (!resolved)
            {
                lastFailure = resolutionFailure;
                // note: Missing reviewed functions or a missing manifest cannot improve with another seed; move on to another eligible kit instead.
                if (variantIndependentFailure)
                    break;
                // note: Cached manifest retries still yield so a site with missing contracts cannot run all twelve selections on one frame.
                yield return null;
                continue;
            }
            if (!materialization.TryValidateFootprint(locationId, radius, out lastFailure))
            {
                yield return null;
                continue;
            }
            if (!TryReserveUniqueCompositionV2(
                    compositionOwners,
                    locationId,
                    displayName,
                    composition,
                    out string repeatedFailure))
            {
                lastFailure = repeatedFailure;
                yield return null;
                continue;
            }

            // note: Prepass and later streaming consume the exact same accepted variant seed, so terrain support can never be graded for one district and then receive another.
            _resolvedSemanticCompositionSeedsV2[locationId] = candidateSeed;
            completed?.Invoke(true, radius, string.Empty);
            yield break;
        }

        completed?.Invoke(
            false,
            0f,
            !string.IsNullOrWhiteSpace(lastFailure)
                ? lastFailure
                : "no distinct valid semantic composition was available.");
    }

    private string ResolveSemanticCompositionSeedV2(
        GeneratedWorldPlanRecord plan,
        string locationId,
        string fallbackSeed)
    {
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan) &&
            !string.IsNullOrWhiteSpace(locationId) &&
            _resolvedSemanticCompositionSeedsV2.TryGetValue(
                locationId,
                out string resolvedSeed))
        {
            return resolvedSeed;
        }

        return fallbackSeed ?? string.Empty;
    }

    private static bool TryReserveUniqueCompositionV2(
        Dictionary<string, string> compositionOwners,
        string locationId,
        string displayName,
        YQSemanticSiteCompositionV2 composition,
        out string failure)
    {
        failure = string.Empty;
        if (compositionOwners == null)
            return true;
        if (string.IsNullOrWhiteSpace(composition.compositionSignature) ||
            composition.SelectedCount == 0)
        {
            failure = "its semantic composition has no stable geometry signature.";
            return false;
        }

        if (compositionOwners.TryGetValue(
                composition.compositionSignature,
                out string existingOwner))
        {
            // note: Reviewed geometry is reusable content, not an exclusive world resource; each location still validates its own functions, reserve and physical construction.
            Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Reviewed assembly reused by " +
                displayName + " and " + existingOwner + ". Additional library variety remains a content task.");
            return true;
        }

        compositionOwners.Add(
            composition.compositionSignature,
            !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : locationId);
        return true;
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

    private static bool TryResolveConstructionCenter(
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

    private static float HorizontalDistanceSquared(
        Vector3 left,
        Vector3 right)
    {
        float dx = left.x - right.x;
        float dz = left.z - right.z;
        return dx * dx + dz * dz;
    }

    internal static bool GradeTerrainPad(
        Terrain terrain,
        Vector3 center,
        float flatRadius,
        float outerRadius)
    {
        return GradeTerrainPad(
            terrain,
            center,
            flatRadius,
            outerRadius,
            float.NegativeInfinity);
    }

    internal static bool GradeTerrainPad(
        Terrain terrain,
        Vector3 center,
        float flatRadius,
        float outerRadius,
        float minimumWorldHeight,
        YQProceduralSettlementLayoutRecord constructionLayout = null,
        float constructionHeading = 0f,
        YQSpatialBlueprintTerrainSamplerV2 spatialSampler = null)
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

        if (!float.IsNaN(minimumWorldHeight) &&
            !float.IsInfinity(minimumWorldHeight))
        {
            // note: Construction overlapping a deterministic lake rises into a dry shelf/island before buildings spawn; the water plane never becomes the settlement's floor.
            sampledWorldHeight =
                Mathf.Max(
                    sampledWorldHeight,
                    minimumWorldHeight);
        }
        float targetHeight = Mathf.Clamp01(
            (sampledWorldHeight - origin.y) / Mathf.Max(0.001f, size.y));
        // note: Expand the construction shoulder when the accepted parcel datum would otherwise exceed a natural walking slope; this turns steep source relief into a deterministic ramp instead of a sheer pad wall.
        float targetWorldHeight = origin.y + targetHeight * size.y;
        float elevationDelta = 0f;
        // note: Read the four shoulder samples before editing the heightmap so the adaptive ramp responds to the actual surrounding relief rather than the already flattened centre.
        Vector3[] shoulderSamples =
        {
            center + Vector3.forward * outerRadius,
            center - Vector3.forward * outerRadius,
            center + Vector3.right * outerRadius,
            center - Vector3.right * outerRadius
        };
        for (int sampleIndex = 0; sampleIndex < shoulderSamples.Length; sampleIndex++)
            elevationDelta = Mathf.Max(
                elevationDelta,
                Mathf.Abs(
                    terrain.SampleHeight(shoulderSamples[sampleIndex]) +
                    origin.y - targetWorldHeight));
        float slopeLimitedRun = elevationDelta /
            Mathf.Max(.1f, Mathf.Tan(24f * Mathf.Deg2Rad));
        float requestedOuterRadius = Mathf.Max(
            outerRadius,
            flatRadius + slopeLimitedRun + 8f);
        float edgeClearance = Mathf.Min(
            Mathf.Min(center.x - origin.x, origin.x + size.x - center.x),
            Mathf.Min(center.z - origin.z, origin.z + size.z - center.z));
        outerRadius = Mathf.Min(
            requestedOuterRadius,
            Mathf.Max(flatRadius + 2f, edgeClearance - 1f));
        // note: Recompute the heightmap window after the adaptive shoulder expands; the original window was derived before the slope-limited radius and silently left the new ramp outside the edited terrain.
        radiusX = Mathf.CeilToInt(
            outerRadius / size.x * (resolution - 1));
        radiusZ = Mathf.CeilToInt(
            outerRadius / size.z * (resolution - 1));
        startX = Mathf.Clamp(centerX - radiusX, 0, resolution - 1);
        startZ = Mathf.Clamp(centerZ - radiusZ, 0, resolution - 1);
        endX = Mathf.Clamp(centerX + radiusX, 0, resolution - 1);
        endZ = Mathf.Clamp(centerZ + radiusZ, 0, resolution - 1);
        width = endX - startX + 1;
        height = endZ - startZ + 1;
        if (width <= 1 || height <= 1)
            return false;
        heights = data.GetHeights(startX, startZ, width, height);
        // note: Resolve the complete parcel profile before editing the heightmap; accepted records reuse their saved world elevations.
        float[] parcelHeights = null;
        if (constructionLayout != null && constructionLayout.earthworkVersion != 0 &&
            !TryResolveParcelGroundHeights(terrain, center, constructionHeading, constructionLayout,
                minimumWorldHeight, out parcelHeights))
            return false;
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
                // note: Independent blocks and their declared streets own earthworks; empty space inside the town's bounding circle retains its original relief.
                float localTargetHeight = targetHeight;
                // note: Keep the broad shoulder away from accepted route, water, and cave masks; those corridors must retain their own physical grade.
                YQSpatialTerrainSampleV2 spatialSample = spatialSampler != null
                    ? spatialSampler.Sample(worldX, worldZ)
                    : default;
                bool protectedCorridor = spatialSampler != null &&
                    (spatialSample.routeMask > .08f ||
                     spatialSample.waterMask > .08f ||
                     spatialSample.caveMassMask > .08f);
                float radialBlend = protectedCorridor ? 0f : blend;
                if (parcelHeights != null)
                {
                    // note: Flat parcel cores meet sloped street connections; unowned ground retains the original heightmap.
                    ResolveParcelEarthwork(
                        constructionLayout,
                        parcelHeights,
                        new Vector2(worldX - center.x, worldZ - center.z),
                        constructionHeading,
                        out float parcelBlend,
                        out float worldTarget);
                    // note: Preserve the broad radial shoulder outside semantic cells; discarding it left an abrupt parcel edge and a sheer settlement cut.
                    if (parcelBlend > 0.0001f)
                    {
                        blend = Mathf.Max(radialBlend, parcelBlend);
                        localTargetHeight = (worldTarget - origin.y) / size.y;
                    }
                    else
                    {
                        blend = radialBlend;
                    }
                }
                else if (constructionLayout != null)
                {
                    // note: Semantic street ownership augments the radial shoulder instead of replacing it, keeping settlement edges walkable on natural hills.
                    blend = Mathf.Max(
                        radialBlend,
                        ResolveConstructionEarthworkWeight(
                            constructionLayout,
                            new Vector2(worldX - center.x, worldZ - center.z),
                            constructionHeading));
                }
                heights[z, x] = Mathf.Lerp(
                    heights[z, x],
                    localTargetHeight,
                    blend);
            }
        }

        // note: The caller batches all delayed pad writes and publishes one final heightmap, avoiding settlement-by-settlement terrain rebuild stalls.
        data.SetHeightsDelayLOD(startX, startZ, heights);
        // note: Persist only a successfully applied profile; failed candidates must not leave partially resolved elevation records.
        if (parcelHeights != null && constructionLayout.parcelGroundHeights.Count == 0)
            constructionLayout.parcelGroundHeights.AddRange(parcelHeights);
        return true;
    }

    private static bool TryResolveParcelGroundHeights(Terrain terrain, Vector3 center, float heading,
        YQProceduralSettlementLayoutRecord layout, float minimumHeight, out float[] heights)
    {
        heights = null;
        // note: Unknown profile versions and malformed saved elevations require migration, never silent reinterpretation.
        if ((layout.earthworkVersion != 1 && layout.earthworkVersion != 2) || layout.cells == null || layout.cells.Count == 0 ||
            layout.parcelGroundHeights == null ||
            (layout.parcelGroundHeights.Count != 0 && layout.parcelGroundHeights.Count != layout.cells.Count))
            return false;
        var resolved = new float[layout.cells.Count];
        float angle = heading * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
        for (int i = 0; i < layout.cells.Count; i++)
        {
            var cell = layout.cells[i];
            if (cell == null) return false;
            // note: Invert the grading mask transform to sample each parcel in world space before any pad writes occur.
            Vector3 offset = cell.boundsCenter - layout.origin;
            Vector3 point = center + new Vector3(cosine * offset.x + sine * offset.z, 0f,
                -sine * offset.x + cosine * offset.z);
            // note: A clamped tile-edge sample or terrain hole cannot establish a buildable parcel, even when its elevation was persisted.
            if (!YQTerrainApproachV2.TrySampleTerrain(terrain, point, out float existingHeight))
                return false;
            // note: The entire flat core, including its three-metre approach margin, must fit the tile; a valid centre alone can leave edge buildings unsupported.
            if (float.IsNaN(cell.boundsSize.x) || float.IsInfinity(cell.boundsSize.x) || cell.boundsSize.x <= 0f ||
                float.IsNaN(cell.boundsSize.z) || float.IsInfinity(cell.boundsSize.z) || cell.boundsSize.z <= 0f)
                return false;
            for (int cornerZ = -1; cornerZ <= 1; cornerZ += 2)
                for (int cornerX = -1; cornerX <= 1; cornerX += 2)
                {
                    float dx = cornerX * (cell.boundsSize.x * .5f + 3f);
                    float dz = cornerZ * (cell.boundsSize.z * .5f + 3f);
                    Vector3 corner = point + new Vector3(cosine * dx + sine * dz, 0f, -sine * dx + cosine * dz);
                    if (!YQTerrainApproachV2.TrySampleTerrain(terrain, corner, out _)) return false;
                }
            float value = layout.parcelGroundHeights.Count > 0 ? layout.parcelGroundHeights[i] :
                Mathf.Max(existingHeight, minimumHeight);
            if (float.IsNaN(value) || float.IsInfinity(value) || value < terrain.transform.position.y ||
                value > terrain.transform.position.y + terrain.terrainData.size.y || value < minimumHeight)
                return false;
            resolved[i] = value;
        }
        // note: Reconcile only unsaved version-two proposals; accepted parcel elevations remain authoritative on reload.
        if (layout.earthworkVersion == 2 && layout.parcelGroundHeights.Count == 0)
        {
            var original = (float[])resolved.Clone();
            for (int pass = 0; pass < 64; pass++)
            {
                bool changed = false;
                for (int i = 0; i < layout.cells.Count; i++)
                    for (int j = i + 1; j < layout.cells.Count; j++)
                    {
                        var a = layout.cells[i];
                        var b = layout.cells[j];
                        float dx = Mathf.Max(0f, Mathf.Abs(a.boundsCenter.x - b.boundsCenter.x) - (a.boundsSize.x + b.boundsSize.x) * .5f - 6f);
                        float dz = Mathf.Max(0f, Mathf.Abs(a.boundsCenter.z - b.boundsCenter.z) - (a.boundsSize.z + b.boundsSize.z) * .5f - 6f);
                        float allowed = ResolveParcelRiseAllowance(layout, a, b, dx, dz);
                        float difference = resolved[i] - resolved[j];
                        float excess = Mathf.Abs(difference) - allowed;
                        if (excess <= .0001f) continue;
                        // note: Share the minimum required correction between both pads, preserving local relief instead of flattening the town.
                        float correction = Mathf.Sign(difference) * excess * .5f;
                        resolved[i] -= correction;
                        resolved[j] += correction;
                        changed = true;
                    }
                if (!changed) break;
            }
            // note: Reject excessive earthworks before terrain or persistent data changes; relocation must handle unsuitable sites.
            for (int i = 0; i < resolved.Length; i++)
                if (Mathf.Abs(resolved[i] - original[i]) > .5f || resolved[i] < minimumHeight)
                {
                    // note: Distinguish an unsuitable site's earthwork budget from missing terrain or malformed saved data.
                    Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Parcel earthwork rejected: cell=" + layout.cells[i].cellId +
                        " sampled=" + original[i] + " proposed=" + resolved[i] + " correction=" + Mathf.Abs(resolved[i] - original[i]) +
                        " maximumCorrection=0.5 minimumHeight=" + minimumHeight);
                    return false;
                }
        }
        // note: Different elevation planes cannot own the same foundation core; reject before changing either terrain or saved data.
        for (int i = 0; i < layout.cells.Count; i++)
            for (int j = i + 1; j < layout.cells.Count; j++)
            {
                var a = layout.cells[i];
                var b = layout.cells[j];
                // note: Version two requires enough space between flat cores to connect their elevations at the shared surface grade.
                if (layout.earthworkVersion == 2)
                {
                    float gapX = Mathf.Max(0f, Mathf.Abs(a.boundsCenter.x - b.boundsCenter.x) - (a.boundsSize.x + b.boundsSize.x) * .5f - 6f);
                    float gapZ = Mathf.Max(0f, Mathf.Abs(a.boundsCenter.z - b.boundsCenter.z) - (a.boundsSize.z + b.boundsSize.z) * .5f - 6f);
                    float allowedRise = ResolveParcelRiseAllowance(layout, a, b, gapX, gapZ);
                    if (Mathf.Abs(resolved[i] - resolved[j]) > allowedRise + .001f)
                    {
                        // note: A constrained profile that did not converge must reject before terrain mutation, with the conflicting pair identified.
                        Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Parcel grade constraint rejected: " + a.cellId + " / " + b.cellId +
                            " rise=" + Mathf.Abs(resolved[i] - resolved[j]) + " allowance=" + allowedRise);
                        return false;
                    }
                }
                if (Mathf.Abs(a.boundsCenter.x - b.boundsCenter.x) < (a.boundsSize.x + b.boundsSize.x) * .5f + 6f &&
                    Mathf.Abs(a.boundsCenter.z - b.boundsCenter.z) < (a.boundsSize.z + b.boundsSize.z) * .5f + 6f &&
                    Mathf.Abs(resolved[i] - resolved[j]) > .001f)
                    return false;
            }
        heights = resolved;
        return true;
    }

    private static float ResolveParcelRiseAllowance(YQProceduralSettlementLayoutRecord layout,
        YQProceduralCellPlacement a, YQProceduralCellPlacement b, float gapX, float gapZ)
    {
        float allowed = .4f * Mathf.Sqrt(gapX * gapX + gapZ * gapZ);
        if (layout.streets == null) return allowed;
        // note: Axis-aligned opposing cores constrain the cross-section of a separating spine. Other arrangements retain the general distance bound.
        foreach (var road in layout.streets)
        {
            if (road == null || road.width <= 0f) continue;
            bool vertical = Mathf.Abs(road.start.x - road.end.x) < .001f;
            bool horizontal = Mathf.Abs(road.start.z - road.end.z) < .001f;
            if (!vertical && !horizontal) continue;
            float aAcross = vertical ? a.boundsCenter.x : a.boundsCenter.z;
            float bAcross = vertical ? b.boundsCenter.x : b.boundsCenter.z;
            float roadAcross = vertical ? road.start.x : road.start.z;
            if ((aAcross - roadAcross) * (bAcross - roadAcross) >= 0f) continue;
            float alongA = vertical ? a.boundsCenter.z : a.boundsCenter.x;
            float alongB = vertical ? b.boundsCenter.z : b.boundsCenter.x;
            float halfA = (vertical ? a.boundsSize.z : a.boundsSize.x) * .5f + 3f;
            float halfB = (vertical ? b.boundsSize.z : b.boundsSize.x) * .5f + 3f;
            float overlapMin = Mathf.Max(alongA - halfA, alongB - halfB);
            float overlapMax = Mathf.Min(alongA + halfA, alongB + halfB);
            float roadMin = Mathf.Min(vertical ? road.start.z : road.start.x, vertical ? road.end.z : road.end.x);
            float roadMax = Mathf.Max(vertical ? road.start.z : road.start.x, vertical ? road.end.z : road.end.x);
            if (Mathf.Max(overlapMin, roadMin) > Mathf.Min(overlapMax, roadMax)) continue;
            float gap = vertical ? gapX : gapZ;
            // note: Reserve the road width at nine-degree crossfall, leaving terrain interpolation margin; only the remaining gap can use the transition grade.
            float roadRun = Mathf.Min(gap, road.width);
            allowed = Mathf.Min(allowed, .4f * (gap - roadRun) + Mathf.Tan(9f * Mathf.Deg2Rad) * roadRun);
        }
        return allowed;
    }

    private static float ResolvePointRiseAllowance(YQProceduralSettlementLayoutRecord layout,
        YQProceduralCellPlacement cell, Vector2 point, float distance)
    {
        // note: Integrate the smaller cross-road grade along the route from a flat core to this surface sample.
        Vector2 nearest = new Vector2(
            Mathf.Clamp(point.x, cell.boundsCenter.x - cell.boundsSize.x * .5f - 3f, cell.boundsCenter.x + cell.boundsSize.x * .5f + 3f),
            Mathf.Clamp(point.y, cell.boundsCenter.z - cell.boundsSize.z * .5f - 3f, cell.boundsCenter.z + cell.boundsSize.z * .5f + 3f));
        float allowance = .4f * distance;
        if (distance <= .00001f || layout.streets == null) return allowance;
        foreach (var road in layout.streets)
        {
            if (road == null || road.width <= 0f) continue;
            bool vertical = Mathf.Abs(road.start.x - road.end.x) < .001f;
            bool horizontal = Mathf.Abs(road.start.z - road.end.z) < .001f;
            if (!vertical && !horizontal) continue;
            float acrossStart = vertical ? nearest.x : nearest.y;
            float acrossEnd = vertical ? point.x : point.y;
            float across = Mathf.Abs(acrossEnd - acrossStart);
            if (across < .00001f) continue;
            float roadAxis = vertical ? road.start.x : road.start.z;
            float overlap = Mathf.Max(0f, Mathf.Min(Mathf.Max(acrossStart, acrossEnd), roadAxis + road.width * .5f) -
                Mathf.Max(Mathf.Min(acrossStart, acrossEnd), roadAxis - road.width * .5f));
            float alongStart = vertical ? nearest.y : nearest.x;
            float alongEnd = vertical ? point.y : point.x;
            float roadMin = Mathf.Min(vertical ? road.start.z : road.start.x, vertical ? road.end.z : road.end.x);
            float roadMax = Mathf.Max(vertical ? road.start.z : road.start.x, vertical ? road.end.z : road.end.x);
            // note: Fade corridor influence beyond its end caps instead of introducing a height discontinuity at the endpoint.
            float outside = Mathf.Max(0f, roadMin - Mathf.Max(alongStart, alongEnd), Mathf.Min(alongStart, alongEnd) - roadMax);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, outside / 6f);
            allowance -= (.4f - Mathf.Tan(9f * Mathf.Deg2Rad)) * overlap * fade;
        }
        return Mathf.Max(0f, allowance);
    }

    internal static void ResolveParcelEarthwork(YQProceduralSettlementLayoutRecord layout, float[] heights,
        Vector2 worldOffset, float heading, out float weight, out float target)
    {
        // note: Parcel cores take precedence over street shoulders so a nearby road cannot tilt a building foundation.
        float angle = heading * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
        Vector2 point = new Vector2(cosine * worldOffset.x - sine * worldOffset.y + layout.origin.x,
            sine * worldOffset.x + cosine * worldOffset.y + layout.origin.z);
        weight = 0f;
        target = 0f;
        float totalStrength = 0f;
        float nearestCoreDistance = float.PositiveInfinity;
        float lowerSurface = float.NegativeInfinity, upperSurface = float.PositiveInfinity;
        for (int i = 0; i < layout.cells.Count; i++)
        {
            var cell = layout.cells[i];
            float dx = Mathf.Max(0f, Mathf.Abs(point.x - cell.boundsCenter.x) - cell.boundsSize.x * .5f - 3f);
            float dz = Mathf.Max(0f, Mathf.Abs(point.y - cell.boundsCenter.z) - cell.boundsSize.z * .5f - 3f);
            float influence = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / 15f);
            // note: Lipschitz envelopes share a continuous height at every junction and reproduce feasible parcel planes exactly.
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            nearestCoreDistance = Mathf.Min(nearestCoreDistance, distance);
            float riseAllowance = layout.earthworkVersion == 2 ? ResolvePointRiseAllowance(layout, cell, point, distance) : .4f * distance;
            lowerSurface = Mathf.Max(lowerSurface, heights[i] - riseAllowance);
            upperSurface = Mathf.Min(upperSurface, heights[i] + riseAllowance);
            if (influence >= 1f) { weight = 1f; target = heights[i]; return; }
            // note: Smooth competing shoulders rather than switching abruptly between neighboring terrace heights.
            float strength = influence / Mathf.Max(.000001f, 1f - influence);
            totalStrength += strength;
            target += heights[i] * strength;
            weight = Mathf.Max(weight, influence);
        }
        target = totalStrength > 0f ? target / totalStrength : 0f;
        // note: New layouts use the same continuous parcel interpolant at junctions and shoulders; a compact influence cutoff compressed the rise near the edge of its radius.
        if (layout.version >= 4 && layout.earthworkVersion == 1)
            target = SharedParcelHeight(layout, heights, point);
        if (layout.earthworkVersion == 2) target = (lowerSurface + upperSurface) * .5f;
        if (layout.streets == null) return;
        // note: Street intersections share a bounded weighted surface; returning the first full-width road created height jumps when road enumeration changed.
        float roadHeightSum = 0f, roadStrengthSum = 0f, roadMask = 0f;
        foreach (var street in layout.streets)
        {
            if (street == null) continue;
            Vector2 start = new Vector2(street.start.x, street.start.z);
            Vector2 end = new Vector2(street.end.x, street.end.z);
            Vector2 segment = end - start;
            float t = segment.sqrMagnitude > .0001f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / segment.sqrMagnitude) : 0f;
            float shoulder = Mathf.Max(0f, Vector2.Distance(point, start + segment * t) - street.width * .5f - 1f);
            float influence = 1f - Mathf.SmoothStep(0f, 1f, shoulder / 6f);
            if (influence <= 0f) continue;
            // note: New roads extend one common surface's mask; saved version-one roads retain their original profiles.
            if (layout.earthworkVersion == 2) { weight = Mathf.Max(weight, influence); continue; }
            float length = segment.magnitude;
            Vector2 direction = length > .0001f ? segment / length : Vector2.zero;
            // note: Keep the approach flat through each foundation core, then grade the space between the two landings.
            float startT = length > .0001f ? ParcelApproachExtent(layout, start, direction) / length : 0f;
            float endT = length > .0001f ? 1f - ParcelApproachExtent(layout, end, -direction) / length : 1f;
            float startHeight = layout.version >= 4 ? SharedParcelHeight(layout, heights, start) : NearestParcelHeight(layout, heights, start);
            float endHeight = layout.version >= 4 ? SharedParcelHeight(layout, heights, end) : NearestParcelHeight(layout, heights, end);
            float roadTarget = Mathf.Lerp(startHeight, endHeight,
                Mathf.InverseLerp(startT, Mathf.Max(startT + .0001f, endT), t));
            // note: Preserve the recorded surface algorithm for earlier layout versions; corrected junction geometry belongs to new version-four layouts.
            if (layout.version < 4)
            {
                if (influence >= 1f) { weight = 1f; target = roadTarget; return; }
                float oldStrength = influence / Mathf.Max(.000001f, 1f - influence);
                target = (target * totalStrength + roadTarget * oldStrength) / (totalStrength + oldStrength);
                totalStrength += oldStrength;
                weight = Mathf.Max(weight, influence);
                continue;
            }
            // note: Finite weights prevent a nearly saturated shoulder from abruptly taking ownership of the whole junction.
            float strength = influence * influence;
            roadHeightSum += roadTarget * strength;
            roadStrengthSum += strength;
            roadMask = Mathf.Max(roadMask, influence);
        }
        if (roadStrengthSum > 0f)
        {
            // note: Fade road ownership before a fixed foundation core; otherwise its exact flat height meets an unrelated road blend in one terrain sample.
            float roadOwnership = roadMask * Mathf.SmoothStep(0f, 1f, nearestCoreDistance / 6f);
            target = Mathf.Lerp(target, roadHeightSum / roadStrengthSum, roadOwnership);
            weight = Mathf.Max(weight, roadMask);
        }
    }

    private static float ParcelApproachExtent(YQProceduralSettlementLayoutRecord layout, Vector2 point, Vector2 direction)
    {
        // note: Measure the ray exit from a parcel core containing this street endpoint; endpoints outside cores need no plateau extension.
        foreach (var cell in layout.cells)
        {
            float x = point.x - cell.boundsCenter.x, z = point.y - cell.boundsCenter.z;
            float halfX = cell.boundsSize.x * .5f + 3f, halfZ = cell.boundsSize.z * .5f + 3f;
            if (Mathf.Abs(x) > halfX || Mathf.Abs(z) > halfZ) continue;
            float exitX = Mathf.Abs(direction.x) > .0001f ? (halfX - Mathf.Sign(direction.x) * x) / Mathf.Abs(direction.x) : float.PositiveInfinity;
            float exitZ = Mathf.Abs(direction.y) > .0001f ? (halfZ - Mathf.Sign(direction.y) * z) / Mathf.Abs(direction.y) : float.PositiveInfinity;
            float distance = Mathf.Min(exitX, exitZ);
            return float.IsInfinity(distance) ? 0f : Mathf.Max(0f, distance);
        }
        return 0f;
    }

    private static float NearestParcelHeight(YQProceduralSettlementLayoutRecord layout, float[] heights, Vector2 point)
    {
        // note: Distance to the supported footprint associates an approach with its parcel rather than the town center.
        float best = float.PositiveInfinity, height = heights[0];
        for (int i = 0; i < layout.cells.Count; i++)
        {
            var cell = layout.cells[i];
            float dx = Mathf.Max(0f, Mathf.Abs(point.x - cell.boundsCenter.x) - cell.boundsSize.x * .5f);
            float dz = Mathf.Max(0f, Mathf.Abs(point.y - cell.boundsCenter.z) - cell.boundsSize.z * .5f);
            float distance = dx * dx + dz * dz;
            if (distance < best) { best = distance; height = heights[i]; }
        }
        return height;
    }

    private static float SharedParcelHeight(YQProceduralSettlementLayoutRecord layout, float[] heights, Vector2 point)
    {
        // note: A junction belongs to all surrounding parcels. Nearest-only assignment gave adjoining roads different heights across a Voronoi boundary.
        float weightedHeight = 0f, total = 0f;
        for (int i = 0; i < layout.cells.Count; i++)
        {
            var cell = layout.cells[i];
            float dx = Mathf.Max(0f, Mathf.Abs(point.x - cell.boundsCenter.x) - cell.boundsSize.x * .5f - 3f);
            float dz = Mathf.Max(0f, Mathf.Abs(point.y - cell.boundsCenter.z) - cell.boundsSize.z * .5f - 3f);
            float squared = dx * dx + dz * dz;
            if (squared < .0001f) return heights[i];
            float strength = 1f / squared;
            weightedHeight += heights[i] * strength;
            total += strength;
        }
        return total > 0f ? weightedHeight / total : 0f;
    }

    internal static float ResolveConstructionEarthworkWeight(YQProceduralSettlementLayoutRecord layout,
        Vector2 worldOffset, float heading)
    {
        if (layout == null) return 0f;
        // note: Match the compiled site's heading and saved layout origin, including already-rotated block bounds.
        float angle = heading * Mathf.Deg2Rad;
        float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
        Vector2 point = new Vector2(cosine * worldOffset.x - sine * worldOffset.y + layout.origin.x,
            sine * worldOffset.x + cosine * worldOffset.y + layout.origin.z);
        float weight = 0f;
        if (layout.cells != null)
            foreach (var cell in layout.cells)
            {
                if (cell == null) continue;
                // note: Three metres of support outside each block protects its foundation; a local shoulder blends that shelf into untouched terrain.
                float dx = Mathf.Max(0f, Mathf.Abs(point.x - cell.boundsCenter.x) - cell.boundsSize.x * 0.5f - 3f);
                float dz = Mathf.Max(0f, Mathf.Abs(point.y - cell.boundsCenter.z) - cell.boundsSize.z * 0.5f - 3f);
                weight = Mathf.Max(weight, 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / 15f));
            }
        if (layout.streets != null)
            foreach (var street in layout.streets)
            {
                if (street == null) continue;
                // note: Grade the full street width and verge along the segment, including diagonal entrances and end caps.
                Vector2 start = new Vector2(street.start.x, street.start.z);
                Vector2 segment = new Vector2(street.end.x, street.end.z) - start;
                float lengthSquared = segment.sqrMagnitude;
                float t = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared) : 0f;
                float distance = Vector2.Distance(point, start + segment * t);
                float shoulderDistance = Mathf.Max(0f, distance - street.width * 0.5f - 1f);
                weight = Mathf.Max(weight, 1f - Mathf.SmoothStep(0f, 1f, shoulderDistance / 6f));
            }
        return weight;
    }

    private static float ResolveMinimumConstructionWorldHeight(
        string worldSeed,
        Terrain terrain,
        Vector3 center,
        float footprintRadius)
    {
        if (terrain == null || terrain.terrainData == null)
            return float.NegativeInfinity;

        float minimumHeight =
            float.NegativeInfinity;

        for (int basinIndex = 0;
             basinIndex < YQGeneratedWorldTerrain.MacroWaterBasinCount;
             basinIndex++)
        {
            if (!YQGeneratedWorldTerrain.TryGetMacroWaterBasin(
                    worldSeed,
                    terrain,
                    basinIndex,
                    out YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin) ||
                !basin.ContainsXZ(
                    center,
                    Mathf.Max(0f, footprintRadius)))
            {
                continue;
            }

            minimumHeight =
                Mathf.Max(
                    minimumHeight,
                    basin.WaterSurfaceY +
                        1.25f);
        }

        return minimumHeight;
    }

    private static float SampleTerrainDataWorldHeight(
        Terrain terrain,
        Vector3 worldPosition)
    {
        if (terrain == null || terrain.terrainData == null)
            return worldPosition.y;

        TerrainData data = terrain.terrainData;
        Vector3 origin = terrain.transform.position;
        Vector3 size = data.size;
        float normalizedX = Mathf.InverseLerp(
            origin.x,
            origin.x + size.x,
            worldPosition.x);
        float normalizedZ = Mathf.InverseLerp(
            origin.z,
            origin.z + size.z,
            worldPosition.z);

        // note: TerrainData exposes delayed height edits immediately, unlike physics which waits for the final SyncHeightmap transaction.
        return origin.y + data.GetInterpolatedHeight(normalizedX, normalizedZ);
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

                // note: These are world-distance thresholds, not interpolation output values.
                float pathWeight = 1f - YQGeneratedWorldEnvironment.SmoothThreshold(
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

        Terrain[] activeTerrains =
            Terrain.activeTerrains;

        for (int terrainIndex = 0;
             terrainIndex < activeTerrains.Length;
             terrainIndex++)
        {
            Terrain terrain =
                activeTerrains[terrainIndex];

            if (terrain == null ||
                terrain.terrainData == null ||
                !terrain.gameObject.activeInHierarchy ||
                !YQGeneratedWorldTerrain.TrySampleFootprintHeight(
                    terrain,
                    bounds,
                    out _,
                    out _,
                    out _))
            {
                continue;
            }

            float terrainEmbed =
                Mathf.Clamp(
                    bounds.size.y * 0.01f,
                    0.015f,
                    0.15f);

            // note: Every fallback or modular building now enters the same structural terrain authority as streamed and generated site assets.
            if (YQGeneratedWorldTerrain.TryPlaceGroundedObject(
                    instance,
                    terrain,
                    YQGeneratedWorldPlacementCategory.Structure,
                    terrainEmbed,
                    out _))
            {
                return;
            }
            // note: A building that failed full-footprint terrain support cannot bypass that failure through a single downward raycast onto scenery.
            if (instance.name.StartsWith("SettlementBuilding_", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Building foundation cannot fit the terrain without grading or relocation: " + instance.name);
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

        if (vey == null)
        {
            // note: Manual rebuilds may run before the bootstrap's inactive staging shell exists; create the curated Archivist actor before rejecting the origin.
            vey = YourQuestTutorialAutoBootstrap.EnsureRuntimeOriginActorForWorldGeneration();
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
            // note: Imported pivots are not the shrine's contact datum; centre its measured geometry over the summit that the terrain prepass actually prepared.
            if (TryGetRenderableBounds(statue, out Bounds authoredStatueBounds))
            {
                Vector3 summit = originAnchor + OriginGoddessSummitOffset;
                statue.transform.position += new Vector3(summit.x - authoredStatueBounds.center.x, 0f,
                    summit.z - authoredStatueBounds.center.z);
            }
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

        // note: The furnished origin streams and repairs 1,968 renderers cooperatively; a cold load can exceed 45 seconds while still completing successfully.
        float loadDeadline = Time.unscaledTime + 120f;

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

            // note: The authored socket is derived from the loaded hut bounds, keeping Vey visible and stable even when semantic circulation tags are absent from an imported cell.
            Vector3 veyPosition = new Vector3(
                Mathf.Lerp(witchHouseBounds.min.x, witchHouseBounds.max.x, OriginVeySocketNormalizedX),
                witchHouseBounds.min.y + 1.05f,
                Mathf.Lerp(witchHouseBounds.min.z, witchHouseBounds.max.z, OriginVeySocketNormalizedZ));

            vey.transform.position =
                veyPosition;
        }
        else
        {
            // note: G08 keeps the authored hut, arrival landing and exit route playable when the later resident system has not supplied an NPC yet.
            Debug.LogWarning(
                "[YQGeneratedWorldRuntimeBuilder] " +
                "Archivist Vey is unavailable; continuing with the NPC-free hut-first origin required by G08.");
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
        // note: Some imported shrine variants place loose benches, carts, and broken bridge pieces beside the compiled cells; cull those root-level source props before the cell pass.
        for (int rootIndex = mountainRoot.transform.childCount - 1; rootIndex >= 0; rootIndex--)
        {
            Transform rootChild = mountainRoot.transform.GetChild(rootIndex);
            if (rootChild == null || rootChild == statue || statue.IsChildOf(rootChild) ||
                rootChild.name.StartsWith("CompiledCell__", StringComparison.Ordinal))
                continue;

            string rootName = rootChild.name.ToLowerInvariant();
            bool rootShrineFeature = ContainsOriginCurationToken(
                rootName, "statue", "angel", "pedestal", "altar", "column",
                "pillar", "stair", "step", "brazier", "torch", "light",
                "particle", "vfx");
            bool rootRouteFeature = ContainsOriginCurationToken(
                rootName, "path", "road", "trail", "bridge", "plank");
            if (!rootShrineFeature && !rootRouteFeature &&
                TryGetRenderableBounds(rootChild.gameObject, out _))
            {
                // note: Keep the statue and named route/shrine assembly, but do not let generic source-scene props reappear around the landmark.
                rootChild.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(rootChild.gameObject);
                excluded++;
            }
        }
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
                    bool looseStageDressing = ContainsOriginCurationToken(
                        objectName,
                        "wagon", "cart", "chest", "barrel", "crate", "bench",
                        "chair", "table", "bed", "books", "potion", "shelf",
                        "shop", "cube");
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
                    // note: The origin landmark keeps only shrine/route dressing; loose carts and furniture from the source scene must not float across the summit as points of interest.
                    // note: Outside the authored approach, only explicitly named shrine features may remain; generic source-scene props were the white benches/carts that appeared around the statue.
                    keep = !unsupportedHydrology && !oversizedLooseRock &&
                        (!looseStageDressing || routeCell || shrineFeature) &&
                        (coherentRouteObject ||
                        (!vegetation && shrineFeature && horizontalDistance <= 30f &&
                         Mathf.Abs(bounds.center.y - statueBounds.center.y) <= 30f &&
                         largestDimension <= 48f));
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

        float statueStructuralBottom;
        if (!YQGeneratedWorldTerrain.TryGetStableContactGeometry(
            statue.gameObject,
            out statueBounds,
            out statueStructuralBottom))
        {
            // note: Some imported shrine variants expose only renderer geometry; their measured bounds are still a deterministic contact datum.
            if (!TryGetRenderableBounds(statue.gameObject, out statueBounds))
                return false;
            statueStructuralBottom = statueBounds.min.y;
            Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Shrine stable-contact subset unavailable; using renderer bounds for deterministic grounding.");
        }

        float terrainHeight;
        if (!YQGeneratedWorldTerrain.TrySampleFootprintHeight(
            terrain,
            statueBounds,
            out terrainHeight,
            out _,
            out _))
        {
            // note: A valid in-bounds renderer can outlive the footprint sampler's support quorum; the canonical terrain sample still gives the same playable contact plane.
            terrainHeight = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, statueBounds.center);
            Debug.LogWarning("[YQGeneratedWorldRuntimeBuilder] Shrine footprint quorum unavailable; using canonical terrain height at the measured center.");
        }

        // note: The retained Goddess shrine settles by its structural footprint, so its pedestal cannot hover when the imported statue pivot is offset.
        float verticalCorrection =
            terrainHeight -
            statueStructuralBottom -
            0.015f;

        if (float.IsNaN(verticalCorrection) ||
            float.IsInfinity(verticalCorrection) ||
            Mathf.Abs(verticalCorrection) > 500f)
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

        // note: A restrained directional key preserves authored material response and contact shadows instead of washing the generated world into a flat white skybox.
        light.intensity =
            0.72f;

        light.shadows =
            LightShadows.Soft;

        light.shadowStrength =
            0.52f;

        light.transform.rotation =
            Quaternion.Euler(
                48f,
                -35f,
                0f);
    }

    private static Vector3 ResolveDryPlayerHorizontalPosition(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
        Vector3 requested,
        bool preserveContinuationPosition)
    {
        if (terrain == null || terrain.terrainData == null)
            return requested;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        bool outsideAuthoredTerrain = requested.x < origin.x || requested.x > origin.x + size.x ||
            requested.z < origin.z || requested.z > origin.z + size.z;
        if (preserveContinuationPosition && outsideAuthoredTerrain)
        {
            // note: Ordinary loads preserve a saved continuation coordinate; the player-following streamer owns the destination cell and will publish its collider after this placement.
            return requested;
        }
        const float spawnMargin = 4f;
        // note: New-origin placement gets a small dry spawn inset, while ordinary continuation loads preserve the saved horizontal coordinate for the streamer.
        if (!preserveContinuationPosition)
        {
            requested.x = Mathf.Clamp(
                requested.x,
                origin.x + spawnMargin,
                origin.x + size.x - spawnMargin);
            requested.z = Mathf.Clamp(
                requested.z,
                origin.z + spawnMargin,
                origin.z + size.z - spawnMargin);
        }
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[] basins =
            new YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[
                YQGeneratedWorldTerrain.MacroWaterBasinCount];
        int basinCount = 0;

        if (plan != null)
        {
            for (int basinIndex = 0;
                 basinIndex < YQGeneratedWorldTerrain.MacroWaterBasinCount;
                 basinIndex++)
            {
                if (YQGeneratedWorldTerrain.TryGetMacroWaterBasin(
                        plan.worldSeed,
                        terrain,
                        basinIndex,
                        out YQGeneratedWorldTerrain.MacroWaterBasinDescriptor basin))
                {
                    basins[basinCount++] = basin;
                }
            }
        }

        if (!IsInsidePlayerWaterReserve(basins, basinCount, requested))
            return requested;

        float phase = Deterministic01(
            (plan != null ? plan.worldSeed : string.Empty) +
            "|player_dry_migration") *
            Mathf.PI * 2f;

        for (float radius = 12f; radius <= 180f; radius += 12f)
        {
            for (int directionIndex = 0;
                 directionIndex < 16;
                 directionIndex++)
            {
                float angle = phase +
                    directionIndex / 16f * Mathf.PI * 2f;
                Vector3 candidate = new Vector3(
                    requested.x + Mathf.Cos(angle) * radius,
                    requested.y,
                    requested.z + Mathf.Sin(angle) * radius);

                if (candidate.x < origin.x + spawnMargin ||
                    candidate.x > origin.x + size.x - spawnMargin ||
                    candidate.z < origin.z + spawnMargin ||
                    candidate.z > origin.z + size.z - spawnMargin ||
                    IsInsidePlayerWaterReserve(basins, basinCount, candidate))
                {
                    continue;
                }

                float normalizedX = Mathf.InverseLerp(
                    origin.x,
                    origin.x + size.x,
                    candidate.x);
                float normalizedZ = Mathf.InverseLerp(
                    origin.z,
                    origin.z + size.z,
                    candidate.z);

                if (terrain.terrainData.GetSteepness(
                        normalizedX,
                        normalizedZ) > 34f)
                {
                    continue;
                }

                // note: A save whose old horizontal location became lake water migrates to the nearest deterministic dry, walkable ring instead of spawning underwater.
                return candidate;
            }
        }

        Vector3 fallback = YQGeneratedWorldLayout.GetVeyOriginAnchor();
        fallback.x = Mathf.Clamp(
            fallback.x,
            origin.x + spawnMargin,
            origin.x + size.x - spawnMargin);
        fallback.z = Mathf.Clamp(
            fallback.z,
            origin.z + spawnMargin,
            origin.z + size.z - spawnMargin);
        return fallback;
    }

    private static bool IsInsidePlayerWaterReserve(
        YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[] basins,
        int basinCount,
        Vector3 candidate)
    {
        for (int basinIndex = 0;
             basins != null && basinIndex < basinCount;
             basinIndex++)
        {
            if (basins[basinIndex].ContainsXZ(candidate, 7f))
            {
                return true;
            }
        }

        return false;
    }

    private static void PlacePlayerAtGeneratedOrigin(
        Terrain terrain,
        GeneratedWorldPlanRecord plan,
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
            // note: Initial-generation positioning can run before the player enables its motor; resolve one unique tagged or exact-name root without selecting arbitrary scene props.
            Transform[] candidates = FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            GameObject taggedCandidate = null;
            bool ambiguousCandidate = false;
            for (int index = 0; index < candidates.Length; index++)
            {
                Transform candidate = candidates[index];
                if (candidate == null || candidate.parent != null)
                    continue;

                bool identityMatch = false;
                try
                {
                    identityMatch = candidate.gameObject.CompareTag("Player");
                }
                catch
                {
                }
                identityMatch |= string.Equals(candidate.name, "Player",
                    StringComparison.OrdinalIgnoreCase);
                if (!identityMatch ||
                    candidate.GetComponentInChildren<YQInvestorPlayerMotor>(true) == null)
                    continue;

                if (taggedCandidate != null && taggedCandidate != candidate.gameObject)
                {
                    ambiguousCandidate = true;
                    break;
                }

                taggedCandidate = candidate.gameObject;
            }

            if (!ambiguousCandidate)
                player = taggedCandidate;
        }

        if (player == null)
        {
            // note: A manual rebuild or verifier marker may arrive before startup has linked the player; recover through the bootstrap's authoritative singleton path.
            player = YourQuestTutorialAutoBootstrap.EnsureRuntimePlayerForWorldGeneration();
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
            spawn = ResolveDryPlayerHorizontalPosition(
                terrain,
                plan,
                spawn,
                !useGeneratedOrigin);
            bool outsideAuthoredTerrain = spawn.x < terrain.transform.position.x ||
                spawn.x > terrain.transform.position.x + terrain.terrainData.size.x ||
                spawn.z < terrain.transform.position.z ||
                spawn.z > terrain.transform.position.z + terrain.terrainData.size.z;
            float terrainSafeHeight;
            if (!outsideAuthoredTerrain)
            {
                terrainSafeHeight = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, spawn) + 0.45f;
            }
            else if (plan != null && YQGeneratedWorldTerrain.TryCreateV2HeightSampler(plan, out YQGeneratedWorldTerrain.V2HeightSampler continuationSampler, out _))
            {
                // note: Before the streamer is attached, derive the saved continuation altitude from the same world-space authority used by streamed TerrainData.
                // note: Saved continuation placement uses the same accepted feature authority and canonical cell size as the player-following streamer.
                YQContinuousWorldCellAuthority continuationAuthority = new YQContinuousWorldCellAuthority(plan.worldSeed, terrain, continuationSampler, plan, 128f);
                terrainSafeHeight = terrain.transform.position.y + continuationAuthority.SampleHeightNormalized(spawn.x, spawn.z) * terrain.terrainData.size.y + 0.45f;
            }
            else
            {
                terrainSafeHeight = spawn.y;
            }

            // note: Ordinary saves migrate onto the current deterministic surface instead of retaining a stale pre-migration altitude; new-origin authored clearance may still be higher.
            spawn.y = useGeneratedOrigin
                ? Mathf.Max(spawn.y, terrainSafeHeight)
                : terrainSafeHeight;
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

        // note: Catastrophic fall recovery records the validated spawn but never substitutes for the mandatory collider validation pass.
        YQGeneratedWorldPlayerFallSafety.EnsureInstalled(
            player,
            terrain);

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

        // note: Reload the accepted geometry decision instead of rerolling a different variant during Continue.
        if (settlement.proceduralLayout != null && !string.IsNullOrWhiteSpace(settlement.proceduralLayout.seed))
            return settlement.proceduralLayout.seed;

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

    private static float ResolveSettlementHeading(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement)
    {
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
        {
            return ResolveSpatialSiteHeading(
                plan,
                settlement != null
                    ? settlement.settlementId
                    : string.Empty,
                0f);
        }

        if (settlement != null &&
            YQGeneratedWorldSpatialPlanner.TryGetLocation(
                plan,
                settlement.settlementId,
                out GeneratedSpatialLocationRecord location))
        {
            // note: Persisted spatial authority owns settlement orientation so roads, gates, and regenerated saves remain deterministic.
            return Mathf.Repeat(location.entranceHeadingDegrees, 360f);
        }

        return DeterministicQuarterTurn(
            SettlementSeed(settlement) + ":legacy_orientation");
    }

    private static float ResolveSpatialSiteHeading(
        GeneratedWorldPlanRecord plan,
        string semanticId,
        float v1Fallback)
    {
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan))
            return v1Fallback;

        if (YQSpatialMaterializationResolverV2.TryGetPrepared(
                plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out string failure) &&
            prepared.TryGetSiteBySemanticId(
                semanticId,
                out YQSpatialMaterializationSiteV2 site))
        {
            // note: Site façades and authored entrances face the accepted route frontage rather than a separately hashed quarter-turn.
            return site.headingDegrees;
        }

        Debug.LogError(
            "[YQGeneratedWorldRuntimeBuilder] Missing V2 site heading for " +
            semanticId + ": " + failure);
        return 0f;
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

    private static bool IsSettlementInsideGeneratedTerrain(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        Terrain terrain)
    {
        if (plan == null || settlement == null || terrain == null ||
            terrain.terrainData == null)
            return false;

        Vector3 anchor = YQGeneratedWorldLayout.GetSettlementAnchor(
            plan,
            settlement,
            terrain);
        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        // note: Leave the authored origin terrain a conservative border so a reviewed footprint is never partially built at the edge and then mistaken for a streamed owner.
        return IsWorldPointInsideGeneratedTerrain(
            anchor,
            terrain,
            160f);
    }

    private static bool IsEncampmentInsideGeneratedTerrain(
        GeneratedWorldPlanRecord plan,
        GeneratedEncampmentRecord encampment,
        Terrain terrain)
    {
        if (plan == null || encampment == null || terrain == null ||
            terrain.terrainData == null)
            return false;

        return IsWorldPointInsideGeneratedTerrain(
            YQGeneratedWorldLayout.GetEncampmentAnchor(
                plan,
                encampment,
                terrain),
            terrain,
            96f);
    }

    private static bool IsWorldPointInsideGeneratedTerrain(
        Vector3 anchor,
        Terrain terrain,
        float margin)
    {
        if (terrain == null || terrain.terrainData == null)
            return false;

        Vector3 terrainPosition = terrain.transform.position;
        Vector3 terrainSize = terrain.terrainData.size;
        return anchor.x >= terrainPosition.x + margin &&
            anchor.x <= terrainPosition.x + terrainSize.x - margin &&
            anchor.z >= terrainPosition.z + margin &&
            anchor.z <= terrainPosition.z + terrainSize.z - margin;
    }

    private int ExpectedStartupSettlementCount(GeneratedWorldPlanRecord plan)
    {
        // note: All startup/rebuild gates share the construction footprint without inflating the actual built count.
        return YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(plan)
            ? CountInitialTerrainSettlements(plan, _generatedTerrain)
            : plan?.settlements?.Count ?? 0;
    }

    private static int CountInitialTerrainSettlements(
        GeneratedWorldPlanRecord plan,
        Terrain terrain)
    {
        if (plan == null || plan.settlements == null)
            return 0;

        int count = 0;
        for (int index = 0; index < plan.settlements.Count; index++)
        {
            if (IsSettlementInsideGeneratedTerrain(
                    plan,
                    plan.settlements[index],
                    terrain))
                count++;
        }

        return count;
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
        LayoutFamily family = ResolveLayoutFamily(plan, settlement, layoutRuleProfile);
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
        LayoutFamily family = ResolveLayoutFamily(plan, settlement, layoutRuleProfile);
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

        LayoutFamily family = ResolveLayoutFamily(plan, settlement, layoutRuleProfile);
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

        LayoutFamily family = ResolveLayoutFamily(plan, settlement, layoutRuleProfile);
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

        LayoutFamily family = ResolveLayoutFamily(plan, settlement, layoutRuleProfile);
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

    private static LayoutFamily ResolveLayoutFamily(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement,
        string layoutRuleProfile)
    {
        string profile = (layoutRuleProfile ?? string.Empty).Trim().ToLowerInvariant();
        if (settlement != null &&
            YQGeneratedWorldSpatialPlanner.TryGetLocation(
                plan,
                settlement.settlementId,
                out GeneratedSpatialLocationRecord spatialLocation))
        {
            // note: Settlement topology follows its persisted causal archetype; asset-pack profiles refine it but no longer define it alone.
            string archetype = (spatialLocation.structuralArchetype ?? string.Empty).ToLowerInvariant();
            if (Contains(archetype, "dense", "street_block", "transit", "urban"))
                return LayoutFamily.StreetGrid;
            if (Contains(archetype, "defensible", "courtyard", "mining", "barricaded"))
                return LayoutFamily.Courtyard;
        }

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
// note: Automatic refresh verification touch for the semantic chunk integration.
