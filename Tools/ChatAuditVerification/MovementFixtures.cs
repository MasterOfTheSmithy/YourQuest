// note: Admission and integration methods are extracted verbatim. Unity object lifetime, collision results, and time are controlled fixtures, not a native physics test.
using System;
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
    public static class Physics { public static int syncs; public static void SyncTransforms() { syncs++; } }
    public static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
    public static class Debug { public static void LogError(string value) { } }
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
    private int TerrainPreparationPriority(Vector2Int value, Vector3 velocity) => 0;
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
        File.WriteAllText(Path.Combine(output, "movement-fixtures.json"), JsonConvert.SerializeObject(new {
            evidence = "VERBATIM_MANAGED_MOVEMENT_AND_BUDGET_METHODS_WITH_CONTROLLED_UNITY_BOUNDARIES_NOT_RUNTIME_TRAVEL", checks = results.Count, failures, results
        }, Formatting.Indented));
        Console.WriteLine("Movement managed fixtures: " + results.Count + " checks; failures=" + failures);
        return failures;
    }
}
