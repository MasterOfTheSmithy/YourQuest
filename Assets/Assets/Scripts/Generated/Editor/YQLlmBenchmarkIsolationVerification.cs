#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// note: A named, opt-in assisted fixture uses canonical profile/origin/world owners; it never certifies ordinary player input.
[InitializeOnLoad]
public static class YQLlmBenchmarkIsolationVerification
{
    private const string Key = "YourQuest.LlmBenchmarkIsolation.";
    private const string Folder = "outputs/LlmRequalification_20261008/UnityBaseline";
    private const string StartRequest = "Assets/Assets/EditorBuildRequests/YQ_LLM_BENCHMARK_BASELINE.request";
    private const string ResumeRequest = "Assets/Assets/EditorBuildRequests/YQ_LLM_BENCHMARK_RESUME.request";
    private static double nextReceipt;
    private static int lastFrame, renderedFrames;
    private static readonly List<float> frames = new List<float>();

    static YQLlmBenchmarkIsolationVerification()
    {
        EditorApplication.update += Tick;
        RenderPipelineManager.endContextRendering += OnRendered;
    }

    public static void ResumeExistingFixture()
    {
        // note: A controlled Editor restart reuses only this run's named benchmark profile and its accepted fallback world.
        string priorPath = Path.Combine(Folder, "Current.json");
        var prior = JsonConvert.DeserializeAnonymousType(File.ReadAllText(priorPath), new { profileId = "", previousProfileId = "" });
        if (prior == null || !Guid.TryParseExact(prior.profileId, "N", out _))
            throw new InvalidOperationException("No valid prior benchmark profile to resume.");
        SessionState.SetString(Key + "profile", prior.profileId);
        SessionState.SetString(Key + "previousProfile", prior.previousProfileId);
        SessionState.SetBool(Key + "resumeWorld", true);
        Begin();
    }

