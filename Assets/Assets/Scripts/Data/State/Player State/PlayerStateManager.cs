// Assets/Assets/Scripts/Data/State/Player State/PlayerStateManager.cs

using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

public partial class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    [Header("Persistence")]
    public string saveFileName = "player_state.json";

    [Tooltip("If true, Save() may be called by systems after mutations.")]
    public bool autosave = true;

    [Tooltip("Minimum seconds between autosaves to avoid disk spam.")]
    public float autosaveMinIntervalSeconds = 2f;

    public PlayerState state = new PlayerState();

    public PlayerState GetPlayerState() => state;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    // note: Keep the previous complete document so interrupted writes cannot erase player-owned generated state.
    private string BackupSavePath => SavePath + ".bak";

    private float nextAutosaveTime;
    // note: One immutable automatic snapshot may write at a time; explicit persistence/ownership boundaries drain it first.
    private Task pendingAutosave;
    public string LastLoadStatus { get; private set; } = "not_loaded";

    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        Formatting = Formatting.Indented,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters =
        {
            new Vector3JsonConverter(),
            new Vector2JsonConverter(),
            new QuaternionJsonConverter()
        }
    };

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        LoadOrCreate();
    }

    private void Update()
    {
        // note: Observe worker failures on the Unity thread without waiting during ordinary movement frames.
        if (pendingAutosave != null && pendingAutosave.IsCompleted && !TryFlushPendingAutosave(out string autosaveFailure))
            Debug.LogWarning("[PlayerStateManager] Autosave failed: " + autosaveFailure);
        if (state == null)
            return;

        string scene = SceneManager.GetActiveScene().name;
        if (state.currentScene != scene)
        {
            state.currentScene = scene;
            state.Touch();
            TryAutosave();
        }
    }

    public void LoadOrCreate()
    {
        // note: A test rollback must retain its original player owner and cannot silently reload a normal save.
        if (YQDeveloperConsoleGate.BlocksPersistence) return;
        // note: A previous snapshot must finish before a reload reads or replaces the shared player document.
        if (!TryFlushPendingAutosave(out string autosaveFailure))
        {
            LastLoadStatus = "autosave_flush_failed";
            Debug.LogWarning("[PlayerStateManager] Reload rejected: " + autosaveFailure);
            return;
        }
        bool hadPersistentState = File.Exists(SavePath) || File.Exists(BackupSavePath);
        string backupFailure = string.Empty;
        bool loaded = TryLoadState(SavePath, out PlayerState loadedState, out string primaryFailure);
        if (!loaded && TryLoadState(BackupSavePath, out loadedState, out backupFailure))
        {
            // note: A valid previous version is safer than silently resetting a player's permanent history.
            Debug.LogWarning("[PlayerStateManager] Primary save was unreadable; recovered the last known-good backup.");
            loaded = true;
            LastLoadStatus = "recovered_backup";
        }

        if (loaded)
        {
            state = loadedState ?? new PlayerState();
            if (LastLoadStatus == "not_loaded") LastLoadStatus = "loaded";
        }
        else
        {
            // note: Unsupported future schemas fail closed and never overwrite the user's only unreadable save with defaults.
            state = new PlayerState();
            LastLoadStatus = hadPersistentState && ((primaryFailure ?? string.Empty).IndexOf("Unsupported", StringComparison.OrdinalIgnoreCase) >= 0 ||
                (backupFailure ?? string.Empty).IndexOf("Unsupported", StringComparison.OrdinalIgnoreCase) >= 0)
                ? "unsupported_version" : "created_default";
        }

        NormalizeState();

        if (!hadPersistentState && !File.Exists(SavePath))
            Save();
    }

    public void Save()
    {
        TrySave(out _);
    }

    public bool TrySave(out string failure)
    {
        // note: Explicit Save callers as well as autosave must respect temporary development state isolation.
        if (YQDeveloperConsoleGate.BlocksPersistence) { failure = YQDeveloperConsoleGate.SaveBlocked; return false; }
        failure = string.Empty;
        try
        {
            if (!TryPrepareSnapshot(out string json, out failure))
                return false;
            WriteAtomically(json);
            return true;
        }
        catch (Exception e)
        {
            failure = e.Message;
            Debug.LogWarning("[PlayerStateManager] Save failed:\n" + e);
            return false;
        }
    }

    public bool TryPrepareSnapshot(out string json, out string failure)
    {
        json = string.Empty;
        failure = string.Empty;
        try
        {
            // note: Paired profile commits use this snapshot boundary, so an older automatic writer cannot race their projections.
            if (!TryFlushPendingAutosave(out failure))
                return false;
            NormalizeState();
            json = JsonConvert.SerializeObject(state, JsonSettings);
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    private bool TryLoadState(string path, out PlayerState loaded, out string failure)
    {
        loaded = null;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            // note: Do not change the authoritative runtime state until a complete JSON document is accepted.
            if (!YQStateMigrations.TryNormalizePlayerDocument(File.ReadAllText(path), out string normalizedJson, out _, out failure))
                return false;
            loaded = JsonConvert.DeserializeObject<PlayerState>(normalizedJson, JsonSettings);
            YQMigrationResult migration = null;
            if (loaded == null || !YQStateMigrations.TryMigrate(loaded, out migration))
            {
                failure = loaded == null ? "Player record was null." : migration.message;
                Debug.LogError("[PlayerStateManager] Load rejected: " + failure);
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            Debug.LogWarning("[PlayerStateManager] Save file could not be read: " + Path.GetFileName(path) + "\n" + exception);
            return false;
        }
    }

    private void WriteAtomically(string json)
    {
        WriteAtomically(json, SavePath, BackupSavePath);
    }

    private static void WriteAtomically(string json, string path, string backupPath)
    {
        // note: Captured paths and JSON are the worker's complete inputs; no Unity object or mutable state is read off-thread.
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, json);

        try
        {
            if (File.Exists(path))
            {
                // note: File.Replace commits the new document while retaining the previous valid version as recovery data.
                File.Replace(temporaryPath, path, backupPath, true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // note: Unsupported atomic replacement is observable failure; the previous accepted save remains untouched.
            throw new IOException("This filesystem does not support atomic player-save replacement.");
        }
    }

    private void NormalizeState()
    {
        if (state == null)
            state = new PlayerState();

        state.EnsureCollections();
        state.displayName = string.IsNullOrWhiteSpace(state.displayName) ? "The Player" : state.displayName.Trim();
        state.playerId = string.IsNullOrWhiteSpace(state.playerId) ? "player" : state.playerId.Trim();
        state.level = Mathf.Max(1, state.level);
        state.experience = Mathf.Max(0, state.experience);

        if (string.IsNullOrWhiteSpace(state.currentRegionId))
            state.currentRegionId = "region_unknown";
        if (string.IsNullOrWhiteSpace(state.currentRegionName))
            state.currentRegionName = "Unknown";
        if (state.stats == null)
            state.stats = new StatBlock();

        state.stats.maxHealth = Mathf.Max(1, state.stats.maxHealth);
        state.stats.maxStamina = Mathf.Max(1, state.stats.maxStamina);
        state.stats.maxMana = Mathf.Max(1, state.stats.maxMana);
        state.stats.attack = Mathf.Max(1, state.stats.attack);
        state.stats.defense = Mathf.Max(0, state.stats.defense);
        state.stats.moveSpeed = Mathf.Max(1f, state.stats.moveSpeed);
        state.stats.critChance = Mathf.Clamp01(state.stats.critChance);

        for (int i = state.inventoryItems.Count - 1; i >= 0; i--)
        {
            InventoryItemRecord item = state.inventoryItems[i];
            if (item == null)
            {
                state.inventoryItems.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(item.itemId))
                item.itemId = YQStateContract.LegacyId(YQStableEntityKind.Item, state.playerId, i, 0);
            if (string.IsNullOrWhiteSpace(item.displayName))
                item.displayName = "Unknown Item";
            if (string.IsNullOrWhiteSpace(item.itemType))
                item.itemType = string.IsNullOrWhiteSpace(item.equipSlot) ? "misc" : "armor";
            if (item.quantity < 1)
                item.quantity = 1;
        }

        for (int i = state.skills.Count - 1; i >= 0; i--)
        {
            SkillRecord skill = state.skills[i];
            if (skill == null)
            {
                state.skills.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(skill.skillId))
                skill.skillId = YQStateContract.LegacyId(YQStableEntityKind.Content, state.playerId, i, 0);
            if (string.IsNullOrWhiteSpace(skill.name))
                skill.name = "Unknown Skill";
            if (skill.tier <= 0)
                skill.tier = 1;
            if (skill.rank <= 0)
                skill.rank = 1;
        }

        for (int i = state.quests.Count - 1; i >= 0; i--)
        {
            QuestRecord quest = state.quests[i];
            if (quest == null)
            {
                state.quests.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(quest.questId))
                quest.questId = YQStateContract.LegacyId(YQStableEntityKind.Quest, state.playerId, i, quest.createdUnix);
            if (string.IsNullOrWhiteSpace(quest.name))
                quest.name = "Unknown Quest";
            if (string.IsNullOrWhiteSpace(quest.status))
                quest.status = "active";
            quest.tags ??= Array.Empty<string>();
        }
        state.GetActiveQuest();

        for (int i = state.classes.Count - 1; i >= 0; i--)
        {
            ClassRecord record = state.classes[i];
            if (record == null)
            {
                state.classes.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.classId))
                record.classId = YQStateContract.LegacyId(YQStableEntityKind.Content, state.playerId, i, record.unlockedUnix);
            if (string.IsNullOrWhiteSpace(record.name))
                record.name = "Unknown Class";
        }

        for (int i = state.titles.Count - 1; i >= 0; i--)
        {
            TitleRecord record = state.titles[i];
            if (record == null)
            {
                state.titles.RemoveAt(i);
                continue;
            }

            if (string.IsNullOrWhiteSpace(record.titleId))
                record.titleId = YQStateContract.LegacyId(YQStableEntityKind.Content, state.playerId, i, record.acquiredUnix);
            if (string.IsNullOrWhiteSpace(record.name))
                record.name = "Unknown Title";
        }

        // note: Register identities only after legacy fields have been assigned so migration is repeatable and label-independent.
        YQStateIdentity.EnsurePlayerState(state);
    }

    private void TryAutosave()
    {
        // note: Do not enqueue an asynchronous projection containing temporary test mutations.
        if (YQDeveloperConsoleGate.BlocksPersistence) return;
        if (!autosave)
            return;
        if (Time.time < nextAutosaveTime)
            return;
        // note: Keep automatic writes serialized; the next ordinary due update captures the latest state after this writer completes.
        if (pendingAutosave != null && !pendingAutosave.IsCompleted)
            return;

        nextAutosaveTime = Time.time + Mathf.Max(0.05f, autosaveMinIntervalSeconds);
        if (!TryPrepareSnapshot(out string json, out string failure))
        {
            Debug.LogWarning("[PlayerStateManager] Autosave snapshot failed: " + failure);
            return;
        }
        string path = SavePath;
        string backupPath = BackupSavePath;
        pendingAutosave = Task.Run(() => WriteAtomically(json, path, backupPath));
    }

    public bool TryFlushPendingAutosave(out string failure)
    {
        // note: Explicit save, profile replacement, reload and teardown remain synchronous barriers with observable failures.
        failure = string.Empty;
        Task pending = pendingAutosave;
        if (pending == null)
            return true;
        try
        {
            pending.GetAwaiter().GetResult();
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }
        finally
        {
            if (pendingAutosave == pending)
                pendingAutosave = null;
        }
    }

    public void SetLocation(string sceneName, string regionId, Vector3 position)
    {
        NormalizeState();
        state.currentScene = sceneName ?? state.currentScene;
        state.currentRegionId = regionId ?? state.currentRegionId;
        state.lastPosition = position;
        state.Touch();
        TryAutosave();
    }

    public void SetLocation(string sceneName, string regionId)
    {
        SetLocation(sceneName, regionId, state != null ? state.lastPosition : Vector3.zero);
    }

    public void SetRegion(string regionId, string regionName = null)
    {
        NormalizeState();
        state.currentRegionId = string.IsNullOrWhiteSpace(regionId) ? "region_unknown" : regionId;
        if (regionName != null)
            state.currentRegionName = string.IsNullOrWhiteSpace(regionName) ? "Unknown" : regionName;
        state.Touch();
        TryAutosave();
    }

    public void SetPosition(Vector3 pos)
    {
        NormalizeState();
        state.lastPosition = pos;
        state.logicalPosition = pos;
        state.Touch();
    }

    private void OnApplicationQuit()
    {
        // note: The profile owner performs the paired commit; this fallback protects standalone state-manager scenes.
        if (YQProfileSaveSystem.Instance == null)
            TrySave(out _);
    }

    private void OnDestroy()
    {
        // note: Scene teardown cannot leave an old document writer alive across the next player/profile owner.
        if (!TryFlushPendingAutosave(out string autosaveFailure))
            Debug.LogWarning("[PlayerStateManager] Teardown autosave failed: " + autosaveFailure);
        // note: Clear the singleton so scene reloads cannot route mutations to a destroyed player state owner.
        if (Instance == this)
            Instance = null;
    }

    public void GrantXp(int amount)
    {
        if (amount <= 0)
            return;

        NormalizeState();
        state.xp += amount;
        while (true)
        {
            int needed = PlayerState.GetXpRequiredForLevel(state.level);
            if (state.xp < needed)
                break;

            state.xp -= needed;
            state.level += 1;
        }

        state.Touch();
        TryAutosave();
    }

    public void EquipSkill(SkillData skill)
    {
        if (skill == null)
            return;

        NormalizeState();
        string slotKey = skill.type.ToString();
        state.equippedSkillBySlot[slotKey] = skill.skillId;
        state.Touch();
        TryAutosave();
    }

    public void EquipSkill(string slotKey, string skillId)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || string.IsNullOrWhiteSpace(skillId))
            return;

        NormalizeState();
        state.equippedSkillBySlot[slotKey.Trim()] = skillId.Trim();
        state.Touch();
        TryAutosave();
    }
}
