using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

// note: These tests execute compiled production validators with synthetic records, without Unity native objects or saves.
public static class DrainageChecks
{
    private static readonly List<object> Checks = new List<object>();
    private static int failures;
    private static readonly List<object> QueryOrderCoverage = new List<object>();
    private static string archiveRoot;
    private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Static;

    private static void Check(string id, Action test)
    {
        try { test(); Checks.Add(new { id, passed = true }); Console.WriteLine("PASS " + id); }
        catch (Exception error) { failures++; Checks.Add(new { id, passed = false, error = error.ToString() }); Console.WriteLine("FAIL " + id + ": " + error.Message); }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private static YQBlueprintPointV2 Point(float x, float z, float height) =>
        new YQBlueprintPointV2 { x = x, z = z, normalizedElevation = height / 140f, width = 8f };

    private static Dictionary<string, YQHydrologyFeatureV2> Network(float rise = 0f, float joinOffset = 0f)
    {
        var lake = new YQHydrologyFeatureV2 { hydrologyId = "lake", kind = YQHydrologyKindV2.Lake,
            waterLevelNormalized = 28f / 140f, nominalWidth = 80f, nominalDepth = 10f };
        lake.controlPoints.Add(Point(0f, 0f, 28f));
        var river = new YQHydrologyFeatureV2 { hydrologyId = "river", kind = YQHydrologyKindV2.River,
            sinkHydrologyId = "lake", nominalWidth = 8f, nominalDepth = 2f };
        river.controlPoints.Add(Point(-20f, -200f, 56f));
        river.controlPoints.Add(Point(-10f, -100f, 56f + rise));
        river.controlPoints.Add(Point(0f, 0f, 28f + joinOffset));
        return new Dictionary<string, YQHydrologyFeatureV2>(StringComparer.OrdinalIgnoreCase) { ["lake"] = lake, ["river"] = river };
    }

    private static YQSpatialBlueprintValidationResultV2 Validate(Dictionary<string, YQHydrologyFeatureV2> network, bool legacy)
    {
        var result = new YQSpatialBlueprintValidationResultV2();
        string method = legacy ? "ValidateReceivingWatershed" : "ValidatePhysicalDrainage";
        var boundary = typeof(YQSpatialBlueprintValidatorV2).GetMethod(method, Flags);
        Require(boundary != null, "Production drainage boundary is missing: " + method);
        boundary.Invoke(null, new object[] { network, result });
        return result;
    }

    private static bool Has(YQSpatialBlueprintValidationResultV2 result, string reason) =>
        result.Errors.Exists(error => error.Contains(reason, StringComparison.Ordinal));

    private static Dictionary<string, YQHydrologyFeatureV2> BasinNetwork()
    {
        // note: An explicit closed-basin fixture exercises the real new contract without inventing native water or terrain objects.
        var graph = Network();
        graph["river"].catchmentId = graph["lake"].catchmentId = "fixture:catchment";
        graph["river"].drainageOrder = 1;
        graph["lake"].basin = new YQHydrologyBasinV2 { disposition = YQBasinDispositionV2.Closed,
            closedBalance = YQClosedBasinBalanceV2.SeasonalStorageEvaporationAndSeepage };
        return graph;
    }

    private static YQSpatialBlueprintValidationResultV2 ValidateBasins(Dictionary<string, YQHydrologyFeatureV2> graph)
    {
        var result = new YQSpatialBlueprintValidationResultV2();
        typeof(YQSpatialBlueprintValidatorV2).GetMethod("ValidateBasinConnectivity", Flags).Invoke(null, new object[] { graph, result });
        return result;
    }

    private static void ChannelWetFootprintChecks()
    {
        // note: Execute the actual production envelope with measured-metre fixtures; no native terrain or copied terrain formula stands in for the implementation.
        var type = typeof(YQContinuousWorldFeatureAuthority).GetNestedType("AcceptedWaterTerrainEnvelope", BindingFlags.NonPublic);
        var add = type.GetMethod("AddCanonicalChannel", BindingFlags.NonPublic | BindingFlags.Instance);
        var legacy = type.GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance);
        var project = type.GetMethod("Project", BindingFlags.NonPublic | BindingFlags.Instance);
        var core = type.GetProperty("HasWetCore", BindingFlags.NonPublic | BindingFlags.Instance);
        object Envelope(float distance, float outside)
        {
            object value = Activator.CreateInstance(type);
            add.Invoke(value, new object[] { 140f, .5f, 2f, 4f, distance, outside });
            return value;
        }
        float Apply(object value, float height) => (float)project.Invoke(value, new object[] { height });
        Check("CHANNEL-CENTER-ACCEPTED-DEPTH", () => Require(Math.Abs(Apply(Envelope(0f, 0f), .7f) * 140f - 68f) <= .0001f,
            "Centre lost its accepted two-metre depth."));
        Check("CHANNEL-LATERAL-BANK-MEETS-WATER-DATUM", () => Require(Math.Abs(Apply(Envelope(4f, 0f), .7f) - .5f) <= .000001f,
            "Accepted bank remains submerged or rises above its water datum."));
        Check("CHANNEL-DRY-PADDING-CANNOT-CARVE", () => {
            object value = Envelope(5f, 1f);
            Require(!(bool)core.GetValue(value) && Apply(value, .48f) >= .5f,
                "Outside-ribbon native padding still owns a submerged bed.");
            float once = Apply(value, .8f);
            Require(Math.Abs(once - Apply(value, once)) <= .000001f, "Dry bank projection is not idempotent.");
        });
        Check("CHANNEL-FINITE-ENDPOINT-USES-TRIANGLE-COVERAGE", () => {
            object value = Envelope(0f, 2f);
            Require(!(bool)core.GetValue(value) && Apply(value, .48f) >= .5f,
                "A point beyond the finite cap became wet from its short centreline distance.");
        });
        Check("CHANNEL-ARCHIVED-PADDING-REMAINS-READABLE", () => {
            object value = Activator.CreateInstance(type);
            legacy.Invoke(value, new object[] { 140f, .5f, 2f, 4f, 5f });
            Require((bool)core.GetValue(value) && Apply(value, .7f) < .5f,
                "Archived padded geometry was silently replaced.");
        });
    }

