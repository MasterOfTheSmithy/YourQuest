using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQAuthoredSiteSourceDiscovery
{
    public const string CatalogPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQAuthoredSiteSourceCatalog.asset";

    private static bool _delayedSyncQueued;

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Asset Intake/Detect New Authored Environment Packs")]
    public static void DetectNewAuthoredEnvironmentPacks()
    {
        SyncCatalog(true);
    }

    public static YQAuthoredSiteSourceCatalog SyncCatalog(bool logResult)
    {
        EnsureFolderPath(
            "Assets/Assets/GeneratedAssets/WorldIntake");
        YQAuthoredSiteSourceCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQAuthoredSiteSourceCatalog>(CatalogPath);

        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<YQAuthoredSiteSourceCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        List<YQAuthoredSiteSourceRecord> detected = DetectRecords();
        catalog.ApplyDetection(detected);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();

        if (logResult)
        {
            int buildable = detected.Count(record =>
                !string.IsNullOrWhiteSpace(record.selectedScenePath));
            int needsScene = detected.Count - buildable;
            Debug.Log(
                "[YQAuthoredSiteSourceDiscovery] AUTHORED PACK DETECTION COMPLETE\n" +
                "Environment bundles: " + detected.Count + "\n" +
                "Buildable authored sources: " + buildable + "\n" +
                "Needs authored scene: " + needsScene + "\n" +
                "Catalog: " + CatalogPath);
        }

        return catalog;
    }

    public static void ScheduleAutomaticSync()
    {
        if (_delayedSyncQueued)
            return;

        _delayedSyncQueued = true;
        EditorApplication.delayCall += RunDelayedSync;
    }

    private static void RunDelayedSync()
    {
        _delayedSyncQueued = false;

        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            ScheduleAutomaticSync();
            return;
        }

        // note: Import detection updates only lightweight metadata; it never opens scenes or starts heavy extraction automatically.
        SyncCatalog(false);
    }

    private static List<YQAuthoredSiteSourceRecord> DetectRecords()
    {
        List<string> packRoots = DetectPackRoots();
        List<YQAuthoredSiteSourceRecord> result =
            new List<YQAuthoredSiteSourceRecord>();

        for (int index = 0; index < packRoots.Count; index++)
        {
            string root = packRoots[index];
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { root });
            List<string> scenes = sceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            string selectedScene = SelectBestAuthoredScene(scenes);
            string folderName = root.Substring(root.LastIndexOf('/') + 1);
            string kitId = ToSnakeCase(folderName);
            YQAuthoredSiteKind siteKind = InferSiteKind(root + "/" + selectedScene);

            // note: Content libraries may contain demonstration scenes and prefabs, but they are not authored world locations and must never enter site extraction.
            if (LooksLikeNonWorldContent(root) || siteKind == YQAuthoredSiteKind.Unknown)
            {
                selectedScene = string.Empty;
            }

            result.Add(
                new YQAuthoredSiteSourceRecord
                {
                    kitId = kitId,
                    displayName = SplitDisplayName(folderName),
                    assetRoot = root,
                    selectedScenePath = selectedScene,
                    discoveredScenePaths = scenes,
                    siteKind = siteKind,
                    forceUrpConversion =
                        selectedScene.IndexOf("HDRP", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        selectedScene.IndexOf("URP", StringComparison.OrdinalIgnoreCase) < 0,
                    sourceSignature = BuildSourceSignature(scenes),
                    state = string.IsNullOrWhiteSpace(selectedScene)
                        ? YQAuthoredSiteSourceState.NeedsAuthoredScene
                        : YQAuthoredSiteSourceState.DetectedPendingBuild
                });
        }

        return result;
    }

    private static List<string> DetectPackRoots()
    {
        List<string> result = new List<string>();
        AddImmediateSubfolders("Assets/BefourStudios", result);
        AddImmediateSubfolders("Assets/HIVEMIND", result);

        // note: Hivemind stores The Messenger Mountain one level below a generic HDRP folder; promote that real bundle root instead of cataloguing the container.
        result.RemoveAll(path =>
            string.Equals(path, "Assets/HIVEMIND/HDRP", StringComparison.OrdinalIgnoreCase));
        AddImmediateSubfolders("Assets/HIVEMIND/HDRP", result);

        string[] topLevelFolders = AssetDatabase.GetSubFolders("Assets");

        for (int index = 0; index < topLevelFolders.Length; index++)
        {
            string folder = topLevelFolders[index].Replace('\\', '/');

            if (IsExcludedProjectFolder(folder) ||
                string.Equals(folder, "Assets/BefourStudios", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(folder, "Assets/HIVEMIND", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // note: A third-party top-level folder becomes an authored-site bundle only when it actually contains both a scene and reusable prefabs.
            if (AssetDatabase.FindAssets("t:Scene", new[] { folder }).Length > 0 &&
                AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Length > 0)
            {
                result.Add(folder);
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool IsExcludedProjectFolder(string folder)
    {
        string[] excluded =
        {
            "Assets/Assets",
            "Assets/Scenes",
            "Assets/Settings",
            "Assets/TutorialInfo",
            "Assets/Editor Default Resources",
            "Assets/TextMesh Pro",
            "Assets/StarterAssets"
        };

        for (int index = 0; index < excluded.Length; index++)
        {
            if (string.Equals(folder, excluded[index], StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static void AddImmediateSubfolders(
        string parent,
        List<string> destination)
    {
        if (!AssetDatabase.IsValidFolder(parent))
            return;

        string[] subfolders = AssetDatabase.GetSubFolders(parent);

        for (int index = 0; index < subfolders.Length; index++)
        {
            destination.Add(subfolders[index].Replace('\\', '/'));
        }
    }

    private static string SelectBestAuthoredScene(List<string> scenes)
    {
        string best = string.Empty;
        int bestScore = int.MinValue;

        for (int index = 0; index < scenes.Count; index++)
        {
            string path = scenes[index];
            string lower = path.ToLowerInvariant();
            int score = 0;
            if (lower.Contains("urp")) score += 220;
            if (lower.Contains("main")) score += 80;
            if (lower.Contains("demo")) score += 75;
            if (lower.Contains("showcase")) score += 70;
            if (lower.Contains("village")) score += 65;
            if (lower.Contains("town")) score += 65;
            if (lower.Contains("hospital")) score += 60;
            if (lower.Contains("sewer")) score += 60;
            if (lower.Contains("island")) score += 60;
            if (lower.Contains("overview")) score -= 180;
            if (lower.Contains("weapon")) score -= 300;
            if (lower.Contains("vfx")) score -= 300;
            if (lower.Contains("sample")) score -= 300;

            if (score > bestScore)
            {
                bestScore = score;
                best = path;
            }
        }

        return bestScore <= -250 ? string.Empty : best;
    }

    private static YQAuthoredSiteKind InferSiteKind(string text)
    {
        string lower = (text ?? string.Empty).ToLowerInvariant();
        if (lower.Contains("cyber") || lower.Contains("scifi") || lower.Contains("container")) return YQAuthoredSiteKind.SciFiSite;
        if (lower.Contains("dungeon") || lower.Contains("sewer") || lower.Contains("tomb") || lower.Contains("depth")) return YQAuthoredSiteKind.Dungeon;
        if (lower.Contains("hospital") || lower.Contains("house") || lower.Contains("mansion") || lower.Contains("room")) return YQAuthoredSiteKind.Interior;
        if (lower.Contains("camp")) return YQAuthoredSiteKind.Camp;
        if (lower.Contains("mountain")) return YQAuthoredSiteKind.Wilderness;
        if (lower.Contains("temple") || lower.Contains("cathedral") || lower.Contains("arena") || lower.Contains("ruin") || lower.Contains("forge")) return YQAuthoredSiteKind.Landmark;
        if (lower.Contains("village") || lower.Contains("town") || lower.Contains("kingdom") || lower.Contains("district") || lower.Contains("island") || lower.Contains("dynasty") || lower.Contains("empire")) return YQAuthoredSiteKind.Settlement;
        if (lower.Contains("environment") || lower.Contains("forest") || lower.Contains("desert")) return YQAuthoredSiteKind.Wilderness;
        return YQAuthoredSiteKind.Unknown;
    }

    private static bool LooksLikeNonWorldContent(string path)
    {
        string lower = (path ?? string.Empty).ToLowerInvariant();
        string[] nonWorldTokens =
        {
            "weapon", "vfx", "character", "creature", "monster",
            "terrain tools", "bushes", "forst", "magic pig games"
        };

        for (int index = 0; index < nonWorldTokens.Length; index++)
        {
            if (lower.Contains(nonWorldTokens[index]))
                return true;
        }

        return false;
    }

    private static string BuildSourceSignature(List<string> scenes)
    {
        StringBuilder canonical = new StringBuilder();

        for (int index = 0; index < scenes.Count; index++)
        {
            canonical.Append(AssetDatabase.AssetPathToGUID(scenes[index]));
            canonical.Append(':');
            canonical.Append(AssetDatabase.GetAssetDependencyHash(scenes[index]).ToString());
            canonical.Append('|');
        }

        uint hash = 2166136261u;
        string value = canonical.ToString();

        for (int index = 0; index < value.Length; index++)
        {
            hash ^= value[index];
            hash *= 16777619u;
        }

        return "fnv1a32_" + hash.ToString("x8");
    }

    private static string ToSnakeCase(string value)
    {
        StringBuilder result = new StringBuilder();

        for (int index = 0; index < (value ?? string.Empty).Length; index++)
        {
            char character = value[index];

            if (char.IsUpper(character) && index > 0 &&
                result.Length > 0 && result[result.Length - 1] != '_')
            {
                result.Append('_');
            }

            result.Append(char.IsLetterOrDigit(character)
                ? char.ToLowerInvariant(character)
                : '_');
        }

        return result.ToString().Trim('_');
    }

    private static string SplitDisplayName(string value)
    {
        StringBuilder result = new StringBuilder();

        for (int index = 0; index < (value ?? string.Empty).Length; index++)
        {
            char character = value[index];

            if (index > 0 && char.IsUpper(character) &&
                !char.IsWhiteSpace(value[index - 1]))
            {
                result.Append(' ');
            }

            result.Append(character);
        }

        return result.ToString();
    }

    private static void EnsureFolderPath(string folderPath)
    {
        string[] segments = folderPath.Replace('\\', '/').Split('/');
        string current = segments[0];

        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[index]);
            current = next;
        }
    }
}

public sealed class YQAuthoredSiteAssetImportWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (ContainsRelevantWorldAsset(importedAssets) ||
            ContainsRelevantWorldAsset(deletedAssets) ||
            ContainsRelevantWorldAsset(movedAssets) ||
            ContainsRelevantWorldAsset(movedFromAssetPaths))
        {
            YQAuthoredSiteSourceDiscovery.ScheduleAutomaticSync();
        }
    }

    private static bool ContainsRelevantWorldAsset(string[] paths)
    {
        for (int index = 0; index < (paths?.Length ?? 0); index++)
        {
            string path = paths[index] ?? string.Empty;
            bool candidateAssetRoot =
                path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/Assets/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/Scenes/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/Settings/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/TutorialInfo/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("Assets/TextMesh Pro/", StringComparison.OrdinalIgnoreCase);

            if (candidateAssetRoot &&
                (path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                 path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
