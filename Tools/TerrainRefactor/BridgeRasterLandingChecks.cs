using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

// note: A detached verifier reconstructs only immutable query records; it does not certify full artifact admission or native terrain.
    public static partial class BridgeRasterLandingChecks
{
    private static void Set<T>(ref T value,string key,object field) where T:struct {
        object box=value; typeof(T).GetField(key,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(box,field); value=(T)box;
    }
    private static YQPreparedSpatialMaterializationV2 NumericProjection(GeneratedWorldPlanRecord plan) {
        var b=plan.spatialPlanV2.blueprint;
        object[] args={b,true,null,null};
        var prepare=typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("TryPrepareBlueprint",BindingFlags.NonPublic|BindingFlags.Static);
        if(!(bool)prepare.Invoke(null,args))throw new Exception("Numeric preparation: "+args[3]);
        var sampler=(YQSpatialBlueprintTerrainSamplerV2)args[2];
        var sites=b.sites.Select(s=>new YQSpatialMaterializationSiteV2 {
            siteId=s.siteId,sourceSemanticId=s.sourceSemanticId,parentRegionId=s.parentRegionId,kind=s.kind,
            x=s.x,z=s.z,headingDegrees=Mathf.Repeat(s.preferredHeadingDegrees,360),reservedRadius=s.reservedRadius
            ,surfaceElevationNormalized=sampler.Sample(s.x,s.z).elevationNormalized,
            terrainReserveReady=s.kind==YQSiteKindV2.NaturalFeature||!s.requiresTerrainConformance||sampler.Sample(s.x,s.z).siteReserveMask>=.9f
        }).ToArray();
        var routes=new List<YQSpatialMaterializationRouteV2>(); var points=new List<YQSpatialMaterializationRoutePointV2>();
        var crossings=new List<YQSpatialMaterializationCrossingV2>();
        foreach(var r in b.routes) {
            var value=new YQSpatialMaterializationRouteV2 {routeId=r.routeId,fromSiteId=r.fromSiteId,toSiteId=r.toSiteId,
                routeClass=r.routeClass,width=r.width,shoulderWidth=r.shoulderWidth,maximumGradeDegrees=r.maximumGradeDegrees};
            value.permittedBoundaryContinuation=true;
            Set(ref value,"pointStart",points.Count);Set(ref value,"pointCount",r.controlPoints.Count);
            Set(ref value,"hasSpatialBounds",r.controlPoints.Count>0);
            Set(ref value,"minimumX",r.controlPoints.Min(p=>p.x));Set(ref value,"maximumX",r.controlPoints.Max(p=>p.x));
            Set(ref value,"minimumZ",r.controlPoints.Min(p=>p.z));Set(ref value,"maximumZ",r.controlPoints.Max(p=>p.z));
            points.AddRange(r.controlPoints.Select(p=>new YQSpatialMaterializationRoutePointV2{x=p.x,z=p.z,width=Mathf.Max(r.width,p.width),surfaceElevationNormalized=sampler.Sample(p.x,p.z).elevationNormalized}));
            routes.Add(value);
            crossings.AddRange(r.crossings.Select(c=>new YQSpatialMaterializationCrossingV2{crossingId=c.crossingId,routeId=r.routeId,
                hydrologyId=c.hydrologyId,kind=c.kind,x=c.x,z=c.z,requiredSpan=c.requiredSpan}));
        }
        var waters=new YQSpatialMaterializationWaterV2[b.hydrology.Count]; var waterPoints=new List<YQSpatialMaterializationWaterPointV2>();
        var basin=typeof(YQSpatialBlueprintTerrainSamplerV2).GetMethod("GetMaterializedBasin",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
        for(int i=0;i<waters.Length;i++){
            var w=b.hydrology[i]; float width=Mathf.Max(w.kind==YQHydrologyKindV2.River?14f:w.kind==YQHydrologyKindV2.Waterfall?10f:1f,w.nominalWidth);
            var value=new YQSpatialMaterializationWaterV2{hydrologyId=w.hydrologyId,kind=w.kind,nominalWidth=width,
                nominalDepth=Mathf.Max(w.kind==YQHydrologyKindV2.River?4.5f:w.kind==YQHydrologyKindV2.Waterfall?3.5f:0,w.nominalDepth),
                waterLevelNormalized=w.waterLevelNormalized,sourceTerrainFieldId=w.sourceTerrainFieldId,sinkHydrologyId=w.sinkHydrologyId,parentRegionId=w.parentRegionId};
            var connected=(YQPreparedConnectedBasinV2)basin.Invoke(sampler,new object[]{i});
            Set(ref value,"connectedBasin",connected);
            Set(ref value,"pointStart",waterPoints.Count);Set(ref value,"pointCount",w.controlPoints.Count);
            Set(ref value,"hasSpatialBounds",w.controlPoints.Count>0);
            Set(ref value,"minimumX",connected!=null?connected.MinimumX:w.controlPoints.Min(p=>p.x));Set(ref value,"maximumX",connected!=null?connected.MaximumX:w.controlPoints.Max(p=>p.x));
            Set(ref value,"minimumZ",connected!=null?connected.MinimumZ:w.controlPoints.Min(p=>p.z));Set(ref value,"maximumZ",connected!=null?connected.MaximumZ:w.controlPoints.Max(p=>p.z));
            waterPoints.AddRange(w.controlPoints.Select(p=>new YQSpatialMaterializationWaterPointV2{x=p.x,z=p.z,width=Mathf.Max(width,p.width),waterSurfaceNormalized=Mathf.Clamp01(p.normalizedElevation)}));waters[i]=value;
        }
        var constructor=typeof(YQPreparedSpatialMaterializationV2).GetConstructors(BindingFlags.NonPublic|BindingFlags.Instance).Single();
        // note: Reconstruct only exact fields consumed by route/bank queries; this is not full accepted-artifact preparation or publication proof.
        return (YQPreparedSpatialMaterializationV2)constructor.Invoke(new object[]{Array.Empty<YQSpatialMaterializationRegionV2>(),sites,waters,waterPoints.ToArray(),
            routes.ToArray(),points.ToArray(),crossings.ToArray(),new Dictionary<string,int>(),
            sites.Select((s,i)=>(s,i)).Where(p=>!string.IsNullOrEmpty(p.s.sourceSemanticId)).ToDictionary(p=>p.s.sourceSemanticId,p=>p.i),
            sites.Select((s,i)=>(s,i)).ToDictionary(p=>p.s.siteId,p=>p.i),sampler,"",0L,null});
    }
    private static float lastStartExtension,lastEndExtension;
    private static int checks,failures;
    private static string currentCase;
    private static readonly List<object> stages=new List<object>();
    private static readonly BindingFlags PrivateStatic=BindingFlags.NonPublic|BindingFlags.Static;
    private static Func<float,float,float> ProjectionFactory(YQPreparedSpatialMaterializationV2 prepared,float scale)=>
        (Func<float,float,float>)typeof(YQContinuousWorldCellAuthority).GetMethod("CreateAcceptedFeatureHeightProjection",PrivateStatic).Invoke(null,new object[]{prepared,scale});
    private static void RecordLanding(string stage,float startExtension,float endExtension,List<Vector3> profile,Func<Vector3,float> sample,int firstCore) {
        float a=profile[0].y+.22f-sample(profile[0]),b=profile.Last().y+.22f-sample(profile.Last());
        lastStartExtension=startExtension;lastEndExtension=endExtension;
        stages.Add(new{caseId=currentCase,stage,startExtension,endExtension,startStep=a,endStep=b,firstCore});
    }

    private static void Run(System.Collections.IEnumerator r) {while(r.MoveNext())if(r.Current is System.Collections.IEnumerator nested)Run(nested);}
    private static void Check(string label,bool passed) {checks++;if(!passed)failures++;Console.WriteLine((passed?"PASS ":"FAIL ")+label);}
    private static JsonSerializerSettings Settings()=>new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,Converters={new Vector2JsonConverter(),new Vector3JsonConverter(),new QuaternionJsonConverter()}};
    private static string Describe(List<AcceptedCrossingSpan> spans)=>JsonConvert.SerializeObject(spans.Select(s=>new{s.crossingId,s.routeIndex,s.startDistance,s.endDistance,s.width,s.points,owner=s.bridge?.crossingId,s.bankConnections}),Settings());
    public static int Main() {
        var settings=Settings();var results=new List<object>();GeneratedWorldPlanRecord syntheticPlan=null;
        foreach(var fixture in new[]{new{name="UnityNewGame",file="outputs/TerrainRefactor_20261013_BankDiscovery/Unity_NewGame_Plan.json"},
            new{name="BankDiscovery",file="outputs/TerrainRefactor_20261013_BankDiscovery/Failing_Plan.json"},
            new{name="BridgeGeneralization",file="outputs/TerrainRefactor_20261013_BridgeGeneralization/Failing_Plan.json"},
            new{name="BridgeLanding",file="outputs/TerrainRefactor_20261013_BridgeLanding/Failing_Plan.json"},
            new{name="LoadFailure",file="outputs/TerrainRefactor_20261013_LoadFailure/Failing_Plan.json"}}) {
            var plan=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(fixture.file),settings);string document=JsonConvert.SerializeObject(plan,settings);
            if(syntheticPlan==null)syntheticPlan=plan;var expected=new Dictionary<string,string>();
            foreach(string lifecycle in new[]{"fresh","restored"}) {
                var ownerDocument=lifecycle=="fresh"?plan:JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(document,settings);
                var prepared=NumericProjection(ownerDocument);var ground=GetNativeBridgeHeightProjection(prepared,140);List<AcceptedCrossingSpan> spans=null;
                try {Run(PrepareBridgeLayoutRoutine(prepared,new TerrainDatum{height=ground},v=>spans=v));}
                catch(Exception e){Check(fixture.name+" "+lifecycle+" raster-safe layout",false);Console.WriteLine("LAYOUT_REJECTED "+fixture.name+" "+lifecycle+" "+e.GetBaseException().Message);continue;}
                Console.WriteLine("LAYOUT_ACCEPTED "+fixture.name+" "+lifecycle+" crossings="+spans.Count);
                if(fixture.name=="UnityNewGame"&&lifecycle=="fresh") {
                    bool Recut(Vector2 p){var sample=prepared.SampleTerrain(p.x,p.y);return sample.waterMask>=.9f&&sample.elevationNormalized<sample.waterSurfaceNormalized;}
                    Check("captured native failure footprint rejected",!RasterSupportSafe(new Vector2(17.67f,-9.87f),2.8f,Recut));
                    Check("outside authored grid unaffected",RasterSupportSafe(new Vector2(600,600),2.8f,p=>true));
                    Check("dry grid accepted",RasterSupportSafe(new Vector2(17.67f,-9.87f),2.8f,p=>false));
                    Check("submerged supporting corner rejected",!RasterSupportSafe(new Vector2(17.67f,-9.87f),2.8f,p=>p==new Vector2(16,-10)));
                }
                string before=Describe(spans);Check(fixture.name+" "+lifecycle+" all required crossing records retained",spans.Count==prepared.CrossingCount);
                for(int index=0;index<prepared.RouteCount;index++) {
                    var route=prepared.GetRoute(index);var points=GetAcceptedRouteCenterline(prepared,index);string reason=null;
                    try{ApplySharedBridgeRoute(prepared,index,points);}catch(Exception e){reason=e.GetBaseException().Message;}
                    Check(fixture.name+" "+lifecycle+" "+route.routeId+" composes",reason==null);
                    string path=JsonConvert.SerializeObject(points,settings);
                    if(lifecycle=="fresh")expected.Add(route.routeId,path);else Check(fixture.name+" "+route.routeId+" restored path identical",expected[route.routeId]==path);
                    if(fixture.name=="UnityNewGame"&&route.routeId.Contains("hut_2--")&&route.routeId.Contains("fenward")) {
                        var targets=spans.Where(s=>s.routeIndex==index&&!object.ReferenceEquals(s,s.bridge)).OrderBy(s=>s.startDistance).ToList();
                        Check("latest primary and continuation identities retained",targets.Count==2&&targets[0].bridge==targets[1].bridge);
                        var first=targets[0].bankConnections[0];var last=targets.OrderByDescending(s=>s.endDistance).First().bankConnections[1];
                        Check("merged latest path includes both outer connectors and common deck",first.Concat(targets[0].bridge.points).Concat(last).All(p=>points.Any(q=>(q-new Vector2(p.x,p.z)).sqrMagnitude<.000001f)));
                        Console.WriteLine("LATEST_ROUTE_RESULT success="+(reason==null)+" crossings="+spans.Count+" sharedLabels="+targets.Count+" points="+points.Count+" error="+reason);
                    }
                    results.Add(new{fixture=fixture.name,lifecycle,route.routeId,success=reason==null,error=reason,points=points.Count});
                }
                Check(fixture.name+" "+lifecycle+" derived crossing metadata unchanged",before==Describe(spans));
                Check(fixture.name+" "+lifecycle+" accepted document unchanged",document==JsonConvert.SerializeObject(ownerDocument,settings));
            }
            Console.WriteLine("PLAN_DONE "+fixture.name);
        }
        Synthetic(syntheticPlan);
        File.WriteAllText(Environment.GetEnvironmentVariable("YQ_SHARED_ROUTE_DETAIL_PATH"),JsonConvert.SerializeObject(new{results},Formatting.Indented));
        Console.WriteLine("RESULT checks="+checks+" failures="+failures);return failures==0?0:1;
    }
    private static void Synthetic(GeneratedWorldPlanRecord plan) {
        var prepared=NumericProjection(plan);var points=Enumerable.Range(0,11).Select(i=>new Vector2(i*10,0)).ToList();
        var owner=new AcceptedCrossingSpan{crossingId="synthetic:owner",points=new List<Vector3>{new Vector3(0,0,5),new Vector3(50,0,5),new Vector3(100,0,5)}};owner.bridge=owner;
        AcceptedCrossingSpan Span(string id,float start,float end,AcceptedCrossingSpan bridge) {
            var s=new AcceptedCrossingSpan{crossingId=id,routeIndex=0,startDistance=start,endDistance=end,width=5.2f,
                points=new List<Vector3>{new Vector3(start,0,0),new Vector3(end,0,0)},bridge=bridge};
            s.bankConnections.Add(new List<Vector3>{s.points[0],bridge.points[0]});s.bankConnections.Add(new List<Vector3>{s.points[1],bridge.points.Last()});return s;
        }
        void Case(string label,List<AcceptedCrossingSpan> spans,bool accepted,Vector2 entrance,Vector2 exit) {
            BridgeLayouts.Remove(prepared);BridgeLayouts.Add(prepared,new AcceptedBridgeLayout{ready=true,spans=spans});
            string before=Describe(spans);var line=new List<Vector2>(points);string error=null;
            try{ApplySharedBridgeRoute(prepared,0,line);}catch(Exception e){error=e.GetBaseException().Message;}
            Check(label+" acceptance",(error==null)==accepted);
            Check(label+" crossing records preserved",before==Describe(spans));
            if(accepted)Check(label+" outer contacts and shared deck retained",line.Contains(entrance)&&line.Contains(exit)&&owner.points.All(p=>line.Contains(new Vector2(p.x,p.z))));
        }
        Case("duplicate same owner",new List<AcceptedCrossingSpan>{Span("a",20,40,owner),Span("b",20,40,owner)},true,new Vector2(20,0),new Vector2(40,0));
        Case("contained same owner",new List<AcceptedCrossingSpan>{Span("a",20,60,owner),Span("b",30,40,owner)},true,new Vector2(20,0),new Vector2(60,0));
        Case("partial overlap same owner",new List<AcceptedCrossingSpan>{Span("a",20,50,owner),Span("b",40,70,owner)},true,new Vector2(20,0),new Vector2(70,0));
        Case("disjoint same owner",new List<AcceptedCrossingSpan>{Span("a",20,30,owner),Span("b",60,70,owner)},true,new Vector2(20,0),new Vector2(70,0));
        var other=new AcceptedCrossingSpan{crossingId="synthetic:other-owner",points=owner.points};other.bridge=other;
        Case("overlapping different owner",new List<AcceptedCrossingSpan>{Span("a",20,50,owner),Span("b",40,70,other)},false,default,default);
        var incomplete=Span("b",40,70,owner);incomplete.bankConnections.RemoveAt(1);
        Case("overlapping incomplete connectors",new List<AcceptedCrossingSpan>{Span("a",20,50,owner),incomplete},false,default,default);
        Case("reversed interval",new List<AcceptedCrossingSpan>{Span("a",50,20,owner)},false,default,default);
    }
}
