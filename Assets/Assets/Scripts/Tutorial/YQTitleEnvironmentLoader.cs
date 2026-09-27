using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class YQTitleEnvironmentLoader : MonoBehaviour
{
    private const string TitleEnvironmentScene = "YourQuest_TitleEnvironment";

    private static YQTitleEnvironmentLoader s_instance;
    private bool _requestedVisible;
    private bool _thresholdMode;
    private bool _generationHold;
    private bool _worldGenerationCameraReleased;
    private bool _loadFailureLogged;
    private bool _transitionInProgress;

    public static bool IsWorldGenerationStageVisible =>
        SceneManager.GetSceneByName(TitleEnvironmentScene).isLoaded;

    // note: An in-flight additive load can become visible later; scene absence alone is not proof of released camera ownership.
    public static bool IsWorldGenerationStageReleased =>
        !IsWorldGenerationStageVisible && (s_instance == null ||
            (!s_instance._requestedVisible && !s_instance._transitionInProgress));

    public static bool IsWorldGenerationCameraReleased =>
        s_instance != null && s_instance._worldGenerationCameraReleased;

    public static void Show()
    {
        YQTitleEnvironmentLoader instance = EnsureInstance();
        instance._generationHold = false;
        instance._worldGenerationCameraReleased = false;
        instance._thresholdMode = false;
        instance.SetRequestedVisible(true);
    }

    public static void HoldForOriginQuestionnaire()
    {
        YQTitleEnvironmentLoader instance = EnsureInstance();
        instance._generationHold = false;
        instance._worldGenerationCameraReleased = false;
        instance._thresholdMode = true;
        instance.SetRequestedVisible(true);
        instance.ApplyThresholdModeIfAvailable();
    }

    public static void HoldForWorldGeneration()
    {
        YQTitleEnvironmentLoader instance = EnsureInstance();
        instance._generationHold = true;
        instance._thresholdMode = true;
        instance.SetRequestedVisible(true);
        // note: Origin generation can precede creation of the player; retain the existing portrait/listener until there is a real gameplay target.
        ReleaseWorldGeneration();
    }

    public static void ReleaseWorldGeneration()
    {
        // note: Instantiate the persistent handoff owner before releasing generation so a late title-scene load cannot suppress the gameplay camera.
        YQTitleEnvironmentLoader instance = EnsureInstance();

        YQInvestorPlayerMotor motor = YQInvestorPlayerMotor.ActiveMotor;
        Camera gameplayCamera = motor != null && motor.IsAuthoritative
            ? motor.playerCamera
            : Camera.main;
        if (gameplayCamera == null || !gameplayCamera.gameObject.activeInHierarchy ||
            gameplayCamera.gameObject.scene.name == TitleEnvironmentScene)
        {
            // note: A release request before player creation must not unload the only camera and audio listener. The normal player binding completes this handoff.
            instance._generationHold = true;
            return;
        }

        // note: Generation must never wait for additive-scene unload before returning the live gameplay camera.
        gameplayCamera.enabled = true;
        YQTitleEnvironmentScene environment = FindLoadedEnvironment();
        if (environment != null)
            environment.ReleaseGameplayPresentation();

        // note: Keep late-created gameplay cameras from being recaptured by the title scene after generation has released presentation ownership.
        instance._worldGenerationCameraReleased = true;

        // note: Releasing the hold only schedules title-stage cleanup; camera ownership was returned immediately above.
        instance._generationHold = false;
        instance._thresholdMode = false;
        instance.SetRequestedVisible(false);
    }

    public static void Hide()
    {
        if (s_instance != null)
        {
            if (s_instance._generationHold)
                return;

            s_instance._thresholdMode = false;
            s_instance.SetRequestedVisible(false);
        }
    }

    public static void PlayUiHover()
    {
        YQTitleEnvironmentScene environment = FindLoadedEnvironment();
        if (environment != null)
            environment.PlayUiHover();
    }

    public static void PlayUiConfirm()
    {
        YQTitleEnvironmentScene environment = FindLoadedEnvironment();
        if (environment != null)
            environment.PlayUiConfirm();
    }

    public static void SuppressGameplayPresentationUntilRelease(
        Camera gameplayCamera)
    {
        if ((s_instance != null && s_instance._worldGenerationCameraReleased) ||
            YQGeneratedWorldRuntimeBuilder.IsInitialGenerationGameplayLocked)
        {
            // note: Initial generation never owns the gameplay camera; late-created cameras stay enabled even if the title stage is still unloading.
            if (gameplayCamera != null)
            {
                gameplayCamera.enabled = true;
                ReleaseWorldGeneration();
            }
            return;
        }

        YQTitleEnvironmentScene environment = FindLoadedEnvironment();
        if (environment != null)
        {
            // note: Late-created gameplay cameras are handed to the already-loaded title stage without introducing a second presentation manager.
            environment.SuppressGameplayPresentationUntilRelease(
                gameplayCamera);
        }
    }

    private static YQTitleEnvironmentLoader EnsureInstance()
    {
        if (s_instance != null)
            return s_instance;

        // note: The lightweight loader belongs to the persistent startup flow; the authored title environment remains an unloadable additive scene.
        GameObject host = new GameObject("__YQ_TitleEnvironmentLoader");
        DontDestroyOnLoad(host);
        s_instance = host.AddComponent<YQTitleEnvironmentLoader>();
        return s_instance;
    }

    private void SetRequestedVisible(bool value)
    {
        _requestedVisible = value;
        if (!_transitionInProgress)
        {
            // note: Claim ownership before StartCoroutine; a routine with no yields may finish before that call returns.
            _transitionInProgress = true;
            StartCoroutine(ApplyRequestedState());
        }
    }

    private IEnumerator ApplyRequestedState()
    {
        // note: Scene transitions are serialized so repeated menu state changes cannot load duplicate title stages or race an unload.
        try
        {
            while (true)
            {
                Scene loaded = SceneManager.GetSceneByName(TitleEnvironmentScene);
                if (_requestedVisible && !loaded.isLoaded)
                {
                    if (!Application.CanStreamedLevelBeLoaded(TitleEnvironmentScene))
                    {
                        if (!_loadFailureLogged)
                        {
                            Debug.LogWarning(
                                "[YQTitleEnvironmentLoader] The 3D title environment is not available in build settings; the menu remains fully functional without it.");
                            _loadFailureLogged = true;
                        }

                        break;
                    }

                    AsyncOperation load = SceneManager.LoadSceneAsync(
                        TitleEnvironmentScene,
                        LoadSceneMode.Additive);
                    if (load == null)
                        break;

                    while (!load.isDone)
                        yield return null;

                    // note: A player can confirm a save while the backdrop is loading; immediately honor that decision instead of flashing the scene.
                    if (!_requestedVisible)
                    {
                        // note: If generation released the camera while this additive load was in flight, Awake may have suppressed gameplay presentation; undo that late suppression before unloading.
                        YQTitleEnvironmentScene lateEnvironment = FindLoadedEnvironment();
                        if (lateEnvironment != null && _worldGenerationCameraReleased)
                            lateEnvironment.ReleaseGameplayPresentation();
                        continue;
                    }

                    // note: A title scene that finishes loading after generation began must not reclaim presentation for even one rendered frame.
                    if (_worldGenerationCameraReleased)
                    {
                        YQTitleEnvironmentScene lateEnvironment = FindLoadedEnvironment();
                        if (lateEnvironment != null)
                            lateEnvironment.ReleaseGameplayPresentation();
                    }
                    else
                    {
                        ApplyThresholdModeIfAvailable();
                    }
                    // note: Re-evaluate requests after activation callbacks without recursively starting another coroutine.
                    continue;
                }
                else if (_requestedVisible && loaded.isLoaded)
                {
                    YQTitleEnvironmentScene environment = FindLoadedEnvironment();
                    if (environment != null)
                        environment.CancelExitTransition();
                    // note: Generation may leave the additive stage loaded briefly for cleanup, but it must never reacquire camera ownership on a later transition tick.
                    if (_worldGenerationCameraReleased)
                    {
                        if (environment != null)
                            environment.ReleaseGameplayPresentation();
                    }
                    else
                    {
                        // note: Apply the latest camera mode after either a completed load or a cancelled unload, including requests made while the scene was unavailable.
                        ApplyThresholdModeIfAvailable();
                    }
                }
                else if (!_requestedVisible && loaded.isLoaded)
                {
                    YQTitleEnvironmentScene environment = FindLoadedEnvironment();
                    if (environment != null)
                    {
                        environment.BeginExitTransition();
                        while (!_requestedVisible &&
                               environment != null &&
                               !environment.ExitTransitionComplete)
                        {
                            yield return null;
                        }

                        if (_requestedVisible)
                        {
                            environment.CancelExitTransition();
                            continue;
                        }
                    }

                    AsyncOperation unload = SceneManager.UnloadSceneAsync(loaded);
                    if (unload == null)
                    {
                        Debug.LogError("[YQTitleEnvironmentLoader] Title-stage unload could not start; camera handoff remains blocked until retry.");
                        break;
                    }
                    while (!unload.isDone)
                        yield return null;

                    continue;
                }

                break;
            }
        }
        finally
        {
            // note: Clear ownership on normal completion, unavailable assets, exceptions and disposal; a later explicit request may retry.
            _transitionInProgress = false;
        }
    }

    private void ApplyThresholdModeIfAvailable()
    {
        Scene loaded = SceneManager.GetSceneByName(TitleEnvironmentScene);
        if (!loaded.isLoaded)
            return;

        // note: The threshold camera is resolved only inside the dedicated additive scene, never from the gameplay camera hierarchy.
        YQTitleEnvironmentScene environment = FindLoadedEnvironment();
        if (environment == null)
            return;

        if (!_thresholdMode)
        {
            environment.ShowTitleComposition();
        }
        else if (_generationHold)
        {
            // note: World generation reuses the baked Goddess stage but gives it a distinct contemplative camera motion instead of a static questionnaire portrait.
            environment.BeginGenerationIdle();
        }
        else
        {
            // note: The questionnaire owns a fresh cinematic handoff each time; returning to the title stage must not reuse a completed portrait pose.
            environment.BeginQuestionnaireTransition();
        }
    }

    private static YQTitleEnvironmentScene FindLoadedEnvironment()
    {
        Scene loaded = SceneManager.GetSceneByName(TitleEnvironmentScene);
        if (!loaded.isLoaded)
            return null;

        GameObject[] roots = loaded.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            YQTitleEnvironmentScene environment =
                roots[index].GetComponentInChildren<YQTitleEnvironmentScene>(true);
            if (environment != null)
                return environment;
        }

        return null;
    }
}
