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
    // note: Keep the previous complete document so interrupted writes cannot erase player-owned generated state.
    private string BackupSavePath => SavePath + ".bak";

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
        bool loaded = TryLoadState(SavePath, out PlayerState loadedState);
        if (!loaded && TryLoadState(BackupSavePath, out loadedState))
        {
            // note: A valid previous version is safer than silently resetting a player's permanent history.
            Debug.LogWarning("[PlayerStateManager] Primary save was unreadable; recovered the last known-good backup.");
            loaded = true;
        }

        if (loaded)
        {
            state = loadedState ?? new PlayerState();
        }
        else
        {
            // note: This path is reserved for a first run or double-corruption fallback.
            state = new PlayerState();
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
            state.Touch();
            string json = JsonConvert.SerializeObject(state, JsonSettings);
            WriteAtomically(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[PlayerStateManager] Save failed:\n" + e);
        }
    }

    private bool TryLoadState(string path, out PlayerState loaded)
    {
        loaded = null;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return false;

        try
        {
            // note: Do not change the authoritative runtime state until a complete JSON document is accepted.
            loaded = JsonConvert.DeserializeObject<PlayerState>(File.ReadAllText(path), JsonSettings);
            return loaded != null;
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[PlayerStateManager] Save file could not be read: " + Path.GetFileName(path) + "\n" + exception);
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
                // note: File.Replace commits the new document while retaining the previous valid version as recovery data.
                File.Replace(temporaryPath, SavePath, BackupSavePath, true);
            }
            else
            {
                File.Move(temporaryPath, SavePath);
            }
        }
        catch (PlatformNotSupportedException)
        {
            // note: Constrained platforms still finish the temporary write before the final overwrite fallback.
            File.Copy(temporaryPath, SavePath, true);
            File.Delete(temporaryPath);
        }
        catch (IOException)
        {
            // note: This fallback covers filesystems that cannot perform a native replace operation.
            File.Copy(temporaryPath, SavePath, true);
            File.Delete(temporaryPath);
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
                item.itemId = Guid.NewGuid().ToString("N");
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
                skill.skillId = Guid.NewGuid().ToString("N");
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
                quest.questId = Guid.NewGuid().ToString("N");
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
                record.classId = Guid.NewGuid().ToString("N");
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
                record.titleId = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(record.name))
                record.name = "Unknown Title";
        }
    }

    private void TryAutosave()
    {
        if (!autosave)
            return;
        if (Time.time < nextAutosaveTime)
            return;

        nextAutosaveTime = Time.time + Mathf.Max(0.05f, autosaveMinIntervalSeconds);
        Save();
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
        state.Touch();
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
