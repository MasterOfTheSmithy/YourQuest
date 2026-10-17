using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

// note: A detached verifier reconstructs only immutable query records; it does not certify full artifact admission or native terrain.
    public static partial class BridgeLandingChecks
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
        Console.WriteLine("STAGE case="+currentCase+" startExtension="+startExtension+" endExtension="+endExtension+" stage="+stage+" startStep="+a+" endStep="+b+" firstCore="+firstCore);
    }
    private static void RecordDryFailure(float extension,int firstCore) {
        stages.Add(new{caseId=currentCase,stage="dry_rejected",extension,firstCore});
        Console.WriteLine("DRY_REJECTED case="+currentCase+" extension="+extension+" firstCore="+firstCore);
    }
    public static int Main()
    {
        var settings=new JsonSerializerSettings {TypeNameHandling=TypeNameHandling.None,
            Converters={new Vector2JsonConverter(),new Vector3JsonConverter(),new QuaternionJsonConverter()}};
        var results=new List<object>();
        foreach(var fixture in new[]{new{file="outputs/TerrainRefactor_20261013_BridgeLanding/Failing_Plan.json",name="current",route="route:primaryroad:site:origin--site:settlement:settlement_morvale_hold_1d641011",water="water:river:primary"},
            new{file="outputs/TerrainRefactor_20261013_LoadFailure/Failing_Plan.json",name="previous",route="route:secondaryroad:site:poi:curated_nordic_hut_2--site:settlement:settlement_mormere_fold_0faac520",water="water:river:lake_outlet"}}) {
            var plan=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(fixture.file),settings);
            string original=JsonConvert.SerializeObject(plan,settings);
            var prepared=NumericProjection(plan);var ground=GetNativeBridgeHeightProjection(prepared,140);
            int routeIndex=Enumerable.Range(0,prepared.RouteCount).First(i=>prepared.GetRoute(i).routeId==fixture.route);
            var route=prepared.GetRoute(routeIndex);
            var crossing=Enumerable.Range(0,prepared.CrossingCount).Select(prepared.GetCrossing).First(c=>c.routeId==fixture.route&&c.hydrologyId==fixture.water);
            var terrain=new TerrainDatum {height=ground};
            if(!TryBuildCrossingSpan(prepared,crossing,terrain,out var span,out string buildError))throw new Exception(buildError);
            Console.WriteLine("SPAN case="+fixture.name+" hash="+plan.spatialPlanV2.contentHash+" startDistance="+span.startDistance+" endDistance="+span.endDistance+
                " first="+JsonConvert.SerializeObject(span.points[0],settings)+" last="+JsonConvert.SerializeObject(span.points.Last(),settings)+" points="+span.points.Count);
            var deck=ConstrainNativeBridgeDeckGrade(BuildNativeBridgeDeckProfile(span.points,p=>ground(p.x,p.z)*140),route.maximumGradeDegrees);
            foreach(float maximum in new[]{64f}) {
                currentCase=fixture.name+"_independent"+maximum;
                List<Vector3> fitted=null;string failure=null;
                terrain.sampleCalls.Clear();
                try {var routine=FitNativeBridgeApproachesRoutine(span,prepared,terrain,deck,p=>fitted=p);while(routine.MoveNext()){};}
                catch(Exception e){failure=e.GetBaseException().ToString();}
                float first=fitted!=null?fitted[0].y+.22f-ground(fitted[0].x,fitted[0].z)*140:float.NaN;
                float last=fitted!=null?fitted.Last().y+.22f-ground(fitted.Last().x,fitted.Last().z)*140:float.NaN;
                results.Add(new{caseId=currentCase,success=fitted!=null,failure,points=fitted?.Count,startStep=first,endStep=last});
                Console.WriteLine("CASE_RESULT case="+currentCase+" success="+(fitted!=null)+" points="+fitted?.Count+" startStep="+first+" endStep="+last+" failure="+failure);
                Check(fixture.name+" actual final fitter completes",fitted!=null);
                if(fitted!=null) {
                    Check(fixture.name+" settled entrances",first>=-.001f&&last>=-.001f&&first<=.25001f&&last<=.25001f);
                    Check(fixture.name+" search remains bounded",lastStartExtension<=64&&lastEndExtension<=64);
                    Check(fixture.name+" exact native-coordinate cache samples each point once",terrain.sampleCalls.Values.All(c=>c==1));
                    float maximumGrade=0,maximumPenetration=float.NegativeInfinity;
                    for(int i=1;i<fitted.Count;i++) {
                        Vector3 delta=fitted[i]-fitted[i-1];Vector2 horizontal=new Vector2(delta.x,delta.z);
                        if(horizontal.sqrMagnitude<.000001f)continue;
                        maximumGrade=Mathf.Max(maximumGrade,Mathf.Atan(Mathf.Abs(delta.y)/horizontal.magnitude)*Mathf.Rad2Deg);
                        Vector2 side=new Vector2(-horizontal.y,horizontal.x).normalized;
                        int longitudinal=Mathf.Max(1,Mathf.CeilToInt(horizontal.magnitude*2));
                        for(int along=0;along<=longitudinal;along++)for(int rail=0;rail<=16;rail++) {
                            float t=(float)along/longitudinal;Vector3 centre=Vector3.Lerp(fitted[i-1],fitted[i],t);
                            Vector2 p=new Vector2(centre.x,centre.z)+side*Mathf.Lerp(-(route.width+2)*.5f,(route.width+2)*.5f,rail/16f);
                            maximumPenetration=Mathf.Max(maximumPenetration,ground(p.x,p.y)*140-centre.y-.22f);
                        }
                    }
                    Check(fixture.name+" accepted centreline grade",maximumGrade<=route.maximumGradeDegrees+.03f);
                    Check(fixture.name+" full shoulder clears actual finite feature datum",maximumPenetration<=.001f);
                    var tread=typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQNativeBridgeTread");
                    var stationIndices=(List<int>)tread.GetMethod("Stations",PrivateStatic).Invoke(null,new object[]{fitted});
                    var compact=stationIndices.Select(i=>fitted[i]).ToList();
                    var sections=(Vector2[,])tread.GetMethod("CrossSections",PrivateStatic).Invoke(null,new object[]{compact,route.width+2f,null,null});
                    float triangleGrade=0;
                    void Triangle(Vector2 a,float ya,Vector2 b,float yb,Vector2 c,float yc) {
                        Vector3 normal=Vector3.Cross(new Vector3(b.x-a.x,yb-ya,b.y-a.y),new Vector3(c.x-a.x,yc-ya,c.y-a.y));
                        if(normal.sqrMagnitude>.00000001f)triangleGrade=Mathf.Max(triangleGrade,
                            Mathf.Atan2(new Vector2(normal.x,normal.z).magnitude,Mathf.Abs(normal.y))*Mathf.Rad2Deg);
                    }
                    for(int i=1;i<compact.Count;i++) {
                        Triangle(sections[i-1,0],compact[i-1].y,sections[i-1,1],compact[i-1].y,sections[i,0],compact[i].y);
                        Triangle(sections[i-1,1],compact[i-1].y,sections[i,1],compact[i].y,sections[i,0],compact[i].y);
                    }
                    Check(fixture.name+" full shoulder triangle grade",triangleGrade<=route.maximumGradeDegrees+.03f);
                    Console.WriteLine("TRIANGLE_GATE case="+currentCase+" grade="+triangleGrade);
                    Console.WriteLine("FIT_GATE case="+currentCase+" grade="+maximumGrade+" penetration="+maximumPenetration+" uniqueNativeSamples="+terrain.sampleCalls.Count);
                }
            }
            if(fixture.name=="current") {
                var tooHigh=deck.Select(p=>new Vector3(p.x,1000,p.z)).ToList();
                currentCase="unsupported_high_core";bool accepted=false;string reason=null;int yields=0;
                try {var r=FitNativeBridgeApproachesRoutine(span,prepared,terrain,tooHigh,p=>accepted=true);while(r.MoveNext())yields++;}
                catch(InvalidOperationException e){reason=e.Message;}
                Check("unsupported landing fails at finite original bound",!accepted&&reason!=null&&reason.Contains("startExtension=64.0")&&reason.Contains("endExtension=64.0")&&yields<=16);
                var projection=GetAcceptedRouteCenterline(prepared,routeIndex);
                var line=projection.Select(p=>new Vector3(p.x,0,p.y)).ToArray();var distance=new float[line.Length];
                for(int i=1;i<line.Length;i++)distance[i]=distance[i-1]+Vector2.Distance(projection[i-1],projection[i]);
                var wetCore=new[]{SampleCrossingRoute(line,distance,0),SampleCrossingRoute(line,distance,2)}.ToList();
                bool wetWitness=prepared.SampleTerrain(wetCore[0].x,wetCore[0].z).waterMask>.05f;
                var wetSpan=new AcceptedCrossingSpan{routeIndex=routeIndex,width=route.width,startDistance=0,endDistance=2,
                    crossingId="detached:unsupported-wet-landing",points=wetCore};
                currentCase="unsupported_wet_landing";accepted=false;reason=null;yields=0;
                try {var r=FitNativeBridgeApproachesRoutine(wetSpan,prepared,terrain,wetCore,p=>accepted=true);while(r.MoveNext())yields++;}
                catch(InvalidOperationException e){reason=e.Message;}
                Check("wet approach remains rejected within finite bound",wetWitness&&!accepted&&reason!=null&&reason.Contains("accepted dry corridor")&&yields<=16);
            }
            Check(fixture.name+" accepted plan unchanged",original==JsonConvert.SerializeObject(plan,settings));
        }
        File.WriteAllText(Environment.GetEnvironmentVariable("YQ_LANDING_DETAIL_PATH")??"outputs/TerrainRefactor_20261013_BridgeLanding/Landing_Final_Detail.json",JsonConvert.SerializeObject(new{results,stages},Formatting.Indented));
        Console.WriteLine("RESULT checks="+checks+" failures="+failures);
        return failures==0?0:1;
    }
    private static void Check(string label,bool passed) {checks++;if(!passed)failures++;Console.WriteLine((passed?"PASS ":"FAIL ")+label);}
}
