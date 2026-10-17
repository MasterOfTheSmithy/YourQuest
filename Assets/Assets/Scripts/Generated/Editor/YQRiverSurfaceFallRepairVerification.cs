using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;

// note: Disposable geometry and read-only saved-plan checks reproduce overlapping river/fall projection without entering Play Mode or publishing a profile.
internal static class YQRiverSurfaceFallRepairVerification
{
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void RunFromCommandLine()
    {
        // note: The same detached checks can run in a closed, non-playing batch Editor without creating an interactive session or publishing saves.
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch Editor required.");
        try { Run(); EditorApplication.Exit(0); }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [MenuItem("YourQuest/Verification/Check River Surface And Fall Recovery")]
    private static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Run this detached check outside Play Mode.");
        var checks = new List<object>();
        int failures = 0, samples = 0;
        float rawOverlapRise = 0f, projectedError = 0f;
        void Check(string name, bool pass)
        {
            checks.Add(new { name, pass });
            if (!pass) failures++;
        }
        var preview = EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        var temporaryLayers = new List<TerrainLayer>();
        string savePath = Path.Combine(Application.persistentDataPath, "world_state.json");
        string saveHash = File.Exists(savePath) ? FileHash(savePath) : string.Empty;
        try
        {
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(1024f, 140f, 1024f) };
            var heights = new float[33, 33];
            for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = .25f;
            data.SetHeights(0, 0, heights);
            var ground = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(ground, preview);
            ground.transform.position = new Vector3(-512f, 13f, -512f);
            Terrain terrain = ground.GetComponent<Terrain>();
            GeneratedWorldPlanRecord lastPlan = null;
            YQPreparedSpatialMaterializationV2 lastPrepared = null;
            void InspectPlan(string label, GeneratedWorldPlanRecord plan)
            {
                bool preparedOk = YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string failure);
                Check(label + ": accepted materialization projection " + failure, preparedOk);
                if (!preparedOk) return;
                lastPlan = plan; lastPrepared = prepared;
                var visited = new HashSet<Vector2Int>();
                var all = new HashSet<int>();
                for (int i = 0; i < prepared.WaterCount; i++) all.Add(i);
                for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
                {
                    var water = prepared.GetWater(waterIndex);
                    if (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall) continue;
                    for (int pointIndex = 0; pointIndex < prepared.GetWaterPointCount(waterIndex); pointIndex++)
                    {
                        var point = prepared.GetWaterPoint(waterIndex, pointIndex);
                        var coordinate = new Vector2Int(
                            Mathf.FloorToInt((point.x - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / 128f),
                            Mathf.FloorToInt((point.z - YQContinuousWorldFeatureAuthority.WorldGridOrigin) / 128f));
                        var atPoint = prepared.SampleTerrain(point.x, point.z);
                        if (atPoint.waterFeatureIndex >= 0)
                            rawOverlapRise = Mathf.Max(rawOverlapRise, (point.waterSurfaceNormalized - atPoint.waterSurfaceNormalized) * data.size.y);
                        if (!visited.Add(coordinate)) continue;
                        object[] args = { prepared, coordinate, 128f, terrain, null, 0f, null, null, null };
                        typeof(YQContinuousWorldFeatureMaterializer).GetMethod("TryGetAcceptedRiverPointsCore", PrivateStatic).Invoke(null, args);
                        var points = (List<Vector3>)args[4];
                        var widths = (List<float>)args[7];
                        Check(label + ": widths stay paired in " + coordinate, points.Count == widths.Count);
                        foreach (Vector3 emitted in points)
                        {
                            var sample = prepared.SampleTerrain(emitted.x, emitted.z);
                            if (sample.waterFeatureIndex < 0) continue;
                            samples++;
                            projectedError = Mathf.Max(projectedError,
                                Mathf.Abs(emitted.y - (13f + sample.waterSurfaceNormalized * data.size.y + .005f)));
                        }
                        // note: A retained owner is excluded independently while any other missing feature in the same cell still emits its accepted geometry.
                        args[8] = new HashSet<int> { waterIndex };
                        typeof(YQContinuousWorldFeatureMaterializer).GetMethod("TryGetAcceptedRiverPointsCore", PrivateStatic).Invoke(null, args);
                        Check(label + ": partial retention preserves paired geometry in " + coordinate,
                            ((List<Vector3>)args[4]).Count == ((List<float>)args[7]).Count);
                        args[8] = all;
                        typeof(YQContinuousWorldFeatureMaterializer).GetMethod("TryGetAcceptedRiverPointsCore", PrivateStatic).Invoke(null, args);
                        Check(label + ": all retained finite water is not rebuilt in " + coordinate, ((List<Vector3>)args[4]).Count == 0);
                    }
                }
            }
            foreach (string seed in new[] { "semantic-v2-318", "33b9e87d", "watershed-alpha" })
            {
                var plan = (GeneratedWorldPlanRecord)typeof(YQSemanticWorldAuthorityTests).GetMethod("BuildFixture", PrivateStatic).Invoke(null, new object[] { seed });
                YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
                bool compiled = YQSpatialBlueprintCompilerV2.TryCompile(plan, out var accepted, out string reason);
                Check("fixed seed compiles: " + seed + " " + reason, compiled);
                if (compiled) { plan.spatialPlanV2 = accepted; InspectPlan(seed, plan); }
            }
            if (File.Exists(savePath))
            {
                var serializer = JsonSerializer.Create(new JsonSerializerSettings {
                    Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } });
                var plan = JObject.Parse(File.ReadAllText(savePath))["generatedWorldPlan"]?.ToObject<GeneratedWorldPlanRecord>(serializer);
                Check("active saved plan exists", plan != null);
                if (plan != null) InspectPlan("active saved plan", plan);
            }
            Check("overlap fixture contains an elevated raw fall", rawOverlapRise > 5f);
            Check("all projected water uses the terrain's shared datum", samples > 0 && projectedError < .001f);
            // note: Exercise the real recovery boundary on a disposable authoritative motor; no input or accepted player is selected.
            var actor = new GameObject("DisposableFallRecoveryMotor");
            SceneManager.MoveGameObjectToScene(actor, preview);
            var controller = actor.AddComponent<CharacterController>();
            var motor = actor.AddComponent<YQInvestorPlayerMotor>();
            var safety = actor.AddComponent<YQGeneratedWorldPlayerFallSafety>();
            // note: An active synthetic skeletal body proves terrain recovery retires native bone motion and re-enables the canonical capsule, rather than only zeroing motor fields.
            var ragdoll = actor.AddComponent<YQPlayerRagdoll>();
            var bone = new GameObject("DisposableRagdollBone"); bone.transform.SetParent(actor.transform, false);
            var boneBody = bone.AddComponent<Rigidbody>(); boneBody.useGravity = false; boneBody.linearVelocity = new Vector3(900f, -900f, 0f);
            var boneShape = bone.AddComponent<CapsuleCollider>();
            void RigField(string name, object value) => typeof(YQPlayerRagdoll).GetField(name, PrivateInstance).SetValue(ragdoll, value);
            RigField("motor", motor); RigField("capsule", controller); RigField("capsuleWasEnabled", true);
            RigField("bodies", new[] { boneBody }); RigField("shapes", new Collider[] { boneShape });
            typeof(YQPlayerRagdoll).GetField("<IsSimulating>k__BackingField", PrivateInstance).SetValue(ragdoll, true);
            controller.enabled = false;
            void Field(string name, object value) => typeof(YQInvestorPlayerMotor).GetField(name, PrivateInstance).SetValue(motor, value);
            Field("_verticalVelocity", -95f); Field("_pausedFallVelocity", -95f);
            Field("_planarVelocity", new Vector3(3f, 0f, 4f)); Field("_isSwimming", true); Field("_headUnderwater", true);
            typeof(YQGeneratedWorldPlayerFallSafety).GetMethod("Restore", PrivateInstance).Invoke(safety, new object[] { new Vector3(0f, 50f, 0f) });
            Check("recovery clears accumulated fall velocity", (float)typeof(YQInvestorPlayerMotor).GetField("_verticalVelocity", PrivateInstance).GetValue(motor) == -2f);
            Check("recovery clears planar impulse and stale swimming", !motor.IsSwimming && !motor.IsHeadUnderwater &&
                ((Vector3)typeof(YQInvestorPlayerMotor).GetField("_planarVelocity", PrivateInstance).GetValue(motor)).sqrMagnitude == 0f);
            Check("recovery preserves enabled capsule and target", controller.enabled && actor.transform.position == new Vector3(0f, 50f, 0f));
            Check("terrain recovery retires all native ragdoll physics", !ragdoll.IsSimulating && boneBody.isKinematic && !boneShape.enabled);
            var physicsStep = typeof(YQPlayerRagdoll).GetMethod("IsPhysicsStepSafe", PrivateStatic | BindingFlags.Public);
            bool Step(Vector3 admitted, Vector3 root, Vector3 predicted, float budget) =>
                (bool)physicsStep.Invoke(null, new object[] { admitted, root, predicted, budget });
            Check("normal physical movement fits its admission budget", Step(Vector3.zero, new Vector3(1f, -.2f, 0f), new Vector3(1.1f, -.3f, 0f), 8f));
            Check("accepted fast launch retains its larger admission budget", Step(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(12f, 0f, 0f), 16f));
            Check("observed explosive cliff coordinates cannot reach terrain admission", !Step(Vector3.zero, new Vector3(-747359.5f, -656297.9f, -83177.5f), Vector3.zero, 8f));
            Check("non-finite physical coordinates fail closed", !Step(Vector3.zero, new Vector3(float.NaN, 0f, 0f), Vector3.zero, 8f));

