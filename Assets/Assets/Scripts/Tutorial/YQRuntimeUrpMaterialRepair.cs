using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-9000)]
public sealed class YQRuntimeUrpMaterialRepair : MonoBehaviour
{
    // note: Generated assets repair themselves as they spawn; a global Play Mode renderer sweep is too expensive for large imports.
    public bool enableSceneWideMaterialRepair = false;
    public bool enableSceneWideTextRepair = true;
    public float firstRepairDelay = 0.2f;
    public float followupRepairInterval = 1.25f;
    public int followupPasses = 0;
    public float ongoingRepairInterval = 3.5f;
    public float ongoingRepairDuration = 0f;

    private float _nextOngoingRepairTime;
    private float _stopOngoingRepairTime;
    private int _quietOngoingPasses;

    private static readonly string[] BaseTextureProperties = { "_BaseMap", "_MainTex", "_Albedo", "_BaseColorMap", "_DiffuseMap", "_ColorMap", "_BaseColorTexture", "_ColorTexture", "_MainTexture", "_Texture2D" };
    private static readonly string[] BaseColorProperties = { "_BaseColor", "_Color", "_TintColor" };
    private static readonly string[] NormalTextureProperties = { "_BumpMap", "_NormalMap" };
    private static readonly string[] MetallicTextureProperties = { "_MetallicGlossMap", "_MetallicMap", "_MetallicRoughnessMap", "_MaskMap" };
    private static readonly string[] OcclusionTextureProperties = { "_OcclusionMap", "_AmbientOcclusionMap" };
    private static readonly string[] EmissionTextureProperties = { "_EmissionMap", "_EmissiveColorMap" };
    private static readonly string[] BaseTextureNameHints =
    {
        "albedo", "basecolor", "base color", "base map", "diffuse", "color", "col", "albedoopacity",
        "leaf", "leaves", "bark", "trunk", "wood", "branch", "grass", "plant",
        "skin", "body", "head", "face", "hair", "eye", "eyes", "horn", "teeth", "claw", "wing", "scale", "scales",
        "cloth", "fabric", "robe", "armor", "boot", "glove", "gauntlet",
        "lock", "pick", "chest", "mimic", "stone", "wall", "floor", "tile"
    };
    private static readonly string[] NormalTextureNameHints = { "normal", "bump", "nrm" };
    private static readonly string[] NonBaseTextureNameHints = { "normal", "bump", "nrm", "rough", "metal", "metallic", "smooth", "ambientocclusion", "occlusion", " ao", "height", "mask", "emiss", "spec", "orm" };
    private static readonly string[] NearbyTextureFolderNames = { "Textures & Materials", "Textures", "Texture", "Texture & Materials", "Textures and Materials", "Texture and Materials" };
    private static readonly string[] VfxNameHints = { "vfx", "fx", "particle", "spell", "magic", "projectile", "fire", "flame", "ember", "burst", "impact", "aura", "aoe", "beam", "laser", "shield", "electric", "lightning", "spark", "trail", "slash", "heal", "holy", "poison", "venom", "smoke" };
    private static readonly string[] StrongVfxNameHints = { "vfx", "fx", "particle", "spell", "magic", "projectile", "burst", "impact", "aura", "aoe", "beam", "laser", "spark", "trail", "slash", "heal", "holy", "poison", "venom", "smoke" };
    private static readonly string[] WorldSurfaceNameHints = { "ground", "floor", "terrain", "road", "path", "pad", "region", "surface", "tile", "dirt", "soil", "grass", "field", "stone", "rock", "wall", "roof", "outside", "plane" };
    private static readonly string[] SolidObjectNameHints =
    {
        "wall", "floor", "roof", "stair", "step", "column", "pillar",
        "door", "frame", "beam", "plank", "stone", "rock", "cliff",
        "statue", "pedestal", "foundation", "building", "house", "hut",
        "weapon", "sword", "axe", "shield", "chest", "crate", "barrel",
        "table", "chair", "furniture"
    };
    private static readonly string[] FallbackSkipTokens = { "missingmaterial", "missing", "runtimeurp", "repaired", "assettest", "material", "materials", "mat", "mesh", "renderer", "object", "gameobject", "prefab", "model", "models", "lod", "group", "human", "male", "female", "base" };
    private static readonly string[] NumberedFamilyPrefixes = { "sword", "dagger", "axe", "hammer", "club", "bow", "crossbow", "shield", "staff", "chest" };
    private static readonly Dictionary<string, Material> s_runtimeRepairMaterialCache = new Dictionary<string, Material>(System.StringComparer.OrdinalIgnoreCase);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRuntimeMaterialCache()
    {
        // note: Entering a new play session must release runtime adapter references when domain reload is disabled; otherwise every generated world retains the prior session's material clones.
        foreach (Material material in s_runtimeRepairMaterialCache.Values)
        {
            if (material != null)
                Object.Destroy(material);
        }

        s_runtimeRepairMaterialCache.Clear();
    }

    private enum TextureSearchKind
    {
        Base,
        Normal,
        Metallic,
        Occlusion,
        Emission
    }

#if UNITY_EDITOR
    private sealed class EditorMaterialCandidate
    {
        public Material material;
        public string text;
        public bool hasTexture;
        public bool hasUsefulColor;
    }

