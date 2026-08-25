// Assets/Assets/Scripts/Tutorial/YourQuestTutorialWorldHelpers.cs
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

internal static class YourQuestTutorialWorldHelpers
{
    private const string TreePrefabA = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Tree.prefab";
    private const string TreePrefabB = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_TreeNeedles01.prefab";
    private const string TreePrefabC = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Tree_03.prefab";
    private const string TreePrefabD = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Alder.prefab";
    private const string TreePrefabE = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Mimosa.prefab";
    private const string BushPrefab = "Assets/YughuesFreeBushes2018/Prefabs/P_Bush01.prefab";
    private const string BushPrefabB = "Assets/YughuesFreeBushes2018/Prefabs/P_Bush03.prefab";
    private const string GrassPrefab = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Grass04.prefab";
    private const string FernPrefab = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Fern.prefab";
    private const string ThatchGrassPrefab = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_ThatchGrass03.prefab";
    private const string NordicBush = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Bush.prefab";
    private const string TerrainThinTree = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ThinTree.prefab";
    private const string TerrainSycamore = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/Sycamore.prefab";
    private const string TerrainPineA = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeA.prefab";
    private const string TerrainPineB = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Trees Ambient-Occlusion/ScotsPineTypeB.prefab";
    private const string TerrainRock = "Assets/Tom's Terrain Tools/Unity Terrain Assets/Rocks/RockMesh.prefab";
    private const string NordicRockA = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_RockSmall01.prefab";
    private const string NordicRockB = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_RockSmall02.prefab";
    private const string WesternRockA = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Rock06.prefab";
    private const string WesternRockB = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Rock07.prefab";
    private const string PersepolisFloor = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_FloorSetCustom_Base.prefab";
    private const string PersepolisWallArc = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_WallSideArc.prefab";
    private const string PersepolisGrassPatch = "Assets/BefourStudios/PersepolisEmpireEnvironment/Art/Prefabs/SM_Grasspatch_1.prefab";
    private const string AsianBuilding = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_Building01.prefab";
    private const string AsianPavilion = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_MiniPavilionPlatform.prefab";
    private const string AsianStairs = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_StairSet_1.prefab";
    private const string AsianDragon = "Assets/BefourStudios/AsianDynastyEnvironment/Art/Prefabs/SM_ChineseDragon_1.prefab";
    private const string MimicPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Mimics/MimicSimpleSmall.prefab";
    private const string LockpickPrefab = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/_Prefabs/LockpickA.prefab";
    private const string LockPrefab = "Assets/Magic Pig Games (Infinity PBR)/Tools & Systems/Locks and Lockpicking/_Prefabs/Lock1.prefab";
    private const string SpiderPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Spiders/_Prefabs/Spider 1.prefab";
    private const string PlantMonsterPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Plant Monster/_Prefabs/PlantMonster.prefab";
    private const string MushroomMonsterPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mushroom Monster/_Prefabs/Mushroom_v2.prefab";
    private const string DemonPrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Demons/_Prefabs/Demons.prefab";
    private const string ChestSimplePrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestSimpleSmall.prefab";
    private const string ChestOrnatePrefab = "Assets/Magic Pig Games (Infinity PBR)/Characters/Mimics & Chests/_Prefabs/Chests/ChestOrnateMedium.prefab";
    private const string VictorianBookshelf = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_Bookshelf_BIG.prefab";
    private const string VictorianBookPile = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_BookPile10.prefab";
    private const string VictorianCarpetLong = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_PersianCarpet_Long.prefab";
    private const string VictorianStudyTable = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_WoodenStudyTable.prefab";
    private const string VictorianFlowerPlate = "Assets/BefourStudios/VictorianMansionEnvironment/Art/Prefabs/SM_FlowerPlate.prefab";
    private const string NordicCrate = "Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_WoodenCrate.prefab";
    private const string VikingTowerBase = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_WoodenMiniWatchtower_Base.prefab";
    private const string VikingTowerBody = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_WoodenMiniWatchtower_Body.prefab";
    private const string VikingTowerStairs = "Assets/BefourStudios/MedievalVikingVillage/Art/Prefabs/SM_WoodenMiniWatchtower_Stairs.prefab";
    private const string WesternCaveEnd = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_CaveEnd01.prefab";
    private const string WesternCaveStraight = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_CaveStraight.prefab";
    private const string WesternChurch = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Church.prefab";
    private const string WesternGrassA = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Grass01.prefab";
    private const string WesternGrassB = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Grass02.prefab";
    private const string WesternGrassC = "Assets/BefourStudios/WesternDesertTown/Art/Prefabs/SM_Grass03.prefab";
    private const string WitchCabinPrefab = "Assets/HIVEMIND/HauntedVillage/HDRP (Default)/Art/Prefabs/SM_Cabin_01.prefab";
    private const string WitchCauldronPrefab = "Assets/HIVEMIND/WitchHouse/HDRP(Default)/Art/Prefabs/SM_HangingCauldron_lo_HangingCauldron_lo.prefab";
    private const string WitchNoticeBoardPrefab = "Assets/HIVEMIND/WitchHouse/HDRP(Default)/Art/Prefabs/SM_NoticeBoard.prefab";
    private const float RuntimeTerrainHalfSize = 148f;
    private const float RuntimeTerrainSize = RuntimeTerrainHalfSize * 2f;
    private const float RuntimeTerrainHeight = 34f;
    private const float RuntimeTerrainBaseY = -0.22f;

#if UNITY_EDITOR
    private static readonly Dictionary<string, GameObject> s_editorPrefabCache = new Dictionary<string, GameObject>();
#endif

