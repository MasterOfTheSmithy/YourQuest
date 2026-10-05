using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQProceduralSettlementLayoutRecord
{
    public int version = 1;
    public string seed, kitId, sourceSignature;
    // note: Persist coordinates only; Unity's computed normalized property recursively expands under the world's default JSON serializer.
    [Newtonsoft.Json.JsonConverter(typeof(Vector3JsonConverter))]
    public Vector3 origin;
    public float radius;
    // note: Legacy records keep shared grading; new layouts persist parcel elevations instead of resampling them after reload.
    public int earthworkVersion;
    public List<float> parcelGroundHeights = new List<float>();
    public List<YQProceduralCellPlacement> cells = new List<YQProceduralCellPlacement>();
    public List<YQProceduralStreet> streets = new List<YQProceduralStreet>();
    // note: Sector geometry is derived from the accepted footprint; older committed layouts retain their original coordinates and version.
    public string sectorFootprintSignature;
    public List<YQProceduralSiteSector> sectors = new List<YQProceduralSiteSector>();
}

[Serializable]
public sealed class YQProceduralSiteSector
{
    public string id;
    [Newtonsoft.Json.JsonConverter(typeof(Vector3JsonConverter))]
    public Vector3 center;
    public float radius;
}

[Serializable]
public sealed class YQProceduralCellPlacement
{
    public string cellId;
    public string sectorId;
    // note: Version six distinguishes a placed assembly from its approved source; central legacy identities are retained.
    public string sourceCellId;
    // note: Every persisted placement vector uses the existing coordinate converter, including bounds and street endpoints.
    [Newtonsoft.Json.JsonConverter(typeof(Vector3JsonConverter))]
    public Vector3 position;
    public float yaw;
    [Newtonsoft.Json.JsonConverter(typeof(Vector3JsonConverter))]
    public Vector3 boundsCenter, boundsSize;
}

[Serializable]
public sealed class YQProceduralStreet
{
    // note: Keep the established x/y/z save shape without serializing computed Unity properties.
    [Newtonsoft.Json.JsonConverter(typeof(Vector3JsonConverter))]
    public Vector3 start, end;
    public float width;
}

public enum YQWorldConstructionMode
{
    Unresolved = 0,
    AuthoredStreamingSlice = 1,
    AuthoredZones = 2,
    ProceduralBlocks = 3
}

[Serializable]
public sealed class YQWorldConstructionReport
{
    public string locationId, kitId, sourceSignature, selectionSeed, limitation;
    public YQWorldConstructionMode mode;
    public int requestedBlocks, selectedUnits, sourceInstances;
    public bool populationCapacityVerified;

    public static YQWorldConstructionReport Describe(string locationId,
        YQReviewedSemanticSiteManifest manifest, HashSet<string> selected,
        string seed, YQProceduralSettlementLayoutRecord layout, int requestedBlocks)
    {
        // note: Report the exact prepared selection; tags and catalog membership do not prove procedural geometry or population capacity.
        var report = new YQWorldConstructionReport
        {
            locationId = locationId ?? string.Empty,
            kitId = manifest != null ? manifest.KitId : string.Empty,
            sourceSignature = manifest != null ? manifest.SourceSignature : string.Empty,
            selectionSeed = seed ?? string.Empty,
            requestedBlocks = Math.Max(0, requestedBlocks),
            selectedUnits = selected != null ? selected.Count : 0
        };
        if (manifest == null || selected == null || selected.Count == 0)
        {
            report.limitation = "No complete prepared manifest/selection is available.";
            return report;
        }

        // note: Count each selected source unit once and reject missing IDs instead of treating a partial selection as construction proof.
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (manifest.StreamingSite != null)
        {
            report.mode = YQWorldConstructionMode.AuthoredStreamingSlice;
            foreach (var cell in manifest.StreamingSite.Cells)
                if (cell != null && selected.Contains(cell.StableCellId) && resolved.Add(cell.StableCellId))
                    report.sourceInstances += Math.Max(0, cell.SourceInstanceCount);
            report.limitation = "Authored streaming fragments retain source placement; independent assembly and population capacity are not proven.";
        }
        else
        {
            report.mode = YQWorldConstructionMode.AuthoredZones;
            foreach (var zone in manifest.Zones)
                if (zone != null && selected.Contains(zone.stableId) && resolved.Add(zone.stableId))
                    report.sourceInstances += Math.Max(0, zone.sourceInstanceCount);
            report.limitation = "Authored zone placement; no accepted independent block layout.";
        }
        if (!resolved.SetEquals(selected))
        {
            report.mode = YQWorldConstructionMode.Unresolved;
            report.limitation = "Selected construction units are missing from the source manifest.";
            return report;
        }

        // note: A cached layout is evidence only when its identity, geometry and complete selection agree with this prepared site.
        if (layout != null)
        {
            // note: Only the versioned independent-assembly path may apply a block layout to streamed cells.
            if (manifest.StreamingSite != null && !YQProceduralSettlementLayout.UsesIndependentStreaming(seed))
            {
                report.mode = YQWorldConstructionMode.Unresolved;
                report.limitation = "Independent layout supplied to an authored-only streaming materializer.";
                return report;
            }
            var layoutIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (layout.cells != null)
                foreach (var cell in layout.cells)
                    if (cell != null) layoutIds.Add(string.IsNullOrEmpty(cell.sourceCellId) ? cell.cellId : cell.sourceCellId);
            if (layout.kitId != manifest.KitId || layout.sourceSignature != manifest.SourceSignature ||
                layout.seed != seed || !layoutIds.SetEquals(selected) ||
                !YQProceduralSettlementLayout.ValidateRecord(layout, out string _))
            {
                report.mode = YQWorldConstructionMode.Unresolved;
                report.limitation = "Prepared layout identity, selection or geometry does not match the source contract.";
                return report;
            }
            report.mode = YQWorldConstructionMode.ProceduralBlocks;
            if (layout.version == 6)
            {
                // note: Report physical placements and all cloned source instances; reusing one approved source is not one resident cell's worth of work.
                report.selectedUnits = layout.cells.Count;
                report.sourceInstances = 0;
                foreach (var cell in layout.cells)
                    if (manifest.StreamingSite != null)
                    {
                        foreach (var source in manifest.StreamingSite.Cells)
                            if (source != null && string.Equals(source.StableCellId, cell.sourceCellId, StringComparison.OrdinalIgnoreCase))
                                report.sourceInstances += Math.Max(0, source.SourceInstanceCount);
                    }
                    else foreach (var source in manifest.Zones)
                        if (source != null && string.Equals(source.stableId, cell.sourceCellId, StringComparison.OrdinalIgnoreCase))
                            report.sourceInstances += Math.Max(0, source.sourceInstanceCount);
            }
            report.limitation = report.requestedBlocks > report.selectedUnits
                ? "Requested block count exceeds the fitted assembly count."
                : "Block layout verified; inhabitant and service capacity require separate functional validation.";
        }
        return report;
    }
}

