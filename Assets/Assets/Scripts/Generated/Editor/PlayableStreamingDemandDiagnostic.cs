#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: This paused, explicitly invoked observer reports existing demand; it never admits work or changes accepted state.
public static class PlayableStreamingDemandDiagnostic
{
    private const string ProfileId = "18452e6b66b444a3935c9a3118a2f5e9";
    private const string ProfileName = "Sol61 Voice Control 20261009";
    private const string PlaySafe = "Assets/Assets/Scenes/YourQuest_PlaySafe.unity";
    private const int MaximumCellsPerField = 4096;
    // note: Match canonical owner hashing so Unity vector derived properties cannot recurse during observation.
    private static readonly JsonSerializerSettings Canonical = new JsonSerializerSettings
    {
        Formatting = Formatting.None, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    [MenuItem("YourQuest/Playable Evaluation/Streaming/Capture Read-Only Demand")]
    public static void CaptureReadOnlyDemand()
    {
        Require(EditorApplication.isPlaying && EditorApplication.isPaused &&
            !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            SceneManager.GetActiveScene().path == PlaySafe, "Requires paused stable PlaySafe.");
        var owner = YQProfileSaveSystem.Instance;
        var player = PlayerStateManager.Instance?.state;
        var world = WorldStateManager.Instance?.State;
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        Require(owner != null && player != null && world != null && streamer != null &&
            owner.ActiveProfileId == ProfileId && player.playerId == ProfileId &&
            world.worldIdentity?.ownerProfileId == ProfileId &&
            owner.Profiles.Count(profile => profile != null && profile.profileId == ProfileId &&
                profile.displayName == ProfileName) == 1, "Requires the existing disposable control and its canonical owners.");

        string playerBefore = HashText(JsonConvert.SerializeObject(player, Canonical));
        string worldBefore = HashText(JsonConvert.SerializeObject(world, Canonical));
        string commitBefore = owner.ActiveCommitId;
        int revisionBefore = owner.ActiveRevision, epochBefore = YQServiceLifecycle.RequestEpoch;
        int frameBefore = Time.frameCount;

        // note: Copy only typed coordinate collections; unknown observations fail instead of looking like an empty demand set.
        var sets = new Dictionary<string, HashSet<Vector2Int>>();
        foreach (string field in new[] { "_guaranteedViewDemand", "_hardViewDemand",
            "_provisionalGroundTurnBufferDemand", "_highSpeedTurnBufferCoverageDemand",
            "_provisionalGroundPrefetchDemand", "_sharedSiteOwnerDemand", "_sharedSiteVisibleOwners",
            "_siteTerrainDependencies", "_siteTerrainHandoffRequests", "_contentDemand",
            "_publicationVerificationDemand" })
            sets.Add(field, ReadCells(streamer, field));
        var admitted = ReadCells(streamer, "_physicalDemand");
        var union = new HashSet<Vector2Int>();
        foreach (var cells in sets.Values) union.UnionWith(cells);
        var rows = sets.ToDictionary(pair => pair.Key, pair => (object)new
        {
            count = pair.Value.Count,
            cells = Coordinates(pair.Value),
            missingFromAdmittedCount = pair.Value.Count(cell => !admitted.Contains(cell)),
            missingFromAdmitted = Coordinates(pair.Value.Where(cell => !admitted.Contains(cell)))
        });
        var streamerAssembly = typeof(YQPlayerFollowingSemanticChunkStreamer).Assembly;
        var sourceHashes = new Dictionary<string, string>();
        foreach (string name in new[] { "YQPlayerFollowingSemanticChunkStreamer.cs",
            "YQContinuousWorldFeatureAuthority.cs", "YQGeneratedWorldRuntimeBuilder.cs" })
        {
            string path = Path.Combine(Application.dataPath, "Assets/Scripts/Generated", name);
            sourceHashes.Add(path, HashFile(path));
        }
        string observerPath = Path.Combine(Application.dataPath,
            "Assets/Scripts/Generated/Editor/PlayableStreamingDemandDiagnostic.cs");
        sourceHashes.Add(observerPath, HashFile(observerPath));

        // note: Pausing Unity does not certify worker quiescence; retain actual lifetime snapshots separately from cell membership.
        var lifetimes = new
        {
            height = YQPlayerFollowingSemanticChunkStreamer.CaptureHeightWorkerLifetime(),
            frontier = YQGeneratedWorldRuntimeBuilder.CaptureFrontierWorkerLifetime(),
            biome = YQContinuousWorldFeatureMaterializer.CaptureBiomeWorkerLifetime()
        };
        bool unchanged = ReferenceEquals(player, PlayerStateManager.Instance.state) &&
            ReferenceEquals(world, WorldStateManager.Instance.State) &&
            ReferenceEquals(streamer, YQPlayerFollowingSemanticChunkStreamer.Active) &&
            owner.ActiveProfileId == ProfileId && owner.ActiveCommitId == commitBefore &&
            owner.ActiveRevision == revisionBefore && YQServiceLifecycle.RequestEpoch == epochBefore &&
            Time.frameCount == frameBefore && EditorApplication.isPlaying && EditorApplication.isPaused &&
            playerBefore == HashText(JsonConvert.SerializeObject(player, Canonical)) &&
            worldBefore == HashText(JsonConvert.SerializeObject(world, Canonical));
        var report = new
        {
            utc = DateTime.UtcNow.ToString("O"),
            evidence = "PAUSED_READ_ONLY_CURRENT_DEMAND_DIAGNOSIS_NOT_GAMEPLAY_ACCEPTANCE_OR_QUIESCENCE",
            profileId = ProfileId, commitBefore, revisionBefore, epochBefore, frameBefore,
            acceptedWorldSeed = world.generatedWorldPlan?.worldSeed,
            runtimeMvid = typeof(YQPlayerFollowingSemanticChunkStreamer).Module.ModuleVersionId.ToString(),
            runtimeSha256 = HashFile(streamerAssembly.Location), sourceHashes,
            canonicalUnchanged = unchanged, playerBefore, worldBefore,
            streamer.chunkWorldSize, streamer.guaranteedVisualDistanceMeters,
            current = new[] { streamer.CurrentChunk.x, streamer.CurrentChunk.y },
            reportedOverflow = streamer.PhysicalDemandOverflowCount,
            criticalOverflowFlag = Read(streamer, "_physicalDemandVisibleOverflow"),
            admittedCount = admitted.Count, admitted = Coordinates(admitted),
            observedUnionCount = union.Count,
            uniqueObservedCellsNotAdmitted = Coordinates(union.Where(cell => !admitted.Contains(cell))),
            demand = rows, lifetimes,
            physicalOwnerCapacity = ReadPhysicalCapacity(streamer),
            cachedPhysicalOwnerCount = streamer.PhysicalChunkCount,
            activeChunkCount = streamer.ActiveChunkCount, retainedChunkCount = streamer.RetainedChunkCount,
            limitations = "Membership is a point-in-time observation. Sets overlap; overflow is attempted insertions, not a unique count. No admission, cancellation, collection clearing, save, or fixture movement is invoked. Async tasks and external publishers are not universally certified idle."
        };
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath,
            "../Docs/PlayableEvaluation_2026-10-09/reviews/streaming_resume_diagnosis/captures"));
        Directory.CreateDirectory(directory);
        string output = Path.Combine(directory, "demand-" + Guid.NewGuid().ToString("N") + ".json");
        using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(file))
            writer.Write(JsonConvert.SerializeObject(report, Formatting.Indented, Canonical));
        Require(unchanged, "Canonical/frame ownership changed during observation; retained capture is not stable.");
        Debug.Log("[PlayableStreamingDemandDiagnostic] Read-only demand capture: " + output);
    }

    private static int ReadPhysicalCapacity(YQPlayerFollowingSemanticChunkStreamer streamer)
    {
        // note: This exact inspected getter only computes the existing capacity formula; unknown contracts fail closed.
        var property = typeof(YQPlayerFollowingSemanticChunkStreamer).GetProperty("PhysicalOwnerCapacity",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Require(property != null && property.PropertyType == typeof(int) && property.GetIndexParameters().Length == 0,
            "Unknown existing physical-capacity observation.");
        return (int)property.GetValue(streamer);
    }

    private static object Read(object owner, string name)
    {
        FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(field != null, "Unknown diagnostic field: " + name);
        return field.GetValue(owner);
    }

    private static HashSet<Vector2Int> ReadCells(object owner, string name)
    {
        object value = Read(owner, name);
        IEnumerable values = value is IDictionary dictionary ? dictionary.Keys : value as IEnumerable;
        Require(values != null, "Unknown coordinate collection: " + name);
        var result = new HashSet<Vector2Int>();
        foreach (object candidate in values)
        {
            Require(candidate is Vector2Int && result.Count < MaximumCellsPerField,
                "Unknown coordinate or diagnostic bound exceeded: " + name);
            result.Add((Vector2Int)candidate);
        }
        return result;
    }

    private static int[][] Coordinates(IEnumerable<Vector2Int> cells) =>
        cells.OrderBy(cell => cell.x).ThenBy(cell => cell.y).Select(cell => new[] { cell.x, cell.y }).ToArray();

    private static string HashText(string text)
    {
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)))
                .Replace("-", "").ToLowerInvariant();
    }

    private static string HashFile(string path)
    {
        using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var algorithm = SHA256.Create())
            return BitConverter.ToString(algorithm.ComputeHash(file)).Replace("-", "").ToLowerInvariant();
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
