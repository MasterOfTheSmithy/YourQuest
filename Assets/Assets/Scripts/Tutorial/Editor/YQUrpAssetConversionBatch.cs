using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// note: This editor-only utility converts imported material assets and prefab renderer slots in small update slices so Unity remains responsive.
[InitializeOnLoad]
internal static class YQUrpAssetConversionBatch
{
    private const string VariantFolder = "Assets/Assets/GeneratedAssets/MaterialVariants/URP";
    private const string MenuRoot = "Tools/YourQuest/Materials/";
    private const string SessionKey = "YQUrpAssetConversionBatch_v3";
    private const int MaterialsPerTick = 8;
    private const int PrefabsPerTick = 8;

    private enum Phase
    {
        Materials,
        Prefabs
    }

    private static readonly Dictionary<string, Material> VariantCache = new Dictionary<string, Material>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> GeneratedVariantPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> InvalidMaterialPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static string[] materialGuids = Array.Empty<string>();
    private static string[] prefabGuids = Array.Empty<string>();
    private static int materialIndex;
    private static int prefabIndex;
    private static Phase phase;
    private static bool running;
    private static bool cancelRequested;
    private static double startedAt;
    private static int convertedMaterialCount;
    private static int updatedPrefabCount;

    static YQUrpAssetConversionBatch()
    {
        // note: Imported-material conversion is now opt-in so Unity cannot restart a CPU-heavy prefab batch during editor startup, Play Mode, or video capture.
    }

    [MenuItem(MenuRoot + "Convert Imported Materials to URP (Paced)", priority = 100)]
    private static void StartConversion()
    {
        if (running)
        {
            Debug.Log("[YourQuest] URP material conversion is already running.");
            return;
        }

        EnsureVariantFolder();
        VariantCache.Clear();
        GeneratedVariantPaths.Clear();
        InvalidMaterialPaths.Clear();
        materialGuids = AssetDatabase.FindAssets("t:Material");
        prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        materialIndex = 0;
        prefabIndex = 0;
        phase = Phase.Materials;
        cancelRequested = false;
        running = true;
        startedAt = EditorApplication.timeSinceStartup;
        convertedMaterialCount = 0;
        updatedPrefabCount = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Debug.Log($"[YourQuest] Started paced URP conversion: {materialGuids.Length} materials, {prefabGuids.Length} prefabs.");
    }

    [MenuItem(MenuRoot + "Cancel Paced URP Conversion", priority = 101)]
    private static void CancelConversion()
    {
        if (!running)
            return;

        // note: Cancellation is cooperative so a prefab currently being saved can finish without leaving serialized data half-written.
        cancelRequested = true;
    }

    [MenuItem(MenuRoot + "Cancel Paced URP Conversion", validate = true)]
    private static bool ValidateCancelConversion()
    {
        Menu.SetChecked(MenuRoot + "Cancel Paced URP Conversion", running);
        return running;
    }

    private static void Tick()
    {
        if (!running)
            return;

        try
        {
            if (cancelRequested)
            {
                Finish("cancelled");
                return;
            }

            if (phase == Phase.Materials)
            {
                ProcessMaterials();
                if (materialIndex >= materialGuids.Length)
                    phase = Phase.Prefabs;
            }

            if (phase == Phase.Prefabs)
            {
                ProcessPrefabs();
                if (prefabIndex >= prefabGuids.Length)
                    Finish("completed");
            }

            UpdateProgress();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Finish("stopped after an error");
        }
    }

    private static void ProcessMaterials()
    {
        int processed = 0;
        while (materialIndex < materialGuids.Length && processed++ < MaterialsPerTick)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialGuids[materialIndex++]);
            if (ShouldSkipPath(path))
                continue;

            Material source = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (source == null || YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(source))
                continue;

