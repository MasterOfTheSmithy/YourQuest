using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class YQTitleEnvironmentScene : MonoBehaviour
{
    private struct GalaxyParticleState
    {
        public Vector3 center;
        public float radius;
        public float angle;
        public float depth;
        public float orbitSpeed;
        public float armTightness;
        public float size;
        public Color color;
    }

    private sealed class ShootingStarState
    {
        public LineRenderer line;
        public Vector3 origin;
        public Vector3 direction;
        public float distance;
        public float tailLength;
        public float delay;
        public float period;
        public float duration;
    }

    private struct MistParticleState
    {
        public Vector3 origin;
        public Vector3 velocity;
        public float size;
        public float phase;
        public Color color;
    }

    [SerializeField]
    private Camera titleCamera;

    [SerializeField]
    private Transform cameraTarget;

    [SerializeField]
    private Transform goddessRoot;

    [SerializeField]
    private Transform goddessPortraitCameraAnchor;

    [SerializeField]
    private Transform goddessPortraitLookTarget;

    [SerializeField]
    private Light goddessKeyLight;

    [SerializeField]
    private AudioSource uiAudioSource;

    [SerializeField]
    private AudioClip uiHoverClip;

    [SerializeField]
    private AudioClip uiConfirmClip;

    [SerializeField]
    private AudioClip thresholdTransitionClip;

    [SerializeField]
    private bool useGalaxyTitleBackdrop = true;

    [SerializeField]
    private float galaxyMotionAmplitude = 0.018f;

    [SerializeField]
    private float galaxyMotionSpeed = 0.08f;

    [SerializeField]
    private float driftDistance = 0.45f;

    [SerializeField]
    private float driftSpeed = 0.075f;

    [SerializeField]
    private int sceneRecipeVersion;

    private Vector3 _cameraOrigin;
    private Vector3 _titleCameraOrigin;
    private Quaternion _titleCameraRotation;
    private Quaternion _thresholdStartRotation;
    private bool _titlePoseCaptured;
    private Vector3 _lookOrigin;
    private Vector3 _thresholdStartPosition;
    private Vector3 _thresholdDestinationPosition;
    private Vector3 _thresholdDestinationLook;
    private float _thresholdTransitionStartedAt;
    private float _goddessKeyBaseIntensity;
    private float _nextUiSoundTime;
    private float _exitTransitionStartedAt;
    private bool _exitTransitionActive;
    private AudioSource[] _sceneAudioSources = System.Array.Empty<AudioSource>();
    private float[] _sceneAudioBaseVolumes = System.Array.Empty<float>();
    private const float ThresholdTransitionSeconds = 5f;
    private const float ExitTransitionSeconds = 0.46f;
    private bool _thresholdTransitionActive;
    private bool _thresholdPoseActive;
    private bool _generationIdleRequested;
    private bool _generationIdleActive;
    private readonly List<Camera> _suppressedGameplayCameras =
        new List<Camera>();
    private readonly List<AudioListener> _suppressedAudioListeners =
        new List<AudioListener>();
    private Material _galaxySkybox;
    private Material _galaxyBackdropMaterial;
    private GameObject _galaxyBackdropQuad;
    private Vector3 _galaxyBackdropBasePosition;
    private Material _previousSkybox;
    private Volume _titleRuntimeVolume;
    private VolumeProfile _titleRuntimeProfile;
    private Light _titleFillLight;
    private Light _titleRimLight;
    private Light _titleSceneFillLight;
    private float _titleFillBaseIntensity;
    private float _titleRimBaseIntensity;
    private float _titleSceneFillBaseIntensity;
    private GameObject _animatedUniverseRoot;
    private ParticleSystem _galaxyParticleSystem;
    private ParticleSystem.Particle[] _galaxyParticles = System.Array.Empty<ParticleSystem.Particle>();
    private GalaxyParticleState[] _galaxyParticleStates = System.Array.Empty<GalaxyParticleState>();
    private readonly List<ShootingStarState> _shootingStars =
        new List<ShootingStarState>();
    private Material _universeParticleMaterial;
    private Material _shootingStarMaterial;
    private Material _mistMaterial;
    private Texture2D _celestialSpriteTexture;
    private ParticleSystem _mistSystem;
    private ParticleSystem.Particle[] _mistParticles = System.Array.Empty<ParticleSystem.Particle>();
    private MistParticleState[] _mistStates = System.Array.Empty<MistParticleState>();

    public bool ExitTransitionComplete => !_exitTransitionActive ||
        Time.unscaledTime - _exitTransitionStartedAt >= ExitTransitionSeconds;

    public void Configure(
        Camera newTitleCamera,
        Transform newCameraTarget,
        Transform newGoddessRoot,
        Transform newGoddessPortraitCameraAnchor,
        Transform newGoddessPortraitLookTarget,
        Light newGoddessKeyLight,
        AudioSource newUiAudioSource,
        AudioClip newUiHoverClip,
        AudioClip newUiConfirmClip,
        AudioClip newThresholdTransitionClip,
        int newSceneRecipeVersion)
    {
        // note: The scene builder persists this camera contract so runtime presentation never searches the gameplay world for a camera or target.
        titleCamera = newTitleCamera;
        cameraTarget = newCameraTarget;
        goddessRoot = newGoddessRoot;
        goddessPortraitCameraAnchor = newGoddessPortraitCameraAnchor;
        goddessPortraitLookTarget = newGoddessPortraitLookTarget;
        goddessKeyLight = newGoddessKeyLight;
        uiAudioSource = newUiAudioSource;
        uiHoverClip = newUiHoverClip;
        uiConfirmClip = newUiConfirmClip;
        thresholdTransitionClip = newThresholdTransitionClip;
        sceneRecipeVersion = Mathf.Max(0, newSceneRecipeVersion);
    }

    private void Awake()
    {
        bool generationCameraReleased =
            YQTitleEnvironmentLoader.IsWorldGenerationCameraReleased;

        // note: Apply the authored galaxy backdrop only while the additive title stage owns presentation.
        ApplyGalaxyTitleBackdrop();
        EnsureTitleVisualPolish();
        if (titleCamera != null)
        {
            if (generationCameraReleased)
                titleCamera.enabled = false;

            _cameraOrigin = titleCamera.transform.position;
            CaptureTitlePose();
            Transform obsoleteWordmark = titleCamera.transform.Find("04__YourQuest_3DWordmark");
            if (obsoleteWordmark != null)
            {
                // note: Recipe 9 removes the failed camera-space plaque; this guard also suppresses it in an older serialized title scene before the editor rebuild runs.
                obsoleteWordmark.gameObject.SetActive(false);
            }
        }
        if (cameraTarget != null)
            _lookOrigin = cameraTarget.position;
        ResolveGoddessRoot();
        if (goddessKeyLight != null)
            _goddessKeyBaseIntensity = goddessKeyLight.intensity;

        // note: Persisted URP adapters are primary; the lightweight compatibility pass touches only genuinely unsupported leftovers and avoids recreating dozens of materials every title load.
        GameObject[] sceneRoots = gameObject.scene.GetRootGameObjects();
        for (int index = 0; index < sceneRoots.Length; index++)
            YQRuntimeUrpMaterialRepair.RepairHierarchy(sceneRoots[index]);

        Camera[] cameras = Camera.allCameras;
        for (int index = 0; index < cameras.Length; index++)
        {
            Camera camera = cameras[index];
            if (camera == null || camera == titleCamera ||
                !camera.enabled || generationCameraReleased)
                continue;

            // note: Gameplay remains initialized behind the startup gate, but its camera does not waste a full render underneath the opaque title camera.
            camera.enabled = false;
            _suppressedGameplayCameras.Add(camera);
        }

        AudioListener titleListener = titleCamera != null
            ? titleCamera.GetComponent<AudioListener>()
            : null;
        AudioListener[] listeners = FindObjectsByType<AudioListener>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (int index = 0; index < listeners.Length; index++)
        {
            AudioListener listener = listeners[index];
            if (listener == null || listener == titleListener || !listener.enabled || generationCameraReleased)
                continue;

            // note: The additive title stage owns one listener while visible, preventing doubled ambience from dormant gameplay cameras.
            listener.enabled = false;
            _suppressedAudioListeners.Add(listener);
        }
        // note: A late additive load must not introduce a second listener after an actual gameplay-camera handoff.
        if (generationCameraReleased && titleListener != null)
            titleListener.enabled = false;

        List<AudioSource> sceneAudio = new List<AudioSource>();
        for (int rootIndex = 0; rootIndex < sceneRoots.Length; rootIndex++)
            sceneAudio.AddRange(
                sceneRoots[rootIndex].GetComponentsInChildren<AudioSource>(true));
        _sceneAudioSources = sceneAudio.ToArray();
        _sceneAudioBaseVolumes = new float[_sceneAudioSources.Length];
        for (int index = 0; index < _sceneAudioSources.Length; index++)
            _sceneAudioBaseVolumes[index] = _sceneAudioSources[index] != null
                ? _sceneAudioSources[index].volume
                : 0f;
    }

    private void ApplyGalaxyTitleBackdrop()
    {
        // note: Keep the title image in Resources so the additive stage can load it without adding a second scene or mutating gameplay assets.
        if (!useGalaxyTitleBackdrop)
            return;
        Texture2D backdrop = Resources.Load<Texture2D>("TitleScreen/YourQuestGalaxyTitleBackdrop");
        if (backdrop == null)
            return;

        // note: Capture the active environment before applying the title-only presentation layer.
        _previousSkybox = RenderSettings.skybox;
        Shader panoramicShader = Shader.Find("Skybox/Panoramic");
        if (panoramicShader != null)
        {
            // note: Use the panoramic skybox when the active render pipeline exposes the built-in shader.
            _galaxySkybox = new Material(panoramicShader)
            {
                name = "YQ_TitleGalaxySkybox_Runtime"
            };
            _galaxySkybox.SetTexture("_Tex", backdrop);
            _galaxySkybox.SetColor("_Tint", new Color(0.82f, 0.88f, 1f, 1f));
            _galaxySkybox.SetFloat("_Exposure", 0.72f);
            _galaxySkybox.SetFloat("_Rotation", 0f);
            RenderSettings.skybox = _galaxySkybox;
            DynamicGI.UpdateEnvironment();
        }

        // note: Add a camera-local fallback so an additive scene or URP shader mismatch cannot leave the title without its galaxy backdrop.
        CreateGalaxyBackdropQuad(backdrop);
    }

    private void CreateGalaxyBackdropQuad(Texture2D backdrop)
    {
        if (titleCamera == null || backdrop == null)
            return;

        // note: Prefer the URP unlit shader and fall back to the legacy unlit texture shader for older project render paths.
        Shader backdropShader = Shader.Find("Universal Render Pipeline/Unlit");
        if (backdropShader == null)
            backdropShader = Shader.Find("Unlit/Texture");
        if (backdropShader == null)
            return;

        // note: Create a double-sided, non-lighting material so the image remains visible regardless of quad winding or scene lighting.
        _galaxyBackdropMaterial = new Material(backdropShader)
        {
            name = "YQ_TitleGalaxyBackdrop_Runtime",
            renderQueue = 1000
        };
        if (_galaxyBackdropMaterial.HasProperty("_BaseMap"))
            _galaxyBackdropMaterial.SetTexture("_BaseMap", backdrop);
        if (_galaxyBackdropMaterial.HasProperty("_MainTex"))
            _galaxyBackdropMaterial.SetTexture("_MainTex", backdrop);
        if (_galaxyBackdropMaterial.HasProperty("_Cull"))
            _galaxyBackdropMaterial.SetInt("_Cull", 0);
        if (_galaxyBackdropMaterial.HasProperty("_ZWrite"))
            _galaxyBackdropMaterial.SetInt("_ZWrite", 0);

        // note: Place the image in camera space behind the title composition and size it to fill the current view.
        _galaxyBackdropQuad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        _galaxyBackdropQuad.name = "YQ_TitleGalaxyBackdrop";
        _galaxyBackdropQuad.transform.SetParent(titleCamera.transform, false);
        float distance = Mathf.Clamp(titleCamera.farClipPlane * 0.45f, 20f, 60f);
        float height = titleCamera.orthographic
            ? titleCamera.orthographicSize * 2f
            : 2f * distance * Mathf.Tan(titleCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * titleCamera.aspect;
        _galaxyBackdropQuad.transform.localPosition = Vector3.forward * distance;
        _galaxyBackdropQuad.transform.localRotation = Quaternion.identity;
        // note: Overscan the camera-local plate so its gentle rotation and drift never reveal a hard image edge at the viewport.
        _galaxyBackdropQuad.transform.localScale = new Vector3(width * 1.08f, height * 1.08f, 1f);
        _galaxyBackdropBasePosition = _galaxyBackdropQuad.transform.localPosition;
        _galaxyBackdropQuad.layer = titleCamera.gameObject.layer;
        MeshRenderer renderer = _galaxyBackdropQuad.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = _galaxyBackdropMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void EnsureTitleVisualPolish()
    {
        if (titleCamera == null)
            return;

        // note: Include both the default volume layer and the authored title layer so future scene profiles remain visible to this camera.
        UniversalAdditionalCameraData cameraData =
            titleCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;
        LayerMask volumeLayerMask = cameraData.volumeLayerMask;
        volumeLayerMask.value |= (1 << 0) | (1 << titleCamera.gameObject.layer);
        cameraData.volumeLayerMask = volumeLayerMask;

        // note: Build a runtime fallback grade because the serialized title profile currently contains no effect components.
        if (_titleRuntimeVolume == null)
        {
            GameObject volumeObject = new GameObject("YQ_TitleRuntimeGrade");
            volumeObject.layer = 0;
            _titleRuntimeVolume = volumeObject.AddComponent<Volume>();
            _titleRuntimeVolume.isGlobal = true;
            _titleRuntimeVolume.priority = 110f;
            _titleRuntimeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            _titleRuntimeProfile.name = "YQ Title Runtime Grade";
            _titleRuntimeVolume.sharedProfile = _titleRuntimeProfile;

            // note: ACES and restrained bloom shape bright nebula edges while keeping title text and portrait detail legible.
            Bloom bloom = _titleRuntimeProfile.Add<Bloom>(true);
            bloom.intensity.Override(0.22f);
            bloom.threshold.Override(1.15f);
            bloom.scatter.Override(0.55f);
            Tonemapping tonemapping = _titleRuntimeProfile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            // note: Lift the midtones slightly and cool the highlights to recover the goddess silhouette from the original near-black grade.
            ColorAdjustments color = _titleRuntimeProfile.Add<ColorAdjustments>(true);
            color.postExposure.Override(0.28f);
            color.contrast.Override(8f);
            color.saturation.Override(2f);
            color.colorFilter.Override(new Color(0.94f, 0.98f, 1f, 1f));
            Vignette vignette = _titleRuntimeProfile.Add<Vignette>(true);
            vignette.color.Override(new Color(0.004f, 0.012f, 0.03f, 1f));
            vignette.intensity.Override(0.16f);
            vignette.smoothness.Override(0.38f);
        }

        // note: Add local portrait lights around the existing 3D goddess so the questionnaire close-up has readable form, warm key light, and a cool rim.
        CreateCinematicPortraitLights();

        // note: Keep the nebula plate as the base layer and add independently animated celestial elements for visible depth and motion.
        CreateAnimatedUniverseLayer();
    }

    private void CreateCinematicPortraitLights()
    {
        if (titleCamera == null || _titleFillLight != null)
            return;

        ResolveGoddessRoot();
        Bounds portraitBounds = new Bounds(
            cameraTarget != null ? cameraTarget.position : titleCamera.transform.position + titleCamera.transform.forward * 6f,
            Vector3.one * 2f);
        if (goddessRoot != null)
        {
            Renderer[] renderers = goddessRoot.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null || !renderer.enabled)
                    continue;
                if (!hasBounds)
                {
                    portraitBounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    portraitBounds.Encapsulate(renderer.bounds);
                }
            }
        }

        Transform cameraTransform = titleCamera.transform;
        Vector3 center = portraitBounds.center;
        Vector3 towardCamera = -cameraTransform.forward;
        int titleLayerMask = cameraDataMask(titleCamera);

        // note: A warm key from the camera side restores skin and stone separation without flattening the sculpted shadows.
        _titleFillLight = CreatePortraitPointLight(
            "YQ_Title_Warm_Fill",
            center + towardCamera * 4.5f - cameraTransform.right * 2.5f + Vector3.up * 2.2f,
            new Color(1f, 0.62f, 0.38f, 1f),
            7.5f,
            15f,
            titleLayerMask,
            true);
        _titleFillBaseIntensity = _titleFillLight.intensity;

        // note: A cool opposing fill preserves facial planes and keeps the frame consistent with the blue-violet space palette.
        _titleRimLight = CreatePortraitPointLight(
            "YQ_Title_Cool_Rim",
            center + towardCamera * 1.5f + cameraTransform.right * 4.2f + Vector3.up * 1.8f,
            new Color(0.24f, 0.58f, 1f, 1f),
            6f,
            13f,
            titleLayerMask,
            false);
        _titleRimBaseIntensity = _titleRimLight.intensity;

        // note: A broad, dim fill keeps the authored shrine readable in the wide save-selection composition without competing with the portrait key.
        Vector3 sceneFillTarget = cameraTarget != null ? cameraTarget.position : center;
        _titleSceneFillLight = CreatePortraitPointLight(
            "YQ_Title_Scene_Fill",
            sceneFillTarget + towardCamera * 7f + Vector3.up * 2.5f,
            new Color(0.28f, 0.46f, 0.78f, 1f),
            2.4f,
            32f,
            titleLayerMask,
            false);
        _titleSceneFillBaseIntensity = _titleSceneFillLight.intensity;
    }

    private static int cameraDataMask(Camera camera)
    {
        // note: Keep dynamically created lights on the same isolated layer as the title camera and its authored geometry.
        return camera != null ? camera.cullingMask : -1;
    }

    private static Light CreatePortraitPointLight(
        string lightName,
        Vector3 position,
        Color color,
        float intensity,
        float range,
        int cullingMask,
        bool castsShadows)
    {
        // note: Create a small runtime light with soft shadows so the authored statue remains the visual subject.
        GameObject lightObject = new GameObject(lightName);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.cullingMask = cullingMask;
        light.shadows = castsShadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = 0.55f;
        light.transform.position = position;
        return light;
    }

    private void CreateAnimatedUniverseLayer()
    {
        if (titleCamera == null || _animatedUniverseRoot != null)
            return;

        // note: Parent all procedural space elements to the title camera so they stay behind the authored 3D composition during portrait movement.
        _animatedUniverseRoot = new GameObject("YQ_AnimatedUniverse");
        _animatedUniverseRoot.transform.SetParent(titleCamera.transform, false);
        _animatedUniverseRoot.layer = titleCamera.gameObject.layer;

        // note: Use the project particle shader when available so galaxy stars and cores support soft alpha and additive-looking bloom.
        Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Particles/Standard Unlit");
        if (particleShader == null)
            particleShader = Shader.Find("Sprites/Default");
        if (particleShader == null)
            return;

        // note: A generated radial alpha texture keeps stars, clouds, meteors, and mist soft without adding another imported asset.
        _celestialSpriteTexture = CreateSoftCircleTexture(64);

        _universeParticleMaterial = new Material(particleShader)
        {
            name = "YQ_AnimatedUniverse_Stars_Runtime",
            renderQueue = 3000
        };
        if (_universeParticleMaterial.HasProperty("_BaseMap"))
            _universeParticleMaterial.SetTexture("_BaseMap", _celestialSpriteTexture);
        if (_universeParticleMaterial.HasProperty("_MainTex"))
            _universeParticleMaterial.SetTexture("_MainTex", _celestialSpriteTexture);
        if (_universeParticleMaterial.HasProperty("_ZTest"))
            _universeParticleMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        if (_universeParticleMaterial.HasProperty("_Cull"))
            _universeParticleMaterial.SetInt("_Cull", 0);

        // note: Disable built-in emission and author deterministic spiral positions so every galaxy follows a stable orbital flow.
        GameObject galaxyObject = new GameObject("YQ_SwirlingGalaxyParticles");
        galaxyObject.transform.SetParent(_animatedUniverseRoot.transform, false);
        galaxyObject.layer = titleCamera.gameObject.layer;
        _galaxyParticleSystem = galaxyObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = _galaxyParticleSystem.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 320;
        main.startLifetime = 99999f;
        main.startSpeed = 0f;
        main.startSize = 0.08f;
        main.startColor = Color.white;
        ParticleSystem.EmissionModule emission = _galaxyParticleSystem.emission;
        emission.enabled = false;
        ParticleSystemRenderer particleRenderer =
            _galaxyParticleSystem.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.material = _universeParticleMaterial;

        const int particleCount = 320;
        _galaxyParticleStates = new GalaxyParticleState[particleCount];
        _galaxyParticles = new ParticleSystem.Particle[particleCount];
        for (int index = 0; index < particleCount; index++)
        {
            // note: Split particles between two edge galaxies and bias their radii toward visible spiral arms and bright cores.
            int galaxyIndex = index < particleCount / 2 ? 0 : 1;
            int localIndex = index % (particleCount / 2);
            float seed = localIndex * 0.6180339887f + galaxyIndex * 17.31f;
            float radius = Mathf.Lerp(0.18f, 4.8f, Mathf.Pow(Hash01(seed), 0.62f));
            float arm = (localIndex % 5) * (Mathf.PI * 2f / 5f);
            float angle = arm + Hash01(seed + 4.2f) * 1.2f + radius * 0.72f;
            float warm = Mathf.Clamp01(1f - radius / 5.2f);
            _galaxyParticleStates[index] = new GalaxyParticleState
            {
                center = new Vector3(galaxyIndex == 0 ? -14.5f : 14.5f, galaxyIndex == 0 ? 4.8f : 4.2f, 56f),
                radius = radius,
                angle = angle,
                depth = Hash01(seed + 8.8f) * 3f,
                orbitSpeed = 0.018f + Hash01(seed + 11.1f) * 0.022f,
                armTightness = 0.66f + Hash01(seed + 13.7f) * 0.18f,
                size = Mathf.Lerp(0.045f, 0.13f, warm) + Hash01(seed + 16.4f) * 0.035f,
                color = Color.Lerp(
                    new Color(0.25f, 0.48f, 1f, 0.76f),
                    new Color(1f, 0.68f, 0.42f, 0.95f),
                    warm * 0.72f)
            };
        }
        _galaxyParticleSystem.Pause(true);
        UpdateGalaxyParticles(0f);
        CreateShootingStars();
        CreateFloorMist(particleShader);
    }

    private void CreateFloorMist(Shader particleShader)
    {
        // note: Keep only a low floor mist layer from the optional celestial additions so the authored shrine remains the visual subject.
        _mistMaterial = CreateCelestialMaterial(particleShader, "YQ_FloorMist_Runtime", new Color(0.38f, 0.62f, 0.78f, 0.24f));
        _mistSystem = CreateCelestialParticleSystem("YQ_FloorMist", _mistMaterial, 28, out _mistParticles, out _mistStates);
        SeedFloorMist();
        UpdateFloorMist(Time.unscaledTime);
    }

    private Material CreateCelestialMaterial(Shader shader, string materialName, Color color)
    {
        // note: Configure transparent depth-tested material state so opaque assets hide anything behind them.
        Material material = new Material(shader) { name = materialName, renderQueue = 3000 };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", _celestialSpriteTexture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", _celestialSpriteTexture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        return material;
    }

    private ParticleSystem CreateCelestialParticleSystem(string systemName, Material material, int count,
        out ParticleSystem.Particle[] particles, out MistParticleState[] states)
    {
        // note: Use paused, manually positioned particles so title motion remains deterministic and allocation-free after setup.
        GameObject particleObject = new GameObject(systemName);
        particleObject.transform.SetParent(_animatedUniverseRoot.transform, false);
        particleObject.layer = titleCamera.gameObject.layer;
        ParticleSystem system = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = count;
        main.startLifetime = 99999f;
        main.startSpeed = 0f;
        main.startSize = 1f;
        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.material = material;
        particles = new ParticleSystem.Particle[count];
        states = new MistParticleState[count];
        system.Pause(true);
        return system;
    }

    private static Texture2D CreateSoftCircleTexture(int size)
    {
        // note: Build one small radial alpha mask for all soft celestial sprites and mist puffs.
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true) { name = "YQ_CelestialSoftCircle_Runtime" };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float dx = (x + 0.5f) / size * 2f - 1f;
            float dy = (y + 0.5f) / size * 2f - 1f;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy)), 1.6f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private void CreateShootingStars()
    {
        // note: Use a transparent line material so streaks read as brief luminous events rather than solid geometry.
        Shader streakShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (streakShader == null)
            streakShader = Shader.Find("Sprites/Default");
        if (streakShader == null)
            return;
        _shootingStarMaterial = new Material(streakShader)
        {
            name = "YQ_AnimatedUniverse_ShootingStars_Runtime",
            renderQueue = 3000
        };
        if (_shootingStarMaterial.HasProperty("_MainTex"))
            _shootingStarMaterial.SetTexture("_MainTex", Texture2D.whiteTexture);
        if (_shootingStarMaterial.HasProperty("_BaseMap"))
            _shootingStarMaterial.SetTexture("_BaseMap", _celestialSpriteTexture);
        if (_shootingStarMaterial.HasProperty("_Surface"))
            _shootingStarMaterial.SetFloat("_Surface", 1f);
        if (_shootingStarMaterial.HasProperty("_ZWrite"))
            _shootingStarMaterial.SetInt("_ZWrite", 0);
        if (_shootingStarMaterial.HasProperty("_ZTest"))
            _shootingStarMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);

        for (int index = 0; index < 5; index++)
        {
            // note: Stagger deterministic spawn windows so one or two streaks cross the readable outer frame at a time.
            GameObject starObject = new GameObject("YQ_ShootingStar_" + index);
            starObject.transform.SetParent(_animatedUniverseRoot.transform, false);
            starObject.layer = titleCamera.gameObject.layer;
            LineRenderer line = starObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.startWidth = 0.075f;
            line.endWidth = 0.004f;
            line.material = _shootingStarMaterial;
            line.textureMode = LineTextureMode.Stretch;
            line.sortingOrder = -100;
            _shootingStars.Add(new ShootingStarState
            {
                line = line,
                origin = new Vector3(-22f + index * 9.1f, 9.5f - index * 2.2f, 52f + index * 1.6f),
                direction = new Vector3(0.88f, -0.32f, 0f).normalized,
                distance = 8.5f + index * 1.1f,
                tailLength = 1.9f + index * 0.25f,
                delay = index * 3.7f,
                period = 13f + index * 1.9f,
                duration = 0.55f + index * 0.06f
            });
        }
    }

    private void UpdateGalaxyParticles(float time)
    {
        if (_galaxyParticleSystem == null)
            return;

        for (int index = 0; index < _galaxyParticleStates.Length; index++)
        {
            GalaxyParticleState state = _galaxyParticleStates[index];
            float angle = state.angle + time * state.orbitSpeed;
            float armAngle = angle + state.radius * state.armTightness;
            float armRadius = state.radius + Mathf.Sin(time * 0.28f + index * 0.37f) * 0.035f;
            Vector3 position = state.center + new Vector3(
                Mathf.Cos(armAngle) * armRadius,
                Mathf.Sin(armAngle) * armRadius * 0.46f,
                state.depth + Mathf.Sin(angle * 1.7f) * 0.22f);
            float shimmer = 0.92f + Mathf.Sin(time * 0.9f + index * 0.41f) * 0.08f;
            ParticleSystem.Particle particle = _galaxyParticles[index];
            particle.position = position;
            particle.startSize = state.size * shimmer;
            particle.startColor = state.color * shimmer;
            particle.remainingLifetime = 99999f;
            particle.startLifetime = 99999f;
            _galaxyParticles[index] = particle;
        }
        _galaxyParticleSystem.SetParticles(_galaxyParticles, _galaxyParticles.Length);
    }

    private void UpdateShootingStars(float time)
    {
        for (int index = 0; index < _shootingStars.Count; index++)
        {
            ShootingStarState star = _shootingStars[index];
            float cycle = Mathf.Repeat(time + star.delay, star.period);
            float normalized = cycle / star.duration;
            if (cycle >= star.duration)
            {
                star.line.enabled = false;
                star.line.startColor = Color.clear;
                star.line.endColor = Color.clear;
                continue;
            }

            // note: Accelerate each streak through the frame and fade its tail to create a readable shooting-star gesture.
            star.line.enabled = true;
            float travel = Mathf.SmoothStep(0f, 1f, normalized);
            Vector3 head = star.origin + star.direction * (travel * star.distance);
            Vector3 tail = head - star.direction * Mathf.Lerp(star.tailLength, star.tailLength * 0.55f, travel);
            star.line.SetPosition(0, head);
            star.line.SetPosition(1, tail);
            float alpha = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            star.line.startColor = new Color(0.82f, 0.95f, 1f, alpha);
            star.line.endColor = new Color(0.35f, 0.68f, 1f, alpha * 0.05f);
        }
    }

    private void SeedFloorMist()
    {
        // note: Keep mist low and forward enough to soften the visible floor plane while depth testing still protects the goddess.
        for (int index = 0; index < _mistStates.Length; index++)
        {
            float seed = index * 0.293f;
            _mistStates[index] = new MistParticleState
            {
                origin = new Vector3(Mathf.Lerp(-11f, 11f, Hash01(seed)), -1.4f + Hash01(seed + 1f) * 1.4f, 13f + Hash01(seed + 3f) * 5f),
                velocity = new Vector3(0.09f + Hash01(seed + 4f) * 0.07f, 0.015f, 0f),
                size = 1.8f + Hash01(seed + 5f) * 2.6f,
                phase = seed,
                color = new Color(0.38f, 0.62f, 0.78f, 0.08f + Hash01(seed + 6f) * 0.08f)
            };
        }
    }

    private void UpdateFloorMist(float time)
    {
        // note: Drift only the low mist layer so it softens the floor without adding competing galaxy objects.
        ParticleSystem system = _mistSystem;
        ParticleSystem.Particle[] particles = _mistParticles;
        MistParticleState[] states = _mistStates;
        const float motionScale = 0.12f;
        if (system == null) return;
        for (int index = 0; index < states.Length; index++)
        {
            MistParticleState state = states[index];
            Vector3 position = state.origin + state.velocity * (time * motionScale);
            position.x = Mathf.Repeat(position.x + 24f, 48f) - 24f;
            float pulse = 0.9f + Mathf.Sin(time * 0.35f + state.phase * 6f) * 0.1f;
            ParticleSystem.Particle particle = particles[index];
            particle.position = position;
            particle.startSize = state.size * pulse;
            particle.startColor = state.color * pulse;
            particle.startLifetime = 99999f;
            particle.remainingLifetime = 99999f;
            particles[index] = particle;
        }
        system.SetParticles(particles, particles.Length);
    }

    private static float Hash01(float value)
    {
        // note: A small deterministic hash keeps the procedural celestial arrangement stable across runs and saved journeys.
        return Mathf.Repeat(Mathf.Sin(value * 12.9898f) * 43758.5453f, 1f);
    }

    public void BeginExitTransition()
    {
        if (_exitTransitionActive)
            return;

        // note: Ambience fades on unscaled time before the additive stage unloads, avoiding an abrupt cut beneath the loading handoff.
        _exitTransitionActive = true;
        _exitTransitionStartedAt = Time.unscaledTime;
    }

    public void CancelExitTransition()
    {
        _exitTransitionActive = false;
        for (int index = 0; index < _sceneAudioSources.Length; index++)
        {
            if (_sceneAudioSources[index] != null)
                _sceneAudioSources[index].volume = _sceneAudioBaseVolumes[index];
        }
    }

    public void PlayUiHover()
    {
        if (Time.unscaledTime < _nextUiSoundTime)
            return;

        _nextUiSoundTime = Time.unscaledTime + 0.055f;
        PlayOneShot(uiHoverClip, 0.22f);
    }

    public void PlayUiConfirm()
    {
        PlayOneShot(uiConfirmClip, 0.42f);
    }

    public void SuppressGameplayPresentationUntilRelease(Camera gameplayCamera)
    {
        if (gameplayCamera == null || gameplayCamera == titleCamera)
            return;

        if (YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked ||
            YQTitleEnvironmentLoader.IsWorldGenerationCameraReleased)
        {
            // note: Generation may overlap additive title cleanup, but it never grants this scene authority to disable the live gameplay camera.
            gameplayCamera.enabled = true;
            return;
        }

        if (gameplayCamera.enabled &&
            !_suppressedGameplayCameras.Contains(gameplayCamera))
        {
            // note: Gameplay can be constructed after the additive title scene awakens; explicitly transfer camera ownership before the first rendered gameplay frame.
            gameplayCamera.enabled = false;
            _suppressedGameplayCameras.Add(gameplayCamera);
        }

        AudioListener gameplayListener =
            gameplayCamera.GetComponent<AudioListener>();
        if (gameplayListener != null &&
            gameplayListener.enabled &&
            !_suppressedAudioListeners.Contains(gameplayListener))
        {
            // note: Exactly one listener remains active while the Goddess generation stage owns presentation, eliminating per-frame Unity warning spam.
            gameplayListener.enabled = false;
            _suppressedAudioListeners.Add(gameplayListener);
        }
    }

    public void ReleaseGameplayPresentation()
    {
        // note: Generation releases the gameplay camera synchronously instead of waiting for additive-scene unload to finish.
        if (titleCamera != null)
            titleCamera.enabled = false;

        for (int index = 0; index < _suppressedGameplayCameras.Count; index++)
        {
            Camera camera = _suppressedGameplayCameras[index];
            if (camera != null)
                camera.enabled = true;
        }

        _suppressedGameplayCameras.Clear();
        RestoreGameplayAudioPresentation();
    }

    public void BeginQuestionnaireTransition()
    {
        // note: Repeated UI/scene-ready notifications must not rewind an in-progress camera move or replay its sound.
        _generationIdleRequested = false;
        _generationIdleActive = false;
        BeginGoddessThresholdTransition();
    }

    private void CaptureTitlePose()
    {
        // note: Preserve the wide authored pose separately; the moving portrait overwrites the idle origin later.
        if (_titlePoseCaptured || titleCamera == null)
            return;
        _titleCameraOrigin = titleCamera.transform.position;
        _titleCameraRotation = titleCamera.transform.rotation;
        _titlePoseCaptured = true;
    }

    public void ShowTitleComposition()
    {
        CaptureTitlePose();
        if (titleCamera == null || cameraTarget == null || !_titlePoseCaptured)
            return;
        // note: Returning to the title resets the wide shot once, allowing the next questionnaire to visibly approach the face again.
        if (_thresholdTransitionActive || _thresholdPoseActive)
            titleCamera.transform.SetPositionAndRotation(_titleCameraOrigin, _titleCameraRotation);
        _cameraOrigin = _titleCameraOrigin;
        _lookOrigin = cameraTarget.position;
        _thresholdTransitionActive = _thresholdPoseActive = false;
        _generationIdleRequested = _generationIdleActive = false;
    }

    public void BeginGoddessThresholdTransition()
    {
        if (titleCamera == null || cameraTarget == null ||
            _thresholdTransitionActive || _thresholdPoseActive)
        {
            return;
        }

        // note: Move from the displayed wide shot to the authored face composition, with bounds only as a legacy fallback.
        CaptureTitlePose();
        _thresholdStartPosition = titleCamera.transform.position;
        _thresholdStartRotation = titleCamera.transform.rotation;
        if (!TryResolveGoddessPortraitPose(
                out _thresholdDestinationPosition,
                out _thresholdDestinationLook))
        {
            _thresholdDestinationPosition =
                cameraTarget.position + new Vector3(-5.5f, 4.1f, -6.2f);
            _thresholdDestinationLook =
                cameraTarget.position + Vector3.up * 2.4f;
        }
        _thresholdTransitionStartedAt = Time.unscaledTime;
        _thresholdTransitionActive = true;
        PlayOneShot(thresholdTransitionClip, 0.62f);
    }

    public void BeginGenerationIdle()
    {
        _generationIdleRequested = true;

        if (_thresholdPoseActive)
        {
            _generationIdleActive = true;
            return;
        }

        // note: Generation reaches the same authored face-and-upper-body portrait first, then changes to a slow orbit once the transition settles.
        BeginGoddessThresholdTransition();
    }

    private bool TryResolveGoddessPortraitPose(
        out Vector3 cameraPosition,
        out Vector3 lookPosition)
    {
        cameraPosition = Vector3.zero;
        lookPosition = Vector3.zero;
        ResolveGoddessRoot();
        if (goddessRoot == null)
            return false;

        Renderer[] renderers = goddessRoot.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds portraitBounds = default;
        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                portraitBounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                portraitBounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds || portraitBounds.size.y <= 0.1f)
            return false;

        // note: Use the live statue bounds for the vertical framing; the serialized anchors provide only the authored approach direction and distance.
        lookPosition = new Vector3(
            portraitBounds.center.x,
            portraitBounds.min.y + portraitBounds.size.y * 0.80f,
            portraitBounds.center.z);
        if (goddessPortraitCameraAnchor != null &&
            goddessPortraitLookTarget != null)
        {
            Vector3 authoredApproach =
                goddessPortraitCameraAnchor.position -
                goddessPortraitLookTarget.position;
            authoredApproach.y = 0f;
            float authoredDistance = authoredApproach.magnitude;

            if (authoredApproach.sqrMagnitude < 0.01f)
                authoredApproach = -goddessRoot.forward;
            authoredApproach.y = 0f;
            if (authoredApproach.sqrMagnitude < 0.01f)
                authoredApproach = new Vector3(-1f, 0f, -1f);
            authoredApproach.Normalize();
            cameraPosition = lookPosition +
                authoredApproach * Mathf.Max(
                    3.25f,
                    authoredDistance) +
                Vector3.up * (portraitBounds.size.y * 0.035f);
            return true;
        }

        // note: Legacy title recipes fall back to the statue's authored forward plane when no approach anchors are available.
        Vector3 approach = -goddessRoot.forward;
        approach.y = 0f;
        if (approach.sqrMagnitude < 0.01f)
            approach = _thresholdStartPosition - lookPosition;
        approach.y = 0f;
        if (approach.sqrMagnitude < 0.01f)
            approach = new Vector3(-1f, 0f, -1f);
        approach.Normalize();

        float portraitDistance = Mathf.Max(3.25f, portraitBounds.size.y * 0.72f);
        cameraPosition = lookPosition +
            approach * portraitDistance +
            Vector3.up * (portraitBounds.size.y * 0.035f);
        return true;
    }

    private void OnDestroy()
    {
        // note: Disable the title listener before restoring gameplay listeners; otherwise the unload frame briefly exposes two active listeners.
        AudioListener titleListener = titleCamera != null
            ? titleCamera.GetComponent<AudioListener>()
            : null;
        if (titleListener != null)
            titleListener.enabled = false;

        for (int index = 0; index < _suppressedGameplayCameras.Count; index++)
        {
            Camera camera = _suppressedGameplayCameras[index];
            if (camera != null)
                camera.enabled = true;
        }

        _suppressedGameplayCameras.Clear();

        RestoreGameplayAudioPresentation();
        RestoreTitleVisualState();
    }

    private void RestoreGameplayAudioPresentation()
    {
        // note: Restore exactly one non-title listener after an immediate handoff or normal additive-scene destruction.
        AudioListener titleListener = titleCamera != null
            ? titleCamera.GetComponent<AudioListener>()
            : null;
        if (titleListener != null)
            titleListener.enabled = false;

        // note: Re-enable listeners that were explicitly suppressed by the title stage before choosing the preferred gameplay listener.
        AudioListener preferred = null;
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera != titleCamera)
            preferred = mainCamera.GetComponent<AudioListener>();
        for (int index = 0; index < _suppressedAudioListeners.Count; index++)
        {
            AudioListener listener = _suppressedAudioListeners[index];
            if (listener != null)
                listener.enabled = true;
            if (preferred == null && listener != null && listener.enabled)
                preferred = listener;
        }
        AudioListener[] activeListeners = FindObjectsByType<AudioListener>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        if (preferred == null)
        {
            for (int index = 0; index < activeListeners.Length; index++)
            {
                AudioListener listener = activeListeners[index];
                if (listener != null && listener != titleListener)
                {
                    preferred = listener;
                    break;
                }
            }
        }
        for (int index = 0; index < activeListeners.Length; index++)
        {
            AudioListener listener = activeListeners[index];
            if (listener != null && listener != preferred)
                listener.enabled = false;
        }
        if (preferred != null)
            preferred.enabled = true;
        _suppressedAudioListeners.Clear();
    }

    private void RestoreTitleVisualState()
    {
        // note: Restore the prior world skybox and release the runtime material when the title additive scene leaves.
        if (_previousSkybox != null)
            RenderSettings.skybox = _previousSkybox;
        if (_galaxySkybox != null)
            Destroy(_galaxySkybox);
        _galaxySkybox = null;
        if (_galaxyBackdropQuad != null)
            Destroy(_galaxyBackdropQuad);
        if (_galaxyBackdropMaterial != null)
            Destroy(_galaxyBackdropMaterial);
        if (_titleRuntimeVolume != null)
            Destroy(_titleRuntimeVolume.gameObject);
        if (_titleRuntimeProfile != null)
            Destroy(_titleRuntimeProfile);
        if (_titleFillLight != null)
            Destroy(_titleFillLight.gameObject);
        if (_titleRimLight != null)
            Destroy(_titleRimLight.gameObject);
        if (_titleSceneFillLight != null)
            Destroy(_titleSceneFillLight.gameObject);
        if (_animatedUniverseRoot != null)
            Destroy(_animatedUniverseRoot);
        if (_universeParticleMaterial != null)
            Destroy(_universeParticleMaterial);
        if (_shootingStarMaterial != null)
            Destroy(_shootingStarMaterial);
        if (_mistMaterial != null)
            Destroy(_mistMaterial);
        if (_celestialSpriteTexture != null)
            Destroy(_celestialSpriteTexture);
        _galaxyBackdropQuad = null;
        _galaxyBackdropMaterial = null;
        _galaxyBackdropBasePosition = Vector3.zero;
        _titleRuntimeVolume = null;
        _titleRuntimeProfile = null;
        _titleFillLight = null;
        _titleRimLight = null;
        _titleSceneFillLight = null;
        _animatedUniverseRoot = null;
        _galaxyParticleSystem = null;
        _galaxyParticles = System.Array.Empty<ParticleSystem.Particle>();
        _galaxyParticleStates = System.Array.Empty<GalaxyParticleState>();
        _shootingStars.Clear();
        _universeParticleMaterial = null;
        _shootingStarMaterial = null;
        _mistMaterial = null;
        _celestialSpriteTexture = null;
        _mistSystem = null;
        _mistParticles = System.Array.Empty<ParticleSystem.Particle>();
        _mistStates = System.Array.Empty<MistParticleState>();
        _previousSkybox = null;
    }

    private void LateUpdate()
    {
        if (_exitTransitionActive)
        {
            float fade = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.Clamp01((Time.unscaledTime -
                    _exitTransitionStartedAt) / ExitTransitionSeconds));
            for (int index = 0; index < _sceneAudioSources.Length; index++)
            {
                if (_sceneAudioSources[index] != null)
                _sceneAudioSources[index].volume =
                        _sceneAudioBaseVolumes[index] * fade;
            }
        }

        // note: Give the nebula a restrained parallax and exposure movement so the title backdrop feels alive without distracting from the logo.
        AnimateGalaxyBackdrop();
        AnimateCinematicPortraitLights();
        float universeTime = Time.unscaledTime;
        UpdateGalaxyParticles(universeTime);
        UpdateShootingStars(universeTime);
        UpdateFloorMist(universeTime);
        if (titleCamera == null || cameraTarget == null)
            return;

        if (goddessKeyLight != null && _goddessKeyBaseIntensity > 0f)
        {
            // note: A very low-amplitude unscaled pulse keeps the portrait alive without visible disco flicker or frame allocations.
            goddessKeyLight.intensity = _goddessKeyBaseIntensity *
                (1f + Mathf.Sin(Time.unscaledTime * 0.72f) * 0.035f);
        }

        if (_thresholdTransitionActive)
        {
            float elapsed = Time.unscaledTime - _thresholdTransitionStartedAt;
            float normalized = Mathf.Clamp01(elapsed / ThresholdTransitionSeconds);
            float eased = Mathf.SmoothStep(0f, 1f, normalized);
            titleCamera.transform.position = Vector3.Lerp(
                _thresholdStartPosition,
                _thresholdDestinationPosition,
                eased);
            // note: Blend from the displayed orientation too, preventing an opening-frame snap when the title camera was drifting.
            titleCamera.transform.rotation = Quaternion.Slerp(
                _thresholdStartRotation,
                Quaternion.LookRotation(_thresholdDestinationLook - _thresholdDestinationPosition, Vector3.up),
                eased);
            if (normalized >= 1f)
            {
                _thresholdTransitionActive = false;
                _thresholdPoseActive = true;
                _generationIdleActive = _generationIdleRequested;
                _cameraOrigin = _thresholdDestinationPosition;
                _lookOrigin = _thresholdDestinationLook;
            }

            return;
        }

        if (_generationIdleActive)
        {
            float orbitPhase = Time.unscaledTime * 0.095f;
            Vector3 portraitOffset = _cameraOrigin - _lookOrigin;
            Quaternion orbit = Quaternion.AngleAxis(
                Mathf.Sin(orbitPhase) * 7.5f,
                Vector3.up);
            Vector3 idlePosition = _lookOrigin + orbit * portraitOffset;
            idlePosition += Vector3.up *
                (Mathf.Sin(orbitPhase * 0.71f) * driftDistance * 0.32f);
            titleCamera.transform.position = idlePosition;
            titleCamera.transform.rotation = Quaternion.LookRotation(
                _lookOrigin - idlePosition,
                Vector3.up);
            return;
        }

        // note: A sub-pixel-speed cinematic drift gives the static baked stage life without moving geometry, lights, or generating garbage.
        float phase = Time.unscaledTime * driftSpeed;
        titleCamera.transform.position = _cameraOrigin +
            titleCamera.transform.right * (Mathf.Sin(phase) * driftDistance) +
            Vector3.up * (Mathf.Cos(phase * 0.63f) * driftDistance * 0.15f);
        titleCamera.transform.rotation = Quaternion.LookRotation(
            (_thresholdPoseActive ? _lookOrigin : cameraTarget.position) -
                titleCamera.transform.position,
            Vector3.up);
    }

    private void AnimateGalaxyBackdrop()
    {
        if (_galaxyBackdropQuad == null)
            return;

        // note: Use unscaled time so the title animation remains smooth while menus or loading transitions pause gameplay time.
        float phase = Time.unscaledTime * galaxyMotionSpeed;
        Transform backdropTransform = _galaxyBackdropQuad.transform;
        backdropTransform.localPosition = _galaxyBackdropBasePosition +
            new Vector3(
                Mathf.Sin(phase) * galaxyMotionAmplitude,
                Mathf.Cos(phase * 0.73f) * galaxyMotionAmplitude * 0.55f,
                0f);
        backdropTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Sin(phase * 0.61f) * 0.35f);

        // note: Modulate the environment material gently to imitate changing nebula density while retaining the baked color balance.
        float exposure = 0.72f + Mathf.Sin(phase * 0.47f) * 0.018f;
        if (_galaxySkybox != null && _galaxySkybox.HasProperty("_Exposure"))
            _galaxySkybox.SetFloat("_Exposure", exposure);
        if (_galaxyBackdropMaterial != null)
        {
            Color tint = Color.white * (0.985f + Mathf.Sin(phase * 0.47f) * 0.012f);
            if (_galaxyBackdropMaterial.HasProperty("_BaseColor"))
                _galaxyBackdropMaterial.SetColor("_BaseColor", tint);
            if (_galaxyBackdropMaterial.HasProperty("_Color"))
                _galaxyBackdropMaterial.SetColor("_Color", tint);
        }
    }

    private void AnimateCinematicPortraitLights()
    {
        if (_titleFillLight == null || _titleRimLight == null)
            return;

        // note: Move the key and rim intensity by only a few percent to suggest breathing celestial light instead of a visible flicker.
        float phase = Time.unscaledTime * 0.24f;
        _titleFillLight.intensity = _titleFillBaseIntensity *
            (1f + Mathf.Sin(phase) * 0.035f);
        _titleRimLight.intensity = _titleRimBaseIntensity *
            (1f + Mathf.Cos(phase * 0.77f) * 0.045f);
        if (_titleSceneFillLight != null)
            _titleSceneFillLight.intensity = _titleSceneFillBaseIntensity *
                (1f + Mathf.Sin(phase * 0.52f + 0.8f) * 0.025f);
    }

    private void ResolveGoddessRoot()
    {
        if (goddessRoot != null)
            return;

        GameObject[] roots = gameObject.scene.GetRootGameObjects();
        for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
        {
            Transform[] transforms =
                roots[rootIndex].GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if (candidate == null || candidate.name.IndexOf(
                        "AngelStatue",
                        System.StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                // note: Keep the complete authored statue root so wings, garments, ornaments, and every material submesh turn together.
                goddessRoot = candidate;
                return;
            }
        }
    }

    private void PlayOneShot(AudioClip clip, float volume)
    {
        if (uiAudioSource == null || clip == null)
            return;

        // note: One shared non-spatial source keeps title feedback deterministic and avoids accumulating transient AudioSources.
        uiAudioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
