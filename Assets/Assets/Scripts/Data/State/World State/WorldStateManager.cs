// WorldStateManager.cs
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// Authoritative WorldState persistence + mutation API.
/// Saves to Application.persistentDataPath/Save/world_state.json
/// </summary>
public class WorldStateManager : MonoBehaviour
{
    public static WorldStateManager Instance { get; private set; }

    [Header("Persistence")]
    public bool autosave = true;

    [Tooltip("Optional: override filename if needed.")]
    public string fileName = "world_state.json";

    /// <summary>
    /// Preferred API
    /// </summary>
    public WorldState State { get; private set; } = WorldState.CreateDefault();

    /// <summary>
    /// Back-compat for older scripts that reference WorldStateManager.Instance.state
    /// </summary>
    public WorldState state => State;

    private string SaveDir => Path.Combine(Application.persistentDataPath, "Save");
    private string SavePath => Path.Combine(SaveDir, fileName);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        Load();
    }

    // ---------------------------
    // Load / Save
    // ---------------------------

    public void Load()
    {
        try
        {
            Directory.CreateDirectory(SaveDir);

            if (!File.Exists(SavePath))
            {
                State = WorldState.CreateDefault();
                Touch();
                Save();
                Debug.Log($"[WorldStateManager] Created new world state at {SavePath}");
                return;
            }

            var json = File.ReadAllText(SavePath);
            State = JsonConvert.DeserializeObject<WorldState>(json) ?? WorldState.CreateDefault();
            Touch();
            Debug.Log($"[WorldStateManager] Loaded world state from {SavePath}");
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WorldStateManager] Load failed, creating default. " + ex.Message);
            State = WorldState.CreateDefault();
            Touch();
            Save();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SaveDir);
            Touch();

            var json = JsonConvert.SerializeObject(State, Formatting.Indented);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[WorldStateManager] Save failed: " + ex.Message);
        }
    }

    private void Touch()
    {
        if (State == null) State = WorldState.CreateDefault();
        State.Touch();
    }

    // ---------------------------
    // Canon / Region
    // ---------------------------

    public void SetCurrentRegion(string regionId)
    {
        if (string.IsNullOrWhiteSpace(regionId)) return;
        State.currentRegionId = regionId.Trim();
        if (autosave) Save();
    }

    public void AppendCanon(string line, int maxLines = 40)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        line = line.Trim();

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(State.canonLedger))
        {
            var existing = State.canonLedger.Replace("\r", "").Split('\n');
            for (int i = 0; i < existing.Length; i++)
            {
                var l = existing[i].Trim();
                if (!string.IsNullOrWhiteSpace(l)) lines.Add(l);
            }
        }

        lines.Add(line);

        if (maxLines > 0 && lines.Count > maxLines)
            lines.RemoveRange(0, lines.Count - maxLines);

        State.canonLedger = string.Join("\n", lines);
        if (autosave) Save();
    }

    // ---------------------------
    // Flags (global numeric)
    // ---------------------------

    public void SetFlag(string key, float value)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim();

        if (State.globalFlags == null) State.globalFlags = new Dictionary<string, float>();
        State.globalFlags[key] = value;

        if (autosave) Save();
    }

    public void IncFlag(string key, float delta)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        key = key.Trim();

        if (State.globalFlags == null) State.globalFlags = new Dictionary<string, float>();

        if (!State.globalFlags.TryGetValue(key, out var cur))
            cur = 0f;

        State.globalFlags[key] = cur + delta;

        if (autosave) Save();
    }

    public float GetFlag(string key, float fallback = 0f)
    {
        if (string.IsNullOrWhiteSpace(key)) return fallback;
        key = key.Trim();

        if (State.globalFlags == null) return fallback;
        return State.globalFlags.TryGetValue(key, out var v) ? v : fallback;
    }

    // ---------------------------
    // Factions
    // ---------------------------

    public FactionRecord EnsureFaction(string factionId, string name = null)
    {
        if (string.IsNullOrWhiteSpace(factionId)) return null;
        return State.GetOrCreateFaction(factionId.Trim(), name);
    }

    public void SetFactionAttitude(string factionId, float value)
    {
        var f = EnsureFaction(factionId);
        if (f == null) return;

        f.attitudeToPlayer = Mathf.Clamp(value, -1f, 1f);
        if (autosave) Save();
    }

    public void IncFactionAttitude(string factionId, float delta)
    {
        var f = EnsureFaction(factionId);
        if (f == null) return;

        f.attitudeToPlayer = Mathf.Clamp(f.attitudeToPlayer + delta, -1f, 1f);
        if (autosave) Save();
    }

    public void SetFactionStatus(string factionId, string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return;
        var f = EnsureFaction(factionId);
        if (f == null) return;

        f.status = status.Trim();
        if (autosave) Save();
    }

    // ---------------------------
    // Locations
    // ---------------------------

    public LocationRecord EnsureLocation(string locationId, string name = null, string regionId = null)
    {
        if (string.IsNullOrWhiteSpace(locationId)) return null;
        return State.GetOrCreateLocation(locationId.Trim(), name, regionId);
    }

    public void SetLocationState(string locationId, string state)
    {
        if (string.IsNullOrWhiteSpace(state)) return;

        var l = EnsureLocation(locationId);
        if (l == null) return;

        l.state = state.Trim();
        if (autosave) Save();
    }

    public void SetLocationImportance(string locationId, float value)
    {
        var l = EnsureLocation(locationId);
        if (l == null) return;

        l.importance = Mathf.Clamp(value, -1000f, 1000f);
        if (autosave) Save();
    }

    public void IncLocationImportance(string locationId, float delta)
    {
        var l = EnsureLocation(locationId);
        if (l == null) return;

        l.importance = Mathf.Clamp(l.importance + delta, -1000f, 1000f);
        if (autosave) Save();
    }

    public void SetLocationText(string locationId, string text)
    {
        var l = EnsureLocation(locationId);
        if (l == null) return;

        l.text = (text ?? "").Trim();
        if (autosave) Save();
    }
}
