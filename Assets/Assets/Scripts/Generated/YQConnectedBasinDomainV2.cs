using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class YQConnectedBasinDomainV2
{
    // note: Persist a bounded terrain patch and one flooded component, including its dry islands; no render-cell arrival order participates in this geometry.
    public int version = 1;
    public int resolution;
    public float spacing;
    public float minimumX;
    public float minimumZ;
    public float seedX;
    public float seedZ;
    public List<float> bed = new List<float>();
    public List<int> wetTriangles = new List<int>();
}

internal struct YQNativeChannelNodeV2
{
    public float surface;
    public bool core;
}

// note: This immutable derived index measures the finite rendered channel union; it is not a second planner or persisted water authority.
internal sealed class YQChannelFootprintIndexV2
{
    internal struct Triangle
    {
        public Vector2 a, b, c;
        public int owner;
    }
    private struct Node
    {
        public Vector2 minimum, maximum;
        public int start, count, left, right;
    }
    private readonly Triangle[] triangles;
    private readonly int[] order;
    private readonly Node[] nodes;

    internal YQChannelFootprintIndexV2(List<Triangle> source)
    {
        triangles = source.ToArray(); order = new int[triangles.Length];
        for (int i = 0; i < order.Length; i++) order[i] = i;
        var prepared = new List<Node>();
        if (order.Length > 0) Build(prepared, 0, order.Length);
        nodes = prepared.ToArray();
    }

    internal static void AddPolygon(List<Triangle> output, List<Vector2> polygon, int owner)
    {
        for (int i = 1; i + 1 < polygon.Count; i++)
        {
            Vector2 a = polygon[0], b = polygon[i], c = polygon[i + 1];
            double area = ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);
            if (Math.Abs(area) > 1e-10) output.Add(new Triangle { a = a, b = b, c = c, owner = owner });
        }
    }

    internal static void AddSourceTriangle(List<Triangle> output, Vector2 a, Vector2 b, Vector2 c,
        int owner, IReadOnlyList<YQNativeWaterJoinPatchV2> joins, int firstJoin = 0)
    {
        // note: Remove the same bounded join rectangles that replace source ribbons during rendering; rounded caps and discarded old ribbons are not placement water.
        var fragments = new List<List<Vector2>> { new List<Vector2> { a, b, c } };
        for (int joinIndex = firstJoin; joinIndex < joins.Count; joinIndex++)
        {
            var join = joins[joinIndex];
            var remaining = new List<List<Vector2>>();
            float[] bounds = { join.MinimumX, join.MaximumX, join.MinimumZ, join.MaximumZ };
            foreach (var fragment in fragments)
            {
                var inside = fragment;
                for (int plane = 0; plane < 4 && inside.Count > 0; plane++)
                {
                    var outside = Clip(inside, plane, bounds[plane], false);
                    if (outside.Count >= 3) remaining.Add(outside);
                    inside = Clip(inside, plane, bounds[plane], true);
                }
            }
            fragments = remaining;
        }
        foreach (var fragment in fragments) AddPolygon(output, fragment, owner);
    }

    private static List<Vector2> Clip(List<Vector2> input, int plane, float bound, bool inside)
    {
        var output = new List<Vector2>(8);
        if (input.Count == 0) return output;
        float Coordinate(Vector2 p) => plane < 2 ? p.x : p.y;
        bool Keep(Vector2 p) => (((plane & 1) == 0 ? Coordinate(p) >= bound : Coordinate(p) <= bound) == inside);
        Vector2 previous = input[input.Count - 1]; bool previousInside = Keep(previous);
        foreach (var current in input)
        {
            bool currentInside = Keep(current);
            if (previousInside != currentInside)
            {
                Vector2 low = previous, high = current;
                if (Coordinate(low) > Coordinate(high)) { low = current; high = previous; }
                Vector2 hit = Vector2.LerpUnclamped(low, high, (bound - Coordinate(low)) / (Coordinate(high) - Coordinate(low)));
                if (plane < 2) hit.x = bound; else hit.y = bound;
                output.Add(hit);
            }
            if (currentInside) output.Add(current);
            previous = current; previousInside = currentInside;
        }
        return output;
    }

    private int Build(List<Node> prepared, int start, int count)
    {
        // note: Preparation owns sorting and allocation; background placement queries only read bounded nodes and original triangles.
        var node = new Node { minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity),
            maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity), start = start, count = count, left = -1, right = -1 };
        for (int i = start; i < start + count; i++)
        {
            var triangle = triangles[order[i]];
            node.minimum = Vector2.Min(node.minimum, Vector2.Min(triangle.a, Vector2.Min(triangle.b, triangle.c)));
            node.maximum = Vector2.Max(node.maximum, Vector2.Max(triangle.a, Vector2.Max(triangle.b, triangle.c)));
        }
        int index = prepared.Count; prepared.Add(node);
        if (count > 8)
        {
            bool xAxis = node.maximum.x - node.minimum.x >= node.maximum.y - node.minimum.y;
            Array.Sort(order, start, count, Comparer<int>.Create((first, second) =>
            {
                var a = triangles[first]; var b = triangles[second];
                double ac = xAxis ? (double)a.a.x + a.b.x + a.c.x : (double)a.a.y + a.b.y + a.c.y;
                double bc = xAxis ? (double)b.a.x + b.b.x + b.c.x : (double)b.a.y + b.b.y + b.c.y;
                int result = ac.CompareTo(bc); return result != 0 ? result : first.CompareTo(second);
            }));
            int half = count / 2;
            node.left = Build(prepared, start, half); node.right = Build(prepared, start + half, count - half);
            node.count = 0; prepared[index] = node;
        }
        return index;
    }

    internal bool TryMeasure(float x, float z, out int owner, out float distance)
    {
        owner = -1; distance = float.PositiveInfinity;
        if (nodes.Length == 0 || float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(z) || float.IsInfinity(z)) return false;
        double best = double.PositiveInfinity; Find(0, new Vector2(x, z), ref best, ref owner);
        distance = (float)Math.Sqrt(best); return owner >= 0;
    }

    internal struct CoveragePoint
    {
        internal double x, z;
        internal CoveragePoint(double x, double z) { this.x = x; this.z = z; }
    }

    internal static List<CoveragePoint> ClipCoverage(List<CoveragePoint> input, Func<CoveragePoint, double> distance)
    {
        // note: Double precision preserves small dry remnants while clipping the immutable accepted water union.
        var output = new List<CoveragePoint>(8);
        if (input.Count == 0) return output;
        var previous = input[input.Count - 1]; double before = distance(previous);
        foreach (var current in input)
        {
            double after = distance(current);
            if ((before >= 0d) != (after >= 0d))
            {
                double t = before / (before - after);
                output.Add(new CoveragePoint(previous.x + (current.x - previous.x) * t,
                    previous.z + (current.z - previous.z) * t));
            }
            if (after >= 0d) output.Add(current);
            previous = current; before = after;
        }
        return output;
    }

    internal static bool HasCoverageArea(List<CoveragePoint> polygon)
    {
        if (polygon.Count < 3) return false;
        double area = 0d; var origin = polygon[0];
        for (int i = 1; i + 1 < polygon.Count; i++)
            area += (polygon[i].x - origin.x) * (polygon[i + 1].z - origin.z) -
                (polygon[i].z - origin.z) * (polygon[i + 1].x - origin.x);
        return Math.Abs(area) > 1e-10;
    }

    internal bool CoversPolygon(List<CoveragePoint> polygon)
        => MeasureCoverage(polygon, out _);

    internal bool MeasureCoverage(List<CoveragePoint> polygon, out Vector2 dryWitness)
    {
        // note: Subtract real channel triangles from the dry lake remnant. Corner samples cannot prove coverage of islands or curved shores.
        dryWitness = new Vector2(float.NaN, float.NaN);
        if (!HasCoverageArea(polygon)) return true;
        if (nodes.Length == 0) { dryWitness = CoverageCentroid(polygon); return false; }
        double minX = double.PositiveInfinity, minZ = minX, maxX = double.NegativeInfinity, maxZ = maxX;
        foreach (var point in polygon)
        { minX = Math.Min(minX, point.x); minZ = Math.Min(minZ, point.z); maxX = Math.Max(maxX, point.x); maxZ = Math.Max(maxZ, point.z); }
        var remaining = new List<List<CoveragePoint>> { polygon };
        bool bounded = SubtractCoverage(0, minX, minZ, maxX, maxZ, ref remaining);
        if (bounded && remaining.Count > 0) dryWitness = CoverageCentroid(remaining[0]);
        return bounded && remaining.Count == 0;
    }

    internal static Vector2 CoverageCentroid(List<CoveragePoint> polygon)
    {
        // note: Every clipped remnant is convex, so the mean of its vertices lies inside that specific dry remnant.
        double x = 0d, z = 0d;
        foreach (var point in polygon) { x += point.x; z += point.z; }
        return new Vector2((float)(x / polygon.Count), (float)(z / polygon.Count));
    }

    private bool SubtractCoverage(int index, double minX, double minZ, double maxX, double maxZ,
        ref List<List<CoveragePoint>> remaining)
    {
        var node = nodes[index];
        if (remaining.Count == 0 || node.maximum.x < minX || node.maximum.y < minZ || node.minimum.x > maxX || node.minimum.y > maxZ) return true;
        if (node.count == 0)
            return SubtractCoverage(node.left, minX, minZ, maxX, maxZ, ref remaining) &&
                SubtractCoverage(node.right, minX, minZ, maxX, maxZ, ref remaining);
        for (int i = node.start; i < node.start + node.count && remaining.Count > 0; i++)
        {
            var triangle = triangles[order[i]];
            Vector2[] points = { triangle.a, triangle.b, triangle.c };
            double winding = Math.Sign(((double)triangle.b.x - triangle.a.x) * ((double)triangle.c.y - triangle.a.y) -
                ((double)triangle.b.y - triangle.a.y) * ((double)triangle.c.x - triangle.a.x));
            var next = new List<List<CoveragePoint>>();
            foreach (var fragment in remaining)
            {
                var inside = fragment;
                for (int edge = 0; edge < 3 && HasCoverageArea(inside); edge++)
                {
                    Vector2 a = points[edge], b = points[(edge + 1) % 3];
                    double Distance(CoveragePoint p) => winding * (((double)b.x - a.x) * (p.z - a.y) - ((double)b.y - a.y) * (p.x - a.x));
                    var outside = ClipCoverage(inside, p => -Distance(p));
                    if (HasCoverageArea(outside)) next.Add(outside);
                    inside = ClipCoverage(inside, Distance);
                }
            }
            // note: Pathological fragmentation fails closed instead of turning startup into an unbounded geometry job.
            if (next.Count > 512) return false;
            remaining = next;
        }
        return true;
    }

    private static double BoxDistance(Node node, Vector2 point)
    {
        double x = Math.Max(0d, Math.Max((double)node.minimum.x - point.x, (double)point.x - node.maximum.x));
        double z = Math.Max(0d, Math.Max((double)node.minimum.y - point.y, (double)point.y - node.maximum.y));
        return x * x + z * z;
    }

    private void Find(int index, Vector2 point, ref double best, ref int owner)
    {
        var node = nodes[index];
        if (BoxDistance(node, point) > best + Math.Max(1e-10, best * 1e-12)) return;
        if (node.count > 0)
        {
            for (int i = node.start; i < node.start + node.count; i++)
            {
                var triangle = triangles[order[i]];
                double distance = DistanceSquared(point, triangle);
                if (distance < best || distance == best && (owner < 0 || triangle.owner < owner))
                { best = distance; owner = triangle.owner; }
            }
            return;
        }
        bool leftFirst = BoxDistance(nodes[node.left], point) <= BoxDistance(nodes[node.right], point);
        Find(leftFirst ? node.left : node.right, point, ref best, ref owner);
        Find(leftFirst ? node.right : node.left, point, ref best, ref owner);
    }

    private static double DistanceSquared(Vector2 point, Triangle triangle)
    {
        // note: Test closed interiors in double precision, then measure original edges; no radial widening or independent channel equation participates.
        double bx = (double)triangle.b.x - triangle.a.x, bz = (double)triangle.b.y - triangle.a.y;
        double cx = (double)triangle.c.x - triangle.a.x, cz = (double)triangle.c.y - triangle.a.y;
        double px = (double)point.x - triangle.a.x, pz = (double)point.y - triangle.a.y, determinant = bx * cz - bz * cx;
        if (Math.Abs(determinant) > 1e-10)
        {
            double v = (px * cz - pz * cx) / determinant, w = (bx * pz - bz * px) / determinant;
            if (v >= -1e-9 && w >= -1e-9 && v + w <= 1d + 1e-9) return 0d;
        }
        return Math.Min(EdgeDistance(point, triangle.a, triangle.b),
            Math.Min(EdgeDistance(point, triangle.b, triangle.c), EdgeDistance(point, triangle.c, triangle.a)));
    }

    private static double EdgeDistance(Vector2 point, Vector2 a, Vector2 b)
    {
        double dx = (double)b.x - a.x, dz = (double)b.y - a.y, px = (double)point.x - a.x, pz = (double)point.y - a.y;
        double length = dx * dx + dz * dz, t = length > 0d ? Math.Max(0d, Math.Min(1d, (px * dx + pz * dz) / length)) : 0d;
        px -= t * dx; pz -= t * dz; return px * px + pz * pz;
    }
}

