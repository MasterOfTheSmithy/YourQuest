using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering.Universal;
using Unity.Profiling;

// note: Camera immersion and player splashes share the streamed surface query; presentation never moves the player or owns breath.
[DisallowMultipleComponent]
public sealed class YQUnderwaterVisual : MonoBehaviour
{
    private static readonly ProfilerMarker G08UpdateMarker = new ProfilerMarker("G08FrameCost.YQUnderwaterVisual.Update()");
    private Camera targetCamera;
    private Image overlay;
    private GameObject canvasObject;
    private AudioLowPassFilter underwaterFilter;
    private bool ownsFilter;
    private bool originalFilterEnabled;
    private float originalCutoff;
    private float originalResonance;
    private float immersion;
    private ParticleSystem droplets;
    private ParticleSystem foam;
    private Material particleMaterial;
    private Texture2D particleTexture;
    private float nextSplash;
    private Vector3 lastPlayerPosition;
    private YQInvestorPlayerMotor lastMotor;
    private bool previouslyWet;

    public static void Attach(Transform generatedRoot)
    {
        if (generatedRoot == null || generatedRoot.GetComponentInChildren<YQUnderwaterVisual>(true) != null) return;
        GameObject host = new GameObject("YQ_UnderwaterVisual");
        host.transform.SetParent(generatedRoot, false);
        host.AddComponent<YQUnderwaterVisual>();
    }

    private void EnsureCamera(Camera camera)
    {
        if (camera == targetCamera && overlay != null) return;
        ReleaseCamera();
        targetCamera = camera;
        if (targetCamera == null) return;
        targetCamera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
        canvasObject = new GameObject("YQ_UnderwaterCanvas");
        canvasObject.transform.SetParent(targetCamera.transform, false);
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // note: Tint the world beneath the existing HUD so oxygen values stay sharp and readable underwater.
        canvas.sortingOrder = 4100;
        GameObject imageObject = new GameObject("YQ_UnderwaterTint", typeof(RectTransform));
        imageObject.transform.SetParent(canvasObject.transform, false);
        overlay = imageObject.AddComponent<Image>();
        overlay.raycastTarget = false;
        overlay.color = new Color(.025f, .18f, .25f, 0f);
        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // note: Unity's listener low-pass filters the complete heard mix, including sources created by future streamed content.
        AudioListener listener = targetCamera.GetComponent<AudioListener>();
        if (listener != null)
        {
            underwaterFilter = listener.GetComponent<AudioLowPassFilter>();
            ownsFilter = underwaterFilter == null;
            if (ownsFilter) underwaterFilter = listener.gameObject.AddComponent<AudioLowPassFilter>();
            originalFilterEnabled = underwaterFilter.enabled;
            originalCutoff = underwaterFilter.cutoffFrequency;
            originalResonance = underwaterFilter.lowpassResonanceQ;
            if (ownsFilter) underwaterFilter.enabled = false;
        }
    }

    private void Update()
    {
        using (G08UpdateMarker.Auto())
        {
            YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
            EnsureCamera(motor != null ? motor.playerCamera : Camera.main);
            bool gameplay = motor != null && !YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked;
            Vector3 position = targetCamera != null ? targetCamera.transform.position : Vector3.zero;
            float height = 0f;
            bool submerged = gameplay && YQWaterSurfaceMotion.TryGetWaterHeight(position, out height) && position.y < height - .04f;
            float target = submerged ? Mathf.Clamp01((height - position.y) * .18f + .25f) : 0f;
            immersion = Mathf.MoveTowards(immersion, target, Time.unscaledDeltaTime * 2.5f);
            if (overlay != null)
            {
                // note: Gentle fluctuating absorption provides a readable underwater cue without changing camera aim or applying a second camera controller.
                float wave = .012f * Mathf.Sin(Time.unscaledTime * 2.4f);
                overlay.color = new Color(.025f, .18f + wave, .25f + wave, immersion * .38f);
            }
            if (underwaterFilter != null)
            {
                if (immersion > .001f)
                {
                    underwaterFilter.enabled = true;
                    underwaterFilter.cutoffFrequency = Mathf.Lerp(22000f, 750f + 180f * Mathf.Sin(Time.unscaledTime * 2.1f), Mathf.Clamp01(immersion * 4f));
                    underwaterFilter.lowpassResonanceQ = 1.35f;
                }
                else RestoreFilter();
            }
            if (gameplay && Time.deltaTime > 0f) UpdateMovementSplash(motor);
        }
    }

