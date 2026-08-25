using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQTitleEnvironmentScene : MonoBehaviour
{
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
    private float driftDistance = 0.45f;

    [SerializeField]
    private float driftSpeed = 0.075f;

    [SerializeField]
    private int sceneRecipeVersion;

    private Vector3 _cameraOrigin;
    private Vector3 _lookOrigin;
    private Vector3 _thresholdStartPosition;
    private Vector3 _thresholdStartLook;
    private Vector3 _thresholdDestinationPosition;
    private Vector3 _thresholdDestinationLook;
    private float _thresholdTransitionStartedAt;
    private float _goddessKeyBaseIntensity;
    private float _nextUiSoundTime;
    private float _exitTransitionStartedAt;
    private bool _exitTransitionActive;
    private AudioSource[] _sceneAudioSources = System.Array.Empty<AudioSource>();
    private float[] _sceneAudioBaseVolumes = System.Array.Empty<float>();
    private const float ThresholdTransitionSeconds = 2.4f;
    private const float ExitTransitionSeconds = 0.46f;
    private bool _thresholdTransitionActive;
    private bool _thresholdPoseActive;
    private bool _generationIdleRequested;
    private bool _generationIdleActive;
    private readonly List<Camera> _suppressedGameplayCameras =
        new List<Camera>();
    private readonly List<AudioListener> _suppressedAudioListeners =
        new List<AudioListener>();

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
        if (titleCamera != null)
        {
            _cameraOrigin = titleCamera.transform.position;
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
            if (camera == null || camera == titleCamera || !camera.enabled)
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
            if (listener == null || listener == titleListener || !listener.enabled)
                continue;

            // note: The additive title stage owns one listener while visible, preventing doubled ambience from dormant gameplay cameras.
            listener.enabled = false;
            _suppressedAudioListeners.Add(listener);
        }

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

    public void BeginGoddessThresholdTransition()
    {
        if (titleCamera == null || cameraTarget == null ||
            _thresholdTransitionActive || _thresholdPoseActive)
        {
            return;
        }

        // note: The questionnaire transition resolves the authored statue bounds so the destination is a true face-and-upper-body composition at any asset scale.
        _thresholdStartPosition = titleCamera.transform.position;
        _thresholdStartLook = cameraTarget.position;
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

        // note: Eighty percent of the live statue height targets the face and upper chest; serialized title anchors supply direction, never a stale center-of-statue aim point.
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

        // note: Legacy title recipes fall back to the statue's authored forward plane; current recipes always serialize exact portrait anchors above.
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
        for (int index = 0; index < _suppressedGameplayCameras.Count; index++)
        {
            Camera camera = _suppressedGameplayCameras[index];
            if (camera != null)
                camera.enabled = true;
        }

        _suppressedGameplayCameras.Clear();

        for (int index = 0; index < _suppressedAudioListeners.Count; index++)
        {
            AudioListener listener = _suppressedAudioListeners[index];
            if (listener != null)
                listener.enabled = true;
        }

        _suppressedAudioListeners.Clear();
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
            Vector3 look = Vector3.Lerp(
                _thresholdStartLook,
                _thresholdDestinationLook,
                eased);
            titleCamera.transform.position = Vector3.Lerp(
                _thresholdStartPosition,
                _thresholdDestinationPosition,
                eased);
            titleCamera.transform.rotation = Quaternion.LookRotation(
                look - titleCamera.transform.position,
                Vector3.up);
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
