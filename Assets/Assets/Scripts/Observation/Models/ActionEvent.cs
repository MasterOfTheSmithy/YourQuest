using System;
using UnityEngine;

[Serializable]
public class ActionEvent
{
    // Core meaning
    public string Verb;
    public float Significance;

    // Context (safe to persist)
    public string SceneName;

    /// <summary>
    /// Region identifier used for LLM + analytics.
    /// Prefer semantic ids (e.g. region_library). Falls back to grid buckets (e.g. x0_z-1).
    /// </summary>
    public string RegionId;

    /// <summary>
    /// Human-readable region label when available (e.g. Library). Optional.
    /// </summary>
    public string RegionName;

    public Vector3 Position;

    // Optional "who/what"
    public string TargetName;
    public int TargetInstanceId;    // runtime only; useful for debugging

    // Time
    public float TimeSinceStart;    // Time.time at capture
    public long UnixTime;           // DateTimeOffset.UtcNow

    public ActionEvent(
        string verb,
        float significance,
        GameObject target = null,
        Vector3? position = null,
        string sceneName = null,
        string regionId = null,
        string regionName = null
    )
    {
        Verb = string.IsNullOrWhiteSpace(verb) ? "unknown" : verb.Trim().ToLowerInvariant();
        Significance = Mathf.Max(0f, significance);

        Position = position ?? Vector3.zero;
        SceneName = string.IsNullOrWhiteSpace(sceneName)
            ? UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
            : sceneName.Trim();

        RegionId = string.IsNullOrWhiteSpace(regionId) ? null : regionId.Trim();
        RegionName = string.IsNullOrWhiteSpace(regionName) ? null : regionName.Trim();

        if (target != null)
        {
            TargetName = target.name;
            TargetInstanceId = target.GetInstanceID();
        }
        else
        {
            TargetName = null;
            TargetInstanceId = 0;
        }

        TimeSinceStart = Time.time;
        UnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}
