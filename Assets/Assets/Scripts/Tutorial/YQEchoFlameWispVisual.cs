using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-120)]
public sealed class YQEchoFlameWispVisual : MonoBehaviour
{
    public Color emberColor = new Color(1f, 0.42f, 0.12f, 1f);
    public Color echoColor = new Color(0.52f, 0.78f, 1f, 1f);
    public float hoverHeight = 1.05f;
    public float hoverAmplitude = 0.18f;

    private Transform _visualRoot;
    private Transform _core;
    private Vector3 _baseLocalPosition;
    private float _phase;

    public void ApplyPalette(Color ember, Color echo)
    {
        emberColor = ember;
        echoColor = echo;
        if (_visualRoot == null)
        {
            BuildVisual();
            return;
        }

        Renderer coreRenderer = _visualRoot.Find("Core")?.GetComponent<Renderer>();
        if (coreRenderer != null)
            coreRenderer.sharedMaterial = CreateWispMaterial(emberColor);

        Renderer haloRenderer = _visualRoot.Find("EchoHalo")?.GetComponent<Renderer>();
        if (haloRenderer != null)
            haloRenderer.sharedMaterial = CreateWispMaterial(new Color(echoColor.r, echoColor.g, echoColor.b, 0.36f));

        Light light = _visualRoot.GetComponent<Light>();
        if (light != null)
            light.color = emberColor;
    }

    private void Awake()
    {
        _phase = Random.value * 6.28f;
        ConfigurePhysics();
        HideLegacyRenderer();
        BuildVisual();
    }

    private void Update()
    {
        if (_visualRoot == null)
            return;

        float pulse = Mathf.Sin(Time.time * 4.2f + _phase);
        _visualRoot.localPosition = _baseLocalPosition + new Vector3(0f, pulse * hoverAmplitude, 0f);
        _visualRoot.Rotate(Vector3.up, 48f * Time.deltaTime, Space.Self);

        if (_core != null)
        {
            float scale = 1f + Mathf.Sin(Time.time * 6.5f + _phase) * 0.08f;
            _core.localScale = new Vector3(0.72f * scale, 0.98f * scale, 0.72f * scale);
        }
    }

    private void ConfigurePhysics()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
            rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            capsule = gameObject.AddComponent<CapsuleCollider>();

        capsule.height = 1.55f;
        capsule.radius = 0.48f;
        capsule.center = new Vector3(0f, 0.92f, 0f);
    }

    private void HideLegacyRenderer()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
            renderer.enabled = false;
    }

    private void BuildVisual()
    {
        Transform existing = transform.Find("EchoFlameWispVisual");
        if (existing != null)
        {
            _visualRoot = existing;
            _baseLocalPosition = _visualRoot.localPosition;
            _core = _visualRoot.Find("Core");
            return;
        }

        GameObject root = new GameObject("EchoFlameWispVisual");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, hoverHeight, 0f);
        root.transform.localRotation = Quaternion.identity;
        _visualRoot = root.transform;
        _baseLocalPosition = _visualRoot.localPosition;

        GameObject core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(_visualRoot, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = new Vector3(0.72f, 0.98f, 0.72f);
        Collider coreCollider = core.GetComponent<Collider>();
        if (coreCollider != null)
            DestroyUnityObject(coreCollider);
        _core = core.transform;

        Renderer coreRenderer = core.GetComponent<Renderer>();
        if (coreRenderer != null)
        {
            coreRenderer.sharedMaterial = CreateWispMaterial(emberColor);
            coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
        }

        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = "EchoHalo";
        halo.transform.SetParent(_visualRoot, false);
        halo.transform.localPosition = Vector3.zero;
        halo.transform.localScale = new Vector3(1.2f, 0.42f, 1.2f);
        Collider haloCollider = halo.GetComponent<Collider>();
        if (haloCollider != null)
            DestroyUnityObject(haloCollider);

        Renderer haloRenderer = halo.GetComponent<Renderer>();
        if (haloRenderer != null)
        {
            haloRenderer.sharedMaterial = CreateWispMaterial(new Color(echoColor.r, echoColor.g, echoColor.b, 0.36f));
            haloRenderer.shadowCastingMode = ShadowCastingMode.Off;
            haloRenderer.receiveShadows = false;
        }

        AddParticleLoop(_visualRoot, emberColor, 0.34f, 26f, 0.11f);
        AddParticleLoop(_visualRoot, echoColor, 0.78f, 12f, 0.075f);

        Light light = root.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = emberColor;
        light.intensity = 1.05f;
        light.range = 4.2f;
        light.shadows = LightShadows.None;
    }

    private static void AddParticleLoop(Transform parent, Color color, float radius, float rate, float size)
    {
        GameObject go = new GameObject("WispParticles");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.duration = 1.3f;
        main.startLifetime = 0.62f;
        main.startSpeed = 0.42f;
        main.startSize = size;
        main.startColor = new Color(color.r, color.g, color.b, 0.72f);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = radius;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.material = YQGeneratedRuntimeVfx.CreateParticleMaterial(color);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        ps.Play(true);
    }

    private static void DestroyUnityObject(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    private static Material CreateWispMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.name = "YQ_EchoFlameWisp";
        material.hideFlags = HideFlags.DontSave;
        SetColor(material, color);
        ConfigureTransparent(material);
        return material;
    }

    private static void SetColor(Material material, Color color)
    {
        if (material == null)
            return;
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_EmissionColor"))
        {
            Color emission = new Color(Mathf.Clamp01(color.r * 2.4f), Mathf.Clamp01(color.g * 2.4f), Mathf.Clamp01(color.b * 2.4f), color.a);
            material.SetColor("_EmissionColor", emission);
            material.EnableKeyword("_EMISSION");
        }
    }

    private static void ConfigureTransparent(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 1f);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.One);
        material.SetInt("_ZWrite", 0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
