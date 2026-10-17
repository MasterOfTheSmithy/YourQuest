#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Explicit, read-only captures inspect existing publication owners; they never refresh demand, publish content, or certify gameplay acceptance.
internal static class YQPlayableEvaluationDiagnostics
{
    private const string ControlProfile = "18452e6b66b444a3935c9a3118a2f5e9";

    [MenuItem("YourQuest/Playable Evaluation/Capture Control Startup Publication")]
    private static void CaptureControlStartupPublication()
    {
        if (!EditorApplication.isPlaying ||
            SceneManager.GetActiveScene().path != "Assets/Assets/Scenes/YourQuest_PlaySafe.unity" ||
            YQProfileSaveSystem.Instance == null || YQProfileSaveSystem.Instance.ActiveProfileId != ControlProfile)
            throw new InvalidOperationException("Capture requires the disposable control profile in ordinary PlaySafe.");
        YQPlayerFollowingSemanticChunkStreamer owner = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<YQPlayerFollowingSemanticChunkStreamer>(FindObjectsSortMode.None))
        {
            if (!candidate.isActiveAndEnabled) continue;
            if (owner != null) throw new InvalidOperationException("Multiple active streamer owners; capture refused.");
            owner = candidate;
        }
        if (owner == null) throw new InvalidOperationException("No active production streamer.");
        var snapshots = new List<object>();
        foreach (var coordinate in new[] { new Vector2Int(4, 7), new Vector2Int(5, 7), new Vector2Int(6, 8) })
        {
            // note: These public snapshot/contract readers do not reconcile queues or mutate readiness.
            bool present = owner.TryGetPublicationSnapshot(coordinate, out var snapshot);
            bool contractValid = owner.TryValidatePublicationWorkContract(coordinate, out string failure);
            snapshots.Add(new { coordinate, present, contractValid, failure, snapshot });
        }
        string directory = Path.GetFullPath("outputs/PlayableEvaluation_20261009/startup");
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, "publication-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + ".json");
        string heartbeat = File.Exists("Temp/YQ_STREAMER_PLAYMODE.status")
            ? File.ReadAllText("Temp/YQ_STREAMER_PLAYMODE.status") : null;
        var receipt = new
        {
            utc = DateTime.UtcNow.ToString("O"), evidence = "INSTRUMENTED_ORDINARY_CONTROL_STARTUP_READ_ONLY",
            scene = SceneManager.GetActiveScene().path, profileId = ControlProfile,
            expectedAcceptedSeed = "72787057", heartbeat, snapshots,
            playableAcceptance = "NOT_VERIFIED", microphonePlaybackAcceptance = "NOT_VERIFIED"
        };
        using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream))
            writer.Write(JsonConvert.SerializeObject(receipt, Formatting.Indented));
        Debug.Log("[YQPlayableEvaluation] Read-only startup capture: " + destination);
    }
}
#endif
