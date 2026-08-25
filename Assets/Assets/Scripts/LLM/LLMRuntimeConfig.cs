using System;
using UnityEngine;

public enum YQLlmBackend
{
    Ollama = 0,
    LlamaCpp = 1
}

public enum YQLlmRuntimeState
{
    Disabled = 0,
    Starting = 1,
    Ready = 2,
    Busy = 3,
    Recovering = 4,
    Faulted = 5
}

public enum LLMGenerationCategory
{
    Default = 0,
    Dialogue = 1,
    GoddessCommentary = 2,
    OriginGeneration = 3,
    WorldGeneration = 4,
    NpcPopulation = 5,
    QuestGeneration = 6,
    StructuredState = 7,
    Summarization = 8
}

[Serializable]
public sealed class LLMGenerationProfile
{
    public LLMGenerationCategory category = LLMGenerationCategory.Default;
    [Range(64, 6800)] public int maxOutputTokens = 512;
    [Range(0.05f, 1.5f)] public float temperature = 0.7f;
    [Range(0.05f, 1f)] public float topP = 0.8f;
    [Range(1, 100)] public int topK = 20;
    [Range(0f, 2f)] public float presencePenalty = 1.5f;
    [Range(0.8f, 2.5f)] public float repeatPenalty = 1.0f;
    public bool preferJson = false;
    public bool directMode = true;
    public bool reasoningMode = false;
    public string[] stopSequences = Array.Empty<string>();
}

[CreateAssetMenu(menuName = "YourQuest/LLM Runtime Config")]
public sealed class LLMRuntimeConfig : ScriptableObject
{
    [Header("Backend")]
    public YQLlmBackend backend = YQLlmBackend.LlamaCpp;
    public bool enableRuntimeLlm = true;

    [Header("llama.cpp Server")]
    public string llamaServerExecutablePath = "C:\\Ai\\llama.cpp\\llama-server.exe";
    public string ggufModelPath = "C:\\Ai\\Text Models\\Qwen3.5-4B-Q4_K_M.gguf";
    public string serverHost = "127.0.0.1";
    [Range(1024, 65535)] public int serverPort = 11435;
    [Range(2048, 32768)] public int contextSizeTokens = 12288;
    [Range(1, 4)] public int serverParallelSlots = 1;
    [Range(-1, 80)] public int gpuLayerCount = -1;
    [Range(512, 8192)] public int targetGpuHeadroomMb = 3072;
    public bool enableFlashAttention = true;
    public bool keepKvCacheInSystemRam = false;
    public bool closeOwnedServerOnQuit = true;
    [Range(1, 60)] public int startupTimeoutSeconds = 30;
    [Range(1, 5)] public int startupRecoveryAttempts = 1;
    [Range(1, 5)] public int helpProbeTimeoutSeconds = 2;
    public string extraLlamaServerArguments = string.Empty;

    [Header("Frame-Friendly Inference")]
    public bool preserveGameResponsiveness = true;
    // note: Reserve meaningful scheduler headroom for Unity's presentation thread even when local inference takes longer as a result.
    [Range(1, 16)] public int reservedCpuThreads = 4;
    [Range(32, 2048)] public int promptBatchSize = 128;
    [Range(16, 512)] public int promptMicroBatchSize = 32;
    [Range(0, 100)] public int serverPollingPercent = 0;
    public bool staggerResponseHandoffAcrossFrames = true;

    [Header("Legacy Ollama")]
    public string ollamaModel = "llama3.1";
    public string ollamaApiUrl = "http://127.0.0.1:11434";

    [Header("Budgets")]
    [Range(64, 2048)] public int contextSafetyTokens = 256;
    [Range(5000, 50000)] public int hardPromptCharacterLimit = 30000;
    [Range(16, 256)] public int maxQueueDepth = 24;
    // note: Qwen direct-mode prompts must explicitly suppress hidden reasoning so short structured budgets produce a playable answer instead of an empty final-content field.
    public bool emitQwenDirectModeToken = true;
    public string qwenDirectModeToken = "/no_think";

    [Header("Reliability")]
    [Range(0, 3)] public int transientRequestRetries = 1;
    [Range(0.1f, 10f)] public float retryBaseDelaySeconds = 0.75f;
    [Range(0.1f, 30f)] public float retryMaxDelaySeconds = 4f;
    [Range(256, 12000)] public int maxStoredDiagnosticCharacters = 2400;

