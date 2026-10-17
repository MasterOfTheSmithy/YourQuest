#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

// note: Disposable terrain and copied accepted plans exercise the actual materializer without changing profiles or scene assets.
public static class YQStartupVoiceRepairVerification
{
    private const string Folder = "outputs/StartupVoiceRepair_20261008";
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [MenuItem("YourQuest/Verification/Run Speech Transport Checks")]
    public static void RunSpeechTransportChecks()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var checks = new List<object>();
        GameObject host = null;
        LLMRuntimeConfig config = null;
        string failure = null;
        void Check(string name, bool pass)
        {
            checks.Add(new { name, pass });
            if (!pass) throw new InvalidOperationException(name);
        }
        try
        {
            // note: Inspect the actual transport serializer/parser without loading a model or modifying a profile.
            host = new GameObject("Disposable speech transport verification") { hideFlags = HideFlags.HideAndDontSave };
            var client = host.AddComponent<LLMClient>();
            config = LLMRuntimeConfig.CreateRuntimeDefault();
            var build = typeof(LLMClient).GetMethod("BuildRequestJson", BindingFlags.Instance | BindingFlags.NonPublic);
            var endpoint = typeof(LLMClient).GetMethod("SelectOllamaInferenceUrl", PrivateStatic);
            var parse = typeof(LLMClient).GetMethod("TryExtractResponseText", PrivateStatic);
            foreach (var category in new[] { LLMGenerationCategory.GoddessVerification, LLMGenerationCategory.DialogueVerification })
            {
                var profile = config.GetProfile(category);
                profile.reasoningMode = true; profile.directMode = false;
                var body = JObject.Parse((string)build.Invoke(client, new object[] { config, YQLlmBackend.Ollama, "review fixture", "fixture",
                    new Dictionary<string, object> { { "num_predict", 2048 } }, profile, true, YQDialogueGrounding.ReviewSchema() }));
                Check(category + " uses a chat message", body["prompt"] == null && body["messages"]?[0]?["content"]?.Value<string>() == "review fixture");
                Check(category + " keeps reasoning and schema", body.Value<bool>("think") && body["format"] is JObject);
                Check(category + " uses matching endpoint", (string)endpoint.Invoke(null, new object[] { "http://127.0.0.1:11434/api/generate", profile }) == "http://127.0.0.1:11434/api/chat");
            }
            var author = config.GetProfile(LLMGenerationCategory.GoddessCommentary);
            var direct = JObject.Parse((string)build.Invoke(client, new object[] { config, YQLlmBackend.Ollama, "speak fixture", "fixture",
                new Dictionary<string, object>(), author, true, YQGoddessGrounding.ReplySchema() }));
            Check("direct author retains generate payload", direct["messages"] == null && direct.Value<string>("prompt") == "speak fixture" && direct.Value<bool>("think") == false);
            object[] final = { "{\"message\":{\"content\":\"final verdict\",\"thinking\":\"private analysis\"}}", YQLlmBackend.Ollama, null, null };
            Check("chat extracts final content", (bool)parse.Invoke(null, final) && (string)final[2] == "final verdict");
            object[] thought = { "{\"message\":{\"content\":\"\",\"thinking\":\"private analysis\"},\"reasoning_content\":\"private analysis\"}", YQLlmBackend.Ollama, null, null };
            Check("thinking alone cannot become a reply", !(bool)parse.Invoke(null, thought));
            object[] legacy = { "{\"response\":\"legacy final\"}", YQLlmBackend.Ollama, null, null };
            Check("direct replies remain compatible", (bool)parse.Invoke(null, legacy) && (string)legacy[2] == "legacy final");
        }
        catch (Exception exception) { failure = exception.ToString(); Debug.LogException(exception); }
        finally
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            if (config != null) UnityEngine.Object.DestroyImmediate(config);
            const string folder = "outputs/SpeechRecovery_20261008";
            Directory.CreateDirectory(folder);
            File.WriteAllText(folder + "/TransportChecks.json", JsonConvert.SerializeObject(new { utc = DateTime.UtcNow,
                status = failure == null ? "PASS" : "FAIL", evidence = "UNITY_EDITOR_TRANSPORT_CONTRACTS; NOT_LIVE_SPEECH",
                runtimeMvid = typeof(LLMClient).Module.ModuleVersionId, checks, failure }, Formatting.Indented));
            Debug.Log("[YQSpeechTransportVerification] " + (failure == null ? "PASS" : "FAIL") + "; checks=" + checks.Count);
        }
    }

    [MenuItem("YourQuest/Verification/Run Startup Voice Repair Checks")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var checks = new List<object>();
        var unavailable = new List<object>();
        string failure = null;
        GameObject temporary = null;
        TerrainData data = null;
        Material bankMaterial = null;
        void Check(string name, bool pass, string detail = null)
        {
            checks.Add(new { name, pass, detail });
            if (!pass) throw new InvalidOperationException(name + ": " + detail);
        }
        try
        {
            data = new TerrainData { hideFlags = HideFlags.HideAndDontSave, heightmapResolution = 33, size = new Vector3(1024f, 140f, 1024f) };
            temporary = new GameObject("Disposable startup crossing verification") { hideFlags = HideFlags.HideAndDontSave };
            // note: Production terrain is centered at zero; a corner-origin fixture concealed world-owned approaches outside its tile.
            temporary.transform.position = new Vector3(-512f, 0f, -512f);
            var terrain = temporary.AddComponent<Terrain>(); terrain.terrainData = data;
            bankMaterial = (Material)typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildFallbackRoadMaterial", PrivateStatic).Invoke(null, null);
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None,
                Converters = { new Vector2JsonConverter(), new Vector3JsonConverter(), new QuaternionJsonConverter() } };
            foreach (string fixture in new [] { "FailingWorldPlan.json", "BaselineWorldPlan.json" })
            {
                var plan = JsonConvert.DeserializeObject<GeneratedWorldPlanRecord>(File.ReadAllText(Path.Combine(Folder, fixture)), settings);
                string originalHash = YQSpatialBlueprintHasherV2.ComputeContentHash(plan.spatialPlanV2);
                Check(fixture + " accepted content hash", originalHash == plan.spatialPlanV2.contentHash, originalHash);
                bool preparedOk = YQSpatialMaterializationCompilerV2.TryPrepare(plan, out var prepared, out string reason);
                // note: The copied baseline contains continuation providers whose cache belongs to its active runtime. Do not impersonate that world or weaken preparation to run a detached fixture.
                if (!preparedOk && fixture == "BaselineWorldPlan.json" && reason == "continuation reviewed provider is not cached")
                {
                    unavailable.Add(new { fixture, reason });
                    continue;
                }
                Check(fixture + " accepted projection", preparedOk, reason);
                var physical = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("IsPhysicalRiverCrossing", PrivateStatic);
                var build = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("TryBuildCrossingSpan", PrivateStatic);
                int tested = 0;
                for (int index = 0; index < prepared.CrossingCount; index++)
                {
                    var crossing = prepared.GetCrossing(index);
                    if (!(bool)physical.Invoke(null, new object[] { crossing.kind })) continue;
                    object[] args = { prepared, crossing, terrain, null, null };
                    bool passed = (bool)build.Invoke(null, args);
                    Check(fixture + " crossing " + crossing.crossingId, passed, args[4] as string);
                    var spanType = args[3].GetType();
                    var deckPoints = (List<Vector3>)spanType.GetField("points").GetValue(args[3]);
                    checks.Add(new { name = "derived span geometry " + crossing.crossingId, pass = true,
                        width = (float)spanType.GetField("width").GetValue(args[3]), start = deckPoints[0].ToString("F2"),
                        end = deckPoints[deckPoints.Count - 1].ToString("F2") });
                    tested++;
                }
                Check(fixture + " physical crossings exercised", tested > 0, tested.ToString());
                // note: Individual spans cannot certify sharing and bank connectivity. Exercise the complete derived layout against the same native terrain and accepted projection.
                var layout = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("PrepareBridgeLayoutRoutine", PrivateStatic);
                var callbackType = layout.GetParameters()[2].ParameterType;
                var callback = Delegate.CreateDelegate(callbackType, typeof(YQStartupVoiceRepairVerification)
                    .GetMethod(nameof(CaptureLayout), PrivateStatic).MakeGenericMethod(callbackType.GetGenericArguments()[0]));
                capturedLayout = null;
                var stack = new Stack<IEnumerator>();
                stack.Push((IEnumerator)layout.Invoke(null, new object[] { prepared, terrain, callback }));
                int steps = 0;
                try
                {
                    while (stack.Count > 0)
                    {
                        if (++steps > 1000000) throw new InvalidOperationException("Detached layout pump exceeded its verification bound.");
                        var routine = stack.Peek();
                        if (!routine.MoveNext()) { (routine as IDisposable)?.Dispose(); stack.Pop(); }
                        else if (routine.Current is IEnumerator child) stack.Push(child);
                    }
                }
                finally { while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose(); }
                Check(fixture + " complete bridge sharing and dry-bank layout", capturedLayout != null);
                var physicalDecks = new HashSet<object>();
                var dryBank = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("IsDryBridgeBank", PrivateStatic);
                foreach (object span in (IEnumerable)capturedLayout)
                {
                    var spanType = span.GetType();
                    object bridge = spanType.GetField("bridge").GetValue(span);
                    physicalDecks.Add(bridge);
                    if (ReferenceEquals(span, bridge)) continue;
                    var paths = (List<List<Vector3>>)spanType.GetField("bankConnections").GetValue(span);
                    float pathWidth = (float)spanType.GetField("width").GetValue(span);
                    bool dry = paths.Count == 2;
                    foreach (var path in paths)
                    foreach (var point in path)
                        dry &= (bool)dryBank.Invoke(null, new object[] { prepared, new Vector2(point.x, point.z), pathWidth });
                    Check("shared crossing has two full-width dry approaches " + spanType.GetField("crossingId").GetValue(span), dry);
                    // note: Exercise the actual mesh and collider path, including banks beyond the initial terrain boundary.
                    foreach (var path in paths)
                    {
                        var geometry = (GameObject)typeof(YQContinuousWorldFeatureMaterializer).GetMethod("BuildSharedBridgeBankPath", PrivateStatic)
                            .Invoke(null, new object[] { temporary.transform, path, pathWidth, bankMaterial, terrain, prepared });
                        string id = (string)spanType.GetField("crossingId").GetValue(span);
                        Check("shared approach has physical geometry " + id, geometry != null && geometry.GetComponent<MeshRenderer>() != null &&
                            geometry.GetComponent<MeshCollider>()?.sharedMesh?.triangles.Length > 0, path[0] + " -> " + path[path.Count - 1]);
                        UnityEngine.Object.DestroyImmediate(geometry);
                    }
                }
                var decks = new List<object>(physicalDecks);
                var conflict = typeof(YQContinuousWorldFeatureMaterializer).GetMethod("CrossingDecksConflict", PrivateStatic);
                bool clear = true;
                for (int a = 0; a < decks.Count; a++)
                for (int b = a + 1; b < decks.Count; b++)
                {
                    var spanType = decks[a].GetType();
                    clear &= !(bool)conflict.Invoke(null, new [] { spanType.GetField("points").GetValue(decks[a]), spanType.GetField("width").GetValue(decks[a]),
                        spanType.GetField("points").GetValue(decks[b]), spanType.GetField("width").GetValue(decks[b]) });
                }
                Check(fixture + " physical decks retain required clearance", clear, decks.Count.ToString());
                Check(fixture + " immutable canonical plan", originalHash == YQSpatialBlueprintHasherV2.ComputeContentHash(plan.spatialPlanV2));
            }
            var merge = typeof(YQOriginGenerationService).GetMethod("TryMergeOriginIdentityRepair", PrivateStatic);
            JObject original = JObject.Parse("{\"titleName\":\"\",\"className\":\"Bridge Keeper\",\"ability\":{\"name\":\"Measure the Ford\"},\"quest\":{\"name\":\"Ask the Archivist\"},\"loadout\":[{\"nameHint\":\"Survey Staff\"}]}");
            string preimage = original.ToString(Formatting.None);
            object[] Merge(string raw, string field = "titleName") => new object[] { original, field, raw, null, null };
            object[] patch = Merge("{\"titleName\":\"Keeper of the Span\"}");
            Check("required identity patch accepted", (bool)merge.Invoke(null, patch));
            JObject expected = (JObject)original.DeepClone(); expected["titleName"] = "Keeper of the Span";
            Check("identity repair preserves every other field", JToken.DeepEquals(expected, JObject.Parse((string)patch[3])));
            Check("identity repair never mutates the source proposal", preimage == original.ToString(Formatting.None));
            foreach (string raw in new [] { "{\"titleName\":\"  \"}", "{\"titleName\":null}", "{\"titleName\":{}}",
                "{\"titleName\":\"a\",\"className\":\"Changed\"}", "{\"titleName\":\"a\",\"titleName\":\"b\"}",
                "{\"titleName\":\"a\"} trailing", "{\"titleName\":\"" + new string('x', 121) + "\"}" })
                Check("invalid identity patch rejected " + raw.Substring(0, Math.Min(45, raw.Length)), !(bool)merge.Invoke(null, Merge(raw)));
            Check("identity repair whitelist rejects other fields", !(bool)merge.Invoke(null, Merge("{\"ability\":\"Changed\"}", "ability")));
            var properties = (Dictionary<string, object>)YQLlmJsonSchema.BuildOrigin()["properties"];
            foreach (string key in new [] { "titleName", "className" })
                Check("wire requires nonempty " + key, Convert.ToInt32(((Dictionary<string, object>)properties[key])["minLength"]) == 1);
        }
        catch (Exception exception) { failure = exception.ToString(); Debug.LogException(exception); }
        finally
        {
            if (temporary != null) UnityEngine.Object.DestroyImmediate(temporary);
            if (data != null) UnityEngine.Object.DestroyImmediate(data);
            if (bankMaterial != null) UnityEngine.Object.DestroyImmediate(bankMaterial);
            Directory.CreateDirectory(Folder);
            File.WriteAllText(Path.Combine(Folder, "EditorChecks.json"), JsonConvert.SerializeObject(new {
                utc = DateTime.UtcNow, status = failure == null ? "PASS" : "FAIL", evidence = "UNITY_EDITOR_NATIVE_TERRAIN_AND_REAL_ACCEPTED_PROJECTIONS; NOT_SCENE_RUNTIME_OR_PLAYER_TRAVERSAL",
                runtimeMvid = typeof(YQContinuousWorldFeatureMaterializer).Module.ModuleVersionId, checks, unavailable, failure
            }, Formatting.Indented));
            Debug.Log("[YQStartupVoiceRepairVerification] " + (failure == null ? "PASS" : "FAIL") + "; checks=" + checks.Count + "; receipt=" + Folder + "/EditorChecks.json");
        }
    }
    private static object capturedLayout;
    private static void CaptureLayout<T>(T value) { capturedLayout = value; }
}
#endif
