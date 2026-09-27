using System;
using System.Collections.Generic;

[Serializable]
public sealed class YQReviewedCellFunctionContractV2
{
    public string cellId = string.Empty;
    public string sourceSignature = string.Empty;
    public YQSemanticSiteReviewState reviewState = YQSemanticSiteReviewState.Pending;
    public string reviewNote = string.Empty;
    public YQAssetCurationContractV2 curation = new YQAssetCurationContractV2();
    // note: Independent placement is reviewed separately from function tags; missing metadata keeps old authored fragments rigid.
    public YQIndependentAssemblyContract independentAssembly = new YQIndependentAssemblyContract();
    // note: Door providers are reviewed independently from the district's visual tags; drafts never install runtime behaviour.
    public List<YQCellDoorBindingV2> doorBindings = new List<YQCellDoorBindingV2>();
    // note: Exact reviewed storage slots bind generated reward mechanics and persistence at materialization time.
    public List<YQCellLootBindingV2> lootBindings = new List<YQCellLootBindingV2>();
}

[Serializable]
public sealed class YQIndependentAssemblyContract
{
    public YQSemanticSiteReviewState reviewState = YQSemanticSiteReviewState.Pending;
    public string sourceSignature = string.Empty;
    public bool completeStructuralDependencies;
    public bool foundationVerified;
    // note: Version zero preserves existing serialized reviews; version one requires explicit final-terrain contact evidence.
    public int terrainContactVersion;
    public List<YQFoundationTerrainContact> terrainContacts = new List<YQFoundationTerrainContact>();
    public List<string> externalConnectionPaths = new List<string>();
}

[Serializable]
public sealed class YQFoundationTerrainContact
{
    // note: The source signature on the owning assembly binds these reviewed cell-local points to exact geometry.
    public string supportPath = string.Empty;
    public UnityEngine.Vector3 localBottom;
    public float minimumEmbedDepth;
    public float maximumEmbedDepth;
}

public static class YQFoundationTerrainContacts
{
    // note: Final procedural grading can differ from reviewed capture on steep joisted pads; the upper tolerance absorbs bounded soil burial drift without permitting a full-cell teleport.
    private const float RuntimeTerrainOverEmbedTolerance = 0.90f;
    // note: Floating support is structurally invalid, so the lower tolerance stays at the measured contact epsilon instead of sharing the burial allowance.
    private const float RuntimeTerrainUnderEmbedTolerance = 0.03f;

    public static bool TryValidate(UnityEngine.Transform cell, UnityEngine.Terrain terrain,
        YQReviewedCellFunctionContractV2 contract, float proposedVerticalDelta, out string failure)
    {
        failure = string.Empty;
        var assembly = contract?.independentAssembly;
        // note: Old authored assemblies retain their existing validation path; they do not acquire version-one certification.
        if (assembly == null || assembly.terrainContactVersion == 0) return true;
        if (cell == null || terrain == null || assembly.terrainContactVersion != 1 ||
            assembly.reviewState != YQSemanticSiteReviewState.Approved || !assembly.foundationVerified ||
            string.IsNullOrWhiteSpace(contract.sourceSignature) || assembly.sourceSignature != contract.sourceSignature ||
            assembly.terrainContacts == null || assembly.terrainContacts.Count == 0 || assembly.terrainContacts.Count > 256 ||
            !Finite(proposedVerticalDelta))
        {
            failure = "Missing, stale or unsupported foundation terrain contact evidence.";
            return false;
        }
        foreach (var contact in assembly.terrainContacts)
        {
            // note: Validate before moving the cell so failed support does not partially apply a placement.
            if (contact == null || !Finite(contact.localBottom.x) || !Finite(contact.localBottom.y) || !Finite(contact.localBottom.z) ||
                !Finite(contact.minimumEmbedDepth) || !Finite(contact.maximumEmbedDepth) || contact.minimumEmbedDepth < 0f ||
                contact.maximumEmbedDepth < contact.minimumEmbedDepth ||
                !YQCellDoorBindingsV2.TryResolveUniquePath(cell, contact.supportPath, out _))
            {
                failure = "Invalid foundation contact geometry or support path.";
                return false;
            }
            var bottom = cell.TransformPoint(contact.localBottom) + UnityEngine.Vector3.up * proposedVerticalDelta;
            if (!TrySamplePublishedTerrain(terrain, bottom, out float soil))
            {
                failure = "Foundation contact has no supported terrain: " + contact.supportPath;
                return false;
            }
            float embed = soil - bottom.y;
            if (!Finite(embed) || embed < contact.minimumEmbedDepth - RuntimeTerrainUnderEmbedTolerance || embed > contact.maximumEmbedDepth + RuntimeTerrainOverEmbedTolerance)
            {
                failure = "Foundation terrain contact outside reviewed embedding range: " + contact.supportPath + " depth=" + embed;
                return false;
            }
        }
        return true;
    }

