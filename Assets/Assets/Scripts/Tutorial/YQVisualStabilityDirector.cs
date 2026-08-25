using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-1300)]
public sealed class YQVisualStabilityDirector : MonoBehaviour
{
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionMapPropertyId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveColorPropertyId = Shader.PropertyToID("_EmissiveColor");

    public float maxShadowDistance = 32f;
    public float additionalLightIntensityCap = 2.6f;
    public float additionalLightRangeCap = 28f;
    public bool stabilizeSceneRenderersOnPlay = false;
    public int followupPasses = 1;
    public float followupInterval = 1.0f;

    private static Material s_floorMaterial;
    private static Material s_padMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQVisualStabilityDirector>() != null)
            return;

        // note: Awake owns the initial lighting pass; running it here as well previously scanned every loaded light twice during the same startup frame.
        GameObject go = new GameObject("00__YQ_VisualStabilityDirector");
        DontDestroyOnLoad(go);
        go.AddComponent<YQVisualStabilityDirector>();
    }

#if UNITY_EDITOR
    private static bool s_editorStabilizerQueued;

    [InitializeOnLoadMethod]
    private static void InstallEditorStabilizer()
    {
        QueueEditorStabilizer();
    }

    private static void QueueEditorStabilizer()
    {
        if (s_editorStabilizerQueued)
            return;

        s_editorStabilizerQueued = true;
        EditorApplication.delayCall += RunEditorStabilizerOnce;
    }

    private static void RunEditorStabilizerOnce()
    {
        s_editorStabilizerQueued = false;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueEditorStabilizer();
            return;
        }

        int changedCount = StabilizeRenderPipelineAssetsForEditor();
        StabilizeLightingOnly(32f, 2.6f, 28f);
        if (changedCount > 0)
            Debug.Log("[YQVisualStability] Disabled additional-light shadows in " + changedCount + " render-pipeline asset(s).");
    }
