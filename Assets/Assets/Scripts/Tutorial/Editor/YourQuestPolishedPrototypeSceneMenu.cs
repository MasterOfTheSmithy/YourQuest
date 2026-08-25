// Assets/Assets/Scripts/Tutorial/Editor/YourQuestPolishedPrototypeSceneMenu.cs
#if UNITY_EDITOR
using UnityEditor;

public static class YourQuestPolishedPrototypeSceneMenu
{
    public static void BuildPolishedPrototypeScene()
    {
        // note: Legacy polished prototype shortcut is hidden from menus but kept for any direct editor calls.
        YourQuestInvestorSceneBuilder.RebuildInvestorPrototypeScene();
    }

    public static void OpenPolishedPrototypeScene()
    {
        // note: Legacy polished prototype shortcut is hidden from menus but still delegates to the investor scene.
        YourQuestInvestorSceneBuilder.OpenInvestorPrototypeScene();
    }
}
#endif
