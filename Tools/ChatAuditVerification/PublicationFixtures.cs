// note: Test-only owners intentionally contain small documents. Current production publisher methods and the complete commit store are compiled verbatim by Verify.cs.
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace UnityEngine
{
    public static class Debug { public static void LogWarning(string message) { } }
}

public sealed class PlayerState
{
    public int schemaVersion = YQStateContract.CurrentStateSchemaVersion;
    public string playerId = "fixture";
    public int health = 100;
}
public sealed class WorldIdentity { public string ownerProfileId = "fixture"; public string worldId = "fixture-world"; }
public sealed class WorldState { public WorldIdentity worldIdentity = new WorldIdentity(); public int revision = 1; }
public sealed class GeneratedSpatialContinuationV2Record { public string id = "accepted-fixture"; }
public sealed class PlayerStateManager
{
    public static PlayerStateManager Instance;
    public bool isActiveAndEnabled = true;
    public PlayerState state = new PlayerState();
    public int joins;
    public bool TryFlushPendingAutosave(out string failure) { joins++; failure = ""; return true; }
}
public sealed class WorldStateManager
{
    public static WorldStateManager Instance;
    public bool isActiveAndEnabled = true;
    public WorldState State = new WorldState();
    public int applications;
    public bool failApply;
    public sealed class PreparedContinuationBaseWorld { }
    public sealed class PreparedContinuationSnapshot
    {
        public WorldState source;
        public string sourceJson;
        public string worldJson;
    }
    public bool TryPrepareContinuationSnapshot(WorldState source, PlayerState paired, GeneratedSpatialContinuationV2Record accepted,
        out PreparedContinuationSnapshot snapshot, out string failure, YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        PreparedContinuationBaseWorld baseWorld)
    {
        snapshot = new PreparedContinuationSnapshot { source = source, sourceJson = JsonConvert.SerializeObject(source),
            worldJson = JsonConvert.SerializeObject(new WorldState { revision = source.revision + 1 }) };
        return TryValidateContinuationSnapshot(snapshot, out failure);
    }
    public bool TryValidateContinuationSnapshot(PreparedContinuationSnapshot snapshot, out string failure)
    {
        failure = "World fixture changed";
        return snapshot != null && ReferenceEquals(State, snapshot.source) && JsonConvert.SerializeObject(State) == snapshot.sourceJson;
    }
    public void ReserveContinuationPublication(PreparedContinuationSnapshot snapshot) { }
    public void ApplyPublishedContinuation(PreparedContinuationSnapshot snapshot)
    {
        if (failApply) throw new InvalidOperationException("fixture apply failure");
        State.revision++; applications++;
    }
}
public static class YQServiceLifecycle
{
    public static int RequestEpoch;
    public static bool IsCurrent(int epoch) => epoch == RequestEpoch;
}
public static class YQDeveloperConsoleGate { public static bool BlocksPersistence; }
public sealed class YQPlayerFollowingSemanticChunkStreamer
{
    public static YQPlayerFollowingSemanticChunkStreamer Active = new YQPlayerFollowingSemanticChunkStreamer();
    public bool enabled = true;
}
public static class YQGeneratedWorldRuntimeBuilder
{
    public sealed class FrontierConstructionAcceptance
    {
        public bool valid = true;
        public GeneratedSpatialContinuationV2Record accepted = new GeneratedSpatialContinuationV2Record();
        public bool TryValidateForPublication(WorldState world, out GeneratedSpatialContinuationV2Record value, out string failure)
        { value = accepted; failure = valid ? "" : "fixture construction invalid"; return valid; }
    }
    public static class FrontierPublicationDiagnostics
    {
        public enum Operation { ProfileDiskProof, AuxiliaryCapture, RevisionStage, ManifestPointer, WorkingProjection }
        public static int diskProofs;
        public static Action afterStage;
        public static IDisposable Begin(Operation operation)
        {
            if (operation == Operation.ProfileDiskProof) diskProofs++;
            return new Scope(operation);
        }
        private sealed class Scope : IDisposable
        {
            private readonly Operation operation;
            public Scope(Operation operation) { this.operation = operation; }
            public void Dispose() { if (operation == Operation.RevisionStage) afterStage?.Invoke(); }
        }
    }
}

