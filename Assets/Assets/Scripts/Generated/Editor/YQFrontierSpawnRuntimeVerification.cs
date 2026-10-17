#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// note: This explicit batch witness observes production Continue and real scene objects; it never creates a profile, bypasses readiness, or forces a spawn.
[InitializeOnLoad]
public static class YQFrontierSpawnRuntimeVerification
{
    private const string Prefix = "YQFrontierSpawnWitness.";
    private const string Scene = "Assets/Assets/Scenes/YourQuest_PlaySafe.unity";
    private static readonly FieldInfo SemanticId = typeof(YQCompiledWorldSiteInstance).GetField("settlementId", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly List<string> Errors = new List<string>();
    private static double nextObservation;
    private static bool continued;
    private static bool terminal;

    static YQFrontierSpawnRuntimeVerification()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    public static void RunFromCommandLine()
    {
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-quit"))
            throw new InvalidOperationException("The spawning witness requires its own idle batch Editor without -quit.");
        // note: Read the existing selected profile; the normal title action remains the only load owner.
        string manifestPath = Path.Combine(Application.persistentDataPath, "Profiles", "profiles_manifest.json");
        var manifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(manifestPath));
        string profile = (string)manifest["activeProfileId"];
        if (string.IsNullOrWhiteSpace(profile)) throw new InvalidOperationException("No existing selected profile is available.");
        for (int index = 0; index < EditorSceneManager.sceneCount; index++)
            if (EditorSceneManager.GetSceneAt(index).isDirty) throw new InvalidOperationException("The batch Editor contains unsaved scene changes.");
        EditorSceneManager.OpenScene(Scene, OpenSceneMode.Single);
        string directory = "outputs/YourQuest_Tandem_20261004/frontier-runtime-spawn-verification";
        Directory.CreateDirectory(directory);
        string path = Path.GetFullPath(Path.Combine(directory, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json"));
        SessionState.SetString(Prefix + "path", path);
        SessionState.SetString(Prefix + "profile", profile);
        SessionState.SetString(Prefix + "started", DateTime.UtcNow.ToString("O"));
        SessionState.SetBool(Prefix + "running", true);
        SessionState.SetBool(Prefix + "ending", false);
        SessionState.SetBool(Prefix + "success", false);
        Errors.Clear();
        Debug.Log("[YQFrontierSpawnWitness] Production Continue requested for existing profile=" + profile + "; receipt=" + path);
        EditorApplication.isPlaying = true;
    }

    public static void QualifyAndVerifySitesFromCommandLine()
    {
        // note: The named asset publisher measures one new source assembly; the following fixtures certify contracts, without Play or profile admission.
        if (!Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode ||
            Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-quit"))
            throw new InvalidOperationException("Site qualification requires an idle batch Editor without -quit.");
        try
        {
            if (!Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-yqContractsOnly"))
            {
                if (Array.Exists(Environment.GetCommandLineArgs(), arg => arg == "-yqResumeShrine"))
                    YQCandidateInteriorBuilderV2.ResumeQualifiedNordicShrine();
                else YQCandidateInteriorBuilderV2.PublishQualifiedNordicShrine();
            }
            if (!YQDoorlessFoundationVerification.RunContracts())
                throw new InvalidOperationException("Doorless foundation regression failed.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
            return;
        }
        YQSemanticWorldAuthorityTests.RunColdProviderReplayContractsFromCommandLine();
    }

    private static void OnLog(string condition, string trace, LogType type)
    {
        if (!SessionState.GetBool(Prefix + "running", false) || Errors.Count >= 32 ||
            (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)) return;
        if (!Errors.Contains(condition)) Errors.Add(condition);
    }

    private static void Tick()
    {
        if (!Application.isBatchMode || !SessionState.GetBool(Prefix + "running", false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (SessionState.GetBool(Prefix + "ending", false))
        {
            // note: Exit only this explicitly owned batch session after normal Play teardown has completed.
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            bool success = SessionState.GetBool(Prefix + "success", false);
            SessionState.SetBool(Prefix + "running", false);
            EditorApplication.Exit(success ? 0 : 1);
            return;
        }
        if (!EditorApplication.isPlaying) return;
        string expectedProfile = SessionState.GetString(Prefix + "profile", string.Empty);
        if (!continued && YQTitleScreenUI.Instance != null)
        {
            if (YQTitleScreenUI.StartupFlowComplete)
            { Finish(false, "Startup was already complete before the requested Continue action."); return; }
            continued = YQTitleScreenUI.Instance.ContinueExistingForDevelopmentVerification(expectedProfile);
            if (continued) Debug.Log("[YQFrontierSpawnWitness] Normal title Continue action dispatched.");
        }
        if (EditorApplication.timeSinceStartup < nextObservation) return;
        nextObservation = EditorApplication.timeSinceStartup + 10d;
        try
        {
            var profiles = YQProfileSaveSystem.Instance;
            var builder = YQGeneratedWorldRuntimeBuilder.Instance;
            bool released = YourQuestTutorialAutoBootstrap.GameplayPresentationReleased;
            var world = WorldStateManager.Instance?.State;
            var plan = world?.generatedWorldPlan;
            var owners = new List<object>();
            int settlements = 0, hostileSites = 0, pois = 0, visibleNpc = 0, visibleEnemies = 0;
            var prepared = plan != null && YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var found, out _) ? found : null;
            foreach (var owner in UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string semanticId = SemanticId?.GetValue(owner) as string;
                string kind = "Unmatched";
                if (prepared != null && prepared.TryGetSiteBySemanticId(semanticId, out var site)) kind = site.kind.ToString();
                int renderers = CountVisible(owner);
                int colliders = 0;
                foreach (var collider in owner.GetComponentsInChildren<Collider>(false)) if (collider.enabled && !collider.isTrigger) colliders++;
                bool physical = owner.IsLoaded && owner.gameObject.activeInHierarchy && renderers > 0 && colliders > 0;
                if (physical && kind == YQSiteKindV2.Settlement.ToString()) settlements++;
                if (physical && kind == YQSiteKindV2.HostileSite.ToString()) hostileSites++;
                if (physical && kind == YQSiteKindV2.PointOfInterest.ToString()) pois++;
                Vector3 position = owner.transform.position;
                owners.Add(new { semanticId, kind, loaded = owner.IsLoaded, active = owner.gameObject.activeInHierarchy,
                    renderers, colliders, x = position.x, y = position.y, z = position.z });
            }
            foreach (var npc in UnityEngine.Object.FindObjectsByType<NpcDialogueAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (CountVisible(npc) > 0 && BelongsToLoadedCanonicalSite(plan, npc.NpcId, false)) visibleNpc++;
            foreach (var enemy in UnityEngine.Object.FindObjectsByType<YQInvestorEnemy>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (!enemy.IsDead && CountVisible(enemy) > 0 &&
                    BelongsToLoadedCanonicalSite(plan, enemy.GetComponentInParent<EntityInfo>(true)?.entityId, true)) visibleEnemies++;
            bool ownerMatches = profiles != null && profiles.ActiveProfileId == expectedProfile && world?.worldIdentity?.ownerProfileId == expectedProfile;
            bool success = continued && released && ownerMatches && settlements > 0 && hostileSites > 0 && pois > 0 && visibleNpc > 0 && visibleEnemies > 0 && Errors.Count == 0;
            string status = success ? "ACTUAL_SPAWNING_OBSERVED" : "WAITING_FOR_REQUIRED_PHYSICAL_CONTENT";
            WriteObservation(status, new { continued, released, ownerMatches, activeProfile = profiles?.ActiveProfileId,
                worldId = world?.worldIdentity?.worldId, worldSeed = plan?.worldSeed, baseHash = plan?.spatialPlanV2?.contentHash,
                continuationRevision = plan?.spatialPlanV2?.acceptedContinuation?.revision ?? 0,
                materializing = builder != null && builder.IsMaterializationInProgress,
                materializationFailed = builder != null && builder.HasMaterializationFailed,
                restoreFailure = builder?.LastProfileTerrainRestoreFailureReason, settlements, hostileSites, pois, visibleNpc, visibleEnemies, owners });
            Debug.Log("[YQFrontierSpawnWitness] " + status + " released=" + released + " settlement=" + settlements + " hostile=" + hostileSites + " poi=" + pois + " npc=" + visibleNpc + " enemy=" + visibleEnemies);
            if (success) Finish(true, "Production Continue exposed all required physical categories at the saved player location.");
            else if (builder != null && builder.HasMaterializationFailed) Finish(false, "Production materialization rejected; inspect the fresh runtime log and observation.");
            else if (DateTime.UtcNow - DateTime.Parse(SessionState.GetString(Prefix + "started", string.Empty), null, System.Globalization.DateTimeStyles.RoundtripKind) > TimeSpan.FromMinutes(20))
                Finish(false, "The required physical categories were not all observed within 20 minutes; no spawn readiness is claimed.");
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(false, exception.Message); }
    }

    private static int CountVisible(Component root)
    {
        int count = 0;
        foreach (var renderer in root.GetComponentsInChildren<Renderer>(false)) if (renderer.enabled && renderer.gameObject.activeInHierarchy) count++;
        return count;
    }

    private static bool BelongsToLoadedCanonicalSite(GeneratedWorldPlanRecord plan, string npcId, bool hostile)
    {
        // note: A dialogue-only Goddess or unrelated actor cannot satisfy settlement population evidence; require an accepted identity and its real loaded site.
        if (plan == null || string.IsNullOrWhiteSpace(npcId)) return false;
        if (plan.generatedNpcs != null)
            foreach (var npc in plan.generatedNpcs)
                if (Matches(npc)) return true;
        var continuation = plan.spatialPlanV2?.acceptedContinuation;
        if (continuation?.state == YQSpatialContinuationStateV2.Accepted && continuation.locations != null)
            foreach (var location in continuation.locations)
                if (location?.state == YQSpatialContinuationStateV2.Accepted && location.population != null)
                    foreach (var npc in location.population)
                        if (Matches(npc)) return true;
        return false;

        bool Matches(GeneratedNpcPlanRecord npc)
        {
            if (npc == null || npc.npcId != npcId || npc.hostile != hostile) return false;
            string parent = hostile ? npc.encampmentId : npc.settlementId;
            return !string.IsNullOrWhiteSpace(parent) && YQCompiledWorldSiteInstance.IsSiteLoaded(parent);
        }
    }

    private static void WriteObservation(string status, object observed)
    {
        // note: Record fresh executing identities beside actual object counts; this is assisted startup evidence, not human camera, travel, combat, or performance acceptance.
        var hashes = new Dictionary<string, string>();
        foreach (string path in new[] { "Assets/Assets/Scripts/Generated/Editor/YQFrontierSpawnRuntimeVerification.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs", "Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs",
            "Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs", "Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs" })
        {
            using var digest = SHA256.Create();
            hashes[path] = BitConverter.ToString(digest.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }
        File.WriteAllText(SessionState.GetString(Prefix + "path", string.Empty), JsonConvert.SerializeObject(new {
            status, utc = DateTime.UtcNow.ToString("O"), startedUtc = SessionState.GetString(Prefix + "started", string.Empty),
            evidence = "ASSISTED_PRODUCTION_CONTINUE_ACTUAL_SCENE_OBJECTS_NOT_ORDINARY_TRAVEL_OR_VISUAL_ACCEPTANCE",
            scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, frame = Time.frameCount,
            runtimeMvid = typeof(YQGeneratedWorldRuntimeBuilder).Assembly.ManifestModule.ModuleVersionId,
            editorMvid = typeof(YQFrontierSpawnRuntimeVerification).Assembly.ManifestModule.ModuleVersionId,
            hashes, observed, errors = Errors.ToArray()
        }, Formatting.Indented));
    }

    private static void Finish(bool success, string reason)
    {
        if (terminal) return;
        terminal = true;
        Debug.Log("[YQFrontierSpawnWitness] TERMINAL success=" + success + " reason=" + reason);
        string path = SessionState.GetString(Prefix + "path", string.Empty);
        if (File.Exists(path))
        {
            var receipt = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(path));
            receipt["terminal"] = true;
            receipt["success"] = success;
            receipt["reason"] = reason;
            File.WriteAllText(path, receipt.ToString(Formatting.Indented));
        }
        SessionState.SetBool(Prefix + "success", success);
        SessionState.SetBool(Prefix + "ending", true);
        EditorApplication.isPlaying = false;
    }
}
#endif
