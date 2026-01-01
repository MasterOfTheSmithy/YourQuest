using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Converts an EmergentSkill draft into a committed SkillData.
/// Handles:
/// - new skill creation (Tier 1)
/// - upgrade creation (Tier 2+ within same family)
/// - dynamic replacement offers via UpgradeOfferManager
/// </summary>
public static class SkillCommitter
{
    public static SkillData Commit(EmergentSkill draft, PlayerProfile profile = null)
    {
        if (draft == null)
        {
            Debug.LogWarning("[SkillCommitter] Draft is null.");
            return null;
        }

        if (draft.committed)
        {
            Debug.LogWarning($"[SkillCommitter] Draft '{draft.skillName}' already committed.");
            return null;
        }

        // -------------------------------------------------
        // Resolve upgrade target (if any)
        // -------------------------------------------------
        SkillData upgradeTarget = null;

        if (draft.isUpgradeCandidate && !string.IsNullOrWhiteSpace(draft.upgradeTargetSkillId))
        {
            var acc = EventAccumulator.Instance;
            if (acc != null)
            {
                var committed = acc.GetCommittedSkills();
                for (int i = 0; i < committed.Count; i++)
                {
                    var c = committed[i];
                    if (c != null && c.skillId == draft.upgradeTargetSkillId)
                    {
                        upgradeTarget = c;
                        break;
                    }
                }
            }
        }

        // -------------------------------------------------
        // Create committed skill
        // -------------------------------------------------
        SkillData committedSkill = ScriptableObject.CreateInstance<SkillData>();

        committedSkill.skillId = Guid.NewGuid().ToString("N");
        committedSkill.skillName = draft.skillName;
        committedSkill.description = draft.description;
        committedSkill.type = draft.type;
        committedSkill.context = draft.context;
        committedSkill.environment = draft.environment;
        committedSkill.level = 1;

        // -------------------------------------------------
        // Family + tier logic
        // -------------------------------------------------
        if (upgradeTarget != null)
        {
            committedSkill.familyId =
                !string.IsNullOrWhiteSpace(upgradeTarget.familyId)
                    ? upgradeTarget.familyId
                    : upgradeTarget.skillId;

            committedSkill.parentSkillId = upgradeTarget.skillId;
            committedSkill.tier = Mathf.Max(1, upgradeTarget.tier + 1);
        }
        else
        {
            committedSkill.familyId = Guid.NewGuid().ToString("N");
            committedSkill.parentSkillId = null;
            committedSkill.tier = 1;
        }

        // -------------------------------------------------
        // Mark draft committed
        // -------------------------------------------------
        draft.committed = true;
        draft.committedSkillId = committedSkill.skillId;

#if UNITY_EDITOR
        SaveCommittedAsset(committedSkill);
        EditorUtility.SetDirty(draft);
#endif

        // -------------------------------------------------
        // Register skill globally
        // -------------------------------------------------
        EventAccumulator.Instance?.AddCommittedSkill(committedSkill);

        // -------------------------------------------------
        // Update PlayerState (authoritative)
        // -------------------------------------------------
        PlayerStateManager.Instance?.AddOrUpdateSkillFromCommitted(committedSkill);

        // -------------------------------------------------
        // Legacy runtime profile (bridge only)
        // -------------------------------------------------
        if (profile != null)
        {
            profile.AddSkill(committedSkill.skillName);

            if (committedSkill.tier == 1)
            {
                var equipped = profile.GetEquippedSkillId(committedSkill.type);
                if (string.IsNullOrWhiteSpace(equipped))
                {
                    profile.EquipSkill(committedSkill);
                }
            }
        }

        // -------------------------------------------------
        // Dynamic replacement offer (upgrades only)
        // -------------------------------------------------
        if (upgradeTarget != null)
        {
            UpgradeOfferManager.Instance?
                .OfferReplacementIfRelevant(committedSkill, upgradeTarget);
        }

        return committedSkill;
    }

#if UNITY_EDITOR
    private static void SaveCommittedAsset(SkillData skill)
    {
        string folder = "Assets/GeneratedSkills/Committed";
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        string safeName = ToSafeFileName(skill.skillName);
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{folder}/{safeName}_T{skill.tier}.asset"
        );

        AssetDatabase.CreateAsset(skill, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static string ToSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "UnnamedSkill";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        name = name.Replace('/', '_').Replace('\\', '_').Replace(':', '_');

        return name.Length > 64 ? name.Substring(0, 64) : name.Trim();
    }
#endif
}
