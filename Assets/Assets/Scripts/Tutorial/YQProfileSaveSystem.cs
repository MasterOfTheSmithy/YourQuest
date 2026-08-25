// Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQProfileSaveSystem : MonoBehaviour
{
    public static YQProfileSaveSystem Instance { get; private set; }

    [Serializable]
    public sealed class ProfileManifest
    {
        public List<ProfileEntry> profiles = new List<ProfileEntry>();
        public string activeProfileId = string.Empty;
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

    private const string ProfilesFolder = "Profiles";
    private const string ManifestFileName = "profiles_manifest.json";
    private const string PlayerFileName = "player_state.json";
    private const string WorldFileName = "world_state.json";
    private const string BackupSuffix = ".bak";

    private ProfileManifest _manifest = new ProfileManifest();

    private string RootDir => Path.Combine(Application.persistentDataPath, ProfilesFolder);
    private string ManifestPath => Path.Combine(RootDir, ManifestFileName);
    private string ActivePlayerPath => Path.Combine(Application.persistentDataPath, PlayerFileName);
    private string ActiveWorldPath => Path.Combine(Application.persistentDataPath, WorldFileName);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureBootstrap()
    {
        if (FindFirstObjectByType<YQProfileSaveSystem>() != null)
            return;

        GameObject go = new GameObject("YQProfileSaveSystem");
        DontDestroyOnLoad(go);
        go.AddComponent<YQProfileSaveSystem>();
    }

    private void Awake()
    {
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
    }

    public string CreateNewProfile(string displayName)
    {
        return CreateNewProfile(displayName, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    public string CreateNewProfile(string displayName, string pronouns, string bodyFrame, string lifeDirection, string vow, string appearanceSummary)
    {
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
        _manifest.activeProfileId = profileId;
        EnsureProfileFolder(profileId);

        PlayerState player = new PlayerState();
        player.displayName = trimmedName;
        player.playerId = profileId;
        player.EnsureCollections();
        ApplyCharacterCreation(player, pronouns, bodyFrame, lifeDirection, vow, appearanceSummary);
        SavePlayerTo(Path.Combine(GetProfileFolder(profileId), PlayerFileName), player);

        WorldState world = WorldState.CreateDefault();
        SaveWorldTo(Path.Combine(GetProfileFolder(profileId), WorldFileName), world);

        SaveManifest();
        LoadProfile(profileId);
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

    public bool SaveActiveProfile()
    {
        if (string.IsNullOrWhiteSpace(_manifest.activeProfileId))
            return false;
        return SaveProfile(_manifest.activeProfileId);
    }

    public bool SaveProfile(string profileId)
    {
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null)
            return false;

        PlayerStateManager psm = PlayerStateManager.Instance;
        WorldStateManager wsm = WorldStateManager.Instance;
        if (psm == null || psm.state == null || wsm == null || wsm.State == null)
            return false;

        psm.Save();
        wsm.Save();

        string folder = EnsureProfileFolder(profileId);
        File.Copy(ActivePlayerPath, Path.Combine(folder, PlayerFileName), true);
        File.Copy(ActiveWorldPath, Path.Combine(folder, WorldFileName), true);
        CopyOrRemoveProfileBackup(
            ActivePlayerPath + BackupSuffix,
            Path.Combine(folder, PlayerFileName) + BackupSuffix);
        CopyOrRemoveProfileBackup(
            ActiveWorldPath + BackupSuffix,
            Path.Combine(folder, WorldFileName) + BackupSuffix);

        entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        _manifest.activeProfileId = profileId;
        SaveManifest();
        return true;
    }

    public bool LoadProfile(string profileId)
    {
        ProfileEntry entry = FindProfile(profileId);
        if (entry == null)
            return false;

        string folder = GetProfileFolder(profileId);
        string playerSrc = Path.Combine(folder, PlayerFileName);
        string worldSrc = Path.Combine(folder, WorldFileName);
        if (!File.Exists(playerSrc) || !File.Exists(worldSrc))
            return false;

        File.Copy(playerSrc, ActivePlayerPath, true);
        File.Copy(worldSrc, ActiveWorldPath, true);
        // note: Recovery files are profile-owned state. Leaving the previous profile's shared backup in place could silently load the wrong character/world when this profile's primary file is damaged.
        CopyOrRemoveProfileBackup(
            playerSrc + BackupSuffix,
            ActivePlayerPath + BackupSuffix);
        CopyOrRemoveProfileBackup(
            worldSrc + BackupSuffix,
            ActiveWorldPath + BackupSuffix);

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

        _manifest.activeProfileId = profileId;
        entry.updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        SaveManifest();

        TeleportPlayerToSavedPosition();
        return true;
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

    private void LoadManifest()
    {
        try
        {
            if (File.Exists(ManifestPath))
                _manifest = JsonUtility.FromJson<ProfileManifest>(File.ReadAllText(ManifestPath)) ?? new ProfileManifest();
            else
                _manifest = new ProfileManifest();
        }
        catch
        {
            _manifest = new ProfileManifest();
        }

        if (_manifest.profiles == null)
            _manifest.profiles = new List<ProfileEntry>();
    }

    private void SaveManifest()
    {
        Directory.CreateDirectory(RootDir);
        File.WriteAllText(ManifestPath, JsonUtility.ToJson(_manifest, true));
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
        File.WriteAllText(path, JsonConvert.SerializeObject(world, Formatting.Indented));
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
}
