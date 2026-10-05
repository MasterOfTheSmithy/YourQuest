using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Unity.Profiling;
using UnityEngine;

public enum YQSemanticChunkLifecycle
{
    Unseen = 0,
    SemanticallyPlanned = 1,
    Queued = 2,
    Generating = 3,
    Generated = 4,
    Active = 5,
    Retained = 6,
    Unloading = 7,
    Unloaded = 8,
    Failed = 9
}

// note: Keep the physical terrain publication contract separate from semantic chunk lifecycle so terrain presence never implies collision safety.
public enum YQTerrainReadinessState
{
    None = 0,
    TerrainSampled = 1,
    TerrainCreated = 2,
    CollisionReady = 3
}

// note: Keep publication evidence immutable and read-only so verification can inspect ownership without refreshing demand or changing scheduler state.
public struct YQSemanticChunkPublicationSnapshot
{
    public Vector2Int coordinate;
    public int publicationVersion;
    // note: The provider kind and full owner epoch distinguish replacement from ordinary stage receipts.
    public bool authoredTerrain;
    public int ownerEpoch;
    public YQSemanticChunkLifecycle lifecycle;
    public bool demanded;
    public bool hardViewDemanded;
    // note: Recovery probes must distinguish the strict live frustum from the wider preparation ring before choosing a physical witness.
    public bool guaranteedViewDemanded;
    // note: Natural unload verification needs the exact live retention leases so a site or speed runway is not mistaken for a stale owner.
    public bool canonicalPreparationDemanded;
    public bool predictedViewDemanded;
    public bool provisionalGroundPrefetchDemanded;
    public bool provisionalGroundTurnBufferDemanded;
    public bool siteTerrainDependency;
    public bool siteTerrainHandoffPending;
    public bool publicationVerificationPinned;
    public int currentChunkDistance;
    public int effectiveUnloadRadius;
    public bool physicalRepresentation;
    public bool terrainPublished;
    public YQTerrainReadinessState terrainReadiness;
    public bool requiredContentReady;
    public bool overlayReady;
    public bool appearanceReady;
    public bool requiredEcologyReady;
    public bool activationComplete;
    public bool traversable;
    public bool hasExplicitFailure;
    public string failureReason;
    public bool terrainWorkActive;
    // note: Distinguish a live collider-cook publication from queued or sampling work while preserving the final readiness gate.
    public bool terrainColliderPublicationPending;
    // note: A queued request is scheduled work, but it has not reached a live terrain worker or publication phase yet.
    public bool terrainWorkQueued;
    public long terrainWorkId;
    public string terrainWorkPhase;
    public float terrainWorkElapsedSeconds;
    public bool terrainCollisionPublicationHeld;
    public bool terrainRetryScheduled;
    public int terrainRetryCount;
    public bool appearanceWorkActive;
    public long appearanceWorkVersion;
    public bool appearanceRetryScheduled;
    public int appearanceRetryCount;
    public bool contentWorkActive;
    // note: A queued owner is live versioned content work even when lifecycle distance accounting has relabeled it as Active or Generated.
    public bool contentWorkQueued;
    public long contentWorkId;
    public bool ecologyWorkActive;
    public long ecologyWorkId;
    // note: Preserve the active ecology owner phase and age in view-readiness evidence so a deadline miss identifies its actual wait stage.
    public string ecologyWorkPhase;
    public float ecologyWorkPhaseElapsedSeconds;
    public bool ecologyRetryScheduled;
    public int ecologyRetryCount;
    public bool activationWorkActive;
    public bool lifecycleWorkPending;
}

public enum YQPublicationVerificationStage
{
    TerrainCollision = 0,
    Appearance = 1,
    RequiredEcology = 2
}

// note: Runtime feature targets give generated providers one typed ownership hook for overlay replay without making GameObjects authoritative.
[DisallowMultipleComponent]
public sealed class YQStreamedFeatureOverlayTarget : MonoBehaviour
{
    public string featureId;
    public string objectId;
    [NonSerialized] public long lastOverlayRevision = -1L;
    [NonSerialized] public string appliedState = string.Empty;
    [NonSerialized] public string appliedPayload = string.Empty;

    private void OnEnable()
    {
        // note: Late-created provider roots replay durable overlays once geometry is published; actor population completes independently.
        YQCompiledWorldSiteInstance site = GetComponent<YQCompiledWorldSiteInstance>();
        if (site != null && !site.IsContinuationGeometryLoaded)
            return;
        YQPlayerFollowingSemanticChunkStreamer.ReplayPersistedFeatureOverlaysForTarget(this);
    }
}

// note: Keep streamed terrain ownership checks in the same compiled source as the queue and publication state machine.

// note: This coordinator follows the authoritative player over the persisted semantic plan while keeping physical hierarchy activation bounded.
[DisallowMultipleComponent]
public sealed class YQPlayerFollowingSemanticChunkStreamer : MonoBehaviour
{
#if UNITY_EDITOR
    // note: Name existing pure height workers in opt-in traces without registering or changing pooled threads.
    private static readonly ProfilerMarker BackgroundHeightSamplingMarker =
        new ProfilerMarker("YQ.R2.Worker.HeightSampling");
#endif
    // note: Attribute repeated queue and owner-census CPU separately while preserving the existing dispatch order and resource rules.
    private static readonly ProfilerMarker ContentStartSearchProfilerMarker =
        new ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TryStartNextContentGeneration");
    private static readonly ProfilerMarker PhysicalOwnerReservationProfilerMarker =
        new ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TryReservePhysicalOwner");
    private static readonly ProfilerMarker ContentQueueSortProfilerMarker =
        new ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.SortContentQueue");
    private static readonly ProfilerMarker ReadyHardViewScanProfilerMarker =
        new ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TryGetReadyHardViewContentCandidate");
    private static readonly ProfilerMarker ReadyHardPreparationScanProfilerMarker =
        new ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TryGetReadyHardPreparationContentCandidate");

    // note: Separate streamer owner CPU from aggregate Unity update markers while preserving the same scheduling path.
    private static readonly Unity.Profiling.ProfilerMarker StreamingUpdateMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.Update");
    private static readonly Unity.Profiling.ProfilerMarker StreamingLateUpdateMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LateUpdate");
    private static readonly Unity.Profiling.ProfilerMarker TerrainAppearanceSliceMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TerrainAppearanceSlice");
    // note: Attribute the complete provisional-ground scheduler pass so a frame hitch can be matched to its exact queue owner.
    private static readonly Unity.Profiling.ProfilerMarker ProvisionalGroundQueueMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.ProcessProvisionalGroundQueue");
    // note: Attribute lifecycle census, owner transitions, and imported activation callbacks separately when a bounded pass still hitches.
    private static readonly Unity.Profiling.ProfilerMarker LifecycleDemandScanMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.ApplyLifecycleDemandScan");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleOwnerPassMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.ApplyLifecycleOwnerPass");
    // note: Bounded binary diagnostics distinguish cumulative lifecycle subcalls without changing readiness, budgets, or operation order.
    private static readonly Unity.Profiling.ProfilerMarker LifecycleReadinessMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleReadiness");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleActivationSliceMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleActivationSlice");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleVisualReconciliationMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleVisualReconciliation");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleHierarchyVisualGateMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleHierarchyVisualGate");
    // note: Saved binary samples separate enumeration and native state access from the hierarchy's inclusive cost before another repair is considered.
    private static readonly Unity.Profiling.ProfilerMarker LifecycleRendererEnumerationMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleRendererEnumeration");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleRendererStateReadMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleRendererStateRead");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleRendererStateWriteMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleRendererStateWrite");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleTerrainLookupMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleTerrainLookup");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleTerrainStateReadMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleTerrainStateRead");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleTerrainStateWriteMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleTerrainStateWrite");
#if UNITY_EDITOR
    // note: A binary-only observer counts real hierarchy assignments already at the requested state; clean witnesses never enable the added property reads.
    internal static bool R2ObserveVisualGateWrites;
    internal static readonly long[] R2VisualGateWriteCounts = new long[6];
    internal static long R2VisualRendererWritesPerformed;
    internal static long R2VisualTerrainWritesPerformed;
    // note: The Edit-only reconciliation fixture counts actual managed Terrain writes separately from the hierarchy gate.
    internal static long R2ManagedTerrainWritesPerformed;
    // note: Only the detached Edit regression enables scan counting; clean gameplay keeps this observer disabled.
    internal static bool R2ObserveContentCandidateScans;
    internal static long R2ContentCandidateScans;
#endif
    private static readonly Unity.Profiling.ProfilerMarker LifecycleCancellationMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleCancellation");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleTeardownMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.LifecycleTeardown");
    private static readonly Unity.Profiling.ProfilerMarker LifecycleObjectActivationMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.ApplyLifecycleObjectActivation");
    // note: Match camera-demand and motor-sweep fallback cost to the same Unity frame as the R2 wall-time witness.
    private static readonly Unity.Profiling.ProfilerMarker CameraGroundCoverageMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TryEnsureCurrentCameraGround");
    private static readonly Unity.Profiling.ProfilerMarker MotorTraversalConstraintMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.TryConstrainMovement");
    private static readonly Unity.Profiling.ProfilerMarker ImmediateGroundFallbackMarker =
        new Unity.Profiling.ProfilerMarker("YQPlayerFollowingSemanticChunkStreamer.EnsureGroundRepresentationImmediately");
    [Flags]
    private enum PhysicalDemandReason
    {
        Occupied = 1,
        Visible = 2,
        TurnCoverage = 4,
        Travel = 8,
        ImmediateNeighbor = 16,
        SharedSite = 32
    }

    // note: Distinguish camera safety publication from capsule probes so stress receipts identify which deadline exhausted prepared terrain.
    private enum GroundFallbackSource
    {
        CameraView,
        CapsuleStart,
        StationaryProbe,
        SweptFootprint
    }

    // note: Expose the configured world streamer so the authoritative motor can enforce the same traversal contract before moving.
    public static YQPlayerFollowingSemanticChunkStreamer Active { get; private set; }

    private struct StreamingWorkToken
    {
        // note: Capture the persisted ownership identity beside the local epoch so a yielded build cannot publish into another profile or world.
        public string profileId;
        public string worldId;
        public int generationEpoch;
        public long featureRevision;
        // note: Terrain and content retire independently; publicationVersion is a receipt counter, not a cancellation epoch.
        public RuntimeChunk owner;
        public int ownerEpoch;
        public bool terrainWork;
        public long workId;
    }

    private sealed class PendingTerrainPublication
    {
        public Vector2Int coordinate;
        public StreamingWorkToken token;
        public TerrainData data;
        public GameObject terrainObject;
        public Terrain terrain;
        public YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap appearancePreparation;
        public float startedAt;
        public string phase;
        public Coroutine coroutine;
        // note: Terrain appearance may finish during collider cooking but is not exposed until collision publication commits.
        public bool appearancePrepared;
        public bool published;
        public bool cancelled;
        public bool resourcesDestroyed;
    }

    private sealed class PendingRequiredEcologyPublication
    {
        // note: Share one attempt's prepared receipt with its structural owner; a delayed coroutine must observe publication before cancellation cleanup.
        public StreamingWorkToken token;
        public long workId;
        public GameObject root;
        public int result = int.MinValue;
        public string providerFailure;
        public string failure;
        public bool published;
    }

    // note: Align streamed tile coordinates with the authored terrain's centered 1024 metre footprint.
    private const float WorldGridOrigin = YQContinuousWorldFeatureAuthority.WorldGridOrigin;
    private const string ComponentName = "YQ_PLAYER_FOLLOWING_SEMANTIC_CHUNKS";
    private const string TerrainObjectName = "YQ_GENERATED_TERRAIN";
    private const int DefaultChunkWorldSize = 128;
    private const int DefaultActiveRadius = 1;
    private const int DefaultGenerationRadius = 2;
    private const int DefaultSemanticLookaheadRadius = 4;
    private const int DefaultRetentionRadius = 3;
    private const int DefaultUnloadRadius = 4;
    // note: The visual contract is intentionally independent from the smaller collision and content rings.
    private const int DefaultVisualRadius = 4;
    private const int DefaultVisualPrewarmRadius = 5;
    private const int DefaultVisualRetentionRadius = 6;
    // note: Reserve the motor's 0.52 m stair-resolution allowance so collision response cannot slide the capsule into an unready hard-view cell beside its requested path.
    private const float TraversalSweepSafetyMarginMeters = 0.55f;
    private const float GuaranteedVisualDistance = 512f;
    private const float QualificationCameraFarClip = 520f;
    // note: Bound stale terrain-request inspection independently from the queue's retained capacity so one frame cannot drain a large obsolete backlog.
    private const int MaximumTerrainRequestsInspectedPerFrame = 8;
    // note: Bound retry re-admission work so repeated transient failures cannot monopolize the streaming update.
    private const int MaximumTerrainRetriesPerFrame = 8;
    // note: Failed terrain owners receive four scheduled retries and then a terminal publication failure instead of an endless missing-ground loop.
    private const int MaximumTerrainPreparationRetries = 4;
    private const float TerrainPreparationRetryDelaySeconds = 0.35f;
    // note: A live terrain worker must publish, be preempted, or time out with an explicit retry receipt.
    private const float TerrainPreparationTimeoutSeconds = 120f;
    private const int MaximumQueuedChunkCount = 64;
    private const int MaximumTerrainCoverageRadius = 6;
    // note: Retry trimmed required-ring cells at a low cadence so a bounded queue can converge on the full coverage contract without adding work to every frame.
    private const float RequiredCoverageAdmissionIntervalSeconds = 0.10f;
    // note: Admit only a rotating bounded slice of the required ring so repair work cannot create a synchronous frontier hitch.
    private const int RequiredCoverageAdmissionsPerSlice = 2;
    // note: Spread hierarchy toggles across frames so a frontier transition cannot stall the main thread on a large authored object set.
    private const int LifecycleActivationObjectsPerFrame = 8;
    // note: Give camera-visible owners a larger bounded activation slice so a visible chunk cannot remain hidden behind retained-owner lifecycle work.
    private const int HardViewActivationObjectsPerFrame = 128;
    // note: Cap one hard-view owner's share so a large site hierarchy cannot consume the whole visible set's activation budget and starve later camera cells.
    private const int HardViewActivationObjectsPerOwner = 32;
    // note: Keep the hard-view acceleration short and explicit; distant lifecycle work remains under the normal two-millisecond slice.
    private const float HardViewLifecycleBudgetSeconds = 0.010f;
    // note: Evict only a small number of distant streamed roots per frame; destroying imported hierarchies is the expensive part of a frontier transition.
    // note: Evict one streamed history cell per frame so Unity destruction and terrain-neighbor repair cannot combine into a frontier hitch.
    private const int SemanticEvictionsPerFrame = 1;
    // note: Admit a bounded portion of a new semantic frontier per frame so a full-chunk teleport cannot synchronously create the entire lookahead square.
    private const int MaximumFrontierCellsPerFrame = 8;
    // note: Enforce the design's 50 ms hitch ceiling; ordinary scheduling uses smaller work slices within this upper bound.
    public const float StreamingFrameBudgetSeconds = 0.05f;
    // note: Upload a bounded rectangle per frame so one continuation tile needs fewer native TerrainData calls while each slice remains measurable.
    private const int TerrainRowsPerSlice = 8;
    // note: A measured 68 ms height-upload burst exceeded the shared and hitch budgets, so prepared heightfields use the same small row quantum as other terrain uploads.
    private const int PreparedTerrainRowsPerSlice = 8;
    // note: At extreme travel speed, reduce serial upload frames while staying below the 16-row batch associated with the measured 68 ms burst.
    private const int HighSpeedPreparedTerrainRowsPerSlice = 12;
    private const float HighSpeedPreparedTerrainThresholdMetersPerSecond = 120f;
    // note: Let the owning terrain driver continue between small strips while its shared frame budget permits, avoiding one forced frame per strip.
    private static readonly object ContinueTerrainPreparationWithinFrameBudget = new object();
    // note: Admit long predictive corridors over multiple frames; the time budget bounds synchronous owner work when a sudden speed increase exposes a missing lane.
    private const int MaximumPredictiveTerrainAdmissionChecksPerFrame = 12;
    private const int MaximumPredictiveTerrainAdmissionsPerFrame = 4;
    private const int MaximumPredictiveTerrainOwnerCreationsPerFrame = 1;
    private const float PredictiveOwnerAdmissionBudgetSeconds = 0.004f;
    // note: Refill the five-second visible-content runway to match terrain's measured readiness horizon at 260 m/s.
    private const int MaximumPredictiveSemanticOwnerCreationsPerPass = 8;
    // note: Start the max-speed semantic runway as soon as travel direction is known so a later speed hack does not begin prediction from an empty corridor.
    private const float PredictiveSemanticViewMinimumSpeedMetersPerSecond = 1f;
    private const float PredictiveSemanticViewHorizonSeconds = 5f;
    private const float PredictiveSemanticViewStepSeconds = 0.5f;
    // note: Keep high-speed prediction from rebuilding the full swept camera set every rendered frame when its lane is unchanged.
    private const float PredictiveSemanticViewRefreshIntervalSeconds = 0.25f;
    private const float MinimumPredictiveSemanticViewRefreshIntervalSeconds = 1f / 30f;
    // note: Keep three full cells of semantic turn runway so a terrain tile can finish before a 260 m/s player reaches the moving all-direction ring.
    private const int MaximumSemanticTurnAheadCells = 3;
    // note: Keep exact authored-border sampling bounded independently from the background-prepared interior heightfield.
    private const int TerrainSeamSamplesPerSlice = 16;
    // note: Overlap several immutable next-tile height samples with the active Unity terrain publication; these are pure arrays, not extra TerrainData/colliders, and keep the next urgent tile ready without expanding physical residency.
    private const int MaximumTerrainHeightPrefetches = 4;
    // note: Overlap the two physics cooking frame boundaries with the next canonical heightfield build while bounding unpublished TerrainData residency.
    private const int MaximumPendingTerrainColliderPublications = 2;
    // note: Use Unity's minimum valid terrain heightmap for temporary ground; canonical terrain replaces this same-authority 4 m preview with full detail.
    private const int ProvisionalGroundHeightmapResolution = 33;
    private const int ProvisionalGroundAlphamapResolution = 32;
    // note: Sample a smaller same-authority emergency grid, then interpolate it to Unity's minimum valid TerrainData resolution before publication.
    private const int EmergencyGroundHeightmapResolution = 13;
    private const int MinimumProvisionalGroundHeightmapResolution = 33;
    // note: Prebuild a temporary same-authority ground tile for incomplete view/preparation demand so sudden speed increases inherit ready terrain.
    private const int MaximumConcurrentProvisionalGroundSamplers = 4;
    private const int MaximumHighSpeedProvisionalGroundSamplers = 8;
    // note: Keep bounded completed results available while one collision-safe Terrain publication is admitted per frame, so a newly visible deadline can still enter the sampler pool.
    private const int MaximumRetainedProvisionalGroundHeightmaps = 64;
    private const int MaximumProvisionalGroundPublicationsPerFrame = 1;
    // note: A second publication may serve an already-visible cell only when the complete shared frame allowance still has room.
    // note: Completed live-view samples may use additional bounded publication slices before motor camera admission would otherwise synchronously publish them.
    private const int MaximumUrgentVisibleProvisionalGroundPublicationsPerFrame = 3;
    // note: Reuse retired safety Terrains at the moving frontier so a visibility-deadline miss does not allocate and destroy native TerrainData during fast travel.
    private const int MaximumPooledProvisionalGroundTiles = 32;
    // note: Keep more than the 2.5-second stress traversal of exact-authority ground ahead of the live camera.
    private const float MaximumGroundPrefetchSpeedMetersPerSecond = 260f;
    // note: Keep one second of continuous-authority ground ready in every direction so a sudden boost, reversal, or hard turn cannot outrun the current heading prediction.
    private const float ProvisionalGroundTurnBufferSeconds = 1f;
    private const float ProvisionalGroundLeadSeconds = 3f;
    // note: A real speed boost changes the three-second centerline lane immediately while ordinary speed noise does not rebuild the full view envelope.
    private const float HardViewAdmissionSpeedChangeMetersPerSecond = 24f;
    private const float HardViewAdmissionDirectionDotThreshold = 0.8f;
    // note: All streamer-owned main-thread lanes draw from this one per-frame allowance; an individual opaque Unity/API call may overrun, but no second lane starts after the shared deadline.
    private const float AggregateMainThreadBudgetSeconds = 0.010f;
    // note: Keep one existing provider-layer slice available to required camera-view ecology inside the shared frame budget.
    private const float RequiredHardViewEcologyBudgetReserveSeconds = 0.004f;
    private const float ContentSliceTargetSeconds = AggregateMainThreadBudgetSeconds;
    // note: Bound required-content concurrency explicitly; terrain sampling, painting, and ecology retain bounded independent workers but share the main-thread allowance.
    // note: Keep one bounded content lane available for the live camera envelope; site-content workers may legitimately wait on terrain and must not starve visible publication.
    private const int MaximumContentGenerationWorkers = 3;
    // note: Keep the prediction horizon finite while covering the measured terrain-readiness deadline with a safety margin.
    private const float PredictiveReadinessHorizonSeconds = 5f;
    private const float CriticalTerrainEscalationSeconds = 2f;
    // note: Retry a demanded published tile whose appearance lane ended without publication, but back off and surface a terminal failure instead of spinning every frame.
    private const float TerrainAppearanceRetryDelaySeconds = 0.25f;
    internal const int MaximumTerrainAppearanceRetries = 4;
    // note: Keep the physical appearance worker pool bounded even when terrain preparation completes several tiles in one streaming burst.
    private const int MaximumTerrainPaintingWorkers = 2;
    // note: Hold one painter slot in reserve for a terrain tile already inside the camera envelope.
    private const int LiveVisibleTerrainPaintingWorkerReserve = 1;
    // note: Revisit only a bounded number of demanded published owners per frame so appearance repair cannot become an unbounded scan.
    private const int MaximumAppearanceReconciliationsPerFrame = 8;
    // note: Retry a demanded camera ecology lane with the same bounded ownership rules as terrain appearance instead of silently leaving an exposed cell incomplete.
    private const float RequiredEcologyRetryDelaySeconds = 0.25f;
    private const int MaximumRequiredEcologyRetries = 4;
    private const int MaximumPredictiveTerrainRadius = 12;
    // note: Keep the normal speculative physical backlog bounded; the guaranteed camera envelope is added to this floor when its configured distance requires more cells.
    private const int MaximumTerrainQueueCapacity = 64;
    // note: Leave bounded residency headroom for the current all-direction view envelope and the complete four-second high-speed view runway.
    private const int MaximumPhysicalChunkCount = 256;
    // note: Keep ecology bounded while giving the current view and its preparation envelope three slots plus one reserved background slot.
    private const int MaximumDecorativeScatterWorkers = 3;
    private const int HardViewDecorativeWorkerReserve = 1;
    // note: Detailed streaming snapshots remain available at a lower cadence; two-second console writes were outside the measured work slice and caused avoidable editor frame spikes.
    private const float DiagnosticsIntervalSeconds = 10f;

    // note: Per-owner progress is transient observation only; it never advances or reschedules work.
    private sealed class OwnerIteratorProgress
    {
        public Type iteratorType, yieldType;
        public string providerSubstage;
        public bool yieldPending;
        public double iteratorStartedAt = -1d, yieldStartedAt = -1d, deniedStartedAt = -1d;
        public long steps;
        public double stepSeconds, maximumStepSeconds, deniedSeconds;
        public int lastStepFrame = -1, lastDeniedFrame = -1, deniedFrames;

        public void Reset()
        {
            iteratorType = yieldType = null;
            providerSubstage = null;
            yieldPending = false;
            iteratorStartedAt = yieldStartedAt = deniedStartedAt = -1d;
            steps = 0;
            stepSeconds = maximumStepSeconds = deniedSeconds = 0d;
            lastStepFrame = lastDeniedFrame = -1;
            deniedFrames = 0;
        }

        public void ObserveIterator(IEnumerator current)
        {
            Type nextType = current.GetType();
            if (iteratorType != nextType)
            {
                iteratorType = nextType;
                iteratorStartedAt = Time.realtimeSinceStartupAsDouble;
            }
        }

        public void Resume()
        {
            yieldPending = false;
            yieldStartedAt = -1d;
        }

        public void Denied()
        {
            if (lastDeniedFrame != Time.frameCount)
            {
                deniedFrames++;
                lastDeniedFrame = Time.frameCount;
            }
            if (deniedStartedAt < 0d)
                deniedStartedAt = Time.realtimeSinceStartupAsDouble;
        }

        public void Admitted()
        {
            if (deniedStartedAt >= 0d)
                deniedSeconds += Time.realtimeSinceStartupAsDouble - deniedStartedAt;
            deniedStartedAt = -1d;
        }

        public void Stepped(double start)
        {
            double elapsed = Math.Max(0d, Time.realtimeSinceStartupAsDouble - start);
            steps++;
            stepSeconds += elapsed;
            maximumStepSeconds = Math.Max(maximumStepSeconds, elapsed);
            lastStepFrame = Time.frameCount;
        }

        public void Yielded(object value)
        {
            yieldType = value == null ? null : value.GetType();
            yieldPending = true;
            yieldStartedAt = Time.realtimeSinceStartupAsDouble;
        }

        public string Describe(double now)
        {
            double denied = deniedSeconds + (deniedStartedAt < 0d ? 0d : now - deniedStartedAt);
            return "iterator=" + (iteratorType == null ? "none" : iteratorType.Name) +
                ",iteratorTypeAge=" + (iteratorStartedAt < 0d ? "n/a" : (now - iteratorStartedAt).ToString("0.000")) +
                ",steps=" + steps + ",stepMs=" + (stepSeconds * 1000d).ToString("0.000") +
                ",maxStepMs=" + (maximumStepSeconds * 1000d).ToString("0.000") +
                ",lastStepFrame=" + lastStepFrame + ",yield=" +
                (yieldPending ? (yieldType == null ? "frame" : yieldType.Name) : "none") +
                ",yieldAge=" + (yieldPending ? (now - yieldStartedAt).ToString("0.000") : "n/a") +
                ",budgetDeniedFrames=" + deniedFrames + ",budgetDeniedSeconds=" + denied.ToString("0.000") +
                (providerSubstage == null ? string.Empty : ",providerSubstage=" + providerSubstage);
        }
    }

    // note: Site presentation is a cache of one accepted physical root, never another site or streaming owner.
    private sealed class SharedSitePresentation
    {
        public string siteId;
        public string sourceSemanticId;
        public GameObject root;
        public YQStreamedFeatureOverlayTarget target;
        public YQCompiledWorldSiteInstance provider;
        public bool hasCompiledProvider;
        public bool requiresCompiledProvider;
        public int configurationEpoch;
        public int contentOwnerEpoch;
        public YQPreparedSpatialMaterializationV2 projection;
        public readonly List<Renderer> renderers = new List<Renderer>(64);
        public readonly List<Renderer> requiredRenderers = new List<Renderer>(64);
        public bool visualStateKnown;
        public bool visualState;
    }

    private sealed class RuntimeChunk
    {
        public GeneratedSemanticChunkRecord record;
        public readonly List<GameObject> ownedObjects = new List<GameObject>();
        public readonly List<GameObject> generatedObjects = new List<GameObject>();
        // note: Cache site renderer membership only at publication; ordinary lifecycle passes never rediscover these hierarchies.
        public readonly List<SharedSitePresentation> sitePresentations = new List<SharedSitePresentation>();
        public readonly HashSet<Renderer> sitePresentationRenderers = new HashSet<Renderer>();
        // note: Cache renderer/component discovery per published owner so live camera checks can validate state without rescanning Unity hierarchies every movement frame.
        public List<Renderer> rendererValidationCache;
        public List<Terrain> terrainValidationCache;
        public bool rendererValidationCacheDirty = true;
        // note: Keep structural and optional ecology roots separate so a preempted distant scatter pass can restart without duplicating accepted route/site objects.
        public GameObject generationRoot;
        public GameObject decorativeRoot;
        // note: Publication is transient versioned state; persisted lifecycle labels never authorize physical traversal.
        public int publicationVersion;
        public int terrainOwnerEpoch = 1;
        public int contentOwnerEpoch = 1;
        public long decorativeWorkId;
        // note: Transient timestamps identify whether a visible ecology miss began in demand admission, structure, or scatter work.
        public float hardViewAdmittedAt = -1f;
        public float collisionReadyAt = -1f;
        // note: Preserve queue age separately from hard-view age to distinguish late admission from worker dispatch delay.
        public float contentQueuedAt = -1f;
        public float contentStartedAt = -1f;
        public float contentReadyAt = -1f;
        public float ecologyStartedAt = -1f;
        public double appearanceWorkStartedAt = -1d;
        // note: Reset progress per content epoch; retain only the latest preemption and bounded attempt counts.
        public readonly OwnerIteratorProgress structuralProgress = new OwnerIteratorProgress();
        public readonly OwnerIteratorProgress ecologyProgress = new OwnerIteratorProgress();
        // note: Keep paint-lane progress separate so a visible appearance miss identifies biome, detail, or budget-wait work.
        public readonly OwnerIteratorProgress biomeAppearanceProgress = new OwnerIteratorProgress();
        public readonly OwnerIteratorProgress detailAppearanceProgress = new OwnerIteratorProgress();
        public string appearanceDetailSubstage = string.Empty;
        public int progressEpoch, contentAttemptCount, diagnosticPreemptionCount, lastPreemptedEpoch;
        // note: Attribute shared hard-view content headroom to the owner so a starved visible iterator reveals who claimed its reserved progress.
        public int hardViewProgressReserveSlices, hardViewProgressReserveDeniedFrames, hardViewProgressYieldedToLiveFrames;
        public int lastHardViewProgressDiagnosticFrame = -1;
        // note: Keep bounded failure-only terrain dispatch counters so an incomplete live cell identifies queue, active-slot, and owner-capacity waits.
        public int terrainDispatchWaitFrames, terrainDispatchActiveFrames, terrainDispatchPublicationFrames;
        public int terrainDispatchPainterFrames, terrainDispatchAdmissionFrames, terrainDispatchCapacityFrames;
        public int terrainDispatchOtherCandidateFrames, terrainDispatchStartedFrames, lastTerrainDispatchDiagnosticFrame = -1;
        public int lastTerrainDispatchQueueIndex = -1;
        public double progressStartedAt = -1d, epochHardDemandAt = -1d, epochCollisionAt = -1d, lastPreemptedAt = -1d;
        public bool hardDemandReadyAtStart, collisionReadyAtStart;
        public Vector2Int lastPreemptingCoordinate;
        // note: Slot ownership survives coroutine cancellation and is released by the same work identity exactly once.
        public long decorativeSlotWorkId;
        public string decorativeWorkPhase = string.Empty;
        public float decorativePhaseStartedAt;
        public bool terrainPublished;
        // note: Track the monotonic physical terrain stages used by collision-first release and runtime verification.
        public YQTerrainReadinessState terrainReadiness;
        public bool physicalRepresentation;
        // note: Required structure, appearance/seams, and activation publish independently so completion order cannot overwrite readiness.
        public bool requiredContentReady;
        public bool appearanceReady;
        // note: Overlay replay is a publication stage; a generated root cannot become authoritative while an applicable persisted mutation is still unapplied.
        public bool overlayReady;
        public bool activationComplete;
        // note: Keep the legacy visual flag synchronized with appearanceReady for existing diagnostics.
        public bool visualReady;
        public float lastGenerationSeconds;
        public YQSemanticChunkLifecycle state;
        // note: Ecology may prepare beside required structure, but its readiness receipt waits for structural and overlay publication.
        public Coroutine decorativeGeneration;
        public PendingRequiredEcologyPublication pendingRequiredEcology;
        // note: Core canopy/understory/shrubs plus the streamed Terrain detail grass are the minimum ecology needed before a continuation cell may enter the camera; prefab groundcover, rocks, and edge dressing may finish afterward.
        public bool requiredEcologyReady;
        public bool decorativeComplete;
        // note: Preserve the last hard publication failure so readiness diagnostics identify the rejected contract stage instead of only reporting Failed.
        public string failureReason = string.Empty;
        // note: Cache the last requested hierarchy activation so lifecycle refreshes do not rescan every owned object every frame.
        public sbyte activeState = -1;
        // note: Cache the renderer exposure decision separately from object activation; readiness refreshes must not rewrite every Renderer when the visible result is unchanged.
        public bool visualStateKnown;
        public bool visualState;
        // note: Retain a partial activation cursor when a chunk owns more objects than the per-frame lifecycle budget.
        public int activationCursor;
        public sbyte activationTarget = -1;
        // note: Keep per-feature replay receipts on the transient owner so duplicate and stale overlays cannot reapply during one materialization epoch.
        public readonly Dictionary<string, long> appliedOverlayRevisions = new Dictionary<string, long>(StringComparer.Ordinal);
        public readonly HashSet<string> appliedOverlayReceipts = new HashSet<string>(StringComparer.Ordinal);

        public bool Traversable =>
            // note: Keep the publication snapshot's legacy traversal certificate strict; motor ground readiness is checked separately by the TerrainCollider owner.
            terrainReadiness >= YQTerrainReadinessState.CollisionReady &&
            requiredContentReady &&
            overlayReady &&
            appearanceReady &&
            requiredEcologyReady &&
            activationComplete;
    }

    // note: Keep each off-thread height sample tied to its original semantic owner so stale work cannot publish into a replacement world.
    private sealed class ProvisionalGroundSampling
    {
        public RuntimeChunk owner;
        public Task<float[,]> task;
        public CancellationTokenSource cancellation;
        public YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparedBiomeAlphamap;
        public bool abandoned;
    }

    private readonly Dictionary<Vector2Int, RuntimeChunk> _chunks =
        new Dictionary<Vector2Int, RuntimeChunk>();
    // note: Track a generation that lost current demand so its yielded content iterator can unwind without cancelling a still-useful published collider.
    private readonly HashSet<Vector2Int> _demandCancelledGenerations = new HashSet<Vector2Int>();
    // note: Index persisted semantic records by coordinate so every frontier refresh remains constant-time per cell.
    private readonly Dictionary<Vector2Int, GeneratedSemanticChunkRecord> _persistedChunkIndex =
        new Dictionary<Vector2Int, GeneratedSemanticChunkRecord>();
    private readonly List<Vector2Int> _queue = new List<Vector2Int>();
    // note: Reuse a sorted snapshot while projecting the live view into future camera cells at high traversal speeds.
    private readonly List<Vector2Int> _semanticViewPredictionScratch = new List<Vector2Int>(64);
    // note: Adjacent future frusta overlap heavily; process each projected coordinate once at its earliest arrival during a lane refresh.
    private readonly HashSet<Vector2Int> _semanticViewPredictionVisited = new HashSet<Vector2Int>(4096);
    // note: Keep the earliest future-frustum deadline so broad turn-buffer demand cannot outrank cells about to enter view.
    private readonly Dictionary<Vector2Int, float> _semanticViewPredictionArrivalSeconds =
        new Dictionary<Vector2Int, float>(128);
    private Vector2 _lastSemanticViewPredictionVelocity;
    private Vector2Int _lastSemanticViewPredictionCameraChunk = new Vector2Int(int.MinValue, int.MinValue);
    private float _nextSemanticViewPredictionRefreshAt;
    private bool _hasSemanticViewPrediction;
    private int _lastSemanticViewPredictionAdmissionBudget;
    private int _lastSemanticViewPredictionAdmitted;
    private int _lastSemanticViewPredictionProjectedHard;
    private int _lastSemanticViewPredictionOwnerBudgetSkipped;
    private float _lastSemanticViewPredictionRefreshAt;
    // note: Content demand is narrower than terrain coverage so decoration cannot backlog behind the player while colliders are still prewarmed.
    private readonly HashSet<Vector2Int> _contentDemand = new HashSet<Vector2Int>();
    // note: This set is the hard camera contract plus its bounded one-cell preparation envelope; the exact live frustum is tracked separately for strict publication deadlines.
    private readonly HashSet<Vector2Int> _hardViewDemand = new HashSet<Vector2Int>();
    // note: Keep near all-direction canonical preparation distinct from the longer speculative runway so release proves immediate camera turns are ready.
    private readonly HashSet<Vector2Int> _canonicalPreparationDemand = new HashSet<Vector2Int>();
    // note: Reuse overlapping camera/preparation cells across frontier refreshes so only newly exposed cells incur full owner admission work.
    private readonly HashSet<Vector2Int> _previousHardViewDemand = new HashSet<Vector2Int>();
    // note: Keep the exact live frustum separate from its asynchronous preparation ring; only the former is a presentation deadline.
    private readonly HashSet<Vector2Int> _guaranteedViewDemand = new HashSet<Vector2Int>();
    // note: Loading sites lease collision dependencies; reference counts preserve shared tiles until every waiter releases them.
    private readonly Dictionary<Vector2Int, int> _siteTerrainDependencies = new Dictionary<Vector2Int, int>();
    // note: Preserve the direct site-grounding handoff signal even if a shared waiter refreshes its lease during the same frame as queue admission.
    private readonly HashSet<Vector2Int> _siteTerrainHandoffRequests = new HashSet<Vector2Int>();
    // note: These are derived demands on the existing site owner, not new site instances or persistent authorities.
    private readonly HashSet<Vector2Int> _sharedSiteOwnerDemand = new HashSet<Vector2Int>();
    private readonly HashSet<Vector2Int> _sharedSiteVisibleOwners = new HashSet<Vector2Int>();
    private readonly HashSet<string> _sharedSiteVisibleIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Vector2Int, float> _sharedSiteOwnerArrivalSeconds = new Dictionary<Vector2Int, float>();
    private readonly Dictionary<string, Vector2Int> _sharedSiteIdentityDemand = new Dictionary<string, Vector2Int>(StringComparer.OrdinalIgnoreCase);
    private readonly List<int> _sharedSiteIndexScratch = new List<int>(32);
    private readonly List<int> _sharedSiteReadinessScratch = new List<int>(32);
    private readonly Dictionary<Vector2Int, int> _siteTerrainProgress = new Dictionary<Vector2Int, int>();
    private long _terrainProgressVersion;
    private WorldState _world;
    private GeneratedWorldPlanRecord _plan;
    // note: Detach accepted feature metadata at rebind so frontier queries neither revalidate the entire save nor follow mutable in-place edits.
    private YQSpatialBlueprintV2 _acceptedBlueprint;
    // note: Freeze the hash-verified physical projection for this configured plan epoch so each streamed cell does not rehash the full accepted world.
    private YQPreparedSpatialMaterializationV2 _acceptedSpatialProjection;
    // note: Remember the accepted spatial artifact identity so chunks created before compilation finishes can be rebuilt from the final authority.
    private string _observedSpatialArtifactSignature = string.Empty;
    private Transform _worldRoot;
    private Transform _player;
    private YQInvestorPlayerMotor _playerMotor;
    private Terrain _terrain;
    private YQGeneratedWorldTerrain.V2HeightSampler _heightSampler;
    private YQContinuousWorldCellAuthority _continuousAuthority;
    private bool _heightSamplerAttempted;
    private bool _terrainContinuationFailureLogged;
    private Vector2Int _currentChunk = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2 _lastPlayerPosition;
    private Vector2 _lastLookaheadPosition;
    private bool _hasLastLookaheadPosition;
    private float _nextDiagnosticsAt;
    private float _nextRequiredCoverageAdmissionAt;
    private int _requiredCoverageAdmissionCursor;
    private int _requiredCoverageAdmissionRadius = -1;
    // note: Avoid rebuilding the same bounded predictive corridor every frame when neither the current cell nor quantized direction has changed.
    private bool _hasPredictiveTerrainCorridor;
    private Vector2Int _lastPredictiveTerrainCell;
    private Vector2Int _lastPredictiveTerrainDirection;
    private int _lastPredictiveTerrainRadius = -1;
    // note: Keep the predicted corridor ordered by urgency and resume its bounded admission cursor on the next Update.
    private readonly List<Vector2Int> _predictiveTerrainCorridorCoordinates = new List<Vector2Int>(64);
    private int _predictiveTerrainCorridorCursor;
    private int _maximumPredictiveTerrainAdmissionsPerFrame;
    private int _maximumPredictiveTerrainOwnerCreationsPerFrame;
    private float _maximumPredictiveTerrainAdmissionSeconds;
    private int _queuedCount;
    private int _activeCount;
    private int _retainedCount;
    private int _physicalCount;
    private int _physicalCountRefreshFrame = -1;
    private Vector2Int _lastOwnerReservationBlock = new Vector2Int(int.MinValue, int.MinValue);
    private int _lastOwnerReservationBlockCount;
    private bool _lastOwnerReservationBlockUrgent;
    private int _ownerReservationBlockCount;
    private int _unloadedCount;
    private float _lastTerrainBuildSeconds;
    private float _maximumTerrainBuildSeconds;
    private float _lastTerrainPaintSeconds;
    private float _maximumTerrainPaintSeconds;
    private float _lastStreamingWorkSeconds;
    private int _lastStreamingWorkFrameCount = -1;
    private float _maximumStreamingWorkSeconds;
    // note: Measure periodic diagnostic formatting separately because it runs after the bounded gameplay work slice.
    private float _maximumStreamerDiagnosticLogSeconds;
    // note: Keep one deduplicated, capacity-limited physical admission set for the live, visible, predictive, and shared-site envelope.
    private readonly Dictionary<Vector2Int, PhysicalDemandReason> _physicalDemand =
        new Dictionary<Vector2Int, PhysicalDemandReason>();
    private int _physicalDemandOverflowCount;
    private bool _physicalDemandVisibleOverflow;
    private bool _physicalDemandOverflowReported;
    private int _aggregateBudgetFrame = -1;
    private int _requiredHardViewEcologyBudgetFrame = -1;
    private float _aggregateFrameWorkSeconds;
    private int _maximumAggregateFrameWorkFrame = -1;
    private int _reservedTerrainUploadFrame = -1;
    // note: Reserve one collider synchronization for demanded terrain per frame so two waiting collider tickets cannot block all further terrain dispatch.
    private int _reservedTerrainColliderSyncFrame = -1;
    // note: Preserve one bounded frame slice for already-visible terrain appearance when normal streaming work reaches its shared deadline.
    private int _reservedHardViewAppearanceFrame = -1;
    // note: The explicit R2 probe owns this fixed ring; ordinary gameplay allocates and records nothing.
    private struct AppearanceBudgetObservation
    {
        public int frame, lane, outcome, winnerLane;
        public Vector2Int coordinate, winner;
        public bool visible, winnerVisible;
        public float arrival, winnerArrival, spent;
    }
    private AppearanceBudgetObservation[] _appearanceBudgetObservations;
    private int _appearanceBudgetObservationCount;
    private bool _appearanceBudgetObservationActive;
    private int _appearanceBudgetWinnerFrame = -1, _appearanceBudgetWinnerLane;
    private Vector2Int _appearanceBudgetWinner, _appearanceBudgetFailureTarget;
    // note: Keep a bounded per-frame terrain handoff trace so an R2 miss can distinguish a one-frame cancel from repeated loss of an urgent request.
    private struct TerrainDispatchObservation
    {
        public int frame, queueCount, targetQueueIndex, physicalOwners, pendingPublications;
        public Vector2Int head, second, third, active, target;
        public int headPriority, secondPriority, thirdPriority, activePriority, targetPriority;
        public bool activeExists, activePreempted, targetKnown, targetQueued, targetActive;
        public float activeAge, targetRequestAge;
        public long activeWorkId;
        public string activePhase;
    }
    private TerrainDispatchObservation[] _terrainDispatchObservations;
    private int _terrainDispatchObservationCount;
    private int _terrainDispatchFailureFrame = -1;
    private Vector2Int _terrainDispatchFailureTarget;

    // note: An explicit focused witness owns this append-only buffer. Keep the first admission even if a later cell fails or the buffer fills.
    private static readonly Vector2Int FixedTerrainTraceTarget = new Vector2Int(11, 5);
    // note: Diagnostic replay retains transitions for every demanded cell, so the first miss need not be guessed in advance.
    private struct CompleteCellObservation
    {
        public int frame, classes, terrainRank, contentRank, priority, configurationEpoch, contentEpoch;
        public int preemptions, structureDenied, ecologyDenied, appearanceDenied, owners, demandCount, painters, colliders;
        public long structureSteps, ecologySteps, appearanceSteps;
        public double time, structureMs, ecologyMs;
        public float arrival, budgetMs;
        public string phase, reason, structureStage, ecologyStage, appearanceStage;
        public Vector2Int coordinate, terrainOwner, contentHead;
        public YQSemanticChunkPublicationSnapshot snapshot;
        public bool complete, stageReady, rootPresent, rootActive, rootInHierarchy;
        public int ownedCount, activationCursor, activationTarget, activeState;
        public bool queueCurrentUnready, queueRequiredCollision, queueImmediateCollision;
        public Vector2Int queueCurrentCoordinate;
        public Vector2 queueVelocity;
    }
    private CompleteCellObservation[] _completeCellHistory;
    private readonly Dictionary<Vector2Int, CompleteCellObservation> _completeCellLast = new Dictionary<Vector2Int, CompleteCellObservation>();
    private readonly List<Vector2Int> _completeCellRemoved = new List<Vector2Int>();
    private readonly Dictionary<Vector2Int, int> _completeCellSweepFrames = new Dictionary<Vector2Int, int>();
    private int _completeCellHistoryCount, _completeCellDropped, _completeCellFailureCount = -1;
    private int _completeCellIgnoredNoOwner;
    private Vector2Int _completeCellFailureCoordinate;
    private string _completeCellPhase = "approach";

    public void BeginCompleteCellObservation()
    {
        // note: This larger history is opt-in, allocated before measurement, and never admits or prepares gameplay work.
        _completeCellHistory = new CompleteCellObservation[65536];
        _completeCellLast.Clear();
        _completeCellSweepFrames.Clear();
        _completeCellHistoryCount = _completeCellDropped = 0;
        _completeCellIgnoredNoOwner = 0;
        _completeCellFailureCount = -1;
        _completeCellPhase = "approach";
        SampleCompleteCellObservation("begin", true);
    }

    public void SetCompleteCellObservationPhase(string phase)
    {
        if (_completeCellHistory == null)
            return;
        SampleCompleteCellObservation("phaseEnd", true);
        _completeCellPhase = phase;
        SampleCompleteCellObservation("phaseStart", true);
    }

    internal static bool IsCompleteCellObservationCoordinate(Vector2Int coordinate)
    {
        // note: This exact pair denotes an idle/no-owner scheduler slot, not a physical or canonical cell; leave every other signed coordinate eligible for observation.
        return coordinate.x != int.MinValue || coordinate.y != int.MinValue;
    }

    private void ObserveCompleteCell(Vector2Int coordinate, string reason, bool force = false)
    {
        if (_completeCellHistory == null)
            return;
        // note: Idle dispatch events still remain in the fixed-target trace, but must not enter cell snapshot distance arithmetic or abort the production Update.
        if (!IsCompleteCellObservationCoordinate(coordinate))
        {
            _completeCellIgnoredNoOwner++;
            return;
        }
        bool known = _completeCellLast.TryGetValue(coordinate, out CompleteCellObservation previous);
        if (!known && _completeCellLast.Count >= 2048)
        {
            _completeCellDropped++;
            return;
        }
        TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot);
        RuntimeChunk chunk = GetRuntimeChunk(coordinate);
        int classes = (_guaranteedViewDemand.Contains(coordinate) ? 1 : 0) |
            (_provisionalGroundTurnBufferDemand.Contains(coordinate) ? 2 : 0) |
            (_completeCellSweepFrames.TryGetValue(coordinate, out int sweptFrame) && sweptFrame == Time.frameCount ? 4 : 0) |
            (_semanticViewPredictionArrivalSeconds.ContainsKey(coordinate) ? 8 : 0) |
            (_physicalDemand.ContainsKey(coordinate) ? 16 : 0) |
            (_hardViewDemand.Contains(coordinate) ? 32 : 0) |
            (snapshot.siteTerrainDependency ? 64 : 0);
        // note: Record the comparator's safety predicates at observation time; a later frame-end rank cannot reconstruct the exact dispatch choice.
        Vector2 queueVelocity = ResolveTraversalVelocity();
        bool terrainUnpublished = !IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate);
        var item = new CompleteCellObservation
        {
            frame = Time.frameCount, time = Time.realtimeSinceStartupAsDouble, coordinate = coordinate,
            phase = _completeCellPhase, reason = reason, classes = classes, snapshot = snapshot,
            terrainRank = _terrainQueue.IndexOf(coordinate), contentRank = _queue.IndexOf(coordinate),
            priority = TerrainPreparationPriority(coordinate, queueVelocity),
            queueCurrentCoordinate = _currentChunk, queueVelocity = queueVelocity,
            queueCurrentUnready = coordinate == _currentChunk && terrainUnpublished,
            queueRequiredCollision = IsRequiredCoverageCoordinate(coordinate) && terrainUnpublished,
            queueImmediateCollision = queueVelocity.sqrMagnitude > 0.01f && IsImmediateTraversalTerrainCoordinate(coordinate, queueVelocity),
            arrival = _semanticViewPredictionArrivalSeconds.TryGetValue(coordinate, out float arrival) ? arrival : -1f,
            configurationEpoch = _configurationEpoch, contentEpoch = chunk != null ? chunk.contentOwnerEpoch : 0,
            preemptions = chunk != null ? chunk.diagnosticPreemptionCount : 0,
            structureSteps = chunk != null ? chunk.structuralProgress.steps : 0,
            ecologySteps = chunk != null ? chunk.ecologyProgress.steps : 0,
            appearanceSteps = chunk != null ? chunk.biomeAppearanceProgress.steps + chunk.detailAppearanceProgress.steps : 0,
            appearanceStage = chunk != null ? chunk.appearanceDetailSubstage : null,
            structureDenied = chunk != null ? chunk.structuralProgress.deniedFrames : 0,
            ecologyDenied = chunk != null ? chunk.ecologyProgress.deniedFrames : 0,
            appearanceDenied = chunk != null ? chunk.biomeAppearanceProgress.deniedFrames + chunk.detailAppearanceProgress.deniedFrames : 0,
            structureMs = chunk != null ? chunk.structuralProgress.stepSeconds * 1000d : 0d,
            ecologyMs = chunk != null ? chunk.ecologyProgress.stepSeconds * 1000d : 0d,
            structureStage = chunk != null ? chunk.structuralProgress.iteratorType?.Name : null,
            ecologyStage = chunk != null ? chunk.ecologyProgress.iteratorType?.Name : null,
            terrainOwner = _terrainPreparingCoordinate,
            contentHead = _queue.Count > 0 ? _queue[0] : new Vector2Int(int.MinValue, int.MinValue),
            owners = _physicalCount, demandCount = _physicalDemand.Count, painters = _terrainPainting.Count,
            colliders = _pendingTerrainPublications.Count,
            budgetMs = _aggregateBudgetFrame == Time.frameCount ? _aggregateFrameWorkSeconds * 1000f : 0f,
            // note: Distinguish prepared stage flags from actual published hierarchy; hidden roots cannot count as complete cells.
            complete = IsFullyLoadedForView(coordinate, chunk),
            rootPresent = chunk != null && chunk.generationRoot != null,
            rootActive = chunk != null && chunk.generationRoot != null && chunk.generationRoot.activeSelf,
            rootInHierarchy = chunk != null && chunk.generationRoot != null && chunk.generationRoot.activeInHierarchy,
            ownedCount = chunk != null ? chunk.ownedObjects.Count : 0,
            activationCursor = chunk != null ? chunk.activationCursor : 0,
            activationTarget = chunk != null ? chunk.activationTarget : -1,
            activeState = chunk != null ? chunk.activeState : -1,
            stageReady = snapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady && snapshot.requiredContentReady &&
                snapshot.appearanceReady && snapshot.requiredEcologyReady && snapshot.overlayReady && snapshot.activationComplete &&
                (chunk == null || chunk.visualReady)
        };
        // note: Time and continuously decreasing arrival are not transition keys; unchanged cells do not consume the bounded history.
        bool changed = !known || previous.phase != item.phase || previous.classes != item.classes ||
            previous.queueCurrentUnready != item.queueCurrentUnready || previous.queueRequiredCollision != item.queueRequiredCollision ||
            previous.queueImmediateCollision != item.queueImmediateCollision || previous.queueCurrentCoordinate != item.queueCurrentCoordinate ||
            previous.terrainRank != item.terrainRank || previous.contentRank != item.contentRank || previous.priority != item.priority ||
            previous.configurationEpoch != item.configurationEpoch || previous.contentEpoch != item.contentEpoch ||
            previous.preemptions != item.preemptions || previous.structureSteps != item.structureSteps || previous.ecologySteps != item.ecologySteps ||
            previous.structureDenied != item.structureDenied || previous.ecologyDenied != item.ecologyDenied || previous.appearanceDenied != item.appearanceDenied ||
            previous.appearanceSteps != item.appearanceSteps || previous.appearanceStage != item.appearanceStage ||
            previous.complete != item.complete || previous.stageReady != item.stageReady ||
            previous.rootPresent != item.rootPresent || previous.rootActive != item.rootActive || previous.rootInHierarchy != item.rootInHierarchy ||
            previous.ownedCount != item.ownedCount || previous.activationCursor != item.activationCursor ||
            previous.activationTarget != item.activationTarget || previous.activeState != item.activeState || previous.snapshot.ownerEpoch != snapshot.ownerEpoch ||
            previous.snapshot.publicationVersion != snapshot.publicationVersion || previous.snapshot.terrainReadiness != snapshot.terrainReadiness ||
            previous.snapshot.terrainWorkId != snapshot.terrainWorkId || previous.snapshot.terrainWorkPhase != snapshot.terrainWorkPhase ||
            previous.snapshot.contentWorkId != snapshot.contentWorkId || previous.snapshot.appearanceWorkVersion != snapshot.appearanceWorkVersion ||
            previous.snapshot.ecologyWorkId != snapshot.ecologyWorkId || previous.snapshot.ecologyWorkPhase != snapshot.ecologyWorkPhase ||
            previous.snapshot.requiredContentReady != snapshot.requiredContentReady || previous.snapshot.appearanceReady != snapshot.appearanceReady ||
            previous.snapshot.requiredEcologyReady != snapshot.requiredEcologyReady || previous.snapshot.overlayReady != snapshot.overlayReady ||
            previous.snapshot.activationComplete != snapshot.activationComplete;
        if (!force && !changed)
            return;
        _completeCellLast[coordinate] = item;
        if (_completeCellHistoryCount < _completeCellHistory.Length)
            _completeCellHistory[_completeCellHistoryCount++] = item;
        else
            _completeCellDropped++;
    }

    private void SampleCompleteCellObservation(string reason, bool force = false)
    {
        if (_completeCellHistory == null)
            return;
        foreach (Vector2Int coordinate in _physicalDemand.Keys)
            ObserveCompleteCell(coordinate, reason, force);
        // note: Record departures once so phase throughput can distinguish completion from discarded demand.
        _completeCellRemoved.Clear();
        foreach (KeyValuePair<Vector2Int, CompleteCellObservation> pair in _completeCellLast)
            if ((pair.Value.classes & 16) != 0 && !_physicalDemand.ContainsKey(pair.Key))
                _completeCellRemoved.Add(pair.Key);
        foreach (Vector2Int coordinate in _completeCellRemoved)
            ObserveCompleteCell(coordinate, "demandRemoved", true);
    }

    public string FinishCompleteCellObservation()
    {
        if (_completeCellHistory == null)
            return "not-recorded";
        SampleCompleteCellObservation("windowEnd", true);
        var report = new StringBuilder();
        report.Append("# records=").Append(_completeCellHistoryCount).Append(" dropped=").Append(_completeCellDropped)
            .Append(" ignoredNoOwner=").Append(_completeCellIgnoredNoOwner)
            .Append(" firstFailure=").Append(_completeCellFailureCoordinate).Append(" frozenPrefixRecords=").Append(_completeCellFailureCount)
            .AppendLine(" classes=1:visible,2:turn,4:exactSweep,8:predictive,16:physical,32:hard,64:site; classes overlap; arrival is existing prediction, first visible membership is observed exposure; frame-end transitions bound publication times; no scheduler mutation");
        report.AppendLine("frame\ttime\tphase\tevent\tcell\tclasses\tcomplete\tterrain\tcontent\tappearance\tecology\toverlay\tactivation\tterrainRank\tcontentRank\tpriority\tarrival\tconfigurationEpoch\tterrainEpoch\tcontentEpoch\tpublicationVersion\tterrainWork\tterrainPhase\tcolliderPending\tcontentWork\tappearanceWork\tecologyWork\tecologyPhase\tstructureSteps\tstructureMs\tstructureStage\tstructureDenied\tecologySteps\tecologyMs\tecologyStage\tecologyDenied\tappearanceDenied\tpreemptions\tterrainOwner\tcontentHead\towners\tdemandCount\tpainters\tcolliders\tbudgetMs\tappearanceSteps\tappearanceStage\tstageReady\trootPresent\trootActive\trootInHierarchy\townedCount\tactivationCursor\tactivationTarget\tactiveState\tqueueCurrentUnready\tqueueRequiredCollision\tqueueImmediateCollision\tqueueCurrentCoordinate\tqueueVelocityX\tqueueVelocityZ");
        for (int index = 0; index < _completeCellHistoryCount; index++)
        {
            CompleteCellObservation item = _completeCellHistory[index];
            var s = item.snapshot;
            report.AppendLine(string.Join("\t", new object[] { item.frame, item.time.ToString("F6", CultureInfo.InvariantCulture), item.phase, item.reason,
                item.coordinate, item.classes, item.complete, s.terrainReadiness, s.requiredContentReady, s.appearanceReady, s.requiredEcologyReady,
                s.overlayReady, s.activationComplete, item.terrainRank, item.contentRank, item.priority, item.arrival.ToString("F4", CultureInfo.InvariantCulture),
                item.configurationEpoch, s.ownerEpoch, item.contentEpoch, s.publicationVersion, s.terrainWorkId, s.terrainWorkPhase, s.terrainColliderPublicationPending,
                s.contentWorkId, s.appearanceWorkVersion, s.ecologyWorkId, s.ecologyWorkPhase, item.structureSteps, item.structureMs.ToString("F3", CultureInfo.InvariantCulture),
                item.structureStage, item.structureDenied, item.ecologySteps, item.ecologyMs.ToString("F3", CultureInfo.InvariantCulture), item.ecologyStage,
                item.ecologyDenied, item.appearanceDenied, item.preemptions, item.terrainOwner, item.contentHead, item.owners, item.demandCount,
                item.painters, item.colliders, item.budgetMs.ToString("F3", CultureInfo.InvariantCulture), item.appearanceSteps, item.appearanceStage,
                item.stageReady, item.rootPresent, item.rootActive, item.rootInHierarchy, item.ownedCount, item.activationCursor, item.activationTarget, item.activeState,
                item.queueCurrentUnready, item.queueRequiredCollision, item.queueImmediateCollision, item.queueCurrentCoordinate,
                item.queueVelocity.x.ToString("R", CultureInfo.InvariantCulture), item.queueVelocity.y.ToString("R", CultureInfo.InvariantCulture) }));
        }
        _completeCellHistory = null;
        _completeCellLast.Clear();
        _completeCellSweepFrames.Clear();
        return report.ToString();
    }
    private struct FixedTerrainObservation
    {
        public int frame, request, rank, priority, demandRevision, demandFrame, sweptFrame;
        public int owners, painters, colliders, activePriority, configurationEpoch, activeOwnerEpoch;
        public long ticks, activeWorkId, eventWorkId;
        public double time, admissionAge;
        public float requestAge, activeAge, spent, slice;
        public Vector2Int actor, active, head;
        public string reason, phase;
        public bool cancel, physicalDemand, pendingViewRefresh, viewValid;
        public YQSemanticChunkPublicationSnapshot target;
    }
    private FixedTerrainObservation[] _fixedTerrainObservations;
    private int _fixedTerrainCount, _fixedTerrainDropped, _fixedTerrainRequest;
    private int _fixedTerrainDemandRevision, _fixedTerrainDemandFrame = -1, _fixedTerrainSweptFrame = -1;
    private double _fixedTerrainFirstAdmission = -1d;

    private void BeginFixedTerrainObservation()
    {
        _fixedTerrainObservations = new FixedTerrainObservation[8192];
        _fixedTerrainCount = _fixedTerrainDropped = _fixedTerrainRequest = _fixedTerrainDemandRevision = 0;
        _fixedTerrainDemandFrame = _fixedTerrainSweptFrame = -1;
        _fixedTerrainFirstAdmission = -1d;
        RecordFixedTerrainObservation("begin; earlier admission not observed", FixedTerrainTraceTarget);
    }

    private void RecordFixedTerrainObservation(string reason, Vector2Int actor, long eventWorkId = 0)
    {
        ObserveCompleteCell(actor, reason);
        if (_fixedTerrainObservations == null)
            return;
        if (_fixedTerrainCount == _fixedTerrainObservations.Length)
        {
            _fixedTerrainDropped++;
            return;
        }
        // note: Snapshot only existing state; never sort, refresh demand, reserve capacity, or allocate gameplay work while tracing.
        TryGetPublicationSnapshot(FixedTerrainTraceTarget, out YQSemanticChunkPublicationSnapshot target);
        Vector2 velocity = ResolveTraversalVelocity();
        RuntimeChunk activeOwner = GetRuntimeChunk(_terrainPreparingCoordinate);
        _fixedTerrainObservations[_fixedTerrainCount++] = new FixedTerrainObservation
        {
            frame = Time.frameCount, ticks = System.Diagnostics.Stopwatch.GetTimestamp(),
            time = Time.realtimeSinceStartupAsDouble, reason = reason, actor = actor, eventWorkId = eventWorkId,
            request = _fixedTerrainRequest,
            admissionAge = _fixedTerrainFirstAdmission < 0d ? -1d : Time.realtimeSinceStartupAsDouble - _fixedTerrainFirstAdmission,
            requestAge = _terrainRequestedAt.TryGetValue(FixedTerrainTraceTarget, out float requestedAt)
                ? Time.unscaledTime - requestedAt : -1f,
            rank = _terrainQueue.IndexOf(FixedTerrainTraceTarget), priority = TerrainPreparationPriority(FixedTerrainTraceTarget, velocity),
            demandRevision = _fixedTerrainDemandRevision, demandFrame = _fixedTerrainDemandFrame, sweptFrame = _fixedTerrainSweptFrame,
            physicalDemand = _physicalDemand.ContainsKey(FixedTerrainTraceTarget), pendingViewRefresh = _cameraViewAdmissionPending,
            viewValid = _queuePriorityViewValid, configurationEpoch = _configurationEpoch,
            active = _terrainPreparingCoordinate, activeWorkId = _terrainPreparationWorkId,
            activeOwnerEpoch = activeOwner != null ? activeOwner.terrainOwnerEpoch : 0,
            activePriority = _terrainPreparationWorkId != 0 ? TerrainPreparationPriority(_terrainPreparingCoordinate, velocity) : -1,
            activeAge = _terrainPreparationWorkId != 0 ? Time.realtimeSinceStartup - _terrainPreparationStartedAt : -1f,
            phase = _terrainPreparationPhase, cancel = _terrainPreparationPreempted,
            head = _terrainQueue.Count > 0 ? _terrainQueue[0] : new Vector2Int(int.MinValue, int.MinValue),
            owners = _physicalCount, painters = _terrainPainting.Count, colliders = _pendingTerrainPublications.Count,
            spent = _aggregateBudgetFrame == Time.frameCount ? _aggregateFrameWorkSeconds : 0f,
            slice = _lastTerrainSliceSeconds, target = target
        };
    }

    public string FinishFixedTerrainObservation()
    {
        if (_fixedTerrainObservations == null)
            return "not-recorded";
        // note: Flush only after measured movement. Demand revision/frame describes the dispatch map; -1 means no rebuild observed yet.
        var report = new System.Text.StringBuilder();
        report.Append("# target=(11,5) records=").Append(_fixedTerrainCount).Append(" dropped=").Append(_fixedTerrainDropped)
            .Append(" truncated=").Append(_fixedTerrainDropped > 0).Append(" stopwatchFrequency=")
            .Append(System.Diagnostics.Stopwatch.Frequency).Append(" aggregateBudgetMs=10 colliderLimit=")
            .Append(MaximumPendingTerrainColliderPublications).Append(" capacity=").Append(PhysicalOwnerCapacity)
            .Append("; snapshot stages are sampled at events/frame end; sweptFrame is exact motor sweep membership\n");
        report.AppendLine("frame\tticks\ttime\tevent\tactor\teventWorkId\trequest\tfirstAdmissionAge\trequestAge\trank\tpriority\tdemandRevision\tdemandFrame\tphysicalDemand\tvisible\thard\tpredicted\tsweptFrame\tpendingViewRefresh\tviewValid\tconfigurationEpoch\townerEpoch\tactive\tactiveWorkId\tactiveOwnerEpoch\tactivePriority\tphase\tactiveAge\tcancel\thead\towners\tpainters\tcolliders\tbudgetMs\tlastTerrainSliceMs\tterrain\tterrainWorkId\tterrainPhase\tcolliderPending\tcontent\tappearance\tecology\toverlay\tactivation\tpublicationVersion\tretries\tretryScheduled\tfailure");
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        for (int index = 0; index < _fixedTerrainCount; index++)
        {
            FixedTerrainObservation item = _fixedTerrainObservations[index];
            YQSemanticChunkPublicationSnapshot target = item.target;
            report.Append(item.frame).Append('\t').Append(item.ticks).Append('\t').Append(item.time.ToString("F6", culture))
                .Append('\t').Append(item.reason).Append('\t').Append(item.actor).Append('\t').Append(item.eventWorkId)
                .Append('\t').Append(item.request).Append('\t').Append(item.admissionAge.ToString("F6", culture))
                .Append('\t').Append(item.requestAge.ToString("F6", culture)).Append('\t').Append(item.rank).Append('\t').Append(item.priority)
                .Append('\t').Append(item.demandRevision).Append('\t').Append(item.demandFrame).Append('\t').Append(item.physicalDemand)
                .Append('\t').Append(target.guaranteedViewDemanded).Append('\t').Append(target.hardViewDemanded)
                .Append('\t').Append(target.predictedViewDemanded).Append('\t').Append(item.sweptFrame)
                .Append('\t').Append(item.pendingViewRefresh).Append('\t').Append(item.viewValid)
                .Append('\t').Append(item.configurationEpoch).Append('\t').Append(target.ownerEpoch)
                .Append('\t').Append(item.active).Append('\t').Append(item.activeWorkId).Append('\t').Append(item.activeOwnerEpoch)
                .Append('\t').Append(item.activePriority).Append('\t').Append(item.phase).Append('\t').Append(item.activeAge.ToString("F6", culture))
                .Append('\t').Append(item.cancel).Append('\t').Append(item.head).Append('\t').Append(item.owners)
                .Append('\t').Append(item.painters).Append('\t').Append(item.colliders).Append('\t').Append((item.spent * 1000f).ToString("F3", culture))
                .Append('\t').Append((item.slice * 1000f).ToString("F3", culture)).Append('\t').Append(target.terrainReadiness)
                .Append('\t').Append(target.terrainWorkId).Append('\t').Append(target.terrainWorkPhase).Append('\t').Append(target.terrainColliderPublicationPending)
                .Append('\t').Append(target.requiredContentReady).Append('\t').Append(target.appearanceReady)
                .Append('\t').Append(target.requiredEcologyReady).Append('\t').Append(target.overlayReady).Append('\t').Append(target.activationComplete)
                .Append('\t').Append(target.publicationVersion).Append('\t').Append(target.terrainRetryCount)
                .Append('\t').Append(target.terrainRetryScheduled).Append('\t').Append((target.failureReason ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ')).AppendLine();
        }
        _fixedTerrainObservations = null;
        return report.ToString();
    }

    public void BeginAppearanceBudgetObservation()
    {
        // note: Allocate once before the focused motor window, and retain only a bounded recent scheduling history.
        _appearanceBudgetObservations = new AppearanceBudgetObservation[4096];
        _terrainDispatchObservations = new TerrainDispatchObservation[2048];
        _appearanceBudgetObservationCount = 0;
        _terrainDispatchObservationCount = 0;
        _appearanceBudgetObservationActive = true;
        _appearanceBudgetWinnerFrame = -1;
        BeginFixedTerrainObservation();
    }

    public void FreezeAppearanceBudgetObservation()
    {
        // note: Preserve the first actual visibility failure across input and coast; a later call cannot relabel an already-frozen observation ring.
        if (!_appearanceBudgetObservationActive || !_hasLastVisualCoverageFailureCoordinate)
            return;
        if (_completeCellHistory != null && _completeCellFailureCount < 0)
        {
            // note: The first failed cell and its immutable prefix survive later failures and recovery.
            ObserveCompleteCell(_lastVisualCoverageFailureCoordinate, "firstFailure", true);
            _completeCellFailureCoordinate = _lastVisualCoverageFailureCoordinate;
            _completeCellFailureCount = _completeCellHistoryCount;
        }
        _appearanceBudgetObservationActive = false;
        _appearanceBudgetFailureTarget = _lastVisualCoverageFailureCoordinate;
        _terrainDispatchFailureFrame = Time.frameCount;
        _terrainDispatchFailureTarget = _lastVisualCoverageFailureCoordinate;
    }

    public string DescribeAppearanceBudgetObservation()
    {
        // note: Format only after measured movement; failure-time capture merely freezes the preallocated ring.
        if (_appearanceBudgetObservations == null || _appearanceBudgetObservationCount == 0)
            return "not-recorded";
        var report = new System.Text.StringBuilder();
        int count = Mathf.Min(_appearanceBudgetObservationCount, _appearanceBudgetObservations.Length);
        int start = _appearanceBudgetObservationCount - count;
        int lastFrame = _appearanceBudgetObservations[(_appearanceBudgetObservationCount - 1) % _appearanceBudgetObservations.Length].frame;
        report.Append("target=").Append(_appearanceBudgetFailureTarget).Append(" lastFrame=").Append(lastFrame)
            .Append(" outcomes=0:ordinary,1:reserve,2:reserveAlreadyClaimed,3:higherPriorityPainter,4:sharedBudget; lanes=1:biome,2:detail");
        for (int index = start; index < _appearanceBudgetObservationCount; index++)
        {
            AppearanceBudgetObservation item = _appearanceBudgetObservations[index % _appearanceBudgetObservations.Length];
            if (item.frame < lastFrame - 24)
                continue;
            report.Append("\n  frame=").Append(item.frame).Append(" owner=").Append(item.coordinate)
                .Append(" lane=").Append(item.lane).Append(" visible=").Append(item.visible)
                .Append(" arrival=").Append(item.arrival.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" outcome=").Append(item.outcome).Append(" spentMs=")
                .Append((item.spent * 1000f).ToString("0.000", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" winner=").Append(item.winner).Append(" winnerLane=").Append(item.winnerLane)
                .Append(" winnerVisible=").Append(item.winnerVisible).Append(" winnerArrival=")
                .Append(item.winnerArrival.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));
        }
        return report.ToString();
    }

    public void EndAppearanceBudgetObservation()
    {
        _appearanceBudgetObservationActive = false;
        _appearanceBudgetObservations = null;
        _terrainDispatchObservations = null;
    }

    public string DescribeTerrainDispatchObservation()
    {
        // note: Format the bounded queue/worker timeline after movement so diagnostic strings never run in the streaming hot path.
        if (_terrainDispatchObservations == null || _terrainDispatchObservationCount == 0)
            return "not-recorded";
        int count = Mathf.Min(_terrainDispatchObservationCount, _terrainDispatchObservations.Length);
        int start = _terrainDispatchObservationCount - count;
        int latestFrame = _terrainDispatchFailureFrame >= 0
            ? _terrainDispatchFailureFrame
            : _terrainDispatchObservations[(_terrainDispatchObservationCount - 1) % _terrainDispatchObservations.Length].frame;
        var report = new System.Text.StringBuilder(4096);
        report.Append("failureTarget=").Append(_terrainDispatchFailureTarget)
            .Append(" failureFrame=").Append(_terrainDispatchFailureFrame)
            .Append(" records=").Append(count).Append(" entries=frame targetQueued/index/priority/age targetActive head/priority second/priority third/priority active/priority/phase/age/preempted/workId queueCount physical pending");
        for (int index = start; index < _terrainDispatchObservationCount; index++)
        {
            TerrainDispatchObservation item = _terrainDispatchObservations[index % _terrainDispatchObservations.Length];
            if (item.frame > latestFrame || item.frame < latestFrame - 90)
                continue;
            report.Append("\n  ").Append(item.frame).Append(' ')
                .Append(item.targetKnown ? item.target : new Vector2Int(int.MinValue, int.MinValue)).Append(' ')
                .Append(item.targetQueued).Append('/').Append(item.targetQueueIndex).Append('/').Append(item.targetPriority).Append('/')
                .Append(item.targetRequestAge.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
                .Append(item.targetActive).Append(' ')
                .Append(item.head).Append('/').Append(item.headPriority).Append(' ')
                .Append(item.second).Append('/').Append(item.secondPriority).Append(' ')
                .Append(item.third).Append('/').Append(item.thirdPriority).Append(' ')
                .Append(item.activeExists ? item.active.ToString() : "<none>").Append('/').Append(item.activePriority).Append('/')
                .Append(item.activePhase ?? string.Empty).Append('/')
                .Append(item.activeAge.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture)).Append('/')
                .Append(item.activePreempted).Append('/').Append(item.activeWorkId).Append(' ')
                .Append(item.queueCount).Append(' ').Append(item.physicalOwners).Append(' ').Append(item.pendingPublications);
        }
        return report.ToString();
    }

    private void RecordTerrainDispatchObservation()
    {
        RecordFixedTerrainObservation("dispatchPass", _terrainPreparingCoordinate);
        if (!_appearanceBudgetObservationActive || _terrainDispatchObservations == null)
            return;
        Vector2 velocity = ResolveTraversalVelocity();
        TerrainDispatchObservation item = new TerrainDispatchObservation
        {
            frame = Time.frameCount,
            queueCount = _terrainQueue.Count,
            activeExists = _terrainPreparation != null,
            active = _terrainPreparingCoordinate,
            activePriority = _terrainPreparation != null ? TerrainPreparationPriority(_terrainPreparingCoordinate, velocity) : -1,
            activePhase = _terrainPreparationPhase,
            activeAge = _terrainPreparation != null ? Mathf.Max(0f, Time.realtimeSinceStartup - _terrainPreparationStartedAt) : 0f,
            activePreempted = _terrainPreparationPreempted,
            activeWorkId = _terrainPreparationWorkId,
            physicalOwners = _physicalCount,
            pendingPublications = _pendingTerrainPublications.Count,
            head = _terrainQueue.Count > 0 ? _terrainQueue[0] : new Vector2Int(int.MinValue, int.MinValue),
            second = _terrainQueue.Count > 1 ? _terrainQueue[1] : new Vector2Int(int.MinValue, int.MinValue),
            third = _terrainQueue.Count > 2 ? _terrainQueue[2] : new Vector2Int(int.MinValue, int.MinValue),
            headPriority = _terrainQueue.Count > 0 ? TerrainPreparationPriority(_terrainQueue[0], velocity) : -1,
            secondPriority = _terrainQueue.Count > 1 ? TerrainPreparationPriority(_terrainQueue[1], velocity) : -1,
            thirdPriority = _terrainQueue.Count > 2 ? TerrainPreparationPriority(_terrainQueue[2], velocity) : -1,
            targetKnown = _hasLastVisualCoverageFailureCoordinate,
            target = _lastVisualCoverageFailureCoordinate,
            targetQueued = _hasLastVisualCoverageFailureCoordinate && _terrainQueued.Contains(_lastVisualCoverageFailureCoordinate),
            targetActive = _hasLastVisualCoverageFailureCoordinate && _terrainPreparation != null &&
                _terrainPreparingCoordinate == _lastVisualCoverageFailureCoordinate,
            targetQueueIndex = _hasLastVisualCoverageFailureCoordinate ? _terrainQueue.IndexOf(_lastVisualCoverageFailureCoordinate) : -1,
            targetPriority = _hasLastVisualCoverageFailureCoordinate
                ? TerrainPreparationPriority(_lastVisualCoverageFailureCoordinate, velocity)
                : -1,
            targetRequestAge = _hasLastVisualCoverageFailureCoordinate &&
                _terrainRequestedAt.TryGetValue(_lastVisualCoverageFailureCoordinate, out float requestedAt)
                    ? Mathf.Max(0f, Time.unscaledTime - requestedAt)
                    : 0f
        };
        _terrainDispatchObservations[_terrainDispatchObservationCount++ % _terrainDispatchObservations.Length] = item;
    }

    private void RecordAppearanceBudgetObservation(Vector2Int coordinate, OwnerIteratorProgress progress,
        bool admitted, int previousReservedFrame, float spent)
    {
        if (!_appearanceBudgetObservationActive || _appearanceBudgetObservations == null)
            return;
        int lane = _chunks.TryGetValue(coordinate, out RuntimeChunk owner) &&
            ReferenceEquals(progress, owner.detailAppearanceProgress) ? 2 : 1;
        bool wonReserve = admitted && previousReservedFrame != Time.frameCount &&
            _reservedHardViewAppearanceFrame == Time.frameCount;
        if (wonReserve)
        {
            _appearanceBudgetWinnerFrame = Time.frameCount;
            _appearanceBudgetWinner = coordinate;
            _appearanceBudgetWinnerLane = lane;
        }
        bool hasWinner = _appearanceBudgetWinnerFrame == Time.frameCount;
        int outcome = admitted ? (wonReserve ? 1 : 0) :
            _reservedHardViewAppearanceFrame == Time.frameCount ? 2 : 4;
        if (!admitted && outcome == 4 && HasImmediateTerrainAppearanceDeadline(coordinate))
        {
            // note: Observe the same eligibility test without modifying reserve selection or iterator progress.
            if (HasHigherPriorityAppearanceReserveOwner(coordinate))
                outcome = 3;
        }
        _appearanceBudgetObservations[_appearanceBudgetObservationCount++ % _appearanceBudgetObservations.Length] =
            new AppearanceBudgetObservation
            {
                frame = Time.frameCount, coordinate = coordinate, lane = lane, outcome = outcome,
                visible = _guaranteedViewDemand.Contains(coordinate), arrival = GetAppearanceDeadlineArrival(coordinate),
                spent = spent, winner = hasWinner ? _appearanceBudgetWinner : new Vector2Int(int.MinValue, int.MinValue),
                winnerLane = hasWinner ? _appearanceBudgetWinnerLane : 0,
                winnerVisible = hasWinner && _guaranteedViewDemand.Contains(_appearanceBudgetWinner),
                winnerArrival = hasWinner ? GetAppearanceDeadlineArrival(_appearanceBudgetWinner) : float.PositiveInfinity
            };
    }
    // note: A fast-travel safety floor gets one bounded publication quantum per frame even when unrelated canonical work exhausts the shared allowance.
    private int _reservedTurnBufferGroundProgressFrame = -1;
    // note: Reserve at most one demanded hard-view content start per frame after unrelated work consumes the aggregate allowance.
    private int _reservedHardViewContentDispatchFrame = -1;
    private int _hardViewContentDispatchReservationCount;
    // note: Let one selected hard-view content owner spend the existing ecology reserve after unrelated work spends its allowance.
    private int _reservedHardViewContentProgressFrame = -1;
    private Vector2Int _reservedHardViewContentProgressOwner = new Vector2Int(int.MinValue, int.MinValue);
    private float _hardViewContentProgressReservedSeconds;
    private bool _nextContentSliceUsesHardViewReserve;
    private int _hardViewContentProgressReservationCount;
    // note: Rotate equal-deadline hard-view owners so coroutine start order cannot starve required content.
    private Vector2Int _lastHardViewContentProgressOwner = new Vector2Int(int.MinValue, int.MinValue);
    private float _maximumAggregateFrameWorkSeconds;
    private string _maximumAggregateFrameWorkStage = string.Empty;
    // note: Identify one indivisible nested painter/ecology step when aggregate frame timing exposes a hitch.
    private float _maximumPublicationSliceSeconds;
    private string _maximumPublicationSliceStage = string.Empty;
    private string _lastAggregateWorkStage = string.Empty;
    private int _aggregateBudgetDeferredSlices;
    // note: Keep the largest synchronous stage name beside the aggregate slice so a beta hitch identifies its owning production operation.
    private float _maximumStreamingStageSeconds;
    private string _maximumStreamingStage = string.Empty;
    private float _lastStreamingWorstStageSeconds;
    private string _lastStreamingWorstStage = string.Empty;
    private float _maximumProvisionalGroundTileSeconds;
    private Vector2Int _slowestProvisionalGroundTile = new Vector2Int(int.MinValue, int.MinValue);
    private float _lastTerrainSliceSeconds;
    private float _maximumTerrainSliceSeconds;
    private string _maximumTerrainSlicePhase = string.Empty;
    private float _lastContentSliceSeconds;
    private float _maximumContentSliceSeconds;
    private string _maximumContentSliceStage = string.Empty;
    private float _maximumMaterializationSubstageSeconds;
    private string _maximumMaterializationSubstage = string.Empty;
    private float _lastTerrainPaintSliceSeconds;
    private float _maximumTerrainPaintSliceSeconds;
    // note: Aggregate readiness telemetry over a rolling interval so the stress probe can compare production rate with traversal demand.
    private int _terrainReadyWindowCount;
    private float _terrainReadyWindowStartedAt;
    private float _terrainReadyCellsPerSecond;
    // note: Keep run-wide forward terrain throughput measured against active motor time so a quiet final window cannot erase productive traversal.
    private long _terrainReadyForwardCellCount;
    private float _terrainReadyMovementSeconds;
    private float _terrainReadyAverageCellsPerSecond;
    private float _terrainReadyMaximumCellsPerSecond;
    private float _terrainReadyWindowMovementSeconds;
    // note: Count each forward-corridor coordinate once per sampling window so lateral safety tiles cannot inflate longitudinal production.
    private readonly HashSet<Vector2Int> _terrainReadyWindowCoordinates = new HashSet<Vector2Int>();
    private int _maximumTerrainQueueDepth;
    private int _maximumTraversalCriticalTerrainQueueDepth;
    private int _maximumConcurrentTerrainPreparations;
    private int _activeDecorativeScatters;
    private int _maximumConcurrentDecorativeScatters;
    private int _duplicateTerrainRequestCount;
    private int _missedTerrainDeadlineCount;
    private Vector2Int _lastMissedTerrainCoordinate = new Vector2Int(int.MinValue, int.MinValue);
    // note: Count readiness-gate interventions separately from terrain deadlines so acceptance can prove normal travel was never held by the safety backstop.
    private int _traversalConstraintBlockCount;
    private Vector2Int _lastTraversalConstraintCell = new Vector2Int(int.MinValue, int.MinValue);
    // note: Keep the last diagnostic coordinate so a sustained safe stop reports its publication state once instead of flooding the Unity log every frame.
    private Vector2Int _lastTraversalDiagnosticCell = new Vector2Int(int.MinValue, int.MinValue);
    private int _minimumForwardReadyLeadCells = int.MaxValue;
    private int _lastForwardReadyLeadCells;
    // note: Keep admission timestamps separate from semantic persistence so readiness measures queue wait plus materialization time.
    private readonly Dictionary<Vector2Int, float> _terrainRequestedAt = new Dictionary<Vector2Int, float>();
    // note: P0/P1 latency begins when queued or active work enters the live velocity centerline, not when it was originally admitted as lateral speculation.
    private readonly Dictionary<Vector2Int, float> _terrainCriticalRequestedAt = new Dictionary<Vector2Int, float>();
    private readonly List<float> _terrainReadinessSamples = new List<float>();
    private readonly List<float> _terrainReadinessScratch = new List<float>();
    private float _maximumTerrainReadinessSeconds;
    // note: Measure the deferred save's synchronous serialization and atomic write so profiling reports the real persistence cost.
    private float _lastSemanticSaveSeconds;
    private int _lastSemanticSaveFrameCount = -1;
    private float _maximumSemanticSaveSeconds;
    private bool _configured;
    private float _nextSemanticSaveAt;
    private Coroutine _semanticSaveRoutine;
    private bool _semanticPrunePending;
    // note: Keep one real streamed tile as a bounded unload/revisit anchor so lifecycle transitions remain observable without retaining unbounded history.
    private Vector2Int _unloadedHistoryAnchor = new Vector2Int(int.MinValue, int.MinValue);
    // note: Share activation allowance across every lifecycle call in a frame and resume unfinished hierarchy work while the player is stationary.
    private bool _lifecyclePending;
    private int _lifecycleBudgetFrame = -1;
    private int _lifecycleBudgetRemaining;
    private float _lifecycleDeadline;
    // note: Unity performs queued destruction after LateUpdate; share the existing eviction allowance across lifecycle, capacity and history retirement.
    private int _streamingRetirementFrame = -1;
    private int _streamingRetirementsThisFrame;
    private int _retiredPhysicalOwnersThisFrame;
    // note: Reuse one bounded coordinate list so camera-visible owners receive activation slices before retained or distant owners.
    private readonly List<Vector2Int> _lifecyclePriorityScratch = new List<Vector2Int>();
    // note: Rotate the hard-view start point so a large visible frontier cannot let its first owner consume every activation slice.
    private int _hardViewLifecycleCursor;
    // note: Keep the visible-owner order separate from the all-owner list so the rotating cursor never changes retained-owner accounting.
    private readonly List<Vector2Int> _hardViewLifecycleScratch = new List<Vector2Int>();
    // note: Reuse a separate teardown list so rotating unload work stays ahead of inactive retained owners.
    private readonly List<Vector2Int> _unloadingLifecycleScratch = new List<Vector2Int>();
    private int _unloadingLifecycleCursor;
    // note: Reuse a stable candidate buffer so pre-view ecology can finish before its terrain reaches the live camera frustum.
    private readonly List<Vector2Int> _decorativePriorityScratch = new List<Vector2Int>();
    // note: Keep a small bounded worker set so hard-view content does not serialize every required cell behind one site hierarchy.
    private readonly Dictionary<Vector2Int, Coroutine> _activeGenerations =
        new Dictionary<Vector2Int, Coroutine>();
    private int _contentPreemptionCount;
    private int _predictedContentPreemptionCount;
    private int _configurationEpoch;
    // note: One existing population adapter advances under the streamer's shared budget after collider activation.
    private readonly Stack<IEnumerator> _continuationPopulationWork = new Stack<IEnumerator>();
    private YQCompiledWorldSiteInstance _continuationPopulationProvider;
    private RuntimeChunk _continuationPopulationChunk;
    private Transform _continuationPopulationContent;
    private StreamingWorkToken _continuationPopulationToken;
    // note: Frontier proposals share this streamer's lifetime and budget; generated semantics remain detached until paired publication.
    private readonly Stack<IEnumerator> _frontierConstructionWork = new Stack<IEnumerator>();
    private AsyncOperation _frontierConstructionPendingLoad;
    private readonly Dictionary<Vector2Int, int> _frontierConstructionAttempts = new Dictionary<Vector2Int, int>();
    private readonly Queue<Vector2Int> _frontierConstructionAttemptOrder = new Queue<Vector2Int>();
    private const float FrontierConstructionRetrySeconds = 0.75f;
    private const int FrontierConstructionBlocksPerSlice = 8;
    private const float FrontierConstructionScanBudgetSeconds = 0.0015f;
    private static readonly Vector2Int[] FrontierConstructionScanOffsets = BuildFrontierConstructionScanOffsets();
    private WorldState _frontierConstructionWorld;
    private GeneratedWorldPlanRecord _frontierConstructionPlan;
    private int _frontierConstructionEpoch, _frontierConstructionServiceEpoch, _frontierConstructionScan;
    private float _nextFrontierConstructionAt;
    private string _frontierConstructionBaseHash;
    private GeneratedSpatialContinuationV2Record _frontierConstructionPrevious;
    private YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance _frontierConstructionAccepted;
    private string _frontierConstructionFailure;

    private sealed class PreparedContinuationRefresh
    {
        public int configurationEpoch;
        public YQSpatialMaterializationResolverV2.PreparedContinuationInstallation installation;
        public YQPreparedSpatialMaterializationV2 previousProjection;
        public YQContinuousWorldCellAuthority previousCellAuthority, nextCellAuthority;
        public GeneratedSemanticWorldAuthorityRecord previousSemanticAuthority, nextSemanticAuthority;
        public string overlaySnapshot;
        public readonly List<Rect> terrainInfluences = new List<Rect>();
    }
    private long _streamingWorkVersion;
    private readonly Dictionary<Vector2Int, long> _activeGenerationWorkIds = new Dictionary<Vector2Int, long>();
    private long _terrainPreparationWorkId;
    // note: Retain a partially admitted frontier so boundary refresh work resumes without repeating already indexed cells.
    private bool _frontierRefreshPending;
    private bool _frontierRefreshInitial;
    private Vector2Int _frontierRefreshCenter;
    private Vector2 _frontierRefreshPosition;
    private int _frontierRefreshLookahead;
    private int _frontierRefreshX;
    private int _frontierRefreshZ;
    private Coroutine _terrainPreparation;
    private Vector2Int _terrainPreparingCoordinate = new Vector2Int(int.MinValue, int.MinValue);
    // note: Keep the active terrain phase visible so a stalled single-owner slot can be diagnosed without changing admission or readiness semantics.
    private float _terrainPreparationStartedAt;
    // note: Protect only the dispatch frame from cancellation while the newly-started terrain coroutine reaches its first yield.
    private int _terrainPreparationDispatchFrame = -1;
    private string _terrainPreparationPhase = string.Empty;
    private bool _terrainPreparationPreempted;
    // note: Preserve the latest terrain handoff decision so a stalled publication names the owner that repeatedly displaced its sampler.
    private int _terrainPreemptionCount;
    private long _lastTerrainPreemptedWorkId;
    private Vector2Int _lastTerrainPreemptingCoordinate = new Vector2Int(int.MinValue, int.MinValue);
    private int _lastTerrainPreemptedPriority = -1;
    private int _lastTerrainPreemptingPriority = -1;
    private string _lastTerrainPreemptionReason = string.Empty;
    private CancellationTokenSource _terrainHeightPreparationCancellation;
    private readonly List<Vector2Int> _terrainQueue = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> _terrainQueued = new HashSet<Vector2Int>();
    private readonly Dictionary<Vector2Int, float> _terrainRetryAt =
        new Dictionary<Vector2Int, float>();
    private readonly Dictionary<Vector2Int, int> _terrainPreparationRetryCount =
        new Dictionary<Vector2Int, int>();
    private readonly List<Vector2Int> _terrainRetryScratch =
        new List<Vector2Int>();
    private readonly Dictionary<Vector2Int, Terrain> _extendedTerrainTiles =
        new Dictionary<Vector2Int, Terrain>();
    private readonly List<Renderer> _rendererValidationScratch = new List<Renderer>(64);
    // note: TerrainData and colliders wait here after sampling releases the single main-thread preparation lane.
    private readonly Dictionary<Vector2Int, PendingTerrainPublication> _pendingTerrainPublications =
        new Dictionary<Vector2Int, PendingTerrainPublication>();
    private int _maximumPendingTerrainColliderPublications;
    private int _lastTerrainPhysicsSyncFrame = int.MinValue;
    // note: Keep a small visible/collidable terrain owner until its canonical streamed Terrain finishes collision and appearance publication.
    private readonly Dictionary<Vector2Int, Terrain> _provisionalGroundTiles =
        new Dictionary<Vector2Int, Terrain>();
    private readonly Stack<Terrain> _provisionalGroundPool = new Stack<Terrain>();
    private readonly List<Vector2Int> _provisionalGroundQueue = new List<Vector2Int>();
    private readonly HashSet<Vector2Int> _provisionalGroundQueued = new HashSet<Vector2Int>();
    // note: One camera-envelope refresh admits many coordinates; defer per-item overflow scans until the full priority set is known.
    private bool _batchingHardViewAdmission;
    // note: Track the forward safety corridor separately so it never promotes distant decoration/content work.
    private readonly HashSet<Vector2Int> _provisionalGroundPrefetchDemand = new HashSet<Vector2Int>();
    // note: Track the bounded all-direction speed-change envelope separately from the current-heading lead for deadline ordering.
    private readonly HashSet<Vector2Int> _provisionalGroundTurnBufferDemand = new HashSet<Vector2Int>();
    // note: Retain every cell geometrically inside the safety envelope for validation, including cells already owned by canonical view demand.
    private readonly HashSet<Vector2Int> _highSpeedTurnBufferCoverageDemand = new HashSet<Vector2Int>();
    private int _highSpeedTurnBufferCoverageReadyCount;
    private readonly Dictionary<Vector2Int, ProvisionalGroundSampling> _provisionalGroundSamplers =
        new Dictionary<Vector2Int, ProvisionalGroundSampling>();
    private readonly List<Vector2Int> _provisionalGroundSamplerScratch = new List<Vector2Int>();
    // note: Canceled workers release their map entries immediately but still occupy CPU until their next cancellation checkpoint.
    private int _abandonedProvisionalGroundWorkerCount;
    // note: Keep failed emergency entries visible separately from successfully published fallback tiles.
    private int _synchronousGroundFallbackAttemptCount;
    private int _synchronousGroundFallbackCount;
    private int _cameraGroundFallbackAttemptCount;
    private int _movementGroundFallbackAttemptCount;
    private int _completedSamplerGroundFallbackCount;
    private int _inFlightSamplerGroundFallbackCount;
    private int _missingSamplerGroundFallbackCount;
    // note: Retain only a few exceptional fallback snapshots so a speed witness can distinguish late view admission from sampler or owner starvation.
    private readonly string[] _recentSynchronousGroundFallbackDiagnostics = new string[8];
    private int _recentSynchronousGroundFallbackDiagnosticCount;
    private int _recentSynchronousGroundFallbackDiagnosticNext;
    private float _maximumSynchronousGroundFallbackSeconds;
    private float _maximumSynchronousGroundSamplingSeconds;
    private float _maximumSynchronousGroundPublicationSeconds;
    private int _lastSynchronousSpatialSampleEvaluations;
    private int _lastSynchronousSpatialSampleCacheHits;
    private int _lastSynchronousGroundFallbackResolution;
    private string _lastSynchronousGroundFallbackCell = string.Empty;
    private bool _groundSafetyPhysicsSyncPending;
    // note: Prefetched heightfields contain only deterministic scalar data; they are not physical terrain until the normal publication coroutine consumes them.
    private readonly Dictionary<Vector2Int, Task<float[,]>> _terrainHeightPrefetches =
        new Dictionary<Vector2Int, Task<float[,]>>();
    private long _backgroundHeightTaskCount;
    private long _backgroundHeightTaskSucceededCount;
    private long _backgroundHeightTaskCanceledCount;
    private long _backgroundHeightTaskFaultedCount;
    private long _backgroundHeightTaskTotalTicks;
    private long _backgroundHeightTaskMaximumTicks;
    private int _terrainPreparationHandoffCount;
    private float _terrainPreparationHandoffTotalSeconds;
    private float _terrainPreparationHandoffMaximumSeconds;
    private readonly Dictionary<Vector2Int, CancellationTokenSource> _terrainHeightPrefetchCancellations =
        new Dictionary<Vector2Int, CancellationTokenSource>();
    private readonly List<Vector2Int> _terrainHeightPrefetchScratch = new List<Vector2Int>();
    // note: Deferred biome painting is tracked separately so visual work cannot block the next collision tile.
    private readonly Dictionary<Vector2Int, Coroutine> _terrainPainting =
        new Dictionary<Vector2Int, Coroutine>();
    private readonly List<Vector2Int> _terrainPaintingScratch =
        new List<Vector2Int>();
    private readonly List<Vector2Int> _appearanceReconciliationScratch =
        new List<Vector2Int>();
    // note: Deduplicate the stable-priority candidate list in O(1) without changing its deterministic demand-enumeration order.
    private readonly HashSet<Vector2Int> _appearanceReconciliationMembershipScratch =
        new HashSet<Vector2Int>();
    // note: Track appearance recovery independently from collision retries so a collision-ready tile cannot silently remain visually incomplete.
    private readonly Dictionary<Vector2Int, float> _terrainAppearanceRetryAt =
        new Dictionary<Vector2Int, float>();
    private readonly Dictionary<Vector2Int, int> _terrainAppearanceRetryCount =
        new Dictionary<Vector2Int, int>();
    // note: Identify each painter instance so a stale completion can never remove a replacement painter registered for the same coordinate.
    private long _terrainPaintingWorkVersion;
    private readonly Dictionary<Vector2Int, long> _terrainPaintingWorkIds =
        new Dictionary<Vector2Int, long>();
    // note: Keep ecology retry state separate from terrain so a failed scatter lane cannot consume or reset collision publication telemetry.
    private readonly Dictionary<Vector2Int, float> _requiredEcologyRetryAt =
        new Dictionary<Vector2Int, float>();
    private readonly Dictionary<Vector2Int, int> _requiredEcologyRetryCount =
        new Dictionary<Vector2Int, int>();
    // note: Development-only publication gates delay a real stage completion without changing movement, queue priority, or persisted semantic data.
    private sealed class PublicationVerificationScenario
    {
        public bool holdTerrain;
        public bool holdAppearance;
        public bool holdRequiredEcology;
        public int appearanceFailuresRemaining;
        public int ecologyFailuresRemaining;
        public int ownerVersionAtStart;
    }
    private readonly Dictionary<Vector2Int, PublicationVerificationScenario> _publicationVerificationScenarios =
        new Dictionary<Vector2Int, PublicationVerificationScenario>();
    // note: Pin one selected owner while a publication replacement probe is active so ordinary frontier refresh cannot invalidate its recovery witness.
    private readonly HashSet<Vector2Int> _publicationVerificationDemand = new HashSet<Vector2Int>();
    // note: Cache one camera projection context per queue sort so view-aware priority stays allocation-free inside comparers.
    private Camera _queuePriorityCamera;
    private Vector3 _queuePriorityCameraPosition;
    private bool _queuePriorityViewValid;
    // note: Reuse the camera frustum planes so the hard-view admission test is exact without allocating every frame.
    private readonly Plane[] _queuePriorityFrustumPlanes = new Plane[6];
    // note: Remember the last camera projection admitted during Update so a LateUpdate camera turn can refresh only when the visible envelope actually changed.
    private Vector3 _lastHardViewAdmissionCameraPosition;
    private Quaternion _lastHardViewAdmissionCameraRotation = Quaternion.identity;
    private float _lastHardViewAdmissionFieldOfView;
    private float _lastHardViewAdmissionAspect;
    private float _lastHardViewAdmissionNearClip;
    private float _lastHardViewAdmissionFarClip;
    private Camera _lastHardViewAdmissionCamera;
    private Vector2Int _lastHardViewAdmissionPlayerChunk = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2Int _lastHardViewAdmissionCameraChunk = new Vector2Int(int.MinValue, int.MinValue);
    private Vector2 _lastHardViewAdmissionVelocity;
    private bool _hasHardViewAdmissionCamera;
    // note: Keep a rejected look direction as physical demand without allowing the camera to render that direction before publication completes.
    private bool _cameraViewAdmissionPending;

    private int GuaranteedViewChunkRadius =>
        Mathf.CeilToInt(Mathf.Max(1f, guaranteedVisualDistanceMeters) / Mathf.Max(32f, chunkWorldSize));

    private int GuaranteedViewChunkCapacity =>
        (GuaranteedViewChunkRadius * 2 + 1) * (GuaranteedViewChunkRadius * 2 + 1);

    private int GuaranteedViewPreparationRadius =>
        // note: Keep three full cells of all-direction semantic lead so turns and coast camera motion do not reveal owners before publication finishes.
        Mathf.Max(EffectiveVisualRadius, GuaranteedViewChunkRadius + MaximumSemanticTurnAheadCells);

    private int GuaranteedViewPreparationCapacity =>
        (GuaranteedViewPreparationRadius * 2 + 1) * (GuaranteedViewPreparationRadius * 2 + 1);

    private int GuaranteedViewAdmissionChunkCapacity =>
        // note: The admission scan includes one boundary ring so every chunk whose volume can intersect the guaranteed frustum has a queue slot.
        ((GuaranteedViewChunkRadius + 1) * 2 + 1) *
        ((GuaranteedViewChunkRadius + 1) * 2 + 1);

    private int PhysicalOwnerCapacity =>
        Mathf.Max(MaximumPhysicalChunkCount, GuaranteedViewChunkCapacity, GuaranteedViewPreparationCapacity);

    private int ContentQueueCapacity =>
        // note: Never trim a hard-view preparation owner merely because the camera frustum or its lead ring is larger than the ordinary queue floor.
        Mathf.Max(MaximumQueuedChunkCount, GuaranteedViewAdmissionChunkCapacity, GuaranteedViewPreparationCapacity);

    private int TerrainCoverageRadius =>
        // note: Keep collision prewarm tied to the nearby generation ring; semantic lookahead remains metadata demand, while hard-view admission separately certifies every visible cell.
        Mathf.Clamp(Mathf.Max(1, generationRadius), 1, MaximumTerrainCoverageRadius);

    private int EffectiveVisualRadius =>
        // note: Clamp the configured visual ring to a bounded square while preserving the serialized setting for qualification.
        Mathf.Clamp(Mathf.Max(1, visualRadius), 1, 20);

    private int EffectiveVisualPrewarmRadius =>
        // note: Prewarm always contains the complete visual ring so camera entry never depends on a corridor prediction.
        Mathf.Clamp(Mathf.Max(EffectiveVisualRadius, visualPrewarmRadius), EffectiveVisualRadius, 24);

    private int EffectiveVisualRetentionRadius =>
        // note: Retain one additional bounded ring so a reversal cannot unload a still-qualified visual cell.
        Mathf.Clamp(Mathf.Max(EffectiveVisualPrewarmRadius, visualRetentionRadius), EffectiveVisualPrewarmRadius, 28);

    private int EffectiveUnloadRadius =>
        // note: Keep the visual retention square resident long enough for reversals and camera turns to reuse complete content.
        Mathf.Max(unloadRadius, Mathf.Max(TerrainCoverageRadius, EffectiveVisualRetentionRadius));

    private int TerrainQueueCapacity =>
        // note: Bound queued coordinates by the same physical-owner limit so a full prewarm/prediction envelope cannot silently lose a demanded terrain request while one worker is active.
        Mathf.Max(1, Mathf.Max(MaximumTerrainQueueCapacity, PhysicalOwnerCapacity - 1));

    [Header("Deterministic Chunk Streaming")]
    [Min(32)] public int chunkWorldSize = DefaultChunkWorldSize;
    [Min(0)] public int activeRadius = DefaultActiveRadius;
    [Min(1)] public int generationRadius = DefaultGenerationRadius;
    [Min(1)] public int semanticLookaheadRadius = DefaultSemanticLookaheadRadius;
    [Min(1)] public int retentionRadius = DefaultRetentionRadius;
    [Min(1)] public int unloadRadius = DefaultUnloadRadius;

    [Header("Guaranteed Visual Envelope")]
    [Min(1)] public int visualRadius = DefaultVisualRadius;
    [Min(1)] public int visualPrewarmRadius = DefaultVisualPrewarmRadius;
    [Min(1)] public int visualRetentionRadius = DefaultVisualRetentionRadius;
    [Min(1f)] public float guaranteedVisualDistanceMeters = GuaranteedVisualDistance;
    [Min(1f)] public float qualificationCameraFarClipMeters = QualificationCameraFarClip;

    public Vector2Int CurrentChunk => _currentChunk;
    public int ActiveChunkCount => _activeCount;
    public int RetainedChunkCount => _retainedCount;
    public int PhysicalChunkCount => _physicalCount;
    public int UnloadedChunkCount => _unloadedCount;
    public int QueuedChunkCount => _queuedCount;
    private static int CountLiveObjectReferences(List<GameObject> objects)
    {
        // note: Count Unity-live owners during the one-shot census so destroyed references cannot masquerade as retained resources.
        int liveCount = 0;
        for (int index = 0; index < objects.Count; index++)
            if (objects[index] != null)
                liveCount++;
        return liveCount;
    }

    internal string CapturePhysicalOwnerCapacityCensus()
    {
        // note: Capture the two distinct owner populations after a measured run so capacity evidence cannot add work to movement frames.
        const int maximumOwnerDetails = 512;
        int physicalOwners = 0;
        int reservationOwners = 0;
        int physicalWithoutReservation = 0;
        int reservationWithoutPhysical = 0;
        int omittedPhysicalOwners = 0;
        int omittedReservationOnlyOwners = 0;
        StringBuilder physicalDetails = new StringBuilder(512);
        StringBuilder reservationOnlyDetails = new StringBuilder(256);
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            RuntimeChunk chunk = pair.Value;
            if (chunk == null)
                continue;
            bool reservationOwned = chunk.generatedObjects.Count > 0 ||
                chunk.state == YQSemanticChunkLifecycle.Generating ||
                (_terrainPreparation != null && pair.Key == _terrainPreparingCoordinate) ||
                _pendingTerrainPublications.ContainsKey(pair.Key);
            if (chunk.physicalRepresentation)
            {
                physicalOwners++;
                if (!reservationOwned)
                    physicalWithoutReservation++;
                if (physicalOwners <= maximumOwnerDetails)
                {
                    if (physicalDetails.Length > 0)
                        physicalDetails.Append('|');
                    physicalDetails.Append(pair.Key).Append(':').Append(chunk.state)
                        .Append(":epoch=").Append(chunk.terrainOwnerEpoch)
                        .Append(":publication=").Append(chunk.publicationVersion)
                        .Append(":objects=generated:").Append(chunk.generatedObjects.Count).Append('/').Append(CountLiveObjectReferences(chunk.generatedObjects))
                        .Append(":owned=").Append(chunk.ownedObjects.Count).Append('/').Append(CountLiveObjectReferences(chunk.ownedObjects))
                        .Append(":terrain=").Append(chunk.terrainReadiness)
                        .Append(":content=").Append(chunk.requiredContentReady ? '1' : '0')
                        .Append(":appearance=").Append(chunk.appearanceReady ? '1' : '0')
                        .Append(":ecology=").Append(chunk.requiredEcologyReady ? '1' : '0')
                        .Append(":overlay=").Append(chunk.overlayReady ? '1' : '0')
                        .Append(":activation=").Append(chunk.activationComplete ? '1' : '0')
                        .Append(":reservation=").Append(reservationOwned ? '1' : '0')
                        .Append(":authored=").Append(IsChunkInsideAuthoredTerrain(pair.Key) ? '1' : '0')
                        .Append(":siteLease=").Append(_siteTerrainDependencies.ContainsKey(pair.Key) ? '1' : '0');
                }
                else
                    omittedPhysicalOwners++;
            }
            if (reservationOwned)
            {
                reservationOwners++;
                if (!chunk.physicalRepresentation)
                {
                    reservationWithoutPhysical++;
                    if (reservationWithoutPhysical <= maximumOwnerDetails)
                    {
                        if (reservationOnlyDetails.Length > 0)
                            reservationOnlyDetails.Append('|');
                        reservationOnlyDetails.Append(pair.Key).Append(':').Append(chunk.state)
                            .Append(":epoch=").Append(chunk.terrainOwnerEpoch)
                            .Append(":publication=").Append(chunk.publicationVersion)
                            .Append(":objects=generated:").Append(chunk.generatedObjects.Count).Append('/').Append(CountLiveObjectReferences(chunk.generatedObjects))
                            .Append(":owned=").Append(chunk.ownedObjects.Count).Append('/').Append(CountLiveObjectReferences(chunk.ownedObjects))
                            .Append(":terrain=").Append(chunk.terrainReadiness)
                            .Append(":pendingPublication=").Append(_pendingTerrainPublications.ContainsKey(pair.Key) ? '1' : '0');
                    }
                    else
                        omittedReservationOnlyOwners++;
                }
            }
        }

        // note: Report the cached lifecycle census beside a live recount and its refresh frame to expose timing or population mismatches.
        return "sampleFrame=" + Time.frameCount +
            ",lifecycleRefreshFrame=" + _physicalCountRefreshFrame +
            ",cachedPhysical=" + _physicalCount +
            ",livePhysical=" + physicalOwners +
            ",capacity=" + PhysicalOwnerCapacity +
            ",reservationOwners=" + reservationOwners +
            ",pendingNativeRetirements=" + PendingStreamingNativeRetirements +
            ",reservationOwnersWithPendingNative=" + (reservationOwners + PendingStreamingNativeRetirements) +
            ",physicalWithoutReservation=" + physicalWithoutReservation +
            ",reservationWithoutPhysical=" + reservationWithoutPhysical +
            ",active=" + _activeCount +
            ",retained=" + _retainedCount +
            ",unloaded=" + _unloadedCount +
            ",terrainPreparation=" + (_terrainPreparation != null ? _terrainPreparingCoordinate + ":" + _terrainPreparationPhase + ":workId=" + _terrainPreparationWorkId : "none") +
            ",pendingTerrainPublications=" + DescribePendingTerrainPublications() +
            ",terrainQueue=" + _terrainQueue.Count +
            ",appearanceWorkers=" + _terrainPainting.Count +
            ",physicalOwners=[" + physicalDetails + "]" +
            (omittedPhysicalOwners > 0 ? ",omittedPhysicalOwners=" + omittedPhysicalOwners : string.Empty) +
            ",reservationOnlyOwners=[" + reservationOnlyDetails + "]" +
            (omittedReservationOnlyOwners > 0 ? ",omittedReservationOnlyOwners=" + omittedReservationOnlyOwners : string.Empty);
    }
    // note: Pending terrain reports queued, sampled, collider-cooking, and deferred-appearance work so capacity changes remain visible in profiling.
    public int PendingTerrainCount => _terrainQueue.Count + (_terrainPreparation != null ? 1 : 0) + _pendingTerrainPublications.Count + _terrainPainting.Count;
    // note: Collision readiness excludes deferred alphamap painting but includes tickets waiting for Unity's synchronized collider cook.
    public int PendingTerrainCollisionCount => _terrainQueue.Count + (_terrainPreparation != null ? 1 : 0) + _pendingTerrainPublications.Count;
    // note: Expose the single active collision preparation slot so a readiness timeout can distinguish slow work from an empty scheduler.
    public bool IsTerrainPreparing => _terrainPreparation != null;
    // note: Keep terrain admission diagnostics bounded to the active owner and queue head so a stalled required cell can be identified without serializing the full frontier.
    public string TerrainAdmissionDiagnostics
    {
        get
        {
            string queueHead = _terrainQueue.Count > 0 ? _terrainQueue[0].ToString() : "<none>";
            Vector2 velocity = ResolveTraversalVelocity();
            int activePriority = _terrainPreparation != null
                ? TerrainPreparationPriority(_terrainPreparingCoordinate, velocity)
                : -1;
            int queuePriority = _terrainQueue.Count > 0
                ? TerrainPreparationPriority(_terrainQueue[0], velocity)
                : -1;
            RuntimeChunk activeOwner = null;
            if (_terrainPreparation != null)
                _chunks.TryGetValue(_terrainPreparingCoordinate, out activeOwner);
            return "active=" + (_terrainPreparation != null ? _terrainPreparingCoordinate.ToString() : "<none>") +
                ";queueHead=" + queueHead + ";age=" +
                (_terrainPreparation != null ? Mathf.Max(0f, Time.realtimeSinceStartup - _terrainPreparationStartedAt).ToString("0.000") : "0.000") +
                ";phase=" + (_terrainPreparation != null && !string.IsNullOrWhiteSpace(_terrainPreparationPhase)
                    ? _terrainPreparationPhase
                    : "none") +
                ";workId=" + (_terrainPreparation != null ? _terrainPreparationWorkId : 0) +
                ";ownerEpoch=" + (activeOwner != null ? activeOwner.terrainOwnerEpoch : 0) +
                ";activePriority=" + activePriority + ";queuePriority=" + queuePriority +
                ";colliderPending=" + DescribePendingTerrainPublications() + "/" + MaximumPendingTerrainColliderPublications +
                ";activeDemand=" + (_terrainPreparation != null
                    ? "guaranteed:" + _guaranteedViewDemand.Contains(_terrainPreparingCoordinate) +
                      "/hard:" + _hardViewDemand.Contains(_terrainPreparingCoordinate) +
                      "/site:" + _siteTerrainDependencies.ContainsKey(_terrainPreparingCoordinate) +
                      "/handoff:" + _siteTerrainHandoffRequests.Contains(_terrainPreparingCoordinate)
                    : "none") +
                ";preempted=" + _terrainPreparationPreempted +
                ";preemptionCount=" + _terrainPreemptionCount +
                ";lastPreemption=" + _lastTerrainPreemptingCoordinate +
                ":" + _lastTerrainPreemptionReason +
                ":" + _lastTerrainPreemptedPriority + "->" + _lastTerrainPreemptingPriority +
                ";lastReservationBlock=" + _lastOwnerReservationBlock +
                ":owners=" + _lastOwnerReservationBlockCount +
                ":urgent=" + _lastOwnerReservationBlockUrgent +
                ":count=" + _ownerReservationBlockCount +
                ";viewDemand=" + _guaranteedViewDemand.Count + "/" + _hardViewDemand.Count +
                ";viewPrediction=" + _semanticViewPredictionArrivalSeconds.Count +
                ";predictionPass=" + _lastSemanticViewPredictionAdmissionBudget +
                ":admitted=" + _lastSemanticViewPredictionAdmitted +
                ":projectedHard=" + _lastSemanticViewPredictionProjectedHard +
                ":ownerLimitSkipped=" + _lastSemanticViewPredictionOwnerBudgetSkipped +
                ":age=" + (_lastSemanticViewPredictionRefreshAt > 0f
                    ? Mathf.Max(0f, Time.unscaledTime - _lastSemanticViewPredictionRefreshAt).ToString("0.000")
                    : "n/a") +
                ";current=" + _currentChunk + ";velocity=" + velocity.ToString("F2") +
                ";physical=" + _physicalCount + ";capacity=" + PhysicalOwnerCapacity;
        }
    }
    // note: Keep the visual backlog visible to diagnostics even though it does not block ground-collision readiness.
    public int PendingTerrainPaintCount => _terrainPainting.Count;
    public int SemanticChunkCount => _chunks.Count;
    // note: Expose the locked visual qualification envelope for runtime receipts and acceptance tooling.
    public int GuaranteedVisualRadius => EffectiveVisualRadius;
    public int VisualPrewarmRadius => EffectiveVisualPrewarmRadius;
    public int VisualRetentionRadius => EffectiveVisualRetentionRadius;
    public int VisualPreparationRadius => GuaranteedViewPreparationRadius;
    public float GuaranteedVisualDistanceMeters => Mathf.Max(1f, guaranteedVisualDistanceMeters);
    public float QualificationCameraFarClipMeters => Mathf.Max(1f, qualificationCameraFarClipMeters);
    // note: Count only cells that have passed the complete transient visual publication gate.
    public int VisualReadyCellCount
    {
        get
        {
            int ready = 0;
            foreach (RuntimeChunk chunk in _chunks.Values)
                if (chunk != null && chunk.visualReady)
                    ready++;
            return ready;
        }
    }
    // note: Report both deferred hierarchy toggles and partial frontier admission so readiness checks cannot finish while the current cell's lifecycle is still stale.
    public bool HasPendingLifecycleWork => _lifecyclePending || _frontierRefreshPending;
    public float LastTerrainBuildSeconds => _lastTerrainBuildSeconds;
    public float MaximumTerrainBuildSeconds => _maximumTerrainBuildSeconds;
    public long BackgroundHeightTaskCount => Interlocked.Read(ref _backgroundHeightTaskCount);
    public long BackgroundHeightTaskSucceededCount => Interlocked.Read(ref _backgroundHeightTaskSucceededCount);
    public long BackgroundHeightTaskCanceledCount => Interlocked.Read(ref _backgroundHeightTaskCanceledCount);
    public long BackgroundHeightTaskFaultedCount => Interlocked.Read(ref _backgroundHeightTaskFaultedCount);
    // note: Expose cumulative worker time so the verifier can calculate a phase-local average without resetting live diagnostics.
    public double BackgroundHeightTaskTotalSeconds => Interlocked.Read(ref _backgroundHeightTaskTotalTicks) /
        (double)System.Diagnostics.Stopwatch.Frequency;
    public float BackgroundHeightTaskAverageSeconds => BackgroundHeightTaskCount > 0
        ? (float)(Interlocked.Read(ref _backgroundHeightTaskTotalTicks) /
            (double)System.Diagnostics.Stopwatch.Frequency / BackgroundHeightTaskCount)
        : 0f;
    public float BackgroundHeightTaskMaximumSeconds =>
        (float)(Interlocked.Read(ref _backgroundHeightTaskMaximumTicks) /
            (double)System.Diagnostics.Stopwatch.Frequency);
    public int TerrainPreparationHandoffCount => _terrainPreparationHandoffCount;
    public float TerrainPreparationHandoffTotalSeconds => _terrainPreparationHandoffTotalSeconds;
    public float TerrainPreparationHandoffAverageSeconds => _terrainPreparationHandoffCount > 0
        ? _terrainPreparationHandoffTotalSeconds / _terrainPreparationHandoffCount
        : 0f;
    public float TerrainPreparationHandoffMaximumSeconds => _terrainPreparationHandoffMaximumSeconds;
    public float LastTerrainPaintSeconds => _lastTerrainPaintSeconds;
    public float MaximumTerrainPaintSeconds => _maximumTerrainPaintSeconds;
    public float LastStreamingWorkSeconds => _lastStreamingWorkSeconds;
    public int LastStreamingWorkFrameCount => _lastStreamingWorkFrameCount;
    public float MaximumStreamingWorkSeconds => _maximumStreamingWorkSeconds;
    public float MaximumStreamingStageSeconds => _maximumStreamingStageSeconds;
    public string MaximumStreamingStage => _maximumStreamingStage;
    public float LastStreamingWorstStageSeconds => _lastStreamingWorstStageSeconds;
    public string LastStreamingWorstStage => _lastStreamingWorstStage;
    public float LastTerrainSliceSeconds => _lastTerrainSliceSeconds;
    public float MaximumTerrainSliceSeconds => _maximumTerrainSliceSeconds;
    public string MaximumTerrainSlicePhase => _maximumTerrainSlicePhase;
    public float LastContentSliceSeconds => _lastContentSliceSeconds;
    public float MaximumContentSliceSeconds => _maximumContentSliceSeconds;
    public string MaximumContentSliceStage => _maximumContentSliceStage;
    public float MaximumMaterializationSubstageSeconds => _maximumMaterializationSubstageSeconds;
    public string MaximumMaterializationSubstage => _maximumMaterializationSubstage;
    public float MaximumAggregateFrameWorkSeconds => _maximumAggregateFrameWorkSeconds;
    public string MaximumAggregateFrameWorkStage => _maximumAggregateFrameWorkStage;
    public float MaximumPublicationSliceSeconds => _maximumPublicationSliceSeconds;
    public string MaximumPublicationSliceStage => _maximumPublicationSliceStage;
    public int AggregateBudgetDeferredSlices => _aggregateBudgetDeferredSlices;
    public int PhysicalDemandCount => _physicalDemand.Count;
    public int PhysicalDemandOverflowCount => _physicalDemandOverflowCount;
    public float LastTerrainPaintSliceSeconds => _lastTerrainPaintSliceSeconds;
    public float MaximumTerrainPaintSliceSeconds => _maximumTerrainPaintSliceSeconds;
    public float TerrainReadyCellsPerSecond => _terrainReadyCellsPerSecond;
    public int MaximumTerrainQueueDepth => _maximumTerrainQueueDepth;
    public int MaximumTraversalCriticalTerrainQueueDepth => _maximumTraversalCriticalTerrainQueueDepth;
    public int MaximumConcurrentTerrainPreparations => _maximumConcurrentTerrainPreparations;
    public int MaximumConcurrentDecorativeScatters => _maximumConcurrentDecorativeScatters;
    public int DuplicateTerrainRequestCount => _duplicateTerrainRequestCount;
    public int MissedTerrainDeadlineCount => _missedTerrainDeadlineCount;
    public long TerrainReadyForwardCellCount => _terrainReadyForwardCellCount;
    public float TerrainReadyAverageCellsPerSecond => _terrainReadyAverageCellsPerSecond;
    public float TerrainReadyMaximumCellsPerSecond => _terrainReadyMaximumCellsPerSecond;
    public int TraversalConstraintBlockCount => _traversalConstraintBlockCount;
    public Vector2Int LastTraversalConstraintCell => _lastTraversalConstraintCell;
    public int MinimumForwardReadyLeadCells => _minimumForwardReadyLeadCells == int.MaxValue ? 0 : _minimumForwardReadyLeadCells;
    public int LastForwardReadyLeadCells => _lastForwardReadyLeadCells;
    public float MaximumTerrainReadinessSeconds => _maximumTerrainReadinessSeconds;
    public float P95TerrainReadinessSeconds => CalculateReadinessPercentile(0.95f);
    public float LastSemanticSaveSeconds => _lastSemanticSaveSeconds;
    public int LastSemanticSaveFrameCount => _lastSemanticSaveFrameCount;
    public int ContentPreemptionCount => _contentPreemptionCount;
    public int PredictedContentPreemptionCount => _predictedContentPreemptionCount;
    public float MaximumSemanticSaveSeconds => _maximumSemanticSaveSeconds;
    // note: Expose the canonical authored terrain used by the streamer so diagnostics and seam checks never select an unrelated site terrain.
    public Terrain AuthoredTerrain => _terrain;

    public bool TryGetGeneratedTerrainAt(Vector3 worldPosition, out Terrain terrain)
    {
        terrain = null;
        // note: Site grounding must use a published collision-ready terrain tile at the site's actual world position, including streamed continuation tiles outside the origin terrain.
        if (IsTerrainAtWorldPosition(_terrain, worldPosition))
        {
            terrain = _terrain;
            return true;
        }

        foreach (Terrain candidate in _extendedTerrainTiles.Values)
        {
            if (!IsTerrainAtWorldPosition(candidate, worldPosition))
                continue;
            terrain = candidate;
            return true;
        }

        return false;
    }

    public void RequestTerrainAtWorldPosition(Vector3 worldPosition)
    {
        // note: Site grounding can span a tile edge; admit only the required collision tile so hidden validation cannot compete with unrelated content generation.
        if (float.IsNaN(worldPosition.x) || float.IsInfinity(worldPosition.x) ||
            float.IsNaN(worldPosition.z) || float.IsInfinity(worldPosition.z))
            return;

        Vector2Int coordinate = new Vector2Int(
            Mathf.FloorToInt((worldPosition.x - WorldGridOrigin) /
                Mathf.Max(1f, chunkWorldSize)),
            Mathf.FloorToInt((worldPosition.z - WorldGridOrigin) /
                Mathf.Max(1f, chunkWorldSize)));
        if (IsChunkInsideAuthoredTerrain(coordinate) ||
            HasPublishedTerrain(coordinate))
            return;

        // note: Create the canonical semantic owner without enqueueing its structures; the site already owns the content slot and needs only this tile's collision dependency.
        GetOrCreateChunk(coordinate);
        // note: Mark the exact request at its source so the active sampler can hand off without depending on a later dictionary refresh or queue sort.
        _siteTerrainHandoffRequests.Add(coordinate);
        PrioritizeTerrainRequest(coordinate);
    }

    public IEnumerator WaitForSiteTerrainRoutine(GameObject siteContent, Vector3[] coveragePoints, Action<bool> completed)
    {
        // note: The caller owns this iterator, so its existing cancellation disposal also releases the terrain leases.
        int epoch = _configurationEpoch;
        HashSet<Vector2Int> dependencies = new HashSet<Vector2Int>();
        const float maximumNoProgressSeconds = 6f;
        float lastProgressAt = Time.realtimeSinceStartup;
        long observedProgress = _terrainProgressVersion;
        try
        {
            for (int index = 0; index < coveragePoints.Length; index++)
            {
                Vector3 point = coveragePoints[index];
                Vector2Int coordinate = ChunkFor(new Vector2(point.x, point.z));
                if (IsChunkInsideAuthoredTerrain(coordinate) || !dependencies.Add(coordinate))
                    continue;
                _siteTerrainDependencies.TryGetValue(coordinate, out int users);
                _siteTerrainDependencies[coordinate] = users + 1;
            }

            while (siteContent != null && this != null && isActiveAndEnabled && epoch == _configurationEpoch)
            {
                bool ready = true;
                Vector2Int missing = default;
                for (int index = 0; index < coveragePoints.Length; index++)
                {
                    Vector3 point = coveragePoints[index];
                    if (TryGetGeneratedTerrainAt(point, out _))
                        continue;
                    missing = ChunkFor(new Vector2(point.x, point.z));
                    ready = false;
                    // note: Retry admission after capacity pressure without replacing an already queued or running request.
                    RequestTerrainAtWorldPosition(point);
                }
                if (ready)
                {
                    completed?.Invoke(true);
                    yield break;
                }

                // note: Queue delay is pending while collision production advances; retries/yields alone never reset the stall clock.
                if (observedProgress != _terrainProgressVersion)
                {
                    observedProgress = _terrainProgressVersion;
                    lastProgressAt = Time.realtimeSinceStartup;
                }
                if (Time.realtimeSinceStartup - lastProgressAt >= maximumNoProgressSeconds)
                {
                    string state = missing == _terrainPreparingCoordinate ? "preparing" :
                        _terrainQueued.Contains(missing) ? "queued" :
                        _terrainRetryAt.ContainsKey(missing) ? "retry-backoff" : "awaiting-admission-or-collision";
                    _siteTerrainProgress.TryGetValue(missing, out int progress);
                    Debug.LogError("[YQSemanticChunkStreamer] Site terrain dependency made no progress for " +
                        maximumNoProgressSeconds.ToString("F1") + "s. Site=" + siteContent.name +
                        " coordinate=" + missing + " requestState=" + state + " progress=" + progress +
                        " queueIndex=" + _terrainQueue.IndexOf(missing) + " queueDepth=" + _terrainQueue.Count +
                        " preparing=" + _terrainPreparingCoordinate + " painters=" + _terrainPainting.Count);
                    // note: A queued or actively sampled dependency is delayed, not failed; rejecting the site here exposed an empty visible owner while bounded terrain work was still progressing.
                    if (_terrainPreparation != null || _terrainQueued.Contains(missing) || _terrainRetryAt.ContainsKey(missing))
                    {
                        lastProgressAt = Time.realtimeSinceStartup;
                        yield return null;
                        continue;
                    }
                    completed?.Invoke(false);
                    yield break;
                }
                yield return null;
            }
            // note: A destroyed site, disabled streamer, or replaced world cannot authorize publication from an old wait.
            completed?.Invoke(false);
        }
        finally
        {
            if (epoch == _configurationEpoch)
            {
                foreach (Vector2Int coordinate in dependencies)
                {
                    if (!_siteTerrainDependencies.TryGetValue(coordinate, out int users))
                        continue;
                    if (users > 1)
                        _siteTerrainDependencies[coordinate] = users - 1;
                    else
                    {
                        _siteTerrainDependencies.Remove(coordinate);
                        _siteTerrainHandoffRequests.Remove(coordinate);
                        _siteTerrainProgress.Remove(coordinate);
                    }
                }
                // note: Released tiles return to normal distance retention; shared waiters retain their priority and pin.
                _lifecyclePending = true;
                TrimTerrainQueue();
            }
        }
    }

    private void RecordSiteTerrainProgress(Vector2Int coordinate, int progress)
    {
        // note: Record new completed stages/rows only; repeating cancelled work cannot keep a stalled site alive indefinitely.
        if (!_siteTerrainDependencies.ContainsKey(coordinate))
            return;
        _siteTerrainProgress.TryGetValue(coordinate, out int previous);
        if (progress <= previous)
            return;
        _siteTerrainProgress[coordinate] = progress;
        _terrainProgressVersion++;
    }

    private static bool IsTerrainAtWorldPosition(Terrain terrain, Vector3 worldPosition)
    {
        // note: Collision is published before appearance painting, so the renderer may be disabled while its active TerrainCollider is already authoritative.
        if (terrain == null ||
            terrain.terrainData == null ||
            !IsTerrainColliderReady(terrain))
            return false;

        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        // note: Reject out-of-bounds heightmap clamping so a site cannot be certified against a neighboring tile's edge sample.
        return worldPosition.x >= origin.x &&
            worldPosition.x <= origin.x + size.x &&
            worldPosition.z >= origin.z &&
            worldPosition.z <= origin.z + size.z;
    }

    // note: Identify whether a specific published continuation still has deferred visual painting in flight.
    public bool IsTerrainPaintPending(Terrain terrain)
    {
        if (terrain == null)
            return false;
        foreach (KeyValuePair<Vector2Int, Terrain> pair in _extendedTerrainTiles)
        {
            if (pair.Value == terrain)
                return _terrainPainting.ContainsKey(pair.Key);
        }
        return false;
    }

    public YQTerrainReadinessState GetTerrainReadiness(Vector2Int coordinate)
    {
        // note: Authored terrain is already the collision authority when its shared collider is valid; streamed terrain reports its own staged state.
        if (IsChunkInsideAuthoredTerrain(coordinate))
            return IsTerrainColliderReady(_terrain) ? YQTerrainReadinessState.CollisionReady : YQTerrainReadinessState.None;
        return _chunks.TryGetValue(coordinate, out RuntimeChunk chunk) && chunk != null
            ? chunk.terrainReadiness
            : YQTerrainReadinessState.None;
    }

    public string LifecycleDiagnostics
    {
        get
        {
            int pendingOwners = 0;
            int maximumOwnedObjects = 0;
            int pendingObjects = 0;
            foreach (RuntimeChunk chunk in _chunks.Values)
            {
                if (chunk == null)
                    continue;
                maximumOwnedObjects = Mathf.Max(maximumOwnedObjects, chunk.ownedObjects.Count);
                if (chunk.activeState == -1 || chunk.activationTarget != -1 || chunk.activationCursor > 0)
                {
                    pendingOwners++;
                    pendingObjects += Mathf.Max(0, chunk.ownedObjects.Count - chunk.activationCursor);
                }
            }
            // note: Report only bounded counters so a diagnostic request cannot enumerate or serialize the object hierarchy.
            return "pendingOwners=" + pendingOwners + ";pendingObjects=" + pendingObjects + ";maxOwnedObjects=" + maximumOwnedObjects;
        }
    }
    public int FailedChunkCount
    {
        get
        {
            int failed = 0;
            foreach (RuntimeChunk chunk in _chunks.Values)
                if (chunk != null && chunk.state == YQSemanticChunkLifecycle.Failed)
                    failed++;
            return failed;
        }
    }

    // note: Let the production-motor acceptance window isolate every synchronous fallback entry from earlier startup work.
    internal int SynchronousGroundFallbackAttemptCount => _synchronousGroundFallbackAttemptCount;

    internal int SynchronousGroundFallbackCount => _synchronousGroundFallbackCount;

    internal string ProvisionalGroundDiagnostics
    {
        get
        {
            // note: Keep movement, admission, terrain upload, and native publication timings together so a speed test identifies the frame-stall owner.
            return "tiles=" + _provisionalGroundTiles.Count +
                ",pool=" + _provisionalGroundPool.Count +
                ",tileMaxMs=" + (_maximumProvisionalGroundTileSeconds * 1000f).ToString("0.0") +
                ",tileMaxCell=" + _slowestProvisionalGroundTile +
                ",streamingStageMaxMs=" + (_maximumStreamingStageSeconds * 1000f).ToString("0.0") +
                ",streamingStage=" + _maximumStreamingStage +
                ",predictiveAdmissionMaxMs=" + (_maximumPredictiveTerrainAdmissionSeconds * 1000f).ToString("0.0") +
                ",predictiveAdmissionMaxCalls=" + _maximumPredictiveTerrainAdmissionsPerFrame +
                ",predictiveOwnerCreatesMax=" + _maximumPredictiveTerrainOwnerCreationsPerFrame +
                ",terrainSliceMaxMs=" + (_maximumTerrainSliceSeconds * 1000f).ToString("0.0") +
                ",terrainSlicePhase=" + _maximumTerrainSlicePhase +
                ",colliderPending=" + _pendingTerrainPublications.Count +
                ",colliderPendingMax=" + _maximumPendingTerrainColliderPublications +
                ",aggregateFrameMaxMs=" + (_maximumAggregateFrameWorkSeconds * 1000f).ToString("0.0") +
                ",aggregateFrameStage=" + _maximumAggregateFrameWorkStage +
                ",aggregateFrameFrame=" + _maximumAggregateFrameWorkFrame +
                ",hardViewContentDispatchReservations=" + _hardViewContentDispatchReservationCount +
                ",hardViewContentProgressReservations=" + _hardViewContentProgressReservationCount +
                ",contentSliceMaxMs=" + (_maximumContentSliceSeconds * 1000f).ToString("0.0") +
                ",contentSliceStage=" + _maximumContentSliceStage +
                ",terrainPaintSliceMaxMs=" + (_maximumTerrainPaintSliceSeconds * 1000f).ToString("0.0") +
                ",publicationSliceMaxMs=" + (_maximumPublicationSliceSeconds * 1000f).ToString("0.0") +
                ",publicationSliceStage=" + _maximumPublicationSliceStage +
                ",materializationSubstageMaxMs=" + (_maximumMaterializationSubstageSeconds * 1000f).ToString("0.0") +
                ",materializationSubstage=" + _maximumMaterializationSubstage +
                ",streamingWorkMaxMs=" + (_maximumStreamingWorkSeconds * 1000f).ToString("0.0") +
                ",queue=" + _provisionalGroundQueue.Count +
                ",sampling=" + _provisionalGroundSamplers.Count +
                ",lead=" + _provisionalGroundPrefetchDemand.Count +
                ",turnBuffer=" + _provisionalGroundTurnBufferDemand.Count +
                ",turnCoverage=" + _highSpeedTurnBufferCoverageDemand.Count +
                ",turnCoverageReadyPrefix=" + _highSpeedTurnBufferCoverageReadyCount +
                ",syncFallbackAttempts=" + _synchronousGroundFallbackAttemptCount +
                ",syncFallbacks=" + _synchronousGroundFallbackCount +
                ",cameraFallbackAttempts=" + _cameraGroundFallbackAttemptCount +
                ",movementFallbackAttempts=" + _movementGroundFallbackAttemptCount +
                ",completedSamplerFallbacks=" + _completedSamplerGroundFallbackCount +
                ",inFlightSamplerFallbacks=" + _inFlightSamplerGroundFallbackCount +
                ",missingSamplerFallbacks=" + _missingSamplerGroundFallbackCount +
                ",syncFallbackMaxMs=" + (_maximumSynchronousGroundFallbackSeconds * 1000f).ToString("0.0") +
                ",syncSampleMaxMs=" + (_maximumSynchronousGroundSamplingSeconds * 1000f).ToString("0.0") +
                ",syncPublishMaxMs=" + (_maximumSynchronousGroundPublicationSeconds * 1000f).ToString("0.0") +
                ",syncSpatialEvaluations=" + _lastSynchronousSpatialSampleEvaluations +
                ",syncSpatialCacheHits=" + _lastSynchronousSpatialSampleCacheHits +
                ",syncFallbackResolution=" + _lastSynchronousGroundFallbackResolution +
                ",syncFallbackCell=" + _lastSynchronousGroundFallbackCell +
                ",fallbackEvents=" + DescribeRecentSynchronousGroundFallbackDiagnostics() +
                ",streamerDiagnosticLogMaxMs=" + (_maximumStreamerDiagnosticLogSeconds * 1000f).ToString("0.0") +
                ",speed=" + ResolveTraversalVelocity().magnitude.ToString("0.0");
        }
    }

    private string DescribeRecentSynchronousGroundFallbackDiagnostics()
    {
        // note: Serialize only the bounded exceptional event ring when a verification report requests diagnostics.
        if (_recentSynchronousGroundFallbackDiagnosticCount <= 0)
            return "none";
        StringBuilder description = new StringBuilder(1024);
        int oldest = (_recentSynchronousGroundFallbackDiagnosticNext - _recentSynchronousGroundFallbackDiagnosticCount +
            _recentSynchronousGroundFallbackDiagnostics.Length) % _recentSynchronousGroundFallbackDiagnostics.Length;
        for (int index = 0; index < _recentSynchronousGroundFallbackDiagnosticCount; index++)
        {
            if (index > 0)
                description.Append('|');
            int slot = (oldest + index) % _recentSynchronousGroundFallbackDiagnostics.Length;
            description.Append(_recentSynchronousGroundFallbackDiagnostics[slot]);
        }
        return description.ToString();
    }

    private void RecordSynchronousGroundFallbackDiagnostic(
        Vector2Int coordinate,
        GroundFallbackSource fallbackSource,
        bool samplerEntryFound,
        bool sampleCompleted)
    {
        // note: Capture the scheduler state at the exact exceptional entry without adding recurring work to ordinary movement frames.
        Vector2 velocity = ResolveTraversalDemandVelocity();
        float predictedArrival = _semanticViewPredictionArrivalSeconds.TryGetValue(coordinate, out float arrival)
            ? arrival
            : -1f;
        _recentSynchronousGroundFallbackDiagnostics[_recentSynchronousGroundFallbackDiagnosticNext] =
            "frame=" + Time.frameCount +
            ";age=" + Time.realtimeSinceStartup.ToString("0.000", CultureInfo.InvariantCulture) +
            ";cell=" + coordinate +
            ";source=" + fallbackSource +
            ";current=" + (coordinate == _currentChunk) +
            ";visible=" + _guaranteedViewDemand.Contains(coordinate) +
            ";hard=" + _hardViewDemand.Contains(coordinate) +
            ";predicted=" + predictedArrival.ToString("0.000", CultureInfo.InvariantCulture) +
            ";turn=" + _provisionalGroundTurnBufferDemand.Contains(coordinate) +
            ";prefetch=" + _provisionalGroundPrefetchDemand.Contains(coordinate) +
            ";sampler=" + (sampleCompleted ? "completed" : samplerEntryFound ? "inFlight" : "missing") +
            ";provisionalQueue=" + _provisionalGroundQueue.Count +
            ";provisionalQueued=" + _provisionalGroundQueued.Contains(coordinate) +
            ";samplers=" + _provisionalGroundSamplers.Count +
            ";terrainPriority=" + TerrainPreparationPriority(coordinate, velocity) +
            ";terrainQueued=" + _terrainQueued.Contains(coordinate) +
            ";terrainActive=" + (_terrainPreparation != null ? _terrainPreparingCoordinate.ToString() : "none") +
            ";colliderPending=" + _pendingTerrainPublications.Count +
            ";physical=" + _physicalCount + "/" + PhysicalOwnerCapacity +
            ";speed=" + velocity.magnitude.ToString("0.0", CultureInfo.InvariantCulture);
        _recentSynchronousGroundFallbackDiagnosticNext =
            (_recentSynchronousGroundFallbackDiagnosticNext + 1) % _recentSynchronousGroundFallbackDiagnostics.Length;
        _recentSynchronousGroundFallbackDiagnosticCount = Mathf.Min(
            _recentSynchronousGroundFallbackDiagnosticCount + 1,
            _recentSynchronousGroundFallbackDiagnostics.Length);
    }

    internal bool TryEnsureCurrentCameraGround(out string failure)
    {
        using (CameraGroundCoverageMarker.Auto())
            return TryEnsureCurrentCameraGroundCore(out failure);
    }

    private bool TryEnsureCurrentCameraGroundCore(out string failure)
    {
        // note: The authoritative motor calls this after its final camera pose; background sampling is a preparation aid, never permission to render a hole.
        failure = string.Empty;
        if (!_configured || _continuousAuthority == null)
        {
            failure = "continuous ground authority is not configured";
            return false;
        }

        UpdateQueuePriorityView();
        if (!_queuePriorityViewValid || _queuePriorityCamera == null)
        {
            failure = "no active gameplay camera for ground publication";
            return false;
        }

        Vector2 playerPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z);
        if (ShouldRefreshHardViewDemand(playerPosition) || _guaranteedViewDemand.Count == 0)
        {
            // note: Cell/projection/speed changes rebuild the full envelope; sub-cell camera follow reuses its already published all-direction runway.
            RefreshHardViewDemand(playerPosition);
        }

        bool allGroundPrepared = true;
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (!EnsureGroundRepresentationImmediately(coordinate, true, GroundFallbackSource.CameraView, out failure))
            {
                allGroundPrepared = false;
                break;
            }
        }

        if (_groundSafetyPhysicsSyncPending)
        {
            // note: Batch Unity transform synchronization after the complete live view has its collider owners, rather than paying one global sync per tile.
            Physics.SyncTransforms();
            _groundSafetyPhysicsSyncPending = false;
        }

        if (!allGroundPrepared)
            return false;

        return TryValidateGroundViewCoverage(out failure);
    }

    public bool TryValidateRequiredCoverage(out string failure)
    {
        // note: Validate the same terrain frontier that is provisioned, including the visible lookahead ring.
        int radius = TerrainCoverageRadius;
        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int coordinate = new Vector2Int(_currentChunk.x + x, _currentChunk.y + z);
                if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
                {
                    failure = "missing required chunk " + coordinate;
                    return false;
                }
                if (chunk.state == YQSemanticChunkLifecycle.Failed)
                {
                    failure = "required chunk failed " + coordinate +
                        (string.IsNullOrWhiteSpace(chunk.failureReason) ? string.Empty : ": " + chunk.failureReason);
                    return false;
                }
                if (!IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
                {
                    failure = "required chunk has no terrain " + coordinate;
                    return false;
                }
            }
        }
        failure = string.Empty;
        return true;
    }

    public bool TryValidateVisualCoverage(out string failure)
    {
        int incompleteVisibleChunks;
        return TryValidateVisualCoverageCore(true, true, out incompleteVisibleChunks, out failure);
    }

    internal bool TryValidateCurrentVisualCoverage(out string failure)
    {
        // note: Check the live camera frustum without refreshing scheduler demand so per-frame qualification observes visibility without changing streaming behavior.
        int incompleteVisibleChunks;
        return TryValidateVisualCoverageCore(true, false, out incompleteVisibleChunks, out failure);
    }

    internal bool TryValidateGroundViewCoverage(out string failure)
    {
        // note: Expose the real-time ground contract separately from full semantic dressing so high-speed tests can prove there are no visible ground/collider holes.
        if (_guaranteedViewDemand.Count == 0)
        {
            failure = "live camera ground envelope is empty";
            return false;
        }
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
                !IsTraversableChunk(coordinate, chunk))
            {
                bool provisionalTerrainReady = _provisionalGroundTiles.TryGetValue(coordinate, out Terrain missingGround) &&
                    IsTerrainColliderReady(missingGround);
                bool provisionalQueued = _provisionalGroundQueued.Contains(coordinate);
                bool provisionalSampling = _provisionalGroundSamplers.TryGetValue(coordinate, out ProvisionalGroundSampling sampling);
                failure = "visible ground collider is not ready " + coordinate + " provisional{" +
                    ProvisionalGroundDiagnostics + "} cell{tile=" + provisionalTerrainReady +
                    ",queued=" + provisionalQueued + ",sampling=" + provisionalSampling +
                    ",lead=" + _provisionalGroundPrefetchDemand.Contains(coordinate) +
                    ",turnBuffer=" + _provisionalGroundTurnBufferDemand.Contains(coordinate) +
                    ",hardView=" + _hardViewDemand.Contains(coordinate) +
                    ",sampleComplete=" + (provisionalSampling && sampling != null && sampling.task != null && sampling.task.IsCompleted) +
                    ",canonical=" + HasPublishedTerrain(coordinate) + "}";
                return false;
            }
            if (IsChunkInsideAuthoredTerrain(coordinate))
            {
                if (_terrain == null || !_terrain.enabled || !_terrain.gameObject.activeInHierarchy)
                {
                    failure = "visible authored ground renderer is disabled " + coordinate;
                    return false;
                }
                continue;
            }
            bool canonicalGroundVisible = _extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) &&
                terrain != null && terrain.enabled && terrain.gameObject.activeInHierarchy;
            bool provisionalGroundVisible = _provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisional) &&
                IsTerrainColliderReady(provisional) && provisional.enabled;
            if (!canonicalGroundVisible && !provisionalGroundVisible)
            {
                // note: Capture cell-local readiness only on failure so stale-map and late-camera gaps can be distinguished without per-frame logging.
                TerrainCollider canonicalCollider = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
                bool provisionalMapped = _provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisionalDiagnostic) &&
                    provisionalDiagnostic != null;
                TerrainCollider provisionalCollider = provisionalMapped
                    ? provisionalDiagnostic.GetComponent<TerrainCollider>()
                    : null;
                failure = "visible streamed ground renderer is not ready " + coordinate + " provisional{" +
                    ProvisionalGroundDiagnostics + "} cell{canonicalMapped=" + (terrain != null) +
                    ",canonicalData=" + (terrain != null && terrain.terrainData != null) +
                    ",canonicalCollider=" + (canonicalCollider != null && canonicalCollider.enabled) +
                    ",canonicalEnabled=" + (terrain != null && terrain.enabled) +
                    ",canonicalActive=" + (terrain != null && terrain.gameObject.activeInHierarchy) +
                    ",provisionalMapped=" + provisionalMapped +
                    ",provisionalCollider=" + (provisionalCollider != null && provisionalCollider.enabled) +
                    ",provisionalEnabled=" + (provisionalMapped && provisionalDiagnostic.enabled) +
                    ",provisionalActive=" + (provisionalMapped && provisionalDiagnostic.gameObject.activeInHierarchy) +
                    ",ownerReadiness=" + chunk.terrainReadiness + ",ownerState=" + chunk.state +
                    ",activationComplete=" + chunk.activationComplete + ",activeState=" + chunk.activeState +
                    ",visualStateKnown=" + chunk.visualStateKnown + ",visualState=" + chunk.visualState +
                    ",guaranteed=" + _guaranteedViewDemand.Contains(coordinate) +
                    ",hardView=" + _hardViewDemand.Contains(coordinate) + "}";
                return false;
            }
        }
        failure = string.Empty;
        return true;
    }

    public bool TryValidateVisiblePublicationWorkContracts(out int incompleteVisibleChunks, out string failure)
    {
        // note: Report an incomplete view only when each demanded stage still has its own live publication lane; this is an observation and does not refresh scheduler demand.
        return TryValidateVisualCoverageCore(false, false, out incompleteVisibleChunks, out failure);
    }

    public bool TryValidateDemandedPublicationWorkContracts(out int demandedChunks, out string failure)
    {
        // note: Include background preparation in the work-ownership check without requiring it to satisfy visible-frustum publication.
        demandedChunks = 0;
        foreach (Vector2Int coordinate in _hardViewDemand)
        {
            demandedChunks++;
            if (!TryValidatePublicationWorkContract(coordinate, out failure))
                return false;
        }
        foreach (Vector2Int coordinate in _contentDemand)
        {
            if (_hardViewDemand.Contains(coordinate))
                continue;
            demandedChunks++;
            if (!TryValidatePublicationWorkContract(coordinate, out failure))
                return false;
        }
        failure = string.Empty;
        return true;
    }

    private RuntimeChunk _lastVisualCoverageFailureOwner;
    private Vector2Int _lastVisualCoverageFailureCoordinate;
    private bool _hasLastVisualCoverageFailureCoordinate;

    // note: Only first-failure receipts and the existing bounded heartbeat format owner progress.
    internal string DescribeLastVisualCoverageOwnerProgress()
    {
        RuntimeChunk chunk = _lastVisualCoverageFailureOwner;
        string activeContentWorkers = DescribeActiveContentGenerationProgress();
        string appearanceWorkAge = chunk != null && _terrainPainting.ContainsKey(_lastVisualCoverageFailureCoordinate) &&
            chunk.appearanceWorkStartedAt >= 0d
            ? (Time.realtimeSinceStartupAsDouble - chunk.appearanceWorkStartedAt).ToString("0.000")
            : "n/a";
        // note: Terrain dispatch wait reasons are recorded before content generation begins, so keep that evidence visible without an owner-progress epoch.
        string terrainDispatchWait = chunk == null
            ? " terrainDispatchWait{unavailable}"
            : " terrainDispatchWait{frames=" + chunk.terrainDispatchWaitFrames +
                ",active=" + chunk.terrainDispatchActiveFrames +
                ",publication=" + chunk.terrainDispatchPublicationFrames +
                ",painters=" + chunk.terrainDispatchPainterFrames +
                ",admission=" + chunk.terrainDispatchAdmissionFrames +
                ",capacity=" + chunk.terrainDispatchCapacityFrames +
                ",otherCandidate=" + chunk.terrainDispatchOtherCandidateFrames +
                ",started=" + chunk.terrainDispatchStartedFrames +
                ",lastQueueIndex=" + chunk.lastTerrainDispatchQueueIndex + "}";
        // note: At the first visual miss, identify whether the bounded painter slots or a scheduled retry owns the missing appearance stage.
        _terrainAppearanceRetryCount.TryGetValue(_lastVisualCoverageFailureCoordinate, out int appearanceRetryCount);
        bool appearanceRetryScheduled = _terrainAppearanceRetryAt.TryGetValue(
            _lastVisualCoverageFailureCoordinate,
            out float appearanceRetryAt);
        StringBuilder appearancePainters = new StringBuilder(96);
        foreach (KeyValuePair<Vector2Int, Coroutine> painter in _terrainPainting)
        {
            if (appearancePainters.Length > 0)
                appearancePainters.Append(';');
            RuntimeChunk painterOwner = _chunks.TryGetValue(painter.Key, out RuntimeChunk candidate)
                ? candidate
                : null;
            appearancePainters.Append(painter.Key)
                .Append(":age=")
                .Append(painterOwner != null && painterOwner.appearanceWorkStartedAt >= 0d
                    ? (Time.realtimeSinceStartupAsDouble - painterOwner.appearanceWorkStartedAt).ToString("0.000", CultureInfo.InvariantCulture)
                    : "n/a")
                .Append(":demand=")
                .Append(_guaranteedViewDemand.Contains(painter.Key) ? "visible" :
                    _hardViewDemand.Contains(painter.Key) ? "hard" :
                    IsContentDemandedNow(painter.Key) ? "content" : "optional");
        }
        string appearanceAdmission = " appearanceAdmission{retryCount=" + appearanceRetryCount +
            ",retryRemaining=" + (appearanceRetryScheduled
                ? Mathf.Max(0f, appearanceRetryAt - Time.unscaledTime).ToString("0.000", CultureInfo.InvariantCulture)
                : "n/a") +
            ",painters=" + _terrainPainting.Count + "/" + MaximumTerrainPaintingWorkers +
            ",failure=" + (chunk != null && !string.IsNullOrWhiteSpace(chunk.failureReason)
                ? chunk.failureReason
                : "<none>") +
            ",owners=" + (appearancePainters.Length > 0 ? appearancePainters.ToString() : "<none>") + "}";
        string appearanceLaneProgress = chunk == null
            ? " appearanceLanes{unavailable}"
            : " appearanceLanes{biome{" + chunk.biomeAppearanceProgress.Describe(Time.realtimeSinceStartupAsDouble) +
              "},detail{" + chunk.detailAppearanceProgress.Describe(Time.realtimeSinceStartupAsDouble) +
              (string.IsNullOrEmpty(chunk.appearanceDetailSubstage) ? string.Empty : ",substage=" + chunk.appearanceDetailSubstage) + "}}";
        if (chunk == null || chunk.progressStartedAt < 0d)
            return " ownerProgress{unavailable}" + terrainDispatchWait + appearanceAdmission + appearanceLaneProgress +
                " appearanceWorkAge=" + appearanceWorkAge + activeContentWorkers;
        double now = Time.realtimeSinceStartupAsDouble;
        if (chunk.progressEpoch != chunk.contentOwnerEpoch)
            return " ownerProgress{staleEpoch=" + chunk.progressEpoch + ",currentEpoch=" + chunk.contentOwnerEpoch +
                ",attempt=" + chunk.contentAttemptCount + "}" + terrainDispatchWait + appearanceAdmission +
                appearanceLaneProgress + " appearanceWorkAge=" + appearanceWorkAge + activeContentWorkers;
        return " ownerProgress{epoch=" + chunk.progressEpoch + ",currentEpoch=" + chunk.contentOwnerEpoch +
            ",attempt=" + chunk.contentAttemptCount + ",restarts=" + Math.Max(0, chunk.contentAttemptCount - 1) +
            ",hardViewReserveSlices=" + chunk.hardViewProgressReserveSlices +
            ",hardViewReserveDeniedFrames=" + chunk.hardViewProgressReserveDeniedFrames +
            ",hardViewReserveYieldedToLiveFrames=" + chunk.hardViewProgressYieldedToLiveFrames +
            ",start=" + chunk.progressStartedAt.ToString("0.000") + ",age=" + (now - chunk.progressStartedAt).ToString("0.000") +
            ",epochHardAge=" + (chunk.epochHardDemandAt < 0d ? "n/a" : (now - chunk.epochHardDemandAt).ToString("0.000")) +
            ",hardAtStart=" + chunk.hardDemandReadyAtStart +
            ",epochCollisionAge=" + (chunk.epochCollisionAt < 0d ? "n/a" : (now - chunk.epochCollisionAt).ToString("0.000")) +
            ",collisionAtStart=" + chunk.collisionReadyAtStart +
            ",preemptions=" + chunk.diagnosticPreemptionCount + ",lastPreemptedEpoch=" + chunk.lastPreemptedEpoch +
            ",lastPreemptedAge=" + (chunk.lastPreemptedAt < 0d ? "n/a" : (now - chunk.lastPreemptedAt).ToString("0.000")) +
            ",preemptedBy=" + chunk.lastPreemptingCoordinate +
            ",structure{" + chunk.structuralProgress.Describe(now) + "},ecology{" + chunk.ecologyProgress.Describe(now) + "}}" +
            terrainDispatchWait + appearanceAdmission + appearanceLaneProgress + " appearanceWorkAge=" + appearanceWorkAge +
            activeContentWorkers;
    }

    private string DescribeActiveContentGenerationProgress()
    {
        // note: At a first visible miss, identify whether each bounded content worker is progressing structure or waiting for its terrain prerequisite.
        if (_activeGenerations.Count == 0)
            return " activeContentWorkers{none}";
        double now = Time.realtimeSinceStartupAsDouble;
        StringBuilder workers = new StringBuilder(512);
        int recorded = 0;
        foreach (KeyValuePair<Vector2Int, Coroutine> pair in _activeGenerations)
        {
            if (recorded >= MaximumContentGenerationWorkers)
                break;
            RuntimeChunk owner = _chunks.TryGetValue(pair.Key, out RuntimeChunk current) ? current : null;
            if (recorded++ > 0)
                workers.Append(';');
            workers.Append(pair.Key)
                .Append(":state=").Append(owner == null ? "missing" : owner.state.ToString())
                .Append(":terrainReady=").Append(ResolveTerrainForContentMaterialization(pair.Key) != null)
                .Append(":visible=").Append(_guaranteedViewDemand.Contains(pair.Key))
                .Append(":hard=").Append(_hardViewDemand.Contains(pair.Key))
                .Append(":predicted=").Append(_semanticViewPredictionArrivalSeconds.TryGetValue(pair.Key, out float arrival))
                .Append(":arrival=").Append(_semanticViewPredictionArrivalSeconds.TryGetValue(pair.Key, out arrival)
                    ? arrival.ToString("0.000", CultureInfo.InvariantCulture)
                    : "n/a")
                .Append(":queuedAge=").Append(owner == null || owner.contentQueuedAt < 0f
                    ? "n/a"
                    : (Time.unscaledTime - owner.contentQueuedAt).ToString("0.000", CultureInfo.InvariantCulture))
                .Append(":startedAge=").Append(owner == null || owner.contentStartedAt < 0f
                    ? "n/a"
                    : (Time.unscaledTime - owner.contentStartedAt).ToString("0.000", CultureInfo.InvariantCulture))
                .Append(":structure{").Append(owner == null
                    ? "unavailable"
                    : owner.structuralProgress.Describe(now))
                .Append('}');
        }
        return " activeContentWorkers{" + workers + "}";
    }

    private bool TryValidateVisualCoverageCore(
        bool requireFullPublication,
        bool refreshSchedulerView,
        out int incompleteVisibleChunks,
        out string failure)
    {
        incompleteVisibleChunks = 0;
        _lastVisualCoverageFailureOwner = null;
        _hasLastVisualCoverageFailureCoordinate = false;
        // note: Validate every chunk intersecting the live camera frustum, including terrain paint and complete activation; cells outside the frustum are not falsely treated as visible.
        // note: Reject a qualification setup whose camera far clip cannot display the promised visual distance.
        if (QualificationCameraFarClipMeters < GuaranteedVisualDistanceMeters)
        {
            failure = "camera far clip is shorter than guaranteed visual distance";
            return false;
        }
        if (refreshSchedulerView)
            UpdateQueuePriorityView();
        if (!_queuePriorityViewValid || _queuePriorityCamera == null)
        {
            failure = "no active gameplay camera for visual coverage";
            return false;
        }
        GeometryUtility.CalculateFrustumPlanes(_queuePriorityCamera, _queuePriorityFrustumPlanes);
        Vector2 playerPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        // note: Camera validation can run from the motor before this streamer's LateUpdate; refresh the live frustum first so a newly exposed cell cannot be certified against the previous camera demand.
        // note: The all-direction published envelope covers sub-cell camera turns; bulk admission changes only when the view leaves that envelope or its projection expands.
        if (refreshSchedulerView && ShouldRefreshHardViewDemand(playerPosition))
            RefreshHardViewDemand(playerPosition);
        // note: Validate from the live camera cell, not the last completed player frontier; the camera can lead the player by a third-person offset or during a high-speed boundary transition.
        Vector2Int cameraChunk = ChunkFor(new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z));
        int radius = GuaranteedViewChunkRadius + 1;
        bool testedVisibleChunk = false;
        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int coordinate = new Vector2Int(cameraChunk.x + x, cameraChunk.y + z);
                if (!IsChunkInGuaranteedView(coordinate, playerPosition, Mathf.Max(32f, chunkWorldSize)))
                    continue;
                testedVisibleChunk = true;
                if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
                {
                    failure = "missing visual chunk " + coordinate;
                    return false;
                }
                if (!TryValidatePublicationWorkContract(coordinate, out string publicationWorkFailure))
                {
                    failure = publicationWorkFailure;
                    return false;
                }
                    bool fullyLoaded = IsFullyLoadedForView(coordinate, chunk);
                    bool traversable = IsTraversableChunk(coordinate, chunk);
                    if (!fullyLoaded || !traversable)
                    {
                    incompleteVisibleChunks++;
                    if (!requireFullPublication)
                    {
                        if (TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot pendingSnapshot) &&
                            pendingSnapshot.traversable)
                        {
                            failure = "visible chunk failed view readiness while reporting traversable " + coordinate;
                            return false;
                        }
                        continue;
                    }
                    // note: Capture demand and prediction state at the first visible miss to separate late camera admission from exhausted streaming capacity.
                    bool currentlyGuaranteed = _guaranteedViewDemand.Contains(coordinate);
                    bool currentlyHardDemanded = _hardViewDemand.Contains(coordinate);
                    bool hasPredictedArrival = _semanticViewPredictionArrivalSeconds.TryGetValue(
                        coordinate, out float predictedArrivalSeconds);
                    Vector2 failureTraversalVelocity = ResolveTraversalVelocity();
                    int failureTerrainPriority = TerrainPreparationPriority(coordinate, failureTraversalVelocity);
                    int failureQueueHeadPriority = _terrainQueue.Count > 0
                        ? TerrainPreparationPriority(_terrainQueue[0], failureTraversalVelocity)
                        : -1;
                    bool failureHasActiveTerrainPreparation = _terrainPreparation != null;
                    int failureActiveTerrainPriority = failureHasActiveTerrainPreparation
                        ? TerrainPreparationPriority(_terrainPreparingCoordinate, failureTraversalVelocity)
                        : -1;
                    float failureActiveTerrainAgeSeconds = failureHasActiveTerrainPreparation
                        ? Mathf.Max(0f, Time.realtimeSinceStartup - _terrainPreparationStartedAt)
                        : 0f;
                    bool failureTerrainQueued = _terrainQueued.Contains(coordinate);
                    bool failureTerrainActive = _terrainPreparation != null && coordinate == _terrainPreparingCoordinate;
                    bool failureTerrainPublicationPending = _pendingTerrainPublications.ContainsKey(coordinate);
                    bool failurePhysicalDemand = _physicalDemand.ContainsKey(coordinate);
                    string failureTerrainRequestAge = _terrainRequestedAt.TryGetValue(
                        coordinate, out float terrainRequestedAt)
                        ? Mathf.Max(0f, Time.unscaledTime - terrainRequestedAt).ToString("0.000")
                        : "n/a";
                    string workDiagnostics = string.Empty;
                    if (TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot workSnapshot))
                    {
                        workDiagnostics =
                            " work=terrain:" + workSnapshot.terrainWorkActive +
                            "/retry:" + workSnapshot.terrainRetryScheduled +
                            ";content:" + workSnapshot.contentWorkActive + "/workId:" + workSnapshot.contentWorkId +
                            "/queued:" + workSnapshot.contentWorkQueued +
                            ";appearance:" + workSnapshot.appearanceWorkActive +
                            "/retry:" + workSnapshot.appearanceRetryScheduled +
                            ";ecology:" + workSnapshot.ecologyWorkActive + "/workId:" + workSnapshot.ecologyWorkId +
                            "/phase:" + (workSnapshot.ecologyWorkPhase ?? string.Empty) +
                            "/phaseAge:" + workSnapshot.ecologyWorkPhaseElapsedSeconds.ToString("0.000") +
                            "/retry:" + workSnapshot.ecologyRetryScheduled +
                            ";lifecycle:" + workSnapshot.lifecycleWorkPending;
                    }
                    // note: Snapshot only the first failing view owner so queue delay can be separated from worker saturation without per-frame diagnostics.
                    int failureContentQueueIndex = _queue.IndexOf(coordinate);
                    string failureContentQueueHead = _queue.Count > 0
                        ? _queue[0] + ":priority=" + CompareContentQueuePriority(coordinate, _queue[0], failureTraversalVelocity)
                        : "<none>";
                    StringBuilder failureActiveContent = new StringBuilder(192);
                    int failureActiveContentCount = 0;
                    foreach (KeyValuePair<Vector2Int, Coroutine> activeContent in _activeGenerations)
                    {
                        if (failureActiveContentCount >= MaximumContentGenerationWorkers)
                            break;
                        if (failureActiveContentCount > 0)
                            failureActiveContent.Append(';');
                        bool activeChunkFound = _chunks.TryGetValue(activeContent.Key, out RuntimeChunk activeChunk);
                        float activeContentAge = activeChunkFound && activeChunk != null && activeChunk.contentStartedAt >= 0f
                            ? Mathf.Max(0f, Time.unscaledTime - activeChunk.contentStartedAt)
                            : -1f;
                        bool activeHasArrival = _semanticViewPredictionArrivalSeconds.TryGetValue(
                            activeContent.Key, out float activeArrivalSeconds);
                        // note: Compare each occupied lane with the failed visible cell so the receipt proves whether the existing strict preemption rule could reclaim it.
                        bool activeWorkerPreemptionEligible = activeChunkFound && activeChunk != null &&
                            !activeChunk.physicalRepresentation &&
                            activeChunk.state == YQSemanticChunkLifecycle.Generating &&
                            activeContent.Key != _currentChunk;
                        failureActiveContent.Append(activeContent.Key)
                            .Append(":state=").Append(activeChunkFound && activeChunk != null ? activeChunk.state.ToString() : "missing")
                            .Append(",view=").Append(_guaranteedViewDemand.Contains(activeContent.Key))
                            .Append(",hard=").Append(_hardViewDemand.Contains(activeContent.Key))
                            .Append(",physical=").Append(activeChunkFound && activeChunk != null && activeChunk.physicalRepresentation)
                            .Append(",current=").Append(activeContent.Key == _currentChunk)
                            .Append(",preemptible=").Append(activeWorkerPreemptionEligible)
                            .Append(",deadlineVsFailure=").Append(CompareContentQueueDeadlines(coordinate, activeContent.Key, failureTraversalVelocity))
                            .Append(",priorityVsFailure=").Append(CompareContentQueuePriority(coordinate, activeContent.Key, failureTraversalVelocity))
                            .Append(",arrival=").Append(activeHasArrival
                                ? activeArrivalSeconds.ToString("0.000")
                                : "n/a")
                            .Append(",age=").Append(activeContentAge >= 0f
                                ? activeContentAge.ToString("0.000")
                                : "n/a");
                        failureActiveContentCount++;
                    }
                    _lastVisualCoverageFailureOwner = chunk;
                    _lastVisualCoverageFailureCoordinate = coordinate;
                    _hasLastVisualCoverageFailureCoordinate = true;
                    string rendererPublicationBlocker = !fullyLoaded &&
                        IsReadyForViewActivation(coordinate, chunk) &&
                        chunk.activationComplete && chunk.activeState == 1
                        ? DescribePublishedRendererBlocker(chunk)
                        : string.Empty;
                    string readinessBlocker = fullyLoaded ? string.Empty :
                        DescribeViewActivationBlocker(coordinate, chunk);
                    failure = "visual chunk is not fully loaded " + coordinate +
                        " stageAges{hard=" + (chunk.hardViewAdmittedAt >= 0f ? (Time.unscaledTime - chunk.hardViewAdmittedAt).ToString("0.000") : "n/a") +
                        ",collision=" + (chunk.collisionReadyAt >= 0f ? (Time.unscaledTime - chunk.collisionReadyAt).ToString("0.000") : "n/a") +
                        ",contentQueue=" + (chunk.contentQueuedAt >= 0f ? (Time.unscaledTime - chunk.contentQueuedAt).ToString("0.000") : "n/a") +
                        ",contentStart=" + (chunk.contentStartedAt >= 0f ? (Time.unscaledTime - chunk.contentStartedAt).ToString("0.000") : "n/a") +
                        ",content=" + (chunk.contentReadyAt >= 0f ? (Time.unscaledTime - chunk.contentReadyAt).ToString("0.000") : "n/a") +
                        ",ecology=" + (chunk.ecologyStartedAt >= 0f ? (Time.unscaledTime - chunk.ecologyStartedAt).ToString("0.000") : "n/a") + "}" +
                        " traversable=" + traversable +
                        " terrain=" + chunk.terrainReadiness +
                        " required=" + chunk.requiredContentReady +
                        " overlays=" + chunk.overlayReady +
                        " appearance=" + chunk.appearanceReady +
                        " ecology=" + chunk.requiredEcologyReady +
                        " visual=" + chunk.visualReady +
                        " state=" + chunk.state +
                        " activation=" + chunk.activationComplete +
                        " activeState=" + chunk.activeState +
                        (string.IsNullOrEmpty(rendererPublicationBlocker)
                            ? string.Empty
                            : " rendererBlocker=" + rendererPublicationBlocker) +
                        (string.IsNullOrEmpty(readinessBlocker)
                            ? string.Empty
                            : " readinessBlocker=" + readinessBlocker) +
                        " demand{guaranteed=" + currentlyGuaranteed +
                        ",hard=" + currentlyHardDemanded +
                        ",predicted=" + hasPredictedArrival +
                        ",arrivalSeconds=" + (hasPredictedArrival ? predictedArrivalSeconds.ToString("0.000") : "n/a") +
                        ",camera=" + _queuePriorityCameraPosition.ToString("F1") +
                        ",player=" + (_player != null ? _player.position.ToString("F1") : "n/a") +
                        ",velocity=" + failureTraversalVelocity.ToString("F1") +
                        ",physical=" + _physicalCount + "/" + PhysicalOwnerCapacity +
                        ",terrainPending=" + PendingTerrainCollisionCount +
                        ",contentQueue=" + _queue.Count + "}" +
                        " terrainAdmission{targetPriority=" + failureTerrainPriority +
                        ",targetQueued=" + failureTerrainQueued +
                        ",targetActive=" + failureTerrainActive +
                        ",targetColliderPending=" + failureTerrainPublicationPending +
                        ",targetRequestAge=" + failureTerrainRequestAge +
                        ",physicalDemand=" + failurePhysicalDemand +
                        ",queueHead=" + (_terrainQueue.Count > 0 ? _terrainQueue[0].ToString() : "<none>") +
                        ",queueHeadPriority=" + failureQueueHeadPriority +
                        // note: Capture the selected tile and single-slot owner at the exact visible miss to distinguish priority waiting from throughput delay.
                        ",activePriority=" + failureActiveTerrainPriority +
                        ",activeAge=" + (failureHasActiveTerrainPreparation
                            ? failureActiveTerrainAgeSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                            : "n/a") +
                        ",active=" + (_terrainPreparation != null
                            ? _terrainPreparingCoordinate + ":" + _terrainPreparationPhase
                            : "<none>") +
                        ",colliderTickets=" + DescribePendingTerrainPublications() + "}" +
                        " contentAdmission{targetIndex=" + failureContentQueueIndex +
                        ",targetVersusHead=" + failureContentQueueHead +
                        ",workers=" + _activeGenerations.Count +
                        ",active=" + (failureActiveContent.Length > 0 ? failureActiveContent.ToString() : "<none>") + "}" +
                        " activationTarget=" + chunk.activationTarget +
                        " activationCursor=" + chunk.activationCursor +
                        " owned=" + chunk.ownedObjects.Count +
                        " ecologyFrame{" + YQGeneratedWorldEnvironment.DescribeRequiredEcologyFrameAttribution(Time.frameCount) + "}" +
                        workDiagnostics +
                        (string.IsNullOrWhiteSpace(chunk.failureReason) ? string.Empty :
                            " failure=" + chunk.failureReason);
                    return false;
                }
            }
        }
        if (!testedVisibleChunk)
        {
            failure = "camera frustum contains no qualified chunk";
            return false;
        }

        failure = string.Empty;
        return true;
    }

    public bool TryValidatePreparedViewEnvelope(out string failure)
    {
        // note: Startup and explicit qualification use the same fully-published contract for the ahead-of-view ring that gameplay uses for the live frustum.
        if (!TryValidateVisualCoverage(out failure))
            return false;
        if (_guaranteedViewDemand.Count == 0)
        {
            failure = "live camera view envelope is empty";
            return false;
        }
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            {
                failure = "missing prepared view chunk " + coordinate;
                return false;
            }
            if (IsFullyLoadedForView(coordinate, chunk))
                continue;
            string publicationWorkDiagnostics = string.Empty;
            if (TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot publicationSnapshot))
            {
                // note: Include the live queue/worker state in startup failures so a missing visible cell identifies its blocked publication lane rather than only its readiness flags.
                publicationWorkDiagnostics =
                    " physical=" + publicationSnapshot.physicalRepresentation +
                    " demanded=" + publicationSnapshot.demanded +
                    " work=terrain:" + publicationSnapshot.terrainWorkActive +
                    "/retry:" + publicationSnapshot.terrainRetryScheduled +
                    ";content:" + publicationSnapshot.contentWorkActive + "/workId:" + publicationSnapshot.contentWorkId +
                    "/queued:" + publicationSnapshot.contentWorkQueued +
                    ";appearance:" + publicationSnapshot.appearanceWorkActive +
                    "/retry:" + publicationSnapshot.appearanceRetryScheduled +
                    ";ecology:" + publicationSnapshot.ecologyWorkActive +
                    "/retry:" + publicationSnapshot.ecologyRetryScheduled +
                    ";lifecycle:" + publicationSnapshot.lifecycleWorkPending +
                    " activeContentWorkers=" + _activeGenerations.Count +
                    " queueDepth=" + _queue.Count +
                    " activeContent=" + DescribeActiveContentWorkers();
            }
            failure = "prepared view chunk is not fully loaded " + coordinate +
                " terrain=" + chunk.terrainReadiness +
                " required=" + chunk.requiredContentReady +
                " overlays=" + chunk.overlayReady +
                " appearance=" + chunk.appearanceReady +
                " ecology=" + chunk.requiredEcologyReady +
                " activation=" + chunk.activationComplete +
                " state=" + chunk.state +
                publicationWorkDiagnostics;
            return false;
        }
        if (!TryValidateCanonicalPreparationEnvelope(out failure))
            return false;
        // note: Include full content in the one-second all-direction buffer so an immediate speed jump or reversal cannot reveal provisional-only cells.
        return TryValidateHighSpeedTurnBufferCoverage(out failure);
    }

    private bool TryValidateCanonicalPreparationEnvelope(out string failure)
    {
        // note: Do not release gameplay while the all-direction lead ring is only queued or represented by emergency ground.
        if (_canonicalPreparationDemand.Count == 0)
        {
            failure = "all-direction canonical preparation envelope is empty";
            return false;
        }
        foreach (Vector2Int coordinate in _canonicalPreparationDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            {
                failure = "missing all-direction canonical preparation chunk " + coordinate;
                return false;
            }
            // note: Offscreen lead owners stay retained by lifecycle policy; require traversable prepared publication, while visible owners still need completed activation.
            bool currentlyVisible = _guaranteedViewDemand.Contains(coordinate);
            bool preparationReady = currentlyVisible
                ? IsFullyLoadedForView(coordinate, chunk)
                : IsReadyForCanonicalPreparation(coordinate, chunk);
            if (preparationReady)
                continue;

            string work = string.Empty;
            if (TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot))
            {
                // note: Distinguish live publication stages from explicit terminal failure so startup exposes the owning rejection instead of a generic wait.
                work = " physical=" + snapshot.physicalRepresentation +
                    " work=terrain:" + snapshot.terrainWorkActive + "/queued:" + snapshot.terrainWorkQueued +
                    ";content:" + snapshot.contentWorkActive + "/queued:" + snapshot.contentWorkQueued +
                    ";appearance:" + snapshot.appearanceWorkActive +
                    ";ecology:" + snapshot.ecologyWorkActive +
                    ";activation:" + snapshot.activationWorkActive +
                    ";lifecycle:" + snapshot.lifecycleWorkPending +
                    ";failure:" + (snapshot.hasExplicitFailure
                        ? snapshot.failureReason
                        : "none");
            }
            failure = "all-direction canonical preparation chunk is not fully loaded " + coordinate +
                " terrain=" + chunk.terrainReadiness +
                " required=" + chunk.requiredContentReady +
                " overlays=" + chunk.overlayReady +
                " appearance=" + chunk.appearanceReady +
                " ecology=" + chunk.requiredEcologyReady +
                " activation=" + chunk.activationComplete +
                " state=" + chunk.state + work;
            return false;
        }
        failure = string.Empty;
        return true;
    }

    internal bool TryValidateHighSpeedTurnBufferCoverage(out string failure)
    {
        // note: Validate the full geometric safety envelope rather than only cells needing provisional work; canonical ownership must never make the witness empty.
        _highSpeedTurnBufferCoverageReadyCount = 0;
        if (_highSpeedTurnBufferCoverageDemand.Count == 0)
        {
            failure = "high-speed turn-buffer coverage envelope is empty";
            return false;
        }

        // note: Every off-screen cell must already have traversable same-authority ground, while currently visible cells must be fully published before rendering.
        foreach (Vector2Int coordinate in _highSpeedTurnBufferCoverageDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            {
                failure = "missing high-speed turn-buffer coverage chunk " + coordinate +
                    " ready=" + _highSpeedTurnBufferCoverageReadyCount + "/" + _highSpeedTurnBufferCoverageDemand.Count;
                return false;
            }
            bool currentlyVisible = _guaranteedViewDemand.Contains(coordinate);
            bool readyForImmediateActivation = currentlyVisible
                ? IsFullyLoadedForView(coordinate, chunk)
                : IsTraversableChunk(coordinate, chunk);
            if (readyForImmediateActivation)
            {
                _highSpeedTurnBufferCoverageReadyCount++;
                continue;
            }

            string publicationWorkDiagnostics = string.Empty;
            if (TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot publicationSnapshot))
            {
                publicationWorkDiagnostics =
                    " physical=" + publicationSnapshot.physicalRepresentation +
                    " demanded=" + publicationSnapshot.demanded +
                    " work=terrain:" + publicationSnapshot.terrainWorkActive +
                    ";content:" + publicationSnapshot.contentWorkActive + "/workId:" + publicationSnapshot.contentWorkId +
                    "/queued:" + publicationSnapshot.contentWorkQueued +
                    ";appearance:" + publicationSnapshot.appearanceWorkActive +
                    ";ecology:" + publicationSnapshot.ecologyWorkActive + "/workId:" + publicationSnapshot.ecologyWorkId +
                    ";lifecycle:" + publicationSnapshot.lifecycleWorkPending;
            }
            // note: A turn-buffer gate can wait for terrain while the chunk itself has no worker; expose the bounded global slots and queue head to locate that starvation.
            publicationWorkDiagnostics += " scheduler{contentQueue=" + _queue.Count +
                ";contentWorkers=" + _activeGenerations.Count + "/" + MaximumContentGenerationWorkers +
                ";contentCells=" + DescribeActiveContentWorkers() +
                ";terrainQueueDepth=" + PendingTerrainCollisionCount +
                ";terrainQueued=" + _terrainQueue.Count +
                ";terrainActive=" + (_terrainPreparation != null ? 1 : 0) +
                ";colliderPending=" + _pendingTerrainPublications.Count +
                ";v2=" + YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan) +
                ";blueprint=" + (_acceptedBlueprint != null) +
                ";targetTerrainList=" + _terrainQueue.Contains(coordinate) +
                ";targetTerrainSet=" + _terrainQueued.Contains(coordinate) +
                ";targetTerrainPriority=" + TerrainPreparationPriority(coordinate, ResolveTraversalVelocity()) +
                ";terrainPainters=" + _terrainPainting.Count +
                ";priorityTerrainPending=" + HasQueuedPriorityTerrain() +
                ";aggregate=" + _aggregateFrameWorkSeconds.ToString("0.000") + "/" + AggregateMainThreadBudgetSeconds.ToString("0.000") +
                ";terrain{" + TerrainAdmissionDiagnostics + "}}";
            failure = "high-speed turn-buffer chunk is not traversable before entering view " + coordinate +
                " ready=" + _highSpeedTurnBufferCoverageReadyCount + "/" + _highSpeedTurnBufferCoverageDemand.Count +
                " visible=" + currentlyVisible +
                " terrain=" + chunk.terrainReadiness +
                " required=" + chunk.requiredContentReady +
                " overlays=" + chunk.overlayReady +
                " appearance=" + chunk.appearanceReady +
                " ecology=" + chunk.requiredEcologyReady +
                " traversable=" + IsTraversableChunk(coordinate, chunk) +
                " activation=" + chunk.activationComplete +
                " state=" + chunk.state + publicationWorkDiagnostics;
            return false;
        }
        _highSpeedTurnBufferCoverageReadyCount = _highSpeedTurnBufferCoverageDemand.Count;
        failure = string.Empty;
        return true;
    }

    private string DescribeActiveContentWorkers()
    {
        // note: Keep the bounded worker identities in a readiness failure so a protected preparation owner cannot be mistaken for a missing queue dispatch.
        string description = string.Empty;
        foreach (KeyValuePair<Vector2Int, Coroutine> pair in _activeGenerations)
        {
            if (description.Length > 0)
                description += ",";
            _chunks.TryGetValue(pair.Key, out RuntimeChunk chunk);
            description += pair.Key +
                ":hardView=" + _hardViewDemand.Contains(pair.Key) +
                ":guaranteed=" + _guaranteedViewDemand.Contains(pair.Key) +
                ":required=" + (chunk != null && chunk.requiredContentReady) +
                ":near=" + IsRequiredCoverageCoordinate(pair.Key);
        }
        return description.Length == 0 ? "<none>" : description;
    }

    private string DescribePendingTerrainPublications()
    {
        // note: The collider lane is deliberately capped; list its small live owner set so a stalled synchronization ticket is distinguishable from a queued sampler request.
        string description = string.Empty;
        foreach (KeyValuePair<Vector2Int, PendingTerrainPublication> pair in _pendingTerrainPublications)
        {
            PendingTerrainPublication publication = pair.Value;
            if (publication == null)
                continue;
            if (description.Length > 0)
                description += ",";
            description += pair.Key + ":" + (publication.phase ?? "unknown") +
                ":age=" + Mathf.Max(0f, Time.realtimeSinceStartup - publication.startedAt).ToString("0.000") +
                ":workId=" + publication.token.workId;
        }
        return description.Length == 0 ? "<none>" : description;
    }

    public bool TryValidateCurrentTerrain(out string failure)
    {
        if (IsChunkInsideAuthoredTerrain(_currentChunk) || HasPublishedTerrain(_currentChunk))
        {
            failure = string.Empty;
            return true;
        }
        failure = "current chunk has no published terrain " + _currentChunk;
        return false;
    }

    public bool TryValidateCurrentTraversability(
        Vector3 position,
        float capsuleRadius,
        float skinWidth,
        out string failure)
    {
        // note: Validate the actual player capsule against the same publication predicate used by the movement gate.
        float footprintRadius = Mathf.Max(0.01f, capsuleRadius + Mathf.Max(0f, skinWidth));
        if (_currentChunk.x == int.MinValue)
        {
            failure = "streaming frontier has no current chunk";
            return false;
        }
        if (IsTraversableCapsulePosition(position, footprintRadius, out Vector2Int blockingCell))
        {
            failure = string.Empty;
            return true;
        }
        if (!_chunks.TryGetValue(blockingCell, out RuntimeChunk chunk) || chunk == null)
        {
            failure = "capsule intersects missing chunk " + blockingCell;
            return false;
        }
        // note: Include each publication stage in the failure so startup can request the missing stage instead of merely holding the player.
        failure = "capsule intersects unready chunk " + blockingCell +
            " terrain=" + chunk.terrainReadiness +
            " required=" + chunk.requiredContentReady +
            " overlays=" + chunk.overlayReady +
            " appearance=" + chunk.appearanceReady +
            " ecology=" + chunk.requiredEcologyReady +
            " activation=" + chunk.activationComplete +
            " physical=" + chunk.physicalRepresentation;
        return false;
    }

    public bool TryConstrainMovement(
        Vector3 start,
        Vector3 requestedDelta,
        float capsuleRadius,
        float skinWidth,
        out Vector3 permittedDelta)
    {
        using (MotorTraversalConstraintMarker.Auto())
            return TryConstrainMovementCore(
                start, requestedDelta, capsuleRadius, skinWidth, out permittedDelta);
    }

    private bool TryConstrainMovementCore(
        Vector3 start,
        Vector3 requestedDelta,
        float capsuleRadius,
        float skinWidth,
        out Vector3 permittedDelta)
    {
        permittedDelta = requestedDelta;
        float footprintRadius = Mathf.Max(0.01f, capsuleRadius + Mathf.Max(0f, skinWidth));
        float movementClearanceRadius = footprintRadius + TraversalSweepSafetyMarginMeters;
        float sweepRadius = movementClearanceRadius;
        Vector2Int initialBlockingCell = new Vector2Int(int.MinValue, int.MinValue);
        bool currentGroundReady = IsTraversableCapsulePosition(start, footprintRadius, out initialBlockingCell);
        for (int attempt = 0; !currentGroundReady && attempt < 4 && initialBlockingCell.x != int.MinValue; attempt++)
        {
            // note: Fill only the capsule's immediate footprint synchronously when streamed work misses the movement deadline.
            if (!EnsureGroundRepresentationImmediately(initialBlockingCell, false, GroundFallbackSource.CapsuleStart, out _))
                break;
            currentGroundReady = IsTraversableCapsulePosition(start, footprintRadius, out initialBlockingCell);
        }
        if (_currentChunk.x == int.MinValue || !currentGroundReady)
        {
            // note: A missing current collider is a hard stop; allowing gravity or horizontal motion here would let the player interact with an unready cell.
            if (initialBlockingCell.x != int.MinValue)
            {
                _traversalConstraintBlockCount++;
                _lastTraversalConstraintCell = initialBlockingCell;
                LogTraversalConstraintState(initialBlockingCell, "start");
                PrioritizeTraversalCell(initialBlockingCell);
            }
            permittedDelta = Vector3.zero;
            return false;
        }

        Vector3 planarDelta = requestedDelta;
        planarDelta.y = 0f;
        float planarDistance = planarDelta.magnitude;
        if (planarDistance <= 0.001f)
        {
            // note: A stationary capsule checks synchronized ground, without waiting for visual dressing or optional ecology.
            if (IsTraversableCapsulePosition(start, movementClearanceRadius, out Vector2Int stationaryBlockingCell))
                return true;
            if (EnsureGroundRepresentationImmediately(stationaryBlockingCell, false, GroundFallbackSource.StationaryProbe, out _) &&
                IsTraversableCapsulePosition(start, movementClearanceRadius, out _))
                return true;
            _traversalConstraintBlockCount++;
            _lastTraversalConstraintCell = stationaryBlockingCell;
            LogTraversalConstraintState(stationaryBlockingCell, "stationary-ground");
            PrioritizeTraversalCell(stationaryBlockingCell);
            permittedDelta = Vector3.zero;
            return false;
        }

        // note: Intersect the continuous swept footprint with grid cells; point samples can miss a diagonal corner between frames.
        float size = Mathf.Max(32f, chunkWorldSize);
        int firstX = Mathf.FloorToInt((Mathf.Min(start.x, start.x + planarDelta.x) - sweepRadius - WorldGridOrigin) / size);
        int lastX = Mathf.FloorToInt((Mathf.Max(start.x, start.x + planarDelta.x) + sweepRadius - WorldGridOrigin) / size);
        float permittedT = 1f;
        Vector2Int firstBlocked = new Vector2Int(int.MinValue, int.MinValue);
        int examined = 0;
        for (int x = firstX; x <= lastX; x++)
        {
            float enterX = 0f;
            float exitX = 1f;
            if (!ClipSweepAxis(start.x, planarDelta.x, WorldGridOrigin + x * size - sweepRadius,
                    WorldGridOrigin + (x + 1) * size + sweepRadius, ref enterX, ref exitX))
                continue;
            float z0 = start.z + planarDelta.z * enterX;
            float z1 = start.z + planarDelta.z * exitX;
            int firstZ = Mathf.FloorToInt((Mathf.Min(z0, z1) - sweepRadius - WorldGridOrigin) / size);
            int lastZ = Mathf.FloorToInt((Mathf.Max(z0, z1) + sweepRadius - WorldGridOrigin) / size);
            for (int z = firstZ; z <= lastZ; z++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                // note: Trace exact capsule sweep membership using the same pure clipping test; this does not change permitted movement.
                if (_completeCellHistory != null || (_fixedTerrainObservations != null && coordinate == FixedTerrainTraceTarget))
                {
                    float traceEnter = enterX, traceExit = exitX;
                    if (ClipSweepAxis(start.z, planarDelta.z, WorldGridOrigin + z * size - sweepRadius,
                        WorldGridOrigin + (z + 1) * size + sweepRadius, ref traceEnter, ref traceExit))
                    {
                        if (_completeCellHistory != null)
                        {
                            _completeCellSweepFrames[coordinate] = Time.frameCount;
                            ObserveCompleteCell(coordinate, "sweptMembership");
                        }
                        if (coordinate == FixedTerrainTraceTarget)
                        {
                            _fixedTerrainSweptFrame = Time.frameCount;
                            RecordFixedTerrainObservation("sweptMembership", coordinate);
                        }
                    }
                }
                // note: Reject pathological teleport-sized input without skipping cells or spending unbounded time on the main thread.
                if (++examined > 4096)
                {
                    _traversalConstraintBlockCount++;
                    _lastTraversalConstraintCell = coordinate;
                    LogTraversalConstraintState(coordinate, "sweep-budget");
                    permittedDelta = Vector3.zero;
                    return false;
                }
                if (!_chunks.TryGetValue(coordinate, out RuntimeChunk owner) || owner == null ||
                    !IsTraversableChunk(coordinate, owner))
                {
                    // note: A speed spike can cross a tile before queued work starts; publish same-authority ground for the actual swept cells before submitting the motor step.
                    if (EnsureGroundRepresentationImmediately(coordinate, false, GroundFallbackSource.SweptFootprint, out _) &&
                        _chunks.TryGetValue(coordinate, out owner) && owner != null &&
                        IsTraversableChunk(coordinate, owner))
                        continue;
                }
                else
                    continue;
                float enter = enterX;
                float exit = exitX;
                if (ClipSweepAxis(start.z, planarDelta.z, WorldGridOrigin + z * size - sweepRadius,
                        WorldGridOrigin + (z + 1) * size + sweepRadius, ref enter, ref exit) && enter <= permittedT)
                {
                    // note: A valid capsule may sit inside only the extra numerical margin; permit movement that increases its distance from that unready cell so it can leave the edge safely.
                    if (enter <= 0.000001f && exit > 0f &&
                        IsMovementEscapingUnreadyCell(start, planarDelta, coordinate, size, footprintRadius))
                        continue;
                    permittedT = enter;
                    firstBlocked = coordinate;
                }
            }
        }

        if (firstBlocked.x == int.MinValue)
            return true;

        // note: Stop just before contact, leaving a distance-based numerical margin independent of requested speed.
        _traversalConstraintBlockCount++;
        _lastTraversalConstraintCell = firstBlocked;
        LogTraversalConstraintState(firstBlocked, "sweep");
        PrioritizeTraversalCell(firstBlocked);
        permittedT = Mathf.Max(0f, permittedT - 0.002f / planarDistance);
        permittedDelta = new Vector3(
            requestedDelta.x * permittedT,
            requestedDelta.y,
            requestedDelta.z * permittedT);
        return false;
    }

    private bool EnsureGroundRepresentationImmediately(
        Vector2Int coordinate,
        bool exposeRenderer,
        GroundFallbackSource fallbackSource,
        out string failure)
    {
        using (ImmediateGroundFallbackMarker.Auto())
            return EnsureGroundRepresentationImmediatelyCore(
                coordinate, exposeRenderer, fallbackSource, out failure);
    }

    private bool EnsureGroundRepresentationImmediatelyCore(
        Vector2Int coordinate,
        bool exposeRenderer,
        GroundFallbackSource fallbackSource,
        out string failure)
    {
        // note: Resolve a missed movement or render deadline from the existing continuous authority, while keeping semantic readiness on its own publication path.
        failure = string.Empty;
        if (IsChunkInsideAuthoredTerrain(coordinate))
        {
            if (!IsTerrainColliderReady(_terrain))
            {
                failure = "authored terrain collider is not ready " + coordinate;
                return false;
            }
            // note: Capsule and sweep checks require the cell owner even when the shared authored collider already covers it; returning without admission would repeat a false missing-ground stop.
            GetOrCreateChunk(coordinate);
            if (exposeRenderer && _terrain != null)
            {
                if (!_terrain.gameObject.activeSelf)
                    _terrain.gameObject.SetActive(true);
                _terrain.enabled = true;
            }
            return true;
        }

        if (_extendedTerrainTiles.TryGetValue(coordinate, out Terrain canonical) &&
            IsTerrainColliderReady(canonical))
        {
            if (exposeRenderer)
            {
                if (!canonical.gameObject.activeSelf)
                    canonical.gameObject.SetActive(true);
                canonical.enabled = true;
            }
            return true;
        }

        if (_provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisional))
        {
            if (provisional != null && provisional.terrainData != null)
            {
                TerrainCollider provisionalCollider = provisional.GetComponent<TerrainCollider>();
                bool colliderChanged = provisionalCollider != null && !provisionalCollider.enabled;
                if (!provisional.gameObject.activeSelf)
                    provisional.gameObject.SetActive(true);
                if (provisionalCollider != null)
                    provisionalCollider.enabled = true;
                if (colliderChanged)
                {
                    if (exposeRenderer)
                        _groundSafetyPhysicsSyncPending = true;
                    else
                        Physics.SyncTransforms();
                }
                if (exposeRenderer)
                    provisional.enabled = true;
                if (IsTerrainColliderReady(provisional))
                    return true;
            }
            RecycleProvisionalGroundTile(coordinate, GetRuntimeChunk(coordinate), provisional);
        }

        // note: Count failed as well as successful emergency entries so qualification detects synchronous work before any early return.
        _synchronousGroundFallbackAttemptCount++;
        if (fallbackSource == GroundFallbackSource.CameraView)
            _cameraGroundFallbackAttemptCount++;
        else
            _movementGroundFallbackAttemptCount++;
        if (_continuousAuthority == null || _terrain == null || _terrain.terrainData == null)
        {
            failure = "continuous terrain authority or source TerrainData is unavailable " + coordinate;
            return false;
        }

        RuntimeChunk owner = GetOrCreateChunk(coordinate);
        if (owner == null)
        {
            failure = "semantic terrain owner could not be admitted " + coordinate;
            return false;
        }

        // note: Preserve normal canonical generation and accepted semantic content after the same-authority safety collider covers this deadline.
        _contentDemand.Add(coordinate);
        RequestTerrainForChunk(coordinate);
        if (!owner.physicalRepresentation && owner.state != YQSemanticChunkLifecycle.Generating)
            Enqueue(coordinate);
        TryReservePhysicalOwner(coordinate);

        float[,] heights = null;
        bool samplerEntryFound = false;
        if (_provisionalGroundSamplers.TryGetValue(coordinate, out ProvisionalGroundSampling sampling) &&
            sampling != null && ReferenceEquals(sampling.owner, owner))
        {
            samplerEntryFound = true;
            if (sampling.task != null && sampling.task.IsCompleted)
            {
                if (!sampling.task.IsCanceled && !sampling.task.IsFaulted)
                    heights = sampling.task.Result;
                else if (sampling.task.IsFaulted && sampling.task.Exception != null)
                    Debug.LogWarning("[YQSemanticChunkStreamer] Provisional ground sample failed for " + coordinate + ": " +
                        sampling.task.Exception.GetBaseException().Message);
                _provisionalGroundSamplers.Remove(coordinate);
                sampling.preparedBiomeAlphamap?.Dispose();
                sampling.cancellation?.Dispose();
            }
            // note: Keep an in-flight immutable worker alive while the emergency surface is sampled; cancelling it here forced repeated long main-thread resamples.
        }
        if (heights != null)
            _completedSamplerGroundFallbackCount++;
        else if (samplerEntryFound)
            _inFlightSamplerGroundFallbackCount++;
        else
            _missingSamplerGroundFallbackCount++;
        RecordSynchronousGroundFallbackDiagnostic(
            coordinate,
            fallbackSource,
            samplerEntryFound,
            heights != null);

        float startedAt = Time.realtimeSinceStartup;
        try
        {
            int spatialSampleEvaluations = 0;
            int spatialSampleCacheHits = 0;
            if (heights == null)
            {
                float size = Mathf.Max(32f, chunkWorldSize);
                float minimumX = WorldGridOrigin + coordinate.x * size;
                float minimumZ = WorldGridOrigin + coordinate.y * size;
                if (!_continuousAuthority.CanSampleRectangleOffMainThread(
                    minimumX, minimumX + size, minimumZ, minimumZ + size))
                {
                    failure = "continuous authority cannot safely sample the required rectangle " + coordinate;
                    return false;
                }
                float samplingStartedAt = Time.realtimeSinceStartup;
                int fallbackResolution = EmergencyGroundHeightmapResolution;
                // note: A missed background deadline gets a coarse same-authority safety grid immediately; canonical full-resolution terrain replaces it after the collider pipeline completes.
                heights = SampleAuthorityHeightmap(
                    _continuousAuthority,
                    fallbackResolution,
                    minimumX,
                    minimumZ,
                    size,
                    CancellationToken.None,
                    out spatialSampleEvaluations,
                    out spatialSampleCacheHits);
                if (heights.GetLength(0) < MinimumProvisionalGroundHeightmapResolution)
                    heights = UpsampleHeightmapBilinear(heights, MinimumProvisionalGroundHeightmapResolution);
                _maximumSynchronousGroundSamplingSeconds = Mathf.Max(
                    _maximumSynchronousGroundSamplingSeconds,
                    Time.realtimeSinceStartup - samplingStartedAt);
                _lastSynchronousSpatialSampleEvaluations = spatialSampleEvaluations;
                _lastSynchronousSpatialSampleCacheHits = spatialSampleCacheHits;
                _lastSynchronousGroundFallbackResolution = fallbackResolution;
                RecordAggregateWorkSlice(samplingStartedAt, "synchronousGroundHeightSampling");
            }

            float publicationStartedAt = Time.realtimeSinceStartup;
            bool tileCreated = CreateProvisionalGroundTile(coordinate, owner, heights, !exposeRenderer);
            _maximumSynchronousGroundPublicationSeconds = Mathf.Max(
                _maximumSynchronousGroundPublicationSeconds,
                Time.realtimeSinceStartup - publicationStartedAt);
            if (!tileCreated ||
                !_provisionalGroundTiles.TryGetValue(coordinate, out Terrain published) ||
                !IsTerrainColliderReady(published))
            {
                failure = "same-authority safety Terrain failed to publish " + coordinate;
                return false;
            }
            if (exposeRenderer)
            {
                if (!published.gameObject.activeSelf)
                    published.gameObject.SetActive(true);
                published.enabled = true;
                _groundSafetyPhysicsSyncPending = true;
            }

            _synchronousGroundFallbackCount++;
            _lastSynchronousGroundFallbackCell = coordinate.ToString();
            return true;
        }
        catch (Exception exception)
        {
            failure = "same-authority safety ground failed for " + coordinate + ": " + exception.Message;
            return false;
        }
        finally
        {
            float elapsed = Time.realtimeSinceStartup - startedAt;
            _maximumSynchronousGroundFallbackSeconds = Mathf.Max(
                _maximumSynchronousGroundFallbackSeconds,
                elapsed);
        }
    }

    private static bool IsMovementEscapingUnreadyCell(
        Vector3 start,
        Vector3 planarDelta,
        Vector2Int coordinate,
        float cellSize,
        float footprintRadius)
    {
        float minimumX = WorldGridOrigin + coordinate.x * cellSize - footprintRadius;
        float maximumX = WorldGridOrigin + (coordinate.x + 1) * cellSize + footprintRadius;
        float minimumZ = WorldGridOrigin + coordinate.y * cellSize - footprintRadius;
        float maximumZ = WorldGridOrigin + (coordinate.y + 1) * cellSize + footprintRadius;
        Vector2 nearestPoint = new Vector2(
            Mathf.Clamp(start.x, minimumX, maximumX),
            Mathf.Clamp(start.z, minimumZ, maximumZ));
        Vector2 awayFromCell = new Vector2(start.x, start.z) - nearestPoint;
        Vector2 requestedPlanarDelta = new Vector2(planarDelta.x, planarDelta.z);
        return awayFromCell.sqrMagnitude > 0.00000001f &&
            Vector2.Dot(requestedPlanarDelta, awayFromCell) > 0.000001f;
    }

    private void LogTraversalConstraintState(Vector2Int coordinate, string phase)
    {
        // note: Snapshot every readiness component at the first block for a coordinate so a safe stop can be repaired at its real publication boundary.
        if (_lastTraversalDiagnosticCell == coordinate)
            return;
        _lastTraversalDiagnosticCell = coordinate;
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
        {
            Debug.LogWarning("[YQSemanticChunkStreamer] TRAVERSAL BLOCK " + coordinate +
                " phase=" + phase + " owner=missing hardView=" + _hardViewDemand.Contains(coordinate));
            return;
        }
        Debug.LogWarning("[YQSemanticChunkStreamer] TRAVERSAL BLOCK " + coordinate +
            " phase=" + phase +
            " state=" + chunk.state +
            " terrain=" + chunk.terrainReadiness +
            " physical=" + chunk.physicalRepresentation +
            " required=" + chunk.requiredContentReady +
            " overlays=" + chunk.overlayReady +
            " appearance=" + chunk.appearanceReady +
            " visual=" + chunk.visualReady +
            " activation=" + chunk.activationComplete +
            " activeState=" + chunk.activeState +
            " owned=" + chunk.ownedObjects.Count +
            " generated=" + chunk.generatedObjects.Count +
            " hardView=" + _hardViewDemand.Contains(coordinate) +
            " fullyLoaded=" + IsFullyLoadedForView(coordinate, chunk) +
            " viewBlocker=" + DescribeViewLoadBlocker(coordinate, chunk) +
            " failure=" + (string.IsNullOrWhiteSpace(chunk.failureReason) ? "none" : chunk.failureReason));
    }

    // note: Report the first missing readiness or renderer stage when a hard-view publication gate blocks movement.
    private string DescribeViewLoadBlocker(Vector2Int coordinate, RuntimeChunk chunk)
    {
        if (chunk == null)
            return "owner-missing";
        if (_terrainPainting.ContainsKey(coordinate))
            return "terrain-painting";
        if (!chunk.physicalRepresentation && !IsChunkInsideAuthoredTerrain(coordinate))
            return "physical-representation";
        if (!chunk.requiredContentReady)
            return "required-content";
        if (!chunk.overlayReady)
            return "overlays";
        if (!chunk.appearanceReady)
            return "appearance";
        if (!chunk.visualReady)
            return "visual";
        if (!chunk.requiredEcologyReady)
            return "ecology";
        if (!IsChunkInsideAuthoredTerrain(coordinate) && chunk.terrainReadiness < YQTerrainReadinessState.CollisionReady)
            return "terrain-" + chunk.terrainReadiness;
        if (!chunk.activationComplete || chunk.activeState != 1)
            return "activation-complete=" + chunk.activationComplete + ":active-state=" + chunk.activeState;

        // note: Explain renderer-level publication stalls only when a real traversal gate is hit, avoiding per-frame hierarchy scans.
        for (int objectIndex = 0; objectIndex < chunk.generatedObjects.Count; objectIndex++)
        {
            GameObject generatedObject = chunk.generatedObjects[objectIndex];
            if (generatedObject == null)
                continue;
            if (!generatedObject.activeInHierarchy)
                return "generated-object-inactive=" + generatedObject.name;
            Renderer[] renderers = generatedObject.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (generatedObject == null)
                    continue;
                if (!renderer.gameObject.activeInHierarchy)
                {
                    YQStreamedFeatureOverlayTarget tombstoneTarget =
                        renderer.GetComponentInParent<YQStreamedFeatureOverlayTarget>(true);
                    if (tombstoneTarget != null && !tombstoneTarget.gameObject.activeInHierarchy)
                        continue;
                    return "renderer-object-inactive=" + renderer.name;
                }
                if (!renderer.enabled)
                    return "renderer-disabled=" + renderer.name + ":" + renderer.GetType().Name;
            }
            Terrain terrain = generatedObject.GetComponent<Terrain>();
            if (terrain != null && (!terrain.enabled || !generatedObject.activeInHierarchy))
                return "terrain-component-disabled=" + generatedObject.name;
        }
        return "none";
    }

    internal static bool ClipSweepAxis(float start, float delta, float minimum, float maximum, ref float enter, ref float exit)
    {
        // note: Slab intersection supplies exact entry/exit fractions for an expanded cell and also handles stationary axes.
        if (delta == 0f)
            return start >= minimum && start <= maximum;
        float first = (minimum - start) / delta;
        float last = (maximum - start) / delta;
        enter = Mathf.Max(enter, Mathf.Min(first, last));
        exit = Mathf.Min(exit, Mathf.Max(first, last));
        return enter <= exit;
    }

    private bool IsTraversableCapsulePosition(
        Vector3 position,
        float footprintRadius,
        out Vector2Int blockingCell)
    {
        float size = Mathf.Max(32f, chunkWorldSize);
        int minimumX = Mathf.FloorToInt((position.x - footprintRadius - WorldGridOrigin) / size);
        int maximumX = Mathf.FloorToInt((position.x + footprintRadius - WorldGridOrigin) / size);
        int minimumZ = Mathf.FloorToInt((position.z - footprintRadius - WorldGridOrigin) / size);
        int maximumZ = Mathf.FloorToInt((position.z + footprintRadius - WorldGridOrigin) / size);
        for (int z = minimumZ; z <= maximumZ; z++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) &&
                    chunk != null && IsTraversableChunk(coordinate, chunk))
                    continue;
                // note: The capsule footprint is conservatively evaluated by its intersecting cell bounds, so no skin-width overlap can enter an unready owner.
                blockingCell = coordinate;
                return false;
            }
        }
        blockingCell = new Vector2Int(int.MinValue, int.MinValue);
        return true;
    }

    private void PrioritizeTraversalCell(Vector2Int coordinate)
    {
        AdmitPhysicalTraversalDemand(coordinate);
        SortContentQueue();
        SortTerrainQueue();
    }

    private void AdmitPhysicalTraversalDemand(Vector2Int coordinate)
    {
        RuntimeChunk chunk = GetOrCreateChunk(coordinate);
        if (chunk == null)
            return;
        _contentDemand.Add(coordinate);
        RequestTerrainForChunk(coordinate);
        QueueProvisionalGround(coordinate);
        if (!chunk.physicalRepresentation &&
            chunk.state != YQSemanticChunkLifecycle.Generating)
            Enqueue(coordinate);
    }

    private void QueueProvisionalGround(Vector2Int coordinate)
    {
        // note: Keep the safety floor ahead of the player's current speed so an abrupt boost cannot expose an unprepared camera cell.
        if (IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate) ||
            _provisionalGroundTiles.ContainsKey(coordinate) || _provisionalGroundSamplers.ContainsKey(coordinate) ||
            !_provisionalGroundQueued.Add(coordinate))
            return;
        if (_provisionalGroundQueue.Count >= PhysicalOwnerCapacity && !_batchingHardViewAdmission)
        {
            if (!TryMakeRoomForProvisionalGround(coordinate))
            {
                _provisionalGroundQueued.Remove(coordinate);
                return;
            }
        }
        // note: Keep predictive admission lightweight; create a semantic owner only when a bounded sampler slot is ready.
        _provisionalGroundQueue.Add(coordinate);
    }

    private bool HasPendingVisibleGroundWork()
    {
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate) ||
                _provisionalGroundTiles.ContainsKey(coordinate))
                continue;
            if (_provisionalGroundQueued.Contains(coordinate))
                return true;
        }
        foreach (KeyValuePair<Vector2Int, ProvisionalGroundSampling> pair in _provisionalGroundSamplers)
        {
            ProvisionalGroundSampling sampling = pair.Value;
            if (_guaranteedViewDemand.Contains(pair.Key) &&
                (sampling == null || sampling.task == null || sampling.task.IsCompleted))
                return true;
        }
        return false;
    }

    private bool HasPendingHighSpeedTurnBufferGroundWork()
    {
        // note: Reserve a bounded same-authority floor publication while high-speed input is active so a newly exposed turn lane does not wait behind speculative terrain work.
        if (ResolveTraversalDemandVelocity().sqrMagnitude < 900f)
            return false;
        foreach (Vector2Int coordinate in _provisionalGroundTurnBufferDemand)
        {
            if (IsChunkInsideAuthoredTerrain(coordinate))
                continue;
            if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) &&
                chunk != null && IsTraversableChunk(coordinate, chunk))
                continue;
            return true;
        }
        return false;
    }

    private bool TryMakeRoomForProvisionalGround(Vector2Int requested)
    {
        Vector2 position = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        Vector2 velocity = ResolveTraversalDemandVelocity();
        int leastUrgentIndex = -1;
        for (int index = 0; index < _provisionalGroundQueue.Count; index++)
        {
            Vector2Int queued = _provisionalGroundQueue[index];
            if (leastUrgentIndex < 0 || CompareProvisionalGroundPriority(
                    queued,
                    _provisionalGroundQueue[leastUrgentIndex],
                    position,
                    velocity) > 0)
                leastUrgentIndex = index;
        }
        if (leastUrgentIndex < 0 || CompareProvisionalGroundPriority(
                requested,
                _provisionalGroundQueue[leastUrgentIndex],
                position,
                velocity) >= 0)
            return false;

        // note: A newly visible ground cell replaces the farthest speculative lead instead of losing its collider request at capacity.
        _provisionalGroundQueued.Remove(_provisionalGroundQueue[leastUrgentIndex]);
        _provisionalGroundQueue.RemoveAt(leastUrgentIndex);
        return true;
    }

    private void TrimProvisionalGroundQueueToCapacity()
    {
        if (_provisionalGroundQueue.Count <= PhysicalOwnerCapacity)
            return;

        Vector2 position = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        Vector2 velocity = ResolveTraversalDemandVelocity();
        // note: Select the same highest-priority bounded sampler set once after bulk view admission instead of rescanning the queue for every overflow candidate.
        _provisionalGroundQueue.Sort((left, right) =>
            CompareProvisionalGroundPriority(left, right, position, velocity));
        for (int index = PhysicalOwnerCapacity; index < _provisionalGroundQueue.Count; index++)
            _provisionalGroundQueued.Remove(_provisionalGroundQueue[index]);
        _provisionalGroundQueue.RemoveRange(
            PhysicalOwnerCapacity,
            _provisionalGroundQueue.Count - PhysicalOwnerCapacity);
    }

    private void ProcessProvisionalGroundQueue()
    {
        // note: Keep the existing queue behavior intact while exposing one matching profiler sample for the bounded pass.
        using (ProvisionalGroundQueueMarker.Auto())
            ProcessProvisionalGroundQueueCore();
    }

    private void ProcessProvisionalGroundQueueCore()
    {
        // note: Keep background floor sampling bounded independently from main-thread renderer/collider publication.
        BeginAggregateBudgetFrame();
        int publicationsRemaining = MaximumProvisionalGroundPublicationsPerFrame;
        int urgentVisiblePublicationsRemaining = MaximumUrgentVisibleProvisionalGroundPublicationsPerFrame;
        float aggregateWorkBeforePublication = _aggregateFrameWorkSeconds;
        float publicationPassStartedAt = Time.realtimeSinceStartup;

        // note: Remove superseded requests before admission so rapid changes in speed or direction cannot pin old lead cells in the bounded queue.
        for (int index = _provisionalGroundQueue.Count - 1; index >= 0; index--)
        {
            Vector2Int coordinate = _provisionalGroundQueue[index];
            bool stillDemanded = _guaranteedViewDemand.Contains(coordinate) ||
                _provisionalGroundPrefetchDemand.Contains(coordinate) ||
                _provisionalGroundTurnBufferDemand.Contains(coordinate) || IsContentDemandedNow(coordinate);
            if (stillDemanded && !IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate) &&
                !_provisionalGroundTiles.ContainsKey(coordinate) && !_provisionalGroundSamplers.ContainsKey(coordinate))
                continue;
            _provisionalGroundQueue.RemoveAt(index);
            _provisionalGroundQueued.Remove(coordinate);
        }
        PreemptProvisionalGroundSamplingForVisibleDemand();

        // note: Cancel stale samples first so their bounded worker slots cannot delay a newly visible cell.
        _provisionalGroundSamplerScratch.Clear();
        foreach (KeyValuePair<Vector2Int, ProvisionalGroundSampling> pair in _provisionalGroundSamplers)
        {
            ProvisionalGroundSampling sampling = pair.Value;
            bool currentOwner = sampling != null &&
                _chunks.TryGetValue(pair.Key, out RuntimeChunk current) && ReferenceEquals(current, sampling.owner);
            bool stillDemanded = _guaranteedViewDemand.Contains(pair.Key) ||
                _provisionalGroundPrefetchDemand.Contains(pair.Key) ||
                _provisionalGroundTurnBufferDemand.Contains(pair.Key) || IsContentDemandedNow(pair.Key);
            if (sampling == null || sampling.abandoned || !currentOwner || !stillDemanded ||
                IsChunkInsideAuthoredTerrain(pair.Key) || HasPublishedTerrain(pair.Key) ||
                _provisionalGroundTiles.ContainsKey(pair.Key))
            {
                if (sampling != null && !sampling.abandoned)
                {
                    sampling.abandoned = true;
                    sampling.cancellation?.Cancel();
                }
                _provisionalGroundSamplerScratch.Add(pair.Key);
                continue;
            }
            if (sampling.task == null || !sampling.task.IsCompleted)
                continue;

            if (sampling.task.IsCanceled || sampling.task.IsFaulted)
            {
                if (sampling.task.IsFaulted && sampling.task.Exception != null)
                    Debug.LogWarning("[YQSemanticChunkStreamer] Provisional ground sampling failed for " + pair.Key + ": " + sampling.task.Exception.GetBaseException().Message);
                _provisionalGroundSamplerScratch.Add(pair.Key);
            }
        }

        for (int index = 0; index < _provisionalGroundSamplerScratch.Count; index++)
        {
            Vector2Int coordinate = _provisionalGroundSamplerScratch[index];
            if (_provisionalGroundSamplers.TryGetValue(coordinate, out ProvisionalGroundSampling sampling))
            {
                CancelProvisionalGroundSampler(coordinate, sampling);
                if (sampling != null &&
                    _chunks.TryGetValue(coordinate, out RuntimeChunk owner) && ReferenceEquals(owner, sampling.owner) &&
                    (_guaranteedViewDemand.Contains(coordinate) || _hardViewDemand.Contains(coordinate) ||
                     _provisionalGroundPrefetchDemand.Contains(coordinate) ||
                     _provisionalGroundTurnBufferDemand.Contains(coordinate) || _contentDemand.Contains(coordinate)))
                    QueueProvisionalGround(coordinate);
            }
        }
        _provisionalGroundSamplerScratch.Clear();

        // note: Publish completed exact-view samples ahead of predictive/preparation work, including when admission already consumed the shared frame budget.
        Vector2 priorityPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        // note: Movement intent opens the high-speed sampler pool in the same frame a boost is requested, before motor acceleration updates measured velocity.
        Vector2 priorityVelocity = ResolveTraversalDemandVelocity();
        while (publicationsRemaining > 0 || urgentVisiblePublicationsRemaining > 0)
        {
            Vector2Int selectedCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            ProvisionalGroundSampling selectedSampling = null;
            foreach (KeyValuePair<Vector2Int, ProvisionalGroundSampling> pair in _provisionalGroundSamplers)
            {
                ProvisionalGroundSampling sampling = pair.Value;
                if (sampling == null || sampling.abandoned || sampling.task == null || !sampling.task.IsCompleted ||
                    sampling.task.IsCanceled || sampling.task.IsFaulted ||
                    (sampling.preparedBiomeAlphamap != null && !sampling.preparedBiomeAlphamap.IsCompleted))
                    continue;
                if (selectedSampling == null || CompareProvisionalGroundPriority(
                    pair.Key, selectedCoordinate, priorityPosition, priorityVelocity) < 0)
                {
                    selectedCoordinate = pair.Key;
                    selectedSampling = sampling;
                }
            }
            if (selectedSampling == null)
                break;

            bool liveView = _guaranteedViewDemand.Contains(selectedCoordinate);
            bool turnBuffer = _provisionalGroundTurnBufferDemand.Contains(selectedCoordinate);
            bool usesUrgentVisibleReserve = publicationsRemaining <= 0;
            if (usesUrgentVisibleReserve &&
                (!liveView || urgentVisiblePublicationsRemaining <= 0 ||
                 aggregateWorkBeforePublication + (Time.realtimeSinceStartup - publicationPassStartedAt) >=
                    AggregateMainThreadBudgetSeconds))
                break;
            if ((!CanAdvanceAggregateWork("provisionalGroundTile") && !liveView && !turnBuffer) ||
                !TryReservePhysicalOwner(selectedCoordinate))
                break;

            bool published = CreateProvisionalGroundTile(
                selectedCoordinate,
                selectedSampling.owner,
                selectedSampling.task.Result,
                synchronizePhysics: true,
                recordAggregateSlice: false,
                preparedBiomeAlphamap: selectedSampling.preparedBiomeAlphamap);
            _provisionalGroundSamplers.Remove(selectedCoordinate);
            selectedSampling.cancellation?.Dispose();
            selectedSampling.preparedBiomeAlphamap?.Dispose();
            if (!published)
                QueueProvisionalGround(selectedCoordinate);
            else if (publicationsRemaining > 0)
                publicationsRemaining--;
            else
                urgentVisiblePublicationsRemaining--;
        }

        // note: Spend more worker-side height sampling only during fast travel; the extra Terrain publication remains reserved for budget-safe live view.
        int samplerCapacity = priorityVelocity.magnitude >= 30f
            ? MaximumHighSpeedProvisionalGroundSamplers
            : MaximumConcurrentProvisionalGroundSamplers;
        int activeSamplerCount = Mathf.Max(0, Volatile.Read(ref _abandonedProvisionalGroundWorkerCount));
        foreach (ProvisionalGroundSampling sampling in _provisionalGroundSamplers.Values)
            if (sampling != null && !sampling.abandoned &&
                ((sampling.task != null && !sampling.task.IsCompleted) ||
                 (sampling.preparedBiomeAlphamap != null && !sampling.preparedBiomeAlphamap.IsCompleted)))
                activeSamplerCount++;

        while (activeSamplerCount < samplerCapacity &&
            _provisionalGroundSamplers.Count < MaximumRetainedProvisionalGroundHeightmaps &&
            _provisionalGroundQueue.Count > 0)
        {
            int queueIndex = FindNextProvisionalGroundQueueIndex();
            Vector2Int coordinate = _provisionalGroundQueue[queueIndex];
            // note: Sampling touches only immutable authority data off-thread, so the bounded worker pool can prepare the next safety floor while the main thread is busy.
            _provisionalGroundQueue.RemoveAt(queueIndex);
            _provisionalGroundQueued.Remove(coordinate);
            if ((!_guaranteedViewDemand.Contains(coordinate) &&
                 !_provisionalGroundPrefetchDemand.Contains(coordinate) &&
                 !_provisionalGroundTurnBufferDemand.Contains(coordinate) && !IsContentDemandedNow(coordinate)) ||
                IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate) ||
                _provisionalGroundTiles.ContainsKey(coordinate) || _provisionalGroundSamplers.ContainsKey(coordinate))
                continue;

            if (_terrain == null || _terrain.terrainData == null || _continuousAuthority == null)
                continue;

            float size = Mathf.Max(32f, chunkWorldSize);
            float tileMinX = WorldGridOrigin + coordinate.x * size;
            float tileMinZ = WorldGridOrigin + coordinate.y * size;
            if (!_continuousAuthority.CanSampleRectangleOffMainThread(
                tileMinX, tileMinX + size, tileMinZ, tileMinZ + size))
            {
                // note: A rectangle inside the authored transition collar cannot use the immutable worker sampler; preserve its demand through canonical terrain publication instead of dropping it.
                RequestTerrainForChunk(coordinate);
                continue;
            }

            RuntimeChunk chunk = GetOrCreateChunk(coordinate);
            if (chunk == null)
                continue;

            YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparedBiomeAlphamap = null;
            TerrainLayer[] sourceLayers = _terrain.terrainData.terrainLayers;
            if (sourceLayers != null && sourceLayers.Length > 0 &&
                !SharesOriginTerrainBoundary(coordinate, size))
            {
                try
                {
                    // note: Sample identical biome weights off-thread while the heightfield runs; cells sharing an authored edge retain exact border copying.
                    preparedBiomeAlphamap = YQContinuousWorldFeatureMaterializer.BeginBiomeAlphamapPreparation(
                        sourceLayers,
                        ProvisionalGroundAlphamapResolution,
                        _continuousAuthority,
                        coordinate,
                        size);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("[YQSemanticChunkStreamer] Provisional biome preparation could not start for " +
                        coordinate + "; retaining the border-compatible painter: " + exception.Message);
                }
            }

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _provisionalGroundSamplers[coordinate] = new ProvisionalGroundSampling
            {
                owner = chunk,
                cancellation = cancellation,
                preparedBiomeAlphamap = preparedBiomeAlphamap,
                task = StartBackgroundHeightTask(
                    coordinate,
                    ProvisionalGroundHeightmapResolution,
                    tileMinX,
                    tileMinZ,
                    size,
                    cancellation.Token)
            };
            activeSamplerCount++;
        }
    }

    private bool SharesOriginTerrainBoundary(Vector2Int coordinate, float size)
    {
        // note: Preserve authored alpha seams with the existing exact sampler; only fully external tiles use worker-prepared biome maps.
        if (_terrain == null || _terrain.terrainData == null)
            return true;

        Vector3 baseOrigin = _terrain.transform.position;
        Vector3 baseSize = _terrain.terrainData.size;
        float tileMinX = WorldGridOrigin + coordinate.x * size;
        float tileMaxX = WorldGridOrigin + (coordinate.x + 1) * size;
        float tileMinZ = WorldGridOrigin + coordinate.y * size;
        float tileMaxZ = WorldGridOrigin + (coordinate.y + 1) * size;
        float overlapZ = Mathf.Min(tileMaxZ, baseOrigin.z + baseSize.z) - Mathf.Max(tileMinZ, baseOrigin.z);
        float overlapX = Mathf.Min(tileMaxX, baseOrigin.x + baseSize.x) - Mathf.Max(tileMinX, baseOrigin.x);
        return
            (Mathf.Abs(tileMinX - (baseOrigin.x + baseSize.x)) < 0.01f && overlapZ > 0.01f) ||
            (Mathf.Abs(tileMaxX - baseOrigin.x) < 0.01f && overlapZ > 0.01f) ||
            (Mathf.Abs(tileMinZ - (baseOrigin.z + baseSize.z)) < 0.01f && overlapX > 0.01f) ||
            (Mathf.Abs(tileMaxZ - baseOrigin.z) < 0.01f && overlapX > 0.01f);
    }

    private void PreemptProvisionalGroundSamplingForVisibleDemand()
    {
        if (_provisionalGroundQueue.Count == 0)
            return;

        Vector2 priorityPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        Vector2 priorityVelocity = ResolveTraversalDemandVelocity();
        Vector2Int queuedCoordinate = _provisionalGroundQueue[FindNextProvisionalGroundQueueIndex()];
        Vector2Int selectedCoordinate = new Vector2Int(int.MinValue, int.MinValue);
        ProvisionalGroundSampling selectedSampling = null;
        foreach (KeyValuePair<Vector2Int, ProvisionalGroundSampling> pair in _provisionalGroundSamplers)
        {
            ProvisionalGroundSampling sampling = pair.Value;
            if (sampling == null || sampling.abandoned || sampling.task == null || sampling.task.IsCompleted)
                continue;
            if (selectedSampling == null || CompareProvisionalGroundPriority(
                pair.Key, selectedCoordinate, priorityPosition, priorityVelocity) > 0)
            {
                selectedCoordinate = pair.Key;
                selectedSampling = sampling;
            }
        }
        if (selectedSampling == null || CompareProvisionalGroundPriority(
            queuedCoordinate, selectedCoordinate, priorityPosition, priorityVelocity) >= 0)
            return;

        // note: Free one lower-priority worker for the earliest view deadline; the canceled pure sampler cannot publish a stale result.
        CancelProvisionalGroundSampler(selectedCoordinate, selectedSampling);
        QueueProvisionalGround(selectedCoordinate);
    }

    private void CancelProvisionalGroundSampler(Vector2Int coordinate, ProvisionalGroundSampling sampling)
    {
        _provisionalGroundSamplers.Remove(coordinate);
        sampling?.preparedBiomeAlphamap?.Dispose();
        if (sampling == null || sampling.cancellation == null)
            return;
        sampling.abandoned = true;
        sampling.cancellation.Cancel();
        if (sampling.task == null || sampling.task.IsCompleted)
        {
            if (sampling.task != null && sampling.task.IsFaulted)
                _ = sampling.task.Exception;
            sampling.cancellation.Dispose();
            return;
        }

        CancellationTokenSource cancellation = sampling.cancellation;
        Task<float[,]> task = sampling.task;
        Interlocked.Increment(ref _abandonedProvisionalGroundWorkerCount);
        task.ContinueWith(completedTask =>
        {
            // note: Observe a raced worker fault before releasing its cancellation source on the worker thread.
            if (completedTask.IsFaulted)
                _ = completedTask.Exception;
            cancellation.Dispose();
            Interlocked.Decrement(ref _abandonedProvisionalGroundWorkerCount);
        }, TaskScheduler.Default);
    }

    private int FindNextProvisionalGroundQueueIndex()
    {
        int selectedIndex = 0;
        Vector2 playerPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        Vector2 velocity = ResolveTraversalDemandVelocity();
        for (int index = 0; index < _provisionalGroundQueue.Count; index++)
        {
            if (index > 0 && CompareProvisionalGroundPriority(
                _provisionalGroundQueue[index],
                _provisionalGroundQueue[selectedIndex],
                playerPosition,
                velocity) < 0)
                selectedIndex = index;
        }
        return selectedIndex;
    }

    private int CompareProvisionalGroundPriority(
        Vector2Int left,
        Vector2Int right,
        Vector2 playerPosition,
        Vector2 velocity)
    {
        // note: Apply one shared deadline ordering to queue admission and completed tile publication.
        float size = Mathf.Max(32f, chunkWorldSize);
        // note: The current camera envelope is a hard deadline and always outranks speculative safety cells.
        bool leftVisible = _guaranteedViewDemand.Contains(left);
        bool rightVisible = _guaranteedViewDemand.Contains(right);
        if (leftVisible != rightVisible)
            return leftVisible ? -1 : 1;
        if (leftVisible)
        {
            // note: Publish the far edge of the live camera envelope first; the player-near traversal lane has its own higher-priority canonical terrain schedule.
            return DistanceToChunkSquared(playerPosition, right, size).CompareTo(
                DistanceToChunkSquared(playerPosition, left, size));
        }

        // note: Keep one-second all-direction collision safety ahead of the distant four-second camera runway.
        bool leftTurnBuffer = _provisionalGroundTurnBufferDemand.Contains(left);
        bool rightTurnBuffer = _provisionalGroundTurnBufferDemand.Contains(right);
        if (leftTurnBuffer != rightTurnBuffer)
            return leftTurnBuffer ? -1 : 1;
        if (leftTurnBuffer && rightTurnBuffer)
        {
            // note: Spend each bounded publication on the earliest requested-travel deadline; a 6-to-260 m/s boost must fill its forward floor before lateral ring cells.
            float turnBufferSpeed = velocity.magnitude;
            if (turnBufferSpeed > 0.01f)
            {
                Vector2 turnBufferDirection = velocity / turnBufferSpeed;
                float leftArrival = ProvisionalGroundTraversalArrivalSeconds(left, playerPosition, turnBufferDirection, turnBufferSpeed, size);
                float rightArrival = ProvisionalGroundTraversalArrivalSeconds(right, playerPosition, turnBufferDirection, turnBufferSpeed, size);
                int arrivalCompare = leftArrival.CompareTo(rightArrival);
                if (arrivalCompare != 0)
                    return arrivalCompare;
            }

            // note: Preserve the existing radial safety fill order while stationary, when no traversal heading has a deadline.
            return DistanceToChunkSquared(playerPosition, right, size).CompareTo(
                DistanceToChunkSquared(playerPosition, left, size));
        }

        bool leftPreparation = _hardViewDemand.Contains(left) && !_guaranteedViewDemand.Contains(left);
        bool rightPreparation = _hardViewDemand.Contains(right) && !_guaranteedViewDemand.Contains(right);
        if (leftPreparation != rightPreparation)
            return leftPreparation ? -1 : 1;
        if (leftPreparation && rightPreparation)
        {
            bool leftHasArrival = _semanticViewPredictionArrivalSeconds.TryGetValue(left, out float leftPredictedArrival);
            bool rightHasArrival = _semanticViewPredictionArrivalSeconds.TryGetValue(right, out float rightPredictedArrival);
            if (leftHasArrival != rightHasArrival)
                return leftHasArrival ? -1 : 1;
            if (leftHasArrival && rightHasArrival)
            {
                int arrivalCompare = leftPredictedArrival.CompareTo(rightPredictedArrival);
                if (arrivalCompare != 0)
                    return arrivalCompare;
            }
            // note: When predicted arrival ties, prepare the outer edge first so each tile keeps its full crossing interval.
            return DistanceToChunkSquared(playerPosition, right, size).CompareTo(
                DistanceToChunkSquared(playerPosition, left, size));
        }

        // note: Fill the nearby all-direction preparation ring before speculative forward leads so sudden movement changes have sampled ground waiting ahead.
        bool leftGroundLead = _provisionalGroundPrefetchDemand.Contains(left);
        bool rightGroundLead = _provisionalGroundPrefetchDemand.Contains(right);
        if (leftGroundLead != rightGroundLead)
            return leftGroundLead ? -1 : 1;

        // note: Order speculative cells by the measured motor direction; exact live-frustum cells remain higher priority above.
        float speed = velocity.magnitude;
        Vector2 direction = speed > 0.01f ? velocity / speed : Vector2.zero;
        if ((leftGroundLead || rightGroundLead) && direction.sqrMagnitude <= 0.0001f && _queuePriorityCamera != null)
        {
            Vector3 cameraForward = _queuePriorityCamera.transform.forward;
            Vector2 cameraDirection = new Vector2(cameraForward.x, cameraForward.z);
            if (cameraDirection.sqrMagnitude > 0.0001f)
            {
                direction = cameraDirection.normalized;
                speed = MaximumGroundPrefetchSpeedMetersPerSecond;
            }
        }
        if (speed > 0.01f)
        {
            float leftEta = ProvisionalGroundViewArrivalSeconds(left, playerPosition, direction, speed, size);
            float rightEta = ProvisionalGroundViewArrivalSeconds(right, playerPosition, direction, speed, size);
            int etaCompare = leftEta.CompareTo(rightEta);
            if (etaCompare != 0)
                return etaCompare;
        }

        int distanceCompare = DistanceToChunkSquared(playerPosition, left, size).CompareTo(
            DistanceToChunkSquared(playerPosition, right, size));
        if (distanceCompare != 0)
            return distanceCompare;
        return CompareCoordinates(left, right);
    }

    private float ProvisionalGroundViewArrivalSeconds(
        Vector2Int coordinate,
        Vector2 playerPosition,
        Vector2 direction,
        float speed,
        float size)
    {
        Vector2 center = new Vector2(
            WorldGridOrigin + (coordinate.x + 0.5f) * size,
            WorldGridOrigin + (coordinate.y + 0.5f) * size);
        Vector2 offset = center - playerPosition;
        float along = Vector2.Dot(offset, direction);
        float lateral = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
        return Mathf.Max(0f, along - Mathf.Max(0f, guaranteedVisualDistanceMeters)) / speed +
            lateral / speed * 0.25f;
    }

    private float ProvisionalGroundTraversalArrivalSeconds(
        Vector2Int coordinate,
        Vector2 playerPosition,
        Vector2 direction,
        float speed,
        float size)
    {
        // note: Rank same-authority turn-buffer tiles along requested travel so sudden boosts publish the approaching floor before off-axis cells.
        Vector2 center = new Vector2(
            WorldGridOrigin + (coordinate.x + 0.5f) * size,
            WorldGridOrigin + (coordinate.y + 0.5f) * size);
        Vector2 offset = center - playerPosition;
        float along = Vector2.Dot(offset, direction);
        float lateral = Mathf.Abs(offset.x * direction.y - offset.y * direction.x);
        float behindPenalty = along < 0f ? size : 0f;
        return (Mathf.Max(0f, along) + behindPenalty + lateral * 0.75f) / Mathf.Max(1f, speed);
    }

    private static float DistanceToChunkSquared(Vector2 position, Vector2Int coordinate, float size)
    {
        // note: Rank by distance to the cell bounds rather than its center so edge-adjacent view cells remain urgent.
        float minimumX = WorldGridOrigin + coordinate.x * size;
        float minimumZ = WorldGridOrigin + coordinate.y * size;
        float nearestX = Mathf.Clamp(position.x, minimumX, minimumX + size);
        float nearestZ = Mathf.Clamp(position.y, minimumZ, minimumZ + size);
        return (position - new Vector2(nearestX, nearestZ)).sqrMagnitude;
    }

    private bool CreateProvisionalGroundTile(
        Vector2Int coordinate,
        RuntimeChunk owner,
        float[,] heights,
        bool synchronizePhysics = true,
        bool recordAggregateSlice = true,
        YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparedBiomeAlphamap = null)
    {
        // note: Materialize only a finished continuous-authority sample; normal terrain publication replaces this bounded fast-travel floor.
        if (_terrain == null || _terrain.terrainData == null || _continuousAuthority == null || owner == null ||
            heights == null || heights.GetLength(0) < MinimumProvisionalGroundHeightmapResolution ||
            heights.GetLength(1) != heights.GetLength(0) ||
            !ReferenceEquals(owner, GetRuntimeChunk(coordinate)))
            return false;

        float startedAt = Time.realtimeSinceStartup;
        TerrainData data = null;
        GameObject tileObject = null;
        Terrain tile = null;
        TerrainCollider collider = null;
        bool reused = false;
        try
        {
            float size = Mathf.Max(32f, chunkWorldSize);
            int resolution = heights.GetLength(0);
            Vector3 baseOrigin = _terrain.transform.position;
            float tileMinX = WorldGridOrigin + coordinate.x * size;
            float tileMinZ = WorldGridOrigin + coordinate.y * size;

            while (_provisionalGroundPool.Count > 0 && !reused)
            {
                Terrain candidate = _provisionalGroundPool.Pop();
                if (candidate == null)
                    continue;
                TerrainData candidateData = candidate.terrainData;
                if (candidateData == null || candidateData.heightmapResolution != resolution ||
                    Mathf.Abs(candidateData.size.x - size) > 0.01f ||
                    Mathf.Abs(candidateData.size.z - size) > 0.01f)
                {
                    UnityEngine.Object.Destroy(candidate.gameObject);
                    if (candidateData != null)
                        UnityEngine.Object.Destroy(candidateData);
                    continue;
                }
                TerrainCollider candidateCollider = candidate.GetComponent<TerrainCollider>();
                if (candidateCollider == null)
                {
                    UnityEngine.Object.Destroy(candidate.gameObject);
                    UnityEngine.Object.Destroy(candidateData);
                    continue;
                }

                tile = candidate;
                collider = candidateCollider;
                data = candidateData;
                tileObject = candidate.gameObject;
                tile.SetNeighbors(null, null, null, null);
                tile.enabled = false;
                collider.enabled = false;
                tileObject.SetActive(false);
                reused = true;
            }

            if (!reused)
            {
                data = new TerrainData
                {
                    heightmapResolution = resolution,
                    size = new Vector3(size, YQGeneratedWorldTerrain.TerrainHeight, size)
                };
                data.alphamapResolution = ProvisionalGroundAlphamapResolution;
                TerrainLayer[] sourceLayers = _terrain.terrainData.terrainLayers;
                if (sourceLayers != null)
                {
                    data.terrainLayers = sourceLayers;
                }
            }

            data.SetHeights(0, 0, heights);
            if (data.terrainLayers != null && data.terrainLayers.Length > 0)
            {
                // note: Paint both new and pooled safety tiles from the canonical biome authority so a fast frontier never shows a stale or uniform layer-0 surface.
                try
                {
                    if (preparedBiomeAlphamap != null)
                    {
                        float[,,] maps = preparedBiomeAlphamap.TakeMaps();
                        if (maps.GetLength(0) != data.alphamapResolution ||
                            maps.GetLength(1) != data.alphamapResolution ||
                            maps.GetLength(2) != data.terrainLayers.Length)
                            throw new InvalidOperationException("Prepared provisional biome map does not match its TerrainData");
                        // note: This small upload preserves the exact 32px biome result without replaying its scalar sampler on the gameplay thread.
                        data.SetAlphamaps(0, 0, maps);
                    }
                    else
                    {
                        YQContinuousWorldFeatureMaterializer.PaintBiomeAlphamaps(
                            data,
                            _continuousAuthority,
                            coordinate,
                            size);
                    }
                }
                catch (Exception paintFailure)
                {
                    // note: Optional fallback color painting cannot invalidate a valid same-authority ground collider; reset pooled alpha data before publishing the surface.
                    float[,,] baseSurface = new float[data.alphamapResolution, data.alphamapResolution, data.terrainLayers.Length];
                    for (int z = 0; z < data.alphamapResolution; z++)
                        for (int x = 0; x < data.alphamapResolution; x++)
                            baseSurface[z, x, 0] = 1f;
                    try
                    {
                        data.SetAlphamaps(0, 0, baseSurface);
                    }
                    catch (Exception resetFailure)
                    {
                        Debug.LogError("[YQSemanticChunkStreamer] Could not reset provisional terrain paint " + coordinate + ": " + resetFailure.Message);
                    }
                    Debug.LogWarning("[YQSemanticChunkStreamer] Provisional biome paint failed for " + coordinate + "; publishing its collision floor with the base terrain layer: " + paintFailure.Message);
                }
            }

            if (!reused)
            {
                // note: Keep a new safety object hidden while Unity creates its native renderer and collider from the sampled authority heights.
                tileObject = Terrain.CreateTerrainGameObject(data);
                if (tileObject == null)
                    throw new InvalidOperationException("Unity could not create the provisional terrain object");
                tile = tileObject.GetComponent<Terrain>();
                collider = tileObject.GetComponent<TerrainCollider>();
                if (tile == null || collider == null)
                    throw new InvalidOperationException("Unity provisional terrain is missing its renderer or collider");
                tile.enabled = false;
                collider.enabled = false;
            }

            tileObject.name = "YQ_PROVISIONAL_GROUND_" + coordinate.x + "_" + coordinate.y;
            tileObject.transform.SetParent(_worldRoot, false);
            tileObject.transform.position = new Vector3(tileMinX, baseOrigin.y, tileMinZ);
            tile.allowAutoConnect = false;
            tile.drawInstanced = true;
            tile.heightmapPixelError = 5f;
            tile.basemapDistance = 1000f;
            tile.materialTemplate = _terrain.materialTemplate;
            tile.treeDistance = 0f;
            tile.detailObjectDistance = 0f;
            tile.drawTreesAndFoliage = false;
            tile.shadowCastingMode = _terrain.shadowCastingMode;
            tile.reflectionProbeUsage = _terrain.reflectionProbeUsage;
            collider.terrainData = data;
            collider.enabled = true;
            // note: Publish the collider before enabling the renderer so an emergency safety floor is never visible without traversable ground.
            tile.Flush();
            tileObject.SetActive(true);
            if (synchronizePhysics)
                Physics.SyncTransforms();
            else
                _groundSafetyPhysicsSyncPending = true;
            tile.enabled = _guaranteedViewDemand.Contains(coordinate);

            _provisionalGroundTiles[coordinate] = tile;
            if (!owner.ownedObjects.Contains(tileObject))
                RegisterOwnedObject(owner, tileObject);
            if (!owner.generatedObjects.Contains(tileObject))
            {
                owner.generatedObjects.Add(tileObject);
                InvalidateRendererValidationCache(owner);
            }
            owner.visualStateKnown = false;
            owner.publicationVersion++;
            _lifecyclePending = true;
            return true;
        }
        catch (Exception exception)
        {
            if (tile != null)
                tile.SetNeighbors(null, null, null, null);
            if (tileObject != null)
                Destroy(tileObject);
            if (data != null)
                Destroy(data);
            Debug.LogWarning("[YQSemanticChunkStreamer] Provisional ground tile failed for " + coordinate + ": " + exception.Message);
            return false;
        }
        finally
        {
            float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - startedAt);
            if (elapsed > _maximumProvisionalGroundTileSeconds)
            {
                _maximumProvisionalGroundTileSeconds = elapsed;
                _slowestProvisionalGroundTile = coordinate;
            }
            // note: Queue publication is already measured around its full scheduler pass; avoid counting this nested native call twice.
            if (recordAggregateSlice)
                RecordAggregateWorkSlice(startedAt, "provisionalGroundTile");
        }
    }

    public bool IsGenerating => _activeGenerations.Count > 0;

    public bool TryApplyFeatureOverlay(
        GeneratedSemanticFeatureOverlayRecord overlay,
        long expectedWorldRevision,
        out YQMutationReceipt receipt)
    {
        receipt = null;
        if (_world == null || _plan == null || overlay == null)
        {
            receipt = new YQMutationReceipt { message = "Feature overlay owner or payload is missing." };
            return false;
        }

        _world.EnsureCollections();
        _plan.EnsureCollections();
        overlay.EnsureCollections();
        if (!string.Equals(overlay.schemaVersion, YQStateContract.FeatureOverlaySchemaVersion, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(overlay.featureId) || string.IsNullOrWhiteSpace(overlay.mutationCommitKey))
        {
            receipt = new YQMutationReceipt { message = "Feature overlay schema, feature ID, or mutation commit key is invalid." };
            return false;
        }

        GeneratedSemanticWorldAuthorityRecord authority = _plan.semanticAuthority;
        authority.EnsureCollections();
        string receiptId = string.IsNullOrWhiteSpace(overlay.receiptId) ? overlay.mutationCommitKey : overlay.receiptId;
        for (int index = 0; index < authority.featureOverlays.Count; index++)
        {
            GeneratedSemanticFeatureOverlayRecord existing = authority.featureOverlays[index];
            if (existing == null ||
                (!string.Equals(existing.mutationCommitKey, overlay.mutationCommitKey, StringComparison.Ordinal) &&
                 !string.Equals(existing.receiptId, receiptId, StringComparison.Ordinal)))
                continue;
            // note: A repeated production receipt is an idempotent success and returns the already accepted revision without mutating the save again.
            receipt = new YQMutationReceipt
            {
                commitKey = existing.mutationCommitKey,
                stateRevision = _world.stateRevision,
                appliedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                applied = false,
                message = "Feature overlay receipt was already applied."
            };
            return true;
        }

        if (!_world.TryApplyMutationCommit(overlay.mutationCommitKey, expectedWorldRevision, out receipt))
            return false;

        authority.featureOverlayRevision = Math.Max(authority.featureOverlayRevision, FindMaximumOverlayRevision(authority)) + 1L;
        overlay.revision = authority.featureOverlayRevision;
        overlay.receiptId = receiptId;
        overlay.ownerRegionId = string.IsNullOrWhiteSpace(overlay.ownerRegionId)
            ? ResolveOverlayOwnerRegion(overlay.featureId)
            : overlay.ownerRegionId;
        authority.featureOverlays.Add(overlay);
        // note: Replaying the accepted overlay immediately keeps already-loaded gameplay aligned with the same persisted authority used on reconstruction.
        ApplyFeatureOverlayToLoadedChunks(overlay);
        ApplyFeatureOverlayToSceneTargets(overlay);
        PersistSemanticFrontier();
        return true;
    }

    public bool RegisterFeatureOverlayTarget(string featureId, GameObject target, string objectId = null)
    {
        if (target == null || string.IsNullOrWhiteSpace(featureId))
            return false;
        YQStreamedFeatureOverlayTarget binding = target.GetComponent<YQStreamedFeatureOverlayTarget>();
        if (binding == null)
            binding = target.AddComponent<YQStreamedFeatureOverlayTarget>();
        binding.featureId = featureId;
        binding.objectId = objectId ?? string.Empty;
        // note: A generated root can bind after streamer configuration during world rebuild; replay accepted overlays at registration so late roots cannot resurrect deleted gameplay.
        if (_plan != null && _plan.semanticAuthority != null)
        {
            _plan.semanticAuthority.EnsureCollections();
            for (int index = 0; index < _plan.semanticAuthority.featureOverlays.Count; index++)
            {
                GeneratedSemanticFeatureOverlayRecord overlay = _plan.semanticAuthority.featureOverlays[index];
                if (overlay != null && OverlayTargetMatches(binding, overlay))
                {
                    // note: Registration only needs to reconcile this new target; a scene-wide scan here repeated once per generated object creates quadratic chunk-publication work.
                    ApplyFeatureOverlayToSceneTarget(binding, overlay);
                }
            }
        }
        // note: Loaded providers register their real accepted root here, including authored roots that are borrowed by the streamer.
        if (_acceptedSpatialProjection != null &&
            (_acceptedSpatialProjection.TryGetSiteBySiteId(featureId, out YQSpatialMaterializationSiteV2 site) ||
             _acceptedSpatialProjection.TryGetSiteBySemanticId(featureId, out site)))
        {
            RuntimeChunk chunk = GetRuntimeChunk(ChunkFor(new Vector2(site.x, site.z)));
            if (chunk != null)
                CacheSharedSitePresentation(chunk, binding, site);
        }
        return true;
    }

    // note: Expose one chunk's lifecycle for the runtime verification probe without exposing mutable streamer state.
    public bool TryGetChunkDiagnostics(Vector2Int coordinate, out YQSemanticChunkLifecycle lifecycle, out bool physicalRepresentation)
    {
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk))
        {
            lifecycle = chunk.state;
            physicalRepresentation = chunk.physicalRepresentation;
            return true;
        }

        lifecycle = YQSemanticChunkLifecycle.Unseen;
        physicalRepresentation = false;
        return false;
    }

    public bool TryGetPublicationSnapshot(Vector2Int coordinate, out YQSemanticChunkPublicationSnapshot snapshot)
    {
        snapshot = default;
        snapshot.coordinate = coordinate;
        snapshot.authoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
        snapshot.hardViewDemanded = _hardViewDemand.Contains(coordinate);
        snapshot.guaranteedViewDemanded = _guaranteedViewDemand.Contains(coordinate);
        snapshot.canonicalPreparationDemanded = _canonicalPreparationDemand.Contains(coordinate);
        snapshot.predictedViewDemanded = _semanticViewPredictionArrivalSeconds.ContainsKey(coordinate);
        snapshot.provisionalGroundPrefetchDemanded = _provisionalGroundPrefetchDemand.Contains(coordinate);
        snapshot.provisionalGroundTurnBufferDemanded = _provisionalGroundTurnBufferDemand.Contains(coordinate);
        snapshot.siteTerrainDependency = _siteTerrainDependencies.ContainsKey(coordinate);
        snapshot.siteTerrainHandoffPending = _siteTerrainHandoffRequests.Contains(coordinate);
        snapshot.publicationVerificationPinned = _publicationVerificationDemand.Contains(coordinate);
        snapshot.currentChunkDistance = Mathf.Max(
            Mathf.Abs(coordinate.x - _currentChunk.x),
            Mathf.Abs(coordinate.y - _currentChunk.y));
        snapshot.effectiveUnloadRadius = EffectiveUnloadRadius;
        snapshot.demanded = snapshot.hardViewDemanded || IsContentDemandedNow(coordinate);
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
        {
            // note: Authored terrain is a pre-existing publication owner; unknown streamed cells remain empty placeholders rather than implicit ready cells.
            if (IsChunkInsideAuthoredTerrain(coordinate))
            {
                bool authoredCollisionReady = IsTerrainColliderReady(_terrain);
                snapshot.lifecycle = YQSemanticChunkLifecycle.Active;
                snapshot.publicationVersion = 1;
                snapshot.ownerEpoch = 1;
                snapshot.physicalRepresentation = authoredCollisionReady;
                snapshot.terrainPublished = authoredCollisionReady;
                snapshot.terrainReadiness = authoredCollisionReady ? YQTerrainReadinessState.CollisionReady : YQTerrainReadinessState.None;
                snapshot.requiredContentReady = authoredCollisionReady;
                snapshot.overlayReady = authoredCollisionReady;
                snapshot.appearanceReady = authoredCollisionReady;
                snapshot.requiredEcologyReady = authoredCollisionReady;
                snapshot.activationComplete = authoredCollisionReady;
                snapshot.traversable = authoredCollisionReady;
                return true;
            }
            return false;
        }

        // note: Report every publication stage and its outstanding work/retry lane without touching queue priority, terrain, or lifecycle state.
        snapshot.publicationVersion = chunk.publicationVersion;
        snapshot.ownerEpoch = chunk.terrainOwnerEpoch;
        snapshot.lifecycle = chunk.state;
        snapshot.physicalRepresentation = chunk.physicalRepresentation;
        snapshot.terrainPublished = chunk.terrainPublished;
        snapshot.terrainReadiness = chunk.terrainReadiness;
        snapshot.requiredContentReady = chunk.requiredContentReady;
        snapshot.overlayReady = chunk.overlayReady;
        snapshot.appearanceReady = chunk.appearanceReady;
        snapshot.requiredEcologyReady = chunk.requiredEcologyReady;
        snapshot.activationComplete = chunk.activationComplete;
        snapshot.traversable = chunk.Traversable;
        snapshot.hasExplicitFailure = !string.IsNullOrWhiteSpace(chunk.failureReason);
        snapshot.failureReason = chunk.failureReason ?? string.Empty;
        // note: Preserve scheduled-work compatibility while exposing an actual coroutine id and phase for barrier assertions.
        bool terrainWorkerActive = coordinate == _terrainPreparingCoordinate &&
            _terrainPreparation != null && _terrainPreparationWorkId != 0;
        bool colliderPublicationPending = _pendingTerrainPublications.TryGetValue(
            coordinate, out PendingTerrainPublication pendingPublication) && pendingPublication != null;
        snapshot.terrainWorkQueued = _terrainQueued.Contains(coordinate);
        snapshot.terrainColliderPublicationPending = colliderPublicationPending;
        snapshot.terrainWorkActive = terrainWorkerActive || colliderPublicationPending || snapshot.terrainWorkQueued;
        snapshot.terrainWorkId = terrainWorkerActive ? _terrainPreparationWorkId :
            colliderPublicationPending ? pendingPublication.token.workId : 0;
        snapshot.terrainWorkPhase = terrainWorkerActive ? _terrainPreparationPhase ?? string.Empty :
            colliderPublicationPending ? pendingPublication.phase ?? "colliderSync" : string.Empty;
        snapshot.terrainWorkElapsedSeconds = terrainWorkerActive
            ? Mathf.Max(0f, Time.realtimeSinceStartup - _terrainPreparationStartedAt)
            : colliderPublicationPending
                ? Mathf.Max(0f, Time.realtimeSinceStartup - pendingPublication.startedAt)
                : 0f;
        // note: The missing-ground gate can wait for the real collision barrier instead of mistaking queued or heightfield work for a held publication.
        snapshot.terrainCollisionPublicationHeld = (terrainWorkerActive || colliderPublicationPending) &&
            string.Equals(snapshot.terrainWorkPhase, "collisionHold", StringComparison.Ordinal) &&
            IsPublicationVerificationStageHeld(coordinate, YQPublicationVerificationStage.TerrainCollision);
        snapshot.terrainRetryScheduled = _terrainRetryAt.ContainsKey(coordinate);
        _terrainPreparationRetryCount.TryGetValue(coordinate, out snapshot.terrainRetryCount);
        snapshot.appearanceWorkActive = _terrainPainting.ContainsKey(coordinate);
        _terrainPaintingWorkIds.TryGetValue(coordinate, out snapshot.appearanceWorkVersion);
        snapshot.appearanceRetryScheduled = _terrainAppearanceRetryAt.ContainsKey(coordinate);
        _terrainAppearanceRetryCount.TryGetValue(coordinate, out snapshot.appearanceRetryCount);
        snapshot.contentWorkActive = _activeGenerationWorkIds.TryGetValue(coordinate, out long contentWorkId) && contentWorkId != 0;
        snapshot.contentWorkId = snapshot.contentWorkActive ? contentWorkId : 0;
        // note: Inspect the authoritative queue rather than trusting the mutable lifecycle label, which is also used for distance accounting.
        snapshot.contentWorkQueued = _queue.Contains(coordinate);
        snapshot.ecologyWorkId = chunk.decorativeWorkId;
        snapshot.ecologyWorkActive = snapshot.ecologyWorkId != 0;
        snapshot.ecologyWorkPhase = chunk.decorativeWorkPhase ?? string.Empty;
        snapshot.ecologyWorkPhaseElapsedSeconds = snapshot.ecologyWorkActive
            ? Mathf.Max(0f, Time.unscaledTime - chunk.decorativePhaseStartedAt)
            : 0f;
        snapshot.ecologyRetryScheduled = _requiredEcologyRetryAt.ContainsKey(coordinate);
        _requiredEcologyRetryCount.TryGetValue(coordinate, out snapshot.ecologyRetryCount);
        snapshot.activationWorkActive = chunk.activationTarget != -1 || chunk.activationCursor > 0;
        snapshot.lifecycleWorkPending = _lifecyclePending;
        return true;
    }

    public bool TryValidatePublicationWorkContract(Vector2Int coordinate, out string failure)
    {
        // note: Check only live ownership evidence; this method never refreshes demand, starts work, or changes lifecycle state.
        if (!TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot))
        {
            failure = "demanded chunk has no runtime owner " + coordinate;
            return false;
        }
        if (!snapshot.demanded || snapshot.traversable || snapshot.hasExplicitFailure)
        {
            failure = string.Empty;
            return true;
        }
        if (snapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady &&
            !snapshot.terrainWorkActive && !snapshot.terrainRetryScheduled)
        {
            failure = "stranded terrain publication " + coordinate + " ownerVersion=" + snapshot.publicationVersion;
            return false;
        }
        // note: A lifecycle label alone cannot prove live work. Downstream stages may wait only on a real queued/active prerequisite or its bounded retry.
        bool terrainPublicationPending = snapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady &&
            (snapshot.terrainWorkActive || snapshot.terrainRetryScheduled);
        if (!snapshot.requiredContentReady && !snapshot.contentWorkActive && !snapshot.contentWorkQueued &&
            !terrainPublicationPending)
        {
            failure = "stranded required-content publication " + coordinate + " ownerVersion=" + snapshot.publicationVersion;
            return false;
        }
        if (!snapshot.overlayReady && !snapshot.contentWorkActive && !snapshot.contentWorkQueued &&
            !terrainPublicationPending)
        {
            failure = "stranded overlay publication " + coordinate + " ownerVersion=" + snapshot.publicationVersion;
            return false;
        }
        if (!snapshot.appearanceReady && !snapshot.appearanceWorkActive &&
            !snapshot.appearanceRetryScheduled && snapshot.requiredContentReady &&
            snapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady)
        {
            failure = "stranded appearance publication " + coordinate + " ownerVersion=" + snapshot.publicationVersion;
            return false;
        }
        if (!snapshot.requiredEcologyReady && !snapshot.ecologyWorkActive &&
            !snapshot.ecologyRetryScheduled && snapshot.requiredContentReady)
        {
            failure = "stranded required-ecology publication " + coordinate + " ownerVersion=" + snapshot.publicationVersion;
            return false;
        }
        bool upstreamPublicationPending = terrainPublicationPending ||
            (!snapshot.requiredContentReady && (snapshot.contentWorkActive || snapshot.contentWorkQueued)) ||
            (!snapshot.overlayReady && (snapshot.contentWorkActive || snapshot.contentWorkQueued)) ||
            (!snapshot.appearanceReady && (snapshot.appearanceWorkActive || snapshot.appearanceRetryScheduled)) ||
            (!snapshot.requiredEcologyReady && (snapshot.ecologyWorkActive || snapshot.ecologyRetryScheduled));
        if (!snapshot.activationComplete && !snapshot.activationWorkActive &&
            !snapshot.lifecycleWorkPending && !upstreamPublicationPending)
        {
            failure = "stranded activation publication " + coordinate + " ownerVersion=" + snapshot.publicationVersion;
            return false;
        }
        failure = string.Empty;
        return true;
    }

    public bool BeginPublicationVerificationScenario(
        Vector2Int coordinate,
        bool holdTerrain,
        bool holdAppearance,
        bool holdRequiredEcology,
        int appearanceFailures,
        int ecologyFailures)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            return false;
        // note: A retained all-direction preparation cell is a safe witness only when it is outside the current frustum, predicted path, and emergency turn buffer.
        bool protectedViewOrTraversalDemand = coordinate == _currentChunk ||
            _guaranteedViewDemand.Contains(coordinate) ||
            _semanticViewPredictionArrivalSeconds.ContainsKey(coordinate) ||
            _provisionalGroundPrefetchDemand.Contains(coordinate) ||
            _provisionalGroundTurnBufferDemand.Contains(coordinate);
        bool preparationOnly = _canonicalPreparationDemand.Contains(coordinate) && !protectedViewOrTraversalDemand;
        // note: Keep fault probes outside the live camera and immediate traversal contract; preparation-only owners remain valid non-visible witnesses.
        if (protectedViewOrTraversalDemand ||
            (_hardViewDemand.Contains(coordinate) && !preparationOnly))
            return false;
        _publicationVerificationScenarios[coordinate] = new PublicationVerificationScenario
        {
            holdTerrain = holdTerrain,
            holdAppearance = holdAppearance,
            holdRequiredEcology = holdRequiredEcology,
            appearanceFailuresRemaining = Mathf.Max(0, appearanceFailures),
            ecologyFailuresRemaining = Mathf.Max(0, ecologyFailures),
            ownerVersionAtStart = chunk.publicationVersion
        };
        // note: A bounded recovery probe owns a real scheduler lease so its publication work cannot age out while the test observes production behavior.
        _publicationVerificationDemand.Add(coordinate);
        return true;
    }

    public bool TryBeginActiveTerrainCollisionVerificationScenario(
        out Vector2Int coordinate,
        out long workId,
        out string workPhase)
    {
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        workId = 0;
        workPhase = string.Empty;
        Vector2Int activeCoordinate = new Vector2Int(int.MinValue, int.MinValue);
        long activeWorkId = 0;
        string activePhase = string.Empty;
        if (_terrainPreparation != null && _terrainPreparationWorkId != 0 &&
            _terrainPreparingCoordinate != _currentChunk &&
            (string.Equals(_terrainPreparationPhase, "colliderSync", StringComparison.Ordinal) ||
             string.Equals(_terrainPreparationPhase, "collisionHold", StringComparison.Ordinal)))
        {
            activeCoordinate = _terrainPreparingCoordinate;
            activeWorkId = _terrainPreparationWorkId;
            activePhase = _terrainPreparationPhase ?? string.Empty;
        }
        else
        {
            foreach (KeyValuePair<Vector2Int, PendingTerrainPublication> pair in _pendingTerrainPublications)
            {
                PendingTerrainPublication pending = pair.Value;
                if (pending == null || pair.Key == _currentChunk ||
                    (!string.Equals(pending.phase, "colliderSync", StringComparison.Ordinal) &&
                     !string.Equals(pending.phase, "collisionHold", StringComparison.Ordinal)))
                    continue;
                activeCoordinate = pair.Key;
                activeWorkId = pending.token.workId;
                activePhase = pending.phase ?? "colliderSync";
                break;
            }
        }
        if (activeWorkId == 0 || !_chunks.TryGetValue(activeCoordinate, out RuntimeChunk chunk) || chunk == null ||
            chunk.terrainReadiness >= YQTerrainReadinessState.CollisionReady ||
            IsChunkInsideAuthoredTerrain(activeCoordinate) || !IsTerrainRequestStillRelevant(activeCoordinate) ||
            _publicationVerificationScenarios.ContainsKey(activeCoordinate))
            return false;

        // note: Attach the probe to a real production owner at its collider boundary without promoting or replacing scheduler work.
        if (!BeginPublicationVerificationScenario(activeCoordinate, true, false, false, 0, 0))
            return false;
        coordinate = activeCoordinate;
        workId = activeWorkId;
        workPhase = activePhase;
        return true;
    }

    public bool TryPrepareAppearanceRetryVerification(Vector2Int coordinate)
    {
        // note: Reuse only retained owners outside protected camera demand so retry and lag probes preserve the live visible surface.
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
            coordinate == _currentChunk || _hardViewDemand.Contains(coordinate) ||
            _guaranteedViewDemand.Contains(coordinate) || !chunk.physicalRepresentation ||
            !chunk.terrainPublished || !chunk.appearanceReady ||
            !_extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) ||
            terrain == null || terrain.terrainData == null || !IsTerrainColliderReady(terrain))
            return false;

        int distance = Mathf.Max(
            Mathf.Abs(coordinate.x - _currentChunk.x),
            Mathf.Abs(coordinate.y - _currentChunk.y));
        if (!_hardViewDemand.Contains(coordinate) && !IsContentDemandedNow(coordinate))
        {
            if (distance > EffectiveVisualRetentionRadius)
                return false;
            _contentDemand.Add(coordinate);
        }

        // note: Reuse a published physical owner so the retry probe exercises only the production appearance lane, not a second full world admission.
        StopTerrainPaintingForChunk(chunk);
        terrain.enabled = false;
        chunk.appearanceReady = false;
        chunk.visualReady = false;
        chunk.activationComplete = false;
        chunk.visualStateKnown = false;
        chunk.activeState = -1;
        chunk.activationTarget = -1;
        chunk.activationCursor = 0;
        chunk.failureReason = string.Empty;
        chunk.publicationVersion++;
        _terrainAppearanceRetryAt.Remove(coordinate);
        _terrainAppearanceRetryCount.Remove(coordinate);
        _lifecyclePending = true;
        return true;
    }

    public bool ReleasePublicationVerificationStage(Vector2Int coordinate, YQPublicationVerificationStage stage)
    {
        if (!_publicationVerificationScenarios.TryGetValue(coordinate, out PublicationVerificationScenario scenario) || scenario == null)
            return false;
        if (stage == YQPublicationVerificationStage.TerrainCollision)
            scenario.holdTerrain = false;
        else if (stage == YQPublicationVerificationStage.Appearance)
            scenario.holdAppearance = false;
        else
            scenario.holdRequiredEcology = false;
        return true;
    }

    public void EndPublicationVerificationScenario(Vector2Int coordinate)
    {
        // note: Removing the gate never changes the owner; it only lets already-admitted production work complete normally.
        _publicationVerificationScenarios.Remove(coordinate);
        _publicationVerificationDemand.Remove(coordinate);
    }

    public bool TryReplacePublicationOwnerForVerification(
        Vector2Int coordinate,
        out int previousPublicationVersion,
        out int replacementPublicationVersion)
    {
        previousPublicationVersion = 0;
        replacementPublicationVersion = 0;
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            return false;
        bool protectedViewOrTraversalDemand = coordinate == _currentChunk ||
            _guaranteedViewDemand.Contains(coordinate) ||
            _semanticViewPredictionArrivalSeconds.ContainsKey(coordinate) ||
            _provisionalGroundPrefetchDemand.Contains(coordinate) ||
            _provisionalGroundTurnBufferDemand.Contains(coordinate);
        bool preparationOnly = _canonicalPreparationDemand.Contains(coordinate) && !protectedViewOrTraversalDemand;
        if (protectedViewOrTraversalDemand ||
            (_hardViewDemand.Contains(coordinate) && !preparationOnly))
            return false;
        // note: Replacement stays on a bounded physical owner outside live camera/traversal demand, while preparation-only residency supplies a safe non-visible witness.
        if (!_hardViewDemand.Contains(coordinate) && !IsContentDemandedNow(coordinate))
        {
            int distance = Mathf.Max(
                Mathf.Abs(coordinate.x - _currentChunk.x),
                Mathf.Abs(coordinate.y - _currentChunk.y));
            if (distance > EffectiveVisualRetentionRadius || !chunk.physicalRepresentation)
                return false;
            _contentDemand.Add(coordinate);
        }
        if (coordinate == _terrainPreparingCoordinate)
            return false;
        _publicationVerificationDemand.Add(coordinate);
        previousPublicationVersion = chunk.publicationVersion;
        // note: DestroyGeneratedObjects retires the owner before stopping its workers, including content.
        for (int index = _queue.Count - 1; index >= 0; index--)
        {
            if (_queue[index] == coordinate)
                _queue.RemoveAt(index);
        }
        _terrainQueued.Remove(coordinate);
        _terrainRetryAt.Remove(coordinate);
        _terrainRequestedAt.Remove(coordinate);
        _terrainCriticalRequestedAt.Remove(coordinate);
        DestroyGeneratedObjects(chunk);
        chunk.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
        chunk.failureReason = string.Empty;
        chunk.publicationVersion++;
        _publicationVerificationScenarios.Remove(coordinate);
        RequestTerrainForChunk(coordinate);
        Enqueue(coordinate);
        _lifecyclePending = true;
        replacementPublicationVersion = chunk.publicationVersion;
        return true;
    }

    private bool IsPublicationVerificationStageHeld(Vector2Int coordinate, YQPublicationVerificationStage stage)
    {
        if (!_publicationVerificationScenarios.TryGetValue(coordinate, out PublicationVerificationScenario scenario) || scenario == null)
            return false;
        if (stage == YQPublicationVerificationStage.TerrainCollision)
            return scenario.holdTerrain;
        if (stage == YQPublicationVerificationStage.Appearance)
            return scenario.holdAppearance;
        return scenario.holdRequiredEcology;
    }

    private bool ConsumePublicationVerificationFailure(Vector2Int coordinate, YQPublicationVerificationStage stage)
    {
        if (!_publicationVerificationScenarios.TryGetValue(coordinate, out PublicationVerificationScenario scenario) || scenario == null)
            return false;
        if (stage == YQPublicationVerificationStage.Appearance && scenario.appearanceFailuresRemaining > 0)
        {
            scenario.appearanceFailuresRemaining--;
            return true;
        }
        if (stage == YQPublicationVerificationStage.RequiredEcology && scenario.ecologyFailuresRemaining > 0)
        {
            scenario.ecologyFailuresRemaining--;
            return true;
        }
        return false;
    }

    public bool TryGetUnloadedStreamedChunk(out Vector2Int coordinate)
    {
        // note: Return the protected physical anchor first so the later revisit assertion examines the same owner that pruning was required to preserve.
        if (_unloadedHistoryAnchor.x != int.MinValue &&
            _chunks.TryGetValue(_unloadedHistoryAnchor, out RuntimeChunk anchor) && anchor != null &&
            anchor.state == YQSemanticChunkLifecycle.Unloaded &&
            !IsChunkInsideAuthoredTerrain(_unloadedHistoryAnchor))
        {
            coordinate = _unloadedHistoryAnchor;
            return true;
        }
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            if (pair.Value != null && pair.Value.state == YQSemanticChunkLifecycle.Unloaded &&
                !IsChunkInsideAuthoredTerrain(pair.Key))
            {
                coordinate = pair.Key;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    public static YQPlayerFollowingSemanticChunkStreamer Attach(
        Transform worldRoot,
        WorldState world,
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        Transform playerOverride = null)
    {
        if (worldRoot == null || world == null || plan == null)
            return null;

        YQPlayerFollowingSemanticChunkStreamer streamer =
            worldRoot.GetComponent<YQPlayerFollowingSemanticChunkStreamer>() ??
            worldRoot.gameObject.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
        // note: Publish the single configured streamer before gameplay can ask the motor for a traversability decision.
        Active = streamer;
        streamer.Configure(worldRoot, world, plan, terrain, playerOverride);
        return streamer;
    }

    private void Configure(
        Transform worldRoot,
        WorldState world,
        GeneratedWorldPlanRecord plan,
        Terrain terrain,
        Transform playerOverride)
    {
        // note: Retain the prior detached authority long enough to remove only metadata it provably contributed during an in-place rebind.
        CancelContinuationPopulationWork();
        YQSpatialBlueprintV2 previousAcceptedBlueprint = _acceptedBlueprint;
        CancelFrontierConstructionWork();
        _frontierConstructionAttempts.Clear();
        _frontierConstructionAttemptOrder.Clear();
        _frontierConstructionScan = 0;
        _nextFrontierConstructionAt = Time.unscaledTime;
        if (_configured)
        {
            // note: Treat every reconfiguration as an atomic epoch transition, including in-place plan edits whose object references remain unchanged.
            _configurationEpoch++;
            // note: Old waiters cannot carry terrain priority or pins into the replacement world.
            _siteTerrainDependencies.Clear();
            _siteTerrainHandoffRequests.Clear();
            ClearSharedSiteDemand();
            _physicalDemand.Clear();
            _physicalDemandVisibleOverflow = false;
            _physicalDemandOverflowReported = false;
            _siteTerrainProgress.Clear();
            // note: Stop every bounded content worker during a world rebind so no retired owner can publish after the epoch changes.
            _activeGenerationWorkIds.Clear();
            foreach (Coroutine activeGeneration in _activeGenerations.Values)
                if (activeGeneration != null)
                    StopCoroutine(activeGeneration);
            _activeGenerations.Clear();
            _demandCancelledGenerations.Clear();
            if (_terrainPreparation != null)
            {
                // note: End pure background sampling before unwinding its one owning terrain coroutine during a runtime rebind.
                _terrainHeightPreparationCancellation?.Cancel();
                StopCoroutine(_terrainPreparation);
            }
            CancelAllPendingTerrainPublications();
            _terrainPreparation = null;
            _terrainPreparingCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            CancelAllTerrainHeightPrefetches();
            StopAllTerrainPainting();
            // note: A rebuilt world cannot inherit queue entries or generated roots from the previous plan identity.
            foreach (RuntimeChunk oldChunk in _chunks.Values)
                DestroyGeneratedObjects(oldChunk);
            DestroyOrphanedExtensionTerrains();
            DestroyProvisionalGroundPool();
            _chunks.Clear();
            _publicationVerificationScenarios.Clear();
            _publicationVerificationDemand.Clear();
            _persistedChunkIndex.Clear();
            // note: A rebind starts a new runtime ownership epoch, so an anchor from the previous world cannot protect stale data.
            _unloadedHistoryAnchor = new Vector2Int(int.MinValue, int.MinValue);
            _queue.Clear();
            _hardViewDemand.Clear();
            _canonicalPreparationDemand.Clear();
            _guaranteedViewDemand.Clear();
            _provisionalGroundPrefetchDemand.Clear();
            _provisionalGroundTurnBufferDemand.Clear();
            _highSpeedTurnBufferCoverageDemand.Clear();
            _highSpeedTurnBufferCoverageReadyCount = 0;
            _terrainQueue.Clear();
            _terrainQueued.Clear();
            _terrainRetryAt.Clear();
            _terrainPreparationRetryCount.Clear();
            _terrainRetryScratch.Clear();
            _terrainRequestedAt.Clear();
            _terrainCriticalRequestedAt.Clear();
            _terrainReadinessSamples.Clear();
            _terrainReadinessScratch.Clear();
            _terrainReadyWindowCoordinates.Clear();
            _maximumTerrainReadinessSeconds = 0f;
            _extendedTerrainTiles.Clear();
            _provisionalGroundTiles.Clear();
            _provisionalGroundQueue.Clear();
            _provisionalGroundQueued.Clear();
            _lastTerrainBuildSeconds = 0f;
            _maximumTerrainBuildSeconds = 0f;
            _lastTerrainPaintSeconds = 0f;
            _maximumTerrainPaintSeconds = 0f;
            _lastStreamingWorkSeconds = 0f;
            _maximumStreamingWorkSeconds = 0f;
            _maximumAggregateFrameWorkSeconds = 0f;
            _maximumAggregateFrameWorkStage = string.Empty;
            _maximumAggregateFrameWorkFrame = -1;
            _hardViewContentDispatchReservationCount = 0;
            _hardViewContentProgressReservationCount = 0;
            _lastHardViewContentProgressOwner = new Vector2Int(int.MinValue, int.MinValue);
            _maximumPublicationSliceSeconds = 0f;
            _maximumPublicationSliceStage = string.Empty;
            _lastTerrainSliceSeconds = 0f;
            _maximumTerrainSliceSeconds = 0f;
            _maximumTerrainSlicePhase = string.Empty;
            _lastContentSliceSeconds = 0f;
            _maximumContentSliceSeconds = 0f;
            _maximumMaterializationSubstageSeconds = 0f;
            _maximumMaterializationSubstage = string.Empty;
            _lastTerrainPaintSliceSeconds = 0f;
            _maximumTerrainPaintSliceSeconds = 0f;
            _hasLastLookaheadPosition = false;
            _hasPredictiveTerrainCorridor = false;
            _lastPredictiveTerrainCell = new Vector2Int(int.MinValue, int.MinValue);
            _lastPredictiveTerrainDirection = Vector2Int.zero;
            _lastPredictiveTerrainRadius = -1;
            _predictiveTerrainCorridorCoordinates.Clear();
            _predictiveTerrainCorridorCursor = 0;
            _maximumPredictiveTerrainAdmissionsPerFrame = 0;
            _maximumPredictiveTerrainOwnerCreationsPerFrame = 0;
            _maximumPredictiveTerrainAdmissionSeconds = 0f;
            _frontierRefreshPending = false;
            _terrainPreparationPreempted = false;
        }
        _worldRoot = worldRoot;
        _world = world;
        _plan = plan;
        _terrain = terrain;
        if (_terrain != null)
            _terrain.allowAutoConnect = false;
        _world.EnsureCollections();
        _plan.EnsureCollections();
        // note: A new configuration epoch must resolve and verify its own immutable V2 materialization before any cell can reuse it.
        _acceptedSpatialProjection = null;
        _acceptedBlueprint = YQSpatialPlanVersionRouter.TryValidateAcceptedV2(_plan, out _)
            ? JsonUtility.FromJson<YQSpatialBlueprintV2>(JsonUtility.ToJson(_plan.spatialPlanV2.blueprint))
            : null;
        LogAcceptedPhysicalContractSummary();
        _observedSpatialArtifactSignature = BuildSpatialArtifactSignature(_plan);
        _lifecyclePending = false;
        _lifecycleBudgetFrame = -1;
        _streamingRetirementFrame = -1;
        _streamingRetirementsThisFrame = 0;
        _retiredPhysicalOwnersThisFrame = 0;
        _hardViewLifecycleCursor = 0;
        _unloadingLifecycleCursor = 0;
        // note: Re-resolve the accepted artifact on every configuration pass so an in-place save revision cannot leave the authority on stale arrays.
        _heightSamplerAttempted = false;
        _heightSampler = null;
        _continuousAuthority = null;
        if (!_heightSamplerAttempted)
        {
            _heightSamplerAttempted = true;
            YQGeneratedWorldTerrain.TryCreateV2HeightSampler(
                _plan,
                out _heightSampler,
                out string samplerFailure);
            if (_heightSampler == null && YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan))
                Debug.LogError("[YQSemanticChunkStreamer] V2 terrain continuation unavailable: " + samplerFailure);
        }
        // note: Rebuild the continuation authority from the persisted seed and final origin terrain so every cell uses the same world-space fields.
        _continuousAuthority = new YQContinuousWorldCellAuthority(_plan.worldSeed, _terrain, _heightSampler, _plan, chunkWorldSize);
        for (int index = 0; index < _plan.semanticChunks.Count; index++)
        {
            GeneratedSemanticChunkRecord existing = _plan.semanticChunks[index];
            if (existing != null)
            {
                Vector2Int coordinate = new Vector2Int(existing.chunkX, existing.chunkZ);
                _continuousAuthority.PopulateCellRecord(existing, new Vector2Int(existing.chunkX, existing.chunkZ), chunkWorldSize, _plan);
                RefreshAcceptedFeatureMetadata(existing, coordinate, previousAcceptedBlueprint, _acceptedBlueprint);
                AddPortalFeatureIds(existing);
                CanonicalizeAcceptedProjectionFeatureMetadata(existing, coordinate);
            }
        }
        _player = playerOverride != null ? playerOverride : ResolvePlayer();
        _playerMotor = ResolvePlayerMotor(_player);
        BuildSemanticIndex();
        AssignExistingPhysicalObjects();
        ApplyPersistedOverlaysToExistingChunks();
        ApplyPersistedOverlaysToSceneTargets();
        _configured = true;
        RefreshForPlayer(true);
    }

    // note: Keep Unity's Update message while exposing this scheduler owner's complete frame cost to the bounded CPU capture.
    private void Update()
    {
        if (!_configured)
            return;
        using (StreamingUpdateMarker.Auto())
            UpdateStreamingFrame();
    }

    private void UpdateStreamingFrame()
    {
        if (!_configured)
            return;

        // note: A late accepted-plan replacement invalidates previously published physical chunks instead of leaving a tree-only frontier behind.
        if (RefreshSpatialArtifactIfChanged())
            return;

        if (_player == null)
        {
            _player = ResolvePlayer();
            _playerMotor = ResolvePlayerMotor(_player);
        }
        if (_player == null)
            return;

        // note: Reset per-Update attribution so the runtime speed probe can identify work from its actual worst frame.
        _lastStreamingWorstStageSeconds = 0f;
        _lastStreamingWorstStage = string.Empty;
        BeginAggregateBudgetFrame();
        ReserveRequiredHardViewEcologyBudget();
        float streamingWorkStarted = Time.realtimeSinceStartup;
        if (_playerMotor == null || !_playerMotor ||
            (_playerMotor.IsAuthoritative && YQInvestorPlayerMotor.ActiveMotor != _playerMotor))
            _playerMotor = ResolvePlayerMotor(_player);

        float stageStarted = Time.realtimeSinceStartup;
        // note: A late world rebind can briefly clear the runtime index; rebuild the frontier before movement reaches an unowned cell.
        if (_chunks.Count == 0)
        {
            BuildSemanticIndex();
            RefreshForPlayer(true);
            if (_chunks.Count == 0)
                return;
        }

        Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
        Vector2Int nextChunk = ChunkFor(playerPosition);
        if (_frontierRefreshPending)
        {
            // note: Preserve the bounded scan cursor across chunk crossings; restarting its square every few frames starves the frontier at high travel speeds.
            _frontierRefreshPosition = playerPosition;
            ProcessFrontierRefreshSlice();
        }
        else if (nextChunk != _currentChunk)
        {
            _lastPlayerPosition = playerPosition;
            RefreshForPlayer(false);
        }
        RecordStreamingStage("frontier", stageStarted);

        // note: Prewarm terrain toward every nearby cell edge so height/collider publication leads the player instead of waiting for a boundary crossing.
        float workStarted;
        stageStarted = Time.realtimeSinceStartup;
        workStarted = Time.realtimeSinceStartup;
        if (CanAdvanceAggregateWork("predictiveTerrainAdmission"))
            RequestTerrainLookahead(playerPosition);
        RecordStreamingStage("predictiveTerrainAdmission", stageStarted);
        RecordAggregateWorkSlice(workStarted, "predictiveTerrainAdmission");

        // note: Camera-visible cells are a hard physical/content demand, not merely a priority hint; admit them before any queue can be processed or trimmed.
        stageStarted = Time.realtimeSinceStartup;
        workStarted = Time.realtimeSinceStartup;
        bool hardViewAdmissionChanged = ShouldRefreshHardViewDemand(playerPosition);
        bool hardViewAdmissionRefreshed = false;
        if (_cameraViewAdmissionPending)
        {
            // note: Reuse the prepared view envelope while a movement key changes; only camera/player admission changes rebuild the full hard-view set.
            if (hardViewAdmissionChanged)
            {
                RefreshHardViewDemand(playerPosition, false);
                hardViewAdmissionRefreshed = true;
            }
        }
        else if (hardViewAdmissionChanged)
        {
            RefreshHardViewDemand(playerPosition, false);
            hardViewAdmissionRefreshed = true;
        }
        if (!hardViewAdmissionRefreshed && _queuePriorityViewValid && _queuePriorityCamera != null &&
            ResolveTraversalDemandVelocity().sqrMagnitude >=
            PredictiveSemanticViewMinimumSpeedMetersPerSecond * PredictiveSemanticViewMinimumSpeedMetersPerSecond)
        {
            // note: Direction edges update semantic lookahead here; the all-direction ground turn ring is rebuilt only on spatial/view admission, avoiding a full radial rescan for every rapid input turn.
            Vector2 cameraPlanarPosition = new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z);
            Vector2Int cameraChunk = ChunkFor(cameraPlanarPosition);
            RefreshPredictiveSemanticViewDemand(cameraPlanarPosition, cameraChunk, Mathf.Max(32f, chunkWorldSize));
        }
        RecordStreamingStage("hardViewAdmission", stageStarted);
        RecordAggregateWorkSlice(workStarted, "hardViewAdmission");
        ReserveRequiredHardViewEcologyBudget();

        // note: Start current and pre-view ecology before terrain uploads consume the shared frame allowance; these bounded workers must finish before their cells cross into view.
        stageStarted = Time.realtimeSinceStartup;
        if (HasUnreadyHardViewEcology(new Vector2Int(int.MinValue, int.MinValue)))
            ResumeDeferredDecorativeGeneration(true);
        RecordStreamingStage("hardViewEcologyAdmission", stageStarted);

        if (Time.unscaledTime >= _nextRequiredCoverageAdmissionAt &&
            CanAdvanceAggregateWork("requiredCoverageRepair"))
        {
            // note: Required nearby coverage is authoritative even during high-speed verification; prediction may add work but cannot suppress the view/readiness ring.
            _nextRequiredCoverageAdmissionAt = Time.unscaledTime + RequiredCoverageAdmissionIntervalSeconds;
            stageStarted = Time.realtimeSinceStartup;
            workStarted = Time.realtimeSinceStartup;
            RequestRequiredTerrainCoverage();
            RecordStreamingStage("requiredCoverageRepair", stageStarted);
            RecordAggregateWorkSlice(workStarted, "requiredCoverageRepair");
        }

        // note: Freeze the admitted union before dispatch so terrain, content, shared-site, and predictive owners consult the same bounded physical capacity.
        bool physicalDemandReady = CanAdvanceAggregateWork("physicalDemandAdmission");
        if (physicalDemandReady)
        {
            workStarted = Time.realtimeSinceStartup;
            RebuildPhysicalDemandSet();
            MaintainHighSpeedTurnBufferGroundDemand();
            SortContentQueue();
            SortTerrainQueue();
            TrimContentQueue();
            TrimQueue();
            TrimTerrainQueue();
            RecordAggregateWorkSlice(workStarted, "physicalDemandAndQueueAdmission");
        }

        // note: Admit ready hard-view content before terrain upload spends the remaining shared slice; prewarm work must start before it reaches the live frustum.
        bool hardViewContentDispatched = false;
        if (physicalDemandReady &&
            (HasReadyHardViewContentCandidate() || TryGetReadyHardPreparationContentCandidate(out _)) &&
            CanAdvanceAggregateWork("contentDispatch"))
        {
            stageStarted = Time.realtimeSinceStartup;
            ProcessQueue(_reservedHardViewContentDispatchFrame == Time.frameCount ? 1 : MaximumContentGenerationWorkers);
            RecordStreamingStage("contentDispatch", stageStarted);
            hardViewContentDispatched = true;
        }

        // note: Reserve a budget bypass only for live-view collision and the next traversal boundary; distant preparation stays within the shared frame allowance.
        stageStarted = Time.realtimeSinceStartup;
        bool priorityTerrainPending = HasQueuedUrgentTerrain();
        bool provisionalGroundProgressed = false;
        bool highSpeedTurnBufferGroundPending = HasPendingHighSpeedTurnBufferGroundWork();
        if (highSpeedTurnBufferGroundPending && physicalDemandReady &&
            CanAdvanceAggregateWork("turnBufferGroundProgress"))
        {
            // note: Give the bounded same-authority safety floor one publication/sampling quantum before canonical terrain dispatch consumes the frame allowance.
            workStarted = Time.realtimeSinceStartup;
            ProcessProvisionalGroundQueue();
            RecordAggregateWorkSlice(workStarted, "turnBufferGroundProgress");
            provisionalGroundProgressed = true;
        }
        bool terrainDispatchBudgetReady = physicalDemandReady && CanAdvanceAggregateWork("terrainDispatch");
        if ((physicalDemandReady && terrainDispatchBudgetReady) || priorityTerrainPending)
        {
            ProcessTerrainQueue();
            if (terrainDispatchBudgetReady)
                RetryFailedTerrainRequests();
            RecordAggregateWorkSlice(stageStarted, terrainDispatchBudgetReady ? "terrainDispatch" : "priorityTerrainDispatch");
        }
        else
            RecordFixedTerrainObservation(physicalDemandReady ? "refused:dispatchBudget" : "refused:demandRefreshBudget", FixedTerrainTraceTarget);
        RecordStreamingStage("terrainDispatch", stageStarted);

        bool visibleGroundWorkPending = HasPendingVisibleGroundWork();
        bool provisionalGroundWorkPending = _provisionalGroundQueue.Count > 0 || _provisionalGroundSamplers.Count > 0;
        highSpeedTurnBufferGroundPending = HasPendingHighSpeedTurnBufferGroundWork();
        if (!provisionalGroundProgressed &&
            ((physicalDemandReady && CanAdvanceAggregateWork("provisionalGroundPublication")) ||
             visibleGroundWorkPending ||
             highSpeedTurnBufferGroundPending))
        {
            // note: Current visible ground and high-speed turn-buffer cells keep one bounded floor quantum even while unrelated canonical terrain remains queued.
            workStarted = Time.realtimeSinceStartup;
            ProcessProvisionalGroundQueue();
            RecordAggregateWorkSlice(workStarted,
                visibleGroundWorkPending
                    ? "visibleProvisionalGround"
                    : provisionalGroundWorkPending ? "provisionalGroundSafetySampling" : "provisionalGroundPublication");
        }

        stageStarted = Time.realtimeSinceStartup;
        bool appearanceReconciliationBudgetReady = physicalDemandReady &&
            CanAdvanceAggregateWork("appearanceReconciliation");
        if (appearanceReconciliationBudgetReady)
            ReconcileDemandedAppearanceSlice();
        else if (physicalDemandReady)
            TryReconcileUrgentViewAppearance();
        RecordStreamingStage("appearanceReconciliation", stageStarted);

        stageStarted = Time.realtimeSinceStartup;
        if (!hardViewContentDispatched && physicalDemandReady && CanAdvanceAggregateWork("contentDispatch"))
            ProcessQueue(_reservedHardViewContentDispatchFrame == Time.frameCount ? 1 : MaximumContentGenerationWorkers);
        UpdateTerrainTelemetry();
        RecordStreamingStage("contentDispatch", stageStarted);

        // note: Terrain/content deadlines receive their dispatch first; pending site actors use only the remaining shared allowance.
        if (!ShouldReserveFrameForTraversalTerrain() && CanAdvanceAggregateWork("continuationPopulation"))
            ProcessContinuationPopulationWork();
        if (!ShouldReserveFrameForTraversalTerrain() && CanAdvanceAggregateWork("frontierConstruction"))
            ProcessFrontierConstructionWork();

        stageStarted = Time.realtimeSinceStartup;
        // note: Let required content and ecology coroutines use the remaining shared slice before the final pre-render activation pass.
        // note: LateUpdate applies the same pending lifecycle once, after those workers have had a chance to publish.
        RecordStreamingStage("lifecycle", stageStarted);

        // note: Re-admit interrupted optional ecology only after the current hard-view set has first received every available scatter slot.
        if (CanAdvanceAggregateWork("optionalEcologyDispatch"))
            ResumeDeferredDecorativeGeneration();

        stageStarted = Time.realtimeSinceStartup;
        // note: Continue deferred distant-root eviction after the boundary transition so one teleport cannot destroy an entire streamed frontier in one frame.
        if (_semanticPrunePending && CanAdvanceAggregateWork("semanticPrune"))
        {
            workStarted = Time.realtimeSinceStartup;
            _semanticPrunePending = !PruneSemanticHistory(SemanticEvictionsPerFrame);
            RecordAggregateWorkSlice(workStarted, "semanticPrune");
        }
        RecordStreamingStage("prune", stageStarted);
        _lastStreamingWorkSeconds = Time.realtimeSinceStartup - streamingWorkStarted;
        _lastStreamingWorkFrameCount = Time.frameCount;
        _maximumStreamingWorkSeconds = Mathf.Max(_maximumStreamingWorkSeconds, _lastStreamingWorkSeconds);
        RecordFixedTerrainObservation("updateComplete", FixedTerrainTraceTarget);
        // note: Emit diagnostics after closing the measured slice so logging cannot masquerade as a gameplay streaming stall.
        if (Time.unscaledTime >= _nextDiagnosticsAt)
        {
            _nextDiagnosticsAt = Time.unscaledTime + DiagnosticsIntervalSeconds;
            float diagnosticsStartedAt = Time.realtimeSinceStartup;
            int guaranteedViewLoaded = 0;
            foreach (Vector2Int demandedCoordinate in _guaranteedViewDemand)
            {
                // note: Count only the strict live frustum; the wider preparation ring is deliberately not a presentation deadline.
                if (_chunks.TryGetValue(demandedCoordinate, out RuntimeChunk demandedChunk) &&
                    IsFullyLoadedForView(demandedCoordinate, demandedChunk))
                    guaranteedViewLoaded++;
            }
            Debug.Log("[YQSemanticChunkStreamer] CHUNK STREAM\n" +
                "Current=" + _currentChunk +
                " active=" + _activeCount +
                " retained=" + _retainedCount +
                " queued=" + _queuedCount +
                 " guaranteedView=" + _guaranteedViewDemand.Count +
                 " guaranteedViewLoaded=" + guaranteedViewLoaded +
                 " guaranteedViewPending=" + Mathf.Max(0, _guaranteedViewDemand.Count - guaranteedViewLoaded) +
                 " preparationView=" + Mathf.Max(0, _hardViewDemand.Count - _guaranteedViewDemand.Count) +
                " semantic=" + _chunks.Count +
                " physical=" + _physicalCount +
                " terrainBuildLast=" + _lastTerrainBuildSeconds.ToString("0.000") +
                " terrainBuildMax=" + _maximumTerrainBuildSeconds.ToString("0.000") +
                " terrainPaintLast=" + _lastTerrainPaintSeconds.ToString("0.000") +
                " terrainPaintMax=" + _maximumTerrainPaintSeconds.ToString("0.000") +
                " semanticSaveLast=" + _lastSemanticSaveSeconds.ToString("0.000") +
                " semanticSaveMax=" + _maximumSemanticSaveSeconds.ToString("0.000") +
                " streamingWorkLast=" + _lastStreamingWorkSeconds.ToString("0.000") +
                " streamingWorkMax=" + _maximumStreamingWorkSeconds.ToString("0.000") +
                " streamingStageMax=" + _maximumStreamingStageSeconds.ToString("0.000") +
                " streamingStage=" + _maximumStreamingStage +
                " aggregateFrameMax=" + _maximumAggregateFrameWorkSeconds.ToString("0.000") +
                " aggregateBudgetDeferred=" + _aggregateBudgetDeferredSlices +
                " aggregateBudgetStage=" + _lastAggregateWorkStage +
                " physicalDemand=" + _physicalDemand.Count +
                " physicalDemandOverflow=" + _physicalDemandOverflowCount +
                " terrainSliceMax=" + _maximumTerrainSliceSeconds.ToString("0.000") +
                " contentSliceMax=" + _maximumContentSliceSeconds.ToString("0.000") +
                " materializationSubstageMax=" + _maximumMaterializationSubstageSeconds.ToString("0.000") +
                " materializationSubstage=" + _maximumMaterializationSubstage +
                " decorativeActive=" + _activeDecorativeScatters +
                " decorativeMax=" + _maximumConcurrentDecorativeScatters +
                " ecologyWorkers=" + DescribeEcologyWorkers() +
                " traversalBlocks=" + _traversalConstraintBlockCount +
                " traversalBlockCell=" + _lastTraversalConstraintCell);
            _maximumStreamerDiagnosticLogSeconds = Mathf.Max(
                _maximumStreamerDiagnosticLogSeconds,
                Time.realtimeSinceStartup - diagnosticsStartedAt);
        }
    }

    // note: Profile the post-motor hard-view pass separately so a long camera-admission frame has an identifiable owner.
    private void LateUpdate()
    {
        if (!_configured || _player == null)
            return;
        using (StreamingLateUpdateMarker.Auto())
            UpdateStreamingLateFrame();
        RecordFixedTerrainObservation("lateUpdateComplete", FixedTerrainTraceTarget);
        SampleCompleteCellObservation("frameEnd");
    }

    private void UpdateStreamingLateFrame()
    {
        if (!_configured || _player == null)
            return;

        float lateUpdateStarted = Time.realtimeSinceStartup;
        try
        {
            // note: The motor applies look rotation after Update; admit and hide the newly exposed frustum before Unity renders the frame.
            float stageStarted = Time.realtimeSinceStartup;
            UpdateQueuePriorityView();
            RecordStreamingStage("lateViewUpdate", stageStarted);
            if (!_queuePriorityViewValid || _queuePriorityCamera == null)
                return;
            Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
            stageStarted = Time.realtimeSinceStartup;
            bool admissionChanged = ShouldRefreshHardViewDemand(playerPosition);
            RecordStreamingStage("lateHardViewAdmissionCheck", stageStarted);

            // note: LateUpdate observes the final camera pose, but sub-cell follow/rotation reuses the admitted all-direction safety runway.
            if (admissionChanged)
            {
                stageStarted = Time.realtimeSinceStartup;
                RefreshHardViewDemand(playerPosition);
                RecordStreamingStage("lateHardViewAdmission", stageStarted);
            }
            if (_lifecyclePending || _cameraViewAdmissionPending || admissionChanged)
            {
                // note: One final-pose pass handles pending publication and changed view demand without repeating a full owner census in this frame.
                stageStarted = Time.realtimeSinceStartup;
                ApplyLifecycle();
                RecordStreamingStage("lateLifecycle", stageStarted);
            }

            if (_cameraViewAdmissionPending)
            {
                stageStarted = Time.realtimeSinceStartup;
                bool currentCoverageReady = TryValidateCurrentVisualCoverage(out _);
                RecordStreamingStage("lateVisualCoverage", stageStarted);
                if (!currentCoverageReady)
                    return;

                _cameraViewAdmissionPending = false;
            }
        }
        finally
        {
            // note: Include post-motor demand, validation, and activation in the same frame receipt as Update so profiler attribution covers the full streamer owner.
            float lateUpdateSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - lateUpdateStarted);
            _lastStreamingWorkSeconds += lateUpdateSeconds;
            _lastStreamingWorkFrameCount = Time.frameCount;
            _maximumStreamingWorkSeconds = Mathf.Max(_maximumStreamingWorkSeconds, _lastStreamingWorkSeconds);
        }
    }

    private bool RefreshSpatialArtifactIfChanged()
    {
        string currentSignature = BuildSpatialArtifactSignature(_plan);
        if (string.Equals(currentSignature, _observedSpatialArtifactSignature, StringComparison.Ordinal))
            return false;

        _observedSpatialArtifactSignature = currentSignature;
        if (_worldRoot == null || _world == null || _plan == null)
            return false;

        Debug.Log("[YQSemanticChunkStreamer] ACCEPTED SPATIAL ARTIFACT CHANGED; REBUILDING PHYSICAL FRONTIER.");
        Configure(_worldRoot, _world, _plan, _terrain, _player);
        return true;
    }

    private static string BuildSpatialArtifactSignature(GeneratedWorldPlanRecord plan)
    {
        GeneratedSpatialWorldPlanV2Record artifact = plan?.spatialPlanV2;
        if (artifact == null)
            return "<none>";

        return string.Concat(
            artifact.schemaVersion, "|",
            artifact.generationVersion, "|",
            artifact.validationVersion, "|",
            artifact.acceptanceState.ToString(), "|",
            artifact.contentHash, "|",
            artifact.validatedContentHash);
    }

    private bool TryPrepareContinuationRefresh(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationRefresh refresh, out string failure)
    {
        refresh = null;
        failure = "Continuation refresh requires the current idle streaming authority.";
        if (_acceptedSpatialProjection == null || _continuousAuthority == null || _plan?.semanticAuthority == null ||
            _continuationPopulationWork.Count > 0 ||
            !YQSpatialMaterializationResolverV2.TryStageContinuationInstallation(_world, construction, out var installation, out failure))
            return false;
        var candidate = installation.Candidate;
        var previous = _plan.spatialPlanV2.acceptedContinuation;
        int previousCount = previous?.locations?.Count ?? 0;
        if (candidate?.locations == null || candidate.locations.Count <= previousCount)
        { failure = "Continuation refresh requires an appended accepted location."; return false; }
        for (int index = 0; index < previousCount; index++)
            if (!string.Equals(JsonUtility.ToJson(previous.locations[index]), JsonUtility.ToJson(candidate.locations[index]), StringComparison.Ordinal))
            { failure = "Continuation refresh cannot replace an existing accepted location."; return false; }
        var staged = new PreparedContinuationRefresh
        {
            configurationEpoch = _configurationEpoch, installation = installation,
            previousProjection = _acceptedSpatialProjection, previousCellAuthority = _continuousAuthority,
            previousSemanticAuthority = _plan.semanticAuthority,
            overlaySnapshot = Newtonsoft.Json.JsonConvert.SerializeObject(_plan.semanticAuthority.featureOverlays),
            nextCellAuthority = _continuousAuthority.WithAcceptedMaterialization(installation.Prepared)
        };
        var appendedSites = new HashSet<string>(StringComparer.Ordinal);
        for (int index = previousCount; index < candidate.locations.Count; index++)
            appendedSites.Add(candidate.locations[index].anchor.siteId);
        var prepared = installation.Prepared;
        // note: Guard the complete terrain modifier envelope, including its broad transition beyond the visible road shoulder.
        for (int index = 0; index < prepared.ContinuationPadCount; index++)
        {
            var pad = prepared.GetContinuationPad(index);
            if (!appendedSites.Contains(pad.ownerSiteId)) continue;
            staged.terrainInfluences.Add(YQGeneratedWorldRuntimeBuilder.ContinuationPadTerrainInfluence(
                pad.x, pad.z, pad.reservedRadius, pad.shoulderWidth));
        }
        for (int index = 0; index < prepared.RouteCount; index++)
        {
            var route = prepared.GetRoute(index);
            if (!appendedSites.Contains(route.continuationOwnerSiteId)) continue;
            for (int point = 1; point < prepared.GetRoutePointCount(index); point++)
            {
                var a = prepared.GetRoutePoint(index, point - 1); var b = prepared.GetRoutePoint(index, point);
                staged.terrainInfluences.Add(YQGeneratedWorldRuntimeBuilder.ContinuationRouteTerrainInfluence(
                    a.x, a.z, b.x, b.z, route.width, route.shoulderWidth));
            }
        }
        if (staged.terrainInfluences.Count == 0)
        { failure = "Continuation refresh contains no measured terrain influences."; return false; }
        if (!YQSemanticWorldAuthority.TryPrepareContinuationAuthority(_plan, candidate, prepared,
                out staged.nextSemanticAuthority, out failure) || !TryValidateContinuationRefresh(staged, out failure)) return false;
        refresh = staged;
        return true;
    }

    private bool TryValidateContinuationRefresh(PreparedContinuationRefresh refresh, out string failure)
    {
        failure = "Continuation streaming owners changed during staging.";
        if (refresh == null || refresh.configurationEpoch != _configurationEpoch || !isActiveAndEnabled ||
            !ReferenceEquals(_continuousAuthority, refresh.previousCellAuthority) ||
            !ReferenceEquals(_acceptedSpatialProjection, refresh.previousProjection) ||
            !ReferenceEquals(_plan?.semanticAuthority, refresh.previousSemanticAuthority) ||
            _continuationPopulationWork.Count > 0 ||
            refresh.nextSemanticAuthority.featureOverlayRevision != _plan.semanticAuthority.featureOverlayRevision ||
            refresh.nextSemanticAuthority.overlaySchemaVersion != _plan.semanticAuthority.overlaySchemaVersion ||
            !ReferenceEquals(refresh.nextSemanticAuthority.featureOverlays, _plan.semanticAuthority.featureOverlays) ||
            !string.Equals(refresh.overlaySnapshot, Newtonsoft.Json.JsonConvert.SerializeObject(_plan.semanticAuthority.featureOverlays), StringComparison.Ordinal) ||
            !YQSpatialMaterializationResolverV2.TryValidateBeforePublication(_world, refresh.installation, out failure)) return false;
        return TryValidateContinuationTerrainOwnership(refresh.terrainInfluences, out failure);
    }

    private bool TryValidateContinuationTerrainOwnership(IReadOnlyList<Rect> influences, out string failure)
    {
        // note: Early numeric screening and final publication share ownership vetoes; this read-only guard grants no provenance or installation authority.
        failure = "Continuation terrain influence bounds are missing or invalid.";
        if (influences == null || influences.Count == 0) return false;
        foreach (Rect bounds in influences)
            if (float.IsNaN(bounds.xMin) || float.IsInfinity(bounds.xMin) || float.IsNaN(bounds.xMax) || float.IsInfinity(bounds.xMax) ||
                float.IsNaN(bounds.yMin) || float.IsInfinity(bounds.yMin) || float.IsNaN(bounds.yMax) || float.IsInfinity(bounds.yMax) ||
                bounds.width <= 0f || bounds.height <= 0f) return false;
        if (_terrain != null && _terrain.terrainData != null)
        {
            Vector3 position = _terrain.transform.position, size = _terrain.terrainData.size;
            if (IntersectsContinuationTerrainInfluence(influences, Rect.MinMaxRect(position.x, position.z, position.x + size.x, position.z + size.z)))
            { failure = "Continuation terrain influence intersects the accepted origin terrain."; return false; }
        }
        foreach (var coordinate in _extendedTerrainTiles.Keys)
            if (RejectOwnedContinuationTerrainCell(influences, coordinate, "published terrain", out failure)) return false;
        foreach (var coordinate in _provisionalGroundTiles.Keys)
            if (RejectOwnedContinuationTerrainCell(influences, coordinate, "provisional terrain", out failure)) return false;
        foreach (var coordinate in _pendingTerrainPublications.Keys)
            if (RejectOwnedContinuationTerrainCell(influences, coordinate, "pending terrain publication", out failure)) return false;
        foreach (var coordinate in _terrainPainting.Keys)
            if (RejectOwnedContinuationTerrainCell(influences, coordinate, "terrain painting", out failure)) return false;
        if (_terrainPreparation != null && RejectOwnedContinuationTerrainCell(influences, _terrainPreparingCoordinate, "terrain preparation", out failure)) return false;
        foreach (var coordinate in _activeGenerations.Keys)
            if (RejectOwnedContinuationTerrainCell(influences, coordinate, "content generation", out failure)) return false;
        foreach (var pair in _chunks)
            if (pair.Value != null && (pair.Value.physicalRepresentation || pair.Value.generationRoot != null ||
                pair.Value.decorativeRoot != null || pair.Value.ownedObjects.Count > 0 || pair.Value.generatedObjects.Count > 0) &&
                RejectOwnedContinuationTerrainCell(influences, pair.Key, "materialized content", out failure)) return false;
        failure = string.Empty;
        return true;
    }

    private bool RejectOwnedContinuationCell(PreparedContinuationRefresh refresh, Vector2Int coordinate, string owner, out string failure)
    {
        return RejectOwnedContinuationTerrainCell(refresh.terrainInfluences, coordinate, owner, out failure);
    }

    private bool RejectOwnedContinuationTerrainCell(IReadOnlyList<Rect> influences, Vector2Int coordinate, string owner, out string failure)
    {
        failure = string.Empty;
        if (!ContinuationTerrainInfluencesCell(influences, coordinate)) return false;
        failure = "Continuation terrain influence intersects " + owner + " in cell " + coordinate + ".";
        return true;
    }

    private bool ContinuationInfluencesCell(PreparedContinuationRefresh refresh, Vector2Int coordinate)
    {
        return ContinuationTerrainInfluencesCell(refresh.terrainInfluences, coordinate);
    }

    private bool ContinuationTerrainInfluencesCell(IReadOnlyList<Rect> influences, Vector2Int coordinate)
    {
        float size = Mathf.Max(32f, chunkWorldSize);
        return IntersectsContinuationTerrainInfluence(influences, Rect.MinMaxRect(WorldGridOrigin + coordinate.x * size,
            WorldGridOrigin + coordinate.y * size, WorldGridOrigin + (coordinate.x + 1f) * size, WorldGridOrigin + (coordinate.y + 1f) * size));
    }

    private static bool IntersectsContinuationInfluence(PreparedContinuationRefresh refresh, Rect bounds)
    {
        return IntersectsContinuationTerrainInfluence(refresh.terrainInfluences, bounds);
    }

    private static bool IntersectsContinuationTerrainInfluence(IReadOnlyList<Rect> influences, Rect bounds)
    {
        foreach (Rect influence in influences)
            if (influence.xMin <= bounds.xMax && influence.xMax >= bounds.xMin &&
                influence.yMin <= bounds.yMax && influence.yMax >= bounds.yMin) return true;
        return false;
    }

    private bool TryPublishAndInstallContinuation(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction, out string failure)
    {
        if (!TryPrepareContinuationRefresh(construction, out var refresh, out failure)) return false;
        var profile = YQProfileSaveSystem.Instance;
        if (profile == null) { failure = "Continuation profile publication owner is unavailable."; return false; }
        // note: Preparation may capture auxiliary stores; repeat exact streaming guards immediately before the synchronous paired pointer commit.
        if (!profile.TryPrepareContinuationPublication(construction, out var publication, out failure) ||
            !TryValidateContinuationRefresh(refresh, out failure) ||
            !profile.TryPublishContinuation(publication, out _, out failure)) return false;
        try
        {
        if (!YQSpatialMaterializationResolverV2.TryInstallPublishedContinuation(_world, refresh.installation, out failure))
        {
            // note: A durable accepted save remains authoritative; stop this owner if the precompiled runtime installation unexpectedly disagrees.
            Debug.LogError("[YQSemanticChunkStreamer] Published continuation runtime installation failed: " + failure);
            enabled = false;
            return false;
        }
        _acceptedSpatialProjection = refresh.installation.Prepared;
        _continuousAuthority = refresh.nextCellAuthority;
        _plan.semanticAuthority = refresh.nextSemanticAuthority;
        foreach (var chunk in _chunks.Values)
            foreach (var presentation in chunk.sitePresentations)
                presentation.projection = _acceptedSpatialProjection;
        RetireContinuationPrefetches(refresh);
        // note: Only records with no terrain/content ownership are reprojected; all existing live roots and epochs stay intact.
        foreach (var record in _plan.semanticChunks)
        {
            if (record == null) continue;
            var coordinate = new Vector2Int(record.chunkX, record.chunkZ);
            if (!ContinuationInfluencesCell(refresh, coordinate)) continue;
            _continuousAuthority.PopulateCellRecord(record, coordinate, chunkWorldSize, _plan);
            AddPortalFeatureIds(record);
            CanonicalizeAcceptedProjectionFeatureMetadata(record, coordinate);
        }
        RebuildPhysicalDemandSet();
        _lifecyclePending = true;
        _nextSemanticSaveAt = Mathf.Min(_nextSemanticSaveAt, Time.unscaledTime + 1f);
        failure = string.Empty;
        return true;
        }
        catch (Exception exception)
        {
            // note: Never continue with a partially refreshed runtime owner after a durable pointer; the accepted save can replay on a fresh bind.
            failure = "Published continuation runtime refresh failed: " + exception.Message;
            Debug.LogError("[YQSemanticChunkStreamer] " + failure);
            enabled = false;
            return false;
        }
    }

    private void RetireContinuationPrefetches(PreparedContinuationRefresh refresh)
    {
        // note: Pure samplers may finish on an older immutable projection; retire only affected entries so their results cannot publish stale heights.
        var affected = new List<Vector2Int>();
        foreach (var coordinate in _terrainHeightPrefetches.Keys)
            if (ContinuationInfluencesCell(refresh, coordinate)) affected.Add(coordinate);
        foreach (var coordinate in affected)
        {
            var task = _terrainHeightPrefetches[coordinate];
            _terrainHeightPrefetchCancellations.TryGetValue(coordinate, out var cancellation);
            _terrainHeightPrefetches.Remove(coordinate); _terrainHeightPrefetchCancellations.Remove(coordinate);
            cancellation?.Cancel();
            if (task.IsCompleted) { if (task.IsFaulted) _ = task.Exception; cancellation?.Dispose(); }
            else _ = task.ContinueWith(completed => { if (completed.IsFaulted) _ = completed.Exception; cancellation?.Dispose(); }, TaskScheduler.Default);
        }
        affected.Clear();
        foreach (var coordinate in _provisionalGroundSamplers.Keys)
            if (ContinuationInfluencesCell(refresh, coordinate)) affected.Add(coordinate);
        foreach (var coordinate in affected) CancelProvisionalGroundSampler(coordinate, _provisionalGroundSamplers[coordinate]);
    }

    private void LogAcceptedPhysicalContractSummary()
    {
        if (!YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan))
            return;

        if (!YQSpatialMaterializationResolverV2.TryGetPrepared(
                _plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out string failure) || prepared == null)
        {
            // note: Record an authority failure at bind time so a later tree/rock-only symptom cannot be mistaken for an intentionally quiet accepted cell.
            Debug.LogError(
                "[YQSemanticChunkStreamer] ACCEPTED V2 CONTRACT UNAVAILABLE: " +
                (string.IsNullOrWhiteSpace(failure) ? "unknown projection failure" : failure));
            return;
        }
        _acceptedSpatialProjection = prepared;

        // note: Include the accepted world envelope so a tree-and-rock screenshot can be classified as quiet wilderness versus travel outside the persisted physical contract without scanning runtime hierarchies.
        GeneratedSpatialWorldPlanV2Record artifact = _plan?.spatialPlanV2;
        Debug.Log("[YQSemanticChunkStreamer] ACCEPTED V2 CONTRACT " +
            "generation=" + (artifact?.generationVersion ?? string.Empty) +
            " hash=" + (artifact?.contentHash ?? string.Empty) +
            " worldSize=" + (artifact?.blueprint != null ? artifact.blueprint.worldSize.ToString("0") : "0") +
            " authoredHalfExtent=" + YQContinuousWorldFeatureAuthority.WorldGridOrigin.ToString("0") +
            " routes=" + prepared.RouteCount +
            " water=" + prepared.WaterCount +
            " crossings=" + prepared.CrossingCount +
            " sites=" + prepared.SiteCount);
    }

    // note: Start one streamer-wide allowance per Unity frame, shared by Update work and every manually advanced coroutine.
    private void BeginAggregateBudgetFrame()
    {
        if (_aggregateBudgetFrame == Time.frameCount)
            return;
        _aggregateBudgetFrame = Time.frameCount;
        _aggregateFrameWorkSeconds = 0f;
        _aggregateBudgetDeferredSlices = 0;
        _reservedHardViewContentProgressFrame = -1;
        _reservedHardViewContentProgressOwner = new Vector2Int(int.MinValue, int.MinValue);
        _hardViewContentProgressReservedSeconds = 0f;
        _nextContentSliceUsesHardViewReserve = false;
    }

    // note: Do not start another indivisible main-thread slice after the shared deadline; visible publication remains fail-closed while deferred work resumes next frame.
    private bool CanAdvanceAggregateWork(
        string stage,
        bool useHardViewActivationBudget = false,
        bool useUrgentTerrainColliderSyncBudget = false,
        bool useHardViewContentProgressBudget = false,
        bool liveViewContentProgress = false,
        Vector2Int? contentProgressCoordinate = null,
        Vector2Int? appearanceCoordinate = null)
    {
        BeginAggregateBudgetFrame();
        // note: Protect one existing ecology slice when hard-view required habitat can advance, without adding a separate budget or weakening the total cap.
        float aggregateBudgetLimit = _requiredHardViewEcologyBudgetFrame == Time.frameCount &&
            !string.Equals(stage, "requiredHardViewEcology", StringComparison.Ordinal)
            ? Mathf.Max(0f, AggregateMainThreadBudgetSeconds - RequiredHardViewEcologyBudgetReserveSeconds)
            : AggregateMainThreadBudgetSeconds;
        if (_aggregateFrameWorkSeconds >= aggregateBudgetLimit)
        {
            // note: Honor the separate, time-bounded lifecycle allowance only for complete cells already inside the live view.
            if (useHardViewActivationBudget &&
                (stage == "lifecycleActivation" || stage == "lifecycleObjectActivation") &&
                _lifecycleBudgetFrame == Time.frameCount &&
                Time.realtimeSinceStartup < _lifecycleDeadline)
            {
                _lastAggregateWorkStage = stage;
                return true;
            }
            if (string.Equals(stage, "hardViewTerrainAppearance", StringComparison.Ordinal) &&
                _reservedHardViewAppearanceFrame != Time.frameCount)
            {
                // note: Choose the existing reserve by camera deadline, independent of the order Unity resumes painters.
                if (!HasHigherPriorityAppearanceReserveOwner(appearanceCoordinate))
                {
                    _reservedHardViewAppearanceFrame = Time.frameCount;
                    _lastAggregateWorkStage = stage;
                    return true;
                }
            }
            if (string.Equals(stage, "turnBufferGroundProgress", StringComparison.Ordinal) &&
                _reservedTurnBufferGroundProgressFrame != Time.frameCount)
            {
                _reservedTurnBufferGroundProgressFrame = Time.frameCount;
                _lastAggregateWorkStage = stage;
                return true;
            }
            // note: Keep one critical collision preparation slice moving after an indivisible safety-floor publish overruns the budget.
            if (string.Equals(stage, "terrainHeightUpload", StringComparison.Ordinal) &&
                _terrainPreparation != null &&
                _reservedTerrainUploadFrame != Time.frameCount &&
                TerrainPreparationPriority(_terrainPreparingCoordinate, ResolveTraversalVelocity()) <= 3)
            {
                _reservedTerrainUploadFrame = Time.frameCount;
                _lastAggregateWorkStage = stage;
                return true;
            }
            // note: Keep demanded collider publication moving after background slices spend the budget; Physics.SyncTransforms remains globally limited to one call per frame.
            if (useUrgentTerrainColliderSyncBudget &&
                string.Equals(stage, "terrainColliderSync", StringComparison.Ordinal) &&
                _reservedTerrainColliderSyncFrame != Time.frameCount)
            {
                _reservedTerrainColliderSyncFrame = Time.frameCount;
                _lastAggregateWorkStage = stage;
                return true;
            }
            bool activeLiveViewContentWorker = useHardViewContentProgressBudget &&
                !liveViewContentProgress && HasActiveLiveViewContentWorker();
            bool selectedHardViewContentProgressOwner = useHardViewContentProgressBudget &&
                contentProgressCoordinate.HasValue &&
                (liveViewContentProgress
                    ? IsNextLiveViewContentProgressOwner(contentProgressCoordinate.Value)
                    : !activeLiveViewContentWorker && IsNextHardPreparationContentProgressOwner(contentProgressCoordinate.Value));
            bool ownsHardViewProgressReserve = contentProgressCoordinate.HasValue &&
                _reservedHardViewContentProgressFrame == Time.frameCount &&
                _reservedHardViewContentProgressOwner == contentProgressCoordinate.Value;
            bool hardViewProgressReserveAvailable =
                _aggregateFrameWorkSeconds < AggregateMainThreadBudgetSeconds &&
                _hardViewContentProgressReservedSeconds < RequiredHardViewEcologyBudgetReserveSeconds;
            bool hardViewProgressOwnerCanReserve = ownsHardViewProgressReserve ||
                (_reservedHardViewContentProgressFrame != Time.frameCount && selectedHardViewContentProgressOwner);
            if (useHardViewContentProgressBudget &&
                hardViewProgressOwnerCanReserve &&
                hardViewProgressReserveAvailable)
            {
                // note: Spend up to the already-reserved 4 ms on one deadline-selected owner; total streamer work remains under its 10 ms cap.
                if (!ownsHardViewProgressReserve)
                {
                    _reservedHardViewContentProgressFrame = Time.frameCount;
                    _reservedHardViewContentProgressOwner = contentProgressCoordinate.Value;
                    _hardViewContentProgressReservationCount++;
                    RecordHardViewProgressReservation(contentProgressCoordinate, reserved: true, yieldedToLive: false);
                    _lastHardViewContentProgressOwner = contentProgressCoordinate.Value;
                }
                _nextContentSliceUsesHardViewReserve = true;
                _lastAggregateWorkStage = stage;
                return true;
            }
            if (useHardViewContentProgressBudget && liveViewContentProgress && !selectedHardViewContentProgressOwner)
                RecordHardViewProgressReservation(contentProgressCoordinate, reserved: false, yieldedToLive: false);
            else if (useHardViewContentProgressBudget && activeLiveViewContentWorker)
                RecordHardViewProgressReservation(contentProgressCoordinate, reserved: false, yieldedToLive: true);
            else if (useHardViewContentProgressBudget &&
                _reservedHardViewContentProgressFrame == Time.frameCount)
                RecordHardViewProgressReservation(contentProgressCoordinate, reserved: false, yieldedToLive: false);
            // note: Only an unspent content-dispatch reserve consumes this candidate result; unrelated denied slices must not rescan both queues for every owner.
            if (string.Equals(stage, "contentDispatch", StringComparison.Ordinal) &&
                _reservedHardViewContentDispatchFrame != Time.frameCount &&
                (HasReadyHardViewContentCandidate() || TryGetReadyHardPreparationContentCandidate(out _)))
            {
                // note: Keep ready hard-view content progressing with one worker start while preserving the shared aggregate budget for every other slice.
                _reservedHardViewContentDispatchFrame = Time.frameCount;
                _hardViewContentDispatchReservationCount++;
                _lastAggregateWorkStage = stage;
                return true;
            }
            if (_aggregateBudgetDeferredSlices == 0)
                _lastAggregateWorkStage = stage;
            _aggregateBudgetDeferredSlices++;
            return false;
        }
        if (_aggregateBudgetDeferredSlices == 0)
            _lastAggregateWorkStage = stage;
        return true;
    }

    private void RecordHardViewProgressReservation(
        Vector2Int? coordinate,
        bool reserved,
        bool yieldedToLive)
    {
        // note: Count at most once per owner/frame so nested iterator checks do not distort the shared-reservation evidence.
        if (!coordinate.HasValue ||
            !_chunks.TryGetValue(coordinate.Value, out RuntimeChunk owner) || owner == null ||
            owner.lastHardViewProgressDiagnosticFrame == Time.frameCount)
            return;

        owner.lastHardViewProgressDiagnosticFrame = Time.frameCount;
        if (reserved)
            owner.hardViewProgressReserveSlices++;
        else if (yieldedToLive)
            owner.hardViewProgressYieldedToLiveFrames++;
        else
            owner.hardViewProgressReserveDeniedFrames++;
    }

    private bool IsNextLiveViewContentProgressOwner(Vector2Int requested)
    {
        // note: Pick the earliest live-view deadline, then rotate equal deadlines by stable cell order after the prior slice owner.
        if (!_guaranteedViewDemand.Contains(requested) || !_activeGenerations.ContainsKey(requested))
            return false;

        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        Vector2Int selected = new Vector2Int(int.MinValue, int.MinValue);
        bool found = false;
        foreach (Vector2Int candidate in _activeGenerations.Keys)
        {
            if (!_guaranteedViewDemand.Contains(candidate) ||
                !_chunks.TryGetValue(candidate, out RuntimeChunk owner) || owner == null ||
                owner.state != YQSemanticChunkLifecycle.Generating)
                continue;

            if (!found)
            {
                selected = candidate;
                found = true;
                continue;
            }

            int deadline = CompareContentQueueDeadlines(candidate, selected, traversalVelocity);
            if (deadline < 0)
            {
                selected = candidate;
                continue;
            }
            if (deadline > 0)
                continue;

            bool candidateAfterLast = _lastHardViewContentProgressOwner.x != int.MinValue &&
                CompareCoordinates(candidate, _lastHardViewContentProgressOwner) > 0;
            bool selectedAfterLast = _lastHardViewContentProgressOwner.x != int.MinValue &&
                CompareCoordinates(selected, _lastHardViewContentProgressOwner) > 0;
            if (candidateAfterLast != selectedAfterLast
                    ? candidateAfterLast
                    : CompareCoordinates(candidate, selected) < 0)
                selected = candidate;
        }

        return found && selected == requested;
    }

    private bool IsNextHardPreparationContentProgressOwner(Vector2Int requested)
    {
        // note: When no live-view worker is pending, give the earliest ready hard-view preparation owner the reserved ecology slice.
        if (!_hardViewDemand.Contains(requested) || _guaranteedViewDemand.Contains(requested) ||
            !_activeGenerations.ContainsKey(requested))
            return false;

        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        Vector2Int selected = new Vector2Int(int.MinValue, int.MinValue);
        bool found = false;
        foreach (Vector2Int candidate in _activeGenerations.Keys)
        {
            if (!_hardViewDemand.Contains(candidate) || _guaranteedViewDemand.Contains(candidate) ||
                !_chunks.TryGetValue(candidate, out RuntimeChunk owner) || owner == null ||
                owner.state != YQSemanticChunkLifecycle.Generating)
                continue;

            int deadline = found
                ? CompareContentQueueDeadlines(candidate, selected, traversalVelocity)
                : -1;
            bool candidateAfterLast = _lastHardViewContentProgressOwner.x != int.MinValue &&
                CompareCoordinates(candidate, _lastHardViewContentProgressOwner) > 0;
            bool selectedAfterLast = found && _lastHardViewContentProgressOwner.x != int.MinValue &&
                CompareCoordinates(selected, _lastHardViewContentProgressOwner) > 0;
            if (!found || deadline < 0 || (deadline == 0 && (candidateAfterLast != selectedAfterLast
                    ? candidateAfterLast
                    : CompareCoordinates(candidate, selected) < 0)))
            {
                selected = candidate;
                found = true;
            }
        }
        return found && selected == requested;
    }

    private void RecordRequiredContentWorkSlice(float startedAt)
    {
        // note: Charge only the selected required-content MoveNext time against its reserved share of the existing aggregate budget.
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - startedAt);
        if (_nextContentSliceUsesHardViewReserve)
        {
            _hardViewContentProgressReservedSeconds += elapsed;
            _nextContentSliceUsesHardViewReserve = false;
        }
        RecordAggregateWorkSlice(startedAt, "requiredContent");
    }

    private void RecordAggregateWorkSlice(float startedAt, string stage)
    {
        BeginAggregateBudgetFrame();
        float elapsed = Mathf.Max(0f, Time.realtimeSinceStartup - startedAt);
        _aggregateFrameWorkSeconds += elapsed;
        if (_aggregateFrameWorkSeconds > _maximumAggregateFrameWorkSeconds)
        {
            _maximumAggregateFrameWorkSeconds = _aggregateFrameWorkSeconds;
            _maximumAggregateFrameWorkStage = stage;
            _maximumAggregateFrameWorkFrame = Time.frameCount;
        }
        if (elapsed > 0f)
            _lastAggregateWorkStage = stage;
    }

    // note: Merge all live demand classes into one capacity-limited scheduler authority before any terrain/content dispatch.
    private void RebuildPhysicalDemandSet()
    {
        // note: Diagnostic revision tracks when the existing dispatch map was rebuilt, without changing its authority.
        if (_fixedTerrainObservations != null)
        {
            _fixedTerrainDemandRevision++;
            _fixedTerrainDemandFrame = Time.frameCount;
        }
        _physicalDemand.Clear();
        _physicalDemandOverflowCount = 0;
        _physicalDemandVisibleOverflow = false;
        AddPhysicalDemand(_currentChunk, PhysicalDemandReason.Occupied);
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.Visible);
        // note: Keep the complete occupied-cell handoff neighborhood before any speculative runway cells across reversals and diagonal turns.
        for (int z = -1; z <= 1; z++)
        {
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0)
                    continue;
                AddPhysicalDemand(_currentChunk + new Vector2Int(x, z), PhysicalDemandReason.ImmediateNeighbor);
            }
        }
        foreach (Vector2Int coordinate in _siteTerrainDependencies.Keys)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.SharedSite);
        foreach (Vector2Int coordinate in _siteTerrainHandoffRequests)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.SharedSite);
        // note: Admit the near-term all-direction collision ring before the distant predictive view runway can consume the physical cap.
        foreach (Vector2Int coordinate in _provisionalGroundTurnBufferDemand)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.TurnCoverage);
        foreach (Vector2Int coordinate in _hardViewDemand)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.TurnCoverage);
        // note: Derive owners from external member demand before speculative travel can fill the existing physical capacity.
        RebuildSharedSiteDemand();
        foreach (Vector2Int coordinate in _sharedSiteOwnerDemand)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.SharedSite);
        foreach (Vector2Int coordinate in _contentDemand)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.Travel);
        foreach (Vector2Int coordinate in _publicationVerificationDemand)
            AddPhysicalDemand(coordinate, PhysicalDemandReason.Travel);
        if (_player != null)
        {
            Vector2Int predicted = ResolvePredictedContentChunk(
                new Vector2(_player.position.x, _player.position.z));
            AddPhysicalDemand(predicted, PhysicalDemandReason.Travel);
        }

        if (_physicalDemandVisibleOverflow)
        {
            _cameraViewAdmissionPending = true;
            if (!_physicalDemandOverflowReported)
            {
                Debug.LogError("[YQSemanticChunkStreamer] PHYSICAL DEMAND CAPACITY EXCEEDED; visible or required owners are withheld. capacity=" +
                    PhysicalOwnerCapacity + " overflow=" + _physicalDemandOverflowCount);
                _physicalDemandOverflowReported = true;
            }
        }
        else
            _physicalDemandOverflowReported = false;

        // note: Owner work uses the existing bounded content queue; a remote owner is not lost when its visible member is outside the owner's ordinary radius.
        foreach (Vector2Int coordinate in _sharedSiteOwnerDemand)
        {
            if (!_physicalDemand.ContainsKey(coordinate))
                continue;
            RuntimeChunk chunk = GetOrCreateChunk(coordinate);
            if (!chunk.physicalRepresentation && chunk.state != YQSemanticChunkLifecycle.Generating)
                Enqueue(coordinate);
        }
    }

    public bool IsSharedSiteDemanded(string identity) =>
        // note: A derived request cannot bypass physical admission through the independent authored-site loader.
        !string.IsNullOrWhiteSpace(identity) && _sharedSiteIdentityDemand.TryGetValue(identity, out Vector2Int owner) &&
        _physicalDemand.ContainsKey(owner);

    private void ClearSharedSiteDemand()
    {
        _sharedSiteOwnerDemand.Clear();
        _sharedSiteVisibleOwners.Clear();
        _sharedSiteVisibleIdentities.Clear();
        _sharedSiteOwnerArrivalSeconds.Clear();
        _sharedSiteIdentityDemand.Clear();
    }

    private void RebuildSharedSiteDemand()
    {
        // note: Never seed a new demand from yesterday's derived owner set; this permits retirement after the final member leaves.
        ClearSharedSiteDemand();
        if (_acceptedSpatialProjection == null)
            return;
        foreach (Vector2Int member in _physicalDemand.Keys)
            AddSharedSiteDemandFromMember(member);
        foreach (Vector2Int member in _contentDemand)
            AddSharedSiteDemandFromMember(member);
        foreach (Vector2Int member in _publicationVerificationDemand)
            AddSharedSiteDemandFromMember(member);
    }

    private void AddSharedSiteDemandFromMember(Vector2Int member)
    {
        float size = Mathf.Max(32f, chunkWorldSize);
        _acceptedSpatialProjection.CollectSiteIndicesForCell(member, size, _sharedSiteIndexScratch);
        bool visible = _guaranteedViewDemand.Contains(member) || member == _currentChunk ||
            Mathf.Max(Mathf.Abs(member.x - _currentChunk.x), Mathf.Abs(member.y - _currentChunk.y)) <= EffectiveVisualRadius;
        foreach (int index in _sharedSiteIndexScratch)
        {
            YQSpatialMaterializationSiteV2 site = _acceptedSpatialProjection.GetSite(index);
            if (site.kind == YQSiteKindV2.Origin)
                continue;
            Vector2Int owner = ChunkFor(new Vector2(site.x, site.z));
            _sharedSiteOwnerDemand.Add(owner);
            if (visible)
            {
                _sharedSiteVisibleOwners.Add(owner);
                _sharedSiteVisibleIdentities.Add(site.siteId);
            }
            if (_semanticViewPredictionArrivalSeconds.TryGetValue(member, out float arrival) &&
                (!_sharedSiteOwnerArrivalSeconds.TryGetValue(owner, out float priorArrival) || arrival < priorArrival))
                _sharedSiteOwnerArrivalSeconds[owner] = arrival;
            _sharedSiteIdentityDemand[site.siteId] = owner;
            if (!string.IsNullOrWhiteSpace(site.sourceSemanticId)) _sharedSiteIdentityDemand[site.sourceSemanticId] = owner;
        }
    }

    private bool AreSharedSiteOwnersReady(Vector2Int member)
    {
        if (_acceptedSpatialProjection == null)
            return true;
        _acceptedSpatialProjection.CollectSiteIndicesForCell(member, Mathf.Max(32f, chunkWorldSize), _sharedSiteReadinessScratch);
        foreach (int index in _sharedSiteReadinessScratch)
        {
            YQSpatialMaterializationSiteV2 site = _acceptedSpatialProjection.GetSite(index);
            if (site.kind == YQSiteKindV2.Origin)
                continue;
            Vector2Int owner = ChunkFor(new Vector2(site.x, site.z));
            // note: Inspect the owner's own publication directly, without recursively following overlapping sectors.
            if (!_chunks.TryGetValue(owner, out RuntimeChunk chunk) ||
                !IsReadyForSharedSitePreparation(owner, chunk) ||
                !IsSharedSitePresentationReady(chunk, site.siteId))
                return false;
        }
        return true;
    }

    private bool TryGetSiteAwareViewArrival(Vector2Int coordinate, out float seconds)
    {
        // note: An owner's preparation inherits its earliest member exposure without changing the canonical camera forecast.
        bool found = _semanticViewPredictionArrivalSeconds.TryGetValue(coordinate, out seconds);
        if (_sharedSiteOwnerArrivalSeconds.TryGetValue(coordinate, out float memberSeconds))
        {
            if (!found || memberSeconds < seconds) seconds = memberSeconds;
            found = true;
        }
        return found;
    }

    // note: Retain explicit reasons for prioritization and report critical overflow instead of silently claiming an impossible envelope.
    private void AddPhysicalDemand(Vector2Int coordinate, PhysicalDemandReason reason)
    {
        if (_physicalDemand.TryGetValue(coordinate, out PhysicalDemandReason existing))
        {
            _physicalDemand[coordinate] = existing | reason;
            return;
        }
        if (_physicalDemand.Count >= PhysicalOwnerCapacity)
        {
            _physicalDemandOverflowCount++;
            if ((reason & (PhysicalDemandReason.Occupied | PhysicalDemandReason.Visible |
                           PhysicalDemandReason.TurnCoverage | PhysicalDemandReason.ImmediateNeighbor |
                           PhysicalDemandReason.SharedSite)) != 0)
                _physicalDemandVisibleOverflow = true;
            return;
        }
        _physicalDemand.Add(coordinate, reason);
    }

    private void RecordStreamingStage(string stage, float startedAt)
    {
        // note: Update the stage diagnostic only when it establishes a new high-water mark so normal frames avoid repeated logging or allocations.
        float elapsed = Time.realtimeSinceStartup - startedAt;
        if (elapsed > _lastStreamingWorstStageSeconds)
        {
            _lastStreamingWorstStageSeconds = elapsed;
            _lastStreamingWorstStage = stage;
        }
        if (elapsed <= _maximumStreamingStageSeconds)
            return;
        _maximumStreamingStageSeconds = elapsed;
        _maximumStreamingStage = stage;
    }

    private void RecordMaterializationSubstage(string stage, float elapsedSeconds)
    {
        // note: Identify the indivisible feature call that overruns the shared budget so it can be split without changing accepted geometry.
        if (elapsedSeconds <= _maximumMaterializationSubstageSeconds)
            return;
        _maximumMaterializationSubstageSeconds = elapsedSeconds;
        _maximumMaterializationSubstage = stage ?? string.Empty;
    }

    private void RefreshForPlayer(bool initial)
    {
        if (_player == null)
            return;

        Vector2 position = new Vector2(_player.position.x, _player.position.z);
        Vector2Int center = ChunkFor(position);
        if (!_frontierRefreshPending || _frontierRefreshCenter != center || _frontierRefreshInitial != initial)
        {
            _currentChunk = center;
            _frontierRefreshPending = true;
            _frontierRefreshInitial = initial;
            _frontierRefreshCenter = center;
            _frontierRefreshPosition = position;
            // note: Boundary refreshes admit the entire visual prewarm square so diagonal and reversal travel never replace coverage with a single predicted destination.
            _frontierRefreshLookahead = Mathf.Max(Mathf.Max(semanticLookaheadRadius, generationRadius), EffectiveVisualPrewarmRadius);
            _frontierRefreshX = -_frontierRefreshLookahead;
            _frontierRefreshZ = -_frontierRefreshLookahead;
            _contentDemand.Clear();
        }
        else
        {
            // note: Keep the latest position for deterministic prediction when admission spans multiple frames inside one chunk.
            _frontierRefreshPosition = position;
        }
        ProcessFrontierRefreshSlice();
    }

    private void ProcessFrontierRefreshSlice()
    {
        if (!_frontierRefreshPending)
            return;

        int processed = 0;
        // note: Content demand covers the full prewarm square; queue admission remains bounded by the existing scheduler cap.
        int contentRadius = EffectiveVisualPrewarmRadius;
        while (_frontierRefreshZ <= _frontierRefreshLookahead && processed++ < MaximumFrontierCellsPerFrame)
        {
            if (!CanAdvanceAggregateWork("frontierAdmission"))
                return;
            float frontierCellStartedAt = Time.realtimeSinceStartup;
            int x = _frontierRefreshX;
            int z = _frontierRefreshZ;
            Vector2Int coordinate = new Vector2Int(_frontierRefreshCenter.x + x, _frontierRefreshCenter.y + z);
            RuntimeChunk chunk = GetOrCreateChunk(coordinate);
            int distance = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            // note: Provision collision terrain through the full semantic lookahead so the visible world never ends before its planned continuation cells.
            // note: High-speed prediction supplements the complete nearby demand; it never suppresses terrain required by the visible/readiness square.
            if (distance <= EffectiveVisualPrewarmRadius)
                RequestTerrainForChunk(coordinate);
            if (distance <= contentRadius)
                _contentDemand.Add(coordinate);
            if (distance <= contentRadius &&
                !chunk.physicalRepresentation &&
                chunk.state != YQSemanticChunkLifecycle.Generating)
            {
                Enqueue(coordinate);
            }

            _frontierRefreshX++;
            if (_frontierRefreshX > _frontierRefreshLookahead)
            {
                _frontierRefreshX = -_frontierRefreshLookahead;
                _frontierRefreshZ++;
            }
            RecordAggregateWorkSlice(frontierCellStartedAt, "frontierAdmission");
        }

        if (_frontierRefreshZ <= _frontierRefreshLookahead)
            return;

        // note: Retain one predicted destination as a priority hint while the complete visual prewarm square remains demanded.
        Vector2Int predictedContentChunk = ResolvePredictedContentChunk(_frontierRefreshPosition);
        _contentDemand.Add(predictedContentChunk);
        RuntimeChunk predictedRuntimeChunk = GetOrCreateChunk(predictedContentChunk);
        if (!predictedRuntimeChunk.physicalRepresentation &&
            predictedRuntimeChunk.state != YQSemanticChunkLifecycle.Generating)
            Enqueue(predictedContentChunk);
        // note: Sort once after full frontier admission; per-item sorts were repeated synchronous work during every boundary transition.
        float queueMaintenanceStartedAt = Time.realtimeSinceStartup;
        SortContentQueue();
        SortTerrainQueue();
        TrimContentQueue();

        // note: Enforce the queue cap after the entire frontier pass, then publish the exact count even when no new request was added.
        TrimQueue();
        TrimTerrainQueue();
        RecordAggregateWorkSlice(queueMaintenanceStartedAt, "frontierQueueMaintenance");
        _queuedCount = _queue.Count;
        // note: Initial binding must publish authored visibility immediately; later boundary crossings let Update consume lifecycle work under its existing slice budget.
        if (_frontierRefreshInitial)
            ApplyLifecycle();
        else
            _lifecyclePending = true;
        // note: Defer history eviction to Update so a boundary crossing performs only frontier admission; eviction remains bounded to one slice.
        _semanticPrunePending = true;
        _queuedCount = _queue.Count;
        _frontierRefreshPending = false;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (!_frontierRefreshInitial)
            Debug.Log("[YQSemanticChunkStreamer] FRONTIER ADVANCED to " + _currentChunk);
#endif
    }

    private void ProcessQueue(int maximumStarts = MaximumContentGenerationWorkers)
    {
        // note: Fill the small required-content worker set so visible cells progress in parallel without allowing unbounded Unity allocations.
        if (_activeGenerations.Count >= MaximumContentGenerationWorkers)
        {
            // note: Free one later-deadline worker before a ready predicted cell reaches the live frustum; visible cells retain first claim.
            if (HasPendingHardViewContent() && HasReadyHardViewContentCandidate())
                PreemptLowerPriorityContentForHardView();
            else if (TryGetReadyPredictedContentCandidate(out Vector2Int predictedCandidate))
                PreemptLowerPriorityContentForDemand(predictedCandidate, true);
            else if (TryGetReadyHardPreparationContentCandidate(out Vector2Int preparationCandidate))
                PreemptLowerPriorityContentForDemand(preparationCandidate, false);
        }
        int starts = 0;
        int startLimit = Mathf.Clamp(maximumStarts, 0, MaximumContentGenerationWorkers);
        while (starts < startLimit &&
               _activeGenerations.Count < MaximumContentGenerationWorkers &&
               TryStartNextContentGeneration())
        {
            starts++;
        }
    }

    private bool TryStartNextContentGeneration()
    {
        // note: Measure one candidate scan separately from coroutine startup while leaving queue eligibility unchanged.
        using (ContentStartSearchProfilerMarker.Auto())
            return TryStartNextContentGenerationCore();
    }

    private bool TryStartNextContentGenerationCore()
    {
        if (_queue.Count == 0)
            return false;

        // note: V2-preferred continuation content waits for the accepted spatial authority; publishing scatter before it exists creates the misleading tree/rock-only world.
        if (YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan) && _acceptedBlueprint == null)
            return false;

        bool hardViewPending = HasPendingHardViewContent();
        // note: Reserve a content lane only when a queued camera-visible cell has a complete sampleable heightfield; collider synchronization may continue in parallel.
        bool readyHardViewContentCandidate = hardViewPending && HasReadyHardViewContentCandidate();
        int selectedIndex = -1;
        for (int index = 0; index < _queue.Count; index++)
        {
            Vector2Int candidate = _queue[index];
            if (!IsContentDemandedNow(candidate))
            {
                // note: A prediction can become stale before the next cell transition; release its queued content without touching independently managed terrain.
                ResetQueuedContent(candidate, index);
                index--;
                continue;
            }
            if (!_chunks.TryGetValue(candidate, out RuntimeChunk candidateChunk) || candidateChunk == null || candidateChunk.physicalRepresentation)
            {
                ResetQueuedContent(candidate, index);
                index--;
                continue;
            }
            if (readyHardViewContentCandidate &&
                !_guaranteedViewDemand.Contains(candidate) &&
                _activeGenerations.Count >= MaximumContentGenerationWorkers - 1)
            {
                // note: Preserve the final lane for a visible cell whenever background/site generation is already occupying the other bounded workers.
                continue;
            }
            int reservationBlockCount = _ownerReservationBlockCount;
            if (!TryReservePhysicalOwner(candidate))
            {
                bool blockedByPinnedOwnerCapacity = _ownerReservationBlockCount > reservationBlockCount &&
                    _lastOwnerReservationBlock == candidate;
                bool predictedViewDeadline = _semanticViewPredictionArrivalSeconds.ContainsKey(candidate);
                // note: A ready visible cell may have an open worker lane but no owner slot; reclaim one strictly later worker instead of leaving camera content queued behind the owner cap.
                if (blockedByPinnedOwnerCapacity &&
                    (_guaranteedViewDemand.Contains(candidate) || predictedViewDeadline || _hardViewDemand.Contains(candidate)))
                    PreemptLowerPriorityContentForDemand(candidate, predictedViewDeadline);
                // note: Full-contract stress keeps content publication bounded; distant semantic placeholders remain available until retention frees a physical slot.
                continue;
            }
            if (ResolveTerrainForContentMaterialization(candidate) == null)
            {
                // note: Keep accepted content queued until its exact heightfield exists, then let collision publication and semantic construction overlap.
                RequestTerrainForChunk(candidate);
                continue;
            }
            selectedIndex = index;
            break;
        }
        if (selectedIndex < 0)
        {
            _queuedCount = _queue.Count;
            return false;
        }

        Vector2Int coordinate = _queue[selectedIndex];
        _queue.RemoveAt(selectedIndex);
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null || chunk.physicalRepresentation)
        {
            _queuedCount = _queue.Count;
            return false;
        }

        // note: Start one bounded required-content owner; its nested iterator still yields through the shared per-worker content slice budget.
        chunk.state = YQSemanticChunkLifecycle.Generating;
        chunk.contentQueuedAt = -1f;
        chunk.contentStartedAt = Time.unscaledTime;
        chunk.record.lastVisitedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        chunk.contentOwnerEpoch++;
        chunk.hardViewProgressReserveSlices = 0;
        chunk.hardViewProgressReserveDeniedFrames = 0;
        chunk.hardViewProgressYieldedToLiveFrames = 0;
        chunk.lastHardViewProgressDiagnosticFrame = -1;
        // note: Snapshot this attempt's demand lead separately from collision readiness already present when the attempt starts.
        chunk.progressEpoch = chunk.contentOwnerEpoch;
        chunk.contentAttemptCount++;
        chunk.progressStartedAt = Time.realtimeSinceStartupAsDouble;
        chunk.hardDemandReadyAtStart = _hardViewDemand.Contains(coordinate);
        chunk.collisionReadyAtStart = IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate);
        // note: Preserve pre-attempt queue lead while translating the unscaled admission timestamp into the diagnostic realtime clock.
        chunk.epochHardDemandAt = chunk.hardDemandReadyAtStart
            ? chunk.progressStartedAt - (chunk.hardViewAdmittedAt >= 0f
                ? Math.Max(0d, Time.unscaledTime - chunk.hardViewAdmittedAt) : 0d)
            : -1d;
        chunk.epochCollisionAt = chunk.collisionReadyAtStart ? chunk.progressStartedAt : -1d;
        chunk.structuralProgress.Reset();
        chunk.ecologyProgress.Reset();
        StreamingWorkToken token = CaptureStreamingWorkToken(coordinate);
        _activeGenerationWorkIds[coordinate] = token.workId;
        _activeGenerations[coordinate] = null;
        Coroutine generation = StartCoroutine(GenerateChunkRoutine(coordinate, chunk, token));
        // note: A synchronous failure may already have released this slot before StartCoroutine returns.
        if (_activeGenerationWorkIds.TryGetValue(coordinate, out long activeWorkId) && activeWorkId == token.workId)
            _activeGenerations[coordinate] = generation;
        _queuedCount = _queue.Count;
        return true;
    }

    private void RefreshHardViewDemand(Vector2 playerPosition, bool rebuildPhysicalDemand = true)
    {
        _previousHardViewDemand.Clear();
        foreach (Vector2Int coordinate in _hardViewDemand)
            _previousHardViewDemand.Add(coordinate);
        _hardViewDemand.Clear();
        _canonicalPreparationDemand.Clear();
        _guaranteedViewDemand.Clear();
        _semanticViewPredictionArrivalSeconds.Clear();
        _provisionalGroundPrefetchDemand.Clear();
        _provisionalGroundTurnBufferDemand.Clear();
        _highSpeedTurnBufferCoverageDemand.Clear();
        _highSpeedTurnBufferCoverageReadyCount = 0;
        UpdateQueuePriorityView();
        if (!_queuePriorityViewValid || _queuePriorityCamera == null)
        {
            if (rebuildPhysicalDemand)
                RebuildPhysicalDemandSet();
            return;
        }

        _lastHardViewAdmissionCameraPosition = _queuePriorityCameraPosition;
        _lastHardViewAdmissionCameraRotation = _queuePriorityCamera.transform.rotation;
        _lastHardViewAdmissionFieldOfView = _queuePriorityCamera.fieldOfView;
        _lastHardViewAdmissionAspect = _queuePriorityCamera.aspect;
        _lastHardViewAdmissionNearClip = _queuePriorityCamera.nearClipPlane;
        _lastHardViewAdmissionFarClip = _queuePriorityCamera.farClipPlane;
        _lastHardViewAdmissionCamera = _queuePriorityCamera;
        _lastHardViewAdmissionPlayerChunk = ChunkFor(playerPosition);
        _lastHardViewAdmissionCameraChunk = ChunkFor(new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z));
        _lastHardViewAdmissionVelocity = ResolveTraversalVelocity();
        _hasHardViewAdmissionCamera = true;

        GeometryUtility.CalculateFrustumPlanes(_queuePriorityCamera, _queuePriorityFrustumPlanes);

        float size = Mathf.Max(32f, chunkWorldSize);
        // note: Center the scan on the live camera cell, not the last completed player frontier, so a boundary crossing cannot leave the new view outside admission coverage.
        Vector2 cameraPlanarPosition = new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z);
        Vector2Int cameraChunk = ChunkFor(cameraPlanarPosition);
        float guaranteedDistance = Mathf.Max(1f, guaranteedVisualDistanceMeters);
        // note: Bound the scan by the inclusive player/camera radius box, retaining tangent cells while skipping an unused blanket ring.
        int minimumViewChunkX = Mathf.CeilToInt((Mathf.Min(playerPosition.x, cameraPlanarPosition.x) - guaranteedDistance - WorldGridOrigin) / size) - 1;
        int maximumViewChunkX = Mathf.FloorToInt((Mathf.Max(playerPosition.x, cameraPlanarPosition.x) + guaranteedDistance - WorldGridOrigin) / size);
        int minimumViewChunkZ = Mathf.CeilToInt((Mathf.Min(playerPosition.y, cameraPlanarPosition.y) - guaranteedDistance - WorldGridOrigin) / size) - 1;
        int maximumViewChunkZ = Mathf.FloorToInt((Mathf.Max(playerPosition.y, cameraPlanarPosition.y) + guaranteedDistance - WorldGridOrigin) / size);
        _batchingHardViewAdmission = true;
        try
        {
        for (int z = minimumViewChunkZ; z <= maximumViewChunkZ; z++)
        {
            for (int x = minimumViewChunkX; x <= maximumViewChunkX; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                if (!IsChunkInGuaranteedView(coordinate, playerPosition, size))
                    continue;

                _guaranteedViewDemand.Add(coordinate);
                AdmitHardViewCoordinate(coordinate);
            }
        }

        // note: Admit a complete player-centered preparation ring, not only the current facing direction; a free camera turn or one-cell crossing can then reuse already published owners.
        Vector2Int preparationCenter = ChunkFor(playerPosition);
        int preparationRadius = GuaranteedViewPreparationRadius;
        for (int z = -preparationRadius; z <= preparationRadius; z++)
        {
            for (int x = -preparationRadius; x <= preparationRadius; x++)
            {
                Vector2Int coordinate = preparationCenter + new Vector2Int(x, z);
                if (IsChunkInPreparationEnvelope(coordinate, playerPosition, size))
                {
                    _canonicalPreparationDemand.Add(coordinate);
                    AdmitHardViewCoordinate(coordinate, queueProvisionalGround: false);
                }
            }
        }

        RefreshPredictiveSemanticViewDemand(cameraPlanarPosition, cameraChunk, size, force: true);
        RefreshProvisionalGroundPrefetchDemand(size);
        }
        finally
        {
            _batchingHardViewAdmission = false;
        }

        // note: Keep the bounded safety-floor queue capped here; the shared Update dispatch pass sorts and trims content/terrain only once before starting work.
        TrimProvisionalGroundQueueToCapacity();
        if (rebuildPhysicalDemand)
            RebuildPhysicalDemandSet();
        // note: Replacing the camera-visible demand must always wake lifecycle publication; otherwise a ready owner can remain inactive until an unrelated stream event occurs.
        _lifecyclePending = true;
    }

    private bool ShouldRefreshHardViewDemand(Vector2 playerPosition)
    {
        // note: The published 260 m/s all-direction runway makes sub-cell pose changes reuse the same envelope; refresh on boundary, projection, authority, or real velocity changes.
        UpdateQueuePriorityView();
        if (!_hasHardViewAdmissionCamera || !_queuePriorityViewValid || _queuePriorityCamera == null ||
            _lastHardViewAdmissionCamera != _queuePriorityCamera)
            return true;

        bool projectionChanged =
            Mathf.Abs(_queuePriorityCamera.fieldOfView - _lastHardViewAdmissionFieldOfView) > 0.01f ||
            Mathf.Abs(_queuePriorityCamera.aspect - _lastHardViewAdmissionAspect) > 0.001f ||
            Mathf.Abs(_queuePriorityCamera.nearClipPlane - _lastHardViewAdmissionNearClip) > 0.01f ||
            Mathf.Abs(_queuePriorityCamera.farClipPlane - _lastHardViewAdmissionFarClip) > 0.1f;
        if (projectionChanged)
            return true;

        Vector2 cameraPlanarPosition = new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z);
        if (ChunkFor(playerPosition) != _lastHardViewAdmissionPlayerChunk ||
            ChunkFor(cameraPlanarPosition) != _lastHardViewAdmissionCameraChunk)
            return true;

        Vector2 velocity = ResolveTraversalVelocity();
        float speed = velocity.magnitude;
        float previousSpeed = _lastHardViewAdmissionVelocity.magnitude;
        bool activeMovementIntent = _playerMotor != null && _playerMotor.isActiveAndEnabled &&
            _playerMotor.IsAuthoritative && _playerMotor.MoveInput.sqrMagnitude > 0.0001f;
        if (activeMovementIntent)
            return false;

        bool hasMovementHeading = speed >= PredictiveSemanticViewMinimumSpeedMetersPerSecond;
        bool previouslyHadMovementHeading = previousSpeed >= PredictiveSemanticViewMinimumSpeedMetersPerSecond;
        if (hasMovementHeading != previouslyHadMovementHeading)
            return true;
        if (Mathf.Abs(speed - previousSpeed) >= HardViewAdmissionSpeedChangeMetersPerSecond)
            return true;
        if (hasMovementHeading && previouslyHadMovementHeading &&
            Vector2.Dot(velocity / speed, _lastHardViewAdmissionVelocity / previousSpeed) < HardViewAdmissionDirectionDotThreshold)
            return true;

        return false;
    }

    private void RefreshPredictiveSemanticViewDemand(
        Vector2 cameraPlanarPosition,
        Vector2Int cameraChunk,
        float size,
        bool force = false)
    {
        // note: Ground has a longer emergency prefetch, but required features, paint, and ecology need their own lead before a speed spike reaches the visible frustum.
        Vector2 velocity = ResolveTraversalDemandVelocity();
        float speed = velocity.magnitude;
        if (speed < PredictiveSemanticViewMinimumSpeedMetersPerSecond || _guaranteedViewDemand.Count == 0)
        {
            _semanticViewPredictionArrivalSeconds.Clear();
            _hasSemanticViewPrediction = false;
            _lastSemanticViewPredictionAdmissionBudget = 0;
            _lastSemanticViewPredictionAdmitted = 0;
            _lastSemanticViewPredictionProjectedHard = 0;
            _lastSemanticViewPredictionOwnerBudgetSkipped = 0;
            _lastSemanticViewPredictionRefreshAt = Time.unscaledTime;
            return;
        }

        Vector2 heading = velocity / speed;
        bool predictionLaneChanged = !_hasSemanticViewPrediction ||
            cameraChunk != _lastSemanticViewPredictionCameraChunk ||
            Mathf.Abs(speed - _lastSemanticViewPredictionVelocity.magnitude) >= HardViewAdmissionSpeedChangeMetersPerSecond ||
            Vector2.Dot(heading, _lastSemanticViewPredictionVelocity.normalized) < HardViewAdmissionDirectionDotThreshold;
        if (!force && !predictionLaneChanged && Time.unscaledTime < _nextSemanticViewPredictionRefreshAt)
            return;

        _semanticViewPredictionArrivalSeconds.Clear();
        _semanticViewPredictionVisited.Clear();
        _lastSemanticViewPredictionAdmitted = 0;
        _lastSemanticViewPredictionProjectedHard = 0;
        _lastSemanticViewPredictionOwnerBudgetSkipped = 0;
        _lastSemanticViewPredictionRefreshAt = Time.unscaledTime;
        _lastSemanticViewPredictionVelocity = velocity;
        _lastSemanticViewPredictionCameraChunk = cameraChunk;
        // note: Scale refresh cadence to the configured visible frontier; a radius-512m view at 260m/s exposes about 16 new cells per second, so the old fixed four-per-second cadence fell behind during coasting.
        float visibleFrontierCellsPerSecond = speed * (2f * Mathf.Max(1f, guaranteedVisualDistanceMeters)) / (size * size);
        float desiredOwnerAdmissionRate = Mathf.Max(4f, visibleFrontierCellsPerSecond * 1.5f);
        float refreshInterval = Mathf.Clamp(
            1f / desiredOwnerAdmissionRate,
            MinimumPredictiveSemanticViewRefreshIntervalSeconds,
            PredictiveSemanticViewRefreshIntervalSeconds);
        _nextSemanticViewPredictionRefreshAt = Time.unscaledTime + refreshInterval;
        _hasSemanticViewPrediction = true;

        // note: Preserve room for immediate boundary neighbors and active site leases when extending the critical visible-demand union.
        int admissionBudget = Mathf.Max(0,
            PhysicalOwnerCapacity - _hardViewDemand.Count - 8 -
            _siteTerrainDependencies.Count - _siteTerrainHandoffRequests.Count);
        _lastSemanticViewPredictionAdmissionBudget = admissionBudget;
        if (admissionBudget == 0)
            return;

        _semanticViewPredictionScratch.Clear();
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
            _semanticViewPredictionScratch.Add(coordinate);
        _semanticViewPredictionScratch.Sort(CompareCoordinates);
        // note: Prepare the max-speed semantic runway along the measured direction so a speed increase inherits finished appearance and ecology.
        Vector2 predictionVelocity = velocity.normalized *
            Mathf.Max(speed, MaximumGroundPrefetchSpeedMetersPerSecond);
        // note: Sample more often than one cell crossing so a near-boundary speed boost cannot skip a semantic owner between prediction points.
        float predictionStepSeconds = Mathf.Min(
            PredictiveSemanticViewStepSeconds,
            size * 0.75f / Mathf.Max(1f, predictionVelocity.magnitude));

        float admissionStartedAt = Time.realtimeSinceStartup;
        int admitted = 0;
        int createdOwners = 0;
        Vector2Int previousOffset = new Vector2Int(int.MinValue, int.MinValue);
        for (float predictionSeconds = predictionStepSeconds;
             predictionSeconds <= PredictiveSemanticViewHorizonSeconds;
             predictionSeconds += predictionStepSeconds)
        {
            Vector2Int futureCameraChunk = ChunkFor(cameraPlanarPosition + predictionVelocity * predictionSeconds);
            Vector2Int offset = futureCameraChunk - cameraChunk;
            if (offset == Vector2Int.zero || offset == previousOffset)
                continue;
            previousOffset = offset;

            // note: Project each currently visible chunk by the same future camera displacement so turns preserve the frustum's actual width and shape.
            for (int index = 0; index < _semanticViewPredictionScratch.Count; index++)
            {
                Vector2Int predictedCoordinate = _semanticViewPredictionScratch[index] + offset;
                if (!_semanticViewPredictionVisited.Add(predictedCoordinate))
                    continue;
                bool alreadyHardView = _hardViewDemand.Contains(predictedCoordinate);
                if (alreadyHardView)
                {
                    _lastSemanticViewPredictionProjectedHard++;
                    if (!_semanticViewPredictionArrivalSeconds.TryGetValue(predictedCoordinate, out float previousArrival) ||
                        predictionSeconds < previousArrival)
                        _semanticViewPredictionArrivalSeconds[predictedCoordinate] = predictionSeconds;
                    continue;
                }
                if (admitted >= admissionBudget)
                    continue;
                bool ownerExists = _chunks.ContainsKey(predictedCoordinate);
                if (!ownerExists &&
                    (createdOwners >= MaximumPredictiveSemanticOwnerCreationsPerPass ||
                     (createdOwners > 0 && Time.realtimeSinceStartup - admissionStartedAt >= PredictiveOwnerAdmissionBudgetSeconds)))
                {
                    _lastSemanticViewPredictionOwnerBudgetSkipped++;
                    continue;
                }

                AdmitHardViewCoordinate(predictedCoordinate);
                _semanticViewPredictionArrivalSeconds[predictedCoordinate] = predictionSeconds;
                admitted++;
                _lastSemanticViewPredictionAdmitted++;
                if (!ownerExists)
                    createdOwners++;
            }
            if (admitted >= admissionBudget)
                break;
        }
    }

    private void RefreshProvisionalGroundPrefetchDemand(float size)
    {
        // note: Rebuild the geometric witness with the same camera/player envelope even when canonical demand already owns its cells.
        _highSpeedTurnBufferCoverageDemand.Clear();
        _highSpeedTurnBufferCoverageReadyCount = 0;
        if (_guaranteedViewDemand.Count == 0 || _queuePriorityCamera == null)
            return;

        Vector2 playerPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z);
        Vector2 cameraPosition = new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z);
        Vector2 traversalVelocity = ResolveTraversalVelocity();
        Vector2 demandVelocity = ResolveTraversalDemandVelocity();
        float traversalSpeed = demandVelocity.magnitude;
        Vector2 turnAheadDirection = traversalSpeed > 0.01f
            ? demandVelocity / traversalSpeed
            : Vector2.zero;
        if (turnAheadDirection.sqrMagnitude <= 0.0001f)
        {
            Vector3 cameraForward = _queuePriorityCamera.transform.forward;
            Vector2 cameraDirection = new Vector2(cameraForward.x, cameraForward.z);
            if (cameraDirection.sqrMagnitude > 0.0001f)
                turnAheadDirection = cameraDirection.normalized;
        }
        // note: Keep the all-direction safety ring to its one-second contract; the independent centerline prefetch retains its longer three-second lead.
        float turnAheadMeters = Mathf.Min(size * MaximumSemanticTurnAheadCells, traversalSpeed * ProvisionalGroundTurnBufferSeconds);
        Vector2 predictedPlayerPosition = playerPosition + turnAheadDirection * turnAheadMeters;
        Vector2 predictedCameraPosition = cameraPosition + turnAheadDirection * turnAheadMeters;
        // note: The camera owns the strict visible radius; the separate emergency-floor ring covers one full second of movement in every direction.
        float allDirectionRadius = Mathf.Max(
            1f,
            MaximumGroundPrefetchSpeedMetersPerSecond * ProvisionalGroundTurnBufferSeconds);
        float scanMargin = allDirectionRadius + size;
        int minimumX = Mathf.FloorToInt((Mathf.Min(Mathf.Min(playerPosition.x, cameraPosition.x), Mathf.Min(predictedPlayerPosition.x, predictedCameraPosition.x)) - scanMargin - WorldGridOrigin) / size);
        int maximumX = Mathf.FloorToInt((Mathf.Max(Mathf.Max(playerPosition.x, cameraPosition.x), Mathf.Max(predictedPlayerPosition.x, predictedCameraPosition.x)) + scanMargin - WorldGridOrigin) / size);
        int minimumZ = Mathf.FloorToInt((Mathf.Min(Mathf.Min(playerPosition.y, cameraPosition.y), Mathf.Min(predictedPlayerPosition.y, predictedCameraPosition.y)) - scanMargin - WorldGridOrigin) / size);
        int maximumZ = Mathf.FloorToInt((Mathf.Max(Mathf.Max(playerPosition.y, cameraPosition.y), Mathf.Max(predictedPlayerPosition.y, predictedCameraPosition.y)) + scanMargin - WorldGridOrigin) / size);
        float radiusSquared = allDirectionRadius * allDirectionRadius;
        for (int z = minimumZ; z <= maximumZ; z++)
        {
            for (int x = minimumX; x <= maximumX; x++)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                float minimumCellX = WorldGridOrigin + x * size;
                float minimumCellZ = WorldGridOrigin + z * size;
                Vector2 playerClosest = new Vector2(
                    Mathf.Clamp(playerPosition.x, minimumCellX, minimumCellX + size),
                    Mathf.Clamp(playerPosition.y, minimumCellZ, minimumCellZ + size));
                Vector2 cameraClosest = new Vector2(
                    Mathf.Clamp(cameraPosition.x, minimumCellX, minimumCellX + size),
                    Mathf.Clamp(cameraPosition.y, minimumCellZ, minimumCellZ + size));
                Vector2 predictedPlayerClosest = new Vector2(
                    Mathf.Clamp(predictedPlayerPosition.x, minimumCellX, minimumCellX + size),
                    Mathf.Clamp(predictedPlayerPosition.y, minimumCellZ, minimumCellZ + size));
                Vector2 predictedCameraClosest = new Vector2(
                    Mathf.Clamp(predictedCameraPosition.x, minimumCellX, minimumCellX + size),
                    Mathf.Clamp(predictedCameraPosition.y, minimumCellZ, minimumCellZ + size));
                if ((playerClosest - playerPosition).sqrMagnitude > radiusSquared &&
                    (cameraClosest - cameraPosition).sqrMagnitude > radiusSquared &&
                    (predictedPlayerClosest - predictedPlayerPosition).sqrMagnitude > radiusSquared &&
                    (predictedCameraClosest - predictedCameraPosition).sqrMagnitude > radiusSquared)
                    continue;

                _highSpeedTurnBufferCoverageDemand.Add(coordinate);
                // note: A hidden preparation-ring cell may still be waiting for canonical collision publication; keep its one-second safety floor until it enters the live view.
                if (_guaranteedViewDemand.Contains(coordinate))
                    continue;

                // note: Keep a same-authority safety height sample around the one-second turn envelope; full accepted content is admitted when a cell enters the camera preparation/view envelope.
                _provisionalGroundTurnBufferDemand.Add(coordinate);
                AdmitTurnBufferGroundCoordinate(coordinate);
            }
        }

        // note: Extend the occupied-cell collision runway along measured travel; the all-direction content ring already protects every camera-visible lane.
        Vector2 direction = demandVelocity;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            Vector3 cameraForward = _queuePriorityCamera.transform.forward;
            direction = new Vector2(cameraForward.x, cameraForward.z);
        }
        if (direction.sqrMagnitude <= 0.0001f)
            return;
        direction.Normalize();

        int leadSteps = Mathf.CeilToInt(
            MaximumGroundPrefetchSpeedMetersPerSecond * ProvisionalGroundLeadSeconds / size);
        for (int step = 1; step <= leadSteps; step++)
        {
            Vector2 offset = direction * (step * size);
        Vector2Int cellOffset = new Vector2Int(
                Mathf.RoundToInt(offset.x / size),
                Mathf.RoundToInt(offset.y / size));
            if (cellOffset == Vector2Int.zero)
                continue;

            Vector2Int coordinate = ChunkFor(playerPosition) + cellOffset;
            if (_guaranteedViewDemand.Contains(coordinate) || _hardViewDemand.Contains(coordinate) ||
                !_provisionalGroundPrefetchDemand.Add(coordinate))
                continue;
            // note: Full-content visibility is admitted in every direction; only the extra three-second collision floor follows the occupied centerline.
            QueueProvisionalGround(coordinate);
        }
    }

    private void AdmitHardViewCoordinate(Vector2Int coordinate, bool queueProvisionalGround = true)
    {
        bool firstAdmissionThisPass = _hardViewDemand.Add(coordinate);
        RuntimeChunk chunk = GetOrCreateChunk(coordinate);
        if (chunk != null && chunk.progressStartedAt >= 0d && chunk.progressEpoch == chunk.contentOwnerEpoch && chunk.epochHardDemandAt < 0d)
            chunk.epochHardDemandAt = Time.realtimeSinceStartupAsDouble;
        if (chunk != null && firstAdmissionThisPass && !_previousHardViewDemand.Contains(coordinate))
            chunk.hardViewAdmittedAt = Time.unscaledTime;
        _contentDemand.Add(coordinate);
        if (firstAdmissionThisPass)
        {
            bool retainedWorkAlreadyOwned = _previousHardViewDemand.Contains(coordinate) &&
                chunk != null &&
                (chunk.physicalRepresentation || chunk.state == YQSemanticChunkLifecycle.Generating ||
                    _queue.Contains(coordinate) || _activeGenerations.ContainsKey(coordinate)) &&
                (IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate) ||
                    coordinate == _terrainPreparingCoordinate || _terrainQueued.Contains(coordinate) ||
                    _pendingTerrainPublications.ContainsKey(coordinate));
            if (!retainedWorkAlreadyOwned)
                RequestTerrainForChunk(coordinate);
        }
        // note: Full accepted terrain/content lookahead uses its canonical queues; emergency floor sampling remains reserved for collision-only prefetch.
        if (queueProvisionalGround)
            QueueProvisionalGround(coordinate);
        if (firstAdmissionThisPass && chunk != null && !chunk.physicalRepresentation &&
            chunk.state != YQSemanticChunkLifecycle.Generating)
            Enqueue(coordinate);
    }

    private void AdmitTurnBufferGroundCoordinate(Vector2Int coordinate)
    {
        // note: Keep only the same-authority safety floor in the off-screen turnaround envelope; canonical publication is admitted when camera visibility is predicted.
        RuntimeChunk chunk = GetOrCreateChunk(coordinate);
        if (chunk != null)
            QueueProvisionalGround(coordinate);
    }

    private void MaintainHighSpeedTurnBufferGroundDemand()
    {
        if (_provisionalGroundTurnBufferDemand.Count == 0)
            return;

        // note: Retry only deferred emergency floors as sampler slots open; hidden turnaround cells do not occupy canonical terrain/content queues.
        foreach (Vector2Int coordinate in _provisionalGroundTurnBufferDemand)
        {
            if (IsChunkInsideAuthoredTerrain(coordinate) ||
                HasPublishedTerrain(coordinate) ||
                _provisionalGroundTiles.ContainsKey(coordinate) ||
                _provisionalGroundSamplers.ContainsKey(coordinate) ||
                _provisionalGroundQueued.Contains(coordinate))
                continue;
            if (_provisionalGroundQueue.Count >= PhysicalOwnerCapacity)
                break;
            QueueProvisionalGround(coordinate);
        }
    }

    private bool IsChunkInPreparationEnvelope(Vector2Int coordinate, Vector2 playerPosition, float size)
    {
        float minimumX = WorldGridOrigin + coordinate.x * size;
        float maximumX = minimumX + size;
        float minimumZ = WorldGridOrigin + coordinate.y * size;
        float maximumZ = minimumZ + size;
        Vector2 playerClosest = new Vector2(
            Mathf.Clamp(playerPosition.x, minimumX, maximumX),
            Mathf.Clamp(playerPosition.y, minimumZ, maximumZ));
        Vector2 cameraPosition = _queuePriorityViewValid
            ? new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z)
            : playerPosition;
        Vector2 cameraClosest = new Vector2(
            Mathf.Clamp(cameraPosition.x, minimumX, maximumX),
            Mathf.Clamp(cameraPosition.y, minimumZ, maximumZ));
        float preparationDistance = Mathf.Max(1f, guaranteedVisualDistanceMeters) +
            size * MaximumSemanticTurnAheadCells;
        // note: Use nearest cell points so diagonal corner cells outside the expanded view distance do not consume the publication queue.
        float preparationDistanceSquared = preparationDistance * preparationDistance;
        return Mathf.Min(
            (playerPosition - playerClosest).sqrMagnitude,
            (cameraPosition - cameraClosest).sqrMagnitude) <= preparationDistanceSquared;
    }

    private bool IsChunkInGuaranteedView(Vector2Int coordinate, Vector2 playerPosition, float size)
    {
        Vector3 cameraPosition = _queuePriorityCameraPosition;
        Vector2 closestPlanar = new Vector2(
            Mathf.Clamp(cameraPosition.x, WorldGridOrigin + coordinate.x * size, WorldGridOrigin + (coordinate.x + 1) * size),
            Mathf.Clamp(cameraPosition.z, WorldGridOrigin + coordinate.y * size, WorldGridOrigin + (coordinate.y + 1) * size));
        float guaranteedDistance = Mathf.Max(1f, guaranteedVisualDistanceMeters);
        float guaranteedDistanceSquared = guaranteedDistance * guaranteedDistance;
        float playerDistanceSquared = (playerPosition - closestPlanar).sqrMagnitude;
        float cameraDistanceSquared =
            (new Vector2(cameraPosition.x, cameraPosition.z) - closestPlanar).sqrMagnitude;
        if (Mathf.Min(playerDistanceSquared, cameraDistanceSquared) > guaranteedDistanceSquared)
            return false;

        float minimumX = WorldGridOrigin + coordinate.x * size;
        float maximumX = minimumX + size;
        float minimumZ = WorldGridOrigin + coordinate.y * size;
        float maximumZ = minimumZ + size;
        float groundY = _player != null ? _player.position.y : 0f;
        // note: Test the whole chunk volume, not only its center or corners, so a thin oblique frustum intersection cannot remain an unloaded hole in the view.
        float terrainBaseY = _terrain != null ? _terrain.transform.position.y : 0f;
        float verticalMinimum = Mathf.Min(terrainBaseY - 16f, groundY - 128f);
        float verticalMaximum = Mathf.Max(
            terrainBaseY + YQGeneratedWorldTerrain.TerrainHeight + 128f,
            groundY + 128f);
        float verticalExtent = Mathf.Max(1f, verticalMaximum - verticalMinimum);
        Bounds chunkBounds = new Bounds(
            new Vector3(
                minimumX + size * 0.5f,
                verticalMinimum + verticalExtent * 0.5f,
                minimumZ + size * 0.5f),
            new Vector3(size, verticalExtent, size));
        return GeometryUtility.TestPlanesAABB(_queuePriorityFrustumPlanes, chunkBounds);
    }

    private void ResetQueuedContent(Vector2Int coordinate, int index)
    {
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) && chunk != null &&
            chunk.state == YQSemanticChunkLifecycle.Queued)
        {
            chunk.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
            chunk.contentQueuedAt = -1f;
        }
        _queue.RemoveAt(index);
        _contentDemand.Remove(coordinate);
    }

    private bool IsContentDemandedNow(Vector2Int coordinate)
    {
        if (_publicationVerificationDemand.Contains(coordinate))
            return true;
        // note: The complete camera and 260 m/s turn envelope remains authoritative even when ordinary travel demand fills the bounded dispatch map.
        if (_hardViewDemand.Contains(coordinate))
            return true;
        // note: Once the bounded union is built, it is the scheduler's authoritative physical admission set.
        if (_physicalDemand.Count > 0)
            return _physicalDemand.ContainsKey(coordinate);
        int distance = Mathf.Max(
            Mathf.Abs(coordinate.x - _currentChunk.x),
            Mathf.Abs(coordinate.y - _currentChunk.y));
        if (distance <= EffectiveVisualPrewarmRadius)
            return _contentDemand.Contains(coordinate) || distance <= Mathf.Max(0, activeRadius);
        if (_player == null)
            return _contentDemand.Contains(coordinate);
        // note: Every admitted forward-corridor cell retains content demand through the bounded physical retention radius; terrain-only corridor owners are not acceptable publication substitutes.
        if (_contentDemand.Contains(coordinate) && distance <= TerrainRequestRetentionRadius)
            return true;
        return coordinate == ResolvePredictedContentChunk(
            new Vector2(_player.position.x, _player.position.z));
    }

    private void ProcessTerrainQueue()
    {
        // note: Painting is physical work too; bound concurrent painters before allocating another TerrainData.
        if (_terrainPreparation != null)
        {
            // note: Sort before inspecting the active slot so a newly admitted site dependency is visible to the handoff decision even when it was appended behind speculative terrain work.
            SortTerrainQueue();
            // note: Dispatch and the sampler share one strict-priority handoff rule; equal-priority visible work is allowed to finish instead of thrashing the only preparation slot.
            if (ShouldPreemptTerrainPreparation(_terrainPreparingCoordinate))
                CancelTerrainPreparationForCoordinate(_terrainPreparingCoordinate);
            RecordTerrainDispatchObservation();
            if (_terrainPreparation != null)
            {
                RecordVisibleTerrainDispatchWait(visibleWaitReason: 1);
                RecordFixedTerrainObservation("refused:activeSlot", FixedTerrainTraceTarget);
            }
            MaintainTerrainHeightPrefetch();
            return;
        }
        // note: Keep unpublished terrain residency bounded while the prior colliders cross both physics frame boundaries.
        if (_pendingTerrainPublications.Count >= MaximumPendingTerrainColliderPublications)
        {
            RecordTerrainDispatchObservation();
            RecordVisibleTerrainDispatchWait(visibleWaitReason: 2);
            RecordFixedTerrainObservation("refused:colliderCapacity", FixedTerrainTraceTarget);
            return;
        }
        if (_terrainQueue.Count == 0)
        {
            RecordTerrainDispatchObservation();
            return;
        }
        // note: A camera-visible or site-required collision tile may start while two distant appearance painters finish; required physical dependencies must never wait behind optional painting.
        if (_terrainPainting.Count >= 2 && !HasQueuedPriorityTerrain())
        {
            RecordTerrainDispatchObservation();
            RecordVisibleTerrainDispatchWait(visibleWaitReason: 3);
            RecordFixedTerrainObservation("refused:painterGate", FixedTerrainTraceTarget);
            return;
        }

        // note: Re-evaluate urgency every frame so a turn or acceleration promotes the new deadline before the next tile starts.
        SortTerrainQueue();
        int inspected = 0;
        while (_terrainQueue.Count > 0 && inspected++ < MaximumTerrainRequestsInspectedPerFrame)
        {
            // note: Only an armed history observes the first eight sorted candidates before dequeuing; no admission, ordering or scheduler work is added.
            if (_completeCellHistory != null)
                for (int observed = 0; observed < Mathf.Min(8, _terrainQueue.Count); observed++)
                    ObserveCompleteCell(_terrainQueue[observed], "dispatchOrder", true);
            Vector2Int coordinate = _terrainQueue[0];
            _terrainQueue.RemoveAt(0);
            _terrainQueued.Remove(coordinate);
            bool requiredSiteTerrain = _siteTerrainDependencies.ContainsKey(coordinate);
            bool hardViewPreparation = _hardViewDemand.Contains(coordinate);
            if (_physicalDemand.Count > 0 && !_physicalDemand.ContainsKey(coordinate) &&
                !_publicationVerificationDemand.Contains(coordinate) &&
                !hardViewPreparation &&
                TerrainPreparationPriority(coordinate, ResolveTraversalVelocity()) > 3)
            {
                if (coordinate == _lastVisualCoverageFailureCoordinate)
                    RecordVisibleTerrainDispatchWait(visibleWaitReason: 4);
                RecordFixedTerrainObservation("refused:dispatchDemandMap", coordinate);
                // note: Keep over-cap site dependencies queued, but never strand a hard-view cell solely because the bounded dispatch map omitted it.
                if (requiredSiteTerrain || _guaranteedViewDemand.Contains(coordinate))
                {
                    _terrainQueue.Add(coordinate);
                    _terrainQueued.Add(coordinate);
                    continue;
                }
                _terrainRequestedAt.Remove(coordinate);
                _terrainCriticalRequestedAt.Remove(coordinate);
                continue;
            }
            // note: Drop an obsolete queued cell before starting TerrainData work; only the current retained frontier may consume a preparation slot.
            if (!IsTerrainRequestStillRelevant(coordinate))
            {
                RecordFixedTerrainObservation("refused:obsoleteRequest", coordinate);
                if (coordinate == _lastVisualCoverageFailureCoordinate)
                    RecordVisibleTerrainDispatchWait(visibleWaitReason: 5);
                if (requiredSiteTerrain)
                    Debug.LogError("[YQSemanticChunkStreamer] REQUIRED SITE TERRAIN DISPATCH LOST " + coordinate + " chunkPresent=" + _chunks.ContainsKey(coordinate));
                continue;
            }
            // note: Keep the request queued until retained resources can release a slot; terrain and content share one owner budget.
            int reservationBlockCount = _ownerReservationBlockCount;
            if (!TryReservePhysicalOwner(coordinate))
            {
                RecordFixedTerrainObservation("refused:physicalOwnerReservation", coordinate);
                bool blockedByPinnedOwnerCapacity = _ownerReservationBlockCount > reservationBlockCount &&
                    _lastOwnerReservationBlock == coordinate;
                bool predictedViewDeadline = _semanticViewPredictionArrivalSeconds.ContainsKey(coordinate);
                // note: A visible terrain owner can be blocked by partial content reservations; reclaim one strictly later content deadline before rechecking the shared owner cap.
                if (blockedByPinnedOwnerCapacity &&
                    (_guaranteedViewDemand.Contains(coordinate) || predictedViewDeadline || hardViewPreparation))
                    PreemptLowerPriorityContentForDemand(coordinate, predictedViewDeadline);
                if (coordinate == _lastVisualCoverageFailureCoordinate)
                    RecordVisibleTerrainDispatchWait(visibleWaitReason: 6);
                if (requiredSiteTerrain)
                    Debug.LogWarning("[YQSemanticChunkStreamer] REQUIRED SITE TERRAIN RESERVATION WAIT " + coordinate + " capacity=" + PhysicalOwnerCapacity + " chunks=" + _chunks.Count + " physical=" + _physicalCount);
                _terrainQueue.Insert(0, coordinate);
                _terrainQueued.Add(coordinate);
                RecordTerrainDispatchObservation();
                return;
            }
            _siteTerrainHandoffRequests.Remove(coordinate);
            _terrainPreparingCoordinate = coordinate;
            _terrainPreparationStartedAt = Time.realtimeSinceStartup;
            _terrainPreparationDispatchFrame = Time.frameCount;
            _terrainPreparationPhase = "dispatch";
            _terrainPreparationPreempted = false;
            StreamingWorkToken token = CaptureStreamingWorkToken(coordinate, true);
            _terrainPreparationWorkId = token.workId;
            RecordFixedTerrainObservation("dispatched", coordinate, token.workId);
            Coroutine preparation = StartCoroutine(PrepareTerrainRoutine(coordinate, token));
            if (_terrainPreparationWorkId == token.workId)
                _terrainPreparation = preparation;
            if (coordinate == _lastVisualCoverageFailureCoordinate)
                RecordVisibleTerrainDispatchWait(visibleWaitReason: 8);
            else
                RecordVisibleTerrainDispatchWait(visibleWaitReason: 7);
            RecordTerrainDispatchObservation();
            MaintainTerrainHeightPrefetch();
            return;
        }
        RecordTerrainDispatchObservation();
    }

    private bool HasQueuedPriorityTerrain()
    {
        Vector2 velocity = ResolveTraversalVelocity();
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            Vector2Int coordinate = _terrainQueue[index];
            // note: Hard-view turn-buffer cells use priority three so the safety runway can use one reserved height-upload slice after the shared budget is spent.
            if (TerrainPreparationPriority(coordinate, velocity) <= 4 &&
                !IsChunkInsideAuthoredTerrain(coordinate) &&
                !HasPublishedTerrain(coordinate))
                return true;
        }

        return false;
    }

    private bool HasQueuedUrgentTerrain()
    {
        Vector2 velocity = ResolveTraversalVelocity();
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            Vector2Int coordinate = _terrainQueue[index];
            // note: Reserve budget bypass for the live camera and next collision boundary; speculative view runway remains scheduled work.
            if (TerrainPreparationPriority(coordinate, velocity) <= 2 &&
                !IsChunkInsideAuthoredTerrain(coordinate) &&
                !HasPublishedTerrain(coordinate))
                return true;
        }

        return false;
    }

    public void RequestCameraViewAdmission()
    {
        if (!_configured || _player == null)
            return;

        // note: The authoritative motor calls this while a candidate look is temporarily applied, so the requested frustum is queued before the pose is restored.
        Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
        bool admissionChanged = ShouldRefreshHardViewDemand(playerPosition);
        bool newlyVisibleDemand = HasUndemandedCurrentViewCell(playerPosition);
        bool refreshAdmission = admissionChanged || newlyVisibleDemand || _guaranteedViewDemand.Count == 0;
        if (refreshAdmission)
            RefreshHardViewDemand(playerPosition);
        // note: Camera pose validation already attempted immediate ground once this frame; admission only repeats it for a genuinely new envelope.
        bool visualReady = TryValidateCurrentVisualCoverage(out _);
        bool groundReady = refreshAdmission
            ? TryEnsureCurrentCameraGround(out _)
            : TryValidateGroundViewCoverage(out _);
        _cameraViewAdmissionPending = !visualReady || !groundReady;
        // note: The final-pose LateUpdate pass applies the changed envelope before rendering, so the motor does not repeat a full lifecycle census here.
    }

    private bool HasUndemandedCurrentViewCell(Vector2 playerPosition)
    {
        // note: Reopen a camera turn while an earlier view is still loading only when its live frustum exposes a cell that has not yet been admitted.
        UpdateQueuePriorityView();
        if (!_queuePriorityViewValid || _queuePriorityCamera == null)
            return false;
        GeometryUtility.CalculateFrustumPlanes(_queuePriorityCamera, _queuePriorityFrustumPlanes);
        Vector2Int cameraChunk = ChunkFor(new Vector2(_queuePriorityCameraPosition.x, _queuePriorityCameraPosition.z));
        int radius = GuaranteedViewChunkRadius + 1;
        float size = Mathf.Max(32f, chunkWorldSize);
        for (int z = -radius; z <= radius; z++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                Vector2Int coordinate = cameraChunk + new Vector2Int(x, z);
                if (!_guaranteedViewDemand.Contains(coordinate) &&
                    IsChunkInGuaranteedView(coordinate, playerPosition, size))
                    return true;
            }
        }
        return false;
    }

    private bool AreHardViewCellsLoaded()
    {
        if (_guaranteedViewDemand.Count == 0 || _physicalDemandVisibleOverflow)
            return false;
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) ||
                !IsFullyLoadedForView(coordinate, chunk))
                return false;
        }
        return true;
    }

    private bool HasPendingHardViewContent()
    {
        // note: Only the live frustum reserves a content lane; the larger preparation ring remains useful but cannot delay strict in-view publication.
        if (_guaranteedViewDemand.Count == 0)
            return false;
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) ||
                chunk == null ||
                !IsFullyLoadedForView(coordinate, chunk))
                return true;
        }
        return false;
    }

    private bool HasReadyHardViewContentCandidate()
    {
        // note: Only preempt a background owner when at least one queued visible cell has its collision terrain ready and can consume the freed lane now.
        return TryGetReadyHardViewContentCandidate(out _);
    }

    private bool TryGetReadyHardViewContentCandidate(out Vector2Int selectedCoordinate)
    {
        #if UNITY_EDITOR
        if (R2ObserveContentCandidateScans) R2ContentCandidateScans++;
        #endif
        // note: Measure the repeated visible-candidate scan on the main thread without changing its selection rule.
        using (ReadyHardViewScanProfilerMarker.Auto())
            return TryGetReadyHardViewContentCandidateCore(out selectedCoordinate);
    }

    private bool TryGetReadyHardViewContentCandidateCore(out Vector2Int selectedCoordinate)
    {
        selectedCoordinate = default;
        bool foundCandidate = false;
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        for (int index = 0; index < _queue.Count; index++)
        {
            Vector2Int candidate = _queue[index];
            if (!_guaranteedViewDemand.Contains(candidate) ||
                !_chunks.TryGetValue(candidate, out RuntimeChunk chunk) ||
                chunk == null || chunk.physicalRepresentation ||
                chunk.state == YQSemanticChunkLifecycle.Generating ||
                ResolveTerrainForContentMaterialization(candidate) == null)
                continue;

            if (!foundCandidate || CompareContentQueuePriority(candidate, selectedCoordinate, traversalVelocity) < 0)
            {
                selectedCoordinate = candidate;
                foundCandidate = true;
            }
        }
        return foundCandidate;
    }

    private bool PreemptLowerPriorityContentForHardView()
    {
        // note: An earlier live-view cell may reclaim any unpublished generation worker whose camera deadline comes later, including stale predicted-view work.
        if (!TryGetReadyHardViewContentCandidate(out Vector2Int requestedCoordinate))
            return false;

        return PreemptLowerPriorityContentForDemand(requestedCoordinate, false);
    }

    private bool TryGetReadyPredictedContentCandidate(out Vector2Int selectedCoordinate)
    {
        selectedCoordinate = default;
        bool foundCandidate = false;
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        for (int index = 0; index < _queue.Count; index++)
        {
            Vector2Int candidate = _queue[index];
            if (!_semanticViewPredictionArrivalSeconds.ContainsKey(candidate) ||
                _guaranteedViewDemand.Contains(candidate) ||
                !_hardViewDemand.Contains(candidate) ||
                !_chunks.TryGetValue(candidate, out RuntimeChunk chunk) ||
                chunk == null || chunk.physicalRepresentation ||
                chunk.state == YQSemanticChunkLifecycle.Generating ||
                ResolveTerrainForContentMaterialization(candidate) == null)
                continue;
            if (!foundCandidate || CompareContentQueuePriority(candidate, selectedCoordinate, traversalVelocity) < 0)
            {
                selectedCoordinate = candidate;
                foundCandidate = true;
            }
        }
        return foundCandidate;
    }

    private void RecordVisibleTerrainDispatchWait(int visibleWaitReason)
    {
        // note: Attribute only the currently incomplete visual owner and at most one reason per frame; no queue scan or log is added to ordinary streaming.
        Vector2Int coordinate = _lastVisualCoverageFailureCoordinate;
        RuntimeChunk owner = _lastVisualCoverageFailureOwner;
        if (owner == null || !_hasLastVisualCoverageFailureCoordinate ||
            owner.terrainReadiness >= YQTerrainReadinessState.CollisionReady ||
            (visibleWaitReason != 8 && !_terrainQueued.Contains(coordinate)) ||
            owner.lastTerrainDispatchDiagnosticFrame == Time.frameCount)
            return;

        owner.lastTerrainDispatchDiagnosticFrame = Time.frameCount;
        owner.terrainDispatchWaitFrames++;
        owner.lastTerrainDispatchQueueIndex = _terrainQueue.IndexOf(coordinate);
        switch (visibleWaitReason)
        {
            case 1: owner.terrainDispatchActiveFrames++; break;
            case 2: owner.terrainDispatchPublicationFrames++; break;
            case 3: owner.terrainDispatchPainterFrames++; break;
            case 4: owner.terrainDispatchAdmissionFrames++; break;
            case 6: owner.terrainDispatchCapacityFrames++; break;
            case 7: owner.terrainDispatchOtherCandidateFrames++; break;
            case 8: owner.terrainDispatchStartedFrames++; break;
        }
    }

    private bool HasActiveLiveViewContentWorker()
    {
        // note: The worker set is capped at three, so this bounded check can protect the visible step without introducing a scheduler queue.
        foreach (Vector2Int coordinate in _activeGenerations.Keys)
            if (_guaranteedViewDemand.Contains(coordinate))
                return true;
        return false;
    }

    private bool TryGetReadyHardPreparationContentCandidate(out Vector2Int selectedCoordinate)
    {
        #if UNITY_EDITOR
        if (R2ObserveContentCandidateScans) R2ContentCandidateScans++;
        #endif
        // note: Measure the bounded preparation-candidate scan separately from visible-view work.
        using (ReadyHardPreparationScanProfilerMarker.Auto())
            return TryGetReadyHardPreparationContentCandidateCore(out selectedCoordinate);
    }

    private bool TryGetReadyHardPreparationContentCandidateCore(out Vector2Int selectedCoordinate)
    {
        selectedCoordinate = default;
        bool foundCandidate = false;
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        for (int index = 0; index < _queue.Count; index++)
        {
            Vector2Int candidate = _queue[index];
            if (!_hardViewDemand.Contains(candidate) ||
                _guaranteedViewDemand.Contains(candidate) ||
                _semanticViewPredictionArrivalSeconds.ContainsKey(candidate) ||
                !_chunks.TryGetValue(candidate, out RuntimeChunk chunk) ||
                chunk == null || chunk.physicalRepresentation ||
                chunk.state == YQSemanticChunkLifecycle.Generating ||
                ResolveTerrainForContentMaterialization(candidate) == null)
                continue;

            if (!foundCandidate || CompareContentQueuePriority(candidate, selectedCoordinate, traversalVelocity) < 0)
            {
                selectedCoordinate = candidate;
                foundCandidate = true;
            }
        }
        return foundCandidate;
    }

    private bool PreemptLowerPriorityContentForDemand(Vector2Int requestedCoordinate, bool predictedDeadline)
    {
        // note: Predicted work preempts only a strictly later deadline; equal-deadline coordinate ties cannot churn live workers.
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        Vector2Int victimCoordinate = default;
        RuntimeChunk victim = null;
        foreach (KeyValuePair<Vector2Int, Coroutine> pair in _activeGenerations)
        {
            Vector2Int coordinate = pair.Key;
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) ||
                chunk == null ||
                chunk.physicalRepresentation ||
                chunk.state != YQSemanticChunkLifecycle.Generating ||
                coordinate == _currentChunk ||
                (predictedDeadline
                    ? CompareContentQueueDeadlines(requestedCoordinate, coordinate, traversalVelocity)
                    : CompareContentQueuePriority(requestedCoordinate, coordinate, traversalVelocity)) >= 0)
                continue;

            if (victim == null ||
                (predictedDeadline
                    ? CompareContentQueueDeadlines(coordinate, victimCoordinate, traversalVelocity)
                    : CompareContentQueuePriority(coordinate, victimCoordinate, traversalVelocity)) > 0)
            {
                victimCoordinate = coordinate;
                victim = chunk;
            }
        }
        if (victim == null)
            return false;

        // note: Capture the last cancelled owner before disposal clears its work registration.
        victim.diagnosticPreemptionCount++;
        victim.lastPreemptedEpoch = victim.contentOwnerEpoch;
        victim.lastPreemptedAt = Time.realtimeSinceStartupAsDouble;
        victim.lastPreemptingCoordinate = requestedCoordinate;
        victim.structuralProgress.Admitted();
        victim.ecologyProgress.Admitted();
        if (_activeGenerations.TryGetValue(victimCoordinate, out Coroutine generation) && generation != null)
            StopCoroutine(generation);
        _activeGenerations.Remove(victimCoordinate);
        _activeGenerationWorkIds.Remove(victimCoordinate);
        _demandCancelledGenerations.Remove(victimCoordinate);
        // note: A view deadline may reclaim an unpublished non-current worker; its terrain owner and canonical record remain intact.
        ReleaseObsoleteGenerationContent(victim);
        victim.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
        if (IsContentDemandedNow(victimCoordinate))
            Enqueue(victimCoordinate);
        // note: Preemption is expected during rapid turns; counters retain evidence without allocating a Console message in the hot path.
        _contentPreemptionCount++;
        if (predictedDeadline)
            _predictedContentPreemptionCount++;
        return true;
    }

    private void RetryFailedTerrainRequests()
    {
        // note: A transient terrain upload failure must not become a permanent world edge; retry only nearby cells with a short deterministic backoff.
        if (_terrainRetryAt.Count == 0)
            return;
        _terrainRetryScratch.Clear();
        foreach (KeyValuePair<Vector2Int, float> retry in _terrainRetryAt)
        {
            if (retry.Value <= Time.unscaledTime)
                _terrainRetryScratch.Add(retry.Key);
            if (_terrainRetryScratch.Count >= MaximumTerrainRetriesPerFrame)
                break;
        }
        for (int index = 0; index < _terrainRetryScratch.Count; index++)
        {
            Vector2Int coordinate = _terrainRetryScratch[index];
            _terrainRetryAt.Remove(coordinate);
            // note: Expired retries are re-admitted only while their semantic owner is still retained near the player; stale failures must not rebuild a discarded edge tile.
            if (IsTerrainRequestStillRelevant(coordinate))
                RequestTerrainForChunk(coordinate);
        }
    }

    private void RecordTerrainPreparationFailure(Vector2Int coordinate, RuntimeChunk chunk, string failure)
    {
        if (chunk == null || !_chunks.TryGetValue(coordinate, out RuntimeChunk current) ||
            !ReferenceEquals(current, chunk) || !IsTerrainRequestStillRelevant(coordinate))
            return;

        _terrainPreparationRetryCount.TryGetValue(coordinate, out int retryCount);
        if (retryCount >= MaximumTerrainPreparationRetries)
        {
            _terrainRetryAt.Remove(coordinate);
            chunk.state = YQSemanticChunkLifecycle.Failed;
            chunk.failureReason = "terrain preparation retry budget exhausted: " +
                (string.IsNullOrWhiteSpace(failure) ? "terrain was not published" : failure);
            _lifecyclePending = true;
            Debug.LogError("[YQSemanticChunkStreamer] TERRAIN COLLISION PUBLISH FAILED " +
                coordinate + ": " + chunk.failureReason);
            return;
        }

        retryCount++;
        _terrainPreparationRetryCount[coordinate] = retryCount;
        _terrainRetryAt[coordinate] = Time.unscaledTime + TerrainPreparationRetryDelaySeconds;
        _lifecyclePending = true;
        Debug.LogWarning("[YQSemanticChunkStreamer] TERRAIN COLLISION RETRY " + coordinate +
            " attempt=" + retryCount + "/" + MaximumTerrainPreparationRetries + ": " +
            (string.IsNullOrWhiteSpace(failure) ? "terrain was not published" : failure));
    }

    private void RequestTerrainForChunk(Vector2Int coordinate)
    {
        // note: A saved frontier can carry an Unloaded lifecycle from an earlier trip; rearm it before admitting the same cell to the collision queue.
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk existing) && existing != null &&
            (existing.state == YQSemanticChunkLifecycle.Unloading || existing.state == YQSemanticChunkLifecycle.Unloaded))
            existing.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
        if (existing != null && existing.failureReason != null &&
            existing.failureReason.StartsWith("terrain preparation retry budget exhausted", StringComparison.Ordinal) &&
            _terrainPreparationRetryCount.TryGetValue(coordinate, out int exhaustedRetryCount) &&
            exhaustedRetryCount >= MaximumTerrainPreparationRetries)
            return;
        if (IsChunkInsideAuthoredTerrain(coordinate))
            return;
        if (HasPublishedTerrain(coordinate))
        {
            _terrainRetryAt.Remove(coordinate);
            _terrainPreparationRetryCount.Remove(coordinate);
            if (existing != null && existing.failureReason != null &&
                existing.failureReason.StartsWith("terrain preparation retry budget exhausted", StringComparison.Ordinal))
                existing.failureReason = string.Empty;
            // note: A published collider is not a complete visible cell; reconcile its missing appearance before accepting the request as satisfied.
            ReconcilePublishedTerrainAppearance(coordinate);
            return;
        }
        if (_pendingTerrainPublications.TryGetValue(coordinate, out PendingTerrainPublication pendingPublication))
        {
            // note: A matching token already owns this cell's collider handoff; retire stale tickets before admitting replacement work.
            if (pendingPublication != null &&
                IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), pendingPublication.token))
            {
                _duplicateTerrainRequestCount++;
                return;
            }
            CancelPendingTerrainPublication(coordinate);
        }
        // note: A demanded terrain retry honors its short bounded backoff so a failed collider cannot spin every frame.
        if (coordinate == _terrainPreparingCoordinate || _terrainQueued.Contains(coordinate) ||
            (_terrainRetryAt.TryGetValue(coordinate, out float retryAt) && retryAt > Time.unscaledTime))
        {
            // note: Repeated frontier observations are expected and are ignored because the coordinate remains owned by one physical request.
            _duplicateTerrainRequestCount++;
            return;
        }
        // note: Timestamp only the first physical admission so readiness includes bounded queue delay without double-counting retries.
        if (_terrainQueue.Count >= TerrainQueueCapacity && !_batchingHardViewAdmission)
        {
            // note: Enforce the hard queue cap during admission, not after a complete corridor temporarily overfills it, without sorting once per candidate.
            Vector2 velocity = ResolveTraversalVelocity();
            int leastUrgentIndex = FindLeastUrgentTerrainRequest(velocity);
            Vector2Int leastUrgent = _terrainQueue[leastUrgentIndex];
            if (!IsTerrainRequestMoreUrgent(coordinate, leastUrgent, velocity))
            {
                if (_completeCellHistory != null || coordinate == FixedTerrainTraceTarget)
                    RecordFixedTerrainObservation("refused:queueAdmissionCapacity", coordinate);
                return;
            }
            // note: An urgent P0/P1 request may replace only the least-urgent queued owner; the active Unity mutation remains untouched.
            _terrainQueue.RemoveAt(leastUrgentIndex);
            _terrainQueued.Remove(leastUrgent);
            _terrainRequestedAt.Remove(leastUrgent);
            _terrainCriticalRequestedAt.Remove(leastUrgent);
            if (_completeCellHistory != null || leastUrgent == FixedTerrainTraceTarget)
                RecordFixedTerrainObservation("requestEvicted", leastUrgent);
        }
        if (!_terrainRequestedAt.ContainsKey(coordinate))
            _terrainRequestedAt[coordinate] = Time.unscaledTime;
        _terrainQueued.Add(coordinate);
        _terrainQueue.Add(coordinate);
        ObserveCompleteCell(coordinate, "admitted");
        // note: Retain the first admitted request's clock across requeues; the counter identifies subsequent queue admissions.
        if (_fixedTerrainObservations != null && coordinate == FixedTerrainTraceTarget)
        {
            if (_fixedTerrainFirstAdmission < 0d)
                _fixedTerrainFirstAdmission = Time.realtimeSinceStartupAsDouble;
            _fixedTerrainRequest++;
            RecordFixedTerrainObservation("admitted", coordinate);
        }
        // note: Record total pending physical work at admission, including the one active preparation if present.
        _maximumTerrainQueueDepth = Mathf.Max(
            _maximumTerrainQueueDepth,
            PendingTerrainCollisionCount);
        // note: Keep the P0/P1 high-water mark independent from the total bounded speculative queue.
        _maximumTraversalCriticalTerrainQueueDepth = Mathf.Max(
            _maximumTraversalCriticalTerrainQueueDepth,
            CountTraversalCriticalTerrainRequests());
    }

    private void ReconcileDemandedAppearanceSlice()
    {
        // note: Snapshot only owners with unfinished work so a rotating batch of already-running distant painters cannot hide a visible repair.
        _appearanceReconciliationScratch.Clear();
        _appearanceReconciliationMembershipScratch.Clear();
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (IsAppearanceReconciliationCandidate(coordinate) &&
                _appearanceReconciliationMembershipScratch.Add(coordinate))
                _appearanceReconciliationScratch.Add(coordinate);
        }
        foreach (Vector2Int coordinate in _hardViewDemand)
        {
            if (!_guaranteedViewDemand.Contains(coordinate) &&
                IsAppearanceReconciliationCandidate(coordinate) &&
                _appearanceReconciliationMembershipScratch.Add(coordinate))
                _appearanceReconciliationScratch.Add(coordinate);
        }
        foreach (Vector2Int coordinate in _contentDemand)
        {
            if (!_appearanceReconciliationMembershipScratch.Contains(coordinate) &&
                IsAppearanceReconciliationCandidate(coordinate))
            {
                _appearanceReconciliationMembershipScratch.Add(coordinate);
                _appearanceReconciliationScratch.Add(coordinate);
            }
        }
        // note: Physical admission is the authoritative demand union after it is built; include its owners so demanded recovery work cannot be invisible to this scheduler.
        foreach (Vector2Int coordinate in _physicalDemand.Keys)
        {
            if (!_guaranteedViewDemand.Contains(coordinate) &&
                !_hardViewDemand.Contains(coordinate) &&
                !_contentDemand.Contains(coordinate) &&
                IsAppearanceReconciliationCandidate(coordinate) &&
                _appearanceReconciliationMembershipScratch.Add(coordinate))
                _appearanceReconciliationScratch.Add(coordinate);
        }
        // note: Verification pins use the same bounded priority and per-frame reconciliation cap as production demand.
        foreach (Vector2Int coordinate in _publicationVerificationDemand)
        {
            if (!_guaranteedViewDemand.Contains(coordinate) &&
                !_hardViewDemand.Contains(coordinate) &&
                !_contentDemand.Contains(coordinate) &&
                !_physicalDemand.ContainsKey(coordinate) &&
                IsAppearanceReconciliationCandidate(coordinate) &&
                _appearanceReconciliationMembershipScratch.Add(coordinate))
                _appearanceReconciliationScratch.Add(coordinate);
        }

        int count = _appearanceReconciliationScratch.Count;
        int selectedCount = Mathf.Min(count, MaximumAppearanceReconciliationsPerFrame);
        for (int selected = 0; selected < selectedCount; selected++)
        {
            // note: Select the earliest live-view/deadline candidates with a bounded partial selection instead of sorting the full frontier each frame.
            int bestIndex = selected;
            for (int candidateIndex = selected + 1; candidateIndex < count; candidateIndex++)
            {
                if (CompareAppearanceReconciliationPriority(
                        _appearanceReconciliationScratch[candidateIndex],
                        _appearanceReconciliationScratch[bestIndex]) < 0)
                    bestIndex = candidateIndex;
            }
            if (bestIndex != selected)
            {
                Vector2Int swap = _appearanceReconciliationScratch[selected];
                _appearanceReconciliationScratch[selected] = _appearanceReconciliationScratch[bestIndex];
                _appearanceReconciliationScratch[bestIndex] = swap;
            }

            Vector2Int coordinate = _appearanceReconciliationScratch[selected];
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
                continue;

            // note: Restore a missing semantic request before reconciling the same coordinate's independent terrain appearance.
            bool requiredContentPending = !chunk.physicalRepresentation &&
                chunk.state != YQSemanticChunkLifecycle.Generating &&
                chunk.state != YQSemanticChunkLifecycle.Failed &&
                !_queue.Contains(coordinate) &&
                !_activeGenerations.ContainsKey(coordinate) &&
                IsContentDemandedNow(coordinate);
            if (requiredContentPending)
            {
                if (!IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
                    RequestTerrainForChunk(coordinate);
                Enqueue(coordinate);
                continue;
            }

            if (!IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
            {
                RequestTerrainForChunk(coordinate);
                continue;
            }

            if (IsChunkInsideAuthoredTerrain(coordinate) || chunk.appearanceReady ||
                _terrainPainting.ContainsKey(coordinate))
                continue;
            RequestTerrainForChunk(coordinate);
        }
        _appearanceReconciliationScratch.Clear();
        _appearanceReconciliationMembershipScratch.Clear();
    }

    private bool IsAppearanceReconciliationCandidate(Vector2Int coordinate)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            return false;

        bool requiredContentPending = !chunk.physicalRepresentation &&
            chunk.state != YQSemanticChunkLifecycle.Generating &&
            chunk.state != YQSemanticChunkLifecycle.Failed &&
            !_queue.Contains(coordinate) &&
            !_activeGenerations.ContainsKey(coordinate) &&
            IsContentDemandedNow(coordinate);
        if (requiredContentPending)
            return true;

        if (IsChunkInsideAuthoredTerrain(coordinate))
            return false;
        if (!HasPublishedTerrain(coordinate))
        {
            bool terrainAlreadyOwned = coordinate == _terrainPreparingCoordinate ||
                _terrainQueued.Contains(coordinate) ||
                _pendingTerrainPublications.ContainsKey(coordinate) ||
                _terrainHeightPrefetches.ContainsKey(coordinate) ||
                (_terrainRetryAt.TryGetValue(coordinate, out float terrainRetryAt) &&
                 terrainRetryAt > Time.unscaledTime);
            return !terrainAlreadyOwned;
        }

        return !chunk.appearanceReady && !_terrainPainting.ContainsKey(coordinate) &&
            (!_terrainAppearanceRetryAt.TryGetValue(coordinate, out float appearanceRetryAt) ||
             appearanceRetryAt <= Time.unscaledTime);
    }

    private int CompareAppearanceReconciliationPriority(Vector2Int left, Vector2Int right)
    {
        bool leftVisible = _guaranteedViewDemand.Contains(left);
        bool rightVisible = _guaranteedViewDemand.Contains(right);
        if (leftVisible != rightVisible)
            return leftVisible ? -1 : 1;

        // note: Give the single explicitly pinned recovery owner a fair scheduler turn so the test observes the actual retry path under a busy frontier.
        bool leftVerificationPinned = _publicationVerificationDemand.Contains(left);
        bool rightVerificationPinned = _publicationVerificationDemand.Contains(right);
        if (leftVerificationPinned != rightVerificationPinned)
            return leftVerificationPinned ? -1 : 1;

        bool leftPredicted = _semanticViewPredictionArrivalSeconds.TryGetValue(left, out float leftArrival);
        bool rightPredicted = _semanticViewPredictionArrivalSeconds.TryGetValue(right, out float rightArrival);
        bool leftHardView = _hardViewDemand.Contains(left);
        bool rightHardView = _hardViewDemand.Contains(right);
        int leftPriority = leftVisible ? 0 : leftPredicted ? 1 : leftHardView ? 2 : 3;
        int rightPriority = rightVisible ? 0 : rightPredicted ? 1 : rightHardView ? 2 : 3;
        int priorityCompare = leftPriority.CompareTo(rightPriority);
        if (priorityCompare != 0)
            return priorityCompare;
        if (!leftVisible && leftPredicted && rightPredicted)
        {
            int arrivalCompare = leftArrival.CompareTo(rightArrival);
            if (arrivalCompare != 0)
                return arrivalCompare;
        }
        if (leftVisible && _player != null)
        {
            float size = Mathf.Max(32f, chunkWorldSize);
            Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
            int distanceCompare = DistanceToChunkSquared(playerPosition, left, size).CompareTo(
                DistanceToChunkSquared(playerPosition, right, size));
            if (distanceCompare != 0)
                return distanceCompare;
        }
        return CompareCoordinates(left, right);
    }

    private bool TryReconcileUrgentViewAppearance()
    {
        Vector2 playerPosition = _player != null
            ? new Vector2(_player.position.x, _player.position.z)
            : Vector2.zero;
        float size = Mathf.Max(32f, chunkWorldSize);
        Vector2Int selected = default;
        bool hasSelected = false;
        float selectedDistance = float.MaxValue;
        float selectedArrival = float.MaxValue;
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (IsChunkInsideAuthoredTerrain(coordinate) ||
                !_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
                chunk.appearanceReady || _terrainPainting.ContainsKey(coordinate) ||
                !HasPublishedTerrain(coordinate) ||
                (_terrainAppearanceRetryAt.TryGetValue(coordinate, out float retryAt) &&
                 retryAt > Time.unscaledTime))
                continue;
            float candidateDistance = DistanceToChunkSquared(playerPosition, coordinate, size);
            if (!hasSelected || candidateDistance < selectedDistance ||
                (Mathf.Approximately(candidateDistance, selectedDistance) &&
                 CompareCoordinates(coordinate, selected) < 0))
            {
                selected = coordinate;
                selectedDistance = candidateDistance;
                hasSelected = true;
            }
        }
        if (!hasSelected)
        {
            // note: Start one bounded appearance owner for the earliest predicted camera cell when ordinary reconciliation exhausts its frame allowance.
            foreach (KeyValuePair<Vector2Int, float> prediction in _semanticViewPredictionArrivalSeconds)
            {
                Vector2Int coordinate = prediction.Key;
                float arrival = prediction.Value;
                if (arrival < 0f || arrival > CriticalTerrainEscalationSeconds ||
                    IsChunkInsideAuthoredTerrain(coordinate) ||
                    !_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
                    chunk.appearanceReady || _terrainPainting.ContainsKey(coordinate) ||
                    !HasPublishedTerrain(coordinate) ||
                    (_terrainAppearanceRetryAt.TryGetValue(coordinate, out float retryAt) &&
                     retryAt > Time.unscaledTime))
                    continue;

                if (!hasSelected || arrival < selectedArrival ||
                    (Mathf.Approximately(arrival, selectedArrival) &&
                     CompareCoordinates(coordinate, selected) < 0))
                {
                    selected = coordinate;
                    selectedArrival = arrival;
                    hasSelected = true;
                }
            }
        }
        if (!hasSelected)
            return false;

        // note: A live or imminently visible published tile gets the one bounded appearance admission even when ordinary work used the frame allowance.
        RequestTerrainForChunk(selected);
        return true;
    }

    private void ReconcilePublishedTerrainAppearance(Vector2Int coordinate)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
            IsChunkInsideAuthoredTerrain(coordinate) || !HasPublishedTerrain(coordinate) ||
            chunk.appearanceReady || _terrainPainting.ContainsKey(coordinate) ||
            (!_hardViewDemand.Contains(coordinate) && !IsContentDemandedNow(coordinate)))
            return;

        // note: Do not start or repeatedly defer background paint while a live camera cell is incomplete; those paused workers otherwise occupy every reserved painter slot.
        if (!_guaranteedViewDemand.Contains(coordinate) &&
            !_hardViewDemand.Contains(coordinate) &&
            ShouldPauseTerrainAppearanceForVisibleDemand(coordinate))
            return;

        bool scheduledRetry = _terrainAppearanceRetryAt.TryGetValue(coordinate, out float retryAt);
        if (scheduledRetry && retryAt > Time.unscaledTime)
            return;

        _terrainAppearanceRetryCount.TryGetValue(coordinate, out int retryCount);
        if (retryCount >= MaximumTerrainAppearanceRetries && !scheduledRetry)
        {
            // note: A bounded exhausted retry is an explicit publication failure, never an invisible fallback to collision-only readiness.
            if (string.IsNullOrWhiteSpace(chunk.failureReason))
            {
                chunk.failureReason = "terrain appearance retry budget exhausted";
                Debug.LogError("[YQSemanticChunkStreamer] TERRAIN APPEARANCE PUBLISH FAILED " + coordinate);
            }
            return;
        }

        if (!EnsureTerrainAppearancePainterCapacity(coordinate))
        {
            // note: Capacity deferral is live work admission, not a failed attempt.
            _terrainAppearanceRetryAt[coordinate] = Time.unscaledTime + 0.05f;
            _lifecyclePending = true;
            return;
        }
        _terrainAppearanceRetryAt.Remove(coordinate);

        if (!_extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) ||
            terrain == null || terrain.terrainData == null || !IsTerrainColliderReady(terrain))
        {
            // note: A demanded appearance lane with missing prerequisites receives a bounded retry receipt instead of disappearing with no painter or failure.
            RecordTerrainAppearanceRetryFailure(coordinate, chunk, "published terrain/data/collider is unavailable");
            return;
        }

        StartTerrainAppearancePainting(
            coordinate,
            terrain.terrainData,
            Mathf.Max(32f, chunkWorldSize),
            terrain,
            CaptureStreamingWorkToken(coordinate, true));
        _lifecyclePending = true;
        Debug.LogWarning("[YQSemanticChunkStreamer] TERRAIN APPEARANCE RETRY " + coordinate +
            " attempt=" + (retryCount + 1));
    }

    private void RecordTerrainAppearanceRetryFailure(
        Vector2Int coordinate,
        RuntimeChunk chunk,
        string failure)
    {
        if (chunk == null || !_hardViewDemand.Contains(coordinate) && !IsContentDemandedNow(coordinate))
            return;
        _terrainAppearanceRetryCount.TryGetValue(coordinate, out int retryCount);
        if (retryCount >= MaximumTerrainAppearanceRetries)
        {
            _terrainAppearanceRetryAt.Remove(coordinate);
            chunk.failureReason = "terrain appearance retry budget exhausted: " + failure;
            Debug.LogError("[YQSemanticChunkStreamer] TERRAIN APPEARANCE PUBLISH FAILED " +
                coordinate + ": " + chunk.failureReason);
            _lifecyclePending = true;
            return;
        }
        _terrainAppearanceRetryCount[coordinate] = retryCount + 1;
        _terrainAppearanceRetryAt[coordinate] = Time.unscaledTime + TerrainAppearanceRetryDelaySeconds;
        // note: Scheduling this stage must not erase a terminal failure belonging to another stage.
        _lifecyclePending = true;
        Debug.LogWarning("[YQSemanticChunkStreamer] TERRAIN APPEARANCE PREREQUISITE RETRY " +
            coordinate + " attempt=" + (retryCount + 1) + ": " + failure);
    }

    private void StartTerrainAppearancePainting(
        Vector2Int coordinate,
        TerrainData data,
        float size,
        Terrain targetTerrain,
        StreamingWorkToken token,
        YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparedBiomeAlphamap = null)
    {
        if (!IsGenerationCurrent(coordinate, token.owner, token))
        {
            preparedBiomeAlphamap?.Dispose();
            return;
        }
        // note: Terrain completion may arrive outside the normal reconciliation path; retain a retry receipt instead of bypassing the bounded painter pool.
        bool replacingExistingPainter = _terrainPainting.ContainsKey(coordinate);
        if (!replacingExistingPainter && !EnsureTerrainAppearancePainterCapacity(coordinate))
        {
            _terrainAppearanceRetryAt[coordinate] = Time.unscaledTime + 0.05f;
            _lifecyclePending = true;
            preparedBiomeAlphamap?.Dispose();
            return;
        }
        StopTerrainPaintingForChunk(token.owner);
        long workId = ++_terrainPaintingWorkVersion;
        _terrainPaintingWorkIds[coordinate] = workId;
        _terrainPainting[coordinate] = null;
        token.owner.appearanceWorkStartedAt = Time.realtimeSinceStartupAsDouble;
        token.owner.biomeAppearanceProgress.Reset();
        token.owner.detailAppearanceProgress.Reset();
        token.owner.appearanceDetailSubstage = string.Empty;
        Coroutine painting;
        try
        {
            painting = StartCoroutine(PaintTerrainAppearanceRoutine(
                coordinate,
                data,
                size,
                targetTerrain,
                token,
                workId,
                preparedBiomeAlphamap));
        }
        catch
        {
            if (_terrainPaintingWorkIds.TryGetValue(coordinate, out long failedWorkId) && failedWorkId == workId)
            {
                _terrainPainting.Remove(coordinate);
                _terrainPaintingWorkIds.Remove(coordinate);
            }
            preparedBiomeAlphamap?.Dispose();
            throw;
        }
        if (_terrainPaintingWorkIds.TryGetValue(coordinate, out long activeWorkId) && activeWorkId == workId)
        {
            if (painting != null)
                _terrainPainting[coordinate] = painting;
            else
            {
                _terrainPainting.Remove(coordinate);
                _terrainPaintingWorkIds.Remove(coordinate);
                _terrainAppearanceRetryAt[coordinate] = Time.unscaledTime + 0.05f;
                _lifecyclePending = true;
                preparedBiomeAlphamap?.Dispose();
            }
        }
    }

    private bool HasTerrainAppearancePainterCapacity(Vector2Int coordinate)
    {
        bool reservedAppearanceDemand = IsAppearanceDeadlineDemand(coordinate);
        int workerLimit = MaximumTerrainPaintingWorkers +
            (reservedAppearanceDemand ? LiveVisibleTerrainPaintingWorkerReserve : 0);
        return _terrainPainting.Count < workerLimit;
    }

    private bool EnsureTerrainAppearancePainterCapacity(Vector2Int coordinate)
    {
        if (HasTerrainAppearancePainterCapacity(coordinate))
            return true;
        if (!IsAppearanceDeadlineDemand(coordinate))
            return false;

        // note: Reclaim background painters first, then a farther projected view cell if this request has an earlier camera-arrival deadline.
        _terrainPaintingScratch.Clear();
        foreach (KeyValuePair<Vector2Int, Coroutine> painting in _terrainPainting)
        {
            Vector2Int candidate = painting.Key;
            if (candidate == coordinate)
                continue;
            // note: Two painters already inside the current frustum have the same deadline; do not restart either one on a coordinate tie-break.
            bool bothCurrentlyVisible = _guaranteedViewDemand.Contains(coordinate) &&
                _guaranteedViewDemand.Contains(candidate);
            bool preemptableDeadline = !bothCurrentlyVisible && IsAppearanceDeadlineDemand(candidate) &&
                IsAppearanceDeadlineEarlier(coordinate, candidate);
            bool preemptableBackground = !IsAppearanceDeadlineDemand(candidate) &&
                ShouldPauseTerrainAppearanceForVisibleDemand(candidate);
            if (!preemptableDeadline && !preemptableBackground)
                continue;
            _terrainPaintingScratch.Add(candidate);
        }
        _terrainPaintingScratch.Sort(CompareAppearancePreemptionCandidates);

        int index = 0;
        while (_terrainPainting.Count >= MaximumTerrainPaintingWorkers + LiveVisibleTerrainPaintingWorkerReserve &&
               index < _terrainPaintingScratch.Count)
        {
            Vector2Int deferredCoordinate = _terrainPaintingScratch[index++];
            if (_chunks.TryGetValue(deferredCoordinate, out RuntimeChunk deferredChunk) && deferredChunk != null)
            {
                // note: Opt-in history identifies both forecast deadlines and the discarded stage at destructive capacity preemption.
                if (_completeCellHistory != null)
                    ObserveCompleteCell(deferredCoordinate, "appearanceCapacityPreemption:request=" + coordinate +
                        ":arrival=" + GetAppearanceDeadlineArrival(coordinate).ToString("F4", CultureInfo.InvariantCulture) +
                        ":victimArrival=" + GetAppearanceDeadlineArrival(deferredCoordinate).ToString("F4", CultureInfo.InvariantCulture), true);
                StopTerrainPaintingForChunk(deferredChunk);
                ObserveCompleteCell(deferredCoordinate, "appearanceCapacityPreempted", true);
            }
        }
        _terrainPaintingScratch.Clear();
        return HasTerrainAppearancePainterCapacity(coordinate);
    }

    private bool IsAppearanceDeadlineDemand(Vector2Int coordinate)
    {
        // note: Current and forecast camera cells share the reserved appearance lane so future paint is not postponed until it has already entered view.
        return _guaranteedViewDemand.Contains(coordinate) ||
            _semanticViewPredictionArrivalSeconds.ContainsKey(coordinate) ||
            _publicationVerificationDemand.Contains(coordinate);
    }

    private bool HasImmediateTerrainAppearanceDeadline(Vector2Int coordinate)
    {
        // note: A tile already in the camera frustum shares the hard-view painter quantum instead of yielding every traversal-reserved frame.
        return _hardViewDemand.Contains(coordinate) || _guaranteedViewDemand.Contains(coordinate);
    }

    private bool HasHigherPriorityAppearanceReserveOwner(Vector2Int? coordinate)
    {
        bool visible = coordinate.HasValue && _guaranteedViewDemand.Contains(coordinate.Value);
        // note: Only painters eligible for this reserve may block it; a farther forecast must not win merely because its coroutine resumed first.
        foreach (Vector2Int candidate in _terrainPainting.Keys)
        {
            if ((coordinate.HasValue && candidate == coordinate.Value) ||
                !HasImmediateTerrainAppearanceDeadline(candidate))
                continue;
            if (!coordinate.HasValue)
                return true;
            // note: Retain denial-age fairness among current visible painters, then use the existing deterministic forecast deadline order.
            if (visible
                ? _guaranteedViewDemand.Contains(candidate) && CompareLiveAppearanceProgress(candidate, coordinate.Value) < 0
                : CompareAppearanceReconciliationPriority(candidate, coordinate.Value) < 0)
                return true;
        }
        return false;
    }

    private int CompareLiveAppearanceProgress(Vector2Int left, Vector2Int right)
    {
        // note: Give a frame's single over-budget appearance slice to the visible painter with the longest accumulated budget denial, then use stable existing priority.
        double now = Time.realtimeSinceStartupAsDouble;
        double leftDenied = LiveAppearanceDeniedSeconds(left, now);
        double rightDenied = LiveAppearanceDeniedSeconds(right, now);
        int deniedCompare = rightDenied.CompareTo(leftDenied);
        return deniedCompare != 0
            ? deniedCompare
            : CompareAppearanceReconciliationPriority(left, right);
    }

    private double LiveAppearanceDeniedSeconds(Vector2Int coordinate, double now)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk owner) || owner == null)
            return 0d;
        // note: Use the more-starved required paint lane rather than summing two overlapping denied intervals.
        double biomeDenied = owner.biomeAppearanceProgress.deniedSeconds +
            (owner.biomeAppearanceProgress.deniedStartedAt < 0d
                ? 0d
                : now - owner.biomeAppearanceProgress.deniedStartedAt);
        double detailDenied = owner.detailAppearanceProgress.deniedSeconds +
            (owner.detailAppearanceProgress.deniedStartedAt < 0d
                ? 0d
                : now - owner.detailAppearanceProgress.deniedStartedAt);
        return Math.Max(biomeDenied, detailDenied);
    }

    private bool IsAppearanceDeadlineEarlier(Vector2Int first, Vector2Int second)
    {
        // note: Compare live camera deadlines directly so a far-future prediction cannot occupy the reserved painter slot ahead of a near-entry cell.
        float firstArrival = GetAppearanceDeadlineArrival(first);
        float secondArrival = GetAppearanceDeadlineArrival(second);
        // note: Equal forecast buckets do not justify discarding an in-flight paint/payload lane; coordinate ties only order admission into free slots.
        return firstArrival < secondArrival;
    }

    private int CompareAppearancePreemptionCandidates(Vector2Int left, Vector2Int right)
    {
        bool leftDeadline = IsAppearanceDeadlineDemand(left);
        bool rightDeadline = IsAppearanceDeadlineDemand(right);
        if (leftDeadline != rightDeadline)
            return leftDeadline ? 1 : -1;
        if (leftDeadline)
        {
            // note: Release the farthest forecast owner first so closer camera work keeps its in-flight slice.
            int arrivalCompare = GetAppearanceDeadlineArrival(right).CompareTo(
                GetAppearanceDeadlineArrival(left));
            if (arrivalCompare != 0)
                return arrivalCompare;
        }
        return CompareCoordinates(left, right);
    }

    private float GetAppearanceDeadlineArrival(Vector2Int coordinate)
    {
        if (_publicationVerificationDemand.Contains(coordinate))
            return 0f;
        // note: A visible painter has an immediate deadline even if path prediction also classifies the same cell as future-facing.
        if (_guaranteedViewDemand.Contains(coordinate))
            return 0f;
        if (_semanticViewPredictionArrivalSeconds.TryGetValue(coordinate, out float arrivalSeconds))
            return arrivalSeconds;
        return float.PositiveInfinity;
    }

    private bool ShouldPauseTerrainAppearanceForVisibleDemand(Vector2Int coordinate)
    {
        if (IsAppearanceDeadlineDemand(coordinate))
            return false;
        foreach (Vector2Int visibleCoordinate in _guaranteedViewDemand)
        {
            if (IsChunkInsideAuthoredTerrain(visibleCoordinate))
                continue;
            if (!_chunks.TryGetValue(visibleCoordinate, out RuntimeChunk visibleChunk) ||
                visibleChunk == null || !visibleChunk.appearanceReady)
                return true;
        }
        return false;
    }

    private void ScheduleRequiredEcologyRetry(
        Vector2Int coordinate,
        RuntimeChunk chunk,
        string failure)
    {
        if (chunk == null || !_chunks.TryGetValue(coordinate, out RuntimeChunk current) ||
            !ReferenceEquals(current, chunk) || !IsContentDemandedNow(coordinate))
            return;

        _requiredEcologyRetryCount.TryGetValue(coordinate, out int retryCount);
        if (retryCount >= MaximumRequiredEcologyRetries)
        {
            _requiredEcologyRetryAt.Remove(coordinate);
            // note: Exhaustion is an explicit publication failure, not an invisible ecology=false owner that can remain in the camera envelope forever.
            chunk.failureReason = "required ecology retry budget exhausted: " + failure;
            if (_hardViewDemand.Contains(coordinate))
                chunk.state = YQSemanticChunkLifecycle.Failed;
            Debug.LogError("[YQSemanticChunkStreamer] REQUIRED ECOLOGY PUBLISH FAILED " + coordinate +
                ": " + chunk.failureReason);
            _lifecyclePending = true;
            return;
        }

        _requiredEcologyRetryCount[coordinate] = retryCount + 1;
        _requiredEcologyRetryAt[coordinate] = Time.unscaledTime + RequiredEcologyRetryDelaySeconds;
        chunk.requiredEcologyReady = false;
        chunk.decorativeComplete = false;
        // note: Ecology retry admission cannot clear a failure in terrain, structure, or overlays.
        _lifecyclePending = true;
        Debug.LogWarning("[YQSemanticChunkStreamer] REQUIRED ECOLOGY RETRY " + coordinate +
            " attempt=" + (retryCount + 1) + ": " + failure);
    }

    private bool IsTerrainRequestStillRelevant(Vector2Int coordinate)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            return false;
        // note: Keep the single explicit recovery witness eligible for terrain publication until its verification pin is released.
        if (_publicationVerificationDemand.Contains(coordinate))
            return true;
        // note: The all-direction 260 m/s runway is a live terrain lease even when it extends beyond the ordinary retention radius.
        if (_provisionalGroundTurnBufferDemand.Contains(coordinate))
            return true;
        // note: A camera-visible request remains authoritative even when it lies outside the ordinary movement retention radius.
        if (_guaranteedViewDemand.Contains(coordinate))
            return true;
        // note: Turn-buffer and preparation terrain is admitted beyond ordinary retention; keep that canonical hard-view request alive until its collider publishes.
        if (_hardViewDemand.Contains(coordinate))
            return true;
        // note: A live site footprint may straddle the normal retention boundary while its collision dependency is pending.
        if (_siteTerrainDependencies.ContainsKey(coordinate))
            return true;
        if (_sharedSiteOwnerDemand.Contains(coordinate))
            return true;
        int keepRadius = TerrainRequestRetentionRadius;
        int distance = Mathf.Max(
            Mathf.Abs(coordinate.x - _currentChunk.x),
            Mathf.Abs(coordinate.y - _currentChunk.y));
        // note: A request remains retryable only while its owner is retained near the player; obsolete teleports must not masquerade as an edge failure.
        return distance <= keepRadius && chunk.state != YQSemanticChunkLifecycle.Unloaded;
    }

    private void RequestTerrainLookahead(Vector2 playerPosition)
    {
        float size = Mathf.Max(32f, chunkWorldSize);
        Vector2Int current = ChunkFor(playerPosition);
        float localX = playerPosition.x - (WorldGridOrigin + current.x * size);
        float localZ = playerPosition.y - (WorldGridOrigin + current.y * size);
        float margin = Mathf.Clamp(size * 0.375f, 24f, 48f);

        // note: Predict the requested same-authority collision runway before motor acceleration catches up to a speed increase or reversal.
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        // note: Promote the exact current owner before speculative lookahead so a long frame or diagonal boundary jump cannot enter an unrequested cell.
        float currentAdmissionStarted = Time.realtimeSinceStartup;
        PrioritizeTerrainRequest(current);
        RecordStreamingStage("predictiveTerrainAdmission.current", currentAdmissionStarted);
        // note: Admit a narrow corridor of semantic owners along velocity instead of requesting one distant cell with unmaterialized gaps between it and the player.
        float corridorAdmissionStarted = Time.realtimeSinceStartup;
        RequestPredictiveTerrainCorridor(current, traversalVelocity);
        RecordStreamingStage("predictiveTerrainAdmission.corridor", corridorAdmissionStarted);
        // note: A newly admitted P0/P1 corridor may cancel only the current pure sampling task; no Unity terrain mutation is touched by this handoff.
        if (_terrainHeightPreparationCancellation != null && ShouldPreemptTerrainPreparation(_terrainPreparingCoordinate))
            _terrainHeightPreparationCancellation.Cancel();
        // note: Edge and corner destinations are prioritized after prediction so an imminent boundary can never be starved by a farther lookahead cell.
        if (localX <= margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x - 1, current.y));
        if (localX >= size - margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x + 1, current.y));
        if (localZ <= margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x, current.y - 1));
        if (localZ >= size - margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x, current.y + 1));
        if (localX <= margin && localZ <= margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x - 1, current.y - 1));
        if (localX <= margin && localZ >= size - margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x - 1, current.y + 1));
        if (localX >= size - margin && localZ <= margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x + 1, current.y - 1));
        if (localX >= size - margin && localZ >= size - margin)
            AdmitPhysicalTraversalDemand(new Vector2Int(current.x + 1, current.y + 1));
        _lastLookaheadPosition = playerPosition;
        _hasLastLookaheadPosition = true;
    }

    private void RequestRequiredTerrainCoverage()
    {
        int radius = Mathf.Min(TerrainCoverageRadius, EffectiveVisualPrewarmRadius);
        int diameter = radius * 2 + 1;
        int totalCoordinates = diameter * diameter;
        if (_requiredCoverageAdmissionRadius != radius)
        {
            // note: Reset the rotating cursor when the coverage radius changes so a new frontier starts with a deterministic repair order.
            _requiredCoverageAdmissionRadius = radius;
            _requiredCoverageAdmissionCursor = 0;
        }
        int admissions = 0;
        int scanned = 0;
        // note: Walk a rotating bounded square instead of restarting at ring zero; otherwise the first two unfinished cells can starve the rest of the required collision envelope forever.
        while (scanned < totalCoordinates && admissions < RequiredCoverageAdmissionsPerSlice)
        {
            int linearIndex = (_requiredCoverageAdmissionCursor + scanned) % totalCoordinates;
            int x = (linearIndex % diameter) - radius;
            int z = (linearIndex / diameter) - radius;
            Vector2Int coordinate = new Vector2Int(_currentChunk.x + x, _currentChunk.y + z);
            if (!IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
            {
                // note: Required nearby coverage admits terrain and required content together; no physical terrain-only ring is allowed.
                AdmitPhysicalTraversalDemand(coordinate);
                PrioritizeTerrainRequest(coordinate);
                admissions++;
            }
            scanned++;
        }
        // note: Advance by every inspected coordinate so already-ready cells cannot pin the cursor and starve a later missing cell.
        _requiredCoverageAdmissionCursor = (_requiredCoverageAdmissionCursor + Mathf.Max(1, scanned)) % totalCoordinates;
        // note: The Update pass performs one shared queue sort/trim after all demand sources; terrain dispatch sorts before selection if aggregate admission defers that pass.
    }

    private int TerrainPredictionRadius(Vector2 velocity)
    {
        // note: Convert the conservative time horizon into cells so lead scales with actual speed while remaining bounded.
        float speed = velocity.magnitude;
        float size = Mathf.Max(32f, chunkWorldSize);
        int requiredCells = Mathf.CeilToInt(speed * PredictiveReadinessHorizonSeconds / size) + 2;
        // note: Diagonal movement crosses two cell boundaries on one traversal vector, so retain two extra deterministic owners without widening straight-line latency.
        Vector2 direction = velocity.normalized;
        if (Mathf.Abs(direction.x) >= 0.35f && Mathf.Abs(direction.y) >= 0.35f)
            requiredCells += 2;
        return Mathf.Clamp(Mathf.Max(TerrainCoverageRadius, requiredCells), TerrainCoverageRadius, MaximumPredictiveTerrainRadius);
    }

    private int TerrainRequestRetentionRadius
    {
        get
        {
            // note: Retain the active predictive corridor long enough for an in-flight tile to finish, but never beyond the bounded admission radius.
            Vector2 velocity = ResolveTraversalVelocity();
            return Mathf.Max(TerrainCoverageRadius, EffectiveVisualPrewarmRadius, retentionRadius, TerrainPredictionRadius(velocity));
        }
    }

    private Vector2 ResolveTraversalVelocity()
    {
        // note: Prefer the authoritative motor's planar velocity so prediction does not lag behind transform sampling at high speed.
        if (_playerMotor != null && _playerMotor.isActiveAndEnabled && _playerMotor.IsAuthoritative)
        {
            // note: Avoid stale disabled-motor velocity so a controlled continuous transform probe still drives the predictive corridor.
            Vector3 velocity = _playerMotor.PlanarVelocity;
            return new Vector2(velocity.x, velocity.z);
        }
        if (_player == null || !_hasLastLookaheadPosition)
            return Vector2.zero;
        float deltaTime = Mathf.Max(0.001f, Time.unscaledDeltaTime);
        Vector2 position = new Vector2(_player.position.x, _player.position.z);
        return (position - _lastLookaheadPosition) / deltaTime;
    }

    private Vector2 ResolveTraversalDemandVelocity()
    {
        Vector2 velocity = ResolveTraversalVelocity();
        if (_playerMotor == null || !_playerMotor.isActiveAndEnabled || !_playerMotor.IsAuthoritative ||
            _playerMotor.MoveInput.sqrMagnitude <= 0.0001f)
            return velocity;

        // note: Project the accepted 260 m/s envelope along explicit movement input immediately; actual velocity and movement ownership remain with the motor.
        Transform motorTransform = _playerMotor.transform;
        Vector2 input = _playerMotor.MoveInput;
        Vector3 requestedDirection = motorTransform.forward * input.y + motorTransform.right * input.x;
        Vector2 requestedPlanar = new Vector2(requestedDirection.x, requestedDirection.z);
        if (requestedPlanar.sqrMagnitude <= 0.0001f)
            return velocity;
        return requestedPlanar.normalized * Mathf.Max(velocity.magnitude, MaximumGroundPrefetchSpeedMetersPerSecond);
    }

    private void RequestPredictiveTerrainCorridor(Vector2Int current, Vector2 velocity)
    {
        float admissionStartedAt = Time.realtimeSinceStartup;
        // note: Widen the bounded corridor at high speed while limiting synchronous semantic-owner creation to a short per-frame slice.
        if (velocity.sqrMagnitude < 0.01f)
            return;
        Vector2 direction = velocity.normalized;
        Vector2Int forward = new Vector2Int(
            Mathf.Abs(direction.x) >= 0.35f ? (direction.x >= 0f ? 1 : -1) : 0,
            Mathf.Abs(direction.y) >= 0.35f ? (direction.y >= 0f ? 1 : -1) : 0);
        if (forward == Vector2Int.zero)
            return;
        Vector2Int lateral = new Vector2Int(-forward.y, forward.x);
        int radius = TerrainPredictionRadius(velocity);
        int lateralHalfWidth = radius >= 6 ? 2 : 1;
        bool corridorChanged = !_hasPredictiveTerrainCorridor ||
            _lastPredictiveTerrainCell != current ||
            _lastPredictiveTerrainDirection != forward ||
            _lastPredictiveTerrainRadius != radius;
        if (corridorChanged)
        {
            // note: Rebuild only the small coordinate plan when speed, direction, or center changes; cell synthesis itself is resumed incrementally below.
            _hasPredictiveTerrainCorridor = true;
            _lastPredictiveTerrainCell = current;
            _lastPredictiveTerrainDirection = forward;
            _lastPredictiveTerrainRadius = radius;
            _predictiveTerrainCorridorCoordinates.Clear();
            _predictiveTerrainCorridorCursor = 0;
            if (forward.x != 0 && forward.y != 0)
            {
                // note: Put either immediate diagonal axis crossing first so bounded admission preserves both valid boundary orders.
                _predictiveTerrainCorridorCoordinates.Add(current + new Vector2Int(forward.x, 0));
                _predictiveTerrainCorridorCoordinates.Add(current + new Vector2Int(0, forward.y));
            }
            for (int step = 1; step <= radius; step++)
            {
                Vector2Int corridorCenter = current + forward * step;
                for (int side = -lateralHalfWidth; side <= lateralHalfWidth; side++)
                    _predictiveTerrainCorridorCoordinates.Add(corridorCenter + lateral * side);
                if (forward.x != 0 && forward.y != 0 && step <= 2)
                {
                    // note: Preserve the near diagonal axis alternatives behind the direct next-cell crossings.
                    _predictiveTerrainCorridorCoordinates.Add(corridorCenter + new Vector2Int(forward.x, 0));
                    _predictiveTerrainCorridorCoordinates.Add(corridorCenter + new Vector2Int(0, forward.y));
                }
            }
        }

        int corridorCount = _predictiveTerrainCorridorCoordinates.Count;
        int checks = 0;
        int admissions = 0;
        int ownerCreations = 0;
        int checksThisFrame = Mathf.Min(MaximumPredictiveTerrainAdmissionChecksPerFrame, corridorCount);
        while (checks < checksThisFrame && admissions < MaximumPredictiveTerrainAdmissionsPerFrame)
        {
            // note: A single indivisible owner build may exceed the slice; stop before starting another one once the time allowance is spent.
            if (admissions > 0 && Time.realtimeSinceStartup - admissionStartedAt >= PredictiveOwnerAdmissionBudgetSeconds)
                break;
            if (_predictiveTerrainCorridorCursor >= corridorCount)
                _predictiveTerrainCorridorCursor = 0;
            Vector2Int coordinate = _predictiveTerrainCorridorCoordinates[_predictiveTerrainCorridorCursor];
            bool hasOwner = _chunks.TryGetValue(coordinate, out RuntimeChunk chunk) && chunk != null;
            if (!hasOwner && ownerCreations >= MaximumPredictiveTerrainOwnerCreationsPerFrame)
                break;

            // note: Retain the full predicted content corridor, including ready cells, while only repairing owners that still have work.
            _contentDemand.Add(coordinate);
            _predictiveTerrainCorridorCursor = (_predictiveTerrainCorridorCursor + 1) % corridorCount;
            checks++;
            if (hasOwner && chunk.physicalRepresentation && chunk.requiredContentReady &&
                chunk.appearanceReady && chunk.requiredEcologyReady && HasPublishedTerrain(coordinate))
                continue;

            AdmitPhysicalTraversalDemand(coordinate);
            admissions++;
            if (!hasOwner)
                ownerCreations++;
        }

        float admissionSeconds = Time.realtimeSinceStartup - admissionStartedAt;
        _maximumPredictiveTerrainAdmissionsPerFrame = Mathf.Max(_maximumPredictiveTerrainAdmissionsPerFrame, admissions);
        _maximumPredictiveTerrainOwnerCreationsPerFrame = Mathf.Max(_maximumPredictiveTerrainOwnerCreationsPerFrame, ownerCreations);
        _maximumPredictiveTerrainAdmissionSeconds = Mathf.Max(_maximumPredictiveTerrainAdmissionSeconds, admissionSeconds);
        // note: The shared scheduler sorts and trims after camera/physical demands join this bounded predictive slice.
        if (admissions == 0)
            return;
    }

    private void UpdateTerrainTelemetry()
    {
        // note: Publish bounded queue and preparation occupancy metrics for sampled diagnostics and the runtime stress report.
        // note: Include the active preparation in queue telemetry because acceptance bounds cover all pending physical work.
        _maximumTerrainQueueDepth = Mathf.Max(
            _maximumTerrainQueueDepth,
            PendingTerrainCollisionCount);
        _maximumConcurrentTerrainPreparations = Mathf.Max(_maximumConcurrentTerrainPreparations, _terrainPreparation != null ? 1 : 0);
        _maximumPendingTerrainColliderPublications = Mathf.Max(
            _maximumPendingTerrainColliderPublications,
            _pendingTerrainPublications.Count);

        Vector2 velocity = ResolveTraversalVelocity();
        UpdateTraversalCriticalRequestTracking(velocity);
        if (velocity.sqrMagnitude >= 0.01f)
            _terrainReadyWindowMovementSeconds += Time.unscaledDeltaTime;
        _lastForwardReadyLeadCells = CalculateForwardReadyLeadCells(velocity);
        if (velocity.sqrMagnitude >= 0.01f)
            _minimumForwardReadyLeadCells = Mathf.Min(_minimumForwardReadyLeadCells, _lastForwardReadyLeadCells);

        // note: Count each newly entered unready external cell once as a missed deadline instead of logging every frame at the frontier.
        if (!IsChunkInsideAuthoredTerrain(_currentChunk) && !HasPublishedTerrain(_currentChunk) &&
            _lastMissedTerrainCoordinate != _currentChunk)
        {
            _lastMissedTerrainCoordinate = _currentChunk;
            _missedTerrainDeadlineCount++;
        }

        if (_terrainReadyWindowStartedAt <= 0f)
            _terrainReadyWindowStartedAt = Time.unscaledTime;
        float elapsed = Time.unscaledTime - _terrainReadyWindowStartedAt;
        if (elapsed >= 1f)
        {
            _terrainReadyCellsPerSecond = _terrainReadyWindowMovementSeconds > 0f
                ? _terrainReadyWindowCount / _terrainReadyWindowMovementSeconds
                : 0f;
            if (_terrainReadyWindowMovementSeconds > 0f)
            {
                _terrainReadyForwardCellCount += _terrainReadyWindowCount;
                _terrainReadyMovementSeconds += _terrainReadyWindowMovementSeconds;
                _terrainReadyAverageCellsPerSecond =
                    _terrainReadyForwardCellCount / _terrainReadyMovementSeconds;
                _terrainReadyMaximumCellsPerSecond = Mathf.Max(
                    _terrainReadyMaximumCellsPerSecond,
                    _terrainReadyCellsPerSecond);
            }
            _terrainReadyWindowCount = 0;
            _terrainReadyWindowCoordinates.Clear();
            _terrainReadyWindowMovementSeconds = 0f;
            _terrainReadyWindowStartedAt = Time.unscaledTime;
        }
    }

    private int CalculateForwardReadyLeadCells(Vector2 velocity)
    {
        // note: Count contiguous traversal-ready cells in the current velocity direction so the buffer trend is visible at speed 300.
        if (velocity.sqrMagnitude < 0.01f)
            return 0;
        Vector2 direction = velocity.normalized;
        Vector2Int forward = new Vector2Int(
            Mathf.Abs(direction.x) >= 0.35f ? (direction.x >= 0f ? 1 : -1) : 0,
            Mathf.Abs(direction.y) >= 0.35f ? (direction.y >= 0f ? 1 : -1) : 0);
        if (forward.x != 0 && forward.y != 0)
        {
            // note: Diagonal movement reaches one axis boundary first except at a true corner tie, so readiness follows the actual nearer deadline.
            Vector2Int axisX = _currentChunk + new Vector2Int(forward.x, 0);
            Vector2Int axisZ = _currentChunk + new Vector2Int(0, forward.y);
            bool immediateAxisReady = true;
            if (IsImmediateDiagonalTransition(axisX, velocity))
                immediateAxisReady = IsChunkInsideAuthoredTerrain(axisX) || HasPublishedTerrain(axisX);
            else if (IsImmediateDiagonalTransition(axisZ, velocity))
                immediateAxisReady = IsChunkInsideAuthoredTerrain(axisZ) || HasPublishedTerrain(axisZ);
            if (!immediateAxisReady)
                return 0;
        }
        int lead = 0;
        int radius = TerrainPredictionRadius(velocity);
        for (int step = 1; step <= radius; step++)
        {
            Vector2Int coordinate = _currentChunk + forward * step;
            if (!IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
                break;
            lead++;
        }
        return lead;
    }

    private void RecordTerrainReady(Vector2Int coordinate)
    {
        // note: Record only distinct published collision terrain in the current forward corridor, excluding lateral-only and authored tiles.
        if (_player == null)
            return;
        Vector2 velocity = ResolveTraversalVelocity();
        if (velocity.sqrMagnitude < 0.01f)
            return;
        Vector2 direction = velocity.normalized;
        float size = Mathf.Max(32f, chunkWorldSize);
        Vector2 center = new Vector2(
            WorldGridOrigin + (coordinate.x + 0.5f) * size,
            WorldGridOrigin + (coordinate.y + 0.5f) * size);
        Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
        Vector2 delta = center - playerPosition;
        float along = Vector2.Dot(delta, direction);
        float lateral = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        if (along < -size || lateral > size * 0.75f || !_terrainReadyWindowCoordinates.Add(coordinate))
            return;
        _terrainReadyWindowCount++;
    }

    private void RecordTerrainReadiness(Vector2Int coordinate)
    {
        // note: Close the admission sample exactly when the shared TerrainData and TerrainCollider are published for traversal.
        _terrainRetryAt.Remove(coordinate);
        _terrainPreparationRetryCount.Remove(coordinate);
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk owner) && owner != null &&
            owner.failureReason != null &&
            owner.failureReason.StartsWith("terrain preparation retry budget exhausted", StringComparison.Ordinal))
            owner.failureReason = string.Empty;
        if (!_terrainRequestedAt.TryGetValue(coordinate, out float requestedAt))
            return;
        _terrainRequestedAt.Remove(coordinate);
        float readinessSeconds;
        bool criticalRequest = _terrainCriticalRequestedAt.TryGetValue(coordinate, out float criticalRequestedAt);
        // note: Use the live centerline promotion time when present; otherwise measure from the ordinary physical request.
        if (criticalRequest)
        {
            _terrainCriticalRequestedAt.Remove(coordinate);
            readinessSeconds = Mathf.Max(0f, Time.unscaledTime - criticalRequestedAt);
        }
        else
            readinessSeconds = Mathf.Max(0f, Time.unscaledTime - requestedAt);
        if (readinessSeconds > 5f)
        {
            // note: Preserve the exact late terrain owner and priority age so a readiness regression identifies queue starvation instead of only reporting an aggregate percentile.
            Debug.LogWarning("[YQSemanticChunkStreamer] TERRAIN READINESS OUTLIER " + coordinate +
                " seconds=" + readinessSeconds.ToString("0.000") +
                " requestedAge=" + Mathf.Max(0f, Time.unscaledTime - requestedAt).ToString("0.000") +
                " critical=" + criticalRequest);
        }
        if (_player != null)
        {
            // note: P0/P1 readiness excludes lateral speculation; only the current velocity centerline contributes to the hard latency metric.
            Vector2 velocity = ResolveTraversalVelocity();
            Vector2 direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : Vector2.zero;
            float size = Mathf.Max(32f, chunkWorldSize);
            Vector2 center = new Vector2(
                WorldGridOrigin + (coordinate.x + 0.5f) * size,
                WorldGridOrigin + (coordinate.y + 0.5f) * size);
            Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
            Vector2 delta = center - playerPosition;
            float along = Vector2.Dot(delta, direction);
            float lateral = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
            if (velocity.sqrMagnitude >= 0.01f && (along < -size || lateral > size * 0.5f))
                return;
        }
        _maximumTerrainReadinessSeconds = Mathf.Max(_maximumTerrainReadinessSeconds, readinessSeconds);
        if (_terrainReadinessSamples.Count >= 256)
            _terrainReadinessSamples.RemoveAt(0);
        _terrainReadinessSamples.Add(readinessSeconds);
    }

    private int CountTraversalCriticalTerrainRequests()
    {
        // note: P0/P1 consists of the velocity centerline from current support through the finite predictive horizon; lateral corridor tiles remain speculative.
        if (_player == null)
            return 0;
        Vector2 velocity = ResolveTraversalVelocity();
        if (velocity.sqrMagnitude < 0.01f)
            return 0;
        int count = 0;
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            if (IsTraversalCriticalTerrainCoordinate(_terrainQueue[index], velocity))
                count++;
        }
        if (_terrainPreparation != null && IsTraversalCriticalTerrainCoordinate(_terrainPreparingCoordinate, velocity))
            count++;
        return count;
    }

    private void UpdateTraversalCriticalRequestTracking(Vector2 velocity)
    {
        // note: Rebuild the bounded P0/P1 timestamp set from current scheduler state so direction changes demote stale corridor work immediately.
        if (velocity.sqrMagnitude < 0.01f)
        {
            _terrainCriticalRequestedAt.Clear();
            return;
        }
        _terrainRetryScratch.Clear();
        foreach (KeyValuePair<Vector2Int, float> sample in _terrainCriticalRequestedAt)
        {
            Vector2Int coordinate = sample.Key;
            bool stillPending = coordinate == _terrainPreparingCoordinate ||
                _pendingTerrainPublications.ContainsKey(coordinate) || _terrainQueued.Contains(coordinate);
            if (!stillPending || !IsTraversalCriticalTerrainCoordinate(coordinate, velocity))
                _terrainRetryScratch.Add(coordinate);
        }
        for (int index = 0; index < _terrainRetryScratch.Count; index++)
            _terrainCriticalRequestedAt.Remove(_terrainRetryScratch[index]);
        float now = Time.unscaledTime;
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            Vector2Int coordinate = _terrainQueue[index];
            if (IsTraversalCriticalTerrainCoordinate(coordinate, velocity) && !_terrainCriticalRequestedAt.ContainsKey(coordinate))
                _terrainCriticalRequestedAt[coordinate] = now;
        }
        if (_terrainPreparation != null && IsTraversalCriticalTerrainCoordinate(_terrainPreparingCoordinate, velocity) &&
            !_terrainCriticalRequestedAt.ContainsKey(_terrainPreparingCoordinate))
            _terrainCriticalRequestedAt[_terrainPreparingCoordinate] = now;
    }

    private bool IsTraversalCriticalTerrainCoordinate(Vector2Int coordinate, Vector2 velocity, bool includeImmediateDiagonal = true)
    {
        // note: Use the same world-space centerline tolerance as readiness telemetry so queue and latency metrics describe the same deadline class.
        if (includeImmediateDiagonal && IsImmediateDiagonalTransition(coordinate, velocity))
            return true;
        Vector2 direction = velocity.normalized;
        float size = Mathf.Max(32f, chunkWorldSize);
        Vector2 center = new Vector2(
            WorldGridOrigin + (coordinate.x + 0.5f) * size,
            WorldGridOrigin + (coordinate.y + 0.5f) * size);
        Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
        Vector2 delta = center - playerPosition;
        float along = Vector2.Dot(delta, direction);
        float lateral = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        return along >= -size && along <= (TerrainPredictionRadius(velocity) + 1) * size && lateral <= size * 0.5f;
    }

    private bool IsImmediateDiagonalTransition(Vector2Int coordinate, Vector2 velocity)
    {
        if (!TryResolveImmediateDiagonalTransitions(
                velocity,
                out Vector2Int axisX,
                out Vector2Int axisZ,
                out bool axisXImmediate,
                out bool axisZImmediate))
            return false;
        return (axisXImmediate && coordinate == _currentChunk + axisX) ||
            (axisZImmediate && coordinate == _currentChunk + axisZ);
    }

    private bool TryResolveImmediateDiagonalTransitions(
        Vector2 velocity,
        out Vector2Int axisX,
        out Vector2Int axisZ,
        out bool axisXImmediate,
        out bool axisZImmediate)
    {
        axisX = Vector2Int.zero;
        axisZ = Vector2Int.zero;
        axisXImmediate = false;
        axisZImmediate = false;
        if (_player == null || velocity.sqrMagnitude < 0.01f)
            return false;
        Vector2 direction = velocity.normalized;
        if (Mathf.Abs(direction.x) < 0.35f || Mathf.Abs(direction.y) < 0.35f)
            return false;
        axisX = new Vector2Int(direction.x >= 0f ? 1 : -1, 0);
        axisZ = new Vector2Int(0, direction.y >= 0f ? 1 : -1);
        float size = Mathf.Max(32f, chunkWorldSize);
        Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
        float cellMinX = WorldGridOrigin + _currentChunk.x * size;
        float cellMinZ = WorldGridOrigin + _currentChunk.y * size;
        float distanceToXBoundary = direction.x >= 0f
            ? cellMinX + size - playerPosition.x
            : playerPosition.x - cellMinX;
        float distanceToZBoundary = direction.y >= 0f
            ? cellMinZ + size - playerPosition.y
            : playerPosition.y - cellMinZ;
        float tieTolerance = Mathf.Max(0.05f, size * 0.0005f);
        bool cornerTie = Mathf.Abs(distanceToXBoundary - distanceToZBoundary) <= tieTolerance;
        // note: Classify only the nearer axis as the immediate deadline; a true corner tie keeps both possible boundary owners urgent.
        axisXImmediate = cornerTie || distanceToXBoundary < distanceToZBoundary;
        axisZImmediate = cornerTie || distanceToZBoundary < distanceToXBoundary;
        return true;
    }

    private bool ShouldPreemptTerrainPreparation(Vector2Int coordinate)
    {
        // note: Let the dispatch frame cross its coroutine handoff, but never let a stalled dispatch phase shield lower-priority work indefinitely.
        if (_terrainPreparation != null &&
            coordinate == _terrainPreparingCoordinate &&
            string.Equals(_terrainPreparationPhase, "dispatch", StringComparison.Ordinal) &&
            Time.frameCount <= _terrainPreparationDispatchFrame + 1)
            return false;
        // note: Preempt only unpublished work and only when the queue head has a strictly stronger deadline; an equal-priority owner must finish to prevent cancellation churn.
        if (_terrainQueue.Count > 0 && _terrainQueue[0] != coordinate)
        {
            Vector2 activeVelocity = ResolveTraversalVelocity();
            int activePriority = TerrainPreparationPriority(coordinate, activeVelocity);
            int queuedPriority = TerrainPreparationPriority(_terrainQueue[0], activeVelocity);
            // note: A camera-visible or current-cell request outranks every lower-priority sampler, even after that sampler has started; otherwise terrain=None holes remain in the rendered frustum until speculative work completes.
            // note: The immediate swept boundary is a real readiness deadline too; let it preempt one lower-priority visible sampler so the player never waits behind a remote view tile.
            if (queuedPriority < activePriority && queuedPriority <= 2)
                return RecordTerrainPreparationPreemption(
                    _terrainQueue[0], activePriority, queuedPriority, "deadline");
            // note: Once no hard-view/current deadline is waiting, finish any already-urgent required, centerline, or site sampler to avoid cancellation churn.
            if (activePriority < 5)
                return false;
            // note: Lower values are stronger deadlines; speculative work may yield only to a strictly stronger non-camera deadline.
            if (queuedPriority < activePriority)
                return RecordTerrainPreparationPreemption(
                    _terrainQueue[0], activePriority, queuedPriority, "priority");
        }
        Vector2 velocity = ResolveTraversalVelocity();
        if (velocity.sqrMagnitude < 0.01f || IsTraversalCriticalTerrainCoordinate(coordinate, velocity))
            return false;
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            if (IsTraversalCriticalTerrainCoordinate(_terrainQueue[index], velocity))
                return RecordTerrainPreparationPreemption(
                    _terrainQueue[index],
                    TerrainPreparationPriority(coordinate, velocity),
                    TerrainPreparationPriority(_terrainQueue[index], velocity),
                    "traversal");
        }
        return false;
    }

    private bool RecordTerrainPreparationPreemption(
        Vector2Int preemptingCoordinate,
        int activePriority,
        int preemptingPriority,
        string reason)
    {
        // note: Count once per work identity while keeping the latest trigger available to bounded runtime diagnostics.
        if (_terrainPreparationWorkId != 0 &&
            _lastTerrainPreemptedWorkId != _terrainPreparationWorkId)
        {
            _lastTerrainPreemptedWorkId = _terrainPreparationWorkId;
            _terrainPreemptionCount++;
        }
        _lastTerrainPreemptingCoordinate = preemptingCoordinate;
        _lastTerrainPreemptedPriority = activePriority;
        _lastTerrainPreemptingPriority = preemptingPriority;
        _lastTerrainPreemptionReason = reason ?? string.Empty;
        RecordFixedTerrainObservation("preemptionDecision", preemptingCoordinate, _terrainPreparationWorkId);
        return true;
    }

    private int TerrainPreparationPriority(Vector2Int coordinate, Vector2 velocity)
    {
        // note: Keep current and visible ground first, then collision deadlines that can be reached before speculative turn and corridor work.
        if (coordinate == _currentChunk && !IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
            return 0;
        if (_guaranteedViewDemand.Contains(coordinate) || _publicationVerificationDemand.Contains(coordinate) ||
            _sharedSiteVisibleOwners.Contains(coordinate))
            return 1;
        if (velocity.sqrMagnitude >= 0.01f && IsImmediateTraversalTerrainCoordinate(coordinate, velocity))
            return 2;
        if (velocity.sqrMagnitude >= 0.01f && IsAgedCriticalTerrainCoordinate(coordinate, velocity))
            return 2;
        if (_siteTerrainDependencies.ContainsKey(coordinate) || _siteTerrainHandoffRequests.Contains(coordinate) ||
            _sharedSiteOwnerDemand.Contains(coordinate))
        {
            // note: A required site terrain dependency is structural publication work, not speculative view warming; keep it ahead of lateral hard-view tiles so content cannot strand behind a continuously replenished camera queue.
            return 2;
        }
        if (_semanticViewPredictionArrivalSeconds.TryGetValue(coordinate, out float predictedViewArrivalSeconds) &&
            predictedViewArrivalSeconds <= CriticalTerrainEscalationSeconds)
        {
            // note: Let near-arrival camera terrain reserve the canonical prep lane before it becomes a P1 live-view request.
            return 2;
        }
        if (IsAgedHardViewTerrainCoordinate(coordinate))
            return 3;
        // note: The omnidirectional one-second floor must beat the distant three-second centerline so a sudden reversal never waits behind travel the player may not take.
        if (_provisionalGroundTurnBufferDemand.Contains(coordinate))
            return 3;
        if (IsRequiredCoverageCoordinate(coordinate) &&
            !IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
            return 3;
        // note: Predicted visible tiles outrank broad retention and centerline lookahead so their terrain is ready before they reach the camera.
        if (_semanticViewPredictionArrivalSeconds.ContainsKey(coordinate))
            return 3;
        if (velocity.sqrMagnitude >= 0.01f &&
            IsTraversalCriticalTerrainCoordinate(coordinate, velocity, false))
            return 4;
        if (_hardViewDemand.Contains(coordinate))
            return 4;
        return 5;
    }

    private bool IsAgedHardViewTerrainCoordinate(Vector2Int coordinate)
    {
        // note: Escalate an unfinished preparation-ring request after the existing bounded deadline so camera-entry demand cannot wait behind fresh speculative work.
        if (!_hardViewDemand.Contains(coordinate) ||
            IsChunkInsideAuthoredTerrain(coordinate) ||
            HasPublishedTerrain(coordinate) ||
            !_terrainRequestedAt.TryGetValue(coordinate, out float requestedAt))
            return false;
        return Time.unscaledTime - requestedAt >= CriticalTerrainEscalationSeconds;
    }

    private bool IsAgedCriticalTerrainCoordinate(Vector2Int coordinate, Vector2 velocity)
    {
        // note: Escalation applies only to a live centerline owner whose bounded wait has exceeded the safety budget; fresh camera work keeps its normal priority.
        if (velocity.sqrMagnitude < 0.01f ||
            !IsTraversalCriticalTerrainCoordinate(coordinate, velocity))
            return false;
        float requestedAt;
        if (!_terrainCriticalRequestedAt.TryGetValue(coordinate, out requestedAt) &&
            !_terrainRequestedAt.TryGetValue(coordinate, out requestedAt))
            return false;
        return Time.unscaledTime - requestedAt >= CriticalTerrainEscalationSeconds;
    }

    private float TerrainCriticalRequestedAt(Vector2Int coordinate)
    {
        // note: Use the live centerline promotion time first, then the original physical admission time as a deterministic tie-breaker.
        if (_terrainCriticalRequestedAt.TryGetValue(coordinate, out float criticalRequestedAt))
            return criticalRequestedAt;
        if (_terrainRequestedAt.TryGetValue(coordinate, out float requestedAt))
            return requestedAt;
        return float.MaxValue;
    }

    private bool IsImmediateTraversalTerrainCoordinate(Vector2Int coordinate, Vector2 velocity)
    {
        // note: Only the next boundary owner is an immediate travel deadline; the rest of the predictive corridor must not starve the player's visible view.
        if (_player == null || velocity.sqrMagnitude < 0.01f)
            return false;
        Vector2 direction = velocity.normalized;
        Vector2Int forward = new Vector2Int(
            Mathf.Abs(direction.x) >= 0.35f ? (direction.x >= 0f ? 1 : -1) : 0,
            Mathf.Abs(direction.y) >= 0.35f ? (direction.y >= 0f ? 1 : -1) : 0);
        if (forward == Vector2Int.zero)
            return false;
        if (coordinate == _currentChunk + forward)
            return true;
        if (forward.x != 0 && forward.y != 0 &&
            TryResolveImmediateDiagonalTransitions(
                velocity,
                out Vector2Int axisX,
                out Vector2Int axisZ,
                out bool axisXImmediate,
                out bool axisZImmediate))
        {
            return (axisXImmediate && coordinate == _currentChunk + axisX) ||
                (axisZImmediate && coordinate == _currentChunk + axisZ);
        }
        return false;
    }

    private bool ShouldReserveFrameForTraversalTerrain()
    {
        // note: Critical terrain gets priority, but content/appearance must progress regularly because both are required for traversal publication.
        if (Time.frameCount % 3 == 0)
            return false;
        // note: Any live traversal deadline reserves the frame for pending collision terrain; limiting this to high speed let normal-speed scatter work delay an edge collider.
        Vector2 velocity = ResolveTraversalVelocity();
        if (velocity.sqrMagnitude < 0.01f)
            return false;
        if (_terrainPreparation != null && IsTraversalCriticalTerrainCoordinate(_terrainPreparingCoordinate, velocity))
            return true;
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            if (IsTraversalCriticalTerrainCoordinate(_terrainQueue[index], velocity))
                return true;
        }
        return false;
    }

    private float CalculateReadinessPercentile(float percentile)
    {
        // note: Sort a bounded copy only when diagnostics request a percentile; the streaming hot path never allocates for this report.
        if (_terrainReadinessSamples.Count == 0)
            return 0f;
        _terrainReadinessScratch.Clear();
        _terrainReadinessScratch.AddRange(_terrainReadinessSamples);
        _terrainReadinessScratch.Sort();
        int index = Mathf.Clamp(Mathf.CeilToInt(_terrainReadinessScratch.Count * percentile) - 1, 0, _terrainReadinessScratch.Count - 1);
        return _terrainReadinessScratch[index];
    }

    private void PrioritizeTerrainRequest(Vector2Int coordinate)
    {
        // note: Move an already queued edge destination to the front so terrain preparation follows the player's imminent exit rather than queue insertion order.
        // note: Edge probes can observe a cell before the semantic frontier slice reaches it; create its canonical owner before admitting physical work.
        if (!_chunks.ContainsKey(coordinate))
            GetOrCreateChunk(coordinate);
        if (IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate) || coordinate == _terrainPreparingCoordinate ||
            _pendingTerrainPublications.ContainsKey(coordinate) ||
            (_terrainRetryAt.TryGetValue(coordinate, out float retryAt) && retryAt > Time.unscaledTime))
            return;
        if (!_terrainQueued.Contains(coordinate))
        {
            RequestTerrainForChunk(coordinate);
            return;
        }
        int index = _terrainQueue.IndexOf(coordinate);
        if (index > 0)
        {
            _terrainQueue.RemoveAt(index);
            _terrainQueue.Insert(0, coordinate);
        }
    }

    private void TrimTerrainQueue(bool queueAlreadySorted = false)
    {
        int keepRadius = TerrainRequestRetentionRadius;
        for (int index = _terrainQueue.Count - 1; index >= 0; index--)
        {
            Vector2Int coordinate = _terrainQueue[index];
            int distance = Mathf.Max(Mathf.Abs(coordinate.x - _currentChunk.x), Mathf.Abs(coordinate.y - _currentChunk.y));
            if (distance <= keepRadius || _hardViewDemand.Contains(coordinate) ||
                _publicationVerificationDemand.Contains(coordinate) ||
                _siteTerrainDependencies.ContainsKey(coordinate))
                continue;
            _terrainQueued.Remove(coordinate);
            // note: Remove abandoned admission telemetry with the stale request so a later revisit starts a fresh latency sample.
            _terrainRequestedAt.Remove(coordinate);
            _terrainCriticalRequestedAt.Remove(coordinate);
            _terrainQueue.RemoveAt(index);
            if (coordinate == FixedTerrainTraceTarget)
                RecordFixedTerrainObservation("requestRetired:outsideRetention", coordinate);
        }
        if (_terrainQueue.Count > TerrainQueueCapacity)
        {
            // note: Keep the most urgent bounded requests after sorting; speculative tail work must never evict imminent traversal cells.
            if (!queueAlreadySorted)
                SortTerrainQueue();
            for (int index = TerrainQueueCapacity; index < _terrainQueue.Count; index++)
            {
                Vector2Int trimmed = _terrainQueue[index];
                _terrainRequestedAt.Remove(trimmed);
                _terrainCriticalRequestedAt.Remove(trimmed);
                if (trimmed == FixedTerrainTraceTarget)
                    RecordFixedTerrainObservation("requestTrimmed", trimmed);
            }
            _terrainQueue.RemoveRange(TerrainQueueCapacity, _terrainQueue.Count - TerrainQueueCapacity);
            _terrainQueued.Clear();
            for (int index = 0; index < _terrainQueue.Count; index++)
                _terrainQueued.Add(_terrainQueue[index]);
        }
    }

    private IEnumerator PrepareTerrainRoutine(Vector2Int coordinate, StreamingWorkToken token)
    {
        IEnumerator build = null;
        string preparationFailure = string.Empty;
        bool publicationHandedOff = false;
        try
        {
        // note: A completed immutable prefetch can enter the existing budgeted publication lane immediately; an unfinished sampler still gets a frame boundary.
        bool prefetchedHeightReady = _terrainHeightPrefetches.TryGetValue(coordinate, out Task<float[,]> prefetchedHeight) &&
            prefetchedHeight != null && prefetchedHeight.IsCompleted;
        if (!prefetchedHeightReady || !CanAdvanceAggregateWork("terrainStart"))
            yield return null;
            if (!IsChunkInsideAuthoredTerrain(coordinate) && !HasPublishedTerrain(coordinate))
            {
                // note: Manually advance the terrain builder so upload failures are caught here and cannot strand the scheduler's active-job slot.
                build = CreateExtensionTerrainRoutine(coordinate, token);
                while (true)
                {
                    if (_terrainPreparationPreempted || !IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), token))
                    {
                        RecordFixedTerrainObservation("cancellationOrInvalidationAcknowledged", coordinate, token.workId);
                        yield break;
                    }
                    if (Time.realtimeSinceStartup - _terrainPreparationStartedAt >= TerrainPreparationTimeoutSeconds)
                    {
                        preparationFailure = "terrain preparation timed out after " +
                            TerrainPreparationTimeoutSeconds.ToString("F1") +
                            "s in phase " + (_terrainPreparationPhase ?? "unknown");
                        _terrainPreparationPhase = "timedOut";
                        _terrainHeightPreparationCancellation?.Cancel();
                        break;
                    }
                    if (!CanAdvanceAggregateWork("terrainHeightUpload"))
                    {
                        RecordFixedTerrainObservation("refused:heightBudget", coordinate, token.workId);
                        yield return null;
                        continue;
                    }
                    object yielded = null;
                    bool completed = false;
                    float sliceStarted = Time.realtimeSinceStartup;
                    try
                    {
                        if (!build.MoveNext())
                            completed = true;
                        else
                            yielded = build.Current;
                    }
                    catch (Exception exception)
                    {
                        preparationFailure = "terrain preparation exception: " + exception.Message;
                        Debug.LogError("[YQSemanticChunkStreamer] Terrain preparation failed for " + coordinate + ": " + exception);
                        break;
                    }
                    _lastTerrainSliceSeconds = Time.realtimeSinceStartup - sliceStarted;
                    if (_lastTerrainSliceSeconds > _maximumTerrainSliceSeconds)
                    {
                        _maximumTerrainSliceSeconds = _lastTerrainSliceSeconds;
                        _maximumTerrainSlicePhase = _terrainPreparationPhase ?? string.Empty;
                    }
                    RecordAggregateWorkSlice(sliceStarted, "terrainHeightUpload");
                    if (completed)
                        break;
                    if (yielded is PendingTerrainPublication pendingPublication)
                    {
                        // note: Transfer the completed heightfield to its bounded collider lane so the next cell can use the sole sampler immediately.
                        if (TryStartPendingTerrainPublication(pendingPublication))
                        {
                            float preparationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - _terrainPreparationStartedAt);
                            _terrainPreparationHandoffCount++;
                            _terrainPreparationHandoffTotalSeconds += preparationSeconds;
                            _terrainPreparationHandoffMaximumSeconds = Mathf.Max(_terrainPreparationHandoffMaximumSeconds, preparationSeconds);
                            publicationHandedOff = true;
                        }
                        else
                        {
                            DestroyUnpublishedTerrainPublication(pendingPublication);
                            preparationFailure = "pending collider publication capacity or owner check rejected the completed tile";
                        }
                        break;
                    }
                    if (ReferenceEquals(yielded, ContinueTerrainPreparationWithinFrameBudget))
                        continue;
                    yield return yielded;
                }
                if (!publicationHandedOff && !HasPublishedTerrain(coordinate) && IsTerrainRequestStillRelevant(coordinate) &&
                    IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), token))
                {
                    if (_terrainPreparationPreempted)
                        yield break;
                    // note: Only report a missing tile while its semantic owner is still inside the retained frontier; cancelled far requests are expected cleanup, not an artificial world boundary.
                    if (string.IsNullOrWhiteSpace(preparationFailure))
                        preparationFailure = "terrain preparation produced no tile";
                    RecordTerrainPreparationFailure(coordinate, token.owner, preparationFailure);
                }
            }
        }
        finally
        {
            // note: The manually-driven iterator is not owned by Unity's coroutine scheduler, so dispose it explicitly on cancellation to release unpublished TerrainData.
            (build as IDisposable)?.Dispose();
            // note: A retired iterator may dispose its own data but cannot release a replacement preparation slot.
            if (_terrainPreparationWorkId == token.workId)
            {
                RecordFixedTerrainObservation("preparationExiting", coordinate, token.workId);
                _terrainPreparationWorkId = 0;
                _terrainPreparation = null;
                _terrainPreparingCoordinate = new Vector2Int(int.MinValue, int.MinValue);
                _terrainPreparationStartedAt = 0f;
                _terrainPreparationDispatchFrame = -1;
                _terrainPreparationPhase = string.Empty;
                bool requeue = _terrainPreparationPreempted &&
                    IsGenerationCurrent(coordinate, token.owner, token) && IsTerrainRequestStillRelevant(coordinate);
                _terrainPreparationPreempted = false;
                RecordFixedTerrainObservation("slotReleased", coordinate, token.workId);
                if (requeue)
                    RequestTerrainForChunk(coordinate);
            }
        }
    }

    private bool TryStartPendingTerrainPublication(PendingTerrainPublication publication)
    {
        if (publication == null || publication.terrainObject == null || publication.terrain == null ||
            publication.data == null || _pendingTerrainPublications.Count >= MaximumPendingTerrainColliderPublications ||
            _pendingTerrainPublications.ContainsKey(publication.coordinate) ||
            !IsGenerationCurrent(publication.coordinate, GetRuntimeChunk(publication.coordinate), publication.token) ||
            !IsTerrainRequestStillRelevant(publication.coordinate))
            return false;

        // note: Register before starting the coroutine so a repeated demand sees one canonical owner during its first frame handoff.
        _pendingTerrainPublications.Add(publication.coordinate, publication);
        RecordFixedTerrainObservation("colliderTicketRegistered", publication.coordinate, publication.token.workId);
        _maximumPendingTerrainColliderPublications = Mathf.Max(
            _maximumPendingTerrainColliderPublications,
            _pendingTerrainPublications.Count);
        try
        {
            publication.coroutine = StartCoroutine(CompletePendingTerrainPublicationRoutine(publication));
        }
        catch (Exception exception)
        {
            Debug.LogError("[YQSemanticChunkStreamer] Could not start collider publication for " +
                publication.coordinate + ": " + exception);
        }

        if (publication.coroutine != null)
        {
            // note: Paint independent channels while collider publication waits; appearance startup failure must not destroy a valid terrain ticket.
            YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap appearancePreparation =
                publication.appearancePreparation;
            publication.appearancePreparation = null;
            try
            {
                StartTerrainAppearancePainting(
                    publication.coordinate,
                    publication.data,
                    Mathf.Max(32f, chunkWorldSize),
                    publication.terrain,
                    publication.token,
                    appearancePreparation);
            }
            catch (Exception exception)
            {
                // note: Collider publication remains authoritative and will retry painting after the terrain is published.
                Debug.LogWarning("[YQSemanticChunkStreamer] Could not overlap appearance painting for " +
                    publication.coordinate + ": " + exception.Message);
            }
            return true;
        }

        if (_pendingTerrainPublications.TryGetValue(publication.coordinate, out PendingTerrainPublication current) &&
            ReferenceEquals(current, publication))
            _pendingTerrainPublications.Remove(publication.coordinate);
        DestroyUnpublishedTerrainPublication(publication);
        return false;
    }

    private IEnumerator CompletePendingTerrainPublicationRoutine(PendingTerrainPublication publication)
    {
        IEnumerator steps = PublishPendingTerrainSteps(publication);
        string failure = string.Empty;
        try
        {
            while (steps != null)
            {
                object yielded = null;
                bool completed = false;
                try
                {
                    if (!steps.MoveNext())
                        completed = true;
                    else
                        yielded = steps.Current;
                }
                catch (Exception exception)
                {
                    failure = "terrain collider publication exception: " + exception.Message;
                    Debug.LogError("[YQSemanticChunkStreamer] Collider publication failed for " +
                        publication.coordinate + ": " + exception);
                    break;
                }
                if (completed)
                    break;
                yield return yielded;
            }
        }
        finally
        {
            // note: The wrapper catches iterator errors without surrendering ticket cleanup, retry ownership, or the bounded pending slot.
            (steps as IDisposable)?.Dispose();
            FinishPendingTerrainPublication(publication, failure);
        }
    }

    private IEnumerator PublishPendingTerrainSteps(PendingTerrainPublication publication)
    {
        // note: Preserve a full frame after Terrain.Flush before the one shared transform sync, then preserve a second frame for Unity's collider cook.
        yield return null;
        if (!IsPendingTerrainPublicationCurrent(publication))
            yield break;

        while (_lastTerrainPhysicsSyncFrame == Time.frameCount ||
               !CanAdvanceAggregateWork(
                   "terrainColliderSync",
                   useUrgentTerrainColliderSyncBudget:
                       TerrainPreparationPriority(publication.coordinate, ResolveTraversalVelocity()) <= 3))
            yield return null;
        float syncStartedAt = Time.realtimeSinceStartup;
        _lastTerrainPhysicsSyncFrame = Time.frameCount;
        Physics.SyncTransforms();
        RecordAggregateWorkSlice(syncStartedAt, "terrainColliderSync");
        yield return null;

        if (!IsPendingTerrainPublicationCurrent(publication))
            yield break;
        while (IsPublicationVerificationStageHeld(
            publication.coordinate,
            YQPublicationVerificationStage.TerrainCollision))
        {
            publication.phase = "collisionHold";
            if (!IsPendingTerrainPublicationCurrent(publication))
                yield break;
            yield return null;
        }
        if (!IsPendingTerrainPublicationCurrent(publication))
            yield break;

        publication.phase = "publishing";
        if (publication.terrain == null || publication.data == null ||
            publication.terrain.terrainData != publication.data ||
            !IsTerrainColliderReady(publication.terrain))
            throw new InvalidOperationException("TerrainCollider did not become ready after its synchronized cook frame");
        if (!_chunks.TryGetValue(publication.coordinate, out RuntimeChunk liveOwner) || liveOwner == null)
            yield break;

        MarkTerrainReadiness(publication.coordinate, YQTerrainReadinessState.CollisionReady);
        // note: Publish the exact sampled renderer/collider pair only after owner identity, demand, and physics readiness are rechecked.
        _extendedTerrainTiles[publication.coordinate] = publication.terrain;
        if (!liveOwner.ownedObjects.Contains(publication.terrainObject))
            RegisterOwnedObject(liveOwner, publication.terrainObject);
        if (!liveOwner.generatedObjects.Contains(publication.terrainObject))
        {
            liveOwner.generatedObjects.Add(publication.terrainObject);
            InvalidateRendererValidationCache(liveOwner);
            liveOwner.visualStateKnown = false;
        }
        liveOwner.terrainPublished = true;
        // note: Keep the tile renderer hidden until its independent exact-appearance stage completes; its synchronized collider remains active.
        publication.terrain.enabled = false;
        publication.published = true;
        _terrainProgressVersion++;
        RefreshTerrainNeighbors(publication.coordinate);
        _lastTerrainBuildSeconds = Time.realtimeSinceStartup - publication.startedAt;
        _maximumTerrainBuildSeconds = Mathf.Max(_maximumTerrainBuildSeconds, _lastTerrainBuildSeconds);
        RecordTerrainReady(publication.coordinate);
        RecordTerrainReadiness(publication.coordinate);
        publication.phase = "published";
        SetChunkVisualsEnabled(liveOwner, liveOwner.visualStateKnown && liveOwner.visualState);
        if (publication.appearancePrepared)
        {
            // note: Reveal the renderer only after both paint channels and collider readiness have independently completed.
            publication.terrain.enabled = ShouldExposeTerrainPreview(liveOwner);
            publication.terrain.drawTreesAndFoliage = true;
            liveOwner.appearanceReady = true;
            liveOwner.visualReady = true;
            liveOwner.publicationVersion++;
            _terrainAppearanceRetryAt.Remove(publication.coordinate);
            _terrainAppearanceRetryCount.Remove(publication.coordinate);
            _lifecyclePending = true;
            yield break;
        }
        if (_terrainPainting.ContainsKey(publication.coordinate))
        {
            // note: Keep an in-flight painter started during collider cooking; restarting it here would discard completed paint slices.
            yield break;
        }
        YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparedBiomeAlphamap =
            publication.appearancePreparation;
        publication.appearancePreparation = null;
        StartTerrainAppearancePainting(
            publication.coordinate,
            publication.data,
            Mathf.Max(32f, chunkWorldSize),
            publication.terrain,
            publication.token,
            preparedBiomeAlphamap);
    }

    private bool IsPendingTerrainPublicationCurrent(PendingTerrainPublication publication)
    {
        return publication != null && !publication.cancelled &&
            _pendingTerrainPublications.TryGetValue(publication.coordinate, out PendingTerrainPublication current) &&
            ReferenceEquals(current, publication) &&
            IsGenerationCurrent(publication.coordinate, GetRuntimeChunk(publication.coordinate), publication.token) &&
            IsTerrainRequestStillRelevant(publication.coordinate);
    }

    private void FinishPendingTerrainPublication(PendingTerrainPublication publication, string failure)
    {
        if (publication == null)
            return;
        // note: Any preparation not transferred to the published appearance lane belongs to this ticket and must be released here.
        publication.appearancePreparation?.Dispose();
        publication.appearancePreparation = null;
        if (_pendingTerrainPublications.TryGetValue(publication.coordinate, out PendingTerrainPublication current) &&
            ReferenceEquals(current, publication))
            _pendingTerrainPublications.Remove(publication.coordinate);

        if (!publication.published)
        {
            if (_extendedTerrainTiles.TryGetValue(publication.coordinate, out Terrain mappedTerrain) &&
                ReferenceEquals(mappedTerrain, publication.terrain))
                _extendedTerrainTiles.Remove(publication.coordinate);
            if (_chunks.TryGetValue(publication.coordinate, out RuntimeChunk owner) && owner != null)
            {
                // note: A failed collider ticket invalidates any parallel painter before its TerrainData is destroyed.
                StopTerrainPaintingForChunk(owner);
                owner.generatedObjects.Remove(publication.terrainObject);
                InvalidateRendererValidationCache(owner);
                RemoveOwnedObject(owner, publication.terrainObject);
                if (owner.terrainReadiness < YQTerrainReadinessState.CollisionReady)
                    owner.terrainPublished = false;
            }
            DestroyUnpublishedTerrainPublication(publication);
        }

        if (!publication.published && !publication.cancelled &&
            IsTerrainRequestStillRelevant(publication.coordinate) && !HasPublishedTerrain(publication.coordinate))
        {
            if (!string.IsNullOrWhiteSpace(failure) &&
                IsGenerationCurrent(publication.coordinate, GetRuntimeChunk(publication.coordinate), publication.token))
                RecordTerrainPreparationFailure(publication.coordinate, publication.token.owner, failure);
            else
                RequestTerrainForChunk(publication.coordinate);
        }
        _lifecyclePending = true;
    }

    private void DestroyUnpublishedTerrainPublication(PendingTerrainPublication publication)
    {
        if (publication == null)
            return;
        // note: Cancellation and rejected handoff retire the off-thread map worker with the exact temporary terrain owner.
        publication.appearancePreparation?.Dispose();
        publication.appearancePreparation = null;
        if (publication.published || publication.resourcesDestroyed)
            return;
        publication.resourcesDestroyed = true;
        if (publication.terrain != null)
            publication.terrain.SetNeighbors(null, null, null, null);
        if (publication.terrainObject != null)
            UnityEngine.Object.Destroy(publication.terrainObject);
        if (publication.data != null)
            UnityEngine.Object.Destroy(publication.data);
    }

    private void CancelPendingTerrainPublication(Vector2Int coordinate)
    {
        if (!_pendingTerrainPublications.TryGetValue(coordinate, out PendingTerrainPublication publication) ||
            publication == null)
            return;
        publication.cancelled = true;
        _pendingTerrainPublications.Remove(coordinate);
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk owner) && owner != null)
            StopTerrainPaintingForChunk(owner);
        if (publication.coroutine != null)
            StopCoroutine(publication.coroutine);
        DestroyUnpublishedTerrainPublication(publication);
    }

    private void CancelAllPendingTerrainPublications()
    {
        while (_pendingTerrainPublications.Count > 0)
        {
            Vector2Int coordinate = default;
            foreach (KeyValuePair<Vector2Int, PendingTerrainPublication> pair in _pendingTerrainPublications)
            {
                coordinate = pair.Key;
                break;
            }
            CancelPendingTerrainPublication(coordinate);
        }
    }

    private StreamingWorkToken CaptureStreamingWorkToken(Vector2Int coordinate, bool terrainWork = false)
    {
        GeneratedSemanticWorldAuthorityRecord authority = _plan != null ? _plan.semanticAuthority : null;
        long featureRevision = authority != null ? authority.featureOverlayRevision : 0L;
        string profileId = _world != null && _world.worldIdentity != null ? _world.worldIdentity.ownerProfileId : string.Empty;
        string worldId = _world != null && _world.worldIdentity != null ? _world.worldIdentity.worldId : string.Empty;
        return new StreamingWorkToken
        {
            profileId = profileId ?? string.Empty,
            worldId = worldId ?? string.Empty,
            generationEpoch = _configurationEpoch,
            featureRevision = featureRevision,
            owner = GetRuntimeChunk(coordinate),
            ownerEpoch = GetRuntimeChunk(coordinate) == null ? 0 :
                (terrainWork ? GetRuntimeChunk(coordinate).terrainOwnerEpoch : GetRuntimeChunk(coordinate).contentOwnerEpoch),
            terrainWork = terrainWork,
            workId = ++_streamingWorkVersion
        };
    }

    private IEnumerator GenerateChunkRoutine(Vector2Int coordinate, RuntimeChunk chunk, StreamingWorkToken token)
    {
        // note: Advance the complete nested content iterator stack through one guarded owner so material, POI, and scatter work is timed and cannot strand the single-generation slot.
        if (!IsGenerationCurrent(coordinate, chunk, token))
            yield break;
        Stack<IEnumerator> iterators = new Stack<IEnumerator>();
        iterators.Push(GenerateChunkRoutineCore(coordinate, chunk, token));
        OwnerIteratorProgress progress = chunk.structuralProgress;
        float frameSliceStarted = Time.realtimeSinceStartup;
        try
        {
            while (iterators.Count > 0)
            {
                if (!IsGenerationCurrent(coordinate, chunk, token))
                    yield break;
                // note: A camera/frontier change can make a queued build obsolete while it is yielding; unwind it before it consumes the only content slot needed by the new view.
                if (!IsContentDemandedNow(coordinate))
                {
                    _demandCancelledGenerations.Add(coordinate);
                    yield break;
                }
                progress.Resume();
                progress.ObserveIterator(iterators.Peek());
                // note: Hard-view owners must finish their required structure under high-speed load; only content outside the prepared view runway yields to urgent collision terrain.
                if (ShouldReserveFrameForTraversalTerrain() && !_hardViewDemand.Contains(coordinate))
                {
                    // note: T2/T3 content yields while elevated-speed T1 terrain is pending, then resumes from the same deterministic iterator state.
                    progress.Admitted();
                    progress.Yielded(null);
                    yield return null;
                    frameSliceStarted = Time.realtimeSinceStartup;
                    continue;
                }
                if (!CanAdvanceAggregateWork(
                        "requiredContent",
                        useHardViewContentProgressBudget: _hardViewDemand.Contains(coordinate),
                        liveViewContentProgress: _guaranteedViewDemand.Contains(coordinate),
                        contentProgressCoordinate: coordinate))
                {
                    progress.Denied();
                    yield return null;
                    frameSliceStarted = Time.realtimeSinceStartup;
                    continue;
                }
                progress.Admitted();
                IEnumerator current = null;
                object yielded = null;
                bool completedCurrent = false;
                float contentStepStarted = Time.realtimeSinceStartup;
                try
                {
                    current = iterators.Peek();
                    double stepStartedAt = Time.realtimeSinceStartupAsDouble;
                    bool advanced;
                    try { advanced = current.MoveNext(); }
                    finally { progress.Stepped(stepStartedAt); }
                    if (!advanced)
                    {
                        iterators.Pop();
                        (current as IDisposable)?.Dispose();
                        completedCurrent = true;
                    }
                    else
                        yielded = current.Current;
                    float contentStepSeconds = Time.realtimeSinceStartup - contentStepStarted;
                    if (contentStepSeconds > _maximumContentSliceSeconds)
                        _maximumContentSliceStage = current.GetType().FullName;
                    RecordRequiredContentWorkSlice(contentStepStarted);
                }
                catch (Exception exception)
                {
                    RecordRequiredContentWorkSlice(contentStepStarted);
                    if (!IsGenerationCurrent(coordinate, chunk, token))
                        yield break;
                    CancelEcologyForFailedStructure(chunk);
                    chunk.state = YQSemanticChunkLifecycle.Failed;
                    DestroyGeneratedObjects(chunk);
                    chunk.failureReason = "required content generation: " + exception.Message;
                    Debug.LogError("[YQSemanticChunkStreamer] Chunk generation failed for " + coordinate + ": " + exception);
                    _queuedCount = _queue.Count;
                    ApplyLifecycle();
                    yield break;
                }

                if (!completedCurrent && yielded is IEnumerator nested)
                {
                    // note: Nested Unity iterators are pushed immediately so their own material and placement slices are measured instead of hidden behind a parent MoveNext call.
                    iterators.Push(nested);
                }
                if (completedCurrent || yielded is IEnumerator)
                {
                    // note: Nested completion/push is normally cheap, but a long chain is split once its cumulative frame slice reaches the cooperative target.
                    float accumulatedSeconds = Time.realtimeSinceStartup - frameSliceStarted;
                    if (accumulatedSeconds >= ContentSliceTargetSeconds)
                    {
                        _lastContentSliceSeconds = accumulatedSeconds;
                        _maximumContentSliceSeconds = Mathf.Max(_maximumContentSliceSeconds, _lastContentSliceSeconds);
                        progress.Yielded(null);
                        yield return null;
                        progress.Resume();
                        frameSliceStarted = Time.realtimeSinceStartup;
                        if (!IsGenerationCurrent(coordinate, chunk, token))
                            yield break;
                    }
                    continue;
                }
                // note: Only an outward yield or complete stack marks a real Unity frame boundary; all nested completion and parent-resume work before it belongs to one measured slice.
                _lastContentSliceSeconds = Time.realtimeSinceStartup - frameSliceStarted;
                _maximumContentSliceSeconds = Mathf.Max(_maximumContentSliceSeconds, _lastContentSliceSeconds);
                progress.Yielded(yielded);
                yield return yielded;
                progress.Resume();
                frameSliceStarted = Time.realtimeSinceStartup;
                if (!IsGenerationCurrent(coordinate, chunk, token))
                    yield break;
            }
            // note: Include the final no-yield lifecycle and persistence tail in the same frame-budget metric.
            _lastContentSliceSeconds = Time.realtimeSinceStartup - frameSliceStarted;
            _maximumContentSliceSeconds = Mathf.Max(_maximumContentSliceSeconds, _lastContentSliceSeconds);
        }
        finally
        {
            progress.Admitted();
            progress.Resume();
            // note: A cancelled content build disposes every nested iterator it owns before the chunk can be retried or unloaded.
            while (iterators.Count > 0)
                (iterators.Pop() as IDisposable)?.Dispose();
            ReleaseStaleGeneration(coordinate, chunk, token);
        }
    }

    private IEnumerator GenerateChunkRoutineCore(Vector2Int coordinate, RuntimeChunk chunk, StreamingWorkToken token)
    {
        if (!IsGenerationCurrent(coordinate, chunk, token))
            yield break;
        GameObject root = null;
        Terrain terrainForChunk = null;
        float startedAt = Time.unscaledTime;
        GeneratedRegionRecord region = null;
        GeneratedRegionAssetPaletteRecord palette = null;
        YQRuntimeWorldAssetRegistry registry = null;
        // note: Start canonical structure once its complete hidden heightfield is sampleable; collider synchronization remains an independent ground-readiness gate.
        yield return EnsureTerrainForContentMaterializationRoutine(coordinate);
        if (!IsGenerationCurrent(coordinate, chunk, token))
            yield break;
        try
        {
            terrainForChunk = ResolveTerrainForContentMaterialization(coordinate);
            if (terrainForChunk == null && !IsChunkInsideAuthoredTerrain(coordinate))
                throw new InvalidOperationException("no deterministic terrain continuation was available");
            if (terrainForChunk != null && terrainForChunk != _terrain &&
                !chunk.ownedObjects.Contains(terrainForChunk.gameObject))
            {
                // note: Register already-active terrain without forcing the lifecycle pass to rescan it object by object.
                RegisterOwnedObject(chunk, terrainForChunk.gameObject);
                chunk.generatedObjects.Add(terrainForChunk.gameObject);
                InvalidateRendererValidationCache(chunk);
                chunk.visualStateKnown = false;
            }
            region = ResolveRegion(chunk.record.parentRegionId);
            palette = ResolvePalette(region, chunk.record.biome, !IsChunkInsideAuthoredTerrain(coordinate));
            registry = YQRuntimeWorldAssetRegistry.Instance;
            if (region == null || palette == null || registry == null)
                throw new InvalidOperationException("semantic chunk has no compatible region palette or runtime asset registry");

            root = new GameObject("Generated_SemanticChunk_" + coordinate.x + "_" + coordinate.y);
            root.transform.SetParent(_worldRoot, false);
            // note: Keep the generated hierarchy hidden until terrain appearance and activation publish the complete required owner.
            root.SetActive(false);
            // note: A replacement root must wait for fresh structural and overlay receipts before its concurrently prepared ecology can publish.
            chunk.requiredContentReady = false;
            chunk.overlayReady = false;
            chunk.requiredEcologyReady = false;
            chunk.decorativeComplete = false;
            // note: A replacement root starts a new diagnostic publication lifetime; old stage ages cannot certify this owner.
            chunk.contentReadyAt = -1f;
            chunk.ecologyStartedAt = -1f;
            chunk.generationRoot = root;
            chunk.generatedObjects.Add(root);
            InvalidateRendererValidationCache(chunk);
            chunk.visualStateKnown = false;
        }
        catch (Exception exception)
        {
            if (root != null)
            {
                root.SetActive(false);
                Destroy(root);
            }
            DestroyGeneratedObjects(chunk);
            chunk.state = YQSemanticChunkLifecycle.Failed;
            chunk.failureReason = "terrain continuation setup: " + exception.Message;
            Debug.LogWarning("[YQSemanticChunkStreamer] Chunk generation deferred for " + coordinate + ": " + exception.Message);
            _queuedCount = _queue.Count;
            ApplyLifecycle();
            yield break;
        }

        bool insideAuthoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
        // note: Accepted V2 routes, water, and non-origin sites must also materialize over the authored origin terrain; the origin Terrain is reused, but its accepted physical features cannot be skipped.
        bool materializeContinuationFeatures = !insideAuthoredTerrain ||
            YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan);
        if (_hardViewDemand.Contains(coordinate) || IsAppearanceDeadlineDemand(coordinate))
        {
            // note: Visible and predicted-arrival ecology starts beside route/site materialization, while its receipt remains gated on structure and overlays.
            StartDecorativeGeneration(
                coordinate,
                chunk,
                root,
                terrainForChunk != null ? terrainForChunk : _terrain,
                region,
                palette,
                registry,
                token);
        }
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        if (!insideAuthoredTerrain)
        {
            // note: Detailed per-cell success logs are restricted to standalone development builds; editor telemetry is already captured by the bounded summary and verification report.
            string admittedFeatureIds = chunk.record != null && chunk.record.featureIds != null
                ? string.Join(",", chunk.record.featureIds)
                : string.Empty;
            Debug.Log("[YQSemanticChunkStreamer] PHYSICAL CELL ADMITTED " + coordinate +
                " featureIds=" + admittedFeatureIds);
        }
#endif

        int structuralObjects = 0;
        YQPreparedSpatialMaterializationV2 acceptedProjection = null;
        yield return YQContinuousWorldFeatureMaterializer.BuildCellRoutine(
            root.transform,
            terrainForChunk != null ? terrainForChunk : _terrain,
            _plan,
            chunk.record,
            palette,
            registry,
            Mathf.Max(32f, chunkWorldSize),
            materializeContinuationFeatures,
            count => structuralObjects = count,
            RecordMaterializationSubstage,
            prepared => acceptedProjection = prepared,
            _acceptedSpatialProjection);
        if (!IsGenerationCurrent(coordinate, chunk, token))
            yield break;
        // note: Apply the same accepted structural contract inside and outside the authored terrain so origin cells cannot publish as empty while their V2 route/site/water identities remain active.
        bool acceptedStructuralDemand = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan) &&
            YQContinuousWorldFeatureMaterializer.HasAcceptedStructuralDemand(
                _plan,
                chunk.record,
                coordinate,
                Mathf.Max(32f, chunkWorldSize),
                terrainForChunk != null ? terrainForChunk : _terrain,
                acceptedProjection);
        string missingAcceptedFeatureId = string.Empty;
        bool acceptedIdentityWithoutProjection = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan) &&
            YQContinuousWorldFeatureMaterializer.HasAcceptedStructuralIdentityWithoutProjection(
                _plan,
                chunk.record,
                coordinate,
                Mathf.Max(32f, chunkWorldSize),
                out missingAcceptedFeatureId,
                acceptedProjection);
        if (acceptedIdentityWithoutProjection)
        {
            // note: Do not publish a wilderness owner when persisted route/water identity has lost its accepted physical projection.
            CancelEcologyForFailedStructure(chunk);
            chunk.requiredContentReady = false;
            chunk.state = YQSemanticChunkLifecycle.Failed;
            chunk.failureReason = "accepted feature projection missing: " + missingAcceptedFeatureId;
            Debug.LogError("[YQSemanticChunkStreamer] ACCEPTED FEATURE PROJECTION MISSING for " +
                coordinate + " featureId=" + missingAcceptedFeatureId);
            _queuedCount = _queue.Count;
            ApplyLifecycle();
            yield break;
        }
        if (structuralObjects < 0 || (acceptedStructuralDemand && structuralObjects <= 0))
        {
            // note: An accepted route, water feature, or site may never be relabeled as empty wilderness when its physical materializer produced nothing.
            CancelEcologyForFailedStructure(chunk);
            chunk.requiredContentReady = false;
            chunk.state = YQSemanticChunkLifecycle.Failed;
            chunk.failureReason = "accepted structural materialization returned " + structuralObjects;
            Debug.LogError("[YQSemanticChunkStreamer] REQUIRED CONTENT MISSING for " + coordinate +
                "; accepted structural demand produced zero physical objects.");
            _queuedCount = _queue.Count;
            ApplyLifecycle();
            yield break;
        }
        // note: Structural content is published independently from terrain appearance and decorative scatter, so either completion order is safe.
        chunk.requiredContentReady = !acceptedStructuralDemand || structuralObjects > 0;
        if (chunk.requiredContentReady)
            chunk.contentReadyAt = Time.unscaledTime;
        // note: Register and publish the required owner before optional ecology scatter so routes, crossings, sites, and overlays do not wait on decoration.
        BindGeneratedFeatureTargets(chunk, root);
        ApplyFeatureOverlays(chunk, root);
        if (!chunk.overlayReady)
        {
            // note: Do not publish structural content while an applicable persisted overlay is still unapplied; leave an explicit owner failure for recovery diagnostics.
            CancelEcologyForFailedStructure(chunk);
            chunk.requiredContentReady = false;
            chunk.state = YQSemanticChunkLifecycle.Failed;
            chunk.failureReason = "applicable feature overlay was not applied";
            Debug.LogError("[YQSemanticChunkStreamer] FEATURE OVERLAY PUBLICATION FAILED " + coordinate);
            _queuedCount = _queue.Count;
            ApplyLifecycle();
            yield break;
        }
        RegisterOwnedObject(chunk, root);
        chunk.physicalRepresentation = true;
        chunk.requiredEcologyReady = false;
        chunk.decorativeComplete = false;
        chunk.visualReady = chunk.appearanceReady;
        chunk.activationComplete = false;
        chunk.publicationVersion++;
        chunk.state = YQSemanticChunkLifecycle.Generated;
        chunk.lastGenerationSeconds = Time.unscaledTime - startedAt;
        // note: The just-completed overlay pass covered the prepared ecology under this root; publish its guarded receipt now instead of waiting another coroutine frame.
        TryPublishPreparedRequiredEcology(coordinate, chunk, overlaysAlreadyApplied: true);
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
        // note: Standalone development logs retain accepted feature identity without flooding the editor during continuous streaming.
        string acceptedFeatureSummary = chunk.record != null && chunk.record.featureIds != null
            ? string.Join(",", chunk.record.featureIds)
            : string.Empty;
        Debug.Log("[YQSemanticChunkStreamer] REQUIRED OWNER PUBLISHED " + coordinate +
            " structuralObjects=" + structuralObjects +
            " acceptedDemand=" + acceptedStructuralDemand +
            " featureIds=" + acceptedFeatureSummary +
            " seconds=" + chunk.lastGenerationSeconds.ToString("0.000"));
#endif

        // note: Start remaining ecology after the required owner is admitted; hard-view cells may already have prepared its minimum beside materialization.
        _requiredEcologyRetryAt.Remove(coordinate);
        StartDecorativeGeneration(
                coordinate,
                chunk,
                root,
                terrainForChunk != null ? terrainForChunk : _terrain,
                region,
                palette,
                registry,
                token);

        _queuedCount = _queue.Count;
        ApplyLifecycle();
    }

    private void StartDecorativeGeneration(
        Vector2Int coordinate,
        RuntimeChunk chunk,
        GameObject root,
        Terrain terrain,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        StreamingWorkToken token)
    {
        if (!IsGenerationCurrent(coordinate, chunk, token) || chunk.decorativeWorkId != 0)
            return;
        // note: Register before starting so synchronous failure cannot leave a completed coroutine advertised as live work.
        long workId = ++_streamingWorkVersion;
        chunk.decorativeWorkId = workId;
        chunk.ecologyStartedAt = Time.unscaledTime;
        InvalidateRendererValidationCache(chunk);
        Coroutine scatter = StartCoroutine(GenerateDecorativeScatterRoutine(
            coordinate, chunk, root, terrain, region, palette, registry, token, workId));
        if (IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
            chunk.decorativeGeneration = scatter;
    }

    private bool IsDecorativeWorkCurrent(Vector2Int coordinate, RuntimeChunk chunk, StreamingWorkToken token, long workId)
    {
        return IsGenerationCurrent(coordinate, chunk, token) && chunk.decorativeWorkId == workId;
    }

    private void CancelEcologyForFailedStructure(RuntimeChunk chunk)
    {
        if (chunk == null || chunk.requiredEcologyReady)
            return;
        // note: A hard-view ecology worker may prepare under the hidden content root; a failed structural owner revokes that unpublished work with the root.
        StopDecorativeGeneration(chunk);
    }

    private void StopDecorativeGeneration(RuntimeChunk chunk)
    {
        chunk.ecologyProgress.Admitted();
        chunk.ecologyProgress.Resume();
        Coroutine scatter = chunk.decorativeGeneration;
        GameObject uncommittedRoot = !chunk.requiredEcologyReady ? chunk.decorativeRoot : null;
        // note: Unity cancellation is not guaranteed to dispose the iterator; release its captured slot before retiring the registration.
        ReleaseDecorativeSlot(chunk, chunk.decorativeSlotWorkId);
        // note: Disposal may clean up its captured root, but must not publish retries or clear a replacement registration.
        chunk.decorativeWorkId = 0;
        chunk.pendingRequiredEcology = null;
        InvalidateRendererValidationCache(chunk);
        chunk.decorativeGeneration = null;
        chunk.decorativeWorkPhase = string.Empty;
        if (scatter != null)
            StopCoroutine(scatter);
        // note: Unity may stop a coroutine without running its finally block; explicitly discard only ecology that never published its required receipt.
        if (uncommittedRoot != null)
        {
            uncommittedRoot.SetActive(false);
            Destroy(uncommittedRoot);
            if (chunk.decorativeRoot == uncommittedRoot)
                chunk.decorativeRoot = null;
        }
    }

    private void ReleaseDecorativeSlot(RuntimeChunk chunk, long workId)
    {
        // note: A stopped iterator's finally may still run; it cannot release another attempt's slot or decrement the count twice.
        if (workId == 0 || chunk.decorativeSlotWorkId != workId)
            return;
        // note: Slot waits must not inflate a previously open aggregate-budget denial interval.
        chunk.ecologyProgress.Admitted();
        chunk.ecologyProgress.Resume();
        chunk.decorativeSlotWorkId = 0;
        _activeDecorativeScatters = Mathf.Max(0, _activeDecorativeScatters - 1);
    }

    private void SetEcologyWorkPhase(RuntimeChunk chunk, string phase)
    {
        if (string.Equals(chunk.decorativeWorkPhase, phase, StringComparison.Ordinal))
            return;
        // note: Record real stage age without restarting the work or changing scheduler priority.
        chunk.decorativeWorkPhase = phase;
        chunk.decorativePhaseStartedAt = Time.unscaledTime;
    }

    private string DescribeEcologyWorkers()
    {
        // note: Diagnostics enumerate only the bounded acquired slots and one waiting visible owner.
        System.Text.StringBuilder description = new System.Text.StringBuilder();
        bool includedWaiter = false;
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            RuntimeChunk chunk = pair.Value;
            if (chunk.decorativeWorkId == 0)
                continue;
            if (chunk.decorativeSlotWorkId == 0)
            {
                if (includedWaiter || !_hardViewDemand.Contains(pair.Key))
                    continue;
                includedWaiter = true;
            }
            description.Append(pair.Key).Append(':').Append(chunk.decorativeWorkId)
                .Append('/').Append(chunk.decorativeSlotWorkId).Append(':').Append(chunk.decorativeWorkPhase)
                .Append(':').Append((Time.unscaledTime - chunk.decorativePhaseStartedAt).ToString("0.0")).Append(';');
        }
        return description.ToString();
    }

    private void TryPublishPreparedRequiredEcology(
        Vector2Int coordinate, RuntimeChunk chunk, bool overlaysAlreadyApplied = false)
    {
        PendingRequiredEcologyPublication pending = chunk.pendingRequiredEcology;
        // note: A shared receipt never authorizes stale work, missing required content, or an unpublished overlay stage.
        if (pending == null || pending.published || pending.failure != null || pending.result == int.MinValue ||
            !IsDecorativeWorkCurrent(coordinate, chunk, pending.token, pending.workId) ||
            !chunk.requiredContentReady || !chunk.overlayReady)
            return;
        if (IsPublicationVerificationStageHeld(coordinate, YQPublicationVerificationStage.RequiredEcology))
        {
            SetEcologyWorkPhase(chunk, "verificationHold");
            return;
        }
        if (ConsumePublicationVerificationFailure(coordinate, YQPublicationVerificationStage.RequiredEcology))
            pending.failure = "verification ecology fault";
        else if (pending.result <= 0)
            pending.failure = pending.providerFailure ?? "canonical ecology returned no accepted required layer";
        if (pending.failure != null)
            return;
        try
        {
            if (pending.root == null)
                throw new InvalidOperationException("required ecology content root is unavailable");
            // note: Structural completion has already bound and replayed this same hierarchy without yielding; the ecology-first completion path still performs its own replay.
            if (!overlaysAlreadyApplied)
            {
                BindGeneratedFeatureTargets(chunk, pending.root);
                ApplyFeatureOverlays(chunk, pending.root);
            }
            if (!chunk.overlayReady)
                pending.failure = "required ecology overlay was not applied";
        }
        catch (Exception exception)
        {
            pending.failure = exception.Message;
        }
        if (pending.failure != null)
            return;
        pending.published = true;
        chunk.requiredEcologyReady = true;
        chunk.publicationVersion++;
        _requiredEcologyRetryAt.Remove(coordinate);
        _requiredEcologyRetryCount.Remove(coordinate);
        _lifecyclePending = true;
    }

    private IEnumerator GenerateDecorativeScatterRoutine(
        Vector2Int coordinate,
        RuntimeChunk chunk,
        GameObject root,
        Terrain terrain,
        GeneratedRegionRecord region,
        GeneratedRegionAssetPaletteRecord palette,
        YQRuntimeWorldAssetRegistry registry,
        StreamingWorkToken token,
        long workId)
    {
        Stack<IEnumerator> iterators = new Stack<IEnumerator>();
        GameObject scatterRoot = null;
        // note: The same attempt-local receipt is read by this coroutine's finally and by the structural completion handoff.
        PendingRequiredEcologyPublication pending = new PendingRequiredEcologyPublication
        {
            token = token,
            workId = workId,
            root = root
        };
        chunk.pendingRequiredEcology = pending;
        int spawned = -1;
        string failure = null;
        float startedAt = Time.unscaledTime;
        try
        {
            SetEcologyWorkPhase(chunk, "slot");
            while (IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
            {
                // note: Keep the existing production worker limits; held work retains its normal slot.
                if (CanAcquireDecorativeSlot(coordinate))
                    break;
                yield return null;
            }
            if (!IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
                yield break;
            chunk.decorativeSlotWorkId = workId;
            _activeDecorativeScatters++;
            _maximumConcurrentDecorativeScatters = Mathf.Max(_maximumConcurrentDecorativeScatters, _activeDecorativeScatters);
            try
            {
                if (root == null)
                    throw new InvalidOperationException("required ecology content root is unavailable");
                scatterRoot = new GameObject("Generated_Ecology_" + coordinate.x + "_" + coordinate.y);
                scatterRoot.transform.SetParent(root.transform, false);
                chunk.decorativeRoot = scatterRoot;
                // note: Provider callbacks only return local results; the guarded owner below is the publication authority.
                iterators.Push(YQGeneratedWorldEnvironment.BuildSemanticChunkScatterRoutine(
                    scatterRoot.transform, terrain, _plan, chunk.record, region, palette, registry,
                    Mathf.Max(32f, chunkWorldSize), count => spawned = count, count => pending.result = count,
                    reason => pending.providerFailure = reason));
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }
            if (failure != null)
                yield break;

            while (iterators.Count > 0 || (!pending.published && pending.result != int.MinValue))
            {
                if (!IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
                    yield break;

                // note: Publish an already-computed minimum first; otherwise hand a slot to runnable ecology with an earlier camera deadline.
                if (!pending.published && pending.result == int.MinValue &&
                    ShouldYieldEcologyToEarlierDemand(coordinate))
                {
                    SetEcologyWorkPhase(chunk, "waitingForEarlierVisibleEcology");
                    ReleaseDecorativeSlot(chunk, workId);
                    yield return null;
                    continue;
                }

                if (!pending.published && pending.result != int.MinValue)
                {
                    if (!chunk.requiredContentReady || !chunk.overlayReady)
                    {
                        // note: Hold the prepared habitat without occupying an ecology slot until accepted structures and durable overlays are publishable.
                        SetEcologyWorkPhase(chunk, "waitingForRequiredOwner");
                        ReleaseDecorativeSlot(chunk, workId);
                        yield return null;
                        continue;
                    }
                    // note: Either completion order uses the same token, overlay, verification and accepted-result gates.
                    TryPublishPreparedRequiredEcology(coordinate, chunk);
                    failure = pending.failure;
                    if (failure != null)
                        yield break;
                    if (!pending.published)
                    {
                        yield return null;
                        continue;
                    }
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
                    Debug.Log("[YQSemanticChunkStreamer] REQUIRED ECOLOGY PUBLISHED " + coordinate +
                        " objects=" + pending.result +
                        " seconds=" + (Time.unscaledTime - startedAt).ToString("0.000"));
#endif
                    if (iterators.Count == 0)
                        break;
                }

                if (pending.published && HasUnreadyHardViewEcology(coordinate))
                {
                    // note: Keep this chunk's unchanged target-count dressing iterator alive but release its slot while other camera-visible cells publish their own minimum ecology.
                    SetEcologyWorkPhase(chunk, "optionalAfterRequired");
                    ReleaseDecorativeSlot(chunk, workId);
                    yield return null;
                    continue;
                }
                if (chunk.decorativeSlotWorkId != workId)
                {
                    // note: Resume the same deterministic iterator only after it regains a bounded slot released for higher-priority camera work.
                    while (IsDecorativeWorkCurrent(coordinate, chunk, token, workId) &&
                        !CanAcquireDecorativeSlot(coordinate))
                    {
                        SetEcologyWorkPhase(chunk, "optionalSlot");
                        yield return null;
                    }
                    if (!IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
                        yield break;
                    chunk.decorativeSlotWorkId = workId;
                    _activeDecorativeScatters++;
                    _maximumConcurrentDecorativeScatters = Mathf.Max(
                        _maximumConcurrentDecorativeScatters,
                        _activeDecorativeScatters);
                }
                // note: Let predicted hard-view ecology use its preparation runway so required habitat is published before the camera reaches the cell.
                if (ShouldReserveFrameForTraversalTerrain() && !_hardViewDemand.Contains(coordinate))
                {
                    SetEcologyWorkPhase(chunk, "terrainPriority");
                    chunk.ecologyProgress.Admitted();
                    chunk.ecologyProgress.Yielded(null);
                    yield return null;
                    chunk.ecologyProgress.Resume();
                    continue;
                }
                // note: Flatten nested iterators so exceptions and cancellation remain inside this work identity.
                SetEcologyWorkPhase(chunk, iterators.Peek().GetType().Name);
                if (!TryAdvancePublicationIterator(
                    iterators,
                    out object yielded,
                    out bool frameYield,
                    out failure,
                    budgetStage: !pending.published && _hardViewDemand.Contains(coordinate)
                        ? "requiredHardViewEcology"
                        : null,
                    progress: chunk.ecologyProgress))
                    yield break;
                if (frameYield)
                {
                    if (yielded != null)
                        SetEcologyWorkPhase(chunk, "yield:" + yielded.GetType().Name);
                    yield return yielded;
                    chunk.ecologyProgress.Resume();
                }
            }

            if (!IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
                yield break;
            if (!pending.published)
            {
                failure = "ecology provider completed without a required-layer receipt";
                yield break;
            }
            try
            {
                BindGeneratedFeatureTargets(chunk, root);
                ApplyFeatureOverlays(chunk, root);
                if (!chunk.overlayReady)
                    failure = "applicable ecology overlay was not applied";
            }
            catch (Exception exception)
            {
                failure = exception.Message;
            }
            if (spawned < 0)
                failure = failure ?? "optional ecology did not complete";
            if (failure != null)
            {
                yield break;
            }
            chunk.decorativeComplete = true;
            chunk.publicationVersion++;
            _lifecyclePending = true;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
            Debug.Log("[YQSemanticChunkStreamer] DECORATION COMPLETE " + coordinate +
                " objects=" + spawned +
                " seconds=" + (Time.unscaledTime - startedAt).ToString("0.000"));
#endif
        }
        finally
        {
            string disposalFailure = DisposePublicationIterators(iterators);
            failure = failure ?? pending.failure ?? disposalFailure;
            ReleaseDecorativeSlot(chunk, workId);
            // note: Cleanup owns only this captured attempt's root, never whichever root a replacement has registered.
            if (!pending.published && scatterRoot != null)
            {
                scatterRoot.SetActive(false);
                Destroy(scatterRoot);
            }
            if (IsDecorativeWorkCurrent(coordinate, chunk, token, workId))
            {
                if (!pending.published)
                {
                    if (chunk.decorativeRoot == scatterRoot)
                        chunk.decorativeRoot = null;
                    chunk.requiredEcologyReady = false;
                    chunk.decorativeComplete = false;
                    if (failure != null)
                        ScheduleRequiredEcologyRetry(coordinate, chunk, failure);
                    else if (IsContentDemandedNow(coordinate))
                        _requiredEcologyRetryAt[coordinate] = Time.unscaledTime + RequiredEcologyRetryDelaySeconds;
                }
                else if (failure != null)
                {
                    chunk.decorativeComplete = false;
                    Debug.LogWarning("[YQSemanticChunkStreamer] OPTIONAL ECOLOGY INCOMPLETE " + coordinate + ": " + failure);
                }
                chunk.decorativeWorkId = 0;
                if (ReferenceEquals(chunk.pendingRequiredEcology, pending))
                    chunk.pendingRequiredEcology = null;
                InvalidateRendererValidationCache(chunk);
                chunk.decorativeGeneration = null;
                chunk.decorativeWorkPhase = string.Empty;
                _lifecyclePending = true;
            }
        }
    }

    private bool TryAdvancePublicationIterator(
        Stack<IEnumerator> iterators, out object yielded, out bool frameYield, out string failure,
        bool enforceAggregateBudget = true,
        string budgetStage = null,
        OwnerIteratorProgress progress = null,
        Vector2Int? appearanceCoordinate = null)
    {
        progress?.Resume();
        if (progress != null && iterators.Count > 0)
            progress.ObserveIterator(iterators.Peek());
        yielded = null;
        frameYield = false;
        failure = null;
        string effectiveBudgetStage = string.IsNullOrEmpty(budgetStage)
            ? "appearanceOrEcology"
            : budgetStage;
        int previousAppearanceReservedFrame = _reservedHardViewAppearanceFrame;
        float appearanceBudgetSpent = _aggregateFrameWorkSeconds;
        bool budgetAdmitted = !enforceAggregateBudget || CanAdvanceAggregateWork(
            effectiveBudgetStage, appearanceCoordinate: appearanceCoordinate);
        if (appearanceCoordinate.HasValue)
            RecordAppearanceBudgetObservation(appearanceCoordinate.Value, progress, budgetAdmitted,
                previousAppearanceReservedFrame, appearanceBudgetSpent);
        if (!budgetAdmitted)
        {
            // note: Pause all nested painter/ecology stacks at the same frame deadline instead of granting each worker a separate slice.
            progress?.Denied();
            frameYield = true;
            return true;
        }
        progress?.Admitted();
        float publicationSliceStartedAt = Time.realtimeSinceStartup;
        IEnumerator current = null;
        bool succeeded = true;
        try
        {
            current = iterators.Peek();
            double stepStartedAt = progress == null ? 0d : Time.realtimeSinceStartupAsDouble;
            bool advanced;
            try { advanced = current.MoveNext(); }
            finally
            {
                if (progress != null)
                {
                    progress.Stepped(stepStartedAt);
                    // note: Capture the provider marker before another owner advances and overwrites its shared diagnostic value.
                    progress.providerSubstage = current.GetType().Name.IndexOf(
                        "PaintStreamedDetailRoutine", StringComparison.Ordinal) >= 0
                        ? YQGeneratedWorldEnvironment.LastStreamedDetailStep
                        : YQGeneratedWorldEnvironment.LastSemanticChunkScatterStep;
                }
            }
            if (!advanced)
            {
                iterators.Pop();
                (current as IDisposable)?.Dispose();
            }
            else if (current.Current is IEnumerator nested)
                iterators.Push(nested);
            else
            {
                yielded = current.Current;
                frameYield = true;
                progress?.Yielded(yielded);
            }
        }
        catch (Exception exception)
        {
            failure = exception.ToString();
            succeeded = false;
        }
        float publicationSliceSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - publicationSliceStartedAt);
        if (publicationSliceSeconds > _maximumPublicationSliceSeconds)
        {
            _maximumPublicationSliceSeconds = publicationSliceSeconds;
            // note: Keep the owning iterator visible in the worst-slice receipt so a nested repair can be traced to its exact synchronous step.
            string stage = current != null ? current.GetType().FullName : "unknown publication iterator";
            if (current != null && stage.IndexOf("RepairMaterialHierarchyRoutine", System.StringComparison.Ordinal) >= 0)
                stage += " [" + YQRuntimeUrpMaterialRepair.LastRepairRoutineStep + "]";
            else if (current != null && stage.IndexOf("BuildSemanticChunkScatterRoutine", System.StringComparison.Ordinal) >= 0)
                stage += " [" + YQGeneratedWorldEnvironment.LastSemanticChunkScatterStep + "]";
            else if (current != null && stage.IndexOf("BuildRoadsideDressingRoutine", System.StringComparison.Ordinal) >= 0)
                stage += " [" + YQGeneratedWorldEnvironment.LastRoadsideDressingStep + "]";
            else if (current != null && stage.IndexOf("BuildShorelineDressingRoutine", System.StringComparison.Ordinal) >= 0)
                stage += " [" + YQGeneratedWorldEnvironment.LastShorelineDressingStep + "]";
            else if (current != null && stage.IndexOf("PaintStreamedDetailRoutine", System.StringComparison.Ordinal) >= 0)
                stage += " [" + YQGeneratedWorldEnvironment.LastStreamedDetailStep + "]";
            _maximumPublicationSliceStage = stage;
        }
        RecordAggregateWorkSlice(publicationSliceStartedAt, effectiveBudgetStage);
        return succeeded;
    }

    private static string DisposePublicationIterators(Stack<IEnumerator> iterators)
    {
        string failure = null;
        // note: One broken disposer cannot prevent the remaining owned iterators and worker registration from retiring.
        while (iterators.Count > 0)
        {
            try
            {
                (iterators.Pop() as IDisposable)?.Dispose();
            }
            catch (Exception exception)
            {
                failure = failure ?? exception.ToString();
                Debug.LogException(exception);
            }
        }
        return failure;
    }

    private bool IsRunnableRequiredEcologyWaiter(Vector2Int coordinate, RuntimeChunk chunk)
    {
        // note: Live camera-preparation ecology can run beneath an unpublished structure; keep it eligible for an earlier deadline while excluding work that already has its minimum receipt and is waiting only for publication.
        return chunk != null && chunk.decorativeWorkId != 0 &&
            chunk.decorativeSlotWorkId == 0 && !chunk.requiredEcologyReady &&
            chunk.generationRoot != null &&
            !string.Equals(chunk.decorativeWorkPhase, "verificationHold", StringComparison.Ordinal) &&
            !string.Equals(chunk.decorativeWorkPhase, "waitingForRequiredOwner", StringComparison.Ordinal) &&
            _hardViewDemand.Contains(coordinate);
    }

    private bool ShouldYieldEcologyToEarlierDemand(Vector2Int coordinate)
    {
        // note: Keep a holder's progress for equal deadlines; yield only to ready waiters that enter the camera sooner.
        bool currentVisible = _guaranteedViewDemand.Contains(coordinate);
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        foreach (Vector2Int candidate in _hardViewDemand)
        {
            if (candidate == coordinate ||
                !_chunks.TryGetValue(candidate, out RuntimeChunk candidateChunk) ||
                !IsRunnableRequiredEcologyWaiter(candidate, candidateChunk))
                continue;
            if (_guaranteedViewDemand.Contains(candidate) && !currentVisible)
                return true;
            // note: A predicted owner may preempt only another hard-view owner; background reservations retain their existing class limit.
            if (!currentVisible && _hardViewDemand.Contains(coordinate) &&
                CompareContentQueueDeadlines(candidate, coordinate, traversalVelocity) < 0)
                return true;
        }

        return false;
    }

    private bool HasUnreadyHardViewEcology(Vector2Int excludedCoordinate)
    {
        foreach (Vector2Int coordinate in _hardViewDemand)
        {
            if (coordinate == excludedCoordinate)
                continue;
            if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) &&
                chunk != null && chunk.physicalRepresentation &&
                !chunk.requiredEcologyReady)
                return true;
        }

        return false;
    }

    // note: Preserve aggregate-budget headroom only while an in-demand hard-view ecology owner can actually advance or start.
    private void ReserveRequiredHardViewEcologyBudget()
    {
        foreach (Vector2Int coordinate in _hardViewDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
                chunk.requiredEcologyReady)
                continue;

            if (chunk.decorativeWorkId != 0)
            {
                string phase = chunk.decorativeWorkPhase;
                if (string.Equals(phase, "terrainPriority", StringComparison.Ordinal) ||
                    string.Equals(phase, "verificationHold", StringComparison.Ordinal))
                    continue;
                if (chunk.decorativeSlotWorkId != 0 ||
                    IsRunnableRequiredEcologyWaiter(coordinate, chunk))
                {
                    _requiredHardViewEcologyBudgetFrame = Time.frameCount;
                    return;
                }
                continue;
            }

            if (!chunk.physicalRepresentation || chunk.generationRoot == null)
                continue;

            bool scheduledRetry = _requiredEcologyRetryAt.TryGetValue(coordinate, out float retryAt);
            if (scheduledRetry && retryAt > Time.unscaledTime)
                continue;
            _requiredEcologyRetryCount.TryGetValue(coordinate, out int retryCount);
            if (retryCount >= MaximumRequiredEcologyRetries && !scheduledRetry)
                continue;

            _requiredHardViewEcologyBudgetFrame = Time.frameCount;
            return;
        }
    }

    private bool HasPendingBackgroundEcology()
    {
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            RuntimeChunk chunk = pair.Value;
            if (chunk == null || !chunk.physicalRepresentation ||
                chunk.requiredEcologyReady || chunk.decorativeGeneration != null ||
                chunk.generationRoot == null || _hardViewDemand.Contains(pair.Key) ||
                !IsContentDemandedNow(pair.Key))
                continue;
            if (_requiredEcologyRetryAt.TryGetValue(pair.Key, out float retryAt) &&
                retryAt > Time.unscaledTime)
                continue;
            _requiredEcologyRetryCount.TryGetValue(pair.Key, out int retryCount);
            if (retryCount >= MaximumRequiredEcologyRetries &&
                !_requiredEcologyRetryAt.ContainsKey(pair.Key))
                continue;
            return true;
        }
        return false;
    }

    private bool CanAcquireDecorativeSlot(Vector2Int coordinate)
    {
        if (_activeDecorativeScatters >= MaximumDecorativeScatterWorkers + HardViewDecorativeWorkerReserve)
            return false;
        bool hardView = _hardViewDemand.Contains(coordinate);
        if (hardView)
        {
            // note: A freed hard-view slot goes to the earliest runnable deadline, independent of Unity coroutine resume order.
            Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
            foreach (Vector2Int waitingCoordinate in _hardViewDemand)
            {
                if (waitingCoordinate == coordinate ||
                    !_chunks.TryGetValue(waitingCoordinate, out RuntimeChunk waitingChunk) ||
                    !IsRunnableRequiredEcologyWaiter(waitingCoordinate, waitingChunk))
                    continue;
                int deadlineOrder = CompareContentQueueDeadlines(waitingCoordinate, coordinate, traversalVelocity);
                if (deadlineOrder < 0 ||
                    (deadlineOrder == 0 && CompareCoordinates(waitingCoordinate, coordinate) < 0))
                    return false;
            }
        }
        int sameClassActive = 0;
        bool otherClassWaiting = false;
        // note: Reserve by the identities that actually hold slots, not by a lower total-count ceiling for hard-view work; otherwise background jobs can exclude the camera preparation ring.
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            RuntimeChunk candidate = pair.Value;
            if (candidate.decorativeWorkId == 0)
                continue;
            bool candidateHardView = _hardViewDemand.Contains(pair.Key);
            if (candidate.decorativeSlotWorkId != 0)
            {
                if (candidateHardView == hardView)
                    sameClassActive++;
            }
            else if (candidateHardView != hardView && IsContentDemandedNow(pair.Key))
                otherClassWaiting = true;
        }
        // note: Keep three view/preparation slots and one background slot when both queues wait; either class may borrow idle capacity without cancelling existing work.
        int reservedSlots = hardView ? MaximumDecorativeScatterWorkers : HardViewDecorativeWorkerReserve;
        return sameClassActive < reservedSlots || !otherClassWaiting;
    }

    private void ResumeDeferredDecorativeGeneration(bool hardViewOnly = false)
    {
        bool hardViewEcologyPending = HasUnreadyHardViewEcology(
            new Vector2Int(int.MinValue, int.MinValue));
        bool backgroundEcologyPending = !hardViewOnly && HasPendingBackgroundEcology();
        if (!hardViewEcologyPending && !backgroundEcologyPending)
            return;

        int startedWorkers = 0;
        int workerBudget = hardViewOnly
            ? MaximumDecorativeScatterWorkers
            : hardViewEcologyPending
                ? MaximumDecorativeScatterWorkers + HardViewDecorativeWorkerReserve
                : MaximumDecorativeScatterWorkers;
        int hardViewWorkersStarted = 0;
        bool backgroundWorkerStarted = false;
        _decorativePriorityScratch.Clear();
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            RuntimeChunk chunk = pair.Value;
            if (chunk == null || !chunk.physicalRepresentation ||
                chunk.requiredEcologyReady || chunk.decorativeGeneration != null ||
                chunk.generationRoot == null)
                continue;

            bool hardViewRequired = _hardViewDemand.Contains(pair.Key);
            // note: Pre-view hard demand gets the same bounded ecology lane as current camera cells so its habitat can publish before entering the frustum.
            if (hardViewOnly && !hardViewRequired)
                continue;
            if (!hardViewRequired && !IsContentDemandedNow(pair.Key))
                continue;
            if (_requiredEcologyRetryAt.TryGetValue(pair.Key, out float retryAt) &&
                retryAt > Time.unscaledTime)
                continue;
            _decorativePriorityScratch.Add(pair.Key);
        }

        // note: Select only the few workers this slice can start, avoiding a full sort of the retained world on every frame.
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        while (_decorativePriorityScratch.Count > 0 && startedWorkers < workerBudget)
        {
            int bestIndex = -1;
            for (int candidateIndex = 0; candidateIndex < _decorativePriorityScratch.Count; candidateIndex++)
            {
                Vector2Int candidate = _decorativePriorityScratch[candidateIndex];
                bool candidateHardView = _hardViewDemand.Contains(candidate);
                if (hardViewEcologyPending && backgroundEcologyPending && candidateHardView &&
                    !backgroundWorkerStarted && hardViewWorkersStarted >= MaximumDecorativeScatterWorkers - 1)
                    continue;
                if (hardViewEcologyPending && !candidateHardView &&
                    (!backgroundEcologyPending || backgroundWorkerStarted ||
                     startedWorkers < MaximumDecorativeScatterWorkers - 1))
                    continue;
                if (bestIndex < 0)
                {
                    bestIndex = candidateIndex;
                    continue;
                }
                Vector2Int best = _decorativePriorityScratch[bestIndex];
                int compare = CompareContentQueueDeadlines(candidate, best, traversalVelocity);
                if (compare == 0)
                    compare = CompareNearbyAndViewPriority(candidate, best);
                if (compare == 0)
                    compare = CompareCoordinates(candidate, best);
                if (compare < 0)
                    bestIndex = candidateIndex;
            }
            if (bestIndex < 0)
                break;

            Vector2Int coordinate = _decorativePriorityScratch[bestIndex];
            _decorativePriorityScratch.RemoveAt(bestIndex);
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null ||
                !chunk.physicalRepresentation || chunk.requiredEcologyReady ||
                chunk.decorativeGeneration != null || chunk.generationRoot == null)
                continue;
            bool hardViewRequired = _hardViewDemand.Contains(coordinate);

            float retryAt = 0f;
            bool scheduledRetry = false;
            if (_requiredEcologyRetryAt.TryGetValue(coordinate, out retryAt))
            {
                if (retryAt > Time.unscaledTime)
                    continue;
                // note: Consume the scheduled retry before checking the attempt count so the fourth and final bounded attempt is actually executed.
                _requiredEcologyRetryAt.Remove(coordinate);
                scheduledRetry = true;
            }
            _requiredEcologyRetryCount.TryGetValue(coordinate, out int retryCount);
            if (retryCount >= MaximumRequiredEcologyRetries && !scheduledRetry)
            {
                // note: Do not restart an exhausted ecology owner every frame; a hard-view owner remains an explicit failed publication until a new owner epoch replaces it.
                if (string.IsNullOrWhiteSpace(chunk.failureReason))
                {
                    chunk.state = YQSemanticChunkLifecycle.Failed;
                    chunk.failureReason = "required ecology retry budget exhausted";
                    _lifecyclePending = true;
                }
                continue;
            }

            Terrain terrain = ResolveTerrainForChunk(coordinate);
            GeneratedRegionRecord region = ResolveRegion(chunk.record.parentRegionId);
            GeneratedRegionAssetPaletteRecord palette = ResolvePalette(
                region,
                chunk.record.biome,
                !IsChunkInsideAuthoredTerrain(coordinate));
            YQRuntimeWorldAssetRegistry registry = YQRuntimeWorldAssetRegistry.Instance;
            if (terrain == null || region == null || palette == null || registry == null)
            {
                // note: A demanded ecology lane records missing provider prerequisites for bounded recovery instead of silently disappearing from the scheduler.
                ScheduleRequiredEcologyRetry(coordinate, chunk, "ecology provider prerequisite unavailable");
                continue;
            }

            _requiredEcologyRetryAt.Remove(coordinate);
            StartDecorativeGeneration(
                    coordinate,
                    chunk,
                    chunk.generationRoot,
                    terrain,
                    region,
                    palette,
                    registry,
                    CaptureStreamingWorkToken(coordinate));
            startedWorkers++;
            if (hardViewRequired)
                hardViewWorkersStarted++;
            else
                backgroundWorkerStarted = true;
            // note: Fill the existing bounded ecology budget in one dispatch pass; serially starting one visible owner per frame can strand the final hard-view publication behind the startup timeout.
            if (startedWorkers >= workerBudget)
                break;
        }
    }

    private bool IsGenerationCurrent(Vector2Int coordinate, RuntimeChunk chunk, StreamingWorkToken token)
    {
        // note: Require profile/world identity, overlay revision, local epoch, and live dictionary ownership before a yielded build may publish.
        string profileId = _world != null && _world.worldIdentity != null ? _world.worldIdentity.ownerProfileId : string.Empty;
        string worldId = _world != null && _world.worldIdentity != null ? _world.worldIdentity.worldId : string.Empty;
        long featureRevision = _plan != null && _plan.semanticAuthority != null
            ? _plan.semanticAuthority.featureOverlayRevision
            : 0L;
        return token.generationEpoch == _configurationEpoch &&
            token.featureRevision == featureRevision &&
            string.Equals(token.profileId, profileId ?? string.Empty, StringComparison.Ordinal) &&
            string.Equals(token.worldId, worldId ?? string.Empty, StringComparison.Ordinal) &&
            chunk != null &&
            ReferenceEquals(token.owner, chunk) &&
            token.ownerEpoch == (token.terrainWork ? chunk.terrainOwnerEpoch : chunk.contentOwnerEpoch) &&
            _chunks.TryGetValue(coordinate, out RuntimeChunk current) && ReferenceEquals(current, chunk);
    }

    private void ReleaseStaleGeneration(Vector2Int coordinate, RuntimeChunk chunk, StreamingWorkToken token)
    {
        // note: Release only this coordinate's worker slot; another visible owner may be progressing concurrently.
        if (!_activeGenerationWorkIds.TryGetValue(coordinate, out long workId) || workId != token.workId)
            return;
        _activeGenerationWorkIds.Remove(coordinate);
        _activeGenerations.Remove(coordinate);
        if (chunk == null || !ReferenceEquals(token.owner, chunk) || token.ownerEpoch != chunk.contentOwnerEpoch ||
            !_chunks.TryGetValue(coordinate, out RuntimeChunk current) || !ReferenceEquals(current, chunk))
        {
            return;
        }
        if (_demandCancelledGenerations.Remove(coordinate) && chunk.state == YQSemanticChunkLifecycle.Generating)
        {
            // note: Release only unpublished content from an obsolete build; a terrain tile already published for collision remains available to the new demand owner.
            ReleaseObsoleteGenerationContent(chunk);
            chunk.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
            if (IsContentDemandedNow(coordinate))
                Enqueue(coordinate);
        }
        if (chunk.state == YQSemanticChunkLifecycle.Generating && !IsGenerationCurrent(coordinate, chunk, token))
        {
            // note: Return stale work to the semantic queue so a newer token can reconstruct the owner instead of leaving it permanently Generating.
            chunk.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
            if (IsContentDemandedNow(coordinate))
                Enqueue(coordinate);
        }
    }

    private void ReleaseObsoleteGenerationContent(RuntimeChunk chunk)
    {
        if (chunk == null)
            return;
        Vector2Int coordinate = chunk.record != null
            ? new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ)
            : new Vector2Int(int.MinValue, int.MinValue);
        // note: Authored terrain is a permanent visual/collision provider; cancelling generated content must never revoke its base appearance contract.
        chunk.contentOwnerEpoch++;
        chunk.sitePresentations.Clear();
        chunk.sitePresentationRenderers.Clear();
        StopDecorativeGeneration(chunk);
        // note: A content replacement retires even previously published ecology because it belongs to the retired structural root.
        DestroyDecorativeRoot(chunk);
        bool authoredTerrain = coordinate.x != int.MinValue && IsChunkInsideAuthoredTerrain(coordinate) && IsTerrainColliderReady(_terrain);
        // note: Content cancellation may occur after the shared streamed terrain and its appearance lane have already published; preserve that independent readiness instead of creating a permanent visual hole.
        bool retainedTerrainPublished = authoredTerrain || coordinate.x != int.MinValue &&
            chunk.terrainPublished && HasPublishedTerrain(coordinate);
        bool retainedAppearanceReady = authoredTerrain || retainedTerrainPublished && chunk.appearanceReady;
        for (int index = chunk.generatedObjects.Count - 1; index >= 0; index--)
        {
            GameObject item = chunk.generatedObjects[index];
            if (item == null)
            {
                chunk.generatedObjects.RemoveAt(index);
                InvalidateRendererValidationCache(chunk);
                continue;
            }
            // note: Keep the extension Terrain/TerrainCollider owner intact; only the uncommitted content root is discarded.
            if (item.GetComponent<Terrain>() != null)
                continue;
            UnityEngine.Object.Destroy(item);
            chunk.generatedObjects.RemoveAt(index);
            InvalidateRendererValidationCache(chunk);
            RemoveOwnedObject(chunk, item);
        }
        chunk.physicalRepresentation = false;
        chunk.requiredContentReady = false;
        chunk.contentStartedAt = -1f;
        chunk.contentReadyAt = -1f;
        chunk.ecologyStartedAt = -1f;
        chunk.appearanceReady = retainedAppearanceReady;
        chunk.overlayReady = false;
        chunk.requiredEcologyReady = false;
        chunk.activationComplete = false;
        chunk.visualReady = retainedAppearanceReady;
        chunk.decorativeComplete = false;
        // note: Content-root cancellation invalidates only this root's overlay receipts; the replacement root must replay durable overlays.
        chunk.appliedOverlayRevisions.Clear();
        chunk.appliedOverlayReceipts.Clear();
        chunk.publicationVersion++;
        PruneDestroyedOwnedObjects(chunk);
        // note: If cancellation left a valid terrain tile without a completed paint coroutine, restart only that missing appearance lane; the next structural owner can then publish atomically without freezing traversal.
        if (retainedTerrainPublished && !retainedAppearanceReady && !_terrainPainting.ContainsKey(coordinate) &&
            _extendedTerrainTiles.TryGetValue(coordinate, out Terrain retainedTerrain) && retainedTerrain != null &&
            retainedTerrain.terrainData != null)
        {
            StartTerrainAppearancePainting(
                coordinate,
                retainedTerrain.terrainData,
                Mathf.Max(32f, chunkWorldSize),
                retainedTerrain,
                CaptureStreamingWorkToken(coordinate, true));
        }
    }

    private IEnumerator EnsureTerrainForContentMaterializationRoutine(Vector2Int coordinate)
    {
        if (ResolveTerrainForContentMaterialization(coordinate) != null)
            yield break;
        RequestTerrainForChunk(coordinate);
        float deadline = Time.unscaledTime + 45f;
        while (ResolveTerrainForContentMaterialization(coordinate) == null && Time.unscaledTime < deadline)
            yield return null;
        if (ResolveTerrainForContentMaterialization(coordinate) == null)
        {
            // note: Return a guarded failure instead of throwing from a nested iterator, so the owning generation wrapper can clear its active slot and retry safely.
            Debug.LogWarning("[YQSemanticChunkStreamer] Terrain heightfield continuation timed out for " + coordinate);
        }
    }

    private GeneratedRegionRecord ResolveRegion(string regionId)
    {
        if (_plan == null || _plan.regions == null)
            return null;
        for (int i = 0; i < _plan.regions.Count; i++)
        {
            GeneratedRegionRecord region = _plan.regions[i];
            if (region != null && string.Equals(region.regionId, regionId, StringComparison.OrdinalIgnoreCase))
                return region;
        }
        return _plan.regions.Count > 0 ? _plan.regions[0] : null;
    }

    private GeneratedRegionAssetPaletteRecord ResolvePalette(GeneratedRegionRecord region, string biome, bool preferBiome = false)
    {
        if (_plan == null || _plan.assetPalettes == null || region == null)
            return null;
        GeneratedRegionAssetPaletteRecord regionPalette = null;
        // note: The persisted assetPaletteId is the strongest binding; resolve it completely before considering a legacy regionId match.
        if (!string.IsNullOrWhiteSpace(region.assetPaletteId))
        {
            for (int i = 0; i < _plan.assetPalettes.Count; i++)
            {
                GeneratedRegionAssetPaletteRecord palette = _plan.assetPalettes[i];
                if (palette != null && string.Equals(palette.paletteId, region.assetPaletteId, StringComparison.OrdinalIgnoreCase))
                {
                    regionPalette = palette;
                    break;
                }
            }
        }
        if (regionPalette == null)
        {
            for (int i = 0; i < _plan.assetPalettes.Count; i++)
            {
                GeneratedRegionAssetPaletteRecord palette = _plan.assetPalettes[i];
                if (palette != null && string.Equals(palette.regionId, region.regionId, StringComparison.OrdinalIgnoreCase))
                {
                    regionPalette = palette;
                    break;
                }
            }
        }
        string biomeToken = !string.IsNullOrWhiteSpace(biome) ? biome.Trim().ToLowerInvariant() : string.Empty;
        if (preferBiome && !string.IsNullOrWhiteSpace(biomeToken))
        {
            if (regionPalette != null && PaletteIdentityMatchesBiome(BuildPaletteIdentity(regionPalette), biomeToken))
                return regionPalette;
            // note: Continuation cells use the deterministic world-space biome field to select a compatible curated palette when the nearest authored region has a different style.
            List<GeneratedRegionAssetPaletteRecord> matching = new List<GeneratedRegionAssetPaletteRecord>();
            for (int i = 0; i < _plan.assetPalettes.Count; i++)
            {
                GeneratedRegionAssetPaletteRecord palette = _plan.assetPalettes[i];
                if (palette != null && PaletteIdentityMatchesBiome(BuildPaletteIdentity(palette), biomeToken))
                    matching.Add(palette);
            }
            if (matching.Count > 0)
            {
                int selected = Mathf.Clamp(Mathf.FloorToInt(StableHash(_plan.worldSeed + "|biome-palette|" + biomeToken + "|" + region.regionId) % (uint)matching.Count), 0, matching.Count - 1);
                return matching[selected];
            }
        }
        if (regionPalette != null)
            return regionPalette;
        if (!string.IsNullOrWhiteSpace(biome))
        {
            string token = biome.Trim().ToLowerInvariant();
            for (int i = 0; i < _plan.assetPalettes.Count; i++)
            {
                GeneratedRegionAssetPaletteRecord palette = _plan.assetPalettes[i];
                if (palette == null)
                    continue;
                string identity = BuildPaletteIdentity(palette);
                if (PaletteIdentityMatchesBiome(identity, token))
                    return palette;
            }
        }
        return null;
    }

    private static string BuildPaletteIdentity(GeneratedRegionAssetPaletteRecord palette)
    {
        if (palette == null)
            return string.Empty;
        string tags = palette.styleTags != null ? string.Join("|", palette.styleTags) : string.Empty;
        return ((palette.paletteId ?? string.Empty) + "|" +
            (palette.styleKey ?? string.Empty) + "|" +
            (palette.terrainPack ?? string.Empty) + "|" +
            (palette.naturePack ?? string.Empty) + "|" + tags).ToLowerInvariant();
    }

    private static bool PaletteIdentityMatchesBiome(string identity, string token)
    {
        if (string.IsNullOrWhiteSpace(identity) || string.IsNullOrWhiteSpace(token))
            return false;
        return identity.Contains(token) ||
            (token == "forest" && (identity.Contains("wood") || identity.Contains("nordic") || identity.Contains("forest"))) ||
            (token == "grassland" && (identity.Contains("plain") || identity.Contains("meadow") || identity.Contains("grass"))) ||
            (token == "wetland" && (identity.Contains("marsh") || identity.Contains("wet") || identity.Contains("swamp")));
    }

    private static long FindMaximumOverlayRevision(GeneratedSemanticWorldAuthorityRecord authority)
    {
        long maximum = 0L;
        if (authority == null || authority.featureOverlays == null)
            return maximum;
        for (int index = 0; index < authority.featureOverlays.Count; index++)
            if (authority.featureOverlays[index] != null)
                maximum = Math.Max(maximum, authority.featureOverlays[index].revision);
        return maximum;
    }

    private string ResolveOverlayOwnerRegion(string featureId)
    {
        if (_plan == null || string.IsNullOrWhiteSpace(featureId))
            return string.Empty;
        if (_plan.semanticAuthority != null && _plan.semanticAuthority.siteReservations != null)
        {
            for (int index = 0; index < _plan.semanticAuthority.siteReservations.Count; index++)
            {
                GeneratedSemanticSiteReservationRecord site = _plan.semanticAuthority.siteReservations[index];
                if (site != null && string.Equals(site.siteId, featureId, StringComparison.Ordinal))
                    return site.ownerRegionId ?? string.Empty;
            }
        }
        if (_plan.semanticAuthority != null && _plan.semanticAuthority.routeGraph != null)
        {
            for (int index = 0; index < _plan.semanticAuthority.routeGraph.Count; index++)
            {
                GeneratedSemanticRouteGraphRecord route = _plan.semanticAuthority.routeGraph[index];
                if (route != null && string.Equals(route.routeId, featureId, StringComparison.Ordinal))
                    return route.ownerRegionId ?? string.Empty;
            }
        }
        if (_plan.semanticAuthority != null && _plan.semanticAuthority.waterNetworks != null)
        {
            for (int index = 0; index < _plan.semanticAuthority.waterNetworks.Count; index++)
            {
                GeneratedSemanticWaterNetworkRecord water = _plan.semanticAuthority.waterNetworks[index];
                if (water != null && string.Equals(water.waterId, featureId, StringComparison.Ordinal))
                    return water.ownerRegionId ?? string.Empty;
            }
        }
        for (int index = 0; index < _plan.semanticChunks.Count; index++)
        {
            GeneratedSemanticChunkRecord record = _plan.semanticChunks[index];
            if (record == null || record.featureIds == null || !record.featureIds.Contains(featureId))
                continue;
            return record.parentRegionId ?? string.Empty;
        }
        return string.Empty;
    }

    private void BindGeneratedFeatureTargets(RuntimeChunk chunk, GameObject root)
    {
        if (chunk == null || chunk.record == null || root == null)
            return;
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int index = 0; index < transforms.Length; index++)
        {
            Transform candidate = transforms[index];
            if (candidate == null)
                continue;
            string featureId = ResolveGeneratedFeatureId(chunk.record, candidate.name);
            if (!string.IsNullOrWhiteSpace(featureId))
                RegisterFeatureOverlayTarget(featureId, candidate.gameObject, candidate.name);
        }
        // note: Site roots already carry their accepted identity, including reviewed sites whose name is not a legacy feature alias.
        YQStreamedFeatureOverlayTarget[] targets = root.GetComponentsInChildren<YQStreamedFeatureOverlayTarget>(true);
        for (int index = 0; index < targets.Length; index++)
        {
            YQStreamedFeatureOverlayTarget target = targets[index];
            if (target != null && _acceptedSpatialProjection != null &&
                _acceptedSpatialProjection.TryGetSiteBySiteId(target.featureId, out YQSpatialMaterializationSiteV2 site))
                CacheSharedSitePresentation(chunk, target, site);
        }
    }

    private void CacheSharedSitePresentation(RuntimeChunk chunk, YQStreamedFeatureOverlayTarget target, YQSpatialMaterializationSiteV2 site)
    {
        // note: A callback from another world or a member cell cannot publish into this accepted site's central owner.
        if (chunk?.record == null || target == null || site.kind == YQSiteKindV2.Origin || _worldRoot == null ||
            !target.transform.IsChildOf(_worldRoot) ||
            ChunkFor(new Vector2(site.x, site.z)) != new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ))
            return;
        // note: A marker or unrelated overlay target cannot replace the reviewed provider required by these accepted site kinds.
        bool requiresCompiledProvider = site.kind == YQSiteKindV2.Settlement || site.kind == YQSiteKindV2.HostileSite ||
            site.kind == YQSiteKindV2.PointOfInterest;
        YQCompiledWorldSiteInstance provider = target.GetComponent<YQCompiledWorldSiteInstance>();
        if (requiresCompiledProvider && provider == null)
            return;
        SharedSitePresentation presentation = null;
        for (int index = 0; index < chunk.sitePresentations.Count; index++)
        {
            SharedSitePresentation existing = chunk.sitePresentations[index];
            if (string.Equals(existing.siteId, site.siteId, StringComparison.Ordinal))
            {
                // note: A child overlay with the same feature identity cannot replace the complete accepted site root.
                if (existing.root != null && existing.root != target.gameObject && target.transform.IsChildOf(existing.root.transform))
                    return;
                if (existing.hasCompiledProvider && provider == null)
                    return;
                presentation = existing;
                for (int rendererIndex = 0; rendererIndex < existing.renderers.Count; rendererIndex++)
                    chunk.sitePresentationRenderers.Remove(existing.renderers[rendererIndex]);
                break;
            }
        }
        if (presentation == null)
        {
            presentation = new SharedSitePresentation();
            chunk.sitePresentations.Add(presentation);
        }
        presentation.siteId = site.siteId;
        presentation.sourceSemanticId = site.sourceSemanticId;
        presentation.root = target.gameObject;
        presentation.target = target;
        presentation.provider = provider;
        presentation.hasCompiledProvider = presentation.provider != null;
        presentation.requiresCompiledProvider = requiresCompiledProvider;
        presentation.configurationEpoch = _configurationEpoch;
        presentation.contentOwnerEpoch = chunk.contentOwnerEpoch;
        presentation.projection = _acceptedSpatialProjection;
        presentation.visualStateKnown = false;
        presentation.renderers.Clear();
        presentation.requiredRenderers.Clear();
        target.GetComponentsInChildren<Renderer>(true, presentation.renderers);
        for (int index = 0; index < presentation.renderers.Count; index++)
        {
            Renderer renderer = presentation.renderers[index];
            chunk.sitePresentationRenderers.Add(renderer);
            // note: Renderer.enabled may already reflect staging; preserve the existing exposure policy and certify authored activeSelf paths only.
            if (IsAuthoredSiteRendererActive(renderer.transform, target.transform))
                presentation.requiredRenderers.Add(renderer);
        }
        _lifecyclePending = true;
    }

    private static bool IsAuthoredSiteRendererActive(Transform renderer, Transform root)
    {
        // note: The staged root may be hidden; only authored descendant activeSelf intent determines which renderers certify publication.
        for (Transform current = renderer; current != null && current != root; current = current.parent)
            if (!current.gameObject.activeSelf)
                return false;
        return true;
    }

    private bool IsCurrentSharedSitePresentation(RuntimeChunk chunk, SharedSitePresentation presentation)
    {
        // note: Presentation follows the same configuration and content-owner epochs as the structural publication it represents.
        return presentation.root != null && presentation.target != null &&
            presentation.configurationEpoch == _configurationEpoch && presentation.contentOwnerEpoch == chunk.contentOwnerEpoch &&
            ReferenceEquals(presentation.projection, _acceptedSpatialProjection) &&
            (string.Equals(presentation.target.featureId, presentation.siteId, StringComparison.Ordinal) ||
             (!string.IsNullOrWhiteSpace(presentation.sourceSemanticId) &&
              string.Equals(presentation.target.featureId, presentation.sourceSemanticId, StringComparison.Ordinal)));
    }

    private void ProcessContinuationPopulationWork()
    {
        if (_continuationPopulationWork.Count > 0 && !IsContinuationPopulationWorkCurrent())
            CancelContinuationPopulationWork();
        if (_continuationPopulationWork.Count == 0)
        {
            SharedSitePresentation selected = null;
            RuntimeChunk selectedChunk = null;
            int nearest = int.MaxValue;
            foreach (var pair in _chunks)
            {
                RuntimeChunk chunk = pair.Value;
                if (chunk == null || chunk.state == YQSemanticChunkLifecycle.Failed || !chunk.activationComplete || chunk.activeState != 1)
                    continue;
                int distance = Mathf.Max(Mathf.Abs(pair.Key.x - _currentChunk.x), Mathf.Abs(pair.Key.y - _currentChunk.y));
                foreach (var presentation in chunk.sitePresentations)
                {
                    var provider = presentation.provider;
                    if (!IsCurrentSharedSitePresentation(chunk, presentation) || provider == null ||
                        !provider.isActiveAndEnabled || !provider.NeedsContinuationPopulation ||
                        distance > nearest || distance == nearest && selected != null &&
                        string.CompareOrdinal(presentation.siteId, selected.siteId) >= 0) continue;
                    selected = presentation; selectedChunk = chunk; nearest = distance;
                }
            }
            if (selected == null) return;
            var coordinate = new Vector2Int(selectedChunk.record.chunkX, selectedChunk.record.chunkZ);
            Terrain terrain = ResolveTerrainForContentMaterialization(coordinate);
            if (terrain == null) return;
            _continuationPopulationProvider = selected.provider;
            _continuationPopulationChunk = selectedChunk;
            _continuationPopulationContent = selected.provider.transform.Find("CompiledSiteContent");
            _continuationPopulationToken = CaptureStreamingWorkToken(coordinate);
            var captured = selected;
            var capturedChunk = selectedChunk;
            _continuationPopulationWork.Push(selected.provider.BuildRequiredContinuationPopulationRoutine(terrain, _plan,
                YQRuntimeWorldAssetRegistry.Instance, (success, reason) =>
                {
                    if (!success || !IsCurrentSharedSitePresentation(capturedChunk, captured)) return;
                    // note: Cache actor renderers once at the same site publication boundary used for each geometry lifetime.
                    if (_acceptedSpatialProjection.TryGetSiteBySiteId(captured.siteId, out var site))
                        CacheSharedSitePresentation(capturedChunk, captured.target, site);
                    InvalidateRendererValidationCache(capturedChunk);
                    capturedChunk.visualStateKnown = false;
                    _lifecyclePending = true;
                }));
        }
        float started = Time.realtimeSinceStartup;
        try
        {
            // note: Drain nested completion frames together; stop at the first cooperative yield or bounded elapsed slice.
            for (int steps = 0; steps < 32 && _continuationPopulationWork.Count > 0; steps++)
            {
                if (!IsContinuationPopulationWorkCurrent()) { CancelContinuationPopulationWork(); break; }
                IEnumerator step = _continuationPopulationWork.Peek();
                if (!step.MoveNext())
                {
                    _continuationPopulationWork.Pop(); (step as IDisposable)?.Dispose();
                }
                else if (step.Current is IEnumerator nested) _continuationPopulationWork.Push(nested);
                else break;
                if (Time.realtimeSinceStartup - started >= 0.0015f) break;
            }
        }
        catch (Exception exception)
        {
            if (_continuationPopulationProvider != null) _continuationPopulationProvider.RejectContinuationPopulation(exception);
            else Debug.LogException(exception, this);
            CancelContinuationPopulationWork();
        }
        RecordAggregateWorkSlice(started, "continuationPopulation");
        if (_continuationPopulationWork.Count == 0) CancelContinuationPopulationWork();
    }

    private static Vector2Int[] BuildFrontierConstructionScanOffsets()
    {
        // note: Check candidate blocks beyond the protected opening collar first, then retain the deterministic near rings as bounded recovery.
        const int openingSafePriorityRadius = 3;
        const int maximumRadius = 8;
        var offsets = new Vector2Int[(maximumRadius * 2 + 1) * (maximumRadius * 2 + 1)];
        int index = 0;
        for (int radius = openingSafePriorityRadius; radius <= maximumRadius; radius++)
            for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) == radius)
                        offsets[index++] = new Vector2Int(x, z);
        for (int radius = 0; radius < openingSafePriorityRadius; radius++)
            for (int z = -radius; z <= radius; z++)
                for (int x = -radius; x <= radius; x++)
                    if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) == radius)
                        offsets[index++] = new Vector2Int(x, z);
        return offsets;
    }

    private void ProcessFrontierConstructionWork()
    {
        float started = Time.realtimeSinceStartup;
        if (!Application.isPlaying || !_configured || _player == null ||
            _acceptedSpatialProjection == null || YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked) return;
        if (_frontierConstructionWork.Count > 0 && !IsFrontierConstructionWorkCurrent()) CancelFrontierConstructionWork();
        if (_frontierConstructionWork.Count == 0)
        {
            if (Time.unscaledTime < _nextFrontierConstructionAt || _continuationPopulationWork.Count > 0) return;
            _nextFrontierConstructionAt = Time.unscaledTime + FrontierConstructionRetrySeconds;
            var builder = YQGeneratedWorldRuntimeBuilder.Instance;
            if (builder == null || !builder.isActiveAndEnabled || YQProfileSaveSystem.Instance == null ||
                !ReferenceEquals(WorldStateManager.Instance?.State, _world)) return;
            int span = YQSemanticWorldAuthority.FrontierOpportunitySpanCells;
            var predicted = ResolvePredictedContentChunk(new Vector2(_player.position.x, _player.position.z));
            // note: Convert physical chunk coordinates to the semantic block grid; configured chunk sizes need not equal 128m.
            float chunkSize = Mathf.Max(32f, chunkWorldSize);
            float blockWorldSize = span * YQSemanticWorldAuthority.CellSizeMeters;
            var predictedBlock = new Vector2Int(
                Mathf.FloorToInt((predicted.x + 0.5f) * chunkSize / blockWorldSize),
                Mathf.FloorToInt((predicted.y + 0.5f) * chunkSize / blockWorldSize));
            GeneratedSpatialContinuationLocationV2Record candidate = null;
            Vector2Int candidateBlock = default;
            int candidateAttempts = 0;
            double scanStarted = Time.realtimeSinceStartupAsDouble;
            int scannedBlocks = 0;
            // note: Batch cheap deterministic opportunity checks under a strict time/count cap so collar-only blocks no longer stall site discovery.
            while (scannedBlocks < FrontierConstructionBlocksPerSlice &&
                   Time.realtimeSinceStartupAsDouble - scanStarted < FrontierConstructionScanBudgetSeconds)
            {
                int scan = _frontierConstructionScan++ % FrontierConstructionScanOffsets.Length;
                Vector2Int block = predictedBlock + FrontierConstructionScanOffsets[scan];
                scannedBlocks++;
                _frontierConstructionAttempts.TryGetValue(block, out int attempts);
                if (attempts >= 2 || !YQSemanticWorldAuthority.TryGetUnacceptedContinuationOpportunity(
                        _plan, block.x, block.y, out var opportunity, out _))
                    continue;

                GeneratedRegionRecord region = null;
                float nearest = float.PositiveInfinity;
                foreach (var entry in _plan.regions)
                {
                    if (entry == null || !_acceptedSpatialProjection.TryGetRegion(entry.regionId, out var physicalRegion)) continue;
                    float dx = physicalRegion.centerX - opportunity.worldX, dz = physicalRegion.centerZ - opportunity.worldZ;
                    float distance = dx * dx + dz * dz;
                    if (distance < nearest || distance == nearest && region != null && string.CompareOrdinal(entry.regionId, region.regionId) < 0)
                    { region = entry; nearest = distance; }
                }
                if (region == null)
                    continue;
                if (!YQSemanticWorldAuthority.TryBuildFrontierLocationCandidate(_plan, block.x, block.y,
                        region.regionId, out var proposed, out string candidateFailure))
                {
                    if (candidateFailure.IndexOf("unique on-rim frontages", StringComparison.OrdinalIgnoreCase) >= 0)
                        TrackFrontierConstructionAttempt(block, 2);
                    continue;
                }

                TrackFrontierConstructionAttempt(block, attempts);
                candidate = proposed;
                candidateBlock = block;
                candidateAttempts = attempts;
                break;
            }
            if (candidate == null)
            {
                RecordAggregateWorkSlice(started, "frontierConstruction");
                return;
            }
            _frontierConstructionWorld = _world; _frontierConstructionPlan = _plan;
            _frontierConstructionEpoch = _configurationEpoch; _frontierConstructionServiceEpoch = YQServiceLifecycle.RequestEpoch;
            _frontierConstructionBaseHash = _plan.spatialPlanV2.contentHash;
            _frontierConstructionPrevious = _plan.spatialPlanV2.acceptedContinuation;
            _frontierConstructionAccepted = null; _frontierConstructionFailure = string.Empty;
            _frontierConstructionWork.Push(builder.PrepareFrontierConstructionWithAdmissionRoutine(_world, candidate,
                (accepted, reason) =>
                {
                    _frontierConstructionAccepted = accepted;
                    _frontierConstructionFailure = reason;
                    if (accepted == null && IsPermanentFrontierPlacementFailure(reason) && IsFrontierConstructionWorkCurrent())
                        _frontierConstructionAttempts[candidateBlock] = 2;
                },
                () => {
                    // note: Charge only the exact current block whose typed proposal passed provenance and engine-reservation checks; transient model failures remain eligible for retry.
                    if (IsFrontierConstructionWorkCurrent()) _frontierConstructionAttempts[candidateBlock] = candidateAttempts + 1;
                }, TryScreenFrontierTerrainOwnership));
        }
        try
        {
            // note: Resources.LoadAsync remains owned until completion; a frame boundary alone cannot authorize reading its asset.
            if (_frontierConstructionPendingLoad != null && !_frontierConstructionPendingLoad.isDone) return;
            _frontierConstructionPendingLoad = null;
            for (int steps = 0; steps < 32 && _frontierConstructionWork.Count > 0; steps++)
            {
                if (!IsFrontierConstructionWorkCurrent()) { CancelFrontierConstructionWork(); break; }
                var step = _frontierConstructionWork.Peek();
                if (!step.MoveNext()) { _frontierConstructionWork.Pop(); (step as IDisposable)?.Dispose(); }
                else if (step.Current is IEnumerator nested) _frontierConstructionWork.Push(nested);
                else if (step.Current is AsyncOperation pending) { _frontierConstructionPendingLoad = pending; break; }
                else if (step.Current == null) break;
                else throw new InvalidOperationException("Frontier construction yielded an unsupported wait instruction: " + step.Current.GetType().Name);
                if (Time.realtimeSinceStartup - started >= 0.0015f) break;
            }
            if (_frontierConstructionWork.Count == 0 && IsFrontierConstructionWorkCurrent())
            {
                bool published = _frontierConstructionAccepted != null &&
                    TryPublishAndInstallContinuation(_frontierConstructionAccepted, out _frontierConstructionFailure);
                if (published) Debug.Log("[YQSemanticChunkStreamer] Accepted frontier location published and admitted without replacing live terrain.");
                else if (!string.IsNullOrWhiteSpace(_frontierConstructionFailure))
                    Debug.LogWarning("[YQSemanticChunkStreamer] Frontier construction rejected: " + _frontierConstructionFailure);
                CancelFrontierConstructionWork();
            }
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            CancelFrontierConstructionWork();
        }
        RecordAggregateWorkSlice(started, "frontierConstruction");
    }

    private void TrackFrontierConstructionAttempt(Vector2Int block, int attempts)
    {
        if (!_frontierConstructionAttempts.ContainsKey(block))
        {
            while (_frontierConstructionAttemptOrder.Count >= 128)
                _frontierConstructionAttempts.Remove(_frontierConstructionAttemptOrder.Dequeue());
            _frontierConstructionAttemptOrder.Enqueue(block);
        }
        _frontierConstructionAttempts[block] = attempts;
    }

    private static bool IsPermanentFrontierPlacementFailure(string failure)
    {
        // note: Same-block physical retries are deterministic for this accepted plan; remember a proven geometry rejection and keep scanning other opportunities.
        return !string.IsNullOrWhiteSpace(failure) &&
            (failure.IndexOf("bounded same-opportunity retries", StringComparison.OrdinalIgnoreCase) >= 0 ||
             failure.IndexOf("intersects opening/collar terrain", StringComparison.OrdinalIgnoreCase) >= 0 ||
             failure.IndexOf("unique on-rim frontages", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private bool TryScreenFrontierTerrainOwnership(IReadOnlyList<Rect> influences, out string failure)
    {
        // note: A transient numeric screen is scoped to this current work owner and never constructs an accepted refresh token.
        failure = "Frontier terrain screening belongs to a stale streaming owner.";
        return IsFrontierConstructionWorkCurrent() && TryValidateContinuationTerrainOwnership(influences, out failure);
    }

    private bool IsFrontierConstructionWorkCurrent()
    {
        return _frontierConstructionWorld != null && isActiveAndEnabled && _frontierConstructionEpoch == _configurationEpoch &&
            YQServiceLifecycle.IsCurrent(_frontierConstructionServiceEpoch) &&
            ReferenceEquals(WorldStateManager.Instance?.State, _frontierConstructionWorld) &&
            ReferenceEquals(_world, _frontierConstructionWorld) && ReferenceEquals(_plan, _frontierConstructionPlan) &&
            _plan?.spatialPlanV2?.contentHash == _frontierConstructionBaseHash &&
            ReferenceEquals(_plan.spatialPlanV2.acceptedContinuation, _frontierConstructionPrevious);
    }

    private void CancelFrontierConstructionWork()
    {
        // note: Dispose every nested construction iterator to retire the existing LLM request slot and pending detached layouts on cancellation.
        Exception cleanupFailure;
        try { cleanupFailure = DisposeOwnedContinuationIterators(_frontierConstructionWork); }
        finally
        {
            _frontierConstructionPendingLoad = null;
            _frontierConstructionWorld = null; _frontierConstructionPlan = null; _frontierConstructionPrevious = null;
            _frontierConstructionAccepted = null; _frontierConstructionFailure = string.Empty;
        }
        if (cleanupFailure != null) Debug.LogException(cleanupFailure, this);
    }

    private static Exception DisposeOwnedContinuationIterators(Stack<IEnumerator> work)
    {
        List<Exception> failures = null;
        // note: A failed child cleanup must not strand its parent request or provider lifetime.
        while (work.Count > 0)
            try { (work.Pop() as IDisposable)?.Dispose(); }
            catch (Exception exception) { (failures ??= new List<Exception>()).Add(exception); }
        return failures == null ? null : new AggregateException("Owned continuation iterator cleanup failed.", failures);
    }

    private bool IsContinuationPopulationWorkCurrent()
    {
        var provider = _continuationPopulationProvider;
        var chunk = _continuationPopulationChunk;
        if (provider == null || chunk?.record == null || !provider.isActiveAndEnabled || !provider.IsContinuationGeometryLoaded ||
            _continuationPopulationContent == null || !_continuationPopulationContent.gameObject.activeInHierarchy ||
            provider.transform.Find("CompiledSiteContent") != _continuationPopulationContent) return false;
        var coordinate = new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ);
        return IsGenerationCurrent(coordinate, chunk, _continuationPopulationToken);
    }

    private void CancelContinuationPopulationWork()
    {
        // note: Explicitly dispose nested actor work so provider cancellation retires its hidden staging cast.
        Exception cleanupFailure;
        try { cleanupFailure = DisposeOwnedContinuationIterators(_continuationPopulationWork); }
        finally
        {
            _continuationPopulationProvider = null;
            _continuationPopulationChunk = null;
            _continuationPopulationContent = null;
        }
        if (cleanupFailure != null) Debug.LogException(cleanupFailure, this);
    }

    private static bool IsSharedSiteGeometryReady(SharedSitePresentation presentation)
    {
        // note: Publish accepted site geometry independently; the existing background owner continues loading canonical actors.
        return (!presentation.hasCompiledProvider && !presentation.requiresCompiledProvider) ||
            (presentation.provider != null && presentation.provider.IsContinuationGeometryLoaded && presentation.provider.isActiveAndEnabled &&
             (YQCompiledWorldSiteInstance.IsSiteGeometryLoaded(presentation.siteId) ||
              YQCompiledWorldSiteInstance.IsSiteGeometryLoaded(presentation.sourceSemanticId)));
    }

    private bool IsSharedSitePresentationReady(RuntimeChunk chunk, string siteId)
    {
        for (int index = 0; index < chunk.sitePresentations.Count; index++)
        {
            SharedSitePresentation presentation = chunk.sitePresentations[index];
            if (!string.Equals(presentation.siteId, siteId, StringComparison.Ordinal) || !IsCurrentSharedSitePresentation(chunk, presentation))
                continue;
            // note: An accepted destructive overlay removes the site intentionally; surviving roots require actual renderer publication.
            if (IsDestructiveOverlayState(presentation.target.appliedState) && !presentation.root.activeSelf)
                return true;
            return chunk.activationComplete && chunk.activeState == 1 && presentation.root.activeInHierarchy &&
                IsSharedSiteGeometryReady(presentation) && presentation.visualStateKnown && presentation.visualState &&
                presentation.requiredRenderers.Count > 0 && AreRendererComponentsEnabled(presentation.requiredRenderers);
        }
        return false;
    }

    private bool IsReadyForSharedSitePreparation(Vector2Int coordinate, RuntimeChunk chunk)
    {
        // note: The site has its own validated geometry; exposing a distant member does not request the central cell's terrain appearance or ecology.
        return chunk != null && chunk.physicalRepresentation && chunk.requiredContentReady && chunk.overlayReady &&
            chunk.state != YQSemanticChunkLifecycle.Failed && chunk.state != YQSemanticChunkLifecycle.Generating &&
            IsTraversableChunk(coordinate, chunk);
    }

    private void ReconcileSharedSiteVisuals(RuntimeChunk chunk, bool ordinaryVisualsEnabled)
    {
        if (chunk.record == null || chunk.sitePresentations.Count == 0)
            return;
        Vector2Int coordinate = new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ);
        bool ownerPublished = IsReadyForSharedSitePreparation(coordinate, chunk) && chunk.activationComplete && chunk.activeState == 1;
        for (int index = 0; index < chunk.sitePresentations.Count; index++)
        {
            SharedSitePresentation presentation = chunk.sitePresentations[index];
            // note: Exposure permission precedes renderer writes; the member's post-publication check cannot create a readiness cycle.
            bool expose = ownerPublished && IsCurrentSharedSitePresentation(chunk, presentation) &&
                (ordinaryVisualsEnabled || _sharedSiteVisibleIdentities.Contains(presentation.siteId)) &&
                presentation.root.activeInHierarchy && IsSharedSiteGeometryReady(presentation) &&
                !IsDestructiveOverlayState(presentation.target.appliedState);
            if (presentation.visualStateKnown && presentation.visualState == expose)
                continue;
            for (int rendererIndex = 0; rendererIndex < presentation.renderers.Count; rendererIndex++)
            {
                Renderer renderer = presentation.renderers[rendererIndex];
                if (renderer != null && renderer.enabled != expose)
                    renderer.enabled = expose;
            }
            presentation.visualStateKnown = true;
            presentation.visualState = expose;
        }
    }

    private static string ResolveGeneratedFeatureId(GeneratedSemanticChunkRecord record, string objectName)
    {
        if (record == null || string.IsNullOrWhiteSpace(objectName))
            return string.Empty;
        if (objectName.StartsWith("ContinuousLandmark_", StringComparison.Ordinal) && record.sites != null)
        {
            for (int index = 0; index < record.sites.Count; index++)
                if (record.sites[index] != null && !string.IsNullOrWhiteSpace(record.sites[index].siteId))
                    return record.sites[index].siteId;
        }
        bool water = objectName.StartsWith("ContinuousRiver_", StringComparison.Ordinal) ||
            objectName.StartsWith("ContinuousAreaWater_", StringComparison.Ordinal) ||
            objectName.StartsWith("YQ_V2Water_", StringComparison.Ordinal);
        bool route = objectName.StartsWith("ContinuousRoad_", StringComparison.Ordinal) ||
            objectName.IndexOf("Bridge", StringComparison.OrdinalIgnoreCase) >= 0;
        if (record.edgeContracts != null)
        {
            for (int edgeIndex = 0; edgeIndex < record.edgeContracts.Count; edgeIndex++)
            {
                GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[edgeIndex];
                if (edge == null)
                    continue;
                List<GeneratedSemanticChunkPortalRecord> portals = water ? edge.waterPortals : route ? edge.routePortals : null;
                if (portals == null)
                    continue;
                for (int portalIndex = 0; portalIndex < portals.Count; portalIndex++)
                    if (portals[portalIndex] != null && !string.IsNullOrWhiteSpace(portals[portalIndex].featureId))
                        return portals[portalIndex].featureId;
            }
        }
        return string.Empty;
    }

    private void ApplyFeatureOverlayToLoadedChunks(GeneratedSemanticFeatureOverlayRecord overlay)
    {
        if (overlay == null)
            return;
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
            ApplyFeatureOverlay(pair.Value, overlay, null);
    }

    public static void ReplayPersistedFeatureOverlaysForTarget(YQStreamedFeatureOverlayTarget target)
    {
        if (target == null)
            return;
        WorldStateManager manager = WorldStateManager.Instance;
        GeneratedWorldPlanRecord plan = manager != null && manager.State != null
            ? manager.State.generatedWorldPlan
            : null;
        if (plan == null || plan.semanticAuthority == null)
            return;
        plan.semanticAuthority.EnsureCollections();
        for (int index = 0; index < plan.semanticAuthority.featureOverlays.Count; index++)
            ApplyFeatureOverlayToSceneTarget(target, plan.semanticAuthority.featureOverlays[index]);
    }

    private static void ApplyFeatureOverlayToSceneTargets(GeneratedSemanticFeatureOverlayRecord overlay)
    {
        if (overlay == null)
            return;
        overlay.EnsureCollections();
        if (!string.Equals(overlay.schemaVersion, YQStateContract.FeatureOverlaySchemaVersion, StringComparison.Ordinal))
            return;
        YQStreamedFeatureOverlayTarget[] targets =
            UnityEngine.Object.FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < targets.Length; index++)
        {
            YQStreamedFeatureOverlayTarget target = targets[index];
            ApplyFeatureOverlayToSceneTarget(target, overlay);
        }
    }

    private static void ApplyFeatureOverlayToSceneTarget(
        YQStreamedFeatureOverlayTarget target,
        GeneratedSemanticFeatureOverlayRecord overlay)
    {
        if (target == null || overlay == null || !OverlayTargetMatches(target, overlay))
            return;
        bool disable = overlay.tombstone || IsDestructiveOverlayState(overlay.state);
        if (overlay.revision <= target.lastOverlayRevision)
        {
            // note: Reused hierarchies retain their in-memory revision across rebuild; reassert a destructive tombstone if reconstruction has resurrected the target.
            if (disable && target.gameObject.activeSelf)
                target.gameObject.SetActive(false);
            return;
        }
        target.lastOverlayRevision = overlay.revision;
        target.appliedState = overlay.state;
        target.appliedPayload = overlay.payload;
        // note: Scene-owned targets may not belong to a streamer chunk, so apply the accepted destructive state directly at the registered feature boundary.
        if (disable)
            target.gameObject.SetActive(false);
    }

    private void ApplyPersistedOverlaysToSceneTargets()
    {
        if (_plan == null || _plan.semanticAuthority == null)
            return;
        _plan.semanticAuthority.EnsureCollections();
        for (int index = 0; index < _plan.semanticAuthority.featureOverlays.Count; index++)
            ApplyFeatureOverlayToSceneTargets(_plan.semanticAuthority.featureOverlays[index]);
    }

    private void ApplyPersistedOverlaysToExistingChunks()
    {
        if (_plan == null || _plan.semanticAuthority == null)
            return;
        _plan.semanticAuthority.EnsureCollections();
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            for (int index = 0; index < _plan.semanticAuthority.featureOverlays.Count; index++)
                ApplyFeatureOverlay(pair.Value, _plan.semanticAuthority.featureOverlays[index], null);
        }
    }

    private void ApplyFeatureOverlays(RuntimeChunk chunk, GameObject root)
    {
        if (chunk == null)
            return;
        if (_plan == null || _plan.semanticAuthority == null)
        {
            chunk.overlayReady = true;
            return;
        }
        _plan.semanticAuthority.EnsureCollections();
        if (_plan.semanticAuthority.featureOverlays.Count == 0)
        {
            chunk.overlayReady = true;
            return;
        }
        YQStreamedFeatureOverlayTarget[] targets = root != null
            ? root.GetComponentsInChildren<YQStreamedFeatureOverlayTarget>(true)
            : new YQStreamedFeatureOverlayTarget[0];
        bool allApplicableOverlaysReady = true;
        for (int index = 0; index < _plan.semanticAuthority.featureOverlays.Count; index++)
        {
            GeneratedSemanticFeatureOverlayRecord overlay = _plan.semanticAuthority.featureOverlays[index];
            if (overlay == null || string.IsNullOrWhiteSpace(overlay.featureId))
                continue;
            bool featurePresent = chunk.record != null && chunk.record.featureIds != null &&
                chunk.record.featureIds.Contains(overlay.featureId);
            bool targetMatched = false;
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                if (targets[targetIndex] != null && OverlayTargetMatches(targets[targetIndex], overlay))
                {
                    targetMatched = true;
                    break;
                }
            }
            if (!featurePresent && !targetMatched)
                continue;
            ApplyFeatureOverlay(chunk, overlay, targets);
            bool applied = chunk.appliedOverlayRevisions.TryGetValue(overlay.featureId, out long appliedRevision) &&
                appliedRevision >= overlay.revision;
            if (!applied)
                allApplicableOverlaysReady = false;
        }
        // note: Publish the aggregate overlay stage only after every overlay that applies to this owner has a live replay receipt.
        chunk.overlayReady = allApplicableOverlaysReady;
    }

    private void ApplyFeatureOverlay(
        RuntimeChunk chunk,
        GeneratedSemanticFeatureOverlayRecord overlay,
        YQStreamedFeatureOverlayTarget[] targets)
    {
        if (chunk == null || chunk.record == null || overlay == null || string.IsNullOrWhiteSpace(overlay.featureId))
            return;
        overlay.EnsureCollections();
        if (!string.Equals(overlay.schemaVersion, YQStateContract.FeatureOverlaySchemaVersion, StringComparison.Ordinal))
            return;
        if (!string.IsNullOrWhiteSpace(overlay.receiptId) && chunk.appliedOverlayReceipts.Contains(overlay.receiptId))
            return;
        if (targets == null)
            targets = CollectFeatureOverlayTargets(chunk);
        bool featurePresent = chunk.record.featureIds != null && chunk.record.featureIds.Contains(overlay.featureId);
        bool matched = false;
        if (targets != null)
        {
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                YQStreamedFeatureOverlayTarget target = targets[targetIndex];
                if (target != null && OverlayTargetMatches(target, overlay))
                {
                    matched = true;
                    break;
                }
            }
        }
        if (!featurePresent && !matched)
            return;
        if (!matched)
        {
            // note: A semantic feature without a physical overlay target is an unapplied mutation, not a successful receipt; publication must fail explicitly.
            return;
        }
        if (chunk.appliedOverlayRevisions.TryGetValue(overlay.featureId, out long appliedRevision) &&
            overlay.revision <= appliedRevision)
            return;
        bool disable = overlay.tombstone || IsDestructiveOverlayState(overlay.state);
        if (targets != null)
        {
            for (int targetIndex = 0; targetIndex < targets.Length; targetIndex++)
            {
                YQStreamedFeatureOverlayTarget target = targets[targetIndex];
                if (target == null || !OverlayTargetMatches(target, overlay))
                    continue;
                target.lastOverlayRevision = overlay.revision;
                target.appliedState = overlay.state;
                target.appliedPayload = overlay.payload;
                if (disable)
                    target.gameObject.SetActive(false);
                else if (chunk.state != YQSemanticChunkLifecycle.Unloaded && chunk.state != YQSemanticChunkLifecycle.Failed)
                    target.gameObject.SetActive(true);
            }
        }
        chunk.appliedOverlayRevisions[overlay.featureId] = overlay.revision;
        if (!string.IsNullOrWhiteSpace(overlay.receiptId))
            chunk.appliedOverlayReceipts.Add(overlay.receiptId);
    }

    private static YQStreamedFeatureOverlayTarget[] CollectFeatureOverlayTargets(RuntimeChunk chunk)
    {
        List<YQStreamedFeatureOverlayTarget> targets = new List<YQStreamedFeatureOverlayTarget>();
        if (chunk == null || chunk.generatedObjects == null)
            return targets.ToArray();
        for (int index = 0; index < chunk.generatedObjects.Count; index++)
        {
            GameObject generated = chunk.generatedObjects[index];
            if (generated == null)
                continue;
            YQStreamedFeatureOverlayTarget[] found = generated.GetComponentsInChildren<YQStreamedFeatureOverlayTarget>(true);
            for (int foundIndex = 0; foundIndex < found.Length; foundIndex++)
                if (found[foundIndex] != null && !targets.Contains(found[foundIndex]))
                    targets.Add(found[foundIndex]);
        }
        return targets.ToArray();
    }

    private static bool OverlayTargetMatches(
        YQStreamedFeatureOverlayTarget target,
        GeneratedSemanticFeatureOverlayRecord overlay)
    {
        if (target == null || overlay == null)
            return false;
        if (string.Equals(target.featureId, overlay.featureId, StringComparison.Ordinal))
            return true;
        if (overlay.targetObjectIds == null)
            return false;
        for (int index = 0; index < overlay.targetObjectIds.Count; index++)
        {
            string targetId = overlay.targetObjectIds[index];
            if (!string.IsNullOrWhiteSpace(targetId) &&
                (string.Equals(target.objectId, targetId, StringComparison.Ordinal) ||
                 string.Equals(target.name, targetId, StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    private static bool IsDestructiveOverlayState(string state)
    {
        string normalized = (state ?? string.Empty).Trim().ToLowerInvariant();
        return normalized == "deleted" || normalized == "harvested" || normalized == "destroyed" || normalized == "tombstone";
    }

    private bool IsReadyForViewActivation(Vector2Int coordinate, RuntimeChunk chunk)
    {
        // note: A visible sector cannot certify a missing or still inactive central site hierarchy.
        return IsReadyForViewActivationCore(coordinate, chunk) && AreSharedSiteOwnersReady(coordinate);
    }

    private string DescribeViewActivationBlocker(Vector2Int coordinate, RuntimeChunk chunk)
    {
        // note: Report the exact failed readiness owner on the rare coverage-failure path without changing the publication gate.
        bool sharedAuthoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
        if (chunk == null) return "chunkMissing";
        if (!chunk.physicalRepresentation && !sharedAuthoredTerrain) return "physicalRepresentationMissing";
        if (!chunk.requiredContentReady) return "requiredContentPending";
        if (!chunk.overlayReady) return "overlayPending";
        if (!chunk.appearanceReady) return "appearancePending";
        if (!chunk.visualReady) return "visualPending";
        if (_terrainPainting.ContainsKey(coordinate)) return "terrainPainterActive";
        if (!chunk.requiredEcologyReady) return "ecologyPending";
        if (!sharedAuthoredTerrain && chunk.terrainReadiness < YQTerrainReadinessState.CollisionReady)
            return "terrainCollisionPending:" + chunk.terrainReadiness;

        if (_acceptedSpatialProjection != null)
        {
            _acceptedSpatialProjection.CollectSiteIndicesForCell(coordinate, Mathf.Max(32f, chunkWorldSize),
                _sharedSiteReadinessScratch);
            foreach (int index in _sharedSiteReadinessScratch)
            {
                YQSpatialMaterializationSiteV2 site = _acceptedSpatialProjection.GetSite(index);
                if (site.kind == YQSiteKindV2.Origin) continue;
                Vector2Int owner = ChunkFor(new Vector2(site.x, site.z));
                if (!_chunks.TryGetValue(owner, out RuntimeChunk ownerChunk) || ownerChunk == null)
                    return "sharedSiteOwnerMissing:" + site.siteId + "@" + owner;
                if (!IsReadyForSharedSitePreparation(owner, ownerChunk))
                    return "sharedSiteOwnerNotPrepared:" + site.siteId + "@" + owner;
                if (!IsSharedSitePresentationReady(ownerChunk, site.siteId))
                    return "sharedSitePresentationPending:" + site.siteId + "@" + owner;
            }
        }
        if (!chunk.activationComplete) return "activationIncomplete";
        if (chunk.activeState != 1) return "activationState:" + chunk.activeState;
        string rendererBlocker = DescribePublishedRendererBlocker(chunk);
        return string.IsNullOrEmpty(rendererBlocker) ? "unknown" : "renderer:" + rendererBlocker;
    }

    private bool IsReadyForViewActivationCore(Vector2Int coordinate, RuntimeChunk chunk)
    {
        using var lifecycleScope = LifecycleReadinessMarker.Auto();
        bool sharedAuthoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
        if (chunk == null || (!chunk.physicalRepresentation && !sharedAuthoredTerrain) || !chunk.requiredContentReady ||
            !chunk.overlayReady ||
            !chunk.appearanceReady || !chunk.visualReady || _terrainPainting.ContainsKey(coordinate))
            return false;
        // note: Generated ecology has the same publication boundary over borrowed origin terrain and continuation terrain; a shared collider cannot certify a pending cell-owned canopy layer.
        if (!chunk.requiredEcologyReady)
            return false;
        return IsChunkInsideAuthoredTerrain(coordinate) ||
            chunk.terrainReadiness >= YQTerrainReadinessState.CollisionReady;
    }

    private bool IsReadyForCanonicalPreparation(Vector2Int coordinate, RuntimeChunk chunk)
    {
        bool sharedAuthoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
        // note: Offscreen owners need traversable terrain and accepted semantic layers before release; terrain appearance stays deferred until camera prediction makes it a visible deadline.
        return chunk != null && (chunk.physicalRepresentation || sharedAuthoredTerrain) &&
            chunk.requiredContentReady && chunk.overlayReady && chunk.requiredEcologyReady &&
            IsTraversableChunk(coordinate, chunk);
    }

    private bool IsTraversableChunk(Vector2Int coordinate, RuntimeChunk chunk)
    {
        if (chunk == null)
            return false;
        // note: Movement trusts a synchronized canonical collider or its same-authority temporary replacement; semantic presentation keeps its independent readiness gates.
        if (IsChunkInsideAuthoredTerrain(coordinate))
            return IsTerrainColliderReady(_terrain);
        return (chunk.terrainReadiness >= YQTerrainReadinessState.CollisionReady &&
                HasPublishedTerrain(coordinate)) ||
            (_provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisional) &&
                IsTerrainColliderReady(provisional));
    }

    private bool IsFullyLoadedForView(Vector2Int coordinate, RuntimeChunk chunk)
    {
        // note: A readiness flag is not enough for camera publication; the owner must also have completed activation and have every streamed Terrain renderer enabled.
        if (!IsReadyForViewActivation(coordinate, chunk))
            return false;
        if (IsChunkInsideAuthoredTerrain(coordinate) && chunk != null && chunk.ownedObjects.Count == 0)
        {
            // note: The shared authored Terrain is already the visible/collidable owner for empty per-cell runtime records.
            return _terrain != null && _terrain.enabled && _terrain.gameObject.activeInHierarchy &&
                IsTerrainColliderReady(_terrain);
        }
        return
            chunk.activationComplete &&
            chunk.activeState == 1 &&
            ArePublishedTerrainRenderersEnabled(chunk);
    }

    private bool HasPendingHardViewActivation()
    {
        // note: Only accelerate owners that are already physically and visually ready; generation and painting retain their own bounded queues, and the live frustum is the strict deadline.
        foreach (Vector2Int coordinate in _guaranteedViewDemand)
        {
            if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
                continue;
            if (IsReadyForViewActivation(coordinate, chunk) && !IsFullyLoadedForView(coordinate, chunk))
                return true;
        }
        // note: A member's visibility deadline includes activation of its existing central site owner under the same frame allowance.
        foreach (Vector2Int coordinate in _sharedSiteVisibleOwners)
            if (NeedsHardViewActivation(coordinate))
                return true;
        return false;
    }

    // note: Structural owner changes invalidate cached component discovery without weakening live readiness checks.
    private void InvalidateRendererValidationCache(RuntimeChunk chunk)
    {
        if (chunk == null)
            return;
        chunk.rendererValidationCacheDirty = true;
        // note: Clear destroyed owner references immediately so retained semantic records do not pin stale Unity components after unload.
        chunk.rendererValidationCache?.Clear();
        chunk.terrainValidationCache?.Clear();
    }

    private string DescribePublishedRendererBlocker(RuntimeChunk chunk)
    {
        // note: Explain the last visual publication gate only when a view-ready owner fails its completeness check.
        if (chunk == null)
            return "missingOwner";

        for (int objectIndex = 0; objectIndex < chunk.generatedObjects.Count; objectIndex++)
        {
            GameObject generatedObject = chunk.generatedObjects[objectIndex];
            if (generatedObject != null && !generatedObject.activeInHierarchy)
                return "inactiveObject=" + generatedObject.name +
                    ":activeSelf=" + generatedObject.activeSelf;
            if (generatedObject == null)
                continue;

            _rendererValidationScratch.Clear();
            generatedObject.GetComponentsInChildren<Renderer>(true, _rendererValidationScratch);
            for (int rendererIndex = 0; rendererIndex < _rendererValidationScratch.Count; rendererIndex++)
            {
                Renderer renderer = _rendererValidationScratch[rendererIndex];
                if (renderer == null)
                    continue;
                if (!renderer.gameObject.activeInHierarchy)
                {
                    YQStreamedFeatureOverlayTarget tombstoneTarget =
                        renderer.GetComponentInParent<YQStreamedFeatureOverlayTarget>(true);
                    if (tombstoneTarget != null && !tombstoneTarget.gameObject.activeInHierarchy)
                        continue;
                    string blocker = "inactiveRenderer=" + renderer.name +
                        ":object=" + renderer.gameObject.name +
                        ":activeSelf=" + renderer.gameObject.activeSelf;
                    _rendererValidationScratch.Clear();
                    return blocker;
                }
                if (!renderer.enabled)
                {
                    string blocker = "disabledRenderer=" + renderer.name +
                        ":object=" + renderer.gameObject.name +
                        ":decorativeWork=" + chunk.decorativeWorkPhase;
                    _rendererValidationScratch.Clear();
                    return blocker;
                }
            }
            _rendererValidationScratch.Clear();

            Terrain terrain = generatedObject.GetComponent<Terrain>();
            if (terrain != null && (!terrain.enabled || !terrain.gameObject.activeInHierarchy))
                return "inactiveTerrain=" + terrain.name + ":enabled=" + terrain.enabled;
        }

        return "noInactiveRendererFound:decorativeWork=" + chunk.decorativeWorkId +
            ":phase=" + chunk.decorativeWorkPhase;
    }

    private bool ArePublishedTerrainRenderersEnabled(RuntimeChunk chunk)
    {
        if (chunk == null)
            return false;

        for (int index = 0; index < chunk.generatedObjects.Count; index++)
        {
            GameObject generatedObject = chunk.generatedObjects[index];
            if (generatedObject != null && !generatedObject.activeInHierarchy)
                return false;
        }

        // note: Ecology mutates the generated root asynchronously; inspect its current hierarchy with a reusable list until that worker is idle.
        if (chunk.decorativeWorkId != 0)
        {
            for (int index = 0; index < chunk.generatedObjects.Count; index++)
            {
                GameObject generatedObject = chunk.generatedObjects[index];
                if (generatedObject == null)
                    continue;
                _rendererValidationScratch.Clear();
                generatedObject.GetComponentsInChildren<Renderer>(true, _rendererValidationScratch);
                bool renderersEnabled = AreRendererComponentsEnabled(_rendererValidationScratch);
                _rendererValidationScratch.Clear();
                if (!renderersEnabled)
                    return false;

                // note: Most generated roots have no Terrain; avoid Unity's Editor-only missing-component allocation while keeping the same live readiness checks.
                generatedObject.TryGetComponent<Terrain>(out Terrain terrain);
                if (terrain != null && (!terrain.enabled || !terrain.gameObject.activeInHierarchy))
                    return false;
            }
            return true;
        }

        if (chunk.rendererValidationCacheDirty || chunk.rendererValidationCache == null || chunk.terrainValidationCache == null)
        {
            if (chunk.rendererValidationCache == null)
                chunk.rendererValidationCache = new List<Renderer>(64);
            else
                chunk.rendererValidationCache.Clear();
            if (chunk.terrainValidationCache == null)
                chunk.terrainValidationCache = new List<Terrain>(2);
            else
                chunk.terrainValidationCache.Clear();

            for (int index = 0; index < chunk.generatedObjects.Count; index++)
            {
                GameObject generatedObject = chunk.generatedObjects[index];
                if (generatedObject == null)
                    continue;
                _rendererValidationScratch.Clear();
                // note: Discover hierarchy components only when the published owner changes; live state still gets checked on every camera-readiness query.
                generatedObject.GetComponentsInChildren<Renderer>(true, _rendererValidationScratch);
                for (int rendererIndex = 0; rendererIndex < _rendererValidationScratch.Count; rendererIndex++)
                    chunk.rendererValidationCache.Add(_rendererValidationScratch[rendererIndex]);
                _rendererValidationScratch.Clear();

                // note: Cache only real Terrain components without allocating a missing-component diagnostic for semantic roots.
                generatedObject.TryGetComponent<Terrain>(out Terrain terrain);
                if (terrain != null)
                    chunk.terrainValidationCache.Add(terrain);
            }
            chunk.rendererValidationCacheDirty = false;
        }

        if (!AreRendererComponentsEnabled(chunk.rendererValidationCache))
            return false;
        for (int terrainIndex = 0; terrainIndex < chunk.terrainValidationCache.Count; terrainIndex++)
        {
            Terrain terrain = chunk.terrainValidationCache[terrainIndex];
            if (terrain != null && (!terrain.enabled || !terrain.gameObject.activeInHierarchy))
                return false;
        }
        // note: Authored owners and provider-only site owners may borrow shared terrain, while streamed terrain remains explicitly checked here.
        return true;
    }

    private static bool AreRendererComponentsEnabled(List<Renderer> renderers)
    {
        for (int rendererIndex = 0; rendererIndex < renderers.Count; rendererIndex++)
        {
            Renderer renderer = renderers[rendererIndex];
            if (renderer == null)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
            {
                // note: A durable destructive overlay intentionally removes its renderer; that absence is valid and must not strand the whole cell.
                YQStreamedFeatureOverlayTarget tombstoneTarget =
                    renderer.GetComponentInParent<YQStreamedFeatureOverlayTarget>(true);
                if (tombstoneTarget != null && !tombstoneTarget.gameObject.activeInHierarchy)
                    continue;
                return false;
            }
            if (!renderer.enabled)
                return false;
        }
        return true;
    }

    private void ApplyLifecycle()
    {
        // note: Release an anchor once it is no longer a live streamed unload, allowing the next eligible tile to become the bounded history witness.
        if (_unloadedHistoryAnchor.x != int.MinValue &&
            (!_chunks.TryGetValue(_unloadedHistoryAnchor, out RuntimeChunk anchor) || anchor == null ||
             anchor.state != YQSemanticChunkLifecycle.Unloaded || IsChunkInsideAuthoredTerrain(_unloadedHistoryAnchor)))
            _unloadedHistoryAnchor = new Vector2Int(int.MinValue, int.MinValue);
        _activeCount = 0;
        _retainedCount = 0;
        _physicalCount = 0;
        _unloadedCount = 0;
        if (_lifecycleBudgetFrame != Time.frameCount)
        {
            _lifecycleBudgetFrame = Time.frameCount;
            _lifecycleBudgetRemaining = LifecycleActivationObjectsPerFrame;
            _lifecycleDeadline = Time.realtimeSinceStartup + 0.002f;
            if (HasPendingHardViewActivation())
            {
                // note: Reserve the frame's extra work for the hard-view contract only; this publishes ready visible chunks without making the whole stream unbounded.
                _lifecycleBudgetRemaining = HardViewActivationObjectsPerFrame;
                _lifecycleDeadline = Time.realtimeSinceStartup + HardViewLifecycleBudgetSeconds;
            }
        }
        _lifecyclePending = false;
        int activationBudget = _lifecycleBudgetRemaining;
        using (LifecycleDemandScanMarker.Auto())
        {
            _lifecyclePriorityScratch.Clear();
            _hardViewLifecycleScratch.Clear();
            foreach (Vector2Int coordinate in _guaranteedViewDemand)
                if (_chunks.ContainsKey(coordinate))
                    _hardViewLifecycleScratch.Add(coordinate);
            foreach (Vector2Int coordinate in _sharedSiteVisibleOwners)
                if (!_guaranteedViewDemand.Contains(coordinate) && _chunks.ContainsKey(coordinate))
                    _hardViewLifecycleScratch.Add(coordinate);
            int hardViewCount = _hardViewLifecycleScratch.Count;
            if (hardViewCount > 0)
            {
                // note: Put view-ready owners with unfinished activation ahead of already-published frustum cells so rotation cannot spend the hard-view lifecycle slice while a ready cell remains hidden.
                SortHardViewLifecycleScratch();
                int pendingActivationCount = 0;
                while (pendingActivationCount < hardViewCount &&
                    NeedsHardViewActivation(_hardViewLifecycleScratch[pendingActivationCount]))
                    pendingActivationCount++;
                for (int index = 0; index < pendingActivationCount; index++)
                    _lifecyclePriorityScratch.Add(_hardViewLifecycleScratch[index]);

                // note: Retain rotating fairness for visible owners that do not currently need activation.
                int remainingHardViewCount = hardViewCount - pendingActivationCount;
                if (remainingHardViewCount > 0)
                {
                    int start = _hardViewLifecycleCursor % remainingHardViewCount;
                    for (int offset = 0; offset < remainingHardViewCount; offset++)
                        _lifecyclePriorityScratch.Add(
                            _hardViewLifecycleScratch[pendingActivationCount + (start + offset) % remainingHardViewCount]);
                    _hardViewLifecycleCursor = (start + 1) % remainingHardViewCount;
                }
                else
                {
                    _hardViewLifecycleCursor = 0;
                }
            }
            foreach (Vector2Int coordinate in _publicationVerificationDemand)
                if (!_guaranteedViewDemand.Contains(coordinate) && !_sharedSiteVisibleOwners.Contains(coordinate) && _chunks.ContainsKey(coordinate))
                    _lifecyclePriorityScratch.Add(coordinate);
            foreach (Vector2Int coordinate in _hardViewDemand)
                if (!_guaranteedViewDemand.Contains(coordinate) &&
                    !_sharedSiteVisibleOwners.Contains(coordinate) &&
                    !_publicationVerificationDemand.Contains(coordinate) &&
                    _chunks.ContainsKey(coordinate))
                    _lifecyclePriorityScratch.Add(coordinate);
            // note: Give far pending teardowns a rotating turn before ordinary retained owners so bounded lifecycle slices cannot starve an unload indefinitely.
            _unloadingLifecycleScratch.Clear();
            foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
                if (!_hardViewDemand.Contains(pair.Key) && !_sharedSiteVisibleOwners.Contains(pair.Key) && !_publicationVerificationDemand.Contains(pair.Key) && pair.Value != null &&
                    pair.Value.state == YQSemanticChunkLifecycle.Unloading)
                    _unloadingLifecycleScratch.Add(pair.Key);
            if (_unloadingLifecycleScratch.Count > 0)
            {
                int start = _unloadingLifecycleCursor % _unloadingLifecycleScratch.Count;
                for (int offset = 0; offset < _unloadingLifecycleScratch.Count; offset++)
                    _lifecyclePriorityScratch.Add(_unloadingLifecycleScratch[(start + offset) % _unloadingLifecycleScratch.Count]);
                _unloadingLifecycleCursor = (start + 1) % _unloadingLifecycleScratch.Count;
            }
            foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
                if (!_hardViewDemand.Contains(pair.Key) && !_sharedSiteVisibleOwners.Contains(pair.Key) && !_publicationVerificationDemand.Contains(pair.Key) && pair.Value != null &&
                    pair.Value.state != YQSemanticChunkLifecycle.Unloading)
                    _lifecyclePriorityScratch.Add(pair.Key);
        }
        // note: Exact live-frustum coordinates are processed before the preparation ring, and both remain ahead of ordinary retention in the same bounded pass.
        using (LifecycleOwnerPassMarker.Auto())
        for (int lifecycleIndex = 0; lifecycleIndex < _lifecyclePriorityScratch.Count; lifecycleIndex++)
        {
            Vector2Int lifecycleCoordinate = _lifecyclePriorityScratch[lifecycleIndex];
            if (!_chunks.TryGetValue(lifecycleCoordinate, out RuntimeChunk lifecycleChunk))
                continue;
            KeyValuePair<Vector2Int, RuntimeChunk> pair = new KeyValuePair<Vector2Int, RuntimeChunk>(lifecycleCoordinate, lifecycleChunk);
            RuntimeChunk chunk = pair.Value;
            YQSemanticChunkLifecycle previousState = chunk != null
                ? chunk.state
                : YQSemanticChunkLifecycle.Unseen;
            bool shouldBeActive;
            YQSemanticChunkLifecycle nextState;
            if (chunk.state == YQSemanticChunkLifecycle.Generating)
            {
                // note: A running materialization stays hidden until every visual stage completes; lifecycle distance cannot publish an in-flight object graph.
                nextState = YQSemanticChunkLifecycle.Generating;
                shouldBeActive = false;
                activationBudget = ApplyObjectsActiveSlice(chunk, shouldBeActive, activationBudget);
                chunk.state = nextState;
                _activeCount++;
                if (chunk.physicalRepresentation)
                    _physicalCount++;
                continue;
            }
            if (chunk.state == YQSemanticChunkLifecycle.Failed)
            {
                // note: Preserve a failed result until the frontier explicitly requeues it; distance refreshes must not hide diagnostic failures as generated cells.
                activationBudget = ApplyObjectsActiveSlice(chunk, false, activationBudget);
                _unloadedCount++;
                if (chunk.physicalRepresentation)
                    _physicalCount++;
                continue;
            }
            int distance = Mathf.Max(
                Mathf.Abs(pair.Key.x - _currentChunk.x),
                Mathf.Abs(pair.Key.y - _currentChunk.y));

            // note: Only the exact live frustum receives atomic visible activation; preparation owners remain ordinary physical work until they enter the frustum.
            bool hardViewRequired = _guaranteedViewDemand.Contains(pair.Key);
            bool hardViewPreparation = _hardViewDemand.Contains(pair.Key);
            bool highSpeedTurnBufferRequired = _provisionalGroundTurnBufferDemand.Contains(pair.Key);
            bool verificationPinned = _publicationVerificationDemand.Contains(pair.Key);
            if (_sharedSiteVisibleOwners.Contains(pair.Key))
            {
                // note: Activate a shared owner directly once its own content publishes, avoiding cycles between overlapping member footprints.
                nextState = YQSemanticChunkLifecycle.Active;
                shouldBeActive = hardViewRequired || verificationPinned
                    ? IsReadyForViewActivationCore(pair.Key, chunk)
                    : IsReadyForSharedSitePreparation(pair.Key, chunk);
                _activeCount++;
            }
            else if (hardViewRequired || verificationPinned)
            {
                // note: Visible cells and the single bounded replacement witness stay active until their complete publication is ready.
                nextState = YQSemanticChunkLifecycle.Active;
                shouldBeActive = IsReadyForViewActivation(pair.Key, chunk);
                _activeCount++;
            }
            else if (distance <= EffectiveVisualRadius)
            {
                // note: The active physical square uses required publication as its activation permission; visual paint can finish asynchronously without hiding the route/site owner.
                nextState = YQSemanticChunkLifecycle.Active;
                shouldBeActive = chunk.physicalRepresentation && chunk.requiredContentReady;
                _activeCount++;
            }
            else if (distance <= EffectiveVisualRetentionRadius || _siteTerrainDependencies.ContainsKey(pair.Key) ||
                     _sharedSiteOwnerDemand.Contains(pair.Key))
            {
                // note: Keep leased collision owners out of teardown while a loading site samples their footprint.
                nextState = YQSemanticChunkLifecycle.Retained;
                // note: Distant hidden owner preparation retains resources without activating the whole hierarchy before any member becomes visible.
                shouldBeActive = (distance <= EffectiveVisualRetentionRadius || _siteTerrainDependencies.ContainsKey(pair.Key)) &&
                    chunk.physicalRepresentation && chunk.requiredContentReady;
                _retainedCount++;
            }
            else if (hardViewPreparation || highSpeedTurnBufferRequired)
            {
                // note: Keep camera preparation and all-direction speed-turn owners resident without activating hidden objects; a returning demand must cancel stale teardown.
                nextState = YQSemanticChunkLifecycle.Retained;
                shouldBeActive = false;
                _retainedCount++;
            }
            else if (distance > EffectiveUnloadRadius && !hardViewRequired)
            {
                // note: Expose the teardown phase until the bounded activation pass has completed instead of claiming an unloaded owner early.
                bool deactivationComplete = chunk.activeState == 0 || chunk.ownedObjects.Count == 0;
                nextState = previousState == YQSemanticChunkLifecycle.Unloading && deactivationComplete
                    ? YQSemanticChunkLifecycle.Unloaded
                    : previousState == YQSemanticChunkLifecycle.Unloaded
                        ? YQSemanticChunkLifecycle.Unloaded
                        : YQSemanticChunkLifecycle.Unloading;
                shouldBeActive = false;
                _unloadedCount++;
                // note: Prefer an already materialized continuation tile so the anchor proves actual streamed ownership rather than an empty semantic record.
                if (_unloadedHistoryAnchor.x == int.MinValue && !IsChunkInsideAuthoredTerrain(pair.Key) &&
                    chunk.physicalRepresentation)
                    _unloadedHistoryAnchor = pair.Key;
            }
            else
            {
                nextState = YQSemanticChunkLifecycle.Generated;
                // note: Generated owners outside the visible radius retain semantic/physical state but remain inactive until they re-enter the active envelope.
                shouldBeActive = false;
            }

            if ((nextState == YQSemanticChunkLifecycle.Unloading || nextState == YQSemanticChunkLifecycle.Unloaded) &&
                previousState != YQSemanticChunkLifecycle.Unloading && previousState != YQSemanticChunkLifecycle.Unloaded)
            {
                // note: Unload cancels only unpublished terrain sampling for this owner; committed Unity terrain remains intact until explicit destruction.
                CancelTerrainPreparationForCoordinate(pair.Key);
            }
            chunk.state = nextState;
            // note: Fair-share hard-view activation advances every visible owner over successive frames while retaining one aggregate frame budget.
            int ownerBudget = hardViewRequired || verificationPinned || _sharedSiteVisibleOwners.Contains(pair.Key)
                ? Mathf.Min(activationBudget, HardViewActivationObjectsPerOwner)
                : activationBudget;
            int ownerRemaining = ApplyObjectsActiveSlice(chunk, shouldBeActive, ownerBudget);
            activationBudget -= ownerBudget - ownerRemaining;
            if (nextState == YQSemanticChunkLifecycle.Unloaded &&
                !IsChunkInsideAuthoredTerrain(pair.Key) &&
                chunk.generatedObjects.Count > 0 &&
                (chunk.activeState == 0 || chunk.ownedObjects.Count == 0))
            {
                // note: Release generated hierarchy even when content-only objects outlive their TerrainData; accepted records remain the regeneration authority.
                if (!TryDestroyGeneratedObjectsForStreaming(chunk))
                {
                    // note: A deferred owner remains physically resident and eligible for returning demand; do not report it Unloaded while its hierarchy still exists.
                    nextState = YQSemanticChunkLifecycle.Unloading;
                    chunk.state = nextState;
                }
            }
            if (nextState == YQSemanticChunkLifecycle.Unloading)
            {
                // note: Keep the lifecycle scheduler alive for the follow-up pass that promotes a fully deactivated owner to Unloaded.
                _lifecyclePending = true;
            }

            if (chunk.physicalRepresentation)
                _physicalCount++;
        }
        _lifecycleBudgetRemaining = activationBudget;
        _physicalCountRefreshFrame = Time.frameCount;
    }

    private void SortHardViewLifecycleScratch()
    {
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        for (int index = 1; index < _hardViewLifecycleScratch.Count; index++)
        {
            Vector2Int candidate = _hardViewLifecycleScratch[index];
            int previous = index - 1;
            while (previous >= 0 && CompareHardViewLifecycleDemand(
                candidate,
                _hardViewLifecycleScratch[previous],
                traversalVelocity) < 0)
            {
                _hardViewLifecycleScratch[previous + 1] = _hardViewLifecycleScratch[previous];
                previous--;
            }
            _hardViewLifecycleScratch[previous + 1] = candidate;
        }
    }

    private int CompareHardViewLifecycleDemand(
        Vector2Int left,
        Vector2Int right,
        Vector2 traversalVelocity)
    {
        bool leftNeedsActivation = NeedsHardViewActivation(left);
        bool rightNeedsActivation = NeedsHardViewActivation(right);
        if (leftNeedsActivation != rightNeedsActivation)
            return leftNeedsActivation ? -1 : 1;

        int deadlineCompare = CompareContentQueuePriority(left, right, traversalVelocity);
        return deadlineCompare != 0 ? deadlineCompare : CompareCoordinates(left, right);
    }

    private bool NeedsHardViewActivation(Vector2Int coordinate)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            return false;
        // note: Activation permission precedes the site's renderer receipt; only the owner's own visible cell requires full appearance/ecology preparation.
        bool ready = _sharedSiteVisibleOwners.Contains(coordinate)
            ? (_guaranteedViewDemand.Contains(coordinate) || _publicationVerificationDemand.Contains(coordinate)
                ? IsReadyForViewActivationCore(coordinate, chunk)
                : IsReadyForSharedSitePreparation(coordinate, chunk))
            : IsReadyForViewActivation(coordinate, chunk);
        if (!ready)
            return false;

        return !chunk.activationComplete || chunk.activeState != 1 || chunk.activationTarget == 0;
    }

    private int ApplyObjectsActiveSlice(RuntimeChunk chunk, bool active, int budget)
    {
        using var lifecycleScope = LifecycleActivationSliceMarker.Auto();
        if (chunk == null)
            return budget;
        Vector2Int coordinate = chunk.record != null
            ? new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ)
            : new Vector2Int(int.MinValue, int.MinValue);
        // note: A shared site's activation inherits its member's deadline; ordinary owner visuals keep their complete cell readiness gate.
        bool exactViewRequired = _guaranteedViewDemand.Contains(coordinate);
        bool sharedSiteRequired = _sharedSiteVisibleOwners.Contains(coordinate);
        bool hardViewRequired = exactViewRequired || sharedSiteRequired;
        bool hardViewReady = !hardViewRequired || (sharedSiteRequired
            ? (exactViewRequired || _publicationVerificationDemand.Contains(coordinate)
                ? IsReadyForViewActivationCore(coordinate, chunk)
                : IsReadyForSharedSitePreparation(coordinate, chunk))
            : IsReadyForViewActivation(coordinate, chunk));
        sbyte desired = (sbyte)(active ? 1 : 0);
        if (chunk.activeState == desired && chunk.activationTarget == -1)
        {
            // note: Opt-in replay captures a stale aggregate activation cache before renderer reconciliation can obscure its origin.
            if (_completeCellHistory != null && active && chunk.generationRoot != null && !chunk.generationRoot.activeSelf)
                ObserveCompleteCell(coordinate, "activationFastPathInactiveRoot", true);
            // note: Reconcile publication bookkeeping for an already-activated owner even after the shared frame budget is spent; otherwise a ready visible cell can stay hidden behind a stale completion flag.
            if (active)
                chunk.activationComplete = true;
            bool expose = active && hardViewReady && IsReadyForViewActivation(coordinate, chunk);
            SetChunkVisualsEnabled(chunk, expose);
            return budget;
        }

        if (hardViewRequired)
        {
            bool fullyPublished = active && IsReadyForViewActivation(coordinate, chunk) && chunk.activationComplete &&
                chunk.activeState == 1 && chunk.activationTarget == -1;
            // note: Keep a collision-ready ground renderer exposed for every exact-view cell even while aggregate budget defers optional object activation.
            SetChunkVisualsEnabled(chunk, fullyPublished);
        }
        // note: A ready visible cell uses its separate hard-view deadline even when background work has reached the shared slice ceiling.
        bool hardViewActivationReady = active && hardViewRequired && hardViewReady;
        if (!CanAdvanceAggregateWork("lifecycleActivation", hardViewActivationReady))
        {
            _lifecyclePending = true;
            return budget;
        }
        if (chunk.activeState != desired)
        {
            // note: Any activation target change revokes traversal publication until the entire owned hierarchy reaches the new state.
            chunk.activationComplete = false;
        }
        // note: Reverse a partially completed activation from the beginning; the cached activeState is not proof that every owned object reached that state.
        if (chunk.activationTarget != -1 && chunk.activationTarget != desired)
        {
            chunk.activationTarget = desired;
            chunk.activationCursor = 0;
            chunk.activationComplete = false;
        }
        if (!active)
            SetChunkVisualsEnabled(chunk, false);
        if (budget <= 0 || Time.realtimeSinceStartup >= _lifecycleDeadline)
        {
            _lifecyclePending = true;
            return budget;
        }
        if (chunk.activationTarget != desired)
        {
            chunk.activationTarget = desired;
            chunk.activationCursor = 0;
        }
        int start = chunk.activationCursor;
        ObserveCompleteCell(coordinate, "activationSliceBegin", true);
        int end = Mathf.Min(chunk.ownedObjects.Count, start + Mathf.Max(1, budget));
        int cursor = start;
        for (; cursor < end; cursor++)
        {
            // note: Imported object activation is indivisible, but the next object waits once either aggregate or lifecycle allowance is spent.
            if (Time.realtimeSinceStartup >= _lifecycleDeadline ||
                !CanAdvanceAggregateWork("lifecycleObjectActivation", hardViewActivationReady))
                break;
            float activationSliceStartedAt = Time.realtimeSinceStartup;
            GameObject item = chunk.ownedObjects[cursor];
            // note: Keep TerrainCollider objects active for collision while their Terrain renderer is independently gated by visual readiness.
            // note: An absent TerrainCollider is ordinary for content roots; preserve collision ownership without creating Editor missing-component diagnostics.
            bool keepCollisionActive = item != null && item.TryGetComponent<TerrainCollider>(out _);
            bool desiredActive = keepCollisionActive || active;
            if (item != null && item.activeSelf != desiredActive)
            {
                using (LifecycleObjectActivationMarker.Auto())
                    item.SetActive(desiredActive);
            }
            RecordAggregateWorkSlice(activationSliceStartedAt, "lifecycleObjectActivation");
        }
        chunk.activationCursor = cursor;
        ObserveCompleteCell(coordinate, "activationSliceEnd", true);
        int consumed = cursor - start;
        // note: Complete a chunk only after every owned object received the requested activation state.
        if (chunk.activationCursor >= chunk.ownedObjects.Count)
        {
            chunk.activeState = desired;
            chunk.activationTarget = -1;
            chunk.activationCursor = 0;
            chunk.activationComplete = active;
            chunk.publicationVersion++;
            ObserveCompleteCell(coordinate, "activationCompletedBeforeVisuals", true);
            // note: Renderer visibility follows the complete hard-view contract, never merely terrain, paint, or partial activation publication.
            // note: Exposure is driven by readiness before the renderer is enabled; the strict predicate is reserved for post-publication validation.
            bool expose = active && hardViewReady && IsReadyForViewActivation(coordinate, chunk) && chunk.activationComplete;
            SetChunkVisualsEnabled(chunk, expose);
            ObserveCompleteCell(coordinate, "activationCompletedAfterVisuals", true);
        }
        else
            _lifecyclePending = true;
        return Mathf.Max(0, budget - Mathf.Min(budget, consumed));
    }

    private static void SetStreamedTerrainRenderersEnabled(RuntimeChunk chunk, bool enabled)
    {
        if (chunk == null)
            return;
        for (int index = 0; index < chunk.generatedObjects.Count; index++)
        {
            GameObject generatedObject = chunk.generatedObjects[index];
            if (generatedObject == null)
                continue;
            // note: Preserve the renderer gate on real terrain while avoiding missing-component allocations on other roots.
            generatedObject.TryGetComponent<Terrain>(out Terrain terrain);
            if (terrain != null)
                terrain.enabled = enabled;
        }
    }

    private static void SetStreamedVisualsEnabled(RuntimeChunk chunk, bool enabled)
    {
        // note: Standalone publication gates still control every Terrain and Renderer in the owned hierarchy.
        SetStreamedVisualsExceptManagedTerrain(chunk, enabled, null);
    }

    private static void SetStreamedVisualsExceptManagedTerrain(RuntimeChunk chunk, bool enabled, Terrain independentlyGatedTerrain)
    {
        using var lifecycleScope = LifecycleHierarchyVisualGateMarker.Auto();
        if (chunk == null)
            return;
#if UNITY_EDITOR
        if (R2ObserveVisualGateWrites)
            R2VisualGateWriteCounts[0]++;
#endif
        for (int index = 0; index < chunk.generatedObjects.Count; index++)
        {
            GameObject generatedObject = chunk.generatedObjects[index];
            if (generatedObject == null)
                continue;
            // note: Gate every renderer in the owned hierarchy, not only Terrain, so trees, rocks, route meshes, sites, and partial roots cannot leak into the camera before the cell is certified.
#if UNITY_EDITOR
            if (R2ObserveVisualGateWrites)
                R2VisualGateWriteCounts[5]++;
#endif
            Renderer[] renderers;
            using (LifecycleRendererEnumerationMarker.Auto())
                renderers = generatedObject.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                // note: Cached accepted-site renderers use their own validated publication gate; terrain/routes/ecology keep this ordinary cell gate.
                if (renderer != null && !chunk.sitePresentationRenderers.Contains(renderer))
                {
                    // note: Preserve the full hierarchy gate while avoiding native renderer callbacks for an already-correct enabled state.
                    bool alreadyRequested;
                    using (LifecycleRendererStateReadMarker.Auto())
                        alreadyRequested = renderer.enabled == enabled;
#if UNITY_EDITOR
                    if (R2ObserveVisualGateWrites)
                    {
                        R2VisualGateWriteCounts[1]++;
                        if (alreadyRequested)
                            R2VisualGateWriteCounts[2]++;
                    }
#endif
                    if (!alreadyRequested)
                    {
                        using (LifecycleRendererStateWriteMarker.Auto())
                            renderer.enabled = enabled;
#if UNITY_EDITOR
                        if (R2ObserveVisualGateWrites)
                            R2VisualRendererWritesPerformed++;
#endif
                    }
                }
            }
            // note: The same hierarchy still receives its full visual gate; optional Terrain lookup does not allocate in the Editor.
            Terrain terrain;
            using (LifecycleTerrainLookupMarker.Auto())
                generatedObject.TryGetComponent<Terrain>(out terrain);
            if (terrain != null && terrain != independentlyGatedTerrain)
            {
                // note: Terrain keeps the same independent visual gate; unchanged state needs no repeated native setter.
                bool alreadyRequested;
                using (LifecycleTerrainStateReadMarker.Auto())
                    alreadyRequested = terrain.enabled == enabled;
#if UNITY_EDITOR
                if (R2ObserveVisualGateWrites)
                {
                    R2VisualGateWriteCounts[3]++;
                    if (alreadyRequested)
                        R2VisualGateWriteCounts[4]++;
                }
#endif
                if (!alreadyRequested)
                {
                    using (LifecycleTerrainStateWriteMarker.Auto())
                        terrain.enabled = enabled;
#if UNITY_EDITOR
                    if (R2ObserveVisualGateWrites)
                        R2VisualTerrainWritesPerformed++;
#endif
                }
            }
        }
    }

    private bool ShouldExposeTerrainPreview(RuntimeChunk chunk)
    {
        if (chunk == null || chunk.record == null ||
            chunk.terrainReadiness < YQTerrainReadinessState.CollisionReady)
            return false;
        Vector2Int coordinate = new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ);
        if (!_guaranteedViewDemand.Contains(coordinate) || !HasPublishedTerrain(coordinate))
            return false;
        return _extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) &&
            terrain != null && terrain.gameObject.activeInHierarchy;
    }

    private void SetChunkVisualsEnabled(RuntimeChunk chunk, bool enabled)
    {
        using var lifecycleScope = LifecycleVisualReconciliationMarker.Auto();
        if (chunk == null)
            return;

        bool visualStateChanged = !chunk.visualStateKnown || chunk.visualState != enabled;
        if (visualStateChanged)
        {
            // note: The mapped Terrain receives its final existing full/preview gate below; avoid hiding it here only to re-enable the same permitted preview in this call.
            Terrain independentlyGatedTerrain = null;
            if (chunk.record != null)
                _extendedTerrainTiles.TryGetValue(new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ), out independentlyGatedTerrain);
            SetStreamedVisualsExceptManagedTerrain(chunk, enabled, independentlyGatedTerrain);
            chunk.visualState = enabled;
            chunk.visualStateKnown = true;
        }
        ReconcileSharedSiteVisuals(chunk, enabled);

        // note: Keep synchronized same-authority ground visible in the live frustum while paint or ecology finishes; all feature renderers retain the strict cell publication gate.
        if (chunk.record != null)
        {
            Vector2Int coordinate = new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ);
            bool fullTerrainVisible = false;
            if (_extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) && terrain != null)
            {
                fullTerrainVisible = enabled || ShouldExposeTerrainPreview(chunk);
                if (terrain.enabled != fullTerrainVisible)
                {
                    terrain.enabled = fullTerrainVisible;
#if UNITY_EDITOR
                    if (R2ObserveVisualGateWrites)
                        R2ManagedTerrainWritesPerformed++;
#endif
                }
            }
            if (_provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisional) && provisional != null)
            {
                bool provisionalVisible = !fullTerrainVisible && _guaranteedViewDemand.Contains(coordinate) &&
                    provisional.gameObject.activeInHierarchy;
                if (provisional.enabled != provisionalVisible)
                    provisional.enabled = provisionalVisible;
                if (fullTerrainVisible)
                    RetireProvisionalGroundTile(coordinate, chunk);
            }
        }
    }

    private void RetireProvisionalGroundTile(Vector2Int coordinate, RuntimeChunk owner)
    {
        if (!_provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisional) || provisional == null)
        {
            _provisionalGroundTiles.Remove(coordinate);
            return;
        }

        // note: Canonical publication takes over the coordinate; retain the disabled native Terrain for a later safety tile at the moving frontier.
        RecycleProvisionalGroundTile(coordinate, owner, provisional);
    }

    private void RecycleProvisionalGroundTile(
        Vector2Int coordinate,
        RuntimeChunk owner,
        Terrain provisional)
    {
        _provisionalGroundTiles.Remove(coordinate);
        if (provisional == null)
            return;

        GameObject tileObject = provisional.gameObject;
        if (_completeCellHistory != null && owner != null && owner.record != null)
            ObserveCompleteCell(new Vector2Int(owner.record.chunkX, owner.record.chunkZ), "provisionalRetirementBefore", true);
        TerrainData data = provisional.terrainData;
        TerrainCollider collider = provisional.GetComponent<TerrainCollider>();
        provisional.SetNeighbors(null, null, null, null);
        provisional.enabled = false;
        if (collider != null)
            collider.enabled = false;
        if (tileObject != null)
            tileObject.SetActive(false);
        owner?.generatedObjects.Remove(tileObject);
        InvalidateRendererValidationCache(owner);
        RemoveOwnedObject(owner, tileObject);
        if (owner != null)
            owner.visualStateKnown = false;

        if (tileObject != null && data != null &&
            data.heightmapResolution == ProvisionalGroundHeightmapResolution &&
            _provisionalGroundPool.Count < MaximumPooledProvisionalGroundTiles)
        {
            tileObject.name = "YQ_PROVISIONAL_GROUND_POOL_" + _provisionalGroundPool.Count;
            _provisionalGroundPool.Push(provisional);
            return;
        }

        if (tileObject != null)
            Destroy(tileObject);
        if (data != null)
            Destroy(data);
    }

    private void DestroyProvisionalGroundPool()
    {
        // note: Profile/world rebind and streamer teardown discard pooled native data so no stale terrain can cross an authority epoch.
        while (_provisionalGroundPool.Count > 0)
        {
            Terrain terrain = _provisionalGroundPool.Pop();
            if (terrain == null)
                continue;
            TerrainData data = terrain.terrainData;
            terrain.SetNeighbors(null, null, null, null);
            Destroy(terrain.gameObject);
            if (data != null)
                Destroy(data);
        }
    }

    private RuntimeChunk GetRuntimeChunk(Vector2Int coordinate)
    {
        return _chunks.TryGetValue(coordinate, out RuntimeChunk chunk) ? chunk : null;
    }

    private RuntimeChunk GetOrCreateChunk(Vector2Int coordinate)
    {
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk existing))
            return existing;

        float semanticOwnerStarted = Time.realtimeSinceStartup;
        GeneratedSemanticChunkRecord persisted = FindPersistedChunk(coordinate);
        GeneratedSemanticChunkRecord record = persisted ?? BuildSemanticRecord(coordinate);
        bool authoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
        bool acceptedStructuralContentPending = authoredTerrain &&
            HasAcceptedAuthoredStructuralDemand(record, coordinate);
        RuntimeChunk created = new RuntimeChunk
        {
            record = record,
            state = persisted != null
                ? ParseState(persisted.lifecycleState)
                : YQSemanticChunkLifecycle.SemanticallyPlanned,
            publicationVersion = 1,
            // note: Authored ground alone cannot certify accepted V2 routes/sites before this cell's demanded structural owner has materialized.
            requiredContentReady = authoredTerrain && !acceptedStructuralContentPending,
            appearanceReady = authoredTerrain,
            overlayReady = authoredTerrain,
            requiredEcologyReady = authoredTerrain,
            activationComplete = authoredTerrain,
            activeState = authoredTerrain ? (sbyte)1 : (sbyte)-1
        };
        // note: Existing authored cells inherit collision readiness from the single origin Terrain; continuation cells begin unmaterialized.
        created.terrainReadiness = authoredTerrain && IsTerrainColliderReady(_terrain)
            ? YQTerrainReadinessState.CollisionReady
            : YQTerrainReadinessState.None;
        // note: Authored origin terrain is both collision and visual-ready; streamed cells wait for their complete publication pipeline.
        created.visualReady = created.appearanceReady &&
            IsTerrainColliderReady(_terrain);
        created.record.EnsureCollections();
        _chunks.Add(coordinate, created);
        _persistedChunkIndex[coordinate] = created.record;
        if (persisted == null)
        {
            _plan.semanticChunks.Add(created.record);
        }
        RecordStreamingStage("predictiveTerrainAdmission.semanticOwner", semanticOwnerStarted);
        return created;
    }

    private void BuildSemanticIndex()
    {
        _chunks.Clear();
        _persistedChunkIndex.Clear();
        for (int i = 0; i < _plan.semanticChunks.Count; i++)
        {
            GeneratedSemanticChunkRecord record = _plan.semanticChunks[i];
            if (record == null)
                continue;
            record.EnsureCollections();
            Vector2Int coordinate = new Vector2Int(record.chunkX, record.chunkZ);
            bool authoredTerrain = IsChunkInsideAuthoredTerrain(coordinate);
            bool acceptedStructuralContentPending = authoredTerrain &&
                HasAcceptedAuthoredStructuralDemand(record, coordinate);
            _persistedChunkIndex[coordinate] = record;
            _chunks[coordinate] = new RuntimeChunk
            {
                record = record,
                state = ParseState(record.lifecycleState),
                publicationVersion = 1,
                // note: Rebuilt owners must retain the same structural admission barrier as first-time owners until accepted V2 content is live again.
                requiredContentReady = authoredTerrain && !acceptedStructuralContentPending,
                appearanceReady = authoredTerrain,
                overlayReady = authoredTerrain,
                requiredEcologyReady = authoredTerrain,
                activationComplete = authoredTerrain,
                activeState = authoredTerrain ? (sbyte)1 : (sbyte)-1,
                // note: Rebuilt runtime indexes recover the physical state from current Unity objects instead of persisting transient readiness.
                terrainReadiness = authoredTerrain && IsTerrainColliderReady(_terrain)
                    ? YQTerrainReadinessState.CollisionReady
                    : YQTerrainReadinessState.None,
                // note: Rebuilt visual readiness starts conservative and is promoted only by a live paint or site publication pass.
                visualReady = authoredTerrain && IsTerrainColliderReady(_terrain)
            };
        }
    }

    private bool HasAcceptedAuthoredStructuralDemand(
        GeneratedSemanticChunkRecord record,
        Vector2Int coordinate)
    {
        if (record == null || !YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan))
            return false;
        // note: Use the same canonical demand predicate as materialization so authored terrain is never promoted ahead of its accepted V2 structural projection.
        return YQContinuousWorldFeatureMaterializer.HasAcceptedStructuralDemand(
            _plan,
            record,
            coordinate,
            Mathf.Max(32f, chunkWorldSize),
            _terrain);
    }

    private void AssignExistingPhysicalObjects()
    {
        if (_worldRoot == null)
            return;

        CollectBoundedPhysicalObjects(_worldRoot);
    }

    private void RegisterOwnedObject(RuntimeChunk chunk, GameObject item)
    {
        if (chunk == null || item == null || chunk.ownedObjects.Contains(item))
            return;

        // note: Track the aggregate activation state while ownership is collected, allowing an all-active chunk to skip lifecycle work.
        sbyte itemState = (sbyte)(item.activeSelf ? 1 : 0);
        if (chunk.ownedObjects.Count == 0)
            chunk.activeState = itemState;
        else if (chunk.activeState != itemState)
            chunk.activeState = -1;
        chunk.ownedObjects.Add(item);
        if (_completeCellHistory != null && chunk.record != null)
            ObserveCompleteCell(new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ), "ownedAdded:" + item.name, true);
    }

    private void RemoveOwnedObject(RuntimeChunk chunk, GameObject item)
    {
        if (chunk == null)
            return;
        RemoveOwnedObjectAt(chunk, chunk.ownedObjects.IndexOf(item));
    }

    private void RemoveOwnedObjectAt(RuntimeChunk chunk, int index)
    {
        if (index < 0 || index >= chunk.ownedObjects.Count)
            return;
        GameObject item = chunk.ownedObjects[index];
        if (_completeCellHistory != null && chunk.record != null)
            ObserveCompleteCell(new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ), "ownedRemoving:index=" + index + ":" + (item != null ? item.name : "null"), true);
        // note: Removing an already-processed entry shifts the next unprocessed object left; retain that object as the next activation/deactivation slice target.
        chunk.activationCursor = RemoveOwnedActivationEntry(chunk.ownedObjects, index, chunk.activationCursor);
        if (_completeCellHistory != null && chunk.record != null)
            ObserveCompleteCell(new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ), "ownedRemoved:index=" + index, true);
    }

    internal static int RemoveOwnedActivationEntry(List<GameObject> ownedObjects, int index, int activationCursor)
    {
        if (ownedObjects == null || index < 0 || index >= ownedObjects.Count)
            return activationCursor;
        // note: The cursor denotes a processed prefix, so only a removal inside that prefix changes its length; the next entry remains eligible before completion can be published.
        ownedObjects.RemoveAt(index);
        return index < activationCursor ? activationCursor - 1 : activationCursor;
    }

    private void PruneDestroyedOwnedObjects(RuntimeChunk chunk)
    {
        // note: Unity-destroyed entries obey the same cursor contract as explicitly retired terrain/content owners.
        for (int index = chunk.ownedObjects.Count - 1; index >= 0; index--)
            if (chunk.ownedObjects[index] == null)
                RemoveOwnedObjectAt(chunk, index);
    }

    private void CollectBoundedPhysicalObjects(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child == null || child == transform ||
                child.name == TerrainObjectName ||
                child.name.IndexOf("Sun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                child.name.IndexOf("Underwater", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            // note: An accepted compiled site remains one owner even when its first renderer or member assembly lies in another cell.
            YQCompiledWorldSiteInstance directSite = child.GetComponent<YQCompiledWorldSiteInstance>();
            YQStreamedFeatureOverlayTarget directSiteTarget = directSite != null ? child.GetComponent<YQStreamedFeatureOverlayTarget>() : null;
            YQSpatialMaterializationSiteV2 acceptedSite = default;
            bool acceptedSiteRoot = directSiteTarget != null && _acceptedSpatialProjection != null &&
                (_acceptedSpatialProjection.TryGetSiteBySiteId(directSiteTarget.featureId, out acceptedSite) ||
                 _acceptedSpatialProjection.TryGetSiteBySemanticId(directSiteTarget.featureId, out acceptedSite)) &&
                acceptedSite.kind != YQSiteKindV2.Origin;
            Vector3 center = child.position;
            Bounds aggregateBounds = new Bounds(center, Vector3.zero);
            bool hasBounds = false;
            Renderer renderer = child.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                center = renderer.bounds.center;
                aggregateBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                Collider collider = child.GetComponentInChildren<Collider>();
                if (collider != null)
                {
                    center = collider.bounds.center;
                    aggregateBounds = collider.bounds;
                    hasBounds = true;
                }
            }

            if (acceptedSiteRoot)
                center = new Vector3(acceptedSite.x, child.position.y, acceptedSite.z);
            // note: Whole-world aggregates recurse to bounded descendants; an accepted compiled root keeps its single canonical site owner.
            if (!acceptedSiteRoot && hasBounds && (aggregateBounds.size.x > chunkWorldSize * 2f || aggregateBounds.size.z > chunkWorldSize * 2f))
            {
                CollectBoundedPhysicalObjects(child);
                continue;
            }

            if (!acceptedSiteRoot && !hasBounds && child.childCount > 0)
            {
                CollectBoundedPhysicalObjects(child);
                continue;
            }

            RuntimeChunk chunk = GetOrCreateChunk(ChunkFor(new Vector2(center.x, center.z)));
            RegisterOwnedObject(chunk, child.gameObject);
            chunk.physicalRepresentation = true;
            chunk.state = YQSemanticChunkLifecycle.Generated;
            // note: Existing authored content is eligible immediately; streamed provider roots must prove their own loaded state before publication.
            YQCompiledWorldSiteInstance site = directSite != null ? directSite : child.GetComponentInChildren<YQCompiledWorldSiteInstance>(true);
            Vector2Int coordinate = ChunkFor(new Vector2(center.x, center.z));
            // note: A loaded provider cannot bypass pending terrain paint or an explicit appearance-stage hold.
            bool terrainPublished = IsChunkInsideAuthoredTerrain(coordinate) || HasPublishedTerrain(coordinate);
            chunk.requiredContentReady = true;
            // note: Recovered physical providers must replay applicable persisted overlays before they can re-enter the publication contract.
            ApplyFeatureOverlays(chunk, child.gameObject);
            if (!chunk.overlayReady)
            {
                chunk.state = YQSemanticChunkLifecycle.Failed;
                chunk.failureReason = "applicable feature overlay was not applied";
                continue;
            }
            chunk.appearanceReady = chunk.appearanceReady ||
                (site != null && site.IsLoaded && terrainPublished && !_terrainPainting.ContainsKey(coordinate) &&
                 !IsPublicationVerificationStageHeld(coordinate, YQPublicationVerificationStage.Appearance));
            chunk.visualReady = chunk.appearanceReady;
            // note: A preloaded site may never register again; cache its published hierarchy before the first ordinary streamer hide pass.
            if (acceptedSiteRoot)
                CacheSharedSitePresentation(chunk, directSiteTarget, acceptedSite);
        }
    }

    private GeneratedSemanticChunkRecord BuildSemanticRecord(Vector2Int coordinate)
    {
        bool canonicalV2 = YQWorldGenerationArchitecture.UsesV2SpatialRuntimeFor(_plan);
        float centerX = WorldGridOrigin + coordinate.x * chunkWorldSize + chunkWorldSize * 0.5f;
        float centerZ = WorldGridOrigin + coordinate.y * chunkWorldSize + chunkWorldSize * 0.5f;
        GeneratedSemanticChunkRecord record = new GeneratedSemanticChunkRecord
        {
            chunkId = (_plan.worldSeed ?? string.Empty) + ":chunk:" + coordinate.x + ":" + coordinate.y,
            worldSeed = _plan.worldSeed,
            chunkX = coordinate.x,
            chunkZ = coordinate.y,
            deterministicSeed = StableHash((_plan.worldSeed ?? string.Empty) + "|chunk|" + coordinate.x + "|" + coordinate.y).ToString("X8"),
            lifecycleState = YQSemanticChunkLifecycle.SemanticallyPlanned.ToString(),
            // note: The continuation authority assigns the deterministic dominant biome below; avoid sampling the same three fields twice for every new cell.
            biome = "forest",
            parentRegionId = canonicalV2 && _continuousAuthority != null
                ? "world"
                : ResolveParentRegion(centerX, centerZ)
        };

        if (!canonicalV2)
        {
            // note: V2 cell projection below already supplies accepted identities; avoid rescanning legacy routes and full V2 polylines on every streamed cell.
            float half = chunkWorldSize * 0.5f;
            AddIntersectingFeatures(record, centerX, centerZ, half);
        }
        _continuousAuthority?.PopulateCellRecord(record, coordinate, chunkWorldSize, _plan);
        // note: Persist structural continuation identities alongside the cell so save/reload and site queries can discover roads and waterways without inspecting scene objects.
        AddPortalFeatureIds(record);
        CanonicalizeAcceptedProjectionFeatureMetadata(record, coordinate);
        return record;
    }

    private static void AddPortalFeatureIds(GeneratedSemanticChunkRecord record)
    {
        if (record?.edgeContracts == null)
            return;
        // note: Portal feature IDs are authoritative, including accepted terminal continuations; generic IDs remain only for synthetic fallback portals.
        for (int edgeIndex = 0; edgeIndex < record.edgeContracts.Count; edgeIndex++)
        {
            GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[edgeIndex];
            if (edge == null)
                continue;
            AddPortalFeatureIds(record.featureIds, edge.routePortals);
            AddPortalFeatureIds(record.featureIds, edge.waterPortals);
        }
    }

    private static void AddPortalFeatureIds(List<string> featureIds, List<GeneratedSemanticChunkPortalRecord> portals)
    {
        if (featureIds == null || portals == null)
            return;
        for (int portalIndex = 0; portalIndex < portals.Count; portalIndex++)
        {
            GeneratedSemanticChunkPortalRecord portal = portals[portalIndex];
            if (portal != null)
                AddUnique(featureIds, portal.featureId);
        }
    }

    private void AddIntersectingFeatures(GeneratedSemanticChunkRecord record, float centerX, float centerZ, float half)
    {
        if (_plan.spatialPlan == null)
            return;

        for (int i = 0; i < _plan.spatialPlan.macroFeatures.Count; i++)
        {
            GeneratedSpatialFeatureRecord feature = _plan.spatialPlan.macroFeatures[i];
            if (feature == null || !Intersects(feature.centerX, feature.centerZ, feature.radiusX, feature.radiusZ, centerX, centerZ, half))
                continue;
            AddUnique(record.featureIds, feature.featureId);
            AddBorderContracts(record, feature.featureId, feature.centerX, feature.centerZ, feature.radiusX, feature.radiusZ, centerX, centerZ, half);
        }

        for (int i = 0; i < _plan.spatialPlan.locations.Count; i++)
        {
            GeneratedSpatialLocationRecord location = _plan.spatialPlan.locations[i];
            if (location == null || Vector2.Distance(new Vector2(location.worldX, location.worldZ), new Vector2(centerX, centerZ)) > half * 1.4143f + location.footprintRadius)
                continue;
            AddUnique(record.featureIds, location.locationId);
        }

        for (int i = 0; i < _plan.spatialPlan.routes.Count; i++)
        {
            GeneratedSpatialRouteRecord route = _plan.spatialPlan.routes[i];
            if (route == null || route.waypoints == null)
                continue;
            for (int p = 0; p < route.waypoints.Count; p++)
            {
                GeneratedSpatialPointRecord point = route.waypoints[p];
                if (point != null && Mathf.Abs(point.x - centerX) <= half && Mathf.Abs(point.z - centerZ) <= half)
                {
                    AddUnique(record.featureIds, route.routeId);
                    break;
                }
            }
        }

        // note: Accepted V2 hydrology, routes, sites, and terrain fields extend the same feature graph so chunk semantics follow the locked spatial authority.
        if (_acceptedBlueprint != null)
        {
            AddAcceptedFeatureMetadata(record, _acceptedBlueprint, centerX, centerZ, half);
            CanonicalizeAcceptedProjectionFeatureMetadata(record, new Vector2Int(record.chunkX, record.chunkZ));
        }
    }

    private void RefreshAcceptedFeatureMetadata(
        GeneratedSemanticChunkRecord record,
        Vector2Int coordinate,
        YQSpatialBlueprintV2 previousBlueprint,
        YQSpatialBlueprintV2 currentBlueprint)
    {
        if (record == null)
            return;
        record.EnsureCollections();
        float safeSize = Mathf.Max(32f, chunkWorldSize);
        float half = safeSize * 0.5f;
        float centerX = WorldGridOrigin + coordinate.x * safeSize + half;
        float centerZ = WorldGridOrigin + coordinate.y * safeSize + half;
        if (previousBlueprint != null)
        {
            // note: Reconstruct prior contributions spatially instead of guessing ownership from legacy strings or clearing unrelated custom metadata.
            GeneratedSemanticChunkRecord previousContribution = new GeneratedSemanticChunkRecord();
            previousContribution.EnsureCollections();
            AddAcceptedFeatureMetadata(previousContribution, previousBlueprint, centerX, centerZ, half);
            RemoveExactValues(record.featureIds, previousContribution.featureIds);
            RemoveExactValues(record.borderContracts, previousContribution.borderContracts);
        }
        if (currentBlueprint != null)
        {
            // note: A protected save can retain accepted route IDs from an older cell assignment; remove the current contract's full identity universe before reprojecting ownership for this cell.
            GeneratedSemanticChunkRecord currentIdentityUniverse = new GeneratedSemanticChunkRecord();
            currentIdentityUniverse.EnsureCollections();
            AddAcceptedFeatureIdentityUniverse(currentIdentityUniverse, currentBlueprint);
            RemoveExactValues(record.featureIds, currentIdentityUniverse.featureIds);
        }
        AddAcceptedFeatureMetadata(record, currentBlueprint, centerX, centerZ, half);
        CanonicalizeAcceptedProjectionFeatureMetadata(record, coordinate);
    }

    private void CanonicalizeAcceptedProjectionFeatureMetadata(
        GeneratedSemanticChunkRecord record,
        Vector2Int coordinate)
    {
        if (record == null || _plan == null ||
            !YQSpatialMaterializationResolverV2.TryGetPrepared(
                _plan,
                out YQPreparedSpatialMaterializationV2 prepared,
                out _))
            return;

        // note: Remove every accepted physical identity before re-adding only the canonical cell projection; the prepared V2 plan caches this immutable identity set so admission does not rebuild it per cell.
        for (int featureIndex = record.featureIds.Count - 1; featureIndex >= 0; featureIndex--)
            if (prepared.IsAcceptedFeatureIdentity(record.featureIds[featureIndex]))
                record.featureIds.RemoveAt(featureIndex);

        // note: Persist route IDs only for accepted spans that the physical materializer will actually consume in this cell.
        List<int> acceptedRouteIndices = new List<int>();
        YQContinuousWorldFeatureAuthority.GetAcceptedRouteIndicesForCell(
            prepared, coordinate, Mathf.Max(32f, chunkWorldSize), acceptedRouteIndices);
        for (int index = 0; index < acceptedRouteIndices.Count; index++)
        {
            YQSpatialMaterializationRouteV2 route = prepared.GetRoute(acceptedRouteIndices[index]);
            AddUnique(record.featureIds, route.routeId);
            AddUnique(record.featureIds, route.sourceSemanticRouteId);
        }

        // note: Apply the same canonical ownership rule to water identities so a stale river label cannot reject an otherwise valid visible owner.
        List<int> acceptedWaterIndices = new List<int>();
        YQContinuousWorldFeatureAuthority.GetAcceptedWaterIndicesForCell(
            prepared, coordinate, Mathf.Max(32f, chunkWorldSize), acceptedWaterIndices);
        for (int index = 0; index < acceptedWaterIndices.Count; index++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(acceptedWaterIndices[index]);
            AddUnique(record.featureIds, water.hydrologyId);
        }
    }

    private static void AddAcceptedFeatureIdentityUniverse(
        GeneratedSemanticChunkRecord record,
        YQSpatialBlueprintV2 blueprint)
    {
        if (record == null || blueprint == null)
            return;
        record.EnsureCollections();
        blueprint.EnsureCollections();
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 feature = blueprint.hydrology[index];
            if (feature != null)
                AddUnique(record.featureIds, feature.hydrologyId);
        }
        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            if (route != null)
                AddUnique(record.featureIds, route.routeId);
        }
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 site = blueprint.sites[index];
            if (site != null)
                AddUnique(record.featureIds, site.siteId);
        }
    }

    private static void AddAcceptedFeatureMetadata(
        GeneratedSemanticChunkRecord record,
        YQSpatialBlueprintV2 blueprint,
        float centerX,
        float centerZ,
        float half)
    {
        if (record == null || blueprint == null)
            return;
        record.EnsureCollections();
        blueprint.EnsureCollections();
        for (int index = 0; index < blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 feature = blueprint.hydrology[index];
            float halfWidth = feature != null ? Mathf.Max(0f, feature.nominalWidth * 0.5f) : 0f;
            if (feature == null || !PolylineIntersectsCell(feature.controlPoints, centerX, centerZ, half, halfWidth))
                continue;
            AddUnique(record.featureIds, feature.hydrologyId);
            AddPolylineBorderContracts(record, feature.hydrologyId, feature.controlPoints, centerX, centerZ, half, halfWidth);
        }
        for (int index = 0; index < blueprint.routes.Count; index++)
        {
            YQRouteCorridorV2 route = blueprint.routes[index];
            // note: Road footprint includes both the paved/travel width and its authored shoulders when deciding chunk ownership.
            float halfWidth = route != null ? Mathf.Max(0f, route.width * 0.5f) + Mathf.Max(0f, route.shoulderWidth) : 0f;
            if (route == null || !PolylineIntersectsCell(route.controlPoints, centerX, centerZ, half, halfWidth))
                continue;
            AddUnique(record.featureIds, route.routeId);
            AddPolylineBorderContracts(record, route.routeId, route.controlPoints, centerX, centerZ, half, halfWidth);
        }
        for (int index = 0; index < blueprint.sites.Count; index++)
        {
            YQSiteAnchorV2 site = blueprint.sites[index];
            if (site == null)
                continue;
            if (PointDistanceSquaredToCell(site.x, site.z, centerX, centerZ, half) <=
                Mathf.Max(0f, site.reservedRadius) * Mathf.Max(0f, site.reservedRadius))
            {
                AddUnique(record.featureIds, site.siteId);
            }
            // note: Member sectors carry the same accepted identity for semantic ownership and overlay replay; only the owner cell materializes the site-wide root.
            if (site.memberFootprint == null)
                continue;
            for (int memberIndex = 0; memberIndex < site.memberFootprint.Count; memberIndex++)
            {
                YQSiteMemberFootprintV2 member = site.memberFootprint[memberIndex];
                if (member != null && PointDistanceSquaredToCell(member.x, member.z, centerX, centerZ, half) <=
                    Mathf.Max(0f, member.reservedRadius) * Mathf.Max(0f, member.reservedRadius))
                {
                    AddUnique(record.featureIds, site.siteId);
                }
            }
        }
    }

    private string ResolveParentRegion(float x, float z)
    {
        string bestId = string.Empty;
        float bestDistance = float.MaxValue;
        if (_plan.spatialPlan == null)
            return _plan.regions.Count > 0 && _plan.regions[0] != null
                ? _plan.regions[0].regionId
                : "world";
        for (int i = 0; i < _plan.spatialPlan.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord region = _plan.spatialPlan.regions[i];
            if (region == null)
                continue;
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(region.centerX, region.centerZ));
            if (distance <= region.radius && distance < bestDistance)
            {
                bestDistance = distance;
                bestId = string.IsNullOrWhiteSpace(region.regionId) ? bestId : region.regionId;
            }
        }
        if (!string.IsNullOrWhiteSpace(bestId))
            return bestId;
        for (int i = 0; i < _plan.spatialPlan.regions.Count; i++)
        {
            GeneratedSpatialRegionRecord region = _plan.spatialPlan.regions[i];
            if (region == null)
                continue;
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(region.centerX, region.centerZ));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestId = region.regionId;
            }
        }
        return string.IsNullOrWhiteSpace(bestId) ? "world" : bestId;
    }

    private GeneratedSemanticChunkRecord FindPersistedChunk(Vector2Int coordinate)
    {
        // note: The semantic index is rebuilt from the save before streaming begins, avoiding an O(n) save scan per frontier cell.
        return _persistedChunkIndex.TryGetValue(coordinate, out GeneratedSemanticChunkRecord record)
            ? record
            : null;
    }

    private void Enqueue(Vector2Int coordinate)
    {
        if (_queue.Contains(coordinate))
            return;
        RuntimeChunk chunk = _chunks[coordinate];
        if (chunk.physicalRepresentation || chunk.state == YQSemanticChunkLifecycle.Generating)
            return;
        // note: Queued semantic demand owns no Unity resources; enforce capacity at physical dispatch so urgent requests cannot disappear at saturation.
        chunk.state = YQSemanticChunkLifecycle.Queued;
        if (chunk.contentQueuedAt < 0f)
            chunk.contentQueuedAt = Time.unscaledTime;
        _queue.Add(coordinate);
        if (!_batchingHardViewAdmission)
            TrimQueue();
    }

    private int DistanceToCurrent(Vector2Int coordinate)
    {
        return Mathf.Abs(coordinate.x - _currentChunk.x) + Mathf.Abs(coordinate.y - _currentChunk.y);
    }

    private bool IsRequiredCoverageCoordinate(Vector2Int coordinate)
    {
        // note: Required collision coverage is an urgent physical demand even when the cell is outside the camera frustum; it must be able to reclaim stale prewarm residency.
        int distance = Mathf.Max(
            Mathf.Abs(coordinate.x - _currentChunk.x),
            Mathf.Abs(coordinate.y - _currentChunk.y));
        return distance <= TerrainCoverageRadius;
    }

    private bool TryReservePhysicalOwner(Vector2Int requested)
    {
        // note: Measure the exact owner census and reclamation path before designing a persistent census optimization.
        using (PhysicalOwnerReservationProfilerMarker.Auto())
            return TryReservePhysicalOwnerCore(requested);
    }

    private bool TryReservePhysicalOwnerCore(Vector2Int requested)
    {
        // note: Count partial terrain/content owners as well as completed cells, avoiding an uncounted TerrainData backlog.
        // note: A retired hierarchy still owns native resources until frame end, including across repeated admission calls in this same frame.
        int count = PendingStreamingNativeRetirements;
        RuntimeChunk victim = null;
        Vector2Int victimCoordinate = default;
        int farthest = -1;
        // note: Distinguish real camera/traversal deadlines from the broader all-direction preparation ring so critical demand can reclaim expendable prewarm residency.
        bool requestedHardView = _guaranteedViewDemand.Contains(requested);
        bool requestedCritical = requestedHardView ||
            _semanticViewPredictionArrivalSeconds.ContainsKey(requested) ||
            _provisionalGroundPrefetchDemand.Contains(requested) ||
            _provisionalGroundTurnBufferDemand.Contains(requested) ||
            _siteTerrainDependencies.ContainsKey(requested) ||
            _sharedSiteOwnerDemand.Contains(requested) ||
            IsRequiredCoverageCoordinate(requested);
        // note: A queued preparation request may reclaim stale retained content, but only an actual view/deadline request can reclaim another preparation owner.
        bool requestedUrgent = requestedCritical || _hardViewDemand.Contains(requested);
        foreach (KeyValuePair<Vector2Int, RuntimeChunk> pair in _chunks)
        {
            RuntimeChunk chunk = pair.Value;
            bool ownsResources = chunk.generatedObjects.Count > 0 || chunk.state == YQSemanticChunkLifecycle.Generating ||
                (_terrainPreparation != null && pair.Key == _terrainPreparingCoordinate) ||
                _pendingTerrainPublications.ContainsKey(pair.Key);
            if (!ownsResources)
                continue;
            if (pair.Key == requested)
                return true;
            count++;
            int distance = Mathf.Max(Mathf.Abs(pair.Key.x - _currentChunk.x), Mathf.Abs(pair.Key.y - _currentChunk.y));
            // note: Keep the live camera, predicted arrival, traversal safety, site leases, authored terrain, and unfinished generation owners resident.
            if (pair.Key == _currentChunk || _guaranteedViewDemand.Contains(pair.Key) ||
                _semanticViewPredictionArrivalSeconds.ContainsKey(pair.Key) ||
                _provisionalGroundPrefetchDemand.Contains(pair.Key) ||
                _provisionalGroundTurnBufferDemand.Contains(pair.Key) ||
                _siteTerrainDependencies.ContainsKey(pair.Key) ||
                _sharedSiteOwnerDemand.Contains(pair.Key) ||
                IsRequiredCoverageCoordinate(pair.Key) ||
                IsChunkInsideAuthoredTerrain(pair.Key) || chunk.state == YQSemanticChunkLifecycle.Generating ||
                (_terrainPreparation != null && pair.Key == _terrainPreparingCoordinate))
                continue;
            // note: A genuinely urgent view/traversal request may replace a canonical preparation-ring owner; that cell is deterministically re-admitted when it becomes the live runway.
            if (_hardViewDemand.Contains(pair.Key) && !requestedCritical)
                continue;
            // note: Preserve nearby/prewarm demand during ordinary streaming, while critical view admission can reclaim stale retained owners.
            if (!requestedCritical &&
                (distance <= EffectiveVisualPrewarmRadius || IsContentDemandedNow(pair.Key)))
                continue;
            if (distance > farthest)
            {
                farthest = distance;
                victim = chunk;
                victimCoordinate = pair.Key;
            }
        }
        if (count < PhysicalOwnerCapacity)
            return true;
        if (victim == null)
        {
            // note: Retain the exact reservation denial so startup can distinguish a saturated pinned owner pool from an undispatched queue entry.
            _lastOwnerReservationBlock = requested;
            _lastOwnerReservationBlockCount = count;
            _lastOwnerReservationBlockUrgent = requestedUrgent;
            _ownerReservationBlockCount++;
            return false;
        }
        // note: Release only this runtime owner's generated content; accepted semantic records and borrowed site objects remain intact.
        if (!TryDestroyGeneratedObjectsForStreaming(victim))
            return false;
        _extendedTerrainTiles.Remove(victimCoordinate);
        victim.state = YQSemanticChunkLifecycle.Unloaded;
        _contentDemand.Remove(victimCoordinate);
        RefreshTerrainNeighbors(new Vector2Int(victimCoordinate.x - 1, victimCoordinate.y));
        RefreshTerrainNeighbors(new Vector2Int(victimCoordinate.x + 1, victimCoordinate.y));
        RefreshTerrainNeighbors(new Vector2Int(victimCoordinate.x, victimCoordinate.y - 1));
        RefreshTerrainNeighbors(new Vector2Int(victimCoordinate.x, victimCoordinate.y + 1));
        _lifecyclePending = true;
        // note: Unity destroys resources at frame end; defer the replacement allocation until the following scheduler pass.
        return false;
    }

    private void SortContentQueue()
    {
        // note: Separate queue sorting from content dispatch so frame-budget evidence identifies repeated comparator work.
        using (ContentQueueSortProfilerMarker.Auto())
            SortContentQueueCore();
    }

    private void SortContentQueueCore()
    {
        UpdateQueuePriorityView();
        // note: Required content uses the same near-player deadline ordering as terrain so a ready collider cannot wait behind remote camera/prewarm decoration.
        // note: Sort toward the requested 260 m/s lane before motor acceleration finishes updating measured velocity.
        Vector2 traversalVelocity = ResolveTraversalDemandVelocity();
        _queue.Sort((left, right) =>
        {
            int deadlineCompare = CompareContentQueueDeadlines(left, right, traversalVelocity);
            if (deadlineCompare != 0)
                return deadlineCompare;
            int priorityCompare = CompareNearbyAndViewPriority(left, right);
            return priorityCompare != 0 ? priorityCompare : CompareCoordinates(left, right);
        });
    }

    private int CompareContentQueueDeadlines(
        Vector2Int left,
        Vector2Int right,
        Vector2 traversalVelocity)
    {
        // note: Only incomplete owners participate in the deadline lane; completed or stale entries retain the normal view ordering until they are removed.
        bool leftIncomplete = IsContentPublicationIncomplete(left);
        bool rightIncomplete = IsContentPublicationIncomplete(right);
        if (leftIncomplete != rightIncomplete)
            return leftIncomplete ? -1 : 1;
        if (!leftIncomplete)
            return 0;

        // note: Keep the player's current owner first so a frontier refresh cannot leave the traversed cell waiting behind a visible prewarm ring.
        bool leftCurrent = left == _currentChunk;
        bool rightCurrent = right == _currentChunk;
        if (leftCurrent != rightCurrent)
            return leftCurrent ? -1 : 1;

        // note: Complete content for every cell in the live camera before spending a generation lane on the next swept boundary.
        bool leftGuaranteedView = _guaranteedViewDemand.Contains(left) || _sharedSiteVisibleOwners.Contains(left);
        bool rightGuaranteedView = _guaranteedViewDemand.Contains(right) || _sharedSiteVisibleOwners.Contains(right);
        // note: Current-frustum cells share an immediate deadline; their future path projection cannot defer one visible cell behind another.
        if (leftGuaranteedView != rightGuaranteedView)
            return leftGuaranteedView ? -1 : 1;

        // note: The next swept boundary remains the strongest deadline among cells outside the live camera frustum.
        bool leftImmediate = traversalVelocity.sqrMagnitude >= 0.01f &&
            IsImmediateTraversalTerrainCoordinate(left, traversalVelocity);
        bool rightImmediate = traversalVelocity.sqrMagnitude >= 0.01f &&
            IsImmediateTraversalTerrainCoordinate(right, traversalVelocity);
        if (leftImmediate != rightImmediate)
            return leftImmediate ? -1 : 1;

        // note: A current visible cell can also occur in a later shifted view; ignore that forecast when its live frustum deadline is already zero.
        float leftArrivalSeconds = 0f;
        float rightArrivalSeconds = 0f;
        bool leftPredictiveView = !_guaranteedViewDemand.Contains(left) &&
            TryGetSiteAwareViewArrival(left, out leftArrivalSeconds);
        bool rightPredictiveView = !_guaranteedViewDemand.Contains(right) &&
            TryGetSiteAwareViewArrival(right, out rightArrivalSeconds);
        if (leftPredictiveView != rightPredictiveView)
            return leftPredictiveView ? -1 : 1;
        if (leftPredictiveView)
        {
            // note: Finish the earliest projected camera cells first so the max-speed runway is ready before it reaches the live frustum.
            int arrivalCompare = leftArrivalSeconds.CompareTo(rightArrivalSeconds);
            if (arrivalCompare != 0)
                return arrivalCompare;
        }

        // note: Prepare the requested future camera view before optional off-screen turn-buffer content; only its collision floor is a hard turnaround need.
        bool leftTurnBuffer = _provisionalGroundTurnBufferDemand.Contains(left);
        bool rightTurnBuffer = _provisionalGroundTurnBufferDemand.Contains(right);
        if (leftTurnBuffer != rightTurnBuffer)
            return leftTurnBuffer ? -1 : 1;

        // note: Every admitted preparation cell still outranks broad nearby retention and distant corridor work.
        bool leftHardView = _hardViewDemand.Contains(left);
        bool rightHardView = _hardViewDemand.Contains(right);
        if (leftHardView != rightHardView)
            return leftHardView ? -1 : 1;
        if (leftHardView)
        {
            // note: Among nonvisible preparation owners with equal live deadlines, finish the oldest admission first so a moving camera cannot perpetually replace unfinished prewarm work.
            float leftAdmittedAt = _chunks.TryGetValue(left, out RuntimeChunk leftHardViewChunk) && leftHardViewChunk != null
                ? leftHardViewChunk.hardViewAdmittedAt
                : float.MaxValue;
            float rightAdmittedAt = _chunks.TryGetValue(right, out RuntimeChunk rightHardViewChunk) && rightHardViewChunk != null
                ? rightHardViewChunk.hardViewAdmittedAt
                : float.MaxValue;
            int admissionAgeCompare = leftAdmittedAt.CompareTo(rightAdmittedAt);
            if (admissionAgeCompare != 0)
                return admissionAgeCompare;
        }

        bool leftRequiredCoverage = IsRequiredCoverageCoordinate(left);
        bool rightRequiredCoverage = IsRequiredCoverageCoordinate(right);
        if (leftRequiredCoverage != rightRequiredCoverage)
            return leftRequiredCoverage ? -1 : 1;
        return 0;
    }

    private int CompareContentQueuePriority(
        Vector2Int left,
        Vector2Int right,
        Vector2 traversalVelocity)
    {
        // note: Use the same deadline, nearby-view, and coordinate tie-breakers for queue dispatch and cooperative active-worker preemption.
        int deadlineCompare = CompareContentQueueDeadlines(left, right, traversalVelocity);
        if (deadlineCompare != 0)
            return deadlineCompare;
        int priorityCompare = CompareNearbyAndViewPriority(left, right);
        return priorityCompare != 0 ? priorityCompare : CompareCoordinates(left, right);
    }

    private bool IsContentPublicationIncomplete(Vector2Int coordinate)
    {
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) || chunk == null)
            return true;
        bool predictedViewAppearancePending =
            (_guaranteedViewDemand.Contains(coordinate) ||
                _provisionalGroundTurnBufferDemand.Contains(coordinate) ||
                _semanticViewPredictionArrivalSeconds.ContainsKey(coordinate)) &&
            (!chunk.appearanceReady || !chunk.requiredEcologyReady);
        return !chunk.physicalRepresentation ||
            !chunk.requiredContentReady ||
            !chunk.overlayReady ||
            !chunk.activationComplete ||
            predictedViewAppearancePending;
    }

    private void SortTerrainQueue()
    {
        UpdateQueuePriorityView();
        // note: Resolve traversal state once per sort so a bounded queue does not repeat player-position and boundary math inside every comparison.
        Vector2 velocity = ResolveTraversalVelocity();
        bool hasVelocity = velocity.sqrMagnitude > 0.01f;
        bool diagonalVelocity = hasVelocity &&
            Mathf.Abs(velocity.normalized.x) >= 0.35f && Mathf.Abs(velocity.normalized.y) >= 0.35f;
        TryResolveImmediateDiagonalTransitions(
            velocity,
            out Vector2Int diagonalAxisX,
            out Vector2Int diagonalAxisZ,
            out bool diagonalAxisXImmediate,
            out bool diagonalAxisZImmediate);
        _terrainQueue.Sort((left, right) =>
            CompareTerrainQueueRequests(
                left,
                right,
                velocity,
                hasVelocity,
                diagonalVelocity,
                diagonalAxisX,
                diagonalAxisZ,
                diagonalAxisXImmediate,
                diagonalAxisZImmediate));
    }

    private int FindLeastUrgentTerrainRequest(Vector2 velocity)
    {
        UpdateQueuePriorityView();
        // note: Scan the bounded queue with the canonical priority comparator so a full admission avoids sorting the same candidates once per incoming corridor cell.
        bool hasVelocity = velocity.sqrMagnitude > 0.01f;
        bool diagonalVelocity = hasVelocity &&
            Mathf.Abs(velocity.normalized.x) >= 0.35f && Mathf.Abs(velocity.normalized.y) >= 0.35f;
        TryResolveImmediateDiagonalTransitions(
            velocity,
            out Vector2Int diagonalAxisX,
            out Vector2Int diagonalAxisZ,
            out bool diagonalAxisXImmediate,
            out bool diagonalAxisZImmediate);
        int leastUrgentIndex = 0;
        for (int index = 1; index < _terrainQueue.Count; index++)
        {
            if (CompareTerrainQueueRequests(
                    _terrainQueue[leastUrgentIndex],
                    _terrainQueue[index],
                    velocity,
                    hasVelocity,
                    diagonalVelocity,
                    diagonalAxisX,
                    diagonalAxisZ,
                    diagonalAxisXImmediate,
                    diagonalAxisZImmediate) < 0)
                leastUrgentIndex = index;
        }
        return leastUrgentIndex;
    }

    private bool IsTerrainRequestMoreUrgent(Vector2Int incoming, Vector2Int existing, Vector2 velocity)
    {
        UpdateQueuePriorityView();
        // note: Reuse the exact queue ordering context when comparing a candidate with the scanned tail so diagonal immediacy and critical classes cannot drift.
        bool hasVelocity = velocity.sqrMagnitude > 0.01f;
        bool diagonalVelocity = hasVelocity &&
            Mathf.Abs(velocity.normalized.x) >= 0.35f && Mathf.Abs(velocity.normalized.y) >= 0.35f;
        TryResolveImmediateDiagonalTransitions(
            velocity,
            out Vector2Int diagonalAxisX,
            out Vector2Int diagonalAxisZ,
            out bool diagonalAxisXImmediate,
            out bool diagonalAxisZImmediate);
        return CompareTerrainQueueRequests(
                incoming,
                existing,
                velocity,
                hasVelocity,
                diagonalVelocity,
                diagonalAxisX,
                diagonalAxisZ,
                diagonalAxisXImmediate,
                diagonalAxisZImmediate) < 0;
    }

    private int CompareTerrainQueueRequests(
        Vector2Int left,
        Vector2Int right,
        Vector2 velocity,
        bool hasVelocity,
        bool diagonalVelocity,
        Vector2Int diagonalAxisX,
        Vector2Int diagonalAxisZ,
        bool diagonalAxisXImmediate,
        bool diagonalAxisZImmediate)
    {
        // note: Dispatch, admission, and preemption must share the same deadline classes; otherwise a cancelled owner can win dispatch again ahead of the request that cancelled it.
        int priorityCompare = TerrainPreparationPriority(left, velocity).CompareTo(TerrainPreparationPriority(right, velocity));
        if (priorityCompare != 0)
            return priorityCompare;
        // note: Keep the unready current cell ahead of future speculation; its center can fall behind the player before its collider is published, which must not erase explicit current-cell promotion.
        bool leftCurrentUnready = left == _currentChunk && !IsChunkInsideAuthoredTerrain(left) && !HasPublishedTerrain(left);
        bool rightCurrentUnready = right == _currentChunk && !IsChunkInsideAuthoredTerrain(right) && !HasPublishedTerrain(right);
        if (leftCurrentUnready != rightCurrentUnready)
            return leftCurrentUnready ? -1 : 1;
        bool leftRequiredCoverage = IsRequiredCoverageCoordinate(left) &&
            !IsChunkInsideAuthoredTerrain(left) && !HasPublishedTerrain(left);
        bool rightRequiredCoverage = IsRequiredCoverageCoordinate(right) &&
            !IsChunkInsideAuthoredTerrain(right) && !HasPublishedTerrain(right);
        if (leftRequiredCoverage != rightRequiredCoverage)
            return leftRequiredCoverage ? -1 : 1;
        if (hasVelocity)
        {
            bool leftImmediate = IsImmediateTraversalTerrainCoordinate(left, velocity);
            bool rightImmediate = IsImmediateTraversalTerrainCoordinate(right, velocity);
            if (leftImmediate != rightImmediate)
                return leftImmediate ? -1 : 1;
        }
        // note: Within one urgency class, prepare the earliest camera exposure before a farther centerline owner; current and immediate collision owners remain ahead.
        bool leftPredictedView = TryGetSiteAwareViewArrival(
            left, out float leftPredictedArrival);
        bool rightPredictedView = TryGetSiteAwareViewArrival(
            right, out float rightPredictedArrival);
        if (leftPredictedView != rightPredictedView)
            return leftPredictedView ? -1 : 1;
        if (leftPredictedView)
        {
            int predictedArrivalCompare = leftPredictedArrival.CompareTo(rightPredictedArrival);
            if (predictedArrivalCompare != 0)
                return predictedArrivalCompare;
        }
        if (hasVelocity)
        {
            // note: Equal exposure deadlines and unpredicted requests retain the existing centerline age ordering.
            bool leftCritical = IsTraversalCriticalTerrainCoordinate(left, velocity, false);
            bool rightCritical = IsTraversalCriticalTerrainCoordinate(right, velocity, false);
            if (leftCritical != rightCritical)
                return leftCritical ? -1 : 1;
            // note: Among centerline owners, finish the oldest deadline first so a multi-cell corridor cannot starve its tail.
            if (leftCritical)
            {
                bool leftAgedCritical = IsAgedCriticalTerrainCoordinate(left, velocity);
                bool rightAgedCritical = IsAgedCriticalTerrainCoordinate(right, velocity);
                if (leftAgedCritical != rightAgedCritical)
                    return leftAgedCritical ? -1 : 1;
                float leftRequestedAt = TerrainCriticalRequestedAt(left);
                float rightRequestedAt = TerrainCriticalRequestedAt(right);
                int ageCompare = leftRequestedAt.CompareTo(rightRequestedAt);
                if (ageCompare != 0)
                    return ageCompare;
            }
        }
        // note: Nearby player cells outrank distant work so local readiness cannot wait behind remote speculation.
        int nearbyCompare = CompareNearbyAndViewPriority(left, right);
        if (nearbyCompare != 0)
            return nearbyCompare;
        // note: Required site collision outranks remote speculation after local and visible traversal safety priorities, including the direct handoff set populated by the site waiter.
        bool leftSiteDependency = _siteTerrainDependencies.ContainsKey(left) || _siteTerrainHandoffRequests.Contains(left);
        bool rightSiteDependency = _siteTerrainDependencies.ContainsKey(right) || _siteTerrainHandoffRequests.Contains(right);
        if (leftSiteDependency != rightSiteDependency)
            return leftSiteDependency ? -1 : 1;
        float leftUrgency = EstimateTerrainUrgency(left, velocity);
        float rightUrgency = EstimateTerrainUrgency(right, velocity);
        int urgencyCompare = leftUrgency.CompareTo(rightUrgency);
        return urgencyCompare != 0 ? urgencyCompare : CompareCoordinates(left, right);
    }

    private void UpdateQueuePriorityView()
    {
        // note: The authoritative motor camera owns the actual gameplay frustum; Camera.main may still be the title or presentation camera after a flow transition.
        Camera authoritativeCamera = _playerMotor != null && _playerMotor.playerCamera != null
            ? _playerMotor.playerCamera
            : Camera.main;
        _queuePriorityCamera = authoritativeCamera;
        if (_queuePriorityCamera == null)
        {
            _queuePriorityViewValid = false;
            return;
        }

        _queuePriorityCameraPosition = _queuePriorityCamera.transform.position;
        _queuePriorityViewValid = true;
    }

    private int CompareNearbyAndViewPriority(Vector2Int left, Vector2Int right)
    {
        // note: The exact live frustum outranks its preparation ring and every predictive or retention hint so visible cells cannot be trimmed behind remote work.
        bool leftGuaranteedView = _guaranteedViewDemand.Contains(left);
        bool rightGuaranteedView = _guaranteedViewDemand.Contains(right);
        if (leftGuaranteedView != rightGuaranteedView)
            return leftGuaranteedView ? -1 : 1;

        // note: The preparation ring still outranks ordinary nearby and distant work, but it is not a presentation deadline.
        bool leftHardView = _hardViewDemand.Contains(left);
        bool rightHardView = _hardViewDemand.Contains(right);
        if (leftHardView != rightHardView)
            return leftHardView ? -1 : 1;
        int leftDistance = DistanceToCurrent(left);
        int rightDistance = DistanceToCurrent(right);
        bool leftNearby = leftDistance <= Mathf.Max(0, activeRadius);
        bool rightNearby = rightDistance <= Mathf.Max(0, activeRadius);
        if (leftNearby != rightNearby)
            return leftNearby ? -1 : 1;

        bool leftVisible = IsChunkInPriorityView(left);
        bool rightVisible = IsChunkInPriorityView(right);
        if (leftVisible != rightVisible)
            return leftVisible ? -1 : 1;

        int distanceCompare = leftDistance.CompareTo(rightDistance);
        return distanceCompare != 0 ? distanceCompare : 0;
    }

    private bool IsChunkInPriorityView(Vector2Int coordinate)
    {
        if (!_queuePriorityViewValid || _queuePriorityCamera == null)
            return true;

        float size = Mathf.Max(32f, chunkWorldSize);
        Vector3 center = new Vector3(
            WorldGridOrigin + (coordinate.x + 0.5f) * size,
            _queuePriorityCameraPosition.y,
            WorldGridOrigin + (coordinate.y + 0.5f) * size);
        Vector3 viewport = _queuePriorityCamera.WorldToViewportPoint(center);
        return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f &&
            viewport.y >= 0f && viewport.y <= 1f;
    }

    private float EstimateTerrainUrgency(Vector2Int coordinate)
    {
        return EstimateTerrainUrgency(coordinate, ResolveTraversalVelocity());
    }

    private float EstimateTerrainUrgency(Vector2Int coordinate, Vector2 velocity)
    {
        // note: Rank by estimated arrival minus conservative build time so the queue serves deadline risk rather than only geometric distance.
        float speed = velocity.magnitude;
        if (speed < 0.01f || _player == null)
            return DistanceToCurrent(coordinate);
        Vector2 direction = velocity / speed;
        Vector2 center = new Vector2(
            WorldGridOrigin + (coordinate.x + 0.5f) * Mathf.Max(32f, chunkWorldSize),
            WorldGridOrigin + (coordinate.y + 0.5f) * Mathf.Max(32f, chunkWorldSize));
        Vector2 playerPosition = new Vector2(_player.position.x, _player.position.z);
        Vector2 delta = center - playerPosition;
        float along = Vector2.Dot(delta, direction);
        float lateral = Mathf.Abs(delta.x * direction.y - delta.y * direction.x);
        float arrival = along > 0f ? along / speed : 1000f + (-along / speed);
        float estimatedReady = Mathf.Max(0.15f, _lastTerrainBuildSeconds > 0f ? _lastTerrainBuildSeconds : 0.35f);
        float size = Mathf.Max(32f, chunkWorldSize);
        // note: Prefer the centerline slightly while retaining enough urgency for camera-width flank terrain to arrive within the high-speed lead.
        float lateralLanePenalty = lateral >= size * 0.5f ? Mathf.Min(1f, PredictiveReadinessHorizonSeconds * 0.2f) : 0f;
        return arrival - estimatedReady + lateralLanePenalty + lateral / Mathf.Max(speed, 1f) * 0.25f;
    }

    private static int CompareCoordinates(Vector2Int left, Vector2Int right)
    {
        int xCompare = left.x.CompareTo(right.x);
        return xCompare != 0 ? xCompare : left.y.CompareTo(right.y);
    }

    private void TrimQueue(bool queueAlreadySorted = false)
    {
        if (_queue.Count <= ContentQueueCapacity)
            return;
        if (!queueAlreadySorted)
            SortContentQueue();
        for (int i = _queue.Count - 1; i >= 0 && _queue.Count > ContentQueueCapacity; i--)
        {
            Vector2Int coordinate = _queue[i];
            if (_publicationVerificationDemand.Contains(coordinate))
                continue;
            if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) && chunk != null &&
                chunk.state == YQSemanticChunkLifecycle.Queued)
            {
                chunk.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
                chunk.contentQueuedAt = -1f;
            }
            _queue.RemoveAt(i);
        }
        _queuedCount = _queue.Count;
        // note: Dropping only stale distant requests keeps the scheduler bounded while the current neighborhood is re-enqueued on the next frontier refresh.
    }

    private void TrimContentQueue()
    {
        // note: Reset only queued semantic work that is no longer demanded; published terrain remains available for traversal and is managed by its own radius.
        for (int index = _queue.Count - 1; index >= 0; index--)
        {
            Vector2Int coordinate = _queue[index];
            if (_contentDemand.Contains(coordinate) || _publicationVerificationDemand.Contains(coordinate) ||
                _sharedSiteOwnerDemand.Contains(coordinate))
                continue;
            if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk) && chunk != null &&
                chunk.state == YQSemanticChunkLifecycle.Queued)
            {
                chunk.state = YQSemanticChunkLifecycle.SemanticallyPlanned;
                chunk.contentQueuedAt = -1f;
            }
            _queue.RemoveAt(index);
        }
    }

    private Vector2Int ResolvePredictedContentChunk(Vector2 playerPosition)
    {
        Vector2 velocity = Vector2.zero;
        if (_playerMotor != null && _playerMotor.isActiveAndEnabled && _playerMotor.IsAuthoritative)
        {
            // note: Predict content from active motor motion, otherwise use the measured player displacement maintained by lookahead refresh.
            Vector3 planar = _playerMotor.PlanarVelocity;
            velocity = new Vector2(planar.x, planar.z);
        }
        else if (_hasLastLookaheadPosition)
        {
            float deltaTime = Mathf.Max(0.001f, Time.unscaledDeltaTime);
            velocity = (playerPosition - _lastLookaheadPosition) / deltaTime;
        }
        return ChunkFor(playerPosition + velocity * 3f);
    }

    private bool PruneSemanticHistory(int maximumEvictions)
    {
        // note: Keep velocity-predicted owners alive until their bounded terrain requests either publish or become stale.
        int retention = Mathf.Max(semanticLookaheadRadius + 2, EffectiveUnloadRadius + 1, TerrainRequestRetentionRadius + 1);
        int evictions = 0;
        for (int i = _plan.semanticChunks.Count - 1; i >= 0; i--)
        {
            GeneratedSemanticChunkRecord record = _plan.semanticChunks[i];
            if (record == null)
                continue;
            record.EnsureCollections();
            int distance = Mathf.Max(Mathf.Abs(record.chunkX - _currentChunk.x), Mathf.Abs(record.chunkZ - _currentChunk.y));
            if (distance <= retention)
                continue;
            Vector2Int coordinate = new Vector2Int(record.chunkX, record.chunkZ);
            // note: A remote owner still serving a live member cannot lose its persisted semantic record during distance pruning.
            if (_sharedSiteOwnerDemand.Contains(coordinate))
                continue;
            // note: Keep the single explicit recovery witness alive until its verification owner releases the bounded demand pin.
            if (_publicationVerificationDemand.Contains(coordinate))
                continue;
            // note: Semantic pruning cannot remove a cell currently inside the camera contract, even if the player has not entered its grid square.
            if (_hardViewDemand.Contains(coordinate) || _provisionalGroundPrefetchDemand.Contains(coordinate))
                continue;
            // note: Authored terrain is a persistent physical source and must remain indexed for return traversal even when the player is many streamed cells away.
            if (IsChunkInsideAuthoredTerrain(coordinate) || _siteTerrainDependencies.ContainsKey(coordinate))
                continue;
            if (_chunks.TryGetValue(coordinate, out RuntimeChunk chunk))
            {
                // note: Preserve only the bounded unload witness; all other distant runtime owners remain eligible for normal eviction.
                if (coordinate == _unloadedHistoryAnchor && chunk.state == YQSemanticChunkLifecycle.Unloaded &&
                    !IsChunkInsideAuthoredTerrain(coordinate))
                    continue;
                // note: Keep the tile whose heightfield is still receiving deferred biome paint alive until that preparation coroutine releases ownership.
                if (coordinate == _terrainPreparingCoordinate)
                    continue;
                if (chunk.state == YQSemanticChunkLifecycle.Generating)
                    continue;
                // note: History pruning shares the same native retirement allowance; keep queues, records and resource ownership intact when that allowance is spent.
                if (!TryDestroyGeneratedObjectsForStreaming(chunk))
                    return false;
                if (chunk.state == YQSemanticChunkLifecycle.Queued)
                {
                    _queue.Remove(coordinate);
                    chunk.contentQueuedAt = -1f;
                }
                // note: Remove pending requests and retries with the runtime owner so stale work cannot retain an evicted cell.
                _contentDemand.Remove(coordinate);
                _terrainQueue.Remove(coordinate);
                _terrainQueued.Remove(coordinate);
                _terrainRetryAt.Remove(coordinate);
                _terrainRequestedAt.Remove(coordinate);
                _terrainCriticalRequestedAt.Remove(coordinate);
                // note: Evict generated roots and extension terrain together before forgetting the semantic record; authored borrowed objects remain owned by their original world systems.
                _chunks.Remove(coordinate);
                _extendedTerrainTiles.Remove(coordinate);
                RefreshTerrainNeighbors(new Vector2Int(coordinate.x - 1, coordinate.y));
                RefreshTerrainNeighbors(new Vector2Int(coordinate.x + 1, coordinate.y));
                RefreshTerrainNeighbors(new Vector2Int(coordinate.x, coordinate.y - 1));
                RefreshTerrainNeighbors(new Vector2Int(coordinate.x, coordinate.y + 1));
                evictions++;
            }
            // note: Durable site choices and gameplay deltas remain in the save while their materialized runtime owner is released above.
            if (!HasDurableSemanticState(record))
            {
                _persistedChunkIndex.Remove(coordinate);
                _plan.semanticChunks.RemoveAt(i);
            }
            if (evictions >= Mathf.Max(1, maximumEvictions))
                return false;
        }
        return true;
    }

    private static bool HasDurableSemanticState(GeneratedSemanticChunkRecord record)
    {
        // note: Deterministic untouched cells may be regenerated, but accepted named sites and their bindings must survive distant-record pruning.
        if (record == null)
            return false;
        if (record.persistentDeltaIds != null && record.persistentDeltaIds.Count > 0)
            return true;
        if (record.sites == null)
            return false;
        for (int index = 0; index < record.sites.Count; index++)
        {
            GeneratedSemanticSiteRecord site = record.sites[index];
            if (site != null && site.immutableReservation && !string.IsNullOrWhiteSpace(site.siteId))
                return true;
        }
        return false;
    }

    private void OnDisable()
    {
        CancelContinuationPopulationWork();
        CancelFrontierConstructionWork();
    }

    private void OnDestroy()
    {
        CancelContinuationPopulationWork();
        CancelFrontierConstructionWork();
        // note: Clear the movement-gate owner before teardown so a destroyed streamer cannot authorize a later player move.
        if (ReferenceEquals(Active, this))
            Active = null;
        // note: Invalidate any iterator that might still be unwinding while Unity destroys this coordinator.
        _configurationEpoch++;
        _siteTerrainDependencies.Clear();
        _siteTerrainHandoffRequests.Clear();
        ClearSharedSiteDemand();
        _physicalDemand.Clear();
        _physicalDemandVisibleOverflow = false;
        _physicalDemandOverflowReported = false;
        _siteTerrainProgress.Clear();
        // note: Stop every bounded content worker before destroying owned roots so concurrent stale iterators cannot publish during teardown.
        _activeGenerationWorkIds.Clear();
        foreach (Coroutine activeGeneration in _activeGenerations.Values)
            if (activeGeneration != null)
                StopCoroutine(activeGeneration);
        _activeGenerations.Clear();
        if (_terrainPreparation != null)
        {
            // note: Stop pure sampling before destroying the streamer so its isolated worker cannot outlive the authoritative owner.
            _terrainHeightPreparationCancellation?.Cancel();
            StopCoroutine(_terrainPreparation);
        }
        CancelAllPendingTerrainPublications();
        CancelAllTerrainHeightPrefetches();
        StopAllTerrainPainting();
        _terrainPreparingCoordinate = new Vector2Int(int.MinValue, int.MinValue);
        foreach (RuntimeChunk chunk in _chunks.Values)
            DestroyGeneratedObjects(chunk);
        CancelAllProvisionalGroundSamplers();
        DestroyOrphanedExtensionTerrains();
        DestroyProvisionalGroundPool();
        _chunks.Clear();
        _publicationVerificationScenarios.Clear();
        _publicationVerificationDemand.Clear();
        _demandCancelledGenerations.Clear();
        _queue.Clear();
        _hardViewDemand.Clear();
        _canonicalPreparationDemand.Clear();
        _guaranteedViewDemand.Clear();
        _provisionalGroundPrefetchDemand.Clear();
        _provisionalGroundTurnBufferDemand.Clear();
        _highSpeedTurnBufferCoverageDemand.Clear();
        _highSpeedTurnBufferCoverageReadyCount = 0;
        _terrainAppearanceRetryAt.Clear();
        _terrainAppearanceRetryCount.Clear();
        _requiredEcologyRetryAt.Clear();
        _requiredEcologyRetryCount.Clear();
        _extendedTerrainTiles.Clear();
        _provisionalGroundTiles.Clear();
        _provisionalGroundQueue.Clear();
        _provisionalGroundQueued.Clear();
        _semanticPrunePending = false;
        _queuedCount = 0;
        _activeCount = 0;
        _retainedCount = 0;
        _physicalCount = 0;
        _physicalCountRefreshFrame = -1;
        _unloadedCount = 0;
    }

    private void DestroyOrphanedExtensionTerrains()
    {
        foreach (Terrain terrain in _extendedTerrainTiles.Values)
        {
            if (terrain == null)
                continue;
            TerrainData data = terrain.terrainData;
            terrain.SetNeighbors(null, null, null, null);
            Destroy(terrain.gameObject);
            if (data != null)
                Destroy(data);
        }
        _extendedTerrainTiles.Clear();
    }

    private void CancelAllProvisionalGroundSamplers()
    {
        foreach (ProvisionalGroundSampling sampling in _provisionalGroundSamplers.Values)
        {
            if (sampling == null)
                continue;
            sampling.abandoned = true;
            sampling.preparedBiomeAlphamap?.Dispose();
            sampling.cancellation?.Cancel();
            if (sampling.cancellation == null)
                continue;
            if (sampling.task == null || sampling.task.IsCompleted)
                sampling.cancellation.Dispose();
            else
            {
                CancellationTokenSource cancellation = sampling.cancellation;
                sampling.task.ContinueWith(_ => cancellation.Dispose(), TaskScheduler.Default);
            }
        }
        _provisionalGroundSamplers.Clear();
        _provisionalGroundSamplerScratch.Clear();
        _provisionalGroundQueue.Clear();
        _provisionalGroundQueued.Clear();
    }

    private void StopAllTerrainPainting()
    {
        _terrainPaintingScratch.Clear();
        // note: Revoke every painter before disposal so bulk cancellation cannot consume a failed-attempt retry.
        foreach (KeyValuePair<Vector2Int, Coroutine> painting in _terrainPainting)
            _terrainPaintingScratch.Add(painting.Key);
        _terrainPaintingWorkIds.Clear();
        for (int index = 0; index < _terrainPaintingScratch.Count; index++)
        {
            Vector2Int coordinate = _terrainPaintingScratch[index];
            if (_terrainPainting.TryGetValue(coordinate, out Coroutine painting) && painting != null)
                StopCoroutine(painting);
        }
        // note: Clear after all stops makes cleanup idempotent even when Unity already disposed a painter iterator.
        _terrainPainting.Clear();
        _terrainPaintingScratch.Clear();
    }

    private void StopTerrainPaintingForChunk(RuntimeChunk chunk)
    {
        if (chunk == null || chunk.record == null)
            return;
        Vector2Int coordinate = new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ);
        if (_terrainPainting.TryGetValue(coordinate, out Coroutine painting))
        {
            // note: Revoke registration before disposal so cancellation cannot record a spurious failed attempt.
            _terrainPainting.Remove(coordinate);
            _terrainPaintingWorkIds.Remove(coordinate);
            if (painting != null)
                StopCoroutine(painting);
        }
    }

    private void CancelTerrainPreparationForCoordinate(Vector2Int coordinate)
    {
        using var lifecycleScope = LifecycleCancellationMarker.Auto();
        if (_terrainPreparation == null || _terrainPreparingCoordinate != coordinate)
            return;
        // note: Set the cooperative cancellation flag for both background and main-thread samplers so their next safe yield releases the owner.
        _terrainPreparationPreempted = true;
        _terrainHeightPreparationCancellation?.Cancel();
        RecordFixedTerrainObservation("cancellationRequested", coordinate, _terrainPreparationWorkId);
    }

    private int PendingStreamingNativeRetirements =>
        _streamingRetirementFrame == Time.frameCount ? _retiredPhysicalOwnersThisFrame : 0;

    internal static bool TryTakeStreamingRetirementSlot(int frame, ref int allowanceFrame, ref int used)
    {
        // note: Repeated lifecycle/capacity/prune passes cannot renew the existing one-cell-per-frame retirement allowance.
        if (allowanceFrame != frame)
        {
            allowanceFrame = frame;
            used = 0;
        }
        if (used >= SemanticEvictionsPerFrame)
            return false;
        used++;
        return true;
    }

    private bool TryDestroyGeneratedObjectsForStreaming(RuntimeChunk chunk)
    {
        if (chunk == null)
            return true;
        Vector2Int coordinate = chunk.record != null
            ? new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ)
            : new Vector2Int(int.MinValue, int.MinValue);
        bool ownsResources = chunk.generatedObjects.Count > 0 || chunk.decorativeRoot != null ||
            chunk.state == YQSemanticChunkLifecycle.Generating ||
            (_terrainPreparation != null && coordinate == _terrainPreparingCoordinate) ||
            _pendingTerrainPublications.ContainsKey(coordinate);
        if (ownsResources)
        {
            if (_streamingRetirementFrame != Time.frameCount)
                _retiredPhysicalOwnersThisFrame = 0;
            if (!TryTakeStreamingRetirementSlot(Time.frameCount, ref _streamingRetirementFrame, ref _streamingRetirementsThisFrame))
            {
                _lifecyclePending = true;
                return false;
            }
            // note: Count the retiring owner before invalidating its registrations; native destruction is deferred and replacement allocation must still obey the same owner cap.
            _retiredPhysicalOwnersThisFrame++;
        }
        DestroyGeneratedObjects(chunk);
        return true;
    }

    private void DestroyGeneratedObjects(RuntimeChunk chunk)
    {
        using var lifecycleScope = LifecycleTeardownMarker.Auto();
        if (chunk == null)
            return;
        Vector2Int coordinate = chunk.record != null
            ? new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ)
            : new Vector2Int(int.MinValue, int.MinValue);
        // note: Retire provisional ground with its semantic owner so replacement worlds cannot reuse a stale renderer or collider.
        if (_provisionalGroundTiles.TryGetValue(coordinate, out Terrain provisionalGround))
            RecycleProvisionalGroundTile(coordinate, chunk, provisionalGround);
        else
            _provisionalGroundTiles.Remove(coordinate);
        _provisionalGroundQueued.Remove(coordinate);
        if (_provisionalGroundSamplers.TryGetValue(coordinate, out ProvisionalGroundSampling provisionalSampling) &&
            provisionalSampling != null)
        {
            // note: A retired owner cannot publish a completed background sample into its replacement chunk.
            provisionalSampling.abandoned = true;
            provisionalSampling.cancellation?.Cancel();
        }
        for (int index = _provisionalGroundQueue.Count - 1; index >= 0; index--)
            if (_provisionalGroundQueue[index] == coordinate)
                _provisionalGroundQueue.RemoveAt(index);
        // note: Retire both physical owners before stopping workers; content-only cancellation uses its separate epoch.
        chunk.terrainOwnerEpoch++;
        chunk.contentOwnerEpoch++;
        if (coordinate == FixedTerrainTraceTarget)
            RecordFixedTerrainObservation("ownerInvalidated", coordinate);
        _activeGenerationWorkIds.Remove(coordinate);
        if (_activeGenerations.TryGetValue(coordinate, out Coroutine generation))
        {
            _activeGenerations.Remove(coordinate);
            if (generation != null)
                StopCoroutine(generation);
        }
        CancelTerrainPreparationForCoordinate(coordinate);
        CancelPendingTerrainPublication(coordinate);
        StopDecorativeGeneration(chunk);
        // note: Full owner teardown releases the committed ecology root together with its structural objects.
        DestroyDecorativeRoot(chunk);
        StopTerrainPaintingForChunk(chunk);
        for (int i = 0; i < chunk.generatedObjects.Count; i++)
        {
            GameObject item = chunk.generatedObjects[i];
            if (item == null)
                continue;
            Terrain terrain = item.GetComponent<Terrain>();
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (terrain != null)
            {
                // note: Disconnect equal-resolution neighbors before destroying a streamed tile so surviving cells never retain a dead topology link.
                terrain.SetNeighbors(null, null, null, null);
            }
            UnityEngine.Object.Destroy(item);
            // note: Remove destroyed generated roots from the ownership list while preserving borrowed authored objects assigned to this chunk.
            RemoveOwnedObject(chunk, item);
            if (data != null)
                UnityEngine.Object.Destroy(data);
        }
        chunk.generatedObjects.Clear();
        InvalidateRendererValidationCache(chunk);
        chunk.generationRoot = null;
        chunk.sitePresentations.Clear();
        chunk.sitePresentationRenderers.Clear();
        chunk.decorativeRoot = null;
        // note: Release the physical-owner slot when Unity objects are destroyed; otherwise the bounded cap permanently blocks the next traversable boundary.
        chunk.physicalRepresentation = false;
        bool authoredProviderReady = IsChunkInsideAuthoredTerrain(coordinate) && IsTerrainColliderReady(_terrain);
        chunk.terrainPublished = authoredProviderReady;
        // note: Teardown revokes every transient publication flag before the owner can be re-admitted, preventing an unloaded cell from becoming traversable on stale state.
        chunk.requiredContentReady = false;
        chunk.contentStartedAt = -1f;
        chunk.contentReadyAt = -1f;
        chunk.ecologyStartedAt = -1f;
        // note: Generated teardown does not destroy the borrowed authored Terrain or its accepted base appearance.
        chunk.appearanceReady = authoredProviderReady;
        chunk.overlayReady = false;
        chunk.requiredEcologyReady = false;
        chunk.activationComplete = false;
        chunk.visualReady = authoredProviderReady;
        chunk.visualStateKnown = false;
        chunk.visualState = false;
        chunk.decorativeComplete = false;
        // note: Overlay receipts belong to the retired physical root; replacement content must replay them onto its new targets.
        chunk.appliedOverlayRevisions.Clear();
        chunk.appliedOverlayReceipts.Clear();
        chunk.failureReason = string.Empty;
        chunk.publicationVersion++;
        _terrainRetryAt.Remove(coordinate);
        _terrainPreparationRetryCount.Remove(coordinate);
        _terrainAppearanceRetryAt.Remove(coordinate);
        _terrainAppearanceRetryCount.Remove(coordinate);
        _terrainRetryAt.Remove(coordinate);
        _terrainPreparationRetryCount.Remove(coordinate);
        _requiredEcologyRetryAt.Remove(coordinate);
        _requiredEcologyRetryCount.Remove(coordinate);
        // note: Destroying a continuation terrain returns its transient readiness to None while authored cells keep the origin collider contract.
        chunk.terrainReadiness = IsChunkInsideAuthoredTerrain(chunk.record != null
            ? new Vector2Int(chunk.record.chunkX, chunk.record.chunkZ)
            : new Vector2Int(int.MinValue, int.MinValue)) && IsTerrainColliderReady(_terrain)
            ? YQTerrainReadinessState.CollisionReady
            : YQTerrainReadinessState.None;
        PruneDestroyedOwnedObjects(chunk);
    }

    private void DestroyDecorativeRoot(RuntimeChunk chunk)
    {
        if (chunk == null || chunk.decorativeRoot == null)
            return;

        // note: Preemption removes only optional ecology children; the structural generation root, terrain, routes, sites, and overlay owner remain intact.
        UnityEngine.Object.Destroy(chunk.decorativeRoot);
        chunk.decorativeRoot = null;
    }

    private void PersistSemanticFrontier()
    {
        // note: Only accepted semantic mutations request this active-world mirror; terrain, ecology, and readiness are reconstructed runtime state.
        if (_world == null || Time.unscaledTime < _nextSemanticSaveAt)
            return;
        _nextSemanticSaveAt = Time.unscaledTime + 1f;
        // note: Yield before the mirror write so accepted overlay replay is not serialized inside its publication call stack; the write remains synchronous.
        if (_semanticSaveRoutine == null)
            _semanticSaveRoutine = StartCoroutine(PersistSemanticFrontierRoutine());
    }

    private IEnumerator PersistSemanticFrontierRoutine()
    {
        yield return null;
        _semanticSaveRoutine = null;
        if (_world == null)
            yield break;
        string failure = string.Empty;
        WorldStateManager manager = WorldStateManager.Instance;
        float saveStarted = Time.realtimeSinceStartup;
        bool saved = manager != null && ReferenceEquals(manager.State, _world) && manager.TrySave(out failure);
        _lastSemanticSaveSeconds = Time.realtimeSinceStartup - saveStarted;
        // note: Pair a synchronous save with the rendered frame that paid for it during the speed witness.
        _lastSemanticSaveFrameCount = Time.frameCount;
        _maximumSemanticSaveSeconds = Mathf.Max(_maximumSemanticSaveSeconds, _lastSemanticSaveSeconds);
        // note: Report save-owner failures after measuring the same synchronous path used by successful frontier commits.
        if (!saved)
            Debug.LogWarning("[YQSemanticChunkStreamer] Active world mirror save failed after semantic mutation: " + (string.IsNullOrWhiteSpace(failure) ? "world save owner unavailable" : failure));
    }

    private Vector2Int ChunkFor(Vector2 position)
    {
        return new Vector2Int(
            Mathf.FloorToInt((position.x - WorldGridOrigin) / Mathf.Max(1, chunkWorldSize)),
            Mathf.FloorToInt((position.y - WorldGridOrigin) / Mathf.Max(1, chunkWorldSize)));
    }

    private Transform ResolvePlayer()
    {
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor != null && motor.IsAuthoritative)
            return motor.transform;
        GameObject tagged = GameObject.FindGameObjectWithTag("Player");
        return tagged != null ? tagged.transform : null;
    }

    private static YQInvestorPlayerMotor ResolvePlayerMotor(Transform player)
    {
        // note: Support authored player roots that keep the CharacterController motor on a child while avoiding a per-frame hierarchy search.
        if (player == null)
            return null;
        return player.GetComponent<YQInvestorPlayerMotor>() ?? player.GetComponentInChildren<YQInvestorPlayerMotor>();
    }

    private Terrain ResolveTerrainForChunk(Vector2Int coordinate)
    {
        if (_terrain == null || _terrain.terrainData == null)
            return null;
        if (IsChunkInsideAuthoredTerrain(coordinate))
            return _terrain;
        return _extendedTerrainTiles.TryGetValue(coordinate, out Terrain existing) ? existing : null;
    }

    private Terrain ResolveTerrainForContentMaterialization(Vector2Int coordinate)
    {
        // note: Use only published terrain or a current hidden TerrainCreated ticket whose complete canonical heightfield is already assigned.
        Terrain terrain = ResolveTerrainForChunk(coordinate);
        if (terrain != null || !_pendingTerrainPublications.TryGetValue(coordinate, out PendingTerrainPublication publication) ||
            publication == null || publication.cancelled || publication.published ||
            publication.terrain == null || publication.data == null ||
            publication.terrain.terrainData != publication.data ||
            !IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), publication.token))
            return terrain;
        return publication.terrain;
    }

    private static float[,] SampleAuthorityHeightmap(
        YQContinuousWorldCellAuthority authority,
        int extensionResolution,
        float tileMinX,
        float tileMinZ,
        float size,
        CancellationToken cancellationToken,
        out int spatialSampleEvaluations,
        out int spatialSampleCacheHits)
    {
        // note: Use one deterministic sampling loop for background preparation and deadline fallback so both publish identical same-authority heights.
        if (authority == null || extensionResolution < 2 || size <= 0f)
            throw new InvalidOperationException("continuous authority heightmap inputs are invalid");
        spatialSampleEvaluations = 0;
        spatialSampleCacheHits = 0;
        float[,] preparedHeights = new float[extensionResolution, extensionResolution];
        using (YQContinuousWorldCellAuthority.HeightmapSamplingSession session =
            authority.BeginHeightmapSamplingSession())
        {
            for (int z = 0; z < extensionResolution; z++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < extensionResolution; x++)
                {
                    float worldX = tileMinX + x / (float)(extensionResolution - 1) * size;
                    float worldZ = tileMinZ + z / (float)(extensionResolution - 1) * size;
                    float normalizedHeight = session.SampleHeightNormalized(worldX, worldZ);
                    if (float.IsNaN(normalizedHeight) || float.IsInfinity(normalizedHeight))
                        throw new InvalidOperationException("continuous authority returned a non-finite terrain height");
                    preparedHeights[z, x] = normalizedHeight;
                }
            }
            spatialSampleEvaluations = session.SpatialSampleEvaluations;
            spatialSampleCacheHits = session.SpatialSampleCacheHits;
        }
        return preparedHeights;
    }

    private static float[,] UpsampleHeightmapBilinear(float[,] source, int targetResolution)
    {
        // note: Preserve the emergency authority samples while expanding them to a TerrainData-compatible grid for immediate safety-floor publication.
        if (source == null || source.GetLength(0) < 2 || source.GetLength(1) != source.GetLength(0) ||
            targetResolution < source.GetLength(0))
            throw new ArgumentException("heightmap upsample inputs are invalid", nameof(source));

        int sourceResolution = source.GetLength(0);
        float[,] result = new float[targetResolution, targetResolution];
        for (int z = 0; z < targetResolution; z++)
        {
            float sourceZ = z * (sourceResolution - 1f) / (targetResolution - 1f);
            int z0 = Mathf.FloorToInt(sourceZ);
            int z1 = Mathf.Min(sourceResolution - 1, z0 + 1);
            float zBlend = sourceZ - z0;
            for (int x = 0; x < targetResolution; x++)
            {
                float sourceX = x * (sourceResolution - 1f) / (targetResolution - 1f);
                int x0 = Mathf.FloorToInt(sourceX);
                int x1 = Mathf.Min(sourceResolution - 1, x0 + 1);
                float xBlend = sourceX - x0;
                float top = Mathf.Lerp(source[z0, x0], source[z0, x1], xBlend);
                float bottom = Mathf.Lerp(source[z1, x0], source[z1, x1], xBlend);
                result[z, x] = Mathf.Lerp(top, bottom, zBlend);
            }
        }
        return result;
    }

    private Task<float[,]> StartBackgroundHeightTask(
        Vector2Int coordinate,
        int extensionResolution,
        float tileMinX,
        float tileMinZ,
        float size,
        CancellationToken cancellationToken)
    {
        YQContinuousWorldCellAuthority authority = _continuousAuthority;
#if UNITY_EDITOR
        // note: Read profiler state on the Unity thread; the worker only uses this immutable diagnostic flag.
        bool traceHeightWorker = UnityEngine.Profiling.Profiler.enabled;
#endif
        // note: Capture only immutable accepted-world sampling inputs before leaving the Unity thread; no UnityEngine object is touched by the worker.
        return Task.Run(() =>
        {
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
#if UNITY_EDITOR
            if (traceHeightWorker)
                BackgroundHeightSamplingMarker.Begin();
#endif
            try
            {
                float[,] heights = SampleAuthorityHeightmap(
                    authority, extensionResolution, tileMinX, tileMinZ, size,
                    cancellationToken, out _, out _);
                // note: Record the actual task outcome without changing cancellation, sampled values, or publication ownership.
                Interlocked.Increment(ref _backgroundHeightTaskSucceededCount);
                return heights;
            }
            catch (OperationCanceledException)
            {
                Interlocked.Increment(ref _backgroundHeightTaskCanceledCount);
                throw;
            }
            catch
            {
                Interlocked.Increment(ref _backgroundHeightTaskFaultedCount);
                throw;
            }
            finally
            {
#if UNITY_EDITOR
                // note: Close the worker scope even when sampling is canceled or fails.
                if (traceHeightWorker)
                    BackgroundHeightSamplingMarker.End();
#endif
                // note: One atomic sample per tile exposes worker throughput without touching deterministic height values or Unity objects.
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                Interlocked.Increment(ref _backgroundHeightTaskCount);
                Interlocked.Add(ref _backgroundHeightTaskTotalTicks, elapsed);
                long previous = Interlocked.Read(ref _backgroundHeightTaskMaximumTicks);
                while (elapsed > previous)
                {
                    long observed = Interlocked.CompareExchange(ref _backgroundHeightTaskMaximumTicks, elapsed, previous);
                    if (observed == previous)
                        break;
                    previous = observed;
                }
            }
        }, cancellationToken);
    }

    private void MaintainTerrainHeightPrefetch()
    {
        _terrainHeightPrefetchScratch.Clear();
        foreach (KeyValuePair<Vector2Int, Task<float[,]>> pair in _terrainHeightPrefetches)
        {
            if (!IsTerrainRequestStillRelevant(pair.Key))
                _terrainHeightPrefetchScratch.Add(pair.Key);
        }
        for (int index = 0; index < _terrainHeightPrefetchScratch.Count; index++)
        {
            Vector2Int coordinate = _terrainHeightPrefetchScratch[index];
            if (_terrainHeightPrefetchCancellations.TryGetValue(coordinate, out CancellationTokenSource cancellation))
                cancellation.Cancel();
            _terrainHeightPrefetches.Remove(coordinate);
            _terrainHeightPrefetchCancellations.Remove(coordinate);
        }
        _terrainHeightPrefetchScratch.Clear();

        if (_terrainHeightPrefetches.Count >= MaximumTerrainHeightPrefetches ||
            _terrainQueue.Count == 0 || _terrain == null || _terrain.terrainData == null ||
            _continuousAuthority == null)
            return;

        float size = Mathf.Max(32f, chunkWorldSize);
        float originSpacing = Mathf.Max(
            _terrain.terrainData.size.x / Mathf.Max(1f, _terrain.terrainData.heightmapResolution - 1f),
            _terrain.terrainData.size.z / Mathf.Max(1f, _terrain.terrainData.heightmapResolution - 1f));
        int extensionResolution = ResolveSupportedHeightmapResolution(size, originSpacing);
        for (int index = 0; index < _terrainQueue.Count; index++)
        {
            Vector2Int coordinate = _terrainQueue[index];
            if (_terrainHeightPrefetches.ContainsKey(coordinate) ||
                _pendingTerrainPublications.ContainsKey(coordinate) ||
                coordinate == _terrainPreparingCoordinate ||
                !IsTerrainRequestStillRelevant(coordinate) ||
                IsChunkInsideAuthoredTerrain(coordinate) ||
                HasPublishedTerrain(coordinate))
                continue;

            float tileMinX = WorldGridOrigin + coordinate.x * size;
            float tileMaxX = WorldGridOrigin + (coordinate.x + 1) * size;
            float tileMinZ = WorldGridOrigin + coordinate.y * size;
            float tileMaxZ = WorldGridOrigin + (coordinate.y + 1) * size;
            if (!_continuousAuthority.CanSampleRectangleOffMainThread(tileMinX, tileMaxX, tileMinZ, tileMaxZ))
                continue;

            CancellationTokenSource cancellation = new CancellationTokenSource();
            _terrainHeightPrefetchCancellations[coordinate] = cancellation;
            _terrainHeightPrefetches[coordinate] = StartBackgroundHeightTask(
                coordinate,
                extensionResolution,
                tileMinX,
                tileMinZ,
                size,
                cancellation.Token);
            // note: One prefetch is deliberate; it overlaps scalar work without multiplying Unity TerrainData, collider, or painting pressure.
            break;
        }
    }

    private void CancelAllTerrainHeightPrefetches()
    {
        foreach (CancellationTokenSource cancellation in _terrainHeightPrefetchCancellations.Values)
            cancellation?.Cancel();
        _terrainHeightPrefetchCancellations.Clear();
        _terrainHeightPrefetches.Clear();
        _terrainHeightPrefetchScratch.Clear();
    }

    private IEnumerator CreateExtensionTerrainRoutine(Vector2Int coordinate, StreamingWorkToken token)
    {
        if (_terrain == null || _terrain.terrainData == null || _continuousAuthority == null ||
            !IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), token))
            yield break;

        float terrainBuildStarted = Time.realtimeSinceStartup;
        TerrainData data = null;
        GameObject tileObject = null;
        bool publicationCommitted = false;
        bool publicationOwnershipTransferred = false;
        bool dataOwnershipTransferred = false;
        YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap appearancePreparation = null;
        CancellationTokenSource backgroundCancellation = null;
        Task<float[,]> backgroundHeightTask = null;
        try
        {
        _terrainPreparationPhase = "allocate";
        float size = Mathf.Max(32f, chunkWorldSize);
        float originSpacing = Mathf.Max(
            _terrain.terrainData.size.x / Mathf.Max(1f, _terrain.terrainData.heightmapResolution - 1f),
            _terrain.terrainData.size.z / Mathf.Max(1f, _terrain.terrainData.heightmapResolution - 1f));
        // note: Derive the continuation sample count from the origin's world spacing instead of hard-coding a resolution for a different terrain size.
        // note: Unity accepts power-of-two-plus-one heightmap resolutions; choose the nearest supported grid to preserve ordinary TerrainData compatibility.
            // note: Streamed terrain keeps the origin spacing so edge samples, cell size, and heightfield identity remain seamless across the physical world.
            int extensionResolution = ResolveSupportedHeightmapResolution(size, originSpacing);
        data = new TerrainData
        {
            heightmapResolution = extensionResolution,
            size = new Vector3(size, YQGeneratedWorldTerrain.TerrainHeight, size)
        };
        data.alphamapResolution = 64;
        if (_terrain.terrainData.terrainLayers != null)
            data.terrainLayers = _terrain.terrainData.terrainLayers;
        if ((IsAppearanceDeadlineDemand(coordinate) || _hardViewDemand.Contains(coordinate)) &&
            data.terrainLayers != null && data.terrainLayers.Length > 0)
        {
            // note: Prepare deterministic camera-demanded biome weights alongside heightfield work; defer native TerrainData writes until collider publication.
            appearancePreparation = YQContinuousWorldFeatureMaterializer.BeginBiomeAlphamapPreparation(
                data,
                _continuousAuthority,
                coordinate,
                size);
        }
        Vector3 baseOrigin = _terrain.transform.position;
        Vector3 baseSize = _terrain.terrainData.size;
        float tileMinX = WorldGridOrigin + coordinate.x * size;
        float tileMaxX = WorldGridOrigin + (coordinate.x + 1) * size;
        float tileMinZ = WorldGridOrigin + coordinate.y * size;
        float tileMaxZ = WorldGridOrigin + (coordinate.y + 1) * size;
        float overlapZ = Mathf.Min(tileMaxZ, baseOrigin.z + baseSize.z) - Mathf.Max(tileMinZ, baseOrigin.z);
        float overlapX = Mathf.Min(tileMaxX, baseOrigin.x + baseSize.x) - Mathf.Max(tileMinX, baseOrigin.x);
        // note: A diagonal tile that only touches an origin corner must use the continuous authority; copying a clamped corner across its full edge creates a false ridge.
        bool sharesBaseWest = Mathf.Abs(tileMinX - (baseOrigin.x + baseSize.x)) < 0.01f && overlapZ > 0.01f;
        bool sharesBaseEast = Mathf.Abs(tileMaxX - baseOrigin.x) < 0.01f && overlapZ > 0.01f;
        bool sharesBaseSouth = Mathf.Abs(tileMinZ - (baseOrigin.z + baseSize.z)) < 0.01f && overlapX > 0.01f;
        bool sharesBaseNorth = Mathf.Abs(tileMaxZ - baseOrigin.z) < 0.01f && overlapX > 0.01f;
        // note: Keep pure deterministic scalar sampling off the main thread whenever the accepted authority supports it; Unity terrain mutation remains on the main thread.
        bool backgroundHeightPreparation =
            _continuousAuthority.CanSampleRectangleOffMainThread(tileMinX, tileMaxX, tileMinZ, tileMaxZ);
        if (backgroundHeightPreparation)
        {
            _terrainPreparationPhase = "heightTask";
            // note: Consume the next immutable heightfield when available; Unity TerrainData/object/collider work remains below on the main thread.
            if (_terrainHeightPrefetches.TryGetValue(coordinate, out backgroundHeightTask))
            {
                _terrainHeightPrefetches.Remove(coordinate);
                _terrainHeightPrefetchCancellations.TryGetValue(coordinate, out backgroundCancellation);
                _terrainHeightPrefetchCancellations.Remove(coordinate);
            }
            else
            {
                backgroundCancellation = new CancellationTokenSource();
                backgroundHeightTask = StartBackgroundHeightTask(
                    coordinate,
                    extensionResolution,
                    tileMinX,
                    tileMinZ,
                    size,
                    backgroundCancellation.Token);
            }
            _terrainHeightPreparationCancellation = backgroundCancellation;
            while (!backgroundHeightTask.IsCompleted)
            {
                // note: Allow an urgent site-grounding dependency to cancel pure off-main-thread sampling before its six-second wait expires; no Unity TerrainData has been published yet.
                if (ShouldPreemptTerrainPreparation(coordinate))
                {
                    _terrainPreparationPreempted = true;
                    backgroundCancellation.Cancel();
                    RecordFixedTerrainObservation("backgroundCancellationRequested", coordinate, token.workId);
                }
                yield return null;
            }
            if (!IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), token))
            {
                _terrainPreparationPreempted = true;
                yield break;
            }
            if (backgroundHeightTask.IsCanceled)
            {
                _terrainPreparationPreempted = true;
                RecordFixedTerrainObservation("backgroundCancellationAcknowledged", coordinate, token.workId);
                yield break;
            }
            if (backgroundHeightTask.IsFaulted)
                throw backgroundHeightTask.Exception != null ? backgroundHeightTask.Exception.GetBaseException() : new InvalidOperationException("background terrain preparation failed");
            float[,] preparedHeights = backgroundHeightTask.Result;
            RecordSiteTerrainProgress(coordinate, 1);
            // note: The immutable height rectangle is complete before any TerrainData publication, which is the terrain-sampled readiness boundary.
            MarkTerrainReadiness(coordinate, YQTerrainReadinessState.TerrainSampled);
            // note: Reapply the live authored Terrain only on the exact shared border; all collar-interior math came from the immutable captured heightfield.
            int seamSamplesInSlice = 0;
            _terrainPreparationPhase = "seamUpload";
            for (int z = 0; z < extensionResolution; z++)
            {
                if (_terrainPreparationPreempted)
                {
                    _terrainPreparationPreempted = true;
                    yield break;
                }
                for (int x = 0; x < extensionResolution; x++)
                {
                    bool onSharedBaseEdge =
                        (sharesBaseWest && x == 0) ||
                        (sharesBaseEast && x == extensionResolution - 1) ||
                        (sharesBaseSouth && z == 0) ||
                        (sharesBaseNorth && z == extensionResolution - 1);
                    if (!onSharedBaseEdge)
                        continue;
                    float worldX = tileMinX + x / (float)(extensionResolution - 1) * size;
                    float worldZ = tileMinZ + z / (float)(extensionResolution - 1) * size;
                    float baseHeight = _terrain.SampleHeight(new Vector3(worldX, 0f, worldZ)) + baseOrigin.y;
                    preparedHeights[z, x] = Mathf.InverseLerp(baseOrigin.y, baseOrigin.y + baseSize.y, baseHeight);
                    seamSamplesInSlice++;
                    if (seamSamplesInSlice < TerrainSeamSamplesPerSlice)
                        continue;
                    seamSamplesInSlice = 0;
                    yield return ContinueTerrainPreparationWithinFrameBudget;
                    if (ShouldPreemptTerrainPreparation(coordinate))
                    {
                        _terrainPreparationPreempted = true;
                        yield break;
                    }
                }
            }
            float travelSpeed = ResolveTraversalVelocity().magnitude;
            int preparedRowsPerSlice = travelSpeed >= HighSpeedPreparedTerrainThresholdMetersPerSecond
                ? HighSpeedPreparedTerrainRowsPerSlice
                : PreparedTerrainRowsPerSlice;
            for (int startZ = 0; startZ < extensionResolution; startZ += preparedRowsPerSlice)
            {
                _terrainPreparationPhase = "heightUpload";
                if (_terrainPreparationPreempted)
                {
                    // note: The completed immutable heightfield is still unpublished, so unload can abandon it without touching a live Terrain object.
                    _terrainPreparationPreempted = true;
                    yield break;
                }
                if (ShouldPreemptTerrainPreparation(coordinate))
                {
                    // note: The pure height task has completed and no Unity terrain is published, making this a safe owner handoff boundary.
                    _terrainPreparationPreempted = true;
                    yield break;
                }
                int rowCount = Mathf.Min(preparedRowsPerSlice, extensionResolution - startZ);
                float[,] uploadStrip = new float[rowCount, extensionResolution];
                for (int row = 0; row < rowCount; row++)
                    for (int x = 0; x < extensionResolution; x++)
                        uploadStrip[row, x] = preparedHeights[startZ + row, x];
                data.SetHeightsDelayLOD(0, startZ, uploadStrip);
                RecordSiteTerrainProgress(coordinate, 1 + startZ + rowCount);
                yield return ContinueTerrainPreparationWithinFrameBudget;
            }
            // note: Main-thread sampling reaches the same readiness boundary only after every delayed height row has been prepared.
            MarkTerrainReadiness(coordinate, YQTerrainReadinessState.TerrainSampled);
        }
        else
        {
            _terrainPreparationPhase = "mainThreadHeight";
            // note: Collar tiles retain bounded main-thread sampling because exact seam matching may read the authored Terrain heightfield.
            int rowsPerSlice = TerrainRowsPerSlice;
            for (int startZ = 0; startZ < extensionResolution; startZ += rowsPerSlice)
            {
                if (_terrainPreparationPreempted)
                {
                    // note: Main-thread row uploads remain disposable until the TerrainCollider publication boundary is committed.
                    _terrainPreparationPreempted = true;
                    yield break;
                }
                if (ShouldPreemptTerrainPreparation(coordinate))
                {
                    // note: Delayed height rows exist only on an unpublished temporary TerrainData, so cancellation between row uploads is safe.
                    _terrainPreparationPreempted = true;
                    yield break;
                }
                int rowCount = Mathf.Min(rowsPerSlice, extensionResolution - startZ);
                float[,] uploadStrip = new float[rowCount, extensionResolution];
                for (int row = 0; row < rowCount; row++)
                {
                    int z = startZ + row;
                    for (int x = 0; x < extensionResolution; x++)
                    {
                        float worldX = tileMinX + x / (float)(extensionResolution - 1) * size;
                        float worldZ = tileMinZ + z / (float)(extensionResolution - 1) * size;
                        float normalizedHeight = _continuousAuthority.SampleHeightNormalized(worldX, worldZ);
                        if (float.IsNaN(normalizedHeight) || float.IsInfinity(normalizedHeight))
                            throw new InvalidOperationException("continuous authority returned a non-finite terrain height");
                        bool onSharedBaseEdge =
                            (sharesBaseWest && x == 0) ||
                            (sharesBaseEast && x == extensionResolution - 1) ||
                            (sharesBaseSouth && z == 0) ||
                            (sharesBaseNorth && z == extensionResolution - 1);
                        if (onSharedBaseEdge)
                        {
                            Vector3 baseSample = new Vector3(worldX, 0f, worldZ);
                            float baseHeight = _terrain.SampleHeight(baseSample) + baseOrigin.y;
                            normalizedHeight = Mathf.InverseLerp(baseOrigin.y, baseOrigin.y + baseSize.y, baseHeight);
                        }
                        uploadStrip[row, x] = normalizedHeight;
                    }
                }
                data.SetHeightsDelayLOD(0, startZ, uploadStrip);
                RecordSiteTerrainProgress(coordinate, 1 + startZ + rowCount);
                yield return ContinueTerrainPreparationWithinFrameBudget;
            }
        }
        // note: Keep delayed heightmap synchronization deferred; the renderer and collider receive the same uploaded TerrainData without a monolithic request-time sync.
        // note: A queued request may become obsolete while rows are being prepared; never publish a terrain tile without a live semantic owner.
        if (!_chunks.TryGetValue(coordinate, out RuntimeChunk owner) || owner == null || !IsTerrainRequestStillRelevant(coordinate))
        {
            yield break;
        }
        tileObject = Terrain.CreateTerrainGameObject(data);
        _terrainPreparationPhase = "createTerrain";
        if (tileObject == null)
            yield break;
        // note: Once Unity has created the terrain object, its TerrainData is owned by that object and must not be destroyed by the cancellation guard.
        dataOwnershipTransferred = true;
        // note: Object creation and lightweight binding share one slice; physics synchronization remains isolated below.
        tileObject.name = "YQ_GENERATED_TERRAIN_CHUNK_" + coordinate.x + "_" + coordinate.y;
        tileObject.transform.SetParent(_worldRoot, false);
        tileObject.transform.position = new Vector3(WorldGridOrigin + coordinate.x * size, baseOrigin.y, WorldGridOrigin + coordinate.y * size);
        Terrain tile = tileObject.GetComponent<Terrain>();
        if (tile == null)
        {
            UnityEngine.Object.Destroy(tileObject);
            tileObject = null;
            yield break;
        }
        // note: Hide a newly created extension terrain before any subsequent yield so an unpublished heightfield can never flash into the camera.
        tile.enabled = false;
        // note: Bind the collider to the same heightfield as the renderer; Terrain.CreateTerrainGameObject does not make this contract explicit on every Unity version.
        TerrainCollider tileCollider = tileObject.GetComponent<TerrainCollider>();
        if (tileCollider == null)
        {
            UnityEngine.Object.Destroy(tileObject);
            tileObject = null;
            yield break;
        }
        tileCollider.terrainData = data;
        // note: TerrainData and TerrainCollider now exist as one owned object, but physics has not yet synchronized the published heightfield.
        MarkTerrainReadiness(coordinate, YQTerrainReadinessState.TerrainCreated);
        RecordSiteTerrainProgress(coordinate, extensionResolution + 2);
        if (tile != null)
        {
            tile.allowAutoConnect = false;
            tile.drawInstanced = true;
            tile.heightmapPixelError = 5f;
            tile.basemapDistance = 1000f;
            // note: Streamed terrain inherits the authored renderer contract so crossing the origin edge cannot reveal Unity's untextured default terrain.
            tile.materialTemplate = _terrain.materialTemplate;
            tile.treeDistance = _terrain.treeDistance;
            tile.treeBillboardDistance = _terrain.treeBillboardDistance;
            tile.treeCrossFadeLength = _terrain.treeCrossFadeLength;
            tile.detailObjectDistance = _terrain.detailObjectDistance;
            tile.detailObjectDensity = _terrain.detailObjectDensity;
            tile.drawTreesAndFoliage = _terrain.drawTreesAndFoliage;
            tile.shadowCastingMode = _terrain.shadowCastingMode;
            tile.reflectionProbeUsage = _terrain.reflectionProbeUsage;
        }
        // note: Force Unity to upload the freshly assigned heightfield in the same bounded binding slice before physics synchronization.
        tile.Flush();
        // note: Preserve both physics frame boundaries but transfer their wait to a bounded publication ticket so height preparation can continue.
        PendingTerrainPublication publication = new PendingTerrainPublication
        {
            coordinate = coordinate,
            token = token,
            data = data,
            terrainObject = tileObject,
            terrain = tile,
            appearancePreparation = appearancePreparation,
            startedAt = Time.realtimeSinceStartup,
            phase = "colliderSync"
        };
        appearancePreparation = null;
        publicationOwnershipTransferred = true;
        yield return publication;
        }
        finally
        {
            if (ReferenceEquals(_terrainHeightPreparationCancellation, backgroundCancellation))
                _terrainHeightPreparationCancellation = null;
            // note: A pre-publication cancellation releases only its own CPU preparation; the collider ticket owns a transferred task.
            appearancePreparation?.Dispose();
            // note: Disposing an unpublished build also stops pure background sampling so a timed-out or retired owner cannot outlive its version.
            if (!publicationCommitted && backgroundHeightTask != null && !backgroundHeightTask.IsCompleted)
                backgroundCancellation?.Cancel();
            // note: Normal and preempted tasks are complete here; teardown cancellation may still be unwinding, so let its token source be collected instead of disposing under the worker.
            if (backgroundHeightTask == null || backgroundHeightTask.IsCompleted)
                backgroundCancellation?.Dispose();
            // note: An unpublished continuation owns both its temporary Terrain object and native TerrainData until publication commits; discard both on cancellation.
            if (!publicationCommitted && !publicationOwnershipTransferred)
            {
                if (tileObject != null)
                    Destroy(tileObject);
                if (data != null)
                    Destroy(data);
            }
            else if (!dataOwnershipTransferred && data != null)
            {
                // note: Retain the defensive ownership check for the impossible partial-publication path without leaking a detached heightfield.
                Destroy(data);
            }
        }
    }

    private static int ResolveSupportedHeightmapResolution(float size, float sampleSpacing)
    {
        // note: Round the spacing-derived sample count to the nearest supported Unity TerrainData resolution instead of assigning an invalid arbitrary count.
        int targetSamples = Mathf.Max(33, Mathf.RoundToInt(size / Mathf.Max(0.01f, sampleSpacing)) + 1);
        int lower = 33;
        int upper = 33;
        while (upper < targetSamples && upper < 4097)
        {
            lower = upper;
            upper = (upper - 1) * 2 + 1;
        }
        return targetSamples - lower <= upper - targetSamples ? lower : upper;
    }

    private IEnumerator PaintTerrainAppearanceRoutine(
        Vector2Int coordinate,
        TerrainData data,
        float size,
        Terrain targetTerrain,
        StreamingWorkToken token,
        long workId,
        YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparedBiomeAlphamap)
    {
        float terrainPaintStarted = Time.realtimeSinceStartup;
        bool completed = false;
        string failure = null;
        Stack<IEnumerator> iterators = new Stack<IEnumerator>();
        Stack<IEnumerator> detailIterators = new Stack<IEnumerator>();
        float frameSliceStarted = Time.realtimeSinceStartup;
        try
        {
            if (!IsTerrainAppearanceWorkCurrent(coordinate, token, workId))
                yield break;
            if (data == null || targetTerrain == null || _continuousAuthority == null)
            {
                failure = "terrain appearance prerequisites are unavailable";
                yield break;
            }
            iterators.Push(YQContinuousWorldFeatureMaterializer.PaintBiomeAlphamapsRoutine(
                data, _continuousAuthority, coordinate, size, false, preparedBiomeAlphamap));
            int streamedDetailCount = -1;
            if (data != null && _continuousAuthority != null)
            {
                // note: Biome weights and streamed grass use independent TerrainData channels; advance both under the same owner and frame budget so one publication lane does not add its full latency after the other.
                detailIterators.Push(YQGeneratedWorldEnvironment.PaintStreamedDetailRoutine(
                    targetTerrain,
                    _plan,
                    coordinate,
                    _continuousAuthority,
                    YQRuntimeWorldAssetRegistry.Instance,
                    count => streamedDetailCount = count));
            }
            bool advanceBiomeFirst = true;
            bool authoredAlphaEdgesQueued = false;
            while (iterators.Count > 0 || detailIterators.Count > 0)
            {
                // note: A retired painter must stop before advancing another nested Unity mutation.
                if (!IsTerrainAppearanceWorkCurrent(coordinate, token, workId))
                    yield break;
                if (!authoredAlphaEdgesQueued && iterators.Count == 0)
                {
                    // note: Repair authored alpha seams as soon as biome upload ends so independent grass detail work can overlap the final required stage.
                    iterators.Push(CopyAuthoredAlphaEdgesRoutine(targetTerrain));
                    authoredAlphaEdgesQueued = true;
                }
                // note: Prepared hard-view tiles must complete their accepted appearance lane before camera arrival; distant paint remains subordinate to imminent collision work.
                bool immediateAppearanceDeadline = HasImmediateTerrainAppearanceDeadline(coordinate);
                if ((!immediateAppearanceDeadline && ShouldPauseTerrainAppearanceForVisibleDemand(coordinate)) ||
                    (ShouldReserveFrameForTraversalTerrain() && !immediateAppearanceDeadline))
                {
                    // note: Keep distant partial paint intact while a visible tile is unfinished or collision-critical traversal work is pending.
                    yield return null;
                    frameSliceStarted = Time.realtimeSinceStartup;
                    continue;
                }
                // note: Alternate which independent lane receives the first remaining frame quantum while each iterator keeps its existing cancellation and error boundary.
                Stack<IEnumerator> firstLane = advanceBiomeFirst ? iterators : detailIterators;
                Stack<IEnumerator> secondLane = advanceBiomeFirst ? detailIterators : iterators;
                advanceBiomeFirst = !advanceBiomeFirst;
                bool frameYield = false;
                object yielded = null;
                if (firstLane.Count > 0)
                {
                    object firstYielded;
                    bool firstFrameYield;
                    bool firstLaneAdvanced;
                    OwnerIteratorProgress firstLaneProgress = ReferenceEquals(firstLane, iterators)
                        ? token.owner.biomeAppearanceProgress
                        : token.owner.detailAppearanceProgress;
                    using (TerrainAppearanceSliceMarker.Auto())
                        firstLaneAdvanced = TryAdvancePublicationIterator(
                            firstLane,
                            out firstYielded,
                            out firstFrameYield,
                            out failure,
                            budgetStage: HasImmediateTerrainAppearanceDeadline(coordinate)
                                ? "hardViewTerrainAppearance"
                                : "appearanceOrEcology",
                            progress: firstLaneProgress,
                            appearanceCoordinate: coordinate);
                    if (ReferenceEquals(firstLane, detailIterators) && firstLaneProgress.lastStepFrame == Time.frameCount)
                        token.owner.appearanceDetailSubstage = YQGeneratedWorldEnvironment.LastStreamedDetailStep;
                    if (!firstLaneAdvanced)
                        yield break;
                    frameYield = firstFrameYield;
                    yielded = firstYielded;
                }
                // note: Both streamed providers yield at frame boundaries; share one boundary after giving each active lane a chance within the aggregate budget.
                if (secondLane.Count > 0 && yielded == null)
                {
                    object secondYielded;
                    bool secondFrameYield;
                    bool secondLaneAdvanced;
                    OwnerIteratorProgress secondLaneProgress = ReferenceEquals(secondLane, iterators)
                        ? token.owner.biomeAppearanceProgress
                        : token.owner.detailAppearanceProgress;
                    using (TerrainAppearanceSliceMarker.Auto())
                        secondLaneAdvanced = TryAdvancePublicationIterator(
                            secondLane,
                            out secondYielded,
                            out secondFrameYield,
                            out failure,
                            budgetStage: HasImmediateTerrainAppearanceDeadline(coordinate)
                                ? "hardViewTerrainAppearance"
                                : "appearanceOrEcology",
                            progress: secondLaneProgress,
                            appearanceCoordinate: coordinate);
                    if (ReferenceEquals(secondLane, detailIterators) && secondLaneProgress.lastStepFrame == Time.frameCount)
                        token.owner.appearanceDetailSubstage = YQGeneratedWorldEnvironment.LastStreamedDetailStep;
                    if (!secondLaneAdvanced)
                        yield break;
                    frameYield |= secondFrameYield;
                    if (yielded == null)
                        yielded = secondYielded;
                }
                if (frameYield)
                {
                    // note: Record the combined painter quantum so the existing frame telemetry reflects both independent publication lanes.
                    _lastTerrainPaintSliceSeconds = Time.realtimeSinceStartup - frameSliceStarted;
                    _maximumTerrainPaintSliceSeconds = Mathf.Max(_maximumTerrainPaintSliceSeconds, _lastTerrainPaintSliceSeconds);
                    yield return yielded;
                    frameSliceStarted = Time.realtimeSinceStartup;
                }
                else if (iterators.Count == 0 && detailIterators.Count == 0)
                    break;
            }
            if (data != null && _continuousAuthority != null)
            {
                if (streamedDetailCount < 0)
                {
                    // note: A canonical V2 tile cannot publish appearance when accepted grass masks or the approved detail asset were unavailable.
                    failure = "streamed terrain detail publication returned no accepted result";
                    Debug.LogError("[YQSemanticChunkStreamer] STREAMED DETAIL PUBLISH FAILED " + coordinate);
                    yield break;
                }
            }
            if (!IsTerrainAppearanceWorkCurrent(coordinate, token, workId))
                yield break;
            // note: Delay only the final appearance publication; collision remains available but the renderer stays withheld until release.
            while (IsPublicationVerificationStageHeld(coordinate, YQPublicationVerificationStage.Appearance))
            {
                if (!IsTerrainAppearanceWorkCurrent(coordinate, token, workId))
                    yield break;
                yield return null;
                frameSliceStarted = Time.realtimeSinceStartup;
            }
            if (ConsumePublicationVerificationFailure(coordinate, YQPublicationVerificationStage.Appearance))
            {
                // note: Exercise the real nested-iterator exception boundary without adding a synthetic wait or directly scheduling a retry.
                while (!CanAdvanceAggregateWork("appearanceFailureProbe"))
                    yield return null;
                iterators.Push(ThrowPublicationVerificationFailure("verification appearance fault"));
                if (!TryAdvancePublicationIterator(iterators, out _, out _, out failure, false))
                    yield break;
            }
            if (failure != null)
                yield break;
            // note: Include a no-yield paint completion tail even when the alphamap iterator ends immediately.
            _lastTerrainPaintSliceSeconds = Time.realtimeSinceStartup - frameSliceStarted;
            _maximumTerrainPaintSliceSeconds = Mathf.Max(_maximumTerrainPaintSliceSeconds, _lastTerrainPaintSliceSeconds);
            completed = true;
        }
        finally
        {
            try
            {
                // note: A throwing disposer cannot strand the painter registration or bypass its retry budget.
                string disposalFailure = DisposePublicationIterators(iterators);
                string detailDisposalFailure = DisposePublicationIterators(detailIterators);
                failure = failure ?? disposalFailure;
                failure = failure ?? detailDisposalFailure;
                bool currentWork = IsTerrainAppearanceWorkCurrent(coordinate, token, workId);
                // note: A cancelled painter is cleanup, not a completed performance sample; only normal completion updates the report.
                bool matchingPendingTicket = _pendingTerrainPublications.TryGetValue(
                        coordinate, out PendingTerrainPublication pendingTicket) &&
                    pendingTicket != null && !pendingTicket.cancelled && !pendingTicket.published &&
                    pendingTicket.terrain == targetTerrain && pendingTicket.data == data;
                if (completed && failure == null && currentWork && matchingPendingTicket)
                {
                    // note: Record prepared channels on their pending owner, but leave terrain and visual readiness false until collider publication.
                    pendingTicket.appearancePrepared = true;
                }
                else if (completed && failure == null && currentWork &&
                    _extendedTerrainTiles.TryGetValue(coordinate, out Terrain publishedTerrain) &&
                    publishedTerrain != null && publishedTerrain == targetTerrain && token.owner.terrainPublished)
                {
                    // note: Preserve an exact-view ground preview when terrain appearance finishes before the optional chunk activation slice.
                    publishedTerrain.enabled = ShouldExposeTerrainPreview(token.owner);
                    publishedTerrain.drawTreesAndFoliage = true;
                    RuntimeChunk visualOwner = token.owner;
                    visualOwner.appearanceReady = true;
                    visualOwner.visualReady = true;
                    visualOwner.publicationVersion++;
                    _terrainAppearanceRetryAt.Remove(coordinate);
                    _terrainAppearanceRetryCount.Remove(coordinate);
                    _lifecyclePending = true;
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
                    Debug.Log("[YQSemanticChunkStreamer] TERRAIN APPEARANCE PUBLISHED " + coordinate +
                        " seconds=" + (Time.realtimeSinceStartup - terrainPaintStarted).ToString("0.000"));
#endif
                    _lastTerrainPaintSeconds = Time.realtimeSinceStartup - terrainPaintStarted;
                    _maximumTerrainPaintSeconds = Mathf.Max(_maximumTerrainPaintSeconds, _lastTerrainPaintSeconds);
                }
                else if (currentWork && token.owner.terrainPublished && !token.owner.appearanceReady)
                {
                    // note: Every unsuccessful live attempt consumes exactly one retry; terminal failures leave no retry timer behind.
                    RecordTerrainAppearanceRetryFailure(coordinate, token.owner,
                        failure ?? "terrain binding changed before appearance completion");
                }
            }
            finally
            {
                try
                {
                    // note: A canceled parent may retire before the nested biome iterator starts, so release its task explicitly as an idempotent owner cleanup.
                    preparedBiomeAlphamap?.Dispose();
                }
                finally
                {
                    // note: Always retire this registration, including when publishing its receipt throws; preserve any replacement painter.
                    if (_terrainPaintingWorkIds.TryGetValue(coordinate, out long currentWorkId) &&
                        currentWorkId == workId)
                    {
                        _terrainPainting.Remove(coordinate);
                        _terrainPaintingWorkIds.Remove(coordinate);
                    }
                }
            }
        }
    }

    private static IEnumerator ThrowPublicationVerificationFailure(string failure)
    {
        // note: Keep the fault inside MoveNext so verification exercises ordinary iterator exception recovery with no timing override.
        if (!string.IsNullOrEmpty(failure))
            throw new InvalidOperationException(failure);
        yield break;
    }

    private bool IsTerrainAppearanceWorkCurrent(Vector2Int coordinate, StreamingWorkToken token, long workId)
    {
        // note: Owner identity and the painter registration both have to match before work or publication can proceed.
        return IsGenerationCurrent(coordinate, GetRuntimeChunk(coordinate), token) &&
            _terrainPaintingWorkIds.TryGetValue(coordinate, out long currentWorkId) && currentWorkId == workId;
    }

    private IEnumerator CopyAuthoredAlphaEdgesRoutine(Terrain targetTerrain)
    {
        if (targetTerrain == null || targetTerrain.terrainData == null || _terrain == null || _terrain.terrainData == null)
            yield break;
        TerrainData sourceData = _terrain.terrainData;
        TerrainData targetData = targetTerrain.terrainData;
        TerrainLayer[] sourceLayers = sourceData.terrainLayers;
        TerrainLayer[] targetLayers = targetData.terrainLayers;
        if (sourceLayers == null || targetLayers == null || sourceLayers.Length == 0 || targetLayers.Length == 0)
            yield break;
        float sourceMinX = _terrain.transform.position.x;
        float sourceMaxX = sourceMinX + sourceData.size.x;
        float sourceMinZ = _terrain.transform.position.z;
        float sourceMaxZ = sourceMinZ + sourceData.size.z;
        float targetMinX = targetTerrain.transform.position.x;
        float targetMaxX = targetMinX + targetData.size.x;
        float targetMinZ = targetTerrain.transform.position.z;
        float targetMaxZ = targetMinZ + targetData.size.z;
        bool copyWest = Mathf.Abs(targetMinX - sourceMaxX) < 0.5f && targetMaxZ > sourceMinZ && targetMinZ < sourceMaxZ;
        bool copyEast = Mathf.Abs(targetMaxX - sourceMinX) < 0.5f && targetMaxZ > sourceMinZ && targetMinZ < sourceMaxZ;
        bool copySouth = Mathf.Abs(targetMinZ - sourceMaxZ) < 0.5f && targetMaxX > sourceMinX && targetMinX < sourceMaxX;
        bool copyNorth = Mathf.Abs(targetMaxZ - sourceMinZ) < 0.5f && targetMaxX > sourceMinX && targetMinX < sourceMaxX;
        if (!copyWest && !copyEast && !copySouth && !copyNorth)
            yield break;
        int targetResolution = Mathf.Max(1, targetData.alphamapResolution);
        int layerCount = Mathf.Min(sourceLayers.Length, targetLayers.Length);
        int sourceResolution = Mathf.Max(1, sourceData.alphamapResolution);
        if (copyWest || copyEast)
        {
            int targetX = copyWest ? 0 : targetResolution - 1;
            int sourceX = copyWest ? sourceResolution - 1 : 0;
            float[,,] sourceEdge = sourceData.GetAlphamaps(sourceX, 0, 1, sourceResolution);
            float[,,] targetEdge = new float[targetResolution, 1, layerCount];
            for (int z = 0; z < targetResolution; z++)
            {
                float worldZ = targetMinZ + z / (float)Mathf.Max(1, targetResolution - 1) * targetData.size.z;
                float normalizedZ = Mathf.Clamp01((worldZ - sourceMinZ) / Mathf.Max(1f, sourceData.size.z));
                int sourceZ = Mathf.Clamp(Mathf.RoundToInt(normalizedZ * (sourceResolution - 1)), 0, sourceResolution - 1);
                for (int layer = 0; layer < layerCount; layer++)
                    targetEdge[z, 0, layer] = sourceEdge[sourceZ, 0, layer];
            }
            // note: Copy only the shared authored column; the tile remains hidden until all edge strips are committed.
            targetData.SetAlphamaps(targetX, 0, targetEdge);
            yield return null;
        }
        if (copySouth || copyNorth)
        {
            int targetZ = copySouth ? 0 : targetResolution - 1;
            int sourceZ = copySouth ? sourceResolution - 1 : 0;
            float[,,] sourceEdge = sourceData.GetAlphamaps(0, sourceZ, sourceResolution, 1);
            float[,,] targetEdge = new float[1, targetResolution, layerCount];
            for (int x = 0; x < targetResolution; x++)
            {
                float worldX = targetMinX + x / (float)Mathf.Max(1, targetResolution - 1) * targetData.size.x;
                float normalizedX = Mathf.Clamp01((worldX - sourceMinX) / Mathf.Max(1f, sourceData.size.x));
                int sourceX = Mathf.Clamp(Mathf.RoundToInt(normalizedX * (sourceResolution - 1)), 0, sourceResolution - 1);
                for (int layer = 0; layer < layerCount; layer++)
                    targetEdge[0, x, layer] = sourceEdge[0, sourceX, layer];
            }
            // note: Match the authored horizontal border without a full alphamap readback or rewrite.
            targetData.SetAlphamaps(0, targetZ, targetEdge);
            yield return null;
        }
    }

    private void RefreshTerrainNeighbors(Vector2Int coordinate)
    {
        if (!_extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) || terrain == null)
            return;

        // note: Streamed tiles share the same size and spacing, so their native neighbor stitching remains valid while the larger origin keeps its canonical seam samples.
        terrain.SetNeighbors(
            GetExtensionTerrain(new Vector2Int(coordinate.x - 1, coordinate.y)),
            GetExtensionTerrain(new Vector2Int(coordinate.x, coordinate.y + 1)),
            GetExtensionTerrain(new Vector2Int(coordinate.x + 1, coordinate.y)),
            GetExtensionTerrain(new Vector2Int(coordinate.x, coordinate.y - 1)));
        RefreshTerrainNeighborsWithoutRecursion(new Vector2Int(coordinate.x - 1, coordinate.y));
        RefreshTerrainNeighborsWithoutRecursion(new Vector2Int(coordinate.x + 1, coordinate.y));
        RefreshTerrainNeighborsWithoutRecursion(new Vector2Int(coordinate.x, coordinate.y - 1));
        RefreshTerrainNeighborsWithoutRecursion(new Vector2Int(coordinate.x, coordinate.y + 1));
    }

    private Terrain GetExtensionTerrain(Vector2Int coordinate)
    {
        return _extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) ? terrain : null;
    }

    private bool HasPublishedTerrain(Vector2Int coordinate)
    {
        if (_extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) &&
            terrain != null && terrain.terrainData != null &&
            IsTerrainColliderReady(terrain) &&
            _chunks.TryGetValue(coordinate, out RuntimeChunk owner) && owner != null &&
            owner.terrainReadiness >= YQTerrainReadinessState.CollisionReady)
            return true;
        if (_extendedTerrainTiles.ContainsKey(coordinate))
            _extendedTerrainTiles.Remove(coordinate);
        return false;
    }

    private void MarkTerrainReadiness(Vector2Int coordinate, YQTerrainReadinessState state)
    {
        // note: Readiness is monotonic within one tile build so a later observation cannot regress a valid staged state.
        if (_chunks.TryGetValue(coordinate, out RuntimeChunk owner) && owner != null && state > owner.terrainReadiness)
        {
            owner.terrainReadiness = state;
            if (state >= YQTerrainReadinessState.CollisionReady && owner.progressStartedAt >= 0d &&
                owner.progressEpoch == owner.contentOwnerEpoch && owner.epochCollisionAt < 0d)
                owner.epochCollisionAt = Time.realtimeSinceStartupAsDouble;
            if (state >= YQTerrainReadinessState.CollisionReady && owner.collisionReadyAt < 0f)
                owner.collisionReadyAt = Time.unscaledTime;
            owner.publicationVersion++;
        }
    }

    private static bool IsTerrainColliderReady(Terrain terrain)
    {
        // note: Collision-ready means the terrain object, data, enabled non-trigger collider, and active hierarchy all agree.
        TerrainCollider collider = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
        return terrain != null && terrain.terrainData != null && collider != null && collider.enabled && !collider.isTrigger && terrain.gameObject.activeInHierarchy;
    }

    private void RefreshTerrainNeighborsWithoutRecursion(Vector2Int coordinate)
    {
        if (!_extendedTerrainTiles.TryGetValue(coordinate, out Terrain terrain) || terrain == null)
            return;
        terrain.SetNeighbors(
            GetExtensionTerrain(new Vector2Int(coordinate.x - 1, coordinate.y)),
            GetExtensionTerrain(new Vector2Int(coordinate.x, coordinate.y + 1)),
            GetExtensionTerrain(new Vector2Int(coordinate.x + 1, coordinate.y)),
            GetExtensionTerrain(new Vector2Int(coordinate.x, coordinate.y - 1)));
    }

    private bool IsChunkInsideAuthoredTerrain(Vector2Int coordinate)
    {
        if (_terrain == null || _terrain.terrainData == null)
            return false;
        float size = Mathf.Max(32f, chunkWorldSize);
        float half = size * 0.5f;
        float centerX = WorldGridOrigin + coordinate.x * size + half;
        float centerZ = WorldGridOrigin + coordinate.y * size + half;
        Vector3 origin = _terrain.transform.position;
        Vector3 terrainSize = _terrain.terrainData.size;
        return centerX - half >= origin.x && centerX + half <= origin.x + terrainSize.x &&
               centerZ - half >= origin.z && centerZ + half <= origin.z + terrainSize.z;
    }

    private void RegisterTerrainForChunk(Vector2Int coordinate, RuntimeChunk chunk)
    {
        if (chunk == null)
            return;
        Terrain terrain;
        try
        {
            terrain = ResolveTerrainForChunk(coordinate);
        }
        catch (Exception exception)
        {
            chunk.state = YQSemanticChunkLifecycle.Failed;
            Debug.LogError("[YQSemanticChunkStreamer] Terrain provisioning failed for " + coordinate + ": " + exception);
            return;
        }
        if (terrain == null || terrain == _terrain || chunk.ownedObjects.Contains(terrain.gameObject))
            return;
        // note: Terrain colliders are registered ahead of scatter so the player cannot outrun ground provisioning at a chunk boundary.
        RegisterOwnedObject(chunk, terrain.gameObject);
        chunk.generatedObjects.Add(terrain.gameObject);
        InvalidateRendererValidationCache(chunk);
        chunk.visualStateKnown = false;
    }

    private static bool Intersects(float x, float z, float radiusX, float radiusZ, float centerX, float centerZ, float half)
    {
        return Mathf.Abs(x - centerX) <= half + Mathf.Max(1f, radiusX) &&
               Mathf.Abs(z - centerZ) <= half + Mathf.Max(1f, radiusZ);
    }

    private static void AddBorderContracts(GeneratedSemanticChunkRecord record, string featureId, float x, float z, float radiusX, float radiusZ, float centerX, float centerZ, float half)
    {
        if (string.IsNullOrWhiteSpace(featureId))
            return;
        if (x + radiusX >= centerX + half)
            AddUnique(record.borderContracts, featureId + ":east");
        if (x - radiusX <= centerX - half)
            AddUnique(record.borderContracts, featureId + ":west");
        if (z + radiusZ >= centerZ + half)
            AddUnique(record.borderContracts, featureId + ":north");
        if (z - radiusZ <= centerZ - half)
            AddUnique(record.borderContracts, featureId + ":south");
    }

    private static bool ControlPointsIntersect(List<YQBlueprintPointV2> points, float centerX, float centerZ, float half)
    {
        if (points == null)
            return false;
        for (int i = 0; i < points.Count; i++)
        {
            YQBlueprintPointV2 point = points[i];
            if (point != null && Mathf.Abs(point.x - centerX) <= half && Mathf.Abs(point.z - centerZ) <= half)
                return true;
        }
        return false;
    }

    private static bool PolylineIntersectsCell(
        List<YQBlueprintPointV2> points,
        float centerX,
        float centerZ,
        float half,
        float padding)
    {
        // note: This helper gives hydrology and route polylines the same padded cell ownership test used by the runtime frontier.
        if (points == null || points.Count == 0)
            return false;
        float extent = half + Mathf.Max(0f, padding);
        for (int index = 0; index < points.Count; index++)
        {
            YQBlueprintPointV2 point = points[index];
            if (point == null)
                continue;
            if (Mathf.Abs(point.x - centerX) <= extent && Mathf.Abs(point.z - centerZ) <= extent)
                return true;
            if (index + 1 >= points.Count || points[index + 1] == null)
                continue;
            // note: A clipped segment owns a cell even when both authored control points lie outside its bounds.
            if (SegmentIntersectsSquare(
                    point.x, point.z,
                    points[index + 1].x, points[index + 1].z,
                    centerX, centerZ, extent))
                return true;
        }
        return false;
    }

    private static bool SegmentIntersectsSquare(
        float x0,
        float z0,
        float x1,
        float z1,
        float centerX,
        float centerZ,
        float extent)
    {
        float minX = centerX - extent;
        float maxX = centerX + extent;
        float minZ = centerZ - extent;
        float maxZ = centerZ + extent;
        float dx = x1 - x0;
        float dz = z1 - z0;
        float tMin = 0f;
        float tMax = 1f;
        if (!ClipSegmentAxis(x0, dx, minX, maxX, ref tMin, ref tMax) ||
            !ClipSegmentAxis(z0, dz, minZ, maxZ, ref tMin, ref tMax))
            return false;
        return tMin <= tMax;
    }

    private static bool ClipSegmentAxis(
        float origin,
        float direction,
        float minimum,
        float maximum,
        ref float tMin,
        ref float tMax)
    {
        if (Mathf.Abs(direction) < 0.000001f)
            return origin >= minimum && origin <= maximum;
        float inverse = 1f / direction;
        float first = (minimum - origin) * inverse;
        float second = (maximum - origin) * inverse;
        if (first > second)
        {
            float swap = first;
            first = second;
            second = swap;
        }
        tMin = Mathf.Max(tMin, first);
        tMax = Mathf.Min(tMax, second);
        return tMin <= tMax;
    }

    private static void AddPolylineBorderContracts(
        GeneratedSemanticChunkRecord record,
        string featureId,
        List<YQBlueprintPointV2> points,
        float centerX,
        float centerZ,
        float half,
        float padding)
    {
        if (record == null || string.IsNullOrWhiteSpace(featureId) || points == null || points.Count == 0)
            return;
        float extent = half + Mathf.Max(0f, padding);
        float borderPadding = Mathf.Max(0.5f, padding);
        for (int index = 0; index + 1 < points.Count; index++)
        {
            YQBlueprintPointV2 first = points[index];
            YQBlueprintPointV2 second = points[index + 1];
            if (first == null || second == null)
                continue;
            // note: Border contracts use the padded footprint, preserving a continuation obligation for wide rivers and roads.
            if (SegmentTouchesVerticalBorder(first.x, first.z, second.x, second.z, centerX - half, centerZ, extent, borderPadding))
                AddUnique(record.borderContracts, featureId + ":west");
            if (SegmentTouchesVerticalBorder(first.x, first.z, second.x, second.z, centerX + half, centerZ, extent, borderPadding))
                AddUnique(record.borderContracts, featureId + ":east");
            if (SegmentTouchesHorizontalBorder(first.x, first.z, second.x, second.z, centerX, centerZ - half, extent, borderPadding))
                AddUnique(record.borderContracts, featureId + ":south");
            if (SegmentTouchesHorizontalBorder(first.x, first.z, second.x, second.z, centerX, centerZ + half, extent, borderPadding))
                AddUnique(record.borderContracts, featureId + ":north");
        }
    }

    private static bool SegmentTouchesVerticalBorder(
        float x0,
        float z0,
        float x1,
        float z1,
        float borderX,
        float centerZ,
        float extent,
        float padding)
    {
        float dx = x1 - x0;
        if (Mathf.Abs(dx) < 0.000001f)
            return Mathf.Abs(x0 - borderX) <= padding && Mathf.Abs(z0 - centerZ) <= extent;
        float t = (borderX - x0) / dx;
        if (t < 0f || t > 1f)
            return false;
        float z = Mathf.LerpUnclamped(z0, z1, t);
        return Mathf.Abs(z - centerZ) <= extent;
    }

    private static bool SegmentTouchesHorizontalBorder(
        float x0,
        float z0,
        float x1,
        float z1,
        float centerX,
        float borderZ,
        float extent,
        float padding)
    {
        float dz = z1 - z0;
        if (Mathf.Abs(dz) < 0.000001f)
            return Mathf.Abs(z0 - borderZ) <= padding && Mathf.Abs(x0 - centerX) <= extent;
        float t = (borderZ - z0) / dz;
        if (t < 0f || t > 1f)
            return false;
        float x = Mathf.LerpUnclamped(x0, x1, t);
        return Mathf.Abs(x - centerX) <= extent;
    }

    private static float PointDistanceSquaredToCell(
        float x,
        float z,
        float centerX,
        float centerZ,
        float half)
    {
        // note: Squared distance to the cell rectangle keeps site footprints spanning a border in the shared semantic graph.
        float dx = Mathf.Max(Mathf.Abs(x - centerX) - half, 0f);
        float dz = Mathf.Max(Mathf.Abs(z - centerZ) - half, 0f);
        return dx * dx + dz * dz;
    }

    private static void AddBorderContractsFromPoints(GeneratedSemanticChunkRecord record, string featureId, List<YQBlueprintPointV2> points, float centerX, float centerZ, float half)
    {
        if (points == null)
            return;
        for (int i = 0; i < points.Count; i++)
        {
            YQBlueprintPointV2 point = points[i];
            if (point == null)
                continue;
            if (point.x >= centerX + half) AddUnique(record.borderContracts, featureId + ":east");
            if (point.x <= centerX - half) AddUnique(record.borderContracts, featureId + ":west");
            if (point.z >= centerZ + half) AddUnique(record.borderContracts, featureId + ":north");
            if (point.z <= centerZ - half) AddUnique(record.borderContracts, featureId + ":south");
        }
    }

    private static void AddUnique(List<string> values, string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !values.Contains(value))
            values.Add(value);
    }

    private static void RemoveExactValues(List<string> values, List<string> removals)
    {
        if (values == null || removals == null || removals.Count == 0)
            return;
        for (int index = values.Count - 1; index >= 0; index--)
            if (removals.Contains(values[index]))
                values.RemoveAt(index);
    }

    private static void SetObjectsActive(RuntimeChunk chunk, bool active)
    {
        for (int i = 0; i < chunk.ownedObjects.Count; i++)
        {
            GameObject item = chunk.ownedObjects[i];
            if (item != null && item.activeSelf != active)
                item.SetActive(active);
        }
    }

    private static YQSemanticChunkLifecycle ParseState(string value)
    {
        return Enum.TryParse(value, out YQSemanticChunkLifecycle state)
            ? state
            : YQSemanticChunkLifecycle.Unseen;
    }

    private static uint StableHash(string value)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 16777619u;
            }
            return hash;
        }
    }
}
// note: External edits are intentionally picked up by the project auto-refresh watcher.
// note: Automatic refresh verification touch for bounded descendant ownership.
