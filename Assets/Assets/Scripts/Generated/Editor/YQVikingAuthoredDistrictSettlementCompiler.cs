using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQVikingAuthoredDistrictSettlementCompiler
{
    private const string CompilerVersion =
        "viking-authored-district-compiler-1.0.0";

    private const int BenchmarkSeed = 184731;

    private const string DistrictFolder =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/Districts";

    private const string OutputFolder =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/DistrictCompilerBenchmark";

    public const string ReviewScenePath =
        OutputFolder + "/YQ_Viking_AuthoredDistrictSettlementBenchmark.unity";

    private const string ArtifactPath =
        OutputFolder + "/YQ_Viking_AuthoredDistrictSettlement.asset";

    private static readonly string[] RequiredDistrictIds =
    {
        "yq_viking_district_west_homestead",
        "yq_viking_district_central_village",
        "yq_viking_district_southern_quarter",
        "yq_viking_district_eastern_works"
    };

    private sealed class DistrictCandidate
    {
        public string prefabPath;
        public GameObject prefab;
        public YQWorldAssemblyDescriptor assembly;
        public YQWorldDistrictDescriptor district;
        public Vector3 placement;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Settlement Compiler/Build Reviewed Viking Authored-District Benchmark")]
    public static void BuildReviewedVikingAuthoredDistrictBenchmark()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingAuthoredDistrictSettlementCompiler] Wait for Unity to be idle in Edit mode before compiling the settlement.");
            return;
        }

        try
        {
            EnsureFolderPath(OutputFolder);

            List<DistrictCandidate> candidates = LoadCandidates();

            if (RequiresSourceOriginMigration(candidates))
            {
                // note: Districts extracted before source-origin persistence are rebuilt once from the same authored scene; current accepted districts are never regenerated.
                Debug.Log(
                    "[YQVikingAuthoredDistrictSettlementCompiler] Migrating legacy district candidates to the authored-source-origin contract.");
                YQVikingAuthoredDistrictExtractor
                    .ExtractVikingAuthoredDistrictCandidates();
                candidates = LoadCandidates();
            }

            List<string> validation = ValidateCandidates(candidates);

            if (validation.Count > 0)
            {
                throw new InvalidOperationException(
                    "District benchmark rejected:\n- " +
                    string.Join("\n- ", validation));
            }

            // note: The user visually approved this exact extracted set; promotion occurs here before any runtime-facing artifact is emitted.
            ApproveReviewedCandidates(candidates);
            candidates = LoadCandidates();

            Vector3 settlementOrigin = ResolveSettlementOrigin(candidates);
            List<YQCompiledDistrictPlacementRecord> placementRecords =
                BuildPlacementRecords(candidates, settlementOrigin);
            int preservedInstanceCount =
                candidates.Sum(candidate => candidate.district.SourceInstanceCount);

            YQCompiledDistrictSettlementArtifact artifact =
                WriteArtifact(
                    placementRecords,
                    preservedInstanceCount,
                    validation);

            BuildReviewScene(candidates, settlementOrigin);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[YQVikingAuthoredDistrictSettlementCompiler] AUTHORED-DISTRICT SETTLEMENT COMPILED\n" +
                "Seed: " + BenchmarkSeed + "\n" +
                "Districts: " + candidates.Count + "/" + RequiredDistrictIds.Length + "\n" +
                "Authored instances preserved: " + preservedInstanceCount + "\n" +
                "Validation errors: 0\n" +
                "Deterministic artifact: " + AssetDatabase.GetAssetPath(artifact) + "\n" +
                "Review scene: " + ReviewScenePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static bool RequiresSourceOriginMigration(
        List<DistrictCandidate> candidates)
    {
        for (int index = 0; index < candidates.Count; index++)
        {
            if (candidates[index].district.AuthoredSourceOrigin == Vector3.zero)
                return true;
        }

        return false;
    }

    private static List<DistrictCandidate> LoadCandidates()
    {
        List<DistrictCandidate> result = new List<DistrictCandidate>();

        for (int index = 0; index < RequiredDistrictIds.Length; index++)
        {
            string prefabPath =
                DistrictFolder + "/" + RequiredDistrictIds[index] + ".prefab";
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Required reviewed district prefab is missing: " + prefabPath +
                    ". Rerun Extract Viking Authored District Candidates first.");
            }

            YQWorldAssemblyDescriptor assembly =
                prefab.GetComponent<YQWorldAssemblyDescriptor>();
            YQWorldDistrictDescriptor district =
                prefab.GetComponent<YQWorldDistrictDescriptor>();

            if (assembly == null || district == null)
            {
                throw new InvalidOperationException(
                    "District prefab lacks its assembly contract: " + prefabPath);
            }

            result.Add(
                new DistrictCandidate
                {
                    prefabPath = prefabPath,
                    prefab = prefab,
                    assembly = assembly,
                    district = district
                });
        }

        return result;
    }

    private static List<string> ValidateCandidates(
        List<DistrictCandidate> candidates)
    {
        List<string> errors = new List<string>();

        if (candidates.Count != RequiredDistrictIds.Length)
        {
            errors.Add(
                "Expected " + RequiredDistrictIds.Length +
                " districts but loaded " + candidates.Count + ".");
        }

        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<YQDistrictFunction> functions =
            new HashSet<YQDistrictFunction>();

        for (int index = 0; index < candidates.Count; index++)
        {
            DistrictCandidate candidate = candidates[index];

            if (candidate.assembly.AssemblyKind != YQWorldAssemblyKind.District)
            {
                errors.Add(candidate.prefabPath + " is not classified as a district.");
            }

            if (!ids.Add(candidate.assembly.StableAssemblyId))
            {
                errors.Add(
                    "Duplicate district id: " + candidate.assembly.StableAssemblyId + ".");
            }

            if (!functions.Add(candidate.district.DistrictFunction))
            {
                errors.Add(
                    "Duplicate district function: " +
                    candidate.district.DistrictFunction + ".");
            }

            if (candidate.district.SourceInstanceCount < 50)
            {
                errors.Add(
                    candidate.assembly.StableAssemblyId +
                    " is below the authored-density floor.");
            }

            if (candidate.district.AuthoredBuildingCount <= 0 ||
                candidate.district.AuthoredDressingCount <= 0)
            {
                errors.Add(
                    candidate.assembly.StableAssemblyId +
                    " lacks structural or dressing content.");
            }

            if (!IsFinite(candidate.district.AuthoredSourceOrigin))
            {
                errors.Add(
                    candidate.assembly.StableAssemblyId +
                    " has an invalid authored source origin.");
            }

            if (candidate.district.AuthoredSourceOrigin == Vector3.zero)
            {
                errors.Add(
                    candidate.assembly.StableAssemblyId +
                    " has no persisted authored source origin; rerun district extraction after this compiler update.");
            }

            if (candidate.assembly.ConnectionSocketPaths.Count < 4 ||
                candidate.district.ConnectionSocketPaths.Count < 4)
            {
                errors.Add(
                    candidate.assembly.StableAssemblyId +
                    " does not expose four district connection sockets.");
            }
        }

        int sourceInstanceCount =
            candidates.Sum(candidate => candidate.district.SourceInstanceCount);

        if (sourceInstanceCount != 1051)
        {
            errors.Add(
                "Expected all 1,051 reviewed source instances, found " +
                sourceInstanceCount + ".");
        }

        return errors;
    }

    private static void ApproveReviewedCandidates(
        List<DistrictCandidate> candidates)
    {
        for (int index = 0; index < candidates.Count; index++)
        {
            string prefabPath = candidates[index].prefabPath;
            GameObject contents = PrefabUtility.LoadPrefabContents(prefabPath);

            try
            {
                YQWorldAssemblyDescriptor descriptor =
                    contents.GetComponent<YQWorldAssemblyDescriptor>();

                if (descriptor == null)
                {
                    throw new InvalidOperationException(
                        "Cannot approve a district without an assembly descriptor: " +
                        prefabPath);
                }

                descriptor.MarkApprovedGolden();
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }

    private static Vector3 ResolveSettlementOrigin(
        List<DistrictCandidate> candidates)
    {
        Vector3 sum = Vector3.zero;
        float minimumY = float.PositiveInfinity;

        for (int index = 0; index < candidates.Count; index++)
        {
            Vector3 origin = candidates[index].district.AuthoredSourceOrigin;
            sum += origin;
            minimumY = Mathf.Min(minimumY, origin.y);
        }

        Vector3 average = sum / Mathf.Max(1, candidates.Count);
        return new Vector3(average.x, minimumY, average.z);
    }

    private static List<YQCompiledDistrictPlacementRecord> BuildPlacementRecords(
        List<DistrictCandidate> candidates,
        Vector3 settlementOrigin)
    {
        List<YQCompiledDistrictPlacementRecord> result =
            new List<YQCompiledDistrictPlacementRecord>();

        for (int index = 0; index < candidates.Count; index++)
        {
            DistrictCandidate candidate = candidates[index];
            candidate.placement =
                candidate.district.AuthoredSourceOrigin - settlementOrigin;

            result.Add(
                new YQCompiledDistrictPlacementRecord
                {
                    stableDistrictId = candidate.assembly.StableAssemblyId,
                    districtFunction = candidate.district.DistrictFunction,
                    position = candidate.placement,
                    yawDegrees = 0f,
                    boundsSize = candidate.district.LocalBoundsSize,
                    sourceInstanceCount = candidate.district.SourceInstanceCount
                });
        }

        return result;
    }

    private static YQCompiledDistrictSettlementArtifact WriteArtifact(
        List<YQCompiledDistrictPlacementRecord> placementRecords,
        int preservedInstanceCount,
        List<string> validation)
    {
        YQCompiledDistrictSettlementArtifact artifact =
            AssetDatabase.LoadAssetAtPath<YQCompiledDistrictSettlementArtifact>(
                ArtifactPath);

        if (artifact == null)
        {
            artifact =
                ScriptableObject.CreateInstance<YQCompiledDistrictSettlementArtifact>();
            AssetDatabase.CreateAsset(artifact, ArtifactPath);
        }

        artifact.Configure(
            CompilerVersion,
            BenchmarkSeed,
            "viking_authored_four_district_mosaic",
            "assets_befourstudios_medievalvikingvillage",
            true,
            preservedInstanceCount,
            placementRecords,
            validation);
        EditorUtility.SetDirty(artifact);
        return artifact;
    }

    private static void BuildReviewScene(
        List<DistrictCandidate> candidates,
        Vector3 settlementOrigin)
    {
        Scene existing = SceneManager.GetSceneByPath(ReviewScenePath);

        if (existing.IsValid() && existing.isLoaded)
        {
            EditorSceneManager.CloseScene(existing, true);
        }

        Scene scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        scene.name = "YQ_Viking_AuthoredDistrictSettlementBenchmark";
        GameObject root =
            new GameObject("00__VIKING_AUTHORED_DISTRICT_SETTLEMENT");
        SceneManager.MoveGameObjectToScene(root, scene);

        GameObject districtsRoot = new GameObject("Districts");
        districtsRoot.transform.SetParent(root.transform, false);

        for (int index = 0; index < candidates.Count; index++)
        {
            DistrictCandidate candidate = candidates[index];
            GameObject instance =
                PrefabUtility.InstantiatePrefab(candidate.prefab, scene) as GameObject;

            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Failed to instantiate approved district " +
                    candidate.assembly.StableAssemblyId + ".");
            }

            instance.name = candidate.assembly.SourceFamily;
            instance.transform.SetParent(districtsRoot.transform, false);
            instance.transform.localPosition =
                candidate.district.AuthoredSourceOrigin - settlementOrigin;
            instance.transform.localRotation = Quaternion.identity;
        }

        Bounds settlementBounds = CalculateRendererBounds(districtsRoot);
        BuildGround(root.transform, settlementBounds);
        BuildLighting(root.transform);

        // note: The review scene is a deterministic reconstruction of approved district cells, not a new random layout.
        EditorSceneManager.SaveScene(scene, ReviewScenePath);
        EditorSceneManager.CloseScene(scene, true);
    }

    private static void BuildGround(Transform parent, Bounds bounds)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Benchmark Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.position = new Vector3(
            bounds.center.x,
            bounds.min.y - 0.5f,
            bounds.center.z);
        ground.transform.localScale = new Vector3(
            bounds.size.x + 30f,
            1f,
            bounds.size.z + 30f);
    }

    private static void BuildLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("Benchmark Sun");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
    }

    private static Bounds CalculateRendererBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
            return new Bounds(Vector3.zero, new Vector3(100f, 1f, 100f));

        Bounds bounds = renderers[0].bounds;

        for (int index = 1; index < renderers.Length; index++)
        {
            bounds.Encapsulate(renderers[index].bounds);
        }

        return bounds;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
               !float.IsNaN(value.z) && !float.IsInfinity(value.z);
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