    public static void CreateHut(Transform parent)
    {
        // note: Origin_Hut remains the stable save/tutorial identity, while its visible children present the Goddess threshold and Vey's witch hut.
        GameObject hut = new GameObject("Origin_Hut");
        hut.transform.SetParent(parent, false);

        CreateAssetProp(
            hut.transform,
            "Vey_Witch_Hut",
            WitchCabinPrefab,
            new Vector3(5.8f, 0.05f, 4.6f),
            new Vector3(0f, 205f, 0f),
            Vector3.one,
            new Color(0.24f, 0.18f, 0.13f, 1f),
            PrimitiveType.Cube,
            true,
            10.5f,
            7.2f);

        CreateAssetProp(
            hut.transform,
            "Witch_Hut_Cauldron",
            WitchCauldronPrefab,
            new Vector3(2.2f, 0.06f, 6.8f),
            new Vector3(0f, -18f, 0f),
            Vector3.one,
            new Color(0.18f, 0.20f, 0.18f, 1f),
            PrimitiveType.Cylinder,
            false,
            1.8f,
            1.5f);

        CreateAssetProp(
            hut.transform,
            "Witch_Hut_Notice_Board",
            WitchNoticeBoardPrefab,
            new Vector3(1.9f, 0.06f, 3.4f),
            new Vector3(0f, 112f, 0f),
            Vector3.one,
            new Color(0.27f, 0.18f, 0.12f, 1f),
            PrimitiveType.Cube,
            false,
            2.0f,
            2.2f);
    }

    public static void CreateForestScatter(Transform parent)
    {
        string[] treePrefabs = { TreePrefabA, TreePrefabB, TreePrefabD, TerrainPineA, TerrainPineB, TerrainSycamore, TerrainThinTree };
        Vector3[] treePositions =
        {
            new Vector3(-18f, 0f, -12f), new Vector3(-23f, 0f, -2f), new Vector3(-21f, 0f, 11f), new Vector3(-16f, 0f, 26f),
            new Vector3(-12f, 0f, 38f), new Vector3(-25f, 0f, 30f), new Vector3(16f, 0f, -13f), new Vector3(23f, 0f, -1f),
            new Vector3(22f, 0f, 12f), new Vector3(17f, 0f, 28f), new Vector3(11f, 0f, 39f), new Vector3(27f, 0f, 24f),
            new Vector3(-28f, 0f, -21f), new Vector3(-12f, 0f, -25f), new Vector3(12f, 0f, -25f), new Vector3(29f, 0f, -18f),
            new Vector3(-31f, 0f, 12f), new Vector3(31f, 0f, 12f)
        };

        for (int i = 0; i < treePositions.Length; i++)
        {
            float scale = 0.70f + (i % 4) * 0.10f;
            Vector3 position = GroundedTerrainPosition(treePositions[i], 0.02f);
            CreateAssetProp(parent, "Origin_Tree_" + i, treePrefabs[i % treePrefabs.Length], position, new Vector3(0f, i * 31f, 0f), Vector3.one * scale, new Color(0.18f, 0.38f, 0.20f, 1f), PrimitiveType.Cylinder, false, 5.2f, 8.2f);
        }

        string[] understoryPrefabs = { GrassPrefab, FernPrefab, BushPrefab, BushPrefabB, NordicBush, PersepolisGrassPatch };
        Vector3[] understoryPositions =
        {
            new Vector3(-10f, 0.02f, 7f), new Vector3(-14f, 0.02f, 15f), new Vector3(-16f, 0.02f, 22f), new Vector3(-9f, 0.02f, 31f),
            new Vector3(9f, 0.02f, 7f), new Vector3(14f, 0.02f, 15f), new Vector3(15f, 0.02f, 24f), new Vector3(8f, 0.02f, 32f),
            new Vector3(-6f, 0.02f, -9f), new Vector3(6f, 0.02f, -9f), new Vector3(-18f, 0.02f, -6f), new Vector3(18f, 0.02f, -5f),
            new Vector3(-22f, 0.02f, 20f), new Vector3(22f, 0.02f, 20f), new Vector3(-24f, 0.02f, -16f), new Vector3(24f, 0.02f, -16f),
            new Vector3(-30f, 0.02f, 4f), new Vector3(30f, 0.02f, 4f), new Vector3(-7f, 0.02f, 43f), new Vector3(7f, 0.02f, 43f)
        };

        for (int i = 0; i < understoryPositions.Length; i++)
        {
            string prefab = understoryPrefabs[i % understoryPrefabs.Length];
            PrimitiveType fallback = i % 3 == 0 ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
            Color color = i % 3 == 0 ? new Color(0.20f, 0.48f, 0.22f, 1f) : new Color(0.16f, 0.34f, 0.18f, 1f);
            Vector3 position = GroundedTerrainPosition(understoryPositions[i], 0.04f);
            CreateAssetProp(parent, "Origin_Understory_" + i, prefab, position, new Vector3(0f, i * 19f, 0f), Vector3.one * (0.48f + (i % 3) * 0.10f), color, fallback, false, 1.9f, 1.2f);
        }

        CreateDeepForestCanopy(parent);
        CreateForestFloorDetail(parent);
        CreateMountainRidgeDressing(parent);
    }

