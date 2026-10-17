using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using UnityEngine;

// note: Exercise the compiled native projection with fixed analytical fields, independently of Unity terrain, live profiles or copied polygon algorithms.
public static class NativeReachChecks
{
    private static readonly Type Node = typeof(YQNativeWaterJoinPatchV2).Assembly.GetType("YQNativeChannelNodeV2", true);
    private static readonly List<object> Checks = new List<object>();
    private static int failures;
    private static readonly List<object> OrderCoverage = new List<object>();

    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    private static Delegate NodeSampler(float centreX = 0f, float centreZ = -124f)
    {
        // note: Reflection remains inside this detached verifier; production gains no public testing API or alternative water owner.
        var channel = Expression.Parameter(typeof(int)); var x = Expression.Parameter(typeof(float)); var z = Expression.Parameter(typeof(float));
        var abs = typeof(Math).GetMethod("Abs", new[] { typeof(float) });
        var localX = Expression.Subtract(x, Expression.Constant(centreX));
        var across = Expression.Subtract(z, Expression.Add(Expression.Constant(centreZ), Expression.Multiply(localX, Expression.Constant(.4f))));
        var core = Expression.AndAlso(Expression.LessThanOrEqual(Expression.Call(abs, localX), Expression.Constant(220f)),
            Expression.LessThanOrEqual(Expression.Call(abs, across), Expression.Constant(3f)));
        var value = Expression.MemberInit(Expression.New(Node), Expression.Bind(Node.GetField("surface"), Expression.Constant(.5f)),
            Expression.Bind(Node.GetField("core"), core));
        return Expression.Lambda(typeof(Func<,,,>).MakeGenericType(typeof(int), typeof(float), typeof(float), Node), value, channel, x, z).Compile();
    }

