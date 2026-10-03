using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// note: Explicit editor-only evidence capture renders the actual generated world without moving the player or changing its save.
public static class YQWorldPresentationReview
{
    private static bool _cpuCaptureActive;
    private static double _cpuCaptureStarted;
    private static int _cpuFirstGameplayFrame;
    private static bool _cpuFocusedAtStart;
    private static readonly List<Unity.Profiling.ProfilerRecorder> CpuRecorders = new List<Unity.Profiling.ProfilerRecorder>();
    private static readonly List<string> CpuRecorderNames = new List<string>();
    private static readonly List<string> CpuRecorderUnits = new List<string>();

    [UnityEditor.MenuItem("YourQuest/Verification/Capture 15 Seconds of Streaming CPU")]
    public static void CaptureStreamingCpu()
    {
        // note: A bounded explicit capture uses Unity's real CPU samples, with no input, scheduler, save, or player-state changes.
        if (!Application.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || Time.timeScale <= 0f || _cpuCaptureActive)
            return;
        YQSemanticChunkRuntimeVerification.PrimeProfilerMarkerForCpuCapture();
        // note: Direct recorders do not require an attached Profiler window or a selected recording target; discover the actual available metrics before subscribing.
        var wanted = new HashSet<string>(new[] { "Main Thread", "Render Thread", "PlayerLoop", "EditorLoop", "GC.Alloc", "GC.Collect", "BehaviourUpdate", "CoroutinesDelayedCalls", "Camera.Render", "RenderLoop.Draw", "Gfx.WaitForPresentOnGfxThread", "WaitForTargetFPS", "Update.ScriptRunBehaviourUpdate", "Update.ScriptRunDelayedTasks", "PreLateUpdate.ScriptRunBehaviourLateUpdate", "PostLateUpdate.FinishFrameRendering", "YQPlayerFollowingSemanticChunkStreamer.Update", "YQPlayerFollowingSemanticChunkStreamer.LateUpdate", "YQPlayerFollowingSemanticChunkStreamer.TerrainAppearanceSlice", "YQInvestorPlayerMotor.Update", "YQSemanticChunkRuntimeVerification.LateUpdate" }, StringComparer.Ordinal);
        var handles = new List<Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle>();
        Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetAvailable(handles);
        CpuRecorders.Clear();
        CpuRecorderNames.Clear();
        CpuRecorderUnits.Clear();
        foreach (var handle in handles)
        {
            var description = Unity.Profiling.LowLevel.Unsafe.ProfilerRecorderHandle.GetDescription(handle);
            if (!wanted.Contains(description.Name) && !description.Name.StartsWith("YQ", StringComparison.Ordinal))
                continue;
            if (CpuRecorders.Count >= 48)
                break;
            var recorder = new Unity.Profiling.ProfilerRecorder(handle, 1024,
                Unity.Profiling.ProfilerRecorderOptions.StartImmediately |
                Unity.Profiling.ProfilerRecorderOptions.WrapAroundWhenCapacityReached |
                Unity.Profiling.ProfilerRecorderOptions.SumAllSamplesInFrame);
            CpuRecorders.Add(recorder);
            CpuRecorderNames.Add(description.Name);
            CpuRecorderUnits.Add(description.UnitType.ToString());
        }
        _cpuFirstGameplayFrame = Time.frameCount;
        _cpuFocusedAtStart = Application.isFocused;
        _cpuCaptureStarted = UnityEditor.EditorApplication.timeSinceStartup;
        _cpuCaptureActive = true;
        UnityEditor.EditorApplication.update += FinishStreamingCpuCapture;
        Debug.Log("[YQStreamingCpu] Started a 15-second ordinary runtime capture.");
    }

