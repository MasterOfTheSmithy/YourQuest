#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: Preparation only. A reviewed primary-owned Editor invocation must load this code;
// placing it under Tools does not install an Editor menu, create an owner, or execute a test.
public static class RealOfferOwnerChecks
{
    public enum Scenario
    {
        InvalidateFirstCapture, InvalidateSecondCapture,
        ThrowFirstCapture, ThrowSecondCapture,
        ReenterFirstCapture, ReenterSecondCapture,
        ThrowGuardAfterFirstCapture, ThrowGuardAfterSecondCapture,
        PointerReplaceDenied, ProjectionReplaceDenied, CommitAndReplay
    }

    public sealed class Request
    {
        public Scenario scenario;
        public string expectedProfileId;
        public string actualPendingOfferId;
        public string decision = "decline";
        public string expectedRuntimeMvid;
        public string expectedRuntimeSha256;
        public string outputDirectory;
        public string reviewedBackupReceipt;
        public bool primaryExclusiveQuiescenceConfirmed;
        public bool sharedFileLockFaultExplicitlyReviewed;
    }

    // note: Match the observed existing disposable profile exactly; never widen eligibility to another profile.
    private const string ProfileName = "Sol61 Voice Control 20261009";
    private const string DisposableProfileId = "18452e6b66b444a3935c9a3118a2f5e9";
    private const string PlaySafe = "Assets/Assets/Scenes/YourQuest_PlaySafe.unity";
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly JsonSerializerSettings Canonical = new JsonSerializerSettings
    {
        Formatting = Formatting.None, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    private sealed class Snapshot
    {
        public string profile, commit, diskCommit, player, world, lastFailure;
        public int revision, diskRevision, saveCount;
        public bool reload;
        public Dictionary<string, string> files;
        public string[] revisions;
    }

    // note: Every transaction below calls the actual singleton public owner overload. No candidate
    // domain, fake manifest, competing owner, or fabricated pending offer is used as a substitute.
    public static string Run(Request request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        Require(EditorApplication.isPlaying && EditorApplication.isPaused, "Requires primary-owned paused Play Mode.");
        Require(SceneManager.GetActiveScene().path == PlaySafe, "Requires production PlaySafe scene.");
        Require(request.primaryExclusiveQuiescenceConfirmed, "Primary must confirm no save/profile/service work is in flight.");
        Require(File.Exists(request.reviewedBackupReceipt), "Requires an existing primary-reviewed backup receipt.");
        Require(request.decision == "accept" || request.decision == "decline", "Invalid decision.");
        string output = Path.GetFullPath(request.outputDirectory ?? string.Empty);
        string outputRoot = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(),
            "Docs/PlayableEvaluation_2026-10-09/reviews/offer_owner_verification/runs"));
        Require(output.StartsWith(outputRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !Directory.Exists(output), "Output must be a fresh child of the owned runs directory.");

        var owner = YQProfileSaveSystem.Instance;
        var psm = PlayerStateManager.Instance;
        var wsm = WorldStateManager.Instance;
        Require(owner != null && owner.isActiveAndEnabled && psm != null && psm.isActiveAndEnabled &&
            wsm != null && wsm.isActiveAndEnabled, "Existing canonical live owners are required.");
        Require(!owner.RequiresProfileReload, "Resolve existing reload requirement before testing.");
        Require(typeof(YQProfileSaveSystem).Module.ModuleVersionId.ToString() == request.expectedRuntimeMvid,
            "Runtime assembly MVID differs from primary's coherent compiled receipt.");
        string runtimeAssembly = typeof(YQProfileSaveSystem).Assembly.Location;
        Require(!string.IsNullOrWhiteSpace(request.expectedRuntimeSha256) &&
            string.Equals(Hash(runtimeAssembly), request.expectedRuntimeSha256, StringComparison.OrdinalIgnoreCase),
            "Loaded runtime assembly SHA differs from primary's coherent compiled receipt.");
        var matches = owner.Profiles.Where(p => p != null && p.displayName == ProfileName).ToArray();
        Require(matches.Length == 1 && matches[0].profileId == DisposableProfileId && matches[0].profileId == request.expectedProfileId &&
            owner.ActiveProfileId == request.expectedProfileId, "Only the unique existing named disposable active profile is allowed.");
        Require(psm.state != null && wsm.State != null &&
            string.Equals(psm.state.playerId, request.expectedProfileId, StringComparison.OrdinalIgnoreCase), "Player identity mismatch.");
        Require(psm.TryFlushPendingAutosave(out string flushFailure), "Pending autosave failed: " + flushFailure);

        // note: Use the same read-only owner admission check before recording a baseline. This
        // also rejects unpaired, checksum-invalid, persistence-blocked, or schema-invalid sessions.
        var requireOwner = typeof(YQProfileSaveSystem).GetMethod("TryRequireContinuationOwner", PrivateInstance);
        Require(requireOwner != null, "Reviewed owner admission method is unavailable.");
        object[] admission = { null, null, null, null };
        Require((bool)requireOwner.Invoke(owner, admission), "Owner precondition rejected: " + admission[3]);
        var player = psm.state;
        var offer = player.FindOfferById(request.actualPendingOfferId);
        Require(offer != null && offer.IsPending, "Pass an actual current pending offer; the harness never queues one.");
        string terms = YQProfileSaveSystem.CaptureOfferTerms(offer);
        string root = Path.Combine(Application.persistentDataPath, "Profiles");
        string folder = Path.Combine(root, owner.ActiveProfileId);
        Require(Path.GetFullPath(folder).StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Resolved profile path escapes existing owner root.");
        string manifest = Path.Combine(root, "profiles_manifest.json");
        string activePlayer = Path.Combine(Application.persistentDataPath, "player_state.json");
        string activeWorld = Path.Combine(Application.persistentDataPath, "world_state.json");
        bool fileFault = request.scenario == Scenario.PointerReplaceDenied || request.scenario == Scenario.ProjectionReplaceDenied;
        Require(!fileFault || request.sharedFileLockFaultExplicitlyReviewed,
            "Shared-file lock faults require a separately reviewed primary backup/restore boundary.");
        Require(File.Exists(manifest) && File.Exists(activePlayer) && File.Exists(activeWorld), "Required live projections are missing.");
        Require(!File.Exists(manifest + ".tmp") && !File.Exists(activePlayer + ".tmp") &&
            !File.Exists(activeWorld + ".tmp"), "Preserve and investigate existing shared temporary files before this run.");

        string documentId = "offer-owner-check-" + Guid.NewGuid().ToString("N");
        var providers = (Dictionary<string, Func<string>>)typeof(YQProfileSaveSystem)
            .GetField("_auxiliaryProviders", PrivateInstance).GetValue(owner);
        Require(!providers.ContainsKey(documentId), "Test provider ID unexpectedly exists.");
        var before = Capture(owner, player, wsm.State, root, folder, activePlayer, activeWorld);
        Directory.CreateDirectory(output);
        int captures = 0;
        bool lease = true, innerSucceeded = false;
        string innerMessage = null, innerFailure = null;
        FileStream denial = null;
        Func<string> provider = () =>
        {
            captures++;
            bool first = captures == 1, second = captures == 2;
            if ((first && request.scenario == Scenario.InvalidateFirstCapture) ||
                (second && request.scenario == Scenario.InvalidateSecondCapture)) lease = false;
            if ((first && request.scenario == Scenario.ThrowFirstCapture) ||
                (second && request.scenario == Scenario.ThrowSecondCapture))
                throw new InvalidOperationException("offer-owner-check provider throw " + captures);
            if ((first && request.scenario == Scenario.ReenterFirstCapture) ||
                (second && request.scenario == Scenario.ReenterSecondCapture))
            {
                innerSucceeded = owner.TryCommitOfferDecision(player, request.actualPendingOfferId, terms,
                    request.decision, out innerMessage, out innerFailure, () => true);
                lease = false; // keep the outer transaction rejected so this witness does not consume the offer
            }
            if (second && fileFault)
            {
                // note: Windows read sharing permits the owner's subsequent checksum reads but
                // denies File.Replace/delete. It does not overwrite any accepted state itself.
                string target = request.scenario == Scenario.PointerReplaceDenied ? manifest : activePlayer;
                denial = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            return "{\"test_owned\":true}";
        };
        Func<bool> presentation = () =>
        {
            if ((captures == 1 && request.scenario == Scenario.ThrowGuardAfterFirstCapture) ||
                (captures == 2 && request.scenario == Scenario.ThrowGuardAfterSecondCapture))
                throw new InvalidOperationException("offer-owner-check presentation throw " + captures);
            return lease;
        };
        bool succeeded = false;
        string message = null, failure = null, checkFailure = null;
        Snapshot after = null;
        string selected = null, resolverFailure = null;
        bool replaySucceeded = false;
        string replayMessage = null, replayFailure = null;
        try
        {
            Require(owner.RegisterAuxiliaryDocument(documentId, provider), "Provider registration rejected.");
            succeeded = owner.TryCommitOfferDecision(player, request.actualPendingOfferId, terms,
                request.decision, out message, out failure, presentation);
            denial?.Dispose(); denial = null;
            after = Capture(owner, player, wsm.State, root, folder, activePlayer, activeWorld);
            selected = ResolveActualManifest(owner, folder, request.expectedProfileId, out resolverFailure);
            bool committed = request.scenario == Scenario.CommitAndReplay || request.scenario == Scenario.ProjectionReplaceDenied;
            int expectedCaptures = request.scenario == Scenario.InvalidateFirstCapture ||
                request.scenario == Scenario.ThrowFirstCapture || request.scenario == Scenario.ReenterFirstCapture ||
                request.scenario == Scenario.ThrowGuardAfterFirstCapture ? 1 : 2;
            Require(captures == expectedCaptures, "Transaction did not reach the intended auxiliary callback boundary.");
            Require(ReferenceEquals(psm.state, player), "The transaction replaced the authoritative PlayerState reference.");
            Require(after.world == before.world, "Offer transaction changed the canonical world document.");
            if (!committed)
            {
                Require(!succeeded && message == string.Empty && !string.IsNullOrEmpty(failure), "Rejected decision reported success or speech.");
                Require(after.player == before.player && after.commit == before.commit && after.revision == before.revision &&
                    after.diskCommit == before.diskCommit && after.diskRevision == before.diskRevision &&
                    after.saveCount == before.saveCount && after.reload == before.reload, "Rejected transaction changed live domain/pointer/count/reload.");
                Require(EqualFiles(before.files, after.files), "Rejected transaction changed a manifest or recovery projection.");
                Require(selected == before.commit, "Actual owner manifest resolver selected the rejected orphan revision.");
                bool staged = expectedCaptures == 2;
                Require(after.revisions.Except(before.revisions).Count() == (staged ? 1 : 0), "Expected immutable stage boundary was not reached exactly once.");
                if (request.scenario == Scenario.ReenterFirstCapture || request.scenario == Scenario.ReenterSecondCapture)
                    Require(!innerSucceeded && innerMessage == string.Empty &&
                        innerFailure != null && innerFailure.Contains("already in progress"), "Nested owner call was not rejected by the transaction guard.");
                if (request.scenario == Scenario.PointerReplaceDenied)
                    Require(failure.Contains("pointer publication failed"), "The file fault did not reach SaveManifest publication.");
            }
            else
            {
                Require(succeeded && !string.IsNullOrEmpty(message), "Published decision did not report its committed outcome.");
                Require(after.commit != before.commit && after.revision == before.revision + 1 &&
                    after.diskCommit == after.commit && after.diskRevision == after.revision &&
                    after.saveCount == before.saveCount + 1 && selected == after.commit, "Actual durable owner receipt/pointer did not advance once.");
                Require(owner.LastTransactionReceipt != null && owner.LastTransactionReceipt.published &&
                    owner.LastTransactionReceipt.commitId == after.commit, "Missing truthful published receipt.");
                Require(!player.FindOfferById(request.actualPendingOfferId).IsPending && after.player != before.player,
                    "Published decision was not applied to the original player.");
                if (request.scenario == Scenario.ProjectionReplaceDenied)
                {
                    Require(after.files[activePlayer] == before.files[activePlayer], "The intended player projection fault was ineffective.");
                    Require(!owner.RequiresProfileReload && owner.LastFailure.Contains("working projections require refresh"),
                        "Projection-refresh failure was confused with exceptional live-apply reload truth.");
                }
                // note: Replay uses the same saved offer/terms and the real public compatibility overload.
                var replayBefore = Capture(owner, player, wsm.State, root, folder, activePlayer, activeWorld);
                replaySucceeded = owner.TryCommitOfferDecision(player, request.actualPendingOfferId, terms,
                    request.decision, out replayMessage, out replayFailure);
                var replayAfter = Capture(owner, player, wsm.State, root, folder, activePlayer, activeWorld);
                Require(!replaySucceeded && replayMessage == string.Empty &&
                    replayFailure == "The offer changed or is no longer pending.",
                    "Replay did not pass owner admission and reach the settled-offer rejection.");
                Require(replayAfter.player == replayBefore.player && replayAfter.world == replayBefore.world &&
                    replayAfter.commit == replayBefore.commit && replayAfter.saveCount == replayBefore.saveCount &&
                    EqualFiles(replayBefore.files, replayAfter.files), "Committed replay changed domain or persistence.");
            }
        }
        catch (Exception exception) { checkFailure = exception.ToString(); }
        finally
        {
            denial?.Dispose();
            // note: Never unregister a replacement callback belonging to someone else.
            if (providers.TryGetValue(documentId, out var current) && ReferenceEquals(current, provider))
                owner.UnregisterAuxiliaryDocument(documentId);
        }
        string resultPath = Path.Combine(output, "results.json");
        File.WriteAllText(resultPath, JsonConvert.SerializeObject(new
        {
            utc = DateTime.UtcNow.ToString("O"), scenario = request.scenario.ToString(),
            evidence = "PRIMARY_INVOKED_REAL_UNITY_OWNER_TRANSACTION_NOT_ORDINARY_UI_OR_AUDIO",
            status = checkFailure == null ? "PASS" : "FAIL", request, runtime_mvid = typeof(YQProfileSaveSystem).Module.ModuleVersionId.ToString(),
            runtime_assembly = runtimeAssembly, runtime_sha256 = Hash(runtimeAssembly),
            backup_receipt_sha256 = Hash(request.reviewedBackupReceipt), before, after, captures,
            succeeded, message, failure, selected, resolverFailure, innerSucceeded, innerMessage, innerFailure,
            replaySucceeded, replayMessage, replayFailure, checkFailure,
            manifest_temporary_sha256 = Hash(manifest + ".tmp"),
            player_temporary_sha256 = Hash(activePlayer + ".tmp"),
            world_temporary_sha256 = Hash(activeWorld + ".tmp"),
            retained_auxiliary_document = succeeded ? documentId : null,
            cleanup = "Only the exact owned provider and opened read locks were released. No save/revision/temp/profile rollback or reload was performed."
        }, Formatting.Indented));
        return resultPath;
    }

    private static Snapshot Capture(YQProfileSaveSystem owner, PlayerState player, WorldState world,
        string root, string folder, string activePlayer, string activeWorld)
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in new[] { Path.Combine(root, "profiles_manifest.json"), activePlayer, activeWorld,
            Path.Combine(folder, "player_state.json"), Path.Combine(folder, "world_state.json") })
        { files[path] = Hash(path); files[path + ".bak"] = Hash(path + ".bak"); }
        string revisions = Path.Combine(folder, "revisions");
        var diskManifest = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(root, "profiles_manifest.json")));
        return new Snapshot { profile = owner.ActiveProfileId, commit = owner.ActiveCommitId,
            revision = owner.ActiveRevision, diskCommit = (string)diskManifest["activeCommitId"],
            diskRevision = (int)diskManifest["activeRevision"],
            saveCount = owner.SuccessfulSaveCount, reload = owner.RequiresProfileReload, lastFailure = owner.LastFailure,
            player = TextHash(JsonConvert.SerializeObject(player, Canonical)), world = TextHash(JsonConvert.SerializeObject(world, Canonical)),
            files = files, revisions = Directory.Exists(revisions) ? Directory.GetDirectories(revisions)
                .Select(Path.GetFileName).OrderBy(v => v, StringComparer.Ordinal).ToArray() : Array.Empty<string>() };
    }

    private static string ResolveActualManifest(YQProfileSaveSystem owner, string folder, string profile, out string failure)
    {
        var method = typeof(YQProfileSaveSystem).GetMethod("TryResolveLatestCommit", PrivateInstance);
        Require(method != null, "Actual owner manifest resolver unavailable.");
        object[] arguments = { folder, profile, null, null, null, null };
        bool accepted = (bool)method.Invoke(owner, arguments);
        failure = arguments[5] as string;
        Require(accepted, "Actual owner could not resolve its checksum-valid paired revision: " + failure);
        return ((YQProfileCommitRecord)arguments[4]).commitId;
    }

    private static bool EqualFiles(Dictionary<string, string> a, Dictionary<string, string> b)
        => a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out string hash) && hash == pair.Value);

    private static string Hash(string path)
    {
        if (!File.Exists(path)) return null;
        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private static string TextHash(string text)
    {
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text)))
                .Replace("-", "").ToLowerInvariant();
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
