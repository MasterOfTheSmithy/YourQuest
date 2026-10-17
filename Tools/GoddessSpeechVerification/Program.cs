using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: Detached fixtures exercise current speech composition and rejection without selecting a profile or publishing state.
public static class Program
{
    const string Folder = "outputs/GoddessSpeechRepair_20261007";
    static readonly List<object> checks = new List<object>();
    static void Check(string name, bool pass) { checks.Add(new { name, pass }); if (!pass) { Console.Error.WriteLine("FAIL: " + name); Environment.ExitCode = 1; } }
    static string Plan(string key, string aside = "", int delivery = 0, string correction = "") => new JObject {
        ["addressPlayer"] = false, ["claims"] = new JArray(key), ["delivery"] = delivery, ["aside"] = aside, ["correction"] = correction
    }.ToString(Formatting.None);
    public static void Main(string[] args)
    {
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        var cases = (JArray)JObject.Parse(File.ReadAllText("outputs/LlmRepair_20261005/VoiceCases.json"))["cases"];
        if (args[0] == "export")
        {
            var evidence = (JObject)cases[0]["evidence"].DeepClone();
            evidence["priorSpeech"] = new JArray();
            Check("welcome requires known vow", !YQGoddessSpeechPlan.TryCompose(Plan("origin.class", "I am glad to meet you."), evidence, out _, out _, YQGoddessSpeechPlan.Purpose.Welcome));
            Check("invented key rejected", !YQGoddessSpeechPlan.TryCompose(Plan("world.secret"), evidence, out _, out _));
            Check("invented personal fact rejected", !YQGoddessSpeechPlan.TryCompose(Plan("player.vow", "I know your family is safe."), evidence, out _, out _));
            Check("old incoherent adjective combination rejected", !YQGoddessSpeechPlan.TryCompose(Plan("player.vow", "I would call that temper, if I were being absurdly specific."), evidence, out _, out _));
            Check("standalone fact accepted", YQGoddessSpeechPlan.TryCompose(Plan("player.vow"), evidence, out string factOnly, out _));
            Check("standalone fact delivery proven", YQGoddessSpeechPlan.TryReadSpokenComposition(factOnly, evidence, out _));
            Check("added clause rejected", !YQGoddessSpeechPlan.TryReadSpokenComposition(factOnly + " Your family is safe.", evidence, out _));
            Check("empty aside cannot carry correction", !YQGoddessSpeechPlan.TryCompose(Plan("player.vow", "", 0, "I should be more precise."), evidence, out _, out _));
            Check("guidance uses objective", YQGoddessSpeechPlan.TryCompose(Plan("objective.description", "I would begin there."), evidence, out string guidance, out _, YQGoddessSpeechPlan.Purpose.Guidance));
            var recent = (JObject)evidence.DeepClone(); recent["priorSpeech"] = new JArray(guidance);
            Check("recent aside family rejected across fact delivery changes", !YQGoddessSpeechPlan.TryCompose(Plan("objective.description", "I would begin there.", 2), recent, out _, out _, YQGoddessSpeechPlan.Purpose.Guidance));
            Check("explicit guidance can repeat objective without gag", YQGoddessSpeechPlan.TryCompose(Plan("objective.description"), recent, out _, out _, YQGoddessSpeechPlan.Purpose.Guidance));
            Check("ambient curation cannot rephrase recent objective", !YQGoddessSpeechPlan.TryCompose(Plan("objective.description", "I will leave the decision with you.", 2), recent, out _, out _, YQGoddessSpeechPlan.Purpose.Curation));
            Check("curation can choose a different accepted fact", YQGoddessSpeechPlan.TryCompose(Plan("player.level", "I will leave the decision with you."), recent, out _, out _, YQGoddessSpeechPlan.Purpose.Curation));
            var changed = (JObject)recent.DeepClone(); changed["facts"]["objective.description"] = "Ask Vey about a different accepted task";
            Check("changed objective available", YQGoddessSpeechPlan.TryCompose(Plan("objective.description", "I will leave the decision with you."), changed, out _, out _, YQGoddessSpeechPlan.Purpose.Curation));
            var exhausted = new JObject { ["facts"] = new JObject { ["player.vow"] = evidence["facts"]["player.vow"].DeepClone() }, ["priorSpeech"] = new JArray(factOnly) };
            Check("exhausted ambient facts are quiet", !YQGoddessSpeechPlan.HasAvailableClaims(exhausted, YQGoddessSpeechPlan.Purpose.Curation));
            var quoted = (JObject)evidence.DeepClone(); quoted["priorSpeech"] = new JArray("Your vow is “Your next task is “Ask Archivist Vey about the missing ferry ledger”.”. I take that seriously.");
            Check("quoted player text cannot consume another fact", YQGoddessSpeechPlan.TryCompose(Plan("objective.description", "I would begin there."), quoted, out _, out _, YQGoddessSpeechPlan.Purpose.Curation));
            var identity = (JObject)evidence.DeepClone(); identity["facts"]["player.vow"] = "a different vow";
            Check("other player's fact fails delivery proof", !YQGoddessSpeechPlan.TryReadSpokenComposition(factOnly, identity, out _));
            foreach (JObject item in cases)
            {
                var purpose = (YQGoddessSpeechPlan.Purpose)item.Value<int>("purpose");
                string statement = item.Value<string>("statement");
                item["payload"]["prompt"] = YQGoddessSpeechPlan.BuildPrompt((JObject)item["evidence"], item.Value<string>("name"), purpose, statement);
                item["payload"]["format"] = JObject.FromObject(YQGoddessSpeechPlan.Schema((JObject)item["evidence"], purpose, statement));
            }
            // note: Exercise continuity using a current-contract composed line, rather than a stale fixture's retired grammar.
            foreach (JObject item in cases) if (item.Value<string>("name") == "continuity") {
                item["evidence"]["priorSpeech"] = new JArray(guidance);
                item["payload"]["prompt"] = YQGoddessSpeechPlan.BuildPrompt((JObject)item["evidence"], "Offer useful new commentary", YQGoddessSpeechPlan.Purpose.Curation);
                item["purpose"] = (int)YQGoddessSpeechPlan.Purpose.Curation;
                item["payload"]["format"] = JObject.FromObject(YQGoddessSpeechPlan.Schema((JObject)item["evidence"], YQGoddessSpeechPlan.Purpose.Curation));
            }
            File.WriteAllText(Folder + "/Cases.json", new JObject { ["contractHash"] = config.GoddessSpeechPlanContractHash, ["cases"] = cases }.ToString());
        }
        else
        {
            var exported = JObject.Parse(File.ReadAllText(Folder + "/Cases.json"));
            var replies = JArray.Parse(File.ReadAllText(Folder + "/Replies.json"));
            foreach (JObject item in (JArray)exported["cases"])
            {
                JObject reply = null; foreach (JObject candidate in replies) if (candidate.Value<string>("name") == item.Value<string>("name")) reply = candidate;
                string line = "", error = "no reply";
                bool pass = reply != null && reply.Value<string>("error") == null && reply.Value<string>("digest") == config.goddessSpeechPlanModelDigest &&
                    YQGoddessSpeechPlan.TryCompose(reply.Value<string>("raw"), (JObject)item["evidence"], out line, out error,
                    (YQGoddessSpeechPlan.Purpose)item.Value<int>("purpose"), item.Value<string>("statement"));
                checks.Add(new { name = item.Value<string>("name"), pass, line, error });
                if (!pass) Environment.ExitCode = 1;
                Console.WriteLine(item.Value<string>("name") + ": " + (pass ? line : "FAIL " + error));
            }
            Check("production qualification pin matches fresh contract", config.HasQualifiedGoddessSpeechPlan);
        }
        File.WriteAllText(Folder + (args[0] == "export" ? "/PureChecks.json" : "/ModelReview.json"), JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow, evidence = "Detached current-source speech checks; no Unity presentation or audio claim", contractHash = config.GoddessSpeechPlanContractHash, checks
        }, Formatting.Indented));
    }
}
