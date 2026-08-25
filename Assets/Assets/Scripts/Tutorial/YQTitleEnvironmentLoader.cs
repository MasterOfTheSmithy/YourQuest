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
    private bool _loadFailureLogged;
    private Coroutine _transition;

    public static void Show()
    {
        YQTitleEnvironmentLoader instance = EnsureInstance();
        instance._generationHold = false;
        instance._thresholdMode = false;
        instance.SetRequestedVisible(true);
    }

    public static void HoldForOriginQuestionnaire()
    {
        YQTitleEnvironmentLoader instance = EnsureInstance();
        instance._generationHold = false;
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
        instance.ApplyThresholdModeIfAvailable();
    }

    public static void ReleaseWorldGeneration()
    {
        if (s_instance == null)
            return;

        // note: Generation owns the additive Goddess stage until its final handoff; releasing the hold permits the normal cinematic unload.
        s_instance._generationHold = false;
        s_instance._thresholdMode = false;
        s_instance.SetRequestedVisible(false);
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
        if (_transition == null)
            _transition = StartCoroutine(ApplyRequestedState());
    }

    private IEnumerator ApplyRequestedState()
    {
        // note: Scene transitions are serialized so repeated menu state changes cannot load duplicate title stages or race an unload.
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
                    continue;

                ApplyThresholdModeIfAvailable();
            }
            else if (_requestedVisible && loaded.isLoaded)
            {
                YQTitleEnvironmentScene environment = FindLoadedEnvironment();
                if (environment != null)
                    environment.CancelExitTransition();
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
                if (unload != null)
                {
                    while (!unload.isDone)
                        yield return null;
                }

                continue;
            }

            break;
        }

        _transition = null;

        // note: A state request can arrive on the frame the previous transition completes; replay it once rather than dropping the request.
        Scene finalScene = SceneManager.GetSceneByName(TitleEnvironmentScene);
        if (_requestedVisible != finalScene.isLoaded)
            _transition = StartCoroutine(ApplyRequestedState());
    }

    private void ApplyThresholdModeIfAvailable()
    {
        if (!_thresholdMode)
            return;

        Scene loaded = SceneManager.GetSceneByName(TitleEnvironmentScene);
        if (!loaded.isLoaded)
            return;

        // note: The threshold camera is resolved only inside the dedicated additive scene, never from the gameplay camera hierarchy.
        YQTitleEnvironmentScene environment = FindLoadedEnvironment();
        if (environment == null)
            return;

        if (_generationHold)
        {
            // note: World generation reuses the baked Goddess stage but gives it a distinct contemplative camera motion instead of a static questionnaire portrait.
            environment.BeginGenerationIdle();
        }
        else
        {
            environment.BeginGoddessThresholdTransition();
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