// note: This rebuildable join geometry belongs to the existing prepared terrain sampler; accepted feature IDs and the source height field remain authoritative.
public sealed class YQNativeWaterJoinPatchV2
{
    private readonly YQPreparedConnectedBasinV2 lake;
    private readonly int resolution;
    private readonly float spacing, minimumX, minimumZ;
    private readonly float[] bed;
    private Func<float, float, float> prepareBed;
    private readonly bool[] lakeTriangles;
    private readonly float[] lakeNodeLevels;
    private readonly int[] channelIndices;
    private readonly float[][] levels;
    private readonly int[] owners;
    private Vector3[][] polygons;
    private int[] polygonOwners;
    private int[] nativePolygonByTriangle;
    private readonly bool[][] core;
    public float MinimumX => lake != null ? lake.MinimumX : minimumX;
    public float MinimumZ => lake != null ? lake.MinimumZ : minimumZ;
    public float MaximumX => lake != null ? lake.MaximumX : minimumX + spacing * (resolution - 1);
    public float MaximumZ => lake != null ? lake.MaximumZ : minimumZ + spacing * (resolution - 1);
    private int Resolution => lake != null ? lake.NativeResolution : resolution;
    private float Spacing => lake != null ? lake.NativeSpacing : spacing;
    private float Bed(int vertex)
    {
        if (lake != null) return lake.GetNativeBed(vertex);
        if (float.IsNaN(bed[vertex]))
        {
            // note: Only constructor-selected channel triangles need expensive terrain evaluation. Publication reads fully frozen triangle nodes and cannot lazily change terrain or geometry.
            if (prepareBed == null) throw new InvalidOperationException("native_water_bed_not_prepared");
            Vector2 position = Position(vertex); bed[vertex] = prepareBed(position.x, position.y);
        }
        return bed[vertex];
    }
    private Vector2 Position(int vertex) => lake != null ? lake.GetNativePosition(vertex) :
        new Vector2(minimumX + vertex % resolution * spacing, minimumZ + vertex / resolution * spacing);
    private bool LakeTriangle(int triangle) => lake != null ? lake.IsNativeWetTriangle(triangle) : lakeTriangles[triangle];
    private void Triangle(int triangle, out int a, out int b, out int c)
    {
        if (lake != null) { lake.GetNativeTriangle(triangle, out a, out b, out c); return; }
        int cell = triangle / 2, column = cell % (resolution - 1), row = cell / (resolution - 1);
        a = row * resolution + column;
        if ((triangle & 1) == 0) { b = a + resolution + 1; c = a + 1; }
        else { b = a + resolution; c = a + resolution + 1; }
    }

    internal YQNativeWaterJoinPatchV2(YQPreparedConnectedBasinV2 lake, int[] channels,
        Func<int, float, float, YQNativeChannelNodeV2> sample)
        : this(lake, 0, 0f, 0f, 0f, null, null, channels, sample) { }

    internal YQNativeWaterJoinPatchV2(int resolution, float minimumX, float minimumZ, float spacing,
        Func<float, float, float> sourceBed, YQPreparedConnectedBasinV2[] lakes, int[] channels,
        Func<int, float, float, YQNativeChannelNodeV2> sample)
        : this(null, resolution, minimumX, minimumZ, spacing, sourceBed, lakes, channels, sample) { }

