// Assets/Assets/Scripts/Tutorial/Editor/YourQuestWorldDialogueValidationSceneBuilder.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class YourQuestWorldDialogueValidationSceneBuilder
{
    private const string SceneFolder = "Assets/Assets/Scenes";
    private const string ScenePath = "Assets/Assets/Scenes/YourQuest_WorldDialogueValidation.unity";

    private static bool waitingForSavedWorkspace;

    [InitializeOnLoadMethod]
    private static void EnsureValidationSceneExists()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            return;

        // note: First installation creates the requested scene additively after compilation without replacing the developer's open scene.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= BuildAfterPlayModeExit;
            EditorApplication.playModeStateChanged += BuildAfterPlayModeExit;
            // note: Missing optional authoring output does not own the active Play session; resume only after Play exits naturally.
            return;
        }

        QueueMissingSceneBuild();
    }

    private static void BuildAfterPlayModeExit(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= BuildAfterPlayModeExit;
        QueueMissingSceneBuild();
    }

    [MenuItem("YourQuest/Tests/Rebuild World + Dialogue Validation Scene")]
    public static void BuildAndOpen()
    {
        BuildScene();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    public static void BuildHeadless()
    {
        BuildScene();
    }

    private static void BuildScene()
    {
        BuildSceneInternal(NewSceneMode.Single, false);
    }

    private static void QueueMissingSceneBuild()
    {
        // note: Scene save/open/close notifications may arrive together; retain one delayed attempt against the final workspace state.
        EditorApplication.delayCall -= BuildMissingSceneWithoutChangingWorkspace;
        EditorApplication.delayCall += BuildMissingSceneWithoutChangingWorkspace;
    }

    private static bool HasUnsavedWorkspace()
    {
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene scene = SceneManager.GetSceneAt(index);
            if (scene.isLoaded && (string.IsNullOrEmpty(scene.path) || scene.isDirty))
                return true;
        }
        // note: Unity's SaveScene also persists dirty assets. Automatic scene creation must wait rather than flush any unrelated pending persistent-asset edit.
        foreach (UnityEngine.Object asset in Resources.FindObjectsOfTypeAll<UnityEngine.Object>())
            if (HasPendingAssetSave(asset))
                return true;
        return false;
    }

    internal static bool HasPendingAssetSave(UnityEngine.Object asset)
    {
        // note: Persistence includes Unity's built-in scripts and imported shader products. Only authored asset files (including embedded packages) and importer metadata represent pending user saves.
        if (asset == null || !EditorUtility.IsPersistent(asset) || !EditorUtility.IsDirty(asset))
            return false;
        string path = AssetDatabase.GetAssetPath(asset);
        if (!path.StartsWith("Assets/", System.StringComparison.Ordinal) && !path.StartsWith("Packages/", System.StringComparison.Ordinal))
            return false;
        // note: IsNativeAsset covers Unity-serialized data and its subassets; native prefab graphs are classified separately by Unity. Importer metadata remains conservatively protected.
        return AssetDatabase.IsNativeAsset(asset) || asset is AssetImporter ||
            ((asset is GameObject || asset is Component) && PrefabUtility.IsPartOfPrefabAsset(asset) && !PrefabUtility.IsPartOfModelPrefab(asset));
    }

    internal static void RetryAfterAssetChanges()
    {
        // note: Asset save/import completion is an event-driven retry; the existing queue deduplicates simultaneous scene and asset notifications without polling.
        if (waitingForSavedWorkspace)
            QueueMissingSceneBuild();
    }

    private static void RetryAfterSceneSavedOrClosed(Scene scene) => QueueMissingSceneBuild();
    private static void RetryAfterSceneOpened(Scene scene, OpenSceneMode mode) => QueueMissingSceneBuild();

    private static void ClearWorkspaceRetry()
    {
        EditorSceneManager.sceneSaved -= RetryAfterSceneSavedOrClosed;
        EditorSceneManager.sceneClosed -= RetryAfterSceneSavedOrClosed;
        EditorSceneManager.sceneOpened -= RetryAfterSceneOpened;
        waitingForSavedWorkspace = false;
    }

    private static void BuildMissingSceneWithoutChangingWorkspace()
    {
        EditorApplication.delayCall -= BuildMissingSceneWithoutChangingWorkspace;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
        {
            ClearWorkspaceRetry();
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            // note: A queued callback can outlive its Edit Mode frame; retry after the transition without stopping the user's session here.
            EditorApplication.playModeStateChanged -= BuildAfterPlayModeExit;
            EditorApplication.playModeStateChanged += BuildAfterPlayModeExit;
            return;
        }
        if (HasUnsavedWorkspace())
        {
            // note: Additive scene creation rejects an untitled workspace. Automatic authoring waits for the user to save or close it, never saving or discarding their work.
            if (!waitingForSavedWorkspace)
            {
                waitingForSavedWorkspace = true;
                EditorSceneManager.sceneSaved += RetryAfterSceneSavedOrClosed;
                EditorSceneManager.sceneClosed += RetryAfterSceneSavedOrClosed;
                EditorSceneManager.sceneOpened += RetryAfterSceneOpened;
                Debug.LogWarning("[YourQuestWorldDialogueValidationSceneBuilder] Automatic scene build deferred until open scenes and persistent assets have no unsaved changes.");
            }
            return;
        }

        ClearWorkspaceRetry();
        BuildSceneInternal(NewSceneMode.Additive, true);
    }

    private static void BuildSceneInternal(NewSceneMode mode, bool closeAfterSave)
    {
        if (!Directory.Exists(SceneFolder))
            Directory.CreateDirectory(SceneFolder);

        Scene previousScene = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, mode);
        try
        {
            // note: Unity can return false for an already-active scene. Verify the real valid/loaded/active postcondition instead of treating that no-op as failure.
            if (scene.IsValid() && scene.isLoaded && SceneManager.GetActiveScene() != scene)
                SceneManager.SetActiveScene(scene);
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.GetActiveScene() != scene)
                throw new System.InvalidOperationException("New validation scene could not become active.");
            scene.name = "YourQuest_WorldDialogueValidation";

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.52f, 0.57f, 0.66f, 1f);

            BuildLightingAndGround();
            Transform settlementRoot = BuildCuratedSettlementCells();
            GameObject player = BuildPlayer();
            (EntityInfo info, NpcDialogueAgent agent) = BuildNpc();
            YQInvestorDialogueUI dialogueUi = BuildSystems(player.transform);

            YQWorldDialogueValidationHarness harness =
                new GameObject("00__WorldDialogueValidationHarness")
                    .AddComponent<YQWorldDialogueValidationHarness>();
            harness.validationNpc = info;
            harness.validationAgent = agent;
            harness.dialogueUi = dialogueUi;
            harness.curatedSettlementRoot = settlementRoot;

            EditorSceneManager.SaveScene(scene, ScenePath);
            // note: Automatic generation owns only this scene; do not flush unrelated dirty materials or ScriptableObjects from the developer's workspace.
            if (closeAfterSave)
                AssetDatabase.ImportAsset(ScenePath);
            else
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
        }
        finally
        {
            // note: This is only the scene created by this invocation; an authoring failure must not strand it or change the developer's active scene.
            if (closeAfterSave)
            {
                if (previousScene.IsValid() && previousScene.isLoaded)
                    SceneManager.SetActiveScene(previousScene);
                if (scene.IsValid() && scene.isLoaded)
                    EditorSceneManager.CloseScene(scene, true);
            }
        }
        Debug.Log("[YourQuestWorldDialogueValidationSceneBuilder] Built " + ScenePath);
    }

    private static void BuildLightingAndGround()
    {
        GameObject lightGo = new GameObject("Directional Light");
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.35f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(48f, -34f, 0f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "03__ValidationGround";
        ground.transform.position = new Vector3(0f, -0.5f, 8f);
        ground.transform.localScale = new Vector3(64f, 1f, 64f);

        // note: The street is a deliberate circulation spine so imported buildings are evaluated as lots, not as a random showcase pile.
        GameObject street = GameObject.CreatePrimitive(PrimitiveType.Cube);
        street.name = "Street_CirculationSpine";
        street.transform.position = new Vector3(0f, 0.03f, 9f);
        street.transform.localScale = new Vector3(6f, 0.08f, 48f);
    }

    private static Transform BuildCuratedSettlementCells()
    {
        Transform root = new GameObject("03__CuratedSettlementCells").transform;

        CreateCuratedCell(
            root,
            "Residential_HouseOnAHill",
            "Assets/HIVEMIND/HouseOnaHill/HDRP/Art/Prefabs/SM_House.prefab",
            new Vector3(-11f, 0f, 6f),
            90f);
        CreateCuratedCell(
            root,
            "Commerce_CyberpunkShop",
            "Assets/HIVEMIND/CyberpunkCity/HDRP(Default)/Art/Prefabs/SM_MERGED_BP_House_Shop_E10.prefab",
            new Vector3(11f, 0f, 8f),
            -90f);
        CreateCuratedCell(
            root,
            "Civic_VikingHall",
            "Assets/HIVEMIND/ModularVikingVillage/HDRP/Art/Prefabs/SM_HouseBuilding_001_a.prefab",
            new Vector3(-11f, 0f, 23f),
            90f);

        return root;
    }

    private static void CreateCuratedCell(
        Transform parent,
        string name,
        string assetPath,
        Vector3 position,
        float yaw)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        GameObject instance;

        if (prefab != null)
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
        }
        else
        {
            // note: A missing optional reference stays obvious in the test scene without replacing or modifying the source asset pack.
            instance = GameObject.CreatePrimitive(PrimitiveType.Cube);
            instance.transform.SetParent(parent, false);
            instance.transform.localScale = new Vector3(8f, 4f, 8f);
            Debug.LogWarning("[YourQuestWorldDialogueValidationSceneBuilder] Missing optional validation prefab: " + assetPath);
        }

        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        FitAndGround(instance, 15f, 12f);
        YQRuntimeUrpMaterialRepair.RepairHierarchy(instance);
    }

    private static void FitAndGround(GameObject root, float maxFootprint, float maxHeight)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float factor = Mathf.Min(1f,
            Mathf.Min(maxFootprint / Mathf.Max(0.01f, footprint), maxHeight / Mathf.Max(0.01f, bounds.size.y)));
        root.transform.localScale *= factor;

        // note: Recalculate after scaling, then place the lowest visible point on the shared street grade.
        renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        root.transform.position += Vector3.up * (0f - bounds.min.y);
    }

    private static GameObject BuildPlayer()
    {
        GameObject player = new GameObject("05__Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 0.2f, -3f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);

        ActionRecorder recorder = player.AddComponent<ActionRecorder>();
        YQInvestorVitals vitals = player.AddComponent<YQInvestorVitals>();
        YQInvestorPlayerMotor motor = player.AddComponent<YQInvestorPlayerMotor>();

        GameObject pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);

        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetParent(player.transform, false);
        cameraGo.transform.localPosition = pivot.transform.localPosition;
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.fieldOfView = 74f;
        cameraGo.AddComponent<AudioListener>();

        motor.cameraPivot = pivot.transform;
        motor.playerCamera = camera;
        motor.actionRecorder = recorder;
        motor.vitals = vitals;
        motor.firstPerson = true;
        return player;
    }

    private static (EntityInfo, NpcDialogueAgent) BuildNpc()
    {
        GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_TranscriptValidator";
        npc.transform.position = new Vector3(0f, 1f, 0f);

        EntityInfo info = npc.AddComponent<EntityInfo>();
        info.entityId = "npc_world_dialogue_validation";
        info.displayName = "Iona, Memory Auditor";
        info.level = 1;
        info.factionId = "validation_only";
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = new[] { "validation", "scholar", "friendly" };

        NpcDialogueAgent agent = npc.AddComponent<NpcDialogueAgent>();
        agent.npcId = info.entityId;
        agent.npcName = info.displayName;
        agent.personaSummary = "A concise in-world auditor who verifies memory without canned tutorial dialogue.";
        agent.tagsOverride = new List<string>(info.tags);
        return (info, agent);
    }

    private static YQInvestorDialogueUI BuildSystems(Transform player)
    {
        new GameObject("01__System_DialogueThinkService").AddComponent<DialogueThinkService>();
        LLMClient llm = new GameObject("01__System_LLMClient").AddComponent<LLMClient>();
        if (string.IsNullOrWhiteSpace(llm.apiUrl))
            llm.apiUrl = "http://127.0.0.1:11434";

        GameObject eventGo = new GameObject("04__UI_EventSystem");
        eventGo.AddComponent<EventSystem>();
        eventGo.AddComponent<InputSystemUIInputModule>();

        YQInvestorDialogueUI ui = new GameObject("04__UI_Dialogue").AddComponent<YQInvestorDialogueUI>();
        ui.playerRoot = player;
        ui.viewCamera = Camera.main;
        ui.talkRadius = 6f;
        ui.requireLineOfSight = false;
        return ui;
    }
}

// note: A user's explicit asset save can clear the automatic builder's deferral without saving a scene. This observer only requests the owner's existing delayed recheck.
internal sealed class YourQuestWorldDialogueValidationAssetChangeListener : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
    {
        if (importedAssets.Length != 0 || deletedAssets.Length != 0 || movedAssets.Length != 0)
            YourQuestWorldDialogueValidationSceneBuilder.RetryAfterAssetChanges();
    }
}
#endif
