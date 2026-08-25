// note: Defines the typed request and result contracts used by the release LLM pipeline.
using System;
using System.Collections.Generic;

public enum YQLlmRequestPriority
{
    Background = 0,
    PlayerFacing = 1,
    StartupExclusive = 2
}

public sealed class YQLlmRequest
{
    // note: The prompt is immutable once queued so diagnostics and retries describe the same intended work.
    public string prompt;
    public string debugTag;
    public LLMGenerationCategory category = LLMGenerationCategory.Default;
    public YQLlmRequestPriority priority = YQLlmRequestPriority.Background;
    public bool requireJson;
    // note: Domain parsers may repair optional presentation fields while still strictly validating canonical gameplay data.
    public bool deferJsonValidationToCaller;
    public bool disableTimeout;
    public string exclusiveOwner;
    public int maxRetries = -1;
    public Dictionary<string, object> optionsOverride;
}

public readonly struct YQLlmRequestResult
{
    public readonly long requestId;
    public readonly string debugTag;
    public readonly LLMGenerationCategory category;
    public readonly bool success;
    public readonly string text;
    public readonly string error;
    public readonly int attemptCount;
    public readonly float queueWaitSeconds;
    public readonly float generationSeconds;
    public readonly LLMCompiledPrompt compiledPrompt;

    // note: Capture response metadata with the content so callers can decide whether to accept, retry, or use fallback.
    public YQLlmRequestResult(
        long requestId,
        string debugTag,
        LLMGenerationCategory category,
        bool success,
        string text,
        string error,
        int attemptCount,
        float queueWaitSeconds,
        float generationSeconds,
        LLMCompiledPrompt compiledPrompt)
    {
        this.requestId = requestId;
        this.debugTag = debugTag ?? string.Empty;
        this.category = category;
        this.success = success;
        this.text = text;
        this.error = error ?? string.Empty;
        this.attemptCount = attemptCount;
        this.queueWaitSeconds = queueWaitSeconds;
        this.generationSeconds = generationSeconds;
        this.compiledPrompt = compiledPrompt;
    }
}
