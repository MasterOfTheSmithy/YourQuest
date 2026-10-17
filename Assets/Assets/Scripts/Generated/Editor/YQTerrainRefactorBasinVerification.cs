using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class YQTerrainRefactorBasinVerification
{
    [MenuItem("YourQuest/Terrain Refactor/Trace Native Reach Preparation (Read Only)")]
    private static void TraceNativeReachPreparation()
    {
        // note: Diagnose one archived disposable candidate without repeating mesh checks or touching any live profile or scene.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before read-only reach preparation.");
        const string input = "outputs/TerrainRefactor_20261011/20261011_170756_642_unity_basins/new-draft.json";
        string output = Path.Combine("outputs/TerrainRefactor_20261011", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_reach_preparation");
        Directory.CreateDirectory(output);
        string before = Sha256(input);
        var plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText(input));
        var timer = System.Diagnostics.Stopwatch.StartNew();
        bool passed = YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var sampler, out string reason);
        timer.Stop();
        var edgeSamples = new List<object>();
        if (!passed && reason.Contains(";from="))
        {
            // note: Compare source grading with the final detail sample at the rejected physical edge; no reconstruction or mutation of accepted controls is involved.
            var arguments = new object[] { plan, null, null };
            var sourceMethod = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareTerrainSource", BindingFlags.NonPublic | BindingFlags.Static);
            Require((bool)sourceMethod.Invoke(null, arguments), (string)arguments[2]);
            var source = (YQSpatialBlueprintTerrainSamplerV2)arguments[1];
            var heightMethod = typeof(YQGeneratedWorldTerrain).GetMethod("CreatePreparedV2HeightSampler", BindingFlags.NonPublic | BindingFlags.Static);
            var height = (YQGeneratedWorldTerrain.V2HeightSampler)heightMethod.Invoke(null, new object[] { plan, source });
            var nodeMethod = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("SampleNativeChannelNode", BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (string part in reason.Split(';'))
            {
                if (!part.StartsWith("from=", StringComparison.Ordinal) && !part.StartsWith("to=", StringComparison.Ordinal)) continue;
                string[] values = part.Substring(part.IndexOf('=') + 1).Split(',');
                float x = float.Parse(values[0], System.Globalization.CultureInfo.InvariantCulture);
                float z = float.Parse(values[1], System.Globalization.CultureInfo.InvariantCulture);
                var sample = source.Sample(x, z);
                var channels = new List<object>();
                for (int channel = 0; channel < plan.spatialPlanV2.blueprint.hydrology.Count; channel++)
                {
                    var water = plan.spatialPlanV2.blueprint.hydrology[channel];
                    if (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall) continue;
                    channels.Add(new { channel, node = nodeMethod.Invoke(source, new object[] { channel, x, z }) });
                }
                edgeSamples.Add(new { x, z, source = sample, finalHeight = height.SampleNormalized(x, z), channels });
            }
        }
        File.WriteAllText(Path.Combine(output, "Receipt.json"), JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), passed, reason, preparationMilliseconds = timer.Elapsed.TotalMilliseconds,
            edgeSamples,
            nativePatches = sampler != null ? sampler.NativeJoinCount : 0,
            input, inputSha256 = before, inputUnchanged = before == Sha256(input),
            runtimeAssemblySha256 = Sha256(typeof(YQSpatialBlueprintCompilerV2).Assembly.Location),
            domainSha256 = Sha256("Assets/Assets/Scripts/Generated/YQConnectedBasinDomainV2.cs"),
            samplerSha256 = Sha256("Assets/Assets/Scripts/Generated/YQSpatialBlueprintTerrainSamplerV2.cs"),
            evidence = "UNITY_READ_ONLY_DERIVED_PREPARATION; no terrain publication, collider, player or save verification"
        }, Formatting.Indented));
        Debug.Log("Terrain native reach preparation " + (passed ? "PASS" : "FAIL: " + reason) + "; receipt=" + output);
    }

    [MenuItem("YourQuest/Terrain Refactor/Verify Basin Contracts (Read Only)")]
    private static void Verify()
    {
        // note: Exercise Unity's actual JSON, float formatter, accepted router and prepared sampler against an archived disposable world; never select, rewrite or regenerate a live profile.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Leave Play Mode before read-only basin verification.");
        string output = Path.Combine("outputs/TerrainRefactor_20261011", DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_unity_basins");
        Directory.CreateDirectory(output);
        var checks = new List<object>();
        var sourceSha256 = new Dictionary<string, string>();
        foreach (string relative in new[] {
            "Assets/Assets/Scripts/Generated/YQSpatialBlueprintCompilerV2.cs",
            "Assets/Assets/Scripts/Generated/YQConnectedBasinDomainV2.cs",
            "Assets/Assets/Scripts/Generated/YQSpatialBlueprintTerrainSamplerV2.cs",
            "Assets/Assets/Scripts/Generated/YQSpatialMaterializationV2.cs",
            "Assets/Assets/Scripts/Generated/YQSpatialSiteAccessV2.cs",
            "Assets/Assets/Scripts/Generated/YQSpatialSiteNetworkCompilerV2.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedWorldTerrain.cs",
            "Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs",
            "Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs",
            "Assets/Assets/Scripts/Generated/YQSpatialBlueprintV2Contracts.cs",
            "Assets/Assets/Scripts/Generated/YQSpatialBlueprintV2Validation.cs",
            "Assets/Assets/Scripts/Generated/YQWorldGenerationV2Contracts.cs",
            "Assets/Assets/Scripts/Generated/Editor/YQTerrainRefactorBasinVerification.cs" })
            sourceSha256.Add(relative, Sha256(relative));
        int failures = 0;
        GeneratedWorldPlanRecord original = null;
        GeneratedWorldPlanRecord draft = null;
        GeneratedWorldPlanRecord nativeVersion2 = null;
        Action<string, Action> check = (id, test) =>
        {
            try { test(); checks.Add(new { id, passed = true }); }
            catch (Exception error) { failures++; checks.Add(new { id, passed = false, error = error.ToString() }); }
        };
        check("ARCHIVED-ORDINARY-TOPOLOGY-10-ROUTING", () =>
        {
            string json = File.ReadAllText("outputs/TerrainRefactor_20261011/unity_baseline/world_state.json");
            original = JObject.Parse(json)["generatedWorldPlan"].ToObject<GeneratedWorldPlanRecord>();
            Require(original.spatialPlanV2.contentHash == "a66a9706ef076d81", "Wrong archived disposable world.");
            string before = JsonUtility.ToJson(original);
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(original, out string reason), reason);
            Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(original.spatialPlanV2) == original.spatialPlanV2.contentHash,
                "Unity's legacy content hash changed.");
            Require(before == JsonUtility.ToJson(original), "Read-only routing mutated the archived plan.");
        });
        check("ARCHIVED-TOPOLOGY-11-ROUTING", () =>
        {
            // note: Retain the exact former new-generation checksum and Unity router path after introducing connected-domain topology 12.
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_101211_592_unity_basins/new-draft.json"));
            Require(archived.spatialPlanV2.contentHash == "455a452418ecdd5e", "Wrong topology-11 archived identity.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(archived, out string reason), reason);
        });
        check("ARCHIVED-NATIVE-DOMAIN-2-ROUTING", () =>
        {
            // note: The new streaming projection cannot mutate the archived native-domain-2 artifact or reinterpret its accepted hash.
            nativeVersion2 = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_122400_536_unity_basins/new-draft.json"));
            string before = JsonUtility.ToJson(nativeVersion2);
            Require(nativeVersion2.spatialPlanV2.contentHash == "18e50b2a4775cf95", "Wrong native-domain-2 archive.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(nativeVersion2, out string reason), reason);
            Require(before == JsonUtility.ToJson(nativeVersion2), "Archived routing mutated its input.");
        });
        check("ARCHIVED-SHARED-PROJECTION-3-ROUTING", () =>
        {
            // note: Domain 3 keeps its earlier presentation; no accepted bank contract is silently upgraded.
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_125113_373_unity_basins/new-draft.json"));
            string before = JsonUtility.ToJson(archived);
            Require(archived.spatialPlanV2.contentHash == "6db2b157d02d057b", "Wrong shared-projection archive.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(archived, out string reason), reason);
            Require(YQSpatialMaterializationCompilerV2.TryPrepare(archived, out var prepared, out reason), reason);
            Require((bool)typeof(YQPreparedSpatialMaterializationV2).GetProperty("UsesSharedChannelProjection", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(prepared) &&
                !(bool)typeof(YQPreparedSpatialMaterializationV2).GetProperty("UsesCanonicalChannelBanks", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(prepared), "Archived presentation version changed.");
            Require(before == JsonUtility.ToJson(archived), "Archived domain 3 changed.");
        });
        check("ARCHIVED-CANONICAL-BANK-4-ROUTING", () =>
        {
            // note: Previously accepted bank geometry and padded channel projection remain on version 4 after introducing the physical wet-footprint change.
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_131123_096_unity_basins/new-draft.json"));
            string before = JsonUtility.ToJson(archived);
            Require(archived.spatialPlanV2.contentHash == "537ffc8a1a592321", "Wrong canonical-bank archive.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(archived, out string reason), reason);
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(archived, out var prepared, out reason), reason);
            Require(!(bool)typeof(YQSpatialBlueprintTerrainSamplerV2).GetProperty("UsesCanonicalWetFootprint", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(prepared), "Archived channel padding changed.");
            Require(before == JsonUtility.ToJson(archived), "Archived domain 4 changed.");
        });
        check("ORDINARY-SEMANTICS-NEW-BASIN-COMPILATION", () =>
        {
            Require(original != null, "Archived input did not load.");
            draft = JsonUtility.FromJson<GeneratedWorldPlanRecord>(JsonUtility.ToJson(original));
            Require(YQSpatialBlueprintCompilerV2.TryCompile(draft, out var compiled, out string reason), reason);
            draft.spatialPlanV2 = compiled;
            Require(compiled.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion, "Wrong draft generation version.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(draft, out reason), reason);
            File.WriteAllText(Path.Combine(output, "new-draft.json"), JsonUtility.ToJson(draft, true));
        });
        check("UNITY-JSON-BASIN-ROUNDTRIP", () =>
        {
            Require(draft?.spatialPlanV2?.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion, "New draft unavailable.");
            var restored = JsonUtility.FromJson<GeneratedWorldPlanRecord>(JsonUtility.ToJson(draft));
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(restored, out string reason), reason);
            Require(restored.spatialPlanV2.contentHash == draft.spatialPlanV2.contentHash, "Roundtrip changed canonical basin decisions.");
            var lake = restored.spatialPlanV2.blueprint.hydrology.Find(feature => feature.hydrologyId == "water:lake:opening");
            Require(lake.basin != null && lake.basin.hasSpill && lake.basin.spillPoint != null && lake.basin.disposition == YQBasinDispositionV2.Open,
                "Unity serialization dropped basin metadata.");
        });
        check("FINAL-DETAIL-KEEPS-CHANNEL-GRADING-AND-ARCHIVED-HEIGHT", () =>
        {
            // note: Exercise the actual Unity noise/detail path at the failed bank and across its surrounding native nodes; keep the archived version-7 witness pinned separately.
            var sourceMethod = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareTerrainSource", BindingFlags.NonPublic | BindingFlags.Static);
            var heightMethod = typeof(YQGeneratedWorldTerrain).GetMethod("CreatePreparedV2HeightSampler", BindingFlags.NonPublic | BindingFlags.Static);
            var type = typeof(YQSpatialTerrainSampleV2);
            var active = type.GetField("constrainFinalChannelElevation", BindingFlags.NonPublic | BindingFlags.Instance);
            var minimum = type.GetField("minimumChannelElevation", BindingFlags.NonPublic | BindingFlags.Instance);
            var maximum = type.GetField("maximumChannelElevation", BindingFlags.NonPublic | BindingFlags.Instance);
            var arguments = new object[] { draft, null, null };
            Require((bool)sourceMethod.Invoke(null, arguments), (string)arguments[2]);
            var source = (YQSpatialBlueprintTerrainSamplerV2)arguments[1];
            var height = (YQGeneratedWorldTerrain.V2HeightSampler)heightMethod.Invoke(null, new object[] { draft, source });
            int protectedSamples = 0;
            for (float z = 240f; z <= 300f; z += 2f)
            for (float x = 752f; x <= 816f; x += 2f)
            {
                var sample = source.Sample(x, z);
                if (!(bool)active.GetValue(sample)) continue;
                protectedSamples++;
                float final = height.SampleNormalized(x, z);
                Require(final >= (float)minimum.GetValue(sample) - .000001f && final <= (float)maximum.GetValue(sample) + .000001f,
                    "Final detail escaped the accepted channel interval at " + x + "," + z);
            }
            Require(protectedSamples > 500, "Insufficient channel-bank detail coverage.");
            Require(height.SampleNormalized(784f, 256f) >= .4087079f, "The rejected dry-bank node remained submerged.");
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_153928_319_unity_basins/new-draft.json"));
            arguments = new object[] { archived, null, null };
            Require((bool)sourceMethod.Invoke(null, arguments), (string)arguments[2]);
            source = (YQSpatialBlueprintTerrainSamplerV2)arguments[1];
            height = (YQGeneratedWorldTerrain.V2HeightSampler)heightMethod.Invoke(null, new object[] { archived, source });
            Require(!(bool)active.GetValue(source.Sample(784f, 256f)), "Archived terrain acquired the new detail safeguard.");
            Require(Mathf.Abs(height.SampleNormalized(784f, 256f) - .40739423f) < .000001f, "Archived detail-height witness changed.");
            File.WriteAllText(Path.Combine(output, "final-channel-detail.json"), JsonConvert.SerializeObject(new {
                protectedSamples, archivedHeight = height.SampleNormalized(784f, 256f), evidence = "UNITY_SOURCE_HEIGHT_SAMPLER; no native collider or final grading proof"
            }, Formatting.Indented));
        });
        check("PREPARED-SAMPLER-SPILL-LIP", () =>
        {
            Require(draft?.spatialPlanV2?.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion, "New draft unavailable.");
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(draft, out var sampler, out string reason), reason);
            var lake = draft.spatialPlanV2.blueprint.hydrology.Find(feature => feature.hydrologyId == "water:lake:opening");
            var channel = draft.spatialPlanV2.blueprint.hydrology.Find(feature => feature.hydrologyId == lake.sinkHydrologyId);
            var start = channel.controlPoints[0];
            var lip = lake.basin.spillPoint;
            float maximumSurfaceErrorMetres = 0f;
            float maximumBedAboveWaterMetres = 0f;
            const int samples = 65;
            for (int index = 0; index < samples; index++)
            {
                float t = index / (float)(samples - 1);
                var sample = sampler.Sample(Mathf.Lerp(start.x, lip.x, t), Mathf.Lerp(start.z, lip.z, t));
                maximumSurfaceErrorMetres = Mathf.Max(maximumSurfaceErrorMetres,
                    Mathf.Abs(sample.waterSurfaceNormalized - lake.waterLevelNormalized) * YQGeneratedWorldTerrain.TerrainHeight);
                maximumBedAboveWaterMetres = Mathf.Max(maximumBedAboveWaterMetres,
                    (sample.elevationNormalized - lake.waterLevelNormalized) * YQGeneratedWorldTerrain.TerrainHeight);
            }
            File.WriteAllText(Path.Combine(output, "spill-samples.json"), JsonConvert.SerializeObject(new
            { samples, maximumSurfaceErrorMetres, maximumBedAboveWaterMetres, evidence = "PREPARED_PRODUCTION_SAMPLER_NOT_FINAL_GRADED_TERRAIN_OR_MESH" }, Formatting.Indented));
            Require(maximumSurfaceErrorMetres <= .01f, "Spill water surface mismatch exceeds 1 cm.");
            Require(maximumBedAboveWaterMetres <= .01f, "Spill bed stands above its accepted water surface.");
        });
        check("CHANNEL-JOINS-DO-NOT-FILL-CONNECTED-WET-BED", () =>
        {
            // note: Cover submerged basin vertices beyond the centre-to-spill line, including river-bank shoulders that formerly raised lake beds above water.
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(draft, out var sampler, out string reason), reason);
            int samples = 0;
            float maximumFillMetres = 0f;
            foreach (var water in draft.spatialPlanV2.blueprint.hydrology)
            {
                if (!water.hasConnectedDomain) continue;
                Require(YQPreparedConnectedBasinV2.TryPrepare(water.connectedDomain, water.waterLevelNormalized, out var basin, out reason), reason);
                for (float z = basin.MinimumZ; z < basin.MaximumZ; z += water.connectedDomain.spacing)
                for (float x = basin.MinimumX; x < basin.MaximumX; x += water.connectedDomain.spacing)
                {
                    if (!basin.TrySampleBed(x, z, out float bed, out bool wet) || !wet) continue;
                    float actual = sampler.Sample(x, z).elevationNormalized;
                    maximumFillMetres = Mathf.Max(maximumFillMetres, (actual - bed) * YQGeneratedWorldTerrain.TerrainHeight);
                    samples++;
                }
            }
            File.WriteAllText(Path.Combine(output, "wet-bed-fill.json"), JsonConvert.SerializeObject(new {
                samples, maximumFillMetres, evidence = "UNITY_PREPARED_PRODUCTION_SAMPLER_NOT_FINAL_GRADED_TERRAIN_OR_COLLIDER"
            }, Formatting.Indented));
            Require(samples > 0 && maximumFillMetres <= .01f, "Channel projection fills the accepted wet basin bed by more than 1 cm.");
        });
        check("FINITE-STREAMED-CHANNEL-PROJECTION-AGREEMENT", () =>
        {
            // note: Reapplying the real streamed modifier to finite accepted water must be idempotent. Preserve the native-domain-2 result separately to quantify the old mismatch, without repairing an accepted archive.
            Require(YQSpatialMaterializationCompilerV2.TryPrepare(draft, out var current, out string reason), reason);
            Require(YQSpatialMaterializationCompilerV2.TryPrepare(nativeVersion2, out var historical, out reason), reason);
            int samples = 0;
            float maximumCurrentDeltaMetres = 0f, maximumHistoricalDeltaMetres = 0f, maximumFiniteCompatibilityDeltaMetres = 0f;
            var discrepancies = new List<object>();
            Action<float, float> sample = (x, z) =>
            {
                var finite = current.SampleTerrain(x, z);
                if (finite.connectedBasinSupportMask < .999f && finite.waterMask < .999f) return;
                float streamed = YQContinuousWorldFeatureAuthority.ApplyAcceptedTerrainModifiers(current, x, z,
                    finite.elevationNormalized, YQGeneratedWorldTerrain.TerrainHeight);
                var older = historical.SampleTerrain(x, z);
                float oldStreamed = YQContinuousWorldFeatureAuthority.ApplyAcceptedTerrainModifiers(historical, x, z,
                    older.elevationNormalized, YQGeneratedWorldTerrain.TerrainHeight);
                float delta = Mathf.Abs(streamed - finite.elevationNormalized) * YQGeneratedWorldTerrain.TerrainHeight;
                maximumCurrentDeltaMetres = Mathf.Max(maximumCurrentDeltaMetres, delta);
                maximumHistoricalDeltaMetres = Mathf.Max(maximumHistoricalDeltaMetres,
                    Mathf.Abs(oldStreamed - older.elevationNormalized) * YQGeneratedWorldTerrain.TerrainHeight);
                maximumFiniteCompatibilityDeltaMetres = Mathf.Max(maximumFiniteCompatibilityDeltaMetres,
                    Mathf.Abs(older.elevationNormalized - finite.elevationNormalized) * YQGeneratedWorldTerrain.TerrainHeight);
                if (delta > .01f && discrepancies.Count < 32) discrepancies.Add(new { x, z, delta, finite = finite.elevationNormalized, streamed });
                samples++;
            };
            foreach (var water in draft.spatialPlanV2.blueprint.hydrology)
            {
                if (!water.hasConnectedDomain) continue;
                Require(water.connectedDomain.version == YQPreparedConnectedBasinV2.NativeChannelReachVersion, "Draft native join is not version-pinned.");
                Require(YQPreparedConnectedBasinV2.TryPrepare(water.connectedDomain, water.waterLevelNormalized, out var basin, out reason), reason);
                for (float z = basin.MinimumZ; z < basin.MaximumZ; z += 8f)
                for (float x = basin.MinimumX; x < basin.MaximumX; x += 8f) sample(x, z);
                for (int edge = 0; edge < basin.ShoreSegmentCount; edge++)
                {
                    basin.GetShoreSegment(edge, out Vector2 first, out Vector2 second);
                    for (int fraction = 0; fraction <= 2; fraction++)
                    {
                        Vector2 point = Vector2.Lerp(first, second, fraction * .5f);
                        sample(point.x, point.y);
                    }
                }
            }
            File.WriteAllText(Path.Combine(output, "finite-streamed-projection.json"), JsonConvert.SerializeObject(new {
                samples, maximumCurrentDeltaMetres, maximumHistoricalDeltaMetres, maximumFiniteCompatibilityDeltaMetres, discrepancies,
                evidence = "UNITY_PREPARED_PRODUCTION_FINITE_AND_STREAMED_MODIFIER_NOT_NATIVE_FINAL_TERRAIN_OR_TRAVERSAL"
            }, Formatting.Indented));
            Require(samples > 0 && maximumCurrentDeltaMetres <= .01f, "Streamed channel projection changes finite accepted water by more than 1 cm.");
            // note: Version 5 deliberately changes padded channel cuts. Preserve the measured historical delta rather than asserting that new physical geometry must match the archived defect.
            Require(maximumFiniteCompatibilityDeltaMetres > .01f, "The new physical footprint did not change the retained padding witnesses.");
        });
        check("CANONICAL-ORIGIN-AND-STREAMED-BANK-MESHES", () => VerifyCanonicalBankMeshes(
            JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_141344_796_unity_basins/new-draft.json")), output));
        check("SHARED-BASIN-DOMAIN-MATERIALIZATION", () =>
        {
            Require(draft?.spatialPlanV2?.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion, "New draft unavailable.");
            Require(YQSpatialMaterializationCompilerV2.TryPrepare(draft, out var prepared, out string reason), reason);
            int basins = 0;
            for (int index = 0; index < prepared.WaterCount; index++)
            {
                var water = prepared.GetWater(index);
                if (water.kind != YQHydrologyKindV2.Lake && water.kind != YQHydrologyKindV2.Wetland) continue;
                Require(water.connectedBasin != null, "Materialization dropped the accepted basin domain.");
                var source = draft.spatialPlanV2.blueprint.hydrology.Find(feature => feature.hydrologyId == water.hydrologyId);
                Require(water.connectedBasin.TrySampleBed(source.connectedDomain.seedX, source.connectedDomain.seedZ, out _, out bool wet) && wet,
                    "Materialization does not retain the connected wet seed.");
                var vertices = new List<Vector2>(); var triangles = new List<int>();
                water.connectedBasin.AppendClippedTriangles(water.connectedBasin.MinimumX, water.connectedBasin.MinimumZ,
                    water.connectedBasin.MaximumX, water.connectedBasin.MaximumZ, vertices, triangles);
                Require(triangles.Count > 0, "Materialization has no water polygons.");
                basins++;
            }
            Require(basins == 2, "Ordinary semantics did not materialize both generated basin domains.");
        });
        check("ARCHIVED-VERSION-5-WET-FOOTPRINT-ROUTING", () =>
        {
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_133900_123_unity_basins/new-draft.json"));
            string before = JsonUtility.ToJson(archived);
            Require(archived.spatialPlanV2.contentHash == "982b722f7c59e993", "Wrong version-5 archive.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(archived, out string reason), reason);
            Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(archived.spatialPlanV2) == archived.spatialPlanV2.contentHash, "Version-5 hash changed.");
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(archived, out var sampler, out reason), reason);
            for (int index = 0; index < archived.spatialPlanV2.blueprint.hydrology.Count; index++)
                if (archived.spatialPlanV2.blueprint.hydrology[index].hasConnectedDomain)
                    Require(sampler.GetMaterializedBasin(index).Version == 5, "Archive acquired the new join interpretation.");
            Require(before == JsonUtility.ToJson(archived), "Archived records changed during preparation.");
        });
        check("NATIVE-JOIN-MESH-PLACEMENT-AND-HEIGHT-SOURCE", () =>
        {
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(draft, out var sampler, out string reason), reason);
            Require(YQGeneratedWorldTerrain.TryCreateV2HeightSampler(draft, out var heights, out reason), reason);
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_133900_123_unity_basins/new-draft.json"));
            Require(YQGeneratedWorldTerrain.TryCreateV2HeightSampler(archived, out var archivedHeights, out reason), reason);
            var sourceArguments = new object[] { draft, null, null };
            Require((bool)typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareTerrainSource", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, sourceArguments), (string)sourceArguments[2]);
            var source = (YQSpatialBlueprintTerrainSamplerV2)sourceArguments[1];
            var constrain = typeof(YQSpatialTerrainSampleV2).GetField("constrainFinalChannelElevation", BindingFlags.NonPublic | BindingFlags.Instance);
            var minimum = typeof(YQSpatialTerrainSampleV2).GetField("minimumChannelElevation", BindingFlags.NonPublic | BindingFlags.Instance);
            var maximum = typeof(YQSpatialTerrainSampleV2).GetField("maximumChannelElevation", BindingFlags.NonPublic | BindingFlags.Instance);
            string before = JsonUtility.ToJson(draft);
            int samples = 0;
            for (int index = 0; index < draft.spatialPlanV2.blueprint.hydrology.Count; index++)
            {
                var basin = sampler.GetMaterializedBasin(index);
                if (basin == null) continue;
                Require(basin.Version == YQPreparedConnectedBasinV2.NativeChannelReachVersion, "New native reach projection is not active.");
                var vertices = new List<Vector2>(); var triangles = new List<int>();
                basin.AppendClippedTriangles(basin.MinimumX, basin.MinimumZ, basin.MaximumX, basin.MaximumZ, vertices, triangles);
                for (int triangle = 0; triangle < triangles.Count; triangle += 3)
                {
                    Vector2 point = (vertices[triangles[triangle]] + vertices[triangles[triangle + 1]] + vertices[triangles[triangle + 2]]) / 3f;
                    var sample = sampler.Sample(point.x, point.y);
                    Require(sample.waterMask >= .999f && sample.waterFeatureIndex == index, "Rendered lake polygon lost its placement exclusion.");
                    samples++;
                }
                for (float z = basin.MinimumZ; z < basin.MaximumZ; z += 8f)
                for (float x = basin.MinimumX; x < basin.MaximumX; x += 8f)
                {
                    // note: New bank detail corrections deliberately change protected channel nodes; unaffected source nodes retain the historical height exactly.
                    var metadata = source.Sample(x, z);
                    float finalHeight = heights.SampleNormalized(x, z);
                    if (!(bool)constrain.GetValue(metadata))
                        Require(finalHeight == archivedHeights.SampleNormalized(x, z), "An unaffected height-source node changed.");
                    else
                        Require(finalHeight >= (float)minimum.GetValue(metadata) - .000001f && finalHeight <= (float)maximum.GetValue(metadata) + .000001f,
                            "A corrected native source node escaped accepted channel grading.");
                    Require(basin.TrySampleBed(x, z, out float height, out _) && Mathf.Abs(height - heights.SampleNormalized(x, z)) < .000001f,
                        "Frozen native node differs from published height-source evaluation.");
                }
            }
            Require(samples > 0 && before == JsonUtility.ToJson(draft), "No native joined coverage or accepted input changed.");
        });
        check("ARCHIVED-VERSION-6-NATIVE-LAKE-ROUTING", () =>
        {
            // note: Version 7 changes only newly accepted channel joins; an archived version-6 lake keeps its exact interpretation and identity.
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_141344_796_unity_basins/new-draft.json"));
            string before = JsonUtility.ToJson(archived);
            Require(archived.spatialPlanV2.contentHash == "773cdc6f52190d95", "Wrong version-6 archive.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(archived, out string reason), reason);
            Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(archived.spatialPlanV2) == archived.spatialPlanV2.contentHash, "Version-6 hash changed.");
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(archived, out var sampler, out reason), reason);
            Require(sampler.NativeJoinCount == 0, "Archived lake acquired new channel geometry.");
            for (int index = 0; index < archived.spatialPlanV2.blueprint.hydrology.Count; index++)
                if (archived.spatialPlanV2.blueprint.hydrology[index].hasConnectedDomain)
                    Require(sampler.GetMaterializedBasin(index).Version == 6, "Archived lake interpretation changed.");
            Require(before == JsonUtility.ToJson(archived), "Preparation changed an archived record.");
        });
        check("ARCHIVED-VERSION-7-BOUNDED-JOIN-ROUTING", () =>
        {
            // note: A new representation version must leave the prior accepted join rectangles and canonical hash untouched.
            var archived = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261011/20261011_153928_319_unity_basins/new-draft.json"));
            string before = JsonUtility.ToJson(archived);
            Require(archived.spatialPlanV2.contentHash == "515fc09f9099c163", "Wrong version-7 archive.");
            Require(YQSpatialPlanVersionRouter.TryValidateAcceptedV2(archived, out string reason), reason);
            Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(archived.spatialPlanV2) == archived.spatialPlanV2.contentHash, "Version-7 hash changed.");
            Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(archived, out var sampler, out reason), reason);
            Require(sampler.NativeJoinCount == 2, "Archived version-7 acquired full-reach tiles.");
            Require(sampler.GetNativeJoin(0).MinimumX == 394f, "Archived bounded join shifted.");
            Require(before == JsonUtility.ToJson(archived), "Preparation changed an archived version-7 record.");
        });
        check("NATIVE-CHANNEL-ORIGIN-AND-STREAMED-MESHES", () => VerifyNativeChannelMeshes(draft, output));
        File.WriteAllText(Path.Combine(output, "Receipt.json"), JsonConvert.SerializeObject(new
        {
            utc = DateTime.UtcNow.ToString("O"), passed = failures == 0, failures, checks,
            runtimeAssemblyMvid = typeof(YQSpatialBlueprintCompilerV2).Assembly.ManifestModule.ModuleVersionId,
            editorAssemblyMvid = typeof(YQTerrainRefactorBasinVerification).Assembly.ManifestModule.ModuleVersionId,
            runtimeAssemblySha256 = Sha256(typeof(YQSpatialBlueprintCompilerV2).Assembly.Location),
            sourceSha256,
            evidence = "UNITY_EDITOR_READ_ONLY_ROUTING_SERIALIZATION_COMPILATION_AND_PREPARED_SAMPLER",
            limits = "Archived disposable input only; topology 12 is an ephemeral draft. No live save, native heightmap, final graded mesh, player traversal, coast or volume-balance proof."
        }, Formatting.Indented));
        if (failures == 0) Debug.Log("[YQTerrainBasins] PASS; " + output);
        else Debug.LogError("[YQTerrainBasins] FAIL " + failures + "; " + output);
    }

    private static void VerifyCanonicalBankMeshes(GeneratedWorldPlanRecord draft, string output)
    {
        // note: Exercise both real private mesh builders in a disposable preview scene. Reflection is confined to this Editor verifier and introduces no production API or asset dependency.
        Require(YQSpatialMaterializationCompilerV2.TryPrepare(draft, out var prepared, out string reason), reason);
        Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(draft, out var sampler, out reason), reason);
        var originBuilder = typeof(YQGeneratedWorldEnvironment).GetMethod("BuildPreparedWaterRibbonMeshRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        var streamBuilder = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildRibbonCore", BindingFlags.NonPublic | BindingFlags.Static);
        var bankReader = typeof(YQPreparedSpatialMaterializationV2).GetMethod("GetAcceptedChannelBankOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        Func<int, int, Vector3> bankOffset = (waterIndex, pointIndex) => (Vector3)bankReader.Invoke(prepared, new object[] { waterIndex, pointIndex });
        Require(originBuilder != null && streamBuilder != null, "Production ribbon builders are unavailable.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject parent = null;
        TerrainData data = null;
        Material material = null;
        Mesh originMesh = null;
        int features = 0, vertices = 0;
        float maximumEdgeErrorMetres = 0f;
        try
        {
            parent = new GameObject("DISPOSABLE_CanonicalBankProbe") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, scene);
            var terrain = parent.AddComponent<Terrain>();
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(1024f, 140f, 1024f), hideFlags = HideFlags.HideAndDontSave };
            terrain.terrainData = data;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "The installed URP shader is unavailable for the disposable mesh fixture.");
            material = new Material(shader) { name = "DISPOSABLE_BankProbe", hideFlags = HideFlags.HideAndDontSave };
            for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
            {
                var water = prepared.GetWater(waterIndex);
                if (water.kind != YQHydrologyKindV2.River) continue;
                var points = new List<Vector3>();
                var widths = new List<float>();
                var offsets = new List<Vector3>();
                int count = prepared.GetWaterPointCount(waterIndex);
                // note: Include interior subdivisions as well as original bends, with exactly interpolated accepted edges.
                for (int segment = 0; segment + 1 < count; segment++)
                {
                    var a = prepared.GetWaterPoint(waterIndex, segment);
                    var b = prepared.GetWaterPoint(waterIndex, segment + 1);
                    for (int step = 0; step < 3; step++)
                    {
                        float t = step / 3f;
                        points.Add(new Vector3(Mathf.Lerp(a.x, b.x, t), Mathf.Lerp(a.waterSurfaceNormalized, b.waterSurfaceNormalized, t) * 140f + .005f, Mathf.Lerp(a.z, b.z, t)));
                        widths.Add(Mathf.Lerp(a.width, b.width, t));
                        offsets.Add(Vector3.Lerp(bankOffset(waterIndex, segment), bankOffset(waterIndex, segment + 1), t));
                    }
                }
                var end = prepared.GetWaterPoint(waterIndex, count - 1);
                points.Add(new Vector3(end.x, end.waterSurfaceNormalized * 140f + .005f, end.z));
                widths.Add(end.width); offsets.Add(bankOffset(waterIndex, count - 1));
                originMesh = BuildOriginRibbonFixture(originBuilder, points, widths, waterIndex, water.kind, sampler, terrain, offsets);
                var streamed = (GameObject)streamBuilder.Invoke(null, new object[] { parent.transform, "DISPOSABLE_StreamedBank", points, water.nominalWidth, material, true, null, widths, offsets });
                Require(originMesh != null && streamed != null, "A canonical ribbon failed construction.");
                var streamedMesh = streamed.GetComponent<MeshFilter>().sharedMesh;
                var originVertices = originMesh.vertices;
                var streamedVertices = streamedMesh.vertices;
                Require(originVertices.Length == points.Count * 2 && streamedVertices.Length == originVertices.Length, "Bank vertex count changed.");
                for (int vertex = 0; vertex < originVertices.Length; vertex++)
                {
                    int point = vertex / 2;
                    Vector3 expected = points[point] + offsets[point] * (vertex % 2 == 0 ? -1f : 1f);
                    Vector3 actualStreamed = streamed.transform.TransformPoint(streamedVertices[vertex]);
                    float originError = Vector2.Distance(new Vector2(expected.x, expected.z), new Vector2(originVertices[vertex].x, originVertices[vertex].z));
                    float streamedError = Vector2.Distance(new Vector2(expected.x, expected.z), new Vector2(actualStreamed.x, actualStreamed.z));
                    maximumEdgeErrorMetres = Mathf.Max(maximumEdgeErrorMetres, Mathf.Max(originError, streamedError));
                }
                foreach (var normal in streamedMesh.normals) Require(normal.y > 0f, "A canonical streamed river face points downwards.");
                vertices += originVertices.Length;
                features++;
                UnityEngine.Object.DestroyImmediate(originMesh); originMesh = null;
                UnityEngine.Object.DestroyImmediate(streamed);
            }
        }
        finally
        {
            if (originMesh != null) UnityEngine.Object.DestroyImmediate(originMesh);
            if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        File.WriteAllText(Path.Combine(output, "canonical-bank-meshes.json"), JsonConvert.SerializeObject(new {
            features, vertices, maximumEdgeErrorMetres,
            evidence = "UNITY_REAL_ORIGIN_AND_STREAMED_MESH_BUILDERS_HORIZONTAL_BANK_FIXTURE",
            limits = "Horizontal accepted banks and upward river faces only. No final heights, terrain contact, receiving wet union, cell extraction, waves or ordinary streaming acceptance."
        }, Formatting.Indented));
        Require(features > 0 && maximumEdgeErrorMetres <= .01f, "Materialized banks differ from accepted edges by more than 1 cm.");
    }

    private static void VerifyNativeChannelMeshes(GeneratedWorldPlanRecord draft, string output)
    {
        // note: Exercise the real origin builder and streamed replacement publisher with disposable terrain owners, including both lake patches outside the finite opening world.
        Require(YQSpatialMaterializationCompilerV2.TryPrepare(draft, out var prepared, out string reason), reason);
        Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(draft, out var sampler, out reason), reason);
        Require(prepared.NativeJoinCount > 2, "Complete finite reaches were not projected.");
        var originBuilder = typeof(YQGeneratedWorldEnvironment).GetMethod("BuildPreparedWaterRibbonMeshRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        var streamBuilder = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildNativeChannelJoins", BindingFlags.NonPublic | BindingFlags.Static);
        var bankReader = typeof(YQPreparedSpatialMaterializationV2).GetMethod("GetAcceptedChannelBankOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        Require(originBuilder != null && streamBuilder != null && bankReader != null, "Production join builders unavailable.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        GameObject parent = null; TerrainData data = null; Material material = null; Mesh originMesh = null;
        var originMeshes = new Dictionary<int, Mesh>();
        int originProbes = 0, streamProbes = 0, streamObjects = 0;
        float maximumLevelErrorMetres = 0f;
        try
        {
            parent = new GameObject("DISPOSABLE_NativeChannelMeshProbe") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, scene);
            var terrain = parent.AddComponent<Terrain>();
            data = new TerrainData { heightmapResolution = 129, size = new Vector3(256f, 140f, 256f), hideFlags = HideFlags.HideAndDontSave };
            terrain.terrainData = data;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit"); Require(shader != null, "URP shader unavailable.");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            for (int joinIndex = 0; joinIndex < prepared.NativeJoinCount; joinIndex++)
            {
                var patch = prepared.GetNativeJoin(joinIndex);
                parent.transform.position = new Vector3(patch.MinimumX, 0f, patch.MinimumZ);
                int Probe(Mesh mesh, Transform coordinates)
                {
                    int probes = 0; var vertices = mesh.vertices; var triangles = mesh.triangles;
                    for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                    {
                        Vector3 a = vertices[triangles[triangle]], b = vertices[triangles[triangle + 1]], c = vertices[triangles[triangle + 2]];
                        if (coordinates != null) { a = coordinates.TransformPoint(a); b = coordinates.TransformPoint(b); c = coordinates.TransformPoint(c); }
                        Vector3 point = (a + b + c) / 3f;
                        if (point.x <= patch.MinimumX + .001f || point.x >= patch.MaximumX - .001f ||
                            point.z <= patch.MinimumZ + .001f || point.z >= patch.MaximumZ - .001f) continue;
                        Require(patch.TrySample(point.x, point.z, out int owner, out float level), "Rendered native channel has no shared wet polygon.");
                        var sample = sampler.Sample(point.x, point.z);
                        Require(sample.waterMask >= .999f && sample.waterFeatureIndex == owner, "Native channel render and placement ownership differ.");
                        float delta = Mathf.Abs(point.y - (level * 140f + .005f));
                        maximumLevelErrorMetres = Mathf.Max(maximumLevelErrorMetres, delta);
                        Require(delta <= .01f, "Rendered native level differs from the shared sampler by more than 1 cm.");
                        Require(Vector3.Cross(b - a, c - a).y > 0f, "Native joined water face points downward.");
                        probes++;
                    }
                    return probes;
                }
                for (int waterIndex = 0; waterIndex < prepared.WaterCount; waterIndex++)
                {
                    var water = prepared.GetWater(waterIndex);
                    if (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall) continue;
                    if (!originMeshes.TryGetValue(waterIndex, out originMesh))
                    {
                        // note: Build the complete production origin mesh once per accepted feature, then inspect every projection tile without repeatedly rebuilding the entire multi-kilometre river.
                        var points = new List<Vector3>(); var widths = new List<float>(); var offsets = new List<Vector3>();
                        for (int point = 0; point < prepared.GetWaterPointCount(waterIndex); point++)
                        {
                            var source = prepared.GetWaterPoint(waterIndex, point);
                            points.Add(new Vector3(source.x, source.waterSurfaceNormalized * 140f + .005f, source.z));
                            widths.Add(source.width); offsets.Add((Vector3)bankReader.Invoke(prepared, new object[] { waterIndex, point }));
                        }
                        originMesh = BuildOriginRibbonFixture(originBuilder, points, widths, waterIndex, water.kind, sampler, terrain, offsets);
                        Require(originMesh != null, "Origin join mesh unavailable.");
                        originMeshes.Add(waterIndex, originMesh);
                    }
                    originProbes += Probe(originMesh, null); originMesh = null;
                }
                bool expectedWet = false;
                for (int index = 0; index < prepared.WaterCount; index++)
                    expectedWet |= patch.IntersectsChannelRectangle(index, patch.MinimumX, patch.MinimumZ, patch.MaximumX, patch.MaximumZ);
                int built = (int)streamBuilder.Invoke(null, new object[] { parent.transform, prepared, terrain, material, new HashSet<int>() });
                Require((built > 0) == expectedWet, "Streamed publication differs from native wet coverage."); streamObjects += built;
                foreach (var filter in parent.GetComponentsInChildren<MeshFilter>()) streamProbes += Probe(filter.sharedMesh, filter.transform);
                // note: Retained origin water remains its sole owner; the cell publisher must create no replacement for retained feature IDs.
                var retained = new HashSet<int>(); for (int index = 0; index < prepared.WaterCount; index++) retained.Add(index);
                Require((int)streamBuilder.Invoke(null, new object[] { parent.transform, prepared, terrain, material, retained }) == 0, "Retained water acquired a duplicate owner.");
                while (parent.transform.childCount > 0) UnityEngine.Object.DestroyImmediate(parent.transform.GetChild(0).gameObject);
            }
        }
        finally
        {
            if (originMesh != null && !originMeshes.ContainsValue(originMesh)) UnityEngine.Object.DestroyImmediate(originMesh);
            foreach (var mesh in originMeshes.Values) UnityEngine.Object.DestroyImmediate(mesh);
            if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (material != null) UnityEngine.Object.DestroyImmediate(material);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        File.WriteAllText(Path.Combine(output, "native-channel-meshes.json"), JsonConvert.SerializeObject(new {
            originProbes, streamProbes, streamObjects, maximumLevelErrorMetres,
            evidence = "UNITY_PRODUCTION_ORIGIN_AND_STREAMED_NATIVE_CHANNEL_MESH_FIXTURE",
            limits = "Disposable pre-wave meshes and shared placement samples only; no final graded terrain, mixed LOD, live streaming, shader or ordinary traversal proof."
        }, Formatting.Indented));
        Require(originProbes > 0 && streamProbes > 0, "No native channel triangles checked.");
    }

    [MenuItem("YourQuest/Terrain Refactor/Measure Joined Water Profiles (Read Only)")]
    private static void MeasureJoinedWaterProfiles()
    {
        // note: Measure real pre-wave meshes from a source-matched disposable accepted draft; this diagnostic never selects or writes a live profile.
        Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Leave Play Mode before measuring water profiles.");
        const string evidenceRoot = "outputs/TerrainRefactor_20261011";
        string inputDirectory = null;
        foreach (string directory in Directory.GetDirectories(evidenceRoot, "*_unity_basins"))
        {
            string receiptPath = Path.Combine(directory, "Receipt.json");
            if (!File.Exists(receiptPath)) continue;
            var receipt = JObject.Parse(File.ReadAllText(receiptPath));
            // note: Older receipts without source/runtime binding cannot certify this measurement input.
            if ((bool?)receipt["passed"] != true || !(receipt["sourceSha256"] is JObject recordedSources) ||
                receipt["runtimeAssemblySha256"]?.Type != JTokenType.String) continue;
            bool matches = true;
            foreach (var property in recordedSources.Properties())
                if (!property.Name.Contains("/Editor/") && Sha256(property.Name) != (string)property.Value) matches = false;
            matches &= Sha256(typeof(YQSpatialBlueprintCompilerV2).Assembly.Location) == (string)receipt["runtimeAssemblySha256"];
            if (matches && (inputDirectory == null || string.CompareOrdinal(directory, inputDirectory) > 0)) inputDirectory = directory;
        }
        Require(inputDirectory != null, "No passing draft receipt matches the loaded runtime and current production source.");
        string inputPath = Path.Combine(inputDirectory, "new-draft.json"), inputHash = Sha256(inputPath);
        var plan = JsonUtility.FromJson<GeneratedWorldPlanRecord>(File.ReadAllText(inputPath));
        Require(YQSpatialMaterializationCompilerV2.TryPrepare(plan, out var prepared, out string reason), reason);
        Require(YQSpatialBlueprintTerrainSamplerV2.TryPrepare(plan, out var sampler, out reason), reason);
        string output = Path.Combine(evidenceRoot, DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_water_profiles");
        Directory.CreateDirectory(output);
        var sources = ((JObject)JObject.Parse(File.ReadAllText(Path.Combine(inputDirectory, "Receipt.json")))["sourceSha256"]).ToObject<Dictionary<string, string>>();
        sources["Assets/Assets/Scripts/Generated/Editor/YQTerrainRefactorBasinVerification.cs"] = Sha256("Assets/Assets/Scripts/Generated/Editor/YQTerrainRefactorBasinVerification.cs");
        var originBuilder = typeof(YQGeneratedWorldEnvironment).GetMethod("BuildPreparedWaterRibbonMeshRoutine", BindingFlags.NonPublic | BindingFlags.Static);
        var lakeBuilder = typeof(YQGeneratedWorldEnvironment).GetMethod("BuildConnectedBasinMesh", BindingFlags.NonPublic | BindingFlags.Static);
        var bankReader = typeof(YQPreparedSpatialMaterializationV2).GetMethod("GetAcceptedChannelBankOffset", BindingFlags.NonPublic | BindingFlags.Instance);
        var distanceReader = typeof(YQContinuousWorldFeatureAuthority).GetMethod("TryMeasureAcceptedWaterDistance", BindingFlags.NonPublic | BindingFlags.Static);
        var finiteDistanceReader = typeof(YQPreparedSpatialMaterializationV2).GetMethod("TryMeasureMaterializedChannelDistance", BindingFlags.NonPublic | BindingFlags.Instance);
        var descriptorConstructor = typeof(YQGeneratedWorldTerrain.MacroWaterBasinDescriptor).GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)[0];
        Require(originBuilder != null && lakeBuilder != null && bankReader != null, "Production water builders unavailable.");
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var meshes = new List<Mesh>(); var grid = new Dictionary<Vector2Int, List<WaterProfileFace>>();
        var channelFaces = new List<WaterProfileFace>(); var distancePoints = new List<Vector2>();
        GameObject parent = null; TerrainData data = null;
        var measurements = new List<object>(); var failures = new List<object>();
        int probes = 0, missing = 0, uphill = 0, maskMismatch = 0;
        int bankProbes = 0, missedWetClearance = 0;
        int distanceProbes = 0, distanceMismatch = 0; float maximumDistanceError = 0f;
        int handoffProbes = 0, handoffWetPairs = 0, handoffMissing = 0, handoffLevelMismatch = 0, handoffJumps = 0, submergedTerminations = 0;
        float maximumHandoffLevelError = 0f, maximumHandoffJump = 0f, maximumTerminationDepth = 0f;
        var handoffWitnesses = new List<object>();
        float maximumRise = 0f, maximumMaskDelta = 0f;
        bool InProbeBounds(Vector2 point, float padding)
        {
            for (int join = 0; join < prepared.NativeJoinCount; join++)
            {
                var patch = prepared.GetNativeJoin(join);
                if (point.x >= patch.MinimumX - padding && point.x <= patch.MaximumX + padding &&
                    point.y >= patch.MinimumZ - padding && point.y <= patch.MaximumZ + padding) return true;
            }
            return false;
        }
        try
        {
            parent = new GameObject("DISPOSABLE_JoinedWaterProfiles") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, scene);
            var terrain = parent.AddComponent<Terrain>();
            data = new TerrainData { heightmapResolution = 33, size = new Vector3(plan.spatialPlanV2.blueprint.worldSize, 140f, plan.spatialPlanV2.blueprint.worldSize), hideFlags = HideFlags.HideAndDontSave };
            terrain.terrainData = data;
            for (int index = 0; index < prepared.WaterCount; index++)
            {
                var water = prepared.GetWater(index); Mesh mesh = null;
                if (water.connectedBasin != null)
                {
                    var descriptor = descriptorConstructor.Invoke(new object[] { Vector3.zero, Vector2.right, Vector2.up, 1f, 1f, water.waterLevelNormalized * 140f + .005f });
                    mesh = (Mesh)lakeBuilder.Invoke(null, new object[] { terrain, descriptor, water.connectedBasin });
                }
                else if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall)
                {
                    var points = new List<Vector3>(); var widths = new List<float>(); var offsets = new List<Vector3>();
                    for (int point = 0; point < prepared.GetWaterPointCount(index); point++)
                    {
                        var source = prepared.GetWaterPoint(index, point);
                        float level = source.waterSurfaceNormalized > 0f ? source.waterSurfaceNormalized : water.waterLevelNormalized;
                        points.Add(new Vector3(source.x, Mathf.Clamp01(level) * 140f + .005f, source.z));
                        widths.Add(Mathf.Max(1.5f, Mathf.Max(water.nominalWidth, source.width)));
                        offsets.Add((Vector3)bankReader.Invoke(prepared, new object[] { index, point }));
                    }
                    // note: Match the production caller's accepted-input subdivision before the real mesh builder resolves receiving levels; sparse controls are a different fixture.
                    var sampledPoints = new List<Vector3>(); var sampledWidths = new List<float>(); var sampledOffsets = new List<Vector3>();
                    for (int point = 1; point < points.Count; point++)
                    {
                        int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(points[point - 1], points[point]) / .35f));
                        for (int step = 0; step < steps; step++)
                        {
                            float t = step / (float)steps;
                            sampledPoints.Add(Vector3.Lerp(points[point - 1], points[point], t));
                            sampledWidths.Add(Mathf.Lerp(widths[point - 1], widths[point], t));
                            sampledOffsets.Add(Vector3.Lerp(offsets[point - 1], offsets[point], t));
                        }
                    }
                    sampledPoints.Add(points[points.Count - 1]); sampledWidths.Add(widths[widths.Count - 1]); sampledOffsets.Add(offsets[offsets.Count - 1]);
                    mesh = BuildOriginRibbonFixture(originBuilder, sampledPoints, sampledWidths, index, water.kind, sampler, terrain, sampledOffsets);
                }
                if (mesh == null) continue; meshes.Add(mesh);
                var vertices = mesh.vertices; var triangles = mesh.triangles;
                for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                {
                    var face = new WaterProfileFace { a = vertices[triangles[triangle]], b = vertices[triangles[triangle + 1]], c = vertices[triangles[triangle + 2]], owner = index };
                    if (water.kind == YQHydrologyKindV2.River || water.kind == YQHydrologyKindV2.Waterfall) channelFaces.Add(face);
                    // note: Actual rendered wet interiors must be excluded by the production placement query, independent of centreline width estimates.
                    Vector3 center = (face.a + face.b + face.c) / 3f;
                    if (InProbeBounds(new Vector2(center.x, center.z), 8f) && Mathf.Abs(Vector3.Cross(face.b - face.a, face.c - face.a).y) > .000001f)
                    {
                        var arguments = new object[] { prepared, center.x, center.z, null, 0f, false };
                        bool found = (bool)distanceReader.Invoke(null, arguments); bankProbes++;
                        if (bankProbes % 2048 == 0)
                        {
                            distancePoints.Add(new Vector2(center.x + 1f, center.z + 3f));
                            distancePoints.Add(new Vector2(center.x - 5f, center.z + 2f));
                            distancePoints.Add(new Vector2(center.x + 17f, center.z - 11f));
                        }
                        if (!found || !(bool)arguments[5])
                        {
                            missedWetClearance++;
                            if (failures.Count < 64) failures.Add(new { kind = "missedWetClearance", owner = index, x = center.x, z = center.z, reportedDistance = arguments[4] });
                        }
                    }
                    float minX = Mathf.Min(face.a.x, Mathf.Min(face.b.x, face.c.x)), maxX = Mathf.Max(face.a.x, Mathf.Max(face.b.x, face.c.x));
                    float minZ = Mathf.Min(face.a.z, Mathf.Min(face.b.z, face.c.z)), maxZ = Mathf.Max(face.a.z, Mathf.Max(face.b.z, face.c.z));
                    for (int z = Mathf.FloorToInt(minZ / 8f); z <= Mathf.FloorToInt(maxZ / 8f); z++)
                    for (int x = Mathf.FloorToInt(minX / 8f); x <= Mathf.FloorToInt(maxX / 8f); x++)
                    {
                        if (!InProbeBounds(new Vector2(x * 8f + 4f, z * 8f + 4f), 16f)) continue;
                        var key = new Vector2Int(x, z);
                        if (!grid.TryGetValue(key, out var faces)) grid.Add(key, faces = new List<WaterProfileFace>());
                        faces.Add(face);
                    }
                }
            }
            // note: Independently measure nearest distance against actual published triangles, including dry offsets; a wet-centroid pass alone cannot prove clearance magnitudes.
            foreach (var point in distancePoints)
            {
                double squared = double.PositiveInfinity;
                foreach (var face in channelFaces) squared = Math.Min(squared, face.DistanceSquared(point));
                var arguments = new object[] { point.x, point.y, -1, 0f };
                bool found = (bool)finiteDistanceReader.Invoke(prepared, arguments);
                float expected = (float)Math.Sqrt(squared), actual = (float)arguments[3], error = Mathf.Abs(expected - actual);
                maximumDistanceError = Mathf.Max(maximumDistanceError, error); distanceProbes++;
                if (!found || error > .01f) { distanceMismatch++; if (failures.Count < 64) failures.Add(new { kind = "distanceMismatch", x = point.x, z = point.y, expected, actual, error }); }
            }
            // note: Sweep both sides of every native replacement boundary. Keep all failed witnesses; centreline samples cannot establish lateral or exact handoff continuity.
            var heightFactory = typeof(YQGeneratedWorldTerrain).GetMethod("CreatePreparedV2HeightSampler", BindingFlags.NonPublic | BindingFlags.Static);
            var sourceHeights = (YQGeneratedWorldTerrain.V2HeightSampler)heightFactory.Invoke(null, new object[] { plan, sampler });
            var sourceNodes = new Dictionary<Vector2Int, float>();
            float NodeHeight(int x, int z)
            {
                var key = new Vector2Int(x, z);
                if (!sourceNodes.TryGetValue(key, out float value)) sourceNodes.Add(key, value = sourceHeights.SampleNormalized(x * 2f, z * 2f) * 140f);
                return value;
            }
            float NativeSourceBed(Vector2 point)
            {
                // note: This source-triangle estimate uses the independently measured positive native diagonal; it does not claim fresh TerrainData quantization, final grading or collider evidence.
                int x = Mathf.FloorToInt(point.x / 2f), z = Mathf.FloorToInt(point.y / 2f);
                float fx = point.x / 2f - x, fz = point.y / 2f - z;
                return fx >= fz ? NodeHeight(x, z) * (1f - fx) + NodeHeight(x + 1, z + 1) * fz + NodeHeight(x + 1, z) * (fx - fz)
                    : NodeHeight(x, z) * (1f - fz) + NodeHeight(x + 1, z + 1) * fx + NodeHeight(x, z + 1) * (fz - fx);
            }
            bool MeshLevel(Vector2 point, int owner, out float level)
            {
                level = float.NaN;
                if (!grid.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x / 8f), Mathf.FloorToInt(point.y / 8f)), out var faces)) return false;
                foreach (var face in faces) if (face.owner == owner && face.TrySample(point, out level)) return true;
                return false;
            }
            for (int join = 0; join < prepared.NativeJoinCount; join++)
            {
                var patch = prepared.GetNativeJoin(join);
                for (int side = 0; side < 4; side++)
                {
                    float length = side < 2 ? patch.MaximumZ - patch.MinimumZ : patch.MaximumX - patch.MinimumX;
                    int steps = Mathf.CeilToInt(length / .25f);
                    Vector2 inward = side == 0 ? Vector2.right : side == 1 ? Vector2.left : side == 2 ? Vector2.up : Vector2.down;
                    for (int step = 0; step <= steps; step++)
                    {
                        float t = step / (float)steps;
                        Vector2 edge = side < 2 ? new Vector2(side == 0 ? patch.MinimumX : patch.MaximumX, Mathf.Lerp(patch.MinimumZ, patch.MaximumZ, t))
                            : new Vector2(Mathf.Lerp(patch.MinimumX, patch.MaximumX, t), side == 2 ? patch.MinimumZ : patch.MaximumZ);
                        Vector2 inside = edge + inward * .002f, outside = edge - inward * .002f;
                        var innerSample = sampler.Sample(inside.x, inside.y); var outerSample = sampler.Sample(outside.x, outside.y);
                        bool innerWet = innerSample.waterFeatureIndex >= 0 && innerSample.waterMask >= .999f;
                        bool outerWet = outerSample.waterFeatureIndex >= 0 && outerSample.waterMask >= .999f;
                        bool innerMesh = MeshLevel(inside, innerSample.waterFeatureIndex, out float innerLevel);
                        bool outerMesh = MeshLevel(outside, outerSample.waterFeatureIndex, out float outerLevel);
                        handoffProbes++;
                        string failure = null; float levelError = 0f, jump = 0f, depth = 0f;
                        if (innerWet && !innerMesh || outerWet && !outerMesh) { handoffMissing++; failure = "missingHandoffMesh"; }
                        if (innerWet && innerMesh) levelError = Mathf.Abs(innerLevel - (innerSample.waterSurfaceNormalized * 140f + .005f));
                        if (outerWet && outerMesh) levelError = Mathf.Max(levelError, Mathf.Abs(outerLevel - (outerSample.waterSurfaceNormalized * 140f + .005f)));
                        maximumHandoffLevelError = Mathf.Max(maximumHandoffLevelError, levelError);
                        if (levelError > .01f) { handoffLevelMismatch++; failure = "handoffMaskLevelMismatch"; }
                        if (innerWet && outerWet && innerMesh && outerMesh)
                        {
                            handoffWetPairs++; jump = Mathf.Abs(innerLevel - outerLevel); maximumHandoffJump = Mathf.Max(maximumHandoffJump, jump);
                            if (jump > .01f) { handoffJumps++; failure = "handoffJump"; }
                        }
                        if (innerWet != outerWet && (innerWet ? innerMesh : outerMesh))
                        {
                            depth = (innerWet ? innerLevel : outerLevel) - .005f - NativeSourceBed(edge);
                            maximumTerminationDepth = Mathf.Max(maximumTerminationDepth, depth);
                            if (depth > .01f) { submergedTerminations++; failure = "submergedHandoffTermination"; }
                        }
                        if (failure != null) handoffWitnesses.Add(new { kind = failure, join, side, x = edge.x, z = edge.y, innerWet, outerWet,
                            innerMesh, outerMesh, innerOwner = innerSample.waterFeatureIndex, outerOwner = outerSample.waterFeatureIndex, innerLevel, outerLevel, levelError, jump, depth });
                    }
                }
            }
            for (int index = 0; index < prepared.WaterCount; index++)
            {
                var water = prepared.GetWater(index);
                if (water.kind != YQHydrologyKindV2.River && water.kind != YQHydrologyKindV2.Waterfall) continue;
                float minimumLevel = float.PositiveInfinity; int channelProbes = 0;
                for (int segment = 0; segment + 1 < prepared.GetWaterPointCount(index); segment++)
                {
                    var first = prepared.GetWaterPoint(index, segment); var second = prepared.GetWaterPoint(index, segment + 1);
                    Vector2 a = new Vector2(first.x, first.z), b = new Vector2(second.x, second.z);
                    int steps = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / .5f));
                    for (int step = 0; step <= steps; step++)
                    {
                        Vector2 point = Vector2.Lerp(a, b, step / (float)steps);
                        if (!InProbeBounds(point, 8f)) { minimumLevel = float.PositiveInfinity; continue; }
                        var sample = sampler.Sample(point.x, point.y);
                        if (sample.waterFeatureIndex < 0 || sample.waterMask < .999f) { missing++; minimumLevel = float.PositiveInfinity; if (failures.Count < 64) failures.Add(new { kind = "missingMask", source = water.hydrologyId, x = point.x, z = point.y }); continue; }
                        float actual = float.NaN;
                        if (grid.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x / 8f), Mathf.FloorToInt(point.y / 8f)), out var faces))
                            foreach (var face in faces)
                                if (face.owner == sample.waterFeatureIndex && face.TrySample(point, out actual)) break;
                        if (float.IsNaN(actual)) { missing++; minimumLevel = float.PositiveInfinity; if (failures.Count < 64) failures.Add(new { kind = "missingMesh", source = water.hydrologyId, owner = sample.waterFeatureIndex, x = point.x, z = point.y }); continue; }
                        float rise = actual - minimumLevel, delta = Mathf.Abs(actual - (sample.waterSurfaceNormalized * 140f + .005f));
                        minimumLevel = Mathf.Min(minimumLevel, actual); maximumRise = Mathf.Max(maximumRise, rise); maximumMaskDelta = Mathf.Max(maximumMaskDelta, delta);
                        if (rise > .01f) { uphill++; if (failures.Count < 64) failures.Add(new { kind = "uphill", source = water.hydrologyId, x = point.x, z = point.y, actual, minimumLevel, rise }); }
                        if (delta > .01f) { maskMismatch++; if (failures.Count < 64) failures.Add(new { kind = "maskMismatch", source = water.hydrologyId, owner = sample.waterFeatureIndex, x = point.x, z = point.y, actual, expected = sample.waterSurfaceNormalized * 140f + .005f, delta }); }
                        probes++; channelProbes++;
                    }
                }
                measurements.Add(new { source = water.hydrologyId, channelProbes });
            }
        }
        finally
        {
            foreach (var mesh in meshes) UnityEngine.Object.DestroyImmediate(mesh);
            if (parent != null) UnityEngine.Object.DestroyImmediate(parent);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
        bool unchanged = inputHash == Sha256(inputPath); foreach (var source in sources) unchanged &= source.Value == Sha256(source.Key);
        bool profilePassed = unchanged && probes > 0 && missing == 0 && uphill == 0 && maskMismatch == 0;
        bool clearancePassed = unchanged && bankProbes > 0 && missedWetClearance == 0 && distanceProbes > 0 && distanceMismatch == 0;
        bool handoffPassed = unchanged && handoffWetPairs > 0 && handoffMissing == 0 && handoffLevelMismatch == 0 && handoffJumps == 0 && submergedTerminations == 0;
        bool passed = profilePassed && clearancePassed && handoffPassed;
        File.WriteAllText(Path.Combine(output, "handoff-witnesses.json"), JsonConvert.SerializeObject(handoffWitnesses, Formatting.Indented));
        File.WriteAllText(Path.Combine(output, "Receipt.json"), JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), passed, profilePassed, clearancePassed, bankProbes, missedWetClearance, probes, missing, uphill, maskMismatch, maximumRiseMetres = maximumRise,
            maximumMaskDeltaMetres = maximumMaskDelta, measurements, failures, input = inputPath, inputSha256 = inputHash,
            distanceProbes, distanceMismatch, maximumDistanceErrorMetres = maximumDistanceError,
            handoffPassed, handoffProbes, handoffWetPairs, handoffMissing, handoffLevelMismatch, handoffJumps, submergedTerminations,
            maximumHandoffLevelErrorMetres = maximumHandoffLevelError, maximumHandoffJumpMetres = maximumHandoffJump, maximumTerminationDepthMetres = maximumTerminationDepth,
            inputsUnchanged = unchanged, sourceSha256 = sources, runtimeAssemblySha256 = Sha256(typeof(YQSpatialBlueprintCompilerV2).Assembly.Location),
            evidence = "UNITY_PRODUCTION_PREWAVE_ORIGIN_WATER_MESH_CENTERLINES_AND_NATIVE_TILE_EDGES",
            inputPreparation = "Version-pinned disposable controls, production width/level fallback and 0.35m subdivision reproduced in this fixture; production low-level mesh builders exercised directly.",
            limits = "Half-metre centerlines near joins, quarter-metre edge sweep at plus/minus 2mm and published-triangle clearance checks. Termination beds use source native-triangle estimates. No final graded terrain, fresh native quantization/collider, mixed LOD, waves or ordinary travel proof."
        }, Formatting.Indented));
        Debug.Log("[YQJoinedWaterProfiles] " + (passed ? "PASS" : "FAIL") + "; " + output);
    }

    private struct WaterProfileFace
    {
        public Vector3 a, b, c;
        public int owner;
        public double DistanceSquared(Vector2 point)
        {
            if (TrySample(point, out _)) return 0d;
            // note: The reference reads materialized edges directly, without consulting the derived production index or accepted bank controls.
            double Edge(Vector3 first, Vector3 second)
            {
                double dx = (double)second.x - first.x, dz = (double)second.z - first.z;
                double px = (double)point.x - first.x, pz = (double)point.y - first.z, length = dx * dx + dz * dz;
                double t = length > 0d ? Math.Max(0d, Math.Min(1d, (px * dx + pz * dz) / length)) : 0d;
                px -= dx * t; pz -= dz * t; return px * px + pz * pz;
            }
            return Math.Min(Edge(a, b), Math.Min(Edge(b, c), Edge(c, a)));
        }
        public bool TrySample(Vector2 point, out float height)
        {
            // note: Double barycentrics measure the materialized float-coordinate triangle without introducing a second terrain or water equation.
            double bx = (double)b.x - a.x, bz = (double)b.z - a.z, cx = (double)c.x - a.x, cz = (double)c.z - a.z;
            double px = (double)point.x - a.x, pz = (double)point.y - a.z, determinant = bx * cz - bz * cx;
            height = float.NaN; if (Math.Abs(determinant) < 1e-10) return false;
            double v = (px * cz - pz * cx) / determinant, w = (bx * pz - bz * px) / determinant;
            if (v < -1e-7 || w < -1e-7 || v + w > 1d + 1e-7) return false;
            height = (float)(a.y * (1d - v - w) + b.y * v + c.y * w); return true;
        }
    }

    private static Mesh BuildOriginRibbonFixture(MethodInfo builder, List<Vector3> points, List<float> widths,
        int waterIndex, YQHydrologyKindV2 kind, YQSpatialBlueprintTerrainSamplerV2 sampler, Terrain terrain,
        List<Vector3> offsets)
    {
        // note: Execute the current production coroutine, including nested native patches, on disposable Editor objects. Synchronous fixture completion establishes geometry only, never publication budgets.
        Mesh result = null;
        int completions = 0;
        Action<Mesh> completed = mesh => { result = mesh; completions++; };
        var routines = new Stack<IEnumerator>();
        routines.Push((IEnumerator)builder.Invoke(null, new object[] { points, widths, waterIndex, kind, sampler, terrain, offsets, completed }));
        try
        {
            while (routines.Count > 0)
            {
                var current = routines.Peek();
                if (!current.MoveNext())
                {
                    routines.Pop();
                    (current as IDisposable)?.Dispose();
                    continue;
                }
                if (current.Current is IEnumerator nested) routines.Push(nested);
                else Require(current.Current == null, "The production ribbon now requires a yield unsupported by this synchronous geometry fixture.");
            }
            Require(completions == 1, "The production ribbon did not complete exactly once.");
            return result;
        }
        finally
        {
            while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose();
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static string Sha256(string path)
    {
        // note: Receipts bind the archived input check to current source and the executing Unity assembly without modifying either.
        using (var stream = File.OpenRead(path))
        using (var sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
}
