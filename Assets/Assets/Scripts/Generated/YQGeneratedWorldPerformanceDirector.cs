using UnityEngine;

[DefaultExecutionOrder(-80)]
public sealed class YQGeneratedWorldPerformanceDirector : MonoBehaviour
{
    private const float GameplayShadowDistance = 42f;
    private const float GameplayLodBias = 0.9f;
    private const float GameplayNearClipPlane = 0.05f;

    public static void ConfigureForGeneratedWorld(Transform root)
    {
        if (root == null)
            return;

        YQGeneratedWorldPerformanceDirector director =
            FindAnyObjectByType<YQGeneratedWorldPerformanceDirector>();

        if (director == null)
        {
            GameObject host = new GameObject("YQGeneratedWorldPerformanceDirector");
            DontDestroyOnLoad(host);
            director = host.AddComponent<YQGeneratedWorldPerformanceDirector>();
        }

        director.Configure(root);
    }

    private void Configure(Transform root)
    {
        // note: Unity/URP owns renderer visibility and shadow culling; changing thousands of renderer states while the player moves caused popping, flicker, and recurring main-thread stalls.
        Application.targetFrameRate = 60;
        // note: Dense authored towns need a deterministic quality ceiling; inherited Ultra settings previously multiplied shadow and LOD draw cost without improving nearby readability.
        QualitySettings.shadowDistance = GameplayShadowDistance;
        QualitySettings.lodBias = GameplayLodBias;
        QualitySettings.shadowCascades = Mathf.Min(
            QualitySettings.shadowCascades,
            2);
        QualitySettings.pixelLightCount = Mathf.Min(
            QualitySettings.pixelLightCount,
            2);
        QualitySettings.realtimeReflectionProbes = false;

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            // note: Runtime-authored sites have no valid baked occlusion database, so disabling baked occlusion prevents close structures from being incorrectly hidden by unrelated source-scene data.
            gameplayCamera.useOcclusionCulling = false;
            gameplayCamera.nearClipPlane = Mathf.Min(
                gameplayCamera.nearClipPlane,
                GameplayNearClipPlane);
        }

        Debug.Log(
            "[YQGeneratedWorldPerformanceDirector] Stable URP visibility budget active. " +
            "Shadow distance=" + QualitySettings.shadowDistance.ToString("F0") +
            "m, LOD bias=" + QualitySettings.lodBias.ToString("F2") + ".");
    }
}
