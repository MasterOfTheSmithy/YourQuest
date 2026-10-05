#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

// note: This one-shot verifier exercises scheduler ownership without creating a scene, save record, or second gameplay authority.
public static class YQLlmSchedulerRuntimeVerification
{
    public static bool IsRunning { get; private set; }

    public static bool RunFromPlaySafe()
    {
        if (!Application.isPlaying || IsRunning || LLMClient.Instance == null)
            return false;

        IsRunning = true;
        GameObject host = new GameObject("G02_LlmSchedulerRuntimeVerification");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<Runner>();
        return true;
    }

    private sealed class Runner : MonoBehaviour
    {
        private readonly List<string> _evidence = new List<string>();
        private LLMClient _client;
        private LLMRuntimeConfig _fixtureConfig;
        private string _originalApiUrl;
        private string _originalModel;
        private LLMRuntimeConfig _originalRuntimeConfig;
        private LLMRuntimeConfig _originalActiveConfig;
        private bool _originalUsingRuntimeDefault;
        private bool _originalBackendResolved;

        private IEnumerator Start()
        {
            // note: Wait for the actual PlaySafe runtime owner instead of guessing from editor process presence.
            while (Application.isPlaying && LLMClient.Instance == null)
                yield return null;

            if (!Application.isPlaying || LLMClient.Instance == null)
            {
                Finish("BLOCKED: LLMClient was not present in the enabled PlaySafe runtime.");
                yield break;
            }

            _client = LLMClient.Instance;
            yield return WaitForIdle(10f);
            CaptureClientConfig();

            bool schedulerPass = false;
            yield return RunSchedulerFixture(result => schedulerPass = result);
            RestoreClientConfig();

            bool profileFaultPass = false;
            yield return RunProfileSwitchAndDisconnectFixture(result => profileFaultPass = result);
            RestoreClientConfig();

            // note: This request uses the restored configured model and has no state callback, proving integration without mutating gameplay canon.
            bool liveModelPass = false;
            bool liveModelDone = false;
            long liveRequestId = 0;
            liveRequestId = _client.Submit(new YQLlmRequest
            {
                prompt = "Return exactly this JSON object and no other text: {\"status\":\"ok\"}",
                debugTag = "G02LiveConfiguredModelReceipt",
                category = LLMGenerationCategory.StructuredState,
                priority = YQLlmRequestPriority.Background,
                requireJson = true,
                bindPlayerStateRevision = false,
                bindWorldStateRevision = false,
                maxRetries = 0,
                optionsOverride = new Dictionary<string, object>
                {
                    { "num_predict", 64 },
                    { "request_timeout_seconds", 30 }
                }
            }, result =>
            {
                liveModelPass = result.success && result.outcome == YQLlmTerminalOutcome.AcceptedResponse;
                liveModelDone = true;
                _evidence.Add("live-model id=" + liveRequestId + " outcome=" + result.outcome + " success=" + result.success + " textChars=" + (result.text != null ? result.text.Length : 0));
            });

            float deadline = Time.realtimeSinceStartup + 35f;
            while (Application.isPlaying && !liveModelDone && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!liveModelDone)
            {
                _client.CancelRequest(liveRequestId, "G02 live-model receipt deadline elapsed.");
                _evidence.Add("live-model id=" + liveRequestId + " outcome=NOT_YET_TESTABLE success=False timeout=35s");
            }

            string status = schedulerPass && profileFaultPass && liveModelPass ? "PASS" : "PARTIAL";
            Finish(status + ": scheduler=" + schedulerPass + " profileFaults=" + profileFaultPass + " liveModel=" + liveModelPass);
        }