public sealed partial class YQProfileSaveSystem
{
    public static YQProfileSaveSystem Instance;
    public bool isActiveAndEnabled = true;
    public string directory;
    public bool failPointer;
    public string LastFailure = "";
    public YQProfileTransactionReceipt LastTransactionReceipt;
    public int SuccessfulSaveCount;
    public string LastSavedProfileId;
    public bool ReloadRequired => _continuationReloadRequired;
    public int ActiveRevision => _manifest.activeRevision;
    private static readonly JsonSerializerSettings ContinuationJsonSettings = new JsonSerializerSettings {
        Formatting = Formatting.None, ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
    private static readonly JsonSerializerSettings ContinuationPlayerJsonSettings = new JsonSerializerSettings {
        Formatting = Formatting.Indented, ReferenceLoopHandling = ReferenceLoopHandling.Ignore };
    private string ManifestPath => Path.Combine(directory, "manifest.json");
    private string ActivePlayerPath => Path.Combine(directory, "player.json");
    private string ActiveWorldPath => Path.Combine(directory, "world.json");
    private string GetProfileFolder(string id) => Path.Combine(directory, id);
    private bool SaveManifest(ProfileManifest manifest)
    {
        if (failPointer) { LastFailure = "fixture pointer failure"; return false; }
        File.WriteAllText(ManifestPath, JsonConvert.SerializeObject(manifest));
        return true;
    }
    public void AddProvider(string id, Func<string> provider) => _auxiliaryProviders[id] = provider;
    public void RemoveProviders() => _auxiliaryProviders.Clear();
    public string PreviousWorldPath()
    {
        var previous = FindLatestCommit("fixture");
        YQProfileCommitStore.TryReadCommit(GetProfileFolder("fixture"), previous.commitId, previous.playerChecksum, previous.worldChecksum,
            out _, out string path, out _);
        return path;
    }
    public void CorruptDurableManifest() => File.WriteAllText(ManifestPath, "{}");
    public void Initialize(string root)
    {
        directory = root; Directory.CreateDirectory(root);
        Instance = this;
        PlayerStateManager.Instance = new PlayerStateManager();
        WorldStateManager.Instance = new WorldStateManager();
        YQServiceLifecycle.RequestEpoch = 1;
        YQDeveloperConsoleGate.BlocksPersistence = false;
        YQPlayerFollowingSemanticChunkStreamer.Active = new YQPlayerFollowingSemanticChunkStreamer();
        YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics.afterStage = null;
        YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics.diskProofs = 0;
        _manifest = new ProfileManifest();
        _manifest.profiles.Add(new ProfileEntry { profileId = "fixture", displayName = "Fixture" });
        if (!YQProfileCommitStore.TryCommit(GetProfileFolder("fixture"), "fixture", ActivePlayerPath, ActiveWorldPath, 1,
            JsonConvert.SerializeObject(PlayerStateManager.Instance.state), JsonConvert.SerializeObject(WorldStateManager.Instance.State),
            new Dictionary<string, string> { { "dialogue", "baseline-auxiliary" } }, null, out var baseline))
            throw new InvalidOperationException("Fixture baseline failed: " + baseline.failure);
        _manifest = _manifest.PrepareCommit(baseline); SaveManifest(_manifest);
    }
}

public static class PublicationFixtures
{
    public static int Run(string output)
    {
        var results = new List<object>(); int failures = 0;
        void Check(string name, bool passed) { results.Add(new { name, passed }); if (!passed) { failures++; Console.WriteLine("FAIL: " + name); } }
        string root = Path.Combine(output, "publication-fixtures-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        YQProfileSaveSystem New(string name)
        { var owner = new YQProfileSaveSystem(); owner.Initialize(Path.Combine(root, name)); return owner; }
        var token = new YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance();
        var immediate = New("immediate"); int calls = 0;
        immediate.AddProvider("dialogue", () => { calls++; return "current-auxiliary"; });
        bool published = immediate.TryPrepareAndPublishContinuation(token, null, () => "", out var receipt, out string failure);
        Check("immediate publication succeeds with real paired revision files", published && receipt.published && immediate.ActiveRevision == 2 && WorldStateManager.Instance.applications == 1);
        Check("immediate publication uses four disk proofs and two auxiliary captures", calls == 2 && YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics.diskProofs == 4);
        Check("immediate path joins existing player writer once", PlayerStateManager.Instance.joins == 1);

        var retained = New("retained"); calls = 0;
        retained.AddProvider("dialogue", () => { calls++; return "current-auxiliary"; });
        Check("retained preparation succeeds", retained.TryPrepareContinuationPublication(token, out var preparation, out failure));
        Check("retained token publication succeeds", retained.TryPublishContinuation(preparation, out receipt, out failure) && receipt.published);
        Check("retained tokens keep six disk proofs and three auxiliary captures", calls == 3 && YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics.diskProofs == 6);
        Check("consumed token cannot publish twice", !retained.TryPublishContinuation(preparation, out _, out _));

        var changed = New("retained-player-change");
        changed.TryPrepareContinuationPublication(token, out preparation, out _);
        PlayerStateManager.Instance.state.health--;
        Check("retained token rejects player changes", !changed.TryPublishContinuation(preparation, out _, out _) && changed.ActiveRevision == 1);

        var noGuard = New("missing-guard");
        Check("immediate path requires runtime guard", !noGuard.TryPrepareAndPublishContinuation(token, null, null, out _, out _) && noGuard.ActiveRevision == 1);
        var rejectedGuard = New("rejected-guard");
        Check("runtime guard veto prevents publication", !rejectedGuard.TryPrepareAndPublishContinuation(token, null, () => "fixture runtime invalid", out _, out failure) && failure.Contains("runtime invalid") && rejectedGuard.ActiveRevision == 1);

        foreach (string mutation in new[] { "player", "world", "epoch", "provider", "manifest", "construction" })
        {
            var owner = New("guard-" + mutation); var construction = new YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance();
            owner.AddProvider("dialogue", () => "current-auxiliary");
            string Mutate()
            {
                if (mutation == "player") PlayerStateManager.Instance.state.health--;
                if (mutation == "world") WorldStateManager.Instance.State.revision++;
                if (mutation == "epoch") YQServiceLifecycle.RequestEpoch++;
                if (mutation == "provider") owner.RemoveProviders();
                if (mutation == "manifest") owner.CorruptDurableManifest();
                if (mutation == "construction") construction.valid = false;
                return "";
            }
            Check("final preparation proof rejects runtime-guard mutation: " + mutation,
                !owner.TryPrepareAndPublishContinuation(construction, null, Mutate, out _, out _) && owner.ActiveRevision == 1 && WorldStateManager.Instance.applications == 0);
        }
        foreach (string mutation in new[] { "player", "world", "epoch", "provider", "manifest", "prior-file" })
        {
            var owner = New("after-stage-" + mutation); owner.AddProvider("dialogue", () => "current-auxiliary");
            string priorWorld = owner.PreviousWorldPath();
            YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics.afterStage = () => {
                if (mutation == "player") PlayerStateManager.Instance.state.health--;
                if (mutation == "world") WorldStateManager.Instance.State.revision++;
                if (mutation == "epoch") YQServiceLifecycle.RequestEpoch++;
                if (mutation == "provider") owner.RemoveProviders();
                if (mutation == "manifest") owner.CorruptDurableManifest();
                if (mutation == "prior-file") File.AppendAllText(priorWorld, "tampered");
            };
            Check("post-stage proof rejects mutation: " + mutation,
                !owner.TryPrepareAndPublishContinuation(token, null, () => "", out receipt, out _) && !receipt.published && owner.ActiveRevision == 1 && WorldStateManager.Instance.applications == 0);
        }
        foreach (string mutation in new[] { "player", "world", "epoch", "provider", "manifest", "document" })
        {
            var owner = New("recapture-" + mutation); int count = 0;
            owner.AddProvider("dialogue", () => {
                if (++count == 2)
                {
                    if (mutation == "player") PlayerStateManager.Instance.state.health--;
                    if (mutation == "world") WorldStateManager.Instance.State.revision++;
                    if (mutation == "epoch") YQServiceLifecycle.RequestEpoch++;
                    if (mutation == "provider") owner.RemoveProviders();
                    if (mutation == "manifest") owner.CorruptDurableManifest();
                    if (mutation == "document") return "changed";
                }
                return "current-auxiliary";
            });
            Check("post-provider proof rejects mutation: " + mutation,
                !owner.TryPrepareAndPublishContinuation(token, null, () => "", out receipt, out _) && !receipt.published && owner.ActiveRevision == 1 && WorldStateManager.Instance.applications == 0);
        }
        var lateRuntime = New("late-runtime-invalidation"); int runtimeCaptures = 0; bool runtimeCurrent = true;
        lateRuntime.AddProvider("dialogue", () => {
            if (++runtimeCaptures == 2) runtimeCurrent = false;
            return "current-auxiliary";
        });
        Check("same-JSON final auxiliary callback cannot invalidate runtime ownership and still publish",
            !lateRuntime.TryPrepareAndPublishContinuation(token, null, () => runtimeCurrent ? "" : "fixture runtime changed",
                out receipt, out failure) && !receipt.published && lateRuntime.ActiveRevision == 1 &&
                WorldStateManager.Instance.applications == 0 && runtimeCaptures == 2 && failure.Contains("runtime changed"));

        foreach (string mutation in new[] { "player", "world", "epoch", "provider", "manifest", "construction" })
        {
            var owner = New("final-runtime-guard-" + mutation); var construction = new YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance();
            int guardCalls = 0; owner.AddProvider("dialogue", () => "current-auxiliary");
            string Guard()
            {
                if (++guardCalls == 2)
                {
                    if (mutation == "player") PlayerStateManager.Instance.state.health--;
                    if (mutation == "world") WorldStateManager.Instance.State.revision++;
                    if (mutation == "epoch") YQServiceLifecycle.RequestEpoch++;
                    if (mutation == "provider") owner.RemoveProviders();
                    if (mutation == "manifest") owner.CorruptDurableManifest();
                    if (mutation == "construction") construction.valid = false;
                }
                return "";
            }
            Check("exact proof after final runtime guard rejects its mutation: " + mutation,
                !owner.TryPrepareAndPublishContinuation(construction, null, Guard, out receipt, out _) && !receipt.published &&
                owner.ActiveRevision == 1 && WorldStateManager.Instance.applications == 0 && guardCalls == 2);
        }
        var pointerFailure = New("pointer-failure"); pointerFailure.failPointer = true;
        Check("pointer failure leaves live world and prior revision unchanged", !pointerFailure.TryPrepareAndPublishContinuation(token, null, () => "", out receipt, out _) && !receipt.published && pointerFailure.ActiveRevision == 1 && WorldStateManager.Instance.applications == 0);
        var applyFailure = New("apply-failure"); WorldStateManager.Instance.failApply = true; bool threw = false;
        try { applyFailure.TryPrepareAndPublishContinuation(token, null, () => "", out _, out _); }
        catch (InvalidOperationException) { threw = true; }
        Check("post-pointer apply failure retains durable truth and requires reload", threw && applyFailure.LastTransactionReceipt.published && applyFailure.ActiveRevision == 2 && applyFailure.ReloadRequired && !YQPlayerFollowingSemanticChunkStreamer.Active.enabled);

        File.WriteAllText(Path.Combine(output, "publication-tests.json"), JsonConvert.SerializeObject(new {
            evidence = "VERBATIM_PRODUCTION_PUBLICATION_METHODS_AND_REAL_STORE_IO_WITH_CONTROLLED_UNITY_OWNER_FIXTURES_NOT_PLAYSAFE",
            passed = results.Count - failures, failures, results
        }, Formatting.Indented));
        Console.WriteLine("Publication regression fixtures: " + (results.Count - failures) + " passed; " + failures + " failed.");
        return failures;
    }
}
