using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQVikingAuthoredDistrictExtractor
{
    private const string OutputRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage";

    private const string DistrictFolder =
        OutputRoot + "/Districts";

    public const string ReviewScenePath =
        OutputRoot + "/YQ_Viking_AuthoredDistrictReview.unity";

    private static readonly string[] ExcludedTokens =
    {
        "tree", "grass", "bush", "plant", "flower", "terrain",
        "sky", "water", "directional", "postprocess", "volume"
    };

    private static readonly FamilyRule[] CohesiveFamilyRules =
    {
        new FamilyRule("SM_House1_", 11f),
        new FamilyRule("SM_House2_", 11f),
        new FamilyRule("SM_House3_", 11f),
        new FamilyRule("SM_House4_", 13f),
        new FamilyRule("SM_StableWooden_", 13f),
        new FamilyRule("SM_WoodenTurbine_", 11f),
        new FamilyRule("SM_WoodenMiniWatchtower_", 10f),
        new FamilyRule("SM_StructureWC_", 8f)
    };

    private static readonly DistrictDefinition[] DistrictDefinitions =
    {
        new DistrictDefinition(
            "yq_viking_district_west_homestead",
            "WestHomestead",
            YQDistrictFunction.Residential,
            new Vector2(-55f, 5f)),
        new DistrictDefinition(
            "yq_viking_district_central_village",
            "CentralVillage",
            YQDistrictFunction.MixedUse,
            new Vector2(-20f, 5f)),
        new DistrictDefinition(
            "yq_viking_district_southern_quarter",
            "SouthernQuarter",
            YQDistrictFunction.Defensive,
            new Vector2(-30f, -32f)),
        new DistrictDefinition(
            "yq_viking_district_eastern_works",
            "EasternWorks",
            YQDistrictFunction.Service,
            new Vector2(10f, -25f))
    };

    private sealed class SourceInstance
    {
        public GameObject sceneObject;
        public GameObject sourcePrefab;
        public string sourcePath;
        public string sourceName;
        public Bounds worldBounds;
        public int districtIndex = -1;
    }

    private sealed class FamilyRule
    {
        public readonly string prefix;
        public readonly float linkDistance;

        public FamilyRule(string newPrefix, float newLinkDistance)
        {
            prefix = newPrefix;
            linkDistance = newLinkDistance;
        }
    }

    private sealed class DistrictDefinition
    {
        public readonly string stableId;
        public readonly string displayName;
        public readonly YQDistrictFunction function;
        public readonly Vector2 anchor;

        public DistrictDefinition(
            string newStableId,
            string newDisplayName,
            YQDistrictFunction newFunction,
            Vector2 newAnchor)
        {
            stableId = newStableId;
            displayName = newDisplayName;
            function = newFunction;
            anchor = newAnchor;
        }
    }

    private sealed class DistrictBuildResult
    {
        public string prefabPath;
        public int sourceInstanceCount;
        public int buildingCount;
        public int dressingCount;
        public int removedMissingScriptCount;
        public Vector3 boundsSize;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Assembly Authoring/Extract Viking Authored District Candidates")]
    public static void ExtractVikingAuthoredDistrictCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingAuthoredDistrictExtractor] Wait for Unity to be idle in Edit mode before extracting districts.");
            return;
        }

        // note: Preserve the designer's currently active scene; the vendor source is read additively and never saved.
        Scene previousActiveScene =
            SceneManager.GetActiveScene();
        Scene sourceScene =
            SceneManager.GetSceneByPath(
                YQVikingGoldenSourceAnalyzer.SourceScenePath);
        bool openedSourceScene = !sourceScene.IsValid() || !sourceScene.isLoaded;

        try
        {
            if (openedSourceScene)
            {
                sourceScene = EditorSceneManager.OpenScene(
                    YQVikingGoldenSourceAnalyzer.SourceScenePath,
                    OpenSceneMode.Additive);
            }

            EnsureFolderPath(DistrictFolder);

            List<SourceInstance> sourceInstances =
                CollectSourceInstances(sourceScene);

            if (sourceInstances.Count == 0)
            {
                throw new InvalidOperationException(
                    "No authored Viking prefab instances were found under the Meshes root.");
            }

            int cohesiveGroupCount =
                AssignDistricts(sourceInstances);

            DeleteStaleGeneratedDistricts();

            List<DistrictBuildResult> results =
                new List<DistrictBuildResult>();

            for (int districtIndex = 0;
                 districtIndex < DistrictDefinitions.Length;
                 districtIndex++)
            {
                List<SourceInstance> members =
                    sourceInstances
                        .Where(instance =>
                            instance.districtIndex == districtIndex)
                        .OrderBy(instance => instance.sourcePath)
                        .ThenBy(instance => instance.sceneObject.transform.position.x)
                        .ThenBy(instance => instance.sceneObject.transform.position.z)
                        .ToList();

                results.Add(
                    BuildDistrictPrefab(
                        DistrictDefinitions[districtIndex],
                        members));
            }

            ValidateCompleteAssignment(
                sourceInstances,
                results);

            BuildReviewScene(results);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            StringBuilder report = new StringBuilder();
            report.AppendLine(
                "[YQVikingAuthoredDistrictExtractor] AUTHORED DISTRICT CANDIDATES READY");
            report.AppendLine(
                "Source instances preserved: " + sourceInstances.Count);
            report.AppendLine(
                "Cohesive building groups: " + cohesiveGroupCount);
            report.AppendLine(
                "Split cohesive groups: 0");

            for (int index = 0; index < results.Count; index++)
            {
                report.AppendLine(
                    DistrictDefinitions[index].displayName +
                    ": " + results[index].sourceInstanceCount +
                    " instances (" + results[index].buildingCount +
                    " structural, " + results[index].dressingCount +
                    " dressing, " + results[index].removedMissingScriptCount +
                    " broken vendor behaviours removed)");
            }

            report.AppendLine("Review scene: " + ReviewScenePath);
            report.AppendLine("Release eligible: 0 (visual review required)");
            Debug.Log(report.ToString());
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
        finally
        {
            if (openedSourceScene && sourceScene.IsValid() && sourceScene.isLoaded)
            {
                EditorSceneManager.CloseScene(sourceScene, true);
            }

            if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(previousActiveScene);
            }
        }
    }

    private static List<SourceInstance> CollectSourceInstances(Scene sourceScene)
    {
        GameObject meshRoot = null;
        GameObject[] roots = sourceScene.GetRootGameObjects();

        for (int index = 0; index < roots.Length; index++)
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

        List<SourceInstance> result = new List<SourceInstance>();
        Transform[] transforms =
            meshRoot.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            Transform current = transforms[index];

            if (current == null ||
                !PrefabUtility.IsOutermostPrefabInstanceRoot(current.gameObject))
            {
                continue;
            }

            string sourcePath =
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(
                    current.gameObject);
            string sourceName =
                Path.GetFileNameWithoutExtension(sourcePath ?? string.Empty);

            if (string.IsNullOrWhiteSpace(sourcePath) ||
                IsExcludedSource(sourceName))
            {
                continue;
            }

            GameObject sourcePrefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);

            if (sourcePrefab == null)
                continue;

            result.Add(
                new SourceInstance
                {
                    sceneObject = current.gameObject,
                    sourcePrefab = sourcePrefab,
                    sourcePath = sourcePath,
                    sourceName = sourceName,
                    worldBounds = CalculateRendererBounds(current.gameObject)
                });
        }

        return result;
    }

    private static bool IsExcludedSource(string sourceName)
    {
        for (int index = 0; index < ExcludedTokens.Length; index++)
        {
            if (sourceName.IndexOf(
                    ExcludedTokens[index],
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static int AssignDistricts(List<SourceInstance> instances)
    {
        // note: Assign loose dressing by authored position, then overwrite complete modular families as indivisible connected groups.
        for (int index = 0; index < instances.Count; index++)
        {
            instances[index].districtIndex =
                FindNearestDistrict(
                    instances[index].sceneObject.transform.position);
        }

        int cohesiveGroupCount = 0;

        for (int ruleIndex = 0;
             ruleIndex < CohesiveFamilyRules.Length;
             ruleIndex++)
        {
            FamilyRule rule = CohesiveFamilyRules[ruleIndex];
            List<SourceInstance> familyMembers =
                instances
                    .Where(instance =>
                        instance.sourceName.StartsWith(
                            rule.prefix,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            List<List<SourceInstance>> groups =
                BuildConnectedGroups(
                    familyMembers,
                    rule.linkDistance);

            for (int groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                List<SourceInstance> group = groups[groupIndex];

                if (group.Count == 0)
                    continue;

                Vector3 centroid = Vector3.zero;

                for (int memberIndex = 0;
                     memberIndex < group.Count;
                     memberIndex++)
                {
                    centroid +=
                        group[memberIndex].sceneObject.transform.position;
                }

                centroid /= group.Count;
                int districtIndex = FindNearestDistrict(centroid);

                for (int memberIndex = 0;
                     memberIndex < group.Count;
                     memberIndex++)
                {
                    group[memberIndex].districtIndex = districtIndex;
                }

                cohesiveGroupCount++;
            }
        }

        return cohesiveGroupCount;
    }

    private static List<List<SourceInstance>> BuildConnectedGroups(
        List<SourceInstance> members,
        float linkDistance)
    {
        List<List<SourceInstance>> result =
            new List<List<SourceInstance>>();
        HashSet<SourceInstance> remaining =
            new HashSet<SourceInstance>(members);
        float maximumDistanceSquared = linkDistance * linkDistance;

        while (remaining.Count > 0)
        {
            SourceInstance seed = remaining.First();
            remaining.Remove(seed);

            List<SourceInstance> group =
                new List<SourceInstance> { seed };
            Queue<SourceInstance> frontier =
                new Queue<SourceInstance>();
            frontier.Enqueue(seed);

            while (frontier.Count > 0)
            {
                SourceInstance current = frontier.Dequeue();
                List<SourceInstance> linked =
                    remaining
                        .Where(candidate =>
                        {
                            Vector3 delta =
                                candidate.sceneObject.transform.position -
                                current.sceneObject.transform.position;
                            delta.y = 0f;
                            return delta.sqrMagnitude <= maximumDistanceSquared;
                        })
                        .ToList();

                for (int index = 0; index < linked.Count; index++)
                {
                    remaining.Remove(linked[index]);
                    group.Add(linked[index]);
                    frontier.Enqueue(linked[index]);
                }
            }

            result.Add(group);
        }

        return result;
    }

    private static int FindNearestDistrict(Vector3 worldPosition)
    {
        int bestIndex = 0;
        float bestDistanceSquared = float.PositiveInfinity;
        Vector2 point = new Vector2(worldPosition.x, worldPosition.z);

        for (int index = 0;
             index < DistrictDefinitions.Length;
             index++)
        {
            float distanceSquared =
                (point - DistrictDefinitions[index].anchor).sqrMagnitude;

            if (distanceSquared < bestDistanceSquared)
            {
                bestDistanceSquared = distanceSquared;
                bestIndex = index;
            }
        }

        return bestIndex;
    }

    private static DistrictBuildResult BuildDistrictPrefab(
        DistrictDefinition definition,
        List<SourceInstance> members)
    {
        if (members.Count == 0)
        {
            throw new InvalidOperationException(
                definition.displayName + " received no authored instances.");
        }

        Scene previewScene = EditorSceneManager.NewPreviewScene();

        try
        {
            GameObject root = new GameObject(definition.displayName);
            SceneManager.MoveGameObjectToScene(root, previewScene);

            Bounds worldBounds = members[0].worldBounds;

            for (int index = 1; index < members.Count; index++)
            {
                worldBounds.Encapsulate(members[index].worldBounds);
            }

            Vector3 origin = new Vector3(
                worldBounds.center.x,
                worldBounds.min.y,
                worldBounds.center.z);

            int buildingCount = 0;
            int removedMissingScriptCount = 0;

            for (int index = 0; index < members.Count; index++)
            {
                SourceInstance member = members[index];
                GameObject clone =
                    PrefabUtility.InstantiatePrefab(
                        member.sourcePrefab,
                        previewScene) as GameObject;

                if (clone == null)
                    continue;

                clone.name = member.sceneObject.name;
                clone.transform.SetParent(root.transform, false);

                Transform sourceTransform = member.sceneObject.transform;
                clone.transform.localPosition = sourceTransform.position - origin;
                clone.transform.localRotation = sourceTransform.rotation;
                clone.transform.localScale = sourceTransform.lossyScale;

                removedMissingScriptCount +=
                    RemoveMissingScriptsRecursively(clone);

                if (IsStructuralSource(member.sourceName))
                {
                    buildingCount++;
                }
            }

            Bounds localBounds = CalculateLocalRendererBounds(root);
            GameObject socketRoot = new GameObject("Sockets");
            socketRoot.transform.SetParent(root.transform, false);

            List<string> socketPaths =
                CreateDistrictSockets(
                    socketRoot.transform,
                    localBounds,
                    members,
                    origin);

            string signature = BuildCompositionSignature(members);
            YQWorldAssemblyDescriptor assembly =
                root.AddComponent<YQWorldAssemblyDescriptor>();

            assembly.ConfigureExtractedCandidate(
                definition.stableId,
                "assets_befourstudios_medievalvikingvillage",
                YQWorldAssemblyKind.District,
                definition.displayName,
                signature,
                1,
                localBounds.center,
                localBounds.size,
                localBounds.size + new Vector3(6f, 2f, 6f),
                Vector3.forward,
                string.Empty,
                new[]
                {
                    "medieval",
                    "viking",
                    "district",
                    definition.function.ToString().ToLowerInvariant(),
                    "authored-composition"
                });
            assembly.ConfigureConnectionSockets(socketPaths);

            YQWorldDistrictDescriptor district =
                root.AddComponent<YQWorldDistrictDescriptor>();
            district.Configure(
                definition.function,
                members.Count,
                buildingCount,
                members.Count - buildingCount,
                origin,
                localBounds.center,
                localBounds.size,
                socketPaths);

            string prefabPath =
                DistrictFolder + "/" + definition.stableId + ".prefab";

            // note: Save a project-owned wrapper while retaining prefab links to the vendor's authored geometry and materials.
            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            if (saved == null)
            {
                throw new InvalidOperationException(
                    "Failed to save district prefab " + prefabPath + ".");
            }

            return new DistrictBuildResult
            {
                prefabPath = prefabPath,
                sourceInstanceCount = members.Count,
                buildingCount = buildingCount,
                dressingCount = members.Count - buildingCount,
                removedMissingScriptCount = removedMissingScriptCount,
                boundsSize = localBounds.size
            };
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(previewScene);
        }
    }

    private static int RemoveMissingScriptsRecursively(GameObject root)
    {
        int removedCount = 0;
        Transform[] transforms =
            root.GetComponentsInChildren<Transform>(true);

        for (int index = 0; index < transforms.Length; index++)
        {
            GameObject current = transforms[index].gameObject;
            int missingCount =
                GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(
                    current);

            if (missingCount <= 0)
                continue;

            // note: Broken vendor behaviours cannot be serialized into project-owned district prefabs; removing only missing components preserves geometry, lights, materials, and valid scripts.
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(current);
            removedCount += missingCount;
        }

        return removedCount;
    }

    private static List<string> CreateDistrictSockets(
        Transform socketRoot,
        Bounds localBounds,
        List<SourceInstance> members,
        Vector3 origin)
    {
        string[] names =
        {
            "Connection_North",
            "Connection_East",
            "Connection_South",
            "Connection_West"
        };
        Vector3[] directions =
        {
            Vector3.forward,
            Vector3.right,
            Vector3.back,
            Vector3.left
        };
        List<string> paths = new List<string>();

        for (int index = 0; index < names.Length; index++)
        {
            GameObject socket = new GameObject(names[index]);
            socket.transform.SetParent(socketRoot, false);
            socket.transform.localPosition =
                FindPathEdgePosition(
                    localBounds,
                    members,
                    origin,
                    directions[index]);
            socket.transform.localRotation =
                Quaternion.LookRotation(directions[index], Vector3.up);
            paths.Add("Sockets/" + names[index]);
        }

        return paths;
    }

    private static Vector3 FindPathEdgePosition(
        Bounds bounds,
        List<SourceInstance> members,
        Vector3 origin,
        Vector3 direction)
    {
        Vector3 fallback = bounds.center + new Vector3(
            direction.x * bounds.extents.x,
            -bounds.center.y,
            direction.z * bounds.extents.z);
        SourceInstance best = null;
        float bestScore = float.PositiveInfinity;

        for (int index = 0; index < members.Count; index++)
        {
            SourceInstance candidate = members[index];

            if (candidate.sourceName.IndexOf(
                    "Path",
                    StringComparison.OrdinalIgnoreCase) < 0)
            {
                continue;
            }

            Vector3 local = candidate.sceneObject.transform.position - origin;
            float boundaryDistance = direction.x != 0f
                ? Mathf.Abs(fallback.x - local.x)
                : Mathf.Abs(fallback.z - local.z);
            float centerDistance = direction.x != 0f
                ? Mathf.Abs(local.z - bounds.center.z)
                : Mathf.Abs(local.x - bounds.center.x);
            float score = boundaryDistance + (centerDistance * 0.15f);

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best == null)
            return fallback;

        Vector3 result = best.sceneObject.transform.position - origin;
        result.y = 0f;
        return result;
    }

    private static bool IsStructuralSource(string sourceName)
    {
        return sourceName.IndexOf("House", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sourceName.IndexOf("Stable", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sourceName.IndexOf("Structure", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sourceName.IndexOf("Stronghold", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sourceName.IndexOf("Watchtower", StringComparison.OrdinalIgnoreCase) >= 0 ||
               sourceName.IndexOf("Turbine", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static void ValidateCompleteAssignment(
        List<SourceInstance> sourceInstances,
        List<DistrictBuildResult> results)
    {
        int assignedCount = results.Sum(result => result.sourceInstanceCount);

        if (assignedCount != sourceInstances.Count ||
            sourceInstances.Any(instance => instance.districtIndex < 0))
        {
            throw new InvalidOperationException(
                "District extraction did not preserve every source instance exactly once.");
        }

        for (int index = 0; index < results.Count; index++)
        {
            if (results[index].sourceInstanceCount < 50)
            {
                Debug.LogWarning(
                    "[YQVikingAuthoredDistrictExtractor] " +
                    DistrictDefinitions[index].displayName +
                    " contains only " + results[index].sourceInstanceCount +
                    " authored instances; inspect its visual density before approval.");
            }

            if (results[index].buildingCount == 0)
            {
                Debug.LogWarning(
                    "[YQVikingAuthoredDistrictExtractor] " +
                    DistrictDefinitions[index].displayName +
                    " has no recognized structural modules.");
            }
        }
    }

    private static void BuildReviewScene(List<DistrictBuildResult> results)
    {
        Scene existing = SceneManager.GetSceneByPath(ReviewScenePath);

        if (existing.IsValid() && existing.isLoaded)
        {
            // note: Closing the old generated review scene avoids Unity's same-path overwrite failure while leaving authored scenes untouched.
            EditorSceneManager.CloseScene(existing, true);
        }

        Scene reviewScene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        reviewScene.name = "YQ_Viking_AuthoredDistrictReview";

        float maximumWidth = results.Max(result => result.boundsSize.x);
        float maximumDepth = results.Max(result => result.boundsSize.z);
        float horizontalSpacing = maximumWidth + 35f;
        float verticalSpacing = maximumDepth + 35f;
        Vector3[] positions =
        {
            new Vector3(-horizontalSpacing * 0.5f, 0f, verticalSpacing * 0.5f),
            new Vector3(horizontalSpacing * 0.5f, 0f, verticalSpacing * 0.5f),
            new Vector3(-horizontalSpacing * 0.5f, 0f, -verticalSpacing * 0.5f),
            new Vector3(horizontalSpacing * 0.5f, 0f, -verticalSpacing * 0.5f)
        };

        for (int index = 0; index < results.Count; index++)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    results[index].prefabPath);
            GameObject instance =
                PrefabUtility.InstantiatePrefab(prefab, reviewScene) as GameObject;

            if (instance != null)
            {
                instance.name = DistrictDefinitions[index].displayName;
                instance.transform.position = positions[index];
            }
        }

        GameObject lightObject = new GameObject("Review Directional Light");
        SceneManager.MoveGameObjectToScene(lightObject, reviewScene);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Review Ground";
        SceneManager.MoveGameObjectToScene(ground, reviewScene);
        ground.transform.position = Vector3.down * 0.5f;
        ground.transform.localScale = new Vector3(
            horizontalSpacing * 2.2f,
            1f,
            verticalSpacing * 2.2f);

        EditorSceneManager.SaveScene(reviewScene, ReviewScenePath);
        EditorSceneManager.CloseScene(reviewScene, true);
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);

        for (int index = 0; index < renderers.Length; index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null)
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

        return bounds;
    }

    private static Bounds CalculateLocalRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        Matrix4x4 worldToLocal = root.transform.worldToLocalMatrix;

        for (int rendererIndex = 0;
             rendererIndex < renderers.Length;
             rendererIndex++)
        {
            Bounds worldBounds = renderers[rendererIndex].bounds;
            Vector3 center = worldToLocal.MultiplyPoint3x4(worldBounds.center);
            Vector3 extents = worldBounds.extents;

            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = worldToLocal.MultiplyPoint3x4(
                            worldBounds.center +
                            Vector3.Scale(extents, new Vector3(x, y, z)));

                        if (!hasBounds)
                        {
                            bounds = new Bounds(corner, Vector3.zero);
                            hasBounds = true;
                        }
                        else
                        {
                            bounds.Encapsulate(corner);
                        }
                    }
                }
            }

            if (!hasBounds)
            {
                bounds = new Bounds(center, Vector3.zero);
            }
        }

        return bounds;
    }

    private static string BuildCompositionSignature(
        List<SourceInstance> members)
    {
        StringBuilder canonical = new StringBuilder();

        foreach (IGrouping<string, SourceInstance> group in
                 members
                     .GroupBy(instance => instance.sourceName)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            canonical.Append(group.Key.ToLowerInvariant());
            canonical.Append(':');
            canonical.Append(group.Count());
            canonical.Append('|');
        }

        // note: FNV-1a is stable across editor sessions, unlike string.GetHashCode, so saved compositions remain deterministic.
        uint hash = 2166136261u;
        string value = canonical.ToString();

        for (int index = 0; index < value.Length; index++)
        {
            hash ^= value[index];
            hash *= 16777619u;
        }

        return "fnv1a32_" + hash.ToString("x8");
    }

    private static void DeleteStaleGeneratedDistricts()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { DistrictFolder });

        for (int index = 0; index < prefabGuids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[index]);

            if (path.StartsWith(DistrictFolder + "/", StringComparison.OrdinalIgnoreCase))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void EnsureFolderPath(string folderPath)
    {
        string normalized = folderPath.Replace('\\', '/');
        string[] segments = normalized.Split('/');
        string current = segments[0];

        for (int index = 1; index < segments.Length; index++)
        {
            string next = current + "/" + segments[index];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, segments[index]);
            }

            current = next;
        }
    }
}
