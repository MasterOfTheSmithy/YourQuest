// Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

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

    private const string ProfilesFolder = "Profiles";
    private const string ManifestFileName = "profiles_manifest.json";
    private const string PlayerFileName = "player_state.json";
    private const string WorldFileName = "world_state.json";
    private const string BackupSuffix = ".bak";

    private ProfileManifest _manifest = new ProfileManifest();
    private bool _manifestUnsupported;
    private readonly Dictionary<string, Func<string>> _auxiliaryProviders = new Dictionary<string, Func<string>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _loadedAuxiliaryDocuments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

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
        _manifest.commits.RemoveAll(commit => commit != null && string.Equals(commit.commitId, receipt.commitId, StringComparison.OrdinalIgnoreCase));
        _manifest.commits.Add(new YQProfileCommitRecord
        {
            profileId = profileId,
            commitId = receipt.commitId,
            revision = receipt.revision,
            previousCommitId = receipt.previousCommitId,
            playerChecksum = receipt.playerChecksum,
            worldChecksum = receipt.worldChecksum,
            status = "complete",
            committedUnix = receipt.committedUnix,
            auxiliaryDocuments = new List<YQProfileAuxiliaryDocumentRecord>(receipt.auxiliaryDocuments)
        });
        entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _manifest.activeProfileId = profileId;
        _manifest.activeCommitId = receipt.commitId;
        _manifest.activeRevision = receipt.revision;
        if (!SaveManifest())
            return FailTransaction(profileId, "Commit pointer publication failed: " + LastFailure);
        LastFailure = string.Empty;
        receipt.published = true;
        SuccessfulSaveCount++;
        LastSavedProfileId = profileId;
        return true;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    // note: This dev-only fixture creates one fixed accepted-origin profile so beta reset and reload procedures are reproducible.
    public bool EnsureCanonicalDevelopmentProfile(bool resetExisting)
    {
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

    public bool LoadProfile(string profileId)
    {
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

        _manifest.activeProfileId = profileId;
        entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveManifest();

        // note: Count only a load that passed identity validation and replaced both active documents.
        SuccessfulLoadCount++;
        LastLoadedProfileId = profileId;

        TeleportPlayerToSavedPosition();
        return true;
    }

    private bool TryResolveLatestCommit(string folder, string profileId, out string playerPath, out string worldPath, out YQProfileCommitRecord selectedCommit, out string failure)
    {
        playerPath = worldPath = null;
        selectedCommit = null;
        failure = "No complete profile revision is available.";
        if (_manifest.commits == null) return false;
        List<YQProfileCommitRecord> candidates = new List<YQProfileCommitRecord>();
        for (int index = 0; index < _manifest.commits.Count; index++)
        {
            YQProfileCommitRecord commit = _manifest.commits[index];
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
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null)
            return false;

        string folder = GetProfileFolder(profileId);
        if (Directory.Exists(folder))
            Directory.Delete(folder, true);

        _manifest.profiles.Remove(entry);
        if (string.Equals(_manifest.activeProfileId, profileId, StringComparison.OrdinalIgnoreCase))
            _manifest.activeProfileId = _manifest.profiles.Count > 0 ? _manifest.profiles[0].profileId : string.Empty;

        SaveManifest();
        return true;
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

    private bool SaveManifest()
    {
        try
        {
            Directory.CreateDirectory(RootDir);
            string temporaryPath = ManifestPath + ".tmp";
            File.WriteAllText(temporaryPath, JsonUtility.ToJson(_manifest, true));
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
        string profileId = _manifest.activeProfileId;
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null ||
            !File.Exists(ActivePlayerPath) ||
            !File.Exists(ActiveWorldPath))
        {
            return;
        }

        string folder = GetProfileFolder(profileId);
        string profilePlayerPath = Path.Combine(folder, PlayerFileName);
        string profileWorldPath = Path.Combine(folder, WorldFileName);
        if (!File.Exists(profilePlayerPath) || !File.Exists(profileWorldPath))
            return;

        try
        {
            PlayerState activePlayer =
                JsonConvert.DeserializeObject<PlayerState>(
                    File.ReadAllText(ActivePlayerPath));
            PlayerState profilePlayer =
                JsonConvert.DeserializeObject<PlayerState>(
                    File.ReadAllText(profilePlayerPath));

            if (activePlayer == null ||
                !string.Equals(
                    activePlayer.playerId,
                    profileId,
                    StringComparison.OrdinalIgnoreCase) ||
                (profilePlayer != null &&
                 activePlayer.lastUpdatedUnix <= profilePlayer.lastUpdatedUnix))
            {
                return;
            }

            // note: A newer shared save is recoverable only for the same active player id; this repairs an interrupted profile-copy boundary without importing another character or replacing a newer profile snapshot.
            File.Copy(ActivePlayerPath, profilePlayerPath, true);
            File.Copy(ActiveWorldPath, profileWorldPath, true);
            CopyOrRemoveProfileBackup(
                ActivePlayerPath + BackupSuffix,
                profilePlayerPath + BackupSuffix);
            CopyOrRemoveProfileBackup(
                ActiveWorldPath + BackupSuffix,
                profileWorldPath + BackupSuffix);

            entry.updatedUnix = activePlayer.lastUpdatedUnix;
            SaveManifest();
            Debug.Log(
                "[YQProfileSaveSystem] RECOVERED NEWER ACTIVE PROFILE SNAPSHOT " +
                profileId);
        }
        catch (Exception ex)
        {
            // note: Recovery is opportunistic; unreadable shared state must not prevent the title screen from offering the last valid profile snapshot.
            Debug.LogWarning(
                "[YQProfileSaveSystem] Active profile snapshot recovery skipped: " +
                ex.Message);
        }
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
