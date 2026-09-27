using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// note: This is a read-only saved-world diagnostic. No profile recovery, regeneration, contract approval or active generator change is performed.
public static class YQSavedWorldPlacementReviewV2
{
    private const string Folder = "Assets/Assets/GeneratedAssets/WorldAssemblies/MedievalVikingVillage/V2EntranceCandidates/";
    private const string ReportPath = "Logs/V2_SavedWorldPlacement.md";
    private const string RepairReportPath = "Logs/V2_SavedPlanRepairProposal.md";
    private const string RepairJsonPath = "Logs/V2_SavedPlanRepairProposal.json";
    private sealed class PlayerStamp { public string playerId = string.Empty; public long lastUpdatedUnix = 0; }
    private sealed class WorldEnvelope { public GeneratedWorldPlanRecord generatedWorldPlan = null; }
    private sealed class Candidate
    {
        public string id;
        public GameObject prefab;
        public string[] doors;
        public Vector3[] contacts;
        public float radius;
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Review Saved World Candidate Placement")]
    public static void Review()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before saved-world review.");
        var evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        GeneratedWorldPlanRecord plan = ReadPlan(evidence, out string source);
        var report = new StringBuilder("# Saved V2 candidate placement review\n\n");
        report.AppendLine("Input: " + source + ". Saves are read directly without invoking recovery or generation.");
        if (!YQGeneratedWorldTerrain.TryCreateV2HeightSampler(plan, out var heights, out string failure))
        {
            report.AppendLine("\nBLOCKED: the saved V2 artifact did not pass accepted-data validation. " + failure);
            FinishBlockedReport(evidence, report, failure);
            return;
        }
        if (!YQSpatialMaterializationCompilerV2.TryPrepare(plan, out var materialization, out failure))
        {
            report.AppendLine("\nThe saved artifact passed schema, hash and accepted-data validation, but failed the existing runtime materialization gate.");
            report.AppendLine("Saved blueprint: sites=" + plan.spatialPlanV2.blueprint.sites.Count + "; routes=" + plan.spatialPlanV2.blueprint.routes.Count +
                "; water features=" + plan.spatialPlanV2.blueprint.hydrology.Count + ".");
            report.AppendLine("\nBLOCKED: " + failure);
            FinishBlockedReport(evidence, report, failure);
            return;
        }
        report.AppendLine("Accepted blueprint passed the existing version/hash/semantic validation. Sites=" + materialization.SiteCount +
            "; routes=" + materialization.RouteCount + "; water features=" + materialization.WaterCount + ".");
        report.AppendLine("This evaluates the two unapproved entrance candidates individually at saved settlement/hostile-site anchors. It does not select a functional settlement, move saved anchors, flatten whole sites or approve release.");
        report.AppendLine("Terrain uses the exact V2 synthesis on the production 2m lattice, before the runtime construction-pad prepass. These are prospective V2 results, not the currently active V1 world.");
        report.AppendLine("Neighbor site reservations and routes/water come from this saved blueprint. Within-site foundation contracts and complete cell composition still need review; no missing evidence is promoted to approval.\n");
        Candidate[] candidates = {
            LoadCandidate("yq_viking_district_central_village", new[] { "SM_House4_Door11" }),
            LoadCandidate("yq_viking_district_eastern_works", new[] { "SM_House4_Door6", "SM_House2_Door_64" })
        };
        int trials = 0, connected = 0;
        for (int index = 0; index < materialization.SiteCount; index++)
        {
            var site = materialization.GetSite(index);
            if (site.kind != YQSiteKindV2.Settlement && site.kind != YQSiteKindV2.HostileSite) continue;
            if (trials + candidates.Length > 24) throw new InvalidOperationException("Saved-world placement exceeds the 24-trial review budget.");
            report.AppendLine("## " + site.siteId + " (" + site.kind + ")\n");
            report.AppendLine("Anchor: (" + site.x.ToString("F2") + ", " + site.z.ToString("F2") + "); heading=" + site.headingDegrees.ToString("F1") +
                "; reserved radius=" + site.reservedRadius.ToString("F2") + "m; required functions=" + site.RequiredFunctions.Count + ".\n");
            foreach (Candidate candidate in candidates)
            {
                trials++;
                report.AppendLine("### " + candidate.id + "\n");
                if (ReviewCandidate(candidate, site, plan.spatialPlanV2.blueprint, heights, report)) connected++;
                report.AppendLine();
            }
        }
        report.AppendLine("Geometric trials=" + trials + "; connected entrance trials=" + connected + ". None are approved for runtime use.");
        SaveReport(evidence, report);
        Debug.Log("[YQSavedPlacementV2] Saved-world review complete: " + trials + " trials, " + connected + " geometrically connected. " + ReportPath);
    }

    private static void FinishBlockedReport(Dictionary<string, string> evidence, StringBuilder report, string failure)
    {
        // note: An expected placement rejection is report evidence, not permission to substitute old data, weaken a gate or silently rebuild accepted content.
        report.AppendLine("\nNo candidate placement trials were run. The review did not bypass the gate, move sites, regenerate the accepted plan, change approval state or activate V2.");
        report.AppendLine("Next requirement: review an explicit saved-plan repair/migration before testing candidate placement; a compiled C# pass alone cannot repair this snapshot.");
        SaveReport(evidence, report);
        Debug.LogWarning("[YQSavedPlacementV2] BLOCKED before placement: " + failure + ". See " + ReportPath);
    }

    private static void SaveReport(Dictionary<string, string> evidence, StringBuilder report, string path = ReportPath)
    {
        // note: Concurrent gameplay/save writes invalidate this report; never restore or overwrite a user's changed snapshot.
        foreach (var input in evidence)
            if (Hash(File.ReadAllBytes(input.Key)) != input.Value) throw new InvalidOperationException("Saved input changed during review; rerun against a stable snapshot.");
        report.AppendLine("All input file hashes remained unchanged. Candidate assets and runtime configuration were not written.");
        Directory.CreateDirectory("Logs");
        File.WriteAllText(path, report.ToString());
    }