    private YQNativeWaterJoinPatchV2(YQPreparedConnectedBasinV2 lake, int resolution, float minimumX,
        float minimumZ, float spacing, Func<float, float, float> sourceBed,
        YQPreparedConnectedBasinV2[] lakes, int[] channels, Func<int, float, float, YQNativeChannelNodeV2> sample)
    {
        this.lake = lake; this.resolution = resolution; this.minimumX = minimumX;
        this.minimumZ = minimumZ; this.spacing = spacing;
        channelIndices = (int[])channels.Clone();
        int n = Resolution, vertices = n * n, triangles = 2 * (n - 1) * (n - 1);
        if (lake == null)
        {
            // note: Tiles sample one global native lattice from immutable accepted terrain. They store derived geometry only, never synthetic lake records or per-cell planning state.
            bed = new float[vertices]; prepareBed = sourceBed; lakeTriangles = new bool[triangles]; lakeNodeLevels = new float[vertices];
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                Vector2 position = Position(vertex); bed[vertex] = float.NaN;
                lakeNodeLevels[vertex] = float.NaN;
                // note: Include wet triangles incident across tile boundaries so adjacent tiles pin precisely the same lake/channel edge nodes.
                foreach (var basin in lakes)
                {
                    if (basin == null) continue;
                    int x = Mathf.RoundToInt((position.x - basin.MinimumX) / basin.NativeSpacing);
                    int z = Mathf.RoundToInt((position.y - basin.MinimumZ) / basin.NativeSpacing);
                    if (x < 0 || z < 0 || x >= basin.NativeResolution || z >= basin.NativeResolution) continue;
                    int node = z * basin.NativeResolution + x;
                    for (int row = Math.Max(0, z - 1); row <= Math.Min(basin.NativeResolution - 2, z); row++)
                    for (int column = Math.Max(0, x - 1); column <= Math.Min(basin.NativeResolution - 2, x); column++)
                    for (int side = 0; side < 2; side++)
                    {
                        int triangle = 2 * (row * (basin.NativeResolution - 1) + column) + side;
                        if (!basin.IsNativeWetTriangle(triangle)) continue;
                        basin.GetNativeTriangle(triangle, out int a, out int b, out int c);
                        if (node == a || node == b || node == c) lakeNodeLevels[vertex] = basin.WaterLevel;
                    }
                }
            }
            for (int triangle = 0; triangle < triangles; triangle++)
            {
                int cell = triangle / 2;
                float x = minimumX + cell % (n - 1) * spacing, z = minimumZ + cell / (n - 1) * spacing;
                foreach (var basin in lakes)
                {
                    if (basin == null || x < basin.MinimumX || z < basin.MinimumZ ||
                        x >= basin.MaximumX || z >= basin.MaximumZ) continue;
                    int column = Mathf.RoundToInt((x - basin.MinimumX) / basin.NativeSpacing);
                    int row = Mathf.RoundToInt((z - basin.MinimumZ) / basin.NativeSpacing);
                    if (basin.IsNativeWetTriangle(2 * (row * (basin.NativeResolution - 1) + column) + (triangle & 1)))
                        lakeTriangles[triangle] = true;
                }
            }
        }
        var lakeNodes = new bool[vertices];
        for (int triangle = 0; triangle < triangles; triangle++)
        {
            if (!LakeTriangle(triangle)) continue;
            Triangle(triangle, out int a, out int b, out int c);
            lakeNodes[a] = lakeNodes[b] = lakeNodes[c] = true;
        }
        levels = new float[channels.Length][];
        core = new bool[channels.Length][];
        for (int channel = 0; channel < channels.Length; channel++)
        {
            levels[channel] = new float[vertices]; core[channel] = new bool[vertices];
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                Vector2 point = Position(vertex);
                var node = sample(channels[channel], point.x, point.y);
                // note: Both owners use the lake datum on every shared native edge. Downstream profiles outside the join retain their accepted level.
                levels[channel][vertex] = lake != null ? (lakeNodes[vertex] ? lake.WaterLevel : node.surface) :
                    (!float.IsNaN(lakeNodeLevels[vertex]) ? lakeNodeLevels[vertex] : node.surface);
                core[channel][vertex] = node.core;
            }
        }
        owners = new int[triangles];
        for (int triangle = 0; triangle < triangles; triangle++) owners[triangle] = -1;
        if (lake == null)
        {
            // note: Complete reach connectivity is frozen across the global representation lattice before any tile is exposed to rendering, placement or background queries.
            polygons = Array.Empty<Vector3[]>(); polygonOwners = Array.Empty<int>();
            return;
        }
        for (int triangle = 0; triangle < triangles; triangle++)
        {
            if (LakeTriangle(triangle)) continue;
            Triangle(triangle, out int a, out int b, out int c);
            for (int channel = 0; channel < channels.Length; channel++)
            {
                // note: Archived version-7 bounded joins retain their original source-node admission and canonical channel priority.
                if (!core[channel][a] && !core[channel][b] && !core[channel][c]) continue;
                if (levels[channel][a] <= Bed(a) && levels[channel][b] <= Bed(b) && levels[channel][c] <= Bed(c)) continue;
                owners[triangle] = channel; break;
            }
        }
        FreezePolygons();
    }

    private void FreezePolygons()
    {
        var surfaces = new List<Vector3[]>(); var surfaceOwners = new List<int>();
        // note: Current complete-reach queries index the same clipped float-coordinate polygons as publication. Archived lake-backed joins retain their version-pinned bed equation.
        if (lake == null)
        {
            nativePolygonByTriangle = new int[owners.Length];
            for (int index = 0; index < nativePolygonByTriangle.Length; index++) nativePolygonByTriangle[index] = -1;
        }
        for (int triangle = 0; triangle < owners.Length; triangle++)
        {
            int channel = owners[triangle]; if (channel < 0) continue;
            Triangle(triangle, out int a, out int b, out int c);
            int[] indices = { a, b, c }; var polygon = new List<Vector3>(4);
            for (int edge = 0; edge < 3; edge++)
            {
                int from = indices[edge], to = indices[(edge + 1) % 3];
                float fd = levels[channel][from] - Bed(from), td = levels[channel][to] - Bed(to);
                Vector2 fp = Position(from);
                if (fd > 0f) polygon.Add(new Vector3(fp.x, levels[channel][from], fp.y));
                if ((fd > 0f) == (td > 0f)) continue;
                int low = Math.Min(from, to), high = Math.Max(from, to);
                float ld = levels[channel][low] - Bed(low), hd = levels[channel][high] - Bed(high);
                float fraction = ld / (ld - hd);
                Vector2 point = Vector2.LerpUnclamped(Position(low), Position(high), fraction);
                polygon.Add(new Vector3(point.x, Mathf.LerpUnclamped(levels[channel][low], levels[channel][high], fraction), point.y));
            }
            if (polygon.Count < 3) { owners[triangle] = -1; continue; }
            if (nativePolygonByTriangle != null) nativePolygonByTriangle[triangle] = surfaces.Count;
            surfaces.Add(polygon.ToArray()); surfaceOwners.Add(channelIndices[channel]);
        }
        polygons = surfaces.ToArray(); polygonOwners = surfaceOwners.ToArray(); prepareBed = null;
    }

    internal static bool TryFreezeConnectedReaches(IReadOnlyList<YQNativeWaterJoinPatchV2> patches, out string failure)
    {
        failure = string.Empty;
        if (patches.Count == 0) return true;
        const int cells = 128;
        float tileSize = patches[0].Spacing * cells;
        var byCoordinate = new Dictionary<Vector2Int, int>(); var channels = new SortedSet<int>();
        for (int index = 0; index < patches.Count; index++)
        {
            var patch = patches[index];
            byCoordinate.Add(new Vector2Int(Mathf.RoundToInt(patch.MinimumX / tileSize), Mathf.RoundToInt(patch.MinimumZ / tileSize)), index);
            foreach (int channel in patch.channelIndices) channels.Add(channel);
        }
        foreach (int channel in channels)
        {
            // note: Flood native wet triangles from accepted channel cores through wet shared edges, in canonical feature order. A tile edge does not end water, and corner-only contact does not connect a separate pool.
            var visited = new HashSet<long>(); var pending = new Queue<long>();
            var localChannels = new int[patches.Count];
            for (int index = 0; index < patches.Count; index++) localChannels[index] = Array.IndexOf(patches[index].channelIndices, channel);
            void Admit(int patchIndex, int triangle)
            {
                var patch = patches[patchIndex]; int localChannel = localChannels[patchIndex];
                if (localChannel < 0 || patch.owners[triangle] >= 0 || patch.LakeTriangle(triangle)) return;
                long key = ((long)patchIndex << 32) | (uint)triangle;
                if (!visited.Add(key)) return;
                patch.Triangle(triangle, out int a, out int b, out int c);
                if (patch.levels[localChannel][a] <= patch.Bed(a) && patch.levels[localChannel][b] <= patch.Bed(b) &&
                    patch.levels[localChannel][c] <= patch.Bed(c)) return;
                patch.owners[triangle] = localChannel; pending.Enqueue(key);
            }
            for (int index = 0; index < patches.Count; index++)
            {
                var patch = patches[index]; int localChannel = localChannels[index];
                if (localChannel < 0) continue;
                for (int triangle = 0; triangle < patch.owners.Length; triangle++)
                {
                    patch.Triangle(triangle, out int a, out int b, out int c);
                    if (patch.core[localChannel][a] || patch.core[localChannel][b] || patch.core[localChannel][c]) Admit(index, triangle);
                }
            }
            while (pending.Count > 0)
            {
                long key = pending.Dequeue(); int index = (int)(key >> 32), triangle = (int)(uint)key;
                var patch = patches[index]; int localChannel = localChannels[index], cell = triangle / 2;
                int x = cell % cells, z = cell / cells, side = triangle & 1;
                patch.Triangle(triangle, out int a, out int b, out int c);
                int[] vertices = { a, b, c };
                for (int edge = 0; edge < 3; edge++)
                {
                    int from = vertices[edge], to = vertices[(edge + 1) % 3];
                    if (patch.levels[localChannel][from] <= patch.Bed(from) && patch.levels[localChannel][to] <= patch.Bed(to)) continue;
                    int nx = x, nz = z, nextSide = 1 - side;
                    if (side == 0) { if (edge == 1) nx++; else if (edge == 2) nz--; }
                    else { if (edge == 0) nx--; else if (edge == 1) nz++; }
                    int nextPatch = index;
                    if (nx < 0 || nz < 0 || nx >= cells || nz >= cells)
                    {
                        int tx = Mathf.RoundToInt(patch.MinimumX / tileSize), tz = Mathf.RoundToInt(patch.MinimumZ / tileSize);
                        if (nx < 0) { tx--; nx += cells; } else if (nx >= cells) { tx++; nx -= cells; }
                        if (nz < 0) { tz--; nz += cells; } else if (nz >= cells) { tz++; nz -= cells; }
                        if (!byCoordinate.TryGetValue(new Vector2Int(tx, tz), out nextPatch) || localChannels[nextPatch] < 0)
                        {
                            // note: An unsupported open flood boundary is a rejection with coordinates, never a silently truncated wet surface.
                            Vector2 edgeFrom = patch.Position(from), edgeTo = patch.Position(to);
                            failure = "native_channel_projection_open_boundary:channel=" + channel + ";x=" + tx + ";z=" + tz +
                                ";from=" + edgeFrom.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                edgeFrom.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                                ";to=" + edgeTo.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                edgeTo.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                                ";levels=" + patch.levels[localChannel][from].ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                patch.levels[localChannel][to].ToString("R", System.Globalization.CultureInfo.InvariantCulture) +
                                ";beds=" + patch.Bed(from).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                                patch.Bed(to).ToString("R", System.Globalization.CultureInfo.InvariantCulture);
                            return false;
                        }
                    }
                    Admit(nextPatch, 2 * (nz * cells + nx) + nextSide);
                }
            }
        }
        foreach (var patch in patches) patch.FreezePolygons();
        return true;
    }

    public bool Contains(float x, float z) => x >= MinimumX && z >= MinimumZ && x <= MaximumX && z <= MaximumZ;

    internal void AppendChannelFootprint(List<YQChannelFootprintIndexV2.Triangle> output,
        IReadOnlyList<YQNativeWaterJoinPatchV2> joins, int nextJoin)
    {
        // note: Placement takes the exact immutable polygons used by native water publication, including wet portions beyond the original ribbon.
        for (int i = 0; i < polygons.Length; i++)
        {
            var polygon = new List<Vector2>(polygons[i].Length);
            foreach (var point in polygons[i]) polygon.Add(new Vector2(point.x, point.z));
            if (lake == null)
            {
                // note: Global reach tiles have disjoint interiors; their native polygons already form the final union and must not be re-clipped against every other tile.
                YQChannelFootprintIndexV2.AddPolygon(output, polygon, polygonOwners[i]);
                continue;
            }
            // note: Later render replacements also clip earlier native patches. Honour that order in the derived distance union even when bounded rectangles overlap.
            for (int corner = 1; corner + 1 < polygon.Count; corner++)
                YQChannelFootprintIndexV2.AddSourceTriangle(output, polygon[0], polygon[corner], polygon[corner + 1], polygonOwners[i], joins, nextJoin);
        }
    }

    public bool IntersectsChannelRectangle(int channel, float minX, float minZ, float maxX, float maxZ)
    {
        // note: Publication demand includes interpolated wet triangles beyond the source ribbon, using the same polygons as rendering.
        if (maxX <= MinimumX || minX >= MaximumX || maxZ <= MinimumZ || minZ >= MaximumZ) return false;
        float[] bounds = { minX, maxX, minZ, maxZ };
        for (int index = 0; index < polygons.Length; index++)
        {
            if (polygonOwners[index] != channel) continue;
            var polygon = new List<MeshVertex>(4);
            foreach (var point in polygons[index]) polygon.Add(new MeshVertex { point = point });
            for (int plane = 0; plane < 4 && polygon.Count > 0; plane++) polygon = Clip(polygon, plane, bounds[plane], true);
            if (polygon.Count < 3) continue;
            for (int corner = 1; corner + 1 < polygon.Count; corner++)
                if (Mathf.Abs(Vector3.Cross(polygon[corner].point - polygon[0].point,
                    polygon[corner + 1].point - polygon[0].point).y) > .00000001f) return true;
        }
        return false;
    }

    public bool TrySample(float x, float z, out int channelIndex, out float surface)
    {
        channelIndex = -1; surface = 0f;
        if (!Contains(x, z)) return false;
        int n = Resolution;
        float gx = (x - MinimumX) / Spacing, gz = (z - MinimumZ) / Spacing;
        int cx = Mathf.Clamp(Mathf.FloorToInt(gx), 0, n - 2), cz = Mathf.Clamp(Mathf.FloorToInt(gz), 0, n - 2);
        // note: Check incident triangles as well as the containing one; a shared lake/channel edge belongs to both closed polygons without growing either footprint.
        for (int row = Math.Max(0, cz - 1); row <= Math.Min(n - 2, cz + 1); row++)
        for (int column = Math.Max(0, cx - 1); column <= Math.Min(n - 2, cx + 1); column++)
        for (int side = 0; side < 2; side++)
        {
            int triangle = 2 * (row * (n - 1) + column) + side, owner = owners[triangle];
            if (owner < 0) continue;
                // note: A bed tolerance is not rendered water. Query the frozen native polygon directly so shallow banks and tile edges cannot admit a dry sliver outside the published footprint.
                if (nativePolygonByTriangle != null)
                {
                    int polygon = nativePolygonByTriangle[triangle];
                    if (polygon >= 0 && TrySamplePolygon(polygons[polygon], x, z, out surface))
                    { channelIndex = polygonOwners[polygon]; return true; }
                    continue;
                }
            float fx = gx - column, fz = gz - row;
            float wa = side == 0 ? 1f - fx : 1f - fz, wb = side == 0 ? fz : fz - fx, wc = side == 0 ? fx - fz : fx;
            if (wa < -.000001f || wb < -.000001f || wc < -.000001f) continue;
            Triangle(triangle, out int a, out int b, out int c);
            float level = levels[owner][a] * wa + levels[owner][b] * wb + levels[owner][c] * wc;
            float bed = Bed(a) * wa + Bed(b) * wb + Bed(c) * wc;
            if (bed > level + .000002f) continue;
            channelIndex = channelIndices[owner]; surface = level; return true;
        }
        return false;
    }

    private static bool TrySamplePolygon(Vector3[] polygon, float x, float z, out float surface)
    {
        // note: Double barycentrics evaluate the actual materialized float vertices, including their clipped bank intersections. The tiny dimensionless edge tolerance handles shared closed edges without adding a bed-height band.
        surface = 0f;
        Vector3 a = polygon[0];
        for (int corner = 1; corner + 1 < polygon.Length; corner++)
        {
            Vector3 b = polygon[corner], c = polygon[corner + 1];
            double bx = (double)b.x - a.x, bz = (double)b.z - a.z, cx = (double)c.x - a.x, cz = (double)c.z - a.z;
            double px = (double)x - a.x, pz = (double)z - a.z, determinant = bx * cz - bz * cx;
            if (Math.Abs(determinant) < 1e-10) continue;
            double v = (px * cz - pz * cx) / determinant, w = (bx * pz - bz * px) / determinant;
            if (v < -1e-7 || w < -1e-7 || v + w > 1d + 1e-7) continue;
            surface = (float)(a.y * (1d - v - w) + b.y * v + c.y * w);
            return true;
        }
        return false;
    }

    private struct MeshVertex
    {
        public Vector3 point;
        public Vector2 uv;
        public Color color;
    }

    private static List<MeshVertex> Clip(List<MeshVertex> input, int plane, float bound, bool inside)
    {
        var output = new List<MeshVertex>(8);
        if (input.Count == 0) return output;
        float Coordinate(MeshVertex v) => plane < 2 ? v.point.x : v.point.z;
        bool Keep(MeshVertex v) => (((plane & 1) == 0 ? Coordinate(v) >= bound : Coordinate(v) <= bound) == inside);
        MeshVertex previous = input[input.Count - 1]; bool previousInside = Keep(previous);
        foreach (var current in input)
        {
            bool currentInside = Keep(current);
            if (previousInside != currentInside)
            {
                MeshVertex low = previous, high = current;
                if (Coordinate(low) > Coordinate(high)) { low = current; high = previous; }
                float t = (bound - Coordinate(low)) / (Coordinate(high) - Coordinate(low));
                var hit = new MeshVertex { point = Vector3.LerpUnclamped(low.point, high.point, t),
                    uv = Vector2.LerpUnclamped(low.uv, high.uv, t), color = Color.LerpUnclamped(low.color, high.color, t) };
                if (plane < 2) hit.point.x = bound; else hit.point.z = bound;
                output.Add(hit);
            }
            if (currentInside) output.Add(current);
            previous = current; previousInside = currentInside;
        }
        return output;
    }

    public void ApplyToMesh(Mesh mesh, Transform coordinates, Terrain terrain, int selectedChannel,
        bool append, HashSet<int> retainedChannels = null)
    {
        // note: Synchronous compatibility and cooperative startup share exactly one clipping algorithm and publication order.
        var routine = ApplyToMeshRoutine(mesh, coordinates, terrain, selectedChannel, append, retainedChannels, false);
        try { while (routine.MoveNext()) { } }
        finally { (routine as IDisposable)?.Dispose(); }
    }

    internal IEnumerator ApplyToMeshRoutine(Mesh mesh, Transform coordinates, Terrain terrain, int selectedChannel,
        bool append, HashSet<int> retainedChannels = null, bool cooperative = true)
    {
        // note: Replace only this bounded join's triangles in the existing owned mesh. Original attributes survive outside it; native water and masks share exactly the same wet polygons inside it.
        var oldVertices = mesh.vertices; var oldUV = mesh.uv; var oldColors = mesh.colors; var oldTriangles = mesh.triangles;
        var vertices = new List<Vector3>(); var uv = new List<Vector2>(); var colors = new List<Color>(); var triangles = new List<int>();
        float[] bounds = { MinimumX, MaximumX, MinimumZ, MaximumZ };
        void Emit(List<MeshVertex> polygon)
        {
            if (polygon.Count < 3) return;
            int start = vertices.Count;
            foreach (var vertex in polygon)
            {
                vertices.Add(coordinates != null ? coordinates.InverseTransformPoint(vertex.point) : vertex.point);
                uv.Add(vertex.uv); colors.Add(vertex.color);
            }
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                Vector3 cross = Vector3.Cross(polygon[i].point - polygon[0].point, polygon[i + 1].point - polygon[0].point);
                if (cross.sqrMagnitude <= .0000000001f) continue;
                triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1);
            }
        }
        double sliceStarted = Time.realtimeSinceStartupAsDouble;
        for (int triangle = 0; triangle < oldTriangles.Length; triangle += 3)
        {
            if (cooperative && (triangle & 63) == 0 && Time.realtimeSinceStartupAsDouble - sliceStarted >= .003d)
            {
                // note: The live mesh remains untouched until all clipping completes; rendering continues between deterministic triangle batches.
                yield return null;
                if (mesh == null) yield break;
                sliceStarted = Time.realtimeSinceStartupAsDouble;
            }
            var remainder = new List<MeshVertex>(3);
            for (int corner = 0; corner < 3; corner++)
            {
                int index = oldTriangles[triangle + corner];
                remainder.Add(new MeshVertex { point = coordinates != null ? coordinates.TransformPoint(oldVertices[index]) : oldVertices[index],
                    uv = oldUV.Length == oldVertices.Length ? oldUV[index] : Vector2.zero,
                    color = oldColors.Length == oldVertices.Length ? oldColors[index] : new Color(0f, .5f, 1f, 1f) });
            }
            for (int plane = 0; plane < 4 && remainder.Count > 0; plane++)
            { Emit(Clip(remainder, plane, bounds[plane], false)); remainder = Clip(remainder, plane, bounds[plane], true); }
        }
        if (append)
        for (int polygonIndex = 0; polygonIndex < polygons.Length; polygonIndex++)
        {
            if (cooperative && (polygonIndex & 63) == 0 && Time.realtimeSinceStartupAsDouble - sliceStarted >= .003d)
            {
                yield return null;
                if (mesh == null) yield break;
                sliceStarted = Time.realtimeSinceStartupAsDouble;
            }
            int owner = polygonOwners[polygonIndex];
            if ((selectedChannel >= 0 && owner != selectedChannel) || (retainedChannels != null && retainedChannels.Contains(owner))) continue;
            var polygon = new List<MeshVertex>(4);
            foreach (Vector3 source in polygons[polygonIndex])
            {
                Vector3 point = source;
                point.y = (terrain != null ? terrain.transform.position.y : 0f) +
                    source.y * (terrain != null ? terrain.terrainData.size.y : YQGeneratedWorldTerrain.TerrainHeight) + .005f;
                polygon.Add(new MeshVertex { point = point, uv = new Vector2(point.x, point.z) * .25f,
                    color = new Color(0f, .5f, 1f, 1f) });
            }
            Emit(polygon);
        }
        mesh.Clear(); mesh.indexFormat = vertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(vertices); mesh.SetUVs(0, uv); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
    }
}

