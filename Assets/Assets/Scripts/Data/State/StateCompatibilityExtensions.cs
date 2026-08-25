// Assets/Assets/Scripts/Data/State/StateCompatibilityExtensions.cs
using System.Collections.Generic;

public static class StateCompatibilityExtensions
{
    public static void EnsureCollections(this PlayerState state)
    {
        state?.EnsureCollections();
    }

    public static void EnsureCollections(this WorldState state)
    {
        state?.EnsureCollections();
    }

    public static List<string> GetCanonLines(this WorldState state)
    {
        return state != null ? state.GetCanonLines() : new List<string>();
    }

    public static float GetFactionAttitudeOrDefault(this WorldState state, string factionId, float fallback = 0f)
    {
        return state != null ? state.GetFactionAttitudeOrDefault(factionId, fallback) : fallback;
    }

    public static float GetLocationImportanceOrDefault(this WorldState state, string locationId, float fallback = 0f)
    {
        return state != null ? state.GetLocationImportanceOrDefault(locationId, fallback) : fallback;
    }

    public static string GetLocationStateOrDefault(this WorldState state, string locationId, string fallback = "")
    {
        return state != null ? state.GetLocationStateOrDefault(locationId, fallback) : fallback;
    }

    public static void AddOrUpdateSkill(this PlayerState state, SkillRecord record)
    {
        state?.UpsertSkill(record);
    }
}