    private sealed class RepairProposal
    {
        // note: Proposal metadata separates each gate so a failed compile, unsafe diff or skipped materialization cannot look migration-ready.
        public string status = "Blocked_NotEvaluated";
        public string sourceContentHash;
        public string sourceValidationVersion;
        public string reviewedValidationVersion = GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion;
        public int sourceValidationErrorCount;
        public int sourceWaterAccessErrorCount;
        public bool compilerAccepted;
        public string compilerFailure;
        public bool nonSiteScopeReviewAvailable;
        public bool nonSiteScopePassed;
        public bool blueprintIdentityUnchanged;
        public bool regionsUnchanged;
        public bool terrainFieldsUnchanged;
        public bool foundationalTerrainUnchanged;
        public bool hydrologyUnchanged;
        public bool routesUnchanged;
        public bool routeContractsUnchanged;
        public bool relationshipsUnchanged;
        public bool foundationalRelationshipsUnchanged;
        public bool metricsUnchanged;
        public bool metricsCompatible;
        public bool siteContractsCompatible;
        public float maximumSiteMovementMeters;
        public float maximumPermittedSiteMovementMeters;
        public bool movementWithinReviewLimit;
        public bool runtimeMaterializationAttempted;
        public bool runtimeMaterializationPassed;
        public string materializationFailure;
        public string failure;
        public GeneratedSpatialWorldPlanV2Record draft;
    }

