using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: This editor-only pass verifies that prefabs rebound to generated URP materials are represented by the existing procedural discovery catalog.
[InitializeOnLoad]
internal static class YQUrpPaletteIntegration
{
    private const string VariantFolder = "Assets/Assets/GeneratedAssets/MaterialVariants/URP";
    private const string CatalogPath = "Assets/Assets/Resources/YQDiscoveredWorldAssetCatalog.asset";
    private const string ReportPath = "Logs/YQUrpPaletteIntegration.txt";
    private const string SessionKey = "YQUrpPaletteIntegration_v4";
    private const int PrefabsPerTick = 32;

    private static readonly string[] ExcludedTerms =
    {
        "weapon", "sword", "dagger", "axe", "bow", "staff", "armor", "helmet",
        "particle", "particles", "audio", "sound", "animation", "animator", "demo",
        "showcase", "example", "preview", "camera", "controller", "canvas", "ui"
    };

    private static readonly HashSet<string> ConvertedMaterialPaths =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ApprovedPrefabRoots =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly List<string> AffectedPrefabPaths =
        new List<string>();

    private static string[] _prefabGuids;
    private static int _prefabIndex;
    private static bool _running;
    private static int _excludedCount;

    [MenuItem("Tools/YourQuest/Materials/Verify Converted Assets in Procedural Palettes")]
    private static void VerifyNow()
    {
        if (_running)
            return;

        SessionState.EraseBool(SessionKey);
        Start();
    }

    private static void Start()
    {
        string[] convertedMaterials = AssetDatabase.FindAssets("t:Material", new[] { VariantFolder });
        if (convertedMaterials == null || convertedMaterials.Length == 0)
        {
            SessionState.SetBool(SessionKey, true);
            return;
        }

        ConvertedMaterialPaths.Clear();
        ApprovedPrefabRoots.Clear();
        string[] configuredRoots = YQRuntimeWorldAssetRegistryBuilder.GetConfiguredPrefabDiscoveryRoots();
        for (int i = 0; i < configuredRoots.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(configuredRoots[i]))
                ApprovedPrefabRoots.Add(configuredRoots[i].Replace('\\', '/').TrimEnd('/') + "/");
        }

        for (int i = 0; i < convertedMaterials.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(convertedMaterials[i]);
            if (!string.IsNullOrWhiteSpace(path))
                ConvertedMaterialPaths.Add(path.Replace('\\', '/'));
        }

