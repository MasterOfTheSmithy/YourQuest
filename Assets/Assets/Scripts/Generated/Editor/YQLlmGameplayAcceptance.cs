#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Profiling;

// note: A read-only diagnostic submits to the live scheduler; it never replaces profiles, config, model owners or accepted records.
[InitializeOnLoad]
public static class YQLlmGameplayAcceptance
{
    private const string ArmedKey = "YourQuest.LlmGameplayAcceptance.Armed";
    private const string ResumeKey = "YourQuest.LlmGameplayAcceptance.Resume";
    private const string FixtureKey = "YourQuest.LlmGameplayAcceptance.Fixture";
    private const string NpcOnlyKey = "YourQuest.LlmGameplayAcceptance.NpcOnly";
    private const string PracticalKey = "YourQuest.LlmGameplayAcceptance.PracticalSpeech";
    private static Runner active;
    static YQLlmGameplayAcceptance() { EditorApplication.update += Tick; }

    public static void BeginPracticalSpeechResume()
    {
        // note: This explicit diagnostic uses only the disposable benchmark profile and exercises ordinary transcript/speech-history publication.
        SessionState.SetBool(PracticalKey, true);
        BeginAssistedResume();
    }

    public static void BeginAssistedNpcResume()
    {
        SessionState.SetBool(NpcOnlyKey, true);
        BeginAssistedResume();
    }

    public static void BeginAssistedResume()
    {
        // note: Explicit command-line verification reuses only the named disposable benchmark profile in a normal, non-isolated Editor.
        if (LLMRuntimeConfig.BenchmarkIsolationActive || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("An idle normal Editor is required.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Unsaved scene blocks assisted resume.");
        var prior = JObject.Parse(File.ReadAllText("outputs/LlmRequalification_20261008/UnityBaseline/Current.json"));
        string fixture = prior.Value<string>("profileId");
        if (!Guid.TryParseExact(fixture, "N", out _)) throw new InvalidOperationException("Missing prior test profile identity.");
        SessionState.SetString(FixtureKey, fixture);
        SessionState.SetString(ResumeKey, "title");
        SessionState.SetBool(ArmedKey, true);
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Assets/Scenes/YourQuest_PlaySafe.unity");
        EditorApplication.isPlaying = true;
    }

    [MenuItem("YourQuest/Verification/Arm LLM Acceptance After Gameplay Loads")]
    public static void Arm()
    {
        if (active != null) return;
        SessionState.SetBool(ArmedKey, true);
        Debug.Log("[YQLlmGameplayAcceptance] Armed; complete ordinary Continue/New Journey. No profile or gameplay settings changed.");
    }

    private static void Tick()
    {
        if (SessionState.GetString(ResumeKey, string.Empty) == "title" && Application.isPlaying &&
            !EditorApplication.isCompiling && YQProfileSaveSystem.Instance != null && YQTitleScreenUI.Instance != null)
        {
            string fixture = SessionState.GetString(FixtureKey, string.Empty);
            if (!(YQProfileSaveSystem.Instance.FindProfile(fixture)?.displayName ?? string.Empty).StartsWith("LLM Benchmark ", StringComparison.Ordinal))
            {
                SessionState.SetString(ResumeKey, string.Empty); SessionState.SetBool(ArmedKey, false);
                Debug.LogError("[YQLlmGameplayAcceptance] Resume refused: profile is not the disposable benchmark fixture.");
                return;
            }
            if (YQTitleScreenUI.Instance.ContinueExistingForDevelopmentVerification(fixture))
            {
                Application.runInBackground = true;
                SessionState.SetString(ResumeKey, "world");
            }
        }
        if (!SessionState.GetBool(ArmedKey, false) || active != null || !Application.isPlaying ||
            !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased || LLMClient.Instance == null) return;
        SessionState.SetBool(ArmedKey, false);
        active = new GameObject("LLM read-only gameplay acceptance").AddComponent<Runner>();
    }

    [MenuItem("YourQuest/Verification/Stop LLM Acceptance")]
    public static void Stop()
    {
        SessionState.SetBool(ArmedKey, false);
        if (active != null) active.End("OPERATOR_STOPPED");
    }

    [MenuItem("YourQuest/Verification/Open Existing NPC Dialogue For Gameplay Test")]
    public static void OpenNpcDialogue()
    {
        // note: This separately named assisted UI step uses a real NPC and the existing validation entry point; typed conversation uses normal transcript persistence.
        if (!Application.isPlaying || active != null || !YourQuestTutorialAutoBootstrap.GameplayPresentationReleased) return;
        NpcDialogueAgent npc = FindLiveNpc();
        EntityInfo info = npc != null ? npc.GetComponentInParent<EntityInfo>() : null;
        bool opened = npc != null && YQInvestorDialogueUI.Instance != null &&
            YQInvestorDialogueUI.Instance.OpenNpcForValidation(info, npc);
        Debug.Log("[YQLlmGameplayAcceptance] Assisted dialogue window opened=" + opened + "; npc=" + (npc != null ? npc.NpcId : "none"));
    }

    private static NpcDialogueAgent FindLiveNpc()
    {
        NpcDialogueAgent selected = null;
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<NpcDialogueAgent>(FindObjectsSortMode.None))
            if (candidate.isActiveAndEnabled && (selected == null || string.CompareOrdinal(candidate.NpcId, selected.NpcId) < 0)) selected = candidate;
        return selected;
    }

