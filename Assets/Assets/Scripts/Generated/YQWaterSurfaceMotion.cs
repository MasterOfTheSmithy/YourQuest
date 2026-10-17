using UnityEngine;
using System.Collections.Generic;

// note: Animate water through a renderer property block so large generated meshes never upload a new UV array every frame.
[DisallowMultipleComponent]
public sealed class YQWaterSurfaceMotion : MonoBehaviour
{
    // note: Register immutable world-space triangles once per streamed surface; player/camera queries visit only one small spatial bucket.
    private const float QueryCellSize = 12f;
    private sealed class SurfaceTriangle
    {
        internal YQWaterSurfaceMotion owner;
        internal Vector3 a, b, c;
        internal float denominator;
    }
    private static readonly Dictionary<Vector2Int, List<SurfaceTriangle>> QueryCells = new();
    private readonly Dictionary<Vector2Int, List<SurfaceTriangle>> surfaceCells = new();
    private bool registered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWaterQueries() => QueryCells.Clear();

    public static bool TryGetWaterHeight(Vector3 position, out float height)
    {
        height = float.NegativeInfinity;
        Vector2Int cell = new Vector2Int(Mathf.FloorToInt(position.x / QueryCellSize), Mathf.FloorToInt(position.z / QueryCellSize));
        if (!QueryCells.TryGetValue(cell, out List<SurfaceTriangle> triangles)) return false;
        for (int index = 0; index < triangles.Count; index++)
        {
            SurfaceTriangle triangle = triangles[index];
            // note: Render-only view culling never removes the physical water volume; streamed root/component lifecycle owns admission and unload.
            if (triangle.owner == null || !triangle.owner.isActiveAndEnabled || triangle.owner.targetRenderer == null) continue;
            Vector3 a = triangle.a, b = triangle.b, c = triangle.c;
            float u = ((b.z - c.z) * (position.x - c.x) + (c.x - b.x) * (position.z - c.z)) / triangle.denominator;
            float v = ((c.z - a.z) * (position.x - c.x) + (a.x - c.x) * (position.z - c.z)) / triangle.denominator;
            float w = 1f - u - v;
            if (u < -0.0001f || v < -0.0001f || w < -0.0001f) continue;
            height = Mathf.Max(height, a.y * u + b.y * v + c.y * w);
        }
        return !float.IsNegativeInfinity(height);
    }

