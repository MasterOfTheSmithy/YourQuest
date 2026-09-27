using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Rendering.Universal;

// note: Provide a lightweight camera overlay when the authoritative player is below a generated water surface.
[DisallowMultipleComponent]
public sealed class YQUnderwaterVisual : MonoBehaviour
{
    private Camera targetCamera;
    private MeshRenderer[] waterRenderers;
    private Image overlay;
    private float nextProbe;
    private GameObject canvasObject;
    // note: Retain immutable mesh arrays once; Unity's mesh getters allocate a full copy on every call.
    private readonly Dictionary<MeshRenderer, (Vector3[] vertices, int[] triangles)> meshSamples = new();

    // note: Attach one presentation component to the generated root so it follows the single world transaction and never duplicates per water body.
    public static void Attach(Transform generatedRoot)
    {
        if (generatedRoot == null || generatedRoot.GetComponentInChildren<YQUnderwaterVisual>(true) != null)
            return;
        GameObject host = new GameObject("YQ_UnderwaterVisual");
        host.transform.SetParent(generatedRoot, false);
        host.AddComponent<YQUnderwaterVisual>();
    }

    // note: Resolve the camera and water surfaces after scene/player startup has completed, keeping the build coroutine independent of UI timing.
    private void Start()
    {
        targetCamera = Camera.main;
        waterRenderers = GetComponentsInParent<Transform>(true).Length > 0
            ? transform.root.GetComponentsInChildren<MeshRenderer>(true)
            : new MeshRenderer[0];
        foreach (var water in waterRenderers)
        {
            if (water.name != "CompiledHydrologySurface" && water.name != "CuratedEllipticalWaterSurface") continue;
            var filter = water.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
                meshSamples[water] = (filter.sharedMesh.vertices, filter.sharedMesh.triangles);
        }
        CreateOverlay();
    }

    // note: Build a screen-space tint without post-processing dependencies so the cue works in the current URP setup and in development scenes.
    private void CreateOverlay()
    {
        if (targetCamera == null)
            return;
        // note: Water absorption and contact blending read opaque depth from the same authoritative gameplay camera.
        targetCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        canvasObject = new GameObject("YQ_UnderwaterCanvas");
        canvasObject.transform.SetParent(targetCamera.transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = targetCamera;
        canvas.planeDistance = 0.05f;
        canvas.sortingOrder = 30000;
        canvasObject.AddComponent<CanvasScaler>();
        canvasObject.AddComponent<GraphicRaycaster>();
        GameObject imageObject = new GameObject("YQ_UnderwaterTint");
        imageObject.transform.SetParent(canvasObject.transform, false);
        overlay = imageObject.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = new Color(0.04f, 0.22f, 0.32f, 0f);
        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // note: Probe at a low cadence and use renderer bounds in XZ to avoid per-frame mesh scans or physics queries on water without colliders.
    private void Update()
    {
        if (Time.unscaledTime < nextProbe)
            return;
        nextProbe = Time.unscaledTime + 0.1f;
        if (targetCamera == null)
            targetCamera = Camera.main;
        if (overlay == null && targetCamera != null) CreateOverlay();
        if (overlay == null || targetCamera == null || waterRenderers == null)
            return;

        float deepest = 0f;
        Vector3 cameraPosition = targetCamera.transform.position;
        for (int index = 0; index < waterRenderers.Length; index++)
        {
            MeshRenderer water = waterRenderers[index];
            if (water == null || !water.enabled || !water.gameObject.activeInHierarchy || !meshSamples.ContainsKey(water))
                continue;
            if (!TrySampleWaterHeight(water, cameraPosition, out float surfaceHeight) ||
                cameraPosition.y >= surfaceHeight)
                continue;
            deepest = Mathf.Max(deepest, Mathf.Clamp01((surfaceHeight - cameraPosition.y) * 0.12f));
        }

        Color tint = overlay.color;
        float targetAlpha = deepest > 0f ? Mathf.Lerp(0.16f, 0.34f, deepest) : 0f;
        // note: This probe runs every tenth of a second; dry land clears immediately so an old tint cannot linger after leaving water.
        tint.a = targetAlpha;
        overlay.color = tint;
    }

    // note: Sample the actual water triangles at the camera's X/Z instead of using a long river's broad renderer bounds as a false volume.
    private bool TrySampleWaterHeight(MeshRenderer water, Vector3 world, out float surfaceHeight)
    {
        surfaceHeight = 0f;
        Bounds bounds = water.bounds;
        if (world.x < bounds.min.x || world.x > bounds.max.x || world.z < bounds.min.z || world.z > bounds.max.z)
            return false;
        if (!meshSamples.TryGetValue(water, out var samples))
            return false;

        int[] triangles = samples.triangles;
        if (triangles == null || triangles.Length < 3)
            return false;
        Vector3[] vertices = samples.vertices;
        Matrix4x4 localToWorld = water.transform.localToWorldMatrix;
        // note: Resolve only the triangle containing the camera so concave banks and elliptical lakes do not report water outside their footprint.
        for (int index = 0; index <= triangles.Length - 3; index += 3)
        {
            Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[index]]);
            Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[index + 1]]);
            Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[index + 2]]);
            float minX = Mathf.Min(a.x, Mathf.Min(b.x, c.x)) - 0.08f;
            float maxX = Mathf.Max(a.x, Mathf.Max(b.x, c.x)) + 0.08f;
            float minZ = Mathf.Min(a.z, Mathf.Min(b.z, c.z)) - 0.08f;
            float maxZ = Mathf.Max(a.z, Mathf.Max(b.z, c.z)) + 0.08f;
            if (world.x < minX || world.x > maxX || world.z < minZ || world.z > maxZ)
                continue;

            float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
            if (Mathf.Abs(denominator) < 0.0001f)
                continue;
            float u = ((b.z - c.z) * (world.x - c.x) + (c.x - b.x) * (world.z - c.z)) / denominator;
            float v = ((c.z - a.z) * (world.x - c.x) + (a.x - c.x) * (world.z - c.z)) / denominator;
            float w = 1f - u - v;
            if (u < -0.02f || v < -0.02f || w < -0.02f)
                continue;
            surfaceHeight = a.y * u + b.y * v + c.y * w;
            return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        // note: The canvas is parented to the camera, so world teardown must explicitly remove it to prevent stale blue overlays.
        if (canvasObject != null) Destroy(canvasObject);
        meshSamples.Clear();
    }
}
