#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

// note: Shared cases call the real production selectors by reflection; this file contains assertions and fixture records only.
internal static class YQCanopyPaletteRepairCases
{
    public static readonly string[] ExpectedBaselineFailures = {
        "regional palette leads canopy", "accepted duplicate keeps reference metadata",
        "accepted input order precedes supplements", "case-insensitive duplicate keeps first accepted reference",
        "canopy round-robin starts and wraps through accepted prefix"
    };
    public static List<object> Run(Type environmentType, out int passed, out int failed)
    {
        var select = environmentType.GetMethod("BuildSmallScatterReferences", BindingFlags.Static | BindingFlags.NonPublic);
        var weighted = environmentType.GetMethod("PickWeightedReference", BindingFlags.Static | BindingFlags.NonPublic);
        if (select == null || weighted == null) throw new MissingMethodException("Actual production selector methods are unavailable.");
        var checks = new List<object>(); int yes = 0, no = 0;
        List<GeneratedAssetReferenceRecord> Select(GeneratedRegionAssetPaletteRecord p, string slot = "vegetation", bool trees = true)
            => (List<GeneratedAssetReferenceRecord>)select.Invoke(null, new object[] { p, slot, trees });
        void Check(string name, Action assertion) {
            try { assertion(); yes++; checks.Add(new { name, passed = true, failure = (string)null }); }
            catch (Exception ex) { no++; checks.Add(new { name, passed = false, failure = (ex.InnerException ?? ex).Message }); }
        }
        string[] Paths(IEnumerable<GeneratedAssetReferenceRecord> refs) => refs.Select(x => x.assetPath).ToArray();
        void EqualPaths(IEnumerable<GeneratedAssetReferenceRecord> a, IEnumerable<GeneratedAssetReferenceRecord> b) {
            Require(Paths(a).SequenceEqual(Paths(b)), "Reference path sequence differs.");
        }
        var empty = Palette(); var approved = Select(empty);
        Require(approved.Count > 0, "Existing approved fallback pool is unavailable.");
        string duplicatePath = approved[0].assetPath;
        Check("regional palette leads canopy", () => {
            var nordic = Ref("Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Tree.prefab", "tree");
            var asian = Ref("Assets/BefourStudios/AsianDynasty/Art/Prefabs/SM_Tree_03.prefab", "tree");
            Require(ReferenceEquals(Select(Palette(nordic))[0], nordic) && ReferenceEquals(Select(Palette(asian))[0], asian),
                "Global approved trees displaced the accepted regional lead.");
        });
        Check("accepted duplicate keeps reference metadata", () => {
            var accepted = Ref(duplicatePath, "tree"); accepted.weight = 19; accepted.scaleMin = .43f; accepted.scaleMax = .61f;
            accepted.assetKey = "accepted-reference-key"; accepted.placementRule = "accepted-placement-rule";
            var selected = Select(Palette(accepted)).Single(x => string.Equals(x.assetPath, duplicatePath, StringComparison.OrdinalIgnoreCase));
            Require(ReferenceEquals(selected, accepted) && selected.weight == 19 && selected.scaleMin == .43f && selected.scaleMax == .61f &&
                selected.assetKey == accepted.assetKey && selected.placementRule == accepted.placementRule,
                "A generated fallback replaced accepted reference metadata.");
        });
        Check("accepted input order precedes supplements", () => {
            var a = Ref(approved[approved.Count - 1].assetPath, "tree"); var b = Ref(duplicatePath, "tree");
            var result = Select(Palette(a, b));
            Require(ReferenceEquals(result[0], a) && ReferenceEquals(result[1], b), "Accepted list order was not retained.");
        });
        Check("case-insensitive duplicate keeps first accepted reference", () => {
            var first = Ref(duplicatePath.ToUpperInvariant(), "tree"); first.weight = 7;
            var second = Ref(duplicatePath.ToLowerInvariant(), "tree"); second.weight = 2;
            var result = Select(Palette(first, second));
            Require(result.Count(x => string.Equals(x.assetPath, duplicatePath, StringComparison.OrdinalIgnoreCase)) == 1 &&
                ReferenceEquals(result.Single(x => string.Equals(x.assetPath, duplicatePath, StringComparison.OrdinalIgnoreCase)), first),
                "Deduplication discarded the first accepted record.");
        });
        Check("canopy round-robin starts and wraps through accepted prefix", () => {
            var first = Ref(approved[approved.Count - 1].assetPath, "tree"); first.weight = 99;
            var second = Ref(duplicatePath, "tree"); second.weight = 1;
            var list = Select(Palette(first, second));
            // note: The source-bound fixture verifies this unchanged production consumer clause; it does not turn canopy cycling into weighted selection.
            int[] attempts = { 0, 1, list.Count, list.Count + 1 };
            var expected = new[] { first, second, first, second };
            for (int i = 0; i < attempts.Length; i++) {
                int attempt = attempts[i];
                Require(ReferenceEquals(list[attempt % list.Count], expected[i]), "Actual production list does not supply the accepted round-robin prefix/wrap.");
            }
        });
        Check("complete existing approved fallback pool retained", () => {
            var native = Ref("Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_TreeNeedles01.prefab", "conifer");
            var result = Select(Palette(native));
            Require(result.Count == approved.Count + 1 && Paths(approved).All(path => Paths(result).Contains(path)), "An approved fallback path was removed or duplicated.");
        });
        Check("null empty and invalid palettes preserve fallback behavior", () => {
            Require(Select(null).Count == 0, "Null palette changed behavior.");
            EqualPaths(Select(Palette()), approved);
            EqualPaths(Select(Palette(null, Ref("", "tree"), Ref("Assets/Backdrop/MountainTree.prefab", "tree"))), approved);
        });
        Check("tree filters reject low flora and large terrain features", () => {
            var tree = Ref("Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Tree.prefab", "tree");
            var result = Select(Palette(tree, Ref("Assets/Fixtures/Bush.prefab", "bush"), Ref("Assets/Fixtures/Grass.prefab", "grass"),
                Ref("Assets/Fixtures/MountainTree.prefab", "tree")));
            Require(result.Contains(tree) && result.Count == approved.Count + 1 && !Paths(result).Any(p => p.Contains("/Fixtures/")), "Existing tree/large-feature filters changed.");
        });
        Check("non-tree palette pool is unchanged", () => {
            var a = Ref("Assets/Fixtures/Fern.prefab", "fern"); var b = Ref("Assets/Fixtures/Bush.prefab", "bush");
            var c = Ref("Assets/Fixtures/Grass.prefab", "grass"); var d = Ref("Assets/Fixtures/Flower.prefab", "flower");
            var p = Palette(a, b, c, d, Ref("Assets/Fixtures/OakTree.prefab", "tree"));
            EqualPaths(Select(p, "vegetation", false), new[] { a, b, c, d });
        });
        Check("rock identity and backdrop filters are unchanged", () => {
            var rock = Ref("Assets/Fixtures/Rock.prefab", "rock"); var gravel = Ref("Assets/Fixtures/Gravel.prefab", "gravel");
            var p = Palette(); p.rock.AddRange(new[] { rock, gravel, Ref("Assets/Fixtures/Statue.prefab", "statue"), Ref("Assets/Fixtures/CliffRock.prefab", "cliff") });
            EqualPaths(Select(p, "rock", false), new[] { rock, gravel });
        });
        Check("rock exterior fallback remains available", () => {
            var p = Palette(); var fallback = Ref("Assets/Fixtures/Rubble.prefab", "rubble"); p.exteriorDeco.Add(fallback);
            EqualPaths(Select(p, "rock", false), new[] { fallback });
        });
        Check("input records remain byte-stable", () => {
            var p = Palette(Ref(duplicatePath, "tree"), Ref("Assets/Fixtures/Fern.prefab", "fern"));
            p.rock.Add(Ref("Assets/Fixtures/Rock.prefab", "rock"));
            string before = JsonConvert.SerializeObject(p);
            Select(p); Select(p, "vegetation", false); Select(p, "rock", false);
            Require(before == JsonConvert.SerializeObject(p), "Selector mutated accepted palette records.");
        });
        Check("repeat and reverse traversal are deterministic", () => {
            var a = Palette(Ref("Assets/BefourStudios/NordicVillage/Art/Prefabs/SM_Tree.prefab", "tree")); a.paletteId = "palette-north";
            var b = Palette(Ref("Assets/BefourStudios/AsianDynasty/Art/Prefabs/SM_Tree_03.prefab", "tree")); b.paletteId = "palette-south";
            var firstA = Select(a); var firstB = Select(b); var secondB = Select(b); var secondA = Select(a);
            EqualPaths(firstA, secondA); EqualPaths(firstB, secondB);
            for (int i = 0; i < 64; i++) {
                string seed = "622eff02|scatter|chunk:" + i + "|rock|palette";
                var x = (GeneratedAssetReferenceRecord)weighted.Invoke(null, new object[] { firstA, seed });
                var y = (GeneratedAssetReferenceRecord)weighted.Invoke(null, new object[] { secondA, seed });
                Require(x.assetPath == y.assetPath, "Actual seeded selector changed across repeat traversal.");
            }
        });
        passed = yes; failed = no; return checks;
    }
    private static GeneratedRegionAssetPaletteRecord Palette(params GeneratedAssetReferenceRecord[] refs) {
        var p = new GeneratedRegionAssetPaletteRecord { paletteId = "accepted-palette", regionId = "accepted-region", styleKey = "nordic_forest" };
        p.vegetation.AddRange(refs); return p;
    }
    private static GeneratedAssetReferenceRecord Ref(string path, string tag) => new GeneratedAssetReferenceRecord {
        assetPath = path, assetType = "prefab", slotTag = tag, assetKey = path, subTags = new List<string> { tag },
        weight = 1, scaleMin = 1f, scaleMax = 1f
    };
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}

#endif