    private static readonly Dictionary<string, Material> s_editorFallbackMaterialCache = new Dictionary<string, Material>();
    private static readonly Dictionary<string, Texture> s_editorNearbyTextureCache = new Dictionary<string, Texture>();
    private static EditorMaterialCandidate[] s_editorMaterialCandidates;
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!YourQuestTutorialAutoBootstrap.GameplayRuntimeReady)
        {
            // note: Title assets own their small scoped repair; the gameplay scene-wide text/material service is deferred until gameplay initialization.
            return;
        }

        if (FindAnyObjectByType<YQRuntimeUrpMaterialRepair>() != null)
            return;

        GameObject go = new GameObject("00__Runtime_URP_MaterialRepair");
        DontDestroyOnLoad(go);
        go.AddComponent<YQRuntimeUrpMaterialRepair>();
    }

    private void Awake()
    {
        _stopOngoingRepairTime = Time.unscaledTime + Mathf.Max(0f, ongoingRepairDuration);
        _nextOngoingRepairTime = Time.unscaledTime + 0.1f;
    }

    private IEnumerator Start()
    {
        if (firstRepairDelay > 0f)
            yield return new WaitForSeconds(firstRepairDelay);

        RunConfiguredSceneRepairPass();

        int passes = enableSceneWideMaterialRepair ? Mathf.Max(0, followupPasses) : 0;
        for (int i = 0; i < passes; i++)
        {
            yield return new WaitForSeconds(Mathf.Max(0.25f, followupRepairInterval));
            RunConfiguredSceneRepairPass();
        }
    }

    private void Update()
    {
        if (!enableSceneWideMaterialRepair || ongoingRepairInterval <= 0f || Time.unscaledTime > _stopOngoingRepairTime)
            return;
        if (Time.unscaledTime < _nextOngoingRepairTime)
            return;

        _nextOngoingRepairTime = Time.unscaledTime + Mathf.Max(0.5f, ongoingRepairInterval);
        int repaired = RunConfiguredSceneRepairPass();
        _quietOngoingPasses = repaired <= 0 ? _quietOngoingPasses + 1 : 0;
        if (_quietOngoingPasses >= 2)
            _stopOngoingRepairTime = Time.unscaledTime;
    }

    private int RunConfiguredSceneRepairPass()
    {
        if (enableSceneWideMaterialRepair)
        {
            return RepairAllSceneRenderers();
        }

        if (enableSceneWideTextRepair)
            return RepairAllSceneText();

        return 0;
    }

    public static int RepairAllSceneRenderers()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int repaired = RepairRenderers(renderers);
        repaired += RepairTextMeshes(FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        repaired += RepairTmpText(FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        return repaired;
    }

    public static int RepairAllSceneText()
    {
        int repaired = RepairTextMeshes(FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        repaired += RepairTmpText(FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None));
        return repaired;
    }

    public static int RepairHierarchy(GameObject root)
    {
        if (root == null)
            return 0;

        int repaired = RepairMaterialHierarchy(root);
        repaired += RepairTextMeshes(root.GetComponentsInChildren<TextMesh>(true));
        repaired += RepairTmpText(root.GetComponentsInChildren<TMP_Text>(true));
        return repaired;
    }

    public static int RepairMaterialHierarchy(GameObject root)
    {
        if (root == null)
            return 0;

        // note: Generated world assets use a material-only scoped pass; unrelated text traversal is reserved for UI/text repair ownership.
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        return RepairRenderers(renderers);
    }

    public static IEnumerator RepairMaterialHierarchyRoutine(
        GameObject root,
        System.Action<int> completed)
    {
        if (root == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        int repaired = 0;
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                pending.Push(current.GetChild(childIndex));
            }

            Renderer[] renderers = current.GetComponents<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
                repaired += RepairRenderer(renderers[index], false);

            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                // note: Dense streamed environments validate imported materials cooperatively so loading presentation animation is never blocked by a full hierarchy pass.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }

        completed?.Invoke(repaired);
    }

    public static int ForceRepairHierarchy(GameObject root)
    {
        if (root == null)
            return 0;

        // note: Known HDRP-only source packs bypass shader-name heuristics and receive an explicit URP material copy per spawned hierarchy.
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        int repaired = RepairRenderers(renderers, true);
        repaired += RepairTextMeshes(root.GetComponentsInChildren<TextMesh>(true));
        repaired += RepairTmpText(root.GetComponentsInChildren<TMP_Text>(true));
        return repaired;
    }

    public static IEnumerator ForceRepairHierarchyRoutine(
        GameObject root,
        System.Action<int> completed)
    {
        if (root == null)
        {
            completed?.Invoke(0);
            yield break;
        }

        int repaired = 0;
        Stack<Transform> pending = new Stack<Transform>();
        pending.Push(root.transform);
        float frameStartedAt = Time.realtimeSinceStartup;
        while (pending.Count > 0)
        {
            Transform current = pending.Pop();
            for (int childIndex = 0;
                 childIndex < current.childCount;
                 childIndex++)
            {
                pending.Push(current.GetChild(childIndex));
            }

            Renderer[] renderers = current.GetComponents<Renderer>();
            for (int index = 0; index < renderers.Length; index++)
                repaired += RepairRenderer(renderers[index], true);
            repaired += RepairTextMeshes(current.GetComponents<TextMesh>());
            repaired += RepairTmpText(current.GetComponents<TMP_Text>());

            if (Time.realtimeSinceStartup - frameStartedAt >= 0.0015f)
            {
                // note: Imported landmark material conversion is spread across loading frames so the Goddess camera and prose remain responsive.
                yield return null;
                frameStartedAt = Time.realtimeSinceStartup;
            }
        }
        completed?.Invoke(repaired);
    }

    private static int RepairRenderers(Renderer[] renderers)
    {
        return RepairRenderers(renderers, false);
    }

    private static int RepairRenderers(
        Renderer[] renderers,
        bool forceUrpMaterialRepair)
    {
        int repairedCount = 0;
        if (renderers == null)
            return repairedCount;

        for (int i = 0; i < renderers.Length; i++)
            repairedCount += RepairRenderer(
                renderers[i],
                forceUrpMaterialRepair);

        return repairedCount;
    }

    private static int RepairRenderer(
        Renderer renderer,
        bool forceUrpMaterialRepair)
    {
        if (renderer == null || IsVfxGraphRenderer(renderer))
            return 0;

        // note: VFXRenderer rejects shared material assignment, while TextMesh owns a dedicated compatibility path.
        TextMesh textMesh = renderer.GetComponent<TextMesh>();
        if (textMesh != null)
            return RepairTextMesh(textMesh) ? 1 : 0;

        Material[] materials = renderer.sharedMaterials;
        bool changed = false;
        int repairedCount = 0;
        bool particleMaterial = renderer is ParticleSystemRenderer;

        for (int slot = 0; slot < materials.Length; slot++)
        {
            Material repaired = CreateRuntimeRepairMaterial(
                materials[slot],
                renderer,
                particleMaterial,
                forceUrpMaterialRepair);
            if (repaired != null && repaired != materials[slot])
            {
                materials[slot] = repaired;
                changed = true;
                repairedCount++;
            }
        }

        if (changed)
            renderer.sharedMaterials = materials;
        return repairedCount;
    }

    private static Material CreateRuntimeRepairMaterial(
        Material source,
        Renderer renderer,
        bool particleMaterial,
        bool forceUrpMaterialRepair)
    {
        if (!forceUrpMaterialRepair &&
            !ShouldRepairMaterial(source, renderer, particleMaterial))
            return source;

        bool vfxMaterial = particleMaterial || LooksLikeVfxMaterial(source, renderer);
        Shader shader = FindRepairShader(vfxMaterial);
        if (shader == null)
            return source;

        Material effectiveSource = ResolveEffectiveSourceMaterial(source, renderer);
        string cacheKey = BuildRuntimeRepairMaterialCacheKey(effectiveSource, renderer, vfxMaterial, shader);
        if (s_runtimeRepairMaterialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            return cached;

        Material repaired = effectiveSource != null ? new Material(effectiveSource) : new Material(shader);
        repaired.shader = shader;
        repaired.name = (effectiveSource != null ? effectiveSource.name : "MissingMaterial") + "_RuntimeURP";
        repaired.hideFlags = HideFlags.DontSave;
        CopyMaterialSurface(effectiveSource, repaired, vfxMaterial, renderer);
        s_runtimeRepairMaterialCache[cacheKey] = repaired;
        return repaired;
    }

    private static string BuildRuntimeRepairMaterialCacheKey(Material source, Renderer renderer, bool vfxMaterial, Shader shader)
    {
        int sourceId = source != null ? source.GetInstanceID() : 0;
        int shaderId = shader != null ? shader.GetInstanceID() : 0;
        Color fallback = ResolveFallbackColor(source, renderer, vfxMaterial);
        // note: The resolved fallback color already captures any real surface variant; instance/renderer names made identical source materials allocate one adapter per spawned object.
        return sourceId + "|" + shaderId + "|" + vfxMaterial + "|" + BuildColorKey(fallback);
    }

    private static int BuildColorKey(Color color)
    {
        Color32 packed = color;
        unchecked
        {
            int hash = 17;
            hash = hash * 397 ^ packed.r;
            hash = hash * 397 ^ packed.g;
            hash = hash * 397 ^ packed.b;
            hash = hash * 397 ^ packed.a;
            return hash;
        }
    }

    private static bool ShouldRepairMaterial(Material material, Renderer renderer, bool particleMaterial)
    {
        if (material == null || material.shader == null)
            return true;

        string shaderName = material.shader.name;
        if (string.IsNullOrWhiteSpace(shaderName))
            return true;
        if (!material.shader.isSupported)
        {
            // note: HDRP Shader Graphs can share the generic Shader Graphs prefix while still being unsupported by the active URP renderer.
            return true;
        }
        if (shaderName.Contains("InternalErrorShader"))
            return true;
        if (particleMaterial)
            return false;
        if (shaderName.StartsWith("Universal Render Pipeline/") || shaderName.StartsWith("Shader Graphs/"))
        {
            if (LooksLikeBrokenGeneratedMaterial(material, renderer))
                return true;
            if (LooksLikeAlertWorldSurfaceMaterial(material, renderer))
                return true;
            if (LooksSemanticallyOpaque(material, renderer) &&
                HasTransparentSurfaceFlags(material))
            {
                // note: Some converted HDRP solids retain a transparent queue/tag despite using a valid URP shader; force one scoped opaque adapter instead of accepting see-through architecture.
                return true;
            }
            if (HasAssignedBaseTexture(material))
                return false;

            return HasUsableBaseTexture(material) || (!HasUsefulColor(material) && !LooksLikeVfxMaterial(material, renderer));
        }
        if (shaderName.StartsWith("Particles/") || shaderName.StartsWith("Mobile/Particles/"))
            return false;

        if (LooksLikeAlertWorldSurfaceMaterial(material, renderer))
            return true;
        if (HasUsableBaseTexture(material) || HasUsefulColor(material))
            return true;

        return LooksExplicitlyMissing(material, renderer);
    }

    private static bool LooksExplicitlyMissing(Material material, Renderer renderer)
    {
        string materialName = material != null ? material.name : string.Empty;
        string rendererName = renderer != null ? renderer.name : string.Empty;
        string objectName = renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        string text = ToSearchText(materialName + " " + rendererName + " " + objectName);
        return LooksLikePlaceholderText(text) || text.Contains(" magenta ");
    }

    private static bool LooksLikeBrokenGeneratedMaterial(Material material, Renderer renderer)
    {
        if (material == null)
            return true;
        if (LooksLikeStableRuntimeRepairMaterial(material, renderer))
            return false;
        if (LooksLikeAlertPlaceholderMaterial(material, renderer))
            return true;
        if (LooksLikeAlertWorldSurfaceMaterial(material, renderer))
            return true;
        if (HasUsableBaseTexture(material))
            return false;

        string text = ToSearchText(material.name + " " + (renderer != null ? renderer.name : string.Empty));
        return LooksLikePlaceholderText(text);
    }

    private static bool LooksLikeStableRuntimeRepairMaterial(Material material, Renderer renderer)
    {
        if (material == null)
            return false;

        string materialName = ToSearchText(material.name ?? string.Empty);
        if (!materialName.Contains(" runtimeurp"))
            return false;

        Color color = FindColor(material, Color.clear);
        if (LooksLikeAlertRed(color) || IsUnreadablyDark(color))
            return false;
        if (LooksLikeAlertWorldSurfaceMaterial(material, renderer))
            return false;

        return HasUsableBaseTexture(material) || HasUsefulColor(material);
    }

    private static bool LooksLikeAlertWorldSurfaceMaterial(Material material, Renderer renderer)
    {
        if (material == null || renderer == null)
            return false;
        if (HasUsableBaseTexture(material) || LooksLikeVfxMaterial(material, renderer))
            return false;

        string rendererName = renderer.name ?? string.Empty;
        string objectName = renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        string text = ToSearchText(material.name + " " + rendererName + " " + objectName);
        if (!ContainsAny(text, WorldSurfaceNameHints))
            return false;

        Color color = FindColor(material, Color.clear);
        return LooksLikeAlertRed(color) || IsUnreadablyDark(color);
    }

    private static Shader FindRepairShader(bool particleMaterial)
    {
        Shader shader = particleMaterial ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null && particleMaterial)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        return shader;
    }

    private static void CopyMaterialSurface(Material source, Material target, bool vfxMaterial, Renderer renderer)
    {
        if (target == null)
            return;

        source = ResolveEffectiveSourceMaterial(source, renderer);

        Texture baseTexture = FindTexture(source, BaseTextureProperties, out string baseTextureProperty);
        if (LooksLikeAlertPlaceholderMaterial(source, renderer))
        {
            baseTexture = null;
            baseTextureProperty = null;
        }
        if (LooksLikeGeneratedSurfaceTexture(baseTexture))
        {
            baseTexture = null;
            baseTextureProperty = null;
        }
        if (baseTexture != null)
        {
            SetTextureIfPresent(target, "_BaseMap", baseTexture);
            SetTextureIfPresent(target, "_MainTex", baseTexture);
            CopyTextureScaleOffsetIfPresent(source, target, baseTextureProperty, "_BaseMap");
            CopyTextureScaleOffsetIfPresent(source, target, baseTextureProperty, "_MainTex");
        }

        Color fallbackColor = ResolveFallbackColor(source, renderer, vfxMaterial);
        Color baseColor = FindColor(source, fallbackColor);
        if (vfxMaterial && IsNearWhite(baseColor) && !IsNearWhite(fallbackColor))
            baseColor = fallbackColor;
        if (!vfxMaterial && baseTexture == null && IsNearWhite(baseColor) && !IsNearWhite(fallbackColor))
            baseColor = fallbackColor;
        if ((source == null || baseTexture == null) && IsUnreadablyDark(baseColor))
            baseColor = fallbackColor;
        baseColor = SanitizeCopiedMaterialColor(source, renderer, baseTexture, baseColor);
        bool foliageMaterial =
            LooksLikeFoliageMaterial(source, renderer, baseTexture);
        bool transparentSurface =
            vfxMaterial || IsGenuinelyTransparentSource(source, renderer);

        if (!vfxMaterial && !foliageMaterial && !transparentSurface)
        {
            // note: HDRP and vendor shaders frequently store non-one tint alpha on opaque materials; opacity follows authored surface mode, never tint alpha alone.
            baseColor.a = 1f;
        }

        if (vfxMaterial && baseColor.a > 0.94f)
            baseColor.a = 0.82f;

        SetColorIfPresent(target, "_BaseColor", baseColor);
        SetColorIfPresent(target, "_Color", baseColor);
        SetColorIfPresent(target, "_TintColor", baseColor);

        Texture normalTexture = FindTexture(source, NormalTextureProperties, out string normalTextureProperty);
        if (normalTexture != null)
        {
            SetTextureIfPresent(target, "_BumpMap", normalTexture);
            CopyTextureScaleOffsetIfPresent(source, target, normalTextureProperty, "_BumpMap");
            target.EnableKeyword("_NORMALMAP");
        }

        Texture metallicTexture = FindTexture(source, MetallicTextureProperties, out string metallicTextureProperty);
        if (metallicTexture != null)
        {
            SetTextureIfPresent(target, "_MetallicGlossMap", metallicTexture);
            SetTextureIfPresent(target, "_MaskMap", metallicTexture);
            CopyTextureScaleOffsetIfPresent(source, target, metallicTextureProperty, "_MetallicGlossMap");
            CopyTextureScaleOffsetIfPresent(source, target, metallicTextureProperty, "_MaskMap");
            target.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        Texture occlusionTexture = FindTexture(source, OcclusionTextureProperties, out string occlusionTextureProperty);
        if (occlusionTexture != null)
        {
            SetTextureIfPresent(target, "_OcclusionMap", occlusionTexture);
            CopyTextureScaleOffsetIfPresent(source, target, occlusionTextureProperty, "_OcclusionMap");
        }

        Texture emissionTexture = null;
        string emissionTextureProperty = null;
        if (vfxMaterial)
            emissionTexture = FindTexture(source, EmissionTextureProperties, out emissionTextureProperty);
        if (emissionTexture != null)
        {
            SetTextureIfPresent(target, "_EmissionMap", emissionTexture);
            CopyTextureScaleOffsetIfPresent(source, target, emissionTextureProperty, "_EmissionMap");
            SetColorIfPresent(target, "_EmissionColor", FindColor(source, Color.white));
            SetColorIfPresent(target, "_EmissiveColor", FindColor(source, Color.white));
            target.EnableKeyword("_EMISSION");
        }
        else if (vfxMaterial)
        {
            SetColorIfPresent(target, "_EmissionColor", Intensify(baseColor, 2.7f));
            SetColorIfPresent(target, "_EmissiveColor", Intensify(baseColor, 2.7f));
            target.EnableKeyword("_EMISSION");
        }
        else
        {
            SetColorIfPresent(target, "_EmissionColor", Color.black);
            SetColorIfPresent(target, "_EmissiveColor", Color.black);
            target.DisableKeyword("_EMISSION");
        }

        CopyFloatIfPresent(source, target, "_Metallic", "_Metallic");
        CopyFloatIfPresent(source, target, "_Glossiness", "_Smoothness");
        CopyFloatIfPresent(source, target, "_Smoothness", "_Smoothness");
        if (foliageMaterial)
            ConfigureFoliageCutout(target);
        else
            ConfigureSurfaceMode(target, transparentSurface, vfxMaterial);
    }

    private static bool IsGenuinelyTransparentSource(
        Material source,
        Renderer renderer)
    {
        if (renderer is ParticleSystemRenderer)
            return true;

        if (LooksSemanticallyTransparent(source, renderer))
            return true;

        if (LooksSemanticallyOpaque(source, renderer))
            return false;

        if (source == null)
            return false;

        string renderType = source.GetTag("RenderType", false, string.Empty);

        if (renderType.IndexOf(
                "Transparent",
                System.StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return true;
        }

        if (source.renderQueue >= 3000)
            return true;

        return IsEnabledSurfaceProperty(source, "_Surface") ||
               IsEnabledSurfaceProperty(source, "_SurfaceType") ||
               IsStandardTransparentMode(source);
    }

    private static bool LooksSemanticallyTransparent(
        Material material,
        Renderer renderer)
    {
        string text = " " + ToSearchText(
            (material != null ? material.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null
                ? renderer.gameObject.name
                : string.Empty)) + " ";
        string[] tokens =
        {
            " glass ", " window ", " transparent ", " translucent ",
            " hologram ", " liquid ", " water ", " smoke ", " fog ",
            " flame ", " particle ", " decal ", " laser "
        };

        for (int index = 0; index < tokens.Length; index++)
        {
            if (text.Contains(tokens[index]))
                return true;
        }

        return false;
    }

    private static bool LooksSemanticallyOpaque(
        Material material,
        Renderer renderer)
    {
        if (LooksSemanticallyTransparent(material, renderer))
            return false;

        string text = ToSearchText(
            (material != null ? material.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null
                ? renderer.gameObject.name
                : string.Empty));
        return ContainsAny(text, SolidObjectNameHints);
    }

    private static bool HasTransparentSurfaceFlags(Material material)
    {
        if (material == null)
            return false;

        string renderType = material.GetTag(
            "RenderType",
            false,
            string.Empty);
        return renderType.IndexOf(
                   "Transparent",
                   System.StringComparison.OrdinalIgnoreCase) >= 0 ||
               material.renderQueue >= 3000 ||
               IsEnabledSurfaceProperty(material, "_Surface") ||
               IsEnabledSurfaceProperty(material, "_SurfaceType") ||
               IsStandardTransparentMode(material);
    }

    private static bool IsEnabledSurfaceProperty(
        Material material,
        string propertyName)
    {
        if (material == null || !IsReadableFloatProperty(material, propertyName))
            return false;

        try
        {
            return material.GetFloat(propertyName) > 0.5f;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsStandardTransparentMode(Material material)
    {
        if (material == null || !IsReadableFloatProperty(material, "_Mode"))
            return false;

        try
        {
            return material.GetFloat("_Mode") >= 2f;
        }
        catch
        {
            return false;
        }
    }

    private static int RepairTextMeshes(TextMesh[] meshes)
    {
        int repaired = 0;
        if (meshes == null)
            return repaired;

        for (int i = 0; i < meshes.Length; i++)
        {
            if (RepairTextMesh(meshes[i]))
                repaired++;
        }

        return repaired;
    }

    private static bool RepairTextMesh(TextMesh mesh)
    {
        if (mesh == null)
            return false;

        bool changed = false;
        Color readable = EnsureReadableTextColor(mesh.color);
        if (!Approximately(mesh.color, readable))
        {
            mesh.color = readable;
            changed = true;
        }

        Renderer renderer = mesh.GetComponent<Renderer>();
        if (renderer == null)
            return changed;

        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null)
                continue;

            SetColorIfPresent(material, "_Color", readable);
            SetColorIfPresent(material, "_BaseColor", readable);
            SetColorIfPresent(material, "_TintColor", readable);
            SetColorIfPresent(material, "_EmissionColor", Intensify(readable, 1.3f));
            changed = true;
        }

        return changed;
    }

    private static int RepairTmpText(TMP_Text[] texts)
    {
        int repaired = 0;
        if (texts == null)
            return repaired;

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];
            if (text == null)
                continue;

            Color readable = EnsureReadableTextColor(text.color);
            if (Approximately(text.color, readable))
                continue;

            text.color = readable;
            repaired++;
        }

        return repaired;
    }

    private static bool LooksLikeVfxMaterial(Material material, Renderer renderer)
    {
        string materialName = material != null ? material.name : string.Empty;
        string shaderName = material != null && material.shader != null ? material.shader.name : string.Empty;
        string rendererName = renderer != null ? renderer.name : string.Empty;
        string objectName = renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        string text = ToSearchText(materialName + " " + shaderName + " " + rendererName + " " + objectName);
        if (LooksLikeWorldSurfaceText(text) && !ContainsAny(text, StrongVfxNameHints))
            return false;

        return ContainsAny(text, VfxNameHints);
    }

    private static bool LooksUnreadablyDark(Material material)
    {
        if (material == null)
            return true;

        Texture baseTexture = FindTexture(material, BaseTextureProperties);
        if (baseTexture != null && !LooksLikeGeneratedSurfaceTexture(baseTexture))
            return false;

        return IsUnreadablyDark(FindColor(material, Color.white));
    }

    private static Material ResolveEffectiveSourceMaterial(Material source, Renderer renderer)
    {
        if (source != null && (!LooksLikeBrokenGeneratedMaterial(source, renderer) || HasUsableBaseTexture(source)))
            return source;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // note: AssetDatabase material searches are editor tooling only; Play Mode must not scan huge imports at startup.
            Material fallback = FindEditorFallbackMaterial(source, renderer);
            if (fallback != null)
                return fallback;
        }
#endif

        return source;
    }

    private static Color ResolveFallbackColor(Material source, Renderer renderer, bool vfxMaterial)
    {
        string sourceName = source != null ? source.name : string.Empty;
        string shaderName = source != null && source.shader != null ? source.shader.name : string.Empty;
        string rendererName = renderer != null ? renderer.name : string.Empty;
        string objectName = renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        string text = ToSearchText(sourceName + " " + shaderName + " " + rendererName + " " + objectName);

        if (LooksLikePlaceholderText(text))
            return vfxMaterial ? new Color(0.42f, 0.58f, 0.76f, 0.82f) : new Color(0.46f, 0.48f, 0.48f, 1f);
        if (!vfxMaterial && LooksLikeWorldSurfaceText(text))
            return new Color(0.45f, 0.42f, 0.34f, 1f);
        if (ContainsAny(text, new[] { "fire", "flame", "ember" }))
            return new Color(1f, 0.42f, 0.13f, vfxMaterial ? 0.84f : 1f);
        if (ContainsAny(text, new[] { "ice", "frost", "water" }))
            return new Color(0.52f, 0.92f, 1f, vfxMaterial ? 0.82f : 1f);
        if (ContainsAny(text, new[] { "storm", "arc", "electric", "lightning" }))
            return new Color(0.42f, 0.72f, 1f, vfxMaterial ? 0.82f : 1f);
        if (ContainsAny(text, new[] { "poison", "venom", "toxic" }))
            return new Color(0.48f, 1f, 0.38f, vfxMaterial ? 0.8f : 1f);
        if (ContainsAny(text, new[] { "heal", "holy", "restore" }))
            return new Color(0.72f, 1f, 0.76f, vfxMaterial ? 0.82f : 1f);
        if (ContainsAny(text, new[] { "shield", "barrier", "aura" }))
            return new Color(0.62f, 0.92f, 1f, vfxMaterial ? 0.72f : 1f);
        if (ContainsAny(text, new[] { "label", "text", "font" }))
            return new Color(0.95f, 0.97f, 1f, 1f);
        if (ContainsAny(text, new[] { "wood", "mansion", "door", "table", "chest" }))
            return new Color(0.64f, 0.44f, 0.26f, 1f);
        if (ContainsAny(text, new[] { "metal", "armor", "sword", "axe", "shield", "helmet", "gauntlet", "ring" }))
            return new Color(0.72f, 0.74f, 0.76f, 1f);
        if (ContainsAny(text, new[] { "human", "skin", "face", "body" }))
            return new Color(0.78f, 0.58f, 0.44f, 1f);

        return vfxMaterial ? new Color(0.72f, 0.88f, 1f, 0.78f) : new Color(0.74f, 0.70f, 0.62f, 1f);
    }

    private static Color EnsureReadableTextColor(Color color)
    {
        Color readable = color;
        if (readable.a < 0.55f)
            readable.a = 0.95f;
        if (IsUnreadablyDark(readable))
            readable = new Color(0.95f, 0.97f, 1f, readable.a);
        return readable;
    }

    private static bool IsUnreadablyDark(Color color)
    {
        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        return color.a > 0.05f && luminance < 0.18f && Mathf.Max(color.r, color.g, color.b) < 0.28f;
    }

    private static bool IsNearWhite(Color color)
    {
        return color.a > 0.05f &&
               color.r > 0.86f &&
               color.g > 0.86f &&
               color.b > 0.86f &&
               Mathf.Abs(color.r - color.g) < 0.12f &&
               Mathf.Abs(color.g - color.b) < 0.12f;
    }

    private static bool LooksLikeAlertPlaceholderMaterial(Material material, Renderer renderer)
    {
        if (material == null)
            return false;

        Texture texture = FindTexture(material, BaseTextureProperties);
        string materialName = material.name ?? string.Empty;
        string textureName = texture != null ? texture.name : string.Empty;
        string rendererName = renderer != null ? renderer.name : string.Empty;
        string objectName = renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty;
        string text = ToSearchText(materialName + " " + textureName + " " + rendererName + " " + objectName);
        if (!LooksLikePlaceholderText(text))
            return false;

        Color color = FindColor(material, Color.clear);
        if (text.Contains("fallback shared vfx") || text.Contains("missingmaterial") || text.Contains("placeholder"))
            return !HasUsefulColor(material) || LooksLikeAlertRed(color) || IsUnreadablyDark(color);

        return LooksLikeAlertRed(color);
    }

    private static bool LooksLikePlaceholderText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        return text.Contains("missingmaterial") ||
               text.Contains(" missing ") ||
               text.Contains("placeholder") ||
               text.Contains("fallback") ||
               text.Contains("proxy");
    }

    private static bool LooksLikeAlertRed(Color color)
    {
        return color.a > 0.05f && color.r > 0.48f && color.r > color.g * 1.55f && color.r > color.b * 1.55f;
    }

    private static bool LooksLikeGeneratedSurfaceTexture(Texture texture)
    {
        if (texture == null)
            return false;

        string name = " " + ToSearchText(texture.name) + " ";
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

    private static bool LooksLikeWorldSurfaceText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string padded = " " + text + " ";
        for (int i = 0; i < WorldSurfaceNameHints.Length; i++)
        {
            string hint = WorldSurfaceNameHints[i];
            if (padded.Contains(" " + hint + " "))
                return true;
        }

        return text.Contains("roadmarker") ||
               text.Contains("outsideground") ||
               text.Contains("groundtile") ||
               text.Contains("floortile") ||
               text.Contains("floorplane") ||
               text.Contains("terrainmesh");
    }

    private static bool HasUsableBaseTexture(Material material)
    {
        Texture texture = FindTexture(material, BaseTextureProperties);
        return texture != null && !LooksLikeGeneratedSurfaceTexture(texture);
    }

    private static bool HasAssignedBaseTexture(Material material)
    {
        if (material == null)
            return false;

        for (int i = 0; i < BaseTextureProperties.Length; i++)
        {
            string propertyName = BaseTextureProperties[i];
            if (!material.HasProperty(propertyName))
                continue;

            Texture texture = material.GetTexture(propertyName);
            if (IsTexture2D(texture) && !LooksLikeGeneratedSurfaceTexture(texture))
                return true;
        }

        try
        {
            return material.HasProperty("_MainTex") && IsTexture2D(material.mainTexture) && !LooksLikeGeneratedSurfaceTexture(material.mainTexture);
        }
        catch
        {
            return false;
        }
    }

    private static bool HasUsefulColor(Material material)
    {
        if (material == null)
            return false;

        Color color = FindColor(material, Color.white);
        return !IsNearWhite(color) && !IsUnreadablyDark(color);
    }

    private static Color Intensify(Color color, float multiplier)
    {
        return new Color(
            Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier),
            color.a);
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) < 0.01f &&
               Mathf.Abs(a.g - b.g) < 0.01f &&
               Mathf.Abs(a.b - b.b) < 0.01f &&
               Mathf.Abs(a.a - b.a) < 0.01f;
    }