// note: One deterministic block plan drives footprint reservation, street presentation and rigid-cell instantiation.
// Existing saved seeds never opt in implicitly; authored interiors are neither scattered nor reconstructed here.
public static class YQProceduralSettlementLayout
{
    public const string SeedPrefix = "cell-streets-v4|";
    public const string SectorSeedPrefix = "site-sectors-v1|";
    public const string ReusableSectorSeedPrefix = "site-sectors-v2|";
    // note: Retain version-three geometry for persisted seeds while new layouts reserve enough run for sloped entrance approaches.
    public const string PriorIndependentSeedPrefix = "cell-streets-v3|";
    public const string PreviousSeedPrefix = "cell-streets-v2|";
    public const string LegacySeedPrefix = "cell-streets-v1|";
    private static readonly Dictionary<string, YQProceduralSettlementLayoutRecord> pending =
        new Dictionary<string, YQProceduralSettlementLayoutRecord>(StringComparer.Ordinal);

    public sealed class Cell
    {
        public string id;
        public Vector3 center, size, entrance, outward;
        public float datum;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => pending.Clear();

    public static bool Enabled(string seed) => seed != null &&
        (UsesSectors(seed) || seed.StartsWith(SeedPrefix, StringComparison.Ordinal) || seed.StartsWith(PriorIndependentSeedPrefix, StringComparison.Ordinal) || seed.StartsWith(PreviousSeedPrefix, StringComparison.Ordinal) || seed.StartsWith(LegacySeedPrefix, StringComparison.Ordinal));

    // note: Fresh worlds opt into independent streaming; existing v1/v2 seeds retain their original selection and geometry versions.
    public static bool UsesIndependentStreaming(string seed) => seed != null &&
        (UsesSectors(seed) || seed.StartsWith(SeedPrefix, StringComparison.Ordinal) || seed.StartsWith(PriorIndependentSeedPrefix, StringComparison.Ordinal));
    public static bool UsesReusableSectors(string seed) => seed != null && seed.StartsWith(ReusableSectorSeedPrefix, StringComparison.Ordinal);
    public static bool UsesSectors(string seed) => UsesReusableSectors(seed) || seed != null && seed.StartsWith(SectorSeedPrefix, StringComparison.Ordinal);
    private static int VersionFor(string seed) => UsesReusableSectors(seed) ? 6 : UsesSectors(seed) ? 5 : seed != null && seed.StartsWith(SeedPrefix, StringComparison.Ordinal) ? 4 : UsesIndependentStreaming(seed) ? 3 :
        seed != null && seed.StartsWith(PreviousSeedPrefix, StringComparison.Ordinal) ? 2 : 1;

    public static void EnableNewWorld(GeneratedWorldPlanRecord plan)
    {
        // note: Called only on newly generated model/fallback output, never on save load. Generated JSON cannot supply geometry authority.
        if (plan?.settlements == null) return;
        foreach (var settlement in plan.settlements)
        {
            if (settlement == null) continue;
            settlement.proceduralLayout = null;
            // note: This engine-owned selection policy is stamped only on fresh accepted generation output, never retrofitted during Continue.
            settlement.preferMeasuredStructuralCells = true;
            settlement.proceduralBlockTarget = DesiredBlockCount(settlement.approxPopulation, settlement.serviceSlots);
            if (!Enabled(settlement.deterministicSeed))
                settlement.deterministicSeed = SeedPrefix + settlement.settlementId + "|" + settlement.deterministicSeed;
        }
    }

    public static YQProceduralSettlementLayoutRecord Get(string seed)
    {
        pending.TryGetValue(seed ?? string.Empty, out var value);
        return value;
    }

    internal static void DiscardPending(string seed, YQProceduralSettlementLayoutRecord expected)
    {
        // note: Detached frontier attempts own their temporary cache entries; retirement must not remove a replacement or a different site's layout.
        if (seed != null && pending.TryGetValue(seed, out var current) && ReferenceEquals(current, expected)) pending.Remove(seed);
    }

    private static IEnumerable<GeneratedSettlementRecord> EnumerateSettlementOwners(GeneratedWorldPlanRecord plan)
    {
        if (plan?.settlements != null)
            foreach (var settlement in plan.settlements) if (settlement != null) yield return settlement;
        var continuation = plan?.spatialPlanV2?.acceptedContinuation;
        // note: Continued layouts remain on the accepted extension rather than being copied into or replacing the historical base plan.
        if (continuation?.state == YQSpatialContinuationStateV2.Accepted && continuation.locations != null)
            foreach (var location in continuation.locations)
                if (location?.state == YQSpatialContinuationStateV2.Accepted && location.settlement != null) yield return location.settlement;
    }

    private static IEnumerable<YQProceduralSettlementLayoutRecord> EnumerateCommittedLayouts(GeneratedWorldPlanRecord plan)
    {
        foreach (var owner in EnumerateSettlementOwners(plan)) if (owner.proceduralLayout != null) yield return owner.proceduralLayout;
        var continuation = plan?.spatialPlanV2?.acceptedContinuation;
        if (continuation?.state == YQSpatialContinuationStateV2.Accepted && continuation.locations != null)
            foreach (var location in continuation.locations)
                if (location?.state == YQSpatialContinuationStateV2.Accepted && location.settlement == null &&
                    location.compositionLayout != null) yield return location.compositionLayout;
    }

    public static int DesiredBlockCount(int population, IReadOnlyList<string> services)
    {
        // note: Scale follows structured population and distinct service demand, not genre labels or a random density roll.
        int residential = population <= 80 ? 1 : population <= 250 ? 2 : population <= 800 ? 3 :
            population <= 2000 ? 4 : population <= 6000 ? 6 : 8;
        var distinct = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (services != null)
            foreach (string service in services)
                if (!string.IsNullOrWhiteSpace(service)) distinct.Add(service.Trim());
        int serviceBlocks = Math.Min(8, (distinct.Count + 1) / 2);
        return Math.Min(8, Math.Max(residential, serviceBlocks));
    }

    public static int ResolveTargetCount(int desired, int mandatoryCount, int capacity)
    {
        // note: Required providers are never dropped to meet a small target. Capacity limits express available cells, not fabricated population.
        return Math.Min(Math.Max(0, capacity), Math.Max(mandatoryCount, desired));
    }

    public static GeneratedSettlementRecord FindOwner(string seed)
    {
        if (!Enabled(seed)) return null;
        // note: A derived sector policy still belongs to the original settlement; it must retain that owner's scale and construction report.
        string ownerSeed = seed;
        if (UsesSectors(seed))
        {
            int suffix = seed.LastIndexOf('|');
            int prefixLength = UsesReusableSectors(seed) ? ReusableSectorSeedPrefix.Length : SectorSeedPrefix.Length;
            if (suffix > prefixLength) ownerSeed = seed.Substring(prefixLength, suffix - prefixLength);
        }
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        foreach (var settlement in EnumerateSettlementOwners(plan))
        {
            if (settlement == null || !Enabled(settlement.deterministicSeed)) continue;
            if (string.Equals(settlement.proceduralLayout?.seed, seed, StringComparison.Ordinal) ||
                string.Equals(settlement.deterministicSeed, ownerSeed, StringComparison.Ordinal) ||
                ownerSeed.StartsWith(settlement.deterministicSeed + "|semantic_variant|", StringComparison.Ordinal)) return settlement;
        }
        return null;
    }

