using System.Collections.Generic;
using UnityEngine;

public static class YQInvestorRuntimeVisuals
{
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private static readonly int BaseColorPropertyId = Shader.PropertyToID("_BaseColor");
    private static readonly int BaseMapPropertyId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexPropertyId = Shader.PropertyToID("_MainTex");
    private static readonly int EmissionMapPropertyId = Shader.PropertyToID("_EmissionMap");
    private static readonly int EmissionColorPropertyId = Shader.PropertyToID("_EmissionColor");
    private static readonly Dictionary<int, Material> s_cleanMaterials = new Dictionary<int, Material>();
    private static MaterialPropertyBlock s_colorBlock;

    public static void SetRendererColor(Renderer renderer, Color color)
    {
        if (renderer == null)
            return;

        renderer.sharedMaterial = GetCleanMaterial(color);
        s_colorBlock ??= new MaterialPropertyBlock();
        s_colorBlock.Clear();
        s_colorBlock.SetColor(ColorPropertyId, color);
        s_colorBlock.SetColor(BaseColorPropertyId, color);
        renderer.SetPropertyBlock(s_colorBlock);
    }

    private static Material GetCleanMaterial(Color color)
    {
        int key = ColorKey(color);
        if (s_cleanMaterials.TryGetValue(key, out Material material) && material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        material = new Material(shader)
        {
            name = "YQ_RuntimeClean_" + ColorUtility.ToHtmlStringRGBA(color),
            hideFlags = HideFlags.DontSave
        };

        if (material.HasProperty(BaseMapPropertyId))
            material.SetTexture(BaseMapPropertyId, null);
        if (material.HasProperty(MainTexPropertyId))
            material.SetTexture(MainTexPropertyId, null);
        if (material.HasProperty(ColorPropertyId))
            material.SetColor(ColorPropertyId, color);
        if (material.HasProperty(BaseColorPropertyId))
            material.SetColor(BaseColorPropertyId, color);
        if (material.HasProperty(EmissionMapPropertyId))
            material.SetTexture(EmissionMapPropertyId, null);
        if (material.HasProperty(EmissionColorPropertyId))
            material.SetColor(EmissionColorPropertyId, Color.black);
        material.DisableKeyword("_EMISSION");

        s_cleanMaterials[key] = material;
        return material;
    }

    private static int ColorKey(Color color)
    {
        int r = Mathf.RoundToInt(Mathf.Clamp01(color.r) * 255f);
        int g = Mathf.RoundToInt(Mathf.Clamp01(color.g) * 255f);
        int b = Mathf.RoundToInt(Mathf.Clamp01(color.b) * 255f);
        int a = Mathf.RoundToInt(Mathf.Clamp01(color.a) * 255f);
        return r | (g << 8) | (b << 16) | (a << 24);
    }
}