#if UNITY_EDITOR

    public static Material CreateEditorUrpLitMaterial(
        Material source,
        Renderer renderer)
    {
        Shader shader =
            FindRepairShader(
                false);

        if (shader == null)
            return null;

        // note: The registry builder persists this texture-preserving URP copy; imported HDRP materials remain untouched.
        Material effectiveSource =
            ResolveEffectiveSourceMaterial(
                source,
                renderer);

        Material converted =
            new Material(
                shader)
            {
                name =
                    (effectiveSource != null
                        ? effectiveSource.name
                        : "MissingMaterial") +
                    "_YQ_URP"
            };

        CopyMaterialSurface(
            effectiveSource,
            converted,
            false,
            renderer);

        return converted;
    }

    public static Material ResolveEditorMaterialForRuntimeBake(
        Material source,
        Renderer renderer)
    {
        if (renderer == null)
            return null;

        // Reuse the exact same effective-source logic used by runtime
        // URP repair. In Edit Mode this is allowed to use AssetDatabase
        // and the existing fallback-material search.
        Material effective =
            ResolveEffectiveSourceMaterial(
                source,
                renderer);

        // No override is necessary when the original material is already
        // the best source. Runtime repair can convert its shader normally.
        if (effective == null ||
            effective == source)
        {
            return null;
        }

        return effective;
    }

    public static void NormalizeEditorGeneratedAdapterSurface(
        Material material,
        Renderer renderer)
    {
        if (material == null)
            return;

        Texture baseTexture =
            FindTexture(material, BaseTextureProperties, out _);
        bool foliage =
            LooksLikeFoliageMaterial(material, renderer, baseTexture);
        bool transparent =
            renderer is ParticleSystemRenderer ||
            LooksSemanticallyTransparent(material, renderer);
        Color color = FindColor(material, Color.white);

        if (!foliage && !transparent)
        {
            // note: Existing project-owned adapters are normalized in place so a corrected pipeline does not require destructive vendor reimports.
            color.a = 1f;
            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
        }

        if (foliage)
            ConfigureFoliageCutout(material);
        else
            ConfigureSurfaceMode(material, transparent, false);

        EditorUtility.SetDirty(material);
    }

    private static Material FindEditorFallbackMaterial(
        Material source,
        Renderer renderer)
    {
        string searchText = BuildFallbackSearchText(source, renderer);
        if (string.IsNullOrWhiteSpace(searchText))
            return null;

        if (s_editorFallbackMaterialCache.TryGetValue(searchText, out Material cached))
            return cached;

        EnsureEditorMaterialCandidates();

        string[] tokens = ExtractFallbackTokens(searchText);
        Material best = null;
        int bestScore = 0;
        if (s_editorMaterialCandidates != null)
        {
            for (int i = 0; i < s_editorMaterialCandidates.Length; i++)
            {
                EditorMaterialCandidate candidate = s_editorMaterialCandidates[i];
                if (candidate == null || candidate.material == null)
                    continue;

                int score = ScoreEditorMaterialCandidate(candidate, tokens, searchText);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate.material;
                }
            }
        }

        if (bestScore < 80)
            best = null;

        s_editorFallbackMaterialCache[searchText] = best;
        return best;
    }

    private static void EnsureEditorMaterialCandidates()
    {
        if (s_editorMaterialCandidates != null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:Material");
        List<EditorMaterialCandidate> candidates = new List<EditorMaterialCandidate>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.Contains("/AssetTest/Repaired/"))
                continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            bool hasTexture = HasUsableBaseTexture(material);
            bool hasUsefulColor = HasUsefulColor(material);
            if (!hasTexture && !hasUsefulColor)
                continue;

            candidates.Add(new EditorMaterialCandidate
            {
                material = material,
                text = ToSearchText(material.name + " " + normalizedPath),
                hasTexture = hasTexture,
                hasUsefulColor = hasUsefulColor
            });
        }

        s_editorMaterialCandidates = candidates.ToArray();
    }

    private static string BuildFallbackSearchText(Material source, Renderer renderer)
    {
        string text = source != null ? source.name : string.Empty;
        if (renderer != null)
        {
            text += " " + renderer.name;
            Transform current = renderer.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                text += " " + current.name;
                current = current.parent;
                depth++;
            }
        }

        return ToSearchText(text);
    }

    private static string[] ExtractFallbackTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new string[0];

        string[] raw = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        List<string> tokens = new List<string>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            string token = raw[i];
            if (token.Length < 3 || ContainsAny(token, FallbackSkipTokens))
                continue;
            if (!tokens.Contains(token))
                tokens.Add(token);
        }

        return tokens.ToArray();
    }

    private static int ScoreEditorMaterialCandidate(EditorMaterialCandidate candidate, string[] tokens, string searchText)
    {
        int score = candidate.hasTexture ? 35 : 0;
        if (candidate.hasUsefulColor)
            score += 15;

        string familyToken = FindNumberedFamilyToken(tokens);
        if (!string.IsNullOrWhiteSpace(familyToken))
        {
            if (candidate.text.Contains(familyToken))
                score += 220;
            else if (ContainsConflictingNumberedFamily(candidate.text, familyToken))
                score -= 220;
            else
                score -= 60;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!candidate.text.Contains(token))
                continue;

            score += token.Length >= 7 ? 70 : 34;
            if (ContainsDigit(token))
                score += 28;
        }

        if (searchText.Contains(ToSearchText(candidate.material.name)))
            score += 120;

        return score;
    }

    private static string FindNumberedFamilyToken(string[] tokens)
    {
        if (tokens == null)
            return null;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!ContainsDigit(token))
                continue;

            for (int j = 0; j < NumberedFamilyPrefixes.Length; j++)
            {
                if (token.StartsWith(NumberedFamilyPrefixes[j]))
                    return token;
            }
        }

        return null;
    }

    private static bool ContainsConflictingNumberedFamily(string candidateText, string familyToken)
    {
        if (string.IsNullOrWhiteSpace(candidateText) || string.IsNullOrWhiteSpace(familyToken))
            return false;

        string prefix = GetLeadingLetters(familyToken);
        if (string.IsNullOrWhiteSpace(prefix) || !candidateText.Contains(prefix))
            return false;

        return !candidateText.Contains(familyToken);
    }

    private static string GetLeadingLetters(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int length = 0;
        while (length < value.Length && char.IsLetter(value[length]))
            length++;

        return length > 0 ? value.Substring(0, length) : string.Empty;
    }

    private static bool ContainsDigit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsDigit(value[i]))
                return true;
        }

        return false;
    }
