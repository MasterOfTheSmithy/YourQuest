// Assets/Assets/Scripts/Tutorial/YourQuestTestSceneRoot.cs
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class YourQuestTestSceneRoot : MonoBehaviour
{
    private const string SceneName = "YourQuest_TestScene";
    private const string SystemsRootName = "__YQ_TestSystems";
    private const string WorldRootName = "__YQ_TestWorld";
    private const string PlayerRootName = "Player";

    private void Start()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
            return;

        BuildIfNeeded();
    }

    [ContextMenu("Build Test Scene")]
    public void BuildIfNeeded()
    {
        if (GameObject.Find(SystemsRootName) == null)
            CreateSystems();

        if (GameObject.Find(WorldRootName) == null)
            CreateWorld();

        if (GameObject.Find(PlayerRootName) == null)
            CreatePlayer();

        WireRuntimeReferences();
        SeedWorldState();
    }

    private void CreateSystems()
    {
        var root = new GameObject(SystemsRootName);

        root.AddComponent<PlayerContext>();
        root.AddComponent<ActionRegistry>();
        root.AddComponent<EventAccumulator>();
        root.AddComponent<PlayerStateManager>();
        root.AddComponent<WorldStateManager>();

        var llm = root.AddComponent<LLMClient>();
        llm.model = "mistral:7b-instruct-q4_K_M";
        llm.apiUrl = "http://127.0.0.1:11434";
        llm.logRequestJson = true;
        llm.logRawModelText = true;

        var snapshot = root.AddComponent<SituationSnapshotBuilder>();
        snapshot.regionId = "region_test_hub";
        snapshot.regionName = "Test Hub";
        snapshot.autoPopulateFromRuntime = true;
        snapshot.sampleInterval = 0.2f;
        snapshot.threatRadius = 20f;

        var worldApplier = root.AddComponent<WorldDeltaApplier>();
        worldApplier.minConfidence = 0.25f;

        var worldThink = root.AddComponent<LLMThinkCycle>();
        worldThink.worldDeltaApplier = worldApplier;
        worldThink.situationSnapshotBuilder = snapshot;
        worldThink.thinkEverySeconds = 8f;
        worldThink.minTotalSignificance = 2.5f;
        worldThink.requireIdleLLM = true;
        worldThink.retrySoonWhenBusy = true;
        worldThink.logPrompt = true;
        worldThink.logRawResponse = true;

        var playerProfile = root.AddComponent<PlayerProfile>();

        var progressionApplier = root.AddComponent<ProgressionDecisionApplier>();
        progressionApplier.playerProfile = playerProfile;
        progressionApplier.snapshotBuilder = snapshot;
        progressionApplier.minConfidence = 0.25f;

        var progressionConfig = ScriptableObject.CreateInstance<ProgressionBalanceConfig>();
        progressionConfig.thinkEverySeconds = 12f;
        progressionConfig.maxRecentEvents = 128;
        progressionConfig.minScoreToConsider = 3f;
        progressionConfig.scoreForSkillCandidate = 8f;
        progressionConfig.scoreForTitleCandidate = 10f;
        progressionConfig.scoreForQuestCandidate = 12f;
        progressionConfig.skillCooldown = 30f;
        progressionConfig.titleCooldown = 60f;
        progressionConfig.questCooldown = 45f;

        var progressionThink = root.AddComponent<ProgressionThinkCycle>();
        progressionThink.balance = progressionConfig;
        progressionThink.applier = progressionApplier;
        progressionThink.logPrompt = true;
        progressionThink.logRawResponse = true;

        var dialogueThink = root.AddComponent<DialogueThinkService>();
        dialogueThink.situationSnapshotBuilder = snapshot;
        dialogueThink.logPrompt = true;
        dialogueThink.logRaw = true;

        root.AddComponent<YourQuestTestHud>();
    }

    private void CreateWorld()
    {
        var root = new GameObject(WorldRootName);

        var lightGo = new GameObject("Directional Light");
        lightGo.transform.SetParent(root.transform, false);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(root.transform, false);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5f, 1f, 5f);
        var groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer != null)
            groundRenderer.sharedMaterial.color = new Color(0.23f, 0.24f, 0.26f);

        CreateRegion(root.transform, "Hub Region", new Vector3(0f, 0.5f, 0f), new Vector3(24f, 2f, 24f), "region_test_hub", "Test Hub", new[] { "hub", "safe_zone", "tutorial" });
        CreateRegion(root.transform, "North Field", new Vector3(0f, 0.5f, 26f), new Vector3(24f, 2f, 20f), "region_test_north", "North Field", new[] { "outdoors", "combat", "training" });
        CreateRegion(root.transform, "South Ruins", new Vector3(0f, 0.5f, -26f), new Vector3(24f, 2f, 20f), "region_test_south", "South Ruins", new[] { "ruins", "danger", "combat" });

        CreateWall(root.transform, new Vector3(0f, 2f, 50f), new Vector3(100f, 4f, 1f));
        CreateWall(root.transform, new Vector3(0f, 2f, -50f), new Vector3(100f, 4f, 1f));
        CreateWall(root.transform, new Vector3(50f, 2f, 0f), new Vector3(1f, 4f, 100f));
        CreateWall(root.transform, new Vector3(-50f, 2f, 0f), new Vector3(1f, 4f, 100f));

        CreateShrine(root.transform, new Vector3(-8f, 0.75f, 6f), "Archive Shrine", new Color(0.35f, 0.75f, 1f), "faction_archivists");
        CreateShrine(root.transform, new Vector3(10f, 0.75f, -6f), "Ember Shrine", new Color(1f, 0.5f, 0.2f), "faction_embers");

        CreateNpc(root.transform, new Vector3(-4f, 1f, 4f), "npc_archivist_01", "Archivist Vey", new[] { "friendly", "scholar", "questgiver" }, "faction_archivists");
        CreateNpc(root.transform, new Vector3(6f, 1f, 2f), "npc_guard_01", "Gate Warden", new[] { "rude", "guard", "pompous" }, "faction_wardens");

        CreateEnemy(root.transform, new Vector3(0f, 1f, 22f), "mob_echo_01", "Echo Wisp", 1);
        CreateEnemy(root.transform, new Vector3(6f, 1f, 28f), "mob_echo_02", "Echo Wisp", 1);
        CreateEnemy(root.transform, new Vector3(-7f, 1f, -22f), "mob_husk_01", "Ash Husk", 2);
        CreateEnemy(root.transform, new Vector3(4f, 1f, -30f), "mob_husk_02", "Ash Husk", 2);
    }

    private void CreatePlayer()
    {
        var player = new GameObject(PlayerRootName);
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 2f, 0f);

        var rb = player.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var capsule = player.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        var pivot = new GameObject("CameraPivot").transform;
        pivot.SetParent(player.transform, false);
        pivot.localPosition = new Vector3(0f, 1.55f, 0f);

        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        camGo.AddComponent<AudioListener>();
        cam.transform.position = pivot.position + new Vector3(0f, 1f, -3f);

        var recorder = player.AddComponent<ActionRecorder>();
        recorder.cellSize = 20f;

        var controller = player.AddComponent<PlayerController>();
        controller.cameraPivot = pivot;
        controller.playerCamera = cam;
        controller.actionRecorder = recorder;
        controller.viewMode = PlayerController.ViewMode.ThirdPerson;

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Visual";
        body.transform.SetParent(player.transform, false);
        body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        var bodyCollider = body.GetComponent<Collider>();
        if (bodyCollider != null) Destroy(bodyCollider);
        var bodyRenderer = body.GetComponent<Renderer>();
        if (bodyRenderer != null)
            bodyRenderer.sharedMaterial.color = new Color(0.35f, 0.55f, 0.95f);

        player.AddComponent<YourQuestTestCombat>();

        var ui = player.AddComponent<DialogueBoxUI>();
        ui.playerRoot = player.transform;
        ui.viewCamera = cam;
        ui.talkRadius = 4f;
        ui.requireLineOfSight = false;
        ui.requireDialogueAgent = true;
        ui.pauseTimeWhenOpen = false;
        ui.unlockCursorWhenOpen = true;
    }

    private void WireRuntimeReferences()
    {
        var player = GameObject.Find(PlayerRootName);
        var snapshot = FindFirstObjectByType<SituationSnapshotBuilder>();
        if (snapshot != null && player != null)
        {
            snapshot.player = player.transform;
            snapshot.playerContext = PlayerContext.Instance;
            snapshot.playerStateManager = PlayerStateManager.Instance;
        }

        var applier = FindFirstObjectByType<WorldDeltaApplier>();
        if (applier != null)
            applier.worldStateManager = WorldStateManager.Instance;
    }

    private void SeedWorldState()
    {
        var wsm = WorldStateManager.Instance;
        if (wsm == null || wsm.State == null)
            return;

        var ws = wsm.State;
        ws.worldName = "YourQuest Testbed";
        ws.currentRegionId = "region_test_hub";
        ws.currentRegionName = "Test Hub";

        ws.ApplyFactionDelta("faction_archivists", "set", 0.35f, "Curious about the player.");
        ws.ApplyFactionDelta("faction_wardens", "set", 0.05f, "Watching the gate and measuring intent.");
        ws.ApplyFactionDelta("faction_embers", "set", -0.15f, "Smoldering resentment around old shrines.");

        ws.ApplyLocationDelta("loc_test_hub", "set", 0.85f, "stable", "A controlled testing ground for systems.");
        ws.ApplyLocationDelta("loc_test_north", "set", 0.65f, "restless", "Echoes drift across the field.");
        ws.ApplyLocationDelta("loc_test_south", "set", 0.75f, "dangerous", "The ruins answer violence with more violence.");

        ws.AppendCanon("The Test Hub is an artificial proving ground where systems wake in miniature before they are trusted with a true world.");
        wsm.Save();
    }

    private void CreateWall(Transform parent, Vector3 position, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Boundary";
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        var r = wall.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial.color = new Color(0.14f, 0.14f, 0.16f);
    }

    private void CreateRegion(Transform parent, string name, Vector3 position, Vector3 size, string regionId, string regionName, string[] tags)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = position;

        var collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;

        var rv = go.AddComponent<RegionVolume>();
        rv.regionId = regionId;
        rv.regionName = regionName;
        rv.playerTag = "Player";
        rv.clearOnExit = false;
        rv.tags = new System.Collections.Generic.List<string>(tags);
    }

    private void CreateNpc(Transform parent, Vector3 position, string entityId, string displayName, string[] tags, string factionId)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.transform.SetParent(parent, false);
        root.name = displayName;
        root.transform.position = position;

        var renderer = root.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial.color = new Color(0.8f, 0.8f, 0.45f);

        var info = root.AddComponent<EntityInfo>();
        info.entityId = entityId;
        info.displayName = displayName;
        info.factionId = factionId;
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = tags;

        var agent = root.AddComponent<NpcDialogueAgent>();
        agent.npcId = entityId;
        agent.npcName = displayName;
        agent.tagsOverride = new System.Collections.Generic.List<string>(tags);
    }

    private void CreateEnemy(Transform parent, Vector3 position, string entityId, string displayName, int level)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        root.transform.SetParent(parent, false);
        root.name = displayName;
        root.transform.position = position;

        var rb = root.AddComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.mass = 30f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var renderer = root.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial.color = new Color(0.75f, 0.2f, 0.2f);

        var info = root.AddComponent<EntityInfo>();
        info.entityId = entityId;
        info.displayName = displayName;
        info.level = level;
        info.factionId = "faction_hostiles";
        info.hostility = Hostility.Hostile;
        info.isNotable = false;
        info.tags = new[] { "enemy", "hostile", "combat" };

        var enemy = root.AddComponent<YourQuestTestEnemy>();
        enemy.maxHealth = 40 + (level * 10);
        enemy.moveSpeed = 3.5f + (level * 0.15f);
        enemy.contactDamage = 8 + (level * 2);
    }

    private void CreateShrine(Transform parent, Vector3 position, string displayName, Color color, string factionId)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        root.transform.SetParent(parent, false);
        root.name = displayName;
        root.transform.position = position;
        root.transform.localScale = new Vector3(1.1f, 0.75f, 1.1f);

        var r = root.GetComponent<Renderer>();
        if (r != null) r.sharedMaterial.color = color;

        var info = root.AddComponent<EntityInfo>();
        info.entityId = displayName.ToLowerInvariant().Replace(" ", "_");
        info.displayName = displayName;
        info.hostility = Hostility.Friendly;
        info.factionId = factionId;
        info.isNotable = true;
        info.tags = new[] { "shrine", "interactable", "healing" };

        root.AddComponent<YourQuestTestShrine>();
    }
}
