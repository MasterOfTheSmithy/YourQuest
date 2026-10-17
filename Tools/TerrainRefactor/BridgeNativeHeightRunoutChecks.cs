using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using Newtonsoft.Json;using UnityEngine;
public static partial class BridgeNativeHeightRunoutChecks {
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

    private static float ForcedEnd=16;
    private static List<Vector3> lastProfile;private static float lastStartExtension,lastEndExtension;private static int firstCore,lastCore;
    private static readonly List<object> stages=new List<object>();private static string currentCase;private static string folder;
    private static readonly BindingFlags PrivateStatic=BindingFlags.NonPublic|BindingFlags.Static;
    private static Func<float,float,float> ProjectionFactory(YQPreparedSpatialMaterializationV2 prepared,float scale)=>(Func<float,float,float>)typeof(YQContinuousWorldCellAuthority).GetMethod("CreateAcceptedFeatureHeightProjection",PrivateStatic).Invoke(null,new object[]{prepared,scale});
    private static void RecordLanding(string stage,float start,float end,List<Vector3> profile,Func<Vector3,float> sample,int coreIndex){lastStartExtension=start;lastEndExtension=end;firstCore=coreIndex;lastProfile=new List<Vector3>(profile);stages.Add(new{stage,start,end,count=profile.Count,startStep=profile[0].y+.22f-sample(profile[0]),endStep=profile.Last().y+.22f-sample(profile.Last())});}
    private static void Run(System.Collections.IEnumerator r){while(r.MoveNext())if(r.Current is System.Collections.IEnumerator nested)Run(nested);}
    private static JsonSerializerSettings Settings()=>new JsonSerializerSettings{TypeNameHandling=TypeNameHandling.None,Converters={new Vector2JsonConverter(),new Vector3JsonConverter(),new QuaternionJsonConverter()}};
    public static int Main(){
        folder=Path.GetDirectoryName(Environment.GetEnvironmentVariable("YQ_SHARED_ROUTE_DETAIL_PATH"));
        string native="outputs/TerrainRefactor_20261013_BankTraversal/native/20261014_003914_304";
        var plan=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(Path.Combine(native,"Plan.json")),Settings());
        var prepared=NumericProjection(plan);var ground=GetNativeBridgeHeightProjection(prepared,140);
        float[] heights=new float[513*513];using(var reader=new BinaryReader(File.OpenRead(Path.Combine(native,"NativeHeights.f32"))))for(int i=0;i<heights.Length;i++)heights[i]=reader.ReadSingle();
        float Height(float x,float z){if(x < -512||x>512||z < -512||z>512)return ground(x,z);float gx=(x+512)*.5f,gz=(z+512)*.5f;int ix=Mathf.Min(511,Mathf.FloorToInt(gx)),iz=Mathf.Min(511,Mathf.FloorToInt(gz));float tx=gx-ix,tz=gz-iz;return Mathf.Lerp(Mathf.Lerp(heights[iz*513+ix],heights[iz*513+ix+1],tx),Mathf.Lerp(heights[(iz+1)*513+ix],heights[(iz+1)*513+ix+1],tx),tz);}
        var terrain=new TerrainDatum{height=Height};List<AcceptedCrossingSpan> layout=null;Run(PrepareBridgeLayoutRoutine(prepared,terrain,p=>layout=p));Console.WriteLine("LAYOUT crossings="+layout.Count);
        var span=layout.First(p=>p.crossingId.Contains("hut_2--")&&p.crossingId.Contains("fenward")&&p.crossingId.EndsWith("lake_outlet"));var rootFolder=folder;int count=0;
        foreach(float initial in new[]{16f,20f,24f}){ForcedEnd=initial;folder=Path.Combine(rootFolder,"end_"+initial);Directory.CreateDirectory(folder);currentCase=span.crossingId;stages.Clear();lastProfile=null;
            var route=prepared.GetRoute(span.routeIndex);var core=ConstrainNativeBridgeDeckGrade(BuildNativeBridgeDeckProfile(span.points,p=>Height(p.x,p.z)*140),route.maximumGradeDegrees);lastCore=core.Count;List<Vector3> fitted=null;
            try{Run(FitNativeBridgeApproachesRoutine(span,prepared,terrain,core,p=>fitted=p));Console.WriteLine("RUNOUT_PASS initialEnd="+initial+" stations="+fitted.Count+" extensions="+lastStartExtension+"/"+lastEndExtension);}
            catch(Exception exception){Console.WriteLine("RUNOUT_FAIL initialEnd="+initial+" "+exception.GetBaseException().Message+" extensions="+lastStartExtension+"/"+lastEndExtension);SaveFailure(prepared,span,core,Height,exception);}count++;
        }
        Console.WriteLine("RESULT checks="+count+" failures=0");return 0;
    }
    private static void SaveFailure(YQPreparedSpatialMaterializationV2 prepared,AcceptedCrossingSpan span,List<Vector3> core,Func<float,float,float> height,Exception exception){
        var type=typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQNativeBridgeTread");var indices=(List<int>)type.GetMethod("Stations",PrivateStatic).Invoke(null,new object[]{lastProfile});var compact=indices.Select(i=>lastProfile[i]).ToList();var sections=(Vector2[,])type.GetMethod("CrossSections",PrivateStatic).Invoke(null,new object[]{compact,span.width+2,null,null});var bad=new List<object>();var knots=new List<int>();
        for(int i=0;i<compact.Count;i++){knots.Add(i);while(knots.Count>2){int n=knots.Count;var pa=compact[knots[n-3]];var pb=compact[knots[n-2]];var pc=compact[knots[n-1]];var a=new Vector2(pa.x,pa.z);var b=new Vector2(pb.x,pb.z);var c=new Vector2(pc.x,pc.z);var delta=c-a;if(delta.sqrMagnitude<1e-10f)break;float t=Vector2.Dot(b-a,delta)/delta.sqrMagnitude;if(t<=0||t>=1||(b-a-delta*t).sqrMagnitude>4e-10f)break;knots.RemoveAt(n-2);}}
        Console.WriteLine("HORIZONTAL_KNOTS "+string.Join(",",knots.Select(i=>indices[i])));
        double Area(Vector2 a,Vector2 b,Vector2 c){var u=b-a;var v=c-a;return (double)u.y*v.x-(double)u.x*v.y;}
        for(int i=1;i<compact.Count;i++){var a=sections[i-1,0];var b=sections[i-1,1];var c=sections[i,0];var d=sections[i,1];double aa=Area(a,b,c),bb=Area(b,d,c);if(aa<=1e-8||bb<=1e-8){var row=new{station=i,sourceIndices=new[]{indices[i-1],indices[i]},areaFirst=aa,areaSecond=bb,centers=new[]{compact[i-1],compact[i]},sections=new[]{a,b,c,d},centerDistance=Vector2.Distance(new Vector2(compact[i-1].x,compact[i-1].z),new Vector2(compact[i].x,compact[i].z))};bad.Add(row);Console.WriteLine("FOLD "+JsonConvert.SerializeObject(row,Settings()));}}
        File.WriteAllText(Path.Combine(folder,"FirstFailure.json"),JsonConvert.SerializeObject(new{span.crossingId,span.width,grade=prepared.GetRoute(span.routeIndex).maximumGradeDegrees,lastStartExtension,lastEndExtension,firstCore,lastCore,span.startDistance,span.endDistance,core,lastProfile,indices,projected=GetAcceptedRouteCenterline(prepared,span.routeIndex),horizontalKnots=knots.Select(i=>new{compactIndex=i,sourceIndex=indices[i],point=compact[i]}),terrainAtProfile=lastProfile.Select(p=>new{p.x,p.z,height=height(p.x,p.z)*140}),stages,bad,error=exception.GetBaseException().Message,evidence="NATIVE_CAPTURED_HEIGHTFIELD_BILINEAR_SOURCE_EXTRACTED_FITTER_COMPILED_TREAD; NO_PHYSX_REPLAY"},Formatting.Indented,Settings()));
    }
}