#endif

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);
        RunConfiguredStabilizerPass();
    }

    private IEnumerator Start()
    {
        // note: Awake already completed the immediate pass; Start schedules only deliberately configured follow-up coverage.
        int passes = Mathf.Max(0, followupPasses);
        for (int i = 0; i < passes; i++)
        {
            yield return new WaitForSeconds(Mathf.Max(0.2f, followupInterval));
            RunConfiguredStabilizerPass();
        }
    }

    private void RunConfiguredStabilizerPass()
    {
        if (stabilizeSceneRenderersOnPlay)
            StabilizeScene(maxShadowDistance, additionalLightIntensityCap, additionalLightRangeCap);
        else
            StabilizeLightingOnly(maxShadowDistance, additionalLightIntensityCap, additionalLightRangeCap);
    }

    public static void StabilizeLightingOnly(float maxShadowDistance = 32f, float lightIntensityCap = 2.6f, float lightRangeCap = 28f)
    {
        if (QualitySettings.shadowDistance <= 0f || QualitySettings.shadowDistance > maxShadowDistance)
            QualitySettings.shadowDistance = Mathf.Max(8f, maxShadowDistance);

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
            StabilizeLight(lights[i], lightIntensityCap, lightRangeCap);
    }

    public static void StabilizeScene(float maxShadowDistance = 32f, float lightIntensityCap = 2.6f, float lightRangeCap = 28f)
    {
        StabilizeLightingOnly(maxShadowDistance, lightIntensityCap, lightRangeCap);

        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
            StabilizeRenderer(renderers[i]);
    }

    public static void StabilizeHierarchy(GameObject root)
    {
        if (root == null)
            return;

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
            StabilizeLight(lights[i], 2.6f, 28f);

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            StabilizeRenderer(renderers[i]);
    }

    private static void StabilizeLight(Light light, float lightIntensityCap, float lightRangeCap)
    {
        if (light == null)
            return;

        bool builtInPipeline = GraphicsSettings.currentRenderPipeline == null;
        if (light.type == LightType.Directional)
        {
            light.shadows = LightShadows.Soft;
            if (builtInPipeline)
            {
                light.shadowResolution = LightShadowResolution.Medium;
                if (light.shadowCustomResolution > 2048)
                    light.shadowCustomResolution = 2048;
            }
            light.shadowStrength = Mathf.Clamp(light.shadowStrength <= 0f ? 0.72f : light.shadowStrength, 0.45f, 0.82f);
            light.shadowBias = Mathf.Clamp(light.shadowBias <= 0f ? 0.04f : light.shadowBias, 0.02f, 0.12f);
            light.shadowNormalBias = Mathf.Clamp(light.shadowNormalBias <= 0f ? 0.45f : light.shadowNormalBias, 0.25f, 0.85f);
            return;
        }

        light.shadows = LightShadows.None;
        if (builtInPipeline)
        {
            light.shadowResolution = LightShadowResolution.Low;
            light.shadowCustomResolution = 0;
        }
        light.intensity = Mathf.Min(light.intensity, Mathf.Max(0.1f, lightIntensityCap));
        light.range = Mathf.Min(light.range, Mathf.Max(1f, lightRangeCap));
    }

    private static void StabilizeRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        string name = renderer.gameObject.name.ToLowerInvariant();
        if (name.Contains("floor") || name.Contains("ground") || name.Contains("pad") || name.Contains("roadmarker"))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            AssignStableSurfaceMaterial(renderer, name.Contains("pad") || name.Contains("roadmarker"));
        }

        if (name.Contains("pad") && renderer.transform.localScale.y <= 0.12f && renderer.transform.localPosition.y < 0.075f)
        {
            Vector3 localPosition = renderer.transform.localPosition;
            localPosition.y = 0.08f;
            renderer.transform.localPosition = localPosition;
        }
    }

    private static void AssignStableSurfaceMaterial(Renderer renderer, bool pad)
    {
        if (renderer == null || renderer is ParticleSystemRenderer)
            return;
        if (HasUsableTexture(renderer.sharedMaterial))
            return;

        renderer.sharedMaterial = pad ? GetPadMaterial() : GetFloorMaterial();
    }

    private static bool HasUsableTexture(Material material)
    {
        if (material == null)
            return false;
        if (material.HasProperty(BaseMapPropertyId) && IsUsableSurfaceTexture(material.GetTexture(BaseMapPropertyId)))
            return true;
        if (material.HasProperty(MainTexPropertyId) && IsUsableSurfaceTexture(material.GetTexture(MainTexPropertyId)))
            return true;
        try
        {
            if (material.HasProperty("_MainTex") && IsUsableSurfaceTexture(material.mainTexture))
                return true;
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static Material GetFloorMaterial()
    {
        if (s_floorMaterial != null)
            return s_floorMaterial;

        s_floorMaterial = CreateSurfaceMaterial("YQ_StableTiledFloor", new Color(0.19f, 0.21f, 0.22f, 1f), new Color(0.27f, 0.29f, 0.30f, 1f), 14f);
        return s_floorMaterial;
    }

    private static Material GetPadMaterial()
    {
        if (s_padMaterial != null)
            return s_padMaterial;

        s_padMaterial = CreateSurfaceMaterial("YQ_StableRaisedPad", new Color(0.16f, 0.18f, 0.20f, 1f), new Color(0.25f, 0.28f, 0.32f, 1f), 7f);
        return s_padMaterial;
    }

    private static Material CreateSurfaceMaterial(string materialName, Color dark, Color light, float tileScale)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = materialName;
        material.hideFlags = HideFlags.DontSave;
        Texture2D texture = CreateTileTexture(materialName + "_Texture", dark, light);

        if (material.HasProperty(BaseMapPropertyId))
        {
            material.SetTexture(BaseMapPropertyId, texture);
            material.SetTextureScale(BaseMapPropertyId, new Vector2(tileScale, tileScale));
        }
        if (material.HasProperty(MainTexPropertyId))
        {
            material.SetTexture(MainTexPropertyId, texture);
            material.SetTextureScale(MainTexPropertyId, new Vector2(tileScale, tileScale));
        }
        if (material.HasProperty(BaseColorPropertyId))
            material.SetColor(BaseColorPropertyId, Color.white);
        if (material.HasProperty(ColorPropertyId))
            material.SetColor(ColorPropertyId, Color.white);
        if (material.HasProperty(EmissionMapPropertyId))
            material.SetTexture(EmissionMapPropertyId, null);
        if (material.HasProperty(EmissionColorPropertyId))
            material.SetColor(EmissionColorPropertyId, Color.black);
        if (material.HasProperty(EmissiveColorPropertyId))
            material.SetColor(EmissiveColorPropertyId, Color.black);
        material.DisableKeyword("_EMISSION");

        return material;
    }

    private static bool IsUsableSurfaceTexture(Texture texture)
    {
        return texture != null && !LooksLikeGeneratedSurfaceTexture(texture);
    }

    private static bool LooksLikeGeneratedSurfaceTexture(Texture texture)
    {
        if (texture == null)
            return false;

        string name = ToSearchText(texture.name);
        if (name.Contains(" tex ") || name.Contains(" tile ") || name.Contains("fallbacktex") || name.Contains("fallback tex") || name.Contains("runtime"))
            return true;

#if UNITY_EDITOR
        string path = AssetDatabase.GetAssetPath(texture);
        if (!string.IsNullOrWhiteSpace(path))
        {
            string normalizedPath = path.Replace('\\', '/').ToLowerInvariant();
            return normalizedPath.StartsWith("assets/assets/materials/") && normalizedPath.EndsWith(".asset");
        }
#endif

        return false;
    }

    private static string ToSearchText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = value.ToLowerInvariant().Replace('\\', '/').ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = ' ';
        }

        return " " + new string(chars) + " ";
    }

    private static Texture2D CreateTileTexture(string textureName, Color dark, Color light)
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        texture.name = textureName;
        texture.hideFlags = HideFlags.DontSave;
        texture.wrapMode = TextureWrapMode.Repeat;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool checker = ((x / 16) + (y / 16)) % 2 == 0;
                bool grout = x % 16 == 0 || y % 16 == 0;
                Color color = checker ? light : dark;
                if (grout)
                    color = Color.Lerp(color, dark, 0.32f);
                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply(false, true);
        return texture;
    }

