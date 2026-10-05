using System;
using System.Collections.Generic;
using UnityEngine;

public enum GeneratedSpatialPlanAcceptanceState
{
    None = 0,
    Draft = 1,
    Validated = 2,
    Accepted = 3,
    Rejected = 4
}

public enum YQSpatialPlanAuthority
{
    None = 0,
    PersistedV1 = 1,
    AcceptedV2 = 2
}

[Serializable]
public sealed class GeneratedSpatialWorldPlanV2Record
{
    public const string SupportedSchemaVersion =
        "spatial_world_plan_v2";

    // note: Topology 9 routes sustained river-following roads onto a deterministic bank and records each separate water entry so accepted trails remain materializable.
    public const string SupportedGenerationVersion =
        "spatial_world_plan_v2_topology_9";

    // note: Topology 8 is the immediately previous accepted contract; its riverside routes are rebuilt transactionally from the same canonical semantic plan.
    public const string ImmediatePreviousAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_8";

    // note: Topology 1 was produced only while V1 remained runtime authority; its older water/site rules may be replaced transactionally before a playable V2 build.
    public const string LegacyGenerationVersion =
        "spatial_world_plan_v2_topology_1";

    // note: Topology 7 remains an explicit migration predecessor after this routing correction.
    public const string PreviousAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_7";

    // note: Retain topology 6 as an explicit migration predecessor so protected saves remain readable without reopening legacy fallback.
    public const string OlderAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_6";

    // note: Keep topology 5 explicitly migratable because protected saves can still carry the older accepted envelope.
    public const string LegacyAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_5";

    // note: Keep topology 4 explicitly migratable because protected saves can still carry the earlier accepted envelope.
    public const string EarliestAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_4";

    // note: Keep topology 3 explicitly migratable because protected saves can still carry the earliest accepted envelope.
    public const string OldestAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_3";

    // note: Keep topology 2 explicitly migratable as accepted saves can still carry the pre-continuation-envelope contract.
    public const string AncientAcceptedGenerationVersion =
        "spatial_world_plan_v2_topology_2";

    public const string SupportedValidationVersion =
        "spatial_world_plan_v2_gate_2";

    // note: Older accepted snapshots remain readable, but never bypass the current checks; their stored flags/hashes are not rewritten on load.
    public const string LegacyValidationVersion =
        "spatial_world_plan_v2_gate_1";

    public string schemaVersion = SupportedSchemaVersion;
    public string generationVersion = string.Empty;
    public string worldSeed = string.Empty;
    public string semanticFingerprint = string.Empty;
    public string contentHash = string.Empty;
    public string validationVersion = string.Empty;
    public string validatedContentHash = string.Empty;
    public GeneratedSpatialPlanAcceptanceState acceptanceState =
        GeneratedSpatialPlanAcceptanceState.None;
    public List<string> validationErrors = new List<string>();
    public YQSpatialBlueprintV2 blueprint = new YQSpatialBlueprintV2();

    // note: Accepted frontier data has its own parent-bound checksum; absent data preserves the original opening artifact and old saves.
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public GeneratedSpatialContinuationV2Record acceptedContinuation;

    public void EnsureCollections()
    {
        validationErrors ??= new List<string>();
        blueprint ??= new YQSpatialBlueprintV2();
        blueprint.EnsureCollections();
    }
}

public enum YQSpatialContinuationStateV2
{
    None = 0,
    Staged = 1,
    Accepted = 2,
    Rejected = 3
}

public enum YQSpatialContinuationSourceV2
{
    None = 0,
    LlmProposal = 1,
    ExplicitFallback = 2
}

public enum YQSpatialContinuationProofKindV2
{
    None = 0,
    ContentProposal = 1,
    ProviderComposition = 2,
    TerrainConformance = 3,
    AccessAndHydrology = 4,
    PopulationAndInteractions = 5
}

public enum YQSpatialContinuationProofOutcomeV2
{
    Unverified = 0,
    Passed = 1,
    Rejected = 2
}

