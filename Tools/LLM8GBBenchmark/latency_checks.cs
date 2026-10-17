using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: Native Unity/network/process calls are fakes here; production handoff, options and argument methods are exact source extracts.
namespace UnityEngine
{
    public class ScriptableObject { public static T CreateInstance<T>() where T:new() => new T(); }
    public class CreateAssetMenuAttribute:Attribute { public string menuName; }
    public class HeaderAttribute:Attribute { public HeaderAttribute(string value) {} }
    public class RangeAttribute:Attribute { public RangeAttribute(float min,float max) {} }
    public static class Mathf
    {
        public static int Clamp(int x,int lo,int hi)=>Math.Clamp(x,lo,hi);
        public static float Clamp(float x,float lo,float hi)=>Math.Clamp(x,lo,hi);
        public static int Max(int x,int y)=>Math.Max(x,y);
        public static int Min(int x,int y)=>Math.Min(x,y);
        public static int RoundToInt(float x)=>(int)Math.Round(x);
    }
    public static class SystemInfo { public static int processorCount=16; }
    public static class Debug { public static void Log(string text) {} }
}
// note: Qualification classes are inert dependencies; these tests do not qualify any speech model.
public static class YQRepairEpisode { public static string Hash(string value)=>value; }
public static class YQGoddessSpeechPlan { public const string ContractHash="unqualified"; public const int ContextTokens=4096; }
public static class YQGoddessGrounding { public const string ReviewContractHash="unqualified"; public const int ReviewContextTokens=4096; }
public static class YourQuestTutorialAutoBootstrap { public static bool GameplayPresentationReleased; }

partial class LlamaCppServerProcess
{
    public bool OwnsProcess=true, StopSucceeds=true;
    public List<string> Events;
    public IEnumerator StopOwnedProcessRoutine(Action<bool,string> complete)
    { Events.Add("stop-main"); if(StopSucceeds)OwnsProcess=false; complete(StopSucceeds,"stop result"); yield break; }
    public static string Arguments(LLMRuntimeConfig config,string help,bool live=false)
    { if(!TryBuildArguments(config,help,out string args,out string error,live))throw new Exception(error);return args; }
}
partial class ClientUnderTest
{
    public long AvailableMemoryBytes=8192*YQLlmResidencyPolicy.MiB;
    public readonly List<string> Events=new List<string>();
    public LlamaCppServerProcess _llamaServer;
    public string _residentOllamaModel="small-test:latest", _residentOllamaBaseUrl="http://127.0.0.1:11434";
    public string _ownedLlamaResidencyKey="", _residentOllamaOptionsKey="", _llamaPromptScope="";
    public bool _residentOllamaRequestedCpuOnly=true, _usingRuntimeDefaultConfig, IsExclusiveSequenceActive, logRequestSummaries;
    public bool _preemptionExitPending;
    public bool ReleaseSucceeds=true;
    public string model="fallback", ProbeBody;
    public int numPredict=512;
    private bool RequiresJsonOutput(string tag)=>false;
    private IEnumerator EnsureOllamaReady(LLMRuntimeConfig c,Action<bool,string> cb) { Events.Add("ensure-ollama");cb(true,"");yield break; }
    private IEnumerator ReleaseResidentOllama(Action<bool,string> cb) { Events.Add("release-helper");cb(ReleaseSucceeds,"release result");yield break; }
    private IEnumerator ProbeLocalHealth(string url,int timeout,Action<bool,string> cb,bool body=false) { Events.Add("probe-cpu");cb(true,ProbeBody);yield break; }
    public ClientUnderTest(LLMRuntimeConfig config)
    {
        _llamaServer=new LlamaCppServerProcess { Events=Events };
        _ownedLlamaResidencyKey=LlamaResidencyKey(config);
        ProbeBody=Program.Resident();
    }
    public Dictionary<string,object> Options(LLMRuntimeConfig c, LLMGenerationProfile p, Dictionary<string,object> options)
        =>BuildOllamaOptions(c,p,options);
    public JObject Packet(LLMRuntimeConfig c,YQLlmBackend backend,LLMGenerationProfile p,Dictionary<string,object> options)
        =>JObject.Parse(BuildRequestJson(c,backend,"entire current prompt","test",options,p,true,new Dictionary<string,object>{{"type","object"}}));
    public JObject PacketFor(LLMRuntimeConfig c,YQLlmBackend backend,LLMGenerationProfile p,Dictionary<string,object> options,string prompt,Dictionary<string,object> schema)
        =>JObject.Parse(BuildRequestJson(c,backend,prompt,"test",options,p,true,schema));
    public void Remember(Dictionary<string,object> options) => _residentOllamaOptionsKey=YQLlmResidencyPolicy.OptionsKey(JObject.FromObject(options));
    public bool Handoff(LLMRuntimeConfig c,YQLlmBackend backend,LLMGenerationProfile p,Dictionary<string,object> options)
    {
        bool? result=null; Drain(PrepareRoutedModelResidency(c,backend,p,options,(ok,_)=>result=ok));
        if(result==null)throw new Exception("Handoff missed completion");return result.Value;
    }
    private static void Drain(IEnumerator routine)
    { using(routine as IDisposable)while(routine.MoveNext())if(routine.Current is IEnumerator child)Drain(child); }
}

