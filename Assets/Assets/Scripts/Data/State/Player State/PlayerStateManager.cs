using System;
using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    public static PlayerStateManager Instance { get; private set; }

    [Header("Runtime State")]
    public PlayerState state = new PlayerState();

    [Header("Save")]
    public string fileName = "player_state.json";
    public bool autosave = true;
    public float autosaveInterval = 10f;

    private float nextAutosave;

    public string SavePath => System.IO.Path.Combine(Application.persistentDataPath, "Save", fileName);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        LoadOrCreate();
        nextAutosave = Time.time + autosaveInterval;
    }

    private void Update()
    {
        if (!autosave) return;
        if (Time.time < nextAutosave) return;
        nextAutosave = Time.time + autosaveInterval;
        Save();
    }

    public void LoadOrCreate()
    {
        if (JsonFileStore.TryLoad<PlayerState>(SavePath, out var loaded))
        {
            state = loaded;
            state.Touch();
            Debug.Log($"[PlayerStateManager] Loaded: {SavePath}");
            return;
        }

        // New save
        state = new PlayerState();
        state.Touch();
        Save();
        Debug.Log($"[PlayerStateManager] Created new save: {SavePath}");
    }

    public void Save()
    {
        state.Touch();
        JsonFileStore.TrySave(SavePath, state);
    }

    // -----------------------
    // High-level mutation API
    // -----------------------

    public void SetLocation(string sceneName, string regionId, Vector3 position)
    {
        state.currentScene = sceneName ?? "";
        state.currentRegionId = regionId ?? "";
        state.lastPosition[0] = position.x;
        state.lastPosition[1] = position.y;
        state.lastPosition[2] = position.z;
        state.Touch();
    }

    public void AddOrUpdateSkillFromCommitted(SkillData committed)
    {
        if (committed == null) return;

        var rec = new SkillRecord
        {
            skillId = committed.skillId,
            familyId = committed.familyId,
            tier = committed.tier,
            parentSkillId = committed.parentSkillId,

            name = committed.skillName,
            description = committed.description,
            type = committed.type.ToString(),

            rank = committed.level,
            unlocked = true,

            context = committed.context,
            environment = committed.environment,

            learnedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };

        state.UpsertSkill(rec);

        // Ensure equip slot exists if empty
        string slotKey = committed.type.ToString();
        if (!state.equippedSkillBySlot.ContainsKey(slotKey) || string.IsNullOrWhiteSpace(state.equippedSkillBySlot[slotKey]))
        {
            state.equippedSkillBySlot[slotKey] = committed.skillId;
        }

        state.Touch();
        if (autosave) Save();
    }

    public void EquipSkill(SkillData skill)
    {
        if (skill == null) return;
        state.equippedSkillBySlot[skill.type.ToString()] = skill.skillId;
        state.Touch();
        if (autosave) Save();
    }

    public void AddTitle(string name, string description)
    {
        var t = new TitleRecord
        {
            titleId = Guid.NewGuid().ToString("N"),
            name = name ?? "Untitled",
            description = description ?? "",
            earnedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        state.titles.Add(t);
        state.Touch();
        if (autosave) Save();
    }

    public void AddQuest(string name, string description, string status = "offer", string[] tags = null)
    {
        var q = new QuestRecord
        {
            questId = Guid.NewGuid().ToString("N"),
            name = name ?? "Unnamed Quest",
            description = description ?? "",
            status = status ?? "offer",
            tags = tags ?? Array.Empty<string>(),
            updatedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        state.quests.Add(q);
        state.Touch();
        if (autosave) Save();
    }
}
