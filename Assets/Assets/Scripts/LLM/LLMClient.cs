// Assets/Assets/Scripts/LLM/LLMClient.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public sealed class OllamaRequest
{
    public string model;
    public string prompt;
    public bool stream;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public object format;
    public Dictionary<string, object> options;
}

[DisallowMultipleComponent]
public sealed class LLMClient : MonoBehaviour
{
    public static LLMClient Instance { get; private set; }
    private const string LocalRequestTimeoutSecondsOption = "request_timeout_seconds";

    [Header("Runtime Config")]
    public LLMRuntimeConfig runtimeConfig;

    [Header("Legacy Ollama Fields")]
    public string model = "llama3.1";
    public string apiUrl = "http://127.0.0.1:11434";

    [Header("Request Safety")]
    [Min(0)] public int requestTimeoutSeconds = 180;
    [Min(5)] public float maxQueuedRequestAgeSeconds = 90f;
    [Min(64)] public int numPredict = 300;
    [Range(2048, 16384)] public int contextLength = 6144;

    [Header("Debug")]
    public bool logRequestSummaries = true;
    public bool logRequestJson = false;
    public bool logRawModelText = false;
    [Min(256)] public int maxLoggedPayloadCharacters = 1600;

    public bool IsBusy =>
        _processing ||
        _exclusiveQueue.Count > 0 ||
        _highPriorityQueue.Count > 0 ||
        _normalQueue.Count > 0;

    public bool HasPendingHighPriorityRequests => _highPriorityQueue.Count > 0;

    public int PendingRequestCount =>
        _exclusiveQueue.Count +
        _highPriorityQueue.Count +
        _normalQueue.Count;

    public bool LastRequestFailed { get; private set; }
    public string LastError { get; private set; } = string.Empty;
    public YQLlmRuntimeState RuntimeState { get; private set; } = YQLlmRuntimeState.Disabled;
    public int SuccessfulRequestCount { get; private set; }
    public int FailedRequestCount { get; private set; }
    public YQLlmRequestResult LastCompletedRequest { get; private set; }

    // note: A single completion stream gives UI, telemetry, and gameplay systems one truthful LLM status surface.
    public event Action<YQLlmRequestResult> RequestCompleted;

    public bool IsExclusiveSequenceActive => !string.IsNullOrWhiteSpace(_exclusiveSequenceOwner);
    public string ExclusiveSequenceOwner => _exclusiveSequenceOwner;

    private struct QueuedRequest
    {
        public long id;
        public string prompt;
        public Action<string> onResponse;
        public Action<YQLlmRequestResult> onCompleted;
        public string debugTag;
        public Dictionary<string, object> optionsOverride;
        public LLMGenerationCategory category;
        public bool requireJson;
        // note: Preserve JSON response formatting while allowing the owning domain validator to strip malformed optional prose.
        public bool deferJsonValidationToCaller;
        public int maxRetries;
        public int attempt;

        // note: Queue age lets background requests expire instead of piling onto the model after generation.
        public float queuedAt;
        public float firstQueuedAt;

        public bool exclusive;
        public bool highPriority;
        public string exclusiveOwner;
        public bool disableTimeout;
    }

    private readonly Queue<QueuedRequest> _exclusiveQueue = new Queue<QueuedRequest>();
    private readonly Queue<QueuedRequest> _highPriorityQueue = new Queue<QueuedRequest>();
    private readonly Queue<QueuedRequest> _normalQueue = new Queue<QueuedRequest>();

    private LLMRuntimeConfig _activeConfig;
    private LlamaCppServerProcess _llamaServer;
    private string _exclusiveSequenceOwner = string.Empty;
    private bool _processing;
    private bool _quitting;
    private long _nextRequestId = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _activeConfig = runtimeConfig != null
            ? runtimeConfig
            : LLMRuntimeConfig.CreateRuntimeDefault();