    public static void Commit(GeneratedSettlementRecord settlement, string seed)
    {
        // note: Only the construction prepass may commit a footprint-selected candidate to the existing world save.
        var value = Get(seed);
        if (settlement != null && value != null)
        {
            settlement.proceduralLayout = value;
            // note: A limited library is an explicit capacity shortfall, not permission to clone districts or relax grounding limits.
            if (settlement.proceduralBlockTarget > value.cells.Count)
                Debug.LogWarning("[WORLDGEN] SETTLEMENT CAPACITY LIMITED: location=" + settlement.settlementId +
                    ", requestedBlocks=" + settlement.proceduralBlockTarget + ", availableFittedBlocks=" + value.cells.Count +
                    ". Additional compatible functional cells are required for the requested scale.");
        }
    }

    public static bool TryResolve(YQReviewedSemanticSiteManifest manifest, HashSet<string> selected, string seed,
        out YQProceduralSettlementLayoutRecord layout, out string failure, YQSpatialMaterializationSiteV2? acceptedSite = null)
    {
        layout = null;
        failure = string.Empty;
        if (!Enabled(seed)) return true;
        pending.Remove(seed);
        // note: Restore committed geometry before checking new-generation eligibility. A changed kit cannot silently revert a saved town to donor coordinates.
        var savedPlan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        foreach (var saved in EnumerateCommittedLayouts(savedPlan))
            {
                if (saved == null || !string.Equals(saved.seed, seed, StringComparison.Ordinal)) continue;
                if (saved.kitId != manifest.KitId || saved.sourceSignature != manifest.SourceSignature ||
                    saved.cells == null || saved.version != 6 && saved.cells.Count != selected.Count || !ValidateRecord(saved, out failure))
                { failure = "Saved procedural cell layout no longer matches its source contract."; return false; }
                var savedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in saved.cells) if (cell != null) savedIds.Add(string.IsNullOrEmpty(cell.sourceCellId) ? cell.cellId : cell.sourceCellId);
                if (!savedIds.SetEquals(selected)) { failure = "Saved procedural cell selection changed."; return false; }
                layout = saved;
                pending[seed] = layout;
                return true;
            }
        // note: Preserve old seeds, but require explicitly reviewed complete assemblies for new streamed layouts.
        IReadOnlyList<YQReviewedSemanticZoneRecord> layoutZones = manifest.Zones;
        if (manifest.StreamingSite != null)
        {
            // note: Unconverted libraries remain explicitly authored compatibility content; an invalid published independent contract is rejected below.
            if (!UsesIndependentStreaming(seed) || !HasIndependentStreamingReview(manifest))
            {
                if (UsesSectors(seed)) { failure = "Member sectors require reviewed independent structural assemblies."; return false; }
                return true;
            }
            if (!TryGetIndependentStreamingZones(manifest, out var independentZones, out failure)) return false;
            layoutZones = independentZones;
        }
        var inputs = new List<Cell>();
        foreach (var zone in layoutZones)
        {
            if (zone == null || !selected.Contains(zone.stableId)) continue;
            var connectionPaths = zone.connectionSocketPaths;
            if (UsesSectors(seed))
            {
                // note: Both authored zones and adapted streaming cells need the same approved completeness/foundation contract before independent placement.
                YQIndependentAssemblyContract independent = null;
                if (zone.cellContractsV2 != null)
                    foreach (var binding in zone.cellContractsV2)
                    {
                        if (binding == null || !string.Equals(binding.cellId, zone.stableId, StringComparison.OrdinalIgnoreCase) ||
                            binding.reviewState != YQSemanticSiteReviewState.Approved) continue;
                        var candidate = binding.independentAssembly;
                        if (independent != null || binding.sourceSignature != manifest.SourceSignature || candidate == null ||
                            candidate.reviewState != YQSemanticSiteReviewState.Approved || candidate.sourceSignature != manifest.SourceSignature ||
                            !candidate.completeStructuralDependencies || !candidate.foundationVerified ||
                            manifest.StreamingSite != null && (candidate.externalConnectionPaths == null || candidate.externalConnectionPaths.Count == 0))
                        { failure = "Missing, conflicting or stale independent sector contract: " + zone.stableId; return false; }
                        independent = candidate;
                    }
                if (independent == null) { failure = "No reviewed complete sector assembly: " + zone.stableId; return false; }
                // note: Authored-zone reviews publish their frontage in the zone contract; only streaming adaptations require the separate cell path list.
                connectionPaths = manifest.StreamingSite != null ? independent.externalConnectionPaths : zone.connectionSocketPaths;
            }
            Transform entrance = null;
            uint entranceScore = uint.MaxValue;
            string entrancePath = null;
            // note: Consume an explicitly declared external connection. Never infer habitation or a door from an asset name.
            if (zone.prefab != null && connectionPaths != null)
                foreach (string path in connectionPaths)
                {
                    // note: Empty paths can resolve to the prefab root, which is not an explicitly authored connection.
                    if (string.IsNullOrWhiteSpace(path)) continue;
                    Transform candidate = zone.prefab.transform.Find(path);
                    if (candidate == null || !IsExternalConnection(zone.localBoundsCenter, zone.localBoundsSize,
                            zone.prefab.transform.InverseTransformPoint(candidate.position),
                            zone.prefab.transform.InverseTransformDirection(candidate.forward)))
                        continue;
                    uint score = Hash(seed + "|" + zone.stableId + "|" + path);
                    // note: Rank only geometrically eligible portals, with stable path tie-breaking independent of serialized list order.
                    if (entrance == null || score < entranceScore ||
                        (score == entranceScore && string.CompareOrdinal(path, entrancePath) < 0))
                    { entrance = candidate; entranceScore = score; entrancePath = path; }
                }
            if (entrance == null)
            {
                // note: Undeclared legacy blocks retain compatibility, but explicitly declared unusable entrances must not silently become authored-placement success.
                if (!UsesIndependentStreaming(seed) && (zone.connectionSocketPaths == null || zone.connectionSocketPaths.Count == 0)) return true;
                failure = "No declared external connection reaches the cell frontage: " + zone.stableId;
                return false;
            }
            inputs.Add(new Cell { id = zone.stableId, center = zone.localBoundsCenter, size = zone.localBoundsSize,
                datum = zone.authoredSourceOrigin.y,
                entrance = zone.prefab.transform.InverseTransformPoint(entrance.position),
                outward = zone.prefab.transform.InverseTransformDirection(entrance.forward) });
        }
        if (inputs.Count != selected.Count) { failure = "Procedural layout references missing cells."; return false; }
        if (UsesSectors(seed))
        {
            if (!acceptedSite.HasValue) { failure = "Sector layout has no accepted footprint context."; return false; }
            if (!TryBuildSectors(inputs, seed, acceptedSite.Value, out layout, out failure)) return false;
        }
        else if (!TryBuild(inputs, seed, out layout, out failure)) return false;
        layout.kitId = manifest.KitId;
        layout.sourceSignature = manifest.SourceSignature;
        pending[seed] = layout;
        return true;
    }