    [MenuItem("YourQuest/Verification/Begin Isolated LLM Benchmark World")]
    public static void Begin()
    {
        if (!LLMRuntimeConfig.BenchmarkIsolationActive || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Launch an idle Editor with -yqLlmBenchmarkIsolation before this verification.");
        if (EditorSceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Preserve the unsaved scene before starting the isolated benchmark.");
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Key + "armed", true);
        SessionState.SetString(Key + "startedUtc", DateTime.UtcNow.ToString("O"));
        // note: An explicit rerun retains this session's test-profile identity and original selection, avoiding duplicate profiles.
        SessionState.SetString(Key + "failure", string.Empty);
        SessionState.SetBool(Key + "transportChecks", false);
        SetPhase("EDITOR_IDLE");
        EditorSceneManager.OpenScene("Assets/Assets/Scenes/YourQuest_PlaySafe.unity", OpenSceneMode.Single);
        WriteReceipt();
    }

    private static void SetPhase(string phase)
    {
        SessionState.SetString(Key + "phase", phase);
        SessionState.SetString(Key + "phaseUtc", DateTime.UtcNow.ToString("O"));
        Debug.Log("[YQLlmBenchmarkIsolation] " + phase);
    }

    private static double PhaseSeconds => (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Key + "phaseUtc", DateTime.UtcNow.ToString("O"))).ToUniversalTime()).TotalSeconds;

    private static void Tick()
    {
        // note: Resume the same accepted benchmark world explicitly, preserving its profile, seed, and plan for before/after measurements.
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(ResumeRequest))
        {
            bool fresh = File.GetLastWriteTimeUtc(ResumeRequest) > DateTime.UtcNow.AddMinutes(-2);
            File.Delete(ResumeRequest);
            if (fresh) ResumeExistingFixture();
            return;
        }
        // note: A fresh, named one-shot request may restart this verification after compilation; an ordinary Play stop cannot restart it.
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(StartRequest))
        {
            bool fresh = File.GetLastWriteTimeUtc(StartRequest) > DateTime.UtcNow.AddMinutes(-2);
            File.Delete(StartRequest);
            if (fresh) Begin();
            return;
        }
        if (!SessionState.GetBool(Key + "armed", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        try
        {
            string phase = SessionState.GetString(Key + "phase", string.Empty);
            if (!LLMRuntimeConfig.BenchmarkIsolationActive) { Fail("Isolation flag disappeared."); return; }
            if (phase == "EDITOR_IDLE" && PhaseSeconds >= 10)
            {
                SetPhase("TITLE");
                EditorApplication.isPlaying = true;
                return;
            }
            if (phase != "EDITOR_IDLE" && !EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && PhaseSeconds > 20) Fail("Play Mode stopped before the baseline completed.");
                return;
            }
            LLMClient client = LLMClient.Instance;
            if (client != null && client.InferenceDispatchCount != 0) { Fail("Unity dispatched inference while isolated."); return; }
            if (client != null && !SessionState.GetBool(Key + "transportChecks", false)) VerifyBlockedTransports(client);
            if (Time.frameCount != lastFrame)
            {
                lastFrame = Time.frameCount;
                if (phase == "LOADED_STABILIZING" || phase == "READY") frames.Add(Time.unscaledDeltaTime * 1000f);
                if (frames.Count > 3600) frames.RemoveRange(0, 1800);
            }
            if (phase == "TITLE" && TitleReady() && YQProfileSaveSystem.Instance != null)
            {
                Application.runInBackground = true;
                if (string.IsNullOrEmpty(SessionState.GetString(Key + "previousProfile", string.Empty)))
                    SessionState.SetString(Key + "previousProfile", YQProfileSaveSystem.Instance.ActiveProfileId ?? string.Empty);
                // note: Create only a new named test profile. Existing accepted profiles are never reset or used as fixtures.
                string profile = SessionState.GetString(Key + "profile", string.Empty);
                if (SessionState.GetBool(Key + "resumeWorld", false) &&
                    !(YQProfileSaveSystem.Instance.FindProfile(profile)?.displayName ?? "").StartsWith("LLM Benchmark ", StringComparison.Ordinal))
                { Fail("Resume rejected: this is not the existing named benchmark profile."); return; }
                if (string.IsNullOrEmpty(profile) || YQProfileSaveSystem.Instance.FindProfile(profile) == null)
                    profile = YQProfileSaveSystem.Instance.CreateNewProfile("LLM Benchmark " + DateTime.UtcNow.ToString("yyyyMMdd HHmmss"),
                        "they/them", "average", "patient traveler and practical craft", "Protect people through careful preparation.", "Practical travel clothes.");
                if (string.IsNullOrEmpty(profile)) { Fail("Test profile creation failed: " + YQProfileSaveSystem.Instance.LastFailure); return; }
                SessionState.SetString(Key + "profile", profile);
                SetPhase("PROFILE_READY");
            }
            else if (phase == "PROFILE_READY" && TitleReady())
            {
                if (YQTitleScreenUI.Instance.ContinueExistingForDevelopmentVerification(SessionState.GetString(Key + "profile", string.Empty)))
                    SetPhase(SessionState.GetBool(Key + "resumeWorld", false) ? "WORLD_GENERATION" : "QUESTIONNAIRE");
            }
            else if (phase == "QUESTIONNAIRE" && YQTitleScreenUI.StartupFlowComplete)
            {
                var questionnaire = UnityEngine.Object.FindAnyObjectByType<YQOriginQuestionnaireUI>();
                if (questionnaire != null && !questionnaire.StartupPhaseResolved)
                {
                    // note: Assisted fixture injection calls the existing questionnaire completion/normalization path; this is not a UI-input acceptance test.
                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(YQOriginQuestionnaireUI).GetMethod("BeginQuestions", flags).Invoke(questionnaire, new object[] { "Benchmark fixture", 3 });
                    var answers = (List<string>)typeof(YQOriginQuestionnaireUI).GetField("_answers", flags).GetValue(questionnaire);
                    answers.AddRange(new[] { "I observe danger, protect others, and prepare a safe path.", "Honest craft, repairing equipment and helping travelers.", "Patience, practical judgment and dependable kindness." });
                    SetPhase("WORLD_GENERATION");
                    typeof(YQOriginQuestionnaireUI).GetMethod("CompleteQuestionnaire", flags).Invoke(questionnaire, null);
                }
            }
            else if (phase == "WORLD_GENERATION")
            {
                var builder = YQGeneratedWorldRuntimeBuilder.Instance;
                if (builder != null && (builder.HasMaterializationFailed || builder.InitialGenerationRecoveryRequired))
                { Fail("World readiness rejected the fallback. Inspect the unchanged readiness diagnostics."); return; }
                if (builder != null && builder.HasMaterializedCurrentWorld && YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)
                {
                    frames.Clear(); renderedFrames = 0;
                    SetPhase("LOADED_STABILIZING");
                }
            }
            else if (phase == "LOADED_STABILIZING" && PhaseSeconds >= 30)
            {
                if (renderedFrames == 0) { Fail("No rendered frames observed; this is not a rendered-world baseline."); return; }
                SetPhase("READY");
            }
            if (phase != "READY" && phase != "EDITOR_IDLE" && PhaseSeconds > 480) { Fail("Baseline stage exceeded eight minutes: " + phase); return; }
            if (EditorApplication.timeSinceStartup >= nextReceipt)
            {
                nextReceipt = EditorApplication.timeSinceStartup + 2;
                WriteReceipt();
            }
        }
        catch (Exception error) { Fail(error.GetBaseException().ToString()); }
    }

    private static void VerifyBlockedTransports(LLMClient client)
    {
        // note: Trap endpoints are never contacted: both direct adapters must terminate synchronously before any probe or launch.
        LLMRuntimeConfig config = LLMRuntimeConfig.CreateRuntimeDefault();
        config.enableRuntimeLlm = true;
        config.serverPort = 11997;
        bool llamaCallback = false, ollamaCallback = false, typed = false, legacy = false;
        using (var llama = new LlamaCppServerProcess())
        {
            IEnumerator guard = llama.EnsureReady(config, (ok, _) => llamaCallback = !ok);
            if (guard.MoveNext() || !llamaCallback) throw new InvalidOperationException("llama adapter isolation failed.");
        }
        var ollama = new OllamaServerProcess();
        IEnumerator ollamaGuard = ollama.EnsureReady(config, (ok, _) => ollamaCallback = !ok);
        if (ollamaGuard.MoveNext() || !ollamaCallback) throw new InvalidOperationException("Ollama adapter isolation failed.");
        ollama.Dispose(true);
        client.Submit(new YQLlmRequest { prompt = "Isolation probe", debugTag = "IsolationProbe" }, result => typed = !result.success);
        client.Enqueue("Isolation probe", response => legacy = response == null, "IsolationProbeLegacy");
        UnityEngine.Object.DestroyImmediate(config);
        if (!typed || !legacy || client.PendingRequestCount != 0 || client.InferenceDispatchCount != 0)
            throw new InvalidOperationException("Typed/legacy request isolation failed.");
        SessionState.SetBool(Key + "transportChecks", true);
    }

    private static bool TitleReady()
    {
        // note: Editor callbacks can run before the bootstrap opens the title. Observe its actual interaction gate, not elapsed process time.
        var title = YQTitleScreenUI.Instance;
        if (title == null) return false;
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        return (bool)typeof(YQTitleScreenUI).GetField("_open", flags).GetValue(title) &&
            (bool)typeof(YQTitleScreenUI).GetProperty("CanInteract", flags).GetValue(title);
    }

    private static void OnRendered(ScriptableRenderContext context, List<Camera> cameras)
    {
        if (!Application.isPlaying || !SessionState.GetBool(Key + "armed", false)) return;
        foreach (var camera in cameras) if (camera != null && camera.cameraType == CameraType.Game) { renderedFrames++; break; }
    }

    private static void Fail(string reason)
    {
        SessionState.SetString(Key + "failure", reason);
        SetPhase("BLOCKED");
        SessionState.SetBool(Key + "armed", false);
        WriteReceipt();
        Debug.LogError("[YQLlmBenchmarkIsolation] " + reason);
    }

    private static void WriteReceipt()
    {
        Directory.CreateDirectory(Folder);
        var world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        var builder = YQGeneratedWorldRuntimeBuilder.Instance;
        var client = LLMClient.Instance;
        var sorted = new List<float>(frames); sorted.Sort();
        float Percentile(float fraction) => sorted.Count == 0 ? 0 : sorted[Mathf.Clamp((int)Math.Ceiling(sorted.Count * fraction) - 1, 0, sorted.Count - 1)];
        var receipt = new {
            utc = DateTime.UtcNow.ToString("O"), processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            startedUtc = SessionState.GetString(Key + "startedUtc", string.Empty),
            status = SessionState.GetString(Key + "phase", string.Empty), isolation = LLMRuntimeConfig.BenchmarkIsolationActive,
            failure = SessionState.GetString(Key + "failure", string.Empty), transportChecks = SessionState.GetBool(Key + "transportChecks", false),
            profileId = SessionState.GetString(Key + "profile", string.Empty), previousProfileId = SessionState.GetString(Key + "previousProfile", string.Empty),
            persistentDataPath = Application.persistentDataPath, scene = EditorSceneManager.GetActiveScene().path,
            playMode = Application.isPlaying, gameplayReleased = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased,
            materialized = builder != null && builder.HasMaterializedCurrentWorld, worldSeed = world?.generatedWorldPlan?.worldSeed,
            worldPlanSource = world?.generatedWorldPlan?.source, blockedRequests = client != null ? client.BenchmarkBlockedRequestCount : 0,
            inferenceDispatches = client != null ? client.InferenceDispatchCount : 0, pendingRequests = client != null ? client.PendingRequestCount : 0,
            renderedFrames, sampledFrames = sorted.Count, frameP50Ms = Percentile(.5f), frameP95Ms = Percentile(.95f),
            runtimeMvid = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId,
            evidence = "Assisted production-scene fallback fixture and rendering instrumentation; not ordinary player-input or LLM speech acceptance." };
        string json = JsonConvert.SerializeObject(receipt, Formatting.Indented);
        File.WriteAllText(Path.Combine(Folder, "Current.json"), json);
        File.AppendAllText(Path.Combine(Folder, "History.jsonl"), JsonConvert.SerializeObject(receipt) + Environment.NewLine);
    }
}
#endif