    private void CacheSurface(MeshFilter filter)
    {
        UnregisterSurface();
        surfaceCells.Clear();
        if (filter == null || filter.sharedMesh == null) return;
        Vector3[] vertices = filter.sharedMesh.vertices;
        int[] indices = filter.sharedMesh.triangles;
        Matrix4x4 transformMatrix = filter.transform.localToWorldMatrix;
        for (int index = 0; index + 2 < indices.Length; index += 3)
        {
            Vector3 a = transformMatrix.MultiplyPoint3x4(vertices[indices[index]]);
            Vector3 b = transformMatrix.MultiplyPoint3x4(vertices[indices[index + 1]]);
            Vector3 c = transformMatrix.MultiplyPoint3x4(vertices[indices[index + 2]]);
            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            // note: Vertical waterfall faces are visual sheets, not swimmable horizontal volumes.
            if (Mathf.Abs(denominator) < 0.0001f || Mathf.Abs(Vector3.Cross(b - a, c - a).normalized.y) < 0.25f) continue;
            var triangle = new SurfaceTriangle { owner = this, a = a, b = b, c = c, denominator = denominator };
            int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x, c.x) / QueryCellSize), maxX = Mathf.FloorToInt(Mathf.Max(a.x, b.x, c.x) / QueryCellSize);
            int minZ = Mathf.FloorToInt(Mathf.Min(a.z, b.z, c.z) / QueryCellSize), maxZ = Mathf.FloorToInt(Mathf.Max(a.z, b.z, c.z) / QueryCellSize);
            for (int x = minX; x <= maxX; x++) for (int z = minZ; z <= maxZ; z++)
            {
                var cell = new Vector2Int(x, z);
                if (!surfaceCells.TryGetValue(cell, out var bucket)) surfaceCells[cell] = bucket = new List<SurfaceTriangle>();
                bucket.Add(triangle);
            }
        }
        if (isActiveAndEnabled) RegisterSurface();
    }

    private void RegisterSurface()
    {
        if (registered) return;
        foreach (var pair in surfaceCells)
        {
            if (!QueryCells.TryGetValue(pair.Key, out var bucket)) QueryCells[pair.Key] = bucket = new List<SurfaceTriangle>();
            bucket.AddRange(pair.Value);
        }
        registered = true;
    }

    private void UnregisterSurface()
    {
        if (!registered) return;
        foreach (var pair in surfaceCells)
        {
            if (!QueryCells.TryGetValue(pair.Key, out var bucket)) continue;
            bucket.RemoveAll(triangle => triangle.owner == this);
            if (bucket.Count == 0) QueryCells.Remove(pair.Key);
        }
        registered = false;
    }

    private void OnEnable() => RegisterSurface();
    private void OnDisable() => UnregisterSurface();
    private void OnDestroy() { UnregisterSurface(); surfaceCells.Clear(); }
    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Vector2 velocity;
    private Vector2 baseScale = Vector2.one;
    private float nextUpdate;
    private int baseMapStId;
    private int mainTexStId;
    private bool hasBaseMap;
    private bool hasMainTex;

    // note: Select a distinct drift profile for each accepted hydrology kind while keeping pond-like wetland motion nearly still.
    public void Configure(MeshFilter filter, Renderer renderer, YQHydrologyKindV2 kind, float flowScale = 1f)
    {
        targetRenderer = renderer;
        propertyBlock = new MaterialPropertyBlock();
        switch (kind)
        {
            case YQHydrologyKindV2.Waterfall:
                velocity = new Vector2(0f, -0.16f);
                break;
            case YQHydrologyKindV2.River:
                velocity = new Vector2(0.002f, -0.075f);
                break;
            case YQHydrologyKindV2.Wetland:
                velocity = new Vector2(0.001f, -0.006f);
                break;
            default:
                velocity = new Vector2(0.002f, -0.012f);
                break;
        }

        if (targetRenderer != null && targetRenderer.sharedMaterial != null)
        {
            Material material = targetRenderer.sharedMaterial;
            baseMapStId = Shader.PropertyToID("_BaseMap_ST");
            mainTexStId = Shader.PropertyToID("_MainTex_ST");
            // note: Texture transform uniforms are not ShaderLab properties on every shader; detect the texture slot itself.
            hasBaseMap = material.HasProperty("_BaseMap");
            hasMainTex = material.HasProperty("_MainTex");
            if (hasBaseMap)
                baseScale = material.GetTextureScale("_BaseMap");
            else if (hasMainTex)
                baseScale = material.GetTextureScale("_MainTex");
        }
        // note: Match the lightweight texture drift to the material's grade-aware flow speed without allocating per-frame state.
        velocity *= Mathf.Clamp(flowScale, .75f, 2.8f);
        nextUpdate = 0f;
        CacheSurface(filter);
    }

    // note: Update at a capped cadence because water motion is presentation-only and should not compete with traversal or NPC simulation.
    private void Update()
    {
        if (targetRenderer == null || propertyBlock == null || Time.unscaledTime < nextUpdate ||
            (!hasBaseMap && !hasMainTex))
            return;

        nextUpdate = Time.unscaledTime + 0.0667f;
        float time = Time.unscaledTime;
        Vector2 offset = new Vector2(
            Mathf.Repeat(time * velocity.x, 1f),
            Mathf.Repeat(time * velocity.y, 1f));
        Vector4 st = new Vector4(baseScale.x, baseScale.y, offset.x, offset.y);
        if (hasBaseMap)
            propertyBlock.SetVector(baseMapStId, st);
        if (hasMainTex)
            propertyBlock.SetVector(mainTexStId, st);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }
}