    private static YQNativeWaterJoinPatchV2 Tile(float minX, Func<float, float, float> bed,
        float minZ = -256f, float centreX = 0f, float centreZ = -124f)
    {
        var constructor = typeof(YQNativeWaterJoinPatchV2).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
            new[] { typeof(int), typeof(float), typeof(float), typeof(float), typeof(Func<float, float, float>),
                typeof(YQPreparedConnectedBasinV2[]), typeof(int[]), NodeSampler().GetType() }, null);
        Require(constructor != null, "Production reach constructor is unavailable.");
        return (YQNativeWaterJoinPatchV2)constructor.Invoke(new object[] { 129, minX, minZ, 2f, bed,
            Array.Empty<YQPreparedConnectedBasinV2>(), new[] { 3 }, NodeSampler(centreX, centreZ) });
    }

    private static bool Freeze(IReadOnlyList<YQNativeWaterJoinPatchV2> tiles, out string reason)
    {
        var method = typeof(YQNativeWaterJoinPatchV2).GetMethod("TryFreezeConnectedReaches", BindingFlags.NonPublic | BindingFlags.Static);
        Require(method != null, "Production connected selection is unavailable.");
        var values = new object[] { tiles, null }; bool passed = (bool)method.Invoke(null, values); reason = (string)values[1]; return passed;
    }

    private static bool InsideFrozenPolygon(YQNativeWaterJoinPatchV2 tile, float x, float z)
    {
        // note: Independently classify the production polygon vertices using convex edge signs, rather than its bed equation or query implementation.
        var polygons = (Vector3[][])typeof(YQNativeWaterJoinPatchV2).GetField("polygons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(tile);
        foreach (var polygon in polygons)
        {
            bool positive = false, negative = false;
            for (int edge = 0; edge < polygon.Length; edge++)
            {
                var a = polygon[edge]; var b = polygon[(edge + 1) % polygon.Length];
                double cross = ((double)b.x - a.x) * ((double)z - a.z) - ((double)b.z - a.z) * ((double)x - a.x);
                positive |= cross > 1e-9; negative |= cross < -1e-9;
            }
            if (!(positive && negative) && (positive || negative)) return true;
        }
        return false;
    }

    private static void Check(string id, Action test)
    {
        try { test(); Checks.Add(new { id, passed = true }); Console.WriteLine("PASS " + id); }
        catch (Exception error) { failures++; Checks.Add(new { id, passed = false, error = error.ToString() }); Console.WriteLine("FAIL " + id + ": " + error.Message); }
    }

    private static string FrozenGeometryHash(YQNativeWaterJoinPatchV2 tile)
    {
        // note: Pin actual published polygon vertices/owners bit for bit; query parity alone could miss unsampled geometry changes.
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        var polygons = (Vector3[][])typeof(YQNativeWaterJoinPatchV2).GetField("polygons", flags).GetValue(tile);
        var owners = (int[])typeof(YQNativeWaterJoinPatchV2).GetField("polygonOwners", flags).GetValue(tile);
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(polygons.Length);
            for (int index = 0; index < polygons.Length; index++)
            {
                writer.Write(owners[index]); writer.Write(polygons[index].Length);
                foreach (var vertex in polygons[index]) { writer.Write(vertex.x); writer.Write(vertex.y); writer.Write(vertex.z); }
            }
        }
        return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }

    private static void FourTileOrderChecks()
    {
        // note: Cross both axes at a four-tile junction, including wholly negative world coordinates. Construction/freezing uses real production code with independently specified analytical inputs.
        int[][] orders = { new[] { 0, 1, 2, 3 }, new[] { 3, 2, 1, 0 }, new[] { 0, 1, 3, 2 }, new[] { 1, 2, 0, 3 } };
        foreach (float centre in new[] { -256f, 0f, 256f })
        {
            var reference = new Dictionary<(float, float), (bool, int, float)>();
            string[] referenceGeometry = null;
            for (int order = 0; order < orders.Length; order++)
            {
                int evaluations = 0;
                float Bed(float x, float z)
                {
                    evaluations++;
                    float localX = x - centre, localZ = z - centre;
                    return .5f + (Math.Max(Math.Abs(localZ - .4f * localX) / 8f, Math.Abs(localX) / 225f) - 1f) * .01f;
                }
                var canonical = new YQNativeWaterJoinPatchV2[4];
                var arrival = new List<YQNativeWaterJoinPatchV2>();
                foreach (int tile in orders[order])
                {
                    canonical[tile] = Tile(centre + (tile % 2 - 1) * 256f, Bed,
                        centre + (tile / 2 - 1) * 256f, centre, centre);
                    arrival.Add(canonical[tile]);
                }
                Require(Freeze(arrival, out string reason), reason);
                var geometry = Array.ConvertAll(canonical, FrozenGeometryHash);
                if (order == 0) referenceGeometry = geometry;
                else for (int tile = 0; tile < 4; tile++) Require(geometry[tile] == referenceGeometry[tile],
                    "Arrival order changed frozen polygon vertices/owners at junction " + centre + ", tile=" + tile);
                int preparedEvaluations = evaluations, queries = 0, wet = 0, dry = 0;
                for (int revisit = 0; revisit < 3; revisit++)
                for (float dx = -16f; dx <= 16f; dx += .5f)
                for (float dz = -16f; dz <= 16f; dz += .25f)
                {
                    float x = centre + dx, z = centre + dz;
                    int tile = (dx < 0f ? 0 : 1) + (dz < 0f ? 0 : 2);
                    bool covered = canonical[tile].TrySample(x, z, out int owner, out float level);
                    var sample = (covered, covered ? owner : -1, covered ? level : 0f);
                    if (reference.TryGetValue((x, z), out var expected)) Require(sample == expected, "Order/revisit changed junction coverage.");
                    else { Require(order == 0 && revisit == 0, "Unexpected coverage change."); reference.Add((x, z), sample); }
                    if (covered) { Require(owner == 3 && level == .5f, "Fixture water owner/datum changed."); wet++; } else dry++;
                    queries++;
                    if (dx == 0f || dz == 0f)
                    {
                        // note: Every incident tile must agree at the same world-space boundary point; this catches wrong negative-coordinate lookup and corner handoffs.
                        for (int neighbour = 0; neighbour < 4; neighbour++)
                        {
                            if (dx != 0f && neighbour % 2 != tile % 2 || dz != 0f && neighbour / 2 != tile / 2) continue;
                            bool adjacent = canonical[neighbour].TrySample(x, z, out int adjacentOwner, out float adjacentLevel);
                            Require(adjacent == covered && (!covered || adjacentOwner == owner && adjacentLevel == level), "Incident tiles disagree at shared edge/corner.");
                        }
                    }
                }
                Require(wet > 0 && dry > 0 && evaluations == preparedEvaluations, "Queries lost coverage or resampled source terrain.");
                for (int tile = 0; tile < 4; tile++) Require(FrozenGeometryHash(canonical[tile]) == geometry[tile], "Queries mutated frozen water polygons.");
                OrderCoverage.Add(new { centre, order, tiles = 4, queries, revisits = 3, wet, dry,
                    sourceEvaluations = evaluations, frozenGeometrySha256 = geometry, passed = true });
            }
        }
    }

    public static int Main(string[] args)
    {
        Check("CONNECTED-NATIVE-FOUR-TILE-FOUR-ORDER-JUNCTIONS-AND-REVISITS", FourTileOrderChecks);
        int evaluations = 0;
        float Bed(float x, float z)
        {
            evaluations++;
            float edge = Math.Max(Math.Abs(z + 124f - .4f * x) / 8f, Math.Abs(x) / 225f);
            return .5f + (edge - 1f) * .01f;
        }
        var left = Tile(-256f, Bed); var right = Tile(0f, Bed);
        Check("CONNECTED-NATIVE-FRINGE-CROSSES-NEGATIVE-COORDINATE-TILE-EDGE", () =>
        {
            Require(Freeze(new[] { left, right }, out string reason), reason);
            // note: These submerged fringe points are outside the three-metre source core on both sides; the previous core-node rule stranded this connected water.
            Require(left.TrySample(-.002f, -118f, out int first, out float a) && first == 3 && a == .5f,
                "Left fringe lost connected water.");
            Require(right.TrySample(.002f, -118f, out int second, out float b) && second == 3 && b == .5f,
                "Right fringe terminates over a submerged bed.");
        });
        Check("CONNECTED-NATIVE-PREPARATION-ORDER-INDEPENDENT", () =>
        {
            var reversedRight = Tile(0f, Bed); var reversedLeft = Tile(-256f, Bed);
            Require(Freeze(new[] { reversedRight, reversedLeft }, out string reason), reason);
            for (float z = -133f; z <= -115f; z += .25f)
            {
                bool first = left.TrySample(0f, z, out int a, out float av);
                bool second = reversedLeft.TrySample(0f, z, out int b, out float bv);
                Require(first == second && (!first || a == b && av == bv), "Preparation order changed a shared edge.");
            }
        });
        Check("CONNECTED-NATIVE-QUERIES-DO-NOT-RESAMPLE-OR-MUTATE", () =>
        {
            int before = evaluations;
            for (float z = -133f; z <= -115f; z += .25f)
            { left.TrySample(-.002f, z, out _, out _); right.TrySample(.002f, z, out _, out _); }
            Require(evaluations == before, "A published query evaluated or changed its terrain input.");
        });
        Check("CONNECTED-NATIVE-SHALLOW-DRY-FRINGE-IS-NOT-WATER", () =>
        {
            // note: The former two-micro-unit bed tolerance admitted this dry point despite there being no clipped water polygon beneath it.
            const float x = .002f, z = -115.9985f;
            Require(!InsideFrozenPolygon(right, x, z), "Dry-fringe regression point unexpectedly lies on water.");
            Require(!right.TrySample(x, z, out _, out _), "Dry bank outside every published polygon was classified wet.");
        });
        Check("CONNECTED-NATIVE-PUBLISHED-FOOTPRINT-MATCHES-EDGE-QUERIES", () =>
        {
            // note: Sweep both banks close to the shared tile boundary, including submillimetre offsets that expose rounding/tolerance mismatches.
            foreach (float x in new[] { -.002f, -.25f, .002f, .25f })
            foreach (float offset in new[] { -.004f, -.001f, .001f, .004f })
            foreach (float bank in new[] { -132f, -116f })
            {
                float z = bank + .4f * x + offset; var tile = x < 0f ? left : right;
                bool covered = InsideFrozenPolygon(tile, x, z), wet = tile.TrySample(x, z, out _, out _);
                Require(covered == wet, "Water query differs from frozen polygon at " + x + "," + z + ": polygon=" + covered + " query=" + wet);
            }
        });
        Check("CONNECTED-NATIVE-WORKER-MATCHES-SERIAL-GEOMETRY", () =>
        {
            // note: Use the production constructor/freezer on a worker and compare dense shared-edge samples with its serial result; this checks scheduling equivalence without copying its polygon algorithm.
            var worker = Task.Run(() =>
            {
                float DetachedBed(float x, float z) => .5f + (Math.Max(Math.Abs(z + 124f - .4f * x) / 8f, Math.Abs(x) / 225f) - 1f) * .01f;
                var a = Tile(-256f, DetachedBed); var b = Tile(0f, DetachedBed);
                Require(Freeze(new[] { a, b }, out string reason), reason);
                return new[] { a, b };
            }).GetAwaiter().GetResult();
            for (float x = -8f; x <= 8f; x += .5f)
            for (float z = -140f; z <= -108f; z += .25f)
            {
                int tile = x < 0f ? 0 : 1;
                bool serialWet = (tile == 0 ? left : right).TrySample(x, z, out int serialOwner, out float serialSurface);
                bool workerWet = worker[tile].TrySample(x, z, out int workerOwner, out float workerSurface);
                Require(serialWet == workerWet && (!serialWet || serialOwner == workerOwner && serialSurface == workerSurface),
                    "Worker changed accepted wet coverage, ownership or surface at " + x + "," + z);
            }
        });
        Check("CONNECTED-NATIVE-OPEN-BOUNDARY-REJECTS-WITH-REASON", () =>
        {
            var open = Tile(0f, (x, z) => .49f);
            Require(!Freeze(new[] { open }, out string reason) && reason.StartsWith("native_channel_projection_open_boundary:channel=3;", StringComparison.Ordinal),
                "An unsupported open wet boundary was silently truncated.");
        });
        Check("STARTUP-SNAPSHOT-PRESERVES-NULL-LOCATION-VARIANTS", () =>
        {
            // note: Exercise the exact production copy method; a POI must not gain an empty settlement/encampment branch that masks its saved provider.
            var location = new GeneratedSpatialContinuationLocationV2Record
            { settlement = null, encampment = null, poiRuntimeSiteKitId = "saved-poi-kit", compositionSeed = "saved-seed" };
            var plan = new GeneratedWorldPlanRecord { spatialPlanV2 = new GeneratedSpatialWorldPlanV2Record
            { acceptedContinuation = new GeneratedSpatialContinuationV2Record
            { locations = new List<GeneratedSpatialContinuationLocationV2Record> { location } } } };
            var owner = typeof(YQSpatialMaterializationCompilerV2).GetNestedType("StartupPreparation", BindingFlags.NonPublic);
            var copy = owner.GetMethod("CopySavedPlan", BindingFlags.NonPublic | BindingFlags.Static);
            var detached = (GeneratedWorldPlanRecord)copy.Invoke(null, new object[] { plan });
            var saved = detached.spatialPlanV2.acceptedContinuation.locations[0];
            Require(!ReferenceEquals(location, saved) && saved.settlement == null && saved.encampment == null &&
                saved.poiRuntimeSiteKitId == "saved-poi-kit" && saved.compositionSeed == "saved-seed",
                "Detached startup copy changed location branch/provider identity.");
        });
        File.WriteAllText(Path.Combine(args[0], "native-reach-receipt.json"), JsonSerializer.Serialize(new {
            utc = DateTime.UtcNow.ToString("O"), passed = failures == 0, failures, checks = Checks, evaluations,
            fourTileOrderCoverage = OrderCoverage,
            evidence = "DETACHED_COMPILED_PRODUCTION_NATIVE_WET_SELECTION_WITH_ANALYTICAL_FIELDS",
            limits = "Analytical fixed fields, four-tile junctions at negative/zero/positive coordinates, four preparation orders, three query revisits, worker/serial geometry equivalence, immutable query and explicit rejection only. No native TerrainData, collider, final grading, actual streamed chunk lifecycle or live travel proof."
        }, new JsonSerializerOptions { WriteIndented = true }));
        return failures == 0 ? 0 : 1;
    }
}