    public static string SectorFootprintSignature(YQSpatialMaterializationSiteV2 site)
    {
        // note: Keep the exact canonical geometry in the derived layout identity; this does not alter the historical accepted V2 content hash.
        var text = new System.Text.StringBuilder();
        text.Append(site.siteId?.Length ?? 0).Append(':').Append(site.siteId);
        void Number(float value) => text.Append(':').Append(value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        Number(site.x); Number(site.z); Number(site.headingDegrees); Number(site.reservedRadius);
        var members = new List<YQSiteMemberFootprintV2>(site.MemberFootprint);
        members.Sort((a, b) => string.CompareOrdinal(a?.memberId, b?.memberId));
        foreach (var member in members)
        {
            if (member == null) { text.Append("|null"); continue; }
            text.Append('|').Append(member.memberId?.Length ?? 0).Append(':').Append(member.memberId).Append(':').Append(member.sectorIndex);
            Number(member.x); Number(member.z); Number(member.reservedRadius);
        }
        return text.ToString();
    }

    public static string BuildSectorSeed(string seed, YQSpatialMaterializationSiteV2 site) =>
        SectorSeedPrefix + seed + "|" + Hash(SectorFootprintSignature(site)).ToString("X8");

    public static string BuildReusableSectorSeed(string seed, YQSpatialMaterializationSiteV2 site) =>
        ReusableSectorSeedPrefix + seed + "|" + Hash(SectorFootprintSignature(site)).ToString("X8");

    public static bool TryValidateAcceptedSectorFootprint(YQProceduralSettlementLayoutRecord layout,
        YQSpatialMaterializationSiteV2 site, out string failure)
    {
        // note: A stored signature alone cannot authorize changed sector coordinates or radii. Match the saved physical reservations to every immutable accepted member before loading.
        failure = "Sector layout does not match the accepted footprint.";
        if (layout == null || layout.version < 5 || !ValidateRecord(layout, out _) ||
            layout.sectorFootprintSignature != SectorFootprintSignature(site) ||
            layout.sectors.Count != site.MemberFootprint.Count + 1 ||
            !Finite(new Vector3(site.x, site.headingDegrees, site.z))) return false;
        var expected = new Dictionary<string, YQProceduralSiteSector>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(site.siteId)) return false;
        expected.Add(site.siteId, new YQProceduralSiteSector { id = site.siteId, center = Vector3.zero, radius = site.reservedRadius });
        foreach (var member in site.MemberFootprint)
        {
            if (member == null || string.IsNullOrWhiteSpace(member.memberId) || expected.ContainsKey(member.memberId)) return false;
            expected.Add(member.memberId, new YQProceduralSiteSector { id = member.memberId,
                center = RotateYaw(new Vector3(member.x - site.x, 0f, member.z - site.z), -site.headingDegrees), radius = member.reservedRadius });
        }
        foreach (var sector in layout.sectors)
        {
            if (!expected.TryGetValue(sector.id, out var accepted) || !Finite(accepted.center) ||
                float.IsNaN(accepted.radius) || float.IsInfinity(accepted.radius) || accepted.radius <= 0f ||
                (sector.center - accepted.center).sqrMagnitude > .000001f || Mathf.Abs(sector.radius - accepted.radius) > .001f) return false;
        }
        if (layout.sectors[0].id != site.siteId) return false;
        failure = string.Empty;
        return true;
    }

    public static string SectorInstanceId(string sectorId, string sourceId) =>
        "sector:" + sectorId.Length + ":" + sectorId + "|source:" + sourceId.Length + ":" + sourceId;

    public static string SourceCellId(YQProceduralSettlementLayoutRecord layout, string instanceId) =>
        TryPlacement(layout, instanceId, out var placement) && !string.IsNullOrEmpty(placement.sourceCellId) ? placement.sourceCellId : instanceId;

    public static List<string> InstanceCellIds(YQProceduralSettlementLayoutRecord layout, string sourceId)
    {
        // note: The existing loader consumes every accepted placement once, while older layouts retain their one-to-one source identity.
        var ids = new List<string>();
        if (layout?.version == 6)
        {
            foreach (var placement in layout.cells)
                if (placement != null && string.Equals(placement.sourceCellId, sourceId, StringComparison.OrdinalIgnoreCase)) ids.Add(placement.cellId);
            ids.Sort(StringComparer.Ordinal);
        }
        else ids.Add(sourceId);
        return ids;
    }

