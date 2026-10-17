#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// note: Explicit Editor bridge only. Tools placement does not install these menus; the primary
// must review and load this separate revision. Capture never opens dialogue, flushes saves, or runs a scenario.
public static class RealOfferOwnerEditorBridgeV1
{
    // note: Bind the verified display name and stable ID of this already-existing disposable control.
    private const string DisposableName = "Sol61 Voice Control 20261009";
    private const string DisposableProfileId = "18452e6b66b444a3935c9a3118a2f5e9";
    private const string PlaySafe = "Assets/Assets/Scenes/YourQuest_PlaySafe.unity";
    private const string FrozenHelperSha = "c9467cf38b28faa93585889cad9e19cb47fb4705de8f8cca275e7afc567b692a";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
    private const int MaximumNpcs = 128, MaximumOffers = 64, MaximumFiles = 4096;
    private const long MaximumFileBytes = 64L * 1024 * 1024, MaximumTotalBytes = 256L * 1024 * 1024;
    private static string Project => Directory.GetParent(Application.dataPath).FullName;
    private static string Review => Path.Combine(Project, "Docs/PlayableEvaluation_2026-10-09/reviews/offer_owner_verification");
    private static readonly JsonSerializerSettings Json = new JsonSerializerSettings {
        Formatting = Formatting.None, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    public sealed class Capture
    {
        public int formatVersion = 1;
        public string utc, evidence = "READ_ONLY_PRIMARY_EDITOR_CAPTURE_NOT_TRANSACTION_OR_ORDINARY_INPUT_PROOF";
        public string runtimeSha256, runtimeMvid, runtimeLocation, activeProfileId, activeCommitId;
        public int activeRevision, requestEpoch;
        public string leaseFingerprint;
        public bool knownOwnerQuiescence;
        public List<string> blockers = new List<string>();
        public Dictionary<string, object> details = new Dictionary<string, object>();
        public List<object> pendingOffers = new List<object>();
        public List<PathWitness> backupInventory = new List<PathWitness>();
    }

    public sealed class PathWitness
    {
        public string path, kind, sha256, failure;
        public bool exists;
        public long bytes;
    }

    // note: All observed input fields are required typed constructor arguments. Raw keys remain
    // in captures; only the explicit reviewed-request lease omits the four held/edge bits.
    private sealed class InputObservation
    {
        public readonly bool keyboardPresent;
        public readonly bool mousePresent;
        public readonly bool ePressed;
        public readonly bool ePressedThisFrame;
        public readonly bool leftMousePressed;
        public readonly bool leftMousePressedThisFrame;
        public readonly bool motorPresent;
        public readonly bool motorEnabled;
        public readonly bool authoritative;
        public readonly bool movementInputAllowed;
        public readonly float[] playerPosition;
        public readonly string motorObject;
        public readonly string dialoguePlayerRoot;
        public readonly string dialogueCamera;
        public readonly bool dialogueCameraEnabled;
        public readonly string motorCamera;
        public readonly bool dialogueRootIsMotor;
        public readonly float[] cameraPosition;
        public InputObservation(
            bool keyboardPresent,
            bool mousePresent,
            bool ePressed,
            bool ePressedThisFrame,
            bool leftMousePressed,
            bool leftMousePressedThisFrame,
            bool motorPresent,
            bool motorEnabled,
            bool authoritative,
            bool movementInputAllowed,
            float[] playerPosition,
            string motorObject,
            string dialoguePlayerRoot,
            string dialogueCamera,
            bool dialogueCameraEnabled,
            string motorCamera,
            bool dialogueRootIsMotor,
            float[] cameraPosition)
        {
            this.keyboardPresent = keyboardPresent;
            this.mousePresent = mousePresent;
            this.ePressed = ePressed;
            this.ePressedThisFrame = ePressedThisFrame;
            this.leftMousePressed = leftMousePressed;
            this.leftMousePressedThisFrame = leftMousePressedThisFrame;
            this.motorPresent = motorPresent;
            this.motorEnabled = motorEnabled;
            this.authoritative = authoritative;
            this.movementInputAllowed = movementInputAllowed;
            this.playerPosition = playerPosition == null ? null : (float[])playerPosition.Clone();
            this.motorObject = motorObject;
            this.dialoguePlayerRoot = dialoguePlayerRoot;
            this.dialogueCamera = dialogueCamera;
            this.dialogueCameraEnabled = dialogueCameraEnabled;
            this.motorCamera = motorCamera;
            this.dialogueRootIsMotor = dialogueRootIsMotor;
            this.cameraPosition = cameraPosition == null ? null : (float[])cameraPosition.Clone();
        }
    }

    private sealed class ExplicitRequestInputLease
    {
        public readonly bool keyboardPresent;
        public readonly bool mousePresent;
        public readonly bool motorPresent;
        public readonly bool motorEnabled;
        public readonly bool authoritative;
        public readonly bool movementInputAllowed;
        public readonly float[] playerPosition;
        public readonly string motorObject;
        public readonly string dialoguePlayerRoot;
        public readonly string dialogueCamera;
        public readonly bool dialogueCameraEnabled;
        public readonly string motorCamera;
        public readonly bool dialogueRootIsMotor;
        public readonly float[] cameraPosition;
        public ExplicitRequestInputLease(InputObservation observed)
        {
            keyboardPresent = observed.keyboardPresent;
            mousePresent = observed.mousePresent;
            motorPresent = observed.motorPresent;
            motorEnabled = observed.motorEnabled;
            authoritative = observed.authoritative;
            movementInputAllowed = observed.movementInputAllowed;
            playerPosition = (float[])observed.playerPosition.Clone();
            motorObject = observed.motorObject;
            dialoguePlayerRoot = observed.dialoguePlayerRoot;
            dialogueCamera = observed.dialogueCamera;
            dialogueCameraEnabled = observed.dialogueCameraEnabled;
            motorCamera = observed.motorCamera;
            dialogueRootIsMotor = observed.dialogueRootIsMotor;
            cameraPosition = (float[])observed.cameraPosition.Clone();
        }
    }

    private static ExplicitRequestInputLease ReadExplicitRequestInputLease(Capture result)
    {
        // note: Unknown/missing observations cannot certify a stable lease. Never coerce JSON
        // or missing booleans into observed false; this private DTO comes only from the current reader.
        if (!result.details.TryGetValue("input", out var value) || !(value is InputObservation observed) ||
            !IsFinitePosition(observed.playerPosition) || !IsFinitePosition(observed.cameraPosition) ||
            string.IsNullOrWhiteSpace(observed.motorObject) || string.IsNullOrWhiteSpace(observed.dialoguePlayerRoot) ||
            string.IsNullOrWhiteSpace(observed.dialogueCamera) || string.IsNullOrWhiteSpace(observed.motorCamera))
        {
            result.blockers.Add("Stable explicit-request input observation is missing, invalid, or has an unknown type.");
            return null;
        }
        return new ExplicitRequestInputLease(observed);
    }

    private static bool IsFinitePosition(float[] value)
    {
        if (value == null || value.Length != 3) return false;
        for (int index = 0; index < value.Length; index++)
            if (float.IsNaN(value[index]) || float.IsInfinity(value[index])) return false;
        return true;
    }

    public sealed class ReviewedRequest
    {
        public int formatVersion;
        public string reviewedCapturePath, reviewedCaptureSha256, reviewedLeaseFingerprint;
        public string reviewedBackupReceiptSha256;
        public bool primaryReviewedRequest, externalPublishersQuiescent;
        public string externalQuiescenceExplanation;
        public RealOfferOwnerChecks.Request scenario;
    }

    [MenuItem("YourQuest/Playable Evaluation/Offer Owner/Capture Read-Only Preflight V1")]
    public static void CaptureReadOnlyMenu()
    {
        var capture = ReadOnlyPreflight();
        string directory = Path.Combine(Review, "captures");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "capture-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonConvert.SerializeObject(capture, Formatting.Indented));
        Debug.Log("[OfferOwnerPreflight] Read-only capture: " + path + "; known owner quiescence=" + capture.knownOwnerQuiescence +
            "; no transaction executed. External publisher ownership still requires primary review.");
    }

    [MenuItem("YourQuest/Playable Evaluation/Offer Owner/Run Explicit Reviewed Request V1")]
    public static void RunReviewedRequestMenu()
    {
        // note: This separate menu is the only bridge path to the mutating test API. A freshly
        // rechecked capture and an explicit primary-reviewed request are required; capture cannot launch it.
        string selected = EditorUtility.OpenFilePanel("Select primary-reviewed offer-owner request", Path.Combine(Review, "requests"), "json");
        if (string.IsNullOrWhiteSpace(selected)) return;
        InvokeReviewedRequest(selected);
    }

    public static string InvokeReviewedRequest(string requestPath)
    {
        Require(IsChild(requestPath, Path.Combine(Review, "requests")), "Request must be inside the owned requests directory.");
        Require(new FileInfo(requestPath).Length <= 64 * 1024, "Request exceeds the bounded input size.");
        string requestJson = File.ReadAllText(requestPath);
        var request = JsonConvert.DeserializeObject<ReviewedRequest>(requestJson,
            new JsonSerializerSettings { MissingMemberHandling = MissingMemberHandling.Error });
        Require(request != null && request.formatVersion == 1 && request.primaryReviewedRequest && request.scenario != null,
            "A versioned explicitly primary-reviewed request is required.");
        Require(request.externalPublishersQuiescent && !string.IsNullOrWhiteSpace(request.externalQuiescenceExplanation) &&
            request.externalQuiescenceExplanation.Length <= 4096,
            "No public API certifies all external publishers; require primary's explicit bounded ownership attestation.");
        Require(IsChild(request.reviewedCapturePath, Path.Combine(Review, "captures")) &&
            Hash(request.reviewedCapturePath) == request.reviewedCaptureSha256,
            "Reviewed capture path/hash mismatch.");
        Require(new FileInfo(request.reviewedCapturePath).Length <= 8 * 1024 * 1024, "Capture exceeds the bounded input size.");
        var reviewed = JsonConvert.DeserializeObject<Capture>(File.ReadAllText(request.reviewedCapturePath));
        Require(reviewed != null && reviewed.knownOwnerQuiescence && reviewed.leaseFingerprint == request.reviewedLeaseFingerprint,
            "Reviewed preflight was blocked or fingerprint differs.");
        var current = ReadOnlyPreflight();
        // note: Retain the exact refused observation before the unchanged guard throws; diagnostics never admit the helper.
        if (!current.knownOwnerQuiescence || current.leaseFingerprint != reviewed.leaseFingerprint)
            TryRetainAdmissionRefusal(requestPath, requestJson, request, reviewed, current);
        Require(current.knownOwnerQuiescence && current.leaseFingerprint == reviewed.leaseFingerprint,
            "Live owners, state, pending terms, files, or observed quiescence changed since capture; recapture and review.");
        Require(Hash(Path.Combine(Project, "Tools/PlayableEvaluation/offer_owner_verification/name_revision_2/RealOfferOwnerChecks.cs")) == FrozenHelperSha,
            "Frozen helper source bytes changed; review a separately named revision before execution.");
        Require(request.scenario.expectedProfileId == current.activeProfileId &&
            request.scenario.expectedRuntimeMvid == current.runtimeMvid &&
            string.Equals(request.scenario.expectedRuntimeSha256, current.runtimeSha256, StringComparison.OrdinalIgnoreCase),
            "Scenario identity differs from the actual loaded Unity assembly/profile.");
        Require(!string.IsNullOrWhiteSpace(request.reviewedBackupReceiptSha256) &&
            Hash(request.scenario.reviewedBackupReceipt) == request.reviewedBackupReceiptSha256,
            "The primary-reviewed backup receipt bytes changed or are unavailable.");
        Require(Enum.IsDefined(typeof(RealOfferOwnerChecks.Scenario), request.scenario.scenario), "Unknown scenario.");
        // note: Admission and all observations are repeated synchronously before the unchanged frozen
        // helper runs. This does not turn source/provider fixtures into ordinary UI or audio evidence.
        string requestSha = Hash(requestPath);
        string result = RealOfferOwnerChecks.Run(request.scenario);
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(result), "bridge-request-identity.json"),
            JsonConvert.SerializeObject(new { requestPath, requestSha256 = requestSha,
                request.reviewedCapturePath, request.reviewedCaptureSha256, current.leaseFingerprint,
                request.reviewedBackupReceiptSha256,
                current.runtimeSha256, current.runtimeMvid, externalQuiescence = request.externalQuiescenceExplanation,
                evidence = "EXPLICIT_PRIMARY_REVIEWED_REQUEST_REAL_OWNER_HELPER_NOT_ORDINARY_INPUT" }, Formatting.Indented));
        return result;
    }

    private static void TryRetainAdmissionRefusal(string requestPath, string requestJson,
        ReviewedRequest request, Capture reviewed, Capture current)
    {
        try
        {
            // note: Only a bounded new Docs receipt is written. Source request text and the refused
            // preflight are retained without repeating observations, providers or owner operations.
            var receipt = new
            {
                formatVersion = 1, utc = DateTime.UtcNow.ToString("O"),
                evidence = "REFUSED_BEFORE_HELPER_NOT_TRANSACTION_OR_ORDINARY_INPUT",
                requestPath, requestFileSha256AtRefusal = Hash(requestPath),
                requestTextUtf8Sha256 = TextHash(requestJson), request,
                request.reviewedCapturePath, request.reviewedCaptureSha256,
                expectedLeaseFingerprint = reviewed.leaseFingerprint,
                currentLeaseFingerprint = current.leaseFingerprint,
                currentKnownOwnerQuiescence = current.knownOwnerQuiescence,
                currentPreflight = current,
                helperInvoked = false,
                failure = "Live owners, state, pending terms, files, or observed quiescence changed since capture; recapture and review."
            };
            string json = JsonConvert.SerializeObject(receipt, Formatting.Indented, Json);
            Require(System.Text.Encoding.UTF8.GetByteCount(json) <= 8 * 1024 * 1024,
                "Refused-preflight diagnostic exceeds the bounded output size.");
            string directory = Path.Combine(Review, "admission_refusals");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "refusal-" + Guid.NewGuid().ToString("N") + ".json");
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(file)) writer.Write(json);
            Debug.Log("[RealOfferOwnerEditorBridgeV1] Retained refused preflight: " + path);
        }
        catch (Exception error)
        {
            // note: A failed diagnostic write must not replace or relax the original admission refusal.
            string reason = error.GetBaseException().Message ?? "unknown diagnostic error";
            Debug.LogError("[RealOfferOwnerEditorBridgeV1] Refused preflight could not be retained: " +
                reason.Substring(0, Math.Min(reason.Length, 1024)));
        }
    }

    [MenuItem("YourQuest/Playable Evaluation/Offer Owner/Prepare Assisted Voice Mute")]
    public static void PrepareAssistedVoiceMute()
    {
        // note: This explicitly assisted preparation invokes the existing mute/cancel path; it is never evidence of F2 input or an offer decision.
        Require(EditorApplication.isPlaying && EditorApplication.isPaused && !EditorApplication.isCompiling &&
            !EditorApplication.isUpdating && SceneManager.GetActiveScene().path == PlaySafe, "Requires primary-owned paused stable PlaySafe.");
        var owner = YQProfileSaveSystem.Instance;
        var player = PlayerStateManager.Instance?.state;
        var world = WorldStateManager.Instance?.State;
        var ui = YQInvestorDialogueUI.Instance;
        var speech = YQLocalSpeech.Instance;
        Require(owner != null && owner.isActiveAndEnabled && !owner.RequiresProfileReload && ui != null && ui.isActiveAndEnabled &&
            speech != null && speech.isActiveAndEnabled && player != null && world != null, "Existing active control owners required.");
        var matches = owner.Profiles.Where(profile => profile != null && profile.displayName == DisposableName).ToArray();
        Require(matches.Length == 1 && matches[0].profileId == DisposableProfileId && owner.ActiveProfileId == DisposableProfileId &&
            player.playerId == DisposableProfileId && world.worldIdentity?.ownerProfileId == DisposableProfileId, "Only the unique existing disposable control is eligible.");
        var observations = new Capture();
        // note: Inspect exactly the speech component the UI will cancel; a singleton alone cannot certify a cached UI reference.
        Require(ReferenceEquals(Read(ui, "_speech", observations), speech) &&
            !Flag(owner, "_profileTransactionInProgress", observations) && !Flag(ui, "_waitingOnReply", observations) &&
            !Flag(ui, "_liveWaiting", observations) && Read(ui, "_liveReplyAgent", observations) == null &&
            !speech.IsTranscribing && !speech.IsSpeaking && !Flag(speech, "_retrieving", observations) &&
            Read(speech, "_prepareRoutine", observations) == null && Read(speech, "_request", observations) == null &&
            Count(Read(speech, "_controlRequests", observations)) == 0 &&
            Read(speech, "_controls", observations) is int controls && controls == 0 && observations.blockers.Count == 0,
            "Pending reply, preparation, control, or transaction cannot be retired by this preparation command.");
        string beforePlayer = TextHash(JsonConvert.SerializeObject(player, Json));
        string beforeWorld = TextHash(JsonConvert.SerializeObject(world, Json));
        string beforeCommit = owner.ActiveCommitId;
        int beforeRevision = owner.ActiveRevision, beforeEpoch = YQServiceLifecycle.RequestEpoch;
        bool wasMuted = ui.voiceMuted, wasCapturing = speech.IsCapturing;
        // note: Prepare the owned receipt path before mutation; even cancellation or after-state observation failure must leave an explicit outcome.
        string directory = Path.Combine(Review, "preparations");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "assisted-voice-mute-" + Guid.NewGuid().ToString("N") + ".json");
        var report = new Dictionary<string, object> { { "utc", DateTime.UtcNow.ToString("O") },
            { "evidence", "ASSISTED_PREPARATION_ONLY_NOT_ORDINARY_INPUT_OR_OWNER_DECISION" }, { "status", "STARTED" },
            { "profileId", owner.ActiveProfileId }, { "runtimeMvid", typeof(YQProfileSaveSystem).Module.ModuleVersionId.ToString() },
            { "runtimeSha256", Hash(typeof(YQProfileSaveSystem).Assembly.Location) },
            { "wasMuted", wasMuted }, { "wasCapturing", wasCapturing }, { "beforeCommit", beforeCommit },
            { "beforeRevision", beforeRevision }, { "beforeEpoch", beforeEpoch },
            { "afterObservation", "NOT_COMPLETED" },
            { "remainingWork", "Existing cancel transports must drain normally; a fresh read-only preflight is required." } };
        Exception operationFailure = null;
        try
        {
            ui.SetVoiceMuted(true);
            bool canonicalUnchanged = ReferenceEquals(player, PlayerStateManager.Instance.state) &&
                ReferenceEquals(world, WorldStateManager.Instance.State) && owner.ActiveProfileId == DisposableProfileId &&
                beforeCommit == owner.ActiveCommitId && beforeRevision == owner.ActiveRevision && beforeEpoch == YQServiceLifecycle.RequestEpoch &&
                beforePlayer == TextHash(JsonConvert.SerializeObject(player, Json)) && beforeWorld == TextHash(JsonConvert.SerializeObject(world, Json));
            report["voiceMutedAfter"] = ui.voiceMuted;
            report["capturingAfter"] = speech.IsCapturing;
            report["canonicalUnchanged"] = canonicalUnchanged;
            report["remainingControls"] = Read(speech, "_controls", observations);
            report["afterObservation"] = "COMPLETED";
            Require(canonicalUnchanged && ui.voiceMuted && !speech.IsCapturing, "Assisted mute changed canonical state or failed; inspect retained preparation receipt.");
            report["status"] = "PASS_PREPARATION_ONLY";
        }
        catch (Exception error)
        {
            operationFailure = error;
            report["status"] = "FAIL";
            string failure = error.GetBaseException().Message;
            report["failure"] = failure.Length > 2048 ? failure.Substring(0, 2048) : failure;
            report["failureType"] = error.GetType().FullName;
            throw;
        }
        finally
        {
            try { File.WriteAllText(path, JsonConvert.SerializeObject(report, Formatting.Indented)); }
            catch (Exception receiptError)
            {
                // note: A receipt error must remain visible, and must not replace an already thrown cancellation/observation failure.
                Debug.LogError("[OfferOwnerPreflight] Assisted mute receipt write failed at " + path + ": " + receiptError.Message);
                if (operationFailure == null) throw;
            }
        }
        Debug.Log("[OfferOwnerPreflight] Assisted voice mute preparation: " + path + "; no decision executed. Drain cancel work before recapture.");
    }

    public static Capture ReadOnlyPreflight()
    {
        var result = new Capture { utc = DateTime.UtcNow.ToString("O"), requestEpoch = YQServiceLifecycle.RequestEpoch };
        var owner = YQProfileSaveSystem.Instance;
        var playerOwner = PlayerStateManager.Instance;
        var worldOwner = WorldStateManager.Instance;
        var ui = YQInvestorDialogueUI.Instance;
        var motor = YQInvestorPlayerMotor.ActiveMotor;
        result.details["editor"] = new { playing = EditorApplication.isPlaying, paused = EditorApplication.isPaused,
            compiling = EditorApplication.isCompiling, updating = EditorApplication.isUpdating,
            applicationFocused = Application.isFocused, focusedWindow = EditorWindow.focusedWindow != null ? EditorWindow.focusedWindow.GetType().FullName : null,
            scene = SceneManager.GetActiveScene().path, frame = Time.frameCount, timeScale = Time.timeScale,
            cursorLock = Cursor.lockState.ToString(), cursorVisible = Cursor.visible };
        if (!EditorApplication.isPlaying || !EditorApplication.isPaused || SceneManager.GetActiveScene().path != PlaySafe)
            result.blockers.Add("Requires primary-owned paused PlaySafe; capture does not alter play/pause/focus.");
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) result.blockers.Add("Editor compile/import is active.");
        var runtime = typeof(YQProfileSaveSystem).Assembly;
        result.runtimeLocation = runtime.Location;
        result.runtimeMvid = runtime.ManifestModule.ModuleVersionId.ToString();
        result.runtimeSha256 = Hash(runtime.Location);
        if (result.runtimeSha256 == null) result.blockers.Add("Loaded runtime assembly SHA is unavailable.");
        result.details["bridgeAssembly"] = new { location = typeof(RealOfferOwnerEditorBridgeV1).Assembly.Location,
            mvid = typeof(RealOfferOwnerEditorBridgeV1).Module.ModuleVersionId.ToString(),
            sha256 = Hash(typeof(RealOfferOwnerEditorBridgeV1).Assembly.Location) };
        if (owner == null || playerOwner == null || worldOwner == null || !owner.isActiveAndEnabled ||
            !playerOwner.isActiveAndEnabled || !worldOwner.isActiveAndEnabled)
        { result.blockers.Add("Canonical active profile/player/world owners are missing."); return Finish(result); }
        result.activeProfileId = owner.ActiveProfileId; result.activeCommitId = owner.ActiveCommitId; result.activeRevision = owner.ActiveRevision;
        var matches = owner.Profiles.Where(profile => profile != null && profile.displayName == DisposableName).ToArray();
        result.details["disposableProfiles"] = matches.Select(profile => new { profile.profileId, profile.displayName }).ToArray();
        if (matches.Length != 1 || matches[0].profileId != DisposableProfileId || matches[0].profileId != owner.ActiveProfileId)
        {
            result.blockers.Add("Active profile is not the unique already-existing named disposable profile; private state/path inventory not captured.");
            return Finish(result);
        }
        var player = playerOwner.state; var world = worldOwner.State;
        if (player == null || world == null) { result.blockers.Add("Canonical documents unavailable."); return Finish(result); }
        result.details["canonicalOwners"] = new { playerId = player.playerId, worldOwner = world.worldIdentity?.ownerProfileId,
            worldId = world.worldIdentity?.worldId, seed = world.generatedWorldPlan?.worldSeed,
            playerSchema = player.schemaVersion, stateSchemaContract = YQStateContract.CurrentStateSchemaVersion,
            owner.RequiresProfileReload, owner.LastFailure, owner.SuccessfulSaveCount,
            playerSha256 = TextHash(JsonConvert.SerializeObject(player, Json)), worldSha256 = TextHash(JsonConvert.SerializeObject(world, Json)) };
        try
        {
            var admission = typeof(YQProfileSaveSystem).GetMethod("TryRequireContinuationOwner", Fields);
            object[] args = { null, null, null, null };
            bool admitted = admission != null && (bool)admission.Invoke(owner, args);
            result.details["ownerAdmission"] = new { admitted, failure = args[3] };
            if (!admitted) result.blockers.Add("Actual read-only owner admission rejected or unavailable.");
        }
        catch (Exception error) { result.details["ownerAdmission"] = error.GetBaseException().Message; result.blockers.Add("Owner admission observation failed."); }
        if (Flag(owner, "_profileTransactionInProgress", result)) result.blockers.Add("A paired owner transaction is active.");
        var autosave = Read(playerOwner, "pendingAutosave", result) as Task;
        result.details["pendingAutosave"] = autosave == null ? null : new { status = autosave.Status.ToString(), autosave.IsCompleted, autosave.IsFaulted, autosave.IsCanceled };
        if (autosave != null && (!autosave.IsCompleted || autosave.IsFaulted || autosave.IsCanceled)) result.blockers.Add("Player autosave is unresolved; capture never drains it.");
        if (playerOwner.saveFileName != "player_state.json" || worldOwner.saveFileName != "world_state.json")
            result.blockers.Add("Manager filenames differ from the paired owner projection contract.");
        var providers = Read(owner, "_auxiliaryProviders", result) as IDictionary;
        var providerRows = new List<object>();
        if (providers != null) foreach (DictionaryEntry provider in providers)
        {
            var callback = provider.Value as Delegate;
            providerRows.Add(new { id = provider.Key, targetType = callback?.Target?.GetType().FullName,
                declaringType = callback?.Method.DeclaringType?.FullName, method = callback?.Method.Name });
        }
        result.details["auxiliaryProvidersNotInvoked"] = providerRows;
        var offers = player.pendingOffers == null ? Array.Empty<PendingProgressionOfferRecord>() : player.pendingOffers.Where(o => o != null && o.IsPending).ToArray();
        if (offers.Length > MaximumOffers) result.blockers.Add("Pending offer capture bound exceeded.");
        foreach (var offer in offers.Take(MaximumOffers)) result.pendingOffers.Add(new { offer.offerId, offer.offerState,
            terms = YQProfileSaveSystem.CaptureOfferTerms(offer) });

        result.details["modal"] = new { RuntimeModalUiBlocker.IsBlocked, RuntimeModalUiBlocker.IsMenuOpen,
            RuntimeModalUiBlocker.IsDialogueOpen, manualTokens = Count(Read(typeof(RuntimeModalUiBlocker), "_manualBlockTokens", result)),
            tutorialMenu = YourQuestTutorialMenuUI.IsOpenNow, pauseMenu = YourQuestPauseMenuUI.IsOpenNow,
            quickMenu = YourQuestTutorialMenuUI.IsQuickOpenNow, menuChangedThisFrame = YourQuestTutorialMenuUI.ChangedThisFrame,
            pointerReleaseFrame = Read(typeof(YourQuestTutorialMenuUI), "_pointerReleaseFrame", result),
            profileMenu = YQProfileMenuUI.IsOpenNow, pointerCaptured = YourQuestTutorialMenuUI.CapturesPointerInput,
            YQTitleScreenUI.StartupPresentationActive, YQStartupLoadingScreen.IsVisible, YQStartupLoadingScreen.IsGenerationVisible };
        if (RuntimeModalUiBlocker.IsMenuOpen || Count(Read(typeof(RuntimeModalUiBlocker), "_manualBlockTokens", result)) != 0 ||
            YourQuestTutorialMenuUI.CapturesPointerInput || YQStartupLoadingScreen.IsVisible || YQTitleScreenUI.StartupPresentationActive)
            result.blockers.Add("Menu/manual/startup input ownership remains active.");
        result.details["input"] = new InputObservation(keyboardPresent: Keyboard.current != null, mousePresent: Mouse.current != null,
            ePressed: Keyboard.current != null && Keyboard.current.eKey.isPressed,
            ePressedThisFrame: Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame,
            leftMousePressed: Mouse.current != null && Mouse.current.leftButton.isPressed,
            leftMousePressedThisFrame: Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame,
            motorPresent: motor != null, motorEnabled: motor != null && motor.isActiveAndEnabled,
            authoritative: motor != null && motor.IsAuthoritative,
            movementInputAllowed: motor != null && motor.CanProcessMovementInput,
            playerPosition: motor != null ? Position(motor.transform) : null,
            motorObject: motor != null ? motor.gameObject.name : null,
            dialoguePlayerRoot: ui != null && ui.playerRoot != null ? ui.playerRoot.name : null,
            dialogueCamera: ui != null && ui.viewCamera != null ? ui.viewCamera.name : null,
            dialogueCameraEnabled: ui != null && ui.viewCamera != null && ui.viewCamera.isActiveAndEnabled,
            motorCamera: motor != null && motor.playerCamera != null ? motor.playerCamera.name : null,
            dialogueRootIsMotor: ui != null && motor != null && ui.playerRoot == motor.transform,
            cameraPosition: ui != null && ui.viewCamera != null ? Position(ui.viewCamera.transform) : null);
        if (ui == null || !ui.isActiveAndEnabled || ui.playerRoot == null || ui.viewCamera == null || motor == null || !motor.IsAuthoritative)
            result.blockers.Add("Dialogue/player/camera ownership references are missing; no ResolveRefs is called.");
        if (ui != null)
        {
            result.details["dialogue"] = new { enabled = ui.isActiveAndEnabled, YQInvestorDialogueUI.IsOpenNow,
                playerPosition = ui.playerRoot != null ? Position(ui.playerRoot) : null,
                ui.talkRadius, ui.requireLineOfSight, entityMask = ui.entityMask.value, occluderMask = ui.occluderMask.value,
                epoch = Read(ui, "_dialogueEpoch", result), open = Read(ui, "_open", result),
                waiting = Read(ui, "_waitingOnReply", result), liveWaiting = Read(ui, "_liveWaiting", result),
                voiceFocused = Read(ui, "_voiceFocused", result), ui.voiceMuted,
                activeNpcId = (Read(ui, "_activeAgent", result) as NpcDialogueAgent)?.NpcId,
                hearingNpcId = (Read(ui, "_hearingTarget", result) as NpcDialogueAgent)?.NpcId };
            if (Flag(ui, "_waitingOnReply", result) || Flag(ui, "_liveWaiting", result) ||
                Read(ui, "_thinkingRoutine", result) != null || Read(ui, "_focusRoutine", result) != null)
                result.blockers.Add("Dialogue/reply/focus work remains pending.");
            var displayed = Read(ui, "_displayedOffers", result) as YQNpcConversationRewards.DisplayedOfferList;
            result.details["displayedOfferSnapshot"] = displayed == null ? null : Enumerable.Range(0, displayed.Count)
                .Select(index => new { row = index + 1, id = displayed.OfferIdAt(index), terms = displayed.TermsAt(index) }).ToArray();
            try
            {
                var predicate = typeof(YQInvestorDialogueUI).GetMethod("IsOfferPresentationCurrent", Fields);
                result.details["actualRenderedPresentationCurrent"] = predicate != null && (bool)predicate.Invoke(ui,
                    new object[] { displayed, Convert.ToInt32(Read(ui, "_dialogueEpoch", result)), Read(ui, "_activeAgent", result) });
                if (predicate == null) result.blockers.Add("Rendered presentation observer is unavailable.");
            }
            catch (Exception error) { result.blockers.Add("Rendered presentation observation failed: " + error.GetBaseException().Message); }
        }
        var npcs = NpcDialogueAgent.ActiveAgents.Where(agent => agent != null).ToArray();
        if (npcs.Length > MaximumNpcs) result.blockers.Add("NPC capture bound exceeded; quiescence cannot be qualified.");
        var npcRows = new List<object>();
        foreach (var agent in npcs.Take(MaximumNpcs))
        {
            var entity = Read(agent, "_entityInfo", result) as EntityInfo;
            bool? valid = null;
            if (ui != null && ui.playerRoot != null && ui.viewCamera != null && entity != null)
            {
                try { valid = (bool)typeof(YQInvestorDialogueUI).GetMethod("IsValidNpc", Fields).Invoke(ui, new object[] { entity, agent }); }
                catch (Exception error) { result.blockers.Add("Talkability observation failed: " + error.GetBaseException().Message); }
            }
            npcRows.Add(new { id = agent.NpcId, name = agent.NpcName, active = agent.isActiveAndEnabled,
                position = Position(agent.transform), hostility = entity != null ? entity.hostility.ToString() : null,
                entityId = entity?.entityId, agent.IsThinking, ownerTalkabilityPredicate = valid,
                openingGateEligible = valid == true && agent.isActiveAndEnabled && ui != null && ui.isActiveAndEnabled &&
                    ui.playerRoot != null && ui.viewCamera != null && !YQInvestorDialogueUI.IsOpenNow && !RuntimeModalUiBlocker.IsBlocked });
            if (agent.IsThinking) result.blockers.Add("NPC reply work is active: " + agent.NpcId);
        }
        result.details["npcs"] = npcRows;
        CaptureGeneration(result);
        CapturePaths(result, owner.ActiveProfileId);
        return Finish(result);
    }

    private static void CaptureGeneration(Capture result)
    {
        var llm = LLMClient.Instance;
        result.details["llm"] = llm == null ? null : new { enabled = llm.isActiveAndEnabled, llm.IsBusy,
            llm.PendingRequestCount, llm.ActiveRequestId, llm.ActiveRequestDebugTag, llm.IsExclusiveSequenceActive, llm.ExclusiveSequenceOwner };
        if (llm == null || llm.IsBusy || llm.PendingRequestCount != 0 || llm.ActiveRequestId != 0 || llm.IsExclusiveSequenceActive)
            result.blockers.Add("LLM scheduler is unavailable or not idle.");
        var generation = YQWorldGenerationService.Instance;
        result.details["worldGeneration"] = generation == null ? null : new { generation.IsRequestInFlight,
            backgroundLore = Read(generation, "_backgroundLoreRequestInFlight", result), frontier = Read(generation, "_frontierLocationRequestInFlight", result) };
        if (generation == null || generation.IsRequestInFlight || Flag(generation, "_backgroundLoreRequestInFlight", result) ||
            Flag(generation, "_frontierLocationRequestInFlight", result)) result.blockers.Add("World generation observation is missing or work is active.");
        var builder = YQGeneratedWorldRuntimeBuilder.Instance;
        result.details["startup"] = new { YourQuestTutorialAutoBootstrap.GameplayRuntimeReady,
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased, YourQuestTutorialAutoBootstrap.GameplayPresentationWaitReason,
            YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked, phase = YQStartupLoadingScreen.CurrentGenerationPhase,
            substep = YQStartupLoadingScreen.CurrentGenerationSubstep, builderPresent = builder != null,
            buildActive = builder != null && builder.IsMaterializationInProgress,
            populationActive = builder != null ? Read(builder, "_populationBuildInProgress", result) : null,
            frontierActive = builder != null ? Read(builder, "frontierConstructionInFlight", result) : null };
        // note: HasMaterializedCurrentWorld is intentionally not read: its getter calls plan.EnsureCollections().
        if (builder == null || builder.IsMaterializationInProgress || Flag(builder, "_populationBuildInProgress", result) ||
            Flag(builder, "frontierConstructionInFlight", result) || !YourQuestTutorialAutoBootstrap.GameplayRuntimeReady ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
            result.blockers.Add("Startup/materialization has not reached an idle released state.");
        // note: Aggregate registries retain retired owner tasks; active flags and empty queues are separate observations.
        var frontierLifetime = YQGeneratedWorldRuntimeBuilder.CaptureFrontierWorkerLifetime();
        result.details["frontierWorkerLifetime"] = frontierLifetime;
        if (builder == null || !frontierLifetime.IsQuiescent)
            result.blockers.Add("Frontier physical worker/retirement lifetime is active or incompletely observed.");
        var speech = YQLocalSpeech.Instance;
        result.details["speech"] = speech == null ? null : new { speech.IsCapturing, speech.IsTranscribing, speech.IsSpeaking, speech.Status,
            retrieving = Read(speech, "_retrieving", result), preparing = Read(speech, "_prepareRoutine", result) != null,
            request = Read(speech, "_request", result) != null, controls = Read(speech, "_controls", result),
            controlRequests = Count(Read(speech, "_controlRequests", result)) };
        // note: IsReady would query worker/process liveness. Do not call it or inspect/operate processes here.
        if (speech != null && (speech.IsCapturing || speech.IsTranscribing || speech.IsSpeaking || Flag(speech, "_retrieving", result) ||
            Read(speech, "_prepareRoutine", result) != null || Read(speech, "_request", result) != null ||
            Count(Read(speech, "_controlRequests", result)) != 0 || Convert.ToInt32(Read(speech, "_controls", result) ?? -1) != 0))
            result.blockers.Add("Speech/retrieval/request work is active.");
        var streamer = YQPlayerFollowingSemanticChunkStreamer.Active;
        if (streamer == null) { result.blockers.Add("Streamer scheduler is unavailable."); return; }
        long completedSamples = streamer.BackgroundHeightTaskCount;
        long recordedOutcomes = streamer.BackgroundHeightTaskSucceededCount + streamer.BackgroundHeightTaskCanceledCount + streamer.BackgroundHeightTaskFaultedCount;
        var heightLifetime = YQPlayerFollowingSemanticChunkStreamer.CaptureHeightWorkerLifetime();
        result.details["heightWorkerLifetime"] = heightLifetime;
        if (!heightLifetime.IsQuiescent)
            result.blockers.Add("Height worker/retirement lifetime is active or incompletely observed.");
        var work = new Dictionary<string, object>();
        foreach (string name in new[] { "_semanticSaveRoutine", "_terrainPreparation", "_activeGenerations",
            "_continuationPopulationWork", "_frontierConstructionWork", "_terrainHeightPrefetches", "_provisionalGroundSamplers", "_abandonedProvisionalGroundWorkerCount" })
        {
            object value = Read(streamer, name, result);
            work[name] = value is Coroutine ? (object)true : value is int ? value : (object)Count(value);
            if (value is Coroutine || (value is int number && number != 0) || (value != null && !(value is int) && Count(value) != 0))
                result.blockers.Add("Streamer work still retained: " + name);
        }
        result.details["streamer"] = new { streamer.PendingTerrainCount, streamer.PendingTerrainPaintCount,
            backgroundHeightCompletedSamples = completedSamples, backgroundHeightRecordedOutcomes = recordedOutcomes, work };
        if (completedSamples != recordedOutcomes || streamer.PendingTerrainCount != 0 || streamer.PendingTerrainPaintCount != 0)
            result.blockers.Add("Streamer work queue/background workers are not idle.");
        // note: Include private materializer work and its actual Dispose continuations even after sampling records leave the streamer maps.
        var biomeLifetime = YQContinuousWorldFeatureMaterializer.CaptureBiomeWorkerLifetime();
        result.details["biomeWorkerLifetime"] = biomeLifetime;
        if (!biomeLifetime.IsQuiescent)
            result.blockers.Add("Biome worker/retirement lifetime is active or incompletely observed.");
    }

    private static void CapturePaths(Capture result, string profileId)
    {
        string root = Path.Combine(Application.persistentDataPath, "Profiles");
        string folder = Path.GetFullPath(Path.Combine(root, profileId));
        if (!IsChild(folder, root)) { result.blockers.Add("Profile path escapes the canonical Profiles root."); return; }
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string file in new[] { Path.Combine(root, "profiles_manifest.json"),
            Path.Combine(Application.persistentDataPath, "player_state.json"), Path.Combine(Application.persistentDataPath, "world_state.json"),
            Path.Combine(folder, "player_state.json"), Path.Combine(folder, "world_state.json") })
        { paths.Add(file); paths.Add(file + ".bak"); paths.Add(file + ".tmp"); }
        result.backupInventory.Add(new PathWitness { path = folder, kind = "whole_disposable_profile_directory", exists = Directory.Exists(folder) });
        try
        {
            // note: Inventory only the accepted disposable profile tree, never other profile folders.
            // Refuse reparse points rather than following a link outside the reviewed backup boundary.
            var directories = new Queue<string>(); directories.Enqueue(folder);
            int entries = 0;
            while (directories.Count > 0)
            {
                string directory = directories.Dequeue();
                if (!Directory.Exists(directory)) continue;
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                { result.blockers.Add("Reparse directory prevents complete safe backup inventory: " + directory); continue; }
                foreach (string path in Directory.EnumerateFileSystemEntries(directory))
                {
                    if (++entries > MaximumFiles) { result.blockers.Add("Backup inventory bound exceeded."); directories.Clear(); break; }
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    { result.blockers.Add("Reparse entry prevents complete safe backup inventory: " + path); continue; }
                    if (Directory.Exists(path)) directories.Enqueue(path); else paths.Add(path);
                }
            }
        }
        catch (Exception error) { result.blockers.Add("Backup inventory incomplete: " + error.Message); }
        long total = 0;
        foreach (string path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            var witness = new PathWitness { path = path, kind = path.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) ? "temporary_absence_witness" : "file", exists = File.Exists(path) };
            try
            {
                if (witness.exists)
                {
                    witness.bytes = new FileInfo(path).Length; total += witness.bytes;
                    if (witness.bytes > MaximumFileBytes || total > MaximumTotalBytes) throw new IOException("Bounded hash budget exceeded.");
                    if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse file rejected.");
                    witness.sha256 = Hash(path);
                    // note: Unknown or raced inventory bytes cannot qualify a reviewed backup boundary.
                    if (string.IsNullOrWhiteSpace(witness.sha256) || new FileInfo(path).Length != witness.bytes)
                        throw new IOException("File disappeared or changed during bounded inventory hashing.");
                    if (witness.kind == "temporary_absence_witness") result.blockers.Add("Existing temporary file must be reviewed and preserved: " + path);
                }
            }
            catch (Exception error) { witness.failure = error.Message; result.blockers.Add("Backup path witness incomplete: " + path); }
            result.backupInventory.Add(witness);
        }
    }

    private static Capture Finish(Capture result)
    {
        var stableInput = ReadExplicitRequestInputLease(result);
        result.blockers = result.blockers.Distinct().ToList();
        result.knownOwnerQuiescence = result.blockers.Count == 0;
        // note: The explicit test request, never held/edge input, authorizes helper admission.
        // Keep all stable input fields, documents, terms, path bytes, epochs and idle-state qualification in the lease.
        result.leaseFingerprint = TextHash(JsonConvert.SerializeObject(new { result.runtimeSha256, result.runtimeMvid,
            result.activeProfileId, result.activeCommitId, result.activeRevision, result.requestEpoch,
            canonical = result.details.TryGetValue("canonicalOwners", out var value) ? value : null,
            input = stableInput,
            dialogue = result.details.TryGetValue("dialogue", out var dialogue) ? dialogue : null,
            displayed = result.details.TryGetValue("displayedOfferSnapshot", out var displayed) ? displayed : null,
            presentation = result.details.TryGetValue("actualRenderedPresentationCurrent", out var presentation) ? presentation : null,
            result.pendingOffers, result.backupInventory, result.knownOwnerQuiescence }, Json));
        return result;
    }

    private static object Read(object instanceOrType, string name, Capture capture)
    {
        if (instanceOrType == null) { capture.blockers.Add("Missing owner for observed field " + name); return null; }
        Type type = instanceOrType as Type ?? instanceOrType.GetType();
        var field = type.GetField(name, Fields);
        if (field == null) { capture.blockers.Add("Required observation field unavailable: " + type.FullName + "." + name); return null; }
        return field.GetValue(instanceOrType is Type ? null : instanceOrType);
    }
    private static bool Flag(object owner, string name, Capture capture)
    {
        object observed = Read(owner, name, capture);
        if (observed is bool value) return value;
        // note: A missing or changed field type is unknown, never an observed false/idle flag.
        capture.blockers.Add("Boolean observation type unavailable or changed: " + name);
        return false;
    }
    private static int Count(object collection)
    {
        // note: These are the reviewed collection types at the exact observation sites; never invoke an arbitrary reflected Count getter.
        if (collection == null) return 0;
        if (collection is ICollection value) return value.Count;
        if (collection is HashSet<object> manualTokens) return manualTokens.Count;
        if (collection is HashSet<UnityEngine.Networking.UnityWebRequest> requests) return requests.Count;
        return -1; // Unknown counts remain blocking through every existing != 0 caller.
    }
    private static float[] Position(Transform transform) => new[] { transform.position.x, transform.position.y, transform.position.z };
    private static bool IsChild(string path, string root) => !string.IsNullOrWhiteSpace(path) &&
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    private static string Hash(string path)
    {
        if (!File.Exists(path)) return null;
        using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    private static string TextHash(string text)
    { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant(); }
    private static void Require(bool accepted, string failure) { if (!accepted) throw new InvalidOperationException(failure); }
}
#endif
