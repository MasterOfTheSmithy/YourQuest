using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;

// note: Exercise the compiled streamer's managed identity and disposal rules without constructing native Unity objects or opening profiles.
internal static class StreamingLifetimeChecks
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly Type Streamer = typeof(YQPlayerFollowingSemanticChunkStreamer);
    private static readonly Type Chunk = Streamer.GetNestedType("RuntimeChunk", BindingFlags.NonPublic);
    private static readonly Type Ticket = Streamer.GetNestedType("PendingTerrainPublication", BindingFlags.NonPublic);
    private static readonly Type Token = Streamer.GetNestedType("StreamingWorkToken", BindingFlags.NonPublic);
    private static readonly MethodInfo Match = Streamer.GetMethod("MatchesStructuralTerrainDependency", All);
    private static readonly MethodInfo Clear = Streamer.GetMethod("ClearStructuralTerrainDependency", All);
    private static readonly MethodInfo ShouldCancel = Streamer.GetMethod("ShouldCancelContentForTerrainRetirement", All);
    private static readonly MethodInfo DisposeStructure = Streamer.GetMethod("DisposeStructuralIterators", All);
    private static int failures, checks;

    private static object Blank(Type type) => RuntimeHelpers.GetUninitializedObject(type);
    private static readonly Dictionary<FieldInfo, Action<object, object>> Setters = new Dictionary<FieldInfo, Action<object, object>>();
    private static void Set(object target, string name, object value)
    {
        // note: Direct instance-field stores avoid Reflection.SetValue forcing Unity's unrelated static profiler initialization. No engine call or production method body is replaced.
        FieldInfo field = target.GetType().GetField(name, All);
        if (!Setters.TryGetValue(field, out var setter))
        {
            var method = new DynamicMethod("Set_" + name, typeof(void), new[] { typeof(object), typeof(object) }, typeof(StreamingLifetimeChecks), true);
            ILGenerator il = method.GetILGenerator();
            il.Emit(OpCodes.Ldarg_0); il.Emit(field.DeclaringType.IsValueType ? OpCodes.Unbox : OpCodes.Castclass, field.DeclaringType);
            il.Emit(OpCodes.Ldarg_1); il.Emit(field.FieldType.IsValueType ? OpCodes.Unbox_Any : OpCodes.Castclass, field.FieldType);
            il.Emit(OpCodes.Stfld, field); il.Emit(OpCodes.Ret);
            setter = (Action<object, object>)method.CreateDelegate(typeof(Action<object, object>)); Setters[field] = setter;
        }
        setter(target, value);
    }
    private static object Get(object target, string name) => target.GetType().GetField(name, All).GetValue(target);
    private static void Require(bool value, string failure) { if (!value) throw new InvalidOperationException(failure); }
    private static void Check(string name, Action body)
    {
        checks++;
        try { body(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + " " + error); }
    }

    private sealed class Fixture
    {
        internal readonly object streamer = Blank(Streamer), chunk = Blank(Chunk), ticket = Blank(Ticket);
        internal readonly Terrain terrain = (Terrain)Blank(typeof(Terrain));
        internal readonly TerrainData data = (TerrainData)Blank(typeof(TerrainData));
        internal readonly Vector2Int coordinate = new Vector2Int(-1, -3);
        internal readonly IDictionary chunks;
        internal readonly Dictionary<Vector2Int, long> work = new Dictionary<Vector2Int, long>();
        internal object token;
        internal Fixture()
        {
            // note: These managed shells are identity witnesses only. No native property, overloaded Unity equality or physics method is called.
            chunks = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(Vector2Int), Chunk));
            chunks.Add(coordinate, chunk); work.Add(coordinate, 651);
            Set(streamer, "_chunks", chunks); Set(streamer, "_activeGenerationWorkIds", work);
            Set(chunk, "terrainOwnerEpoch", 4); Set(chunk, "contentOwnerEpoch", 8);
            Set(chunk, "structuralTerrain", terrain); Set(chunk, "structuralTerrainData", data); Set(chunk, "structuralTerrainWorkId", 651L);
            token = Activator.CreateInstance(Token);
            Set(token, "profileId", ""); Set(token, "worldId", ""); Set(token, "owner", chunk);
            Set(token, "terrainWork", true); Set(token, "ownerEpoch", 4); Set(token, "workId", 621L);
            Set(ticket, "coordinate", coordinate); Set(ticket, "token", token);
            Set(ticket, "terrain", terrain); Set(ticket, "data", data);
        }
        internal bool Matches(long id = 651, bool alive = true, Terrain current = null, TerrainData currentData = null) =>
            (bool)Match.Invoke(null, new object[] { chunk, id, ReferenceEquals(current, null) ? terrain : current,
                ReferenceEquals(currentData, null) ? data : currentData, alive });
        internal bool Cancels() => (bool)ShouldCancel.Invoke(streamer, new object[] { ticket, null, 0L });
        internal void ChangeToken(string field, object value) { Set(token, field, value); Set(ticket, "token", token); }
        internal void SetFeatureRevision(long revision)
        {
            // note: Advance only the managed accepted-overlay revision; the same pending native resource is still being retired.
            var plan = (GeneratedWorldPlanRecord)Blank(typeof(GeneratedWorldPlanRecord));
            plan.semanticAuthority = new GeneratedSemanticWorldAuthorityRecord { featureOverlayRevision = revision };
            Set(streamer, "_plan", plan);
        }
        internal object ContentFailure(int attempts = 1)
        {
            // note: Use the real retry owner and matching recorded cause; an unrelated terminal failure must not enter this recovery lane.
            Type retryType = Streamer.GetNestedType("RequiredContentRetryState", BindingFlags.NonPublic);
            object retry = Activator.CreateInstance(retryType, true);
            retryType.GetMethod("SynchronizeContext").Invoke(retry, new object[] { 0, null, 0L });
            for (int i = 0; i < attempts; i++)
                retryType.GetMethod("RecordFailure").Invoke(retry, new object[] { 10f, "missing required bridge" });
            Set(chunk, "requiredContentRetry", retry);
            Set(chunk, "state", YQSemanticChunkLifecycle.Failed);
            Set(chunk, "failureReason", retryType.GetProperty("FailureReason").GetValue(retry));
            return retry;
        }
        internal bool Reconcile(float now) => (bool)Streamer.GetMethod("CanReconcileRequiredContent", All)
            .Invoke(streamer, new object[] { chunk, now });
    }

    private sealed class DisposableIterator : IEnumerator, IDisposable
    {
        private readonly string name; private readonly List<string> order; private readonly bool fail;
        internal DisposableIterator(string name, List<string> order, bool fail = false) { this.name = name; this.order = order; this.fail = fail; }
        public bool MoveNext() => false;
        public object Current => null;
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { order.Add(name); if (fail) throw new InvalidOperationException(name); }
    }

    private static int Main()
    {
        // note: Exercise explicit cancellation without asking Unity to dispose the outer coroutine: this is the shared-layout lease leak boundary.
        Check("EXPLICIT-STOP-RELEASES-NESTED-LEASE", () => {
            var f = new Fixture(); bool held = true;
            IEnumerator lease = HeldLease(() => held = false); lease.MoveNext();
            var stack = new Stack<IEnumerator>(); stack.Push(lease);
            Set(f.chunk, "structuralIterators", stack); Set(f.chunk, "structuralIteratorWorkId", 651L);
            DisposeStructure.Invoke(null, new object[] { f.chunk, 651L, new Action<Exception>(_ => { }) });
            Require(!held && stack.Count == 0, "Stopped worker retained its shared bridge lease.");
            Require(Get(f.chunk, "structuralIterators") == null && (long)Get(f.chunk, "structuralIteratorWorkId") == 0L, "Stopped owner retained a stack.");
        });
        Check("STALE-STOP-PRESERVES-REPLACEMENT-STACK", () => {
            var f = new Fixture(); var stack = new Stack<IEnumerator>(); var order = new List<string>();
            stack.Push(new DisposableIterator("replacement", order));
            Set(f.chunk, "structuralIterators", stack); Set(f.chunk, "structuralIteratorWorkId", 652L);
            DisposeStructure.Invoke(null, new object[] { f.chunk, 651L, new Action<Exception>(_ => { }) });
            Require(order.Count == 0 && stack.Count == 1 && ReferenceEquals(Get(f.chunk, "structuralIterators"), stack), "Stale stop released replacement work.");
        });
        Check("ZERO-STOP-CANNOT-RELEASE-OWNER", () => {
            var f = new Fixture(); var stack = new Stack<IEnumerator>(); stack.Push(HeldLease(() => { }));
            Set(f.chunk, "structuralIterators", stack); Set(f.chunk, "structuralIteratorWorkId", 651L);
            DisposeStructure.Invoke(null, new object[] { f.chunk, 0L, new Action<Exception>(_ => { }) });
            Require(stack.Count == 1 && (long)Get(f.chunk, "structuralIteratorWorkId") == 651L, "Zero stop stole a live owner.");
        });
        Check("THROWING-STOP-DRAINS-AND-DETACHES", () => {
            var f = new Fixture(); var stack = new Stack<IEnumerator>(); var order = new List<string>(); var errors = new List<Exception>();
            stack.Push(new DisposableIterator("parent", order)); stack.Push(new DisposableIterator("broken", order, true));
            Set(f.chunk, "structuralIterators", stack); Set(f.chunk, "structuralIteratorWorkId", 651L);
            string failure = (string)DisposeStructure.Invoke(null, new object[] { f.chunk, 651L, new Action<Exception>(errors.Add) });
            Require(failure != null && errors.Count == 1 && stack.Count == 0 && order.Count == 2, "One broken cleanup stranded its parent lease.");
            DisposeStructure.Invoke(null, new object[] { f.chunk, 651L, new Action<Exception>(errors.Add) });
            Require(order.Count == 2 && Get(f.chunk, "structuralIterators") == null, "Repeated stop disposed twice.");
        });
        Check("REENTRANT-STOP-PRESERVES-NEW-LEASE", () => {
            var f = new Fixture(); var old = new Stack<IEnumerator>(); var replacement = new Stack<IEnumerator>();
            replacement.Push(HeldLease(() => { }));
            IEnumerator lease = HeldLease(() => { Set(f.chunk, "structuralIterators", replacement); Set(f.chunk, "structuralIteratorWorkId", 652L); });
            lease.MoveNext(); old.Push(lease); Set(f.chunk, "structuralIterators", old); Set(f.chunk, "structuralIteratorWorkId", 651L);
            DisposeStructure.Invoke(null, new object[] { f.chunk, 651L, new Action<Exception>(_ => { }) });
            Require(old.Count == 0 && ReferenceEquals(Get(f.chunk, "structuralIterators"), replacement) && (long)Get(f.chunk, "structuralIteratorWorkId") == 652L, "Cleanup detached replacement state.");
        });
        Check("PENDING-RESOURCE-MATCH", () => Require(new Fixture().Matches(), "Current hidden terrain was rejected."));
        Check("PROMOTION-KEEPS-SAME-RESOURCE", () => { var f = new Fixture(); Set(f.ticket, "published", true); Require(f.Matches(), "Promotion invalidated a stable resource."); });
        Check("REPLACED-TERRAIN-REJECTED", () => Require(!new Fixture().Matches(current: (Terrain)Blank(typeof(Terrain))), "Replacement tile reused old structure."));
        Check("REPLACED-DATA-REJECTED", () => Require(!new Fixture().Matches(currentData: (TerrainData)Blank(typeof(TerrainData))), "Replacement heightfield reused old structure."));
        Check("NATIVE-RETIREMENT-REJECTED", () => Require(!new Fixture().Matches(alive: false), "Dead native resource survived through managed identity."));
        Check("STALE-WORK-REJECTED", () => Require(!new Fixture().Matches(id: 650), "Stale work borrowed the current binding."));
        Check("ZERO-WORK-REJECTED", () => Require(!new Fixture().Matches(id: 0), "Unbound work was treated as bound."));
        Check("MISSING-DATA-REJECTED", () => { var f = new Fixture(); Set(f.chunk, "structuralTerrainData", null); Require(!f.Matches(), "Missing bound heightfield was accepted."); });
        Check("RETIRING-TICKET-CANCELS-BORROWER", () => Require(new Fixture().Cancels(), "Failed ticket left dependent content running."));
        Check("EXPLICIT-CANCEL-STILL-RETIRES-BORROWER", () => { var f = new Fixture(); Set(f.ticket, "cancelled", true); Require(f.Cancels(), "Marked cancellation skipped resource cleanup."); });
        Check("PUBLISHED-TICKET-PROTECTED", () => { var f = new Fixture(); Set(f.ticket, "published", true); Require(!f.Cancels(), "Published terrain cancelled current content."); });
        Check("REPLACEMENT-OWNER-PROTECTED", () => { var f = new Fixture(); f.chunks[f.coordinate] = Blank(Chunk); Require(!f.Cancels(), "Old ticket cancelled replacement chunk."); });
        Check("RETIRED-OWNER-EPOCH-PROTECTED", () => { var f = new Fixture(); Set(f.chunk, "terrainOwnerEpoch", 5); Require(!f.Cancels(), "Full teardown requeued retired content."); });
        Check("REPLACEMENT-WORK-PROTECTED", () => { var f = new Fixture(); f.work[f.coordinate] = 652; Require(!f.Cancels(), "Old terrain cancelled replacement work."); });
        Check("FINISHED-WORK-PROTECTED", () => { var f = new Fixture(); f.work.Clear(); Require(!f.Cancels(), "No active work was treated as cancellable."); });
        Check("PREVIOUS-PROFILE-PROTECTED", () => { var f = new Fixture(); f.ChangeToken("profileId", "previous-profile"); Require(!f.Cancels(), "Another profile's ticket cancelled current content."); });
        Check("PREVIOUS-WORLD-PROTECTED", () => { var f = new Fixture(); f.ChangeToken("worldId", "previous-world"); Require(!f.Cancels(), "Another world's ticket cancelled current content."); });
        Check("PREVIOUS-CONFIGURATION-PROTECTED", () => { var f = new Fixture(); f.ChangeToken("generationEpoch", 1); Require(!f.Cancels(), "Old configuration ticket cancelled current content."); });
        Check("STALE-OVERLAY-RETIRES-EXACT-BORROWER", () => {
            var f = new Fixture(); f.SetFeatureRevision(2); f.ChangeToken("featureRevision", 1L);
            Require(f.Cancels(), "Overlay invalidation left the retiring resource's borrower running.");
            Require(!(bool)Streamer.GetMethod("IsGenerationCurrent", All).Invoke(f.streamer, new[] { (object)f.coordinate, f.chunk, f.token }), "A stale overlay token gained publication authority.");
        });
        Check("CONTENT-TOKEN-CANNOT-RETIRE-TERRAIN", () => {
            var f = new Fixture(); f.ChangeToken("terrainWork", false); f.ChangeToken("ownerEpoch", 8);
            Require(!f.Cancels(), "A content ticket was mistaken for terrain retirement ownership.");
        });
        Check("STALE-OVERLAY-CANNOT-CANCEL-REPLACEMENT", () => {
            var f = new Fixture(); f.SetFeatureRevision(2); f.ChangeToken("featureRevision", 1L); f.work[f.coordinate] = 652;
            Require(!f.Cancels(), "Stale overlay retired a replacement content attempt.");
        });
        Check("UNRELATED-TERRAIN-PROTECTED", () => { var f = new Fixture(); Set(f.ticket, "terrain", Blank(typeof(Terrain))); Require(!f.Cancels(), "An unrelated ticket cancelled current structure."); });
        Check("UNRELATED-DATA-PROTECTED", () => { var f = new Fixture(); Set(f.ticket, "data", Blank(typeof(TerrainData))); Require(!f.Cancels(), "An unrelated heightfield cancelled current structure."); });
        Check("STALE-CLEAR-PRESERVES-NEW-BINDING", () => { var f = new Fixture(); Clear.Invoke(null, new object[] { f.chunk, 650L }); Require(f.Matches(), "Old iterator cleared new dependency."); });
        Check("CURRENT-CLEAR-RELEASES-BINDING", () => { var f = new Fixture(); Clear.Invoke(null, new object[] { f.chunk, 651L }); Require((long)Get(f.chunk, "structuralTerrainWorkId") == 0 && ReferenceEquals(Get(f.chunk, "structuralTerrain"), null) && ReferenceEquals(Get(f.chunk, "structuralTerrainData"), null), "Resource references retained."); Require(!f.Cancels(), "Cleared work cancelled twice."); });
        Check("THROWING-DISPOSER-DRAINS-ENTIRE-STACK", () => {
            var order = new List<string>(); var stack = new Stack<IEnumerator>(); var errors = new List<Exception>();
            stack.Push(new DisposableIterator("root", order)); stack.Push(new DisposableIterator("broken", order, true)); stack.Push(new DisposableIterator("leaf", order));
            MethodInfo dispose = Streamer.GetMethod("DisposePublicationIterators", All, null, new[] { typeof(Stack<IEnumerator>), typeof(Action<Exception>) }, null);
            string failure = (string)dispose.Invoke(null, new object[] { stack, (Action<Exception>)errors.Add });
            Require(stack.Count == 0 && string.Join(",", order) == "leaf,broken,root" && errors.Count == 1 && failure.Contains("broken"), "Nested disposal interrupted ownership cleanup.");
        });
        Check("STATIONARY-RETRY-HONORS-DEADLINE", () => {
            var f = new Fixture(); object retry = f.ContentFailure();
            Require(!f.Reconcile(10f) && f.Reconcile(10.25f) && f.Reconcile(11f), "Stationary retry lost its bounded deadline.");
            Require((int)retry.GetType().GetProperty("FailureCount").GetValue(retry) == 1, "Reconciliation reset the same-context budget.");
        });
        Check("STATIONARY-RETRY-KEEPS-TERMINAL-FAILURES", () => {
            var f = new Fixture(); f.ContentFailure(); Set(f.chunk, "failureReason", "terrain preparation retry budget exhausted");
            Require(!f.Reconcile(100f), "Terrain failure acquired the content retry lane.");
            f = new Fixture(); f.ContentFailure(0); Require(!f.Reconcile(100f), "Untracked failure acquired the content retry lane.");
        });
        Check("STATIONARY-RETRY-KEEPS-EXHAUSTION", () => {
            var f = new Fixture(); f.ContentFailure(5); Require(!f.Reconcile(float.MaxValue), "Stationary reconciliation bypassed the five-attempt budget.");
        });
        Check("STATIONARY-CHANGED-CONTEXT-RECOVERS-THROUGH-DISPATCH", () => {
            var f = new Fixture(); f.ContentFailure(5); f.SetFeatureRevision(1);
            Require(f.Reconcile(11f) && f.Reconcile(11f), "Context reset disappeared between candidate selection and dispatch.");
            Require((YQSemanticChunkLifecycle)Get(f.chunk, "state") == YQSemanticChunkLifecycle.SemanticallyPlanned &&
                string.IsNullOrEmpty((string)Get(f.chunk, "failureReason")), "Recovered context kept terminal lifecycle or obsolete failure.");
        });
        Check("GENUINE-CONTENT-FAILURE-KEEPS-BOUNDED-BACKOFF", () => {
            Type retryType = Streamer.GetNestedType("RequiredContentRetryState", BindingFlags.NonPublic);
            object retry = Activator.CreateInstance(retryType, true), projection = new object();
            retryType.GetMethod("SynchronizeContext").Invoke(retry, new object[] { 7, projection, 2L });
            MethodInfo failed = retryType.GetMethod("RecordFailure"), mayStart = retryType.GetMethod("MayStart");
            float now = 10f;
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                failed.Invoke(retry, new object[] { now, "missing required bridge" });
                Require((int)retryType.GetProperty("FailureCount").GetValue(retry) == attempt, "A real failure was omitted.");
                Require(!(bool)mayStart.Invoke(retry, new object[] { now }), "Failed content immediately re-entered.");
                if (attempt < 5)
                {
                    float next = (float)retryType.GetProperty("RetryAt").GetValue(retry);
                    Require(Math.Abs(next - now - .25f * (1 << (attempt - 1))) < .0001f, "Existing retry delay changed.");
                    Require((bool)mayStart.Invoke(retry, new object[] { next }), "Due retry was stranded."); now = next;
                }
            }
            Require((bool)retryType.GetProperty("Exhausted").GetValue(retry) && !(bool)mayStart.Invoke(retry, new object[] { float.MaxValue }), "Missing required content bypassed the attempt limit.");
            Require(!(bool)retryType.GetMethod("SynchronizeContext").Invoke(retry, new object[] { 7, projection, 2L }), "Unchanged dependency reset failure budget.");
            Require((bool)retryType.GetProperty("Exhausted").GetValue(retry), "Repeated same-context work gained fresh retries.");
            Require((bool)retryType.GetMethod("SynchronizeContext").Invoke(retry, new object[] { 7, projection, 3L }) && (bool)mayStart.Invoke(retry, new object[] { now }), "Changed authoritative context could not recover.");
        });
        Console.WriteLine($"RESULT checks={checks} failures={failures}");
        return failures == 0 ? 0 : 1;
    }

    private static IEnumerator HeldLease(Action release)
    {
        // note: Model the actual nested bridge-layout finally; the outer Unity coroutine is deliberately never disposed in these checks.
        try { yield return null; yield return null; }
        finally { release(); }
    }
}
