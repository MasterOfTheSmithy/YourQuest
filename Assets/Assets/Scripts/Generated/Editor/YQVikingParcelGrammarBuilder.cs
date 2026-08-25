using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQVikingParcelGrammarBuilder
{
    private const string AssemblyRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage";

    private const string BuildingFolder =
        AssemblyRoot + "/Buildings";

    private const string StructureFolder =
        AssemblyRoot + "/Structures";

    private const string ParcelFolder =
        AssemblyRoot + "/Parcels";

    private const string StreetProfilePath =
        AssemblyRoot + "/YQ_Viking_RuralStreetProfile.asset";

    private const string ParcelDescriptorScriptPath =
        "Assets/Assets/Scripts/Generated/YQWorldParcelDescriptor.cs";

    private const string StreetProfileScriptPath =
        "Assets/Assets/Scripts/Generated/YQStreetProfileDefinition.cs";

    public const string ReviewScenePath =
        AssemblyRoot + "/YQ_Viking_ParcelGrammarReview.unity";

    private const string GroundMaterialPath =
        AssemblyRoot + "/YQ_Viking_ParcelReviewGround.mat";

    private const string RoadMaterialPath =
        AssemblyRoot + "/YQ_Viking_ParcelReviewRoad.mat";

    private sealed class ParcelRecipe
    {
        public GameObject sourcePrefab;
        public YQWorldAssemblyDescriptor sourceDescriptor;
        public YQParcelFunction function;
        public int residentCapacity;
        public bool requiresServiceAccess;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Build Viking Parcel Grammar Candidates")]
    public static void BuildVikingParcelGrammarCandidates()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingParcelGrammarBuilder] Parcel authoring requires stable Edit mode.");

            return;
        }

        EnsureFolderPath(ParcelFolder);

        List<ParcelRecipe> recipes =
            FindParcelRecipes();

        if (recipes.Count == 0)
        {
            Debug.LogWarning(
                "[YQVikingParcelGrammarBuilder] No reviewed Viking building candidates were found.");

            return;
        }

        try
        {
            YQStreetProfileDefinition streetProfile =
                GetOrCreateStreetProfile();

            List<string> generatedPaths =
                new List<string>();

            for (int index = 0;
                 index < recipes.Count;
                 index++)
            {
                string path =
                    SaveParcelCandidate(
                        recipes[index]);

                if (!string.IsNullOrWhiteSpace(path))
                {
                    generatedPaths.Add(path);
                }
            }

            RemoveStaleParcelCandidates(
                generatedPaths);

            BuildReviewScene(
                generatedPaths,
                streetProfile);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            SceneAsset reviewScene =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(
                    ReviewScenePath);

            if (reviewScene != null)
            {
                Selection.activeObject = reviewScene;
            }

            Debug.Log(
                "[YQVikingParcelGrammarBuilder] VIKING PARCEL GRAMMAR READY\n" +
                "Parcel candidates: " + generatedPaths.Count +
                "\nStreet profile: " + StreetProfilePath +
                "\nCurved-frontage review: " + ReviewScenePath +
                "\nRelease eligible: 0 (visual and traversal review required)");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static List<ParcelRecipe> FindParcelRecipes()
    {
        List<ParcelRecipe> recipes =
            new List<ParcelRecipe>();

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[]
                {
                    BuildingFolder,
                    StructureFolder
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

        paths.Sort(StringComparer.OrdinalIgnoreCase);

        for (int index = 0;
             index < paths.Count;
             index++)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(
                    paths[index]);

            YQWorldAssemblyDescriptor descriptor =
                prefab != null
                    ? prefab.GetComponent<YQWorldAssemblyDescriptor>()
                    : null;

            if (descriptor == null)
                continue;

            bool isHouse =
                descriptor.SourceFamily.StartsWith(
                    "House",
                    StringComparison.OrdinalIgnoreCase);

            bool isStable =
                string.Equals(
                    descriptor.SourceFamily,
                    "StableComplex",
                    StringComparison.OrdinalIgnoreCase);

            bool isWindmill =
                string.Equals(
                    descriptor.SourceFamily,
                    "Windmill",
                    StringComparison.OrdinalIgnoreCase);

            if (!isHouse && !isStable && !isWindmill)
                continue;

            // note: The first parcel benchmark wraps complete reviewed-scale structures only; loose modules never become parcel occupants.
            recipes.Add(
                new ParcelRecipe
                {
                    sourcePrefab = prefab,
                    sourceDescriptor = descriptor,
                    function = isHouse
                        ? YQParcelFunction.Residential
                        : isStable
                            ? YQParcelFunction.Service
                            : YQParcelFunction.LandmarkSupport,
                    residentCapacity = isHouse
                        ? ResolveResidentCapacity(descriptor.SourceFamily)
                        : 0,
                    requiresServiceAccess = isStable || isWindmill
                });
        }

        return recipes;
    }

    private static int ResolveResidentCapacity(
        string sourceFamily)
    {
        if (string.Equals(
                sourceFamily,
                "House2",
                StringComparison.OrdinalIgnoreCase))
            return 4;

        if (string.Equals(
                sourceFamily,
                "House4",
                StringComparison.OrdinalIgnoreCase))
            return 8;

        return 6;
    }

    private static string SaveParcelCandidate(
        ParcelRecipe recipe)
    {
        Scene previewScene =
            EditorSceneManager.NewPreviewScene();

        try
        {
            string stableId =
                "assembly_viking_parcel_" +
                recipe.sourceDescriptor.StableAssemblyId
                    .Replace("assembly_viking_", string.Empty)
                    .ToLowerInvariant();

            GameObject root =
                new GameObject(stableId);

            SceneManager.MoveGameObjectToScene(
                root,
                previewScene);

            GameObject occupant =
                PrefabUtility.InstantiatePrefab(
                    recipe.sourcePrefab,
                    previewScene) as GameObject;

            if (occupant == null)
                return string.Empty;

            occupant.name = "Occupant_Main";
            occupant.transform.SetParent(
                root.transform,
                false);

            Transform sourceEntrance =
                occupant.transform.Find(
                    recipe.sourceDescriptor.EntranceSocketPath);

            Vector3 sourceFront =
                ResolveSourceFront(
                    recipe.sourceDescriptor,
                    sourceEntrance);

            Quaternion occupantRotation =
                Quaternion.FromToRotation(
                    sourceFront,
                    Vector3.back);

            occupant.transform.localRotation = occupantRotation;

            const float frontSetback = 3f;
            const float sideSetback = 2f;
            const float rearSetback = 3f;

            if (sourceEntrance != null)
            {
                Vector3 currentEntrance =
                    root.transform.InverseTransformPoint(
                        sourceEntrance.position);

                occupant.transform.localPosition +=
                    new Vector3(0f, 0f, frontSetback) -
                    currentEntrance;
            }

            Bounds occupantBounds =
                CalculateLocalRendererBounds(root);

            if (occupantBounds.min.z < 1f)
            {
                // note: Keep structural geometry behind the road edge even when a source pivot or doorway sits inside the facade.
                occupant.transform.localPosition +=
                    Vector3.forward *
                    (1f - occupantBounds.min.z);

                occupantBounds =
                    CalculateLocalRendererBounds(root);
            }

            float halfWidth =
                Mathf.Max(
                    Mathf.Abs(occupantBounds.min.x),
                    Mathf.Abs(occupantBounds.max.x));

            float frontageWidth =
                SnapUp(
                    halfWidth * 2f + sideSetback * 2f,
                    2f);

            float parcelDepth =
                SnapUp(
                    occupantBounds.max.z + rearSetback,
                    2f);

            GameObject sockets =
                new GameObject("Sockets");

            sockets.transform.SetParent(
                root.transform,
                false);

            Transform frontage =
                CreateSocket(
                    sockets.transform,
                    "Frontage_Center",
                    Vector3.zero,
                    Vector3.back);

            Transform entranceTarget =
                CreateSocket(
                    sockets.transform,
                    "Entrance_Target",
                    sourceEntrance != null
                        ? root.transform.InverseTransformPoint(sourceEntrance.position)
                        : new Vector3(0f, 0f, frontSetback),
                    Vector3.back);

            Transform service =
                CreateSocket(
                    sockets.transform,
                    "Service_Rear",
                    new Vector3(0f, 0f, parcelDepth),
                    Vector3.forward);

            CreateSocket(
                sockets.transform,
                "Yard_Left",
                new Vector3(-frontageWidth * 0.5f, 0f, parcelDepth * 0.5f),
                Vector3.left);

            CreateSocket(
                sockets.transform,
                "Yard_Right",
                new Vector3(frontageWidth * 0.5f, 0f, parcelDepth * 0.5f),
                Vector3.right);

            YQWorldAssemblyDescriptor assemblyDescriptor =
                root.AddComponent<YQWorldAssemblyDescriptor>();

            assemblyDescriptor.ConfigureExtractedCandidate(
                stableId,
                "assets_befourstudios_medievalvikingvillage",
                YQWorldAssemblyKind.Parcel,
                "Parcel_" + recipe.sourceDescriptor.SourceFamily,
                "parcel:" + recipe.sourceDescriptor.StableAssemblyId,
                recipe.sourceDescriptor.AuthoredOccurrenceCount,
                new Vector3(0f, occupantBounds.center.y, parcelDepth * 0.5f),
                new Vector3(frontageWidth, occupantBounds.size.y, parcelDepth),
                new Vector3(frontageWidth, occupantBounds.size.y + 1f, parcelDepth),
                Vector3.back,
                "Sockets/Entrance_Target",
                new[]
                {
                    "medieval",
                    "viking",
                    "parcel",
                    recipe.function.ToString().ToLowerInvariant(),
                    recipe.sourceDescriptor.SourceFamily.ToLowerInvariant()
                });

            assemblyDescriptor.ConfigureConnectionSockets(
                new[]
                {
                    "Sockets/Frontage_Center",
                    "Sockets/Service_Rear"
                });

            YQWorldParcelDescriptor parcelDescriptor =
                root.AddComponent<YQWorldParcelDescriptor>();

            parcelDescriptor.Configure(
                recipe.function,
                frontageWidth,
                parcelDepth,
                frontSetback,
                sideSetback,
                rearSetback,
                12f,
                recipe.residentCapacity,
                recipe.requiresServiceAccess,
                GetRelativePath(root.transform, frontage),
                GetRelativePath(root.transform, entranceTarget),
                GetRelativePath(root.transform, service),
                new[]
                {
                    recipe.sourceDescriptor.StableAssemblyId
                });

            string prefabPath =
                ParcelFolder + "/" + stableId + ".prefab";

            RepairParcelScriptBindingIfRequired(
                prefabPath);

            // note: Saving a nested prefab preserves the reviewed source assembly and avoids copying or flattening vendor-derived geometry.
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

    private static Vector3 ResolveSourceFront(
        YQWorldAssemblyDescriptor descriptor,
        Transform entrance)
    {
        if (entrance != null)
        {
            Vector3 fromCenter =
                entrance.localPosition -
                descriptor.LocalBoundsCenter;

            fromCenter.y = 0f;

            if (fromCenter.sqrMagnitude > 0.25f)
            {
                return fromCenter.normalized;
            }

            Vector3 socketForward = entrance.localRotation * Vector3.forward;
            socketForward.y = 0f;

            if (socketForward.sqrMagnitude > 0.0001f)
            {
                return socketForward.normalized;
            }
        }

        Vector3 fallback = descriptor.FrontDirection;
        fallback.y = 0f;
        return fallback.sqrMagnitude > 0.0001f
            ? fallback.normalized
            : Vector3.forward;
    }

    private static Transform CreateSocket(
        Transform parent,
        string name,
        Vector3 localPosition,
        Vector3 forward)
    {
        GameObject socket =
            new GameObject(name);

        socket.transform.SetParent(
            parent,
            false);

        socket.transform.localPosition = localPosition;
        socket.transform.localRotation =
            Quaternion.LookRotation(
                forward,
                Vector3.up);

        return socket.transform;
    }

    private static string GetRelativePath(
        Transform root,
        Transform target)
    {
        if (target == null || target == root)
            return string.Empty;

        List<string> parts =
            new List<string>();

        Transform current = target;

        while (current != null && current != root)
        {
            parts.Add(current.name);
            current = current.parent;
        }

        parts.Reverse();
        return string.Join("/", parts);
    }

    private static float SnapUp(
        float value,
        float increment)
    {
        return Mathf.Ceil(value / increment) * increment;
    }

    private static YQStreetProfileDefinition GetOrCreateStreetProfile()
    {
        YQStreetProfileDefinition profile =
            AssetDatabase.LoadAssetAtPath<YQStreetProfileDefinition>(
                StreetProfilePath);

        if (profile != null &&
            !HasExpectedScriptBinding(
                MonoScript.FromScriptableObject(profile),
                StreetProfileScriptPath))
        {
            // note: A generated profile created before its Unity type received a matching source filename is safely replaced once to establish a valid MonoScript GUID.
            AssetDatabase.DeleteAsset(
                StreetProfilePath);

            profile = null;
        }

        if (profile == null)
        {
            UnityEngine.Object incompatibleAsset =
                AssetDatabase.LoadMainAssetAtPath(
                    StreetProfilePath);

            if (incompatibleAsset != null)
            {
                // note: Replace only the reproducible generated profile if an earlier class/file mismatch left it without a valid Unity script association.
                AssetDatabase.DeleteAsset(
                    StreetProfilePath);
            }

            profile =
                ScriptableObject.CreateInstance<YQStreetProfileDefinition>();

            AssetDatabase.CreateAsset(
                profile,
                StreetProfilePath);
        }

        profile.Configure(
            "street_profile_viking_rural_primary",
            "assets_befourstudios_medievalvikingvillage",
            6f,
            1.5f,
            8f,
            24f,
            10f,
            28f,
            new[]
            {
                "residential",
                "service",
                "landmark_support"
            });

        EditorUtility.SetDirty(profile);
        return profile;
    }

    private static void RepairParcelScriptBindingIfRequired(
        string prefabPath)
    {
        GameObject existing =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                prefabPath);

        if (existing == null)
            return;

        string expectedGuid =
            AssetDatabase.AssetPathToGUID(
                ParcelDescriptorScriptPath);

        string serializedPrefab =
            File.ReadAllText(
                prefabPath);

        if (!string.IsNullOrWhiteSpace(expectedGuid) &&
            serializedPrefab.IndexOf(
                "guid: " + expectedGuid,
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return;
        }

        // note: Delete only a reproducible parcel with a stale script GUID so the immediately following save can bind the correctly named component script.
        AssetDatabase.DeleteAsset(
            prefabPath);
    }

    private static bool HasExpectedScriptBinding(
        MonoScript script,
        string expectedPath)
    {
        return script != null &&
               string.Equals(
                   AssetDatabase.GetAssetPath(script),
                   expectedPath,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static void BuildReviewScene(
        List<string> parcelPaths,
        YQStreetProfileDefinition streetProfile)
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
            throw new InvalidOperationException(
                "The Viking parcel review scene is open with unsaved changes. " +
                "Save or discard them before rebuilding it.");
        }

        try
        {
            ClearGeneratedReviewRoot(reviewScene);

            GameObject root =
                new GameObject("00__VIKING_PARCEL_GRAMMAR_REVIEW");

            SceneManager.MoveGameObjectToScene(
                root,
                reviewScene);

            BuildReviewGround(root.transform);
            BuildReviewLighting(root.transform);

            Vector3[] centerline =
            {
                new Vector3(-66f, 0f, -5f),
                new Vector3(-44f, 0f, 1f),
                new Vector3(-22f, 0f, -3f),
                new Vector3(0f, 0f, 4f),
                new Vector3(22f, 0f, 0f),
                new Vector3(44f, 0f, 6f),
                new Vector3(66f, 0f, 2f)
            };

            BuildRoadGuide(
                root.transform,
                centerline,
                streetProfile.CarriagewayWidth);

            float frontageOffset =
                streetProfile.CarriagewayWidth * 0.5f +
                streetProfile.VergeWidth;

            for (int index = 0;
                 index < parcelPaths.Count && index < centerline.Length;
                 index++)
            {
                GameObject prefab =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        parcelPaths[index]);

                if (prefab == null)
                    continue;

                Vector3 tangent =
                    ResolveCenterlineTangent(
                        centerline,
                        index);

                Vector3 normal =
                    new Vector3(
                        -tangent.z,
                        0f,
                        tangent.x).normalized;

                if ((index & 1) == 1)
                {
                    normal = -normal;
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        prefab,
                        reviewScene) as GameObject;

                if (instance == null)
                    continue;

                instance.transform.SetParent(
                    root.transform,
                    false);

                instance.transform.localPosition =
                    centerline[index] +
                    normal * frontageOffset;

                instance.transform.localRotation =
                    Quaternion.LookRotation(
                        normal,
                        Vector3.up);
            }

            // note: The curved alternating frontage is a diagnostic composition proving road ownership and entrance alignment, not a runtime-generated settlement.
            bool saved =
                EditorSceneManager.SaveScene(
                    reviewScene,
                    ReviewScenePath,
                    false);

            if (!saved)
            {
                throw new InvalidOperationException(
                    "Unity refused to save the Viking parcel grammar review scene.");
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

    private static void ClearGeneratedReviewRoot(
        Scene reviewScene)
    {
        GameObject[] roots =
            reviewScene.GetRootGameObjects();

        for (int index = 0;
             index < roots.Length;
             index++)
        {
            if (roots[index] != null &&
                string.Equals(
                    roots[index].name,
                    "00__VIKING_PARCEL_GRAMMAR_REVIEW",
                    StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(
                    roots[index]);
            }
        }
    }

    private static void BuildReviewGround(
        Transform parent)
    {
        GameObject ground =
            GameObject.CreatePrimitive(
                PrimitiveType.Plane);

        ground.name = "Review Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition =
            new Vector3(0f, -0.08f, 10f);
        ground.transform.localScale =
            new Vector3(17f, 1f, 8f);

        AssignSharedMaterial(
            ground,
            GetOrCreateMaterial(
                GroundMaterialPath,
                "YQ_Viking_ParcelReviewGround",
                new Color(0.18f, 0.23f, 0.16f, 1f)));
    }

    private static void BuildReviewLighting(
        Transform parent)
    {
        GameObject lightObject =
            new GameObject("Review Sun");

        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation =
            Quaternion.Euler(48f, -32f, 0f);

        Light light =
            lightObject.AddComponent<Light>();

        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    private static void BuildRoadGuide(
        Transform parent,
        Vector3[] centerline,
        float width)
    {
        Material roadMaterial =
            GetOrCreateMaterial(
                RoadMaterialPath,
                "YQ_Viking_ParcelReviewRoad",
                new Color(0.22f, 0.17f, 0.11f, 1f));

        GameObject roadRoot =
            new GameObject("Road Frontage Guide");

        roadRoot.transform.SetParent(parent, false);

        for (int index = 0;
             index < centerline.Length - 1;
             index++)
        {
            Vector3 delta = centerline[index + 1] - centerline[index];
            float length = delta.magnitude;

            GameObject segment =
                GameObject.CreatePrimitive(
                    PrimitiveType.Cube);

            segment.name = "Road Segment " + (index + 1).ToString("00");
            segment.transform.SetParent(roadRoot.transform, false);
            segment.transform.localPosition =
                (centerline[index] + centerline[index + 1]) * 0.5f;
            segment.transform.localRotation =
                Quaternion.LookRotation(delta.normalized, Vector3.up);
            segment.transform.localScale =
                new Vector3(width, 0.12f, length + 0.4f);

            AssignSharedMaterial(segment, roadMaterial);

            Collider collider = segment.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }
    }

    private static Vector3 ResolveCenterlineTangent(
        Vector3[] centerline,
        int index)
    {
        Vector3 prior =
            centerline[Mathf.Max(0, index - 1)];

        Vector3 next =
            centerline[Mathf.Min(centerline.Length - 1, index + 1)];

        Vector3 tangent = next - prior;
        tangent.y = 0f;
        return tangent.sqrMagnitude > 0.0001f
            ? tangent.normalized
            : Vector3.right;
    }

    private static Material GetOrCreateMaterial(
        string path,
        string materialName,
        Color color)
    {
        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
            return material;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            return null;

        material =
            new Material(shader)
            {
                name = materialName
            };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void AssignSharedMaterial(
        GameObject target,
        Material material)
    {
        Renderer renderer =
            target != null
                ? target.GetComponent<Renderer>()
                : null;

        if (renderer != null && material != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private static Bounds CalculateLocalRendererBounds(
        GameObject root)
    {
        Renderer[] renderers =
            root.GetComponentsInChildren<Renderer>(true);

        bool hasBounds = false;
        Bounds result = default;

        for (int index = 0;
             index < renderers.Length;
             index++)
        {
            Renderer renderer = renderers[index];

            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 center =
                root.transform.InverseTransformPoint(
                    worldBounds.center);

            Vector3 extents = worldBounds.extents;
            Vector3 right =
                root.transform.InverseTransformVector(
                    new Vector3(extents.x, 0f, 0f));
            Vector3 up =
                root.transform.InverseTransformVector(
                    new Vector3(0f, extents.y, 0f));
            Vector3 forward =
                root.transform.InverseTransformVector(
                    new Vector3(0f, 0f, extents.z));

            Vector3 localExtents =
                new Vector3(
                    Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x),
                    Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y),
                    Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z));

            Bounds candidate =
                new Bounds(center, localExtents * 2f);

            if (!hasBounds)
            {
                result = candidate;
                hasBounds = true;
            }
            else
            {
                result.Encapsulate(candidate.min);
                result.Encapsulate(candidate.max);
            }
        }

        return hasBounds
            ? result
            : new Bounds(Vector3.zero, Vector3.zero);
    }

    private static void RemoveStaleParcelCandidates(
        List<string> retainedPaths)
    {
        HashSet<string> retained =
            new HashSet<string>(
                retainedPaths,
                StringComparer.OrdinalIgnoreCase);

        string[] guids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { ParcelFolder });

        for (int index = 0;
             index < guids.Length;
             index++)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(
                    guids[index]);

            if (retained.Contains(path) ||
                !Path.GetFileNameWithoutExtension(path).StartsWith(
                    "assembly_viking_parcel_",
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // note: Delete only reproducible parcel candidates in their dedicated generated folder; authored source assemblies remain untouched.
            AssetDatabase.DeleteAsset(path);
        }
    }

    private static void EnsureFolderPath(
        string path)
    {
        string normalized =
            path.Replace('\\', '/').Trim('/');

        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts = normalized.Split('/');
        string current = "Assets";

        for (int index = 1;
             index < parts.Length;
             index++)
        {
            string next = current + "/" + parts[index];

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
