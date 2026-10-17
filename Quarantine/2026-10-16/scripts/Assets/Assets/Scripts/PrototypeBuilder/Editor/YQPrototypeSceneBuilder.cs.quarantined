// Assets/Assets/Scripts/PrototypeBuilder/Editor/YQPrototypeSceneBuilder.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem.UI;


public static class YQPrototypeSceneBuilder
{
    private const string ScenePath = "Assets/Assets/Scenes/YQ_Prototype_Tutorial.unity";

    public static void BuildScene()
    {
        // note: Legacy prototype scene build is hidden from menus but kept callable for old automation.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "YQ_Prototype_Tutorial";

        EnsureEventSystem();
        BuildManagers();
        GameObject player = BuildPlayer();
        BuildEnvironment();
        BuildNpcGuide();
        BuildShrines();
        BuildEnemies();
        WireSystems(player);

        System.IO.Directory.CreateDirectory("Assets/Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("YourQuest", "Prototype tutorial scene built and saved. Open Assets/Assets/Scenes/YQ_Prototype_Tutorial.unity", "OK");
    }

    public static void OpenScene()
    {
        // note: Legacy prototype scene open is hidden from menus but still available to direct editor calls.
        if (!System.IO.File.Exists(ScenePath))
        {
            BuildScene();
            return;
        }

        EditorSceneManager.OpenScene(ScenePath);
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null) return;
        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
        es.AddComponent<InputSystemUIInputModule>();
    }

    private static void BuildManagers()
    {
        EnsureSingleton<PlayerStateManager>("PlayerStateManager");
        EnsureSingleton<WorldStateManager>("WorldStateManager");
        EnsureSingleton<ActionRegistry>("ActionRegistry");
        EnsureSingleton<EventAccumulator>("EventAccumulator");
        EnsureSingleton<PlayerContext>("PlayerContext");

        var llm = EnsureSingleton<LLMClient>("LLMClient");
        llm.model = string.IsNullOrWhiteSpace(llm.model) ? "mistral:7b-instruct-q4_K_M" : llm.model;

        var snapshot = EnsureSingleton<SituationSnapshotBuilder>("SituationSnapshotBuilder");
        var dialogue = EnsureSingleton<DialogueThinkService>("DialogueThinkService");
        dialogue.situationSnapshotBuilder = snapshot;

        var worldApplier = EnsureSingleton<WorldDeltaApplier>("WorldDeltaApplier");
        worldApplier.worldStateManager = Object.FindFirstObjectByType<WorldStateManager>();

        var progressionApplier = EnsureSingleton<ProgressionDecisionApplier>("ProgressionDecisionApplier");
        progressionApplier.snapshotBuilder = snapshot;
        progressionApplier.playerProfile = EnsureSingleton<PlayerProfile>("PlayerProfile");

        var worldThink = EnsureSingleton<LLMThinkCycle>("LLMThinkCycle");
        worldThink.worldDeltaApplier = worldApplier;
        worldThink.situationSnapshotBuilder = snapshot;
        worldThink.thinkEverySeconds = 18f;
        worldThink.minTotalSignificance = 3f;

        var progressionThink = EnsureSingleton<ProgressionThinkCycle>("ProgressionThinkCycle");
        progressionThink.applier = progressionApplier;
        progressionThink.balance = FindProgressionBalanceAsset();

        GameObject directorRoot = new GameObject("DirectorSystem");
        var promptBuilder = directorRoot.AddComponent<DirectorPromptBuilder>();
        var decisionApplier = directorRoot.AddComponent<DirectorDecisionApplier>();
        decisionApplier.worldDeltaApplier = worldApplier;
        decisionApplier.progressionDecisionApplier = progressionApplier;
        var directorThink = directorRoot.AddComponent<DirectorThinkCycle>();
        directorThink.llmClient = llm;
        directorThink.promptBuilder = promptBuilder;
        directorThink.decisionApplier = decisionApplier;
    }

