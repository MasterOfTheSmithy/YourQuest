using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

// note: A detached verifier reconstructs only immutable query records; it does not certify full artifact admission or native terrain.
    public static partial class BridgeGeneralizationChecks
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
    // note: Fresh and serialized/restored numeric owners must independently produce identical profiles for every required crossing.
    public static int Main()
    {
        var settings=new JsonSerializerSettings {TypeNameHandling=TypeNameHandling.None,
            Converters={new Vector2JsonConverter(),new Vector3JsonConverter(),new QuaternionJsonConverter()}};
        var results=new List<object>();
        NativeTreadControls();
        if(Environment.GetEnvironmentVariable("YQ_GENERALIZATION_GRID_ONLY")=="1") {
            GridChecks(settings,results);
            Console.WriteLine("RESULT checks="+checks+" failures="+failures);return failures==0?0:1;
        }
        foreach(string folder in new[]{"BridgeGeneralization","BridgeLanding","LoadFailure"}) {
            string file="outputs/TerrainRefactor_20261013_"+folder+"/Failing_Plan.json";
            var plan=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(file),settings);
            string original=JsonConvert.SerializeObject(plan,settings);
            var expected=new Dictionary<string,string>();
            Func<float,float,float> previousGround=null;
            foreach(string lifecycle in new[]{"fresh","restored"}) {
                var document=lifecycle=="fresh"?plan:JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(original,settings);
                var prepared=NumericProjection(document);var ground=GetNativeBridgeHeightProjection(prepared,140);
                Check(folder+" "+lifecycle+" cache stays bound to prepared owner",
                    object.ReferenceEquals(ground,GetNativeBridgeHeightProjection(prepared,140))&&!object.ReferenceEquals(ground,previousGround));
                previousGround=ground;
                var layoutTerrain=new TerrainDatum{height=ground};List<AcceptedCrossingSpan> layout=null;
                foreach(var record in Enumerable.Range(0,prepared.CrossingCount).Select(prepared.GetCrossing)) {
                    if(TryBuildCrossingSpan(prepared,record,layoutTerrain,out var candidate,out _))foreach(var point in new[]{candidate.points[0],candidate.points.Last()}) {
                        var p=new Vector2(point.x,point.z);
                        if(!IsDryBridgeBank(prepared,p,candidate.width))Console.WriteLine("BANK_WITNESS "+JsonConvert.SerializeObject(new {
                            folder,lifecycle,record.crossingId,point=p,width=candidate.width,
                            shoulderDry=IsDryBridgeBank(prepared,p,candidate.width+2),cards=Cards(prepared,p,candidate.width)},settings));
                    }
                }
                try {Run(PrepareBridgeLayoutRoutine(prepared,layoutTerrain,value=>layout=value));}
                catch(Exception e) {Console.WriteLine("LAYOUT_FAILURE "+folder+" "+lifecycle+" "+e.GetBaseException().Message);}
                Check(folder+" "+lifecycle+" actual production shared layout",layout!=null&&layout.Count==prepared.CrossingCount);
                if(layout==null)continue;
                for(int crossIndex=0;crossIndex<prepared.CrossingCount;crossIndex++) {
                    var crossing=prepared.GetCrossing(crossIndex);
                    if(crossing.kind!=YQRouteCrossingKindV2.Bridge&&crossing.kind!=YQRouteCrossingKindV2.Causeway&&crossing.kind!=YQRouteCrossingKindV2.Ford)continue;
                    currentCase=folder+"/"+lifecycle+"/"+crossing.crossingId;
                    int routeIndex=Enumerable.Range(0,prepared.RouteCount).First(i=>prepared.GetRoute(i).routeId==crossing.routeId);
                    var route=prepared.GetRoute(routeIndex);var terrain=new TerrainDatum {height=ground};
                    AcceptedCrossingSpan span=layout.First(s=>s.crossingId==crossing.crossingId);List<Vector3> fitted=null;string reason=null;
                    if(!object.ReferenceEquals(span.bridge,span)) {
                        Check(currentCase+" shared deck retained",layout.Contains(span.bridge)&&span.bankConnections.Count==2);
                        bool dry=true;
                        foreach(var connection in span.bankConnections)for(int i=1;i<connection.Count;i++) {
                            int length=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(new Vector2(connection[i-1].x,connection[i-1].z),new Vector2(connection[i].x,connection[i].z))));
                            for(int station=0;station<=length;station++) {
                                var point=Vector3.Lerp(connection[i-1],connection[i],(float)station/length);
                                bool stationDry=IsDryBridgeBank(prepared,new Vector2(point.x,point.z),span.width);
                                if(dry&&!stationDry)Console.WriteLine("PATH_WITNESS "+JsonConvert.SerializeObject(new{currentCase,point=new Vector2(point.x,point.z),span.width,cards=Cards(prepared,new Vector2(point.x,point.z),span.width)},settings));
                                dry &= stationDry;
                            }
                        }
                        Check(currentCase+" shared full width paths remain dry",dry);
                        string sharedProfile=span.bridge.crossingId+JsonConvert.SerializeObject(span.bankConnections,settings);
                        if(lifecycle=="fresh")expected.Add(crossing.crossingId,sharedProfile);
                        else Check(currentCase+" restored deterministic shared topology",expected.TryGetValue(crossing.crossingId,out string value)&&value==sharedProfile);
                        var sharedResult=new{caseId=currentCase,success=dry,sharedDeck=span.bridge.crossingId,paths=span.bankConnections.Count};
                        results.Add(sharedResult);Console.WriteLine("CROSSING_RESULT "+JsonConvert.SerializeObject(sharedResult));continue;
                    }
                    try {
                        var deck=ConstrainNativeBridgeDeckGrade(BuildNativeBridgeDeckProfile(span.points,p=>ground(p.x,p.z)*140),route.maximumGradeDegrees);
                        terrain.sampleCalls.Clear();
                        var routine=FitNativeBridgeApproachesRoutine(span,prepared,terrain,deck,p=>fitted=p);
                        while(routine.MoveNext()){}
                    }catch(Exception e){reason=e.GetBaseException().Message;}
                    float startStep=float.NaN,endStep=float.NaN,grade=0,triangleGrade=0,penetration=float.NegativeInfinity,miterPenetration=float.NegativeInfinity;
                    if(fitted!=null) {
                        startStep=fitted[0].y+.22f-ground(fitted[0].x,fitted[0].z)*140;
                        endStep=fitted.Last().y+.22f-ground(fitted.Last().x,fitted.Last().z)*140;
                        Check(currentCase+" entrance steps",startStep>=-.001f&&endStep>=-.001f&&startStep<=.25001f&&endStep<=.25001f);
                        var projected=GetAcceptedRouteCenterline(prepared,routeIndex);
                        float routeLength=0;for(int i=1;i<projected.Count;i++)routeLength+=Vector2.Distance(projected[i-1],projected[i]);
                        float endpointLimit=ResolveNativeBridgeEndpointApproachLimit(140,route.width,route.maximumGradeDegrees);
                        Check(currentCase+" accepted route extent",lastStartExtension<=Mathf.Max(0,span.startDistance+endpointLimit)+.001f&&lastEndExtension<=Mathf.Max(0,routeLength-span.endDistance+endpointLimit)+.001f);
                        Check(currentCase+" exact-coordinate sampler cache",terrain.sampleCalls.Values.All(c=>c==1));
                        for(int i=1;i<fitted.Count;i++) {
                            var d=fitted[i]-fitted[i-1];var h=new Vector2(d.x,d.z);if(h.sqrMagnitude<.000001f)continue;
                            grade=Mathf.Max(grade,Mathf.Atan(Mathf.Abs(d.y)/h.magnitude)*Mathf.Rad2Deg);
                            var side=new Vector2(-h.y,h.x).normalized;
                            int steps=Mathf.Max(1,Mathf.CeilToInt(h.magnitude*2));
                            for(int k=0;k<=steps;k++)for(int rail=0;rail<5;rail++) {
                                var centre=Vector3.Lerp(fitted[i-1],fitted[i],(float)k/steps);
                                var p=new Vector2(centre.x,centre.z)+side*((rail-2)*(route.width+2)*.25f);
                                penetration=Mathf.Max(penetration,ground(p.x,p.y)*140-centre.y-.22f);
                            }
                        }
                        var tread=typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQNativeBridgeTread");
                        var indices=(List<int>)tread.GetMethod("Stations",PrivateStatic).Invoke(null,new object[]{fitted});
                        var compact=indices.Select(i=>fitted[i]).ToList();
                        var sections=(Vector2[,])tread.GetMethod("CrossSections",PrivateStatic).Invoke(null,new object[]{compact,route.width+2f,null,null});
                        void Triangle(Vector2 a,float ya,Vector2 b,float yb,Vector2 c,float yc) {
                            var normal=Vector3.Cross(new Vector3(b.x-a.x,yb-ya,b.y-a.y),new Vector3(c.x-a.x,yc-ya,c.y-a.y));
                            if(normal.sqrMagnitude>.00000001f)triangleGrade=Mathf.Max(triangleGrade,
                                Mathf.Atan2(new Vector2(normal.x,normal.z).magnitude,Mathf.Abs(normal.y))*Mathf.Rad2Deg);
                        }
                        for(int i=1;i<compact.Count;i++) {
                            Triangle(sections[i-1,0],compact[i-1].y,sections[i-1,1],compact[i-1].y,sections[i,0],compact[i].y);
                            Triangle(sections[i-1,1],compact[i-1].y,sections[i,1],compact[i].y,sections[i,0],compact[i].y);
                        }
                        Check(currentCase+" centre and physical triangle grades",grade<=route.maximumGradeDegrees+.03f&&triangleGrade<=route.maximumGradeDegrees+.03f);
                        Check(currentCase+" full shoulder finite datum clearance",penetration<=.001f);
                        // note: Original two-metre height stations subdivide the same straight-leg miter field. Sample their top triangles directly to include the corner wedges excluded by rectangular rail checks.
                        var fullSections=(Vector2[,])tread.GetMethod("CrossSections",PrivateStatic).Invoke(null,new object[]{fitted,route.width+2f,null,null});
                        void Footprint(Vector2 a,float ya,Vector2 b,float yb,Vector2 c,float yc) {
                            int count=Mathf.Max(1,Mathf.CeilToInt(Mathf.Max(Vector2.Distance(a,b),Mathf.Max(Vector2.Distance(a,c),Vector2.Distance(b,c)))*2));
                            for(int u=0;u<=count;u++)for(int v=0;v<=count-u;v++) {
                                float ub=(float)u/count,vc=(float)v/count;
                                var position=a+(b-a)*ub+(c-a)*vc;
                                float height=ya+(yb-ya)*ub+(yc-ya)*vc+.22f;
                                miterPenetration=Mathf.Max(miterPenetration,ground(position.x+fitted[0].x,position.y+fitted[0].z)*140-height);
                            }
                        }
                        for(int i=1;i<fitted.Count;i++) {
                            Footprint(fullSections[i-1,0],fitted[i-1].y,fullSections[i-1,1],fitted[i-1].y,fullSections[i,0],fitted[i].y);
                            Footprint(fullSections[i-1,1],fitted[i-1].y,fullSections[i,1],fitted[i].y,fullSections[i,0],fitted[i].y);
                        }
                        Check(currentCase+" finite datum actual miter triangle footprint clears",miterPenetration<=.001f);
                        string profile=JsonConvert.SerializeObject(fitted,settings);
                        if(lifecycle=="fresh")expected.Add(crossing.crossingId,profile);
                        else Check(currentCase+" restored deterministic profile",expected.TryGetValue(crossing.crossingId,out string value)&&value==profile);
                    }
                    Check(currentCase+" required crossing completes",fitted!=null);
                    var result=new{caseId=currentCase,success=fitted!=null,failure=reason,points=fitted?.Count,
                        startExtension=lastStartExtension,endExtension=lastEndExtension,startStep,endStep,grade,triangleGrade,penetration,miterPenetration,
                        startDistance=span?.startDistance,endDistance=span?.endDistance};
                    results.Add(result);
                    Console.WriteLine("CROSSING_RESULT "+JsonConvert.SerializeObject(result));
                }
                Check(folder+" "+lifecycle+" accepted document unchanged",original==JsonConvert.SerializeObject(document,settings));
            }
            Console.WriteLine("PLAN_DONE "+folder);
        }
        GridChecks(settings,results);
        File.WriteAllText(Environment.GetEnvironmentVariable("YQ_GENERALIZATION_DETAIL_PATH")??"outputs/TerrainRefactor_20261013_BridgeGeneralization/Final_Matrix_Detail.json",JsonConvert.SerializeObject(new{results,stages},Formatting.Indented));
        Console.WriteLine("RESULT checks="+checks+" failures="+failures);
        return failures==0?0:1;
    }
    private static void Check(string label,bool passed) {checks++;if(!passed)failures++;Console.WriteLine((passed?"PASS ":"FAIL ")+label);}
    private static void Run(System.Collections.IEnumerator routine) {
        while(routine.MoveNext())if(routine.Current is System.Collections.IEnumerator nested)Run(nested);
    }
    private static object[] Cards(YQPreparedSpatialMaterializationV2 prepared,Vector2 p,float width) {
        float radius=Mathf.Max(.5f,width*.5f);
        return new[]{p,p+Vector2.right*radius,p-Vector2.right*radius,p+Vector2.up*radius,p-Vector2.up*radius}.Select(point=>{
            var sample=prepared.SampleTerrain(point.x,point.y);return (object)new{point,sample.waterMask,sample.waterFeatureIndex};}).ToArray();
    }
    // note: This explicitly labelled surrogate uses a lazily sampled 513 grid, 16-bit quantization and triangles; it is not Unity's authored raster or Terrain.SampleHeight.
    private static Func<float,float,float> GridSurrogate(Func<float,float,float> ground) {
        var nodes=new Dictionary<Vector2Int,float>();
        float Node(int x,int z) {
            var key=new Vector2Int(x,z);if(!nodes.TryGetValue(key,out float h)) {
                h=Mathf.Round(Mathf.Clamp01(ground(-512+2*x,-512+2*z))*65535)/65535;nodes.Add(key,h);
            }return h;
        }
        return (x,z)=>{
            if(x< -512||x>512||z< -512||z>512)return ground(x,z);
            float gx=(x+512)*.5f,gz=(z+512)*.5f;
            int a=Mathf.Min(511,Mathf.FloorToInt(gx)),b=Mathf.Min(511,Mathf.FloorToInt(gz));float u=gx-a,v=gz-b;
            float aa=Node(a,b),ab=Node(a+1,b),ba=Node(a,b+1),bb=Node(a+1,b+1);
            return u+v<=1?aa+(ab-aa)*u+(ba-aa)*v:bb+(ba-bb)*(1-u)+(ab-bb)*(1-v);
        };
    }
    private static void GridChecks(JsonSerializerSettings settings,List<object> results) {
        foreach(var fixture in new[]{new{folder="BridgeGeneralization",route="route:primaryroad:site:origin--site:settlement:settlement_narcairn_post_4dd8acaf",water="water:river:primary"},
            new{folder="BridgeLanding",route="route:primaryroad:site:origin--site:settlement:settlement_morvale_hold_1d641011",water="water:river:primary"},
            new{folder="LoadFailure",route="route:secondaryroad:site:poi:curated_nordic_hut_2--site:settlement:settlement_mormere_fold_0faac520",water="water:river:lake_outlet"}}) {
            string text=File.ReadAllText("outputs/TerrainRefactor_20261013_"+fixture.folder+"/Failing_Plan.json"),expected=null;
            foreach(string state in new[]{"fresh","restored"}) {
                var document=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(text,settings);string before=JsonConvert.SerializeObject(document,settings);
                var prepared=NumericProjection(document);var finished=GetNativeBridgeHeightProjection(prepared,140);var height=GridSurrogate(finished);
                var terrain=new TerrainDatum{height=height};
                var crossing=Enumerable.Range(0,prepared.CrossingCount).Select(prepared.GetCrossing).First(c=>c.routeId==fixture.route&&c.hydrologyId==fixture.water);
                currentCase=fixture.folder+"/grid_surrogate/"+state;
                List<Vector3> fitted=null;AcceptedCrossingSpan span=null;string reason=null;
                try {
                    if(!TryBuildCrossingSpan(prepared,crossing,terrain,out span,out reason))throw new Exception(reason);
                    var route=prepared.GetRoute(span.routeIndex);
                    var deck=ConstrainNativeBridgeDeckGrade(BuildNativeBridgeDeckProfile(span.points,p=>height(p.x,p.z)*140),route.maximumGradeDegrees);
                    Run(FitNativeBridgeApproachesRoutine(span,prepared,terrain,deck,value=>fitted=value));
                }catch(Exception e){reason=e.GetBaseException().Message;}
                Check(currentCase+" final fitter completes",fitted!=null);
                float first=float.NaN,last=float.NaN;
                if(fitted!=null) {
                    first=fitted[0].y+.22f-height(fitted[0].x,fitted[0].z)*140;last=fitted.Last().y+.22f-height(fitted.Last().x,fitted.Last().z)*140;
                    Check(currentCase+" .25m entrances",first>=-.001f&&last>=-.001f&&first<=.25001f&&last<=.25001f);
                    string profile=JsonConvert.SerializeObject(fitted,settings);
                    if(state=="fresh")expected=profile;else Check(currentCase+" restored profile identical",expected==profile);
                }
                Check(currentCase+" accepted plan untouched",before==JsonConvert.SerializeObject(document,settings));
                var result=new{caseId=currentCase,success=fitted!=null,failure=reason,points=fitted?.Count,startStep=first,endStep=last,startExtension=lastStartExtension,endExtension=lastEndExtension};
                results.Add(result);Console.WriteLine("GRID_RESULT "+JsonConvert.SerializeObject(result));
                if(fitted!=null&&fixture.folder=="BridgeGeneralization"&&state=="fresh")RejectUnsupported(prepared,terrain,span);
            }
        }
    }
    private static void RejectUnsupported(YQPreparedSpatialMaterializationV2 prepared,TerrainDatum terrain,AcceptedCrossingSpan acceptedSpan) {
        int routeIndex=acceptedSpan.routeIndex;
        var projection=GetAcceptedRouteCenterline(prepared,routeIndex);
        var points=projection.Select(p=>new Vector3(p.x,0,p.y)).ToArray();var distances=new float[points.Length];
        for(int i=1;i<points.Length;i++)distances[i]=distances[i-1]+Vector2.Distance(projection[i-1],projection[i]);
        var route=prepared.GetRoute(routeIndex);
        float endpointLimit=ResolveNativeBridgeEndpointApproachLimit(140,route.width,route.maximumGradeDegrees);
        var core=new[]{SampleCrossingRoute(points,distances,-endpointLimit),SampleCrossingRoute(points,distances,-endpointLimit+2)}.Select(p=>new Vector3(p.x,1000,p.z)).ToList();
        var unsupported=new AcceptedCrossingSpan{crossingId="detached:overraised-endpoint",routeIndex=routeIndex,
            startDistance=-endpointLimit,endDistance=-endpointLimit+2,width=route.width,points=core};
        bool accepted=false;string reason=null;int yielded=0;
        try {var routine=FitNativeBridgeApproachesRoutine(unsupported,prepared,terrain,core,value=>accepted=true);while(routine.MoveNext())yielded++;}
        catch(InvalidOperationException e){reason=e.Message;}
        Check("unsupported core fails at geometric endpoint bound",!accepted&&reason!=null&&reason.Contains("accepted approach extent exhausted")&&yielded<=2);
        Console.WriteLine("REJECTION_RESULT "+reason);
        float wetStation=float.NaN;
        for(float along=acceptedSpan.startDistance;along<=acceptedSpan.endDistance;along+=1) {
            var point=SampleCrossingRoute(points,distances,along);
            if(!IsDryBridgeBank(prepared,new Vector2(point.x,point.z),prepared.GetRoute(routeIndex).width+2)) {wetStation=along;break;}
        }
        bool witness=!float.IsNaN(wetStation);
        var wetCore=new[]{SampleCrossingRoute(points,distances,wetStation+4),SampleCrossingRoute(points,distances,wetStation+6)}.ToList();
        var wet=new AcceptedCrossingSpan{crossingId="detached:wet-approach",routeIndex=routeIndex,startDistance=wetStation+4,endDistance=wetStation+6,
            width=prepared.GetRoute(routeIndex).width,points=wetCore};
        accepted=false;reason=null;yielded=0;
        try {var routine=FitNativeBridgeApproachesRoutine(wet,prepared,terrain,wetCore,value=>accepted=true);while(routine.MoveNext())yielded++;}
        catch(InvalidOperationException e){reason=e.Message;}
        Check("genuine wet approach is rejected without growing through it",witness&&!accepted&&reason!=null&&reason.Contains("accepted dry corridor")&&yielded==0);
        Console.WriteLine("WET_REJECTION_RESULT "+reason);
    }
    private static void NativeTreadControls() {
        var tread=typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQNativeBridgeTread");
        var sectionsMethod=tread.GetMethod("CrossSections",PrivateStatic);
        var offsetMethod=tread.GetMethod("SectionOffset",PrivateStatic);
        Vector2[,] Sections(IReadOnlyList<Vector3> points,Vector2? first=null,Vector2? last=null)=>(Vector2[,])sectionsMethod.Invoke(null,new object[]{points,4.8f,first,last});
        var source=new List<Vector3>{new Vector3(-20,2,0),new Vector3(-1,3,0),new Vector3(0,4,0),new Vector3(0,3.9f,1),new Vector3(0,5,25),new Vector3(0,8,30)};
        foreach(int mirror in new[]{1,-1})foreach(bool reversed in new[]{false,true}) {
            var points=source.Select(p=>new Vector3(p.x*mirror,p.y,p.z)).ToList();if(reversed)points.Reverse();
            List<Vector3> fit=FitTread(points,2.8f,18f);var sections=Sections(fit);float grade=0;bool positive=true,fullWidth=true;
            double Area(Vector2 a,Vector2 b,Vector2 c) {var u=b-a;var v=c-a;return(double)u.y*v.x-(double)u.x*v.y;}
            void Triangle(Vector2 a,float ya,Vector2 b,float yb,Vector2 c,float yc) {
                var n=Vector3.Cross(new Vector3(b.x-a.x,yb-ya,b.y-a.y),new Vector3(c.x-a.x,yc-ya,c.y-a.y));
                grade=Mathf.Max(grade,Mathf.Atan2(new Vector2(n.x,n.z).magnitude,Mathf.Abs(n.y))*Mathf.Rad2Deg);
            }
            for(int i=1;i<fit.Count;i++) {
                positive &= Area(sections[i-1,0],sections[i-1,1],sections[i,0])>0&&Area(sections[i-1,1],sections[i,1],sections[i,0])>0;
                var delta=new Vector2(fit[i].x-fit[i-1].x,fit[i].z-fit[i-1].z).normalized;var normal=new Vector2(-delta.y,delta.x);
                fullWidth &= Mathf.Abs(Vector2.Dot((sections[i-1,1]-sections[i-1,0])*.5f,normal))>=2.3999f&&Mathf.Abs(Vector2.Dot((sections[i,1]-sections[i,0])*.5f,normal))>=2.3999f;
                Triangle(sections[i-1,0],fit[i-1].y,sections[i-1,1],fit[i-1].y,sections[i,0],fit[i].y);
                Triangle(sections[i-1,1],fit[i-1].y,sections[i,1],fit[i].y,sections[i,0],fit[i].y);
            }
            string label="height break near90 degree corner mirror="+mirror+" reverse="+reversed;
            Check(label+" height stations preserved",fit.Count==points.Count&&fit.Select((p,i)=>p.x==points[i].x&&p.z==points[i].z&&p.y>=points[i].y).All(p=>p));
            Check(label+" full shoulder positive triangles",positive&&fullWidth&&grade<=18.03f);
        }
        var road=Enumerable.Range(0,41).Select(i=>new Vector3(i-40,3+Mathf.Sin(i*.13f),0))
            .Concat(Enumerable.Range(1,40).Select(i=>new Vector3(0,3+Mathf.Sin((i+40)*.13f),i))).ToList();
        var whole=Sections(road);bool same=true;
        for(int start=0;start<road.Count-1;start+=23) {
            int end=Mathf.Min(road.Count-1,start+23);var subset=road.Skip(start).Take(end-start+1).ToList();
            var first=(Vector2)offsetMethod.Invoke(null,new object[]{road,start});var last=(Vector2)offsetMethod.Invoke(null,new object[]{road,end});
            var partial=Sections(subset,first,last);var displacement=new Vector2(road[start].x-road[0].x,road[start].z-road[0].z);
            for(int i=0;i<subset.Count;i++)for(int side=0;side<2;side++)same &= (partial[i,side]+displacement-whole[start+i,side]).sqrMagnitude<=.000001f;
        }
        Check("recursive24 station sections inherit same whole road footprint",same);
        bool rejected=false;try{FitTread(new[]{new Vector3(0,0,0),new Vector3(5,0,0),new Vector3(0,0,0)},2.8f,18f);}catch(TargetInvocationException e){rejected=e.GetBaseException() is InvalidOperationException;}
        Check("genuine reversing tread remains rejected",rejected);
        foreach(var input in new[]{new[]{float.NaN,2.8f,18f},new[]{140f,float.PositiveInfinity,18f},new[]{140f,2.8f,0f}}) {
            rejected=false;try{ResolveNativeBridgeEndpointApproachLimit(input[0],input[1],input[2]);}catch(InvalidOperationException){rejected=true;}
            Check("invalid finite approach extent rejected",rejected);
        }
    }
}
