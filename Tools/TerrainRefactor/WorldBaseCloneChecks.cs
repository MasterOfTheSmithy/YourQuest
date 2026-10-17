using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using Newtonsoft.Json;

// note: Exact production helper/copy code executes with real DTOs/codecs; scene lifetime and profiler APIs are managed stand-ins.
static class Program
{
    static int count;
    static void Check(bool ok,string name) { count++;Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name); }
    static WorldState FreshWorld() => new WorldState { generatedWorldPlan=new GeneratedWorldPlanRecord { spatialPlanV2=new GeneratedSpatialWorldPlanV2Record() }, worldIdentity=new YQWorldIdentityRecord {ownerProfileId="profile-a",worldId="world-a"} };
    static WorldStateManager Fresh(out WorldState world) { JsonConvert.DefaultSettings=null;YQServiceLifecycle.RequestEpoch=17;WorldStateManager.Settings.Formatting=Formatting.None;world=FreshWorld();return WorldStateManager.Instance=new WorldStateManager(world); }
    static WorldStateManager.PreparedContinuationBaseWorld Capture(WorldStateManager owner,WorldState world) { Check(owner.TryCaptureContinuationBaseWorld(world,out var token),"plain canonical world stages");return token; }
    static void Main(string[] args)
    {
        try
        {
            var owner=Fresh(out var world);var original=JsonConvert.SerializeObject(world,WorldStateManager.Settings);var token=Capture(owner,world);
            Check(token.TryTake(owner,world,out var json,out var detached),"exact stable source consumes private preparation");
            Check(json==original && YQProfileCommitStore.JsonEqualsCaptured(detached,original,WorldStateManager.Settings),"stable reuse preserves exact canonical JSON");
            Check(!ReferenceEquals(detached,world)&&!ReferenceEquals(detached.generatedWorldPlan,world.generatedWorldPlan)&&!ReferenceEquals(detached.identityRecords,world.identityRecords),"copy stays detached from live owners and lists");
            Check(YQProfileCommitStore.JsonEqualsCaptured(world,original,WorldStateManager.Settings),"preparation and consumption never mutate canonical world");
            Check(!token.TryTake(owner,world,out _,out _),"consumed preparation cannot be reused");
            token=Capture(owner,world);token.Retire();Check(!token.TryTake(owner,world,out _,out _),"cancellation retires private copy");
            token=Capture(owner,world);world.stateRevision++;var fresh=owner.CopyFinal(world,token,out json);Check(YQProfileCommitStore.JsonEqualsCaptured(fresh,JsonConvert.SerializeObject(world,WorldStateManager.Settings),WorldStateManager.Settings),"revision drift falls back to exact fresh capture");
            token=Capture(owner,world);world.generatedWorldPlan.worldSeed="changed-without-touch";Check(!token.TryTake(owner,world,out _,out _),"deep accepted input drift rejects despite stable object identity");
            token=Capture(owner,world);world.generatedWorldPlan=new GeneratedWorldPlanRecord{spatialPlanV2=world.generatedWorldPlan.spatialPlanV2,worldSeed=world.generatedWorldPlan.worldSeed};Check(!token.TryTake(owner,world,out _,out _),"plan replacement rejects");
            token=Capture(owner,world);world.generatedWorldPlan.spatialPlanV2=new GeneratedSpatialWorldPlanV2Record();Check(!token.TryTake(owner,world,out _,out _),"base artifact replacement rejects");
            token=Capture(owner,world);world.generatedWorldPlan.spatialPlanV2.acceptedContinuation=new GeneratedSpatialContinuationV2Record();Check(!token.TryTake(owner,world,out _,out _),"prior continuation replacement rejects");
            token=Capture(owner,world);world.identityRecords=new List<YQEntityIdentityRecord>(world.identityRecords);Check(!token.TryTake(owner,world,out _,out _),"byte-equivalent identity-list replacement rejects");
            token=Capture(owner,world);world.worldIdentity=new YQWorldIdentityRecord{ownerProfileId="profile-a",worldId="world-a"};Check(!token.TryTake(owner,world,out _,out _),"byte-equivalent world-identity replacement rejects");
            token=Capture(owner,world);world.worldIdentity.ownerProfileId="other";Check(!token.TryTake(owner,world,out _,out _),"profile scope drift rejects");
            token=Capture(owner,world);world.worldIdentity.worldId="other";Check(!token.TryTake(owner,world,out _,out _),"world identity drift rejects");
            token=Capture(owner,world);YQServiceLifecycle.RequestEpoch++;Check(!token.TryTake(owner,world,out _,out _),"service epoch replacement rejects");
            owner=Fresh(out world);token=Capture(owner,world);owner.isActiveAndEnabled=false;Check(!token.TryTake(owner,world,out _,out _),"disabled owner rejects");
            owner=Fresh(out world);token=Capture(owner,world);WorldStateManager.Instance=new WorldStateManager(world);Check(!token.TryTake(owner,world,out _,out _),"singleton replacement rejects");
            owner=Fresh(out world);token=Capture(owner,world);owner.State=FreshWorld();Check(!token.TryTake(owner,world,out _,out _),"manager document replacement rejects");
            owner=Fresh(out world);token=Capture(owner,world);Check(!token.TryTake(new WorldStateManager(world),world,out _,out _),"foreign manager rejects");
            token=Capture(owner,world);Check(!token.TryTake(owner,FreshWorld(),out _,out _),"foreign expected source rejects");
            bool forged=false;try { WorldStateManager.PreparedContinuationBaseWorld.Issue(owner,world,"{}",world,new object[0],new object()); } catch(InvalidOperationException){forged=true;}Check(forged,"foreign issuer cannot create usable token");
            owner=Fresh(out world);token=Capture(owner,world);WorldStateManager.Settings.Formatting=Formatting.Indented;fresh=owner.CopyFinal(world,token,out json);Check(json.Contains("\n")&&YQProfileCommitStore.JsonEqualsCaptured(fresh,json,WorldStateManager.Settings),"serializer configuration drift uses original synchronous codec");
            owner=Fresh(out world);int callbacks=0;JsonConvert.DefaultSettings=()=>{callbacks++;return null;};Check(!owner.TryCaptureContinuationBaseWorld(world,out token)&&callbacks==0,"custom default-settings callback never runs in early stage");
            fresh=owner.CopyFinal(world,token,out json);Check(callbacks==3&&YQProfileCommitStore.JsonEqualsCaptured(fresh,json,WorldStateManager.Settings),"unsupported defaults preserve original final callback multiplicity");
            owner=Fresh(out world);token=Capture(owner,world);callbacks=0;JsonConvert.DefaultSettings=()=>{callbacks++;return null;};fresh=owner.CopyFinal(world,token,out json);Check(callbacks==3,"defaults introduced during gap bypass staged comparison and preserve original callbacks");
            owner=Fresh(out world);var hostile=new CallbackWorld{generatedWorldPlan=world.generatedWorldPlan};owner.State=hostile;CallbackWorld.Callbacks=0;Check(!owner.TryCaptureContinuationBaseWorld(hostile,out token)&&CallbackWorld.Callbacks==0,"runtime subtype hook is rejected before early callback");
            owner=Fresh(out world);token=Capture(owner,world);world.npcs.Add(new CallbackNpc());CallbackNpc.Callbacks=0;fresh=owner.CopyFinal(world,token,out json);Check(CallbackNpc.Callbacks==1,"nested subtype introduced during gap receives exactly original final serialization callback");
            Check(YQProfileCommitStore.JsonEqualsCaptured(fresh,json,WorldStateManager.Settings),"unsupported nested subtype fallback preserves original copy parity");
            owner=Fresh(out world);world.schemaVersion++;Check(!owner.TryCaptureContinuationBaseWorld(world,out _),"unsupported schema cannot stage");
            owner=Fresh(out world);world.stateRevision=long.MaxValue;Check(!owner.TryCaptureContinuationBaseWorld(world,out _),"exhausted revision cannot stage");
            owner=Fresh(out world);world.generatedWorldPlan.spatialPlanV2=null;Check(!owner.TryCaptureContinuationBaseWorld(world,out _),"absent accepted base cannot stage");
            owner=Fresh(out world);world.identityRecords=null;Check(!owner.TryCaptureContinuationBaseWorld(world,out _),"absent identities cannot stage");
            owner=Fresh(out world);token=Capture(owner,world);var player=new PlayerState();player.lastPosition=new UnityEngine.Vector3(30,2,8);fresh=owner.CopyFinal(world,token,out json);string playerJson=JsonConvert.SerializeObject(player,WorldStateManager.Settings);Check(YQProfileCommitStore.JsonEqualsCaptured(player,playerJson,WorldStateManager.Settings)&&player.lastPosition.x==30,"moving player is freshly captured after world-only gap");
            Check(fresh.generatedWorldPlan.worldSeed==world.generatedWorldPlan.worldSeed,"player movement does not invalidate unchanged world base");
            owner=Fresh(out world);token=Capture(owner,world);world.generatedWorldPlan.worldSeed="deep-new";fresh=owner.CopyFinal(world,token,out json);Check(fresh.generatedWorldPlan.worldSeed=="deep-new","world drift fallback copies current inputs without dropping fresh player pair");
            string captured=json;world.generatedWorldPlan.worldSeed="post-capture-drift";Check(!YQProfileCommitStore.JsonEqualsCaptured(world,captured,WorldStateManager.Settings),"existing final exact source proof still rejects subsequent drift");
            owner=Fresh(out world);world=JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(args[0]),WorldStateManager.Settings);owner.State=world;var timer=System.Diagnostics.Stopwatch.StartNew();token=Capture(owner,world);long stage=timer.ElapsedMilliseconds;timer.Restart();Check(token.TryTake(owner,world,out json,out detached),"real immutable accepted profile world stages and consumes");long take=timer.ElapsedMilliseconds;Console.WriteLine("DETACHED_SAMPLE stage_ms="+stage+" take_ms="+take+" bytes="+json.Length+"; NOT_NATIVE_PERFORMANCE");
            Check(YQProfileCommitStore.JsonEqualsCaptured(detached,json,WorldStateManager.Settings),"real saved-world exact parity survives staging");
            count+=Scheduling.Run();
            Console.WriteLine("RESULT checks="+count+" failures=0");
        }
        finally { JsonConvert.DefaultSettings=null; }
    }
    sealed class CallbackWorld:WorldState { public static int Callbacks;[OnSerializing]void OnSerialize(StreamingContext context){Callbacks++;} }
    sealed class CallbackNpc:WorldState.NpcRecord { public static int Callbacks;[OnSerializing]void OnSerialize(StreamingContext context){Callbacks++;} }
}
