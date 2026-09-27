using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SkillCommitter
{
    public static SkillData Commit(EmergentSkill draft, PlayerProfile profile = null)
    {
        // note: Preserve the old serialized caller while routing its live mutation to canonical PlayerState.
        return CommitCanonical(draft, profile != null ? profile.State : PlayerStateManager.Instance?.state);
    }

    public static SkillData CommitCanonical(EmergentSkill draft, PlayerState state)
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

        SkillData committedSkill = ScriptableObject.CreateInstance<SkillData>();

        committedSkill.skillId = Guid.NewGuid().ToString("N");
        committedSkill.skillName = draft.skillName;
        committedSkill.description = draft.description;
        committedSkill.type = draft.type;
        committedSkill.context = draft.context;
        committedSkill.environment = draft.environment;

        // ? carry tags forward
        committedSkill.tags = draft.contextTags;

        committedSkill.level = 1;

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

#if UNITY_EDITOR
        string folder = "Assets/GeneratedSkills/Committed";
        if (!System.IO.Directory.Exists(folder))
            System.IO.Directory.CreateDirectory(folder);

        string safeName = ToSafeFileName(committedSkill.skillName);
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{safeName}.asset");

        AssetDatabase.CreateAsset(committedSkill, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ? Register for future upgrade matching / similarity.
        EventAccumulator.Instance?.AddCommittedSkill(committedSkill);

        draft.committed = true;
        draft.committedSkillId = committedSkill.skillId;

        EditorUtility.SetDirty(draft);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SkillCommitter] Committed skill asset: {path}");
#else
        Debug.LogWarning("[SkillCommitter] Commit called at runtime; asset creation requires UNITY_EDITOR.");
#endif

        // note: Commit the accepted skill and equipment selection into the canonical player snapshot, never into a mutable profile mirror.
        if (state != null)
        {
            state.EnsureCollections();
            state.UpsertSkill(new SkillRecord
            {
                skillId = committedSkill.skillId,
                familyId = committedSkill.familyId,
                parentSkillId = committedSkill.parentSkillId,
                tier = committedSkill.tier,
                rank = 1,
                unlocked = true,
                name = committedSkill.skillName,
                description = committedSkill.description,
                type = committedSkill.type.ToString(),
                context = committedSkill.context,
                environment = committedSkill.environment,
                learnedUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            });
            state.equippedSkillBySlot[committedSkill.type.ToString()] = committedSkill.skillId;
            state.Touch();
        }

        return committedSkill;
    }

#if UNITY_EDITOR
    private static string ToSafeFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Unnamed";

        foreach (char c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');

        return name.Length > 64 ? name.Substring(0, 64) : name.Trim();
    }
#endif
}
