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
    private const string CollisionCorridorGeometryVersion = "buffered_road_union_v2";
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

            // note: One continuous shoulder follows the same shared road vertices. Tilted segment boxes exposed steep end caps at joints and lifted the capsule off its intended plane.
            stageStarted = Time.realtimeSinceStartup;
            Mesh supportCollision = BuildSupportSurfaceMesh(road, width);
            RecordBuildSubstage(substageTelemetry, "bridgeSupportMesh", stageStarted);
            if (supportCollision != null)
            {
                stageStarted = Time.realtimeSinceStartup;
                GameObject support = new GameObject("StoneBridgeWalkSurface");
                support.transform.SetParent(root.transform, false);
                support.AddComponent<MeshCollider>().sharedMesh = supportCollision;
                owner.meshes.Add(supportCollision);
                RecordBuildSubstage(substageTelemetry, "bridgeSupportCollider", stageStarted);
                yield return null;
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
        return BuildExtrudedRoadMesh(road, width, WalkSurfaceTopOffset, WalkSurfaceBottomOffset, false, "YQ_StoneBridgeWalkSurface");
    }

    private static Mesh BuildSupportSurfaceMesh(IReadOnlyList<Vector3> road, float width)
    {
        // note: Retain the existing one-metre shoulder and half-metre depth without creating independent internal faces or extending past bank contacts.
        float top = WalkSurfaceTopOffset - WalkSurfaceSupportTopInset;
        return BuildExtrudedRoadMesh(road, width + 2f, top, top - WalkSurfaceSupportThickness, true, "YQ_ContinuousBridgeSupport");
    }

    private static Mesh BuildExtrudedRoadMesh(IReadOnlyList<Vector3> road, float width, float topOffset, float bottomOffset, bool capEnds, string meshName)
    {
        // note: Sampling may repeat a bank endpoint. Remove only redundant derived collision samples (within 0.1 mm) so a zero-length tangent cannot fold the terminal strip; accepted road data and visual assemblies remain untouched.
        var collisionRoad = new List<Vector3>(road.Count);
        for (int index = 0; index < road.Count; index++)
        {
            Vector3 point = road[index];
            if (collisionRoad.Count == 0 || (point - collisionRoad[collisionRoad.Count - 1]).sqrMagnitude > .00000001f)
                collisionRoad.Add(point);
        }
        road = collisionRoad;
        // note: Carry the thin walk surface through both bank samples so the certified road never loses support at the bridge-to-bank seam.
        int first = 0;
        int last = road.Count - 1;
        int count = last - first + 1;
        if (count < 2) return null;
        var vertices = new Vector3[count * 4];
        var triangles = new int[(count - 1) * 24 + (capEnds ? 12 : 0)];
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
            Vector3 top = center + Vector3.up * topOffset;
            Vector3 bottom = center + Vector3.up * bottomOffset;
            int v = i * 4;
            vertices[v] = top - side * halfWidth;
            vertices[v + 1] = top + side * halfWidth;
            vertices[v + 2] = bottom - side * halfWidth;
            vertices[v + 3] = bottom + side * halfWidth;
        }
        for (int i = 0; i < count - 1; i++)
        {
            int a = i * 4, b = (i + 1) * 4, t = i * 24;
            // note: MeshCollider faces are one-sided: wind the top upward, bottom downward and sides outward so physics cannot admit the underside as the road tread.
            triangles[t] = a; triangles[t + 1] = a + 1; triangles[t + 2] = b;
            triangles[t + 3] = a + 1; triangles[t + 4] = b + 1; triangles[t + 5] = b;
            triangles[t + 6] = a + 2; triangles[t + 7] = b + 2; triangles[t + 8] = a + 3;
            triangles[t + 9] = a + 3; triangles[t + 10] = b + 2; triangles[t + 11] = b + 3;
            triangles[t + 12] = a; triangles[t + 13] = b; triangles[t + 14] = a + 2;
            triangles[t + 15] = b; triangles[t + 16] = b + 2; triangles[t + 17] = a + 2;
            triangles[t + 18] = a + 1; triangles[t + 19] = a + 3; triangles[t + 20] = b + 1;
            triangles[t + 21] = a + 3; triangles[t + 22] = b + 3; triangles[t + 23] = b + 1;
        }
        if (capEnds)
        {
            // note: Close only the two terminal banks. Interior cross-sections stay shared, with no sloping cap available as a false tread.
            int t = (count - 1) * 24;
            int end = (count - 1) * 4;
            triangles[t] = 0; triangles[t + 1] = 2; triangles[t + 2] = 1;
            triangles[t + 3] = 1; triangles[t + 4] = 2; triangles[t + 5] = 3;
            triangles[t + 6] = end; triangles[t + 7] = end + 1; triangles[t + 8] = end + 2;
            triangles[t + 9] = end + 1; triangles[t + 10] = end + 3; triangles[t + 11] = end + 2;
        }
        // note: A short sharp turn can fold a bisector strip despite correct winding. Rebuild its full-width footprint, rather than flipping inward triangles or narrowing the accepted corridor.
        for (int segment = 0; segment < count - 1; segment++)
        {
            int offset = segment * 24;
            for (int face = 0; face < 6; face += 3)
                if (Vector3.Cross(vertices[triangles[offset + face + 1]] - vertices[triangles[offset + face]],
                    vertices[triangles[offset + face + 2]] - vertices[triangles[offset + face]]).y <= 0f)
                    return BuildBufferedCollisionMesh(road, halfWidth, topOffset, bottomOffset, capEnds, meshName);
        }
        var mesh = new Mesh { name = meshName };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private struct CorridorEdge
    {
        public Vector2 a, b;
        public int first, second, count;
    }

    private static double CorridorCross(Vector2 a, Vector2 b)
    {
        return (double)a.x * b.y - (double)a.y * b.x;
    }

    private static Mesh BuildBufferedCollisionMesh(IReadOnlyList<Vector3> road, float halfWidth,
        float topOffset, float bottomOffset, bool capEnds, string meshName)
    {
        // note: Work in a small local numeric frame. Segment rectangles and outer bevels preserve width and original bank endpoints; their union has no internal collision caps or overlapping top faces.
        Vector2 origin = new Vector2(road[0].x, road[0].z);
        var centers = new Vector2[road.Count];
        var sides = new Vector2[road.Count - 1];
        var polygons = new List<Vector2[]>();
        for (int index = 0; index < road.Count; index++) centers[index] = new Vector2(road[index].x, road[index].z) - origin;
        for (int index = 0; index < sides.Length; index++)
        {
            Vector2 delta = centers[index + 1] - centers[index];
            if (delta.sqrMagnitude < .00000001f) throw new InvalidOperationException("Bridge collision has a vertical-only road segment.");
            Vector2 direction = delta.normalized;
            Vector2 side = sides[index] = new Vector2(-direction.y, direction.x) * halfWidth;
            polygons.Add(new[] { centers[index] - side, centers[index + 1] - side, centers[index + 1] + side, centers[index] + side });
            if (index == 0) continue;
            double turn = CorridorCross(centers[index] - centers[index - 1], delta);
            if (Math.Abs(turn) < .00000001d) continue;
            float sign = turn > 0d ? -1f : 1f;
            var bevel = new[] { centers[index], centers[index] + sides[index - 1] * sign, centers[index] + side * sign };
            if (CorridorCross(bevel[1] - bevel[0], bevel[2] - bevel[0]) < 0d) Array.Reverse(bevel);
            polygons.Add(bevel);
        }
        List<Vector2> boundary = ResolveCorridorBoundary(polygons);
        var points = new List<Vector2>(boundary);
        var topVertices = new List<Vector3>();
        foreach (Vector2 point in points)
            topVertices.Add(new Vector3(origin.x + point.x, SampleCorridorHeight(point, centers, road) + topOffset, origin.y + point.y));
        List<int> surface = TriangulateCorridorBoundary(boundary);
        // note: Insert every accepted centreline sample into the surface triangulation, including bank-edge samples, so triangulation cannot flatten away an accepted grade or introduce cracks at a shared edge.
        for (int index = 0; index < centers.Length; index++)
            InsertCorridorPoint(centers[index], road[index] + Vector3.up * topOffset, points, topVertices, ref surface);
        InsertCorridorGradeEdges(centers, road, topOffset, origin, points, topVertices, ref surface);
        var edges = new Dictionary<long, CorridorEdge>();
        for (int index = 0; index < surface.Count; index += 3)
            for (int edge = 0; edge < 3; edge++)
            {
                int a = surface[index + edge], b = surface[index + (edge + 1) % 3];
                long key = ((long)Math.Min(a, b) << 32) | (uint)Math.Max(a, b);
                if (edges.TryGetValue(key, out CorridorEdge existing))
                {
                    if (existing.count != 1 || existing.first != b || existing.second != a)
                        throw new InvalidOperationException("Bridge collision triangulation has an overlapping or non-manifold edge.");
                    existing.count = 2; edges[key] = existing;
                }
                else edges.Add(key, new CorridorEdge { first = a, second = b, count = 1 });
            }
        int vertexCount = topVertices.Count;
        var vertices = new Vector3[vertexCount * 2];
        for (int index = 0; index < vertexCount; index++)
        {
            vertices[index] = topVertices[index];
            vertices[index + vertexCount] = topVertices[index] - Vector3.up * (topOffset - bottomOffset);
        }
        var top = new List<int>(surface.Count); var bottom = new List<int>(surface.Count); var walls = new List<int>();
        for (int index = 0; index < surface.Count; index += 3)
        {
            int a = surface[index], b = surface[index + 1], c = surface[index + 2];
            top.Add(a); top.Add(c); top.Add(b);
            bottom.Add(a + vertexCount); bottom.Add(b + vertexCount); bottom.Add(c + vertexCount);
        }
        Vector2 firstDirection = (centers[1] - centers[0]).normalized;
        Vector2 lastDirection = (centers[centers.Length - 1] - centers[centers.Length - 2]).normalized;
        var orderedEdges = new List<long>(edges.Keys);
        orderedEdges.Sort();
        foreach (long key in orderedEdges)
        {
            CorridorEdge edge = edges[key];
            if (edge.count != 1) continue;
            int a = edge.first, b = edge.second;
            if (!capEnds && (OnCorridorBank(points[a], points[b], centers[0], firstDirection) ||
                OnCorridorBank(points[a], points[b], centers[centers.Length - 1], lastDirection))) continue;
            walls.Add(a); walls.Add(b); walls.Add(a + vertexCount);
            walls.Add(b); walls.Add(b + vertexCount); walls.Add(a + vertexCount);
        }
        var mesh = new Mesh { name = meshName, subMeshCount = 3 };
        mesh.vertices = vertices;
        mesh.SetTriangles(top, 0); mesh.SetTriangles(bottom, 1); mesh.SetTriangles(walls, 2);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    private static bool OnCorridorBank(Vector2 a, Vector2 b, Vector2 center, Vector2 direction)
    {
        return Mathf.Abs(Vector2.Dot(a - center, direction)) < .0001f && Mathf.Abs(Vector2.Dot(b - center, direction)) < .0001f;
    }

    private static float SampleCorridorHeight(Vector2 point, Vector2[] centers, IReadOnlyList<Vector3> road)
    {
        float nearest = float.PositiveInfinity, height = road[0].y;
        for (int index = 0; index < centers.Length - 1; index++)
        {
            Vector2 delta = centers[index + 1] - centers[index];
            float t = Mathf.Clamp01(Vector2.Dot(point - centers[index], delta) / delta.sqrMagnitude);
            float distance = (point - centers[index] - delta * t).sqrMagnitude;
            if (distance < nearest) { nearest = distance; height = Mathf.Lerp(road[index].y, road[index + 1].y, t); }
        }
        return height;
    }

    private static List<Vector2> ResolveCorridorBoundary(List<Vector2[]> polygons)
    {
        // note: Split footprint edges at intersections, retain only union boundary pieces, and stitch their existing orientation. Ambiguous/disconnected/hole boundaries fail explicitly instead of manufacturing a convex hull over the water.
        var pieces = new List<CorridorEdge>();
        for (int owner = 0; owner < polygons.Count; owner++)
        for (int edge = 0; edge < polygons[owner].Length; edge++)
        {
            Vector2 a = polygons[owner][edge], b = polygons[owner][(edge + 1) % polygons[owner].Length], delta = b - a;
            var cuts = new List<float> { 0f, 1f };
            for (int other = 0; other < polygons.Count; other++)
            {
                if (other == owner) continue;
                for (int i = 0; i < polygons[other].Length; i++)
                {
                    Vector2 c = polygons[other][i], d = polygons[other][(i + 1) % polygons[other].Length], otherDelta = d - c;
                    double denominator = CorridorCross(delta, otherDelta);
                    if (Math.Abs(denominator) > .00000001d)
                    {
                        double t = CorridorCross(c - a, otherDelta) / denominator;
                        double u = CorridorCross(c - a, delta) / denominator;
                        if (t > 0d && t < 1d && u >= -.000001d && u <= 1.000001d) cuts.Add((float)t);
                    }
                    else if (Math.Abs(CorridorCross(c - a, delta)) < .00001d * delta.magnitude)
                    {
                        float t = Vector2.Dot(c - a, delta) / delta.sqrMagnitude;
                        float u = Vector2.Dot(d - a, delta) / delta.sqrMagnitude;
                        if (t > 0f && t < 1f) cuts.Add(t);
                        if (u > 0f && u < 1f) cuts.Add(u);
                    }
                }
            }
            cuts.Sort();
            for (int i = 1; i < cuts.Count; i++)
            {
                Vector2 start = a + delta * cuts[i - 1], end = a + delta * cuts[i];
                if ((end - start).sqrMagnitude < .0000000001f) continue;
                Vector2 midpoint = (start + end) * .5f;
                bool hidden = false;
                for (int other = 0; other < polygons.Count && !hidden; other++)
                    if (other != owner) hidden = CoveredCorridorEdge(midpoint, delta, polygons[other], other < owner);
                if (!hidden) pieces.Add(new CorridorEdge { a = start, b = end });
            }
        }
        if (pieces.Count < 3) throw new InvalidOperationException("Bridge collision footprint has no closed boundary.");
        var boundary = new List<Vector2>();
        CorridorEdge current = pieces[0]; pieces.RemoveAt(0); boundary.Add(current.a);
        Vector2 endpoint = current.b;
        while ((endpoint - boundary[0]).sqrMagnitude > .00000001f)
        {
            boundary.Add(endpoint);
            int next = -1; float nearest = .00000001f;
            for (int index = 0; index < pieces.Count; index++)
            {
                float distance = (pieces[index].a - endpoint).sqrMagnitude;
                if (distance <= nearest) { nearest = distance; next = index; }
            }
            if (next < 0) throw new InvalidOperationException("Bridge collision union boundary is disconnected.");
            endpoint = pieces[next].b; pieces.RemoveAt(next);
        }
        if (pieces.Count != 0) throw new InvalidOperationException("Bridge collision footprint has multiple boundary loops.");
        for (int index = boundary.Count - 1; index >= 0 && boundary.Count > 3; index--)
        {
            Vector2 a = boundary[(index + boundary.Count - 1) % boundary.Count], b = boundary[index], c = boundary[(index + 1) % boundary.Count];
            if (Math.Abs(CorridorCross(b - a, c - b)) <= .00001d * ((b - a).magnitude + (c - b).magnitude) && Vector2.Dot(b - a, c - b) >= 0f)
                boundary.RemoveAt(index);
        }
        return boundary;
    }

    private static bool CoveredCorridorEdge(Vector2 point, Vector2 direction, Vector2[] polygon, bool preferOther)
    {
        bool boundary = false, hiddenBoundary = false;
        for (int index = 0; index < polygon.Length; index++)
        {
            Vector2 a = polygon[index], delta = polygon[(index + 1) % polygon.Length] - a;
            double side = CorridorCross(delta, point - a), epsilon = .00001d * delta.magnitude;
            if (side < -epsilon) return false;
            if (Math.Abs(side) <= epsilon)
            {
                boundary = true;
                hiddenBoundary |= Vector2.Dot(direction, delta) < 0f || preferOther;
            }
        }
        return !boundary || hiddenBoundary;
    }

    private static bool InsideCorridorTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // note: Float-derived bank intersections have distance error, not a fixed area error. Use the same ten-micrometre edge-distance tolerance for containment and shared-edge insertion.
        return CorridorCross(b - a, p - a) >= -.00001d * (b - a).magnitude &&
            CorridorCross(c - b, p - b) >= -.00001d * (c - b).magnitude &&
            CorridorCross(a - c, p - c) >= -.00001d * (a - c).magnitude;
    }

    private static List<int> TriangulateCorridorBoundary(List<Vector2> points)
    {
        var remaining = new List<int>(); var triangles = new List<int>();
        for (int index = 0; index < points.Count; index++) remaining.Add(index);
        while (remaining.Count > 3)
        {
            bool clipped = false;
            for (int index = 0; index < remaining.Count; index++)
            {
                int a = remaining[(index + remaining.Count - 1) % remaining.Count], b = remaining[index], c = remaining[(index + 1) % remaining.Count];
                if (CorridorCross(points[b] - points[a], points[c] - points[b]) <= .00000001d) continue;
                bool occupied = false;
                foreach (int candidate in remaining)
                    if (candidate != a && candidate != b && candidate != c && InsideCorridorTriangle(points[candidate], points[a], points[b], points[c])) { occupied = true; break; }
                if (occupied) continue;
                triangles.Add(a); triangles.Add(b); triangles.Add(c); remaining.RemoveAt(index); clipped = true; break;
            }
            if (!clipped) throw new InvalidOperationException("Bridge collision boundary cannot be triangulated without overlap.");
        }
        triangles.AddRange(remaining);
        return triangles;
    }

    private static void InsertCorridorPoint(Vector2 point, Vector3 position, List<Vector2> points, List<Vector3> vertices, ref List<int> triangles)
    {
        for (int index = 0; index < points.Count; index++)
            if ((points[index] - point).sqrMagnitude < .00000001f) { points[index] = point; vertices[index] = position; return; }
        int added = points.Count;
        points.Add(point); vertices.Add(position);
        var split = new List<int>(); bool inserted = false;
        for (int index = 0; index < triangles.Count; index += 3)
        {
            int a = triangles[index], b = triangles[index + 1], c = triangles[index + 2];
            if (!InsideCorridorTriangle(point, points[a], points[b], points[c])) { split.Add(a); split.Add(b); split.Add(c); continue; }
            inserted = true;
            for (int edge = 0; edge < 3; edge++)
            {
                int from = triangles[index + edge], to = triangles[index + (edge + 1) % 3];
                if (CorridorCross(points[to] - points[from], point - points[from]) <= .00001d * (points[to] - points[from]).magnitude) continue;
                split.Add(from); split.Add(to); split.Add(added);
            }
        }
        if (!inserted) throw new InvalidOperationException("Accepted bridge centreline sample " + point.ToString("F7") + " lies outside its collision corridor.");
        triangles = split;
    }

    private static void InsertCorridorGradeEdges(Vector2[] centers, IReadOnlyList<Vector3> road, float topOffset,
        Vector2 origin, List<Vector2> points, List<Vector3> vertices, ref List<int> triangles)
    {
        // note: Retaining isolated height anchors is insufficient: a triangle can cut across a bend and change the tread between them. Split every crossed triangulation edge along each original road segment, retaining its interpolated grade as a continuous constrained edge chain.
        for (int segment = 0; segment < centers.Length - 1; segment++)
            InsertCorridorConstrainedEdge(centers[segment], centers[segment + 1], road[segment].y,
                road[segment + 1].y, topOffset, origin, points, vertices, ref triangles);
    }

    private static void InsertCorridorConstrainedEdge(Vector2 a, Vector2 b, float firstHeight,
        float lastHeight, float topOffset, Vector2 origin, List<Vector2> points,
        List<Vector3> vertices, ref List<int> triangles)
    {
        // note: Preserve the existing deterministic centerline edge splitting and height interpolation.
        Vector2 direction = b - a;
        var cuts = new List<float>();
        for (int index = 0; index < triangles.Count; index += 3)
        for (int edge = 0; edge < 3; edge++)
        {
            Vector2 c = points[triangles[index + edge]], delta = points[triangles[index + (edge + 1) % 3]] - c;
            double denominator = CorridorCross(direction, delta);
            if (Math.Abs(denominator) < .00000001d) continue;
            double t = CorridorCross(c - a, delta) / denominator;
            double u = CorridorCross(c - a, direction) / denominator;
            if (t > 0d && t < 1d && u >= -.000001d && u <= 1.000001d) cuts.Add((float)t);
        }
        cuts.Sort();
        float previous = -1f;
        foreach (float t in cuts)
        {
            if (previous >= 0f && (t - previous) * direction.magnitude < .0001f) continue;
            Vector2 point = a + direction * t;
            Vector3 position = new Vector3(origin.x + point.x, Mathf.Lerp(firstHeight, lastHeight, t) + topOffset, origin.y + point.y);
            InsertCorridorPoint(point, position, points, vertices, ref triangles);
            previous = t;
        }
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
