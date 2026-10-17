using System;
using System.Collections;
using Newtonsoft.Json;

// note: Only the actual iterator and private world-copy helper are exercised; profile/disk/provider/install authorities are controlled substitutes.
readonly struct ProfilerMarker
{
    public ProfilerMarker(string name) { }
    public Scope Auto()=>new Scope();
    public readonly struct Scope:IDisposable {public void Dispose(){}}
}
class YQGeneratedWorldRuntimeBuilder {public class FrontierConstructionAcceptance {}}
class YQProfileSaveSystem
{
    public static YQProfileSaveSystem Instance;
    public bool isActiveAndEnabled=true,PrepareOK=true,PublishOK=true,Pointer;
    public int Prepares,Publishes;
    public PlayerState Player=new PlayerState();
    public WorldState ExpectedWorld,DetachedWorld;
    public WorldStateManager.PreparedContinuationBaseWorld LastBase;
    public string PlayerJson,WorldJson;
    public Action OnPrepare;
    public class PreparedContinuationPublication {}
    public bool TryPrepareContinuationPublication(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,
        WorldStateManager.PreparedContinuationBaseWorld baseWorld,out PreparedContinuationPublication publication,out string failure)
    {
        Prepares++;LastBase=baseWorld;publication=null;failure="prepare rejected";
        if(!PrepareOK||!ReferenceEquals(WorldStateManager.Instance.State,ExpectedWorld))return false;
        PlayerJson=JsonConvert.SerializeObject(Player,WorldStateManager.Settings);
        DetachedWorld=WorldStateManager.Instance.CopyFinal(ExpectedWorld,baseWorld,out WorldJson);
        OnPrepare?.Invoke();publication=new PreparedContinuationPublication();failure="";return true;
    }
    public bool TryPublish(PreparedContinuationPublication publication)
    {
        Publishes++;
        if(!PublishOK||!YQProfileCommitStore.JsonEqualsCaptured(Player,PlayerJson,WorldStateManager.Settings)||
           !YQProfileCommitStore.JsonEqualsCaptured(ExpectedWorld,WorldJson,WorldStateManager.Settings))return false;
        Pointer=true;return true;
    }
}
class CloneStreamer
{
    WorldState _world;
    YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance _frontierConstructionAccepted=new YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance();
    int epoch;
    public bool Current=true,RefreshOK=true,InstallOK=true;
    public int Refreshes,Installs,Completions;
    public bool CompletedSuccess;
    public CloneStreamer(WorldState world){_world=world;epoch=YQServiceLifecycle.RequestEpoch;}
    bool IsFrontierConstructionWorkCurrent()=>Current&&YQServiceLifecycle.IsCurrent(epoch);
    class PreparedContinuationRefresh {}
    readonly struct FrontierPublicationTimingScope:IDisposable {public FrontierPublicationTimingScope(CloneStreamer owner,int phase){}public void Dispose(){}}
    bool TryPrepareContinuationRefresh(YQGeneratedWorldRuntimeBuilder.FrontierConstructionAcceptance construction,out PreparedContinuationRefresh refresh,out string failure)
    {Refreshes++;refresh=new PreparedContinuationRefresh();failure=RefreshOK?"":"refresh failure";return RefreshOK;}
    bool TryPublishAndInstallPreparedContinuation(YQProfileSaveSystem profile,YQProfileSaveSystem.PreparedContinuationPublication publication,PreparedContinuationRefresh refresh,out string failure)
    {failure="publish/install failure";if(!profile.TryPublish(publication))return false;Installs++;return InstallOK;}
@@ROUTINE@@
    public IEnumerator Begin()=>PublishAndInstallContinuationRoutine(_frontierConstructionAccepted,(success,failure)=>{Completions++;CompletedSuccess=success;});
}
static class Scheduling
{
    static int checks;
    static void Check(bool ok,string name){checks++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
    static CloneStreamer Fresh(out YQProfileSaveSystem profile,out WorldState world)
    {
        JsonConvert.DefaultSettings=null;WorldStateManager.Settings.Formatting=Formatting.None;YQServiceLifecycle.RequestEpoch=42;
        world=new WorldState{generatedWorldPlan=new GeneratedWorldPlanRecord{spatialPlanV2=new GeneratedSpatialWorldPlanV2Record()}};
        WorldStateManager.Instance=new WorldStateManager(world);profile=YQProfileSaveSystem.Instance=new YQProfileSaveSystem{ExpectedWorld=world};return new CloneStreamer(world);
    }
    static IEnumerator ToBaseGap(CloneStreamer s){var routine=s.Begin();Check(routine.MoveNext()&&routine.MoveNext(),"actual iterator yields after refresh then successful private world copy");return routine;}
    public static int Run()
    {
        var s=Fresh(out var p,out var w);var r=s.Begin();Check(r.MoveNext()&&s.Refreshes==1&&p.Prepares==0,"refresh gap has no player capture");
        p.Player.lastPosition=new UnityEngine.Vector3(1,0,0);Check(r.MoveNext()&&p.Prepares==0&&!p.Pointer,"world-copy gap still holds no paired player or pointer");
        p.Player.lastPosition=new UnityEngine.Vector3(2,0,0);Check(!r.MoveNext()&&p.Prepares==1&&p.Publishes==1&&s.Installs==1&&p.Pointer,"actual iterator captures moving player then publishes and installs in one step");
        Check(s.Completions==1&&s.CompletedSuccess,"success callback executes exactly once");
        Check(p.PlayerJson.Contains("\"x\":2.0")&&YQProfileCommitStore.JsonEqualsCaptured(p.Player,p.PlayerJson,WorldStateManager.Settings),"paired player equals freshest resumed-frame position");
        Check(!p.LastBase.TryTake(WorldStateManager.Instance,w,out _,out _),"final owner consumed and retired private token");
        s=Fresh(out p,out w);r=s.Begin();r.MoveNext();((IDisposable)r).Dispose();Check(p.Prepares==0&&!p.Pointer&&s.Completions==1&&!s.CompletedSuccess,"cancel refresh gap performs no capture or publication");
        s=Fresh(out p,out w);r=ToBaseGap(s);((IDisposable)r).Dispose();Check(p.Prepares==0&&!p.Pointer&&s.Completions==1&&!s.CompletedSuccess,"cancel private-copy gap retires iterator without publication");
        s=Fresh(out p,out w);r=ToBaseGap(s);s.Current=false;Check(!r.MoveNext()&&p.Prepares==0&&s.Completions==1,"stale construction owner at copy gap rejects before paired capture");
        s=Fresh(out p,out w);r=ToBaseGap(s);YQServiceLifecycle.RequestEpoch++;Check(!r.MoveNext()&&p.Prepares==0&&!p.Pointer,"request epoch replacement rejects before paired capture");
        s=Fresh(out p,out w);r=ToBaseGap(s);YQProfileSaveSystem.Instance=new YQProfileSaveSystem();Check(!r.MoveNext()&&p.Prepares==0&&!p.Pointer,"profile singleton replacement rejects before paired capture");
        s=Fresh(out p,out w);r=ToBaseGap(s);p.isActiveAndEnabled=false;Check(!r.MoveNext()&&p.Prepares==0&&!p.Pointer,"disabled profile at copy gap rejects");
        s=Fresh(out p,out w);r=ToBaseGap(s);w.generatedWorldPlan.worldSeed="changed-during-gap";p.Player.lastPosition=new UnityEngine.Vector3(3,0,0);Check(!r.MoveNext()&&p.Pointer&&p.DetachedWorld.generatedWorldPlan.worldSeed=="changed-during-gap","deep world drift falls back synchronously while fresh player can publish");
        s=Fresh(out p,out w);r=ToBaseGap(s);p.OnPrepare=()=>w.generatedWorldPlan.worldSeed="changed-after-final-capture";Check(!r.MoveNext()&&!p.Pointer&&p.Publishes==1&&s.Installs==0,"source-mutating final callback still reaches unchanged rejection gate");
        s=Fresh(out p,out w);r=ToBaseGap(s);p.OnPrepare=()=>p.Player.lastPosition=new UnityEngine.Vector3(8,0,0);Check(!r.MoveNext()&&!p.Pointer&&s.Installs==0,"post-capture player mutation remains a rejection");
        s=Fresh(out p,out w);r=ToBaseGap(s);p.PrepareOK=false;Check(!r.MoveNext()&&p.Publishes==0&&!p.Pointer&&s.Completions==1,"prepare refusal retains no pointer/install");
        s=Fresh(out p,out w);r=ToBaseGap(s);p.PublishOK=false;Check(!r.MoveNext()&&!p.Pointer&&s.Installs==0&&s.Completions==1,"publish refusal retains no installation");
        s=Fresh(out p,out w);r=ToBaseGap(s);s.InstallOK=false;Check(!r.MoveNext()&&p.Pointer&&!s.CompletedSuccess&&s.Completions==1,"failed post-pointer install preserves truthful durable result");
        s=Fresh(out p,out w);r=ToBaseGap(s);p.OnPrepare=()=>throw new InvalidOperationException("controlled");bool threw=false;try{r.MoveNext();}catch(InvalidOperationException){threw=true;}Check(threw&&s.Completions==1&&!p.Pointer&&!p.LastBase.TryTake(WorldStateManager.Instance,w,out _,out _),"throwing preparation callback still retires token and invokes completion once");
        s=Fresh(out p,out w);int defaults=0;JsonConvert.DefaultSettings=()=>{defaults++;return null;};r=s.Begin();Check(r.MoveNext()&&!r.MoveNext()&&p.Pointer&&p.LastBase==null,"unsupported serializer keeps original one-yield path");Check(defaults==6,"unsupported codec executes only existing final copy/player/equality operations");
        JsonConvert.DefaultSettings=null;return checks;
    }
}
