using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: This detached check uses the real compiled PlayerState and history classes with the production JSON converters, never an active save.
public static class StateRoundtrip
{
    public static void Main()
    {
        var checks = new List<object>();
        void Check(string name, bool pass) { checks.Add(new { name, pass }); if (!pass) throw new InvalidOperationException(name); }
        var settings = new JsonSerializerSettings { Formatting = Formatting.Indented, ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } };
        const string legacy = "{\"schemaVersion\":8,\"playerId\":\"detached-history-player\",\"displayName\":\"Preserved Player\",\"stateRevision\":27,\"currency\":17," +
            "\"activeQuestId\":\"preserved-quest\",\"quests\":[{\"questId\":\"preserved-quest\",\"status\":\"active\",\"name\":\"Preserved Quest\",\"objectives\":[{\"objectiveId\":\"objective-kept\",\"description\":\"Ask the known archivist\",\"completed\":false}]}]," +
            "\"inventoryItems\":[{\"itemId\":\"item-kept\",\"displayName\":\"Preserved Item\",\"quantity\":3}],\"generatedOrigin\":{\"originId\":\"origin-kept\",\"className\":\"Preserved Class\"}," +
            "\"goddessVoiceMemory\":[\"An earlier delivered sentence remains available.\"]}";
        PlayerState player = JsonConvert.DeserializeObject<PlayerState>(legacy, settings);
        player.EnsureCollections();
        Check("real legacy state accepts absent history without schema change", player.schemaVersion == 8 && player.goddessDeliveredSpeechHashes.Count == 0);
        JObject before = JObject.Parse(JsonConvert.SerializeObject(player, settings));
        Check("real legacy sentence cannot be reissued with a different tail", !YQGoddessSpeechHistory.IsNovel(player,
            "An earlier delivered sentence remains available. A fresh ending follows.", out _));
        string snapshot = null;
        Check("real history participates in the normal player JSON document", YQGoddessSpeechHistory.TryRecord(player,
            "Your decision about the ledger deserves a closer look.", () => { snapshot = JsonConvert.SerializeObject(player, settings); return true; }, out _));
        PlayerState restored = JsonConvert.DeserializeObject<PlayerState>(snapshot, settings); restored.EnsureCollections();
        Check("real roundtrip retains hashes", !YQGoddessSpeechHistory.IsNovel(restored, "Your decision about the ledger deserves a closer look!", out _));
        Check("real roundtrip preserves accepted state", restored.schemaVersion == player.schemaVersion && restored.stateRevision == 27 && restored.currency == 17 &&
            restored.activeQuestId == "preserved-quest" && restored.quests[0].objectives[0].objectiveId == "objective-kept" && restored.inventoryItems[0].itemId == "item-kept" &&
            restored.inventoryItems[0].quantity == 3 && restored.generatedOrigin.originId == "origin-kept");
        JObject after = JObject.Parse(snapshot);
        before.Remove("goddessVoiceMemory"); before.Remove("goddessDeliveredSpeechHashes");
        after.Remove("goddessVoiceMemory"); after.Remove("goddessDeliveredSpeechHashes");
        Check("only presentation history changes in serialized state", JToken.DeepEquals(before, after));
        string preimage = JsonConvert.SerializeObject(restored, settings);
        Check("real failed commit restores serialized history", !YQGoddessSpeechHistory.TryRecord(restored, "A different concern calls for patience.", () => false, out _) &&
            JsonConvert.SerializeObject(restored, settings) == preimage);
        File.WriteAllText("outputs/GoddessGenerativeRepair_20261007/StateRoundtrip.json", JsonConvert.SerializeObject(new {
            utc = DateTime.UtcNow, evidence = "Detached real compiled PlayerState/history and production converter roundtrip; no active profile, file publication or Unity UI claim",
            assemblyMvid = typeof(PlayerState).Module.ModuleVersionId, checks }, Formatting.Indented));
        Console.WriteLine(checks.Count + " real-state detached roundtrip checks PASS");
    }
}
