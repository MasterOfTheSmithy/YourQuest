using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Opt-in read-only regression uses the installed runtime and saved records, without spawning or committing a world.
public static class YQSpeechStartupRepairVerification
{
    private static IEnumerator _run;
    [MenuItem("YourQuest/Voice/Verify startup repairs")]
    public static void Run()
    {
        if (_run != null) return;
        _run = Verify();
        EditorApplication.update += Tick;
    }
    private static void Tick()
    {
        try { if (_run.MoveNext()) return; }
        catch (Exception ex) { Debug.LogException(ex); }
        (_run as IDisposable)?.Dispose();
        _run = null;
        EditorApplication.update -= Tick;
    }
    private static IEnumerator Verify()
    {
        string folder = "outputs/LocalSpeech/startup-repair";
        Directory.CreateDirectory(folder);
        var receipt = new JObject { ["utc"] = DateTime.UtcNow.ToString("O"), ["evidence"] = "READ_ONLY_EDITOR_REGRESSION" };
        var checks = new JArray(); receipt["checks"] = checks;
        var config = LLMRuntimeConfig.CreateRuntimeDefault();
        try
        {
            var readHelp = typeof(LlamaCppServerProcess).GetMethod("ReadHelpText", BindingFlags.NonPublic | BindingFlags.Static);
            for (int i = 0; !EditorApplication.isPlaying && i < 10; i++)
            {
                string help = null;
                var probe = (IEnumerator)readHelp.Invoke(null, new object[] { config.llamaServerExecutablePath, 2, (Action<string>)(text => help = text) });
                try { while (probe.MoveNext()) yield return null; }
                finally { (probe as IDisposable)?.Dispose(); }
                checks.Add(new JObject { ["name"] = "complete GPU help capture " + i,
                    ["pass"] = help != null && help.Contains("--n-gpu-layers") && help.Contains("--fit") && help.Contains("--device") && !help.StartsWith("HELP_PROBE_FAILED"),
                    ["characters"] = help?.Length ?? 0 });
            }
            string path = Path.Combine(Application.persistentDataPath, "world_state.json");
            var world = EditorApplication.isPlaying ? WorldStateManager.Instance.State : JsonConvert.DeserializeObject<WorldState>(File.ReadAllText(path), new JsonSerializerSettings
                { TypeNameHandling = TypeNameHandling.None, Converters = { new Vector3JsonConverter(), new Vector2JsonConverter(), new QuaternionJsonConverter() } });
            var plan = world.generatedWorldPlan;
            var warmPaths = (System.Collections.Generic.List<string>)typeof(YQGeneratedWorldRuntimeBuilder)
                .GetMethod("CollectActivePaletteAssetPaths", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, new object[] { plan });
            var registry = YQRuntimeWorldAssetRegistry.Instance;
            // note: Simulate Unity retaining a private flag while discarding dictionaries on a detached registry, never the live asset.
            var detachedRegistry = ScriptableObject.CreateInstance<YQRuntimeWorldAssetRegistry>();
            try
            {
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var attempted = typeof(YQRuntimeWorldAssetRegistry).GetField("_granularLookupAttempted", flags);
                attempted.SetValue(detachedRegistry, true);
                typeof(YQRuntimeWorldAssetRegistry).GetMethod("OnEnable", flags).Invoke(detachedRegistry, null);
                checks.Add(new JObject { ["name"] = "reload retires orphaned manifest flag", ["pass"] = !(bool)attempted.GetValue(detachedRegistry) });
            }
            finally { UnityEngine.Object.DestroyImmediate(detachedRegistry); }
            receipt["playMode"] = EditorApplication.isPlaying;
            receipt["catalogLoaded"] = YQDotCreatureCatalog.Current != null;
            receipt["registryGranular"] = registry.UsesGranularResourceShards;
            var visuals = new JArray(); receipt["thornweaverVisuals"] = visuals;
            foreach (var entry in YQDotCreatureCatalog.Current.entries)
            {
                if (entry.species != "thornweaver" || entry.kind != "monster" || !string.IsNullOrEmpty(entry.moduleSlot)) continue;
                var bound = registry.ResolveEntry(entry.prefabPath);
                var prefab = registry.ResolvePrefab(entry.prefabPath);
                visuals.Add(new JObject { ["id"] = entry.assetId, ["path"] = entry.prefabPath,
                    ["entryAvailable"] = bound != null, ["serializedPrefabAvailable"] = bound?.prefab != null,
                    ["resolved"] = prefab != null });
            }
            foreach (var location in plan.spatialPlanV2.acceptedContinuation.locations)
            {
                bool ok = YQGeneratedWorldPopulation.TryValidateContinuationPopulationBindings(plan, world, location, registry, out string failure);
                checks.Add(new JObject { ["name"] = location.contentId, ["pass"] = ok, ["failure"] = failure,
                    ["settlement"] = location.settlement != null, ["encampment"] = location.encampment != null, ["poi"] = location.pointOfInterest != null,
                    ["population"] = JArray.FromObject(location.population) });
                if (location.encampment != null)
                    foreach (var npc in location.population)
                        if (YQDotCreatureCatalog.TrySelectPrefabPath(location.encampment.monsterFamily, "monster",
                            plan.worldSeed + "|encampment_leader|" + npc.npcId, out string selectedPath, out _))
                            checks.Add(new JObject { ["name"] = "selected continuation visual warmed: " + npc.npcId,
                                ["pass"] = warmPaths.Contains(selectedPath), ["path"] = selectedPath });
            }
            checks.Add(new JObject { ["name"] = "unknown species remains unsupported",
                ["pass"] = !YQDotCreatureCatalog.TrySelectPrefabPath("unknown_test_species", "monster", "test", out _, out _) });
            receipt["runtimeMvid"] = typeof(LLMClient).Assembly.ManifestModule.ModuleVersionId.ToString();
            File.WriteAllText(folder + (EditorApplication.isPlaying ? "/play-checks.json" : "/edit-checks.json"), receipt.ToString());
            Debug.Log("[YQSpeechStartupRepair] Read-only checks saved to " + folder + "/checks.json");
        }
        finally { UnityEngine.Object.DestroyImmediate(config); }
    }
}
