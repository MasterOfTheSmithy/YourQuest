#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: Bounded Editor checks compare the legacy exhaustive selection with lazy selection, using existing imported materials without editing assets.
[InitializeOnLoad]
public static class YQMaterialSearchMemoryVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/YQ_MATERIAL_SEARCH_MEMORY_VERIFY.request";
    private const string Output = "outputs/UnityMemoryHeadroom_20261008/MaterialSearchVerification.json";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;
    private static readonly Type Owner = typeof(YQRuntimeUrpMaterialRepair);
    private static double nextCheck;
    static YQMaterialSearchMemoryVerification() { EditorApplication.update += Tick; }
    private static object Call(string name, params object[] args) => Owner.GetMethod(name, Private).Invoke(null, args);
    private static void Set(object item, string field, object value) => item.GetType().GetField(field).SetValue(item, value);
    private static object Get(object item, string field) => item.GetType().GetField(field).GetValue(item);

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < nextCheck) return;
        nextCheck = EditorApplication.timeSinceStartup + 1;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || !File.Exists(Request)) return;
        bool fresh = File.GetLastWriteTimeUtc(Request) > DateTime.UtcNow.AddMinutes(-2);
        File.Delete(Request);
        if (fresh) Run();
    }

    [MenuItem("YourQuest/Verification/Verify Lazy Material Search Memory")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Run search verification outside Play.");
        Directory.CreateDirectory(Path.GetDirectoryName(Output));
        var candidatesField = Owner.GetField("s_editorMaterialCandidates", Private);
        var cache = (IDictionary)Owner.GetField("s_editorFallbackMaterialCache", Private).GetValue(null);
        object originalCandidates = candidatesField.GetValue(null);
        var originalCache = new List<DictionaryEntry>();
        foreach (DictionaryEntry entry in cache) originalCache.Add(entry);
        Material query = null;
        var checks = new List<object>();
        int catalogCount = 0, unknownNames = 0, textureDelta = 0, meshDelta = 0;
        object[] discoveryTextures = Array.Empty<object>();
        object[] unknownSamples = Array.Empty<object>();
        string failure = null;
        try
        {
            // note: Actual project-wide discovery must allocate metadata only, irrespective of the size of the imported library.
            candidatesField.SetValue(null, null);
            var textureIds = new HashSet<int>(Resources.FindObjectsOfTypeAll<Texture>().Select(t => t.GetInstanceID()));
            int meshesBefore = Resources.FindObjectsOfTypeAll<Mesh>().Length;
            Call("EnsureEditorMaterialCandidates");
            Array catalog = (Array)candidatesField.GetValue(null);
            catalogCount = catalog.Length;
            unknownNames = catalog.Cast<object>().Count(c => Get(c, "name") == null && Get(c, "possibleNames") == null);
            Texture[] added = Resources.FindObjectsOfTypeAll<Texture>().Where(t => !textureIds.Contains(t.GetInstanceID())).ToArray();
            discoveryTextures = added.Select(t => (object)new { name = t.name, path = AssetDatabase.GetAssetPath(t), bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(t) }).ToArray();
            // note: Importer Inspector metadata may initialize tiny built-in Editor icons; the growth guard concerns the user's imported asset payloads.
            textureDelta = added.Count(t => AssetDatabase.GetAssetPath(t).StartsWith("Assets/", StringComparison.Ordinal));
            meshDelta = Resources.FindObjectsOfTypeAll<Mesh>().Length - meshesBefore;
            unknownSamples = catalog.Cast<object>().Where(c => Get(c, "name") == null && Get(c, "possibleNames") == null).Take(12)
                .Select(c => (object)new { path = Get(c, "path"), importer = AssetImporter.GetAtPath((string)Get(c, "path"))?.GetType().Name }).ToArray();
            if (textureDelta != 0 || meshDelta != 0) throw new InvalidOperationException("Metadata discovery loaded native textures/meshes.");

            Material[] samples = Resources.FindObjectsOfTypeAll<Material>()
                .Where(m => AssetDatabase.GetAssetPath(m).EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                .GroupBy(m => AssetDatabase.GetAssetPath(m)).Select(g => g.First())
                .OrderBy(m => AssetDatabase.GetAssetPath(m).StartsWith("Assets/", StringComparison.Ordinal) ? 0 : 1).Take(12).ToArray();
            if (samples.Length < 4) throw new InvalidOperationException("Not enough already loaded materials for a meaningful selection comparison.");
            Type candidateType = catalog.GetType().GetElementType();
            foreach (Material sample in samples)
            {
                string name = (string)Call("ReadEditorMaterialName", AssetDatabase.GetAssetPath(sample));
                if (name != sample.name) throw new InvalidOperationException("Serialized material name differs: " + AssetDatabase.GetAssetPath(sample));
            }
            query = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.HideAndDontSave };
            string[] queries = samples.Select(m => m.name).Concat(new[] { "unmatchedqzxv987654", "sword14 chest02 traveler tunic" }).ToArray();
            foreach (string text in queries)
            {
                query.name = text;
                string search = (string)Call("BuildFallbackSearchText", query, null);
                string[] tokens = (string[])Call("ExtractFallbackTokens", search);
                Array testCandidates = Array.CreateInstance(candidateType, samples.Length);
                Material expected = null;
                int bestScore = 0;
                for (int i = 0; i < samples.Length; i++)
                {
                    Material material = samples[i];
                    object candidate = Activator.CreateInstance(candidateType, true);
                    string path = AssetDatabase.GetAssetPath(material);
                    Set(candidate, "path", path); Set(candidate, "name", material.name); Set(candidate, "ordinal", i);
                    Set(candidate, "text", Call("ToSearchText", material.name + " " + path));
                    bool hasTexture = (bool)Call("HasUsableBaseTexture", material);
                    bool hasColor = (bool)Call("HasUsefulColor", material);
                    Set(candidate, "hasTexture", hasTexture); Set(candidate, "hasUsefulColor", hasColor);
                    int score = (int)Call("ScoreEditorMaterialCandidate", candidate, tokens, search);
                    if ((hasTexture || hasColor) && score > bestScore) { expected = material; bestScore = score; }
                    // note: Reset appearance facts so the new search must exercise its conservative upper bound rather than being handed the answer.
                    Set(candidate, "hasTexture", false); Set(candidate, "hasUsefulColor", false);
                    testCandidates.SetValue(candidate, i);
                }
                if (bestScore < 80) expected = null;
                candidatesField.SetValue(null, testCandidates); cache.Clear();
                Material actual = (Material)Call("FindEditorFallbackMaterial", query, null);
                bool pass = expected == actual;
                checks.Add(new { query = text, pass, expected = AssetDatabase.GetAssetPath(expected), actual = AssetDatabase.GetAssetPath(actual) });
                if (!pass) throw new InvalidOperationException("Exhaustive/lazy selection differs for " + text);
            }
        }
        catch (Exception error) { failure = error.GetBaseException().ToString(); Debug.LogException(error); }
        finally
        {
            if (query != null) UnityEngine.Object.DestroyImmediate(query);
            candidatesField.SetValue(null, originalCandidates);
            cache.Clear(); foreach (DictionaryEntry entry in originalCache) cache.Add(entry.Key, entry.Value);
        }
        File.WriteAllText(Output, JsonConvert.SerializeObject(new { utc = DateTime.UtcNow.ToString("O"), status = failure == null ? "PASS" : "FAIL",
            catalogCount, unknownNames, discoveryTextureDelta = textureDelta, discoveryMeshDelta = meshDelta, discoveryTextures, unknownSamples, checks, failure,
            evidence = "Editor metadata growth check and bounded legacy/lazy selection comparison; imported assets unmodified, no visual or gameplay claim." }, Formatting.Indented));
        Debug.Log("[YQMaterialSearchMemoryVerification] " + (failure == null ? "PASS" : "FAIL"));
    }
}
#endif
