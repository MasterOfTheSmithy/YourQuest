using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

// note: A detached verifier reconstructs only immutable query records; it does not certify full artifact admission or native terrain.
    public static partial class BridgeApproachChecks
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
    private static int checks,failures;
    private static void Check(string name,bool passed) { checks++;if(!passed)failures++;Console.WriteLine((passed?"PASS ":"FAIL ")+name); }
    private static readonly BindingFlags PrivateStatic=BindingFlags.NonPublic|BindingFlags.Static;
    private static Func<float,float,float> ProjectionFactory(YQPreparedSpatialMaterializationV2 prepared,float scale) =>
        (Func<float,float,float>)typeof(YQContinuousWorldCellAuthority).GetMethod("CreateAcceptedFeatureHeightProjection",PrivateStatic)
            .Invoke(null,new object[]{prepared,scale});
    private static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b) {
        Vector2 d=b-a;return Vector2.Distance(p,a+d*Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.000001f,d.sqrMagnitude)));
    }
    private static float RawRouteDistance(YQPreparedSpatialMaterializationV2 prepared,int route,Vector2 point) {
        float result=float.PositiveInfinity;
        for(int i=1;i<prepared.GetRoutePointCount(route);i++) {
            var a=prepared.GetRoutePoint(route,i-1);var b=prepared.GetRoutePoint(route,i);
            result=Mathf.Min(result,SegmentDistance(point,new Vector2(a.x,a.z),new Vector2(b.x,b.z)));
        }
        return result;
    }
    private static void GateChecks(YQPreparedSpatialMaterializationV2 prepared,int routeIndex,List<Vector2> projection) {
        var route=prepared.GetRoute(routeIndex);
        foreach(bool atEnd in new[]{false,true}) {
            string siteId=atEnd?route.toSiteId:route.fromSiteId;
            var point=prepared.GetRoutePoint(routeIndex,atEnd?prepared.GetRoutePointCount(routeIndex)-1:0);
            Vector2 expected=ResolveV2RouteGate(prepared,siteId,new Vector2(point.x,point.z));
            Check("accepted "+(atEnd?"destination":"source")+" gate preserved",Vector2.Distance(expected,projection[atEnd?projection.Count-1:0])<.001f);
            TryResolveV2Site(prepared,siteId,out var site);
            float radius=site.reservedRadius;
            Vector2 center=new Vector2(site.x,site.z);
            bool outside=true;
            for(int i=1;i<projection.Count;i++) {
                if(Vector2.Distance(projection[i-1],center)>radius+80&&Vector2.Distance(projection[i],center)>radius+80)continue;
                outside &= SegmentDistance(center,projection[i-1],projection[i])>=radius-.001f;
            }
            Check("local "+(atEnd?"destination":"source")+" connector avoids site reserve",outside);
        }
        bool farRetained=true;
        foreach(var p in projection) {
            bool nearSite=false;
            foreach(string id in new[]{route.fromSiteId,route.toSiteId}) {
                TryResolveV2Site(prepared,id,out var site);
                nearSite|=Vector2.Distance(p,new Vector2(site.x,site.z))<=site.reservedRadius+41;
            }
            if(!nearSite)farRetained &= RawRouteDistance(prepared,routeIndex,p)<.002f;
        }
        Check("canonical corridor retained beyond local connectors",farRetained);
    }
    public static int Main()
    {
        try {
            var settings=new JsonSerializerSettings {TypeNameHandling=TypeNameHandling.None,
                Converters={new Vector2JsonConverter(),new Vector3JsonConverter(),new QuaternionJsonConverter()}};
            var plan=JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText("outputs/TerrainRefactor_20261013_LoadFailure/Failing_Plan.json"),settings);
            string before=JsonConvert.SerializeObject(plan,settings);
            var prepared=NumericProjection(plan);
            Check("native version eight fixture",UsesNativeBridgeRoutes(prepared));
            Check("accepted fixture content hash",YQSpatialBlueprintHasherV2.ComputeContentHash(plan.spatialPlanV2)=="f5b06e6a7049b895");
            int routeIndex=Enumerable.Range(0,prepared.RouteCount).First(i=>prepared.GetRoute(i).routeId.Contains("hut_2--site:settlement:settlement_mormere"));
            var route=prepared.GetRoute(routeIndex);
            var crossing=Enumerable.Range(0,prepared.CrossingCount).Select(prepared.GetCrossing)
                .First(c=>c.routeId==route.routeId&&c.hydrologyId=="water:river:lake_outlet");
            var projection=GetAcceptedRouteCenterline(prepared,routeIndex);
            GateChecks(prepared,routeIndex,projection);
            var ground=GetNativeBridgeHeightProjection(prepared,140);
            Check("same owner and scale reuse cached datum",ReferenceEquals(ground,GetNativeBridgeHeightProjection(prepared,140)));
            var cacheBefore=BridgeLayouts.GetValue(prepared,_=>new AcceptedBridgeLayout());
            Check("actual failing crossing builds",TryBuildCrossingSpan(prepared,crossing,new TerrainDatum(),out var span,out string reason));
            if(span==null)throw new Exception(reason);
            Console.WriteLine("SPAN start="+span.startDistance+" end="+span.endDistance+" points="+span.points.Count);
            Check("native crossing remains locally bounded",span.endDistance-span.startDistance<=1024.01f);
            Check("both full-width banks are dry",IsDryBridgeBank(prepared,new Vector2(span.points[0].x,span.points[0].z),route.width+2)&&
                IsDryBridgeBank(prepared,new Vector2(span.points.Last().x,span.points.Last().z),route.width+2));
            string cachePoints=JsonConvert.SerializeObject(span.points,settings);
            var deck=BuildNativeBridgeDeckProfile(span.points,p=>ground(p.x,p.z)*140);
            deck=ConstrainNativeBridgeDeckGrade(deck,route.maximumGradeDegrees);
            List<Vector3> fitted=null;
            var routine=FitNativeBridgeApproachesRoutine(span,prepared,new TerrainDatum(),deck,p=>fitted=p);
            while(routine.MoveNext()){}
            Check("actual failing crossing approach fitting completes",fitted!=null&&fitted.Count>=4);
            if(fitted==null)throw new Exception("No fitted profile returned.");
            float firstStep=fitted[0].y+.22f-ground(fitted[0].x,fitted[0].z)*140;
            float lastStep=fitted.Last().y+.22f-ground(fitted.Last().x,fitted.Last().z)*140;
            Console.WriteLine("FIT points="+fitted.Count+" startStep="+firstStep+" endStep="+lastStep);
            Check("both settled entrances respect 0.25 metre gate",firstStep<=.25001f&&lastStep<=.25001f&&firstStep>=-.001f&&lastStep>=-.001f);
            float maxGrade=0;
            for(int i=1;i<fitted.Count;i++) {
                float length=Vector2.Distance(new Vector2(fitted[i-1].x,fitted[i-1].z),new Vector2(fitted[i].x,fitted[i].z));
                if(length>.001f)maxGrade=Mathf.Max(maxGrade,Mathf.Atan(Mathf.Abs(fitted[i].y-fitted[i-1].y)/length)*Mathf.Rad2Deg);
            }
            Console.WriteLine("MAX_CENTRELINE_GRADE="+maxGrade);
            Check("fitted centreline respects accepted route grade",maxGrade<=route.maximumGradeDegrees+.03f);
            // note: Inspect the actual production tread sections without creating a native mesh or collider.
            var tread=typeof(YQPreparedSpatialMaterializationV2).Assembly.GetType("YQNativeBridgeTread");
            var stationIndices=(List<int>)tread.GetMethod("Stations",PrivateStatic).Invoke(null,new object[]{fitted});
            var compact=stationIndices.Select(i=>fitted[i]).ToList();
            var sections=(Vector2[,])tread.GetMethod("CrossSections",PrivateStatic).Invoke(null,new object[]{compact,route.width+2f,null,null});
            float maxTriangleGrade=0;
            void Triangle(Vector2 a,float ya,Vector2 b,float yb,Vector2 c,float yc) {
                Vector3 ab=new Vector3(b.x-a.x,yb-ya,b.y-a.y),ac=new Vector3(c.x-a.x,yc-ya,c.y-a.y);
                Vector3 normal=Vector3.Cross(ab,ac);
                if(normal.sqrMagnitude>.00000001f)maxTriangleGrade=Mathf.Max(maxTriangleGrade,
                    Mathf.Atan2(new Vector2(normal.x,normal.z).magnitude,Mathf.Abs(normal.y))*Mathf.Rad2Deg);
            }
            for(int i=1;i<compact.Count;i++) {
                Triangle(sections[i-1,0],compact[i-1].y,sections[i-1,1],compact[i-1].y,sections[i,0],compact[i].y);
                Triangle(sections[i-1,1],compact[i-1].y,sections[i,1],compact[i].y,sections[i,0],compact[i].y);
            }
            Console.WriteLine("MAX_TRIANGLE_GRADE="+maxTriangleGrade);
            Check("actual full-shoulder tread triangles respect accepted grade",maxTriangleGrade<=route.maximumGradeDegrees+.03f);
            // note: Independently compare finished terrain with the final sloped tread after both grade passes; an envelope calculation alone cannot certify its resulting clearance.
            float maximumPenetration=float.NegativeInfinity;int clearanceQueries=0;
            for(int i=1;i<fitted.Count;i++) {
                Vector3 delta=fitted[i]-fitted[i-1];Vector2 horizontal=new Vector2(delta.x,delta.z);
                if(horizontal.sqrMagnitude<.000001f)continue;
                Vector2 side=new Vector2(-horizontal.y,horizontal.x).normalized;
                int longitudinal=Mathf.Max(1,Mathf.CeilToInt(horizontal.magnitude*2));
                for(int along=0;along<=longitudinal;along++)for(int rail=0;rail<=16;rail++) {
                    float t=(float)along/longitudinal;Vector3 centre=Vector3.Lerp(fitted[i-1],fitted[i],t);
                    Vector2 p=new Vector2(centre.x,centre.z)+side*Mathf.Lerp(-(route.width+2)*.5f,(route.width+2)*.5f,rail/16f);
                    maximumPenetration=Mathf.Max(maximumPenetration,ground(p.x,p.y)*140-centre.y-.22f);clearanceQueries++;
                }
            }
            Console.WriteLine("FULL_FOOTPRINT queries="+clearanceQueries+" maximumPenetrationMetres="+maximumPenetration);
            Check("final full-width tread clears finished terrain",maximumPenetration<=.001f);
            Check("cache profile is immutable across fitting",cachePoints==JsonConvert.SerializeObject(span.points,settings));
            Check("accepted owner cache identity retained",ReferenceEquals(cacheBefore,BridgeLayouts.GetValue(prepared,_=>new AcceptedBridgeLayout())));
            Check("accepted plan not mutated",before==JsonConvert.SerializeObject(plan,settings));
            // note: A valid linear road must not inherit the higher endpoint's crest at its lower landing.
            float grade=Mathf.Tan(10*Mathf.Deg2Rad);Vector3 first=new Vector3(0,0,0),last=new Vector3(2,grade*2,0);
            float lift=MeasureNativeBridgeSegmentFloor(first,last,5.2f,p=>grade*p.x-Mathf.Lerp(first.y,last.y,Mathf.Clamp01(p.x/2)));
            Check("linear ten degree corridor requires no artificial lift",Mathf.Abs(lift)<.0001f);
            foreach(float invalidScale in new[]{0f,-1f,float.NaN,float.PositiveInfinity}) {
                bool rejected=false;try{ProjectionFactory(prepared,invalidScale);}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){rejected=true;}
                Check("factory rejects invalid terrain scale "+invalidScale,rejected);
            }
            bool nullRejected=false;try{ProjectionFactory(null,140);}catch(TargetInvocationException e)when(e.InnerException is ArgumentException){nullRejected=true;}
            Check("factory rejects missing prepared owner",nullRejected);
            var differentScale=GetNativeBridgeHeightProjection(prepared,280);
            Check("height scale changes invalidate only derived datum",!ReferenceEquals(ground,differentScale)&&ReferenceEquals(differentScale,GetNativeBridgeHeightProjection(prepared,280)));
            Check("existing delegate remains immutable after cache scale change",Mathf.Abs(ground(fitted[0].x,fitted[0].z)*140-(fitted[0].y+.22f-firstStep))<.0001f);
            GetNativeBridgeHeightProjection(prepared,140);
            var closureOwner=(YQContinuousWorldCellAuthority)ground.Target.GetType().GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)
                .Single(f=>f.FieldType==typeof(YQContinuousWorldCellAuthority)).GetValue(ground.Target);
            var reconcile=typeof(YQContinuousWorldCellAuthority).GetMethod("ApplyContinuationFeatureModifier",BindingFlags.Instance|BindingFlags.NonPublic);
            float largestBaseDifference=0;
            for(int i=0;i<fitted.Count;i++) {
                Vector3 tangent=i==0?fitted[1]-fitted[0]:fitted[i]-fitted[i-1];
                Vector2 side=new Vector2(-tangent.z,tangent.x).normalized;
                for(int across=-2;across<=2;across++) {
                    Vector2 p=new Vector2(fitted[i].x,fitted[i].z)+side*((route.width+2)*.25f*across);
                    float reference=ground(p.x,p.y);
                    foreach(float baseHeight in new[]{0f,.5f,1f}) {
                        float final=(float)reconcile.Invoke(closureOwner,new object[]{p.x,p.y,baseHeight});
                        largestBaseDifference=Mathf.Max(largestBaseDifference,Mathf.Abs(final-reference)*140);
                    }
                }
            }
            Console.WriteLine("MAX_BASE_INPUT_DIFFERENCE_METRES="+largestBaseDifference);
            Check("fitted footprint is independent of macro base input",largestBaseDifference<.001f);
            // note: Archive branch controls alter only detached metadata tags; they do not certify archived terrain geometry or materialization.
            var archived=NumericProjection(plan);
            for(int version=1;version<=7;version++) {
                for(int w=0;w<archived.WaterCount;w++)if(archived.GetWater(w).connectedBasin!=null)
                    typeof(YQPreparedConnectedBasinV2).GetField("domainVersion",BindingFlags.Instance|BindingFlags.NonPublic)
                        .SetValue(archived.GetWater(w).connectedBasin,version);
                var archiveProjection=GetAcceptedRouteCenterline(archived,routeIndex);
                var expected=new List<Vector2>();
                for(int i=0;i<archived.GetRoutePointCount(routeIndex);i++) {
                    var p=archived.GetRoutePoint(routeIndex,i);Vector2 v=new Vector2(p.x,p.z);
                    if(i==0)v=ResolveV2RouteGate(archived,route.fromSiteId,v);
                    else if(i==archived.GetRoutePointCount(routeIndex)-1)v=ResolveV2RouteGate(archived,route.toSiteId,v);
                    expected.Add(v);
                }
                Check("archived route projection branch preserved for version "+version,archiveProjection.SequenceEqual(expected));
            }
            var large=NumericProjection(plan);
            var detachedSites=(YQSpatialMaterializationSiteV2[])typeof(YQPreparedSpatialMaterializationV2)
                .GetField("sites",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(large);
            int sourceSite=Array.FindIndex(detachedSites,s=>s.siteId==route.fromSiteId);
            var bigSite=detachedSites[sourceSite];bigSite.x=0;bigSite.z=0;bigSite.headingDegrees=0;bigSite.reservedRadius=4000;
            detachedSites[sourceSite]=bigSite;
            var largeConnector=new List<Vector2>{Vector2.zero,new Vector2(-8000,0)};
            ApplyLocalV2RouteGate(large,route.fromSiteId,largeConnector,false,3.75f);
            float minimumClearance=float.PositiveInfinity;
            for(int i=1;i<largeConnector.Count;i++)minimumClearance=Mathf.Min(minimumClearance,
                SegmentDistance(Vector2.zero,largeConnector[i-1],largeConnector[i]));
            Console.WriteLine("LARGE_RESERVE_MIN_CENTRELINE_RADIUS="+minimumClearance);
            Check("four kilometre reserve connector preserves 7.5 metre corridor clearance",minimumClearance>=4003.749f);
            Check("large reserve actual gate is preserved",Vector2.Distance(largeConnector[0],new Vector2(0,4018))<.001f);
            var line=new[]{new Vector3(-5000,0,4400),new Vector3(-5000,0,5400)};var cumulative=new[]{0f,1000f};
            bool impossible=TryFindCrossingBank(prepared,line,cumulative,100,1,route.width+2,11.2f,32,512,out float lastRejected,
                (point,tangent)=>false);
            Check("native qualification cannot be bypassed after hard bound",!impossible&&lastRejected<=612.001f);
            // note: This actual accepted lake supplies a wet query witness; a short bounded search is intentionally contained within its interior.
            var lake=plan.spatialPlanV2.blueprint.hydrology.First(w=>w.hasConnectedDomain);
            float seedX=lake.connectedDomain.seedX,seedZ=lake.connectedDomain.seedZ;
            var wetPoints=new[]{new Vector3(seedX-1,0,seedZ),new Vector3(seedX+1,0,seedZ)};
            bool witnessWet=prepared.SampleTerrain(seedX,seedZ).waterMask>.05f;
            bool wetRejected=!TryFindCrossingBank(prepared,wetPoints,new[]{0f,2f},1,1,1,2,1,1,out _,(point,tangent)=>true);
            Check("real accepted wet footprint cannot qualify as a dry bank",witnessWet&&wetRejected);
        } catch(Exception error) { checks++;failures++;Console.WriteLine("FAIL exception "+error); }
        Console.WriteLine("RESULT checks="+checks+" failures="+failures);
        return failures==0?0:1;
    }
}