    [Header("Profiles")]
    public LLMGenerationProfile[] generationProfiles = CreateDefaultProfiles();

    public static LLMRuntimeConfig CreateRuntimeDefault()
    {
        LLMRuntimeConfig config = CreateInstance<LLMRuntimeConfig>();
        config.generationProfiles = CreateDefaultProfiles();
        return config;
    }

    public LLMGenerationProfile GetProfile(LLMGenerationCategory category)
    {
        if (generationProfiles != null)
        {
            for (int i = 0; i < generationProfiles.Length; i++)
            {
                LLMGenerationProfile profile = generationProfiles[i];
                if (profile != null && profile.category == category)
                    return profile;
            }
        }

        return DefaultProfile(category);
    }

    public string BuildBaseUrl()
    {
        string host = string.IsNullOrWhiteSpace(serverHost) ? "127.0.0.1" : serverHost.Trim();
        return "http://" + host + ":" + Mathf.Clamp(serverPort, 1024, 65535);
    }

    private static LLMGenerationProfile[] CreateDefaultProfiles()
    {
        return new[]
        {
            DefaultProfile(LLMGenerationCategory.Default),
            DefaultProfile(LLMGenerationCategory.Dialogue),
            DefaultProfile(LLMGenerationCategory.GoddessCommentary),
            DefaultProfile(LLMGenerationCategory.OriginGeneration),
            DefaultProfile(LLMGenerationCategory.WorldGeneration),
            DefaultProfile(LLMGenerationCategory.NpcPopulation),
            DefaultProfile(LLMGenerationCategory.QuestGeneration),
            DefaultProfile(LLMGenerationCategory.StructuredState),
            DefaultProfile(LLMGenerationCategory.Summarization)
        };
    }

    private static LLMGenerationProfile DefaultProfile(LLMGenerationCategory category)
    {
        LLMGenerationProfile profile = new LLMGenerationProfile
        {
            category = category,
            maxOutputTokens = 512,
            temperature = 0.7f,
            topP = 0.8f,
            topK = 20,
            presencePenalty = 1.5f,
            repeatPenalty = 1.0f,
            preferJson = false,
            directMode = true,
            reasoningMode = false,
            stopSequences = Array.Empty<string>()
        };

        switch (category)
        {
            case LLMGenerationCategory.Dialogue:
                profile.maxOutputTokens = 128;
                profile.temperature = 0.7f;
                profile.topP = 0.8f;
                profile.stopSequences = new[] { "\n\nPLAYER_MESSAGE:", "\n\nRECENT_DIALOGUE:", "```" };
                break;
            case LLMGenerationCategory.GoddessCommentary:
                profile.maxOutputTokens = 260;
                profile.temperature = 0.82f;
                profile.topP = 0.86f;
                profile.repeatPenalty = 1.05f;
                break;
            case LLMGenerationCategory.OriginGeneration:
                profile.maxOutputTokens = 1000;
                profile.temperature = 0.35f;
                profile.topP = 0.86f;
                profile.preferJson = true;
                break;
            case LLMGenerationCategory.WorldGeneration:
                profile.maxOutputTokens = 3000;
                profile.temperature = 0.36f;
                profile.topP = 0.84f;
                profile.preferJson = true;
                // note: Direct structured output is faster and more reliable than spending a small local model's budget on hidden reasoning tokens.
                profile.reasoningMode = false;
                profile.repeatPenalty = 1.05f;
                break;
            case LLMGenerationCategory.NpcPopulation:
                profile.maxOutputTokens = 2600;
                profile.temperature = 0.58f;
                profile.topP = 0.86f;
                profile.preferJson = true;
                break;
            case LLMGenerationCategory.QuestGeneration:
                profile.maxOutputTokens = 1800;
                profile.temperature = 0.55f;
                profile.topP = 0.84f;
                profile.preferJson = true;
                profile.reasoningMode = true;
                break;
            case LLMGenerationCategory.StructuredState:
                profile.maxOutputTokens = 900;
                profile.temperature = 0.28f;
                profile.topP = 0.72f;
                profile.preferJson = true;
                break;
            case LLMGenerationCategory.Summarization:
                profile.maxOutputTokens = 700;
                profile.temperature = 0.35f;
                profile.topP = 0.78f;
                break;
        }

        return profile;
    }
}