#endif

    private static Texture FindTexture(Material material, string[] propertyNames)
    {
        return FindTexture(material, propertyNames, out _);
    }

    private static Texture FindTexture(Material material, string[] propertyNames, out string matchedPropertyName)
    {
        if (material == null || propertyNames == null)
        {
            matchedPropertyName = null;
            return null;
        }

        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            if (!material.HasProperty(propertyName))
                continue;

            Texture texture = material.GetTexture(propertyName);
            if (IsTexture2D(texture))
            {
                matchedPropertyName = propertyName;
                return texture;
            }
        }

        try
        {
            if (material.HasProperty("_MainTex"))
            {
                Texture mainTexture = material.mainTexture;
                if (IsTexture2D(mainTexture))
                {
                    matchedPropertyName = "_MainTex";
                    return mainTexture;
                }
            }
        }
        catch
        {
        }

        Texture bestTexture = FindBestMaterialTexture(material, propertyNames == NormalTextureProperties, out matchedPropertyName);
        if (bestTexture != null)
            return bestTexture;

#if UNITY_EDITOR
        return FindNearbyEditorTexture(material, ResolveTextureSearchKind(propertyNames), out matchedPropertyName);
#else
        return null;
#endif
    }

    private static Texture FindBestMaterialTexture(Material material, bool normalTexture, out string matchedPropertyName)
    {
        if (material == null)
        {
            matchedPropertyName = null;
            return null;
        }

        string[] propertyNames;
        try
        {
            propertyNames = material.GetTexturePropertyNames();
        }
        catch
        {
            matchedPropertyName = null;
            return null;
        }

        Texture bestTexture = null;
        matchedPropertyName = null;
        int bestScore = normalTexture ? 30 : 20;
        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            Texture texture = material.GetTexture(propertyName);
            if (!IsTexture2D(texture))
                continue;

            int score = ScoreTextureName(propertyName + " " + texture.name, normalTexture);
            if (score > bestScore)
            {
                bestScore = score;
                bestTexture = texture;
                matchedPropertyName = propertyName;
            }
        }

        return bestTexture;
    }

    private static int ScoreTextureName(string name, bool normalTexture)
    {
        string text = ToSearchText(name);
        if (normalTexture)
        return ContainsAny(text, NormalTextureNameHints) ? 120 : -50;

        if (ContainsAny(text, NonBaseTextureNameHints))
            return -50;

        int score = 0;
        if (ContainsAny(text, BaseTextureNameHints))
            score += 120;
        if (text.Contains("texture"))
            score += 10;
        return score;
    }

    private static bool IsTexture2D(Texture texture)
    {
        return texture is Texture2D;
    }

    private static TextureSearchKind ResolveTextureSearchKind(string[] propertyNames)
    {
        if (ReferenceEquals(propertyNames, NormalTextureProperties))
            return TextureSearchKind.Normal;
        if (ReferenceEquals(propertyNames, MetallicTextureProperties))
            return TextureSearchKind.Metallic;
        if (ReferenceEquals(propertyNames, OcclusionTextureProperties))
            return TextureSearchKind.Occlusion;
        if (ReferenceEquals(propertyNames, EmissionTextureProperties))
            return TextureSearchKind.Emission;

        return TextureSearchKind.Base;
    }

