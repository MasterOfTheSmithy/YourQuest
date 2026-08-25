// Assets/Assets/Scripts/Tutorial/YQWorldDialogueValidationHarness.cs
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

[DisallowMultipleComponent]
public sealed class YQWorldDialogueValidationHarness : MonoBehaviour
{
    public EntityInfo validationNpc;
    public NpcDialogueAgent validationAgent;
    public YQInvestorDialogueUI dialogueUi;
    public Transform curatedSettlementRoot;

    private string _status = "Preparing validation...";
    private bool _passed;

    private IEnumerator Start()
    {
        EnsureRuntimeValidationScene();
        yield return null;

        if (validationNpc == null || validationAgent == null || dialogueUi == null)
        {
            Fail("Scene references are incomplete.");
            yield break;
        }

        // note: A dedicated test identity keeps validation data isolated from real NPC memories and player saves.
        validationAgent.RefreshIdentityAndSession();
        validationAgent.ClearRecent();
        validationAgent.CommitPlayerLine("Can you show me what the transcript remembers?");
        validationAgent.CommitNpcLine("Yes. Your question and this answer are both persisted, ordered, and visible here.");

        bool opened =
            dialogueUi.OpenNpcForValidation(
                validationNpc,
                validationAgent);

        yield return null;
        yield return new WaitForEndOfFrame();

        string visible = dialogueUi.VisibleTranscriptText;
        bool hasPlayer = visible.Contains("what the transcript remembers");
        bool hasNpc = visible.Contains("both persisted, ordered, and visible");
        bool hasTurns = dialogueUi.VisibleTranscriptTurnCount == 2;
        bool hasCuratedWorld = curatedSettlementRoot != null && curatedSettlementRoot.childCount >= 3;
        bool hasUsableMaterials = HasOnlySupportedMaterials(curatedSettlementRoot);
        bool hasCreatureBinding = TryResolveValidationCreature();

        if (!opened || !hasPlayer || !hasNpc || !hasTurns || !hasCuratedWorld || !hasUsableMaterials || !hasCreatureBinding)
        {
            Fail(
                "Dialogue opened=" + opened +
                ", playerLine=" + hasPlayer +
                ", npcLine=" + hasNpc +
                ", turnCount=" + dialogueUi.VisibleTranscriptTurnCount +
                ", curatedCells=" + (curatedSettlementRoot != null ? curatedSettlementRoot.childCount : 0) +
                ", supportedMaterials=" + hasUsableMaterials +
                ", creatureBinding=" + hasCreatureBinding + ".");
            yield break;
        }

        _passed = true;
        _status = "PASS — transcript persistence, URP palettes, creature binding, and curated settlement cells are active.";
        Debug.Log("[YQWorldDialogueValidation] " + _status);
    }

