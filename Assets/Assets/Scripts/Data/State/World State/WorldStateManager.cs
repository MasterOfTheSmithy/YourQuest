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

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    // note: Preserve the previous complete world document before replacing generated canon.
    private string BackupSavePath => SavePath + ".bak";

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
        bool loaded = TryLoadState(SavePath, out WorldState loadedState);
        if (!loaded && TryLoadState(BackupSavePath, out loadedState))
        {
            // note: Keep the last accepted world plan when the primary write was interrupted.
            Debug.LogWarning("[WorldStateManager] Primary save was unreadable; recovered the last known-good backup.");
            loaded = true;
        }

        if (loaded)
        {
            State = loadedState ?? WorldState.CreateDefault();
        }
        else
        {
            // note: Only a brand-new profile or two invalid files reaches this default-world path.
            State = WorldState.CreateDefault();
        }

        NormalizeState();

        if (!File.Exists(SavePath))
            Save();
    }

    public void Save()
    {
        try
        {
            NormalizeState();
            State.TouchNow();
            string json = JsonConvert.SerializeObject(State, Formatting.Indented);
            WriteAtomically(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WorldStateManager] Save failed:\n" + e);
        }
    }

    private bool TryLoadState(string path, out WorldState loaded)
    {
        loaded = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            // note: Keep deserialization local until the complete persisted world has passed JSON parsing.
            loaded = JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(path));
            return loaded != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WorldStateManager] Save file could not be read: " + Path.GetFileName(path) + "\n" + exception);
            return false;
        }
    }

    private void WriteAtomically(string json)
    {
        string directory = Path.GetDirectoryName(SavePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string temporaryPath = SavePath + ".tmp";
        File.WriteAllText(temporaryPath, json);

        try
        {
            if (File.Exists(SavePath))
            {
                // note: The previous accepted world is retained as a backup before committing the new one.
                File.Replace(temporaryPath, SavePath, BackupSavePath, true);
            }
            else
            {
                File.Move(temporaryPath, SavePath);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // note: Targets without File.Replace still write a complete temporary document before overwriting.
            File.Copy(temporaryPath, SavePath, true);
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // note: This preserves a safe fallback for filesystems without native replace support.
            File.Copy(temporaryPath, SavePath, true);
            File.Delete(temporaryPath);
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
}
