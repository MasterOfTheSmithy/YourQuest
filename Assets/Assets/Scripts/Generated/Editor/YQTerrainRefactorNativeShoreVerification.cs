using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// note: Measure production heights through native TerrainData; the separate interpolation probe owns only a disposable preview fixture. Neither changes authored scenes, assets or profiles.
public static class YQTerrainRefactorNativeShoreVerification
{
    private const string EvidenceRoot = "outputs/TerrainRefactor_20261011";
    private static IEnumerator job;
    private static TerrainData ownedData;
    private static GameObject ownedTerrain;
    private static UnityEngine.SceneManagement.Scene ownedScene;
    private static string output;
    private static string input;
    private static string inputHash;
    private static string planHash;
    private static Dictionary<string, string> sourceHashes;
    private static readonly List<object> measurements = new List<object>();
    private static readonly List<object> joinedMeasurements = new List<object>();
    private static int failures;
    private static bool productionRepairSequence;
    private static readonly List<object> gradingStages = new List<object>();
    private static bool compilerNullContinuationReproduced;

    [MenuItem("YourQuest/Terrain Refactor/Measure Native Height Interpolation (Disposable)")]
    private static void MeasureInterpolation()
    {
        if (job != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Native interpolation measurements require idle Edit Mode.");
        // note: A non-coplanar four-corner fixture distinguishes bilinear interpolation and both possible diagonals; direct collider rays also measure the physical surface, independently of the height query API.
        string destination = Path.Combine(EvidenceRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_native_interpolation");
        Directory.CreateDirectory(destination);
        var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        GameObject instance = null;
        var points = new List<object>();
        try
        {
            const int resolution = 33;
            const float size = 64f, height = 140f;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++) heights[z, x] = .2f;
            heights[16, 16] = .1f; heights[16, 17] = .4f;
            heights[17, 16] = .7f; heights[17, 17] = .2f;
            data = new TerrainData { name = "DISPOSABLE_InterpolationProbe", heightmapResolution = resolution,
                size = new Vector3(size, height, size), hideFlags = HideFlags.HideAndDontSave };
            data.SetHeights(0, 0, heights);
            instance = new GameObject("DISPOSABLE_InterpolationCollider") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(instance, preview);
            var collider = instance.AddComponent<TerrainCollider>(); collider.terrainData = data;
            Physics.SyncTransforms();
            foreach (Vector2 fraction in new[] { new Vector2(.25f, .25f), new Vector2(.75f, .25f),
                new Vector2(.25f, .75f), new Vector2(.75f, .75f), new Vector2(.5f, .5f), new Vector2(.2f, .4f) })
            {
                float x = fraction.x, z = fraction.y, worldX = 32f + x * 2f, worldZ = 32f + z * 2f;
                float interpolated = data.GetInterpolatedHeight(worldX / size, worldZ / size);
                float bilinear = Mathf.Lerp(Mathf.Lerp(.1f, .4f, x), Mathf.Lerp(.7f, .2f, x), z) * height;
                float negativeDiagonal = (x + z <= 1f ? .1f * (1f - x - z) + .7f * z + .4f * x
                    : .4f * (1f - z) + .7f * (1f - x) + .2f * (x + z - 1f)) * height;
                float positiveDiagonal = (x >= z ? .1f * (1f - x) + .4f * (x - z) + .2f * z
                    : .1f * (1f - z) + .7f * (z - x) + .2f * x) * height;
                bool hit = collider.Raycast(new Ray(new Vector3(worldX, height + 10f, worldZ), Vector3.down), out var rayHit, height + 20f);
                points.Add(new { x, z, interpolated, colliderHit = hit, colliderHeight = hit ? rayHit.point.y : (float?)null,
                    bilinear, negativeDiagonal, positiveDiagonal });
            }
        }
        finally
        {
            // note: Preview-scene ownership keeps this fixture out of authored scenes and accepted profiles, even when a native query throws.
            if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview);
        }
        File.WriteAllText(Path.Combine(destination, "Receipt.json"), JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), points,
            verifierSha256 = Hash("Assets/Assets/Scripts/Generated/Editor/YQTerrainRefactorNativeShoreVerification.cs"),
            runtimeAssemblyMvid = typeof(YQGeneratedWorldTerrain).Assembly.ManifestModule.ModuleVersionId,
            evidence = "UNITY_EDITOR_NATIVE_SYNTHETIC_NONCOPLANAR_TERRAIN_AND_COLLIDER",
            limits = "Synthetic interpolation identification only; no generated basin, final terrain, scene/profile, visual, streaming or traversal acceptance."
        }, Formatting.Indented));
        Debug.Log("[YQNativeInterpolation] Measured; " + destination);
    }

    [MenuItem("YourQuest/Terrain Refactor/Measure Native Basin Shore (Disposable)")]
    private static void Begin() => Begin(false);

    [MenuItem("YourQuest/Terrain Refactor/Measure Graded Basin Shore (Disposable)")]
    private static void BeginGraded() => Begin(true);

    private static void Begin(bool includeProductionRepairs)
    {
        if (job != null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Native shore measurements require idle Edit Mode.");
        // note: Only consume a passing, source-bound ephemeral draft; never load or regenerate an accepted live profile.
        input = Directory.GetDirectories(EvidenceRoot, "*_unity_basins")
            .OrderByDescending(path => path, StringComparer.Ordinal)
            .Select(path => Path.Combine(path, "new-draft.json"))
            .FirstOrDefault(path => File.Exists(path) && File.Exists(Path.Combine(Path.GetDirectoryName(path), "Receipt.json")));
        if (input == null) throw new InvalidOperationException("Run read-only basin contracts first.");
        var receipt = JsonConvert.DeserializeObject<Binding>(File.ReadAllText(Path.Combine(Path.GetDirectoryName(input), "Receipt.json")));
        if (receipt == null || !receipt.passed || receipt.sourceSha256 == null)
            throw new InvalidOperationException("The draft has no passing source-bound Unity receipt.");
        foreach (var source in receipt.sourceSha256)
            if (Hash(source.Key) != source.Value) throw new InvalidOperationException("Stale basin source: " + source.Key);
        if (receipt.runtimeAssemblySha256 != Hash(typeof(YQGeneratedWorldTerrain).Assembly.Location))
            throw new InvalidOperationException("The loaded runtime no longer matches the passing basin receipt.");
        var plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText(input));
        if (!YQSpatialPlanVersionRouter.TryValidateAcceptedV2(plan, out string reason)) throw new InvalidOperationException(reason);
        compilerNullContinuationReproduced = false;
        if (includeProductionRepairs)
        {
            // note: Unity serializes a compiler-null inline continuation as a default empty staged object. Reproduce the compiler result and exact parent hash before using that new disposable artifact; never discard accepted frontier content.
            var inline = plan.spatialPlanV2.acceptedContinuation;
            if (inline != null && (inline.state != YQSpatialContinuationStateV2.Staged || inline.revision != 0 ||
                !string.IsNullOrEmpty(inline.worldSeed) || !string.IsNullOrEmpty(inline.parentSpatialContentHash) ||
                !string.IsNullOrEmpty(inline.parentMemberFootprintHash) || !string.IsNullOrEmpty(inline.contentHash) ||
                !string.IsNullOrEmpty(inline.validatedContentHash) || inline.locations == null || inline.locations.Count != 0 ||
                inline.validationErrors == null || inline.validationErrors.Count != 0))
                throw new InvalidOperationException("The disposable grading fixture cannot replace nonempty continuation records.");
            string expectedParentHash = plan.spatialPlanV2.contentHash;
            if (!YQSpatialBlueprintCompilerV2.TryCompile(plan, out var reproduced, out reason) ||
                reproduced.contentHash != expectedParentHash || reproduced.acceptedContinuation != null)
                throw new InvalidOperationException("The compiler-null continuation and identical parent geometry were not reproduced: " + reason);
            plan.spatialPlanV2 = reproduced;
            if (!YQSpatialContinuationValidatorV2.ValidateBasic(reproduced).IsStructurallyValid)
                throw new InvalidOperationException("The reproduced draft failed continuation validation.");
            compilerNullContinuationReproduced = true;
        }
        if (!YQGeneratedWorldTerrain.TryCreateV2HeightSampler(plan, out var sampler, out reason)) throw new InvalidOperationException(reason);
        if (!YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var spatial, out reason)) throw new InvalidOperationException(reason);
        inputHash = Hash(input);
        planHash = plan.spatialPlanV2.contentHash;
        sourceHashes = new Dictionary<string, string>(receipt.sourceSha256);
        string verifier = "Assets/Assets/Scripts/Generated/Editor/YQTerrainRefactorNativeShoreVerification.cs";
        sourceHashes.Add(verifier, Hash(verifier));
        if (includeProductionRepairs)
        {
            // note: Bind the construction-order reference as well as the actual water/road implementation already recorded by the basin verifier.
            string builder = "Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs";
            sourceHashes.Add(builder, Hash(builder));
        }
        productionRepairSequence = includeProductionRepairs;
        output = Path.Combine(EvidenceRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + (productionRepairSequence ? "_graded_native_shore" : "_native_shore"));
        Directory.CreateDirectory(output);
        // note: Own the native collider probe in a disposable preview scene; authored scenes and accepted profiles are never selected or changed.
        ownedScene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        measurements.Clear(); joinedMeasurements.Clear(); gradingStages.Clear(); failures = 0;
        job = Measure(plan, sampler, spatial);
        EditorApplication.update += Step;
        AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        EditorApplication.quitting += Cancel;
    }

    private static IEnumerator Measure(GeneratedWorldPlanRecord plan, YQGeneratedWorldTerrain.V2HeightSampler sampler,
        YQSpatialBlueprintTerrainSamplerV2 spatial)
    {
        for (int waterIndex = 0; waterIndex < plan.spatialPlanV2.blueprint.hydrology.Count; waterIndex++)
        {
            var water = plan.spatialPlanV2.blueprint.hydrology[waterIndex];
            if (!water.hasConnectedDomain) continue;
            if (!YQPreparedConnectedBasinV2.TryPrepare(water.connectedDomain, water.waterLevelNormalized, out var basin, out string reason))
                throw new InvalidOperationException(reason);
            // note: The two-metre grid is aligned to the production origin grid, while the accepted geological patch can have a fractional offset.
            const int resolution = YQGeneratedWorldTerrain.HeightmapResolution;
            const float size = YQGeneratedWorldTerrain.WorldSize;
            float step = size / (resolution - 1f);
            float minimumX = Mathf.Floor(basin.MinimumX / step) * step - step;
            float minimumZ = Mathf.Floor(basin.MinimumZ / step) * step - step;
            if (basin.MaximumX >= minimumX + size || basin.MaximumZ >= minimumZ + size)
                throw new InvalidOperationException("The bounded diagnostic heightfield cannot contain this basin.");
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                    heights[z, x] = sampler.SampleNormalized(minimumX + x * step, minimumZ + z * step);
                if (z % 4 == 0) yield return null;
            }
            ownedData = new TerrainData { name = "DISPOSABLE_NativeBasinShore", heightmapResolution = resolution,
                size = new Vector3(size, YQGeneratedWorldTerrain.TerrainHeight, size), hideFlags = HideFlags.HideAndDontSave };
            ownedData.SetHeights(0, 0, heights);
            // note: Query Unity's actual heightfield collider, independently of TerrainData interpolation, at the same published shoreline witnesses.
            ownedTerrain = new GameObject("DISPOSABLE_NativeBasinCollider") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ownedTerrain, ownedScene);
            ownedTerrain.transform.position = new Vector3(minimumX, 0f, minimumZ);
            ownedTerrain.AddComponent<Terrain>().terrainData = ownedData;
            var collider = ownedTerrain.AddComponent<TerrainCollider>();
            collider.terrainData = ownedData;
            Physics.SyncTransforms();
            if (productionRepairSequence)
            {
                // note: Exercise the actual production water/road routines in construction order, retaining each stage's shoreline error. This fixture has no site foundations or their protected reservations.
                var publishedBasin = spatial.GetMaterializedBasin(waterIndex);
                RecordGradingStage("source", water, publishedBasin, minimumX, minimumZ, size);
                var reservations = new List<YQGeneratedWorldEnvironment.LivedPathTerrainReservation>();
                var repairs = new[] {
                    CreateRiverBedReconciliationFixture(ownedTerrain.GetComponent<Terrain>(), plan),
                    YQGeneratedWorldEnvironment.RepairWaterBanksRoutine(ownedTerrain.GetComponent<Terrain>(), plan, reservations),
                    YQGeneratedWorldEnvironment.RepairLivedPathTerrainRoutine(ownedTerrain.GetComponent<Terrain>(), plan, reservations),
                    YQGeneratedWorldEnvironment.RepairAcceptedWaterChannelsRoutine(ownedTerrain.GetComponent<Terrain>(), plan, reservations),
                    YQGeneratedWorldEnvironment.RepairLivedPathTerrainRoutine(ownedTerrain.GetComponent<Terrain>(), plan, reservations)
                };
                var names = new[] { "river-bed-reconciliation", "bank-repair", "road-repair", "channel-repair", "final-road-repair" };
                for (int stage = 0; stage < repairs.Length; stage++)
                {
                    var routine = FlattenRepairRoutine(repairs[stage]);
                    try { while (routine.MoveNext()) yield return null; }
                    finally { (routine as IDisposable)?.Dispose(); }
                    RecordGradingStage(names[stage], water, publishedBasin, minimumX, minimumZ, size);
                }
                ownedData.SyncHeightmap();
                Physics.SyncTransforms();
            }
            float maximumShoreErrorMetres = 0f;
            float maximumInterpolationErrorMetres = 0f;
            float maximumExposedShoreErrorMetres = 0f;
            float maximumCoveredChannelErrorMetres = 0f;
            float maximumNativeTriangleErrorMetres = 0f;
            int samples = 0, channelJoinSamples = 0;
            int exposedShoreSamples = 0, coveredChannelSamples = 0;
            var points = new List<object>();
            for (int edge = 0; edge < basin.ShoreSegmentCount; edge++)
            {
                basin.GetShoreSegment(edge, out Vector2 first, out Vector2 second);
                for (int fraction = 0; fraction <= 4; fraction++)
                {
                    Vector2 point = Vector2.Lerp(first, second, fraction * .25f);
                    var canonical = spatial.Sample(point.x, point.y);
                    // note: Keep the former semantic classification for comparison, but never skip these points: lake mask ownership can hide a channel, and a padded wet mask cannot establish actual ribbon coverage.
                    if (canonical.waterMask >= .999f && canonical.waterFeatureIndex >= 0 && canonical.waterFeatureIndex != waterIndex &&
                        (plan.spatialPlanV2.blueprint.hydrology[canonical.waterFeatureIndex].kind == YQHydrologyKindV2.River ||
                         plan.spatialPlanV2.blueprint.hydrology[canonical.waterFeatureIndex].kind == YQHydrologyKindV2.Waterfall))
                    { channelJoinSamples++; }
                    float actual = ownedData.GetInterpolatedHeight((point.x - minimumX) / size, (point.y - minimumZ) / size);
                    float expected = water.waterLevelNormalized * YQGeneratedWorldTerrain.TerrainHeight;
                    float error = Mathf.Abs(actual - expected);
                    float canonicalHeight = sampler.SampleNormalized(point.x, point.y) * YQGeneratedWorldTerrain.TerrainHeight;
                    float interpolationError = Mathf.Abs(actual - canonicalHeight);
                    // note: A geological contour is not the versioned published shoreline. Independently interpolate its original native source triangle, retaining the old plane and sampler discrepancies below.
                    float expectedNativeHeight = InterpolateNativeSourceTriangle(heights,
                        (point.x - minimumX) / step, (point.y - minimumZ) / step) * YQGeneratedWorldTerrain.TerrainHeight;
                    float nativeTriangleError = Mathf.Abs(actual - expectedNativeHeight);
                    maximumNativeTriangleErrorMetres = Mathf.Max(maximumNativeTriangleErrorMetres, nativeTriangleError);
                    bool channelCoversContour = spatial.TrySampleTerrainSourceChannelSurface(point.x, point.y,
                        water.waterLevelNormalized, out int coveringChannelIndex, out float coveringChannelLevel) &&
                        Mathf.Abs(coveringChannelLevel - water.waterLevelNormalized) * YQGeneratedWorldTerrain.TerrainHeight <= .01f;
                    if (channelCoversContour)
                    {
                        coveredChannelSamples++;
                        maximumCoveredChannelErrorMetres = Mathf.Max(maximumCoveredChannelErrorMetres, error);
                    }
                    else
                    {
                        exposedShoreSamples++;
                        maximumExposedShoreErrorMetres = Mathf.Max(maximumExposedShoreErrorMetres, error);
                    }
                    var cellSamples = new List<object>();
                    if (error > .01f || interpolationError > .01f)
                    {
                        // note: Preserve the actual four source heights for failed native cells, alongside the shared bed and spatial sampler; this separates projection changes from diagonal or native quantization errors.
                        int cellX = Mathf.FloorToInt((point.x - minimumX) / step), cellZ = Mathf.FloorToInt((point.y - minimumZ) / step);
                        for (int dz = 0; dz <= 1; dz++)
                        for (int dx = 0; dx <= 1; dx++)
                        {
                            float x = minimumX + (cellX + dx) * step, z = minimumZ + (cellZ + dz) * step;
                            var projection = spatial.Sample(x, z);
                            basin.TrySampleBed(x, z, out float acceptedBed, out bool wet);
                            cellSamples.Add(new { x, z, sourceHeight = heights[cellZ + dz, cellX + dx] * YQGeneratedWorldTerrain.TerrainHeight,
                                spatialHeight = projection.elevationNormalized * YQGeneratedWorldTerrain.TerrainHeight,
                                acceptedBed = acceptedBed * YQGeneratedWorldTerrain.TerrainHeight, wet,
                                projection.waterMask, projection.waterFeatureIndex, projection.connectedBasinSupportMask });
                        }
                    }
                    maximumShoreErrorMetres = Mathf.Max(maximumShoreErrorMetres, error);
                    maximumInterpolationErrorMetres = Mathf.Max(maximumInterpolationErrorMetres, interpolationError);
                    samples++;
                    // note: Keep the level-plane failure and the sampler/native discrepancy independently visible; channel projection must not be mistaken for heightfield interpolation error.
                    points.Add(new { x = point.x, z = point.y, actual, expected, error, canonicalHeight, interpolationError, expectedNativeHeight, nativeTriangleError,
                        channelCoversContour, coveringChannelIndex, coveringChannelLevel,
                        canonical.waterMask, canonical.waterFeatureIndex, canonical.connectedBasinSupportMask, cellSamples });
                }
                if (edge % 16 == 0) yield return null;
            }
            bool nominalPlanePassed = samples > 0 && maximumShoreErrorMetres <= .01f;
            // note: Versions 6+ explicitly publish a projected native lake after final channel cuts. Validate every original witness against that triangle contract; the complete projected shore/portal contact gate remains independently required below.
            bool projectedContactAuthority = basin.Version >= YQPreparedConnectedBasinV2.JoinedNativeLakeVersion;
            bool passed = projectedContactAuthority ? samples > 0 && maximumNativeTriangleErrorMetres <= .01f : nominalPlanePassed;
            if (!passed) failures++;
            measurements.Add(new { hydrologyId = water.hydrologyId, passed, nominalPlanePassed, projectedContactAuthority,
                contract = projectedContactAuthority ? "GEOLOGICAL_WITNESSES_MATCH_NATIVE_SOURCE_TRIANGLES; PUBLISHED_CONTACT_REQUIRED_SEPARATELY" : "ARCHIVED_GEOLOGICAL_CONTOUR_CONTACT",
                maximumNativeTriangleErrorMetres, samples, channelJoinSamples,
                exposedShoreSamples, coveredChannelSamples, maximumExposedShoreErrorMetres, maximumCoveredChannelErrorMetres,
                maximumShoreErrorMetres, maximumInterpolationErrorMetres, resolution, stepMetres = step, minimumX, minimumZ });
            File.WriteAllText(Path.Combine(output, "water-" + waterIndex + "-samples.json"), JsonConvert.SerializeObject(points, Formatting.Indented));
            // note: Retain every former nominal witness above. Measure the versioned native join separately, including every ownership portal rather than relabeling a submerged edge as a valid shore.
            var joined = spatial.GetMaterializedBasin(waterIndex);
            if (projectedContactAuthority && (joined == null || joined.Version < YQPreparedConnectedBasinV2.JoinedNativeLakeVersion))
                throw new InvalidOperationException("The current geological witness contract requires a published native shoreline.");
            if (joined != null && joined.Version >= YQPreparedConnectedBasinV2.JoinedNativeLakeVersion)
            {
                var joinedPoints = new List<object>();
                float maximumContactError = 0f;
                int invalidPortalSamples = 0, contactSamples = 0, portalSamples = 0;
                float maximumColliderInterpolationError = 0f, maximumColliderContactError = 0f;
                int colliderProbes = 0, missedColliderSamples = 0;
                for (int edge = 0; edge < joined.ShoreSegmentCount + joined.PortalSegmentCount; edge++)
                {
                    bool portal = edge >= joined.ShoreSegmentCount;
                    Vector2 first, second;
                    if (portal) joined.GetPortalSegment(edge - joined.ShoreSegmentCount, out first, out second);
                    else joined.GetShoreSegment(edge, out first, out second);
                    for (int fraction = 0; fraction <= 4; fraction++)
                    {
                        Vector2 point = Vector2.Lerp(first, second, fraction * .25f);
                        float actual = ownedData.GetInterpolatedHeight((point.x - minimumX) / size, (point.y - minimumZ) / size);
                        float expected = water.waterLevelNormalized * YQGeneratedWorldTerrain.TerrainHeight;
                        float contactError = Mathf.Abs(actual - expected);
                        bool covered = spatial.TrySampleAcceptedChannelSurface(point.x, point.y, water.waterLevelNormalized,
                            out int channelIndex, out float channelLevel) &&
                            Mathf.Abs(channelLevel - water.waterLevelNormalized) * YQGeneratedWorldTerrain.TerrainHeight <= .01f;
                        // note: Keep missing collision and collider/interpolation disagreement separate from a legitimately submerged channel portal.
                        bool colliderHit = collider.Raycast(new Ray(new Vector3(point.x, YQGeneratedWorldTerrain.TerrainHeight + 10f, point.y), Vector3.down),
                            out RaycastHit hit, YQGeneratedWorldTerrain.TerrainHeight + 20f);
                        colliderProbes++;
                        float colliderInterpolationError = colliderHit ? Mathf.Abs(hit.point.y - actual) : float.NaN;
                        float colliderContactError = colliderHit ? Mathf.Abs(hit.point.y - expected) : float.NaN;
                        if (!colliderHit) missedColliderSamples++;
                        else
                        {
                            maximumColliderInterpolationError = Mathf.Max(maximumColliderInterpolationError, colliderInterpolationError);
                            if (!portal && !covered) maximumColliderContactError = Mathf.Max(maximumColliderContactError, colliderContactError);
                        }
                        if (portal) { portalSamples++; if (!covered) invalidPortalSamples++; }
                        else { contactSamples++; maximumContactError = Mathf.Max(maximumContactError, contactError); }
                        joinedPoints.Add(new { x = point.x, z = point.y, portal, actual, expected, contactError, covered, channelIndex, channelLevel,
                            colliderHit, colliderHeight = colliderHit ? hit.point.y : float.NaN, colliderInterpolationError, colliderContactError });
                    }
                    if (edge % 16 == 0) yield return null;
                }
                bool colliderPassed = colliderProbes > 0 && missedColliderSamples == 0 && maximumColliderInterpolationError <= .01f && maximumColliderContactError <= .01f;
                bool joinedPassed = contactSamples > 0 && maximumContactError <= .01f && invalidPortalSamples == 0 && colliderPassed;
                // note: A failed published contact/collision check must remain an acceptance failure even if the nominal contour happens to pass.
                if (!joinedPassed) failures++;
                joinedMeasurements.Add(new { hydrologyId = water.hydrologyId, passed = joinedPassed, contactSamples, portalSamples,
                    maximumContactErrorMetres = maximumContactError, invalidPortalSamples, colliderPassed, colliderProbes, missedColliderSamples,
                    maximumColliderInterpolationErrorMetres = maximumColliderInterpolationError, maximumColliderContactErrorMetres = maximumColliderContactError });
                File.WriteAllText(Path.Combine(output, "water-" + waterIndex + "-joined-samples.json"), JsonConvert.SerializeObject(joinedPoints, Formatting.Indented));
            }
            UnityEngine.Object.DestroyImmediate(ownedTerrain); ownedTerrain = null;
            UnityEngine.Object.DestroyImmediate(ownedData); ownedData = null;
        }
    }

    private static void Step()
    {
        // note: Cooperative Editor slices keep cancellation and cleanup owned by this explicitly invoked diagnostic, without a background native Unity call.
        try { if (job != null && !job.MoveNext()) Finish(null); }
        catch (Exception exception) { Finish(exception); }
    }

    private static void Cancel() => Finish(new OperationCanceledException("Native shore measurement interrupted."));

    private static void Finish(Exception error)
    {
        if (job == null) return;
        (job as IDisposable)?.Dispose(); job = null;
        EditorApplication.update -= Step;
        AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
        EditorApplication.quitting -= Cancel;
        if (ownedTerrain != null) { UnityEngine.Object.DestroyImmediate(ownedTerrain); ownedTerrain = null; }
        if (ownedData != null) { UnityEngine.Object.DestroyImmediate(ownedData); ownedData = null; }
        if (ownedScene.IsValid()) { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(ownedScene); ownedScene = default; }
        bool unchanged = inputHash == Hash(input);
        foreach (var source in sourceHashes) unchanged &= Hash(source.Key) == source.Value;
        bool passed = error == null && failures == 0 && measurements.Count > 0 && unchanged;
        File.WriteAllText(Path.Combine(output, "Receipt.json"), JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), passed, failures, error = error?.ToString(), measurements, joinedMeasurements,
            productionRepairSequence, compilerNullContinuationReproduced, gradingStages,
            input, inputSha256 = inputHash, planHash, inputsUnchanged = unchanged, sourceSha256 = sourceHashes,
            runtimeAssemblyMvid = typeof(YQGeneratedWorldTerrain).Assembly.ManifestModule.ModuleVersionId,
            runtimeAssemblySha256 = Hash(typeof(YQGeneratedWorldTerrain).Assembly.Location),
            evidence = "UNITY_EDITOR_NATIVE_TERRAIN_DATA_PRODUCTION_HEIGHT_SAMPLER_PROJECTION",
            limits = productionRepairSequence
                ? "Ephemeral draft on aligned disposable native heightfields/colliders after actual production river-bed, bank, road, channel and final-road repair routines. No site foundations/protected reservations, ordinary full construction prepass, continuous-cell publication, combined channel wet union, waves, mixed LOD, live profiles or traversal proof. Nominal contour witnesses remain separate."
                : "Ephemeral accepted draft on aligned disposable source heightfields and native colliders. All nominal witnesses retained; joined lake contact, collider/interpolation agreement and bounded channel-ownership portals measured separately. No final site/route grading, combined channel wet union, waves, mixed LOD, profile, streaming or traversal proof."
        }, Formatting.Indented));
        Debug.Log("[YQNativeBasinShore] " + (passed ? "PASS" : "FAIL") + "; " + output);
    }

    private static IEnumerator CreateRiverBedReconciliationFixture(Terrain terrain, GeneratedWorldPlanRecord plan)
    {
        // note: The production reconciliation entry point is runtime-internal. Reflection stays in this Editor diagnostic instead of widening its gameplay API.
        var method = typeof(YQGeneratedWorldEnvironment).GetMethod("ReconcileRuntimeRiverBedsRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        if (method == null) throw new InvalidOperationException("Production river-bed reconciliation is unavailable.");
        return (IEnumerator)method.Invoke(null, new object[] { terrain, plan });
    }

    private static float InterpolateNativeSourceTriangle(float[,] heights, float x, float z)
    {
        // note: Independent piecewise-linear reference for Unity's bottom-left/top-right native diagonal. Never use TerrainData interpolation or the prepared shoreline as this reference.
        int ix = Mathf.Clamp(Mathf.FloorToInt(x), 0, heights.GetLength(1) - 2);
        int iz = Mathf.Clamp(Mathf.FloorToInt(z), 0, heights.GetLength(0) - 2);
        float u = Mathf.Clamp01(x - ix), v = Mathf.Clamp01(z - iz);
        float a = heights[iz, ix], b = heights[iz, ix + 1], c = heights[iz + 1, ix], d = heights[iz + 1, ix + 1];
        return u >= v ? a + (b - a) * u + (d - b) * v : a + (d - c) * u + (c - a) * v;
    }

    private static IEnumerator FlattenRepairRoutine(IEnumerator initial)
    {
        // note: Editor update has no Unity coroutine scheduler. Execute nested production iterators and preserve their null handoffs without replacing terrain equations or accepting unsupported yield instructions.
        var pending = new Stack<IEnumerator>(); pending.Push(initial);
        try
        {
            while (pending.Count > 0)
            {
                var current = pending.Peek();
                if (!current.MoveNext()) { pending.Pop(); (current as IDisposable)?.Dispose(); continue; }
                if (current.Current is IEnumerator nested) { pending.Push(nested); continue; }
                if (current.Current != null) throw new InvalidOperationException("Unsupported terrain repair yield in the disposable Editor fixture.");
                yield return null;
            }
        }
        finally { while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose(); }
    }

    private static void RecordGradingStage(string stage, YQHydrologyFeatureV2 water, YQPreparedConnectedBasinV2 basin,
        float minimumX, float minimumZ, float size)
    {
        // note: Stage probes read the published contact contour against actual delayed height edits; final native collision is measured independently after synchronization.
        float maximumError = 0f; int samples = 0;
        if (basin != null)
            for (int edge = 0; edge < basin.ShoreSegmentCount; edge++)
            {
                basin.GetShoreSegment(edge, out Vector2 first, out Vector2 second);
                for (int fraction = 0; fraction <= 2; fraction++)
                {
                    Vector2 point = Vector2.Lerp(first, second, fraction * .5f);
                    float actual = ownedData.GetInterpolatedHeight((point.x - minimumX) / size, (point.y - minimumZ) / size);
                    maximumError = Mathf.Max(maximumError, Mathf.Abs(actual - water.waterLevelNormalized * YQGeneratedWorldTerrain.TerrainHeight)); samples++;
                }
            }
        gradingStages.Add(new { hydrologyId = water.hydrologyId, stage, samples, maximumContactErrorMetres = maximumError });
    }

    private static string Hash(string path)
    {
        using (var stream = File.OpenRead(path))
        using (var hash = SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }

    private sealed class Binding
    {
        public bool passed;
        public Dictionary<string, string> sourceSha256;
        public string runtimeAssemblySha256;
    }
}
