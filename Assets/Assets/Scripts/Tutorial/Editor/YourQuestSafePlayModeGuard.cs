// Assets/Assets/Scripts/Tutorial/Editor/YourQuestSafePlayModeGuard.cs
#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class YourQuestSafePlayModeGuard
{
    private const string SafeScenePath = "Assets/Assets/Scenes/YourQuest_PlaySafe.unity";
    private const string RebuildTempScenePath = "Assets/Assets/Scenes/YourQuest_PlaySafe_RebuildTemp.unity";
    private const string CuratedSceneRootName = "YQ_PlaySafe_CuratedScene_v13";
    private const long CuratedSceneMinimumBytes = 50000;
    private const bool AutoRebuildExistingSafeScene = false;
    private const string RebuildRequestPath = "Assets/Assets/EditorBuildRequests/RebuildPlaySafeScene.request";
    private const string GeneratedMaterialFolder = "Assets/Assets/Materials/PlaySafe";
    private const string GeneratedTerrainFolder = "Assets/Assets/Terrain/PlaySafe";

    private const string NordicTreeA = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Tree.prefab";
    private const string NordicTreeB = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_TreeNeedles01.prefab";
    private const string NordicGrass = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Grass04.prefab";
    private const string NordicBush = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Bush.prefab";
    private const string VictorianFloor = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Floor.prefab";
    private const string VictorianWall = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Wall_Standard.prefab";
    private const string VictorianWallWindow = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Wall_WindowsStandard.prefab";
    private const string VictorianDoor = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_DoorCarved.prefab";
    private const string VictorianBookshelf = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Bookshelf_BIG.prefab";
    private const string VictorianTable = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_WoodenStudyTable.prefab";
    private const string VictorianFireplace = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Fireplace.prefab";
    private const string VictorianBed = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Bed.prefab";
    private const string AsianBuilding = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Building01.prefab";
    private const string AsianPavilion = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_MiniPavilionPlatform.prefab";
    private const string AsianStairs = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_StairSet_1.prefab";
    private const string AsianTile = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_FloorTile_1.prefab";
    private const string AsianTree = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Tree_03.prefab";
    private const string AsianDragon = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_ChineseDragon_1.prefab";
    private const string PersepolisFloor = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_FloorSetCustom_Base.prefab";
    private const string PersepolisWallArc = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_WallSideArc.prefab";
    private const string PersepolisGrassPatch = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_Grasspatch_1.prefab";
    private const string TerrainThinTree = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ThinTree.prefab";
    private const string TerrainSycamore = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Sycamore.prefab";
    private const string TerrainPineA = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeA.prefab";
    private const string TerrainPineB = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeB.prefab";
    private const string TerrainAlder = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Alder.prefab";
    private const string TerrainMimosa = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Mimosa.prefab";
    private const string TerrainRock = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Rocks/RockMesh.prefab";
    private const string NordicFern = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Fern.prefab";
    private const string NordicThatchGrass = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_ThatchGrass03.prefab";
    private const string NordicRockA = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_RockSmall01.prefab";
    private const string NordicRockB = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_RockSmall02.prefab";
    private const string WesternRockA = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Rock06.prefab";
    private const string WesternRockB = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Rock07.prefab";
    private const string ChestSimple = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestSimpleSmall.prefab";
    private const string ChestOrnate = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestOrnateMedium.prefab";
    private const string MimicSimple = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleSmall.prefab";
    private const string HumanMale = "Assets/Magic Pig Games (Infinity PBR)/Characters/Human - Humans/_Prefabs/Characters/Human Male (v4.1.1).prefab";
    private const string PlantMonster = "Assets/Magic Pig Games (Infinity PBR)/Characters/Plant Monster/_Prefabs/PlantMonster.prefab";
    private const string MushroomMonster = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mushroom Monster/_Prefabs/Mushroom_v2.prefab";
    private const string DragonMonster = "Assets/Magic Pig Games (Infinity PBR)/Characters/Dragons/_Prefabs/Dragon.prefab";
    private const string DemonMonster = "Assets/Magic Pig Games (Infinity PBR)/Characters/Demons/_Prefabs/Demons.prefab";
    private const string Sword = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Swords/Sword004/Prefab/Sword004.prefab";
    private const string Staff = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Staffs/Staff003/Prefab/Staff003.prefab";
    private const string Helmet = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Helmets/Helmet003/Prefab/Helmet003.prefab";
    private const string Shield = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Armor Objects/Shields/Shield004/Prefab/Shield004.prefab";
    private const string Axe = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Axes/Axe004/Prefab/Axe004.prefab";
    private const string Spear = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Weapon Objects/Spears/Spear003/Prefab/Spear003.prefab";
    private const string Ring = "Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor/Accessories/_Prefabs/Rings/Ring_1 1 New.prefab";
    private const string Lockpick = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/_Prefabs/LockpickA.prefab";
    private const string Lock = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/_Prefabs/Lock1.prefab";
    private const string FireVfx = "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Fire Light.prefab";
    private const string HealVfx = "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Magic Spells/Magic Power Heal.prefab";
    private const string FireballVfx = "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Fireball 2 Small.prefab";
    private const string ElectricVfx = "Assets/Magic Pig Games (Infinity PBR)/Shared Files/Magic Spells & Particles/Particles/Particle Electric Explosion.prefab";
    private const string SwordAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Battle/Sword/Sword_On_Wood/Impact/Sword_On_Wood_Impact_1.wav";
    private const string ElectricAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Electric/Explosion/Electric_Explosion_1_S.wav";
    private const string WaterAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Water/Hit/Water_Hit_1_S.wav";
    private const string FireLoopAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Fire/Loop/Fire_Loop_Small_S.wav";
    private const string AmbientHumAudio = "Assets/Magic Pig Games (Infinity PBR)/Audio/Battle Sound Library/Magic (Stereo)/Generic/Humming & Pulsing/Humming_Loop_4_S.wav";
    private const string AmbientWindAudio = "";
    private const string TerrainGrassDiffuse = "Assets/ADG_Textures/ground_vol1/ground3/ground3_Diffuse.tga";
    private const string TerrainGrassNormal = "Assets/ADG_Textures/ground_vol1/ground3/ground3_Normal.tga";
    private const string TerrainTrailDiffuse = "Assets/ADG_Textures/ground_vol1/ground6/ground6_Diffuse.tga";
    private const string TerrainTrailNormal = "Assets/ADG_Textures/ground_vol1/ground6/ground6_Normal.tga";
    private const string TerrainStoneDiffuse = "Assets/ADG_Textures/ground_vol1/ground10/ground10_Diffuse.tga";
    private const string TerrainStoneNormal = "Assets/ADG_Textures/ground_vol1/ground10/ground10_Normal.tga";
    private const string TerrainDarkSoilDiffuse = "Assets/ADG_Textures/ground_vol1/ground8/ground8_Diffuse.tga";
    private const string TerrainDarkSoilNormal = "Assets/ADG_Textures/ground_vol1/ground8/ground8_Normal.tga";
    private const string TerrainWetStoneDiffuse = "Assets/ADG_Textures/ground_vol1/ground12/ground12_Diffuse.tga";
    private const string TerrainWetStoneNormal = "Assets/ADG_Textures/ground_vol1/ground12/ground12_Normal.tga";
    private const string GrassBillboardTexture = "Assets/Grass And Flowers Pack 1/Grass/Grass 1.png";
    private const string GrassBillboardTextureB = "Assets/Grass And Flowers Pack 1/Grass/Grass 3.png";
    private const string FlowerBillboardTexture = "Assets/Grass And Flowers Pack 1/Flower/Grass Flower 4.png";
    private const string RockGroundDiffuse = "Assets/Grass And Flowers Pack 1/Ground Textures/RockSharp 1_D.png";
    private const string RockGroundNormal = "Assets/Grass And Flowers Pack 1/Ground Textures/RockSharp 1_N.png";
    private const float PlaySafeTerrainHalfSize = 148f;
    private const float PlaySafeTerrainSize = PlaySafeTerrainHalfSize * 2f;
    private const float PlaySafeTerrainHeight = 34f;
    private const float PlaySafeTerrainBaseY = -0.22f;

    private static readonly string[] BaseTextureProperties = { "_BaseMap", "_MainTex", "_Albedo", "_BaseColorMap", "_DiffuseMap", "_ColorMap", "_BaseColorTexture", "_ColorTexture", "_MainTexture", "_Texture2D" };
    private static readonly string[] BaseTextureNameHints =
    {
        "albedo", "basecolor", "base color", "base map", "diffuse", "color", "col", "albedoopacity",
        "leaf", "leaves", "bark", "trunk", "wood", "branch", "grass", "plant",
        "skin", "body", "head", "face", "hair", "eye", "eyes", "horn", "teeth", "claw", "wing", "scale", "scales",
        "cloth", "fabric", "robe", "armor", "boot", "glove", "gauntlet",
        "lock", "pick", "chest", "mimic", "stone", "wall", "floor", "tile"
    };
    private static readonly string[] NonBaseTextureNameHints = { "normal", "bump", "nrm", "rough", "metal", "metallic", "smooth", "ambientocclusion", "occlusion", " ao", "height", "mask", "emiss", "spec", "orm" };
    private static readonly string[] NearbyTextureFolderNames = { "Textures & Materials", "Textures", "Texture", "Texture & Materials", "Textures and Materials", "Texture and Materials" };
    private static readonly Dictionary<string, Texture> s_nearbyBaseTextureCache = new Dictionary<string, Texture>();

    static YourQuestSafePlayModeGuard()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.update -= ProcessRebuildRequestWhenIdle;
        EditorApplication.update += ProcessRebuildRequestWhenIdle;
        ScheduleConfigureWhenIdle();
    }

    // note: Lightweight scene controls are recurring Play Mode QA tools, grouped away from production content authoring.
    [MenuItem("Tools/YourQuest/Testing/Play Mode/Use Lightweight Start Scene")]
    public static void ConfigureSafePlayScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            ScheduleConfigureWhenIdle();
            return;
        }

        EnsureSafeSceneAsset();
        SceneAsset safeScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(SafeScenePath);
        if (safeScene != null)
            EditorSceneManager.playModeStartScene = safeScene;
    }

    private static void ScheduleConfigureWhenIdle()
    {
        EditorApplication.update -= ConfigureWhenEditorIsIdle;
        EditorApplication.update += ConfigureWhenEditorIsIdle;
    }

    private static void ConfigureWhenEditorIsIdle()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        EditorApplication.update -= ConfigureWhenEditorIsIdle;
        ConfigureSafePlayScene();
    }

    [MenuItem("Tools/YourQuest/Testing/Play Mode/Rebuild Lightweight Start Scene")]
    public static void RebuildLightweightPlaySceneMenu()
    {
        RebuildLightweightPlayScene(true, true);
        ConfigureSafePlayScene();
    }

    public static void RequestOneShotPlaySceneRebuild()
    {
        // note: One-shot request creation is hidden from menus; direct rebuild remains the supported editor action.
        EnsureFolder("Assets", "Assets");
        EnsureFolder("Assets/Assets", "EditorBuildRequests");
        File.WriteAllText(RebuildRequestPath, "rebuild-safe-play-scene");
        AssetDatabase.ImportAsset(RebuildRequestPath);
    }

    public static void RebuildLightweightPlaySceneForBatch()
    {
        RebuildLightweightPlayScene(false, false);
    }

    private static void ProcessRebuildRequestWhenIdle()
    {
        if (!File.Exists(RebuildRequestPath))
            return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        try
        {
            File.Delete(RebuildRequestPath);
            string metaPath = RebuildRequestPath + ".meta";
            if (File.Exists(metaPath))
                File.Delete(metaPath);
            AssetDatabase.Refresh();
            RebuildLightweightPlayScene(false, false);
            ConfigureSafePlayScene();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("Tools/YourQuest/Testing/Play Mode/Clear Start Scene Override")]
    public static void ClearSafePlayScene()
    {
        EditorSceneManager.playModeStartScene = null;
    }

    private static void EnsureSafeSceneAsset()
    {
        if (!NeedsSafeSceneRebuild())
            return;

        if (File.Exists(SafeScenePath) && !AutoRebuildExistingSafeScene)
            return;

        RebuildLightweightPlayScene(false, false);
    }

    private static bool NeedsSafeSceneRebuild()
    {
        if (!File.Exists(SafeScenePath))
            return true;

        FileInfo sceneInfo = new FileInfo(SafeScenePath);
        if (sceneInfo.Length < CuratedSceneMinimumBytes)
            return true;

        string sceneText = File.ReadAllText(SafeScenePath);
        return !sceneText.Contains(CuratedSceneRootName);
    }

    private static void RebuildLightweightPlayScene(bool openWhenDone, bool showDialog)
    {
        EnsureFolder("Assets", "Assets");
        EnsureFolder("Assets/Assets", "Scenes");
        EnsureGeneratedAssetFolders();

        bool reopenGeneratedScene = IsSafeSceneOpen();
        Scene previousScene = SceneManager.GetActiveScene();
        bool replaceScratchScene = HasGeneratedUntitledSceneBlocker();
        Scene safeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, replaceScratchScene ? NewSceneMode.Single : NewSceneMode.Additive);
        safeScene.name = "YourQuest_PlaySafe";
        SceneManager.SetActiveScene(safeScene);
        reopenGeneratedScene |= replaceScratchScene;

        bool assetEditingStarted = false;
        GameObject root = null;
        try
        {
            AssetDatabase.StartAssetEditing();
            assetEditingStarted = true;

            ConfigureRenderSettings();

            root = new GameObject(CuratedSceneRootName);
            root.isStatic = true;

            BuildPreviewCamera(root.transform);
            BuildLighting(root.transform);
            BuildStableGround(root.transform);
            BuildSpawnHut(root.transform);
            BuildForestDressing(root.transform);
            BuildRegionSamples(root.transform);
            BuildLootAndEquipment(root.transform);
            BuildCreatureAndVfxSamples(root.transform);
            BuildAmbientAudio(root.transform);
        }
        finally
        {
            if (assetEditingStarted)
                AssetDatabase.StopAssetEditing();
        }

        BindSavedSceneAudioClips(root != null ? root.transform : null);
        EditorSceneManager.MarkSceneDirty(safeScene);
        bool saved = EditorSceneManager.SaveScene(safeScene, RebuildTempScenePath, true);
        if (saved)
        {
            File.Copy(RebuildTempScenePath, SafeScenePath, true);
            AssetDatabase.ImportAsset(SafeScenePath, ImportAssetOptions.ForceUpdate);
        }
        AssetDatabase.SaveAssets();
        Debug.Log(saved
            ? "[YourQuest] Rebuilt and saved " + SafeScenePath + " with curated imported assets."
            : "[YourQuest] Failed to save " + SafeScenePath + ". Check whether another copy of the scene is open.");

        if (openWhenDone || reopenGeneratedScene)
        {
            EditorSceneManager.OpenScene(SafeScenePath, OpenSceneMode.Single);
        }
        else
        {
            EditorSceneManager.CloseScene(safeScene, true);
            if (previousScene.IsValid() && previousScene.isLoaded)
                SceneManager.SetActiveScene(previousScene);
        }

        CleanupTempSceneAsset();

        if (showDialog)
            EditorUtility.DisplayDialog("YourQuest", "Lightweight play scene rebuilt and saved with curated imported assets.", "OK");
    }

    private static bool IsSafeSceneOpen()
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            string scenePath = scene.path.Replace('\\', '/');
            if (string.Equals(scenePath, SafeScenePath, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool HasGeneratedUntitledSceneBlocker()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!string.IsNullOrWhiteSpace(scene.path) || !scene.isDirty)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject root = roots[rootIndex];
                if (root != null && root.name.StartsWith("YQ_PlaySafe_CuratedScene", System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static void CleanupTempSceneAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(RebuildTempScenePath) != null)
            AssetDatabase.DeleteAsset(RebuildTempScenePath);
        else if (File.Exists(RebuildTempScenePath))
            File.Delete(RebuildTempScenePath);

        string metaPath = RebuildTempScenePath + ".meta";
        if (File.Exists(metaPath))
            File.Delete(metaPath);
    }

    private static void ConfigureRenderSettings()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.58f, 0.61f, 0.58f, 1f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.20f, 0.25f, 0.24f, 1f);
        RenderSettings.fogDensity = 0.006f;
    }

    private static void BuildPreviewCamera(Transform root)
    {
        GameObject cameraGo = new GameObject("EditorPreviewCamera");
        cameraGo.transform.SetParent(root, false);
        cameraGo.transform.position = new Vector3(0f, 8.5f, -14f);
        cameraGo.transform.rotation = Quaternion.Euler(28f, 0f, 0f);
        Camera camera = cameraGo.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.nearClipPlane = 0.08f;
        camera.farClipPlane = 230f;
        camera.fieldOfView = 62f;
        camera.enabled = false;
    }

    private static void BuildLighting(Transform root)
    {
        GameObject sunGo = new GameObject("Sun_Key_LightweightShadow");
        sunGo.transform.SetParent(root, false);
        sunGo.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
        Light sun = sunGo.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.color = new Color(1f, 0.91f, 0.76f, 1f);
        sun.intensity = 1.25f;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.28f;
        sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.Low;

        CreateGlowLight(root, "Hut_WarmFill_NoShadows", new Vector3(0f, 2.4f, 0f), new Color(1f, 0.62f, 0.36f, 1f), 1.8f, 8f);
        CreateGlowLight(root, "VfxWing_CoolFill_NoShadows", new Vector3(-12f, 2.2f, 12f), new Color(0.42f, 0.82f, 1f, 1f), 1.2f, 9f);
    }

    private static void BuildStableGround(Transform root)
    {
        BuildTerrainTestbed(root);

        Transform ground = CreateGroup(root, "01_StableFloorAndPaths");
        CreateTexturedBox(ground, "SpawnClearing_WalkablePad", new Vector3(0f, 0.035f, 0f), new Vector3(18f, 0.07f, 18f), TerrainGrassDiffuse, TerrainGrassNormal, new Color(0.18f, 0.29f, 0.21f, 1f), true, false, 6f);
        CreateTexturedBox(ground, "NorthPath_PackedDirt_RaisedNoFlicker", new Vector3(0f, 0.025f, 38f), new Vector3(7.5f, 0.045f, 78f), TerrainTrailDiffuse, TerrainTrailNormal, new Color(0.31f, 0.27f, 0.20f, 1f), false, false, 12f);
        CreateTexturedBox(ground, "SouthPath_PackedDirt_RaisedNoFlicker", new Vector3(0f, 0.028f, -40f), new Vector3(7.5f, 0.045f, 78f), TerrainTrailDiffuse, TerrainTrailNormal, new Color(0.31f, 0.27f, 0.20f, 1f), false, false, 12f);
        CreateTexturedBox(ground, "EastPath_RockyDirt_RaisedNoFlicker", new Vector3(40f, 0.031f, 0f), new Vector3(78f, 0.045f, 7.5f), TerrainStoneDiffuse, TerrainStoneNormal, new Color(0.34f, 0.31f, 0.27f, 1f), false, false, 12f);
        CreateTexturedBox(ground, "WestPath_WetStone_RaisedNoFlicker", new Vector3(-40f, 0.034f, 0f), new Vector3(78f, 0.045f, 7.5f), TerrainWetStoneDiffuse, TerrainWetStoneNormal, new Color(0.25f, 0.30f, 0.30f, 1f), false, false, 12f);
        CreateBox(ground, "SpawnInteriorRug", new Vector3(0f, 0.065f, 0f), new Vector3(4.2f, 0.035f, 3.2f), new Color(0.42f, 0.18f, 0.16f, 1f), false, false);
    }

    private static void BuildTerrainTestbed(Transform root)
    {
        TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(GeneratedTerrainFolder + "/YQ_PlaySafe_TerrainData.asset");
        if (terrainData == null)
        {
            terrainData = new TerrainData();
            AssetDatabase.CreateAsset(terrainData, GeneratedTerrainFolder + "/YQ_PlaySafe_TerrainData.asset");
        }

        const int heightResolution = 129;
        const int alphaResolution = 128;
        terrainData.heightmapResolution = heightResolution;
        terrainData.alphamapResolution = alphaResolution;
        terrainData.size = new Vector3(PlaySafeTerrainSize, PlaySafeTerrainHeight, PlaySafeTerrainSize);
        terrainData.terrainLayers = new[]
        {
            GetOrCreateTerrainLayer("ForestMoss", TerrainGrassDiffuse, TerrainGrassNormal, new Color(0.17f, 0.30f, 0.18f, 1f), new Color(0.30f, 0.44f, 0.25f, 1f), 8f),
            GetOrCreateTerrainLayer("PackedTrail", TerrainTrailDiffuse, TerrainTrailNormal, new Color(0.27f, 0.24f, 0.19f, 1f), new Color(0.42f, 0.36f, 0.27f, 1f), 6f),
            GetOrCreateTerrainLayer("RockyGround", TerrainStoneDiffuse, TerrainStoneNormal, new Color(0.39f, 0.36f, 0.31f, 1f), new Color(0.56f, 0.52f, 0.43f, 1f), 8f),
            GetOrCreateTerrainLayer("DarkSoil", TerrainDarkSoilDiffuse, TerrainDarkSoilNormal, new Color(0.23f, 0.18f, 0.14f, 1f), new Color(0.36f, 0.30f, 0.23f, 1f), 7f),
            GetOrCreateTerrainLayer("WetStone", TerrainWetStoneDiffuse, TerrainWetStoneNormal, new Color(0.19f, 0.25f, 0.25f, 1f), new Color(0.34f, 0.40f, 0.38f, 1f), 8f),
            GetOrCreateTerrainLayer("MountainRidgeStone", TerrainStoneDiffuse, TerrainStoneNormal, new Color(0.23f, 0.25f, 0.23f, 1f), new Color(0.42f, 0.43f, 0.38f, 1f), 12f)
        };

        float[,] heights = new float[heightResolution, heightResolution];
        for (int y = 0; y < heightResolution; y++)
        {
            for (int x = 0; x < heightResolution; x++)
            {
                float nx = x / (float)(heightResolution - 1);
                float nz = y / (float)(heightResolution - 1);
                float wx = nx * PlaySafeTerrainSize - PlaySafeTerrainHalfSize;
                float wz = nz * PlaySafeTerrainSize - PlaySafeTerrainHalfSize;
                heights[y, x] = SampleTutorialTerrainHeight01(wx, wz);
            }
        }
        terrainData.SetHeights(0, 0, heights);

        float[,,] splats = new float[alphaResolution, alphaResolution, terrainData.terrainLayers.Length];
        for (int y = 0; y < alphaResolution; y++)
        {
            for (int x = 0; x < alphaResolution; x++)
            {
                float wx = x / (float)(alphaResolution - 1) * PlaySafeTerrainSize - PlaySafeTerrainHalfSize;
                float wz = y / (float)(alphaResolution - 1) * PlaySafeTerrainSize - PlaySafeTerrainHalfSize;
                float[] weights = { 1f, 0f, 0f, 0f, 0f, 0f };
                float path = TutorialPathFlatten(wx, wz);
                float height01 = SampleTutorialTerrainHeight01(wx, wz);
                float mountain = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.11f, 0.31f, height01));
                weights[0] += Mathf.Clamp01(1f - mountain) * 0.65f;
                weights[1] += path * 3.3f;
                weights[5] += mountain * 3.2f;
                if (wz > 44f)
                    weights[2] += Mathf.InverseLerp(44f, PlaySafeTerrainHalfSize, wz) * 2.7f;
                if (wx > 44f)
                    weights[3] += Mathf.InverseLerp(44f, PlaySafeTerrainHalfSize, wx) * 2.7f;
                if (wx < -44f)
                    weights[4] += Mathf.InverseLerp(-44f, -PlaySafeTerrainHalfSize, wx) * 2.7f;
                if (wz < -44f)
                    weights[0] += Mathf.InverseLerp(-44f, -PlaySafeTerrainHalfSize, wz) * 1.7f;

                float total = 0f;
                for (int layer = 0; layer < weights.Length; layer++)
                    total += weights[layer];
                for (int layer = 0; layer < weights.Length; layer++)
                    splats[y, x, layer] = weights[layer] / Mathf.Max(0.0001f, total);
            }
        }
        terrainData.SetAlphamaps(0, 0, splats);
        EditorUtility.SetDirty(terrainData);

        GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
        terrainGo.name = "YQ_PlaySafe_EditableTerrain_HeightmapSplatmap";
        terrainGo.transform.SetParent(root, false);
        terrainGo.transform.position = new Vector3(-PlaySafeTerrainHalfSize, PlaySafeTerrainBaseY, -PlaySafeTerrainHalfSize);
        Terrain terrain = terrainGo.GetComponent<Terrain>();
        if (terrain != null)
        {
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 9f;
            terrain.basemapDistance = 120f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    private static float SampleTutorialTerrainHeight01(float wx, float wz)
    {
        float centerDistance = Mathf.Sqrt(wx * wx + wz * wz);
        float nx = (wx + PlaySafeTerrainHalfSize) / PlaySafeTerrainSize;
        float nz = (wz + PlaySafeTerrainHalfSize) / PlaySafeTerrainSize;
        float broadRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(44f, 132f, centerDistance)) * 0.075f;
        float mountainRing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(82f, PlaySafeTerrainHalfSize, centerDistance)) * 0.17f;
        float wave = Mathf.PerlinNoise(nx * 3.4f + 2.1f, nz * 3.4f + 6.7f) * 0.026f;
        wave += Mathf.PerlinNoise(nx * 8.2f + 19.4f, nz * 8.2f + 3.5f) * 0.010f;
        float peaks =
            MountainPeak(wx, wz, -118f, 112f, 38f, 0.23f) +
            MountainPeak(wx, wz, 118f, 104f, 42f, 0.20f) +
            MountainPeak(wx, wz, -126f, -112f, 42f, 0.21f) +
            MountainPeak(wx, wz, 124f, -116f, 46f, 0.19f) +
            MountainPeak(wx, wz, 0f, 138f, 34f, 0.15f) +
            MountainPeak(wx, wz, 138f, 0f, 38f, 0.13f) +
            MountainPeak(wx, wz, -138f, 0f, 38f, 0.13f);
        float raw = Mathf.Clamp01(0.009f + broadRise + mountainRing + wave + peaks);
        return Mathf.Lerp(raw, 0.006f, TutorialPathFlatten(wx, wz));
    }

    private static float MountainPeak(float wx, float wz, float px, float pz, float radius, float height)
    {
        float dx = (wx - px) / Mathf.Max(1f, radius);
        float dz = (wz - pz) / Mathf.Max(1f, radius);
        return Mathf.Exp(-(dx * dx + dz * dz)) * height;
    }

    private static float TutorialPathFlatten(float wx, float wz)
    {
        float northSouthRoad = Mathf.SmoothStep(1f, 0f, Mathf.Abs(wx) / 8.0f);
        float eastWestRoad = Mathf.SmoothStep(1f, 0f, Mathf.Abs(wz - 4f) / 7.4f);
        float flatten = Mathf.Max(northSouthRoad * 0.92f, eastWestRoad * 0.90f);
        flatten = Mathf.Max(flatten, CircularFlatten(wx, wz, 0f, 8f, 20f));
        flatten = Mathf.Max(flatten, CircularFlatten(wx, wz, 0f, 42f, 24f) * 0.96f);
        flatten = Mathf.Max(flatten, CircularFlatten(wx, wz, 0f, 64f, 21f) * 0.94f);
        flatten = Mathf.Max(flatten, CircularFlatten(wx, wz, 56f, 4f, 20f) * 0.94f);
        flatten = Mathf.Max(flatten, CircularFlatten(wx, wz, 0f, -55f, 20f) * 0.94f);
        flatten = Mathf.Max(flatten, CircularFlatten(wx, wz, -56f, 4f, 20f) * 0.94f);
        return Mathf.Clamp01(flatten);
    }

    private static float CircularFlatten(float wx, float wz, float cx, float cz, float radius)
    {
        float dx = wx - cx;
        float dz = wz - cz;
        return Mathf.SmoothStep(1f, 0f, Mathf.Sqrt(dx * dx + dz * dz) / Mathf.Max(1f, radius));
    }

    private static Vector3 GroundedTerrainPosition(Vector3 position, float offset)
    {
        position.y = PlaySafeTerrainBaseY + SampleTutorialTerrainHeight01(position.x, position.z) * PlaySafeTerrainHeight + offset;
        return position;
    }

    private static void BuildSpawnHut(Transform root)
    {
        Transform hut = CreateGroup(root, "02_SavedImportedSpawnHut");
        CreateBox(hut, "Hut_PhysicsFloor", new Vector3(0f, 0.08f, 0f), new Vector3(7.6f, 0.16f, 6.4f), new Color(0.31f, 0.22f, 0.14f, 1f), true, true);
        CreateBox(hut, "Hut_BackWall", new Vector3(0f, 1.55f, -3.15f), new Vector3(7.8f, 3.1f, 0.22f), new Color(0.30f, 0.23f, 0.17f, 1f), true, true);
        CreateBox(hut, "Hut_LeftWall", new Vector3(-3.85f, 1.55f, 0f), new Vector3(0.22f, 3.1f, 6.4f), new Color(0.30f, 0.23f, 0.17f, 1f), true, true);
        CreateBox(hut, "Hut_RightWall", new Vector3(3.85f, 1.55f, 0f), new Vector3(0.22f, 3.1f, 6.4f), new Color(0.30f, 0.23f, 0.17f, 1f), true, true);
        CreateBox(hut, "Hut_FrontLeft", new Vector3(-2.75f, 1.55f, 3.15f), new Vector3(2.1f, 3.1f, 0.22f), new Color(0.30f, 0.23f, 0.17f, 1f), true, true);
        CreateBox(hut, "Hut_FrontRight", new Vector3(2.75f, 1.55f, 3.15f), new Vector3(2.1f, 3.1f, 0.22f), new Color(0.30f, 0.23f, 0.17f, 1f), true, true);
        CreateBox(hut, "Hut_Roof_NoLeak", new Vector3(0f, 3.2f, 0f), new Vector3(8.4f, 0.34f, 7.1f), new Color(0.18f, 0.16f, 0.12f, 1f), true, true);

        InstantiateImported(hut, "Imported_VictorianFloor", VictorianFloor, new Vector3(0f, 0.18f, 0f), Vector3.zero, Vector3.one * 0.85f, false, false, 7.2f, 0.8f);
        InstantiateImported(hut, "Imported_VictorianWindowWall_Back", VictorianWallWindow, new Vector3(0f, 0.25f, -3.06f), new Vector3(0f, 0f, 0f), Vector3.one * 0.85f, false, false, 7.2f, 3.2f);
        InstantiateImported(hut, "Imported_VictorianWall_LeftDetail", VictorianWall, new Vector3(-3.72f, 0.25f, -0.55f), new Vector3(0f, 90f, 0f), Vector3.one * 0.82f, false, false, 5.8f, 3.2f);
        InstantiateImported(hut, "Imported_VictorianWall_RightDetail", VictorianWall, new Vector3(3.72f, 0.25f, -0.55f), new Vector3(0f, -90f, 0f), Vector3.one * 0.82f, false, false, 5.8f, 3.2f);
        GameObject door = InstantiateImported(hut, "Imported_LockpickableHutDoor", VictorianDoor, new Vector3(0f, 0.12f, 3.23f), new Vector3(0f, 180f, 0f), Vector3.one, true, true, 2.0f, 2.9f);
        if (door != null)
        {
            YQLockpickableDoor lockDoor = door.GetComponent<YQLockpickableDoor>();
            if (lockDoor == null)
                lockDoor = door.AddComponent<YQLockpickableDoor>();
            lockDoor.displayName = "Hut Door";
            lockDoor.regionId = "origin_forest";
            lockDoor.locked = false;
            lockDoor.openEuler = new Vector3(0f, -86f, 0f);
            EnsureBoxCollider(door, new Vector3(1.4f, 2.3f, 0.28f), new Vector3(0f, 1.15f, 0f));
        }

        InstantiateImported(hut, "Imported_Bookshelf", VictorianBookshelf, new Vector3(-2.8f, 0.14f, -2.55f), new Vector3(0f, 0f, 0f), Vector3.one, false, false, 2.2f, 2.4f);
        InstantiateImported(hut, "Imported_StudyTable", VictorianTable, new Vector3(2.2f, 0.12f, -1.45f), new Vector3(0f, -24f, 0f), Vector3.one, false, false, 2.0f, 1.2f);
        InstantiateImported(hut, "Imported_Fireplace", VictorianFireplace, new Vector3(2.85f, 0.12f, 1.15f), new Vector3(0f, -90f, 0f), Vector3.one, false, false, 1.6f, 2.1f);
        InstantiateImported(hut, "Imported_BedrollAnchor", VictorianBed, new Vector3(-2.2f, 0.12f, 1.15f), new Vector3(0f, 88f, 0f), Vector3.one, false, false, 2.2f, 1.0f);
    }

    private static void BuildForestDressing(Transform root)
    {
        Transform forest = CreateGroup(root, "03_SavedImportedForest");
        string[] trees = { NordicTreeA, NordicTreeB, AsianTree, TerrainAlder, TerrainMimosa, TerrainPineA, TerrainPineB, TerrainSycamore, TerrainThinTree };
        int placedTrees = 0;
        for (int i = 0; i < 170 && placedTrees < 60; i++)
        {
            float x = Mathf.Lerp(-92f, 92f, Hash01(i, 3));
            float z = Mathf.Lerp(-88f, 100f, Hash01(i, 11));
            Vector3 position = new Vector3(x, 0f, z);
            if (!IsClearForForestTree(position))
                continue;

            position = GroundedTerrainPosition(position, 0.03f);
            float scale = 0.55f + Hash01(i, 17) * 0.62f;
            float maxHeight = Mathf.Lerp(7.4f, 12.4f, Hash01(i, 23));
            InstantiateImported(forest, "Imported_ForestTree_" + placedTrees, trees[(i + placedTrees) % trees.Length], position, new Vector3(0f, Hash01(i, 31) * 360f, 0f), Vector3.one * scale, false, false, 6.6f, maxHeight);
            placedTrees++;
        }

        string[] understory = { NordicBush, NordicGrass, NordicFern, NordicThatchGrass, PersepolisGrassPatch };
        int placedUnderstory = 0;
        for (int i = 0; i < 210 && placedUnderstory < 82; i++)
        {
            float x = Mathf.Lerp(-84f, 84f, Hash01(i, 41));
            float z = Mathf.Lerp(-76f, 94f, Hash01(i, 47));
            Vector3 position = new Vector3(x, 0f, z);
            if (!IsClearForForestFloor(position))
                continue;

            position = GroundedTerrainPosition(position, 0.045f);
            float scale = 0.38f + Hash01(i, 53) * 0.38f;
            InstantiateImported(forest, "Imported_Understory_" + placedUnderstory, understory[(i + placedUnderstory) % understory.Length], position, new Vector3(0f, Hash01(i, 61) * 360f, 0f), Vector3.one * scale, false, false, 2.2f, 1.4f);
            placedUnderstory++;
        }

        BuildGroundDetailDressing(forest);
        BuildMountainRidgeDressing(forest);
    }

    private static void BuildGroundDetailDressing(Transform parent)
    {
        Material grassA = GetOrCreateBillboardMaterial("GroundGrassBillboard_A", GrassBillboardTexture, new Color(0.40f, 0.62f, 0.28f, 1f));
        Material grassB = GetOrCreateBillboardMaterial("GroundGrassBillboard_B", GrassBillboardTextureB, new Color(0.32f, 0.52f, 0.24f, 1f));
        Material flower = GetOrCreateBillboardMaterial("GroundFlowerBillboard", FlowerBillboardTexture, new Color(0.76f, 0.78f, 0.46f, 1f));
        Material rock = GetOrCreateSceneMaterialFromTexture("GroundRockScatter", RockGroundDiffuse, RockGroundNormal, new Color(0.40f, 0.38f, 0.34f, 1f), 2.5f);

        int placedTufts = 0;
        for (int i = 0; i < 190 && placedTufts < 105; i++)
        {
            float x = Mathf.Lerp(-88f, 88f, Hash01(i, 101));
            float z = Mathf.Lerp(-82f, 96f, Hash01(i, 107));
            Vector3 position = new Vector3(x, 0f, z);
            if (!IsClearForForestFloor(position))
                continue;

            Material material = i % 9 == 0 ? flower : (i % 2 == 0 ? grassA : grassB);
            position = GroundedTerrainPosition(position, 0.08f);
            CreateBillboardTuft(parent, "GroundGrassTuft_" + placedTufts, position, Hash01(i, 109) * 360f, 0.68f + Hash01(i, 113) * 0.54f, material);
            placedTufts++;
        }

        int placedRocks = 0;
        for (int i = 0; i < 90 && placedRocks < 32; i++)
        {
            float x = Mathf.Lerp(-86f, 86f, Hash01(i, 127));
            float z = Mathf.Lerp(-78f, 94f, Hash01(i, 131));
            Vector3 position = new Vector3(x, 0f, z);
            if (!IsClearForForestFloor(position))
                continue;

            GameObject stone = GameObject.CreatePrimitive(i % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Cube);
            stone.name = "GroundRockScatter_" + placedRocks;
            stone.transform.SetParent(parent, false);
            stone.transform.position = GroundedTerrainPosition(position, 0.11f);
            stone.transform.rotation = Quaternion.Euler(0f, Hash01(i, 137) * 360f, (Hash01(i, 139) - 0.5f) * 9f);
            float size = 0.24f + Hash01(i, 149) * 0.46f;
            stone.transform.localScale = new Vector3(size * 1.7f, size * 0.35f, size * (1.1f + (i % 3) * 0.25f));
            stone.isStatic = true;
            Renderer renderer = stone.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = rock;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
            UnityEngine.Object.DestroyImmediate(stone.GetComponent<Collider>());
            placedRocks++;
        }
    }

    private static void BuildMountainRidgeDressing(Transform parent)
    {
        string[] rocks = { TerrainRock, NordicRockA, NordicRockB, WesternRockA, WesternRockB };
        for (int i = 0; i < 24; i++)
        {
            float angle = (i * 41.0f + Hash01(i, 167) * 16f) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(90f, 136f, Hash01(i, 171));
            Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (TutorialPathFlatten(position.x, position.z) > 0.12f)
                position += position.normalized * 18f;

            position = GroundedTerrainPosition(position, 0.05f);
            float scale = Mathf.Lerp(0.8f, 1.9f, Hash01(i, 173));
            InstantiateImported(parent, "Imported_DistantRidgeRock_" + i, rocks[i % rocks.Length], position, new Vector3((Hash01(i, 179) - 0.5f) * 6f, Hash01(i, 181) * 360f, (Hash01(i, 191) - 0.5f) * 8f), Vector3.one * scale, false, false, 6.8f, 4.6f);
        }
    }

    private static bool IsClearForForestTree(Vector3 position)
    {
        if (TutorialPathFlatten(position.x, position.z) > 0.18f)
            return false;

        float centerDistance = Mathf.Sqrt(position.x * position.x + position.z * position.z);
        if (centerDistance < 18f)
            return false;
        if (Mathf.Abs(position.x) < 18f && position.z > 32f && position.z < 72f)
            return false;
        if (Mathf.Abs(position.x) < 19f && position.z < -42f && position.z > -72f)
            return false;
        if (position.x > 42f && position.x < 74f && position.z > -18f && position.z < 24f)
            return false;
        if (position.x < -42f && position.x > -74f && position.z > -18f && position.z < 24f)
            return false;
        if (position.x > 4f && position.x < 18f && position.z > 8f && position.z < 29f)
            return false;
        if (position.x < -2f && position.x > -16f && position.z > 15f && position.z < 38f)
            return false;

        return true;
    }

    private static bool IsClearForForestFloor(Vector3 position)
    {
        if (TutorialPathFlatten(position.x, position.z) > 0.42f)
            return false;

        float centerDistance = Mathf.Sqrt(position.x * position.x + position.z * position.z);
        if (centerDistance < 13f)
            return false;
        if (Mathf.Abs(position.x) < 14f && position.z > 34f && position.z < 68f)
            return false;
        if (Mathf.Abs(position.x) < 13f && position.z < -44f && position.z > -68f)
            return false;
        if (position.x > 45f && position.x < 70f && position.z > -14f && position.z < 20f)
            return false;
        if (position.x < -45f && position.x > -70f && position.z > -14f && position.z < 20f)
            return false;

        return true;
    }

    private static float Hash01(int seed, int salt)
    {
        float value = Mathf.Sin(seed * 12.9898f + salt * 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    private static void BuildRegionSamples(Transform root)
    {
        Transform regions = CreateGroup(root, "04_SavedImportedRegionSamples");

        CreateTexturedBox(regions, "North_ColdStoneRegionGround", new Vector3(0f, 0.09f, 68f), new Vector3(30f, 0.08f, 26f), TerrainWetStoneDiffuse, TerrainWetStoneNormal, new Color(0.38f, 0.48f, 0.48f, 1f), false, false, 8f);
        for (int i = 0; i < 9; i++)
            CreateCrystal(regions, "North_IceCrystal_" + i, new Vector3(-12f + i * 3f, 0.5f, 61f + (i % 3) * 4f), new Color(0.67f, 0.95f, 1f, 1f));

        CreateTexturedBox(regions, "East_RuinsPackedEarthGround", new Vector3(68f, 0.09f, 0f), new Vector3(28f, 0.08f, 28f), TerrainDarkSoilDiffuse, TerrainDarkSoilNormal, new Color(0.33f, 0.27f, 0.20f, 1f), false, false, 8f);
        InstantiateImported(regions, "Imported_PersepolisFloor_East", PersepolisFloor, new Vector3(68f, 0.13f, 0f), Vector3.zero, Vector3.one, false, false, 16f, 2f);
        InstantiateImported(regions, "Imported_PersepolisArch_East", PersepolisWallArc, new Vector3(68f, 0.14f, 9f), Vector3.zero, Vector3.one, false, false, 11f, 5f);
        InstantiateImported(regions, "Imported_PersepolisArch_East_FlankA", PersepolisWallArc, new Vector3(58f, 0.14f, -7f), new Vector3(0f, 36f, 0f), Vector3.one * 0.82f, false, false, 8f, 4.2f);
        InstantiateImported(regions, "Imported_PersepolisArch_East_FlankB", PersepolisWallArc, new Vector3(78f, 0.14f, -7f), new Vector3(0f, -36f, 0f), Vector3.one * 0.82f, false, false, 8f, 4.2f);

        CreateTexturedBox(regions, "South_JungleMossGround", new Vector3(0f, 0.09f, -68f), new Vector3(30f, 0.08f, 26f), TerrainGrassDiffuse, TerrainGrassNormal, new Color(0.18f, 0.34f, 0.17f, 1f), false, false, 8f);
        InstantiateImported(regions, "Imported_AsianTile_South", AsianTile, new Vector3(0f, 0.14f, -68f), Vector3.zero, Vector3.one, false, false, 12f, 1f);
        InstantiateImported(regions, "Imported_AsianPavilion_South", AsianPavilion, new Vector3(-8f, 0.16f, -71f), new Vector3(0f, 20f, 0f), Vector3.one, false, false, 8f, 3.2f);
        InstantiateImported(regions, "Imported_AsianBuilding_South", AsianBuilding, new Vector3(10f, 0.16f, -67f), new Vector3(0f, -24f, 0f), Vector3.one, false, false, 9f, 4.5f);
        InstantiateImported(regions, "Imported_AsianStairs_South", AsianStairs, new Vector3(0f, 0.16f, -58f), new Vector3(0f, 180f, 0f), Vector3.one, false, false, 5f, 2.2f);
        InstantiateImported(regions, "Imported_AsianDragon_South", AsianDragon, new Vector3(0f, 0.16f, -76f), new Vector3(0f, 35f, 0f), Vector3.one, false, false, 7f, 4f);
        for (int i = 0; i < 6; i++)
            InstantiateImported(regions, "Imported_JungleTree_" + i, AsianTree, new Vector3(-13f + i * 5f, 0.04f, -58f - (i % 2) * 6f), new Vector3(0f, i * 37f, 0f), Vector3.one * 0.65f, false, false, 5f, 8f);

        CreateTexturedBox(regions, "West_WetStoneGround", new Vector3(-68f, 0.09f, 0f), new Vector3(28f, 0.08f, 28f), TerrainWetStoneDiffuse, TerrainWetStoneNormal, new Color(0.22f, 0.31f, 0.32f, 1f), false, false, 8f);
        for (int i = 0; i < 7; i++)
            CreateBox(regions, "West_TideStone_" + i, new Vector3(-78f + (i % 3) * 5f, 0.42f, -8f + i * 2.7f), new Vector3(2.4f, 0.75f, 1.5f), new Color(0.19f, 0.32f, 0.36f, 1f), true, true);
    }

    private static void BuildLootAndEquipment(Transform root)
    {
        Transform loot = CreateGroup(root, "05_SavedImportedLootEquipmentAndLocks");
        CreateLockpickChest(loot, "Forest Cache", new Vector3(-5.2f, 0.1f, 6.2f), "origin_forest", false, 0.22f, 18, ChestSimple);
        CreateLockpickChest(loot, "Ornate Ember Coffer", new Vector3(6.2f, 0.1f, 6.3f), "fire_region", false, 0.55f, 42, ChestOrnate);
        CreateLockpickChest(loot, "Sleeping Mimic", new Vector3(10.8f, 0.1f, -4.8f), "origin_forest", true, 0.48f, 30, ChestSimple);

        ShowcaseImported(loot, "Sword Display", Sword, new Vector3(-13f, 0.08f, 5f), new Vector3(0f, -35f, 72f), Vector3.one, 2.4f, 2.5f);
        ShowcaseImported(loot, "Staff Display", Staff, new Vector3(-16f, 0.08f, 5f), new Vector3(0f, 14f, 82f), Vector3.one, 2.4f, 2.6f);
        ShowcaseImported(loot, "Axe Display", Axe, new Vector3(-19f, 0.08f, 5f), new Vector3(0f, 28f, 78f), Vector3.one, 2.4f, 2.6f);
        ShowcaseImported(loot, "Spear Display", Spear, new Vector3(-22f, 0.08f, 5f), new Vector3(0f, -12f, 84f), Vector3.one, 2.6f, 2.8f);
        ShowcaseImported(loot, "Helmet Display", Helmet, new Vector3(-25f, 0.08f, 5f), new Vector3(0f, -25f, 0f), Vector3.one, 1.8f, 1.5f);
        ShowcaseImported(loot, "Shield Display", Shield, new Vector3(-28f, 0.08f, 5f), new Vector3(0f, 22f, 0f), Vector3.one, 2.0f, 2.1f);
        ShowcaseImported(loot, "Ring Display", Ring, new Vector3(-31f, 0.08f, 5f), Vector3.zero, Vector3.one, 1.2f, 1.2f);
        ShowcaseImported(loot, "Lockpick Display", Lockpick, new Vector3(13f, 0.08f, 5f), new Vector3(0f, 20f, 0f), Vector3.one, 1.4f, 1.2f);
        ShowcaseImported(loot, "Lock Display", Lock, new Vector3(16f, 0.08f, 5f), Vector3.zero, Vector3.one, 1.4f, 1.3f);
    }

    private static void BuildCreatureAndVfxSamples(Transform root)
    {
        Transform samples = CreateGroup(root, "06_SavedImportedCreaturesVfxAudio");
        ShowcaseImported(samples, "Human Male Preview", HumanMale, new Vector3(13f, 0.08f, -8f), new Vector3(0f, 190f, 0f), Vector3.one, 2.2f, 2.4f);
        ShowcaseImported(samples, "Plant Monster Preview", PlantMonster, new Vector3(17f, 0.08f, -8f), new Vector3(0f, -25f, 0f), Vector3.one, 2.4f, 2.8f);
        ShowcaseImported(samples, "Mushroom Monster Preview", MushroomMonster, new Vector3(21f, 0.08f, -8f), new Vector3(0f, 25f, 0f), Vector3.one, 2.2f, 2.4f);
        ShowcaseImported(samples, "Dragon Enemy Preview", DragonMonster, new Vector3(25f, 0.08f, -8f), new Vector3(0f, 205f, 0f), Vector3.one, 3.4f, 3.2f);
        ShowcaseImported(samples, "Demon Enemy Preview", DemonMonster, new Vector3(30f, 0.08f, -8f), new Vector3(0f, 170f, 0f), Vector3.one, 2.4f, 2.8f);
        ShowcaseImported(samples, "Bandit Enemy Preview", HumanMale, new Vector3(34f, 0.08f, -8f), new Vector3(0f, 180f, 0f), Vector3.one, 2.2f, 2.4f);

        CreateAudioVfxStation(samples, "Sword Impact", SwordAudio, FireVfx, new Vector3(-10f, 0.12f, 12f), new Color(1f, 0.62f, 0.32f, 1f), 0.55f);
        CreateAudioVfxStation(samples, "Fireball Burst", ElectricAudio, FireballVfx, new Vector3(-6.5f, 0.12f, 12f), new Color(1f, 0.46f, 0.28f, 1f), 0.62f);
        CreateAudioVfxStation(samples, "Healing Ward", WaterAudio, HealVfx, new Vector3(-3f, 0.12f, 12f), new Color(0.42f, 0.95f, 0.72f, 1f), 0.70f);
        CreateAudioVfxStation(samples, "Storm Burst", ElectricAudio, ElectricVfx, new Vector3(0.5f, 0.12f, 12f), new Color(0.55f, 0.74f, 1f, 1f), 0.52f);
    }

    private static void BuildAmbientAudio(Transform root)
    {
        Transform audio = CreateGroup(root, "07_SavedAmbientAudio");
        CreateAmbientLoop(audio, "Forest_AmbientWindBed", AmbientWindAudio, new Vector3(0f, 2.2f, 0f), 0f, 0.88f, 0f, 0f);
        CreateAmbientLoop(audio, "Hut_FireplaceLoop", FireLoopAudio, new Vector3(2.85f, 1.2f, 1.15f), 0.14f, 0.96f, 2f, 12f);
        CreateAmbientLoop(audio, "VfxStation_MagicHum", AmbientHumAudio, new Vector3(-5.8f, 1.2f, 12f), 0.035f, 0.72f, 3f, 11f);
    }

    private static void BindSavedSceneAudioClips(Transform root)
    {
        if (root == null)
            return;

        BindAudioClip(root, "Station_Sword_Impact", SwordAudio);
        BindAudioClip(root, "Station_Fireball_Burst", ElectricAudio);
        BindAudioClip(root, "Station_Healing_Ward", WaterAudio);
        BindAudioClip(root, "Station_Storm_Burst", ElectricAudio);
        BindAudioClip(root, "Forest_AmbientWindBed", AmbientWindAudio);
        BindAudioClip(root, "Hut_FireplaceLoop", FireLoopAudio);
        BindAudioClip(root, "VfxStation_MagicHum", AmbientHumAudio);
    }

    private static void BindAudioClip(Transform root, string objectName, string clipPath)
    {
        Transform target = FindDeepChild(root, objectName);
        AudioSource source = target != null ? target.GetComponent<AudioSource>() : null;
        if (source == null)
            return;
        if (string.IsNullOrWhiteSpace(clipPath))
        {
            source.Stop();
            source.enabled = false;
            return;
        }

        AudioClip clip = ResolveAudioClip(clipPath);
        if (clip == null)
        {
            Debug.LogWarning("[YourQuest] Could not bind audio clip for " + objectName + ": " + clipPath);
            return;
        }

        source.clip = clip;
        EditorUtility.SetDirty(source);
    }

    private static AudioClip ResolveAudioClip(string clipPath)
    {
        string normalizedPath = NormalizeAssetPath(clipPath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
            return null;

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(normalizedPath);
        if (clip != null)
            return clip;

        string wantedLeaf = GetPathLeaf(normalizedPath);
        string wantedName = Path.GetFileNameWithoutExtension(wantedLeaf);
        if (string.IsNullOrWhiteSpace(wantedName))
            return null;

        string[] guids = AssetDatabase.FindAssets(wantedName + " t:AudioClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string foundPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (!string.Equals(GetPathLeaf(foundPath), wantedLeaf, System.StringComparison.OrdinalIgnoreCase))
                continue;

            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(foundPath);
            if (clip != null)
                return clip;
        }

        for (int i = 0; i < guids.Length; i++)
        {
            string foundPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (foundPath.IndexOf(wantedName, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(foundPath);
            if (clip != null)
                return clip;
        }

        return null;
    }

    private static Transform FindDeepChild(Transform root, string name)
    {
        if (root == null)
            return null;
        if (string.Equals(root.name, name, System.StringComparison.Ordinal))
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindDeepChild(root.GetChild(i), name);
            if (match != null)
                return match;
        }

        return null;
    }

    private static Transform CreateGroup(Transform parent, string name)
    {
        GameObject group = new GameObject(name);
        group.transform.SetParent(parent, false);
        group.isStatic = true;
        return group.transform;
    }

    private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Color color, bool keepCollider, bool castShadows)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.position = position;
        box.transform.localScale = scale;
        box.isStatic = true;
        if (!keepCollider)
            UnityEngine.Object.DestroyImmediate(box.GetComponent<Collider>());
        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
        SetRendererColor(box, color);
        return box;
    }

    private static GameObject CreateTexturedBox(Transform parent, string name, Vector3 position, Vector3 scale, string diffusePath, string normalPath, Color fallbackColor, bool keepCollider, bool castShadows, float tileScale)
    {
        GameObject box = CreateBox(parent, name, position, scale, fallbackColor, keepCollider, castShadows);
        Renderer renderer = box.GetComponent<Renderer>();
        if (renderer != null)
            renderer.sharedMaterial = GetOrCreateSceneMaterialFromTexture(name, diffusePath, normalPath, fallbackColor, tileScale);
        return box;
    }

    private static void CreateBillboardTuft(Transform parent, string name, Vector3 position, float yaw, float size, Material material)
    {
        GameObject tuft = new GameObject(name);
        tuft.transform.SetParent(parent, false);
        tuft.transform.position = position;
        tuft.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        tuft.isStatic = true;

        for (int i = 0; i < 2; i++)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "BladePlane_" + i;
            quad.transform.SetParent(tuft.transform, false);
            quad.transform.localPosition = new Vector3(0f, size * 0.42f, 0f);
            quad.transform.localRotation = Quaternion.Euler(0f, i * 90f, 0f);
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.isStatic = true;
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());

            Renderer renderer = quad.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }
        }
    }

    private static void CreateCrystal(Transform parent, string name, Vector3 position, Color color)
    {
        GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        crystal.name = name;
        crystal.transform.SetParent(parent, false);
        crystal.transform.position = position;
        crystal.transform.rotation = Quaternion.Euler(0f, position.x * 7f, 0f);
        crystal.transform.localScale = new Vector3(0.55f, 1.4f + Mathf.Abs(position.x % 3f) * 0.35f, 0.55f);
        crystal.isStatic = true;
        SetRendererColor(crystal, color);
    }

    private static void CreateGlowLight(Transform parent, string name, Vector3 position, Color color, float intensity, float range)
    {
        GameObject lightGo = new GameObject(name);
        lightGo.transform.SetParent(parent, false);
        lightGo.transform.position = position;
        Light light = lightGo.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.shadows = LightShadows.None;
    }

    private static void CreateAmbientLoop(Transform parent, string name, string clipPath, Vector3 position, float volume, float pitch, float minDistance, float maxDistance)
    {
        if (string.IsNullOrWhiteSpace(clipPath) || volume <= 0.001f)
            return;

        AudioClip clip = ResolveAudioClip(clipPath);
        if (clip == null)
        {
            Debug.LogWarning("[YourQuest] Missing ambient audio clip: " + clipPath);
            return;
        }

        GameObject audioGo = new GameObject(name);
        audioGo.transform.SetParent(parent, false);
        audioGo.transform.position = position;
        AudioSource source = audioGo.AddComponent<AudioSource>();
        source.clip = clip;
        source.playOnAwake = true;
        source.loop = true;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, 0.5f, 1.5f);
        source.spatialBlend = minDistance <= 0f ? 0f : 0.72f;
        source.minDistance = Mathf.Max(0f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + 1f, maxDistance);
        source.rolloffMode = AudioRolloffMode.Linear;
        if (clip.length > 0.25f)
            source.time = UnityEngine.Random.Range(0f, clip.length * 0.85f);
    }

    private static void ShowcaseImported(Transform parent, string label, string assetPath, Vector3 position, Vector3 euler, Vector3 scale, float maxFootprint, float maxHeight)
    {
        CreateBox(parent, label + "_Pedestal", position + new Vector3(0f, 0.1f, 0f), new Vector3(1.7f, 0.2f, 1.7f), new Color(0.22f, 0.23f, 0.24f, 1f), false, false);
        InstantiateImported(parent, "Imported_" + MakeSafeName(label), assetPath, position + new Vector3(0f, 0.22f, 0f), euler, scale, false, false, maxFootprint, maxHeight);
    }

    private static void CreateLockpickChest(Transform parent, string displayName, Vector3 position, string regionId, bool mimic, float difficulty, int gold, string prefabPath)
    {
        GameObject chest = InstantiateImported(parent, "Imported_" + MakeSafeName(displayName), prefabPath, position, Vector3.zero, Vector3.one, true, true, 2.2f, 1.8f);
        if (chest == null)
            return;

        YQLockpickableLoot loot = chest.GetComponent<YQLockpickableLoot>();
        if (loot == null)
            loot = chest.AddComponent<YQLockpickableLoot>();
        loot.displayName = displayName;
        loot.regionId = regionId;
        loot.mimic = mimic;
        loot.revealedMimicPrefabPath = mimic ? MimicSimple : string.Empty;
        loot.locked = true;
        loot.lockDifficulty = difficulty;
        loot.gold = gold;
        loot.PrimeVisualState();
        EnsureBoxCollider(chest, new Vector3(1.5f, 1.1f, 1.1f), new Vector3(0f, 0.55f, 0f));
    }

    private static void CreateAudioVfxStation(Transform parent, string label, string clipPath, string vfxPath, Vector3 position, Color color, float vfxScale)
    {
        GameObject stationGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stationGo.name = "Station_" + MakeSafeName(label);
        stationGo.transform.SetParent(parent, false);
        stationGo.transform.position = position;
        stationGo.transform.localScale = new Vector3(0.9f, 0.34f, 0.9f);
        SetRendererColor(stationGo, color);

        GameObject spawn = new GameObject("VFX_Spawn");
        spawn.transform.SetParent(stationGo.transform, false);
        spawn.transform.localPosition = new Vector3(0f, 1.15f, 0f);

        AudioSource source = stationGo.AddComponent<AudioSource>();
        source.clip = ResolveAudioClip(clipPath);
        source.playOnAwake = false;
        source.volume = 0.82f;
        source.spatialBlend = 0.55f;
        source.minDistance = 2f;
        source.maxDistance = 28f;
        source.rolloffMode = AudioRolloffMode.Linear;

        YQAssetTestStation station = stationGo.AddComponent<YQAssetTestStation>();
        station.stationName = label;
        station.audioSource = source;
        station.vfxPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);
        station.vfxSpawnPoint = spawn.transform;
        station.vfxLifetime = 4.5f;
        station.vfxScale = vfxScale;
        station.interactRadius = 4.2f;
        station.statusRenderer = stationGo.GetComponent<Renderer>();
        station.readyColor = color;
        station.activeColor = new Color(1f, 0.98f, 0.70f, 1f);

        CreateGlowLight(stationGo.transform, "Station_Glow_NoShadows", new Vector3(0f, 0.9f, 0f), color, 0.9f, 3.4f);
    }

    private static GameObject InstantiateImported(
        Transform parent,
        string name,
        string prefabPath,
        Vector3 position,
        Vector3 euler,
        Vector3 scale,
        bool keepColliders,
        bool keepBehaviours,
        float maxFootprint,
        float maxHeight)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
            return CreateMissingMarker(parent, name, position);

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
        if (instance == null)
            return CreateMissingMarker(parent, name, position);

        instance.name = name;
        instance.transform.SetParent(parent, false);
        instance.transform.position = position;
        instance.transform.rotation = Quaternion.Euler(euler);
        instance.transform.localScale = scale;
        PrepareImportedInstance(instance, keepColliders, keepBehaviours);
        FitAndGround(instance, position, maxFootprint, maxHeight);
        return instance;
    }

    private static GameObject CreateMissingMarker(Transform parent, string name, Vector3 position)
    {
        GameObject marker = CreateBox(parent, "Missing_" + MakeSafeName(name), position + new Vector3(0f, 0.5f, 0f), Vector3.one, NeutralPlaceholderColor(), true, false);
        Debug.LogWarning("[YourQuest] Missing curated play-scene asset: " + name);
        return marker;
    }

    private static void PrepareImportedInstance(GameObject root, bool keepColliders, bool keepBehaviours)
    {
        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] == null)
                continue;
            bodies[i].isKinematic = true;
            bodies[i].useGravity = false;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = keepColliders;
        }

        Light[] lights = root.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] == null)
                continue;
            lights[i].shadows = LightShadows.None;
            lights[i].intensity = Mathf.Min(lights[i].intensity, 1.2f);
            lights[i].range = Mathf.Min(lights[i].range, 8f);
        }

        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            if (animators[i] != null)
                animators[i].enabled = keepBehaviours && animators[i].runtimeAnimatorController != null;
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i] is not YQLockpickableLoot && behaviours[i] is not YQLockpickableDoor)
                behaviours[i].enabled = keepBehaviours;
        }

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        YQVisualStabilityDirector.StabilizeHierarchy(root);
        RepairAndValidateImportedMaterials(root);
    }

    private static void RepairAndValidateImportedMaterials(GameObject root)
    {
        if (root == null)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool changed = false;
            for (int slot = 0; slot < materials.Length; slot++)
            {
                Material material = materials[slot];
                if (NeedsMaterialFallback(material, renderer))
                {
                    materials[slot] = GetOrCreateSemanticMaterial(root.name + "_" + renderer.name + "_" + slot, renderer, root.name);
                    changed = true;
                }
                else if (NeedsLocalShaderRepair(material, renderer))
                {
                    Material repaired = GetOrCreateLocalUrpMaterial(material, renderer, root.name, slot);
                    if (repaired != null && repaired != material)
                    {
                        materials[slot] = repaired;
                        changed = true;
                    }
                }
            }

            if (changed)
                renderer.sharedMaterials = materials;
        }
    }

    private static bool NeedsMaterialFallback(Material material, Renderer renderer)
    {
        if (material == null || material.shader == null)
            return true;

        string shaderName = material.shader.name;
        if (string.IsNullOrWhiteSpace(shaderName))
            return true;

        if (LooksMagenta(material))
            return true;

        if (renderer is ParticleSystemRenderer)
            return false;

        return !HasUsableTexture(material);
    }

    private static bool NeedsLocalShaderRepair(Material material, Renderer renderer)
    {
        if (material == null || material.shader == null)
            return false;

        string shaderName = material.shader.name;
        if (shaderName.Contains("InternalErrorShader"))
            return HasUsableTexture(material);
        if (shaderName.StartsWith("Universal Render Pipeline/") || shaderName.StartsWith("Shader Graphs/"))
            return !HasAssignedBaseTexture(material) && HasUsableTexture(material);
        if (renderer is ParticleSystemRenderer && (shaderName.StartsWith("Particles/") || shaderName.StartsWith("Mobile/Particles/")))
            return false;
        return true;
    }

    private static Material GetOrCreateLocalUrpMaterial(Material source, Renderer renderer, string rootName, int slot)
    {
        if (source == null)
            return null;

        bool particleMaterial = renderer is ParticleSystemRenderer;
        Shader shader = Shader.Find(particleMaterial ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
        if (shader == null && particleMaterial)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return source;

        string sourcePath = AssetDatabase.GetAssetPath(source);
        string sourceGuid = !string.IsNullOrWhiteSpace(sourcePath) ? AssetDatabase.AssetPathToGUID(sourcePath) : string.Empty;
        string suffix = !string.IsNullOrWhiteSpace(sourceGuid)
            ? sourceGuid.Substring(0, Mathf.Min(10, sourceGuid.Length))
            : Mathf.Abs(source.GetInstanceID()).ToString("X");
        string materialName = "URP_" + MakeSafeName(source.name + "_" + suffix + "_" + slot);
        string materialPath = GeneratedMaterialFolder + "/" + materialName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            material.name = materialName;
            AssetDatabase.CreateAsset(material, materialPath);
        }
        else
        {
            material.shader = shader;
        }

        Texture baseTexture = FindBaseTexture(source, renderer);
        if (baseTexture != null)
        {
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", baseTexture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", baseTexture);
        }

        Color color = SanitizeCopiedMaterialColor(source, renderer, baseTexture, FindMaterialColor(source, Color.white));
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.28f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        if (LooksLikeFoliageMaterial(source, renderer, baseTexture))
            ConfigureFoliageCutout(material);
        if (particleMaterial)
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static bool LooksLikeFoliageMaterial(Material source, Renderer renderer, Texture texture)
    {
        string text = ToTextureSearchText(
            (source != null ? source.name : string.Empty) + " " +
            (texture != null ? texture.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty));

        return ContainsAny(text, "leaf", "leaves", "branch", "branches", "grass", "bush", "flower", "plant", "billboard", "treebillboard", "foliage");
    }

    private static Color SanitizeCopiedMaterialColor(Material source, Renderer renderer, Texture texture, Color color)
    {
        if (!LooksLikeAlertRed(color))
            return color;

        string text = ToTextureSearchText(
            (source != null ? source.name : string.Empty) + " " +
            (texture != null ? texture.name : string.Empty) + " " +
            (renderer != null ? renderer.name : string.Empty) + " " +
            (renderer != null && renderer.gameObject != null ? renderer.gameObject.name : string.Empty));
        if (ContainsAny(text, "leaf", "leaves", "branch", "branches", "grass", "bush", "flower", "plant", "billboard", "treebillboard", "foliage"))
            return new Color(0.22f, 0.42f, 0.20f, color.a);
        if (ContainsAny(text, "missing", "placeholder", "fallback", "proxy", "runtimeurp", "defaultdirty"))
            return NeutralPlaceholderColor();

        return color;
    }

    private static void ConfigureFoliageCutout(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 1f);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", 0.38f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        material.renderQueue = 2450;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHATEST_ON");
    }

    private static Texture FindBaseTexture(Material material, Renderer renderer)
    {
        if (material == null)
            return null;

        for (int i = 0; i < BaseTextureProperties.Length; i++)
        {
            string propertyName = BaseTextureProperties[i];
            if (!material.HasProperty(propertyName))
                continue;

            Texture texture = material.GetTexture(propertyName);
            if (IsTexture2D(texture))
                return texture;
        }

        try
        {
            string[] propertyNames = material.GetTexturePropertyNames();
            Texture best = null;
            int bestScore = 20;
            for (int i = 0; i < propertyNames.Length; i++)
            {
                string propertyName = propertyNames[i];
                Texture texture = material.GetTexture(propertyName);
                if (!IsTexture2D(texture))
                    continue;

                int score = ScoreBaseTextureName(propertyName + " " + texture.name);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = texture;
                }
            }

            if (best != null)
                return best;
        }
        catch
        {
        }

        return FindNearbyBaseTexture(material, renderer);
    }

    private static Color FindMaterialColor(Material material, Color fallback)
    {
        if (material == null)
            return fallback;
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return fallback;
    }

    private static bool HasUsableTexture(Material material)
    {
        if (material == null)
            return false;

        return FindBaseTexture(material, null) != null;
    }

    private static bool HasAssignedBaseTexture(Material material)
    {
        if (material == null)
            return false;

        for (int i = 0; i < BaseTextureProperties.Length; i++)
        {
            string propertyName = BaseTextureProperties[i];
            if (!material.HasProperty(propertyName))
                continue;

            if (IsTexture2D(material.GetTexture(propertyName)))
                return true;
        }

        try
        {
            return material.HasProperty("_MainTex") && IsTexture2D(material.mainTexture);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsTexture2D(Texture texture)
    {
        return texture is Texture2D;
    }

    private static Texture FindNearbyBaseTexture(Material material, Renderer renderer)
    {
        if (material == null)
            return null;

        string materialPath = AssetDatabase.GetAssetPath(material);
        if (string.IsNullOrWhiteSpace(materialPath))
            return null;

        string normalizedPath = materialPath.Replace('\\', '/');
        if (s_nearbyBaseTextureCache.TryGetValue(normalizedPath, out Texture cached))
            return cached;

        string folder = Path.GetDirectoryName(normalizedPath)?.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(folder))
            return null;

        List<string> searchFolders = new List<string>();
        AddSearchFolder(searchFolders, folder);
        AddNearbyTextureSearchFolders(searchFolders, folder);

        string searchText = MakeTextureSearchText(material, renderer, normalizedPath);
        string[] tokens = searchText.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        Texture best = null;
        int bestScore = 35;
        for (int folderIndex = 0; folderIndex < searchFolders.Count; folderIndex++)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { searchFolders[folderIndex] });
            for (int i = 0; i < guids.Length; i++)
            {
                string texturePath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrWhiteSpace(texturePath))
                    continue;

                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(texturePath);
                if (texture == null)
                    continue;

                int score = ScoreNearbyBaseTexture(texturePath + " " + texture.name, tokens);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = texture;
                }
            }
        }

        s_nearbyBaseTextureCache[normalizedPath] = best;
        return best;
    }

    private static void AddNearbyTextureSearchFolders(List<string> searchFolders, string materialFolder)
    {
        string folder = NormalizeAssetPath(materialFolder);
        if (string.IsNullOrWhiteSpace(folder))
            return;

        string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string lowerFolder = folder.ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(parent) && (lowerFolder.Contains("standard shader materials") || lowerFolder.EndsWith("/materials")))
            AddSearchFolder(searchFolders, parent);

        string root = TrimSuffixIgnoreCase(folder, "/Models/Materials");
        if (!string.IsNullOrWhiteSpace(root))
            AddTextureFoldersUnderRoot(searchFolders, root);

        root = TrimSuffixIgnoreCase(folder, "/_Standard Shader Materials");
        if (!string.IsNullOrWhiteSpace(root))
            AddTextureFoldersUnderRoot(searchFolders, root);

        root = TrimSuffixIgnoreCase(folder, "/Standard Shader Materials");
        if (!string.IsNullOrWhiteSpace(root))
            AddTextureFoldersUnderRoot(searchFolders, root);

        root = TrimSuffixIgnoreCase(folder, "/Materials");
        if (string.IsNullOrWhiteSpace(root))
            return;

        AddTextureFoldersUnderRoot(searchFolders, root);
        if (root.EndsWith("/Models"))
        {
            string familyRoot = Path.GetDirectoryName(root)?.Replace('\\', '/');
            AddTextureFoldersUnderRoot(searchFolders, familyRoot);
        }
    }

    private static void AddTextureFoldersUnderRoot(List<string> searchFolders, string root)
    {
        root = NormalizeAssetPath(root);
        if (string.IsNullOrWhiteSpace(root) || !AssetDatabase.IsValidFolder(root))
            return;

        for (int i = 0; i < NearbyTextureFolderNames.Length; i++)
            AddSearchFolder(searchFolders, root + "/" + NearbyTextureFolderNames[i]);

        string[] subFolders = AssetDatabase.GetSubFolders(root);
        for (int i = 0; i < subFolders.Length; i++)
        {
            string subFolder = NormalizeAssetPath(subFolders[i]);
            string leaf = GetPathLeaf(subFolder).ToLowerInvariant();
            if (leaf.StartsWith("tex") || leaf.Contains("texture"))
                AddSearchFolder(searchFolders, subFolder);
        }
    }

    private static void AddSearchFolder(List<string> searchFolders, string folder)
    {
        folder = NormalizeAssetPath(folder);
        if (string.IsNullOrWhiteSpace(folder) || !AssetDatabase.IsValidFolder(folder) || searchFolders.Contains(folder))
            return;

        searchFolders.Add(folder);
    }

    private static string NormalizeAssetPath(string path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Replace('\\', '/').TrimEnd('/');
    }

    private static string TrimSuffixIgnoreCase(string value, string suffix)
    {
        if (string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(suffix))
            return null;

        return value.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase) ? value.Substring(0, value.Length - suffix.Length) : null;
    }

    private static string GetPathLeaf(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        int slash = path.LastIndexOf('/');
        return slash >= 0 ? path.Substring(slash + 1) : path;
    }

    private static string MakeTextureSearchText(Material material, Renderer renderer, string materialPath)
    {
        string text = (material != null ? material.name : string.Empty) + " " + materialPath;
        if (renderer != null)
        {
            text += " " + renderer.name;
            Transform current = renderer.transform;
            int depth = 0;
            while (current != null && depth < 6)
            {
                text += " " + current.name;
                current = current.parent;
                depth++;
            }
        }

        return ToTextureSearchText(text);
    }

    private static int ScoreNearbyBaseTexture(string textureText, string[] materialTokens)
    {
        string text = ToTextureSearchText(textureText);
        int score = ScoreBaseTextureName(text);
        if (materialTokens != null)
        {
            for (int i = 0; i < materialTokens.Length; i++)
            {
                string token = materialTokens[i];
                if (token.Length < 3 || ContainsAny(token, "material", "materials", "mat", "mesh", "renderer", "object", "gameobject", "prefab", "model", "models", "lod"))
                    continue;
                if (text.Contains(token))
                    score += token.Length >= 7 ? 36 : 18;
            }
        }

        return score;
    }

    private static int ScoreBaseTextureName(string textureText)
    {
        string text = ToTextureSearchText(textureText);
        if (ContainsAny(text, NonBaseTextureNameHints))
            return -100;

        int score = 0;
        if (ContainsAny(text, BaseTextureNameHints))
            score += 150;
        if (text.Contains("texture") || text.Contains("tex"))
            score += 10;
        return score;
    }

    private static string ToTextureSearchText(string value)
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

    private static bool LooksMagenta(Material material)
    {
        if (material == null)
            return false;

        Color color = Color.clear;
        bool hasColor = false;
        if (material.HasProperty("_BaseColor"))
        {
            color = material.GetColor("_BaseColor");
            hasColor = true;
        }
        else if (material.HasProperty("_Color"))
        {
            color = material.GetColor("_Color");
            hasColor = true;
        }

        return hasColor && color.r > 0.85f && color.b > 0.85f && color.g < 0.25f;
    }

    private static void FitAndGround(GameObject root, Vector3 anchor, float maxFootprint, float maxHeight)
    {
        if (!TryGetRendererBounds(root, out Bounds bounds))
            return;

        float footprint = Mathf.Max(bounds.size.x, bounds.size.z);
        float scaleFactor = 1f;
        if (footprint > 0.01f && maxFootprint > 0.01f && footprint > maxFootprint)
            scaleFactor = Mathf.Min(scaleFactor, maxFootprint / footprint);
        if (bounds.size.y > 0.01f && maxHeight > 0.01f && bounds.size.y > maxHeight)
            scaleFactor = Mathf.Min(scaleFactor, maxHeight / bounds.size.y);

        if (scaleFactor < 0.999f)
        {
            root.transform.localScale *= scaleFactor;
            if (!TryGetRendererBounds(root, out bounds))
                return;
        }

        Vector3 groundedCenter = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        root.transform.position += anchor - groundedCenter;
    }

    private static bool TryGetRendererBounds(GameObject root, out Bounds bounds)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bounds = default;
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer is ParticleSystemRenderer)
                continue;

            if (!initialized)
            {
                bounds = renderer.bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }
        return initialized;
    }

    private static void EnsureBoxCollider(GameObject target, Vector3 size, Vector3 center)
    {
        BoxCollider collider = target.GetComponent<BoxCollider>();
        if (collider == null)
            collider = target.AddComponent<BoxCollider>();
        collider.enabled = true;
        collider.size = size;
        collider.center = center;
    }

    private static void SetRendererColor(GameObject target, Color color)
    {
        Renderer renderer = target != null ? target.GetComponent<Renderer>() : null;
        if (renderer == null)
            return;

        Material material = GetOrCreateSceneMaterial(target.name, color, ShiftColor(color, 0.16f), 7f);
        renderer.sharedMaterial = material;
    }

    private static Material GetOrCreateSemanticMaterial(string key, Renderer renderer, string objectName)
    {
        string text = (key + " " + objectName + " " + (renderer != null ? renderer.name : string.Empty)).ToLowerInvariant();
        if (renderer is ParticleSystemRenderer || ContainsAny(text, "vfx", "particle", "fire", "flame", "ember", "magic", "electric", "storm", "heal"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Vfx", new Color(0.36f, 0.58f, 0.82f, 0.85f), new Color(0.68f, 0.84f, 1f, 0.85f), 4f, true);
        if (ContainsAny(text, "tree", "grass", "bush", "plant", "jungle", "forest", "moss"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Foliage", new Color(0.18f, 0.34f, 0.16f, 1f), new Color(0.38f, 0.52f, 0.23f, 1f), 7f);
        if (ContainsAny(text, "wood", "door", "table", "bookshelf", "bed", "chest", "mimic"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Wood", new Color(0.42f, 0.27f, 0.14f, 1f), new Color(0.66f, 0.44f, 0.25f, 1f), 5f);
        if (ContainsAny(text, "metal", "sword", "axe", "spear", "staff", "shield", "helmet", "ring", "lock"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Metal", new Color(0.50f, 0.52f, 0.53f, 1f), new Color(0.86f, 0.82f, 0.72f, 1f), 4f);
        if (ContainsAny(text, "human", "male", "skin", "body", "face"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Human", new Color(0.70f, 0.49f, 0.36f, 1f), new Color(0.88f, 0.66f, 0.50f, 1f), 4f);
        if (ContainsAny(text, "ice", "frost", "snow"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Ice", new Color(0.54f, 0.78f, 0.90f, 1f), new Color(0.88f, 0.97f, 1f, 1f), 7f);
        if (ContainsAny(text, "water", "tide", "ocean"))
            return GetOrCreateSceneMaterial("Fallback_Shared_Water", new Color(0.10f, 0.32f, 0.45f, 1f), new Color(0.28f, 0.54f, 0.68f, 1f), 6f);

        return GetOrCreateSceneMaterial("Fallback_Shared_Stone", new Color(0.43f, 0.39f, 0.33f, 1f), new Color(0.63f, 0.58f, 0.48f, 1f), 5f);
    }

    private static Material GetOrCreateSceneMaterial(string name, Color baseColor, Color detailColor, float tileScale, bool transparent = false)
    {
        string safeName = MakeSafeName(name);
        baseColor = SanitizePlaceholderColor(safeName, baseColor, false);
        detailColor = SanitizePlaceholderColor(safeName, detailColor, true);
        string materialPath = GeneratedMaterialFolder + "/" + safeName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find(transparent ? "Universal Render Pipeline/Particles/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find(transparent ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader);
            material.name = safeName;
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture2D texture = GetOrCreatePatternTexture(safeName + "_Tex", baseColor, detailColor);
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", transparent ? 0.08f : 0.34f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", name.ToLowerInvariant().Contains("metal") ? 0.25f : 0f);
        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", Vector2.one * Mathf.Max(1f, tileScale));
        if (material.HasProperty("_MainTex"))
            material.SetTextureScale("_MainTex", Vector2.one * Mathf.Max(1f, tileScale));
        if (transparent)
        {
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateSceneMaterialFromTexture(string name, string diffusePath, string normalPath, Color fallbackColor, float tileScale)
    {
        string safeName = MakeSafeName(name);
        string materialPath = GeneratedMaterialFolder + "/" + safeName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader);
            material.name = safeName;
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        if (diffuse == null)
            diffuse = GetOrCreatePatternTexture(safeName + "_FallbackTex", fallbackColor, ShiftColor(fallbackColor, 0.14f));

        if (material.HasProperty("_BaseMap"))
        {
            material.SetTexture("_BaseMap", diffuse);
            material.SetTextureScale("_BaseMap", Vector2.one * Mathf.Max(1f, tileScale));
        }
        if (material.HasProperty("_MainTex"))
        {
            material.SetTexture("_MainTex", diffuse);
            material.SetTextureScale("_MainTex", Vector2.one * Mathf.Max(1f, tileScale));
        }
        if (normal != null)
        {
            if (material.HasProperty("_BumpMap"))
                material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.16f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        material.DisableKeyword("_EMISSION");
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material GetOrCreateBillboardMaterial(string name, string texturePath, Color fallbackColor)
    {
        string safeName = MakeSafeName(name);
        string materialPath = GeneratedMaterialFolder + "/" + safeName + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            material = new Material(shader);
            material.name = safeName;
            AssetDatabase.CreateAsset(material, materialPath);
        }

        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (texture == null)
            texture = GetOrCreatePatternTexture(safeName + "_FallbackTex", fallbackColor, ShiftColor(fallbackColor, 0.18f));
        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", Color.white);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 1f);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", 0.38f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", 0f);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.08f);
        material.SetOverrideTag("RenderType", "TransparentCutout");
        material.EnableKeyword("_ALPHATEST_ON");
        material.renderQueue = 2450;
        EditorUtility.SetDirty(material);
        return material;
    }

    private static TerrainLayer GetOrCreateTerrainLayer(string name, string diffusePath, string normalPath, Color baseColor, Color detailColor, float tileSize)
    {
        string safeName = MakeSafeName(name);
        string path = GeneratedTerrainFolder + "/" + safeName + ".terrainlayer";
        TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null)
        {
            layer = new TerrainLayer();
            AssetDatabase.CreateAsset(layer, path);
        }

        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
        if (layer.diffuseTexture == null)
            layer.diffuseTexture = GetOrCreatePatternTexture("Terrain_" + safeName + "_Tex", baseColor, detailColor);
        layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        layer.tileSize = Vector2.one * Mathf.Max(1f, tileSize);
        layer.smoothness = 0.12f;
        layer.metallic = 0f;
        EditorUtility.SetDirty(layer);
        return layer;
    }

    private static Texture2D GetOrCreatePatternTexture(string name, Color baseColor, Color detailColor)
    {
        string safeName = MakeSafeName(name);
        baseColor = SanitizePlaceholderColor(safeName, baseColor, false);
        detailColor = SanitizePlaceholderColor(safeName, detailColor, true);
        string path = GeneratedMaterialFolder + "/" + safeName + ".asset";
        Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (texture == null)
        {
            texture = new Texture2D(64, 64, TextureFormat.RGBA32, true, false);
            texture.name = safeName;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            AssetDatabase.CreateAsset(texture, path);
        }

        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float hatch = ((x / 8 + y / 8) % 2 == 0) ? 0.0f : 0.12f;
                float grain = Mathf.PerlinNoise(x * 0.13f, y * 0.13f) * 0.22f;
                Color color = Color.Lerp(baseColor, detailColor, Mathf.Clamp01(hatch + grain));
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply(true, false);
        EditorUtility.SetDirty(texture);
        return texture;
    }

    private static Color NeutralPlaceholderColor()
    {
        return new Color(0.46f, 0.48f, 0.48f, 1f);
    }

    private static Color SanitizePlaceholderColor(string name, Color color, bool detail)
    {
        string text = (name ?? string.Empty).ToLowerInvariant();
        if (!ContainsAny(text, "missing", "placeholder", "fallback", "proxy"))
            return color;

        if (!LooksLikeAlertRed(color))
            return color;

        Color neutral = detail ? new Color(0.62f, 0.65f, 0.65f, color.a) : new Color(0.42f, 0.44f, 0.44f, color.a);
        return neutral;
    }

    private static bool LooksLikeAlertRed(Color color)
    {
        return color.r > 0.48f && color.r > color.g * 1.55f && color.r > color.b * 1.55f;
    }

    private static Color ShiftColor(Color color, float amount)
    {
        return new Color(Mathf.Clamp01(color.r + amount), Mathf.Clamp01(color.g + amount), Mathf.Clamp01(color.b + amount), color.a);
    }

    private static bool ContainsAny(string text, params string[] terms)
    {
        if (string.IsNullOrEmpty(text) || terms == null)
            return false;

        for (int i = 0; i < terms.Length; i++)
        {
            if (!string.IsNullOrEmpty(terms[i]) && text.Contains(terms[i]))
                return true;
        }
        return false;
    }

    private static string MakeSafeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Asset";

        char[] chars = value.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }
        return new string(chars);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingEditMode)
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                ScheduleConfigureWhenIdle();
            return;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        if (!LooksLikeHeavyAssetScene(activeScene))
            return;

        EditorApplication.isPlaying = false;
        if (activeScene.isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        ConfigureSafePlayScene();
        EditorSceneManager.OpenScene(SafeScenePath, OpenSceneMode.Single);
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
                EditorApplication.isPlaying = true;
        };
    }

    private static bool LooksLikeHeavyAssetScene(Scene scene)
    {
        if (!scene.IsValid())
            return false;

        string path = scene.path.Replace('\\', '/');
        if (path.Contains("YourQuest_AssetTest") || path.Contains("/_Recovery/"))
            return true;

        return GameObject.Find("03__World_AssetTest") != null;
    }

    private static void EnsureGeneratedAssetFolders()
    {
        EnsureFolder("Assets", "Assets");
        EnsureFolder("Assets/Assets", "Materials");
        EnsureFolder("Assets/Assets/Materials", "PlaySafe");
        EnsureFolder("Assets/Assets", "Terrain");
        EnsureFolder("Assets/Assets/Terrain", "PlaySafe");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string fullPath = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(fullPath))
            AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
