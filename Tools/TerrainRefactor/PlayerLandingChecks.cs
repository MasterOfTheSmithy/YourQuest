using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// note: Native solver behavior is deliberately substituted. These checks exercise exact source handoff and lifecycle orchestration only.
namespace UnityEngine {
    [AttributeUsage(AttributeTargets.Class)] public class DisallowMultipleComponent:Attribute{}
    public struct Vector3 {
        public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}
        public static Vector3 zero=>new(0,0,0);public static Vector3 one=>new(1,1,1);public static Vector3 up=>new(0,1,0);public static Vector3 down=>new(0,-1,0);
        public float sqrMagnitude=>x*x+y*y+z*z;public float magnitude=>MathF.Sqrt(sqrMagnitude);
        public static Vector3 operator+(Vector3 a,Vector3 b)=>new(a.x+b.x,a.y+b.y,a.z+b.z);
        public static Vector3 operator-(Vector3 a,Vector3 b)=>new(a.x-b.x,a.y-b.y,a.z-b.z);
        public static Vector3 operator*(Vector3 a,float b)=>new(a.x*b,a.y*b,a.z*b);
        public static Vector3 ProjectOnPlane(Vector3 a,Vector3 n)=>a-n*(a.x*n.x+a.y*n.y+a.z*n.z);
        public override string ToString()=>$"({x},{y},{z})";
    }
    public struct Quaternion {public float x,y,z,w;public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Quaternion identity=>new(0,0,0,1);}
    public static class Mathf {public static float Max(float a,float b)=>MathF.Max(a,b);public static float Min(float a,float b)=>MathF.Min(a,b);public static float Abs(float a)=>MathF.Abs(a);public static float Clamp(float v,float a,float b)=>Math.Clamp(v,a,b);public static float Clamp01(float v)=>Math.Clamp(v,0,1);public static float Exp(float v)=>MathF.Exp(v);public static float InverseLerp(float a,float b,float v)=>a==b?0:Clamp01((v-a)/(b-a));}
    public class GameObject {
        public string name;public bool activeInHierarchy=true;public bool destroyed;
        public Transform transform;public readonly List<Component> components=new();
        public GameObject(string name){this.name=name;transform=new Transform(this);}
        public T Add<T>(T component) where T:Component {component.gameObject=this;components.Add(component);return component;}
        public T AddComponent<T>() where T:Component,new()=>Add(new T());
        public T GetComponent<T>() where T:class {foreach(var c in components)if(c is T t)return t;return null;}
        public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class {var list=new List<T>();Walk(transform,list);return list.ToArray();}
        static void Walk<T>(Transform t,List<T> list) {foreach(var c in t.gameObject.components)if(c is T typed)list.Add(typed);foreach(var child in t.children)Walk(child,list);}
    }
    public class Transform {
        public GameObject gameObject;public Transform parent;public readonly List<Transform> children=new();public Vector3 localPosition,localScale=Vector3.one;public Quaternion localRotation=Quaternion.identity;
        public Transform(GameObject go){gameObject=go;}public string name=>gameObject.name;
        public Vector3 position {get=>parent==null?localPosition:parent.position+localPosition;set=>localPosition=parent==null?value:value-parent.position;}
        public Quaternion rotation{get=>localRotation;set=>localRotation=value;}
        public bool IsChildOf(Transform root){for(var t=this;t!=null;t=t.parent)if(t==root)return true;return false;}
        public void SetParent(Transform next,bool worldPositionStays){var world=position;parent?.children.Remove(this);parent=next;parent?.children.Add(this);if(worldPositionStays)position=world;}
        public int GetSiblingIndex()=>parent==null?0:parent.children.IndexOf(this);public void SetSiblingIndex(int i){if(parent==null)return;parent.children.Remove(this);parent.children.Insert(Math.Clamp(i,0,parent.children.Count),this);}
        public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
        public T GetComponentInParent<T>() where T:class {for(var p=this;p!=null;p=p.parent){var found=p.GetComponent<T>();if(found!=null)return found;}return null;}
    }
    public class Component {public GameObject gameObject;public Transform transform=>gameObject.transform;public string name=>gameObject.name;public T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();public T GetComponentInParent<T>() where T:class=>transform.GetComponentInParent<T>();public T[] GetComponentsInChildren<T>(bool inactive) where T:class=>gameObject.GetComponentsInChildren<T>(inactive);}
    public class MonoBehaviour:Component{public static void Destroy(GameObject go){go.destroyed=true;}}
    public class Collider:Component {public bool enabled=true;}
    public class CharacterController:Collider {public bool detectCollisions=true;public float height=1.8f,radius=.38f,skinWidth=.08f;public Vector3 center=new(0,.9f,0);}
    public class Animator:Component{public bool enabled=true;public int rebinds;public void Rebind(){rebinds++;}public void Update(float dt){}}
    public class Rigidbody:Component {
        Vector3 nativePosition;Quaternion nativeRotation;public bool isKinematic=true,detectCollisions=true;public Vector3 linearVelocity,angularVelocity;public int activePoseWrites;
        public Vector3 position{get=>nativePosition;set{if(!isKinematic)activePoseWrites++;nativePosition=value;transform.position=value;}}
        public Quaternion rotation{get=>nativeRotation;set{if(!isKinematic)activePoseWrites++;nativeRotation=value;transform.rotation=value;}}
        public void NativeAdvance(Vector3 p){nativePosition=p;transform.position=p;}
        public void Synchronize(){if(isKinematic){nativePosition=transform.position;nativeRotation=transform.rotation;}}
    }
    public struct RaycastHit{public Collider collider;public Transform transform=>collider.transform;public Vector3 normal,point;public float distance;}
    public struct ContactPoint{public Vector3 normal,point;}
    public class Collision{public Collider collider;public Transform transform=>collider.transform;public Vector3 relativeVelocity;public ContactPoint[] contacts;public int contactCount=>contacts.Length;public ContactPoint GetContact(int i)=>contacts[i];}
    public enum QueryTriggerInteraction{Ignore}
    public static class Physics{
        public static int syncs;public static readonly List<Rigidbody> all=new();public static readonly List<(Collider,Collider)> ignored=new();public static Collider support;
        public static void SyncTransforms(){syncs++;foreach(var body in all)body.Synchronize();}
        public static void IgnoreCollision(Collider a,Collider b,bool ignoredValue){if(ignoredValue)ignored.Add((a,b));}
        public static int RaycastNonAlloc(Vector3 origin,Vector3 direction,RaycastHit[] hits,float distance,int mask,QueryTriggerInteraction trigger){if(support==null)return 0;hits[0]=new(){collider=support,normal=Vector3.up,point=new(origin.x,0,origin.z),distance=2};return 1;}
        public static int OverlapCapsuleNonAlloc(Vector3 a,Vector3 b,float r,Collider[] hits,int mask,QueryTriggerInteraction trigger)=>0;
    }
    public static class Time{public static float time;public static float fixedDeltaTime=.02f;}
    public static class Debug{public static readonly List<string> errors=new();public static void LogError(string error){errors.Add(error);}}
}
public class YQVitals {public bool IsDead;}
public partial class YQInvestorPlayerMotor:MonoBehaviour{
    public bool IsAuthoritative=true,HasPendingFallDamage;public YQVitals vitals=new();public int landingCount,resets,recoveries;public float observedHeight,observedSpeed;
    public void ObserveRagdollLanding(float h,float v){landingCount++;observedHeight=h;observedSpeed=v;}
    public void ResetAfterFallRecovery(){resets++;}public void ResetMotionSample(){}public void CompleteRagdollRecovery(bool clutch){recoveries++;}
}
public class YQInvestorCombat:MonoBehaviour {public int damageCalls;public GameObject lastSource;public void ReceiveDamage(float damage,GameObject source){damageCalls++;lastSource=source;}}
public class YQInvestorEnemy:MonoBehaviour {public int damageCalls;public void ReceiveHit(float damage,GameObject source){damageCalls++;}}
public class RuntimeModalUiBlocker{public static bool IsBlocked;}
public class YQGeneratedWorldRuntimeBuilder{public static bool IsInitialGenerationGameplayLocked;}
public class YQPlayerFollowingSemanticChunkStreamer{public static YQPlayerFollowingSemanticChunkStreamer Active;public bool isActiveAndEnabled;public bool TryConstrainMovement(Vector3 p,Vector3 d,float f,float s,out Vector3 permitted){permitted=d;return true;}public bool TryValidateCurrentTraversability(Vector3 p,float r,float s,out string failure){failure="";return true;}}

