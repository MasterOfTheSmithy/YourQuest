using System.Collections;
using UnityEngine;
using UnityEngine.Scripting;

[DefaultExecutionOrder(-80)]
public sealed class YQGeneratedWorldPerformanceDirector : MonoBehaviour
{
    private const float GameplayShadowDistance = 42f;
    private const float GameplayLodBias = 0.9f;
    private const float GameplayNearClipPlane = 0.05f;
    private const ulong IncrementalGcSliceNanoseconds = 1000000;
    private static bool startupBudgetConfigured;
    private static bool textureBudgetOwned;
    private static int previousTextureMipLimit;
    private const int SharedGpuTextureMipLimit = 2;
    private static AsyncOperation sharedGpuCollection;
    private static float nextSharedGpuCollection;
    private float nextSystemMemoryProbe;
    private bool systemMemoryCollectionRunning;

    private void Update()
    {
        // note: Reuse this budget owner and its single collection/cooldown. Inference gets priority; host RAM is sampled only once per five seconds after gameplay is released.
        if (!Application.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            systemMemoryCollectionRunning || sharedGpuCollection != null ||
            Time.realtimeSinceStartup < nextSharedGpuCollection || Time.realtimeSinceStartup < nextSystemMemoryProbe) return;
        nextSystemMemoryProbe = Time.realtimeSinceStartup + 5f;
        bool inferenceBusy = LLMClient.Instance != null && LLMClient.Instance.IsBusy;
        if (inferenceBusy || !YQLlmResidencyPolicy.ShouldReclaimGameSystemMemory(
            YQLlmResidencyPolicy.AvailableSystemMemoryBytes(), UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(), inferenceBusy)) return;
        systemMemoryCollectionRunning = true;
        StartCoroutine(ReclaimSystemMemoryRoutine());
    }

    private IEnumerator ReclaimSystemMemoryRoutine()
    {
        // note: Cache retirement also releases CPU asset memory; never force a full managed GC or discard live terrain, accepted content or model residency.
        long before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
        try { yield return ReclaimReloadableGpuAssetsRoutine(); }
        finally { systemMemoryCollectionRunning = false; }
        Debug.Log("[YQGeneratedWorldPerformanceDirector] System RAM cache relief: allocatedBeforeMiB=" + before / (1024L * 1024L) +
            ", allocatedAfterMiB=" + UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L) +
            ", availableMiB=" + YQLlmResidencyPolicy.AvailableSystemMemoryBytes() / (1024L * 1024L) + ".");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSharedGpuCollection()
    {
        // note: Domain-reload-disabled Play sessions restart their clocks and cannot inherit an old collection or cooldown.
        sharedGpuCollection = null;
        nextSharedGpuCollection = 0f;
    }

    internal static IEnumerator ReclaimReloadableGpuAssetsRoutine()
    {
        // note: The existing render-budget owner releases only reloadable cache references; Unity preserves assets still used by the player, world, terrain and presentation.
        if (!Application.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased) yield break;
        if (sharedGpuCollection != null)
        {
            // note: A dialogue request can take over a collection begun by preempted frontier work; release the completed handle even if its initiating coroutine was cancelled.
            AsyncOperation pendingCollection = sharedGpuCollection;
            while (!pendingCollection.isDone) yield return null;
            if (ReferenceEquals(sharedGpuCollection, pendingCollection)) sharedGpuCollection = null;
            yield break;
        }
        if (Time.realtimeSinceStartup < nextSharedGpuCollection) yield break;
        nextSharedGpuCollection = Time.realtimeSinceStartup + 30f;
        float started = Time.realtimeSinceStartup;
        float longestFrame = 0f;
        int shards = YQRuntimeWorldAssetRegistry.ReleaseColdReloadableShards(Time.realtimeSinceStartup);
        int manifests = YQCompiledWorldSiteInstance.ReleaseUnusedPreparedManifestCache();
        // note: A prior streaming retirement can leave native allocations even when there is no new cache entry to retire.
        AsyncOperation collection = Resources.UnloadUnusedAssets();
        sharedGpuCollection = collection;
        while (!collection.isDone)
        {
            longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
            yield return null;
        }
        longestFrame = Mathf.Max(longestFrame, Time.unscaledDeltaTime);
        if (ReferenceEquals(sharedGpuCollection, collection)) sharedGpuCollection = null;
        Debug.Log("[YQGeneratedWorldPerformanceDirector] Shared GPU cache collection completed: coldShards=" + shards + ", unusedManifests=" + manifests +
            ", elapsedSeconds=" + (Time.realtimeSinceStartup - started).ToString("F3") + ", longestObservedFrameMs=" + (longestFrame * 1000f).ToString("F1") + ". Live game assets retained.");
    }

    // note: Loading shares rendered frames with the Goddess and input; reserve most of each frame for presentation while preserving the ordinary streaming limit.
    internal static float CooperativeWorkSliceSeconds =>
        Application.isPlaying && !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ? 0.003f : 0.0015f;

    private sealed class LoadingWorkBoundary : CustomYieldInstruction { public override bool keepWaiting => false; }
    internal static readonly object LoadingWorkBoundaryToken = new LoadingWorkBoundary();
    // note: Explicit work boundaries may share a loading frame. Physics, async operation and lifecycle waits must continue to yield normally.
    internal static object CooperativeWorkBoundary => Application.isPlaying && !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased
        ? LoadingWorkBoundaryToken : null;

