using System;
using System.IO;
using Newtonsoft.Json.Linq;

// note: Supply only the request-binding fields needed by the linked episode class; no runtime state or scheduler is instantiated.
public class YQLlmRequest
{
    public string profileId,worldId,ownerId;
    public int generationEpoch;
    public long playerStateRevision,worldStateRevision;
    public YQRepairEpisode repairEpisode;
    public bool repairVerification,protectPrompt;
}
internal static class Program
{
    private static void Main(string[] args)
    {
        JObject input=JObject.Parse(File.ReadAllText(args[1]));var output=new JObject();
        var proposal=(JObject)input["proposal"];
        if(args[0]=="normalize")
        {
            output["valid"]=CurrentTransport.Normalize((string)input["raw"],out string normalized,out string error);
            output["normalized"]=normalized;output["diagnostic"]=error;
        }
        else if(args[0]=="check")
        {
            output["valid"]=CurrentOrigin.Check(proposal.ToObject<YQOriginGenerationDto>(),out string error);
            output["diagnostic"]=error;
        }
        else if(args[0]=="prompt")
        {
            string basePrompt=YQOriginLoadoutRepair.BuildPrompt((string)input["originalPrompt"],proposal);
            output["prompt"]=YQRepairEpisode.RepairPrompt(basePrompt,(string)input["diagnostic"],(string)input["previous"]);
        }
        else if(args[0]=="merge")
        {
            // note: The production repair request normalizes the transport before its allowlisted merge.
            bool normalized=CurrentTransport.Normalize((string)input["repair"],out string repair,out string error);
            string candidate=null;
            bool merged=normalized && YQOriginLoadoutRepair.TryMerge(proposal,repair,out candidate,out error);
            bool valid=merged && CurrentOrigin.Check(JObject.Parse(candidate).ToObject<YQOriginGenerationDto>(),out error);
            output["merged"]=candidate;output["valid"]=valid;output["diagnostic"]=error;
        }
        else throw new ArgumentException("Unknown operation");
        File.WriteAllText(args[2],output.ToString());
    }
}
