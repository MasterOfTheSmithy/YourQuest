using System;
using Newtonsoft.Json;
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
    Faulted = 5,
    // note: An unloaded model can restart on demand; this is distinct from explicitly disabling generation.
    Standby = 6
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
    Summarization = 8,
    // note: Append the progression role so existing serialized category values retain their meaning.
    Progression = 9,
    // note: Verification has its own bounded route; preserve all previously serialized role values.
    DialogueVerification = 10,
    // note: Append a separate Goddess reviewer so NPC qualification and existing serialized roles stay independent.
    GoddessVerification = 11
}

[Serializable]
public sealed class LLMGenerationProfile
{
    public LLMGenerationCategory category = LLMGenerationCategory.Default;
    // note: An empty override preserves the configured single-model backend; Ollama roles resolve independently for each request.
    public string ollamaModel = string.Empty;
    // note: Runtime-default roles can select the server that best fits them without changing the legacy backend of explicit assets.
    [JsonIgnore] public bool useBackendOverride;
    [JsonIgnore] public YQLlmBackend backendOverride = YQLlmBackend.Ollama;
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
    // note: Explicit assets retain the owned llama.cpp default; the runtime factory below selects the screened local Ollama roles.
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
    // note: Release VRAM after a quiet period while leaving externally managed servers untouched.
    public bool closeOwnedServerWhenIdle = true;
    [Range(5, 300)] public int ownedServerIdleTimeoutSeconds = 45;
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
    // note: A bounded high-priority burst gives player-facing work preference without starving queued background curation.
    [Range(1, 8)] public int maxConsecutiveHighPriorityRequests = 3;

    [Header("Ollama")]
    public string ollamaModel = "llama3.1";
    public string ollamaApiUrl = "http://127.0.0.1:11434";
    // note: The existing client owns a headless local service when needed; remote and externally running services remain external.
    public bool startLocalOllamaOnDemand = true;
    public string ollamaExecutablePath = string.Empty;
    public string ollamaModelsDirectory = string.Empty;

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
    // note: Responses are rejected whole when they exceed this ceiling, preventing partial JSON or oversized prose from reaching domain code.
    [Range(1024, 100000)] public int maxResponseCharacters = 60000;
    // note: A stalled exclusive owner must not hold the queue forever if its scene/service disappears before releasing explicitly.
    [Range(15, 600)] public int exclusiveSequenceTimeoutSeconds = 120;

    [Header("Bounded Content Repair")]
    public bool enableBoundedRepair = false;
    // note: Verification is enabled only after source-bound qualification and remains pinned to that model digest.
    public bool dialogueVerifierQualified = false;
    public string dialogueVerifierModelDigest = string.Empty;
    public bool goddessVerifierQualified = false;
    public string goddessVerifierModelDigest = string.Empty;
    public string goddessVerifierContractHash = string.Empty;
    // note: Structured speech is qualified independently; failed free-prose classifiers never authorize this lane.
    public bool goddessSpeechPlanQualified = false;
    public string goddessSpeechPlanModelDigest = string.Empty;
    public string goddessSpeechPlanContractHash = string.Empty;
    public bool goddessSpeechPlanCpuOnly = false;
    public string GoddessSpeechPlanContractHash => YQRepairEpisode.Hash(YQGoddessSpeechPlan.ContractHash +
        JsonConvert.SerializeObject(GetProfile(LLMGenerationCategory.GoddessCommentary)) +
        JsonConvert.SerializeObject(new { backend, contextSizeTokens, contextSafetyTokens, hardPromptCharacterLimit,
            emitQwenDirectModeToken, qwenDirectModeToken, goddessSpeechPlanModelDigest, goddessSpeechPlanCpuOnly, preserveGameResponsiveness, promptBatchSize, reservedCpuThreads,
            closeOwnedServerWhenIdle, ownedServerIdleTimeoutSeconds, ollamaApiUrl, YQGoddessSpeechPlan.ContextTokens }));
    public bool HasQualifiedGoddessSpeechPlan => enableBoundedRepair && backend == YQLlmBackend.Ollama &&
        goddessSpeechPlanQualified && !string.IsNullOrWhiteSpace(goddessSpeechPlanModelDigest) &&
        string.Equals(goddessSpeechPlanContractHash, GoddessSpeechPlanContractHash, StringComparison.Ordinal);
    public string GoddessVerificationContractHash => YQRepairEpisode.Hash(YQGoddessGrounding.ReviewContractHash +
        JsonConvert.SerializeObject(GetProfile(LLMGenerationCategory.GoddessVerification)));
    // note: Explicit assets must opt in to the same model/prompt qualification; a stale contract cannot authorize speech.
    public bool HasQualifiedGoddessVerifier => enableBoundedRepair && backend == YQLlmBackend.Ollama &&
        goddessVerifierQualified && !string.IsNullOrWhiteSpace(goddessVerifierModelDigest) &&
        string.Equals(goddessVerifierContractHash, GoddessVerificationContractHash, StringComparison.Ordinal);

    [Header("Profiles")]
    public LLMGenerationProfile[] generationProfiles = CreateDefaultProfiles();

