using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// note: These statuses prevent editor-only, blocked, failed, and runtime-passing checks from sharing one misleading boolean.
public enum YQBaselineResultStatus
{
    PASS,
    FAIL,
    BLOCKED,
    NOT_YET_TESTABLE
}

// note: Evidence levels describe what was actually observed so compilation and source inspection cannot masquerade as Play Mode proof.
public enum YQBaselineEvidenceLevel
{
    STATIC_VERIFIED,
    COMPILE_VERIFIED,
    EDITOR_TOOL_VERIFIED,
    RUNTIME_BEHAVIOR_VERIFIED,
    NOT_VERIFIED
}

// note: Each receipt is a small structured handoff record with a reproducible owner and an exact observation boundary.
[Serializable]
public sealed class YQBaselineResultRecord
{
    public string checkId;
    public string category;
    public YQBaselineResultStatus status;
    public YQBaselineEvidenceLevel evidence;
    public string expected;
    public string actual;
    public string reproduction;
    public string owner;

    // note: The log form is stable enough for Unity logs and compact bug-report extraction.
    public string ToLogLine()
    {
        return checkId + " status=" + status +
            " evidence=" + evidence +
            " category=" + category +
            " expected=" + expected +
            " actual=" + actual +
            " owner=" + owner;
    }
}

// note: This contract gives the beta baseline one stable place to report build, save, generation, and streaming identity.
public static class YQProductionBaselineContract
{
    // note: The build marker is intentionally human-readable so support logs can be matched to the architecture report.
    public const string BuildVersion = "yourquest-beta-g01-2026.09.16";

    // note: The source marker identifies the observed checkout without pretending the working tree is a clean release commit.
    public const string SourceIdentity = "HEAD=49a10bc;working-tree-uncommitted";

    // note: PlayerState and WorldState currently share the same persisted schema revision and are reported together.
    public const string SaveSchemaVersion = "player=6;world=6";

    // note: These versions are derived from the live production algorithms instead of being duplicated string literals.
    public static string WorldGenerationVersion =>
        YQGeneratedWorldSpatialPlanner.GenerationVersion + "|" +
        YQGeneratedWorldTerrain.TerrainGenerationVersion;

    // note: Streamed continuation records and edge contracts are the semantic chunk identity used by runtime traversal.
    public static string SemanticChunkSchemaVersion =>
        YQContinuousWorldCellAuthority.SchemaVersion + "|" +
        YQContinuousWorldCellAuthority.EdgeContractVersion;

    // note: This is the only production materialization path selected by the architecture boundary.
    public static string ArchitectureStatus =>
        (YQWorldGenerationArchitecture.UsesCompiledWorld ? "compiled-world" : "legacy-scatter") +
        ";spatial=" + YQWorldGenerationArchitecture.ActiveSpatialPlanningMode;
}

[DisallowMultipleComponent]
public sealed class YQProductionBaselineDiagnostics : MonoBehaviour
{
    // note: One runtime object owns baseline reporting so duplicate scene/bootstrap copies become visible immediately.
    public static YQProductionBaselineDiagnostics Instance { get; private set; }

    // note: A half-second cadence keeps diagnostics useful without adding scene-wide searches to the frame hot path.
    [SerializeField] private float reportIntervalSeconds = 0.5f;

    // note: Cached references keep the report observer from repeatedly discovering production services.
    private PlayerStateManager _playerStateManager;
    private WorldStateManager _worldStateManager;
    private YQProfileSaveSystem _profileSaveSystem;
    private YQGeneratedWorldRuntimeBuilder _worldBuilder;
    private YQPlayerFollowingSemanticChunkStreamer _streamer;
    private LLMClient _llmClient;
    private ProgressionThinkCycle _progressionThinkCycle;
    private DirectorThinkCycle _directorThinkCycle;
    private DialogueThinkService _dialogueThinkService;
    private YQQuestCompletionDirector _questCompletionDirector;
    private string _lastSignature = string.Empty;
    private float _nextReportTime;
    private Vector2Int _firstObservedChunk = new Vector2Int(int.MinValue, int.MinValue);
    private bool _hasTraversed;
    private string _latestReport = "not-observed";
    private RuntimeSnapshot _latestSnapshot;
    private bool _ordinaryProfileObserved;

