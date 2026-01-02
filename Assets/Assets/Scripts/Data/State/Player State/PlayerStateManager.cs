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
            state = loaded ?? new PlayerState();

            // Ensure new collections are not null when loading older saves
            state.titles ??= new System.Collections.Generic.List<TitleRecord>();
            state.classes ??= new System.Collections.Generic.List<ClassRecord>();
            state.skills ??= new System.Collections.Generic.List<SkillRecord>();
            state.quests ??= new System.Collections.Generic.List<QuestRecord>();
            state.equippedSkillBySlot ??= new System.Collections.Generic.Dictionary<string, string>();
            state.reputation ??= new System.Collections.Generic.Dictionary<string, float>();
            state.behaviorLedger ??= new System.Collections.Generic.List<string>();
            state.behaviorCounters ??= new System.Collections.Generic.Dictionary<string, float>();

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
        if (state == null) state = new PlayerState();
        state.Touch();
        JsonFileStore.TrySave(SavePath, state);
    }

    // -----------------------
    // High-level mutation API
    // -----------------------

    public void SetLocation(string sceneName, string regionId, Vector3 position)
    {
        if (state == null) state = new PlayerState();

        state.currentScene = sceneName ?? "";
        state.currentRegionId = regionId ?? "";

        // ? FIX: Vector3 is not indexable — assign directly
        state.lastPosition = position;

        state.Touch();
    }

    public void AddOrUpdateSkillFromCommitted(SkillData committed)
    {
        if (committed == null) return;
        if (state == null) state = new PlayerState();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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

            learnedUnix = now,

            // back-compat field (fine to keep)
            acquiredUnix = now
        };

        state.UpsertSkill(rec);

        // Ensure equip slot exists if empty
        state.equippedSkillBySlot ??= new System.Collections.Generic.Dictionary<string, string>();

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
        if (state == null) state = new PlayerState();

        state.equippedSkillBySlot ??= new System.Collections.Generic.Dictionary<string, string>();
        state.equippedSkillBySlot[skill.type.ToString()] = skill.skillId;

        state.Touch();
        if (autosave) Save();
    }

    public void AddTitle(string name, string description)
    {
        if (state == null) state = new PlayerState();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var t = new TitleRecord
        {
            titleId = Guid.NewGuid().ToString("N"),
            name = name ?? "Untitled",
            description = description ?? "",

            // new + back-compat
            earnedUnix = now,
            acquiredUnix = now
        };

        state.titles ??= new System.Collections.Generic.List<TitleRecord>();
        state.titles.Add(t);

        state.Touch();
        if (autosave) Save();
    }

    public void AddQuest(string name, string description, string status = "offer", string[] tags = null)
    {
        if (state == null) state = new PlayerState();

        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        var q = new QuestRecord
        {
            questId = Guid.NewGuid().ToString("N"),
            name = name ?? "Unnamed Quest",
            description = description ?? "",
            status = status ?? "offer",
            tags = tags ?? Array.Empty<string>(),
            createdUnix = now,
            updatedUnix = now
        };

        state.quests ??= new System.Collections.Generic.List<QuestRecord>();
        state.quests.Add(q);

        state.Touch();
        if (autosave) Save();
    }
}
