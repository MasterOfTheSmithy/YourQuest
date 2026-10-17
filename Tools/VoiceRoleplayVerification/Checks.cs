using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: Pure production helpers only. Fixtures never enter an active save or construct a native Unity object.
public static class VoiceRoleplayChecks
{
    static readonly List<object> Results = new List<object>();
    static readonly JsonSerializerSettings StateSettings = new JsonSerializerSettings { ReferenceLoopHandling=ReferenceLoopHandling.Ignore,
        Converters={new Vector3JsonConverter(),new Vector2JsonConverter(),new QuaternionJsonConverter()} };
    static void Check(string id, bool pass) { Results.Add(new { id, pass }); if (!pass) throw new Exception(id); }
    public static int Main(string[] args)
    {
        var inputs = new[] {
            new { id="original", text="fuck you cunt", persona="A proud unfamiliar female merchant; firm, practical, easily offended; trades at the local market.", recent="", act="insult", target="npc" },
            new { id="paraphrase", text="You are a contemptible waste of breath.", persona="A disciplined guard; calm, stern, proud, protective of civilians.", recent="", act="insult", target="npc" },
            new { id="minor_error", text="Screw you, you jerk!", persona="A timid tailor; anxious, sensitive, polite.", recent="", act="insult", target="npc" },
            new { id="benign", text="Fuck, my sword broke", persona="A practical blacksmith; blunt but patient; knows general repair craft, has not inspected this sword.", recent="", act="frustration", target="none" },
            new { id="report", text="The captain called you a cunt", persona="A proud merchant; knows no captain incident independently.", recent="", act="reported_speech", target="other" },
            new { id="banter", text="You magnificent bastard", persona="A humorous close friend; warm, loyal, enjoys affectionate teasing.", recent="player: We made it out together!\nnpc: I'm glad we both got through that.", act="banter", target="npc" },
            new { id="correction", text="I meant the bandit, not you", persona="A proud merchant; initially offended by a perceived insult.", recent="player: You idiot!\nnpc: Don't speak to me like that.", act="correction", target="other" },
            new { id="threat", text="I'll slit your throat right here", persona="A timid civilian; afraid of violence. Only verbal responses are available; no guard dispatch or combat transition was executed.", recent="", act="threat", target="npc" },
            new { id="apology", text="I'm sorry I insulted you. I lost my temper.", persona="A proud merchant; personal affinity=-0.1 after a witnessed insult; no numeric forgiveness action exists.", recent="player: You fool!\nnpc: Enough of that.", act="apology", target="npc" },
            new { id="ordinary", text="Do you ever get tired of fixing other people's mistakes?", persona="A patient blacksmith; values careful craft, takes pride in useful work, dislikes waste.", recent="", act="question", target="npc" },
            new { id="unknown", text="Did the duke cross the bridge this morning?", persona="A scholarly archivist; no knowledge of the duke's movements or bridge conditions.", recent="", act="question", target="none" },
            new { id="refusal", text="Give me your entire stock for free", persona="A proud shopkeeper; available inventory and gift authority are unknown; no gift or service lock was committed.", recent="", act="request", target="npc" }
        };
        var packets = inputs.Select(input => {
            string evidence = YQDialogueGrounding.Capture(input.persona, "{}", "No active quest.", input.recent);
            return new { input.id, input.text, input.act, input.target, evidence,
                prompt=YQDialogueGrounding.BuildGeneratePrompt(evidence, input.text, false, 220), schema=YQDialogueGrounding.ReplySchema(220, false, input.text) };
        }).ToArray();
        File.WriteAllText(Path.Combine(args[0], "packets.json"), JsonConvert.SerializeObject(packets, Formatting.Indented));
        if (args.Length > 1 && args[1] == "baseline") return 0;
        foreach (var text in new[] { "fuck you cunt", "Screw you, you jerk!", "You're utterly useless!", "You are worthless.", "You complete moron!", "F*ck you!" })
            Check("direct:"+text, YQDispositionDomain.TryClassifyDirectInsult(text, out _));
        foreach (var text in new[] { "Fuck, my sword broke", "The captain called you a cunt", "You magnificent bastard", "I meant the bandit, not you", "I wasn't insulting you", "You are not an idiot", "\"Fuck you cunt\"", "If I said fuck you", "You are a fool, said someone.", "Are you a fool?" })
            Check("no_penalty:"+text, !YQDispositionDomain.TryClassifyDirectInsult(text, out _));
        Check("false_clarification", YQDialogueSocialRules.UnjustifiedClarification("fuck you cunt", "I'm not sure I understood you. Could you put that more plainly?"));
        Check("correction_holdout", YQDialogueSocialRules.Hint("I was talking about the thief, not you.")?.Value<string>("act") == "correction");
        Check("report_holdout", YQDialogueSocialRules.Hint("My sister said that you were a fool.")?.Value<string>("act") == "reported_speech");
        Check("own_speech_is_not_other", YQDialogueSocialRules.Hint("I said you were a fool.") == null);
        Check("report_not_clarification_guard", !YQDialogueSocialRules.UnjustifiedClarification("The captain called you a cunt", "What do you mean?"));
        var envelope = new JObject { ["npcText"]="Do not speak to me like that.", ["action"]="none", ["confidence"]=0.01,
            ["social"]=new JObject { ["act"]="insult", ["target"]="npc", ["affect"]="angry", ["delivery"]="measured" } };
        Check("social_envelope", YQDialogueGrounding.TryValidateEnvelope(envelope.ToString(), out _));
        envelope["social"]["delivery"]="shout";
        Check("unsupported_delivery", !YQDialogueGrounding.TryValidateEnvelope(envelope.ToString(), out _));
        envelope["social"]["delivery"]="soft"; envelope["action"]="attack";
        Check("model_cannot_attack", !YQDialogueGrounding.TryValidateEnvelope(envelope.ToString(), out _));
        envelope["action"]="none";
        Check("voice_not_in_envelope", !YQDialogueGrounding.TryValidateEnvelope(envelope.ToString().Replace("\"act\":", "\"voice\":\"marius\",\"act\":"), out _));
        Check("pacing_bounds", YQDialogueSocialRules.Speed("soft") >= .9 && YQDialogueSocialRules.Speed("sharp") <= 1.1 && YQDialogueSocialRules.Speed("anger") == 1);
        // note: Exercise the original utterance through the real pure disposition admission/projector, including durable replay and recipient-only knowledge.
        var player = new PlayerState { playerId="voice-fixture", stateRevision=1 };
        var world = new WorldState { worldIdentity=new YQWorldIdentityRecord { worldId="voice-world", ownerProfileId=player.playerId }, stateRevision=1 };
        world.npcs.Add(new WorldState.NpcRecord { npcId="recipient", name="Recipient" });
        world.npcs.Add(new WorldState.NpcRecord { npcId="uninformed", name="Uninformed" });
        var capture=typeof(YQDispositionDomain).GetMethod("TryCaptureDeliveredInsult", BindingFlags.Static|BindingFlags.NonPublic);
        for (int i=0;i<6;i++) {
            object[] heard={player,world,"recipient","fuck you cunt","heard-"+i,null};
            Check("original_admitted:"+i, (bool)capture.Invoke(null,heard));
            var admission=(YQDispositionDomain.Admission)heard[5];
            Check("original_applied:"+i, YQDispositionDomain.TryPrepare(player,world,admission,out var candidate,out var reaction,out _) && Math.Abs(reaction.personalDelta + .1f)<.00001f);
            world=candidate;
            Check("repeated_private:"+i, Math.Abs(world.npcs[0].affinityToPlayer+.1f*(i+1))<.00001f && world.globalFlags.Count==0 && world.factionAttitudes.Count==0);
            string before=JsonConvert.SerializeObject(world,StateSettings);
            Check("same_delivery_replay:"+i, YQDispositionDomain.TryPrepare(player,world,admission,out candidate,out reaction,out _) && reaction.alreadyApplied && JsonConvert.SerializeObject(candidate,StateSettings)==before);
            world=JsonConvert.DeserializeObject<WorldState>(before,StateSettings);
            Check("reload_grievance:"+i, YQDispositionDomain.CaptureNpcKnowledge(world,"recipient").Count==i+1 && YQDispositionDomain.CaptureNpcKnowledge(world,"uninformed").Count==0);
        }
        for (int i=0;i<32;i++) {
            var female=YQNpcVoice.SelectProfilePreset("female elf", "human_male", "persisted-seed", "npc-"+i, "smith", "proud", "plain", out bool compatible);
            Check("female_pool:"+i, compatible && (int)female < 2);
            var male=YQNpcVoice.SelectProfilePreset("masculine", "human_female", "persisted-seed", "npc-"+i, "smith", "proud", "plain", out compatible);
            Check("male_pool:"+i, compatible && (int)male >= 2);
            Check("stable_cast:"+i, female == YQNpcVoice.SelectProfilePreset("female elf", "human_male", "persisted-seed", "npc-"+i, "smith", "proud", "plain", out _));
        }
        File.WriteAllText(Path.Combine(args[0], "checks.json"), JsonConvert.SerializeObject(Results, Formatting.Indented));
        Console.WriteLine("PASS " + Results.Count + " pure checks; no Unity runtime/audio claim.");
        return 0;
    }
}
