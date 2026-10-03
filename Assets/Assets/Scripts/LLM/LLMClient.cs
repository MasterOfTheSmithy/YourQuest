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
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public string keep_alive;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public object format;
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)] public bool? think;
    public Dictionary<string, object> options;
}

[DisallowMultipleComponent]
public sealed class LLMClient : MonoBehaviour
{
    public static LLMClient Instance { get; private set; }
    private float nextRepairDeadlineSweep;
    private readonly List<long> expiredRepairRequestIds = new List<long>();
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
        _normalQueue.Count > 0 ||
        _retryingRequests.Count > 0;

    public bool HasPendingHighPriorityRequests => _highPriorityQueue.Count > 0;

    public int PendingRequestCount =>
        _exclusiveQueue.Count +
        _highPriorityQueue.Count +
        _normalQueue.Count +
        _retryingRequests.Count;

    public long ActiveRequestId => _activeRequestValid ? _activeRequest.id : 0;
    public float ActiveRequestAgeSeconds => _activeRequestValid
        ? Mathf.Max(0f, Time.unscaledTime - _activeRequestStartedAt)
        : 0f;

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
        // note: Preserve the caller's schema with the queued request so retries constrain the identical canonical contract.
        public Dictionary<string, object> jsonSchema;
        // note: Preserve JSON response formatting while allowing the owning domain validator to strip malformed optional prose.
        public bool deferJsonValidationToCaller;
        public int maxRetries;
        public int attempt;
        public YQRepairEpisode repairEpisode;
        public bool repairVerification;
        public bool protectPrompt;
        public string parentRequestKey;
        public string requiredOllamaModelDigest;
        public string repairRequestKey;
        public Func<bool> ownerStillCurrent;

        // note: Queue age lets background requests expire instead of piling onto the model after generation.
        public float queuedAt;
        public float firstQueuedAt;

