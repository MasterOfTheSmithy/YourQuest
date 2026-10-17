// note: Admission and integration methods are extracted verbatim. Unity object lifetime, collision results, and time are controlled fixtures, not a native physics test.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        public bool destroyed, enabled = true;
        public bool isActiveAndEnabled => !destroyed && enabled;
        public static implicit operator bool(Object value) => !ReferenceEquals(value, null) && !value.destroyed;
        public static bool operator !(Object value) => !((bool)value);
        public static bool operator ==(Object a, Object b) => (!(bool)a && !(bool)b) || ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => ReferenceEquals(this, other);
        public override int GetHashCode() => base.GetHashCode();
    }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => default;
        public static Vector3 up => new Vector3(0, 1, 0);
        public float sqrMagnitude => x * x + y * y + z * z;
        public float magnitude => (float)Math.Sqrt(sqrMagnitude);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator /(Vector3 a, float b) => new Vector3(a.x / b, a.y / b, a.z / b);
        public static Vector3 ProjectOnPlane(Vector3 a, Vector3 plane) => new Vector3(a.x, 0, a.z);
        public string ToString(string format) => $"({x},{y},{z})";
    }
    public struct Vector2Int
    {
        public int x, y;
        public Vector2Int(int x, int y) { this.x = x; this.y = y; }
        public static bool operator ==(Vector2Int a, Vector2Int b) => a.x == b.x && a.y == b.y;
        public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);
        public override bool Equals(object other) => other is Vector2Int v && this == v;
        public override int GetHashCode() => HashCode.Combine(x, y);
    }
    public sealed class Transform { public Vector3 position; }
    public sealed class GameObject : Object { public bool activeSelf = true; public void SetActive(bool active) => activeSelf = active; }
    public sealed class TerrainData : Object { }
    public sealed class TerrainCollider : Object { public TerrainData terrainData; }
    public sealed class Terrain : Object
    {
        public TerrainData terrainData;
        public TerrainCollider collider;
        public bool drawTreesAndFoliage;
        public T GetComponent<T>() where T : class => collider as T;
    }
    public sealed class Coroutine { public IEnumerator routine; public bool completed; }
    [Flags] public enum CollisionFlags { None = 0, Sides = 1, Above = 2, Below = 4 }
    public sealed class CharacterController
    {
        public bool enabled = true, isGrounded;
        public float radius = .3f, skinWidth = .08f, stepOffset = .52f;
        public Transform transform;
        public CollisionFlags result;
        public int moves;
        public CollisionFlags Move(Vector3 delta) { moves++; transform.position += delta; return result; }
    }
    public static class Time { public static int frameCount; public static float realtimeSinceStartup; }
    public static class Physics
    {
        public static int syncs;
        public static Action inspectSync;
        public static bool throwNextSync;
        public static float syncDuration;
        public static readonly Dictionary<int, int> callsPerFrame = new Dictionary<int, int>();
        public static void SyncTransforms()
        {
            syncs++;
            callsPerFrame.TryGetValue(Time.frameCount, out int count); callsPerFrame[Time.frameCount] = count + 1;
            inspectSync?.Invoke(); Time.realtimeSinceStartup += syncDuration;
            if (throwNextSync) { throwNextSync = false; throw new InvalidOperationException("controlled native synchronization failure"); }
        }
    }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static int Max(int a, int b) => Math.Max(a, b);
    }
    public static class Debug { public static void LogError(string value) { } public static void LogWarning(string value) { } }
}
public enum YQTerrainReadinessState { TerrainCreated, CollisionReady }
public enum YQPublicationVerificationStage { TerrainCollision }
public static class YQContinuousWorldFeatureMaterializer
{
    public sealed class PreparedBiomeAlphamap : IDisposable { public void Dispose() { } }
}
public sealed class GeneratedWorldPlanRecord { }
public sealed class WorldState { public GeneratedWorldPlanRecord generatedWorldPlan; }
public sealed class WorldStateManager : UnityEngine.Object { public static WorldStateManager Instance; public WorldState State; }
public sealed class YQInvestorCombat { }