    private void UpdateMovementSplash(YQInvestorPlayerMotor motor)
    {
        Vector3 current = motor.transform.position;
        if (lastMotor != motor)
        {
            lastMotor = motor;
            lastPlayerPosition = current;
            previouslyWet = false;
        }
        bool wet = YQWaterSurfaceMotion.TryGetWaterHeight(current, out float height) && current.y < height + .1f && current.y > height - 2.4f;
        float speed = (current - lastPlayerPosition).magnitude / Mathf.Max(.001f, Time.deltaTime);
        lastPlayerPosition = current;
        bool entry = wet && !previouslyWet;
        previouslyWet = wet;
        if (!wet || (!entry && speed < .15f) || Time.time < nextSplash) return;
        EnsureSplashParticles();
        if (droplets == null || foam == null) return;
        nextSplash = Time.time + (motor.IsSwimming ? .28f : .20f);
        Vector3 origin = new Vector3(current.x, height + .035f, current.z);
        int count = entry ? 18 : Mathf.Clamp(Mathf.RoundToInt(speed * 3f), 3, 12);
        // note: Emit into two persistent, capped world-space pools; locomotion never instantiates a new VFX object per step.
        for (int index = 0; index < count; index++)
        {
            float angle = index * 2.399963f;
            Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var emit = new ParticleSystem.EmitParams
            {
                position = origin + side * .18f,
                velocity = side * (.35f + speed * .09f) + Vector3.up * (entry ? 1.7f : .7f),
                startSize = .045f + (index % 3) * .015f,
                startLifetime = .45f + (index % 3) * .08f,
                startColor = new Color(.8f, .94f, 1f, .78f)
            };
            droplets.Emit(emit, 1);
        }
        var ripple = new ParticleSystem.EmitParams { position = origin, velocity = Vector3.zero, startSize = entry ? .85f : .48f, startLifetime = 1.2f, startColor = new Color(.88f, .98f, 1f, .5f) };
        foam.Emit(ripple, 1);
    }

    private void EnsureSplashParticles()
    {
        if (droplets != null) return;
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        // note: Native blood splashes and lit fish-splash assets are unsuitable for transparent URP water droplets; this tiny runtime foam mask is presentation geometry.
        particleTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
        {
            float radius = new Vector2((x - 15.5f) / 15.5f, (y - 15.5f) / 15.5f).magnitude;
            float alpha = Mathf.Clamp01((1f - radius) * 8f) * Mathf.Clamp01((radius - .55f) * 8f);
            pixels[y * 32 + x] = new Color(1f, 1f, 1f, alpha);
        }
        particleTexture.SetPixels(pixels);
        particleTexture.Apply(false, true);
        particleMaterial = new Material(shader) { name = "YQ_RuntimeWaterFoam" };
        particleMaterial.mainTexture = particleTexture;
        if (particleMaterial.HasProperty("_Surface")) particleMaterial.SetFloat("_Surface", 1f);
        if (particleMaterial.HasProperty("_Blend")) particleMaterial.SetFloat("_Blend", 0f);
        if (particleMaterial.HasProperty("_SrcBlend")) particleMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (particleMaterial.HasProperty("_DstBlend")) particleMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (particleMaterial.HasProperty("_ZWrite")) particleMaterial.SetFloat("_ZWrite", 0f);
        particleMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        particleMaterial.renderQueue = 3000;
        droplets = CreateParticlePool("YQ_PlayerWaterDroplets", false);
        foam = CreateParticlePool("YQ_PlayerWaterWake", true);
    }

    private ParticleSystem CreateParticlePool(string label, bool flat)
    {
        GameObject host = new GameObject(label);
        host.transform.SetParent(transform, false);
        ParticleSystem system = host.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.maxParticles = flat ? 48 : 192;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = flat ? 0f : .28f;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
        var color = system.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;
        var size = system.sizeOverLifetime;
        size.enabled = flat;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, .5f, 1f, 2f));
        ParticleSystemRenderer renderer = host.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = particleMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = flat ? ParticleSystemRenderSpace.World : ParticleSystemRenderSpace.View;
        if (flat) { main.startRotation3D = true; main.startRotationX = Mathf.PI * .5f; }
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
        return system;
    }

    private void RestoreFilter()
    {
        if (underwaterFilter == null) return;
        underwaterFilter.cutoffFrequency = originalCutoff;
        underwaterFilter.lowpassResonanceQ = originalResonance;
        underwaterFilter.enabled = ownsFilter ? false : originalFilterEnabled;
    }

    private void ReleaseCamera()
    {
        RestoreFilter();
        if (ownsFilter && underwaterFilter != null) Destroy(underwaterFilter);
        underwaterFilter = null;
        if (canvasObject != null) Destroy(canvasObject);
        canvasObject = null;
        overlay = null;
    }

    private void OnDisable() { immersion = 0f; if (overlay != null) overlay.color = Color.clear; RestoreFilter(); }
    private void OnDestroy()
    {
        ReleaseCamera();
        if (particleMaterial != null) Destroy(particleMaterial);
        if (particleTexture != null) Destroy(particleTexture);
    }
}
