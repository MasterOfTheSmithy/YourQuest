using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// note: Publish a continuous bridge treatment over an accepted road profile while leaving the carved river channel open below.
public sealed class YQGeneratedRiverBridge : MonoBehaviour
{
    internal const float CooperativeSliceSeconds = 0.005f;
    private const float BridgeVisualDeckCenterOffset = 0.08f;
    private const float BridgeVisualDeckThickness = 0.28f;
    private const float WalkSurfaceTopOffset = BridgeVisualDeckCenterOffset + BridgeVisualDeckThickness * 0.5f;
    private const float WalkSurfaceThickness = 0.047f;
    private const float WalkSurfaceBottomOffset = WalkSurfaceTopOffset - WalkSurfaceThickness;
    private const float WalkSurfaceSupportTopInset = 0.01f;
    private const float WalkSurfaceSupportThickness = 0.5f;
    private readonly List<Mesh> meshes = new List<Mesh>();
    private readonly List<Material> materials = new List<Material>();
    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorShaderId = Shader.PropertyToID("_Color");
    public const string AssetPath = "Assets/HIVEMIND/MedievalKingdom/HDRP(Default)/Art/Prefabs/SM_Bridge_Small_01.prefab";
    private const string WoodBridgeAssetPath = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_MediumWoodenBridge_Floor.prefab";
    private const string SmallBridgeAssetPath = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_MiniBridge_Body.prefab";
    private const string RepairPlankAssetPath = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Plank01.prefab";
    // note: Registry rebuilds must retain every source used by the deterministic bridge variants, including the stone bridge absent from semantic scatter palettes.
    public static IReadOnlyList<string> RequiredAssetPaths { get; } = Array.AsReadOnly(new[] { AssetPath, WoodBridgeAssetPath, SmallBridgeAssetPath, RepairPlankAssetPath });

    public static void Build(Transform parent, IReadOnlyList<Vector3> road, float width, YQRuntimeWorldAssetRegistry registry)
    {
        RunSynchronously(BuildRoutine(parent, road, width, registry));
    }