public sealed partial class YQInvestorPlayerMotor : UnityEngine.Object
{
    private YQPlayerFollowingSemanticChunkStreamer _streamedMovementOwner;
    private WorldState _streamedMovementWorld;
    private bool _frameMovementHeld, _frameControllerMoveAdmitted, _frameIntegrationCaptured, _fallVelocityPaused;
    private bool _isSwimming, _isClimbing, _fallTracked, _frameLanded;
    private Vector3 _framePlanarBeforeIntegration, _planarVelocity, _frameStart, _motionSamplePosition, _sampleVelocity, _supportVelocity;
    private float _frameVerticalBeforeIntegration, _frameDashBeforeIntegration, _verticalVelocity, _dashTimeRemaining;
    private float _pausedFallVelocity, _frameImpactSpeed, _fallApex, _sampleElapsed;
    private YQInvestorCombat _movementCombat;
    private CharacterController _controller;
    private Vector3 _lastRequestedMoveDisplacement, _lastActualMoveDisplacement;
    private CollisionFlags _lastMoveCollisionFlags;
    private bool _lastMoveRejectedByTraversalGate;
    private int _animationGroundContactFrame;
    public readonly Transform transform = new Transform();
    public Vector3 ResolvedVelocity;
    public float accelerationSampleSeconds = .12f, extremeVelocityChange = 40f, extremeAcceleration = 250f;
    public bool IsDashing => _dashTimeRemaining > 0f;
    public bool HasPendingFallDamage => false;
    public bool IsRagdoll => false;
    public int ragdolls, landings;
    public float landingHeight, landingSpeed;
    private T GetComponent<T>() where T : class => null;
    private void QueueLanding(float height, float speed) { landings++; landingHeight = height; landingSpeed = speed; }
    private void EnterPhysicalRagdoll(Vector3 velocity) { ragdolls++; }
    public YQInvestorPlayerMotor() { _controller = new CharacterController { transform = transform }; }
    public void Seed(Vector3 planar, float vertical, float dash, float apex = 25f)
    {
        _planarVelocity = planar; _verticalVelocity = vertical; _dashTimeRemaining = dash;
        _sampleVelocity = planar + new Vector3(0, vertical, 0); _sampleElapsed = .08f;
        _fallTracked = true; _fallApex = apex; _motionSamplePosition = transform.position;
    }
    public void Begin() => BeginMotionFrame(.1f);
    public void Capture() => CaptureMotionIntegrationState();
    public void Integrate(Vector3 planar, float vertical, float dash) { _planarVelocity = planar; _verticalVelocity = vertical; _dashTimeRemaining = dash; }
    public void End() => EndMotionFrame(.1f);
    public CollisionFlags Move(Vector3 value) => MoveWithTraversalGate(value);
    public void NativeLanding() { _controller.result = CollisionFlags.Below; }
    public void SetVertical(float value) => _verticalVelocity = value;
    public Vector3 Planar => _planarVelocity;
    public float Vertical => _verticalVelocity;
    public float Dash => _dashTimeRemaining;
    public bool FallTracked => _fallTracked;
    public float Apex => _fallApex;
    public float SampleElapsed => _sampleElapsed;
    public Vector3 SampleVelocity => _sampleVelocity;
    public int Moves => _controller.moves;
}
public sealed partial class YQPlayerFollowingSemanticChunkStreamer : UnityEngine.Object
{
    public static YQPlayerFollowingSemanticChunkStreamer Active { get; private set; }
    private bool _configured, _movementAdmissionUnavailable, _groundSafetyPhysicsSyncPending;
    private WorldStateManager _movementAdmissionWorldOwner;
    private WorldState _world;
    private GeneratedWorldPlanRecord _plan;
    private YQInvestorPlayerMotor _playerMotor;
    private const float AggregateMainThreadBudgetSeconds = .010f, RequiredHardViewEcologyBudgetReserveSeconds = .004f;
    private int _aggregateBudgetFrame = -1, _aggregateBudgetDeferredSlices, _requiredHardViewEcologyBudgetFrame = -1;
    private int _reservedHardViewContentProgressFrame, _hardViewContentProgressReservationCount, _reservedHardViewContentDispatchFrame;
    private int _lifecycleBudgetFrame, _reservedHardViewAppearanceFrame, _reservedTurnBufferGroundProgressFrame, _reservedTerrainUploadFrame;
    private int _reservedTerrainColliderSyncFrame, _hardViewContentDispatchReservationCount;
    private int _lastTerrainPhysicsSyncFrame = int.MinValue;
    private Vector2Int _reservedHardViewContentProgressOwner, _lastHardViewContentProgressOwner, _terrainPreparingCoordinate;
    private float _aggregateFrameWorkSeconds, _hardViewContentProgressReservedSeconds, _lifecycleDeadline;
    private float _maximumAggregateFrameWorkSeconds;
    private string _lastAggregateWorkStage, _maximumAggregateFrameWorkStage;
    private int _maximumAggregateFrameWorkFrame;
    private bool _nextContentSliceUsesHardViewReserve;
    private object _terrainPreparation = new object();
    public bool constraintReady = true;
    private bool MustFinishOccupiedContentFirst() => false;
    private bool HasHigherPriorityAppearanceReserveOwner(Vector2Int? value) => false;
    private bool HasActiveLiveViewContentWorker() => false;
    private bool IsNextLiveViewContentProgressOwner(Vector2Int value) => true;
    private bool IsNextHardPreparationContentProgressOwner(Vector2Int value) => true;
    private bool HasReadyHardViewContentCandidate() => true;
    private bool TryGetReadyHardPreparationContentCandidate(out Vector2Int value) { value = default; return true; }
    public int terrainPriority;
    public readonly Dictionary<Vector2Int, int> terrainPriorities = new Dictionary<Vector2Int, int>();
    private int TerrainPreparationPriority(Vector2Int value, Vector3 velocity) => terrainPriorities.TryGetValue(value, out int priority) ? priority : terrainPriority;
    private Vector3 ResolveTraversalVelocity() => _playerMotor.Planar;
    private void RecordHardViewProgressReservation(Vector2Int? value, bool reserved, bool yieldedToLive) { }
    public bool TryValidateCurrentTraversability(Vector3 start, float radius, float skin, out string failure)
    { failure = "fixture"; return IsMovementAdmissionAvailable && !_groundSafetyPhysicsSyncPending; }
    public bool TryConstrainMovement(Vector3 start, Vector3 delta, float radius, float skin, out Vector3 permitted)
    { permitted = constraintReady ? delta : Vector3.zero; return constraintReady && IsMovementAdmissionAvailable; }
    public void Bind(YQInvestorPlayerMotor motor, WorldStateManager manager)
    {
        _configured = true; _movementAdmissionUnavailable = false; enabled = true; Active = this;
        _movementAdmissionWorldOwner = manager; _world = manager.State; _plan = _world.generatedWorldPlan;
        _playerMotor = motor; motor.BindStreamedMovementAdmission(this, _world);
    }
    public void Disable() { enabled = false; _movementAdmissionUnavailable = true; }
    public void ClearActive() { Active = null; }
    public void Budget(float spent)
    {
        Time.frameCount++; BeginAggregateBudgetFrame(); _aggregateFrameWorkSeconds = spent;
        _lifecycleBudgetFrame = Time.frameCount; _lifecycleDeadline = Time.realtimeSinceStartup + 10;
    }
    public bool BudgetAllows(string stage) => CanAdvanceAggregateWork(stage, true, true, true, true, new Vector2Int(0, 0), new Vector2Int(0, 0));
    public void PendingSync() => _groundSafetyPhysicsSyncPending = true;
    public bool Sync() => TryCompleteGroundSafetySynchronization(out _);
    public bool SyncPending => _groundSafetyPhysicsSyncPending;
    public void ReserveEcology() => _requiredHardViewEcologyBudgetFrame = Time.frameCount;

