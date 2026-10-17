using System;
using System.Collections;
using System.Collections.Generic;

// note: These stand-ins exercise iterator ownership only; they do not emulate prefab, collision or Unity activation.
public sealed class GameObject {
    public bool activeSelf=true; public Transform transform;
    public bool activeInHierarchy=>activeSelf&&(transform.parent==null||transform.parent.gameObject.activeInHierarchy);
}
public sealed class Transform {
    public readonly GameObject gameObject; public Transform parent;public string name="fixture";
    public Transform(Transform parent=null){this.parent=parent;gameObject=new GameObject{transform=this};}
    public bool IsChildOf(Transform root){for(var p=this;p!=null;p=p.parent)if(p==root)return true;return false;}
}
public sealed class GeneratedWorldPlanRecord {}
public sealed class WorldState {public GeneratedWorldPlanRecord generatedWorldPlan;}
public sealed class WorldStateManager {public static WorldStateManager Instance;public WorldState State=new WorldState();}
public sealed class YQProfileSaveSystem {public static YQProfileSaveSystem Instance=new();public string ActiveProfileId="fixture";}
public static class YQServiceLifecycle {public static int RequestEpoch;}
public static class Time {public static float realtimeSinceStartup;public static int frameCount;public static double realtimeSinceStartupAsDouble;}
public static class Debug {public static void LogError(string s){}public static void LogWarning(string s){}public static void LogException(Exception e){}}
public abstract class CustomYieldInstruction:IEnumerator {public object Current=>null;public bool MoveNext()=>false;public void Reset(){}}
public static class YourQuestTutorialAutoBootstrap {public static bool GameplayPresentationReleased=true;}
public static class YQGeneratedWorldPerformanceDirector {public static readonly object LoadingWorkBoundaryToken=new();public const float CooperativeWorkSliceSeconds=.002f;}
public sealed class YQStreamedFeatureOverlayTarget {public string featureId,objectId;}
public sealed class YQPlayerFollowingSemanticChunkStreamer {public void RegisterFeatureOverlayTarget(string id,GameObject obj,string objectId){}}
public partial class YQCompiledWorldSiteInstance {
    public static readonly Dictionary<string,YQCompiledWorldSiteInstance> Instances=new();
    public static YQCompiledWorldSiteInstance ActiveStreamLoader;
    public Transform transform;public GameObject gameObject=>transform.gameObject;
    public bool enabled=true,loaded,loading,unloading,loadRejected,interruptedStreamCleanupRequired;
    public bool IsLoaded=>loaded;
    public bool isActiveAndEnabled=>enabled&&gameObject.activeInHierarchy;
    public int streamExecutionEpoch,loads,quarantines,clones;
    public string settlementId="camp",expectedKitId="kit",semanticSliceSeed="seed";
    public string CanonicalRegionId="region";public object PreparedLayout;
    IEnumerator streamLoadExecution;object pendingSiteContent;
    public void SetPending(object value){pendingSiteContent=value;}
    private const float StreamSlotDiagnosticSeconds=12;
    private static T FindFirstObjectByType<T>() where T:class=>null;
    public T GetComponent<T>() where T:class=>null;
    void QuarantinePendingSiteContent(){quarantines++;}
    void ReleaseStreamLoadSlot(){if(ActiveStreamLoader==this)ActiveStreamLoader=null;}
    void ReportStreamExecutionFailure(Exception e){loadRejected=true;ReleaseStreamLoadSlot();}
    private IEnumerator LoadPreparedSiteGuardedRoutine()=>LoadPreparedSiteDetachedRoutine();
    private IEnumerator LoadPreparedSiteRoutine(){
        loads++;loading=true;
        yield return CloneOne();yield return CloneOne();loaded=true;loading=false;
    }
    IEnumerator CloneOne(){clones++;yield return null;}
}
public sealed class Settlement {public object proceduralLayout;}
public sealed class Anchor {public string sourceSemanticId="camp";}
public sealed class Location {public Anchor anchor=new();public Settlement settlement;public object compositionLayout=new();public string compositionSeed="seed";}
public struct YQSpatialMaterializationSiteV2 {public string parentRegionId;}
public partial class FeatureQualification {
    public static Location Accepted=new();
    private static bool TryResolveAcceptedContinuationSiteContext(GeneratedWorldPlanRecord plan,YQSpatialMaterializationSiteV2 site,out Location location,out object prepared,out string failure){location=Accepted;prepared=null;failure="";return true;}
    private static string ContinuationKitId(Location l)=>"kit";
}
public static class SiteStagingChecks {
    static int checks,failures;
    static void Check(bool result,string name){checks++;if(!result)failures++;Console.WriteLine((result?"PASS ":"FAIL ")+name);}
    static (Transform,YQCompiledWorldSiteInstance,GeneratedWorldPlanRecord) Fixture(){
        YQCompiledWorldSiteInstance.Instances.Clear();YQCompiledWorldSiteInstance.ActiveStreamLoader=null;
        YQServiceLifecycle.RequestEpoch=7;
        var plan=new GeneratedWorldPlanRecord();WorldStateManager.Instance=new(){State=new(){generatedWorldPlan=plan}};
        var root=new Transform();root.gameObject.activeSelf=false;
        var site=new YQCompiledWorldSiteInstance{transform=new Transform(root),streamExecutionEpoch=3};
        YQCompiledWorldSiteInstance.Instances["camp"]=site;return(root,site,plan);
    }
    static void Drain(IEnumerator it){try{while(it.MoveNext())if(it.Current is IEnumerator child)Drain(child);}finally{(it as IDisposable)?.Dispose();}}
    static IEnumerator Stage(string id,Transform root,GeneratedWorldPlanRecord plan,Action<bool> callback)=>
        YQCompiledWorldSiteInstance.EnsureSiteGeometryLoadedForStagingRoutine(id,root,plan,(ready,context)=>callback(ready));
    public static void Main(){
        var (root,site,plan)=Fixture();bool? result=null;
        Drain(YQCompiledWorldSiteInstance.EnsureSiteLoadedRoutine("camp",v=>result=v));
        Check(result==false&&site.loads==0,"original public load rejects a hidden registered owner");
        int callbacks=0;result=null;
        Drain(Stage("camp",root,plan,v=>{result=v;callbacks++;}));
        Check(result==true&&callbacks==1&&site.loads==1,"structural candidate loads its exact hidden descendant once");
        Check(!root.gameObject.activeSelf&&!site.isActiveAndEnabled,"structural loading never exposes hidden geometry");
        Check(!YQCompiledWorldSiteInstance.IsSiteLoaded("camp"),"public population readiness still rejects inactive geometry");
        Check(YQCompiledWorldSiteInstance.ActiveStreamLoader==null,"detached load releases its own loader slot");
        (root,site,plan)=Fixture();site.enabled=false;result=null;
        Drain(Stage("camp",root,plan,v=>result=v));
        Check(result==false&&site.loads==0,"disabled component rejects");
        (root,site,plan)=Fixture();site.gameObject.activeSelf=false;result=null;
        Drain(Stage("camp",root,plan,v=>result=v));
        Check(result==false&&site.loads==0,"inactive-self tombstone rejects");
        (root,site,plan)=Fixture();result=null;
        Drain(Stage("camp",new Transform(),plan,v=>result=v));
        Check(result==false&&site.loads==0,"unrelated hierarchy cannot borrow structural load");
        foreach(string mutation in new[]{"replacement","epoch","profile","request","activation"}){
            (root,site,plan)=Fixture();result=null;
            var other=new YQCompiledWorldSiteInstance{transform=new Transform()};YQCompiledWorldSiteInstance.ActiveStreamLoader=other;
            var it=Stage("camp",root,plan,v=>result=v);
            Check(it.MoveNext()&&it.Current==null&&result==null,"busy slot waits cooperatively "+mutation);
            if(mutation=="replacement")YQCompiledWorldSiteInstance.Instances["camp"]=other;
            if(mutation=="epoch")site.streamExecutionEpoch++;
            if(mutation=="profile")WorldStateManager.Instance.State.generatedWorldPlan=new();
            if(mutation=="request")YQServiceLifecycle.RequestEpoch++;
            if(mutation=="activation")root.gameObject.activeSelf=true;
            Check(!it.MoveNext()&&result==false&&site.loads==0&&YQCompiledWorldSiteInstance.ActiveStreamLoader==other,"stale request rejects without stealing slot "+mutation);
            (it as IDisposable)?.Dispose();
        }
        (root,site,plan)=Fixture();site.loadRejected=true;site.interruptedStreamCleanupRequired=true;result=null;
        Drain(Stage("camp",root,plan,v=>result=v));
        Check(result==false&&site.loads==0,"hidden interrupted rejection does not bypass retirement");
        (root,site,plan)=Fixture();root.gameObject.activeSelf=true;result=null;
        Drain(Stage("camp",root,plan,v=>result=v));
        Check(result==true&&site.loads==1,"active provider retains public load path");
        (root,site,plan)=Fixture();FeatureQualification.Accepted=new();site.PreparedLayout=FeatureQualification.Accepted.compositionLayout;
        YQCompiledWorldSiteInstance.StagedSiteGeometryContext receipt=null;
        Drain(YQCompiledWorldSiteInstance.EnsureSiteGeometryLoadedForStagingRoutine("camp",root,plan,(ready,context)=>{result=ready;receipt=context;}));
        var expectedSite=new YQSpatialMaterializationSiteV2{parentRegionId="region"};
        Check(result==true&&receipt!=null&&FeatureQualification.TryGetAcceptedContinuationLoadedProvider(plan,expectedSite,out var accepted,out _,receipt)&&accepted==site,"hidden structural load qualifies saved kit seed layout region end to end");
        Check(!FeatureQualification.TryGetAcceptedContinuationLoadedProvider(plan,expectedSite,out _,out _),"same hidden continuation remains rejected by public qualifier");
        foreach(string mismatch in new[]{"kit","seed","layout","region","pending","cleanup","reject","disabled","owner","epoch","profile"}){
            string kit=site.expectedKitId,seed=site.semanticSliceSeed,region=site.CanonicalRegionId;
            object layout=site.PreparedLayout;int epoch=site.streamExecutionEpoch;
            if(mismatch=="kit")site.expectedKitId="other";
            if(mismatch=="seed")site.semanticSliceSeed="other";
            if(mismatch=="layout")site.PreparedLayout=new();
            if(mismatch=="region")site.CanonicalRegionId="other";
            if(mismatch=="pending")site.SetPending(new());
            if(mismatch=="cleanup")site.interruptedStreamCleanupRequired=true;
            if(mismatch=="reject")site.loadRejected=true;
            if(mismatch=="disabled")site.enabled=false;
            if(mismatch=="owner")YQCompiledWorldSiteInstance.Instances["camp"]=new(){transform=new Transform()};
            if(mismatch=="epoch")site.streamExecutionEpoch++;
            if(mismatch=="profile")WorldStateManager.Instance.State.generatedWorldPlan=new();
            Check(!FeatureQualification.TryGetAcceptedContinuationLoadedProvider(plan,expectedSite,out _,out _,receipt),"structural qualifier rejects "+mismatch);
            site.expectedKitId=kit;site.semanticSliceSeed=seed;site.CanonicalRegionId=region;site.PreparedLayout=layout;
            site.SetPending(null);site.interruptedStreamCleanupRequired=false;site.loadRejected=false;site.enabled=true;site.streamExecutionEpoch=epoch;
            YQCompiledWorldSiteInstance.Instances["camp"]=site;WorldStateManager.Instance.State.generatedWorldPlan=plan;
        }
        foreach(string mutation in new[]{"profile","request","epoch","owner"}){
            (root,site,plan)=Fixture();result=null;
            var it=YQCompiledWorldSiteInstance.EnsureSiteGeometryLoadedForStagingRoutine("camp",root,plan,(ready,context)=>result=ready);
            Check(it.MoveNext()&&it.Current is IEnumerator,"load supplies owned detached iterator "+mutation);
            var child=(IEnumerator)it.Current;Check(child.MoveNext()&&child.Current is IEnumerator,"detached loader supplies existing owned runner "+mutation);
            var work=(IEnumerator)child.Current;Check(work.MoveNext()&&work.Current==null&&site.clones==1,"one clone yields before invalidation "+mutation);
            if(mutation=="profile")WorldStateManager.Instance.State.generatedWorldPlan=new();
            if(mutation=="request")YQServiceLifecycle.RequestEpoch++;
            if(mutation=="epoch")site.streamExecutionEpoch++;
            if(mutation=="owner")YQCompiledWorldSiteInstance.Instances["camp"]=new(){transform=new Transform()};
            Check(!work.MoveNext()&&site.clones==1,"nested guard stops further stale clone work "+mutation);
            Check(!child.MoveNext()&&!it.MoveNext()&&result==false,"stale child cannot publish receipt "+mutation);
            (work as IDisposable)?.Dispose();(child as IDisposable)?.Dispose();(it as IDisposable)?.Dispose();
            if(mutation!="epoch")Check(!site.loading&&site.loadRejected&&site.quarantines==1&&YQCompiledWorldSiteInstance.ActiveStreamLoader==null,"interrupted same lifetime quarantines and releases slot "+mutation);
        }
        Console.WriteLine($"RESULT checks={checks} failures={failures}");Environment.ExitCode=failures==0?0:1;
    }
}
