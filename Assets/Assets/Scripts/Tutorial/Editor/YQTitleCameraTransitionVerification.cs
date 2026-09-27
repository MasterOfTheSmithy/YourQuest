#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Verify the serialized title scene in an isolated preview; never enter Play mode or save the user's scene.
public static class YQTitleCameraTransitionVerification
{
    private const string RequestPath = "Temp/YQTitleCameraTransitionVerification.request";
    private const string ReportPath = "Logs/YQTitleCameraTransitionVerification.txt";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [InitializeOnLoadMethod]
    private static void CheckRequestAfterCompilation()
    {
        // note: Explicit one-shot opt-in only; normal script reloads do not run scene tests.
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(RequestPath) && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                File.Delete(RequestPath);
                Verify();
            }
        };
    }

    [MenuItem("Tools/YourQuest/Testing/Verify Questionnaire Camera Transition")]
    public static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run the title camera verification outside Play mode.");

        // note: Preserve listener states because the existing stage teardown restores gameplay audio ownership.
        AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bool[] listenerStates = new bool[listeners.Length];
        for (int i = 0; i < listeners.Length; i++)
            listenerStates[i] = listeners[i].enabled;
        Scene preview = default;
        try
        {
            preview = EditorSceneManager.OpenPreviewScene("Assets/Assets/Scenes/YourQuest_TitleEnvironment.unity");
            YQTitleEnvironmentScene stage = null;
            foreach (GameObject root in preview.GetRootGameObjects())
            {
                stage = root.GetComponentInChildren<YQTitleEnvironmentScene>(true);
                if (stage != null)
                    break;
            }
            Require(stage != null, "The baked title stage controller must exist.");
            Camera camera = Read<Camera>(stage, "titleCamera");
            Transform anchor = Read<Transform>(stage, "goddessPortraitCameraAnchor");
            Transform face = Read<Transform>(stage, "goddessPortraitLookTarget");
            Require(camera != null && anchor != null && face != null, "Camera and authored portrait anchors must be assigned.");
            // note: Suppress test audio without modifying serialized scene assets.
            Write(stage, "uiAudioSource", null);
            Vector3 wide = camera.transform.position;
            Quaternion wideRotation = camera.transform.rotation;
            Require(Vector3.Distance(wide, anchor.position) > 1f, "The face shot must visibly differ from the wide shot.");
            Transform goddessRoot = Read<Transform>(stage, "goddessRoot");
            Require(goddessRoot != null, "The goddess root must be assigned.");
            Renderer[] goddessRenderers = goddessRoot.GetComponentsInChildren<Renderer>(true);
            Bounds goddessBounds = default;
            bool hasBounds = false;
            for (int i = 0; i < goddessRenderers.Length; i++)
            {
                if (goddessRenderers[i] == null || !goddessRenderers[i].enabled)
                    continue;
                if (!hasBounds) { goddessBounds = goddessRenderers[i].bounds; hasBounds = true; }
                else goddessBounds.Encapsulate(goddessRenderers[i].bounds);
            }
            Require(hasBounds && goddessBounds.size.y > 0.1f, "The goddess geometry must have usable bounds.");

            stage.BeginQuestionnaireTransition();
            Vector3 computedDestination = Read<Vector3>(stage, "_thresholdDestinationPosition");
            Sample(stage, 0f);
            Require(Vector3.Distance(camera.transform.position, wide) < 0.01f, "Opening must not snap position.");
            Require(Quaternion.Angle(camera.transform.rotation, wideRotation) < 0.1f, "Opening must not snap rotation.");
            Sample(stage, 2.5f);
            Require(Vector3.Distance(camera.transform.position, Vector3.Lerp(wide, computedDestination, 0.5f)) < 0.02f, "Halfway must be halfway through the five-second eased move.");
            float startedAt = Read<float>(stage, "_thresholdTransitionStartedAt");
            stage.BeginQuestionnaireTransition();
            Require(Read<float>(stage, "_thresholdTransitionStartedAt") == startedAt, "Duplicate opening must not restart the animation.");
            stage.BeginGenerationIdle();
            Require(Read<float>(stage, "_thresholdTransitionStartedAt") == startedAt, "Generation handoff must preserve the ongoing approach.");
            Sample(stage, 5f);
            Vector3 destination = Read<Vector3>(stage, "_thresholdDestinationPosition");
            Vector3 destinationLook = Read<Vector3>(stage, "_thresholdDestinationLook");
            Require(Vector3.Distance(camera.transform.position, destination) < 0.01f, "The approach must arrive at its computed portrait destination.");
            Require(destinationLook.y > goddessBounds.min.y + goddessBounds.size.y * 0.74f, "The computed look point must be above the statue midpoint, toward the face.");
            Require(Vector3.Angle(camera.transform.forward, destinationLook - camera.transform.position) < 0.1f, "The portrait must look at its computed face target.");
            Require(Read<bool>(stage, "_generationIdleActive"), "Generation idle may start only after the approach completes.");
            stage.ShowTitleComposition();
            Require(Vector3.Distance(camera.transform.position, wide) < 0.01f, "Returning to title must restore the wide shot.");
            stage.BeginQuestionnaireTransition();
            Require(Read<bool>(stage, "_thresholdTransitionActive"), "Reopening must animate again.");

            // note: Reproduce the exact missing-MeshFilter path that interrupted title Awake on dust particles.
            GameObject dust = new GameObject("TitleCameraVerification_Dust", typeof(ParticleSystem));
            SceneManager.MoveGameObjectToScene(dust, preview);
            MethodInfo recoveryKey = typeof(YQRuntimeUrpMaterialRepair).GetMethod("BuildMaterialRecoveryKey", BindingFlags.Static | BindingFlags.NonPublic);
            Require(recoveryKey != null, "Material recovery regression entry must exist.");
            recoveryKey.Invoke(null, new object[] { dust.GetComponent<ParticleSystemRenderer>(), 0, true });
            Directory.CreateDirectory("Logs");
            string result = "PASS: actual baked scene; five-second eased camera translation and rotation; no opening snap; duplicate-open protection; generation handoff; bounds-derived face target; title reset/reopen; particle MeshFilter regression. Goddess bounds=" + goddessBounds + ". Edit-mode pose sampling only, not a rendered Play-mode test.";
            File.WriteAllText(ReportPath, result);
            Debug.Log("[YQTitleCameraTransitionVerification] " + result);
        }
        catch (Exception exception)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(ReportPath, "FAIL: " + exception);
            Debug.LogException(exception);
        }
        finally
        {
            if (preview.IsValid())
                EditorSceneManager.ClosePreviewScene(preview);
            for (int i = 0; i < listeners.Length; i++)
                if (listeners[i] != null)
                    listeners[i].enabled = listenerStates[i];
        }
    }

    // note: Sample the production LateUpdate on unscaled time, without changing global time scale or adding runtime test hooks.
    private static void Sample(YQTitleEnvironmentScene stage, float elapsed)
    {
        Write(stage, "_thresholdTransitionStartedAt", Time.unscaledTime - elapsed);
        typeof(YQTitleEnvironmentScene).GetMethod("LateUpdate", PrivateInstance).Invoke(stage, null);
    }

    private static T Read<T>(YQTitleEnvironmentScene stage, string name) =>
        (T)typeof(YQTitleEnvironmentScene).GetField(name, PrivateInstance).GetValue(stage);

    private static void Write(YQTitleEnvironmentScene stage, string name, object value) =>
        typeof(YQTitleEnvironmentScene).GetField(name, PrivateInstance).SetValue(stage, value);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