    // note: The streamer uses this cooperative path so bridge geometry stays hidden under its chunk root while each expensive Unity object is built across bounded slices.
    public static IEnumerator BuildRoutine(
        Transform parent,
        IReadOnlyList<Vector3> road,
        float width,
        YQRuntimeWorldAssetRegistry registry,
        Action<string, float> substageTelemetry = null)
    {
        if (road == null || road.Count < 2)
            throw new InvalidOperationException("Bridge needs two bank contacts.");
        if (parent == null || registry == null)
            throw new InvalidOperationException("Bridge needs a live parent and runtime asset registry.");

        // note: Long crossings repeat supported arches rather than stretching one imported bridge across an entire lake.
        if (road.Count > 28)
        {
            for (int start = 0; start < road.Count - 1; start += 24)
            {
                List<Vector3> section = new List<Vector3>();
                for (int index = start; index <= Mathf.Min(start + 24, road.Count - 1); index++)
                    section.Add(road[index]);
                yield return BuildRoutine(parent, section, width, registry, substageTelemetry);
            }
            yield break;
        }

        // note: Keep visual style deterministic from accepted route coordinates while yielding independently through every costly deck and collider stage.
        int variant = Mathf.Abs(StableHash(road[0].x.ToString("F2") + "|" + road[0].z.ToString("F2"))) % 4;
        GameObject root = new GameObject("Generated_RiverBridge_" + VariantName(variant));
        root.transform.SetParent(parent, false);
        YQGeneratedRiverBridge owner = root.AddComponent<YQGeneratedRiverBridge>();

        float stageStarted = Time.realtimeSinceStartup;
        bool materialNeedsVariantTint = TryResolveBridgeRuntimeMaterial(registry, variant, out Material bridgeMaterial);
        RecordBuildSubstage(substageTelemetry, "bridgeMaterialRegistryLookup", stageStarted);
        if (!materialNeedsVariantTint)
        {
            // note: Retain the compatibility repair path for an asset whose persisted approved slot cannot be reused directly.
            stageStarted = Time.realtimeSinceStartup;
            bridgeMaterial = ResolveBridgeMaterial(registry, variant);
            RecordBuildSubstage(substageTelemetry, "bridgeMaterialFallbackRepair", stageStarted);
            owner.materials.Add(bridgeMaterial);
        }
        yield return null;

        MaterialPropertyBlock tintBlock = materialNeedsVariantTint ? new MaterialPropertyBlock() : null;
        yield return BuildContinuousDeckVisualRoutine(root.transform, road, width, bridgeMaterial, variant, tintBlock, substageTelemetry);
        yield return BuildBridgeRailsRoutine(root.transform, road, width, bridgeMaterial, variant, tintBlock, substageTelemetry);

        stageStarted = Time.realtimeSinceStartup;
        Mesh deckCollision = BuildWalkSurfaceMesh(road, width);
        RecordBuildSubstage(substageTelemetry, "bridgeCollisionMesh", stageStarted);
        if (deckCollision != null)
        {
            stageStarted = Time.realtimeSinceStartup;
            GameObject deck = new GameObject("StoneBridgeWalkSurface");
            deck.transform.SetParent(root.transform, false);
            deck.AddComponent<MeshCollider>().sharedMesh = deckCollision;
            owner.meshes.Add(deckCollision);
            RecordBuildSubstage(substageTelemetry, "bridgeCollisionDeck", stageStarted);
            yield return null;

            // note: Preserve per-segment hidden support at curved seams while spreading collider creation across the same bounded work lane.
            float supportSliceStartedAt = Time.realtimeSinceStartup;
            for (int segment = 0; segment < road.Count - 1; segment++)
            {
                stageStarted = Time.realtimeSinceStartup;
                Vector3 a = road[segment];
                Vector3 b = road[segment + 1];
                Vector3 delta = b - a;
                Vector3 flat = new Vector3(delta.x, 0f, delta.z);
                float length = flat.magnitude;
                if (length >= 0.05f)
                {
                    float segmentLength = delta.magnitude;
                    Vector3 segmentForward = delta / segmentLength;
                    Vector3 segmentSide = new Vector3(-flat.z, 0f, flat.x) / length;
                    // note: Match each hidden support's top plane to the accepted sloped deck so its retained depth cannot form a raised entry lip.
                    Vector3 deckNormal = Vector3.Cross(segmentSide, segmentForward).normalized;
                    GameObject support = new GameObject("StoneBridgeWalkSurface");
                    support.transform.SetParent(root.transform, false);
                    // note: Keep the support just beneath the mesh top so the mesh remains the authoritative walk plane at seams.
                    support.transform.position = Vector3.Lerp(a, b, 0.5f) + Vector3.up * (WalkSurfaceTopOffset - WalkSurfaceSupportTopInset) - deckNormal * (WalkSurfaceSupportThickness * 0.5f);
                    support.transform.rotation = Quaternion.LookRotation(segmentForward, deckNormal);
                    BoxCollider box = support.AddComponent<BoxCollider>();
                    // note: Give the hidden support a narrow safety shoulder so the certified capsule remains supported at bank-turn corners without widening the visible deck.
                    float supportLength = (length + 0.18f) * (segmentLength / length);
                    box.size = new Vector3(width + 2f, WalkSurfaceSupportThickness, supportLength);
                }
                RecordBuildSubstage(substageTelemetry, "bridgeSupportCollider", stageStarted);
                if (Time.realtimeSinceStartup - supportSliceStartedAt >= CooperativeSliceSeconds)
                {
                    yield return null;
                    supportSliceStartedAt = Time.realtimeSinceStartup;
                }
            }
        }
    }