    private static void CreateDeepForestCanopy(Transform parent)
    {
        string[] trees = { TreePrefabA, TreePrefabB, TreePrefabC, TreePrefabD, TreePrefabE, TerrainPineA, TerrainPineB, TerrainSycamore, TerrainThinTree };
        int placed = 0;
        for (int i = 0; i < 90 && placed < 30; i++)
        {
            float x = Mathf.Lerp(-88f, 88f, Hash01(i, 3));
            float z = Mathf.Lerp(-86f, 98f, Hash01(i, 11));
            Vector3 candidate = new Vector3(x, 0f, z);
            if (!IsClearForForestTree(candidate))
                continue;

            float scale = 0.54f + Hash01(i, 17) * 0.58f;
            float maxHeight = Mathf.Lerp(7.2f, 11.8f, Hash01(i, 23));
            Color fallback = Color.Lerp(new Color(0.14f, 0.32f, 0.16f, 1f), new Color(0.24f, 0.42f, 0.25f, 1f), Hash01(i, 29));
            Vector3 position = GroundedTerrainPosition(candidate, 0.02f);
            CreateAssetProp(parent, "Origin_DeepForest_Tree_" + placed, trees[(i + placed) % trees.Length], position, new Vector3(0f, Hash01(i, 31) * 360f, 0f), Vector3.one * scale, fallback, PrimitiveType.Cylinder, false, 6.4f, maxHeight);
            placed++;
        }
    }

    private static void CreateForestFloorDetail(Transform parent)
    {
        string[] floorProps = { GrassPrefab, FernPrefab, ThatchGrassPrefab, BushPrefab, BushPrefabB, NordicBush, PersepolisGrassPatch };
        int placed = 0;
        for (int i = 0; i < 120 && placed < 40; i++)
        {
            float x = Mathf.Lerp(-82f, 82f, Hash01(i, 41));
            float z = Mathf.Lerp(-72f, 92f, Hash01(i, 47));
            Vector3 candidate = new Vector3(x, 0f, z);
            if (!IsClearForForestFloor(candidate))
                continue;

            string prefab = floorProps[(i + placed * 2) % floorProps.Length];
            float scale = 0.38f + Hash01(i, 53) * 0.36f;
            Color fallback = Color.Lerp(new Color(0.14f, 0.30f, 0.13f, 1f), new Color(0.34f, 0.52f, 0.22f, 1f), Hash01(i, 59));
            Vector3 position = GroundedTerrainPosition(candidate, 0.035f);
            CreateAssetProp(parent, "Origin_ForestFloor_" + placed, prefab, position, new Vector3(0f, Hash01(i, 61) * 360f, 0f), Vector3.one * scale, fallback, placed % 3 == 0 ? PrimitiveType.Sphere : PrimitiveType.Cylinder, false, 2.2f, 1.35f);
            placed++;
        }
    }