    private static void BasinChecks(string output)
    {
        Check("BASIN-CLOSED-EXPLICIT-BALANCE", () => Require(ValidateBasins(BasinNetwork()).Accepted, "Valid explicit closed basin rejected."));
        Check("BASIN-UNDECLARED-TERMINAL-REJECTS", () => {
            var graph = BasinNetwork(); graph["lake"].basin = null;
            Require(Has(ValidateBasins(graph), "unspecified_basin"), "Implicit terminal lake admitted by the new generation contract.");
        });
        Check("BASIN-CLOSED-WITH-OUTLET-REJECTS", () => {
            var graph = BasinNetwork(); graph["lake"].sinkHydrologyId = "river";
            Require(Has(ValidateBasins(graph), "invalid_closed_basin_balance"), "Closed basin with a declared outlet admitted.");
        });
        Check("BASIN-CLOSED-UNKNOWN-BALANCE-REJECTS", () => {
            var graph = BasinNetwork(); graph["lake"].basin.closedBalance = (YQClosedBasinBalanceV2)99;
            Require(Has(ValidateBasins(graph), "invalid_closed_basin_balance"), "Unsupported loss model admitted.");
        });
        Check("BASIN-STRICTLY-DECREASING-ORDER", () => {
            var graph = BasinNetwork(); graph["river"].drainageOrder = 0;
            Require(Has(ValidateBasins(graph), "nondecreasing_catchment_edge"), "Nondecreasing drainage rank admitted.");
            graph["river"].drainageOrder = int.MaxValue;
            Require(Has(ValidateBasins(graph), "invalid_catchment_order"), "Unbounded drainage rank admitted.");
        });
        Check("BASIN-CATCHMENT-EDGE-IDENTITY", () => {
            var graph = BasinNetwork(); graph["river"].catchmentId = "another";
            Require(Has(ValidateBasins(graph), "nondecreasing_catchment_edge"), "Unagreed catchment edge admitted.");
        });
        Check("BASIN-CHANNEL-METADATA-REJECTS", () => {
            var graph = BasinNetwork(); graph["river"].basin = graph["lake"].basin;
            Require(Has(ValidateBasins(graph), "channel_basin_metadata"), "Channel pretending to be a terminal basin admitted.");
        });
        Check("BASIN-INACTIVE-SPILL-DEFAULTS", () => {
            var graph = BasinNetwork(); graph["lake"].basin.spillPoint.x = float.NaN;
            Require(Has(ValidateBasins(graph), "invalid_stored_spill"), "Inactive nonfinite spill storage admitted.");
            graph["lake"].basin.spillPoint.x = 1f;
            Require(Has(ValidateBasins(graph), "invalid_stored_spill"), "Inactive nondefault spill storage admitted.");
        });
        Check("BASIN-COMPILED-SPILL-AND-HASH", () => {
            var compiled = Compile("watershed-alpha", false);
            var graph = new Dictionary<string, YQHydrologyFeatureV2>(StringComparer.OrdinalIgnoreCase);
            foreach (var feature in compiled.blueprint.hydrology) graph.Add(feature.hydrologyId, feature);
            var lake = graph["water:lake:opening"];
            var outlet = graph["water:river:lake_outlet"];
            Require(lake.basin.disposition == YQBasinDispositionV2.Open && lake.sinkHydrologyId == outlet.hydrologyId,
                "Production compiler did not connect the opening lake to its physical outlet.");
            Require(ValidateBasins(graph).Accepted, "Compiled basin graph rejected.");
            foreach (var change in new Action[] {
                () => lake.basin.spillPoint.x += 1f,
                () => lake.basin.spillPoint.normalizedElevation += .02f / 140f,
                () => lake.catchmentId += "changed",
                () => lake.drainageOrder += 1,
                () => lake.basin.hasSpill = false,
                () => lake.basin.disposition = YQBasinDispositionV2.Closed,
                () => lake.basin.closedBalance = YQClosedBasinBalanceV2.SeasonalStorageEvaporationAndSeepage }) {
                var before = JsonSerializer.Serialize(compiled, new JsonSerializerOptions { IncludeFields = true });
                change();
                Require(compiled.contentHash != YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(compiled), "Basin mutation omitted from accepted hash.");
                compiled = JsonSerializer.Deserialize<GeneratedSpatialWorldPlanV2Record>(before, new JsonSerializerOptions { IncludeFields = true });
                graph.Clear(); foreach (var feature in compiled.blueprint.hydrology) graph.Add(feature.hydrologyId, feature);
                lake = graph["water:lake:opening"];
            }
            lake.basin.spillPoint.x += 1f;
            Require(Has(ValidateBasins(graph), "disconnected_spill"), "Spill disconnected from the outgoing channel admitted.");
            lake.basin.spillPoint.normalizedElevation += .02f / 140f;
            Require(Has(ValidateBasins(graph), "invalid_spill_level"), "Two-centimetre spill mismatch admitted.");
            lake.basin.hasSpill = false;
            Require(Has(ValidateBasins(graph), "invalid_spill_level"), "Missing spill admitted.");
        });
        Check("BASIN-LEGACY-GOLDEN-HASHES", () => {
            // note: Read immutable pre-change production outputs and the archived disposable accepted world, never live saves.
            string root = archiveRoot;
            foreach (string seed in new[] { "semantic-v2-318", "33b9e87d", "watershed-alpha", "watershed-beta" }) {
                var record = JsonSerializer.Deserialize<GeneratedSpatialWorldPlanV2Record>(File.ReadAllText(
                    Path.Combine(root, "persistence_regressions", "generated-" + seed + ".json")), new JsonSerializerOptions { IncludeFields = true });
                Require(record.generationVersion == "spatial_world_plan_v2_topology_10", "Golden source has wrong generation identity.");
                Require(record.contentHash == YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(record), "Old generation canonical hash changed.");
                Require(YQSpatialBlueprintValidatorV2.Validate(record).Accepted, "Old accepted blueprint no longer validates.");
            }
            // note: Unity-authored ordinary hashes require Unity's float formatting; their exact preservation is checked by the Editor router test, not by a different CLR's round-trip formatter.
        });
        Check("BASIN-PRODUCTION-GATE-CANNOT-DOWNGRADE", () => {
            var record = Compile("watershed-alpha", false);
            record.validationVersion = GeneratedSpatialWorldPlanV2Record.LegacyValidationVersion;
            record.blueprint.hydrology.Find(feature => feature.kind == YQHydrologyKindV2.Lake).basin = null;
            Require(Has(YQSpatialBlueprintValidatorV2.Validate(record), "unspecified_basin"), "New generation bypassed its basin contract with an older gate label.");
        });
    }