    private static GameObject BuildPlayer()
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 2f, -4f);

        var rb = player.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.center = new Vector3(0f, 0.9f, 0f);
        capsule.radius = 0.38f;

        var visuals = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visuals.name = "Body";
        visuals.transform.SetParent(player.transform, false);
        visuals.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        Object.DestroyImmediate(visuals.GetComponent<Collider>());
        visuals.GetComponent<Renderer>().sharedMaterial.color = new Color(0.25f, 0.6f, 1f, 1f);

        var pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = player.transform.position + new Vector3(0f, 1.6f, -3.5f);
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();

        var recorder = player.AddComponent<ActionRecorder>();
        var controller = player.AddComponent<PlayerController>();
        controller.cameraPivot = pivot.transform;
        controller.playerCamera = cam;
        controller.actionRecorder = recorder;

        player.AddComponent<YQPrototypePlayerVitals>();
        player.AddComponent<YQPrototypePlayerCombat>();

        return player;
    }

    private static void BuildEnvironment()
    {
        CreateGround(new Vector3(0f, 0f, 0f), new Vector3(10f, 1f, 10f), new Color(0.18f, 0.19f, 0.2f));
        CreateBoundary(new Vector3(0f, 3f, 98f), new Vector3(196f, 6f, 2f));
        CreateBoundary(new Vector3(0f, 3f, -98f), new Vector3(196f, 6f, 2f));
        CreateBoundary(new Vector3(98f, 3f, 0f), new Vector3(2f, 6f, 196f));
        CreateBoundary(new Vector3(-98f, 3f, 0f), new Vector3(2f, 6f, 196f));

        GameObject lightGo = new GameObject("Directional Light");
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);

        CreateRegion("Region_Hub", new Vector3(0f, 2f, 0f), new Vector3(34f, 4f, 34f), "region_tutorial_hub", "Tutorial Hub", new List<string>{"safe_zone","hub","training"});
        CreateRegion("Region_Archive", new Vector3(-46f, 2f, 10f), new Vector3(30f, 4f, 28f), "region_archive_walk", "Archive Walk", new List<string>{"lore","archives","ruins"});
        CreateRegion("Region_Ember", new Vector3(46f, 2f, 10f), new Vector3(30f, 4f, 28f), "region_ember_yard", "Ember Yard", new List<string>{"combat","arena","ember"});
        CreateRegion("Region_Hollows", new Vector3(0f, 2f, -44f), new Vector3(40f, 4f, 30f), "region_hollow_south", "Hollow South", new List<string>{"wilds","danger","hunt"});

        // landmarks
        for (int i = -2; i <= 2; i++)
            CreateProp(PrimitiveType.Cube, new Vector3(i * 6f, 1.5f, 18f), new Vector3(2f, 3f, 2f), new Color(0.32f, 0.34f, 0.37f));

        CreateProp(PrimitiveType.Cylinder, new Vector3(-46f, 1.5f, 10f), new Vector3(4f, 3f, 4f), new Color(0.35f, 0.35f, 0.45f));
        CreateProp(PrimitiveType.Cylinder, new Vector3(46f, 1.5f, 10f), new Vector3(4f, 3f, 4f), new Color(0.45f, 0.3f, 0.22f));
        CreateProp(PrimitiveType.Cube, new Vector3(0f, 1f, -44f), new Vector3(16f, 2f, 16f), new Color(0.18f, 0.3f, 0.18f));
    }

    private static void BuildNpcGuide()
    {
        GameObject root = new GameObject("Archivist Ren");
        root.transform.position = new Vector3(0f, 0f, 8f);

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.GetComponent<Renderer>().sharedMaterial.color = new Color(0.85f, 0.82f, 0.58f, 1f);

        var info = root.AddComponent<EntityInfo>();
        info.entityId = "npc_archivist_ren";
        info.displayName = "Archivist Ren";
        info.factionId = "the_archives";
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = new[] { "guide", "quest_giver", "lorekeeper" };

        var agent = root.AddComponent<NpcDialogueAgent>();
        agent.npcId = info.entityId;
        agent.npcName = info.displayName;
        agent.CommitNpcLine("You are awake in the tutorial corridor. Move, fight, learn, then ask the world who you are.");
    }

    private static void BuildShrines()
    {
        CreateShrine(new Vector3(0f, 0.75f, 18f), "Shrine of Recall", new Color(0.5f, 0.85f, 1f, 1f));
        CreateShrine(new Vector3(-38f, 0.75f, 10f), "Shrine of Echoes", new Color(0.65f, 1f, 0.65f, 1f));
        CreateShrine(new Vector3(38f, 0.75f, 10f), "Shrine of Ash", new Color(1f, 0.68f, 0.4f, 1f));
    }

    private static void BuildEnemies()
    {
        SpawnEnemy(new Vector3(44f, 1f, 18f), "ember_court", "region_ember_yard", new Color(0.95f, 0.25f, 0.2f));
        SpawnEnemy(new Vector3(52f, 1f, 6f), "ember_court", "region_ember_yard", new Color(0.95f, 0.25f, 0.2f));
        SpawnEnemy(new Vector3(36f, 1f, 4f), "ember_court", "region_ember_yard", new Color(0.95f, 0.25f, 0.2f));

        SpawnEnemy(new Vector3(-6f, 1f, -36f), "wild_hollows", "region_hollow_south", new Color(0.8f, 0.15f, 0.15f));
        SpawnEnemy(new Vector3(10f, 1f, -48f), "wild_hollows", "region_hollow_south", new Color(0.8f, 0.15f, 0.15f));
        SpawnEnemy(new Vector3(0f, 1f, -54f), "wild_hollows", "region_hollow_south", new Color(0.8f, 0.15f, 0.15f));
    }

    private static void WireSystems(GameObject player)
    {
        var cam = Camera.main;
        var snapshot = Object.FindFirstObjectByType<SituationSnapshotBuilder>();
        if (snapshot != null)
        {
            snapshot.player = player.transform;
            snapshot.playerContext = Object.FindFirstObjectByType<PlayerContext>();
            snapshot.playerStateManager = Object.FindFirstObjectByType<PlayerStateManager>();
        }

        var dialogueUi = Object.FindFirstObjectByType<DialogueBoxUI>();
        if (dialogueUi == null)
            dialogueUi = new GameObject("DialogueBoxUI").AddComponent<DialogueBoxUI>();
        dialogueUi.viewCamera = cam;
        dialogueUi.playerRoot = player.transform;

        var combat = player.GetComponent<YQPrototypePlayerCombat>();
        if (combat != null)
        {
            combat.playerCamera = cam;
            combat.actionRecorder = player.GetComponent<ActionRecorder>();
            combat.vitals = player.GetComponent<YQPrototypePlayerVitals>();
        }

        var tutorial = new GameObject("YQPrototypeTutorialDirector").AddComponent<YQPrototypeTutorialDirector>();
        tutorial.snapshotBuilder = snapshot;
        tutorial.worldDeltaApplier = Object.FindFirstObjectByType<WorldDeltaApplier>();
        tutorial.progressionDecisionApplier = Object.FindFirstObjectByType<ProgressionDecisionApplier>();

        var hud = new GameObject("YQPrototypeHUD").AddComponent<YQPrototypeHUD>();
        hud.tutorialDirector = tutorial;
    }

    private static T EnsureSingleton<T>(string name) where T : Component
    {
        T existing = Object.FindFirstObjectByType<T>();
        if (existing != null) return existing;
        GameObject go = new GameObject(name);
        return go.AddComponent<T>();
    }

    private static ProgressionBalanceConfig FindProgressionBalanceAsset()
    {
        string[] guids = AssetDatabase.FindAssets("t:ProgressionBalanceConfig");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<ProgressionBalanceConfig>(path);
            if (asset != null)
                return asset;
        }
        return null;
    }

    private static void CreateGround(Vector3 position, Vector3 scale, Color color)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "YQ_Tutorial_Ground";
        ground.transform.position = position;
        ground.transform.localScale = scale;
        ground.GetComponent<Renderer>().sharedMaterial.color = color;
    }

    private static void CreateBoundary(Vector3 position, Vector3 scale)
    {
        CreateProp(PrimitiveType.Cube, position, scale, new Color(0.11f, 0.12f, 0.14f));
    }

    private static void CreateProp(PrimitiveType type, Vector3 position, Vector3 scale, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.transform.position = position;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial.color = color;
    }

    private static void CreateRegion(string name, Vector3 position, Vector3 size, string regionId, string regionName, List<string> tags)
    {
        GameObject go = new GameObject(name);
        go.transform.position = position;
        var collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;

        var region = go.AddComponent<RegionVolume>();
        region.regionId = regionId;
        region.regionName = regionName;
        region.tags = tags;
    }

    private static void CreateShrine(Vector3 position, string displayName, Color color)
    {
        GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.name = displayName;
        root.transform.position = position;
        root.transform.localScale = new Vector3(1.15f, 0.75f, 1.15f);
        root.GetComponent<Renderer>().sharedMaterial.color = color;

        var info = root.AddComponent<EntityInfo>();
        info.entityId = displayName.ToLowerInvariant().Replace(" ", "_");
        info.displayName = displayName;
        info.factionId = "the_archives";
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = new[] { "shrine", "interactable", "healing" };

        var shrine = root.AddComponent<YQPrototypeShrine>();
        shrine.shrineLabel = displayName;
    }

    private static void SpawnEnemy(Vector3 position, string factionId, string semanticRegionId, Color color)
    {
        GameObject root = new GameObject($"Enemy_{semanticRegionId}_{position.x:0}_{position.z:0}");
        root.transform.position = position;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        body.GetComponent<Renderer>().sharedMaterial.color = color;

        var rb = root.AddComponent<Rigidbody>();
        rb.mass = 40f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        var collider = root.AddComponent<CapsuleCollider>();
        collider.height = 2f;
        collider.radius = 0.45f;
        collider.center = new Vector3(0f, 1f, 0f);

        var info = root.AddComponent<EntityInfo>();
        info.entityId = root.name.ToLowerInvariant();
        info.displayName = "Hostile Echo";
        info.factionId = factionId;
        info.hostility = Hostility.Hostile;
        info.isNotable = false;
        info.tags = new[] { "enemy", "melee", semanticRegionId };

        var enemy = root.AddComponent<YQPrototypeEnemy>();
        enemy.semanticRegionId = semanticRegionId;
        enemy.factionId = factionId;
    }
}
#endif

