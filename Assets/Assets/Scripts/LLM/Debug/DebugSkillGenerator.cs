using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json;
#if UNITY_EDITOR
using UnityEditor;
#endif

#region JSON DTOs

[Serializable]
public class SkillMetadataJson
{
    public string context;
    public string environment;
}

[Serializable]
public class SkillJson
{
    public string skillName;
    public string description;
    public string type;
    public SkillMetadataJson metadata;
}

#endregion

public class DebugSkillGenerator : MonoBehaviour
{
    [Header("Debug Keys")]
    public Key generateKey = Key.K;   // Generate ghost skill
    public Key commitKey = Key.L;     // Commit latest ghost skill

    [Header("Optional References")]
    public PlayerProfile playerProfile; // used only on commit

    void Update()
    {
        if (Keyboard.current == null) return;

        if (Keyboard.current[generateKey].wasPressedThisFrame)
            GenerateRandomSkillDraft();

        if (Keyboard.current[commitKey].wasPressedThisFrame)
            CommitLatestGhostSkill();
    }

    #region GENERATION

    private void GenerateRandomSkillDraft()
    {
        string[] contexts = { "combat", "exploration", "stealth", "trading", "magic dueling" };
        string[] environments = { "forest", "dungeon", "ruined city", "battlefield", "underground cave" };
        string[] types = { "Passive", "Active", "Ultimate" };

        string seedContext = contexts[UnityEngine.Random.Range(0, contexts.Length)];
        string seedEnvironment = environments[UnityEngine.Random.Range(0, environments.Length)];
        string seedType = types[UnityEngine.Random.Range(0, types.Length)];

        string prompt = $@"
You are generating a unique RPG skill for a Solo Leveling-style game.

Seed:
- Type: {seedType}
- Context: {seedContext}
- Environment: {seedEnvironment}

Return ONLY JSON with these fields:
- skillName (no numbers)
- description (one paragraph)
- type (must match seed type exactly)
- metadata {{ context, environment }}

Example:
{{
  ""skillName"": ""Flame Strike"",
  ""description"": ""A fiery strike that burns enemies in the dungeon."",
  ""type"": ""Active"",
  ""metadata"": {{
      ""context"": ""combat"",
      ""environment"": ""dungeon""
  }}
}}
";

        LLMClient.Instance.GenerateSkill(prompt, response =>
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                Debug.LogWarning("[LLM] Empty response.");
                return;
            }

            try
            {
                string jsonText = ExtractFirstJsonObject(response);
                SkillJson json = JsonConvert.DeserializeObject<SkillJson>(jsonText);

                string skillName = !string.IsNullOrWhiteSpace(json?.skillName)
                    ? json.skillName.Trim()
                    : $"Skill_{Guid.NewGuid():N}".Substring(0, 12);

                string description = !string.IsNullOrWhiteSpace(json?.description)
                    ? json.description.Trim()
                    : $"A procedurally generated {seedType} skill.";

                string context = !string.IsNullOrWhiteSpace(json?.metadata?.context)
                    ? json.metadata.context.Trim()
                    : seedContext;

                string environment = !string.IsNullOrWhiteSpace(json?.metadata?.environment)
                    ? json.metadata.environment.Trim()
                    : seedEnvironment;

                SkillType type = Enum.TryParse(json?.type, true, out SkillType parsed)
                    ? parsed
                    : SkillType.Active;

                // Create ghost draft
                EmergentSkill draft = ScriptableObject.CreateInstance<EmergentSkill>();
                draft.draftId = Guid.NewGuid().ToString("N");
                draft.skillName = skillName;
                draft.description = description;
                draft.type = type;
                draft.context = context;
                draft.environment = environment;
                draft.committed = false;
                draft.createdUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

#if UNITY_EDITOR
                SaveDraftAsset(draft);
#endif

                EventAccumulator.Instance?.AddGhostSkillOrUpgradeCandidate(draft);


                Debug.Log(
                    $"[GHOST SKILL CREATED]\n" +
                    $"{draft.skillName} ({draft.type})\n" +
                    $"{draft.context} / {draft.environment}\n\n" +
                    $"{draft.description}"
                );
            }
            catch (Exception e)
            {
                Debug.LogError("[LLM PARSE FAILED]\n" + e + "\n\nRAW:\n" + response);
            }
        });
    }

    #endregion

    #region COMMIT

    private void CommitLatestGhostSkill()
    {
        var accumulator = EventAccumulator.Instance;
        if (accumulator == null)
        {
            Debug.LogWarning("[Commit] No EventAccumulator.");
            return;
        }

        var ghosts = accumulator.GetGhostSkills();
        if (ghosts == null || ghosts.Count == 0)
        {
            Debug.Log("[Commit] No ghost skills available.");
            return;
        }

        EmergentSkill candidate = null;
        for (int i = ghosts.Count - 1; i >= 0; i--)
        {
            if (!ghosts[i].committed)
            {
                candidate = ghosts[i];
                break;
            }
        }

        if (candidate == null)
        {
            Debug.Log("[Commit] All ghost skills already committed.");
            return;
        }

        SkillData committed = SkillCommitter.Commit(candidate, playerProfile);

        if (committed != null)
        {
            Debug.Log(
                $"[SKILL COMMITTED]\n" +
                $"{candidate.skillName} ? SkillData\n" +
                $"Type: {committed.type}\n" +
                $"Context: {committed.context} | {committed.environment}"
            );
        }
        else
        {
            Debug.LogWarning("[Commit] Commit failed.");
        }
    }

    #endregion

    #region HELPERS

    private static string ExtractFirstJsonObject(string text)
    {
        string stripped = Regex.Replace(
            text.Trim(),
            @"^```(?:json)?\s*|\s*```$",
            "",
            RegexOptions.IgnoreCase
        ).Trim();

        int start = stripped.IndexOf('{');
        if (start < 0) throw new FormatException("No JSON object found.");

        int depth = 0;
        for (int i = start; i < stripped.Length; i++)
        {
            if (stripped[i] == '{') depth++;
            else if (stripped[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return stripped.Substring(start, i - start + 1);
            }
        }

        throw new FormatException("Unbalanced JSON braces.");
    }

#if UNITY_EDITOR
    private static void SaveDraftAsset(EmergentSkill draft)
    {
        string folder = "Assets/GeneratedSkills/Drafts";
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        string safeName = ToSafeFileName(draft.skillName);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.asset");

        AssetDatabase.CreateAsset(draft, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ToSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unnamed";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name.Length > 64 ? name.Substring(0, 64) : name.Trim();
    }
#endif

    #endregion
}

