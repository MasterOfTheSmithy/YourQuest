// Assets/Assets/Scripts/Tutorial/Editor/YourQuestTestSceneMenu.cs
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YourQuestTestSceneMenu
{
    private const string SceneFolder = "Assets/Assets/Scenes";
    private const string ScenePath = "Assets/Assets/Scenes/YourQuest_TestScene.unity";

    public static void BuildTestScene()
    {
        // note: Legacy test scene builder is hidden from menus but kept callable for old editor workflows.
        Directory.CreateDirectory(SceneFolder);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SceneManager.SetActiveScene(scene);

        var root = new GameObject("YourQuest_TestSceneRoot");
        root.AddComponent<YourQuestTestSceneRoot>();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var runtimeRoot = Object.FindFirstObjectByType<YourQuestTestSceneRoot>();
        if (runtimeRoot != null)
            runtimeRoot.BuildIfNeeded();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        AssetDatabase.Refresh();
        Debug.Log("[YourQuest] Built test scene at " + ScenePath);
    }
}
#endif

