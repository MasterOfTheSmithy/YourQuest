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
}

[Serializable]
public sealed class YQProceduralCellPlacement
{
    public string cellId;
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
                    if (cell != null) layoutIds.Add(cell.cellId);
            if (layout.kitId != manifest.KitId || layout.sourceSignature != manifest.SourceSignature ||
                layout.seed != seed || !layoutIds.SetEquals(selected) ||
                !YQProceduralSettlementLayout.ValidateRecord(layout, out string _))
            {
                report.mode = YQWorldConstructionMode.Unresolved;
                report.limitation = "Prepared layout identity, selection or geometry does not match the source contract.";
                return report;
            }
            report.mode = YQWorldConstructionMode.ProceduralBlocks;
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
        (seed.StartsWith(SeedPrefix, StringComparison.Ordinal) || seed.StartsWith(PriorIndependentSeedPrefix, StringComparison.Ordinal) || seed.StartsWith(PreviousSeedPrefix, StringComparison.Ordinal) || seed.StartsWith(LegacySeedPrefix, StringComparison.Ordinal));

    // note: Fresh worlds opt into independent streaming; existing v1/v2 seeds retain their original selection and geometry versions.
    public static bool UsesIndependentStreaming(string seed) => seed != null &&
        (seed.StartsWith(SeedPrefix, StringComparison.Ordinal) || seed.StartsWith(PriorIndependentSeedPrefix, StringComparison.Ordinal));
    private static int VersionFor(string seed) => seed != null && seed.StartsWith(SeedPrefix, StringComparison.Ordinal) ? 4 : UsesIndependentStreaming(seed) ? 3 :
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
        var plan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (plan?.settlements == null) return null;
        foreach (var settlement in plan.settlements)
        {
            if (settlement == null || !Enabled(settlement.deterministicSeed)) continue;
            if (string.Equals(settlement.proceduralLayout?.seed, seed, StringComparison.Ordinal) ||
                string.Equals(settlement.deterministicSeed, seed, StringComparison.Ordinal) ||
                seed.StartsWith(settlement.deterministicSeed + "|semantic_variant|", StringComparison.Ordinal)) return settlement;
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
        out YQProceduralSettlementLayoutRecord layout, out string failure)
    {
        layout = null;
        failure = string.Empty;
        if (!Enabled(seed)) return true;
        pending.Remove(seed);
        // note: Restore committed geometry before checking new-generation eligibility. A changed kit cannot silently revert a saved town to donor coordinates.
        var savedPlan = WorldStateManager.Instance?.State?.generatedWorldPlan;
        if (savedPlan?.settlements != null)
            foreach (var settlement in savedPlan.settlements)
            {
                var saved = settlement?.proceduralLayout;
                if (saved == null || !string.Equals(saved.seed, seed, StringComparison.Ordinal)) continue;
                if (saved.kitId != manifest.KitId || saved.sourceSignature != manifest.SourceSignature ||
                    saved.cells == null || saved.cells.Count != selected.Count || !ValidateRecord(saved, out failure))
                { failure = "Saved procedural cell layout no longer matches its source contract."; return false; }
                var savedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in saved.cells) if (cell != null) savedIds.Add(cell.cellId);
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
            if (!UsesIndependentStreaming(seed) || !HasIndependentStreamingReview(manifest)) return true;
            if (!TryGetIndependentStreamingZones(manifest, out var independentZones, out failure)) return false;
            layoutZones = independentZones;
        }
        var inputs = new List<Cell>();
        foreach (var zone in layoutZones)
        {
            if (zone == null || !selected.Contains(zone.stableId)) continue;
            Transform entrance = null;
            uint entranceScore = uint.MaxValue;
            string entrancePath = null;
            // note: Consume an explicitly declared external connection. Never infer habitation or a door from an asset name.
            if (zone.prefab != null && zone.connectionSocketPaths != null)
                foreach (string path in zone.connectionSocketPaths)
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
        if (!TryBuild(inputs, seed, out layout, out failure)) return false;
        layout.kitId = manifest.KitId;
        layout.sourceSignature = manifest.SourceSignature;
        pending[seed] = layout;
        return true;
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
            float.IsNaN(record.radius) || float.IsInfinity(record.radius) || record.radius <= 0 || record.radius > 210 ||
            record.cells == null || record.cells.Count == 0 || record.cells.Count > 8 ||
            record.streets == null || record.streets.Count != record.cells.Count + 1 +
                (record.version >= 2 && record.cells.Count >= 4 ? 4 : 0)) return false;
        // note: Reject unsupported or partially serialized earthwork profiles at the save boundary, before runtime terrain mutation.
        if (record.earthworkVersion < 0 || record.earthworkVersion > 2 || record.parcelGroundHeights == null ||
            (record.earthworkVersion == 0 && record.parcelGroundHeights.Count != 0) ||
            (record.parcelGroundHeights.Count != 0 && record.parcelGroundHeights.Count != record.cells.Count))
            return false;
        foreach (float height in record.parcelGroundHeights)
            if (float.IsNaN(height) || float.IsInfinity(height)) return false;
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cell in record.cells)
        {
            if (cell == null || string.IsNullOrWhiteSpace(cell.cellId) || !ids.Add(cell.cellId) ||
                !Finite(cell.position) || !Finite(cell.boundsCenter) || !Finite(cell.boundsSize) ||
                cell.boundsSize.x <= 0 || cell.boundsSize.z <= 0 || cell.boundsSize.y <= 0 ||
                float.IsNaN(cell.yaw) || float.IsInfinity(cell.yaw)) return false;
            float x = Mathf.Abs(cell.boundsCenter.x) + cell.boundsSize.x * .5f;
            float z = Mathf.Abs(cell.boundsCenter.z) + cell.boundsSize.z * .5f;
            if (new Vector2(x, z).magnitude > record.radius + .01f) return false;
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