    private static void CreateMountainRidgeDressing(Transform parent)
    {
        string[] rocks = { TerrainRock, NordicRockA, NordicRockB, WesternRockA, WesternRockB };
        for (int i = 0; i < 14; i++)
        {
            float angle = (i * 41.0f + Hash01(i, 67) * 16f) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(90f, 136f, Hash01(i, 71));
            Vector3 candidate = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            if (TutorialPathFlatten(candidate.x, candidate.z) > 0.12f)
                candidate += candidate.normalized * 18f;

            Vector3 position = GroundedTerrainPosition(candidate, 0.04f);
            float scale = Mathf.Lerp(0.8f, 1.85f, Hash01(i, 73));
            Color fallback = Color.Lerp(new Color(0.23f, 0.25f, 0.23f, 1f), new Color(0.42f, 0.40f, 0.34f, 1f), Hash01(i, 79));
            CreateAssetProp(parent, "Distant_Ridge_Rock_" + i, rocks[i % rocks.Length], position, new Vector3(Hash01(i, 83) * 6f - 3f, Hash01(i, 89) * 360f, Hash01(i, 97) * 8f - 4f), Vector3.one * scale, fallback, PrimitiveType.Cube, false, 6.5f, 4.4f);
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

    private static Vector3 GroundedTerrainPosition(Vector3 position, float offset)
    {
        position.y = RuntimeTerrainBaseY + SampleTutorialTerrainHeight01(position.x, position.z) * RuntimeTerrainHeight + offset;
        return position;
    }

    private static float SampleTutorialTerrainHeight01(float wx, float wz)
    {
        float centerDistance = Mathf.Sqrt(wx * wx + wz * wz);
        float nx = (wx + RuntimeTerrainHalfSize) / RuntimeTerrainSize;
        float nz = (wz + RuntimeTerrainHalfSize) / RuntimeTerrainSize;
        float broadRise = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(44f, 132f, centerDistance)) * 0.075f;
        float mountainRing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(82f, RuntimeTerrainHalfSize, centerDistance)) * 0.17f;
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

    private static float Hash01(int seed, int salt)
    {
        float value = Mathf.Sin(seed * 12.9898f + salt * 78.233f) * 43758.5453f;
        return value - Mathf.Floor(value);
    }

    public static void CreateRegionDressing(Transform parent)
    {
        CreateRoadMarker(parent, new Vector3(0f, 0.085f, 54f), new Vector3(18f, 0.16f, 12f), new Color(0.45f, 0.64f, 0.70f, 1f));
        CreateRoadMarker(parent, new Vector3(54f, 0.085f, 0f), new Vector3(12f, 0.16f, 18f), new Color(0.32f, 0.27f, 0.21f, 1f));
        CreateRoadMarker(parent, new Vector3(0f, 0.085f, -54f), new Vector3(18f, 0.16f, 12f), new Color(0.15f, 0.32f, 0.17f, 1f));
        CreateRoadMarker(parent, new Vector3(-54f, 0.085f, 0f), new Vector3(12f, 0.16f, 18f), new Color(0.13f, 0.28f, 0.35f, 1f));

        CreateAssetProp(parent, "Practice_Lockpick_Worktable", VictorianStudyTable, GroundedTerrainPosition(new Vector3(-1.9f, 0f, 18.35f), 0.08f), new Vector3(0f, 8f, 0f), Vector3.one, new Color(0.35f, 0.23f, 0.14f, 1f), PrimitiveType.Cube, false, 2.4f, 1.05f);
        CreateAssetProp(parent, "Practice_Lock_Model", LockPrefab, GroundedTerrainPosition(new Vector3(-2.28f, 0f, 18.48f), 0.82f), new Vector3(0f, 18f, 0f), Vector3.one, new Color(0.55f, 0.44f, 0.25f, 1f), PrimitiveType.Cube, false, 1.05f, 0.9f);
        CreateAssetProp(parent, "Practice_Lockpick_Tool", LockpickPrefab, GroundedTerrainPosition(new Vector3(-1.35f, 0f, 18.18f), 0.84f), new Vector3(0f, -22f, 0f), Vector3.one, new Color(0.78f, 0.72f, 0.56f, 1f), PrimitiveType.Cube, false, 0.90f, 0.34f);

        Vector3[] pathStones =
        {
            new Vector3(-3.6f, 0.13f, 12.2f), new Vector3(3.8f, 0.12f, 13.8f), new Vector3(-3.4f, 0.12f, 22.8f), new Vector3(3.1f, 0.12f, 29.2f),
            new Vector3(-5.2f, 0.12f, 38.4f), new Vector3(5.4f, 0.12f, 39.8f), new Vector3(-5.8f, 0.12f, 50.6f), new Vector3(4.4f, 0.12f, 53.2f)
        };
        for (int i = 0; i < pathStones.Length; i++)
            CreateTraversalBlock(parent, "Tutorial_PathStone_" + i, pathStones[i], new Vector3(0.72f + (i % 3) * 0.18f, 0.10f, 0.42f + (i % 2) * 0.16f), new Color(0.22f, 0.24f, 0.22f, 1f));

        Vector3[] frostCrystals =
        {
            new Vector3(-6f, 0.75f, 53f), new Vector3(-3f, 0.95f, 59f), new Vector3(5f, 0.75f, 54f), new Vector3(8f, 1.05f, 60f)
        };
        for (int i = 0; i < frostCrystals.Length; i++)
            CreateTraversalBlock(parent, "Frost_Waystone_" + i, frostCrystals[i], new Vector3(0.8f, 1.5f + (i % 2) * 0.35f, 0.8f), new Color(0.62f, 0.88f, 0.94f, 1f));

        Vector3[] frostPines =
        {
            new Vector3(-13f, 0f, 57f), new Vector3(13f, 0f, 58f), new Vector3(-12f, 0f, 68f), new Vector3(12f, 0f, 70f)
        };
        for (int i = 0; i < frostPines.Length; i++)
            CreateAssetProp(parent, "Frost_Pine_" + i, TerrainPineA, frostPines[i], new Vector3(0f, i * 37f, 0f), Vector3.one * 0.62f, new Color(0.24f, 0.38f, 0.36f, 1f), PrimitiveType.Cylinder, false, 4.2f, 7.2f);

        CreateAssetProp(parent, "Ember_Trial_Dais", PersepolisFloor, new Vector3(54f, 0.12f, 0f), Vector3.zero, Vector3.one, new Color(0.34f, 0.28f, 0.21f, 1f), PrimitiveType.Cube, false, 10f, 1.1f);
        CreateAssetProp(parent, "Ember_Trial_Arch_North", PersepolisWallArc, new Vector3(54f, 0.14f, 6.5f), new Vector3(0f, 90f, 0f), Vector3.one * 0.72f, new Color(0.46f, 0.34f, 0.25f, 1f), PrimitiveType.Cube, false, 5.8f, 3.6f);
        CreateAssetProp(parent, "Ember_Trial_Arch_South", PersepolisWallArc, new Vector3(54f, 0.14f, -6.5f), new Vector3(0f, 90f, 0f), Vector3.one * 0.72f, new Color(0.46f, 0.34f, 0.25f, 1f), PrimitiveType.Cube, false, 5.8f, 3.6f);

        Vector3[] cinderStones =
        {
            new Vector3(47f, 0.45f, -7f), new Vector3(61f, 0.40f, -4f), new Vector3(48f, 0.36f, 7f), new Vector3(62f, 0.46f, 6f)
        };
        for (int i = 0; i < cinderStones.Length; i++)
            CreateTraversalBlock(parent, "Ember_SideStone_" + i, cinderStones[i], new Vector3(1.8f, 0.8f, 1.3f), new Color(0.34f, 0.30f, 0.25f, 1f));

        Vector3[] emberRunes =
        {
            new Vector3(52f, 0.16f, -8f), new Vector3(58f, 0.16f, -8f), new Vector3(52f, 0.16f, 8f), new Vector3(58f, 0.16f, 8f)
        };
        for (int i = 0; i < emberRunes.Length; i++)
            CreateTraversalBlock(parent, "Ember_RunePlate_" + i, emberRunes[i], new Vector3(1.1f, 0.12f, 1.1f), new Color(0.58f, 0.34f, 0.18f, 1f));

        CreateAssetProp(parent, "Auralith_Root_Pavilion", AsianPavilion, new Vector3(0f, 0.16f, -56f), new Vector3(0f, 12f, 0f), Vector3.one, new Color(0.24f, 0.42f, 0.22f, 1f), PrimitiveType.Cube, false, 7f, 3f);
        CreateAssetProp(parent, "Auralith_Root_Steps", AsianStairs, new Vector3(0f, 0.16f, -48f), new Vector3(0f, 180f, 0f), Vector3.one, new Color(0.25f, 0.28f, 0.22f, 1f), PrimitiveType.Cube, false, 4.5f, 1.8f);
        CreateAssetProp(parent, "Auralith_Root_Totem", AsianDragon, new Vector3(-8f, 0.16f, -58f), new Vector3(0f, 35f, 0f), Vector3.one, new Color(0.28f, 0.44f, 0.28f, 1f), PrimitiveType.Cube, false, 4f, 2.5f);

        Vector3[] verdantGrowth =
        {
            new Vector3(-7f, 0f, -50f), new Vector3(7f, 0f, -50f), new Vector3(-10f, 0f, -58f), new Vector3(10f, 0f, -58f), new Vector3(0f, 0f, -63f)
        };
        for (int i = 0; i < verdantGrowth.Length; i++)
            CreateAssetProp(parent, "Auralith_Root_Growth_" + i, i % 2 == 0 ? BushPrefab : GrassPrefab, verdantGrowth[i], Vector3.zero, Vector3.one * 0.48f, new Color(0.18f, 0.44f, 0.20f, 1f), PrimitiveType.Sphere, false, 1.8f, 1.2f);

        Vector3[] tideRocks =
        {
            new Vector3(-53f, 0.35f, -6f), new Vector3(-60f, 0.30f, -2f), new Vector3(-55f, 0.35f, 6f), new Vector3(-48f, 0.30f, 7f)
        };
        for (int i = 0; i < tideRocks.Length; i++)
            CreateTraversalBlock(parent, "Tide_SideRock_" + i, tideRocks[i], new Vector3(2.1f, 0.7f, 1.3f), new Color(0.18f, 0.32f, 0.38f, 1f));

        Vector3[] tidePools =
        {
            new Vector3(-58f, 0.11f, 8f), new Vector3(-50f, 0.11f, -8f), new Vector3(-64f, 0.11f, 5f)
        };
        for (int i = 0; i < tidePools.Length; i++)
            CreateTraversalBlock(parent, "Tide_Pool_" + i, tidePools[i], new Vector3(2.8f, 0.08f, 1.55f), new Color(0.10f, 0.34f, 0.44f, 1f));

        CreateArchivistNook(parent);
        CreateLandmarkBuildings(parent);
        CreateHiddenDiscoveries(parent);
        CreateCuratedGroundCover(parent);
    }

    private static void CreateArchivistNook(Transform parent)
    {
        CreateAssetProp(parent, "Archivist_Field_Rug", VictorianCarpetLong, GroundedTerrainPosition(new Vector3(-6.3f, 0f, 6.2f), 0.09f), new Vector3(0f, 86f, 0f), Vector3.one, new Color(0.42f, 0.18f, 0.14f, 1f), PrimitiveType.Cube, false, 3.6f, 0.14f);
        CreateAssetProp(parent, "Archivist_Field_Bookshelf", VictorianBookshelf, GroundedTerrainPosition(new Vector3(-7.35f, 0f, 5.1f), 0.04f), new Vector3(0f, 96f, 0f), Vector3.one, new Color(0.30f, 0.22f, 0.15f, 1f), PrimitiveType.Cube, false, 2.35f, 2.4f);
        CreateAssetProp(parent, "Archivist_Field_Table", VictorianStudyTable, GroundedTerrainPosition(new Vector3(-5.45f, 0f, 6.1f), 0.08f), new Vector3(0f, -7f, 0f), Vector3.one, new Color(0.30f, 0.20f, 0.12f, 1f), PrimitiveType.Cube, false, 2.0f, 1.0f);
        CreateAssetProp(parent, "Archivist_Field_BookPile", VictorianBookPile, GroundedTerrainPosition(new Vector3(-5.42f, 0f, 6.0f), 0.78f), new Vector3(0f, 18f, 0f), Vector3.one, new Color(0.62f, 0.48f, 0.32f, 1f), PrimitiveType.Cube, false, 0.64f, 0.34f);
        CreateAssetProp(parent, "Archivist_Supply_Crate", NordicCrate, GroundedTerrainPosition(new Vector3(-7.2f, 0f, 7.9f), 0.04f), new Vector3(0f, -18f, 0f), Vector3.one, new Color(0.34f, 0.22f, 0.14f, 1f), PrimitiveType.Cube, false, 0.95f, 0.82f);
    }

    private static void CreateLandmarkBuildings(Transform parent)
    {
        CreateAssetProp(parent, "Frostglass_Warden_Lookout_Base", VikingTowerBase, GroundedTerrainPosition(new Vector3(16.5f, 0f, 64.5f), 0.06f), new Vector3(0f, -18f, 0f), Vector3.one, new Color(0.31f, 0.32f, 0.28f, 1f), PrimitiveType.Cube, false, 5.0f, 2.2f);
        CreateAssetProp(parent, "Frostglass_Warden_Lookout_Body", VikingTowerBody, GroundedTerrainPosition(new Vector3(16.5f, 0f, 64.5f), 1.0f), new Vector3(0f, -18f, 0f), Vector3.one, new Color(0.36f, 0.34f, 0.28f, 1f), PrimitiveType.Cube, false, 4.4f, 4.2f);
        CreateAssetProp(parent, "Frostglass_Warden_Lookout_Stairs", VikingTowerStairs, GroundedTerrainPosition(new Vector3(13.8f, 0f, 61.8f), 0.06f), new Vector3(0f, -18f, 0f), Vector3.one, new Color(0.30f, 0.26f, 0.20f, 1f), PrimitiveType.Cube, false, 3.6f, 2.2f);
        CreateLockpickChest(parent, "Warden's Overlook Cache", GroundedTerrainPosition(new Vector3(18.6f, 0f, 66.8f), 0.10f), "region_ice_north", false, 0.36f, 34, ChestOrnatePrefab);

        CreateAssetProp(parent, "Cinderfall_Field_Chapel", WesternChurch, GroundedTerrainPosition(new Vector3(73f, 0f, 3.5f), 0.06f), new Vector3(0f, -92f, 0f), Vector3.one, new Color(0.48f, 0.34f, 0.24f, 1f), PrimitiveType.Cube, false, 9.5f, 6.2f);
        CreateLockpickChest(parent, "Chapel Reliquary", GroundedTerrainPosition(new Vector3(69.8f, 0f, 10.3f), 0.10f), "region_fire_east", false, 0.42f, 38, ChestOrnatePrefab);
        CreateSpawner(parent, GroundedTerrainPosition(new Vector3(76.2f, 0f, -1.8f), 1.0f), "ember_wilds", "region_fire_east", "Reliquary Cinderfiend", 1, DemonPrefab, new Color(0.88f, 0.34f, 0.18f, 1f), new Color(1f, 0.70f, 0.32f, 1f));

        CreateAssetProp(parent, "Auralith_Root_Sanctum", AsianBuilding, GroundedTerrainPosition(new Vector3(-12.4f, 0f, -70.5f), 0.08f), new Vector3(0f, 22f, 0f), Vector3.one, new Color(0.30f, 0.46f, 0.28f, 1f), PrimitiveType.Cube, false, 8.0f, 5.4f);
        CreateLockpickChest(parent, "Root-Sanctum Seedbox", GroundedTerrainPosition(new Vector3(-16.7f, 0f, -66.4f), 0.10f), "region_jungle_south", false, 0.38f, 32, ChestSimplePrefab);
        CreateSpawner(parent, GroundedTerrainPosition(new Vector3(-10.5f, 0f, -64.8f), 1.0f), "verdant_wilds", "region_jungle_south", "Sanctum Thornbound", 1, PlantMonsterPrefab, new Color(0.28f, 0.64f, 0.32f, 1f), new Color(0.70f, 0.88f, 0.42f, 1f));
    }

    private static void CreateHiddenDiscoveries(Transform parent)
    {
        CreateAssetProp(parent, "Moss_Hidden_Cave_Mouth", WesternCaveEnd, GroundedTerrainPosition(new Vector3(-39.5f, 0f, 38.5f), 0.04f), new Vector3(0f, 128f, 0f), Vector3.one, new Color(0.25f, 0.27f, 0.22f, 1f), PrimitiveType.Cube, false, 8.0f, 4.6f);
        CreateAssetProp(parent, "Moss_Hidden_Cave_Depth", WesternCaveStraight, GroundedTerrainPosition(new Vector3(-43.6f, 0f, 42.1f), 0.04f), new Vector3(0f, 128f, 0f), Vector3.one, new Color(0.18f, 0.20f, 0.18f, 1f), PrimitiveType.Cube, false, 7.0f, 4.0f);
        CreateLockpickChest(parent, "Moss-Hidden Cache", GroundedTerrainPosition(new Vector3(-36.0f, 0f, 35.6f), 0.10f), "origin_forest", false, 0.30f, 30, ChestSimplePrefab);
        CreateSpawner(parent, GroundedTerrainPosition(new Vector3(-42.5f, 0f, 40.5f), 1.0f), "verdant_wilds", "origin_forest", "Cavewake Sprout", 1, PlantMonsterPrefab, new Color(0.30f, 0.56f, 0.26f, 1f), new Color(0.64f, 0.82f, 0.38f, 1f));

        CreateAssetProp(parent, "Tideglass_Cleft_Cave", WesternCaveEnd, GroundedTerrainPosition(new Vector3(-78.0f, 0f, -18.5f), 0.05f), new Vector3(0f, 36f, 0f), Vector3.one, new Color(0.18f, 0.28f, 0.34f, 1f), PrimitiveType.Cube, false, 8.2f, 4.8f);
        CreateLockpickChest(parent, "Cleft-Tide Strongbox", GroundedTerrainPosition(new Vector3(-74.4f, 0f, -14.2f), 0.10f), "region_water_west", false, 0.44f, 42, ChestOrnatePrefab);
        CreateSpawner(parent, GroundedTerrainPosition(new Vector3(-80.5f, 0f, -16.0f), 1.0f), "tide_wilds", "region_water_west", "Cleft-Tide Skitter", 1, SpiderPrefab, new Color(0.24f, 0.62f, 0.78f, 1f), new Color(0.68f, 0.90f, 1f, 1f));

        CreateAssetProp(parent, "Frostcap_Smuggler_Crate", NordicCrate, GroundedTerrainPosition(new Vector3(-18.8f, 0f, 74.5f), 0.05f), new Vector3(0f, 28f, 0f), Vector3.one, new Color(0.32f, 0.26f, 0.18f, 1f), PrimitiveType.Cube, false, 0.92f, 0.82f);
        CreateLockpickChest(parent, "Frostcap Smuggler's Cache", GroundedTerrainPosition(new Vector3(-20.6f, 0f, 76.2f), 0.10f), "region_ice_north", false, 0.40f, 36, ChestSimplePrefab);
        CreateSpawner(parent, GroundedTerrainPosition(new Vector3(-17.2f, 0f, 78.2f), 1.0f), "frost_wilds", "region_ice_north", "Smuggler Frostcap", 1, MushroomMonsterPrefab, new Color(0.46f, 0.72f, 0.82f, 1f), new Color(0.82f, 0.95f, 1f, 1f));
    }

    private static void CreateCuratedGroundCover(Transform parent)
    {
        string[] prefabs = { GrassPrefab, FernPrefab, ThatchGrassPrefab, WesternGrassA, WesternGrassB, WesternGrassC, VictorianFlowerPlate, PersepolisGrassPatch, NordicBush };
        Vector3[] positions =
        {
            new Vector3(-11.5f, 0f, 4.8f), new Vector3(-14.8f, 0f, 8.4f), new Vector3(-17.5f, 0f, 16.0f), new Vector3(-15.4f, 0f, 27.4f),
            new Vector3(17.3f, 0f, 7.8f), new Vector3(19.5f, 0f, 18.6f), new Vector3(18.0f, 0f, 30.8f), new Vector3(8.4f, 0f, 45.4f),
            new Vector3(-12.5f, 0f, 47.6f), new Vector3(24.2f, 0f, 54.8f), new Vector3(-25.2f, 0f, 55.0f), new Vector3(34.5f, 0f, 18.2f),
            new Vector3(42.6f, 0f, -10.4f), new Vector3(50.2f, 0f, -16.5f), new Vector3(66.8f, 0f, 18.5f), new Vector3(75.5f, 0f, 11.8f),
            new Vector3(-34.0f, 0f, 22.6f), new Vector3(-47.8f, 0f, 16.8f), new Vector3(-68.2f, 0f, 16.5f), new Vector3(-83.2f, 0f, -11.8f),
            new Vector3(-19.2f, 0f, -42.4f), new Vector3(-24.8f, 0f, -55.8f), new Vector3(18.2f, 0f, -47.2f), new Vector3(24.2f, 0f, -63.5f),
            new Vector3(-34.0f, 0f, 36.2f), new Vector3(-46.8f, 0f, 44.2f), new Vector3(31.4f, 0f, -32.8f), new Vector3(37.8f, 0f, -45.8f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            string prefab = prefabs[i % prefabs.Length];
            float scale = 0.46f + (i % 4) * 0.08f;
            Color fallback = i % 5 == 0 ? new Color(0.52f, 0.42f, 0.28f, 1f) : new Color(0.20f, 0.44f, 0.20f, 1f);
            CreateAssetProp(parent, "Curated_GroundCover_" + i, prefab, GroundedTerrainPosition(positions[i], 0.035f), new Vector3(0f, i * 29f, 0f), Vector3.one * scale, fallback, PrimitiveType.Sphere, false, 1.7f, 1.15f);
        }
    }

    public static void CreateLockpickChest(Transform parent, string displayName, Vector3 position, string regionId, bool mimic, float difficulty, int gold, string prefabPath)
    {
        GameObject chest = CreateAssetProp(parent, displayName, prefabPath, position, Vector3.zero, Vector3.one, new Color(0.48f, 0.30f, 0.16f, 1f), PrimitiveType.Cube, true, 2.4f, 1.8f);
        if (chest == null)
            return;

        chest.name = displayName;
        YQLockpickableLoot loot = chest.GetComponent<YQLockpickableLoot>();
        if (loot == null)
            loot = chest.AddComponent<YQLockpickableLoot>();
        loot.displayName = displayName;
        loot.regionId = regionId;
        loot.mimic = mimic;
        loot.revealedMimicPrefabPath = mimic ? MimicPrefab : string.Empty;
        loot.locked = true;
        loot.lockDifficulty = difficulty;
        loot.gold = gold;
        loot.PrimeVisualState();

        EntityInfo info = chest.GetComponent<EntityInfo>();
        if (info == null)
            info = chest.AddComponent<EntityInfo>();
        info.entityId = "tutorial_" + NormalizeIdentifier(displayName);
        info.displayName = displayName;
        info.factionId = mimic ? "mimics" : "tutorial";
        info.hostility = Hostility.Neutral;
        info.isNotable = true;
        info.tags = mimic
            ? new[] { "mimic", "chest", "too quiet", "hostile", "tutorial" }
            : new[] { "chest", "loot", "lockpick", "practice", "tutorial" };
    }

    public static void CreateSpawner(Transform parent, Vector3 position, string factionId, string regionId, string displayName, int count, string prefabPath, Color primary, Color secondary)
    {
        GameObject root = new GameObject(displayName + " Spawner");
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        YQInvestorEnemySpawner spawner = root.AddComponent<YQInvestorEnemySpawner>();
        spawner.factionId = factionId;
        spawner.semanticRegionId = regionId;
        spawner.enemyDisplayName = displayName;
        spawner.enemyCount = count;
        spawner.enemyPrefabPath = prefabPath;
        spawner.allowImportedPrefabModelsInPlay = true;
        spawner.spawnRadius = 1.8f;
        spawner.requirePlayerNear = true;
        spawner.playerActivationDistance = string.Equals(regionId, "origin_forest", System.StringComparison.OrdinalIgnoreCase) ? 34f : 38f;
        spawner.playerFarDespawnDistance = spawner.playerActivationDistance + 24f;
        spawner.gatedSpawnRetryInterval = 1.65f;
        spawner.primaryColor = primary;
        spawner.secondaryColor = secondary;
        if (string.Equals(regionId, "origin_forest", System.StringComparison.OrdinalIgnoreCase))
            spawner.requiredCounter = "dialogue:npc_archivist_01";
        spawner.PrimeSpawnGate();
    }

    private static GameObject CreateHutPart(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(part.GetComponent<Renderer>(), color);
        return part;
    }

    private static string NormalizeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "entity";

        char[] chars = value.Trim().ToLowerInvariant().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (!char.IsLetterOrDigit(chars[i]))
                chars[i] = '_';
        }

        return new string(chars);
    }

