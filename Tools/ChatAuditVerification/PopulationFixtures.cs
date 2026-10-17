// note: Managed logic checks use verbatim extracted production methods. Unity lifecycle, native loading, JsonUtility codec and renderer bounds are controlled fixtures, not engine certification.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityEngine
{
    public static class JsonUtility
    {
        public static int calls;
        public static string ToJson(object value) { calls++; return JsonConvert.SerializeObject(value); }
        public static T FromJson<T>(string value) => JsonConvert.DeserializeObject<T>(value);
    }
    public class Transform { public Transform parent; }
    public class Renderer { public string name; }
    public class TextMesh { }
    public class GameObject
    {
        public string name;
        public LODGroup[] groups = Array.Empty<LODGroup>();
        public T[] GetComponentsInChildren<T>(bool inactive) => typeof(T) == typeof(LODGroup) ? (T[])(object)groups.Clone() : Array.Empty<T>();
    }
    public static class Object
    {
        public static int instances;
        public static GameObject Instantiate(GameObject source, Transform parent) { instances++; return new GameObject { name = source.name + "(Clone)" }; }
    }
    public struct LOD
    {
        public float screenRelativeTransitionHeight, fadeTransitionWidth;
        public Renderer[] renderers;
        public LOD(float threshold, Renderer[] values) { screenRelativeTransitionHeight = threshold; renderers = values; fadeTransitionWidth = 0; }
    }
    public class LODGroup
    {
        public Transform transform = new Transform();
        public LOD[] lods;
        public bool enabled = true;
        public int boundsRecalculations;
        public LOD[] GetLODs() => (LOD[])lods.Clone();
        public void SetLODs(LOD[] value) => lods = value;
        public void RecalculateBounds() => boundsRecalculations++;
    }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a, b); public static float Max(float a, float b) => Math.Max(a, b); }
}
namespace TMPro { public class TMP_Text { } }
public sealed class GeneratedWorldPlanRecord { public string worldSeed; }
public sealed class GeneratedNpcPlanRecord
{
    public string npcId, regionId, settlementId, encampmentId, factionId, displayName, role, archetype, ageBand, presentation,
        appearanceSummary, personality, speakingStyle, dailyRoutine, localKnowledge, privateConcern;
    public bool notable, merchant, guard, hostile, boss;
    public List<string> tags = new List<string>(), verboseInternals = new List<string>();
}
public sealed class GeneratedSettlementRecord { public string displayName; public List<string> nested = new List<string>(); }
public sealed class GeneratedEncampmentRecord { public string monsterFamily; }
public sealed class GeneratedSpatialContinuationLocationV2Record
{
    public string contentHash = "unchanged-claim";
    public GeneratedSettlementRecord settlement = new GeneratedSettlementRecord();
    public GeneratedEncampmentRecord encampment;
    public List<GeneratedNpcPlanRecord> population = new List<GeneratedNpcPlanRecord>();
    public List<string> proofClaims = new List<string>();
}
public sealed class WorldState
{
    public sealed class NpcRecord { public string npcId, status; }
    public List<NpcRecord> npcs = new List<NpcRecord>();
    public void EnsureCollections() { npcs ??= new List<NpcRecord>(); }
}
public static class YQServiceLifecycle { public static int RequestEpoch; }
public sealed class YQRuntimeWorldAssetEntry { public string assetPath; public GameObject prefab; }
public sealed class YQRuntimeWorldAssetRegistry
{
    public int overrides, unexpectedAutomaticRepairs;
    public void ApplyMaterialOverrides(string path, GameObject value, bool repairHierarchy = true)
    { overrides++; if (repairHierarchy) unexpectedAutomaticRepairs++; }
}
public static class YQGeneratedActorVisualIntent { public static int captures; public static void Capture(GameObject value, GameObject source) => captures++; }
public static class YQPopulationWorkDiagnostics
{
    public sealed class Scope : IDisposable { public void Dispose() { } }
    public sealed class Marker { public Scope Auto() => new Scope(); }
    public static readonly Marker PrefabInstantiate = new Marker(), PrefabVisualIntent = new Marker(), PrefabMaterialOverrides = new Marker(), PrefabText = new Marker();
}
public static partial class YQRuntimeUrpMaterialRepair
{
    public static int materialPasses, textPasses, cooperativePasses;
    public static int RepairMaterialHierarchy(GameObject root) { materialPasses++; return 1; }
    private static int RepairTextMeshes(TextMesh[] text) { textPasses++; return 0; }
    private static int RepairTmpText(TMPro.TMP_Text[] text) { textPasses++; return 0; }
    public static IEnumerator RepairMaterialHierarchyRoutine(GameObject root, Action<int> done)
    { cooperativePasses++; yield return null; done?.Invoke(1); }
}
public static partial class YQGeneratedWorldPopulation
{
    internal static object Capture(GeneratedWorldPlanRecord plan, WorldState world, GeneratedSpatialContinuationLocationV2Record location) =>
        new ContinuationPopulationSnapshot(plan, world, location);
    internal static bool Current(object snapshot, GeneratedWorldPlanRecord plan, WorldState world, GeneratedSpatialContinuationLocationV2Record location) =>
        ((ContinuationPopulationSnapshot)snapshot).IsCurrent(plan, world, location);
    internal static IEnumerator Prepare(Transform parent, YQRuntimeWorldAssetEntry entry, YQRuntimeWorldAssetRegistry registry, Action<GameObject> done) =>
        InstantiateRegisteredPrefabRoutine(parent, entry, registry, done);
}
public static partial class YQRuntimeCreatureAssetIndex
{
    internal static bool Excluded(string path) => ExcludesHumanCategory(ClassifyMetadataPath(path));
}
public static class PopulationFixtures
{
    public static int Run(string output)
    {
        var results = new List<object>(); int failures = 0;
        void Check(string name, bool pass) { results.Add(new { name, passed = pass }); if (!pass) { failures++; Console.WriteLine("FAIL: " + name); } }
        var plan = new GeneratedWorldPlanRecord { worldSeed = "76603739" };
        var world = new WorldState();
        var first = new GeneratedNpcPlanRecord { npcId = "npc-1", role = "smith", tags = new List<string> { "friendly" } };
        var second = new GeneratedNpcPlanRecord { npcId = "npc-2", role = "guard" };
        var location = new GeneratedSpatialContinuationLocationV2Record { population = new List<GeneratedNpcPlanRecord> { first, second } };
        object snapshot = YQGeneratedWorldPopulation.Capture(plan, world, location);
        bool Current() => YQGeneratedWorldPopulation.Current(snapshot, plan, world, location);
        Check("unchanged scoped context remains current", Current());
        first.role = "merchant"; Check("in-place actor field invalidates unchanged hash claim", !Current()); first.role = "smith";
        first.tags.Add("changed"); Check("in-place actor list invalidates", !Current()); first.tags.RemoveAt(1);
        location.population.Reverse(); Check("canonical ordering is part of staging context", !Current()); location.population.Reverse();
        location.population[0] = JsonUtility.FromJson<GeneratedNpcPlanRecord>(JsonUtility.ToJson(first));
        Check("equal cloned actor cannot replace canonical reference", !Current()); location.population[0] = first;
        location.settlement.nested.Add("layout mutation"); Check("nested location body invalidates without hash change", !Current()); location.settlement.nested.Clear();
        location.proofClaims.Add("changed proof"); Check("non-actor location proof mutation invalidates", !Current()); location.proofClaims.Clear();
        var saved = location.settlement; location.settlement = new GeneratedSettlementRecord();
        Check("equal replacement settlement identity invalidates", !Current()); location.settlement = saved;
        world.npcs.Add(new WorldState.NpcRecord { npcId = first.npcId, status = "dead" });
        Check("death outside serialized location invalidates", !Current()); world.npcs.Clear();
        plan.worldSeed = "changed"; Check("canonical seed mutation invalidates", !Current()); plan.worldSeed = "76603739";
        YQServiceLifecycle.RequestEpoch++; Check("profile request epoch invalidates", !Current()); YQServiceLifecycle.RequestEpoch--;
        Check("restored exact context passes", Current());

        var renderer = new Renderer { name = "shared" }; var other = new Renderer { name = "outer" };
        var parent = new Transform();
        var child = new LODGroup { transform = new Transform { parent = parent }, lods = new[] {
            new LOD(.65f, new[] { renderer, renderer }), new LOD(.2f, new[] { renderer }) } };
        var outer = new LODGroup { transform = parent, lods = new[] {
            new LOD(.75f, new[] { renderer, other }), new LOD(.1f, Array.Empty<Renderer>()) } };
        var root = new GameObject { groups = new[] { outer, child } };
        YQRuntimeUrpMaterialRepair.RepairHierarchy(root, false);
        Check("deferred hierarchy repair performs one material pass without premature LOD mutation", YQRuntimeUrpMaterialRepair.materialPasses == 1 && child.boundsRecalculations == 0 && outer.boundsRecalculations == 0);
        YQRuntimeUrpMaterialRepair.StabilizeLodHierarchy(root);
        Check("deepest LOD group retains authored shared renderer across levels", child.lods.Length == 2 && child.lods[0].renderers.SequenceEqual(new[] { renderer }) && child.lods[1].renderers.SequenceEqual(new[] { renderer }));
        Check("ancestor relinquishes shared renderer and drops empty level", outer.lods.Length == 1 && outer.lods[0].renderers.SequenceEqual(new[] { other }));
        Check("authored transition thresholds survive one final stabilization", child.lods[0].screenRelativeTransitionHeight == .65f && child.lods[1].screenRelativeTransitionHeight == .2f && outer.lods[0].screenRelativeTransitionHeight == .75f);
        Check("bounds rebuild occurs once per final group", child.boundsRecalculations == 1 && outer.boundsRecalculations == 1);

        var registry = new YQRuntimeWorldAssetRegistry(); GameObject prepared = null;
        var routine = YQGeneratedWorldPopulation.Prepare(new Transform(), new YQRuntimeWorldAssetEntry { assetPath = "fixture", prefab = new GameObject { name = "body" } }, registry, value => prepared = value);
        Check("native clone has its own yielded step", routine.MoveNext() && routine.Current == null && UnityEngine.Object.instances == 1 && YQGeneratedActorVisualIntent.captures == 0 && prepared == null);
        Check("visual intent has a separate yielded step", routine.MoveNext() && YQGeneratedActorVisualIntent.captures == 1 && registry.overrides == 0 && prepared == null);
        Check("overrides skip automatic residual repair", routine.MoveNext() && registry.overrides == 1 && registry.unexpectedAutomaticRepairs == 0 && prepared == null);
        Check("existing cooperative material repair is delegated", routine.MoveNext() && routine.Current is IEnumerator && prepared == null);
        var nested = (IEnumerator)routine.Current; while (nested.MoveNext()) { }
        Check("text finishes before publication callback", routine.MoveNext() && prepared == null);
        Check("complete preparation invokes callback only after all phases", !routine.MoveNext() && prepared != null && YQRuntimeUrpMaterialRepair.cooperativePasses == 1);

        foreach (string path in new[] { "Assets/Characters/Dragons/Dragon.prefab", "Assets/Characters/Demons/Devil.prefab", "Assets/Characters/Spiders/Spider.prefab", "Assets/Characters/Rock Monster/RockMonster.prefab" })
        {
            string category = YQRuntimeCreatureAssetIndex.ClassifyEntry(new YQRuntimeWorldAssetEntry { assetPath = path, prefab = new GameObject { name = "Human Male" } });
            Check("excluded path remains non-human even with human prefab-name tokens: " + path,
                YQRuntimeCreatureAssetIndex.Excluded(path) && category != YQRuntimeCreatureAssetIndex.HumanGeneric && category != YQRuntimeCreatureAssetIndex.HumanMale && category != YQRuntimeCreatureAssetIndex.HumanFemale);
        }
        Check("ambiguous generic-monster metadata remains eligible for full human inspection", !YQRuntimeCreatureAssetIndex.Excluded("Assets/Characters/Monster/Unknown.prefab"));
        File.WriteAllText(Path.Combine(output, "population-fixtures.json"), JsonConvert.SerializeObject(new {
            evidence = "VERBATIM_MANAGED_CONTEXT_LOD_AND_PREPARATION_METHODS_WITH_CONTROLLED_UNITY_BOUNDARIES_NOT_RUNTIME_OR_PERFORMANCE_PASS",
            limitations = "JsonUtility uses a test JSON codec; DTOs, native cloning, bounds and material work use small controlled fixtures. Real imported assets, Unity lifecycle, all LOD animation and cold frame durations require Unity validation.",
            checks = results.Count, failures, results
        }, Formatting.Indented));
        Console.WriteLine("Population managed fixtures: checks=" + results.Count + "; failures=" + failures);
        return failures;
    }
}
