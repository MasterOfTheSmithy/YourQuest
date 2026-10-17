#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// note: One-shot detached replay uses the failed accepted plan and production ecology provider without selecting, mutating, or saving a profile.
[InitializeOnLoad]
internal static class YQHydrologyEcologyCoverageVerification
{
    private const string Request = "Assets/Assets/EditorBuildRequests/HydrologyEcologyCoverage.request";
    private const string DirectoryPath = "outputs/Hydrology_Ecology_Repair_20261006";
    private static readonly MethodInfo DomainQuery = typeof(YQGeneratedWorldEnvironment).GetMethod(
        "ResolveVegetationPlacementBounds", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly MethodInfo AquaticQuery = typeof(YQGeneratedWorldEnvironment).GetMethod(
        "IsCellVegetationExcludedByAreaWater", BindingFlags.Static | BindingFlags.NonPublic);

    static YQHydrologyEcologyCoverageVerification() => EditorApplication.update += Tick;

    private static void Tick()
    {
        // note: Detached admission regression never selects or saves a profile and cannot run during gameplay.
        const string frontierRequest = "Assets/Assets/EditorBuildRequests/FrontierParentAdmission.request";
        if (File.Exists(frontierRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AssetDatabase.DeleteAsset(frontierRequest);
            ReplayFrontierParentAdmission();
        }
        // note: This owned one-shot request runs only the read-only failed-world replay, outside Play Mode.
        const string loadRequest = "Assets/Assets/EditorBuildRequests/WorldLoadEcologyReplay.request";
        if (File.Exists(loadRequest) && !EditorApplication.isCompiling && !EditorApplication.isUpdating &&
            !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            AssetDatabase.DeleteAsset(loadRequest);
            ReplayWorldLoad();
        }
        if (!File.Exists(Request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode) return;
        AssetDatabase.DeleteAsset(Request);
        Run();
    }

    [MenuItem("YourQuest/Verification/Hydrology Ecology Coverage Contracts")]
    internal static void Run()
    {
        var checks = new List<object>();
        int passed = 0, failed = 0;
        GeneratedWorldPlanRecord plan = null;
        YQPreparedSpatialMaterializationV2 prepared = null;
        string originalHash = null;
        object replay = null;
        Bounds cell = new Bounds(new Vector3(576f, 0f, 320f), new Vector3(128f, 10000f, 128f));
        Vector3 tileOrigin = new Vector3(512f, 0f, 256f), tileSize = new Vector3(128f, 140f, 128f);
        void Check(string name, Action action)
        {
            try { action(); passed++; checks.Add(new { name, passed = true, failure = "" }); }
            catch (Exception exception) { failed++; checks.Add(new { name, passed = false, failure = (exception.InnerException ?? exception).Message }); }
        }

        Check("exact failed seed passes accepted-plan validation", () =>
        {
            plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(
                File.ReadAllText(Path.Combine(DirectoryPath, "Failed_Plan_Fixture.json")));
            originalHash = plan.spatialPlanV2.contentHash;
            Require(plan.worldSeed == "396c690c" && originalHash == "4d04b2df6bc5f0cb",
                "The detached fixture differs from the observed failed run.");
            Require(YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out prepared, out string reason), reason);
        });
        Check("aquatic proof uses the actual groundable domain", () =>
        {
            Bounds domain = Domain(cell, tileOrigin, tileSize);
            Require(domain.min.x == 515f && domain.max.x == 637f && domain.min.z == 259f && domain.max.z == 381f,
                "Groundable bounds differ from the production three-metre placement margin.");
            bool wholeCell = Aquatic(cell, prepared), groundable = Aquatic(domain, prepared);
            float minimumMask = 1f;
            for (int z = 0; z <= 61; z++)
                for (int x = 0; x <= 61; x++)
                    minimumMask = Mathf.Min(minimumMask, prepared.SampleTerrain(515f + x * 2f, 259f + z * 2f).waterMask);
            replay = new { seed = plan.worldSeed, hash = originalHash, wholeCell, groundable, minimumMask };
            Require(!wholeCell && groundable && minimumMask >= .12f,
                "The failed cell does not prove complete water coverage of its plantable domain.");
        });
        Check("production provider publishes required aquatic ecology", () =>
        {
            // note: A disposable preview tile exercises the real readiness callback before any optional scatter starts; no imported prefab or gameplay scene is changed.
            Scene preview = EditorSceneManager.NewPreviewScene();
            TerrainData data = null;
            YQRuntimeWorldAssetRegistry registry = null;
            IEnumerator work = null;
            try
            {
                var root = new GameObject("AquaticEcologyCoverageFixture");
                SceneManager.MoveGameObjectToScene(root, preview);
                data = new TerrainData { heightmapResolution = 33, size = tileSize };
                GameObject terrainObject = Terrain.CreateTerrainGameObject(data);
                SceneManager.MoveGameObjectToScene(terrainObject, preview);
                terrainObject.transform.SetParent(root.transform, false);
                terrainObject.transform.position = tileOrigin;
                registry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
                int required = int.MinValue;
                string rejection = null;
                work = YQGeneratedWorldEnvironment.BuildSemanticChunkScatterRoutine(root.transform,
                    terrainObject.GetComponent<Terrain>(), plan,
                    new GeneratedSemanticChunkRecord { chunkX = 8, chunkZ = 6, chunkId = "396c690c:chunk:8:6" },
                    plan.regions[0], new GeneratedRegionAssetPaletteRecord(), registry, 128f, null,
                    result => required = result, failure => rejection = failure);
                for (int step = 0; step < 16 && required == int.MinValue && work.MoveNext(); step++) { }
                Require(required > 0 && rejection == null,
                    "The real provider did not publish aquatic readiness: result=" + required + " failure=" + rejection);
            }
            finally
            {
                (work as IDisposable)?.Dispose();
                EditorSceneManager.ClosePreviewScene(preview);
                if (data != null) UnityEngine.Object.DestroyImmediate(data);
                if (registry != null) UnityEngine.Object.DestroyImmediate(registry);
            }
        });
        Check("authored terrain keeps internal cell boundaries usable", () =>
        {
            Bounds domain = Domain(cell, Vector3.zero, new Vector3(1024f, 140f, 1024f));
            Require(domain.min.x == cell.min.x && domain.max.x == cell.max.x &&
                domain.min.z == cell.min.z && domain.max.z == cell.max.z && !Aquatic(domain, prepared),
                "A chunk-local inset incorrectly removed usable terrain inside a larger authored owner.");
        });
        Check("neighbouring dry shoreline retains required foliage", () =>
        {
            Bounds shore = new Bounds(new Vector3(448f, 0f, 320f), cell.size);
            Bounds domain = Domain(shore, new Vector3(384f, 0f, 256f), tileSize);
            Require(!Aquatic(domain, prepared), "Dry shore habitat was incorrectly waived.");
        });
        Check("accepted plan and hydrology remain unchanged", () =>
            Require(plan.spatialPlanV2.contentHash == originalHash &&
                YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2) == originalHash,
                "The replay changed accepted content or its hash."));

        Directory.CreateDirectory(DirectoryPath);
        File.WriteAllText(Path.Combine(DirectoryPath, "Coverage_Contracts.json"), JsonConvert.SerializeObject(new
        {
            evidence = "detached native provider and fixed-seed sampler replay; production gameplay release is not verified",
            passed, failed, checks, replay, runtimeMvid = typeof(YQGeneratedWorldEnvironment).Module.ModuleVersionId,
            utc = DateTime.UtcNow
        }, Formatting.Indented));
        Debug.Log("[YQHydrologyEcologyCoverage] Contracts passed=" + passed + " failed=" + failed);
        if (failed > 0) throw new InvalidOperationException("Hydrology ecology coverage contracts failed: " + failed);
    }

    // note: Editor assembly access does not widen the internal production APIs.
    private static Bounds Domain(Bounds cell, Vector3 origin, Vector3 size) =>
        (Bounds)DomainQuery.Invoke(null, new object[] { cell, origin, size });
    private static bool Aquatic(Bounds cell, YQPreparedSpatialMaterializationV2 prepared) =>
        (bool)AquaticQuery.Invoke(null, new object[] { cell, prepared, null });
    private static void Require(bool accepted, string failure)
    { if (!accepted) throw new InvalidOperationException(failure); }

    [MenuItem("YourQuest/Verification/Frontier Parent Admission Replay (Read Only)")]
    internal static void ReplayFrontierParentAdmission()
    {
        // note: Compare complete admission with exact prepared reuse on the reported accepted seed; all corruption probes operate on this detached fixture.
        const string output = "outputs/PeriodicHitchRepair_20261011";
        var plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/WorldLoadRepair_20261011/Failed_Plan_Fixture.json"));
        var checks = new List<object>(); int failed = 0;
        void Check(string name, Action action)
        {
            try { action(); checks.Add(new { name, passed = true }); }
            catch (Exception exception) { failed++; checks.Add(new { name, passed = false, failure = exception.Message }); }
        }
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out string reason), reason);
        double fullValidationMs = watch.Elapsed.TotalMilliseconds;
        Require(YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var admitted, out reason), reason);
        string originalHash = plan.spatialPlanV2.contentHash;
        watch.Restart();
        Check("unchanged parent reuses fully admitted immutable projection", () =>
        {
            for (int repeat = 0; repeat < 5; repeat++)
                Require(YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var current, out reason) &&
                    ReferenceEquals(current, admitted), reason);
        });
        double fiveWarmLookupsMs = watch.Elapsed.TotalMilliseconds;
        void RejectedMutation(string name, Action mutate, Action restore)
        {
            Check(name, () =>
            {
                try
                {
                    mutate();
                    Require(!YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var current, out _) ||
                        !ReferenceEquals(current, admitted), "Changed source reused the previous admission.");
                }
                finally { restore(); }
            });
        }
        string seed = plan.worldSeed;
        RejectedMutation("changed world seed cannot reuse admission", () => plan.worldSeed += "-changed", () => plan.worldSeed = seed);
        string semantic = plan.spatialPlan.semanticFingerprint;
        RejectedMutation("changed semantic fingerprint cannot reuse admission", () => plan.spatialPlan.semanticFingerprint += "-changed", () => plan.spatialPlan.semanticFingerprint = semantic);
        var basin = plan.spatialPlanV2.blueprint.hydrology.Find(water => water.connectedDomain != null).connectedDomain;
        float bed = basin.bed[0];
        RejectedMutation("unrevisioned deep water-bed mutation cannot reuse admission", () => basin.bed[0] += .01f, () => basin.bed[0] = bed);
        var site = plan.spatialPlanV2.blueprint.sites[0];
        RejectedMutation("unrevisioned member footprint cannot reuse admission", () => site.memberFootprint.Add(new YQSiteMemberFootprintV2 {
            memberId = "detached-invalid-member", x = float.NaN, z = site.z, reservedRadius = -1f }), () => site.memberFootprint.RemoveAt(site.memberFootprint.Count - 1));
        string validatedHash = plan.spatialPlanV2.validatedContentHash;
        RejectedMutation("changed validation stamp cannot reuse admission", () => plan.spatialPlanV2.validatedContentHash = "changed", () => plan.spatialPlanV2.validatedContentHash = validatedHash);
        Check("restored accepted source and hashes remain intact", () => Require(
            YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var restored, out reason) && ReferenceEquals(restored, admitted) &&
            YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2) == originalHash, reason));
        Directory.CreateDirectory(output);
        File.WriteAllText(output + "/Admission_Replay.json", JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow, evidence = "UNITY_NATIVE_DETACHED_PARENT_ADMISSION_NOT_GAMEPLAY",
            runtimeMvid = typeof(YQGeneratedWorldRuntimeBuilder).Assembly.ManifestModule.ModuleVersionId,
            seed = plan.worldSeed, hash = originalHash, fullValidationMs, fiveWarmLookupsMs,
            passed = checks.Count - failed, failed, checks }, Formatting.Indented));
        Require(failed == 0, "Frontier parent admission replay failed " + failed + " checks.");
        Debug.Log("[YQFrontierAdmission] Exact-parent reuse and changed-source rejection passed.");
    }

    [MenuItem("YourQuest/Verification/World Load Ecology Replay (Read Only)")]
    internal static void ReplayWorldLoad()
    {
        // note: Replay the reported accepted seed without selecting or changing its saved profile.
        const string output = "outputs/WorldLoadRepair_20261011";
        var plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText(output + "/Failed_Plan_Fixture.json"));
        Require(plan.worldSeed == "110b8224" && plan.spatialPlanV2.contentHash == "8abef624410f2f54", "Unexpected load fixture.");
        Require(YQSpatialMaterializationResolverV2.TryGetPrepared(plan, out var prepared, out string reason), reason);
        var probes = new List<object>();
        // note: Compare the exact failed coverage remnant with both canonical query owners and published channel triangles.
        float witnessX = 257.67246f, witnessZ = 384.83424f;
        var sampler = typeof(YQPreparedSpatialMaterializationV2).GetField("terrainSampler", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(prepared);
        var footprint = sampler.GetType().GetField("channelFootprint", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sampler);
        object[] measurement = { witnessX, witnessZ, -1, float.PositiveInfinity };
        footprint.GetType().GetMethod("TryMeasure", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(footprint, measurement);
        probes.Add(new { witnessX, witnessZ, sample = prepared.SampleTerrain(witnessX, witnessZ), channel = measurement[2], distance = measurement[3] });
        for (int joinIndex = 0; joinIndex < prepared.NativeJoinCount; joinIndex++)
        {
            var join = prepared.GetNativeJoin(joinIndex);
            if (join.Contains(witnessX, witnessZ)) probes.Add(new { joinIndex, wet = join.TrySample(witnessX, witnessZ, out int owner, out float surface), owner, surface });
        }
        var checks = new List<object>(); int failed = 0;
        void Check(string name, Action action)
        {
            try { action(); checks.Add(new { name, passed = true }); }
            catch (Exception exception) { failed++; checks.Add(new { name, passed = false, failure = (exception.InnerException ?? exception).Message }); }
        }
        foreach (int chunkZ in new[] { 7, 8 })
        {
            Bounds cell = new Bounds(new Vector3(320f, 0f, -512f + chunkZ * 128f + 64f), new Vector3(128f, 10000f, 128f));
            Vector3 origin = chunkZ == 7 ? new Vector3(-512f, 0f, -512f) : new Vector3(256f, 0f, 512f);
            Vector3 size = chunkZ == 7 ? new Vector3(1024f, 140f, 1024f) : new Vector3(128f, 140f, 128f);
            Bounds domain = Domain(cell, origin, size);
            for (int corner = 0; corner < 4; corner++)
            {
                float x = (corner & 1) == 0 ? domain.min.x : domain.max.x;
                float z = (corner & 2) == 0 ? domain.min.z : domain.max.z;
                var sample = prepared.SampleTerrain(x, z);
                probes.Add(new { chunkZ, x, z, sample.waterMask, sample.waterFeatureIndex, sample.siteReserveMask, sample.routeMask });
            }
            probes.Add(new { chunkZ, excluded = Aquatic(domain, prepared) });
            Check("accepted habitat classification 6," + chunkZ, () => Require(Aquatic(domain, prepared) == (chunkZ == 8), "Dry remnant/aquatic classification is incorrect."));
            if (chunkZ == 8)
                Check("production aquatic readiness callback 6,8", () => VerifyAquaticProvider(plan, chunkZ, origin, size));
            else Check("dry remnant candidate retains vegetation admission", () =>
            {
                var query = typeof(YQPreparedSpatialMaterializationV2).GetMethod("TryFindVegetationDryCandidate", BindingFlags.Instance | BindingFlags.NonPublic);
                object[] arguments = { domain, default(Vector2) };
                Require((bool)query.Invoke(prepared, arguments), "No canonical dry candidate was found.");
                Vector2 candidate = (Vector2)arguments[1]; var sample = prepared.SampleTerrain(candidate.x, candidate.y);
                var admission = typeof(YQGeneratedWorldEnvironment).GetMethod("IsPreparedVegetationPositionAllowed", BindingFlags.Static | BindingFlags.NonPublic);
                Require((bool)admission.Invoke(null, new object[] { sample, false }) &&
                    (bool)admission.Invoke(null, new object[] { sample, true }), "The candidate bypasses existing canopy or understory exclusions.");
                probes.Add(new { candidateX = candidate.x, candidateZ = candidate.y, sample, evidence = "CANDIDATE_ADMISSION_NOT_PHYSICAL_PLANT_PUBLICATION" });
            });
        }
        Check("dry neighbouring shore retains vegetation", () => Require(!Aquatic(
            new Bounds(new Vector3(192f, 0f, 384f), new Vector3(128f, 10000f, 128f)), prepared), "Dry shore was waived."));
        Check("native lake remnant requires its channel union", () =>
        {
            // note: The lake alone cannot waive its dry shore remnant.
            var source = plan.spatialPlanV2.blueprint.hydrology[0].connectedDomain;
            Require(YQPreparedConnectedBasinV2.TryPrepare(source, plan.spatialPlanV2.blueprint.hydrology[0].waterLevelNormalized,
                out var basin, out reason), reason);
            var query = typeof(YQPreparedConnectedBasinV2).GetMethod("CoversVegetationRectangle", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(!(bool)query.Invoke(basin, new object[] {
                new Bounds(new Vector3(320f, 0f, 446.5f), new Vector3(128f, 10000f, 125f)), null }),
                "The dry lake remnant was waived without its accepted channel.");
        });
        Check("four wet corners cannot hide a dry interior island", () =>
        {
            Require(YQPreparedConnectedBasinV2.TryCreate(0f, 0f, 1f, 9, 2f, 2f, .5f,
                (x, z) => x == 0f || z == 0f || x == 8f || z == 8f || x == 4f && z == 4f ? .8f : .1f,
                out var source, out reason), reason);
            Require(YQPreparedConnectedBasinV2.TryPrepare(source, .5f, out var basin, out reason), reason);
            var query = typeof(YQPreparedConnectedBasinV2).GetMethod("CoversVegetationRectangle", BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (Vector2 corner in new[] { new Vector2(2f, 2f), new Vector2(6f, 2f), new Vector2(2f, 6f), new Vector2(6f, 6f) })
                Require(basin.TrySampleBed(corner.x, corner.y, out _, out bool wet) && wet, "The island fixture needs four wet corners.");
            Require(!(bool)query.Invoke(basin, new object[] { new Bounds(new Vector3(4f, 0f, 4f), new Vector3(4f, 100f, 4f)), null }), "Interior island was waived.");
            Require((bool)query.Invoke(basin, new object[] { new Bounds(new Vector3(2f, 0f, 2f), new Vector3(1f, 100f, 1f)), null }), "A fully wet subrectangle was rejected.");
        });
        Check("accepted content hash preserved", () => Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2) == "8abef624410f2f54", "Accepted content changed."));
        File.WriteAllText(output + "/Ecology_Replay_After.json", JsonConvert.SerializeObject(new { probes, checks, failed,
            evidence = "UNITY_NATIVE_PROVIDER_FIXTURE_NOT_ORDINARY_STARTUP", utc = DateTime.UtcNow,
            runtimeMvid = typeof(YQGeneratedWorldEnvironment).Module.ModuleVersionId }, Formatting.Indented));
        Debug.Log("[YQWorldLoadEcologyReplay] Checks=" + checks.Count + " failed=" + failed);
    }

    private static void VerifyAquaticProvider(GeneratedWorldPlanRecord plan, int chunkZ, Vector3 origin, Vector3 size)
    {
        // note: A disposable preview exercises the real readiness callback without imported assets, scene changes, or profile writes.
        Scene preview = EditorSceneManager.NewPreviewScene(); TerrainData data = null;
        YQRuntimeWorldAssetRegistry registry = null; IEnumerator work = null;
        try
        {
            var root = new GameObject("WorldLoadEcologyFixture"); SceneManager.MoveGameObjectToScene(root, preview);
            data = new TerrainData { heightmapResolution = 33, size = size };
            var terrainObject = Terrain.CreateTerrainGameObject(data); SceneManager.MoveGameObjectToScene(terrainObject, preview);
            terrainObject.transform.SetParent(root.transform, false); terrainObject.transform.position = origin;
            registry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
            int required = int.MinValue; string rejection = null;
            work = YQGeneratedWorldEnvironment.BuildSemanticChunkScatterRoutine(root.transform, terrainObject.GetComponent<Terrain>(), plan,
                new GeneratedSemanticChunkRecord { chunkX = 6, chunkZ = chunkZ, chunkId = "110b8224:chunk:6:" + chunkZ },
                plan.regions[0], new GeneratedRegionAssetPaletteRecord(), registry, 128f, null,
                result => required = result, failure => rejection = failure);
            for (int step = 0; step < 16 && required == int.MinValue && work.MoveNext(); step++) { }
            Require(required > 0 && rejection == null, "Required ecology callback failed: " + required + " " + rejection);
        }
        finally
        {
            (work as IDisposable)?.Dispose(); EditorSceneManager.ClosePreviewScene(preview);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (registry != null) UnityEngine.Object.DestroyImmediate(registry);
        }
    }
}
#endif
