using UnityEngine;

// note: Animate water through a renderer property block so large generated meshes never upload a new UV array every frame.
[DisallowMultipleComponent]
public sealed class YQWaterSurfaceMotion : MonoBehaviour
{
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