    public static void ConfigureGameplayWorkBudget()
    {
        // note: Restore both native integration limits at the authoritative input-release edge, before the first playable frame.
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        AsyncInstantiateOperation.SetIntegrationTimeMS(2f);
    }

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void RegisterTextureBudgetCleanup()
    {
        // note: Restore the Editor's quality after Play; never change texture imports or persist a quality asset.
        UnityEditor.EditorApplication.playModeStateChanged -= RestoreEditorTextureBudget;
        UnityEditor.EditorApplication.playModeStateChanged += RestoreEditorTextureBudget;
    }

    private static void RestoreEditorTextureBudget(UnityEditor.PlayModeStateChange state)
    {
        if (state != UnityEditor.PlayModeStateChange.EnteredEditMode) return;
        if (UnityEditor.SessionState.GetBool("YQ.LoadingBudget.Owned", false))
        {
            Application.backgroundLoadingPriority = (ThreadPriority)UnityEditor.SessionState.GetInt("YQ.LoadingBudget.PreviousPriority", (int)ThreadPriority.Normal);
            AsyncInstantiateOperation.SetIntegrationTimeMS(UnityEditor.SessionState.GetFloat("YQ.LoadingBudget.PreviousIntegration", 2f));
            UnityEditor.SessionState.EraseBool("YQ.LoadingBudget.Owned");
        }
        // note: Session state survives the domain reload between Play and Edit, unlike runtime static ownership flags.
        if (UnityEditor.SessionState.GetBool("YQ.TextureBudget.Owned", false) &&
            QualitySettings.globalTextureMipmapLimit == UnityEditor.SessionState.GetInt("YQ.TextureBudget.Applied", -1))
            QualitySettings.globalTextureMipmapLimit = UnityEditor.SessionState.GetInt("YQ.TextureBudget.Previous", 0);
        UnityEditor.SessionState.EraseBool("YQ.TextureBudget.Owned");
        textureBudgetOwned = false;
        startupBudgetConfigured = false;
    }
#endif

    public static void ConfigureStartupFrameBudget()
    {
        if (startupBudgetConfigured)
            return;

        startupBudgetConfigured = true;
        // note: Loaded travel assets exceeded the 8 GB card's shared model budget at one mip. Limit resident texture dimensions to one quarter while preserving imports and restoring Editor quality after Play.
        if (SystemInfo.graphicsMemorySize > 0 && SystemInfo.graphicsMemorySize <= 8192 &&
            QualitySettings.globalTextureMipmapLimit < SharedGpuTextureMipLimit)
        {
            previousTextureMipLimit = QualitySettings.globalTextureMipmapLimit;
            QualitySettings.globalTextureMipmapLimit = SharedGpuTextureMipLimit;
            textureBudgetOwned = true;
            RememberTextureBudget(SharedGpuTextureMipLimit);
        }
        Application.targetFrameRate = 60;
        // note: Keep procedural startup advancing when the editor loses focus; verification must not depend on a foreground native window.
        Application.runInBackground = true;
        // note: Native asset integration must fit the same visible loading frame as procedural work and the title camera.
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool("YQ.LoadingBudget.Owned", true);
        UnityEditor.SessionState.SetInt("YQ.LoadingBudget.PreviousPriority", (int)Application.backgroundLoadingPriority);
        UnityEditor.SessionState.SetFloat("YQ.LoadingBudget.PreviousIntegration", AsyncInstantiateOperation.GetIntegrationTimeMS());
#endif
        Application.backgroundLoadingPriority = ThreadPriority.Low;
        AsyncInstantiateOperation.SetIntegrationTimeMS(2f);

        // note: Do not change Unity's async-upload buffer or render queue at runtime; D3D12 can lose an in-flight fence when these native graphics settings are changed during Play Mode startup.

        if (GarbageCollector.isIncremental)
        {
            // note: Keep managed collection work bounded to a small slice so generation allocations cannot accumulate into a long reveal-frame collection.
            GarbageCollector.incrementalTimeSliceNanoseconds =
                IncrementalGcSliceNanoseconds;
        }

        Debug.Log(
            "[YQGeneratedWorldPerformanceDirector] Startup anti-hang budget active. " +
            "Loading integration=2ms, cooperative work=3ms, incremental GC=1ms, graphics queue left driver-safe.");
    }

    private static void RememberTextureBudget(int applied)
    {
#if UNITY_EDITOR
        UnityEditor.SessionState.SetBool("YQ.TextureBudget.Owned", true);
        UnityEditor.SessionState.SetInt("YQ.TextureBudget.Previous", previousTextureMipLimit);
        UnityEditor.SessionState.SetInt("YQ.TextureBudget.Applied", applied);
#endif
    }

    public static bool TryRelieveGpuMemoryPressure()
    {
        // note: One monotonic extra mip releases texture residency for inference. Never oscillate quality, touch imports, or reduce the required free-memory reserve.
        if (!Application.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            SystemInfo.graphicsMemorySize <= 0 || SystemInfo.graphicsMemorySize > 8192 ||
            QualitySettings.globalTextureMipmapLimit >= 3) return false;
        if (!textureBudgetOwned) previousTextureMipLimit = QualitySettings.globalTextureMipmapLimit;
        textureBudgetOwned = true;
        QualitySettings.globalTextureMipmapLimit = 3;
        RememberTextureBudget(3);
        Debug.Log("[YQGeneratedWorldPerformanceDirector] Shared GPU memory pressure: resident texture detail reduced one step; imports and the inference reserve are unchanged.");
        return true;
    }

    public static void ConfigureForGeneratedWorld(Transform root)
    {
        ConfigureStartupFrameBudget();

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