            // note: Compare original and clipped views of one path's procedural style and prove duplicate routes do not accumulate paint opacity.
            var pathType = typeof(YQGeneratedWorldEnvironment).GetNestedType("LivedPathSegment", BindingFlags.NonPublic);
            object path = Activator.CreateInstance(pathType, true);
            void PathField(object target, string name, object value) => pathType.GetField(name).SetValue(target, value);
            PathField(path, "routeId", "fixture-route"); PathField(path, "start", Vector2.zero); PathField(path, "end", new Vector2(100f, 0f));
            PathField(path, "halfWidth", 3f); PathField(path, "shoulderWidth", 4f); PathField(path, "styled", true);
            PathField(path, "styleStart", Vector2.zero); PathField(path, "styleEnd", new Vector2(100f, 0f));
            var centerMethod = typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveLivedPathCenter", PrivateStatic);
            Vector2 Center(object target, float t) => (Vector2)centerMethod.Invoke(null, new object[] { target, t });
            object clippedPath = typeof(YQGeneratedWorldEnvironment).GetMethod("CloneLivedPathSegment", PrivateStatic).Invoke(null, new[] { path });
            PathField(clippedPath, "start", new Vector2(50f, 0f));
            Check("path style pins actual junction endpoints", Center(path, 0f) == Vector2.zero && (Center(path, 1f) - new Vector2(100f, 0f)).sqrMagnitude < .000001f);
            Check("path style does not restart at a clipped tile boundary", (Center(path, .75f) - Center(clippedPath, .5f)).sqrMagnitude < .000001f);
            bool curved = false;
            for (int i = 1; i < 20; i++) curved |= Mathf.Abs(Center(path, i / 20f).y) > .005f;
            Check("a long visible path has gentle non-linear wear", curved);
            var widthMethod = typeof(YQGeneratedWorldEnvironment).GetMethod("LivedPathWidthScale", PrivateStatic | BindingFlags.Public);
            float narrow = 1f, wide = 0f;
            for (int i = 0; i < 100; i++)
            {
                float scale = (float)widthMethod.Invoke(null, new object[] { new Vector2(i * 3f, i * 2f) });
                narrow = Mathf.Min(narrow, scale); wide = Mathf.Max(wide, scale);
            }
            Check("path width varies inside the accepted corridor", narrow >= .8f && wide <= .98f && wide - narrow > .02f);
            var pathList = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(pathType)); pathList.Add(path);
            var maskMethod = typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveLivedPathMask", PrivateStatic);
            float singleMask = (float)maskMethod.Invoke(null, new object[] { pathList, new Vector2(50f, 0f) }); pathList.Add(path);
            Check("shared/overlapping paths paint one surface", singleMask == (float)maskMethod.Invoke(null, new object[] { pathList, new Vector2(50f, 0f) }));
            var shader = Shader.Find("YourQuest/Grounded Path");
            Check("grounded path shader imports without reported errors", shader != null && !ShaderUtil.ShaderHasError(shader));

            // note: Real native splat data must prove each dry route before a painted surface can replace its ribbon; empty and weak paint cannot count as realized content.
            var paintMethod = typeof(YQGeneratedWorldEnvironment).GetMethod("TryCountPublishedLivedRoutePaint", PrivateStatic | BindingFlags.Public);
            bool Painted(Vector2Int cell, out int count)
            {
                object[] args = { lastPlan, terrain, lastPrepared, cell, 128f, 0 };
                bool result = (bool)paintMethod.Invoke(null, args);
                count = (int)args[5]; return result;
            }
            Check("absent path layers cannot replace accepted ribbons", !Painted(Vector2Int.zero, out int absentCount) && absentCount == 0);
            var groundLayer = new TerrainLayer { name = "Fixture_Ground" };
            var pathLayer = new TerrainLayer { name = "Fixture_LivedPath" };
            temporaryLayers.Add(groundLayer); temporaryLayers.Add(pathLayer);
            data.alphamapResolution = 64; data.terrainLayers = new[] { groundLayer, pathLayer };
            void FillPaint(float alpha)
            {
                var paint = new float[64, 64, 2];
                for (int z = 0; z < 64; z++) for (int x = 0; x < 64; x++) { paint[z, x, 0] = 1f - alpha; paint[z, x, 1] = alpha; }
                data.SetAlphamaps(0, 0, paint);
            }
            FillPaint(1f);
            bool foundPainted = false;
            Vector2Int paintedCell = Vector2Int.zero;
            for (int z = 0; z < 8 && !foundPainted; z++) for (int x = 0; x < 8 && !foundPainted; x++)
            {
                var cell = new Vector2Int(x, z);
                if (Painted(cell, out int count) && count > 0) { paintedCell = cell; foundPainted = true; }
            }
            Check("actual published dry route paint can replace its duplicate ribbon", foundPainted);
            FillPaint(0f);
            Check("an unpainted accepted route retains its ribbon fallback", foundPainted && !Painted(paintedCell, out int emptyCount) && emptyCount == 0);
            FillPaint(.1f);
            Check("insufficient path paint never certifies route coverage", foundPainted && !Painted(paintedCell, out int weakCount) && weakCount == 0);

            // note: Reproduce a terrain retiring while its shared bridge cache is still building, then exercise both callers rather than only the layout helper.
            object layouts = typeof(YQContinuousWorldFeatureMaterializer).GetField("BridgeLayouts", PrivateStatic).GetValue(null);
            object[] cacheArgs = { lastPrepared, null };
            bool cacheExists = (bool)layouts.GetType().GetMethod("TryGetValue").Invoke(layouts, cacheArgs);
            object bridgeCache = cacheExists ? cacheArgs[1] : Activator.CreateInstance(
                typeof(YQContinuousWorldFeatureMaterializer).GetNestedType("AcceptedBridgeLayout", BindingFlags.NonPublic), true);
            if (!cacheExists) layouts.GetType().GetMethod("Add").Invoke(layouts, new[] { lastPrepared, bridgeCache });
            var buildingField = bridgeCache.GetType().GetField("building");
            bool priorBuilding = (bool)buildingField.GetValue(bridgeCache);
            var retiredGround = Terrain.CreateTerrainGameObject(data);
            SceneManager.MoveGameObjectToScene(retiredGround, preview);
            Terrain retiredTerrain = retiredGround.GetComponent<Terrain>();
            bool callbackInvoked = false, canceledLayout = false;
            Action<object> onLayout = value => { callbackInvoked = true; canceledLayout = value == null; };
            var bridgeMethod = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("PrepareBridgeLayoutRoutine", PrivateStatic);
            var waiting = (System.Collections.IEnumerator)bridgeMethod.Invoke(null, new object[] { lastPrepared, retiredTerrain, onLayout });
            try
            {
                buildingField.SetValue(bridgeCache, true);
                Check("pending bridge layout yields while its shared cache builds", waiting.MoveNext());
                UnityEngine.Object.DestroyImmediate(retiredGround);
                Check("retired terrain cancels pending bridge layout without native access", !waiting.MoveNext() && callbackInvoked && canceledLayout);
            }
            finally { (waiting as IDisposable)?.Dispose(); buildingField.SetValue(bridgeCache, priorBuilding); }
            bool FinishesCanceled(System.Collections.IEnumerator routine)
            {
                try
                {
                    if (!routine.MoveNext()) return true;
                    var nested = routine.Current as System.Collections.IEnumerator;
                    if (nested == null || nested.MoveNext()) return false;
                    return !routine.MoveNext();
                }
                finally { (routine as IDisposable)?.Dispose(); }
            }
            Type spanListType = bridgeMethod.GetParameters()[2].ParameterType.GetGenericArguments()[0];
            object spanList = Activator.CreateInstance(spanListType);
            var cellRoutine = (System.Collections.IEnumerator)typeof(YQContinuousWorldFeatureMaterializer).GetMethod("PrepareAcceptedCrossingSpansRoutine", PrivateStatic)
                .Invoke(null, new object[] { lastPrepared, retiredTerrain, Vector2Int.zero, 128f, spanList, null });
            Check("streamed crossing caller handles retired terrain without a null layout dereference", FinishesCanceled(cellRoutine));
            var originRoutine = (System.Collections.IEnumerator)typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildAcceptedOriginCrossingsRoutine", PrivateStatic | BindingFlags.Public)
                .Invoke(null, new object[] { ground.transform, retiredTerrain, lastPrepared, null });
            Check("origin crossing caller cancels before building on a retired terrain", FinishesCanceled(originRoutine));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            foreach (var layer in temporaryLayers) if (layer != null) UnityEngine.Object.DestroyImmediate(layer);
        }
        Check("active world file was never rewritten", saveHash == (File.Exists(savePath) ? FileHash(savePath) : string.Empty));
        Directory.CreateDirectory("outputs/GoddessWaterLoadRepair_20261007");
        File.WriteAllText("outputs/GoddessWaterLoadRepair_20261007/EditorWaterChecks.json", JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow, evidence = "Detached Unity Editor geometry/recovery checks; no ordinary travel or crash reproduction claim",
            seed = "fixed fixtures and read-only active accepted plan", checks, failures, samples, rawOverlapRise, projectedError
        }, Formatting.Indented));
        if (failures > 0) throw new InvalidOperationException("River/fall repair checks failed: " + failures);
        Debug.Log("[YQRiverFallRepair] " + checks.Count + " detached Editor checks PASS; " + samples + " shared-datum samples; max error=" + projectedError);
    }

    private static string FileHash(string path)
    {
        using (var hash = SHA256.Create()) return Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path)));
    }
}

