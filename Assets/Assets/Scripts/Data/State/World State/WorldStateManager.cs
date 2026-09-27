// Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }

    [Header("Persistence")]
    public string saveFileName = "world_state.json";

    public WorldState State { get; private set; } = WorldState.CreateDefault();
    public WorldState state => State;
    public WorldState GetWorldState() => State;

    // note: Keep Unity vector values on the explicit x/y/z converter so persistence never walks Vector3's derived normalized property.
    private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
    {
        // note: Compact JSON keeps the transactional world snapshot identical in schema while reducing synchronous serialization and atomic-write time.
        Formatting = Formatting.None,
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        Converters =
        {
            new Vector3JsonConverter(),
            new Vector2JsonConverter(),
            new QuaternionJsonConverter()
        }
    };

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    // note: Preserve the previous complete world document before replacing generated canon.
    private string BackupSavePath => SavePath + ".bak";
    public string LastLoadStatus { get; private set; } = "not_loaded";

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

    public void LoadOrCreate()
    {
        bool hadPersistentState = File.Exists(SavePath) || File.Exists(BackupSavePath);
        string backupFailure = string.Empty;
        bool loaded = TryLoadState(SavePath, out WorldState loadedState, out string primaryFailure);
        if (!loaded && TryLoadState(BackupSavePath, out loadedState, out backupFailure))
        {
            // note: Keep the last accepted world plan when the primary write was interrupted.
            Debug.LogWarning("[WorldStateManager] Primary save was unreadable; recovered the last known-good backup.");
            loaded = true;
            LastLoadStatus = "recovered_backup";
        }

        if (loaded)
        {
            State = loadedState ?? WorldState.CreateDefault();
            if (LastLoadStatus == "not_loaded") LastLoadStatus = "loaded";
        }
        else
        {
            // note: Unsupported future schemas fail closed and never overwrite the only unreadable world save.
            State = WorldState.CreateDefault();
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
        // note: Preserve existing Unity events and callers while transactional owners can observe persistence failure.
        TrySave(out _);
    }

    public bool TrySave(out string failure)
    {
        failure = string.Empty;
        try
        {
            NormalizeState();
            State.TouchNow();
            // note: Compact JSON preserves the same persisted schema while reducing serialization and atomic-write time for large procedural world saves.
            string json = JsonConvert.SerializeObject(State, JsonSettings);
            WriteAtomically(json);
            return true;
        }
        catch (Exception e)
        {
            failure = e.Message;
            Debug.LogWarning("[WorldStateManager] Save failed:\n" + e);
            return false;
        }
    }

    public bool TryPrepareSnapshot(out string json, out string failure)
    {
        json = string.Empty;
        failure = string.Empty;
        try
        {
            NormalizeState();
            json = JsonConvert.SerializeObject(State, JsonSettings);
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            return false;
        }
    }

    private bool TryLoadState(string path, out WorldState loaded, out string failure)
    {
        loaded = null;
        failure = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            // note: Keep deserialization local until the complete persisted world has passed JSON parsing.
            loaded = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(path), JsonSettings);
            YQMigrationResult migration = null;
            if (loaded == null || !YQStateMigrations.TryMigrate(loaded, out migration))
            {
                failure = loaded == null ? "World record was null." : migration.message;
                Debug.LogError("[WorldStateManager] Load rejected: " + failure);
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            failure = exception.Message;
            Debug.LogWarning("[WorldStateManager] Save file could not be read: " + Path.GetFileName(path) + "\n" + exception);
            return false;
        }
    }

    private void WriteAtomically(string json)
    {
        // note: Keep filesystem commit logic independently testable without creating another world-state manager or touching a profile.
        WriteWorldDocument(SavePath, json);
    }

    internal static void WriteWorldDocument(string path, string json)
    {
        string directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = path + ".tmp";
        File.WriteAllText(temporaryPath, json);

        try
        {
            if (File.Exists(path))
            {
                // note: The previous accepted world is retained as a backup before committing the new one.
                File.Replace(temporaryPath, path, path + ".bak", true);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // note: An unsupported atomic commit is failure, not permission to overwrite the last accepted save non-atomically.
            throw new IOException("This filesystem does not support atomic world-save replacement; the accepted save was preserved.");
        }
    }

    private void NormalizeState()
    {
        if (State == null)
            State = WorldState.CreateDefault();

        State.EnsureCollections();
        State.worldName = string.IsNullOrWhiteSpace(State.worldName) ? "YourQuest" : State.worldName.Trim();
        if (string.IsNullOrWhiteSpace(State.currentRegionId))
            State.currentRegionId = "region_unknown";
        if (string.IsNullOrWhiteSpace(State.currentRegionName))
            State.currentRegionName = "Unknown";
        State.tension = Mathf.Max(0f, State.tension);
        YQStateIdentity.EnsureWorldState(State);
    }

    public void AddCanonLine(string line)
    {
        NormalizeState();
        if (string.IsNullOrWhiteSpace(line))
            return;
        State.AppendCanon(line.Trim());
        State.TouchNow();
    }

    public void SetCurrentRegion(string regionId, string regionName = null)
    {
        NormalizeState();
        State.currentRegionId = string.IsNullOrWhiteSpace(regionId) ? "region_unknown" : regionId;
        if (regionName != null)
            State.currentRegionName = string.IsNullOrWhiteSpace(regionName) ? "Unknown" : regionName;
        State.TouchNow();
    }

    public void ReplaceState(WorldState newState)
    {
        State = newState ?? WorldState.CreateDefault();
        NormalizeState();
    }

    public void SetTension(float t)
    {
        NormalizeState();
        State.tension = Mathf.Max(0f, t);
        State.ApplyFlagDelta("tension", "set", State.tension);
    }

    private void OnApplicationQuit()
    {
        // note: The profile owner performs the paired commit; this fallback protects standalone world-state scenes.
        if (YQProfileSaveSystem.Instance == null)
            TrySave(out _);
    }

    private void OnDestroy()
    {
        // note: Clear the singleton so scene reloads cannot route mutations to a destroyed world state owner.
        if (Instance == this)
            Instance = null;
    }
}