    private static void CreateRoadMarker(Transform parent, Vector3 position, Vector3 scale, Color color)
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = "RoadMarker";
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
        marker.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(marker.GetComponent<Renderer>(), color);
    }

    private static void CreateTraversalBlock(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = name;
        block.transform.SetParent(parent, false);
        block.transform.position = position;
        block.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(block.GetComponent<Renderer>(), color);
    }

    private static GameObject CreateAssetProp(
        Transform parent,
        string name,
        string prefabPath,
        Vector3 position,
        Vector3 euler,
        Vector3 scale,
        Color fallbackColor,
        PrimitiveType fallbackType,
        bool keepColliders,
        float maxFootprint = 0f,
        float maxHeight = 0f)
    {
        // note: Runtime builds resolve imported props through the approved lazy registry; editor AssetDatabase lookup remains only a stale-registry recovery path.
        GameObject approvedPrefab = YQRuntimeWorldAssetRegistry.Instance != null
            ? YQRuntimeWorldAssetRegistry.Instance.ResolvePrefab(prefabPath)
            : null;

        if (approvedPrefab != null)
        {
            GameObject approvedInstance = UnityEngine.Object.Instantiate(approvedPrefab);
            approvedInstance.name = name;
            approvedInstance.transform.SetParent(parent, false);
            approvedInstance.transform.position = position;
            approvedInstance.transform.rotation = Quaternion.Euler(euler);
            approvedInstance.transform.localScale = scale;
            PrepareWorldProp(approvedInstance, keepColliders);
            FitAndGround(approvedInstance, position, maxFootprint, maxHeight);
            return approvedInstance;
        }

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(prefabPath))
        {
            GameObject prefab = LoadEditorPrefab(prefabPath);
            if (prefab != null)
            {
                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance != null)
                {
                    instance.name = name;
                    instance.transform.SetParent(parent, false);
                    instance.transform.position = position;
                    instance.transform.rotation = Quaternion.Euler(euler);
                    instance.transform.localScale = scale;
                    PrepareWorldProp(instance, keepColliders);
                    FitAndGround(instance, position, maxFootprint, maxHeight);
                    return instance;
                }
            }
        }