    private sealed class Runner : MonoBehaviour
    {
        private readonly List<object> results = new List<object>();
        private readonly List<float> frameMs = new List<float>();
        private readonly List<double> mainThreadMs = new List<double>();
        private LLMClient client;
        private string profile, world, owner, folder;
        private int epoch;
        private long pending;
        private bool ended, cancelledOwner, publishesSpeechHistory;
        private ProfilerRecorder mainThread;
        private Action<YQLlmRequestResult> dialogueObserver;

        private IEnumerator Start()
        {
            client = LLMClient.Instance;
            profile = YQProfileSaveSystem.Instance.ActiveProfileId;
            world = WorldStateManager.Instance.State.worldIdentity.worldId;
            epoch = YQServiceLifecycle.RequestEpoch;
            owner = "GameplayAcceptance:" + Guid.NewGuid().ToString("N");
            publishesSpeechHistory = SessionState.GetBool(PracticalKey, false);
            folder = Path.Combine(publishesSpeechHistory ? "outputs/SpeechPractical_20261008" : "outputs/LlmReimplementation_20261008", "Live_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
            Directory.CreateDirectory(folder);
            mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
            Write("RUNNING");
            if (SessionState.GetBool(PracticalKey, false))
            {
                SessionState.SetBool(PracticalKey, false);
                yield return PracticalSpeech();
                End("COMPLETED_PRACTICAL_SPEECH");
                yield break;
            }
            if (SessionState.GetBool(NpcOnlyKey, false))
            {
                SessionState.SetBool(NpcOnlyKey, false);
                yield return DialogueWithCurrentNpc();
                End("COMPLETED_NPC_REVIEW_RESULTS");
                yield break;
            }
            // note: These facts are a diagnostic snapshot, not proposed gameplay state; unrelated ticking revisions cannot invalidate their reconstruction.
            string evidence = JsonConvert.SerializeObject(new { profileId = profile, worldId = world,
                goldBefore = 8, priceEach = 3, purchased = 2, arrows = 0, hasKey = false, destination = (string)null });
            string prompt = "Reconstruct the explicit evidence as JSON. Copy profileId/worldId exactly. remainingGold is goldBefore minus priceEach times purchased. Preserve arrows=0, hasKey=false and destination=null. Return only these six fields. Evidence: " + evidence;
            var schema = new Dictionary<string, object> {
                {"type", "object"}, {"additionalProperties", false},
                {"properties", new Dictionary<string, object> {
                    {"profileId", new {type="string"}}, {"worldId", new {type="string"}},
                    {"remainingGold", new {type="integer"}}, {"arrows", new {type="integer"}},
                    {"hasKey", new {type="boolean"}}, {"destination", new {type="null"}} }},
                {"required", new [] {"profileId","worldId","remainingGold","arrows","hasKey","destination"}} };
            yield return Call("main1", LLMGenerationCategory.StructuredState, prompt, schema, ValidateReconstruction);
            // note: Give an already queued real NPC batch a chance to start, so the next player-priority probe can witness nonterminal yielding.
            // This bounds observation only; it neither times out inference nor submits synthetic background work.
            bool npcObserved = false;
            for (int frame = 0; frame < 180 && Current(); frame++)
            {
                yield return null;
                if ((client.ActiveRequestDebugTag ?? string.Empty).StartsWith("GeneratedNpcPopulation:", StringComparison.Ordinal))
                { npcObserved = true; break; }
            }
            results.Add(new { label = "background-preemption-setup", npcObserved,
                observedRequestId = client.ActiveRequestId, observedDebugTag = client.ActiveRequestDebugTag });
            Write("RUNNING");
            yield return Call("summary1", LLMGenerationCategory.Summarization, prompt, schema, ValidateReconstruction);
            yield return Call("main2", LLMGenerationCategory.StructuredState, prompt, schema, ValidateReconstruction);
            yield return Call("summary2", LLMGenerationCategory.Summarization, prompt, schema, ValidateReconstruction);
            yield return Call("main3", LLMGenerationCategory.StructuredState, prompt, schema, ValidateReconstruction);
            string rejected = "{\"remainingGold\":8,\"arrows\":4,\"hasKey\":true,\"destination\":\"castle\"}";
            var repairBinding = new YQLlmRequest { debugTag = "GameplayAcceptance:repair", profileId = profile,
                worldId = world, generationEpoch = epoch, ownerId = owner,
                bindPlayerStateRevision = false, bindWorldStateRevision = false };
            YQRepairEpisode repairEpisode = client.CreateRepairEpisode(repairBinding, "read-only-reconstruction-repair", 0);
            yield return Call("repair", LLMGenerationCategory.StructuredState,
                YQRepairEpisode.RepairPrompt(prompt, "Wrong arithmetic; invented arrows, key and destination; missing identity fields.", rejected),
                schema, ValidateReconstruction, repairEpisode);
            yield return Call("roleplay", LLMGenerationCategory.Dialogue,
                "Roleplay a cautious fantasy archivist. Known facts: the traveler has no key; the destination is unknown; an old bridge may be unsafe. In two brief sentences, advise a cautious next step. Do not invent a destination or give the traveler a key. Do not use JSON or discuss being an AI.",
                null, text => !string.IsNullOrWhiteSpace(text) && text.Length < 1200);
            yield return DialogueWithCurrentNpc();
            // note: Report the real production gate. A missing qualification must remain a failed acceptance item, never be hidden by other passing calls.
            results.Add(new { label = "goddess-production-qualification", pass = client.GoddessSpeechPlanEnabled && client.GoddessSemanticRepairEnabled,
                author = client.GoddessSpeechPlanEnabled, reviewer = client.GoddessSemanticRepairEnabled,
                evidence = "Production admission gate only; no claim of Goddess delivery" });
            // note: A false owner must terminate without opening a model call, using the same cancellation guard as real NPC requests.
            cancelledOwner = true;
            yield return Call("retired-owner", LLMGenerationCategory.Dialogue, "This retired request must never dispatch.", null, text => false);
            End("COMPLETED_REVIEW_RESULTS");
        }

        private bool Current() => !ended && !cancelledOwner && Application.isPlaying &&
            YQServiceLifecycle.IsCurrent(epoch) && YQProfileSaveSystem.Instance != null &&
            YQProfileSaveSystem.Instance.ActiveProfileId == profile && WorldStateManager.Instance != null &&
            WorldStateManager.Instance.State.worldIdentity.worldId == world;

        private bool ValidateReconstruction(string text)
        {
            try
            {
                JObject data = JObject.Parse(text, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                return data.Count == 6 && data.Value<string>("profileId") == profile && data.Value<string>("worldId") == world &&
                    data["remainingGold"]?.Type == JTokenType.Integer && data.Value<int>("remainingGold") == 2 &&
                    data["arrows"]?.Type == JTokenType.Integer && data.Value<int>("arrows") == 0 &&
                    data["hasKey"]?.Type == JTokenType.Boolean && !data.Value<bool>("hasKey") && data["destination"]?.Type == JTokenType.Null;
            }
            catch (JsonException) { return false; }
        }

        private IEnumerator Call(string label, LLMGenerationCategory category, string prompt, Dictionary<string, object> schema, Func<string, bool> validate, YQRepairEpisode episode = null)
        {
            if (ended || client == null) yield break;
            bool completed = false;
            YQLlmRequestResult response = default;
            frameMs.Clear(); mainThreadMs.Clear();
            float started = Time.realtimeSinceStartup;
            var options = new Dictionary<string, object> {{"num_predict", category == LLMGenerationCategory.Dialogue ? 80 : 160}, {"temperature", .05f}, {"presence_penalty", 0f}};
            // note: Acceptance exercises the selected production placement; it must never silently force a GPU summary model onto CPU.
            var request = new YQLlmRequest {
                prompt = prompt, debugTag = "GameplayAcceptance:" + label, category = category,
                priority = YQLlmRequestPriority.PlayerFacing, protectPrompt = true, maxRetries = 0,
                requireJson = schema != null, jsonSchema = schema, optionsOverride = options,
                profileId = profile, worldId = world, generationEpoch = epoch, ownerId = owner,
                bindPlayerStateRevision = false, bindWorldStateRevision = false, ownerStillCurrent = Current
            };
            episode?.Bind(request);
            pending = client.Submit(request, result => { response = result; completed = true; });
            Debug.Log("[YQLlmGameplayAcceptance] Queued " + label + " request=" + pending + "; active=" + client.ActiveRequestDebugTag);
            // note: No generation timeout is introduced. Operator Stop or leaving Play Mode cancels only this diagnostic owner's pending request.
            while (!completed && !ended) yield return null;
            if (ended) yield break;
            pending = 0;
            bool retired = label == "retired-owner";
            // note: Direct-mode prefix insertion also sets `reduced`; compare the full normalized input to detect actual context loss.
            string normalizedInput = prompt.Replace("\r\n", "\n").Replace('\r', '\n');
            bool inputPreserved = response.compiledPrompt.prompt != null &&
                response.compiledPrompt.prompt.EndsWith(normalizedInput, StringComparison.Ordinal);
            bool pass = retired ? response.outcome == YQLlmTerminalOutcome.Superseded && response.generationSeconds == 0 :
                response.success && validate(response.text) && inputPreserved &&
                response.profileId == profile && response.worldId == world && response.generationEpoch == epoch;
            if (label == "repair")
            {
                // note: The deliberately rejected fixture uses the real episode admission/physical-call trace, without committing any repaired state.
                pass &= episode != null && episode.GeneratorCalls == 1 && !string.IsNullOrEmpty(response.repairRequestKey);
                episode?.Finish(pass ? "RepairedAccepted" : "Rejected");
            }
            results.Add(new {label, pass, response.success, outcome=response.outcome.ToString(), response.error, response.text,
                response.requestId, response.attemptCount, response.queueWaitSeconds, response.generationSeconds,
                elapsedSeconds=Time.realtimeSinceStartup-started, reduced=response.compiledPrompt.reduced, inputPreserved,
                response.repairEpisodeKey, response.repairRequestKey, repairCalls=episode?.GeneratorCalls ?? 0,
                response.profileId, response.worldId, response.generationEpoch, model=client.ActiveModelName,
                availableRamMiB=AvailableRamMiB(), frameSamples=frameMs.Count, frameMedianMs=Percentile(frameMs,.5), frameP95Ms=Percentile(frameMs,.95),
                profilerSamples=mainThreadMs.Count, mainThreadMedianMs=Percentile(mainThreadMs,.5), mainThreadP95Ms=Percentile(mainThreadMs,.95)});
            Write("RUNNING");
            Debug.Log("[YQLlmGameplayAcceptance] " + label + " pass=" + pass + " seconds=" + response.generationSeconds);
        }

        private IEnumerator DialogueWithCurrentNpc()
        {
            // note: Exercise the real prompt builder and reply validator against a loaded NPC; bypass transcript publication so accepted state remains untouched.
            NpcDialogueAgent selected = FindLiveNpc();
            if (selected == null || DialogueThinkService.Instance == null)
            {
                results.Add(new { label = "current-npc-dialogue", pass = false, error = "No live NPC/dialogue owner available." });
                Write("RUNNING");
                yield break;
            }
            bool completed = false;
            string line = null, npcId = selected.NpcId, persona = selected.BuildPersonaBlock();
            string before = selected.RenderRecentDialogue(12);
            var transport = new List<object>();
            dialogueObserver = response => {
                if (response.debugTag != "Dialogue:" + npcId && response.debugTag != "DialogueRepair:" + npcId) return;
                transport.Add(new { response.requestId, response.debugTag, response.success,
                    outcome = response.outcome.ToString(), response.error, response.text, response.attemptCount,
                    response.queueWaitSeconds, response.generationSeconds, response.profileId, response.worldId,
                    response.generationEpoch, model = client.ActiveModelName, prompt = response.compiledPrompt.prompt });
            };
            client.RequestCompleted += dialogueObserver;
            frameMs.Clear(); mainThreadMs.Clear(); pending = -1;
            float started = Time.realtimeSinceStartup;
            DialogueThinkService.Instance.RequestNpcReply(selected, "What is your work here, and what local trouble should I watch for?",
                value => { line = value; completed = true; }, Current);
            while (!completed && Current() && selected != null && selected.isActiveAndEnabled) yield return null;
            client.RequestCompleted -= dialogueObserver; dialogueObserver = null; pending = 0;
            bool transcriptUnchanged = selected != null && before == selected.RenderRecentDialogue(12);
            // note: A nonempty callback is insufficient: malformed JSON must never be counted as delivered speech.
            bool spokenLine = !string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("{", StringComparison.Ordinal) &&
                line.IndexOf("\"npcText\"", StringComparison.OrdinalIgnoreCase) < 0;
            results.Add(new { label = "current-npc-dialogue", pass = completed && spokenLine && transcriptUnchanged,
                npcId, persona, text = line, transcriptUnchanged, transport, elapsedSeconds = Time.realtimeSinceStartup - started,
                frameSamples = frameMs.Count, frameMedianMs = Percentile(frameMs,.5), frameP95Ms = Percentile(frameMs,.95),
                evidence = "ASSISTED_LIVE_DIALOGUE_SERVICE_WITH_ACTUAL_NPC_CONTEXT_NOT_UI_OR_TRANSCRIPT_COMMIT" });
            Write("RUNNING");
        }

        private IEnumerator PracticalSpeech()
        {
            // note: The production default is exercised unchanged. Accepted gameplay records remain authoritative; only ordinary dialogue history is published.
            var requests = new List<object>();
            dialogueObserver = r => requests.Add(new { r.debugTag, r.success, r.error, r.text, r.generationSeconds,
                r.queueWaitSeconds, r.repairEpisodeKey, r.repairRequestKey,
                model = client.ActiveModelName, prompt = r.compiledPrompt.prompt });
            client.RequestCompleted += dialogueObserver;
            NpcDialogueAgent npc = FindLiveNpc();
            if (npc != null)
            {
                EntityInfo info = npc.GetComponentInParent<EntityInfo>();
                bool uiOpened = YQInvestorDialogueUI.Instance != null && YQInvestorDialogueUI.Instance.OpenNpcForValidation(info, npc);
                foreach (string question in new[] { "Tell me about your trade.", "Do you have a confirmed report of danger on the road?" })
                {
                    bool done = false; string text = null; float began = Time.realtimeSinceStartup;
                    npc.SendPlayerMessage(question, reply => { text = reply; done = true; });
                    while (!done && Current() && npc != null && npc.isActiveAndEnabled) yield return null;
                    bool persisted = npc != null && npc.RenderRecentDialogue(12).Contains(text ?? "\0");
                    results.Add(new { label = "practical-npc", question, text, uiOpened, persisted,
                        pass = done && !string.IsNullOrWhiteSpace(text) && persisted,
                        elapsedSeconds = Time.realtimeSinceStartup - began, persona = npc != null ? npc.BuildPersonaBlock() : "",
                        evidence = "ASSISTED_ORDINARY_NPC_SEND_TRANSCRIPT_PUBLICATION_NOT_HUMAN_VISUAL_REVIEW" });
                    Write("RUNNING");
                }
            }
            YQInvestorDialogueUI.Instance?.ForceCloseFromBootstrap();
            foreach (var item in new[] {
                new { label="practical-goddess-guidance", task="What is my current quest, and what should I do next?", draft=(string)null },
                new { label="practical-goddess-unknown", task="Is the ferry safe right now, and exactly when does it close? Admit any unknown details.", draft=(string)null },
                new { label="practical-goddess-repair", task="What do you know about the ferry's closing time?", draft="The ferry closes at midnight." } })
            {
                JObject evidence = YQGoddessLoadingVoice.CaptureKnownContext(PlayerStateManager.Instance.state, WorldStateManager.Instance.State);
                YQGoddessSpeech.Result response = null; float began = Time.realtimeSinceStartup;
                if (item.draft == null)
                    YQGoddessSpeech.Request(client, evidence, item.task, owner + item.label, Current, r => response = r);
                else YQGoddessSpeech.ReviewExisting(client, evidence, item.task, item.draft, owner + item.label, Current, r => response = r);
                while (response == null && Current()) yield return null;
                var director = UnityEngine.Object.FindFirstObjectByType<YQInvestorDirector>();
                bool approved = response != null && response.canDeliver && YQGoddessSpeech.IsApproved(response.line, evidence);
                if (approved && director != null)
                    typeof(YQInvestorDirector).GetMethod("PublishGeneratedGoddessLine", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(director, new object[] { JsonConvert.SerializeObject(new { goddessLine = response.line }) });
                yield return new WaitForSecondsRealtime(.35f);
                var hud = UnityEngine.Object.FindFirstObjectByType<YourQuestTutorialHud>();
                var visibleText = hud == null ? null : typeof(YourQuestTutorialHud).GetField("_goddessSpeechText", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(hud) as TMPro.TMP_Text;
                bool hudTextVisible = response != null && hud != null && hud.IsPresentationVisible && visibleText != null &&
                    visibleText.isActiveAndEnabled && visibleText.text == response.line;
                bool published = response != null && director != null && director.LastDirectorMessage == response.line;
                bool recorded = response != null && PlayerStateManager.Instance.state.goddessVoiceMemory.Contains(response.line);
                results.Add(new { label=item.label, item.task, item.draft, text=response?.line, response?.disposition, response?.isFallback,
                    approved, published, recorded, hudTextVisible, pass=approved && published && recorded && hudTextVisible,
                    elapsedSeconds=Time.realtimeSinceStartup-began, facts=evidence["facts"],
                    evidence="ASSISTED_REAL_GODDESS_DELIVERY_GATE_HISTORY_SAVE_AND_HUD_OWNER_NOT_HUMAN_VISUAL_REVIEW" });
                Write("RUNNING");
            }
            client.RequestCompleted -= dialogueObserver; dialogueObserver = null;
            results.Add(new { label="practical-transport", bestEffort=client.BestEffortSpeechEnabled, requests });
        }

        private static long AvailableRamMiB()
        {
            // note: The runtime policy is internal to another assembly; this is diagnostic access only, never a config or state write.
            Type type = typeof(LLMClient).Assembly.GetType("YQLlmResidencyPolicy");
            var method = type?.GetMethod("AvailableSystemMemoryBytes", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            return method != null ? (long)method.Invoke(null, null) / (1024*1024) : -1;
        }
        private void Update()
        {
            if (ended || pending == 0 || frameMs.Count >= 36000) return;
            frameMs.Add(Time.unscaledDeltaTime * 1000f);
            if (mainThread.Valid && mainThread.LastValue > 0) mainThreadMs.Add(mainThread.LastValue / 1000000d);
        }
        private static double Percentile<T>(List<T> samples, double fraction) where T : IConvertible
        {
            if (samples.Count == 0) return -1;
            var sorted = new List<double>(samples.Count);
            foreach (T sample in samples) sorted.Add(sample.ToDouble(null));
            sorted.Sort(); return sorted[Math.Min(sorted.Count-1, (int)Math.Floor((sorted.Count-1)*fraction))];
        }
        private void Write(string status)
        {
            if (folder == null) return;
            File.WriteAllText(Path.Combine(folder, "Receipt.json"), JsonConvert.SerializeObject(new {
                utc=DateTime.UtcNow.ToString("O"), status, profile, world, epoch, results,
                evidence="LIVE_SCHEDULER_IN_RENDERING_PLAYSAFE_READ_ONLY_FIXTURES",
                limits=publishesSpeechHistory ? "Disposable benchmark profile: ordinary NPC transcript and Goddess speech-history saves are exercised. UI component state checked, not human visual review. No new item/quest/world records proposed." : "No canonical commits or profile changes. Roleplay needs human semantic review. Frame samples include queue waiting and Editor overhead; no player-build or paired before/after performance claim."
            }, Formatting.Indented));
        }
        public void End(string status)
        {
            if (ended) return;
            ended = true;
            if (pending > 0 && client != null) client.CancelRequest(pending, "Diagnostic owner stopped.");
            if (client != null && dialogueObserver != null) client.RequestCompleted -= dialogueObserver;
            mainThread.Dispose(); Write(status);
            Debug.Log("[YQLlmGameplayAcceptance] " + status + " receipt=" + folder);
            if (active == this) active = null;
            Destroy(gameObject);
        }
        private void OnDestroy() { if (!ended) End("PLAY_SESSION_ENDED"); }
    }
}
#endif