    public static LLMRuntimeConfig CreateRuntimeDefault()
    {
        LLMRuntimeConfig config = CreateInstance<LLMRuntimeConfig>();
        config.generationProfiles = CreateDefaultProfiles();
        // note: Keep the qualified Goddess lane on Ollama while routing canonical records to the configured Qwen3.5 llama.cpp model.
        config.backend = YQLlmBackend.Ollama;
        if (System.IO.Directory.Exists("D:\\OllamaModels")) config.ollamaModelsDirectory = "D:\\OllamaModels";
        config.enableBoundedRepair = true;
        SetBackendOverride(config, LLMGenerationCategory.Default, YQLlmBackend.LlamaCpp);
        SetBackendOverride(config, LLMGenerationCategory.OriginGeneration, YQLlmBackend.LlamaCpp);
        SetBackendOverride(config, LLMGenerationCategory.WorldGeneration, YQLlmBackend.LlamaCpp);
        SetBackendOverride(config, LLMGenerationCategory.NpcPopulation, YQLlmBackend.LlamaCpp);
        SetBackendOverride(config, LLMGenerationCategory.QuestGeneration, YQLlmBackend.LlamaCpp);
        SetBackendOverride(config, LLMGenerationCategory.StructuredState, YQLlmBackend.LlamaCpp);
        SetBackendOverride(config, LLMGenerationCategory.Progression, YQLlmBackend.LlamaCpp);
        config.GetProfile(LLMGenerationCategory.Dialogue).ollamaModel = "hf.co/Nubinu/Qwen3.5-4B-MiniFantasy-GGUF:Q4_K_M";
        config.GetProfile(LLMGenerationCategory.DialogueVerification).ollamaModel = "yourquest-qwen3-4b:latest";
        config.GetProfile(LLMGenerationCategory.GoddessCommentary).ollamaModel = "smaller-test:latest";
        // note: Short, temporary summaries use the installed fast tier; persistent state remains on the control model.
        config.GetProfile(LLMGenerationCategory.Summarization).ollamaModel = "small-test:latest";
        // note: Fourteen source-bound offline scenarios qualified this CPU route; a literal contract pin prevents settings or protocol changes from self-qualifying.
        config.goddessSpeechPlanModelDigest = "1dcf59c4b2d0b233363c689818a1c48dfd65e5c96f1594ba57f6d851e84f869e";
        config.goddessSpeechPlanCpuOnly = true;
        config.goddessSpeechPlanQualified = true;
        config.goddessSpeechPlanContractHash = "fc41a9fe0f20549846357088c4f41d8c5c1d06e14f53dbe12bf643950a29cc64";
        config.GetProfile(LLMGenerationCategory.GoddessVerification).ollamaModel = "yourquest-qwen3-4b:latest";
        // note: Expanded semantic checks did not qualify a verifier; keep activation gated.
        config.dialogueVerifierQualified = false;
        config.dialogueVerifierModelDigest = string.Empty;
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

        // note: Older explicit assets used StructuredState for progression; retain that profile until they configure the new role.
        if (category == LLMGenerationCategory.Progression)
            return GetProfile(LLMGenerationCategory.StructuredState);

        return DefaultProfile(category);
    }

    public YQLlmBackend GetBackend(LLMGenerationCategory category)
    {
        LLMGenerationProfile profile = GetProfile(category);
        return profile != null && profile.useBackendOverride ? profile.backendOverride : backend;
    }

    private static void SetBackendOverride(LLMRuntimeConfig config, LLMGenerationCategory category, YQLlmBackend selectedBackend)
    {
        // note: Keep model ownership in the existing category profile while selecting only its request transport.
        LLMGenerationProfile profile = config.GetProfile(category);
        profile.useBackendOverride = true;
        profile.backendOverride = selectedBackend;
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
            DefaultProfile(LLMGenerationCategory.Summarization),
            DefaultProfile(LLMGenerationCategory.Progression),
            DefaultProfile(LLMGenerationCategory.DialogueVerification),
            DefaultProfile(LLMGenerationCategory.GoddessVerification)
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
                profile.temperature = 0.65f;
                profile.topP = 0.9f;
                profile.repeatPenalty = 1.05f;
                // note: Dedicated voice generation uses its closed speech envelope, with repeated JSON keys unpenalized.
                profile.presencePenalty = 0f;
                profile.preferJson = true;
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
                // note: Reserve thinking mode for the control model's larger, cross-region world plan; validators still own acceptance.
                profile.directMode = false;
                profile.reasoningMode = true;
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
                profile.directMode = false;
                profile.reasoningMode = true;
                break;
            case LLMGenerationCategory.Progression:
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
            case LLMGenerationCategory.DialogueVerification:
                profile.maxOutputTokens = 240;
                profile.temperature = 0.05f;
                profile.topP = 0.72f;
                profile.preferJson = true;
                profile.presencePenalty = 0f;
                profile.reasoningMode = false;
                break;
            case LLMGenerationCategory.GoddessVerification:
                // note: Use the same closed review contract as dialogue with room for specific rejected clauses.
                profile.maxOutputTokens = 400;
                profile.temperature = 0.05f;
                profile.topP = 0.72f;
                profile.preferJson = true;
                profile.presencePenalty = 0f;
                profile.directMode = true;
                profile.reasoningMode = false;
                break;
        }

        return profile;
    }
}