#endif

        GameObject fallback = GameObject.CreatePrimitive(fallbackType);
        fallback.name = name;
        fallback.transform.SetParent(parent, false);
        fallback.transform.position = position;
        fallback.transform.rotation = Quaternion.Euler(euler);
        fallback.transform.localScale = scale;
        YQInvestorRuntimeVisuals.SetRendererColor(fallback.GetComponent<Renderer>(), fallbackColor);
        if (!keepColliders)
        {
            Collider collider = fallback.GetComponent<Collider>();
            if (collider != null)
                UnityEngine.Object.Destroy(collider);
        }
        return fallback;
    }

#if UNITY_EDITOR
    private static GameObject LoadEditorPrefab(string prefabPath)
    {
        if (string.IsNullOrWhiteSpace(prefabPath))
            return null;
        if (s_editorPrefabCache.TryGetValue(prefabPath, out GameObject cached))
            return cached;

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        s_editorPrefabCache[prefabPath] = prefab;
        return prefab;
    }
#endif

    private static void PrepareWorldProp(GameObject root, bool keepColliders)
    {
        if (root == null)
            return;

        Rigidbody[] bodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
                bodies[i].isKinematic = true;
        }

        if (!keepColliders)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].enabled = false;
            }
        }

        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null)
                behaviours[i].enabled = false;
        }

        YQRuntimeUrpMaterialRepair.RepairHierarchy(root);
        YQVisualStabilityDirector.StabilizeHierarchy(root);
    }

    private static void FitAndGround(GameObject root, Vector3 anchor, float maxFootprint, float maxHeight)
    {
        if (root == null || !TryGetRendererBounds(root, out Bounds bounds))
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
}
