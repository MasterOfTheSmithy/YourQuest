using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQVikingGoldenSourceAnalyzer
{
    public const string SourceScenePath =
        "Assets/BefourStudios/MedievalVikingVillage/Art/Scenes/VillageMapURP.unity";

    public const string ReportPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQVikingGoldenSourceReport.md";

    public const string SpatialSnapshotPath =
        "Assets/Assets/GeneratedAssets/WorldIntake/YQVikingAuthoredInstanceSnapshot.csv";

    private sealed class HierarchySummary
    {
        public string hierarchyPath;
        public int gameObjectCount;
        public int rendererCount;
        public int prefabInstanceRootCount;
        public Vector3 boundsSize;
    }

    private sealed class PrefabInstanceSnapshot
    {
        public string hierarchyPath;
        public string sourcePrefabPath;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Analyze Authored Viking Scene")]
    public static void AnalyzeAuthoredVikingScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingGoldenSourceAnalyzer] " +
                "Scene analysis requires stable Edit mode.");

            return;
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                SourceScenePath) == null)
        {
            Debug.LogError(
                "[YQVikingGoldenSourceAnalyzer] " +
                "Authored URP scene is missing: " +
                SourceScenePath);

            return;
        }

        Scene sourceScene =
            SceneManager.GetSceneByPath(
                SourceScenePath);

        bool openedByAnalyzer =
            !sourceScene.IsValid() ||
            !sourceScene.isLoaded;

        try
        {
            // note: Load the vendor scene additively and read-only so the current YourQuest scene and imported source layout remain untouched.
            if (openedByAnalyzer)
            {
                sourceScene =
                    EditorSceneManager.OpenScene(
                        SourceScenePath,
                        OpenSceneMode.Additive);
            }

            WriteAnalysisReport(
                sourceScene);

            AssetDatabase.Refresh();

            UnityEngine.Object reportAsset =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                    ReportPath);

            if (reportAsset != null)
            {
                Selection.activeObject =
                    reportAsset;
            }

            Debug.Log(
                "[YQVikingGoldenSourceAnalyzer] ANALYSIS COMPLETE\n" +
                "Source: " +
                SourceScenePath +
                "\nReport: " +
                ReportPath +
                "\nSpatial snapshot: " +
                SpatialSnapshotPath);
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
        finally
        {
            if (openedByAnalyzer &&
                sourceScene.IsValid() &&
                sourceScene.isLoaded)
            {
                // note: Close only the additive scene opened by this tool; never close or save a scene the designer already had open.
                EditorSceneManager.CloseScene(
                    sourceScene,
                    true);
            }
        }
    }

    private static void WriteAnalysisReport(
        Scene sourceScene)
    {
        GameObject[] roots =
            sourceScene.GetRootGameObjects();

        List<HierarchySummary> rootSummaries =
            new List<HierarchySummary>();

        List<HierarchySummary> directChildSummaries =
            new List<HierarchySummary>();

        Dictionary<string, int> prefabSourceUsage =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        List<PrefabInstanceSnapshot> spatialSnapshots =
            new List<PrefabInstanceSnapshot>();

        int totalGameObjects = 0;
        int totalRenderers = 0;
        int totalPrefabRoots = 0;

        for (int rootIndex = 0;
             rootIndex < roots.Length;
             rootIndex++)
        {
            GameObject root =
                roots[rootIndex];

            if (root == null)
                continue;

            HierarchySummary rootSummary =
                BuildHierarchySummary(
                    root.transform,
                    root.name,
                    prefabSourceUsage);

            rootSummaries.Add(
                rootSummary);

            totalGameObjects +=
                rootSummary.gameObjectCount;

            totalRenderers +=
                rootSummary.rendererCount;

            totalPrefabRoots +=
                rootSummary.prefabInstanceRootCount;

            if (string.Equals(
                    root.name,
                    "Meshes",
                    StringComparison.OrdinalIgnoreCase))
            {
                // note: Capture authored mesh-instance transforms separately; vegetation roots are intentionally excluded from WG2 building reconstruction.
                CollectSpatialSnapshots(
                    root.transform,
                    spatialSnapshots);
            }

            for (int childIndex = 0;
                 childIndex < root.transform.childCount;
                 childIndex++)
            {
                Transform child =
                    root.transform.GetChild(
                        childIndex);

                HierarchySummary childSummary =
                    BuildHierarchySummary(
                        child,
                        root.name +
                        "/" +
                        child.name,
                        null);

                if (childSummary.rendererCount > 0 ||
                    childSummary.prefabInstanceRootCount > 0)
                {
                    directChildSummaries.Add(
                        childSummary);
                }
            }
        }

        rootSummaries.Sort(
            CompareHierarchySummaries);

        directChildSummaries.Sort(
            CompareHierarchySummaries);

        List<KeyValuePair<string, int>> prefabUsage =
            new List<KeyValuePair<string, int>>(
                prefabSourceUsage);

        prefabUsage.Sort(
            (left, right) =>
            {
                int countComparison =
                    right.Value.CompareTo(
                        left.Value);

                return countComparison != 0
                    ? countComparison
                    : string.Compare(
                        left.Key,
                        right.Key,
                        StringComparison.OrdinalIgnoreCase);
            });

        StringBuilder report =
            new StringBuilder();

        report.AppendLine(
            "# Viking Authored Golden Source Analysis");

        report.AppendLine();
        report.AppendLine(
            "- Source scene: `" +
            SourceScenePath +
            "`");

        report.AppendLine(
            "- Analyzed UTC: `" +
            DateTime.UtcNow.ToString("O") +
            "`");

        report.AppendLine(
            "- Root objects: " +
            roots.Length);

        report.AppendLine(
            "- GameObjects: " +
            totalGameObjects);

        report.AppendLine(
            "- Renderers: " +
            totalRenderers);

        report.AppendLine(
            "- Prefab instance roots: " +
            totalPrefabRoots);

        report.AppendLine();
        report.AppendLine(
            "## Root hierarchy groups");

        AppendHierarchyTable(
            report,
            rootSummaries,
            int.MaxValue);

        report.AppendLine();
        report.AppendLine(
            "## Direct-child composition groups");

        report.AppendLine();
        report.AppendLine(
            "These groups are the first candidates for project-owned building, parcel, street, and landmark wrappers. They are evidence only and are not automatically promoted.");

        AppendHierarchyTable(
            report,
            directChildSummaries,
            300);

        report.AppendLine();
        report.AppendLine(
            "## Prefab source usage");

        report.AppendLine();
        report.AppendLine(
            "| Instances | Source prefab |");

        report.AppendLine(
            "|---:|---|");

        int prefabLimit =
            Mathf.Min(
                prefabUsage.Count,
                300);

        for (int index = 0;
             index < prefabLimit;
             index++)
        {
            report.Append("| ");
            report.Append(prefabUsage[index].Value);
            report.Append(" | `");
            report.Append(EscapeMarkdown(prefabUsage[index].Key));
            report.AppendLine("` |");
        }

        string reportFolder =
            Path.GetDirectoryName(
                ReportPath);

        if (!string.IsNullOrWhiteSpace(reportFolder))
        {
            Directory.CreateDirectory(
                reportFolder);
        }

        // note: This report is deterministic evidence for selecting WG2 extraction boundaries; it does not modify or serialize the vendor scene.
        File.WriteAllText(
            ReportPath,
            report.ToString(),
            Encoding.UTF8);

        WriteSpatialSnapshot(
            spatialSnapshots);
    }

    private static void CollectSpatialSnapshots(
        Transform meshRoot,
        List<PrefabInstanceSnapshot> destination)
    {
        Transform[] transforms =
            meshRoot.GetComponentsInChildren<Transform>(
                true);

        for (int index = 0;
             index < transforms.Length;
             index++)
        {
            Transform current =
                transforms[index];

            if (current == null ||
                !PrefabUtility.IsOutermostPrefabInstanceRoot(
                    current.gameObject))
            {
                continue;
            }

            string sourcePath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    current.gameObject);

            if (string.IsNullOrWhiteSpace(sourcePath))
                continue;

            Renderer[] renderers =
                current.GetComponentsInChildren<Renderer>(
                    true);

            bool hasBounds = false;
            Bounds bounds = default;

            for (int rendererIndex = 0;
                 rendererIndex < renderers.Length;
                 rendererIndex++)
            {
                Renderer renderer =
                    renderers[rendererIndex];

                if (renderer == null)
                    continue;

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(
                        renderer.bounds);
                }
            }

            destination.Add(
                new PrefabInstanceSnapshot
                {
                    hierarchyPath =
                        BuildHierarchyPath(
                            meshRoot,
                            current),
                    sourcePrefabPath = sourcePath,
                    position = current.position,
                    rotation = current.eulerAngles,
                    scale = current.lossyScale,
                    boundsCenter = hasBounds
                        ? bounds.center
                        : current.position,
                    boundsSize = hasBounds
                        ? bounds.size
                        : Vector3.zero
                });
        }

        destination.Sort(
            (left, right) =>
                string.Compare(
                    left.hierarchyPath,
                    right.hierarchyPath,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildHierarchyPath(
        Transform root,
        Transform child)
    {
        List<string> parts =
            new List<string>();

        Transform current = child;

        while (current != null &&
               current != root)
        {
            parts.Add(
                current.name);

            current = current.parent;
        }

        parts.Add(
            root.name);

        parts.Reverse();

        return string.Join(
            "/",
            parts);
    }

    private static void WriteSpatialSnapshot(
        List<PrefabInstanceSnapshot> snapshots)
    {
        StringBuilder csv =
            new StringBuilder();

        csv.AppendLine(
            "hierarchy_path,source_prefab_path,position_x,position_y,position_z,rotation_x,rotation_y,rotation_z,scale_x,scale_y,scale_z,bounds_center_x,bounds_center_y,bounds_center_z,bounds_size_x,bounds_size_y,bounds_size_z");

        for (int index = 0;
             index < snapshots.Count;
             index++)
        {
            PrefabInstanceSnapshot snapshot =
                snapshots[index];

            AppendCsvText(csv, snapshot.hierarchyPath);
            AppendCsvText(csv, snapshot.sourcePrefabPath);
            AppendCsvNumber(csv, snapshot.position.x);
            AppendCsvNumber(csv, snapshot.position.y);
            AppendCsvNumber(csv, snapshot.position.z);
            AppendCsvNumber(csv, snapshot.rotation.x);
            AppendCsvNumber(csv, snapshot.rotation.y);
            AppendCsvNumber(csv, snapshot.rotation.z);
            AppendCsvNumber(csv, snapshot.scale.x);
            AppendCsvNumber(csv, snapshot.scale.y);
            AppendCsvNumber(csv, snapshot.scale.z);
            AppendCsvNumber(csv, snapshot.boundsCenter.x);
            AppendCsvNumber(csv, snapshot.boundsCenter.y);
            AppendCsvNumber(csv, snapshot.boundsCenter.z);
            AppendCsvNumber(csv, snapshot.boundsSize.x);
            AppendCsvNumber(csv, snapshot.boundsSize.y);
            csv.Append(
                snapshot.boundsSize.z.ToString(
                    "R",
                    CultureInfo.InvariantCulture));

            csv.AppendLine();
        }

        // note: The machine-readable snapshot lets WG2 reconstruct authored spatial relationships without serializing or mutating the vendor scene.
        File.WriteAllText(
            SpatialSnapshotPath,
            csv.ToString(),
            Encoding.UTF8);
    }

    private static void AppendCsvText(
        StringBuilder csv,
        string value)
    {
        csv.Append('"');
        csv.Append(
            (value ?? string.Empty)
                .Replace("\"", "\"\""));
        csv.Append("\",");
    }

    private static void AppendCsvNumber(
        StringBuilder csv,
        float value)
    {
        csv.Append(
            value.ToString(
                "R",
                CultureInfo.InvariantCulture));

        csv.Append(',');
    }

    private static HierarchySummary BuildHierarchySummary(
        Transform root,
        string hierarchyPath,
        Dictionary<string, int> prefabSourceUsage)
    {
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(
                true);

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        int prefabRootCount = 0;

        for (int index = 0;
             index < transforms.Length;
             index++)
        {
            GameObject current =
                transforms[index] != null
                    ? transforms[index].gameObject
                    : null;

            if (current == null ||
                !PrefabUtility.IsAnyPrefabInstanceRoot(
                    current))
            {
                continue;
            }

            prefabRootCount++;

            if (prefabSourceUsage == null)
                continue;

            string prefabPath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    current);

            if (string.IsNullOrWhiteSpace(prefabPath))
                continue;

            prefabSourceUsage.TryGetValue(
                prefabPath,
                out int usageCount);

            prefabSourceUsage[prefabPath] =
                usageCount + 1;
        }

        bool hasBounds =
            false;

        Bounds bounds =
            default;

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Renderer renderer =
                renderers[rendererIndex];

            if (renderer == null)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(
                    renderer.bounds);
            }
        }

        return new HierarchySummary
        {
            hierarchyPath = hierarchyPath,
            gameObjectCount = transforms.Length,
            rendererCount = renderers.Length,
            prefabInstanceRootCount = prefabRootCount,
            boundsSize = hasBounds
                ? bounds.size
                : Vector3.zero
        };
    }

    private static int CompareHierarchySummaries(
        HierarchySummary left,
        HierarchySummary right)
    {
        int prefabComparison =
            right.prefabInstanceRootCount.CompareTo(
                left.prefabInstanceRootCount);

        if (prefabComparison != 0)
            return prefabComparison;

        int rendererComparison =
            right.rendererCount.CompareTo(
                left.rendererCount);

        return rendererComparison != 0
            ? rendererComparison
            : string.Compare(
                left.hierarchyPath,
                right.hierarchyPath,
                StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendHierarchyTable(
        StringBuilder report,
        List<HierarchySummary> summaries,
        int maximumRows)
    {
        report.AppendLine();
        report.AppendLine(
            "| Hierarchy | Objects | Renderers | Prefab roots | Bounds (m) |");

        report.AppendLine(
            "|---|---:|---:|---:|---:|");

        int rowCount =
            Mathf.Min(
                summaries.Count,
                maximumRows);

        for (int index = 0;
             index < rowCount;
             index++)
        {
            HierarchySummary summary =
                summaries[index];

            report.Append("| `");
            report.Append(EscapeMarkdown(summary.hierarchyPath));
            report.Append("` | ");
            report.Append(summary.gameObjectCount);
            report.Append(" | ");
            report.Append(summary.rendererCount);
            report.Append(" | ");
            report.Append(summary.prefabInstanceRootCount);
            report.Append(" | ");
            report.Append(FormatVector(summary.boundsSize));
            report.AppendLine(" |");
        }
    }

    private static string FormatVector(
        Vector3 value)
    {
        return value.x.ToString("0.0") +
               " x " +
               value.y.ToString("0.0") +
               " x " +
               value.z.ToString("0.0");
    }

    private static string EscapeMarkdown(
        string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("|", "\\|");
    }
}
