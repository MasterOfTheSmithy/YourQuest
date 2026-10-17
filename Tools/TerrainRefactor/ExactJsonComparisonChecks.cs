using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;

// note: Real Json.NET and production value converters are used; no Unity runtime, profile writes or gameplay services are invoked.
public static class ExactJsonComparisonChecks
{
    static int checks, failures;
    static void Require(bool pass,string name){checks++;if(!pass)failures++;Console.WriteLine((pass?"PASS ":"FAIL ")+name);}
    public sealed class Values {
        public string text;public string optional;public int[] integers;public Dictionary<string,string> mapping;
        public float single;public double number;public decimal precise;public DateTime when;public Vector3 vector;public Quaternion rotation;
    }
    public sealed class Cycle {public string id;public Cycle next;}
    public sealed class CallbackValue {
        public static int reads;public string First{get{reads++;return "actual";}}
        public string Last{get{reads++;return "still_visits_later_field";}}
    }
    public sealed class ThrowingValue {
        public string First=>"actual";
        public string Last=>throw new InvalidOperationException("late getter remains observable");
    }
    public sealed class RawValue {public string raw;}
    public sealed class RawConverter:JsonConverter {
        public override bool CanConvert(Type t)=>t==typeof(RawValue);
        public override void WriteJson(JsonWriter writer,object value,JsonSerializer serializer){writer.WriteRawValue(((RawValue)value).raw);}
        public override object ReadJson(JsonReader r,Type t,object old,JsonSerializer s)=>throw new NotSupportedException();
    }
    static JsonSerializerSettings Settings(Formatting format=Formatting.None)=>new(){Formatting=format,ReferenceLoopHandling=ReferenceLoopHandling.Ignore,Converters={new Vector2JsonConverter(),new Vector3JsonConverter(),new QuaternionJsonConverter()}};
    static string Error(Action action){try{action();return "none";}catch(Exception error){return error.GetType().FullName+"|"+error.Message;}}
    static void Equivalent(object value,JsonSerializerSettings settings,string name){
        string expected=JsonConvert.SerializeObject(value,settings);
        Require(YQProfileCommitStore.JsonEqualsCaptured(value,expected,settings),"exact output parity: "+name);
        Require(!YQProfileCommitStore.JsonEqualsCaptured(value,expected+" ",settings),"suffix mismatch rejects: "+name);
        Require(!YQProfileCommitStore.JsonEqualsCaptured(value,expected.Length>0?expected.Substring(1):"x",settings),"prefix/length mismatch rejects: "+name);
        Require(!YQProfileCommitStore.JsonEqualsCaptured(value,null,settings),"null captured text rejects: "+name);
    }
    static Dictionary<string,object> Allocation(object value,JsonSerializerSettings settings,string name){
        string captured=JsonConvert.SerializeObject(value,settings);
        YQProfileCommitStore.JsonEqualsCaptured(value,captured,settings);
        // note: Reclaim earlier parity/fixture allocations before each timed phase so baseline garbage does not get charged to the candidate.
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        long before=GC.GetAllocatedBytesForCurrentThread();var watch=Stopwatch.StartNew();
        for(int i=0;i<3;i++)if(!string.Equals(JsonConvert.SerializeObject(value,settings),captured,StringComparison.Ordinal))throw new Exception("baseline changed");
        watch.Stop();long baseline=GC.GetAllocatedBytesForCurrentThread()-before;double baselineMs=watch.Elapsed.TotalMilliseconds;
        GC.Collect();GC.WaitForPendingFinalizers();GC.Collect();
        before=GC.GetAllocatedBytesForCurrentThread();watch.Restart();
        for(int i=0;i<3;i++)if(!YQProfileCommitStore.JsonEqualsCaptured(value,captured,settings))throw new Exception("comparison changed");
        watch.Stop();long candidate=GC.GetAllocatedBytesForCurrentThread()-before;
        Require(candidate<baseline,"detached allocations decrease: "+name);
        return new(){["case"]=name,["iterations"]=3,["jsonChars"]=captured.Length,["baselineAllocatedBytes"]=baseline,["comparisonAllocatedBytes"]=candidate,["savedAllocatedBytes"]=baseline-candidate,["baselineMs"]=baselineMs,["comparisonMs"]=watch.Elapsed.TotalMilliseconds,["evidence"]="DETACHED_CLR_ONLY; NOT_NATIVE_FRAME_TIMING"};
    }
    public static int Main(string[] args){
        var values=new Values{text="quote\" slash\\ newline\n tab\t\r unicodeé漢😀 e\u0301",optional=null,integers=new[]{int.MinValue,0,int.MaxValue},mapping=new(){{"b","second"},{"a","first"}},single=float.NaN,number=double.PositiveInfinity,precise=123.456789012345678901m,when=new DateTime(2026,10,14,7,8,9,DateTimeKind.Utc),vector=new Vector3(1,-2,.001f),rotation=new Quaternion(.1f,.2f,.3f,.4f)};
        foreach(var format in new[]{Formatting.None,Formatting.Indented})Equivalent(values,Settings(format),"typed values "+format);
        var escaped=Settings();escaped.StringEscapeHandling=StringEscapeHandling.EscapeNonAscii;Equivalent(values,escaped,"escaped unicode/surrogates");
        var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("fr-FR");Equivalent(values,Settings(Formatting.Indented),"noninvariant process culture");}finally{CultureInfo.CurrentCulture=culture;}
        var typed=Settings();typed.TypeNameHandling=TypeNameHandling.All;Equivalent(values,typed,"explicit type metadata");
        Equivalent(null,Settings(),"null value");Equivalent(new[]{"","α","\\\"","\ud800"},Settings(),"empty and unpaired-surrogate strings");
        var cycle=new Cycle{id="self"};cycle.next=cycle;Equivalent(cycle,Settings(),"ignored reference loop");
        var errorSettings=Settings();errorSettings.ReferenceLoopHandling=ReferenceLoopHandling.Error;
        Require(Error(()=>JsonConvert.SerializeObject(cycle,errorSettings))==Error(()=>YQProfileCommitStore.JsonEqualsCaptured(cycle,"wrong",errorSettings)),"loop rejection/error path remains unchanged");
        string before=JsonConvert.SerializeObject(values,Settings());values.integers[1]=1;Require(!YQProfileCommitStore.JsonEqualsCaptured(values,before,Settings()),"deep collection mutation without revision rejected");values.integers[1]=0;
        values.mapping=new(){{"a","first"},{"b","second"}};Require(!YQProfileCommitStore.JsonEqualsCaptured(values,before,Settings()),"dictionary output-order change remains rejected");
        var callback=new CallbackValue();CallbackValue.reads=0;JsonConvert.SerializeObject(callback,Settings());int reads=CallbackValue.reads;CallbackValue.reads=0;Require(!YQProfileCommitStore.JsonEqualsCaptured(callback,"wrong",Settings())&&CallbackValue.reads==reads,"mismatch does not skip later serialized getters");
        var late=new ThrowingValue();Require(Error(()=>JsonConvert.SerializeObject(late,Settings()))==Error(()=>YQProfileCommitStore.JsonEqualsCaptured(late,"wrong",Settings())),"mismatch does not swallow late serialization error");
        Require(YQProfileCommitStore.JsonEqualsCaptured(new CallbackValue(),JsonConvert.SerializeObject(new CallbackValue(),Settings()),Settings()),"fresh operation succeeds after prior serialization exception");
        var rawSettings=Settings();rawSettings.Converters.Add(new RawConverter());Equivalent(new RawValue{raw="{\"raw\": [1,2], \"escaped\":\"\\u00e9\"}"},rawSettings,"raw converter chunks");
        var defaults=JsonConvert.DefaultSettings;int factories=0;try{JsonConvert.DefaultSettings=()=>{factories++;return new JsonSerializerSettings{Formatting=Formatting.Indented,NullValueHandling=NullValueHandling.Ignore};};string json=JsonConvert.SerializeObject(values,Settings());int prior=factories;Require(YQProfileCommitStore.JsonEqualsCaptured(values,json,Settings())&&factories==prior+1,"same default-settings factory and explicit-settings merge");}finally{JsonConvert.DefaultSettings=defaults;}
        var sinkType=typeof(YQProfileCommitStore).GetNestedType("CapturedJsonComparisonWriter",BindingFlags.NonPublic);
        var sink=(TextWriter)Activator.CreateInstance(sinkType,BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{"abc"},CultureInfo.InvariantCulture);
        sink.Write('a');sink.Write(new[]{'x','b','c','x'},1,2);sink.Flush();Require((bool)sinkType.GetProperty("MatchesSnapshot",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(sink),"offset/count character chunks compare exact text");
        Require(sink.FormatProvider==CultureInfo.InvariantCulture&&sink.Encoding==System.Text.Encoding.Unicode,"writer matches original invariant UTF16 text contract");
        Require(Error(()=>sink.Write(new[]{'x'},2,1))!="none"&&Error(()=>sink.Write(null,0,1))!="none","invalid writer buffer arguments still reject");sink.Dispose();
        Require(sinkType.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Length==0,"comparison sink has no shared mutable serialization state");
        var leftLocation=new GeneratedSpatialContinuationLocationV2Record{anchor=new YQSiteAnchorV2{x=0}};
        Func<bool,bool> callbackPrefix=current=>{
            var left=new GeneratedSpatialContinuationV2Record{locations=new(){leftLocation}};
            var right=new GeneratedSpatialContinuationV2Record{locations=new(){new GeneratedSpatialContinuationLocationV2Record{anchor=new YQSiteAnchorV2{x=100}}}};
            var previous=JsonConvert.DefaultSettings;int called=0;
            try{JsonConvert.DefaultSettings=()=>{if(called++==0)right.locations[0]=leftLocation;return new JsonSerializerSettings();};return current?WsmPrefixProbe.PreserveCurrentPrefix(left,right,out _):WsmPrefixProbe.PreserveOriginalPrefix(left,right,out _);}finally{JsonConvert.DefaultSettings=previous;}
        };
        Require(callbackPrefix(false)&&callbackPrefix(true),"source serialization callbacks occur before target lookup as in original prefix gate");
        var preservedLeft=new GeneratedSpatialContinuationV2Record{locations=new(){leftLocation}};
        var changedRight=new GeneratedSpatialContinuationV2Record{locations=new(){new GeneratedSpatialContinuationLocationV2Record{anchor=new YQSiteAnchorV2{x=5}}}};
        Require(!WsmPrefixProbe.PreserveCurrentPrefix(preservedLeft,changedRight,out _)&&!WsmPrefixProbe.PreserveOriginalPrefix(preservedLeft,changedRight,out _),"changed accepted location rejected by both prefix gates");
        var allocations=new List<Dictionary<string,object>>();allocations.Add(Allocation(new Values{text=new string('A',2600000)},Settings(),"2.6MB payload"));
        foreach(string file in args){string captured=File.ReadAllText(file);WorldState world=JsonConvert.DeserializeObject<WorldState>(captured,Settings());Equivalent(world,Settings(),"saved world DTO "+Path.GetFileName(Path.GetDirectoryName(file)));string canonical=JsonConvert.SerializeObject(world,Settings());world.stateRevision++;Require(!YQProfileCommitStore.JsonEqualsCaptured(world,canonical,Settings()),"saved world canonical mutation rejects");world.stateRevision--;allocations.Add(Allocation(world,Settings(),"actual saved world DTO"));}
        File.WriteAllText("Allocation.json",JsonConvert.SerializeObject(allocations,Formatting.Indented));
        Console.WriteLine($"RESULT checks={checks} failures={failures}");return failures==0?0:1;
    }
}
