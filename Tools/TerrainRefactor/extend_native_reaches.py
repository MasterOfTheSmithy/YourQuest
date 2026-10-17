"""Apply the version-pinned finite channel projection to exact saved preimages."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
DOMAIN = ROOT / 'Assets/Assets/Scripts/Generated/YQConnectedBasinDomainV2.cs'
SAMPLER = ROOT / 'Assets/Assets/Scripts/Generated/YQSpatialBlueprintTerrainSamplerV2.cs'

def replace(text, old, new):
    assert text.count(old) == 1, old[:120]
    return text.replace(old, new)

text = DOMAIN.read_text(encoding='utf-8-sig')
start = text.index('public sealed class YQNativeWaterJoinPatchV2')
end = text.index('// note: This immutable projection', start)
part = text[start:end]
part = replace(part, '    private readonly YQPreparedConnectedBasinV2 lake;', '''    private readonly YQPreparedConnectedBasinV2 lake;
    private readonly int resolution;
    private readonly float spacing, minimumX, minimumZ;
    private readonly float[] bed;
    private readonly bool[] lakeTriangles;
    private readonly float[] lakeNodeLevels;''')
part = replace(part, '''    public float MinimumX => lake.MinimumX;
    public float MinimumZ => lake.MinimumZ;
    public float MaximumX => lake.MaximumX;
    public float MaximumZ => lake.MaximumZ;''', '''    public float MinimumX => lake != null ? lake.MinimumX : minimumX;
    public float MinimumZ => lake != null ? lake.MinimumZ : minimumZ;
    public float MaximumX => lake != null ? lake.MaximumX : minimumX + spacing * (resolution - 1);
    public float MaximumZ => lake != null ? lake.MaximumZ : minimumZ + spacing * (resolution - 1);
    private int Resolution => lake != null ? lake.NativeResolution : resolution;
    private float Spacing => lake != null ? lake.NativeSpacing : spacing;
    private float Bed(int vertex) => lake != null ? lake.GetNativeBed(vertex) : bed[vertex];
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
    }''')
part = replace(part, '''    {
        this.lake = lake; channelIndices = (int[])channels.Clone();
        int n = lake.NativeResolution, vertices = n * n, triangles = 2 * (n - 1) * (n - 1);''', '''        : this(lake, 0, 0f, 0f, 0f, null, null, channels, sample) { }

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
            bed = new float[vertices]; lakeTriangles = new bool[triangles]; lakeNodeLevels = new float[vertices];
            for (int vertex = 0; vertex < vertices; vertex++)
            {
                Vector2 position = Position(vertex); bed[vertex] = sourceBed(position.x, position.y);
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
        }''')
# note: Keep all historical version-7 operations on their original lake values and endpoint order.
part = part.replace('lake.NativeResolution, vertices', 'Resolution, vertices')
part = part.replace('lake.IsNativeWetTriangle(triangle)', 'LakeTriangle(triangle)')
# Restore the wrapper itself after mechanical substitution.
part = part.replace('lake != null ? LakeTriangle(triangle)', 'lake != null ? lake.IsNativeWetTriangle(triangle)')
part = part.replace('lake.GetNativeTriangle(triangle, out int a, out int b, out int c);', 'Triangle(triangle, out int a, out int b, out int c);')
part = part.replace('lake.GetNativePosition(vertex)', 'Position(vertex)')
part = part.replace('lake != null ? Position(vertex)', 'lake != null ? lake.GetNativePosition(vertex)')
part = part.replace('lakeNodes[vertex] ? lake.WaterLevel : node.surface', 'lake != null ? (lakeNodes[vertex] ? lake.WaterLevel : node.surface) :\n                    (!float.IsNaN(lakeNodeLevels[vertex]) ? lakeNodeLevels[vertex] : node.surface)')
import re
part = re.sub(r'lake.GetNativeBed\((\w+)\)', r'Bed(\1)', part)
part = part.replace('lake != null ? Bed(vertex)', 'lake != null ? lake.GetNativeBed(vertex)')
part = re.sub(r'lake.GetNativePosition\((from|low|high)\)', r'Position(\1)', part)
part = part.replace('int n = lake.NativeResolution;', 'int n = Resolution;')
part = part.replace('/ lake.NativeSpacing', '/ Spacing')
text = text[:start] + part + text[end:]
text = replace(text, '    public const int NativeChannelJoinVersion = 7;', '    public const int NativeChannelJoinVersion = 7;\n    public const int NativeChannelReachVersion = 8;')
text = replace(text, 'version = NativeChannelJoinVersion, minimumX', 'version = NativeChannelReachVersion, minimumX')
text = replace(text, 'source.version != NativeChannelJoinVersion) ||', 'source.version != NativeChannelJoinVersion && source.version != NativeChannelReachVersion) ||')
DOMAIN.write_text(text, encoding='utf-8')

text = SAMPLER.read_text(encoding='utf-8-sig')
old = '''        // note: Freeze native channel ownership in the same bounded lake patches. Renderers and placement consume this cache; source heights never depend on it.
        for (int index = 0; index < sampler.joinedLakes.Length; index++)
            if (sampler.joinedLakes[index] != null && sampler.waters[index].basin.Version >= YQPreparedConnectedBasinV2.NativeChannelJoinVersion)
                sampler.nativeJoins.Add(new YQNativeWaterJoinPatchV2(sampler.joinedLakes[index], channels.ToArray(), sampler.SampleNativeChannelNode));'''
new = '''        bool nativeReaches = false;
        foreach (var water in sampler.waters)
            if (water.basin != null && water.basin.Version >= YQPreparedConnectedBasinV2.NativeChannelReachVersion) nativeReaches = true;
        if (nativeReaches)
            sampler.PrepareNativeReaches(channels.ToArray(), finalBed.SampleNormalized);
        else
        {
            // note: Archived version-7 worlds keep their bounded join projection exactly; only new version-8 domains opt into complete finite reaches.
            for (int index = 0; index < sampler.joinedLakes.Length; index++)
                if (sampler.joinedLakes[index] != null && sampler.waters[index].basin.Version >= YQPreparedConnectedBasinV2.NativeChannelJoinVersion)
                    sampler.nativeJoins.Add(new YQNativeWaterJoinPatchV2(sampler.joinedLakes[index], channels.ToArray(), sampler.SampleNativeChannelNode));
        }'''
text = replace(text, old, new)
marker = '    private void PrepareChannelFootprint()'
new = '''    private void PrepareNativeReaches(int[] channels, Func<float, float, float> sourceBed)
    {
        // note: Representation tiles cover accepted bank segments and their native interpolation support. Global coordinates, channel order and source heights determine every tile independently of traversal or render-cell arrival.
        const float tileSize = 256f, support = 4f;
        var tiles = new HashSet<Vector2Int>();
        foreach (int channel in channels)
        {
            var water = waters[channel];
            for (int point = 1; point < water.pointCount; point++)
            {
                var a = waterPoints[water.pointStart + point - 1]; var b = waterPoints[water.pointStart + point];
                Vector3 sideA = GetAcceptedChannelBankOffset(channel, point - 1), sideB = GetAcceptedChannelBankOffset(channel, point);
                float minX = Mathf.Min(a.x - Mathf.Abs(sideA.x), b.x - Mathf.Abs(sideB.x)) - support;
                float maxX = Mathf.Max(a.x + Mathf.Abs(sideA.x), b.x + Mathf.Abs(sideB.x)) + support;
                float minZ = Mathf.Min(a.z - Mathf.Abs(sideA.z), b.z - Mathf.Abs(sideB.z)) - support;
                float maxZ = Mathf.Max(a.z + Mathf.Abs(sideA.z), b.z + Mathf.Abs(sideB.z)) + support;
                for (int z = Mathf.FloorToInt(minZ / tileSize); z <= Mathf.FloorToInt(maxZ / tileSize); z++)
                for (int x = Mathf.FloorToInt(minX / tileSize); x <= Mathf.FloorToInt(maxX / tileSize); x++)
                    tiles.Add(new Vector2Int(x, z));
            }
        }
        var ordered = new List<Vector2Int>(tiles);
        ordered.Sort((a, b) => a.y != b.y ? a.y.CompareTo(b.y) : a.x.CompareTo(b.x));
        foreach (var tile in ordered)
            nativeJoins.Add(new YQNativeWaterJoinPatchV2(129, tile.x * tileSize, tile.y * tileSize, 2f,
                sourceBed, joinedLakes, channels, SampleNativeChannelNode));
    }

'''
text = replace(text, marker, new + marker)
SAMPLER.write_text(text, encoding='utf-8')