    [MenuItem("Tools/YourQuest/AAA World Generation/V2/Propose Saved Water Access Repair")]
    public static void ProposeSavedRepair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Leave Play Mode before proposing saved-world repair.");
        var evidence = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        GeneratedWorldPlanRecord original = ReadPlan(evidence, out string source);
        var report = new StringBuilder("# Saved V2 water-access repair proposal\n\n");
        report.AppendLine("Source: " + source + ". This proposal is NOT APPLIED and has no automatic apply path.");
        var saved = original.spatialPlanV2;
        if (saved == null || saved.acceptanceState != GeneratedSpatialPlanAcceptanceState.Accepted ||
            saved.schemaVersion != GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion ||
            (saved.generationVersion != GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion &&
             !YQSpatialPlanVersionRouter.IsKnownLegacyNonAuthoritativeArtifact(saved)) ||
            (saved.validationVersion != GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion && saved.validationVersion != GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion) ||
            saved.worldSeed != original.worldSeed || saved.semanticFingerprint != original.spatialPlan?.semanticFingerprint ||
            string.IsNullOrEmpty(saved.contentHash) || saved.contentHash != saved.validatedContentHash ||
            saved.contentHash != YQSpatialBlueprintHasherV2.ComputeContentHash(saved))
            // note: The read-only proposal accepts only the exact shadow-era generation tuple in addition to the current contract; unknown or future artifacts remain blocked.
            throw new InvalidOperationException("Repair source identity/hash is invalid; no proposal was generated.");
        var validation = YQSpatialBlueprintValidatorV2.Validate(saved);
        int waterAccessErrorCount = 0;
        var failingWaterSiteIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string error in validation.Errors)
        {
            if (!error.StartsWith(YQSpatialBlueprintFailureV2.InsufficientWaterAccess + ":", StringComparison.Ordinal))
                throw new InvalidOperationException("Saved-plan errors exceed this water-repair proposal's scope: " + error);
            waterAccessErrorCount++;
            string payload = error.Substring(
                (YQSpatialBlueprintFailureV2.InsufficientWaterAccess + ":").Length);
            int separator = payload.IndexOf('|');
            string siteId = separator >= 0
                ? payload.Substring(0, separator)
                : payload;
            if (!string.IsNullOrWhiteSpace(siteId))
                failingWaterSiteIds.Add(siteId);
        }
        // note: A currently valid artifact is not eligible for regeneration through a repair-only diagnostic.
        if (waterAccessErrorCount == 0)
            throw new InvalidOperationException("The saved plan has no water-access validation error; no repair proposal was generated.");
        if (saved.blueprint.sites.Count > 32) throw new InvalidOperationException("Repair source exceeds the 32-site review budget.");
        report.AppendLine("Original acceptance errors under current checks: " + validation.Errors.Count + ".");
        foreach (string error in validation.Errors) report.AppendLine("- " + error);

        // note: Reuse the persisted semantic records and seed in a detached copy. No LLM calls, narrative regeneration or assignment back into the source save occurs.
        GeneratedWorldPlanRecord copy = JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(JsonConvert.SerializeObject(original),
            new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None });
        copy.spatialPlanV2 = null;
        bool compiled = YQSpatialBlueprintCompilerV2.TryCompile(copy, out var candidate, out string compileFailure);

        // note: Foundations must remain byte-identical; routes, site reserves, their relationships and metrics are expected deterministic dependents of moved anchors.
        bool scopeReviewAvailable = candidate?.blueprint != null;
        bool blueprintIdentityUnchanged = scopeReviewAvailable &&
            saved.blueprint.worldSize.Equals(candidate.blueprint.worldSize) &&
            string.Equals(saved.blueprint.originSiteId, candidate.blueprint.originSiteId, StringComparison.Ordinal);
        bool regionsUnchanged = scopeReviewAvailable && SameJson(saved.blueprint.regions, candidate.blueprint.regions);
        bool terrainFieldsUnchanged = scopeReviewAvailable && SameJson(saved.blueprint.terrainFields, candidate.blueprint.terrainFields);
        bool foundationalTerrainUnchanged = scopeReviewAvailable && SameFoundationalTerrain(saved.blueprint, candidate.blueprint);
        bool hydrologyUnchanged = scopeReviewAvailable && SameJson(saved.blueprint.hydrology, candidate.blueprint.hydrology);
        bool routesUnchanged = scopeReviewAvailable && SameJson(saved.blueprint.routes, candidate.blueprint.routes);
        bool routeContractsUnchanged = scopeReviewAvailable && SameRouteContracts(saved.blueprint, candidate.blueprint);
        bool relationshipsUnchanged = scopeReviewAvailable && SameJson(saved.blueprint.relationships, candidate.blueprint.relationships);
        bool foundationalRelationshipsUnchanged = scopeReviewAvailable && SameFoundationalRelationships(saved.blueprint, candidate.blueprint);
        bool metricsUnchanged = scopeReviewAvailable && SameJson(saved.blueprint.metrics, candidate.blueprint.metrics);
        bool metricsCompatible = scopeReviewAvailable && SameMetricContracts(saved.blueprint.metrics, candidate.blueprint.metrics);
        float maximumSiteMovement = 0f;
        float maximumPermittedMovement = 0f;
        bool movementWithinReviewLimit = false;
        bool siteContractsCompatible = scopeReviewAvailable && TryReviewSiteContracts(
            saved.blueprint, candidate.blueprint, failingWaterSiteIds,
            out maximumSiteMovement,
            out maximumPermittedMovement,
            out movementWithinReviewLimit);
        bool nonSiteScopePassed = scopeReviewAvailable && blueprintIdentityUnchanged &&
                                  regionsUnchanged && foundationalTerrainUnchanged &&
                                  hydrologyUnchanged && foundationalRelationshipsUnchanged &&
                                  siteContractsCompatible && routeContractsUnchanged &&
                                  relationshipsUnchanged && metricsCompatible;

        var changedNonSiteSections = new List<string>();
        if (scopeReviewAvailable)
        {
            if (!blueprintIdentityUnchanged) changedNonSiteSections.Add("blueprint identity/world size");
            if (!regionsUnchanged) changedNonSiteSections.Add("regions");
            if (!foundationalTerrainUnchanged) changedNonSiteSections.Add("foundational terrain fields");
            if (!hydrologyUnchanged) changedNonSiteSections.Add("hydrology");
            if (!foundationalRelationshipsUnchanged) changedNonSiteSections.Add("foundational relationships");
            if (!siteContractsCompatible) changedNonSiteSections.Add("site identities/contracts");
            if (!routeContractsUnchanged) changedNonSiteSections.Add("route topology/contracts or crossing membership");
            if (!relationshipsUnchanged) changedNonSiteSections.Add("relationship identity/contracts");
            if (!metricsCompatible) changedNonSiteSections.Add("metric counts/contracts");
        }

        bool materialized = false;
        // note: Materialization is a read-only diagnostic on the detached candidate, even when migration scope remains blocked.
        bool materializationAttempted = compiled;
        string materializationFailure = string.Empty;
        if (materializationAttempted)
        {
            copy.spatialPlanV2 = candidate;
            materialized = YQSpatialMaterializationCompilerV2.TryPrepare(copy, out _, out materializationFailure);
        }

        // note: Every failed gate produces an explicit blocked status; even a materializable candidate remains blocked when relocation exceeds the review bound.
        string status;
        var blockers = new List<string>();
        if (!compiled) blockers.Add("Compiler rejected the detached candidate: " + (compileFailure ?? string.Empty));
        if (!scopeReviewAvailable) blockers.Add("The detached compiler did not return a blueprint for scope review.");
        else if (!nonSiteScopePassed) blockers.Add("Detached recompilation changed immutable sections outside this repair's scope: " +
            string.Join(", ", changedNonSiteSections) + ".");
        if (nonSiteScopePassed && !movementWithinReviewLimit)
            blockers.Add("Detached recompilation moves at least one site " +
                maximumSiteMovement.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                "m, beyond its original local terrain-search allowance (largest applicable allowance " +
                maximumPermittedMovement.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "m)." );
        if (materializationAttempted && !materialized)
            blockers.Add("Runtime materialization rejected the detached candidate: " + (materializationFailure ?? string.Empty));
        if (!scopeReviewAvailable || !nonSiteScopePassed)
            status = "Blocked_UnsafeScope_NotApplied";
        else if (!compiled)
            status = "Blocked_CompilationFailed_NotApplied";
        else if (!materialized)
            status = "Blocked_MaterializationFailed_NotApplied";
        else if (!movementWithinReviewLimit)
            status = "Blocked_ExcessiveRelocation_NotApplied";
        else
            status = "PendingDerivedMigrationReview_NotApplied";
        string failure = string.Join(" | ", blockers);

        var proposal = new RepairProposal {
            status = status,
            sourceContentHash = saved.contentHash,
            sourceValidationVersion = saved.validationVersion,
            sourceValidationErrorCount = validation.Errors.Count,
            sourceWaterAccessErrorCount = waterAccessErrorCount,
            compilerAccepted = compiled,
            compilerFailure = compileFailure ?? string.Empty,
            nonSiteScopeReviewAvailable = scopeReviewAvailable,
            nonSiteScopePassed = nonSiteScopePassed,
            blueprintIdentityUnchanged = blueprintIdentityUnchanged,
            regionsUnchanged = regionsUnchanged,
            terrainFieldsUnchanged = terrainFieldsUnchanged,
            foundationalTerrainUnchanged = foundationalTerrainUnchanged,
            hydrologyUnchanged = hydrologyUnchanged,
            routesUnchanged = routesUnchanged,
            routeContractsUnchanged = routeContractsUnchanged,
            relationshipsUnchanged = relationshipsUnchanged,
            foundationalRelationshipsUnchanged = foundationalRelationshipsUnchanged,
            metricsUnchanged = metricsUnchanged,
            metricsCompatible = metricsCompatible,
            siteContractsCompatible = siteContractsCompatible,
            maximumSiteMovementMeters = maximumSiteMovement,
            maximumPermittedSiteMovementMeters = maximumPermittedMovement,
            movementWithinReviewLimit = movementWithinReviewLimit,
            runtimeMaterializationAttempted = materializationAttempted,
            runtimeMaterializationPassed = materialized,
            materializationFailure = materializationFailure ?? string.Empty,
            failure = failure,
            draft = candidate
        };
        report.AppendLine("\nProposal status: " + status + ".");
        report.AppendLine("Current compiler accepted the detached proposal: " + compiled + ". Immutable scope passed: " + nonSiteScopePassed +
            ". Runtime materialization attempted: " + materializationAttempted + "; passed: " + materialized + ".");
        if (!string.IsNullOrEmpty(failure)) report.AppendLine("BLOCKED: " + failure);
        if (candidate?.blueprint != null)
        {
            report.AppendLine("\n## Scope of changes\n");
            report.AppendLine("Blueprint identity/world size unchanged: " + blueprintIdentityUnchanged + ".");
            report.AppendLine("Region records unchanged: " + regionsUnchanged + ".");
            report.AppendLine("Terrain-field records unchanged: " + terrainFieldsUnchanged + ".");
            report.AppendLine("Foundational terrain fields unchanged: " + foundationalTerrainUnchanged + ".");
            report.AppendLine("Water-feature records unchanged: " + hydrologyUnchanged + ".");
            report.AppendLine("Route records unchanged: " + routesUnchanged + ".");
            report.AppendLine("Route topology/contracts and crossing membership unchanged: " + routeContractsUnchanged + ".");
            report.AppendLine("Relationship records unchanged: " + relationshipsUnchanged + ".");
            report.AppendLine("Foundational relationships unchanged: " + foundationalRelationshipsUnchanged + ".");
            report.AppendLine("Metrics unchanged: " + metricsUnchanged + ".");
            report.AppendLine("Metric counts/contracts compatible: " + metricsCompatible + ".");
            report.AppendLine("Site identities/contracts compatible: " + siteContractsCompatible + ".");
            report.AppendLine("Maximum site movement: " + maximumSiteMovement.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) +
                "m (largest applicable original terrain-search allowance " +
                maximumPermittedMovement.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "m): " +
                movementWithinReviewLimit + ".");
            report.AppendLine("Sites: " + saved.blueprint.sites.Count + " → " + candidate.blueprint.sites.Count + "; routes: " + saved.blueprint.routes.Count +
                " → " + candidate.blueprint.routes.Count + ".\n");
            report.AppendLine("| Site | Movement (m) | Water access before → proposed | Requirements preserved |\n|---|---:|---|---|");
            foreach (var before in saved.blueprint.sites)
            {
                var after = candidate.blueprint.sites.Find(item => item != null && item.siteId == before.siteId);
                if (after == null) { report.AppendLine("| " + before.siteId + " | removed | — | False |"); continue; }
                YQSpatialSiteAccessV2.TryMeasureWaterAccess(saved.blueprint, before, out float oldAccess, out _, out _, out _);
                YQSpatialSiteAccessV2.TryMeasureWaterAccess(candidate.blueprint, after, out float newAccess, out _, out _, out _);
                float moved = Vector2.Distance(new Vector2(before.x, before.z), new Vector2(after.x, after.z));
                bool sameRequirements = before.minimumWaterAccess == after.minimumWaterAccess && before.minimumRouteAccess == after.minimumRouteAccess &&
                    before.reservedRadius == after.reservedRadius && SameJson(before.requiredFunctions, after.requiredFunctions);
                report.AppendLine("| " + before.siteId + " | " + moved.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " | " +
                    oldAccess.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) + " → " + newAccess.ToString("F3", System.Globalization.CultureInfo.InvariantCulture) +
                    " | " + sameRequirements + " |");
            }
            // note: A compiler-valid proposal is still a draft for human review; its serialized form cannot satisfy the accepted-artifact runtime gate.
            candidate.acceptanceState = GeneratedSpatialPlanAcceptanceState.Draft;
            candidate.validatedContentHash = string.Empty;
        }
        report.AppendLine("\n## Review required before any migration\n");
        report.AppendLine("Moving sites also changes their roads/reserves and can affect saved actors, quests and player locations. This tool does not migrate any of those records. Review the listed movement and terrain/route differences, then validate cell fit and a reversible migration before applying anything.");
        report.AppendLine("Only the proposal files in Logs are written. The source snapshot, active V1/V2 mode and candidate assets remain untouched.");
        SaveReport(evidence, report, RepairReportPath);
        File.WriteAllText(RepairJsonPath, JsonConvert.SerializeObject(proposal, Formatting.Indented));
        Debug.Log("[YQSavedRepairV2] Proposal written; status=" + status + "; compiled=" + compiled + "; scope=" + nonSiteScopePassed +
            "; materialization=" + materialized + ". " + RepairReportPath);
    }

    private static bool SameFoundationalTerrain(
        YQSpatialBlueprintV2 before,
        YQSpatialBlueprintV2 after)
    {
        if (before?.terrainFields == null || after?.terrainFields == null)
            return false;
        var beforeFoundation = new List<YQTerrainFieldV2>();
        var afterFoundation = new List<YQTerrainFieldV2>();
        for (int index = 0; index < before.terrainFields.Count; index++)
        {
            YQTerrainFieldV2 field = before.terrainFields[index];
            if (field == null || field.kind != YQTerrainFieldKindV2.SiteReserve)
                beforeFoundation.Add(field);
        }
        for (int index = 0; index < after.terrainFields.Count; index++)
        {
            YQTerrainFieldV2 field = after.terrainFields[index];
            if (field == null || field.kind != YQTerrainFieldKindV2.SiteReserve)
                afterFoundation.Add(field);
        }
        // note: SiteReserve fields follow site anchors; every geological/cave field remains immutable in this repair class.
        return SameJson(beforeFoundation, afterFoundation);
    }

    private static bool SameFoundationalRelationships(
        YQSpatialBlueprintV2 before,
        YQSpatialBlueprintV2 after)
    {
        if (before?.relationships == null || after?.relationships == null)
            return false;
        List<YQSpatialRelationshipV2> beforeFoundation =
            SelectFoundationalRelationships(before);
        List<YQSpatialRelationshipV2> afterFoundation =
            SelectFoundationalRelationships(after);
        // note: Hydrology/geology links are immutable; frontage, crossing, reserve and feature links are validated dependents of sites/routes.
        return SameJson(beforeFoundation, afterFoundation);
    }

    private static List<YQSpatialRelationshipV2> SelectFoundationalRelationships(
        YQSpatialBlueprintV2 blueprint)
    {
        var derivedEntityIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        if (blueprint.sites != null)
            for (int index = 0; index < blueprint.sites.Count; index++)
                if (!string.IsNullOrWhiteSpace(blueprint.sites[index]?.siteId))
                    derivedEntityIds.Add(blueprint.sites[index].siteId);
        if (blueprint.routes != null)
            for (int index = 0; index < blueprint.routes.Count; index++)
                if (!string.IsNullOrWhiteSpace(blueprint.routes[index]?.routeId))
                    derivedEntityIds.Add(blueprint.routes[index].routeId);
        if (blueprint.terrainFields != null)
        {
            for (int index = 0; index < blueprint.terrainFields.Count; index++)
            {
                YQTerrainFieldV2 field = blueprint.terrainFields[index];
                if (field != null && field.kind == YQTerrainFieldKindV2.SiteReserve &&
                    !string.IsNullOrWhiteSpace(field.fieldId))
                {
                    derivedEntityIds.Add(field.fieldId);
                }
            }
        }

        var result = new List<YQSpatialRelationshipV2>();
        for (int index = 0; index < blueprint.relationships.Count; index++)
        {
            YQSpatialRelationshipV2 relationship = blueprint.relationships[index];
            if (relationship == null ||
                (!derivedEntityIds.Contains(relationship.subjectId) &&
                 !derivedEntityIds.Contains(relationship.objectId)))
            {
                result.Add(relationship);
            }
        }
        return result;
    }

    private static bool SameRouteContracts(
        YQSpatialBlueprintV2 before,
        YQSpatialBlueprintV2 after)
    {
        if (before?.routes == null || after?.routes == null ||
            before.routes.Count != after.routes.Count)
            return false;
        var afterById = new Dictionary<string, YQRouteCorridorV2>(
            StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < after.routes.Count; index++)
        {
            YQRouteCorridorV2 route = after.routes[index];
            if (route == null || string.IsNullOrWhiteSpace(route.routeId) ||
                afterById.ContainsKey(route.routeId))
                return false;
            afterById.Add(route.routeId, route);
        }

        for (int index = 0; index < before.routes.Count; index++)
        {
            YQRouteCorridorV2 oldRoute = before.routes[index];
            if (oldRoute == null ||
                !afterById.TryGetValue(oldRoute.routeId, out YQRouteCorridorV2 newRoute) ||
                !string.Equals(oldRoute.sourceSemanticRouteId, newRoute.sourceSemanticRouteId, StringComparison.Ordinal) ||
                !string.Equals(oldRoute.fromSiteId, newRoute.fromSiteId, StringComparison.Ordinal) ||
                !string.Equals(oldRoute.toSiteId, newRoute.toSiteId, StringComparison.Ordinal) ||
                !string.Equals(oldRoute.parentRegionId, newRoute.parentRegionId, StringComparison.Ordinal) ||
                oldRoute.routeClass != newRoute.routeClass ||
                !oldRoute.width.Equals(newRoute.width) ||
                !oldRoute.shoulderWidth.Equals(newRoute.shoulderWidth) ||
                !oldRoute.maximumGradeDegrees.Equals(newRoute.maximumGradeDegrees) ||
                oldRoute.controlPoints == null || newRoute.controlPoints == null ||
                oldRoute.controlPoints.Count != newRoute.controlPoints.Count ||
                !SameJson(oldRoute.tags, newRoute.tags) ||
                !SameCrossingContracts(oldRoute.crossings, newRoute.crossings))
            {
                return false;
            }
        }

        // note: Route paths may bend with a local anchor, but endpoint topology, semantic identity, dimensions and crossing membership cannot change.
        return true;
    }

    private static bool SameCrossingContracts(
        List<YQRouteCrossingV2> before,
        List<YQRouteCrossingV2> after)
    {
        if (before == null || after == null || before.Count != after.Count)
            return false;
        var afterById = new Dictionary<string, YQRouteCrossingV2>(
            StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < after.Count; index++)
        {
            YQRouteCrossingV2 crossing = after[index];
            if (crossing == null || string.IsNullOrWhiteSpace(crossing.crossingId) ||
                afterById.ContainsKey(crossing.crossingId))
                return false;
            afterById.Add(crossing.crossingId, crossing);
        }
        for (int index = 0; index < before.Count; index++)
        {
            YQRouteCrossingV2 oldCrossing = before[index];
            if (oldCrossing == null ||
                !afterById.TryGetValue(oldCrossing.crossingId, out YQRouteCrossingV2 newCrossing) ||
                !string.Equals(oldCrossing.hydrologyId, newCrossing.hydrologyId, StringComparison.Ordinal) ||
                oldCrossing.kind != newCrossing.kind ||
                !oldCrossing.requiredSpan.Equals(newCrossing.requiredSpan))
                return false;
        }
        return true;
    }

    private static bool SameMetricContracts(
        YQSpatialBlueprintMetricsV2 before,
        YQSpatialBlueprintMetricsV2 after)
    {
        if (before == null || after == null)
            return false;
        // note: Only primary-route length may follow local route geometry; every count/connectivity/reserve metric stays invariant.
        return before.regionCount == after.regionCount &&
               before.terrainFieldCount == after.terrainFieldCount &&
               before.hydrologyFeatureCount == after.hydrologyFeatureCount &&
               before.siteCount == after.siteCount &&
               before.routeCount == after.routeCount &&
               before.crossingCount == after.crossingCount &&
               before.relationshipCount == after.relationshipCount &&
               before.connectedSettlementCount == after.connectedSettlementCount &&
               before.reservedWorldFraction.Equals(after.reservedWorldFraction);
    }

    private static bool TryReviewSiteContracts(
        YQSpatialBlueprintV2 before,
        YQSpatialBlueprintV2 after,
        HashSet<string> failingWaterSiteIds,
        out float maximumMovement,
        out float maximumPermittedMovement,
        out bool movementWithinLocalSearch)
    {
        maximumMovement = 0f;
        maximumPermittedMovement = 0f;
        movementWithinLocalSearch = true;
        if (before?.sites == null || after?.sites == null ||
            failingWaterSiteIds == null ||
            before.sites.Count != after.sites.Count)
            return false;

        var afterById = new Dictionary<string, YQSiteAnchorV2>(
            StringComparer.OrdinalIgnoreCase);
        for (int index = 0; index < after.sites.Count; index++)
        {
            YQSiteAnchorV2 site = after.sites[index];
            if (site == null || string.IsNullOrWhiteSpace(site.siteId) ||
                !afterById.TryAdd(site.siteId, site))
                return false;
        }

        for (int index = 0; index < before.sites.Count; index++)
        {
            YQSiteAnchorV2 oldSite = before.sites[index];
            YQSiteAnchorV2 orderedNewSite = after.sites[index];
            if (oldSite == null || orderedNewSite == null ||
                !string.Equals(oldSite.siteId, orderedNewSite.siteId, StringComparison.Ordinal) ||
                !afterById.TryGetValue(oldSite.siteId, out YQSiteAnchorV2 newSite))
                return false;
            bool compatibleWaterRequirement =
                oldSite.minimumWaterAccess.Equals(newSite.minimumWaterAccess) ||
                (oldSite.kind == YQSiteKindV2.NaturalFeature &&
                 failingWaterSiteIds.Contains(oldSite.siteId) &&
                 newSite.minimumWaterAccess >= 0.9f &&
                 newSite.minimumWaterAccess <= oldSite.minimumWaterAccess);
            if (!string.Equals(oldSite.sourceSemanticId, newSite.sourceSemanticId, StringComparison.Ordinal) ||
                !string.Equals(oldSite.parentRegionId, newSite.parentRegionId, StringComparison.Ordinal) ||
                oldSite.kind != newSite.kind ||
                oldSite.placementMode != newSite.placementMode ||
                !oldSite.reservedRadius.Equals(newSite.reservedRadius) ||
                !oldSite.terrainSearchRadius.Equals(newSite.terrainSearchRadius) ||
                !oldSite.maximumSlopeDegrees.Equals(newSite.maximumSlopeDegrees) ||
                !oldSite.minimumRouteAccess.Equals(newSite.minimumRouteAccess) ||
                !compatibleWaterRequirement ||
                oldSite.requiresTerrainConformance != newSite.requiresTerrainConformance ||
                oldSite.hiddenFromPrimaryRoute != newSite.hiddenFromPrimaryRoute ||
                !SameJson(oldSite.requiredFunctions, newSite.requiredFunctions) ||
                !SameJson(oldSite.tags, newSite.tags))
            {
                return false;
            }

            // note: Position and route-facing heading may change, but identity, function, reserve size and semantic requirements stay authoritative.
            float movement = Vector2.Distance(
                new Vector2(oldSite.x, oldSite.z),
                new Vector2(newSite.x, newSite.z));
            maximumMovement = Mathf.Max(maximumMovement, movement);
            if (movement > 0.001f)
            {
                float permitted = Mathf.Max(0f, oldSite.terrainSearchRadius);
                maximumPermittedMovement = Mathf.Max(
                    maximumPermittedMovement, permitted);
                if (!failingWaterSiteIds.Contains(oldSite.siteId) ||
                    movement > permitted + 0.001f)
                {
                    movementWithinLocalSearch = false;
                }
            }
        }
        return true;
    }

    private static bool SameJson(object a, object b) => string.Equals(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b), StringComparison.Ordinal);

    private static GeneratedWorldPlanRecord ReadPlan(Dictionary<string, string> evidence, out string source)
    {
        string root = Application.persistentDataPath;
        var manifest = Read<YQProfileSaveSystem.ProfileManifest>(Path.Combine(root, "Profiles", "profiles_manifest.json"), evidence);
        if (manifest?.profiles == null || manifest.profiles.Count > 128 || !Guid.TryParse(manifest.activeProfileId, out _))
            throw new InvalidOperationException("Active profile manifest is missing or invalid.");
        int matches = 0;
        foreach (var profile in manifest.profiles)
            if (profile != null && string.Equals(profile.profileId, manifest.activeProfileId, StringComparison.OrdinalIgnoreCase)) matches++;
        if (matches != 1) throw new InvalidOperationException("Active profile identity is missing or ambiguous.");
        string profileFolder = Path.Combine(root, "Profiles", manifest.activeProfileId);
        string profileWorld = Path.Combine(profileFolder, "world_state.json");
        var profileStamp = Read<PlayerStamp>(Path.Combine(profileFolder, "player_state.json"), evidence);
        if (profileStamp == null || !string.Equals(profileStamp.playerId, manifest.activeProfileId, StringComparison.OrdinalIgnoreCase) || !File.Exists(profileWorld))
            throw new InvalidOperationException("Profile snapshot is missing or belongs to a different player.");
        string sharedPlayer = Path.Combine(root, "player_state.json"), sharedWorld = Path.Combine(root, "world_state.json");
        string selected = profileWorld;
        source = "active profile snapshot";
        if (File.Exists(sharedPlayer) && File.Exists(sharedWorld))
        {
            var shared = Read<PlayerStamp>(sharedPlayer, evidence);
            if (ChooseShared(manifest.activeProfileId, profileStamp.playerId, profileStamp.lastUpdatedUnix, shared?.playerId, shared?.lastUpdatedUnix ?? 0))
            {
                selected = sharedWorld;
                source = "newer same-player shared snapshot (the existing recovery rule), read without copying it";
            }
        }
        var world = Read<WorldEnvelope>(selected, evidence);
        if (world?.generatedWorldPlan == null) throw new InvalidOperationException("Selected snapshot has no generated world plan.");
        return world.generatedWorldPlan;
    }

    private static T Read<T>(string path, Dictionary<string, string> evidence)
    {
        var file = new FileInfo(path);
        if (!file.Exists || file.Length <= 0 || file.Length > 32 * 1024 * 1024) throw new InvalidOperationException("Missing or oversized review input: " + file.Name);
        byte[] bytes = File.ReadAllBytes(path);
        evidence[path] = Hash(bytes);
        // note: Explicit plain JSON deserialization never honors type names or executes save-loader migration/recovery paths.
        try { return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None }); }
        catch (JsonException) { throw new InvalidOperationException("Could not parse review input: " + file.Name); }
    }

    private static string Hash(byte[] bytes)
    {
        using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes));
    }

    private static bool ChooseShared(string active, string profile, long profileTime, string shared, long sharedTime) =>
        !string.IsNullOrEmpty(active) && string.Equals(active, profile, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(active, shared, StringComparison.OrdinalIgnoreCase) && sharedTime > profileTime;

    private static Candidate LoadCandidate(string id, string[] doors)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + id + "_entrance_candidate.prefab");
        if (prefab == null) throw new InvalidOperationException("Missing entrance candidate: " + id);
        var candidate = new Candidate { id = id, prefab = prefab, doors = doors, contacts = new Vector3[doors.Length] };
        for (int index = 0; index < doors.Length; index++)
        {
            if (!YQCellDoorBindingsV2.TryResolveUniquePath(prefab.transform, ContactPath(doors[index]), out var contact))
                throw new InvalidOperationException("Candidate is missing its explicit terrain contact: " + doors[index]);
            candidate.contacts[index] = prefab.transform.InverseTransformPoint(contact.position);
        }
        // note: Measure actual candidate visuals, including added stairs; old source bounds do not include the repairs.
        foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            Bounds box = renderer.localBounds;
            Matrix4x4 toCell = prefab.transform.worldToLocalMatrix * renderer.localToWorldMatrix;
            for (int x = -1; x <= 1; x += 2) for (int y = -1; y <= 1; y += 2) for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = toCell.MultiplyPoint3x4(box.center + Vector3.Scale(box.extents, new Vector3(x, y, z)));
                candidate.radius = Mathf.Max(candidate.radius, new Vector2(corner.x, corner.z).magnitude);
            }
        }
        if (!Finite(candidate.radius) || candidate.radius <= 0) throw new InvalidOperationException("Candidate visual footprint is invalid.");
        return candidate;
    }

    private static bool ReviewCandidate(Candidate candidate, YQSpatialMaterializationSiteV2 site, YQSpatialBlueprintV2 blueprint,
        YQGeneratedWorldTerrain.V2HeightSampler sampler, StringBuilder report)
    {
        report.AppendLine("Measured candidate radius=" + candidate.radius.ToString("F2") + "m.");
        if (candidate.radius > site.reservedRadius) { report.AppendLine("Rejected: candidate exceeds the saved site reserve; no anchor relocation or reserve enlargement attempted."); return false; }
        var scene = EditorSceneManager.NewPreviewScene();
        TerrainData data = null;
        try
        {
            GameObject reviewRoot = new GameObject("SavedPlacementReview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(reviewRoot, scene);
            Quaternion heading = Quaternion.Euler(0, site.headingDegrees, 0);
            // note: The local 128m tile is aligned to the actual world lattice, not centered on a landing to make its interpolation easier.
            float tileX = Mathf.Floor(site.x / 2f) * 2f - 64, tileZ = Mathf.Floor(site.z / 2f) * 2f - 64;
            float halfWorld = YQGeneratedWorldTerrain.WorldSize * 0.5f;
            if (tileX < -halfWorld || tileZ < -halfWorld || tileX + 128 > halfWorld || tileZ + 128 > halfWorld)
            { report.AppendLine("Rejected: bounded review tile leaves the accepted world; edge-tile stitching has not been verified."); return false; }
            data = new TerrainData { heightmapResolution = 65, size = new Vector3(128, YQGeneratedWorldTerrain.TerrainHeight, 128) };
            float[,] heights = new float[65, 65];
            for (int z = 0; z < 65; z++) for (int x = 0; x < 65; x++) heights[z, x] = sampler.SampleNormalized(tileX + x * 2, tileZ + z * 2);
            data.SetHeights(0, 0, heights);
            GameObject ground = new GameObject("ReviewTerrain");
            ground.transform.SetParent(reviewRoot.transform, false);
            ground.transform.position = new Vector3(tileX, 0, tileZ);
            Terrain terrain = ground.AddComponent<Terrain>();
            terrain.terrainData = data;
            ground.AddComponent<TerrainCollider>().terrainData = data;
            float[] requiredOffsets = new float[candidate.contacts.Length];
            for (int index = 0; index < candidate.contacts.Length; index++)
            {
                Vector3 local = heading * candidate.contacts[index];
                Vector3 world = new Vector3(site.x + local.x, 0, site.z + local.z);
                if (!YQTerrainApproachV2.TrySampleTerrain(terrain, world, out float height))
                { report.AppendLine("Rejected: endpoint leaves the review tile."); return false; }
                requiredOffsets[index] = height - local.y;
                report.AppendLine("- " + candidate.doors[index] + ": unmodified ground=" + height.ToString("F3") + "m; exact-contact cell height=" + requiredOffsets[index].ToString("F3") + "m.");
            }
            if (!TryCommonDatum(requiredOffsets, 0.5f, 0.5f, out float datum, out float deficit))
            { report.AppendLine("Rejected: no single cell height can connect every entrance within 0.5m cut/fill. Height-interval deficit=" + deficit.ToString("F3") + "m. A reviewed terrace/stair redesign is needed; more flattening is not authorized."); return false; }
            report.AppendLine("Trial common cell height=" + datum.ToString("F3") + "m (entrance-derived trial, not an approved structural foundation datum).");
            if (!YQTerrainRepairProtectionV2.TryCreate(blueprint, site.siteId, Array.Empty<Bounds>(), out var protection, out string failure))
            { report.AppendLine("Protection data rejected: " + failure); return false; }
            GameObject cell = (GameObject)PrefabUtility.InstantiatePrefab(candidate.prefab, reviewRoot.transform);
            cell.name = "Cell";
            cell.transform.SetPositionAndRotation(new Vector3(site.x, datum, site.z), heading);
            bool allConnected = true;
            for (int index = 0; index < candidate.doors.Length; index++)
            {
                string doorName = candidate.doors[index];
                if (!YQCellDoorBindingsV2.TryResolveUniquePath(cell.transform, doorName, out Transform door)) throw new InvalidOperationException("Candidate door disappeared.");
                Vector3 start = cell.transform.TransformPoint(candidate.contacts[index]);
                // note: Synthetic approval permits geometric checking only; neither this temporary contract nor its transforms are saved.
                var contract = new YQTerrainApproachContractV2 { reviewState = YQSemanticSiteReviewState.Approved, authoredRouteVerified = true,
                    localStart = candidate.contacts[index], localOutward = cell.transform.InverseTransformDirection(door.right),
                    supportPath = "YQ_V2_EntranceRepairs/" + doorName + "_StairApproach/LowerLanding" };
                bool graded = YQTerrainApproachConstructionReviewV2.TryGradePreview(terrain, start, door.right, contract, protection, out failure);
                bool connected = graded && YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out failure);
                allConnected &= connected;
                report.AppendLine("- " + doorName + ": " + (connected ? "height connection fits" : "rejected — " + failure));
            }
            // note: Later patches can invalidate earlier contacts; recheck all endpoints after the complete trial rather than trusting per-step success.
            foreach (string doorName in candidate.doors)
            {
                YQCellDoorBindingsV2.TryResolveUniquePath(cell.transform, ContactPath(doorName), out Transform contact);
                YQCellDoorBindingsV2.TryResolveUniquePath(cell.transform, doorName, out Transform door);
                var contract = new YQTerrainApproachContractV2 { reviewState = YQSemanticSiteReviewState.Approved, authoredRouteVerified = true,
                    localStart = cell.transform.InverseTransformPoint(contact.position), localOutward = cell.transform.InverseTransformDirection(door.right),
                    supportPath = "YQ_V2_EntranceRepairs/" + doorName + "_StairApproach/LowerLanding" };
                if (!YQTerrainApproachV2.TryValidateReviewedConnection(cell.transform, contract, terrain, out failure))
                {
                    allConnected = false;
                    report.AppendLine("- Final combined recheck rejected " + doorName + ": " + failure);
                }
            }
            report.AppendLine(allConnected ? "All reviewed entrance height contacts fit this trial. Full structural support, collision, furnishing and visual approval remain unverified."
                : "Trial rejected; temporary terrain and instances discarded.");
            return allConnected;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static string ContactPath(string door) => "YQ_V2_EntranceRepairs/" + door + "_StairApproach/TerrainContact";
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool TryCommonDatum(float[] exactOffsets, float cut, float fill, out float datum, out float deficit)
    {
        datum = deficit = 0;
        if (exactOffsets == null || exactOffsets.Length == 0 || exactOffsets.Length > 32 || !Finite(cut) || !Finite(fill) || cut < 0 || fill < 0) return false;
        float min = float.NegativeInfinity, max = float.PositiveInfinity;
        foreach (float value in exactOffsets)
        {
            if (!Finite(value)) return false;
            min = Mathf.Max(min, value - cut);
            max = Mathf.Min(max, value + fill);
        }
        deficit = Mathf.Max(0, min - max);
        if (min > max) return false;
        datum = min + (max - min) * 0.5f;
        return Finite(datum);
    }

    public static string TestReviewInputsAndDatum()
    {
        if (!ChooseShared("a", "a", 1, "a", 2) || ChooseShared("a", "a", 2, "a", 1) ||
            ChooseShared("a", "a", 1, "b", 2) || ChooseShared("a", "b", 1, "a", 2)) return "Snapshot selection bypassed identity or timestamp checks.";
        if (!TryCommonDatum(new[] { 10f, 10.8f }, 0.5f, 0.5f, out float datum, out _) || Mathf.Abs(datum - 10.4f) > 0.001f)
            return "Compatible contacts lost their shared datum.";
        if (TryCommonDatum(new[] { 10f, 11.21f }, 0.5f, 0.5f, out _, out float deficit) || Mathf.Abs(deficit - 0.21f) > 0.001f)
            return "Incompatible cell contacts were silently averaged.";
        if (TryCommonDatum(new[] { float.NaN }, 0.5f, 0.5f, out _, out _) || TryCommonDatum(Array.Empty<float>(), 0.5f, 0.5f, out _, out _))
            return "Missing/nonfinite contact evidence passed.";
        return null;
    }

    public static void RunBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) Review();
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }

    public static void RunRepairBatch()
    {
        try
        {
            int failures = YQWorldGenerationV2ContractTests.RunTests(out int tested);
            Debug.Log("[YQWorldGenV2Tests] Tested " + tested + " contracts; failures=" + failures + ".");
            if (failures == 0) { Review(); ProposeSavedRepair(); }
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
