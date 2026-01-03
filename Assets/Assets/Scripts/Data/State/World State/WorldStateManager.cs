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

    // legacy alias
    public WorldState state => State;

    public WorldState GetWorldState() => State;

    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadOrCreate();
    }

    public void LoadOrCreate()
    {
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                State = JsonConvert.DeserializeObject<WorldState>(json) ?? WorldState.CreateDefault();
            }
            catch
            {
                State = WorldState.CreateDefault();
            }
        }
        else
        {
            State = WorldState.CreateDefault();
            Save();
        }
    }

    public void Save()
    {
        try
        {
            State.TouchNow();
            string json = JsonConvert.SerializeObject(State, Formatting.Indented);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[WorldStateManager] Save failed:\n" + e);
        }
    }

    public void AddCanonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        State.AppendCanon(line.Trim());
        State.TouchNow();
    }

    // ? required by PlayerLocationReporter
    public void SetCurrentRegion(string regionId, string regionName = null)
    {
        State.currentRegionId = regionId ?? "";
        if (regionName != null) State.currentRegionName = regionName;
        State.TouchNow();
    }

    public void SetTension(float t)
    {
        State.tension = t;
        State.ApplyFlagDelta("tension", "set", t);
    }
}