#if UNITY_EDITOR
    public static int StabilizeRenderPipelineAssetsForEditor()
    {
        HashSet<string> paths = new HashSet<string>();
        CollectPipelineAssetPaths("t:RenderPipelineAsset", paths);
        CollectPipelineAssetPaths("t:UniversalRenderPipelineAsset", paths);

        int changedCount = 0;
        foreach (string path in paths)
        {
            RenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
            if (asset == null)
                continue;

            SerializedObject serialized = new SerializedObject(asset);
            bool changed = false;
            changed |= SetSerializedBool(serialized, "m_AdditionalLightShadowsSupported", false);
            changed |= ClampSerializedInt(serialized, "m_AdditionalLightsPerObjectLimit", 4);
            changed |= ClampSerializedInt(serialized, "m_AdditionalLightsShadowmapResolution", 1024);
            changed |= ClampSerializedInt(serialized, "m_AdditionalLightsShadowResolutionTierLow", 128);
            changed |= ClampSerializedInt(serialized, "m_AdditionalLightsShadowResolutionTierMedium", 256);
            changed |= ClampSerializedInt(serialized, "m_AdditionalLightsShadowResolutionTierHigh", 512);
            changed |= ClampSerializedInt(serialized, "m_MainLightShadowmapResolution", 2048);
            changed |= ClampSerializedInt(serialized, "m_ShadowCascadeCount", 2);
            changed |= ClampSerializedFloat(serialized, "m_ShadowDistance", 42f);

            if (!changed)
                continue;

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            changedCount++;
        }

        if (changedCount > 0)
            AssetDatabase.SaveAssets();

        return changedCount;
    }

    private static void CollectPipelineAssetPaths(string filter, HashSet<string> paths)
    {
        string[] guids = AssetDatabase.FindAssets(filter);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }
    }

    private static bool SetSerializedBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.boolValue == value)
            return false;

        property.boolValue = value;
        return true;
    }

    private static bool ClampSerializedInt(SerializedObject serialized, string propertyName, int maxValue)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.intValue <= maxValue)
            return false;

        property.intValue = maxValue;
        return true;
    }

    private static bool ClampSerializedFloat(SerializedObject serialized, string propertyName, float maxValue)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || property.floatValue <= maxValue)
            return false;

        property.floatValue = maxValue;
        return true;
    }
#endif
}
