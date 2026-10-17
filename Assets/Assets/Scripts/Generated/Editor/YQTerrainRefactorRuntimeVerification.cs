#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Named menu adapters reuse the existing protected itinerary, production motor, device input and save rollback owner.
public static class YQTerrainRefactorRuntimeVerification
{
    private const string Command = "outputs/TerrainRefactor_20261012/RuntimeCommand.json";
    private const string CaptureKey = "YQTerrainRefactor.CaptureRejectedDisposableBuild";
    private static double beginDeadline;
    private static string beginProfile, beginArtifact;

    [InitializeOnLoadMethod]
    private static void RegisterCapture()
    {
        // note: A one-shot Editor diagnostic survives the ordinary Play Mode reload; it never starts gameplay, repairs a world or changes acceptance.
        EditorApplication.update -= CaptureRejectedBuild;
        EditorApplication.update += CaptureRejectedBuild;
        AssemblyReloadEvents.beforeAssemblyReload -= CancelBegin;
        AssemblyReloadEvents.beforeAssemblyReload += CancelBegin;
        EditorApplication.playModeStateChanged -= CancelBeginOnExit;
        EditorApplication.playModeStateChanged += CancelBeginOnExit;
    }

    [MenuItem("YourQuest/Terrain Refactor/Arm Rejected Disposable Build Capture (Read Only)")]
    private static void ArmCapture() => SessionState.SetBool(CaptureKey, true);

    [MenuItem("YourQuest/Terrain Refactor/Cancel Rejected Build Capture")]
    private static void CancelCapture() => SessionState.SetBool(CaptureKey, false);