    public static bool TryBuildSectors(IReadOnlyList<Cell> source, string seed, YQSpatialMaterializationSiteV2 site,
        out YQProceduralSettlementLayoutRecord result, out string failure)
    {
        // note: Partition the same bounded selected cells, never duplicate a site/provider. Every accepted member receives a complete rigid assembly and frontage.
        result = null; failure = "Invalid accepted sector layout.";
        bool reusable = UsesReusableSectors(seed);
        if (!reusable && source != null && site.MemberFootprint.Count >= source.Count)
        { failure = "Insufficient selected complete assemblies for the owner and every accepted member sector."; return false; }
        if (!UsesSectors(seed) || source == null || source.Count == 0 || source.Count > 8 || site.MemberFootprint.Count == 0 ||
            (reusable ? source.Count + site.MemberFootprint.Count > 8 : site.MemberFootprint.Count >= source.Count) || !Finite(new Vector3(site.x, site.headingDegrees, site.z)) ||
            float.IsNaN(site.reservedRadius) || float.IsInfinity(site.reservedRadius) || site.reservedRadius <= 0f)
            return false;
        var remaining = new List<Cell>(source);
        remaining.Sort((a, b) => string.CompareOrdinal(a?.id, b?.id));
        var inputIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in remaining)
        {
            if (cell == null || !inputIds.Add(cell.id)) { failure = "Missing or duplicate source assembly identity."; return false; }
            if (!TryBuild(new[] { cell }, SeedPrefix + seed, out _, out failure)) return false;
        }
        var members = new List<YQSiteMemberFootprintV2>(site.MemberFootprint);
        members.Sort((a, b) => { int order = (a?.reservedRadius ?? 0f).CompareTo(b?.reservedRadius ?? 0f);
            return order != 0 ? order : string.CompareOrdinal(a?.memberId, b?.memberId); });
        var layout = new YQProceduralSettlementLayoutRecord { seed = seed, version = reusable ? 6 : 5, earthworkVersion = 1,
            sectorFootprintSignature = SectorFootprintSignature(site) };
        var sectorIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { site.siteId };
        var memberEntries = new List<Vector3>();
        foreach (var member in members)
        {
            if (member == null || string.IsNullOrWhiteSpace(member.memberId) || !sectorIds.Add(member.memberId) ||
                !Finite(new Vector3(member.x, member.reservedRadius, member.z)) || member.reservedRadius <= 0f)
            { failure = "Invalid or duplicate accepted member sector."; return false; }
            Vector3 center = RotateYaw(new Vector3(member.x - site.x, 0f, member.z - site.z), -site.headingDegrees);
            Vector3 facing = -center;
            if (facing.sqrMagnitude < .01f) facing = Vector3.forward;
            facing = facing.normalized;
            Cell chosen = null;
            YQProceduralCellPlacement placement = null;
            Vector3 entry = default;
            float bestArea = -1f;
            foreach (var cell in remaining)
            {
                float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg - Mathf.Atan2(cell.outward.x, cell.outward.z) * Mathf.Rad2Deg;
                Vector3 x = RotateYaw(new Vector3(cell.size.x, 0f, 0f), yaw), z = RotateYaw(new Vector3(0f, 0f, cell.size.z), yaw);
                Vector3 size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(z.x), cell.size.y, Mathf.Abs(x.z) + Mathf.Abs(z.z));
                // note: Reserve fourteen metres ahead of the declared entrance for the existing graded approach, with a three-metre foundation apron.
                Vector3 actualCenter = center - facing * 7f;
                Vector3 position = actualCenter - RotateYaw(cell.center, yaw);
                position.y = cell.datum;
                actualCenter = position + RotateYaw(cell.center, yaw);
                Vector3 candidateEntry = position + RotateYaw(cell.entrance, yaw);
                Vector3 approach = candidateEntry + facing * 14f;
                float extentX = Mathf.Abs(actualCenter.x - center.x) + size.x * .5f;
                float extentZ = Mathf.Abs(actualCenter.z - center.z) + size.z * .5f;
                float area = cell.size.x * cell.size.z;
                if (new Vector2(extentX, extentZ).magnitude + 3f > member.reservedRadius ||
                    new Vector2(approach.x - center.x, approach.z - center.z).magnitude + 2.25f > member.reservedRadius || area <= bestArea)
                    continue;
                chosen = cell; bestArea = area; entry = candidateEntry;
                placement = new YQProceduralCellPlacement { cellId = reusable ? SectorInstanceId(member.memberId, cell.id) : cell.id,
                    sourceCellId = reusable ? cell.id : null, sectorId = member.memberId,
                    position = position, yaw = yaw, boundsCenter = actualCenter, boundsSize = size };
            }
            if (chosen == null) { failure = "No complete selected assembly fits accepted sector " + member.memberId; return false; }
            if (!reusable) remaining.Remove(chosen);
            layout.cells.Add(placement);
            layout.sectors.Add(new YQProceduralSiteSector { id = member.memberId, center = center, radius = member.reservedRadius });
            Vector3 dock = entry + facing * 14f; dock.y = entry.y = 0f;
            layout.streets.Add(new YQProceduralStreet { start = dock, end = entry, width = 3f });
            memberEntries.Add(dock);
        }
        if (!TryBuild(remaining, SeedPrefix + seed, out var core, out failure)) return false;
        if (core.radius > site.reservedRadius) { failure = "Central assemblies exceed the accepted owner reservation."; return false; }
        layout.origin = core.origin;
        layout.sectors.Insert(0, new YQProceduralSiteSector { id = site.siteId, center = Vector3.zero, radius = site.reservedRadius });
        foreach (var cell in core.cells) { cell.sectorId = site.siteId; if (reusable) cell.sourceCellId = cell.cellId; layout.cells.Add(cell); }
        layout.streets.AddRange(core.streets);
        foreach (Vector3 dock in memberEntries)
        {
            // note: Connect to an actual central street instead of the centre of a building. Reject a blocked connector rather than drawing through a rigid assembly.
            bool connected = false;
            foreach (var street in core.streets)
            {
                Vector3 run = street.end - street.start;
                Vector3 join = street.start + run * Mathf.Clamp01(Vector3.Dot(dock - street.start, run) / Mathf.Max(.001f, run.sqrMagnitude));
                // note: Endpoints preserve connected alternatives when the nearest frontage point would cross another block.
                foreach (var candidate in new[] { join, street.start, street.end })
                {
                    // note: A short central street may face a block. Bounded right-angle alternatives keep its connector outside complete structures and their aprons.
                    foreach (var corner in new[] { candidate, new Vector3(dock.x, 0f, candidate.z), new Vector3(candidate.x, 0f, dock.z) })
                    {
                        bool blocked = false;
                        foreach (var cell in layout.cells)
                            if (SectorSegmentTouchesCell(candidate, corner, cell) || SectorSegmentTouchesCell(corner, dock, cell))
                            { blocked = true; break; }
                        if (blocked) continue;
                        if ((corner - candidate).sqrMagnitude > .001f)
                            layout.streets.Add(new YQProceduralStreet { start = candidate, end = corner, width = 4.5f });
                        if ((dock - corner).sqrMagnitude > .001f)
                            layout.streets.Add(new YQProceduralStreet { start = corner, end = dock, width = 4.5f });
                        connected = true; break;
                    }
                    if (connected) break;
                }
                if (connected) break;
            }
            if (!connected) { failure = "Accepted sector frontage has no unblocked connection to the owner streets."; return false; }
        }
        // note: The accepted reservation may overlap another sector, but complete assemblies and their foundation aprons must not overlap one another.
        for (int a = 0; a < layout.cells.Count; a++)
            for (int b = a + 1; b < layout.cells.Count; b++)
            {
                var first = layout.cells[a]; var second = layout.cells[b];
                if (Mathf.Abs(first.boundsCenter.x - second.boundsCenter.x) < (first.boundsSize.x + second.boundsSize.x) * .5f + 6f &&
                    Mathf.Abs(first.boundsCenter.z - second.boundsCenter.z) < (first.boundsSize.z + second.boundsSize.z) * .5f + 6f)
                { failure = "Accepted sector assemblies overlap their reserved foundation aprons."; return false; }
            }
        float radius = core.radius;
        foreach (var sector in layout.sectors) radius = Mathf.Max(radius, new Vector2(sector.center.x, sector.center.z).magnitude + sector.radius);
        layout.radius = radius;
        if (!ValidateRecord(layout, out failure)) return false;
        result = layout;
        return true;
    }

    private static bool SectorSegmentTouchesCell(Vector3 start, Vector3 end, YQProceduralCellPlacement cell)
    {
        // note: Clip the finite horizontal segment, not an infinite physics ray. Include half the road width plus the existing three-metre foundation apron.
        float lo = 0f, hi = 1f;
        bool Axis(float a, float b, float center, float extent)
        {
            float delta = b - a;
            if (Mathf.Abs(delta) < .0001f) return a >= center - extent && a <= center + extent;
            float first = (center - extent - a) / delta, last = (center + extent - a) / delta;
            if (first > last) { float swap = first; first = last; last = swap; }
            lo = Mathf.Max(lo, first); hi = Mathf.Min(hi, last); return lo <= hi;
        }
        return Axis(start.x, end.x, cell.boundsCenter.x, cell.boundsSize.x * .5f + 5.25f) &&
            Axis(start.z, end.z, cell.boundsCenter.z, cell.boundsSize.z * .5f + 5.25f);
    }

    public static bool HasIndependentStreamingReview(YQReviewedSemanticSiteManifest manifest)
    {
        // note: Capability discovery is limited to the selected manifest and never promotes candidate or missing reviews.
        if (manifest?.StreamingSite == null) return false;
        foreach (var zone in manifest.Zones)
            if (zone?.cellContractsV2 != null)
                foreach (var binding in zone.cellContractsV2)
                    if (binding?.independentAssembly?.reviewState == YQSemanticSiteReviewState.Approved) return true;
        return false;
    }

    public static int CountReviewedSectorAssemblies(YQReviewedSemanticSiteManifest manifest)
    {
        // note: Capability discovery never promotes a candidate or causes an accepted central layout to be replaced by a rejected sector attempt.
        if (manifest == null || !manifest.ReleaseEligible) return 0;
        IReadOnlyList<YQReviewedSemanticZoneRecord> zones = manifest.Zones;
        if (manifest.StreamingSite != null)
        {
            if (!TryGetIndependentStreamingZones(manifest, out var adapted, out _)) return 0;
            zones = adapted;
        }
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in zones)
        {
            if (zone == null || zone.prefab == null || !HasUsableExternalConnection(zone) || zone.cellContractsV2 == null) continue;
            foreach (var binding in zone.cellContractsV2)
            {
                var independent = binding?.independentAssembly;
                if (binding != null && string.Equals(binding.cellId, zone.stableId, StringComparison.OrdinalIgnoreCase) &&
                    binding.reviewState == YQSemanticSiteReviewState.Approved && binding.sourceSignature == manifest.SourceSignature &&
                    independent?.reviewState == YQSemanticSiteReviewState.Approved && independent.sourceSignature == manifest.SourceSignature &&
                    independent.completeStructuralDependencies && independent.foundationVerified &&
                    (manifest.StreamingSite == null ? zone.connectionSocketPaths?.Count > 0 : independent.externalConnectionPaths?.Count > 0))
                    ids.Add(zone.stableId);
            }
        }
        return ids.Count;
    }

    public static bool HasUsableExternalConnection(YQReviewedSemanticZoneRecord zone)
    {
        // note: Apply the solver's actual geometric prerequisite before selection; this does not certify traversal or gameplay functions.
        if (zone?.prefab == null || zone.connectionSocketPaths == null) return false;
        foreach (string path in zone.connectionSocketPaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            Transform socket = zone.prefab.transform.Find(path);
            if (socket != null && IsExternalConnection(zone.localBoundsCenter, zone.localBoundsSize,
                zone.prefab.transform.InverseTransformPoint(socket.position),
                zone.prefab.transform.InverseTransformDirection(socket.forward))) return true;
        }
        return false;
    }

    public static bool TryGetIndependentStreamingZones(YQReviewedSemanticSiteManifest manifest,
        out List<YQReviewedSemanticZoneRecord> zones, out string failure)
    {
        // note: Adapt reviewed streaming cells to the existing block solver; no Unity assets are cloned, moved or approved here.
        zones = new List<YQReviewedSemanticZoneRecord>();
        failure = string.Empty;
        if (manifest == null || manifest.StreamingSite == null || string.IsNullOrWhiteSpace(manifest.SourceSignature) ||
            manifest.SourceSignature != manifest.StreamingSite.SourceSignature)
        { failure = "Independent streaming requires matching semantic and streaming source signatures."; return false; }
        var cells = new Dictionary<string, YQAuthoredSiteStreamingCellRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in manifest.StreamingSite.Cells)
        {
            if (cell == null || string.IsNullOrWhiteSpace(cell.StableCellId) || cells.ContainsKey(cell.StableCellId))
            { failure = "Independent streaming source has a missing or duplicate cell identity."; return false; }
            cells.Add(cell.StableCellId, cell);
        }
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var zone in manifest.Zones)
        {
            if (zone?.cellContractsV2 == null) continue;
            foreach (var binding in zone.cellContractsV2)
            {
                var contract = binding?.independentAssembly;
                if (contract == null || contract.reviewState != YQSemanticSiteReviewState.Approved) continue;
                if (string.IsNullOrWhiteSpace(binding.cellId) || !seen.Add(binding.cellId) ||
                    !cells.TryGetValue(binding.cellId, out var cell) || zone.streamingCellIds == null ||
                    !zone.streamingCellIds.Exists(id => string.Equals(id, binding.cellId, StringComparison.OrdinalIgnoreCase)) ||
                    binding.sourceSignature != manifest.SourceSignature || contract.sourceSignature != manifest.SourceSignature ||
                    !contract.completeStructuralDependencies || !contract.foundationVerified ||
                    !cell.HasStructuralFoundation || cell.CellPrefab == null ||
                    contract.externalConnectionPaths == null || contract.externalConnectionPaths.Count == 0)
                { failure = "Incomplete or stale independent assembly contract: " + binding.cellId; return false; }
                zones.Add(new YQReviewedSemanticZoneRecord
                {
                    stableId = cell.StableCellId, displayName = zone.displayName,
                    districtFunction = zone.districtFunction, prefab = cell.CellPrefab,
                    authoredSourceOrigin = cell.AuthoredLocalPosition,
                    localBoundsCenter = cell.LocalBoundsCenter, localBoundsSize = cell.LocalBoundsSize,
                    sourceInstanceCount = cell.SourceInstanceCount, semanticTags = zone.semanticTags,
                    connectionSocketPaths = contract.externalConnectionPaths,
                    cellContractsV2 = new List<YQReviewedCellFunctionContractV2> { binding }
                });
            }
        }
        if (zones.Count == 0)
        { failure = "Kit has no reviewed independent streaming assemblies; authored fragments cannot form a procedural town."; return false; }
        return true;
    }

    public static bool ApplyPlacement(YQProceduralSettlementLayoutRecord layout, string cellId,
        Transform target, Vector3 origin)
    {
        // note: Runtime streaming and verification use the same saved position/rotation application without changing child geometry.
        if (target == null || !TryPlacement(layout, cellId, out var placement)) return false;
        target.localPosition = placement.position - origin;
        target.localRotation = Quaternion.Euler(0f, placement.yaw, 0f);
        return true;
    }

    public static bool TryResolveZonePlacement(YQProceduralSettlementLayoutRecord layout,
        YQReviewedSemanticZoneRecord zone, string seed, out YQProceduralCellPlacement placement)
    {
        // note: A semantic district may reference several streamed assemblies; residents must use a selected placed assembly, never the empty donor district.
        placement = null;
        if (zone == null || layout == null) return false;
        if (TryPlacement(layout, zone.stableId, out placement)) return true;
        uint best = uint.MaxValue;
        if (zone.streamingCellIds != null)
            foreach (string cellId in zone.streamingCellIds)
                if (TryPlacement(layout, cellId, out var candidate))
                {
                    uint rank = Hash(seed + "|resident-cell|" + cellId);
                    if (placement == null || rank < best ||
                        (rank == best && string.CompareOrdinal(candidate.cellId, placement.cellId) < 0))
                    { placement = candidate; best = rank; }
                }
        return placement != null;
    }

    public static bool TryBuild(IReadOnlyList<Cell> source, string seed,
        out YQProceduralSettlementLayoutRecord result, out string failure)
    {
        result = null; failure = string.Empty;
        if (source == null || source.Count == 0 || source.Count > 8)
        { failure = "A street block plan requires one to eight bounded cells."; return false; }
        var ordered = new List<Cell>(source.Count);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (Cell cell in source)
        {
            if (cell == null || string.IsNullOrWhiteSpace(cell.id) || !ids.Add(cell.id) ||
                !Finite(cell.size) || !Finite(cell.center) || !Finite(cell.entrance) || !Finite(cell.outward) ||
                cell.size.x <= 0 || cell.size.z <= 0 || cell.size.y <= 0 || cell.size.x > 240 || cell.size.z > 240 ||
                float.IsNaN(cell.datum) || float.IsInfinity(cell.datum) || new Vector2(cell.outward.x, cell.outward.z).sqrMagnitude < .5f)
            { failure = "Invalid cell envelope or external entrance."; return false; }
            ordered.Add(cell);
        }
        // note: Cell order changes geometry, not merely labels. Stable IDs break hash ties independently of input enumeration.
        ordered.Sort((a, b) => { int order = Hash(seed + "|block|" + a.id).CompareTo(Hash(seed + "|block|" + b.id));
            return order != 0 ? order : string.CompareOrdinal(a.id, b.id); });
        var layout = new YQProceduralSettlementLayoutRecord { seed = seed, version = VersionFor(seed), earthworkVersion = 1 };
        float width = 5f + Hash(seed + "|street") % 4;
        float[] cursor = { 0f, 0f };
        float minDatum = float.PositiveInfinity;
        Bounds aggregate = default;
        bool first = true;
        for (int i = 0; i < ordered.Count; i++)
        {
            Cell cell = ordered[i];
            int side = (i + (int)(Hash(seed + "|side") % 2)) % 2;
            float sign = side == 0 ? -1f : 1f;
            float yaw = Mathf.Atan2(-sign, 0f) * Mathf.Rad2Deg - Mathf.Atan2(cell.outward.x, cell.outward.z) * Mathf.Rad2Deg;
            Vector3 x = RotateYaw(new Vector3(cell.size.x, 0, 0), yaw), z = RotateYaw(new Vector3(0, 0, cell.size.z), yaw);
            Vector3 size = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(z.x), cell.size.y, Mathf.Abs(x.z) + Mathf.Abs(z.z));
            // note: Pack non-overlapping rigid blocks on two street frontages. Empty setback is reserved for the entry spur, not random props.
            // note: The street width excludes each foundation's three-metre apron; reserve additional transition run rather than compressing height changes into the road edge.
            float setback = (layout.version >= 4 ? 14f : 2f) + Hash(seed + "|setback|" + cell.id) % 5;
            Vector3 center = new Vector3(sign * (width * .5f + setback + size.x * .5f), cell.center.y, cursor[side] + size.z * .5f);
            Vector3 position = center - RotateYaw(cell.center, yaw);
            position.y = cell.datum;
            Vector3 actualCenter = position + RotateYaw(cell.center, yaw);
            var bounds = new Bounds(actualCenter, size);
            if (first) { aggregate = bounds; first = false; } else aggregate.Encapsulate(bounds);
            minDatum = Mathf.Min(minDatum, cell.datum);
            layout.cells.Add(new YQProceduralCellPlacement { cellId = cell.id, position = position, yaw = yaw,
                boundsCenter = actualCenter, boundsSize = size });
            Vector3 entry = position + RotateYaw(cell.entrance, yaw);
            // note: A declared portal buried inside the envelope is not permission to draw a road through the cell.
            if (!IsExternalConnection(cell.center, cell.size, cell.entrance, cell.outward))
            { failure = "External cell connection is not on its frontage: " + cell.id; return false; }
            layout.streets.Add(new YQProceduralStreet { start = new Vector3(0, 0, entry.z), end = new Vector3(entry.x, 0, entry.z), width = 3f });
            cursor[side] += size.z + 6f + Hash(seed + "|gap|" + cell.id) % 5;
        }
        float shift = aggregate.center.z;
        layout.origin = new Vector3(0, minDatum, 0);
        foreach (var cell in layout.cells) { cell.position.z -= shift; cell.boundsCenter.z -= shift; }
        foreach (var street in layout.streets) { street.start.z -= shift; street.end.z -= shift; }
        // note: Extend the connected spine beyond the last block to meet the accepted site's approach, keeping the site centre on a road.
        float endZ = Mathf.Max(cursor[0], cursor[1]) - shift;
        layout.streets.Add(new YQProceduralStreet { start = new Vector3(0, 0, -shift - 6f), end = new Vector3(0, 0, endZ), width = width });
        float maxX = Mathf.Max(Mathf.Abs(aggregate.min.x), Mathf.Abs(aggregate.max.x));
        float maxZ = Mathf.Max(shift + 6f, endZ);
        if (layout.version >= 2 && layout.cells.Count >= 4)
        {
            // note: Larger settlements get an alternate connected route around the blocks. Offset the full road width from geometry, not just its centreline.
            float west = aggregate.min.x - 6f, east = aggregate.max.x + 6f;
            float south = -shift - 6f, north = endZ;
            Vector3 sw = new Vector3(west, 0, south), se = new Vector3(east, 0, south);
            Vector3 nw = new Vector3(west, 0, north), ne = new Vector3(east, 0, north);
            layout.streets.Add(new YQProceduralStreet { start = sw, end = se, width = 4f });
            layout.streets.Add(new YQProceduralStreet { start = se, end = ne, width = 4f });
            layout.streets.Add(new YQProceduralStreet { start = ne, end = nw, width = 4f });
            layout.streets.Add(new YQProceduralStreet { start = nw, end = sw, width = 4f });
            maxX = Mathf.Max(Mathf.Abs(west), Mathf.Abs(east));
        }
        layout.radius = new Vector2(maxX + 3f, maxZ + 3f).magnitude;
        if (layout.radius > 210f) { failure = "Procedural blocks exceed the supported site footprint."; return false; }
        result = layout;
        return true;
    }

    public static bool IsExternalConnection(Vector3 center, Vector3 size, Vector3 entrance, Vector3 outward)
    {
        // note: Apply the same rotated-envelope rule used by street placement before choosing a portal; this is geometric eligibility, not a collision/path certificate.
        if (!Finite(center) || !Finite(size) || !Finite(entrance) || !Finite(outward) ||
            size.x <= 0f || size.y <= 0f || size.z <= 0f ||
            new Vector2(outward.x, outward.z).sqrMagnitude < .5f)
            return false;
        float yaw = 90f - Mathf.Atan2(outward.x, outward.z) * Mathf.Rad2Deg;
        Vector3 relativeEntry = RotateYaw(entrance - center, yaw);
        Vector3 x = RotateYaw(new Vector3(size.x, 0f, 0f), yaw);
        Vector3 z = RotateYaw(new Vector3(0f, 0f, size.z), yaw);
        float halfWidth = (Mathf.Abs(x.x) + Mathf.Abs(z.x)) * .5f;
        float halfDepth = (Mathf.Abs(x.z) + Mathf.Abs(z.z)) * .5f;
        return Mathf.Abs(relativeEntry.x - halfWidth) <= 1f &&
            Mathf.Abs(relativeEntry.z) <= halfDepth + 1f;
    }

    public static bool ValidateRecord(YQProceduralSettlementLayoutRecord record, out string failure)
    {
        failure = "Invalid persisted procedural layout.";
        if (record == null || !Enabled(record.seed) || record.version != VersionFor(record.seed) || !Finite(record.origin) ||
            float.IsNaN(record.radius) || float.IsInfinity(record.radius) || record.radius <= 0 || (record.version < 5 && record.radius > 210) ||
            record.cells == null || record.cells.Count == 0 || record.cells.Count > 8 ||
            record.streets == null || (record.version < 5 && record.streets.Count != record.cells.Count + 1 +
                (record.version >= 2 && record.cells.Count >= 4 ? 4 : 0))) return false;
        // note: A large owner is a union of accepted bounded sectors, not permission to expand the eight-cell payload or an individual assembly envelope.
        Dictionary<string, YQProceduralSiteSector> sectorMap = null;
        if (record.version >= 5)
        {
            if (string.IsNullOrWhiteSpace(record.sectorFootprintSignature) || record.sectors == null ||
                record.sectors.Count < 2 || record.sectors.Count > record.cells.Count ||
                record.streets.Count > record.cells.Count * 3 + 5 || record.streets.Count < record.cells.Count) return false;
            sectorMap = new Dictionary<string, YQProceduralSiteSector>(StringComparer.OrdinalIgnoreCase);
            foreach (var sector in record.sectors)
                if (sector == null || string.IsNullOrWhiteSpace(sector.id) || !Finite(sector.center) ||
                    float.IsNaN(sector.radius) || float.IsInfinity(sector.radius) || sector.radius <= 0f || sectorMap.ContainsKey(sector.id) ||
                    new Vector2(sector.center.x, sector.center.z).magnitude + sector.radius > record.radius + .01f)
                    return false;
                else sectorMap.Add(sector.id, sector);
        }
        // note: Reject unsupported or partially serialized earthwork profiles at the save boundary, before runtime terrain mutation.
        if (record.earthworkVersion < 0 || record.earthworkVersion > 2 || record.parcelGroundHeights == null ||
            (record.earthworkVersion == 0 && record.parcelGroundHeights.Count != 0) ||
            (record.parcelGroundHeights.Count != 0 && record.parcelGroundHeights.Count != record.cells.Count))
            return false;
        foreach (float height in record.parcelGroundHeights)
            if (float.IsNaN(height) || float.IsInfinity(height)) return false;
        var ids = new HashSet<string>(record.version >= 5 ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        var occupiedSectors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in record.cells)
        {
            if (cell == null || string.IsNullOrWhiteSpace(cell.cellId) || !ids.Add(cell.cellId) ||
                !Finite(cell.position) || !Finite(cell.boundsCenter) || !Finite(cell.boundsSize) ||
                cell.boundsSize.x <= 0 || cell.boundsSize.z <= 0 || cell.boundsSize.y <= 0 ||
                float.IsNaN(cell.yaw) || float.IsInfinity(cell.yaw)) return false;
            float x = Mathf.Abs(cell.boundsCenter.x) + cell.boundsSize.x * .5f;
            float z = Mathf.Abs(cell.boundsCenter.z) + cell.boundsSize.z * .5f;
            if (new Vector2(x, z).magnitude > record.radius + .01f) return false;
            if (sectorMap != null)
            {
                if (cell.sectorId == null || !sectorMap.TryGetValue(cell.sectorId, out var sector) ||
                    cell.boundsSize.x > 480f || cell.boundsSize.z > 480f) return false;
                float sx = Mathf.Abs(cell.boundsCenter.x - sector.center.x) + cell.boundsSize.x * .5f;
                float sz = Mathf.Abs(cell.boundsCenter.z - sector.center.z) + cell.boundsSize.z * .5f;
                if (new Vector2(sx, sz).magnitude + 3f > sector.radius + .01f) return false;
                occupiedSectors.Add(sector.id);
                if (record.version == 6 && (string.IsNullOrWhiteSpace(cell.sourceCellId) ||
                    cell.cellId != (cell.sectorId == record.sectors[0].id ? cell.sourceCellId : SectorInstanceId(cell.sectorId, cell.sourceCellId)))) return false;
            }
        }
        if (sectorMap != null && occupiedSectors.Count != sectorMap.Count) return false;
        if (record.version == 6)
        {
            // note: Every repeated source must also belong to the owner's selected central set; a serialized alias cannot introduce an unselected assembly.
            var centralSources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var cell in record.cells)
                if (cell.sectorId == record.sectors[0].id) centralSources.Add(cell.sourceCellId);
            foreach (var cell in record.cells) if (!centralSources.Contains(cell.sourceCellId)) return false;
        }
        foreach (var street in record.streets)
            if (street == null || !Finite(street.start) || !Finite(street.end) ||
                float.IsNaN(street.width) || float.IsInfinity(street.width) || street.width < 2 || street.width > 12 ||
                new Vector2(street.start.x, street.start.z).magnitude > record.radius ||
                new Vector2(street.end.x, street.end.z).magnitude > record.radius) return false;
        failure = string.Empty;
        return true;
    }

    public static bool TryPlacement(YQProceduralSettlementLayoutRecord layout, string id, out YQProceduralCellPlacement placement)
    {
        placement = null;
        if (layout?.cells == null) return false;
        // note: Match the case-insensitive stable-ID contract used by source selection and tolerate incomplete deserialized entries.
        foreach (var cell in layout.cells)
            if (cell != null && string.Equals(cell.cellId, id, StringComparison.OrdinalIgnoreCase))
            { placement = cell; return true; }
        return false;
    }

    public static string GeometrySignature(YQProceduralSettlementLayoutRecord layout)
    {
        // note: Include visible transforms, not the seed alone, when checking whether settlements actually differ.
        var text = new System.Text.StringBuilder(layout.kitId);
        if (layout.version >= 5) text.Append("|sectors:").Append(layout.sectorFootprintSignature);
        // note: A persisted terrace change changes geometry identity; legacy records retain their historical signature.
        if (layout.earthworkVersion != 0)
        {
            text.Append("|earthwork:").Append(layout.earthworkVersion);
            foreach (float height in layout.parcelGroundHeights)
                text.Append(':').Append(height.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        foreach (var cell in layout.cells)
            text.Append('|').Append(cell.cellId).Append(':').Append(cell.position.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(':').Append(cell.position.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                .Append(':').Append(cell.yaw.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        if (layout.version >= 2)
        {
            // note: V2 uniqueness includes actual roads; preserve the historical V1 signature for accepted legacy compositions.
            foreach (var street in layout.streets)
                text.Append("|road:").Append(street.start.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(street.start.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(street.end.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(street.end.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
                    .Append(':').Append(street.width.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        return Hash(text.ToString()).ToString("X8");
    }

    private static bool Finite(Vector3 v) => !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
        !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    private static Vector3 RotateYaw(Vector3 point, float degrees)
    {
        // note: Pure managed layout math can be tested outside the Unity editor and never instantiates engine objects.
        double radians = degrees * Math.PI / 180.0;
        float sin = (float)Math.Sin(radians), cos = (float)Math.Cos(radians);
        return new Vector3(cos * point.x + sin * point.z, point.y, -sin * point.x + cos * point.z);
    }
    private static uint Hash(string value)
    {
        // note: Avalanche the full seed before small bounded choices; raw FNV low bits correlated cell order, setbacks and street width.
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in value ?? string.Empty) hash = (hash ^ c) * 16777619;
            hash ^= hash >> 16; hash *= 0x85ebca6b;
            hash ^= hash >> 13; hash *= 0xc2b2ae35;
            return hash ^ (hash >> 16);
        }
    }
}
