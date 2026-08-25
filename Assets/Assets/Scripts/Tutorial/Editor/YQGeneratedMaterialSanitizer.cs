using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class YQGeneratedMaterialSanitizer
{
    private const string GeneratedMaterialFolder = "Assets/Assets/Materials";
    private const string SessionKey = "YQGeneratedMaterialSanitizer_v2";
    private static readonly string[] TextureProperties = { "_BaseMap", "_MainTex" };
    private static readonly string[] ColorProperties = { "_BaseColor", "_Color" };

    static YQGeneratedMaterialSanitizer()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += SanitizeGeneratedMaterials;
    }

    private static void SanitizeGeneratedMaterials()
    {
        string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { GeneratedMaterialFolder });
        bool changedAny = false;
        for (int i = 0; i < materialGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialGuids[i]);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            changedAny |= SanitizeMaterial(material, path);
        }

        if (changedAny)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    private static bool SanitizeMaterial(Material material, string assetPath)
    {
        bool changed = false;
        for (int i = 0; i < TextureProperties.Length; i++)
        {
            string property = TextureProperties[i];
            if (!material.HasProperty(property))
                continue;

            Texture texture = TryGetTexture(material, property);
            string texturePath = texture != null ? AssetDatabase.GetAssetPath(texture) : string.Empty;
            if (IsGeneratedTextureAsset(texturePath))
            {
                material.SetTexture(property, null);
                changed = true;
            }
        }

        Color current = FindColor(material, Color.white);
        if (LooksLikeGeneratedSurface(assetPath, material.name) || LooksLikeAlertRed(current))
        {
            Color target = ResolveColor(material.name, current.a);
            for (int i = 0; i < ColorProperties.Length; i++)
            {
                string property = ColorProperties[i];
                if (material.HasProperty(property))
                    material.SetColor(property, target);
            }

            changed = true;
        }

        if (material.HasProperty("_EmissionColor"))
        {
            Color emission = TryGetColor(material, "_EmissionColor", Color.black);
            if (emission.maxColorComponent > 0.001f)
            {
                material.SetColor("_EmissionColor", Color.black);
                material.DisableKeyword("_EMISSION");
                changed = true;
            }
        }

        if (changed)
            EditorUtility.SetDirty(material);
        return changed;
    }

    private static bool IsGeneratedTextureAsset(string texturePath)
    {
        return !string.IsNullOrWhiteSpace(texturePath) &&
               texturePath.StartsWith(GeneratedMaterialFolder + "/", StringComparison.OrdinalIgnoreCase) &&
               texturePath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeGeneratedSurface(string path, string name)
    {
        string text = ((path ?? string.Empty) + " " + (name ?? string.Empty)).ToLowerInvariant();
        return ContainsAny(text, "pad", "floor", "ground", "path", "road", "trail", "dirt", "soil", "cinder",
            "grass", "foliage", "tree", "bush", "rock", "stone", "wall", "roof", "hut", "pedestal",
            "placeholder", "missing", "fallback", "walkable", "region", "terrain");
    }

    private static Color ResolveColor(string name, float alpha)
    {
        string text = (name ?? string.Empty).ToLowerInvariant();
        if (ContainsAny(text, "grass", "foliage", "bush", "leaf", "leaves", "jungle", "moss", "flower", "treebranch", "treebillboard"))
            return new Color(0.22f, 0.40f, 0.20f, alpha);
        if (ContainsAny(text, "path", "road", "trail", "dirt", "soil", "cinder", "east", "packed"))
            return new Color(0.32f, 0.27f, 0.21f, alpha);
        if (ContainsAny(text, "water", "wet", "tide", "west"))
            return new Color(0.20f, 0.30f, 0.33f, alpha);
        if (ContainsAny(text, "ice", "frost", "cold", "north", "crystal"))
            return new Color(0.38f, 0.48f, 0.48f, alpha);
        if (ContainsAny(text, "wood", "bark", "trunk"))
            return new Color(0.34f, 0.25f, 0.17f, alpha);
        if (ContainsAny(text, "metal", "armor", "sword", "axe", "shield", "spear", "staff", "lock"))
            return new Color(0.48f, 0.49f, 0.48f, alpha);
        if (ContainsAny(text, "vfx", "magic", "audio", "station", "spell"))
            return new Color(0.36f, 0.58f, 0.82f, alpha);
        if (ContainsAny(text, "missing", "placeholder", "fallback", "default"))
            return new Color(0.46f, 0.48f, 0.48f, alpha);

        return new Color(0.36f, 0.35f, 0.32f, alpha);
    }

    private static Color FindColor(Material material, Color fallback)
    {
        for (int i = 0; i < ColorProperties.Length; i++)
        {
            string property = ColorProperties[i];
            if (material.HasProperty(property))
                return TryGetColor(material, property, fallback);
        }

        return fallback;
    }

    private static Texture TryGetTexture(Material material, string property)
    {
        try
        {
            return material != null && material.HasProperty(property) ? material.GetTexture(property) : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static Color TryGetColor(Material material, string property, Color fallback)
    {
        try
        {
            return material != null && material.HasProperty(property) ? material.GetColor(property) : fallback;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }

    private static bool LooksLikeAlertRed(Color color)
    {
        return color.r > 0.45f && color.r > color.g * 1.45f && color.r > color.b * 1.45f;
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrEmpty(terms[i]) && text.Contains(terms[i]))
                return true;
        }

        return false;
    }
}
