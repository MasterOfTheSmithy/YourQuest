#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

// note: Census is read-only; separate explicit idle cleanup and guarded restart operations never change assets, quality, or start Play.
[InitializeOnLoad]
public static class YQLlmMemoryCensus
{
    private const string Request = "Assets/Assets/EditorBuildRequests/YQ_LLM_MEMORY_CENSUS.request";
    private const string RestartRequest = "Assets/Assets/EditorBuildRequests/YQ_LLM_ISOLATED_RESTART.request";
    private const string ReleaseRequest = "Assets/Assets/EditorBuildRequests/YQ_LLM_RELEASE_IDLE_CACHE.request";
    private const string Folder = "outputs/LlmRequalification_20261008/MemoryCensus";
    private static double nextCheck;

    static YQLlmMemoryCensus() { EditorApplication.update += Tick; }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (File.Exists(ReleaseRequest))
        {
            bool freshRelease = File.GetLastWriteTimeUtc(ReleaseRequest) > DateTime.UtcNow.AddMinutes(-2);
            string expectedProcess = File.ReadAllText(ReleaseRequest).Trim();
            File.Delete(ReleaseRequest);
            if (freshRelease && expectedProcess == System.Diagnostics.Process.GetCurrentProcess().Id.ToString())
            {
                try { ReleaseIdleWorldCache(); }
                catch (Exception error) { Debug.LogException(error); }
            }
            return;
        }
        if (File.Exists(RestartRequest))
        {
            // note: The external benchmark launcher may close this exact idle process only; unsaved scenes and prefab edits block the restart.
            bool freshRestart = File.GetLastWriteTimeUtc(RestartRequest) > DateTime.UtcNow.AddMinutes(-2);
            string expectedProcess = File.ReadAllText(RestartRequest).Trim();
            File.Delete(RestartRequest);
            if (!freshRestart || expectedProcess != System.Diagnostics.Process.GetCurrentProcess().Id.ToString()) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("[YQLlmMemoryCensus] Restart rejected: Play is active."); return; }
            for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                { Debug.LogError("[YQLlmMemoryCensus] Restart rejected: unsaved scene."); return; }
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null && prefabStage.scene.isDirty)
            { Debug.LogError("[YQLlmMemoryCensus] Restart rejected: unsaved prefab."); return; }
            EditorApplication.Exit(0);
            return;
        }
        if (!File.Exists(Request)) return;
        bool fresh = File.GetLastWriteTimeUtc(Request) > DateTime.UtcNow.AddMinutes(-2);
        File.Delete(Request);
        if (!fresh) return;
        try { Capture(); }
        catch (Exception error) { Debug.LogException(error); }
    }

    [MenuItem("YourQuest/Verification/Capture LLM Coexistence Memory Census")]
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
        string label = !Application.isPlaying ? "edit-before-play" :
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ? "gameplay-released" : "title-before-continue";
        // note: Full dependency-size inventories can stall a large live world. Play captures only aggregate counters; detailed object attribution is explicitly deferred until Edit mode.
        bool includeAssetInventory = !Application.isPlaying;
        var textures = new List<object>();
        var objectGroups = new Dictionary<string, (int count, long bytes)>();
        var largestObjects = new List<(string type, string name, string path, long bytes)>();
        if (includeAssetInventory)
        {
            typeof(YQSemanticChunkRuntimeVerification).GetMethod("WriteR2AssetSessionSnapshot", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { label, Path.Combine(Folder, stamp + ".assetsession.tsv") });
            foreach (Texture texture in Resources.FindObjectsOfTypeAll<Texture>())
            {
                var image = texture as Texture2D;
                var target = texture as RenderTexture;
                textures.Add(new {
                    name = texture.name, path = AssetDatabase.GetAssetPath(texture), type = texture.GetType().Name,
                    width = texture.width, height = texture.height, bytes = Profiler.GetRuntimeMemorySizeLong(texture),
                    format = texture.graphicsFormat.ToString(), readable = texture.isReadable,
                    mipCount = image != null ? image.mipmapCount : 0,
                    streaming = image != null && image.streamingMipmaps,
                    loadedMip = image != null && image.streamingMipmaps ? image.loadedMipmapLevel : -1,
                    renderTargetCreated = target != null && target.IsCreated()
                });
            }
            // note: Attribute non-texture native objects too; managed heap and texture totals alone cannot explain animation, mesh or audio residency. Estimates may include shared dependencies.
            foreach (var asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            {
                if (asset == null) continue;
                string type = asset.GetType().Name;
                long bytes = Profiler.GetRuntimeMemorySizeLong(asset);
                objectGroups.TryGetValue(type, out var total);
                objectGroups[type] = (total.count + 1, total.bytes + bytes);
                if (bytes >= 16L * 1024 * 1024)
                    largestObjects.Add((type, asset.name, AssetDatabase.GetAssetPath(asset), bytes));
            }
        }
        var report = new {
            utc = DateTime.UtcNow.ToString("O"), processId = System.Diagnostics.Process.GetCurrentProcess().Id,
            playing = Application.isPlaying, isolation = LLMRuntimeConfig.BenchmarkIsolationActive,
            assetInventoryIncluded = includeAssetInventory,
            scene = EditorSceneManager.GetActiveScene().path, sceneDirty = EditorSceneManager.GetActiveScene().isDirty,
            gameplayReleased = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased,
            presentationWait = YourQuestTutorialAutoBootstrap.GameplayPresentationWaitReason,
            frame = Time.frameCount, allocatedBytes = Profiler.GetTotalAllocatedMemoryLong(),
            profilerEnabled = Profiler.enabled, profilerRecording = UnityEditorInternal.ProfilerDriver.enabled,
            profilerFirstFrame = UnityEditorInternal.ProfilerDriver.firstFrameIndex,
            profilerLastFrame = UnityEditorInternal.ProfilerDriver.lastFrameIndex,
            reservedBytes = Profiler.GetTotalReservedMemoryLong(), unusedReservedBytes = Profiler.GetTotalUnusedReservedMemoryLong(),
            managedUsedBytes = Profiler.GetMonoUsedSizeLong(), graphicsDriverAllocatedBytes = Profiler.GetAllocatedMemoryForGraphicsDriver(),
            textureMemoryBytes = Texture.currentTextureMemory, textureDesiredBytes = Texture.desiredTextureMemory,
            textureNonStreamingBytes = Texture.nonStreamingTextureMemory,
            streamingEnabled = QualitySettings.streamingMipmapsActive, textureMipLimit = QualitySettings.globalTextureMipmapLimit,
            runtimeMvid = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId, textures,
            // note: Existing main-thread counters identify startup coroutine stalls without enumerating assets or retaining native frames.
            startupWorkMilliseconds = YQGeneratedWorldRuntimeBuilder.StartupWorkMilliseconds,
            startupMaximumStepMilliseconds = YQGeneratedWorldRuntimeBuilder.StartupMaximumStepMilliseconds,
            sessionCaches = CaptureSessionCaches(),
            objectTypes = objectGroups.OrderByDescending(p => p.Value.bytes).Select(p => new { type = p.Key, p.Value.count, p.Value.bytes }).ToArray(),
            largestObjects = largestObjects.OrderByDescending(p => p.bytes).Take(30).Select(p => new { p.type, p.name, p.path, p.bytes }).ToArray(),
            evidence = includeAssetInventory
                ? "Read-only census of loaded Editor objects and Unity allocation estimates. Not physical per-object GPU residency, a leak proof, or a gameplay/performance pass."
                : "Lightweight live Unity aggregate counters; detailed asset inventory deferred until Edit mode. Empty inventory arrays do not mean zero loaded assets. Not physical GPU residency or a performance pass."
        };
        string json = JsonConvert.SerializeObject(report, Formatting.Indented);
        File.WriteAllText(Path.Combine(Folder, stamp + ".json"), json);
        File.WriteAllText(Path.Combine(Folder, "Current.json"), json);
        Debug.Log("[YQLlmMemoryCensus] Captured " + stamp + "; playing=" + Application.isPlaying);
    }

    private static List<object> CaptureSessionCaches()
    {
        // note: Inspect only established cache owners without invoking loaders or traversing arbitrary object graphs.
        var result = new List<object>();
        string[] owners = { "YQCompiledWorldSiteInstance", "YQCompiledWorldSiteBindingService", "YQRuntime2DArtRegistry",
            "YourQuestTutorialWorldHelpers", "YQBlueglassStyle", "YQRuntimeUrpMaterialRepair", "YQGeneratedWorldEnvironment" };
        foreach (string owner in owners)
        {
            Type type = typeof(LLMClient).Assembly.GetType(owner);
            if (type == null) continue;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (field.IsLiteral) continue;
                object value = field.GetValue(null);
                if (value is System.Collections.IDictionary map)
                    result.Add(new { owner, field = field.Name, count = map.Count, asset = string.Empty });
                else if (typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    var asset = value as UnityEngine.Object;
                    result.Add(new { owner, field = field.Name, count = asset != null ? 1 : 0, asset = asset != null ? AssetDatabase.GetAssetPath(asset) : string.Empty });
                }
            }
        }
        return result;
    }

    [MenuItem("YourQuest/Verification/Release Idle Benchmark World Cache")]
    public static void ReleaseIdleWorldCache()
    {
        // note: A one-off attribution experiment invokes the owner's existing session reset only outside Play; Unity retains all still-referenced assets.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Cache attribution requires an idle Editor.");
        for (int index = 0; index < UnityEngine.SceneManagement.SceneManager.sceneCount; index++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(index).isDirty)
                throw new InvalidOperationException("Preserve unsaved scenes before cache attribution.");
        var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
        if (prefabStage != null && prefabStage.scene.isDirty)
            throw new InvalidOperationException("Preserve unsaved prefab edits before cache attribution.");
        typeof(YQRuntimeWorldAssetRegistry).GetMethod("ResetRuntimeStatics", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
        EditorUtility.UnloadUnusedAssetsImmediate();
        Capture();
    }
}
#endif
