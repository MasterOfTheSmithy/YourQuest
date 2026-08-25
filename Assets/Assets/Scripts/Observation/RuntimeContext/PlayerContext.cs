using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Runtime-only semantic context about the player's current situation.
/// This is NOT authoritative persistence (PlayerState.json is).
/// It exists to provide high-signal context for ActionEvents + LLM prompts.
/// </summary>
public class PlayerContext : MonoBehaviour
{
    public static PlayerContext Instance { get; private set; }

    [Header("Semantic Region")]
    [Tooltip("Stable semantic region id (e.g. region_library). Falls back to grid bucket if empty.")]
    public string SemanticRegionId;

    [Tooltip("Human-readable semantic region name (e.g. Library).")]
    public string SemanticRegionName;

    [Tooltip("Optional region tags (e.g. indoors, restricted, safe_zone).")]
    public List<string> RegionTags = new List<string>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void SetRegion(string regionId, string regionName, IEnumerable<string> tags = null)
    {
        SemanticRegionId = string.IsNullOrWhiteSpace(regionId) ? null : regionId.Trim();
        SemanticRegionName = string.IsNullOrWhiteSpace(regionName) ? null : regionName.Trim();

        RegionTags.Clear();
        if (tags != null)
        {
            foreach (var t in tags)
            {
                if (string.IsNullOrWhiteSpace(t)) continue;
                RegionTags.Add(t.Trim().ToLowerInvariant());
            }
        }
    }

    public void ClearRegion()
    {
        SemanticRegionId = null;
        SemanticRegionName = null;
        RegionTags.Clear();
    }
}