// note: This immutable projection supplies the same triangular bed, shoreline and clipped water polygons to all existing terrain/water/placement owners.
public sealed class YQPreparedConnectedBasinV2
{
    public const int NativeDiagonalVersion = 2;
    public const int SharedChannelProjectionVersion = 3;
    public const int CanonicalChannelBanksVersion = 4;
    public const int CanonicalWetFootprintVersion = 5;
    public const int JoinedNativeLakeVersion = 6;
    public const int NativeChannelJoinVersion = 7;
    public const int NativeChannelReachVersion = 8;
    public const int MaximumResolution = 129;
    public const int MaximumSamples = MaximumResolution * MaximumResolution;
    private readonly int resolution;
    private readonly int domainVersion;
    private readonly float spacing;
    private readonly float minimumX;
    private readonly float minimumZ;
    private readonly float level;
    private readonly float[] bed;
    private readonly bool[] wet;
    private readonly Vector2[] shore;
    private readonly Vector2[][] polygons;
    private readonly Vector2[] portals;
    private readonly Vector2[] distanceBoundary;
    private struct ShoreNode
    {
        public Vector2 minimum, maximum;
        public int start, count, left, right;
    }
    private readonly ShoreNode[] shoreNodes;
    private readonly int[] shoreOrder;
    public float MinimumX => minimumX;
    public int Version => domainVersion;
    public float MinimumZ => minimumZ;
    public float MaximumX => minimumX + spacing * (resolution - 1);
    public float MaximumZ => minimumZ + spacing * (resolution - 1);
    public int PolygonCount => polygons.Length;
    public int ShoreSegmentCount => shore.Length / 2;
    public int PortalSegmentCount => portals.Length / 2;
    internal int NativeResolution => resolution;
    internal float NativeSpacing => spacing;
    internal float WaterLevel => level;
    internal float GetNativeBed(int vertex) => bed[vertex];
    internal bool IsNativeWetTriangle(int triangle) => wet[triangle];
    internal void GetNativeTriangle(int triangle, out int a, out int b, out int c)
        => VertexIndices(triangle, resolution, domainVersion, out a, out b, out c);
    internal Vector2 GetNativePosition(int vertex) => Position(vertex);

