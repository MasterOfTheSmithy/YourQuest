using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Wander=YQGeneratedNpcWander;

// note: These scene/Physics stand-ins provide exact hit/owner witnesses to extracted production floor/wander decisions, not native collision or animation proof.
class GameObject
{
    public Transform transform;public bool activeSelf=true;public string name;
    public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
    public GameObject(string name){this.name=name;transform=new Transform(this);}
}
class Transform
{
    public GameObject gameObject;public Transform parent;public List<Transform> children=new();public Vector3 position;public Quaternion rotation=Quaternion.identity;
    public string name=>gameObject.name;
    public Transform(GameObject value){gameObject=value;}
    public void SetParent(Transform value){parent?.children.Remove(this);parent=value;value?.children.Add(this);}
    public Transform Find(string name)=>children.Find(x=>x.name==name);
    public bool IsChildOf(Transform owner){for(var current=this;current!=null;current=current.parent)if(current==owner)return true;return false;}
    public Vector3 TransformPoint(Vector3 local)=>position+local;
}
class EntityInfo {public GameObject gameObject;public Transform transform=>gameObject.transform;public string entityId;public bool enabled=true;public object wander;public T GetComponent<T>() where T:class=>wander as T;}
class Collider {public Transform transform;public bool boundary;public Bounds bounds;public Collider(Transform value){transform=value;}}
class TerrainCollider:Collider {public TerrainCollider(Transform value):base(value){}}
struct RaycastHit {public Collider collider;public Vector3 point,normal;}
enum QueryTriggerInteraction {Ignore}
static class Physics
{
    public static Func<Vector3,Vector3,RaycastHit[]> Supply;
    public static Collider[] Overlaps=Array.Empty<Collider>();
    public static int Raycasts;public static Vector3 LastOrigin;
    public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] result,float distance,int mask,QueryTriggerInteraction mode)
    {
        Raycasts++;LastOrigin=origin;var hits=Supply?.Invoke(origin,direction)??Array.Empty<RaycastHit>();int count=0;
        foreach(var hit in hits)if(direction.y<-.5f&&hit.point.y<=origin.y&&hit.point.y>=origin.y-distance||direction.y>=-.5f)
        {if(count<result.Length)result[count]=hit;count++;}
        return Math.Min(count,result.Length);
    }
    public static int OverlapCapsuleNonAlloc(Vector3 low,Vector3 high,float radius,Collider[] result,int mask,QueryTriggerInteraction mode)
    {for(int i=0;i<Math.Min(result.Length,Overlaps.Length);i++)result[i]=Overlaps[i];return Math.Min(result.Length,Overlaps.Length);}
}
static class YQPhysicalChunkAdmissionBoundary {public static bool IsOwned(Collider value)=>value!=null&&value.boundary;}
class WorldState {public GeneratedWorldPlanRecord generatedWorldPlan=new();}
class GeneratedWorldPlanRecord {}
class WorldStateManager {public static WorldStateManager Instance;public WorldState State=new();}
static class YQServiceLifecycle {public static int RequestEpoch;public static bool IsCurrent(int epoch)=>epoch==RequestEpoch;}
static class Time {public static float unscaledTime,deltaTime=.016f;}
static class YQGeneratedWorldTerrain
{
    public static bool TryGetActorContactGeometry(GameObject actor,out Bounds bounds,out float bottom){bounds=new Bounds(actor.transform.position,Vector3.one);bottom=actor.transform.position.y;return true;}
}
static class YQGeneratedWorldPopulation
{
    public static bool TryPlaceResidentOnReviewedSurface(GameObject actor,Vector3 floor){actor.transform.position=floor-Vector3.up*.005f;return true;}
}
partial class YQCompiledWorldSiteInstance
{
    static readonly Dictionary<string,YQCompiledWorldSiteInstance> Instances=new();
    readonly List<ResidentPositionBinding> residentPositionBindings=new();
    readonly Dictionary<Transform,Vector3> residentSurfaceDatums=new();
    static readonly RaycastHit[] ResidentSurfaceHitBuffer=new RaycastHit[64];
    static readonly Collider[] ResidentClearanceBuffer=new Collider[24];
    const float MinimumResidentSurfaceNormalY=.68f,ResidentStandingRadius=.30f;const int ResidentRelocationLimitPerFrame=2;
    string settlementId="site-a",expectedKitId="kit-a",semanticSliceSeed="seed-a";
    int streamExecutionEpoch=3;bool continuationPopulationInFlight=true;
    public GameObject gameObject;public Transform transform=>gameObject.transform;public bool IsLoaded,GeometryLoaded=true,Active=true;
    private sealed class ResidentPositionBinding {public EntityInfo entity;public string roleIntent,seed;public int index;}
    private struct ResidentPlacementDiagnostics {public int foreignHits,slopeHits,heightHits,roofHits,clearanceHits,saturatedQueries;}
    public YQCompiledWorldSiteInstance(){gameObject=new GameObject("provider");transform.position=new Vector3(0,57.09f,0);Instances[settlementId]=this;new GameObject("CompiledSiteContent").transform.SetParent(transform);}
    static bool TryGetContinuationGeometryContext(string id,string kit,string seed,out YQCompiledWorldSiteInstance provider)
    {if(Instances.TryGetValue(id,out provider)&&provider.GeometryLoaded&&provider.Active&&provider.expectedKitId==kit&&provider.semanticSliceSeed==seed)return true;provider=null;return false;}
@@CONTEXT@@
@@FLOOR@@
    public void Bind(EntityInfo entity)=>residentPositionBindings.Add(new ResidentPositionBinding{entity=entity});
    public void RemoveBinding()=>residentPositionBindings.Clear();
    public void ChangeEpoch()=>streamExecutionEpoch++;
    public void ReplaceContent(){var old=transform.Find("CompiledSiteContent");transform.children.Remove(old);new GameObject("CompiledSiteContent").transform.SetParent(transform);}
    public bool ForeignIssuerRejected(EntityInfo entity)=>ResidentWanderSurfaceContext.Capture(this,entity,transform.Find("CompiledSiteContent"),new object())==null;
    public void RenewExisting(){var routine=RenewRegisteredResidentWanderSurfacesRoutine();while(routine.MoveNext()){} }
}
class YQGeneratedNpcWander
{
    readonly RaycastHit[] _groundHits=new RaycastHit[8],_obstacleHits=new RaycastHit[8];
    Vector3 _origin,_target;float _contactOffset,_radius=6f,_speed=.8f,_nextDecision;uint _state=83;bool _hasTarget,_requiresReviewedSurface;object _authoredGait;
    YQCompiledWorldSiteInstance.ResidentWanderSurfaceContext _reviewedSurface;
    const float DecisionInterval=.45f,ArrivalDistance=.35f,CollisionProbeHeight=.85f;
    public GameObject gameObject;public Transform transform=>gameObject.transform;public string name=>gameObject.name;public bool isActiveAndEnabled=>gameObject.activeInHierarchy;
    public YQGeneratedNpcWander(GameObject actor){gameObject=actor;_origin=actor.transform.position;}
@@WANDER@@
@@ORIGINAL@@
    private void UpdateAuthoredGait(){throw new Exception("unexpected authored path in non-DOT controls");}
    public bool Ground(Vector3 p,out Vector3 floor)=>TryGetGround(p,out floor);
    public bool Original(Vector3 p,out Vector3 floor)=>OriginalTryGetGround(p,out floor);
    public bool ChooseHome()=>TryChooseTarget();public Vector3 Target=>_target;
    public void StepTo(Vector3 target){_target=target;_hasTarget=true;_nextDecision=1000;UpdateCore();}
}
static class Program
{
    static int checks;
    static void Check(bool ok,string name){checks++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
    static RaycastHit Hit(Collider owner,float height,float normal=1)=>new RaycastHit{collider=owner,point=new Vector3(0,height,0),normal=new Vector3(0,normal,0)};
    static Wander Fresh(out YQCompiledWorldSiteInstance site,out EntityInfo actor,out Collider floor,out TerrainCollider terrain,bool published=true,bool retainedExternal=false)
    {
        WorldStateManager.Instance=new WorldStateManager();YQServiceLifecycle.RequestEpoch=19;Time.unscaledTime=0;Physics.Overlaps=Array.Empty<Collider>();
        site=new YQCompiledWorldSiteInstance();var content=site.transform.Find("CompiledSiteContent");var cell=new GameObject("reviewed_cell");cell.transform.SetParent(content);cell.transform.position=new Vector3(0,57.09f,0);
        var support=new GameObject("floor");support.transform.SetParent(cell.transform);floor=new Collider(support.transform);
        terrain=new TerrainCollider(new GameObject("terrain").transform);
        actor=new EntityInfo{entityId="npc-a",gameObject=new GameObject("actor")};actor.transform.position=new Vector3(0,58.21f,0);actor.transform.SetParent(retainedExternal?new GameObject("legacy population root").transform:content);actor.gameObject.activeSelf=false;site.Bind(actor);
        var f=floor;var t=terrain;Physics.Supply=(origin,direction)=>direction.y<-.5f?new[]{Hit(f,58.21f),Hit(t,57.09f)}:Array.Empty<RaycastHit>();
        var wander=new Wander(actor.gameObject);actor.wander=wander;wander.BindReviewedSurface("site-a",actor);site.IsLoaded=published;actor.gameObject.activeSelf=true;return wander;
    }
    static void Main()
    {
        var w=Fresh(out var site,out var actor,out var floor,out var terrain);Check(Math.Abs(actor.transform.position.y-58.205f)<.0001f,"binding establishes actual reviewed home contact while staged cast is inactive");
        Check(w.Original(new Vector3(0,57.09f,0),out var old)&&old.y==57.09f,"original floor witness admits terrain beneath1.12m building slab");
        Check(w.Ground(new Vector3(0,57.09f,0),out var ground)&&Math.Abs(ground.y-58.21f)<.0001f,"reviewed movement selects existing owned building floor rather than nearest terrain");
        var t=terrain;Physics.Supply=(o,d)=>d.y<-.5f?new[]{Hit(t,57.09f)}:Array.Empty<RaycastHit>();Check(!w.Ground(new Vector3(0,58.21f,0),out _),"missing owned floor never admits available terrain");
        var exteriorActor=new GameObject("exterior");exteriorActor.transform.position=new Vector3(0,57.09f,0);var exterior=new Wander(exteriorActor);Check(exterior.Ground(exteriorActor.transform.position,out ground)&&ground.y==57.09f,"unbound exterior movement retains existing terrain support");
        w=Fresh(out site,out actor,out floor,out terrain);var f=floor;Physics.Supply=(o,d)=>d.y<-.5f&&Math.Abs(o.x)<.0001f&&Math.Abs(o.z)<.0001f?new[]{Hit(f,58.21f)}:Array.Empty<RaycastHit>();Check(w.ChooseHome()&&Math.Abs(w.Target.x)<.0001f&&Math.Abs(w.Target.z)<.0001f,"all failed pacing candidates return only to validated owned home");
        var before=actor.transform.position;w.StepTo(before+Vector3.right);Check(actor.transform.position==before,"home return and ordinary steps cannot traverse unsupported gap");
        w=Fresh(out site,out actor,out floor,out terrain);f=floor;Physics.Supply=(o,d)=>d.y<-.5f?new[]{Hit(f,58.21f)}:Array.Empty<RaycastHit>();before=actor.transform.position;w.StepTo(before+Vector3.right);Check(actor.transform.position.x>before.x&&Math.Abs(actor.transform.position.y-58.21f)<.0001f,"valid step stays on provider floor through actual UpdateCore");
        site.Active=false;before=actor.transform.position;w.StepTo(before+Vector3.right);Check(actor.transform.position==before,"inactive provider stops movement without alternate ground");
        w=Fresh(out site,out actor,out floor,out terrain);site.GeometryLoaded=false;Check(!w.Ground(actor.transform.position,out _),"unloaded geometry receipt rejects");
        w=Fresh(out site,out actor,out floor,out terrain,published:false);Check(!w.Ground(actor.transform.position,out _),"public movement cannot use still-unpublished required cast");
        w=Fresh(out site,out actor,out floor,out terrain);actor.enabled=false;Check(!w.Ground(actor.transform.position,out _),"disabled identity rejects");
        w=Fresh(out site,out actor,out floor,out terrain);actor.gameObject.activeSelf=false;Check(!w.Ground(actor.transform.position,out _),"inactive actor cannot wander");
        w=Fresh(out site,out actor,out floor,out terrain);actor.entityId="foreign";Check(!w.Ground(actor.transform.position,out _),"changed canonical actor identity rejects");
        w=Fresh(out site,out actor,out floor,out terrain);site.RemoveBinding();Check(!w.Ground(actor.transform.position,out _),"removed registered resident binding rejects");
        w=Fresh(out site,out actor,out floor,out terrain);site.ChangeEpoch();Check(!w.Ground(actor.transform.position,out _),"replacement geometry lifetime rejects");
        site.RenewExisting();Check(w.Ground(actor.transform.position,out _),"same published provider explicitly renews retained reviewed actor after epoch change");
        w=Fresh(out site,out actor,out floor,out terrain,retainedExternal:true);site.ReplaceContent();Check(!w.Ground(actor.transform.position,out _),"replaced content root rejects");
        floor.transform.parent.SetParent(site.transform.Find("CompiledSiteContent"));site.RenewExisting();Check(w.Ground(actor.transform.position,out _),"retained exterior-root resident can renew only same actor/world against current content");
        w=Fresh(out site,out actor,out floor,out terrain);new YQCompiledWorldSiteInstance();Check(!w.Ground(actor.transform.position,out _),"same-ID replacement provider rejects old receipt");
        w=Fresh(out site,out actor,out floor,out terrain);actor.transform.SetParent(new GameObject("foreign parent").transform);Check(!w.Ground(actor.transform.position,out _),"reparented actor cannot reuse captured lifetime");
        w=Fresh(out site,out actor,out floor,out terrain);WorldStateManager.Instance=new WorldStateManager();Check(!w.Ground(actor.transform.position,out _),"replaced world document rejects");
        bool badRenew=false;try{site.RenewExisting();}catch(InvalidOperationException){badRenew=true;}Check(badRenew,"post-loaded renewal cannot transfer actors across world/profile replacement");
        w=Fresh(out site,out actor,out floor,out terrain);WorldStateManager.Instance.State.generatedWorldPlan=new GeneratedWorldPlanRecord();Check(!w.Ground(actor.transform.position,out _),"replaced canonical plan rejects");
        w=Fresh(out site,out actor,out floor,out terrain);YQServiceLifecycle.RequestEpoch++;Check(!w.Ground(actor.transform.position,out _),"profile request epoch change rejects");
        w=Fresh(out site,out actor,out floor,out terrain);Check(site.ForeignIssuerRejected(actor),"foreign caller cannot forge registered provider context");
        w=Fresh(out site,out actor,out floor,out terrain);f=floor;Physics.Supply=(o,d)=>new[]{Hit(f,58.21f,.5f)};Check(!w.Ground(actor.transform.position,out _),"canonical steep floor gate remains strict");
        w=Fresh(out site,out actor,out floor,out terrain);floor.transform.gameObject.name="roof";Check(!w.Ground(actor.transform.position,out _),"canonical roof gate remains strict");
        w=Fresh(out site,out actor,out floor,out terrain);f=floor;Physics.Supply=(o,d)=>new[]{Hit(f,61f)};Check(!w.Ground(actor.transform.position,out _),"canonical datum/candidate ceiling remains strict");
        w=Fresh(out site,out actor,out floor,out terrain);f=floor;Physics.Supply=(o,d)=>Enumerable.Repeat(Hit(f,58.21f),64).ToArray();Check(!w.Ground(actor.transform.position,out _),"saturated owned floor ray remains rejected");
        w=Fresh(out site,out actor,out floor,out terrain);var obstruction=new Collider(new GameObject("wall").transform){bounds=new Bounds(new Vector3(0,59,0),Vector3.one)};Physics.Overlaps=new[]{obstruction};Check(!w.Ground(actor.transform.position,out _),"standing-clearance obstacle remains rejected");
        w=Fresh(out site,out actor,out floor,out terrain);var self=new Collider(actor.transform){bounds=new Bounds(new Vector3(0,59,0),Vector3.one)};Physics.Overlaps=new[]{self};Check(w.Ground(actor.transform.position,out _),"owned selector excludes exactly this actor from standing clearance");
        w=Fresh(out site,out actor,out floor,out terrain);Physics.Overlaps=Enumerable.Repeat(new Collider(new GameObject("crowd").transform),24).ToArray();Check(!w.Ground(actor.transform.position,out _),"saturated standing-clearance query remains rejected");
        w=Fresh(out site,out actor,out floor,out terrain);floor.boundary=true;Check(!w.Ground(actor.transform.position,out _),"new root physics admission boundary exclusion preserved in owned floor query");
        Physics.Overlaps=Array.Empty<Collider>();var boundary=new TerrainCollider(new GameObject("boundary").transform){boundary=true};Physics.Supply=(o,d)=>new[]{Hit(boundary,57.09f)};Check(!exterior.Ground(exteriorActor.transform.position,out _),"root boundary exclusion preserved for original exterior ground path");
        w=Fresh(out site,out actor,out floor,out terrain);bool refused=false;try{w.BindReviewedSurface("wrong-site",actor);}catch(InvalidOperationException){refused=true;}Check(refused&&!w.Ground(actor.transform.position,out _),"failed reviewed binding stays required and never degrades to terrain fallback");
        Console.WriteLine("RESULT checks="+checks+" failures=0");
    }
}
