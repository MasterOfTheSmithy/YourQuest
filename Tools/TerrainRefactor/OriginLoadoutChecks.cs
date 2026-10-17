using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

// note: Exercise the actual extracted pure schema/merge owners without a model, Unity, or persistent profile writes.
internal static class OriginLoadoutChecks
{
    private static int checks, failures;
    private static void Check(string label, bool ok)
    {
        checks++;
        if (!ok) failures++;
        Console.WriteLine((ok ? "PASS " : "FAIL ") + label);
    }

    public static int Main()
    {
        var schema = YQOriginLoadoutRepair.BuildSchema();
        var properties = (Dictionary<string, object>)schema["properties"];
        var loadout = (Dictionary<string, object>)properties["loadout"];
        var item = (Dictionary<string, object>)loadout["items"];
        var itemProperties = (Dictionary<string, object>)item["properties"];
        Check("repair grammar allows only the required loadout field", (string)schema["type"] == "object" &&
            !(bool)schema["additionalProperties"] && properties.Count == 1 &&
            ((string[])schema["required"]).SequenceEqual(new [] { "loadout" }));
        Check("repair uses the canonical origin loadout grammar", JToken.DeepEquals(JToken.FromObject(loadout),
            JToken.FromObject(((Dictionary<string, object>)YQLlmJsonSchema.BuildOrigin()["properties"])["loadout"])));
        Check("grammar requires exactly three equipped items", (string)loadout["type"] == "array" &&
            (int)loadout["minItems"] == 3 && (int)loadout["maxItems"] == 3);
        Check("item grammar rejects undeclared fields", !(bool)item["additionalProperties"] && itemProperties.Count == 5);
        Check("grammar requires every canonical item field", ((string[])item["required"]).OrderBy(x => x)
            .SequenceEqual(itemProperties.Keys.OrderBy(x => x)));
        Check("item fields retain string types", itemProperties.Values.Cast<Dictionary<string, object>>()
            .All(x => (string)x["type"] == "string"));
        var slots = (string[])((Dictionary<string, object>)itemProperties["slot"])["enum"];
        Check("all fifteen equipped slots remain available without consumables", slots.Length == 15 &&
            slots.Contains("weapon") && slots.Contains("trinket") && !slots.Contains("consumable"));
        Check("ordinary and explicit rarity intent remain available", ((string[])((Dictionary<string, object>)itemProperties["rarityHint"])["enum"])
            .SequenceEqual(new [] { "", "Common", "Uncommon", "Rare", "Epic", "Legendary" }));
        loadout["minItems"] = 99;
        Check("schema calls do not share mutable grammar", (int)((Dictionary<string, object>)
            ((Dictionary<string, object>)YQOriginLoadoutRepair.BuildSchema()["properties"])["loadout"])["minItems"] == 3);

        JObject original = JObject.Parse("{\"titleName\":\"Keeper\",\"className\":\"Surveyor\",\"ability\":{\"name\":\"Measure\"},\"quest\":{\"name\":\"Ask\"},\"loadout\":[]}");
        string before = original.ToString(Formatting.None);
        var entries = new JArray();
        foreach (string slot in new [] { "weapon", "offhand", "boots" })
            entries.Add(new JObject { ["slot"] = slot, ["nameHint"] = "Survey " + slot,
                ["descriptionHint"] = "Leather and wood equipment fitted for measuring riverbanks.",
                ["rarityHint"] = "", ["intentEvidence"] = "" });
        string valid = new JObject { ["loadout"] = entries }.ToString(Formatting.None);
        Check("valid bounded loadout patch merges", YQOriginLoadoutRepair.TryMerge(original, valid, out string merged, out _));
        JObject expected = (JObject)original.DeepClone(); expected["loadout"] = entries.DeepClone();
        Check("merge preserves every non-loadout field", merged != null && JToken.DeepEquals(expected, JObject.Parse(merged)));
        Check("merge preserves source proposal", original.ToString(Formatting.None) == before);
        Check("merge clones replacement items", merged != null && JToken.DeepEquals(JObject.Parse(merged)["loadout"], entries));
        foreach (string raw in new [] { valid + "}", valid + " trailing", valid + valid,
            "{\"loadout\":[],\"loadout\":[]}", "{\"loadout\":[{\"slot\":\"weapon\",\"slot\":\"boots\"}]}",
            "{\"loadout\":[],\"className\":\"Changed\"}", "{\"loadout\":null}", "{\"loadout\":{}}", "{}" })
            Check("malformed or out-of-scope patch rejected: " + raw.Substring(0, Math.Min(55, raw.Length)),
                !YQOriginLoadoutRepair.TryMerge(original, raw, out _, out _));
        Check("missing source proposal rejected", !YQOriginLoadoutRepair.TryMerge(null, valid, out _, out _));
        string prompt = YQOriginLoadoutRepair.BuildPrompt("Player evidence", original);
        Check("repair prompt matches equipped three-item grammar", prompt.Contains("exactly three") &&
            prompt.Contains("earring_left, earring_right, necklace, trinket.") && !prompt.Contains("trinket, consumable"));
        Console.WriteLine("RESULT checks=" + checks + " failures=" + failures);
        return failures == 0 ? 0 : 1;
    }
}