    private YQPreparedConnectedBasinV2(YQConnectedBasinDomainV2 source, float level, bool[] selected)
    {
        resolution = source.resolution;
        domainVersion = source.version;
        spacing = source.spacing;
        minimumX = source.minimumX;
        minimumZ = source.minimumZ;
        this.level = level;
        bed = source.bed.ToArray();
        wet = selected;
        var edges = new List<Vector2>();
        var surfaces = new List<Vector2[]>();
        foreach (int triangle in source.wetTriangles)
        {
            VertexIndices(triangle, resolution, domainVersion, out int a, out int b, out int c);
            var indices = new[] { a, b, c };
            var polygon = new List<Vector2>(4);
            var contour = new List<Vector2>(2);
            for (int i = 0; i < 3; i++)
            {
                int from = indices[i], to = indices[(i + 1) % 3];
                bool fromWet = bed[from] < level, toWet = bed[to] < level;
                if (fromWet) polygon.Add(Position(from));
                if (fromWet == toWet) continue;
                // note: Canonical endpoint order makes a shared edge's crossing bit-identical in adjacent triangles and independently clipped cells.
                int low = Math.Min(from, to), high = Math.Max(from, to);
                float t = (level - bed[low]) / (bed[high] - bed[low]);
                Vector2 crossing = Position(low) + (Position(high) - Position(low)) * t;
                polygon.Add(crossing);
                contour.Add(crossing);
            }
            if (polygon.Count >= 3) surfaces.Add(polygon.ToArray());
            if (contour.Count == 2 && (contour[1] - contour[0]).sqrMagnitude > .00000001f)
                edges.AddRange(contour);
        }
        polygons = surfaces.ToArray();
        shore = edges.ToArray();
        // note: A bounded lake-to-channel ownership edge is a portal, not a terrain-contact shoreline. Retain it explicitly so verification cannot silently discard it.
        var boundary = new Dictionary<long, int>();
        foreach (int triangle in source.wetTriangles)
        {
            VertexIndices(triangle, resolution, domainVersion, out int a, out int b, out int c);
            for (int edge = 0; edge < 3; edge++)
            {
                int from = edge == 0 ? a : edge == 1 ? b : c, to = edge == 0 ? b : edge == 1 ? c : a;
                long key = ((long)Math.Min(from, to) << 32) | (uint)Math.Max(from, to);
                boundary.TryGetValue(key, out int count); boundary[key] = count + 1;
            }
        }
        var portalEdges = new List<Vector2>();
        var boundaryKeys = new List<long>(boundary.Keys); boundaryKeys.Sort();
        foreach (long key in boundaryKeys)
        {
            if (boundary[key] != 1) continue;
            int from = (int)(key >> 32), to = (int)(uint)key;
            if (bed[from] >= level && bed[to] >= level) continue;
            Vector2 first = Position(from), second = Position(to);
            if (bed[from] >= level || bed[to] >= level)
            {
                Vector2 crossing = first + (second - first) * ((level - bed[from]) / (bed[to] - bed[from]));
                if (bed[from] >= level) first = crossing; else second = crossing;
            }
            if ((first - second).sqrMagnitude > .00000001f) portalEdges.AddRange(new[] { first, second });
        }
        portals = portalEdges.ToArray();
        // note: Spatial exclusions measure the complete wet boundary, including channel portals; physical-contact checks retain the separate shore array.
        distanceBoundary = new Vector2[shore.Length + portals.Length];
        Array.Copy(shore, distanceBoundary, shore.Length); Array.Copy(portals, 0, distanceBoundary, shore.Length, portals.Length);
        shoreOrder = new int[distanceBoundary.Length / 2];
        for (int i = 0; i < shoreOrder.Length; i++) shoreOrder[i] = i;
        var nodes = new List<ShoreNode>();
        if (shoreOrder.Length > 0) BuildShoreIndex(nodes, 0, shoreOrder.Length);
        shoreNodes = nodes.ToArray();
    }