    [MenuItem("YourQuest/Terrain Refactor/Inspect Captured Route Witnesses (Read Only)")]
    private static void InspectWitnesses()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Captured route inspection requires idle Edit Mode.");
        const string fixture = "outputs/TerrainRefactor_20261012/route_startup_rejection/20261012_142159_282";
        string json = File.ReadAllText(Path.Combine(fixture, "Plan.json"));
        var plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(json);
        string before = JsonUtility.ToJson(plan);
        // note: This archived diagnostic asks only the accepted base terrain owner; live continuation admission requires a WorldState and is not claimed here.
        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var prepared, out string failure))
            throw new InvalidOperationException(failure);
        var points = new HashSet<Vector2>();
        foreach (Match match in Regex.Matches(File.ReadAllText("outputs/TerrainRefactor_20261012/route_startup_rejection/Route_Witnesses.txt"),
                     @"(?:point|previousPoint)=\((-?[\d.]+), (-?[\d.]+)\)"))
            points.Add(new Vector2(float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)));
        points.Add(new Vector2(77.57f, 10.30f)); points.Add(new Vector2(77.95f, 10.30f)); points.Add(new Vector2(78.33f, 10.29f));
        var observations = new JArray();
        // note: Sample the captured canonical owner at each actual failed witness and its native supporting corners. No terrain is created or modified.
        foreach (Vector2 point in points)
        {
            var samples = new JArray();
            float x = Mathf.Floor(point.x / 2f) * 2f, z = Mathf.Floor(point.y / 2f) * 2f;
            foreach (Vector2 query in new[] { point, new Vector2(x,z), new Vector2(x+2f,z), new Vector2(x,z+2f), new Vector2(x+2f,z+2f) })
            {
                var sample = prepared.Sample(query.x, query.y);
                var shores = new JArray();
                for (int water = 0; water < plan.spatialPlanV2.blueprint.hydrology.Count; water++)
                {
                    var basin = prepared.GetMaterializedBasin(water);
                    if (basin != null) shores.Add(new JObject { ["id"] = plan.spatialPlanV2.blueprint.hydrology[water].hydrologyId,
                        ["distance"] = basin.SignedShoreDistance(query.x, query.y) });
                }
                samples.Add(new JObject { ["x"] = query.x, ["z"] = query.y, ["canonicalHeight"] = sample.elevationNormalized * 140f,
                    ["waterMask"] = sample.waterMask, ["connectedSupport"] = sample.connectedBasinSupportMask,
                    ["waterIndex"] = sample.waterFeatureIndex, ["routeMask"] = sample.routeMask, ["shores"] = shores });
            }
            observations.Add(new JObject { ["x"] = point.x, ["z"] = point.y, ["samples"] = samples });
        }
        if (before != JsonUtility.ToJson(plan)) throw new InvalidOperationException("Captured route inspection mutated its canonical input.");
        string destination = Path.Combine(fixture, "CanonicalWitnesses.json");
        File.WriteAllText(destination, new JObject { ["utc"] = DateTime.UtcNow.ToString("O"),
            ["artifactHash"] = plan.spatialPlanV2.contentHash, ["observations"] = observations,
            ["evidence"] = "EDITOR_READ_ONLY_CAPTURED_PLAN_SAMPLING; NOT_TRAVERSAL" }.ToString());
        Debug.Log("[YQTerrainRefactor] Captured route witnesses inspected: " + destination);
    }

    private static void CaptureRejectedBuild()
    {
        if (!SessionState.GetBool(CaptureKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling)
            return;
        var builder = YQGeneratedWorldRuntimeBuilder.Instance;
        if (builder == null || (!builder.HasMaterializationFailed && !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased)) return;
        SessionState.SetBool(CaptureKey, false);
        if (!builder.HasMaterializationFailed) return;
        try
        {
            var profiles = YQProfileSaveSystem.Instance;
            string name = profiles?.FindProfile(profiles.ActiveProfileId)?.displayName;
            var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
            if (name == null || (!name.StartsWith("G08-R3 Disposable ", StringComparison.Ordinal) &&
                !name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal)) || plan == null)
                throw new InvalidOperationException("Rejected capture requires the named disposable world.");
            string directory = "outputs/TerrainRefactor_20261013_BankTraversal/native/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
            Directory.CreateDirectory(directory);
            // note: Capture the actual rejected canonical plan before stopping its runtime, rather than regenerating a presumed equivalent seed.
            string before = JsonUtility.ToJson(plan);
            File.WriteAllText(Path.Combine(directory, "Plan.json"), before);
            typeof(YQG08R3PhysicalItineraryVerification).GetMethod("Snapshot", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { directory });
            Terrain terrain = null;
            var root = GameObject.Find("YQ_GENERATED_WORLD_RUNTIME");
            if (root != null) foreach (var candidate in root.GetComponentsInChildren<Terrain>(true))
                if (candidate.name == YQGeneratedWorldTerrain.RuntimeTerrainObjectName) { terrain = candidate; break; }
            if (terrain == null || terrain.terrainData == null) throw new InvalidOperationException("Rejected origin terrain is absent.");
            var data = terrain.terrainData;
            int resolution = data.heightmapResolution;
            float[,] heights = data.GetHeights(0, 0, resolution, resolution);
            using (var writer = new BinaryWriter(File.Create(Path.Combine(directory, "NativeHeights.f32"))))
                for (int z = 0; z < resolution; z++) for (int x = 0; x < resolution; x++) writer.Write(heights[z, x]);
            // note: Read the rejected bank collision geometry alongside its actual terrain; this observer never rebuilds a path or modifies accepted inputs.
            var banks = new JArray();
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.name != "SharedBridgeBankPath" || filter.sharedMesh == null) continue;
                var vertices = new JArray();
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                    vertices.Add(JObject.Parse(JsonUtility.ToJson(filter.transform.TransformPoint(vertex))));
                banks.Add(new JObject { ["owner"] = filter.transform.parent?.name, ["vertices"] = vertices,
                    ["triangles"] = new JArray(filter.sharedMesh.triangles), ["active"] = filter.gameObject.activeInHierarchy });
            }
            File.WriteAllText(Path.Combine(directory, "BankMeshes.json"), banks.ToString());
            if (before != JsonUtility.ToJson(plan)) throw new InvalidOperationException("Rejected capture changed the canonical plan.");
            File.WriteAllText(Path.Combine(directory, "Rejected.json"), new JObject {
                ["utc"] = DateTime.UtcNow.ToString("O"), ["profileId"] = profiles.ActiveProfileId,
                ["seed"] = plan.worldSeed, ["contentHash"] = plan.spatialPlanV2?.contentHash,
                ["decision"] = builder.LastMaterializationDecision, ["resolution"] = resolution,
                ["terrainOrigin"] = JObject.Parse(JsonUtility.ToJson(terrain.transform.position)),
                ["terrainSize"] = JObject.Parse(JsonUtility.ToJson(data.size)),
                ["planUnchanged"] = true, ["evidence"] = "READ_ONLY_REJECTED_DISPOSABLE_RUNTIME; NO_TRAVERSAL_PASS"
            }.ToString());
            Debug.Log("[YQTerrainRefactor] Rejected disposable build captured: " + directory);
        }
        catch (Exception error) { Debug.LogError("[YQTerrainRefactor] Rejected build capture failed: " + error); }
    }

    [MenuItem("YourQuest/Terrain Refactor/Capture Runtime Geometry (Read Only)")]
    private static void Capture()
    {
        // note: The existing census checks the live accepted identity and never moves the player or changes streaming demand.
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (!EditorApplication.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure))
            throw new InvalidOperationException("Realized route capture requires the released accepted world.");
        string before = JsonUtility.ToJson(plan);
        var routes = new JArray(); var banks = new JArray();
        // note: Observe the same cached bridge layout used for publication and acceptance, rather than reconstructing bank approaches from narrative or raw route anchors.
        var centerline = typeof(YQGeneratedWorldEnvironment).GetMethod("GetAcceptedRouteCenterline", BindingFlags.NonPublic | BindingFlags.Static);
        var realize = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("ApplySharedBridgeRoute", BindingFlags.NonPublic | BindingFlags.Static);
        var visit = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("VisitSharedBridgeBankPaths", BindingFlags.NonPublic | BindingFlags.Static);
        if (centerline == null || realize == null || visit == null) throw new InvalidOperationException("The existing internal route observers are unavailable.");
        for (int index = 0; index < prepared.RouteCount; index++)
        {
            // note: Keep the production APIs internal; this Editor-only observer calls exactly the reviewed owners across the assembly boundary.
            var points = (List<Vector2>)centerline.Invoke(null, new object[] { prepared, index });
            realize.Invoke(null, new object[] { prepared, index, points });
            var polyline = new JArray();
            foreach (Vector2 point in points) polyline.Add(new JObject { ["x"] = point.x, ["z"] = point.y });
            routes.Add(new JObject { ["routeId"] = prepared.GetRoute(index).routeId, ["points"] = polyline });
        }
        Action<int, float, IReadOnlyList<Vector3>> collectBanks = (index, width, connection) => {
            var polyline = new JArray();
            foreach (Vector3 point in connection) polyline.Add(JObject.Parse(JsonUtility.ToJson(point)));
            banks.Add(new JObject { ["routeId"] = prepared.GetRoute(index).routeId, ["width"] = width, ["points"] = polyline });
        };
        visit.Invoke(null, new object[] { prepared, collectBanks });
        // note: Observe only the accepted east continuation's registered site owner. Entrance tests consume actual published sockets rather than extending a route through the altar.
        var sites = new JArray();
        var registered = typeof(YQCompiledWorldSiteInstance).GetField("Instances", BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null) as Dictionary<string, YQCompiledWorldSiteInstance>;
        if (registered == null) throw new InvalidOperationException("The existing registered site observer is unavailable.");
        foreach (var pair in registered)
        {
            if (pair.Key != "continuation:east" && pair.Key != "site:continuation:east") continue;
            var owner = pair.Value;
            if (owner == null) continue;
            var sockets = new JArray(); var approachColliders = new JArray();
            foreach (var child in owner.GetComponentsInChildren<Transform>(true))
                if (child.name.StartsWith("Socket_", StringComparison.Ordinal))
                    sockets.Add(new JObject { ["name"] = child.name,
                        ["position"] = JObject.Parse(JsonUtility.ToJson(child.position)),
                        ["active"] = child.gameObject.activeInHierarchy });
            foreach (var collider in owner.GetComponentsInChildren<Collider>(true))
            {
                if (collider.transform.parent == null || collider.transform.parent.name != "Approach") continue;
                approachColliders.Add(new JObject { ["name"] = collider.name, ["type"] = collider.GetType().Name,
                    ["enabled"] = collider.enabled, ["active"] = collider.gameObject.activeInHierarchy,
                    ["trigger"] = collider.isTrigger, ["center"] = JObject.Parse(JsonUtility.ToJson(collider.bounds.center)),
                    ["size"] = JObject.Parse(JsonUtility.ToJson(collider.bounds.size)) });
            }
            sites.Add(new JObject { ["id"] = pair.Key, ["loaded"] = owner.IsLoaded,
                ["active"] = owner.isActiveAndEnabled, ["position"] = JObject.Parse(JsonUtility.ToJson(owner.transform.position)),
                ["sockets"] = sockets, ["approachColliders"] = approachColliders });
        }
        if (before != JsonUtility.ToJson(plan)) throw new InvalidOperationException("Realized route capture changed its accepted plan.");
        string directory = "outputs/TerrainRefactor_20261012/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_realized_routes";
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Routes.json"), new JObject {
            ["utc"] = DateTime.UtcNow.ToString("O"), ["profileId"] = YQProfileSaveSystem.Instance?.ActiveProfileId,
            ["artifactHash"] = plan.spatialPlanV2.contentHash, ["runtimeMvid"] = typeof(YQGeneratedWorldTerrain).Assembly.ManifestModule.ModuleVersionId.ToString(),
            ["planUnchanged"] = true, ["routes"] = routes, ["banks"] = banks, ["sites"] = sites,
            ["evidence"] = "READ_ONLY_PUBLISHED_OWNER_DERIVED_ROUTES; NOT_PLAYER_TRAVERSAL"
        }.ToString());
        YQG08R3PhysicalItineraryVerification.Dispatch(new JObject {
            ["action"] = "hydrology-snapshot", ["profileId"] = YQProfileSaveSystem.Instance?.ActiveProfileId,
            ["artifactHash"] = plan?.spatialPlanV2?.contentHash
        }.ToString());
    }

    [MenuItem("YourQuest/Terrain Refactor/Begin Protected Traversal")]
    private static void Begin()
    {
        // note: Acquire input only for named disposable terrain journeys; accepted player profiles remain outside this test adapter.
        var profiles = YQProfileSaveSystem.Instance;
        string name = profiles?.FindProfile(profiles.ActiveProfileId)?.displayName;
        if (name == null || (!name.StartsWith("G08-R3 Disposable ", StringComparison.Ordinal) &&
            !name.StartsWith("Terrain Apron Playtest ", StringComparison.Ordinal)))
            throw new InvalidOperationException("Select the terrain continuation's disposable journey before acquiring traversal input.");
        if (!EditorApplication.isPlaying || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased ||
            YQDeveloperTestSession.Active)
            throw new InvalidOperationException("Protected traversal requires released gameplay and no existing test session.");
        // note: Wait at most thirty seconds for the existing model owner to become idle; no request is cancelled and the itinerary's own snapshot gate remains authoritative.
        CancelBegin();
        beginProfile = profiles.ActiveProfileId;
        beginArtifact = WorldStateManager.Instance?.State?.generatedWorldPlan?.spatialPlanV2?.contentHash;
        beginDeadline = EditorApplication.timeSinceStartup + 30d;
        EditorApplication.update += BeginWhenIdle;
        Debug.Log("[YQTerrainRefactor] Waiting for an idle model boundary before protected traversal.");
    }

    private static void BeginWhenIdle()
    {
        var profiles = YQProfileSaveSystem.Instance;
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling ||
            profiles?.ActiveProfileId != beginProfile || plan?.spatialPlanV2?.contentHash != beginArtifact ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || YQDeveloperTestSession.Active)
        { CancelBegin(); return; }
        if (EditorApplication.timeSinceStartup >= beginDeadline)
        {
            CancelBegin();
            Debug.LogError("[YQTerrainRefactor] Protected traversal was not acquired: model quiescence deadline exceeded.");
            return;
        }
        var model = LLMClient.Instance;
        if (model != null && (model.IsBusy || model.PendingRequestCount != 0)) return;
        // note: Dispatch once from the observed idle boundary; the existing itinerary captures geometry and independently refuses an unsafe snapshot.
        string profile = beginProfile, artifact = beginArtifact;
        CancelBegin();
        YQG08R3PhysicalItineraryVerification.Dispatch("{\"action\":\"begin\"}");
        Directory.CreateDirectory("outputs/TerrainRefactor_20261012");
        File.WriteAllText("outputs/TerrainRefactor_20261012/TraversalBarrier.json", new JObject {
            ["utc"] = DateTime.UtcNow.ToString("O"), ["profileId"] = profile, ["artifactHash"] = artifact,
            ["modelIdleAtDispatch"] = true, ["existingBarrierAcquired"] = YQDeveloperTestSession.Active,
            ["evidence"] = "UNITY_EDITOR_EXISTING_PROTECTED_ITINERARY; NOT_TRAVERSAL"
        }.ToString());
    }

    private static void CancelBegin()
    {
        EditorApplication.update -= BeginWhenIdle;
        beginProfile = beginArtifact = null; beginDeadline = 0d;
    }

    private static void CancelBeginOnExit(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode || state == PlayModeStateChange.EnteredEditMode) CancelBegin();
    }

    [MenuItem("YourQuest/Terrain Refactor/Run Prepared Traversal Leg")]
    private static void Run()
    {
        // note: A fixed workspace evidence file supplies only the existing bounded walk/observe contract; no arbitrary runtime action is accepted.
        var request = JObject.Parse(File.ReadAllText(Command));
        string action = (string)request["action"];
        if (action != "walk" && action != "observe")
            throw new InvalidOperationException("Prepared terrain legs must use the existing walk or observe action.");
        YQG08R3PhysicalItineraryVerification.Dispatch(request.ToString());
    }

    [MenuItem("YourQuest/Terrain Refactor/Restore Protected Traversal")]
    private static void Restore()
    {
        // note: An explicit restore also disarms a waiting begin, so no delayed callback can acquire input after cleanup.
        CancelBegin();
        YQG08R3PhysicalItineraryVerification.Dispatch("{\"action\":\"restore\"}");
    }
}
#endif
