#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

// note: Explicit, bounded observation covers menus, the entire load, and released gameplay without retaining native frame history.
internal static class YQWholeSessionFrameCapture
{
    private static readonly string[] Names = { "Main Thread", "Render Thread", "PlayerLoop", "EditorLoop", "GC.Collect", "CoroutinesDelayedCalls", "Gfx.WaitForPresentOnGfxThread", "WaitForTargetFPS", "YQ.Title.Presentation", "YQ.Title.SelectSavedProfile", "YQ.PlayerVisual.InitialBinding" };
    private static readonly List<ProfilerRecorder> Recorders = new List<ProfilerRecorder>();
    private static readonly List<object> Hitches = new List<object>();
    private static readonly Dictionary<string, double> Maxima = new Dictionary<string, double>();
    private static readonly Dictionary<string, List<double>> Gaps = new Dictionary<string, List<double>>();
    private static double Started, Previous, Released;
    private static int PreviousFrame;
    private static bool Active;

    internal static void Start()
    {
        if (Active || !Application.isPlaying) return;
        // note: One sample per recorder bounds memory independently of how many frames the loading sequence takes.
        Hitches.Clear(); Maxima.Clear(); Gaps.Clear(); Recorders.Clear();
        foreach (string name in Names)
            Recorders.Add(ProfilerRecorder.StartNew(ProfilerCategory.Internal, name, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.WrapAroundWhenCapacityReached | ProfilerRecorderOptions.SumAllSamplesInFrame));
        Started = Previous = EditorApplication.timeSinceStartup; Released = 0d;
        PreviousFrame = Time.frameCount; Active = true;
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += Finish;
        EditorApplication.quitting += Finish;
    }

    private static void Tick()
    {
        if (!Active) return;
        double now = EditorApplication.timeSinceStartup;
        if (!Application.isPlaying || now - Started >= 600d || (Released > 0d && now - Released >= 120d)) { Finish(); return; }
        if (Time.frameCount == PreviousFrame) return;
        string phase = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ? "gameplay" : YQStartupLoadingScreen.IsGenerationVisible ? "loading" : "title";
        if (phase == "gameplay" && Released <= 0d) Released = now;
        double gapMs = (now - Previous) * 1000d;
        Previous = now; PreviousFrame = Time.frameCount;
        // note: A bounded scalar distribution preserves whole-session gaps, including Editor stalls outside PlayerLoop; it is not a traversal acceptance test.
        if (!Gaps.TryGetValue(phase, out var gaps)) Gaps[phase] = gaps = new List<double>();
        if (gaps.Count < 20000) gaps.Add(gapMs);
        Dictionary<string, double> values = null;
        if (gapMs >= 80d && Hitches.Count < 256) values = new Dictionary<string, double>();
        for (int index = 0; index < Recorders.Count; index++)
        {
            var recorder = Recorders[index];
            if (!recorder.Valid) continue;
            double ms = recorder.LastValue / 1000000d;
            string key = phase + ":" + Names[index];
            if (!Maxima.TryGetValue(key, out double maximum) || ms > maximum) Maxima[key] = ms;
            if (values != null) values[Names[index]] = ms;
        }
        if (values != null) Hitches.Add(new { elapsedSeconds = now - Started, phase, gapMs, focused = Application.isFocused, stage = YQStartupLoadingScreen.CurrentGenerationSubstep, values });
    }

    private static void Finish()
    {
        if (!Active) return;
        // note: End instrumentation before writing the receipt so observation I/O is outside the measured interval.
        Active = false; EditorApplication.update -= Tick;
        AssemblyReloadEvents.beforeAssemblyReload -= Finish; EditorApplication.quitting -= Finish;
        foreach (var recorder in Recorders) recorder.Dispose();
        Recorders.Clear();
        var distributions = new Dictionary<string, object>();
        foreach (var phase in Gaps)
        {
            phase.Value.Sort(); var samples = phase.Value;
            distributions[phase.Key] = new { samples = samples.Count, medianMs = samples[samples.Count / 2], p95Ms = samples[(int)((samples.Count - 1) * .95d)], maximumMs = samples[samples.Count - 1] };
        }
        string path = "outputs/DialogueVramRepair_20261011/whole-session-" + DateTime.UtcNow.ToString("HHmmss") + ".json";
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow, evidence = "Whole-session Editor frame-gap observation; direct recorder samples may precede the observed gap", runtimeMvid = typeof(YQGeneratedWorldRuntimeBuilder).Assembly.ManifestModule.ModuleVersionId, gameplayReleased = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased, elapsedSeconds = EditorApplication.timeSinceStartup - Started, distributions, Maxima, Hitches, startupMaximumSteps = YQGeneratedWorldRuntimeBuilder.StartupMaximumStepMilliseconds }, Formatting.Indented));
        Debug.Log("[YQWholeSessionFrames] Receipt: " + path);
    }
}
#endif