    // note: Real registration, arbitration, publication iterators and current-ticket checks execute above these controlled native/resource boundaries.
    public sealed class RuntimeChunk
    {
        public readonly List<GameObject> ownedObjects = new List<GameObject>(), generatedObjects = new List<GameObject>();
        public bool terrainPublished, visualStateKnown, visualState, appearanceReady, visualReady;
        public int publicationVersion;
    }
    public struct StreamingWorkToken { public RuntimeChunk owner; public long workId; }
    public sealed class PendingTerrainPublication
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
        public int registeredFrame = int.MinValue, synchronizedFrame = int.MinValue;
        public string synchronizationFailure;
        public bool appearancePrepared, published, cancelled, resourcesDestroyed;
        public int finished, publishedFrame = -1;
        public string finishFailure;
    }
    private const int MaximumPendingTerrainColliderPublications = 2;
    private readonly Dictionary<Vector2Int, PendingTerrainPublication> _pendingTerrainPublications = new Dictionary<Vector2Int, PendingTerrainPublication>();
    private readonly Dictionary<Vector2Int, RuntimeChunk> _chunks = new Dictionary<Vector2Int, RuntimeChunk>();
    private readonly Dictionary<Vector2Int, Terrain> _extendedTerrainTiles = new Dictionary<Vector2Int, Terrain>();
    private readonly Dictionary<Vector2Int, float> _terrainAppearanceRetryAt = new Dictionary<Vector2Int, float>();
    private readonly Dictionary<Vector2Int, int> _terrainAppearanceRetryCount = new Dictionary<Vector2Int, int>();
    private readonly Dictionary<Vector2Int, object> _terrainPainting = new Dictionary<Vector2Int, object>();
    private int _maximumPendingTerrainColliderPublications;
    private long _terrainProgressVersion, nextWorkId;
    private float _lastTerrainBuildSeconds, _maximumTerrainBuildSeconds, chunkWorldSize = 128;
    private bool _lifecyclePending;
    public bool holdCollision;
    public float Spent => _aggregateFrameWorkSeconds;
    private RuntimeChunk GetRuntimeChunk(Vector2Int coordinate) => _chunks.TryGetValue(coordinate, out var chunk) ? chunk : null;
    private bool IsGenerationCurrent(Vector2Int coordinate, RuntimeChunk chunk, StreamingWorkToken token) =>
        chunk != null && ReferenceEquals(chunk, token.owner) && ReferenceEquals(GetRuntimeChunk(coordinate), token.owner);
    private bool IsTerrainRequestStillRelevant(Vector2Int coordinate) => _chunks.ContainsKey(coordinate);
    private void RecordFixedTerrainObservation(string phase, Vector2Int coordinate, long id) { }
    private Coroutine StartCoroutine(IEnumerator routine)
    {
        var coroutine = new Coroutine { routine = routine };
        coroutine.completed = !routine.MoveNext();
        return coroutine;
    }
    private void StartTerrainAppearancePainting(Vector2Int coordinate, TerrainData data, float size, Terrain terrain,
        StreamingWorkToken token, YQContinuousWorldFeatureMaterializer.PreparedBiomeAlphamap preparation) { }
    private void DestroyUnpublishedTerrainPublication(PendingTerrainPublication publication)
    { publication.resourcesDestroyed = true; publication.terrainObject.SetActive(false); publication.terrain.collider.enabled = false; }
    private void FinishPendingTerrainPublication(PendingTerrainPublication publication, string failure)
    {
        publication.finished++; publication.finishFailure = failure;
        if (_pendingTerrainPublications.TryGetValue(publication.coordinate, out var current) && ReferenceEquals(current, publication))
            _pendingTerrainPublications.Remove(publication.coordinate);
        if (!publication.published) DestroyUnpublishedTerrainPublication(publication);
    }
    private bool IsPublicationVerificationStageHeld(Vector2Int coordinate, YQPublicationVerificationStage stage) => holdCollision;
    private bool IsTerrainColliderReady(Terrain terrain) => terrain != null && terrain.collider != null && terrain.collider.enabled;
    private void MarkTerrainReadiness(Vector2Int coordinate, YQTerrainReadinessState readiness) { }
    private void RegisterOwnedObject(RuntimeChunk owner, GameObject instance) => owner.ownedObjects.Add(instance);
    private void InvalidateRendererValidationCache(RuntimeChunk owner) { }
    private void RefreshTerrainNeighbors(Vector2Int coordinate) { }
    private void RecordTerrainReady(Vector2Int coordinate) { }
    private void RecordTerrainReadiness(Vector2Int coordinate)
    {
        if (_pendingTerrainPublications.TryGetValue(coordinate, out var publication)) publication.publishedFrame = Time.frameCount;
    }
    private void SetChunkVisualsEnabled(RuntimeChunk owner, bool enabled) { }
    private bool ShouldExposeTerrainPreview(RuntimeChunk owner) => true;
    public PendingTerrainPublication Ticket(int coordinate)
    {
        var key = new Vector2Int(coordinate, 0);
        var owner = new RuntimeChunk(); _chunks[key] = owner;
        var data = new TerrainData();
        var publication = new PendingTerrainPublication {
            coordinate = key, token = new StreamingWorkToken { owner = owner, workId = ++nextWorkId }, data = data,
            terrain = new Terrain { terrainData = data, collider = new TerrainCollider { terrainData = data, enabled = false } },
            terrainObject = new GameObject(), appearancePrepared = true, phase = "colliderSync"
        };
        if (!TryStartPendingTerrainPublication(publication)) throw new InvalidOperationException("Fixture ticket registration failed");
        return publication;
    }
    public void Resume(PendingTerrainPublication publication)
    {
        if (!publication.coroutine.completed) publication.coroutine.completed = !publication.coroutine.routine.MoveNext();
    }
    public void Retire(PendingTerrainPublication publication)
    {
        publication.cancelled = true;
        _pendingTerrainPublications.Remove(publication.coordinate);
        (publication.coroutine.routine as IDisposable)?.Dispose();
        publication.coroutine.completed = true;
    }
    public void ReplaceOwner(PendingTerrainPublication publication) => _chunks[publication.coordinate] = new RuntimeChunk();
}

