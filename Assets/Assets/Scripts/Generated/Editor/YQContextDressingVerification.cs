using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// note: Explicit editor regression checks exercise production placement mathematics without modifying a save or the open scene.
[InitializeOnLoad]
public static class YQContextDressingVerification
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
    static YQContextDressingVerification()
    {
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            !File.Exists("Temp/YQContextDressingVerification.request")) return;
        string command = File.ReadAllText("Temp/YQContextDressingVerification.request").Trim();
        File.Delete("Temp/YQContextDressingVerification.request");
        try
        {
            if (command == "inspect-encounter")
            {
                InspectEncounter();
                return;
            }
            if (command == "route-reserve")
            {
                VerifyRouteReserves();
                return;
            }
            if (command == "river-repair")
            {
                RepairSavedRiver();
                return;
            }
            Verify();
            File.WriteAllText("Logs/YQContextDressingVerification.txt", DateTime.UtcNow.ToString("O") +
                "\nPASS: curved verge tangent, both sides, seeded lake grouping, same-seed reproduction, different-seed variation, elliptical exclusion.\nRuntime navigation, visual quality and performance remain separate requirements.");
        }
        catch (Exception exception)
        {
            File.WriteAllText("Logs/YQContextDressingVerification.txt", "FAILED: " + exception);
        }
    }

    private static void VerifyRouteReserves()
    {
        // note: Exercise the real planner with an obstacle between samples and an invalid intermediate point; keep all saved-world changes detached.
        Type planner = typeof(YQSpatialBlueprintCompilerV2).Assembly.GetType("YQSpatialSiteNetworkCompilerV2");
        MethodInfo avoid = planner.GetMethod("TryAvoidUnrelatedSiteReserves", BindingFlags.Static | BindingFlags.NonPublic);
        var blueprint = new YQSpatialBlueprintV2();
        blueprint.sites.Add(new YQSiteAnchorV2 { siteId = "camp", kind = YQSiteKindV2.HostileSite, reservedRadius = 18f });
        var route = new YQRouteCorridorV2 { routeId = "road", fromSiteId = "a", toSiteId = "b", width = 6f, shoulderWidth = 3f };
        route.controlPoints.Add(new YQBlueprintPointV2 { x = -90f, normalizedElevation = .4f, width = 6f });
        route.controlPoints.Add(new YQBlueprintPointV2 { x = 0f, normalizedElevation = .4f, width = 6f });
        route.controlPoints.Add(new YQBlueprintPointV2 { x = 90f, normalizedElevation = .4f, width = 6f });
        string initial = Newtonsoft.Json.JsonConvert.SerializeObject(route);
        object[] args = { blueprint, route, null };
        Require((bool)avoid.Invoke(null, args), "Detour rejected: " + args[2]);
        Require(route.controlPoints[0].x == -90f && route.controlPoints[route.controlPoints.Count - 1].x == 90f, "Route destinations moved.");
        for (int index = 1; index < route.controlPoints.Count; index++)
        {
            var first = route.controlPoints[index - 1]; var last = route.controlPoints[index];
            Vector2 a = new Vector2(first.x, first.z), b = new Vector2(last.x, last.z);
            Vector2 delta = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(-a, delta) / delta.sqrMagnitude);
            Require((a + delta * t).magnitude >= 26f, "Detour still crosses the expanded reserve.");
        }
        var repeated = Newtonsoft.Json.JsonConvert.DeserializeObject<YQRouteCorridorV2>(initial);
        args = new object[] { blueprint, repeated, null };
        Require((bool)avoid.Invoke(null, args) && Newtonsoft.Json.JsonConvert.SerializeObject(route) == Newtonsoft.Json.JsonConvert.SerializeObject(repeated), "Routing is not repeatable.");
        var report = new System.Text.StringBuilder(DateTime.UtcNow.ToString("O") + "\nPASS: continuous reserve clearance, blocked steering-point replacement, destination preservation, deterministic detour.\n");

        MethodInfo read = typeof(YQSavedWorldPlacementReviewV2).GetMethod("ReadPlan", PrivateStatic);
        var inputs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        object[] readArgs = { inputs, null };
        var saved = (GeneratedWorldPlanRecord)read.Invoke(null, readArgs);
        string original = Newtonsoft.Json.JsonConvert.SerializeObject(saved.spatialPlanV2);
        var draft = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialWorldPlanV2Record>(original);
        report.AppendLine("Source=" + readArgs[1] + " seed=" + saved.worldSeed);
        foreach (var candidateRoute in draft.blueprint.routes)
        {
            string before = Newtonsoft.Json.JsonConvert.SerializeObject(candidateRoute.controlPoints);
            args = new object[] { draft.blueprint, candidateRoute, null };
            bool routed = (bool)avoid.Invoke(null, args);
            bool changed = before != Newtonsoft.Json.JsonConvert.SerializeObject(candidateRoute.controlPoints);
            report.AppendLine(candidateRoute.routeId + " routed=" + routed + " changed=" + changed + " reason=" + args[2]);
        }
        Require(original == Newtonsoft.Json.JsonConvert.SerializeObject(saved.spatialPlanV2), "Source plan changed during draft review.");
        // note: A route draft is not accepted or published until crossing/access/grade validation and a complete materialization review succeed.
        draft.acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft;
        draft.validatedContentHash = string.Empty;
        var validation = YQSpatialBlueprintValidatorV2.Validate(draft);
        report.AppendLine("DraftValidation=" + validation.Accepted + " errors=" + string.Join("; ", validation.Errors));
        if (validation.Accepted)
        {
            // note: Validate a detached accepted envelope through the normal hash/version/materialization contract; do not publish it to the source save.
            draft.contentHash = YQSpatialBlueprintHasherV2.ComputeContentHash(draft);
            draft.validatedContentHash = draft.contentHash;
            draft.acceptanceState = GeneratedSpatialPlanAcceptanceState.Accepted;
            bool prepared = YQSpatialMaterializationCompilerV2.TryPrepareCandidate(saved, draft, out _, out string failure);
            report.AppendLine("DraftMaterialization=" + prepared + " reason=" + failure);
            if (prepared)
            {
                // note: Prepare exact replacement artifacts for matching snapshots, leaving external saves untouched until an explicit file publication step.
                var publications = new List<object>();
                foreach (var input in inputs)
                {
                    if (Path.GetFileName(input.Key) != "world_state.json") continue;
                    byte[] sourceBytes = File.ReadAllBytes(input.Key);
                    var envelope = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(sourceBytes).TrimStart('\uFEFF'));
                    var spatial = envelope["generatedWorldPlan"]?["spatialPlanV2"];
                    if (spatial == null || spatial.ToObject<GeneratedSpatialWorldPlanV2Record>().contentHash != saved.spatialPlanV2.contentHash) continue;
                    string expected;
                    using (var sha = System.Security.Cryptography.SHA256.Create())
                        expected = BitConverter.ToString(sha.ComputeHash(sourceBytes)).Replace("-", "");
                    envelope["generatedWorldPlan"]["spatialPlanV2"] = Newtonsoft.Json.Linq.JToken.FromObject(draft);
                    string artifact = Path.GetFullPath("Logs/WorldDressing-20260910/repaired-world-" + publications.Count + ".json");
                    File.WriteAllText(artifact, envelope.ToString(Newtonsoft.Json.Formatting.Indented));
                    publications.Add(new { source = input.Key, expectedSha256 = expected, replacement = artifact });
                }
                File.WriteAllText("Logs/WorldDressing-20260910/route-repair-publication.json",
                    Newtonsoft.Json.JsonConvert.SerializeObject(publications, Newtonsoft.Json.Formatting.Indented));
            }
            draft.acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft;
            draft.validatedContentHash = string.Empty;
        }
        File.WriteAllText("Logs/WorldDressing-20260910/route-reserve-draft.json", Newtonsoft.Json.JsonConvert.SerializeObject(draft, Newtonsoft.Json.Formatting.Indented));
        File.WriteAllText("Logs/WorldDressing-20260910/route-reserve-verification.txt", report.ToString());
    }

    private static void InspectEncounter()
    {
        // note: Observe the retained live site directly, without relocating actors, editing a save, or treating a prepared root as loaded geometry.
        var report = new System.Text.StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
        Type owner = typeof(YQCompiledWorldSiteInstance);
        BindingFlags instanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
        MethodInfo resolver = owner.GetMethod("TryResolveRolePosition", instanceFields);
        foreach (var site in UnityEngine.Object.FindObjectsByType<YQCompiledWorldSiteInstance>(FindObjectsSortMode.None))
        {
            if (!site.name.Contains("shadow_fleet")) continue;
            report.AppendLine(site.name + " position=" + site.transform.position + " loaded=" + owner.GetField("loaded", instanceFields).GetValue(site));
            var manifest = (YQReviewedSemanticSiteManifest)owner.GetField("manifest", instanceFields).GetValue(site);
            report.AppendLine("Manifest=" + (manifest != null ? manifest.name : "null") + " authoredOrigin=" + owner.GetField("authoredOrigin", instanceFields).GetValue(site));
            if (manifest != null)
                foreach (var zone in manifest.Zones)
                    if (zone != null) report.AppendLine("Zone=" + zone.stableId + " origin=" + zone.authoredSourceOrigin + " center=" + zone.localBoundsCenter + " size=" + zone.localBoundsSize);
            int supported = 0;
            for (int attempt = 0; attempt < 24; attempt++)
            {
                string seed = "4887e372|encampment_leader|npc_97a709d9" + (attempt == 0 ? "" : "|route_clear|" + attempt);
                object[] args = { "hostile leader boss", seed, 0, Vector3.zero, null };
                bool ready = (bool)resolver.Invoke(site, args);
                if (ready) supported++;
                report.AppendLine("Attempt=" + attempt + " supported=" + ready + " position=" + args[3]);
            }
            report.AppendLine("SupportedAttempts=" + supported);
        }
        File.WriteAllText("Logs/YQEncounterPlacementInspection.txt", report.ToString());
    }

    private static void RepairSavedRiver()
    {
        // note: Repair only the persisted river geometry in a detached copy, then require the normal validator/materializer before publication.
        MethodInfo read = typeof(YQSavedWorldPlacementReviewV2).GetMethod("ReadPlan", PrivateStatic);
        var inputs = new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        object[] readArgs = { inputs, null };
        var saved = (GeneratedWorldPlanRecord)read.Invoke(null, readArgs);
        if (saved == null || saved.spatialPlanV2 == null || saved.spatialPlanV2.blueprint == null)
            throw new InvalidOperationException("No saved spatial plan is available for river repair.");

        string original = Newtonsoft.Json.JsonConvert.SerializeObject(saved.spatialPlanV2);
        var draft = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedSpatialWorldPlanV2Record>(original);
        YQHydrologyFeatureV2 river = null;
        YQHydrologyFeatureV2 waterfall = null;
        for (int index = 0; index < draft.blueprint.hydrology.Count; index++)
        {
            YQHydrologyFeatureV2 feature = draft.blueprint.hydrology[index];
            if (feature == null) continue;
            if (feature.kind == YQHydrologyKindV2.River) river = feature;
            if (feature.kind == YQHydrologyKindV2.Waterfall) waterfall = feature;
        }
        if (river == null || river.controlPoints == null || river.controlPoints.Count < 3)
            throw new InvalidOperationException("Saved plan does not contain a repairable river.");

        var oldPoints = new List<YQBlueprintPointV2>(river.controlPoints.Count);
        for (int index = 0; index < river.controlPoints.Count; index++)
            oldPoints.Add(river.controlPoints[index]);
        float phase = Stable01(saved.worldSeed + "|river_meander_phase") * Mathf.PI * 2f;
        for (int index = 0; index < oldPoints.Count; index++)
        {
            float t = index / (float)(oldPoints.Count - 1);
            Vector2 before = new Vector2(oldPoints[Mathf.Max(0, index - 1)].x, oldPoints[Mathf.Max(0, index - 1)].z);
            Vector2 after = new Vector2(oldPoints[Mathf.Min(oldPoints.Count - 1, index + 1)].x, oldPoints[Mathf.Min(oldPoints.Count - 1, index + 1)].z);
            Vector2 tangent = after - before;
            if (tangent.sqrMagnitude < .0001f) tangent = Vector2.up;
            tangent.Normalize();
            Vector2 bankNormal = new Vector2(-tangent.y, tangent.x);
            float offset = Mathf.Sin(t * Mathf.PI * 2.1f + phase) * Mathf.Sin(t * Mathf.PI) * 8f;
            float width = Mathf.Clamp(Mathf.Lerp(5.5f, 10.5f, t) * Mathf.Lerp(.93f, 1.07f,
                Stable01(saved.worldSeed + "|river_width|" + index)), 5f, 12f);
            YQBlueprintPointV2 source = oldPoints[index];
            source.x += bankNormal.x * offset;
            source.z += bankNormal.y * offset;
            source.width = width;
            river.controlPoints[index] = source;
        }

        if (waterfall != null && waterfall.controlPoints != null)
        {
            // note: Keep the waterfall attached to the repaired river by remapping each endpoint to its nearest prior river sample.
            for (int index = 0; index < waterfall.controlPoints.Count; index++)
            {
                YQBlueprintPointV2 point = waterfall.controlPoints[index];
                int nearest = 0;
                float best = float.PositiveInfinity;
                for (int candidate = 0; candidate < oldPoints.Count; candidate++)
                {
                    float dx = point.x - oldPoints[candidate].x;
                    float dz = point.z - oldPoints[candidate].z;
                    float distance = dx * dx + dz * dz;
                    if (distance < best) { best = distance; nearest = candidate; }
                }
                YQBlueprintPointV2 repaired = river.controlPoints[nearest];
                repaired.normalizedElevation = point.normalizedElevation;
                waterfall.controlPoints[index] = repaired;
            }
        }

        draft.acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft;
        draft.validatedContentHash = string.Empty;
        var validation = YQSpatialBlueprintValidatorV2.Validate(draft);
        var report = new System.Text.StringBuilder(DateTime.UtcNow.ToString("O") + "\n");
        report.AppendLine("Source=" + readArgs[1] + " seed=" + saved.worldSeed);
        report.AppendLine("RiverPoints=" + river.controlPoints.Count + " amplitude=8m");
        report.AppendLine("Validation=" + validation.Accepted + " errors=" + string.Join("; ", validation.Errors));
        if (!validation.Accepted)
            throw new InvalidOperationException("Repaired river failed validation: " + string.Join("; ", validation.Errors));

        draft.contentHash = YQSpatialBlueprintHasherV2.ComputeContentHash(draft);
        draft.validatedContentHash = draft.contentHash;
        draft.acceptanceState = GeneratedSpatialPlanAcceptanceState.Accepted;
        bool prepared = YQSpatialMaterializationCompilerV2.TryPrepareCandidate(saved, draft, out _, out string materializationFailure);
        report.AppendLine("Materialization=" + prepared + " reason=" + materializationFailure);
        if (!prepared)
            throw new InvalidOperationException("Repaired river failed materialization: " + materializationFailure);

        var publications = new List<object>();
        foreach (var input in inputs)
        {
            if (Path.GetFileName(input.Key) != "world_state.json") continue;
            byte[] sourceBytes = File.ReadAllBytes(input.Key);
            var envelope = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(sourceBytes).TrimStart('\uFEFF'));
            var spatial = envelope["generatedWorldPlan"]?["spatialPlanV2"];
            if (spatial == null || spatial.ToObject<GeneratedSpatialWorldPlanV2Record>().contentHash != saved.spatialPlanV2.contentHash) continue;
            string expected;
            using (var sha = System.Security.Cryptography.SHA256.Create())
                expected = BitConverter.ToString(sha.ComputeHash(sourceBytes)).Replace("-", "");
            envelope["generatedWorldPlan"]["spatialPlanV2"] = Newtonsoft.Json.Linq.JToken.FromObject(draft);
            string artifact = Path.GetFullPath("Logs/WorldDressing-20260910/river-repaired-world-" + publications.Count + ".json");
            File.WriteAllText(artifact, envelope.ToString(Newtonsoft.Json.Formatting.Indented));
            publications.Add(new { source = input.Key, expectedSha256 = expected, replacement = artifact });
        }
        File.WriteAllText("Logs/WorldDressing-20260910/river-repair-publication.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(publications, Newtonsoft.Json.Formatting.Indented));
        draft.acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft;
        draft.validatedContentHash = string.Empty;
        File.WriteAllText("Logs/WorldDressing-20260910/river-repair-verification.txt", report.ToString());
    }

    private static void Verify()
    {
        Type owner = typeof(YQGeneratedWorldEnvironment);
        Type pathType = owner.GetNestedType("LivedPathSegment", BindingFlags.NonPublic);
        object path = Activator.CreateInstance(pathType, true);
        pathType.GetField("start").SetValue(path, Vector2.zero);
        pathType.GetField("end").SetValue(path, new Vector2(100f, 0f));
        pathType.GetField("curveAmplitude").SetValue(path, 12f);
        pathType.GetField("curvePhase").SetValue(path, 0.7f);
        MethodInfo centerMethod = owner.GetMethod("ResolveLivedPathCenter", PrivateStatic);
        MethodInfo vergeMethod = owner.GetMethod("ResolveRoadsideDressingPoint", PrivateStatic);
        const float along = 0.35f;
        Vector2 center = (Vector2)centerMethod.Invoke(null, new object[] { path, along });
        Vector2 before = (Vector2)centerMethod.Invoke(null, new object[] { path, along - .01f });
        Vector2 after = (Vector2)centerMethod.Invoke(null, new object[] { path, along + .01f });
        Vector2 left = (Vector2)vergeMethod.Invoke(null, new object[] { path, along, 8f });
        Vector2 right = (Vector2)vergeMethod.Invoke(null, new object[] { path, along, -8f });
        Require(Mathf.Abs(center.y) > 1f, "Fixture must differ from a straight road.");
        Require(Vector2.Distance((left + right) * .5f, center) < .001f, "Verges do not follow curve.");
        Require(Mathf.Abs(Vector2.Dot(left - center, (after - before).normalized)) < .001f, "Verge is not perpendicular to local road tangent.");
        Require(Mathf.Abs(Vector2.Distance(left, center) - 8f) < .001f, "Incorrect verge width.");

        // note: Rotated non-circular lakes expose accidental river-width offsets and uniform unseeded rings.
        Vector2 axis = new Vector2(.6f, .8f);
        // note: The runtime descriptor constructor is internal; tests in the Editor assembly use reflection without widening its public API.
        var basin = (YQGeneratedWorldTerrain.MacroWaterBasinDescriptor)Activator.CreateInstance(
            typeof(YQGeneratedWorldTerrain.MacroWaterBasinDescriptor), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { new Vector3(70f, 4f, -30f), axis, new Vector2(-axis.y, axis.x), 70f, 23f, 4f }, null);
        MethodInfo areaMethod = owner.GetMethod("AppendAreaShorelineCandidates", PrivateStatic);
        var first = new List<Vector3>();
        var repeat = new List<Vector3>();
        var different = new List<Vector3>();
        areaMethod.Invoke(null, new object[] { basin, "seed-a", first });
        areaMethod.Invoke(null, new object[] { basin, "seed-a", repeat });
        areaMethod.Invoke(null, new object[] { basin, "seed-b", different });
        Require(first.Count == 12 && repeat.Count == 12 && different.Count == 12, "Missing lake candidates.");
        bool changed = false;
        for (int index = 0; index < first.Count; index++)
        {
            Require(first[index] == repeat[index], "Same seed changed placement.");
            changed |= first[index] != different[index];
            Vector2 delta = new Vector2(first[index].x - 70f, first[index].z + 30f);
            float x = Vector2.Dot(delta, axis) / 70f;
            float z = Vector2.Dot(delta, new Vector2(-axis.y, axis.x)) / 23f;
            Require(x * x + z * z > 1f, "Shoreline candidate inside lake.");
        }
        Require(changed, "Different seeds repeat the same lake dressing.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static float Stable01(string value)
    {
        // note: Keep editor-only repair randomness identical across runs without reaching into the runtime compiler's internal helper.
        unchecked
        {
            uint hash = 2166136261;
            string text = value ?? string.Empty;
            for (int index = 0; index < text.Length; index++)
            {
                hash ^= text[index];
                hash *= 16777619;
            }
            return (hash & 0x00FFFFFF) / 16777215f;
        }
    }
}
