// Assets/Assets/Scripts/Tutorial/Editor/YourQuestInvestorSceneBuilder.cs
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

public static class YourQuestInvestorSceneBuilder
{
    private const string SceneFolder = "Assets/Assets/Scenes";
    private const string ScenePath = "Assets/Assets/Scenes/YourQuest_InvestorPrototype.unity";
    private const string ResourceFolder = "Assets/Assets/Resources";
    private const string LibraryPath = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset";

    public static void BuildInvestorPrototypeScene()
    {
        // note: Legacy investor scene build remains callable, but is hidden from the main YourQuest tools menu.
        EnsureFolders();
        GeneratedRpgContentLibrary library = EnsureLibraryAsset();

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "YourQuest_InvestorPrototype";
        SceneManager.SetActiveScene(scene);

        ConfigureSceneSettings();
        BuildSystemRoots(library);
        BuildWorld();
        BuildPlayer();
        BuildUiRoots();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorUtility.DisplayDialog("YourQuest", "Investor prototype scene built.", "OK");
    }

    public static void OpenInvestorPrototypeScene()
    {
        // note: Legacy investor scene open remains callable for scripts that still reference this builder.
        EnsureFolders();
        if (!File.Exists(ScenePath))
            BuildInvestorPrototypeScene();
        else
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    public static void RebuildInvestorPrototypeScene()
    {
        // note: Legacy investor scene rebuild remains callable, but is no longer exposed as a menu tool.
        if (File.Exists(ScenePath))
        {
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.Refresh();
        }
        BuildInvestorPrototypeScene();
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Assets"))
            AssetDatabase.CreateFolder("Assets", "Assets");
        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets/Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(ResourceFolder))
            AssetDatabase.CreateFolder("Assets/Assets", "Resources");
    }

    private static GeneratedRpgContentLibrary EnsureLibraryAsset()
    {
        GeneratedRpgContentLibrary existing = AssetDatabase.LoadAssetAtPath<GeneratedRpgContentLibrary>(LibraryPath);
        if (existing != null)
            return existing;

        GeneratedRpgContentLibrary created = ScriptableObject.CreateInstance<GeneratedRpgContentLibrary>();
        AssetDatabase.CreateAsset(created, LibraryPath);
        EditorUtility.SetDirty(created);
        AssetDatabase.SaveAssets();
        return created;
    }