[Serializable]
public sealed class GeneratedSpatialContinuationV2Record
{
    public const string SupportedSchemaVersion = "spatial_continuation_v2_1";
    public const string SupportedGenerationVersion = "spatial_continuation_generation_1";
    public const string SupportedValidationVersion = "spatial_continuation_basic_gate_1";
    public const string SupportedHashAlgorithm = "sha256_canonical_unity_fields_v1";

    public string schemaVersion = SupportedSchemaVersion;
    public string generationVersion = SupportedGenerationVersion;
    public string validationVersion = SupportedValidationVersion;
    public string hashAlgorithm = SupportedHashAlgorithm;
    public string worldSeed;
    public string parentSpatialContentHash;
    public string parentMemberFootprintHash;
    public long revision;
    public YQSpatialContinuationStateV2 state = YQSpatialContinuationStateV2.Staged;
    public string contentHash;
    public string validatedContentHash;
    public List<string> validationErrors = new List<string>();
    // note: These mutable serialization containers are immutable by contract after owner acceptance and are rechecked against their content hashes.
    public List<GeneratedSpatialContinuationLocationV2Record> locations =
        new List<GeneratedSpatialContinuationLocationV2Record>();
}

[Serializable]
public sealed class GeneratedSpatialContinuationLocationV2Record
{
    public const string SupportedSchemaVersion = "spatial_continuation_location_v2_1";

    public string schemaVersion = SupportedSchemaVersion;
    public string contentId;
    public int blockX;
    public int blockZ;
    public string deterministicSeed;
    public long revision;
    public YQSpatialContinuationSourceV2 source;
    public string sourceContentId;
    public string sourceContentHash;
    public YQSpatialContinuationStateV2 state = YQSpatialContinuationStateV2.Staged;
    public string contentHash;
    public string validatedContentHash;
    public List<string> validationErrors = new List<string>();

    // note: The anchor is the sole site-position/member-footprint authority; semantic reservations and owner-cell projections are derived later.
    public YQSiteAnchorV2 anchor;
    public List<GeneratedSemanticEntranceRecord> entrances = new List<GeneratedSemanticEntranceRecord>();
    public GeneratedSettlementRecord settlement;
    public GeneratedEncampmentRecord encampment;
    public GeneratedPointOfInterestRecord pointOfInterest;
    public List<GeneratedNpcPlanRecord> population = new List<GeneratedNpcPlanRecord>();

    // note: Settlement/camp payloads retain their existing binding fields; only POIs lack those fields and use this shared binding slot.
    public string poiRuntimeSiteKitId;
    public string poiRuntimeSiteBindingVersion;
    public string compositionSeed;
    public string compositionGeometrySignature;
    public List<string> selectedSourceCellIds = new List<string>();
    // note: Settlement layouts remain on settlement.proceduralLayout; this slot retains exact geometry for payloads that have no existing layout owner.
    public YQProceduralSettlementLayoutRecord compositionLayout;
    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public GeneratedSpatialContinuationPhysicalContextV2Record physicalContext;
    public List<GeneratedSpatialContinuationProofV2Record> proofClaims =
        new List<GeneratedSpatialContinuationProofV2Record>();
}

[Serializable]
public sealed class GeneratedSpatialContinuationPhysicalContextV2Record
{
    public const string SupportedVersion = "frontier_physical_context_v1";
    public string version = SupportedVersion;
    // note: Coordinates and reserve radii remain on the anchor; pads carry only the engine-approved elevation and shoulder for each exact sector identity.
    public List<GeneratedSpatialContinuationTerrainPadV2Record> terrainPads =
        new List<GeneratedSpatialContinuationTerrainPadV2Record>();
    public List<YQRouteCorridorV2> routes = new List<YQRouteCorridorV2>();
}

[Serializable]
public sealed class GeneratedSpatialContinuationTerrainPadV2Record
{
    public string sectorId;
    public float elevationNormalized;
    public float shoulderWidth = 24f;
}

[Serializable]
public sealed class GeneratedSpatialContinuationProofV2Record
{
    public const string SupportedSchemaVersion = "spatial_continuation_proof_v2_1";

