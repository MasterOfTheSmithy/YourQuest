#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: This audit advances one generation-ready prefab per editor update so thousands of imported assets cannot monopolize the editor or realtime playback.
public static class YQGenerationReadyPrefabRuntimeAudit
{
    private const string IntakePath = "Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset";
    private const string RootRegistryPath = "Assets/Assets/Resources/YQRuntimeWorldAssetRegistry.asset";
    private static readonly List<YQSpatialAssetRecord> Records = new List<YQSpatialAssetRecord>();
    private static readonly List<string> Rows = new List<string>();
    private static int _index;
    private static int _shardBound;
    private static int _resolved;
    private static int _instantiated;
    private static int _failures;
    private static bool _running;
    private static double _startedAt;
    private static YQRuntimeWorldAssetRegistry _runtimeRegistry;

    [MenuItem("YourQuest/World Generation/Validate Every Generation-Ready Prefab")]
    public static void RunFromMenu()
    {
        if (_running || EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        YQWorldAssetIntakeCatalog intake = AssetDatabase.LoadAssetAtPath<YQWorldAssetIntakeCatalog>(IntakePath);
        _runtimeRegistry = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(RootRegistryPath);
        Records.Clear();
        Rows.Clear();
        _index = 0;
        _shardBound = 0;
        _resolved = 0;
        _instantiated = 0;
        _failures = 0;
        _startedAt = EditorApplication.timeSinceStartup;

        if (intake == null || intake.SpatialAssets == null || _runtimeRegistry == null)
        {
            Rows.Add("FAIL|catalog_or_root_registry_missing");
            WriteReport("INCOMPLETE");
            return;
        }

        HashSet<string> uniquePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < intake.SpatialAssets.Count; i++)
        {
            YQSpatialAssetRecord record = intake.SpatialAssets[i];
            if (!YQRuntimeWorldAssetRegistryBuilder.IsGenerationReadySpatialAsset(record))
                continue;
            string path = Normalize(record.assetPath);
            if (uniquePaths.Add(path))
                Records.Add(record);
        }
        Records.Sort((a, b) => string.Compare(Normalize(a.assetPath), Normalize(b.assetPath), StringComparison.OrdinalIgnoreCase));
        _running = true;
        EditorApplication.update -= ProcessOne;
        EditorApplication.update += ProcessOne;
        Debug.Log("[YQWorldGenAssetKit] Started bounded exhaustive generation-ready audit for " + Records.Count + " unique prefabs.");
    }

    [MenuItem("YourQuest/World Generation/Cancel Generation-Ready Prefab Validation")]
    public static void Cancel()
    {
        if (!_running)
            return;
        _running = false;
        EditorApplication.update -= ProcessOne;
        WriteReport("INCOMPLETE");
        Records.Clear();
    }

    internal static bool IsRunning => _running;