internal static class Program
{
    static readonly List<string> passed=new List<string>();
    static void Check(bool ok,string name) { if(!ok)throw new Exception(name);passed.Add(name); }
    public static string Resident(long vram=0,long size=1200*YQLlmResidencyPolicy.MiB)
        =>JsonConvert.SerializeObject(new { models=new[]{new { name="small-test:latest",size_vram=vram,size }} });
    static void Policies()
    {
        long free=8192*YQLlmResidencyPolicy.MiB;
        bool Allow(string body,long available=0,long additional=0) =>YQLlmResidencyPolicy.CanRetainCpu(body,"small-test",2048,available==0?free:available,4096,additional);
        Check(Allow(Resident()),"verified CPU helper admitted");
        Check(!Allow(Resident(1)),"any GPU allocation rejects retention");
        Check(!Allow(Resident(size:0)),"zero size rejects retention");
        Check(!Allow(Resident(size:2049*YQLlmResidencyPolicy.MiB)),"oversized CPU model rejects retention");
        Check(!Allow(Resident(),2048*YQLlmResidencyPolicy.MiB),"low RAM uses eviction");
        Check(!Allow(Resident(),additional:5000*YQLlmResidencyPolicy.MiB),"cold main allocation reserves RAM");
        foreach(string bad in new[]{"{}","[]","null","{","{\"models\":[]}","{\"models\":[null]}",
            "{\"models\":[{\"name\":\"small-test:latest\",\"size\":100}]}",
            "{\"models\":[{\"name\":\"small-test:latest\",\"size\":100,\"size_vram\":\"0\"}]}"})
            Check(!Allow(bad),"unproven telemetry rejected: "+bad);
        var duplicate=JObject.Parse(Resident());((JArray)duplicate["models"]).Add(duplicate["models"][0].DeepClone());
        Check(!Allow(duplicate.ToString()),"duplicate model residency rejected");
        Check(!YQLlmResidencyPolicy.CanRetainCpu(Resident(),"small-test",2048,0,4096,0),"unknown physical RAM rejected");
        Check(YQLlmResidencyPolicy.AvailableSystemMemoryBytes()>0,"native free RAM measurement available");
        Check(YQLlmResidencyPolicy.IsLocalEndpoint("http://[::1]:11434"),"IPv6 loopback admitted");
        Check(!YQLlmResidencyPolicy.IsLocalEndpoint("http://192.0.2.1:11434"),"remote placement excluded");
        Check(!YQLlmResidencyPolicy.ExplicitCpu(JObject.Parse("{\"num_gpu\":-1}")),"automatic placement not CPU proof");
        Check(!YQLlmResidencyPolicy.ExplicitCpu(JObject.Parse("{\"num_gpu\":\"0\"}")),"string placement not CPU proof");
        string scope=YQLlmResidencyPolicy.PromptScope("p","w",1,2,3,"m","s");
        Check(YQLlmResidencyPolicy.CanReusePrompt(scope,scope),"unchanged scope reuses exact prefix");
        foreach(string other in new[]{YQLlmResidencyPolicy.PromptScope("p2","w",1,2,3,"m","s"),
            YQLlmResidencyPolicy.PromptScope("p","w2",1,2,3,"m","s"),YQLlmResidencyPolicy.PromptScope("p","w",2,2,3,"m","s"),
            YQLlmResidencyPolicy.PromptScope("p","w",1,3,3,"m","s"),YQLlmResidencyPolicy.PromptScope("p","w",1,2,4,"m","s"),
            YQLlmResidencyPolicy.PromptScope("p","w",1,2,3,"m2","s"),YQLlmResidencyPolicy.PromptScope("p","w",1,2,3,"m","s2"),
            YQLlmResidencyPolicy.PromptScope("","w",1,2,3,"m","s"),""})
            Check(!YQLlmResidencyPolicy.CanReusePrompt(scope,other),"changed or missing scope disables reuse: "+other);
    }
    static void Handoffs()
    {
        var c=new LLMRuntimeConfig();var p=new LLMGenerationProfile { ollamaModel="small-test:latest",cpuOnly=true };
        var options=new Dictionary<string,object>{{"num_gpu",0},{"num_ctx",4096}};
        ClientUnderTest Fresh() { var x=new ClientUnderTest(c);x.Remember(options);return x; }
        var x=Fresh();Check(x.Handoff(c,YQLlmBackend.Ollama,p,options)&&x._llamaServer.OwnsProcess&&!x.Events.Contains("release-helper"),"warm CPU helper preserves main");
        x=Fresh();Check(x.Handoff(c,YQLlmBackend.LlamaCpp,p,options)&&!x.Events.Contains("release-helper")&&!x.Events.Contains("stop-main"),"warm return to main preserves helper");
        x=Fresh();x._residentOllamaRequestedCpuOnly=false;Check(x.Handoff(c,YQLlmBackend.Ollama,p,options)&&x.Events.Contains("stop-main"),"cold unverified helper takes serial path");
        x=Fresh();x.ProbeBody=Resident(1);Check(x.Handoff(c,YQLlmBackend.LlamaCpp,p,options)&&x.Events.Contains("release-helper"),"GPU helper unloaded before main");
        x=Fresh();x.ProbeBody="{}";Check(x.Handoff(c,YQLlmBackend.Ollama,p,options)&&x.Events.Contains("stop-main"),"failed residency proof closes main before helper");
        x=Fresh();var changed=new Dictionary<string,object>(options);changed["num_ctx"]=8192;
        Check(x.Handoff(c,YQLlmBackend.Ollama,p,changed)&&x.Events.Contains("stop-main"),"context growth cannot reuse old placement proof");
        x=Fresh();changed["num_gpu"]=-1;Check(x.Handoff(c,YQLlmBackend.Ollama,p,changed)&&x.Events.Contains("stop-main"),"explicit GPU placement closes main");
        x=Fresh();var other=new LLMGenerationProfile { ollamaModel="other" };
        Check(x.Handoff(c,YQLlmBackend.Ollama,other,options)&&x.Events.IndexOf("release-helper")<x.Events.IndexOf("stop-main")&&x.Events.Contains("stop-main"),"different helper unload order retained");
        x=Fresh();x.ReleaseSucceeds=false;Check(!x.Handoff(c,YQLlmBackend.Ollama,other,options)&&!x.Events.Contains("stop-main"),"failed helper unload blocks replacement");
        x=Fresh();x._llamaServer.StopSucceeds=false;x._residentOllamaRequestedCpuOnly=false;
        Check(!x.Handoff(c,YQLlmBackend.Ollama,p,options),"failed owned process exit blocks handoff");
        x=Fresh();x._ownedLlamaResidencyKey="old-policy";Check(x.Handoff(c,YQLlmBackend.Ollama,p,options)&&x.Events.Contains("stop-main"),"changed GPU policy replaces main");
        x=Fresh();c.retainVerifiedCpuHelpers=false;Check(x.Handoff(c,YQLlmBackend.Ollama,p,options)&&x.Events.Contains("stop-main"),"retention rollback switch restores serial behavior");
    }
    static void PacketsAndLaunch()
    {
        var c=LLMRuntimeConfig.CreateRuntimeDefault();var p=c.GetProfile(LLMGenerationCategory.Summarization);var x=new ClientUnderTest(c);
        Check(c.GetBackend(LLMGenerationCategory.Summarization)==YQLlmBackend.LlamaCpp &&
            c.GetBackend(LLMGenerationCategory.Dialogue)==YQLlmBackend.Ollama,"default summaries use qualified control while dialogue keeps its specialist route");
        var raw=new Dictionary<string,object>();var resolved=x.Options(c,p,raw);
        Check((int)resolved["num_gpu"]==0&&!raw.ContainsKey("num_gpu"),"summary role CPU placement resolved without mutating caller");
        Check((int)resolved["num_thread"]<=4&&(int)resolved["num_batch"]==128,"cooperative CPU and batch limits preserved");
        Check((int)x.Options(c,p,new Dictionary<string,object>{{"num_gpu",-1}})["num_gpu"]==-1,"caller GPU placement overrides CPU role default");
        JObject packet=x.Packet(c,YQLlmBackend.Ollama,p,resolved);
        Check(JToken.DeepEquals(packet["options"],JObject.FromObject(resolved)),"wire uses exactly the admitted options");
        Check((string)packet["prompt"]=="entire current prompt","current prompt retained whole");
        Check(!(bool)x.Packet(c,YQLlmBackend.LlamaCpp,p,resolved)["cache_prompt"],"packet defaults to no unverified external cache");
        string help="--model --host --port --ctx-size --parallel --threads --threads-batch --batch-size --ubatch-size --prio --poll --cache-ram --no-cache-prompt --no-cache-idle-slots --n-gpu-layers --flash-attn --fit --fit-target --reasoning";
        string args=LlamaCppServerProcess.Arguments(c,help);
        Check(args.Contains("--cache-ram \"0\"")&&args.Contains("--no-cache-idle-slots")&&!args.Contains("--no-cache-prompt"),"reuse keeps host snapshots disabled");
        Check(args.Contains("--threads \"4\"")&&args.Contains("--batch-size \"128\"")&&args.Contains("--ubatch-size \"32\""),"reuse retains rendering protections");
        c.enablePromptPrefixReuse=false;Check(LlamaCppServerProcess.Arguments(c,help).Contains("--no-cache-prompt"),"cache rollback switch honored");
        c.enablePromptPrefixReuse=true;c.serverParallelSlots=2;Check(!LlamaCppServerProcess.CanUseBoundedPromptReuse(c,help),"multiple slots excluded");
        c.serverParallelSlots=1;c.extraLlamaServerArguments="--cache-ram 8192";Check(!LlamaCppServerProcess.CanUseBoundedPromptReuse(c,help),"custom launch overrides excluded");
        c.extraLlamaServerArguments="";Check(!LlamaCppServerProcess.CanUseBoundedPromptReuse(c,"--model --no-cache-prompt"),"missing RAM cache control excluded");
        Check(!LlamaCppServerProcess.CanUseBoundedPromptReuse(c,"HELP_PROBE_FAILED: --cache-ram"),"failed binary probe excluded");
        Check(!LlamaCppServerProcess.CanUseBoundedPromptReuse(c,"--cache-ram"),"missing slot-count control excludes reuse");
        // note: Rendering pressure selected this CPU path in PlaySafe; it must retain bounded reuse without any GPU allocation flags.
        c.gpuLayerCount=0;
        string cpuHelp=help+" --device --no-op-offload --no-kv-offload";
        string cpuArgs=LlamaCppServerProcess.Arguments(c,cpuHelp,true);
        Check(cpuArgs.Contains("--device \"none\"")&&cpuArgs.Contains("--no-op-offload")&&cpuArgs.Contains("--no-kv-offload"),"live CPU placement excludes device, operation and KV offload");
        Check(cpuArgs.Contains("--fit off")&&!cpuArgs.Contains("--fit on"),"CPU launch has one unambiguous fit policy");
        Check(LlamaCppServerProcess.CanUseBoundedPromptReuse(c,cpuHelp)&&cpuArgs.Contains("--cache-ram \"0\"")&&!cpuArgs.Contains("--no-cache-prompt"),"owned CPU policy retains bounded prefix reuse");
        bool unsupportedRejected=false;try{LlamaCppServerProcess.Arguments(c,help,true);}catch(InvalidOperationException){unsupportedRejected=true;}catch(Exception){unsupportedRejected=true;}
        Check(unsupportedRejected,"unknown CPU offload controls fail closed");
        c.preserveGameResponsiveness=false;Check(LlamaCppServerProcess.Arguments(c,help).Contains("--cache-ram \"0\""),"RAM bound independent of cooperative scheduling");
    }
    static void Main(string[] args)
    {
        if(args.Length==3 && args[0]=="wire")
        {
            // note: Export the actual production launch/packet methods for a private live server; only the process/network owner is detached.
            JObject input=JObject.Parse(File.ReadAllText(args[1]));var c=LLMRuntimeConfig.CreateRuntimeDefault();
            JsonConvert.PopulateObject(input["config"].ToString(),c);
            bool cpu=(bool?)input["cpu"]==true;
            var p=c.GetProfile(cpu?LLMGenerationCategory.Summarization:LLMGenerationCategory.StructuredState);
            var x=new ClientUnderTest(c);var options=input["options"].ToObject<Dictionary<string,object>>();
            if(cpu)options=x.Options(c,p,options);
            JObject packet=x.PacketFor(c,cpu?YQLlmBackend.Ollama:YQLlmBackend.LlamaCpp,p,options,
                (string)input["prompt"],input["schema"].ToObject<Dictionary<string,object>>());
            File.WriteAllText(args[2],JsonConvert.SerializeObject(new {packet,arguments=LlamaCppServerProcess.Arguments(c,(string)input["help"])}));return;
        }
        if(args.Length==3 && args[0]=="admit")
        {
            JObject input=JObject.Parse(File.ReadAllText(args[1]));
            bool allowed=YQLlmResidencyPolicy.CanRetainCpu(input["residency"].ToString(),"small-test:latest",2048,
                YQLlmResidencyPolicy.AvailableSystemMemoryBytes(),4096,(long?)input["additionalBytes"]??0);
            File.WriteAllText(args[2],JsonConvert.SerializeObject(new {allowed}));return;
        }
        Policies();Handoffs();PacketsAndLaunch();
        string result=JsonConvert.SerializeObject(new { status="PASS",checks=passed.Count,passed,
            evidence="Linked production policy/config and exact transport/handoff/launch members; fake Unity/network/process boundaries" },Formatting.Indented);
        if(args.Length>0)File.WriteAllText(args[0],result);
        Console.WriteLine("PASS: "+passed.Count+" detached latency-policy checks");
    }
}