// note: This explicit, assisted witness uses the existing player, river and rollback barrier; it never creates terrain or publishes a profile.
[InitializeOnLoad]
internal static class YQRiverBankExitRuntimeVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/RiverBankExit.request";
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<object> Samples = new List<object>();
    private static readonly List<string> Errors = new List<string>();
    private static YQInvestorPlayerMotor motor;
    private static Terrain terrain;
    private static Keyboard keyboard, priorKeyboard;
    private static Dictionary<string, string> saveHashes;
    private static YQPreparedSpatialMaterializationV2 prepared;
    private static bool running, swam, submerged, surfaced, landed;
    private static float maxWaterError, deepestPenetration;
    private static int initialRecoveries;
    private static double start, nextSample;

    static YQRiverBankExitRuntimeVerification()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += End;
        EditorApplication.playModeStateChanged += state => { if (state == PlayModeStateChange.ExitingPlayMode) End(); };
    }

    [MenuItem("YourQuest/Verification/Protected River Bank Exit Witness")]
    private static void Begin()
    {
        if (!EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/Assets/Scenes/YourQuest_PlaySafe.unity" ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || Time.timeScale <= 0f)
            throw new InvalidOperationException("Released production PlaySafe is required.");
        motor = YQInvestorPlayerMotor.ActiveMotor;
        terrain = YQPlayerFollowingSemanticChunkStreamer.Active?.AuthoredTerrain;
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (motor == null || !motor.CanProcessMovementInput || terrain == null ||
            !YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out string reason))
            throw new InvalidOperationException("Player, loaded terrain and accepted water are required.");

        // note: Select a loaded deep river with a dry, traversable bank using the accepted projection and the actual native water mesh.
        Vector3 waterPosition = default, bankPosition = default;
        float best = float.PositiveInfinity;
        for (int w = 0; w < prepared.WaterCount; w++)
        {
            var water = prepared.GetWater(w);
            if (water.kind != YQHydrologyKindV2.River) continue;
            for (int p = 0; p + 1 < prepared.GetWaterPointCount(w); p++)
            {
                var a = prepared.GetWaterPoint(w, p); var b = prepared.GetWaterPoint(w, p + 1);
                Vector3 direction = new Vector3(b.x - a.x, 0f, b.z - a.z).normalized;
                Vector3 side = new Vector3(direction.z, 0f, -direction.x);
                for (int sample = 1; sample < 20; sample++)
                {
                    float t = sample / 20f;
                    Vector3 point = new Vector3(Mathf.Lerp(a.x, b.x, t), 0f, Mathf.Lerp(a.z, b.z, t));
                    if (!Contains(point) || !YQWaterSurfaceMotion.TryGetWaterHeight(point, out float height) || height - Ground(point) < 2.1f) continue;
                    float halfWidth = Mathf.Max(water.nominalWidth, Mathf.Lerp(a.width, b.width, t)) * .5f;
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        Vector3 bank = point + side * (sign * (halfWidth + 4f));
                        if (!Contains(bank) || YQWaterSurfaceMotion.TryGetWaterHeight(bank, out _)) continue;
                        bank.y = Ground(bank);
                        Vector3 local = bank - terrain.transform.position;
                        if (bank.y - height > 5f || terrain.terrainData.GetInterpolatedNormal(local.x / terrain.terrainData.size.x, local.z / terrain.terrainData.size.z).y < .65f) continue;
                        float distance = (point - motor.transform.position).sqrMagnitude;
                        if (distance >= best) continue;
                        best = distance; waterPosition = new Vector3(point.x, height - 1.95f, point.z); bankPosition = bank;
                    }
                }
            }
        }
        if (float.IsPositiveInfinity(best)) throw new InvalidOperationException("No loaded river with a safe dry bank was found.");
        if (!YQDeveloperTestSession.Begin(out string failure)) throw new InvalidOperationException(failure);
        saveHashes = ReadSaveHashes();
        Vector3 originalPosition = motor.transform.position;
        Quaternion originalRotation = motor.transform.rotation;
        var originalFields = new Dictionary<FieldInfo, object>();
        foreach (string name in new[] { "_yaw", "_pitch", "_verticalVelocity", "_pausedFallVelocity", "_planarVelocity", "_supportVelocity", "_breathRemaining", "_headUnderwater", "_isSwimming" })
        {
            var field = typeof(YQInvestorPlayerMotor).GetField(name, Fields);
            if (field != null) originalFields.Add(field, field.GetValue(motor));
        }
        var player = PlayerStateManager.Instance.state;
        Vector3 last = player.lastPosition, logical = player.logicalPosition, origin = player.renderOrigin;
        YQDeveloperTestSession.OnRestore(() =>
        {
            if (motor != null)
            {
                var capsule = motor.GetComponent<CharacterController>(); bool enabled = capsule.enabled;
                capsule.enabled = false; motor.transform.SetPositionAndRotation(originalPosition, originalRotation);
                foreach (var entry in originalFields) entry.Key.SetValue(motor, entry.Value);
                capsule.enabled = enabled;
            }
            player.lastPosition = last; player.logicalPosition = logical; player.renderOrigin = origin;
        });
        // note: Only the initial river placement and facing are assisted; native input, collision and buoyancy own the exit onto the bank.
        var controller = motor.GetComponent<CharacterController>(); bool controllerEnabled = controller.enabled;
        controller.enabled = false;
        float yaw = Quaternion.LookRotation(new Vector3(bankPosition.x - waterPosition.x, 0f, bankPosition.z - waterPosition.z)).eulerAngles.y;
        motor.transform.SetPositionAndRotation(waterPosition, Quaternion.Euler(0f, yaw, 0f));
        typeof(YQInvestorPlayerMotor).GetField("_yaw", Fields).SetValue(motor, yaw);
        typeof(YQInvestorPlayerMotor).GetField("_pitch", Fields).SetValue(motor, 0f);
        typeof(YQInvestorPlayerMotor).GetMethod("ResetAfterFallRecovery", Fields).Invoke(motor, null);
        controller.enabled = controllerEnabled;
        initialRecoveries = motor.GetComponent<YQGeneratedWorldPlayerFallSafety>()?.RecoveryCount ?? 0;
        priorKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.onBeforeUpdate += Input; Application.logMessageReceived += Log;
        Samples.Clear(); Errors.Clear(); swam = submerged = surfaced = landed = false;
        maxWaterError = deepestPenetration = 0f;
        start = nextSample = EditorApplication.timeSinceStartup; running = true;
        Debug.Log("[YQRiverBankExit] Protected assisted check started at " + waterPosition + "; bank=" + bankPosition);
    }

    private static bool Contains(Vector3 point)
    {
        Vector3 local = point - terrain.transform.position;
        return local.x >= 0f && local.z >= 0f && local.x <= terrain.terrainData.size.x && local.z <= terrain.terrainData.size.z;
    }
    private static float Ground(Vector3 point) => terrain.SampleHeight(point) + terrain.transform.position.y;
    private static void Input()
    {
        if (!running || keyboard == null) return;
        double elapsed = EditorApplication.timeSinceStartup - start;
        // note: Dive, surface, swim and jump toward the selected bank, then allow the capsule to settle without movement input.
        InputSystem.QueueStateEvent(keyboard, elapsed < 3 ? new KeyboardState(Key.LeftCtrl) : elapsed < 7 ? new KeyboardState(Key.Space) :
            elapsed < 24 ? new KeyboardState(Key.W, Key.Space) : new KeyboardState());
    }
    private static void Log(string message, string trace, LogType type)
    { if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && Errors.Count < 32) Errors.Add(message); }
    private static void Tick()
    {
        if (!running)
        {
            if (!File.Exists(Request) || !EditorApplication.isPlaying || EditorApplication.isCompiling || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased) return;
            if (LLMClient.Instance != null && LLMClient.Instance.IsBusy) return;
            AssetDatabase.DeleteAsset(Request);
            try { Begin(); } catch (Exception error) { Debug.LogException(error); }
            return;
        }
        if (!EditorApplication.isPlaying || motor == null) { End(); return; }
        double now = EditorApplication.timeSinceStartup, elapsed = now - start;
        swam |= motor.IsSwimming; submerged |= motor.IsHeadUnderwater;
        surfaced |= elapsed > 5 && motor.IsSwimming && !motor.IsHeadUnderwater;
        landed |= elapsed >= 24 && !motor.IsSwimming && motor.IsGrounded;
        if (now >= nextSample)
        {
            nextSample = now + .25;
            Vector3 position = motor.transform.position;
            float ground = Contains(position) ? Ground(position) : float.NaN;
            bool wet = YQWaterSurfaceMotion.TryGetWaterHeight(position, out float waterHeight);
            var sample = prepared.SampleTerrain(position.x, position.z);
            float expectedWater = terrain.transform.position.y + sample.waterSurfaceNormalized * terrain.terrainData.size.y + .005f;
            if (wet && sample.waterFeatureIndex >= 0) maxWaterError = Mathf.Max(maxWaterError, Mathf.Abs(waterHeight - expectedWater));
            if (!float.IsNaN(ground)) deepestPenetration = Mathf.Max(deepestPenetration, ground - position.y);
            Samples.Add(new { elapsed, position, swimming = motor.IsSwimming, underwater = motor.IsHeadUnderwater, grounded = motor.IsGrounded,
                ground, wet, waterHeight, expectedWater, recoveries = motor.GetComponent<YQGeneratedWorldPlayerFallSafety>()?.RecoveryCount });
        }
        if (elapsed >= 32) End();
    }
    private static Dictionary<string, string> ReadSaveHashes()
    {
        var hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.GetFiles(Application.persistentDataPath, "*.json", SearchOption.AllDirectories))
            using (var hash = SHA256.Create()) hashes.Add(path, Convert.ToBase64String(hash.ComputeHash(File.ReadAllBytes(path))));
        return hashes;
    }
    private static void End()
    {
        if (!running) return;
        running = false; InputSystem.onBeforeUpdate -= Input; Application.logMessageReceived -= Log;
        if (keyboard != null) InputSystem.RemoveDevice(keyboard); keyboard = null; priorKeyboard?.MakeCurrent();
        bool restingOnBank = motor != null && !motor.IsSwimming && motor.IsGrounded;
        bool noRecovery = motor != null && (motor.GetComponent<YQGeneratedWorldPlayerFallSafety>()?.RecoveryCount ?? 0) == initialRecoveries;
        bool restored = YQDeveloperTestSession.RestoreForPlayModeExit(out string failure);
        var after = ReadSaveHashes(); bool unchangedSaves = after.Count == saveHashes.Count;
        foreach (var entry in saveHashes) unchangedSaves &= after.TryGetValue(entry.Key, out string hash) && hash == entry.Value;
        string directory = "outputs/GoddessWaterLoadRepair_20261007"; Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "RuntimeBankExit_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + ".json");
        File.WriteAllText(path, JsonConvert.SerializeObject(new
        {
            utc = DateTime.UtcNow, evidence = "ASSISTED_NATIVE_PLAYER_RIVER_BANK_EXIT_NOT_ORDINARY_EXPLORATION_OR_BUILD_ACCEPTANCE",
            verdict = swam && submerged && surfaced && landed && restingOnBank && noRecovery && maxWaterError < .05f && deepestPenetration < 6f && restored && unchangedSaves && Errors.Count == 0 ? "PASS" : "FAIL",
            seed = WorldStateManager.Instance?.State?.generatedWorldPlan?.worldSeed, swam, submerged, surfaced, landed, restingOnBank, noRecovery,
            maxWaterError, deepestPenetration, restored, unchangedSaves, failure, samples = Samples, errors = Errors
        }, Formatting.Indented, new JsonSerializerSettings { Converters = { new Vector3JsonConverter() } }));
        Debug.Log("[YQRiverBankExit] Complete; landed=" + landed + "; restored=" + restored + "; unchanged saves=" + unchangedSaves + "; receipt=" + path);
    }
}
