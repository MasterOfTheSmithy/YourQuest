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

// note: An explicit request exercises loaded production methods against disposable fixture files, never an active player's profile.
[InitializeOnLoad]
internal static class YQContinuationPublicationRepairVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/ContinuationPublicationRepair.request";
    private const string Output = "outputs/Continuation_Publication_Repair_20261006";
    private const BindingFlags HiddenStatic = BindingFlags.Static | BindingFlags.NonPublic;
    static YQContinuationPublicationRepairVerification() => EditorApplication.update += Tick;
    private static void Tick()
    {
        if (EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        AssetDatabase.DeleteAsset(Request);
        Run();
    }
    private static void Run()
    {
        var checks = new List<object>(); int failed = 0;
        void Check(string name, bool pass) { checks.Add(new { name, passed = pass }); if (!pass) failed++; }
        string root = Path.GetFullPath(Path.Combine(Output, "NativeFixtures", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff")));
        var stage = typeof(YQProfileCommitStore).GetMethod("TryStageContinuationCommit", HiddenStatic);
        var refresh = typeof(YQProfileCommitStore).GetMethod("TryRefreshPublishedContinuationProjections", HiddenStatic);
        var auxiliary = new Dictionary<string, string> { { "dialogue", "fixture-auxiliary" } };
        var playerOwner = PlayerStateManager.Instance; var worldOwner = WorldStateManager.Instance;
        var profileOwner = YQProfileSaveSystem.Instance;
        try
        {
            Directory.CreateDirectory(root);
            Check("stage and refresh APIs loaded from current runtime assembly", stage != null && refresh != null);
            bool Stage(string folder, Action<YQProfileCommitPoint> fault, out YQProfileTransactionReceipt receipt)
            {
                object[] args = { folder, "publication-fixture", 2, "new-player", "new-world", auxiliary, fault, null };
                bool result = (bool)stage.Invoke(null, args); receipt = (YQProfileTransactionReceipt)args[7]; return result;
            }
            bool Refresh(string folder, string playerPath, string worldPath, YQProfileTransactionReceipt receipt)
            {
                object[] args = { folder, playerPath, worldPath, receipt, null };
                return (bool)refresh.Invoke(null, args);
            }
            foreach (YQProfileCommitPoint point in new[] { YQProfileCommitPoint.PlayerStaged, YQProfileCommitPoint.WorldStaged,
                YQProfileCommitPoint.AuxiliaryStaged, YQProfileCommitPoint.RevisionPublished })
            {
                string folder = Path.Combine(root, point.ToString()); Directory.CreateDirectory(folder);
                string player = Path.Combine(folder, "active-player.json"), world = Path.Combine(folder, "active-world.json");
                Check("baseline paired revision writes " + point, YQProfileCommitStore.TryCommit(folder, "publication-fixture", player, world,
                    1, "old-player", "old-world", auxiliary, null, out var baseline));
                File.WriteAllText(player + ".bak", "old-player-backup"); File.WriteAllText(world + ".bak", "old-world-backup");
                bool reached = false;
                bool result = Stage(folder, p => { if (p == point) { reached = true; throw new IOException("Explicit fixture fault " + p); } }, out var receipt);
                Check("staging fault rejects without published receipt " + point, reached && !result && !receipt.published);
                Check("staging fault leaves working copies and backups exact " + point,
                    File.ReadAllText(player) == "old-player" && File.ReadAllText(world) == "old-world" &&
                    File.ReadAllText(player + ".bak") == "old-player-backup" && File.ReadAllText(world + ".bak") == "old-world-backup");
                Check("prior immutable pair stays readable " + point, YQProfileCommitStore.TryReadCommit(folder, baseline.commitId,
                    baseline.playerChecksum, baseline.worldChecksum, out _, out _, out _));
            }
            string success = Path.Combine(root, "success"); Directory.CreateDirectory(success);
            string activePlayer = Path.Combine(success, "active-player.json"), activeWorld = Path.Combine(success, "active-world.json");
            var ordinaryPoints = new List<YQProfileCommitPoint>();
            Check("ordinary TryCommit compatibility wrapper succeeds", YQProfileCommitStore.TryCommit(success, "publication-fixture", activePlayer,
                activeWorld, 1, "old-player", "old-world", auxiliary, p => ordinaryPoints.Add(p), out var ordinary));
            Check("ordinary fault-point order preserved", ordinary.published && ordinaryPoints.SequenceEqual((YQProfileCommitPoint[])Enum.GetValues(typeof(YQProfileCommitPoint))));
            var stagePoints = new List<YQProfileCommitPoint>();
            Check("successful continuation stage stays unpublished", Stage(success, p => stagePoints.Add(p), out var staged) && !staged.published);
            Check("stage never invokes projection or pointer fault points", stagePoints.SequenceEqual(new[] { YQProfileCommitPoint.PlayerStaged,
                YQProfileCommitPoint.WorldStaged, YQProfileCommitPoint.AuxiliaryStaged, YQProfileCommitPoint.RevisionPublished }));
            Check("successful stage does not replace working pair", File.ReadAllText(activePlayer) == "old-player" && File.ReadAllText(activeWorld) == "old-world");
            Check("staged complete revision is readable", YQProfileCommitStore.TryReadCommit(success, staged.commitId, staged.playerChecksum,
                staged.worldChecksum, out _, out _, out _));
            Check("unpublished receipt cannot refresh working copies", !Refresh(success, activePlayer, activeWorld, staged) && !staged.published &&
                File.ReadAllText(activePlayer) == "old-player" && File.ReadAllText(activeWorld) == "old-world");
            // note: Marking the fixture receipt published models the store's caller precondition; this is not a real profile manifest publication test.
            staged.published = true;
            Check("published store receipt refreshes paired projections", Refresh(success, activePlayer, activeWorld, staged) && staged.published &&
                File.ReadAllText(activePlayer) == "new-player" && File.ReadAllText(activeWorld) == "new-world");
            Check("refresh retains prior working copies as backups", File.ReadAllText(activePlayer + ".bak") == "old-player" && File.ReadAllText(activeWorld + ".bak") == "old-world");
            string obstructed = Path.Combine(success, "obstructed-world"); Directory.CreateDirectory(obstructed);
            Check("refresh I/O failure does not revoke published truth", !Refresh(success, activePlayer, obstructed, staged) && staged.published);
            Check("refresh failure leaves accepted immutable revision readable", YQProfileCommitStore.TryReadCommit(success, staged.commitId,
                staged.playerChecksum, staged.worldChecksum, out _, out string committedWorld, out _));
            File.AppendAllText(committedWorld, "fixture-tamper");
            Check("checksum failure refuses refresh before either working copy changes", !Refresh(success, activePlayer, activeWorld, staged) && staged.published &&
                File.ReadAllText(activePlayer) == "new-player" && File.ReadAllText(activeWorld) == "new-world");

            var recovery = typeof(YQProfileSaveSystem).GetMethod("HasSameAcceptedContinuationForRecovery", HiddenStatic);
            bool Same(WorldState a, WorldState b) => (bool)recovery.Invoke(null, new object[] { a, b });
            // note: Fixture cloning uses the production world serializer, including Unity vector converters.
            var worldSettings = (JsonSerializerSettings)typeof(WorldStateManager).GetField("JsonSettings", HiddenStatic).GetValue(null);
            WorldState Clone(WorldState world) => JsonConvert.DeserializeObject<WorldState>(JsonConvert.SerializeObject(world, worldSettings), worldSettings);
            var original = new WorldState { generatedWorldPlan = new GeneratedWorldPlanRecord { spatialPlanV2 = new GeneratedSpatialWorldPlanV2Record() } };
            var candidate = Clone(original);
            Check("recovery accepts two worlds without continuation", Same(candidate, original));
            original.generatedWorldPlan.spatialPlanV2.acceptedContinuation = new GeneratedSpatialContinuationV2Record { revision = 1,
                state = YQSpatialContinuationStateV2.Accepted, contentHash = "fixture-hash", locations = new List<GeneratedSpatialContinuationLocationV2Record>() };
            Check("recovery rejects missing candidate continuation", !Same(candidate, original));
            candidate = Clone(original);
            Check("recovery accepts exact manifest-listed continuation", Same(candidate, original));
            candidate.generatedWorldPlan.spatialPlanV2.acceptedContinuation.revision++;
            Check("recovery rejects a newer unlisted continuation", !Same(candidate, original));
            candidate = Clone(original);
            candidate.generatedWorldPlan.spatialPlanV2.acceptedContinuation.locations.Add(new GeneratedSpatialContinuationLocationV2Record { contentId = "leaked" });
            Check("recovery rejects payload drift despite unchanged checksum text", !Same(candidate, original));
            Check("recovery rejects leaked continuation against absent committed continuation", !Same(candidate, new WorldState()));

            var preview = EditorSceneManager.NewPreviewScene(); GameObject inactive = null;
            try
            {
                // note: Inactive preview components exercise only the early paired-save barrier; Awake, profile loading and live publisher admission never run.
                inactive = new GameObject("ContinuationBarrierFixture") { hideFlags = HideFlags.HideAndDontSave }; inactive.SetActive(false);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inactive, preview);
                var profiles = inactive.AddComponent<YQProfileSaveSystem>();
                var barrier = typeof(YQProfileSaveSystem).GetField("_continuationReloadRequired", BindingFlags.Instance | BindingFlags.NonPublic);
                barrier.SetValue(profiles, true);
                bool saved = (bool)typeof(YQProfileSaveSystem).GetMethod("SaveProfileCore", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(profiles, new object[] { "inactive-barrier-fixture" });
                Check("paired-save barrier rejects ambiguous live state before profile I/O", !saved && profiles.LastFailure.Contains("requires profile reload") && (bool)barrier.GetValue(profiles));
            }
            finally { if (inactive != null) UnityEngine.Object.DestroyImmediate(inactive); EditorSceneManager.ClosePreviewScene(preview); }
            Check("canonical owner references unchanged", ReferenceEquals(playerOwner, PlayerStateManager.Instance) &&
                ReferenceEquals(worldOwner, WorldStateManager.Instance) && ReferenceEquals(profileOwner, YQProfileSaveSystem.Instance));
        }
        catch (Exception error) { Check("fixture completed without unexpected exception: " + error, false); }
        File.WriteAllText(Path.Combine(Output, "Local_Native_Contracts.json"), JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow, evidence = "DETACHED_EDITOR_STORE_IO_AND_RECOVERY_PREDICATES_NOT_ACTUAL_PUBLISHER_OR_GAMEPLAY",
            runtimeMvid = typeof(YQProfileCommitStore).Module.ModuleVersionId, root, passed = checks.Count - failed, failed, checks }, Formatting.Indented));
        if (failed == 0) Debug.Log("[YQContinuationPublicationRepair] " + checks.Count + " detached native checks PASS; no user profile touched.");
        else Debug.LogError("[YQContinuationPublicationRepair] " + failed + " detached native checks failed.");
    }
}
#endif
