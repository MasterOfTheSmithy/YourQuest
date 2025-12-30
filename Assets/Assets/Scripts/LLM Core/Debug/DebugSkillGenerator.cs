using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json; // Make sure Newtonsoft.Json is installed
#if UNITY_EDITOR
using UnityEditor;
#endif

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

public class DebugSkillGenerator : MonoBehaviour
{
    public Key generateKey = Key.K;
    public PlayerProfile playerProfile; // Optional, assign in inspector

    void Update()
    {
        if (Keyboard.current[generateKey].wasPressedThisFrame)
        {
            GenerateRandomSkill();
        }
    }

    private void GenerateRandomSkill()
    {
        // Seed data
        string[] contexts = { "combat", "exploration", "stealth", "trading", "magic dueling" };
        string[] environments = { "forest", "dungeon", "ruined city", "battlefield", "underground cave" };
        string[] types = { "Passive", "Active", "Ultimate" };

        string seedContext = contexts[UnityEngine.Random.Range(0, contexts.Length)];
        string seedEnvironment = environments[UnityEngine.Random.Range(0, environments.Length)];
        string seedType = types[UnityEngine.Random.Range(0, types.Length)];

        // LLM Prompt with explicit JSON example
        string prompt = $@"
You are generating a unique RPG skill for a Solo Leveling-style game.
Seed data:
- Type: {seedType}
- Context: {seedContext}
- Environment: {seedEnvironment}

Generate a skill with these fields ONLY in JSON format:
- skillName: a creative RPG-style name (no numbers)
- description: one-paragraph RPG-style description that fits the type, context, and environment
- type: must match the seed type
- metadata: context and environment

Example output:

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
            if (string.IsNullOrEmpty(response))
            {
                Debug.LogWarning("LLM returned empty response.");
                return;
            }

            try
            {
                // Remove possible code fences from LLM
                string cleanResponse = response.Trim();
                if (cleanResponse.StartsWith("```json"))
                {
                    cleanResponse = cleanResponse.Substring(7, cleanResponse.Length - 10).Trim();
                }

                // Parse using Newtonsoft.Json
                SkillJson json = JsonConvert.DeserializeObject<SkillJson>(cleanResponse);

                // Fallback only if necessary
                string skillName = !string.IsNullOrEmpty(json.skillName)
                    ? json.skillName
                    : $"Skill_{Guid.NewGuid().ToString("N").Substring(0, 6)}";

                string description = !string.IsNullOrEmpty(json.description)
                    ? json.description
                    : $"A procedurally generated {seedType} skill in {seedContext}/{seedEnvironment}.";

                string context = (json.metadata != null && !string.IsNullOrEmpty(json.metadata.context))
                    ? json.metadata.context
                    : seedContext;

                string environment = (json.metadata != null && !string.IsNullOrEmpty(json.metadata.environment))
                    ? json.metadata.environment
                    : seedEnvironment;

                SkillType type = Enum.TryParse(json.type, out SkillType t) ? t : SkillType.Active;

                // Create ScriptableObject
                SkillData skillAsset = ScriptableObject.CreateInstance<SkillData>();
                skillAsset.skillName = skillName;
                skillAsset.description = description;
                skillAsset.type = type;
                skillAsset.context = context;
                skillAsset.environment = environment;

#if UNITY_EDITOR
                string folder = "Assets/GeneratedSkills";
                if (!System.IO.Directory.Exists(folder)) System.IO.Directory.CreateDirectory(folder);
                string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{skillAsset.skillName}.asset");
                AssetDatabase.CreateAsset(skillAsset, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
#endif

                // --- Add to EventAccumulator ---
                EventAccumulator.Instance?.AddSkill(skillAsset);

                // --- Add to PlayerProfile ---
                if (playerProfile != null)
                    playerProfile.AddSkill(skillName);

                Debug.Log($"[Generated Skill] {skillAsset.skillName} ({skillAsset.type}) - {skillAsset.context}/{skillAsset.environment}\n{skillAsset.description}");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to parse LLM response: " + e);
            }
        });
    }
}
