using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQVikingGoldenAssemblyExtractor
{
    private const string OutputRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage";

    private const string BuildingFolder =
        OutputRoot +
        "/Buildings";

    private const string StructureFolder =
        OutputRoot +
        "/Structures";

    private const string EdgeFolder =
        OutputRoot +
        "/Edges";

    private const string StreetFolder =
        OutputRoot +
        "/Streets";

    public const string ReviewScenePath =
        OutputRoot +
        "/YQ_Viking_GoldenAssemblyReview.unity";

    private const string ReviewGroundMaterialPath =
        OutputRoot +
        "/YQ_Viking_ReviewGround.mat";

    private sealed class SourceInstance
    {
        public GameObject sceneObject;
        public GameObject sourcePrefab;
        public string sourcePath;
        public string sourceName;
        public string family;
        public Bounds worldBounds;
    }

    private sealed class BuildingGroup
    {
        public string family;
        public SourceInstance anchor;
        public YQWorldAssemblyKind kind =
            YQWorldAssemblyKind.Building;
        public string compositionSignature;
        public int authoredOccurrenceCount = 1;
        public readonly List<SourceInstance> members =
            new List<SourceInstance>();
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Build First Viking Golden Building Candidates")]
    public static void BuildFirstVikingGoldenBuildingCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingGoldenAssemblyExtractor] " +
                "Golden assembly extraction requires stable Edit mode.");

            return;
        }

        EnsureFolderPath(
            BuildingFolder);

        Scene sourceScene =
            SceneManager.GetSceneByPath(
                YQVikingGoldenSourceAnalyzer.SourceScenePath);

        bool openedByExtractor =
            !sourceScene.IsValid() ||
            !sourceScene.isLoaded;

        List<string> generatedPrefabPaths =
            new List<string>();

        try
        {
            // note: Read authored transforms from the vendor URP scene additively; source objects are never reparented, saved, or modified.
            if (openedByExtractor)
            {
                sourceScene =
                    EditorSceneManager.OpenScene(
                        YQVikingGoldenSourceAnalyzer.SourceScenePath,
                        OpenSceneMode.Additive);
            }

            List<SourceInstance> sourceInstances =
                CollectSourceInstances(
                    sourceScene,
                    ResolveBuildingFamily);

            List<BuildingGroup> validatedGroups =
                BuildValidatedGroups(
                    sourceInstances);

            List<BuildingGroup> groups =
                CollapseEquivalentGroups(
                    validatedGroups);

            Dictionary<string, int> familyOrdinals =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);

            for (int index = 0;
                 index < groups.Count;
                 index++)
            {
                BuildingGroup group =
                    groups[index];

                familyOrdinals.TryGetValue(
                    group.family,
                    out int priorFamilyCount);

                int familyOrdinal =
                    priorFamilyCount + 1;

                familyOrdinals[group.family] =
                    familyOrdinal;

                string prefabPath =
                    SaveBuildingCandidate(
                        group,
                        familyOrdinal,
                        BuildingFolder);

                if (!string.IsNullOrWhiteSpace(prefabPath))
                {
                    generatedPrefabPaths.Add(
                        prefabPath);
                }
            }

            BuildReviewScene(
                generatedPrefabPaths);

            RemoveStaleCandidateAssets(
                generatedPrefabPaths,
                BuildingFolder);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset reviewScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    ReviewScenePath);

            if (reviewScene != null)
            {
                Selection.activeObject =
                    reviewScene;
            }

            Debug.Log(
                "[YQVikingGoldenAssemblyExtractor] GOLDEN BUILDING CANDIDATES READY\n" +
                "Validated authored placements: " +
                validatedGroups.Count +
                "\nUnique structural candidates: " +
                generatedPrefabPaths.Count +
                "\nEquivalent placements collapsed: " +
                (validatedGroups.Count - generatedPrefabPaths.Count) +
                "\nReview scene: " +
                ReviewScenePath +
                "\nRelease eligible: 0 (visual review required)");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
        finally
        {
            if (openedByExtractor &&
                sourceScene.IsValid() &&
                sourceScene.isLoaded)
            {
                EditorSceneManager.CloseScene(
                    sourceScene,
                    true);
            }
        }
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Build Viking Diverse Structure Candidates")]
    public static void BuildVikingDiverseStructureCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingGoldenAssemblyExtractor] " +
                "Diverse structure extraction requires stable Edit mode.");

            return;
        }

        EnsureFolderPath(
            StructureFolder);

        Scene sourceScene =
            SceneManager.GetSceneByPath(
                YQVikingGoldenSourceAnalyzer.SourceScenePath);

        bool openedByExtractor =
            !sourceScene.IsValid() ||
            !sourceScene.isLoaded;

        try
        {
            if (openedByExtractor)
            {
                // note: Read only the vendor scene's authored transforms; diverse candidates remain project-owned wrappers.
                sourceScene =
                    EditorSceneManager.OpenScene(
                        YQVikingGoldenSourceAnalyzer.SourceScenePath,
                        OpenSceneMode.Additive);
            }

            List<SourceInstance> sourceInstances =
                CollectSourceInstances(
                    sourceScene,
                    ResolveDiverseFamily);

            List<BuildingGroup> authoredGroups =
                BuildDiverseGroups(
                    sourceInstances);

            List<BuildingGroup> uniqueGroups =
                CollapseEquivalentGroups(
                    authoredGroups);

            Dictionary<string, int> familyOrdinals =
                new Dictionary<string, int>(
                    StringComparer.Ordinal);

            List<string> generatedPaths =
                new List<string>();

            for (int index = 0;
                 index < uniqueGroups.Count;
                 index++)
            {
                BuildingGroup group =
                    uniqueGroups[index];

                familyOrdinals.TryGetValue(
                    group.family,
                    out int priorFamilyCount);

                int familyOrdinal =
                    priorFamilyCount + 1;

                familyOrdinals[group.family] =
                    familyOrdinal;

                string path =
                    SaveBuildingCandidate(
                        group,
                        familyOrdinal,
                        StructureFolder);

                if (!string.IsNullOrWhiteSpace(path))
                {
                    generatedPaths.Add(
                        path);
                }
            }

            RemoveStaleCandidateAssets(
                generatedPaths,
                StructureFolder);

            List<string> allReviewPaths =
                FindAllGoldenCandidatePaths();

            BuildReviewScene(
                allReviewPaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[YQVikingGoldenAssemblyExtractor] DIVERSE STRUCTURE CANDIDATES READY\n" +
                "Validated authored compositions: " +
                authoredGroups.Count +
                "\nUnique structure candidates: " +
                generatedPaths.Count +
                "\nEquivalent placements collapsed: " +
                (authoredGroups.Count - generatedPaths.Count) +
                "\nCombined review candidates: " +
                allReviewPaths.Count +
                "\nReview scene: " +
                ReviewScenePath +
                "\nRelease eligible: 0 (visual review required)");
        }
        catch (Exception exception)
        {
            Debug.LogException(
                exception);
        }
        finally
        {
            if (openedByExtractor &&
                sourceScene.IsValid() &&
                sourceScene.isLoaded)
            {
                EditorSceneManager.CloseScene(
                    sourceScene,
                    true);
            }
        }
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Build Viking Edge and Street Cell Candidates")]
    public static void BuildVikingEdgeAndStreetCellCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingGoldenAssemblyExtractor] " +
                "Edge and street extraction requires stable Edit mode.");

            return;
        }

        EnsureFolderPath(EdgeFolder);
        EnsureFolderPath(StreetFolder);

        Scene sourceScene =
            SceneManager.GetSceneByPath(
                YQVikingGoldenSourceAnalyzer.SourceScenePath);

        bool openedByExtractor =
            !sourceScene.IsValid() ||
            !sourceScene.isLoaded;

        try
        {
            if (openedByExtractor)
            {
                // note: The vendor scene remains read-only evidence; generated cells preserve prefab references in project-owned wrappers.
                sourceScene =
                    EditorSceneManager.OpenScene(
                        YQVikingGoldenSourceAnalyzer.SourceScenePath,
                        OpenSceneMode.Additive);
            }

            List<SourceInstance> sourceInstances =
                CollectSourceInstances(
                    sourceScene,
                    ResolveEdgeStreetFamily);

            List<BuildingGroup> edgeGroups =
                CollapseEquivalentGroups(
                    BuildDefensiveEdgeGroups(sourceInstances));

            List<BuildingGroup> streetGroups =
                CollapseEquivalentGroups(
                    BuildElevatedStreetGroups(sourceInstances));

            List<string> edgePaths =
                SaveCandidateGroups(
                    edgeGroups,
                    EdgeFolder);

            List<string> streetPaths =
                SaveCandidateGroups(
                    streetGroups,
                    StreetFolder);

            RemoveStaleCandidateAssets(
                edgePaths,
                EdgeFolder);

            RemoveStaleCandidateAssets(
                streetPaths,
                StreetFolder);

            List<string> allReviewPaths =
                FindAllGoldenCandidatePaths();

            BuildReviewScene(
                allReviewPaths);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[YQVikingGoldenAssemblyExtractor] EDGE AND STREET CELLS READY\n" +
                "Unique defensive edge cells: " + edgePaths.Count +
                "\nUnique elevated street cells: " + streetPaths.Count +
                "\nCombined review candidates: " + allReviewPaths.Count +
                "\nReview scene: " + ReviewScenePath +
                "\nRelease eligible: 0 (visual review required)");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (openedByExtractor &&
                sourceScene.IsValid() &&
                sourceScene.isLoaded)
            {
                EditorSceneManager.CloseScene(
                    sourceScene,
                    true);
            }
        }
    }

    private static List<BuildingGroup> BuildDefensiveEdgeGroups(
        List<SourceInstance> sourceInstances)
    {
        List<SourceInstance> wallBases =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "StrongholdWallBase",
                        StringComparison.Ordinal));

        List<SourceInstance> oneSidedWalls =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "StrongholdWallOneSide",
                        StringComparison.Ordinal));

        wallBases.Sort(CompareSourcePosition);
        oneSidedWalls.Sort(CompareSourcePosition);

        List<BuildingGroup> groups =
            new List<BuildingGroup>();

        if (wallBases.Count > 0)
        {
            // note: A vendor wall-base prefab is already a complete repeatable segment; unattached floor pieces are intentionally excluded from this contract.
            BuildingGroup straight =
                CreateSingleMemberGroup(
                    "StrongholdStraight",
                    YQWorldAssemblyKind.Edge,
                    wallBases[0],
                    wallBases.Count);

            groups.Add(straight);
        }

        if (oneSidedWalls.Count > 0)
        {
            BuildingGroup oneSided =
                CreateSingleMemberGroup(
                    "StrongholdOneSide",
                    YQWorldAssemblyKind.Edge,
                    oneSidedWalls[0],
                    oneSidedWalls.Count);

            groups.Add(oneSided);
        }

        for (int leftIndex = 0;
             leftIndex < wallBases.Count;
             leftIndex++)
        {
            for (int rightIndex = leftIndex + 1;
                 rightIndex < wallBases.Count;
                 rightIndex++)
            {
                SourceInstance left = wallBases[leftIndex];
                SourceInstance right = wallBases[rightIndex];

                if (HorizontalTransformDistance(left, right) > 0.3f ||
                    !ArePerpendicular(left, right))
                {
                    continue;
                }

                // note: Coincident authored pivots with perpendicular rotations encode a deliberate perimeter corner, not an inferred proximity cluster.
                BuildingGroup corner =
                    new BuildingGroup
                    {
                        family = "StrongholdCorner",
                        anchor = left,
                        kind = YQWorldAssemblyKind.Edge
                    };

                corner.members.Add(left);
                corner.members.Add(right);
                groups.Add(corner);
            }
        }

        return groups;
    }

    private static List<BuildingGroup> BuildElevatedStreetGroups(
        List<SourceInstance> sourceInstances)
    {
        List<SourceInstance> pathway =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "ElevatedPathway",
                        StringComparison.Ordinal));

        List<SourceInstance> bodies =
            pathway.FindAll(
                instance => ContainsSourceToken(instance, "PathwayBody"));

        List<SourceInstance> stairs =
            pathway.FindAll(
                instance => ContainsSourceToken(instance, "PathwayStairs"));

        List<SourceInstance> sections =
            pathway.FindAll(
                instance => ContainsSourceToken(instance, "PathwaySection"));

        List<SourceInstance> opens =
            pathway.FindAll(
                instance => ContainsSourceToken(instance, "PathwayOpen"));

        bodies.Sort(CompareSourcePosition);
        opens.Sort(CompareSourcePosition);

        List<BuildingGroup> groups =
            new List<BuildingGroup>();

        for (int index = 0;
             index < bodies.Count;
             index++)
        {
            SourceInstance stair =
                FindNearestByTransform(
                    bodies[index],
                    stairs,
                    2.6f);

            SourceInstance section =
                FindNearestByTransform(
                    bodies[index],
                    sections,
                    2.6f);

            if (stair == null || section == null)
                continue;

            // note: This compact authored body/landing/stair composition becomes the repeatable straight access grammar cell.
            BuildingGroup access =
                new BuildingGroup
                {
                    family = "ElevatedAccess",
                    anchor = bodies[index],
                    kind = YQWorldAssemblyKind.Street
                };

            access.members.Add(bodies[index]);
            access.members.Add(section);
            access.members.Add(stair);
            groups.Add(access);
            break;
        }

        for (int index = 0;
             index < bodies.Count;
             index++)
        {
            List<SourceInstance> nearbyStairs =
                FindAllByTransformDistance(
                    bodies[index],
                    stairs,
                    4f);

            if (nearbyStairs.Count != 2)
                continue;

            // note: Exactly two authored stairs around one deck define a controlled rise/switchback cell without absorbing unrelated scenery.
            BuildingGroup rise =
                new BuildingGroup
                {
                    family = "ElevatedRise",
                    anchor = bodies[index],
                    kind = YQWorldAssemblyKind.Street
                };

            rise.members.Add(bodies[index]);
            rise.members.AddRange(nearbyStairs);
            groups.Add(rise);
            break;
        }

        if (opens.Count > 0)
        {
            // note: The authored open-deck prefab is a junction grammar cell and records its source frequency without duplicating equivalent placements.
            groups.Add(
                CreateSingleMemberGroup(
                    "ElevatedJunction",
                    YQWorldAssemblyKind.Street,
                    opens[0],
                    opens.Count));
        }

        return groups;
    }

    private static BuildingGroup CreateSingleMemberGroup(
        string family,
        YQWorldAssemblyKind kind,
        SourceInstance member,
        int authoredOccurrenceCount)
    {
        BuildingGroup group =
            new BuildingGroup
            {
                family = family,
                anchor = member,
                kind = kind,
                authoredOccurrenceCount = Mathf.Max(1, authoredOccurrenceCount)
            };

        group.members.Add(member);
        return group;
    }

    private static List<string> SaveCandidateGroups(
        List<BuildingGroup> groups,
        string outputFolder)
    {
        Dictionary<string, int> familyOrdinals =
            new Dictionary<string, int>(StringComparer.Ordinal);

        List<string> paths =
            new List<string>();

        for (int index = 0;
             index < groups.Count;
             index++)
        {
            BuildingGroup group = groups[index];

            familyOrdinals.TryGetValue(
                group.family,
                out int priorCount);

            int ordinal = priorCount + 1;
            familyOrdinals[group.family] = ordinal;

            string path =
                SaveBuildingCandidate(
                    group,
                    ordinal,
                    outputFolder);

            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        return paths;
    }

    private static List<BuildingGroup> BuildDiverseGroups(
        List<SourceInstance> sourceInstances)
    {
        List<BuildingGroup> result =
            new List<BuildingGroup>();

        AddStableComplexGroup(
            sourceInstances,
            result);

        AddWindmillGroups(
            sourceInstances,
            result);

        AddWatchtowerGroups(
            sourceInstances,
            result);

        AddOuthouseGroups(
            sourceInstances,
            result);

        AddCurvedBridgeGroup(
            sourceInstances,
            result);

        return result;
    }

    private static void AddStableComplexGroup(
        List<SourceInstance> sourceInstances,
        List<BuildingGroup> destination)
    {
        List<SourceInstance> stableInstances =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "StableComplex",
                        StringComparison.Ordinal));

        List<SourceInstance> roofs =
            stableInstances.FindAll(
                instance =>
                    ContainsSourceToken(
                        instance,
                        "Roof"));

        roofs.Sort(
            CompareSourcePosition);

        if (roofs.Count < 3)
            return;

        BuildingGroup group =
            new BuildingGroup
            {
                family = "StableComplex",
                anchor = roofs[0],
                kind = YQWorldAssemblyKind.Building
            };

        for (int index = 0;
             index < stableInstances.Count;
             index++)
        {
            if (DistanceToNearest(
                    stableInstances[index],
                    roofs) <= 9f)
            {
                group.members.Add(
                    stableInstances[index]);
            }
        }

        if (CountMembers(group, "Roof") >= 3 &&
            CountMembers(group, "Door") >= 3 &&
            CountMembers(group, "BackWall") >= 3)
        {
            destination.Add(
                group);
        }
    }

    private static void AddWindmillGroups(
        List<SourceInstance> sourceInstances,
        List<BuildingGroup> destination)
    {
        List<SourceInstance> windmillInstances =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "Windmill",
                        StringComparison.Ordinal));

        List<SourceInstance> bases =
            windmillInstances.FindAll(
                instance =>
                    ContainsSourceToken(
                        instance,
                        "_Base"));

        bases.Sort(
            CompareSourcePosition);

        for (int baseIndex = 0;
             baseIndex < bases.Count;
             baseIndex++)
        {
            BuildingGroup group =
                new BuildingGroup
                {
                    family = "Windmill",
                    anchor = bases[baseIndex],
                    kind = YQWorldAssemblyKind.Landmark
                };

            for (int memberIndex = 0;
                 memberIndex < windmillInstances.Count;
                 memberIndex++)
            {
                if (HorizontalDistance(
                        bases[baseIndex],
                        windmillInstances[memberIndex]) <= 5f)
                {
                    group.members.Add(
                        windmillInstances[memberIndex]);
                }
            }

            if (CountMembers(group, "_Base") == 1 &&
                CountMembers(group, "_Mid") >= 1 &&
                CountMembers(group, "MidDome") == 1 &&
                CountMembers(group, "_Head") == 1 &&
                CountMembers(group, "_Wing") == 1)
            {
                destination.Add(
                    group);
            }
        }
    }

    private static void AddWatchtowerGroups(
        List<SourceInstance> sourceInstances,
        List<BuildingGroup> destination)
    {
        List<SourceInstance> towers =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "Watchtower",
                        StringComparison.Ordinal));

        List<SourceInstance> windmillParts =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "Windmill",
                        StringComparison.Ordinal));

        List<SourceInstance> bases =
            towers.FindAll(
                instance =>
                    ContainsSourceToken(
                        instance,
                        "_Base"));

        List<SourceInstance> stairs =
            towers.FindAll(
                instance =>
                    ContainsSourceToken(
                        instance,
                        "Stairs"));

        HashSet<SourceInstance> usedStairs =
            new HashSet<SourceInstance>();

        bases.Sort(
            CompareSourcePosition);

        for (int baseIndex = 0;
             baseIndex < bases.Count;
             baseIndex++)
        {
            SourceInstance towerBase =
                bases[baseIndex];

            if (DistanceToNearest(
                    towerBase,
                    windmillParts) <= 5f)
            {
                // note: The central watchtower pieces are part of an incomplete experimental turbine hybrid, not a validated tower composition.
                continue;
            }

            SourceInstance body =
                FindNearestMatching(
                    towerBase,
                    towers,
                    "_Body",
                    null,
                    3f);

            SourceInstance stair =
                FindNearestMatching(
                    towerBase,
                    stairs,
                    "Stairs",
                    usedStairs,
                    12f);

            if (body == null ||
                stair == null)
            {
                continue;
            }

            usedStairs.Add(
                stair);

            BuildingGroup group =
                new BuildingGroup
                {
                    family = "Watchtower",
                    anchor = towerBase,
                    kind = YQWorldAssemblyKind.Landmark
                };

            group.members.Add(towerBase);
            group.members.Add(body);
            group.members.Add(stair);
            destination.Add(group);
        }
    }

    private static void AddOuthouseGroups(
        List<SourceInstance> sourceInstances,
        List<BuildingGroup> destination)
    {
        List<SourceInstance> outhouseInstances =
            sourceInstances.FindAll(
                instance =>
                    string.Equals(
                        instance.family,
                        "Outhouse",
                        StringComparison.Ordinal));

        List<SourceInstance> bodies =
            outhouseInstances.FindAll(
                instance =>
                    ContainsSourceToken(
                        instance,
                        "_Body"));

        List<SourceInstance> doors =
            outhouseInstances.FindAll(
                instance =>
                    ContainsSourceToken(
                        instance,
                        "_Door"));

        HashSet<SourceInstance> usedDoors =
            new HashSet<SourceInstance>();

        bodies.Sort(
            CompareSourcePosition);

        for (int bodyIndex = 0;
             bodyIndex < bodies.Count;
             bodyIndex++)
        {
            SourceInstance door =
                FindNearestMatching(
                    bodies[bodyIndex],
                    doors,
                    "_Door",
                    usedDoors,
                    3f);

            if (door == null)
                continue;

            usedDoors.Add(
                door);

            BuildingGroup group =
                new BuildingGroup
                {
                    family = "Outhouse",
                    anchor = bodies[bodyIndex],
                    kind = YQWorldAssemblyKind.Building
                };

            group.members.Add(bodies[bodyIndex]);
            group.members.Add(door);
            destination.Add(group);
        }
    }

    private static void AddCurvedBridgeGroup(
        List<SourceInstance> sourceInstances,
        List<BuildingGroup> destination)
    {
        SourceInstance bridge =
            sourceInstances.Find(
                instance =>
                    string.Equals(
                        instance.family,
                        "CurvedBridge",
                        StringComparison.Ordinal));

        if (bridge == null)
            return;

        BuildingGroup group =
            new BuildingGroup
            {
                family = "CurvedBridge",
                anchor = bridge,
                kind = YQWorldAssemblyKind.Street
            };

        group.members.Add(bridge);
        destination.Add(group);
    }

    private static SourceInstance FindNearestMatching(
        SourceInstance origin,
        List<SourceInstance> candidates,
        string sourceToken,
        HashSet<SourceInstance> excluded,
        float maximumDistance)
    {
        SourceInstance nearest = null;
        float nearestDistance = maximumDistance;

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            SourceInstance candidate =
                candidates[index];

            if ((excluded != null &&
                 excluded.Contains(candidate)) ||
                !ContainsSourceToken(
                    candidate,
                    sourceToken))
            {
                continue;
            }

            float distance =
                HorizontalDistance(
                    origin,
                    candidate);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidate;
            }
        }

        return nearest;
    }

    private static float DistanceToNearest(
        SourceInstance origin,
        List<SourceInstance> candidates)
    {
        float nearest = float.MaxValue;

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            nearest = Mathf.Min(
                nearest,
                HorizontalDistance(
                    origin,
                    candidates[index]));
        }

        return nearest;
    }

    private static float HorizontalDistance(
        SourceInstance left,
        SourceInstance right)
    {
        Vector3 leftCenter = left.worldBounds.center;
        Vector3 rightCenter = right.worldBounds.center;
        float deltaX = leftCenter.x - rightCenter.x;
        float deltaZ = leftCenter.z - rightCenter.z;

        return Mathf.Sqrt(
            deltaX * deltaX +
            deltaZ * deltaZ);
    }

    private static float HorizontalTransformDistance(
        SourceInstance left,
        SourceInstance right)
    {
        Vector3 leftPosition = left.sceneObject.transform.position;
        Vector3 rightPosition = right.sceneObject.transform.position;
        float deltaX = leftPosition.x - rightPosition.x;
        float deltaZ = leftPosition.z - rightPosition.z;

        return Mathf.Sqrt(
            deltaX * deltaX +
            deltaZ * deltaZ);
    }

    private static SourceInstance FindNearestByTransform(
        SourceInstance origin,
        List<SourceInstance> candidates,
        float maximumDistance)
    {
        SourceInstance nearest = null;
        float nearestDistance = maximumDistance;

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            float distance =
                HorizontalTransformDistance(
                    origin,
                    candidates[index]);

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = candidates[index];
            }
        }

        return nearest;
    }

    private static List<SourceInstance> FindAllByTransformDistance(
        SourceInstance origin,
        List<SourceInstance> candidates,
        float maximumDistance)
    {
        List<SourceInstance> result =
            new List<SourceInstance>();

        for (int index = 0;
             index < candidates.Count;
             index++)
        {
            if (HorizontalTransformDistance(
                    origin,
                    candidates[index]) <= maximumDistance)
            {
                result.Add(candidates[index]);
            }
        }

        result.Sort(CompareSourcePosition);
        return result;
    }

    private static bool ArePerpendicular(
        SourceInstance left,
        SourceInstance right)
    {
        float difference =
            Mathf.Abs(
                Mathf.DeltaAngle(
                    left.sceneObject.transform.eulerAngles.y,
                    right.sceneObject.transform.eulerAngles.y));

        return Mathf.Abs(difference - 90f) <= 1f;
    }

    private static bool ContainsSourceToken(
        SourceInstance instance,
        string token)
    {
        return instance != null &&
               instance.sourceName.IndexOf(
                   token,
                   StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<string> FindAllGoldenCandidatePaths()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[]
                {
                    BuildingFolder,
                    StructureFolder,
                    EdgeFolder,
                    StreetFolder
                });

        List<string> paths =
            new List<string>();

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]);

            if (!string.IsNullOrWhiteSpace(path))
            {
                paths.Add(path);
            }
        }

        paths.Sort(
            StringComparer.OrdinalIgnoreCase);

        return paths;
    }

    private static List<SourceInstance> CollectSourceInstances(
        Scene sourceScene,
        Func<string, string> familyResolver)
    {
        List<SourceInstance> result =
            new List<SourceInstance>();

        GameObject meshRoot = null;
        GameObject[] roots =
            sourceScene.GetRootGameObjects();

        for (int index = 0;
             index < roots.Length;
             index++)
        {
            if (roots[index] != null &&
                string.Equals(
                    roots[index].name,
                    "Meshes",
                    StringComparison.OrdinalIgnoreCase))
            {
                meshRoot = roots[index];
                break;
            }
        }

        if (meshRoot == null)
        {
            throw new InvalidOperationException(
                "The authored Viking scene has no Meshes root.");
        }

        Transform[] transforms =
            meshRoot.GetComponentsInChildren<Transform>(
                true);

        for (int index = 0;
             index < transforms.Length;
             index++)
        {
            Transform current =
                transforms[index];

            if (current == null ||
                !PrefabUtility.IsOutermostPrefabInstanceRoot(
                    current.gameObject))
            {
                continue;
            }

            string sourcePath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    current.gameObject);

            string sourceName =
                Path.GetFileNameWithoutExtension(
                    sourcePath ?? string.Empty);

            string family =
                familyResolver(
                    sourceName);

            if (string.IsNullOrWhiteSpace(family) ||
                sourceName.IndexOf(
                    "Rack",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            GameObject sourcePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    sourcePath);

            if (sourcePrefab == null)
                continue;

            result.Add(
                new SourceInstance
                {
                    sceneObject = current.gameObject,
                    sourcePrefab = sourcePrefab,
                    sourcePath = sourcePath,
                    sourceName = sourceName,
                    family = family,
                    worldBounds = CalculateRendererBounds(
                        current.gameObject)
                });
        }

        return result;
    }

    private static List<BuildingGroup> CollapseEquivalentGroups(
        List<BuildingGroup> validatedGroups)
    {
        Dictionary<string, BuildingGroup> representativeBySignature =
            new Dictionary<string, BuildingGroup>(
                StringComparer.Ordinal);

        List<BuildingGroup> result =
            new List<BuildingGroup>();

        for (int index = 0;
             index < validatedGroups.Count;
             index++)
        {
            BuildingGroup group =
                validatedGroups[index];

            string signature =
                BuildCompositionSignature(
                    group);

            group.compositionSignature =
                signature;

            if (representativeBySignature.TryGetValue(
                    signature,
                    out BuildingGroup representative))
            {
                // note: Repeated placements prove frequency and layout compatibility, but do not inflate the assembly library with visually redundant prefabs.
                representative.authoredOccurrenceCount++;
                continue;
            }

            representativeBySignature[signature] =
                group;

            result.Add(
                group);
        }

        return result;
    }

    private static string BuildCompositionSignature(
        BuildingGroup group)
    {
        Dictionary<string, int> countBySource =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index < group.members.Count;
             index++)
        {
            string sourceName =
                group.members[index].sourceName;

            countBySource.TryGetValue(
                sourceName,
                out int priorCount);

            countBySource[sourceName] =
                priorCount + 1;
        }

        List<string> sourceNames =
            new List<string>(
                countBySource.Keys);

        sourceNames.Sort(
            StringComparer.OrdinalIgnoreCase);

        StringBuilder signature =
            new StringBuilder(
                group.family);

        for (int index = 0;
             index < sourceNames.Count;
             index++)
        {
            signature.Append('|');
            signature.Append(sourceNames[index]);
            signature.Append(':');
            signature.Append(countBySource[sourceNames[index]]);
        }

        return signature.ToString();
    }

    private static List<BuildingGroup> BuildValidatedGroups(
        List<SourceInstance> sourceInstances)
    {
        string[] families =
        {
            "House2",
            "House3",
            "House4"
        };

        List<BuildingGroup> result =
            new List<BuildingGroup>();

        for (int familyIndex = 0;
             familyIndex < families.Length;
             familyIndex++)
        {
            string family =
                families[familyIndex];

            List<SourceInstance> familyInstances =
                sourceInstances.FindAll(
                    instance =>
                        string.Equals(
                            instance.family,
                            family,
                            StringComparison.Ordinal));

            List<SourceInstance> anchors =
                familyInstances.FindAll(
                    instance =>
                        instance.sourceName.IndexOf(
                            "Roof",
                            StringComparison.OrdinalIgnoreCase) >= 0);

            anchors.Sort(
                CompareSourcePosition);

            List<BuildingGroup> familyGroups =
                new List<BuildingGroup>();

            for (int anchorIndex = 0;
                 anchorIndex < anchors.Count;
                 anchorIndex++)
            {
                BuildingGroup group =
                    new BuildingGroup
                    {
                        family = family,
                        anchor = anchors[anchorIndex]
                    };

                familyGroups.Add(
                    group);
            }

            for (int instanceIndex = 0;
                 instanceIndex < familyInstances.Count;
                 instanceIndex++)
            {
                SourceInstance instance =
                    familyInstances[instanceIndex];

                BuildingGroup nearest =
                    FindNearestAnchorGroup(
                        instance,
                        familyGroups);

                if (nearest != null)
                {
                    nearest.members.Add(
                        instance);
                }
            }

            for (int groupIndex = 0;
                 groupIndex < familyGroups.Count;
                 groupIndex++)
            {
                BuildingGroup group =
                    familyGroups[groupIndex];

                if (IsValidBuildingGroup(
                        group))
                {
                    group.members.Sort(
                        (left, right) =>
                            string.Compare(
                                left.sourceName,
                                right.sourceName,
                                StringComparison.OrdinalIgnoreCase));

                    result.Add(
                        group);
                }
                else
                {
                    Debug.LogWarning(
                        "[YQVikingGoldenAssemblyExtractor] " +
                        "Rejected incomplete authored " +
                        family +
                        " group near " +
                        group.anchor.sceneObject.transform.position +
                        ".");
                }
            }
        }

        return result;
    }

    private static BuildingGroup FindNearestAnchorGroup(
        SourceInstance instance,
        List<BuildingGroup> groups)
    {
        BuildingGroup nearest = null;
        float nearestDistance = float.MaxValue;
        Vector3 position =
            instance.worldBounds.center;

        for (int index = 0;
             index < groups.Count;
             index++)
        {
            Vector3 anchorPosition =
                groups[index]
                    .anchor
                    .worldBounds
                    .center;

            float deltaX =
                position.x -
                anchorPosition.x;

            float deltaZ =
                position.z -
                anchorPosition.z;

            float distance =
                deltaX * deltaX +
                deltaZ * deltaZ;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = groups[index];
            }
        }

        return nearest;
    }

    private static bool IsValidBuildingGroup(
        BuildingGroup group)
    {
        int roofs = CountMembers(group, "Roof");
        int doors = CountMembers(group, "Door");

        if (roofs != 1)
        {
            return false;
        }

        if (string.Equals(
                group.family,
                "House2",
                StringComparison.Ordinal))
        {
            return doors == 1 &&
                   CountMembers(group, "BackWall") == 1 &&
                   CountMembers(group, "FrontWall") == 1 &&
                   CountMembers(group, "SideWall") == 2;
        }

        if (string.Equals(
                group.family,
                "House3",
                StringComparison.Ordinal))
        {
            return CountMembers(group, "Floor") == 1 &&
                   CountMembers(group, "SideWall") >= 2;
        }

        if (string.Equals(
                group.family,
                "House4",
                StringComparison.Ordinal))
        {
            return doors == 1 &&
                   CountMembers(group, "BackWall") == 1 &&
                   CountMembers(group, "FrontWall") == 1 &&
                   CountMembers(group, "SideWall") == 2;
        }

        return false;
    }

    private static int CountMembers(
        BuildingGroup group,
        string nameToken)
    {
        int count = 0;

        for (int index = 0;
             index < group.members.Count;
             index++)
        {
            if (group.members[index]
                    .sourceName
                    .IndexOf(
                        nameToken,
                        StringComparison.OrdinalIgnoreCase) >= 0)
            {
                count++;
            }
        }

        return count;
    }

    private static string SaveBuildingCandidate(
        BuildingGroup group,
        int familyOrdinal,
        string outputFolder)
    {
        Scene previewScene =
            EditorSceneManager.NewPreviewScene();

        try
        {
            string stableId =
                "assembly_viking_" +
                group.family.ToLowerInvariant() +
                "_" +
                familyOrdinal.ToString("00");

            GameObject root =
                new GameObject(
                    stableId);

            SceneManager.MoveGameObjectToScene(
                root,
                previewScene);

            Vector3 origin =
                ResolveGroupOrigin(
                    group);

            float canonicalYaw =
                group.anchor
                    .sceneObject
                    .transform
                    .eulerAngles
                    .y;

            Quaternion worldToCanonical =
                Quaternion.Euler(
                    0f,
                    -canonicalYaw,
                    0f);

            SourceInstance door = null;

            for (int index = 0;
                 index < group.members.Count;
                 index++)
            {
                SourceInstance member =
                    group.members[index];

                GameObject clone =
                    PrefabUtility.InstantiatePrefab(
                        member.sourcePrefab,
                        previewScene) as GameObject;

                if (clone == null)
                    continue;

                clone.name =
                    member.sourceName;

                clone.transform.SetParent(
                    root.transform,
                    false);

                Transform sourceTransform =
                    member.sceneObject.transform;

                clone.transform.localPosition =
                    worldToCanonical *
                    (sourceTransform.position - origin);

                clone.transform.localRotation =
                    worldToCanonical *
                    sourceTransform.rotation;

                clone.transform.localScale =
                    sourceTransform.lossyScale;

                bool isDoor =
                    member.sourceName.IndexOf(
                        "Door",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                bool isFallbackEntrance =
                    door == null &&
                    member.sourceName.IndexOf(
                        "Stairs",
                        StringComparison.OrdinalIgnoreCase) >= 0;

                if (isDoor ||
                    isFallbackEntrance)
                {
                    door = member;
                }
            }

            GameObject sockets =
                new GameObject("Sockets");

            sockets.transform.SetParent(
                root.transform,
                false);

            GameObject entrance =
                new GameObject("Entrance_Main");

            entrance.transform.SetParent(
                sockets.transform,
                false);

            if (door != null)
            {
                entrance.transform.localPosition =
                    worldToCanonical *
                    (door.sceneObject.transform.position - origin);

                entrance.transform.localPosition =
                    new Vector3(
                        entrance.transform.localPosition.x,
                        0f,
                        entrance.transform.localPosition.z);

                entrance.transform.localRotation =
                    worldToCanonical *
                    door.sceneObject.transform.rotation;
            }

            Bounds localBounds =
                CalculateLocalRendererBounds(
                    root);

            List<string> connectionSocketPaths =
                CreateConnectionSockets(
                    sockets.transform,
                    localBounds,
                    group,
                    worldToCanonical,
                    origin);

            YQWorldAssemblyDescriptor descriptor =
                root.AddComponent<YQWorldAssemblyDescriptor>();

            string kindTag =
                group.kind.ToString().ToLowerInvariant();

            descriptor.ConfigureExtractedCandidate(
                stableId,
                "assets_befourstudios_medievalvikingvillage",
                group.kind,
                group.family,
                group.compositionSignature,
                group.authoredOccurrenceCount,
                localBounds.center,
                localBounds.size,
                localBounds.size +
                new Vector3(2f, 1f, 2f),
                Vector3.forward,
                "Sockets/Entrance_Main",
                new[]
                {
                    "medieval",
                    "viking",
                    "settlement",
                    kindTag,
                    group.family.ToLowerInvariant()
                });

            descriptor.ConfigureConnectionSockets(
                connectionSocketPaths);

            string prefabPath =
                outputFolder +
                "/" +
                stableId +
                ".prefab";

            // note: Save a project-owned wrapper composed from prefab instances; vendor source GUIDs and materials remain intact and upgradeable.
            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(
                    root,
                    prefabPath);

            return saved != null
                ? prefabPath
                : string.Empty;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(
                previewScene);
        }
    }

    private static List<string> CreateConnectionSockets(
        Transform socketRoot,
        Bounds localBounds,
        BuildingGroup group,
        Quaternion worldToCanonical,
        Vector3 origin)
    {
        List<string> paths =
            new List<string>();

        if (group.kind != YQWorldAssemblyKind.Edge &&
            group.kind != YQWorldAssemblyKind.Street)
        {
            return paths;
        }

        if (string.Equals(
                group.family,
                "StrongholdCorner",
                StringComparison.Ordinal) &&
            group.members.Count == 2)
        {
            // note: Each perpendicular wall arm exposes its far authored endpoint, preserving the corner instead of approximating it as a straight bounding box.
            for (int index = 0;
                 index < group.members.Count;
                 index++)
            {
                SourceInstance member = group.members[index];
                Vector3 pivot =
                    worldToCanonical *
                    (member.sceneObject.transform.position - origin);

                Vector3 center =
                    worldToCanonical *
                    (member.worldBounds.center - origin);

                Vector3 outward = center - pivot;
                outward.y = 0f;

                if (outward.sqrMagnitude <= 0.0001f)
                    continue;

                outward.Normalize();

                float armLength =
                    Mathf.Max(
                        member.worldBounds.size.x,
                        member.worldBounds.size.z);

                string socketName =
                    index == 0
                        ? "Connection_A"
                        : "Connection_B";

                CreateConnectionSocket(
                    socketRoot,
                    socketName,
                    new Vector3(
                        pivot.x + outward.x * armLength,
                        localBounds.min.y,
                        pivot.z + outward.z * armLength),
                    outward);

                paths.Add("Sockets/" + socketName);
            }

            return paths;
        }

        bool runsAlongX =
            localBounds.size.x >= localBounds.size.z;

        Vector3 axis =
            runsAlongX
                ? Vector3.right
                : Vector3.forward;

        float halfLength =
            runsAlongX
                ? localBounds.extents.x
                : localBounds.extents.z;

        Vector3 socketCenter =
            new Vector3(
                localBounds.center.x,
                localBounds.min.y,
                localBounds.center.z);

        // note: Two outward-facing endpoint sockets let the deterministic compiler join cells while respecting their measured authored footprint.
        CreateConnectionSocket(
            socketRoot,
            "Connection_A",
            socketCenter - axis * halfLength,
            -axis);

        CreateConnectionSocket(
            socketRoot,
            "Connection_B",
            socketCenter + axis * halfLength,
            axis);

        paths.Add("Sockets/Connection_A");
        paths.Add("Sockets/Connection_B");
        return paths;
    }

    private static void CreateConnectionSocket(
        Transform parent,
        string socketName,
        Vector3 localPosition,
        Vector3 outwardDirection)
    {
        GameObject socket =
            new GameObject(socketName);

        socket.transform.SetParent(
            parent,
            false);

        socket.transform.localPosition = localPosition;
        socket.transform.localRotation =
            Quaternion.LookRotation(
                outwardDirection,
                Vector3.up);
    }

    private static void RemoveStaleCandidateAssets(
        List<string> generatedPrefabPaths,
        string generatedFolder)
    {
        HashSet<string> retained =
            new HashSet<string>(
                generatedPrefabPaths,
                StringComparer.OrdinalIgnoreCase);

        string[] existingGuids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { generatedFolder });

        for (int index = 0;
             index < existingGuids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    existingGuids[index]);

            string fileName =
                Path.GetFileNameWithoutExtension(
                    path);

            if (retained.Contains(path) ||
                !fileName.StartsWith(
                    "assembly_viking_",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // note: Remove only stale, reproducible candidates inside the dedicated generated folder so obsolete duplicates cannot later enter a catalog scan.
            AssetDatabase.DeleteAsset(
                path);
        }
    }

    private static void BuildReviewScene(
        List<string> generatedPrefabPaths)
    {
        Scene reviewScene =
            SceneManager.GetSceneByPath(
                ReviewScenePath);

        bool createdByBuilder =
            !reviewScene.IsValid() ||
            !reviewScene.isLoaded;

        if (createdByBuilder)
        {
            reviewScene =
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Additive);
        }
        else if (reviewScene.isDirty)
        {
            // note: Never erase unsaved designer changes in an already-open review scene; require an explicit save or revert first.
            throw new InvalidOperationException(
                "The generated Viking review scene is open with unsaved changes. " +
                "Save or discard those changes before rebuilding it.");
        }

        try
        {
            GameObject[] existingRoots =
                reviewScene.GetRootGameObjects();

            for (int index = 0;
                 index < existingRoots.Length;
                 index++)
            {
                if (existingRoots[index] != null &&
                    (string.Equals(
                         existingRoots[index].name,
                         "00__VIKING_GOLDEN_BUILDING_REVIEW",
                         StringComparison.Ordinal) ||
                     string.Equals(
                         existingRoots[index].name,
                         "00__VIKING_GOLDEN_ASSEMBLY_REVIEW",
                         StringComparison.Ordinal)))
                {
                    // note: Replace only the generated review hierarchy; unrelated saved objects in the scene are preserved.
                    UnityEngine.Object.DestroyImmediate(
                        existingRoots[index]);
                }
            }

            GameObject root =
                new GameObject("00__VIKING_GOLDEN_ASSEMBLY_REVIEW");

            SceneManager.MoveGameObjectToScene(
                root,
                reviewScene);

            GameObject ground =
                GameObject.CreatePrimitive(
                    PrimitiveType.Plane);

            ground.name = "Review Ground";
            ground.transform.SetParent(
                root.transform,
                false);
            ground.transform.localScale =
                new Vector3(14f, 1f, 10f);

            Material groundMaterial =
                GetOrCreateReviewGroundMaterial();

            Renderer groundRenderer =
                ground.GetComponent<Renderer>();

            if (groundRenderer != null &&
                groundMaterial != null)
            {
                groundRenderer.sharedMaterial =
                    groundMaterial;
            }

            GameObject lightObject =
                new GameObject("Review Sun");

            lightObject.transform.SetParent(
                root.transform,
                false);
            lightObject.transform.rotation =
                Quaternion.Euler(45f, -35f, 0f);

            Light light =
                lightObject.AddComponent<Light>();

            light.type = LightType.Directional;
            light.intensity = 1.2f;

            int columns = 6;
            float spacingX = 20f;
            float spacingZ = 24f;

            for (int index = 0;
                 index < generatedPrefabPaths.Count;
                 index++)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        generatedPrefabPaths[index]);

                if (prefab == null)
                    continue;

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        reviewScene) as GameObject;

                if (instance == null)
                    continue;

                instance.transform.SetParent(
                    root.transform,
                    false);

                int row = index / columns;
                int column = index % columns;

                instance.transform.localPosition =
                    new Vector3(
                        (column - (columns - 1) * 0.5f) * spacingX,
                        0f,
                        row * spacingZ - 24f);
            }

            // note: The review scene deliberately presents candidates in a neutral grid; it is a visual QA surface, not a runtime settlement.
            bool saved =
                EditorSceneManager.SaveScene(
                    reviewScene,
                    ReviewScenePath,
                    false);

            if (!saved)
            {
                throw new InvalidOperationException(
                    "Unity refused to save the generated Viking review scene.");
            }
        }
        finally
        {
            if (createdByBuilder)
            {
                EditorSceneManager.CloseScene(
                    reviewScene,
                    true);
            }
        }
    }

    private static Material GetOrCreateReviewGroundMaterial()
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(
                ReviewGroundMaterialPath);

        if (material != null)
            return material;

        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Lit");

        if (shader == null)
            return null;

        material =
            new Material(shader)
            {
                name = "YQ_Viking_ReviewGround"
            };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor(
                "_BaseColor",
                new Color(0.18f, 0.22f, 0.17f, 1f));
        }

        AssetDatabase.CreateAsset(
            material,
            ReviewGroundMaterialPath);

        return material;
    }

    private static Vector3 ResolveGroupOrigin(
        BuildingGroup group)
    {
        float minimumY =
            float.MaxValue;

        for (int index = 0;
             index < group.members.Count;
             index++)
        {
            minimumY = Mathf.Min(
                minimumY,
                group.members[index]
                    .worldBounds
                    .min
                    .y);
        }

        Vector3 anchorPosition =
            group.anchor
                .sceneObject
                .transform
                .position;

        return new Vector3(
            anchorPosition.x,
            minimumY,
            anchorPosition.z);
    }

    private static string ResolveBuildingFamily(
        string sourceName)
    {
        if (sourceName.StartsWith(
                "SM_House2_",
                StringComparison.OrdinalIgnoreCase))
            return "House2";

        if (sourceName.StartsWith(
                "SM_House3_",
                StringComparison.OrdinalIgnoreCase))
            return "House3";

        if (sourceName.StartsWith(
                "SM_House4_",
                StringComparison.OrdinalIgnoreCase))
            return "House4";

        return string.Empty;
    }

    private static string ResolveDiverseFamily(
        string sourceName)
    {
        if (sourceName.StartsWith(
                "SM_StableWooden_",
                StringComparison.OrdinalIgnoreCase))
            return "StableComplex";

        if (sourceName.StartsWith(
                "SM_WoodenTurbine_",
                StringComparison.OrdinalIgnoreCase))
            return "Windmill";

        if (sourceName.StartsWith(
                "SM_WoodenMiniWatchtower_",
                StringComparison.OrdinalIgnoreCase))
            return "Watchtower";

        if (sourceName.StartsWith(
                "SM_StructureWC_",
                StringComparison.OrdinalIgnoreCase))
            return "Outhouse";

        if (string.Equals(
                sourceName,
                "SM_WoodenBridgeBend",
                StringComparison.OrdinalIgnoreCase))
            return "CurvedBridge";

        return string.Empty;
    }

    private static string ResolveEdgeStreetFamily(
        string sourceName)
    {
        if (string.Equals(
                sourceName,
                "SM_StrongholdWallBase",
                StringComparison.OrdinalIgnoreCase))
            return "StrongholdWallBase";

        if (string.Equals(
                sourceName,
                "SM_StrongholdWallBase_OneSide",
                StringComparison.OrdinalIgnoreCase))
            return "StrongholdWallOneSide";

        if (sourceName.StartsWith(
                "SM_WoodenUpPathway_",
                StringComparison.OrdinalIgnoreCase) &&
            (sourceName.IndexOf(
                 "PathwayBody",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             sourceName.IndexOf(
                 "PathwayOpen",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             sourceName.IndexOf(
                 "PathwaySection",
                 StringComparison.OrdinalIgnoreCase) >= 0 ||
             sourceName.IndexOf(
                 "PathwayStairs",
                 StringComparison.OrdinalIgnoreCase) >= 0))
        {
            return "ElevatedPathway";
        }

        // note: Loose wall floors, fences, and monuments remain catalogued source modules but are excluded until a reviewed composition gives them a safe spatial contract.
        return string.Empty;
    }

    private static Bounds CalculateRendererBounds(
        GameObject root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        if (renderers.Length == 0)
        {
            return new Bounds(
                root.transform.position,
                Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;

        for (int index = 1;
             index < renderers.Length;
             index++)
        {
            if (renderers[index] != null)
            {
                bounds.Encapsulate(
                    renderers[index].bounds);
            }
        }

        return bounds;
    }

    private static Bounds CalculateLocalRendererBounds(
        GameObject root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(
                true);

        bool hasBounds = false;
        Bounds localBounds = default;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 localCenter =
                root.transform.InverseTransformPoint(
                    worldBounds.center);

            Bounds candidate =
                new Bounds(
                    localCenter,
                    worldBounds.size);

            if (!hasBounds)
            {
                localBounds = candidate;
                hasBounds = true;
            }
            else
            {
                localBounds.Encapsulate(
                    candidate.min);
                localBounds.Encapsulate(
                    candidate.max);
            }
        }

        return hasBounds
            ? localBounds
            : new Bounds(Vector3.zero, Vector3.zero);
    }

    private static int CompareSourcePosition(
        SourceInstance left,
        SourceInstance right)
    {
        Vector3 leftPosition =
            left.sceneObject.transform.position;

        Vector3 rightPosition =
            right.sceneObject.transform.position;

        int xComparison =
            leftPosition.x.CompareTo(
                rightPosition.x);

        return xComparison != 0
            ? xComparison
            : leftPosition.z.CompareTo(
                rightPosition.z);
    }

    private static void EnsureFolderPath(
        string path)
    {
        string normalized =
            path.Replace('\\', '/').Trim('/');

        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts =
            normalized.Split('/');

        string current = "Assets";

        for (int index = 1;
             index < parts.Length;
             index++)
        {
            string next =
                current +
                "/" +
                parts[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(
                    current,
                    parts[index]);
            }

            current = next;
        }
    }
}