    private static void ConnectedDomainChecks(string output)
    {
        // note: Exercise terrain-connected holes, isolated puddles and cell seams with pure production geometry before any native rendering claim.
        Check("CHANNEL-COVERAGE-INDEPENDENT-OF-LAKE-OWNER-AND-PADDING", () => {
            // note: Exercise canonical triangles, not the padded semantic mask: overlap must still identify the channel, while bank padding and endpoint caps must remain excluded.
            var blueprint = new YQSpatialBlueprintV2();
            var lake = new YQHydrologyFeatureV2 { hydrologyId = "coverage:lake", kind = YQHydrologyKindV2.Lake,
                nominalWidth = 80f, nominalDepth = 6f, waterLevelNormalized = .2f };
            lake.controlPoints.Add(Point(0f, 0f, 28f));
            var channel = new YQHydrologyFeatureV2 { hydrologyId = "coverage:river", kind = YQHydrologyKindV2.River,
                nominalWidth = 14f, nominalDepth = 4.5f };
            channel.controlPoints.Add(Point(0f, -100f, 42f));
            channel.controlPoints.Add(Point(0f, 100f, 42f));
            blueprint.hydrology.Add(lake); blueprint.hydrology.Add(channel);
            var method = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareBlueprint", Flags);
            object[] arguments = { blueprint, false, null, null };
            Require((bool)method.Invoke(null, arguments), "Coverage fixture preparation failed.");
            var sampler = (YQSpatialBlueprintTerrainSamplerV2)arguments[2];
            Require(sampler.Sample(0f, 0f).waterFeatureIndex == 0, "Fixture does not exercise hidden channel ownership.");
            Require(sampler.TrySampleAcceptedChannelSurface(0f, 0f, .2f, out int owner, out float surface) && owner == 1 &&
                Math.Abs(surface - .2f) * 140f < .01f, "Lake-owned channel opening was hidden or given another datum.");
            Require(sampler.TrySampleAcceptedChannelSurface(6.9f, 0f, .2f, out _, out _), "Interior ribbon edge was lost.");
            Require(!sampler.TrySampleAcceptedChannelSurface(7.1f, 0f, .2f, out _, out _), "Dry bank padding counted as water geometry.");
            Require(!sampler.TrySampleAcceptedChannelSurface(0f, 104f, .3f, out _, out _), "Rounded endpoint cap counted as a rendered triangle.");
            Require(!sampler.TrySampleAcceptedChannelSurface(float.NaN, 0f, .2f, out owner, out surface) && owner == -1 && surface == 0f,
                "Nonfinite coverage query admitted or left unsafe outputs.");
        });
        Check("DOMAIN-NATIVE-DIAGONAL-AND-LEGACY-GEOMETRY", () => {
            // note: The saddle fixture has distinct heights for both diagonals; legacy triangle membership is constructed independently from its stored version-1 rule.
            const int n = 5;
            var heights = new float[n, n];
            for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++) heights[z, x] = x == 0 || z == 0 || x == n - 1 || z == n - 1 ? .99f : .2f;
            heights[2, 2] = .1f; heights[2, 3] = .4f; heights[3, 2] = .7f; heights[3, 3] = .2f;
            Require(YQPreparedConnectedBasinV2.TryCreate(0f, 0f, 2f, n, 4f, 4f, .9f,
                (x,z) => heights[(int)(z / 2f), (int)(x / 2f)], out var native, out string reason), reason);
            Require(native.version == YQPreparedConnectedBasinV2.NativeChannelReachVersion, "New domain has no pinned native-join version.");
            Require(YQPreparedConnectedBasinV2.TryPrepare(native, .9f, out var prepared, out reason), reason);
            Require(prepared.TrySampleBed(4.5f, 4.5f, out float sampled, out _) && Math.Abs(sampled - .125f) < .000001f,
                "New domain disagrees with the measured native diagonal.");
            // note: Every retained native-diagonal version must preserve stored bed interpretation; preparation must not migrate accepted domain bytes.
            for (int version = 2; version < YQPreparedConnectedBasinV2.NativeChannelReachVersion; version++)
            {
                native.version = version;
                string stored = JsonSerializer.Serialize(native, new JsonSerializerOptions { IncludeFields = true });
                Require(YQPreparedConnectedBasinV2.TryPrepare(native, .9f, out prepared, out reason), reason);
                Require(prepared.TrySampleBed(4.5f, 4.5f, out sampled, out _) && Math.Abs(sampled - .125f) < .000001f,
                    "Stored native-version-" + version + " triangular geometry changed.");
                Require(JsonSerializer.Serialize(native, new JsonSerializerOptions { IncludeFields = true }) == stored,
                    "Preparation rewrote archived version " + version);
            }
            native.version = 1; native.wetTriangles.Clear();
            for (int z = 0; z < n - 1; z++)
            for (int x = 0; x < n - 1; x++)
            {
                if (heights[z, x] < .9f || heights[z + 1, x] < .9f || heights[z, x + 1] < .9f)
                    native.wetTriangles.Add(2 * (z * (n - 1) + x));
                if (heights[z, x + 1] < .9f || heights[z + 1, x] < .9f || heights[z + 1, x + 1] < .9f)
                    native.wetTriangles.Add(2 * (z * (n - 1) + x) + 1);
            }
            Require(YQPreparedConnectedBasinV2.TryPrepare(native, .9f, out prepared, out reason), reason);
            Require(prepared.TrySampleBed(4.5f, 4.5f, out sampled, out _) && Math.Abs(sampled - .325f) < .000001f,
                "Stored legacy triangular geometry changed.");
            native.version = 999;
            Require(!YQPreparedConnectedBasinV2.TryPrepare(native, .9f, out _, out _), "Unknown domain version admitted.");
        });
        float Bed(float x, float z)
        {
            float basin = .25f + (x * x / 900f + z * z / 576f) * .35f;
            float island = .7f - ((x - 6f) * (x - 6f) + z * z) * .016f;
            float isolated = .35f + ((x - 48f) * (x - 48f) + (z - 40f) * (z - 40f)) * .006f;
            return Math.Min(.9f, Math.Min(Math.Max(basin, island), isolated));
        }
        YQConnectedBasinDomainV2 Make()
        {
            Require(YQPreparedConnectedBasinV2.TryCreate(-64f, -64f, 2f, 65, -10f, 0f, .5f, Bed, out var data, out string reason), reason);
            return data;
        }
        YQPreparedConnectedBasinV2 Prepare(YQConnectedBasinDomainV2 data)
        {
            Require(YQPreparedConnectedBasinV2.TryPrepare(data, .5f, out var prepared, out string reason), reason); return prepared;
        }
        Check("DOMAIN-CONNECTED-WET-ISLAND-AND-ISOLATED-PUDDLE", () => {
            var prepared = Prepare(Make());
            Require(prepared.SignedShoreDistance(-10f, 0f) < 0f, "Wet seed not selected.");
            Require(prepared.SignedShoreDistance(6f, 0f) > 0f, "Dry island flooded.");
            Require(prepared.SignedShoreDistance(48f, 40f) > 0f, "Disconnected depression flooded.");
        });
        Check("DOMAIN-CONTAINMENT-AND-DRY-SEED-REJECT", () => {
            Require(!YQPreparedConnectedBasinV2.TryCreate(-4f, -4f, 1f, 9, 0f, 0f, .5f, (x,z) => .2f, out _, out string reason)
                && reason == "basin_reaches_planning_boundary", "Uncontained domain admitted.");
            Require(!YQPreparedConnectedBasinV2.TryCreate(-4f, -4f, 1f, 9, 0f, 0f, .5f, (x,z) => .7f, out _, out reason)
                && reason == "basin_seed_is_dry", "Dry seed admitted.");
        });
        Check("DOMAIN-MALFORMED-PERSISTED-GEOMETRY", () => {
            foreach (var change in new Action<YQConnectedBasinDomainV2>[] { data => data.resolution = int.MaxValue,
                data => data.spacing = float.NaN, data => data.bed[5] = float.PositiveInfinity,
                data => data.wetTriangles.Reverse(), data => data.wetTriangles.Add(data.wetTriangles[0]),
                data => data.wetTriangles.RemoveAt(0), data => data.seedX = data.minimumX })
            {
                var data = Make(); change(data);
                Require(!YQPreparedConnectedBasinV2.TryPrepare(data, .5f, out _, out _), "Malformed domain admitted.");
            }
        });
        Check("DOMAIN-IMMUTABLE-PREPARED-PROJECTION", () => {
            var data = Make(); var prepared = Prepare(data);
            float before = prepared.SignedShoreDistance(6f, 0f);
            data.bed.Clear(); data.wetTriangles.Clear(); data.minimumX = 500f;
            Require(before == prepared.SignedShoreDistance(6f, 0f), "Prepared domain depends on mutable save data.");
        });
        Check("DOMAIN-SHORE-LIP-AT-FIXED-PLANE", () => {
            var prepared = Prepare(Make());
            for (int i = 0; i < prepared.ShoreSegmentCount; i++)
            {
                prepared.GetShoreSegment(i, out var a, out var b);
                foreach (var p in new[] { a, (a+b)*.5f, b })
                {
                    Require(prepared.TrySampleBed(p.x, p.y, out float height, out _) && Math.Abs(height - .5f) * 140f <= .01f,
                        "Shore does not lie on its accepted bed/water intersection.");
                }
            }
            Require(prepared.TryFirstShoreCrossing(new UnityEngine.Vector2(-10f, 0f), UnityEngine.Vector2.left, out var spill)
                && Math.Abs(prepared.SignedShoreDistance(spill.x, spill.y)) <= .01f, "Ray spill misses the contour.");
        });
        Check("DOMAIN-CLIPPED-MESH-HOLES-WINDING-AND-AREA", () => {
            var prepared = Prepare(Make());
            double Area(float minX, float minZ, float maxX, float maxZ)
            {
                var vertices = new List<UnityEngine.Vector2>(); var triangles = new List<int>();
                prepared.AppendClippedTriangles(minX, minZ, maxX, maxZ, vertices, triangles);
                double area = 0d;
                for (int t = 0; t < triangles.Count; t += 3)
                {
                    UnityEngine.Vector2 a = vertices[triangles[t]], b = vertices[triangles[t+1]], c = vertices[triangles[t+2]], p = (a+b+c)/3f;
                    float cross = (b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
                    Require(cross < 0f, "Water triangle winding is not upward.");
                    Require(prepared.TrySampleBed(p.x, p.y, out float bed, out bool wet) && wet && bed < .5f, "Mesh covers dry land.");
                    foreach (var v in new[] { a, b, c }) Require(v.x >= minX && v.x <= maxX && v.y >= minZ && v.y <= maxZ, "Mesh leaves its cell.");
                    area += -cross * .5d;
                }
                return area;
            }
            double whole = Area(-64f, -64f, 64f, 64f);
            double cells = Area(-64f,-64f,0f,0f)+Area(0f,-64f,64f,0f)+Area(-64f,0f,0f,64f)+Area(0f,0f,64f,64f);
            Require(Math.Abs(whole - cells) < .01d, "Clipping introduced a cell seam gap or overlap.");
            File.WriteAllText(Path.Combine(output, "connected-domain-geometry.json"), JsonSerializer.Serialize(new { wholeArea=whole, clippedArea=cells, difference=Math.Abs(whole-cells), prepared.PolygonCount, prepared.ShoreSegmentCount }));
        });
        Check("DOMAIN-DETERMINISTIC-SERIALIZATION", () => Require(JsonSerializer.Serialize(Make(), new JsonSerializerOptions { IncludeFields=true }) ==
            JsonSerializer.Serialize(Make(), new JsonSerializerOptions { IncludeFields=true }), "Repeated domain differs."));
        Check("DOMAIN-SHORE-INDEX-MATCHES-EXHAUSTIVE-QUERY", () => {
            var prepared = Prepare(Make());
            for (int z = -20; z <= 20; z++)
                for (int x = -20; x <= 20; x++)
                {
                    var p = new UnityEngine.Vector2(x * 3.71f + .193f, z * 3.97f - .271f);
                    float expected = float.PositiveInfinity;
                    for (int edge = 0; edge < prepared.ShoreSegmentCount; edge++)
                    {
                        prepared.GetShoreSegment(edge, out var a, out var b);
                        var delta = b - a;
                        float t = UnityEngine.Mathf.Clamp01(UnityEngine.Vector2.Dot(p-a, delta) / delta.sqrMagnitude);
                        expected = Math.Min(expected, (p-a-delta*t).sqrMagnitude);
                    }
                    Require(Math.Abs(Math.Abs(prepared.SignedShoreDistance(p.x,p.y))-Math.Sqrt(expected)) <= .00001f,
                        "Indexed shore differs from exhaustive segment projection.");
                }
        });
        Check("DOMAIN-COMPLETE-CANONICAL-HASH-COVERAGE", () => {
            foreach (var change in new Action<YQHydrologyFeatureV2>[] {
                water => water.hasConnectedDomain = false, water => water.connectedDomain.version++,
                water => water.connectedDomain.resolution--, water => water.connectedDomain.spacing += .01f,
                water => water.connectedDomain.minimumX += .01f, water => water.connectedDomain.minimumZ += .01f,
                water => water.connectedDomain.seedX += .01f, water => water.connectedDomain.seedZ += .01f,
                water => water.connectedDomain.bed[0] += .001f, water => water.connectedDomain.wetTriangles.RemoveAt(0) })
            {
                var record = Compile("watershed-alpha", false);
                var water = record.blueprint.hydrology.Find(feature => feature.hasConnectedDomain);
                change(water);
                Require(record.contentHash != YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(record), "Domain decision omitted from accepted hash.");
            }
        });
        Check("DOMAIN-SHARED-PROJECTION-CANNOT-MIX-HISTORICAL-VERSIONS", () => {
            var record = Compile("watershed-alpha", false);
            var waters = record.blueprint.hydrology.FindAll(feature => feature.hasConnectedDomain);
            Require(waters.Count == 2, "Shared projection fixture has no two-basin boundary.");
            waters[0].connectedDomain.version = 2;
            Require(Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Mixed runtime projection versions admitted.");
            waters[1].connectedDomain.version = 2;
            Require(!Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Entire historical version-2 artifact was rejected as mixed.");
            waters[0].connectedDomain.version = 3;
            waters[1].connectedDomain.version = 4;
            Require(Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Mixed version-3/version-4 bank interpretation admitted.");
            waters[1].connectedDomain.version = 3;
            Require(!Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Entire version-3 archive was rejected as mixed.");
            waters[0].connectedDomain.version = 4;
            waters[1].connectedDomain.version = 5;
            Require(Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Mixed version-4/version-5 bed interpretation admitted.");
            waters[0].connectedDomain.version = 5;
            Require(!Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Complete physical wet-footprint version rejected as mixed.");
            waters[1].connectedDomain.version = 6;
            Require(Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Mixed version-5/version-6 native joins admitted.");
            waters[0].connectedDomain.version = 6;
            Require(!Has(YQSpatialBlueprintValidatorV2.Validate(record), "mixed_connected_projection_versions"), "Complete native join rejected as mixed.");
        });
        Check("DOMAIN-TOPOLOGY-11-GOLDEN-HASH", () => {
            string root = archiveRoot;
            var archived = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(Path.Combine(root,"20261011_101211_592_unity_basins","new-draft.json")));
            Require(archived.spatialPlanV2.generationVersion == GeneratedSpatialWorldPlanV2Record.BasinMetadataGenerationVersion, "Wrong topology-11 archive.");
            Require(YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(archived.spatialPlanV2) == "455a452418ecdd5e", "Topology-11 checksum changed.");
            Require(YQSpatialBlueprintValidatorV2.Validate(archived.spatialPlanV2).Accepted, "Retained topology-11 blueprint no longer validates.");
        });
    }

    private static void ChunkQueryOrderChecks(GeneratedSpatialWorldPlanV2Record record, string seed)
    {
        // note: Reconstruct independent accepted inputs, then visit chunk-shaped source samples in four orders. This is managed query/reconstruction coverage, not actual streamed publication or a save reload.
        int[][] orders = {
            new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15 },
            new[] { 15, 14, 13, 12, 11, 10, 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 },
            new[] { 0, 1, 2, 3, 7, 6, 5, 4, 8, 9, 10, 11, 15, 14, 13, 12 },
            new[] { 5, 6, 10, 9, 0, 1, 2, 3, 7, 11, 15, 14, 13, 12, 8, 4 }
        };
        string[] names = { "row_major", "reverse", "serpentine", "centre_out" };
        var options = new JsonSerializerOptions { IncludeFields = true };
        string canonical = JsonSerializer.Serialize(record, options);
        var reference = new Dictionary<(float, float), string>();
        // note: Blueprint worldSize covers the macro world (16 km in these fixtures); native representation tiles remain 256 m. Sparse macro-world quadrants can miss every accepted channel.
        const float size = 256f, half = 512f;
        float[] offsets = { 0f, .002f, size / 4f, size / 2f, size * .75f, size - .002f, size };
        var prepare = typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareBlueprint", Flags);
        Require(prepare != null, "Production source blueprint preparation is missing.");
        for (int order = 0; order < orders.Length; order++)
        {
            var restored = JsonSerializer.Deserialize<GeneratedSpatialWorldPlanV2Record>(canonical, options);
            object[] arguments = { restored.blueprint, true, null, null };
            Require((bool)prepare.Invoke(null, arguments), "Source projection preparation failed: " + arguments[3]);
            var sampler = (YQSpatialBlueprintTerrainSamplerV2)arguments[2];
            int queries = 0, wet = 0, dry = 0;
            var witnesses = new List<(float x, float z, bool wet)>();
            foreach (var water in restored.blueprint.hydrology)
            {
                if (!water.hasConnectedDomain) continue;
                var domain = water.connectedDomain;
                Require(YQPreparedConnectedBasinV2.TryPrepare(domain, water.waterLevelNormalized, out var basin, out string failure), failure);
                Require(basin.TrySampleBed(domain.seedX, domain.seedZ, out _, out bool inside) && inside,
                    "Accepted basin seed is not a wet source witness.");
                witnesses.Add((domain.seedX, domain.seedZ, true));
                bool foundDry = false;
                foreach (float x in new[] { basin.MinimumX, basin.MaximumX })
                foreach (float z in new[] { basin.MinimumZ, basin.MaximumZ })
                {
                    // note: A corner outside the accepted basin and every other water footprint supplies a dry witness without inferring dryness from height alone.
                    basin.TrySampleBed(x, z, out _, out bool inBasin);
                    if (inBasin || sampler.Sample(x, z).waterMask > 0f) continue;
                    witnesses.Add((x, z, false)); foundDry = true;
                }
                Require(foundDry, "Accepted basin has no dry domain-corner witness.");
            }
            Require(witnesses.Count > 0, "No accepted basin witnesses available.");
            void Observe(float x, float z, int revisit, bool? expectedWet = null)
            {
                var sample = sampler.Sample(x, z);
                if (expectedWet.HasValue) Require((sample.waterMask > 0f) == expectedWet.Value,
                    "Accepted wet/dry witness changed at " + x + "," + z);
                float[] values = { sample.elevationNormalized, sample.ruggedness, sample.waterMask,
                    sample.waterSurfaceNormalized, sample.bankSupportMask, sample.connectedBasinSupportMask,
                    sample.routeMask, sample.siteReserveMask, sample.caveMassMask };
                foreach (float value in values) Require(float.IsFinite(value), "Nonfinite source sample at " + x + "," + z);
                string signature = sample.waterFeatureIndex + ":" + string.Join(",", Array.ConvertAll(values, BitConverter.SingleToInt32Bits));
                if (reference.TryGetValue((x, z), out string expected))
                    Require(signature == expected, "Source sample changed for " + seed + ", order=" + names[order] +
                        ", revisit=" + revisit + ", point=" + x + "," + z);
                else { Require(order == 0 && revisit == 0, "Unexpected query coverage change."); reference.Add((x, z), signature); }
                queries++; if (sample.waterMask > 0f) wet++; else dry++;
            }
            for (int revisit = 0; revisit < 3; revisit++)
            {
                foreach (int cell in orders[order])
                foreach (float dz in offsets)
                foreach (float dx in offsets)
                    Observe(-half + cell % 4 * size + dx, -half + cell / 4 * size + dz, revisit);
                foreach (var witness in witnesses) Observe(witness.x, witness.z, revisit, witness.wet);
            }
            Require(wet > 0 && dry > 0, "Order fixture did not exercise both water and dry source terrain.");
            Require(JsonSerializer.Serialize(restored, options) == canonical, "Source projection mutated reconstructed accepted geometry.");
            QueryOrderCoverage.Add(new { seed, order = names[order], cells = 16, queries, uniquePoints = reference.Count,
                revisits = 3, witnessPoints = witnesses.Count, tileSizeMetres = size, wet, dry, passed = true });
        }
        Require(JsonSerializer.Serialize(record, options) == canonical, "Source query coverage mutated the accepted input.");
    }

    private static GeneratedWorldPlanRecord Fixture(string seed)
    {
        // note: Reuse the existing semantic verifier's fixture shape; this is generated test input, never an accepted player world.
        var plan = new GeneratedWorldPlanRecord { worldSeed = seed, summary = "high fantasy river kingdom", designNotes = "semantic authority fixture" };
        plan.EnsureCollections();
        for (int i = 0; i < 3; i++) {
            plan.regions.Add(new GeneratedRegionRecord { regionId = "fixture_region_" + i,
                terrainProfile = i == 1 ? "mountain ridge" : "river forest", climateProfile = i == 2 ? "wet temperate" : "seasonal",
                gameplayPremise = "trade kingdom", biomeTags = new List<string> { i == 1 ? "highland" : "forest" } });
            plan.settlements.Add(new GeneratedSettlementRecord { settlementId = "fixture_settlement_" + i,
                regionId = "fixture_region_" + i, displayName = i == 0 ? "Fixture Gate" : "Fixture Village " + i,
                runtimeSiteKitId = i == 0 ? "fixture-town-kit" : "", runtimeSiteBindingVersion = i == 0 ? "fixture-site-bind-v1" : "",
                kind = i == 0 ? "trade town" : "village", approxPopulation = 35 + i * 20 });
        }
        for (int i = 0; i < 2; i++)
            plan.encampments.Add(new GeneratedEncampmentRecord { encampmentId = "fixture_hostile_" + i,
                regionId = "fixture_region_" + i, displayName = "Fixture Hostile " + i,
                layoutIntent = i == 0 ? "checkpoint_layout" : "cave_layout", kind = i == 1 ? "cave" : "bandit checkpoint", threatTier = i + 2 });
        plan.pointsOfInterest.Add(new GeneratedPointOfInterestRecord { poiId = "fixture_poi_0", regionId = "fixture_region_2",
            displayName = "Fixture Shrine", visualStyleKey = "shrine_visual_v1", kind = "lakeside shrine", gameplayHook = "exploration" });
        YQGeneratedWorldSpatialPlanner.EnsureSpatialPlan(plan);
        return plan;
    }

    private static GeneratedSpatialWorldPlanV2Record Compile(string seed, bool coroutine)
    {
        var plan = Fixture(seed);
        GeneratedSpatialWorldPlanV2Record result = null;
        string failure = null;
        if (coroutine) {
            var routine = YQSpatialBlueprintCompilerV2.CompileRoutine(plan, (record, reason) => { result = record; failure = reason; });
            int advances = 0;
            while (routine.MoveNext()) Require(++advances < 10000, "Unbounded compile routine.");
        }
        else Require(YQSpatialBlueprintCompilerV2.TryCompile(plan, out result, out failure), "Compiler rejected " + seed + ": " + failure);
        Require(result != null && result.acceptanceState == GeneratedSpatialPlanAcceptanceState.Accepted,
            "No accepted compiled record for " + seed + ": " + failure);
        Require(result.validationVersion == "spatial_world_plan_v2_gate_3", "New compilation did not select the physical gate.");
        Require(result.contentHash == result.validatedContentHash && result.contentHash == YQSpatialBlueprintHasherV2.ComputeContentHash(result),
            "Accepted compiler checksum does not match validated data.");
        return result;
    }

    public static int Main(string[] args)
    {
        int archiveArgument = Array.IndexOf(args, "--archive-root");
        archiveRoot = archiveArgument >= 0 ? Path.GetFullPath(args[archiveArgument + 1]) : Path.GetFullPath(Path.Combine(args[0], ".."));
        int replayArgument = Array.IndexOf(args, "--query-orders-from");
        if (replayArgument >= 0)
        {
            // note: Replay the exact saved generated inputs after a fixture-placement repair; expensive world generation need not be repeated to test query orders and retained gates.
            string[] inputs = Directory.GetFiles(args[replayArgument + 1], "generated-*.json");
            Array.Sort(inputs, StringComparer.Ordinal);
            Check("QUERY-REPLAY-TWENTY-INPUTS", () => Require(inputs.Length == 20, "Expected the complete twenty-seed generated input set."));
            foreach (string input in inputs)
                Check("QUERY-ORDER-REPLAY:" + Path.GetFileNameWithoutExtension(input), () => {
                    var record = JsonSerializer.Deserialize<GeneratedSpatialWorldPlanV2Record>(File.ReadAllText(input), new JsonSerializerOptions { IncludeFields = true });
                    Require(record.contentHash == YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(record), "Saved generated input checksum changed.");
                    Require(YQSpatialBlueprintValidatorV2.Validate(record).Accepted, "Saved generated input no longer validates.");
                    ChunkQueryOrderChecks(record, Path.GetFileNameWithoutExtension(input).Substring("generated-".Length));
                    foreach (string version in new[] { "spatial_world_plan_v2_gate_1", "spatial_world_plan_v2_gate_2" }) {
                        record.validationVersion = version;
                        Require(YQSpatialBlueprintValidatorV2.Validate(record).Accepted, "Retained validation gate rejected the replay geometry.");
                        Require(record.contentHash == YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(record), "Retained gate changed canonical geometry hash.");
                    }
                });
            File.WriteAllText(Path.Combine(args[0], "query-order-replay-receipt.json"), JsonSerializer.Serialize(new {
                utc = DateTime.UtcNow.ToString("O"), passed = failures == 0, failures, checks = Checks,
                sourceQueryOrderCoverage = QueryOrderCoverage, inputs,
                evidence = "DETACHED_SOURCE_QUERY_ORDER_REPLAY_OF_SAME_GENERATED_ARTIFACTS",
                limits = "Source blueprint preparation/sampling and retained gates only. No new generation, native TerrainData, actual streamed chunk construction/reload, collider, final grading or travel proof."
            }, new JsonSerializerOptions { WriteIndented = true }));
            return failures == 0 ? 0 : 1;
        }
        bool baseline = args.Length > 1 && args[1] == "--baseline";
        if (baseline)
        {
            Check("BASELINE-14CM-UPHILL-TOLERANCE", () => {
                var points = Network(.02f)["river"].controlPoints;
                var method = typeof(YQSpatialBlueprintValidatorV2).GetMethod("FlowsDownhill", Flags);
                Require((bool)method.Invoke(null, new object[] { points }), "Expected the historical gate to admit the two-centimetre rise.");
            });
            Check("BASELINE-70CM-RECEIVER-TOLERANCE", () => Require(Validate(Network(0f, .02f), true).Accepted,
                "Expected the historical gate to admit the two-centimetre join mismatch."));
            Check("BASELINE-LAKE-CYCLE-UNEXAMINED", () => {
                var graph = Network(); graph["lake"].sinkHydrologyId = "river";
                Require(Validate(graph, true).Accepted, "Expected the historical channel walk to stop at the lake.");
            });
        }
        else
        {
            Check("DRAINAGE-LEVEL-REACH", () => Require(Validate(Network(), false).Accepted, "A flat reach and connected level sink must be valid."));
            Check("DRAINAGE-RISE-UNDER-1CM", () => Require(Validate(Network(.009f), false).Accepted, "Sub-centimetre numeric rise rejected."));
            Check("DRAINAGE-RISE-OVER-1CM", () => Require(Has(Validate(Network(.011f), false), "rise_over_1cm"), "Excess downstream rise admitted."));
            Check("DRAINAGE-JOIN-UNDER-1CM", () => Require(Validate(Network(0f, .009f), false).Accepted, "Sub-centimetre receiver mismatch rejected."));
            Check("DRAINAGE-JOIN-OVER-1CM", () => Require(Has(Validate(Network(0f, .011f), false), "receiver_level_over_1cm"), "Excess receiver mismatch admitted."));
            Check("DRAINAGE-AREA-FIXED-LEVEL", () => {
                var graph = Network(); graph["lake"].controlPoints[0].normalizedElevation = 56f / 140f;
                Require(Validate(graph, false).Accepted, "Area-water level incorrectly inferred from a shaping point.");
            });
            Check("DRAINAGE-LAKE-OUTLET-CYCLE", () => {
                var graph = Network(); graph["lake"].sinkHydrologyId = "river";
                Require(Has(Validate(graph, false), "drainage_cycle"), "Cycle through a lake's outlet admitted.");
            });
            Check("DRAINAGE-CASE-CYCLE", () => {
                var graph = Network(); graph["river"].sinkHydrologyId = "RIVER";
                Require(Has(Validate(graph, false), "drainage_cycle"), "Case-insensitive identity cycle admitted.");
            });
            Check("DRAINAGE-UNDECLARED-TERMINAL", () => {
                var graph = Network(); graph["river"].sinkHydrologyId = "";
                Require(Has(Validate(graph, false), "no_receiving_body"), "A terminal channel was admitted as a sink.");
            });
            Check("DRAINAGE-MISSING-LAKE-OUTLET", () => {
                var graph = Network(); graph["lake"].sinkHydrologyId = "absent";
                Require(Has(Validate(graph, false), "missing_receiver"), "A declared missing basin outlet was ignored.");
            });
            Check("DRAINAGE-MALFORMED-POINT-REJECTS", () => {
                foreach (bool legacy in new[] { false, true }) {
                    var graph = Network(); graph["river"].controlPoints[1] = null;
                    Require(!Validate(graph, legacy).Accepted, "Null geometry admitted.");
                    graph = Network(); graph["lake"].controlPoints[0].x = float.NaN;
                    Require(!Validate(graph, legacy).Accepted, "Nonfinite receiver admitted.");
                }
            });
            Check("DRAINAGE-MULTISEGMENT-RECEIVER", () => {
                var graph = Network(); var receiver = graph["river"];
                var tributary = new YQHydrologyFeatureV2 { hydrologyId = "tributary", kind = YQHydrologyKindV2.River,
                    nominalWidth = 6f, nominalDepth = 2f, sinkHydrologyId = "river" };
                tributary.controlPoints.Add(Point(-100f, -50f, 60f));
                tributary.controlPoints.Add(Point(-5f, -50f, 42f));
                graph.Add("tributary", tributary);
                Require(Validate(graph, false).Accepted, "Exact interpolated downstream join rejected.");
                tributary.controlPoints[1].normalizedElevation += .02f / 140f;
                Require(Has(Validate(graph, false), "receiver_level_over_1cm"), "Interior segment join discrepancy missed.");
            });
            Check("DRAINAGE-QUERY-ORDER-AND-READONLY", () => {
                var graph = Network(.02f, .03f); graph["lake"].sinkHydrologyId = "river";
                string before = JsonSerializer.Serialize(graph, new JsonSerializerOptions { IncludeFields = true });
                var reference = Validate(graph, false).Errors; reference.Sort(StringComparer.Ordinal);
                var reversed = new Dictionary<string, YQHydrologyFeatureV2>(StringComparer.OrdinalIgnoreCase) {
                    ["river"] = graph["river"], ["lake"] = graph["lake"] };
                var alternate = Validate(reversed, false).Errors; alternate.Sort(StringComparer.Ordinal);
                Require(string.Join("|", reference) == string.Join("|", alternate), "Insertion order changed rejection facts.");
                Require(before == JsonSerializer.Serialize(graph, new JsonSerializerOptions { IncludeFields = true }), "Validation mutated accepted geometry.");
            });
            Check("DRAINAGE-VERSIONED-PRODUCTION-ROUTING", () => {
                // note: Full blueprint validation, rather than the private helper alone, must enforce the new physical gate.
                var graph = Network(.02f, .02f);
                var blueprint = new YQSpatialBlueprintV2 { worldSize = 1024f, hydrology = new List<YQHydrologyFeatureV2>(graph.Values) };
                foreach (string version in new[] { "spatial_world_plan_v2_gate_1", "spatial_world_plan_v2_gate_2", "spatial_world_plan_v2_gate_3" }) {
                    var record = new GeneratedSpatialWorldPlanV2Record { blueprint = blueprint,
                        generationVersion = GeneratedSpatialWorldPlanV2Record.CompatibleAcceptedGenerationVersion, validationVersion = version };
                    var result = YQSpatialBlueprintValidatorV2.Validate(record);
                    bool strict = version.EndsWith("_3", StringComparison.Ordinal);
                    Require(Has(result, "rise_over_1cm") == strict && Has(result, "receiver_level_over_1cm") == strict,
                        "Wrong validation contract selected for " + version);
                }
                Require(GeneratedSpatialWorldPlanV2Record.CompatibleAcceptedGenerationVersion == "spatial_world_plan_v2_topology_10", "Prior accepted generation is no longer retained.");
                graph = Network(); graph["lake"].controlPoints[0].normalizedElevation = 56f / 140f;
                var levelRecord = new GeneratedSpatialWorldPlanV2Record {
                    blueprint = new YQSpatialBlueprintV2 { worldSize = 1024f, hydrology = new List<YQHydrologyFeatureV2>(graph.Values) },
                    generationVersion = GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion,
                    validationVersion = GeneratedSpatialWorldPlanV2Record.SupportedValidationVersion };
                Require(!Has(YQSpatialBlueprintValidatorV2.Validate(levelRecord), "disconnected_receiver"),
                    "Production receiving-water stage still uses the lake shaping point as its water plane.");
            });
            var generatorSeeds = new List<string> { "semantic-v2-318", "33b9e87d", "watershed-alpha", "watershed-beta" };
            // note: Iteration covers the original four seeds plus both waterfront regressions; the default remains the full twenty-seed fixture suite.
            if (Array.IndexOf(args, "--quick") >= 0) generatorSeeds.AddRange(new[] { "basin-seed-06", "basin-seed-08" });
            else for (int index = 0; index < 16; index++) generatorSeeds.Add("basin-seed-" + index.ToString("D2"));
            foreach (string seed in generatorSeeds) {
                Check("DRAINAGE-GENERATOR-REGRESSION:" + seed, () => {
                    var serial = Compile(seed, false);
                    var routine = Compile(seed, true);
                    var detached = Task.Run(() => Compile(seed, false)).GetAwaiter().GetResult();
                    Require(serial.contentHash == routine.contentHash && serial.contentHash == detached.contentHash,
                        "Serial, yielding and detached compiler hashes differ.");
                    string before = JsonSerializer.Serialize(serial, new JsonSerializerOptions { IncludeFields = true });
                    var repeated = YQSpatialBlueprintValidatorV2.Validate(serial);
                    Require(repeated.Accepted, "Compiled physical network rejected on repeat validation.");
                    Require(before == JsonSerializer.Serialize(serial, new JsonSerializerOptions { IncludeFields = true }),
                        "Repeat validation changed canonical compiled records.");
                    foreach (var area in serial.blueprint.hydrology)
                    {
                        if (area.kind != YQHydrologyKindV2.Lake && area.kind != YQHydrologyKindV2.Wetland) continue;
                        Require(area.hasConnectedDomain && YQPreparedConnectedBasinV2.TryPrepare(area.connectedDomain, area.waterLevelNormalized, out _, out _),
                            "Generator did not produce a contained connected basin.");
                        Require(area.connectedDomain.minimumX / area.connectedDomain.spacing == Math.Round(area.connectedDomain.minimumX / area.connectedDomain.spacing) &&
                            area.connectedDomain.minimumZ / area.connectedDomain.spacing == Math.Round(area.connectedDomain.minimumZ / area.connectedDomain.spacing),
                            "New geological bed is not aligned to its production heightfield grid.");
                    }
                    File.WriteAllText(Path.Combine(args[0], "generated-" + seed + ".json"), before);
                    ChunkQueryOrderChecks(serial, seed);
                    foreach (string legacyVersion in new[] { "spatial_world_plan_v2_gate_1", "spatial_world_plan_v2_gate_2" }) {
                        serial.validationVersion = legacyVersion;
                        Require(YQSpatialBlueprintValidatorV2.Validate(serial).Accepted, "Prior physical contract no longer accepts valid geometry.");
                        Require(serial.contentHash == YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(serial), "Existing canonical geometry checksum changed.");
                    }
                });
            }
            BasinChecks(args[0]);
            ChannelWetFootprintChecks();
            ConnectedDomainChecks(args[0]);
            if (args.Length > 2 && args[1] == "--unity-artifact")
                Check("BASIN-UNITY-CORECLR-CANONICAL-HASH", () => {
                    // note: Rehash exactly the Unity-accepted archived draft, not a separately regenerated plan; the runner binds both receipts to identical source hashes.
                    var plan = Newtonsoft.Json.JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(args[2]));
                    Require(plan.spatialPlanV2.generationVersion == GeneratedSpatialWorldPlanV2Record.SupportedGenerationVersion, "Wrong Unity artifact version.");
                    Require(plan.spatialPlanV2.contentHash == YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2),
                        "Unity/CoreCLR disagree on the same canonical float values.");
                    Require(YQSpatialBlueprintValidatorV2.Validate(plan.spatialPlanV2).Accepted, "Unity draft fails compiled basin validation.");
                    File.WriteAllText(Path.Combine(args[0], "unity-artifact-hash.json"), JsonSerializer.Serialize(new {
                        storedHash = plan.spatialPlanV2.contentHash, computedHash = YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly(plan.spatialPlanV2),
                        passed = true, evidence = "SAME_SERIALIZED_ARTIFACT_UNITY_MONO_AND_CORECLR_NOT_PLAYER_BUILD" }));
                });
        }
        File.WriteAllText(Path.Combine(args[0], "drainage-receipt.json"), JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow.ToString("O"), passed = failures == 0, failures, checks = Checks,
            evidence = "DETACHED_COMPILED_PRODUCTION_VALIDATION_SYNTHETIC_RECORDS",
            runtimeVerified = false, baseline, terrainHeightMetres = 140f,
            sourceQueryOrderCoverage = QueryOrderCoverage,
            sourceQueryBoundary = "TryPrepareBlueprint and Sample on reconstructed accepted blueprint records; four chunk-shaped query orders and three repeated passes, not actual chunk construction/publication or persisted reload.",
            limits = "No Unity native terrain, final mesh, accepted profile, save reload, full canonical generator coverage, actual chunk-order acceptance, visual or travel proof."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