public static class MovementFixtures
{
    public static int Run(string output)
    {
        var results = new List<object>(); int failures = 0;
        void Check(string name, bool passed) { results.Add(new { name, passed }); if (!passed) { failures++; Console.WriteLine("FAIL: " + name); } }
        bool Equal(Vector3 a, Vector3 b) => (a - b).sqrMagnitude < .00001f;
        var motor = new YQInvestorPlayerMotor();
        Check("fresh authored scene has no streamed dependency", motor.TryResolveStreamedMovementAdmission(out var resolved) && resolved == null);
        var world = new WorldState { generatedWorldPlan = new GeneratedWorldPlanRecord() };
        var manager = new WorldStateManager { State = world }; WorldStateManager.Instance = manager;
        var streamer = new YQPlayerFollowingSemanticChunkStreamer(); streamer.Bind(motor, manager);
        Check("bound current owner admits dependency", motor.TryResolveStreamedMovementAdmission(out resolved) && resolved == streamer);
        WorldStateManager.Instance = null;
        Check("missing manager closes admission", !motor.TryResolveStreamedMovementAdmission(out _));
        WorldStateManager.Instance = new WorldStateManager { State = world };
        Check("replacement manager cannot reuse a document proof", !motor.TryResolveStreamedMovementAdmission(out _));
        WorldStateManager.Instance = manager;
        var plan = world.generatedWorldPlan; world.generatedWorldPlan = new GeneratedWorldPlanRecord();
        Check("in-place world plan replacement closes admission", !motor.TryResolveStreamedMovementAdmission(out _)); world.generatedWorldPlan = plan;
        manager.State = new WorldState { generatedWorldPlan = new GeneratedWorldPlanRecord() };
        Check("new generated world waits for its bind", !motor.TryResolveStreamedMovementAdmission(out _));
        manager.State = new WorldState();
        Check("new authored world releases generated dependency", motor.TryResolveStreamedMovementAdmission(out resolved) && resolved == null);
        manager.State = world; streamer.Disable();
        Check("disabled bound streamer closes admission", !motor.TryResolveStreamedMovementAdmission(out _)); streamer.enabled = true;
        Check("reenable without bind remains closed", !motor.TryResolveStreamedMovementAdmission(out _));
        motor.Begin(); motor.Capture(); motor.Move(new Vector3(20, -1, 0)); motor.End();
        Check("closed dependency never calls native controller", motor.Moves == 0);
        streamer.Bind(motor, manager); streamer.constraintReady = false;
        motor.Seed(new Vector3(200, 0, 0), -10, .5f);
        bool stable = true;
        for (int i = 0; i < 30; i++)
        {
            motor.Begin(); motor.Capture(); motor.Integrate(new Vector3(250, 0, 0), -14.35f, .4f);
            motor.Move(new Vector3(25, -1.435f, 0)); motor.End();
            stable &= Equal(motor.Planar, new Vector3(200, 0, 0)) && motor.Vertical == -10 && motor.Dash == .5f;
        }
        Check("30 fully denied steps accumulate no acceleration gravity or dash time", stable && motor.Moves == 0);
        Check("holds preserve actual fall height and impact history", motor.FallTracked && motor.Apex == 25 && motor.SampleElapsed == .08f && Equal(motor.SampleVelocity, new Vector3(200, -10, 0)));
        Check("readiness stop does not cause physical ragdoll", motor.ragdolls == 0);
        Check("resolved displacement still truthfully reports zero during holds", Equal(motor.ResolvedVelocity, Vector3.zero));
        motor.Begin(); motor.SetVertical(12); motor.Capture(); motor.Integrate(motor.Planar, 9, .4f);
        motor.Move(new Vector3(20, .9f, 0)); motor.End();
        Check("accepted launch survives a held time integration", motor.Vertical == 12);
        motor.Seed(new Vector3(200, 0, 0), -10, 0); motor.Begin(); motor.Capture(); motor.NativeLanding();
        streamer.constraintReady = true; motor.Move(new Vector3(1, 0, 0));
        streamer.constraintReady = false; motor.Move(new Vector3(0, .5f, 0)); motor.End();
        Check("denied secondary assist preserves earlier actual landing", motor.landings == 1 && motor.landingHeight == 25 && motor.landingSpeed == 10 && !motor.FallTracked);
        Check("real collision still updates momentum and acceleration accounting", Equal(motor.Planar, new Vector3(10, 0, 0)) && motor.ragdolls == 1);
        foreach (string stage in new[] { "lifecycleActivation", "lifecycleObjectActivation", "hardViewTerrainAppearance", "turnBufferGroundProgress", "terrainHeightUpload", "terrainColliderSync", "contentDispatch", "requiredContent", "requiredHardViewEcology", "emergencyGroundPublication", "provisionalGroundReactivation" })
        { streamer.Budget(.01f); Check("exhausted aggregate denies " + stage, !streamer.BudgetAllows(stage)); }
        streamer.PendingSync(); streamer.Budget(.01f); int before = Physics.syncs;
        Check("native synchronization remains pending after exhausted allowance", !streamer.Sync() && streamer.SyncPending && Physics.syncs == before);
        streamer.Budget(0);
        Check("next frame completes the required native synchronization", streamer.Sync() && !streamer.SyncPending && Physics.syncs == before + 1);
        Check("unchanged ground reuses completed synchronization", streamer.Sync() && Physics.syncs == before + 1);
        streamer.PendingSync();
        Check("later same-frame ground dirtiness cannot reuse an earlier sync", !streamer.Sync() && streamer.SyncPending && Physics.syncs == before + 1);
        streamer.Budget(0);
        Check("later dirtiness completes on its own shared sync frame", streamer.Sync() && !streamer.SyncPending && Physics.syncs == before + 2);
        streamer.destroyed = true; streamer.ClearActive();
        Check("destroyed required owner does not become authored-scene permission", !motor.TryResolveStreamedMovementAdmission(out _));

        YQPlayerFollowingSemanticChunkStreamer Scenario()
        {
            var fixture = new YQPlayerFollowingSemanticChunkStreamer();
            var fixtureOwner = new WorldStateManager { State = new WorldState { generatedWorldPlan = new GeneratedWorldPlanRecord() } };
            WorldStateManager.Instance = fixtureOwner;
            fixture.Bind(new YQInvestorPlayerMotor(), fixtureOwner); fixture.Budget(0);
            return fixture;
        }

        // note: Replay Update's sync opportunity before resumed canonical iterators, then create more provisional dirtiness every frame.
        var continuous = Scenario(); var ticket = continuous.Ticket(1); int registeredAt = Time.frameCount;
        bool enabledBeforeNativeSync = false, sameFrameRejected = true, groundCompleted = true, firstCookFrameHeld = false;
        var syncWitness = new List<object>();
        for (int frame = 1; frame <= 12; frame++)
        {
            continuous.Budget(0); continuous.PendingSync();
            Physics.inspectSync = () => enabledBeforeNativeSync |= ticket.terrain.collider.enabled && ticket.terrain.collider.terrainData == ticket.data;
            int nativeBefore = Physics.syncs;
            groundCompleted &= continuous.Sync();
            Physics.inspectSync = null;
            continuous.Resume(ticket);
            if (frame == 1) firstCookFrameHeld = !ticket.published;
            continuous.PendingSync();
            sameFrameRejected &= !continuous.Sync() && continuous.SyncPending && Physics.syncs == nativeBefore + 1;
            syncWitness.Add(new { frame = Time.frameCount, ticket.registeredFrame, ticket.synchronizedFrame,
                ticket.published, ticket.publishedFrame, colliderEnabled = ticket.terrain.collider.enabled,
                nativeCalls = Physics.syncs - nativeBefore, laterGroundPending = continuous.SyncPending });
        }
        Check("shared sync includes eligible canonical collider despite continuous provisional dirtiness", ticket.synchronizedFrame == registeredAt + 1 && enabledBeforeNativeSync);
        Check("canonical readiness waits a full frame after its own successful sync", firstCookFrameHeld && ticket.publishedFrame == ticket.synchronizedFrame + 1);
        Check("continuous provisional work cannot starve canonical publication", ticket.published && ticket.finished == 1 && ticket.publishedFrame <= registeredAt + 2);
        Check("continuous shared synchronization completes existing ground but retains later same-frame dirtiness", groundCompleted && sameFrameRejected);
        File.WriteAllText(Path.Combine(output, "terrain-sync-progress.json"), JsonConvert.SerializeObject(new {
            evidence = "VERBATIM_PUBLICATION_ITERATOR_AND_SHARED_SYNC_WITH_CONTROLLED_FRAME_NATIVE_BOUNDARIES_NOT_UNITY_RUNTIME",
            registeredAt, frames = syncWitness
        }, Formatting.Indented));

        var pair = Scenario(); var first = pair.Ticket(2); var second = pair.Ticket(3);
        registeredAt = Time.frameCount; int calls = Physics.syncs;
        pair.Sync();
        Check("same-frame registration preserves the pre-sync flush boundary", first.registeredFrame == registeredAt && second.registeredFrame == registeredAt &&
            Physics.syncs == calls && !first.terrain.collider.enabled && !second.terrain.collider.enabled);
        pair.Budget(0); pair.PendingSync(); pair.Sync(); pair.Resume(second); pair.Resume(first);
        Check("oldest equally eligible ticket owns the first shared sync regardless of coroutine resume order", first.synchronizedFrame == Time.frameCount && second.synchronizedFrame == int.MinValue);
        pair.Budget(0); pair.PendingSync(); pair.Sync(); pair.Resume(first); pair.Resume(second);
        Check("both pending canonical tickets progress with provisional work every frame", first.published && second.synchronizedFrame == Time.frameCount && !second.published);
        pair.Budget(0); pair.PendingSync(); pair.Sync(); pair.Resume(second);
        Check("second ticket publishes after its own subsequent cook frame", second.published && second.publishedFrame == registeredAt + 3);

        var exhausted = Scenario(); var waiting = exhausted.Ticket(4); exhausted.Budget(.01f); exhausted.PendingSync(); calls = Physics.syncs;
        Check("spent aggregate budget enables no canonical collider and syncs no ground", !exhausted.Sync() && exhausted.SyncPending &&
            waiting.synchronizedFrame == int.MinValue && !waiting.terrain.collider.enabled && Physics.syncs == calls);
        exhausted.Budget(0); Physics.syncDuration = .002f; exhausted.Sync(); Physics.syncDuration = 0;
        Check("next available frame advances canonical and provisional ground with charged native cost", waiting.synchronizedFrame == Time.frameCount && !exhausted.SyncPending && Math.Abs(exhausted.Spent - .002f) < .00001f);

        var beforeUpdate = Scenario(); var nextFrameTicket = beforeUpdate.Ticket(15); beforeUpdate.Budget(.01f);
        Time.frameCount++;
        Check("shared sync resets prior-frame spending before eligibility selection even before Update", beforeUpdate.Sync() && nextFrameTicket.synchronizedFrame == Time.frameCount);

        var readyGround = Scenario(); var unrelated = readyGround.Ticket(16); readyGround.Budget(.01f); calls = Physics.syncs;
        Check("spent canonical-only allowance does not withhold already-ready ground", readyGround.Sync() && Physics.syncs == calls && unrelated.synchronizedFrame == int.MinValue);

        var nonurgent = Scenario(); nonurgent.terrainPriority = 4; var distant = nonurgent.Ticket(5);
        nonurgent.Budget(.007f); nonurgent.ReserveEcology(); calls = Physics.syncs;
        Check("nonurgent canonical-only ticket preserves ecology allowance without blocking ready ground", nonurgent.Sync() &&
            Physics.syncs == calls && !distant.terrain.collider.enabled && distant.synchronizedFrame == int.MinValue);
        nonurgent.PendingSync();
        Check("pending provisional ground retains urgent eligibility while advancing its canonical companion", nonurgent.Sync() &&
            Physics.syncs == calls + 1 && distant.synchronizedFrame == Time.frameCount);

        var priority = Scenario(); var older = priority.Ticket(6); var newer = priority.Ticket(7);
        priority.terrainPriorities[older.coordinate] = 4; priority.terrainPriorities[newer.coordinate] = 0;
        priority.Budget(.007f); priority.ReserveEcology(); calls = Physics.syncs;
        Check("older nonurgent ticket cannot hide newer budget-eligible urgent work", priority.Sync() && Physics.syncs == calls + 1 &&
            older.synchronizedFrame == int.MinValue && newer.synchronizedFrame == Time.frameCount);
        priority.Budget(0); priority.Sync();
        Check("older deferred ticket progresses when ordinary allowance returns", older.synchronizedFrame == Time.frameCount);

        var urgent = Scenario(); var urgentTicket = urgent.Ticket(8); urgent.Budget(.007f); urgent.ReserveEcology(); calls = Physics.syncs;
        Check("urgent canonical-only work retains its existing reserved allowance", urgent.Sync() && Physics.syncs == calls + 1 && urgentTicket.synchronizedFrame == Time.frameCount);

        var cancellation = Scenario(); var cancelled = cancellation.Ticket(9); cancellation.Retire(cancelled);
        cancellation.Budget(0); cancellation.PendingSync(); cancellation.Sync();
        Check("cancelled canonical ticket cannot enable or acquire a later sync receipt", cancelled.resourcesDestroyed && cancelled.finished == 1 &&
            cancelled.synchronizedFrame == int.MinValue && !cancelled.terrain.collider.enabled && !cancelled.published);

        var replacement = Scenario(); var stale = replacement.Ticket(10); replacement.ReplaceOwner(stale);
        replacement.Budget(0); replacement.PendingSync(); replacement.Sync(); replacement.Resume(stale);
        Check("replaced canonical owner cannot consume a native sync or publish", stale.synchronizedFrame == int.MinValue && !stale.published && stale.resourcesDestroyed);

        var cookCancellation = Scenario(); var cooked = cookCancellation.Ticket(11);
        cookCancellation.Budget(0); cookCancellation.PendingSync(); cookCancellation.Sync(); cookCancellation.Retire(cooked);
        cookCancellation.Budget(0); cookCancellation.Resume(cooked);
        Check("cancellation after synchronization retires collider before publication", cooked.synchronizedFrame != int.MinValue &&
            !cooked.published && cooked.resourcesDestroyed && !cooked.terrain.collider.enabled && cooked.finished == 1);

        var mismatch = Scenario(); var wrongData = mismatch.Ticket(12); wrongData.terrain.collider.terrainData = new TerrainData();
        mismatch.Budget(0); mismatch.PendingSync(); calls = Physics.syncs; mismatch.Sync(); mismatch.Resume(wrongData);
        Check("wrong owned heightfield fails before sync and follows existing ticket cleanup", Physics.syncs == calls && mismatch.SyncPending &&
            wrongData.synchronizedFrame == int.MinValue && !wrongData.terrain.collider.enabled && wrongData.resourcesDestroyed && !string.IsNullOrEmpty(wrongData.finishFailure));

        var nativeFailure = Scenario(); var failed = nativeFailure.Ticket(13);
        nativeFailure.Budget(0); nativeFailure.PendingSync(); Physics.throwNextSync = true; calls = Physics.syncs;
        bool failedGround;
        try { failedGround = !nativeFailure.Sync(); }
        catch (InvalidOperationException) { failedGround = true; }
        Check("failed native sync retains dirty ground and no canonical proof", failedGround && nativeFailure.SyncPending &&
            failed.synchronizedFrame == int.MinValue && !failed.terrain.collider.enabled && !string.IsNullOrEmpty(failed.synchronizationFailure));
        nativeFailure.Resume(failed);
        Check("native failure consumes its existing coroutine rejection and cleanup path", failed.finished == 1 && failed.resourcesDestroyed && !string.IsNullOrEmpty(failed.finishFailure));
        Check("failed native call cannot claim a second shared sync in the same frame", !nativeFailure.Sync() && Physics.syncs == calls + 1);
        nativeFailure.Budget(0);
        Check("remaining provisional ground recovers on the next successful sync", nativeFailure.Sync() && !nativeFailure.SyncPending);

        var hold = Scenario(); hold.holdCollision = true; var held = hold.Ticket(14);
        hold.Budget(0); hold.PendingSync(); hold.Sync(); hold.Resume(held);
        hold.Budget(0); hold.Resume(held);
        Check("explicit canonical collision verification hold remains effective after shared sync", !held.published && held.phase == "collisionHold");
        hold.holdCollision = false; hold.Budget(0); hold.Resume(held);
        Check("releasing collision verification hold permits exact ticket publication", held.published && held.finished == 1);

        bool atMostOne = true;
        foreach (int frameCalls in Physics.callsPerFrame.Values) atMostOne &= frameCalls <= 1;
        Check("shared terrain lane performs at most one native sync per frame across all cases", atMostOne);
        File.WriteAllText(Path.Combine(output, "movement-fixtures.json"), JsonConvert.SerializeObject(new {
            evidence = "VERBATIM_MANAGED_MOVEMENT_AND_BUDGET_METHODS_WITH_CONTROLLED_UNITY_BOUNDARIES_NOT_RUNTIME_TRAVEL", checks = results.Count, failures, results
        }, Formatting.Indented));
        Console.WriteLine("Movement managed fixtures: " + results.Count + " checks; failures=" + failures);
        return failures;
    }
}
