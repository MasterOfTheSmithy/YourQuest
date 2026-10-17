using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// note: Explicit assisted water traversal exercises the production capsule, input, animation and presentation behind the existing save barrier.
[InitializeOnLoad]
internal static class YQWaterWorldRuntimeVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/WaterWorldRuntime.request";
    private static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<float> Frames = new();
    private static readonly List<object> Samples = new();
    private static readonly List<string> Errors = new();
    private static YQInvestorPlayerMotor motor;
    private static Keyboard keyboard, priorKeyboard;
    private static Vector3 originalPosition, waterPosition;
    private static Quaternion originalRotation;
    private static Dictionary<FieldInfo, object> originalFields;
    private static bool running, swam, submerged, surfaced, splashed;
    private static double start, nextSample;
    private static int lastFrame;
    private static string artifact;
    static YQWaterWorldRuntimeVerification()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += End;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) End(); };
    }
    [MenuItem("YourQuest/Verification/Water And Streaming Repair Witness")]
    private static void Begin()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/Assets/Scenes/YourQuest_PlaySafe.unity" ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || Time.timeScale <= 0f)
            throw new InvalidOperationException("Released production PlaySafe is required.");
        motor = YQInvestorPlayerMotor.ActiveMotor;
        if (motor == null || !motor.CanProcessMovementInput) throw new InvalidOperationException("Authoritative input is unavailable.");
        // note: Choose an existing loaded deep channel, never spawn test water or move accepted geography.
        float best = float.PositiveInfinity;
        Terrain ground = YQPlayerFollowingSemanticChunkStreamer.Active?.AuthoredTerrain;
        foreach (var surface in UnityEngine.Object.FindObjectsByType<YQWaterSurfaceMotion>(FindObjectsSortMode.None))
        {
            var filter = surface.GetComponent<MeshFilter>();
            if (filter?.sharedMesh == null) continue;
            Vector3[] vertices = filter.sharedMesh.vertices;
            int[] triangles = filter.sharedMesh.triangles;
            for (int index = 0; index + 2 < triangles.Length; index += 3)
            {
                Vector3 vertex = (vertices[triangles[index]] + vertices[triangles[index + 1]] + vertices[triangles[index + 2]]) / 3f;
                Vector3 point = filter.transform.TransformPoint(vertex);
                if (!YQWaterSurfaceMotion.TryGetWaterHeight(point, out float height)) continue;
                if (ground == null || point.x < ground.transform.position.x || point.z < ground.transform.position.z ||
                    point.x > ground.transform.position.x + ground.terrainData.size.x || point.z > ground.transform.position.z + ground.terrainData.size.z) continue;
                float bed = ground.SampleHeight(point) + ground.transform.position.y;
                float distance = new Vector2(point.x - motor.transform.position.x, point.z - motor.transform.position.z).sqrMagnitude;
                if (height - bed < 2.1f || distance >= best) continue;
                best = distance; waterPosition = new Vector3(point.x, height - 1.95f, point.z);
            }
        }
        if (float.IsPositiveInfinity(best)) throw new InvalidOperationException("No loaded channel has safe capsule-depth water.");
        if (!YQDeveloperTestSession.Begin(out string failure)) throw new InvalidOperationException(failure);
        originalPosition = motor.transform.position; originalRotation = motor.transform.rotation;
        originalFields = new();
        foreach (string name in new[] { "_yaw", "_pitch", "_verticalVelocity", "_planarVelocity", "_breathRemaining", "_headUnderwater", "_isSwimming" })
        {
            FieldInfo field = typeof(YQInvestorPlayerMotor).GetField(name, Fields);
            if (field != null) originalFields.Add(field, field.GetValue(motor));
        }
        var player = PlayerStateManager.Instance.state;
        Vector3 last = player.lastPosition, logical = player.logicalPosition, origin = player.renderOrigin;
        string scene = player.currentScene, region = player.currentRegionId, regionName = player.currentRegionName;
        YQDeveloperTestSession.OnRestore(() =>
        {
            if (motor != null)
            {
                var controller = motor.GetComponent<CharacterController>(); bool enabled = controller.enabled;
                controller.enabled = false; motor.transform.SetPositionAndRotation(originalPosition, originalRotation);
                foreach (var entry in originalFields) entry.Key.SetValue(motor, entry.Value);
                controller.enabled = enabled;
            }
            player.lastPosition = last; player.logicalPosition = logical; player.renderOrigin = origin;
            player.currentScene = scene; player.currentRegionId = region; player.currentRegionName = regionName;
        });
        priorKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.onBeforeUpdate += Input; Application.logMessageReceived += Log;
        artifact = WorldStateManager.Instance.State.generatedWorldPlan.spatialPlanV2.contentHash;
        Frames.Clear(); Samples.Clear(); Errors.Clear(); swam = submerged = surfaced = splashed = false;
        start = EditorApplication.timeSinceStartup; nextSample = start; lastFrame = -1; running = true;
    }
    private static void Input()
    {
        if (!running || keyboard == null) return;
        double elapsed = EditorApplication.timeSinceStartup - start;
        // note: After the idle baseline, drive ordinary ascent/forward input in the existing native channel.
        InputSystem.QueueStateEvent(keyboard, elapsed < 20 ? new KeyboardState() : elapsed < 24 ? new KeyboardState(Key.LeftCtrl) :
            elapsed < 31 ? new KeyboardState(Key.Space) : new KeyboardState(Key.W, Key.Space));
    }
    private static void Log(string message, string trace, LogType type)
    { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && Errors.Count < 32) Errors.Add(message); }
    private static void Tick()
    {
        if (!running)
        {
            if (!File.Exists(Request) || !EditorApplication.isPlaying || EditorApplication.isCompiling || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased) return;
            AssetDatabase.DeleteAsset(Request);
            try { Begin(); } catch (Exception error) { Debug.LogException(error); }
            return;
        }
        if (!EditorApplication.isPlaying || motor == null) { End(); return; }
        double now = EditorApplication.timeSinceStartup, elapsed = now - start;
        if (Time.frameCount != lastFrame) { lastFrame = Time.frameCount; Frames.Add(Time.unscaledDeltaTime * 1000f); }
        if (elapsed >= 20 && elapsed < 20.5 && !swam && !submerged)
        {
            // note: This explicitly assisted placement is recorded as such; native input and collision own all following movement.
            var controller = motor.GetComponent<CharacterController>(); controller.enabled = false; motor.transform.position = waterPosition; controller.enabled = true;
        }
        swam |= motor.IsSwimming; submerged |= motor.IsHeadUnderwater;
        surfaced |= elapsed > 27 && motor.IsSwimming && !motor.IsHeadUnderwater;
        if (now >= nextSample)
        {
            nextSample = now + .5;
            var visual = UnityEngine.Object.FindFirstObjectByType<YQUnderwaterVisual>();
            var foamField = typeof(YQUnderwaterVisual).GetField("foam", Fields);
            var dropletsField = typeof(YQUnderwaterVisual).GetField("droplets", Fields);
            var foam = visual != null ? foamField.GetValue(visual) as ParticleSystem : null;
            var droplets = visual != null ? dropletsField.GetValue(visual) as ParticleSystem : null;
            splashed |= (foam != null && foam.particleCount > 0) || (droplets != null && droplets.particleCount > 0);
            var filter = motor.playerCamera != null ? motor.playerCamera.GetComponent<AudioLowPassFilter>() : null;
            var overlay = visual != null ? typeof(YQUnderwaterVisual).GetField("overlay", Fields).GetValue(visual) as UnityEngine.UI.Image : null;
            Samples.Add(new { elapsed, position = motor.transform.position.ToString("F3"), swimming = motor.IsSwimming, underwater = motor.IsHeadUnderwater,
                breath = motor.BreathRemainingSeconds, frameMs = Time.unscaledDeltaTime * 1000f, tint = overlay?.color.a, cutoffHz = filter?.cutoffFrequency,
                lowPass = filter != null && filter.enabled, foam = foam?.particleCount, droplets = droplets?.particleCount });
        }
        if (elapsed >= 42) End();
    }
    private static void End()
    {
        if (!running) return;
        running = false; InputSystem.onBeforeUpdate -= Input; Application.logMessageReceived -= Log;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard); keyboard = null; priorKeyboard?.MakeCurrent();
        bool sameArtifact = WorldStateManager.Instance?.State?.generatedWorldPlan?.spatialPlanV2?.contentHash == artifact;
        bool restored = YQDeveloperTestSession.RestoreForPlayModeExit(out string failure);
        Frames.Sort(); float Quantile(float q) => Frames.Count == 0 ? 0f : Frames[Mathf.Clamp(Mathf.RoundToInt((Frames.Count - 1) * q), 0, Frames.Count - 1)];
        string directory = "outputs/World_Water_Repair_20261006"; Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Runtime_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json"), Newtonsoft.Json.JsonConvert.SerializeObject(
            new { utc = DateTime.UtcNow.ToString("O"), evidence = "ASSISTED_NATIVE_PLAYER_WATER_INPUT_NOT_ORDINARY_EXPLORATION_OR_BUILD_ACCEPTANCE",
                verdict = swam && submerged && surfaced && splashed && restored && sameArtifact && Errors.Count == 0 ? "PASS" : "FAIL",
                swam, submerged, surfaced, splashed, restored, failure, sameArtifact, artifact, frameCount = Frames.Count,
                unity = Application.unityVersion, runtimeMvid = typeof(YQInvestorPlayerMotor).Assembly.ManifestModule.ModuleVersionId.ToString(),
                streamingWorkMaxMs = YQPlayerFollowingSemanticChunkStreamer.Active?.MaximumStreamingWorkSeconds * 1000f,
                streamingStageMaxMs = YQPlayerFollowingSemanticChunkStreamer.Active?.MaximumStreamingStageSeconds * 1000f,
                frameP50Ms = Quantile(.5f), frameP95Ms = Quantile(.95f), frameP99Ms = Quantile(.99f), frameMaxMs = Quantile(1), samples = Samples, errors = Errors }, Newtonsoft.Json.Formatting.Indented));
        Debug.Log("[YQWaterRuntimeWitness] Complete; restored=" + restored + "; swimming=" + swam + "; submerged=" + submerged + "; surfaced=" + surfaced + "; splash=" + splashed);
    }
}
