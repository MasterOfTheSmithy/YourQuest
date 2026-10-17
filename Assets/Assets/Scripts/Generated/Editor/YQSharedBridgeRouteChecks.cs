using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.IO;
using UnityEditor;
using UnityEngine;

// note: Exercise the compiled production route splice with synthetic immutable layouts; reflection belongs only to this detached verifier.
internal static class SharedRouteChecks
{
    private static readonly Type Owner = typeof(YQContinuousWorldFeatureMaterializer);
    private static readonly Type Span = Owner.GetNestedType("AcceptedCrossingSpan", BindingFlags.NonPublic);
    private static readonly Type Layout = Owner.GetNestedType("AcceptedBridgeLayout", BindingFlags.NonPublic);
    private static readonly MethodInfo Apply = Owner.GetMethod("ApplySharedBridgeRoute", BindingFlags.NonPublic | BindingFlags.Static);
    private static int failures;
    [MenuItem("YourQuest/Terrain Refactor/Verify Shared Bridge Route Contracts (Disposable)")]
    private static void Verify()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            throw new InvalidOperationException("Shared route fixtures require idle Edit Mode.");
        // note: The materializer initializes native profiler markers, so execute its compiled splice inside Unity; synthetic cache keys never reference a live world or player.
        var previous = Console.Out; var output = new StringWriter(); failures = 0; int result;
        try { Console.SetOut(output); result = Main(); }
        finally { Console.SetOut(previous); }
        string directory = "outputs/TerrainRefactor_20261012/route_startup_rejection/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_shared_route_contracts";
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "Checks.txt"), output.ToString());
        File.WriteAllText(Path.Combine(directory, "Receipt.json"), Newtonsoft.Json.JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow.ToString("O"), passed = result == 0, checks = 25, failures,
            source = YQStateContract.Sha256Hex(File.ReadAllText("Assets/Assets/Scripts/Generated/YQContinuousWorldFeatureAuthority.cs")),
            bridgeSource = YQStateContract.Sha256Hex(File.ReadAllText("Assets/Assets/Scripts/Generated/YQGeneratedRiverBridge.cs")),
            nativeTreadSource = YQStateContract.Sha256Hex(File.ReadAllText("Assets/Assets/Scripts/Generated/YQNativeBridgeTread.cs")),
            editorSource = YQStateContract.Sha256Hex(File.ReadAllText("Assets/Assets/Scripts/Generated/Editor/YQSharedBridgeRouteChecks.cs")),
            capturedProfileSource = YQStateContract.Sha256Hex(File.ReadAllText("outputs/TerrainRefactor_20261012/bend_followup/Captured_Profile.json")),
            terrainApiObservation = ObserveTerrainApis(),
            runtimeMvid = typeof(YQContinuousWorldFeatureMaterializer).Assembly.ManifestModule.ModuleVersionId,
            evidence = "UNITY_EDITOR_COMPILED_SHARED_ROUTE_SYNTHETIC_CONTRACTS; NOT_PLAYER_TRAVERSAL"
        }, Newtonsoft.Json.Formatting.Indented));
        if (result != 0) throw new InvalidOperationException("Shared bridge contract failure: " + directory);
        Debug.Log("[YQSharedBridgeRoutes] All twenty-five contracts passed: " + directory);
    }
    private static Vector3 P(float x, float z = 0f) => new Vector3(x, 0f, z);
    private static object ObserveTerrainApis()
    {
        // note: Observe actual API values on an owned disposable terrain; this is diagnostic evidence, not another accepted world or a traversal assertion.
        var data = new TerrainData { heightmapResolution = 33, size = new Vector3(32,10,32), hideFlags = HideFlags.HideAndDontSave };
        GameObject surface = null;
        try {
            var heights = new float[33,33]; heights[10,11] = 1f; heights[11,10] = 1f; data.SetHeights(0,0,heights);
            surface = Terrain.CreateTerrainGameObject(data); surface.hideFlags = HideFlags.HideAndDontSave;
            surface.transform.position = new Vector3(100000,7,100000); Physics.SyncTransforms();
            var terrain = surface.GetComponent<Terrain>(); var collider = surface.GetComponent<TerrainCollider>();
            Vector3 point = surface.transform.position + new Vector3(10.5f,0,10.5f);
            float terrainSample = terrain.SampleHeight(point);
            float dataSample = data.GetInterpolatedHeight(10.5f/32f,10.5f/32f);
            bool hit = collider.Raycast(new Ray(point + Vector3.up * 30, Vector3.down), out var contact, 60);
            return new { terrainSample, dataSample, colliderHit = hit, colliderHeight = hit ? contact.point.y - 7f : float.NaN,
                independentBilinearHeight = 5f, independentNativeDiagonalHeight = 0f };
        } finally { if (surface != null) UnityEngine.Object.DestroyImmediate(surface); UnityEngine.Object.DestroyImmediate(data); }
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    private static YQPreparedSpatialMaterializationV2 Prepared(int version)
    {
        var basin = (YQPreparedConnectedBasinV2)FormatterServices.GetUninitializedObject(typeof(YQPreparedConnectedBasinV2));
        Set(basin, "domainVersion", version);
        var prepared = (YQPreparedSpatialMaterializationV2)FormatterServices.GetUninitializedObject(typeof(YQPreparedSpatialMaterializationV2));
        Set(prepared, "waters", new[] { new YQSpatialMaterializationWaterV2 { connectedBasin = basin } });
        return prepared;
    }
    private static object Crossing(float start, float end, bool sameBank = false)
    {
        var bridge = Activator.CreateInstance(Span, true);
        Set(bridge, "points", new List<Vector3> { P(start, 4f), P((start + end) * .5f, 4f), P(end, 4f) });
        var span = Activator.CreateInstance(Span, true);
        Set(span, "crossingId", "synthetic:" + start); Set(span, "routeIndex", 0); Set(span, "bridge", bridge);
        Set(span, "points", new List<Vector3> { P(start), P(end) });
        var banks = (IList)Span.GetField("bankConnections").GetValue(span);
        banks.Add(new List<Vector3> { P(start), P(start, 2f), P(start, 4f) });
        banks.Add(new List<Vector3> { P(end), P(end, 2f), P(sameBank ? start : end, 4f) });
        return span;
    }
    private static void Install(YQPreparedSpatialMaterializationV2 prepared, params object[] spans)
    {
        var layout = Activator.CreateInstance(Layout, true); Set(layout, "ready", true);
        var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Span));
        foreach (var span in spans) list.Add(span);
        Set(layout, "spans", list);
        object cache = Owner.GetField("BridgeLayouts", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        cache.GetType().GetMethod("Add").Invoke(cache, new[] { (object)prepared, layout });
    }
    private static List<Vector2> Line() => new List<Vector2> { new Vector2(0f,0f), new Vector2(5f,0f), new Vector2(10f,0f) };
    private static void Run(YQPreparedSpatialMaterializationV2 prepared, List<Vector2> line) => Apply.Invoke(null, new object[] { prepared, 0, line });
    private static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
    private static void Reject(Action action)
    {
        try { action(); } catch (TargetInvocationException error) when (error.InnerException is InvalidOperationException) { return; }
        throw new Exception("Unsafe layout was not rejected.");
    }
    private static void Check(string name, Action action)
    {
        try { action(); Console.WriteLine("PASS " + name); }
        catch (Exception error) { failures++; Console.WriteLine("FAIL " + name + ": " + error); }
    }
    public static int Main()
    {
        // note: Rebuild the exact recorded failing centre profile through both production mesh paths, retaining the old projection as a controlled before comparison.
        var captured = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText("outputs/TerrainRefactor_20261012/bend_followup/Captured_Profile.json"));
        var recordedRoad = new List<Vector3>();
        foreach (var point in captured["road"]) recordedRoad.Add(new Vector3((float)point["x"],(float)point["y"],(float)point["z"]));
        float recordedWidth = (float)captured["width"];
        var legacyBuilder = typeof(YQGeneratedRiverBridge).GetMethod("BuildWalkSurfaceMesh", BindingFlags.NonPublic | BindingFlags.Static);
        var currentBuilder = typeof(YQGeneratedRiverBridge).GetMethod("BuildNativeWalkSurfaceMesh", BindingFlags.NonPublic | BindingFlags.Static);
        var supportBuilder = typeof(YQGeneratedRiverBridge).GetMethod("BuildNativeSupportSurfaceMesh", BindingFlags.NonPublic | BindingFlags.Static);
        var native=Owner.Assembly.GetType("YQNativeBridgeTread");
        var fit=native.GetMethod("FitProfile",BindingFlags.NonPublic|BindingFlags.Static);
        var nativeRoad=(List<Vector3>)fit.Invoke(null,new object[] { recordedRoad,recordedWidth,18f });
        Check("CAPTURED-BUFFERED-LEGACY-REPRODUCES-STEEP-TREAD", () => {
            Mesh mesh = (Mesh)legacyBuilder.Invoke(null,new object[] { recordedRoad,recordedWidth });
            try { Require(MaximumTreadSlope(mesh)>80f,"Recorded steep-face defect did not reproduce."); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        });
        Check("CAPTURED-NATIVE-SWEPT-FULL-TREAD-GRADE", () => {
            for(int index=0;index<recordedRoad.Count;index++) Require(nativeRoad[index].x==recordedRoad[index].x&&nativeRoad[index].z==recordedRoad[index].z&&nativeRoad[index].y>=recordedRoad[index].y,"Accepted lower bound or layout changed.");
            Mesh mesh = (Mesh)currentBuilder.Invoke(null,new object[] { nativeRoad,recordedWidth });
            // note: Preserve the failing compiled triangle coordinates for diagnosis without publishing or moving a player.
            try { File.WriteAllText("outputs/TerrainRefactor_20261012/sweep_followup/NativeMesh.json",Newtonsoft.Json.JsonConvert.SerializeObject(new { vertices=Array.ConvertAll(mesh.vertices,point=>new { x=point.x,y=point.y,z=point.z }),triangles=mesh.triangles },Newtonsoft.Json.Formatting.Indented));
                float maximum = MaximumTreadSlope(mesh); Console.WriteLine("OBSERVATION currentMaximumTreadSlope="+maximum);
                Require(maximum<=18.02f,"Full tread exceeds the accepted 18-degree route grade: "+maximum); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        });
        Check("CAPTURED-NATIVE-SWEPT-REVERSE-FULL-TREAD-GRADE", () => {
            var reverse = new List<Vector3>(recordedRoad); reverse.Reverse();
            var reverseRoad=(List<Vector3>)fit.Invoke(null,new object[] { reverse,recordedWidth,18f });
            for(int index=0;index<nativeRoad.Count;index++) Require((nativeRoad[index]-reverseRoad[reverseRoad.Count-1-index]).sqrMagnitude<.00000001f,"Traversal ordering changed native profile.");
            Mesh mesh = (Mesh)currentBuilder.Invoke(null,new object[] { reverseRoad,recordedWidth });
            try { float maximum = MaximumTreadSlope(mesh); Console.WriteLine("OBSERVATION reverseMaximumTreadSlope="+maximum);
                Require(maximum<=18.02f,"Reversed full tread exceeds accepted grade: "+maximum); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        });
        Check("CAPTURED-NATIVE-SHOULDER-GRADE-AND-TOP-DATUM", () => {
            Mesh mesh=(Mesh)supportBuilder.Invoke(null,new object[] { nativeRoad,recordedWidth });
            Mesh deck=(Mesh)currentBuilder.Invoke(null,new object[] { nativeRoad,recordedWidth });
            try { Require(MaximumTreadSlope(mesh)<=18.02f,"Shoulder exceeds accepted route grade.");
                Vector3[] top=deck.vertices,shoulder=mesh.vertices; Require(top.Length==shoulder.Length,"Station ownership diverged.");
                for(int index=0;index<top.Length;index+=4) Require(Mathf.Abs(top[index].y-shoulder[index].y-.01f)<.00002f,"Shoulder became a competing top datum."); }
            finally { UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(deck); }
        });
        Check("CAPTURED-NATIVE-BENT-SUBDIVISION-SHARES-EXACT-SECTION", () => {
            int cut=9; var side=(Vector2)native.GetMethod("SectionOffset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[] { nativeRoad,cut });
            var build=native.GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Static);
            Mesh left=(Mesh)build.Invoke(null,new object[] { nativeRoad.GetRange(0,cut+1),recordedWidth,.22f,.173f,false,"left",null,(Vector2?)side });
            Mesh right=(Mesh)build.Invoke(null,new object[] { nativeRoad.GetRange(cut,nativeRoad.Count-cut),recordedWidth,.22f,.173f,false,"right",(Vector2?)side,null });
            try { Vector3[] a=left.vertices,b=right.vertices;
                for(int index=0;index<4;index++) Require((a[a.Length-4+index]-b[index]).sqrMagnitude<.000000001f,"Bent arch sections have a physical seam.");
                Require(MaximumTreadSlope(left)<=18.02f&&MaximumTreadSlope(right)<=18.02f,"Subdivision changed accepted grade."); }
            finally { UnityEngine.Object.DestroyImmediate(left); UnityEngine.Object.DestroyImmediate(right); }
        });
        Check("NATIVE-SWEPT-INVALID-CONTRACTS-REJECT", () => {
            foreach(float width in new[] { 0f,-1f,float.NaN,float.PositiveInfinity }) Reject(()=>fit.Invoke(null,new object[] { recordedRoad,width,18f }));
            foreach(float grade in new[] { 0f,90f,float.NaN }) Reject(()=>fit.Invoke(null,new object[] { recordedRoad,recordedWidth,grade }));
        });
        Check("NATIVE-BRIDGE-OFF-CENTRE-TERRAIN-CREST-MEASURED", () => {
            // note: A terrain crest outside the centreline caused the captured exit footprint failure. The actual production corridor query must retain that obstacle as a lower bound.
            var method = Owner.GetMethod("MeasureNativeBridgeSegmentFloor", BindingFlags.NonPublic | BindingFlags.Static);
            Func<Vector3,float> terrain = point => Mathf.Abs(point.x-1.5f)<.01f && Mathf.Abs(point.z-2f)<.01f ? 69f : 60f;
            float floor = (float)method.Invoke(null,new object[] { P(0),P(4),3f,terrain });
            Require(floor==69f,"Off-centre finished terrain was omitted.");
        });
        Check("NATIVE-BRIDGE-FOOTPRINT-REVERSE-ORDER-INVARIANT", () => {
            var method = Owner.GetMethod("MeasureNativeBridgeSegmentFloor", BindingFlags.NonPublic | BindingFlags.Static);
            Func<Vector3,float> terrain = point => point.x*.25f+point.z*.5f;
            float forward = (float)method.Invoke(null,new object[] { P(0),P(4),3f,terrain });
            float reverse = (float)method.Invoke(null,new object[] { P(4),P(0),3f,terrain });
            Require(Mathf.Abs(forward-reverse)<.00001f,"Approach order changed the terrain floor.");
        });
        Check("NATIVE-BRIDGE-INVALID-FOOTPRINT-REJECTS", () => {
            var method = Owner.GetMethod("MeasureNativeBridgeSegmentFloor", BindingFlags.NonPublic | BindingFlags.Static);
            Func<Vector3,float> invalid = point => float.NaN;
            Reject(()=>method.Invoke(null,new object[] { P(0),P(4),3f,invalid }));
            Reject(()=>method.Invoke(null,new object[] { P(0),P(0),3f,new Func<Vector3,float>(point=>0f) }));
        });
        Check("NATIVE-DECK-GRADE-CLOSE-CREST-RETAINS-LOWER-BOUNDS", () => {
            // note: The captured entry loses support after a high terrain crest beside a closely spaced lower sample. Exercise that pattern against the actual production grade projection, not a relaxed probe.
            var source = new List<Vector3> { new Vector3(44.028576f,68.21353f,15.975803f), new Vector3(46.945866f,69.27366f,15.603532f),
                new Vector3(47.95355f,68.09019f,15.277704f), new Vector3(50.802956f,67.99784f,14.356366f), new Vector3(56.876476f,67.78512f,11.110003f) };
            var method = Owner.GetMethod("ConstrainNativeBridgeDeckGrade", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (List<Vector3>)method.Invoke(null, new object[] { source, 18f });
            float grade = Mathf.Tan(18f * Mathf.Deg2Rad);
            for (int index=0; index<source.Count; index++) {
                Require(result[index].x == source[index].x && result[index].z == source[index].z && result[index].y >= source[index].y, "Layout or terrain/water lower bound changed.");
                if (index>0) Require(Mathf.Abs(result[index].y-result[index-1].y) <= grade*Vector2.Distance(new Vector2(source[index].x,source[index].z),new Vector2(source[index-1].x,source[index-1].z))+.00002f, "Accepted route grade exceeded.");
            }
            Require(source[2].y==68.09019f && result[2].y>68.9f, "Source mutated or close drop remained.");
        });
        Check("NATIVE-DECK-GRADE-REVERSE-ORDER-INVARIANT", () => {
            var source = new List<Vector3> { new Vector3(0,1,0),new Vector3(3,4,0),new Vector3(5,2,2),new Vector3(12,3,2) };
            var method = Owner.GetMethod("ConstrainNativeBridgeDeckGrade", BindingFlags.NonPublic | BindingFlags.Static);
            var forward = (List<Vector3>)method.Invoke(null,new object[] { source,10f }); source.Reverse();
            var reverse = (List<Vector3>)method.Invoke(null,new object[] { source,10f }); reverse.Reverse();
            for(int index=0;index<forward.Count;index++) Require((forward[index]-reverse[index]).sqrMagnitude<.00000001f,"Traversal order changed grade.");
        });
        Check("NATIVE-DECK-GRADE-ALREADY-VALID-PROFILE-UNCHANGED", () => {
            var source = new List<Vector3> { new Vector3(0,4,0),new Vector3(10,5,0),new Vector3(20,4,0) };
            var method = Owner.GetMethod("ConstrainNativeBridgeDeckGrade", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (List<Vector3>)method.Invoke(null,new object[] { source,10f });
            for(int index=0;index<source.Count;index++) Require(result[index]==source[index],"An already valid grade changed.");
        });
        Check("NATIVE-DECK-GRADE-INVALID-ACCEPTED-LIMIT-REJECTS", () => {
            var method = Owner.GetMethod("ConstrainNativeBridgeDeckGrade", BindingFlags.NonPublic | BindingFlags.Static);
            foreach(float grade in new[] { 0f,90f,float.NaN,float.PositiveInfinity })
                Reject(()=>method.Invoke(null,new object[] { new List<Vector3> { P(0),P(10) },grade }));
        });
        Check("NATIVE-DECK-FINISHED-BANKS-NOT-RIVERBED", () => {
            var source = new List<Vector3> { new Vector3(0,10,0), new Vector3(2,3,0), new Vector3(10,12,0) };
            var method = Owner.GetMethod("BuildNativeBridgeDeckProfile", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (List<Vector3>)method.Invoke(null, new object[] { source, new Func<Vector3,float>(point => point.x == 0 ? 14f : point.x == 10 ? 16f : 3f) });
            Require(Mathf.Abs(result[0].y - 14.2f) < .0001f && Mathf.Abs(result[1].y - 14.6f) < .0001f && Mathf.Abs(result[2].y - 16.2f) < .0001f, "Finished bank grade was not interpolated by distance.");
            Require(source[1].y == 3f && result[1].x == source[1].x && result[1].z == source[1].z, "Accepted layout mutated.");
        });
        Check("NATIVE-DECK-ACCEPTED-WATER-CLEARANCE-LOWER-BOUND", () => {
            var source = new List<Vector3> { new Vector3(0,10,0), new Vector3(5,20,0), new Vector3(10,10,0) };
            var method = Owner.GetMethod("BuildNativeBridgeDeckProfile", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (List<Vector3>)method.Invoke(null, new object[] { source, new Func<Vector3,float>(_ => 9f) });
            Require(result[1].y == 20f && result[0].y == 10f, "Accepted clearance lowered.");
        });
        Check("NATIVE-DECK-FINISHED-INTERIOR-CREST-LOWER-BOUND", () => {
            var source = new List<Vector3> { new Vector3(0,10,0), new Vector3(5,3,0), new Vector3(10,10,0) };
            var method = Owner.GetMethod("BuildNativeBridgeDeckProfile", BindingFlags.NonPublic | BindingFlags.Static);
            var result = (List<Vector3>)method.Invoke(null, new object[] { source, new Func<Vector3,float>(point => point.x == 5 ? 20f : 9f) });
            Require(Mathf.Abs(result[1].y - 20.2f) < .0001f && source[1].y == 3f, "Finished crest intersects the derived deck or changed the cached layout.");
        });
        Check("NATIVE-DECK-INVALID-BANK-REJECTS", () => {
            var method = Owner.GetMethod("BuildNativeBridgeDeckProfile", BindingFlags.NonPublic | BindingFlags.Static);
            Reject(() => method.Invoke(null, new object[] { new List<Vector3> { P(0), P(10) }, new Func<Vector3,float>(_ => float.NaN) }));
        });
        Check("SHARED-ROUTE-COMPLETE-BANKS-DECK-AND-SEMANTIC-ENDPOINTS", () => {
            var prepared = Prepared(8); object span = Crossing(2f,8f); Install(prepared,span); var line = Line(); Run(prepared,line);
            Require(line[0] == new Vector2(0f,0f) && line[line.Count-1] == new Vector2(10f,0f), "Endpoint identity changed.");
            foreach (var point in new[] { new Vector2(2f,2f), new Vector2(2f,4f), new Vector2(5f,4f), new Vector2(8f,4f), new Vector2(8f,2f) })
                Require(line.Contains(point), "A published bank/deck leg was omitted: " + point);
            Require(!line.Contains(new Vector2(5f,0f)), "Obsolete wet centre remained the validation corridor.");
            Require(((List<Vector3>)Span.GetField("points").GetValue(span)).Count == 2, "Cached geometry mutated.");
        });
        Check("SHARED-ROUTE-SAME-CONNECTED-BANK", () => {
            var prepared=Prepared(8); Install(prepared,Crossing(2f,8f,true)); var line=Line(); Run(prepared,line);
            Require(line.Contains(new Vector2(8f,2f)) && !line.Contains(new Vector2(5f,4f)), "Same-bank path acquired an unrelated crossing.");
        });
        Check("SHARED-ROUTE-MULTIPLE-INTERVALS-KEEP-MIDDLE-ROAD", () => {
            var prepared=Prepared(8); Install(prepared,Crossing(6f,8f),Crossing(2f,4f)); var line=Line(); Run(prepared,line);
            Require(line.Contains(new Vector2(5f,0f)) && line.Contains(new Vector2(3f,4f)) && line.Contains(new Vector2(7f,4f)), "Required route sections omitted.");
        });
        Check("SHARED-ROUTE-OVERLAP-REJECTS", () => { var prepared=Prepared(8); Install(prepared,Crossing(2f,7f),Crossing(6f,8f)); Reject(()=>Run(prepared,Line())); });
        Check("SHARED-ROUTE-MISSING-LAYOUT-REJECTS", () => Reject(()=>Run(Prepared(8),Line())));
        Check("SHARED-ROUTE-ARCHIVED-PROJECTION-PRESERVED", () => { var line=Line(); Run(Prepared(7),line); Require(line.Count==3 && line[1]==new Vector2(5f,0f), "Archived route changed."); });
        foreach (int version in new[] { 7, 8 })
            Check("ORIGIN-ROUTE-GATE-VERSION-" + version, () => {
                var prepared = Prepared(version);
                Set(prepared, "sites", new[] { new YQSpatialMaterializationSiteV2 { siteId = "site:origin", kind = YQSiteKindV2.Origin,
                    x = 0f, z = 0f, reservedRadius = 96f, headingDegrees = 0f } });
                Set(prepared, "siteBySiteId", new Dictionary<string,int> { ["site:origin"] = 0 });
                var method = typeof(YQGeneratedWorldEnvironment).GetMethod("ResolveV2RouteGate", BindingFlags.NonPublic | BindingFlags.Static);
                var point = (Vector2)method.Invoke(null, new object[] { prepared, "site:origin", new Vector2(0f,0f) });
                Require(point == (version == 8 ? new Vector2(0f,0f) : new Vector2(0f,114f)), "Origin endpoint/archived gate contract changed.");
            });
        return failures == 0 ? 0 : 1;
    }

    private static float MaximumTreadSlope(Mesh mesh)
    {
        // note: Measure actual upward collision triangles, including transverse faces; centreline checks alone missed the recorded failure.
        float maximum = 0f; Vector3[] vertices = mesh.vertices; int[] triangles = mesh.triangles;
        for(int index=0;index<triangles.Length;index+=3)
        {
            Vector3 normal=Vector3.Cross(vertices[triangles[index+1]]-vertices[triangles[index]],vertices[triangles[index+2]]-vertices[triangles[index]]);
            if(normal.y>0f) maximum=Mathf.Max(maximum,Mathf.Atan2(new Vector2(normal.x,normal.z).magnitude,normal.y)*Mathf.Rad2Deg);
        }
        return maximum;
    }
}