    // note: The first live owner wins and every later duplicate is rejected instead of silently synchronizing two observers.
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[YQProductionBaseline] Duplicate diagnostics owner rejected: " + name);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        _nextReportTime = 0f;
    }

    // note: Reports are sampled periodically so the baseline stays readable while the runtime remains cheap.
    private void Update()
    {
        if (Time.unscaledTime < _nextReportTime)
            return;

        _nextReportTime = Time.unscaledTime + Mathf.Max(0.1f, reportIntervalSeconds);
        CaptureAndReport(false);
    }

    // note: Editor regression and support tooling can request one immediate report without changing runtime state.
    public string CaptureAndReport(bool forceLog)
    {
        CacheReferences();
        RuntimeSnapshot snapshot = CaptureSnapshot();
        _latestReport = snapshot.ToLogLine();
        string signature = snapshot.ToSignature();
        if (forceLog || !string.Equals(signature, _lastSignature, StringComparison.Ordinal))
        {
            _lastSignature = signature;
            Debug.Log("[YQProductionBaseline] " + _latestReport);
        }

        return _latestReport;
    }

    // note: This accessor exposes the last captured evidence to the top-level regression entry point.
    public string LatestReport => _latestReport;

    // note: The editor runner consumes separate records so skipped runtime work remains explicitly not yet testable.
    public IReadOnlyList<YQBaselineResultRecord> CaptureResultRecords()
    {
        CacheReferences();
        RuntimeSnapshot snapshot = CaptureSnapshot();
        List<YQBaselineResultRecord> records = new List<YQBaselineResultRecord>(5);
        records.Add(CreateResult(
            "live.snapshot",
            "live-snapshots",
            YQBaselineResultStatus.PASS,
            YQBaselineEvidenceLevel.RUNTIME_BEHAVIOR_VERIFIED,
            "A read-only runtime snapshot is captured.",
            snapshot.ToLogLine(),
            "PlaySafe startup with diagnostics owner present.",
            "G01"));

        bool canonical = string.Equals(
            snapshot.profileId,
            YQBetaDevelopmentFixture.CanonicalProfileId,
            StringComparison.OrdinalIgnoreCase);
        bool startupReady = snapshot.servicesReady && snapshot.profileReady &&
            snapshot.originReady && snapshot.planReady && snapshot.spatialReady &&
            snapshot.materialized && snapshot.playerReady && snapshot.npcReady &&
            snapshot.terrainReady && snapshot.streamingReady;
        YQBaselineResultStatus startupStatus = startupReady
            ? YQBaselineResultStatus.PASS
            : YQBaselineResultStatus.BLOCKED;
        records.Add(CreateResult(
            canonical ? "fixture.startup" : "ordinary.startup",
            canonical ? "fixture-startup" : "ordinary-startup",
            canonical
                ? startupStatus
                : (_ordinaryProfileObserved
                    ? startupStatus
                    : YQBaselineResultStatus.NOT_YET_TESTABLE),
            YQBaselineEvidenceLevel.RUNTIME_BEHAVIOR_VERIFIED,
            "Title → profile → origin → world reaches the selected profile's accepted playable boundary.",
            snapshot.ToLogLine(),
            "Enter PlaySafe, use the title/profile flow, and wait for materialization.",
            canonical ? "G01 fixture / G05-G10 downstream failures" : "G01 / G10"));

        YQBaselineResultStatus traversalStatus = snapshot.traversed &&
            snapshot.streamingReady && snapshot.terrainReady
            ? YQBaselineResultStatus.PASS
            : snapshot.failedChunkCount > 0
                ? YQBaselineResultStatus.FAIL
                : YQBaselineResultStatus.NOT_YET_TESTABLE;
        records.Add(CreateResult(
            "live.traversal",
            "traversal",
            traversalStatus,
            YQBaselineEvidenceLevel.RUNTIME_BEHAVIOR_VERIFIED,
            "Cross an authored and a streamed cell boundary with no failed current-cell readiness.",
            "traversed=" + snapshot.traversed + " current=" + snapshot.currentChunk +
                " failedChunks=" + snapshot.failedChunkCount + " terrain=" + snapshot.terrainReady,
            "Move across both designated cell boundaries in the active world.",
            "G06-G08"));

        YQBaselineResultStatus reloadStatus = snapshot.successfulLoadCount >= 2 &&
            string.Equals(snapshot.lastLoadedProfileId, snapshot.profileId, StringComparison.OrdinalIgnoreCase)
            ? YQBaselineResultStatus.PASS
            : YQBaselineResultStatus.NOT_YET_TESTABLE;
        records.Add(CreateResult(
            "live.reload",
            "reload",
            reloadStatus,
            YQBaselineEvidenceLevel.RUNTIME_BEHAVIOR_VERIFIED,
            "The same profile is loaded successfully at least twice with the same identity.",
            "loads=" + snapshot.successfulLoadCount + " lastLoaded=" + snapshot.lastLoadedProfileId +
                " profile=" + snapshot.profileId,
            "Save, return to title, Continue the same profile, then capture again.",
            "G02"));

        return records;
    }

    // note: This factory centralizes receipt wording so every check exposes its status, evidence, reproduction, and owner.
    private static YQBaselineResultRecord CreateResult(
        string checkId,
        string category,
        YQBaselineResultStatus status,
        YQBaselineEvidenceLevel evidence,
        string expected,
        string actual,
        string reproduction,
        string owner)
    {
        return new YQBaselineResultRecord
        {
            checkId = checkId,
            category = category,
            status = status,
            evidence = evidence,
            expected = expected,
            actual = actual,
            reproduction = reproduction,
            owner = owner
        };
    }

    // note: Runtime validation is intentionally observational; it never regenerates a save or changes active authority.
    public bool TryValidateRuntime(out string failure)
    {
        CacheReferences();
        RuntimeSnapshot snapshot = CaptureSnapshot();
        if (!snapshot.servicesReady) { failure = "production services are not bootstrapped"; return false; }
        if (!snapshot.profileReady) { failure = "no active profile owns the loaded PlayerState"; return false; }
        if (!snapshot.originReady) { failure = "accepted origin is not complete"; return false; }
        if (!snapshot.planReady) { failure = "persisted generated world plan is missing a seed"; return false; }
        if (!snapshot.spatialReady) { failure = "active spatial authority is unresolved: " + snapshot.spatialReason; return false; }
        if (!snapshot.materialized) { failure = "current accepted world is not materialized"; return false; }
        if (!snapshot.playerReady) { failure = "authoritative player motor is not ready"; return false; }
        if (!snapshot.npcReady) { failure = "NPC population is not present"; return false; }
        if (!snapshot.terrainReady) { failure = "current-cell terrain is not ready: " + snapshot.terrainReason; return false; }
        if (!snapshot.streamingReady) { failure = "streaming frontier is not ready"; return false; }

        failure = string.Empty;
        return true;
    }

    // note: References are refreshed only when missing so scene transitions do not create a per-frame FindObjectsByType cost.
    private void CacheReferences()
    {
        if (_playerStateManager == null)
            _playerStateManager = PlayerStateManager.Instance;
        if (_worldStateManager == null)
            _worldStateManager = WorldStateManager.Instance;
        if (_profileSaveSystem == null)
            _profileSaveSystem = YQProfileSaveSystem.Instance;
        if (_worldBuilder == null)
            _worldBuilder = YQGeneratedWorldRuntimeBuilder.Instance;
        if (_streamer == null)
            _streamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (_llmClient == null)
            _llmClient = LLMClient.Instance;
        if (_progressionThinkCycle == null)
            _progressionThinkCycle = FindFirstObjectByType<ProgressionThinkCycle>();
        if (_directorThinkCycle == null)
            _directorThinkCycle = FindFirstObjectByType<DirectorThinkCycle>();
        if (_dialogueThinkService == null)
            _dialogueThinkService = DialogueThinkService.Instance;
        if (_questCompletionDirector == null)
            _questCompletionDirector = FindFirstObjectByType<YQQuestCompletionDirector>();
    }

    // note: The snapshot reads only existing authority and presentation objects; it does not create fallback gameplay state.
    private RuntimeSnapshot CaptureSnapshot()
    {
        PlayerState player = _playerStateManager != null ? _playerStateManager.state : null;
        WorldState world = _worldStateManager != null ? _worldStateManager.State : null;
        GeneratedWorldPlanRecord plan = world != null ? world.generatedWorldPlan : null;
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;

        string spatialReason = string.Empty;
        YQSpatialPlanAuthority authority = YQSpatialPlanAuthority.None;
        bool spatialReady = plan != null &&
            YQWorldGenerationArchitecture.TryResolveRuntimeAuthority(
                plan, out authority, out spatialReason);

        bool terrainReady = false;
        string terrainReason = "streamer missing";
        bool streamingReady = false;
        // note: Keep the visual publication gate visible beside collision readiness so baseline receipts cannot certify terrain-only continuity.
        bool visualReady = false;
        string visualReason = "streamer missing";
        int visualRadius = 0;
        int visualPrewarmRadius = 0;
        int visualRetentionRadius = 0;
        float visualDistanceMeters = 0f;
        float visualFarClipMeters = 0f;
        Vector2Int currentChunk = new Vector2Int(int.MinValue, int.MinValue);
        int failedChunkCount = 0;
        if (_streamer != null)
        {
            terrainReady = _streamer.TryValidateCurrentTerrain(out terrainReason);
            visualReady = _streamer.TryValidateVisualCoverage(out visualReason);
            visualRadius = _streamer.GuaranteedVisualRadius;
            visualPrewarmRadius = _streamer.VisualPrewarmRadius;
            visualRetentionRadius = _streamer.VisualRetentionRadius;
            visualDistanceMeters = _streamer.GuaranteedVisualDistanceMeters;
            visualFarClipMeters = _streamer.QualificationCameraFarClipMeters;
            currentChunk = _streamer.CurrentChunk;
            failedChunkCount = _streamer.FailedChunkCount;
            streamingReady = currentChunk.x != int.MinValue && failedChunkCount == 0;
            if (_firstObservedChunk.x == int.MinValue)
                _firstObservedChunk = currentChunk;
            else if (currentChunk != _firstObservedChunk)
                _hasTraversed = true;
        }

        int activeNpcCount = FindObjectsByType<NpcDialogueAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
        int expectedNpcCount = plan != null && plan.generatedNpcs != null ? plan.generatedNpcs.Count : 0;
        bool npcReady = activeNpcCount > 0 || expectedNpcCount == 0;

        if (_profileSaveSystem != null &&
            !string.IsNullOrWhiteSpace(_profileSaveSystem.ActiveProfileId) &&
            !string.Equals(_profileSaveSystem.ActiveProfileId,
                YQBetaDevelopmentFixture.CanonicalProfileId,
                StringComparison.OrdinalIgnoreCase))
            _ordinaryProfileObserved = true;

        int activeServiceCount = 0;
        if (_playerStateManager != null) activeServiceCount++;
        if (_worldStateManager != null) activeServiceCount++;
        if (_profileSaveSystem != null) activeServiceCount++;
        if (_llmClient != null) activeServiceCount++;
        if (_progressionThinkCycle != null) activeServiceCount++;
        if (_directorThinkCycle != null) activeServiceCount++;
        if (_dialogueThinkService != null) activeServiceCount++;
        if (_questCompletionDirector != null) activeServiceCount++;
        if (YQOriginGenerationService.Instance != null) activeServiceCount++;
        if (YQWorldGenerationService.Instance != null) activeServiceCount++;

        string v1Artifact = plan != null && plan.spatialPlan != null
            ? plan.spatialPlan.schemaVersion + ":" + plan.spatialPlan.generationVersion + ":" + plan.spatialPlan.semanticFingerprint
            : "none";
        string v2Artifact = plan != null && plan.spatialPlanV2 != null
            ? plan.spatialPlanV2.schemaVersion + ":" + plan.spatialPlanV2.generationVersion + ":" + plan.spatialPlanV2.contentHash
            : "none";
        string artifactIdentity = spatialReady
            ? authority + ";v1=" + v1Artifact + ";v2=" + v2Artifact
            : "unresolved;v1=" + v1Artifact + ";v2=" + v2Artifact;
        string modelMode = _llmClient == null
            ? "unavailable"
            : "runtime=" + _llmClient.RuntimeState +
              ";model=" + (_llmClient.model ?? "<empty>") +
              ";pending=" + _llmClient.PendingRequestCount +
              ";lastFailure=" + _llmClient.LastRequestFailed;
        CharacterController playerController = motor != null ? motor.GetComponent<CharacterController>() : null;
        YQPlayerCollisionContract collision = null;
        if (motor != null && playerController != null)
            YQPlayerContractCapture.TryCapture(motor.gameObject, motor.walkSpeed, motor.sprintSpeed, out collision);

        return new RuntimeSnapshot
        {
            servicesReady = _playerStateManager != null && _worldStateManager != null &&
                _profileSaveSystem != null && YQOriginGenerationService.Instance != null &&
                YQWorldGenerationService.Instance != null,
            profileReady = player != null && !string.IsNullOrWhiteSpace(player.playerId) &&
                _profileSaveSystem != null && !string.IsNullOrWhiteSpace(_profileSaveSystem.ActiveProfileId) &&
                string.Equals(player.playerId, _profileSaveSystem.ActiveProfileId, StringComparison.OrdinalIgnoreCase),
            originReady = GeneratedRpgContentService.HasCompletedOrigin(player),
            planReady = plan != null && !string.IsNullOrWhiteSpace(plan.worldSeed),
            spatialReady = spatialReady,
            spatialAuthority = spatialReady ? authority.ToString() : "None",
            spatialReason = spatialReason,
            materialized = _worldBuilder != null && _worldBuilder.HasMaterializedCurrentWorld,
            playerReady = motor != null && motor.IsAuthoritative,
            npcReady = npcReady,
            activeNpcCount = activeNpcCount,
            expectedNpcCount = expectedNpcCount,
            terrainReady = terrainReady,
            terrainReason = terrainReason,
            visualReady = visualReady,
            visualReason = visualReason,
            visualRadius = visualRadius,
            visualPrewarmRadius = visualPrewarmRadius,
            visualRetentionRadius = visualRetentionRadius,
            visualDistanceMeters = visualDistanceMeters,
            visualFarClipMeters = visualFarClipMeters,
            streamingReady = streamingReady,
            traversed = _hasTraversed,
            currentChunk = currentChunk,
            failedChunkCount = failedChunkCount,
            selectedArtifact = artifactIdentity,
            modelMode = modelMode,
            activeServiceCount = activeServiceCount,
            requiredServiceCount = 10,
            successfulLoadCount = _profileSaveSystem != null ? _profileSaveSystem.SuccessfulLoadCount : 0,
            successfulSaveCount = _profileSaveSystem != null ? _profileSaveSystem.SuccessfulSaveCount : 0,
            lastLoadedProfileId = _profileSaveSystem != null ? _profileSaveSystem.LastLoadedProfileId : "<none>",
            lastSavedProfileId = _profileSaveSystem != null ? _profileSaveSystem.LastSavedProfileId : "<none>",
            profileId = _profileSaveSystem != null ? _profileSaveSystem.ActiveProfileId : "<none>",
            worldSeed = plan != null ? plan.worldSeed : "<none>",
            architecture = YQProductionBaselineContract.ArchitectureStatus,
            activeMaterialization = YQWorldGenerationArchitecture.ActiveMaterializationPath.ToString(),
            buildVersion = YQProductionBaselineContract.BuildVersion,
            saveSchema = YQProductionBaselineContract.SaveSchemaVersion,
            worldGeneration = YQProductionBaselineContract.WorldGenerationVersion,
            semanticChunkSchema = YQProductionBaselineContract.SemanticChunkSchemaVersion
            , playerStateRevision = player != null ? player.stateRevision : 0
            , worldStateRevision = world != null ? world.stateRevision : 0
            , worldId = world != null && world.worldIdentity != null ? world.worldIdentity.worldId : string.Empty
            , activeCommitId = _profileSaveSystem != null ? _profileSaveSystem.ActiveCommitId : string.Empty
            , requestEpoch = YQServiceLifecycle.RequestEpoch
            , playerCollision = collision
        };
    }

    // note: The value object keeps report formatting deterministic for logs, tests, and support captures.
    private sealed class RuntimeSnapshot
    {
        public bool servicesReady;
        public bool profileReady;
        public bool originReady;
        public bool planReady;
        public bool spatialReady;
        public string spatialAuthority;
        public string spatialReason;
        public bool materialized;
        public bool playerReady;
        public bool npcReady;
        public int activeNpcCount;
        public int expectedNpcCount;
        public bool terrainReady;
        public string terrainReason;
        public bool visualReady;
        public string visualReason;
        public int visualRadius;
        public int visualPrewarmRadius;
        public int visualRetentionRadius;
        public float visualDistanceMeters;
        public float visualFarClipMeters;
        public bool streamingReady;
        public bool traversed;
        public Vector2Int currentChunk;
        public int failedChunkCount;
        public string selectedArtifact;
        public string modelMode;
        public int activeServiceCount;
        public int requiredServiceCount;
        public int successfulLoadCount;
        public int successfulSaveCount;
        public string lastLoadedProfileId;
        public string lastSavedProfileId;
        public string profileId;
        public string worldSeed;
        public string architecture;
        public string activeMaterialization;
        public string buildVersion;
        public string saveSchema;
        public string worldGeneration;
        public string semanticChunkSchema;
        public long playerStateRevision;
        public long worldStateRevision;
        public string worldId;
        public string activeCommitId;
        public int requestEpoch;
        public YQPlayerCollisionContract playerCollision;

        // note: The log line is intentionally compact enough to paste into a bug report without losing authority identity.
        public string ToLogLine()
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("build=").Append(buildVersion);
            builder.Append(" save=").Append(saveSchema);
            builder.Append(" worldGen=").Append(worldGeneration);
            builder.Append(" semanticChunk=").Append(semanticChunkSchema);
            builder.Append(" source=").Append(YQProductionBaselineContract.SourceIdentity);
            builder.Append(" architecture=").Append(architecture);
            builder.Append(" materialization=").Append(activeMaterialization);
            builder.Append(" spatialAuthority=").Append(spatialAuthority);
            builder.Append(" artifact=").Append(selectedArtifact);
            builder.Append(" seed=").Append(worldSeed);
            builder.Append(" profile=").Append(profileId);
            builder.Append(" services=").Append(activeServiceCount).Append('/').Append(requiredServiceCount);
            builder.Append(" modelMode=").Append(modelMode);
            builder.Append(" origin=").Append(originReady);
            builder.Append(" plan=").Append(planReady);
            builder.Append(" materialized=").Append(materialized);
            builder.Append(" player=").Append(playerReady);
            builder.Append(" npc=").Append(npcReady).Append('(').Append(activeNpcCount).Append('/').Append(expectedNpcCount).Append(')');
            builder.Append(" terrain=").Append(terrainReady);
            builder.Append(" visual=").Append(visualReady)
                .Append("(").Append(visualRadius).Append("/prewarm=").Append(visualPrewarmRadius)
                .Append("/retain=").Append(visualRetentionRadius).Append("/")
                .Append(visualDistanceMeters.ToString("0", CultureInfo.InvariantCulture)).Append("m/far=")
                .Append(visualFarClipMeters.ToString("0", CultureInfo.InvariantCulture)).Append("m)");
            builder.Append(" streaming=").Append(streamingReady);
            builder.Append(" currentChunk=").Append(currentChunk);
            builder.Append(" failedChunks=").Append(failedChunkCount);
            builder.Append(" traversed=").Append(traversed);
            builder.Append(" loads=").Append(successfulLoadCount);
            builder.Append(" saves=").Append(successfulSaveCount);
            builder.Append(" stateRevision=").Append(playerStateRevision).Append('/').Append(worldStateRevision);
            builder.Append(" worldId=").Append(worldId);
            builder.Append(" commit=").Append(activeCommitId);
            builder.Append(" epoch=").Append(requestEpoch);
            if (playerCollision != null)
                builder.Append(" playerContract=").Append(playerCollision.radius.ToString("0.###")).Append('x').Append(playerCollision.height.ToString("0.###")).Append("m walk=").Append(playerCollision.supportedWalkSpeed.ToString("0.###")).Append(" sprint=").Append(playerCollision.supportedSprintSpeed.ToString("0.###"));
            return builder.ToString();
        }

        // note: Signatures exclude verbose failure text so a repeated transient explanation does not spam identical state logs.
        public string ToSignature()
        {
            return servicesReady + "|" + profileReady + "|" + originReady + "|" + planReady + "|" +
                spatialReady + "|" + spatialAuthority + "|" + materialized + "|" + playerReady + "|" +
                npcReady + "|" + terrainReady + "|" + visualReady + "|" + streamingReady + "|" + traversed + "|" +
                currentChunk + "|" + failedChunkCount + "|" + successfulLoadCount + "|" +
                successfulSaveCount + "|" + profileId + "|" + worldSeed + "|" + activeCommitId + "|" + playerStateRevision + "|" + worldStateRevision;
        }
    }
}
