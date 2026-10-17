using System;
using System.Reflection;
using System.Text.Json;

public sealed class GeneratedWorldPlanRecord {public string worldSeed;}
public sealed class WorldIdentity {public string ownerProfileId,worldId;}
public sealed class WorldState {public WorldIdentity worldIdentity;}
public static class JsonUtility {
    public static int Parses;
    static readonly JsonSerializerOptions settings=new(){IncludeFields=true};
    public static T FromJson<T>(string json){Parses++;try{return JsonSerializer.Deserialize<T>(json,settings);}catch(JsonException error){throw new ArgumentException("Controlled JSON substitute",error);}}
    public static string ToJson(object value)=>JsonSerializer.Serialize(value,settings);
}
public static class TerrainProviderMemoChecks {
    static int checks,failures;
    static void Require(bool pass,string name){checks++;Console.WriteLine((pass?"PASS ":"FAIL ")+name);if(!pass)failures++;}
    static WorldState World(string owner="profile",string id="world")=>new(){worldIdentity=new(){ownerProfileId=owner,worldId=id}};
    static GeneratedWorldPlanRecord Plan(string seed="seed")=>new(){worldSeed=seed};
    static YQGeneratedWorldTerrain.ProfileTerrainSnapshotRecord Record()=>new(){schemaVersion=1,hasTerrain=true,ownerProfileId="profile",worldId="world",worldSeed="seed",planFingerprint="hash",heightmapChecksum="checksum",compressedHeightmapBase64=new string('A',1024*1024)};
    static bool Cached(MemoProbe memo,string json,string profile="profile",WorldState world=null,GeneratedWorldPlanRecord plan=null,string hash="hash")=>memo.IsCompatibleCachedProfileTerrainSnapshot(json,profile,world,plan,hash);
    static void Equivalent(MemoProbe memo,string json,string profile,WorldState world,GeneratedWorldPlanRecord plan,string hash,string name){
        bool expected=MemoProbe.IsCompatibleProfileTerrainSnapshot(json,profile,world,plan,hash);
        bool first=Cached(memo,json,profile,world,plan,hash);int parses=JsonUtility.Parses;
        bool second=Cached(memo,json,profile,world,plan,hash);
        Require(first==expected&&second==expected,"exact predicate equivalence: "+name);
        Require(JsonUtility.Parses==parses,"same complete input reuses declaration: "+name);
    }
    public static int Main(){
        var memo=new MemoProbe();var world=World();var plan=Plan();string json=JsonUtility.ToJson(Record());
        Equivalent(memo,json,"profile",world,plan,"hash","valid 1 MiB immutable JSON");
        int count=JsonUtility.Parses;Cached(memo,json,"profile",World(),Plan(),"hash");Require(JsonUtility.Parses==count,"equivalent replacement world/plan objects retain same pure scalar result");
        foreach(string change in new[]{"profile","worldOwner","worldId","seed","fingerprint"}){
            var w=World();var p=Plan();string profile="profile",hash="hash";
            if(change=="profile")profile="other";if(change=="worldOwner")w.worldIdentity.ownerProfileId="other";if(change=="worldId")w.worldIdentity.worldId="other";if(change=="seed")p.worldSeed="other";if(change=="fingerprint")hash="other";
            Equivalent(memo,json,profile,w,p,hash,"changed context "+change);
            Require(!Cached(memo,json,profile,w,p,hash),"changed context rejects: "+change);
        }
        var liveWorld=World();var livePlan=Plan();Cached(memo,json,"profile",liveWorld,livePlan,"hash");liveWorld.worldIdentity.worldId="mutated_without_revision";
        Require(!Cached(memo,json,"profile",liveWorld,livePlan,"hash"),"same live world object mutation is never hidden by reference/revision cache");liveWorld.worldIdentity.worldId="world";livePlan.worldSeed="mutated_without_revision";
        Require(!Cached(memo,json,"profile",liveWorld,livePlan,"hash"),"same live plan object seed mutation revalidates");
        foreach(string change in new[]{"schema","presence","checksum","profile","world","seed","fingerprint"}){
            var r=Record();if(change=="schema")r.schemaVersion=2;if(change=="presence")r.hasTerrain=false;if(change=="checksum")r.heightmapChecksum="";if(change=="profile")r.ownerProfileId="other";if(change=="world")r.worldId="other";if(change=="seed")r.worldSeed="other";if(change=="fingerprint")r.planFingerprint="other";
            string changed=JsonUtility.ToJson(r);Equivalent(memo,changed,"profile",world,plan,"hash","invalid declaration "+change);Require(!Cached(memo,changed,"profile",world,plan,"hash"),"invalid declaration remains rejected: "+change);
        }
        foreach(string invalid in new[]{"{broken","null","[]",""," ",null})Equivalent(memo,invalid,"profile",world,plan,"hash","malformed/empty JSON "+(invalid??"null string"));
        Equivalent(memo,json,"",world,plan,"hash","empty active profile");Equivalent(memo,json,null,world,plan,"hash","null active profile");
        Equivalent(memo,json,"PROFILE",World("PROFILE","WORLD"),plan,"hash","existing case-insensitive ownership matching");
        Equivalent(memo,json,"profile",null,plan,"changed","no world retains owned declaration as before");Equivalent(memo,json,"profile",new WorldState(),plan,"changed","missing world identity retains as before");Equivalent(memo,json,"profile",world,null,"changed","missing plan retains as before");
        Equivalent(memo,json,"profile",world,plan,"changed","world identity restored must reject fingerprint");
        var distinct=new string(json.ToCharArray());Require(!ReferenceEquals(json,distinct),"fixture creates distinct JSON object with identical bytes");var fresh=new MemoProbe();Cached(fresh,json,"profile",world,plan,"hash");count=JsonUtility.Parses;Cached(fresh,distinct,"profile",world,plan,"hash");Require(JsonUtility.Parses==count+1,"distinct JSON reference always runs original decoder");
        var freshOwner=new MemoProbe();count=JsonUtility.Parses;Cached(freshOwner,json,"profile",world,plan,"hash");Require(JsonUtility.Parses==count+1,"new builder instance has no global cached authority");
        var eviction=new MemoProbe();string two=new string(json.ToCharArray()),three=new string(json.ToCharArray());Cached(eviction,json,"profile",world,plan,"hash");Cached(eviction,two,"profile",world,plan,"hash");Cached(eviction,three,"profile",world,plan,"hash");count=JsonUtility.Parses;Cached(eviction,json,"profile",world,plan,"hash");Require(JsonUtility.Parses==count+1,"third distinct candidate evicts first from bounded two-entry cache");
        var retained=(Array)typeof(MemoProbe).GetField("_profileTerrainCompatibilityMemos",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(eviction);Require(retained.Length==2,"cache retains exactly two compatibility declarations");
        var transaction=new MemoProbe();count=JsonUtility.Parses;for(int i=0;i<3;i++)Cached(transaction,json,"profile",world,plan,"hash");Require(JsonUtility.Parses==count+1,"three unchanged provider captures decode once");count=JsonUtility.Parses;for(int i=0;i<3;i++)MemoProbe.IsCompatibleProfileTerrainSnapshot(json,"profile",world,plan,"hash");Require(JsonUtility.Parses==count+3,"original predicate performs three native-decode calls for same transaction inputs");
        count=JsonUtility.Parses;Cached(transaction,json,"profile",world,plan,"changed");Require(JsonUtility.Parses==count+1&&!Cached(transaction,json,"profile",world,plan,"changed"),"post-callback fingerprint mutation forces original revalidation/rejection");
        Console.WriteLine($"RESULT checks={checks} failures={failures}");return failures==0?0:1;
    }
}