    private static bool HasOnlySupportedMaterials(
        Transform root)
    {
        if (root == null)
            return false;

        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        if (renderers.Length == 0)
            return false;

        for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
        {
            Material[] materials =
                renderers[rendererIndex].sharedMaterials;

            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (material == null ||
                    material.shader == null ||
                    !material.shader.isSupported ||
                    material.shader.name.Contains("InternalErrorShader"))
                {
                    // note: The validation scene treats any unsupported Hivemind material as a release-blocking palette failure.
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryResolveValidationCreature()
    {
        YQRuntimeWorldAssetRegistry registry =
            YQRuntimeWorldAssetRegistry.Instance;

        // note: This exercises the same lazy creature shard used by generated encampments rather than the intentionally empty root registry.
        return registry != null &&
               YQRuntimeCreatureAssetIndex.TryResolveMonster(
                   registry,
                   "sewer mutants",
                   "validation_family",
                   "validation_variant",
                   out YQRuntimeWorldAssetEntry entry,
                   out _) &&
               entry != null &&
               entry.prefab != null;
    }

    private void Fail(string reason)
    {
        _passed = false;
        _status = "FAIL — " + reason;
        Debug.LogError("[YQWorldDialogueValidation] " + _status);
    }

    private void EnsureRuntimeValidationScene()
    {
        if (validationNpc != null && validationAgent != null && dialogueUi != null && curatedSettlementRoot != null)
            return;

        // note: The tiny serialized test scene builds its isolated fixtures at runtime, so it remains stable even while the main scene is open in another Unity editor session.
        BuildLightingAndGround();
        curatedSettlementRoot = BuildCuratedSettlementCells();
        Transform player = BuildPlayer();
        BuildNpc(out validationNpc, out validationAgent);
        dialogueUi = BuildDialogueSystems(player);
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

        GameObject street = GameObject.CreatePrimitive(PrimitiveType.Cube);
        street.name = "Street_CirculationSpine";
        street.transform.position = new Vector3(0f, 0.03f, 9f);
        street.transform.localScale = new Vector3(6f, 0.08f, 48f);
    }

    private static Transform BuildCuratedSettlementCells()
    {
        Transform root = new GameObject("03__CuratedSettlementCells").transform;
        SpawnCuratedCell(root, "Residential_HouseOnAHill",
            "Assets/HIVEMIND/HouseOnaHill/HDRP/Art/Prefabs/SM_House.prefab",
            new Vector3(-11f, 0f, 6f), 90f);
        SpawnCuratedCell(root, "Commerce_CyberpunkShop",
            "Assets/HIVEMIND/CyberpunkCity/HDRP(Default)/Art/Prefabs/SM_MERGED_BP_House_Shop_E10.prefab",
            new Vector3(11f, 0f, 8f), -90f);
        SpawnCuratedCell(root, "Civic_VikingHall",
            "Assets/HIVEMIND/ModularVikingVillage/HDRP/Art/Prefabs/SM_HouseBuilding_001_a.prefab",
            new Vector3(-11f, 0f, 23f), 90f);
        return root;
    }

    private static void SpawnCuratedCell(Transform parent, string name, string assetPath, Vector3 position, float yaw)
    {
        YQRuntimeWorldAssetRegistry registry = YQRuntimeWorldAssetRegistry.Instance;
        GameObject prefab = registry != null ? registry.ResolvePrefab(assetPath) : null;
        GameObject instance = prefab != null ? Instantiate(prefab, parent) : GameObject.CreatePrimitive(PrimitiveType.Cube);
        if (instance.transform.parent != parent)
            instance.transform.SetParent(parent, false);

        instance.name = name;
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        if (prefab == null)
            instance.transform.localScale = new Vector3(8f, 4f, 8f);
        else
            registry.ApplyMaterialOverrides(assetPath, instance);

        YQRuntimeUrpMaterialRepair.RepairHierarchy(instance);
        FitAndGround(instance, 15f, 12f);
    }

    private static void FitAndGround(GameObject root, float maxFootprint, float maxHeight)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return;

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float factor = Mathf.Min(1f,
            Mathf.Min(maxFootprint / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z)),
                maxHeight / Mathf.Max(0.01f, bounds.size.y)));
        root.transform.localScale *= factor;

        renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        root.transform.position += Vector3.up * -bounds.min.y;
    }

    private static Transform BuildPlayer()
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
        return player.transform;
    }

    private static void BuildNpc(out EntityInfo info, out NpcDialogueAgent agent)
    {
        GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_TranscriptValidator";
        npc.transform.position = new Vector3(0f, 1f, 0f);

        info = npc.AddComponent<EntityInfo>();
        info.entityId = "npc_world_dialogue_validation";
        info.displayName = "Iona, Memory Auditor";
        info.level = 1;
        info.factionId = "validation_only";
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = new[] { "validation", "scholar", "friendly" };

        agent = npc.AddComponent<NpcDialogueAgent>();
        agent.npcId = info.entityId;
        agent.npcName = info.displayName;
        agent.personaSummary = "A concise in-world auditor who verifies memory without canned tutorial dialogue.";
    }

    private static YQInvestorDialogueUI BuildDialogueSystems(Transform player)
    {
        if (DialogueThinkService.Instance == null)
            new GameObject("01__System_DialogueThinkService").AddComponent<DialogueThinkService>();
        if (LLMClient.Instance == null)
            new GameObject("01__System_LLMClient").AddComponent<LLMClient>();

        if (FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventGo = new GameObject("04__UI_EventSystem");
            eventGo.AddComponent<EventSystem>();
            eventGo.AddComponent<InputSystemUIInputModule>();
        }

        YQInvestorDialogueUI ui = YQInvestorDialogueUI.Instance;
        if (ui == null)
            ui = new GameObject("04__UI_Dialogue").AddComponent<YQInvestorDialogueUI>();
        ui.playerRoot = player;
        ui.viewCamera = Camera.main;
        ui.talkRadius = 6f;
        ui.requireLineOfSight = false;
        return ui;
    }

    private void OnGUI()
    {
        // note: The test status remains visible even if TextMeshPro or gameplay HUD setup is the subject under test.
        Color previous = GUI.color;
        GUI.color = _passed ? new Color(0.72f, 1f, 0.72f, 1f) : Color.white;
        GUI.Box(new Rect(18f, 18f, 720f, 70f),
            "YOURQUEST WORLD + DIALOGUE VALIDATION\n" + _status +
            "\nClose the transcript with Esc; walk the authored street and press E near the NPC to reopen it.");
        GUI.color = previous;
    }
}
