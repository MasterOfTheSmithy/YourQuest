using UnityEngine;

// note: One shipped, editable character profile supplies every Goddess prompt; there is no embedded sample-dialogue fallback.
public static class YQGoddessAgentProfile
{
    private static TextAsset profile;
    public static string Text
    {
        get
        {
            if (profile == null) profile = Resources.Load<TextAsset>("GoddessAgentProfile");
            return profile != null ? profile.text.Trim() : string.Empty;
        }
    }
    public static string Hash => YQRepairEpisode.Hash(Text);
}