        _llamaServer = new LlamaCppServerProcess();
        RuntimeState = _activeConfig.enableRuntimeLlm ? YQLlmRuntimeState.Starting : YQLlmRuntimeState.Disabled;
    }

    private void OnApplicationQuit()
    {
        _quitting = true;
        DisposeOwnedRuntime();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        DisposeOwnedRuntime();
    }

    public bool BeginExclusiveSequence(string owner)
    {
        string normalizedOwner = string.IsNullOrWhiteSpace(owner) ? string.Empty : owner.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOwner))
        {
            Debug.LogWarning("[LLMClient] Cannot begin an exclusive sequence without an owner.");
            return false;
        }

        if (IsExclusiveSequenceActive)
        {
            if (string.Equals(_exclusiveSequenceOwner, normalizedOwner, StringComparison.Ordinal))
                return true;

            Debug.LogWarning(
                "[LLMClient] Exclusive sequence '" +
                _exclusiveSequenceOwner +
                "' is already active. '" +
                normalizedOwner +
                "' will not replace it.");
            return false;
        }

        _exclusiveSequenceOwner = normalizedOwner;
        Debug.Log("[LLMClient] EXCLUSIVE SEQUENCE BEGIN: " + _exclusiveSequenceOwner);
        EnsureQueueProcessorRunning();
        return true;
    }

    public void EndExclusiveSequence(string owner)
    {
        if (!IsExclusiveSequenceActive)
            return;

        string normalizedOwner = owner != null ? owner.Trim() : string.Empty;
        if (!string.Equals(_exclusiveSequenceOwner, normalizedOwner, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                "[LLMClient] Ignored exclusive-sequence release from '" +
                (owner ?? "<null>") +
                "' because current owner is '" +
                _exclusiveSequenceOwner +
                "'.");
            return;
        }

        Debug.Log("[LLMClient] EXCLUSIVE SEQUENCE END: " + _exclusiveSequenceOwner);
        _exclusiveSequenceOwner = string.Empty;
        EnsureQueueProcessorRunning();
    }

    public void GenerateSkill(string prompt, Action<string> onResponse)
    {
        Enqueue(prompt, onResponse, "GenerateSkill");
    }

    public void SendOnce(string prompt, Action<string> onResponse, string debugTag = null)
    {
        Enqueue(prompt, onResponse, string.IsNullOrWhiteSpace(debugTag) ? "SendOnce" : debugTag);
    }

    public void Submit(YQLlmRequest request, Action<YQLlmRequestResult> onComplete)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.prompt))
        {
            CompleteDirectFailure(onComplete, request, "LLM request prompt was empty.");
            return;
        }

        string tag = string.IsNullOrWhiteSpace(request.debugTag) ? "LLMRequest" : request.debugTag.Trim();
        bool exclusive = request.priority == YQLlmRequestPriority.StartupExclusive;
        bool important = exclusive || request.priority == YQLlmRequestPriority.PlayerFacing;
        string owner = string.IsNullOrWhiteSpace(request.exclusiveOwner) ? string.Empty : request.exclusiveOwner.Trim();

        if (exclusive && string.IsNullOrWhiteSpace(owner))
        {
            CompleteDirectFailure(onComplete, request, "Exclusive LLM requests require an explicit sequence owner.");
            return;
        }

        if (exclusive && !IsExclusiveSequenceActive)
            BeginExclusiveSequence(owner);

        if (exclusive && !string.Equals(_exclusiveSequenceOwner, owner, StringComparison.Ordinal))
        {
            CompleteDirectFailure(onComplete, request, "Another exclusive LLM sequence currently owns the runtime.");
            return;
        }

        if (!TryReserveQueueSlot(important, out string queueError))
        {
            RecordFailure(queueError, tag);
            CompleteDirectFailure(onComplete, request, queueError);
            return;
        }

        float now = Time.unscaledTime;
        QueuedRequest queued = new QueuedRequest
        {
            id = _nextRequestId++,
            prompt = request.prompt,
            onCompleted = onComplete,
            debugTag = tag,
            optionsOverride = request.optionsOverride,
            category = request.category,
            requireJson = request.requireJson,
            deferJsonValidationToCaller = request.deferJsonValidationToCaller,
            maxRetries = request.maxRetries,
            attempt = 0,
            queuedAt = now,
            firstQueuedAt = now,
            exclusive = exclusive,
            highPriority = important,
            exclusiveOwner = owner,
            disableTimeout = request.disableTimeout
        };

        QueueRequest(queued, important);
    }

    public void Enqueue(string prompt, Action<string> onResponse, string debugTag = null)
    {
        Enqueue(prompt, onResponse, debugTag, optionsOverride: null);
    }

    public void Enqueue(string prompt, Action<string> onResponse, string debugTag, Dictionary<string, object> optionsOverride)
    {
        EnqueueInternal(prompt, onResponse, debugTag, optionsOverride, IsHighPriorityTag(debugTag), false, string.Empty, false);
    }

    public void EnqueuePriority(
        string prompt,
        Action<string> onResponse,
        string debugTag,
        Dictionary<string, object> optionsOverride,
        bool highPriority)
    {
        EnqueueInternal(prompt, onResponse, debugTag, optionsOverride, highPriority, false, string.Empty, false);
    }

    public void Enqueue(string prompt, Action<string> onResponse, string debugTag, object optionsOverride)
    {
        Enqueue(prompt, onResponse, debugTag, optionsOverride as Dictionary<string, object>);
    }

    public void EnqueueExclusive(
        string prompt,
        Action<string> onResponse,
        string debugTag,
        Dictionary<string, object> optionsOverride,
        string exclusiveOwner,
        bool disableTimeout = true)
    {
        string owner = string.IsNullOrWhiteSpace(exclusiveOwner) ? string.Empty : exclusiveOwner.Trim();
        if (string.IsNullOrWhiteSpace(owner))
        {
            Debug.LogWarning("[LLMClient] EnqueueExclusive received no owner; falling back to ordinary queued execution.");
            Enqueue(prompt, onResponse, debugTag, optionsOverride);
            return;
        }

        if (!IsExclusiveSequenceActive)
            BeginExclusiveSequence(owner);

        if (!string.Equals(_exclusiveSequenceOwner, owner, StringComparison.Ordinal))
        {
            Debug.LogWarning(
                "[LLMClient] Exclusive request '" +
                (debugTag ?? "LLMRequest") +
                "' belongs to '" +
                owner +
                "' but current owner is '" +
                _exclusiveSequenceOwner +
                "'. Request preserved for later execution.");

            Enqueue(prompt, onResponse, debugTag, optionsOverride);
            return;
        }

        EnqueueInternal(prompt, onResponse, debugTag, optionsOverride, true, true, owner, disableTimeout);
    }

    public void SafeInvoke(Action<string> cb, string value, string debugTag)
    {
        if (cb == null)
            return;

        try
        {
            cb.Invoke(value);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LLMClient] onResponse callback threw" + FormatTag(debugTag) + ":\n" + ex);
        }
    }

    private void EnqueueInternal(
        string prompt,
        Action<string> onResponse,
        string debugTag,
        Dictionary<string, object> optionsOverride,
        bool highPriority,
        bool exclusive,
        string exclusiveOwner,
        bool disableTimeout)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            SafeInvoke(onResponse, null, debugTag);
            return;
        }

        if (!TryReserveQueueSlot(highPriority || exclusive, out string queueError))
        {
            RecordFailure(queueError, debugTag);
            SafeInvoke(onResponse, null, debugTag);
            return;
        }

        QueuedRequest request = new QueuedRequest
        {
            id = _nextRequestId++,
            prompt = prompt,
            onResponse = onResponse,
            debugTag = string.IsNullOrWhiteSpace(debugTag) ? "LLMRequest" : debugTag.Trim(),
            optionsOverride = optionsOverride,
            category = ResolveCategory(debugTag),
            requireJson = RequiresJsonOutput(debugTag),
            maxRetries = -1,
            attempt = 0,
            queuedAt = Time.unscaledTime,
            firstQueuedAt = Time.unscaledTime,
            exclusive = exclusive,
            highPriority = highPriority || exclusive,
            exclusiveOwner = exclusiveOwner ?? string.Empty,
            disableTimeout = disableTimeout
        };

        QueueRequest(request, request.highPriority);
    }

    private void QueueRequest(QueuedRequest request, bool important)
    {
        if (request.exclusive)
            _exclusiveQueue.Enqueue(request);
        else if (important)
            _highPriorityQueue.Enqueue(request);
        else
            _normalQueue.Enqueue(request);

        EnsureQueueProcessorRunning();
    }

    private void CompleteDirectFailure(Action<YQLlmRequestResult> callback, YQLlmRequest request, string error)
    {
        YQLlmRequestResult result = new YQLlmRequestResult(
            0,
            request != null ? request.debugTag : string.Empty,
            request != null ? request.category : LLMGenerationCategory.Default,
            false,
            null,
            error,
            0,
            0f,
            0f,
            default);
        PublishCompletion(result, callback);
    }

    private void CompleteRequest(
        QueuedRequest request,
        bool success,
        string text,
        string error,
        float queueWaitSeconds,
        float generationSeconds,
        LLMCompiledPrompt compiled)
    {
        YQLlmRequestResult result = new YQLlmRequestResult(
            request.id,
            request.debugTag,
            request.category,
            success,
            text,
            error,
            request.attempt + 1,
            queueWaitSeconds,
            generationSeconds,
            compiled);

        PublishCompletion(result, request.onCompleted);
        SafeInvoke(request.onResponse, success ? text : null, request.debugTag);
    }

    private void PublishCompletion(YQLlmRequestResult result, Action<YQLlmRequestResult> callback)
    {
        LastCompletedRequest = result;
        if (result.success)
            SuccessfulRequestCount++;
        else
            FailedRequestCount++;

        try
        {
            callback?.Invoke(result);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LLMClient] Typed completion callback threw:\n" + ex);
        }

        try
        {
            RequestCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[LLMClient] RequestCompleted subscriber threw:\n" + ex);
        }
    }

    private bool TryScheduleTransientRetry(QueuedRequest request, string error)
    {
        LLMRuntimeConfig config = ActiveConfig();
        int maxRetries = request.maxRetries >= 0
            ? Mathf.Clamp(request.maxRetries, 0, 3)
            : Mathf.Clamp(config != null ? config.transientRequestRetries : 0, 0, 3);

        if (_quitting || request.attempt >= maxRetries)
            return false;

        request.attempt++;
        Debug.LogWarning(
            "[LLMClient] Retrying request #" +
            request.id +
            FormatTag(request.debugTag) +
            " after transient failure (attempt " +
            request.attempt +
            "/" +
            maxRetries +
            "): " +
            TruncateForLog(error));
        StartCoroutine(RequeueAfterRetryDelay(request));
        return true;
    }

    private IEnumerator RequeueAfterRetryDelay(QueuedRequest request)
    {
        LLMRuntimeConfig config = ActiveConfig();
        float baseDelay = config != null ? config.retryBaseDelaySeconds : 0.75f;
        float maxDelay = config != null ? config.retryMaxDelaySeconds : 4f;
        float delay = Mathf.Min(
            Mathf.Max(0.1f, maxDelay),
            Mathf.Max(0.1f, baseDelay) * Mathf.Pow(2f, Mathf.Max(0, request.attempt - 1)));

        // note: Backoff prevents a faulted local runtime from being hammered by multiple immediate retries.
        yield return new WaitForSecondsRealtime(delay);

        if (_quitting)
            yield break;

        if (!TryReserveQueueSlot(request.highPriority || request.exclusive, out string queueError))
        {
            RecordFailure(queueError, request.debugTag);
            CompleteRequest(request, false, null, queueError, 0f, 0f, default);
            yield break;
        }

        request.queuedAt = Time.unscaledTime;
        QueueRequest(request, request.highPriority || request.exclusive);
    }

    private void EnsureQueueProcessorRunning()
    {
        if (_processing || _quitting)
            return;

        _processing = true;
        StartCoroutine(ProcessQueueCoroutine());
    }

    private IEnumerator ProcessQueueCoroutine()
    {
        while (!_quitting)
        {
            if (IsExclusiveSequenceActive && _exclusiveQueue.Count == 0)
            {
                // note: Startup generation can pause between stages; wait without letting lower-priority work interrupt it.
                yield return new WaitForSecondsRealtime(0.10f);
                continue;
            }

            QueuedRequest request;
            if (!TryDequeueNextRequest(out request))
                break;

            if (ShouldAbandonQueuedRequest(request))
                continue;

            yield return SendOnceCoroutine(request);

            if (ActiveConfig().staggerResponseHandoffAcrossFrames)
            {
                // note: Never let a completed callback, persistence work, and preparation of the following inference request collapse into one Unity frame.
                yield return null;
            }
        }

        _processing = false;

        if (!_quitting &&
            (IsExclusiveSequenceActive || _exclusiveQueue.Count > 0 || _highPriorityQueue.Count > 0 || _normalQueue.Count > 0))
        {
            EnsureQueueProcessorRunning();
        }
    }

    private bool TryDequeueNextRequest(out QueuedRequest request)
    {
        if (IsExclusiveSequenceActive)
        {
            if (_exclusiveQueue.Count == 0)
            {
                request = default;
                return false;
            }

            request = _exclusiveQueue.Dequeue();
            if (!string.Equals(request.exclusiveOwner, _exclusiveSequenceOwner, StringComparison.Ordinal))
            {
                Debug.LogWarning("[LLMClient] Preserving stale exclusive request '" + request.debugTag + "' for ordinary execution.");
                _normalQueue.Enqueue(request);
                return TryDequeueNextRequest(out request);
            }

            return true;
        }

        if (_highPriorityQueue.Count > 0)
        {
            request = _highPriorityQueue.Dequeue();
            return true;
        }

        if (_normalQueue.Count > 0)
        {
            request = _normalQueue.Dequeue();
            return true;
        }

        request = default;
        return false;
    }

    private IEnumerator SendOnceCoroutine(QueuedRequest request)
    {
        LLMRuntimeConfig config = ActiveConfig();
        if (config == null || !config.enableRuntimeLlm)
        {
            const string disabledError = "LLM runtime is disabled.";
            RecordFailure(disabledError, request.debugTag);
            CompleteRequest(request, false, null, disabledError, 0f, 0f, default);
            yield break;
        }

        if (_llamaServer != null && _llamaServer.HasOwnedProcessExited())
        {
            RuntimeState = YQLlmRuntimeState.Recovering;
            RecordFailure("Owned llama-server process exited unexpectedly.", request.debugTag);
        }

        LLMGenerationCategory category = request.category == LLMGenerationCategory.Default
            ? ResolveCategory(request.debugTag)
            : request.category;
        LLMGenerationProfile profile = config.GetProfile(category);
        Dictionary<string, object> options = BuildEffectiveOptions(config, profile, request.optionsOverride);
        int reservedOutputTokens = ReadIntOption(options, "num_predict", profile != null ? profile.maxOutputTokens : numPredict);
        int requestTimeout = request.disableTimeout
            ? 0
            : Mathf.Max(0, ReadIntOption(options, LocalRequestTimeoutSecondsOption, requestTimeoutSeconds));

        // note: Local transport controls must not leak into Ollama/llama.cpp sampling payloads.
        options.Remove(LocalRequestTimeoutSecondsOption);

        if (!LLMContextCompiler.TryCompile(
                request.prompt,
                config,
                profile,
                reservedOutputTokens,
                out LLMCompiledPrompt compiled,
                out string compileError))
        {
            RecordFailure(compileError, request.debugTag);
            CompleteRequest(request, false, null, compileError, 0f, 0f, default);
            yield break;
        }

        if (config.backend == YQLlmBackend.LlamaCpp)
        {
            bool ready = false;
            string readyMessage = string.Empty;
            RuntimeState = RuntimeState == YQLlmRuntimeState.Ready ? YQLlmRuntimeState.Ready : YQLlmRuntimeState.Starting;
            yield return EnsureLlamaCppReady(config, (ok, message) =>
            {
                ready = ok;
                readyMessage = message;
            });

            if (!ready)
            {
                RuntimeState = YQLlmRuntimeState.Faulted;
                RecordFailure(readyMessage, request.debugTag);
                if (!TryScheduleTransientRetry(request, readyMessage))
                    CompleteRequest(request, false, null, readyMessage, 0f, 0f, compiled);
                yield break;
            }
        }

        if (!TryBuildGenerateUrl(config, out string url, out string urlError))
        {
            RecordFailure(urlError, request.debugTag);
            CompleteRequest(request, false, null, urlError, 0f, 0f, compiled);
            yield break;
        }

        string json = BuildRequestJson(config, compiled.prompt, request.debugTag, options, profile, request.requireJson);
        float queueWait = Mathf.Max(0f, Time.unscaledTime - request.firstQueuedAt);
        float startedAt = Time.unscaledTime;

        if (logRequestSummaries)
        {
            Debug.Log(
                "[LLMClient] Request #" +
                request.id +
                FormatTag(request.debugTag) +
                ": backend=" +
                config.backend +
                ", category=" +
                category +
                ", attempt=" +
                (request.attempt + 1) +
                ", queueWait=" +
                queueWait.ToString("0.00") +
                "s, inputTokens~" +
                compiled.estimatedInputTokens +
                ", reservedOutputTokens=" +
                compiled.reservedOutputTokens +
                ", contextLimit=" +
                compiled.contextLimitTokens +
                ", reduced=" +
                compiled.reduced);
        }

        if (logRequestJson)
            Debug.Log("[LLMClient] Request JSON" + FormatTag(request.debugTag) + ":\n" + TruncateForLog(json));

        RuntimeState = YQLlmRuntimeState.Busy;
        using (UnityWebRequest www = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
        {
            byte[] body = Encoding.UTF8.GetBytes(json);
            www.uploadHandler = new UploadHandlerRaw(body);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.timeout = requestTimeout;
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Accept", "application/json");

            yield return www.SendWebRequest();

            float generationSeconds = Mathf.Max(0f, Time.unscaledTime - startedAt);
            if (www.result != UnityWebRequest.Result.Success)
            {
                string bodyText = www.downloadHandler != null ? www.downloadHandler.text : string.Empty;
                RuntimeState = YQLlmRuntimeState.Faulted;
                string transportError =
                    "LLM request failed at " +
                    url +
                    " (" +
                    www.result +
                    "): " +
                    www.error +
                    (string.IsNullOrWhiteSpace(bodyText) ? string.Empty : "\n" + TruncateForLog(bodyText));
                RecordFailure(transportError, request.debugTag);
                if (!TryScheduleTransientRetry(request, transportError))
                    CompleteRequest(request, false, null, transportError, queueWait, generationSeconds, compiled);
                yield break;
            }

            if (config.staggerResponseHandoffAcrossFrames)
            {
                // note: Do not extract, validate, and dispatch a completed response on the same frame that Unity finalizes the HTTP download buffer.
                yield return null;
            }

            string raw = www.downloadHandler.text;
            if (!TryExtractResponseText(raw, config.backend, out string modelText, out string responseError))
            {
                RuntimeState = YQLlmRuntimeState.Faulted;
                // note: Preserve a bounded copy of the successful HTTP envelope so backend schema or finish-reason failures are diagnosable without flooding the Unity Console.
                string extractionError =
                    "LLM returned an unusable response: " +
                    responseError +
                    "\nEnvelope: " +
                    TruncateForLog(raw);
                RecordFailure(extractionError, request.debugTag);
                if (!TryScheduleTransientRetry(request, extractionError))
                    CompleteRequest(request, false, null, extractionError, queueWait, generationSeconds, compiled);
                yield break;
            }

            if (config.staggerResponseHandoffAcrossFrames)
            {
                // note: Give rendering one frame between backend-envelope extraction and structured JSON validation/canonical domain callbacks.
                yield return null;
            }

            if (request.requireJson &&
                !request.deferJsonValidationToCaller &&
                !TryNormalizeJsonObject(modelText, out modelText, out string jsonError))
            {
                RuntimeState = YQLlmRuntimeState.Ready;
                string structuredError = "LLM returned invalid structured JSON: " + jsonError;
                RecordFailure(structuredError, request.debugTag);
                CompleteRequest(request, false, null, structuredError, queueWait, generationSeconds, compiled);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(modelText))
            {
                // note: A successful transport must never publish an empty generation as accepted canonical content.
                const string emptyCompletionError =
                    "LLM response normalization produced empty model text.";
                RuntimeState = YQLlmRuntimeState.Faulted;
                RecordFailure(emptyCompletionError, request.debugTag);
                if (!TryScheduleTransientRetry(request, emptyCompletionError))
                {
                    CompleteRequest(
                        request,
                        false,
                        null,
                        emptyCompletionError,
                        queueWait,
                        generationSeconds,
                        compiled);
                }
                yield break;
            }

            RuntimeState = YQLlmRuntimeState.Ready;
            ClearFailure();

            if (logRequestSummaries)
            {
                Debug.Log(
                    "[LLMClient] Response #" +
                    request.id +
                    FormatTag(request.debugTag) +
                    ": textChars=" +
                    (modelText != null ? modelText.Length : 0) +
                    ", generationSeconds=" +
                    generationSeconds.ToString("0.00"));
            }

            if (logRawModelText)
                Debug.Log("[LLMClient] Raw model text" + FormatTag(request.debugTag) + ":\n" + TruncateForLog(modelText ?? "<null>"));

            if (config.staggerResponseHandoffAcrossFrames)
            {
                // note: Domain parsing and world-state mutation begin on a clean frame instead of stacking behind transport cleanup and logging.
                yield return null;
            }

            CompleteRequest(request, true, modelText, string.Empty, queueWait, generationSeconds, compiled);
        }
    }

    private IEnumerator EnsureLlamaCppReady(LLMRuntimeConfig config, Action<bool, string> onComplete)
    {
        if (_llamaServer == null)
            _llamaServer = new LlamaCppServerProcess();

        yield return _llamaServer.EnsureReady(config, onComplete);
    }

    private LLMRuntimeConfig ActiveConfig()
    {
        if (runtimeConfig != null)
            _activeConfig = runtimeConfig;

        if (_activeConfig == null)
            _activeConfig = LLMRuntimeConfig.CreateRuntimeDefault();

        return _activeConfig;
    }

    private Dictionary<string, object> BuildEffectiveOptions(
        LLMRuntimeConfig config,
        LLMGenerationProfile profile,
        Dictionary<string, object> overrides)
    {
        LLMGenerationProfile activeProfile = profile ?? config.GetProfile(LLMGenerationCategory.Default);
        int profileMaxOutput = Mathf.Clamp(activeProfile.maxOutputTokens, 64, 6800);

        Dictionary<string, object> options = new Dictionary<string, object>(12)
        {
            { "num_predict", profileMaxOutput },
            { "temperature", activeProfile.temperature },
            { "top_p", activeProfile.topP },
            { "top_k", activeProfile.topK },
            { "presence_penalty", activeProfile.presencePenalty },
            { "repeat_penalty", activeProfile.repeatPenalty }
        };

        if (activeProfile.stopSequences != null && activeProfile.stopSequences.Length > 0)
            options["stop"] = activeProfile.stopSequences;

        if (overrides != null)
        {
            foreach (KeyValuePair<string, object> kvp in overrides)
            {
                if (string.IsNullOrWhiteSpace(kvp.Key))
                    continue;

                options[kvp.Key] = kvp.Value;
            }
        }

        int requestedOutput = ReadIntOption(options, "num_predict", profileMaxOutput);
        // note: Structured origin/world calls explicitly reserve bounded JSON budgets; profile values are defaults, not a hidden lower ceiling.
        int permittedOutput = overrides != null && overrides.ContainsKey("num_predict")
            ? 6800
            : profileMaxOutput;
        options["num_predict"] = Mathf.Clamp(requestedOutput, 64, permittedOutput);
        options["temperature"] = Mathf.Clamp(ReadFloatOption(options, "temperature", activeProfile.temperature), 0.05f, 1.5f);
        options["top_p"] = Mathf.Clamp(ReadFloatOption(options, "top_p", activeProfile.topP), 0.05f, 1f);
        options["top_k"] = Mathf.Clamp(ReadIntOption(options, "top_k", activeProfile.topK), 1, 100);
        options["presence_penalty"] = Mathf.Clamp(ReadFloatOption(options, "presence_penalty", activeProfile.presencePenalty), 0f, 2f);
        options["repeat_penalty"] = Mathf.Clamp(ReadFloatOption(options, "repeat_penalty", activeProfile.repeatPenalty), 0.8f, 2.5f);

        return options;
    }

    private string BuildRequestJson(
        LLMRuntimeConfig config,
        string prompt,
        string debugTag,
        Dictionary<string, object> options,
        LLMGenerationProfile profile,
        bool forceJson)
    {
        bool jsonOutput = forceJson || RequiresJsonOutput(debugTag) || (profile != null && profile.preferJson);

        if (config.backend == YQLlmBackend.LlamaCpp)
        {
            List<Dictionary<string, string>> messages = new List<Dictionary<string, string>>(1)
            {
                new Dictionary<string, string>
                {
                    { "role", "user" },
                    { "content", prompt }
                }
            };

            Dictionary<string, object> payload = new Dictionary<string, object>(16)
            {
                { "messages", messages },
                { "stream", false },
                { "max_tokens", ReadIntOption(options, "num_predict", numPredict) },
                { "temperature", ReadFloatOption(options, "temperature", 0.7f) },
                { "top_p", ReadFloatOption(options, "top_p", 0.8f) },
                { "top_k", ReadIntOption(options, "top_k", 20) },
                { "presence_penalty", ReadFloatOption(options, "presence_penalty", 1.5f) },
                { "repeat_penalty", ReadFloatOption(options, "repeat_penalty", 1.0f) },
                // note: cache_prompt lets llama.cpp reuse prompt prefixes when the server supports it.
                { "cache_prompt", true }
            };

            if (profile != null && profile.directMode && !profile.reasoningMode)
            {
                // note: Qwen chat templates can otherwise spend the entire structured-output budget in hidden reasoning and return an empty final content field.
                payload["chat_template_kwargs"] =
                    new Dictionary<string, object>
                    {
                        { "enable_thinking", false }
                    };
            }

            if (options.TryGetValue("stop", out object stop))
                payload["stop"] = stop;

            if (jsonOutput)
            {
                // note: The chat endpoint treats this as a JSON preference; Unity still validates before commit.
                payload["response_format"] = new Dictionary<string, string> { { "type", "json_object" } };
            }

            return JsonConvert.SerializeObject(payload);
        }

        Dictionary<string, object> ollamaOptions = new Dictionary<string, object>(options)
        {
            // note: Ollama uses num_ctx; llama.cpp receives context size at server startup.
            { "num_ctx", Mathf.Clamp(config.contextSizeTokens, 2048, 32768) }
        };

        if (config.preserveGameResponsiveness)
        {
            // note: Legacy Ollama receives the same cooperative CPU and prompt-batch limits as the owned llama.cpp runtime.
            ollamaOptions["num_batch"] =
                Mathf.Clamp(
                    config.promptBatchSize,
                    32,
                    2048);

            ollamaOptions["num_thread"] =
                Mathf.Clamp(
                    SystemInfo.processorCount -
                    Mathf.Max(1, config.reservedCpuThreads),
                    1,
                    4);
        }

        OllamaRequest payloadOllama = new OllamaRequest
        {
            model = string.IsNullOrWhiteSpace(config.ollamaModel) ? model : config.ollamaModel,
            prompt = prompt,
            stream = false,
            format = jsonOutput ? "json" : null,
            options = ollamaOptions
        };

        return JsonConvert.SerializeObject(payloadOllama);
    }

    private bool TryBuildGenerateUrl(LLMRuntimeConfig config, out string url, out string error)
    {
        url = string.Empty;
        error = string.Empty;

        if (config.backend == YQLlmBackend.LlamaCpp)
        {
            url = config.BuildBaseUrl().TrimEnd('/') + "/v1/chat/completions";
            return true;
        }

        string trimmed = !string.IsNullOrWhiteSpace(config.ollamaApiUrl)
            ? config.ollamaApiUrl.Trim().TrimEnd('/')
            : (apiUrl ?? string.Empty).Trim().TrimEnd('/');

        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = "Ollama URL is empty. Expected something like http://127.0.0.1:11434.";
            return false;
        }

        if (trimmed.EndsWith("/api/generate", StringComparison.OrdinalIgnoreCase))
            url = trimmed;
        else if (trimmed.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            url = trimmed + "/generate";
        else
            url = trimmed + "/api/generate";

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsed) ||
            (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Invalid Ollama URL '" + url + "'. Expected an http or https URL.";
            return false;
        }

        return true;
    }

    private bool TryReserveQueueSlot(bool important, out string error)
    {
        error = string.Empty;
        LLMRuntimeConfig config = ActiveConfig();
        int maxDepth = config != null ? Mathf.Max(16, config.maxQueueDepth) : 96;
        if (PendingRequestCount < maxDepth)
            return true;

        if (important && _normalQueue.Count > 0)
        {
            // note: A stale background item is cheaper to lose than player-facing dialogue or startup generation.
            _normalQueue.Dequeue();
            return true;
        }

        error = "LLM request queue is full (" + PendingRequestCount + "/" + maxDepth + ").";
        return false;
    }

    private bool ShouldAbandonQueuedRequest(QueuedRequest request)
    {
        if (request.exclusive)
            return false;

        float maxAge = Mathf.Max(5f, maxQueuedRequestAgeSeconds);
        float age = Time.unscaledTime - request.queuedAt;
        if (age <= maxAge)
            return false;

        RecordFailure(
            "Abandoned queued LLM request after " +
            age.ToString("0.0") +
            "s behind generation/busy work.",
            request.debugTag);

        CompleteRequest(request, false, null, LastError, age, 0f, default);
        return true;
    }

    private static bool TryExtractResponseText(
        string raw,
        YQLlmBackend backend,
        out string modelText,
        out string error)
    {
        modelText = null;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "empty response body";
            return false;
        }

        try
        {
            JObject jo = JObject.Parse(raw);
            JToken errorToken = jo["error"];
            if (errorToken != null && !string.IsNullOrWhiteSpace(errorToken.ToString()))
            {
                error = errorToken.ToString();
                return false;
            }

            JToken content = backend == YQLlmBackend.LlamaCpp
                ? jo["content"]
                : jo["response"];
            if (TryReadNonEmptyText(content, out modelText))
            {
                return true;
            }

            JToken choiceMessage = jo["choices"]?[0]?["message"]?["content"];
            if (TryReadNonEmptyText(choiceMessage, out modelText))
            {
                return true;
            }

            JToken choiceText = jo["choices"]?[0]?["text"];
            if (TryReadNonEmptyText(choiceText, out modelText))
            {
                return true;
            }

            // note: Some llama.cpp/Qwen combinations expose the completed payload in reasoning_content even when the OpenAI-compatible final content field is empty.
            JToken reasoningContent = jo["choices"]?[0]?["message"]?["reasoning_content"] ??
                jo["reasoning_content"];
            if (TryReadNonEmptyText(reasoningContent, out modelText))
                return true;

            JToken alternate = jo["completion"] ?? jo["generated_text"] ??
                jo["choices"]?[0]?["delta"]?["content"];
            if (TryReadNonEmptyText(alternate, out modelText))
                return true;

            string finishReason = jo["choices"]?[0]?["finish_reason"]?.ToString();
            error = "response contained no non-empty model text" +
                (string.IsNullOrWhiteSpace(finishReason)
                    ? string.Empty
                    : " (finish_reason=" + finishReason + ")");
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryReadNonEmptyText(
        JToken token,
        out string modelText)
    {
        modelText = null;

        if (token == null || token.Type == JTokenType.Null)
            return false;

        string candidate = StripReasoningBlocks(token.ToString());

        if (string.IsNullOrWhiteSpace(candidate))
            return false;

        modelText = candidate;
        return true;
    }

    private static LLMGenerationCategory ResolveCategory(string debugTag)
    {
        if (string.IsNullOrWhiteSpace(debugTag))
            return LLMGenerationCategory.Default;

        string tag = debugTag.Trim();
        if (tag.StartsWith("DialogueRepair", StringComparison.OrdinalIgnoreCase) ||
            tag.StartsWith("Dialogue", StringComparison.OrdinalIgnoreCase))
            return LLMGenerationCategory.Dialogue;
        if (tag.StartsWith("OriginGeneration", StringComparison.OrdinalIgnoreCase))
            return LLMGenerationCategory.OriginGeneration;
        if (tag.StartsWith("WorldPlanGeneration", StringComparison.OrdinalIgnoreCase))
            return LLMGenerationCategory.WorldGeneration;
        if (tag.StartsWith("GeneratedNpcPopulation", StringComparison.OrdinalIgnoreCase))
            return LLMGenerationCategory.NpcPopulation;
        if (tag.IndexOf("Goddess", StringComparison.OrdinalIgnoreCase) >= 0)
            return LLMGenerationCategory.GoddessCommentary;
        if (tag.IndexOf("Summary", StringComparison.OrdinalIgnoreCase) >= 0)
            return LLMGenerationCategory.Summarization;
        if (tag.IndexOf("Quest", StringComparison.OrdinalIgnoreCase) >= 0)
            return LLMGenerationCategory.QuestGeneration;

        return LLMGenerationCategory.Default;
    }

    private static string StripReasoningBlocks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string cleaned = text;
        while (true)
        {
            int start = cleaned.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                break;

            int end = cleaned.IndexOf("</think>", start, StringComparison.OrdinalIgnoreCase);
            if (end < 0)
            {
                cleaned = cleaned.Substring(0, start).Trim();
                break;
            }

            // note: The model may think internally, but gameplay systems should only receive the playable answer.
            cleaned = cleaned.Remove(start, end + "</think>".Length - start);
        }

        return cleaned.Trim();
    }

    private static bool TryNormalizeJsonObject(string raw, out string normalized, out string error)
    {
        normalized = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "response text was empty";
            return false;
        }

        string candidate = raw.Trim();
        if (candidate.StartsWith("```", StringComparison.Ordinal))
        {
            int firstLineEnd = candidate.IndexOf('\n');
            candidate = firstLineEnd >= 0 ? candidate.Substring(firstLineEnd + 1) : string.Empty;
            int closingFence = candidate.LastIndexOf("```", StringComparison.Ordinal);
            if (closingFence >= 0)
                candidate = candidate.Substring(0, closingFence);
            candidate = candidate.Trim();
        }

        int objectStart = candidate.IndexOf('{');
        int objectEnd = candidate.LastIndexOf('}');
        if (objectStart < 0 || objectEnd <= objectStart)
        {
            error = "response did not contain one JSON object";
            return false;
        }

        candidate = candidate.Substring(objectStart, objectEnd - objectStart + 1);
        try
        {
            JObject parsed = JObject.Parse(candidate);
            normalized = parsed.ToString(Formatting.None);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool RequiresJsonOutput(string debugTag)
    {
        if (string.IsNullOrWhiteSpace(debugTag))
            return false;

        return debugTag.StartsWith("OriginGeneration", StringComparison.OrdinalIgnoreCase) ||
               debugTag.StartsWith("WorldPlanGeneration", StringComparison.OrdinalIgnoreCase) ||
               debugTag.StartsWith("GeneratedNpcPopulation", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHighPriorityTag(string debugTag)
    {
        if (string.IsNullOrWhiteSpace(debugTag))
            return false;

        string tag = debugTag.Trim();
        return tag.StartsWith("Dialogue", StringComparison.OrdinalIgnoreCase) ||
               tag.StartsWith("NPC", StringComparison.OrdinalIgnoreCase) ||
               tag.IndexOf("DialogueRepair", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static int ReadIntOption(Dictionary<string, object> options, string key, int fallback)
    {
        if (options == null || !options.TryGetValue(key, out object value) || value == null)
            return fallback;

        if (value is int intValue)
            return intValue;
        if (value is long longValue)
            return (int)Mathf.Clamp(longValue, int.MinValue, int.MaxValue);
        if (value is float floatValue)
            return Mathf.RoundToInt(floatValue);
        if (value is double doubleValue)
            return Mathf.RoundToInt((float)doubleValue);
        if (int.TryParse(value.ToString(), out int parsed))
            return parsed;

        return fallback;
    }

    private static float ReadFloatOption(Dictionary<string, object> options, string key, float fallback)
    {
        if (options == null || !options.TryGetValue(key, out object value) || value == null)
            return fallback;

        if (value is float floatValue)
            return floatValue;
        if (value is double doubleValue)
            return (float)doubleValue;
        if (value is int intValue)
            return intValue;
        if (float.TryParse(value.ToString(), out float parsed))
            return parsed;

        return fallback;
    }

    private static string JsonGrammar()
    {
        return
            "root ::= object\n" +
            "object ::= \"{\" space members? \"}\" space\n" +
            "members ::= member (\",\" space member)*\n" +
            "member ::= string space \":\" space value\n" +
            "value ::= object | array | string | number | \"true\" | \"false\" | \"null\"\n" +
            "array ::= \"[\" space (value (\",\" space value)*)? \"]\" space\n" +
            "string ::= \"\\\"\" ([^\"\\\\] | \"\\\\\" ([\"\\\\/bfnrt] | \"u\" [0-9a-fA-F]{4}))* \"\\\"\" space\n" +
            "number ::= \"-\"? ([0-9] | [1-9] [0-9]*) (\".\" [0-9]+)? ([eE] [-+]? [0-9]+)? space\n" +
            "space ::= [ \\t\\n\\r]*";
    }

    private string TruncateForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        // note: Use the configured diagnostic ceiling so an unexpectedly large model payload cannot bloat player logs.
        LLMRuntimeConfig config = ActiveConfig();
        int configuredLimit = config != null
            ? Mathf.Clamp(config.maxStoredDiagnosticCharacters, 256, 12000)
            : 2400;
        int maxChars = Mathf.Min(
            Mathf.Max(256, maxLoggedPayloadCharacters),
            configuredLimit);
        if (value.Length <= maxChars)
            return value;

        return value.Substring(0, maxChars) +
               "\n... <truncated " +
               (value.Length - maxChars) +
               " chars>";
    }

    private static string FormatTag(string debugTag)
    {
        return string.IsNullOrWhiteSpace(debugTag) ? string.Empty : " (" + debugTag + ")";
    }

    private void RecordFailure(string message, string debugTag)
    {
        LastRequestFailed = true;
        // note: Persist only bounded diagnostics because response bodies may be arbitrarily large.
        LastError = string.IsNullOrWhiteSpace(message)
            ? "LLM request failed."
            : TruncateForLog(message.Trim());
        Debug.LogError("[LLMClient] " + FormatTag(debugTag) + " " + LastError);
    }

    private void ClearFailure()
    {
        LastRequestFailed = false;
        LastError = string.Empty;
    }

    private void DisposeOwnedRuntime()
    {
        if (_llamaServer == null)
            return;

        LLMRuntimeConfig config = ActiveConfig();
        if (config != null && config.closeOwnedServerOnQuit)
            _llamaServer.StopOwnedProcess();

        _llamaServer.Dispose();
        _llamaServer = null;
    }

    private void SafeInvoke(Action<string> cb, string value)
    {
        SafeInvoke(cb, value, null);
    }
}
