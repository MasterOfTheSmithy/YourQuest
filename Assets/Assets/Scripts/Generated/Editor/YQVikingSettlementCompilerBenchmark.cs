using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class YQVikingSettlementCompilerBenchmark
{
    private const string CompilerVersion = "wg3-viking-0.1.0";
    private const int BenchmarkSeed = 184731;

    private const string AssemblyRoot =
        "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage";

    private const string ParcelFolder = AssemblyRoot + "/Parcels";
    private const string StreetProfilePath =
        AssemblyRoot + "/YQ_Viking_RuralStreetProfile.asset";

    private const string OutputFolder =
        AssemblyRoot + "/CompilerBenchmark";

    private const string MeshFolder = OutputFolder + "/RoadMeshes";
    private const string ArtifactPath =
        OutputFolder + "/YQ_Viking_FixedSeedSettlement.asset";
    private const string ReportPath =
        OutputFolder + "/YQ_Viking_FixedSeedValidation.md";
    private const string GroundMaterialPath =
        OutputFolder + "/YQ_Viking_CompilerGround.mat";
    private const string RoadMaterialPath =
        OutputFolder + "/YQ_Viking_CompilerRoad.mat";

    public const string ReviewScenePath =
        OutputFolder + "/YQ_Viking_SettlementCompilerBenchmark.unity";

    private sealed class ParcelCandidate
    {
        public GameObject prefab;
        public YQWorldAssemblyDescriptor assembly;
        public YQWorldParcelDescriptor parcel;
    }

    private sealed class RoadDraft
    {
        public string id;
        public string role;
        public float width;
        public readonly List<Vector3> points = new List<Vector3>();
    }

    private sealed class FrontageSlot
    {
        public string roadId;
        public string roadRole;
        public Vector3 roadPoint;
        public Vector3 tangent;
        public Vector3 outward;
        public float segmentLength;
        public int deterministicOrder;
    }

    private sealed class PlacementDraft
    {
        public ParcelCandidate candidate;
        public FrontageSlot slot;
        public Vector3 position;
        public Quaternion rotation;
        public OrientedRect footprint;
    }

    private struct OrientedRect
    {
        public Vector2 center;
        public Vector2 axisX;
        public Vector2 axisZ;
        public Vector2 halfSize;
    }

    [MenuItem(
        "Tools/YourQuest/AAA World Generation/Archived Tools/Rejected Prototypes/Build Viking Fixed-Seed Benchmark")]
    public static void BuildVikingFixedSeedBenchmark()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            Debug.LogWarning(
                "[YQVikingSettlementCompilerBenchmark] Compilation requires stable Edit mode.");
            return;
        }

        EnsureFolderPath(OutputFolder);
        EnsureFolderPath(MeshFolder);

        try
        {
            YQStreetProfileDefinition streetProfile =
                AssetDatabase.LoadAssetAtPath<YQStreetProfileDefinition>(
                    StreetProfilePath);

            if (streetProfile == null)
            {
                throw new InvalidOperationException(
                    "The reviewed Viking street profile is missing. Rebuild the archived parcel grammar first.");
            }

            List<ParcelCandidate> candidates =
                LoadParcelCandidates();

            if (candidates.Count == 0)
            {
                throw new InvalidOperationException(
                    "No Viking parcel candidates are available to compile.");
            }

            List<RoadDraft> roads =
                BuildRoadGraph(
                    BenchmarkSeed,
                    streetProfile.CarriagewayWidth);

            List<FrontageSlot> slots =
                BuildFrontageSlots(
                    roads,
                    streetProfile.VergeWidth,
                    BenchmarkSeed);

            List<PlacementDraft> placements =
                SolveParcels(
                    candidates,
                    slots,
                    streetProfile,
                    BenchmarkSeed);

            List<string> validation =
                ValidateCompilation(
                    candidates,
                    roads,
                    placements);

            bool valid = validation.Count == 0;

            YQCompiledSettlementArtifact artifact =
                WriteArtifact(
                    roads,
                    placements,
                    validation,
                    valid);

            BuildReviewScene(
                roads,
                placements);

            WriteValidationReport(
                roads,
                candidates,
                placements,
                validation,
                valid);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = valid
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(ReviewScenePath)
                : artifact;

            Debug.Log(
                "[YQVikingSettlementCompilerBenchmark] FIXED-SEED SETTLEMENT COMPILED\n" +
                "Seed: " + BenchmarkSeed +
                "\nRoad graph nodes: " + CountRoadNodes(roads) +
                "\nRoads: " + roads.Count +
                "\nPlaced parcels: " + placements.Count + "/" + candidates.Count +
                "\nValidation errors: " + validation.Count +
                "\nDeterministic artifact: " + ArtifactPath +
                "\nReview scene: " + ReviewScenePath);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static List<ParcelCandidate> LoadParcelCandidates()
    {
        string[] guids =
            AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { ParcelFolder });

        List<string> paths = new List<string>();

        for (int index = 0; index < guids.Length; index++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[index]);
            if (!string.IsNullOrWhiteSpace(path))
                paths.Add(path);
        }

        paths.Sort(StringComparer.OrdinalIgnoreCase);

        List<ParcelCandidate> result =
            new List<ParcelCandidate>();

        for (int index = 0; index < paths.Count; index++)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(paths[index]);

            YQWorldAssemblyDescriptor assembly =
                prefab != null
                    ? prefab.GetComponent<YQWorldAssemblyDescriptor>()
                    : null;

            YQWorldParcelDescriptor parcel =
                prefab != null
                    ? prefab.GetComponent<YQWorldParcelDescriptor>()
                    : null;

            if (assembly == null ||
                parcel == null ||
                assembly.AssemblyKind != YQWorldAssemblyKind.Parcel)
            {
                continue;
            }

            result.Add(
                new ParcelCandidate
                {
                    prefab = prefab,
                    assembly = assembly,
                    parcel = parcel
                });
        }

        return result;
    }

    private static List<RoadDraft> BuildRoadGraph(
        int seed,
        float width)
    {
        System.Random random = new System.Random(seed);

        RoadDraft main =
            new RoadDraft
            {
                id = "road_viking_main",
                role = "mixed_main",
                width = width
            };

        main.points.Add(new Vector3(-62f, 0f, -8f));
        main.points.Add(new Vector3(-33f, 0f, -3f + Jitter(random, 2f)));
        main.points.Add(new Vector3(0f, 0f, 2f + Jitter(random, 2f)));
        main.points.Add(new Vector3(32f, 0f, 8f + Jitter(random, 2f)));
        main.points.Add(new Vector3(62f, 0f, 4f));

        RoadDraft residential =
            new RoadDraft
            {
                id = "road_viking_residential_branch",
                role = "residential_branch",
                width = Mathf.Max(4.5f, width - 1f)
            };

        residential.points.Add(main.points[1]);
        residential.points.Add(new Vector3(-39f, 0f, 25f + Jitter(random, 2f)));
        residential.points.Add(new Vector3(-23f, 0f, 48f));

        RoadDraft service =
            new RoadDraft
            {
                id = "road_viking_service_spur",
                role = "service_spur",
                width = Mathf.Max(4.5f, width - 1f)
            };

        service.points.Add(main.points[3]);
        service.points.Add(new Vector3(43f, 0f, -18f + Jitter(random, 2f)));
        service.points.Add(new Vector3(59f, 0f, -38f));

        // note: Morphology produces a connected main street, residential branch, and service spur before any parcel is considered.
        return new List<RoadDraft>
        {
            main,
            residential,
            service
        };
    }

    private static float Jitter(
        System.Random random,
        float maximumMagnitude)
    {
        return ((float)random.NextDouble() * 2f - 1f) *
               maximumMagnitude;
    }

    private static List<FrontageSlot> BuildFrontageSlots(
        List<RoadDraft> roads,
        float vergeWidth,
        int seed)
    {
        List<FrontageSlot> result =
            new List<FrontageSlot>();

        int order = 0;

        for (int roadIndex = 0;
             roadIndex < roads.Count;
             roadIndex++)
        {
            RoadDraft road = roads[roadIndex];

            for (int pointIndex = 0;
                 pointIndex < road.points.Count - 1;
                 pointIndex++)
            {
                Vector3 start = road.points[pointIndex];
                Vector3 end = road.points[pointIndex + 1];
                Vector3 delta = end - start;
                float length = delta.magnitude;
                Vector3 tangent = delta.normalized;
                Vector3 left =
                    new Vector3(-tangent.z, 0f, tangent.x);

                float[] parameters = length >= 34f
                    ? new[] { 0.32f, 0.68f }
                    : new[] { 0.5f };

                for (int parameterIndex = 0;
                     parameterIndex < parameters.Length;
                     parameterIndex++)
                {
                    Vector3 roadPoint =
                        Vector3.Lerp(
                            start,
                            end,
                            parameters[parameterIndex]);

                    for (int side = -1; side <= 1; side += 2)
                    {
                        result.Add(
                            new FrontageSlot
                            {
                                roadId = road.id,
                                roadRole = road.role,
                                roadPoint = roadPoint,
                                tangent = tangent,
                                outward = left * side,
                                segmentLength = length,
                                deterministicOrder =
                                    StableOrder(seed, order++)
                            });
                    }
                }
            }
        }

        return result;
    }

    private static int StableOrder(
        int seed,
        int value)
    {
        unchecked
        {
            int hash = seed;
            hash = hash * 397 ^ value;
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            return hash;
        }
    }

    private static List<PlacementDraft> SolveParcels(
        List<ParcelCandidate> candidates,
        List<FrontageSlot> slots,
        YQStreetProfileDefinition streetProfile,
        int seed)
    {
        List<ParcelCandidate> ordered =
            new List<ParcelCandidate>(candidates);

        ordered.Sort(
            (left, right) =>
            {
                int functionComparison =
                    PlacementPriority(left.parcel.ParcelFunction)
                        .CompareTo(
                            PlacementPriority(right.parcel.ParcelFunction));

                return functionComparison != 0
                    ? functionComparison
                    : string.Compare(
                        left.assembly.StableAssemblyId,
                        right.assembly.StableAssemblyId,
                        StringComparison.Ordinal);
            });

        List<PlacementDraft> result =
            new List<PlacementDraft>();

        HashSet<FrontageSlot> used =
            new HashSet<FrontageSlot>();

        for (int candidateIndex = 0;
             candidateIndex < ordered.Count;
             candidateIndex++)
        {
            ParcelCandidate candidate = ordered[candidateIndex];
            List<FrontageSlot> compatible =
                FindCompatibleSlots(
                    candidate,
                    slots,
                    used,
                    seed);

            for (int slotIndex = 0;
                 slotIndex < compatible.Count;
                 slotIndex++)
            {
                FrontageSlot slot = compatible[slotIndex];

                if (candidate.parcel.FrontageWidth >
                    slot.segmentLength - 4f)
                {
                    continue;
                }

                float roadWidth =
                    slot.roadRole == "mixed_main"
                        ? streetProfile.CarriagewayWidth
                        : Mathf.Max(4.5f, streetProfile.CarriagewayWidth - 1f);

                Vector3 position =
                    slot.roadPoint +
                    slot.outward *
                    (roadWidth * 0.5f + streetProfile.VergeWidth);

                Quaternion rotation =
                    Quaternion.LookRotation(
                        slot.outward,
                        Vector3.up);

                OrientedRect footprint =
                    CreateFootprint(
                        position,
                        rotation,
                        candidate.parcel.FrontageWidth,
                        candidate.parcel.ParcelDepth);

                if (OverlapsAny(footprint, result, 0.75f))
                    continue;

                result.Add(
                    new PlacementDraft
                    {
                        candidate = candidate,
                        slot = slot,
                        position = position,
                        rotation = rotation,
                        footprint = footprint
                    });

                used.Add(slot);
                break;
            }
        }

        return result;
    }

    private static int PlacementPriority(
        YQParcelFunction function)
    {
        if (function == YQParcelFunction.LandmarkSupport)
            return 0;
        if (function == YQParcelFunction.Service)
            return 1;
        return 2;
    }

    private static List<FrontageSlot> FindCompatibleSlots(
        ParcelCandidate candidate,
        List<FrontageSlot> slots,
        HashSet<FrontageSlot> used,
        int seed)
    {
        List<FrontageSlot> result =
            slots.FindAll(
                slot =>
                    !used.Contains(slot) &&
                    IsRoleCompatible(
                        candidate.parcel.ParcelFunction,
                        slot.roadRole));

        result.Sort(
            (left, right) =>
            {
                if (candidate.parcel.ParcelFunction ==
                    YQParcelFunction.LandmarkSupport)
                {
                    return right.roadPoint.sqrMagnitude.CompareTo(
                        left.roadPoint.sqrMagnitude);
                }

                if (candidate.parcel.ParcelFunction ==
                    YQParcelFunction.Service)
                {
                    return left.roadPoint.sqrMagnitude.CompareTo(
                        right.roadPoint.sqrMagnitude);
                }

                int leftOrder = left.deterministicOrder ^ seed;
                int rightOrder = right.deterministicOrder ^ seed;
                return leftOrder.CompareTo(rightOrder);
            });

        return result;
    }

    private static bool IsRoleCompatible(
        YQParcelFunction function,
        string roadRole)
    {
        if (function == YQParcelFunction.Service ||
            function == YQParcelFunction.LandmarkSupport)
        {
            return string.Equals(
                roadRole,
                "service_spur",
                StringComparison.Ordinal);
        }

        return string.Equals(
                   roadRole,
                   "mixed_main",
                   StringComparison.Ordinal) ||
               string.Equals(
                   roadRole,
                   "residential_branch",
                   StringComparison.Ordinal);
    }

    private static OrientedRect CreateFootprint(
        Vector3 parcelOrigin,
        Quaternion rotation,
        float width,
        float depth)
    {
        Vector3 axisX3 = rotation * Vector3.right;
        Vector3 axisZ3 = rotation * Vector3.forward;
        Vector3 center3 =
            parcelOrigin + axisZ3 * (depth * 0.5f);

        return new OrientedRect
        {
            center = new Vector2(center3.x, center3.z),
            axisX = new Vector2(axisX3.x, axisX3.z).normalized,
            axisZ = new Vector2(axisZ3.x, axisZ3.z).normalized,
            halfSize = new Vector2(width * 0.5f, depth * 0.5f)
        };
    }

    private static bool OverlapsAny(
        OrientedRect candidate,
        List<PlacementDraft> existing,
        float margin)
    {
        for (int index = 0; index < existing.Count; index++)
        {
            if (Overlaps(candidate, existing[index].footprint, margin))
                return true;
        }

        return false;
    }

    private static bool Overlaps(
        OrientedRect left,
        OrientedRect right,
        float margin)
    {
        Vector2[] axes =
        {
            left.axisX,
            left.axisZ,
            right.axisX,
            right.axisZ
        };

        Vector2 centerDelta = right.center - left.center;

        for (int index = 0; index < axes.Length; index++)
        {
            Vector2 axis = axes[index];
            float distance = Mathf.Abs(Vector2.Dot(centerDelta, axis));
            float leftRadius = ProjectionRadius(left, axis);
            float rightRadius = ProjectionRadius(right, axis);

            if (distance >= leftRadius + rightRadius + margin)
                return false;
        }

        return true;
    }

    private static float ProjectionRadius(
        OrientedRect rectangle,
        Vector2 axis)
    {
        return rectangle.halfSize.x *
                   Mathf.Abs(Vector2.Dot(rectangle.axisX, axis)) +
               rectangle.halfSize.y *
                   Mathf.Abs(Vector2.Dot(rectangle.axisZ, axis));
    }

    private static List<string> ValidateCompilation(
        List<ParcelCandidate> candidates,
        List<RoadDraft> roads,
        List<PlacementDraft> placements)
    {
        List<string> errors = new List<string>();

        if (roads.Count < 3)
            errors.Add("Road graph does not contain the required main, residential, and service branches.");

        if (placements.Count != candidates.Count)
        {
            errors.Add(
                "Only " + placements.Count + " of " + candidates.Count +
                " parcel candidates received valid frontage slots.");
        }

        int residential = 0;
        int service = 0;
        int landmark = 0;

        for (int index = 0; index < placements.Count; index++)
        {
            YQParcelFunction function =
                placements[index].candidate.parcel.ParcelFunction;

            if (function == YQParcelFunction.Residential)
                residential++;
            else if (function == YQParcelFunction.Service)
                service++;
            else if (function == YQParcelFunction.LandmarkSupport)
                landmark++;

            for (int other = index + 1;
                 other < placements.Count;
                 other++)
            {
                if (Overlaps(
                        placements[index].footprint,
                        placements[other].footprint,
                        0.25f))
                {
                    errors.Add(
                        "Forbidden parcel overlap: " +
                        placements[index].candidate.assembly.StableAssemblyId +
                        " and " +
                        placements[other].candidate.assembly.StableAssemblyId + ".");
                }
            }
        }

        if (residential < 5)
            errors.Add("Residential coverage is below the five-parcel benchmark requirement.");
        if (service < 1)
            errors.Add("The service district has no service parcel.");
        if (landmark < 1)
            errors.Add("The settlement has no landmark-support parcel.");

        return errors;
    }

    private static YQCompiledSettlementArtifact WriteArtifact(
        List<RoadDraft> roads,
        List<PlacementDraft> placements,
        List<string> validation,
        bool valid)
    {
        YQCompiledSettlementArtifact artifact =
            AssetDatabase.LoadAssetAtPath<YQCompiledSettlementArtifact>(
                ArtifactPath);

        if (artifact == null)
        {
            artifact = ScriptableObject.CreateInstance<YQCompiledSettlementArtifact>();
            AssetDatabase.CreateAsset(artifact, ArtifactPath);
        }

        List<YQCompiledRoadRecord> roadRecords =
            new List<YQCompiledRoadRecord>();

        for (int index = 0; index < roads.Count; index++)
        {
            roadRecords.Add(
                new YQCompiledRoadRecord
                {
                    stableRoadId = roads[index].id,
                    role = roads[index].role,
                    width = roads[index].width,
                    centerline = new List<Vector3>(roads[index].points)
                });
        }

        List<YQCompiledParcelPlacementRecord> placementRecords =
            new List<YQCompiledParcelPlacementRecord>();

        for (int index = 0; index < placements.Count; index++)
        {
            PlacementDraft placement = placements[index];
            placementRecords.Add(
                new YQCompiledParcelPlacementRecord
                {
                    stableParcelId = placement.candidate.assembly.StableAssemblyId,
                    roadId = placement.slot.roadId,
                    parcelFunction = placement.candidate.parcel.ParcelFunction,
                    position = placement.position,
                    yawDegrees = placement.rotation.eulerAngles.y,
                    footprintSize = new Vector2(
                        placement.candidate.parcel.FrontageWidth,
                        placement.candidate.parcel.ParcelDepth)
                });
        }

        artifact.Configure(
            CompilerVersion,
            BenchmarkSeed,
            "viking_rural_branching_spine",
            "assets_befourstudios_medievalvikingvillage",
            valid,
            roadRecords,
            placementRecords,
            validation);

        EditorUtility.SetDirty(artifact);
        return artifact;
    }

    private static void BuildReviewScene(
        List<RoadDraft> roads,
        List<PlacementDraft> placements)
    {
        Scene scene = SceneManager.GetSceneByPath(ReviewScenePath);
        bool created = !scene.IsValid() || !scene.isLoaded;

        if (created)
        {
            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
        }
        else if (scene.isDirty)
        {
            throw new InvalidOperationException(
                "The settlement compiler benchmark scene has unsaved changes.");
        }

        try
        {
            ClearGeneratedRoot(scene);

            GameObject root =
                new GameObject("00__VIKING_FIXED_SEED_SETTLEMENT");
            SceneManager.MoveGameObjectToScene(root, scene);

            BuildGround(root.transform);
            BuildLighting(root.transform);

            GameObject roadsRoot = new GameObject("Road Graph");
            roadsRoot.transform.SetParent(root.transform, false);

            for (int index = 0; index < roads.Count; index++)
            {
                BuildRoadMesh(
                    roadsRoot.transform,
                    roads[index],
                    index);
            }

            Dictionary<string, Transform> districtRoots =
                new Dictionary<string, Transform>(StringComparer.Ordinal);

            for (int index = 0; index < placements.Count; index++)
            {
                PlacementDraft placement = placements[index];
                string districtName =
                    "District_" + placement.slot.roadRole;

                if (!districtRoots.TryGetValue(
                        districtName,
                        out Transform districtRoot))
                {
                    GameObject district = new GameObject(districtName);
                    district.transform.SetParent(root.transform, false);
                    districtRoot = district.transform;
                    districtRoots[districtName] = districtRoot;
                }

                GameObject instance =
                    PrefabUtility.InstantiatePrefab(
                        placement.candidate.prefab,
                        scene) as GameObject;

                if (instance == null)
                    continue;

                instance.transform.SetParent(districtRoot, false);
                instance.transform.localPosition = placement.position;
                instance.transform.localRotation = placement.rotation;
            }

            // note: The scene is a deterministic golden-master candidate generated from the persisted graph, not a hand-positioned presentation layout.
            if (!EditorSceneManager.SaveScene(scene, ReviewScenePath, false))
            {
                throw new InvalidOperationException(
                    "Unity refused to save the settlement compiler benchmark scene.");
            }
        }
        finally
        {
            if (created)
                EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static void ClearGeneratedRoot(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        for (int index = 0; index < roots.Length; index++)
        {
            if (roots[index] != null &&
                string.Equals(
                    roots[index].name,
                    "00__VIKING_FIXED_SEED_SETTLEMENT",
                    StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(roots[index]);
            }
        }
    }

    private static void BuildRoadMesh(
        Transform parent,
        RoadDraft road,
        int roadIndex)
    {
        string meshPath =
            MeshFolder + "/YQ_Viking_Road_" + roadIndex.ToString("00") + ".asset";

        Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "YQ_Viking_Road_" + road.id };
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        else
        {
            mesh.Clear();
        }

        Vector3[] vertices = new Vector3[road.points.Count * 2];
        Vector2[] uv = new Vector2[vertices.Length];
        int[] triangles = new int[(road.points.Count - 1) * 6];
        float cumulativeDistance = 0f;

        for (int index = 0; index < road.points.Count; index++)
        {
            Vector3 prior = road.points[Mathf.Max(0, index - 1)];
            Vector3 next = road.points[Mathf.Min(road.points.Count - 1, index + 1)];
            Vector3 tangent = (next - prior).normalized;
            Vector3 normal = new Vector3(-tangent.z, 0f, tangent.x);

            if (index > 0)
                cumulativeDistance += Vector3.Distance(road.points[index - 1], road.points[index]);

            vertices[index * 2] =
                road.points[index] - normal * road.width * 0.5f;
            vertices[index * 2 + 1] =
                road.points[index] + normal * road.width * 0.5f;
            uv[index * 2] = new Vector2(0f, cumulativeDistance / road.width);
            uv[index * 2 + 1] = new Vector2(1f, cumulativeDistance / road.width);

            if (index >= road.points.Count - 1)
                continue;

            int triangle = index * 6;
            int vertex = index * 2;
            triangles[triangle] = vertex;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 1;
            triangles[triangle + 4] = vertex + 2;
            triangles[triangle + 5] = vertex + 3;
        }

        mesh.vertices = vertices;
        mesh.uv = uv;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        GameObject roadObject = new GameObject(road.id);
        roadObject.transform.SetParent(parent, false);
        MeshFilter filter = roadObject.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        MeshRenderer renderer = roadObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateMaterial(
            RoadMaterialPath,
            "YQ_Viking_CompilerRoad",
            new Color(0.22f, 0.17f, 0.11f, 1f));
    }

    private static void BuildGround(Transform parent)
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Compiler Ground";
        ground.transform.SetParent(parent, false);
        ground.transform.localPosition = new Vector3(0f, -0.06f, 4f);
        ground.transform.localScale = new Vector3(17f, 1f, 12f);
        ground.GetComponent<Renderer>().sharedMaterial = GetOrCreateMaterial(
            GroundMaterialPath,
            "YQ_Viking_CompilerGround",
            new Color(0.18f, 0.23f, 0.16f, 1f));
    }

    private static void BuildLighting(Transform parent)
    {
        GameObject lightObject = new GameObject("Compiler Sun");
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
    }

    private static Material GetOrCreateMaterial(
        string path,
        string materialName,
        Color color)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material != null)
            return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            return null;

        material = new Material(shader) { name = materialName };
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static void WriteValidationReport(
        List<RoadDraft> roads,
        List<ParcelCandidate> candidates,
        List<PlacementDraft> placements,
        List<string> validation,
        bool valid)
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("# Viking Fixed-Seed Settlement Compiler Validation");
        report.AppendLine();
        report.AppendLine("- Compiler: `" + CompilerVersion + "`");
        report.AppendLine("- Seed: `" + BenchmarkSeed + "`");
        report.AppendLine("- Morphology: `viking_rural_branching_spine`");
        report.AppendLine("- Roads: " + roads.Count);
        report.AppendLine("- Road nodes: " + CountRoadNodes(roads));
        report.AppendLine("- Parcel candidates: " + candidates.Count);
        report.AppendLine("- Parcel placements: " + placements.Count);
        report.AppendLine("- Result: **" + (valid ? "VALID" : "INVALID") + "**");
        report.AppendLine();
        report.AppendLine("## Validation");

        if (validation.Count == 0)
        {
            report.AppendLine("- No forbidden overlaps or missing required functions detected.");
        }
        else
        {
            for (int index = 0; index < validation.Count; index++)
                report.AppendLine("- " + validation[index]);
        }

        report.AppendLine();
        report.AppendLine("## Placements");
        for (int index = 0; index < placements.Count; index++)
        {
            PlacementDraft placement = placements[index];
            report.AppendLine(
                "- `" + placement.candidate.assembly.StableAssemblyId +
                "` -> `" + placement.slot.roadId +
                "` at " + placement.position.ToString("F2") +
                ", yaw " + placement.rotation.eulerAngles.y.ToString("F1"));
        }

        // note: The report is deterministic evidence for this compiler version and seed, suitable for later regression comparison.
        File.WriteAllText(ReportPath, report.ToString());
        AssetDatabase.ImportAsset(ReportPath);
    }

    private static int CountRoadNodes(List<RoadDraft> roads)
    {
        int count = 0;
        for (int index = 0; index < roads.Count; index++)
            count += roads[index].points.Count;
        return count;
    }

    private static void EnsureFolderPath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim('/');
        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts = normalized.Split('/');
        string current = "Assets";
        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[index]);
            current = next;
        }
    }
}
