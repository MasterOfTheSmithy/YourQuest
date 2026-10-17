using System;
using System.Collections;
using System.Collections.Generic;

// note: The runner injects the entire production raster bodies; dependencies are controlled managed fixtures.
static class RasterChecks
{
    static int checks, failures;
    static void Check(bool result, string label) { checks++; if (!result) failures++; Console.WriteLine((result ? "PASS " : "FAIL ") + label); }
    static void Main()
    {
        foreach (bool preserve in new[] { false, true })
        foreach (bool legacy in new[] { false, true })
        {
            var a = new RasterFixture(preserve, legacy);
            var b = new RasterFixture(preserve, legacy);
            a.Run(false); b.Run(true);
            Check(Equal(a.targets, b.targets) && Equal(a.strengths, b.strengths) && Equal(a.blends, b.blends), $"Exact raster arrays preserve={preserve} legacy={legacy}");
            Check(a.modifiedSamples == b.modifiedSamples && a.terracedPaths == b.terracedPaths && Equal(a.minimum, b.minimum) && Equal(a.maximum, b.maximum), $"Exact sample counts and dirty rows preserve={preserve} legacy={legacy}");
            Check(a.modifiedSamples > 0, $"Fixture grades actual cells preserve={preserve} legacy={legacy}");
            if (!legacy)
            {
                Check(b.macroWater.preparedV2.Calls < a.macroWater.preparedV2.Calls, $"Overlapping corridors reuse samples preserve={preserve}");
                Check(b.macroWater.preparedV2.MaxCallsAtPoint == 1, $"Each requested grid point sampled once preserve={preserve}");
                Console.WriteLine($"QUERY_COUNTS preserve={preserve} old={a.macroWater.preparedV2.Calls} new={b.macroWater.preparedV2.Calls}");
            }
        }
        var skippedOld = new RasterFixture(true, false) { rejectAll = true };
        var skippedNew = new RasterFixture(true, false) { rejectAll = true };
        skippedOld.Run(false); skippedNew.Run(true);
        Check(skippedOld.modifiedSamples == 0 && skippedNew.modifiedSamples == 0, "Skipped-heavy fixture never grades rejected cells");
        Check(skippedOld.MaximumVisitedBetweenYields > 64 && skippedNew.MaximumVisitedBetweenYields <= 18, "Skipped cells participate in each-cell cooperative checks");
        Check(skippedNew.Visited == skippedOld.Visited, "Yield repair visits exactly the original candidate cells");
        Console.WriteLine($"SKIPPED_VISITS_BETWEEN_YIELDS old={skippedOld.MaximumVisitedBetweenYields} new={skippedNew.MaximumVisitedBetweenYields}");
        var expensive = new RasterFixture(true, false) { rejectAll = true, PerCellSeconds = .002f }; expensive.Run(true);
        Check(expensive.MaximumVisitedBetweenYields <= 6, "Two-millisecond cells yield within eight milliseconds plus one indivisible cell and float tolerance");
        Console.WriteLine($"EXPENSIVE_VISITS_BETWEEN_YIELDS new={expensive.MaximumVisitedBetweenYields} syntheticCellMs=2");
        var changed = new RasterFixture(true, false); changed.Run(true);
        var firstTargets = changed.targets;
        changed.macroWater.preparedV2.Version = 2; changed.Run(true);
        var changedOld = new RasterFixture(true, false); changedOld.macroWater.preparedV2.Version = 2; changedOld.Run(false);
        Check(!Equal(firstTargets, changed.targets), "Repeated invocation on same owner resamples changed immutable input");
        Check(Equal(changedOld.targets, changed.targets), "Second transaction retains old-body arithmetic with fresh cache");
        Check(changed.macroWater.preparedV2.MaxCallsAtPoint == 1 && changed.macroWater.preparedV2.Calls > 0, "Cache is local to one repair invocation");
        Check(RasterFixture.CacheElementBytes == 16, "Production cache element occupies sixteen managed bytes");
        Console.WriteLine($"CACHE_BYTES resolution513={RasterFixture.CacheElementBytes*513*513}");
        Console.WriteLine($"RESULT checks={checks} failures={failures}");
        Environment.ExitCode = failures == 0 ? 0 : 1;
    }
    static bool Equal(float[,] a, float[,] b) { for(int z=0;z<a.GetLength(0);z++) for(int x=0;x<a.GetLength(1);x++) if(BitConverter.SingleToInt32Bits(a[z,x]) != BitConverter.SingleToInt32Bits(b[z,x])) return false; return true; }
    static bool Equal(int[] a, int[] b) { for(int i=0;i<a.Length;i++) if(a[i]!=b[i]) return false; return true; }
}