        private IEnumerator RunSchedulerFixture(Action<bool> completed)
        {
            bool pass = true;
            ApplyFixtureConfig(false, string.Empty);

            List<YQLlmTerminalOutcome> evictedResults = new List<YQLlmTerminalOutcome>();
            List<long> backgroundIds = new List<long>();
            _client.BeginExclusiveSequence("G02-queue-fixture");
            for (int index = 0; index < 16; index++)
            {
                long id = _client.Submit(new YQLlmRequest
                {
                    prompt = "fixture background " + index,
                    debugTag = "G02QueueBackground" + index,
                    category = LLMGenerationCategory.StructuredState,
                    priority = YQLlmRequestPriority.Background,
                    maxRetries = 0
                }, result => evictedResults.Add(result.outcome));
                backgroundIds.Add(id);
            }

            long highId = _client.Submit(new YQLlmRequest
            {
                prompt = "fixture high priority",
                debugTag = "G02QueueHigh",
                category = LLMGenerationCategory.Dialogue,
                priority = YQLlmRequestPriority.PlayerFacing,
                maxRetries = 0
            }, result => evictedResults.Add(result.outcome));

            bool evicted = _client.QueueEvictionCount > 0 && evictedResults.Contains(YQLlmTerminalOutcome.Evicted);
            bool cancelled = backgroundIds.Count > 1 && _client.CancelRequest(backgroundIds[1], "G02 queue cancellation fixture");
            _client.EndExclusiveSequence("G02-queue-fixture");
            _evidence.Add("queue priority eviction=" + evicted + " cancellation=" + cancelled + " highId=" + highId);
            pass &= evicted && cancelled;

            float deadline = Time.realtimeSinceStartup + 8f;
            while (Application.isPlaying && _client.IsBusy && Time.realtimeSinceStartup < deadline)
                yield return null;

            List<string> fairnessOrder = new List<string>();
            _client.BeginExclusiveSequence("G02-fairness-fixture");
            for (int index = 0; index < 4; index++)
            {
                int captured = index;
                _client.Submit(new YQLlmRequest
                {
                    prompt = "fixture high " + captured,
                    debugTag = "G02FairHigh" + captured,
                    category = LLMGenerationCategory.Dialogue,
                    priority = YQLlmRequestPriority.PlayerFacing,
                    maxRetries = 0
                }, result => fairnessOrder.Add("H" + captured));
            }
            _client.Submit(new YQLlmRequest
            {
                prompt = "fixture normal",
                debugTag = "G02FairNormal",
                category = LLMGenerationCategory.StructuredState,
                priority = YQLlmRequestPriority.Background,
                maxRetries = 0
            }, result => fairnessOrder.Add("N"));
            _client.EndExclusiveSequence("G02-fairness-fixture");
            deadline = Time.realtimeSinceStartup + 8f;
            while (Application.isPlaying && _client.IsBusy && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool fair = fairnessOrder.Count >= 5 && fairnessOrder[0] == "H0" && fairnessOrder[1] == "H1" && fairnessOrder[2] == "H2" && fairnessOrder[3] == "N" && fairnessOrder[4] == "H3";
            _evidence.Add("fairness order=" + string.Join(",", fairnessOrder.ToArray()) + " pass=" + fair);
            pass &= fair;

            // note: A lifecycle transition invalidates a queued request and proves stale work gets a terminal result before replacement state can be loaded.
            bool stale = false;
            _client.BeginExclusiveSequence("G02-stale-fixture");
            _client.Submit(new YQLlmRequest
            {
                prompt = "fixture stale request",
                debugTag = "G02StaleOwnership",
                category = LLMGenerationCategory.StructuredState,
                priority = YQLlmRequestPriority.StartupExclusive,
                exclusiveOwner = "G02-stale-fixture",
                maxRetries = 0
            }, result => stale = result.outcome == YQLlmTerminalOutcome.Superseded);
            YQServiceLifecycle.BeginProfileSession("G02-disposable-switch");
            _evidence.Add("stale ownership pass=" + stale);
            pass &= stale;
            completed?.Invoke(pass);
        }

        private IEnumerator WaitForIdle(float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (Application.isPlaying && _client != null && _client.IsBusy && Time.realtimeSinceStartup < deadline)
                yield return null;
        }

        private IEnumerator RunProfileSwitchAndDisconnectFixture(Action<bool> completed)
        {
            YQProfileSaveSystem profiles = YQProfileSaveSystem.Instance;
            string originalProfileId = profiles != null ? profiles.ActiveProfileId : string.Empty;
            if (profiles == null || string.IsNullOrWhiteSpace(originalProfileId))
            {
                _evidence.Add("profile-switch BLOCKED: no active profile owner");
                completed?.Invoke(false);
                yield break;
            }

            // note: Create one explicitly named disposable profile through the production profile owner, then restore the original before fault injection.
            string disposableProfileId = profiles.CreateNewProfile("G02 Disposable Scheduler");
            bool prepared = !string.IsNullOrWhiteSpace(disposableProfileId) && profiles.LoadProfile(originalProfileId);
            if (!prepared)
            {
                _evidence.Add("profile-switch BLOCKED: disposable profile preparation failed");
                if (!string.IsNullOrWhiteSpace(disposableProfileId))
                    profiles.DeleteProfile(disposableProfileId);
                completed?.Invoke(false);
                yield break;
            }

            bool stale = false;
            bool requestDone = false;
            ApplyFixtureConfig(true, _originalRuntimeConfig != null ? _originalRuntimeConfig.ollamaApiUrl : _originalApiUrl);
            long requestId = 0;
            requestId = _client.Submit(new YQLlmRequest
            {
                prompt = "Return a short JSON status object after considering this disposable profile-switch fixture.",
                debugTag = "G02ProfileSwitchActiveRequest",
                category = LLMGenerationCategory.StructuredState,
                priority = YQLlmRequestPriority.Background,
                maxRetries = 0,
                optionsOverride = new Dictionary<string, object>
                {
                    { "num_predict", 256 },
                    { "request_timeout_seconds", 30 }
                }
            }, result =>
            {
                stale = result.outcome == YQLlmTerminalOutcome.Superseded;
                requestDone = true;
                _evidence.Add("profile-switch id=" + requestId + " outcome=" + result.outcome + " success=" + result.success);
            });

            // note: Wait until the request owns the active transport, making the profile load a real mid-request lifecycle transition.
            float deadline = Time.realtimeSinceStartup + 8f;
            while (Application.isPlaying && !requestDone && _client.ActiveRequestId != requestId && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool switched = !requestDone && profiles.LoadProfile(disposableProfileId);
            deadline = Time.realtimeSinceStartup + 8f;
            while (Application.isPlaying && !requestDone && Time.realtimeSinceStartup < deadline)
                yield return null;

            bool restored = profiles.LoadProfile(originalProfileId);
            bool deleted = profiles.DeleteProfile(disposableProfileId);
            bool switchPass = switched && restored && deleted && requestDone && stale;
            _evidence.Add("profile-switch prepared=" + prepared + " switched=" + switched + " restored=" + restored + " deleted=" + deleted + " pass=" + switchPass);

            // note: Point the same scheduler at a disconnected local endpoint and verify failure remains terminal before restoring the configured service.
            bool disconnectedDone = false;
            bool disconnectedFailed = false;
            ApplyFixtureConfig(true, "http://127.0.0.1:1");
            long disconnectedId = 0;
            disconnectedId = _client.Submit(new YQLlmRequest
            {
                prompt = "Return exactly {\"status\":\"disconnect-fixture\"}.",
                debugTag = "G02ConfiguredModelDisconnect",
                category = LLMGenerationCategory.StructuredState,
                priority = YQLlmRequestPriority.Background,
                requireJson = true,
                bindPlayerStateRevision = false,
                bindWorldStateRevision = false,
                maxRetries = 0,
                optionsOverride = new Dictionary<string, object>
                {
                    { "num_predict", 64 },
                    { "request_timeout_seconds", 2 }
                }
            }, result =>
            {
                disconnectedFailed = !result.success && result.outcome == YQLlmTerminalOutcome.Failed;
                disconnectedDone = true;
                _evidence.Add("disconnect id=" + disconnectedId + " outcome=" + result.outcome + " success=" + result.success);
            });
            deadline = Time.realtimeSinceStartup + 6f;
            while (Application.isPlaying && !disconnectedDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            bool disconnectPass = disconnectedDone && disconnectedFailed;
            _evidence.Add("disconnect terminal pass=" + disconnectPass);

            // note: Withhold the service response past a legacy one-second override; only explicit cancellation may retire this pending request.
            bool timeoutDone = false;
            bool timeoutFailed = false;
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int timeoutPort = ((IPEndPoint)listener.LocalEndpoint).Port;
            Task.Run(() =>
            {
                try
                {
                    using (TcpClient accepted = listener.AcceptTcpClient())
                        Thread.Sleep(3500);
                }
                catch (SocketException)
                {
                    // note: Stopping the listener after explicit cancellation is expected fixture cleanup.
                }
                catch (ObjectDisposedException)
                {
                    // note: Disposal races are harmless once the cancelled terminal result has been observed.
                }
            });
            ApplyFixtureConfig(true, "http://127.0.0.1:" + timeoutPort);
            long timeoutId = 0;
            int timeoutCountBefore = _client.TimeoutFailureCount;
            timeoutId = _client.Submit(new YQLlmRequest
            {
                prompt = "Return exactly {\"status\":\"timeout-fixture\"}.",
                debugTag = "G02ConfiguredModelTimeout",
                category = LLMGenerationCategory.StructuredState,
                priority = YQLlmRequestPriority.Background,
                requireJson = true,
                bindPlayerStateRevision = false,
                bindWorldStateRevision = false,
                maxRetries = 0,
                optionsOverride = new Dictionary<string, object>
                {
                    { "num_predict", 64 },
                    { "request_timeout_seconds", 1 }
                }
            }, result =>
            {
                timeoutFailed = !result.success && result.outcome == YQLlmTerminalOutcome.Cancelled && _client.TimeoutFailureCount == timeoutCountBefore;
                timeoutDone = true;
                _evidence.Add("timeout id=" + timeoutId + " outcome=" + result.outcome + " success=" + result.success + " classified=" + timeoutFailed);
            });
            deadline = Time.realtimeSinceStartup + 1.5f;
            while (Application.isPlaying && !timeoutDone && Time.realtimeSinceStartup < deadline)
                yield return null;
            bool remainedPending = !timeoutDone;
            _client.CancelRequest(timeoutId, "Slow-service fixture explicitly cancelled");
            bool timeoutPass = remainedPending && timeoutDone && timeoutFailed;
            listener.Stop();
            _evidence.Add("no automatic timeout; explicit cancellation pass=" + timeoutPass);
            completed?.Invoke(switchPass && disconnectPass && timeoutPass);
        }

        private void CaptureClientConfig()
        {
            _originalRuntimeConfig = _client.runtimeConfig;
            _originalApiUrl = _client.apiUrl;
            _originalModel = _client.model;
            _originalActiveConfig = GetPrivate<LLMRuntimeConfig>("_activeConfig");
            _originalUsingRuntimeDefault = GetPrivate<bool>("_usingRuntimeDefaultConfig");
            _originalBackendResolved = GetPrivate<bool>("_runtimeBackendResolved");
        }

        private void ApplyFixtureConfig(bool enabled, string apiUrl)
        {
            _fixtureConfig = ScriptableObject.CreateInstance<LLMRuntimeConfig>();
            _fixtureConfig.enableRuntimeLlm = enabled;
            _fixtureConfig.backend = YQLlmBackend.Ollama;
            _fixtureConfig.ollamaApiUrl = string.IsNullOrWhiteSpace(apiUrl) ? "http://127.0.0.1:11434" : apiUrl;
            _fixtureConfig.ollamaModel = _originalModel;
            _fixtureConfig.maxQueueDepth = 16;
            _fixtureConfig.maxConsecutiveHighPriorityRequests = 3;
            _fixtureConfig.transientRequestRetries = 0;
            _fixtureConfig.staggerResponseHandoffAcrossFrames = false;
            _client.runtimeConfig = null;
            SetPrivate("_activeConfig", _fixtureConfig);
            SetPrivate("_usingRuntimeDefaultConfig", false);
            SetPrivate("_runtimeBackendResolved", true);
        }

        private void RestoreClientConfig()
        {
            _client.runtimeConfig = _originalRuntimeConfig;
            _client.apiUrl = _originalApiUrl;
            _client.model = _originalModel;
            SetPrivate("_activeConfig", _originalActiveConfig);
            SetPrivate("_usingRuntimeDefaultConfig", _originalUsingRuntimeDefault);
            SetPrivate("_runtimeBackendResolved", _originalBackendResolved);
            if (_fixtureConfig != null)
                Destroy(_fixtureConfig);
            _fixtureConfig = null;
        }

        private T GetPrivate<T>(string fieldName)
        {
            FieldInfo field = typeof(LLMClient).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return field != null ? (T)field.GetValue(_client) : default;
        }

        private void SetPrivate(string fieldName, object value)
        {
            FieldInfo field = typeof(LLMClient).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            field?.SetValue(_client, value);
        }

        private void Finish(string summary)
        {
            RestoreClientConfig();
            StringBuilder receipt = new StringBuilder();
            receipt.AppendLine("G02 LLM scheduler runtime receipt");
            receipt.AppendLine("status=" + summary);
            receipt.AppendLine("profile=" + (YQProfileSaveSystem.Instance != null ? YQProfileSaveSystem.Instance.ActiveProfileId : "<none>"));
            receipt.AppendLine("epoch=" + YQServiceLifecycle.RequestEpoch);
            receipt.AppendLine("queueDepth=" + (_client != null ? _client.PendingRequestCount.ToString() : "<none>"));
            receipt.AppendLine("evictions=" + (_client != null ? _client.QueueEvictionCount.ToString() : "<none>"));
            receipt.AppendLine("cancellations=" + (_client != null ? _client.CancellationCount.ToString() : "<none>"));
            receipt.AppendLine("malformed=" + (_client != null ? _client.MalformedResponseCount.ToString() : "<none>"));
            for (int index = 0; index < _evidence.Count; index++)
                receipt.AppendLine(_evidence[index]);
            File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "YQ_LLM_Scheduler_Runtime.receipt"), receipt.ToString());
            Debug.Log("[YQLlmSchedulerRuntimeVerification] " + summary + "\n" + receipt);
            IsRunning = false;
            Destroy(gameObject);
        }
    }
}
#endif