#if UNITY_EDITOR
    private static Texture FindNearbyEditorTexture(Material material, TextureSearchKind kind, out string matchedPropertyName)
    {
        matchedPropertyName = null;
        if (material == null)
            return null;

        string materialPath = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrWhiteSpace(materialPath))
            return null;

        string normalizedPath = materialPath.Replace('\\', '/');
        string cacheKey = normalizedPath + "|" + kind;
        if (s_editorNearbyTextureCache.TryGetValue(cacheKey, out Texture cached))
        {
            matchedPropertyName = "nearby:" + kind;
            return cached;
        }

        string folder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        List<string> searchFolders = new List<string>();
        AddSearchFolder(searchFolders, folder);
        AddNearbyTextureSearchFolders(searchFolders, folder);

        string searchText = ToSearchText(material.name + " " + normalizedPath);
        string[] materialTokens = ExtractFallbackTokens(searchText);
        Texture best = null;
        int bestScore = kind == TextureSearchKind.Base ? 35 : 75;
        for (int folderIndex = 0; folderIndex < searchFolders.Count; folderIndex++)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { searchFolders[folderIndex] });
            for (int i = 0; i < guids.Length; i++)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(texturePath))
                    continue;

                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                if (texture == null)
                    continue;

                int score = ScoreNearbyTexture(texturePath + " " + texture.name, kind, materialTokens);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = texture;
                }
            }
        }

        s_editorNearbyTextureCache[cacheKey] = best;
        if (best != null)
            matchedPropertyName = "nearby:" + kind;
        return best;
    }

    private static void AddNearbyTextureSearchFolders(List<string> searchFolders, string materialFolder)
    {
        string folder = NormalizeAssetPath(materialFolder);
        if (string.IsNullOrWhiteSpace(folder))
            return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string lowerFolder = folder.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(parent) && (lowerFolder.Contains("standard shader materials") || lowerFolder.EndsWith("/materials")))
            AddSearchFolder(searchFolders, parent);

        string root = TrimSuffixIgnoreCase(folder, "/Models/Materials");
        if (!string.IsNullOrWhiteSpace(root))
            AddTextureFoldersUnderRoot(searchFolders, root);

        root = TrimSuffixIgnoreCase(folder, "/_Standard Shader Materials");
        if (!string.IsNullOrWhiteSpace(root))
            AddTextureFoldersUnderRoot(searchFolders, root);

        root = TrimSuffixIgnoreCase(folder, "/Standard Shader Materials");
        if (!string.IsNullOrWhiteSpace(root))
            AddTextureFoldersUnderRoot(searchFolders, root);

        root = TrimSuffixIgnoreCase(folder, "/Materials");
        if (string.IsNullOrWhiteSpace(root))
            return;

        AddTextureFoldersUnderRoot(searchFolders, root);
        if (root.EndsWith("/Models"))
        {
            string familyRoot = Path.GetDirectoryName(root)?.Replace('\\', '/');
            AddTextureFoldersUnderRoot(searchFolders, familyRoot);
        }
    }

    private static void AddTextureFoldersUnderRoot(List<string> searchFolders, string root)
    {
        root = NormalizeAssetPath(root);
        if (string.IsNullOrWhiteSpace(root) || !AssetDatabase.IsValidFolder(root))
            return;

        for (int i = 0; i < NearbyTextureFolderNames.Length; i++)
            AddSearchFolder(searchFolders, root + "/" + NearbyTextureFolderNames[i]);

        string[] subFolders = AssetDatabase.GetSubFolders(root);
        for (int i = 0; i < subFolders.Length; i++)
        {
            string subFolder = NormalizeAssetPath(subFolders[i]);
            string leaf = GetPathLeaf(subFolder).ToLowerInvariant();
            if (leaf.StartsWith("tex") || leaf.Contains("texture"))
                AddSearchFolder(searchFolders, subFolder);
        }
    }

    private static void AddSearchFolder(List<string> searchFolders, string folder)
    {
        folder = NormalizeAssetPath(folder);
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder) || searchFolders.Contains(folder))
            return;

        searchFolders.Add(folder);
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
    }

    private static string TrimSuffixIgnoreCase(string value, string suffix)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(suffix))
            return null;

        return value.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - suffix.Length) : null;
    }

    private static string GetPathLeaf(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    private static int ScoreNearbyTexture(string textureText, TextureSearchKind kind, string[] materialTokens)
    {
        string text = ToSearchText(textureText);
        int score = 0;
        switch (kind)
        {
            case TextureSearchKind.Normal:
                score += ContainsAny(text, NormalTextureNameHints) ? 140 : -80;
                break;
            case TextureSearchKind.Metallic:
                score += ContainsAny(text, new[] { "metallic", "roughness", "mask" }) ? 140 : -80;
                break;
            case TextureSearchKind.Occlusion:
                score += ContainsAny(text, new[] { "ambientocclusion", "ambient occlusion", "occlusion" }) ? 140 : -80;
                break;
            case TextureSearchKind.Emission:
                score += ContainsAny(text, new[] { "emissive", "emission", "glow" }) ? 140 : -80;
                break;
            default:
                score += ContainsAny(text, NonBaseTextureNameHints) ? -100 : 0;
                score += ContainsAny(text, BaseTextureNameHints) || text.Contains("albedoopacity") ? 150 : 0;
                break;
        }

        if (materialTokens != null)
        {
            for (int i = 0; i < materialTokens.Length; i++)
            {
                string token = materialTokens[i];
                if (!string.IsNullOrWhiteSpace(token) && text.Contains(token))
                    score += token.Length >= 7 ? 36 : 18;
            }
        }

        return score;
    }
#endif

    private static bool ContainsAny(string text, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(text) || hints == null)
            return false;

        for (int i = 0; i < hints.Length; i++)
        {
            if (text.Contains(hints[i]))
                return true;
        }

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

        return new string(chars);
    }

    private static Color FindColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;

        for (int i = 0; i < BaseColorProperties.Length; i++)
        {
            string propertyName = BaseColorProperties[i];
            if (!material.HasProperty(propertyName))
                continue;

            try
            {
                return material.GetColor(propertyName);
            }
            catch
            {
            }
        }

        return fallback;
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material != null && IsTexture2D(texture) && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    private static void CopyTextureScaleOffsetIfPresent(Material source, Material target, string sourcePropertyName, string targetPropertyName)
    {
        if (source == null || target == null || string.IsNullOrWhiteSpace(sourcePropertyName))
            return;
        if (!source.HasProperty(sourcePropertyName) || !target.HasProperty(targetPropertyName))
            return;

        try
        {
            target.SetTextureScale(targetPropertyName, source.GetTextureScale(sourcePropertyName));
            target.SetTextureOffset(targetPropertyName, source.GetTextureOffset(sourcePropertyName));
        }
        catch
        {
        }
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void CopyFloatIfPresent(Material source, Material target, string sourcePropertyName, string targetPropertyName)
    {
        if (source == null ||
            target == null ||
            !source.HasProperty(sourcePropertyName) ||
            !target.HasProperty(targetPropertyName) ||
            !IsReadableFloatProperty(source, sourcePropertyName))
        {
            return;
        }

        try
        {
            // note: Some imported shader graphs report a property name but reject GetFloat for non-float slots.
            target.SetFloat(targetPropertyName, source.GetFloat(sourcePropertyName));
        }
        catch
        {
        }
    }

    private static bool IsVfxGraphRenderer(Renderer renderer)
    {
        // note: Avoid a hard assembly dependency while still identifying Unity VFX Graph renderers by type.
        return renderer != null &&
               string.Equals(renderer.GetType().Name, "VFXRenderer", System.StringComparison.Ordinal);
    }

    private static bool IsReadableFloatProperty(Material material, string propertyName)
    {
        if (material == null || material.shader == null || string.IsNullOrWhiteSpace(propertyName))
            return false;

        try
        {
            int propertyIndex =
                material.shader.FindPropertyIndex(
                    propertyName);

            if (propertyIndex < 0)
                return false;

            UnityEngine.Rendering.ShaderPropertyType propertyType =
                material.shader.GetPropertyType(
                    propertyIndex);

            // note: Unity logs before throwing when GetFloat touches non-float shader graph properties.
            return propertyType == UnityEngine.Rendering.ShaderPropertyType.Float ||
                   propertyType == UnityEngine.Rendering.ShaderPropertyType.Range;
        }
        catch
        {
            return false;
        }
    }

    private static void ConfigureSurfaceMode(Material material, bool transparent, bool additive)
    {
        if (material == null)
            return;

        if (!transparent)
        {
            SetFloatIfPresent(material, "_Surface", 0f);
            SetFloatIfPresent(material, "_Blend", 0f);
            SetFloatIfPresent(material, "_AlphaClip", 0f);
            SetFloatIfPresent(
                material,
                "_SrcBlend",
                (float)UnityEngine.Rendering.BlendMode.One);
            SetFloatIfPresent(
                material,
                "_DstBlend",
                (float)UnityEngine.Rendering.BlendMode.Zero);
            SetFloatIfPresent(material, "_ZWrite", 1f);
            material.renderQueue = -1;
            material.SetOverrideTag("RenderType", string.Empty);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            return;
        }

        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", additive ? 2f : 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static bool LooksLikeFoliageMaterial(Material source, Renderer renderer, Texture texture)
    {
        string text = ToSearchText(
            (source != null ? source.name : string.Empty) + " " +
            (texture != null ? texture.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty));

        return ContainsAny(text, new[] { "leaf", "leaves", "branch", "branches", "grass", "bush", "flower", "plant", "billboard", "treebillboard", "foliage" });
    }

    private static Color SanitizeCopiedMaterialColor(Material source, Renderer renderer, Texture texture, Color color)
    {
        if (!LooksLikeAlertRed(color))
            return color;

        string text = ToSearchText(
            (source != null ? source.name : string.Empty) + " " +
            (texture != null ? texture.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty));
        if (ContainsAny(text, new[] { "leaf", "leaves", "branch", "branches", "grass", "bush", "flower", "plant", "billboard", "treebillboard", "foliage" }))
            return new Color(0.22f, 0.42f, 0.20f, color.a);
        if (LooksLikePlaceholderText(text) || ContainsAny(text, new[] { "fallback", "proxy", "runtimeurp", "defaultdirty" }))
            return new Color(0.46f, 0.48f, 0.48f, color.a);

        return color;
    }

    private static void ConfigureFoliageCutout(Material material)
    {
        if (material == null)
            return;

        SetFloatIfPresent(material, "_Surface", 0f);
        SetFloatIfPresent(material, "_AlphaClip", 1f);
        SetFloatIfPresent(material, "_Cutoff", 0.38f);
        SetFloatIfPresent(material, "_Cull", 0f);
        SetFloatIfPresent(material, "_ZWrite", 1f);
        material.renderQueue = 2450;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHATEST_ON");
    }
}