            InvalidMaterialPaths.Add(path);
            bool particleMaterial = LooksLikeParticleMaterial(path, source);
            CreateOrLoadVariant(source, path, particleMaterial);
        }
    }

    private static void ProcessPrefabs()
    {
        int processed = 0;
        while (prefabIndex < prefabGuids.Length && processed++ < PrefabsPerTick)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[prefabIndex++]);
            if (ShouldSkipPath(path))
                continue;
            if (!PrefabMayNeedRepair(path))
                continue;

            GameObject root = null;
            bool changed = false;
            try
            {
                root = PrefabUtility.LoadPrefabContents(path);
                if (root == null)
                    continue;

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    changed |= RepairRendererMaterials(renderers[i]);

                if (changed)
                {
                    // note: Save only changed prefabs so imported package data and prefab overrides remain untouched when already valid.
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    updatedPrefabCount++;
                }
            }
            finally
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }

    private static bool RepairRendererMaterials(Renderer renderer)
    {
        if (renderer == null)
            return false;

        Material[] materials = renderer.sharedMaterials;
        if (materials == null || materials.Length == 0)
            return false;

        Material fallback = FindUsableSiblingMaterial(materials);
        bool changed = false;
        for (int slot = 0; slot < materials.Length; slot++)
        {
            Material source = materials[slot];
            if (YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(source))
                continue;

            string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
            bool particleMaterial = renderer is ParticleSystemRenderer || LooksLikeParticleMaterial(sourcePath, source);
            Material replacement = null;
            if (source != null && !string.IsNullOrWhiteSpace(sourcePath))
                replacement = CreateOrLoadVariant(source, sourcePath, particleMaterial);
            if (replacement == null && fallback != null)
                replacement = fallback;

            if (replacement != null && materials[slot] != replacement)
            {
                materials[slot] = replacement;
                changed = true;
            }
        }

        if (changed)
            renderer.sharedMaterials = materials;
        return changed;
    }

    private static Material FindUsableSiblingMaterial(Material[] materials)
    {
        for (int i = 0; i < materials.Length; i++)
        {
            if (YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(materials[i]))
                return materials[i];
        }

        return null;
    }

    private static bool PrefabMayNeedRepair(string prefabPath)
    {
        if (InvalidMaterialPaths.Count == 0)
            return false;

        // note: Dependency filtering avoids opening thousands of valid prefabs and keeps the expensive serialized edit limited to affected assets.
        string[] dependencies = AssetDatabase.GetDependencies(prefabPath, false);
        for (int i = 0; i < dependencies.Length; i++)
        {
            if (InvalidMaterialPaths.Contains(dependencies[i]))
                return true;
        }

        return false;
    }

    private static Material CreateOrLoadVariant(Material source, string sourcePath, bool particleMaterial)
    {
        if (source == null || string.IsNullOrWhiteSpace(sourcePath) || ShouldSkipPath(sourcePath))
            return null;

        string cacheKey = sourcePath + (particleMaterial ? "|particle" : "|lit");
        if (VariantCache.TryGetValue(cacheKey, out Material cached))
            return cached;

        string guid = AssetDatabase.AssetPathToGUID(sourcePath);
        string suffix = particleMaterial ? "_Particles" : "_Lit";
        string fileName = SanitizeFileName(source.name) + "__URP_" + (string.IsNullOrEmpty(guid) ? source.GetInstanceID().ToString() : guid) + suffix + ".mat";
        string outputPath = VariantFolder + "/" + fileName;
        Material variant = AssetDatabase.LoadAssetAtPath<Material>(outputPath);
        if (variant == null)
        {
            variant = YQRuntimeUrpMaterialRepair.CreateEditorUrpMaterialVariant(source, null, particleMaterial);
            if (variant == null)
                return null;

            variant.name = fileName.Substring(0, fileName.Length - 4);
            AssetDatabase.CreateAsset(variant, outputPath);
            EditorUtility.SetDirty(variant);
            convertedMaterialCount++;
        }

        GeneratedVariantPaths.Add(outputPath);
        VariantCache[cacheKey] = variant;
        return variant;
    }

    private static void Finish(string state)
    {
        if (!running)
            return;

        running = false;
        EditorApplication.update -= Tick;
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        double seconds = EditorApplication.timeSinceStartup - startedAt;
        Debug.Log($"[YourQuest] Paced URP conversion {state}: {convertedMaterialCount} material variants, {updatedPrefabCount} prefabs, {seconds:0.0}s.");
    }

    private static void UpdateProgress()
    {
        int total = materialGuids.Length + prefabGuids.Length;
        int complete = materialIndex + prefabIndex;
        float progress = total > 0 ? Mathf.Clamp01((float)complete / total) : 1f;
        string item = phase == Phase.Materials ? "material assets" : "prefab bindings";
        EditorUtility.DisplayProgressBar("YourQuest URP conversion", "Processing " + item + " in small batches...", progress);
    }

    private static bool ShouldSkipPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) || path.StartsWith(VariantFolder + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeParticleMaterial(string path, Material material)
    {
        string text = ((path ?? string.Empty) + " " + (material != null ? material.name : string.Empty)).ToLowerInvariant();
        return text.Contains("particle") || text.Contains("vfx") || text.Contains("trail") || text.Contains("line");
    }

    private static void EnsureVariantFolder()
    {
        string[] segments = VariantFolder.Split('/');
        string current = segments[0];
        for (int i = 1; i < segments.Length; i++)
        {
            string next = current + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, segments[i]);
            current = next;
        }
    }

    private static string SanitizeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "Material" : value;
        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars())
            result = result.Replace(invalid.ToString(), string.Empty);
        return string.IsNullOrWhiteSpace(result) ? "Material" : result;
    }

}
// note: The pass remains menu-addressable for repeat imports after this session completes.
