// Assets/Assets/Scripts/Observation/Entities/EntityInfo.cs

using UnityEngine;

public enum Hostility
{
    Friendly = 0,
    Neutral = 1,
    Hostile = 2
}

/// <summary>
/// Stable identity card for any entity the director should care about.
/// Attach to NPCs/enemies/guards/important actors.
/// </summary>
public class EntityInfo : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("Stable unique id. Example: npc_archivist_01, mob_goblin_scout_03")]
    public string entityId = "entity_unknown";

    public string displayName = "Unknown";

    [Header("Power")]
    public int level = 1;

    [Header("Affiliation")]
    public string factionId = "none";
    public Hostility hostility = Hostility.Neutral;

    [Header("Director Relevance")]
    public bool isNotable = false;

    [Tooltip("Optional tags: guard, civilian, boss, merchant, etc.")]
    public string[] tags;

    // Preferred API
    public bool IsHostile => hostility == Hostility.Hostile;

    // -----------------------------
    // Compatibility API (fix compile errors)
    // -----------------------------

    // Some scripts still expect a field/property called isHostile
    public bool isHostile => IsHostile;

    // Some scripts expect targetingPlayer (runtime flag).
    // Set this from your AI/perception system if/when available.
    public bool targetingPlayer = false;

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(entityId))
            entityId = "entity_unknown";
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = gameObject.name;
        if (level < 1) level = 1;
        if (string.IsNullOrWhiteSpace(factionId))
            factionId = "none";
    }
}
