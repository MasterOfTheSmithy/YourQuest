// Assets/Assets/Scripts/Data/State/Player State/PlayerStateManager.cs

using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerStateManager : MonoBehaviour
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

    private float nextAutosaveTime;

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
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadOrCreate();
    }

    private void Update()
    {
        // Keep scene in sync (helps prompts)
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
        if (File.Exists(SavePath))
        {
            try
            {
                string json = File.ReadAllText(SavePath);
                state = JsonConvert.DeserializeObject<PlayerState>(json, JsonSettings) ?? new PlayerState();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PlayerStateManager] Load failed, creating new state:\n" + e);
                state = new PlayerState();
            }
        }
        else
        {
            state = new PlayerState();
            Save(); // first write
        }
    }

    public void Save()
    {
        try
        {
            string json = JsonConvert.SerializeObject(state, JsonSettings);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlayerStateManager] Save failed:\n" + e);
        }
    }

    private void TryAutosave()
    {
        if (!autosave) return;
        if (Time.time < nextAutosaveTime) return;

        nextAutosaveTime = Time.time + Mathf.Max(0.05f, autosaveMinIntervalSeconds);
        Save();
    }

    // Used by PlayerLocationReporter
    public void SetLocation(string sceneName, string regionId, Vector3 position)
    {
        state.currentScene = sceneName ?? state.currentScene;
        state.currentRegionId = regionId ?? state.currentRegionId;
        state.lastPosition = position;
        state.Touch();

        TryAutosave();
    }

    public void SetLocation(string sceneName, string regionId)
    {
        SetLocation(sceneName, regionId, state.lastPosition);
    }

    public void SetRegion(string regionId, string regionName = null)
    {
        state.currentRegionId = regionId ?? "";
        if (regionName != null) state.currentRegionName = regionName;
        state.Touch();
        TryAutosave();
    }

    public void SetPosition(Vector3 pos)
    {
        state.lastPosition = pos;
        state.Touch();
        // no autosave here on purpose
    }

    public void GrantXp(int amount)
    {
        if (amount <= 0) return;

        state.xp += amount;

        while (true)
        {
            int needed = PlayerState.GetXpRequiredForLevel(state.level);
            if (state.xp < needed) break;

            state.xp -= needed;
            state.level += 1;
        }

        state.Touch();
        TryAutosave();
    }

    // Required by UpgradeOfferManager
    public void EquipSkill(SkillData skill)
    {
        if (skill == null) return;

        state.equippedSkillBySlot ??= new System.Collections.Generic.Dictionary<string, string>();

        string slotKey = skill.type.ToString();
        state.equippedSkillBySlot[slotKey] = skill.skillId;

        state.Touch();
        TryAutosave();
    }

    public void EquipSkill(string slotKey, string skillId)
    {
        if (string.IsNullOrWhiteSpace(slotKey) || string.IsNullOrWhiteSpace(skillId)) return;

        state.equippedSkillBySlot ??= new System.Collections.Generic.Dictionary<string, string>();
        state.equippedSkillBySlot[slotKey.Trim()] = skillId.Trim();

        state.Touch();
        TryAutosave();
    }
}