public static class PlayerLandingChecks {
    static int checks,failures;static void Require(bool value,string name){checks++;Console.WriteLine((value?"PASS ":"FAIL ")+name);if(!value)failures++;}
    static void Set(object owner,string field,object value)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(owner,value);
    static object Get(object owner,string field)=>owner.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(owner);
    static void Invoke(object owner,string method)=>owner.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(owner,null);
    static bool Same(Vector3 a,Vector3 b)=>(a-b).sqrMagnitude<1e-10f;
    sealed class Fixture{
        public GameObject player=new("canonical_player"),visual=new("visual_ancestor"),rig=new("existing_animator_root");public YQPlayerRagdoll rag;public YQInvestorPlayerMotor motor;public CharacterController capsule;public Animator animator;public Rigidbody[] bodies;public Collider[] shapes;
        public Fixture(){Physics.all.Clear();Physics.ignored.Clear();Physics.support=null;Time.time=0;RuntimeModalUiBlocker.IsBlocked=false;
            player.transform.position=new(20,10,30);visual.transform.SetParent(player.transform,false);rig.transform.SetParent(visual.transform,false);rig.transform.localPosition=new(0,.05f,0);rig.transform.localScale=new(1,1,1);
            motor=player.Add(new YQInvestorPlayerMotor());player.Add(new YQInvestorCombat());capsule=player.Add(new CharacterController());rag=player.Add(new YQPlayerRagdoll());animator=rig.Add(new Animator());rig.Add(new YQPlayerRagdollContact{owner=rag});bodies=new Rigidbody[2];shapes=new Collider[2];
            for(int i=0;i<2;i++){var bone=new GameObject("bone"+i);bone.transform.SetParent(rig.transform,false);bone.transform.localPosition=new(0,1+i*.5f,0);bodies[i]=bone.Add(new Rigidbody());shapes[i]=bone.Add(new Collider{enabled=false});bone.Add(new YQPlayerRagdollContact{owner=rag});Physics.all.Add(bodies[i]);}
            Set(rag,"motor",motor);Set(rag,"capsule",capsule);Set(rag,"animator",animator);Set(rag,"simulationRoot",rig.transform);Set(rag,"bodies",bodies);Set(rag,"shapes",shapes);Set(rag,"positions",new Vector3[2]);Set(rag,"rotations",new Quaternion[2]);
        }
    }
    public static int Main(){
        var originalFailed=new Fixture();originalFailed.rag.TestRigReady=false;Require(!originalFailed.rag.OriginalTryBegin(new(0,-2,0)),"preimage failed rig admission reproduced");originalFailed.rag.OriginalResetForRespawn();Require(!originalFailed.capsule.enabled,"preimage reset disables capsule without any successful handoff");
        var originalStale=new Fixture();originalStale.rag.OriginalTryBegin(new(0,-23,0));Require(!Same((Vector3)Get(originalStale.rag,"hipsOffset"),new(0,1.05f,0)),"preimage handoff reads stale native pose when synchronization is off");originalStale.rag.ResetForRespawn();
        var originalFollow=new Fixture();Physics.SyncTransforms();originalFollow.rag.OriginalTryBegin(new(0,-23,0));var nativeBefore=originalFollow.bodies[0].transform.position;originalFollow.player.transform.position+=new Vector3(0,-.1f,0);Require(!Same(originalFollow.bodies[0].transform.position,nativeBefore),"preimage dynamic body inherits capsule ancestor translation");originalFollow.bodies[0].NativeAdvance(new(20,10.8f,30));originalFollow.bodies[1].NativeAdvance(new(20,11.3f,30));Invoke(originalFollow.rag,"OriginalFixedUpdate");Require(originalFollow.bodies[0].activePoseWrites>0&&originalFollow.bodies[1].activePoseWrites>0,"preimage admitted root follow rewrites active native body poses");originalFollow.rag.ResetForRespawn();
        foreach(float speed in new[]{.1f,2f,12f,23f,60f,150f}){
            var f=new Fixture();var originalParent=f.rig.transform.parent;var originalLocal=f.rig.transform.localPosition;var originalWorld=f.rig.transform.position;int syncs=Physics.syncs;
            Require(f.rag.TryBegin(new(0,-speed,0)),"finite uncapped handoff speed="+speed);
            Require(f.rag.IsSimulating&&!f.capsule.enabled&&!f.animator.enabled&&f.rig.transform.parent==null,"exclusive native body ownership speed="+speed);
            Require(Physics.syncs>=syncs+2&&Same(f.rig.transform.position,originalWorld),"entry synchronization preserves world pose speed="+speed);
            Require(Same((Vector3)Get(f.rag,"hipsOffset"),new(0,1.05f,0)),"fresh animated hips offset speed="+speed);
            f.player.transform.position+=new Vector3(0,-.1f,0);f.visual.transform.localPosition=new(50,50,50);
            Require(Same(f.rig.transform.position,originalWorld),"player and visual ancestor cannot drag native body speed="+speed);
            f.bodies[0].NativeAdvance(new(20,10.8f,30));f.bodies[1].NativeAdvance(new(20,11.3f,30));
            Invoke(f.rag,"FixedUpdate");Require(f.bodies[0].activePoseWrites==0&&f.bodies[1].activePoseWrites==0,"normal admitted root follow never teleports active body speed="+speed);
            Require(Physics.ignored.Count==4,"self/equipment ignore pairs established speed="+speed);
            f.rag.ObserveContact(new Collision{collider=f.shapes[1],contacts=new[]{new ContactPoint{normal=Vector3.up}}});Require(f.motor.landingCount==0,"detached self contact excluded speed="+speed);
            var ground=new GameObject("published_ground").Add(new Collider());f.rag.ObserveContact(new Collision{collider=ground,relativeVelocity=new(0,speed,0),contacts=new[]{new ContactPoint{normal=Vector3.up,point=new(20,0,30)}}});
            Require(f.motor.landingCount==1&&f.motor.observedSpeed>=speed,"real external support forwards existing landing owner speed="+speed);
            f.rag.ResetForRespawn();Require(!f.rag.IsSimulating&&f.capsule.enabled&&f.animator.enabled&&f.rig.transform.parent==originalParent&&Same(f.rig.transform.localPosition,originalLocal),"kinematic teardown restores canonical attachment/capsule speed="+speed);
            Require(f.bodies[0].isKinematic&&!f.shapes[0].enabled&&f.bodies[1].isKinematic&&!f.shapes[1].enabled,"teardown ends skeletal collision ownership speed="+speed);
        }
        var failed=new Fixture();failed.rag.TestRigReady=false;Require(!failed.rag.TryBegin(new(0,-2,0)),"failed rig admission stays on capsule");failed.rag.ResetForRespawn();Require(failed.capsule.enabled,"failed rig reset cannot disable capsule with default captured flag");
        var invalid=new Fixture();Require(!invalid.rag.TryBegin(new(float.NaN,0,0))&&invalid.capsule.enabled,"nonfinite impulse rejected before handoff");
        var disabled=new Fixture();disabled.capsule.enabled=false;Require(!disabled.rag.TryBegin(new(0,-2,0)),"foreign disabled capsule admission rejected");disabled.rag.ResetForRespawn();Require(!disabled.capsule.enabled,"foreign disabled capsule remains owned by original caller");
        var paused=new Fixture();paused.rag.TryBegin(new(0,-23,0));RuntimeModalUiBlocker.IsBlocked=true;Invoke(paused.rag,"FixedUpdate");Require(paused.rag.IsSimulating&&paused.bodies[0].isKinematic&&!paused.capsule.enabled,"modal pause retains handoff and suspends bones");RuntimeModalUiBlocker.IsBlocked=false;Invoke(paused.rag,"FixedUpdate");Require(!paused.bodies[0].isKinematic,"unpause restores dynamic ownership");Invoke(paused.rag,"OnDisable");Require(paused.capsule.enabled&&!paused.rag.IsSimulating&&paused.rig.transform.parent==paused.visual.transform,"disable restores attachment and collision owner");Invoke(paused.rag,"OnDestroy");Require(paused.rig.transform.parent==paused.visual.transform,"destroy after disable is idempotent");
        var recovery=new Fixture();recovery.rag.TryBegin(new(0,-23,0));Physics.support=new GameObject("actual_support").Add(new Collider());Require(recovery.rag.TryRecover(false)&&recovery.motor.recoveries==1&&recovery.capsule.enabled,"existing physically supported get-up path preserved");
        Require(YQPlayerRagdoll.IsPhysicsStepSafe(new(0,100,0),new(0,99,0),new(0,98,0),8),"normal finite fall accepted");Require(!YQPlayerRagdoll.IsPhysicsStepSafe(Vector3.zero,new(0,-9,0),new(0,-10,0),8),"excessive step remains rejected");Require(!YQPlayerRagdoll.IsPhysicsStepSafe(Vector3.zero,new(float.PositiveInfinity,0,0),Vector3.zero,8),"nonfinite pose remains rejected");
        Require(YQPlayerMotionRules.FallDamage(1,2,3,22,12,35,100)==0,"unchanged motion rules keep ordinary low drop non-damaging");float ordinary=YQPlayerMotionRules.FallDamage(10,23,3,22,12,35,100);Require(ordinary>0&&ordinary<100,"unchanged damaging impact remains nonzero/nonfatal");Require(YQPlayerMotionRules.FallDamage(22,35,3,22,12,35,100)==200,"unchanged lethal fall uses existing damage rule");Require(YQPlayerMotionRules.FallDamage(30,2,3,22,12,35,100)==0,"gently caught fall does not become lethal from height alone");Require(YQPlayerMotionRules.ClutchDamage(ordinary,100,100,100,0)<=ordinary,"existing clutch can only reduce injury");
        var actor=new Fixture();Require(YQInvestorPlayerMotor.ResolveFromCollider(actor.capsule)==actor.motor,"ordinary capsule resolves through original motor ancestry");Require(YQInvestorPlayerMotor.ResolveFromTransform(actor.rig.transform)==actor.motor,"ordinary nested body resolves through original ancestry");
        Require(YQInvestorPlayerMotor.ResolveFromCollider(null)==null&&YQInvestorPlayerMotor.ResolveFromTransform(null)==null,"null actor queries reject");actor.rag.TryBegin(new(0,-23,0));
        Require(YQInvestorPlayerMotor.ResolveFromCollider(actor.shapes[0])==actor.motor,"active detached bone resolves single original motor");Require(YQInvestorPlayerMotor.ResolveFromTransform(actor.rig.transform)==actor.motor,"active detached root marker resolves original motor");var nonbone=new GameObject("nonbone_mesh");nonbone.transform.SetParent(actor.rig.transform,false);var nonboneShape=nonbone.Add(new Collider());Require(YQInvestorPlayerMotor.ResolveFromCollider(nonboneShape)==actor.motor,"nonbone mesh collider uses guarded root marker");
        var forged=new GameObject("unowned_forged_relay");forged.Add(new YQPlayerRagdollContact{owner=actor.rag});var forgedShape=forged.Add(new Collider());Require(YQInvestorPlayerMotor.ResolveFromCollider(forgedShape)==null,"forged relay outside exact detached rig rejected");
        var foreign=new Fixture();foreign.rig.GetComponent<YQPlayerRagdollContact>().owner=actor.rag;Require(YQInvestorPlayerMotor.ResolveFromCollider(foreign.shapes[0])==foreign.motor,"normal actor ancestry takes priority over forged relay");foreign.rig.transform.SetParent(null,true);foreign.shapes[0].GetComponent<YQPlayerRagdollContact>().owner=actor.rag;Require(YQInvestorPlayerMotor.ResolveFromCollider(foreign.shapes[0])==null,"foreign detached rig cannot claim active owner");
        var enemySource=new GameObject("enemy_effect_source");enemySource.Add(new YQInvestorEnemy());var effect=new VfxDamageProbe{source=enemySource,damage=5};var combat=actor.player.GetComponent<YQInvestorCombat>();Require(effect.TryApplyDamage(actor.shapes[0])&&combat.damageCalls==1&&combat.lastSource==enemySource,"accepted enemy effect invokes existing player combat for detached body");Require(!effect.TryApplyDamage(forgedShape)&&combat.damageCalls==1,"forged relay cannot deliver player damage");
        actor.motor.IsAuthoritative=false;Require(YQInvestorPlayerMotor.ResolveFromCollider(actor.shapes[0])==null,"retired motor authority invalidates detached relay");actor.motor.IsAuthoritative=true;
        var deadRelay=actor.shapes[0].GetComponent<YQPlayerRagdollContact>();deadRelay.owner=null;Require(YQInvestorPlayerMotor.ResolveFromCollider(actor.shapes[0])==null,"missing/dead nearest relay owner rejected");deadRelay.owner=actor.rag;
        actor.rag.ResetForRespawn();Require(YQInvestorPlayerMotor.ResolveFromCollider(actor.shapes[0])==actor.motor,"restored body again resolves normal ancestry");actor.rig.transform.SetParent(null,true);Require(YQInvestorPlayerMotor.ResolveFromCollider(actor.shapes[0])==null,"stopped detached relay cannot fabricate active actor");
        Require(actor.rig.GetComponent<YQPlayerRagdollContact>()!=null&&actor.shapes[0].GetComponent<YQPlayerRagdollContact>()!=null,"one existing contact relay per root/bone fixture");
        Console.WriteLine($"RESULT checks={checks} failures={failures}");return failures==0?0:1;
    }
}
