using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Attach to trigger volumes to set a semantic region on the player.
/// Use stable ids like 'region_library' and readable names like 'Library'.
/// </summary>
[RequireComponent(typeof(Collider))]
public class RegionVolume : MonoBehaviour
{
    [Header("Region Identity")]
    public string regionId = "region_unknown";
    public string regionName = "Unknown Region";

    [Header("Optional Tags")]
    public List<string> tags = new List<string>();

    [Header("Detection")]
    [Tooltip("Only objects with this tag will set region context.")]
    public string playerTag = "Player";

    [Tooltip("If true, clears region when the player exits this volume (only if this volume is the active region).")]
    public bool clearOnExit = false;

    private void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;

        if (PlayerContext.Instance == null)
        {
            Debug.LogWarning("RegionVolume: No PlayerContext in scene. Add PlayerContext to a persistent GameObject.");
            return;
        }

        PlayerContext.Instance.SetRegion(regionId, regionName, tags);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!clearOnExit) return;
        if (!other.CompareTag(playerTag)) return;
        if (PlayerContext.Instance == null) return;

        // Only clear if this volume is still the active one.
        if (PlayerContext.Instance.SemanticRegionId == regionId)
            PlayerContext.Instance.ClearRegion();
    }
}