    public string schemaVersion = SupportedSchemaVersion;
    public YQSpatialContinuationProofKindV2 kind;
    public YQSpatialContinuationProofOutcomeV2 outcome;
    public string subjectSiteId;
    public string subjectPayloadHash;
    public string ownerValidationVersion;
    public string evidenceId;
    public string evidenceHash;
    // note: A persisted proof is a claim for the responsible owner to verify; structural/hash validation never authorizes promotion or publication.
}

public static class YQSpatialPlanVersionRouter
{
    private const int MaximumAcceptedRegions = 128;
    private const int MaximumAcceptedTerrainFields = 1024;
    private const int MaximumAcceptedHydrologyFeatures = 128;
    private const int MaximumAcceptedSites = 256;
    private const int MaximumAcceptedRoutes = 512;
    private const int MaximumAcceptedRelationships = 2048;
    private const long MaximumRouteHydrologySegmentChecks = 8000000L;

    public static bool TryResolveActive(
        GeneratedWorldPlanRecord plan,
        out YQSpatialPlanAuthority authority,
        out string reason)
    {
        return TryResolve(
            plan,
            YQWorldGenerationArchitecture.ActiveSpatialPlanningMode,
            out authority,
            out reason);
    }

    public static bool IsKnownLegacyNonAuthoritativeArtifact(
        GeneratedSpatialWorldPlanV2Record candidate)
    {
        if (candidate == null ||
            candidate.acceptanceState !=
            GeneratedSpatialPlanAcceptanceState.Accepted)
        {
            return false;
        }

        // note: Only known predecessor contracts are eligible for this explicit replacement; unknown, future, or current accepted artifacts remain immutable for explicit review.
        bool knownPredecessor = string.Equals(
                   candidate.schemaVersion,
                   GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                   StringComparison.Ordinal) &&
               (string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.ImmediatePreviousAcceptedGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.LegacyGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.PreviousAcceptedGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.OlderAcceptedGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.LegacyAcceptedGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.EarliestAcceptedGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.OldestAcceptedGenerationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.generationVersion,
                    GeneratedSpatialWorldPlanV2Record.AncientAcceptedGenerationVersion,
                    StringComparison.Ordinal)) &&
               (string.Equals(
                    candidate.validationVersion,
                    GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion,
                    StringComparison.Ordinal) ||
                string.Equals(
                    candidate.validationVersion,
                    GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                    StringComparison.Ordinal));
        return knownPredecessor;
    }

    private static bool HasCanonicalWorld(
        GeneratedWorldPlanRecord plan)
    {
        // note: An accepted V2 artifact or persisted semantic frontier is canonical generated state; it must not be silently reinterpreted by the legacy renderer.
        return plan != null &&
            ((plan.spatialPlanV2 != null &&
              plan.spatialPlanV2.acceptanceState == GeneratedSpatialPlanAcceptanceState.Accepted) ||
             (plan.semanticChunks != null && plan.semanticChunks.Count > 0 &&
              plan.spatialPlan != null &&
              !string.IsNullOrWhiteSpace(plan.spatialPlan.semanticFingerprint)));
    }

    public static bool TryResolve(
        GeneratedWorldPlanRecord plan,
        YQSpatialPlanningMode planningMode,
        out YQSpatialPlanAuthority authority,
        out string reason)
    {
        authority = YQSpatialPlanAuthority.None;
        reason = string.Empty;

        if (plan == null)
        {
            reason = "The persisted world plan is missing.";
            return false;
        }

        // note: Shadow compilation may create and validate V2 data, but only the explicit authoritative mode is allowed to consume it.
        if (planningMode == YQSpatialPlanningMode.PersistedV1 ||
            planningMode == YQSpatialPlanningMode.V2Shadow)
        {
            if (plan.spatialPlan == null)
            {
                reason = "The persisted V1 spatial plan is missing.";
                return false;
            }

            authority = YQSpatialPlanAuthority.PersistedV1;
            return true;
        }

        if (planningMode == YQSpatialPlanningMode.V2Preferred)
        {
            // note: Preferred mode selects one complete accepted authority; a canonical generated save fails closed instead of falling back to a misleading legacy physical presentation.
            if (TryValidateAcceptedV2(plan, out _))
            {
                authority = YQSpatialPlanAuthority.AcceptedV2;
                return true;
            }
            if (HasCanonicalWorld(plan))
            {
                reason = "The canonical generated world has no valid accepted V2 artifact; legacy fallback is disabled.";
                return false;
            }
            if (plan.spatialPlan == null)
            {
                reason = "Neither a valid accepted V2 artifact nor the persisted V1 spatial plan is available.";
                return false;
            }
            authority = YQSpatialPlanAuthority.PersistedV1;
            reason = string.Empty;
            return true;
        }

        if (planningMode != YQSpatialPlanningMode.V2Authoritative)
        {
            reason = "The requested spatial planning mode is unsupported.";
            return false;
        }

        if (!TryValidateAcceptedV2(plan, out reason))
            return false;

        // note: Invalid authoritative V2 data fails closed; this router never mixes V1 coordinates with V2 terrain or sites.
        authority = YQSpatialPlanAuthority.AcceptedV2;
        return true;
    }

    public static bool TryValidateAcceptedV2(
        GeneratedWorldPlanRecord plan,
        out string reason)
    {
        reason = string.Empty;

        if (plan == null)
        {
            reason = "The persisted world plan is missing.";
            return false;
        }

        GeneratedSpatialWorldPlanV2Record candidate =
            plan.spatialPlanV2;

        if (candidate == null)
        {
            reason = "V2 authority was requested without a compiled V2 artifact.";
            return false;
        }

        if (!string.Equals(
                candidate.schemaVersion,
                GeneratedSpatialWorldPlanV2Record.SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            reason = "The V2 spatial schema is unsupported.";
            return false;
        }

        if (!string.Equals(
                candidate.generationVersion,
                GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
                StringComparison.Ordinal))
        {
            reason = "The V2 spatial compiler version is unsupported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(plan.worldSeed) ||
            !string.Equals(
                candidate.worldSeed,
                plan.worldSeed,
                StringComparison.Ordinal))
        {
            reason = "The V2 spatial seed does not match the enclosing world plan.";
            return false;
        }

        string acceptedSemanticFingerprint =
            plan.spatialPlan != null
                ? plan.spatialPlan.semanticFingerprint
                : string.Empty;

        if (string.IsNullOrWhiteSpace(candidate.semanticFingerprint) ||
            string.IsNullOrWhiteSpace(acceptedSemanticFingerprint) ||
            !string.Equals(
                candidate.semanticFingerprint,
                acceptedSemanticFingerprint,
                StringComparison.Ordinal))
        {
            reason = "The V2 semantic fingerprint does not match the accepted world semantics.";
            return false;
        }

        if (candidate.acceptanceState !=
            GeneratedSpatialPlanAcceptanceState.Accepted)
        {
            reason = "The V2 spatial artifact has not been accepted.";
            return false;
        }

        if (candidate.validationErrors != null &&
            candidate.validationErrors.Count > 0)
        {
            reason = "The V2 spatial artifact still contains validation errors.";
            return false;
        }

        if (!string.Equals(
                candidate.validationVersion,
                GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion,
                StringComparison.Ordinal) &&
            !string.Equals(candidate.validationVersion,
                GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion, StringComparison.Ordinal))
        {
            reason = "The V2 validation version is unsupported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(candidate.contentHash) ||
            !string.Equals(
                candidate.contentHash,
                candidate.validatedContentHash,
                StringComparison.Ordinal))
        {
            reason = "The V2 artifact changed after validation.";
            return false;
        }

        // note: Reject impossible collection sizes before detached copying, graph validation, sorting, or hashing can consume an unbounded load frame.
        if (!TryPassAcceptedSnapshotPreflight(candidate, out reason))
            return false;

        // note: Integrity checks normalize only a detached copy, so reading an accepted save never rewrites null collections or any other persisted evidence.
        GeneratedSpatialWorldPlanV2Record validationCandidate;
        try
        {
            string detachedJson = JsonUtility.ToJson(candidate);
            validationCandidate =
                JsonUtility.FromJson<GeneratedSpatialWorldPlanV2Record>(
                    detachedJson);
        }
        catch (Exception exception)
        {
            reason = "The V2 artifact could not be copied for read-only validation: " +
                     exception.GetType().Name + ".";
            return false;
        }

        if (validationCandidate == null ||
            validationCandidate.blueprint == null)
        {
            reason = "The V2 spatial blueprint is missing.";
            return false;
        }

        YQSpatialBlueprintValidationResultV2 validation =
            YQSpatialBlueprintValidatorV2.Validate(validationCandidate);
        if (!validation.Accepted)
        {
            reason = "The persisted V2 blueprint no longer satisfies its validation contract: " + validation.Errors[0];
            return false;
        }

        string computedHash =
            YQSpatialBlueprintHasherV2.ComputeContentHash(validationCandidate);
        if (!string.Equals(
                candidate.contentHash,
                computedHash,
                StringComparison.Ordinal))
        {
            reason = "The persisted V2 content does not match its validated hash.";
            return false;
        }

        // note: Shadow reuse and authoritative activation share this full integrity gate, preventing stale saves from becoming runtime authority later.
        return true;
    }

    private static bool TryPassAcceptedSnapshotPreflight(
        GeneratedSpatialWorldPlanV2Record candidate,
        out string reason)
    {
        reason = string.Empty;
        YQSpatialBlueprintV2 blueprint = candidate.blueprint;
        if (blueprint == null)
        {
            reason = "The V2 spatial blueprint is missing.";
            return false;
        }

        int regionCount = CountOrZero(blueprint.regions);
        int terrainCount = CountOrZero(blueprint.terrainFields);
        int hydrologyCount = CountOrZero(blueprint.hydrology);
        int siteCount = CountOrZero(blueprint.sites);
        int routeCount = CountOrZero(blueprint.routes);
        int relationshipCount = CountOrZero(blueprint.relationships);
        if (regionCount > MaximumAcceptedRegions ||
            terrainCount > MaximumAcceptedTerrainFields ||
            hydrologyCount > MaximumAcceptedHydrologyFeatures ||
            siteCount > MaximumAcceptedSites ||
            routeCount > MaximumAcceptedRoutes ||
            relationshipCount > MaximumAcceptedRelationships)
        {
            reason = "The V2 artifact exceeds bounded acceptance collection limits.";
            return false;
        }

        // note: Route/water crossing validation is multiplicative, so cap its conservative segment-pair estimate before invoking the validator.
        int maximumRouteSegments = MaximumSegments(blueprint.routes);
        int maximumHydrologySegments = MaximumSegments(blueprint.hydrology);
        long estimatedSegmentChecks =
            (long)routeCount * hydrologyCount *
            maximumRouteSegments * maximumHydrologySegments;
        if (estimatedSegmentChecks > MaximumRouteHydrologySegmentChecks)
        {
            reason = "The V2 artifact exceeds the bounded route/hydrology validation complexity budget.";
            return false;
        }

        return true;
    }

    private static int CountOrZero<T>(List<T> values)
    {
        return values != null ? values.Count : 0;
    }

    private static int MaximumSegments(List<YQRouteCorridorV2> routes)
    {
        int maximum = 0;
        if (routes == null)
            return maximum;

        // note: Only collection counts are inspected here; geometry remains untouched until detached validation.
        for (int index = 0; index < routes.Count; index++)
        {
            int pointCount = routes[index]?.controlPoints?.Count ?? 0;
            maximum = Math.Max(maximum, Math.Max(0, pointCount - 1));
        }

        return maximum;
    }

    private static int MaximumSegments(List<YQHydrologyFeatureV2> hydrology)
    {
        int maximum = 0;
        if (hydrology == null)
            return maximum;

        // note: The same count-only scan bounds water polyline work without normalizing live feature records.
        for (int index = 0; index < hydrology.Count; index++)
        {
            int pointCount = hydrology[index]?.controlPoints?.Count ?? 0;
            maximum = Math.Max(maximum, Math.Max(0, pointCount - 1));
        }

        return maximum;
    }
}