class RasterFixture
{
    /*__CACHE__*/
    public readonly List<LivedPathSegment> paths = new();
    public readonly List<float[]> networkProfiles = new();
    public readonly List<LivedPathTerrainReservation> protectedFootprints = new();
    public readonly int resolution = 257;
    public readonly float[,] heights;
    public readonly Vector3 terrainPosition = new(-128, 2, -128), terrainSize = new(256, 100, 256);
    public readonly MacroWaterSet macroWater;
    public readonly bool preserveConnectedBasinGeometry;
    public int modifiedSamples, terracedPaths;
    public float[,] targets, strengths, blends;
    public int[] minimum, maximum;
    public bool rejectAll;
    public float PerCellSeconds = .0005f;
    public static int CacheElementBytes => System.Runtime.CompilerServices.Unsafe.SizeOf<LivedPathRasterWaterSample>();
    public int Visited, MaximumVisitedBetweenYields;
    public RasterFixture(bool preserve, bool legacy)
    {
        preserveConnectedBasinGeometry = preserve;
        heights = new float[resolution,resolution];
        for(int z=0;z<resolution;z++) for(int x=0;x<resolution;x++) heights[z,x] = .2f + .0002f*(x+z);
        macroWater = new MacroWaterSet { preparedV2 = legacy ? null : new Prepared(), count = legacy ? 1 : 0, basins = new[] { new YQGeneratedWorldTerrain.MacroWaterBasinDescriptor() } };
        paths.Add(new LivedPathSegment { start = new(-124,-116), end = new(124,116), halfWidth=3, shoulderWidth=7 });
        paths.Add(new LivedPathSegment { start = new(-124,116), end = new(124,-116), halfWidth=4, shoulderWidth=5, terraced=true });
        paths.Add(new LivedPathSegment { start = new(-100,-16), end = new(100,16), halfWidth=6, shoulderWidth=8, curveAmplitude=3 });
        paths.Add(new LivedPathSegment { start = new(-124,-116), end = new(124,116), halfWidth=2, shoulderWidth=5 });
        foreach(var path in paths) networkProfiles.Add(new[] { 25f, 28f, 27f, 33f });
        protectedFootprints.Add(new LivedPathTerrainReservation());
    }
    public void Run(bool newer)
    {
        modifiedSamples=0; terracedPaths=0; Visited=0; MaximumVisitedBetweenYields=0; macroWater.preparedV2?.ResetCounters();
        Time.realtimeSinceStartup = 0; YQGeneratedWorldRuntimeBuilder.ProgressCalls = 0;
        var routine = newer ? New() : Old(); int last = 0;
        while(routine.MoveNext()) { MaximumVisitedBetweenYields = Math.Max(MaximumVisitedBetweenYields, Visited-last); last = Visited; }
        MaximumVisitedBetweenYields = Math.Max(MaximumVisitedBetweenYields, Visited-last);
    }
    IEnumerator Old()
    {
        int[] dirtyMinimumXByRow = new int[resolution], dirtyMaximumXByRow = new int[resolution];
        Array.Fill(dirtyMinimumXByRow, resolution); Array.Fill(dirtyMaximumXByRow, -1);
        const float routeWorkSliceSeconds = .008f; float frameStartedAt = Time.realtimeSinceStartup;
        /*__OLD_BODY__*/
        targets = roadTargets; strengths = roadStrengths; blends = roadBlends; minimum=dirtyMinimumXByRow; maximum=dirtyMaximumXByRow;
        yield break;
    }
    IEnumerator New()
    {
        int[] dirtyMinimumXByRow = new int[resolution], dirtyMaximumXByRow = new int[resolution];
        Array.Fill(dirtyMinimumXByRow, resolution); Array.Fill(dirtyMaximumXByRow, -1);
        const float routeWorkSliceSeconds = .008f; float frameStartedAt = Time.realtimeSinceStartup;
        /*__NEW_BODY__*/
        targets = roadTargets; strengths = roadStrengths; blends = roadBlends; minimum=dirtyMinimumXByRow; maximum=dirtyMaximumXByRow;
        yield break;
    }
    bool TryResolveLivedPathSample(LivedPathSegment path, Vector2 point, out float t, out Vector2 closest, out float distance)
    {
        Visited++; Time.realtimeSinceStartup += PerCellSeconds;
        Vector2 delta = path.end-path.start; float n=delta.x*delta.x+delta.y*delta.y;
        t=Mathf.Clamp01(((point.x-path.start.x)*delta.x+(point.y-path.start.y)*delta.y)/n);
        closest=new(path.start.x+t*delta.x,path.start.y+t*delta.y); distance=Vector2.Distance(point,closest);
        return !rejectAll;
    }
    static bool IsInsideLivedPathTerrainReservation(IReadOnlyList<LivedPathTerrainReservation> reservations, Vector2 p) => p.x>=-25 && p.x<=-10 && p.y>=-30 && p.y<=30;
    static float SampleLivedPathHeightProfile(float[] values, float t) { float q=t*(values.Length-1); int i=Math.Min(values.Length-2,(int)q); return Mathf.Lerp(values[i],values[i+1],q-i); }
    static float SmoothThreshold(float start,float end,float value) { float t=Mathf.Clamp01((value-start)/(end-start)); return t*t*(3-2*t); }
}

