#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class YourQuestAssetTestSceneBuilder
{
    private const string SceneFolder = "Assets/Assets/Scenes";
    private const string ScenePath = "Assets/Assets/Scenes/YourQuest_AssetTest.unity";
    private const string ResourceFolder = "Assets/Assets/Resources";
    private const string LibraryPath = "Assets/Assets/Resources/GeneratedRpgContentLibrary.asset";
    private const string MaterialFolder = "Assets/Assets/Materials";
    private const string AssetTestMaterialFolder = "Assets/Assets/Materials/AssetTest";
    private const string AssetTestRepairedMaterialFolder = "Assets/Assets/Materials/AssetTest/Repaired";
    private const string PendingBuildRequestPath = "Assets/Assets/EditorBuildRequests/BuildAssetTestScene.request";
    private const float ShowcaseMaxFootprint = 5.6f;
    private const float ShowcaseMaxHeight = 5.2f;
    private const int RepairedMaterialNameLimit = 96;
    private static readonly string[] BaseTextureProperties = { "_BaseMap", "_MainTex", "_Albedo", "_BaseColorMap", "_DiffuseMap", "_ColorMap" };
    private static readonly string[] BaseColorProperties = { "_BaseColor", "_Color", "_TintColor" };
    private static readonly string[] NormalTextureProperties = { "_BumpMap", "_NormalMap" };
    private static readonly string[] BaseTextureNameHints = { "albedo", "basecolor", "base color", "base map", "diffuse", "color", "col" };
    private static readonly string[] NormalTextureNameHints = { "normal", "bump", "nrm" };
    private static readonly string[] NonBaseTextureNameHints = { "normal", "bump", "nrm", "rough", "metal", "metallic", "smooth", "ambientocclusion", "occlusion", "_ao", " ao", "height", "mask", "emiss", "spec", "orm" };
    private static readonly string[] FallbackSkipTokens = { "missingmaterial", "missing", "runtimeurp", "repaired", "assettest", "material", "materials", "mat", "mesh", "renderer", "object", "gameobject", "prefab", "model", "models", "lod", "group", "human", "male", "female", "base" };
    private static readonly string[] NumberedFamilyPrefixes = { "sword", "dagger", "axe", "hammer", "club", "bow", "crossbow", "shield", "staff", "chest" };

    private sealed class MaterialCandidate
    {
        public Material material;
        public string text;
        public bool hasTexture;
        public bool hasUsefulColor;
    }

    private static readonly Dictionary<string, Material> FallbackMaterialCache = new Dictionary<string, Material>();
    private static MaterialCandidate[] MaterialCandidates;

    [InitializeOnLoadMethod]
    private static void RegisterPendingBuildRequest()
    {
        EditorApplication.delayCall += TryConsumePendingBuildRequest;
    }

    private static void TryConsumePendingBuildRequest()
    {
        if (!File.Exists(PendingBuildRequestPath))
            return;

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += TryConsumePendingBuildRequest;
            return;
        }

        AssetDatabase.DeleteAsset(PendingBuildRequestPath);
        BuildAssetTestSceneInternal(false, false);
    }

    // note: Build or Refresh is the single active regeneration command because it already overwrites the complete generated test scene.
    [MenuItem("Tools/YourQuest/Testing/Asset Test Scene/Build or Refresh")]
    public static void BuildAssetTestScene()
    {
        BuildAssetTestSceneInternal(true, true);
    }

    public static void BuildAssetTestSceneHeadless()
    {
        BuildAssetTestSceneInternal(false, false);
    }

    [MenuItem("Tools/YourQuest/Testing/Asset Test Scene/Open")]
    public static void OpenAssetTestScene()
    {
        EnsureFolders();
        if (!File.Exists(ScenePath))
            BuildAssetTestSceneInternal(true, false);
        else
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
    }

    // note: Preserve the delete-first variant for recovery while keeping the redundant action out of the normal testing menu.
    [MenuItem("Tools/YourQuest/Archived Tools/Testing/Rebuild Asset Test Scene (Legacy)")]
    public static void RebuildAssetTestScene()
    {
        EnsureFolders();
        if (File.Exists(ScenePath))
        {
            AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.Refresh();
        }

        BuildAssetTestSceneInternal(true, true);
    }

    private static void BuildAssetTestSceneInternal(bool openWhenDone, bool showDialog)
    {
        EnsureFolders();
        FallbackMaterialCache.Clear();
        MaterialCandidates = null;
        GeneratedRpgContentLibrary library = EnsureLibraryAsset();
        RefreshLibraryAsset(library);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = "YourQuest_AssetTest";
        SceneManager.SetActiveScene(scene);

        ConfigureSceneSettings();
        BuildSystemRoots(library);
        BuildAssetWorld();
        BuildPlayer();
        BuildUiRoots();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (openWhenDone)
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (showDialog)
            EditorUtility.DisplayDialog("YourQuest", "Asset test scene built.", "OK");

        Debug.Log("[YourQuest] Built asset test scene at " + ScenePath);
    }

    private static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Assets"))
            AssetDatabase.CreateFolder("Assets", "Assets");
        if (!AssetDatabase.IsValidFolder(SceneFolder))
            AssetDatabase.CreateFolder("Assets/Assets", "Scenes");
        if (!AssetDatabase.IsValidFolder(ResourceFolder))
            AssetDatabase.CreateFolder("Assets/Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(MaterialFolder))
            AssetDatabase.CreateFolder("Assets/Assets", "Materials");
        if (!AssetDatabase.IsValidFolder(AssetTestMaterialFolder))
            AssetDatabase.CreateFolder(MaterialFolder, "AssetTest");
        if (!AssetDatabase.IsValidFolder(AssetTestRepairedMaterialFolder))
            AssetDatabase.CreateFolder(AssetTestMaterialFolder, "Repaired");
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

    private static void RefreshLibraryAsset(GeneratedRpgContentLibrary library)
    {
        if (library == null)
            return;

        GeneratedRpgContentLibrary defaults = ScriptableObject.CreateInstance<GeneratedRpgContentLibrary>();
        library.prefabKeys = MergeUnique(library.prefabKeys, defaults.prefabKeys);
        library.effectKeys = MergeUnique(library.effectKeys, defaults.effectKeys);
        library.weaponPrefabKeys = MergeUnique(library.weaponPrefabKeys, defaults.weaponPrefabKeys);
        library.offhandPrefabKeys = MergeUnique(library.offhandPrefabKeys, defaults.offhandPrefabKeys);
        library.headPrefabKeys = MergeUnique(library.headPrefabKeys, defaults.headPrefabKeys);
        library.chestPrefabKeys = MergeUnique(library.chestPrefabKeys, defaults.chestPrefabKeys);
        library.glovesPrefabKeys = MergeUnique(library.glovesPrefabKeys, defaults.glovesPrefabKeys);
        library.legsPrefabKeys = MergeUnique(library.legsPrefabKeys, defaults.legsPrefabKeys);
        library.bootsPrefabKeys = MergeUnique(library.bootsPrefabKeys, defaults.bootsPrefabKeys);
        library.beltPrefabKeys = MergeUnique(library.beltPrefabKeys, defaults.beltPrefabKeys);
        library.ringPrefabKeys = MergeUnique(library.ringPrefabKeys, defaults.ringPrefabKeys);
        library.necklacePrefabKeys = MergeUnique(library.necklacePrefabKeys, defaults.necklacePrefabKeys);
        library.trinketPrefabKeys = MergeUnique(library.trinketPrefabKeys, defaults.trinketPrefabKeys);
        library.environmentPrefabKeys = MergeUnique(library.environmentPrefabKeys, defaults.environmentPrefabKeys);
        library.chestInteractablePrefabKeys = MergeUnique(library.chestInteractablePrefabKeys, defaults.chestInteractablePrefabKeys);
        library.mimicPrefabKeys = MergeUnique(library.mimicPrefabKeys, defaults.mimicPrefabKeys);
        library.meleeAudioKeys = MergeUnique(library.meleeAudioKeys, defaults.meleeAudioKeys);
        library.magicAudioKeys = MergeUnique(library.magicAudioKeys, defaults.magicAudioKeys);
        library.aoeEffectKeys = MergeUnique(library.aoeEffectKeys, defaults.aoeEffectKeys);
        library.projectileEffectKeys = MergeUnique(library.projectileEffectKeys, defaults.projectileEffectKeys);
        library.beamEffectKeys = MergeUnique(library.beamEffectKeys, defaults.beamEffectKeys);
        library.shieldEffectKeys = MergeUnique(library.shieldEffectKeys, defaults.shieldEffectKeys);
        Object.DestroyImmediate(defaults);

        EditorUtility.SetDirty(library);
    }

    private static string[] MergeUnique(string[] existing, string[] additions)
    {
        List<string> merged = new List<string>();
        HashSet<string> seen = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        AddUnique(existing, merged, seen);
        AddUnique(additions, merged, seen);
        return merged.ToArray();
    }

    private static void AddUnique(string[] source, List<string> target, HashSet<string> seen)
    {
        if (source == null)
            return;

        for (int i = 0; i < source.Length; i++)
        {
            string value = source[i];
            if (string.IsNullOrWhiteSpace(value))
                continue;

            value = value.Trim().Replace('\\', '/');
            if (seen.Add(value))
                target.Add(value);
        }
    }

    private static void ConfigureSceneSettings()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.58f, 0.62f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.18f, 0.19f, 0.21f, 1f);
        RenderSettings.fogDensity = 0.008f;
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

        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem == null)
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
        YQInvestorDialogueUI dialogue = CreateSingletonRoot<YQInvestorDialogueUI>("04__UI_Dialogue");
        dialogue.talkRadius = 5.75f;
        dialogue.requireLineOfSight = false;
    }

    private static void BuildAssetWorld()
    {
        GameObject root = new GameObject("03__World_AssetTest");
        Transform environment = CreateChild(root.transform, "Environment_Showroom").transform;
        Transform loot = CreateChild(root.transform, "Loot_And_Interactables").transform;
        Transform equipment = CreateChild(root.transform, "Weapons_Armor_Accessories").transform;
        Transform characters = CreateChild(root.transform, "Character_Mannequins").transform;
        Transform audioVfx = CreateChild(root.transform, "Audio_And_VFX_References").transform;
        Transform dialogue = CreateChild(root.transform, "Dialogue_Test_NPCs").transform;
        Transform regions = CreateChild(root.transform, "Regions").transform;
        Transform lighting = CreateChild(root.transform, "Lighting").transform;

        BuildLighting(lighting);
        BuildWalkableGround(environment);
        BuildVictorianWing(environment);
        BuildAsianWing(environment);
        BuildLootWing(loot);
        BuildEquipmentWing(equipment);
        BuildCharacterWing(characters);
        BuildAudioVfxWing(audioVfx);
        BuildDialogueWing(dialogue);
        BuildSceneRegions(regions);
    }

    private static void BuildLighting(Transform parent)
    {
        GameObject lightGo = new GameObject("Directional Light");
        lightGo.transform.SetParent(parent, false);
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.55f;
        light.shadows = LightShadows.Soft;
        light.shadowResolution = LightShadowResolution.Medium;
        lightGo.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        CreatePointLight(parent, "Spawn Fill", new Vector3(0f, 5f, -25f), new Color(0.64f, 0.78f, 1f, 1f), 2.8f, 26f);
        CreatePointLight(parent, "Warm Mansion Fill", new Vector3(-24f, 5f, -2f), new Color(1f, 0.76f, 0.45f, 1f), 3.3f, 22f);
        CreatePointLight(parent, "Cool Equipment Fill", new Vector3(0f, 5f, 24f), new Color(0.48f, 0.70f, 1f, 1f), 2.6f, 24f);
        CreatePointLight(parent, "Arcane VFX Fill", new Vector3(24f, 5f, -24f), new Color(0.52f, 1f, 0.82f, 1f), 3.2f, 22f);
    }

    private static void BuildWalkableGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Walkable_Audit_Floor";
        ground.transform.SetParent(parent, false);
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(9f, 1f, 9f);
        Renderer groundRenderer = ground.GetComponent<Renderer>();
        if (groundRenderer != null)
        {
            groundRenderer.shadowCastingMode = ShadowCastingMode.Off;
            groundRenderer.receiveShadows = true;
        }
        SetRendererSurface(ground, new Color(0.19f, 0.20f, 0.21f, 1f), 14f);

        CreateFloorPad(parent, "Spawn_Dialogue_Pad", new Vector3(0f, 0f, -26f), new Vector3(18f, 0.04f, 16f), new Color(0.16f, 0.19f, 0.23f, 1f));
        CreateFloorPad(parent, "Mansion_Pad", new Vector3(-24f, 0f, -2f), new Vector3(22f, 0.04f, 22f), new Color(0.23f, 0.19f, 0.16f, 1f));
        CreateFloorPad(parent, "Dynasty_Pad", new Vector3(24f, 0f, -2f), new Vector3(22f, 0.04f, 22f), new Color(0.18f, 0.22f, 0.18f, 1f));
        CreateFloorPad(parent, "Loot_Pad", new Vector3(-24f, 0f, 25f), new Vector3(22f, 0.04f, 18f), new Color(0.22f, 0.20f, 0.15f, 1f));
        CreateFloorPad(parent, "Equipment_Pad", new Vector3(0f, 0f, 25f), new Vector3(22f, 0.04f, 18f), new Color(0.15f, 0.18f, 0.23f, 1f));
        CreateFloorPad(parent, "Character_Pad", new Vector3(24f, 0f, 25f), new Vector3(22f, 0.04f, 18f), new Color(0.20f, 0.17f, 0.23f, 1f));
        CreateFloorPad(parent, "Audio_VFX_Pad", new Vector3(24f, 0f, -26f), new Vector3(22f, 0.04f, 16f), new Color(0.14f, 0.23f, 0.22f, 1f));

        CreateLabel(parent, "YOURQUEST ASSET QA ROOM", new Vector3(0f, 0.06f, -35f), 0.11f, Color.white);
        CreateLabel(parent, "Open inventory with I. Talk to NPCs with E or click. Audio/VFX stations use E.", new Vector3(0f, 0.06f, -31.5f), 0.055f, new Color(0.86f, 0.91f, 1f, 1f));
    }

    private static void BuildVictorianWing(Transform parent)
    {
        Transform wing = CreateChild(parent, "Victorian_Mansion_Wing").transform;
        CreateLabel(wing, "Victorian mansion kit", new Vector3(-24f, 0.08f, -12f), 0.085f, new Color(1f, 0.88f, 0.66f, 1f));

        ShowcasePrefab(wing, "Mansion Floor", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Floor.prefab", new Vector3(-24f, 0f, -6f), Vector3.zero, new Vector3(0.8f, 0.8f, 0.8f));
        ShowcasePrefab(wing, "Wall Window", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Wall_WindowsStandard.prefab", new Vector3(-30f, 0f, -1f), new Vector3(0f, 90f, 0f), Vector3.one);
        ShowcasePrefab(wing, "Carved Door", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_DoorCarved.prefab", new Vector3(-24f, 0f, 4f), Vector3.zero, Vector3.one);
        ShowcasePrefab(wing, "Bookshelf", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Bookshelf_BIG.prefab", new Vector3(-18f, 0f, -1f), new Vector3(0f, -90f, 0f), Vector3.one);
        ShowcasePrefab(wing, "Study Table", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_WoodenStudyTable.prefab", new Vector3(-27f, 0f, 4f), Vector3.zero, Vector3.one);
        ShowcasePrefab(wing, "Fireplace", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Fireplace.prefab", new Vector3(-21f, 0f, 4f), new Vector3(0f, 180f, 0f), Vector3.one);
        ShowcasePrefab(wing, "Knight Armor", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_KnightArmor_Pose.prefab", new Vector3(-15.5f, 0f, -6f), new Vector3(0f, -30f, 0f), Vector3.one);
        ShowcasePrefab(wing, "Fire Particle", "Assets/BefourStudios/VictorianMansionEnvironment/Art/Particle/PS_Fire01.prefab", new Vector3(-21f, 0.5f, 4f), Vector3.zero, Vector3.one);
    }

    private static void BuildAsianWing(Transform parent)
    {
        Transform wing = CreateChild(parent, "Asian_Dynasty_Wing").transform;
        CreateLabel(wing, "Asian dynasty kit", new Vector3(24f, 0.08f, -12f), 0.085f, new Color(0.92f, 1f, 0.76f, 1f));

        ShowcasePrefab(wing, "Floor Set", "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_FloorSet_V3.prefab", new Vector3(24f, 0f, -7f), Vector3.zero, new Vector3(0.72f, 0.72f, 0.72f));
        ShowcasePrefab(wing, "Building", "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Building01.prefab", new Vector3(18f, 0f, -1f), new Vector3(0f, 25f, 0f), new Vector3(0.46f, 0.46f, 0.46f));
        ShowcasePrefab(wing, "Pavilion Platform", "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_MiniPavilionPlatform.prefab", new Vector3(30f, 0f, -1f), Vector3.zero, new Vector3(0.72f, 0.72f, 0.72f));
        ShowcasePrefab(wing, "Dragon", "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_ChineseDragon_1.prefab", new Vector3(24f, 0f, 6f), new Vector3(0f, 180f, 0f), new Vector3(0.52f, 0.52f, 0.52f));
        ShowcasePrefab(wing, "Torch Fire", "Assets/BefourStudios/AsianDynastyEnvironment/Art/Particle/PS_TorchFire.prefab", new Vector3(20f, 0.5f, -8f), Vector3.zero, Vector3.one);
        ShowcasePrefab(wing, "Market Props", "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Bazaar_Props1.prefab", new Vector3(30f, 0f, -8f), Vector3.zero, new Vector3(0.72f, 0.72f, 0.72f));
    }

    private static void BuildLootWing(Transform parent)
    {
        CreateLabel(parent, "Chests, mimic, and loot", new Vector3(-24f, 0.08f, 16f), 0.085f, new Color(0.95f, 0.78f, 0.45f, 1f));

        ShowcasePrefab(parent, "Chest Simple", "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestSimpleSmall.prefab", new Vector3(-31f, 0f, 29f), new Vector3(0f, 180f, 0f), Vector3.one);
        ShowcasePrefab(parent, "Chest Ornate", "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestOrnateMedium.prefab", new Vector3(-24f, 0f, 29f), new Vector3(0f, 180f, 0f), Vector3.one);
        ShowcasePrefab(parent, "Mimic", "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleMedium.prefab", new Vector3(-17f, 0f, 29f), new Vector3(0f, 180f, 0f), Vector3.one);
        ShowcasePrefab(parent, "Gold Pile", "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Loot/GoldPile.prefab", new Vector3(-28f, 0f, 22f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Gem Pile", "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Loot/GemPile.prefab", new Vector3(-20f, 0f, 22f), Vector3.zero, Vector3.one);
    }

    private static void BuildEquipmentWing(Transform parent)
    {
        CreateLabel(parent, "Generated equipment pools", new Vector3(0f, 0.08f, 15f), 0.085f, new Color(0.74f, 0.88f, 1f, 1f));

        ShowcasePrefab(parent, "Sword", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword004/Prefab/Sword004.prefab", new Vector3(-8f, 0f, 31f), new Vector3(0f, 0f, 80f), new Vector3(0.75f, 0.75f, 0.75f));
        ShowcasePrefab(parent, "Axe", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Axes/Axe004/Prefab/Axe004.prefab", new Vector3(-4f, 0f, 31f), new Vector3(0f, 0f, 70f), new Vector3(0.75f, 0.75f, 0.75f));
        ShowcasePrefab(parent, "Staff", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Staffs/Staff003/Prefab/Staff003.prefab", new Vector3(0f, 0f, 31f), new Vector3(0f, 0f, 80f), new Vector3(0.75f, 0.75f, 0.75f));
        ShowcasePrefab(parent, "Bow", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Bows, Arrows & Crossbows/Bow001 & Bow002/Prefab/Bow001_002.prefab", new Vector3(4f, 0f, 31f), new Vector3(0f, 0f, 80f), new Vector3(0.75f, 0.75f, 0.75f));
        ShowcasePrefab(parent, "Shield", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Shields/Shield004/Prefab/Shield004.prefab", new Vector3(8f, 0f, 31f), new Vector3(0f, 180f, 0f), new Vector3(0.75f, 0.75f, 0.75f));

        ShowcasePrefab(parent, "Helmet", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Helmets/Helmet003/Prefab/Helmet003.prefab", new Vector3(-8f, 0f, 24f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Chest Armor", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Spaulder_Chest.prefab", new Vector3(-4f, 0f, 24f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Gauntlet", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Gauntlet_L.prefab", new Vector3(0f, 0f, 24f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Belt", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Gladiator_Belt.prefab", new Vector3(4f, 0f, 24f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Boot", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Boot_Left.prefab", new Vector3(8f, 0f, 24f), Vector3.zero, Vector3.one);

        ShowcasePrefab(parent, "Ring", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Rings/Ring_1 1 New.prefab", new Vector3(-6f, 0f, 18f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Amulet", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Amulets/Amulet_Chain A.prefab", new Vector3(0f, 0f, 18f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Bracer", "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Bracers/Bracer_1_L A New.prefab", new Vector3(6f, 0f, 18f), Vector3.zero, Vector3.one);
    }

    private static void BuildCharacterWing(Transform parent)
    {
        CreateLabel(parent, "Human character RPG pack", new Vector3(24f, 0.08f, 16f), 0.085f, new Color(0.92f, 0.82f, 1f, 1f));
        ShowcasePrefab(parent, "Human Male", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4.1.1).prefab", new Vector3(19f, 0f, 29f), new Vector3(0f, 180f, 0f), Vector3.one);
        ShowcasePrefab(parent, "Human Female", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Female (v4).prefab", new Vector3(29f, 0f, 29f), new Vector3(0f, 180f, 0f), Vector3.one);
        ShowcasePrefab(parent, "Armor Chest", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Spaulder_Chest.prefab", new Vector3(20f, 0f, 21f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Armor Belt", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Gladiator_Belt.prefab", new Vector3(24f, 0f, 21f), Vector3.zero, Vector3.one);
        ShowcasePrefab(parent, "Sabatons", "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Male Human Armor/Sabatons_Left.prefab", new Vector3(28f, 0f, 21f), Vector3.zero, Vector3.one);
    }

    private static void BuildAudioVfxWing(Transform parent)
    {
        CreateLabel(parent, "Audio and VFX lab", new Vector3(24f, 0.08f, -35f), 0.085f, new Color(0.64f, 1f, 0.86f, 1f));
        CreateLabel(parent, "Move close and press E, or click a station.", new Vector3(24f, 0.08f, -32.5f), 0.052f, new Color(0.85f, 1f, 0.94f, 1f));

        CreateAudioVfxStation(parent, "Sword Impact", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Sword/Sword_On_Wood/Impact/Sword_On_Wood_Impact_1.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particle Components/PComponent Sparks Forward.prefab", new Vector3(13f, 0.5f, -28f), new Color(0.45f, 0.66f, 1f, 1f));
        CreateAudioVfxStation(parent, "Fire Burst", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Fire/Explosions/FireWarmupExplosion_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Fire Explosion.prefab", new Vector3(17.5f, 0.5f, -28f), new Color(1f, 0.48f, 0.28f, 1f));
        CreateAudioVfxStation(parent, "Frost Shatter", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Ice/Explosions/IceWarmupExplosion_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Ice Explosion.prefab", new Vector3(22f, 0.5f, -28f), new Color(0.52f, 0.95f, 1f, 1f));
        CreateAudioVfxStation(parent, "Storm Arc", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Electric/Explosions/ElectricWarmupExplosion_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Electric Explosion.prefab", new Vector3(26.5f, 0.5f, -28f), new Color(0.46f, 0.82f, 1f, 1f));
        CreateAudioVfxStation(parent, "Toxic Cloud", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Poison/Explosions/PoisonWarmupExplosion_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Toxic Cloud.prefab", new Vector3(31f, 0.5f, -28f), new Color(0.48f, 1f, 0.36f, 1f));
        CreateAudioVfxStation(parent, "Holy Heal", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/HolyLight/Hits/HolyLightWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Power Heal.prefab", new Vector3(35.5f, 0.5f, -28f), new Color(0.48f, 1f, 0.68f, 1f));

        CreateAudioVfxStation(parent, "Fireball", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Fire/Hits/FireWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Fire Ball.prefab", new Vector3(13f, 0.5f, -22f), new Color(1f, 0.72f, 0.36f, 1f));
        CreateAudioVfxStation(parent, "Ice Ball", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Ice/Hits/IceWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Ice Ball.prefab", new Vector3(17.5f, 0.5f, -22f), new Color(0.44f, 0.92f, 1f, 1f));
        CreateAudioVfxStation(parent, "Magic Missile", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Air/Hits/AirWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Magic Missile.prefab", new Vector3(22f, 0.5f, -22f), new Color(0.66f, 0.78f, 1f, 1f));
        CreateAudioVfxStation(parent, "Spirit Beam", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Lances/LanceBuff_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Spirit Beam.prefab", new Vector3(26.5f, 0.5f, -22f), new Color(0.68f, 0.6f, 1f, 1f));
        CreateAudioVfxStation(parent, "Force Shield", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Water/Hits/WaterWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Spiritual Force.prefab", new Vector3(31f, 0.5f, -22f), new Color(0.72f, 1f, 0.96f, 1f));
        CreateAudioVfxStation(parent, "Stone Skin", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Earth/Hits/EarthWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Stone Skin.prefab", new Vector3(35.5f, 0.5f, -22f), new Color(0.74f, 0.66f, 0.46f, 1f));

        CreateAudioVfxStation(parent, "Soul Void", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/EvilDark/Explosions/EvilDarkWarmupExplosion_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Soul Void.prefab", new Vector3(13f, 0.5f, -16f), new Color(0.64f, 0.42f, 1f, 1f));
        CreateAudioVfxStation(parent, "Weakness Hex", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/EvilDark/Hits/EvilDarkWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Weakness.prefab", new Vector3(17.5f, 0.5f, -16f), new Color(0.82f, 0.56f, 1f, 1f));
        CreateAudioVfxStation(parent, "Speed Up", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Air/Hits/AirWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Speed Up.prefab", new Vector3(22f, 0.5f, -16f), new Color(0.72f, 1f, 0.96f, 1f));
        CreateAudioVfxStation(parent, "Fire Ring", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Fire/Explosions/FireWarmupExplosion_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Fire Ring.prefab", new Vector3(26.5f, 0.5f, -16f), new Color(1f, 0.46f, 0.2f, 1f));
        CreateAudioVfxStation(parent, "Acid Spray", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/Poison/Hits/PoisonWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Acid Spray.prefab", new Vector3(31f, 0.5f, -16f), new Color(0.54f, 1f, 0.42f, 1f));
        CreateAudioVfxStation(parent, "Courage Buff", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/_Combined Clips (Magic)/HolyLight/Hits/HolyLightWarmupHit_0.wav", "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Courage.prefab", new Vector3(35.5f, 0.5f, -16f), new Color(1f, 0.92f, 0.54f, 1f));

        // note: These stations use extracted imported prefabs instead of deleted installer archives.
        CreateAudioVfxStation(parent, "Blood Jet", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Sword/Sword_On_Flesh/Impact/Sword_On_Flesh_Impact_1.wav", "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS_Blood.prefab", new Vector3(15f, 0.5f, -36f), new Color(0.8f, 0.04f, 0.04f, 1f));
        CreateAudioVfxStation(parent, "Blood Spray", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Axe/Axe On Flesh/Axe/Axe_On_Flesh_Axe_1.wav", "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS_SplatterDirectional_01.prefab", new Vector3(20f, 0.5f, -36f), new Color(0.66f, 0.02f, 0.03f, 1f));
        CreateAudioVfxStation(parent, "Blood Burst", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Axe/Axe On Flesh/Flesh/Axe_On_Flesh_Flesh_1.wav", "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS__SplatterOmni_01.prefab", new Vector3(25f, 0.5f, -36f), new Color(0.52f, 0.02f, 0.04f, 1f));
        CreateAudioVfxStation(parent, "Blood Splash", "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Axe/Axe On Flesh/Impact/Axe_On_Flesh_Impact_1.wav", "Assets/HIVEMIND/RealisticBloodVFX/HDRP(Default)/RealisticBlood/Particle Systems/PS__SplatterOmni_02.prefab", new Vector3(30f, 0.5f, -36f), new Color(0.86f, 0.22f, 0.16f, 1f));
    }

    private static void BuildDialogueWing(Transform parent)
    {
        CreateLabel(parent, "Dialogue test NPCs", new Vector3(0f, 0.08f, -20f), 0.082f, new Color(1f, 0.88f, 0.58f, 1f));
        CreateDialogueNpc(parent, "Archivist Iona", "npc_asset_archivist_iona", "the_archives", new[] { "lorekeeper", "scholar", "guide" }, new Vector3(-4.2f, 1f, -24f), new Color(0.44f, 0.63f, 0.95f, 1f), "A calm archivist who explains how imported assets, world memory, and player behavior are being evaluated in this room.");
        CreateDialogueNpc(parent, "Warden Vale", "npc_asset_warden_vale", "the_watch", new[] { "warden", "guard", "stern" }, new Vector3(4.2f, 1f, -24f), new Color(0.72f, 0.56f, 0.42f, 1f), "A blunt test-room warden who reacts to player choices and keeps answers short, practical, and in-world.");
    }

    private static void BuildSceneRegions(Transform parent)
    {
        CreateRegion(parent, "Region_AssetTest_Showroom", new Vector3(0f, 2f, 0f), new Vector3(86f, 4f, 86f), "region_asset_test_showroom", "Asset Test Showroom", new[] { "asset_test", "safe_zone", "showroom" });
        CreateRegion(parent, "Region_Dialogue_Test", new Vector3(0f, 2f, -25f), new Vector3(18f, 4f, 16f), "region_dialogue_test", "Dialogue Test", new[] { "dialogue", "npc", "safe_zone" });
        CreateRegion(parent, "Region_Equipment_Lane", new Vector3(0f, 2f, 25f), new Vector3(24f, 4f, 20f), "region_equipment_lane", "Equipment Lane", new[] { "equipment", "generation", "loot" });
        CreateRegion(parent, "Region_VFX_Lane", new Vector3(24f, 2f, -25f), new Vector3(30f, 4f, 28f), "region_vfx_lane", "VFX Lane", new[] { "magic", "vfx", "audio" });
    }

    private static void BuildPlayer()
    {
        GameObject player = new GameObject("05__Player");
        player.tag = "Player";
        player.transform.position = new Vector3(0f, 1.25f, -32f);
        player.transform.rotation = Quaternion.Euler(0f, 0f, 0f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.8f;
        controller.radius = 0.35f;
        controller.center = new Vector3(0f, 0.9f, 0f);
        controller.stepOffset = 0.45f;

        ActionRecorder recorder = player.AddComponent<ActionRecorder>();
        YQInvestorVitals vitals = player.AddComponent<YQInvestorVitals>();
        player.AddComponent<YQInvestorCombat>();
        YQInvestorPlayerMotor motor = player.AddComponent<YQInvestorPlayerMotor>();
        player.AddComponent<PlayerLocationReporter>();
        player.AddComponent<YQPlayerEquipmentVisual>();

        GameObject pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(player.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.55f, 0f);

        GameObject cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        cameraGo.transform.SetParent(player.transform, false);
        cameraGo.transform.localPosition = pivot.transform.localPosition;
        Camera cam = cameraGo.AddComponent<Camera>();
        cam.fieldOfView = 76f;
        cameraGo.AddComponent<AudioListener>();

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "Player_Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        Object.DestroyImmediate(visual.GetComponent<Collider>());
        SetRendererColor(visual, new Color(0.36f, 0.58f, 0.95f, 1f));

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

    private static void ShowcasePrefab(Transform parent, string label, string assetPath, Vector3 position, Vector3 euler, Vector3 scale)
    {
        CreatePedestal(parent, label + "_Pedestal", position, new Vector3(1.7f, 0.2f, 1.7f), new Color(0.24f, 0.25f, 0.27f, 1f));
        GameObject instance = TryInstantiatePrefab(assetPath, parent, label, position + new Vector3(0f, 0.25f, 0f), euler, scale);
        if (instance != null)
        {
            RepairShowcaseMaterials(instance, label);
            YQVisualStabilityDirector.StabilizeHierarchy(instance);
            FitShowcaseInstance(instance, position + new Vector3(0f, 0.25f, 0f), ShowcaseMaxFootprint, ShowcaseMaxHeight);
            instance.isStatic = true;
        }

        CreateLabel(parent, label, position + new Vector3(0f, 0.08f, -1.25f), 0.055f, Color.white);
    }

    private static GameObject TryInstantiatePrefab(string assetPath, Transform parent, string label, Vector3 position, Vector3 euler, Vector3 scale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefab == null)
            return CreateMissingMarker(parent, label, assetPath, position);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return CreateMissingMarker(parent, label, assetPath, position);

        instance.name = label;
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = position;
        instance.transform.localRotation = Quaternion.Euler(euler);
        instance.transform.localScale = scale;
        return instance;
    }

    private static GameObject CreateMissingMarker(Transform parent, string label, string assetPath, Vector3 position)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "MissingAsset_" + MakeSafeName(label);
        marker.transform.SetParent(parent, false);
        marker.transform.localPosition = position + new Vector3(0f, 0.5f, 0f);
        marker.transform.localScale = Vector3.one;
        SetRendererColor(marker, NeutralPlaceholderColor());
        CreateLabel(parent, "Missing: " + label, position + new Vector3(0f, 1.35f, 0f), 0.045f, new Color(0.82f, 0.86f, 0.90f, 1f));
        Debug.LogWarning("[YourQuest] Missing asset for test scene: " + assetPath);
        return marker;
    }

    private static void CreateAudioProbe(Transform parent, string label, string clipPath, Vector3 position)
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        probe.name = "AudioProbe_" + MakeSafeName(label);
        probe.transform.SetParent(parent, false);
        probe.transform.localPosition = position;
        probe.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
        SetRendererColor(probe, new Color(0.45f, 0.66f, 1f, 1f));

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        if (clip != null)
        {
            AudioSource source = probe.AddComponent<AudioSource>();
            source.clip = clip;
            source.spatialBlend = 1f;
            source.playOnAwake = false;
            source.volume = 0.7f;
            source.maxDistance = 12f;
        }
        else
        {
            Debug.LogWarning("[YourQuest] Missing audio clip for test scene: " + clipPath);
        }

        CreateLabel(parent, label, position + new Vector3(0f, 0.65f, 0f), 0.05f, Color.white);
    }

    private static void CreateAudioVfxStation(Transform parent, string label, string clipPath, string vfxPath, Vector3 position, Color color)
    {
        GameObject stationGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stationGo.name = "Station_" + MakeSafeName(label);
        stationGo.transform.SetParent(parent, false);
        stationGo.transform.localPosition = position;
        stationGo.transform.localScale = new Vector3(0.92f, 0.34f, 0.92f);
        SetRendererColor(stationGo, color);

        Transform spawn = CreateChild(stationGo.transform, "VFX_Spawn").transform;
        spawn.localPosition = new Vector3(0f, 1.1f, 0f);
        spawn.localRotation = Quaternion.identity;

        AudioClip clip = !string.IsNullOrWhiteSpace(clipPath) ? AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath) : null;
        if (clip == null && !string.IsNullOrWhiteSpace(clipPath))
            Debug.LogWarning("[YourQuest] Missing audio clip for test scene: " + clipPath);

        AudioSource source = stationGo.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = false;
        source.volume = 0.92f;
        source.spatialBlend = 0.35f;
        source.minDistance = 2.5f;
        source.maxDistance = 36f;
        source.rolloffMode = AudioRolloffMode.Linear;

        GameObject vfxPrefab = !string.IsNullOrWhiteSpace(vfxPath) ? AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath) : null;
        if (vfxPrefab == null && !string.IsNullOrWhiteSpace(vfxPath))
            Debug.LogWarning("[YourQuest] Missing VFX prefab for test scene: " + vfxPath);

        YQAssetTestStation station = stationGo.AddComponent<YQAssetTestStation>();
        station.stationName = label;
        station.audioSource = source;
        station.vfxPrefab = vfxPrefab;
        station.vfxSpawnPoint = spawn;
        station.vfxLifetime = 5.5f;
        string scaleLabel = label.ToLowerInvariant();
        station.vfxScale = scaleLabel.Contains("fireball") || scaleLabel.Contains("ice ball") || scaleLabel.Contains("missile")
            ? 0.65f
            : scaleLabel.Contains("beam") || scaleLabel.Contains("spray")
                ? 0.78f
                : scaleLabel.Contains("ring")
                    ? 0.86f
                    : 1f;
        station.interactRadius = 4.25f;
        station.statusRenderer = stationGo.GetComponent<Renderer>();
        station.idleColor = new Color(0.17f, 0.22f, 0.24f, 1f);
        station.readyColor = color;
        station.activeColor = new Color(0.95f, 1f, 0.68f, 1f);

        Light light = CreateChild(stationGo.transform, "Station_Glow").AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = 1.6f;
        light.range = 5f;
        light.shadows = LightShadows.None;

        CreateLabel(parent, label, position + new Vector3(0f, 0.68f, 0f), 0.048f, Color.white);
    }

    private static void CreateDialogueNpc(Transform parent, string displayName, string npcId, string factionId, string[] tags, Vector3 position, Color color, string persona)
    {
        GameObject npc = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        npc.name = "NPC_" + MakeSafeName(displayName);
        npc.transform.SetParent(parent, false);
        npc.transform.localPosition = position;
        npc.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
        SetRendererColor(npc, color);

        EntityInfo info = npc.AddComponent<EntityInfo>();
        info.entityId = npcId;
        info.displayName = displayName;
        info.level = 4;
        info.factionId = factionId;
        info.hostility = Hostility.Friendly;
        info.isNotable = true;
        info.tags = tags;

        NpcDialogueAgent agent = npc.AddComponent<NpcDialogueAgent>();
        agent.npcId = npcId;
        agent.npcName = displayName;
        agent.personaSummary = persona;
        agent.tagsOverride = new List<string>(tags);

        GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        eye.name = "Face_Marker";
        eye.transform.SetParent(npc.transform, false);
        eye.transform.localPosition = new Vector3(0f, 0.38f, -0.48f);
        eye.transform.localScale = new Vector3(0.16f, 0.08f, 0.04f);
        Object.DestroyImmediate(eye.GetComponent<Collider>());
        SetRendererColor(eye, new Color(1f, 0.94f, 0.72f, 1f));

        CreateLabel(parent, displayName, position + new Vector3(0f, 1.35f, 0f), 0.052f, Color.white);
    }

    private static void CreatePedestal(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pedestal.name = name;
        pedestal.transform.SetParent(parent, false);
        pedestal.transform.localPosition = position + new Vector3(0f, 0.1f, 0f);
        pedestal.transform.localScale = scale;
        SetRendererColor(pedestal, color);
    }

    private static void CreateFloorPad(Transform parent, string name, Vector3 center, Vector3 scale, Color color)
    {
        GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pad.name = name;
        pad.transform.SetParent(parent, false);
        pad.transform.localPosition = center + new Vector3(0f, 0.08f, 0f);
        pad.transform.localScale = new Vector3(scale.x, Mathf.Max(0.035f, scale.y), scale.z);
        Object.DestroyImmediate(pad.GetComponent<Collider>());
        Renderer renderer = pad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
        SetRendererSurface(pad, color, 7f);
    }

    private static void RepairShowcaseMaterials(GameObject root, string ownerLabel)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            bool particleMaterial = renderer is ParticleSystemRenderer;

            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material repaired = GetOrCreateUrpSafeMaterial(materials[slot], ownerLabel, renderer, slot, particleMaterial);
                if (repaired != null && repaired != materials[slot])
                {
                    materials[slot] = repaired;
                    changed = true;
                }
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    private static Material GetOrCreateUrpSafeMaterial(Material source, string ownerLabel, Renderer renderer, int slot, bool particleMaterial)
    {
        Material effectiveSource = ResolveEffectiveSourceMaterial(source, renderer, ownerLabel);
        if (!ShouldRepairMaterial(source, renderer, particleMaterial) && effectiveSource == source)
            return source;

        Shader shader = FindRepairShader(particleMaterial);
        if (shader == null)
            return source;

        string rendererName = renderer != null ? renderer.name : "Renderer";
        string sourcePath = effectiveSource != null ? AssetDatabase.GetAssetPath(effectiveSource) : string.Empty;
        string sourceGuid = !string.IsNullOrWhiteSpace(sourcePath) ? AssetDatabase.AssetPathToGUID(sourcePath) : string.Empty;
        string suffix = !string.IsNullOrWhiteSpace(sourceGuid)
            ? sourceGuid.Substring(0, Mathf.Min(10, sourceGuid.Length))
            : (effectiveSource != null ? Mathf.Abs(effectiveSource.GetInstanceID()).ToString("X") : "missing");
        string sourceName = effectiveSource != null ? effectiveSource.name : (source != null ? source.name : "MissingMaterial");
        string materialName = ShortSafeName(ownerLabel + "_" + rendererName + "_" + sourceName + "_" + slot + "_" + suffix, RepairedMaterialNameLimit);
        string materialPath = AssetTestRepairedMaterialFolder + "/" + materialName + ".mat";

        Material repaired = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (repaired == null)
        {
            repaired = effectiveSource != null ? new Material(effectiveSource) : new Material(shader);
            repaired.shader = shader;
            repaired.name = materialName;
            AssetDatabase.CreateAsset(repaired, materialPath);
        }
        else if (repaired.shader != shader)
        {
            repaired.shader = shader;
        }

        CopyMaterialSurface(effectiveSource, repaired, particleMaterial, renderer);
        EditorUtility.SetDirty(repaired);
        return repaired;
    }

    private static bool ShouldRepairMaterial(Material material, Renderer renderer, bool particleMaterial)
    {
        if (material == null || material.shader == null)
            return true;

        string shaderName = material.shader.name;
        if (string.IsNullOrWhiteSpace(shaderName))
            return true;
        if (shaderName.Contains("InternalErrorShader"))
            return true;
        if (shaderName.StartsWith("Universal Render Pipeline/") || shaderName.StartsWith("Shader Graphs/"))
            return LooksLikeBrokenGeneratedMaterial(material, renderer);
        if (shaderName.StartsWith("Particles/") || shaderName.StartsWith("Mobile/Particles/"))
            return false;
        return true;
    }

    private static Shader FindRepairShader(bool particleMaterial)
    {
        Shader shader = particleMaterial ? Shader.Find("Universal Render Pipeline/Particles/Unlit") : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null && particleMaterial)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        return shader;
    }

    private static void CopyMaterialSurface(Material source, Material target, bool particleMaterial, Renderer renderer, Texture fallbackBaseTexture = null, Texture fallbackNormalTexture = null)
    {
        if (target == null)
            return;

        Texture baseTexture = FindTexture(source, BaseTextureProperties) ?? fallbackBaseTexture;
        if (baseTexture != null)
        {
            SetTextureIfPresent(target, "_BaseMap", baseTexture);
            SetTextureIfPresent(target, "_MainTex", baseTexture);
        }

        Color baseColor = SanitizeCopiedMaterialColor(source, renderer, baseTexture, FindColor(source, fallbackBaseTexture != null ? new Color(0.86f, 0.82f, 0.72f, 1f) : Color.white));
        if (particleMaterial && baseTexture == null && IsUnreadablyDark(baseColor))
            baseColor = new Color(0.72f, 0.88f, 1f, 0.82f);
        SetColorIfPresent(target, "_BaseColor", baseColor);
        SetColorIfPresent(target, "_Color", baseColor);
        SetColorIfPresent(target, "_TintColor", baseColor);

        Texture normalTexture = FindTexture(source, NormalTextureProperties) ?? fallbackNormalTexture;
        if (normalTexture != null)
        {
            SetTextureIfPresent(target, "_BumpMap", normalTexture);
            target.EnableKeyword("_NORMALMAP");
        }

        CopyFloatIfPresent(source, target, "_Metallic", "_Metallic");
        CopyFloatIfPresent(source, target, "_Glossiness", "_Smoothness");
        CopyFloatIfPresent(source, target, "_Smoothness", "_Smoothness");
        ConfigureSurfaceMode(target, particleMaterial || baseColor.a < 0.98f);
    }

    private static bool IsUnreadablyDark(Color color)
    {
        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        return color.a > 0.05f && luminance < 0.18f && Mathf.Max(color.r, color.g, color.b) < 0.28f;
    }

    private static bool LooksLikeBrokenGeneratedMaterial(Material material, Renderer renderer)
    {
        if (material == null)
            return true;
        if (HasUsableBaseTexture(material))
            return false;

        string text = ToSearchText(material.name + " " + (renderer != null ? renderer.name : string.Empty));
        return text.Contains("missingmaterial") || text.Contains(" missing ") || text.Contains(" runtimeurp");
    }

    private static Material ResolveEffectiveSourceMaterial(Material source, Renderer renderer, string ownerLabel)
    {
        if (source != null && (!LooksLikeBrokenGeneratedMaterial(source, renderer) || HasUsableBaseTexture(source)))
            return source;

        Material fallback = FindFallbackMaterial(source, renderer, ownerLabel);
        return fallback != null ? fallback : source;
    }

    private static Material FindFallbackMaterial(Material source, Renderer renderer, string ownerLabel)
    {
        string searchText = BuildFallbackSearchText(source, renderer, ownerLabel);
        if (string.IsNullOrWhiteSpace(searchText))
            return null;

        if (FallbackMaterialCache.TryGetValue(searchText, out Material cached))
            return cached;

        EnsureMaterialCandidates();
        string[] tokens = ExtractFallbackTokens(searchText);
        Material best = null;
        int bestScore = 0;
        if (MaterialCandidates != null)
        {
            for (int i = 0; i < MaterialCandidates.Length; i++)
            {
                MaterialCandidate candidate = MaterialCandidates[i];
                if (candidate == null || candidate.material == null)
                    continue;

                int score = ScoreMaterialCandidate(candidate, tokens, searchText);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate.material;
                }
            }
        }

        if (bestScore < 80)
            best = null;

        FallbackMaterialCache[searchText] = best;
        return best;
    }

    private static void EnsureMaterialCandidates()
    {
        if (MaterialCandidates != null)
            return;

        string[] guids = AssetDatabase.FindAssets("t:Material");
        List<MaterialCandidate> candidates = new List<MaterialCandidate>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrWhiteSpace(path))
                continue;

            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.Contains("/AssetTest/Repaired/"))
                continue;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                continue;

            bool hasTexture = HasUsableBaseTexture(material);
            bool hasUsefulColor = HasUsefulColor(material);
            if (!hasTexture && !hasUsefulColor)
                continue;

            candidates.Add(new MaterialCandidate
            {
                material = material,
                text = ToSearchText(material.name + " " + normalizedPath),
                hasTexture = hasTexture,
                hasUsefulColor = hasUsefulColor
            });
        }

        MaterialCandidates = candidates.ToArray();
    }

    private static string BuildFallbackSearchText(Material source, Renderer renderer, string ownerLabel)
    {
        string text = ownerLabel + " " + (source != null ? source.name : string.Empty);
        if (renderer != null)
        {
            text += " " + renderer.name;
            Transform current = renderer.transform;
            int depth = 0;
            while (current != null && depth < 8)
            {
                text += " " + current.name;
                current = current.parent;
                depth++;
            }
        }

        return ToSearchText(text);
    }

    private static string[] ExtractFallbackTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new string[0];

        string[] raw = text.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        List<string> tokens = new List<string>(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            string token = raw[i];
            if (token.Length < 3 || ContainsAny(token, FallbackSkipTokens))
                continue;
            if (!tokens.Contains(token))
                tokens.Add(token);
        }

        return tokens.ToArray();
    }

    private static int ScoreMaterialCandidate(MaterialCandidate candidate, string[] tokens, string searchText)
    {
        int score = candidate.hasTexture ? 35 : 0;
        if (candidate.hasUsefulColor)
            score += 15;

        string familyToken = FindNumberedFamilyToken(tokens);
        if (!string.IsNullOrWhiteSpace(familyToken))
        {
            if (candidate.text.Contains(familyToken))
                score += 220;
            else if (ContainsConflictingNumberedFamily(candidate.text, familyToken))
                score -= 220;
            else
                score -= 60;
        }

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!candidate.text.Contains(token))
                continue;

            score += token.Length >= 7 ? 70 : 34;
            if (ContainsDigit(token))
                score += 28;
        }

        if (searchText.Contains(ToSearchText(candidate.material.name)))
            score += 120;

        return score;
    }

    private static string FindNumberedFamilyToken(string[] tokens)
    {
        if (tokens == null)
            return null;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (!ContainsDigit(token))
                continue;

            for (int j = 0; j < NumberedFamilyPrefixes.Length; j++)
            {
                if (token.StartsWith(NumberedFamilyPrefixes[j]))
                    return token;
            }
        }

        return null;
    }

    private static bool ContainsConflictingNumberedFamily(string candidateText, string familyToken)
    {
        if (string.IsNullOrWhiteSpace(candidateText) || string.IsNullOrWhiteSpace(familyToken))
            return false;

        string prefix = GetLeadingLetters(familyToken);
        if (string.IsNullOrWhiteSpace(prefix) || !candidateText.Contains(prefix))
            return false;

        return !candidateText.Contains(familyToken);
    }

    private static string GetLeadingLetters(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        int length = 0;
        while (length < value.Length && char.IsLetter(value[length]))
            length++;

        return length > 0 ? value.Substring(0, length) : string.Empty;
    }

    private static bool HasUsableBaseTexture(Material material)
    {
        return FindTexture(material, BaseTextureProperties) != null;
    }

    private static bool HasUsefulColor(Material material)
    {
        if (material == null)
            return false;

        Color color = FindColor(material, Color.white);
        return !IsNearWhite(color) && !IsUnreadablyDark(color);
    }

    private static bool IsNearWhite(Color color)
    {
        return color.a > 0.05f &&
               color.r > 0.86f &&
               color.g > 0.86f &&
               color.b > 0.86f &&
               Mathf.Abs(color.r - color.g) < 0.12f &&
               Mathf.Abs(color.g - color.b) < 0.12f;
    }

    private static bool ContainsDigit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsDigit(value[i]))
                return true;
        }

        return false;
    }

    private static Texture FindTexture(Material material, string[] propertyNames)
    {
        if (material == null || propertyNames == null)
            return null;

        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            if (!material.HasProperty(propertyName))
                continue;

            Texture texture = material.GetTexture(propertyName);
            if (IsTexture2D(texture))
                return texture;
        }

        try
        {
            if (material.HasProperty("_MainTex"))
            {
                Texture mainTexture = material.mainTexture;
                if (IsTexture2D(mainTexture))
                    return mainTexture;
            }
        }
        catch
        {
        }

        return FindBestMaterialTexture(material, propertyNames == NormalTextureProperties);
    }

    private static Texture FindBestMaterialTexture(Material material, bool normalTexture)
    {
        if (material == null)
            return null;

        string[] propertyNames;
        try
        {
            propertyNames = material.GetTexturePropertyNames();
        }
        catch
        {
            return null;
        }

        Texture bestTexture = null;
        int bestScore = normalTexture ? 30 : 20;
        for (int i = 0; i < propertyNames.Length; i++)
        {
            string propertyName = propertyNames[i];
            Texture texture = material.GetTexture(propertyName);
            if (!IsTexture2D(texture))
                continue;

            int score = ScoreTextureName(propertyName + " " + texture.name, normalTexture);
            if (score > bestScore)
            {
                bestScore = score;
                bestTexture = texture;
            }
        }

        return bestTexture;
    }

    private static int ScoreTextureName(string name, bool normalTexture)
    {
        string text = ToSearchText(name);
        if (normalTexture)
            return ContainsAny(text, NormalTextureNameHints) ? 120 : -50;

        if (ContainsAny(text, NonBaseTextureNameHints))
            return -50;

        int score = 0;
        if (ContainsAny(text, BaseTextureNameHints))
            score += 120;
        if (text.Contains("texture"))
            score += 10;
        return score;
    }

    private static Color FindColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;

        for (int i = 0; i < BaseColorProperties.Length; i++)
        {
            string propertyName = BaseColorProperties[i];
            if (!material.HasProperty(propertyName))
                continue;

            try
            {
                return material.GetColor(propertyName);
            }
            catch
            {
            }
        }

        return fallback;
    }

    private static bool ContainsAny(string text, string[] hints)
    {
        if (string.IsNullOrWhiteSpace(text) || hints == null)
            return false;

        for (int i = 0; i < hints.Length; i++)
        {
            if (text.Contains(hints[i]))
                return true;
        }

        return false;
    }

    private static string ToSearchText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = value.ToLowerInvariant().Replace('\\', '/').ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = ' ';
        }

        return new string(chars);
    }

    private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
    {
        if (material != null && IsTexture2D(texture) && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
    }

    private static void SetColorIfPresent(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    private static void SetFloatIfPresent(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void CopyFloatIfPresent(Material source, Material target, string sourcePropertyName, string targetPropertyName)
    {
        if (source != null && target != null && source.HasProperty(sourcePropertyName) && target.HasProperty(targetPropertyName))
            target.SetFloat(targetPropertyName, source.GetFloat(sourcePropertyName));
    }

    private static void ConfigureSurfaceMode(Material material, bool transparent)
    {
        if (material == null)
            return;

        if (!transparent)
        {
            SetFloatIfPresent(material, "_Surface", 0f);
            material.renderQueue = -1;
            material.SetOverrideTag("RenderType", string.Empty);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            return;
        }

        SetFloatIfPresent(material, "_Surface", 1f);
        SetFloatIfPresent(material, "_Blend", 0f);
        SetFloatIfPresent(material, "_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        SetFloatIfPresent(material, "_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        SetFloatIfPresent(material, "_ZWrite", 0f);
        material.renderQueue = 3000;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
    }

    private static void FitShowcaseInstance(GameObject instance, Vector3 anchor, float maxFootprint, float maxHeight)
    {
        if (!TryCalculateRendererBounds(instance, out Bounds bounds))
            return;

        float horizontal = Mathf.Max(bounds.size.x, bounds.size.z);
        float factor = 1f;
        if (horizontal > maxFootprint && horizontal > 0.01f)
            factor = Mathf.Min(factor, maxFootprint / horizontal);
        if (bounds.size.y > maxHeight && bounds.size.y > 0.01f)
            factor = Mathf.Min(factor, maxHeight / bounds.size.y);

        if (factor < 0.999f)
        {
            instance.transform.localScale *= factor;
            if (!TryCalculateRendererBounds(instance, out bounds))
                return;
        }

        Vector3 offset = new Vector3(anchor.x - bounds.center.x, anchor.y - bounds.min.y, anchor.z - bounds.center.z);
        instance.transform.position += offset;
    }

    private static bool TryCalculateRendererBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return hasBounds;
    }

    private static void CreatePointLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
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
        region.tags = new List<string>(tags);
    }

    private static void CreateLabel(Transform parent, string text, Vector3 position, float characterSize, Color color)
    {
        GameObject go = new GameObject("Label_" + MakeSafeName(text));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(65f, 0f, 0f);

        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = text;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.fontSize = 48;
        mesh.characterSize = characterSize;
        mesh.color = color;

        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Material labelMaterial = new Material(renderer.sharedMaterial);
            labelMaterial.name = go.name + "_Readable";
            SetColorIfPresent(labelMaterial, "_Color", color);
            SetColorIfPresent(labelMaterial, "_BaseColor", color);
            SetColorIfPresent(labelMaterial, "_TintColor", color);
            renderer.sharedMaterial = labelMaterial;
        }
    }

    private static string MakeSafeName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "Unnamed";

        string safe = raw.Trim()
            .Replace(" ", "_")
            .Replace("/", "_")
            .Replace("\\", "_")
            .Replace(":", "_")
            .Replace("&", "And")
            .Replace("(", string.Empty)
            .Replace(")", string.Empty)
            .Replace(",", string.Empty);
        return safe;
    }

    private static string ShortSafeName(string raw, int maxLength)
    {
        string safe = MakeSafeName(raw);
        if (safe.Length <= maxLength)
            return safe;
        return safe.Substring(0, Mathf.Max(12, maxLength));
    }

    private static void SetRendererColor(GameObject go, Color color)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null)
            return;

        renderer.sharedMaterial = GetOrCreateSceneMaterial(go.name, color, renderer.sharedMaterial);
    }

    private static void SetRendererSurface(GameObject go, Color color, float tileScale)
    {
        Renderer renderer = go.GetComponent<Renderer>();
        if (renderer == null || renderer.sharedMaterial == null)
            return;

        renderer.sharedMaterial = GetOrCreateSceneMaterial(go.name, color, renderer.sharedMaterial);
        Texture2D texture = GetOrCreateTileTexture(go.name, color);
        ApplyTextureIfPresent(renderer.sharedMaterial, "_BaseMap", texture, tileScale);
        ApplyTextureIfPresent(renderer.sharedMaterial, "_MainTex", texture, tileScale);
        EditorUtility.SetDirty(renderer.sharedMaterial);
    }

    private static Material GetOrCreateSceneMaterial(string sourceName, Color color, Material source)
    {
        color = SanitizePlaceholderColor(sourceName, color);
        string colorKey = ColorUtility.ToHtmlStringRGB(color);
        string materialName = MakeSafeName(sourceName) + "_" + colorKey;
        string materialPath = AssetTestMaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null && source != null)
                shader = source.shader;
            if (shader == null)
                shader = Shader.Find("Standard");

            material = source != null ? new Material(source) : new Material(shader);
            material.name = materialName;
            AssetDatabase.CreateAsset(material, materialPath);
        }

        material.color = color;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Texture2D GetOrCreateTileTexture(string sourceName, Color color)
    {
        string textureName = MakeSafeName(sourceName) + "_Tile_" + ColorUtility.ToHtmlStringRGB(color);
        string texturePath = AssetTestMaterialFolder + "/" + textureName + ".asset";
        const int size = 64;
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
        {
            texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            texture.name = textureName;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            AssetDatabase.CreateAsset(texture, texturePath);
        }
        else
        {
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
        }

        color = SanitizePlaceholderColor(sourceName, color);
        Color light = Color.Lerp(color, Color.white, 0.16f);
        Color dark = Color.Lerp(color, new Color(0.18f, 0.19f, 0.20f, color.a), 0.20f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool checker = ((x / 16) + (y / 16)) % 2 == 0;
                bool grout = x % 16 == 0 || y % 16 == 0;
                Color pixel = checker ? light : dark;
                if (grout)
                    pixel = Color.Lerp(pixel, dark, 0.35f);
                texture.SetPixel(x, y, pixel);
            }
        }

        texture.Apply(false, true);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static Color NeutralPlaceholderColor()
    {
        return new Color(0.46f, 0.48f, 0.48f, 1f);
    }

    private static Color SanitizePlaceholderColor(string name, Color color)
    {
        string text = (name ?? string.Empty).ToLowerInvariant();
        if ((text.Contains("missing") || text.Contains("placeholder") || text.Contains("fallback")) && LooksLikeAlertRed(color))
            return NeutralPlaceholderColor();

        return color;
    }

    private static Color SanitizeCopiedMaterialColor(Material source, Renderer renderer, Texture texture, Color color)
    {
        if (!LooksLikeAlertRed(color))
            return color;

        string text = ToSearchText(
            (source != null ? source.name : string.Empty) + " " +
            (texture != null ? texture.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty));
        if (text.Contains("leaf") || text.Contains("leaves") || text.Contains("branch") || text.Contains("grass") ||
            text.Contains("bush") || text.Contains("flower") || text.Contains("plant") || text.Contains("billboard") ||
            text.Contains("foliage"))
            return new Color(0.22f, 0.42f, 0.20f, color.a);
        if (text.Contains("missing") || text.Contains("placeholder") || text.Contains("fallback") || text.Contains("proxy") ||
            text.Contains("runtimeurp") || text.Contains("defaultdirty"))
            return NeutralPlaceholderColor();

        return color;
    }

    private static bool LooksLikeAlertRed(Color color)
    {
        return color.r > 0.48f && color.r > color.g * 1.55f && color.r > color.b * 1.55f;
    }

    private static void ApplyTextureIfPresent(Material material, string propertyName, Texture texture, float tileScale)
    {
        if (material == null || !IsTexture2D(texture) || !material.HasProperty(propertyName))
            return;

        material.SetTexture(propertyName, texture);
        material.SetTextureScale(propertyName, new Vector2(tileScale, tileScale));
    }

    private static bool IsTexture2D(Texture texture)
    {
        return texture is Texture2D;
    }
}
#endif
