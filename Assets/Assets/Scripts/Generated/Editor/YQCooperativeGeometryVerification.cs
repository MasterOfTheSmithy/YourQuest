#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

internal static class YQCooperativeGeometryVerification
{
    internal static void StartFrontierWorkerContracts()
    {
        // note: Run the real candidate admission worker on an already accepted body without invoking a publisher or model; this closes coverage when ordinary random candidates reject before admission.
        YQGeneratedWorldRuntimeBuilder.Instance.StartCoroutine(FrontierWorkerContracts());
    }

    private static IEnumerator FrontierWorkerContracts()
    {
        var builder = YQGeneratedWorldRuntimeBuilder.Instance;
        var instanceFlags = BindingFlags.Instance | BindingFlags.NonPublic;
        var construction = typeof(YQGeneratedWorldRuntimeBuilder).GetField("frontierConstructionInFlight", instanceFlags);
        double deadline = UnityEditor.EditorApplication.timeSinceStartup + 40d;
        // note: Wait for the existing construction owner, then hold its own admission flag while this diagnostic exercises one worker at a time.
        while ((bool)construction.GetValue(builder) && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
        if ((bool)construction.GetValue(builder)) throw new InvalidOperationException("Frontier owner did not become idle within the diagnostic bound.");
        construction.SetValue(builder, true);
        try
        {
            var world = WorldStateManager.Instance.State;
            var plan = world.generatedWorldPlan;
            string before = JsonUtility.ToJson(plan.spatialPlanV2);
            var settings = new JsonSerializerSettings { Converters = { new Vector2JsonConverter(), new Vector3JsonConverter(), new QuaternionJsonConverter() } };
            var accepted = JsonConvert.DeserializeObject<GeneratedSpatialContinuationV2Record>(JsonConvert.SerializeObject(plan.spatialPlanV2.acceptedContinuation, settings), settings);
            if (accepted == null) throw new InvalidOperationException("A saved accepted continuation is required.");
            string fingerprint = accepted.schemaVersion + "|" + accepted.revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + accepted.contentHash;
            var type = typeof(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance);
            var constructor = type.GetConstructors(instanceFlags).Single();
            var prepare = type.GetMethod("PrepareRoutine", instanceFlags);
            var prepareOwned = type.GetMethod("PrepareOwnedRoutine", instanceFlags);
            var publishCheck = type.GetMethod("TryPrepareForPublication", instanceFlags);
            object workerOwner = typeof(YQGeneratedWorldRuntimeBuilder).GetField("_frontierWorkerLifetimeOwner", instanceFlags).GetValue(builder);
            bool Current() => ReferenceEquals(WorldStateManager.Instance?.State, world);
            double started = UnityEditor.EditorApplication.timeSinceStartup;
            var checks = new List<string>();
            void Check(string name, bool result) { if (!result) throw new InvalidOperationException(name); checks.Add(name); }
            string compatibilityEnvelope = null;
            foreach (bool owned in new[] { false, true })
            {
                var token = constructor.Invoke(new object[] { world, fingerprint, accepted, null });
                bool success = false; string failure = null;
                var completed = new Action<bool, string>((value, reason) => { success = value; failure = reason; });
                yield return (IEnumerator)(owned ? prepareOwned.Invoke(token, new object[] { world, new Func<bool>(Current), completed, workerOwner })
                    : prepare.Invoke(token, new object[] { world, new Func<bool>(Current), completed }));
                Check((owned ? "captured" : "compatibility") + " candidate admission passes: " + failure, success);
                object[] args = { world, null, null, null };
                Check((owned ? "captured" : "compatibility") + " complete publication admission passes", (bool)publishCheck.Invoke(token, args));
                var projection = args[2];
                Check((owned ? "captured" : "compatibility") + " exact immutable projection reuses", (bool)publishCheck.Invoke(token, args) && ReferenceEquals(projection, args[2]));
                string envelope = (string)type.GetField("preparedCandidateInputsJson", instanceFlags).GetValue(token);
                if (!owned) compatibilityEnvelope = envelope;
                else Check("captured final candidate envelope equals compatibility including nulls/default fields", envelope == compatibilityEnvelope);
            }
            var stale = constructor.Invoke(new object[] { world, "retired-contract-fixture", accepted, null });
            bool staleAdmitted = true;
            yield return (IEnumerator)prepareOwned.Invoke(stale, new object[] { world, new Func<bool>(() => true),
                new Action<bool, string>((value, reason) => staleAdmitted = value), workerOwner });
            Check("captured stale owner rejects", !staleAdmitted);
            // note: Drive a real token to its observed copy task, then invoke the exact stack disposer used by the streamer on this diagnostic-owned local stack.
            var retiring = constructor.Invoke(new object[] { world, fingerprint, accepted, null });
            bool current = true, retiredAdmitted = true; int retiredCallbacks = 0;
            var stack = new Stack<IEnumerator>();
            stack.Push((IEnumerator)prepareOwned.Invoke(retiring, new object[] { world, new Func<bool>(() => current && Current()),
                new Action<bool, string>((value, reason) => { retiredAdmitted = value; retiredCallbacks++; }), workerOwner }));
            var disposer = typeof(YQPlayerFollowingSemanticChunkStreamer).GetMethod("DisposeOwnedContinuationIterators", BindingFlags.Static | BindingFlags.NonPublic);
            bool enteredWorker = false;
            try
            {
                for (int step = 0; step < 24 && stack.Count > 0; step++)
                {
                    var top = stack.Peek();
                    if (!top.MoveNext()) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                    var lifetime = YQGeneratedWorldRuntimeBuilder.CaptureFrontierWorkerLifetime();
                    if (lifetime.pendingWorkerTasks > 0)
                    { Check("captured copy has one active observed worker", lifetime.pendingWorkerTasks == 1); enteredWorker = true; break; }
                    if (top.Current is IEnumerator nested) stack.Push(nested);
                }
                Check("retirement control reached actual captured worker", enteredWorker);
                current = false;
                Check("streamer stack disposer reports no failure", disposer.Invoke(null, new object[] { stack }) == null);
            }
            finally { current = false; disposer.Invoke(null, new object[] { stack }); }
            deadline = UnityEditor.EditorApplication.timeSinceStartup + 10d;
            while (!YQGeneratedWorldRuntimeBuilder.CaptureFrontierWorkerLifetime().IsQuiescent && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Check("cancelled worker and late disposal become quiescent", YQGeneratedWorldRuntimeBuilder.CaptureFrontierWorkerLifetime().IsQuiescent);
            Check("disposed token rejects late completion exactly once", stack.Count == 0 && !retiredAdmitted && retiredCallbacks == 1);
            Check("live accepted artifact unchanged", before == JsonUtility.ToJson(plan.spatialPlanV2));
            File.WriteAllText("outputs/DialogueVramRepair_20261011/frontier-worker-contracts.json", JsonConvert.SerializeObject(new {
                utc = DateTime.UtcNow, passed = true, checks, runtimeMvid = builder.GetType().Module.ModuleVersionId,
                elapsedSeconds = UnityEditor.EditorApplication.timeSinceStartup - started,
                evidence = "ASSISTED_NATIVE_COMPATIBILITY_AND_CAPTURED_ADMISSION_LOCAL_STACK_ACTUAL_STREAMER_DISPOSER_NO_MODEL_OR_PUBLICATION_NOT_ORDINARY_FRONTIER_ACCEPTANCE"
            }, Formatting.Indented));
        }
        finally { construction.SetValue(builder, false); }
    }

    internal static void Run()
    {
        // note: All geometry and mutation checks use detached records and owned meshes; the current world and save are read only.
        var live = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (live == null) throw new InvalidOperationException("A loaded accepted world is required.");
        string liveBefore = JsonUtility.ToJson(live);
        var checks = new List<string>();
        void Check(bool passed, string name)
        { if (!passed) throw new InvalidOperationException(name); checks.Add(name); }
        var copyMethod = typeof(YQSpatialMaterializationCompilerV2).GetNestedType("StartupPreparation", BindingFlags.NonPublic).GetMethod("CopySavedPlan", BindingFlags.Static | BindingFlags.NonPublic);
        var copy = (GeneratedWorldPlanRecord)copyMethod.Invoke(null, new object[] { live });
        var sourceMethod = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareTerrainSource", BindingFlags.Static | BindingFlags.NonPublic);
        bool Source(GeneratedWorldPlanRecord plan, out YQSpatialBlueprintTerrainSamplerV2 sampler)
        {
            object[] args = { plan, null, null };
            bool accepted = (bool)sourceMethod.Invoke(null, args); sampler = (YQSpatialBlueprintTerrainSamplerV2)args[1]; return accepted;
        }
        var sampleMethod = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("SampleTerrainSource", BindingFlags.Instance | BindingFlags.NonPublic);
        Check(Source(copy, out var cached), "Accepted terrain source admits");
        var prepare = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareBlueprint", BindingFlags.Static | BindingFlags.NonPublic);
        object[] parameters = { copy.spatialPlanV2.blueprint,
            copy.spatialPlanV2.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion, null, null };
        Check((bool)prepare.Invoke(null, parameters), "Uncached original terrain source prepares");
        var original = (YQSpatialBlueprintTerrainSamplerV2)parameters[2];
        for (int z = -10; z <= 10; z++) for (int x = -10; x <= 10; x++)
            Check(JsonUtility.ToJson(sampleMethod.Invoke(cached, new object[] { x * 73f, z * 73f })) ==
                JsonUtility.ToJson(sampleMethod.Invoke(original, new object[] { x * 73f, z * 73f })), "Source sample matches " + x + ":" + z);
        Check(Source(copy, out var reused) && ReferenceEquals(cached, reused), "Exact source reuses prepared geometry");
        copy.spatialPlanV2.blueprint.regions[0].radius += 1f;
        Check(!Source(copy, out _), "Deep unrevisioned geometry mutation rejects before cache reuse");
        Check(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(live, out var materialized, out _) && materialized.NativeJoinCount > 0, "Accepted native water join exists");
        var join = materialized.GetNativeJoin(0);
        Mesh source = null, atomic = null, cooperative = null, cancelled = null;
        try
        {
            // note: A dense mesh crossing the real accepted join bounds exercises clipping, appending and frame-boundary cancellation.
            const int n = 129;
            var vertices = new Vector3[n * n]; var uv = new Vector2[n * n]; var colors = new Color[n * n];
            var triangles = new int[(n - 1) * (n - 1) * 6];
            for (int z = 0; z < n; z++) for (int x = 0; x < n; x++)
            {
                int i = z * n + x;
                vertices[i] = new Vector3(Mathf.Lerp(join.MinimumX - 2f, join.MaximumX + 2f, x / (float)(n - 1)), .1f,
                    Mathf.Lerp(join.MinimumZ - 2f, join.MaximumZ + 2f, z / (float)(n - 1)));
                uv[i] = new Vector2(x / (float)n, z / (float)n); colors[i] = new Color(.1f, .2f, .3f, 1f);
                if (x + 1 == n || z + 1 == n) continue;
                int t = (z * (n - 1) + x) * 6;
                triangles[t] = i; triangles[t + 1] = i + n + 1; triangles[t + 2] = i + 1;
                triangles[t + 3] = i; triangles[t + 4] = i + n; triangles[t + 5] = i + n + 1;
            }
            source = new Mesh { vertices = vertices, uv = uv, colors = colors, triangles = triangles };
            atomic = UnityEngine.Object.Instantiate(source); cooperative = UnityEngine.Object.Instantiate(source); cancelled = UnityEngine.Object.Instantiate(source);
            join.ApplyToMesh(atomic, null, null, -1, true);
            var meshRoutine = typeof(YQNativeWaterJoinPatchV2).GetMethod("ApplyToMeshRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
            IEnumerator Routine(Mesh mesh) => (IEnumerator)meshRoutine.Invoke(join, new object[] { mesh, null, null, -1, true, null, true });
            IEnumerator routine = Routine(cooperative);
            int boundaries = 0;
            try
            {
                while (routine.MoveNext())
                { boundaries++; Check(cooperative.vertices.SequenceEqual(vertices) && cooperative.triangles.SequenceEqual(triangles), "Mesh stays unchanged before publication " + boundaries); }
            }
            finally { (routine as IDisposable)?.Dispose(); }
            Check(boundaries > 0, "Large mesh actually yields");
            Check(atomic.vertices.SequenceEqual(cooperative.vertices) && atomic.triangles.SequenceEqual(cooperative.triangles) &&
                atomic.uv.SequenceEqual(cooperative.uv) && atomic.colors.SequenceEqual(cooperative.colors) && atomic.normals.SequenceEqual(cooperative.normals), "Cooperative publication equals atomic geometry and attributes");
            routine = Routine(cancelled);
            try { Check(routine.MoveNext(), "Cancellation fixture reaches a frame boundary"); }
            finally { (routine as IDisposable)?.Dispose(); }
            Check(cancelled.vertices.SequenceEqual(vertices) && cancelled.triangles.SequenceEqual(triangles), "Cancellation leaves original mesh intact");
        }
        finally
        {
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
            if (atomic != null) UnityEngine.Object.DestroyImmediate(atomic);
            if (cooperative != null) UnityEngine.Object.DestroyImmediate(cooperative);
            if (cancelled != null) UnityEngine.Object.DestroyImmediate(cancelled);
        }
        Check(liveBefore == JsonUtility.ToJson(live), "Live accepted plan unchanged");
        File.WriteAllText("outputs/DialogueVramRepair_20261011/cooperative-geometry.json", JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow, passed = true, checks, evidence = "NATIVE_DETACHED_ACCEPTED_SOURCE_AND_OWNED_MESH_NOT_PLAYER_PERFORMANCE"
        }, Formatting.Indented));
    }
}
#endif