        AffectedPrefabPaths.Clear();
        _excludedCount = 0;
        _prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        _prefabIndex = 0;
        _running = true;
        EditorApplication.update += ProcessTick;
    }

    private static void ProcessTick()
    {
        if (!_running)
            return;

        int processed = 0;
        while (_prefabGuids != null && _prefabIndex < _prefabGuids.Length && processed++ < PrefabsPerTick)
        {
            string path = AssetDatabase.GUIDToAssetPath(_prefabGuids[_prefabIndex++]);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string normalizedPath = path.Replace('\\', '/');
            if (!IsApprovedPrefabPath(normalizedPath))
                continue;

            // note: Direct dependencies identify the exact prefab slots rebound by the URP conversion without opening or saving unrelated prefabs.
            string[] dependencies = AssetDatabase.GetDependencies(normalizedPath, false);
            for (int i = 0; i < dependencies.Length; i++)
            {
                if (!ConvertedMaterialPaths.Contains(dependencies[i].Replace('\\', '/')))
                    continue;

                AffectedPrefabPaths.Add(normalizedPath);
                break;
            }
        }

        if (_prefabIndex < (_prefabGuids != null ? _prefabGuids.Length : 0))
            return;

        EditorApplication.update -= ProcessTick;
        _running = false;
        Complete();
    }

    private static void Complete()
    {
        YQDiscoveredWorldAssetCatalog catalog =
            AssetDatabase.LoadAssetAtPath<YQDiscoveredWorldAssetCatalog>(CatalogPath);

        HashSet<string> catalogPaths = CollectCatalogPaths(catalog);

        List<string> missingUsable = new List<string>();
        for (int i = 0; i < AffectedPrefabPaths.Count; i++)
        {
            string path = AffectedPrefabPaths[i];
            if (catalogPaths.Contains(path))
                continue;

            if (IsExcludedPath(path))
            {
                _excludedCount++;
                continue;
            }

            missingUsable.Add(path);
        }

        // note: Refresh the existing authoritative discovery catalog once so converted prefabs receive current slot/style classification rather than relying on stale serialized inference.
        SessionState.SetBool(SessionKey, true);
        Debug.Log("[YQUrpPaletteIntegration] Rebuilding the existing procedural discovery catalog for converted prefab coverage. Affected prefabs: " +
            AffectedPrefabPaths.Count + ", excluded non-world assets: " + _excludedCount + ", missing entries before rebuild: " + missingUsable.Count + ".");
        YQRuntimeWorldAssetRegistryBuilder.RebuildRegistryWithDiscoveredAssets();

        // note: Verify the persisted catalog after the rebuild so the report proves serialized coverage, not just the pre-rebuild audit.
        catalog = AssetDatabase.LoadAssetAtPath<YQDiscoveredWorldAssetCatalog>(CatalogPath);
        catalogPaths = CollectCatalogPaths(catalog);
        int postRebuildMissing = 0;
        int unresolvedAffectedPrefabs = 0;
        HashSet<string> affectedCatalogKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int duplicateAffectedRecords = 0;
        if (catalog != null && catalog.Entries != null)
        {
            for (int i = 0; i < catalog.Entries.Count; i++)
            {
                GeneratedAssetReferenceRecord entry = catalog.Entries[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.assetPath))
                    continue;

                string normalizedPath = entry.assetPath.Replace('\\', '/');
                if (!AffectedPrefabPaths.Contains(normalizedPath))
                    continue;

                string styleKey = entry.styleTags != null ? string.Join(",", entry.styleTags) : string.Empty;
                string identity = normalizedPath + "|" + (entry.slotTag ?? string.Empty) + "|" + styleKey;
                if (!affectedCatalogKeys.Add(identity))
                    duplicateAffectedRecords++;
            }
        }

        for (int i = 0; i < AffectedPrefabPaths.Count; i++)
        {
            string path = AffectedPrefabPaths[i];
            if (!catalogPaths.Contains(path) && !IsExcludedPath(path))
                postRebuildMissing++;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null && !IsExcludedPath(path))
                unresolvedAffectedPrefabs++;
        }

        // note: Persist a compact audit result so completion can be verified without relying on a transient Unity console message.
        string report =
            "Converted URP material assets: " + ConvertedMaterialPaths.Count + Environment.NewLine +
            "Affected approved prefabs: " + AffectedPrefabPaths.Count + Environment.NewLine +
            "Excluded non-world prefabs: " + _excludedCount + Environment.NewLine +
            "Missing usable catalog entries: " + missingUsable.Count + Environment.NewLine +
            "Missing usable catalog entries after rebuild: " + postRebuildMissing + Environment.NewLine +
            "Unresolved affected prefabs after rebuild: " + unresolvedAffectedPrefabs + Environment.NewLine +
            "Duplicate affected catalog records: " + duplicateAffectedRecords + Environment.NewLine +
            "Catalog path: " + CatalogPath + Environment.NewLine;
        File.WriteAllText(ReportPath, report);

        ConvertedMaterialPaths.Clear();
        AffectedPrefabPaths.Clear();
        _prefabGuids = null;
    }

    private static HashSet<string> CollectCatalogPaths(YQDiscoveredWorldAssetCatalog catalog)
    {
        // note: Normalize catalog paths once so coverage checks use the same identity as Unity's asset database.
        HashSet<string> paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (catalog == null || catalog.Entries == null)
            return paths;

        for (int i = 0; i < catalog.Entries.Count; i++)
        {
            GeneratedAssetReferenceRecord entry = catalog.Entries[i];
            if (entry != null && !string.IsNullOrWhiteSpace(entry.assetPath))
                paths.Add(entry.assetPath.Replace('\\', '/'));
        }

        return paths;
    }

    private static bool IsExcludedPath(string path)
    {
        string search = (path ?? string.Empty).Replace('\\', '/').ToLowerInvariant();
        for (int i = 0; i < ExcludedTerms.Length; i++)
        {
            if (search.Contains(ExcludedTerms[i]))
                return true;
        }

        return false;
    }

    private static bool IsApprovedPrefabPath(string path)
    {
        foreach (string root in ApprovedPrefabRoots)
        {
            if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

}
