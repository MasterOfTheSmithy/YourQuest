#if UNITY_EDITOR || (DEVELOPMENT_BUILD && YQ_DEVELOPER_CONSOLE)
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

// note: This is a rollback buffer for the existing owners, never a replacement gameplay state or a save slot.
public static class YQDeveloperTestSession
{
    public static bool Active => _player != null;
    public static bool Publishing { get; private set; }
    private static bool _temporarySceneChanges;
    private static PlayerStateManager _manager;
    private static WorldStateManager _worldManager;
    private static PlayerState _player;
    private static WorldState _world;
    private static string _playerJson;
    private static string _containersJson;
    private static string _profileId;
    private static readonly Dictionary<string, (float affinity, long updated)> NpcAffinities = new Dictionary<string, (float, long)>();
    private static readonly List<Action> Rollbacks = new List<Action>();
    private static readonly List<Action> RuntimeRollbacks = new List<Action>();
    private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        ObjectCreationHandling = ObjectCreationHandling.Replace,
        Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() }
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRetiredSession()
    {
        // note: With domain reload disabled, a destroyed previous player cannot retain a save barrier in the next Play session.
        // A surviving owner deliberately keeps its rollback buffer/barrier until restoration succeeds.
        if (Active && _manager == null) Clear();
        Publishing = false;
    }

    public static bool Begin(out string message)
    {
        return Begin(PlayerStateManager.Instance, WorldStateManager.Instance, out message);
    }

    public static bool Begin(PlayerStateManager manager, WorldStateManager world, out string message)
    {
        if (Active) { message = "Snapshot already active; restore or persist before replacing it."; return false; }
        if (manager == null || manager.state == null || world == null || world.State == null)
        { message = "Player/world state owners are not ready."; return false; }
        if (LLMClient.Instance != null && LLMClient.Instance.IsBusy)
        { message = "Wait for in-flight generation/dialogue to finish before snapshotting."; return false; }
        if (!manager.TryPrepareSnapshot(out string json, out message)) return false;
        _manager = manager; _worldManager = world; _player = manager.state; _world = world.State;
        _playerJson = json;
        // note: Container test transfers/regeneration roll back with the player's items, preserving their cross-document conservation boundary.
        _containersJson = JsonConvert.SerializeObject(_world.containers, Settings);
        _profileId = YQProfileSaveSystem.Instance?.ActiveProfileId;
        // note: Current vitals are runtime-owned rather than serialized; use their explicit restoration API after restoring max stats.
        var vitals = UnityEngine.Object.FindFirstObjectByType<YQInvestorVitals>();
        if (vitals != null && ReferenceEquals(manager, PlayerStateManager.Instance))
        {
            float health = vitals.CurrentHealth, stamina = vitals.CurrentStamina, mana = vitals.CurrentMana;
            RuntimeRollbacks.Add(() => { if (vitals != null) vitals.DevelopmentRestoreVitals(health, stamina, mana); });
        }
        NpcAffinities.Clear();
        foreach (WorldState.NpcRecord npc in _world.npcs)
            if (npc != null && !string.IsNullOrWhiteSpace(npc.npcId)) NpcAffinities[npc.npcId] = (npc.affinityToPlayer, npc.updatedUnix);
        message = "SESSION snapshot captured. All player changes since this point and NPC affinity edits are reversible. Saves/profile transitions are blocked; scene/world/position changes are not rolled back.";
        return true;
    }

    public static bool CheckOwners(out string message)
    {
        bool valid = Active && _manager != null && _worldManager != null &&
            ReferenceEquals(_manager.state, _player) && ReferenceEquals(_worldManager.State, _world) &&
            string.Equals(_profileId, YQProfileSaveSystem.Instance?.ActiveProfileId, StringComparison.Ordinal);
        message = valid ? string.Empty : "Snapshot owners changed; refusing cross-profile restoration/mutation.";
        return valid;
    }

    public static void OnRestore(Action restore) => Rollbacks.Add(restore);

    // note: Test encounters are temporary scene objects; they must never be published into a normal profile.
    public static void OnTemporarySceneChange(Action cleanup)
    { _temporarySceneChanges = true; Rollbacks.Add(cleanup); }

    public static bool Restore(out string message)
    {
        if (!CheckOwners(out message)) return false;
        if (LLMClient.Instance != null && LLMClient.Instance.IsBusy)
        { message = "Wait for in-flight LLM work before restoring."; return false; }
        // note: Retain the player document object (async consumers bind it); deserialize with the same converters as its owner.
        Vector3 livePosition = _player.lastPosition, logical = _player.logicalPosition, origin = _player.renderOrigin;
        long liveRevision = _player.stateRevision;
        string liveScene = _player.currentScene, region = _player.currentRegionId, regionName = _player.currentRegionName;
        JsonConvert.PopulateObject(_playerJson, _player, Settings);
        _world.containers = JsonConvert.DeserializeObject<Dictionary<string, YQContainerRecord>>(_containersJson, Settings)
            ?? new Dictionary<string, YQContainerRecord>(StringComparer.Ordinal);
        // note: Replacement JSON collections must retain the canonical case-insensitive key contracts, and rollback cannot reuse an old revision.
        _player.equippedSkillBySlot = new Dictionary<string, string>(_player.equippedSkillBySlot, StringComparer.OrdinalIgnoreCase);
        _player.equippedItemBySlot = new Dictionary<string, string>(_player.equippedItemBySlot, StringComparer.OrdinalIgnoreCase);
        _player.reputation = new Dictionary<string, float>(_player.reputation, StringComparer.OrdinalIgnoreCase);
        _player.behaviorCounters = new Dictionary<string, float>(_player.behaviorCounters, StringComparer.OrdinalIgnoreCase);
        _player.stateRevision = Math.Max(_player.stateRevision, liveRevision);
        _player.lastPosition = livePosition; _player.logicalPosition = logical; _player.renderOrigin = origin;
        _player.currentScene = liveScene; _player.currentRegionId = region; _player.currentRegionName = regionName;
        foreach (WorldState.NpcRecord npc in _world.npcs)
            if (npc != null && NpcAffinities.TryGetValue(npc.npcId ?? "", out var snapshot))
            { npc.affinityToPlayer = snapshot.affinity; npc.updatedUnix = snapshot.updated; }
        for (int i = Rollbacks.Count - 1; i >= 0; i--) Rollbacks[i]();
        for (int i = RuntimeRollbacks.Count - 1; i >= 0; i--) RuntimeRollbacks[i]();
        message = "RESTORED player state and NPC affinities; temporary progression overrides cleared. No save written. World/scene/live position retained.";
        _world.TouchNow();
        _manager.DevelopmentNotify(message);
        Clear();
        return true;
    }

    public static bool Persist(out string message)
    {
        if (!CheckOwners(out message)) return false;
        if (_temporarySceneChanges) { message = "Temporary test encounters are active. Use test restore before publishing a normal profile."; return false; }
        if (YQProfileSaveSystem.Instance == null) { message = "No paired profile save owner; persistence unavailable."; return false; }
        if (LLMClient.Instance != null && LLMClient.Instance.IsBusy) { message = "Wait for LLM work before persistence."; return false; }
        // note: Only this explicit command can cross the temporary-session save barrier, using the existing paired/checksummed commit.
        Publishing = true;
        bool saved;
        try { saved = YQProfileSaveSystem.Instance.SaveActiveProfile(); }
        finally { Publishing = false; }
        if (!saved) { message = "Persistence failed: " + YQProfileSaveSystem.Instance.LastFailure + "; snapshot retained."; return false; }
        // note: Reset temporary rule configuration without rewinding current vitals in a deliberately published state.
        for (int i = Rollbacks.Count - 1; i >= 0; i--) Rollbacks[i]();
        Clear();
        message = "PERSISTENT paired profile revision published. Progression settings remain session-only and have been reset; rollback snapshot discarded.";
        return true;
    }

    private static void Clear()
    {
        _player = null; _world = null; _manager = null; _worldManager = null; _playerJson = null; _containersJson = null;
        Rollbacks.Clear(); RuntimeRollbacks.Clear(); NpcAffinities.Clear();
        _temporarySceneChanges = false;
    }
}
#endif