    private int BuildShoreIndex(List<ShoreNode> nodes, int start, int count)
    {
        // note: Build once during preparation; exact nearest-segment queries then prune immutable bounding boxes instead of walking every contour for each terrain/placement sample.
        var node = new ShoreNode { minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity),
            maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity), start = start, count = count, left = -1, right = -1 };
        for (int i = start; i < start + count; i++)
        {
            int edge = shoreOrder[i] * 2;
            node.minimum = Vector2.Min(node.minimum, Vector2.Min(distanceBoundary[edge], distanceBoundary[edge + 1]));
            node.maximum = Vector2.Max(node.maximum, Vector2.Max(distanceBoundary[edge], distanceBoundary[edge + 1]));
        }
        int index = nodes.Count; nodes.Add(node);
        if (count > 8)
        {
            bool xAxis = node.maximum.x - node.minimum.x >= node.maximum.y - node.minimum.y;
            Array.Sort(shoreOrder, start, count, Comparer<int>.Create((first, second) =>
            {
                Vector2 a = distanceBoundary[first * 2] + distanceBoundary[first * 2 + 1], b = distanceBoundary[second * 2] + distanceBoundary[second * 2 + 1];
                int order = (xAxis ? a.x : a.y).CompareTo(xAxis ? b.x : b.y);
                return order != 0 ? order : first.CompareTo(second);
            }));
            int half = count / 2;
            node.left = BuildShoreIndex(nodes, start, half); node.right = BuildShoreIndex(nodes, start + half, count - half);
            node.count = 0; nodes[index] = node;
        }
        return index;
    }

    private static float NodeDistanceSquared(ShoreNode node, Vector2 point)
    {
        float x = Mathf.Max(0f, Mathf.Max(node.minimum.x - point.x, point.x - node.maximum.x));
        float z = Mathf.Max(0f, Mathf.Max(node.minimum.y - point.y, point.y - node.maximum.y));
        return x * x + z * z;
    }

    private void FindNearestShore(int nodeIndex, Vector2 point, ref float best)
    {
        var node = shoreNodes[nodeIndex];
        // note: A small conservative float allowance preserves candidates at bounding-box roundoff boundaries; leaf distances always use the original exact segment projection.
        if (NodeDistanceSquared(node, point) > best + Mathf.Max(.000001f, best * .000001f)) return;
        if (node.count > 0)
        {
            for (int i = node.start; i < node.start + node.count; i++)
            {
                int edge = shoreOrder[i] * 2;
                Vector2 delta = distanceBoundary[edge + 1] - distanceBoundary[edge];
                float t = Mathf.Clamp01(Vector2.Dot(point - distanceBoundary[edge], delta) / delta.sqrMagnitude);
                best = Mathf.Min(best, (point - distanceBoundary[edge] - delta * t).sqrMagnitude);
            }
            return;
        }
        bool leftFirst = NodeDistanceSquared(shoreNodes[node.left], point) <= NodeDistanceSquared(shoreNodes[node.right], point);
        FindNearestShore(leftFirst ? node.left : node.right, point, ref best);
        FindNearestShore(leftFirst ? node.right : node.left, point, ref best);
    }

    public static bool TryCreate(float minimumX, float minimumZ, float spacing, int resolution,
        float seedX, float seedZ, float waterLevel, Func<float, float, float> sampleBed,
        out YQConnectedBasinDomainV2 domain, out string failure)
    {
        domain = null;
        failure = string.Empty;
        if (sampleBed == null || !ValidGeometry(minimumX, minimumZ, spacing, resolution, seedX, seedZ, waterLevel))
        { failure = "invalid_basin_grid"; return false; }
        // note: New records use the native lower-left/upper-right diagonal confirmed against TerrainData and TerrainCollider; stored version-1 records keep their original triangular geometry.
        // note: Version 3 additionally pins finite/streamed channel projection to the same prepared ribbon and receiving level; accepted versions 1 and 2 retain their earlier runtime interpretation.
        // note: Version 4 also pins materialized banks to accepted control frames; earlier records retain their published ribbon interpretation.
        // note: Version 5 confines channel bed cuts to the accepted ribbon and reaches the water datum at its banks; archived padding remains version-pinned.
        var draft = new YQConnectedBasinDomainV2 { version = NativeChannelReachVersion, minimumX = minimumX, minimumZ = minimumZ,
            spacing = spacing, resolution = resolution, seedX = seedX, seedZ = seedZ };
        for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float height = sampleBed(minimumX + x * spacing, minimumZ + z * spacing);
                if (!Finite(height) || height < 0f || height > 1f)
                { failure = "invalid_basin_bed"; return false; }
                draft.bed.Add(height);
            }
        if (!TryFlood(draft, waterLevel, out bool[] component, out failure)) return false;
        for (int i = 0; i < component.Length; i++) if (component[i]) draft.wetTriangles.Add(i);
        domain = draft;
        return true;
    }

    public static bool TryPrepare(YQConnectedBasinDomainV2 source, float waterLevel,
        out YQPreparedConnectedBasinV2 prepared, out string failure)
    {
        prepared = null;
        failure = string.Empty;
        if (source == null || (source.version != 1 && source.version != NativeDiagonalVersion && source.version != SharedChannelProjectionVersion && source.version != CanonicalChannelBanksVersion && source.version != CanonicalWetFootprintVersion && source.version != JoinedNativeLakeVersion && source.version != NativeChannelJoinVersion && source.version != NativeChannelReachVersion) ||
            !ValidGeometry(source.minimumX, source.minimumZ, source.spacing, source.resolution, source.seedX, source.seedZ, waterLevel) ||
            source.bed == null || source.bed.Count != source.resolution * source.resolution ||
            source.wetTriangles == null || source.wetTriangles.Count == 0 ||
            source.wetTriangles.Count > 2 * (source.resolution - 1) * (source.resolution - 1))
        { failure = "invalid_basin_domain"; return false; }
        foreach (float height in source.bed)
            if (!Finite(height) || height < 0f || height > 1f)
            { failure = "invalid_basin_bed"; return false; }
        if (!TryFlood(source, waterLevel, out bool[] component, out failure)) return false;
        int cursor = 0;
        for (int i = 0; i < component.Length; i++)
        {
            if (!component[i]) continue;
            if (cursor >= source.wetTriangles.Count || source.wetTriangles[cursor++] != i)
            { failure = "basin_component_mismatch"; return false; }
        }
        if (cursor != source.wetTriangles.Count) { failure = "basin_component_mismatch"; return false; }
        prepared = new YQPreparedConnectedBasinV2(source, waterLevel, component);
        if (prepared.shore.Length == 0 || prepared.polygons.Length == 0)
        { prepared = null; failure = "empty_basin_shore"; return false; }
        return true;
    }

    private static bool ValidGeometry(float x, float z, float step, int count, float sx, float sz, float water)
    {
        // note: Reject malformed persisted dimensions before multiplication, indexing or allocating any grid.
        return count >= 3 && count <= MaximumResolution && Finite(step) && step >= .5f && step <= 32f &&
            Finite(x) && Finite(z) && Math.Abs(x) <= 10000000f && Math.Abs(z) <= 10000000f &&
            Finite(sx) && Finite(sz) && sx > x && sz > z &&
            sx < x + step * (count - 1) && sz < z + step * (count - 1) && Finite(water) && water > 0f && water < 1f;
    }

    private static bool TryFlood(YQConnectedBasinDomainV2 source, float waterLevel, out bool[] selected, out string failure,
        bool[] allowed = null)
    {
        int n = source.resolution, cells = n - 1;
        selected = new bool[2 * cells * cells];
        failure = string.Empty;
        float gx = (source.seedX - source.minimumX) / source.spacing, gz = (source.seedZ - source.minimumZ) / source.spacing;
        int cx = (int)gx, cz = (int)gz;
        int start = 2 * (cz * cells + cx) + TriangleSide(gx - cx, gz - cz, source.version);
        if (allowed != null && !allowed[start]) { failure = "joined_lake_seed_outside_ownership"; return false; }
        VertexIndices(start, n, source.version, out int sa, out int sb, out int sc);
        float seedBed = Interpolate(source.bed[sa], source.bed[sb], source.bed[sc], gx - cx, gz - cz, (start & 1) != 0, source.version);
        if (seedBed >= waterLevel) { failure = "basin_seed_is_dry"; return false; }
        var edgeOwners = new Dictionary<long, int>(selected.Length * 2);
        var neighbors = new int[selected.Length * 3];
        for (int i = 0; i < neighbors.Length; i++) neighbors[i] = -1;
        for (int triangle = 0; triangle < selected.Length; triangle++)
        {
            VertexIndices(triangle, n, source.version, out int a, out int b, out int c);
            for (int e = 0; e < 3; e++)
            {
                int first = e == 0 ? a : e == 1 ? b : c, second = e == 0 ? b : e == 1 ? c : a;
                if (source.bed[first] >= waterLevel && source.bed[second] >= waterLevel) continue;
                // note: Wet components connect across a positive-length wet edge, never across a single dry crest vertex.
                long key = ((long)Math.Min(first, second) << 32) | (uint)Math.Max(first, second);
                if (edgeOwners.TryGetValue(key, out int other))
                { neighbors[triangle * 3 + e] = other / 3; neighbors[other] = triangle; }
                else edgeOwners.Add(key, triangle * 3 + e);
            }
        }
        var pending = new Queue<int>();
        pending.Enqueue(start); selected[start] = true;
        while (pending.Count > 0)
        {
            int triangle = pending.Dequeue();
            VertexIndices(triangle, n, source.version, out int a, out int b, out int c);
            for (int vertex = 0; vertex < 3; vertex++)
            {
                int v = vertex == 0 ? a : vertex == 1 ? b : c;
                int vx = v % n, vz = v / n;
                if ((vx == 0 || vz == 0 || vx == n - 1 || vz == n - 1) && source.bed[v] < waterLevel)
                { failure = "basin_reaches_planning_boundary"; return false; }
            }
            for (int e = 0; e < 3; e++)
            {
                int neighbor = neighbors[triangle * 3 + e];
                if (neighbor < 0 || selected[neighbor] || (allowed != null && !allowed[neighbor])) continue;
                selected[neighbor] = true; pending.Enqueue(neighbor);
            }
        }
        return true;
    }

    internal static bool TryProjectNativeLake(YQPreparedConnectedBasinV2 geology, float seedX, float seedZ,
        Func<float, float, float> sampleFinalBed, out YQPreparedConnectedBasinV2 prepared, out string failure)
    {
        prepared = null; failure = string.Empty;
        // note: Rebuildable geometry is bounded by the accepted geological patch, aligned to the production two-metre grid and independent of render-cell arrival/quality. It never floods a downstream reach at the lake datum.
        const float step = YQGeneratedWorldTerrain.WorldSize / (YQGeneratedWorldTerrain.HeightmapResolution - 1f);
        // note: The wet component's bounds, with a four-cell ownership halo, avoid resampling the much larger geological search square on every prepared runtime transaction.
        var minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var polygon in geology.polygons)
            foreach (Vector2 point in polygon) { minimum = Vector2.Min(minimum, point); maximum = Vector2.Max(maximum, point); }
        float x = Mathf.Floor(minimum.x / step) * step - 4f * step, z = Mathf.Floor(minimum.y / step) * step - 4f * step;
        int count = Mathf.CeilToInt(Mathf.Max(maximum.x + 4f * step - x, maximum.y + 4f * step - z) / step) + 1;
        if (count > 2 * MaximumResolution + 1 || sampleFinalBed == null)
        { failure = "joined_lake_native_grid_unbounded"; return false; }
        var draft = new YQConnectedBasinDomainV2 { version = geology.Version, spacing = step,
            minimumX = x, minimumZ = z, resolution = count, seedX = seedX, seedZ = seedZ };
        for (int row = 0; row < count; row++)
            for (int column = 0; column < count; column++)
            {
                float value = sampleFinalBed(x + column * step, z + row * step);
                if (!Finite(value) || value < 0f || value > 1f) { failure = "joined_lake_invalid_native_bed"; return false; }
                draft.bed.Add(value);
            }
        var allowed = new bool[2 * (count - 1) * (count - 1)];
        float halo = step * 1.414214f;
        for (int triangle = 0; triangle < allowed.Length; triangle++)
        {
            VertexIndices(triangle, count, draft.version, out int a, out int b, out int c);
            float cx = x + ((a % count + b % count + c % count) / 3f) * step;
            float cz = z + ((a / count + b / count + c / count) / 3f) * step;
            allowed[triangle] = geology.SignedShoreDistance(cx, cz) <= halo;
        }
        if (!TryFlood(draft, geology.level, out bool[] selected, out failure, allowed)) return false;
        for (int triangle = 0; triangle < selected.Length; triangle++) if (selected[triangle]) draft.wetTriangles.Add(triangle);
        prepared = new YQPreparedConnectedBasinV2(draft, geology.level, selected);
        if (prepared.ShoreSegmentCount == 0 || prepared.PolygonCount == 0)
        { prepared = null; failure = "joined_lake_empty_native_domain"; return false; }
        return true;
    }

    private static int TriangleSide(float x, float z, int version) => version == 1 ? (x + z > 1f ? 1 : 0) : (x >= z ? 0 : 1);

    private static void VertexIndices(int triangle, int n, int version, out int a, out int b, out int c)
    {
        int cell = triangle / 2, x = cell % (n - 1), z = cell / (n - 1), origin = z * n + x;
        if (version == 1)
        {
            if ((triangle & 1) == 0) { a = origin; b = origin + n; c = origin + 1; }
            else { a = origin + 1; b = origin + n; c = origin + n + 1; }
        }
        else
        {
            // note: Keep upward winding with the same diagonal as the native terrain's physical triangles.
            a = origin;
            if ((triangle & 1) == 0) { b = origin + n + 1; c = origin + 1; }
            else { b = origin + n; c = origin + n + 1; }
        }
    }

    private Vector2 Position(int vertex) => new Vector2(minimumX + vertex % resolution * spacing, minimumZ + vertex / resolution * spacing);
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    private static float Interpolate(float a, float b, float c, float x, float z, bool upper, int version)
    {
        if (version == 1) return upper ? a * (1f - z) + b * (1f - x) + c * (x + z - 1f) : a * (1f - x - z) + b * z + c * x;
        return upper ? a * (1f - z) + b * (z - x) + c * x : a * (1f - x) + b * z + c * (x - z);
    }

    public bool TrySampleBed(float x, float z, out float height, out bool inside)
    {
        height = level; inside = false;
        if (!Finite(x) || !Finite(z)) return false;
        float gx = (x - minimumX) / spacing, gz = (z - minimumZ) / spacing;
        if (gx < 0f || gz < 0f || gx >= resolution - 1 || gz >= resolution - 1) return false;
        int cx = (int)gx, cz = (int)gz;
        int triangle = 2 * (cz * (resolution - 1) + cx) + TriangleSide(gx - cx, gz - cz, domainVersion);
        VertexIndices(triangle, resolution, domainVersion, out int a, out int b, out int c);
        height = Interpolate(bed[a], bed[b], bed[c], gx - cx, gz - cz, (triangle & 1) != 0, domainVersion);
        inside = wet[triangle] && height < level;
        return true;
    }

    internal bool CoversVegetationRectangle(Bounds bounds, YQChannelFootprintIndexV2 channels)
        => InspectVegetationRectangle(bounds, channels, out _);

    internal bool InspectVegetationRectangle(Bounds bounds, YQChannelFootprintIndexV2 channels, out Vector2 dryWitness)
    {
        // note: Inspect every native triangle intersecting the planting rectangle, retaining dry islands and allowing accepted channels to cover dry lake remnants.
        Vector3 min = bounds.min, max = bounds.max;
        dryWitness = new Vector2(float.NaN, float.NaN);
        if (min.x < MinimumX || min.z < MinimumZ || max.x >= MaximumX || max.z >= MaximumZ) return false;
        int firstX = Math.Max(0, Mathf.FloorToInt((min.x - minimumX) / spacing));
        int firstZ = Math.Max(0, Mathf.FloorToInt((min.z - minimumZ) / spacing));
        int lastX = Math.Min(resolution - 2, Mathf.FloorToInt((max.x - minimumX) / spacing));
        int lastZ = Math.Min(resolution - 2, Mathf.FloorToInt((max.z - minimumZ) / spacing));
        for (int z = firstZ; z <= lastZ; z++)
        for (int x = firstX; x <= lastX; x++)
        for (int side = 0; side < 2; side++)
        {
            int triangle = 2 * (z * (resolution - 1) + x) + side;
            VertexIndices(triangle, resolution, domainVersion, out int a, out int b, out int c);
            if (wet[triangle] && bed[a] < level && bed[b] < level && bed[c] < level) continue;
            Vector2 pa = Position(a), pb = Position(b), pc = Position(c);
            var dry = new List<YQChannelFootprintIndexV2.CoveragePoint> {
                new YQChannelFootprintIndexV2.CoveragePoint(pa.x, pa.y),
                new YQChannelFootprintIndexV2.CoveragePoint(pb.x, pb.y),
                new YQChannelFootprintIndexV2.CoveragePoint(pc.x, pc.y) };
            dry = YQChannelFootprintIndexV2.ClipCoverage(dry, p => p.x - min.x);
            dry = YQChannelFootprintIndexV2.ClipCoverage(dry, p => max.x - p.x);
            dry = YQChannelFootprintIndexV2.ClipCoverage(dry, p => p.z - min.z);
            dry = YQChannelFootprintIndexV2.ClipCoverage(dry, p => max.z - p.z);
            if (wet[triangle])
            {
                double determinant = ((double)pb.x - pa.x) * ((double)pc.y - pa.y) - ((double)pb.y - pa.y) * ((double)pc.x - pa.x);
                dry = YQChannelFootprintIndexV2.ClipCoverage(dry, p => {
                    double u = ((p.x - pa.x) * ((double)pc.y - pa.y) - (p.z - pa.y) * ((double)pc.x - pa.x)) / determinant;
                    double v = (((double)pb.x - pa.x) * (p.z - pa.y) - ((double)pb.y - pa.y) * (p.x - pa.x)) / determinant;
                    return bed[a] + u * (bed[b] - bed[a]) + v * (bed[c] - bed[a]) - level;
                });
            }
            if (!YQChannelFootprintIndexV2.HasCoverageArea(dry)) continue;
            if (channels == null) { dryWitness = YQChannelFootprintIndexV2.CoverageCentroid(dry); return false; }
            if (!channels.MeasureCoverage(dry, out dryWitness)) return false;
        }
        dryWitness = new Vector2(float.NaN, float.NaN);
        return true;
    }

    public float SignedShoreDistance(float x, float z)
    {
        // note: No allocation or mutation occurs in hot terrain queries; placement can request the same exact shore even outside the bounded patch.
        Vector2 p = new Vector2(x, z);
        if (!Finite(x) || !Finite(z)) return float.PositiveInfinity;
        float best = float.PositiveInfinity;
        FindNearestShore(0, p, ref best);
        TrySampleBed(x, z, out _, out bool inside);
        return Mathf.Sqrt(best) * (inside ? -1f : 1f);
    }

    public void GetShoreSegment(int index, out Vector2 first, out Vector2 second)
    { first = shore[index * 2]; second = shore[index * 2 + 1]; }

    public void GetPortalSegment(int index, out Vector2 first, out Vector2 second)
    { first = portals[index * 2]; second = portals[index * 2 + 1]; }

    public bool TryFirstShoreCrossing(Vector2 start, Vector2 direction, out Vector2 crossing)
    {
        crossing = default;
        if (direction.sqrMagnitude <= .000001f || !TrySampleBed(start.x, start.y, out _, out bool inside) || !inside) return false;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < shore.Length; i += 2)
        {
            Vector2 edge = shore[i + 1] - shore[i], offset = shore[i] - start;
            float denominator = direction.x * edge.y - direction.y * edge.x;
            if (Mathf.Abs(denominator) <= .0000001f) continue;
            float t = (offset.x * edge.y - offset.y * edge.x) / denominator;
            float u = (offset.x * direction.y - offset.y * direction.x) / denominator;
            if (t < 0f || t >= nearest || u < 0f || u > 1f) continue;
            nearest = t; crossing = start + direction * t;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    public void AppendClippedTriangles(float minX, float minZ, float maxX, float maxZ, List<Vector2> vertices, List<int> triangles)
    {
        // note: Clip each accepted wet polygon to terrain ownership bounds; dry islands remain holes without independently fitted ellipses or chunk-local floods.
        var input = new List<Vector2>(8);
        var output = new List<Vector2>(8);
        foreach (Vector2[] polygon in polygons)
        {
            input.Clear(); input.AddRange(polygon);
            for (int plane = 0; plane < 4 && input.Count > 0; plane++)
            {
                output.Clear();
                float bound = plane == 0 ? minX : plane == 1 ? maxX : plane == 2 ? minZ : maxZ;
                Vector2 previous = input[input.Count - 1];
                float PreviousCoordinate(Vector2 v) => plane < 2 ? v.x : v.y;
                bool IsInside(Vector2 v) => (plane & 1) == 0 ? PreviousCoordinate(v) >= bound : PreviousCoordinate(v) <= bound;
                bool previousInside = IsInside(previous);
                foreach (Vector2 current in input)
                {
                    bool currentInside = IsInside(current);
                    if (currentInside != previousInside)
                    {
                        Vector2 low = previous, high = current;
                        if (PreviousCoordinate(low) > PreviousCoordinate(high)) { low = current; high = previous; }
                        float t = (bound - PreviousCoordinate(low)) / (PreviousCoordinate(high) - PreviousCoordinate(low));
                        Vector2 hit = low + (high - low) * t;
                        if (plane < 2) hit.x = bound; else hit.y = bound;
                        output.Add(hit);
                    }
                    if (currentInside) output.Add(current);
                    previous = current; previousInside = currentInside;
                }
                var swap = input; input = output; output = swap;
            }
            if (input.Count < 3) continue;
            int offset = vertices.Count;
            vertices.AddRange(input);
            for (int i = 1; i + 1 < input.Count; i++)
            {
                Vector2 ab = input[i] - input[0], ac = input[i + 1] - input[0];
                if (Mathf.Abs(ab.x * ac.y - ab.y * ac.x) < .0000001f) continue;
                triangles.Add(offset); triangles.Add(offset + i); triangles.Add(offset + i + 1);
            }
        }
    }
}
