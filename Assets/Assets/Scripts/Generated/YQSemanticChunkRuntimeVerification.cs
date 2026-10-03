using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;

// note: This bounded probe exercises the live player-following streamer only when explicitly armed by the editor marker.
// note: Run after the streamer camera pass so live-view assertions describe the pose immediately before rendering.
[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class YQSemanticChunkRuntimeVerification : MonoBehaviour
{
#if UNITY_EDITOR
    private const string R2FrameTraceRequest = "Temp/YQ_R2_FRAME_TRACE.request";
    private const string R2FrameTraceAnalysisRequest = "Temp/YQ_R2_FRAME_TRACE_ANALYZE.request";
#if UNITY_EDITOR_WIN
    // note: Calibrate Windows ETW against Unity native timestamps; Mono Stopwatch can have a different epoch from Windows QPC.
    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool QueryPerformanceCounter(out long value);
    [System.Runtime.InteropServices.DllImport("kernel32.dll", ExactSpelling = true)]
    private static extern bool QueryPerformanceFrequency(out long value);
#endif

    // note: Opt-in binary profiling carries an explicit gameplay frame ID; recorder arrival order is not a frame identity.
    private sealed class R2FrameTraceCapture : IDisposable
    {
        private static R2FrameTraceCapture activeCapture;
        private readonly Guid metadataId = Guid.NewGuid();
        private readonly long[] metadata = new long[4];
        private readonly long[] visualGateMetadata = new long[8];
        private readonly string previousLogFile;
        private readonly bool previousBinaryLog;
        private readonly bool previousAllocationCallstacks;
        private readonly bool boundedCoastAllocationStacks;
        private int allocationStackCoastFrames;
        private double allocationStackStartedAt = -1d;
        private bool allocationStackWindowStopped;
        private readonly string path;
        private readonly double startedAt;
        private readonly StringBuilder report;
        private bool disposed;
#if UNITY_EDITOR_WIN
        private readonly YQR2GpuBudgetObservation gpuBudget;
#endif

        public R2FrameTraceCapture(StringBuilder report)
        {
            this.report = report;
            previousLogFile = UnityEngine.Profiling.Profiler.logFile;
            previousBinaryLog = UnityEngine.Profiling.Profiler.enableBinaryLog;
            previousAllocationCallstacks = UnityEngine.Profiling.Profiler.enableAllocationCallstacks;
            // note: A named diagnostic records allocating call sites only in a short coast window; ordinary clean witnesses never arm it.
            const string allocationStackRequest = "Temp/YQ_R2_COAST_ALLOCATION_STACKS.request";
            boundedCoastAllocationStacks = File.Exists(allocationStackRequest);
            if (boundedCoastAllocationStacks)
            {
                File.Delete(allocationStackRequest);
                report.AppendLine("- r2CoastAllocationStacks: diagnosticOptIn=true maximumFrames=24 maximumSeconds=1 cleanCertification=false");
            }
            path = Path.GetFullPath(Path.Combine("Logs", "G08_R2_FrameTrace_" +
                DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".raw"));
            startedAt = Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR_WIN
            // note: Only the explicitly named pressure diagnostic attaches bounded in-process budget readings to this binary capture; clean witnesses do not query DXGI.
            const string gpuBudgetRequest = "Temp/YQ_R2_GPU_BUDGET.request";
            if (File.Exists(gpuBudgetRequest))
            {
                File.Delete(gpuBudgetRequest);
                if (YQR2GpuBudgetObservation.TryCreate(out gpuBudget, out string failure))
                    report.AppendLine("- r2GpuBudget: diagnosticOptIn=true cadenceHz=4 capacity=256 output=" + Path.ChangeExtension(path, ".gpubudget.tsv"));
                else report.AppendLine("- r2GpuBudget: NOT_CAPTURED reason=" + failure);
            }
#endif
            UnityEngine.Profiling.Profiler.logFile = path;
            UnityEngine.Profiling.Profiler.enableBinaryLog = true;
            UnityEngine.Profiling.Profiler.enabled = true;
            activeCapture = this;
            // note: Observe assignment redundancy only during this bounded binary capture, preserving every original renderer write.
            Array.Clear(YQPlayerFollowingSemanticChunkStreamer.R2VisualGateWriteCounts, 0, 6);
            YQPlayerFollowingSemanticChunkStreamer.R2VisualRendererWritesPerformed = 0;
            YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed = 0;
            YQPlayerFollowingSemanticChunkStreamer.R2ObserveVisualGateWrites = true;
            report.AppendLine("- r2FrameTrace: diagnosticCapture=true raw=" + path +
                " metadataGuid=" + metadataId + " profilerOverheadIncluded=true");
        }

        public static R2FrameTraceCapture TryBegin(StringBuilder report)
        {
            if (!_focusedSpeed260MotorWitnessOnly || !File.Exists(R2FrameTraceRequest))
                return null;
            File.Delete(R2FrameTraceRequest);
            // note: Never redirect an existing user profiler recording or enable deep profiling for this diagnostic.
            if (UnityEngine.Profiling.Profiler.enabled || UnityEngine.Profiling.Profiler.enableBinaryLog ||
                UnityEditorInternal.ProfilerDriver.deepProfiling)
            {
                report.AppendLine("- r2FrameTrace: NOT_CAPTURED existingProfilerOrDeepProfilingActive=true");
                return null;
            }
            return new R2FrameTraceCapture(report);
        }

        public void RecordBoundary(int unityFrame, bool coast, float wallSeconds)
        {
            if (disposed)
                return;
            // note: Enable after the first coast boundary and restore before further coast work; limits end stack collection, never movement or binary frame capture.
            if (boundedCoastAllocationStacks && coast && !allocationStackWindowStopped)
            {
                if (allocationStackStartedAt < 0d)
                {
                    allocationStackStartedAt = Time.realtimeSinceStartupAsDouble;
                    UnityEngine.Profiling.Profiler.enableAllocationCallstacks = true;
                    report.AppendLine("- r2CoastAllocationStacksStarted: boundaryFrame=" + unityFrame);
                }
                else if (++allocationStackCoastFrames >= 24 || Time.realtimeSinceStartupAsDouble - allocationStackStartedAt >= 1d)
                {
                    UnityEngine.Profiling.Profiler.enableAllocationCallstacks = previousAllocationCallstacks;
                    allocationStackWindowStopped = true;
                    report.AppendLine("- r2CoastAllocationStacksStopped: boundaryFrame=" + unityFrame + " recordedIntervals=" + allocationStackCoastFrames);
                }
            }
            // note: Keep the trace bounded even if the motor never settles; this limit only ends profiling, not the witness.
            if (Time.realtimeSinceStartupAsDouble - startedAt > 45d)
            {
                report.AppendLine("- r2FrameTraceLimit: reached45Seconds=true capturePartial=true");
                Dispose();
                return;
            }
            metadata[0] = unityFrame;
            metadata[1] = coast ? 1L : 0L;
            metadata[2] = (long)(wallSeconds * 1000000000d);
            metadata[3] = System.Diagnostics.Stopwatch.GetTimestamp();
            UnityEngine.Profiling.Profiler.EmitFrameMetaData(metadataId, 0, metadata);
            Array.Copy(YQPlayerFollowingSemanticChunkStreamer.R2VisualGateWriteCounts, visualGateMetadata, 6);
            visualGateMetadata[6] = YQPlayerFollowingSemanticChunkStreamer.R2VisualRendererWritesPerformed;
            visualGateMetadata[7] = YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed;
            UnityEngine.Profiling.Profiler.EmitFrameMetaData(metadataId, 1, visualGateMetadata);
            // note: The native timestamp of this boundary marker locates coroutine resumption within the recorded main-thread frame.
            UnityEngine.Profiling.Profiler.BeginSample("YQ.R2.WitnessFrameBoundary");
            UnityEngine.Profiling.Profiler.EndSample();
#if UNITY_EDITOR_WIN
            gpuBudget?.Record(unityFrame, coast);
#endif
        }

        public static void StopActive()
        {
            activeCapture?.Dispose();
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            YQPlayerFollowingSemanticChunkStreamer.R2ObserveVisualGateWrites = false;
            if (ReferenceEquals(activeCapture, this))
                activeCapture = null;
            // note: Close the native writer before requesting analysis; restore the prior inactive profiler configuration on every exit.
            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableAllocationCallstacks = previousAllocationCallstacks;
            UnityEngine.Profiling.Profiler.logFile = string.Empty;
            UnityEngine.Profiling.Profiler.logFile = previousLogFile;
            UnityEngine.Profiling.Profiler.enableBinaryLog = previousBinaryLog;
#if UNITY_EDITOR_WIN
            if (gpuBudget != null)
            {
                try { gpuBudget.Write(Path.ChangeExtension(path, ".gpubudget.tsv")); }
                catch (Exception exception) { report.AppendLine("- r2GpuBudget: WRITE_FAILED reason=" + exception.Message); }
                finally { gpuBudget.Dispose(); }
            }
#endif
            try
            {
                File.WriteAllLines(R2FrameTraceAnalysisRequest, new[] { path, metadataId.ToString() });
            }
            catch (IOException exception)
            {
                // note: A diagnostic receipt failure must not prevent the enclosing verifier from restoring motor and input state.
                Debug.LogError("[YQR2FrameTrace] Could not queue analysis: " + exception.Message);
            }
        }
    }

    public static void AnalyzeRequestedR2FrameTrace()
    {
        if (!File.Exists(R2FrameTraceAnalysisRequest) || UnityEngine.Profiling.Profiler.enabled)
            return;
        string[] request;
        Guid metadataId;
        bool assetSessionOnly;
        try
        {
            request = File.ReadAllLines(R2FrameTraceAnalysisRequest);
            // note: Compare identical absent request paths in idle Edit; native allocation samples distinguish path normalization from dispatch work.
            if (request.Length == 3 && request[2] == "marker-poll-traced" &&
                Guid.TryParse(request[1], out _) && !UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                string pollPath = Path.GetFullPath(request[0]);
                if (!pollPath.StartsWith(Path.GetFullPath("Logs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return;
                if (UnityEngine.Profiling.Profiler.enableBinaryLog || UnityEngine.Profiling.Profiler.enableAllocationCallstacks || UnityEditorInternal.ProfilerDriver.deepProfiling)
                    return;
                File.Delete(R2FrameTraceAnalysisRequest);
                var pollTraceReport = new StringBuilder();
                var pollCapture = new R2FrameTraceCapture(pollTraceReport);
                pollCapture.RecordBoundary(Time.frameCount, false, 0f);
                try { WriteR2MarkerPollProbe(Path.ChangeExtension(pollPath, ".markerpoll.tsv")); }
                finally
                {
                    File.WriteAllText(Path.ChangeExtension(pollPath, ".polltrace.txt"), pollTraceReport.ToString());
                    UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.delayCall += pollCapture.Dispose;
                }
                return;
            }
            // note: Route arithmetic is verified in Edit Mode without staging a player or preparing streamed cells.
            if (request.Length == 3 && request[2] == "speed260-route-only" &&
                Guid.TryParse(request[1], out _) && !Application.isPlaying)
            {
                string routePath = Path.GetFullPath(request[0]);
                if (!routePath.StartsWith(Path.GetFullPath("Logs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return;
                File.Delete(R2FrameTraceAnalysisRequest);
                WriteR2Speed260RouteRegression(Path.ChangeExtension(routePath, ".route.tsv"));
                return;
            }
            // note: Reuse the bounded Edit-only analysis transport for request-policy verification; no model or profile is loaded.
            if (request.Length == 3 && request[2] == "ollama-policy-only" &&
                Guid.TryParse(request[1], out _) && !Application.isPlaying)
            {
                string policyPath = Path.GetFullPath(request[0]);
                if (!policyPath.StartsWith(Path.GetFullPath("Logs") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return;
                File.Delete(R2FrameTraceAnalysisRequest);
                WriteR2OllamaPolicyRegression(Path.ChangeExtension(policyPath, ".ollamapolicy.tsv"));
                return;
            }
            if (request.Length < 2 || request.Length > 4 || !Guid.TryParse(request[1], out metadataId) ||
                (request.Length == 3 && request[2] != "counters-only" && request[2] != "clocks-only" && request[2] != "gpu-budget-only" && request[2] != "detail-binding-only" && request[2] != "gpu-times-only" && request[2] != "gpu-config-only" && request[2] != "sentinel-only" && request[2] != "activation-removal-only" && request[2] != "teardown-only" && request[2] != "ecology-handoff-only" && request[2] != "llama-launch-only" && request[2] != "terrain-allocation-only" && request[2] != "terrain-allocation-traced" && request[2] != "optional-components-traced" && request[2] != "blueprint-hash-traced" && request[2] != "location-normalization-traced" && request[2] != "aggregate-admission-reference" && request[2] != "aggregate-admission-only" && request[2] != "idle-allocation-stacks" && request[2] != "idle-allocation-stacks-watcher-paused") ||
                (request.Length == 4 && request[2] != "asset-roots-only" && request[2] != "asset-session-only" && request[2] != "sample-owners-only" && request[2] != "sample-threads-only" && request[2] != "sample-callstacks-only"))
            {
                File.Delete(R2FrameTraceAnalysisRequest);
                return;
            }
            assetSessionOnly = request.Length == 4 && request[2] == "asset-session-only";
            // note: Only an explicit resource census can run during Play; profiler parsing and dependency metadata remain post-Play operations.
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode && !assetSessionOnly)
                return;
            File.Delete(R2FrameTraceAnalysisRequest);
        }
        catch (IOException exception) when ((exception.HResult & 0xffff) == 32 || (exception.HResult & 0xffff) == 33)
        {
            // note: An external marker writer may still own the file when this Editor tick observes it. Retry after its sharing lock closes without consuming the request; other IO failures remain visible.
            return;
        }
        bool countersOnly = request.Length == 3 && request[2] == "counters-only";
        bool clocksOnly = request.Length == 3 && request[2] == "clocks-only";
        bool gpuBudgetOnly = request.Length == 3 && request[2] == "gpu-budget-only";
        bool detailBindingOnly = request.Length == 3 && request[2] == "detail-binding-only";
        bool gpuTimesOnly = request.Length == 3 && request[2] == "gpu-times-only";
        bool gpuConfigOnly = request.Length == 3 && request[2] == "gpu-config-only";
        bool sentinelOnly = request.Length == 3 && request[2] == "sentinel-only";
        bool activationRemovalOnly = request.Length == 3 && request[2] == "activation-removal-only";
        bool teardownOnly = request.Length == 3 && request[2] == "teardown-only";
        bool ecologyHandoffOnly = request.Length == 3 && request[2] == "ecology-handoff-only";
        bool assetRootsOnly = request.Length == 4 && request[2] == "asset-roots-only";
        bool sampleOwnersOnly = request.Length == 4 && request[2] == "sample-owners-only";
        bool sampleThreadsOnly = request.Length == 4 && request[2] == "sample-threads-only";
        bool sampleCallstacksOnly = request.Length == 4 && request[2] == "sample-callstacks-only";
        bool llamaLaunchOnly = request.Length == 3 && request[2] == "llama-launch-only";
        string path = Path.GetFullPath(request[0]);
        string logsRoot = Path.GetFullPath("Logs") + Path.DirectorySeparatorChar;
        if (!path.StartsWith(logsRoot, StringComparison.OrdinalIgnoreCase))
            return;
        string summaryPath = Path.ChangeExtension(path, assetSessionOnly ? ".assetsession.tsv" : assetRootsOnly ? ".assetroots.tsv" : sampleOwnersOnly ? ".sampleowners.tsv" : sampleThreadsOnly ? ".samplethreads.tsv" : sampleCallstacksOnly ? ".samplecallstacks.tsv" : countersOnly ? ".counters.tsv" : clocksOnly ? ".clocks.tsv" : gpuBudgetOnly ? ".gpubudget.tsv" : detailBindingOnly ? ".detailbinding.tsv" : gpuTimesOnly ? ".gputimes.tsv" : gpuConfigOnly ? ".gpuconfig.tsv" : sentinelOnly ? ".sentinel.tsv" : activationRemovalOnly ? ".activationremoval.tsv" : ".tsv");
        try
        {
            if (ecologyHandoffOnly)
            {
                WriteR2EcologyHandoffRegression(summaryPath);
                return;
            }
            if (teardownOnly)
            {
                WriteR2StreamingTeardownRegression(summaryPath);
                return;
            }
            // note: Compare actual budget admission on transient owners without loading a profile or preparing a gameplay cell.
            if (request.Length == 3 && (request[2] == "aggregate-admission-reference" || request[2] == "aggregate-admission-only"))
            {
                WriteR2AggregateAdmissionRegression(Path.ChangeExtension(path, ".aggregateadmission.tsv"), request[2] == "aggregate-admission-only");
                return;
            }
            // note: Measure canonical formatting in Edit without changing validation, accepted content or live owners.
            if (request.Length == 3 && request[2] == "blueprint-hash-traced")
            {
                if (UnityEngine.Profiling.Profiler.enableBinaryLog || UnityEngine.Profiling.Profiler.enableAllocationCallstacks || UnityEditorInternal.ProfilerDriver.deepProfiling || File.Exists("Temp/YQ_R2_GPU_BUDGET.request"))
                    throw new InvalidOperationException("Blueprint hash fixture requires inactive profiler and allocation stacks");
                var hashTraceReport = new StringBuilder();
                var hashCapture = new R2FrameTraceCapture(hashTraceReport);
                hashCapture.RecordBoundary(Time.frameCount, false, 0f);
                try { WriteR2BlueprintHashRegression(Path.ChangeExtension(path, ".blueprinthash.tsv")); }
                finally
                {
                    File.WriteAllText(Path.ChangeExtension(path, ".hashtrace.txt"), hashTraceReport.ToString());
                    UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.delayCall += hashCapture.Dispose;
                }
                return;
            }
            // note: Isolate periodic state normalization on detached documents, without loading or committing a live profile.
            if (request.Length == 3 && request[2] == "location-normalization-traced")
            {
                if (UnityEngine.Profiling.Profiler.enableBinaryLog || UnityEditorInternal.ProfilerDriver.deepProfiling || File.Exists("Temp/YQ_R2_GPU_BUDGET.request"))
                    throw new InvalidOperationException("Location normalization fixture requires inactive profiler and no GPU request");
                var locationTraceReport = new StringBuilder();
                var locationCapture = new R2FrameTraceCapture(locationTraceReport);
                locationCapture.RecordBoundary(Time.frameCount, false, 0f);
                try { WriteR2LocationNormalizationProbe(Path.ChangeExtension(path, ".locationnormalization.tsv")); }
                finally
                {
                    File.WriteAllText(Path.ChangeExtension(path, ".locationtrace.txt"), locationTraceReport.ToString());
                    UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.delayCall += locationCapture.Dispose;
                }
                return;
            }
            // note: This bounded Edit-only fixture compares optional component semantics and native allocation paths without preparing the accepted world.
            if (request.Length == 3 && request[2] == "optional-components-traced")
            {
                if (UnityEngine.Profiling.Profiler.enableBinaryLog || UnityEditorInternal.ProfilerDriver.deepProfiling || File.Exists("Temp/YQ_R2_GPU_BUDGET.request"))
                    throw new InvalidOperationException("Optional component regression requires inactive profiler and no GPU request");
                var componentTraceReport = new StringBuilder();
                var componentCapture = new R2FrameTraceCapture(componentTraceReport);
                componentCapture.RecordBoundary(Time.frameCount, false, 0f);
                try { WriteR2OptionalComponentRegression(Path.ChangeExtension(path, ".optionalcomponents.tsv")); }
                finally
                {
                    File.WriteAllText(Path.ChangeExtension(path, ".componenttrace.txt"), componentTraceReport.ToString());
                    UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.delayCall += componentCapture.Dispose;
                }
                return;
            }
            // note: Capture only idle Edit allocations; no accepted-world preparation or gameplay work is performed.
            if (request.Length == 3 && (request[2] == "idle-allocation-stacks" || request[2] == "idle-allocation-stacks-watcher-paused"))
            {
                BeginR2IdleAllocationStackCapture(Path.ChangeExtension(path, ".idlestacks.txt"), request[2] == "idle-allocation-stacks-watcher-paused");
                return;
            }
            // note: Measure the accepted sampler in Edit Mode without touching live profile services or enabling a new runtime capture.
            if (request.Length == 3 && request[2] == "terrain-allocation-only")
            {
                WriteR2TerrainAllocationRegression(Path.ChangeExtension(path, ".terrainallocation.tsv"));
                return;
            }
            if (request.Length == 3 && request[2] == "terrain-allocation-traced")
            {
                // note: A single Edit frame supplies native GC.Alloc byte metadata when Unity's managed allocation counter fails calibration.
                if (UnityEngine.Profiling.Profiler.enableBinaryLog || UnityEditorInternal.ProfilerDriver.deepProfiling || File.Exists("Temp/YQ_R2_GPU_BUDGET.request"))
                    throw new InvalidOperationException("Edit allocation trace requires inactive profiler and no GPU request");
                var traceReport = new StringBuilder();
                var editCapture = new R2FrameTraceCapture(traceReport);
                editCapture.RecordBoundary(Time.frameCount, false, 0f);
                try { WriteR2TerrainAllocationRegression(Path.ChangeExtension(path, ".terrainallocation.tsv")); }
                finally
                {
                    File.WriteAllText(Path.ChangeExtension(path, ".terraintrace.txt"), traceReport.ToString());
                    // note: Let this Editor frame close before flushing; no Play Mode operation or next gameplay warmup is scheduled.
                    UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.delayCall += editCapture.Dispose;
                }
                return;
            }
            if (llamaLaunchOnly)
            {
                WriteR2LlamaLaunchRegression(Path.ChangeExtension(path, ".llamalaunch.tsv"));
                return;
            }
            if (gpuConfigOnly)
            {
                WriteR2GpuConfigurationProbe(summaryPath);
                return;
            }
            if (activationRemovalOnly)
            {
                WriteR2ActivationRemovalRegression(summaryPath);
                return;
            }
            if (sentinelOnly)
            {
                WriteR2NoOwnerSentinelRegression(summaryPath);
                return;
            }
            if (detailBindingOnly)
            {
                WriteR2DetailBindingRegression(summaryPath);
                return;
            }
            if (gpuBudgetOnly)
            {
#if UNITY_EDITOR_WIN
                // note: Exercise the native read-only adapter query once in Edit Mode before arming the named runtime diagnostic; this mode never loads a profiler capture.
                if (!YQR2GpuBudgetObservation.TryCreate(out var observer, out string failure))
                    throw new InvalidOperationException(failure);
                using (observer) { observer.Record(Time.frameCount, false, true); observer.Write(summaryPath); }
                return;
#else
                throw new InvalidOperationException("DXGI GPU budget observation requires Windows Editor");
#endif
            }
            if (assetSessionOnly)
            {
                WriteR2AssetSessionSnapshot(request[3], summaryPath);
                if (File.Exists(summaryPath + ".error.txt"))
                    File.Delete(summaryPath + ".error.txt");
                return;
            }
            // note: Dependency metadata and already loaded registry identities are inspected only after Play, without reloading a capture or loading/unloading assets.
            if (assetRootsOnly)
            {
                WriteR2AssetRootAudit(request[3], summaryPath);
                if (File.Exists(summaryPath + ".error.txt"))
                    File.Delete(summaryPath + ".error.txt");
                return;
            }
            // note: Reuse an already loaded capture on analysis retry instead of appending its native buffers a second time.
            bool alreadyLoaded = false;
            for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex;
                 frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
            {
                using (var existing = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (existing.valid && existing.GetFrameMetaDataCount(metadataId, 0) > 0)
                    {
                        alreadyLoaded = true;
                        break;
                    }
                }
            }
            // note: Append the saved capture instead of clearing any existing user Profiler history; metadata identifies only this run.
            if (!alreadyLoaded && (!File.Exists(path) || !UnityEditorInternal.ProfilerDriver.LoadProfile(path, true)))
                throw new InvalidOperationException("Unity could not load the binary capture");
            WriteR2VisualGateWriteAudit(metadataId, Path.ChangeExtension(path, ".visualwrites.tsv"));
            if (gpuTimesOnly)
            {
                // note: Inspect recorded GPU duration availability in the existing capture only; profiler-frame GPU totals are not an assumed coroutine interval or proof of a command's cause.
                int matched = 0, positive = 0;
                using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("# Saved capture GPU durations; captureGuid=" + metadataId + "; extractionAssemblyMvid=" +
                        typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
                        "; zero means unavailable-or-zero, not no GPU work; GPU durations are not native clock timestamps.");
                    writer.WriteLine("profilerFrame\tunityFrame\tphase\twallNs\tboundaryQpc\tgpuTimeNs\tgpuTimeMs\tpositiveRecordedDuration\tgpuAvailabilityBits\tgpuAvailabilityFlags");
                    for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex;
                        frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
                    {
                        using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                        {
                            if (!view.valid || view.GetFrameMetaDataCount(metadataId, 0) == 0) continue;
                            var data = view.GetFrameMetaData<long>(metadataId, 0);
                            if (data.Length < 4) continue;
                            ulong gpuNs = view.frameGpuTimeNs;
                            float gpuMs = view.frameGpuTimeMs;
                            bool recorded = gpuNs > 0 && !float.IsNaN(gpuMs) && !float.IsInfinity(gpuMs) && gpuMs > 0f;
                            // note: Availability belongs to this stored profiler frame, not the current area setting or a guessed wall interval.
                            var availability = UnityEditorInternal.ProfilerDriver.GetGpuStatisticsAvailabilityState(frame);
                            matched++;
                            if (recorded) positive++;
                            writer.WriteLine(frame + "\t" + data[0] + "\t" + data[1] + "\t" + data[2] + "\t" + data[3] +
                                "\t" + gpuNs + "\t" + gpuMs.ToString("R", CultureInfo.InvariantCulture) + "\t" + recorded +
                                "\t" + (int)availability + "\t" + availability);
                        }
                    }
                    writer.WriteLine("# matchedFrames=" + matched + " positiveRecordedDurations=" + positive);
                }
                return;
            }
            if (clocksOnly)
            {
                // note: Existing QPC metadata calibrates external observations against native boundary timestamps without assigning a coarse observation to an assumed frame.
                using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
                {
                    writer.WriteLine("# Saved capture boundary clocks; qpcFrequency=" + System.Diagnostics.Stopwatch.Frequency + "; no new capture or runtime work.");
                    writer.WriteLine("profilerFrame\tunityFrame\tphase\twallNs\tboundaryQpc\tnativeBoundaryNs");
#if UNITY_EDITOR_WIN
                    // note: Bracket the native profiler clock with actual Windows QPC reads; this post-Play observation never changes the recorded witness.
                    if (!QueryPerformanceFrequency(out long windowsFrequency)) throw new InvalidOperationException("Windows QPC frequency unavailable");
                    var nativeRatio = ProfilerUnsafeUtility.TimestampToNanosecondsConversionRatio;
                    for (int calibration = 0; calibration < 3; calibration++)
                    {
                        if (!QueryPerformanceCounter(out long windowsBefore)) throw new InvalidOperationException("Windows QPC unavailable");
                        long nativeTimestamp = ProfilerUnsafeUtility.Timestamp;
                        long managedTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                        if (!QueryPerformanceCounter(out long windowsAfter)) throw new InvalidOperationException("Windows QPC unavailable");
                        writer.WriteLine("# clockCalibration windowsBefore=" + windowsBefore + " nativeTimestamp=" + nativeTimestamp +
                            " managedTimestamp=" + managedTimestamp + " windowsAfter=" + windowsAfter + " windowsFrequency=" + windowsFrequency +
                            " nativeNumerator=" + nativeRatio.Numerator + " nativeDenominator=" + nativeRatio.Denominator);
                    }
#endif
                    for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex; frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
                    {
                        using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                        {
                            if (!view.valid || view.GetFrameMetaDataCount(metadataId, 0) == 0) continue;
                            var data = view.GetFrameMetaData<long>(metadataId, 0);
                            if (data.Length < 4) continue;
                            ulong boundaryNs = 0;
                            for (int sample = 0; sample < view.sampleCount; sample++)
                                if (view.GetSampleName(sample) == "YQ.R2.WitnessFrameBoundary") { boundaryNs = view.GetSampleStartTimeNs(sample); break; }
                            writer.WriteLine(frame + "\t" + data[0] + "\t" + data[1] + "\t" + data[2] + "\t" + data[3] + "\t" + boundaryNs);
                        }
                    }
                }
                return;
            }
            if (sampleCallstacksOnly)
            {
                WriteR2AllocationCallstacks(request[3], metadataId, summaryPath);
                if (File.Exists(summaryPath + ".error.txt")) File.Delete(summaryPath + ".error.txt");
                return;
            }
            if (sampleOwnersOnly || sampleThreadsOnly)
            {
                // note: Inspect existing UI/native sample metadata around one named stall after Play; the original capture and timing extraction remain unchanged.
                WriteR2SampleOwnerAudit(request[3], metadataId, summaryPath, sampleThreadsOnly);
                if (File.Exists(summaryPath + ".error.txt"))
                    File.Delete(summaryPath + ".error.txt");
                return;
            }
            if (countersOnly)
            {
                // note: Reuse captured memory counters after Play without another run or overwriting the immutable timing extraction; absent values remain unavailable, not zero.
                string[] counterNames = { "Gfx Used Memory", "Gfx Reserved Memory", "Total Used Memory", "Total Reserved Memory",
                    "GC Used Memory", "GC Reserved Memory", "System Used Memory", "Texture Memory", "Mesh Memory", "RenderTexture Memory" };
                using (var counterWriter = new StreamWriter(summaryPath, false, Encoding.UTF8))
                {
                    counterWriter.WriteLine("# Saved-capture counters only. Graphics allocation is not GPU residency or proof of pressure; unavailable counters are NA.");
                    counterWriter.WriteLine("# Current Editor hardware: graphicsMemorySizeMb=" + SystemInfo.graphicsMemorySize +
                        " graphicsDeviceVersion=" + SystemInfo.graphicsDeviceVersion);
                    counterWriter.WriteLine("profilerFrame\tunityFrame\tcounter\tpresent\tbytes");
                    for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex;
                         frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
                    {
                        using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                        {
                            if (!view.valid || view.GetFrameMetaDataCount(metadataId, 0) == 0)
                                continue;
                            var metadata = view.GetFrameMetaData<long>(metadataId, 0);
                            if (metadata.Length < 4)
                                continue;
                            foreach (string counterName in counterNames)
                            {
                                int markerId = view.GetMarkerId(counterName);
                                bool present = markerId >= 0 && view.HasCounterValue(markerId);
                                counterWriter.WriteLine(frame + "\t" + metadata[0] + "\t" + counterName + "\t" + present + "\t" +
                                    (present ? view.GetCounterValueAsLong(markerId).ToString(CultureInfo.InvariantCulture) : "NA"));
                            }
                        }
                    }
                }
                // note: Identify the largest currently loaded resources without loading new assets; this post-Play inventory is separate from captured gameplay memory and GPU residency.
                using (var inventory = new StreamWriter(Path.ChangeExtension(path, ".liveassets.tsv"), false, Encoding.UTF8))
                {
                    inventory.WriteLine("# POST-PLAY current Editor assets utc=" + DateTime.UtcNow.ToString("O") +
                        "; runtime memory estimates are not captured-frame allocations or GPU residency; shared resources are not per-cell copies.");
                    inventory.WriteLine("type\tcount\ttotalEstimatedBytes\tresourceBytes\tname\tassetPath");
                    foreach (Type resourceType in new[] { typeof(Mesh), typeof(Texture), typeof(TerrainData) })
                    {
                        UnityEngine.Object[] resources = Resources.FindObjectsOfTypeAll(resourceType);
                        var sizes = new List<KeyValuePair<long, UnityEngine.Object>>(resources.Length);
                        long total = 0;
                        foreach (UnityEngine.Object resource in resources)
                        {
                            long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(resource);
                            total += bytes;
                            sizes.Add(new KeyValuePair<long, UnityEngine.Object>(bytes, resource));
                        }
                        inventory.WriteLine(resourceType.Name + "\t" + resources.Length + "\t" + total + "\t0\t[total]\t");
                        sizes.Sort((left, right) => right.Key.CompareTo(left.Key));
                        for (int i = 0; i < Math.Min(20, sizes.Count); i++)
                        {
                            UnityEngine.Object resource = sizes[i].Value;
                            inventory.WriteLine(resourceType.Name + "\t0\t0\t" + sizes[i].Key + "\t" +
                                resource.name.Replace('\t', ' ').Replace('\n', ' ') + "\t" +
                                UnityEditor.AssetDatabase.GetAssetPath(resource).Replace('\t', ' ').Replace('\n', ' '));
                        }
                    }
                }
                if (File.Exists(summaryPath + ".error.txt"))
                    File.Delete(summaryPath + ".error.txt");
                return;
            }
            using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
            {
                writer.WriteLine("# Explicit frame metadata; wall intervals span two coroutine boundaries. Inclusive samples overlap and must not be summed.");
                writer.WriteLine("profilerFrame\tunityFrame\tphase\twallMs\tthread\tframeStartNs\tframeMs\tsampleStartNs\tsampleMs\tsample");
                int matchedFrames = 0;
                int first = UnityEditorInternal.ProfilerDriver.firstFrameIndex;
                int last = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
                for (int frame = first; frame <= last; frame++)
                {
                    long unityFrame = -1, phase = -1, wallNs = 0;
                    using (var main = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                    {
                        if (!main.valid || main.GetFrameMetaDataCount(metadataId, 0) == 0)
                            continue;
                        var data = main.GetFrameMetaData<long>(metadataId, 0);
                        if (data.Length < 4)
                            continue;
                        unityFrame = data[0];
                        phase = data[1];
                        wallNs = data[2];
                    }
                    matchedFrames++;
                    // note: Analyze after Play Mode so sample-name formatting and file I/O cannot inflate measured gameplay frames.
                    for (int thread = 0; thread < 256; thread++)
                    {
                        using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, thread))
                        {
                            if (!view.valid)
                                break;
                            if (thread != 0 && (view.threadName ?? string.Empty).IndexOf("Render", StringComparison.OrdinalIgnoreCase) < 0)
                                continue;
                            string prefix = frame + "\t" + unityFrame + "\t" + (phase == 1 ? "coast" : "input") +
                                "\t" + (wallNs / 1000000d).ToString("F3", CultureInfo.InvariantCulture) +
                                "\t" + view.threadName + "\t" + view.frameStartTimeNs +
                                "\t" + view.frameTimeMs.ToString("F3", CultureInfo.InvariantCulture);
                            writer.WriteLine(prefix + "\t0\t0\t[frame]");
                            for (int sample = 0; sample < view.sampleCount; sample++)
                            {
                                float duration = view.GetSampleTimeMs(sample);
                                // note: Native profiler samples can have no registered name; retain their timing instead of aborting the trace.
                                string name = view.GetSampleName(sample) ?? "<unnamed>";
                                if (duration < 1f && name != "YQ.R2.WitnessFrameBoundary")
                                    continue;
                                writer.WriteLine(prefix + "\t" + view.GetSampleStartTimeNs(sample) +
                                    "\t" + duration.ToString("F3", CultureInfo.InvariantCulture) +
                                    "\t" + name.Replace('\t', ' ').Replace('\n', ' '));
                            }
                        }
                    }
                }
                writer.WriteLine("# matchedFrames=" + matchedFrames + " result=" + (matchedFrames > 0 ? "CAPTURED" : "NO_FRAME_METADATA"));
            }
            // note: A successful retry supersedes only this capture's prior generated analysis error receipt.
            if (File.Exists(summaryPath + ".error.txt"))
                File.Delete(summaryPath + ".error.txt");
        }
        catch (Exception exception)
        {
            File.WriteAllText(summaryPath + ".error.txt", exception.ToString());
            Debug.LogError("[YQR2FrameTrace] Analysis failed: " + exception.Message);
        }
    }

    private static void WriteR2VisualGateWriteAudit(Guid metadataId, string summaryPath)
    {
        // note: Saved cumulative counters share the exact frame-boundary identity; no fresh runtime or asset access is needed for extraction.
        using var writer = new StreamWriter(summaryPath, false, Encoding.UTF8);
        writer.WriteLine("# Binary-only visual assignment observer; original writes preserved; captureGuid=" + metadataId);
        writer.WriteLine("profilerFrame\tunityFrame\tphase\tgateCalls\trendererAssignmentsConsidered\trendererAlreadyRequested\tterrainAssignmentsConsidered\tterrainAlreadyRequested\trootScans\trendererWritesPerformed\tterrainWritesPerformed");
        int matched = 0;
        for (int frame = UnityEditorInternal.ProfilerDriver.firstFrameIndex; frame <= UnityEditorInternal.ProfilerDriver.lastFrameIndex; frame++)
        {
            using var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0);
            if (!view.valid || view.GetFrameMetaDataCount(metadataId, 0) == 0 || view.GetFrameMetaDataCount(metadataId, 1) == 0)
                continue;
            var boundary = view.GetFrameMetaData<long>(metadataId, 0);
            var writes = view.GetFrameMetaData<long>(metadataId, 1);
            if (boundary.Length < 4 || writes.Length < 6)
                continue;
            writer.Write(frame + "\t" + boundary[0] + "\t" + (boundary[1] == 1 ? "coast" : "input"));
            for (int index = 0; index < 8; index++)
                writer.Write("\t" + (index < writes.Length ? writes[index] : -1L));
            writer.WriteLine();
            matched++;
        }
        writer.WriteLine("# matchedFrames=" + matched);
    }

    private static void WriteR2GpuConfigurationProbe(string summaryPath)
    {
        // note: This explicit Edit-only probe tests area readback/restoration without recording frames or changing graphics jobs, render settings, or world state.
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
            UnityEngine.Profiling.Profiler.enabled || UnityEngine.Profiling.Profiler.enableBinaryLog ||
            UnityEditorInternal.ProfilerDriver.deepProfiling)
            throw new InvalidOperationException("GPU configuration probe requires idle Edit Mode with profiling disabled");
        bool previousGpuArea = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU);
        bool previousCpuArea = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU);
        bool enabledReadback = false;
        try
        {
            UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, true);
            enabledReadback = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU);
        }
        finally
        {
            UnityEngine.Profiling.Profiler.SetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU, previousGpuArea);
        }
        bool restored = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.GPU) == previousGpuArea;
        bool cpuUnchanged = UnityEngine.Profiling.Profiler.GetAreaEnabled(UnityEngine.Profiling.ProfilerArea.CPU) == previousCpuArea;
        bool profilingStillDisabled = !UnityEngine.Profiling.Profiler.enabled && !UnityEngine.Profiling.Profiler.enableBinaryLog;
        int historicalFrame = UnityEditorInternal.ProfilerDriver.lastFrameIndex;
        string historicalFlags = "NA";
        if (historicalFrame >= 0 && historicalFrame >= UnityEditorInternal.ProfilerDriver.firstFrameIndex)
        {
            using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(historicalFrame, 0))
                if (view.valid)
                    historicalFlags = UnityEditorInternal.ProfilerDriver.GetGpuStatisticsAvailabilityState(historicalFrame).ToString();
        }
        using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
        {
            writer.WriteLine("# Edit-only GPU area configuration; utc=" + DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture) +
                "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
                "; last-frame availability is historical only; configuration PASS does not prove GPU durations are supported or gathered.");
            writer.WriteLine("result\tunityVersion\tgraphicsApi\tactualThreadingMode\tplayerSettingsGraphicsJobs\tactiveBuildTarget\tgpuAreaBefore\tenabledReadback\tgpuAreaRestored\tcpuAreaUnchanged\tprofilingStillDisabled\thistoricalLastFrame\thistoricalGpuFlags");
            writer.WriteLine((enabledReadback && restored && cpuUnchanged && profilingStillDisabled ? "PASS" : "FAIL") +
                "\t" + Application.unityVersion + "\t" + SystemInfo.graphicsDeviceType + "\t" + SystemInfo.renderingThreadingMode +
                "\t" + UnityEditor.PlayerSettings.graphicsJobs + "\t" + UnityEditor.EditorUserBuildSettings.activeBuildTarget +
                "\t" + previousGpuArea + "\t" + enabledReadback + "\t" + restored + "\t" + cpuUnchanged +
                "\t" + profilingStillDisabled + "\t" + historicalFrame + "\t" + historicalFlags);
        }
    }

    private static void WriteR2Speed260RouteRegression(string summaryPath)
    {
        // note: Verify direction coverage, stable exit, and physical-travel rejection independently of frame pacing and scene preparation.
        var report = new StringBuilder("# Edit-only speed260 route-v2 regression; utc=" + DateTime.UtcNow.ToString("O") +
            "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId + "\n");
        int checks = 0;
        void Check(bool condition, string name)
        {
            report.AppendLine((condition ? "PASS\t" : "FAIL\t") + name);
            if (!condition) throw new InvalidOperationException(name);
            checks++;
        }
        try
        {
            Check(Speed260DirectionIndex(0, 8) == -1, "first boost retains seam heading");
            // note: Exercise waypoint planning without a scene, including an obstructed direct line and bounded fail-closed cases.
            bool TestWalkEdge(Vector3 from, Vector3 to)
            {
                for (int sample = 0; sample <= 32; sample++)
                {
                    Vector3 point = Vector3.Lerp(from, to, sample / 32f);
                    if (Mathf.Abs(point.x) < 6f && point.z > 3f && point.z < 9f) return false;
                }
                return true;
            }
            var testTargets = new List<Vector3> { new Vector3(0f, 0f, 16f) };
            var testRoute = FindSpeed260StagingRoute(Vector3.zero, testTargets, TestWalkEdge,
                out int testTarget, out int testVisited);
            Check(testRoute != null && testTarget == 0 && testVisited <= 2145, "waypoint search routes around direct obstacle within node bound");
            bool testEdgesClear = testRoute != null;
            float testLength = 0f;
            Vector3 testPrevious = Vector3.zero;
            if (testRoute != null)
                foreach (Vector3 point in testRoute)
                {
                    testEdgesClear &= TestWalkEdge(testPrevious, point);
                    testLength += PlanarDistance(testPrevious, point);
                    testPrevious = point;
                }
            Check(testEdgesClear && testLength <= 135f, "all reconstructed waypoint edges clear and within walk budget");
            Check(testPrevious == testTargets[0], "waypoint route ends at exact selected staging target");
            Check(FindSpeed260StagingRoute(Vector3.zero, testTargets, (from, to) => false, out _, out _) == null,
                "blocked staging fails closed");
            Check(FindSpeed260StagingRoute(Vector3.zero, new List<Vector3>(), TestWalkEdge, out _, out _) == null,
                "missing turn lane fails closed");
            Check(FindSpeed260StagingRoute(Vector3.zero, new List<Vector3> { new Vector3(0f, 0f, 150f) },
                (from, to) => true, out _, out _) == null, "route beyond existing staging budget rejected");
            for (int profile = 0; profile < 6; profile++)
                for (int leg = 0; leg < 9; leg++)
                    Check(Speed260PreflightStep(profile, leg) > 0f && Speed260PreflightStep(profile, leg) <=
                        260f * YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds,
                        "route profile step bounded " + profile + "/" + leg);
            // note: Every extremal combination of first-boost and eight turn step lengths must remain inside the sampled footprint, including a rotated player basis.
            foreach (float yaw in new[] { 0f, 37f, 90f })
            {
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3[] directions = Speed260TurnDirections(forward, right);
                Bounds footprint = Speed260TurnFootprint(Vector3.zero, directions, 13f);
                footprint.Expand(0.002f);
                bool contained = true;
                for (int combination = 0; combination < 512; combination++)
                {
                    Vector3 point = Vector3.zero;
                    for (int leg = 0; leg < directions.Length; leg++)
                    {
                        if ((combination & (1 << leg)) != 0) point += directions[leg].normalized * 13f;
                        contained &= footprint.Contains(point);
                    }
                }
                Check(contained, "turn footprint contains 512 extremal trajectories at yaw " + yaw);
            }
            for (int count = 10; count <= 512; count++)
            {
                int mask = 0;
                for (int frame = 0; frame < count; frame++)
                {
                    int direction = Speed260DirectionIndex(frame, 8);
                    if (direction >= 0) mask |= 1 << direction;
                }
                Check(mask == 255 && Speed260DirectionIndex(count - 1, 8) == -1,
                    "all eight directions and fixed exit at " + count + " frames");
            }
            foreach (float delta in new[] { 1f / 120f, 1f / 60f, 1f / 30f, 0.05f })
            foreach (float braking in new[] { 34f, 28f * 0.55f })
            {
                float speed = 260f, distance = 0f, tolerance = 0f;
                while (speed > 2f)
                {
                    speed = Mathf.Max(0f, speed - braking * delta);
                    float step = speed * delta;
                    distance += step;
                    tolerance += Mathf.Min(step, 0.16f);
                }
                Check(Speed260CoastTravelValid(distance, distance, tolerance, distance, 128f), "unobstructed coast " + delta + "/" + braking);
                Check(!Speed260CoastTravelValid(distance, 13.9f, tolerance, 13.9f, 128f), "historical wall-stall rejected " + delta + "/" + braking);
                Check(!Speed260CoastTravelValid(distance, distance, tolerance, 0f, 128f), "circling without net traversal rejected " + delta + "/" + braking);
            }
            Check(!Speed260CoastTravelValid(float.NaN, 1000f, 1f, 1000f, 128f), "NaN rejected");
            Check(!Speed260CoastTravelValid(float.PositiveInfinity, 1000f, 1f, 1000f, 128f), "infinity rejected");
            Check(!Speed260CoastTravelValid(0f, 0f, 0f, 0f, 128f), "no motion rejected");
            Check(Speed260CoastTimeoutSeconds(260f, 34f, 28f * 0.55f) > 18d,
                "ordinary airborne braking receives full coast time");
            Check(Speed260CoastTimeoutSeconds(260f, 34f, 34f) == 12d,
                "ordinary ground braking retains the existing minimum bound");
            Check(double.IsNaN(Speed260CoastTimeoutSeconds(260f, 34f, 0f)), "zero airborne braking rejected");
            Check(double.IsNaN(Speed260CoastTimeoutSeconds(260f, -1f, 15.4f)), "negative braking rejected");
            Check(double.IsNaN(Speed260CoastTimeoutSeconds(260f, 34f, float.NaN)), "NaN braking rejected");
            Check(double.IsNaN(Speed260CoastTimeoutSeconds(260f, 0.001f, 0.001f)), "unbounded diagnostic duration rejected");
            report.AppendLine("# complete; checks=" + checks + "; result=PASS; no physics, profile or streaming mutation");
        }
        finally { File.WriteAllText(summaryPath, report.ToString()); }
    }

    private static void WriteR2OllamaPolicyRegression(string summaryPath)
    {
        // note: Exercise the actual payload policy on detached dictionaries and config; never start a server or mutate live ownership.
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        config.hideFlags = HideFlags.HideAndDontSave;
        var results = new StringBuilder("# Edit-only Ollama request policy; utc=" + DateTime.UtcNow.ToString("O") +
            "; assemblyMvid=" + typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId + "\n");
        results.AppendLine("runtimeDefault\texclusive\treleased\tresponsive\texplicitGpu\tresult");
        try
        {
            MethodInfo apply = typeof(LLMClient).GetMethod("ApplyOllamaLivePresentationPolicy", BindingFlags.NonPublic | BindingFlags.Static);
            if (apply == null) throw new MissingMethodException("Ollama live request policy unavailable");
            int cases = 0;
            foreach (bool runtimeDefault in new[] { false, true })
            foreach (bool exclusive in new[] { false, true })
            foreach (bool released in new[] { false, true })
            foreach (bool responsive in new[] { false, true })
            foreach (int? explicitGpu in new int?[] { null, -1, 0, 6 })
            {
                config.preserveGameResponsiveness = responsive;
                string configBefore = Newtonsoft.Json.JsonConvert.SerializeObject(config.generationProfiles);
                string qualificationBefore = config.GoddessSpeechPlanContractHash;
                var original = new Dictionary<string, object> {
                    { "num_ctx", 12288 }, { "num_predict", 600 }, { "temperature", 0.28f },
                    { "num_batch", 128 }, { "num_thread", 4 }, { "stop", new[] { "END" } }
                };
                if (explicitGpu.HasValue) original["num_gpu"] = explicitGpu.Value;
                string originalJson = Newtonsoft.Json.JsonConvert.SerializeObject(original);
                var actual = new Dictionary<string, object>(original);
                apply.Invoke(null, new object[] { config, actual, runtimeDefault, exclusive, released });
                var expected = Newtonsoft.Json.Linq.JObject.Parse(originalJson);
                if (runtimeDefault && !exclusive && released && responsive && !explicitGpu.HasValue)
                    expected["num_gpu"] = 0;
                // note: Compare wire representations on both sides so float-to-double token conversion cannot create a fixture-only mismatch.
                var actualJson = Newtonsoft.Json.Linq.JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(actual));
                bool pass = Newtonsoft.Json.Linq.JToken.DeepEquals(expected, actualJson) &&
                    originalJson == Newtonsoft.Json.JsonConvert.SerializeObject(original) &&
                    configBefore == Newtonsoft.Json.JsonConvert.SerializeObject(config.generationProfiles) &&
                    qualificationBefore == config.GoddessSpeechPlanContractHash;
                results.AppendLine(runtimeDefault + "\t" + exclusive + "\t" + released + "\t" + responsive + "\t" +
                    (explicitGpu.HasValue ? explicitGpu.Value.ToString() : "absent") + "\t" + (pass ? "PASS" : "FAIL"));
                if (!pass) throw new InvalidOperationException("Ollama request policy changed an explicit option or configuration");
                cases++;
            }
            results.AppendLine("# complete; cases=" + cases + "; result=PASS; no model request, save mutation or live owner change");
        }
        catch (Exception exception)
        {
            results.AppendLine("# FAIL: " + exception);
            throw;
        }
        finally
        {
            File.WriteAllText(summaryPath, results.ToString());
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void WriteR2LlamaLaunchRegression(string summaryPath)
    {
        // note: Test the real argument builder against installed capabilities without starting a server or modifying a serialized config.
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        config.hideFlags = HideFlags.HideAndDontSave;
        try
        {
            string help = File.ReadAllText("outputs/G08_R2_LlamaHelp_20260930.txt");
            MethodInfo build = typeof(LlamaCppServerProcess).GetMethod("TryBuildArguments", BindingFlags.NonPublic | BindingFlags.Static);
            if (build == null) throw new MissingMethodException("Llama launch argument builder unavailable");
            var results = new List<string> { "# Edit-only LLM launch regression; utc=" + DateTime.UtcNow.ToString("O") +
                "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
                "live\tresponsive\tlayers\tcustom\texpectedCpu\tresult" };
            string gameplayArguments = string.Empty;
            int cases = 0;
            foreach (bool live in new[] { false, true })
            foreach (bool responsive in new[] { false, true })
            foreach (int layers in new[] { -1, 0, 6 })
            foreach (bool custom in new[] { false, true })
            {
                config.preserveGameResponsiveness = responsive;
                config.gpuLayerCount = layers;
                config.extraLlamaServerArguments = custom ? "--seed 42" : string.Empty;
                string before = JsonUtility.ToJson(config);
                object[] arguments = { config, help, null, null, live };
                if (!(bool)build.Invoke(null, arguments)) throw new InvalidOperationException((string)arguments[3]);
                string command = (string)arguments[2];
                bool cpu = live && responsive && layers < 0 && !custom;
                if (command.Contains("--device \"none\"") != cpu || command.Contains("--fit off") != cpu ||
                    command.Contains("--no-op-offload") != cpu || command.Contains("--no-kv-offload") != cpu ||
                    command.Contains("--fit-target") == cpu || JsonUtility.ToJson(config) != before ||
                    !command.Contains(config.ggufModelPath) || !command.Contains("--ctx-size \"12288\"") ||
                    !command.Contains("--parallel \"1\""))
                    throw new InvalidOperationException("LLM launch policy or preserved config mismatch");
                if (cpu) gameplayArguments = command;
                results.Add(live + "\t" + responsive + "\t" + layers + "\t" + custom + "\t" + cpu + "\tPASS");
                cases++;
            }
            config.preserveGameResponsiveness = true;
            config.gpuLayerCount = -1;
            config.extraLlamaServerArguments = string.Empty;
            object[] unsupported = { config, "--model --host --port", null, null, true };
            if ((bool)build.Invoke(null, unsupported)) throw new InvalidOperationException("Unsupported CPU residency policy did not fail explicitly");
            results.Add("# PASS cases=" + cases + "; unsupportedCapabilitiesRejected=True; serializedConfigUnchanged=True");
            results.Add("# gameplayArguments=" + gameplayArguments);
            File.WriteAllLines(summaryPath, results);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(config);
        }
    }

    private static void WriteR2MarkerPollProbe(string summaryPath)
    {
        // note: Inspect only the existing hook's named request paths; never create, delete or dispatch a gameplay request in this probe.
        Type hook = Type.GetType("YQEditorAutoRefreshBootstrap, Assembly-CSharp-Editor", true);
        var relative = new List<string>();
        var absolute = new List<string>();
        foreach (FieldInfo field in hook.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            if (field.FieldType != typeof(string) || !field.Name.EndsWith("Marker", StringComparison.Ordinal)) continue;
            string path = (string)field.GetValue(null);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".request", StringComparison.Ordinal)) continue;
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath)) throw new InvalidOperationException("Marker poll probe requires no pending hook requests: " + field.Name);
            relative.Add(Path.IsPathRooted(path) ? Path.GetRelativePath(Directory.GetCurrentDirectory(), path) : path);
            absolute.Add(fullPath);
        }
        if (relative.Count == 0) throw new InvalidOperationException("No hook request paths found");
        long calibrationStart = GC.GetAllocatedBytesForCurrentThread();
        var calibration = new byte[4096];
        long calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - calibrationStart;
        GC.KeepAlive(calibration);
        var output = new StringBuilder("mode\tpaths\trounds\tpresent\tallocatedBytes\telapsedMs\tcounterCalibrated\n");
        const int rounds = 64;
        var timer = new System.Diagnostics.Stopwatch();
        var markers = new[] { new ProfilerMarker("YQR2MarkerPoll.Relative"), new ProfilerMarker("YQR2MarkerPoll.Absolute"), new ProfilerMarker("YQR2MarkerPoll.PendingCensus") };
        MethodInfo censusMethod = hook.GetMethod("HasPendingVerificationRequest", BindingFlags.Static | BindingFlags.NonPublic);
        var census = (Func<string, string, bool>)Delegate.CreateDelegate(typeof(Func<string, string, bool>), censusMethod);
        const string externalRequest = "Assets/Assets/EditorBuildRequests/RunContinuousTerrainDeterminismTests.request";
        // note: Warm both File.Exists code paths once outside the measured comparison; this is an isolated Edit fixture, never gameplay preparation.
        foreach (string path in relative) File.Exists(path);
        foreach (string path in absolute) File.Exists(path);
        census("Temp", externalRequest);
        for (int mode = 0; mode < 3; mode++)
        {
            List<string> paths = mode == 0 ? relative : absolute;
            int present = 0;
            long before = GC.GetAllocatedBytesForCurrentThread();
            timer.Restart();
            using (markers[mode].Auto())
                for (int round = 0; round < rounds; round++)
                    if (mode == 2)
                    {
                        if (census("Temp", externalRequest)) present++;
                    }
                    else
                    {
                        for (int index = 0; index < paths.Count; index++)
                            if (File.Exists(paths[index])) present++;
                    }
            timer.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            output.Append(mode == 0 ? "relative" : mode == 1 ? "absolute" : "pendingCensus").Append('\t').Append(paths.Count).Append('\t').Append(rounds)
                .Append('\t').Append(present).Append('\t').Append(bytes).Append('\t')
                .Append(timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture)).Append('\t')
                .Append(calibrationBytes >= 4096).AppendLine();
        }
        // note: Check request detection on a fresh disposable directory outside Temp; no real editor request can be consumed by these cases.
        string fixtureRoot = Path.GetFullPath(Path.Combine("outputs", "G08_R2_MarkerFixture_" + Guid.NewGuid().ToString("N")));
        string inbox = Path.Combine(fixtureRoot, "inbox");
        string outside = Path.Combine(fixtureRoot, "external.request");
        string noise = Path.Combine(inbox, "unrelated.txt");
        string marker = Path.Combine(inbox, "YQ_PROBE.request");
        string lower = Path.Combine(inbox, "yq_probe.request");
        Directory.CreateDirectory(inbox);
        int checks = 0;
        void Check(bool expected)
        {
            bool actual = census(inbox, outside);
            checks++;
            if (actual != expected) throw new InvalidOperationException("Request census mismatch at fixture case " + checks);
        }
        try
        {
            Check(false);
            File.WriteAllText(noise, "fixture"); Check(false);
            File.WriteAllText(marker, "fixture"); Check(true);
            File.Delete(marker); Check(false);
            File.Move(noise, marker); Check(true);
            File.Move(marker, noise); Check(false);
            File.WriteAllText(lower, "fixture"); Check(true);
            File.Delete(lower); Check(false);
            File.WriteAllText(outside, "fixture"); Check(true);
            File.Delete(outside); Check(false);
            File.Delete(noise); Directory.Delete(inbox); Check(false);
            File.WriteAllText(outside, "fixture"); Check(true);
        }
        finally
        {
            foreach (string file in new[] { marker, lower, noise, outside }) if (File.Exists(file)) File.Delete(file);
            if (Directory.Exists(inbox)) Directory.Delete(inbox);
            Directory.Delete(fixtureRoot);
        }
        output.AppendLine("# requestDetectionChecks=" + checks + " result=PASS");
        output.AppendLine("# diagnosticOnly=true; identical paths; no gameplay or profile mutation; timing includes binary profiler overhead");
        File.WriteAllText(summaryPath, output.ToString());
    }

    private static void WriteR2TerrainAllocationRegression(string summaryPath)
    {
        // note: This immutable workspace copy is the accepted disposable world; no save is loaded, normalized, committed or regenerated.
        const string fixturePath = "outputs/G08_R2_AcceptedTerrainAllocationFixture_20261001.json";
        string fixtureJson = File.ReadAllText(fixturePath);
        WorldState world = JsonUtility.FromJson<WorldState>(fixtureJson);
        GeneratedWorldPlanRecord plan = world.generatedWorldPlan;
        if (plan.worldSeed != "4bb221dc" || plan.spatialPlanV2.contentHash != "c1d6ce9d3e06f760" ||
            !YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var sampler, out _))
            throw new InvalidOperationException("Allocation probe requires the unchanged accepted D21 fixture");

        // note: Preserve the params-overload comparison semantics even for non-finite values and signed zero; ordinary finite terrain samples alone cannot prove this.
        MethodInfo maximum = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("MaximumOfThree", BindingFlags.NonPublic | BindingFlags.Static);
        float[] maximumCases = { float.NegativeInfinity, -float.MaxValue, -1f, -0f, 0f, 1f, float.MaxValue, float.PositiveInfinity, float.NaN };
        int maximumChecked = 0;
        if (maximum == null) throw new InvalidOperationException("Allocation regression requires the production maximum helper");
        foreach (float first in maximumCases)
            foreach (float second in maximumCases)
                foreach (float third in maximumCases)
                {
                    float expected = Mathf.Max(new[] { first, second, third });
                    float actual = (float)maximum.Invoke(null, new object[] { first, second, third });
                    if (BitConverter.ToInt32(BitConverter.GetBytes(expected), 0) != BitConverter.ToInt32(BitConverter.GetBytes(actual), 0))
                        throw new InvalidOperationException("Allocation-free maximum changed params comparison output bits");
                    maximumChecked++;
                }

        const int resolution = 65;
        // note: Unity's GC backend can expose an unsupported allocation counter; a retained known allocation calibrates this metric before interpreting zero.
        long calibrationBefore = GC.GetAllocatedBytesForCurrentThread();
        byte[] calibration;
        using (new ProfilerMarker("YQ.R2.EditAllocation.Calibration").Auto()) calibration = new byte[65536];
        long calibrationBytes = GC.GetAllocatedBytesForCurrentThread() - calibrationBefore;
        GC.KeepAlive(calibration);
        int[] cellX = { -4, -3, 10, 11 };
        int[] cellZ = { -2, -2, 9, 9 };
        var results = new YQSpatialTerrainSampleV2[cellX.Length * resolution * resolution];
        sampler.Sample(0f, 0f);
        var timer = new System.Diagnostics.Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        timer.Start();
        int cursor = 0;
        using (new ProfilerMarker("YQ.R2.EditAllocation.BlueprintSamples").Auto())
        {
        for (int cell = 0; cell < cellX.Length; cell++)
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                    results[cursor++] = sampler.Sample(cellX[cell] * 128f + x * 2f, cellZ[cell] * 128f + z * 2f);
        }
        timer.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // note: Exercise the actual worker's authority/session wrapper separately from the raw blueprint sampler; cache construction belongs to this measured cost.
        if (!YQGeneratedWorldTerrain.TryCreateV2HeightSampler(plan, out var heightSampler, out string heightFailure))
            throw new InvalidOperationException(heightFailure);
        var authority = new YQContinuousWorldCellAuthority(plan.worldSeed, null, heightSampler, plan);
        float[] heightResults = new float[results.Length];
        var heightTimer = new System.Diagnostics.Stopwatch();
        long heightBefore = GC.GetAllocatedBytesForCurrentThread();
        heightTimer.Start();
        cursor = 0;
        int[] spatialEvaluations = new int[cellX.Length];
        int[] spatialCacheHits = new int[cellX.Length];
        int[] spatialCacheCounts = new int[cellX.Length];
        using (new ProfilerMarker("YQ.R2.EditAllocation.HeightSessions").Auto())
        {
            for (int cell = 0; cell < cellX.Length; cell++)
            using (var session = authority.BeginHeightmapSamplingSession())
            {
                for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                        heightResults[cursor++] = session.SampleHeightNormalized(
                            cellX[cell] * 128f + x * 2f, cellZ[cell] * 128f + z * 2f);
                spatialEvaluations[cell] = session.SpatialSampleEvaluations;
                spatialCacheHits[cell] = session.SpatialSampleCacheHits;
                spatialCacheCounts[cell] = session.CachedSpatialSampleCount;
            }
        }
        heightTimer.Stop();
        long heightAllocated = GC.GetAllocatedBytesForCurrentThread() - heightBefore;

        // note: Repeat identical jobs only in the Edit probe to prove cleared leases preserve hit/evaluation counts and avoid dictionary storage allocation.
        float[] repeatedHeightResults = new float[heightResults.Length];
        cursor = 0;
        using (new ProfilerMarker("YQ.R2.EditAllocation.HeightSessionsRepeat").Auto())
        {
            for (int cell = 0; cell < cellX.Length; cell++)
            using (var session = authority.BeginHeightmapSamplingSession())
            {
                if (session.CachedSpatialSampleCount != 0 || session.SpatialSampleEvaluations != 0 || session.SpatialSampleCacheHits != 0)
                    throw new InvalidOperationException("A sampling lease retained prior-job values or counters");
                for (int z = 0; z < resolution; z++)
                    for (int x = 0; x < resolution; x++)
                        repeatedHeightResults[cursor++] = session.SampleHeightNormalized(
                            cellX[cell] * 128f + x * 2f, cellZ[cell] * 128f + z * 2f);
                if (session.SpatialSampleEvaluations != spatialEvaluations[cell] || session.SpatialSampleCacheHits != spatialCacheHits[cell] ||
                    session.CachedSpatialSampleCount != spatialCacheCounts[cell] || session.CachedSpatialSampleCount > 8192)
                    throw new InvalidOperationException("Reusing empty cache storage changed sampling or its entry limit");
            }
        }
        int bufferLeaseCases = VerifyR2SpatialSampleBufferLeases(heightSampler);

        // note: Store every output bit outside the measured loop; adjacent grids include identical seam coordinates in both traversal orders.
        string samplesPath = Path.ChangeExtension(summaryPath, ".samples.bin");
        using (var writer = new BinaryWriter(File.Create(samplesPath)))
            foreach (var value in results)
            {
                writer.Write(value.elevationNormalized); writer.Write(value.ruggedness);
                writer.Write(value.waterMask); writer.Write(value.waterSurfaceNormalized);
                writer.Write(value.waterFeatureIndex); writer.Write(value.bankSupportMask);
                writer.Write(value.routeMask); writer.Write(value.siteReserveMask); writer.Write(value.caveMassMask);
            }
        using (var writer = new BinaryWriter(File.Create(Path.ChangeExtension(summaryPath, ".heights.bin"))))
            foreach (float height in heightResults) writer.Write(height);
        string repeatedHeightsPath = Path.ChangeExtension(summaryPath, ".repeatedheights.bin");
        using (var writer = new BinaryWriter(File.Create(repeatedHeightsPath)))
            foreach (float height in repeatedHeightResults) writer.Write(height);
        bool repeatedHeightBitsEqual = R2FilesHaveEqualBytes(Path.ChangeExtension(summaryPath, ".heights.bin"), repeatedHeightsPath);
        // note: Compare every captured field and authority height against the immutable pre-repair native probe, rather than compare rounded aggregates.
        string beforeBase = "Logs/G08_R2_D22_NativeAllocation_Before_20261001.terrainallocation";
        bool sampleBitsEqual = R2FilesHaveEqualBytes(beforeBase + ".samples.bin", samplesPath);
        bool heightBitsEqual = R2FilesHaveEqualBytes(beforeBase + ".heights.bin", Path.ChangeExtension(summaryPath, ".heights.bin"));
        int seamMismatches = 0;
        for (int pair = 0; pair < cellX.Length; pair += 2)
            for (int z = 0; z < resolution; z++)
                if (!results[pair * resolution * resolution + z * resolution + 64].Equals(
                    results[(pair + 1) * resolution * resolution + z * resolution])) seamMismatches++;
        File.WriteAllLines(summaryPath, new[] {
            "# Edit-only accepted terrain sampler; utc=" + DateTime.UtcNow.ToString("O") +
                "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
            "seed\tcontentHash\tsamples\tallocatedBytes\telapsedMs\tseamMismatches\tfixtureSha256\tsamplesSha256\theightAllocatedBytes\theightElapsedMs\tknown65536ByteAllocationCounter\tallocationMetricValid\tmaximumCases\tsampleBitsEqual\theightBitsEqual\tspatialEvaluations\tspatialCacheHits\tspatialCacheCounts\trepeatedHeightBitsEqual\tbufferLeaseCases\tretainedEmptyBuffers",
            plan.worldSeed + "\t" + plan.spatialPlanV2.contentHash + "\t" + cursor + "\t" + allocated + "\t" +
                timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) + "\t" + seamMismatches + "\t" +
                YQStateContract.Sha256Hex(fixtureJson) + "\t" + BitConverter.ToString(SHA256.Create().ComputeHash(File.ReadAllBytes(samplesPath))).Replace("-", "") +
                "\t" + heightAllocated + "\t" + heightTimer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                "\t" + calibrationBytes + "\t" + (calibrationBytes >= 65536) + "\t" + maximumChecked + "\t" + sampleBitsEqual + "\t" + heightBitsEqual +
                "\t" + string.Join(",", spatialEvaluations) + "\t" + string.Join(",", spatialCacheHits) + "\t" + string.Join(",", spatialCacheCounts) +
                "\t" + repeatedHeightBitsEqual + "\t" + bufferLeaseCases + "\t" + heightSampler.RetainedSpatialSampleBufferCount
        });
        if (seamMismatches != 0 || !sampleBitsEqual || !heightBitsEqual || !repeatedHeightBitsEqual)
            throw new InvalidOperationException("Accepted terrain sampling changed output bits or an adjacent-cell seam");
    }

    private static int VerifyR2SpatialSampleBufferLeases(YQGeneratedWorldTerrain.V2HeightSampler sampler)
    {
        // note: These transient leases exercise reuse, exclusive ownership, overflow cleanup and fault cleanup without creating terrain or touching saves.
        var sessions = new YQGeneratedWorldTerrain.V2HeightSampler.SpatialSamplingSession[12];
        var identities = new HashSet<object>();
        int cases = 0;
        try
        {
            for (int i = 0; i < sessions.Length; i++)
            {
                sessions[i] = sampler.BeginSpatialSamplingSession();
                if (sessions[i].CachedSpatialSampleCount != 0 || !identities.Add(sessions[i].SampleBufferIdentity))
                    throw new InvalidOperationException("Concurrent sampling leases share storage or retained entries");
                sessions[i].SampleNormalized(100f + i * 2f, 200f);
                cases++;
            }
        }
        finally
        {
            foreach (var session in sessions) session?.Dispose();
        }
        if (sampler.RetainedSpatialSampleBufferCount != 8)
            throw new InvalidOperationException("Empty sampling storage exceeded or failed its bounded retention");
        foreach (var session in sessions) session.Dispose();
        if (sampler.RetainedSpatialSampleBufferCount != 8)
            throw new InvalidOperationException("Repeated disposal returned a buffer twice");
        cases += 2;

        // note: Hold eight actual worker leases together before sampling to exercise the pool lock and exclusive ownership under production-width concurrency.
        using (var ready = new System.Threading.CountdownEvent(8))
        using (var release = new System.Threading.ManualResetEventSlim(false))
        {
            object[] workerIdentities = new object[8];
            float[,] expected = new float[8, 64];
            for (int worker = 0; worker < 8; worker++)
                for (int sample = 0; sample < 64; sample++)
                    expected[worker, sample] = sampler.SampleNormalized(200f + worker * 128f + sample * 2f, 300f);
            var tasks = new System.Threading.Tasks.Task[8];
            for (int worker = 0; worker < tasks.Length; worker++)
            {
                int workerIndex = worker;
                tasks[worker] = System.Threading.Tasks.Task.Run(() =>
                {
                    using (var session = sampler.BeginSpatialSamplingSession())
                    {
                        workerIdentities[workerIndex] = session.SampleBufferIdentity;
                        ready.Signal();
                        release.Wait();
                        for (int sample = 0; sample < 64; sample++)
                        {
                            float actual = session.SampleNormalized(200f + workerIndex * 128f + sample * 2f, 300f);
                            if (BitConverter.ToInt32(BitConverter.GetBytes(actual), 0) !=
                                BitConverter.ToInt32(BitConverter.GetBytes(expected[workerIndex, sample]), 0))
                                throw new InvalidOperationException("Concurrent cache reuse changed height bits");
                        }
                    }
                });
            }
            bool allReady = false;
            try
            {
                allReady = ready.Wait(TimeSpan.FromSeconds(10));
                if (allReady && new HashSet<object>(workerIdentities).Count != workerIdentities.Length)
                    throw new InvalidOperationException("Parallel workers acquired the same sample buffer");
            }
            finally
            {
                release.Set();
                System.Threading.Tasks.Task.WaitAll(tasks);
            }
            if (!allReady || sampler.RetainedSpatialSampleBufferCount != 8)
                throw new InvalidOperationException("Parallel sampling leases failed to complete or return bounded storage");
            cases += 9;
        }

        // note: A deliberate fault unwinds through the same using/finally lifetime as canceled production height jobs.
        object identity = null;
        try
        {
            using (var session = sampler.BeginSpatialSamplingSession())
            {
                identity = session.SampleBufferIdentity;
                session.SampleNormalized(150f, 200f);
                throw new OperationCanceledException("Edit-only cancellation cleanup probe");
            }
        }
        catch (OperationCanceledException) { cases++; }
        using (var session = sampler.BeginSpatialSamplingSession())
        {
            if (session.CachedSpatialSampleCount != 0 || !ReferenceEquals(identity, session.SampleBufferIdentity))
                throw new InvalidOperationException("Fault cleanup retained cached samples");
            session.SampleNormalized(150f, 200f);
            session.Dispose();
            try { session.SampleNormalized(150f, 200f); }
            catch (ObjectDisposedException) { cases++; return cases; }
        }
        throw new InvalidOperationException("Disposed sampling lease remained usable");
    }

    private static bool R2FilesHaveEqualBytes(string firstPath, string secondPath)
    {
        // note: This Edit-only comparison runs after the measured loops and never normalizes a persisted document.
        byte[] first = File.ReadAllBytes(firstPath), second = File.ReadAllBytes(secondPath);
        if (first.Length != second.Length) return false;
        for (int i = 0; i < first.Length; i++) if (first[i] != second[i]) return false;
        return true;
    }


    private static void WriteR2BlueprintHashRegression(string summaryPath)
    {
        // note: This detached Edit fixture compares the exact frozen character-stream implementation; no live world/profile owner is loaded or replaced.
        const string fixturePath = "outputs/G08_R2_AcceptedTerrainAllocationFixture_20261001.json";
        string input = File.ReadAllText(fixturePath);
        var settings = new Newtonsoft.Json.JsonSerializerSettings {
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        };
        WorldState world = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(input, settings);
        GeneratedSpatialWorldPlanV2Record artifact = world?.generatedWorldPlan?.spatialPlanV2;
        if (world?.generatedWorldPlan?.worldSeed != "4bb221dc" || artifact?.contentHash != "c1d6ce9d3e06f760")
            throw new InvalidOperationException("Hash fixture requires the unchanged accepted artifact");
        string before = Newtonsoft.Json.JsonConvert.SerializeObject(artifact, settings);
        var playerOwner = PlayerStateManager.Instance;
        var worldOwner = WorldStateManager.Instance;
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        int cases = 0;
        var results = new List<string> {
            "# utc=" + DateTime.UtcNow.ToString("O") + " assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
            " fixture=" + fixturePath + " seed=4bb221dc V2=c1d6ce9d3e06f760 frozenHasherSha=1043B230105DFEF5FF68D8A04183BE5C5751C02DA8C36B7CA0F089AE63F0272F",
            "scope\titerations\ttotalMs\tmeanMs"
        };
        Action<GeneratedSpatialWorldPlanV2Record> parity = record => {
            string expected = R2OriginalBlueprintHasher.ComputeContentHashReadOnly(record);
            if (YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(record) != expected)
                throw new InvalidOperationException("Canonical hash mismatch at case " + cases + " culture=" + CultureInfo.CurrentCulture.Name);
            cases++;
        };
        try
        {
            foreach (CultureInfo culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("en-US"), CultureInfo.GetCultureInfo("fr-FR"), CultureInfo.GetCultureInfo("ar-SA") })
            {
                CultureInfo.CurrentCulture = culture;
                parity(null); parity(new GeneratedSpatialWorldPlanV2Record { blueprint = null }); parity(artifact);
                // note: Tiny artifacts isolate float round-trip tokens, signed enum integers, nulls and UTF-16 delimiters without thousands of whole-world copies.
                var tiny = new GeneratedSpatialWorldPlanV2Record { schemaVersion = null, worldSeed = "x:|\ud800\udfff", blueprint = new YQSpatialBlueprintV2() };
                // note: Exercise canonical token length transitions against the frozen culture-sensitive writer, including empty and long UTF-16 tokens.
                foreach (int length in new[] { 0, 1, 9, 10, 11, 99, 100, 999, 1000, 10000 })
                {
                    tiny.worldSeed = new string('x', length);
                    parity(tiny);
                }
                tiny.worldSeed = "x:|\ud800\udfff";
                var field = new YQTerrainFieldV2 { fieldId = "signed-enum", tags = new List<string> { null, "x", "|:" } };
                tiny.blueprint.terrainFields.Add(field);
                // note: Exercise scratch-buffer boundaries, duplicate/null tags, signed functions, and alternating sizes against the frozen hasher without changing accepted records.
                var scratchSite = new YQSiteAnchorV2 { siteId = "scratch-parity" };
                tiny.blueprint.sites.Add(scratchSite);
                foreach (int count in new[] { 0, 1, 2, 3, 17, 65, 2, 1, 0 })
                {
                    field.tags = new List<string>();
                    scratchSite.requiredFunctions = new List<YQAssetFunctionV2>();
                    for (int index = 0; index < count; index++)
                    {
                        field.tags.Add(index % 4 == 0 ? null : index % 4 == 1 ? "|:雪" : index % 4 == 2 ? "a" : "A");
                        scratchSite.requiredFunctions.Add((YQAssetFunctionV2)(index % 3 - 1));
                    }
                    string untouched = Newtonsoft.Json.JsonConvert.SerializeObject(tiny, settings);
                    parity(tiny);
                    if (untouched != Newtonsoft.Json.JsonConvert.SerializeObject(tiny, settings))
                        throw new InvalidOperationException("Hash scratch storage mutated source collections");
                    field.tags.Reverse(); scratchSite.requiredFunctions.Reverse(); parity(tiny);
                }
                field.tags = null; scratchSite.requiredFunctions = null; parity(tiny);
                tiny.blueprint.sites.Clear();
                field.tags = new List<string> { null, "x", "|:" };
                foreach (float value in new[] { 0f, -0f, float.Epsilon, -float.Epsilon, float.MinValue, float.MaxValue, float.NaN, float.PositiveInfinity, float.NegativeInfinity, 1f / 3f, 1e-20f, 1e20f })
                { tiny.blueprint.worldSize = value; parity(tiny); }
                var random = new System.Random(11029);
                for (int i = 0; i < 2048; i++)
                {
                    tiny.blueprint.worldSize = BitConverter.ToSingle(BitConverter.GetBytes(random.Next(int.MinValue, int.MaxValue)), 0);
                    field.kind = (YQTerrainFieldKindV2)random.Next(int.MinValue, int.MaxValue);
                    parity(tiny);
                }
                foreach (int value in new[] { int.MinValue, -1, 0, 1, int.MaxValue })
                { field.kind = (YQTerrainFieldKindV2)value; parity(tiny); }
                var reordered = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialWorldPlanV2Record>(before, settings);
                reordered.blueprint.regions.Reverse(); reordered.blueprint.terrainFields.Reverse(); reordered.blueprint.hydrology.Reverse();
                reordered.blueprint.sites.Reverse(); reordered.blueprint.routes.Reverse(); reordered.blueprint.relationships.Reverse();
                parity(reordered);
                if (YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(reordered) != YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact))
                    throw new InvalidOperationException("Reordered unique-ID blueprint changed its hash");
                reordered.blueprint.terrainFields.Add(null); parity(reordered);
                reordered.blueprint.regions = null; reordered.blueprint.hydrology = null; parity(reordered);
                float original = artifact.blueprint.worldSize;
                string acceptedHash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact);
                artifact.blueprint.worldSize += 1f;
                parity(artifact);
                if (YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact) == acceptedHash)
                    throw new InvalidOperationException("Changed content retained stale hash");
                artifact.blueprint.worldSize = original;
            }
            CultureInfo.CurrentCulture = previousCulture;
            if (YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(artifact) != artifact.contentHash)
                throw new InvalidOperationException("Accepted fixture hash changed");
            Action<string, Func<GeneratedSpatialWorldPlanV2Record, string>> measure = (name, compute) => {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                string last = null;
                using (new ProfilerMarker("YQ.R2.BlueprintHash." + name).Auto())
                    for (int repeat = 0; repeat < 8; repeat++) last = compute(artifact);
                clock.Stop();
                if (last != artifact.contentHash) throw new InvalidOperationException("Measured accepted hash changed");
                results.Add(name + "\t8\t" + clock.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture) +
                    "\t" + (clock.Elapsed.TotalMilliseconds / 8d).ToString("F6", CultureInfo.InvariantCulture));
            };
            measure("Reference", R2OriginalBlueprintHasher.ComputeContentHashReadOnly);
            measure("Current", YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly);
            if (before != Newtonsoft.Json.JsonConvert.SerializeObject(artifact, settings) || input != File.ReadAllText(fixturePath) ||
                !ReferenceEquals(playerOwner, PlayerStateManager.Instance) || !ReferenceEquals(worldOwner, WorldStateManager.Instance))
                throw new InvalidOperationException("Read-only hash fixture changed document or ownership");
            results.Add("# PASS parityCases=" + cases + " acceptedHash=c1d6ce9d3e06f760 mutationDetected=True readOnly=True singletonOwnersUnchanged=True");
            File.WriteAllLines(summaryPath, results);
        }
        finally { CultureInfo.CurrentCulture = previousCulture; }
    }

    // note: Frozen D29 hasher is a diagnostic reference only, compiled exclusively in the existing UNITY_EDITOR block.
private static class R2OriginalBlueprintHasher
{
    public static string ComputeContentHash(
        GeneratedSpatialWorldPlanV2Record record)
    {
        if (record == null || record.blueprint == null)
            return string.Empty;

        record.EnsureCollections();
        return ComputeContentHashReadOnly(record);
    }

    public static string ComputeContentHashReadOnly(GeneratedSpatialWorldPlanV2Record record)
    {
        // note: Runtime integrity checks treat missing collections as empty without normalizing accepted data or cloning its full JSON graph.
        if (record == null || record.blueprint == null)
            return string.Empty;
        // note: Stream the exact canonical character sequence into FNV so per-cell authority checks avoid building and copying a large temporary string.
        StableHashWriter writer = new StableHashWriter();
        Append(writer, record.schemaVersion);
        Append(writer, record.generationVersion);
        Append(writer, record.worldSeed);
        Append(writer, record.semanticFingerprint);
        Append(writer, record.blueprint.worldSize);
        Append(writer, record.blueprint.originSiteId);

        AppendRegions(writer, record.blueprint.regions);
        AppendTerrain(writer, record.blueprint.terrainFields);
        AppendHydrology(writer, record.blueprint.hydrology);
        AppendSites(writer, record.blueprint.sites);
        AppendRoutes(writer, record.blueprint.routes);
        AppendRelationships(writer, record.blueprint.relationships);
        return writer.ToHashString();
    }

    private static void AppendRegions(
        StableHashWriter builder,
        IReadOnlyList<YQRegionDomainV2> values)
    {
        List<YQRegionDomainV2> sorted = SortById(
            values,
            value => value != null ? value.regionId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQRegionDomainV2 value = sorted[index];
            Append(builder, value.regionId);
            Append(builder, value.centerX);
            Append(builder, value.centerZ);
            Append(builder, value.radius);
            Append(builder, value.elevationBias);
            Append(builder, value.moisture);
            Append(builder, value.ruggedness);
            Append(builder, value.civilizationDensity);
            Append(builder, value.danger);
            AppendStrings(builder, value.biomeTags);
        }
    }

    private static void AppendTerrain(
        StableHashWriter builder,
        IReadOnlyList<YQTerrainFieldV2> values)
    {
        List<YQTerrainFieldV2> sorted = SortById(
            values,
            value => value != null ? value.fieldId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQTerrainFieldV2 value = sorted[index];
            Append(builder, value.fieldId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.kind);
            Append(builder, value.strength);
            Append(builder, value.radius);
            Append(builder, value.falloff);
            AppendPoints(builder, value.controlPoints);
            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendHydrology(
        StableHashWriter builder,
        IReadOnlyList<YQHydrologyFeatureV2> values)
    {
        List<YQHydrologyFeatureV2> sorted = SortById(
            values,
            value => value != null ? value.hydrologyId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQHydrologyFeatureV2 value = sorted[index];
            Append(builder, value.hydrologyId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.kind);
            Append(builder, value.sourceTerrainFieldId);
            Append(builder, value.sinkHydrologyId);
            Append(builder, value.waterLevelNormalized);
            Append(builder, value.nominalWidth);
            Append(builder, value.nominalDepth);
            AppendPoints(builder, value.controlPoints);
            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendSites(
        StableHashWriter builder,
        IReadOnlyList<YQSiteAnchorV2> values)
    {
        List<YQSiteAnchorV2> sorted = SortById(
            values,
            value => value != null ? value.siteId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQSiteAnchorV2 value = sorted[index];
            Append(builder, value.siteId);
            Append(builder, value.sourceSemanticId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.kind);
            Append(builder, (int)value.placementMode);
            Append(builder, value.x);
            Append(builder, value.z);
            Append(builder, value.preferredHeadingDegrees);
            Append(builder, value.reservedRadius);
            Append(builder, value.terrainSearchRadius);
            Append(builder, value.maximumSlopeDegrees);
            Append(builder, value.minimumRouteAccess);
            Append(builder, value.minimumWaterAccess);
            Append(builder, value.requiresTerrainConformance ? 1 : 0);
            Append(builder, value.hiddenFromPrimaryRoute ? 1 : 0);

            List<int> functions = new List<int>();
            for (int functionIndex = 0;
                 functionIndex < (value.requiredFunctions != null ? value.requiredFunctions.Count : 0);
                 functionIndex++)
            {
                functions.Add((int)value.requiredFunctions[functionIndex]);
            }

            functions.Sort();
            for (int functionIndex = 0;
                 functionIndex < functions.Count;
                 functionIndex++)
            {
                Append(builder, functions[functionIndex]);
            }

            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendRoutes(
        StableHashWriter builder,
        IReadOnlyList<YQRouteCorridorV2> values)
    {
        List<YQRouteCorridorV2> sorted = SortById(
            values,
            value => value != null ? value.routeId : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQRouteCorridorV2 value = sorted[index];
            Append(builder, value.routeId);
            Append(builder, value.sourceSemanticRouteId);
            Append(builder, value.fromSiteId);
            Append(builder, value.toSiteId);
            Append(builder, value.parentRegionId);
            Append(builder, (int)value.routeClass);
            Append(builder, value.width);
            Append(builder, value.shoulderWidth);
            Append(builder, value.maximumGradeDegrees);
            AppendPoints(builder, value.controlPoints);

            List<YQRouteCrossingV2> crossings = SortById(
                value.crossings,
                crossing => crossing != null
                    ? crossing.crossingId
                    : string.Empty);
            for (int crossingIndex = 0;
                 crossingIndex < crossings.Count;
                 crossingIndex++)
            {
                YQRouteCrossingV2 crossing = crossings[crossingIndex];
                Append(builder, crossing.crossingId);
                Append(builder, crossing.hydrologyId);
                Append(builder, (int)crossing.kind);
                Append(builder, crossing.x);
                Append(builder, crossing.z);
                Append(builder, crossing.requiredSpan);
            }

            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendRelationships(
        StableHashWriter builder,
        IReadOnlyList<YQSpatialRelationshipV2> values)
    {
        List<YQSpatialRelationshipV2> sorted = SortById(
            values,
            value => value != null
                ? value.relationshipId
                : string.Empty);
        for (int index = 0; index < sorted.Count; index++)
        {
            YQSpatialRelationshipV2 value = sorted[index];
            Append(builder, value.relationshipId);
            Append(builder, value.subjectId);
            Append(builder, value.objectId);
            Append(builder, (int)value.kind);
            Append(builder, value.minimumDistance);
            Append(builder, value.maximumDistance);
            Append(builder, value.required ? 1 : 0);
            AppendStrings(builder, value.tags);
        }
    }

    private static void AppendPoints(
        StableHashWriter builder,
        IReadOnlyList<YQBlueprintPointV2> points)
    {
        // note: This is the same canonical empty sequence produced by EnsureCollections, without writing into the accepted record.
        for (int index = 0; index < (points != null ? points.Count : 0); index++)
        {
            YQBlueprintPointV2 point = points[index];
            Append(builder, point.x);
            Append(builder, point.z);
            Append(builder, point.normalizedElevation);
            Append(builder, point.width);
        }
    }

    private static void AppendStrings(
        StableHashWriter builder,
        IReadOnlyList<string> values)
    {
        List<string> sorted = new List<string>();
        if (values != null)
        {
            for (int index = 0; index < values.Count; index++)
                sorted.Add(values[index] ?? string.Empty);
        }

        sorted.Sort(StringComparer.Ordinal);
        for (int index = 0; index < sorted.Count; index++)
            Append(builder, sorted[index]);
    }

    private static List<T> SortById<T>(
        IReadOnlyList<T> values,
        Func<T, string> idSelector)
        where T : class
    {
        List<T> sorted = new List<T>();
        if (values != null)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (values[index] != null)
                    sorted.Add(values[index]);
            }
        }

        sorted.Sort(
            (left, right) => string.CompareOrdinal(
                idSelector(left),
                idSelector(right)));
        return sorted;
    }

    private static void Append(StableHashWriter builder, string value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length);
        builder.Append(':');
        builder.Append(safe);
        builder.Append('|');
    }

    private static void Append(StableHashWriter builder, float value)
    {
        Append(builder, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static void Append(StableHashWriter builder, int value)
    {
        Append(builder, value.ToString(CultureInfo.InvariantCulture));
    }

    private sealed class StableHashWriter
    {
        private ulong _hash = 14695981039346656037UL;

        public StableHashWriter Append(int value)
        {
            // note: StringBuilder.Append(int) used the current culture for this nonnegative length prefix; retain its exact character sequence.
            Append(value.ToString(CultureInfo.CurrentCulture));
            return this;
        }

        public StableHashWriter Append(char value)
        {
            AppendCharacter(value);
            return this;
        }

        public StableHashWriter Append(string value)
        {
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
                AppendCharacter(text[index]);
            return this;
        }

        public string ToHashString()
        {
            return _hash.ToString("x16", CultureInfo.InvariantCulture);
        }

        private void AppendCharacter(char value)
        {
            // note: Preserve the existing stable FNV-1a character stream and output format exactly.
            unchecked
            {
                _hash ^= value;
                _hash *= 1099511628211UL;
            }
        }
    }
}

    private static void WriteR2LocationNormalizationProbe(string summaryPath)
    {
        // note: Inactive, unsaved managers exercise actual setters on a private accepted-world copy; Awake/save/profile services never run.
        const string fixturePath = "outputs/G08_R2_AcceptedTerrainAllocationFixture_20261001.json";
        var settings = new Newtonsoft.Json.JsonSerializerSettings
        {
            ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore,
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        };
        WorldState world = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(fixturePath), settings);
        if (world?.generatedWorldPlan?.worldSeed != "4bb221dc" ||
            world.generatedWorldPlan.spatialPlanV2?.contentHash != "c1d6ce9d3e06f760")
            throw new InvalidOperationException("Location probe requires the unchanged accepted fixture");
        // note: Read one checksum-matched disposable revision to distinguish ordinary state normalization from its autosave IO; never load it into live services.
        const string profileId = "3ccfff4df9a1479884061e51940e4457";
        string profilesRoot = Path.Combine(Application.persistentDataPath, "Profiles");
        string manifestPath = Path.Combine(profilesRoot, "profiles_manifest.json");
        string manifestJson = File.ReadAllText(manifestPath);
        var manifest = JsonUtility.FromJson<YQProfileSaveSystem.ProfileManifest>(manifestJson);
        YQProfileCommitRecord commit = null;
        foreach (YQProfileCommitRecord candidate in manifest.commits)
            if (candidate != null && candidate.profileId == profileId && (commit == null || candidate.revision > commit.revision))
                commit = candidate;
        if (commit == null || !YQProfileCommitStore.TryReadCommit(Path.Combine(profilesRoot, profileId),
                commit.commitId, commit.playerChecksum, commit.worldChecksum, out string playerPath, out string worldPath, out _))
            throw new InvalidOperationException("Location probe requires a checksum-matched disposable paired revision");
        string playerJson = File.ReadAllText(playerPath), currentWorldJson = File.ReadAllText(worldPath);
        WorldState currentWorld = Newtonsoft.Json.JsonConvert.DeserializeObject<WorldState>(currentWorldJson, settings);
        PlayerState player = Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerState>(playerJson, settings);
        if (player.playerId != profileId || currentWorld.generatedWorldPlan.worldSeed != "4bb221dc" ||
            currentWorld.generatedWorldPlan.spatialPlanV2.contentHash != "c1d6ce9d3e06f760")
            throw new InvalidOperationException("Location probe revision does not match the disposable accepted world");
        WorldStateManager originalWorldOwner = WorldStateManager.Instance;
        PlayerStateManager originalPlayerOwner = PlayerStateManager.Instance;
        GameObject fixture = new GameObject("YQ_R2_DetachedLocationProbe") { hideFlags = HideFlags.HideAndDontSave };
        fixture.SetActive(false);
        var results = new List<string> { "# utc=" + DateTime.UtcNow.ToString("O") +
            " assemblyMvid=" + typeof(WorldStateManager).Assembly.ManifestModule.ModuleVersionId +
            " fixture=" + fixturePath + " worldSeed=4bb221dc acceptedV2=c1d6ce9d3e06f760; Edit fixture only",
            "scope\titerations\ttotalMs\tmeanMs" };
        results.Add("# pairedRevision=" + commit.revision + " commit=" + commit.commitId +
            " playerChecksum=" + commit.playerChecksum + " worldChecksum=" + commit.worldChecksum);
        try
        {
            var worldManager = fixture.AddComponent<WorldStateManager>();
            var playerManager = fixture.AddComponent<PlayerStateManager>();
            playerManager.autosave = false;
            playerManager.state = new PlayerState();
            worldManager.ReplaceState(world);
            string region = world.currentRegionId;
            string acceptedBefore = Newtonsoft.Json.JsonConvert.SerializeObject(world.generatedWorldPlan, settings);
            long revisionBefore = world.stateRevision;
            Action<string, Action> measure = (name, action) =>
            {
                // note: Measure repeated already-normalized operations; construction, serialization and report writing stay outside the named scope.
                var clock = System.Diagnostics.Stopwatch.StartNew();
                using (new ProfilerMarker("YQ.R2.LocationNormalization." + name).Auto())
                    for (int repeat = 0; repeat < 8; repeat++) action();
                clock.Stop();
                results.Add(name + "\t8\t" + clock.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture) +
                    "\t" + (clock.Elapsed.TotalMilliseconds / 8d).ToString("F6", CultureInfo.InvariantCulture));
            };
            measure("WorldRegion", () => worldManager.SetCurrentRegion(region));
            measure("WorldCollections", world.EnsureCollections);
            measure("WorldIdentity", () => YQStateIdentity.EnsureWorldState(world));
            measure("PlayerLocationDefault", () => playerManager.SetLocation("YourQuest_PlaySafe", region, new Vector3(1f, 2f, 3f)));
            if (world.stateRevision != revisionBefore + 8 || world.currentRegionId != region ||
                acceptedBefore != Newtonsoft.Json.JsonConvert.SerializeObject(world.generatedWorldPlan, settings) ||
                WorldStateManager.Instance != originalWorldOwner || PlayerStateManager.Instance != originalPlayerOwner)
                throw new InvalidOperationException("Detached location probe changed accepted content, owner or setter semantics");
            worldManager.ReplaceState(currentWorld);
            playerManager.state = player;
            playerManager.SetLocation(player.currentScene, player.currentRegionId, player.lastPosition);
            string currentAcceptedBefore = Newtonsoft.Json.JsonConvert.SerializeObject(currentWorld.generatedWorldPlan, settings);
            long currentRevisionBefore = currentWorld.stateRevision;
            measure("WorldRegionCurrent", () => worldManager.SetCurrentRegion(currentWorld.currentRegionId));
            measure("PlayerLocationNoSave", () => playerManager.SetLocation(player.currentScene, player.currentRegionId, player.lastPosition));
            measure("PlayerSnapshotOnly", () => {
                if (!playerManager.TryPrepareSnapshot(out _, out string failure)) throw new InvalidOperationException(failure);
            });
            // note: Actual atomic autosaves target this explicit workspace fixture file; no profile projection/revision is written or scheduled.
            string autosavePath = Path.GetFullPath("outputs/G08_R2_D27_PlayerAutosaveFixture.json");
            playerManager.saveFileName = autosavePath;
            PropertyInfo savePath = typeof(PlayerStateManager).GetProperty("SavePath", BindingFlags.Instance | BindingFlags.NonPublic);
            if (!string.Equals((string)savePath.GetValue(playerManager), autosavePath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Detached autosave path escaped the explicit workspace fixture");
            FieldInfo nextAutosave = typeof(PlayerStateManager).GetField("nextAutosaveTime", BindingFlags.Instance | BindingFlags.NonPublic);
            playerManager.autosave = true;
            // note: Compare eight callback dispatches; drain each write outside its measured scope so fixture cleanup is not called a frame saving.
            double dispatchMilliseconds = 0d;
            for (int repeat = 0; repeat < 8; repeat++)
            {
                nextAutosave.SetValue(playerManager, 0f);
                var dispatchClock = System.Diagnostics.Stopwatch.StartNew();
                using (new ProfilerMarker("YQ.R2.LocationNormalization.PlayerLocationAutosave").Auto())
                    playerManager.SetLocation(player.currentScene, player.currentRegionId, player.lastPosition);
                dispatchClock.Stop();
                dispatchMilliseconds += dispatchClock.Elapsed.TotalMilliseconds;
                if (!playerManager.TryFlushPendingAutosave(out string writeFailure))
                    throw new InvalidOperationException(writeFailure);
            }
            results.Add("PlayerLocationAutosave\t8\t" + dispatchMilliseconds.ToString("F6", CultureInfo.InvariantCulture) +
                "\t" + (dispatchMilliseconds / 8d).ToString("F6", CultureInfo.InvariantCulture));
            results.Add("# autosave callback scope excludes the explicit fixture flush; disk completion is still required at ownership boundaries");
            int barrierCases = VerifyR2AutosaveBarriers(playerManager, nextAutosave, autosavePath, settings);
            if (currentWorld.stateRevision != currentRevisionBefore + 8 ||
                currentAcceptedBefore != Newtonsoft.Json.JsonConvert.SerializeObject(currentWorld.generatedWorldPlan, settings) ||
                manifestJson != File.ReadAllText(manifestPath) || playerJson != File.ReadAllText(playerPath) ||
                currentWorldJson != File.ReadAllText(worldPath) || WorldStateManager.Instance != originalWorldOwner ||
                PlayerStateManager.Instance != originalPlayerOwner)
                throw new InvalidOperationException("Location autosave probe changed persistent fixture, accepted content or live owner");
            results.Add("# PASS accepted plans and persistent paired revision/manifest unchanged; eight touches per world; singleton owners unchanged; autosave writes only " + autosavePath);
            results.Add("# autosaveBarrierCases=" + barrierCases + " PASS; profile replacement helper is state-level fixture evidence, not visible two-profile UI certification");
            File.WriteAllLines(summaryPath, results);
        }
        finally
        {
            // note: Destroy only the inactive fixture; no manager persistence method or accepted-state replacement is called on live owners.
            UnityEngine.Object.DestroyImmediate(fixture);
        }
    }

    private static int VerifyR2AutosaveBarriers(PlayerStateManager player, FieldInfo nextAutosave, string path,
        Newtonsoft.Json.JsonSerializerSettings settings)
    {
        // note: Exercise immutable writes, real snapshot/save/transition barriers and failure guards only on named workspace files.
        FieldInfo pending = typeof(PlayerStateManager).GetField("pendingAutosave", BindingFlags.Instance | BindingFlags.NonPublic);
        if (pending == null) throw new InvalidOperationException("Autosave regression requires the production pending writer slot");
        int cases = 0;
        Action<bool, string> check = (passed, reason) => {
            if (!passed) throw new InvalidOperationException("Autosave regression: " + reason);
            cases++;
        };
        Func<string, PlayerState> read = file => Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(file), settings);
        Action<Vector3> dispatch = position => {
            nextAutosave.SetValue(player, 0f);
            player.SetLocation("YourQuest_PlaySafe", player.state.currentRegionId, position);
        };
        Vector3 first = new Vector3(11f, 12f, 13f), second = new Vector3(21f, 22f, 23f);
        dispatch(first);
        check(pending.GetValue(player) != null, "automatic write was not registered");
        player.state.lastPosition = second;
        check(player.TryPrepareSnapshot(out string snapshot, out _), "snapshot barrier rejected a successful write");
        check(pending.GetValue(player) == null, "snapshot left an old writer pending");
        check(read(path).lastPosition == first, "worker read mutable player position");
        check(Newtonsoft.Json.JsonConvert.DeserializeObject<PlayerState>(snapshot, settings).lastPosition == second, "new snapshot lost current state");
        check(player.TrySave(out _), "explicit save failed");
        check(read(path).lastPosition == second, "explicit save was overwritten by an old automatic snapshot");
        check(read(path + ".bak").lastPosition == first, "atomic recovery backup changed");
        dispatch(first);
        check(YQProfileSaveSystem.TryPreparePlayerForProfileTransition(player, out _), "profile transition did not drain the writer");
        check(pending.GetValue(player) == null && read(path).lastPosition == first, "profile transition returned before disk completion");
        string nextPath = Path.GetFullPath("outputs/G08_R2_P10_NextProjectionFixture.json");
        dispatch(second);
        player.saveFileName = nextPath;
        check(player.TryFlushPendingAutosave(out _), "captured-path flush failed");
        check(read(path).lastPosition == second, "writer followed a mutable save filename");
        check(player.TrySave(out _), "replacement-path save failed");
        check(read(nextPath).lastPosition == second, "replacement-path snapshot changed");
        player.saveFileName = path;
        var held = new System.Threading.Tasks.TaskCompletionSource<bool>();
        pending.SetValue(player, held.Task);
        dispatch(first);
        check(ReferenceEquals(pending.GetValue(player), held.Task), "automatic dispatch replaced an in-flight writer");
        held.SetResult(true);
        check(player.TryFlushPendingAutosave(out _), "completed slot did not drain");
        pending.SetValue(player, System.Threading.Tasks.Task.FromException(new IOException("expected detached autosave failure")));
        check(!YQProfileSaveSystem.TryPreparePlayerForProfileTransition(player, out string failure) &&
            failure.Contains("expected detached autosave failure"), "profile transition hid write failure");
        check(pending.GetValue(player) == null, "failed writer slot was not observed and cleared");
        return cases;
    }

    private static void WriteR2AggregateAdmissionRegression(string summaryPath, bool requireNoUnrelatedScans)
    {
        // note: Exercise the production predicate with the configured 256-entry bound; only one detached candidate is ever eligible.
        Type ownerType = typeof(YQPlayerFollowingSemanticChunkStreamer);
        Type chunkType = ownerType.GetNestedType("RuntimeChunk", BindingFlags.NonPublic);
        MethodInfo method = ownerType.GetMethod("CanAdvanceAggregateWork", BindingFlags.Instance | BindingFlags.NonPublic);
        GameObject root = new GameObject("YQ_R2_AggregateAdmissionFixture") { hideFlags = HideFlags.HideAndDontSave };
        root.SetActive(false);
        TerrainData data = null;
        bool previousObserver = YQPlayerFollowingSemanticChunkStreamer.R2ObserveContentCandidateScans;
        try
        {
            var owner = root.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            var admit = (Func<string, bool, bool, bool, bool, Vector2Int?, Vector2Int?, bool>)method.CreateDelegate(
                typeof(Func<string, bool, bool, bool, bool, Vector2Int?, Vector2Int?, bool>), owner);
            Func<string, FieldInfo> field = name => ownerType.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            var queue = (List<Vector2Int>)field("_queue").GetValue(owner);
            var visible = (HashSet<Vector2Int>)field("_guaranteedViewDemand").GetValue(owner);
            var hard = (HashSet<Vector2Int>)field("_hardViewDemand").GetValue(owner);
            var chunks = (System.Collections.IDictionary)field("_chunks").GetValue(owner);
            var terrains = (Dictionary<Vector2Int, Terrain>)field("_extendedTerrainTiles").GetValue(owner);
            Vector2Int coordinate = new Vector2Int(31, 29);
            for (int index = 0; index < 256; index++) queue.Add(new Vector2Int(31 + index, 29));
            object chunk = Activator.CreateInstance(chunkType, true);
            chunkType.GetField("state").SetValue(chunk, YQSemanticChunkLifecycle.SemanticallyPlanned);
            chunks.Add(coordinate, chunk);
            // note: The production resolver requires an authored TerrainData reference even for continuation lookup; this small detached surface supplies that dependency only.
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(128f, 10f, 128f) };
            Terrain terrain = root.AddComponent<Terrain>();
            terrain.terrainData = data;
            field("_terrain").SetValue(owner, terrain);
            terrains.Add(coordinate, terrain);
            YQPlayerFollowingSemanticChunkStreamer.R2ObserveContentCandidateScans = true;
            var rows = new List<string> { "# Detached aggregate admission; utc=" + DateTime.UtcNow.ToString("O") +
                "; assemblyMvid=" + ownerType.Assembly.ManifestModule.ModuleVersionId,
                "stage\tcandidate\tbudgetSpent\treserved\thardActivation\tdeadlineOpen\texpected\tactual\tscans" };
            int cases = 0;
            long unrelatedScans = 0;
            foreach (string stage in new[] { "lifecycleActivation", "lifecycleObjectActivation", "contentDispatch", "requiredContent", "terrainStart", "unknown" })
            foreach (int candidate in new[] { 0, 1, 2 })
            foreach (bool spent in new[] { false, true })
            foreach (bool reserved in new[] { false, true })
            foreach (bool activation in new[] { false, true })
            foreach (bool deadlineOpen in new[] { false, true })
            {
                visible.Clear(); hard.Clear();
                if (candidate == 1) visible.Add(coordinate);
                if (candidate == 2) hard.Add(coordinate);
                field("_aggregateBudgetFrame").SetValue(owner, Time.frameCount);
                field("_aggregateFrameWorkSeconds").SetValue(owner, spent ? 0.020f : 0f);
                field("_requiredHardViewEcologyBudgetFrame").SetValue(owner, -1);
                field("_reservedHardViewContentDispatchFrame").SetValue(owner, reserved ? Time.frameCount : -1);
                field("_lifecycleBudgetFrame").SetValue(owner, Time.frameCount);
                field("_lifecycleDeadline").SetValue(owner, Time.realtimeSinceStartup + (deadlineOpen ? 60f : -60f));
                bool expected = !spent || (activation && deadlineOpen &&
                    (stage == "lifecycleActivation" || stage == "lifecycleObjectActivation")) ||
                    (stage == "contentDispatch" && !reserved && candidate != 0);
                long before = YQPlayerFollowingSemanticChunkStreamer.R2ContentCandidateScans;
                bool actual = admit(stage, activation, false, false, false, null, null);
                long scans = YQPlayerFollowingSemanticChunkStreamer.R2ContentCandidateScans - before;
                if (actual != expected) throw new InvalidOperationException("Aggregate admission decision changed: " + stage +
                    " candidate=" + candidate + " spent=" + spent + " reserved=" + reserved + " activation=" + activation + " deadline=" + deadlineOpen);
                if (stage != "contentDispatch" || reserved) unrelatedScans += scans;
                rows.Add(stage + "\t" + candidate + "\t" + spent + "\t" + reserved + "\t" + activation + "\t" +
                    deadlineOpen + "\t" + expected + "\t" + actual + "\t" + scans);
                cases++;
            }
            // note: Model the 450 denied visits in the saved maximum, using a delegate to avoid per-call reflection allocation.
            visible.Clear(); hard.Clear();
            field("_aggregateBudgetFrame").SetValue(owner, Time.frameCount);
            field("_aggregateFrameWorkSeconds").SetValue(owner, 0.020f);
            long scanStart = YQPlayerFollowingSemanticChunkStreamer.R2ContentCandidateScans;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int repeat = 0; repeat < 450; repeat++)
                if (admit("lifecycleActivation", false, false, false, false, null, null))
                    throw new InvalidOperationException("Denied activation was admitted");
            timer.Stop();
            long benchmarkScans = YQPlayerFollowingSemanticChunkStreamer.R2ContentCandidateScans - scanStart;
            rows.Add("# result=PASS cases=" + cases + " unrelatedScans=" + unrelatedScans + " benchmarkCalls=450 benchmarkScans=" +
                benchmarkScans + " benchmarkWallMs=" + timer.Elapsed.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) +
                " requireNoUnrelatedScans=" + requireNoUnrelatedScans + " evidence=detached-not-runtime");
            if (requireNoUnrelatedScans && (unrelatedScans != 0 || benchmarkScans != 0))
                throw new InvalidOperationException("Unrelated aggregate admission still scans content candidates");
            File.WriteAllLines(summaryPath, rows);
            terrains.Clear(); chunks.Clear();
        }
        finally
        {
            YQPlayerFollowingSemanticChunkStreamer.R2ObserveContentCandidateScans = previousObserver;
            // note: This fixture owns its inactive root and component; ordinary scene objects and profile state are untouched.
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void WriteR2OptionalComponentRegression(string summaryPath)
    {
        // note: Exercise the actual private renderer/collider predicates on transient roots; reflection is fixture overhead, never a gameplay authority.
        Type streamerType = typeof(YQPlayerFollowingSemanticChunkStreamer);
        Type chunkType = streamerType.GetNestedType("RuntimeChunk", BindingFlags.NonPublic);
        MethodInfo readiness = streamerType.GetMethod("ArePublishedTerrainRenderersEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo colliderReady = streamerType.GetMethod("IsTerrainColliderReady", BindingFlags.Static | BindingFlags.NonPublic);
        MethodInfo visuals = streamerType.GetMethod("SetStreamedVisualsEnabled", BindingFlags.Static | BindingFlags.NonPublic);
        if (chunkType == null || readiness == null || colliderReady == null || visuals == null)
            throw new InvalidOperationException("Optional component fixture requires production publication predicates");
        var fixtures = new List<GameObject>();
        TerrainData data = null;
        int cases = 0;
        try
        {
            GameObject observer = new GameObject("YQ_R2_OptionalComponentObserver") { hideFlags = HideFlags.HideAndDontSave };
            fixtures.Add(observer);
            observer.SetActive(false);
            var streamer = observer.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            object chunk = Activator.CreateInstance(chunkType, true);
            var generated = (List<GameObject>)chunkType.GetField("generatedObjects").GetValue(chunk);
            FieldInfo workId = chunkType.GetField("decorativeWorkId");
            FieldInfo dirty = chunkType.GetField("rendererValidationCacheDirty");
            for (int i = 0; i < 20; i++)
            {
                GameObject root = new GameObject("YQ_R2_OptionalRoot_" + i) { hideFlags = HideFlags.HideAndDontSave };
                fixtures.Add(root); generated.Add(root);
            }
            GameObject rendererRoot = new GameObject("YQ_R2_OptionalRenderer") { hideFlags = HideFlags.HideAndDontSave };
            fixtures.Add(rendererRoot); generated.Add(rendererRoot);
            MeshRenderer renderer = rendererRoot.AddComponent<MeshRenderer>();
            data = new TerrainData { hideFlags = HideFlags.HideAndDontSave, heightmapResolution = 33, size = new Vector3(32f, 8f, 32f) };
            GameObject terrainRoot = Terrain.CreateTerrainGameObject(data);
            terrainRoot.hideFlags = HideFlags.HideAndDontSave;
            fixtures.Add(terrainRoot); generated.Add(terrainRoot);
            Terrain terrain = terrainRoot.GetComponent<Terrain>();
            TerrainCollider collider = terrainRoot.GetComponent<TerrainCollider>();
            object[] chunkArgs = { chunk };
            foreach (long ecology in new[] { 0L, 1L })
            {
                workId.SetValue(chunk, ecology);
                dirty.SetValue(chunk, true);
                Action<bool> check = expected => {
                    bool actual = (bool)readiness.Invoke(streamer, chunkArgs);
                    if (actual != expected) throw new InvalidOperationException("Renderer readiness changed for optional component fixture");
                    cases++;
                };
                check(true);
                renderer.enabled = false; check(false); renderer.enabled = true;
                rendererRoot.SetActive(false); check(false); rendererRoot.SetActive(true);
                terrain.enabled = false; check(false); terrain.enabled = true;
                terrainRoot.SetActive(false); check(false); terrainRoot.SetActive(true);
                GameObject missing = new GameObject("YQ_R2_DestroyedOptionalRoot") { hideFlags = HideFlags.HideAndDontSave };
                generated.Add(missing); UnityEngine.Object.DestroyImmediate(missing); dirty.SetValue(chunk, true); check(true);
                generated.RemoveAt(generated.Count - 1);
                visuals.Invoke(null, new object[] { chunk, false });
                if (renderer.enabled || terrain.enabled) throw new InvalidOperationException("Full visual hiding changed");
                check(false);
                visuals.Invoke(null, new object[] { chunk, true }); check(true);
            }
            // note: Collider state remains authoritative independently of renderer state and optional content-root components.
            Action<bool> checkCollider = expected => {
                bool actual = (bool)colliderReady.Invoke(null, new object[] { terrain });
                if (actual != expected) throw new InvalidOperationException("Terrain collider readiness changed");
                cases++;
            };
            checkCollider(true);
            terrain.enabled = false; checkCollider(true); terrain.enabled = true;
            collider.enabled = false; checkCollider(false); collider.enabled = true;
            terrainRoot.SetActive(false); checkCollider(false); terrainRoot.SetActive(true);
            foreach (GameObject root in generated)
            {
                bool oldCollision = root.GetComponent<TerrainCollider>() != null;
                bool newCollision = root.TryGetComponent<TerrainCollider>(out _);
                if (oldCollision != newCollision) throw new InvalidOperationException("Collision-preserving activation classification changed");
                cases++;
            }
            // note: The old optional Terrain lookup is retained only as a benchmark reference; the repaired comparison calls the actual production predicate.
            workId.SetValue(chunk, 1L);
            readiness.Invoke(streamer, chunkArgs);
            using (new ProfilerMarker("YQ.R2.OptionalComponents.ReferenceLookup").Auto())
                for (int repeat = 0; repeat < 128; repeat++)
                    foreach (GameObject root in generated)
                    {
                        Terrain optional = root.GetComponent<Terrain>();
                        if (optional != null && (!optional.enabled || !optional.gameObject.activeInHierarchy))
                            throw new InvalidOperationException("Reference terrain fixture unexpectedly hidden");
                    }
            using (new ProfilerMarker("YQ.R2.OptionalComponents.ProductionReadiness").Auto())
                for (int repeat = 0; repeat < 128; repeat++)
                    if (!(bool)readiness.Invoke(streamer, chunkArgs))
                        throw new InvalidOperationException("Production terrain fixture unexpectedly hidden");
            cases += VerifyR2VisualGateSetterParity(visuals, chunkType, terrainRoot, fixtures);
            cases += VerifyR2ManagedTerrainGate(streamer, chunkType, terrainRoot, data, fixtures,
                Path.ChangeExtension(summaryPath, ".terrainpreview.tsv"));
            File.WriteAllLines(summaryPath, new[] {
                "# Edit-only temporary publication regression; visualSetterParityCases=64; utc=" + DateTime.UtcNow.ToString("O") +
                    "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
                "result\tcases\tmissingRoots\tcachedAndLiveEcology\thiddenDisabledDestroyed\tcollisionClassification\tvisualGate\tallocationMetric",
                "PASS\t" + cases + "\t21\tPASS\tPASS\tPASS\tPASS\tnativeSavedTraceRequired"
            });
        }
        finally
        {
            // note: Only fixture-owned objects/data are removed; no scene, profile, accepted record, or approved asset is changed.
            foreach (GameObject fixture in fixtures)
                if (fixture != null) UnityEngine.Object.DestroyImmediate(fixture);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static int VerifyR2VisualGateSetterParity(MethodInfo visuals, Type chunkType, GameObject terrainRoot, List<GameObject> fixtures)
    {
        // note: Compare all mixed states, inactive descendants and overlapping roots on disposable hierarchies; only changed properties may be written.
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        foreach (GameObject item in new[] { root, child })
        {
            item.hideFlags = HideFlags.HideAndDontSave;
            fixtures.Add(item);
            item.GetComponent<Collider>().enabled = false;
        }
        root.name = "YQ_R2_VisualGateParityRoot";
        child.name = "YQ_R2_VisualGateParityChild";
        child.transform.SetParent(root.transform, false);
        Renderer parentRenderer = root.GetComponent<Renderer>();
        Renderer childRenderer = child.GetComponent<Renderer>();
        Terrain terrain = terrainRoot.GetComponent<Terrain>();
        TerrainCollider collider = terrainRoot.GetComponent<TerrainCollider>();
        bool colliderEnabled = collider.enabled;
        object chunk = Activator.CreateInstance(chunkType, true);
        var generated = (List<GameObject>)chunkType.GetField("generatedObjects").GetValue(chunk);
        generated.Add(root); generated.Add(child); generated.Add(terrainRoot); generated.Add(null);
        int cases = 0;
        foreach (bool active in new[] { false, true })
        foreach (bool desired in new[] { false, true })
        for (int mask = 0; mask < 8; mask++)
        {
            root.SetActive(active);
            child.SetActive(false);
            parentRenderer.enabled = (mask & 1) != 0;
            childRenderer.enabled = (mask & 2) != 0;
            terrain.enabled = (mask & 4) != 0;
            long rendererBefore = YQPlayerFollowingSemanticChunkStreamer.R2VisualRendererWritesPerformed;
            long terrainBefore = YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed;
            int expectedRendererWrites = (parentRenderer.enabled != desired ? 1 : 0) + (childRenderer.enabled != desired ? 1 : 0);
            int expectedTerrainWrites = terrain.enabled != desired ? 1 : 0;
            visuals.Invoke(null, new object[] { chunk, desired });
            if (parentRenderer.enabled != desired || childRenderer.enabled != desired || terrain.enabled != desired ||
                root.activeSelf != active || child.activeSelf || collider.enabled != colliderEnabled ||
                YQPlayerFollowingSemanticChunkStreamer.R2VisualRendererWritesPerformed - rendererBefore != expectedRendererWrites ||
                YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed - terrainBefore != expectedTerrainWrites)
                throw new InvalidOperationException("Visual setter guard changed hierarchy state or wrote an already-correct property");
            cases++;
            rendererBefore = YQPlayerFollowingSemanticChunkStreamer.R2VisualRendererWritesPerformed;
            terrainBefore = YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed;
            visuals.Invoke(null, new object[] { chunk, desired });
            if (parentRenderer.enabled != desired || childRenderer.enabled != desired || terrain.enabled != desired ||
                YQPlayerFollowingSemanticChunkStreamer.R2VisualRendererWritesPerformed != rendererBefore ||
                YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed != terrainBefore)
                throw new InvalidOperationException("Repeated visual publication lost state or repeated a native setter");
            cases++;
        }
        // note: Benchmark identical already-correct roots using frozen old writes and the actual repaired method; inclusive scope costs are compared separately.
        Action reference = () => {
            foreach (GameObject generatedRoot in generated)
            {
                if (generatedRoot == null) continue;
                foreach (Renderer renderer in generatedRoot.GetComponentsInChildren<Renderer>(true))
                    if (renderer != null) renderer.enabled = true;
                if (generatedRoot.TryGetComponent<Terrain>(out Terrain originalTerrain)) originalTerrain.enabled = true;
            }
        };
        reference();
        using (new ProfilerMarker("YQ.R2.VisualGate.ReferenceAlreadyCorrect").Auto())
            for (int repeat = 0; repeat < 128; repeat++) reference();
        using (new ProfilerMarker("YQ.R2.VisualGate.CurrentAlreadyCorrect").Auto())
            for (int repeat = 0; repeat < 128; repeat++) visuals.Invoke(null, new object[] { chunk, true });
        if (!parentRenderer.enabled || !childRenderer.enabled || !terrain.enabled || collider.enabled != colliderEnabled)
            throw new InvalidOperationException("Visual guard benchmark changed the required final state");
        return cases;
    }

    private static int VerifyR2ManagedTerrainGate(
        YQPlayerFollowingSemanticChunkStreamer streamer, Type chunkType, GameObject terrainRoot,
        TerrainData data, List<GameObject> fixtures, string summaryPath)
    {
        // note: Exercise the real per-chunk gate on detached owners; preview eligibility and collider rules remain production predicates.
        Type streamerType = typeof(YQPlayerFollowingSemanticChunkStreamer);
        MethodInfo gate = streamerType.GetMethod("SetChunkVisualsEnabled", BindingFlags.Instance | BindingFlags.NonPublic);
        var chunks = (System.Collections.IDictionary)streamerType.GetField("_chunks", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(streamer);
        var terrains = (Dictionary<Vector2Int, Terrain>)streamerType.GetField("_extendedTerrainTiles", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(streamer);
        var visible = (HashSet<Vector2Int>)streamerType.GetField("_guaranteedViewDemand", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(streamer);
        Vector2Int coordinate = new Vector2Int(31, 29);
        object chunk = Activator.CreateInstance(chunkType, true);
        chunkType.GetField("record").SetValue(chunk, new GeneratedSemanticChunkRecord { chunkX = coordinate.x, chunkZ = coordinate.y });
        FieldInfo readiness = chunkType.GetField("terrainReadiness");
        FieldInfo known = chunkType.GetField("visualStateKnown");
        FieldInfo state = chunkType.GetField("visualState");
        var generated = (List<GameObject>)chunkType.GetField("generatedObjects").GetValue(chunk);
        GameObject meshRoot = new GameObject("YQ_R2_ManagedTerrainMesh") { hideFlags = HideFlags.HideAndDontSave };
        fixtures.Add(meshRoot);
        MeshRenderer renderer = meshRoot.AddComponent<MeshRenderer>();
        GameObject otherRoot = Terrain.CreateTerrainGameObject(data);
        otherRoot.hideFlags = HideFlags.HideAndDontSave;
        fixtures.Add(otherRoot);
        Terrain otherTerrain = otherRoot.GetComponent<Terrain>();
        Terrain terrain = terrainRoot.GetComponent<Terrain>();
        TerrainCollider collider = terrainRoot.GetComponent<TerrainCollider>();
        bool drawHeightmap = terrain.drawHeightmap, drawFoliage = terrain.drawTreesAndFoliage;
        generated.Add(meshRoot); generated.Add(terrainRoot); generated.Add(otherRoot);
        chunks.Add(coordinate, chunk);
        object[] gateArgs = { chunk, false };
        int cases = 0;
        foreach (bool active in new[] { false, true })
        foreach (bool collisionEnabled in new[] { false, true })
        foreach (bool collisionPublished in new[] { false, true })
        foreach (bool guaranteed in new[] { false, true })
        foreach (bool desired in new[] { false, true })
        foreach (bool initiallyEnabled in new[] { false, true })
        foreach (bool initiallyKnown in new[] { false, true })
        {
            terrainRoot.SetActive(active);
            collider.enabled = collisionEnabled;
            readiness.SetValue(chunk, collisionPublished ? YQTerrainReadinessState.CollisionReady : YQTerrainReadinessState.None);
            if (guaranteed) visible.Add(coordinate); else visible.Remove(coordinate);
            terrains[coordinate] = terrain;
            terrain.enabled = initiallyEnabled;
            renderer.enabled = !desired;
            otherTerrain.enabled = !desired;
            known.SetValue(chunk, initiallyKnown);
            state.SetValue(chunk, !desired);
            gateArgs[1] = desired;
            bool expectedTerrain = desired || (active && collisionEnabled && collisionPublished && guaranteed);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                gate.Invoke(streamer, gateArgs);
                if (terrain.enabled != expectedTerrain || renderer.enabled != desired || otherTerrain.enabled != desired ||
                    collider.enabled != collisionEnabled || terrainRoot.activeSelf != active ||
                    terrain.drawHeightmap != drawHeightmap || terrain.drawTreesAndFoliage != drawFoliage ||
                    !(bool)known.GetValue(chunk) || (bool)state.GetValue(chunk) != desired)
                    throw new InvalidOperationException("Managed Terrain reconciliation changed final visual/collision state");
                cases++;
            }
        }
        // note: Forced hierarchy invalidation models newly attached owned content while the existing permitted ground preview remains visible.
        terrainRoot.SetActive(true); collider.enabled = true;
        readiness.SetValue(chunk, YQTerrainReadinessState.CollisionReady);
        visible.Add(coordinate); terrains[coordinate] = terrain;
        terrain.enabled = true; renderer.enabled = false; otherTerrain.enabled = false;
        gateArgs[1] = false;
        long hierarchyBefore = YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed;
        long managedBefore = YQPlayerFollowingSemanticChunkStreamer.R2ManagedTerrainWritesPerformed;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        using (new ProfilerMarker("YQ.R2.TerrainGate.PermittedPreviewReconciliation").Auto())
            for (int repeat = 0; repeat < 128; repeat++)
            {
                known.SetValue(chunk, false);
                gate.Invoke(streamer, gateArgs);
                if (!terrain.enabled || renderer.enabled || otherTerrain.enabled || !collider.enabled)
                    throw new InvalidOperationException("Permitted preview benchmark changed final publication state");
            }
        timer.Stop();
        File.WriteAllLines(summaryPath, new[] {
            "# Edit-only detached managed Terrain gate; utc=" + DateTime.UtcNow.ToString("O") +
                "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
            "result\tcases\tpreviewCalls\thierarchyTerrainWrites\tmanagedTerrainWrites\tinclusiveWallMs\tmetric",
            "PASS\t" + cases + "\t128\t" + (YQPlayerFollowingSemanticChunkStreamer.R2VisualTerrainWritesPerformed - hierarchyBefore) +
                "\t" + (YQPlayerFollowingSemanticChunkStreamer.R2ManagedTerrainWritesPerformed - managedBefore) +
                "\t" + timer.Elapsed.TotalMilliseconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + "\tnativeSavedTraceRequired"
        });
        chunks.Remove(coordinate); terrains.Remove(coordinate); visible.Remove(coordinate);
        return cases;
    }

    private static void WriteR2EcologyHandoffRegression(string summaryPath)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Ecology handoff regression is Edit-only");
        GameObject fixture = new GameObject("YQR2_EcologyHandoffFixture") { hideFlags = HideFlags.HideAndDontSave };
        GameObject root = new GameObject("YQR2_EcologyHandoffRoot") { hideFlags = HideFlags.HideAndDontSave };
        int cases = 0;
        try
        {
            // note: Exercise the exact production handoff on isolated inactive owners without a profile, terrain, provider, or persistent mutation.
            fixture.SetActive(false);
            var observer = fixture.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var type = typeof(YQPlayerFollowingSemanticChunkStreamer);
            var chunkType = type.GetNestedType("RuntimeChunk", System.Reflection.BindingFlags.NonPublic);
            var tokenType = type.GetNestedType("StreamingWorkToken", System.Reflection.BindingFlags.NonPublic);
            var pendingType = type.GetNestedType("PendingRequiredEcologyPublication", System.Reflection.BindingFlags.NonPublic);
            var scenarioType = type.GetNestedType("PublicationVerificationScenario", System.Reflection.BindingFlags.NonPublic);
            var chunks = (System.Collections.IDictionary)type.GetField("_chunks", flags).GetValue(observer);
            var scenarios = (System.Collections.IDictionary)type.GetField("_publicationVerificationScenarios", flags).GetValue(observer);
            var publish = type.GetMethod("TryPublishPreparedRequiredEcology", flags);
            Vector2Int coordinate = new Vector2Int(17, 23);
            object chunk = null, pending = null, token = null, scenario = null;
            void Set(object target, string name, object value) => target.GetType().GetField(name, flags).SetValue(target, value);
            object Get(object target, string name) => target.GetType().GetField(name, flags).GetValue(target);
            void Reset(int result = 1, bool content = true, bool overlay = true, bool held = false, bool fault = false)
            {
                chunks.Clear();
                scenarios.Clear();
                chunk = Activator.CreateInstance(chunkType, true);
                token = Activator.CreateInstance(tokenType);
                pending = Activator.CreateInstance(pendingType, true);
                scenario = Activator.CreateInstance(scenarioType, true);
                Set(chunk, "requiredContentReady", content);
                Set(chunk, "overlayReady", overlay);
                Set(chunk, "decorativeWorkId", 41L);
                Set(chunk, "decorativeRoot", root);
                Set(token, "profileId", string.Empty);
                Set(token, "worldId", string.Empty);
                Set(token, "generationEpoch", type.GetField("_configurationEpoch", flags).GetValue(observer));
                Set(token, "owner", chunk);
                Set(token, "ownerEpoch", Get(chunk, "contentOwnerEpoch"));
                Set(pending, "token", token);
                Set(pending, "workId", 41L);
                Set(pending, "root", root);
                Set(pending, "result", result);
                Set(pending, "providerFailure", "fixture required-layer rejection");
                Set(chunk, "pendingRequiredEcology", pending);
                Set(scenario, "holdRequiredEcology", held);
                Set(scenario, "ecologyFailuresRemaining", fault ? 1 : 0);
                chunks.Add(coordinate, chunk);
                scenarios.Add(coordinate, scenario);
            }
            void Invoke(bool alreadyApplied) => publish.Invoke(observer, new object[] { coordinate, chunk, alreadyApplied });
            void Check(bool expected, string reason)
            {
                if ((bool)Get(pending, "published") != expected || (bool)Get(chunk, "requiredEcologyReady") != expected ||
                    (int)Get(chunk, "publicationVersion") != (expected ? 1 : 0) || !root.activeSelf)
                    throw new InvalidOperationException("Ecology handoff violated " + reason);
                cases++;
            }
            // note: Both completion orders preserve absent-result, accepted-count, structural, overlay, hold and injected-fault barriers.
            foreach (bool alreadyApplied in new[] { false, true })
                foreach (int result in new[] { int.MinValue, 0, 1 })
                    for (int mask = 0; mask < 16; mask++)
                    {
                        bool content = (mask & 1) != 0, overlay = (mask & 2) != 0;
                        bool held = (mask & 4) != 0, fault = (mask & 8) != 0;
                        Reset(result, content, overlay, held, fault);
                        Invoke(alreadyApplied);
                        bool eligible = result != int.MinValue && content && overlay && !held;
                        bool expected = eligible && result > 0 && !fault;
                        Check(expected, "completion-order matrix " + mask);
                        if (((string)Get(pending, "failure") != null) != (eligible && (result <= 0 || fault)) ||
                            (int)Get(scenario, "ecologyFailuresRemaining") != (fault && !eligible ? 1 : 0))
                            throw new InvalidOperationException("Ecology failure escaped its required publication barrier");
                        Invoke(alreadyApplied);
                        Check(expected, "idempotent publication");
                    }
            // note: Delayed prepared results remain bound to the profile/world, configuration, overlay revision, content owner epoch, registration and work identity.
            for (int invalid = 0; invalid < 9; invalid++)
            {
                Reset();
                switch (invalid)
                {
                    case 0: Set(token, "profileId", "another-profile"); break;
                    case 1: Set(token, "worldId", "another-world"); break;
                    case 2: Set(token, "generationEpoch", (int)Get(token, "generationEpoch") + 1); break;
                    case 3: Set(token, "featureRevision", 1L); break;
                    case 4: Set(token, "ownerEpoch", (int)Get(token, "ownerEpoch") + 1); break;
                    case 5: Set(token, "owner", Activator.CreateInstance(chunkType, true)); break;
                    case 6: Set(pending, "workId", 42L); break;
                    case 7: chunks.Clear(); break;
                    case 8: chunks[coordinate] = Activator.CreateInstance(chunkType, true); break;
                }
                Set(pending, "token", token);
                Invoke(true);
                Check(false, "stale work " + invalid);
            }
            Reset(held: true);
            Invoke(true);
            Check(false, "required-layer hold");
            Set(scenario, "holdRequiredEcology", false);
            Invoke(true);
            Check(true, "hold release");
            // note: Cancellation after the structural handoff must preserve its published ecology and clear only the transient worker registration.
            type.GetMethod("StopDecorativeGeneration", flags).Invoke(observer, new[] { chunk });
            if (!(bool)Get(pending, "published") || !(bool)Get(chunk, "requiredEcologyReady") ||
                (long)Get(chunk, "decorativeWorkId") != 0L || Get(chunk, "pendingRequiredEcology") != null || !root.activeSelf)
                throw new InvalidOperationException("Cancellation destroyed a jointly published required layer");
            cases++;
            Reset();
            Set(pending, "root", null);
            Invoke(true);
            Check(false, "missing owner hierarchy");
            if ((string)Get(pending, "failure") == null)
                throw new InvalidOperationException("Missing ecology hierarchy was silently accepted");
            chunks.Clear();
            scenarios.Clear();
            File.WriteAllLines(summaryPath, new[] {
                "# Edit-only ecology completion handoff regression; utc=" + DateTime.UtcNow.ToString("O") +
                    "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
                "result\tcases\tbothCompletionOrders\trequiredBarriers\tstaleWorkRejected\tidempotentReceipt\tcancelPreservesPublishedLayer",
                "PASS\t" + cases + "\tTrue\tTrue\tTrue\tTrue\tTrue"
            });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void WriteR2StreamingTeardownRegression(string summaryPath)
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Streaming retirement regression is Edit-only");
        int cases = 0, allowanceFrame = -1, used = 0;
        // note: Simulate a 64-owner retirement burst with repeated lifecycle, capacity and history passes; only a new frame may renew the allowance.
        for (int frame = 0; frame < 64; frame++)
        {
            for (int pass = 0; pass < 12; pass++)
            {
                bool admitted = YQPlayerFollowingSemanticChunkStreamer.TryTakeStreamingRetirementSlot(frame, ref allowanceFrame, ref used);
                if (admitted != (pass == 0) || used != 1 || allowanceFrame != frame)
                    throw new InvalidOperationException("Streaming retirement allowance renewed within a frame or failed to resume");
                cases++;
            }
        }
        GameObject fixture = new GameObject("YQR2_DeferredRetirementFixture") { hideFlags = HideFlags.HideAndDontSave };
        GameObject owned = new GameObject("YQR2_DeferredRetirementOwned") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            // note: A spent allowance must leave the real production owner object and its bookkeeping untouched; this inactive fixture never loads or changes a world.
            fixture.SetActive(false);
            var observer = fixture.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            var type = typeof(YQPlayerFollowingSemanticChunkStreamer);
            var chunkType = type.GetNestedType("RuntimeChunk", System.Reflection.BindingFlags.NonPublic);
            object chunk = Activator.CreateInstance(chunkType, true);
            var generated = (List<GameObject>)chunkType.GetField("generatedObjects", flags).GetValue(chunk);
            generated.Add(owned);
            chunkType.GetField("physicalRepresentation", flags).SetValue(chunk, true);
            chunkType.GetField("state", flags).SetValue(chunk, YQSemanticChunkLifecycle.Unloading);
            type.GetField("_streamingRetirementFrame", flags).SetValue(observer, Time.frameCount);
            type.GetField("_streamingRetirementsThisFrame", flags).SetValue(observer, 1);
            bool retired = (bool)type.GetMethod("TryDestroyGeneratedObjectsForStreaming", flags).Invoke(observer, new[] { chunk });
            if (retired || generated.Count != 1 || generated[0] != owned || !owned.activeSelf ||
                !(bool)chunkType.GetField("physicalRepresentation", flags).GetValue(chunk) ||
                (YQSemanticChunkLifecycle)chunkType.GetField("state", flags).GetValue(chunk) != YQSemanticChunkLifecycle.Unloading ||
                !(bool)type.GetField("_lifecyclePending", flags).GetValue(observer))
                throw new InvalidOperationException("Deferred retirement changed resource ownership or stopped continuation");
            cases++;
            type.GetField("_retiredPhysicalOwnersThisFrame", flags).SetValue(observer, 1);
            var pending = type.GetProperty("PendingStreamingNativeRetirements", flags);
            if ((int)pending.GetValue(observer) != 1)
                throw new InvalidOperationException("Queued native destruction lost its capacity reservation");
            type.GetField("_streamingRetirementFrame", flags).SetValue(observer, Time.frameCount - 1);
            if ((int)pending.GetValue(observer) != 0)
                throw new InvalidOperationException("Completed native destruction retained its capacity reservation");
            cases += 2;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(fixture);
            UnityEngine.Object.DestroyImmediate(owned);
        }
        File.WriteAllLines(summaryPath, new[] {
            "# Edit-only streaming retirement regression; utc=" + DateTime.UtcNow.ToString("O") +
                "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
            "result\tcases\tsameFrameSharedAllowance\tnewFrameResumes\tdeferredOwnerPreserved\tnativeCapacityHeldUntilNextFrame",
            "PASS\t" + cases + "\tTrue\tTrue\tTrue\tTrue"
        });
    }

    private static void WriteR2ActivationRemovalRegression(string summaryPath)
    {
        // note: Exercise the production list-removal operation against processed prefixes in both activation directions; all objects are owned transient fixtures, never scene/save content.
        GameObject[] fixtures = new GameObject[3];
        int cases = 0;
        try
        {
            for (int i = 0; i < fixtures.Length; i++)
                fixtures[i] = new GameObject("YQ_R2_ActivationRemovalFixture_" + i) { hideFlags = HideFlags.HideAndDontSave };
            foreach (bool desiredActive in new[] { true, false })
            {
                for (int cursor = 0; cursor <= fixtures.Length; cursor++)
                {
                    for (int removedIndex = 0; removedIndex < fixtures.Length; removedIndex++)
                    {
                        for (int i = 0; i < fixtures.Length; i++)
                            fixtures[i].SetActive(i < cursor ? desiredActive : !desiredActive);
                        var owned = new List<GameObject>(fixtures);
                        int adjusted = YQPlayerFollowingSemanticChunkStreamer.RemoveOwnedActivationEntry(owned, removedIndex, cursor);
                        int expected = cursor - (removedIndex < cursor ? 1 : 0);
                        if (adjusted != expected || owned.Count != fixtures.Length - 1)
                            throw new InvalidOperationException("Owned removal changed the processed-prefix boundary");
                        for (int i = 0; i < owned.Count; i++)
                        {
                            GameObject expectedObject = fixtures[i < removedIndex ? i : i + 1];
                            if (owned[i] != expectedObject || owned[i].activeSelf != (i < adjusted ? desiredActive : !desiredActive))
                                throw new InvalidOperationException("Owned removal skipped or reordered an unprocessed object");
                        }
                        // note: Resume from the returned cursor just as the production slice does; the remaining generated root must receive its requested state before completion.
                        for (int i = adjusted; i < owned.Count; i++)
                            owned[i].SetActive(desiredActive);
                        foreach (GameObject item in owned)
                            if (item.activeSelf != desiredActive)
                                throw new InvalidOperationException("Removal allowed completion with an unprocessed object");
                        if (YQPlayerFollowingSemanticChunkStreamer.RemoveOwnedActivationEntry(owned, -1, adjusted) != adjusted ||
                            YQPlayerFollowingSemanticChunkStreamer.RemoveOwnedActivationEntry(owned, owned.Count, adjusted) != adjusted ||
                            owned.Count != fixtures.Length - 1)
                            throw new InvalidOperationException("Absent ownership removal was not a no-op");
                        cases++;
                    }
                }
            }
            File.WriteAllLines(summaryPath, new[] {
                "# Edit-only owned-list activation regression; utc=" + DateTime.UtcNow.ToString("O") +
                    "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
                "result\tcases\tbothDirections\tallCursorPositions\tabsentIndexNoOp\tremainingObjectsReachedTarget",
                "PASS\t" + cases + "\tTrue\tTrue\tTrue\tTrue"
            });
        }
        finally
        {
            foreach (GameObject item in fixtures)
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static void WriteR2NoOwnerSentinelRegression(string summaryPath)
    {
        // note: Reproduce the observer's failing arithmetic as pure fixture evidence, then exercise the exact production eligibility predicate without constructing a streamer or touching world state.
        bool oldArithmeticThrows = false;
        try { Mathf.Abs(int.MinValue); }
        catch (OverflowException) { oldArithmeticThrows = true; }
        if (!oldArithmeticThrows ||
            YQPlayerFollowingSemanticChunkStreamer.IsCompleteCellObservationCoordinate(new Vector2Int(int.MinValue, int.MinValue)) ||
            !YQPlayerFollowingSemanticChunkStreamer.IsCompleteCellObservationCoordinate(Vector2Int.zero) ||
            !YQPlayerFollowingSemanticChunkStreamer.IsCompleteCellObservationCoordinate(new Vector2Int(-7, 13)) ||
            !YQPlayerFollowingSemanticChunkStreamer.IsCompleteCellObservationCoordinate(new Vector2Int(int.MinValue, 1)) ||
            !YQPlayerFollowingSemanticChunkStreamer.IsCompleteCellObservationCoordinate(new Vector2Int(1, int.MinValue)))
            throw new InvalidOperationException("No-owner sentinel eligibility regression failed");
        // note: An inactive disposable observer verifies first-failure retention without loading a world or registering a live streamer.
        if (Application.isPlaying) throw new InvalidOperationException("Observer regression requires Edit Mode");
        GameObject fixture = new GameObject("YQR2_FirstFailure_EditorFixture") { hideFlags = HideFlags.HideAndDontSave };
        fixture.SetActive(false);
        try
        {
            var observer = fixture.AddComponent<YQPlayerFollowingSemanticChunkStreamer>();
            FieldInfo Field(string name) => typeof(YQPlayerFollowingSemanticChunkStreamer).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Missing observer field " + name);
            observer.BeginAppearanceBudgetObservation();
            observer.BeginCompleteCellObservation();
            observer.FreezeAppearanceBudgetObservation();
            if ((int)Field("_completeCellFailureCount").GetValue(observer) != -1 ||
                !(bool)Field("_appearanceBudgetObservationActive").GetValue(observer))
                throw new InvalidOperationException("No-failure call consumed the first-failure observation");
            Vector2Int first = new Vector2Int(11, 6), later = new Vector2Int(11, 5);
            Field("_hasLastVisualCoverageFailureCoordinate").SetValue(observer, true);
            Field("_lastVisualCoverageFailureCoordinate").SetValue(observer, first);
            observer.SetCompleteCellObservationPhase("input");
            observer.FreezeAppearanceBudgetObservation();
            int prefix = (int)Field("_completeCellFailureCount").GetValue(observer);
            if (prefix <= 0 || (bool)Field("_appearanceBudgetObservationActive").GetValue(observer))
                throw new InvalidOperationException("Input failure did not freeze the observation");
            Field("_lastVisualCoverageFailureCoordinate").SetValue(observer, later);
            observer.SetCompleteCellObservationPhase("coast");
            observer.FreezeAppearanceBudgetObservation();
            foreach (string field in new[] { "_completeCellFailureCoordinate", "_appearanceBudgetFailureTarget", "_terrainDispatchFailureTarget" })
                if ((Vector2Int)Field(field).GetValue(observer) != first)
                    throw new InvalidOperationException("Coast failure replaced first input failure: " + field);
            if ((int)Field("_completeCellFailureCount").GetValue(observer) != prefix)
                throw new InvalidOperationException("Frozen first-failure prefix changed");
        }
        finally { UnityEngine.Object.DestroyImmediate(fixture); }
        File.WriteAllLines(summaryPath, new[] {
            "# Edit-only pure observer eligibility regression; utc=" + DateTime.UtcNow.ToString("O") +
                "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
            "result\toldArithmeticThrows\tnoOwnerRejected\tzeroEligible\tnegativeEligible\totherSignedCoordinatesEligible",
            "PASS\tTrue\tTrue\tTrue\tTrue\tTrue",
            "# firstFailureRetention=PASS noFailureDoesNotFreeze=True inputFreezes=True coastCannotReplace=True immutablePrefix=True inactiveFixtureDestroyed=True"
        });
    }

    private static void WriteR2DetailBindingRegression(string summaryPath)
    {
        // note: Exercise the production binding helpers on temporary TerrainData only; builtin textures stand in for binding identity without loading or changing any approved asset/profile/scene.
        TerrainData data = new TerrainData { hideFlags = HideFlags.HideAndDontSave, heightmapResolution = 33 };
        try
        {
            bool firstGrid = YQGeneratedWorldEnvironment.EnsureStreamedDetailResolution(data);
            bool firstPrototype = YQGeneratedWorldEnvironment.EnsureStreamedDetailPrototype(data, Texture2D.whiteTexture);
            int[,] density = new int[64, 64];
            density[0, 0] = 1;
            density[63, 63] = 2;
            data.SetDetailLayer(0, 0, 0, density);
            bool repeatedGrid = YQGeneratedWorldEnvironment.EnsureStreamedDetailResolution(data);
            bool repeatedPrototype = YQGeneratedWorldEnvironment.EnsureStreamedDetailPrototype(data, Texture2D.whiteTexture);
            int[,] retained = data.GetDetailLayer(0, 0, 64, 64, 0);
            for (int z = 0; z < 64; z++)
                for (int x = 0; x < 64; x++)
                    if (retained[z, x] != density[z, x])
                        throw new InvalidOperationException("Equivalent detail configuration changed a density sample");
            if (!firstGrid || !firstPrototype || repeatedGrid || repeatedPrototype)
                throw new InvalidOperationException("First/equivalent detail setter decisions are incorrect");
            // note: Cover fields omitted from Unity's prototype equality so reuse cannot preserve an incompatible visual binding.
            DetailPrototype[] changed = data.detailPrototypes;
            changed[0].alignToGround = 0.25f;
            data.detailPrototypes = changed;
            if (!YQGeneratedWorldEnvironment.EnsureStreamedDetailPrototype(data, Texture2D.whiteTexture))
                throw new InvalidOperationException("Changed alignment was incorrectly reused");
            changed = data.detailPrototypes;
            changed[0].positionJitter = 0.25f;
            data.detailPrototypes = changed;
            if (!YQGeneratedWorldEnvironment.EnsureStreamedDetailPrototype(data, Texture2D.whiteTexture))
                throw new InvalidOperationException("Changed jitter was incorrectly reused");
            if (!YQGeneratedWorldEnvironment.EnsureStreamedDetailPrototype(data, Texture2D.blackTexture) ||
                data.detailPrototypes[0].prototypeTexture != Texture2D.blackTexture)
                throw new InvalidOperationException("Changed texture was incorrectly reused");
            data.SetDetailResolution(32, 8);
            if (!YQGeneratedWorldEnvironment.EnsureStreamedDetailResolution(data) ||
                data.detailResolution != 64 || data.detailResolutionPerPatch != 16)
                throw new InvalidOperationException("Changed detail grid was incorrectly reused");
            File.WriteAllLines(summaryPath, new[] {
                "# Edit-only temporary TerrainData binding regression; utc=" + DateTime.UtcNow.ToString("O") +
                    "; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
                "result\tfirstGrid\tfirstPrototype\trepeatedGrid\trepeatedPrototype\tdensitySamplesPreserved\tchangedAlignment\tchangedJitter\tchangedTexture\tchangedGrid",
                "PASS\tTrue\tTrue\tFalse\tFalse\t4096\tPASS\tPASS\tPASS\tPASS"
            });
        }
        finally
        {
            // note: The probe owns the only temporary TerrainData; never destroy the builtin texture fixtures.
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static void BeginR2IdleAllocationStackCapture(string receiptPath, bool pauseScriptWatcher)
    {
        // note: Preserve inactive user settings and stop at the first Editor update after 150 ms; actual elapsed time is reported.
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling ||
            UnityEditor.EditorApplication.isUpdating || UnityEngine.Profiling.Profiler.enabled ||
            UnityEngine.Profiling.Profiler.enableBinaryLog || UnityEditorInternal.ProfilerDriver.deepProfiling ||
            File.Exists("Temp/YQ_R2_GPU_BUDGET.request"))
            throw new InvalidOperationException("Idle allocation stacks require idle Edit, inactive profiler, and no GPU request");
        bool previousStacks = UnityEngine.Profiling.Profiler.enableAllocationCallstacks;
        string previousLog = UnityEngine.Profiling.Profiler.logFile;
        FileSystemWatcher scriptWatcher = null;
        bool previousWatcherEnabled = false;
        if (pauseScriptWatcher)
        {
            // note: A bounded ownership experiment pauses only the existing project watcher; restore its exact state on every exit.
            Type bootstrap = Type.GetType("YQEditorAutoRefreshBootstrap, Assembly-CSharp-Editor", true);
            scriptWatcher = bootstrap.GetField("_scriptWatcher", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as FileSystemWatcher;
            if (scriptWatcher == null) throw new InvalidOperationException("Project script watcher unavailable for ownership experiment");
            previousWatcherEnabled = scriptWatcher.EnableRaisingEvents;
        }
        var report = new StringBuilder();
        report.AppendLine("# Edit-only idle allocation stacks utc=" + DateTime.UtcNow.ToString("O") +
            " assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
            " requestedSeconds=0.150 previousAllocationCallstacks=" + previousStacks +
            " pauseProjectScriptWatcher=" + pauseScriptWatcher + " previousWatcherEnabled=" + previousWatcherEnabled +
            " profilerOverheadIncluded=true gameplayCertification=false");
        R2FrameTraceCapture capture = null;
        try
        {
            if (scriptWatcher != null) scriptWatcher.EnableRaisingEvents = false;
            UnityEngine.Profiling.Profiler.enableAllocationCallstacks = true;
            capture = new R2FrameTraceCapture(report);
            capture.RecordBoundary(Time.frameCount, false, 0f);
        }
        catch
        {
            capture?.Dispose();
            UnityEngine.Profiling.Profiler.enabled = false;
            UnityEngine.Profiling.Profiler.enableBinaryLog = false;
            UnityEngine.Profiling.Profiler.logFile = previousLog;
            UnityEngine.Profiling.Profiler.enableAllocationCallstacks = previousStacks;
            if (scriptWatcher != null) scriptWatcher.EnableRaisingEvents = previousWatcherEnabled;
            throw;
        }
        double started = UnityEditor.EditorApplication.timeSinceStartup;
        int ticks = 0;
        bool closed = false;
        UnityEditor.EditorApplication.CallbackFunction tick = null;
        UnityEditor.AssemblyReloadEvents.AssemblyReloadCallback reload = null;
        Action<UnityEditor.PlayModeStateChange> playChange = null;
        Action<string> close = reason =>
        {
            if (closed) return;
            closed = true;
            UnityEditor.EditorApplication.update -= tick;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= reload;
            UnityEditor.EditorApplication.playModeStateChanged -= playChange;
            try { capture.Dispose(); }
            finally
            {
                // note: Restore allocation stacks even if the native writer or receipt fails; never carry this setting into Play.
                UnityEngine.Profiling.Profiler.enableAllocationCallstacks = previousStacks;
                if (scriptWatcher != null) scriptWatcher.EnableRaisingEvents = previousWatcherEnabled;
                report.AppendLine("- terminal: reason=" + reason + " elapsedSeconds=" +
                    (UnityEditor.EditorApplication.timeSinceStartup - started).ToString("R", CultureInfo.InvariantCulture) +
                    " editorUpdates=" + ticks + " playing=" + UnityEditor.EditorApplication.isPlaying +
                    " profilerEnabled=" + UnityEngine.Profiling.Profiler.enabled +
                    " binaryLog=" + UnityEngine.Profiling.Profiler.enableBinaryLog +
                    " allocationCallstacksRestored=" + (UnityEngine.Profiling.Profiler.enableAllocationCallstacks == previousStacks) +
                    " scriptWatcherRestored=" + (scriptWatcher == null || scriptWatcher.EnableRaisingEvents == previousWatcherEnabled) +
                    " logFileRestored=" + (UnityEngine.Profiling.Profiler.logFile == previousLog));
                File.WriteAllText(receiptPath, report.ToString());
            }
        };
        tick = () =>
        {
            ticks++;
            double elapsed = UnityEditor.EditorApplication.timeSinceStartup - started;
            capture.RecordBoundary(Time.frameCount, false, (float)elapsed);
            if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling)
                close("interrupted");
            else if (elapsed >= 0.150d) close("bounded-window-complete");
        };
        reload = () => close("assembly-reload");
        playChange = state => close("play-mode-transition-" + state);
        UnityEditor.EditorApplication.update += tick;
        UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += reload;
        UnityEditor.EditorApplication.playModeStateChanged += playChange;
    }

    private static void WriteR2AllocationCallstacks(string frameRange, Guid metadataId, string summaryPath)
    {
        // note: Resolve representative stored GC.Alloc stacks on every recorded thread, bounded to eight frames and 64 samples per thread/frame.
        string[] bounds = frameRange.Split(':');
        if (bounds.Length != 2 || !int.TryParse(bounds[0], out int first) || !int.TryParse(bounds[1], out int last) ||
            first < UnityEditorInternal.ProfilerDriver.firstFrameIndex || last > UnityEditorInternal.ProfilerDriver.lastFrameIndex ||
            last < first || last - first >= 8)
            throw new InvalidOperationException("Allocation stack audit needs at most eight saved profiler frames");
        using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
        {
            writer.WriteLine("# Saved allocation call stacks; captureGuid=" + metadataId + " extractionAssemblyMvid=" +
                typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
                "; selection=evenly-spaced-at-most-64-per-thread-frame; stacks-resolve-at-most-48-addresses; idle-capture-is-not-runtime-proof");
            writer.WriteLine("profilerFrame\tunityFrame\tthreadIndex\tthreadId\tthreadName\tsampleIndex\tstartNs\tbytes\tstackAddresses\tresolvedStack");
            var addresses = new List<ulong>();
            int matched = 0;
            for (int frame = first; frame <= last; frame++)
            {
                long unityFrame;
                using (var main = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!main.valid || main.GetFrameMetaDataCount(metadataId, 0) == 0) continue;
                    var boundary = main.GetFrameMetaData<long>(metadataId, 0);
                    if (boundary.Length < 4) continue;
                    unityFrame = boundary[0];
                }
                matched++;
                for (int thread = 0; thread < 256; thread++)
                {
                    using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!view.valid) break;
                        int allocations = 0;
                        long totalBytes = 0;
                        for (int sample = 0; sample < view.sampleCount; sample++)
                        {
                            if (view.GetSampleName(sample) != "GC.Alloc") continue;
                            allocations++;
                            if (view.GetSampleMetadataCount(sample) > 0) totalBytes += view.GetSampleMetadataAsLong(sample, 0);
                        }
                        if (allocations == 0) continue;
                        int selectedLimit = Math.Min(64, allocations), selected = 0, ordinal = 0, withStacks = 0;
                        var uniqueStacks = new HashSet<string>(StringComparer.Ordinal);
                        for (int sample = 0; sample < view.sampleCount && selected < selectedLimit; sample++)
                        {
                            if (view.GetSampleName(sample) != "GC.Alloc") continue;
                            int target = selectedLimit == 1 ? 0 : (int)((long)selected * (allocations - 1) / (selectedLimit - 1));
                            if (ordinal++ != target) continue;
                            selected++;
                            addresses.Clear();
                            view.GetSampleCallstack(sample, addresses);
                            if (addresses.Count > 0) withStacks++;
                            var resolved = new StringBuilder();
                            var key = new StringBuilder();
                            for (int index = 0; index < Math.Min(48, addresses.Count); index++)
                            {
                                if (index > 0) { resolved.Append(" > "); key.Append(','); }
                                ulong address = addresses[index];
                                key.Append(address.ToString("X", CultureInfo.InvariantCulture));
                                var method = view.ResolveMethodInfo(address);
                                resolved.Append(method.methodName).Append(" [").Append(method.sourceFileName)
                                    .Append(':').Append(method.sourceFileLine).Append(']');
                            }
                            if (!uniqueStacks.Add(key.ToString())) continue;
                            writer.WriteLine(frame + "\t" + unityFrame + "\t" + thread + "\t" + view.threadId + "\t" +
                                (view.threadName ?? string.Empty).Replace('\t', ' ').Replace('\n', ' ') + "\t" + sample + "\t" +
                                view.GetSampleStartTimeNs(sample) + "\t" + (view.GetSampleMetadataCount(sample) > 0 ?
                                view.GetSampleMetadataAsLong(sample, 0).ToString(CultureInfo.InvariantCulture) : "NA") + "\t" +
                                addresses.Count + "\t" + resolved.ToString().Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' '));
                        }
                        writer.WriteLine("# threadSummary frame=" + frame + " thread=" + thread + " nativeId=" + view.threadId +
                            " allocationEvents=" + allocations + " totalBytes=" + totalBytes + " selected=" + selected +
                            " withCallstack=" + withStacks + " uniqueSelectedStacks=" + uniqueStacks.Count);
                    }
                }
            }
            writer.WriteLine("# matchedFrames=" + matched);
            if (matched == 0) throw new InvalidOperationException("Selected saved frames have no matching capture identity");
        }
    }

    private static void WriteR2SampleOwnerAudit(string frameRange, Guid metadataId, string summaryPath, bool includeAllThreads = false)
    {
        // note: At most eight explicitly selected saved frames identify resource owners or long CPU job/wait scopes; this never records new frames or measures GPU execution.
        string[] bounds = frameRange.Split(':');
        if (bounds.Length != 2 || !int.TryParse(bounds[0], out int first) || !int.TryParse(bounds[1], out int last) ||
            first < UnityEditorInternal.ProfilerDriver.firstFrameIndex || last > UnityEditorInternal.ProfilerDriver.lastFrameIndex || last < first || last - first >= 8)
            throw new InvalidOperationException("Sample owner audit needs a valid saved profiler range of at most eight frames");
        using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
        {
            writer.WriteLine("# Saved capture UI/native owner metadata only; assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
                "; profilerFrames=" + frameRange + "; includeAllThreads=" + includeAllThreads + "; inclusive scopes overlap; absent metadata does not identify an owner.");
            writer.WriteLine("profilerFrame\tunityFrame\tthread\tsampleIndex\tsampleStartNs\tsampleMs\tmetadataCount\tmetadata\tscopePath" + (includeAllThreads ? "\tthreadIndex\tthreadId" : string.Empty));
            int matchedFrames = 0;
            for (int frame = first; frame <= last; frame++)
            {
                long unityFrame;
                using (var main = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, 0))
                {
                    if (!main.valid || main.GetFrameMetaDataCount(metadataId, 0) == 0)
                        continue;
                    var boundary = main.GetFrameMetaData<long>(metadataId, 0);
                    if (boundary.Length < 4)
                        continue;
                    unityFrame = boundary[0];
                }
                matchedFrames++;
                for (int thread = 0; thread < 256; thread++)
                {
                    using (var view = UnityEditorInternal.ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!view.valid)
                            break;
                        if (!includeAllThreads && thread != 0 && (view.threadName ?? string.Empty).IndexOf("Render", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;
                        if (includeAllThreads)
                            writer.WriteLine("# threadInventory frame=" + frame + " index=" + thread + " samples=" + view.sampleCount +
                                " threadId=" + view.threadId + " name=" + (view.threadName ?? string.Empty).Replace('\t', ' ').Replace('\n', ' '));
                        var ancestors = new Stack<KeyValuePair<int, string>>();
                        for (int sample = 0; sample < view.sampleCount; sample++)
                        {
                            while (ancestors.Count > 0 && sample > ancestors.Peek().Key)
                                ancestors.Pop();
                            string name = view.GetSampleName(sample) ?? "<unnamed>";
                            string scopePath = (ancestors.Count > 0 ? ancestors.Peek().Value + " > " : string.Empty) + name;
                            int descendants = view.GetSampleChildrenCountRecursive(sample);
                            if (descendants > 0)
                                ancestors.Push(new KeyValuePair<int, string>(sample + descendants, scopePath));
                            // note: Allocation samples can have near-zero duration; retain their recorded caller path and byte metadata in this Edit-only saved-capture audit.
                            bool allocationOrCollection = includeAllThreads && name.StartsWith("GC.", StringComparison.Ordinal);
                            // note: Retain tiny lifecycle subcalls so the saved audit can measure their cumulative cost rather than treating filtered samples as free.
                            bool lifecycleSubcall = includeAllThreads && name.StartsWith("YQPlayerFollowingSemanticChunkStreamer.Lifecycle", StringComparison.Ordinal);
                            // note: Budget-denied activation may repeatedly scan content queues; retain both existing scan markers to attribute their cumulative cost from saved frames.
                            bool contentCandidateScan = includeAllThreads &&
                                (name == "YQPlayerFollowingSemanticChunkStreamer.TryGetReadyHardViewContentCandidate" ||
                                 name == "YQPlayerFollowingSemanticChunkStreamer.TryGetReadyHardPreparationContentCandidate");
                            // note: Keep every saved delayed-coroutine child, including tiny destruction calls, so their sum can distinguish cumulative work from unmarked parent cost.
                            bool delayedCoroutineChild = includeAllThreads && scopePath.IndexOf(" > CoroutinesDelayedCalls", StringComparison.Ordinal) >= 0;
                            if (!allocationOrCollection && !lifecycleSubcall && !contentCandidateScan && !delayedCoroutineChild && name.IndexOf("UI", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Panel", StringComparison.OrdinalIgnoreCase) < 0 &&
                                name.IndexOf("GameView", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Gfx", StringComparison.OrdinalIgnoreCase) < 0 &&
                                name.IndexOf("Buffer", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("CreateResource", StringComparison.OrdinalIgnoreCase) < 0 &&
                                name.IndexOf("Semaphore", StringComparison.OrdinalIgnoreCase) < 0 &&
                                (!includeAllThreads || (view.GetSampleTimeMs(sample) <= 1f &&
                                    name.IndexOf("Job", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("Wait", StringComparison.OrdinalIgnoreCase) < 0)))
                                continue;
                            int count = view.GetSampleMetadataCount(sample);
                            var metadata = new StringBuilder();
                            var fields = view.GetMarkerMetadataInfo(view.GetSampleMarkerId(sample));
                            for (int index = 0; index < Math.Min(count, 8); index++)
                            {
                                if (index > 0) metadata.Append(" | ");
                                if (fields != null && index < fields.Length)
                                    metadata.Append(fields[index].name).Append('(').Append(fields[index].type).Append(")=");
                                string value = view.GetSampleMetadataAsString(sample, index) ?? string.Empty;
                                metadata.Append(value.Length <= 512 ? value : value.Substring(0, 512) + "[truncated]");
                                // note: Unity formats resource handles as names and can return <No Name>; retain their typed numeric identities to match buffer creation to the submitting owner.
                                if (fields != null && index < fields.Length)
                                {
                                    var type = fields[index].type;
                                    if (type == Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt64)
                                        metadata.Append(" [rawUInt64=").Append(unchecked((ulong)view.GetSampleMetadataAsLong(sample, index))).Append(']');
                                    else if (type == Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int64)
                                        metadata.Append(" [rawInt64=").Append(view.GetSampleMetadataAsLong(sample, index)).Append(']');
                                    else if (type == Unity.Profiling.LowLevel.ProfilerMarkerDataType.UInt32)
                                        metadata.Append(" [rawUInt32=").Append(unchecked((uint)view.GetSampleMetadataAsInt(sample, index))).Append(']');
                                    else if (type == Unity.Profiling.LowLevel.ProfilerMarkerDataType.Int32)
                                        metadata.Append(" [rawInt32=").Append(view.GetSampleMetadataAsInt(sample, index)).Append(']');
                                }
                            }
                            writer.WriteLine(frame + "\t" + unityFrame + "\t" + view.threadName + "\t" + sample + "\t" + view.GetSampleStartTimeNs(sample) + "\t" +
                                view.GetSampleTimeMs(sample).ToString("F3", CultureInfo.InvariantCulture) + "\t" + count + "\t" +
                                metadata.ToString().Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ') + "\t" + scopePath.Replace('\t', ' ').Replace('\n', ' ') +
                                (includeAllThreads ? "\t" + thread + "\t" + view.threadId : string.Empty));
                        }
                    }
                }
            }
            writer.WriteLine("# matchedFrames=" + matchedFrames);
            if (matchedFrames == 0)
                throw new InvalidOperationException("Selected saved frames have no matching witness identity");
        }
    }

    private static void WriteR2AssetSessionSnapshot(string label, string summaryPath)
    {
        // note: Three explicit snapshots bracket ordinary Continue; this census is diagnostic overhead, never frame-performance or readiness certification.
        if (label != "edit-before-play" && label != "title-before-continue" && label != "gameplay-released")
            throw new InvalidOperationException("Unknown bounded resource session snapshot label");
        using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
        {
            YQProfileSaveSystem profile = YQProfileSaveSystem.Instance;
            GeneratedWorldPlanRecord plan = WorldStateManager.Instance != null ? WorldStateManager.Instance.State?.generatedWorldPlan : null;
            writer.WriteLine("# Resource session census utc=" + DateTime.UtcNow.ToString("O") + " label=" + label +
                " playing=" + UnityEditor.EditorApplication.isPlaying + " unityFrame=" + Time.frameCount +
                " titleFlowComplete=" + YQTitleScreenUI.StartupFlowComplete +
                " gameplayReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased +
                " profileId=" + (profile != null ? profile.ActiveProfileId : string.Empty) +
                " worldSeed=" + (plan != null ? plan.worldSeed : string.Empty) +
                " acceptedV2Hash=" + (plan != null && plan.spatialPlanV2 != null ? plan.spatialPlanV2.contentHash : string.Empty));
            writer.WriteLine("# assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId +
                "; already loaded object estimates and cache identities only; no asset load/unload requested; not GPU residency.");
            writer.WriteLine("kind\towner\tassetPath\tcount\testimatedBytes");
            foreach (Type resourceType in new[] { typeof(Mesh), typeof(Texture), typeof(TerrainData) })
            {
                UnityEngine.Object[] resources = Resources.FindObjectsOfTypeAll(resourceType);
                var sizes = new List<KeyValuePair<long, UnityEngine.Object>>(resources.Length);
                long total = 0;
                foreach (UnityEngine.Object resource in resources)
                {
                    long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(resource);
                    total += bytes;
                    sizes.Add(new KeyValuePair<long, UnityEngine.Object>(bytes, resource));
                }
                writer.WriteLine("resourceTotal\t" + resourceType.Name + "\t\t" + resources.Length + "\t" + total);
                sizes.Sort((left, right) => right.Key.CompareTo(left.Key));
                for (int index = 0; index < Math.Min(20, sizes.Count); index++)
                {
                    UnityEngine.Object resource = sizes[index].Value;
                    writer.WriteLine("resource\t" + resource.name.Replace('\t', ' ').Replace('\n', ' ') + "\t" +
                        UnityEditor.AssetDatabase.GetAssetPath(resource) + "\t1\t" + sizes[index].Key);
                }
            }
            YQRuntimeWorldAssetRegistry cachedRoot = YQRuntimeWorldAssetRegistry.CachedInstanceForDiagnostics;
            writer.WriteLine("cachedRoot\t" + (cachedRoot != null ? cachedRoot.name : "[none]") + "\t\t0\t0");
            foreach (YQRuntimeWorldAssetRegistry registry in Resources.FindObjectsOfTypeAll<YQRuntimeWorldAssetRegistry>())
            {
                writer.WriteLine("loadedRegistry\t" + registry.name + "\t" + UnityEditor.AssetDatabase.GetAssetPath(registry) +
                    "\t" + (registry.Entries != null ? registry.Entries.Count : 0) + "\t0");
                if (registry.CachedShardsForDiagnostics == null)
                    continue;
                // note: Distinguish the current registry's owned cache from assets merely retained by the Editor or another registry.
                foreach (var shard in registry.CachedShardsForDiagnostics)
                    writer.WriteLine("cachedShard\t" + registry.name + "\t" + shard.Key + "\t" +
                        (shard.Value != null && shard.Value.Entries != null ? shard.Value.Entries.Count : 0) + "\t0");
            }
        }
    }

    private static void WriteR2AssetRootAudit(string rootsFile, string summaryPath)
    {
        // note: Bound this opt-in audit to workspace analysis inputs; malformed or oversized lists cannot broaden the Editor scan.
        string inputPath = Path.GetFullPath(rootsFile);
        string outputRoot = Path.GetFullPath("outputs") + Path.DirectorySeparatorChar;
        if (!inputPath.StartsWith(outputRoot, StringComparison.OrdinalIgnoreCase) ||
            !inputPath.EndsWith(".roots.txt", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Asset-root audit input must be a workspace .roots.txt file");
        byte[] inputBytes = File.ReadAllBytes(inputPath);
        string[] lines = Encoding.UTF8.GetString(inputBytes).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var roots = new List<string>();
        var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines)
        {
            string root = line.Trim().Replace('\\', '/');
            if (root.StartsWith("#", StringComparison.Ordinal))
                continue;
            if (!root.StartsWith("Assets/", StringComparison.Ordinal) || root.Contains("..") || root.Contains("\t"))
                throw new InvalidOperationException("Invalid project asset root in audit input");
            if (unique.Add(root))
                roots.Add(root);
        }
        if (roots.Count == 0 || roots.Count > 512)
            throw new InvalidOperationException("Asset-root audit requires 1..512 unique roots");
        roots.Sort(StringComparer.OrdinalIgnoreCase);
        using (var writer = new StreamWriter(summaryPath, false, Encoding.UTF8))
        using (SHA256 hash = SHA256.Create())
        {
            writer.WriteLine("# POST-PLAY Editor dependency metadata utc=" + DateTime.UtcNow.ToString("O") +
                "; recursive references are not proof of live runtime use or GPU residency; no asset load/unload requested.");
            writer.WriteLine("# roots=" + roots.Count + " inputSha256=" +
                BitConverter.ToString(hash.ComputeHash(inputBytes)).Replace("-", string.Empty).ToLowerInvariant() +
                " assemblyMvid=" + typeof(YQSemanticChunkRuntimeVerification).Assembly.ManifestModule.ModuleVersionId);
            writer.WriteLine("kind\towner\tdependency\tentries");
            foreach (string root in roots)
            {
                bool present = !string.IsNullOrEmpty(UnityEditor.AssetDatabase.AssetPathToGUID(root));
                writer.WriteLine((present ? "root" : "missingRoot") + "\t" + root + "\t\t0");
                if (!present)
                    continue;
                // note: Retain the recursive dependency relationship per requested root, so nested prefab references cannot be mistaken for unrelated pack expansion.
                string[] dependencies = UnityEditor.AssetDatabase.GetDependencies(root, true);
                Array.Sort(dependencies, StringComparer.OrdinalIgnoreCase);
                foreach (string dependency in dependencies)
                    writer.WriteLine("dependency\t" + root + "\t" + dependency + "\t0");
            }
            foreach (YQRuntimeWorldAssetRegistry registry in Resources.FindObjectsOfTypeAll<YQRuntimeWorldAssetRegistry>())
            {
                string assetPath = UnityEditor.AssetDatabase.GetAssetPath(registry);
                writer.WriteLine("loadedRegistry\t" + registry.name.Replace('\t', ' ').Replace('\n', ' ') + "\t" +
                    assetPath + "\t" + (registry.Entries != null ? registry.Entries.Count : 0));
            }
        }
    }
#endif

    // note: Retain a bounded per-frame timeline so wall-clock hitches can be matched to Unity and streamer CPU samples.
    private struct R2FrameTimingDiagnostic
    {
        public int UnityFrame;
        public bool Coast;
        public float WallSeconds;
        public float UnityDeltaSeconds;
        public float MainThreadSeconds;
        public float BehaviourUpdateSeconds;
        public float PhysicsSeconds;
        public float GcSeconds;
        public float StreamerUpdateSeconds;
        public float StreamerLateUpdateSeconds;
        public float CurrentThreadSemaphoreWaitSeconds;
        public float StreamingWorkSeconds;
        public float StreamingStageSeconds;
        public string StreamingStage;
        public long AllocatedBytes;
        public int Gen0Collections;
        public int Gen1Collections;
        public int Gen2Collections;
    }

    // note: Opt-in coast observations distinguish physical capsule blocking from readiness rejection without changing movement or formatting every frame.
    private struct R2CoastMotionDiagnostic
    {
        public int UnityFrame;
        public Vector3 Start;
        public Vector3 End;
        public Vector3 Velocity;
        public float MotorDeltaSeconds;
        public Vector3 LastRequestedMove;
        public Vector3 LastActualMove;
        public CollisionFlags Flags;
        public bool Rejected;
        public int SideContacts;
        public string ContactOwner;
        public Vector3 ContactPoint;
        public Vector3 ContactNormal;
    }

    private sealed class ProfilerMarkerFrameCapture
    {
        public string Category;
        public string Name;
        public string UnitType;
        public int Priority;
        public ProfilerRecorder Recorder;
        public ProfilerRecorder CurrentThreadRecorder;
        public float MainThreadPeakSeconds;
        public float WorstWallFrameSeconds;
        public float WorstCoastFrameSeconds;
        public float CurrentThreadPeakSeconds;
        public float CurrentThreadWorstWallFrameSeconds;
        public float CurrentThreadWorstCoastFrameSeconds;
        public readonly float[] HighSpeedSamples = new float[128];
        public int HighSpeedSampleCount;
        public readonly float[] CoastSamples = new float[512];
        public int CoastSampleCount;

        public float ReadSeconds()
        {
            // note: ProfilerRecorder stores timing metrics in nanoseconds; keep unavailable markers at zero and report their validity separately.
            return Recorder.Valid && Recorder.Count > 0
                ? (float)Math.Max(0d, Recorder.LastValue * 1e-9d)
                : 0f;
        }

        public float ReadCurrentThreadSeconds()
        {
            // note: Keep the semaphore total separate from the calling Unity thread so worker wait time is not mistaken for a main-thread stall.
            return CurrentThreadRecorder.Valid && CurrentThreadRecorder.Count > 0
                ? (float)Math.Max(0d, CurrentThreadRecorder.LastValue * 1e-9d)
                : 0f;
        }

        public void RecordFrame(bool highSpeed, float seconds)
        {
            // note: Keep bounded per-run distributions without allocating in the measured movement loops.
            if (highSpeed)
            {
                if (HighSpeedSampleCount < HighSpeedSamples.Length)
                    HighSpeedSamples[HighSpeedSampleCount++] = seconds;
            }
            else if (CoastSampleCount < CoastSamples.Length)
                CoastSamples[CoastSampleCount++] = seconds;
        }

        public float Percentile(bool highSpeed, float percentile)
        {
            float[] samples = highSpeed ? HighSpeedSamples : CoastSamples;
            int count = highSpeed ? HighSpeedSampleCount : CoastSampleCount;
            if (count <= 0)
                return 0f;
            float[] sorted = new float[count];
            Array.Copy(samples, sorted, count);
            Array.Sort(sorted);
            int index = Mathf.Clamp(Mathf.CeilToInt(percentile * count) - 1, 0, count - 1);
            return sorted[index];
        }
    }

    private static float CalculatePercentile(float[] samples, int sampleCount, float percentile)
    {
        // note: Sort an after-run copy so reporting p50/p95 never mutates or allocates inside the timed frame loop.
        if (sampleCount <= 0)
            return 0f;
        float[] sorted = new float[sampleCount];
        Array.Copy(samples, sorted, sampleCount);
        Array.Sort(sorted);
        int index = Mathf.Clamp(Mathf.CeilToInt(percentile * sampleCount) - 1, 0, sampleCount - 1);
        return sorted[index];
    }

    // note: Attribute frame-budget spikes to the active acceptance harness separately from the production motor and streamer.
    private static readonly ProfilerMarker VerificationFrameMarker =
        new ProfilerMarker("YQSemanticChunkRuntimeVerification.LateUpdate");
    // note: Separate the speed witness's live-player, ground, visible-content, and turn-buffer probes from the game work they qualify.
    private static readonly ProfilerMarker Speed260TraversabilityMarker =
        new ProfilerMarker("YQSemanticChunkRuntimeVerification.Speed260Traversability");
    private static readonly ProfilerMarker Speed260GroundCoverageMarker =
        new ProfilerMarker("YQSemanticChunkRuntimeVerification.Speed260GroundCoverage");
    private static readonly ProfilerMarker Speed260VisualCoverageMarker =
        new ProfilerMarker("YQSemanticChunkRuntimeVerification.Speed260VisualCoverage");
    private static readonly ProfilerMarker Speed260TurnBufferMarker =
        new ProfilerMarker("YQSemanticChunkRuntimeVerification.Speed260TurnBuffer");

    // note: Keep the expensive traversal acceptance probe explicitly marker/menu gated so normal Play Mode never launches the chunk-stream test.
    private const bool RuntimeVerificationEnabled = true;
    // note: The probe validates destination-cell ground publication independently from the broader decoration frontier.
    // note: Full authored materialization can legitimately exceed four minutes while Unity streams reviewed sites; keep the probe alive until that transaction settles.
    private const float StartupTimeoutSeconds = 600f;
    private const float MovementTimeoutSeconds = 45f;
    // note: Initial coverage can pass before the bounded retained ring has admitted a non-view recovery owner; wait for that real owner instead of weakening the fault-injection witness.
    private const double RecoveryWitnessTimeoutSeconds = 90.0;
    // note: Streamed ecology is real Unity work and can exceed the short movement timeout; recovery stages use one bounded wall-clock budget instead of timing out a live publisher mid-transaction.
    private const float PublicationRecoveryTimeoutSeconds = 300f;
    // note: The injected appearance failure should exhaust four short retries promptly; fail this liveness probe without waiting through the unrelated publication watchdog.
    private const float AppearanceRetryExhaustionTimeoutSeconds = 30f;
    // note: A held appearance worker should be observable promptly on a reused physical owner; keep the lag witness bounded so a lost scheduler lease cannot idle for five minutes.
    private const float AppearanceLagRecoveryTimeoutSeconds = 30f;
    // note: Natural unload travel uses its own wall-clock watchdog because real production movement may need several minutes to clear the retained ring.
    private const float UnloadRevisitTimeoutSeconds = 600f;
    // note: Recovery traversal changes ordinary motor direction after a bounded wall-clock stall so an unrelated blocked edge cannot monopolize the whole gate.
    private const float RecoveryNoProgressTimeoutSeconds = 8f;
    // note: A short physical trail lets the unload witness retrace narrow, obstacle-aware production-motor movement without cutting across a traversable cell.
    private const float ReturnWaypointSampleDistanceMeters = 5f;
    // note: Allow a stalled return waypoint enough collision-resolved lateral steps to route around a wide prop while remaining inside its ready owner.
    private const int SameOwnerReturnBypassMaxAttempts = 8;
    private const float SameOwnerReturnBypassStepMeters = 3f;
    private const float ActiveTerrainBarrierObservationTimeoutSeconds = 30f;
    // note: Full-world atomic persistence is deferred from Update and gets its own bounded commit budget instead of inflating the per-frame streaming budget.
    private const float PersistenceCommitBudgetSeconds = 0.10f;
    // note: Allow a small wall-clock sampling margin for editor timer jitter while retaining the raw measured peak in the report.
    private const float StreamingMeasurementToleranceSeconds = 0.010f;
    // note: Run the 150/300 m/s, reversal, diagonal, deadline, and frame-budget probes in the production acceptance pass so skipped stress cannot be reported as completion.
    private static readonly bool EnforceG07ThroughputGates = true;
    // note: Use normal camera-relative movement keys to route around a blocked frontier without teleporting or changing streamer scheduling policy.
    private static readonly UnityEngine.InputSystem.Key[] RecoveryCardinalDirections =
    {
        UnityEngine.InputSystem.Key.W,
        UnityEngine.InputSystem.Key.D,
        UnityEngine.InputSystem.Key.S,
        UnityEngine.InputSystem.Key.A
    };
    private static bool _started;
    private static bool _completed;
    // note: A bounded motor-only run can qualify the changed 260 m/s witness without entering unrelated world and recovery phases.
    private static bool _focusedSpeed260MotorWitnessOnly;
    // note: Run the named publication-fault witnesses without repeating the separate long motor and unload/revisit suites.
    private static bool _focusedR1PublicationWitnessOnly;
    // note: Pair the bounded R1 fault matrix with a real unload/revisit witness without rerunning the independent speed matrix.
    private static bool _focusedR1UnloadRevisitWitnessOnly;
    // note: Preserve the first nested coroutine failure so the terminal report names the failed traversal phase instead of only saying FAIL.
    private static string _lastNestedFailure = string.Empty;
    private double _nextLivenessReportAt;
    private bool _startupPredicateReported;
    private Stack<IEnumerator> _verificationIterators;
    // note: Append only new phase text so asynchronous heartbeat evidence is never overwritten by an older report snapshot.
    private static StringBuilder _activeReport;
    private static int _writtenReportCharacters;
    private static bool _terminalReportWritten;
    // note: Keep the verifier's explicitly queued keyboard current after native input processing so the production motor reads the event that was actually injected.
    private static UnityEngine.InputSystem.Keyboard _verificationKeyboard;
    private static bool _verificationKeyboardHooked;
    // note: Clone editor input routing only for the marker-gated run so focus changes cannot silently discard virtual keyboard events.
    private static UnityEngine.InputSystem.InputSettings _verificationOriginalInputSettings;
    private static UnityEngine.InputSystem.InputSettings _verificationTestInputSettings;

    public static bool IsRunning => _started;
    public static bool HasCompleted => _completed;

    public static void Begin()
    {
        BeginInternal(false);
    }

    public static void PrimeProfilerMarkerForCpuCapture()
    {
        // note: Register the verifier frame marker before the editor snapshots available profiler counters.
        using (VerificationFrameMarker.Auto())
        {
        }
    }

    private static void BeginInternal(
        bool focusedSpeed260MotorWitnessOnly,
        bool focusedR1PublicationWitnessOnly = false,
        bool focusedR1UnloadRevisitWitnessOnly = false)
    {
        if (!RuntimeVerificationEnabled || _started)
            return;
        _focusedSpeed260MotorWitnessOnly = focusedSpeed260MotorWitnessOnly;
        _focusedR1PublicationWitnessOnly = focusedR1PublicationWitnessOnly;
        _focusedR1UnloadRevisitWitnessOnly = focusedR1UnloadRevisitWitnessOnly;
        _started = true;
        _completed = false;
        _lastNestedFailure = string.Empty;
        _activeReport = null;
        _writtenReportCharacters = 0;
        _terminalReportWritten = false;
        GameObject root = new GameObject("YQ_SemanticChunkRuntimeVerification");
        DontDestroyOnLoad(root);
        root.AddComponent<YQSemanticChunkRuntimeVerification>();
    }

    public static bool TryBeginFromCurrentPlaySession()
    {
        // note: Refuse to launch from Edit Mode or duplicate an active probe; the menu is an explicit current-session entry point.
        if (!RuntimeVerificationEnabled || !Application.isPlaying || _started)
            return false;
        BeginInternal(false);
        return true;
    }

    public static bool TryBeginFocusedSpeed260WitnessFromCurrentPlaySession()
    {
        // note: Run only the production motor matrix and its abrupt 260 m/s boundary witness before attempting long world or recovery checks.
        if (!RuntimeVerificationEnabled || !Application.isPlaying || _started)
            return false;
        BeginInternal(true);
        return true;
    }

    public static bool TryBeginFocusedR1PublicationWitnessFromCurrentPlaySession()
    {
        // note: Start only the current session's R1 publication/recovery matrix and prevent a second active verifier.
        if (!RuntimeVerificationEnabled || !Application.isPlaying || _started)
            return false;
        BeginInternal(false, true);
        return true;
    }

    public static bool TryBeginFocusedR1UnloadRevisitWitnessFromCurrentPlaySession()
    {
        // note: Reuse the passing publication recovery matrix and add only its current-source physical unload/revisit gate.
        if (!RuntimeVerificationEnabled || !Application.isPlaying || _started)
            return false;
        BeginInternal(false, true, true);
        return true;
    }

    public static void RestartForEditor()
    {
        // note: Ignore duplicate editor dispatches while the current probe is active so a lingering file event cannot destroy its terminal receipt.
        if (!RuntimeVerificationEnabled || _started)
            return;
        // note: Replace only the completed unattended probe so a new marker can exercise the current authored scene after a code reload.
        YQSemanticChunkRuntimeVerification[] existing =
            FindObjectsByType<YQSemanticChunkRuntimeVerification>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int index = 0; index < existing.Length; index++)
            Destroy(existing[index].gameObject);
        _started = false;
        Begin();
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        // note: Disabling the verifier also closes its native profiler writer even before iterator destruction runs.
        R2FrameTraceCapture.StopActive();
#endif
    }

    private void OnDestroy()
    {
        // note: Unwind nested probes on manual stop as well as exceptions, releasing injected keys and publication holds in their finally blocks.
        if (!_completed)
        {
            _lastNestedFailure = "ABORTED_BEFORE_TERMINAL_REPORT";
            DisposeVerificationIterators();
            if (!_terminalReportWritten)
                AppendVerifierStatusToReport(_lastNestedFailure);
        }
        // note: Keep completion observable until the next explicit Begin resets this session.
        ReleaseVerificationKeyboardHook();
        _started = false;
    }

    private void Update()
    {
        if (!_completed && Time.realtimeSinceStartupAsDouble >= _nextLivenessReportAt)
        {
            // note: A low-rate heartbeat proves the verifier GameObject remains alive while its startup predicate is being evaluated.
            _nextLivenessReportAt = Time.realtimeSinceStartupAsDouble + 2.0;
            YQPlayerFollowingSemanticChunkStreamer liveStreamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
            // note: Include only bounded scheduler counters so a stuck settle wait reveals ownership state without serializing streamed objects.
            string coverageFailure = string.Empty;
            bool requiredCoverageReady = liveStreamer != null && liveStreamer.TryValidateRequiredCoverage(out coverageFailure);
            string visualCoverageFailure = string.Empty;
            bool visualCoverageReady = liveStreamer != null && liveStreamer.TryValidateVisualCoverage(out visualCoverageFailure);
            // note: Format owner progress only at the existing bounded heartbeat, never on ordinary motor probes.
            if (!visualCoverageReady && liveStreamer != null)
                visualCoverageFailure += liveStreamer.DescribeLastVisualCoverageOwnerProgress();
            YQInvestorPlayerMotor[] liveMotors = FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Transform livePlayer = YQInvestorPlayerMotor.ActiveMotor != null ? YQInvestorPlayerMotor.ActiveMotor.transform : null;
            AppendVerifierStatusToReport("HEARTBEAT" +
                (liveStreamer == null
                    ? string.Empty
                    : " queue=" + liveStreamer.QueuedChunkCount +
                      ",pendingTerrain=" + liveStreamer.PendingTerrainCollisionCount +
                      ",pendingPaint=" + liveStreamer.PendingTerrainPaintCount +
                      ",generating=" + liveStreamer.IsGenerating +
                      ",lifecycle=" + liveStreamer.HasPendingLifecycleWork +
                      ",active=" + liveStreamer.ActiveChunkCount +
                      ",retained=" + liveStreamer.RetainedChunkCount +
                      ",physical=" + liveStreamer.PhysicalChunkCount +
                      ",terrainAdmission=" + liveStreamer.TerrainAdmissionDiagnostics +
                      ",semantic=" + liveStreamer.SemanticChunkCount +
                      ",coverage=" + requiredCoverageReady +
                      ",coverageFailure=" + coverageFailure +
                      ",visualCoverage=" + visualCoverageReady +
                      ",visualCoverageFailure=" + visualCoverageFailure +
                      ",motors=" + liveMotors.Length +
                      ",playerPosition=" + (livePlayer != null ? livePlayer.position.ToString() : "<null>")));
            if (!_startupPredicateReported && Time.realtimeSinceStartupAsDouble >= 5.0)
            {
                _startupPredicateReported = true;
                YQPlayerFollowingSemanticChunkStreamer streamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
                WorldStateManager manager = WorldStateManager.Instance;
                YQGeneratedWorldRuntimeBuilder builder = YQGeneratedWorldRuntimeBuilder.Instance ??
                    FindFirstObjectByType<YQGeneratedWorldRuntimeBuilder>(FindObjectsInactive.Include);
                bool planPresent = manager != null && manager.State != null && manager.State.generatedWorldPlan != null &&
                    manager.State.generatedWorldPlan.semanticChunks != null && manager.State.generatedWorldPlan.semanticChunks.Count > 0;
                // note: Snapshot every readiness predicate from the live verifier component so a coroutine sentinel cannot conceal which production owner is absent.
                AppendVerifierStatusToReport("STARTUP_PREDICATE streamer=" + (streamer != null) +
                    ",manager=" + (manager != null) +
                    ",player=" + (YQInvestorPlayerMotor.ActiveMotor != null) +
                    ",authoritativePlayer=" + (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative) +
                    ",builder=" + (builder != null) +
                    ",plan=" + planPresent +
                    ",materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    ",recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                    ",gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                    ",presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
            }
        }
    }

    private void LateUpdate()
    {
        // note: Inspect after the production motor's Update; queue input for the next normal Input System update without manually advancing its clock.
        if (!_completed)
        {
            using (VerificationFrameMarker.Auto())
                AdvanceVerificationOneFrame();
        }
    }

    private void DisposeVerificationIterators()
    {
        // note: One failing cleanup must not prevent outer finally blocks from releasing fault injection or flushing the partial report.
        while (_verificationIterators != null && _verificationIterators.Count > 0)
        {
            try { (_verificationIterators.Pop() as IDisposable)?.Dispose(); }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }

    private void Start()
    {
        // note: Keep nested verification waits under a main-thread frame driver so every yielded phase advances deterministically and remains observable.
        _verificationIterators = new Stack<IEnumerator>();
        _verificationIterators.Push(RunVerification());
    }

    private void AdvanceVerificationOneFrame()
    {
        if (_verificationIterators == null)
            return;
        while (_verificationIterators.Count > 0)
        {
            IEnumerator current = _verificationIterators.Peek();
            object yielded;
            try
            {
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    _verificationIterators.Pop();
                    continue;
                }
                yielded = current.Current;
            }
            catch (Exception exception)
            {
                // note: Nested exceptions precede root disposal, while a root exception runs its finally before escaping MoveNext; persist the reason in both cases.
                string failureText = exception.GetType().Name + ": " + exception.Message;
                _lastNestedFailure = failureText;
                Debug.LogError("[YQSemanticChunkRuntimeVerification] " + exception);
                AppendNestedFailureToReport(failureText);
                DisposeVerificationIterators();
                // note: The root coroutine was disposed with the failed nested iterator, so finalize the report explicitly instead of relying on its finally block.
                _started = false;
                _completed = true;
                return;
            }
            if (yielded is IEnumerator nested)
            {
                _verificationIterators.Push(nested);
                continue;
            }
            return;
        }
        Destroy(gameObject);
    }

    private IEnumerator RunVerification()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# YourQuest Semantic Chunk Runtime Verification");
        report.AppendLine("- verificationScope: " + (_focusedSpeed260MotorWitnessOnly
            ? "focused production motor matrix and abrupt speed-260 boundary witness; remaining R1/G08 rows not run"
            : _focusedR1UnloadRevisitWitnessOnly
                ? "focused G08-R1 publication recovery plus physical unload/revisit; speed-260 motor and remaining G08 rows not run"
                : _focusedR1PublicationWitnessOnly
                ? "focused G08-R1 publication recovery matrix; R2 motor, unload/revisit, and remaining G08 rows not run"
                : "G08-R1 publication recovery with R2 motor and unload/revisit witnesses"));
        report.AppendLine("- startedUtc: " + DateTime.UtcNow.ToString("O"));
        AppendExecutionIdentity(report);
        // note: Capture the executing measurement setup so profiler and viewport changes cannot silently qualify a focused receipt.
        report.AppendLine("- measurementSetup: unity=" + Application.unityVersion +
            " os=" + SystemInfo.operatingSystem + " cpu=" + SystemInfo.processorType +
            " logicalProcessors=" + SystemInfo.processorCount + " memoryMb=" + SystemInfo.systemMemorySize +
            " gpu=" + SystemInfo.graphicsDeviceName + " graphicsApi=" + SystemInfo.graphicsDeviceType +
            " viewport=" + Screen.width + "x" + Screen.height + " focused=" + Application.isFocused);
#if UNITY_EDITOR
        report.AppendLine("- measurementProfilerSetup: enabled=" + UnityEngine.Profiling.Profiler.enabled +
            " binaryLog=" + UnityEngine.Profiling.Profiler.enableBinaryLog +
            " allocationCallstacks=" + UnityEngine.Profiling.Profiler.enableAllocationCallstacks +
            " deepProfiling=" + UnityEditorInternal.ProfilerDriver.deepProfiling +
            " traceRequested=" + File.Exists(R2FrameTraceRequest));
#endif
        YQPlayerFollowingSemanticChunkStreamer streamer = null;
        Transform player = null;
        YQInvestorPlayerMotor motor = null;
        CharacterController characterController = null;
        WorldStateManager manager = null;
        YQGeneratedWorldRuntimeBuilder builder = null;
        WorldState testWorld = null;
        Vector3 startingPosition = Vector3.zero;
        bool success = false;
        bool recoveryCasesVerified = false;
        bool unloadRevisitPassed = false;
        bool unloadRevisitInputInconclusive = false;
        bool verificationInconclusive = false;
        bool postMotorPublicationWorkContractFailed = false;
        string postMotorPublicationWorkContractFailure = string.Empty;
        string postMotorVisualCoverageFailure = string.Empty;
        string overlayFeatureId = string.Empty;
        string overlayTargetObjectId = string.Empty;

        try
        {
            // note: Persist an immediate diagnostic state so an explicit menu run is observable while normal startup is still pending.
            report.AppendLine("- status: WAITING_FOR_STARTUP");
            VerifyMacroFieldHashParity(report);
            WriteReport(report, false, false);
            double startupDeadline = Time.realtimeSinceStartupAsDouble + StartupTimeoutSeconds;
            bool startupDiagnosticWritten = false;
            double nextStartupPredicateTraceAt = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < startupDeadline)
            {
                streamer = FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
                manager = WorldStateManager.Instance;
                player = ResolveAuthoritativePlayer();
                testWorld = manager != null ? manager.State : testWorld;
                // note: Include inactive scene objects so the authored builder is recognized before its first Awake/Update frame.
                YQGeneratedWorldRuntimeBuilder sceneBuilder =
                    FindFirstObjectByType<YQGeneratedWorldRuntimeBuilder>(FindObjectsInactive.Include);
                builder = YQGeneratedWorldRuntimeBuilder.Instance ?? sceneBuilder;
                bool livePlanUnavailable = testWorld == null || testWorld.generatedWorldPlan == null ||
                    testWorld.generatedWorldPlan.semanticChunks == null || testWorld.generatedWorldPlan.semanticChunks.Count == 0;
                bool productionReady = streamer != null && manager != null && builder != null &&
                    !livePlanUnavailable && builder.HasMaterializedCurrentWorld &&
                    !builder.InitialGenerationRecoveryRequired &&
                    !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked &&
                    YourQuestTutorialAutoBootstrap.GameplayPresentationReleased &&
                    player != null;
                if (!productionReady && Time.realtimeSinceStartupAsDouble >= nextStartupPredicateTraceAt)
                {
                    // note: Persist the exact in-process startup references so a status-writer mismatch identifies the blocking predicate without waiting for timeout.
                    nextStartupPredicateTraceAt = Time.realtimeSinceStartupAsDouble + 2.0;
                    report.AppendLine("- startupPredicateTrace: streamer=" + (streamer != null) +
                        ",manager=" + (manager != null) +
                        ",player=" + (player != null) +
                        ",builder=" + (builder != null) +
                        ",plan=" + !livePlanUnavailable +
                        ",materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                        ",recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                        ",gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                        ",presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
                    WriteReport(report, false, false);
                }
                if (!startupDiagnosticWritten && Time.realtimeSinceStartupAsDouble >= startupDeadline - StartupTimeoutSeconds + 5.0)
                {
                    // note: Capture the first live readiness mismatch so a stalled unattended probe identifies ownership state without waiting for the full timeout.
                    YQInvestorPlayerMotor[] liveMotors = FindObjectsByType<YQInvestorPlayerMotor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    report.AppendLine("- startupDiagnostic: streamer=" + (streamer != null) +
                        ", manager=" + (manager != null) +
                        ", player=" + (player != null) +
                        ", builder=" + (builder != null) +
                        ", plan=" + !livePlanUnavailable +
                        ", materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                        ", recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                        ", gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked +
                        ", presentationReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased +
                        ", activeMotor=" + (YQInvestorPlayerMotor.ActiveMotor != null) +
                        ", activeMotorAuthoritative=" + (YQInvestorPlayerMotor.ActiveMotor != null && YQInvestorPlayerMotor.ActiveMotor.IsAuthoritative) +
                        ", motorCount=" + liveMotors.Length);
                    WriteReport(report, false, false);
                    startupDiagnosticWritten = true;
                }
                // note: Begin only after the accepted world is fully materialized, gameplay is released, and the authoritative player owns movement; no rebuild or synthetic fixture can produce a PASS.
                if (productionReady)
                    break;
                yield return null;
            }

            if (streamer == null || testWorld == null || player == null || builder == null ||
                !builder.HasMaterializedCurrentWorld || builder.InitialGenerationRecoveryRequired ||
                YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked ||
                !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
            {
                // note: Preserve the production-owner state when startup times out so an incomplete authored build cannot be mistaken for a streaming edge.
                report.AppendLine("- liveStartupFailure: builderPresent=" + (builder != null) +
                    " builderMaterialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    " recoveryRequired=" + (builder != null && builder.InitialGenerationRecoveryRequired) +
                    " gameplayLocked=" + YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked);
                report.AppendLine("- liveStartupFailurePredicates: streamer=" + (streamer != null) +
                    ", world=" + (testWorld != null) +
                    ", player=" + (player != null) +
                    ", builder=" + (builder != null) +
                    ", plan=" + (testWorld != null && testWorld.generatedWorldPlan != null) +
                    ", materialized=" + (builder != null && builder.HasMaterializedCurrentWorld) +
                    ", gameplayReleased=" + YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
                throw new InvalidOperationException("live streamer, test world, or authoritative player did not become available");
            }
            report.AppendLine("- sourceMode: live-authored-world");
            report.AppendLine("- status: STARTUP_READY");
            // note: Prove a selected profile's accepted terrain snapshot was reused when its persisted identity matches the loaded world.
            YQProfileSaveSystem profileSave = YQProfileSaveSystem.Instance;
            AppendWorldIdentity(report, manager.State, profileSave);
            string loadedTerrainSnapshotJson = string.Empty;
            bool loadedTerrainSnapshotDocument = profileSave != null &&
                profileSave.TryGetLoadedAuxiliaryDocument(
                    YQGeneratedWorldTerrain.ProfileTerrainSnapshotDocumentId,
                    out loadedTerrainSnapshotJson);
            YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord loadedTerrainSnapshot = null;
            if (loadedTerrainSnapshotDocument && !string.IsNullOrWhiteSpace(loadedTerrainSnapshotJson))
            {
                try
                {
                    loadedTerrainSnapshot = JsonUtility.FromJson<YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord>(
                        loadedTerrainSnapshotJson);
                }
                catch (ArgumentException)
                {
                    // note: A malformed optional snapshot is reported as unavailable and cannot be mistaken for an accepted restore.
                }
            }
            WorldState startupWorld = manager.State;
            GeneratedWorldPlanRecord startupPlan = startupWorld != null
                ? startupWorld.generatedWorldPlan
                : null;
            bool loadedTerrainSnapshotMatchesWorld = loadedTerrainSnapshot != null &&
                loadedTerrainSnapshot.schemaVersion == 1 && loadedTerrainSnapshot.hasTerrain &&
                profileSave != null && startupWorld != null && startupWorld.worldIdentity != null && startupPlan != null &&
                string.Equals(loadedTerrainSnapshot.ownerProfileId, profileSave.ActiveProfileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loadedTerrainSnapshot.worldId, startupWorld.worldIdentity.worldId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(loadedTerrainSnapshot.worldSeed, startupPlan.worldSeed, StringComparison.Ordinal);
            bool profileTerrainRestoreSucceeded = builder.LastProfileTerrainRestoreSucceeded;
            if (loadedTerrainSnapshotMatchesWorld && !profileTerrainRestoreSucceeded)
            {
                report.AppendLine("- acceptedSaveTerrainStartup: FAIL reason=" +
                    (string.IsNullOrWhiteSpace(builder.LastProfileTerrainRestoreFailureReason)
                        ? "matching saved terrain was not restored"
                        : builder.LastProfileTerrainRestoreFailureReason));
                WriteReport(report, false, false);
                throw new InvalidOperationException("selected profile's matching terrain snapshot was not restored");
            }
            report.AppendLine("- acceptedSaveTerrainStartup: " +
                (loadedTerrainSnapshotMatchesWorld && profileTerrainRestoreSucceeded
                    ? "PASS"
                    : loadedTerrainSnapshot != null && loadedTerrainSnapshot.hasTerrain
                        ? "NOT_APPLICABLE_SNAPSHOT_DOES_NOT_MATCH_ACTIVE_WORLD"
                        : "NOT_APPLICABLE_NO_ACCEPTED_TERRAIN_SNAPSHOT"));
            report.AppendLine("- profileTerrainSnapshotMatchesWorld: " + loadedTerrainSnapshotMatchesWorld);
            report.AppendLine("- profileTerrainRestoreSucceeded: " + profileTerrainRestoreSucceeded);
            // note: Flush readiness before any synchronous preflight work so a stalled acceptance helper cannot hide that production startup already succeeded.
            WriteReport(report, false, false);
            yield return null;

            // note: Validate canonical procedural connectors before movement so a shared border cannot hide asymmetric route or water records.
            if (!YQContinuousWorldFeatureAuthority.TryValidatePortalSymmetry(testWorld.generatedWorldPlan.worldSeed, streamer.chunkWorldSize, out string portalFailure))
                throw new InvalidOperationException("continuation portal symmetry failed: " + portalFailure);
            report.AppendLine("- continuationPortalSymmetry: PASS");
            if (!YQContinuousWorldFeatureAuthority.TryValidateBoundaryContinuation(out string boundaryContinuationFailure))
            {
                // note: Preserve the focused continuation failure in the report so a failed unattended probe identifies the exact direction/cell instead of only reporting FAIL.
                report.AppendLine("- authoredFeatureBoundaryContinuation: FAIL: " + boundaryContinuationFailure);
                throw new InvalidOperationException("authored feature boundary continuation failed: " + boundaryContinuationFailure);
            }
            report.AppendLine("- authoredFeatureBoundaryContinuation: PASS");
            // note: Yield after connector validation so the editor remains responsive before the first terrain refresh and queue-settle wait.
            yield return null;

            motor = player.GetComponentInChildren<YQInvestorPlayerMotor>(true);
            characterController = player.GetComponentInChildren<CharacterController>(true);
            startingPosition = player.position;
            if (motor == null || !motor.enabled || !motor.IsAuthoritative)
                throw new InvalidOperationException("authoritative production motor must already be enabled for physical traversal verification");
            if (characterController == null || !characterController.enabled)
                throw new InvalidOperationException("authoritative CharacterController is unavailable for physical traversal verification");

            // note: Observe readiness at production handoff; a verifier-only settling period would conceal what the player actually sees.
            report.AppendLine("- status: INITIAL_FRONTIER_OBSERVATION");
            // note: Compare the observed production camera with the bootstrap release receipt before input begins.
            report.AppendLine("- initialCamera: fov=" + motor.playerCamera.fieldOfView +
                " position=" + motor.playerCamera.transform.position + " rotation=" + motor.playerCamera.transform.eulerAngles +
                " timeScale=" + Time.timeScale);
            WriteReport(report, false, false);
            if (!streamer.TryValidateRequiredCoverage(out string initialCoverageFailure))
                throw new InvalidOperationException("initial required chunk coverage failed: " + initialCoverageFailure);
            report.AppendLine("- initialRequiredCoverage: PASS");
            // note: The production handoff must already include a complete one-cell preparation ring; this is a startup contract, not a verifier-only settling delay.
            if (!streamer.TryValidatePreparedViewEnvelope(out string initialPreparedViewFailure))
                throw new InvalidOperationException("initial prepared view envelope failed: " + initialPreparedViewFailure);
            report.AppendLine("- initialPreparedViewEnvelope: PASS radius=" + streamer.VisualPreparationRadius);
            if (!streamer.TryValidateVisualCoverage(out string initialVisualCoverageFailure))
                throw new InvalidOperationException("initial visual coverage failed: " + initialVisualCoverageFailure);
            report.AppendLine("- initialVisualCoverage: PASS radius=" + streamer.GuaranteedVisualRadius +
                " prewarm=" + streamer.VisualPrewarmRadius +
                " distance=" + streamer.GuaranteedVisualDistanceMeters.ToString("0", CultureInfo.InvariantCulture) + "m");
            Vector2Int originChunk = streamer.CurrentChunk;

            // note: Record the production handoff preconditions before input is injected, separating a disabled motor from an untraversable starting capsule.
            report.AppendLine("- productionMotorPreflight: enabledBeforeVerifier=" + motor.enabled +
                ", authoritative=" + motor.IsAuthoritative +
                ", cameraPivot=" + (motor.cameraPivot != null) +
                ", playerCamera=" + (motor.playerCamera != null) +
                ", controller=" + (characterController != null && characterController.enabled) +
                ", modalBlocked=" + RuntimeModalUiBlocker.IsBlocked +
                ", timeScale=" + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
            if (!streamer.TryValidateCurrentTraversability(
                    player.position,
                    characterController.radius,
                    characterController.skinWidth,
                    out string initialTraversabilityFailure))
                throw new InvalidOperationException("initial traversability failed: " + initialTraversabilityFailure);
            report.AppendLine("- initialTraversability: PASS");

            // note: Keep the authoritative motor enabled throughout the probe so queue waits and static checks cannot freeze or replace gameplay ownership.
            bool productionMotorMatrixPassed = false;
            bool productionMotorInputInconclusive = false;
            if (_focusedR1PublicationWitnessOnly)
            {
                // note: R1's appearance-lag witness drives the real motor itself; skip the independent movement suite here.
                productionMotorMatrixPassed = true;
                report.AppendLine("- productionMotorTraversal: NOT_RUN_FOCUSED_R1_SCOPE");
            }
            else
            {
                yield return VerifyProductionMotorTraversal(
                    streamer,
                    motor,
                    player,
                    characterController,
                    report,
                    (passed, inputInconclusive) =>
                    {
                        productionMotorMatrixPassed = passed;
                        productionMotorInputInconclusive = inputInconclusive;
                    });
            }
            if (_focusedSpeed260MotorWitnessOnly)
            {
                // note: End after the persisted motor/boundary measurements so a failed short witness does not spend minutes in unrelated acceptance phases.
                report.AppendLine("- focusedProductionMotorWitness: " +
                    (productionMotorInputInconclusive ? "INCONCLUSIVE" : productionMotorMatrixPassed ? "PASS" : "FAIL"));
                report.AppendLine("- remainingR1AndG08Rows: NOT_RUN_FOCUSED_SCOPE");
                verificationInconclusive = productionMotorInputInconclusive;
                if (productionMotorMatrixPassed || productionMotorInputInconclusive)
                {
                    success = true;
                    report.AppendLine("- result: " + (productionMotorInputInconclusive ? "INCONCLUSIVE" : "PASS"));
                }
                yield break;
            }
            if (productionMotorInputInconclusive)
            {
                // note: Keep publication recovery evidence independent from an unavailable background input device; this is not a locomotion pass.
                report.AppendLine("- productionMotorTraversal: INPUT_DELIVERY_INCONCLUSIVE");
                WriteReport(report, false, false);
            }
            else if (!productionMotorMatrixPassed)
            {
                // note: Keep independent publication recovery evidence runnable after a real motor or frame-budget failure; the final acceptance still fails on this saved result.
                report.AppendLine("- productionMotorTraversal: FAIL per-gate evidence recorded; continuing independent publication recovery checks");
                WriteReport(report, false, false);
            }

            if (!productionMotorInputInconclusive)
            {
                bool postMotorVisualCoveragePassed = streamer.TryValidateVisualCoverage(out postMotorVisualCoverageFailure);
                if (postMotorVisualCoveragePassed)
                {
                    postMotorPublicationWorkContractFailed = false;
                    report.AppendLine("- postMotorVisualCoverage: PASS");
                    report.AppendLine("- postMotorPublicationWorkContract: PASS incompleteVisibleChunks=0");
                }
                else
                {
                    // note: Keep the immediate coverage miss visible in the report while distinguishing live recoverable publication from a stranded visible stage.
                    bool publicationWorkContractPassed = streamer.TryValidateVisiblePublicationWorkContracts(
                        out int incompleteVisibleChunks,
                        out string publicationWorkContractFailure);
                    postMotorPublicationWorkContractFailed = !publicationWorkContractPassed;
                    postMotorPublicationWorkContractFailure = publicationWorkContractFailure;
                    report.AppendLine("- postMotorVisualCoverage: INCOMPLETE " + postMotorVisualCoverageFailure);
                    report.AppendLine("- postMotorPublicationWorkContract: " +
                        (publicationWorkContractPassed ? "PASS" : "FAIL") +
                        " incompleteVisibleChunks=" + incompleteVisibleChunks +
                        (publicationWorkContractPassed ? string.Empty : " failure=" + publicationWorkContractFailure));
                }
            }
            else
                report.AppendLine("- postMotorVisualCoverage: NOT_RUN_INPUT_DELIVERY_INCONCLUSIVE");
            WriteReport(report, false, false);

            if (!TryValidateAdjacentContracts(testWorld.generatedWorldPlan, originChunk, Mathf.Max(1, streamer.generationRadius), out string contractFailure, out float contractDelta))
                throw new InvalidOperationException("semantic edge contract continuity failed: " + contractFailure);
            report.AppendLine("- adjacentEdgeContracts: PASS");
            report.AppendLine("- adjacentEdgeMaximumContractFloatDelta: " + contractDelta.ToString("0.000000", CultureInfo.InvariantCulture));
            int originSemanticCount = testWorld.generatedWorldPlan.semanticChunks.Count;
            report.AppendLine("- originChunk: " + originChunk);
            report.AppendLine("- originSemanticCount: " + originSemanticCount);

            // note: Probe the shared world-space biome field for a real neighboring transition so palette continuity is validated beyond alphamap normalization.
            Terrain biomeTerrain = streamer.AuthoredTerrain;
            YQContinuousWorldCellAuthority biomeAuthority = new YQContinuousWorldCellAuthority(
                testWorld.generatedWorldPlan.worldSeed,
                biomeTerrain,
                null,
                testWorld.generatedWorldPlan,
                streamer.chunkWorldSize);
            if (!TryValidateBiomeTransition(biomeAuthority, biomeTerrain, out string biomeFailure, out float biomeDelta))
                throw new InvalidOperationException("biome transition continuity failed: " + biomeFailure);
            report.AppendLine("- biomeTransition: PASS");
            report.AppendLine("- biomeTransitionMaximumEightMetreWeightDelta: " + biomeDelta.ToString("0.000", CultureInfo.InvariantCulture));
            if (!TryValidateGenerationOrderIndependence(
                    testWorld.generatedWorldPlan,
                    biomeAuthority,
                    streamer.chunkWorldSize,
                    out string orderFailure))
                throw new InvalidOperationException("generation-order independence failed: " + orderFailure);
            report.AppendLine("- generationOrderIndependence: PASS");
            if (!TryValidateDistantProceduralSynthesis(
                    testWorld.generatedWorldPlan,
                    biomeAuthority,
                    biomeTerrain,
                    streamer.chunkWorldSize,
                    out string distantSynthesisFailure,
                    out float minimumDistantRelief,
                    out float maximumDistantRelief,
                    out int distantBiomeCount,
                    out int distantRoadCells,
                    out int distantRiverCells,
                    out int distantLandmarkCells))
                throw new InvalidOperationException("distant procedural synthesis failed: " + distantSynthesisFailure);
            report.AppendLine("- distantProceduralSynthesis: PASS");
            report.AppendLine("- distantReliefRangeMetres: " + minimumDistantRelief.ToString("0.00", CultureInfo.InvariantCulture) + ".." + maximumDistantRelief.ToString("0.00", CultureInfo.InvariantCulture));
            report.AppendLine("- distantRegionalBiomeCount: " + distantBiomeCount);
            report.AppendLine("- distantRoadCells: " + distantRoadCells);
            report.AppendLine("- distantRiverCells: " + distantRiverCells);
            report.AppendLine("- distantIndependentLandmarkCells: " + distantLandmarkCells);
            if (!TryValidateCanonicalChunkIdentity(testWorld.generatedWorldPlan, out string identityFailure))
                throw new InvalidOperationException("canonical chunk identity failed: " + identityFailure);
            report.AppendLine("- canonicalChunkIdentity: PASS");

            Terrain verificationTerrain = streamer.AuthoredTerrain;
            if (verificationTerrain != null && verificationTerrain.terrainData != null)
            {
                Vector3 edgeAttempt = player.position;
                // note: Stand inside the first continuation cell so the raycast validates its collider surface instead of Unity's numerically fragile shared border.
                edgeAttempt.x = verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x + streamer.chunkWorldSize * 0.5f + 0.1f;
                // note: Keep the edge probe inside the authored terrain's longitudinal span so it selects a true east neighbor rather than a non-overlapping diagonal tile from a persisted far position.
                edgeAttempt.z = verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z * 0.5f;
                float halfCell = streamer.chunkWorldSize * 0.5f;
                Vector3 originCornerProbe = new Vector3(
                    verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x + halfCell,
                    startingPosition.y,
                    verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + halfCell);
                Vector3[] continuationProbePositions =
                {
                    edgeAttempt,
                    new Vector3(verificationTerrain.transform.position.x - halfCell - 0.1f, startingPosition.y, startingPosition.z),
                    new Vector3(startingPosition.x, startingPosition.y, verificationTerrain.transform.position.z - halfCell - 0.1f),
                    new Vector3(startingPosition.x, startingPosition.y, verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + halfCell + 0.1f),
                    originCornerProbe,
                    new Vector3(originCornerProbe.x, startingPosition.y, verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z - halfCell),
                    new Vector3(verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x - halfCell, startingPosition.y, originCornerProbe.z)
                };
                Terrain extension = null;
                int readyPhysicalNeighborCount = 0;
                for (int probeIndex = 0; probeIndex < continuationProbePositions.Length; probeIndex++)
                {
                    bool ready = TryGetReadyStreamedTerrainAt(
                        streamer,
                        verificationTerrain,
                        continuationProbePositions[probeIndex],
                        out Terrain candidateTerrain);
                    if (probeIndex == 0)
                        extension = candidateTerrain;
                    if (ready)
                        readyPhysicalNeighborCount++;
                }
                bool physicalEnvelopeReady = readyPhysicalNeighborCount == continuationProbePositions.Length;
                if (physicalEnvelopeReady)
                {
                if (edgeAttempt.x <= verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x)
                    throw new InvalidOperationException("player-following terrain did not continue beyond the authored terrain tile");
                if (extension == null || extension == verificationTerrain || extension.GetComponent<TerrainCollider>() == null || !extension.GetComponent<TerrainCollider>().enabled)
                    throw new InvalidOperationException("extended chunk has no enabled terrain collider");
                // note: Confirm the streamed TerrainData contract before comparing samples; a matching edge on incompatible resolutions is still an invalid continuation.
                float originSpacing = Mathf.Max(
                    verificationTerrain.terrainData.size.x / Mathf.Max(1f, verificationTerrain.terrainData.heightmapResolution - 1f),
                    verificationTerrain.terrainData.size.z / Mathf.Max(1f, verificationTerrain.terrainData.heightmapResolution - 1f));
                float extensionSpacing = Mathf.Max(
                    extension.terrainData.size.x / Mathf.Max(1f, extension.terrainData.heightmapResolution - 1f),
                    extension.terrainData.size.z / Mathf.Max(1f, extension.terrainData.heightmapResolution - 1f));
                if (Mathf.Abs(extension.terrainData.size.x - streamer.chunkWorldSize) > 0.01f ||
                    Mathf.Abs(extension.terrainData.size.z - streamer.chunkWorldSize) > 0.01f ||
                    Mathf.Abs(extensionSpacing - originSpacing) > 0.01f)
                    throw new InvalidOperationException("extended chunk TerrainData is not using the origin-compatible spacing and cell size");
                float maximumSharedEdgeDelta = 0f;
                float sharedX = verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x;
                float overlapMinZ = Mathf.Max(verificationTerrain.transform.position.z, extension.transform.position.z);
                float overlapMaxZ = Mathf.Min(verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z, extension.transform.position.z + extension.terrainData.size.z);
                for (int sampleZ = 0; sampleZ <= 16; sampleZ++)
                {
                    float worldZ = Mathf.Lerp(overlapMinZ, overlapMaxZ, sampleZ / 16f);
                    float originHeight = verificationTerrain.SampleHeight(new Vector3(sharedX, 0f, worldZ));
                    float extensionHeight = extension.SampleHeight(new Vector3(sharedX, 0f, worldZ));
                    maximumSharedEdgeDelta = Mathf.Max(maximumSharedEdgeDelta, Mathf.Abs(originHeight - extensionHeight));
                }
                report.AppendLine("- eastSharedEdgeMaximumDelta: " + maximumSharedEdgeDelta.ToString("0.000"));
                if (maximumSharedEdgeDelta > 0.25f)
                {
                    // note: Record the canonical and continuation transforms plus the seam samples before rejecting a terrain mismatch.
                    Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Seam detail canonical=" + verificationTerrain.transform.position + " size=" + verificationTerrain.terrainData.size + " continuation=" + extension.transform.position + " size=" + extension.terrainData.size + " sharedX=" + sharedX + " originY=" + verificationTerrain.SampleHeight(new Vector3(sharedX, 0f, overlapMinZ)) + " extensionY=" + extension.SampleHeight(new Vector3(sharedX, 0f, overlapMinZ)));
                    throw new InvalidOperationException("origin-to-continuation shared edge diverged by " + maximumSharedEdgeDelta.ToString("0.000") + " metres");
                }
                Physics.SyncTransforms();
                if (!Physics.Raycast(new Vector3(edgeAttempt.x, 300f, edgeAttempt.z), Vector3.down, out RaycastHit edgeHit, 600f))
                {
                    TerrainCollider extensionCollider = extension.GetComponent<TerrainCollider>();
                    Vector3 extensionOrigin = extension.transform.position;
                    Vector3 extensionSize = extension.terrainData.size;
                    float sampledProbeHeight = extension.SampleHeight(edgeAttempt);
                    RaycastHit directColliderRaycast = default;
                    bool directColliderHit = extensionCollider != null && extensionCollider.Raycast(
                        new Ray(new Vector3(player.position.x, 300f, player.position.z), Vector3.down),
                        out directColliderRaycast,
                        600f);
                    report.AppendLine("- edgeProbeFailurePosition: " + edgeAttempt);
                    report.AppendLine("- edgeProbeTerrainBounds: " + extensionOrigin + " size=" + extensionSize);
                    report.AppendLine("- edgeProbeSampleHeight: " + sampledProbeHeight.ToString("0.000", CultureInfo.InvariantCulture));
                    report.AppendLine("- edgeProbeColliderState: enabled=" + (extensionCollider != null && extensionCollider.enabled) + " active=" + extension.gameObject.activeInHierarchy + " layer=" + extension.gameObject.layer);
                    report.AppendLine("- edgeProbeColliderBounds: " + (extensionCollider != null ? extensionCollider.bounds.ToString() : "<null>"));
                    report.AppendLine("- edgeProbeColliderDataMatch: " + (extensionCollider != null && extensionCollider.terrainData == extension.terrainData));
                    report.AppendLine("- edgeProbeDirectColliderHit: " + directColliderHit + (directColliderHit ? " y=" + directColliderRaycast.point.y.ToString("0.000", CultureInfo.InvariantCulture) : string.Empty));
                    throw new InvalidOperationException("extended chunk terrain did not produce a raycastable ground surface");
                }
                float extensionMinimumHeight = float.MaxValue;
                float extensionMaximumHeight = float.MinValue;
                for (int sampleZ = 0; sampleZ <= 4; sampleZ++)
                {
                    for (int sampleX = 0; sampleX <= 4; sampleX++)
                    {
                        float sampleXWorld = extension.transform.position.x + extension.terrainData.size.x * sampleX / 4f;
                        float sampleZWorld = extension.transform.position.z + extension.terrainData.size.z * sampleZ / 4f;
                        float height = extension.SampleHeight(new Vector3(sampleXWorld, 0f, sampleZWorld));
                        extensionMinimumHeight = Mathf.Min(extensionMinimumHeight, height);
                        extensionMaximumHeight = Mathf.Max(extensionMaximumHeight, height);
                    }
                }
                report.AppendLine("- sampledExtensionHeightRange: " + extensionMinimumHeight.ToString("0.000") + ".." + extensionMaximumHeight.ToString("0.000"));
                if (extensionMaximumHeight - extensionMinimumHeight < 0.05f)
                    throw new InvalidOperationException("continuation terrain was effectively flat across the sampled extension tile");
                report.AppendLine("- extendedTerrainPositionX: " + edgeAttempt.x.ToString("0.00"));
                report.AppendLine("- extendedTerrainCollider: enabled");
                report.AppendLine("- extendedTerrainRaycastY: " + edgeHit.point.y.ToString("0.00"));
                report.AppendLine("- extendedTerrainHeightRange: " + extensionMinimumHeight.ToString("0.00") + ".." + extensionMaximumHeight.ToString("0.00"));
                if (extension.terrainData.terrainLayers == null || extension.terrainData.terrainLayers.Length < 3)
                    throw new InvalidOperationException("extended terrain did not inherit the required biome layers");
                // note: Inspect only an already traversable extension; the continuation probe must never wait for paint or create background demand.
                if (streamer.IsTerrainPaintPending(extension))
                    throw new InvalidOperationException("published continuation Terrain still has a pending appearance pass");
                float[,,] alphaSample = extension.terrainData.GetAlphamaps(0, 0, 1, 1);
                float alphaTotal = 0f;
                for (int layer = 0; layer < alphaSample.GetLength(2); layer++)
                {
                    float value = alphaSample[0, 0, layer];
                    if (float.IsNaN(value) || float.IsInfinity(value))
                        throw new InvalidOperationException("extended terrain alphamap contains a non-finite value");
                    alphaTotal += value;
                }
                report.AppendLine("- extendedTerrainAlphaSampleTotal: " + alphaTotal.ToString("0.000"));
                // note: Unity stores terrain alphamaps in quantized texture channels, so the readback can drift a few percent while remaining correctly normalized for rendering.
                if (alphaTotal < 0.97f || alphaTotal > 1.03f)
                    throw new InvalidOperationException("extended terrain alphamap is not normalized");
                if (!TrySampleAlphaBoundaryDelta(verificationTerrain, extension, out float alphaBoundaryDelta))
                    throw new InvalidOperationException("published continuation Terrain had no readable biome boundary sample");
                report.AppendLine("- originExtensionAlphaBoundaryMaximumDelta: " + alphaBoundaryDelta.ToString("0.000", CultureInfo.InvariantCulture));
                if (alphaBoundaryDelta > 0.08f)
                {
                    // note: Preserve both edge texels and layer identities when a seam fails so an authored normalization or layer-order mismatch is distinguishable from a stale paint coroutine.
                    report.AppendLine("- alphaBoundaryDiagnostics: " + DescribeAlphaBoundary(verificationTerrain, extension));
                    throw new InvalidOperationException("origin and extension biome alphamaps diverged at their shared edge");
                }
                string[] remainingEdges = { "west", "south", "north" };
                for (int edgeIndex = 0; edgeIndex < remainingEdges.Length; edgeIndex++)
                {
                    string edge = remainingEdges[edgeIndex];
                    Vector3 remainingEdgeAttempt = startingPosition;
                    if (edge == "west")
                        remainingEdgeAttempt.x = verificationTerrain.transform.position.x - streamer.chunkWorldSize * 0.5f - 0.1f;
                    else if (edge == "south")
                        remainingEdgeAttempt.z = verificationTerrain.transform.position.z - streamer.chunkWorldSize * 0.5f - 0.1f;
                    else
                        remainingEdgeAttempt.z = verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + streamer.chunkWorldSize * 0.5f + 0.1f;
                    Terrain edgeExtension = FindTerrainAt(remainingEdgeAttempt);
                    if (edgeExtension == null || edgeExtension == verificationTerrain)
                        throw new InvalidOperationException("streamed continuation terrain was not published after crossing the " + edge + " authored edge");
                    if (edgeExtension == null || edgeExtension == verificationTerrain || edgeExtension.GetComponent<TerrainCollider>() == null || !edgeExtension.GetComponent<TerrainCollider>().enabled)
                        throw new InvalidOperationException("" + edge + " continuation has no enabled terrain collider");
                    float edgeDelta = SampleSharedEdgeDelta(verificationTerrain, edgeExtension, edge);
                    report.AppendLine("- " + edge + "SharedEdgeMaximumDelta: " + edgeDelta.ToString("0.000"));
                    if (edgeDelta > 0.25f)
                        throw new InvalidOperationException(edge + " origin-to-continuation shared edge diverged by " + edgeDelta.ToString("0.000") + " metres");
                }

                // note: Exercise the first diagonal continuation explicitly; corner-only contact must use the world-space authority instead of copying an origin edge across the tile.
                Vector3 diagonalAttempt = startingPosition;
                diagonalAttempt.x = verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x + streamer.chunkWorldSize * 0.5f;
                diagonalAttempt.z = verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z + streamer.chunkWorldSize * 0.5f;
                Terrain diagonal = FindTerrainAt(diagonalAttempt);
                Terrain eastCandidate = FindTerrainAt(
                    new Vector3(diagonalAttempt.x, 0f, verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z - streamer.chunkWorldSize * 0.5f));
                Terrain northCandidate = FindTerrainAt(
                    new Vector3(verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x - streamer.chunkWorldSize * 0.5f, 0f, diagonalAttempt.z));
                if (diagonal == null || diagonal == verificationTerrain ||
                    eastCandidate == null || eastCandidate == verificationTerrain ||
                    northCandidate == null || northCandidate == verificationTerrain ||
                    diagonal.GetComponent<TerrainCollider>() == null || !diagonal.GetComponent<TerrainCollider>().enabled ||
                    eastCandidate.GetComponent<TerrainCollider>() == null || !eastCandidate.GetComponent<TerrainCollider>().enabled ||
                    northCandidate.GetComponent<TerrainCollider>() == null || !northCandidate.GetComponent<TerrainCollider>().enabled)
                    throw new InvalidOperationException("diagonal continuation terrain was not published after crossing the authored corner");
                if (diagonal == null || diagonal == verificationTerrain || diagonal.GetComponent<TerrainCollider>() == null || !diagonal.GetComponent<TerrainCollider>().enabled)
                    throw new InvalidOperationException("diagonal continuation has no enabled terrain collider");
                Vector3 originCorner = new Vector3(
                    verificationTerrain.transform.position.x + verificationTerrain.terrainData.size.x,
                    0f,
                    verificationTerrain.transform.position.z + verificationTerrain.terrainData.size.z);
                Terrain eastContinuation = FindTerrainAt(originCorner + new Vector3(halfCell, 0f, -halfCell));
                Terrain northContinuation = FindTerrainAt(originCorner + new Vector3(-halfCell, 0f, halfCell));
                if (eastContinuation == null || northContinuation == null)
                    throw new InvalidOperationException("diagonal seam probe could not resolve both cardinal continuation tiles");
                float eastCornerHeight = eastContinuation.SampleHeight(originCorner);
                float northCornerHeight = northContinuation.SampleHeight(originCorner);
                float cardinalCornerDelta = Mathf.Abs(eastCornerHeight - northCornerHeight);
                float diagonalCornerHeight = diagonal.SampleHeight(originCorner);
                float diagonalCornerDelta = Mathf.Max(
                    Mathf.Abs(diagonalCornerHeight - eastCornerHeight),
                    Mathf.Abs(diagonalCornerHeight - northCornerHeight));
                float maximumCornerDelta = Mathf.Max(cardinalCornerDelta, diagonalCornerDelta);
                report.AppendLine("- diagonalCornerMaximumDelta: " + maximumCornerDelta.ToString("0.000"));
                if (maximumCornerDelta > 0.25f)
                    throw new InvalidOperationException("diagonal continuation corner diverged by " + maximumCornerDelta.ToString("0.000") + " metres");
                report.AppendLine("- diagonalContinuation: PASS");
                }
                else
                {
                    if (!TryValidateCanonicalContinuationBoundarySamples(
                            streamer,
                            biomeAuthority,
                            verificationTerrain,
                            continuationProbePositions,
                            out int sampleCount,
                            out int demandedWorkContracts,
                            out string continuationFailure))
                        throw new InvalidOperationException("unmaterialized continuation authority failed: " + continuationFailure);
                    report.AppendLine("- originEdgePhysicalEnvelope: NOT_FULLY_PUBLISHED readyPhysicalNeighbors=" +
                        readyPhysicalNeighborCount + "/" + continuationProbePositions.Length);
                    report.AppendLine("- canonicalContinuationBoundarySamples: PASS samples=" + sampleCount +
                        " demandedWorkContracts=" + demandedWorkContracts);
                }
            }

            // note: Capture the initial continuation Terrain envelope before real motor travel can legitimately unload distant origin-edge owners.
            bool recoveryInputDeliveryInconclusive = false;
            yield return VerifyPublicationRecoveryMatrix(
                streamer,
                motor,
                player,
                characterController,
                report,
                (value, inputInconclusive, unloadPassed, unloadInputInconclusive) =>
                {
                    recoveryCasesVerified = value;
                    recoveryInputDeliveryInconclusive = inputInconclusive;
                    unloadRevisitPassed = unloadPassed;
                    unloadRevisitInputInconclusive = unloadInputInconclusive;
                });
            verificationInconclusive |= productionMotorInputInconclusive ||
                recoveryInputDeliveryInconclusive || unloadRevisitInputInconclusive;
            report.AppendLine("- r1PublicationRecoveryGate: " +
                (!recoveryCasesVerified ? "FAIL" : recoveryInputDeliveryInconclusive ? "INCONCLUSIVE" : "PASS"));
            WriteReport(report, false, false);
            if (!recoveryCasesVerified)
                throw new InvalidOperationException("R1 publication/recovery gates failed; inspect their individual gate rows");

            if (_focusedR1PublicationWitnessOnly && !_focusedR1UnloadRevisitWitnessOnly)
            {
                // note: Publish a terminal R1-only result after its complete fault matrix; leave all R2 and world-itinerary rows explicitly unrun.
                report.AppendLine("- focusedR1PublicationRecovery: " +
                    (recoveryInputDeliveryInconclusive ? "INCONCLUSIVE" : "PASS"));
                report.AppendLine("- remainingR2AndG08Rows: NOT_RUN_FOCUSED_SCOPE");
                success = true;
                yield break;
            }

            if (_focusedR1UnloadRevisitWitnessOnly)
            {
                // note: Stop after the current-source recovery and lifecycle witnesses; do not rerun the separate speed matrix or broaden into R3.
                bool focusedR1UnloadPassed = recoveryCasesVerified && unloadRevisitPassed;
                report.AppendLine("- focusedR1AndUnloadRevisit: " +
                    (verificationInconclusive ? "INCONCLUSIVE" : focusedR1UnloadPassed ? "PASS" : "FAIL") +
                    " recovery=" + recoveryCasesVerified +
                    " unloadRevisit=" + unloadRevisitPassed +
                    " inputInconclusive=" + verificationInconclusive);
                report.AppendLine("- remainingR2AndG08Rows: NOT_RUN_FOCUSED_SCOPE");
                success = recoveryCasesVerified && (unloadRevisitPassed || unloadRevisitInputInconclusive);
                yield break;
            }

            // note: Keep the R1 probe on the production motor path; synthetic teleports cannot prove continuous traversal or bounded publication.
            report.AppendLine("- productionMotorCoverage: OBSERVED_ONLY");
            report.AppendLine("- nearChunk: " + streamer.CurrentChunk);
            report.AppendLine("- nearSemanticCount: " + testWorld.generatedWorldPlan.semanticChunks.Count);
            int typedSiteCount = 0;
            for (int recordIndex = 0; recordIndex < testWorld.generatedWorldPlan.semanticChunks.Count; recordIndex++)
                typedSiteCount += testWorld.generatedWorldPlan.semanticChunks[recordIndex]?.sites?.Count ?? 0;
            report.AppendLine("- typedContinuationSiteCount: " + typedSiteCount);
            if (typedSiteCount < 2)
                throw new InvalidOperationException("multi-cell continuation did not produce intentionally distributed typed sites");
            report.AppendLine("- syntheticTeleportTraversal: NOT_RUN_PRODUCTION_MOTOR_REQUIRED");

            // note: R1 does not synthesize a terrain-edge start or ground correction; the production motor matrix above is the only locomotion subject.
            float normalSpeed = motor != null ? Mathf.Max(1f, motor.walkSpeed) : 6.8f;
            // note: Normal locomotion is already measured through the enabled production motor matrix; do not duplicate it with a verifier-owned CharacterController.Move loop.
            report.AppendLine("- normalTraversal: OBSERVED_PRODUCTION_MOTOR_MATRIX speed=" +
                normalSpeed.ToString("0.0", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);

            // note: Accept the 260 m/s clause only from measured production-motor input segments; the remaining G07 speed thresholds stay separate.
            report.AppendLine("- speed260ConstantInputSegmentsGate: " + (productionMotorInputInconclusive
                ? "INPUT_DELIVERY_INCONCLUSIVE"
                : productionMotorMatrixPassed ? "PASS_PRODUCTION_MOTOR" : "FAIL_PRODUCTION_MOTOR"));
            report.AppendLine("- speed150StraightGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed300StraightGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed300DirectionChangeGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed150DiagonalGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- speed300DeadlineGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            report.AppendLine("- streamingFrameBudgetGate: NOT_VERIFIED_G07_PRODUCTION_MOTOR_REQUIRED");
            WriteReport(report, false, false);

            // note: Keep higher-speed diagonal/300 m/s qualification separate; the focused request here is the sudden 260 m/s straight traversal.
            WriteReport(report, false, false);

            // note: Persist the live stream timings collected while the authoritative motor matrix and initial visual envelope were active.
            report.AppendLine("- terrainBuildLastSeconds: " + streamer.LastTerrainBuildSeconds.ToString("0.000"));
            report.AppendLine("- terrainBuildMaximumSeconds: " + streamer.MaximumTerrainBuildSeconds.ToString("0.000"));
            report.AppendLine("- terrainPaintLastSeconds: " + streamer.LastTerrainPaintSeconds.ToString("0.000"));
            report.AppendLine("- terrainPaintMaximumSeconds: " + streamer.MaximumTerrainPaintSeconds.ToString("0.000"));
            // note: Report both the final rolling window and run-wide production during motor-active seconds so idle cleanup cannot mask sustained output.
            report.AppendLine("- terrainReadyCellsPerSecondLastWindow: " + streamer.TerrainReadyCellsPerSecond.ToString("0.00"));
            report.AppendLine("- terrainReadyCellsPerSecondAverageWhileMoving: " + streamer.TerrainReadyAverageCellsPerSecond.ToString("0.00"));
            report.AppendLine("- terrainReadyCellsPerSecondMaximumWhileMoving: " + streamer.TerrainReadyMaximumCellsPerSecond.ToString("0.00"));
            report.AppendLine("- terrainReadyForwardCellCount: " + streamer.TerrainReadyForwardCellCount);
            report.AppendLine("- maximumTerrainQueueDepth: " + streamer.MaximumTerrainQueueDepth);
            report.AppendLine("- maximumConcurrentTerrainPreparations: " + streamer.MaximumConcurrentTerrainPreparations);
            report.AppendLine("- duplicateTerrainRequests: " + streamer.DuplicateTerrainRequestCount);
            report.AppendLine("- missedTerrainDeadlines: " + streamer.MissedTerrainDeadlineCount);
            report.AppendLine("- minimumForwardReadyLeadCells: " + streamer.MinimumForwardReadyLeadCells);
            report.AppendLine("- lastForwardReadyLeadCells: " + streamer.LastForwardReadyLeadCells);
            // note: Capture the measured synchronous streamer slice so the acceptance report exposes the worst normal update cost after warmup.
            report.AppendLine("- streamingWorkLastSeconds: " + streamer.LastStreamingWorkSeconds.ToString("0.000"));
            report.AppendLine("- streamingWorkMaximumSeconds: " + streamer.MaximumStreamingWorkSeconds.ToString("0.000"));
            // note: Coroutine slice timings include resumed terrain and content stages that do not execute inside Update itself.
            report.AppendLine("- terrainSliceMaximumSeconds: " + streamer.MaximumTerrainSliceSeconds.ToString("0.000"));
            report.AppendLine("- terrainSliceMaximumPhase: " + streamer.MaximumTerrainSlicePhase);
            report.AppendLine("- contentSliceLastSeconds: " + streamer.LastContentSliceSeconds.ToString("0.000"));
            report.AppendLine("- contentSliceMaximumSeconds: " + streamer.MaximumContentSliceSeconds.ToString("0.000"));
            report.AppendLine("- terrainPaintSliceMaximumSeconds: " + streamer.MaximumTerrainPaintSliceSeconds.ToString("0.000"));
            report.AppendLine("- streamedBiomeComputeMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedBiomeComputeSeconds.ToString("0.000"));
            report.AppendLine("- streamedBiomeUploadMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedBiomeUploadSeconds.ToString("0.000"));
            report.AppendLine("- streamedDetailNormalMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedDetailNormalSeconds.ToString("0.000"));
            report.AppendLine("- streamedDetailComputeMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedDetailComputeSeconds.ToString("0.000"));
            report.AppendLine("- streamedDetailUploadMaximumSeconds: " + YQGeneratedWorldEnvironment.MaximumStreamedDetailUploadSeconds.ToString("0.000"));
            // note: Keep aggregate and indivisible publication high-water marks in the receipt so a small inner-slice maximum cannot hide a combined stall.
            report.AppendLine("- maximumAggregateFrameWorkSeconds: " + streamer.MaximumAggregateFrameWorkSeconds.ToString("0.000"));
            report.AppendLine("- maximumAggregateFrameWorkStage: " + streamer.MaximumAggregateFrameWorkStage);
            report.AppendLine("- maximumPublicationSliceSeconds: " + streamer.MaximumPublicationSliceSeconds.ToString("0.000"));
            report.AppendLine("- maximumPublicationSliceStage: " + streamer.MaximumPublicationSliceStage);
            report.AppendLine("- maximumMaterializationSubstageSeconds: " + streamer.MaximumMaterializationSubstageSeconds.ToString("0.000"));
            report.AppendLine("- maximumMaterializationSubstage: " + streamer.MaximumMaterializationSubstage);
            // note: Keep per-frame terrain/content/frontier work separate from the deferred full-world persistence transaction.
            float maximumSynchronousFrontierWork = streamer.MaximumStreamingWorkSeconds;
            float maximumRuntimeStreamingSlice = Mathf.Max(
                streamer.MaximumTerrainSliceSeconds,
                streamer.MaximumTerrainPaintSliceSeconds,
                streamer.MaximumContentSliceSeconds);
            maximumRuntimeStreamingSlice = Mathf.Max(maximumRuntimeStreamingSlice, maximumSynchronousFrontierWork);
            report.AppendLine("- semanticSaveMaximumSeconds: " + streamer.MaximumSemanticSaveSeconds.ToString("0.000"));
            report.AppendLine("- persistenceCommitBudgetSeconds: " + PersistenceCommitBudgetSeconds.ToString("0.000"));
            report.AppendLine("- streamingMeasurementToleranceSeconds: " + StreamingMeasurementToleranceSeconds.ToString("0.000"));
            report.AppendLine("- streamingFrameBudgetSeconds: " + YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds.ToString("0.000"));
            report.AppendLine("- maximumMeasuredStreamingSliceSeconds: " + maximumRuntimeStreamingSlice.ToString("0.000"));
            report.AppendLine("- maximumSynchronousFrontierWorkSeconds: " + maximumSynchronousFrontierWork.ToString("0.000"));
            report.AppendLine("- maximumStreamingWorkStage: " + streamer.MaximumStreamingStage + " (" + streamer.MaximumStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) + ")");
            if (EnforceG07ThroughputGates && maximumRuntimeStreamingSlice > YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds + StreamingMeasurementToleranceSeconds)
            {
                // note: Keep the measured hitch visible while allowing persistence verification to prove whether the state boundary itself is sound.
                report.AppendLine("- fullAcceptanceFrameBudgetDiagnostic: DEFERRED_G07: " + maximumRuntimeStreamingSlice.ToString("0.000"));
            }
            if (streamer.MaximumSemanticSaveSeconds > PersistenceCommitBudgetSeconds)
            {
                // note: Keep the separately scoped G02 timing visible without making it fail this R1 publication and recovery matrix.
                report.AppendLine("- persistenceCommitBudgetGate: DEFERRED_G02: " + streamer.MaximumSemanticSaveSeconds.ToString("0.000"));
            }

            // note: Check detached semantic serialization without mutating accepted overlays, saving the live profile, or replacing production owners.
            GeneratedWorldPlanRecord observedPlan = manager.State.generatedWorldPlan;
            string observedSignature = BuildChunkSignature(observedPlan);
            GeneratedWorldPlanRecord roundTrippedPlan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(JsonUtility.ToJson(observedPlan));
            if (roundTrippedPlan == null || roundTrippedPlan.semanticChunks == null ||
                observedPlan.semanticChunks.Count != roundTrippedPlan.semanticChunks.Count ||
                !string.Equals(observedSignature, BuildChunkSignature(roundTrippedPlan), StringComparison.Ordinal))
                throw new InvalidOperationException("semantic chunk records changed across detached serialization");
            report.AppendLine("- semanticPlanSerializationRoundTrip: PASS serializer=UnityJson semanticCount=" + roundTrippedPlan.semanticChunks.Count);
            report.AppendLine("- liveFeatureOverlayMutation: NOT_RUN_R1_STATE_PRESERVATION");
            report.AppendLine("- liveSaveReloadManualRebind: NOT_RUN_R1_STATE_PRESERVATION; runtime rematerialization is covered by unload/revisit");
            WriteReport(report, false, false);
            if (postMotorPublicationWorkContractFailed)
                throw new InvalidOperationException("post-movement visible publication contains a stranded stage: " + postMotorPublicationWorkContractFailure);
            // note: Recheck all current demand after recovery and real traversal; an initial snapshot cannot certify the final publication owners.
            bool finalPublicationWorkPassed = streamer.TryValidateDemandedPublicationWorkContracts(
                out int finalDemandedChunks, out string finalPublicationWorkFailure);
            report.AppendLine("- finalDemandedPublicationWorkContract: " + (finalPublicationWorkPassed ? "PASS" : "FAIL") +
                " demandedChunks=" + finalDemandedChunks +
                (finalPublicationWorkPassed ? string.Empty : " failure=" + finalPublicationWorkFailure));
            WriteReport(report, false, false);
            if (!finalPublicationWorkPassed)
                throw new InvalidOperationException("final demanded publication contains a stranded stage: " + finalPublicationWorkFailure);
            if (!productionMotorMatrixPassed && !productionMotorInputInconclusive)
                throw new InvalidOperationException("production motor traversal matrix failed; inspect the per-gate movement and frame evidence above");
            if (!unloadRevisitPassed && !unloadRevisitInputInconclusive)
                throw new InvalidOperationException("R2 unload/revisit gate failed; inspect the candidate, unload, and republish rows above");
            report.AppendLine("- result: " + (verificationInconclusive ? "INCONCLUSIVE" : "PASS"));
            success = true;
        }
        finally
        {
            if (!success)
            {
                // note: A failure line turns an unattended report into a repair target while retaining the raw Unity console stack trace.
                if (!string.IsNullOrWhiteSpace(_lastNestedFailure))
                    report.AppendLine("- failure: " + _lastNestedFailure.Replace('\r', ' ').Replace('\n', ' '));
                report.AppendLine("- result: FAIL");
            }
            WriteReport(report, success, inconclusive: verificationInconclusive);
            ReleaseVerificationKeyboardHook();
            _lastNestedFailure = string.Empty;
            _started = false;
            _completed = true;
        }
    }

    private static IEnumerator VerifyProductionMotorTraversal(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor,
        Transform player,
        CharacterController characterController,
        StringBuilder report,
        Action<bool, bool> result)
    {
        // note: Drive the existing production input and motor update path through the editor's real keyboard device so normal movement is not replaced by verifier velocity.
        UnityEngine.InputSystem.Keyboard keyboard = null;
        bool captureCoastMotion = false;
        bool walkObserved = false;
        bool sprintObserved = false;
        bool dashActivationObserved = false;
        bool reversalObserved = false;
        bool walkEligibilityObserved = false;
        bool sprintEligibilityObserved = false;
        bool dashEligibilityObserved = false;
        bool reversalEligibilityObserved = false;
        bool walkGroundedObserved = false;
        bool sprintGroundedObserved = false;
        bool dashGroundedObserved = false;
        bool reversalGroundedObserved = false;
        int walkFrames = 0;
        int sprintFrames = 0;
        int dashFrames = 0;
        int reversalFrames = 0;
        float originalWalkSpeed = motor.walkSpeed;
        float originalSprintSpeed = motor.sprintSpeed;
        float originalAcceleration = motor.acceleration;
        float originalDeceleration = motor.deceleration;
        // note: Keep unattended focused-speed samples at a live player-loop cadence, restoring the session value when the bounded probe ends.
        bool originalRunInBackground = Application.runInBackground;
        bool restoreRunInBackground = _focusedSpeed260MotorWitnessOnly && !originalRunInBackground;
        // note: Bind the runtime witness to the actual player recovery owner so any rescue teleport invalidates measured movement.
        YQGeneratedWorldPlayerFallSafety fallSafety = player.GetComponentInParent<YQGeneratedWorldPlayerFallSafety>() ??
            player.GetComponentInChildren<YQGeneratedWorldPlayerFallSafety>(true);
        int fallSafetyRecoveryCountAtTraversalStart = fallSafety != null ? fallSafety.RecoveryCount : -1;
        int historicalDropCountAtTraversalStart = fallSafety != null ? fallSafety.HistoricalDropWithoutPenetrationCount : -1;
        ProfilerRecorder mainThreadTimeRecorder = default;
        string mainThreadTimingMetric = "CPU Main Thread Frame Time";
        ProfilerRecorder behaviourUpdateRecorder = default;
        ProfilerRecorder physicsSimulateRecorder = default;
        ProfilerRecorder gcCollectRecorder = default;
        List<ProfilerMarkerFrameCapture> profilerMarkerFrameCaptures = new List<ProfilerMarkerFrameCapture>();
#if UNITY_EDITOR
        R2FrameTraceCapture frameTrace = null;
#endif
        double deadline = Time.realtimeSinceStartupAsDouble + 20.0;
        try
        {
            if (restoreRunInBackground)
                Application.runInBackground = true;
            // note: The focused witness records reserve decisions in a fixed ring before movement, without binary profiling or hot-path formatting.
            if (_focusedSpeed260MotorWitnessOnly)
                streamer.BeginAppearanceBudgetObservation();
#if UNITY_EDITOR
            // note: Demand-wide history is diagnostic-only and must be explicitly armed for a bounded replay.
            const string completeCellHistoryRequest = "Temp/YQ_R2_CELL_HISTORY.request";
            if (_focusedSpeed260MotorWitnessOnly && File.Exists(completeCellHistoryRequest))
            {
                File.Delete(completeCellHistoryRequest);
                streamer.BeginCompleteCellObservation();
                report.AppendLine("- completeCellHistory: diagnosticOptIn=true cleanCertification=false capacity=65536 cells=2048");
            }
            // note: Arm only the named short-coast question; clean witnesses never allocate or capture this motion timeline.
            const string coastMotionRequest = "Temp/YQ_R2_COAST_MOTION.request";
            if (_focusedSpeed260MotorWitnessOnly && File.Exists(coastMotionRequest))
            {
                File.Delete(coastMotionRequest);
                captureCoastMotion = true;
                report.AppendLine("- coastMotionHistory: diagnosticOptIn=true cleanCertification=false capacity=512");
            }
#endif
            report.AppendLine("- verifierBackgroundCadence: focusedSpeedWitness=" + _focusedSpeed260MotorWitnessOnly +
                ", runInBackgroundBefore=" + originalRunInBackground +
                ", runInBackgroundDuring=" + Application.runInBackground);
            // note: Use one named virtual keyboard for the probe so a physical/background keyboard cannot replace the device read by the production motor.
            keyboard = AcquireVerificationKeyboard();
            if (keyboard == null)
                throw new InvalidOperationException("production motor verification could not acquire a keyboard device");
            keyboard.MakeCurrent();
            // note: Record the live production eligibility and grounding state without waiting or correcting the fixture; the matrix must expose a bad startup state.
            report.AppendLine("- productionMotorEligibility: " + motor.CanProcessMovementInput +
                ", groundedAtInputStart=" + motor.IsGrounded +
                ", moveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);
            // note: Prove that the queued event was consumed by a real Input System update before attributing a zero-distance result to the production motor.
            uint inputUpdateBeforeAcknowledgement = UnityEngine.InputSystem.LowLevel.InputState.updateCount;
            QueueKeyboardState(
                keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
            bool inputDeliveryAcknowledged = false;
            double inputAcknowledgementDeadline = Time.realtimeSinceStartupAsDouble + 0.25;
            while (Time.realtimeSinceStartupAsDouble < inputAcknowledgementDeadline)
            {
                yield return null;
                UnityEngine.InputSystem.Keyboard currentKeyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard.added && keyboard.enabled &&
                    UnityEngine.InputSystem.LowLevel.InputState.updateCount != inputUpdateBeforeAcknowledgement &&
                    currentKeyboard == keyboard && keyboard.wKey.isPressed)
                {
                    inputDeliveryAcknowledged = true;
                    break;
                }
            }
            report.AppendLine("- productionInputDelivery: " +
                (inputDeliveryAcknowledged ? "ACK" : "INPUT_DELIVERY_INCONCLUSIVE") +
                ",updateBefore=" + inputUpdateBeforeAcknowledgement +
                ",updateAfter=" + UnityEngine.InputSystem.LowLevel.InputState.updateCount +
                ",deviceAdded=" + keyboard.added +
                ",deviceEnabled=" + keyboard.enabled +
                ",deviceCurrent=" + (UnityEngine.InputSystem.Keyboard.current == keyboard) +
                ",applicationFocused=" + Application.isFocused);
            AppendInjectedKeyboardDiagnostics(report, keyboard, "acknowledgement");
            WriteReport(report, false, false);
            if (!inputDeliveryAcknowledged)
            {
                // note: Do not report a false locomotion failure when the harness cannot demonstrate delivery through Unity's normal input scheduler.
                result(false, true);
                yield break;
            }

            // note: Start with forward walk input so the real motor proves ordinary collision-supported movement before sprint and dash are requested.
            Vector3 walkStartPosition = player.position;
            double phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 1.25)
            {
                // note: Reassert the injected device before each production frame so Unity cannot restore a different current keyboard during title-to-gameplay handoff.
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.W));
                yield return null;
                if (walkFrames == 0)
                    AppendInjectedKeyboardDiagnostics(report, keyboard, "walk");
                ValidateProductionMotorFrame(streamer, player, characterController, "walk", report);
                walkFrames++;
                walkEligibilityObserved |= motor.CanProcessMovementInput;
                walkGroundedObserved |= motor.IsGrounded;
                walkObserved |= motor.MoveInput.y > 0.1f && motor.IsGrounded;
            }
            float walkPlanarDistance = PlanarDistance(walkStartPosition, player.position);
            walkObserved &= walkPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorWalkEvidence: eligible=" + walkEligibilityObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - phaseStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + walkGroundedObserved + ", planarDistance=" + walkPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorWalk: " + (walkObserved ? "PASS" : "FAIL"));
            // note: Persist each production-input phase immediately so an interrupted probe retains the last truthful observation.
            WriteReport(report, false, false);

            // note: Hold W plus the production sprint key so stamina, acceleration, CharacterController.Move, and sprint state all remain motor-owned.
            Vector3 sprintStartPosition = player.position;
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 1.75)
            {
                // note: Keep the real production keyboard owner current while sprint input crosses multiple Unity frames.
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        UnityEngine.InputSystem.Key.W,
                        UnityEngine.InputSystem.Key.LeftShift));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "run", report);
                sprintFrames++;
                sprintEligibilityObserved |= motor.CanProcessMovementInput;
                sprintGroundedObserved |= motor.IsGrounded;
                sprintObserved |= motor.IsSprinting && motor.MoveInput.y > 0.1f && motor.IsGrounded;
            }
            float sprintPlanarDistance = PlanarDistance(sprintStartPosition, player.position);
            sprintObserved &= sprintPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorRunEvidence: eligible=" + sprintEligibilityObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - phaseStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + sprintGroundedObserved + ", planarDistance=" + sprintPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorRun: " + (sprintObserved ? "PASS" : "FAIL"));
            // note: Flush the sprint result before dash setup can fail or the editor session can be stopped.
            WriteReport(report, false, false);

            // note: Establish a real key-up boundary before pulsing Q so the production motor observes the same wasPressedThisFrame transition as a player.
            // note: Do not refill stamina or otherwise repair the live actor between phases; dash must use the production resource state.
            QueueKeyboardState(
                keyboard,
                new UnityEngine.InputSystem.LowLevel.KeyboardState());
            yield return null;
            // note: Dash away from the known Witch House frontage so authored collision tests dash movement instead of a blocked test lane.
            int dashSideContactsBefore = motor.BlockingControllerContactCount;
            Vector3 dashStartPosition = player.position;
            double dashInputStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - dashInputStarted < 0.10)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        UnityEngine.InputSystem.Key.S,
                        UnityEngine.InputSystem.Key.Q));
                yield return null;
                if (dashFrames == 0)
                    AppendInjectedKeyboardDiagnostics(report, keyboard, "dash");
                ValidateProductionMotorFrame(streamer, player, characterController, "dash-start", report);
                dashFrames++;
                dashEligibilityObserved |= motor.CanProcessMovementInput;
                dashGroundedObserved |= motor.IsGrounded;
                dashActivationObserved |= motor.IsDashing || motor.DashStartedThisFrame;
            }
            // note: Release Q while retaining the same clear-lane direction so the production motor completes the dash without retriggering it.
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 0.55)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.S));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "dash-release", report);
                dashFrames++;
                dashEligibilityObserved |= motor.CanProcessMovementInput;
                dashGroundedObserved |= motor.IsGrounded;
                dashActivationObserved |= motor.IsDashing || motor.DashStartedThisFrame;
            }
            float dashPlanarDistance = PlanarDistance(dashStartPosition, player.position);
            bool dashObserved = dashActivationObserved && dashPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorDashEvidence: eligible=" + dashEligibilityObserved +
                ", activated=" + dashActivationObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - dashInputStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + dashGroundedObserved + ", planarDistance=" + dashPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            AppendProductionMovementDiagnostics(report, motor, "dash", dashSideContactsBefore);
            report.AppendLine("- productionMotorDash: " + (dashObserved ? "PASS" : "FAIL"));
            // note: Flush the dash result before reversal input begins so the matrix remains auditable after partial execution.
            WriteReport(report, false, false);

            // note: Reverse through the same motor input contract so direction changes are proven independently of the high-speed diagnostic harness.
            Vector3 reversalStartPosition = player.position;
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 1.25)
            {
                // note: Reassert the injected device for reversal so the production motor cannot silently read a different keyboard after dash cleanup.
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(UnityEngine.InputSystem.Key.S));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "reversal", report);
                reversalFrames++;
                reversalEligibilityObserved |= motor.CanProcessMovementInput;
                reversalGroundedObserved |= motor.IsGrounded;
                reversalObserved |= motor.MoveInput.y < -0.1f && motor.IsGrounded;
            }
            float reversalPlanarDistance = PlanarDistance(reversalStartPosition, player.position);
            reversalObserved &= reversalPlanarDistance > 0.02f;
            report.AppendLine("- productionMotorReversalEvidence: eligible=" + reversalEligibilityObserved +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - phaseStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                ", grounded=" + reversalGroundedObserved + ", planarDistance=" + reversalPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalMoveInput=" + motor.MoveInput.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorReversal: " + (reversalObserved ? "PASS" : "FAIL"));
            // note: Exercise diagonal camera-relative movement through the same live keyboard and motor path, validating every resulting capsule position.
            int traversalBlocksBeforeDiagonal = streamer.TraversalConstraintBlockCount;
            Vector3 diagonalStartPosition = player.position;
            bool diagonalInputObserved = false;
            bool diagonalGroundedObserved = false;
            int diagonalFrames = 0;
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 0.85)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState(
                        UnityEngine.InputSystem.Key.W,
                        UnityEngine.InputSystem.Key.D));
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "diagonal", report);
                diagonalFrames++;
                diagonalInputObserved |= motor.MoveInput.x > 0.1f && motor.MoveInput.y > 0.1f;
                diagonalGroundedObserved |= motor.IsGrounded;
            }
            float diagonalPlanarDistance = PlanarDistance(diagonalStartPosition, player.position);
            int diagonalTraversalBlocks = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeDiagonal;
            bool diagonalObserved = diagonalInputObserved && diagonalGroundedObserved &&
                diagonalPlanarDistance > 0.02f && diagonalTraversalBlocks == 0;
            report.AppendLine("- productionMotorDiagonalEvidence: inputObserved=" + diagonalInputObserved +
                ", grounded=" + diagonalGroundedObserved + ", planarDistance=" + diagonalPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", traversalBlocks=" + diagonalTraversalBlocks + ", frames=" + diagonalFrames);
            report.AppendLine("- productionMotorDiagonal: " + (diagonalObserved ? "PASS" : "FAIL"));
            WriteReport(report, false, false);

            // note: Rapidly rotate through all four diagonal quadrants on the authoritative motor to catch turn-envelope gaps and swept-capsule corner leaks.
            int traversalBlocksBeforeTurns = streamer.TraversalConstraintBlockCount;
            Vector3 rapidTurnStartPosition = player.position;
            int rapidTurnFrames = 0;
            int rapidTurnInputsObserved = 0;
            bool rapidTurnGroundedObserved = false;
            for (int turn = 0; turn < 4; turn++)
            {
                UnityEngine.InputSystem.Key forwardKey = turn < 2
                    ? UnityEngine.InputSystem.Key.W
                    : UnityEngine.InputSystem.Key.S;
                UnityEngine.InputSystem.Key strafeKey = turn == 0 || turn == 3
                    ? UnityEngine.InputSystem.Key.D
                    : UnityEngine.InputSystem.Key.A;
                float expectedXSign = strafeKey == UnityEngine.InputSystem.Key.D ? 1f : -1f;
                float expectedYSign = forwardKey == UnityEngine.InputSystem.Key.W ? 1f : -1f;
                bool turnInputObserved = false;
                phaseStarted = Time.realtimeSinceStartupAsDouble;
                while (Time.realtimeSinceStartupAsDouble < deadline && Time.realtimeSinceStartupAsDouble - phaseStarted < 0.35)
                {
                    QueueKeyboardState(
                        keyboard,
                        new UnityEngine.InputSystem.LowLevel.KeyboardState(forwardKey, strafeKey));
                    yield return null;
                    ValidateProductionMotorFrame(streamer, player, characterController, "rapid-turn-" + turn, report);
                    rapidTurnFrames++;
                    turnInputObserved |= motor.MoveInput.x * expectedXSign > 0.1f && motor.MoveInput.y * expectedYSign > 0.1f;
                    rapidTurnGroundedObserved |= motor.IsGrounded;
                }
                if (turnInputObserved)
                    rapidTurnInputsObserved++;
            }
            float rapidTurnPlanarDistance = PlanarDistance(rapidTurnStartPosition, player.position);
            int rapidTurnTraversalBlocks = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeTurns;
            bool rapidTurnsObserved = rapidTurnInputsObserved == 4 && rapidTurnGroundedObserved &&
                rapidTurnPlanarDistance > 0.02f && rapidTurnTraversalBlocks == 0;
            report.AppendLine("- productionMotorRapidTurnsEvidence: inputTransitions=" + rapidTurnInputsObserved +
                "/4, grounded=" + rapidTurnGroundedObserved + ", planarDistance=" + rapidTurnPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", traversalBlocks=" + rapidTurnTraversalBlocks + ", frames=" + rapidTurnFrames);
            report.AppendLine("- productionMotorRapidTurns: " + (rapidTurnsObserved ? "PASS" : "FAIL"));

            // note: Walk a short origin-adjacent lane with the production motor, preserving natural terrain and collider constraints.
            int boundaryApproachFallbackAttemptsBefore = streamer.SynchronousGroundFallbackAttemptCount;
            int boundaryApproachFallbacksBefore = streamer.SynchronousGroundFallbackCount;
            int boundaryApproachTraversalBlocksBefore = streamer.TraversalConstraintBlockCount;
            // note: Leave a two-meter source-cell buffer so the measured 6 m/s frame cannot cross the seam before the 260 m/s sample.
            float boundaryApproachTargetMeters = Mathf.Max(
                2f,
                Mathf.Max(1f, motor.walkSpeed) * Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime) * 2f);
            motor.walkSpeed = 6f;
            motor.sprintSpeed = 6f;
            motor.acceleration = Mathf.Max(originalAcceleration, 1000000f);
            motor.deceleration = Mathf.Max(originalDeceleration, 1000000f);
            Vector3 origin = YQGeneratedWorldTerrain.OriginWorldPosition;
            // note: Keep the selected probe inside the same grid row so the boundary witness crosses only the east seam.
            const float defaultSpeed260BoundaryLaneZOffset = 2f;
            float probeChunkSize = Mathf.Max(32f, streamer.chunkWorldSize);
            Vector2Int initialProbeCell = ChunkCoordinateAt(
                new Vector3(origin.x - 2f, player.position.y, origin.z + defaultSpeed260BoundaryLaneZOffset), probeChunkSize);
            float eastProbeBoundary = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                (initialProbeCell.x + 1) * probeChunkSize;
            float stagingInset = Mathf.Max(2f, boundaryApproachTargetMeters * 2f);
            double stagingStartedAt = Time.realtimeSinceStartupAsDouble;
            float speed260BoundaryLaneZOffset = SelectLowSlopeEastBoundaryLaneOffset(
                origin,
                player.position.y,
                eastProbeBoundary,
                probeChunkSize,
                boundaryApproachTargetMeters,
                stagingInset,
                initialProbeCell.y,
                defaultSpeed260BoundaryLaneZOffset,
                characterController,
                player.position,
                out float selectedLanePeakHeightStep,
                out int selectedLaneBlockingHitCount,
                out bool selectedLaneSampled,
                out string laneCandidateEvidence,
                out List<Vector3> stagingWaypoints);
            initialProbeCell = ChunkCoordinateAt(
                new Vector3(origin.x - 2f, player.position.y, origin.z + speed260BoundaryLaneZOffset), probeChunkSize);
            eastProbeBoundary = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                (initialProbeCell.x + 1) * probeChunkSize;
            Vector3 highSpeedStagingPoint = new Vector3(
                eastProbeBoundary - stagingInset,
                player.position.y,
                origin.z + speed260BoundaryLaneZOffset);
            report.AppendLine("- productionMotorSpeed260TerrainLane: zOffset=" +
                speed260BoundaryLaneZOffset.ToString("0.0", CultureInfo.InvariantCulture) +
                ", sampled=" + selectedLaneSampled +
                ", peakTwoMeterHeightStep=" + (selectedLaneSampled
                    ? selectedLanePeakHeightStep.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                ", physicalRouteBlockers=" + selectedLaneBlockingHitCount + ", turnCorridorsChecked=6x9");
            report.AppendLine("- speed260RouteCandidates: " + laneCandidateEvidence);
            // note: The existing 25-second staging budget is independent of the ordinary motor matrix's 20-second deadline.
            double stagingDeadline = stagingStartedAt + 25.0d;
            int stagingSideContactsBefore = motor.BlockingControllerContactCount;
            Vector3 stagingStartPosition = player.position;
            float stagingDistance = PlanarDistance(player.position, highSpeedStagingPoint);
            bool stagingInputObserved = stagingDistance <= 0.8f;
            int stagingFrames = 0;
            int stagingNoProgressFrames = 0;
            int stagingWaypointIndex = 0;
            report.AppendLine("- speed260StagingWaypoints: " + (stagingWaypoints == null ? "unavailable" :
                string.Join(";", stagingWaypoints.ConvertAll(point => point.ToString("F2", CultureInfo.InvariantCulture)))));
            while (selectedLaneSampled && selectedLaneBlockingHitCount == 0 &&
                stagingWaypoints != null && stagingWaypointIndex < stagingWaypoints.Count &&
                Time.realtimeSinceStartupAsDouble < stagingDeadline &&
                stagingNoProgressFrames < 8)
            {
                // note: Follow the checked route through ordinary keyboard input; no position assignment or extra loading wait is permitted.
                Vector3 waypoint = stagingWaypoints[stagingWaypointIndex];
                if (PlanarDistance(player.position, waypoint) <= 0.4f)
                {
                    stagingWaypointIndex++;
                    continue;
                }
                Vector3 stagingDirection = waypoint - player.position;
                stagingDirection.y = 0f;
                UnityEngine.InputSystem.LowLevel.KeyboardState stagingInput =
                    KeyboardStateForMotorDirection(motor, stagingDirection, out Vector2 expectedStagingInput);
                Vector3 stagingFrameStart = player.position;
                QueueKeyboardState(keyboard, stagingInput);
                yield return null;
                ValidateProductionMotorFrame(streamer, player, characterController, "speed-260-safe-staging", report);
                stagingFrames++;
                stagingInputObserved |= IsExpectedMotorInput(motor.MoveInput, expectedStagingInput);
                float nextStagingDistance = PlanarDistance(player.position, highSpeedStagingPoint);
                stagingNoProgressFrames = PlanarDistance(stagingFrameStart, player.position) <= 0.01f
                    ? stagingNoProgressFrames + 1
                    : 0;
                stagingDistance = nextStagingDistance;
            }
            bool stagingReady = selectedLaneSampled && selectedLaneBlockingHitCount == 0 &&
                stagingWaypoints != null && stagingWaypointIndex == stagingWaypoints.Count && stagingDistance <= 0.8f;
            report.AppendLine("- productionMotorSpeed260Staging: ready=" + stagingReady +
                ", inputObserved=" + stagingInputObserved +
                ", start=" + stagingStartPosition.ToString("F2", CultureInfo.InvariantCulture) +
                ", target=" + highSpeedStagingPoint.ToString("F2", CultureInfo.InvariantCulture) +
                ", final=" + player.position.ToString("F2", CultureInfo.InvariantCulture) +
                ", distanceMeters=" + stagingDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - stagingStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                ", frames=" + stagingFrames +
                ", waypointsReached=" + stagingWaypointIndex + "/" + (stagingWaypoints?.Count ?? 0) +
                ", noProgressFrames=" + stagingNoProgressFrames +
                ", termination=" + (stagingReady ? "ready" : !selectedLaneSampled || selectedLaneBlockingHitCount != 0
                    ? "invalid-route-preflight" : stagingNoProgressFrames >= 8 ? "no-progress" : "deadline"));
            // note: Persist the actual collision owner and reason before later input phases replace the motor's last-contact evidence.
            AppendProductionMovementDiagnostics(report, motor, "speed260-staging", stagingSideContactsBefore);
            WriteReport(report, false, false);
            if (!stagingReady)
            {
                // note: Invalid setup is not a speed test. Return through the existing finally restoration without sending another warmup or coast input.
                report.AppendLine("- speed260CertificationValidity: INVALID_ROUTE routeVersion=5 reason=staging-not-ready");
                report.AppendLine("- speed260MeasuredPhases: NOT_RUN input=False coast=False");
                result(false, false);
                yield break;
            }

#if UNITY_EDITOR
            // note: Opt-in profiler initialization precedes the existing walking approach, so its startup cost cannot inflate the first 260 m/s integration step. No setup frame is added.
            frameTrace = R2FrameTraceCapture.TryBegin(report);
#endif
            // note: Approach the east seam through normal collision-supported walking before applying the speed stress input.
            Vector3 desiredBoundaryDirection = Vector3.right;
            UnityEngine.InputSystem.LowLevel.KeyboardState speedProbeInputState =
                KeyboardStateForMotorDirection(motor, desiredBoundaryDirection, out Vector2 speedProbeInputAxes);
            Vector3 boostTravelDirection = MotorWorldPlanarDirection(motor, speedProbeInputAxes);
            bool boundaryApproachInputObserved = stagingReady && stagingInputObserved;
            int boundaryApproachFrames = 0;
            float initialBoundaryGapMeters = float.PositiveInfinity;
            float finalBoundaryGapMeters = float.PositiveInfinity;
            Vector2Int boundaryApproachSourceCell = default;
            Vector2Int boundaryApproachTargetCell = default;
            Vector3 boundaryPoint = player.position;
            if (stagingReady && TryGetNextChunkBoundary(
                    player.position,
                    boostTravelDirection,
                    streamer.chunkWorldSize,
                    out initialBoundaryGapMeters,
                    out boundaryApproachSourceCell,
                    out boundaryApproachTargetCell))
            {
                boundaryPoint = player.position + boostTravelDirection * initialBoundaryGapMeters;
                double boundaryApproachBudgetSeconds = Math.Min(
                    8.0d,
                    Math.Max(2.0d, (initialBoundaryGapMeters - boundaryApproachTargetMeters) / Mathf.Max(1f, motor.walkSpeed) + 1.0d));
                // note: Boundary setup has its own bounded real-time budget; preceding walking must not erase the speed witness.
                double boundaryApproachDeadline = Time.realtimeSinceStartupAsDouble + boundaryApproachBudgetSeconds;
                while (Time.realtimeSinceStartupAsDouble < boundaryApproachDeadline &&
                    finalBoundaryGapMeters > boundaryApproachTargetMeters &&
                    ChunkCoordinateAt(player.position, streamer.chunkWorldSize) == boundaryApproachSourceCell)
                {
                    finalBoundaryGapMeters = Mathf.Abs(Vector3.Dot(boundaryPoint - player.position, boostTravelDirection));
                    if (finalBoundaryGapMeters <= boundaryApproachTargetMeters)
                        break;

                    // note: Let the production motor advance at the editor's real frame pace; boundary setup does not alter simulation timing or ground the player artificially.
                    QueueKeyboardState(keyboard, speedProbeInputState);
                    yield return null;
                    ValidateProductionMotorFrame(streamer, player, characterController, "speed-260-boundary-approach", report);
                    boundaryApproachFrames++;
                    boundaryApproachInputObserved |= IsExpectedMotorInput(motor.MoveInput, speedProbeInputAxes);
                }
                finalBoundaryGapMeters = Mathf.Abs(Vector3.Dot(boundaryPoint - player.position, boostTravelDirection));
            }
            Vector2Int boundaryApproachFinalCell = ChunkCoordinateAt(player.position, streamer.chunkWorldSize);
            bool boundaryApproachReady = stagingReady &&
                boundaryApproachFinalCell == boundaryApproachSourceCell &&
                finalBoundaryGapMeters <= boundaryApproachTargetMeters;
            int boundaryApproachFallbackAttempts =
                streamer.SynchronousGroundFallbackAttemptCount - boundaryApproachFallbackAttemptsBefore;
            int boundaryApproachFallbacks = streamer.SynchronousGroundFallbackCount - boundaryApproachFallbacksBefore;
            int boundaryApproachTraversalBlocks =
                streamer.TraversalConstraintBlockCount - boundaryApproachTraversalBlocksBefore;
            bool boundaryApproachClean = boundaryApproachFallbackAttempts == 0 && boundaryApproachTraversalBlocks == 0;
            report.AppendLine("- productionMotorSpeed260BoundaryApproach: ready=" + boundaryApproachReady +
                ", clean=" + boundaryApproachClean +
                ", inputObserved=" + boundaryApproachInputObserved +
                ", sourceCell=" + boundaryApproachSourceCell +
                ", targetCell=" + boundaryApproachTargetCell +
                ", finalCell=" + boundaryApproachFinalCell +
                ", inputAxes=" + speedProbeInputAxes.ToString("0.000", CultureInfo.InvariantCulture) +
                ", initialGapMeters=" + initialBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                ", finalGapMeters=" + finalBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                ", targetGapMeters=" + boundaryApproachTargetMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                ", fallbackAttempts=" + boundaryApproachFallbackAttempts +
                ", successfulFallbacks=" + boundaryApproachFallbacks +
                ", traversalBlocks=" + boundaryApproachTraversalBlocks +
                ", frames=" + boundaryApproachFrames);

            // note: Measure an ordinary 6 m/s frame followed by the 260 m/s boost without changing Unity's frame timing.
            UnityEngine.InputSystem.LowLevel.KeyboardState lowSpeedDirection = speedProbeInputState;
            int synchronousGroundFallbackAttemptsBeforeSpeedProbe = streamer.SynchronousGroundFallbackAttemptCount;
            int synchronousGroundFallbacksBeforeSpeedProbe = streamer.SynchronousGroundFallbackCount;
            int traversalBlocksBeforeHighSpeed = streamer.TraversalConstraintBlockCount;
            Vector3 preBoostStartPosition = player.position;
            double preBoostStartedAt = Time.realtimeSinceStartupAsDouble;
            int preBoostWarmupFrames = 0;
            bool preBoostLowSpeedInputObserved = false;
            QueueKeyboardState(keyboard, lowSpeedDirection);
            yield return null;
            preBoostWarmupFrames = 1;
            float preBoostPlanarDistance = PlanarDistance(preBoostStartPosition, player.position);
            double preBoostWarmupSeconds = Time.realtimeSinceStartupAsDouble - preBoostStartedAt;
            float preBoostWallPlanarSpeed = preBoostPlanarDistance / (float)Math.Max(0.000001d, preBoostWarmupSeconds);
            float preBoostUnityDeltaTime = Time.deltaTime;
            float preBoostUnityPlanarSpeed = preBoostPlanarDistance / Mathf.Max(0.000001f, preBoostUnityDeltaTime);
            float preBoostRequestedPlanarSpeed = motor.PlanarVelocity.magnitude;
            preBoostLowSpeedInputObserved = IsExpectedMotorInput(motor.MoveInput, speedProbeInputAxes);
            // note: Resolve the same world-space seam heading against the post-warmup motor basis, since HandleLook may rotate the motor after consuming that frame's input.
            speedProbeInputState = KeyboardStateForMotorDirection(
                motor,
                desiredBoundaryDirection,
                out speedProbeInputAxes);
            boostTravelDirection = MotorWorldPlanarDirection(motor, speedProbeInputAxes);
            Vector2Int firstBoostStartCell = ChunkCoordinateAt(player.position, streamer.chunkWorldSize);
            // note: Project the adjacent-cell witness from the latest real frame duration; the following movement remains unmodified by the verifier.
            float projectedFirstBoostFrameSeconds = Mathf.Max(Time.deltaTime, Time.unscaledDeltaTime);
            Vector2Int firstBoostMinimumTargetCell = ChunkCoordinateAt(
                player.position + boostTravelDirection * (240f * projectedFirstBoostFrameSeconds),
                streamer.chunkWorldSize);
            Vector2Int firstBoostMaximumTargetCell = ChunkCoordinateAt(
                player.position + boostTravelDirection * (260f * projectedFirstBoostFrameSeconds),
                streamer.chunkWorldSize);
            Vector2Int firstBoostEndCell = firstBoostStartCell;
            bool firstBoostMinimumTargetCellReady = streamer.TryGetPublicationSnapshot(
                    firstBoostMinimumTargetCell,
                    out YQSemanticChunkPublicationSnapshot firstBoostMinimumTargetSnapshot) &&
                firstBoostMinimumTargetSnapshot.physicalRepresentation && firstBoostMinimumTargetSnapshot.traversable &&
                firstBoostMinimumTargetSnapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady;
            bool firstBoostMaximumTargetCellReady = streamer.TryGetPublicationSnapshot(
                    firstBoostMaximumTargetCell,
                    out YQSemanticChunkPublicationSnapshot firstBoostMaximumTargetSnapshot) &&
                firstBoostMaximumTargetSnapshot.physicalRepresentation && firstBoostMaximumTargetSnapshot.traversable &&
                firstBoostMaximumTargetSnapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady;
            bool firstBoostTargetCellReady = firstBoostMaximumTargetCell != firstBoostStartCell &&
                firstBoostMinimumTargetCellReady && firstBoostMaximumTargetCellReady;
            motor.walkSpeed = 260f;
            motor.sprintSpeed = 260f;
            motor.acceleration = Mathf.Max(originalAcceleration, 1000000f);
            motor.deceleration = Mathf.Max(originalDeceleration, 1000000f);
            // note: Unity 6's frame timing counter reports CPU main-thread time for the completed frame, matching the verifier's wall-frame sample.
            try
            {
                mainThreadTimeRecorder = ProfilerRecorder.StartNew(
                    ProfilerCategory.Internal,
                    mainThreadTimingMetric,
                    128);
                // note: Capture a few parent-system markers so the frame cost outside the streamer can be attributed without a live Profiler window.
                behaviourUpdateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "BehaviourUpdate", 128);
                physicsSimulateRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Physics, "Physics.Simulate", 128);
                gcCollectRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Collect", 128);
            }
            catch (Exception)
            {
                // note: Keep the movement acceptance probe usable when a player/editor build does not expose an optional profiler counter.
                mainThreadTimeRecorder = default;
                mainThreadTimingMetric = "unavailable";
            }
            // note: Capture individual frame callbacks and meaningful waits so aggregate script time can be traced to its owner.
            try
            {
                List<ProfilerRecorderHandle> availableProfilerHandles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(availableProfilerHandles);
                foreach (ProfilerRecorderHandle handle in availableProfilerHandles)
                {
                    ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                    string categoryName = description.Category.Name;
                    string categoryLower = categoryName.ToLowerInvariant();
                    if (!(categoryLower.Contains("internal") || categoryLower.Contains("scripts") ||
                        categoryLower.Contains("render") || categoryLower.Contains("physics") ||
                        categoryLower.Contains("memory")) ||
                        description.UnitType.ToString().IndexOf("Time", StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    string markerName = description.Name;
                    string markerLower = markerName.ToLowerInvariant();
                    // note: The project motor is 3D; Unity's inactive Physics2D API timing handles otherwise crowd out the matching PlayerLoop scopes.
                    if (categoryLower.StartsWith("physics2d", StringComparison.Ordinal) ||
                        markerLower.StartsWith("physics2d/", StringComparison.Ordinal) ||
                        markerLower.StartsWith("physics2d.", StringComparison.Ordinal))
                        continue;
                    bool waitMarker = markerLower.Contains("waitfor") || markerLower.Contains("semaphore");
                    bool usefulWaitMarker = markerLower.Contains("waitfortargetfps") ||
                        markerLower.Contains("waitforvblank") ||
                        markerLower.Contains("waitforframeend") ||
                        markerLower.Contains("waitforjob") ||
                        markerLower.Contains("waitforinput") ||
                        markerLower.Contains("waitforrenderjobs") ||
                        markerLower.Contains("semaphore.waitforsignal") ||
                        markerLower.Contains("gfx.waitforpresentongfxthread") ||
                        markerLower.Contains("gfx.waitforrenderthread");
                    bool frameCallbackMarker = markerLower.Contains(".update()") ||
                        markerLower.Contains(".lateupdate()") || markerLower.Contains(".fixedupdate()") ||
                        markerLower.Contains("scriptrunbehaviourupdate");
                    bool streamerWorkMarker = markerLower.StartsWith(
                        "yqplayerfollowingsemanticchunkstreamer.", StringComparison.Ordinal);
                    bool materializerWorkMarker = markerLower.StartsWith(
                        "yqcontinuousworldfeaturematerializer.", StringComparison.Ordinal);
                    bool g08FrameCostMarker = markerLower.StartsWith(
                        "g08framecost.", StringComparison.Ordinal);
                    bool loopFrameMarker = markerLower.Contains("playerloop") ||
                        markerLower.Contains("delayedcallmanager") ||
                        markerLower.Contains("editorloop") ||
                        markerLower.Contains("coroutine") ||
                        markerLower.Contains("asyncinstantiate");
                    // note: Keep Unity loop and coroutine parents beside the complete callback and streamer sample for main-thread attribution.
                    if ((waitMarker && !usefulWaitMarker) ||
                        !(usefulWaitMarker || frameCallbackMarker || streamerWorkMarker || materializerWorkMarker || g08FrameCostMarker || loopFrameMarker))
                        continue;

                    ProfilerRecorder currentThreadRecorder = default;
                    if (markerLower.Contains("semaphore.waitforsignal"))
                    {
                        // note: Compare aggregate semaphore waits with only the verifier's calling Unity thread to identify whether the frame stall blocks gameplay.
                        currentThreadRecorder = new ProfilerRecorder(
                            handle,
                            128,
                            ProfilerRecorderOptions.Default |
                            ProfilerRecorderOptions.StartImmediately |
                            ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
                    }
                    ProfilerMarkerFrameCapture capture = new ProfilerMarkerFrameCapture
                    {
                        Category = categoryName,
                        Name = markerName,
                        UnitType = description.UnitType.ToString(),
                        Priority = streamerWorkMarker || materializerWorkMarker || g08FrameCostMarker ? 0 :
                            usefulWaitMarker || loopFrameMarker ? 1 : 2,
                        CurrentThreadRecorder = currentThreadRecorder,
                        Recorder = new ProfilerRecorder(
                            handle,
                            128,
                            ProfilerRecorderOptions.Default |
                            ProfilerRecorderOptions.StartImmediately)
                    };
                    if (capture.Recorder.Valid)
                        profilerMarkerFrameCaptures.Add(capture);
                    else
                    {
                        capture.Recorder.Dispose();
                        if (capture.CurrentThreadRecorder.Valid)
                            capture.CurrentThreadRecorder.Dispose();
                    }
                }
                profilerMarkerFrameCaptures.Sort((left, right) => left.Priority.CompareTo(right.Priority));
                for (int i = 128; i < profilerMarkerFrameCaptures.Count; i++)
                {
                    if (profilerMarkerFrameCaptures[i].Recorder.Valid)
                        profilerMarkerFrameCaptures[i].Recorder.Dispose();
                    if (profilerMarkerFrameCaptures[i].CurrentThreadRecorder.Valid)
                        profilerMarkerFrameCaptures[i].CurrentThreadRecorder.Dispose();
                }
                if (profilerMarkerFrameCaptures.Count > 128)
                    profilerMarkerFrameCaptures.RemoveRange(128, profilerMarkerFrameCaptures.Count - 128);
            }
            catch (Exception)
            {
                // note: Keep the core acceptance witness active if a Unity version does not expose recorder-handle enumeration.
            }
            int highSpeedSideContactsBefore = motor.BlockingControllerContactCount;
            Vector3 highSpeedStartPosition = player.position;
            bool highSpeedInputObserved = false;
            bool highSpeedGroundedObserved = false;
            bool groundViewCoverageMaintained = true;
            string firstGroundViewFailure = string.Empty;
            bool visibleViewCoverageMaintained = true;
            string firstVisibleViewFailure = string.Empty;
            bool highSpeedTurnBufferMaintained = true;
            string firstTurnBufferFailure = string.Empty;
            // note: Keep the transition frame explicit so a short speed-up hiccup cannot disappear inside the later distance total.
            float firstBoostFrameSeconds = 0f;
            float firstBoostFramePlanarDistance = 0f;
            float firstBoostFrameWallPlanarSpeed = 0f;
            float firstBoostFrameUnityPlanarSpeed = 0f;
            float firstBoostFrameRequestedPlanarSpeed = 0f;
            float firstBoostFrameUnityDeltaTime = 0f;
            float firstBoostFrameMotorDeltaTime = 0f;
            float firstBoostFrameMotorPlanarSpeed = 0f;
            Vector3 firstBoostFrameRequestedMove = Vector3.zero;
            Vector3 firstBoostFrameActualMove = Vector3.zero;
            Vector3 firstBoostFrameStartPosition = Vector3.zero;
            Vector3 firstBoostFrameEndPosition = Vector3.zero;
            Vector2 firstBoostFrameExpectedInput = Vector2.zero;
            Vector2 firstBoostFrameConsumedInput = Vector2.zero;
            Vector3 firstBoostFrameMotorForward = Vector3.forward;
            Vector3 firstBoostFrameMotorRight = Vector3.right;
            Vector3 firstBoostFrameContactPoint = Vector3.zero;
            Vector3 firstBoostFrameContactNormal = Vector3.zero;
            CollisionFlags firstBoostFrameCollisionFlags = CollisionFlags.None;
            string firstBoostFrameBlockingContact = "<none>";
            bool firstBoostFrameInputObserved = false;
            bool firstBoostFrameDirectionObserved = false;
            bool firstBoostFrameReadinessGateRejected = false;
            bool firstBoostFrameControllerGrounded = false;
            float firstBoostFrameDirectionDot = 0f;
            int firstBoostFrameSideContacts = 0;
            bool firstBoostFrameCrossedChunkBoundary = false;
            bool firstBoostFrameGroundCoverage = false;
            bool firstBoostFrameVisibleCoverage = false;
            bool firstBoostFrameTurnBufferCoverage = false;
            float maximumHighSpeedObserved = 0f;
            float maximumHighSpeedWallObserved = 0f;
            float maximumRequestedHighSpeed = 0f;
            float maximumHighSpeedFrameSeconds = 0f;
            bool maximumHighSpeedFrameFocused = true;
            int highSpeedUnfocusedFrames = 0;
            float[] highSpeedWallFrameSamples = new float[2048];
            int highSpeedWallFrameSampleCount = 0;
            float maximumHighSpeedSaveFrameSeconds = 0f;
            float maximumHighSpeedNoSaveFrameSeconds = 0f;
            int highSpeedSaveFrameCount = 0;
            float minimumHighSpeedUnityDeltaTime = float.MaxValue;
            float maximumHighSpeedUnityDeltaTime = 0f;
            int maximumHighSpeedUnityDeltaTimeFrame = -1;
            float maximumHighSpeedMainThreadSeconds = 0f;
            float worstWallFrameMainThreadSeconds = 0f;
            float worstWallFrameUnityDeltaTime = 0f;
            float worstWallFrameStreamingWorkSeconds = 0f;
            float worstWallFrameStreamingStageSeconds = 0f;
            string worstWallFrameStreamingStage = string.Empty;
            int worstWallFrameUnityFrame = -1;
            int worstWallFrameStreamerFrame = -1;
            float profilerPeakWallFrameSeconds = 0f;
            float profilerPeakUnityDeltaTime = 0f;
            float profilerPeakStreamingWorkSeconds = 0f;
            float profilerPeakStreamingStageSeconds = 0f;
            string profilerPeakStreamingStage = string.Empty;
            int profilerPeakUnityFrame = -1;
            int profilerPeakStreamerFrame = -1;
            int profilerPeakGen0Collections = 0;
            int profilerPeakGen1Collections = 0;
            int profilerPeakGen2Collections = 0;
            int worstFrameGen0Collections = 0;
            int worstFrameGen1Collections = 0;
            int worstFrameGen2Collections = 0;
            long maximumHighSpeedAllocatedBytesPerFrame = 0;
            long worstWallFrameAllocatedBytes = 0;
            long profilerPeakAllocatedBytes = 0;
            float maximumHighSpeedCoastMainThreadSeconds = 0f;
            float worstHighSpeedCoastMainThreadSeconds = 0f;
            int maximumHighSpeedCoastMainThreadUnityFrame = -1;
            float maximumHighSpeedCoastMainThreadWallFrameSeconds = 0f;
            int worstHighSpeedCoastUnityFrame = -1;
            float profilerPeakBehaviourUpdateSeconds = 0f;
            float profilerPeakPhysicsSimulateSeconds = 0f;
            float profilerPeakGcCollectSeconds = 0f;
            float worstWallFrameBehaviourUpdateSeconds = 0f;
            float worstWallFramePhysicsSimulateSeconds = 0f;
            float worstWallFrameGcCollectSeconds = 0f;
            float worstCoastFrameBehaviourUpdateSeconds = 0f;
            float worstCoastFramePhysicsSimulateSeconds = 0f;
            float worstCoastFrameGcCollectSeconds = 0f;
            // note: ProfilerRecorder.LastValue is the last completed frame; retain each completed gameplay sample until its CPU timing arrives.
            bool pendingProfilerFrameValid = false;
            bool pendingProfilerFrameWasHighSpeed = false;
            int pendingProfilerFrameUnityFrame = -1;
            float pendingProfilerFrameWallSeconds = 0f;
            float pendingProfilerFrameUnityDeltaTime = 0f;
            float pendingProfilerFrameStreamingWorkSeconds = 0f;
            float pendingProfilerFrameStreamingStageSeconds = 0f;
            string pendingProfilerFrameStreamingStage = string.Empty;
            int pendingProfilerFrameStreamerFrame = -1;
            int pendingProfilerFrameGen0Collections = 0;
            int pendingProfilerFrameGen1Collections = 0;
            int pendingProfilerFrameGen2Collections = 0;
            long pendingProfilerFrameAllocatedBytes = 0L;
            // note: Keep a fixed-capacity timeline for the stress window; profiler attribution is attached on the next frame when Unity publishes its completed counters.
            const int r2FrameTimingCapacity = 8192;
            R2FrameTimingDiagnostic[] r2FrameTimings = new R2FrameTimingDiagnostic[r2FrameTimingCapacity];
            int r2FrameTimingCount = 0;
            int pendingR2FrameTimingIndex = -1;
            int highSpeedFrames = 0;
            // note: Scope throughput counters to the motor witness so startup work cannot masquerade as high-speed service capacity.
            double highSpeedWitnessStartedAt = Time.realtimeSinceStartupAsDouble;
            long backgroundHeightTasksBeforeSpeed = streamer.BackgroundHeightTaskCount;
            long successfulHeightTasksBeforeSpeed = streamer.BackgroundHeightTaskSucceededCount;
            long canceledHeightTasksBeforeSpeed = streamer.BackgroundHeightTaskCanceledCount;
            long faultedHeightTasksBeforeSpeed = streamer.BackgroundHeightTaskFaultedCount;
            double backgroundHeightTaskSecondsBeforeSpeed = streamer.BackgroundHeightTaskTotalSeconds;
            int terrainHandoffsBeforeSpeed = streamer.TerrainPreparationHandoffCount;
            float terrainHandoffSecondsBeforeSpeed = streamer.TerrainPreparationHandoffTotalSeconds;
            int contentPreemptionsBeforeSpeed = streamer.ContentPreemptionCount;
            int predictedContentPreemptionsBeforeSpeed = streamer.PredictedContentPreemptionCount;
            float highSpeedTravelDistance = 0f;
            float highSpeedConstantInputSegmentDistance = 0f;
            int highSpeedTransitionInputMask = 0;
            int highSpeedTransitionDisplacementMask = 0;
            int inputObstructedFrames = 0;
            int firstInputObstructionFrame = -1;
            int firstInputObstructionContactId = 0;
            Vector3 firstInputObstructionPosition = Vector3.zero;
            Vector3 firstInputObstructionNormal = Vector3.zero;
            float firstInputObstructionExpected = 0f, firstInputObstructionCompleted = 0f;
            // note: Retain each requested turn's real motion and collision evidence; a failed mask must identify the failing direction, not only its final bitset.
            var highSpeedDirectionSamples = new Speed260DirectionSample[8];
            const double highSpeedProbeDurationSeconds = 2.45d;
            // note: Exercise every motor-local direction class so player yaw cannot collapse forward/back checks into repeated diagonals.
            Vector3 highSpeedOrbitForward = motor.transform.forward;
            highSpeedOrbitForward.y = 0f;
            highSpeedOrbitForward.Normalize();
            Vector3 highSpeedOrbitRight = motor.transform.right;
            highSpeedOrbitRight.y = 0f;
            highSpeedOrbitRight.Normalize();
            Vector3[] highSpeedOrbitDirections =
            {
                -highSpeedOrbitRight - highSpeedOrbitForward, -highSpeedOrbitForward,
                highSpeedOrbitRight - highSpeedOrbitForward, highSpeedOrbitRight,
                highSpeedOrbitRight + highSpeedOrbitForward, highSpeedOrbitForward,
                -highSpeedOrbitRight + highSpeedOrbitForward, -highSpeedOrbitRight
            };
            bool highSpeedLoopCaptureWritten = false;
            // note: LastValue counter publication latency is not guaranteed; these legacy snapshots cannot certify same-frame CPU causality.
            report.AppendLine("- profilerRecorderFrameAttribution: UNVERIFIED counterArrivalOrderIsNotFrameIdentity=true useExplicitMetadataTraceForCausality=true");
            void CaptureR2FrameTiming(
                bool coast,
                int unityFrame,
                float wallSeconds,
                float unityDeltaSeconds,
                float streamingWorkSeconds,
                float streamingStageSeconds,
                string streamingStage,
                long allocatedBytes,
                int gen0Collections,
                int gen1Collections,
                int gen2Collections)
            {
#if UNITY_EDITOR
                frameTrace?.RecordBoundary(unityFrame, coast, wallSeconds);
#endif
                if (r2FrameTimingCount >= r2FrameTimings.Length)
                    return;
                pendingR2FrameTimingIndex = r2FrameTimingCount++;
                r2FrameTimings[pendingR2FrameTimingIndex] = new R2FrameTimingDiagnostic
                {
                    UnityFrame = unityFrame,
                    Coast = coast,
                    WallSeconds = wallSeconds,
                    UnityDeltaSeconds = unityDeltaSeconds,
                    StreamingWorkSeconds = streamingWorkSeconds,
                    StreamingStageSeconds = streamingStageSeconds,
                    StreamingStage = streamingStage ?? string.Empty,
                    AllocatedBytes = allocatedBytes,
                    Gen0Collections = gen0Collections,
                    Gen1Collections = gen1Collections,
                    Gen2Collections = gen2Collections
                };
            }
            // note: Retain legacy counter observations for comparison; their assumed frame association is explicitly unverified above.
            void AttributeLastCompletedProfilerSample()
            {
                if (!mainThreadTimeRecorder.Valid || mainThreadTimeRecorder.Count <= 0 ||
                    !pendingProfilerFrameValid || pendingProfilerFrameUnityFrame != Time.frameCount - 1)
                    return;

                float completedFrameMainThreadSeconds = (float)Math.Max(0d, mainThreadTimeRecorder.LastValue * 1e-9d);
                float completedBehaviourUpdateSeconds = behaviourUpdateRecorder.Valid && behaviourUpdateRecorder.Count > 0
                    ? (float)Math.Max(0d, behaviourUpdateRecorder.LastValue * 1e-9d)
                    : 0f;
                float completedPhysicsSimulateSeconds = physicsSimulateRecorder.Valid && physicsSimulateRecorder.Count > 0
                    ? (float)Math.Max(0d, physicsSimulateRecorder.LastValue * 1e-9d)
                    : 0f;
                float completedGcCollectSeconds = gcCollectRecorder.Valid && gcCollectRecorder.Count > 0
                    ? (float)Math.Max(0d, gcCollectRecorder.LastValue * 1e-9d)
                    : 0f;
                float completedStreamerUpdateSeconds = 0f;
                float completedStreamerLateUpdateSeconds = 0f;
                float completedCurrentThreadSemaphoreWaitSeconds = 0f;
                for (int i = 0; i < profilerMarkerFrameCaptures.Count; i++)
                {
                    ProfilerMarkerFrameCapture capture = profilerMarkerFrameCaptures[i];
                    float markerSeconds = capture.ReadSeconds();
                    capture.RecordFrame(pendingProfilerFrameWasHighSpeed, markerSeconds);
                    // note: ProfilerRecorder descriptions split marker category and name; match both fields so per-frame streamer timing is not silently left at zero.
                    if (string.Equals(capture.Category, "Scripts", StringComparison.Ordinal) &&
                        string.Equals(capture.Name, "YQPlayerFollowingSemanticChunkStreamer.Update", StringComparison.Ordinal))
                        completedStreamerUpdateSeconds = markerSeconds;
                    else if (string.Equals(capture.Category, "Scripts", StringComparison.Ordinal) &&
                        string.Equals(capture.Name, "YQPlayerFollowingSemanticChunkStreamer.LateUpdate", StringComparison.Ordinal))
                        completedStreamerLateUpdateSeconds = markerSeconds;
                    if (profilerMarkerFrameCaptures[i].CurrentThreadRecorder.Valid)
                    {
                        float currentThreadSeconds = capture.ReadCurrentThreadSeconds();
                        if (capture.Name.IndexOf("Semaphore.WaitForSignal", StringComparison.OrdinalIgnoreCase) >= 0)
                            completedCurrentThreadSemaphoreWaitSeconds = currentThreadSeconds;
                        capture.CurrentThreadPeakSeconds = Mathf.Max(
                            capture.CurrentThreadPeakSeconds,
                            currentThreadSeconds);
                        if (pendingProfilerFrameWasHighSpeed &&
                            pendingProfilerFrameUnityFrame == worstWallFrameUnityFrame)
                            capture.CurrentThreadWorstWallFrameSeconds = currentThreadSeconds;
                        if (!pendingProfilerFrameWasHighSpeed &&
                            pendingProfilerFrameUnityFrame == worstHighSpeedCoastUnityFrame)
                            capture.CurrentThreadWorstCoastFrameSeconds = currentThreadSeconds;
                    }
                }
                if (pendingR2FrameTimingIndex >= 0 &&
                    pendingR2FrameTimingIndex < r2FrameTimingCount &&
                    r2FrameTimings[pendingR2FrameTimingIndex].UnityFrame == pendingProfilerFrameUnityFrame)
                {
                    R2FrameTimingDiagnostic timing = r2FrameTimings[pendingR2FrameTimingIndex];
                    timing.MainThreadSeconds = completedFrameMainThreadSeconds;
                    timing.BehaviourUpdateSeconds = completedBehaviourUpdateSeconds;
                    timing.PhysicsSeconds = completedPhysicsSimulateSeconds;
                    timing.GcSeconds = completedGcCollectSeconds;
                    timing.StreamerUpdateSeconds = completedStreamerUpdateSeconds;
                    timing.StreamerLateUpdateSeconds = completedStreamerLateUpdateSeconds;
                    timing.CurrentThreadSemaphoreWaitSeconds = completedCurrentThreadSemaphoreWaitSeconds;
                    r2FrameTimings[pendingR2FrameTimingIndex] = timing;
                    pendingR2FrameTimingIndex = -1;
                }
                if (pendingProfilerFrameWasHighSpeed)
                {
                    if (completedFrameMainThreadSeconds > maximumHighSpeedMainThreadSeconds)
                    {
                        maximumHighSpeedMainThreadSeconds = completedFrameMainThreadSeconds;
                        for (int i = 0; i < profilerMarkerFrameCaptures.Count; i++)
                            profilerMarkerFrameCaptures[i].MainThreadPeakSeconds = profilerMarkerFrameCaptures[i].ReadSeconds();
                        profilerPeakBehaviourUpdateSeconds = completedBehaviourUpdateSeconds;
                        profilerPeakPhysicsSimulateSeconds = completedPhysicsSimulateSeconds;
                        profilerPeakGcCollectSeconds = completedGcCollectSeconds;
                        profilerPeakWallFrameSeconds = pendingProfilerFrameWallSeconds;
                        profilerPeakUnityDeltaTime = pendingProfilerFrameUnityDeltaTime;
                        profilerPeakStreamingWorkSeconds = pendingProfilerFrameStreamingWorkSeconds;
                        profilerPeakStreamingStageSeconds = pendingProfilerFrameStreamingStageSeconds;
                        profilerPeakStreamingStage = pendingProfilerFrameStreamingStage;
                        profilerPeakUnityFrame = pendingProfilerFrameUnityFrame;
                        profilerPeakStreamerFrame = pendingProfilerFrameStreamerFrame;
                        profilerPeakGen0Collections = pendingProfilerFrameGen0Collections;
                        profilerPeakGen1Collections = pendingProfilerFrameGen1Collections;
                        profilerPeakGen2Collections = pendingProfilerFrameGen2Collections;
                        profilerPeakAllocatedBytes = pendingProfilerFrameAllocatedBytes;
                    }

                    if (pendingProfilerFrameUnityFrame == worstWallFrameUnityFrame)
                    {
                        worstWallFrameMainThreadSeconds = completedFrameMainThreadSeconds;
                        for (int i = 0; i < profilerMarkerFrameCaptures.Count; i++)
                            profilerMarkerFrameCaptures[i].WorstWallFrameSeconds = profilerMarkerFrameCaptures[i].ReadSeconds();
                        worstWallFrameBehaviourUpdateSeconds = completedBehaviourUpdateSeconds;
                        worstWallFramePhysicsSimulateSeconds = completedPhysicsSimulateSeconds;
                        worstWallFrameGcCollectSeconds = completedGcCollectSeconds;
                    }
                }
                else
                {
                    if (completedFrameMainThreadSeconds > maximumHighSpeedCoastMainThreadSeconds)
                    {
                        maximumHighSpeedCoastMainThreadSeconds = completedFrameMainThreadSeconds;
                        maximumHighSpeedCoastMainThreadUnityFrame = pendingProfilerFrameUnityFrame;
                        maximumHighSpeedCoastMainThreadWallFrameSeconds = pendingProfilerFrameWallSeconds;
                    }
                    if (pendingProfilerFrameUnityFrame == worstHighSpeedCoastUnityFrame)
                    {
                        worstHighSpeedCoastMainThreadSeconds = completedFrameMainThreadSeconds;
                        for (int i = 0; i < profilerMarkerFrameCaptures.Count; i++)
                            profilerMarkerFrameCaptures[i].WorstCoastFrameSeconds = profilerMarkerFrameCaptures[i].ReadSeconds();
                        worstCoastFrameBehaviourUpdateSeconds = completedBehaviourUpdateSeconds;
                        worstCoastFramePhysicsSimulateSeconds = completedPhysicsSimulateSeconds;
                        worstCoastFrameGcCollectSeconds = completedGcCollectSeconds;
                    }
                }

                pendingProfilerFrameValid = false;
            }
            // note: Measure a fixed real-time travel window; frame-count limits would under-travel when Unity renders millisecond frames.
            // note: Measure the unchanged 2.45-second stress window only after a valid approach, independently of setup elapsed time.
            streamer.SetCompleteCellObservationPhase("input");
            phaseStarted = Time.realtimeSinceStartupAsDouble;
            while (boundaryApproachReady &&
                Time.realtimeSinceStartupAsDouble - phaseStarted < highSpeedProbeDurationSeconds)
            {
                // note: Cover every direction once, then sustain the checked seam heading for the rest of the unchanged 2.45-second window; frame count must not select a random coast direction.
                int transitionIndex = Speed260DirectionIndex(highSpeedFrames, highSpeedOrbitDirections.Length);
                Vector2 expectedHighSpeedInput = speedProbeInputAxes;
                UnityEngine.InputSystem.LowLevel.KeyboardState highSpeedInput = highSpeedFrames == 0
                    ? speedProbeInputState
                    : KeyboardStateForMotorDirection(motor, boostTravelDirection, out expectedHighSpeedInput);
                if (transitionIndex >= 0)
                    highSpeedInput = KeyboardStateForMotorDirection(
                        motor,
                        highSpeedOrbitDirections[transitionIndex],
                        out expectedHighSpeedInput);
                // note: Measure each real input frame and capture GC deltas so a boundary hitch cannot be hidden by the 260 m/s distance total.
                Vector3 highSpeedFrameStartPosition = player.position;
                double highSpeedFrameStarted = Time.realtimeSinceStartupAsDouble;
#if UNITY_EDITOR
                // note: Seed the actual first input boundary without adding a frame or warmup, so its maximum can be attributed instead of silently omitted.
                if (highSpeedFrames == 0) frameTrace?.RecordBoundary(Time.frameCount, false, 0f);
#endif
                int highSpeedFrameContactsBefore = motor.BlockingControllerContactCount;
                long allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
                int gen0CollectionsBefore = GC.CollectionCount(0);
                int gen1CollectionsBefore = GC.CollectionCount(1);
                int gen2CollectionsBefore = GC.CollectionCount(2);
                if (highSpeedFrames == 0)
                {
                    firstBoostFrameExpectedInput = expectedHighSpeedInput;
                    firstBoostFrameMotorForward = motor.transform.forward;
                    firstBoostFrameMotorRight = motor.transform.right;
                }
                QueueKeyboardState(keyboard, highSpeedInput);
                yield return null;
                Vector3 highSpeedFrameEndPosition = player.position;
                double highSpeedFrameEnded = Time.realtimeSinceStartupAsDouble;
                float highSpeedFrameSeconds = (float)Math.Max(0d, highSpeedFrameEnded - highSpeedFrameStarted);
                if (highSpeedWallFrameSampleCount < highSpeedWallFrameSamples.Length)
                    highSpeedWallFrameSamples[highSpeedWallFrameSampleCount++] = highSpeedFrameSeconds;
                float highSpeedUnityDeltaTime = Time.deltaTime;
                // note: Attribute sampled wall frames to same-frame semantic saves without changing the timed movement path.
                if (streamer.LastSemanticSaveFrameCount == Time.frameCount)
                {
                    highSpeedSaveFrameCount++;
                    maximumHighSpeedSaveFrameSeconds = Mathf.Max(maximumHighSpeedSaveFrameSeconds, highSpeedFrameSeconds);
                }
                else
                    maximumHighSpeedNoSaveFrameSeconds = Mathf.Max(maximumHighSpeedNoSaveFrameSeconds, highSpeedFrameSeconds);
                minimumHighSpeedUnityDeltaTime = Mathf.Min(minimumHighSpeedUnityDeltaTime, highSpeedUnityDeltaTime);
                if (highSpeedUnityDeltaTime >= maximumHighSpeedUnityDeltaTime)
                {
                    maximumHighSpeedUnityDeltaTime = highSpeedUnityDeltaTime;
                    maximumHighSpeedUnityDeltaTimeFrame = Time.frameCount;
                }
                long highSpeedAllocatedBytes = Math.Max(0L, GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore);
                maximumHighSpeedAllocatedBytesPerFrame = Math.Max(
                    maximumHighSpeedAllocatedBytesPerFrame,
                    highSpeedAllocatedBytes);
                AttributeLastCompletedProfilerSample();
                if (highSpeedFrameSeconds >= maximumHighSpeedFrameSeconds)
                {
                    maximumHighSpeedFrameSeconds = highSpeedFrameSeconds;
                    maximumHighSpeedFrameFocused = Application.isFocused;
                    worstFrameGen0Collections = Math.Max(0, GC.CollectionCount(0) - gen0CollectionsBefore);
                    worstFrameGen1Collections = Math.Max(0, GC.CollectionCount(1) - gen1CollectionsBefore);
                    worstFrameGen2Collections = Math.Max(0, GC.CollectionCount(2) - gen2CollectionsBefore);
                    worstWallFrameAllocatedBytes = highSpeedAllocatedBytes;
                    // note: Associate streamer CPU and its longest stage with the same measured wall frame instead of comparing unrelated high-water marks.
                    worstWallFrameUnityDeltaTime = highSpeedUnityDeltaTime;
                    worstWallFrameStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                    worstWallFrameStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                    worstWallFrameStreamingStage = streamer.LastStreamingWorstStage;
                    worstWallFrameUnityFrame = Time.frameCount;
                    worstWallFrameStreamerFrame = streamer.LastStreamingWorkFrameCount;
                }
                pendingProfilerFrameValid = mainThreadTimeRecorder.Valid && mainThreadTimeRecorder.Count > 0;
                pendingProfilerFrameWasHighSpeed = true;
                pendingProfilerFrameUnityFrame = Time.frameCount;
                pendingProfilerFrameWallSeconds = highSpeedFrameSeconds;
                pendingProfilerFrameUnityDeltaTime = highSpeedUnityDeltaTime;
                pendingProfilerFrameStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                pendingProfilerFrameStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                pendingProfilerFrameStreamingStage = streamer.LastStreamingWorstStage;
                pendingProfilerFrameStreamerFrame = streamer.LastStreamingWorkFrameCount;
                pendingProfilerFrameGen0Collections = Math.Max(0, GC.CollectionCount(0) - gen0CollectionsBefore);
                pendingProfilerFrameGen1Collections = Math.Max(0, GC.CollectionCount(1) - gen1CollectionsBefore);
                pendingProfilerFrameGen2Collections = Math.Max(0, GC.CollectionCount(2) - gen2CollectionsBefore);
                pendingProfilerFrameAllocatedBytes = highSpeedAllocatedBytes;
                CaptureR2FrameTiming(
                    false,
                    pendingProfilerFrameUnityFrame,
                    pendingProfilerFrameWallSeconds,
                    pendingProfilerFrameUnityDeltaTime,
                    pendingProfilerFrameStreamingWorkSeconds,
                    pendingProfilerFrameStreamingStageSeconds,
                    pendingProfilerFrameStreamingStage,
                    pendingProfilerFrameAllocatedBytes,
                    pendingProfilerFrameGen0Collections,
                    pendingProfilerFrameGen1Collections,
                    pendingProfilerFrameGen2Collections);
                using (Speed260TraversabilityMarker.Auto())
                    ValidateProductionMotorFrame(streamer, player, characterController, "speed-260", report);
                highSpeedFrames++;
                if (!Application.isFocused)
                    highSpeedUnfocusedFrames++;
                Vector3 frameTravelDelta = highSpeedFrameEndPosition - highSpeedFrameStartPosition;
                Vector2 frameTravelPlanar = new Vector2(frameTravelDelta.x, frameTravelDelta.z);
                float frameTravelDistance = frameTravelPlanar.magnitude;
                // note: A physical input-phase collision invalidates the test route just as a coast collision does; do not mislabel lost motion as a streaming or motor-input failure.
                Vector2 requestedInputStep = new Vector2(motor.PlanarVelocity.x, motor.PlanarVelocity.z) * motor.LastMovementDeltaTime;
                float expectedInputStep = requestedInputStep.magnitude;
                float completedInputStep = expectedInputStep > 0f ? Vector2.Dot(frameTravelPlanar, requestedInputStep / expectedInputStep) : 0f;
                if (motor.BlockingControllerContactCount > highSpeedFrameContactsBefore && !motor.LastMoveRejectedByTraversalGate &&
                    completedInputStep + Mathf.Max(0.02f, characterController.skinWidth * 2f) < expectedInputStep)
                {
                    inputObstructedFrames++;
                    if (firstInputObstructionFrame < 0)
                    {
                        firstInputObstructionFrame = Time.frameCount;
                        firstInputObstructionPosition = player.position;
                        firstInputObstructionNormal = motor.LastBlockingControllerContactNormal;
                        firstInputObstructionContactId = motor.LastBlockingControllerCollider != null ? motor.LastBlockingControllerCollider.GetInstanceID() : 0;
                        firstInputObstructionExpected = expectedInputStep;
                        firstInputObstructionCompleted = completedInputStep;
                    }
                }
                highSpeedTravelDistance += frameTravelDistance;
                // note: Use monotonic wall time for actual travel speed; Unity deltaTime remains diagnostic because it may be scaled or overridden.
                float observedWallPlanarSpeed = frameTravelDistance / Mathf.Max(0.000001f, highSpeedFrameSeconds);
                float observedUnityPlanarSpeed = frameTravelDistance / Mathf.Max(0.000001f, highSpeedUnityDeltaTime);
                // note: The motor integrates displacement with Time.deltaTime; retain wall speed separately so frame pacing cannot distort its movement-speed contract.
                maximumHighSpeedObserved = Mathf.Max(
                    maximumHighSpeedObserved,
                    observedUnityPlanarSpeed);
                maximumHighSpeedWallObserved = Mathf.Max(maximumHighSpeedWallObserved, observedWallPlanarSpeed);
                maximumRequestedHighSpeed = Mathf.Max(maximumRequestedHighSpeed, motor.PlanarVelocity.magnitude);
                if (highSpeedFrames == 1)
                {
                    // note: Preserve same-frame capsule collision and endpoint evidence so a short stress step can be assigned to terrain/contact or the traversal gate.
                    firstBoostFrameConsumedInput = motor.MoveInput;
                    firstBoostFrameSeconds = highSpeedFrameSeconds;
                    firstBoostFramePlanarDistance = frameTravelDistance;
                    firstBoostFrameWallPlanarSpeed = observedWallPlanarSpeed;
                    firstBoostFrameUnityPlanarSpeed = observedUnityPlanarSpeed;
                    firstBoostFrameRequestedPlanarSpeed = motor.PlanarVelocity.magnitude;
                    firstBoostFrameUnityDeltaTime = highSpeedUnityDeltaTime;
                    firstBoostFrameMotorDeltaTime = motor.LastMovementDeltaTime;
                    firstBoostFrameMotorPlanarSpeed = frameTravelDistance /
                        Mathf.Max(0.000001f, firstBoostFrameMotorDeltaTime);
                    firstBoostFrameRequestedMove = motor.LastRequestedMoveDisplacement;
                    firstBoostFrameActualMove = motor.LastActualMoveDisplacement;
                    firstBoostFrameStartPosition = highSpeedFrameStartPosition;
                    firstBoostFrameEndPosition = highSpeedFrameEndPosition;
                    firstBoostFrameCollisionFlags = motor.LastMoveCollisionFlags;
                    firstBoostFrameReadinessGateRejected = motor.LastMoveRejectedByTraversalGate;
                    firstBoostFrameControllerGrounded = characterController.isGrounded;
                    firstBoostFrameInputObserved = IsExpectedMotorInput(motor.MoveInput, speedProbeInputAxes);
                    Vector3 firstBoostRequestedDirection =
                        motor.transform.forward * motor.MoveInput.y +
                        motor.transform.right * motor.MoveInput.x;
                    Vector2 firstBoostRequestedPlanar = new Vector2(
                        firstBoostRequestedDirection.x,
                        firstBoostRequestedDirection.z);
                    firstBoostFrameDirectionDot = firstBoostRequestedPlanar.sqrMagnitude > 0.0001f
                        ? Vector2.Dot(frameTravelPlanar.normalized, firstBoostRequestedPlanar.normalized)
                        : 0f;
                    firstBoostFrameSideContacts = Math.Max(0, motor.BlockingControllerContactCount - highSpeedSideContactsBefore);
                    if (firstBoostFrameSideContacts > 0 && motor.LastBlockingControllerCollider != null)
                    {
                        firstBoostFrameBlockingContact = motor.LastBlockingControllerCollider.name;
                        firstBoostFrameContactPoint = motor.LastBlockingControllerContactPoint;
                        firstBoostFrameContactNormal = motor.LastBlockingControllerContactNormal;
                    }
                    firstBoostFrameDirectionObserved = firstBoostFrameInputObserved &&
                        firstBoostFrameUnityPlanarSpeed >= 240f &&
                        firstBoostRequestedPlanar.sqrMagnitude > 0.0001f &&
                        firstBoostFrameDirectionDot >= 0.9f;
                    firstBoostEndCell = ChunkCoordinateAt(player.position, streamer.chunkWorldSize);
                    firstBoostFrameCrossedChunkBoundary = firstBoostEndCell != firstBoostStartCell &&
                        (firstBoostEndCell == firstBoostMinimumTargetCell ||
                            firstBoostEndCell == firstBoostMaximumTargetCell);
                }
                // note: Sum real distances over constant-input legs of the safe loop; each segment remains an ordinary straight motor displacement.
                highSpeedConstantInputSegmentDistance += frameTravelDistance;
                highSpeedInputObserved |= motor.MoveInput.y < -0.1f;
                highSpeedGroundedObserved |= motor.IsGrounded;
                if (!highSpeedLoopCaptureWritten && transitionIndex >= 0)
                {
                    // note: Capture the collision owner at the first real high-speed turn before later loop segments can change the contact.
                    AppendProductionMovementDiagnostics(report, motor, "speed260-loop", highSpeedSideContactsBefore);
                    // note: Defer the full report flush until the benchmark ends so synchronous file I/O cannot manufacture its own frame-budget failure.
                    highSpeedLoopCaptureWritten = true;
                }
                if (transitionIndex >= 0)
                {
                    bool transitionInputObserved = IsExpectedMotorInput(motor.MoveInput, expectedHighSpeedInput);
                    int transitionMaskBit = HighSpeedTransitionMaskBit(expectedHighSpeedInput);
                        Vector3 requestedTransitionDirection =
                            motor.transform.forward * motor.MoveInput.y +
                            motor.transform.right * motor.MoveInput.x;
                        Vector2 requestedTransitionPlanar = new Vector2(
                            requestedTransitionDirection.x,
                            requestedTransitionDirection.z);
                        float actualTransitionUnitySpeed = frameTravelPlanar.magnitude /
                            Mathf.Max(0.000001f, highSpeedUnityDeltaTime);
                        highSpeedDirectionSamples[transitionIndex] = new Speed260DirectionSample
                        {
                            observed = true, frame = Time.frameCount, maskBit = transitionMaskBit,
                            expectedInput = expectedHighSpeedInput, consumedInput = motor.MoveInput,
                            unitySeconds = highSpeedUnityDeltaTime, motorSeconds = motor.LastMovementDeltaTime,
                            start = highSpeedFrameStartPosition, end = highSpeedFrameEndPosition,
                            requestedMove = motor.LastRequestedMoveDisplacement, actualMove = motor.LastActualMoveDisplacement,
                            speed = actualTransitionUnitySpeed, directionDot = requestedTransitionPlanar.sqrMagnitude > 0.0001f
                                ? Vector2.Dot(frameTravelPlanar.normalized, requestedTransitionPlanar.normalized) : 0f,
                            sideContacts = Math.Max(0, motor.BlockingControllerContactCount - highSpeedFrameContactsBefore),
                            readinessRejected = motor.LastMoveRejectedByTraversalGate, collisionFlags = motor.LastMoveCollisionFlags,
                            contact = motor.LastBlockingControllerCollider,
                            contactId = motor.LastBlockingControllerCollider != null ? motor.LastBlockingControllerCollider.GetInstanceID() : 0,
                            contactNormal = motor.LastBlockingControllerContactNormal
                        };
                    if (transitionInputObserved && transitionMaskBit >= 0)
                    {
                        highSpeedTransitionInputMask |= 1 << transitionMaskBit;
                        if (actualTransitionUnitySpeed >= 240f &&
                            requestedTransitionPlanar.sqrMagnitude > 0.0001f &&
                            Vector2.Dot(frameTravelPlanar.normalized, requestedTransitionPlanar.normalized) >= 0.9f)
                            highSpeedTransitionDisplacementMask |= 1 << transitionMaskBit;
                    }
                }
                if (groundViewCoverageMaintained)
                {
                    bool groundCoverageReady;
                    using (Speed260GroundCoverageMarker.Auto())
                        groundCoverageReady = streamer.TryValidateGroundViewCoverage(out firstGroundViewFailure);
                    if (!groundCoverageReady)
                        groundViewCoverageMaintained = false;
                }
                if (visibleViewCoverageMaintained)
                {
                    bool visualCoverageReady;
                    using (Speed260VisualCoverageMarker.Auto())
                        visualCoverageReady = streamer.TryValidateCurrentVisualCoverage(out firstVisibleViewFailure);
                    if (!visualCoverageReady)
                    {
                        // note: Capture the current owner's progress once, on the first failed visible frame.
                        visibleViewCoverageMaintained = false;
                        // note: Input failure precedes coast failure; freeze its history now instead of allowing the coast cell to replace the earliest deadline miss.
                        streamer.FreezeAppearanceBudgetObservation();
                        firstVisibleViewFailure += streamer.DescribeLastVisualCoverageOwnerProgress();
                    }
                }
                if (highSpeedTurnBufferMaintained)
                {
                    bool turnBufferReady;
                    using (Speed260TurnBufferMarker.Auto())
                        turnBufferReady = streamer.TryValidateHighSpeedTurnBufferCoverage(out firstTurnBufferFailure);
                    if (!turnBufferReady)
                        highSpeedTurnBufferMaintained = false;
                }
                if (highSpeedFrames == 1)
                {
                    firstBoostFrameGroundCoverage = groundViewCoverageMaintained;
                    firstBoostFrameVisibleCoverage = visibleViewCoverageMaintained;
                    firstBoostFrameTurnBufferCoverage = highSpeedTurnBufferMaintained;
                }
            }
            // note: Keep observing after key release because the authoritative motor retains momentum; the visible terrain contract lasts until that real velocity settles.
            float highSpeedCoastStartSpeed = motor.PlanarVelocity.magnitude;
            Vector3 highSpeedCoastStartPosition = player.position;
            int highSpeedCoastFrames = 0;
            int highSpeedCoastTraversalBlocksBefore = streamer.TraversalConstraintBlockCount;
            int highSpeedCoastFallbackAttemptsBefore = streamer.SynchronousGroundFallbackAttemptCount;
            double highSpeedCoastStartedAt = Time.realtimeSinceStartupAsDouble;
            float maximumHighSpeedCoastFrameSeconds = 0f;
            bool maximumHighSpeedCoastFrameFocused = true;
            int highSpeedCoastUnfocusedFrames = 0;
            float[] highSpeedCoastWallFrameSamples = new float[4096];
            int highSpeedCoastWallFrameSampleCount = 0;
            float maximumHighSpeedCoastSaveFrameSeconds = 0f;
            float maximumHighSpeedCoastNoSaveFrameSeconds = 0f;
            int highSpeedCoastSaveFrameCount = 0;
            float worstHighSpeedCoastStreamingWorkSeconds = 0f;
            float worstHighSpeedCoastStreamingStageSeconds = 0f;
            string worstHighSpeedCoastStreamingStage = string.Empty;
            long worstHighSpeedCoastAllocatedBytes = 0L;
            int worstHighSpeedCoastGen0Collections = 0;
            int worstHighSpeedCoastGen1Collections = 0;
            int worstHighSpeedCoastGen2Collections = 0;
            // note: Airborne braking uses acceleration*airControl, which can need longer than the old 12-second cutoff; retain a bounded full natural coast.
            double highSpeedCoastTimeoutSeconds = Speed260CoastTimeoutSeconds(highSpeedCoastStartSpeed,
                originalDeceleration, originalAcceleration * motor.airControl);
            bool highSpeedCoastBrakingConfigValid = !double.IsNaN(highSpeedCoastTimeoutSeconds);
            bool highSpeedCoastInputReleased = true;
            bool highSpeedCoastGroundCoverageMaintained = true;
            bool highSpeedCoastVisibleCoverageMaintained = true;
            bool highSpeedCoastStartGrounded = motor.IsGrounded;
            // note: Requested velocity can continue decaying against a wall; only real capsule progress supplies coast streaming demand.
            float coastExpectedTravel = 0f;
            float coastForwardTravel = 0f;
            float coastPathDistance = 0f;
            float coastTravelTolerance = 0f;
            int coastObstructedFrames = 0;
            string firstCoastObstruction = string.Empty;
            float coastFrameTravelTolerance = Mathf.Max(0.02f, characterController.skinWidth * 2f);
            string firstHighSpeedCoastGroundFailure = string.Empty;
            string firstHighSpeedCoastVisibleFailure = string.Empty;
            R2CoastMotionDiagnostic[] coastMotionSamples = captureCoastMotion ? new R2CoastMotionDiagnostic[512] : null;
            int coastMotionSampleCount = 0;
            int coastMotionDropped = 0;
            int coastContactOwnerId = 0;
            string coastContactOwner = "<none>";
            // note: Restore both ground braking and airborne control before release; HandleMove uses acceleration times air control while airborne.
            streamer.SetCompleteCellObservationPhase("coast");
            motor.acceleration = originalAcceleration;
            motor.deceleration = originalDeceleration;
            QueueKeyboardState(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            while (Time.realtimeSinceStartupAsDouble - highSpeedCoastStartedAt < highSpeedCoastTimeoutSeconds &&
                   motor.PlanarVelocity.sqrMagnitude > 4f)
            {
                // note: Measure coast frame duration, streamer attribution, allocation, and GC on the same sample as coverage after input release.
                double coastFrameStartedAt = Time.realtimeSinceStartupAsDouble;
                long coastAllocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
                int coastGen0CollectionsBefore = GC.CollectionCount(0);
                int coastGen1CollectionsBefore = GC.CollectionCount(1);
                int coastGen2CollectionsBefore = GC.CollectionCount(2);
                Vector3 coastMotionStart = player.position;
                int coastContactsBefore = motor.BlockingControllerContactCount;
                yield return null;
                highSpeedCoastFrames++;
                Vector3 coastRequested = motor.PlanarVelocity * motor.LastMovementDeltaTime;
                Vector3 coastActual = player.position - coastMotionStart;
                coastActual.y = 0f;
                float expectedCoastStep = coastRequested.magnitude;
                float completedCoastStep = expectedCoastStep > 0f
                    ? Mathf.Max(0f, Vector3.Dot(coastActual, coastRequested / expectedCoastStep)) : 0f;
                coastExpectedTravel += expectedCoastStep;
                coastForwardTravel += completedCoastStep;
                coastPathDistance += coastActual.magnitude;
                coastTravelTolerance += Mathf.Min(expectedCoastStep, coastFrameTravelTolerance);
                bool coastSideContact = motor.BlockingControllerContactCount > coastContactsBefore;
                if (coastSideContact && !motor.LastMoveRejectedByTraversalGate &&
                    completedCoastStep + coastFrameTravelTolerance < expectedCoastStep)
                {
                    coastObstructedFrames++;
                    if (string.IsNullOrEmpty(firstCoastObstruction))
                    {
                        Collider obstruction = motor.LastBlockingControllerCollider;
                        firstCoastObstruction = "frame=" + Time.frameCount + ";position=" + player.position.ToString("F3") +
                            ";requestedMeters=" + expectedCoastStep.ToString("F3", CultureInfo.InvariantCulture) +
                            ";completedMeters=" + completedCoastStep.ToString("F3", CultureInfo.InvariantCulture) +
                            ";owner=" + (obstruction != null ? DescribeColliderHierarchy(obstruction.transform) : "<unknown>") +
                            ";point=" + motor.LastBlockingControllerContactPoint.ToString("F3") +
                            ";normal=" + motor.LastBlockingControllerContactNormal.ToString("F3") +
                            ";readinessRejected=" + motor.LastMoveRejectedByTraversalGate;
                    }
                }
                if (captureCoastMotion)
                {
                    // note: Snapshot collision evidence at coroutine resumption; step-assist can overwrite the last Move, so retain net frame motion and cumulative contact deltas too.
                    int sideContacts = Math.Max(0, motor.BlockingControllerContactCount - coastContactsBefore);
                    Collider contact = sideContacts > 0 ? motor.LastBlockingControllerCollider : null;
                    if (contact != null && contact.GetInstanceID() != coastContactOwnerId)
                    {
                        coastContactOwnerId = contact.GetInstanceID();
                        coastContactOwner = DescribeColliderHierarchy(contact.transform);
                    }
                    if (coastMotionSampleCount < coastMotionSamples.Length)
                        coastMotionSamples[coastMotionSampleCount++] = new R2CoastMotionDiagnostic
                        {
                            UnityFrame = Time.frameCount,
                            Start = coastMotionStart,
                            End = player.position,
                            Velocity = motor.PlanarVelocity,
                            MotorDeltaSeconds = motor.LastMovementDeltaTime,
                            LastRequestedMove = motor.LastRequestedMoveDisplacement,
                            LastActualMove = motor.LastActualMoveDisplacement,
                            Flags = motor.LastMoveCollisionFlags,
                            Rejected = motor.LastMoveRejectedByTraversalGate,
                            SideContacts = sideContacts,
                            ContactOwner = contact != null ? coastContactOwner : "<none this frame>",
                            ContactPoint = motor.LastBlockingControllerContactPoint,
                            ContactNormal = motor.LastBlockingControllerContactNormal
                        };
                    else
                        coastMotionDropped++;
                }
                float coastFrameSeconds = (float)Math.Max(
                    0d,
                    Time.realtimeSinceStartupAsDouble - coastFrameStartedAt);
                if (highSpeedCoastWallFrameSampleCount < highSpeedCoastWallFrameSamples.Length)
                    highSpeedCoastWallFrameSamples[highSpeedCoastWallFrameSampleCount++] = coastFrameSeconds;
                if (streamer.LastSemanticSaveFrameCount == Time.frameCount)
                {
                    highSpeedCoastSaveFrameCount++;
                    maximumHighSpeedCoastSaveFrameSeconds = Mathf.Max(maximumHighSpeedCoastSaveFrameSeconds, coastFrameSeconds);
                }
                else
                    maximumHighSpeedCoastNoSaveFrameSeconds = Mathf.Max(maximumHighSpeedCoastNoSaveFrameSeconds, coastFrameSeconds);
                long coastAllocatedBytes = Math.Max(
                    0L,
                    GC.GetAllocatedBytesForCurrentThread() - coastAllocatedBytesBefore);
                AttributeLastCompletedProfilerSample();
                if (coastFrameSeconds >= maximumHighSpeedCoastFrameSeconds)
                {
                    maximumHighSpeedCoastFrameSeconds = coastFrameSeconds;
                    maximumHighSpeedCoastFrameFocused = Application.isFocused;
                    worstHighSpeedCoastStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                    worstHighSpeedCoastStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                    worstHighSpeedCoastStreamingStage = streamer.LastStreamingWorstStage;
                    worstHighSpeedCoastUnityFrame = Time.frameCount;
                    worstHighSpeedCoastAllocatedBytes = coastAllocatedBytes;
                    worstHighSpeedCoastGen0Collections = Math.Max(0, GC.CollectionCount(0) - coastGen0CollectionsBefore);
                    worstHighSpeedCoastGen1Collections = Math.Max(0, GC.CollectionCount(1) - coastGen1CollectionsBefore);
                    worstHighSpeedCoastGen2Collections = Math.Max(0, GC.CollectionCount(2) - coastGen2CollectionsBefore);
                }
                if (!Application.isFocused)
                    highSpeedCoastUnfocusedFrames++;
                pendingProfilerFrameValid = mainThreadTimeRecorder.Valid && mainThreadTimeRecorder.Count > 0;
                pendingProfilerFrameWasHighSpeed = false;
                pendingProfilerFrameUnityFrame = Time.frameCount;
                pendingProfilerFrameWallSeconds = coastFrameSeconds;
                pendingProfilerFrameUnityDeltaTime = Time.deltaTime;
                pendingProfilerFrameStreamingWorkSeconds = streamer.LastStreamingWorkSeconds;
                pendingProfilerFrameStreamingStageSeconds = streamer.LastStreamingWorstStageSeconds;
                pendingProfilerFrameStreamingStage = streamer.LastStreamingWorstStage;
                pendingProfilerFrameStreamerFrame = streamer.LastStreamingWorkFrameCount;
                pendingProfilerFrameGen0Collections = Math.Max(0, GC.CollectionCount(0) - coastGen0CollectionsBefore);
                pendingProfilerFrameGen1Collections = Math.Max(0, GC.CollectionCount(1) - coastGen1CollectionsBefore);
                pendingProfilerFrameGen2Collections = Math.Max(0, GC.CollectionCount(2) - coastGen2CollectionsBefore);
                pendingProfilerFrameAllocatedBytes = coastAllocatedBytes;
                CaptureR2FrameTiming(
                    true,
                    pendingProfilerFrameUnityFrame,
                    pendingProfilerFrameWallSeconds,
                    pendingProfilerFrameUnityDeltaTime,
                    pendingProfilerFrameStreamingWorkSeconds,
                    pendingProfilerFrameStreamingStageSeconds,
                    pendingProfilerFrameStreamingStage,
                    pendingProfilerFrameAllocatedBytes,
                    pendingProfilerFrameGen0Collections,
                    pendingProfilerFrameGen1Collections,
                    pendingProfilerFrameGen2Collections);
                if (motor.MoveInput.sqrMagnitude > 0.0001f)
                    highSpeedCoastInputReleased = false;
                if (highSpeedCoastGroundCoverageMaintained)
                {
                    bool groundCoverageReady;
                    using (Speed260GroundCoverageMarker.Auto())
                        groundCoverageReady = streamer.TryValidateGroundViewCoverage(out firstHighSpeedCoastGroundFailure);
                    if (!groundCoverageReady)
                        highSpeedCoastGroundCoverageMaintained = false;
                }
                if (highSpeedCoastVisibleCoverageMaintained)
                {
                    bool visualCoverageReady;
                    using (Speed260VisualCoverageMarker.Auto())
                        visualCoverageReady = streamer.TryValidateCurrentVisualCoverage(out firstHighSpeedCoastVisibleFailure);
                    if (!visualCoverageReady)
                    {
                        // note: Capture the current owner's progress once, on the first failed visible frame.
                        highSpeedCoastVisibleCoverageMaintained = false;
                        firstHighSpeedCoastVisibleFailure += streamer.DescribeLastVisualCoverageOwnerProgress();
                        streamer.FreezeAppearanceBudgetObservation();
                    }
                }
            }
            streamer.SetCompleteCellObservationPhase("post");
            bool highSpeedCoastSettled = motor.PlanarVelocity.magnitude <= 2f;
            float highSpeedCoastElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - highSpeedCoastStartedAt);
            float highSpeedCoastDistance = PlanarDistance(highSpeedCoastStartPosition, player.position);
            bool coastPhysicalTravelPassed = Speed260CoastTravelValid(coastExpectedTravel, coastForwardTravel,
                coastTravelTolerance, highSpeedCoastDistance, streamer.chunkWorldSize) && coastObstructedFrames == 0;
            int highSpeedCoastTraversalBlocks = streamer.TraversalConstraintBlockCount - highSpeedCoastTraversalBlocksBefore;
            int highSpeedCoastFallbackAttempts = streamer.SynchronousGroundFallbackAttemptCount - highSpeedCoastFallbackAttemptsBefore;
            if (captureCoastMotion)
            {
                // note: Format the bounded timeline after the measured coast has ended, preserving the full original input and braking window.
                report.AppendLine("- coastMotionSamples: retained=" + coastMotionSampleCount + " dropped=" + coastMotionDropped);
                for (int i = 0; i < coastMotionSampleCount; i++)
                {
                    R2CoastMotionDiagnostic sample = coastMotionSamples[i];
                    report.AppendLine("  coastMotion frame=" + sample.UnityFrame +
                        " start=" + sample.Start.ToString("F4") + " end=" + sample.End.ToString("F4") +
                        " velocity=" + sample.Velocity.ToString("F4") + " motorDelta=" + sample.MotorDeltaSeconds.ToString("F6") +
                        " lastRequested=" + sample.LastRequestedMove.ToString("F4") + " lastActual=" + sample.LastActualMove.ToString("F4") +
                        " flags=" + sample.Flags + " rejected=" + sample.Rejected + " sideContacts=" + sample.SideContacts +
                        " owner=" + sample.ContactOwner + " point=" + sample.ContactPoint.ToString("F4") +
                        " normal=" + sample.ContactNormal.ToString("F4"));
                }
            }
            bool highSpeedCoastFrameBudgetPassed = highSpeedCoastFrames > 0 &&
                maximumHighSpeedCoastFrameSeconds <= YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds;
            bool highSpeedCoastObserved = highSpeedCoastBrakingConfigValid && highSpeedCoastStartSpeed >= 240f && highSpeedCoastFrames > 1 &&
                highSpeedCoastElapsedSeconds >= 1f && coastPhysicalTravelPassed &&
                highSpeedCoastInputReleased && highSpeedCoastSettled &&
                highSpeedCoastGroundCoverageMaintained && highSpeedCoastVisibleCoverageMaintained &&
                highSpeedCoastTraversalBlocks == 0 && highSpeedCoastFallbackAttempts == 0;
            if (pendingProfilerFrameValid && !pendingProfilerFrameWasHighSpeed)
            {
                // note: Advance one neutral frame so the completed profiler sample for the final measured coast frame can be paired before reporting.
                yield return null;
                AttributeLastCompletedProfilerSample();
            }

            double highSpeedWitnessElapsedSeconds = Math.Max(
                0.001d,
                Time.realtimeSinceStartupAsDouble - highSpeedWitnessStartedAt);
#if UNITY_EDITOR
            frameTrace?.Dispose();
#endif
            long backgroundHeightTasksDuringSpeed = Math.Max(
                0L,
                streamer.BackgroundHeightTaskCount - backgroundHeightTasksBeforeSpeed);
            double backgroundHeightTaskSecondsDuringSpeed = Math.Max(
                0d,
                streamer.BackgroundHeightTaskTotalSeconds - backgroundHeightTaskSecondsBeforeSpeed);
            int terrainHandoffsDuringSpeed = Math.Max(
                0,
                streamer.TerrainPreparationHandoffCount - terrainHandoffsBeforeSpeed);
            float terrainHandoffSecondsDuringSpeed = Mathf.Max(
                0f,
                streamer.TerrainPreparationHandoffTotalSeconds - terrainHandoffSecondsBeforeSpeed);

            float highSpeedPlanarDistance = PlanarDistance(highSpeedStartPosition, player.position);
            int highSpeedTraversalBlocks = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeHighSpeed;
            int highSpeedSynchronousGroundFallbacks =
                streamer.SynchronousGroundFallbackCount - synchronousGroundFallbacksBeforeSpeedProbe;
            int highSpeedSynchronousGroundFallbackAttempts =
                streamer.SynchronousGroundFallbackAttemptCount - synchronousGroundFallbackAttemptsBeforeSpeedProbe;
            bool highSpeedNoSynchronousGroundFallbacks = highSpeedSynchronousGroundFallbackAttempts == 0;
            int fallSafetyRecoveryDelta = fallSafety != null
                ? fallSafety.RecoveryCount - fallSafetyRecoveryCountAtTraversalStart
                : -1;
            int historicalDropWithoutPenetrationDelta = fallSafety != null
                ? fallSafety.HistoricalDropWithoutPenetrationCount - historicalDropCountAtTraversalStart
                : -1;
            bool fallSafetyRecoveryGatePassed = fallSafety != null && fallSafetyRecoveryDelta == 0;
            bool realElapsedTransitionObserved = preBoostWarmupSeconds > 0d && firstBoostFrameSeconds > 0f &&
                preBoostUnityPlanarSpeed >= 4f && preBoostUnityPlanarSpeed <= 12f &&
                firstBoostFrameUnityPlanarSpeed >= 240f &&
                firstBoostFrameUnityPlanarSpeed <= firstBoostFrameRequestedPlanarSpeed + 20f;
            bool highSpeedBoundaryObserved = boundaryApproachReady && boundaryApproachClean && firstBoostTargetCellReady &&
                firstBoostFrameCrossedChunkBoundary;
            bool highSpeedAbruptTransitionObserved =
                preBoostLowSpeedInputObserved && preBoostRequestedPlanarSpeed >= 4f && preBoostRequestedPlanarSpeed <= 12f &&
                firstBoostFrameInputObserved && firstBoostFrameRequestedPlanarSpeed >= 240f &&
                firstBoostFrameUnityPlanarSpeed >= 240f && firstBoostFrameDirectionObserved &&
                firstBoostFrameGroundCoverage && firstBoostFrameVisibleCoverage && firstBoostFrameTurnBufferCoverage &&
                highSpeedBoundaryObserved &&
                realElapsedTransitionObserved;
            bool highSpeedConstantInputSegmentsObserved = highSpeedInputObserved && highSpeedGroundedObserved &&
                inputObstructedFrames == 0 &&
                maximumHighSpeedObserved >= 240f &&
                highSpeedAbruptTransitionObserved &&
                highSpeedConstantInputSegmentDistance >= 250f && highSpeedTravelDistance >= 300f && highSpeedTraversalBlocks == 0 &&
                highSpeedNoSynchronousGroundFallbacks && fallSafetyRecoveryGatePassed &&
                highSpeedTransitionInputMask == 0b111111 && highSpeedTransitionDisplacementMask == 0b111111;
            // note: Keep physical-input evidence independent of coverage and timing verdicts; the aggregate still requires every original safety gate.
            bool highSpeedObserved = highSpeedConstantInputSegmentsObserved && highSpeedCoastObserved &&
                groundViewCoverageMaintained && visibleViewCoverageMaintained && highSpeedTurnBufferMaintained;
            bool highSpeedFrameBudgetPassed = highSpeedFrames > 0 &&
                maximumHighSpeedFrameSeconds <= YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds &&
                highSpeedCoastFrameBudgetPassed;
            report.AppendLine("- productionMotorSpeed260AbruptTransition: configuredWalkSpeed=6.0, sameHeadingWarmup=true, frames=" + preBoostWarmupFrames +
                ", seconds=" + preBoostWarmupSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", planarDistance=" + preBoostPlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", lowSpeedWallPlanarSpeed=" + preBoostWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", lowSpeedUnityDeltaTime=" + preBoostUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                ", lowSpeedUnityPlanarSpeed=" + preBoostUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", lowSpeedInputObserved=" + preBoostLowSpeedInputObserved +
                ", lowSpeedRequestedPlanarSpeed=" + preBoostRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", boundaryApproachReady=" + boundaryApproachReady +
                ", boundaryGapMeters=" + finalBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- productionMotorSpeed260FirstBoostFrame: wallSeconds=" + firstBoostFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", planarDistance=" + firstBoostFramePlanarDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                ", wallPlanarSpeed=" + firstBoostFrameWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", unityPlanarSpeed=" + firstBoostFrameUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", unityDeltaTime=" + firstBoostFrameUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                ", motorIntegratedPlanarSpeed=" + firstBoostFrameMotorPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", motorDeltaTime=" + firstBoostFrameMotorDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                ", requestedMove=" + firstBoostFrameRequestedMove.ToString("F4", CultureInfo.InvariantCulture) +
                ", actualMove=" + firstBoostFrameActualMove.ToString("F4", CultureInfo.InvariantCulture) +
                ", startPosition=" + firstBoostFrameStartPosition.ToString("F4", CultureInfo.InvariantCulture) +
                ", endPosition=" + firstBoostFrameEndPosition.ToString("F4", CultureInfo.InvariantCulture) +
                ", requestedPlanarSpeed=" + firstBoostFrameRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", collisionFlags=" + firstBoostFrameCollisionFlags +
                ", readinessGateRejected=" + firstBoostFrameReadinessGateRejected +
                ", controllerGrounded=" + firstBoostFrameControllerGrounded +
                ", blockingContact=" + firstBoostFrameBlockingContact +
                ", contactPoint=" + firstBoostFrameContactPoint.ToString("F4", CultureInfo.InvariantCulture) +
                ", contactNormal=" + firstBoostFrameContactNormal.ToString("F4", CultureInfo.InvariantCulture) +
                ", expectedMoveInput=" + firstBoostFrameExpectedInput.ToString("F2", CultureInfo.InvariantCulture) +
                ", consumedMoveInput=" + firstBoostFrameConsumedInput.ToString("F2", CultureInfo.InvariantCulture) +
                ", motorForward=" + firstBoostFrameMotorForward.ToString("F2", CultureInfo.InvariantCulture) +
                ", motorRight=" + firstBoostFrameMotorRight.ToString("F2", CultureInfo.InvariantCulture) +
                ", boostInputObserved=" + firstBoostFrameInputObserved +
                ", boostDirectionObserved=" + firstBoostFrameDirectionObserved +
                ", boostDirectionDot=" + firstBoostFrameDirectionDot.ToString("0.000", CultureInfo.InvariantCulture) +
                ", boostSideContacts=" + firstBoostFrameSideContacts +
                ", reachedNearMaximumSpeed=" + (firstBoostFrameRequestedPlanarSpeed >= 240f) +
                ", groundCoverage=" + firstBoostFrameGroundCoverage +
                ", visibleCoverage=" + firstBoostFrameVisibleCoverage +
                ", turnBufferCoverage=" + firstBoostFrameTurnBufferCoverage +
                ", startCell=" + firstBoostStartCell +
                ", minimumProjectedTargetCell=" + firstBoostMinimumTargetCell +
                ", maximumProjectedTargetCell=" + firstBoostMaximumTargetCell +
                ", expectedTargetReady=" + firstBoostTargetCellReady +
                ", actualEndCell=" + firstBoostEndCell +
                ", crossedCellBoundary=" + firstBoostFrameCrossedChunkBoundary);
            report.AppendLine("- speed260RealElapsedTransitionGate: " +
                (realElapsedTransitionObserved
                    ? "PASS"
                    : "FAIL lowWallSeconds=" + preBoostWarmupSeconds.ToString("0.000000", CultureInfo.InvariantCulture) +
                      " lowUnitySpeed=" + preBoostUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostWallSeconds=" + firstBoostFrameSeconds.ToString("0.000000", CultureInfo.InvariantCulture) +
                      " boostUnitySpeed=" + firstBoostFrameUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostWallSpeed=" + firstBoostFrameWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostRequestedSpeed=" + firstBoostFrameRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture)));
            report.AppendLine("- speed260AbruptTransitionGate: " +
                (highSpeedAbruptTransitionObserved
                    ? "PASS"
                    : "FAIL lowSpeedInput=" + preBoostLowSpeedInputObserved +
                      " lowSpeed=" + preBoostRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostInput=" + firstBoostFrameInputObserved +
                      " boostRequested=" + firstBoostFrameRequestedPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostActualUnitySpeed=" + firstBoostFrameUnityPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostWallSpeed=" + firstBoostFrameWallPlanarSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " boostDirection=" + firstBoostFrameDirectionObserved +
                      " ground=" + firstBoostFrameGroundCoverage +
                      " visible=" + firstBoostFrameVisibleCoverage +
                      " turnBuffer=" + firstBoostFrameTurnBufferCoverage));
            report.AppendLine("- speed260BoundaryCrossingGate: " +
                (highSpeedBoundaryObserved
                    ? "PASS"
                    : "FAIL approachReady=" + boundaryApproachReady +
                      " approachClean=" + boundaryApproachClean +
                      " gapMeters=" + finalBoundaryGapMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                      " targetReady=" + firstBoostTargetCellReady +
                      " startCell=" + firstBoostStartCell +
                      " minimumTargetCell=" + firstBoostMinimumTargetCell +
                      " maximumTargetCell=" + firstBoostMaximumTargetCell +
                      " actualEndCell=" + firstBoostEndCell +
                      " crossed=" + firstBoostFrameCrossedChunkBoundary));
            report.AppendLine("- speed260SynchronousFallbackGate: " +
                (highSpeedFrames > 0 && highSpeedNoSynchronousGroundFallbacks
                    ? "PASS"
                    : "FAIL frames=" + highSpeedFrames + " fallbackAttempts=" + highSpeedSynchronousGroundFallbackAttempts +
                      " successfulPublications=" + highSpeedSynchronousGroundFallbacks));
            report.AppendLine("- productionMotorSpeed260Evidence: inputObserved=" + highSpeedInputObserved +
                ", grounded=" + highSpeedGroundedObserved +
                ", trajectory=eight-directions-once-then-sustained-seam-heading-v2" +
                ", groundViewCoverageEveryFrame=" + groundViewCoverageMaintained +
                (groundViewCoverageMaintained ? string.Empty : ", firstGroundViewFailure=" + firstGroundViewFailure) +
                ", fullyLoadedVisibleViewEveryFrame=" + visibleViewCoverageMaintained +
                (visibleViewCoverageMaintained ? string.Empty : ", firstVisibleViewFailure=" + firstVisibleViewFailure) +
                ", allDirectionTurnBufferEveryFrame=" + highSpeedTurnBufferMaintained +
                (highSpeedTurnBufferMaintained ? string.Empty : ", firstTurnBufferFailure=" + firstTurnBufferFailure) +
                ", maximumActualPlanarSpeed=" + maximumHighSpeedObserved.ToString("0.0", CultureInfo.InvariantCulture) +
                ", maximumWallPlanarSpeed=" + maximumHighSpeedWallObserved.ToString("0.0", CultureInfo.InvariantCulture) +
                ", maximumRequestedPlanarSpeed=" + maximumRequestedHighSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", observedAboveRequestedPlus20=" + (maximumHighSpeedObserved > maximumRequestedHighSpeed + 20f) +
                ", planarDistance=" + highSpeedPlanarDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", constantInputSegmentDistance=" + highSpeedConstantInputSegmentDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", traveledDistance=" + highSpeedTravelDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", traversalBlocks=" + highSpeedTraversalBlocks + ", transitionInputMask=" + highSpeedTransitionInputMask +
                ", transitionDisplacementMask=" + highSpeedTransitionDisplacementMask +
                ", synchronousGroundFallbackAttempts=" + highSpeedSynchronousGroundFallbackAttempts +
                ", synchronousGroundFallbacks=" + highSpeedSynchronousGroundFallbacks +
                 ", frames=" + highSpeedFrames +
                 ", unityDeltaTimeRange=" + (minimumHighSpeedUnityDeltaTime == float.MaxValue ? 0f : minimumHighSpeedUnityDeltaTime).ToString("0.000000", CultureInfo.InvariantCulture) +
                 ".." + maximumHighSpeedUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                 ", maximumUnityDeltaTimeFrame=" + maximumHighSpeedUnityDeltaTimeFrame +
                 ", maximumFrameSeconds=" + maximumHighSpeedFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", maximumFrameApplicationFocused=" + maximumHighSpeedFrameFocused +
                ", unfocusedFrames=" + highSpeedUnfocusedFrames +
                ", maximumMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                    ? maximumHighSpeedMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                ", worstWallFrameMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                    ? worstWallFrameMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                 ", worstWallFrameStreamingWorkSeconds=" + worstWallFrameStreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                 ", worstWallFrameStreamingStage=" + worstWallFrameStreamingStage +
                 ", worstWallFrameStreamingStageSeconds=" + worstWallFrameStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                 ", worstWallFrameUnityDeltaTime=" + worstWallFrameUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                 ", worstWallFrameUnityFrame=" + worstWallFrameUnityFrame +
                ", worstWallFrameStreamerFrame=" + worstWallFrameStreamerFrame +
                 ", profilerPeakWallFrameSeconds=" + profilerPeakWallFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                 ", profilerPeakUnityDeltaTime=" + profilerPeakUnityDeltaTime.ToString("0.000000", CultureInfo.InvariantCulture) +
                 ", profilerPeakStreamingWorkSeconds=" + profilerPeakStreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakStreamingStage=" + profilerPeakStreamingStage +
                ", profilerPeakStreamingStageSeconds=" + profilerPeakStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakUnityFrame=" + profilerPeakUnityFrame +
                ", profilerPeakStreamerFrame=" + profilerPeakStreamerFrame +
                ", profilerPeakGcCollections=" + profilerPeakGen0Collections + "/" + profilerPeakGen1Collections + "/" + profilerPeakGen2Collections +
                ", profilerPeakAllocatedBytes=" + profilerPeakAllocatedBytes +
                ", profilerPeakBehaviourUpdateSeconds=" + profilerPeakBehaviourUpdateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakPhysicsSimulateSeconds=" + profilerPeakPhysicsSimulateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", profilerPeakGcCollectSeconds=" + profilerPeakGcCollectSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstWallFrameBehaviourUpdateSeconds=" + worstWallFrameBehaviourUpdateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstWallFramePhysicsSimulateSeconds=" + worstWallFramePhysicsSimulateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstWallFrameGcCollectSeconds=" + worstWallFrameGcCollectSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstWallFrameAllocatedBytes=" + worstWallFrameAllocatedBytes +
                ", maximumAllocatedBytesPerFrame=" + maximumHighSpeedAllocatedBytesPerFrame +
                ", worstFrameGcCollections=" + worstFrameGen0Collections + "/" + worstFrameGen1Collections + "/" + worstFrameGen2Collections +
                ", provisionalGround=" + streamer.ProvisionalGroundDiagnostics);
            report.AppendLine("- productionMotorSpeed260CoastEvidence: startSpeed=" + highSpeedCoastStartSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                ", frames=" + highSpeedCoastFrames +
                ", timeoutSeconds=" + highSpeedCoastTimeoutSeconds.ToString("F3", CultureInfo.InvariantCulture) +
                ", brakingConfigValid=" + highSpeedCoastBrakingConfigValid +
                ", elapsedSeconds=" + highSpeedCoastElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", travelDistance=" + highSpeedCoastDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                ", finalSpeed=" + motor.PlanarVelocity.magnitude.ToString("0.0", CultureInfo.InvariantCulture) +
                ", startGrounded=" + highSpeedCoastStartGrounded +
                ", restoredAcceleration=" + originalAcceleration.ToString("0.0", CultureInfo.InvariantCulture) +
                ", restoredDeceleration=" + originalDeceleration.ToString("0.0", CultureInfo.InvariantCulture) +
                ", inputReleased=" + highSpeedCoastInputReleased +
                ", settled=" + highSpeedCoastSettled +
                ", groundCoverageEveryFrame=" + highSpeedCoastGroundCoverageMaintained +
                (highSpeedCoastGroundCoverageMaintained ? string.Empty : ", firstGroundFailure=" + firstHighSpeedCoastGroundFailure) +
                ", fullyLoadedVisibleViewEveryFrame=" + highSpeedCoastVisibleCoverageMaintained +
                (highSpeedCoastVisibleCoverageMaintained ? string.Empty : ", firstVisibleFailure=" + firstHighSpeedCoastVisibleFailure) +
                 ", maximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                 ", maximumFrameApplicationFocused=" + maximumHighSpeedCoastFrameFocused +
                 ", unfocusedFrames=" + highSpeedCoastUnfocusedFrames +
                 ", maximumMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                      ? maximumHighSpeedCoastMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                      : "unavailable") +
                 ", mainThreadTimingMetric=" + mainThreadTimingMetric +
                ", maximumMainThreadUnityFrame=" + maximumHighSpeedCoastMainThreadUnityFrame +
                ", maximumMainThreadWallFrameSeconds=" + maximumHighSpeedCoastMainThreadWallFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameMainThreadSeconds=" + (mainThreadTimeRecorder.Valid
                    ? worstHighSpeedCoastMainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "unavailable") +
                ", worstFrameBehaviourUpdateSeconds=" + worstCoastFrameBehaviourUpdateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFramePhysicsSimulateSeconds=" + worstCoastFramePhysicsSimulateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameGcCollectSeconds=" + worstCoastFrameGcCollectSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameStreamingWorkSeconds=" + worstHighSpeedCoastStreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameStreamingStage=" + worstHighSpeedCoastStreamingStage +
                ", worstFrameStreamingStageSeconds=" + worstHighSpeedCoastStreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ", worstFrameUnityFrame=" + worstHighSpeedCoastUnityFrame +
                ", worstFrameAllocatedBytes=" + worstHighSpeedCoastAllocatedBytes +
                ", worstFrameGcCollections=" + worstHighSpeedCoastGen0Collections + "/" + worstHighSpeedCoastGen1Collections + "/" + worstHighSpeedCoastGen2Collections +
                ", traversalBlocks=" + highSpeedCoastTraversalBlocks +
                ", synchronousGroundFallbackAttempts=" + highSpeedCoastFallbackAttempts);
            report.AppendLine("- speed260CoastPhysicalTravelGate: " + (coastPhysicalTravelPassed ? "PASS" :
                coastObstructedFrames > 0 ? "INVALID_ROUTE" : "FAIL_INSUFFICIENT_TRAVEL") +
                " expectedMeters=" + coastExpectedTravel.ToString("F3", CultureInfo.InvariantCulture) +
                " completedForwardMeters=" + coastForwardTravel.ToString("F3", CultureInfo.InvariantCulture) +
                " pathMeters=" + coastPathDistance.ToString("F3", CultureInfo.InvariantCulture) +
                " netMeters=" + highSpeedCoastDistance.ToString("F3", CultureInfo.InvariantCulture) +
                " collisionToleranceMeters=" + coastTravelTolerance.ToString("F3", CultureInfo.InvariantCulture) +
                " obstructedFrames=" + coastObstructedFrames + " firstObstruction=" +
                (string.IsNullOrEmpty(firstCoastObstruction) ? "<none>" : firstCoastObstruction));
            bool speed260FocusValid = highSpeedUnfocusedFrames == 0 && highSpeedCoastUnfocusedFrames == 0;
            // note: A bounded observer overflow cannot silently publish partial percentiles as a complete measurement window.
            bool speed260SampleCoverageValid = highSpeedWallFrameSampleCount == highSpeedFrames &&
                highSpeedCoastWallFrameSampleCount == highSpeedCoastFrames &&
                r2FrameTimingCount == highSpeedFrames + highSpeedCoastFrames;
            report.AppendLine("- speed260FrameSampleCoverageGate: " + (speed260SampleCoverageValid ? "PASS" : "INVALID_INCOMPLETE_CAPTURE") +
                " inputSamples=" + highSpeedWallFrameSampleCount + "/" + highSpeedFrames +
                " coastSamples=" + highSpeedCoastWallFrameSampleCount + "/" + highSpeedCoastFrames +
                " frameRecords=" + r2FrameTimingCount + "/" + (highSpeedFrames + highSpeedCoastFrames));
            report.AppendLine("- speed260CertificationValidity: " +
                (!highSpeedCoastBrakingConfigValid ? "INVALID_BRAKING_CONFIGURATION" :
                    !selectedLaneSampled || selectedLaneBlockingHitCount != 0 || inputObstructedFrames > 0 || coastObstructedFrames > 0
                    ? "INVALID_ROUTE" : !speed260SampleCoverageValid ? "INVALID_INCOMPLETE_CAPTURE" :
                    !speed260FocusValid ? "INVALID_FOCUS" : "VALID") +
                " routeVersion=5 inputUnfocusedFrames=" + highSpeedUnfocusedFrames +
                " coastUnfocusedFrames=" + highSpeedCoastUnfocusedFrames +
                " exitHeading=" + boostTravelDirection.ToString("F3"));
            report.AppendLine("- speed260WallFrameDistribution: inputSamples=" + highSpeedWallFrameSampleCount +
                " inputP50Seconds=" + CalculatePercentile(highSpeedWallFrameSamples, highSpeedWallFrameSampleCount, 0.50f).ToString("0.000", CultureInfo.InvariantCulture) +
                " inputP95Seconds=" + CalculatePercentile(highSpeedWallFrameSamples, highSpeedWallFrameSampleCount, 0.95f).ToString("0.000", CultureInfo.InvariantCulture) +
                " inputMaximumSeconds=" + maximumHighSpeedFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " coastSamples=" + highSpeedCoastWallFrameSampleCount +
                " coastP50Seconds=" + CalculatePercentile(highSpeedCoastWallFrameSamples, highSpeedCoastWallFrameSampleCount, 0.50f).ToString("0.000", CultureInfo.InvariantCulture) +
                " coastP95Seconds=" + CalculatePercentile(highSpeedCoastWallFrameSamples, highSpeedCoastWallFrameSampleCount, 0.95f).ToString("0.000", CultureInfo.InvariantCulture) +
                " coastMaximumSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            bool[] includeR2FrameTiming = new bool[r2FrameTimingCount];
            int retainedR2FrameTimingCount = 0;
            for (int i = 0; i < r2FrameTimingCount; i++)
            {
                if (r2FrameTimings[i].WallSeconds < YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds)
                    continue;
                includeR2FrameTiming[i] = true;
                if (i > 0 && r2FrameTimings[i - 1].Coast == r2FrameTimings[i].Coast)
                    includeR2FrameTiming[i - 1] = true;
                if (i + 1 < r2FrameTimingCount && r2FrameTimings[i + 1].Coast == r2FrameTimings[i].Coast)
                    includeR2FrameTiming[i + 1] = true;
            }
            for (int i = 0; i < includeR2FrameTiming.Length; i++)
            {
                if (includeR2FrameTiming[i])
                    retainedR2FrameTimingCount++;
            }
            report.AppendLine("- speed260FrameTimingAroundBudgetExceedances: budgetSeconds=" +
                YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " retained=" + retainedR2FrameTimingCount);
            for (int i = 0; i < r2FrameTimingCount; i++)
            {
                if (!includeR2FrameTiming[i])
                    continue;
                R2FrameTimingDiagnostic timing = r2FrameTimings[i];
                report.AppendLine("  frame=" + timing.UnityFrame +
                    " phase=" + (timing.Coast ? "coast" : "input") +
                    " wall=" + timing.WallSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " delta=" + timing.UnityDeltaSeconds.ToString("0.000000", CultureInfo.InvariantCulture) +
                    " mainThread=" + timing.MainThreadSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " behaviour=" + timing.BehaviourUpdateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " physics=" + timing.PhysicsSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " gc=" + timing.GcSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " streamerUpdate=" + timing.StreamerUpdateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " streamerLateUpdate=" + timing.StreamerLateUpdateSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " semaphoreWaitCurrentThread=" + timing.CurrentThreadSemaphoreWaitSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " streamingWork=" + timing.StreamingWorkSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " stage=" + timing.StreamingStage +
                    " stageSeconds=" + timing.StreamingStageSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " allocatedBytes=" + timing.AllocatedBytes +
                    " collections=" + timing.Gen0Collections + "/" + timing.Gen1Collections + "/" + timing.Gen2Collections);
            }
            StringBuilder namedProfilerMarkerAttribution = new StringBuilder();
            for (int i = 0; i < profilerMarkerFrameCaptures.Count; i++)
            {
                ProfilerMarkerFrameCapture capture = profilerMarkerFrameCaptures[i];
                if (namedProfilerMarkerAttribution.Length > 0)
                    namedProfilerMarkerAttribution.Append('|');
                namedProfilerMarkerAttribution.Append(capture.Category).Append('/').Append(capture.Name)
                    .Append(":unit=").Append(capture.UnitType)
                    .Append(":valid=").Append(capture.Recorder.Valid)
                    .Append(":samples=").Append(capture.Recorder.Count)
                    .Append(":lastNanoseconds=").Append(capture.Recorder.LastValue.ToString(CultureInfo.InvariantCulture))
                    .Append(":currentThreadValid=").Append(capture.CurrentThreadRecorder.Valid)
                    .Append(":currentThreadPeak=").Append(capture.CurrentThreadPeakSeconds.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":currentThreadWorstWall=").Append(capture.CurrentThreadWorstWallFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":currentThreadWorstCoast=").Append(capture.CurrentThreadWorstCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":speedMainPeak=").Append(capture.MainThreadPeakSeconds.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":worstWall=").Append(capture.WorstWallFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":worstCoast=").Append(capture.WorstCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":speedP50=").Append(capture.Percentile(true, 0.50f).ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":speedP95=").Append(capture.Percentile(true, 0.95f).ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":coastP50=").Append(capture.Percentile(false, 0.50f).ToString("0.000", CultureInfo.InvariantCulture))
                    .Append(":coastP95=").Append(capture.Percentile(false, 0.95f).ToString("0.000", CultureInfo.InvariantCulture));
            }
            report.AppendLine("- speed260ProfilerMarkerAttribution: captured=" + profilerMarkerFrameCaptures.Count +
                " streamerCallbackAndWaitMarkers=" + (namedProfilerMarkerAttribution.Length > 0 ? namedProfilerMarkerAttribution.ToString() : "none"));
            report.AppendLine("- speed260FallSafetyRecoveryGate: " +
                (fallSafetyRecoveryGatePassed ? "PASS" : "FAIL") +
                " recoveryOwnerPresent=" + (fallSafety != null) +
                " recoveriesDuringMovement=" + fallSafetyRecoveryDelta +
                " historicalDropsWithoutPenetration=" + historicalDropWithoutPenetrationDelta +
                (fallSafety != null && fallSafety.RecoveryCount > 0
                    ? " lastInvalidPosition=" + fallSafety.LastRecoveryPosition.ToString("F2", CultureInfo.InvariantCulture) +
                      " lastTerrainHeight=" + fallSafety.LastRecoveryTerrainHeight.ToString("0.00", CultureInfo.InvariantCulture) +
                      " lastPenetrationDepth=" + fallSafety.LastRecoveryPenetrationDepth.ToString("0.00", CultureInfo.InvariantCulture)
                    : string.Empty));
            AppendProductionMovementDiagnostics(report, motor, "speed260-final", highSpeedSideContactsBefore);
            // note: Capture all current physical and reservation-owned cells after frame measurements so census formatting cannot inflate the 260 m/s budget.
            report.AppendLine("- physicalOwnerCapacityCensus: " + streamer.CapturePhysicalOwnerCapacityCensus());
            report.AppendLine("- speed260ConstantInputSegmentsGate: " + (highSpeedConstantInputSegmentsObserved ? "PASS" : "FAIL"));
            report.AppendLine("- speed260ContentPreemptions: total=" +
                (streamer.ContentPreemptionCount - contentPreemptionsBeforeSpeed) +
                " predicted=" + (streamer.PredictedContentPreemptionCount - predictedContentPreemptionsBeforeSpeed));
            if (_focusedSpeed260MotorWitnessOnly)
                report.AppendLine("- appearanceBudgetDecisions: " + streamer.DescribeAppearanceBudgetObservation());
            // note: Report the focused per-frame terrain dispatch timeline beside the first-view-miss budget history.
            if (_focusedSpeed260MotorWitnessOnly)
                report.AppendLine("- terrainDispatchDecisions: " + streamer.DescribeTerrainDispatchObservation());
            // note: Count successful, canceled, and faulted executed samplers separately; a finally-block completion is not successful terrain service.
            report.AppendLine("- speed260TerrainPreparationThroughput: scope=stress-window counterSnapshot=independentAtomicReads elapsedSeconds=" + highSpeedWitnessElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " executedHeightTasks=" + backgroundHeightTasksDuringSpeed +
                " successfulHeightTasks=" + Math.Max(0L, streamer.BackgroundHeightTaskSucceededCount - successfulHeightTasksBeforeSpeed) +
                " canceledHeightTasks=" + Math.Max(0L, streamer.BackgroundHeightTaskCanceledCount - canceledHeightTasksBeforeSpeed) +
                " faultedHeightTasks=" + Math.Max(0L, streamer.BackgroundHeightTaskFaultedCount - faultedHeightTasksBeforeSpeed) +
                " executedHeightTaskRatePerSecond=" + (backgroundHeightTasksDuringSpeed / highSpeedWitnessElapsedSeconds).ToString("0.0", CultureInfo.InvariantCulture) +
                " executedHeightTaskAverageSeconds=" + (backgroundHeightTasksDuringSpeed > 0
                    ? backgroundHeightTaskSecondsDuringSpeed / backgroundHeightTasksDuringSpeed
                    : 0d).ToString("0.000", CultureInfo.InvariantCulture) +
                " heightTaskLifetimeHighWaterSeconds=" + streamer.BackgroundHeightTaskMaximumSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " handoffs=" + terrainHandoffsDuringSpeed +
                " handoffsPerSecond=" + (terrainHandoffsDuringSpeed / highSpeedWitnessElapsedSeconds).ToString("0.0", CultureInfo.InvariantCulture) +
                " handoffAverageSeconds=" + (terrainHandoffsDuringSpeed > 0
                    ? terrainHandoffSecondsDuringSpeed / terrainHandoffsDuringSpeed
                    : 0f).ToString("0.000", CultureInfo.InvariantCulture) +
                " handoffLifetimeHighWaterSeconds=" + streamer.TerrainPreparationHandoffMaximumSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            // note: These native TerrainData high-water marks are process-wide, not limited to the timed stress window.
            report.AppendLine("- streamedTerrainMutationLifetimeHighWaters: biomeUploadSeconds=" +
                YQGeneratedWorldEnvironment.MaximumStreamedBiomeUploadSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " detailResolutionSeconds=" + YQGeneratedWorldEnvironment.MaximumStreamedDetailResolutionSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " detailNormalSeconds=" + YQGeneratedWorldEnvironment.MaximumStreamedDetailNormalSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " detailComputeSeconds=" + YQGeneratedWorldEnvironment.MaximumStreamedDetailComputeSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " detailLayerUploadSeconds=" + YQGeneratedWorldEnvironment.MaximumStreamedDetailUploadSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            report.AppendLine("- speed260SemanticSaveFrameAttribution: inputSaveFrames=" + highSpeedSaveFrameCount +
                " inputMaxSaveFrameSeconds=" + maximumHighSpeedSaveFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " inputMaxOtherFrameSeconds=" + maximumHighSpeedNoSaveFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " coastSaveFrames=" + highSpeedCoastSaveFrameCount +
                " coastMaxSaveFrameSeconds=" + maximumHighSpeedCoastSaveFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " coastMaxOtherFrameSeconds=" + maximumHighSpeedCoastNoSaveFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " lastSaveSeconds=" + streamer.LastSemanticSaveSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WorldStateManager measuredWorldSaveOwner = WorldStateManager.Instance;
            if (measuredWorldSaveOwner != null)
            report.AppendLine("- worldSaveLifetimeStageHighWaters: normalizeSeconds=" +
                    measuredWorldSaveOwner.MaximumSaveNormalizeSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " serializeSeconds=" + measuredWorldSaveOwner.MaximumSaveSerializeSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                    " atomicWriteSeconds=" + measuredWorldSaveOwner.MaximumSaveWriteSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            // note: Format bounded turn samples only after the input and coast timers end; collection adds no per-frame strings or IO.
            for (int directionIndex = 0; directionIndex < highSpeedDirectionSamples.Length; directionIndex++)
            {
                Speed260DirectionSample sample = highSpeedDirectionSamples[directionIndex];
                if (!sample.observed) continue;
                report.AppendLine("- speed260DirectionSample: index=" + directionIndex + " frame=" + sample.frame +
                    " maskBit=" + sample.maskBit + " expectedInput=" + sample.expectedInput.ToString("F3") +
                    " consumedInput=" + sample.consumedInput.ToString("F3") + " speed=" + sample.speed.ToString("F3", CultureInfo.InvariantCulture) +
                    " dot=" + sample.directionDot.ToString("F3", CultureInfo.InvariantCulture) +
                    " unitySeconds=" + sample.unitySeconds.ToString("F6", CultureInfo.InvariantCulture) +
                    " motorSeconds=" + sample.motorSeconds.ToString("F6", CultureInfo.InvariantCulture) +
                    " start=" + sample.start.ToString("F3") + " end=" + sample.end.ToString("F3") +
                    " requestedMove=" + sample.requestedMove.ToString("F3") + " actualMove=" + sample.actualMove.ToString("F3") +
                    " sideContacts=" + sample.sideContacts + " readinessRejected=" + sample.readinessRejected +
                    " collisionFlags=" + sample.collisionFlags + " contactId=" + sample.contactId +
                    " contact=" + (sample.sideContacts == 0 ? "<none this frame>" : sample.contact != null
                        ? DescribeColliderHierarchy(sample.contact.transform) : "<destroyed; see contactId>") +
                    " normal=" + sample.contactNormal.ToString("F3"));
            }
            report.AppendLine("- speed260InputPhysicalTravelGate: " + (inputObstructedFrames == 0 ? "PASS" : "INVALID_ROUTE") +
                " obstructedFrames=" + inputObstructedFrames + " firstFrame=" + firstInputObstructionFrame +
                " contactId=" + firstInputObstructionContactId + " position=" + firstInputObstructionPosition.ToString("F3") +
                " normal=" + firstInputObstructionNormal.ToString("F3") +
                " expectedMeters=" + firstInputObstructionExpected.ToString("F3", CultureInfo.InvariantCulture) +
                " completedMeters=" + firstInputObstructionCompleted.ToString("F3", CultureInfo.InvariantCulture));
            report.AppendLine("- speed260DirectionChangeGate: " +
                (highSpeedFrames > 0 && highSpeedTransitionInputMask == 0b111111 && highSpeedTransitionDisplacementMask == 0b111111
                    ? "PASS"
                    : "FAIL inputMask=" + highSpeedTransitionInputMask + " displacementMask=" + highSpeedTransitionDisplacementMask));
            report.AppendLine("- speed260VisibleViewGate: " +
                (highSpeedFrames > 0 && visibleViewCoverageMaintained
                    ? "PASS"
                    : "FAIL frames=" + highSpeedFrames + " " + firstVisibleViewFailure));
            report.AppendLine("- speed260TurnBufferGate: " +
                (highSpeedFrames > 0 && highSpeedTurnBufferMaintained
                    ? "PASS"
                    : "FAIL frames=" + highSpeedFrames + " " + firstTurnBufferFailure));
            report.AppendLine("- speed260CoastCoverageGate: " +
                (highSpeedCoastObserved
                    ? "PASS"
                    : "FAIL startSpeed=" + highSpeedCoastStartSpeed.ToString("0.0", CultureInfo.InvariantCulture) +
                      " frames=" + highSpeedCoastFrames +
                      " elapsedSeconds=" + highSpeedCoastElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                      " travelDistance=" + highSpeedCoastDistance.ToString("0.0", CultureInfo.InvariantCulture) +
                      " inputReleased=" + highSpeedCoastInputReleased +
                      " settled=" + highSpeedCoastSettled +
                      " ground=" + highSpeedCoastGroundCoverageMaintained +
                      " visible=" + highSpeedCoastVisibleCoverageMaintained +
                      " traversalBlocks=" + highSpeedCoastTraversalBlocks +
                      " synchronousFallbackAttempts=" + highSpeedCoastFallbackAttempts));
            report.AppendLine("- speed260CoastFrameBudgetGate: " +
                (highSpeedCoastFrameBudgetPassed
                    ? "PASS maximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture)
                    : "FAIL frames=" + highSpeedCoastFrames +
                      " maximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                      " budgetSeconds=" + YQPlayerFollowingSemanticChunkStreamer.StreamingFrameBudgetSeconds.ToString("0.000", CultureInfo.InvariantCulture)));
            report.AppendLine("- speed260FrameBudgetGate: " +
                (highSpeedFrameBudgetPassed
                    ? "PASS"
                    : "FAIL inputFrames=" + highSpeedFrames +
                      " inputMaximumFrameSeconds=" + maximumHighSpeedFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                      " coastFrames=" + highSpeedCoastFrames +
                      " coastMaximumFrameSeconds=" + maximumHighSpeedCoastFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture)));

            report.AppendLine("- productionMotorInputFrames: walk=" + walkFrames + ",run=" + sprintFrames + ",dash=" + dashFrames +
                ",reversal=" + reversalFrames + ",diagonal=" + diagonalFrames + ",rapidTurns=" + rapidTurnFrames +
                ",speed260=" + highSpeedFrames);
            // note: Persist the complete production motor matrix before returning control to the verifier.
            WriteReport(report, false, false);
            result(walkObserved && sprintObserved && dashObserved && reversalObserved && diagonalObserved && rapidTurnsObserved &&
                highSpeedObserved && highSpeedFrameBudgetPassed && fallSafetyRecoveryGatePassed &&
                speed260FocusValid && speed260SampleCoverageValid, false);
        }
        finally
        {
#if UNITY_EDITOR
            frameTrace?.Dispose();
#endif
            // note: Restore Unity's prior background behavior even when a nested movement phase fails or Play Mode is stopped.
            if (_focusedSpeed260MotorWitnessOnly && streamer != null)
            {
                // note: Flush the complete fixed-target history after the measured window, including failure and later recovery; never stop it at another cell's first miss.
                string fixedTrace = streamer.FinishFixedTerrainObservation();
                // note: Flush the bounded demanded-cell history after measurement, preserving the first failure prefix and later useful completions.
                string completeCellTrace = streamer.FinishCompleteCellObservation();
                if (completeCellTrace != "not-recorded")
                {
                    string completeCellPath = Path.GetFullPath(Path.Combine("Logs", "G08_R2_CompleteCells_" +
                        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".tsv"));
                    try
                    {
                        File.WriteAllText(completeCellPath, completeCellTrace);
                        report.AppendLine("- completeCellHistoryTrace: " + completeCellPath);
                    }
                    catch (IOException exception)
                    {
                        report.AppendLine("- completeCellHistoryTrace: NOT_WRITTEN " + exception.Message);
                        Debug.LogError("[YQR2CompleteCellTrace] " + exception.Message);
                    }
                }
                string fixedTracePath = Path.GetFullPath(Path.Combine("Logs", "G08_R2_FixedTarget_" +
                    DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + ".tsv"));
                try
                {
                    File.WriteAllText(fixedTracePath, fixedTrace);
                    report.AppendLine("- fixedTargetTerrainTrace: " + fixedTracePath);
                }
                catch (IOException exception)
                {
                    report.AppendLine("- fixedTargetTerrainTrace: NOT_WRITTEN " + exception.Message);
                    Debug.LogError("[YQR2FixedTargetTrace] " + exception.Message);
                }
                streamer.EndAppearanceBudgetObservation();
            }
            if (restoreRunInBackground)
                Application.runInBackground = originalRunInBackground;
            // note: Runtime stress values are probe-only and never become a serialized player-speed change.
            if (motor != null)
            {
                motor.walkSpeed = originalWalkSpeed;
                motor.sprintSpeed = originalSprintSpeed;
                motor.acceleration = originalAcceleration;
                motor.deceleration = originalDeceleration;
            }
            // note: Release the keys without removing the user's physical device or mutating global Input System ownership.
            if (keyboard != null)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }
            if (mainThreadTimeRecorder.Valid)
                mainThreadTimeRecorder.Dispose();
            if (behaviourUpdateRecorder.Valid)
                behaviourUpdateRecorder.Dispose();
            if (physicsSimulateRecorder.Valid)
                physicsSimulateRecorder.Dispose();
            if (gcCollectRecorder.Valid)
                gcCollectRecorder.Dispose();
            for (int i = 0; i < profilerMarkerFrameCaptures.Count; i++)
            {
                if (profilerMarkerFrameCaptures[i].Recorder.Valid)
                    profilerMarkerFrameCaptures[i].Recorder.Dispose();
                if (profilerMarkerFrameCaptures[i].CurrentThreadRecorder.Valid)
                    profilerMarkerFrameCaptures[i].CurrentThreadRecorder.Dispose();
            }
        }
    }

    private static IEnumerator VerifyPublicationRecoveryMatrix(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor,
        Transform player,
        CharacterController characterController,
        StringBuilder report,
        Action<bool, bool, bool, bool> result)
    {
        UnityEngine.InputSystem.Keyboard keyboard = null;
        HashSet<Vector2Int> usedCoordinates = new HashSet<Vector2Int>();
        bool allPassed = true;
        bool recoveryInputDeliveryInconclusive = false;
        bool unloadRevisitPassed = false;
        bool unloadRevisitInputInconclusive = false;
        try
        {
            // note: Reuse the existing keyboard for every recovery case, creating one only when the editor exposes no keyboard device.
            keyboard = AcquireVerificationKeyboard();
            if (keyboard == null)
                throw new InvalidOperationException("publication recovery verification could not acquire a keyboard device");
            keyboard.MakeCurrent();

            Vector2Int cancellationCoordinate = default;
            bool recoveryWitnessReady = false;
            double recoveryWitnessStarted = Time.realtimeSinceStartupAsDouble;
            double recoveryWitnessDeadline = recoveryWitnessStarted + RecoveryWitnessTimeoutSeconds;
            while (Time.realtimeSinceStartupAsDouble < recoveryWitnessDeadline)
            {
                if (TryFindRecoveryCoordinate(streamer, usedCoordinates, out cancellationCoordinate))
                {
                    recoveryWitnessReady = true;
                    break;
                }
                // note: Wait only for a real retained production owner to become available; this does not settle the initial frontier or certify any movement phase.
                yield return null;
            }
            if (!recoveryWitnessReady)
                throw new InvalidOperationException("no streamed recovery coordinate was available");
            report.AppendLine("- recoveryWitnessReady: PASS elapsedSeconds=" +
                (Time.realtimeSinceStartupAsDouble - recoveryWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                " coordinate=" + cancellationCoordinate);
            WriteReport(report, false, false);
            YQSemanticChunkPublicationSnapshot cancellationBefore;
            if (!streamer.TryGetPublicationSnapshot(cancellationCoordinate, out cancellationBefore))
                throw new InvalidOperationException("cancellation witness disappeared before replacement");
            int firstReplacementVersion;
            int secondReplacementVersion;
            bool replacementStarted = streamer.TryReplacePublicationOwnerForVerification(
                cancellationCoordinate,
                out firstReplacementVersion,
                out secondReplacementVersion);
            if (replacementStarted)
                usedCoordinates.Add(cancellationCoordinate);
            bool workObserved = false;
            YQSemanticChunkPublicationSnapshot cancellationSnapshot = default;
            yield return WaitForPublicationSnapshot(
                streamer,
                cancellationCoordinate,
                snapshot =>
                    (snapshot.terrainWorkActive && snapshot.terrainWorkId != 0) ||
                    (snapshot.contentWorkActive && snapshot.contentWorkId != 0) ||
                    (snapshot.appearanceWorkActive && snapshot.appearanceWorkVersion != 0),
                30f,
                (matched, snapshot) =>
                {
                    workObserved = matched;
                    cancellationSnapshot = snapshot;
                });
            int retiredVersion = cancellationBefore.publicationVersion;
            int retiredOwnerEpoch = cancellationBefore.ownerEpoch;
            int replacementAfterWorkVersion = 0;
            int replacementPublishedVersion = 0;
            bool replacementAfterWork = false;
            // note: A terrain handoff may temporarily own the coordinate; retry the same production replacement API until that handoff releases or the bounded real-time window expires.
            double replacementDeadline = Time.realtimeSinceStartupAsDouble + PublicationRecoveryTimeoutSeconds;
            while (workObserved && !replacementAfterWork && Time.realtimeSinceStartupAsDouble < replacementDeadline)
            {
                replacementAfterWork = streamer.TryReplacePublicationOwnerForVerification(
                    cancellationCoordinate,
                    out replacementAfterWorkVersion,
                    out replacementPublishedVersion);
                if (!replacementAfterWork)
                    yield return null;
            }
            bool replacementCompleted = false;
            YQSemanticChunkPublicationSnapshot replacementFinalSnapshot = default;
            yield return WaitForPublicationSnapshot(
                streamer,
                cancellationCoordinate,
                // note: Replacement publication owns the exact version returned by the streamer; later versions are also valid after a rebuild.
                snapshot => snapshot.traversable && snapshot.publicationVersion >= replacementPublishedVersion,
                 PublicationRecoveryTimeoutSeconds,
                (matched, snapshot) =>
                {
                    replacementCompleted = matched;
                    replacementFinalSnapshot = snapshot;
                });
            // note: The second output is the newly published owner version; compare that version with the retired work.
            bool cancellationPassed = replacementStarted && replacementAfterWork &&
                replacementPublishedVersion > retiredVersion && replacementCompleted &&
                streamer.TryGetPublicationSnapshot(cancellationCoordinate, out YQSemanticChunkPublicationSnapshot cancellationAfter) &&
                cancellationAfter.ownerEpoch != retiredOwnerEpoch;
            report.AppendLine("- cancellationReplacementGate: " + (cancellationPassed ? "PASS" : "FAIL") +
                " initialVersion=" + firstReplacementVersion +
                " replacementVersion=" + replacementPublishedVersion +
                " workObserved=" + workObserved +
                " retiredVersion=" + retiredVersion +
                " retiredOwnerEpoch=" + retiredOwnerEpoch +
                 " replacementAfterWorkVersion=" + replacementAfterWorkVersion +
                 " replacementStarted=" + replacementStarted +
                 " replacementAfterWork=" + replacementAfterWork +
                 " replacementCompleted=" + replacementCompleted +
                 " finalVersion=" + replacementFinalSnapshot.publicationVersion +
                 " finalState=" + replacementFinalSnapshot.lifecycle +
                 " finalTerrain=" + replacementFinalSnapshot.terrainReadiness +
                 " finalContent=" + replacementFinalSnapshot.requiredContentReady +
                 " finalOverlay=" + replacementFinalSnapshot.overlayReady +
                 " finalAppearance=" + replacementFinalSnapshot.appearanceReady +
                 " finalEcology=" + replacementFinalSnapshot.requiredEcologyReady +
                 " finalActivation=" + replacementFinalSnapshot.activationComplete +
                 " finalTerrainWork=" + replacementFinalSnapshot.terrainWorkActive +
                 " finalContentWork=" + replacementFinalSnapshot.contentWorkActive +
                 " finalContentQueued=" + replacementFinalSnapshot.contentWorkQueued +
                 " finalAppearanceWork=" + replacementFinalSnapshot.appearanceWorkActive +
                 " finalEcologyWork=" + replacementFinalSnapshot.ecologyWorkActive +
                 " finalLifecycleWork=" + replacementFinalSnapshot.lifecycleWorkPending +
                 " finalFailure=" + replacementFinalSnapshot.failureReason +
                 " replacementOwnerEpochChanged=" + (replacementStarted && streamer.TryGetPublicationSnapshot(cancellationCoordinate, out YQSemanticChunkPublicationSnapshot replacementSnapshot) && replacementSnapshot.ownerEpoch != retiredOwnerEpoch));
            WriteReport(report, false, false);
            allPassed &= cancellationPassed;
            streamer.EndPublicationVerificationScenario(cancellationCoordinate);
            // note: The completed replacement is again a safe published owner; reuse it for appearance recovery instead of requiring a second off-camera owner.
            usedCoordinates.Remove(cancellationCoordinate);

            Vector2Int retryCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            bool retryOwnerPrepared = false;
            double retryOwnerStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return PrepareAppearanceRetryOwner(
                streamer,
                usedCoordinates,
                retryOwnerStartedAt + RecoveryWitnessTimeoutSeconds,
                (prepared, coordinate) =>
                {
                    retryOwnerPrepared = prepared;
                    retryCoordinate = coordinate;
                });
            if (!retryOwnerPrepared)
                throw new InvalidOperationException("no retry recovery coordinate became available after " +
                    (Time.realtimeSinceStartupAsDouble - retryOwnerStartedAt).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            bool retryPrepared = streamer.BeginPublicationVerificationScenario(
                    retryCoordinate,
                    false,
                    false,
                    false,
                    YQPlayerFollowingSemanticChunkStreamer.MaximumTerrainAppearanceRetries + 1,
                    0);
            bool retryExhausted = false;
            YQSemanticChunkPublicationSnapshot retrySnapshot = default;
            double retryStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return WaitForPublicationSnapshot(
                streamer,
                retryCoordinate,
                snapshot => snapshot.hasExplicitFailure &&
                    snapshot.failureReason.IndexOf("appearance", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    snapshot.failureReason.IndexOf("InvalidOperationException", StringComparison.Ordinal) >= 0 &&
                    snapshot.appearanceRetryCount == YQPlayerFollowingSemanticChunkStreamer.MaximumTerrainAppearanceRetries &&
                    !snapshot.appearanceWorkActive && !snapshot.appearanceRetryScheduled,
                 AppearanceRetryExhaustionTimeoutSeconds,
                (matched, snapshot) =>
                {
                    retryExhausted = matched;
                    retrySnapshot = snapshot;
                });
            streamer.EndPublicationVerificationScenario(retryCoordinate);
            // note: Rebuild the same off-camera owner after the intentional retry exhaustion so later fault probes start from normal traversable state.
            int retryOwnerRetiredVersion = 0;
            int retryOwnerReplacementVersion = 0;
            bool retryOwnerRecoveryStarted = retryPrepared && streamer.TryReplacePublicationOwnerForVerification(
                retryCoordinate,
                out retryOwnerRetiredVersion,
                out retryOwnerReplacementVersion);
            bool retryOwnerRecoveryCompleted = false;
            YQSemanticChunkPublicationSnapshot retryOwnerRecoverySnapshot = default;
            if (retryOwnerRecoveryStarted)
            {
                yield return WaitForPublicationSnapshot(
                    streamer,
                    retryCoordinate,
                    snapshot => snapshot.traversable && snapshot.publicationVersion >= retryOwnerReplacementVersion,
                    PublicationRecoveryTimeoutSeconds,
                    (matched, snapshot) =>
                    {
                        retryOwnerRecoveryCompleted = matched;
                        retryOwnerRecoverySnapshot = snapshot;
                    });
            }
            bool retryOwnerRecovered = retryOwnerRecoveryStarted && retryOwnerRecoveryCompleted;
            streamer.EndPublicationVerificationScenario(retryCoordinate);
            report.AppendLine("- retryExhaustionGate: " + (retryPrepared && retryExhausted ? "PASS" : "FAIL") +
                " prepared=" + retryPrepared +
                " matched=" + retryExhausted +
                " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - retryStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                " timeoutSeconds=" + AppearanceRetryExhaustionTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " coordinate=" + retryCoordinate +
                " version=" + retrySnapshot.publicationVersion +
                " retryCount=" + retrySnapshot.appearanceRetryCount +
                " appearanceWork=" + retrySnapshot.appearanceWorkActive +
                " appearanceRetryScheduled=" + retrySnapshot.appearanceRetryScheduled +
                " explicitFailure=" + retrySnapshot.hasExplicitFailure +
                " failureReason=" + retrySnapshot.failureReason);
            report.AppendLine("- retryOwnerRecoveryGate: " + (retryOwnerRecovered ? "PASS" : "FAIL") +
                " started=" + retryOwnerRecoveryStarted +
                " completed=" + retryOwnerRecoveryCompleted +
                " retiredVersion=" + (retryOwnerRecoveryStarted ? retryOwnerRetiredVersion : 0) +
                " replacementVersion=" + (retryOwnerRecoveryStarted ? retryOwnerReplacementVersion : 0) +
                " finalVersion=" + retryOwnerRecoverySnapshot.publicationVersion +
                " traversable=" + retryOwnerRecoverySnapshot.traversable +
                " appearanceReady=" + retryOwnerRecoverySnapshot.appearanceReady +
                " failureReason=" + retryOwnerRecoverySnapshot.failureReason);
            WriteReport(report, false, false);
            allPassed &= retryPrepared && retryExhausted && retryOwnerRecovered;
            if (retryOwnerRecovered)
                usedCoordinates.Remove(retryCoordinate);
            streamer.EndPublicationVerificationScenario(retryCoordinate);

            Vector2Int lagCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            bool lagOwnerPrepared = false;
            double lagOwnerStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return PrepareAppearanceRetryOwner(
                streamer,
                usedCoordinates,
                lagOwnerStartedAt + RecoveryWitnessTimeoutSeconds,
                (prepared, coordinate) =>
                {
                    lagOwnerPrepared = prepared;
                    lagCoordinate = coordinate;
                });
            if (!lagOwnerPrepared)
                throw new InvalidOperationException("no lag recovery coordinate became available after " +
                    (Time.realtimeSinceStartupAsDouble - lagOwnerStartedAt).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            bool lagPrepared = streamer.BeginPublicationVerificationScenario(
                    lagCoordinate,
                    false,
                    true,
                    false,
                    0,
                    0);
            bool lagHeld = false;
            YQSemanticChunkPublicationSnapshot lagHoldSnapshot = default;
            double lagHoldStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return WaitForPublicationSnapshot(
                streamer,
                lagCoordinate,
                snapshot => snapshot.appearanceWorkActive,
                AppearanceLagRecoveryTimeoutSeconds,
                (matched, snapshot) =>
                {
                    lagHeld = matched;
                    lagHoldSnapshot = snapshot;
                });
            double lagHoldElapsedSeconds = Time.realtimeSinceStartupAsDouble - lagHoldStartedAt;
            bool motorStayedValidDuringLag = true;
            bool lagInputObserved = false;
            bool lagMoved = false;
            yield return DriveProductionInput(
                keyboard,
                streamer,
                motor,
                player,
                characterController,
                report,
                0.75f,
                UnityEngine.InputSystem.Key.W,
                value => motorStayedValidDuringLag &= value,
                (inputObserved, moved, valid) =>
                {
                    lagInputObserved |= inputObserved;
                    lagMoved |= moved;
                });
            streamer.ReleasePublicationVerificationStage(lagCoordinate, YQPublicationVerificationStage.Appearance);
            bool lagReleased = false;
            YQSemanticChunkPublicationSnapshot lagReleaseSnapshot = default;
            double lagReleaseStartedAt = Time.realtimeSinceStartupAsDouble;
            yield return WaitForPublicationSnapshot(
                streamer,
                lagCoordinate,
                snapshot => snapshot.appearanceReady,
                AppearanceLagRecoveryTimeoutSeconds,
                (matched, snapshot) =>
                {
                    lagReleased = matched;
                    lagReleaseSnapshot = snapshot;
                });
            streamer.EndPublicationVerificationScenario(lagCoordinate);
            bool lagPassed = lagPrepared && lagHeld && motorStayedValidDuringLag && lagReleased;
            bool lagInconclusive = !lagInputObserved && lagPassed;
            report.AppendLine("- lagInjectionGate: " + (lagInconclusive ? "INCONCLUSIVE" : lagPassed ? "PASS" : "FAIL") +
                " prepared=" + lagPrepared +
                " held=" + lagHeld +
                " motorValid=" + motorStayedValidDuringLag +
                " released=" + lagReleased +
                " holdElapsedSeconds=" + lagHoldElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " holdTimeoutSeconds=" + AppearanceLagRecoveryTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " releaseElapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - lagReleaseStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                " holdPublicationVersion=" + lagHoldSnapshot.publicationVersion +
                " holdAppearanceWork=" + lagHoldSnapshot.appearanceWorkActive +
                " holdFailure=" + lagHoldSnapshot.failureReason +
                " releasePublicationVersion=" + lagReleaseSnapshot.publicationVersion +
                " releaseAppearanceReady=" + lagReleaseSnapshot.appearanceReady +
                " releaseFailure=" + lagReleaseSnapshot.failureReason +
                " inputObserved=" + lagInputObserved +
                " moved=" + lagMoved +
                " coordinate=" + lagCoordinate);
            WriteReport(report, false, false);
            allPassed &= lagInconclusive || lagPassed;
            recoveryInputDeliveryInconclusive |= lagInconclusive;

            Vector2Int groundCoordinate = new Vector2Int(int.MinValue, int.MinValue);
            // note: Create ordinary replacement work explicitly; a fully drained terrain queue is a valid production state and must not make this fault probe disappear.
            bool groundWitnessPrepared = TryReplaceRecoveryOwner(streamer, usedCoordinates,
                out groundCoordinate, out int groundRetiredVersion, out int groundReplacementVersion);
            report.AppendLine("- missingGroundWitness: " + (groundWitnessPrepared ? "PASS" : "FAIL") +
                " coordinate=" + groundCoordinate + " retiredVersion=" + groundRetiredVersion +
                " replacementVersion=" + groundReplacementVersion);
            WriteReport(report, false, false);
            bool groundPrepared = false;
            long groundWorkIdAtStart = 0;
            int groundOwnerEpoch = 0;
            string groundPhaseAtStart = string.Empty;
            double groundBarrierDeadline = Time.realtimeSinceStartupAsDouble + PublicationRecoveryTimeoutSeconds;
            while (groundWitnessPrepared && !groundPrepared && Time.realtimeSinceStartupAsDouble < groundBarrierDeadline)
            {
                // note: Attach only when this replacement reaches its real collider boundary through the unchanged production queue.
                if (streamer.TryGetPublicationSnapshot(groundCoordinate, out YQSemanticChunkPublicationSnapshot candidate) &&
                    candidate.terrainWorkId != 0 && string.Equals(candidate.terrainWorkPhase, "colliderSync", StringComparison.Ordinal))
                {
                    groundPrepared = streamer.TryBeginActiveTerrainCollisionVerificationScenario(
                        out Vector2Int heldCoordinate, out groundWorkIdAtStart, out groundPhaseAtStart);
                    if (groundPrepared && (heldCoordinate != groundCoordinate || groundWorkIdAtStart != candidate.terrainWorkId))
                    {
                        streamer.EndPublicationVerificationScenario(heldCoordinate);
                        groundPrepared = false;
                    }
                    if (groundPrepared)
                        groundOwnerEpoch = candidate.ownerEpoch;
                }
                if (!groundPrepared)
                    yield return null;
            }
            report.AppendLine("- missingGroundBarrier: " + (groundPrepared ? "PASS" : "FAIL") +
                " coordinate=" + groundCoordinate + " workId=" + groundWorkIdAtStart + " ownerEpoch=" + groundOwnerEpoch);
            WriteReport(report, false, false);
            bool groundHeld = false;
            long groundWorkIdAtHold = 0;
            bool groundQueuedAtHold = false;
            string groundPhaseAtHold = string.Empty;
            float groundElapsedAtHold = 0f;
            if (groundPrepared)
            {
                // note: Require the same active owner to prove that the hold is at the real collision publication boundary.
                yield return WaitForPublicationSnapshot(
                    streamer,
                    groundCoordinate,
                    snapshot => snapshot.terrainCollisionPublicationHeld && snapshot.terrainWorkId == groundWorkIdAtStart &&
                        snapshot.ownerEpoch == groundOwnerEpoch,
                    ActiveTerrainBarrierObservationTimeoutSeconds,
                    (matched, snapshot) =>
                    {
                        groundHeld = matched;
                        groundWorkIdAtHold = snapshot.terrainWorkId;
                        groundQueuedAtHold = snapshot.terrainWorkQueued;
                        groundPhaseAtHold = snapshot.terrainWorkPhase ?? string.Empty;
                        groundElapsedAtHold = snapshot.terrainWorkElapsedSeconds;
                    });
            }
            report.AppendLine("- missingGroundHeld: " + (groundHeld ? "PASS" : "FAIL") + " workId=" + groundWorkIdAtHold);
            WriteReport(report, false, false);
            bool motorStayedValidBeforeGroundRelease = true;
            bool groundInputObserved = false;
            bool groundMoved = false;
            bool groundViewCoverageDuringMovement = true;
            if (groundHeld)
            {
                // note: Verify live ground coverage while ordinary movement may preempt and replace the held terrain worker.
                yield return DriveProductionInput(
                    keyboard,
                    streamer,
                    motor,
                    player,
                    characterController,
                    report,
                    0.75f,
                    UnityEngine.InputSystem.Key.S,
                    value => motorStayedValidBeforeGroundRelease &= value,
                    (inputObserved, moved, valid) =>
                    {
                        groundInputObserved |= inputObserved;
                        groundMoved |= moved;
                    },
                    frameSafety: () =>
                    {
                        bool covered = streamer.TryValidateGroundViewCoverage(out _);
                        groundViewCoverageDuringMovement &= covered;
                        return covered;
                    });
            }
            YQSemanticChunkPublicationSnapshot heldGroundSnapshot = default;
            bool groundStillUnpublished = groundHeld && streamer.TryGetPublicationSnapshot(groundCoordinate, out heldGroundSnapshot) &&
                // note: Input can legitimately preempt terrain work; only the same held owner proves the missing-ground observation remained active.
                heldGroundSnapshot.terrainCollisionPublicationHeld && heldGroundSnapshot.terrainWorkId == groundWorkIdAtStart &&
                heldGroundSnapshot.ownerEpoch == groundOwnerEpoch &&
                heldGroundSnapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady &&
                !heldGroundSnapshot.traversable;
            if (groundPrepared)
                streamer.ReleasePublicationVerificationStage(groundCoordinate, YQPublicationVerificationStage.TerrainCollision);
            bool groundReleased = false;
            YQSemanticChunkPublicationSnapshot groundReleaseSnapshot = default;
            if (groundPrepared)
            {
                yield return WaitForPublicationSnapshot(
                    streamer,
                    groundCoordinate,
                    snapshot => snapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady &&
                        snapshot.ownerEpoch >= groundOwnerEpoch,
                    ActiveTerrainBarrierObservationTimeoutSeconds,
                    (matched, snapshot) =>
                    {
                        groundReleased = matched;
                        groundReleaseSnapshot = snapshot;
                    });
                streamer.EndPublicationVerificationScenario(groundCoordinate);
                usedCoordinates.Add(groundCoordinate);
            }
            report.AppendLine("- missingGroundReleased: " + (groundReleased ? "PASS" : "FAIL") + " ownerEpoch=" + groundReleaseSnapshot.ownerEpoch);
            WriteReport(report, false, false);
            bool groundPassed = groundPrepared && groundHeld && (groundStillUnpublished || groundViewCoverageDuringMovement) &&
                motorStayedValidBeforeGroundRelease && groundReleased;
            bool groundInconclusive = !groundInputObserved && groundPassed;
            report.AppendLine("- missingGroundGate: " + (groundInconclusive ? "INCONCLUSIVE" : groundPassed ? "PASS" : "FAIL") +
                " prepared=" + groundPrepared +
                " held=" + groundHeld +
                " stillUnpublished=" + groundStillUnpublished +
                " groundViewCoverageEveryFrame=" + groundViewCoverageDuringMovement +
                " motorValid=" + motorStayedValidBeforeGroundRelease +
                " released=" + groundReleased +
                " holdWorkId=" + groundWorkIdAtHold +
                " holdPhase=" + groundPhaseAtHold +
                " queuedAtHold=" + groundQueuedAtHold +
                " releaseWorkId=" + groundReleaseSnapshot.terrainWorkId +
                " releasePhase=" + (groundReleaseSnapshot.terrainWorkPhase ?? string.Empty) +
                " releaseElapsedSeconds=" + groundReleaseSnapshot.terrainWorkElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " workActive=" + groundReleaseSnapshot.terrainWorkActive +
                " workQueued=" + groundReleaseSnapshot.terrainWorkQueued +
                " retryScheduled=" + groundReleaseSnapshot.terrainRetryScheduled +
                " retryCount=" + groundReleaseSnapshot.terrainRetryCount +
                " explicitFailure=" + groundReleaseSnapshot.hasExplicitFailure +
                " failureReason=" + (groundReleaseSnapshot.failureReason ?? string.Empty) +
                " startWorkId=" + groundWorkIdAtStart +
                " startPhase=" + groundPhaseAtStart +
                " holdElapsedSeconds=" + groundElapsedAtHold.ToString("0.000", CultureInfo.InvariantCulture) +
                " inputObserved=" + groundInputObserved +
                " moved=" + groundMoved +
                " coordinate=" + groundCoordinate);
            WriteReport(report, false, false);
            allPassed &= groundInconclusive || groundPassed;
            recoveryInputDeliveryInconclusive |= groundInconclusive;

            Vector2Int orderAppearanceFirst = new Vector2Int(int.MinValue, int.MinValue);
            Vector2Int orderEcologyFirst = new Vector2Int(int.MinValue, int.MinValue);
            int orderAppearancePreviousVersion = 0;
            int orderAppearanceReplacementVersion = 0;
            int orderEcologyPreviousVersion = 0;
            int orderEcologyReplacementVersion = 0;
            double orderWitnessStarted = Time.realtimeSinceStartupAsDouble;
            double orderWitnessDeadline = orderWitnessStarted + RecoveryWitnessTimeoutSeconds;
            bool appearanceOrderPrepared = false;
            bool ecologyOrderPrepared = false;
            yield return PrepareReplacementScenarioOwner(
                streamer,
                usedCoordinates,
                true,
                true,
                orderWitnessDeadline,
                (prepared, coordinate, previousVersion, replacementVersion) =>
                {
                    appearanceOrderPrepared = prepared;
                    orderAppearanceFirst = coordinate;
                    orderAppearancePreviousVersion = previousVersion;
                    orderAppearanceReplacementVersion = replacementVersion;
                });
            if (!appearanceOrderPrepared)
                throw new InvalidOperationException("appearance-first completion-order owner was unavailable after " +
                    (Time.realtimeSinceStartupAsDouble - orderWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            // note: Keep appearance held until ecology publishes so this witness actually proves ecology cannot publish a traversable cell first.
            yield return PrepareReplacementScenarioOwner(
                streamer,
                usedCoordinates,
                true,
                true,
                orderWitnessDeadline,
                (prepared, coordinate, previousVersion, replacementVersion) =>
                {
                    ecologyOrderPrepared = prepared;
                    orderEcologyFirst = coordinate;
                    orderEcologyPreviousVersion = previousVersion;
                    orderEcologyReplacementVersion = replacementVersion;
                });
            if (!ecologyOrderPrepared)
                throw new InvalidOperationException("ecology-first completion-order owner was unavailable after " +
                    (Time.realtimeSinceStartupAsDouble - orderWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) + "s");
            bool orderPrepared = appearanceOrderPrepared && ecologyOrderPrepared;
            // note: Persist each observed order boundary before advancing; a timeout or interrupted run must retain its partial evidence.
            report.AppendLine("- completionOrderPrepared: " + (orderPrepared ? "PASS" : "FAIL") +
                " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - orderWitnessStarted).ToString("0.000", CultureInfo.InvariantCulture) +
                " appearanceVersion=" + orderAppearancePreviousVersion + "->" + orderAppearanceReplacementVersion +
                " ecologyVersion=" + orderEcologyPreviousVersion + "->" + orderEcologyReplacementVersion);
            WriteReport(report, false, false);
            bool firstAppearanceStarted = false;
            bool firstAppearanceReady = false;
            bool firstEcologyReady = false;
            yield return ObservePublicationPhase(report, "appearanceFirst.worker", streamer, orderAppearanceFirst, orderPrepared, snapshot => snapshot.appearanceWorkActive, matched => firstAppearanceStarted = matched);
            streamer.ReleasePublicationVerificationStage(orderAppearanceFirst, YQPublicationVerificationStage.Appearance);
            yield return ObservePublicationPhase(report, "appearanceFirst.appearance", streamer, orderAppearanceFirst, firstAppearanceStarted, snapshot => snapshot.appearanceReady, matched => firstAppearanceReady = matched);
            YQSemanticChunkPublicationSnapshot appearanceFirstSnapshot;
            // note: Collision traversability is independent of semantic appearance and ecology publication, so this witness checks the held stage directly.
            bool appearanceFirstRemainedBlocked = streamer.TryGetPublicationSnapshot(orderAppearanceFirst, out appearanceFirstSnapshot) &&
                appearanceFirstSnapshot.appearanceReady && !appearanceFirstSnapshot.requiredEcologyReady;
            report.AppendLine("- appearanceFirst.blockedUntilEcology: " + (firstAppearanceReady && appearanceFirstRemainedBlocked ? "PASS" : "FAIL"));
            WriteReport(report, false, false);
            streamer.ReleasePublicationVerificationStage(orderAppearanceFirst, YQPublicationVerificationStage.RequiredEcology);
            yield return ObservePublicationPhase(report, "appearanceFirst.complete", streamer, orderAppearanceFirst, firstAppearanceReady, snapshot => snapshot.requiredEcologyReady, matched => firstEcologyReady = matched);

            bool secondEcologyStarted = false;
            bool secondEcologyReady = false;
            yield return ObservePublicationPhase(report, "ecologyFirst.worker", streamer, orderEcologyFirst, orderPrepared, snapshot => snapshot.ecologyWorkActive, matched => secondEcologyStarted = matched);
            streamer.ReleasePublicationVerificationStage(orderEcologyFirst, YQPublicationVerificationStage.RequiredEcology);
            yield return ObservePublicationPhase(report, "ecologyFirst.ecology", streamer, orderEcologyFirst, secondEcologyStarted, snapshot => snapshot.requiredEcologyReady, matched => secondEcologyReady = matched);
            YQSemanticChunkPublicationSnapshot ecologyFirstSnapshot;
            bool ecologyFirstRemainedBlocked = streamer.TryGetPublicationSnapshot(orderEcologyFirst, out ecologyFirstSnapshot) &&
                ecologyFirstSnapshot.requiredEcologyReady && !ecologyFirstSnapshot.appearanceReady;
            report.AppendLine("- ecologyFirst.blockedUntilAppearance: " + (secondEcologyReady && ecologyFirstRemainedBlocked ? "PASS" : "FAIL"));
            WriteReport(report, false, false);
            streamer.ReleasePublicationVerificationStage(orderEcologyFirst, YQPublicationVerificationStage.Appearance);
            // note: Completion-order verification ends when both held publication stages are ready; collision coverage is asserted by separate movement gates.
            bool secondAppearanceReady = false;
            yield return ObservePublicationPhase(report, "ecologyFirst.complete", streamer, orderEcologyFirst, secondEcologyReady, snapshot => snapshot.appearanceReady, matched => secondAppearanceReady = matched);
            streamer.EndPublicationVerificationScenario(orderAppearanceFirst);
            streamer.EndPublicationVerificationScenario(orderEcologyFirst);
            bool orderPassed = orderPrepared && firstAppearanceReady && appearanceFirstRemainedBlocked && firstEcologyReady &&
                secondEcologyReady && ecologyFirstRemainedBlocked && secondAppearanceReady;
            report.AppendLine("- completionOrderGate: " + (orderPassed ? "PASS" : "FAIL") +
                " prepared=" + orderPrepared +
                " firstAppearanceReady=" + firstAppearanceReady +
                " appearanceBlocked=" + appearanceFirstRemainedBlocked +
                " firstEcologyReady=" + firstEcologyReady +
                " secondEcologyReady=" + secondEcologyReady +
                " ecologyBlocked=" + ecologyFirstRemainedBlocked +
                " secondAppearanceReady=" + secondAppearanceReady +
                " appearanceCoordinate=" + orderAppearanceFirst +
                " ecologyCoordinate=" + orderEcologyFirst);
            WriteReport(report, false, false);
            allPassed &= orderPassed;

            if (_focusedR1PublicationWitnessOnly && !_focusedR1UnloadRevisitWitnessOnly)
            {
                // note: End after cancellation, retry, lag, missing-ground, and completion-order witnesses when the separate unload/revisit witness was not requested.
                report.AppendLine("- unloadRevisitGate: NOT_RUN_FOCUSED_R1_SCOPE");
                result(allPassed, recoveryInputDeliveryInconclusive, false, false);
                yield break;
            }

            // note: Track a live non-authored terrain owner through natural unload and reverse traversal so the gate never mistakes a distant scene-owned prop for a revisit.
            Vector2Int revisitCoordinate = streamer.CurrentChunk;
            YQSemanticChunkPublicationSnapshot revisitStartSnapshot = default;
            bool revisitCandidateCaptured = false;
            bool captureStoppedAtInvalidTraversal = false;
            bool unloadMotorValid = true;
            bool captureInputObserved = false;
            bool captureMoved = false;
            string captureTraversalFailure = string.Empty;
            Vector2Int lastCaptureCoordinate = revisitCoordinate;
            int captureCandidateAttempts = 0;
            int captureCandidatesScanned = 0;
            int captureRejectedNoOwner = 0;
            int captureRejectedAuthored = 0;
            int captureRejectedExplicitFailure = 0;
            int captureRejectedPhysical = 0;
            int captureRejectedTraversability = 0;
            int captureRejectedTerrain = 0;
            int captureRejectedSiteLease = 0;
            int captureRejectedVerificationPin = 0;
            string captureCandidateRouteFailure = string.Empty;
            double captureStartedAt = Time.realtimeSinceStartupAsDouble;
            Vector3 lastCapturePosition = player.position;
            Vector3 captureApproachHeading = Vector3.zero;
            Func<bool> captureCurrentStreamedOwner = () =>
            {
                Vector2Int currentCoordinate = streamer.CurrentChunk;
                // note: Capture only a streamed cell without a site or verification lease so this lifecycle witness is expected to unload naturally after the player leaves.
                if (!streamer.TryGetPublicationSnapshot(currentCoordinate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                    snapshot.authoredTerrain || snapshot.hasExplicitFailure || !snapshot.physicalRepresentation || !snapshot.traversable ||
                    snapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady ||
                    snapshot.siteTerrainDependency || snapshot.siteTerrainHandoffPending ||
                    snapshot.publicationVerificationPinned)
                    return false;
                revisitCoordinate = currentCoordinate;
                revisitStartSnapshot = snapshot;
                revisitCandidateCaptured = true;
                return true;
            };

            // note: Snapshot a small stable neighborhood first so the probe walks to a real published owner instead of zigzagging indefinitely in one empty cell.
            float captureChunkSize = Mathf.Max(32f, streamer.chunkWorldSize);
            int authoredOuterCell = Mathf.CeilToInt(1024f / captureChunkSize) - 1;
            Vector2Int captureCenter = streamer.CurrentChunk;
            int exteriorCellsX = captureCenter.x < 0 || captureCenter.x > authoredOuterCell
                ? 1
                : Mathf.Min(captureCenter.x + 1, authoredOuterCell - captureCenter.x + 1);
            int exteriorCellsZ = captureCenter.y < 0 || captureCenter.y > authoredOuterCell
                ? 1
                : Mathf.Min(captureCenter.y + 1, authoredOuterCell - captureCenter.y + 1);
            // note: Startup can be centered in the authored 1024 m terrain; include its nearest outer cell so discovery can use an already-published continuation owner before any prior motor course moves the player outside.
            int captureCandidateSearchRadius = Mathf.Max(2, Mathf.Min(exteriorCellsX, exteriorCellsZ));
            const int maximumCaptureCandidates = 8;
            const double captureCandidateTimeoutSeconds = 180d;
            List<Vector2Int> captureCandidates = new List<Vector2Int>(maximumCaptureCandidates);
            List<float> captureCandidateDistances = new List<float>(maximumCaptureCandidates);
            for (int ring = 0; ring <= captureCandidateSearchRadius; ring++)
            {
                for (int offsetX = -ring; offsetX <= ring; offsetX++)
                for (int offsetZ = -ring; offsetZ <= ring; offsetZ++)
                {
                    if (Mathf.Max(Mathf.Abs(offsetX), Mathf.Abs(offsetZ)) != ring)
                        continue;

                    Vector2Int coordinate = streamer.CurrentChunk + new Vector2Int(offsetX, offsetZ);
                    captureCandidatesScanned++;
                    if (!streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot))
                    {
                        captureRejectedNoOwner++;
                        continue;
                    }
                    if (snapshot.authoredTerrain)
                    {
                        captureRejectedAuthored++;
                        continue;
                    }
                    if (snapshot.hasExplicitFailure)
                    {
                        captureRejectedExplicitFailure++;
                        continue;
                    }
                    if (!snapshot.physicalRepresentation)
                    {
                        captureRejectedPhysical++;
                        continue;
                    }
                    if (!snapshot.traversable)
                    {
                        captureRejectedTraversability++;
                        continue;
                    }
                    if (snapshot.terrainReadiness < YQTerrainReadinessState.CollisionReady)
                    {
                        captureRejectedTerrain++;
                        continue;
                    }
                    if (snapshot.siteTerrainDependency || snapshot.siteTerrainHandoffPending)
                    {
                        captureRejectedSiteLease++;
                        continue;
                    }
                    if (snapshot.publicationVerificationPinned)
                    {
                        captureRejectedVerificationPin++;
                        continue;
                    }

                    float centerX = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                        (coordinate.x + 0.5f) * captureChunkSize;
                    float centerZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                        (coordinate.y + 0.5f) * captureChunkSize;
                    float dx = centerX - player.position.x;
                    float dz = centerZ - player.position.z;
                    float distanceSquared = dx * dx + dz * dz;
                    int insertAt = 0;
                    while (insertAt < captureCandidateDistances.Count &&
                           captureCandidateDistances[insertAt] <= distanceSquared)
                        insertAt++;
                    if (insertAt >= maximumCaptureCandidates)
                        continue;
                    captureCandidates.Insert(insertAt, coordinate);
                    captureCandidateDistances.Insert(insertAt, distanceSquared);
                    if (captureCandidates.Count > maximumCaptureCandidates)
                    {
                        captureCandidates.RemoveAt(captureCandidates.Count - 1);
                        captureCandidateDistances.RemoveAt(captureCandidateDistances.Count - 1);
                    }
                }
            }

            // note: Try nearest published owners under a shared wall-clock deadline; every leg still uses the production motor and rejects an invalid traversal immediately.
            if (captureCandidates.Count == 0)
            {
                captureCandidateRouteFailure = "no eligible published owner within cell radius " + captureCandidateSearchRadius +
                    "; rejected noOwner=" + captureRejectedNoOwner +
                    ",authored=" + captureRejectedAuthored +
                    ",explicitFailure=" + captureRejectedExplicitFailure +
                    ",physical=" + captureRejectedPhysical +
                    ",traversable=" + captureRejectedTraversability +
                    ",terrain=" + captureRejectedTerrain +
                    ",siteLease=" + captureRejectedSiteLease +
                    ",verificationPin=" + captureRejectedVerificationPin;
                report.AppendLine("- unloadRevisitCandidateScan: FAIL scanned=" + captureCandidatesScanned +
                    " radius=" + captureCandidateSearchRadius +
                    " eligible=0 reason=" + captureCandidateRouteFailure);
                WriteReport(report, false, false);
            }
            else
            {
                report.AppendLine("- unloadRevisitCandidateScan: PASS scanned=" + captureCandidatesScanned +
                    " radius=" + captureCandidateSearchRadius +
                    " eligible=" + captureCandidates.Count +
                    " rejectedNoOwner=" + captureRejectedNoOwner +
                    " rejectedAuthored=" + captureRejectedAuthored +
                    " rejectedExplicitFailure=" + captureRejectedExplicitFailure +
                    " rejectedPhysical=" + captureRejectedPhysical +
                    " rejectedTraversability=" + captureRejectedTraversability +
                    " rejectedTerrain=" + captureRejectedTerrain +
                    " rejectedSiteLease=" + captureRejectedSiteLease +
                    " rejectedVerificationPin=" + captureRejectedVerificationPin);
                WriteReport(report, false, false);
            }
            double captureDeadline = captureStartedAt + Math.Min(PublicationRecoveryTimeoutSeconds, captureCandidateTimeoutSeconds);
            while (!captureCurrentStreamedOwner() && !captureStoppedAtInvalidTraversal &&
                   captureCandidateAttempts < captureCandidates.Count &&
                   Time.realtimeSinceStartupAsDouble < captureDeadline)
            {
                Vector2Int candidateCoordinate = captureCandidates[captureCandidateAttempts++];
                if (candidateCoordinate == streamer.CurrentChunk && captureCurrentStreamedOwner())
                    break;

                float targetX = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                    (candidateCoordinate.x + 0.5f) * captureChunkSize;
                float targetZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin +
                    (candidateCoordinate.y + 0.5f) * captureChunkSize;
                float targetDistance = PlanarDistance(player.position, new Vector3(targetX, player.position.y, targetZ));
                float captureRouteSeconds = Mathf.Clamp(targetDistance / Mathf.Max(1f, motor.walkSpeed) * 1.75f + 3f, 3f, 180f);
                captureRouteSeconds = Mathf.Min(captureRouteSeconds,
                    Mathf.Max(0.05f, (float)(captureDeadline - Time.realtimeSinceStartupAsDouble)));
                double candidateRouteDeadline = Math.Min(captureDeadline,
                    Time.realtimeSinceStartupAsDouble + captureRouteSeconds);
                string candidateArrivalFailure = string.Empty;
                bool candidateInputObserved = false;
                bool candidateMoved = false;
                int candidateBlockingContactsBefore = motor.BlockingControllerContactCount;
                bool candidateBypassingObstacle = false;
                bool candidateBypassOppositeTried = false;
                Vector3 candidateBypassDirection = Vector3.zero;
                Vector3 candidateBypassStartPosition = player.position;
                Func<bool> stopAtCandidate = () =>
                {
                    lastCaptureCoordinate = streamer.CurrentChunk;
                    if (lastCaptureCoordinate == candidateCoordinate)
                    {
                        if (captureCurrentStreamedOwner())
                            return true;
                        candidateArrivalFailure = "owner no longer met publication filters at arrival coordinate=" + candidateCoordinate;
                        return true;
                    }

                    bool valid = streamer.TryValidateCurrentTraversability(
                        player.position,
                        characterController.radius,
                        characterController.skinWidth,
                        out captureTraversalFailure);
                    captureStoppedAtInvalidTraversal |= !valid;
                    if (!valid)
                    {
                        candidateArrivalFailure = "invalid traversal before candidate coordinate=" + candidateCoordinate + " reason=" + captureTraversalFailure;
                        return true;
                    }
                    return false;
                };
                const float captureRouteCorrectionIntervalSeconds = 2f;
                float lastCandidateDistance = targetDistance;
                double lastCandidateProgressAt = Time.realtimeSinceStartupAsDouble;
                while (!revisitCandidateCaptured && !captureStoppedAtInvalidTraversal &&
                       Time.realtimeSinceStartupAsDouble < candidateRouteDeadline)
                {
                    if (stopAtCandidate())
                        break;

                    double now = Time.realtimeSinceStartupAsDouble;
                    float remainingDistance = PlanarDistance(player.position,
                        new Vector3(targetX, player.position.y, targetZ));
                    if (candidateBypassingObstacle)
                    {
                        if (PlanarDistance(candidateBypassStartPosition, player.position) >= 8f)
                        {
                            // note: Return to direct target steering after moving far enough along the collision surface to clear its local edge.
                            candidateBypassingObstacle = false;
                            candidateBypassOppositeTried = false;
                            lastCandidateDistance = remainingDistance;
                            lastCandidateProgressAt = now;
                        }
                        else if (now - lastCandidateProgressAt >= RecoveryNoProgressTimeoutSeconds)
                        {
                            if (!candidateBypassOppositeTried)
                            {
                                candidateBypassDirection = -candidateBypassDirection;
                                candidateBypassStartPosition = player.position;
                                candidateBypassOppositeTried = true;
                                lastCandidateProgressAt = now;
                            }
                            else
                            {
                                candidateArrivalFailure = "terrain-contact tangent made no progress on either side coordinate=" + candidateCoordinate;
                                break;
                            }
                        }
                    }
                    else if (remainingDistance + 0.05f < lastCandidateDistance)
                    {
                        lastCandidateDistance = remainingDistance;
                        lastCandidateProgressAt = now;
                    }
                    else if (now - lastCandidateProgressAt >= RecoveryNoProgressTimeoutSeconds)
                    {
                        // note: Follow the measured terrain-contact tangent before giving up on a candidate whose straight heading is blocked.
                        Vector3 contactNormal = motor.LastBlockingControllerContactNormal;
                        contactNormal.y = 0f;
                        Vector3 tangent = Vector3.zero;
                        Vector3 targetDirectionForBypass = new Vector3(targetX - player.position.x, 0f, targetZ - player.position.z);
                        if (contactNormal.sqrMagnitude > 0.01f && targetDirectionForBypass.sqrMagnitude > 0.01f)
                        {
                            contactNormal.Normalize();
                            tangent = targetDirectionForBypass - Vector3.Dot(targetDirectionForBypass, contactNormal) * contactNormal;
                        }
                        if (tangent.sqrMagnitude < 0.01f && targetDirectionForBypass.sqrMagnitude > 0.01f)
                            tangent = Vector3.Cross(Vector3.up, targetDirectionForBypass);
                        if (tangent.sqrMagnitude < 0.01f)
                        {
                            candidateArrivalFailure = "candidate route stalled without a usable terrain-contact tangent coordinate=" + candidateCoordinate;
                            break;
                        }

                        tangent.Normalize();
                        if (Vector3.Dot(tangent, targetDirectionForBypass) < 0f)
                            tangent = -tangent;
                        candidateBypassDirection = tangent;
                        candidateBypassStartPosition = player.position;
                        candidateBypassingObstacle = true;
                        candidateBypassOppositeTried = false;
                        lastCandidateProgressAt = now;
                        lastCandidateDistance = remainingDistance;
                    }

                    // note: Recompute the production-motor heading in short legs so a changing facing cannot carry the player past the selected continuation cell.
                    Vector3 targetDelta = new Vector3(targetX - player.position.x, 0f, targetZ - player.position.z);
                    Vector3 targetDirection = candidateBypassingObstacle
                        ? candidateBypassDirection
                        : targetDelta.sqrMagnitude > 0.0001f ? targetDelta.normalized : Vector3.zero;
                    KeyboardStateForMotorDirection(motor, targetDirection, out Vector2 targetMoveInput);
                    ResolveMovementKeys(targetMoveInput, out UnityEngine.InputSystem.Key captureDirection,
                        out UnityEngine.InputSystem.Key captureSecondaryDirection);
                    if (captureDirection == UnityEngine.InputSystem.Key.None)
                    {
                        candidateArrivalFailure = "candidate heading resolved to no motor input coordinate=" + candidateCoordinate;
                        break;
                    }

                    float routeStepSeconds = Mathf.Min(captureRouteCorrectionIntervalSeconds,
                        Mathf.Max(0.05f, (float)(candidateRouteDeadline - now)));
                    yield return DriveProductionInput(
                        keyboard,
                        streamer,
                        motor,
                        player,
                        characterController,
                        report,
                        routeStepSeconds,
                        captureDirection,
                        value => unloadMotorValid &= value,
                        (inputObserved, moved, valid) =>
                        {
                            captureInputObserved |= inputObserved;
                            captureMoved |= moved;
                            candidateInputObserved |= inputObserved;
                            candidateMoved |= moved;
                        },
                        stopAtCandidate,
                        captureSecondaryDirection,
                        noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds,
                        positionObserved: position =>
                        {
                            // note: Preserve the physical approach heading across input release so entering a cell does not reverse the unload route toward its entry edge.
                            Vector3 observedStep = position - lastCapturePosition;
                            observedStep.y = 0f;
                            if (observedStep.sqrMagnitude > 0.0001f)
                                captureApproachHeading = observedStep.normalized;
                            lastCapturePosition = position;
                        });
                }
                if (revisitCandidateCaptured)
                    captureCandidateRouteFailure = string.Empty;
                else
                    captureCandidateRouteFailure = string.IsNullOrWhiteSpace(candidateArrivalFailure)
                        ? "candidate route ended before published owner was reached coordinate=" + candidateCoordinate
                        : candidateArrivalFailure;
                report.AppendLine("- unloadRevisitCandidateAttempt: index=" + captureCandidateAttempts +
                    " coordinate=" + candidateCoordinate +
                    " reached=" + revisitCandidateCaptured +
                    " inputObserved=" + candidateInputObserved +
                    " moved=" + candidateMoved +
                    " currentChunk=" + streamer.CurrentChunk +
                    " reason=" + captureCandidateRouteFailure +
                    " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - captureStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                AppendProductionMovementDiagnostics(
                    report,
                    motor,
                    "unload-revisit-candidate-" + captureCandidateAttempts,
                    candidateBlockingContactsBefore);
                AppendTerrainContactDiagnostics(report, streamer, motor);
                WriteReport(report, false, false);
            }
            float captureElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - captureStartedAt);
            report.AppendLine("- unloadRevisitCandidate: " + (revisitCandidateCaptured ? "PASS" : "FAIL") +
                " coordinate=" + revisitCoordinate +
                " authored=" + revisitStartSnapshot.authoredTerrain +
                " physical=" + revisitStartSnapshot.physicalRepresentation +
                " traversable=" + revisitStartSnapshot.traversable +
                " terrain=" + revisitStartSnapshot.terrainReadiness +
                " publicationVersion=" + revisitStartSnapshot.publicationVersion +
                " ownerEpoch=" + revisitStartSnapshot.ownerEpoch +
                " siteDependency=" + revisitStartSnapshot.siteTerrainDependency +
                " siteHandoff=" + revisitStartSnapshot.siteTerrainHandoffPending +
                " verificationPin=" + revisitStartSnapshot.publicationVerificationPinned +
                " captureInputObserved=" + captureInputObserved +
                " captureMoved=" + captureMoved +
                " candidateAttempts=" + captureCandidateAttempts +
                " candidateScanRadius=" + captureCandidateSearchRadius +
                " candidatesScanned=" + captureCandidatesScanned +
                " eligibleCandidates=" + captureCandidates.Count +
                " rejectedNoOwner=" + captureRejectedNoOwner +
                " rejectedAuthored=" + captureRejectedAuthored +
                " rejectedExplicitFailure=" + captureRejectedExplicitFailure +
                " rejectedPhysical=" + captureRejectedPhysical +
                " rejectedTraversability=" + captureRejectedTraversability +
                " rejectedTerrain=" + captureRejectedTerrain +
                " rejectedSiteLease=" + captureRejectedSiteLease +
                " rejectedVerificationPin=" + captureRejectedVerificationPin +
                " candidateRouteFailure=" + captureCandidateRouteFailure +
                " currentChunk=" + lastCaptureCoordinate +
                " traversalBlocks=" + streamer.TraversalConstraintBlockCount +
                " traversalBlockCell=" + streamer.LastTraversalConstraintCell +
                " traversalFailure=" + captureTraversalFailure +
                " stoppedAtInvalidTraversal=" + captureStoppedAtInvalidTraversal +
                " elapsedSeconds=" + captureElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);

            int estimatedUnloadRadius = Mathf.Max(
                streamer.unloadRadius,
                Mathf.Max(streamer.generationRadius,
                    Mathf.Max(streamer.visualRadius,
                        Mathf.Max(streamer.visualPrewarmRadius, streamer.visualRetentionRadius))));
            float unloadTravelDistance = (estimatedUnloadRadius + 1.5f) * streamer.chunkWorldSize;
            // note: Real obstacle bypasses lengthen this motor-only route beyond its straight-line estimate; let the observed unload or traversal guard finish it within the existing bounded allowance.
            float unloadLegTimeoutSeconds = UnloadRevisitTimeoutSeconds;
            bool outboundInputObserved = false;
            bool outboundMoved = false;
            bool outboundStoppedAtInvalidTraversal = false;
            bool outboundRouteFailed = false;
            bool outsideRetentionBoundaryReached = false;
            bool outboundUsingLateralBypass = false;
            bool outboundBoundaryClearancePending = false;
            int lateralBypassOriginCell = int.MinValue;
            int lateralBypassSide = 1;
            int lateralBypassFailedSides = 0;
            bool unloadedObserved = false;
            YQSemanticChunkPublicationSnapshot unloadedSnapshot = default;
            int outboundDirectionAttempts = 0;
            double outboundStartedAt = Time.realtimeSinceStartupAsDouble;
            Vector3 unloadDestination = player.position;
            // note: Retain only successful physical positions so the return follows the same safe corridor in reverse.
            List<Vector3> outboundReturnWaypoints = new List<Vector3>(64);
            // note: Keep motor-observed samples close enough that reverse navigation preserves the actual outbound corridor through cell-local obstacles.
            Action<Vector3> captureReturnWaypointSample = position =>
            {
                if (outboundReturnWaypoints.Count == 0 ||
                    PlanarDistance(outboundReturnWaypoints[outboundReturnWaypoints.Count - 1], position) >= ReturnWaypointSampleDistanceMeters)
                    AppendReturnWaypoint(outboundReturnWaypoints, position);
            };
            if (revisitCandidateCaptured)
            {
                AppendReturnWaypoint(outboundReturnWaypoints, player.position);
                // note: Choose a world-space destination beyond the measured lifecycle radius so detours cannot end a long drive close to the witness.
                Vector3 revisitTargetForUnload = new Vector3(
                    YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.x + 0.5f) * streamer.chunkWorldSize,
                    player.position.y,
                    YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.y + 0.5f) * streamer.chunkWorldSize);
                // note: Continue the observed approach into the streamed world; the offset from the cell center points backward immediately after entry.
                Vector3 initialOutwardDirection = captureApproachHeading.sqrMagnitude > 0.01f
                    ? captureApproachHeading
                    : motor.PlanarVelocity;
                initialOutwardDirection.y = 0f;
                if (initialOutwardDirection.sqrMagnitude < 0.01f)
                    initialOutwardDirection = player.forward;
                initialOutwardDirection.y = 0f;
                initialOutwardDirection.Normalize();
                // note: A cardinal target clears the Chebyshev retention boundary with the shortest truthful player traversal.
                if (Mathf.Abs(initialOutwardDirection.x) >= Mathf.Abs(initialOutwardDirection.z))
                    initialOutwardDirection = new Vector3(Mathf.Sign(initialOutwardDirection.x), 0f, 0f);
                else
                    initialOutwardDirection = new Vector3(0f, 0f, Mathf.Sign(initialOutwardDirection.z));
                Vector3 lateralBypassDirection = Vector3.Cross(Vector3.up, initialOutwardDirection);
                unloadDestination = revisitTargetForUnload +
                    initialOutwardDirection * unloadTravelDistance;
                report.AppendLine("- unloadRevisitRoutePlan: approachHeading=" + captureApproachHeading.ToString("F3") +
                    " outwardHeading=" + initialOutwardDirection.ToString("F3") +
                    " destination=" + unloadDestination.ToString("F3") +
                    " retentionRadius=" + estimatedUnloadRadius);
                WriteReport(report, false, false);
                Func<bool> stopAfterNaturalUnload = () =>
                {
                    if (streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot snapshot) &&
                        snapshot.lifecycle == YQSemanticChunkLifecycle.Unloaded &&
                        !snapshot.traversable &&
                        snapshot.publicationVersion > revisitStartSnapshot.publicationVersion)
                    {
                        unloadedSnapshot = snapshot;
                        unloadedObserved = true;
                        return true;
                    }
                    bool beyondUnloadRadius = false;
                    bool hasActiveRetentionDemand = false;
                    if (streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot retentionSnapshot))
                    {
                        // note: Keep the real route running while camera, speed-runway, and site leases still legitimately retain the witness.
                        beyondUnloadRadius = retentionSnapshot.currentChunkDistance >
                            Mathf.Max(estimatedUnloadRadius, retentionSnapshot.effectiveUnloadRadius);
                        hasActiveRetentionDemand = retentionSnapshot.demanded ||
                            retentionSnapshot.canonicalPreparationDemanded ||
                            retentionSnapshot.predictedViewDemanded ||
                            retentionSnapshot.provisionalGroundPrefetchDemanded ||
                            retentionSnapshot.provisionalGroundTurnBufferDemanded ||
                            retentionSnapshot.siteTerrainDependency ||
                            retentionSnapshot.siteTerrainHandoffPending ||
                            retentionSnapshot.publicationVerificationPinned;
                    }
                    outsideRetentionBoundaryReached = beyondUnloadRadius && !hasActiveRetentionDemand;
                    bool valid = streamer.TryValidateCurrentTraversability(
                        player.position,
                        characterController.radius,
                        characterController.skinWidth,
                    out _);
                    outboundStoppedAtInvalidTraversal |= !valid;
                    return !valid || outsideRetentionBoundaryReached;
                };
                double outboundDeadline = outboundStartedAt + unloadLegTimeoutSeconds;
                while (!unloadedObserved &&
                       !outboundStoppedAtInvalidTraversal &&
                       !outboundRouteFailed &&
                       !outsideRetentionBoundaryReached &&
                       Time.realtimeSinceStartupAsDouble < outboundDeadline)
                {
                    if (outboundBoundaryClearancePending)
                    {
                        outboundBoundaryClearancePending = false;
                        Vector3 clearanceWorldDirection = -initialOutwardDirection;
                        Vector3 clearanceForward = player.forward;
                        clearanceForward.y = 0f;
                        clearanceForward = clearanceForward.sqrMagnitude > 0.0001f ? clearanceForward.normalized : Vector3.forward;
                        Vector3 clearanceRight = player.right;
                        clearanceRight.y = 0f;
                        clearanceRight = clearanceRight.sqrMagnitude > 0.0001f ? clearanceRight.normalized : Vector3.right;
                        Vector2 clearanceMoveInput = new Vector2(
                            Vector3.Dot(clearanceWorldDirection, clearanceRight),
                            Vector3.Dot(clearanceWorldDirection, clearanceForward));
                        ResolveMovementKeys(clearanceMoveInput, out UnityEngine.InputSystem.Key clearanceDirection,
                            out UnityEngine.InputSystem.Key clearanceSecondaryDirection);
                        Vector3 clearanceStartPosition = player.position;
                        Vector2Int clearanceStartChunk = streamer.CurrentChunk;
                        bool clearanceInputObserved = false;
                        bool clearanceMoved = false;
                        double clearanceStartedAt = Time.realtimeSinceStartupAsDouble;
                        // note: Step the real motor farther inside the published cell before a lateral bypass; measured edge drift can exceed capsule clearance during a long strafe.
                        yield return DriveProductionInput(
                            keyboard,
                            streamer,
                            motor,
                            player,
                            characterController,
                            report,
                            2f,
                            clearanceDirection,
                            value => unloadMotorValid &= value,
                            (inputObserved, moved, valid) =>
                            {
                                outboundInputObserved |= inputObserved;
                                outboundMoved |= moved;
                                clearanceInputObserved |= inputObserved;
                                clearanceMoved |= moved;
                            },
                            stopAfterNaturalUnload,
                            clearanceSecondaryDirection,
                            noProgressTimeoutSeconds: 2f,
                            positionObserved: captureReturnWaypointSample);
                        if (clearanceMoved)
                            AppendReturnWaypoint(outboundReturnWaypoints, player.position);
                        bool clearanceValid = streamer.TryValidateCurrentTraversability(
                            player.position,
                            characterController.radius,
                            characterController.skinWidth,
                            out _);
                        outboundStoppedAtInvalidTraversal |= !clearanceValid;
                        report.AppendLine("- unloadRevisitBoundaryClearance: input=" + clearanceDirection +
                            (clearanceSecondaryDirection == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + clearanceSecondaryDirection) +
                            " startChunk=" + clearanceStartChunk +
                            " currentChunk=" + streamer.CurrentChunk +
                            " planarDistanceMeters=" + PlanarDistance(clearanceStartPosition, player.position).ToString("0.000", CultureInfo.InvariantCulture) +
                            " inputObserved=" + clearanceInputObserved +
                            " moved=" + clearanceMoved +
                            " motorValid=" + clearanceValid +
                            " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - clearanceStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                        WriteReport(report, false, false);
                        continue;
                    }

                    // note: Use short real-input legs so each movement sample can either confirm progress or trigger a persistent side bypass around an unpublished view cell.
                    Vector2Int chunkBeforeLeg = streamer.CurrentChunk;
                    int lateralCellBeforeLeg = Mathf.Abs(initialOutwardDirection.x) > 0.5f
                        ? chunkBeforeLeg.y
                        : chunkBeforeLeg.x;
                    int traversalBlocksBeforeLeg = streamer.TraversalConstraintBlockCount;
                    Vector3 positionBeforeLeg = player.position;
                    bool lateralBypassThisLeg = outboundUsingLateralBypass;
                    int lateralBypassSideForLeg = lateralBypassThisLeg ? lateralBypassSide : 0;
                    Vector3 desiredOutboundWorldDirection = outboundUsingLateralBypass
                        ? lateralBypassDirection * lateralBypassSide
                        : initialOutwardDirection;
                    Vector3 outboundForward = player.forward;
                    outboundForward.y = 0f;
                    outboundForward = outboundForward.sqrMagnitude > 0.0001f ? outboundForward.normalized : Vector3.forward;
                    Vector3 outboundRight = player.right;
                    outboundRight.y = 0f;
                    outboundRight = outboundRight.sqrMagnitude > 0.0001f ? outboundRight.normalized : Vector3.right;
                    Vector2 outboundMoveInput = new Vector2(
                        Vector3.Dot(desiredOutboundWorldDirection, outboundRight),
                        Vector3.Dot(desiredOutboundWorldDirection, outboundForward));
                    ResolveMovementKeys(outboundMoveInput, out UnityEngine.InputSystem.Key outboundDirection,
                        out UnityEngine.InputSystem.Key outboundSecondaryDirection);
                    float remainingOutboundSeconds = Mathf.Max(0.05f,
                        (float)(outboundDeadline - Time.realtimeSinceStartupAsDouble));
                    float inputLegSeconds = Mathf.Min(remainingOutboundSeconds, RecoveryNoProgressTimeoutSeconds);
                    outboundDirectionAttempts++;
                    double outboundLegStartedAt = Time.realtimeSinceStartupAsDouble;
                    yield return DriveProductionInput(
                        keyboard,
                        streamer,
                        motor,
                        player,
                        characterController,
                        report,
                        inputLegSeconds,
                        outboundDirection,
                        value => unloadMotorValid &= value,
                        (inputObserved, moved, valid) =>
                        {
                            outboundInputObserved |= inputObserved;
                            outboundMoved |= moved;
                        },
                        stopAfterNaturalUnload,
                        outboundSecondaryDirection,
                        noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds,
                        positionObserved: captureReturnWaypointSample);

                    float measuredLegDistance = PlanarDistance(positionBeforeLeg, player.position);
                    if (measuredLegDistance > 0.25f)
                        AppendReturnWaypoint(outboundReturnWaypoints, player.position);
                    Vector2Int chunkAfterLeg = streamer.CurrentChunk;
                    int lateralCellAfterLeg = Mathf.Abs(initialOutwardDirection.x) > 0.5f
                        ? chunkAfterLeg.y
                        : chunkAfterLeg.x;
                    int traversalBlocksAdded = streamer.TraversalConstraintBlockCount - traversalBlocksBeforeLeg;
                    if (outboundUsingLateralBypass)
                    {
                        if (lateralCellAfterLeg != lateralBypassOriginCell)
                        {
                            outboundUsingLateralBypass = false;
                            lateralBypassFailedSides = 0;
                            report.AppendLine("- unloadRevisitBypassCleared: chunk=" + chunkAfterLeg +
                                " lateralCell=" + lateralCellAfterLeg + " legDistanceMeters=" +
                                measuredLegDistance.ToString("0.000", CultureInfo.InvariantCulture));
                            WriteReport(report, false, false);
                        }
                        else if (measuredLegDistance < 0.5f)
                        {
                            lateralBypassFailedSides++;
                            if (lateralBypassFailedSides >= 2)
                            {
                                outboundRouteFailed = true;
                                report.AppendLine("- unloadRevisitNoClearRoute: chunk=" + chunkAfterLeg +
                                    " blockCell=" + streamer.LastTraversalConstraintCell +
                                    " traversalBlocksAdded=" + traversalBlocksAdded +
                                    " attemptedBothLateralSides=true");
                            }
                            else
                            {
                                lateralBypassSide *= -1;
                                lateralBypassOriginCell = lateralCellAfterLeg;
                                outboundBoundaryClearancePending = true;
                                report.AppendLine("- unloadRevisitBypassSwitch: chunk=" + chunkAfterLeg +
                                    " blockCell=" + streamer.LastTraversalConstraintCell +
                                    " traversalBlocksAdded=" + traversalBlocksAdded +
                                    " side=" + lateralBypassSide);
                            }
                            WriteReport(report, false, false);
                        }
                    }
                    else if (measuredLegDistance < 0.5f)
                    {
                        // note: Carry the last successful lateral side across blocked outward attempts so each new edge continues around the barrier instead of reversing into the previous cell.
                        outboundUsingLateralBypass = true;
                        outboundBoundaryClearancePending = true;
                        lateralBypassOriginCell = lateralCellBeforeLeg;
                        lateralBypassFailedSides = 0;
                        report.AppendLine("- unloadRevisitBypassStart: chunk=" + chunkBeforeLeg +
                            " blockCell=" + streamer.LastTraversalConstraintCell +
                            " traversalBlocksAdded=" + traversalBlocksAdded +
                            " side=" + lateralBypassSide +
                            " sideAxis=" + (Mathf.Abs(initialOutwardDirection.x) > 0.5f ? "z" : "x"));
                        WriteReport(report, false, false);
                    }
                    report.AppendLine("- unloadRevisitRouteLeg: leg=" + outboundDirectionAttempts +
                        " mode=" + (lateralBypassThisLeg ? "lateral-bypass" : "outward") +
                        " bypassSide=" + lateralBypassSideForLeg +
                        " input=" + outboundDirection +
                        (outboundSecondaryDirection == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + outboundSecondaryDirection) +
                        " startChunk=" + chunkBeforeLeg +
                        " currentChunk=" + chunkAfterLeg +
                        " startPosition=" + positionBeforeLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                        " currentPosition=" + player.position.ToString("0.000", CultureInfo.InvariantCulture) +
                        " planarDistanceMeters=" + measuredLegDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                        " targetDistanceRemainingMeters=" + PlanarDistance(player.position, unloadDestination).ToString("0.000", CultureInfo.InvariantCulture) +
                        " traversalBlocksAdded=" + traversalBlocksAdded +
                        " lastBlockCell=" + streamer.LastTraversalConstraintCell +
                        " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - outboundLegStartedAt).ToString("0.000", CultureInfo.InvariantCulture) +
                        " outsideRetentionBoundary=" + outsideRetentionBoundaryReached);
                    WriteReport(report, false, false);
                }
                if (outsideRetentionBoundaryReached && !unloadedObserved)
                {
                    // note: Stop the motor at the true retention boundary, then observe the production lifecycle under its own bounded wall-clock receipt window.
                    bool lifecycleUnloadCompleted = false;
                    double lifecycleWaitStartedAt = Time.realtimeSinceStartupAsDouble;
                    yield return WaitForPublicationSnapshot(
                        streamer,
                        revisitCoordinate,
                        snapshot => snapshot.lifecycle == YQSemanticChunkLifecycle.Unloaded &&
                            !snapshot.traversable &&
                            snapshot.publicationVersion > revisitStartSnapshot.publicationVersion,
                        PublicationRecoveryTimeoutSeconds,
                        (matched, snapshot) =>
                        {
                            lifecycleUnloadCompleted = matched;
                            unloadedSnapshot = snapshot;
                        });
                    unloadedObserved = lifecycleUnloadCompleted;
                    report.AppendLine("- unloadRevisitLifecycleReceipt: " + (lifecycleUnloadCompleted ? "PASS" : "FAIL") +
                        " coordinate=" + revisitCoordinate +
                        " capturedVersion=" + revisitStartSnapshot.publicationVersion +
                        " observedVersion=" + unloadedSnapshot.publicationVersion +
                        " ownerEpoch=" + unloadedSnapshot.ownerEpoch +
                        " lifecycle=" + unloadedSnapshot.lifecycle +
                        " traversable=" + unloadedSnapshot.traversable +
                        " activationWorkActive=" + unloadedSnapshot.activationWorkActive +
                        " streamerLifecycleWorkPending=" + unloadedSnapshot.lifecycleWorkPending +
                        " retainedBy{demanded=" + unloadedSnapshot.demanded +
                        ",hardView=" + unloadedSnapshot.hardViewDemanded +
                        ",guaranteedView=" + unloadedSnapshot.guaranteedViewDemanded +
                        ",preparation=" + unloadedSnapshot.canonicalPreparationDemanded +
                        ",predicted=" + unloadedSnapshot.predictedViewDemanded +
                        ",groundPrefetch=" + unloadedSnapshot.provisionalGroundPrefetchDemanded +
                        ",turnBuffer=" + unloadedSnapshot.provisionalGroundTurnBufferDemanded +
                        ",site=" + unloadedSnapshot.siteTerrainDependency +
                        ",siteHandoff=" + unloadedSnapshot.siteTerrainHandoffPending +
                        ",verificationPin=" + unloadedSnapshot.publicationVerificationPinned +
                        ",distance=" + unloadedSnapshot.currentChunkDistance +
                        ",unloadRadius=" + unloadedSnapshot.effectiveUnloadRadius + "}" +
                        " terrainWorkActive=" + unloadedSnapshot.terrainWorkActive +
                        " contentWorkActive=" + unloadedSnapshot.contentWorkActive +
                        " appearanceWorkActive=" + unloadedSnapshot.appearanceWorkActive +
                        " ecologyWorkActive=" + unloadedSnapshot.ecologyWorkActive +
                        " failure=" + (unloadedSnapshot.failureReason ?? string.Empty) +
                        " timeoutSeconds=" + PublicationRecoveryTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                        " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - lifecycleWaitStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                    WriteReport(report, false, false);
                }
                if (!unloadedObserved && streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot finalOutboundSnapshot))
                {
                    unloadedSnapshot = finalOutboundSnapshot;
                    unloadedObserved = finalOutboundSnapshot.lifecycle == YQSemanticChunkLifecycle.Unloaded &&
                        !finalOutboundSnapshot.traversable &&
                        finalOutboundSnapshot.publicationVersion > revisitStartSnapshot.publicationVersion;
                }
            }
            float outboundElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - outboundStartedAt);
            report.AppendLine("- unloadRevisitUnload: " + (unloadedObserved ? "PASS" : "FAIL") +
                " coordinate=" + revisitCoordinate +
                " capturedVersion=" + revisitStartSnapshot.publicationVersion +
                " unloadedVersion=" + unloadedSnapshot.publicationVersion +
                " unloadedOwnerEpoch=" + unloadedSnapshot.ownerEpoch +
                " lifecycle=" + unloadedSnapshot.lifecycle +
                " physical=" + unloadedSnapshot.physicalRepresentation +
                " inputObserved=" + outboundInputObserved +
                " moved=" + outboundMoved +
                " motorValid=" + unloadMotorValid +
                " currentChunk=" + streamer.CurrentChunk +
                " currentPosition=" + player.position +
                " outsideRetentionBoundary=" + outsideRetentionBoundaryReached +
                " routeFailed=" + outboundRouteFailed +
                " witnessChunkDistance=" + Mathf.Max(Mathf.Abs(streamer.CurrentChunk.x - revisitCoordinate.x),
                    Mathf.Abs(streamer.CurrentChunk.y - revisitCoordinate.y)) +
                " targetDistanceRemainingMeters=" + (revisitCandidateCaptured && player != null
                    ? Vector2.Distance(new Vector2(player.position.x, player.position.z),
                        new Vector2(unloadDestination.x, unloadDestination.z)).ToString("0.000", CultureInfo.InvariantCulture)
                    : "n/a") +
                " stoppedAtInvalidTraversal=" + outboundStoppedAtInvalidTraversal +
                " timeoutSeconds=" + unloadLegTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " elapsedSeconds=" + outboundElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);

            Vector3 revisitTarget = new Vector3(
                YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.x + 0.5f) * streamer.chunkWorldSize,
                player.position.y,
                YQContinuousWorldFeatureAuthority.WorldGridOrigin + (revisitCoordinate.y + 0.5f) * streamer.chunkWorldSize);
            Vector3 returnDirection = revisitTarget - player.position;
            returnDirection.y = 0f;
            Vector3 motorForward = player.forward;
            motorForward.y = 0f;
            motorForward = motorForward.sqrMagnitude > 0.0001f ? motorForward.normalized : Vector3.forward;
            Vector3 motorRight = player.right;
            motorRight.y = 0f;
            motorRight = motorRight.sqrMagnitude > 0.0001f ? motorRight.normalized : Vector3.right;
            Vector2 returnMoveInput = returnDirection.sqrMagnitude > 0.0001f
                ? new Vector2(Vector3.Dot(returnDirection.normalized, motorRight), Vector3.Dot(returnDirection.normalized, motorForward))
                : Vector2.zero;
            ResolveMovementKeys(returnMoveInput, out UnityEngine.InputSystem.Key returnPrimaryKey, out UnityEngine.InputSystem.Key returnSecondaryKey);
            float returnDistance = returnDirection.magnitude;
            // note: Allow enough genuine motor time to return from beyond the unload radius and enter the original cell, not just trigger its preparation ring.
            float returnTimeoutSeconds = Mathf.Clamp(
                returnDistance / Mathf.Max(1f, motor.walkSpeed * 0.35f) + 90f,
                120f,
                UnloadRevisitTimeoutSeconds);
            bool returnInputObserved = false;
            bool returnMoved = false;
            bool returnStoppedAtInvalidTraversal = false;
            bool returnRouteFailed = false;
            string returnRouteFailure = string.Empty;
            int returnWaypointsCompleted = 0;
            int returnLegCount = 0;
            bool revisited = false;
            YQSemanticChunkPublicationSnapshot revisitedSnapshot = default;
            double returnStartedAt = Time.realtimeSinceStartupAsDouble;
            if (unloadedObserved)
            {
                report.AppendLine("- unloadRevisitReturnStart: PENDING coordinate=" + revisitCoordinate +
                    " targetDistanceMeters=" + returnDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                    " input=" + returnPrimaryKey + (returnSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + returnSecondaryKey) +
                    " reverseWaypoints=" + outboundReturnWaypoints.Count +
                    " physicalTraceStepMeters=" + ReturnWaypointSampleDistanceMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                    " timeoutSeconds=" + returnTimeoutSeconds.ToString("0.000", CultureInfo.InvariantCulture));
                WriteReport(report, false, false);
                Func<bool> revisitedReady = () =>
                {
                    if (!streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                        !snapshot.demanded || !snapshot.traversable ||
                        snapshot.publicationVersion <= unloadedSnapshot.publicationVersion ||
                        streamer.CurrentChunk != revisitCoordinate)
                        return false;
                    revisitedSnapshot = snapshot;
                    revisited = true;
                    return true;
                };
                Func<bool> stopReturn = () =>
                {
                    if (revisitedReady())
                        return true;
                    bool valid = streamer.TryValidateCurrentTraversability(
                        player.position,
                        characterController.radius,
                        characterController.skinWidth,
                        out _);
                    returnStoppedAtInvalidTraversal |= !valid;
                    return !valid;
                };
                if (outboundReturnWaypoints.Count < 2)
                {
                    returnRouteFailed = true;
                    returnRouteFailure = "outbound route did not retain enough physical waypoints to retrace";
                }
                else
                {
                    // note: Retrace only positions reached by the real outbound motor so a fixed diagonal cannot drive into a different incomplete cell.
                    double returnDeadline = returnStartedAt + returnTimeoutSeconds;
                    for (int waypointIndex = outboundReturnWaypoints.Count - 2;
                         waypointIndex >= 0 && !revisited && !returnRouteFailed;
                         waypointIndex--)
                    {
                        Vector3 waypoint = outboundReturnWaypoints[waypointIndex];
                        Vector2Int waypointCoordinate = ChunkCoordinateAt(waypoint, streamer.chunkWorldSize);
                        int waypointBypassAttempts = 0;
                        int waypointBypassPreflightFailures = 0;
                        Vector3 waypointBypassAxis = Vector3.zero;
                        // note: Keep a successful physical sidestep on one side so repeated retries accumulate clearance around the same obstacle.
                        float waypointBypassSide = 1f;
                        bool waypointReached = PlanarDistance(player.position, waypoint) <= 1.5f;
                        while (!waypointReached && !revisited && !returnRouteFailed &&
                               Time.realtimeSinceStartupAsDouble < returnDeadline)
                        {
                            Vector3 waypointDirection = waypoint - player.position;
                            waypointDirection.y = 0f;
                            if (waypointDirection.sqrMagnitude <= 0.0001f)
                            {
                                waypointReached = true;
                                break;
                            }
                            Vector3 waypointForward = player.forward;
                            waypointForward.y = 0f;
                            waypointForward = waypointForward.sqrMagnitude > 0.0001f ? waypointForward.normalized : Vector3.forward;
                            Vector3 waypointRight = player.right;
                            waypointRight.y = 0f;
                            waypointRight = waypointRight.sqrMagnitude > 0.0001f ? waypointRight.normalized : Vector3.right;
                            // note: Approach one dominant world axis at a time and give the rendered input pump enough wall time to move toward a physical waypoint.
                            Vector3 waypointAxisDirection = Mathf.Abs(waypointDirection.x) >= Mathf.Abs(waypointDirection.z)
                                ? new Vector3(Mathf.Sign(waypointDirection.x), 0f, 0f)
                                : new Vector3(0f, 0f, Mathf.Sign(waypointDirection.z));
                            Vector2 waypointInput = new Vector2(
                                Vector3.Dot(waypointAxisDirection, waypointRight),
                                Vector3.Dot(waypointAxisDirection, waypointForward));
                            ResolveMovementKeys(waypointInput,
                                out UnityEngine.InputSystem.Key waypointPrimaryKey,
                                out UnityEngine.InputSystem.Key waypointSecondaryKey);
                            float waypointAxisDistance = Mathf.Max(
                                Mathf.Abs(waypointDirection.x),
                                Mathf.Abs(waypointDirection.z));
                            float waypointLegSeconds = Mathf.Min(
                                RecoveryNoProgressTimeoutSeconds,
                                Mathf.Min(
                                    Mathf.Max(0.75f, waypointAxisDistance / Mathf.Max(1f, motor.walkSpeed) * 0.7f),
                                    Mathf.Max(0.05f, (float)(returnDeadline - Time.realtimeSinceStartupAsDouble))));
                            Vector3 waypointLegStartPosition = player.position;
                            float waypointDistanceBeforeLeg = PlanarDistance(waypointLegStartPosition, waypoint);
                            double waypointLegStartedAt = Time.realtimeSinceStartupAsDouble;
                            bool legReachedWaypoint = false;
                            yield return DriveProductionInput(
                                keyboard,
                                streamer,
                                motor,
                                player,
                                characterController,
                                report,
                                waypointLegSeconds,
                                waypointPrimaryKey,
                                value => unloadMotorValid &= value,
                                (inputObserved, moved, valid) =>
                                {
                                    returnInputObserved |= inputObserved;
                                    returnMoved |= moved;
                                },
                                () =>
                                {
                                    if (revisitedReady())
                                        return true;
                                    if (PlanarDistance(player.position, waypoint) <= 1.5f)
                                    {
                                        legReachedWaypoint = true;
                                        return true;
                                    }
                                    return stopReturn();
                                },
                                waypointSecondaryKey,
                                noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds);
                            returnLegCount++;
                            float waypointLegDistance = PlanarDistance(waypointLegStartPosition, player.position);
                            float waypointDistanceAfterLeg = PlanarDistance(player.position, waypoint);
                            float waypointApproachProgress = waypointDistanceBeforeLeg - waypointDistanceAfterLeg;
                            waypointReached = legReachedWaypoint || waypointDistanceAfterLeg <= 1.5f;
                            if (revisited || waypointReached)
                            {
                                report.AppendLine("- unloadRevisitReturnLeg: PASS waypoint=" + waypointIndex +
                                    " coordinate=" + waypointCoordinate +
                                    " currentChunk=" + streamer.CurrentChunk +
                                    " input=" + waypointPrimaryKey +
                                    (waypointSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + waypointSecondaryKey) +
                                    " planarDistanceMeters=" + waypointLegDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceRemainingMeters=" + waypointDistanceAfterLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceProgressMeters=" + waypointApproachProgress.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - waypointLegStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                                WriteReport(report, false, false);
                                break;
                            }
                            if (returnStoppedAtInvalidTraversal)
                            {
                                returnRouteFailed = true;
                                returnRouteFailure = "invalid current traversability while returning through waypoint " + waypointCoordinate;
                            }
                            // note: Small physical steps count as progress when they reduce distance to the saved route point; only a stalled approach enters publication diagnosis.
                            else if (waypointApproachProgress <= 0.05f)
                            {
                                bool hasWaypointSnapshot = streamer.TryGetPublicationSnapshot(
                                    waypointCoordinate,
                                    out YQSemanticChunkPublicationSnapshot waypointSnapshot);
                                string workContractFailure = string.Empty;
                                bool workContractValid = hasWaypointSnapshot && waypointSnapshot.demanded &&
                                    !waypointSnapshot.traversable &&
                                    streamer.TryValidatePublicationWorkContract(waypointCoordinate, out workContractFailure);
                                if (hasWaypointSnapshot && waypointSnapshot.hasExplicitFailure)
                                {
                                    returnRouteFailed = true;
                                    returnRouteFailure = "return waypoint has explicit publication failure " +
                                        waypointCoordinate + ": " + waypointSnapshot.failureReason;
                                }
                                else if (workContractValid)
                                {
                                    bool waypointPublicationReady = false;
                                    YQSemanticChunkPublicationSnapshot waypointPublicationSnapshot = waypointSnapshot;
                                    float remainingReturnSeconds = Mathf.Max(0f,
                                        (float)(returnDeadline - Time.realtimeSinceStartupAsDouble));
                                    float publicationWaitSeconds = Mathf.Min(PublicationRecoveryTimeoutSeconds, remainingReturnSeconds);
                                    if (publicationWaitSeconds >= 0.05f)
                                    {
                                        yield return WaitForPublicationSnapshot(
                                            streamer,
                                            waypointCoordinate,
                                            snapshot => snapshot.traversable &&
                                                (waypointCoordinate != revisitCoordinate ||
                                                 snapshot.publicationVersion > unloadedSnapshot.publicationVersion),
                                            publicationWaitSeconds,
                                            (matched, snapshot) =>
                                            {
                                                waypointPublicationReady = matched;
                                                waypointPublicationSnapshot = snapshot;
                                            });
                                    }
                                    report.AppendLine("- unloadRevisitReturnPublicationWait: " + (waypointPublicationReady ? "PASS" : "FAIL") +
                                        " coordinate=" + waypointCoordinate +
                                        " demanded=" + waypointSnapshot.demanded +
                                        " terrainWork=" + waypointPublicationSnapshot.terrainWorkActive +
                                        " terrainQueued=" + waypointPublicationSnapshot.terrainWorkQueued +
                                        " contentWork=" + waypointPublicationSnapshot.contentWorkActive +
                                        " contentQueued=" + waypointPublicationSnapshot.contentWorkQueued +
                                        " appearanceWork=" + waypointPublicationSnapshot.appearanceWorkActive +
                                        " ecologyWork=" + waypointPublicationSnapshot.ecologyWorkActive +
                                        " activationWork=" + waypointPublicationSnapshot.activationWorkActive +
                                        " failure=" + (waypointPublicationSnapshot.failureReason ?? workContractFailure) +
                                        " timeoutSeconds=" + publicationWaitSeconds.ToString("0.000", CultureInfo.InvariantCulture));
                                    WriteReport(report, false, false);
                                    if (!waypointPublicationReady)
                                    {
                                        returnRouteFailed = true;
                                        returnRouteFailure = "demanded return waypoint publication did not complete " + waypointCoordinate;
                                    }
                                }
                                else if (hasWaypointSnapshot && waypointSnapshot.traversable)
                                {
                                    if (streamer.CurrentChunk != waypointCoordinate ||
                                        waypointBypassAttempts >= SameOwnerReturnBypassMaxAttempts)
                                    {
                                        returnRouteFailed = true;
                                        returnRouteFailure = streamer.CurrentChunk != waypointCoordinate
                                            ? "stalled return waypoint is no longer inside its traversable owner " + waypointCoordinate
                                            : "bounded same-owner return bypass attempts were exhausted at " + waypointCoordinate;
                                    }
                                    else
                                    {
                                        // note: Try a bounded physical sidestep only when both the player and blocked point belong to the same ready chunk.
                                        if (waypointBypassAxis.sqrMagnitude <= 0.0001f)
                                        {
                                            Vector3 initialBypassDirection = waypoint - player.position;
                                            initialBypassDirection.y = 0f;
                                            if (initialBypassDirection.sqrMagnitude > 0.0001f)
                                                waypointBypassAxis = Vector3.Cross(Vector3.up, initialBypassDirection.normalized).normalized;
                                        }

                                        if (waypointBypassAxis.sqrMagnitude <= 0.0001f)
                                        {
                                            returnRouteFailed = true;
                                            returnRouteFailure = "could not derive a horizontal same-owner bypass direction for " + waypointCoordinate;
                                        }
                                        else
                                        {
                                            int bypassAttempt = waypointBypassAttempts++;
                                            float bypassSide = waypointBypassSide;
                                            Vector3 bypassDirection = waypointBypassAxis * bypassSide;
                                            float bypassSeconds = Mathf.Min(1f,
                                                Mathf.Max(0.75f, SameOwnerReturnBypassStepMeters / Mathf.Max(1f, motor.walkSpeed)));
                                            Vector3 bypassForward = player.forward;
                                            bypassForward.y = 0f;
                                            bypassForward = bypassForward.sqrMagnitude > 0.0001f ? bypassForward.normalized : Vector3.forward;
                                            Vector3 bypassRight = player.right;
                                            bypassRight.y = 0f;
                                            bypassRight = bypassRight.sqrMagnitude > 0.0001f ? bypassRight.normalized : Vector3.right;
                                            Vector2 bypassInput = new Vector2(
                                                Vector3.Dot(bypassDirection, bypassRight),
                                                Vector3.Dot(bypassDirection, bypassForward));
                                            ResolveMovementKeys(bypassInput,
                                                out UnityEngine.InputSystem.Key bypassPrimaryKey,
                                                out UnityEngine.InputSystem.Key bypassSecondaryKey);
                                            Vector2 resolvedBypassInput = ResolveKeyMoveInput(bypassPrimaryKey, bypassSecondaryKey);
                                            Vector3 resolvedBypassDirection = player.forward * resolvedBypassInput.y +
                                                player.right * resolvedBypassInput.x;
                                            resolvedBypassDirection.y = 0f;
                                            resolvedBypassDirection = resolvedBypassDirection.sqrMagnitude > 0.0001f
                                                ? resolvedBypassDirection.normalized
                                                : bypassDirection;
                                            float bypassTargetSpeed = Mathf.Max(motor.walkSpeed, motor.sprintSpeed);
                                            PlayerState bypassPlayerState = PlayerStateManager.Instance != null
                                                ? PlayerStateManager.Instance.state
                                                : null;
                                            if (bypassPlayerState != null)
                                            {
                                                float statMoveBonus = Mathf.Max(0f, bypassPlayerState.stats.moveSpeed - motor.walkSpeed);
                                                float itemMoveBonus = GeneratedRpgContentService.Instance != null
                                                    ? GeneratedRpgContentService.Instance.GetMoveSpeedBonus(bypassPlayerState)
                                                    : 0f;
                                                bypassTargetSpeed += statMoveBonus + itemMoveBonus;
                                            }
                                            Vector3 bypassInitialVelocity = motor.PlanarVelocity;
                                            bypassInitialVelocity.y = 0f;
                                            Vector3 bypassTargetVelocity = resolvedBypassDirection * bypassTargetSpeed;
                                            float bypassAcceleration = motor.IsGrounded
                                                ? motor.acceleration
                                                : motor.acceleration * motor.airControl;
                                            Vector3 bypassExpectedDisplacement = EstimatePlanarMotorDisplacement(
                                                bypassInitialVelocity,
                                                bypassTargetVelocity,
                                                bypassAcceleration,
                                                bypassSeconds);
                                            Vector3 bypassTarget = player.position + bypassExpectedDisplacement;
                                            float bypassTravelMeters = bypassExpectedDisplacement.magnitude;
                                            Vector3 bypassExpectedEnd = bypassTarget;

                                            // note: Bound the same-owner input along its actual lateral sweep, then pad only for the capsule and controller step resolution.
                                            float bypassSafetyMargin = characterController.radius + characterController.skinWidth +
                                                characterController.stepOffset + 0.03f;
                                            float bypassChunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
                                            bool bypassPreflightSafe = Vector3.Dot(resolvedBypassDirection, bypassDirection) > 0.5f;
                                            // note: Check the acceleration-shaped real-motor path, including residual velocity, because quantized camera-relative keys do not exactly follow the requested lateral vector.
                                            for (int sample = 0; sample <= 8 && bypassPreflightSafe; sample++)
                                            {
                                                float sampleSeconds = bypassSeconds * (sample / 8f);
                                                Vector3 samplePosition = player.position + EstimatePlanarMotorDisplacement(
                                                    bypassInitialVelocity,
                                                    bypassTargetVelocity,
                                                    bypassAcceleration,
                                                    sampleSeconds);
                                                bypassPreflightSafe = HasChunkClearanceAt(
                                                    samplePosition,
                                                    waypointCoordinate,
                                                    bypassChunkSize,
                                                    bypassSafetyMargin);
                                            }
                                            if (!bypassPreflightSafe)
                                            {
                                                report.AppendLine("- unloadRevisitReturnBypass: RETRY waypoint=" + waypointIndex +
                                                    " coordinate=" + waypointCoordinate +
                                                    " attempt=" + (bypassAttempt + 1) + "/" + SameOwnerReturnBypassMaxAttempts +
                                                    " side=" + (bypassSide > 0f ? "left" : "right") +
                                                    " expectedTravelMeters=" + bypassTravelMeters.ToString("0.000", CultureInfo.InvariantCulture) +
                                                    " projectedVelocity=" + bypassInitialVelocity.ToString("F2") +
                                                    " projectedDirection=" + resolvedBypassDirection.ToString("F2") +
                                                    " preflight=outside-owner-clearance currentChunk=" + streamer.CurrentChunk);
                                                WriteReport(report, false, false);
                                                waypointBypassSide *= -1f;
                                                waypointBypassPreflightFailures++;
                                                if (waypointBypassPreflightFailures >= 2 ||
                                                    waypointBypassAttempts >= SameOwnerReturnBypassMaxAttempts)
                                                {
                                                    returnRouteFailed = true;
                                                    returnRouteFailure = "neither same-owner return bypass side had safe lateral clearance at " + waypointCoordinate;
                                                }
                                                else
                                                {
                                                    continue;
                                                }
                                            }
                                            else
                                            {
                                                waypointBypassPreflightFailures = 0;
                                                Vector3 bypassStartPosition = player.position;
                                                bool bypassInputObserved = false;
                                                bool bypassMoved = false;
                                                bool bypassFramesValid = true;
                                                bool bypassTargetReached = false;
                                                double bypassStartedAt = Time.realtimeSinceStartupAsDouble;
                                                yield return DriveProductionInput(
                                                    keyboard,
                                                    streamer,
                                                    motor,
                                                    player,
                                                    characterController,
                                                    report,
                                                    bypassSeconds,
                                                    bypassPrimaryKey,
                                                    value =>
                                                    {
                                                        bypassFramesValid &= value;
                                                        unloadMotorValid &= value;
                                                    },
                                                    (inputObserved, moved, valid) =>
                                                    {
                                                        bypassInputObserved |= inputObserved;
                                                        bypassMoved |= moved;
                                                        bypassFramesValid &= valid;
                                                        returnInputObserved |= inputObserved;
                                                        returnMoved |= moved;
                                                    },
                                                    () =>
                                                    {
                                                        if (revisitedReady())
                                                            return true;
                                                        if (streamer.CurrentChunk != waypointCoordinate)
                                                            return true;
                                                        if (PlanarDistance(player.position, bypassTarget) <= 0.75f)
                                                        {
                                                            bypassTargetReached = true;
                                                            return true;
                                                        }
                                                        return stopReturn();
                                                    },
                                                    bypassSecondaryKey,
                                                    noProgressTimeoutSeconds: RecoveryNoProgressTimeoutSeconds);
                                                returnLegCount++;
                                                bool bypassStayedInOwner = streamer.CurrentChunk == waypointCoordinate &&
                                                    ChunkCoordinateAt(player.position, bypassChunkSize) == waypointCoordinate;
                                                bool bypassAccepted = bypassFramesValid && bypassStayedInOwner && !returnStoppedAtInvalidTraversal;
                                                float bypassDistance = PlanarDistance(bypassStartPosition, player.position);
                                                Vector3 bypassActualDelta = player.position - bypassStartPosition;
                                                bypassActualDelta.y = 0f;
                                                float bypassLateralProgress = Vector3.Dot(bypassActualDelta, bypassDirection);
                                                report.AppendLine("- unloadRevisitReturnBypass: " +
                                                    (bypassAccepted && bypassMoved ? "PASS" : bypassAccepted ? "RETRY" : "FAIL") +
                                                    " waypoint=" + waypointIndex +
                                                    " coordinate=" + waypointCoordinate +
                                                    " attempt=" + (bypassAttempt + 1) + "/" + SameOwnerReturnBypassMaxAttempts +
                                                    " side=" + (bypassSide > 0f ? "left" : "right") +
                                                    " input=" + bypassPrimaryKey +
                                                    (bypassSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + bypassSecondaryKey) +
                                                    " inputObserved=" + bypassInputObserved +
                                                    " moved=" + bypassMoved +
                                                    " targetReached=" + bypassTargetReached +
                                                    " lateralProgressMeters=" + bypassLateralProgress.ToString("0.000", CultureInfo.InvariantCulture) +
                                                    " planarDistanceMeters=" + bypassDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                                                    " currentChunk=" + streamer.CurrentChunk +
                                                    " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - bypassStartedAt).ToString("0.000", CultureInfo.InvariantCulture));
                                                WriteReport(report, false, false);
                                                if (!bypassAccepted)
                                                {
                                                    returnRouteFailed = true;
                                                    returnRouteFailure = "same-owner return bypass violated motor validity or chunk ownership at " + waypointCoordinate;
                                                }
                                                else if (waypointBypassAttempts >= SameOwnerReturnBypassMaxAttempts && !bypassMoved)
                                                {
                                                    returnRouteFailed = true;
                                                    returnRouteFailure = "production motor made no progress during bounded same-owner bypass attempts at " + waypointCoordinate;
                                                }
                                                else
                                                {
                                                    // note: Reverse only when collision-resolved motion failed to advance along the chosen same-owner sidestep.
                                                    if (bypassLateralProgress <= 0.5f)
                                                        waypointBypassSide *= -1f;
                                                    continue;
                                                }
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    returnRouteFailed = true;
                                    returnRouteFailure = waypointSnapshot.demanded
                                        ? "demanded return waypoint has no live work, bounded retry, or publication failure " +
                                            waypointCoordinate + ": " + workContractFailure
                                        : "motor made no progress toward return waypoint " + waypointCoordinate +
                                            " without a demanded publication owner";
                                }
                            }
                            if (returnRouteFailed)
                            {
                                report.AppendLine("- unloadRevisitReturnLeg: FAIL waypoint=" + waypointIndex +
                                    " coordinate=" + waypointCoordinate +
                                    " currentChunk=" + streamer.CurrentChunk +
                                    " startPosition=" + waypointLegStartPosition.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetPosition=" + waypoint.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " input=" + waypointPrimaryKey +
                                    (waypointSecondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + waypointSecondaryKey) +
                                    " planarDistanceMeters=" + waypointLegDistance.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceBeforeMeters=" + waypointDistanceBeforeLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceAfterMeters=" + waypointDistanceAfterLeg.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " targetDistanceProgressMeters=" + waypointApproachProgress.ToString("0.000", CultureInfo.InvariantCulture) +
                                    " reason=" + returnRouteFailure);
                                WriteReport(report, false, false);
                                break;
                            }
                            if (Time.realtimeSinceStartupAsDouble >= returnDeadline && !waypointReached)
                            {
                                returnRouteFailed = true;
                                returnRouteFailure = "return wall-clock deadline expired at waypoint " + waypointCoordinate;
                            }
                        }
                        if (waypointReached && !revisited && !returnRouteFailed)
                            returnWaypointsCompleted++;
                        if (!waypointReached && !revisited && !returnRouteFailed)
                        {
                            returnRouteFailed = true;
                            returnRouteFailure = "return wall-clock deadline expired at waypoint " + waypointCoordinate;
                        }
                    }
                    if (!revisited && !returnRouteFailed &&
                        Time.realtimeSinceStartupAsDouble >= returnDeadline)
                    {
                        returnRouteFailed = true;
                        returnRouteFailure = "return wall-clock deadline expired before exact-cell revisit";
                    }
                }
                if (!revisited && streamer.TryGetPublicationSnapshot(revisitCoordinate, out YQSemanticChunkPublicationSnapshot finalRevisitSnapshot))
                {
                    revisitedSnapshot = finalRevisitSnapshot;
                    revisited = finalRevisitSnapshot.demanded && finalRevisitSnapshot.traversable &&
                        finalRevisitSnapshot.publicationVersion > unloadedSnapshot.publicationVersion &&
                        streamer.CurrentChunk == revisitCoordinate;
                }
            }
            float returnElapsedSeconds = (float)(Time.realtimeSinceStartupAsDouble - returnStartedAt);
            // note: Missing input evidence cannot downgrade an observed traversal safety failure to an inconclusive result.
            bool unloadTraversalValid = unloadMotorValid && !captureStoppedAtInvalidTraversal &&
                !outboundStoppedAtInvalidTraversal && !returnStoppedAtInvalidTraversal;
            bool unloadInputInconclusive = unloadedObserved && unloadTraversalValid &&
                (!outboundInputObserved || !returnInputObserved);
            bool unloadPassed = revisitCandidateCaptured && unloadedObserved && revisited &&
                outboundInputObserved && outboundMoved && returnInputObserved && returnMoved &&
                unloadTraversalValid && !returnRouteFailed;
            // note: Unload/revisit belongs to R2 streaming acceptance, so its verdict stays independent from R1 recovery.
            unloadRevisitPassed = unloadPassed;
            unloadRevisitInputInconclusive = unloadInputInconclusive;
            report.AppendLine("- unloadRevisitGate: " + (unloadInputInconclusive ? "INCONCLUSIVE" : unloadPassed ? "PASS" : "FAIL") +
                " candidateCaptured=" + revisitCandidateCaptured +
                " unloaded=" + unloadedObserved +
                " revisited=" + revisited +
                " outboundInputObserved=" + outboundInputObserved +
                " outboundMoved=" + outboundMoved +
                " returnInputObserved=" + returnInputObserved +
                " returnMoved=" + returnMoved +
                " returnRouteFailed=" + returnRouteFailed +
                " returnRouteFailure=" + returnRouteFailure +
                " returnWaypointsCompleted=" + returnWaypointsCompleted + "/" + outboundReturnWaypoints.Count +
                " returnLegCount=" + returnLegCount +
                " motorValid=" + unloadMotorValid +
                " coordinate=" + revisitCoordinate +
                " capturedVersion=" + revisitStartSnapshot.publicationVersion +
                " unloadedVersion=" + unloadedSnapshot.publicationVersion +
                " revisitedVersion=" + revisitedSnapshot.publicationVersion +
                " capturedOwnerEpoch=" + revisitStartSnapshot.ownerEpoch +
                " unloadedOwnerEpoch=" + unloadedSnapshot.ownerEpoch +
                " revisitedOwnerEpoch=" + revisitedSnapshot.ownerEpoch +
                " revisitCurrentChunk=" + streamer.CurrentChunk +
                " revisitedAtCandidate=" + (streamer.CurrentChunk == revisitCoordinate) +
                " revisitPosition=" + player.position.ToString("0.000", CultureInfo.InvariantCulture) +
                " revisitTargetDistanceMeters=" + PlanarDistance(player.position, revisitTarget).ToString("0.000", CultureInfo.InvariantCulture) +
                " stoppedAtInvalidTraversal=" + (captureStoppedAtInvalidTraversal || outboundStoppedAtInvalidTraversal || returnStoppedAtInvalidTraversal) +
                " captureElapsedSeconds=" + captureElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " outboundElapsedSeconds=" + outboundElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                " returnElapsedSeconds=" + returnElapsedSeconds.ToString("0.000", CultureInfo.InvariantCulture));
            WriteReport(report, false, false);
        }
        finally
        {
            // note: Interrupted matrices must release every injected hold, including a phase whose assertion threw before its normal cleanup.
            foreach (Vector2Int coordinate in usedCoordinates)
                if (streamer != null)
                    streamer.EndPublicationVerificationScenario(coordinate);
            if (keyboard != null)
            {
                QueueKeyboardState(
                    keyboard,
                    new UnityEngine.InputSystem.LowLevel.KeyboardState());
            }
        }
        result(allPassed, recoveryInputDeliveryInconclusive, unloadRevisitPassed, unloadRevisitInputInconclusive);
    }

    private static bool TryReplaceRecoveryOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        out Vector2Int coordinate,
        out int previousPublicationVersion,
        out int replacementPublicationVersion)
    {
        previousPublicationVersion = 0;
        replacementPublicationVersion = 0;
        HashSet<Vector2Int> attemptedCoordinates = new HashSet<Vector2Int>();
        // note: Selection and replacement are one bounded operation because the live terrain worker can change between snapshots.
        for (int attempt = 0; attempt < 64; attempt++)
        {
            if (!TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, true, out Vector2Int candidate))
                break;
            attemptedCoordinates.Add(candidate);
            if (streamer.TryReplacePublicationOwnerForVerification(
                    candidate,
                    out previousPublicationVersion,
                    out replacementPublicationVersion))
            {
                usedCoordinates.Add(candidate);
                coordinate = candidate;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    private static IEnumerator PrepareAppearanceRetryOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        double deadline,
        Action<bool, Vector2Int> result)
    {
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (TryPrepareAppearanceRetryOwner(streamer, usedCoordinates, out Vector2Int coordinate))
            {
                result(true, coordinate);
                yield break;
            }
            // note: Let ordinary production streaming finish a retained owner between bounded candidate sweeps instead of failing on one transiently busy frame.
            yield return null;
        }

        result(false, new Vector2Int(int.MinValue, int.MinValue));
    }

    private static bool TryPrepareAppearanceRetryOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        out Vector2Int coordinate)
    {
        // note: Select a completed retained owner, then invalidate only appearance so the retry case does not wait behind a fresh terrain/content admission.
        HashSet<Vector2Int> attemptedCoordinates = new HashSet<Vector2Int>();
        for (int attempt = 0; attempt < 64; attempt++)
        {
            if (!TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, false, out Vector2Int candidate))
                break;
            attemptedCoordinates.Add(candidate);
            if (streamer.TryPrepareAppearanceRetryVerification(candidate))
            {
                usedCoordinates.Add(candidate);
                coordinate = candidate;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    private static IEnumerator PrepareReplacementScenarioOwner(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        bool holdAppearance,
        bool holdRequiredEcology,
        double deadline,
        Action<bool, Vector2Int, int, int> result)
    {
        HashSet<Vector2Int> attemptedCoordinates = new HashSet<Vector2Int>();
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            for (int attempt = 0; attempt < 64; attempt++)
            {
                if (!TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, true, out Vector2Int candidate))
                    break;
                attemptedCoordinates.Add(candidate);
                if (!streamer.TryReplacePublicationOwnerForVerification(
                        candidate,
                        out int previousPublicationVersion,
                        out int replacementPublicationVersion))
                    continue;

                // note: Install the completion-order hold in the same frame as owner replacement so an async publication cannot pass the boundary before the probe starts.
                bool prepared = streamer.BeginPublicationVerificationScenario(
                    candidate,
                    false,
                    holdAppearance,
                    holdRequiredEcology,
                    0,
                    0);
                usedCoordinates.Add(candidate);
                if (!prepared)
                {
                    streamer.EndPublicationVerificationScenario(candidate);
                    continue;
                }

                result(true, candidate, previousPublicationVersion, replacementPublicationVersion);
                yield break;
            }

            // note: Retry the bounded retained-owner search after one production frame; transient terrain handoffs can reject every safe candidate in the current snapshot.
            attemptedCoordinates.Clear();
            yield return null;
        }

        result(false, new Vector2Int(int.MinValue, int.MinValue), 0, 0);
    }

    private static bool TryFindRecoveryCoordinate(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        out Vector2Int coordinate)
    {
        return TryFindRecoveryCoordinate(streamer, usedCoordinates, null, false, out coordinate);
    }

    private static bool TryFindRecoveryCoordinate(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        HashSet<Vector2Int> attemptedCoordinates,
        out Vector2Int coordinate)
    {
        return TryFindRecoveryCoordinate(streamer, usedCoordinates, attemptedCoordinates, false, out coordinate);
    }

    private static bool TryFindRecoveryCoordinate(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        HashSet<Vector2Int> usedCoordinates,
        HashSet<Vector2Int> attemptedCoordinates,
        bool allowPreparationOwners,
        out Vector2Int coordinate)
    {
        Vector2Int current = streamer.CurrentChunk;
        // note: Prefer nearby streamed owners so recovery publication competes with the player's real demand envelope rather than an unrelated retained ring.
        Vector2Int[] localCandidates =
        {
            current + new Vector2Int(1, 0), current + new Vector2Int(-1, 0),
            current + new Vector2Int(0, 1), current + new Vector2Int(0, -1),
            current + new Vector2Int(1, 1), current + new Vector2Int(-1, 1),
            current + new Vector2Int(1, -1), current + new Vector2Int(-1, -1),
            current + new Vector2Int(2, 0), current + new Vector2Int(-2, 0),
            current + new Vector2Int(0, 2), current + new Vector2Int(0, -2),
            current + new Vector2Int(3, 0), current + new Vector2Int(-3, 0),
            current + new Vector2Int(0, 3), current + new Vector2Int(0, -3),
            current + new Vector2Int(5, 0), current + new Vector2Int(-5, 0),
            current + new Vector2Int(0, 5), current + new Vector2Int(0, -5),
            current + new Vector2Int(4, 4), current + new Vector2Int(-4, 4),
            current + new Vector2Int(4, -4), current + new Vector2Int(-4, -4)
        };
        List<Vector2Int> candidates = new List<Vector2Int>(localCandidates.Length + 5);
        YQInvestorPlayerMotor liveMotor = YQInvestorPlayerMotor.ActiveMotor;
        Vector3 planarVelocity = liveMotor != null ? liveMotor.PlanarVelocity : Vector3.zero;
        Vector2 velocity = new Vector2(planarVelocity.x, planarVelocity.z);
        if (velocity.magnitude > 65f)
        {
            Vector2 direction = velocity.normalized;
            Vector2Int travelCell = new Vector2Int(
                Mathf.Abs(direction.x) >= 0.35f ? (direction.x >= 0f ? 1 : -1) : 0,
                Mathf.Abs(direction.y) >= 0.35f ? (direction.y >= 0f ? 1 : -1) : 0);
            // note: The real motor keeps coasting after the probe releases input; choose a demanded owner ahead of that measured travel so cancellation remains observable.
            for (int distance = 5; distance >= 2; distance--)
                candidates.Add(current + travelCell * distance);
        }
        candidates.AddRange(localCandidates);
        // note: Recovery fault injection must stay outside the current camera contract; the streamer separately rejects predicted and emergency-turn owners.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int index = 0; index < candidates.Count; index++)
            {
                Vector2Int candidate = candidates[index];
                if (usedCoordinates.Contains(candidate) ||
                    (attemptedCoordinates != null && attemptedCoordinates.Contains(candidate)) ||
                    candidate == current)
                    continue;
                if (!streamer.TryGetChunkDiagnostics(candidate, out _, out _) ||
                    !streamer.TryGetPublicationSnapshot(candidate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                    snapshot.lifecycle == YQSemanticChunkLifecycle.Unseen || snapshot.authoredTerrain ||
                    (!allowPreparationOwners && snapshot.hardViewDemanded) || !snapshot.demanded)
                    continue;

                int distance = Mathf.Max(
                    Mathf.Abs(candidate.x - current.x),
                    Mathf.Abs(candidate.y - current.y));
                bool isolatedPhysicalOwner = snapshot.physicalRepresentation &&
                    !snapshot.guaranteedViewDemanded && distance >= 2 &&
                    distance <= streamer.VisualRetentionRadius;
                if (pass == 0 && !isolatedPhysicalOwner)
                    continue;
                // note: Probe outside the current camera contract; a non-visible preparation owner is safe when the production API confirms it has no near-term deadline.
                if (pass == 1 && (!snapshot.physicalRepresentation ||
                    (!allowPreparationOwners && snapshot.hardViewDemanded) || snapshot.guaranteedViewDemanded))
                    continue;
                coordinate = candidate;
                return true;
            }
        }
        // note: If the explicit witnesses were consumed by earlier recovery probes, search the complete bounded retention ring before declaring the matrix unable to continue.
        int retention = Mathf.Max(1, streamer.VisualRetentionRadius);
        for (int x = -retention; x <= retention; x++)
        {
            for (int z = -retention; z <= retention; z++)
            {
                Vector2Int candidate = current + new Vector2Int(x, z);
                if (usedCoordinates.Contains(candidate) ||
                    (attemptedCoordinates != null && attemptedCoordinates.Contains(candidate)) ||
                    candidate == current)
                    continue;
                if (!streamer.TryGetPublicationSnapshot(candidate, out YQSemanticChunkPublicationSnapshot snapshot) ||
                    !snapshot.physicalRepresentation || (!allowPreparationOwners && snapshot.hardViewDemanded) ||
                    snapshot.guaranteedViewDemanded || snapshot.authoredTerrain)
                    continue;
                coordinate = candidate;
                return true;
            }
        }
        coordinate = new Vector2Int(int.MinValue, int.MinValue);
        return false;
    }

    private static IEnumerator ObservePublicationPhase(
        StringBuilder report,
        string phase,
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Vector2Int coordinate,
        bool prerequisiteMet,
        Func<YQSemanticChunkPublicationSnapshot, bool> predicate,
        Action<bool> result)
    {
        // note: Skipping a dependent observation preserves failure, without spending another deadline on an order that was never established.
        if (!prerequisiteMet)
        {
            report.AppendLine("- " + phase + ": SKIPPED prerequisite failed coordinate=" + coordinate);
            WriteReport(report, false, false);
            result(false);
            yield break;
        }
        report.AppendLine("- " + phase + ": STARTED coordinate=" + coordinate);
        WriteReport(report, false, false);
        bool completed = false;
        bool matched = false;
        YQSemanticChunkPublicationSnapshot observed = default;
        double started = Time.realtimeSinceStartupAsDouble;
        try
        {
            yield return WaitForPublicationSnapshot(streamer, coordinate, predicate, PublicationRecoveryTimeoutSeconds,
                (success, snapshot) => { completed = true; matched = success; observed = snapshot; });
            result(matched);
        }
        finally
        {
            // note: Iterator disposal also records an interrupted phase before the enclosing matrix writes its terminal receipt.
            report.AppendLine("- " + phase + ": " + (!completed ? "ABORTED" : matched ? "PASS" : "FAIL") +
                " coordinate=" + coordinate +
                " elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - started).ToString("0.000", CultureInfo.InvariantCulture) +
                " explicitFailure=" + observed.hasExplicitFailure + " reason=" + (observed.failureReason ?? string.Empty));
            WriteReport(report, false, false);
        }
    }

    private static IEnumerator WaitForPublicationSnapshot(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Vector2Int coordinate,
        Func<YQSemanticChunkPublicationSnapshot, bool> predicate,
        float timeoutSeconds,
        Action<bool, YQSemanticChunkPublicationSnapshot> result)
    {
        double deadline = Time.realtimeSinceStartupAsDouble + Mathf.Max(0.1f, timeoutSeconds);
        YQSemanticChunkPublicationSnapshot lastSnapshot = default;
        while (Time.realtimeSinceStartupAsDouble < deadline)
        {
            if (streamer.TryGetPublicationSnapshot(coordinate, out lastSnapshot) && predicate(lastSnapshot))
            {
                result(true, lastSnapshot);
                yield break;
            }
            yield return null;
        }
        result(false, lastSnapshot);
    }

    private static IEnumerator DriveProductionInput(
        UnityEngine.InputSystem.Keyboard keyboard,
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor,
        Transform player,
        CharacterController characterController,
        StringBuilder report,
        float seconds,
        UnityEngine.InputSystem.Key key,
        Action<bool> validFrame,
        Action<bool, bool, bool> evidence = null,
        Func<bool> stopWhen = null,
        UnityEngine.InputSystem.Key secondaryKey = UnityEngine.InputSystem.Key.None,
        float noProgressTimeoutSeconds = 0f,
        Action<Vector3> positionObserved = null,
        Func<bool> frameSafety = null)
    {
        double started = Time.realtimeSinceStartupAsDouble;
        double deadline = started + Mathf.Max(0.05f, seconds);
        double lastFrame = started;
        double maximumFrameSeconds = 0.0;
        Vector3 startPosition = player.position;
        Vector3 lastProgressPosition = startPosition;
        double lastProgressAt = started;
        int observedFrames = 0;
        bool inputObserved = false;
        bool allFramesValid = true;
        bool stoppedForNoProgress = false;
        try
        {
            while (Time.realtimeSinceStartupAsDouble < deadline && (stopWhen == null || !stopWhen()))
            {
                // note: Fault probes deliver ordinary movement input while production alone owns acceleration, collision, camera and scheduling.
                UnityEngine.InputSystem.LowLevel.KeyboardState inputState = secondaryKey == UnityEngine.InputSystem.Key.None
                    ? new UnityEngine.InputSystem.LowLevel.KeyboardState(key)
                    : new UnityEngine.InputSystem.LowLevel.KeyboardState(key, secondaryKey);
                QueueKeyboardState(keyboard, inputState);
                yield return null;
                positionObserved?.Invoke(player.position);
                double now = Time.realtimeSinceStartupAsDouble;
                maximumFrameSeconds = Math.Max(maximumFrameSeconds, now - lastFrame);
                lastFrame = now;
                observedFrames++;
                Vector2 requestedInput = ResolveKeyMoveInput(key, secondaryKey);
                inputObserved |= motor.CanProcessMovementInput && requestedInput.sqrMagnitude > 0.01f &&
                    Vector2.Dot(motor.MoveInput, requestedInput) > 0.5f;
                bool valid = streamer.TryValidateCurrentTraversability(
                    player.position, characterController.radius, characterController.skinWidth, out string failure) &&
                    (frameSafety == null || frameSafety());
                if (!valid && allFramesValid)
                {
                    report.AppendLine("- recoveryMotorFailure: " + failure);
                    WriteReport(report, false, false);
                }
                allFramesValid &= valid;
                if (PlanarDistance(lastProgressPosition, player.position) > 0.05f)
                {
                    lastProgressPosition = player.position;
                    lastProgressAt = now;
                }
                if (noProgressTimeoutSeconds > 0f && now - lastProgressAt >= noProgressTimeoutSeconds)
                {
                    stoppedForNoProgress = true;
                    break;
                }
            }
        }
        finally
        {
            // note: Always release input on normal return, assertion failure, cancellation and manual Play Mode exit.
            QueueKeyboardState(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            float distance = PlanarDistance(startPosition, player.position);
            bool moved = observedFrames > 0 && inputObserved && distance > 0.02f;
            report.AppendLine("- recoveryMotorEvidence: keys=" + key +
                (secondaryKey == UnityEngine.InputSystem.Key.None ? string.Empty : "+" + secondaryKey) + ",frames=" + observedFrames +
                ",inputObserved=" + inputObserved + ",planarDistance=" + distance.ToString("0.000", CultureInfo.InvariantCulture) +
                ",elapsedSeconds=" + (Time.realtimeSinceStartupAsDouble - started).ToString("0.000", CultureInfo.InvariantCulture) +
                ",maximumFrameSeconds=" + maximumFrameSeconds.ToString("0.000", CultureInfo.InvariantCulture) +
                ",stoppedForNoProgress=" + stoppedForNoProgress +
                ",traversalBlocks=" + streamer.TraversalConstraintBlockCount +
                ",traversalBlockCell=" + streamer.LastTraversalConstraintCell +
                ",valid=" + allFramesValid + ",moved=" + moved);
            WriteReport(report, false, false);
            // note: Safety validity is independent from whether the editor delivered a usable movement key; unavailable focus is reported separately as inconclusive.
            validFrame(allFramesValid);
            evidence?.Invoke(inputObserved, moved, allFramesValid);
        }
    }

    private static void ResolveMovementKeys(
        Vector2 desiredMoveInput,
        out UnityEngine.InputSystem.Key primaryKey,
        out UnityEngine.InputSystem.Key secondaryKey)
    {
        desiredMoveInput = Vector2.ClampMagnitude(desiredMoveInput, 1f);
        float forwardMagnitude = Mathf.Abs(desiredMoveInput.y);
        float lateralMagnitude = Mathf.Abs(desiredMoveInput.x);
        UnityEngine.InputSystem.Key forwardKey = desiredMoveInput.y >= 0f
            ? UnityEngine.InputSystem.Key.W
            : UnityEngine.InputSystem.Key.S;
        UnityEngine.InputSystem.Key lateralKey = desiredMoveInput.x >= 0f
            ? UnityEngine.InputSystem.Key.D
            : UnityEngine.InputSystem.Key.A;

        // note: Use the production motor's ordinary camera-relative keys to point toward the unloaded owner without changing the camera or player transform.
        if (forwardMagnitude >= lateralMagnitude)
        {
            primaryKey = forwardKey;
            secondaryKey = lateralMagnitude > 0.1f ? lateralKey : UnityEngine.InputSystem.Key.None;
        }
        else
        {
            primaryKey = lateralKey;
            secondaryKey = forwardMagnitude > 0.1f ? forwardKey : UnityEngine.InputSystem.Key.None;
        }
    }

    private static Vector2 ResolveKeyMoveInput(
        UnityEngine.InputSystem.Key primaryKey,
        UnityEngine.InputSystem.Key secondaryKey)
    {
        Vector2 input = ResolveKeyMoveInput(primaryKey);
        if (secondaryKey != UnityEngine.InputSystem.Key.None)
            input += ResolveKeyMoveInput(secondaryKey);
        return Vector2.ClampMagnitude(input, 1f);
    }

    private static Vector2 ResolveKeyMoveInput(UnityEngine.InputSystem.Key key)
    {
        if (key == UnityEngine.InputSystem.Key.W)
            return Vector2.up;
        if (key == UnityEngine.InputSystem.Key.S)
            return Vector2.down;
        if (key == UnityEngine.InputSystem.Key.D)
            return Vector2.right;
        if (key == UnityEngine.InputSystem.Key.A)
            return Vector2.left;
        return Vector2.zero;
    }

    private static void AppendProductionMovementDiagnostics(
        StringBuilder report,
        YQInvestorPlayerMotor motor,
        string phase,
        int blockingContactsBefore)
    {
        // note: Pair the motor request with its collision-resolved displacement and the exact side-contact owner for this movement phase.
        int newBlockingContacts = Math.Max(0, motor.BlockingControllerContactCount - blockingContactsBefore);
        Collider contact = newBlockingContacts > 0 ? motor.LastBlockingControllerCollider : null;
        string contactOwner = "<none this phase>";
        if (contact != null)
        {
            Bounds bounds = contact.bounds;
            contactOwner = DescribeColliderHierarchy(contact.transform) +
                ",instance=" + contact.GetInstanceID() +
                ",layer=" + LayerMask.LayerToName(contact.gameObject.layer) +
                ",tag=" + contact.tag +
                ",boundsCenter=" + bounds.center.ToString("F3") +
                ",boundsSize=" + bounds.size.ToString("F3");
        }

        report.AppendLine("- productionMotorMovementDiagnostics: phase=" + phase +
            ",position=" + motor.transform.position.ToString("F3") +
            ",forward=" + motor.transform.forward.ToString("F3") +
            ",input=" + motor.MoveInput.ToString("F3") +
            ",requestedVelocity=" + motor.PlanarVelocity.ToString("F3") +
            ",lastRequestedMove=" + motor.LastRequestedMoveDisplacement.ToString("F3") +
            ",lastActualMove=" + motor.LastActualMoveDisplacement.ToString("F3") +
            ",collisionFlags=" + motor.LastMoveCollisionFlags +
            ",readinessGateRejected=" + motor.LastMoveRejectedByTraversalGate +
            ",blockingSideContacts=" + newBlockingContacts +
            ",blockingContact=" + contactOwner +
            ",contactPoint=" + motor.LastBlockingControllerContactPoint.ToString("F3") +
            ",contactNormal=" + motor.LastBlockingControllerContactNormal.ToString("F3"));
    }

    private static void AppendTerrainContactDiagnostics(
        StringBuilder report,
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQInvestorPlayerMotor motor)
    {
        // note: Compare the blocked capsule with its owning heightfield so a terrain contact can be distinguished from an authored wall or a player below the sampled surface.
        Vector3 position = motor.transform.position;
        if (!streamer.TryGetGeneratedTerrainAt(position, out Terrain terrain) || terrain == null || terrain.terrainData == null)
        {
            report.AppendLine("- unloadRevisitTerrainContact: terrainAtPlayer=unavailable position=" + position.ToString("F3"));
            return;
        }

        TerrainData data = terrain.terrainData;
        Vector3 local = position - terrain.transform.position;
        float normalizedX = Mathf.Clamp01(local.x / Mathf.Max(0.001f, data.size.x));
        float normalizedZ = Mathf.Clamp01(local.z / Mathf.Max(0.001f, data.size.z));
        float sampledSurfaceY = terrain.SampleHeight(position);
        float steepness = data.GetSteepness(normalizedX, normalizedZ);
        report.AppendLine("- unloadRevisitTerrainContact: terrain=" + DescribeColliderHierarchy(terrain.transform) +
            " enabled=" + terrain.enabled +
            " colliderEnabled=" + (terrain.GetComponent<TerrainCollider>() != null && terrain.GetComponent<TerrainCollider>().enabled) +
            " terrainOrigin=" + terrain.transform.position.ToString("F3") +
            " terrainSize=" + data.size.ToString("F3") +
            " position=" + position.ToString("F3") +
            " sampledSurfaceY=" + sampledSurfaceY.ToString("F3", CultureInfo.InvariantCulture) +
            " capsuleAboveSurface=" + (position.y - sampledSurfaceY).ToString("F3", CultureInfo.InvariantCulture) +
            " localSteepnessDegrees=" + steepness.ToString("F2", CultureInfo.InvariantCulture) +
            " grounded=" + motor.IsGrounded +
            " contactPoint=" + motor.LastBlockingControllerContactPoint.ToString("F3") +
            " contactNormal=" + motor.LastBlockingControllerContactNormal.ToString("F3"));
    }

    private static string DescribeColliderHierarchy(Transform node)
    {
        // note: Build a stable human-readable owner path only when the bounded verification report is written.
        if (node == null)
            return "<none>";
        StringBuilder path = new StringBuilder(node.name);
        while (node.parent != null)
        {
            node = node.parent;
            path.Insert(0, node.name + "/");
        }
        return path.ToString();
    }

    private static void ValidateProductionMotorFrame(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Transform player,
        CharacterController characterController,
        string phase,
        StringBuilder report)
    {
        if (streamer == null || player == null || characterController == null)
            throw new InvalidOperationException("production motor " + phase + " lost its authoritative owner");
        if (streamer.TryValidateCurrentTraversability(
                player.position,
                characterController.radius,
                characterController.skinWidth,
                out string failure))
            return;
        // note: Observe the real motor after its own Update without refreshing or reprioritizing the scheduler from the verifier.
        report.AppendLine("- productionMotor" + phase + "FailurePosition: " +
            player.position.ToString("0.000", CultureInfo.InvariantCulture));
        report.AppendLine("- productionMotor" + phase + "Failure: " + failure);
        WriteReport(report, false, false);
        throw new InvalidOperationException("production motor " + phase + " entered unready traversal space: " + failure);
    }

    private static void VerifyMacroFieldHashParity(StringBuilder report)
    {
        // note: Compare optimized world-field hashes with the prior string-concatenation implementation across signed coordinates and normalized seeds.
        string[] seeds = { "06c4f7d8", "beta-origin-v1", "   ", "  deterministic seed  " };
        Vector2[] points =
        {
            Vector2.zero,
            new Vector2(1234.5f, -678.25f),
            new Vector2(-1024.5f, -64.25f),
            new Vector2(17f, 3000f)
        };
        int sampleCount = 0;
        float maximumDelta = 0f;
        for (int seedIndex = 0; seedIndex < seeds.Length; seedIndex++)
        {
            for (int pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                float elevation;
                float ruggedness;
                float temperature;
                float moisture;
                float forest;
                float grassland;
                float wetland;
                float civilization;
                float danger;
                float culture;
                YQSemanticWorldAuthority.SampleMacroFields(
                    seeds[seedIndex], points[pointIndex].x, points[pointIndex].y,
                    out elevation, out ruggedness, out temperature, out moisture,
                    out forest, out grassland, out wetland, out civilization, out danger, out culture);
                float[] actual = { elevation, ruggedness, temperature, moisture, forest, grassland, wetland, civilization, danger, culture };
                float[] reference = SampleMacroFieldsReference(seeds[seedIndex], points[pointIndex].x, points[pointIndex].y);
                for (int field = 0; field < actual.Length; field++)
                {
                    float delta = Mathf.Abs(actual[field] - reference[field]);
                    maximumDelta = Mathf.Max(maximumDelta, delta);
                    if (actual[field] != reference[field])
                        throw new InvalidOperationException("optimized macro-field hash changed output at seedIndex=" + seedIndex +
                            " pointIndex=" + pointIndex + " field=" + field + " delta=" + delta.ToString("R", CultureInfo.InvariantCulture));
                }
                sampleCount++;
            }
        }
        report.AppendLine("- macroFieldHashParity: PASS seedCoordinateSamples=" + sampleCount +
            " maximumDelta=" + maximumDelta.ToString("0.000000", CultureInfo.InvariantCulture));
    }

    private static float[] SampleMacroFieldsReference(string seed, float worldX, float worldZ)
    {
        string safeSeed = string.IsNullOrWhiteSpace(seed) ? "yourquest_default_world" : seed.Trim();
        float broad = ValueNoiseReference(worldX * 0.0018f, worldZ * 0.0018f, safeSeed + "|landform");
        float ridge = ValueNoiseReference(worldX * 0.0031f, worldZ * 0.0031f, safeSeed + "|ridge");
        float basin = ValueNoiseReference(worldX * 0.0012f, worldZ * 0.0012f, safeSeed + "|basin");
        float moistureField = ValueNoiseReference(worldX * 0.0015f, worldZ * 0.0015f, safeSeed + "|climate_moisture");
        float temperatureField = ValueNoiseReference(worldX * 0.0011f, worldZ * 0.0011f, safeSeed + "|climate_temperature");
        float ecologyField = ValueNoiseReference(worldX * 0.0042f, worldZ * 0.0042f, safeSeed + "|ecology");
        float cultureField = ValueNoiseReference(worldX * 0.0017f, worldZ * 0.0017f, safeSeed + "|culture");
        float dangerField = ValueNoiseReference(worldX * 0.0029f, worldZ * 0.0029f, safeSeed + "|danger");
        float ruggedness = Mathf.Clamp01(Mathf.Abs(ridge * 2f - 1f) * 0.62f + broad * 0.18f);
        float elevation = Mathf.Clamp01(0.28f + broad * 0.18f + ruggedness * 0.24f - basin * 0.11f);
        float temperature = Mathf.Clamp01(temperatureField * 0.82f + 0.09f);
        float moisture = Mathf.Clamp01(moistureField * 0.82f + basin * 0.14f);
        float forest = Mathf.Clamp01((moisture * 0.72f + ecologyField * 0.28f) * (1f - temperature * 0.22f));
        float wetland = Mathf.Clamp01(moisture * 0.72f + (1f - elevation) * 0.2f + (1f - ecologyField) * 0.08f);
        float grassland = Mathf.Clamp01((1f - ruggedness) * 0.46f + (1f - wetland) * 0.34f + ecologyField * 0.2f);
        float total = Mathf.Max(0.001f, forest + grassland + wetland);
        float ecologyForest = forest / total;
        float ecologyGrassland = grassland / total;
        float ecologyWetland = wetland / total;
        float civilization = Mathf.Clamp01((1f - ruggedness) * 0.42f + cultureField * 0.36f + ecologyGrassland * 0.22f);
        float danger = Mathf.Clamp01(dangerField * 0.58f + ruggedness * 0.27f + (1f - civilization) * 0.15f);
        float culture = Mathf.Clamp01(cultureField * 0.8f + ecologyGrassland * 0.1f + (1f - danger) * 0.1f);
        return new[] { elevation, ruggedness, temperature, moisture, ecologyForest, ecologyGrassland, ecologyWetland, civilization, danger, culture };
    }

    private static float ValueNoiseReference(float x, float z, string seed)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        float tx = x - x0;
        float tz = z - z0;
        tx = tx * tx * (3f - 2f * tx);
        tz = tz * tz * (3f - 2f * tz);
        float a = Hash01Reference(seed + "|" + x0 + "|" + z0);
        float b = Hash01Reference(seed + "|" + (x0 + 1) + "|" + z0);
        float c = Hash01Reference(seed + "|" + x0 + "|" + (z0 + 1));
        float d = Hash01Reference(seed + "|" + (x0 + 1) + "|" + (z0 + 1));
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static float Hash01Reference(string value)
    {
        uint hash = 2166136261u;
        string safe = value ?? string.Empty;
        for (int index = 0; index < safe.Length; index++)
        {
            unchecked
            {
                hash ^= safe[index];
                hash *= 16777619u;
            }
        }
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static UnityEngine.InputSystem.LowLevel.KeyboardState KeyboardStateForMotorDirection(
        YQInvestorPlayerMotor motor,
        Vector3 worldDirection,
        out Vector2 expectedMoveInput)
    {
        expectedMoveInput = Vector2.zero;
        if (motor == null)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState();

        // note: Convert a world-space test heading to ordinary WASD relative to the authoritative motor's current basis.
        worldDirection.y = 0f;
        if (worldDirection.sqrMagnitude <= 0.0001f)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState();
        worldDirection.Normalize();
        Vector3 forward = motor.transform.forward;
        Vector3 right = motor.transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();
        float localX = Vector3.Dot(worldDirection, right);
        float localY = Vector3.Dot(worldDirection, forward);
        int xSign = localX >= 0.25f ? 1 : localX <= -0.25f ? -1 : 0;
        int ySign = localY >= 0.25f ? 1 : localY <= -0.25f ? -1 : 0;
        expectedMoveInput = Vector2.ClampMagnitude(new Vector2(xSign, ySign), 1f);

        UnityEngine.InputSystem.Key horizontal = xSign < 0
            ? UnityEngine.InputSystem.Key.A
            : xSign > 0 ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.None;
        UnityEngine.InputSystem.Key vertical = ySign < 0
            ? UnityEngine.InputSystem.Key.S
            : ySign > 0 ? UnityEngine.InputSystem.Key.W : UnityEngine.InputSystem.Key.None;
        if (horizontal != UnityEngine.InputSystem.Key.None && vertical != UnityEngine.InputSystem.Key.None)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState(horizontal, vertical);
        if (horizontal != UnityEngine.InputSystem.Key.None)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState(horizontal);
        if (vertical != UnityEngine.InputSystem.Key.None)
            return new UnityEngine.InputSystem.LowLevel.KeyboardState(vertical);
        return new UnityEngine.InputSystem.LowLevel.KeyboardState();
    }

    private static Vector3 MotorWorldPlanarDirection(YQInvestorPlayerMotor motor, Vector2 moveInput)
    {
        if (motor == null)
            return Vector3.forward;
        Vector3 direction = motor.transform.forward * moveInput.y + motor.transform.right * moveInput.x;
        direction.y = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
    }

    private static bool IsExpectedMotorInput(Vector2 actualInput, Vector2 expectedInput)
    {
        // note: Compare the motor's consumed axes with the keyboard event sent for the same real Input System frame.
        return expectedInput.sqrMagnitude > 0.5f && Vector2.Distance(actualInput, expectedInput) <= 0.15f;
    }

    private static int HighSpeedTransitionMaskBit(Vector2 expectedInput)
    {
        // note: Preserve the six signed transition classes while the speed lane follows an origin-safe eight-direction loop.
        if (expectedInput.x > 0.1f && expectedInput.y > 0.1f) return 0;
        if (expectedInput.x > 0.1f && expectedInput.y < -0.1f) return 1;
        if (expectedInput.x < -0.1f && expectedInput.y < -0.1f) return 2;
        if (expectedInput.x < -0.1f && expectedInput.y > 0.1f) return 3;
        if (Mathf.Abs(expectedInput.x) <= 0.1f && expectedInput.y < -0.1f) return 4;
        if (Mathf.Abs(expectedInput.x) <= 0.1f && expectedInput.y > 0.1f) return 5;
        return -1;
    }

    private static void QueueKeyboardState(
        UnityEngine.InputSystem.Keyboard keyboard,
        UnityEngine.InputSystem.LowLevel.KeyboardState state)
    {
        if (keyboard == null)
            return;
        // note: Queue a normal device event for the next Dynamic Input System update so wasPressedThisFrame observes a real edge instead of a late direct-buffer mutation.
        if (!keyboard.enabled)
            UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
        keyboard.MakeCurrent();
        UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard, state);
        // note: Keep the named test device current after queuing so a physical keyboard cannot silently replace the device read by the next motor frame.
        keyboard.MakeCurrent();
    }

    private static void AppendInjectedKeyboardDiagnostics(
        StringBuilder report,
        UnityEngine.InputSystem.Keyboard injectedKeyboard,
        string phase)
    {
        // note: Capture the live Input System state at the first post-event motor sample so a failed probe distinguishes event delivery from locomotion rejection.
        UnityEngine.InputSystem.Keyboard currentKeyboard = UnityEngine.InputSystem.Keyboard.current;
        report.AppendLine("- productionInputDiagnostics: phase=" + phase +
            ",injected=" + (injectedKeyboard != null ? injectedKeyboard.name : "<null>") +
            ",injectedAdded=" + (injectedKeyboard != null && injectedKeyboard.added) +
            ",injectedW=" + (injectedKeyboard != null && injectedKeyboard.wKey.isPressed) +
            ",injectedQ=" + (injectedKeyboard != null && injectedKeyboard.qKey.isPressed) +
            ",current=" + (currentKeyboard != null ? currentKeyboard.name : "<null>") +
            ",currentW=" + (currentKeyboard != null && currentKeyboard.wKey.isPressed) +
            ",currentQ=" + (currentKeyboard != null && currentKeyboard.qKey.isPressed) +
            ",updateType=" + UnityEngine.InputSystem.LowLevel.InputState.currentUpdateType +
            ",updateCount=" + UnityEngine.InputSystem.LowLevel.InputState.updateCount);
        WriteReport(report, false, false);
    }

    private static UnityEngine.InputSystem.Keyboard AcquireVerificationKeyboard()
    {
        ConfigureVerificationInputRouting();
        // note: Reuse the session-scoped probe device when Unity has already created it; never remove devices during Play because that can invalidate InputSystem global state.
        foreach (UnityEngine.InputSystem.InputDevice device in UnityEngine.InputSystem.InputSystem.devices)
        {
            if (device is UnityEngine.InputSystem.Keyboard existing &&
                string.Equals(existing.name, "YQRuntimeVerificationKeyboard", StringComparison.Ordinal))
            {
                HookVerificationKeyboard(existing);
                // note: A prior failed probe may have left the private virtual device disabled after focus loss; restore only this probe device.
                if (!existing.enabled)
                    UnityEngine.InputSystem.InputSystem.EnableDevice(existing);
                return existing;
            }
        }
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>(
            "YQRuntimeVerificationKeyboard");
        HookVerificationKeyboard(keyboard);
        // note: Keep the verifier's private virtual device enabled when the editor loses focus; never enable or mutate the user's physical devices.
        if (!keyboard.enabled)
            UnityEngine.InputSystem.InputSystem.EnableDevice(keyboard);
        return keyboard;
    }

    private static void HookVerificationKeyboard(UnityEngine.InputSystem.Keyboard keyboard)
    {
        _verificationKeyboard = keyboard;
        if (_verificationKeyboardHooked)
            return;
        // note: Reassert current-device ownership after all queued/native events are processed, before the next production motor Update.
        UnityEngine.InputSystem.InputSystem.onAfterUpdate += KeepVerificationKeyboardCurrent;
        _verificationKeyboardHooked = true;
    }

    private static void KeepVerificationKeyboardCurrent()
    {
        if (_verificationKeyboard != null && _verificationKeyboard.added)
            _verificationKeyboard.MakeCurrent();
    }

    private static void ReleaseVerificationKeyboardHook()
    {
        if (_verificationKeyboardHooked)
            UnityEngine.InputSystem.InputSystem.onAfterUpdate -= KeepVerificationKeyboardCurrent;
        _verificationKeyboardHooked = false;
        _verificationKeyboard = null;
        // note: Restore the exact pre-verification settings object before destroying the temporary routing copy.
        if (_verificationOriginalInputSettings != null)
            UnityEngine.InputSystem.InputSystem.settings = _verificationOriginalInputSettings;
        if (_verificationTestInputSettings != null)
        {
            UnityEngine.Object.Destroy(_verificationTestInputSettings);
            _verificationTestInputSettings = null;
        }
        _verificationOriginalInputSettings = null;
    }

    private static void ConfigureVerificationInputRouting()
    {
        if (_verificationTestInputSettings != null &&
            UnityEngine.InputSystem.InputSystem.settings == _verificationTestInputSettings)
            return;

        // note: A private settings clone routes test-device events while unfocused without persisting changes to the project's Input System asset.
        _verificationOriginalInputSettings = UnityEngine.InputSystem.InputSystem.settings;
        if (_verificationOriginalInputSettings == null)
            return;
        _verificationTestInputSettings = UnityEngine.Object.Instantiate(_verificationOriginalInputSettings);
        _verificationTestInputSettings.name = "YQ Runtime Verification Input Settings";
        _verificationTestInputSettings.backgroundBehavior = UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
        _verificationTestInputSettings.editorInputBehaviorInPlayMode =
            UnityEngine.InputSystem.InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        UnityEngine.InputSystem.InputSystem.settings = _verificationTestInputSettings;
    }

    private static float PlanarDistance(Vector3 from, Vector3 to)
    {
        // note: Measure only horizontal displacement so falling/ground correction cannot masquerade as locomotion progress.
        Vector2 delta = new Vector2(to.x - from.x, to.z - from.z);
        return delta.magnitude;
    }

    private static Vector3 EstimatePlanarMotorDisplacement(
        Vector3 initialVelocity,
        Vector3 targetVelocity,
        float acceleration,
        float seconds)
    {
        initialVelocity.y = 0f;
        targetVelocity.y = 0f;
        float duration = Mathf.Max(0f, seconds);
        Vector3 velocityDelta = targetVelocity - initialVelocity;
        float velocityChange = velocityDelta.magnitude;
        float accelerationRate = Mathf.Max(0.01f, acceleration);
        if (velocityChange <= 0.0001f)
            return initialVelocity * duration;

        Vector3 accelerationDirection = velocityDelta / velocityChange;
        float accelerationDuration = velocityChange / accelerationRate;
        if (duration <= accelerationDuration)
            return initialVelocity * duration + accelerationDirection * (0.5f * accelerationRate * duration * duration);

        float acceleratedDistanceTime = accelerationDuration;
        return initialVelocity * acceleratedDistanceTime +
            accelerationDirection * (0.5f * accelerationRate * acceleratedDistanceTime * acceleratedDistanceTime) +
            targetVelocity * (duration - acceleratedDistanceTime);
    }

    private static bool HasChunkClearanceAt(
        Vector3 center,
        Vector2Int owner,
        float chunkSize,
        float clearanceMeters)
    {
        // note: Check the controller's padded footprint at every projected path sample, not only the requested endpoint.
        Vector3 rightClearance = Vector3.right * Mathf.Max(0f, clearanceMeters);
        Vector3 forwardClearance = Vector3.forward * Mathf.Max(0f, clearanceMeters);
        return ChunkCoordinateAt(center, chunkSize) == owner &&
            ChunkCoordinateAt(center + rightClearance, chunkSize) == owner &&
            ChunkCoordinateAt(center - rightClearance, chunkSize) == owner &&
            ChunkCoordinateAt(center + forwardClearance, chunkSize) == owner &&
            ChunkCoordinateAt(center - forwardClearance, chunkSize) == owner;
    }

    private static void AppendReturnWaypoint(List<Vector3> waypoints, Vector3 position)
    {
        // note: Store only physical motor positions, not a synthesized path, and suppress tiny clearance duplicates.
        if (waypoints == null ||
            (waypoints.Count > 0 && PlanarDistance(waypoints[waypoints.Count - 1], position) <= 0.25f))
            return;
        waypoints.Add(position);
    }

    private static Vector2Int ChunkCoordinateAt(Vector3 position, float chunkSize)
    {
        // note: Map a world waypoint back through the same absolute grid origin used by streamed terrain ownership.
        float size = Mathf.Max(1f, chunkSize);
        return new Vector2Int(
            Mathf.FloorToInt((position.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / size),
            Mathf.FloorToInt((position.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / size));
    }

    private static bool TryGetNextChunkBoundary(
        Vector3 position,
        Vector3 travelDirection,
        float chunkSize,
        out float distanceMeters,
        out Vector2Int sourceCell,
        out Vector2Int targetCell)
    {
        // note: Align the runtime motor with the first absolute world-cell edge on its actual planar path, including negative coordinates.
        float size = Mathf.Max(32f, chunkSize);
        Vector2 direction = new Vector2(travelDirection.x, travelDirection.z);
        sourceCell = ChunkCoordinateAt(position, size);
        targetCell = sourceCell;
        distanceMeters = float.PositiveInfinity;
        if (direction.sqrMagnitude <= 0.0001f)
            return false;
        direction.Normalize();

        if (direction.x > 0.0001f)
        {
            float boundaryX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + (sourceCell.x + 1) * size;
            distanceMeters = Mathf.Max(0f, (boundaryX - position.x) / direction.x);
            targetCell.x++;
        }
        else if (direction.x < -0.0001f)
        {
            float boundaryX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + sourceCell.x * size;
            distanceMeters = Mathf.Max(0f, (boundaryX - position.x) / direction.x);
            targetCell.x--;
        }

        if (direction.y > 0.0001f)
        {
            float boundaryZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + (sourceCell.y + 1) * size;
            float candidateDistance = Mathf.Max(0f, (boundaryZ - position.z) / direction.y);
            if (candidateDistance < distanceMeters)
            {
                distanceMeters = candidateDistance;
                targetCell = sourceCell + Vector2Int.up;
            }
        }
        else if (direction.y < -0.0001f)
        {
            float boundaryZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + sourceCell.y * size;
            float candidateDistance = Mathf.Max(0f, (boundaryZ - position.z) / direction.y);
            if (candidateDistance < distanceMeters)
            {
                distanceMeters = candidateDistance;
                targetCell = sourceCell + Vector2Int.down;
            }
        }

        return !float.IsInfinity(distanceMeters) && !float.IsNaN(distanceMeters);
    }

    private static Transform ResolveAuthoritativePlayer()
    {
        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor != null && motor.IsAuthoritative)
            return motor.transform;
        // note: A tagged object without the authoritative motor is not a valid production traversal subject and must never satisfy this probe.
        return null;
    }

    private static float SampleSharedEdgeDelta(Terrain origin, Terrain extension, string edge)
    {
        // note: Compare the exact shared border at multiple points across the overlap so a single matching corner cannot hide a seam.
        if (origin == null || extension == null || origin.terrainData == null || extension.terrainData == null)
            return float.PositiveInfinity;
        bool vertical = edge == "east" || edge == "west";
        float boundary = vertical
            ? (edge == "east" ? origin.transform.position.x + origin.terrainData.size.x : origin.transform.position.x)
            : (edge == "north" ? origin.transform.position.z + origin.terrainData.size.z : origin.transform.position.z);
        float overlapMin = vertical
            ? Mathf.Max(origin.transform.position.z, extension.transform.position.z)
            : Mathf.Max(origin.transform.position.x, extension.transform.position.x);
        float overlapMax = vertical
            ? Mathf.Min(origin.transform.position.z + origin.terrainData.size.z, extension.transform.position.z + extension.terrainData.size.z)
            : Mathf.Min(origin.transform.position.x + origin.terrainData.size.x, extension.transform.position.x + extension.terrainData.size.x);
        if (overlapMax < overlapMin)
            return float.PositiveInfinity;
        float maximum = 0f;
        for (int sample = 0; sample <= 32; sample++)
        {
            float along = Mathf.Lerp(overlapMin, overlapMax, sample / 32f);
            Vector3 point = vertical ? new Vector3(boundary, 0f, along) : new Vector3(along, 0f, boundary);
            float originHeight = origin.SampleHeight(point);
            float extensionHeight = extension.SampleHeight(point);
            maximum = Mathf.Max(maximum, Mathf.Abs(originHeight - extensionHeight));
        }
        return maximum;
    }

    private static bool TryGetReadyStreamedTerrainAt(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        Terrain authoredTerrain,
        Vector3 position,
        out Terrain terrain)
    {
        terrain = FindTerrainAt(position);
        if (streamer == null || terrain == null || terrain == authoredTerrain ||
            !terrain.enabled || !terrain.gameObject.activeInHierarchy)
            return false;
        TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
        if (collider == null || !collider.enabled)
            return false;

        float chunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
        Vector2Int coordinate = new Vector2Int(
            Mathf.FloorToInt((position.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize),
            Mathf.FloorToInt((position.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize));
        return streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot) &&
            !snapshot.authoredTerrain && snapshot.physicalRepresentation &&
            snapshot.terrainReadiness >= YQTerrainReadinessState.CollisionReady && snapshot.traversable;
    }

    private static bool TryValidateCanonicalContinuationBoundarySamples(
        YQPlayerFollowingSemanticChunkStreamer streamer,
        YQContinuousWorldCellAuthority authority,
        Terrain originTerrain,
        Vector3[] publicationProbePositions,
        out int sampleCount,
        out int demandedWorkContracts,
        out string failure)
    {
        sampleCount = 0;
        demandedWorkContracts = 0;
        failure = string.Empty;
        if (streamer == null || authority == null || originTerrain == null || originTerrain.terrainData == null ||
            publicationProbePositions == null)
        {
            failure = "canonical continuation sample owner is missing";
            return false;
        }

        // note: Probe the canonical height authority at each unmaterialized edge without admitting Terrain owners just for verification.
        Vector3 origin = originTerrain.transform.position;
        Vector3 size = originTerrain.terrainData.size;
        float inset = Mathf.Clamp(Mathf.Max(0.05f, streamer.chunkWorldSize * 0.005f), 0.05f, 0.5f);
        float minimumX = origin.x;
        float maximumX = origin.x + size.x;
        float minimumZ = origin.z;
        float maximumZ = origin.z + size.z;
        float centerX = (minimumX + maximumX) * 0.5f;
        float centerZ = (minimumZ + maximumZ) * 0.5f;
        Vector2[] samplePositions =
        {
            new Vector2(minimumX + inset, centerZ), new Vector2(minimumX - inset, centerZ),
            new Vector2(maximumX - inset, centerZ), new Vector2(maximumX + inset, centerZ),
            new Vector2(centerX, minimumZ + inset), new Vector2(centerX, minimumZ - inset),
            new Vector2(centerX, maximumZ - inset), new Vector2(centerX, maximumZ + inset),
            new Vector2(minimumX - inset, minimumZ - inset), new Vector2(minimumX - inset, maximumZ + inset),
            new Vector2(maximumX + inset, minimumZ - inset), new Vector2(maximumX + inset, maximumZ + inset)
        };
        HashSet<Vector2Int> checkedDemandedOwners = new HashSet<Vector2Int>();
        for (int index = 0; index < samplePositions.Length; index++)
        {
            Vector2 point = samplePositions[index];
            float height = authority.SampleHeightNormalized(point.x, point.y);
            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                failure = "canonical continuation height was non-finite at " + point;
                return false;
            }
            sampleCount++;

            float chunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
            Vector2Int coordinate = new Vector2Int(
                Mathf.FloorToInt((point.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize),
                Mathf.FloorToInt((point.y - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize));
            if (!checkedDemandedOwners.Add(coordinate))
                continue;
            bool hasSnapshot = streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot);
            if (!snapshot.demanded || (hasSnapshot && snapshot.traversable))
                continue;
            if (!streamer.TryValidatePublicationWorkContract(coordinate, out failure))
                return false;
            demandedWorkContracts++;
        }

        // note: Check the exact physical probe owners too; the authored terrain size need not align with the continuous cell grid.
        for (int index = 0; index < publicationProbePositions.Length; index++)
        {
            Vector3 point = publicationProbePositions[index];
            float chunkSize = Mathf.Max(1f, streamer.chunkWorldSize);
            Vector2Int coordinate = new Vector2Int(
                Mathf.FloorToInt((point.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize),
                Mathf.FloorToInt((point.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / chunkSize));
            if (!checkedDemandedOwners.Add(coordinate))
                continue;
            bool hasSnapshot = streamer.TryGetPublicationSnapshot(coordinate, out YQSemanticChunkPublicationSnapshot snapshot);
            if (!snapshot.demanded || (hasSnapshot && snapshot.traversable))
                continue;
            if (!streamer.TryValidatePublicationWorkContract(coordinate, out failure))
                return false;
            demandedWorkContracts++;
        }
        return true;
    }

    private static Terrain FindTerrainAt(Vector3 position, Transform preferredRoot = null)
    {
        Terrain[] terrains = preferredRoot != null
            ? preferredRoot.GetComponentsInChildren<Terrain>(true)
            : FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        Terrain inclusiveFallback = null;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;
            Vector3 origin = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            // note: Use the same half-open border convention as ChunkFor so a shared edge resolves to one canonical tile instead of whichever Terrain Unity enumerates first.
            if (position.x >= origin.x && position.x < origin.x + size.x &&
                position.z >= origin.z && position.z < origin.z + size.z)
                return terrain;
            if (inclusiveFallback == null && position.x >= origin.x && position.x <= origin.x + size.x &&
                position.z >= origin.z && position.z <= origin.z + size.z)
                inclusiveFallback = terrain;
        }
        return inclusiveFallback;
    }

    private static bool TryGetTerrainSurfaceY(Vector3 position, out float surfaceY)
    {
        Terrain terrain = FindTerrainAt(position);
        if (terrain == null || terrain.terrainData == null)
        {
            surfaceY = 0f;
            return false;
        }

        // note: Use the same world-space terrain authority as current-cell validation, preserving the probe's player-to-surface offset across streamed tiles.
        surfaceY = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, position);
        return !float.IsNaN(surfaceY) && !float.IsInfinity(surfaceY);
    }

    private struct Speed260DirectionSample
    {
        public bool observed, readinessRejected;
        public int frame, maskBit, sideContacts, contactId;
        public float unitySeconds, motorSeconds, speed, directionDot;
        public Vector2 expectedInput, consumedInput;
        public Vector3 start, end, requestedMove, actualMove, contactNormal;
        public CollisionFlags collisionFlags;
        public Collider contact;
    }

    private static int Speed260DirectionIndex(int completedInputFrames, int directionCount)
    {
        // note: The first frame is the abrupt seam crossing; one complete direction cycle precedes a fixed-heading exit and natural coast.
        return completedInputFrames > 0 && completedInputFrames <= directionCount ? completedInputFrames - 1 : -1;
    }

    private static double Speed260CoastTimeoutSeconds(float startSpeed, float groundBraking, float airBraking)
    {
        // note: Reject unsupported/non-braking fixture settings rather than shortening their coast or changing the production motor; 60 seconds bounds a malformed diagnostic.
        double braking = Math.Min(groundBraking, airBraking);
        double requiredSeconds = Math.Max(12d, (startSpeed - 2d) / braking + 2d);
        return startSpeed >= 0f && braking > 0d && !double.IsNaN(requiredSeconds) && requiredSeconds <= 60d
            ? requiredSeconds : double.NaN;
    }

    private static bool Speed260CoastTravelValid(float expectedMeters, float completedForwardMeters,
        float collisionToleranceMeters, float netMeters, float cellSize)
    {
        // note: Require real travel across at least a cell and agreement with integrated motor motion within accumulated capsule skin tolerance.
        return expectedMeters > 0f && !float.IsNaN(expectedMeters) && !float.IsInfinity(expectedMeters) &&
            !float.IsNaN(completedForwardMeters) && !float.IsInfinity(completedForwardMeters) &&
            collisionToleranceMeters >= 0f && collisionToleranceMeters < expectedMeters &&
            netMeters >= Mathf.Max(1f, cellSize) &&
            completedForwardMeters + collisionToleranceMeters >= expectedMeters;
    }

    private static Vector3[] Speed260TurnDirections(Vector3 forward, Vector3 right)
    {
        // note: Include the abrupt eastward boost followed by the existing eight-turn sequence; this describes input without changing it.
        return new[] { Vector3.right, -right - forward, -forward, right - forward, right,
            right + forward, forward, -right + forward, -right };
    }

    private static Bounds Speed260TurnFootprint(Vector3 start, Vector3[] directions, float maximumStep)
    {
        // note: Independent step lengths in [0, maximumStep] form interval bounds at each prefix. Their union covers variable frame durations without selecting a convenient fixed frame rate.
        Vector3 minimum = start, maximum = start, totalMinimum = start, totalMaximum = start;
        foreach (Vector3 direction in directions)
        {
            Vector3 step = direction.normalized * maximumStep;
            minimum += Vector3.Min(Vector3.zero, step);
            maximum += Vector3.Max(Vector3.zero, step);
            totalMinimum = Vector3.Min(totalMinimum, minimum);
            totalMaximum = Vector3.Max(totalMaximum, maximum);
        }
        var bounds = new Bounds();
        bounds.SetMinMax(totalMinimum, totalMaximum);
        return bounds;
    }

    private static float Speed260PreflightStep(int profile, int leg)
    {
        // note: Sample 120/60/30/20 FPS and alternating accepted extremes. These are read-only route probes, never timing or input overrides.
        float seconds = profile == 0 ? 1f / 120f : profile == 1 ? 1f / 60f : profile == 2 ? 1f / 30f :
            profile == 3 ? 0.05f : ((leg + profile) & 1) == 0 ? 1f / 120f : 0.05f;
        return 260f * seconds;
    }

    private static bool IsSpeed260TurnRouteClear(Terrain[] terrains, Vector3 start, Vector3[] directions,
        CharacterController controller, Transform ignoredRoot, Vector3 centerOffset, float radius,
        float halfSegment, float rootSurfaceOffset, RaycastHit[] hits, HashSet<Collider> blockers,
        Collider[] overlaps, out float peakHeightStep)
    {
        // note: Sweep the existing input sequence on already-materialized surfaces; unrelated terrain inside its bounding rectangle cannot reject the route.
        float slopeLimit = controller != null ? controller.slopeLimit : 45f;
        float minimumNormalY = Mathf.Max(0.65f, Mathf.Cos(slopeLimit * Mathf.Deg2Rad));
        float maximumGradient = Mathf.Sqrt(Mathf.Max(0f, 1f - minimumNormalY * minimumNormalY)) / minimumNormalY;
        peakHeightStep = 0f;
        for (int profile = 0; profile < 6; profile++)
        {
            Vector3 point = start;
            if (!TrySampleTerrainSurfaceY(terrains, point, out float initialHeight)) return false;
            point.y = initialHeight + rootSurfaceOffset;
            Vector3 initialCenter = point + centerOffset;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(initialCenter + Vector3.up * halfSegment,
                initialCenter - Vector3.up * halfSegment, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length) return false;
            for (int index = 0; index < overlapCount; index++)
            {
                Collider overlap = overlaps[index];
                if (overlap != null && !(overlap is TerrainCollider) &&
                    (ignoredRoot == null || !overlap.transform.IsChildOf(ignoredRoot))) return false;
            }
            for (int leg = 0; leg < directions.Length; leg++)
            {
                Vector3 direction = directions[leg].normalized;
                float distance = Speed260PreflightStep(profile, leg);
                int steps = Mathf.CeilToInt(distance / 2f);
                float step = distance / steps;
                for (int sample = 0; sample < steps; sample++)
                {
                    Vector3 next = point + direction * step;
                    if (!TrySampleTerrainSurfaceY(terrains, next, out float height)) return false;
                    next.y = height + rootSurfaceOffset;
                    float gradient = Mathf.Abs(next.y - point.y) / step;
                    peakHeightStep = Mathf.Max(peakHeightStep, gradient * 2f);
                    if (gradient > maximumGradient) return false;
                    Vector3 delta = next - point;
                    Vector3 center = point + centerOffset;
                    if (CountCapsuleSideBlockers(center + Vector3.up * halfSegment,
                            center - Vector3.up * halfSegment, radius, delta.normalized, delta.magnitude,
                            hits, blockers, ignoredRoot) > 0) return false;
                    point = next;
                }
            }
        }
        return true;
    }

    private static float SelectLowSlopeEastBoundaryLaneOffset(
        Vector3 origin,
        float sampleY,
        float eastBoundary,
        float chunkSize,
        float boundaryGap,
        float stagingInset,
        int requiredCellRow,
        float fallbackOffset,
        CharacterController controller,
        Vector3 playerRootPosition,
        out float selectedPeakHeightStep,
        out int selectedLaneBlockingHitCount,
        out bool selectedLaneSampled,
        out string candidateEvidence,
        out List<Vector3> stagingWaypoints)
    {
        // note: Choose a same-row lane using its live collider corridor and terrain profile so slopes or props cannot falsify the motor's first real-speed sample.
        Terrain[] terrains = FindObjectsByType<Terrain>(FindObjectsSortMode.None);
        float bestOffset = fallbackOffset;
        var candidates = new StringBuilder();
        candidateEvidence = string.Empty;
        stagingWaypoints = null;
        var clearTargets = new List<Vector3>();
        var clearOffsets = new List<float>();
        var clearPeaks = new List<float>();
        float bestPeakHeightStep = float.PositiveInfinity;
        int bestBlockingHitCount = int.MaxValue;
        float laneStep = Mathf.Max(4f, chunkSize / 16f);
        RaycastHit[] corridorHits = new RaycastHit[64];
        HashSet<Collider> blockingColliders = new HashSet<Collider>();
        Collider[] footprintOverlaps = new Collider[256];
        Vector3 turnForward = controller != null ? controller.transform.forward : Vector3.forward;
        Vector3 turnRight = controller != null ? controller.transform.right : Vector3.right;
        turnForward.y = turnRight.y = 0f;
        Vector3[] turnDirections = Speed260TurnDirections(turnForward.normalized, turnRight.normalized);
        Vector3 controllerScale = controller != null ? controller.transform.lossyScale : Vector3.one;
        Quaternion controllerRotation = controller != null ? controller.transform.rotation : Quaternion.identity;
        Vector3 controllerCenter = controller != null
            ? Vector3.Scale(controller.center, controllerScale)
            : Vector3.zero;
        Vector3 centerOffset = controllerRotation * controllerCenter;
        float capsuleRadius = controller != null
            ? controller.radius * Mathf.Max(Mathf.Abs(controllerScale.x), Mathf.Abs(controllerScale.z))
            : 0f;
        float capsuleHeight = controller != null
            ? Mathf.Max(capsuleRadius * 2f, controller.height * Mathf.Abs(controllerScale.y))
            : 0f;
        float capsuleHalfSegment = Mathf.Max(0f, capsuleHeight * 0.5f - capsuleRadius);
        Transform ignoredRoot = controller != null ? controller.transform.root : null;
        if (!TrySampleTerrainSurfaceY(terrains, playerRootPosition, out float playerSurfaceY))
        {
            selectedPeakHeightStep = 0f;
            selectedLaneBlockingHitCount = -1;
            selectedLaneSampled = false;
            return fallbackOffset;
        }
        float rootSurfaceOffset = playerRootPosition.y - playerSurfaceY;
        float corridorDistance = Mathf.Max(0f, stagingInset) + 8f;
        for (float offset = 2f; offset <= chunkSize - 2f; offset += laneStep)
        {
            Vector3 laneProbe = new Vector3(origin.x - 2f, sampleY, origin.z + offset);
            if (ChunkCoordinateAt(laneProbe, chunkSize).y != requiredCellRow)
                continue;

            bool complete = true;
            float previousHeight = 0f;
            float peakHeightStep = 0f;
            for (int sampleIndex = 0; sampleIndex <= 5; sampleIndex++)
            {
                Vector3 sample = new Vector3(
                    eastBoundary - boundaryGap + sampleIndex * 2f,
                    0f,
                    origin.z + offset);
                if (!TrySampleTerrainSurfaceY(terrains, sample, out float height))
                {
                    complete = false;
                    break;
                }
                if (sampleIndex > 0)
                    peakHeightStep = Mathf.Max(peakHeightStep, Mathf.Abs(height - previousHeight));
                previousHeight = height;
            }

            if (!complete)
            {
                candidates.Append(offset.ToString("F1", CultureInfo.InvariantCulture)).Append(":unsampled;");
                continue;
            }

            int blockingHitCount = 0;
            bool turnClear = IsSpeed260TurnRouteClear(terrains,
                    new Vector3(eastBoundary - boundaryGap, 0f, origin.z + offset), turnDirections,
                    controller, ignoredRoot, centerOffset, capsuleRadius, capsuleHalfSegment,
                    rootSurfaceOffset, corridorHits, blockingColliders, footprintOverlaps, out float turnPeakHeightStep);
            if (!turnClear)
                blockingHitCount++;
            peakHeightStep = Mathf.Max(peakHeightStep, turnPeakHeightStep);
            Vector3 target = new Vector3(eastBoundary - stagingInset, sampleY, origin.z + offset);
            bool eastClear = IsSpeed260WalkEdgeClear(terrains, target, target + Vector3.right * corridorDistance,
                controller, ignoredRoot, centerOffset, capsuleRadius, capsuleHalfSegment, rootSurfaceOffset,
                corridorHits, blockingColliders, footprintOverlaps);
            if (!eastClear) blockingHitCount++;
            if (turnClear && eastClear)
            {
                clearTargets.Add(target);
                clearOffsets.Add(offset);
                clearPeaks.Add(peakHeightStep);
            }

            candidates.Append(offset.ToString("F1", CultureInfo.InvariantCulture)).Append(":turnClear=").Append(turnClear)
                .Append(",turnPeak=").Append(turnPeakHeightStep.ToString("F3", CultureInfo.InvariantCulture))
                .Append(",eastClear=").Append(eastClear).Append(';');
            if (blockingHitCount < bestBlockingHitCount ||
                (blockingHitCount == bestBlockingHitCount && peakHeightStep < bestPeakHeightStep))
            {
                bestPeakHeightStep = peakHeightStep;
                bestBlockingHitCount = blockingHitCount;
                bestOffset = offset;
            }
        }

        // note: The test alone plans a bounded walking route over loaded colliders. This neither creates navigation authority nor prepares streaming work.
        bool EdgeClear(Vector3 from, Vector3 to) => IsSpeed260WalkEdgeClear(terrains, from, to,
            controller, ignoredRoot, centerOffset, capsuleRadius + 0.5f, capsuleHalfSegment,
            rootSurfaceOffset, corridorHits, blockingColliders, footprintOverlaps);
        stagingWaypoints = FindSpeed260StagingRoute(playerRootPosition, clearTargets, EdgeClear,
            out int chosenTarget, out int visitedNodes);
        candidates.Append("stagingSearchNodes=").Append(visitedNodes).Append(",routeFound=").Append(stagingWaypoints != null);
        if (stagingWaypoints != null)
        {
            bestOffset = clearOffsets[chosenTarget];
            bestPeakHeightStep = clearPeaks[chosenTarget];
            bestBlockingHitCount = 0;
        }
        else if (bestBlockingHitCount == 0) bestBlockingHitCount = 1;
        selectedLaneSampled = !float.IsInfinity(bestPeakHeightStep);
        candidateEvidence = candidates.ToString();
        selectedPeakHeightStep = selectedLaneSampled ? bestPeakHeightStep : 0f;
        selectedLaneBlockingHitCount = selectedLaneSampled ? bestBlockingHitCount : -1;
        return bestOffset;
    }

    private static bool IsSpeed260WalkEdgeClear(Terrain[] terrains, Vector3 from, Vector3 to,
        CharacterController controller, Transform ignoredRoot, Vector3 centerOffset, float radius,
        float halfSegment, float rootSurfaceOffset, RaycastHit[] hits, HashSet<Collider> blockers, Collider[] overlaps)
    {
        // note: Check the ground-following capsule at one-meter intervals, including initial overlaps and steep terrain; missing surfaces fail closed.
        if (controller == null || !TrySampleTerrainSurfaceY(terrains, from, out float height)) return false;
        from.y = height + rootSurfaceOffset;
        float distance = PlanarDistance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance));
        float minimumNormalY = Mathf.Max(0.65f, Mathf.Cos(controller.slopeLimit * Mathf.Deg2Rad));
        float maximumGradient = Mathf.Sqrt(1f - minimumNormalY * minimumNormalY) / minimumNormalY;
        Vector3 point = from;
        for (int sample = 0; sample <= steps; sample++)
        {
            Vector3 next = Vector3.Lerp(from, to, sample / (float)steps);
            if (!TrySampleTerrainSurfaceY(terrains, next, out height)) return false;
            next.y = height + rootSurfaceOffset;
            Vector3 center = next + centerOffset;
            int overlapCount = Physics.OverlapCapsuleNonAlloc(center + Vector3.up * halfSegment,
                center - Vector3.up * halfSegment, radius, overlaps, Physics.AllLayers, QueryTriggerInteraction.Ignore);
            if (overlapCount == overlaps.Length) return false;
            for (int index = 0; index < overlapCount; index++)
            {
                Collider overlap = overlaps[index];
                if (overlap != null && !(overlap is TerrainCollider) &&
                    (ignoredRoot == null || !overlap.transform.IsChildOf(ignoredRoot))) return false;
            }
            if (sample > 0)
            {
                if (Mathf.Abs(next.y - point.y) > maximumGradient * distance / steps) return false;
                Vector3 delta = next - point;
                center = point + centerOffset;
                if (CountCapsuleSideBlockers(center + Vector3.up * halfSegment, center - Vector3.up * halfSegment,
                    radius, delta.normalized, delta.magnitude, hits, blockers, ignoredRoot) > 0) return false;
            }
            point = next;
        }
        return true;
    }

    private static List<Vector3> FindSpeed260StagingRoute(Vector3 start, List<Vector3> targets,
        Func<Vector3, Vector3, bool> edgeClear, out int chosenTarget, out int visitedNodes)
    {
        // note: Dijkstra searches only a 128x256-meter local grid, with a 135-meter walk bound inside the existing 25 seconds at 6 m/s.
        const int width = 33, depth = 65, count = width * depth;
        const float step = 4f, maximumLength = 135f;
        chosenTarget = -1;
        visitedNodes = 0;
        if (targets == null || targets.Count == 0) return null;
        var costs = new float[count];
        var parents = new int[count];
        var closed = new bool[count];
        for (int index = 0; index < count; index++) { costs[index] = float.PositiveInfinity; parents[index] = -1; }
        int startIndex = (depth / 2) * width + width / 2;
        costs[startIndex] = 0f;
        Vector3 Point(int index) => start + new Vector3((index % width - width / 2) * step, 0f,
            (index / width - depth / 2) * step);
        for (int iteration = 0; iteration < count; iteration++)
        {
            int current = -1;
            float cheapest = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
                if (!closed[index] && costs[index] < cheapest) { current = index; cheapest = costs[index]; }
            if (current < 0 || cheapest > maximumLength) return null;
            closed[current] = true;
            visitedNodes++;
            Vector3 point = Point(current);
            for (int target = 0; target < targets.Count; target++)
            {
                float remaining = PlanarDistance(point, targets[target]);
                if (remaining > step * 1.5f || cheapest + remaining > maximumLength || !edgeClear(point, targets[target])) continue;
                var route = new List<Vector3> { targets[target] };
                for (int node = current; node != startIndex; node = parents[node]) route.Add(Point(node));
                route.Reverse();
                chosenTarget = target;
                return route;
            }
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int x = current % width + dx, z = current / width + dz;
                    if ((dx == 0 && dz == 0) || x < 0 || x >= width || z < 0 || z >= depth) continue;
                    int neighbor = z * width + x;
                    float cost = cheapest + step * (dx != 0 && dz != 0 ? Mathf.Sqrt(2f) : 1f);
                    if (closed[neighbor] || cost >= costs[neighbor] || cost > maximumLength || !edgeClear(point, Point(neighbor))) continue;
                    costs[neighbor] = cost;
                    parents[neighbor] = current;
                }
        }
        return null;
    }

    private static int CountCapsuleSideBlockers(
        Vector3 point1,
        Vector3 point2,
        float radius,
        Vector3 direction,
        float distance,
        RaycastHit[] hits,
        HashSet<Collider> blockingColliders,
        Transform ignoredRoot)
    {
        // note: Count distinct physical side obstacles while ignoring ground contacts and the authoritative player's own colliders.
        if (hits == null || blockingColliders == null || radius <= 0f || distance <= 0.01f)
            return 0;
        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            radius,
            direction,
            hits,
            distance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        blockingColliders.Clear();
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            RaycastHit hit = hits[hitIndex];
            Collider collider = hit.collider;
            if (collider == null || hit.distance <= 0.02f ||
                (ignoredRoot != null && collider.transform.IsChildOf(ignoredRoot)) ||
                Vector3.Dot(hit.normal, Vector3.up) >= 0.65f)
                continue;
            blockingColliders.Add(collider);
        }
        return blockingColliders.Count;
    }

    private static bool TrySampleTerrainSurfaceY(Terrain[] terrains, Vector3 position, out float surfaceY)
    {
        // note: Use the current TerrainData surfaces only; an unsampled lane is never treated as flatter than a real one.
        Terrain inclusiveFallback = null;
        for (int index = 0; terrains != null && index < terrains.Length; index++)
        {
            Terrain terrain = terrains[index];
            TerrainCollider collider = terrain != null ? terrain.GetComponent<TerrainCollider>() : null;
            if (terrain == null || !terrain.isActiveAndEnabled || terrain.terrainData == null ||
                collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;
            Vector3 terrainOrigin = terrain.transform.position;
            Vector3 terrainSize = terrain.terrainData.size;
            if (position.x >= terrainOrigin.x && position.x < terrainOrigin.x + terrainSize.x &&
                position.z >= terrainOrigin.z && position.z < terrainOrigin.z + terrainSize.z)
            {
                surfaceY = YQGeneratedWorldTerrain.SampleWorldHeight(terrain, position);
                return !float.IsNaN(surfaceY) && !float.IsInfinity(surfaceY);
            }
            if (inclusiveFallback == null &&
                position.x >= terrainOrigin.x && position.x <= terrainOrigin.x + terrainSize.x &&
                position.z >= terrainOrigin.z && position.z <= terrainOrigin.z + terrainSize.z)
                inclusiveFallback = terrain;
        }
        if (inclusiveFallback != null)
        {
            surfaceY = YQGeneratedWorldTerrain.SampleWorldHeight(inclusiveFallback, position);
            return !float.IsNaN(surfaceY) && !float.IsInfinity(surfaceY);
        }
        surfaceY = 0f;
        return false;
    }

    private static bool IsTerrainReadyForSettle(YQPlayerFollowingSemanticChunkStreamer streamer, bool requireRequiredCoverage, out string terrainFailure)
    {
        // note: Initial/far/rebound checkpoints require the full acceptance neighborhood; ordinary movement only needs its current published tile.
        if (streamer == null)
        {
            terrainFailure = "streamer is null";
            return false;
        }
        return requireRequiredCoverage
            ? streamer.TryValidateRequiredCoverage(out terrainFailure)
            : streamer.TryValidateCurrentTerrain(out terrainFailure);
    }

    private static IEnumerator WaitForCondition(Func<bool> condition, float timeout, Action<bool> result)
    {
        // note: Observation deadlines use real elapsed time, including stalls, rather than a scaled or synthetic simulation clock.
        double deadline = Time.realtimeSinceStartupAsDouble + timeout;
        while (Time.realtimeSinceStartupAsDouble < deadline && !condition())
            yield return null;
        result(condition());
    }

    private static YQStreamedFeatureOverlayTarget FindActiveFeatureOverlayTarget()
    {
        YQStreamedFeatureOverlayTarget[] targets =
            FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        YQStreamedFeatureOverlayTarget persistedFallback = null;
        YQStreamedFeatureOverlayTarget preferredOrigin = null;
        YQStreamedFeatureOverlayTarget nearestActive = null;
        float nearestActiveDistanceSquared = float.MaxValue;
        Transform activePlayer = YQInvestorPlayerMotor.ActiveMotor != null
            ? YQInvestorPlayerMotor.ActiveMotor.transform
            : null;
        for (int index = 0; index < targets.Length; index++)
        {
            YQStreamedFeatureOverlayTarget target = targets[index];
            if (target == null || string.IsNullOrWhiteSpace(target.featureId))
                continue;
            if (target.gameObject.activeInHierarchy)
            {
                // note: Prefer the pinned origin feature so save/reload/rebind validates one deterministic, always-published target instead of a distance-dependent site.
                if (string.Equals(target.featureId, "origin_vey_witch_house", StringComparison.OrdinalIgnoreCase))
                    preferredOrigin = target;
                else
                {
                    // note: Prefer the nearest active generated feature so the rebind proof stays inside the current published frontier rather than choosing an unloaded distant provider.
                    float distanceSquared = activePlayer != null
                        ? (target.transform.position - activePlayer.position).sqrMagnitude
                        : 0f;
                    if (nearestActive == null || distanceSquared < nearestActiveDistanceSquared)
                    {
                        nearestActive = target;
                        nearestActiveDistanceSquared = distanceSquared;
                    }
                }
                continue;
            }
            // note: Repeated verification may intentionally leave the prior real target tombstoned; reuse it so the next idempotent overlay round trip remains production-backed.
            persistedFallback ??= target;
        }
        return preferredOrigin ?? nearestActive ?? persistedFallback;
    }

    private static YQStreamedFeatureOverlayTarget FindFeatureOverlayTarget(string featureId)
    {
        if (string.IsNullOrWhiteSpace(featureId))
            return null;
        YQStreamedFeatureOverlayTarget[] targets =
            FindObjectsByType<YQStreamedFeatureOverlayTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        YQStreamedFeatureOverlayTarget activeMatch = null;
        for (int index = 0; index < targets.Length; index++)
        {
            YQStreamedFeatureOverlayTarget target = targets[index];
            if (target != null && string.Equals(target.featureId, featureId, StringComparison.Ordinal))
            {
                // note: Prefer an inactive matching target so a durable tombstone is validated on the rebuilt feature instead of being masked by an active duplicate during rebind.
                if (!target.gameObject.activeSelf)
                    return target;
                activeMatch ??= target;
            }
        }
        return activeMatch;
    }

    private static string BuildChunkSignature(GeneratedWorldPlanRecord plan)
    {
        if (plan == null || plan.semanticChunks == null)
            return string.Empty;
        StringBuilder signature = new StringBuilder();
        for (int i = 0; i < plan.semanticChunks.Count; i++)
        {
            GeneratedSemanticChunkRecord record = plan.semanticChunks[i];
            if (record == null)
                continue;
            // note: Persist every deterministic continuation contract so save/reload verification catches lost terrain, biome, route, or water authority rather than only matching chunk IDs.
            signature.Append(record.chunkId).Append('|').Append(record.worldSeed).Append('|')
                .Append(record.chunkX).Append('|').Append(record.chunkZ).Append('|')
                .Append(record.deterministicSeed).Append('|').Append(record.lifecycleState).Append('|')
                .Append(record.parentRegionId).Append('|').Append(record.biome).Append('|')
                .Append(record.continuationSchemaVersion).Append('|').Append(record.seamFingerprint).Append('|')
                .Append(record.lastVisitedUnix).Append('|');
            AppendStringList(signature, record.featureIds);
            if (record.sites != null)
            {
                for (int siteIndex = 0; siteIndex < record.sites.Count; siteIndex++)
                {
                    GeneratedSemanticSiteRecord site = record.sites[siteIndex];
                    if (site == null)
                    {
                        signature.Append("<null-site>;");
                        continue;
                    }
                    signature.Append(site.siteId).Append('|').Append(site.semanticKind).Append('|')
                        .Append(site.assetSlot).Append('|').Append(site.biome).Append('|')
                        .Append(site.lifecycleState).Append('|')
                        .Append(site.ownerCellId).Append('|').Append(site.isOwnerCell).Append('|')
                        .Append(site.worldX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(site.worldZ.ToString("R", CultureInfo.InvariantCulture)).Append('|');
                    AppendStringList(signature, site.memberCellIds);
                    signature.Append(';');
                }
            }
            AppendStringList(signature, record.borderContracts);
            AppendFloatList(signature, record.biomeWeights);
            AppendStringList(signature, record.persistentDeltaIds);
            if (record.edgeContracts != null)
            {
                for (int edgeIndex = 0; edgeIndex < record.edgeContracts.Count; edgeIndex++)
                {
                    GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[edgeIndex];
                    if (edge == null)
                    {
                        signature.Append("<null-edge>;");
                        continue;
                    }
                    signature.Append(edge.edge).Append('|').Append(edge.canonicalKey).Append('|')
                        .Append(edge.contractVersion).Append('|').Append(edge.sampleCount).Append('|')
                        .Append(edge.contractFingerprint).Append('|');
                    AppendFloatList(signature, edge.terrainHeights);
                    AppendFloatList(signature, edge.biomeWeights);
                    AppendPortalList(signature, edge.routePortals);
                    AppendPortalList(signature, edge.waterPortals);
                }
            }
            signature.Append(';');
        }
        return signature.ToString();
    }

    private static bool TryValidateBiomeTransition(
        YQContinuousWorldCellAuthority authority,
        Terrain terrain,
        out string failure,
        out float maximumDelta)
    {
        failure = string.Empty;
        maximumDelta = 0f;
        if (authority == null || terrain == null || terrain.terrainData == null)
        {
            failure = "biome authority or terrain is unavailable";
            return false;
        }
        Vector3 origin = terrain.transform.position;
        Vector3 size = terrain.terrainData.size;
        // note: Macro biome fields intentionally transition over hundreds of metres, so inspect a broad world-space window instead of only the authored tile.
        float minimumX = origin.x - 4096f;
        float maximumX = origin.x + 4096f;
        float minimumZ = origin.z - 4096f;
        float maximumZ = origin.z + 4096f;
        bool foundDifferentDominants = false;
        for (float z = minimumZ; z <= maximumZ && !foundDifferentDominants; z += 64f)
        {
            for (float x = minimumX; x <= maximumX - 32f; x += 32f)
            {
                string firstBiome = authority.ResolveDominantBiome(x, z);
                string secondBiome = authority.ResolveDominantBiome(x + 32f, z);
                if (string.Equals(firstBiome, secondBiome, StringComparison.Ordinal))
                    continue;
                foundDifferentDominants = true;
                List<float> firstWeights = authority.SampleBiomeWeights(x + 8f, z);
                List<float> secondWeights = authority.SampleBiomeWeights(x + 24f, z);
                for (int index = 0; index < Mathf.Min(firstWeights.Count, secondWeights.Count); index++)
                    maximumDelta = Mathf.Max(maximumDelta, Mathf.Abs(firstWeights[index] - secondWeights[index]));
                break;
            }
        }
        if (!foundDifferentDominants)
        {
            failure = "the probe region did not contain two distinct dominant biome influences";
            return false;
        }
        if (maximumDelta > 0.30f)
        {
            failure = "biome weights changed too sharply across an eight-metre sample";
            return false;
        }
        return true;
    }

    private static bool TrySampleAlphaBoundaryDelta(Terrain origin, Terrain extension, out float delta)
    {
        delta = float.PositiveInfinity;
        if (origin == null || extension == null || origin.terrainData == null || extension.terrainData == null ||
            origin.terrainData.alphamapResolution < 1 || extension.terrainData.alphamapResolution < 1)
            return false;
        int originX = origin.terrainData.alphamapResolution - 1;
        int extensionZ = Mathf.Clamp(extension.terrainData.alphamapResolution / 2, 0, extension.terrainData.alphamapResolution - 1);
        // note: Map the sampled extension row through world space so an edge comparison uses the same physical Z on both terrains.
        float worldZ = extension.transform.position.z + extension.terrainData.size.z * extensionZ / (float)Mathf.Max(1, extension.terrainData.alphamapResolution - 1);
        int originZ = Mathf.Clamp(
            Mathf.RoundToInt((worldZ - origin.transform.position.z) / Mathf.Max(1f, origin.terrainData.size.z) * Mathf.Max(1, origin.terrainData.alphamapResolution - 1)),
            0,
            origin.terrainData.alphamapResolution - 1);
        float[,,] originAlpha = origin.terrainData.GetAlphamaps(originX, originZ, 1, 1);
        float[,,] extensionAlpha = extension.terrainData.GetAlphamaps(0, extensionZ, 1, 1);
        int layerCount = Mathf.Min(originAlpha.GetLength(2), extensionAlpha.GetLength(2));
        if (layerCount == 0)
            return false;
        delta = 0f;
        for (int layer = 0; layer < layerCount; layer++)
            delta = Mathf.Max(delta, Mathf.Abs(originAlpha[0, 0, layer] - extensionAlpha[0, 0, layer]));
        return delta <= 0.08f;
    }

    private static string DescribeAlphaBoundary(Terrain origin, Terrain extension)
    {
        if (origin == null || extension == null || origin.terrainData == null || extension.terrainData == null)
            return "terrain-data-unavailable";
        int originResolution = Mathf.Max(1, origin.terrainData.alphamapResolution);
        int extensionResolution = Mathf.Max(1, extension.terrainData.alphamapResolution);
        int extensionZ = Mathf.Clamp(extensionResolution / 2, 0, extensionResolution - 1);
        float worldZ = extension.transform.position.z + extension.terrainData.size.z * extensionZ / (float)Mathf.Max(1, extensionResolution - 1);
        int originZ = Mathf.Clamp(Mathf.RoundToInt((worldZ - origin.transform.position.z) / Mathf.Max(1f, origin.terrainData.size.z) * (originResolution - 1)), 0, originResolution - 1);
        float[,,] originAlpha = origin.terrainData.GetAlphamaps(originResolution - 1, originZ, 1, 1);
        float[,,] extensionAlpha = extension.terrainData.GetAlphamaps(0, extensionZ, 1, 1);
        int layerCount = Mathf.Min(originAlpha.GetLength(2), extensionAlpha.GetLength(2));
        float originTotal = 0f;
        float extensionTotal = 0f;
        StringBuilder layers = new StringBuilder();
        for (int layer = 0; layer < layerCount; layer++)
        {
            originTotal += originAlpha[0, 0, layer];
            extensionTotal += extensionAlpha[0, 0, layer];
            if (layer > 0)
                layers.Append('|');
            layers.Append(layer.ToString(CultureInfo.InvariantCulture));
            layers.Append('=');
            layers.Append(originAlpha[0, 0, layer].ToString("0.000", CultureInfo.InvariantCulture));
            layers.Append("/");
            layers.Append(extensionAlpha[0, 0, layer].ToString("0.000", CultureInfo.InvariantCulture));
        }
        return "worldZ=" + worldZ.ToString("0.000", CultureInfo.InvariantCulture) +
            ";originTotal=" + originTotal.ToString("0.000", CultureInfo.InvariantCulture) +
            ";extensionTotal=" + extensionTotal.ToString("0.000", CultureInfo.InvariantCulture) +
            ";layers=" + layers;
    }

    private static bool TryValidateGenerationOrderIndependence(
        GeneratedWorldPlanRecord plan,
        YQContinuousWorldCellAuthority authority,
        float cellSize,
        out string failure)
    {
        failure = string.Empty;
        if (plan == null || authority == null)
        {
            failure = "world plan or continuation authority is unavailable";
            return false;
        }
        Vector2Int[] firstOrder =
        {
            // note: Exercise the first external cardinal ring, negative-origin ring, and diagonal corners around the authored 0..7 grid.
            new Vector2Int(8, 0), new Vector2Int(0, 8),
            new Vector2Int(-1, 0), new Vector2Int(0, -1),
            new Vector2Int(8, 8), new Vector2Int(-1, 8),
            new Vector2Int(8, -1), new Vector2Int(-1, -1),
            // note: Include a second-ring east cell so order independence is tested beyond the immediate seam.
            new Vector2Int(9, 0)
        };
        Vector2Int[] secondOrder =
        {
            new Vector2Int(9, 0), new Vector2Int(-1, -1),
            new Vector2Int(8, -1), new Vector2Int(-1, 8),
            new Vector2Int(8, 8), new Vector2Int(0, -1),
            new Vector2Int(-1, 0), new Vector2Int(0, 8),
            new Vector2Int(8, 0)
        };
        Dictionary<Vector2Int, string> signatures = new Dictionary<Vector2Int, string>();
        for (int index = 0; index < firstOrder.Length; index++)
        {
            GeneratedSemanticChunkRecord record = BuildOrderProbeRecord(firstOrder[index]);
            authority.PopulateCellRecord(record, firstOrder[index], cellSize, plan);
            signatures[firstOrder[index]] = BuildChunkSignature(new GeneratedWorldPlanRecord
            {
                worldSeed = plan.worldSeed,
                semanticChunks = new List<GeneratedSemanticChunkRecord> { record }
            });
        }
        for (int index = 0; index < secondOrder.Length; index++)
        {
            GeneratedSemanticChunkRecord record = BuildOrderProbeRecord(secondOrder[index]);
            authority.PopulateCellRecord(record, secondOrder[index], cellSize, plan);
            string signature = BuildChunkSignature(new GeneratedWorldPlanRecord
            {
                worldSeed = plan.worldSeed,
                semanticChunks = new List<GeneratedSemanticChunkRecord> { record }
            });
            if (!signatures.TryGetValue(secondOrder[index], out string expected) ||
                !string.Equals(expected, signature, StringComparison.Ordinal))
            {
                failure = "cell " + secondOrder[index] + " changed when generated in a different order";
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateDistantProceduralSynthesis(
        GeneratedWorldPlanRecord plan,
        YQContinuousWorldCellAuthority authority,
        Terrain terrain,
        float cellSize,
        out string failure,
        out float minimumRelief,
        out float maximumRelief,
        out int biomeCount,
        out int roadCellCount,
        out int riverCellCount,
        out int landmarkCellCount)
    {
        failure = string.Empty;
        minimumRelief = float.MaxValue;
        maximumRelief = 0f;
        biomeCount = 0;
        roadCellCount = 0;
        riverCellCount = 0;
        landmarkCellCount = 0;
        if (plan == null || authority == null)
        {
            failure = "world plan or continuation authority is unavailable";
            return false;
        }

        // note: Sample several independent cells well beyond the authored 1024 metre terrain, including cardinal, diagonal, and regional-anchor coordinates.
        Vector2Int[] samples =
        {
            new Vector2Int(12, 2), new Vector2Int(16, 2), new Vector2Int(20, 2), new Vector2Int(24, 2),
            new Vector2Int(-8, 2), new Vector2Int(2, -8), new Vector2Int(12, 12), new Vector2Int(17, 12),
            new Vector2Int(22, 12), new Vector2Int(17, 17)
        };
        HashSet<string> biomes = new HashSet<string>(StringComparer.Ordinal);
        int reliefCells = 0;
        float terrainHeight = terrain != null && terrain.terrainData != null
            ? Mathf.Max(1f, terrain.terrainData.size.y)
            : YQGeneratedWorldTerrain.TerrainHeight;
        for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
        {
            Vector2Int coordinate = samples[sampleIndex];
            float cellMinimum = float.MaxValue;
            float cellMaximum = float.MinValue;
            float minX = YQContinuousWorldFeatureAuthority.WorldGridOrigin + coordinate.x * cellSize;
            float minZ = YQContinuousWorldFeatureAuthority.WorldGridOrigin + coordinate.y * cellSize;
            for (int z = 0; z <= 4; z++)
            {
                for (int x = 0; x <= 4; x++)
                {
                    float worldX = minX + cellSize * x / 4f;
                    float worldZ = minZ + cellSize * z / 4f;
                    float height = authority.SampleHeightNormalized(worldX, worldZ) * terrainHeight;
                    cellMinimum = Mathf.Min(cellMinimum, height);
                    cellMaximum = Mathf.Max(cellMaximum, height);
                }
            }
            float relief = cellMaximum - cellMinimum;
            minimumRelief = Mathf.Min(minimumRelief, relief);
            maximumRelief = Mathf.Max(maximumRelief, relief);
            if (relief >= 1f)
                reliefCells++;
            biomes.Add(authority.ResolveDominantBiome(minX + cellSize * 0.5f, minZ + cellSize * 0.5f));

            // note: Generate a detached semantic record twice so local feature identity is proven without mutating the accepted world plan.
            GeneratedSemanticChunkRecord first = BuildOrderProbeRecord(coordinate);
            GeneratedSemanticChunkRecord second = BuildOrderProbeRecord(coordinate);
            authority.PopulateCellRecord(first, coordinate, cellSize, plan);
            authority.PopulateCellRecord(second, coordinate, cellSize, plan);
            if (!string.Equals(BuildChunkSignature(new GeneratedWorldPlanRecord
                {
                    worldSeed = plan.worldSeed,
                    semanticChunks = new List<GeneratedSemanticChunkRecord> { first }
                }), BuildChunkSignature(new GeneratedWorldPlanRecord
                {
                    worldSeed = plan.worldSeed,
                    semanticChunks = new List<GeneratedSemanticChunkRecord> { second }
                }), StringComparison.Ordinal))
            {
                failure = "far cell " + coordinate + " changed between identical local synthesis calls";
                return false;
            }
            if (YQContinuousWorldFeatureAuthority.TryGetRoadPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                roadCellCount++;
            if (YQContinuousWorldFeatureAuthority.TryGetRiverPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                riverCellCount++;
            string landmarkId = YQContinuousWorldCellAuthority.BuildLandmarkFeatureId(coordinate);
            if (first.featureIds.Contains(landmarkId) && first.sites.Exists(site => site != null && string.Equals(site.siteId, landmarkId, StringComparison.Ordinal)))
                landmarkCellCount++;
        }

        // note: Sweep a bounded far-cell lattice for corridor intersections because a fixed sample cell may legitimately be quiet wilderness.
        roadCellCount = 0;
        riverCellCount = 0;
        for (int z = -24; z <= 24; z += 4)
        {
            for (int x = -24; x <= 24; x += 4)
            {
                Vector2Int coordinate = new Vector2Int(x, z);
                if (YQContinuousWorldFeatureAuthority.TryGetRoadPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                    roadCellCount++;
                if (YQContinuousWorldFeatureAuthority.TryGetRiverPoints(plan.worldSeed, coordinate, cellSize, terrain, out _, out _))
                    riverCellCount++;
            }
        }
        biomeCount = biomes.Count;
        if (reliefCells < 2)
        {
            failure = "distant cells did not produce independent terrain relief";
            return false;
        }
        if (biomeCount < 2)
        {
            failure = "distant cells did not produce distinct regional biome identity";
            return false;
        }
        if (roadCellCount == 0 || riverCellCount == 0)
        {
            failure = "distant local structural synthesis did not produce both road and river candidates";
            return false;
        }
        // note: Accepted V2 plans may intentionally leave distant cells as empty wilderness; only the legacy fallback requires a synthetic landmark witness.
        bool acceptedSemanticPlan = plan.spatialPlanV2 != null;
        if (landmarkCellCount == 0 && !acceptedSemanticPlan)
        {
            failure = "distant local synthesis did not originate an independent landmark";
            return false;
        }
        return true;
    }

    private static bool TryValidateCanonicalChunkIdentity(GeneratedWorldPlanRecord plan, out string failure)
    {
        failure = string.Empty;
        if (plan == null || plan.semanticChunks == null)
        {
            failure = "semantic chunk plan is unavailable";
            return false;
        }
        HashSet<string> coordinates = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < plan.semanticChunks.Count; index++)
        {
            GeneratedSemanticChunkRecord record = plan.semanticChunks[index];
            if (record == null)
                continue;
            string coordinate = record.chunkX + ":" + record.chunkZ;
            if (!coordinates.Add(coordinate))
            {
                failure = "duplicate coordinate " + coordinate;
                return false;
            }
            if (string.IsNullOrWhiteSpace(record.chunkId) || !ids.Add(record.chunkId))
            {
                failure = "duplicate or missing chunk identity at " + coordinate;
                return false;
            }
        }
        return true;
    }

    private static GeneratedSemanticChunkRecord BuildOrderProbeRecord(Vector2Int coordinate)
    {
        // note: Order probes use fresh records so no previous cell mutation can make the comparison pass accidentally.
        return new GeneratedSemanticChunkRecord
        {
            chunkId = "order-probe:" + coordinate.x + ":" + coordinate.y,
            chunkX = coordinate.x,
            chunkZ = coordinate.y,
            lifecycleState = YQSemanticChunkLifecycle.SemanticallyPlanned.ToString()
        };
    }

    private static bool TryValidateAdjacentContracts(
        GeneratedWorldPlanRecord plan,
        Vector2Int centre,
        int radius,
        out string failure,
        out float maximumHeightDelta)
    {
        failure = string.Empty;
        maximumHeightDelta = 0f;
        if (plan == null || plan.semanticChunks == null)
        {
            failure = "semantic chunk plan is unavailable";
            return false;
        }
        for (int z = -radius; z <= radius; z++)
        for (int x = -radius; x <= radius; x++)
        {
            Vector2Int current = new Vector2Int(centre.x + x, centre.y + z);
            if (!TryFindChunkRecord(plan, current, out GeneratedSemanticChunkRecord currentRecord))
            {
                failure = "missing semantic record " + current;
                return false;
            }
            if (x < radius && TryFindChunkRecord(plan, new Vector2Int(current.x + 1, current.y), out GeneratedSemanticChunkRecord eastRecord))
            {
                if (!CompareAdjacentEdges(currentRecord, "east", eastRecord, "west", ref maximumHeightDelta, out failure))
                    return false;
            }
            if (z < radius && TryFindChunkRecord(plan, new Vector2Int(current.x, current.y + 1), out GeneratedSemanticChunkRecord northRecord))
            {
                if (!CompareAdjacentEdges(currentRecord, "north", northRecord, "south", ref maximumHeightDelta, out failure))
                    return false;
            }
        }
        return true;
    }

    private static bool TryFindChunkRecord(GeneratedWorldPlanRecord plan, Vector2Int coordinate, out GeneratedSemanticChunkRecord record)
    {
        if (plan?.semanticChunks != null)
            for (int index = 0; index < plan.semanticChunks.Count; index++)
            {
                GeneratedSemanticChunkRecord candidate = plan.semanticChunks[index];
                if (candidate != null && candidate.chunkX == coordinate.x && candidate.chunkZ == coordinate.y)
                {
                    record = candidate;
                    return true;
                }
            }
        record = null;
        return false;
    }

    private static bool CompareAdjacentEdges(
        GeneratedSemanticChunkRecord firstRecord,
        string firstEdgeName,
        GeneratedSemanticChunkRecord secondRecord,
        string secondEdgeName,
        ref float maximumHeightDelta,
        out string failure)
    {
        failure = string.Empty;
        GeneratedSemanticChunkEdgeContractRecord first = FindEdge(firstRecord, firstEdgeName);
        GeneratedSemanticChunkEdgeContractRecord second = FindEdge(secondRecord, secondEdgeName);
        if (first == null || second == null)
        {
            failure = "missing shared edge contract between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        if (!string.Equals(first.canonicalKey, second.canonicalKey, StringComparison.Ordinal) ||
            !string.Equals(first.contractVersion, second.contractVersion, StringComparison.Ordinal) ||
            first.sampleCount != second.sampleCount)
        {
            failure = "shared edge identity mismatch between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        if (!CompareFloatLists(first.terrainHeights, second.terrainHeights, 0.0001f, ref maximumHeightDelta) ||
            !CompareFloatLists(first.biomeWeights, second.biomeWeights, 0.0001f, ref maximumHeightDelta))
        {
            failure = "shared edge samples diverged between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        if (!ComparePortals(first.routePortals, second.routePortals, firstEdgeName, secondEdgeName) ||
            !ComparePortals(first.waterPortals, second.waterPortals, firstEdgeName, secondEdgeName))
        {
            failure = "shared edge portals diverged between " + firstRecord.chunkId + " and " + secondRecord.chunkId;
            return false;
        }
        return true;
    }

    private static GeneratedSemanticChunkEdgeContractRecord FindEdge(GeneratedSemanticChunkRecord record, string edgeName)
    {
        if (record?.edgeContracts == null)
            return null;
        for (int index = 0; index < record.edgeContracts.Count; index++)
        {
            GeneratedSemanticChunkEdgeContractRecord edge = record.edgeContracts[index];
            if (edge != null && string.Equals(edge.edge, edgeName, StringComparison.OrdinalIgnoreCase))
                return edge;
        }
        return null;
    }

    private static bool CompareFloatLists(List<float> first, List<float> second, float tolerance, ref float maximumDelta)
    {
        if (first == null || second == null || first.Count != second.Count)
            return false;
        for (int index = 0; index < first.Count; index++)
        {
            if (float.IsNaN(first[index]) || float.IsInfinity(first[index]) ||
                float.IsNaN(second[index]) || float.IsInfinity(second[index]))
                return false;
            float delta = Mathf.Abs(first[index] - second[index]);
            maximumDelta = Mathf.Max(maximumDelta, delta);
            if (delta > tolerance)
                return false;
        }
        return true;
    }

    private static bool ComparePortals(
        List<GeneratedSemanticChunkPortalRecord> first,
        List<GeneratedSemanticChunkPortalRecord> second,
        string firstEdgeName,
        string secondEdgeName)
    {
        if (first == null || second == null || first.Count != second.Count)
        {
            // note: Keep the shared-border failure actionable by recording portal counts from both sides.
            Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Portal count mismatch " + firstEdgeName + "/" + secondEdgeName + " left=" + (first != null ? first.Count : -1) + " right=" + (second != null ? second.Count : -1));
            // note: Include the first portal payload so a one-sided boundary crossing can be repaired at the shared feature resolver.
            if (first != null && first.Count > 0 && first[0] != null)
                Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Left portal detail id=" + first[0].portalId + " feature=" + first[0].featureId + " edgeT=" + first[0].edgeT + " pos=" + first[0].worldX + "," + first[0].worldZ + " order=" + first[0].order);
            if (second != null && second.Count > 0 && second[0] != null)
                Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Right portal detail id=" + second[0].portalId + " feature=" + second[0].featureId + " edgeT=" + second[0].edgeT + " pos=" + second[0].worldX + "," + second[0].worldZ + " order=" + second[0].order);
            return false;
        }
        for (int index = 0; index < first.Count; index++)
        {
            GeneratedSemanticChunkPortalRecord left = first[index];
            GeneratedSemanticChunkPortalRecord right = second[index];
            if (left == null || right == null)
            {
                if (left != right)
                {
                    Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Portal mismatch index=" + index + " left=" + (left != null ? left.portalId : "<null>") + " right=" + (right != null ? right.portalId : "<null>"));
                    return false;
                }
                continue;
            }
            if (!string.Equals(left.portalId, right.portalId, StringComparison.Ordinal) ||
                !string.Equals(left.featureId, right.featureId, StringComparison.Ordinal) ||
                !string.Equals(left.kind, right.kind, StringComparison.Ordinal) ||
                !string.Equals(left.edge, firstEdgeName, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(right.edge, secondEdgeName, StringComparison.OrdinalIgnoreCase) ||
                !PortalFloatsFinite(left) || !PortalFloatsFinite(right) ||
                Mathf.Abs(left.edgeT - right.edgeT) > 0.0001f ||
                Mathf.Abs(left.worldX - right.worldX) > 0.0001f ||
                Mathf.Abs(left.worldZ - right.worldZ) > 0.0001f ||
                Mathf.Abs(left.tangentX - right.tangentX) > 0.0001f ||
                Mathf.Abs(left.tangentZ - right.tangentZ) > 0.0001f ||
                Mathf.Abs(left.width - right.width) > 0.0001f ||
                Mathf.Abs(left.depth - right.depth) > 0.0001f ||
                Mathf.Abs(left.surfaceElevation - right.surfaceElevation) > 0.0001f ||
                Mathf.Abs(left.bedElevation - right.bedElevation) > 0.0001f ||
                Mathf.Abs(left.flowX - right.flowX) > 0.0001f ||
                Mathf.Abs(left.flowZ - right.flowZ) > 0.0001f ||
                left.order != right.order ||
                !string.Equals(left.upstreamId, right.upstreamId, StringComparison.Ordinal) ||
                !string.Equals(left.downstreamId, right.downstreamId, StringComparison.Ordinal))
            {
                Debug.LogWarning("[YQSemanticChunkRuntimeVerification] Portal mismatch index=" + index + " left=" + left.portalId + " right=" + right.portalId + " leftPos=" + left.worldX + "," + left.worldZ + " rightPos=" + right.worldX + "," + right.worldZ);
                return false;
            }
        }
        return true;
    }

    private static bool PortalFloatsFinite(GeneratedSemanticChunkPortalRecord portal)
    {
        return portal != null &&
            !float.IsNaN(portal.edgeT) && !float.IsInfinity(portal.edgeT) &&
            !float.IsNaN(portal.worldX) && !float.IsInfinity(portal.worldX) &&
            !float.IsNaN(portal.worldZ) && !float.IsInfinity(portal.worldZ) &&
            !float.IsNaN(portal.tangentX) && !float.IsInfinity(portal.tangentX) &&
            !float.IsNaN(portal.tangentZ) && !float.IsInfinity(portal.tangentZ) &&
            !float.IsNaN(portal.width) && !float.IsInfinity(portal.width) &&
            !float.IsNaN(portal.depth) && !float.IsInfinity(portal.depth) &&
            !float.IsNaN(portal.surfaceElevation) && !float.IsInfinity(portal.surfaceElevation) &&
            !float.IsNaN(portal.bedElevation) && !float.IsInfinity(portal.bedElevation) &&
            !float.IsNaN(portal.flowX) && !float.IsInfinity(portal.flowX) &&
            !float.IsNaN(portal.flowZ) && !float.IsInfinity(portal.flowZ);
    }

    private static void AppendStringList(StringBuilder signature, System.Collections.Generic.List<string> values)
    {
        signature.Append('[');
        if (values != null)
            for (int index = 0; index < values.Count; index++)
                signature.Append(values[index] ?? string.Empty).Append('\u001f');
        signature.Append(']');
    }

    private static void AppendFloatList(StringBuilder signature, System.Collections.Generic.List<float> values)
    {
        signature.Append('[');
        if (values != null)
            for (int index = 0; index < values.Count; index++)
                signature.Append(values[index].ToString("R", CultureInfo.InvariantCulture)).Append('\u001f');
        signature.Append(']');
    }

    private static void AppendPortalList(StringBuilder signature, System.Collections.Generic.List<GeneratedSemanticChunkPortalRecord> portals)
    {
        signature.Append('[');
        if (portals != null)
            for (int index = 0; index < portals.Count; index++)
            {
                GeneratedSemanticChunkPortalRecord portal = portals[index];
                if (portal == null)
                {
                    signature.Append("<null-portal>");
                }
                else
                {
                    signature.Append(portal.portalId).Append('|').Append(portal.featureId).Append('|')
                        .Append(portal.kind).Append('|').Append(portal.edge).Append('|')
                        .Append(portal.edgeT.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.worldX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.worldZ.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.tangentX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.tangentZ.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.width.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.depth.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.surfaceElevation.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.bedElevation.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.flowX.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.flowZ.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                        .Append(portal.order).Append('|').Append(portal.upstreamId).Append('|').Append(portal.downstreamId);
                }
                signature.Append('\u001e');
            }
        signature.Append(']');
    }

    private static string CurrentReportFileName()
    {
        if (_focusedR1UnloadRevisitWitnessOnly)
            return "G08_R1_UnloadRevisitWitness.md";
        if (_focusedR1PublicationWitnessOnly)
            return "G08_R1_PublicationRecoveryWitness.md";
        return _focusedSpeed260MotorWitnessOnly
            ? "G08_R2_Speed260MotorWitness.md"
            : "YQSemanticChunkRuntimeVerification.md";
    }

    private static void AppendNestedFailureToReport(string failureText)
    {
        // note: Preserve a yielded gate exception and publish a complete terminal failure when the frame driver had to dispose the root iterator.
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(failureText))
            return;
        string reportFileName = CurrentReportFileName();
        string reportPath = Path.Combine(projectRoot, "Logs", reportFileName);
        if (!File.Exists(reportPath))
            return;
        string normalizedFailure = failureText.Replace('\r', ' ').Replace('\n', ' ');
        // note: A root iterator exception may already have published FAIL from its finally; append its missing reason without duplicating terminal fields.
        string terminalFields = _terminalReportWritten ? string.Empty :
            "- result: FAIL" + Environment.NewLine +
            "- finishedUtc: " + DateTime.UtcNow.ToString("O") + Environment.NewLine;
        File.AppendAllText(reportPath, "- failure: " + normalizedFailure + Environment.NewLine + terminalFields);
        _terminalReportWritten = true;
        // note: Request Edit Mode only after the failure evidence is durable, matching the normal terminal report contract.
        File.WriteAllText(Path.Combine(projectRoot, "Temp", "YQ_STREAMER_RUNTIME_STOP.request"), "fail");
        Debug.LogError("[YQSemanticChunkRuntimeVerification] FAIL report written to Logs/" + reportFileName + ": " + normalizedFailure);
    }

    private static void AppendVerifierStatusToReport(string status)
    {
        // note: Keep verifier liveness evidence in the same report as traversal results so a sentinel-only run identifies whether the host survived its first frame.
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(status))
            return;
        string reportPath = Path.Combine(projectRoot, "Logs", CurrentReportFileName());
        if (File.Exists(reportPath))
            File.AppendAllText(reportPath, "- verifierStatus: " + status + Environment.NewLine);
    }

    private static void AppendExecutionIdentity(StringBuilder report)
    {
        // note: Record the loaded verifier assembly separately from source files so a stale Unity compile cannot be mistaken for current disk source.
        Assembly assembly = typeof(YQSemanticChunkRuntimeVerification).Assembly;
        string assemblyPath = string.Empty;
        try
        {
            assemblyPath = assembly.Location;
        }
        catch (NotSupportedException)
        {
            // note: Some player backends do not expose a physical assembly path; the module identity remains useful.
        }
        report.AppendLine("- executingAssembly: name=" + ReportValue(assembly.GetName().Name) +
            " mvid=" + assembly.ManifestModule.ModuleVersionId.ToString("D") +
            " path=" + ReportValue(assemblyPath));
        AppendFileSha256(report, "executingAssemblySha256", assemblyPath);

        string generatedSourceDirectory = Path.Combine(Application.dataPath, "Assets", "Scripts", "Generated");
        AppendFileSha256(report, "verifierSourceSha256",
            Path.Combine(generatedSourceDirectory, "YQSemanticChunkRuntimeVerification.cs"));
        AppendFileSha256(report, "streamerSourceSha256",
            Path.Combine(generatedSourceDirectory, "YQPlayerFollowingSemanticChunkStreamer.cs"));
    }

    private static void AppendWorldIdentity(StringBuilder report, WorldState world, YQProfileSaveSystem profileSave)
    {
        // note: Bind later runtime measurements to the live paired profile/world save and report router selection without mutating either authority.
        YQWorldIdentityRecord identity = world != null ? world.worldIdentity : null;
        GeneratedWorldPlanRecord plan = world != null ? world.generatedWorldPlan : null;
        string activeProfileId = profileSave != null ? profileSave.ActiveProfileId : string.Empty;
        string ownerProfileId = identity != null ? identity.ownerProfileId : string.Empty;
        bool ownerMatchesActiveProfile = !string.IsNullOrWhiteSpace(activeProfileId) &&
            string.Equals(ownerProfileId, activeProfileId, StringComparison.OrdinalIgnoreCase);
        report.AppendLine("- profileWorldIdentity: activeProfileId=" + ReportValue(activeProfileId) +
            " ownerProfileId=" + ReportValue(ownerProfileId) +
            " ownerMatchesActiveProfile=" + ownerMatchesActiveProfile);
        report.AppendLine("- worldPlanIdentity: stateSchema=" + (world != null ? world.schemaVersion.ToString(CultureInfo.InvariantCulture) : "<missing>") +
            " planSchema=" + ReportValue(plan != null ? plan.schemaVersion : string.Empty) +
            " worldId=" + ReportValue(identity != null ? identity.worldId : string.Empty) +
            " worldSeed=" + ReportValue(plan != null ? plan.worldSeed : string.Empty));

        YQSpatialPlanAuthority authority;
        string routeReason;
        bool routeResolved = YQSpatialPlanVersionRouter.TryResolveActive(plan, out authority, out routeReason);
        report.AppendLine("- activeSpatialPlanResolution: resolved=" + routeResolved +
            " authority=" + (routeResolved ? authority.ToString() : "None") +
            " reason=" + ReportValue(routeReason));
        report.AppendLine("- persistedSpatialArtifactIdentity: id=" + ReportValue(identity != null ? identity.selectedSpatialArtifactId : string.Empty) +
            " version=" + ReportValue(identity != null ? identity.selectedSpatialArtifactVersion : string.Empty));

        GeneratedSpatialWorldPlanRecord v1 = plan != null ? plan.spatialPlan : null;
        GeneratedSpatialWorldPlanV2Record v2 = plan != null ? plan.spatialPlanV2 : null;
        report.AppendLine("- persistedV1Artifact: schema=" + ReportValue(v1 != null ? v1.schemaVersion : string.Empty) +
            " generation=" + ReportValue(v1 != null ? v1.generationVersion : string.Empty) +
            " seed=" + ReportValue(v1 != null ? v1.worldSeed : string.Empty) +
            " semanticFingerprint=" + ReportValue(v1 != null ? v1.semanticFingerprint : string.Empty));
        report.AppendLine("- acceptedV2Artifact: state=" + (v2 != null ? v2.acceptanceState.ToString() : "<missing>") +
            " schema=" + ReportValue(v2 != null ? v2.schemaVersion : string.Empty) +
            " generation=" + ReportValue(v2 != null ? v2.generationVersion : string.Empty) +
            " validation=" + ReportValue(v2 != null ? v2.validationVersion : string.Empty) +
            " seed=" + ReportValue(v2 != null ? v2.worldSeed : string.Empty) +
            " contentHash=" + ReportValue(v2 != null ? v2.contentHash : string.Empty) +
            " validatedContentHash=" + ReportValue(v2 != null ? v2.validatedContentHash : string.Empty));
    }

    private static void AppendFileSha256(StringBuilder report, string fieldName, string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            report.AppendLine("- " + fieldName + ": unavailable");
            return;
        }

        try
        {
            using (FileStream stream = File.OpenRead(path))
            using (SHA256 sha256 = SHA256.Create())
            {
                string hash = BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
                report.AppendLine("- " + fieldName + ": path=" + ReportValue(path) + " sha256=" + hash);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException ||
                                           exception is CryptographicException || exception is ArgumentException ||
                                           exception is NotSupportedException || exception is System.Security.SecurityException)
        {
            // note: Provenance capture is diagnostic-only and must never change the runtime verification verdict.
            report.AppendLine("- " + fieldName + ": unavailable reason=" + exception.GetType().Name);
        }
    }

    private static string ReportValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "<empty>";
        return value.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ').Trim();
    }

    private static void WriteReport(StringBuilder report, bool success, bool terminal = true, bool inconclusive = false)
    {
        if (_terminalReportWritten)
            return;
        if (terminal)
            report.AppendLine("- finishedUtc: " + DateTime.UtcNow.ToString("O"));
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
        if (string.IsNullOrWhiteSpace(projectRoot))
            return;
        string logsDirectory = Path.Combine(projectRoot, "Logs");
        Directory.CreateDirectory(logsDirectory);
        string reportFileName = CurrentReportFileName();
        string reportPath = Path.Combine(logsDirectory, reportFileName);
        if (!ReferenceEquals(_activeReport, report))
        {
            // note: A newly armed run starts a new receipt; subsequent phase flushes append only their new evidence.
            File.WriteAllText(reportPath, report.ToString());
            _activeReport = report;
        }
        else if (report.Length > _writtenReportCharacters)
        {
            File.AppendAllText(reportPath, report.ToString(_writtenReportCharacters, report.Length - _writtenReportCharacters));
        }
        _writtenReportCharacters = report.Length;
        if (terminal)
        {
            _terminalReportWritten = true;
            // note: Keep the report, editor stop receipt, and Console aligned; completing the matrix with missing input evidence is inconclusive.
            string outcome = !success ? "FAIL" : inconclusive ? "INCONCLUSIVE" : "PASS";
            File.WriteAllText(Path.Combine(projectRoot, "Temp", "YQ_STREAMER_RUNTIME_STOP.request"), outcome.ToLowerInvariant());
            Debug.Log("[YQSemanticChunkRuntimeVerification] " + outcome + " report written to Logs/" + reportFileName);
        }
    }
}