    private static void ConfigureSceneSettings()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.56f, 0.58f, 0.62f, 1f);
    }

    private static void BuildSystemRoots(GeneratedRpgContentLibrary library)
    {
        CreateSingletonRoot<PlayerStateManager>("01__System_PlayerStateManager");
        CreateSingletonRoot<WorldStateManager>("01__System_WorldStateManager");
        CreateSingletonRoot<ActionRegistry>("01__System_ActionRegistry");
        CreateSingletonRoot<EventAccumulator>("01__System_EventAccumulator");
        CreateSingletonRoot<PlayerContext>("01__System_PlayerContext");
        CreateSingletonRoot<PlayerProfile>("01__System_PlayerProfile");
        CreateSingletonRoot<SituationSnapshotBuilder>("01__System_SituationSnapshotBuilder");
        CreateSingletonRoot<DialogueThinkService>("01__System_DialogueThinkService");
        CreateSingletonRoot<WorldDeltaApplier>("01__System_WorldDeltaApplier");
        CreateSingletonRoot<ProgressionDecisionApplier>("01__System_ProgressionDecisionApplier");
        CreateSingletonRoot<LLMThinkCycle>("01__System_LLMThinkCycle");
        CreateSingletonRoot<ProgressionThinkCycle>("01__System_ProgressionThinkCycle");
        CreateSingletonRoot<PlayerBehaviorRollup>("01__System_PlayerBehaviorRollup");

        GeneratedRpgContentService content = CreateSingletonRoot<GeneratedRpgContentService>("01__System_GeneratedRpgContentService");
        content.library = library;

        LLMClient llm = CreateSingletonRoot<LLMClient>("01__System_LLMClient");
        llm.model = string.IsNullOrWhiteSpace(llm.model) ? "mistral:7b-instruct-q4_K_M" : llm.model;
        llm.apiUrl = string.IsNullOrWhiteSpace(llm.apiUrl) ? "http://127.0.0.1:11434" : llm.apiUrl;
        llm.logRequestJson = true;
        llm.logRawModelText = true;

        EventSystem existing = Object.FindFirstObjectByType<EventSystem>();
        if (existing == null)
        {
            GameObject go = new GameObject("04__UI_EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static void BuildUiRoots()
    {
        CreateSingletonRoot<YQInvestorDirector>("02__Director_Investor");
        CreateSingletonRoot<YourQuestTutorialHud>("04__UI_Hud");
        CreateSingletonRoot<YourQuestTutorialMenuUI>("04__UI_Menu");
        CreateSingletonRoot<YQInvestorDialogueUI>("04__UI_Dialogue");
    }

    private static void BuildWorld()
    {
        GameObject root = new GameObject("03__World");
        Transform environment = CreateChild(root.transform, "Environment").transform;
        Transform regions = CreateChild(root.transform, "Regions").transform;
        Transform npcs = CreateChild(root.transform, "NPCs").transform;
        Transform shrines = CreateChild(root.transform, "Shrines").transform;
        Transform enemies = CreateChild(root.transform, "Enemies").transform;
        Transform boundaries = CreateChild(root.transform, "Boundaries").transform;
        Transform lighting = CreateChild(root.transform, "Lighting").transform;

        GameObject lightGo = new GameObject("Directional Light");
        lightGo.transform.SetParent(lighting, false);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        lightGo.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(environment, false);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(10f, 1f, 10f);
        SetRendererColor(ground, new Color(0.22f, 0.24f, 0.26f, 1f));

        CreateBoundary(boundaries, "NorthWall", new Vector3(0f, 2.5f, 95f), new Vector3(190f, 5f, 2f));
        CreateBoundary(boundaries, "SouthWall", new Vector3(0f, 2.5f, -95f), new Vector3(190f, 5f, 2f));
        CreateBoundary(boundaries, "EastWall", new Vector3(95f, 2.5f, 0f), new Vector3(2f, 5f, 190f));
        CreateBoundary(boundaries, "WestWall", new Vector3(-95f, 2.5f, 0f), new Vector3(2f, 5f, 190f));

        CreateLandmark(environment, "HubMarker", PrimitiveType.Cylinder, new Vector3(0f, 0.75f, 0f), new Vector3(3f, 0.75f, 3f), new Color(0.35f, 0.55f, 0.95f, 1f));
        CreateLandmark(environment, "ArchiveMarker", PrimitiveType.Cube, new Vector3(-26f, 1f, 14f), new Vector3(4f, 2f, 4f), new Color(0.45f, 0.8f, 1f, 1f));
        CreateLandmark(environment, "RedYardMarker", PrimitiveType.Cube, new Vector3(26f, 1f, 14f), new Vector3(4f, 2f, 4f), new Color(1f, 0.55f, 0.35f, 1f));
        CreateLandmark(environment, "VaultMarker", PrimitiveType.Cube, new Vector3(0f, 1f, -28f), new Vector3(4f, 2f, 4f), new Color(0.55f, 0.95f, 0.55f, 1f));

        CreateRegion(regions, "Region_TestHub", new Vector3(0f, 1.5f, 0f), new Vector3(26f, 3f, 24f), "region_test_hub", "Test Hub", new[] { "hub", "safe_zone", "tutorial" });
        CreateRegion(regions, "Region_Library", new Vector3(-28f, 1.5f, 12f), new Vector3(24f, 3f, 20f), "region_library", "Blue Library", new[] { "archive", "lore", "mentor" });
        CreateRegion(regions, "Region_RedYard", new Vector3(28f, 1.5f, 12f), new Vector3(24f, 3f, 20f), "region_red_yard", "Red Yard", new[] { "combat", "trial", "hostile" });
        CreateRegion(regions, "Region_VaultApproach", new Vector3(0f, 1.5f, -30f), new Vector3(28f, 3f, 22f), "region_vault_approach", "Vault Approach", new[] { "combat", "vault", "hostile" });

        CreateNpc(npcs, "Archivist Vey", new Vector3(-4f, 0f, 5f), "npc_archivist_01", "Archivist Vey", "the_archives", new[] { "friendly", "scholar", "questgiver" }, new Color(0.82f, 0.82f, 0.55f, 1f));
        CreateNpc(npcs, "Warden Thorne", new Vector3(8f, 0f, -3f), "npc_warden_01", "Warden Thorne", "ember_court", new[] { "rude", "guard", "judge" }, new Color(0.82f, 0.68f, 0.42f, 1f));

        CreateShrine(shrines, "Shrine of Recall", new Vector3(-12f, 0.75f, -2f), "shrine_of_recall", "Shrine of Recall", "the_archives", new Color(0.50f, 0.85f, 1f, 1f));
        CreateShrine(shrines, "Shrine of Ash", new Vector3(14f, 0.75f, 18f), "shrine_of_ash", "Shrine of Ash", "ember_court", new Color(1f, 0.68f, 0.40f, 1f));

        CreateEnemy(enemies, "Enemy_RedYard_01", new Vector3(22f, 0f, 10f), "region_red_yard_enemy_01", "Ember Echo Wisp", "region_red_yard", "ember_court", new Color(1f, 0.38f, 0.10f, 1f));
        CreateEnemy(enemies, "Enemy_RedYard_02", new Vector3(34f, 0f, 16f), "region_red_yard_enemy_02", "Ember Echo Wisp", "region_red_yard", "ember_court", new Color(1f, 0.38f, 0.10f, 1f));
        CreateEnemy(enemies, "Enemy_Vault_01", new Vector3(-6f, 0f, -26f), "region_vault_enemy_01", "Vault Echo Wisp", "region_vault_approach", "wild_hollows", new Color(0.66f, 0.34f, 1f, 1f));
        CreateEnemy(enemies, "Enemy_Vault_02", new Vector3(8f, 0f, -34f), "region_vault_enemy_02", "Vault Echo Wisp", "region_vault_approach", "wild_hollows", new Color(0.66f, 0.34f, 1f, 1f));
    }

    private static void BuildPlayer()
    {
        GameObject player = new GameObject("05__Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1.25f, -8f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.35f;

        ActionRecorder recorder = player.AddComponent<ActionRecorder>();
        YQInvestorVitals vitals = player.AddComponent<YQInvestorVitals>();
        YQInvestorCombat combat = player.AddComponent<YQInvestorCombat>();
        YQInvestorPlayerMotor motor = player.AddComponent<YQInvestorPlayerMotor>();
        player.AddComponent<PlayerLocationReporter>();
        player.AddComponent<YQPlayerEquipmentVisual>();

        GameObject pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);

        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetParent(player.transform, false);
        cameraGo.transform.position = pivot.transform.position;
        Camera cam = cameraGo.AddComponent<Camera>();
        cameraGo.AddComponent<AudioListener>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        SetRendererColor(visual, new Color(0.35f, 0.55f, 0.95f, 1f));

        motor.cameraPivot = pivot.transform;
        motor.playerCamera = cam;
        motor.actionRecorder = recorder;
        motor.vitals = vitals;
        motor.firstPerson = true;
    }

    private static T CreateSingletonRoot<T>(string name) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null)
        {
            existing.gameObject.name = name;
            return existing;
        }
        GameObject go = new GameObject(name);
        return go.AddComponent<T>();
    }

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void CreateBoundary(Transform parent, string name, Vector3 position, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        SetRendererColor(wall, new Color(0.12f, 0.12f, 0.14f, 1f));
    }

    private static void CreateLandmark(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        go.transform.localScale = scale;
        SetRendererColor(go, color);
    }

    private static void CreateRegion(Transform parent, string objectName, Vector3 position, Vector3 size, string regionId, string regionName, string[] tags)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;
        RegionVolume region = go.AddComponent<RegionVolume>();
        region.regionId = regionId;
        region.regionName = regionName;
        region.playerTag = "Player";
        region.clearOnExit = false;
        region.tags = new System.Collections.Generic.List<string>(tags);
    }

    private static void CreateNpc(Transform parent, string objectName, Vector3 position, string entityId, string displayName, string factionId, string[] tags, Color color)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = 2f;
        collider.radius = 0.45f;
        collider.center = new Vector3(0f, 1f, 0f);

        YQEchoFlameWispVisual visual = root.AddComponent<YQEchoFlameWispVisual>();
        visual.ApplyPalette(color, factionId == "ember_court" ? new Color(1f, 0.72f, 0.34f, 1f) : new Color(0.48f, 0.82f, 1f, 1f));

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = entityId;
        info.displayName = displayName;
        info.factionId = factionId;
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = tags;

        NpcDialogueAgent agent = root.AddComponent<NpcDialogueAgent>();
        agent.npcId = entityId;
        agent.npcName = displayName;
        agent.tagsOverride = new System.Collections.Generic.List<string>(tags);
    }

    private static void CreateShrine(Transform parent, string objectName, Vector3 position, string entityId, string displayName, string factionId, Color color)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = objectName;
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        root.transform.localScale = new Vector3(1.15f, 0.75f, 1.15f);
        SetRendererColor(root, color);

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = entityId;
        info.displayName = displayName;
        info.factionId = factionId;
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = new[] { "shrine", "interactable", "healing" };
        root.AddComponent<YQInvestorShrine>();
    }

    private static void CreateEnemy(Transform parent, string objectName, Vector3 position, string entityId, string displayName, string semanticRegionId, string factionId, Color color)
    {
        GameObject root = new GameObject(objectName);
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 1f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.height = 1.55f;
        collider.radius = 0.48f;
        collider.center = new Vector3(0f, 0.92f, 0f);

        YQEchoFlameWispVisual visual = root.AddComponent<YQEchoFlameWispVisual>();
        visual.ApplyPalette(color, factionId == "ember_court" ? new Color(1f, 0.72f, 0.34f, 1f) : new Color(0.48f, 0.82f, 1f, 1f));

        EntityInfo info = root.AddComponent<EntityInfo>();
        info.entityId = entityId;
        info.displayName = displayName;
        info.level = 2;
        info.factionId = factionId;
        info.hostility = Hostility.Hostile;
        info.isNotable = false;
        info.tags = new[] { "enemy", "tutorial", semanticRegionId };

        YQInvestorEnemy enemy = root.AddComponent<YQInvestorEnemy>();
        enemy.semanticRegionId = semanticRegionId;
        enemy.factionId = factionId;
        enemy.displayName = displayName;
    }

    private static void SetRendererColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
            renderer.sharedMaterial.color = color;
    }
}
#endif