    private static void ProcessOne()
    {
        if (!_running || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (_index >= Records.Count)
        {
            Finish();
            return;
        }

        YQSpatialAssetRecord record = Records[_index++];
        string path = Normalize(record.assetPath);
        string code = "PASS";
        int rendererCount = 0;
        int colliderCount = 0;
        int lodCount = 0;
        try
        {
            string shardResourcePath = YQRuntimeWorldAssetRegistry.BuildShardResourcePath(path);
            string shardAssetPath = "Assets/Assets/Resources/" + shardResourcePath + ".asset";
            YQRuntimeWorldAssetRegistry shard = AssetDatabase.LoadAssetAtPath<YQRuntimeWorldAssetRegistry>(shardAssetPath);
            YQRuntimeWorldAssetEntry entry = FindEntry(shard, path);
            if (entry == null || entry.prefab == null)
                code = "MISSING_SHARD_ENTRY";
            else
            {
                _shardBound++;
                if (!string.Equals(Normalize(AssetDatabase.GetAssetPath(entry.prefab)), path, StringComparison.OrdinalIgnoreCase))
                    code = "SHARD_PREFAB_PATH_MISMATCH";
                GameObject resolvedPrefab = _runtimeRegistry.ResolvePrefab(path);
                if (resolvedPrefab == null)
                    code = "REGISTRY_RESOLVE_NULL";
                else
                {
                    _resolved++;
                    GameObject instance = null;
                    try
                    {
                        instance = PrefabUtility.InstantiatePrefab(entry.prefab) as GameObject;
                        if (instance == null)
                            code = "INSTANTIATE_NULL";
                        else
                        {
                            _runtimeRegistry.ApplyMaterialOverrides(path, instance);
                            MonoBehaviour[] scripts = instance.GetComponentsInChildren<MonoBehaviour>(true);
                            for (int i = 0; i < scripts.Length; i++)
                                if (scripts[i] == null) { code = "MISSING_SCRIPT"; break; }
                            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                            rendererCount = renderers.Length;
                            if (code == "PASS" && record.hasRenderer && renderers.Length == 0)
                                code = "RENDERER_MISSING";
                            for (int i = 0; i < renderers.Length && code == "PASS"; i++)
                            {
                                Renderer renderer = renderers[i];
                                if (renderer == null || !IsFinite(renderer.bounds.extents) || renderer.bounds.extents.sqrMagnitude <= 0.000001f)
                                    code = "RENDERER_BOUNDS_INVALID";
                                Material[] materials = renderer != null ? renderer.sharedMaterials : null;
                                if (code == "PASS" && materials == null)
                                    code = "MATERIAL_ARRAY_NULL";
                                for (int m = 0; materials != null && m < materials.Length && code == "PASS"; m++)
                                    if (materials[m] == null) code = "MATERIAL_MISSING";
                            }
                            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                            colliderCount = colliders.Length;
                            if (code == "PASS" && record.hasCollider && colliders.Length == 0)
                                code = "COLLIDER_MISSING";
                            for (int i = 0; i < colliders.Length && code == "PASS"; i++)
                                if (colliders[i] == null || !IsFinite(colliders[i].bounds.extents)) code = "COLLIDER_BOUNDS_INVALID";
                            LODGroup[] lodGroups = instance.GetComponentsInChildren<LODGroup>(true);
                            lodCount = lodGroups.Length;
                            for (int i = 0; i < lodGroups.Length && code == "PASS"; i++)
                            {
                                LOD[] lods = lodGroups[i].GetLODs();
                                if (lods == null || lods.Length == 0) { code = "LOD_EMPTY"; break; }
                                for (int l = 0; l < lods.Length && code == "PASS"; l++)
                                {
                                    bool hasValidRenderer = false;
                                    Renderer[] lodRenderers = lods[l].renderers;
                                    for (int r = 0; lodRenderers != null && r < lodRenderers.Length; r++)
                                        if (lodRenderers[r] != null) { hasValidRenderer = true; break; }
                                    // note: Unity-imported LODs may retain trailing null slots; the runtime registry strips those slots, so only an entirely empty LOD is unsafe.
                                    if (!hasValidRenderer) { code = "LOD_RENDERER_MISSING"; break; }
                                }
                            }
                            if (code == "PASS") _instantiated++;
                        }
                    }
                    finally
                    {
                        if (instance != null)
                            UnityEngine.Object.DestroyImmediate(instance);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            code = "EXCEPTION_" + exception.GetType().Name;
        }

        if (code != "PASS") _failures++;
        Rows.Add(code + "|" + path + "|kit=" + (record.kitId ?? string.Empty) + "|role=" + (record.semanticRole ?? string.Empty) + "|renderers=" + rendererCount + "|colliders=" + colliderCount + "|lodGroups=" + lodCount);
        if (_index % 100 == 0)
            Debug.Log("[YQWorldGenAssetKit] Generation-ready audit progress " + _index + "/" + Records.Count + ".");
    }

    private static void Finish()
    {
        _running = false;
        EditorApplication.update -= ProcessOne;
        WriteReport(_failures == 0 && _instantiated == Records.Count ? "PASS" : "FAIL");
        Debug.Log("[YQWorldGenAssetKit] Exhaustive bounded audit complete: declared=" + Records.Count + " shardBound=" + _shardBound + " resolved=" + _resolved + " instantiated=" + _instantiated + " failures=" + _failures + ".");
        Records.Clear();
    }

    private static void WriteReport(string result)
    {
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("# YourQuest Generation-Ready Prefab Runtime Audit");
        report.AppendLine();
        report.AppendLine("- result: " + result);
        report.AppendLine("- catalog: " + IntakePath);
        report.AppendLine("- root registry: " + RootRegistryPath);
        report.AppendLine("- unique ready prefabs: " + Records.Count);
        report.AppendLine("- shard-bound: " + _shardBound);
        report.AppendLine("- registry-resolved: " + _resolved);
        report.AppendLine("- instantiated: " + _instantiated);
        report.AppendLine("- failures: " + _failures);
        report.AppendLine("- elapsed seconds: " + (EditorApplication.timeSinceStartup - _startedAt).ToString("0.000"));
        for (int i = 0; i < Rows.Count; i++)
            report.AppendLine("- " + Rows[i]);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/YQGenerationReadyPrefabRuntimeAudit.md", report.ToString());
    }

    private static YQRuntimeWorldAssetEntry FindEntry(YQRuntimeWorldAssetRegistry shard, string path)
    {
        if (shard == null || shard.Entries == null)
            return null;
        YQRuntimeWorldAssetEntry found = null;
        for (int i = 0; i < shard.Entries.Count; i++)
        {
            YQRuntimeWorldAssetEntry entry = shard.Entries[i];
            if (entry != null && string.Equals(Normalize(entry.assetPath), path, StringComparison.OrdinalIgnoreCase))
            {
                if (found != null)
                    return null;
                found = entry;
            }
        }
        return found;
    }

    private static string Normalize(string path) => (path ?? string.Empty).Replace('\\', '/');

    private static bool IsFinite(Vector3 value)
    {
        return !(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z) ||
                 float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z));
    }
}
#endif
