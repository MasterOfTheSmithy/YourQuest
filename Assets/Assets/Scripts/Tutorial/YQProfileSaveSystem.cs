// Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using FrontierTiming = YQGeneratedWorldRuntimeBuilder.FrontierPublicationDiagnostics;

[DisallowMultipleComponent]
public sealed class YQProfileSaveSystem : MonoBehaviour
{
    public static YQProfileSaveSystem Instance { get; private set; }

    [Serializable]
    public sealed class ProfileManifest
    {
        public int schemaVersion = YQStateContract.CurrentProfileManifestSchemaVersion;
        public List<ProfileEntry> profiles = new List<ProfileEntry>();
        public string activeProfileId = string.Empty;
        public string activeCommitId = string.Empty;
        public int activeRevision;
        public List<YQProfileCommitRecord> commits = new List<YQProfileCommitRecord>();

        internal void SelectLoadedProfile(string profileId, YQProfileCommitRecord verifiedCommit)
        {
            // note: Profile loading selects one already verified paired revision; never retain the previously selected profile's durable pointer.
            if (string.IsNullOrWhiteSpace(profileId) || !profiles.Exists(item => item != null &&
                    string.Equals(item.profileId, profileId, StringComparison.OrdinalIgnoreCase)) ||
                (verifiedCommit != null && (!string.Equals(verifiedCommit.profileId, profileId, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(verifiedCommit.status, "complete", StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(verifiedCommit.commitId) || verifiedCommit.revision <= 0)) ||
                (verifiedCommit == null && commits.Exists(item => item != null &&
                    string.Equals(item.profileId, profileId, StringComparison.OrdinalIgnoreCase))))
                throw new InvalidOperationException("Loaded profile selection has no matching verified revision.");
            activeProfileId = profileId;
            activeCommitId = verifiedCommit?.commitId ?? string.Empty;
            activeRevision = verifiedCommit?.revision ?? 0;
        }

        public ProfileManifest PrepareCommit(YQProfileTransactionReceipt receipt)
        {
            // note: Stage a detached pointer candidate; a failed publication must leave the accepted in-memory revision authoritative.
            ProfileManifest next = JsonConvert.DeserializeObject<ProfileManifest>(JsonConvert.SerializeObject(this));
            ProfileEntry owner = next.profiles.Find(profile => profile != null &&
                string.Equals(profile.profileId, receipt.profileId, StringComparison.OrdinalIgnoreCase));
            if (owner == null) throw new InvalidOperationException("Commit has no owning profile.");
            next.commits.RemoveAll(commit => commit != null && string.Equals(commit.commitId, receipt.commitId, StringComparison.OrdinalIgnoreCase));
            next.commits.Add(new YQProfileCommitRecord
            {
                profileId = receipt.profileId, commitId = receipt.commitId, revision = receipt.revision,
                previousCommitId = receipt.previousCommitId, playerChecksum = receipt.playerChecksum,
                worldChecksum = receipt.worldChecksum, status = "complete", committedUnix = receipt.committedUnix,
                auxiliaryDocuments = new List<YQProfileAuxiliaryDocumentRecord>(receipt.auxiliaryDocuments)
            });
            owner.updatedUnix = receipt.committedUnix;
            next.activeProfileId = receipt.profileId; next.activeCommitId = receipt.commitId; next.activeRevision = receipt.revision;
            return next;
        }
    }

    [Serializable]
    public sealed class ProfileEntry
    {
        public string profileId;
        public string displayName;
        public long createdUnix;
        public long updatedUnix;
    }

    public IReadOnlyList<ProfileEntry> Profiles => _manifest.profiles;
    public string ActiveProfileId => _manifest.activeProfileId;
    public string ActiveCommitId => _manifest.activeCommitId;
    public int ActiveRevision => _manifest.activeRevision;

    // note: These in-memory counters let the baseline observer prove profile lifecycle activity without changing persisted save data.
    public int SuccessfulLoadCount { get; private set; }
    public int SuccessfulSaveCount { get; private set; }
    public string LastLoadedProfileId { get; private set; } = string.Empty;
    public string LastSavedProfileId { get; private set; } = string.Empty;
    public YQProfileTransactionReceipt LastTransactionReceipt { get; private set; }
    public string LastFailure { get; private set; } = string.Empty;
    public bool RequiresProfileReload => _continuationReloadRequired;

    private const string ProfilesFolder = "Profiles";
    private const string ManifestFileName = "profiles_manifest.json";
    private const string PlayerFileName = "player_state.json";
    private const string WorldFileName = "world_state.json";
    private const string BackupSuffix = ".bak";

    private ProfileManifest _manifest = new ProfileManifest();
    private bool _manifestUnsupported;
    private bool _profileTransactionInProgress;
    private bool _continuationReloadRequired;
    private readonly Dictionary<string, Func<string>> _auxiliaryProviders = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _loadedAuxiliaryDocuments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    // note: Snapshot comparison uses the same explicit Unity value representation as persisted world documents, preserving optional null records.
    private static readonly JsonSerializerSettings ContinuationJsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    // note: Match PlayerStateManager's persisted representation without calling its live normalization boundary.
    private static readonly JsonSerializerSettings ContinuationPlayerJsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    public sealed class PreparedContinuationPublication
    {
        private PreparedContinuationPublication() { }
        internal static PreparedContinuationPublication Create() => new PreparedContinuationPublication();
    }

    private PreparedContinuationPublication _preparedContinuationPublication;
    private ContinuationPublicationData _preparedContinuationData;

    private sealed class ContinuationPublicationData
    {
        internal readonly YQProfileSaveSystem owner;
        internal readonly YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction;
        internal readonly PlayerStateManager playerManager;
        internal readonly WorldStateManager worldManager;
        internal readonly PlayerState player;
        internal readonly ProfileManifest manifest;
        internal readonly string manifestJson;
        internal readonly string profileId;
        internal readonly string commitId;
        internal readonly int revision;
        internal readonly int epoch;
        internal readonly string playerJson;
        internal readonly string playerSourceJson;
        internal readonly string acceptedJson;
        internal readonly WorldStateManager.PreparedContinuationSnapshot worldSnapshot;
        internal readonly Dictionary<string, Func<string>> providers;
        internal readonly Dictionary<string, string> loadedDocuments;
        internal readonly Dictionary<string, string> auxiliaryDocuments;
        internal ContinuationPublicationData(YQProfileSaveSystem owner,
            YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
            PlayerStateManager playerManager, WorldStateManager worldManager, string playerJson, string playerSourceJson,
            string acceptedJson, WorldStateManager.PreparedContinuationSnapshot worldSnapshot,
            Dictionary<string, Func<string>> providers, Dictionary<string, string> loadedDocuments,
            Dictionary<string, string> auxiliaryDocuments)
        {
            this.owner = owner; this.construction = construction; this.playerManager = playerManager; this.worldManager = worldManager;
            player = playerManager.state; manifest = owner._manifest;
            manifestJson = JsonConvert.SerializeObject(manifest, ContinuationJsonSettings);
            profileId = manifest.activeProfileId; commitId = manifest.activeCommitId; revision = manifest.activeRevision;
            epoch = YQServiceLifecycle.RequestEpoch; this.playerJson = playerJson;
            this.playerSourceJson = playerSourceJson;
            this.acceptedJson = acceptedJson; this.worldSnapshot = worldSnapshot;
            this.providers = providers; this.loadedDocuments = loadedDocuments; this.auxiliaryDocuments = auxiliaryDocuments;
        }

    }

    private string RootDir => Path.Combine(Application.persistentDataPath, ProfilesFolder);
    private string ManifestPath => Path.Combine(RootDir, ManifestFileName);
    private string ActivePlayerPath => Path.Combine(Application.persistentDataPath, PlayerFileName);
    private string ActiveWorldPath => Path.Combine(Application.persistentDataPath, WorldFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureBootstrap()
    {
        // note: Explicit isolated traversal scenes must never load, recover, or rewrite a real player profile.
        if (YQHomeTraversalReview.IsIsolatedReview) return;
        if (FindFirstObjectByType<YQProfileSaveSystem>() != null)
            return;

        GameObject go = new GameObject("YQProfileSaveSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<YQProfileSaveSystem>();
    }

    private void Awake()
    {
        // note: Also guard a scene-authored component before any directory creation or snapshot recovery.
        if (YQHomeTraversalReview.IsIsolatedReview) { Destroy(gameObject); return; }
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        Directory.CreateDirectory(RootDir);
        LoadManifest();

        if (_manifest.profiles == null)
            _manifest.profiles = new List<ProfileEntry>();
        if (_manifest.commits == null)
            _manifest.commits = new List<YQProfileCommitRecord>();
        if (_manifest.schemaVersion <= 0)
            _manifest.schemaVersion = YQStateContract.CurrentProfileManifestSchemaVersion;

        RecoverNewerActiveSnapshot();
    }

    public string CreateNewProfile(string displayName)
    {
        return CreateNewProfile(displayName, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public string CreateNewProfile(string displayName, string pronouns, string bodyFrame, string lifeDirection, string vow, string appearanceSummary)
    {
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return string.Empty; }
        // note: Profile creation writes standalone documents before its ordinary load/commit path.
        if (YQDeveloperConsoleGate.BlocksPersistence) { LastFailure = YQDeveloperConsoleGate.SaveBlocked; return null; }
        if (_manifestUnsupported)
        {
            LastFailure = "Unsupported future profile manifest; creation is disabled until a compatible build can migrate it.";
            return string.Empty;
        }
        string trimmedName = string.IsNullOrWhiteSpace(displayName) ? "New Adventurer" : displayName.Trim();
        string profileId = Guid.NewGuid().ToString("N");
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        ProfileEntry entry = new ProfileEntry
        {
            profileId = profileId,
            displayName = trimmedName,
            createdUnix = now,
            updatedUnix = now
        };

        _manifest.profiles.Add(entry);
        // note: Keep the previous active profile until both new documents have been loaded successfully.
        EnsureProfileFolder(profileId);

        PlayerState player = new PlayerState();
        player.displayName = trimmedName;
        player.playerId = profileId;
        player.EnsureCollections();
        ApplyCharacterCreation(player, pronouns, bodyFrame, lifeDirection, vow, appearanceSummary);
        SavePlayerTo(Path.Combine(GetProfileFolder(profileId), PlayerFileName), player);

        WorldState world = WorldState.CreateDefault();
        world.worldIdentity.ownerProfileId = profileId;
        // note: A new profile owns a distinct world identity from its first snapshot; the default seed must never make separate saves share one world ID.
        world.worldIdentity.worldId = YQStateContract.StableId(YQStableEntityKind.World, profileId + "|world", 0);
        SaveWorldTo(Path.Combine(GetProfileFolder(profileId), WorldFileName), world);

        SaveManifest();
        // note: Activate only after the new profile has a complete paired snapshot and a published commit pointer.
        if (!LoadProfile(profileId) || !SaveActiveProfile())
            return string.Empty;
        return profileId;
    }

    public bool ApplyCharacterCreationToActive(string pronouns, string bodyFrame, string lifeDirection, string vow, string appearanceSummary)
    {
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return false; }
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return false;

        ApplyCharacterCreation(psm.state, pronouns, bodyFrame, lifeDirection, vow, appearanceSummary);
        psm.Save();
        return SaveActiveProfile();
    }

    public bool RegisterAuxiliaryDocument(string documentId, Func<string> snapshotProvider)
    {
        if (string.IsNullOrWhiteSpace(documentId) || snapshotProvider == null)
            return false;
        _auxiliaryProviders[documentId.Trim()] = snapshotProvider;
        return true;
    }

    public bool UnregisterAuxiliaryDocument(string documentId)
    {
        return !string.IsNullOrWhiteSpace(documentId) && _auxiliaryProviders.Remove(documentId.Trim());
    }

    // note: Expose only checksum-verified documents from the selected profile revision to runtime owners such as terrain restoration.
    public bool TryGetLoadedAuxiliaryDocument(string documentId, out string contents)
    {
        contents = null;
        return !string.IsNullOrWhiteSpace(documentId) &&
               _loadedAuxiliaryDocuments.TryGetValue(documentId.Trim(), out contents);
    }

    // note: Let an auxiliary owner recover an older paired snapshot only after it validates that document's domain identity.
    internal bool TryGetPriorAuxiliaryDocument(
        string documentId,
        Func<string, bool> acceptCandidate,
        out string contents)
    {
        contents = null;
        if (string.IsNullOrWhiteSpace(documentId) || acceptCandidate == null ||
            string.IsNullOrWhiteSpace(_manifest.activeProfileId) || _manifest.commits == null)
            return false;

        string profileId = _manifest.activeProfileId;
        YQProfileCommitRecord latest = FindLatestCommit(profileId);
        if (latest == null)
            return false;

        List<YQProfileCommitRecord> priorCommits = new List<YQProfileCommitRecord>();
        for (int index = 0; index < _manifest.commits.Count; index++)
        {
            YQProfileCommitRecord commit = _manifest.commits[index];
            if (commit != null && commit.revision < latest.revision &&
                string.Equals(commit.profileId, profileId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(commit.status, "complete", StringComparison.OrdinalIgnoreCase))
                priorCommits.Add(commit);
        }
        priorCommits.Sort((left, right) => right.revision.CompareTo(left.revision));

        string folder = GetProfileFolder(profileId);
        for (int commitIndex = 0; commitIndex < priorCommits.Count; commitIndex++)
        {
            YQProfileCommitRecord commit = priorCommits[commitIndex];
            if (commit.auxiliaryDocuments == null)
                continue;

            YQProfileAuxiliaryDocumentRecord matchingDocument = null;
            for (int documentIndex = 0; documentIndex < commit.auxiliaryDocuments.Count; documentIndex++)
            {
                YQProfileAuxiliaryDocumentRecord document = commit.auxiliaryDocuments[documentIndex];
                if (document != null && string.Equals(document.documentId, documentId.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    matchingDocument = document;
                    break;
                }
            }
            if (matchingDocument == null ||
                !YQProfileCommitStore.TryReadAuxiliaryDocument(
                    folder,
                    commit.commitId,
                    matchingDocument.documentId,
                    matchingDocument.checksum,
                    out string documentPath,
                    out _))
                continue;

            try
            {
                string candidate = File.ReadAllText(documentPath);
                if (!acceptCandidate(candidate))
                    continue;

                // note: Validate the paired player/world checksums only after domain validation accepts this auxiliary copy.
                if (!YQProfileCommitStore.TryReadCommit(
                        folder,
                        commit.commitId,
                        commit.playerChecksum,
                        commit.worldChecksum,
                        out _,
                        out _,
                        out _))
                    continue;

                contents = candidate;
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // note: A damaged prior auxiliary copy is skipped so a later checksum-valid revision can still recover it.
            }
        }

        return false;
    }

    public bool SaveActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(_manifest.activeProfileId))
            return false;
        return SaveProfile(_manifest.activeProfileId);
    }

    public bool SaveProfile(string profileId)
    {
        // note: Auxiliary providers can call back into persistence; one paired transaction must retain sole pointer-publication ownership.
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return false; }
        _profileTransactionInProgress = true;
        try { return SaveProfileCore(profileId); }
        finally { _profileTransactionInProgress = false; }
    }

    private bool SaveProfileCore(string profileId)
    {
        // note: An exceptional failure after a durable continuation pointer requires canonical reload, not another save of ambiguous live state.
        if (_continuationReloadRequired) return FailTransaction(profileId, "Published continuation requires profile reload before saving.");
        // note: Automatic, menu and quit commits cannot publish the developer's temporary test state.
        if (YQDeveloperConsoleGate.BlocksPersistence) { LastFailure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        if (_manifestUnsupported)
            return FailTransaction(profileId, "Unsupported future profile manifest; save refused.");
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null)
            return false;

        PlayerStateManager psm = PlayerStateManager.Instance;
        WorldStateManager wsm = WorldStateManager.Instance;
        if (psm == null || psm.state == null || wsm == null || wsm.State == null)
            return false;

        // note: This API saves the active character, not a copy over an arbitrary selected profile.
        if (!string.Equals(profileId, _manifest.activeProfileId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(profileId, psm.state.playerId, StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogError("[YQProfileSaveSystem] PROFILE SAVE REJECTED: active player does not own profile " + profileId);
            return false;
        }
        // note: The world document carries its owning profile identity so paired commits cannot silently cross character saves.
        wsm.State.worldIdentity ??= new YQWorldIdentityRecord();
        wsm.State.worldIdentity.ownerProfileId = profileId;

        if (!psm.TryPrepareSnapshot(out string playerJson, out string playerFailure))
            return FailTransaction(profileId, "Player snapshot failed: " + playerFailure);
        if (!wsm.TryPrepareSnapshot(out string worldJson, out string worldFailure))
            return FailTransaction(profileId, "World snapshot failed: " + worldFailure);
        YQIdentityValidationResult validation = YQStateReferenceValidator.Validate(psm.state, wsm.State);
        if (!validation.IsValid)
            return FailTransaction(profileId, "Reference validation failed: " + string.Join("; ", validation.failures));
        Dictionary<string, string> auxiliarySnapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, Func<string>> provider in _auxiliaryProviders)
        {
            string snapshot;
            try { snapshot = provider.Value != null ? provider.Value() : null; }
            catch (Exception exception) { return FailTransaction(profileId, "Auxiliary snapshot failed for " + provider.Key + ": " + exception.Message); }
            if (snapshot == null)
                return FailTransaction(profileId, "Auxiliary snapshot returned null for " + provider.Key + ".");
            auxiliarySnapshots[provider.Key] = snapshot;
        }

        string folder = EnsureProfileFolder(profileId);
        YQProfileCommitRecord previous = FindLatestCommit(profileId);
        int revision = previous != null ? previous.revision + 1 : 1;
        if (!YQProfileCommitStore.TryCommit(folder, profileId, ActivePlayerPath, ActiveWorldPath, revision, playerJson, worldJson, auxiliarySnapshots, null, out YQProfileTransactionReceipt receipt))
            return FailTransaction(profileId, receipt != null ? receipt.failure : "Profile transaction failed.");

        LastTransactionReceipt = receipt;
        receipt.previousCommitId = previous != null ? previous.commitId : string.Empty;
        ProfileManifest candidate = _manifest.PrepareCommit(receipt);
        if (!SaveManifest(candidate))
            return FailTransaction(profileId, "Commit pointer publication failed: " + LastFailure);
        _manifest = candidate;
        LastFailure = string.Empty;
        receipt.published = true;
        SuccessfulSaveCount++;
        LastSavedProfileId = profileId;
        return true;
    }

    public static string CaptureOfferTerms(PendingProgressionOfferRecord offer)
    {
        // note: The identity alone is insufficient: a refreshed offer can retain its ID while changing its terms during inference.
        return offer == null ? null : JsonConvert.SerializeObject(offer, ContinuationJsonSettings);
    }

    public bool TryCommitOfferDecision(PlayerState expectedPlayer, string offerId, string expectedTerms,
        string decision, out string message, out string failure)
    {
        // note: Preserve the existing public entry point; only displayed choices supply an extra prepublication presentation guard.
        return TryCommitOfferDecision(expectedPlayer, offerId, expectedTerms, decision, out message, out failure, null);
    }

    public bool TryCommitOfferDecision(PlayerState expectedPlayer, string offerId, string expectedTerms,
        string decision, out string message, out string failure, Func<bool> presentationCurrent)
    {
        message = string.Empty;
        failure = string.Empty;
        if (_profileTransactionInProgress) { failure = "A paired profile transaction is already in progress."; return false; }
        _profileTransactionInProgress = true;
        YQProfileTransactionReceipt receipt = null;
        try
        {
            // note: Join the existing projection writer, then evaluate once on a detached player. Rejection never writes a candidate working copy.
            int epoch = YQServiceLifecycle.RequestEpoch;
            if (presentationCurrent != null && !presentationCurrent())
            { failure = "The displayed offer context is no longer current."; return false; }
            if (!TryRequireContinuationOwner(out var psm, out var wsm, out var previous, out failure) ||
                !ReferenceEquals(expectedPlayer, psm.state) || !psm.TryFlushPendingAutosave(out failure))
            { if (string.IsNullOrEmpty(failure)) failure = "The active profile changed."; return false; }
            var offer = expectedPlayer.FindOfferById(offerId);
            if (offer == null || !offer.IsPending || string.IsNullOrEmpty(expectedTerms) ||
                !string.Equals(CaptureOfferTerms(offer), expectedTerms, StringComparison.Ordinal) ||
                (decision != "accept" && decision != "decline"))
            { failure = "The offer changed or is no longer pending."; return false; }

            var manifest = _manifest;
            var world = wsm.State;
            string sourcePlayerJson = JsonConvert.SerializeObject(expectedPlayer, ContinuationJsonSettings);
            string worldJson = JsonConvert.SerializeObject(world, ContinuationJsonSettings);
            string manifestJson = JsonConvert.SerializeObject(manifest, ContinuationJsonSettings);
            var prepared = JsonConvert.DeserializeObject<PlayerState>(sourcePlayerJson, ContinuationJsonSettings);
            if (prepared == null) { failure = "Player snapshot could not be prepared."; return false; }
            bool applied = decision == "accept" ? prepared.AcceptOffer(offerId, out message) : prepared.DeclineOffer(offerId, out message);
            if (!applied) { failure = message; message = string.Empty; return false; }
            YQStateIdentity.EnsurePlayerState(prepared);

            // note: Compare an explicit typed projection with the complete candidate before publication; future domain changes cannot silently escape the bounded apply fields.
            var projected = JsonConvert.DeserializeObject<PlayerState>(sourcePlayerJson, ContinuationJsonSettings);
            projected.ApplyPreparedOfferDecision(prepared);
            string preparedJson = JsonConvert.SerializeObject(prepared, ContinuationJsonSettings);
            if (!string.Equals(JsonConvert.SerializeObject(projected, ContinuationJsonSettings), preparedJson, StringComparison.Ordinal))
            { failure = "Offer evaluation changed fields outside its prepared decision contract."; message = string.Empty; return false; }
            var detachedWorld = JsonConvert.DeserializeObject<WorldState>(worldJson, ContinuationJsonSettings);
            var validation = YQStateReferenceValidator.Validate(prepared, detachedWorld);
            if (!validation.IsValid)
            { failure = "Offer reference validation failed: " + string.Join("; ", validation.failures); message = string.Empty; return false; }
            if (!TryCaptureContinuationAuxiliary(out var providers, out var loaded, out var auxiliary, out failure)) return false;

            bool ContextIsCurrent(out string why)
            {
                why = "The profile or prepared offer context changed before publication.";
                if (presentationCurrent != null && !presentationCurrent()) return false;
                if (!TryRequireContinuationOwner(out var currentPlayer, out var currentWorld, out _, out why) ||
                    !ReferenceEquals(currentPlayer, psm) || !ReferenceEquals(currentWorld, wsm) ||
                    !ReferenceEquals(currentPlayer.state, expectedPlayer) || !ReferenceEquals(currentWorld.State, world) ||
                    !ReferenceEquals(_manifest, manifest) || !YQServiceLifecycle.IsCurrent(epoch) ||
                    !string.Equals(JsonConvert.SerializeObject(expectedPlayer, ContinuationJsonSettings), sourcePlayerJson, StringComparison.Ordinal) ||
                    !string.Equals(JsonConvert.SerializeObject(world, ContinuationJsonSettings), worldJson, StringComparison.Ordinal) ||
                    !string.Equals(JsonConvert.SerializeObject(_manifest, ContinuationJsonSettings), manifestJson, StringComparison.Ordinal) ||
                    _auxiliaryProviders.Count != providers.Count || !SameAuxiliaryDocuments(_loadedAuxiliaryDocuments, loaded))
                { if (string.IsNullOrEmpty(why)) why = "The profile or prepared offer context changed before publication."; return false; }
                foreach (var provider in providers)
                    if (!_auxiliaryProviders.TryGetValue(provider.Key, out var current) || !ReferenceEquals(provider.Value, current))
                    { why = "Auxiliary provider ownership changed."; return false; }
                why = string.Empty;
                return true;
            }

            if (!ContextIsCurrent(out failure)) return false;
            string folder = GetProfileFolder(manifest.activeProfileId);
            if (!YQProfileCommitStore.TryStagePairedCommit(folder, manifest.activeProfileId, checked(previous.revision + 1),
                    JsonConvert.SerializeObject(prepared, ContinuationPlayerJsonSettings), worldJson, auxiliary, null, out receipt))
            { failure = receipt?.failure ?? "Offer paired revision failed."; return false; }
            receipt.previousCommitId = previous.commitId;
            var next = manifest.PrepareCommit(receipt);
            if (!TryCaptureContinuationAuxiliary(out _, out _, out var currentDocuments, out failure) ||
                !SameAuxiliaryDocuments(currentDocuments, auxiliary) || !ContextIsCurrent(out failure))
            { if (string.IsNullOrEmpty(failure)) failure = "Auxiliary documents changed before publication."; return false; }
            if (!SaveManifest(next)) { failure = "Offer commit pointer publication failed: " + LastFailure; return false; }

            // note: No callbacks, normalization, IDs, or validation follow the durable pointer. Success refers to this exact retained decision, even if projection refresh needs recovery.
            _manifest = next;
            receipt.published = true;
            LastTransactionReceipt = receipt;
            expectedPlayer.ApplyPreparedOfferDecision(prepared);
            SuccessfulSaveCount++;
            LastSavedProfileId = receipt.profileId;
            LastFailure = string.Empty;
            if (!YQProfileCommitStore.TryRefreshPublishedProjections(folder, ActivePlayerPath, ActiveWorldPath, receipt, out var refreshFailure))
            {
                receipt.failure = "Offer committed; working projections require refresh: " + refreshFailure;
                LastFailure = receipt.failure;
                Debug.LogWarning("[YQProfileSaveSystem] " + LastFailure);
            }
            return true;
        }
        catch (Exception exception)
        {
            if (receipt != null && receipt.published)
            {
                // note: A published decision cannot be relabeled as failed or retried after an exceptional live apply; canonical reload is required.
                _continuationReloadRequired = true;
                failure = "Offer committed; profile reload required: " + exception.Message;
                message = "Your choice was saved. Reload this journey before continuing.";
                receipt.failure = failure;
                if (YQPlayerFollowingSemanticChunkStreamer.Active != null) YQPlayerFollowingSemanticChunkStreamer.Active.enabled = false;
                return true;
            }
            failure = "Offer decision could not be saved: " + exception.Message;
            message = string.Empty;
            return false;
        }
        finally
        {
            _profileTransactionInProgress = false;
            if (!string.IsNullOrEmpty(failure))
            {
                LastFailure = failure;
                if (receipt != null) { receipt.failure = failure; LastTransactionReceipt = receipt; }
                if (receipt == null || !receipt.published) message = string.Empty;
            }
        }
    }

    // note: Social publication reuses this profile owner and existing paired revision pointer; domain preparation owns no save route.
    private delegate bool PrepareDisposition(PlayerState player, WorldState world, out WorldState prepared, out YQDispositionDomain.Result result, out string failure);

    public bool TryCommitDisposition(PlayerState expectedPlayer, WorldState expectedWorld, YQDispositionDomain.Admission admission,
        out YQProfileTransactionReceipt receipt, out YQDispositionDomain.Result result, out string failure)
    {
        receipt = null; result = null; failure = "Disposition admission belongs to a different owner.";
        if (admission == null || !ReferenceEquals(admission.player, expectedPlayer) || !ReferenceEquals(admission.world, expectedWorld)) return false;
        return TryCommitDispositionCore(expectedPlayer, expectedWorld,
            (PlayerState p, WorldState w, out WorldState prepared, out YQDispositionDomain.Result outcome, out string why) =>
                YQDispositionDomain.TryPrepare(p, w, admission, out prepared, out outcome, out why), out receipt, out result, out failure);
    }

    public bool TryCommitDispositionPolicy(PlayerState expectedPlayer, WorldState expectedWorld, string rawJson, string source, string prompt,
        out YQProfileTransactionReceipt receipt, out string failure, Func<bool> producerCurrent = null)
    {
        return TryCommitDispositionCore(expectedPlayer, expectedWorld,
            (PlayerState p, WorldState w, out WorldState prepared, out YQDispositionDomain.Result result, out string why) => {
                prepared = null; result = null;
                if (!YQDispositionDomain.TryPreparePolicy(p, w, rawJson, source, prompt, out var proposal, out why)) return false;
                string id = Newtonsoft.Json.Linq.JObject.Parse(proposal.normalizedPayloadJson).Value<string>("contextId");
                var matches = w.acceptedContent.FindAll(r => r != null && r.contentId == id);
                if (matches.Count > 0) {
                    if (matches.Count != 1 || matches[0].schemaVersion != YQDispositionDomain.ContextSchema || matches[0].normalizedPayloadJson != proposal.normalizedPayloadJson ||
                        matches[0].contentHash != YQStateContract.Sha256Hex(proposal.normalizedPayloadJson) || matches[0].kind != YQStableEntityKind.Content)
                    { why = "Accepted policy ID cannot be refreshed with different terms."; return false; }
                    prepared = w; why = ""; return true;
                }
                if (!YQContentProposalBoundary.TryCommit(w, id, YQStableEntityKind.Content, proposal, w.stateRevision, out var mutation))
                { why = mutation.message; return false; }
                prepared = w; why = ""; return true;
            }, out receipt, out _, out failure, false, producerCurrent);
    }

    public bool TryCommitSocialRequest(PlayerState expectedPlayer, WorldState expectedWorld, string raw, string source, string prompt,
        out YQProfileTransactionReceipt receipt, out string failure, Func<bool> producerCurrent)
    {
        receipt = null; failure = "Supply request interaction changed.";
        if (producerCurrent == null || !producerCurrent()) return false;
        return TryCommitDispositionCore(expectedPlayer, expectedWorld,
            (PlayerState p, WorldState w, out WorldState prepared, out YQDispositionDomain.Result result, out string why) =>
                YQSocialAidDomain.PrepareRequest(p, w, raw, source, prompt, out prepared, out result, out why), out receipt, out _, out failure, false, producerCurrent);
    }

    internal bool TryCommitSocialAid(PlayerState expectedPlayer, WorldState expectedWorld, YQSocialAidDomain.AidDelivery token,
        out YQProfileTransactionReceipt receipt, out YQDispositionDomain.Result result, out string failure)
    {
        receipt = null; result = null; failure = "Gift delivery owner changed.";
        if (token == null || !ReferenceEquals(token.player, expectedPlayer) || !ReferenceEquals(token.world, expectedWorld) || token.current == null || !token.current()) return false;
        return TryCommitDispositionCore(expectedPlayer, expectedWorld,
            (PlayerState p, WorldState w, out WorldState prepared, out YQDispositionDomain.Result outcome, out string why) =>
                YQSocialAidDomain.PrepareAid(p, w, token, out prepared, out outcome, out why), out receipt, out result, out failure, true, token.current);
    }

    internal bool TryCommitSocialReport(PlayerState expectedPlayer, WorldState expectedWorld, YQSocialAidDomain.ReportDelivery token,
        out YQProfileTransactionReceipt receipt, out YQDispositionDomain.Result result, out string failure)
    {
        receipt = null; result = null; failure = "NPC report delivery owner changed.";
        if (token == null || !ReferenceEquals(token.player, expectedPlayer) || !ReferenceEquals(token.world, expectedWorld) || token.current == null || !token.current()) return false;
        return TryCommitDispositionCore(expectedPlayer, expectedWorld,
            (PlayerState p, WorldState w, out WorldState prepared, out YQDispositionDomain.Result outcome, out string why) =>
                YQSocialAidDomain.PrepareReport(p, w, token, out prepared, out outcome, out why), out receipt, out result, out failure, false, token.current);
    }
    private bool TryCommitDispositionCore(PlayerState expectedPlayer, WorldState expectedWorld, PrepareDisposition prepare,
        out YQProfileTransactionReceipt receipt, out YQDispositionDomain.Result result, out string failure,
        bool allowAidInventory = false, Func<bool> producerCurrent = null)
    {
        receipt = null; result = null; failure = "";
        if (_profileTransactionInProgress) { failure = "A paired profile transaction is already in progress."; return false; }
        _profileTransactionInProgress = true;
        try {
            int epoch = YQServiceLifecycle.RequestEpoch;
            if (!TryRequireContinuationOwner(out var psm, out var wsm, out var previous, out failure) ||
                !ReferenceEquals(expectedPlayer, psm.state) || !ReferenceEquals(expectedWorld, wsm.State) || !psm.TryFlushPendingAutosave(out failure))
            { if (string.IsNullOrEmpty(failure)) failure = "The active profile changed."; return false; }
            var manifest = _manifest;
            string sourcePlayer = JsonConvert.SerializeObject(expectedPlayer, ContinuationJsonSettings);
            string sourceWorld = JsonConvert.SerializeObject(expectedWorld, ContinuationJsonSettings);
            string sourceManifest = JsonConvert.SerializeObject(manifest, ContinuationJsonSettings);
            var player = JsonConvert.DeserializeObject<PlayerState>(sourcePlayer, ContinuationJsonSettings);
            var detached = JsonConvert.DeserializeObject<WorldState>(sourceWorld, ContinuationJsonSettings);
            if (!prepare(player, detached, out var prepared, out result, out failure)) return false;
            var validation = YQStateReferenceValidator.Validate(player, prepared);
            if (!validation.IsValid) { failure = "Social reference validation failed: " + string.Join("; ", validation.failures); return false; }
            var projectedPlayer = JsonConvert.DeserializeObject<PlayerState>(sourcePlayer, ContinuationJsonSettings);
            var inventoryApply = allowAidInventory ? new YQPreparedSocialInventory(player, prepared) : null;
            inventoryApply?.ApplyPlayer(projectedPlayer);
            string permittedPlayerJson = allowAidInventory ? JsonConvert.SerializeObject(projectedPlayer, ContinuationJsonSettings) : sourcePlayer;
            if (JsonConvert.SerializeObject(player, ContinuationJsonSettings) != permittedPlayerJson)
            { failure = "Social preparation altered player fields outside the typed aid inventory projection."; return false; }
            string preparedJson = JsonConvert.SerializeObject(prepared, ContinuationJsonSettings);
            var projected = JsonConvert.DeserializeObject<WorldState>(sourceWorld, ContinuationJsonSettings);
            new YQPreparedDispositionWorld(projected, prepared).Apply(projected);
            inventoryApply?.ApplyWorld(projected);
            if (JsonConvert.SerializeObject(projected, ContinuationJsonSettings) != preparedJson)
            { failure = "Social evaluation changed fields outside its typed world projection."; return false; }
            var liveApply = new YQPreparedDispositionWorld(expectedWorld, prepared);
            if (!TryCaptureContinuationAuxiliary(out var providers, out var loaded, out var auxiliary, out failure)) return false;
            bool Current(out string why) {
                why = "The prepared social profile context changed before publication.";
                if (producerCurrent != null && !producerCurrent()) { why = "The actual social producer interaction changed before publication."; return false; }
                if (!TryRequireContinuationOwner(out var currentPlayer, out var currentWorld, out _, out why) ||
                    !ReferenceEquals(currentPlayer, psm) || !ReferenceEquals(currentWorld, wsm) ||
                    !ReferenceEquals(currentPlayer.state, expectedPlayer) || !ReferenceEquals(currentWorld.State, expectedWorld) ||
                    !ReferenceEquals(_manifest, manifest) || !YQServiceLifecycle.IsCurrent(epoch) ||
                    JsonConvert.SerializeObject(expectedPlayer, ContinuationJsonSettings) != sourcePlayer ||
                    JsonConvert.SerializeObject(expectedWorld, ContinuationJsonSettings) != sourceWorld ||
                    JsonConvert.SerializeObject(_manifest, ContinuationJsonSettings) != sourceManifest ||
                    _auxiliaryProviders.Count != providers.Count || !SameAuxiliaryDocuments(_loadedAuxiliaryDocuments, loaded))
                { if (string.IsNullOrEmpty(why)) why = "The prepared social profile context changed before publication."; return false; }
                foreach (var provider in providers)
                    if (!_auxiliaryProviders.TryGetValue(provider.Key, out var current) || !ReferenceEquals(provider.Value, current))
                    { why = "Auxiliary provider ownership changed."; return false; }
                why = ""; return true;
            }
            if (!Current(out failure)) return false;
            string folder = GetProfileFolder(manifest.activeProfileId);
            if (!YQProfileCommitStore.TryStagePairedCommit(folder, manifest.activeProfileId, checked(previous.revision + 1),
                JsonConvert.SerializeObject(player, ContinuationPlayerJsonSettings), preparedJson, auxiliary, null, out receipt))
            { failure = receipt?.failure ?? "Social paired revision staging failed."; return false; }
            receipt.previousCommitId = previous.commitId;
            var next = manifest.PrepareCommit(receipt);
            if (!TryCaptureContinuationAuxiliary(out _, out _, out var recaptured, out failure) || !SameAuxiliaryDocuments(recaptured, auxiliary) || !Current(out failure))
            { if (string.IsNullOrEmpty(failure)) failure = "Auxiliary documents changed before social publication."; return false; }
            if (!SaveManifest(next)) { failure = "Social pointer publication failed: " + LastFailure; return false; }
            // note: All targets/allocations are retained before publication. After the durable pointer, only typed assignments and ordinary projection refresh remain.
            _manifest = next; receipt.published = true; LastTransactionReceipt = receipt;
            liveApply.Apply(expectedWorld);
            inventoryApply?.ApplyPlayer(expectedPlayer);
            inventoryApply?.ApplyWorld(expectedWorld);
            SuccessfulSaveCount++; LastSavedProfileId = receipt.profileId; LastFailure = "";
            if (!YQProfileCommitStore.TryRefreshPublishedProjections(folder, ActivePlayerPath, ActiveWorldPath, receipt, out string refreshFailure)) {
                receipt.failure = "Social outcome saved; working projections require refresh: " + refreshFailure;
                LastFailure = receipt.failure; Debug.LogWarning("[YQProfileSaveSystem] " + LastFailure);
            }
            return true;
        } catch (Exception error) {
            if (receipt != null && receipt.published) {
                _continuationReloadRequired = true; failure = "Social outcome saved; reload this journey before continuing: " + error.Message;
                receipt.failure = failure; if (YQPlayerFollowingSemanticChunkStreamer.Active != null) YQPlayerFollowingSemanticChunkStreamer.Active.enabled = false;
                return true;
            }
            failure = "Social outcome could not be saved: " + error.Message; result = null; return false;
        } finally {
            _profileTransactionInProgress = false;
            if (!string.IsNullOrEmpty(failure)) { LastFailure = failure; if (receipt != null) { receipt.failure = failure; LastTransactionReceipt = receipt; } }
            if (receipt == null || !receipt.published) result = null;
        }
    }


    public bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        out PreparedContinuationPublication publication, out string failure)
        => TryPrepareContinuationPublicationCore(construction, null, out publication, out failure);

    internal bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        WorldStateManager.PreparedContinuationBaseWorld baseWorld, out PreparedContinuationPublication publication, out string failure)
        => TryPrepareContinuationPublicationCore(construction, baseWorld, out publication, out failure);

    private bool TryPrepareContinuationPublicationCore(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        WorldStateManager.PreparedContinuationBaseWorld baseWorld, out PreparedContinuationPublication publication, out string failure)
    {
        publication = null;
        failure = string.Empty;
        if (_profileTransactionInProgress) { failure = "A paired profile transaction is already in progress."; return false; }
        _profileTransactionInProgress = true;
        // note: One privately owned preparation may be outstanding; preparing a newer candidate retires the older marker without touching accepted state.
        _preparedContinuationPublication = null;
        _preparedContinuationData = null;
        try
        {
            int epoch = YQServiceLifecycle.RequestEpoch;
            if (construction == null) { failure = "A construction-owner acceptance token is required."; return false; }
            if (!TryRequireContinuationOwner(out PlayerStateManager psm, out WorldStateManager wsm, out _, out failure) ||
                !construction.TryValidateForPublication(wsm.State, out GeneratedSpatialContinuationV2Record accepted, out failure) ||
                !psm.TryFlushPendingAutosave(out failure)) return false;
            // note: Join only the already-owned immutable writer, then capture canonical player fields verbatim; continuation staging never repairs live accepted player content.
            string playerJson = JsonConvert.SerializeObject(psm.state, ContinuationPlayerJsonSettings);
            string playerSourceJson = JsonConvert.SerializeObject(psm.state, ContinuationJsonSettings);
            PlayerState detachedPlayer = JsonConvert.DeserializeObject<PlayerState>(playerJson, ContinuationPlayerJsonSettings);
            if (detachedPlayer == null || !YQProfileCommitStore.JsonEqualsCaptured(detachedPlayer, playerJson, ContinuationPlayerJsonSettings))
            { failure = "The player snapshot requires normalization before continuation staging; live accepted player fields were preserved."; return false; }
            if (!wsm.TryPrepareContinuationSnapshot(wsm.State, detachedPlayer, accepted, out var worldSnapshot, out failure, construction, baseWorld) ||
                !TryCaptureContinuationAuxiliary(out var providers, out var loaded, out var auxiliary, out failure)) return false;
            if (!YQServiceLifecycle.IsCurrent(epoch)) { failure = "Profile request epoch changed during continuation preparation."; return false; }
            var data = new ContinuationPublicationData(this, construction, psm, wsm, playerJson, playerSourceJson,
                JsonConvert.SerializeObject(accepted, ContinuationJsonSettings), worldSnapshot, providers, loaded, auxiliary);
            if (!TryValidateContinuationPublication(data, false, out failure)) return false;
            publication = PreparedContinuationPublication.Create();
            _preparedContinuationPublication = publication;
            _preparedContinuationData = data;
            return true;
        }
        catch (Exception exception)
        {
            failure = "Continuation preparation failed: " + exception.Message;
            return false;
        }
        finally { _profileTransactionInProgress = false; }
    }

    public bool TryPublishContinuation(PreparedContinuationPublication publication,
        out YQProfileTransactionReceipt receipt, out string failure)
    {
        receipt = null;
        failure = string.Empty;
        if (_profileTransactionInProgress) { failure = "A paired profile transaction is already in progress."; return false; }
        if (publication == null || !ReferenceEquals(publication, _preparedContinuationPublication) || _preparedContinuationData == null)
        { failure = "Continuation preparation is missing, foreign, superseded, or already consumed."; return false; }
        ContinuationPublicationData data = _preparedContinuationData;
        _preparedContinuationPublication = null;
        _preparedContinuationData = null;
        _profileTransactionInProgress = true;
        try
        {
            if (!ReferenceEquals(PlayerStateManager.Instance, data.playerManager) ||
                !ReferenceEquals(WorldStateManager.Instance, data.worldManager) || !YQServiceLifecycle.IsCurrent(data.epoch))
            { failure = "Continuation profile owners or request epoch changed after preparation."; return false; }
            // note: This barrier only joins the existing immutable projection-writer task; it invokes no profile save and never recaptures a changed player.
            if (!data.playerManager.TryFlushPendingAutosave(out failure) ||
                !TryValidateContinuationPublication(data, true, out failure)) return false;
            data.worldManager.ReserveContinuationPublication(data.worldSnapshot);
            string folder = GetProfileFolder(data.profileId);
            // note: Separate immutable staging from repeated final validation and atomic pointer publication.
            using (FrontierTiming.Begin(FrontierTiming.Operation.RevisionStage))
            if (!YQProfileCommitStore.TryStageContinuationCommit(folder, data.profileId,
                    checked(data.revision + 1), data.playerJson, data.worldSnapshot.worldJson, data.auxiliaryDocuments, null, out receipt))
            { failure = receipt?.failure ?? "Continuation paired revision failed."; return false; }
            // note: Only immutable revision files exist here. Rejected final validation or pointer publication leaves every working projection untouched.
            receipt.published = false;
            receipt.previousCommitId = data.commitId;
            ProfileManifest next = data.manifest.PrepareCommit(receipt);
            if (!TryValidateContinuationPublication(data, true, out failure)) { receipt.failure = failure; return false; }
            using (FrontierTiming.Begin(FrontierTiming.Operation.ManifestPointer))
            if (!SaveManifest(next)) { failure = "Continuation commit pointer publication failed: " + LastFailure; receipt.failure = failure; return false; }
            // note: No validation, providers, callbacks or document replacement occur after the durable pointer; apply only the prepared delta in place.
            _manifest = next;
            receipt.published = true;
            LastTransactionReceipt = receipt;
            data.worldManager.ApplyPublishedContinuation(data.worldSnapshot);
            LastTransactionReceipt = receipt;
            LastFailure = string.Empty;
            SuccessfulSaveCount++;
            LastSavedProfileId = data.profileId;
            // note: Post-pointer projection refresh retains its truthful published-receipt and recovery behavior.
            using (FrontierTiming.Begin(FrontierTiming.Operation.WorkingProjection))
            if (!YQProfileCommitStore.TryRefreshPublishedContinuationProjections(folder,
                    ActivePlayerPath, ActiveWorldPath, receipt, out string projectionFailure))
            {
                // note: Canon and live state are already accepted. Keep that truthful success while surfacing working-copy recovery as a separate warning.
                receipt.failure = "Continuation published; working projections require refresh: " + projectionFailure;
                LastFailure = receipt.failure;
                Debug.LogWarning("[YQProfileSaveSystem] " + LastFailure);
            }
            return true;
        }
        catch (Exception exception)
        {
            if (receipt != null && receipt.published)
            {
                // note: Never classify a durable accepted pointer as rejection. Stop the live streaming owner and require canonical reload after an exceptional in-place apply failure.
                _continuationReloadRequired = true;
                failure = "Continuation committed, but live publication failed; profile reload required: " + exception.Message;
                receipt.failure = failure;
                if (YQPlayerFollowingSemanticChunkStreamer.Active != null)
                    YQPlayerFollowingSemanticChunkStreamer.Active.enabled = false;
                throw new InvalidOperationException(failure, exception);
            }
            failure = "Continuation publication failed: " + exception.Message;
            if (receipt != null) receipt.failure = failure;
            return false;
        }
        finally
        {
            _profileTransactionInProgress = false;
            if (!string.IsNullOrEmpty(failure))
            {
                LastFailure = failure;
                if (receipt != null) LastTransactionReceipt = receipt;
            }
        }
    }

    private bool TryRequireContinuationOwner(out PlayerStateManager psm, out WorldStateManager wsm,
        out YQProfileCommitRecord previous, out string failure)
    {
        // note: Measure this existing owner without changing its callbacks or rejection gates.
        using var publicationTiming = FrontierTiming.Begin(FrontierTiming.Operation.ProfileDiskProof);
        psm = PlayerStateManager.Instance; wsm = WorldStateManager.Instance; previous = null;
        failure = "Continuation publication requires the active canonical paired profile owners and complete commit.";
        if (!ReferenceEquals(Instance, this) || !isActiveAndEnabled || _continuationReloadRequired || YQDeveloperConsoleGate.BlocksPersistence || _manifestUnsupported ||
            _manifest == null || _manifest.schemaVersion != YQStateContract.CurrentProfileManifestSchemaVersion ||
            psm == null || !psm.isActiveAndEnabled || psm.state == null || wsm == null || !wsm.isActiveAndEnabled || wsm.State == null ||
            psm.state.schemaVersion != YQStateContract.CurrentStateSchemaVersion ||
            string.IsNullOrWhiteSpace(_manifest.activeProfileId) || string.IsNullOrWhiteSpace(_manifest.activeCommitId) ||
            _manifest.activeRevision <= 0 || _manifest.activeRevision == int.MaxValue || FindProfile(_manifest.activeProfileId) == null ||
            !string.Equals(psm.state.playerId, _manifest.activeProfileId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(wsm.State.worldIdentity?.ownerProfileId, _manifest.activeProfileId, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(wsm.State.worldIdentity?.worldId)) return false;
        previous = FindLatestCommit(_manifest.activeProfileId);
        if (previous == null || previous.revision != _manifest.activeRevision ||
            !string.Equals(previous.commitId, _manifest.activeCommitId, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(previous.playerChecksum) || string.IsNullOrWhiteSpace(previous.worldChecksum) ||
            !YQProfileCommitStore.TryReadCommit(GetProfileFolder(_manifest.activeProfileId), previous.commitId,
                previous.playerChecksum, previous.worldChecksum, out _, out _, out failure)) return false;
        // note: A stale in-memory pointer cannot overwrite a newer on-disk manifest, even when its old revision files still exist.
        if (!HasSameDurableContinuationManifest())
        { failure = "The durable profile manifest differs from the active in-memory pointer."; return false; }
        if (previous.auxiliaryDocuments == null) { failure = "Active complete commit is missing its auxiliary document inventory."; return false; }
        foreach (YQProfileAuxiliaryDocumentRecord document in previous.auxiliaryDocuments)
        {
            if (document == null || string.IsNullOrWhiteSpace(document.checksum) ||
                !YQProfileCommitStore.TryReadAuxiliaryDocument(GetProfileFolder(_manifest.activeProfileId), previous.commitId,
                    document.documentId, document.checksum, out _, out failure)) return false;
        }
        failure = string.Empty;
        return true;
    }

    private bool HasSameDurableContinuationManifest()
    {
        if (!File.Exists(ManifestPath)) return false;
        // note: Finish the unchanged durable parse/serialization callbacks before capturing the current live pointer; compare its complete output without allocating a second manifest-sized string.
        string durableJson = JsonConvert.SerializeObject(
            JsonConvert.DeserializeObject<ProfileManifest>(File.ReadAllText(ManifestPath)), ContinuationJsonSettings);
        return YQProfileCommitStore.JsonEqualsCaptured(_manifest, durableJson, ContinuationJsonSettings);
    }

    private bool TryCaptureContinuationAuxiliary(out Dictionary<string, Func<string>> providers,
        out Dictionary<string, string> loaded, out Dictionary<string, string> documents, out string failure)
    {
        // note: Measure this existing owner without changing its callbacks or rejection gates.
        using var publicationTiming = FrontierTiming.Begin(FrontierTiming.Operation.AuxiliaryCapture);
        providers = new Dictionary<string, Func<string>>(_auxiliaryProviders, StringComparer.OrdinalIgnoreCase);
        loaded = new Dictionary<string, string>(_loadedAuxiliaryDocuments, StringComparer.OrdinalIgnoreCase);
        documents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        failure = string.Empty;
        // note: An absent adapter retains the latest committed document; the load-time cache can precede intervening ordinary saves.
        YQProfileCommitRecord previous = FindLatestCommit(_manifest.activeProfileId);
        foreach (YQProfileAuxiliaryDocumentRecord document in previous.auxiliaryDocuments)
        {
            if (!YQProfileCommitStore.TryReadAuxiliaryDocument(GetProfileFolder(_manifest.activeProfileId), previous.commitId,
                    document.documentId, document.checksum, out string path, out failure)) return false;
            documents[document.documentId] = File.ReadAllText(path);
        }
        // note: Active adapters provide the current complete snapshot; provider callbacks are followed by exact document and ownership rechecks.
        foreach (var provider in providers)
        {
            string snapshot = provider.Value?.Invoke();
            if (snapshot == null) { failure = "Auxiliary snapshot returned null for " + provider.Key + "."; return false; }
            documents[provider.Key] = snapshot;
        }
        return true;
    }

    private bool TryValidateContinuationPublication(ContinuationPublicationData data, bool recaptureAuxiliary, out string failure)
    {
        failure = "Continuation publication context changed after preparation.";
        if (!ReferenceEquals(data.owner, this) || !TryRequireContinuationOwner(out var psm, out var wsm, out _, out failure) ||
            !ReferenceEquals(psm, data.playerManager) || !ReferenceEquals(wsm, data.worldManager) || !ReferenceEquals(psm.state, data.player) ||
            !ReferenceEquals(_manifest, data.manifest) || !YQServiceLifecycle.IsCurrent(data.epoch) ||
            !YQProfileCommitStore.JsonEqualsCaptured(_manifest, data.manifestJson, ContinuationJsonSettings) ||
            !YQProfileCommitStore.JsonEqualsCaptured(psm.state, data.playerSourceJson, ContinuationJsonSettings) ||
            !wsm.TryValidateContinuationSnapshot(data.worldSnapshot, out failure) ||
            !data.construction.TryValidateForPublication(wsm.State, out var accepted, out failure) ||
            !YQProfileCommitStore.JsonEqualsCaptured(accepted, data.acceptedJson, ContinuationJsonSettings))
        { if (string.IsNullOrEmpty(failure)) failure = "Continuation publication context changed after preparation."; return false; }
        if (_auxiliaryProviders.Count != data.providers.Count || !SameAuxiliaryDocuments(_loadedAuxiliaryDocuments, data.loadedDocuments))
        { failure = "Auxiliary provider ownership or loaded documents changed after preparation."; return false; }
        foreach (var provider in data.providers)
            if (!_auxiliaryProviders.TryGetValue(provider.Key, out var current) || !ReferenceEquals(current, provider.Value))
            { failure = "Auxiliary provider changed for " + provider.Key + "."; return false; }
        if (recaptureAuxiliary)
        {
            if (!TryCaptureContinuationAuxiliary(out _, out _, out var currentDocuments, out failure) ||
                !SameAuxiliaryDocuments(currentDocuments, data.auxiliaryDocuments))
            { if (string.IsNullOrEmpty(failure)) failure = "Auxiliary document changed after preparation."; return false; }
            // note: Providers are external callbacks and may mutate either canonical document or registry; recheck without invoking them again.
            return TryValidateContinuationPublication(data, false, out failure);
        }
        failure = string.Empty;
        return true;
    }

    private static bool SameAuxiliaryDocuments(Dictionary<string, string> left, Dictionary<string, string> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var document in right)
            if (!left.TryGetValue(document.Key, out string value) || !string.Equals(value, document.Value, StringComparison.Ordinal)) return false;
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // note: This dev-only fixture creates one fixed accepted-origin profile so beta reset and reload procedures are reproducible.
    public bool EnsureCanonicalDevelopmentProfile(bool resetExisting)
    {
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return false; }
        // note: Fixture reset also writes profile projections and must respect an active rollback session.
        if (YQDeveloperConsoleGate.BlocksPersistence) { LastFailure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        ProfileEntry existing = FindProfile(YQBetaDevelopmentFixture.CanonicalProfileId);
        if (resetExisting && existing != null)
        {
            // note: Only the explicitly named canonical fixture is removed; ordinary player profiles remain untouched.
            string fixtureFolder = GetProfileFolder(YQBetaDevelopmentFixture.CanonicalProfileId);
            if (Directory.Exists(fixtureFolder))
                Directory.Delete(fixtureFolder, true);
            _manifest.profiles.Remove(existing);
            // note: A clean fixture reset must retire commit pointers for the deleted folder, otherwise the next title boot rejects fresh raw fixture documents as a missing revision store.
            _manifest.commits.RemoveAll(commit => commit != null &&
                string.Equals(commit.profileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase));
            if (string.Equals(_manifest.activeProfileId, YQBetaDevelopmentFixture.CanonicalProfileId, StringComparison.OrdinalIgnoreCase))
            {
                _manifest.activeProfileId = string.Empty;
                _manifest.activeCommitId = string.Empty;
                _manifest.activeRevision = 0;
            }
            SaveManifest();
            existing = null;
        }

        if (existing != null && !resetExisting && Directory.Exists(GetProfileFolder(YQBetaDevelopmentFixture.CanonicalProfileId)))
        {
            // note: An existing fixture must reload its accepted revision without rewriting raw documents or discarding its generated world plan.
            return LoadProfile(YQBetaDevelopmentFixture.CanonicalProfileId);
        }

        if (existing == null)
        {
            // note: The fixture uses the same persisted schema and accepted-origin fields as a normal completed origin flow.
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            existing = new ProfileEntry
            {
                profileId = YQBetaDevelopmentFixture.CanonicalProfileId,
                displayName = YQBetaDevelopmentFixture.CanonicalDisplayName,
                createdUnix = now,
                updatedUnix = now
            };
            _manifest.profiles.Add(existing);
        }

        string folder = EnsureProfileFolder(YQBetaDevelopmentFixture.CanonicalProfileId);
        PlayerState player = new PlayerState
        {
            playerId = YQBetaDevelopmentFixture.CanonicalProfileId,
            displayName = YQBetaDevelopmentFixture.CanonicalDisplayName,
            originQuestionnaireMode = "beta_fixture",
            originQuestionnaireAnswers = new List<string>(YQBetaDevelopmentFixture.CanonicalAnswers),
            generatedOrigin = new GeneratedOriginRecord
            {
                source = "beta_fixture",
                seed = YQBetaDevelopmentFixture.CanonicalOriginSeed,
                mode = "beta_fixture",
                directionKey = YQBetaDevelopmentFixture.CanonicalDirectionKey,
                stimulus = "repeatable beta baseline",
                className = "wayfinder",
                titleName = "Baseline Walker",
                abilityName = "Measured Step",
                abilityKind = "skill",
                questName = YQBetaDevelopmentFixture.CanonicalOriginQuestName,
                tags = new[] { "beta_fixture", "origin_generated" },
                rawJson = "{\"fixture\":\"beta-dev-canonical\"}",
                generatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            }
        };
        player.EnsureCollections();
        player.behaviorCounters[GeneratedRpgContentService.OriginCompletionCounter] = 1f;
        player.behaviorCounters["origin:equipment_manifested"] = 1f;
        player.generatedOrigin.generatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        player.Touch();

        WorldState world = WorldState.CreateDefault();
        world.worldName = YQBetaDevelopmentFixture.CanonicalWorldName;
        world.worldIdentity.ownerProfileId = YQBetaDevelopmentFixture.CanonicalProfileId;
        // note: The named fixture also gets an explicit profile-owned world ID before normalization can derive a shared default identity.
        world.worldIdentity.worldId = YQStateContract.StableId(
            YQStableEntityKind.World,
            YQBetaDevelopmentFixture.CanonicalProfileId + "|world",
            0);
        world.generatedWorldPlan = new GeneratedWorldPlanRecord();
        world.TouchNow();

        // note: Write the profile-owned copies first so LoadProfile can never fall back to a different active character.
        SavePlayerTo(Path.Combine(folder, PlayerFileName), player);
        SaveWorldTo(Path.Combine(folder, WorldFileName), world);
        existing.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveManifest();
        return LoadProfile(YQBetaDevelopmentFixture.CanonicalProfileId);
    }
#endif

    private sealed class PreparedProfileLoad
    {
        internal byte[] playerBytes, worldBytes;
        internal YQProfileCommitRecord verifiedCommit;
        internal PlayerState player;
        internal WorldState world;
        internal readonly Dictionary<string, string> auxiliary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        internal readonly List<string> warnings = new List<string>();
    }

    private static string DecodeDocument(byte[] bytes)
    {
        // note: Decode and validate the same captured bytes that the main-thread owner will project; a second file read cannot substitute different contents after checksum admission.
        using (var memory = new MemoryStream(bytes, false))
        using (var reader = new StreamReader(memory, System.Text.Encoding.UTF8, true)) return reader.ReadToEnd();
    }

    private static PreparedProfileLoad PrepareProfileLoad(ProfileManifest manifest, string folder, string profileId)
    {
        bool committed = TryResolveLatestCommit(manifest, folder, profileId, out string playerPath, out string worldPath, out var commit, out string failure);
        if (!committed && manifest.commits.Exists(item => item != null && string.Equals(item.profileId, profileId, StringComparison.OrdinalIgnoreCase)))
            throw new IOException(failure);
        if (!committed && !TryResolveProfileDocuments(folder, profileId, out playerPath, out worldPath, out failure))
            throw new IOException(failure);
        var result = new PreparedProfileLoad { playerBytes = File.ReadAllBytes(playerPath), worldBytes = File.ReadAllBytes(worldPath),
            verifiedCommit = committed ? commit : null };
        string playerJson = DecodeDocument(result.playerBytes), worldJson = DecodeDocument(result.worldBytes);
        if (committed && ((!string.IsNullOrWhiteSpace(commit.playerChecksum) && YQStateContract.Sha256Hex(playerJson) != commit.playerChecksum) ||
            (!string.IsNullOrWhiteSpace(commit.worldChecksum) && YQStateContract.Sha256Hex(worldJson) != commit.worldChecksum)))
            throw new IOException("The selected paired revision changed during loading.");
        if (!PlayerStateManager.TryPrepareLoadedDocument(playerJson, out result.player, out failure) ||
            !WorldStateManager.TryPrepareLoadedDocument(worldJson, out result.world, out failure)) throw new IOException(failure);
        if (!string.Equals(result.player.playerId, profileId, StringComparison.OrdinalIgnoreCase) || result.world.worldIdentity == null ||
            (!string.IsNullOrWhiteSpace(result.world.worldIdentity.ownerProfileId) && !string.Equals(result.world.worldIdentity.ownerProfileId, profileId, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("The selected paired documents belong to another profile.");
        if (string.IsNullOrWhiteSpace(result.world.worldIdentity.ownerProfileId)) result.world.worldIdentity.ownerProfileId = profileId;
        if (committed && commit.auxiliaryDocuments != null)
            foreach (var document in commit.auxiliaryDocuments)
            {
                if (document == null || string.IsNullOrWhiteSpace(document.documentId)) continue;
                if (!YQProfileCommitStore.TryReadAuxiliaryDocument(folder, commit.commitId, document.documentId, document.checksum, out string path, out failure))
                { result.warnings.Add(document.documentId + ": " + failure); continue; }
                try
                {
                    string json = File.ReadAllText(path);
                    if (!string.IsNullOrWhiteSpace(document.checksum) && YQStateContract.Sha256Hex(json) != document.checksum)
                        throw new IOException("Auxiliary document changed during loading.");
                    result.auxiliary[document.documentId] = json;
                }
                catch (Exception exception) { result.warnings.Add(document.documentId + ": " + exception.Message); }
            }
        return result;
    }

    public async Task<bool> LoadProfileAsync(string profileId)
    {
        // note: The existing profile transaction exclusively owns selection while its worker reads and normalizes detached documents. All service teardown and singleton replacement remain on Unity's thread.
        if (_profileTransactionInProgress || YQDeveloperConsoleGate.BlocksPersistence)
        { LastFailure = _profileTransactionInProgress ? "A paired profile transaction is already in progress." : YQDeveloperConsoleGate.SaveBlocked; return false; }
        var entry = FindProfile(profileId);
        var player = PlayerStateManager.Instance;
        var world = WorldStateManager.Instance;
        if (entry == null || player == null || world == null) { LastFailure = "Selected profile or state owners are unavailable."; return false; }
        _profileTransactionInProgress = true;
        int epoch = YQServiceLifecycle.RequestEpoch;
        var manifest = _manifest;
        try
        {
            string folder = GetProfileFolder(profileId);
            var snapshot = JsonConvert.DeserializeObject<ProfileManifest>(JsonConvert.SerializeObject(manifest));
            var prepared = await Task.Run(() => PrepareProfileLoad(snapshot, folder, profileId));
            if (this == null || Instance != this || !YQServiceLifecycle.IsCurrent(epoch) || !ReferenceEquals(_manifest, manifest) ||
                !ReferenceEquals(FindProfile(profileId), entry) || PlayerStateManager.Instance != player || WorldStateManager.Instance != world ||
                YQDeveloperConsoleGate.BlocksPersistence)
            { LastFailure = "Profile selection changed while its documents were prepared."; return false; }
            if (!TryPreparePlayerForProfileTransition(player, out string failure)) { LastFailure = failure; return false; }
            YQGeneratedWorldRuntimeBuilder.MeasureStartupOperation("Profile.ProjectDocuments", () => {
                File.WriteAllBytes(ActivePlayerPath, prepared.playerBytes); File.WriteAllBytes(ActiveWorldPath, prepared.worldBytes);
                CopyOrRemoveProfileBackup(Path.Combine(folder, PlayerFileName + BackupSuffix), ActivePlayerPath + BackupSuffix);
                CopyOrRemoveProfileBackup(Path.Combine(folder, WorldFileName + BackupSuffix), ActiveWorldPath + BackupSuffix);
                return true;
            });
            YQGeneratedWorldRuntimeBuilder.MeasureStartupOperation("Profile.InstallDocuments", () => {
                YQServiceLifecycle.BeginProfileSession(profileId);
                // note: Teardown callbacks cannot replace the canonical owners between the preparation guard and paired installation.
                if (PlayerStateManager.Instance != player || WorldStateManager.Instance != world || YQDeveloperConsoleGate.BlocksPersistence)
                    throw new InvalidOperationException("Profile owners changed during service teardown; reload is required.");
                player.InstallPreparedProfileDocument(prepared.player); world.InstallPreparedProfileDocument(prepared.world);
                return true;
            });
            _loadedAuxiliaryDocuments.Clear();
            foreach (var document in prepared.auxiliary) _loadedAuxiliaryDocuments[document.Key] = document.Value;
            foreach (string warning in prepared.warnings) Debug.LogWarning("[YQProfileSaveSystem] Optional profile document rejected: " + warning);
            _manifest.SelectLoadedProfile(profileId, prepared.verifiedCommit);
            entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (!SaveManifest()) return false;
            SuccessfulLoadCount++; LastLoadedProfileId = profileId; _continuationReloadRequired = false; LastFailure = string.Empty;
            TeleportPlayerToSavedPosition();
            return true;
        }
        catch (Exception exception)
        {
            LastFailure = exception.Message;
            if (this != null) Debug.LogError("[YQProfileSaveSystem] PROFILE LOAD REJECTED: " + profileId + ": " + LastFailure);
            return false;
        }
        finally { _profileTransactionInProgress = false; }
    }

    public bool LoadProfile(string profileId)
    {
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return false; }
        // note: Refuse owner replacement until the test session has been deliberately resolved.
        if (YQDeveloperConsoleGate.BlocksPersistence) { LastFailure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null)
            return false;

        string folder = GetProfileFolder(profileId);
        _loadedAuxiliaryDocuments.Clear();
        // note: Validate both documents before overwriting shared saves or replacing either live state manager.
        if (PlayerStateManager.Instance == null || WorldStateManager.Instance == null)
            return false;
        bool hasRevision = TryResolveLatestCommit(folder, profileId, out string playerSrc, out string worldSrc, out YQProfileCommitRecord loadedCommit, out string failure);
        if (!hasRevision && _manifest.commits.Exists(commit => commit != null && string.Equals(commit.profileId, profileId, StringComparison.OrdinalIgnoreCase)))
        {
            Debug.LogError("[YQProfileSaveSystem] PROFILE LOAD REJECTED: " + profileId + ": " + failure);
            return false;
        }
        if (!hasRevision && !TryResolveProfileDocuments(folder, profileId, out playerSrc, out worldSrc, out failure))
        {
            Debug.LogError("[YQProfileSaveSystem] PROFILE LOAD REJECTED: " + profileId + ": " + failure);
            return false;
        }

        // note: Drain the old player's automatic projection writer before replacing either profile document.
        if (!TryPreparePlayerForProfileTransition(PlayerStateManager.Instance, out failure))
        {
            Debug.LogError("[YQProfileSaveSystem] PROFILE LOAD REJECTED: pending player autosave: " + failure);
            return false;
        }
        File.Copy(playerSrc, ActivePlayerPath, true);
        File.Copy(worldSrc, ActiveWorldPath, true);
        // note: Recovery files are profile-owned state. Leaving the previous profile's shared backup in place could silently load the wrong character/world when this profile's primary file is damaged.
        CopyOrRemoveProfileBackup(
            Path.Combine(folder, PlayerFileName + BackupSuffix),
            ActivePlayerPath + BackupSuffix);
        CopyOrRemoveProfileBackup(
            Path.Combine(folder, WorldFileName + BackupSuffix),
            ActiveWorldPath + BackupSuffix);

        // note: Invalidate in-flight requests and registered service teardown before replacing the canonical session.
        YQServiceLifecycle.BeginProfileSession(profileId);
        PlayerStateManager.Instance?.LoadOrCreate();
        WorldStateManager.Instance?.LoadOrCreate();

        PlayerState loadedPlayer = PlayerStateManager.Instance?.state;
        if (loadedPlayer == null ||
            !string.Equals(
                loadedPlayer.playerId,
                profileId,
                StringComparison.OrdinalIgnoreCase))
        {
            // note: A corrupt profile may fail closed, but it must never be accepted through another profile's stale recovery document.
            Debug.LogError(
                "[YQProfileSaveSystem] PROFILE LOAD REJECTED\n" +
                "Expected profile: " + profileId + "\n" +
                "Loaded player: " +
                (loadedPlayer != null ? loadedPlayer.playerId : "<null>"));
            return false;
        }
        WorldState loadedWorld = WorldStateManager.Instance?.State;
        if (loadedWorld == null || loadedWorld.worldIdentity == null ||
            (!string.IsNullOrWhiteSpace(loadedWorld.worldIdentity.ownerProfileId) &&
             !string.Equals(loadedWorld.worldIdentity.ownerProfileId, profileId, StringComparison.OrdinalIgnoreCase)))
        {
            Debug.LogError("[YQProfileSaveSystem] PROFILE LOAD REJECTED: world document belongs to another profile.");
            return false;
        }
        if (string.IsNullOrWhiteSpace(loadedWorld.worldIdentity.ownerProfileId))
            loadedWorld.worldIdentity.ownerProfileId = profileId;

        // note: Load only auxiliary state tied to the exact paired revision that supplied this character and world.
        if (hasRevision)
            LoadAuxiliaryDocuments(folder, loadedCommit);

        _manifest.SelectLoadedProfile(profileId, hasRevision ? loadedCommit : null);
        entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveManifest();

        // note: Count only a load that passed identity validation and replaced both active documents.
        SuccessfulLoadCount++;
        LastLoadedProfileId = profileId;
        _continuationReloadRequired = false;

        TeleportPlayerToSavedPosition();
        return true;
    }

    private bool TryResolveLatestCommit(string folder, string profileId, out string playerPath, out string worldPath, out YQProfileCommitRecord selectedCommit, out string failure)
        => TryResolveLatestCommit(_manifest, folder, profileId, out playerPath, out worldPath, out selectedCommit, out failure);

    private static bool TryResolveLatestCommit(ProfileManifest manifest, string folder, string profileId, out string playerPath, out string worldPath, out YQProfileCommitRecord selectedCommit, out string failure)
    {
        playerPath = worldPath = null;
        selectedCommit = null;
        failure = "No complete profile revision is available.";
        if (manifest.commits == null) return false;
        List<YQProfileCommitRecord> candidates = new List<YQProfileCommitRecord>();
        for (int index = 0; index < manifest.commits.Count; index++)
        {
            YQProfileCommitRecord commit = manifest.commits[index];
            if (commit != null && string.Equals(commit.profileId, profileId, StringComparison.OrdinalIgnoreCase) && string.Equals(commit.status, "complete", StringComparison.OrdinalIgnoreCase))
                candidates.Add(commit);
        }
        candidates.Sort((left, right) => right.revision.CompareTo(left.revision));
        for (int index = 0; index < candidates.Count; index++)
        {
            if (YQProfileCommitStore.TryReadCommit(folder, candidates[index].commitId, candidates[index].playerChecksum, candidates[index].worldChecksum, out playerPath, out worldPath, out failure))
            {
                selectedCommit = candidates[index];
                return true;
            }
        }
        return false;
    }

    private void LoadAuxiliaryDocuments(string folder, YQProfileCommitRecord commit)
    {
        if (commit == null || commit.auxiliaryDocuments == null)
            return;

        for (int index = 0; index < commit.auxiliaryDocuments.Count; index++)
        {
            YQProfileAuxiliaryDocumentRecord document = commit.auxiliaryDocuments[index];
            if (document == null || string.IsNullOrWhiteSpace(document.documentId))
                continue;

            if (!YQProfileCommitStore.TryReadAuxiliaryDocument(
                    folder,
                    commit.commitId,
                    document.documentId,
                    document.checksum,
                    out string documentPath,
                    out string failure))
            {
                Debug.LogWarning("[YQProfileSaveSystem] Optional profile document rejected: " + document.documentId + ": " + failure);
                continue;
            }

            try
            {
                _loadedAuxiliaryDocuments[document.documentId] = File.ReadAllText(documentPath);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("[YQProfileSaveSystem] Optional profile document could not be read: " + document.documentId + ": " + exception.Message);
            }
        }
    }

    public static bool TryResolveProfileDocuments(string folder, string profileId,
        out string playerPath, out string worldPath, out string failure)
    {
        // note: Recovery remains confined to the selected profile; this preflight never changes files or live state.
        playerPath = worldPath = null;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(profileId))
        {
            failure = "Missing profile location or identity.";
            return false;
        }
        playerPath = ResolveReadableProfileDocument(folder, PlayerFileName, profileId, true);
        worldPath = ResolveReadableProfileDocument(folder, WorldFileName, profileId, false);
        if (playerPath != null && worldPath != null)
            return true;
        failure = playerPath == null ? "Player document and recovery copy are invalid or belong to another profile."
            : "World document and recovery copy are invalid.";
        return false;
    }

    private static string ResolveReadableProfileDocument(string folder, string filename, string profileId, bool player)
    {
        for (int attempt = 0; attempt < 2; attempt++)
        {
            string path = Path.Combine(folder, filename + (attempt == 0 ? string.Empty : BackupSuffix));
            if (!File.Exists(path))
                continue;
            try
            {
                string rawJson = File.ReadAllText(path);
                // note: An empty object is not a saved world; constructor defaults must not erase accepted canon.
                if (player)
                {
                    if (!YQStateMigrations.TryNormalizePlayerDocument(rawJson, out string normalizedJson, out _, out _))
                        continue;
                    JObject document = JObject.Parse(normalizedJson);
                    var settings = new JsonSerializerSettings
                    {
                        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
                    };
                    PlayerState state = document.ToObject<PlayerState>(JsonSerializer.Create(settings));
                    if (state != null && string.Equals(state.playerId, profileId, StringComparison.OrdinalIgnoreCase) &&
                        YQStateMigrations.TryMigrate(state, out _))
                        return path;
                }
                else if (YQStateMigrations.TryNormalizeWorldDocument(rawJson, out string normalizedJson, out _, out _))
                {
                    JObject document = JObject.Parse(normalizedJson);
                    if (document["worldName"]?.Type != JTokenType.String)
                        continue;
                    WorldState world = document.ToObject<WorldState>();
                    if (world != null && (world.worldIdentity == null || string.IsNullOrWhiteSpace(world.worldIdentity.ownerProfileId) ||
                        string.Equals(world.worldIdentity.ownerProfileId, profileId, StringComparison.OrdinalIgnoreCase)) &&
                        YQStateMigrations.TryMigrate(world, out _))
                        return path;
                }
            }
            catch (Exception exception) when (exception is JsonException || exception is IOException || exception is UnauthorizedAccessException)
            {
                // note: Try only this profile's one recovery copy; callers receive a bounded failure instead of a default world.
            }
        }
        return null;
    }

    public bool DeleteProfile(string profileId)
    {
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return false; }
        // note: Never invalidate the profile backing a live development snapshot.
        if (YQDeveloperConsoleGate.BlocksPersistence) { LastFailure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null)
            return false;

        string folder = GetProfileFolder(profileId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        return CompleteProfileDeletion(entry, profileId);
    }

    public async Task<bool> DeleteProfileAsync(string profileId)
    {
        // note: Hold the existing transaction gate while only filesystem removal runs off-thread; manifest and Unity state remain on the calling thread.
        if (_profileTransactionInProgress) { LastFailure = "A paired profile transaction is already in progress."; return false; }
        if (YQDeveloperConsoleGate.BlocksPersistence) { LastFailure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null) return false;
        string folder = GetProfileFolder(profileId);
        _profileTransactionInProgress = true;
        try
        {
            await Task.Run(() =>
            {
                // note: Capture the selected folder before dispatch; the worker never reads live profile state or Unity objects.
                if (Directory.Exists(folder)) Directory.Delete(folder, true);
            });
            if (this == null) return false;
            return CompleteProfileDeletion(entry, profileId);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            LastFailure = exception.Message;
            Debug.LogError("[YQProfileSaveSystem] Profile deletion failed: " + LastFailure);
            return false;
        }
        finally { _profileTransactionInProgress = false; }
    }

    private bool CompleteProfileDeletion(ProfileEntry entry, string profileId)
    {
        // note: Keep manifest publication shared with the synchronous compatibility API after the folder operation completes.
        _manifest.profiles.Remove(entry);
        if (string.Equals(_manifest.activeProfileId, profileId, StringComparison.OrdinalIgnoreCase))
            _manifest.activeProfileId = _manifest.profiles.Count > 0 ? _manifest.profiles[0].profileId : string.Empty;

        return SaveManifest();
    }

    public ProfileEntry FindProfile(string profileId)
    {
        if (_manifest.profiles == null)
            return null;
        for (int i = 0; i < _manifest.profiles.Count; i++)
        {
            ProfileEntry entry = _manifest.profiles[i];
            if (entry != null && string.Equals(entry.profileId, profileId, StringComparison.OrdinalIgnoreCase))
                return entry;
        }
        return null;
    }

    private YQProfileCommitRecord FindLatestCommit(string profileId)
    {
        YQProfileCommitRecord latest = null;
        if (_manifest.commits == null) return null;
        for (int index = 0; index < _manifest.commits.Count; index++)
        {
            YQProfileCommitRecord commit = _manifest.commits[index];
            if (commit == null || !string.Equals(commit.profileId, profileId, StringComparison.OrdinalIgnoreCase) || !string.Equals(commit.status, "complete", StringComparison.OrdinalIgnoreCase))
                continue;
            if (latest == null || commit.revision > latest.revision)
                latest = commit;
        }
        return latest;
    }

    private bool FailTransaction(string profileId, string failure)
    {
        LastFailure = string.IsNullOrWhiteSpace(failure) ? "Profile transaction failed." : failure;
        LastTransactionReceipt = new YQProfileTransactionReceipt
        {
            profileId = profileId ?? string.Empty,
            published = false,
            failure = LastFailure,
            committedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        Debug.LogError("[YQProfileSaveSystem] " + LastFailure);
        return false;
    }

    private void LoadManifest()
    {
        _manifestUnsupported = false;
        string[] candidates = { ManifestPath, ManifestPath + BackupSuffix };
        for (int index = 0; index < candidates.Length; index++)
        {
            if (!File.Exists(candidates[index])) continue;
            try
            {
                ProfileManifest loaded = JsonUtility.FromJson<ProfileManifest>(File.ReadAllText(candidates[index]));
                if (loaded == null) continue;
                if (loaded.schemaVersion > YQStateContract.CurrentProfileManifestSchemaVersion)
                {
                    _manifestUnsupported = true;
                    _manifest = new ProfileManifest();
                    Debug.LogError("[YQProfileSaveSystem] Unsupported future profile manifest schema " + loaded.schemaVersion + ".");
                    return;
                }
                _manifest = loaded;
                break;
            }
            catch
            {
                // note: Try the manifest's own recovery copy before exposing an empty profile list.
            }
        }

        if (_manifest.profiles == null)
            _manifest.profiles = new List<ProfileEntry>();
    }

    private bool SaveManifest(ProfileManifest candidate = null)
    {
        try
        {
            Directory.CreateDirectory(RootDir);
            string temporaryPath = ManifestPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(candidate ?? _manifest, true));
            if (File.Exists(ManifestPath))
                File.Replace(temporaryPath, ManifestPath, ManifestPath + BackupSuffix, true);
            else
                File.Move(temporaryPath, ManifestPath);
            return true;
        }
        catch (Exception exception)
        {
            LastFailure = exception.Message;
            Debug.LogError("[YQProfileSaveSystem] Manifest commit failed: " + exception.Message);
            return false;
        }
    }

    private void RecoverNewerActiveSnapshot()
    {
        // note: Recover through the paired revision owner; copying compatibility files alone leaves Continue pointing at an older immutable commit.
        string profileId = _manifest.activeProfileId;
        if (_manifestUnsupported || FindProfile(profileId) == null || YQDeveloperConsoleGate.BlocksPersistence) return;
        string folder = GetProfileFolder(profileId);
        try
        {
            bool committed = TryResolveLatestCommit(folder, profileId, out string committedPlayerPath, out string committedWorldPath,
                out YQProfileCommitRecord previous, out _);
            PlayerState baselinePlayer = null;
            WorldState baselineWorld = null;
            if (committed && !TryReadRecoveryPair(committedPlayerPath, committedWorldPath, profileId,
                out baselinePlayer, out baselineWorld, out _, out _)) return;
            PlayerState selectedPlayer = baselinePlayer;
            WorldState selectedWorld = baselineWorld;
            string selectedPlayerJson = null, selectedWorldJson = null;
            foreach (string pairRoot in new[] { folder, Application.persistentDataPath })
            {
                if (!TryReadRecoveryPair(Path.Combine(pairRoot, PlayerFileName), Path.Combine(pairRoot, WorldFileName), profileId,
                    out PlayerState candidatePlayer, out WorldState candidateWorld, out string playerJson, out string worldJson) ||
                    !HasSameAcceptedContinuationForRecovery(candidateWorld, baselineWorld) ||
                    !IsNewerRecoveryPair(candidatePlayer, candidateWorld, selectedPlayer, selectedWorld)) continue;
                selectedPlayer = candidatePlayer; selectedWorld = candidateWorld;
                selectedPlayerJson = playerJson; selectedWorldJson = worldJson;
            }
            if (selectedPlayerJson == null) return;
            // note: Preserve the exact auxiliary revision (terrain snapshot, dialogue, etc.) when recovering this same world's newer canonical documents.
            var auxiliary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (previous?.auxiliaryDocuments != null)
                foreach (var document in previous.auxiliaryDocuments)
                {
                    if (!YQProfileCommitStore.TryReadAuxiliaryDocument(folder, previous.commitId, document.documentId, document.checksum,
                        out string path, out string failure)) throw new IOException("Recovery auxiliary document rejected: " + failure);
                    auxiliary.Add(document.documentId, File.ReadAllText(path));
                }
            int revision = previous != null ? previous.revision + 1 : 1;
            if (!YQProfileCommitStore.TryCommit(folder, profileId, ActivePlayerPath, ActiveWorldPath, revision,
                selectedPlayerJson, selectedWorldJson, auxiliary, null, out YQProfileTransactionReceipt receipt))
                throw new IOException("Recovery paired commit failed: " + receipt?.failure);
            receipt.previousCommitId = previous?.commitId ?? string.Empty;
            ProfileManifest next = _manifest.PrepareCommit(receipt);
            if (!SaveManifest(next)) throw new IOException("Recovery pointer publication failed: " + LastFailure);
            _manifest = next;
            LastTransactionReceipt = receipt;
            Debug.Log("[YQProfileSaveSystem] RECOVERED NEWER PAIRED PROFILE REVISION " + profileId +
                " player=" + selectedPlayer.stateRevision + " world=" + selectedWorld.stateRevision + " revision=" + revision);
        }
        catch (Exception exception)
        {
            // note: Keep the previous complete revision and recovered source documents available if validation or publication fails.
            Debug.LogWarning("[YQProfileSaveSystem] Paired profile recovery skipped: " + exception.Message);
        }
    }

    private static bool TryReadRecoveryPair(string playerPath, string worldPath, string profileId,
        out PlayerState player, out WorldState world, out string playerJson, out string worldJson)
    {
        // note: Detached parsing and migration validate both documents before a recovery can write any projection or pointer.
        player = null; world = null; playerJson = null; worldJson = null;
        if (!File.Exists(playerPath) || !File.Exists(worldPath)) return false;
        if (!YQStateMigrations.TryNormalizePlayerDocument(File.ReadAllText(playerPath), out string normalizedPlayer, out _, out _) ||
            !YQStateMigrations.TryNormalizeWorldDocument(File.ReadAllText(worldPath), out string normalizedWorld, out _, out _)) return false;
        var settings = new JsonSerializerSettings { Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
        player = JsonConvert.DeserializeObject<PlayerState>(normalizedPlayer, settings);
        world = JsonConvert.DeserializeObject<WorldState>(normalizedWorld, settings);
        if (player == null || world == null || !string.Equals(player.playerId, profileId, StringComparison.OrdinalIgnoreCase) ||
            !YQStateMigrations.TryMigrate(player, out _) || !YQStateMigrations.TryMigrate(world, out _)) return false;
        if (world.worldIdentity == null || !string.Equals(world.worldIdentity.ownerProfileId, profileId, StringComparison.OrdinalIgnoreCase) ||
            !YQStateReferenceValidator.Validate(player, world).IsValid) return false;
        playerJson = JsonConvert.SerializeObject(player, settings);
        worldJson = JsonConvert.SerializeObject(world, settings);
        return true;
    }

    private static bool HasSameAcceptedContinuationForRecovery(WorldState candidate, WorldState committed)
    {
        // note: A new continuation requires its manifest-listed paired revision. Legacy leaked projections from a rejected transaction cannot acquire acceptance through autosave recovery.
        var candidateContinuation = candidate?.generatedWorldPlan?.spatialPlanV2?.acceptedContinuation;
        var committedContinuation = committed?.generatedWorldPlan?.spatialPlanV2?.acceptedContinuation;
        if (candidateContinuation == null || committedContinuation == null)
            return candidateContinuation == null && committedContinuation == null;
        return string.Equals(JsonConvert.SerializeObject(candidateContinuation, ContinuationJsonSettings),
            JsonConvert.SerializeObject(committedContinuation, ContinuationJsonSettings), StringComparison.Ordinal);
    }

    private static bool IsNewerRecoveryPair(PlayerState candidatePlayer, WorldState candidateWorld, PlayerState baselinePlayer, WorldState baselineWorld)
    {
        // note: Wall-clock autosaves from an older loaded revision cannot roll back newer accepted canon; both state revisions must advance monotonically.
        if (candidatePlayer == null || candidateWorld == null) return false;
        if (baselinePlayer == null || baselineWorld == null) return true;
        if (!string.Equals(candidateWorld.worldIdentity?.worldId, baselineWorld.worldIdentity?.worldId, StringComparison.Ordinal) ||
            candidatePlayer.stateRevision < baselinePlayer.stateRevision || candidateWorld.stateRevision < baselineWorld.stateRevision) return false;
        if (candidatePlayer.stateRevision > baselinePlayer.stateRevision || candidateWorld.stateRevision > baselineWorld.stateRevision) return true;
        return candidatePlayer.stateRevision == 0 && candidateWorld.stateRevision == 0 &&
            candidatePlayer.lastUpdatedUnix > baselinePlayer.lastUpdatedUnix && candidateWorld.lastUpdatedUnix >= baselineWorld.lastUpdatedUnix;
    }

    private string EnsureProfileFolder(string profileId)
    {
        string folder = GetProfileFolder(profileId);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private string GetProfileFolder(string profileId)
    {
        return Path.Combine(RootDir, profileId);
    }

    internal static bool TryPreparePlayerForProfileTransition(PlayerStateManager player, out string failure)
    {
        // note: Share the exact replacement barrier with detached regression fixtures without changing singleton/profile ownership.
        failure = string.Empty;
        return player == null || player.TryFlushPendingAutosave(out failure);
    }

    private static void CopyOrRemoveProfileBackup(
        string source,
        string destination)
    {
        if (File.Exists(source))
        {
            File.Copy(source, destination, true);
            return;
        }

        if (File.Exists(destination))
            File.Delete(destination);
    }

    private static void SavePlayerTo(string path, PlayerState player)
    {
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(player, settings));
    }

    private static void SaveWorldTo(string path, WorldState world)
    {
        // note: Use the same explicit Unity-value converters as player snapshots so world identity renderOrigin remains a plain persisted vector.
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
        };
        File.WriteAllText(path, JsonConvert.SerializeObject(world, settings));
    }

    private static void ApplyCharacterCreation(PlayerState player, string pronouns, string bodyFrame, string lifeDirection, string vow, string appearanceSummary)
    {
        if (player == null)
            return;

        player.EnsureCollections();
        player.characterPronouns = Clean(pronouns, 32);
        player.characterBodyFrame = Clean(bodyFrame, 48);
        player.characterLifeDirection = Clean(lifeDirection, 120);
        player.characterVow = Clean(vow, 260);
        player.characterAppearanceSummary = Clean(appearanceSummary, 220);
        player.characterCreationSeed = StableHex(player.playerId + "|" + player.displayName + "|" + player.characterPronouns + "|" + player.characterBodyFrame + "|" + player.characterLifeDirection + "|" + player.characterVow);
        AddKeyword(player, "character_created");
        AddKeyword(player, "new_save_identity");
        AddKeyword(player, NormalizeKey(player.characterLifeDirection));
        player.behaviorCounters["character_creation:complete"] = 1f;
        player.AddLedgerLine("Character creation committed: " + player.displayName + " | " + player.characterLifeDirection + " | " + player.characterVow, 80);
        player.Touch();
    }

    private static string Clean(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        string clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= maxLength ? clean : clean.Substring(0, maxLength).TrimEnd();
    }

    private static void AddKeyword(PlayerState player, string keyword)
    {
        if (player == null || string.IsNullOrWhiteSpace(keyword))
            return;

        player.EnsureCollections();
        string clean = NormalizeKey(keyword);
        if (string.IsNullOrWhiteSpace(clean))
            return;

        for (int i = 0; i < player.identityKeywords.Count; i++)
        {
            if (string.Equals(player.identityKeywords[i], clean, StringComparison.OrdinalIgnoreCase))
                return;
        }
        player.identityKeywords.Add(clean);
    }

    private static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }
        return new string(chars).Trim('_');
    }

    private static string StableHex(string value)
    {
        unchecked
        {
            int hash = 23;
            string text = value ?? string.Empty;
            for (int i = 0; i < text.Length; i++)
                hash = hash * 31 + text[i];
            return (hash & 0x7fffffff).ToString("x8");
        }
    }

    private static void TeleportPlayerToSavedPosition()
    {
        PlayerStateManager psm = PlayerStateManager.Instance;
        if (psm == null || psm.state == null)
            return;

        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
            return;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        player.transform.position = psm.state.lastPosition == Vector3.zero ? new Vector3(0f, 1.25f, -10f) : psm.state.lastPosition;

        if (cc != null)
            cc.enabled = true;
    }

    private void OnApplicationQuit()
    {
        // note: Shutdown commits the active player/world pair through the profile owner before Unity tears down scene services.
        SaveActiveProfile();
        YQServiceLifecycle.ResetAll();
    }

    private void OnDestroy()
    {
        // note: Clear the singleton and invalidate request epochs so late model/service callbacks cannot mutate a new profile.
        if (Instance == this)
        {
            Instance = null;
            YQServiceLifecycle.ResetAll();
        }
    }
}
