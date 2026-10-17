using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

internal static class GenerationTests
{
    private static void Main(string[] args)
    {
        if (args[0] == "fixtures")
        {
            // note: Exercise the current production prompt/schema using detached evidence, without a save mutation.
            string recall = JsonConvert.SerializeObject(new {
                npc_profile = new { name = "Mara", role = "ferryman", temperament = "plainspoken and cautious" },
                situation_snapshot = new { bridge_status = "unknown", ferry_status = "unknown" },
                current_objective = "No active quest.",
                recent_dialogue = YQDialogueMemorySelection.Compose("player: I left my sword beside the chapel altar.",
                    "player: Where did I tell you I left my sword?"),
                conversation_facts = new { grants = new string[0], completed_deeds = new string[0] }
            });
            string offer = JsonConvert.SerializeObject(new {
                npc_profile = new { name = "Mara", role = "ferryman", temperament = "plainspoken and cautious" },
                situation_snapshot = new { presented_offer = "Carry a sealed letter to the mill.", accepted = false },
                current_objective = "No active quest.", recent_dialogue = "npc: Will you carry this letter to the mill?",
                conversation_facts = new { grants = new string[0], completed_deeds = new string[0] }
            });
            var fixtures = new[] {
                new { id = "attributed_memory", prompt = YQDialogueGrounding.BuildGeneratePrompt(recall, "Where did I tell you I left my sword?"), schema = YQDialogueGrounding.ReplySchema(220), expectedDecision = "none" },
                new { id = "polite_offer_refusal", prompt = YQDialogueGrounding.BuildGeneratePrompt(offer, "No thank you, I cannot help today.", true), schema = YQDialogueGrounding.ReplySchema(220, true), expectedDecision = "decline" }
            };
            File.WriteAllText(args[1], JsonConvert.SerializeObject(fixtures, Formatting.Indented));
            return;
        }
        foreach (JObject row in JArray.Parse(File.ReadAllText(args[1])))
        {
            string raw = row.Value<string>("raw");
            if (!YQDialogueGrounding.TryValidateEnvelope(raw, out string error)) throw new Exception(error);
            if (YQDialogueGrounding.ReadOfferDecision(raw) != row.Value<string>("expectedDecision")) throw new Exception("Offer choice changed");
            Console.WriteLine("PASS production dialogue envelope and offer intent: " + row.Value<string>("id"));
        }
    }
}