    private static bool TrySamplePublishedTerrain(
        UnityEngine.Terrain preferredTerrain,
        UnityEngine.Vector3 world,
        out float height)
    {
        // note: A reviewed assembly may straddle a streamed-tile edge; resolve the collision-ready tile at each contact instead of clamping every post to the root tile.
        if (YQTerrainApproachV2.TrySampleTerrain(
                preferredTerrain,
                world,
                out height))
        {
            return true;
        }

        YQPlayerFollowingSemanticChunkStreamer streamer =
            UnityEngine.Object.FindFirstObjectByType<YQPlayerFollowingSemanticChunkStreamer>();
        if (streamer != null &&
            streamer.TryGetGeneratedTerrainAt(world, out UnityEngine.Terrain streamedTerrain))
        {
            return YQTerrainApproachV2.TrySampleTerrain(
                streamedTerrain,
                world,
                out height);
        }

        height = 0f;
        return false;
    }

    // note: Serialized nonfinite values must never turn a range comparison into an implicit success.
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

public static class YQSiteFunctionContractsV2
{
    public static int PopulateCandidates(YQReviewedSemanticSiteManifest manifest)
    {
        if (manifest == null)
            return 0;

        // note: Population copies only existing structured district evidence; it never approves an inferred home, service, entrance, or traversable route.
        int added = 0;
        HashSet<string> assigned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone == null)
                continue;
            zone.cellContractsV2 ??= new List<YQReviewedCellFunctionContractV2>();
            if (manifest.StreamingSite == null && assigned.Add(zone.stableId))
                added += AddCandidate(manifest, zone, zone.stableId);
            else if (zone.streamingCellIds != null)
                foreach (string cellId in zone.streamingCellIds)
                    if (assigned.Add(cellId))
                        added += AddCandidate(manifest, zone, cellId);
        }
        return added;
    }

    private static int AddCandidate(YQReviewedSemanticSiteManifest manifest,
        YQReviewedSemanticZoneRecord zone, string cellId)
    {
        if (string.IsNullOrWhiteSpace(cellId))
            return 0;
        foreach (YQReviewedCellFunctionContractV2 existing in zone.cellContractsV2)
            if (existing != null && string.Equals(existing.cellId, cellId,
                    StringComparison.OrdinalIgnoreCase))
                return 0;

        // note: A streaming subcell cannot inherit its parent district's functions without cell-level inspection.
        YQAssetFunctionV2 function = manifest.StreamingSite == null
            ? SuggestedFunction(zone.districtFunction)
            : YQAssetFunctionV2.None;
        zone.cellContractsV2.Add(new YQReviewedCellFunctionContractV2
        {
            cellId = cellId,
            sourceSignature = manifest.SourceSignature,
            reviewNote = "Candidate only: verify this cell's support, entrances, runtime interactions and route connections before approval.",
            curation = new YQAssetCurationContractV2
            {
                primaryRole = YQAssetRoleV2.SiteAssembly,
                primaryFunction = function,
                familyId = manifest.KitId,
                variantGroupId = cellId,
                preserveAuthoredScale = true
            }
        });
        return 1;
    }

    private static YQAssetFunctionV2 SuggestedFunction(YQDistrictFunction district)
    {
        switch (district)
        {
            case YQDistrictFunction.Residential: return YQAssetFunctionV2.Habitation;
            case YQDistrictFunction.Service: return YQAssetFunctionV2.Service;
            case YQDistrictFunction.Defensive: return YQAssetFunctionV2.Security;
            default: return YQAssetFunctionV2.None;
        }
    }

    public static bool TryValidate(YQReviewedSemanticSiteManifest manifest,
        IReadOnlyCollection<string> selectedIds,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions,
        YQWorldStructureUsagePolicy structurePolicy, out string failure)
    {
        failure = string.Empty;
        if (manifest == null || !manifest.ReleaseEligible || selectedIds == null || selectedIds.Count == 0 ||
            requiredFunctions == null || requiredFunctions.Count == 0)
        {
            failure = "Functional composition requires selected cells and explicit site functions.";
            return false;
        }

        HashSet<string> selected = new HashSet<string>(selectedIds,
            StringComparer.OrdinalIgnoreCase);
        Dictionary<string, YQReviewedCellFunctionContractV2> bindings =
            new Dictionary<string, YQReviewedCellFunctionContractV2>(StringComparer.OrdinalIgnoreCase);
        foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
        {
            if (zone?.cellContractsV2 == null)
                continue;
            foreach (YQReviewedCellFunctionContractV2 binding in zone.cellContractsV2)
            {
                if (binding == null || string.IsNullOrWhiteSpace(binding.cellId) ||
                    !selected.Contains(binding.cellId))
                    continue;
                bool belongs = manifest.StreamingSite == null
                    ? string.Equals(zone.stableId, binding.cellId, StringComparison.OrdinalIgnoreCase)
                    : ContainsId(zone.streamingCellIds, binding.cellId);
                if (!belongs || bindings.ContainsKey(binding.cellId))
                {
                    failure = "Conflicting or misbound functional contract for cell " + binding.cellId + ".";
                    return false;
                }
                bindings.Add(binding.cellId, binding);
            }
        }

        List<string> missing = new List<string>();
        foreach (YQAssetFunctionV2 function in requiredFunctions)
        {
            if (function == YQAssetFunctionV2.None || !Enum.IsDefined(typeof(YQAssetFunctionV2), function))
            {
                failure = "Site contains an invalid required function.";
                return false;
            }
            bool covered = false;
            foreach (YQReviewedCellFunctionContractV2 binding in bindings.Values)
            {
                if (HasReviewedFunction(binding, manifest.SourceSignature, function, structurePolicy))
                {
                    covered = true;
                    break;
                }
            }
            if (!covered && !missing.Contains(function.ToString()))
                missing.Add(function.ToString());
        }
        if (missing.Count == 0)
            return true;
        failure = "Selected cells lack reviewed functional evidence for: " + string.Join(", ", missing) +
            ". District tags and exterior shells are not playable-function proof.";
        return false;
    }

    public static bool TryValidateAvailableFunctions(YQReviewedSemanticSiteManifest manifest,
        IReadOnlyList<YQAssetFunctionV2> requiredFunctions, YQWorldStructureUsagePolicy policy,
        out string failure)
    {
        // note: If the complete kit cannot provide a required function, rerolling twelve subsets cannot repair it.
        failure = string.Empty;
        if (manifest == null || !manifest.ReleaseEligible || requiredFunctions == null || requiredFunctions.Count == 0)
        {
            failure = "Available-function review requires a released manifest and explicit site functions.";
            return false;
        }
        var missing = new List<string>();
        foreach (YQAssetFunctionV2 function in requiredFunctions)
        {
            bool covered = false;
            foreach (YQReviewedSemanticZoneRecord zone in manifest.Zones)
            {
                if (zone?.cellContractsV2 == null) continue;
                foreach (YQReviewedCellFunctionContractV2 binding in zone.cellContractsV2)
                {
                    if (binding == null || string.IsNullOrWhiteSpace(binding.cellId)) continue;
                    bool belongs = manifest.StreamingSite == null
                        ? string.Equals(zone.stableId, binding.cellId, StringComparison.OrdinalIgnoreCase)
                        : ContainsId(zone.streamingCellIds, binding.cellId);
                    if (belongs && HasReviewedFunction(binding, manifest.SourceSignature, function, policy))
                    {
                        covered = true;
                        break;
                    }
                }
                if (covered) break;
            }
            if (!covered && !missing.Contains(function.ToString())) missing.Add(function.ToString());
        }
        // note: Unselected malformed cells must not reject a valid subset; final composition still checks selected contracts, connectivity, budget and footprint.
        if (missing.Count == 0) return true;
        failure = "Entire kit lacks reviewed functional evidence for: " + string.Join(", ", missing) + ".";
        return false;
    }

    internal static bool HasReviewedFunction(YQReviewedCellFunctionContractV2 binding,
        string sourceSignature, YQAssetFunctionV2 function, YQWorldStructureUsagePolicy policy)
    {
        // note: Selection and final acceptance share the same evidence rules; a label-only provider must not displace a usable cell.
        if (binding == null || binding.reviewState != YQSemanticSiteReviewState.Approved ||
            binding.curation == null ||
            binding.curation.contractVersion != YQAssetCurationContractV2.SupportedContractVersion ||
            !string.Equals(binding.sourceSignature, sourceSignature, StringComparison.Ordinal))
            return false;
        binding.curation.EnsureCollections();
        if (!binding.curation.HasFunction(function) || !HasFunctionalEvidence(binding.curation, function, policy))
            return false;
        if (function != YQAssetFunctionV2.Storage && function != YQAssetFunctionV2.Reward) return true;
        // note: Storage/reward geometry requires an actual reviewed provider matched to the same access socket; a decorative chest cannot satisfy the function alone.
        foreach (YQCellLootBindingV2 loot in binding.lootBindings ?? new List<YQCellLootBindingV2>())
            if (YQCellLootBindingsV2.HasReviewedEvidence(loot, sourceSignature))
                foreach (YQAssetSocketRecordV2 socket in binding.curation.sockets)
                    if (socket != null && socket.socketId == loot.accessSocket.socketId &&
                        socket.transformPath == loot.accessSocket.transformPath &&
                        (socket.localPosition - loot.accessSocket.localPosition).sqrMagnitude < 0.0001f &&
                        UnityEngine.Quaternion.Angle(socket.localRotation, loot.accessSocket.localRotation) < 0.1f)
                        return true;
        return false;
    }

    private static bool ContainsId(IReadOnlyList<string> ids, string id)
    {
        if (ids != null)
            foreach (string candidate in ids)
                if (string.Equals(candidate, id, StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    private static bool HasFunctionalEvidence(YQAssetCurationContractV2 curation,
        YQAssetFunctionV2 function, YQWorldStructureUsagePolicy policy)
    {
        // note: These minimum typed affordances are a review gate, not an implementation of doors, services, or interiors; approval must also verify their actual runtime providers.
        switch (function)
        {
            case YQAssetFunctionV2.Habitation:
                return (policy == YQWorldStructureUsagePolicy.FullyEnterable ||
                        policy == YQWorldStructureUsagePolicy.SingleFurnishedPrimaryWithExteriorShells) &&
                    curation.HasAffordance(YQAssetAffordanceV2.Entrance) &&
                    curation.HasAffordance(YQAssetAffordanceV2.Sleeping) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Entrance) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Furnishing);
            case YQAssetFunctionV2.Circulation:
                return curation.HasAffordance(YQAssetAffordanceV2.TraversableSurface) &&
                    (HasUsableSocket(curation, YQAssetSocketKindV2.Road) ||
                     HasUsableSocket(curation, YQAssetSocketKindV2.Connection));
            case YQAssetFunctionV2.Service:
            case YQAssetFunctionV2.Commerce:
                return HasUsableSocket(curation, YQAssetSocketKindV2.Interaction);
            case YQAssetFunctionV2.Manufacturing:
                return curation.HasAffordance(YQAssetAffordanceV2.Workstation) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Interaction);
            case YQAssetFunctionV2.Security:
                return curation.HasAffordance(YQAssetAffordanceV2.Cover) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Cover);
            case YQAssetFunctionV2.Reward:
                return curation.HasAffordance(YQAssetAffordanceV2.RewardAnchor) &&
                    curation.HasAffordance(YQAssetAffordanceV2.Storage) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Interaction);
            case YQAssetFunctionV2.Encounter:
                return curation.HasAffordance(YQAssetAffordanceV2.SpawnAnchor) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Spawn);
            case YQAssetFunctionV2.Transition:
                return curation.HasAffordance(YQAssetAffordanceV2.Entrance) &&
                    curation.HasAffordance(YQAssetAffordanceV2.Exit) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Entrance) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Exit);
            case YQAssetFunctionV2.Storage:
                return curation.HasAffordance(YQAssetAffordanceV2.Storage) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Interaction);
            case YQAssetFunctionV2.Civic:
            case YQAssetFunctionV2.CulturalFocus:
                return curation.HasAffordance(YQAssetAffordanceV2.QuestAnchor) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Interaction);
            case YQAssetFunctionV2.Infrastructure:
                return curation.HasAffordance(YQAssetAffordanceV2.TraversableSurface) &&
                    HasUsableSocket(curation, YQAssetSocketKindV2.Connection);
            case YQAssetFunctionV2.Ecology:
                return curation.ecology != null && curation.ecology.layer != YQEcologyLayerV2.None &&
                    !string.IsNullOrWhiteSpace(curation.ecology.speciesFamilyId);
            default:
                return false;
        }
    }

    private static bool HasUsableSocket(YQAssetCurationContractV2 curation, YQAssetSocketKindV2 kind)
    {
        // note: Empty candidate socket names do not count; a reviewed socket needs a stable identity, transform path and positive finite clearance.
        foreach (YQAssetSocketRecordV2 socket in curation.sockets)
        {
            if (socket == null || socket.kind != kind ||
                string.IsNullOrWhiteSpace(socket.socketId) || string.IsNullOrWhiteSpace(socket.transformPath))
                continue;
            UnityEngine.Vector3 size = socket.clearanceSize;
            UnityEngine.Vector3 position = socket.localPosition;
            UnityEngine.Quaternion rotation = socket.localRotation;
            if (FinitePositive(size.x) && FinitePositive(size.y) && FinitePositive(size.z) &&
                Finite(position.x) && Finite(position.y) && Finite(position.z) &&
                Finite(rotation.x) && Finite(rotation.y) && Finite(rotation.z) && Finite(rotation.w) &&
                rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w > 0.0001f)
                return true;
        }
        return false;
    }

    private static bool FinitePositive(float value)
    {
        return value > 0f && Finite(value);
    }

    private static bool Finite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