    private static void FinishStreamingCpuCapture()
    {
        if (!_cpuCaptureActive || (Application.isPlaying && UnityEditor.EditorApplication.timeSinceStartup - _cpuCaptureStarted < 15d))
            return;
        // note: Stop and restore instrumentation before reading samples; aggregation overhead is outside the measured window.
        UnityEditor.EditorApplication.update -= FinishStreamingCpuCapture;
        _cpuCaptureActive = false;
        var report = new StringBuilder("# G08-R1 streaming CPU capture\n\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
        report.AppendLine("Evidence: direct Unity Profiler recorders in the Editor. Inclusive metrics overlap and must not be added together. Deep profiling and quality settings were not changed.");
        report.AppendLine("Focus at start/end: " + _cpuFocusedAtStart + "/" + Application.isFocused + "; ending timeScale: " + Time.timeScale);
        // note: Leaving Play Mode resets the frame counter; an interrupted capture must not masquerade as a complete gameplay measurement.
        bool completeWindow = Application.isPlaying && Time.timeScale > 0f &&
            Time.frameCount >= _cpuFirstGameplayFrame && UnityEditor.EditorApplication.timeSinceStartup - _cpuCaptureStarted >= 15d;
        report.AppendLine("Real elapsed seconds: " + (UnityEditor.EditorApplication.timeSinceStartup - _cpuCaptureStarted).ToString("F3") +
            "; gameplay frames: " + (Application.isPlaying && Time.frameCount >= _cpuFirstGameplayFrame
                ? (Time.frameCount - _cpuFirstGameplayFrame).ToString() : "unavailable after Play Mode exit"));
        try
        {
            report.AppendLine("\n| Metric | Unit | Samples | Mean | Maximum |\n| --- | --- | ---: | ---: | ---: |");
            int measured = 0;
            // note: Stop every recorder before aggregation so the receipt-writing pass cannot contaminate later metrics.
            for (int metric = 0; metric < CpuRecorders.Count; metric++)
            {
                if (CpuRecorders[metric].Valid)
                    CpuRecorders[metric].Stop();
            }
            for (int metric = 0; metric < CpuRecorders.Count; metric++)
            {
                var recorder = CpuRecorders[metric];
                var samples = new List<Unity.Profiling.ProfilerRecorderSample>();
                recorder.CopyTo(samples);
                double total = 0d, maximum = 0d;
                foreach (var sample in samples) { total += sample.Value; maximum = Math.Max(maximum, sample.Value); }
                bool time = CpuRecorderUnits[metric] == "TimeNanoseconds";
                double divisor = time ? 1000000d : 1d;
                if (samples.Count > 0) measured++;
                report.AppendLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2} | {3:F3} | {4:F3} |", CpuRecorderNames[metric], time ? "ms" : CpuRecorderUnits[metric],
                    samples.Count, samples.Count > 0 ? total / samples.Count / divisor : 0d, maximum / divisor));
            }
            report.AppendLine(!completeWindow ? "\nResult: INTERRUPTED (partial samples only; not a complete gameplay measurement)." :
                measured > 0 ? "\nResult: CAPTURED (not a performance acceptance PASS)." : "\nResult: UNAVAILABLE (no recorder samples).");
        }
        catch (Exception exception)
        {
            report.AppendLine("Result: UNAVAILABLE\n" + exception);
        }
        finally
        {
            foreach (var recorder in CpuRecorders) recorder.Dispose();
            CpuRecorders.Clear();
        }
        // note: Each capture gets its own timestamped receipt so follow-up measurements never replace existing evidence.
        string capturePath = Path.Combine("Docs", "G08_R1_Streaming_CPU_" +
            DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss_fff", System.Globalization.CultureInfo.InvariantCulture) + ".md");
        Directory.CreateDirectory("Docs");
        File.WriteAllText(capturePath, report.ToString());
        Debug.Log("[YQStreamingCpu] Capture receipt: " + capturePath);
    }

    [UnityEditor.MenuItem("YourQuest/Verification/Capture Live Landscape Repair Review")]
    public static void CaptureLandscapeRepairReview()
    {
        Camera source = Camera.main;
        if (!Application.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || source == null) return;
        string prefix = "Landscape_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        Directory.CreateDirectory("Logs/PresentationReview");
        var report = new StringBuilder("# Live landscape repair review\n\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
        report.AppendLine("runtimeMvid=" + typeof(YQContinuousWorldFeatureAuthority).Assembly.ManifestModule.ModuleVersionId);
        report.AppendLine("Evidence: auxiliary views of the current live world; player and accepted records untouched. This is not a throughput test.");
        var filters = UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None);
        var selected = new Dictionary<string, MeshFilter>();
        foreach (MeshFilter filter in filters)
        {
            bool river = filter.name.StartsWith("ContinuousRiver_", StringComparison.Ordinal);
            bool road = filter.name.StartsWith("ContinuousRoad_", StringComparison.Ordinal);
            if ((!river && !road) || filter.sharedMesh == null || !filter.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled) continue;
            Vector3 center = renderer.bounds.center;
            bool outside = Mathf.Abs(center.x) > 520f || Mathf.Abs(center.z) > 520f;
            string key = (outside ? "continuation_" : "origin_") + (river ? "river" : "path");
            if (!selected.TryGetValue(key, out var previous) ||
                (center - source.transform.position).sqrMagnitude < (previous.GetComponent<Renderer>().bounds.center - source.transform.position).sqrMagnitude)
                selected[key] = filter;
        }
        GameObject host = new GameObject("LandscapeReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.cullingMask &= ~(1 << 5);
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        try
        {
            Render(camera, source.transform.position, source.transform.position + source.transform.forward * 20f, prefix + "_player");
            foreach (var selection in selected)
            {
                MeshFilter filter = selection.Value;
                Vector3 target = filter.GetComponent<Renderer>().bounds.center;
                Vector3 eye = target + new Vector3(18f, 18f, -24f);
                Terrain eyeTerrain = ResolveReviewTerrain(eye, streamer);
                if (eyeTerrain != null) eye.y = Mathf.Max(eye.y, YQGeneratedWorldTerrain.SampleWorldHeight(eyeTerrain, eye) + 8f);
                Render(camera, eye, target, prefix + "_" + selection.Key);
                int samples = 0, missing = 0, buried = 0, floating = 0;
                float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(vertex);
                    Terrain terrain = ResolveReviewTerrain(point, streamer);
                    if (terrain == null) { missing++; continue; }
                    float separation = point.y - YQGeneratedWorldTerrain.SampleWorldHeight(terrain, point);
                    samples++;
                    minimum = Mathf.Min(minimum, separation);
                    maximum = Mathf.Max(maximum, separation);
                    if (separation < -0.02f) buried++;
                    if (selection.Key.EndsWith("path", StringComparison.Ordinal) && separation > 0.2f) floating++;
                }
                report.AppendLine(selection.Key + " object=" + filter.name + " target=" + target + " samples=" + samples +
                    " missingTerrain=" + missing + " buried=" + buried + " floating=" + floating +
                    " minSeparation=" + minimum + " maxSeparation=" + maximum);
                AppendMaterialEvidence(filter.GetComponent<Renderer>(), report);
            }
            foreach (string key in new[] { "origin_river", "origin_path", "continuation_river", "continuation_path" })
                if (!selected.ContainsKey(key)) report.AppendLine(key + " NOT VERIFIED: no currently loaded surface");
            Render(camera, source.transform.position + new Vector3(-65f, 85f, -65f), source.transform.position + source.transform.forward * 70f, prefix + "_landscape");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
        File.WriteAllText("outputs/World_Readiness_20261002/" + prefix + ".md", report.ToString());
        Debug.Log("[YQWorldPresentationReview] Landscape capture " + prefix);
    }

    [UnityEditor.MenuItem("YourQuest/Verification/Capture Live Streamed Water Appearance")]
    public static void CaptureLiveStreamedWaterAppearance()
    {
        // note: Compare ordinary streamed water with the origin presentation using a temporary review camera; player, terrain, quality, materials, and shader time are untouched.
        Camera source = Camera.main;
        if (!Application.isPlaying || Time.timeScale <= 0f || source == null)
            return;
        Directory.CreateDirectory("Logs/PresentationReview");
        MeshFilter streamed = null, origin = null;
        float closestStream = float.PositiveInfinity, closestOrigin = float.PositiveInfinity;
        foreach (MeshFilter filter in UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if (filter.sharedMesh == null || !filter.TryGetComponent<Renderer>(out var renderer) || !renderer.enabled)
                continue;
            bool isStream = filter.name.StartsWith("ContinuousRiver_", StringComparison.Ordinal);
            bool isOrigin = filter.name == "CompiledHydrologySurface";
            if (!isStream && !isOrigin)
                continue;
            float distance = (renderer.bounds.center - source.transform.position).sqrMagnitude;
            if (isStream && distance < closestStream) { streamed = filter; closestStream = distance; }
            if (isOrigin && distance < closestOrigin) { origin = filter; closestOrigin = distance; }
        }
        var host = new GameObject("YQ_StreamedWaterEvidenceCamera") { hideFlags = HideFlags.HideAndDontSave };
        Camera camera = host.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        camera.cullingMask &= ~(1 << 5);
        camera.farClipPlane = 800f;
        string captureId = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string capturePrefix = "G08_R3_" + captureId + "_";
        string reportPath = Path.Combine("Docs", "G08_R3_Live_Water_Appearance_" + captureId + ".md");
        YQPlayerFollowingSemanticChunkStreamer streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        var report = new StringBuilder("# Live water appearance\n\nUTC: " + DateTime.UtcNow.ToString("O") + "\n");
        float firstShaderTime = Time.time;
        double firstTime = UnityEditor.EditorApplication.timeSinceStartup;
        void Capture(MeshFilter filter, string name)
        {
            if (filter == null) { report.AppendLine(name + ": no active surface available"); return; }
            Vector3[] vertices = filter.sharedMesh.vertices;
            int left = Mathf.Clamp((vertices.Length / 4) * 2, 0, vertices.Length - 2);
            // note: Aim at the deepest actual channel sample while recording dry samples too; a bank in front of the camera must not hide the surface being reviewed.
            float deepest = float.NegativeInfinity, minimumDepth = float.PositiveInfinity;
            int wetSamples = 0, drySamples = 0, missingOwnerSamples = 0;
            for (int sample = 0; sample + 1 < vertices.Length; sample += 2)
            {
                Vector3 center = filter.transform.TransformPoint((vertices[sample] + vertices[sample + 1]) * .5f);
                Terrain terrain = ResolveReviewTerrain(center, streamer);
                if (terrain == null)
                {
                    missingOwnerSamples++;
                    continue;
                }
                // note: Sample the streamed cell selected by its owner instead of the first overlapping active Terrain.
                float depth = center.y - YQGeneratedWorldTerrain.SampleWorldHeight(terrain, center);
                if (depth > .1f) wetSamples++; else drySamples++;
                minimumDepth = Mathf.Min(minimumDepth, depth);
                if (depth > deepest) { deepest = depth; left = sample; }
            }
            report.AppendLine(name + " channel samples: wet=" + wetSamples + " dry=" + drySamples + " missingOwner=" + missingOwnerSamples +
                " minDepth=" + minimumDepth + " maxDepth=" + deepest);
            Vector3 a = filter.transform.TransformPoint(vertices[left]);
            Vector3 b = filter.transform.TransformPoint(vertices[left + 1]);
            Vector3 target = (a + b) * .5f;
            Terrain ownerTerrain = ResolveReviewTerrain(target, streamer);
            Vector3 side = (b - a).normalized;
            if (side.sqrMagnitude < .5f) side = Vector3.right;
            Vector3 eye = target + side * Mathf.Max(8f, Vector3.Distance(a, b) * .8f) + Vector3.up * 6f;
            Terrain eyeTerrain = ResolveReviewTerrain(eye, streamer);
            if (eyeTerrain != null)
            {
                eye.y = Mathf.Max(eye.y, YQGeneratedWorldTerrain.SampleWorldHeight(eyeTerrain, eye) + 5f);
            }
            Render(camera, eye, target, capturePrefix + name);
            Renderer renderer = filter.GetComponent<Renderer>();
            AppendMaterialEvidence(renderer, report);
            Material material = renderer.sharedMaterial;
            report.AppendLine(name + ": surface=" + filter.name + " target=" + target +
                " flowSpeed=" + (material.HasProperty("_FlowSpeed") ? material.GetFloat("_FlowSpeed") : 0f) +
                " normal=" + (material.HasProperty("_BumpMap") && material.GetTexture("_BumpMap") != null ? material.GetTexture("_BumpMap").name : "none") +
                " vertexColours=" + filter.sharedMesh.colors.Length);
            if (ownerTerrain != null && ownerTerrain.terrainData != null)
            {
                Vector3 terrainPosition = ownerTerrain.GetPosition();
                Vector3 terrainSize = ownerTerrain.terrainData.size;
                float groundY = YQGeneratedWorldTerrain.SampleWorldHeight(ownerTerrain, target);
                GeneratedWorldPlanRecord plan = WorldStateManager.Instance != null && WorldStateManager.Instance.State != null
                    ? WorldStateManager.Instance.State.generatedWorldPlan
                    : null;
                float cellSize = Mathf.Max(32f, plan != null && plan.semanticAuthority != null
                    ? plan.semanticAuthority.cellSizeMeters
                    : 128f);
                Vector2Int cell = new Vector2Int(
                    Mathf.FloorToInt((target.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / cellSize),
                    Mathf.FloorToInt((target.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / cellSize));
                if (TryFindNearestAcceptedWater(target, out string waterId, out YQHydrologyKindV2 waterKind,
                        out float surfaceNormalized, out float nominalDepth, out float effectiveDepth, out float distance))
                {
                    float acceptedSurfaceY = terrainPosition.y + terrainSize.y * surfaceNormalized;
                    float expectedDepthInWorldUnits = effectiveDepth * terrainSize.y / YQGeneratedWorldTerrain.TerrainHeight;
                    report.AppendLine(name + " ownerTerrain=" + ownerTerrain.name + " cell=" + cell +
                        " terrainPosition=" + terrainPosition + " terrainSize=" + terrainSize +
                        " groundY=" + groundY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " meshY=" + target.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " meshAboveGround=" + (target.y - groundY).ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " acceptedWater=" + waterId + " kind=" + waterKind + " distance=" + distance.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " acceptedSurfaceY=" + acceptedSurfaceY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " nominalDepth=" + nominalDepth.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " effectiveCarveDepth=" + effectiveDepth.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " expectedCenterlineDepth=" + expectedDepthInWorldUnits.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " bedClearanceError=" + (groundY - (acceptedSurfaceY - expectedDepthInWorldUnits)).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                {
                    report.AppendLine(name + " ownerTerrain=" + ownerTerrain.name + " cell=" + cell +
                        " groundY=" + groundY.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " meshY=" + target.y.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                        " acceptedWater=unresolved");
                }
            }
            else
            {
                report.AppendLine(name + " ownerTerrain=unavailable target=" + target);
            }
        }
        try { Capture(origin, "origin_water_a"); Capture(streamed, "streamed_water_a"); }
        catch { UnityEngine.Object.DestroyImmediate(host); throw; }
        void Finish()
        {
            if (Application.isPlaying && UnityEditor.EditorApplication.timeSinceStartup - firstTime < 1d)
                return;
            UnityEditor.EditorApplication.update -= Finish;
            try
            {
                if (Application.isPlaying && camera != null)
                {
                    Capture(origin, "origin_water_b");
                    Capture(streamed, "streamed_water_b");
                    report.AppendLine("Shader time advanced: " + (Time.time - firstShaderTime).ToString("F3") + " seconds; final timeScale=" + Time.timeScale);
                }
                report.AppendLine("Evidence images require visual inspection; capture alone is not appearance acceptance.");
                report.AppendLine("\nCapture images: Logs/PresentationReview/" + capturePrefix + "{origin_water_a,streamed_water_a,origin_water_b,streamed_water_b}.png");
                File.WriteAllText(reportPath, report.ToString());
                Debug.Log("[YQWorldPresentationReview] Live water receipt: " + reportPath);
            }
            finally { if (host != null) UnityEngine.Object.DestroyImmediate(host); }
        }
        UnityEditor.EditorApplication.update += Finish;
    }

    private static Terrain ResolveReviewTerrain(Vector3 worldPosition, YQPlayerFollowingSemanticChunkStreamer streamer)
    {
        if (streamer != null && streamer.TryGetGeneratedTerrainAt(worldPosition, out Terrain ownerTerrain))
            return ownerTerrain;

        // note: Select the smallest containing fallback when the streamer has no published owner for a diagnostic sample.
        Terrain best = null;
        float bestArea = float.PositiveInfinity;
        foreach (Terrain candidate in Terrain.activeTerrains)
        {
            if (candidate == null || candidate.terrainData == null)
                continue;
            Vector3 position = candidate.GetPosition();
            Vector3 size = candidate.terrainData.size;
            if (worldPosition.x < position.x || worldPosition.x > position.x + size.x ||
                worldPosition.z < position.z || worldPosition.z > position.z + size.z)
                continue;
            float area = size.x * size.z;
            if (area < bestArea)
            {
                best = candidate;
                bestArea = area;
            }
        }
        return best;
    }

    // note: Join one live ribbon sample to its accepted record so clearance diagnostics compare persisted depth against the correct cell surface.
    private static bool TryFindNearestAcceptedWater(
        Vector3 worldPosition,
        out string hydrologyId,
        out YQHydrologyKindV2 kind,
        out float surfaceNormalized,
        out float nominalDepth,
        out float effectiveDepth,
        out float distance)
    {
        hydrologyId = string.Empty;
        kind = YQHydrologyKindV2.Unknown;
        surfaceNormalized = 0f;
        nominalDepth = 0f;
        effectiveDepth = 0f;
        distance = float.PositiveInfinity;
        GeneratedWorldPlanRecord plan = WorldStateManager.Instance != null && WorldStateManager.Instance.State != null
            ? WorldStateManager.Instance.State.generatedWorldPlan
            : null;
        if (plan == null || !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out YQPreparedSpatialMaterializationV2 prepared, out _))
            return false;

        float bestDistanceSquared = float.PositiveInfinity;
        Vector2 query = new Vector2(worldPosition.x, worldPosition.z);
        for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
        {
            YQSpatialMaterializationWaterV2 water = prepared.GetWater(waterIndex);
            int pointCount = prepared.GetWaterPointCount(waterIndex);
            if (pointCount <= 0)
                continue;
            if (pointCount == 1)
            {
                YQSpatialMaterializationWaterPointV2 point = prepared.GetWaterPoint(waterIndex, 0);
                float distanceSquared = (query - new Vector2(point.x, point.z)).sqrMagnitude;
                if (distanceSquared < bestDistanceSquared)
                {
                    bestDistanceSquared = distanceSquared;
                    hydrologyId = water.hydrologyId ?? string.Empty;
                    kind = water.kind;
                    surfaceNormalized = point.waterSurfaceNormalized;
                    nominalDepth = water.nominalDepth;
                    effectiveDepth = GetEffectiveCarveDepth(water);
                }
                continue;
            }
            for (int pointIndex = 0; pointIndex + 1 < pointCount; pointIndex++)
            {
                YQSpatialMaterializationWaterPointV2 first = prepared.GetWaterPoint(waterIndex, pointIndex);
                YQSpatialMaterializationWaterPointV2 second = prepared.GetWaterPoint(waterIndex, pointIndex + 1);
                Vector2 start = new Vector2(first.x, first.z);
                Vector2 segment = new Vector2(second.x - first.x, second.z - first.z);
                float t = segment.sqrMagnitude > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(query - start, segment) / segment.sqrMagnitude)
                    : 0f;
                Vector2 closest = start + segment * t;
                float distanceSquared = (query - closest).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared)
                    continue;
                bestDistanceSquared = distanceSquared;
                hydrologyId = water.hydrologyId ?? string.Empty;
                kind = water.kind;
                surfaceNormalized = Mathf.Lerp(first.waterSurfaceNormalized, second.waterSurfaceNormalized, t);
                nominalDepth = water.nominalDepth;
                effectiveDepth = GetEffectiveCarveDepth(water);
            }
        }
        if (float.IsInfinity(bestDistanceSquared))
            return false;
        distance = Mathf.Sqrt(bestDistanceSquared);
        return true;
    }

    private static float GetEffectiveCarveDepth(YQSpatialMaterializationWaterV2 water)
    {
        // note: Match the terrain sampler's wider river and waterfall cut so this receipt does not call intended channel depth a geometry defect.
        if (water.kind == YQHydrologyKindV2.River)
            return Mathf.Max(4.5f, water.nominalDepth);
        if (water.kind == YQHydrologyKindV2.Waterfall)
            return Mathf.Max(3.5f, water.nominalDepth);
        return Mathf.Max(0.6f, water.nominalDepth);
    }

    public static void CaptureWaterAndBridges()
    {
        // note: Capture the live confluence and each bridge variant with material provenance; shader-support counts alone missed the visible failures.
        var root = GameObject.Find("YQ_GENERATED_WORLD_RUNTIME");
        var source = Camera.main;
        GameObject generatedSourceHost = null;
        if (source == null)
            source = UnityEngine.Object.FindFirstObjectByType<Camera>();
        if (source == null && Application.isPlaying && root != null)
        {
            // note: Direct production verification can omit the presentation camera; a temporary review camera preserves visual evidence without altering gameplay ownership.
            generatedSourceHost = new GameObject("YQ_WaterBridgeSourceCamera") { hideFlags = HideFlags.HideAndDontSave };
            source = generatedSourceHost.AddComponent<Camera>();
            source.transform.SetPositionAndRotation(root.transform.position + new Vector3(0f, 120f, -160f), Quaternion.LookRotation(Vector3.forward, Vector3.up));
        }
        if (!Application.isPlaying || root == null || source == null)
            throw new InvalidOperationException("A live generated world and gameplay camera are required.");
        Directory.CreateDirectory("Logs/PresentationReview");
        var report = new StringBuilder("Captured UTC=" + DateTime.UtcNow.ToString("O") + "\nFrame=" + Time.frameCount + "\n");
        // note: Editor domain reloads clear singleton caches while the captured scene can remain present; identify that case without failing before evidence is written.
        var worldManager = WorldStateManager.Instance != null ? WorldStateManager.Instance : UnityEngine.Object.FindFirstObjectByType<WorldStateManager>();
        report.AppendLine("Seed=" + (worldManager != null && worldManager.State != null && worldManager.State.generatedWorldPlan != null
            ? worldManager.State.generatedWorldPlan.worldSeed : "unavailable after domain reload"));
        var terrain = root.GetComponentInChildren<Terrain>();
        var host = new GameObject("YQ_WaterBridgeReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = host.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        camera.cullingMask &= ~(1 << 5);
        camera.farClipPlane = 1600f;
        try
        {
            var surfaces = root.GetComponentsInChildren<MeshFilter>();
            int lakeIndex = 0;
            foreach (var lake in surfaces)
            {
                if (lake.name != "CuratedEllipticalWaterSurface" || lake.sharedMesh == null) continue;
                var lakeVertices = lake.sharedMesh.vertices;
                Vector3 center = lake.transform.TransformPoint(lakeVertices[0]);
                float maximumLevelError = 0f;
                foreach (var vertex in lakeVertices)
                    maximumLevelError = Mathf.Max(maximumLevelError, Mathf.Abs(lake.transform.TransformPoint(vertex).y - center.y));
                report.AppendLine("Lake=" + lake.transform.parent.name + " center=" + center.ToString("F3") + " maximumLevelError=" + maximumLevelError.ToString("F4"));
                AppendMaterialEvidence(lake.GetComponent<Renderer>(), report);
                foreach (var river in surfaces)
                {
                    if (river.name != "CompiledHydrologySurface" || river.sharedMesh == null || !river.transform.parent.name.Contains("River")) continue;
                    var vertices = river.sharedMesh.vertices;
                    Vector3 a = river.transform.TransformPoint((vertices[0] + vertices[1]) * .5f);
                    Vector3 b = river.transform.TransformPoint((vertices[vertices.Length - 2] + vertices[vertices.Length - 1]) * .5f);
                    Vector3 mouth = (a - center).sqrMagnitude < (b - center).sqrMagnitude ? a : b;
                    report.AppendLine("River=" + river.transform.parent.name + " mouth=" + mouth.ToString("F3") + " lakeLevelDelta=" + (mouth.y - center.y).ToString("F4"));
                    AppendMaterialEvidence(river.GetComponent<Renderer>(), report);
                    float minimumDepth = float.PositiveInfinity;
                    for (int sample = 0; sample <= 40; sample++)
                    {
                        Vector3 point = Vector3.Lerp(mouth, center, sample / 40f);
                        minimumDepth = Mathf.Min(minimumDepth, center.y - terrain.SampleHeight(point) - terrain.transform.position.y);
                    }
                    report.AppendLine("MouthToLakeMinimumTerrainDepth=" + minimumDepth.ToString("F4"));
                    Vector3 outward = mouth - center;
                    outward.y = 0f;
                    if (outward.sqrMagnitude < 1f) outward = Vector3.back * 20f;
                    outward.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, outward);
                    Vector3 target = Vector3.Lerp(mouth, center, .25f);
                    Render(camera, target + outward * 48f + side * 26f + Vector3.up * 18f, target, "confluence_" + lakeIndex);
                    Render(camera, target + Vector3.up * 115f + outward * 25f, target, "confluence_overview_" + lakeIndex++);
                }
            }
            var variants = new HashSet<string>();
            int bridgeIndex = 0;
            foreach (var bridge in root.GetComponentsInChildren<YQGeneratedRiverBridge>())
            {
                var renderers = bridge.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) { report.AppendLine("BridgeWithoutRenderers=" + bridge.name); continue; }
                Bounds bounds = renderers[0].bounds;
                var seenMaterials = new HashSet<Material>();
                report.AppendLine("Bridge=" + bridge.name);
                foreach (var renderer in renderers)
                {
                    bounds.Encapsulate(renderer.bounds);
                    foreach (var material in renderer.sharedMaterials)
                        if (seenMaterials.Add(material)) AppendMaterialEvidence(renderer, report);
                }
                // note: Capture one representative of each deterministic variant while auditing every live bridge's material slots.
                if (!variants.Add(bridge.name)) continue;
                Vector3 eye = bounds.center + new Vector3(24f, 15f, -30f);
                eye.y = Mathf.Max(eye.y, terrain.SampleHeight(eye) + terrain.transform.position.y + 4f);
                Render(camera, eye, bounds.center, "live_bridge_" + bridgeIndex);
                report.AppendLine("live_bridge_" + bridgeIndex++ + "=" + bridge.name + " bounds=" + bounds);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            if (generatedSourceHost != null)
                UnityEngine.Object.DestroyImmediate(generatedSourceHost);
            File.WriteAllText("Logs/PresentationReview/water_bridge_report.txt", report.ToString());
        }
    }

    private static void AppendMaterialEvidence(Renderer renderer, StringBuilder report)
    {
        if (renderer == null) { report.AppendLine("MissingRenderer"); return; }
        foreach (var material in renderer.sharedMaterials)
        {
            // note: Record shader compilation errors and pipeline tags alongside the exact material rather than equating a non-null slot with valid rendering.
            if (material == null) { report.AppendLine("MissingMaterial=" + renderer.name); continue; }
            var shader = material.shader;
            report.AppendLine("  Material=" + material.name + " shader=" + (shader != null ? shader.name : "NULL") +
                " pipeline=" + material.GetTag("RenderPipeline", false, "") + " queue=" + material.renderQueue +
                " usable=" + YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material) +
                " shaderErrors=" + (shader == null || UnityEditor.ShaderUtil.ShaderHasError(shader)) +
                " path=" + UnityEditor.AssetDatabase.GetAssetPath(material));
        }
    }

    public static void VerifyWaterBridgeRegressionFixtures()
    {
        // note: Exercise production mesh/material code against varied basin shapes and every bridge variant without rewriting the accepted world or imported assets.
        var report = new StringBuilder("Captured UTC=" + DateTime.UtcNow.ToString("O") + "\n");
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var buildLake = typeof(YQGeneratedWorldEnvironment).GetMethod("BuildMacroWaterEllipseMesh", flags);
        var resolveBridge = typeof(YQGeneratedRiverBridge).GetMethod("ResolveBridgeMaterial", flags);
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(256f, 140f, 256f) };
        var host = Terrain.CreateTerrainGameObject(data);
        host.SetActive(false);
        host.hideFlags = HideFlags.HideAndDontSave;
        host.transform.position = new Vector3(-2000f, 7f, -2000f);
        var terrain = host.GetComponent<Terrain>();
        try
        {
            for (int fixture = 0; fixture < 3; fixture++)
            {
                float heading = fixture * 37f * Mathf.Deg2Rad;
                var axis = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
                var side = new Vector2(-axis.y, axis.x);
                float longRadius = fixture == 0 ? 56f : fixture == 1 ? 92f : 23f;
                float shortRadius = fixture == 0 ? 56f : fixture == 1 ? 31f : 9f;
                // note: Keep the runtime constructor internal; the editor fixture alone supplies synthetic accepted basin descriptors.
                var basin = (YQGeneratedWorldTerrain.MacroWaterBasinDescriptor)Activator.CreateInstance(
                    typeof(YQGeneratedWorldTerrain.MacroWaterBasinDescriptor), System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                    null, new object[] { new Vector3(-1872f, 35f, -1872f), axis, side, longRadius, shortRadius, 35f }, null);
                var mesh = (Mesh)buildLake.Invoke(null, new object[] { terrain, basin, fixture });
                try
                {
                    float maxLevelError = 0f, maxFootprintRadius = 0f;
                    foreach (var vertex in mesh.vertices)
                    {
                        maxLevelError = Mathf.Max(maxLevelError, Mathf.Abs(vertex.y));
                        var offset = new Vector2(vertex.x, vertex.z);
                        float u = Vector2.Dot(offset, axis) / longRadius;
                        float v = Vector2.Dot(offset, side) / shortRadius;
                        maxFootprintRadius = Mathf.Max(maxFootprintRadius, Mathf.Sqrt(u * u + v * v));
                    }
                    report.AppendLine("LakeFixture=" + fixture + " heading=" + (heading * Mathf.Rad2Deg) + " levelError=" + maxLevelError + " footprint=" + maxFootprintRadius);
                    // note: The production basin uses the near-full accepted footprint so connected river mouths cannot terminate in an inset dry annulus.
                    // note: Accept the production skirt range while still rejecting a collapsed or overextended basin footprint.
                    if (maxLevelError > .0001f || maxFootprintRadius < .98f || maxFootprintRadius > 1.01f)
                        throw new InvalidOperationException("Lake surface differs from its accepted level or footprint.");
                }
                finally { UnityEngine.Object.DestroyImmediate(mesh); }
            }
            for (int variant = 0; variant < 4; variant++)
            {
                var material = (Material)resolveBridge.Invoke(null, new object[] { YQRuntimeWorldAssetRegistry.Instance, variant });
                try
                {
                    bool valid = YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material) && !UnityEditor.ShaderUtil.ShaderHasError(material.shader);
                    Texture baseMap = material.HasProperty("_BaseMap") ? material.GetTexture("_BaseMap") : null;
                    report.AppendLine("BridgeVariant=" + variant + " material=" + material.name + " shader=" + material.shader.name + " baseMap=" + (baseMap != null ? baseMap.name : "NULL") + " valid=" + valid);
                    if (!valid || baseMap == null || material.name.Contains("Fallback"))
                        throw new InvalidOperationException("Bridge variant lost its compatible authored material: " + variant);
                }
                finally { UnityEngine.Object.DestroyImmediate(material); }
            }
            report.AppendLine("PASS: three basin shapes and all four material variants. Full-world seed/reload validation remains separate.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(data);
            Directory.CreateDirectory("Logs/PresentationReview");
            File.WriteAllText("Logs/PresentationReview/water_bridge_fixtures.txt", report.ToString());
        }
    }

    public static void Capture()
    {
        var root = GameObject.Find("YQ_GENERATED_WORLD_RUNTIME");
        var terrain = root != null ? root.GetComponentInChildren<Terrain>() : null;
        var source = Camera.main;
        if (terrain == null || source == null) throw new InvalidOperationException("Generated terrain and gameplay camera required.");
        // note: Exercise threshold units at endpoints and midpoint; these checks catch the SmoothStep overload error directly.
        if (YQGeneratedWorldEnvironment.SmoothThreshold(3f, 8f, 3f) != 0f ||
            YQGeneratedWorldEnvironment.SmoothThreshold(3f, 8f, 8f) != 1f ||
            Mathf.Abs(YQGeneratedWorldEnvironment.SmoothThreshold(3f, 8f, 5.5f) - .5f) > .0001f)
            throw new InvalidOperationException("Terrain threshold regression.");
        Directory.CreateDirectory("Logs/PresentationReview");
        var report = new StringBuilder("Captured UTC: " + DateTime.UtcNow.ToString("O") + "\n");
        report.AppendLine("Seed: " + WorldStateManager.Instance.State.generatedWorldPlan.worldSeed);
        // note: Record the actual native vegetation configuration alongside visual evidence.
        report.AppendLine("Trees=" + terrain.terrainData.treeInstanceCount + ", detailLayers=" + terrain.terrainData.detailPrototypes.Length);
        foreach (var detail in terrain.terrainData.detailPrototypes)
            report.AppendLine("Detail=" + (detail.prototypeTexture != null ? detail.prototypeTexture.name : "mesh"));
        AppendWorldDressingMetrics(root, terrain, source, report);
        var host = new GameObject("YQ_EditorReviewCamera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = host.AddComponent<Camera>();
        camera.CopyFrom(source);
        camera.enabled = false;
        camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        camera.cullingMask &= ~(1 << 5);
        camera.nearClipPlane = .15f;
        camera.farClipPlane = 1600f;
        camera.fieldOfView = 60f;
        int index = 0;
        try
        {
            Render(camera, source.transform.position, source.transform.position + source.transform.forward * 15f, "player");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.name != "CompiledHydrologySurface" && filter.name != "CuratedEllipticalWaterSurface") continue;
                var vertices = filter.sharedMesh.vertices;
                if (vertices.Length < 3) continue;
                bool river = filter.name == "CompiledHydrologySurface";
                int wet = 0, dry = 0, downwardNormals = 0;
                float deepest = 0f;
                // note: Report the most obstructed water centre and normal orientation, since maximum depth alone can conceal dams or inverted shading.
                float minimumDepth = float.PositiveInfinity;
                Vector3 shallowestPoint = Vector3.zero;
                var normals = filter.sharedMesh.normals;
                foreach (var normal in normals)
                    if (filter.transform.TransformDirection(normal).y < 0f) downwardNormals++;
                Vector3 target = filter.transform.TransformPoint(vertices[0]);
                Vector3 across = Vector3.right * 10f;
                // note: Measure water-to-terrain depth along the actual mesh, retaining dry crossings as a separate count.
                int count = river ? vertices.Length / 2 : 1;
                for (int i = 0; i < count; i++)
                {
                    Vector3 a = filter.transform.TransformPoint(vertices[river ? i * 2 : 0]);
                    Vector3 b = filter.transform.TransformPoint(vertices[river ? i * 2 + 1 : 0]);
                    Vector3 center = (a + b) * .5f;
                    float depth = center.y - terrain.SampleHeight(center) - terrain.transform.position.y;
                    if (depth < minimumDepth) { minimumDepth = depth; shallowestPoint = center; }
                    if (depth > .2f) wet++; else dry++;
                    if (depth > deepest)
                    {
                        deepest = depth;
                        target = center;
                        across = river ? (b - a) : Vector3.right * 25f;
                    }
                }
                report.AppendLine(filter.sharedMesh.name + ": wetSamples=" + wet + ", dryOrCrossingSamples=" + dry + ", maximumDepth=" + deepest.ToString("F3") +
                    ", minimumDepth=" + minimumDepth.ToString("F3") + ", shallowestPoint=" + shallowestPoint + ", downwardNormals=" + downwardNormals);
                Vector3 eye = target + across.normalized * Mathf.Max(across.magnitude, 12f) + Vector3.up * 4f;
                eye.y = Mathf.Max(eye.y, terrain.SampleHeight(eye) + terrain.transform.position.y + 2f);
                Render(camera, eye, target, "water_" + index++);
                Render(camera, target + new Vector3(0, 95, -65), target, "water_overview_" + index);
                // note: Compare terrain tessellation at the exact same viewpoint without changing the saved world or its runtime quality setting.
                float pixelError = terrain.heightmapPixelError;
                try
                {
                    terrain.heightmapPixelError = 1f;
                    Render(camera, target + new Vector3(0, 95, -65), target, "water_lod1_overview_" + index);
                }
                finally { terrain.heightmapPixelError = pixelError; }
            }
            index = 0;
            foreach (Transform site in root.transform)
            {
                if (!site.name.StartsWith("CompiledSettlement") && !site.name.StartsWith("Settlement_") &&
                    !site.name.StartsWith("Origin_") && site.name != "Generated_CompiledHostileSites") continue;
                Vector3 target = site.position + Vector3.up * 3f;
                // note: Imported assembly pivots may be far from the visible building; frame its actual renderer bounds.
                var renderers = site.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) if (renderer.enabled) bounds.Encapsulate(renderer.bounds);
                    target = bounds.center;
                }
                Vector3 eye = target + new Vector3(20, 15, -25);
                eye.y = Mathf.Max(eye.y, terrain.SampleHeight(eye) + terrain.transform.position.y + 5f);
                Render(camera, eye, target, "site_" + index);
                report.AppendLine("site_" + index++ + "=" + site.name + " position=" + site.position);
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            File.WriteAllText("Logs/PresentationReview/report.txt", report.ToString());
        }
    }

    [Flags]
    private enum DressingHint
    {
        None = 0, Tree = 1, Vegetation = 2, Outskirts = 4, Roadside = 8, Shoreline = 16, LandUse = 32
    }

    private struct ReviewNode
    {
        public Transform transform;
        public Transform region;
        public Transform bridge;
        public DressingHint hints;
    }

    private static void AppendWorldDressingMetrics(GameObject root, Terrain terrain, Camera source, StringBuilder report)
    {
        // note: Traverse each hierarchy node once and count renderer components, never empty composition folders or the same descendant through multiple ancestors.
        var pending = new Stack<ReviewNode>();
        pending.Push(new ReviewNode { transform = root.transform });
        var localRenderers = new List<Renderer>();
        var populatedRegions = new HashSet<Transform>();
        var populatedBridges = new HashSet<Transform>();
        var frustum = GeometryUtility.CalculateFrustumPlanes(source);
        int meshRenderers = 0, treeRenderers = 0, vegetationRenderers = 0, outskirtsRenderers = 0;
        int roadRenderers = 0, shorelineRenderers = 0, landUseRenderers = 0, bridgeRenderers = 0;
        int nearCandidates = 0, middleCandidates = 0, distantCandidates = 0;
        Transform wilderness = root.transform.Find("Generated_Wilderness");
        while (pending.Count > 0)
        {
            ReviewNode node = pending.Pop();
            Transform item = node.transform;
            if (!item.gameObject.activeInHierarchy) continue;
            string name = item.name.ToLowerInvariant();
            if (item.parent == wilderness && item.name != "Generated_RoadsideDressing" && item.name != "Generated_ShorelineDressing")
                node.region = item;
            // note: Bridge roots live outside wilderness; match the generated bridge owner anywhere beneath the world root.
            if (item.name == "Generated_RiverBridge") node.bridge = item;
            if (name.Contains("tree")) node.hints |= DressingHint.Tree | DressingHint.Vegetation;
            if (name.Contains("vegetation") || name.Contains("bush") || name.Contains("shrub") || name.Contains("grass") || name.Contains("flower"))
                node.hints |= DressingHint.Vegetation;
            // note: Compiled settlements use a dedicated outskirts container; count that authored edge dressing in the same audit bucket as legacy outer dressing.
            if (name.Contains("outerdressing") || name.Contains("settlementoutskirts")) node.hints |= DressingHint.Outskirts;
            if (name.Contains("roadside")) node.hints |= DressingHint.Roadside;
            if (name.Contains("shoreline")) node.hints |= DressingHint.Shoreline;
            // note: The compiled land-use pass names sparse transition props explicitly so they remain visible to the acceptance report.
            if (name.Contains("districtfrontage") || name.Contains("field") || name.Contains("orchard") || name.Contains("pasture") || name.Contains("landuse") || name.Contains("transitionmarker"))
                node.hints |= DressingHint.LandUse;
            item.GetComponents(localRenderers);
            foreach (Renderer renderer in localRenderers)
            {
                if (!renderer.enabled || renderer.forceRenderingOff || renderer.bounds.size.sqrMagnitude < .000001f) continue;
                Mesh mesh = null;
                if (renderer is SkinnedMeshRenderer skinned) mesh = skinned.sharedMesh;
                else if (renderer is MeshRenderer && item.TryGetComponent<MeshFilter>(out var filter)) mesh = filter.sharedMesh;
                if (mesh == null || mesh.vertexCount == 0) continue;
                meshRenderers++;
                if (node.region != null) populatedRegions.Add(node.region);
                if (node.bridge != null) { populatedBridges.Add(node.bridge); bridgeRenderers++; }
                if ((node.hints & DressingHint.Tree) != 0) treeRenderers++;
                if ((node.hints & DressingHint.Vegetation) != 0) vegetationRenderers++;
                if ((node.hints & DressingHint.Outskirts) != 0) outskirtsRenderers++;
                if ((node.hints & DressingHint.Roadside) != 0) roadRenderers++;
                if ((node.hints & DressingHint.Shoreline) != 0) shorelineRenderers++;
                if ((node.hints & DressingHint.LandUse) != 0) landUseRenderers++;
                // note: Frustum candidates describe the gameplay viewpoint only; LOD selection and occlusion can still prevent a candidate from being drawn.
                if ((source.cullingMask & (1 << item.gameObject.layer)) == 0 || !GeometryUtility.TestPlanesAABB(frustum, renderer.bounds)) continue;
                float distanceSquared = renderer.bounds.SqrDistance(source.transform.position);
                if (distanceSquared < 25f * 25f) nearCandidates++;
                else if (distanceSquared < 100f * 100f) middleCandidates++;
                else distantCandidates++;
            }
            for (int i = 0; i < item.childCount; i++)
                pending.Push(new ReviewNode { transform = item.GetChild(i), region = node.region, bridge = node.bridge, hints = node.hints });
        }
        report.AppendLine("ObservationOnly=True, Playing=" + UnityEditor.EditorApplication.isPlaying + ", Frame=" + Time.frameCount);
        report.AppendLine("ActiveEnabledMeshRenderers=" + meshRenderers + ", WildernessRegionsWithMeshRenderers=" + populatedRegions.Count);
        report.AppendLine("NameHintMeshRenderers=tree:" + treeRenderers + ", vegetation:" + vegetationRenderers + ", outskirts:" + outskirtsRenderers + ", landUse:" + landUseRenderers + ", roadside:" + roadRenderers + ", shoreline:" + shorelineRenderers);
        report.AppendLine("GeneratedBridgeRootsWithMeshRenderers=" + populatedBridges.Count + ", BridgeMeshRenderers=" + bridgeRenderers);
        report.AppendLine("GameplayCamera=" + source.name + ", Position=" + source.transform.position + ", Forward=" + source.transform.forward);
        report.AppendLine("GameplayFrustumMeshCandidates=nearestBoundsUnder25m:" + nearCandidates + ", from25mTo100m:" + middleCandidates + ", atLeast100m:" + distantCandidates);
        report.AppendLine("RendererCountLimits=Unique components; multiple LOD meshes remain included; overlapping name hints are not semantic validation. Terrain grass/trees are reported separately. Frustum candidates are not confirmed visible pixels or composition quality.");
        report.AppendLine("TerrainDetailConfiguration=resolution:" + terrain.terrainData.detailResolution + ", distance:" + terrain.detailObjectDistance.ToString("F1") + " (configuration only, not measured performance)");
        // note: Sample the published runtime heightfield on a bounded grid so the hill repair has measurable slope evidence without mutating terrain or creating extra scene objects.
        AppendTerrainSlopeMetrics(terrain, report);
        // note: Keep untested acceptance requirements explicit; counts and render settings cannot certify a playable, richly composed, performant world.
        report.AppendLine("VisualAcceptance=UNVERIFIED: inspect captures and routes for ecological clusters, forest edges/interiors/clearings, layered meadows, shoreline vegetation, settlement land use, roadside context, natural spacing, intentional empty areas and landmark sightlines.");
        report.AppendLine("PhysicalAndNavigationAcceptance=UNVERIFIED: require same-run grounding, water/bank and crossing validation, terrain slope review, required-route traversal, and player release/playability evidence.");
        report.AppendLine("PerformanceAcceptance=UNVERIFIED: require representative traversal frame-time, memory, draw-call and collider measurements plus LOD/culling/streaming transitions; capture-camera timings cannot establish gameplay performance.");
        report.AppendLine("SeedVariationAndDeterminismAcceptance=UNVERIFIED: this report captures one world; compare multiple fresh seeds and a repeat of the same accepted save.");
    }

    private static void AppendTerrainSlopeMetrics(Terrain terrain, StringBuilder report)
    {
        if (terrain == null || terrain.terrainData == null)
        {
            report.AppendLine("TerrainSlopeMetrics=UNAVAILABLE");
            return;
        }

        const int samplesPerAxis = 33;
        var slopes = new List<float>(samplesPerAxis * samplesPerAxis);
        float maximum = 0f;
        Vector3 maximumPoint = terrain.transform.position;
        Vector3 maximumNormal = Vector3.up;
        for (int z = 0; z < samplesPerAxis; z++)
        {
            float normalizedZ = z / (float)(samplesPerAxis - 1);
            for (int x = 0; x < samplesPerAxis; x++)
            {
                float normalizedX = x / (float)(samplesPerAxis - 1);
                Vector3 normal = terrain.terrainData.GetInterpolatedNormal(normalizedX, normalizedZ);
                float slope = Vector3.Angle(normal, Vector3.up);
                slopes.Add(slope);
                if (slope > maximum)
                {
                    maximum = slope;
                    maximumPoint = terrain.transform.position + new Vector3(normalizedX * terrain.terrainData.size.x, 0f, normalizedZ * terrain.terrainData.size.z);
                    maximumPoint.y = terrain.SampleHeight(maximumPoint) + terrain.transform.position.y;
                    maximumNormal = normal;
                }
            }
        }

        slopes.Sort();
        float p95 = slopes[Mathf.Clamp(Mathf.CeilToInt(slopes.Count * .95f) - 1, 0, slopes.Count - 1)];
        int overFortyFive = 0;
        for (int index = 0; index < slopes.Count; index++)
            if (slopes[index] > 45f) overFortyFive++;

        string authorityContext = "unavailable";
        var plan = WorldStateManager.Instance != null ? WorldStateManager.Instance.State.generatedWorldPlan : null;
        if (plan != null && YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out _))
        {
            YQSpatialTerrainSampleV2 sample = prepared.SampleTerrain(maximumPoint.x, maximumPoint.z);
            authorityContext = "waterMask:" + sample.waterMask.ToString("F2") + ", routeMask:" + sample.routeMask.ToString("F2") + ", siteReserveMask:" + sample.siteReserveMask.ToString("F2") + ", caveMask:" + sample.caveMassMask.ToString("F2");
        }

        // note: Report distribution rather than declaring visual acceptance; cliffs can still be intentional at authored natural features and require route/camera review.
        report.AppendLine("TerrainSlopeMetrics=samples:" + slopes.Count + ", maximumDegrees:" + maximum.ToString("F2") + ", p95Degrees:" + p95.ToString("F2") + ", over45Degrees:" + overFortyFive + ", maximumPoint:" + maximumPoint + ", maximumNormal:" + maximumNormal + ", authorityAtMaximum=" + authorityContext);
    }

    private static void Render(Camera camera, Vector3 eye, Vector3 target, string name)
    {
        // note: Restore render state and release native buffers even if an evidence render fails.
        var previous = RenderTexture.active;
        var texture = RenderTexture.GetTemporary(1024, 640, 24, RenderTextureFormat.ARGB32);
        Texture2D pixels = null;
        try
        {
            camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            pixels = new Texture2D(1024, 640, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1024, 640), 0, 0);
            pixels.Apply();
            File.WriteAllBytes("Logs/PresentationReview/" + name + ".png", pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(texture);
            if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
        }
    }

    public static void VerifyGeneratedBridgeDeck()
    {
        // note: Exercise real imported stone geometry over multiple joined sections in isolation; all temporary geometry is removed after measuring centre and shoulder contacts.
        var host = new GameObject("YQ_BridgeDeckVerification") { hideFlags = HideFlags.HideAndDontSave };
        var road = new List<Vector3>();
        for (int i = 0; i <= 64; i++) road.Add(new Vector3(2000f, 20f + i * .1f, 2000f + i));
        try
        {
            YQGeneratedRiverBridge.Build(host.transform, road, 7f, YQRuntimeWorldAssetRegistry.Instance);
            Physics.SyncTransforms();
            var colliders = host.GetComponentsInChildren<MeshCollider>();
            float maximumDeviation = 0f;
            int missing = 0, excessive = 0;
            for (int i = 0; i <= 256; i++)
            for (int side = -1; side <= 1; side++)
            {
                float along = i * .25f;
                float expected = 20f + along * .1f + .02f;
                var ray = new Ray(new Vector3(2000f + side * .31f, expected + 2f, 2000f + along), Vector3.down);
                float top = float.NegativeInfinity;
                foreach (var collider in colliders)
                    if (collider.Raycast(ray, out var hit, 4f)) top = Mathf.Max(top, hit.point.y);
                if (float.IsNegativeInfinity(top)) { missing++; continue; }
                float deviation = Mathf.Abs(top - expected);
                maximumDeviation = Mathf.Max(maximumDeviation, deviation);
                if (deviation > .20f) excessive++;
            }
            Directory.CreateDirectory("Logs/PresentationReview");
            File.WriteAllText("Logs/PresentationReview/bridge_deck_check.txt", "Samples=771 Missing=" + missing + " ExcessiveDeviation=" + excessive + " MaximumDeviation=" + maximumDeviation.ToString("F4"));
            if (missing > 0 || excessive > 0) throw new InvalidOperationException("Generated bridge deck has gaps or excessive surface discontinuity; see bridge_deck_check.txt.");
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    public static void ReviewBridge()
    {
        // note: Preview only the exact existing bridge asset, outside the world, and destroy it after recording dimensions and deck contacts.
        const string path = "Assets/HIVEMIND/MedievalKingdom/HDRP(Default)/Art/Prefabs/SM_Bridge_Small_01.prefab";
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var instance = UnityEngine.Object.Instantiate(prefab, new Vector3(2000,0,2000), Quaternion.identity);
        var cameraHost = new GameObject("BridgeReviewCamera");
        var camera = cameraHost.AddComponent<Camera>();
        var report = new StringBuilder();
        try
        {
            YQRuntimeWorldAssetRegistry.Instance.ApplyMaterialOverrides(path,instance);
            var lod = instance.GetComponentInChildren<LODGroup>();
            lod.ForceLOD(0);
            var renderer = lod.GetLODs()[0].renderers[0];
            var filter = renderer.GetComponent<MeshFilter>();
            var collider = renderer.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            var bounds = renderer.bounds;
            report.AppendLine("Bounds=" + bounds + " mesh=" + filter.sharedMesh.name);
            bool alongZ = bounds.size.z > bounds.size.x;
            for (int i = 0; i <= 10; i++)
            {
                Vector3 position = bounds.center;
                position[alongZ ? 2 : 0] = Mathf.Lerp(bounds.min[alongZ ? 2 : 0],bounds.max[alongZ ? 2 : 0], (i+.2f)/10.4f);
                position.y = bounds.max.y+2;
                if(collider.Raycast(new Ray(position,Vector3.down),out var hit,30)) report.AppendLine(i + " deck=" + hit.point.y + " normal="+hit.normal);
            }
            Directory.CreateDirectory("Logs/PresentationReview");
            Render(camera,bounds.center+new Vector3(22,15,-25),bounds.center,"bridge_asset");
            File.WriteAllText("Logs/PresentationReview/bridge_asset.txt",report.ToString());
        }
        finally { UnityEngine.Object.DestroyImmediate(instance); UnityEngine.Object.DestroyImmediate(cameraHost); }
    }
}
