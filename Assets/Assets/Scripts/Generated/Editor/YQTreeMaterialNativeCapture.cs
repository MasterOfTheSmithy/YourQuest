#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class YQTreeMaterialNativeCapture
{
    private const string Menu = "YourQuest/Terrain Refactor/Capture Nearby Tree Materials (Read Only)";
    private const string Output = "outputs/TerrainRefactor_20261014_StreamingRetest/TreeMaterials/native";
    private const int Limit = 24;
    private static readonly string[] TreeHints = { "tree", "leaf", "leaves", "bark", "trunk", "canopy", "foliage", "billboard", "conifer", "pine", "needle", "frond", "oak", "birch", "palm" };
    private static readonly string[] ActorHints = { "actor", "npc", "enemy", "monster", "player", "character", "avatar" };
    private const int TexturePropertyLimit = 64;
    private const int PixelSide = 64, PixelTextureLimit = 16, ShaderMessageLimit = 16, ShaderLimit = 32;
    private static readonly string[] FloatProperties = { "_Surface", "_SurfaceType", "_AlphaClip", "_AlphaCutoffEnable", "_Cutoff", "_AlphaCutoff", "_Cull", "_ZWrite", "_SrcBlend", "_DstBlend" };
    private static readonly string[] ColorProperties = { "_BaseColor", "_Color", "_TintColor" };

    [Serializable] private sealed class Capture
    {
        public string evidence = "NATIVE_READ_ONLY_BINDING_CAPTURE; not a visual correctness or active-LOD PASS";
        public string limitation = "Nearest scene renderer sample, including inactive LODs; native Terrain tree instances are not enumerated. isVisible includes all cameras. LOD membership does not identify the active draw. BaseMap pixels use at most 16 unique 64x64 filtered GPU copies; this is not a full-resolution alpha or shader-draw PASS. Arrays are explicitly unavailable for pixel sampling. Cached shader messages do not certify all drawn variants.";
        public string utc, unityVersion, scene, profileId, profileName, worldId, worldSeed, spatialContentHash, renderPipeline;
        public int frame, playerInstanceId, candidateCount, selectedCount, pixelReadbacksAttempted, pixelTexturesSampled, pixelTexturesOmitted;
        public float captureSeconds;
        public Vector3 playerPosition, playerLogicalPosition;
        public string mainCameraHierarchy;
        public Vector3 mainCameraPosition, mainCameraForward;
        public List<RendererRecord> renderers = new List<RendererRecord>();
    }
    [Serializable] private sealed class RendererRecord
    {
        public int instanceId, meshInstanceId, subMeshCount;
        public string hierarchy, scene, rendererType, prefabAssetPath, meshName, meshAssetPath, selectionSemantics;
        public bool enabled, activeInHierarchy, isVisible, forceRenderingOff;
        public float boundsDistance, pivotDistance;
        public Bounds worldBounds;
        public Vector3 position, lossyScale;
        public List<LodRecord> lodMembership = new List<LodRecord>();
        public List<MaterialRecord> sharedMaterials = new List<MaterialRecord>();
        public string billboardName, billboardAssetPath;
        public int billboardInstanceId, billboardVertexCount, billboardIndexCount, billboardImageCount;
        public float billboardWidth, billboardHeight, billboardBottom;
        public bool hasPropertyBlock, rendererPropertyBlockEmpty;
        public List<string> meshVertexAttributes = new List<string>();
        public MaterialRecord billboardMaterial;
    }
    [Serializable] private sealed class LodRecord
    {
        public string groupHierarchy, fadeMode;
        public int groupInstanceId, level, levelCount;
        public bool enabled, animateCrossFading;
        public float screenRelativeTransitionHeight, fadeTransitionWidth, size;
        public Vector3 localReferencePoint;
    }
    [Serializable] private sealed class MaterialRecord
    {
        public int slot, instanceId, renderQueue, texturePropertyCount, texturePropertiesOmitted;
        public bool missing, persistent, shaderSupported, alphaTestKeyword, slotPropertyBlockRead, slotPropertyBlockEmpty;
        public string name, assetPath, shaderName, shaderAssetPath, pipelineTag, renderType;
        public string[] keywords;
        public List<FloatRecord> floats = new List<FloatRecord>();
        public List<ColorRecord> colors = new List<ColorRecord>();
        public List<TextureRecord> textures = new List<TextureRecord>();
        public ShaderRecord shaderMessages;
    }
    [Serializable] private sealed class FloatRecord { public string property; public float value; }
    [Serializable] private sealed class ColorRecord { public string property; public Color value; }
    [Serializable] private sealed class TextureRecord
    {
        public string property, name, assetPath, type, graphicsFormat, importerAlphaSource;
        public int instanceId, width, height, depth;
        public string dimension;
        public bool missing, importerAvailable, importerAlphaIsTransparency;
        public Vector2 scale, offset;
        public PixelRecord pixels;
    }
    [Serializable] private sealed class PixelRecord
    {
        public string evidence = "64x64 filtered linear GPU copy of current assigned BaseMap; no source/import modifications", unavailableReason;
        public bool sampled;
        public int sampleCount, alphaZero, alphaBelow038, alphaBelow04, alphaOpaque;
        public int[] alphaBins = new int[16];
        public float alphaMinimum, alphaMaximum;
        public Vector3 rgbMinimum, rgbMaximum, rgbMean;
    }
    [Serializable] private sealed class ShaderRecord
    {
        public string evidence = "Existing cached ShaderUtil messages; not shader compilation or active draw PASS", unavailableReason;
        public int messageCount, omitted;
        public List<ShaderMessageRecord> messages = new List<ShaderMessageRecord>();
    }
    [Serializable] private sealed class ShaderMessageRecord { public string severity, platform, file, message; public int line; }
    private sealed class CaptureReadContext
    {
        public readonly Dictionary<Texture, PixelRecord> pixels = new Dictionary<Texture, PixelRecord>();
        public readonly Dictionary<Shader, ShaderRecord> shaders = new Dictionary<Shader, ShaderRecord>();
        public int attempted, sampled, omitted;
    }

    private sealed class Candidate
    {
        public Renderer renderer;
        public string hierarchy, semantics;
        public float distanceSquared;
    }

    [MenuItem(Menu)]
    public static void CaptureNearby()
    {
        // note: This explicit one-shot menu performs scene reads and one new output-file write only; it installs no callbacks or test/input lease.
        if (!TryGetContext(out YQInvestorPlayerMotor motor, out string reason))
        {
            Debug.LogWarning("[YQTreeMaterials] Capture refused: " + reason);
            return;
        }
        var profiles = YQProfileSaveSystem.Instance;
        var world = WorldStateManager.Instance.State;
        var player = PlayerStateManager.Instance.state;
        var capture = new Capture
        {
            utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
            scene = motor.gameObject.scene.path, profileId = profiles.ActiveProfileId,
            profileName = profiles.FindProfile(profiles.ActiveProfileId).displayName,
            worldId = world.worldIdentity.worldId, worldSeed = Convert.ToString(world.generatedWorldPlan?.worldSeed),
            spatialContentHash = world.generatedWorldPlan?.spatialPlanV2?.contentHash,
            renderPipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null
                ? UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name : "Built-in",
            frame = Time.frameCount, playerInstanceId = motor.GetInstanceID(), playerPosition = motor.transform.position,
            playerLogicalPosition = motor.transform.position + player.renderOrigin
        };
        Camera camera = Camera.main;
        if (camera != null)
        {
            capture.mainCameraHierarchy = Hierarchy(camera.transform);
            capture.mainCameraPosition = camera.transform.position;
            capture.mainCameraForward = camera.transform.forward;
        }
        // note: A single unsorted scene query selects the nearest bounded set by world bounds; no renderer/material instance getter or scene mutation is used.
        var nearest = new List<Candidate>(Limit + 1);
        foreach (Renderer renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            // note: Actor ownership excludes characters without suppressing legitimate tree wind Animators.
            if (renderer == null || !(renderer is MeshRenderer || renderer is BillboardRenderer) ||
                !renderer.gameObject.scene.IsValid() || !renderer.gameObject.scene.isLoaded || EditorUtility.IsPersistent(renderer) ||
                // note: First-person presentation can live beneath the camera rather than the motor; it is not an ecology renderer.
                (camera != null && renderer.transform.IsChildOf(camera.transform)) ||
                renderer.GetComponentInParent<YQInvestorPlayerMotor>(true) != null ||
                renderer.GetComponentInParent<EntityInfo>(true) != null || renderer.GetComponentInParent<NpcDialogueAgent>(true) != null ||
                renderer.GetComponentInParent<YQInvestorEnemy>(true) != null || renderer.GetComponentInParent<CharacterController>(true) != null)
                continue;
            string hierarchy = Hierarchy(renderer.transform);
            if (Contains(hierarchy, ActorHints)) continue;
            string semantics = hierarchy;
            foreach (Material material in renderer.sharedMaterials)
                if (material != null) semantics += " " + material.name;
            if (renderer is BillboardRenderer billboard && billboard.billboard != null)
                semantics += " billboard " + billboard.billboard.name;
            if (!Contains(semantics, TreeHints)) continue;
            float distanceSquared = renderer.bounds.SqrDistance(capture.playerPosition);
            if (float.IsNaN(distanceSquared) || float.IsInfinity(distanceSquared)) continue;
            capture.candidateCount++;
            nearest.Add(new Candidate { renderer = renderer, hierarchy = hierarchy, semantics = semantics, distanceSquared = distanceSquared });
            nearest.Sort((a, b) => a.distanceSquared != b.distanceSquared
                ? a.distanceSquared.CompareTo(b.distanceSquared) : a.renderer.GetInstanceID().CompareTo(b.renderer.GetInstanceID()));
            if (nearest.Count > Limit) nearest.RemoveAt(Limit);
        }
        var reads = new CaptureReadContext();
        float captureStarted = Time.realtimeSinceStartup;
        foreach (Candidate candidate in nearest) capture.renderers.Add(ReadRenderer(candidate, capture.playerPosition, reads));
        capture.pixelReadbacksAttempted = reads.attempted; capture.pixelTexturesSampled = reads.sampled; capture.pixelTexturesOmitted = reads.omitted;
        capture.captureSeconds = Time.realtimeSinceStartup - captureStarted;
        capture.selectedCount = capture.renderers.Count;
        // note: Recheck canonical context before publishing; a new unique receipt never overwrites an earlier capture or writes beneath Assets.
        if (!TryGetContext(out YQInvestorPlayerMotor current, out reason) || current != motor ||
            profiles.ActiveProfileId != capture.profileId || !ReferenceEquals(world, WorldStateManager.Instance.State) ||
            !ReferenceEquals(player, PlayerStateManager.Instance.state))
            throw new InvalidOperationException("Tree material capture context changed before output.");
        string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Output));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "tree-materials-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff") + ".json");
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) writer.Write(JsonUtility.ToJson(capture, true));
        Debug.Log("[YQTreeMaterials] NATIVE_READ_ONLY_BINDING_CAPTURE: " + capture.selectedCount + "/" + capture.candidateCount + " candidate renderers: " + path);
    }

    [MenuItem(Menu, true)]
    private static bool ValidateCapture() => TryGetContext(out _, out _);

    private static bool TryGetContext(out YQInvestorPlayerMotor motor, out string reason)
    {
        // note: Reuse the established named disposable-profile/released-game boundary without acquiring or modifying a developer test session.
        motor = YQInvestorPlayerMotor.ActiveMotor;
        var profiles = YQProfileSaveSystem.Instance;
        // note: Unity singleton references also require Unity null comparisons; null-conditional access does not detect destroyed native objects.
        var profile = profiles != null ? profiles.FindProfile(profiles.ActiveProfileId) : null;
        string name = profile != null ? profile.displayName : null;
        var worldManager = WorldStateManager.Instance;
        var playerManager = PlayerStateManager.Instance;
        var world = worldManager != null ? worldManager.State : null;
        bool valid = EditorApplication.isPlaying && !Application.isBatchMode && !EditorApplication.isCompiling &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased && !YQDeveloperTestSession.Active &&
            !YQDeveloperConsoleGate.BlocksPersistence && name != null && name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal) &&
            motor != null && motor.IsAuthoritative && motor.CanProcessMovementInput && motor.gameObject.activeInHierarchy &&
            motor.gameObject.scene.IsValid() && motor.gameObject.scene.isLoaded && playerManager != null && playerManager.state != null &&
            world?.worldIdentity != null && string.Equals(world.worldIdentity.ownerProfileId, profiles.ActiveProfileId, StringComparison.Ordinal);
        reason = valid ? string.Empty : "Requires the released Terrain Apron Playtest profile, its current authoritative player/world, and no developer test lease or persistence barrier.";
        return valid;
    }

    private static RendererRecord ReadRenderer(Candidate candidate, Vector3 playerPosition, CaptureReadContext reads)
    {
        // note: Record imported-source identities separately from live bindings; prefab lineage may be absent on runtime clones, so mesh asset paths remain visible.
        Renderer renderer = candidate.renderer;
        // note: BillboardRenderer owns a BillboardAsset, not a MeshFilter; Unity's missing-component sentinel must pass its overloaded null check before dereferencing.
        MeshFilter filter = renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>() : null;
        Mesh mesh = filter != null ? filter.sharedMesh : null;
        var record = new RendererRecord
        {
            instanceId = renderer.GetInstanceID(), hierarchy = candidate.hierarchy, selectionSemantics = candidate.semantics,
            scene = renderer.gameObject.scene.path, rendererType = renderer.GetType().FullName,
            prefabAssetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(renderer.gameObject),
            enabled = renderer.enabled, activeInHierarchy = renderer.gameObject.activeInHierarchy, isVisible = renderer.isVisible,
            forceRenderingOff = renderer.forceRenderingOff, boundsDistance = Mathf.Sqrt(candidate.distanceSquared),
            pivotDistance = Vector3.Distance(renderer.transform.position, playerPosition), worldBounds = renderer.bounds,
            position = renderer.transform.position, lossyScale = renderer.transform.lossyScale,
            meshInstanceId = mesh != null ? mesh.GetInstanceID() : 0, meshName = mesh != null ? mesh.name : null,
            meshAssetPath = AssetDatabase.GetAssetPath(mesh), subMeshCount = mesh != null ? mesh.subMeshCount : 0
        };
        // note: Read property-block presence and vertex declarations without instancing materials or accessing unreadable mesh pixels/vertices.
        record.hasPropertyBlock = renderer.HasPropertyBlock();
        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        record.rendererPropertyBlockEmpty = block.isEmpty;
        if (mesh != null)
            foreach (var attribute in mesh.GetVertexAttributes())
                record.meshVertexAttributes.Add(attribute.attribute + ":" + attribute.format + ":" + attribute.dimension + ":stream" + attribute.stream);
        Material[] materials = renderer.sharedMaterials;
        for (int slot = 0; slot < materials.Length; slot++)
        {
            var materialRecord = ReadMaterial(materials[slot], slot, reads);
            block.Clear();
            renderer.GetPropertyBlock(block, slot);
            materialRecord.slotPropertyBlockRead = true;
            materialRecord.slotPropertyBlockEmpty = block.isEmpty;
            record.sharedMaterials.Add(materialRecord);
        }
        foreach (LODGroup group in renderer.GetComponentsInParent<LODGroup>(true))
        {
            LOD[] levels = group.GetLODs();
            for (int level = 0; level < levels.Length; level++)
                foreach (Renderer member in levels[level].renderers)
                    if (member == renderer) record.lodMembership.Add(new LodRecord
                    {
                        groupHierarchy = Hierarchy(group.transform), groupInstanceId = group.GetInstanceID(), level = level, levelCount = levels.Length,
                        enabled = group.enabled, fadeMode = group.fadeMode.ToString(), animateCrossFading = group.animateCrossFading,
                        screenRelativeTransitionHeight = levels[level].screenRelativeTransitionHeight, fadeTransitionWidth = levels[level].fadeTransitionWidth,
                        size = group.size, localReferencePoint = group.localReferencePoint
                    });
        }
        if (renderer is BillboardRenderer billboard && billboard.billboard != null)
        {
            // note: A BillboardAsset supplies its own vertex/atlas contract; an empty inherited material slot does not authorize a generic mesh shader.
            record.billboardVertexCount = billboard.billboard.vertexCount;
            record.billboardIndexCount = billboard.billboard.indexCount;
            record.billboardImageCount = billboard.billboard.imageCount;
            record.billboardWidth = billboard.billboard.width;
            record.billboardHeight = billboard.billboard.height;
            record.billboardBottom = billboard.billboard.bottom;
            record.billboardInstanceId = billboard.billboard.GetInstanceID();
            record.billboardName = billboard.billboard.name;
            record.billboardAssetPath = AssetDatabase.GetAssetPath(billboard.billboard);
            record.billboardMaterial = ReadMaterial(billboard.billboard.material, -1, reads);
        }
        return record;
    }

    private static MaterialRecord ReadMaterial(Material material, int slot, CaptureReadContext reads)
    {
        // note: Shared materials and assigned textures remain unchanged; only bounded temporary GPU copies and cached shader diagnostics supplement the binding receipt.
        var record = new MaterialRecord { slot = slot, missing = material == null };
        if (material == null) return record;
        record.instanceId = material.GetInstanceID(); record.name = material.name;
        record.persistent = EditorUtility.IsPersistent(material); record.assetPath = AssetDatabase.GetAssetPath(material);
        record.shaderName = material.shader != null ? material.shader.name : null;
        record.shaderAssetPath = AssetDatabase.GetAssetPath(material.shader);
        record.shaderSupported = material.shader != null && material.shader.isSupported;
        record.shaderMessages = ReadShaderMessages(material.shader, reads);
        record.pipelineTag = material.GetTag("RenderPipeline", false, string.Empty);
        record.renderType = material.GetTag("RenderType", false, string.Empty);
        record.renderQueue = material.renderQueue; record.keywords = material.shaderKeywords;
        record.alphaTestKeyword = material.IsKeywordEnabled("_ALPHATEST_ON");
        foreach (string property in FloatProperties)
            if (material.HasProperty(property)) record.floats.Add(new FloatRecord { property = property, value = material.GetFloat(property) });
        foreach (string property in ColorProperties)
            if (material.HasProperty(property)) record.colors.Add(new ColorRecord { property = property, value = material.GetColor(property) });
        // note: Authored CTI bark arrays and billboard normal atlases use custom properties. Enumerate only this material's declared texture bindings, with an explicit bounded receipt.
        string[] textureProperties = material.GetTexturePropertyNames();
        record.texturePropertyCount = textureProperties.Length;
        record.texturePropertiesOmitted = Mathf.Max(0, textureProperties.Length - TexturePropertyLimit);
        for (int index = 0; index < Mathf.Min(textureProperties.Length, TexturePropertyLimit); index++)
        {
            string property = textureProperties[index];
            // note: Custom shaders may reuse a familiar texture-property name for another type; query only actual Texture properties.
            int propertyIndex = material.shader != null ? material.shader.FindPropertyIndex(property) : -1;
            if (propertyIndex < 0 || material.shader.GetPropertyType(propertyIndex) != UnityEngine.Rendering.ShaderPropertyType.Texture) continue;
            Texture texture = material.GetTexture(property);
            var entry = new TextureRecord { property = property, missing = texture == null, scale = material.GetTextureScale(property), offset = material.GetTextureOffset(property) };
            if (texture != null)
            {
                entry.instanceId = texture.GetInstanceID(); entry.name = texture.name; entry.assetPath = AssetDatabase.GetAssetPath(texture);
                entry.type = texture.GetType().FullName; entry.width = texture.width; entry.height = texture.height; entry.graphicsFormat = texture.graphicsFormat.ToString();
                entry.dimension = texture.dimension.ToString();
                entry.depth = texture is Texture2DArray array ? array.depth : texture is Texture3D volume ? volume.depth : 1;
                var importer = string.IsNullOrEmpty(entry.assetPath) ? null : AssetImporter.GetAtPath(entry.assetPath) as TextureImporter;
                if (importer != null) { entry.importerAvailable = true; entry.importerAlphaSource = importer.alphaSource.ToString(); entry.importerAlphaIsTransparency = importer.alphaIsTransparency; }
            }
            if (property == "_BaseMap" || property == "_BaseMapArray") entry.pixels = ReadBasePixels(texture, reads);
            record.textures.Add(entry);
        }
        return record;
    }

    private static ShaderRecord ReadShaderMessages(Shader shader, CaptureReadContext reads)
    {
        if (shader == null) return new ShaderRecord { unavailableReason = "no shader" };
        if (reads.shaders.TryGetValue(shader, out var cached)) return cached;
        if (reads.shaders.Count >= ShaderLimit) return new ShaderRecord { unavailableReason = "32 unique cached-shader query budget exhausted" };
        var result = new ShaderRecord(); reads.shaders.Add(shader, result);
        try
        {
            var messages = ShaderUtil.GetShaderMessages(shader);
            result.messageCount = messages.Length; result.omitted = Mathf.Max(0, messages.Length - ShaderMessageLimit);
            for (int index = 0; index < Mathf.Min(messages.Length, ShaderMessageLimit); index++)
                result.messages.Add(new ShaderMessageRecord { severity = messages[index].severity.ToString(), platform = messages[index].platform.ToString(), file = messages[index].file, line = messages[index].line, message = messages[index].message });
        }
        catch (Exception exception) { result.unavailableReason = exception.GetType().Name + ": " + exception.Message; }
        return result;
    }

    private static PixelRecord ReadBasePixels(Texture texture, CaptureReadContext reads)
    {
        if (texture == null) return new PixelRecord { unavailableReason = "no assigned BaseMap" };
        if (reads.pixels.TryGetValue(texture, out var cached)) return cached;
        var result = new PixelRecord(); reads.pixels.Add(texture, result);
        // note: No generic shader substitutes for a texture-array or BillboardAsset contract. This probe only copies an assigned 2D BaseMap; array slice sampling remains unavailable.
        if (!(texture is Texture2D)) { result.unavailableReason = "texture arrays/volumes are not sampled by this 2D copy probe"; return result; }
        if (reads.attempted >= PixelTextureLimit) { reads.omitted++; result.unavailableReason = "16 unique BaseMap GPU-copy budget exhausted"; return result; }
        reads.attempted++;
        RenderTexture previous = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        RenderTexture copy = null;
        Texture2D cpu = null;
        try
        {
            // note: Readback works for non-readable imported textures without changing their importers. Default copy blit never replaces or invokes the authored tree material.
            copy = RenderTexture.GetTemporary(PixelSide, PixelSide, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            GL.sRGBWrite = false;
            Graphics.Blit(texture, copy);
            RenderTexture.active = copy;
            cpu = new Texture2D(PixelSide, PixelSide, TextureFormat.RGBA32, false, true) { hideFlags = HideFlags.HideAndDontSave };
            cpu.ReadPixels(new Rect(0, 0, PixelSide, PixelSide), 0, 0, false);
            Color32[] pixels = cpu.GetPixels32();
            result.alphaMinimum = 1f; result.alphaMaximum = 0f;
            result.rgbMinimum = Vector3.one; result.rgbMaximum = Vector3.zero;
            foreach (Color32 pixel in pixels)
            {
                float alpha = pixel.a / 255f;
                Vector3 rgb = new Vector3(pixel.r, pixel.g, pixel.b) / 255f;
                result.alphaMinimum = Mathf.Min(result.alphaMinimum, alpha); result.alphaMaximum = Mathf.Max(result.alphaMaximum, alpha);
                result.rgbMinimum = Vector3.Min(result.rgbMinimum, rgb); result.rgbMaximum = Vector3.Max(result.rgbMaximum, rgb); result.rgbMean += rgb;
                result.alphaBins[pixel.a / 16]++;
                if (pixel.a == 0) result.alphaZero++;
                if (alpha < .38f) result.alphaBelow038++;
                if (alpha < .4f) result.alphaBelow04++;
                if (pixel.a == 255) result.alphaOpaque++;
            }
            result.sampleCount = pixels.Length; result.rgbMean /= Mathf.Max(1, pixels.Length); result.sampled = true; reads.sampled++;
        }
        catch (Exception exception) { result.unavailableReason = exception.GetType().Name + ": " + exception.Message; }
        finally
        {
            // note: Restore the exact prior render-target and sRGB-write state even when copy/readback fails; dispose only temporary objects owned by this one-shot probe.
            RenderTexture.active = previous;
            GL.sRGBWrite = previousSrgbWrite;
            if (cpu != null) UnityEngine.Object.DestroyImmediate(cpu);
            if (copy != null) RenderTexture.ReleaseTemporary(copy);
        }
        return result;
    }

    private static string Hierarchy(Transform transform)
    {
        // note: Include sibling indices to disambiguate repeated names without changing names or attaching diagnostic components.
        var parts = new Stack<string>();
        for (Transform node = transform; node != null; node = node.parent) parts.Push(node.name + "[" + node.GetSiblingIndex() + "]");
        return string.Join("/", parts);
    }

    private static bool Contains(string text, string[] hints)
    {
        foreach (string hint in hints) if (text.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }
}
#endif
