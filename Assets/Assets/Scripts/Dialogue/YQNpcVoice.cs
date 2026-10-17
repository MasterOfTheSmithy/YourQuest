using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQNpcVoice : MonoBehaviour
{
    // note: Stable installed Kokoro identities; affect changes pacing only, never casting.
    public enum Preset { Alba, Anna, Javert, Marius }
    public Preset voice = Preset.Alba;
    [Range(.5f, 30f)] public float hearingRadius = 6f;
    public string VoiceId => voice.ToString().ToLowerInvariant();

    public static Preset SelectProfilePreset(string presentation, string resolvedHumanCategory, string residentSeed,
        string npcId, string role, string personality, string speakingStyle, out bool compatible)
    {
        // note: Accepted presentation outranks a visual fallback, including nonhuman bodies. Names and transient emotion are never casting inputs.
        bool female = System.Text.RegularExpressions.Regex.IsMatch(presentation ?? "", @"\b(?:female|woman|feminine)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        bool male = System.Text.RegularExpressions.Regex.IsMatch(presentation ?? "", @"\b(?:male|man|masculine)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        string category = female != male ? (female ? YQRuntimeCreatureAssetIndex.HumanFemale : YQRuntimeCreatureAssetIndex.HumanMale) : resolvedHumanCategory;
        return SelectDefaultPreset(category, residentSeed, npcId, role, personality, speakingStyle, out compatible);
    }

    public static string ForAgent(NpcDialogueAgent agent)
    {
        YQNpcVoice authored = agent != null ? Resolve(agent.gameObject) : null;
        if (authored != null && Enum.IsDefined(typeof(Preset), authored.voice)) return authored.VoiceId;
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        var record = plan?.generatedNpcs?.Find(n => n != null && n.npcId == agent?.NpcId);
        // note: Recover the materializer's canonical casting seed when metadata is missing, so re-resolution does not select a different voice for the same accepted NPC.
        string castingSeed = record != null ? plan.worldSeed + "|resident_position|" + record.npcId : "voice-profile-v1";
        Preset fallback = SelectProfilePreset(record?.presentation, "", castingSeed, agent?.NpcId,
            record?.role, record?.personality, record?.speakingStyle, out bool compatible);
        // note: Missing voice data/coverage remains visible instead of silently assigning every speaker Alba.
        Debug.LogWarning("[YQNpcVoice] Missing or invalid casting for NPC=" + agent?.NpcId + "; deterministic fallback=" + fallback + "; presentationCompatible=" + compatible);
        return fallback.ToString().ToLowerInvariant();
    }

    public static YQNpcVoice Resolve(GameObject root)
    {
        // note: Metadata may be authored on an inactive child; root takes precedence and no authored values/enabled state are changed.
        if (root == null) return null;
        YQNpcVoice own = root.GetComponent<YQNpcVoice>();
        return own != null ? own : root.GetComponentInChildren<YQNpcVoice>(true);
    }

    public static Preset SelectDefaultPreset(string resolvedHumanCategory, string residentSeed, string npcId,
        string role, string personality, string speakingStyle, out bool usesHumanPool)
    {
        // note: Voice derivation consumes no random stream and cannot change accepted identity or the existing body resolver's result.
        uint hash = HashField(2166136261u, "npc-default-voice-v1");
        hash = HashField(hash, residentSeed ?? string.Empty);
        hash = HashField(hash, npcId ?? string.Empty);
        hash = HashField(hash, NormalizeVoiceText(role));
        hash = HashField(hash, NormalizeVoiceText(personality));
        hash = HashField(hash, NormalizeVoiceText(speakingStyle));
        bool female = string.Equals(resolvedHumanCategory, YQRuntimeCreatureAssetIndex.HumanFemale, StringComparison.Ordinal);
        bool male = string.Equals(resolvedHumanCategory, YQRuntimeCreatureAssetIndex.HumanMale, StringComparison.Ordinal);
        usesHumanPool = female || male;
        if (female) return (Preset)(hash % 2u);
        if (male) return (Preset)(2u + hash % 2u);
        // note: Unknown/generic/nonhuman bodies have no qualified sex/species/age fit; retain a stable full-pool presentation fallback.
        return (Preset)(hash % 4u);
    }

    private static string NormalizeVoiceText(string value) =>
        System.Text.RegularExpressions.Regex.Replace((value ?? string.Empty).Trim().ToLowerInvariant(), @"\s+", " ");

    private static uint HashField(uint hash, string value)
    {
        // note: Length framing separates accepted fields without ambiguous concatenation or culture-dependent formatting.
        unchecked
        {
            hash ^= (uint)value.Length;
            hash *= 16777619u;
            foreach (char character in value) { hash ^= character; hash *= 16777619u; }
        }
        return hash;
    }
}