    private static IEnumerator BuildContinuousDeckVisualRoutine(
        Transform parent,
        IReadOnlyList<Vector3> road,
        float width,
        Material material,
        int variant,
        MaterialPropertyBlock tintBlock,
        Action<string, float> substageTelemetry)
    {
        if (material == null)
            yield break;
        // note: Segment-local deck blocks follow accepted bends while each primitive is published into the still-hidden chunk root in its own frame slice.
        float sliceStartedAt = Time.realtimeSinceStartup;
        for (int index = 0; index < road.Count - 1; index++)
        {
            float stageStarted = Time.realtimeSinceStartup;
            Vector3 a = road[index];
            Vector3 b = road[index + 1];
            Vector3 delta = b - a;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float length = flat.magnitude;
            if (length >= 0.05f)
            {
                GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                deck.name = variant == 1 ? "WoodBridgeDeck" : variant == 2 ? "DisheveledBridgeDeck" : variant == 3 ? "RepairedBridgeDeck" : "StoneBridgeDeck";
                deck.transform.SetParent(parent, false);
                deck.transform.position = Vector3.Lerp(a, b, .5f) + Vector3.up * BridgeVisualDeckCenterOffset;
                deck.transform.rotation = Quaternion.LookRotation(flat / length, Vector3.up);
                deck.transform.localScale = new Vector3(Mathf.Max(2f, width), BridgeVisualDeckThickness, length + .16f);
                Collider collider = deck.GetComponent<Collider>();
                if (collider != null) UnityEngine.Object.Destroy(collider);
                Renderer renderer = deck.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                    ApplyVariantTint(renderer, material, variant, tintBlock);
                }
            }
            RecordBuildSubstage(substageTelemetry, "bridgeDeckSegment", stageStarted);
            if (Time.realtimeSinceStartup - sliceStartedAt >= CooperativeSliceSeconds)
            {
                yield return null;
                sliceStartedAt = Time.realtimeSinceStartup;
            }
        }
    }

    private static IEnumerator BuildBridgeRailsRoutine(
        Transform parent,
        IReadOnlyList<Vector3> road,
        float width,
        Material material,
        int variant,
        MaterialPropertyBlock tintBlock,
        Action<string, float> substageTelemetry)
    {
        if (material == null || variant == 2 || variant == 3)
            yield break;
        // note: Sparse posts preserve the current bridge silhouette and open river view while each post remains individually budgeted.
        float half = Mathf.Max(1f, width * .5f - .22f);
        float sliceStartedAt = Time.realtimeSinceStartup;
        for (int index = 0; index < road.Count; index += 2)
        {
            if (index == road.Count - 1 && road.Count > 2)
                continue;
            Vector3 tangent = index == road.Count - 1 ? road[index] - road[index - 1] : road[Mathf.Min(index + 1, road.Count - 1)] - road[index];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < .01f)
                continue;
            tangent.Normalize();
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            for (int sideIndex = -1; sideIndex <= 1; sideIndex += 2)
            {
                float stageStarted = Time.realtimeSinceStartup;
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "BridgeRailPost";
                post.transform.SetParent(parent, false);
                post.transform.position = road[index] + side * (half * sideIndex) + Vector3.up * .65f;
                post.transform.localScale = new Vector3(.18f, 1.15f, .18f);
                Collider collider = post.GetComponent<Collider>();
                // note: Disable the primitive collider immediately so same-frame route certification cannot sweep a rail post scheduled for deferred destruction.
                if (collider != null) collider.enabled = false;
                Renderer renderer = post.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterial = material;
                    ApplyVariantTint(renderer, material, variant, tintBlock);
                }
                RecordBuildSubstage(substageTelemetry, "bridgeRailPost", stageStarted);
                if (Time.realtimeSinceStartup - sliceStartedAt >= CooperativeSliceSeconds)
                {
                    yield return null;
                    sliceStartedAt = Time.realtimeSinceStartup;
                }
            }
        }
    }

    private static void RecordBuildSubstage(Action<string, float> telemetry, string stage, float startedAt)
    {
        telemetry?.Invoke(stage, Mathf.Max(0f, Time.realtimeSinceStartup - startedAt));
    }

    private static bool TryResolveBridgeRuntimeMaterial(
        YQRuntimeWorldAssetRegistry registry,
        int variant,
        out Material material)
    {
        material = null;
        if (registry == null)
            return false;

        string[] candidates = GetBridgeMaterialCandidates(variant);
        for (int index = 0; index < candidates.Length; index++)
        {
            string candidatePath = candidates[index];
            GameObject prefab = registry.ResolvePrefab(candidatePath);
            if (prefab == null)
                continue;

            if (!registry.TryResolveFirstRuntimeMaterial(candidatePath, out Material registeredMaterial) ||
                !IsAuthoredBridgeMaterialUsable(registeredMaterial))
            {
                // note: A valid imported URP source material is also safe to share when an older registry has no baked adapter entry.
                Renderer sourceRenderer = prefab.GetComponentInChildren<Renderer>(true);
                registeredMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            }

            if (!IsAuthoredBridgeMaterialUsable(registeredMaterial))
                continue;

            material = registeredMaterial;
            return true;
        }
        return false;
    }

    private static bool IsAuthoredBridgeMaterialUsable(Material material)
    {
        return material != null &&
            YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(material) &&
            material.HasProperty("_BaseMap") &&
            material.GetTexture("_BaseMap") != null;
    }

    private static void ApplyVariantTint(
        Renderer renderer,
        Material material,
        int variant,
        MaterialPropertyBlock tintBlock)
    {
        if (renderer == null || material == null || tintBlock == null)
            return;

        // note: Keep the established four bridge tints per renderer so all crossings can share the registry material without creating per-bridge material copies.
        Color tint = variant == 2
            ? new Color(.55f, .38f, .22f, 1f)
            : variant == 3
                ? new Color(.72f, .54f, .32f, 1f)
                : variant == 1
                    ? new Color(.82f, .62f, .42f, 1f)
                    : Color.white;
        tintBlock.Clear();
        if (material.HasProperty(BaseColorShaderId))
            tintBlock.SetColor(BaseColorShaderId, material.GetColor(BaseColorShaderId) * tint);
        else if (material.HasProperty(ColorShaderId))
            tintBlock.SetColor(ColorShaderId, material.GetColor(ColorShaderId) * tint);
        renderer.SetPropertyBlock(tintBlock);
    }

    private static string[] GetBridgeMaterialCandidates(int variant)
    {
        return variant == 1
            ? new[] { WoodBridgeAssetPath, AssetPath }
            : variant == 3
                ? new[] { RepairPlankAssetPath, WoodBridgeAssetPath, AssetPath }
                : variant == 2
                    ? new[] { SmallBridgeAssetPath, AssetPath, WoodBridgeAssetPath }
                    : new[] { AssetPath, WoodBridgeAssetPath, SmallBridgeAssetPath };
    }

    private static void RunSynchronously(IEnumerator root)
    {
        // note: Preserve the public synchronous adapter for compatibility callers while the production streamer consumes BuildRoutine one yielded object at a time.
        if (root == null)
            return;
        Stack<IEnumerator> stack = new Stack<IEnumerator>();
        stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    stack.Pop();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                    stack.Push(nested);
            }
        }
        finally
        {
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch { }
            }
        }
    }

    private static Material ResolveBridgeMaterial(YQRuntimeWorldAssetRegistry registry, int variant)
    {
        string[] candidates = GetBridgeMaterialCandidates(variant);
        for (int index = 0; index < candidates.Length; index++)
        {
            GameObject prefab = registry != null ? registry.ResolvePrefab(candidates[index]) : null;
            if (prefab == null) continue;
            // note: Resolve the same renderer-slot adapters as a normal prefab spawn. The stone bridge uses an HDRP Shader Graph whose name does not contain "HDRP", bypassing the old name-only shader swap.
            var staging = new GameObject("YQ_BridgeMaterialBinding") { hideFlags = HideFlags.HideAndDontSave };
            staging.SetActive(false);
            Material material;
            try
            {
                GameObject sourceInstance = UnityEngine.Object.Instantiate(prefab, staging.transform, false);
                registry.ApplyMaterialOverrides(candidates[index], sourceInstance);
                Renderer renderer = sourceInstance.GetComponentInChildren<Renderer>(true);
                if (renderer == null || !YQRuntimeUrpMaterialRepair.IsRuntimeMaterialUsable(renderer.sharedMaterial))
                    throw new InvalidOperationException("Bridge material binding is invalid: " + candidates[index]);
                material = new Material(renderer.sharedMaterial) { name = "YQ_BridgeVariant_" + VariantName(variant), hideFlags = HideFlags.DontSave };
            }
            finally
            {
                // note: The bridge owns its material copy; the inactive source hierarchy is never published and is released even when binding fails.
                if (Application.isPlaying) UnityEngine.Object.Destroy(staging);
                else UnityEngine.Object.DestroyImmediate(staging);
            }
            Color tint = variant == 2 ? new Color(.55f, .38f, .22f, 1f) : variant == 3 ? new Color(.72f, .54f, .32f, 1f) : variant == 1 ? new Color(.82f, .62f, .42f, 1f) : Color.white;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", material.GetColor("_BaseColor") * tint);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", material.GetColor("_Color") * tint);
            return material;
        }
        // note: A missing registered source is a binding failure, not permission to mask it with an untextured generated material.
        throw new InvalidOperationException("No registered bridge material source for variant " + VariantName(variant));
    }

    private static string VariantName(int variant)
    {
        return variant == 1 ? "Wood" : variant == 2 ? "Disheveled" : variant == 3 ? "Repaired" : "Stone";
    }

    private static int StableHash(string value)
    {
        unchecked { int hash = 23; for (int index = 0; index < value.Length; index++) hash = hash * 31 + value[index]; return hash & 0x7fffffff; }
    }

    private static Mesh BuildWalkSurfaceMesh(IReadOnlyList<Vector3> road, float width)
    {
        // note: Carry the thin walk surface through both bank samples so the certified road never loses support at the bridge-to-bank seam.
        int first = 0;
        int last = road.Count - 1;
        int count = last - first + 1;
        if (count < 2) return null;
        var vertices = new Vector3[count * 4];
        var triangles = new int[(count - 1) * 24];
        float halfWidth = Mathf.Max(1.2f, width * .5f);
        for (int i = 0; i < count; i++)
        {
            int source = first + i;
            Vector3 tangent = source == 0 ? road[1] - road[0] : source == road.Count - 1 ? road[source] - road[source - 1] : road[source + 1] - road[source - 1];
            tangent.y = 0f;
            if (tangent.sqrMagnitude < .0001f) tangent = Vector3.forward;
            tangent.Normalize();
            Vector3 side = new Vector3(-tangent.z, 0f, tangent.x);
            Vector3 center = road[source];
            Vector3 top = center + Vector3.up * WalkSurfaceTopOffset;
            Vector3 bottom = center + Vector3.up * WalkSurfaceBottomOffset;
            int v = i * 4;
            vertices[v] = top - side * halfWidth;
            vertices[v + 1] = top + side * halfWidth;
            vertices[v + 2] = bottom - side * halfWidth;
            vertices[v + 3] = bottom + side * halfWidth;
        }
        for (int i = 0; i < count - 1; i++)
        {
            int a = i * 4, b = (i + 1) * 4, t = i * 24;
            triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = a + 1;
            triangles[t + 3] = a + 1; triangles[t + 4] = b; triangles[t + 5] = b + 1;
            triangles[t + 6] = a + 2; triangles[t + 7] = a + 3; triangles[t + 8] = b + 2;
            triangles[t + 9] = a + 3; triangles[t + 10] = b + 3; triangles[t + 11] = b + 2;
            triangles[t + 12] = a; triangles[t + 13] = a + 2; triangles[t + 14] = b;
            triangles[t + 15] = b; triangles[t + 16] = a + 2; triangles[t + 17] = b + 2;
            triangles[t + 18] = a + 1; triangles[t + 19] = b + 1; triangles[t + 20] = a + 3;
            triangles[t + 21] = a + 3; triangles[t + 22] = b + 1; triangles[t + 23] = b + 3;
        }
        var mesh = new Mesh { name = "YQ_StoneBridgeWalkSurface" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float[] MeasureDeckProfile(Transform source, Renderer[] renderers, float centerX, float minZ, float span, float deckDatum, out float deckMinZ, out float deckSpan)
    {
        // note: Measure the actual upper centreline triangles once per bridge section; no temporary colliders or global physics synchronization are required.
        var profile = new float[129];
        for (int sample = 0; sample < profile.Length; sample++) profile[sample] = float.NegativeInfinity;
        foreach (var renderer in renderers)
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            var vertices = filter.sharedMesh.vertices;
            var triangles = filter.sharedMesh.triangles;
            for (int i = 0; i < vertices.Length; i++)
                vertices[i] = source.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
            for (int sample = 0; sample < profile.Length; sample++)
            {
                float z = minZ + Mathf.Lerp(.08f, span - .08f, sample / (float)(profile.Length - 1));
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    Vector3 a = vertices[triangles[triangle]], b = vertices[triangles[triangle+1]], c = vertices[triangles[triangle+2]];
                    float determinant = (b.x-a.x)*(c.z-a.z) - (b.z-a.z)*(c.x-a.x);
                    if (Mathf.Abs(determinant) < .000001f) continue;
                    float v = ((centerX-a.x)*(c.z-a.z) - (z-a.z)*(c.x-a.x)) / determinant;
                    float w = ((b.x-a.x)*(z-a.z) - (b.z-a.z)*(centerX-a.x)) / determinant;
                    if (v < -.0001f || w < -.0001f || v+w > 1.0001f) continue;
                    profile[sample] = Mathf.Max(profile[sample], a.y + v*(b.y-a.y) + w*(c.y-a.y));
                }
            }
        }
        // note: The authored pillars project beyond the traversable deck; trim those end samples while still rejecting any hole inside the walking span.
        int first = 0, last = profile.Length - 1;
        while (first <= last && (float.IsInfinity(profile[first]) || profile[first] < deckDatum - 1f)) first++;
        while (last >= first && (float.IsInfinity(profile[last]) || profile[last] < deckDatum - 1f)) last--;
        if (last - first < profile.Length / 2)
            throw new InvalidOperationException("Approved bridge has insufficient continuous walking span.");
        for (int sample = first; sample <= last; sample++)
            if (float.IsInfinity(profile[sample]) || profile[sample] < deckDatum - 1f)
                throw new InvalidOperationException("Approved bridge has no continuous upper deck at sample " + sample + ".");
        float spacing = (span - .16f) / (profile.Length - 1);
        deckMinZ = minZ + .08f + first * spacing;
        deckSpan = (last - first) * spacing;
        var walkingProfile = new float[last - first + 1];
        Array.Copy(profile, first, walkingProfile, 0, walkingProfile.Length);
        return walkingProfile;
    }

    private void OnDestroy()
    {
        // note: Runtime mesh deformation owns its copies and never modifies the imported source asset.
        foreach(var mesh in meshes) if(mesh!=null) Destroy(mesh);
        // note: Per-bridge material instances are released with their owner so repeated world rebuilds do not accumulate bridge variants.
        foreach(var material in materials) if(material!=null) Destroy(material);
    }
}