struct Vector2 { public float x,y; public Vector2(float x,float y){this.x=x;this.y=y;} public float magnitude=>MathF.Sqrt(x*x+y*y); public static Vector2 operator -(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y); public static float Distance(Vector2 a,Vector2 b)=>(a-b).magnitude; }
struct Vector3 { public float x,y,z; public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;} }
static class Mathf { public static float Min(float a,float b)=>Math.Min(a,b); public static float Max(float a,float b)=>Math.Max(a,b); public static int Min(int a,int b)=>Math.Min(a,b); public static int Max(int a,int b)=>Math.Max(a,b); public static float Abs(float a)=>Math.Abs(a); public static float Round(float a)=>MathF.Round(a); public static int FloorToInt(float a)=>(int)MathF.Floor(a); public static int CeilToInt(float a)=>(int)MathF.Ceiling(a); public static int Clamp(int a,int b,int c)=>Math.Clamp(a,b,c); public static float Clamp01(float a)=>Math.Clamp(a,0,1); public static float Lerp(float a,float b,float t)=>a+(b-a)*Clamp01(t); }
static class Time { public static float realtimeSinceStartup; }
static class YQGeneratedWorldRuntimeBuilder { public static int ProgressCalls; public static void ReportInitialGenerationProgress()=>ProgressCalls++; }
static class YQStartupLoadingScreen { public static readonly List<string> Stages = new(); public static void SetGenerationSubstep(string value)=>Stages.Add(value); public static void SetGenerationWorkStage(string a,int b,int c,string d,float e)=>Stages.Add(d); }
class LivedPathSegment { public Vector2 start,end; public float halfWidth,shoulderWidth,curveAmplitude; public bool terraced; }
class LivedPathTerrainReservation { }
struct YQSpatialTerrainSampleV2 { public float connectedBasinSupportMask,waterMask,waterSurfaceNormalized; }
class Prepared
{
    public int Calls,Version=1;
    readonly Dictionary<(float,float),int> counts = new();
    public void ResetCounters() { Calls=0; counts.Clear(); }
    public int MaxCallsAtPoint { get { int max=0; foreach(var n in counts.Values) max=Math.Max(max,n); return max; } }
    public YQSpatialTerrainSampleV2 SampleTerrain(float x,float z)
    {
        Calls++; var key=(x,z); counts.TryGetValue(key,out int n); counts[key]=n+1;
        return new() { connectedBasinSupportMask=x>30 && x<50 && z>20 && z<60?1:0, waterMask=x>-12 && x<12?1:0,waterSurfaceNormalized=.33f+.08f*(Version-1)+.0001f*z };
    }
}
class MacroWaterSet { public Prepared preparedV2; public int count; public YQGeneratedWorldTerrain.MacroWaterBasinDescriptor[] basins; }
static class YQGeneratedWorldTerrain { public struct MacroWaterBasinDescriptor { public float WaterSurfaceY=>37; public bool ContainsXZ(Vector3 p,float margin)=>p.x*p.x+p.z*p.z<900; } }
static class YQContinuousWorldFeatureAuthority { public const float AcceptedTerrainTransitionDistance=12; public static float SampleAcceptedRouteGrade(float source,float target,float height,float half,float shoulder,float distance)=>Mathf.Lerp(source,target,1-Mathf.Clamp01((distance-half)/(shoulder+AcceptedTerrainTransitionDistance))); }