        public bool exclusive;
        public bool highPriority;
        public string exclusiveOwner;
        public bool disableTimeout;
        // note: Admission stamps keep a response tied to the live profile, world, lifecycle epoch, and relevant revisions.
        public string profileId;
        public string worldId;
        public int generationEpoch;
        public string ownerId;
        public long playerStateRevision;
        public long worldStateRevision;
    }

    private readonly Queue<QueuedRequest> _exclusiveQueue = new Queue<QueuedRequest>();
    private readonly Queue<QueuedRequest> _highPriorityQueue = new Queue<QueuedRequest>();
    private readonly Queue<QueuedRequest> _normalQueue = new Queue<QueuedRequest>();

    private LLMRuntimeConfig _activeConfig;
    private bool _usingRuntimeDefaultConfig;
    private bool _runtimeBackendResolved;
    private LlamaCppServerProcess _llamaServer;
    private UnityWebRequest _activeWebRequest;
    private string _exclusiveSequenceOwner = string.Empty;
    private bool _processing;
    private bool _quitting;
    private float _lastLlmActivityTime;
    private long _nextRequestId = 1;
    private int _consecutiveHighPriorityRequests;
    private readonly HashSet<long> _terminalRequestIds = new HashSet<long>();
    private readonly Dictionary<long, QueuedRequest> _retryingRequests = new Dictionary<long, QueuedRequest>();
    private QueuedRequest _activeRequest;
    private bool _activeRequestValid;
    private float _activeRequestStartedAt;
    private float _exclusiveSequenceStartedAt;

    public int QueueEvictionCount { get; private set; }
    public int CancellationCount { get; private set; }
    public int SupersededRequestCount { get; private set; }
    public int MalformedResponseCount { get; private set; }
    public int TimeoutFailureCount { get; private set; }
    public int MaxObservedQueueDepth { get; private set; }
    public float LastQueuedLatencySeconds { get; private set; }
    public float LastActiveLatencySeconds { get; private set; }
    public float TotalQueuedLatencySeconds { get; private set; }
    public float TotalActiveLatencySeconds { get; private set; }

    // note: Category counters expose scheduler health without logging private prompts or full model transcripts.
    private readonly Dictionary<LLMGenerationCategory, int> _categoryTerminalCounts = new Dictionary<LLMGenerationCategory, int>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        _usingRuntimeDefaultConfig = runtimeConfig == null;
        _activeConfig = runtimeConfig != null
            ? runtimeConfig
            : LLMRuntimeConfig.CreateRuntimeDefault();

        _llamaServer = new LlamaCppServerProcess();
        _lastLlmActivityTime = Time.realtimeSinceStartup;
        RuntimeState = _activeConfig.enableRuntimeLlm ? YQLlmRuntimeState.Starting : YQLlmRuntimeState.Disabled;
        YQServiceLifecycle.RegisterTeardown(InvalidateForProfileLifecycle);
        // note: Unity invokes Application.quitting for editor Play Mode stops as well as player shutdown, so the owned llama process is closed even when OnApplicationQuit ordering is skipped.
        Application.quitting += HandleApplicationQuitting;
    }

    private void Update()
    {
        if (_quitting)
            return;

        // note: Episode time includes queue and health setup; retire expired children through the existing scheduler.
        if (Time.unscaledTime >= nextRepairDeadlineSweep)
        {
            nextRepairDeadlineSweep = Time.unscaledTime + 0.25f;
            ExpireRepairRequests();
        }
        LLMRuntimeConfig config = ActiveConfig();
        if (IsExclusiveSequenceActive &&
            Time.realtimeSinceStartup - _exclusiveSequenceStartedAt >
            Mathf.Max(15, config != null ? config.exclusiveSequenceTimeoutSeconds : 120))
        {
            // note: A vanished startup owner cannot keep lower-priority work locked indefinitely; terminalize its work before releasing the lease.
            CancelRequestsOwnedBy(_exclusiveSequenceOwner, YQLlmTerminalOutcome.Superseded, "Exclusive LLM sequence exceeded its bounded lease.");
            string expiredOwner = _exclusiveSequenceOwner;
            _exclusiveSequenceOwner = string.Empty;
            Debug.LogWarning("[LLMClient] Exclusive sequence lease expired: " + expiredOwner);
            EnsureQueueProcessorRunning();
        }

        if (_llamaServer == null || !_llamaServer.OwnsProcess)
            return;

        if (config == null || !config.closeOwnedServerWhenIdle ||
            _processing || _activeWebRequest != null ||
            _exclusiveQueue.Count > 0 || _highPriorityQueue.Count > 0 || _normalQueue.Count > 0)
            return;

        float idleSeconds = Time.realtimeSinceStartup - _lastLlmActivityTime;
        if (idleSeconds < Mathf.Max(5, config.ownedServerIdleTimeoutSeconds))
            return;

        // note: Stop only the process launched by this client so its model leaves VRAM; a user-managed server remains available.
        _llamaServer.StopOwnedProcess();
        RuntimeState = YQLlmRuntimeState.Disabled;
        Debug.Log("[LLMClient] Closed owned llama-server after " + idleSeconds.ToString("0") + "s idle.");
    }

    private void OnApplicationQuit()
    {
        BeginShutdown();
        DisposeOwnedRuntime();
    }

    private void HandleApplicationQuitting()
    {
        // note: Route the engine-wide quit signal through the same idempotent teardown used by the component lifecycle.
        BeginShutdown();
        DisposeOwnedRuntime();
    }

    private void OnDestroy()
    {
        BeginShutdown();
        // note: Remove the quit callback before releasing the persistent client so a later scene teardown cannot invoke a stale delegate.
        Application.quitting -= HandleApplicationQuitting;
        YQServiceLifecycle.UnregisterTeardown(InvalidateForProfileLifecycle);

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
        _exclusiveSequenceStartedAt = Time.realtimeSinceStartup;
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
        // note: Releasing an owner with queued work must terminalize that work; otherwise the exclusive queue would be stranded once ordinary scheduling resumes.
        CancelRequestsOwnedBy(normalizedOwner, YQLlmTerminalOutcome.Superseded, "Exclusive sequence ended before all owned requests were dispatched.");
        _exclusiveSequenceOwner = string.Empty;
        _exclusiveSequenceStartedAt = 0f;
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

    public bool BoundedRepairEnabled => ActiveConfig().enableBoundedRepair;
    public bool DialogueSemanticRepairEnabled => BoundedRepairEnabled && ActiveConfig().dialogueVerifierQualified;
    // note: Goddess qualification is independent of NPC dialogue and bound to the reviewed prompt/schema contract.
    public bool GoddessSemanticRepairEnabled => ActiveConfig().HasQualifiedGoddessVerifier;
    public string QualifiedGoddessVerifierDigest => GoddessSemanticRepairEnabled ? ActiveConfig().goddessVerifierModelDigest : string.Empty;
    public bool GoddessSpeechPlanEnabled => ActiveConfig().HasQualifiedGoddessSpeechPlan;
    public string QualifiedGoddessSpeechPlanDigest => GoddessSpeechPlanEnabled ? ActiveConfig().goddessSpeechPlanModelDigest : string.Empty;
    public bool GoddessSpeechPlanCpuOnly => ActiveConfig().goddessSpeechPlanCpuOnly;
    private void ExpireRepairRequests()
    {
        expiredRepairRequestIds.Clear();
        foreach (QueuedRequest request in _exclusiveQueue) CaptureExpiredRepair(request);
        foreach (QueuedRequest request in _highPriorityQueue) CaptureExpiredRepair(request);
        foreach (QueuedRequest request in _normalQueue) CaptureExpiredRepair(request);
        foreach (QueuedRequest request in _retryingRequests.Values) CaptureExpiredRepair(request);
        if (_activeRequestValid) CaptureExpiredRepair(_activeRequest);
        // note: Callbacks may change queues, so collect IDs before terminalizing any child.
        for (int i = 0; i < expiredRepairRequestIds.Count; i++)
            TerminalizeRequestById(expiredRepairRequestIds[i], YQLlmTerminalOutcome.Failed,
                "Repair episode deadline exhausted.", false);
    }

    private void CaptureExpiredRepair(QueuedRequest request)
    {
        if (request.repairEpisode != null && request.repairEpisode.RemainingSeconds < 1d)
            expiredRepairRequestIds.Add(request.id);
    }

    public void CancelRepairEpisode(YQRepairEpisode episode)
    {
        // note: Cancel exact admitted children, preserving unrelated work that happens to share an owner label.
        if (episode == null) return;
        foreach (long requestId in episode.RequestIds) CancelRequest(requestId, "Repair owner retired.");
    }
    public string QualifiedDialogueVerifierDigest => ActiveConfig().dialogueVerifierQualified
        ? ActiveConfig().dialogueVerifierModelDigest : string.Empty;

    public YQRepairEpisode CreateRepairEpisode(YQLlmRequest request, string taskFingerprint, double seconds)
    {
        if (!BoundedRepairEnabled) return null;
        CaptureRequestBinding(request, request.debugTag, out string profile, out string world, out int epoch,
            out string owner, out long playerRev, out long worldRev);
        if (request.priority == YQLlmRequestPriority.StartupExclusive)
        {
            owner = request.exclusiveOwner;
            if (IsExclusiveSequenceActive)
                seconds = Math.Min(seconds, Math.Max(0d, ActiveConfig().exclusiveSequenceTimeoutSeconds -
                    (Time.realtimeSinceStartup - _exclusiveSequenceStartedAt) - 5d));
        }
        return new YQRepairEpisode(profile, world, epoch, owner, playerRev, worldRev, taskFingerprint, seconds);
    }

    public long Submit(YQLlmRequest request, Action<YQLlmRequestResult> onComplete)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.prompt))
        {
            CompleteDirectFailure(onComplete, request, "LLM request prompt was empty.");
            return 0;
        }
        // note: Admission failures cannot restart the same repair episode indefinitely.
        if (request.repairEpisode != null && !request.repairEpisode.TryAdmit(request.repairVerification))
        {
            CompleteDirectFailure(onComplete, request, "Repair episode budget or deadline exhausted.");
            return 0;
        }

        string tag = string.IsNullOrWhiteSpace(request.debugTag) ? "LLMRequest" : request.debugTag.Trim();
        bool exclusive = request.priority == YQLlmRequestPriority.StartupExclusive;
        bool important = exclusive || request.priority == YQLlmRequestPriority.PlayerFacing;
        string owner = string.IsNullOrWhiteSpace(request.exclusiveOwner) ? string.Empty : request.exclusiveOwner.Trim();

        if (exclusive && string.IsNullOrWhiteSpace(owner))
        {
            CompleteDirectFailure(onComplete, request, "Exclusive LLM requests require an explicit sequence owner.");
            return 0;
        }

        if (exclusive && !IsExclusiveSequenceActive)
            BeginExclusiveSequence(owner);

        if (exclusive && !string.Equals(_exclusiveSequenceOwner, owner, StringComparison.Ordinal))
        {
            CompleteDirectFailure(onComplete, request, "Another exclusive LLM sequence currently owns the runtime.");
            return 0;
        }

        if (!TryReserveQueueSlot(important, out string queueError))
        {
            RecordFailure(queueError, tag);
            CompleteDirectFailure(onComplete, request, queueError);
            return 0;
        }

        float now = Time.unscaledTime;
        CaptureRequestBinding(request, tag, out string profileId, out string worldId, out int generationEpoch, out string ownerId, out long playerRevision, out long worldRevision);
        if (exclusive)
            ownerId = owner;
        long requestId = _nextRequestId++;
        request.repairEpisode?.RegisterRequest(requestId);
        QueuedRequest queued = new QueuedRequest
        {
            id = requestId,
            prompt = request.prompt,
            onCompleted = onComplete,
            debugTag = tag,
            optionsOverride = request.optionsOverride,
            category = request.category,
            requireJson = request.requireJson,
            jsonSchema = request.jsonSchema,
            deferJsonValidationToCaller = request.deferJsonValidationToCaller,
            maxRetries = request.maxRetries,
            repairEpisode = request.repairEpisode,
            repairVerification = request.repairVerification,
            protectPrompt = request.protectPrompt,
            parentRequestKey = request.parentRequestKey,
            requiredOllamaModelDigest = request.requiredOllamaModelDigest,
            ownerStillCurrent = request.ownerStillCurrent,
            attempt = 0,
            queuedAt = now,
            firstQueuedAt = now,
            exclusive = exclusive,
            highPriority = important,
            exclusiveOwner = owner,
            disableTimeout = request.disableTimeout,
            profileId = profileId,
            worldId = worldId,
            generationEpoch = generationEpoch,
            ownerId = ownerId,
            playerStateRevision = playerRevision,
            worldStateRevision = worldRevision
        };

        QueueRequest(queued, important);
        return requestId;
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
        // note: Startup-exclusive work is important, not infinite; callers must opt in explicitly if a genuinely unbounded local request is ever required.
        bool disableTimeout = false)
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
        YQLlmRequest legacyBinding = new YQLlmRequest
        {
            profileId = string.Empty,
            worldId = string.Empty,
            generationEpoch = -1,
            ownerId = request.debugTag,
            playerStateRevision = -1,
            worldStateRevision = -1
        };
        CaptureRequestBinding(legacyBinding, request.debugTag, out request.profileId, out request.worldId, out request.generationEpoch, out request.ownerId, out request.playerStateRevision, out request.worldStateRevision);
        if (exclusive)
            request.ownerId = exclusiveOwner ?? string.Empty;

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

        MaxObservedQueueDepth = Mathf.Max(MaxObservedQueueDepth, PendingRequestCount);

        EnsureQueueProcessorRunning();
    }

    private void CaptureRequestBinding(
        YQLlmRequest request,
        string debugTag,
        out string profileId,
        out string worldId,
        out int generationEpoch,
        out string ownerId,
        out long playerRevision,
        out long worldRevision)
    {
        PlayerState player = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        WorldState world = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        profileId = !string.IsNullOrWhiteSpace(request.profileId)
            ? request.profileId.Trim()
            : (YQProfileSaveSystem.Instance != null ? YQProfileSaveSystem.Instance.ActiveProfileId : string.Empty);
        if (string.IsNullOrWhiteSpace(profileId) && player != null)
            profileId = player.playerId ?? string.Empty;

        worldId = !string.IsNullOrWhiteSpace(request.worldId)
            ? request.worldId.Trim()
            : (world != null && world.worldIdentity != null ? world.worldIdentity.worldId : string.Empty);
        generationEpoch = request.generationEpoch >= 0 ? request.generationEpoch : YQServiceLifecycle.RequestEpoch;
        ownerId = string.IsNullOrWhiteSpace(request.ownerId) ? debugTag : request.ownerId.Trim();
        playerRevision = !request.bindPlayerStateRevision
            ? -1
            : request.playerStateRevision >= 0 ? request.playerStateRevision : player != null ? player.stateRevision : -1;
        worldRevision = !request.bindWorldStateRevision
            ? -1
            : request.worldStateRevision >= 0 ? request.worldStateRevision : world != null ? world.stateRevision : -1;
    }

    private void CompleteDirectFailure(Action<YQLlmRequestResult> callback, YQLlmRequest request, string error)
    {
        YQLlmRequestResult result = new YQLlmRequestResult(
            0,
            request != null ? request.debugTag : string.Empty,
            request != null ? request.category : LLMGenerationCategory.Default,
            false,
            YQLlmTerminalOutcome.Failed,
            null,
            error,
            0,
            0f,
            0f,
            default,
            request?.profileId, request?.worldId, request != null ? request.generationEpoch : -1,
            request?.ownerId, request != null ? request.playerStateRevision : -1,
            request != null ? request.worldStateRevision : -1, request?.repairEpisode?.key, null);
        PublishCompletion(result, callback);
    }

    private void CompleteRequest(
        QueuedRequest request,
        bool success,
        string text,
        string error,
        float queueWaitSeconds,
        float generationSeconds,
        LLMCompiledPrompt compiled,
        YQLlmTerminalOutcome requestedOutcome = YQLlmTerminalOutcome.Failed)
    {
        if (!_terminalRequestIds.Add(request.id))
            return;

        YQLlmTerminalOutcome outcome = success ? YQLlmTerminalOutcome.AcceptedResponse : requestedOutcome;
        YQLlmRequestResult result = new YQLlmRequestResult(
            request.id,
            request.debugTag,
            request.category,
            success,
            outcome,
            text,
            error,
            request.attempt + 1,
            queueWaitSeconds,
            generationSeconds,
            compiled,
            request.profileId,
            request.worldId,
            request.generationEpoch,
            request.ownerId,
            request.playerStateRevision,
            request.worldStateRevision,
            request.repairEpisode?.key,
            request.repairRequestKey);

        LastQueuedLatencySeconds = queueWaitSeconds;
        LastActiveLatencySeconds = generationSeconds;
        TotalQueuedLatencySeconds += Mathf.Max(0f, queueWaitSeconds);
        TotalActiveLatencySeconds += Mathf.Max(0f, generationSeconds);
        if (request.category != LLMGenerationCategory.Default)
        {
            _categoryTerminalCounts.TryGetValue(request.category, out int count);
            _categoryTerminalCounts[request.category] = count + 1;
        }
        if (outcome == YQLlmTerminalOutcome.Superseded)
            SupersededRequestCount++;

        if (logRequestSummaries)
        {
            // note: Terminal telemetry records queue depth, category, outcome, and separate latency clocks without emitting private prompt history.
            Debug.Log(
                "[LLMClient] Terminal #" + request.id +
                FormatTag(request.debugTag) +
                ": outcome=" + outcome +
                ", category=" + request.category +
                ", queueDepth=" + PendingRequestCount +
                ", queuedLatency=" + queueWaitSeconds.ToString("0.00") +
                "s, activeLatency=" + generationSeconds.ToString("0.00") + "s");
        }

        PublishCompletion(result, request.onCompleted);
        SafeInvoke(request.onResponse, success ? text : null, request.debugTag);
    }

    public int GetTerminalCount(LLMGenerationCategory category)
    {
        return _categoryTerminalCounts.TryGetValue(category, out int count) ? count : 0;
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
        if (request.repairEpisode != null && !request.repairEpisode.CanDispatch(request.repairVerification))
            return false;
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
        _retryingRequests[request.id] = request;
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

        _retryingRequests.Remove(request.id);
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

            _activeRequest = request;
            _activeRequestValid = true;
            _activeRequestStartedAt = Time.unscaledTime;
            yield return SendOnceCoroutine(request);
            _activeRequestValid = false;
            _activeRequestStartedAt = 0f;

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
                // note: An exclusive request whose owner lease disappeared is stale work, not ordinary work; terminalize it so it cannot cross a generation boundary.
                CompleteRequest(request, false, null, "Exclusive owner no longer matches the active lease.", 0f, 0f, default, YQLlmTerminalOutcome.Superseded);
                return TryDequeueNextRequest(out request);
            }

            return true;
        }

        LLMRuntimeConfig config = ActiveConfig();
        int highBurstLimit = Mathf.Clamp(config != null ? config.maxConsecutiveHighPriorityRequests : 3, 1, 8);
        bool serveNormalForFairness = _highPriorityQueue.Count > 0 &&
            _normalQueue.Count > 0 &&
            _consecutiveHighPriorityRequests >= highBurstLimit;

        if (_highPriorityQueue.Count > 0 && !serveNormalForFairness)
        {
            request = _highPriorityQueue.Dequeue();
            _consecutiveHighPriorityRequests++;
            return true;
        }

        if (_normalQueue.Count > 0)
        {
            request = _normalQueue.Dequeue();
            _consecutiveHighPriorityRequests = 0;
            return true;
        }

        request = default;
        return false;
    }

    private IEnumerator SendOnceCoroutine(QueuedRequest request)
    {
        _lastLlmActivityTime = Time.realtimeSinceStartup;
        if (!IsRequestCurrent(request))
        {
            CompleteRequest(request, false, null, "Request ownership is no longer current.", 0f, 0f, default, YQLlmTerminalOutcome.Superseded);
            yield break;
        }

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

        // note: The protected path rejects overflow instead of removing required repair facts from the payload.
        LLMCompiledPrompt compiled;
        string compileError;
        int contextLimitTokens = config.backend == YQLlmBackend.Ollama
            ? ReadIntOption(options, "num_ctx", config.contextSizeTokens) : config.contextSizeTokens;
        bool compiledOk = request.protectPrompt
            ? LLMContextCompiler.TryCompileProtected(request.prompt, config, profile, reservedOutputTokens, out compiled, out compileError, contextLimitTokens)
            : LLMContextCompiler.TryCompile(request.prompt, config, profile, reservedOutputTokens, out compiled, out compileError, contextLimitTokens);
        if (!compiledOk)
        {
            RecordFailure(compileError, request.debugTag);
            CompleteRequest(request, false, null, compileError, 0f, 0f, default);
            yield break;
        }

        if (_usingRuntimeDefaultConfig && !_runtimeBackendResolved)
        {
            bool backendReady = false;
            string backendMessage = string.Empty;
            yield return EnsureRuntimeDefaultBackend(config, (ok, message) =>
            {
                backendReady = ok;
                backendMessage = message;
            });

            if (!backendReady)
            {
                RuntimeState = YQLlmRuntimeState.Faulted;
                RecordFailure(backendMessage, request.debugTag);
                if (!TryScheduleTransientRetry(request, backendMessage))
                    CompleteRequest(request, false, null, backendMessage, 0f, 0f, compiled);
                yield break;
            }
        }

        if (config.backend == YQLlmBackend.LlamaCpp &&
            (!_usingRuntimeDefaultConfig || _runtimeBackendResolved))
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

        string json = BuildRequestJson(
            config,
            compiled.prompt,
            request.debugTag,
            options,
            profile,
            request.requireJson,
            request.jsonSchema);
        if (!string.IsNullOrWhiteSpace(request.requiredOllamaModelDigest))
        {
            // note: Qualification belongs to a specific model; a retagged model must not inherit its verifier receipt.
            bool matches = false;
            if (config.backend == YQLlmBackend.Ollama)
            {
                string tagsUrl = config.ollamaApiUrl.TrimEnd('/') + "/api/tags";
                string selectedModel = JObject.Parse(json).Value<string>("model");
                yield return ProbeLocalHealth(tagsUrl, 2, (ok, body) =>
                    matches = ok && MatchesOllamaModelDigest(body, selectedModel, request.requiredOllamaModelDigest), true);
            }
            if (!matches)
            {
                CompleteRequest(request, false, null, "Qualified dialogue verifier model is unavailable or its digest changed.", 0f, 0f, compiled);
                yield break;
            }
        }
        if (!IsRequestCurrent(request))
        {
            CompleteRequest(request, false, null, "Repair owner retired before inference dispatch.", 0f, 0f, compiled, YQLlmTerminalOutcome.Superseded);
            yield break;
        }
        if (request.repairEpisode != null)
        {
            if (!request.repairEpisode.TryBeginCall(request.repairVerification, json, request.parentRequestKey, out request.repairRequestKey))
            {
                CompleteRequest(request, false, null, "Repair episode budget or deadline exhausted.", 0f, 0f, compiled);
                yield break;
            }
            if (_activeRequestValid && _activeRequest.id == request.id) _activeRequest = request;
            requestTimeout = Math.Max(1, Math.Min(requestTimeout > 0 ? requestTimeout : int.MaxValue,
                (int)Math.Floor(request.repairEpisode.RemainingSeconds)));
        }
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

            // note: Retain only the in-flight transport so Play Mode shutdown can abort local inference immediately instead of waiting for the HTTP timeout or model completion.
            _activeWebRequest = www;
            yield return www.SendWebRequest();
            if (ReferenceEquals(_activeWebRequest, www))
                _activeWebRequest = null;

            if (_quitting || _terminalRequestIds.Contains(request.id))
                yield break;
            if (request.repairEpisode != null && request.repairEpisode.RemainingSeconds < 1d)
            {
                CompleteRequest(request, false, null, "Repair episode deadline exhausted.", queueWait,
                    Mathf.Max(0f, Time.unscaledTime - startedAt), compiled);
                yield break;
            }

            if (!IsRequestCurrent(request))
            {
                CompleteRequest(request, false, null, "Request completed after its profile/world ownership became stale.", queueWait, Mathf.Max(0f, Time.unscaledTime - startedAt), compiled, YQLlmTerminalOutcome.Superseded);
                yield break;
            }

            float generationSeconds = Mathf.Max(0f, Time.unscaledTime - startedAt);
            if (www.result != UnityWebRequest.Result.Success)
            {
                string bodyText = www.downloadHandler != null ? www.downloadHandler.text : string.Empty;
                RuntimeState = YQLlmRuntimeState.Faulted;
                if (!string.IsNullOrWhiteSpace(www.error) &&
                    www.error.IndexOf("timeout", StringComparison.OrdinalIgnoreCase) >= 0)
                    TimeoutFailureCount++;
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
                MalformedResponseCount++;
                // note: Preserve a bounded copy of the successful HTTP envelope so backend schema or finish-reason failures are diagnosable without flooding the Unity Console.
                string extractionError =
                    "LLM returned an unusable response: " +
                    responseError +
                    "\nEnvelope: " +
                    TruncateForLog(raw);
                RecordFailure(extractionError, request.debugTag);
                if (!TryScheduleTransientRetry(request, extractionError))
                    CompleteRequest(request, false, null, extractionError, queueWait, generationSeconds, compiled, YQLlmTerminalOutcome.InvalidResponse);
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
                MalformedResponseCount++;
                RuntimeState = YQLlmRuntimeState.Ready;
                string structuredError = "LLM returned invalid structured JSON: " + jsonError;
                RecordFailure(structuredError, request.debugTag);
                CompleteRequest(request, false, null, structuredError, queueWait, generationSeconds, compiled);
                yield break;
            }

            LLMRuntimeConfig responseConfig = ActiveConfig();
            int responseLimit = Mathf.Clamp(responseConfig != null ? responseConfig.maxResponseCharacters : 60000, 1024, 100000);
            if (modelText != null && modelText.Length > responseLimit)
            {
                MalformedResponseCount++;
                string oversizedError = "LLM response exceeded the " + responseLimit + " character response envelope.";
                RecordFailure(oversizedError, request.debugTag);
                CompleteRequest(request, false, null, oversizedError, queueWait, generationSeconds, compiled, YQLlmTerminalOutcome.InvalidResponse);
                yield break;
            }

            if (string.IsNullOrWhiteSpace(modelText))
            {
                // note: A successful transport must never publish an empty generation as accepted canonical content.
                const string emptyCompletionError =
                    "LLM response normalization produced empty model text.";
                MalformedResponseCount++;
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
                        compiled,
                        YQLlmTerminalOutcome.InvalidResponse);
                }
                yield break;
            }

            RuntimeState = YQLlmRuntimeState.Ready;
            ClearFailure();
            _lastLlmActivityTime = Time.realtimeSinceStartup;

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

            if (!IsRequestCurrent(request))
            {
                CompleteRequest(request, false, null, "Request became stale before application.", queueWait, generationSeconds, compiled, YQLlmTerminalOutcome.Superseded);
                yield break;
            }

            CompleteRequest(request, true, modelText, string.Empty, queueWait, generationSeconds, compiled);
        }
    }

    private IEnumerator EnsureLlamaCppReady(LLMRuntimeConfig config, Action<bool, string> onComplete)
    {
        if (_llamaServer == null)
            _llamaServer = new LlamaCppServerProcess();

        // note: Automatic on-demand model loading must not evict rendering resources during released gameplay; explicit configurations and startup generation retain their selected offload policy.
        bool protectLivePresentation = _usingRuntimeDefaultConfig && !IsExclusiveSequenceActive &&
            YourQuestTutorialAutoBootstrap.GameplayPresentationReleased;
        yield return _llamaServer.EnsureReady(config, onComplete, protectLivePresentation);
    }

    private IEnumerator EnsureRuntimeDefaultBackend(LLMRuntimeConfig config, Action<bool, string> onComplete)
    {
        // note: Screened Ollama role selections must reach Ollama rather than silently running every role on the llama.cpp model.
        if (config.backend == YQLlmBackend.Ollama)
        {
            string baseUrl = string.IsNullOrWhiteSpace(config.ollamaApiUrl) ? apiUrl : config.ollamaApiUrl;
            string healthUrl = (baseUrl ?? string.Empty).Trim().TrimEnd('/') + "/api/tags";
            bool ready = false;
            yield return ProbeLocalHealth(healthUrl, 2, (ok, _) => ready = ok);
            _runtimeBackendResolved = ready;
            onComplete?.Invoke(ready, ready
                ? "Connected to Ollama with category-specific local models."
                : "The configured Ollama backend is not reachable: " + healthUrl);
            yield break;
        }

        // note: Prefer the project's owned llama.cpp runtime so the no-config path remains deterministic even when Ollama is installed but unhealthy.
        bool llamaReady = false;
        yield return ProbeLocalHealth(config.BuildBaseUrl() + "/health", 2, (ok, _) => llamaReady = ok);
        if (llamaReady)
        {
            config.backend = YQLlmBackend.LlamaCpp;
            _runtimeBackendResolved = true;
            onComplete?.Invoke(true, "Connected to the available llama.cpp server.");
            yield break;
        }

        // note: Start the configured local server before considering a separately managed Ollama process.
        config.backend = YQLlmBackend.LlamaCpp;
        bool started = false;
        string startupMessage = string.Empty;
        yield return EnsureLlamaCppReady(config, (ok, message) =>
        {
            started = ok;
            startupMessage = message;
        });
        if (started)
        {
            _runtimeBackendResolved = true;
            onComplete?.Invoke(true, startupMessage);
            yield break;
        }

        // note: Ollama remains a compatibility fallback when the owned executable or model cannot be started.
        bool ollamaReady = false;
        string ollamaBase = string.IsNullOrWhiteSpace(config.ollamaApiUrl)
            ? apiUrl
            : config.ollamaApiUrl;
        string ollamaProbe = (ollamaBase ?? string.Empty).Trim().TrimEnd('/') + "/api/tags";
        yield return ProbeLocalHealth(ollamaProbe, 2, (ok, _) => ollamaReady = ok);
        if (ollamaReady)
        {
            config.backend = YQLlmBackend.Ollama;
            _runtimeBackendResolved = true;
            onComplete?.Invoke(true, "Connected to the available Ollama server after llama.cpp startup failed.");
            yield break;
        }

        config.backend = YQLlmBackend.Ollama;
        onComplete?.Invoke(false,
            "No local LLM backend is reachable. llama.cpp: " + startupMessage +
            "; Ollama endpoint: " + ollamaProbe);
    }

    private static bool MatchesOllamaModelDigest(string body, string modelName, string digest)
    {
        try
        {
            if (!(JObject.Parse(body ?? string.Empty)["models"] is JArray models)) return false;
            foreach (JObject entry in models)
                if (string.Equals(entry.Value<string>("name"), modelName, StringComparison.Ordinal) &&
                    string.Equals(entry.Value<string>("digest"), digest, StringComparison.Ordinal)) return true;
        }
        catch (Exception) { return false; }
        return false;
    }

    private static IEnumerator ProbeLocalHealth(string url, int timeoutSeconds, Action<bool, string> onComplete, bool includeResponseBody = false)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri parsed) ||
            (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            onComplete?.Invoke(false, "Invalid local LLM health URL.");
            yield break;
        }

        using (UnityWebRequest request = UnityWebRequest.Get(parsed.AbsoluteUri))
        {
            request.timeout = Mathf.Max(1, timeoutSeconds);
            yield return request.SendWebRequest();
            bool ok = request.result == UnityWebRequest.Result.Success && request.responseCode >= 200 && request.responseCode < 500;
            onComplete?.Invoke(ok, ok ? (includeResponseBody ? request.downloadHandler.text : string.Empty) : request.error);
        }
    }

    private LLMRuntimeConfig ActiveConfig()
    {
        if (runtimeConfig != null)
        {
            _usingRuntimeDefaultConfig = false;
            _runtimeBackendResolved = true;
            _activeConfig = runtimeConfig;
        }

        if (_activeConfig == null)
        {
            _usingRuntimeDefaultConfig = true;
            _runtimeBackendResolved = false;
            _activeConfig = LLMRuntimeConfig.CreateRuntimeDefault();
        }

        if (_usingRuntimeDefaultConfig)
        {
            // note: Scene/bootstrap legacy fields remain the fallback for roles without an override; the runtime factory owns the selected backend.
            if (!string.IsNullOrWhiteSpace(apiUrl))
                _activeConfig.ollamaApiUrl = apiUrl;
            if (!string.IsNullOrWhiteSpace(model))
                _activeConfig.ollamaModel = model;
        }

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
        bool forceJson,
        Dictionary<string, object> jsonSchema)
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
                // note: Distinct world-generation prompts produced 200-300 MB cache evictions in the runtime log; responsiveness mode rejects that churn even when connecting to an external server.
                { "cache_prompt", !config.preserveGameResponsiveness }
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
                // note: JSON-object mode is the compatibility fallback; an explicit schema below upgrades llama.cpp to grammar-constrained generation.
                payload["response_format"] = new Dictionary<string, string> { { "type", "json_object" } };

                if (jsonSchema != null && jsonSchema.Count > 0)
                {
                    // note: llama.cpp converts this per-request schema to GBNF, while the owning Unity validator still decides whether the content is semantically acceptable.
                    payload["json_schema"] = jsonSchema;
                }
            }

            return JsonConvert.SerializeObject(payload);
        }

        Dictionary<string, object> ollamaOptions = new Dictionary<string, object>(options);
        // note: Keep the automatic live-gameplay residency protection when the default backend is Ollama; explicit request/config choices retain authority.
        ApplyOllamaLivePresentationPolicy(config, ollamaOptions, _usingRuntimeDefaultConfig,
            IsExclusiveSequenceActive, YourQuestTutorialAutoBootstrap.GameplayPresentationReleased);
        // note: Match the compiled context ceiling and permit a smaller bounded request without duplicate sampling keys.
        ollamaOptions["num_ctx"] = Mathf.Min(Mathf.Clamp(config.contextSizeTokens, 2048, 32768),
            Mathf.Clamp(ReadIntOption(options, "num_ctx", config.contextSizeTokens), 2048, 32768));

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

        // note: Bounded dialogue and Goddess speech/review use closed grammars; other generation retains its existing format.
        bool constrainDialogue = profile != null && profile.category == LLMGenerationCategory.Dialogue &&
            (debugTag ?? string.Empty).StartsWith("DialogueRepair:", StringComparison.Ordinal);
        bool constrainGoddess = profile != null && (profile.category == LLMGenerationCategory.GoddessCommentary ||
            profile.category == LLMGenerationCategory.GoddessVerification);
        OllamaRequest payloadOllama = new OllamaRequest
        {
            model = profile != null && !string.IsNullOrWhiteSpace(profile.ollamaModel)
                ? profile.ollamaModel.Trim()
                : (string.IsNullOrWhiteSpace(config.ollamaModel) ? model : config.ollamaModel),
            prompt = prompt,
            stream = false,
            // note: Ollama unloads its model after the same bounded idle window used by the owned llama.cpp server.
            keep_alive = config.closeOwnedServerWhenIdle
                ? Mathf.Max(5, config.ownedServerIdleTimeoutSeconds).ToString() + "s"
                : null,
            // note: Backend shape constraints never replace the domain parser or semantic acceptance checks.
            format = jsonOutput ? (profile != null && (profile.category == LLMGenerationCategory.DialogueVerification || constrainDialogue || constrainGoddess) &&
                jsonSchema != null && jsonSchema.Count > 0 ? (object)jsonSchema : "json") : null,
            // note: Both bounded Goddess roles stay direct; a model alias must not spend the reply budget in hidden reasoning.
            think = constrainGoddess ? (bool?)false : null,
            options = ollamaOptions
        };

        return JsonConvert.SerializeObject(payloadOllama);
    }

    private static void ApplyOllamaLivePresentationPolicy(LLMRuntimeConfig config,
        Dictionary<string, object> options, bool runtimeDefault, bool exclusiveSequence, bool presentationReleased)
    {
        // note: num_gpu=0 is a per-request CPU placement choice, not a server-wide setting; never overwrite an explicit placement, including automatic (-1).
        if (runtimeDefault && !exclusiveSequence && presentationReleased && config.preserveGameResponsiveness &&
            !options.ContainsKey("num_gpu"))
            options["num_gpu"] = 0;
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
            QueuedRequest evicted = _normalQueue.Dequeue();
            QueueEvictionCount++;
            CompleteRequest(
                evicted,
                false,
                null,
                "Background request evicted to admit higher-priority work.",
                Mathf.Max(0f, Time.unscaledTime - evicted.firstQueuedAt),
                0f,
                default,
                YQLlmTerminalOutcome.Evicted);
            return true;
        }

        error = "LLM request queue is full (" + PendingRequestCount + "/" + maxDepth + ").";
        return false;
    }

    public bool CancelRequest(long requestId, string reason = null)
    {
        if (requestId <= 0 || _terminalRequestIds.Contains(requestId))
            return false;

        return TerminalizeRequestById(
            requestId,
            YQLlmTerminalOutcome.Cancelled,
            string.IsNullOrWhiteSpace(reason) ? "LLM request cancelled by its owner." : reason,
            true);
    }

    private void InvalidateForProfileLifecycle()
    {
        // note: Profile switches supersede every outstanding model result before the shared state managers are replaced.
        CancelAllPending(YQLlmTerminalOutcome.Superseded, "LLM request superseded by a profile lifecycle transition.");
        _exclusiveSequenceOwner = string.Empty;
        _exclusiveSequenceStartedAt = 0f;
        EnsureQueueProcessorRunning();
    }

    private void CancelRequestsOwnedBy(string owner, YQLlmTerminalOutcome outcome, string reason)
    {
        if (string.IsNullOrWhiteSpace(owner))
            return;

        List<long> ids = new List<long>();
        CollectOwnedIds(_exclusiveQueue, owner, ids);
        CollectOwnedIds(_highPriorityQueue, owner, ids);
        CollectOwnedIds(_normalQueue, owner, ids);
        foreach (KeyValuePair<long, QueuedRequest> retry in _retryingRequests)
        {
            if (string.Equals(retry.Value.ownerId, owner, StringComparison.Ordinal))
                ids.Add(retry.Key);
        }
        if (_activeRequestValid && string.Equals(_activeRequest.ownerId, owner, StringComparison.Ordinal))
            ids.Add(_activeRequest.id);

        for (int index = 0; index < ids.Count; index++)
            TerminalizeRequestById(ids[index], outcome, reason, true);
    }

    private void CancelAllPending(YQLlmTerminalOutcome outcome, string reason)
    {
        List<long> ids = new List<long>();
        CollectIds(_exclusiveQueue, ids);
        CollectIds(_highPriorityQueue, ids);
        CollectIds(_normalQueue, ids);
        foreach (KeyValuePair<long, QueuedRequest> retry in _retryingRequests)
            ids.Add(retry.Key);
        if (_activeRequestValid)
            ids.Add(_activeRequest.id);

        for (int index = 0; index < ids.Count; index++)
            TerminalizeRequestById(ids[index], outcome, reason, true);
    }

    private void CollectOwnedIds(Queue<QueuedRequest> queue, string owner, List<long> ids)
    {
        foreach (QueuedRequest request in queue)
        {
            if (string.Equals(request.ownerId, owner, StringComparison.Ordinal))
                ids.Add(request.id);
        }
    }

    private void CollectIds(Queue<QueuedRequest> queue, List<long> ids)
    {
        foreach (QueuedRequest request in queue)
            ids.Add(request.id);
    }

    private bool TerminalizeRequestById(long requestId, YQLlmTerminalOutcome outcome, string reason, bool countCancellation)
    {
        if (_terminalRequestIds.Contains(requestId))
            return false;

        QueuedRequest request;
        if (TryRemoveQueuedRequest(_exclusiveQueue, requestId, out request) ||
            TryRemoveQueuedRequest(_highPriorityQueue, requestId, out request) ||
            TryRemoveQueuedRequest(_normalQueue, requestId, out request))
        {
            if (countCancellation && outcome == YQLlmTerminalOutcome.Cancelled)
                CancellationCount++;
            CompleteRequest(request, false, null, reason, Mathf.Max(0f, Time.unscaledTime - request.firstQueuedAt), 0f, default, outcome);
            return true;
        }

        if (_retryingRequests.TryGetValue(requestId, out request))
        {
            _retryingRequests.Remove(requestId);
            if (countCancellation && outcome == YQLlmTerminalOutcome.Cancelled)
                CancellationCount++;
            CompleteRequest(request, false, null, reason, Mathf.Max(0f, Time.unscaledTime - request.firstQueuedAt), 0f, default, outcome);
            return true;
        }

        if (_activeRequestValid && _activeRequest.id == requestId)
        {
            request = _activeRequest;
            if (countCancellation && outcome == YQLlmTerminalOutcome.Cancelled)
                CancellationCount++;
            CompleteRequest(request, false, null, reason, Mathf.Max(0f, Time.unscaledTime - request.firstQueuedAt), Mathf.Max(0f, Time.unscaledTime - _activeRequestStartedAt), default, outcome);
            AbortActiveWebRequest();
            return true;
        }

        return false;
    }

    private bool TryRemoveQueuedRequest(Queue<QueuedRequest> queue, long requestId, out QueuedRequest removed)
    {
        removed = default;
        bool found = false;
        int count = queue.Count;
        for (int index = 0; index < count; index++)
        {
            QueuedRequest candidate = queue.Dequeue();
            if (!found && candidate.id == requestId)
            {
                removed = candidate;
                found = true;
            }
            else
            {
                queue.Enqueue(candidate);
            }
        }
        return found;
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
        QueueEvictionCount++;

        CompleteRequest(request, false, null, LastError, age, 0f, default, YQLlmTerminalOutcome.Evicted);
        return true;
    }

    private bool IsRequestCurrent(QueuedRequest request)
    {
        // note: NPC/thinker lifetime is narrower than profile lifetime; stale child work cannot dispatch another repair.
        if (request.ownerStillCurrent != null)
        {
            try { if (!request.ownerStillCurrent()) return false; }
            catch (Exception) { return false; }
        }
        if (request.generationEpoch >= 0 && !YQServiceLifecycle.IsCurrent(request.generationEpoch))
            return false;

        string activeProfileId = YQProfileSaveSystem.Instance != null
            ? YQProfileSaveSystem.Instance.ActiveProfileId
            : (PlayerStateManager.Instance != null && PlayerStateManager.Instance.state != null
                ? PlayerStateManager.Instance.state.playerId
                : string.Empty);
        if (!string.IsNullOrWhiteSpace(request.profileId) &&
            !string.Equals(request.profileId, activeProfileId, StringComparison.OrdinalIgnoreCase))
            return false;

        WorldState activeWorld = WorldStateManager.Instance != null ? WorldStateManager.Instance.State : null;
        string activeWorldId = activeWorld != null && activeWorld.worldIdentity != null
            ? activeWorld.worldIdentity.worldId
            : string.Empty;
        if (!string.IsNullOrWhiteSpace(request.worldId) &&
            !string.Equals(request.worldId, activeWorldId, StringComparison.OrdinalIgnoreCase))
            return false;

        PlayerState player = PlayerStateManager.Instance != null ? PlayerStateManager.Instance.state : null;
        if (request.playerStateRevision >= 0 && (player == null || player.stateRevision != request.playerStateRevision))
            return false;
        if (request.worldStateRevision >= 0 && (activeWorld == null || activeWorld.stateRevision != request.worldStateRevision))
            return false;

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
        // note: Legacy progression submissions use the same domain route as typed progression requests.
        if (tag.StartsWith("ProgressionDecision", StringComparison.OrdinalIgnoreCase))
            return LLMGenerationCategory.Progression;
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
        // note: Preserve an explicitly externalized owned server when requested; normal config defaults still close it on teardown.
        _llamaServer.Dispose(config == null || config.closeOwnedServerOnQuit);
        _llamaServer = null;
    }

    private void BeginShutdown()
    {
        if (_quitting)
            return;

        _quitting = true;
        _lastLlmActivityTime = Time.realtimeSinceStartup;
        // note: Teardown terminalizes queued, retrying, and active work so every admitted request receives exactly one terminal result.
        CancelAllPending(YQLlmTerminalOutcome.Cancelled, "LLM client is shutting down.");
        _exclusiveSequenceOwner = string.Empty;
        _exclusiveSequenceStartedAt = 0f;

        // note: Background curation is disposable during teardown; aborting its loopback request prevents Unity from appearing hung while exiting Play Mode.
        AbortActiveWebRequest();
    }

    private void AbortActiveWebRequest()
    {
        UnityWebRequest request = _activeWebRequest;
        _activeWebRequest = null;
        if (request == null)
            return;

        try
        {
            request.Abort();
        }
        catch (Exception ex)
        {
            Debug.LogWarning(
                "[LLMClient] Could not abort the active request during shutdown: " +
                ex.Message);
        }
    }

    private void SafeInvoke(Action<string> cb, string value)
    {
        SafeInvoke(cb, value, null);
    }
}
