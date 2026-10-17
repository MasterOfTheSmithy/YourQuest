using System;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

// note: Exercise the production policy directly on detached inputs; never select a profile or mutate a Unity save.
internal static class Program
{
    private static int checks;
    private static void Require(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }

    private static void Main(string[] args)
    {
        var boundaries = new Dictionary<string, int>();
        for (int i = 0; i < 10000; i++)
        {
            string tier = YQItemAuthoringPolicy.RarityForBucket(i);
            boundaries[tier] = boundaries.GetValueOrDefault(tier) + 1;
        }
        Require(boundaries["Common"] == 4400 && boundaries["Uncommon"] == 2000 && boundaries["Rare"] == 3000 &&
            boundaries["Epic"] == 500 && boundaries["Legendary"] == 100, "Incorrect rarity intervals.");

        var sample = new Dictionary<string, int>();
        var forward = new string[100000];
        for (int i = 0; i < forward.Length; i++)
        {
            string seed = "origin:player-" + i + ":merchant:weapon:0";
            string tier = YQItemAuthoringPolicy.RollRarity(seed);
            forward[i] = tier;
            sample[tier] = sample.GetValueOrDefault(tier) + 1;
        }
        for (int i = forward.Length - 1; i >= 0; i--)
            Require(forward[i] == YQItemAuthoringPolicy.RollRarity("origin:player-" + i + ":merchant:weapon:0"), "Order-dependent rarity.");
        Require(Math.Abs(sample["Rare"] / 100000.0 - .30) < .006 && Math.Abs(sample["Epic"] / 100000.0 - .05) < .003 &&
            Math.Abs(sample["Legendary"] / 100000.0 - .01) < .0015, "Unexpected hash distribution.");

        string normalSeed = "normal";
        string rolled = YQItemAuthoringPolicy.RollRarity(normalSeed);
        string forced = rolled == "Legendary" ? "Epic" : "Legendary";
        string mythic = "I'm the legendary god Osiris hailing from the battle of the Nephalem with the all seeing eyes.";
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "trinket", "Legendary", mythic, new[] { mythic }) == "Legendary", "Mythic identity ignored.");
        string itemClaim = "I carry a " + forced + " sword called Nightfall.";
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, itemClaim, new[] { itemClaim }) == forced, "Explicit item ignored.");
        string namedItem = "I carry a sword named Dawnfire, forged from the sun.";
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, namedItem, new[] { namedItem }) == forced, "Specific named item ignored.");
        string plainItem = "I carry a sword on my travels.";
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, plainItem, new[] { plainItem }) == rolled, "Ordinary unnamed item upgraded.");
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "boots", forced, itemClaim, new[] { itemClaim }) == rolled, "Special sword upgraded unrelated boots.");
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, itemClaim, new[] { "I am a merchant." }) == rolled, "Fabricated evidence upgraded gear.");
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, "", new[] { mythic }) == rolled, "Missing evidence upgraded gear.");
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", "Mythical", mythic, new[] { mythic }) == rolled, "Unsupported rarity accepted.");
        foreach (string ambition in new[] { "I want to become a legendary god.", "I worship a legendary god.", "I'm not a legendary god.", "I hope to be an immortal one day.", "I am a merchant who prays to a god." })
            Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, ambition, new[] { ambition }) == rolled, "Ordinary aspiration upgraded gear.");
        string denied = "I do not own a " + forced + " sword called Nightfall.";
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, forced + " sword called Nightfall", new[] { denied }) == rolled, "Partial quote removed negation.");
        Require(YQItemAuthoringPolicy.ResolveRarity(normalSeed, "weapon", forced, itemClaim, new[] { "I carry a " + forced, "sword called Nightfall." }) == rolled, "Evidence crossed field boundaries.");

        Require(YQItemAuthoringPolicy.IsUsableHint("weapon", "Nightfall", "A dark steel sword carried through the siege of Nephalem."), "Proper name rejected.");
        Require(YQItemAuthoringPolicy.IsUsableHint("boots", "Merchant's Soles", "Leather boots with reinforced heels for long market journeys."), "Concrete footwear rejected.");
        Require(YQItemAuthoringPolicy.IsUsableHint("chest", "Frostglass Cuirass", "A fitted cuirass etched with the sigil of your house."), "Authored fantasy material rejected.");
        Require(!YQItemAuthoringPolicy.IsUsableHint("boots", "Dawn Sword", "A sharp blade carried through the siege."), "Wrong slot accepted.");
        Require(!YQItemAuthoringPolicy.IsUsableHint("weapon", "Blade", "A weapon for your current build and fighting rhythm."), "Generic authoring accepted.");
        Require(!YQItemAuthoringPolicy.IsUsableHint("weapon", "<b>Blade</b>", "A well-balanced sword with a wrapped hilt."), "Markup accepted.");
        Require(!YQItemAuthoringPolicy.IsUsableHint("weapon", "Sword Sword Sword", "A well-balanced sword with a wrapped hilt."), "Repetition accepted.");
        bool integration = args.Length == 1;
        if (integration) CheckCompiledContracts(args[0]);
        Console.WriteLine(JsonSerializer.Serialize(new { status = "PASS", checks, compiledContractsChecked = integration, exactBuckets = boundaries, sampledItems = forward.Length,
            sampledCounts = sample, evidence = "Detached production-policy regression; no live model or Unity runtime claim" }));
    }

    private static void CheckCompiledContracts(string folder)
    {
        // note: Load actual compiled schema/DTO/validator methods, without constructing Unity objects or touching a live profile.
        string[] references = File.ReadAllLines(Path.Combine(folder, "references.txt"));
        AssemblyLoadContext.Default.Resolving += (context, name) =>
        {
            foreach (string reference in references)
                if (string.Equals(Path.GetFileNameWithoutExtension(reference), name.Name, StringComparison.OrdinalIgnoreCase))
                    return context.LoadFromAssemblyPath(reference);
            return null;
        };
        Assembly runtime = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(folder, "Assembly-CSharp.dll"));
        var schema = (Dictionary<string, object>)runtime.GetType("YQLlmJsonSchema").GetMethod("BuildOrigin").Invoke(null, null);
        var properties = (Dictionary<string, object>)schema["properties"];
        var loadout = (Dictionary<string, object>)properties["loadout"];
        var entry = (Dictionary<string, object>)loadout["items"];
        var fields = (Dictionary<string, object>)entry["properties"];
        string[] slots = (string[])((Dictionary<string, object>)fields["slot"])["enum"];
        Require(Array.IndexOf(slots, "chest") >= 0 && Array.IndexOf(slots, "cloak") >= 0 && Array.IndexOf(slots, "ring_left") >= 0,
            "Transport still forces weapon/offhand/boots.");
        Require(fields.ContainsKey("rarityHint") && fields.ContainsKey("intentEvidence"), "Missing evidence fields.");

        Type dto = runtime.GetType("YQOriginGeneratedItemDto");
        MethodInfo validate = runtime.GetType("YQOriginGenerationService").GetMethod("HasUsableGeneratedLoadout", BindingFlags.Static | BindingFlags.NonPublic);
        object Item(string slot, string name, string description)
        {
            object item = Activator.CreateInstance(dto);
            dto.GetField("slot").SetValue(item, slot);
            dto.GetField("nameHint").SetValue(item, name);
            dto.GetField("descriptionHint").SetValue(item, description);
            return item;
        }
        bool Validate(params object[] items)
        {
            Array array = Array.CreateInstance(dto, items.Length);
            for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i);
            return (bool)validate.Invoke(null, new object[] { array });
        }
        object sword = Item("weapon", "Nightfall", "A dark steel sword carried through the siege of Nephalem.");
        object armor = Item("chest", "Frostglass Cuirass", "A fitted cuirass etched with the sigil of your house.");
        object boots = Item("boots", "Merchant's Soles", "Leather boots with reinforced heels for long market journeys.");
        Require(Validate(sword, armor, boots), "Usable legacy DTO hints without optional rarity fields rejected.");
        Require(!Validate(sword, sword, boots), "Duplicate slots and names accepted.");
        Require(!Validate(sword, armor, Item("boots", "Nightfall", "Leather boots with reinforced heels for long market journeys.")), "Duplicate name accepted across slots.");
        Require(!Validate(sword, armor, Item("boots", "Dawn Sword", "A sharp blade carried through the siege.")), "Wrong-form item accepted by origin parser.");
        Require(!Validate(sword, boots, Item("consumable", "Road Tonic", "A tonic that restores health, stamina, and mana.")), "Consumable counted as third equipment slot.");
    }
}
