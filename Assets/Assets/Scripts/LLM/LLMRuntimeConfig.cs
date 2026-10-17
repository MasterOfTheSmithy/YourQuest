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
    GoddessVerification = 11,
    // note: Append content roles; existing assets and queued category numbers remain compatible.
    Equipment = 12,
    Consumable = 13,
    SkillSpell = 14,
    TitleClass = 15,
    Lore = 16
}

[Serializable]
public sealed class LLMGenerationProfile
{
    public LLMGenerationCategory category = LLMGenerationCategory.Default;
    // note: An empty override preserves the configured single-model backend; Ollama roles resolve independently for each request.
    public string ollamaModel = string.Empty;
    // note: Empty GGUF settings inherit the asset defaults. Overrides belong to the existing single queue, not another loader.
    public string ggufModelPath = string.Empty;
    public int contextSizeTokens = 0;
    public int gpuLayerCount = -1;
    public int reasoningBudget = -1;
    public int modelBlockCount = 0;
    public string systemPrompt = string.Empty;
    public bool suppressLegacyDirectToken = false;
    public float minP = -1f;
    // note: Runtime-default roles can select the server that best fits them without changing the legacy backend of explicit assets.
    [JsonIgnore] public bool useBackendOverride;
    [JsonIgnore] public YQLlmBackend backendOverride = YQLlmBackend.Ollama;
    [Range(64, 6800)] public int maxOutputTokens = 512;
    [Range(0f, 1.5f)] public float temperature = 0.7f;
    [Range(0.05f, 1f)] public float topP = 0.8f;
    [Range(0, 100)] public int topK = 20;
    [Range(0f, 2f)] public float presencePenalty = 1.5f;
    [Range(0.8f, 2.5f)] public float repeatPenalty = 1.0f;
    public bool preferJson = false;
    public bool directMode = true;
    public bool reasoningMode = false;
    // note: A role can request CPU placement before residency arbitration; an explicit request override still wins.
    public bool cpuOnly = false;
    // note: CPU is an explicit model exception, bound to its exact GGUF path or Ollama tag; cpuOnly alone is not permission.
    public string cpuSuitableModel = string.Empty;
    public string[] stopSequences = Array.Empty<string>();
}

[CreateAssetMenu(menuName = "YourQuest/LLM Runtime Config")]
public sealed class LLMRuntimeConfig : ScriptableObject
{
    // note: Explicit Editor launch isolation is immutable for this process and never changes a saved config or a player build.
#if UNITY_EDITOR
    public static readonly bool BenchmarkIsolationActive = Array.IndexOf(Environment.GetCommandLineArgs(), "-yqLlmBenchmarkIsolation") >= 0;
#else
    public static readonly bool BenchmarkIsolationActive = false;
#endif

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
    // note: Reserve growth space above the already resident game; fitting chooses partial GPU layers when the 8 GB device cannot hold the whole model.
    [Range(1536, 8192)] public int targetGpuHeadroomMb = 1536;
    public bool enableFlashAttention = true;
    public bool useQuantizedKvCache = false;
    public bool requireOwnedLlamaServer = false;
    [NonSerialized] public bool cpuInferenceApproved;
    public int modelBlockCount = 0;
    public int reasoningBudget = -1;
    // note: Recurrent models need bounded host checkpoints for actual prefix reuse; zero preserves legacy no-snapshot behavior.
    public int promptCacheRamMb = 0;
    public int promptCheckpointCount = 2;
    public int promptCheckpointMinTokens = 256;
    public bool keepKvCacheInSystemRam = false;
    public bool closeOwnedServerOnQuit = true;
    // note: Release VRAM after a quiet period while leaving externally managed servers untouched.
    public bool closeOwnedServerWhenIdle = true;
    [Range(5, 300)] public int ownedServerIdleTimeoutSeconds = 45;
    [Range(1, 60)] public int startupTimeoutSeconds = 30;
    [Range(1, 5)] public int startupRecoveryAttempts = 1;
    [Range(1, 5)] public int helpProbeTimeoutSeconds = 2;
    public string extraLlamaServerArguments = string.Empty;

    [Header("Bounded Model Reuse")]
    // note: Reuse only the active slot on an owned, capability-checked server; no host snapshots are retained.
    public bool enablePromptPrefixReuse = true;
    public bool retainVerifiedCpuHelpers = true;
    [Range(256, 4096)] public int maximumCpuHelperResidentMb = 2048;
    [Range(2048, 16384)] public int minimumFreeSystemMemoryMb = 4096;

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
    // note: Give a player time to reply before restartable background work replaces the conversational model; ageing bounds this extra wait.
    [Range(0, 10)] public float playerReplyGraceSeconds = 3f;
    [Range(0, 30)] public float maximumBackgroundGraceWaitSeconds = 12f;

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
    // note: User-authorized practical presentation mode is distinct from semantic qualification; structured gameplay acceptance never reads this flag.
    public bool enableBestEffortSpeech = false;
    public string bestEffortSpeechModelSha256 = string.Empty;
    public bool BestEffortSpeechAvailable => enableBoundedRepair && enableBestEffortSpeech &&
        !string.IsNullOrWhiteSpace(bestEffortSpeechModelSha256) &&
        GetBackend(LLMGenerationCategory.GoddessCommentary) == YQLlmBackend.LlamaCpp;
    // note: Verification is enabled only after source-bound qualification and remains pinned to that model digest.
    public bool dialogueVerifierQualified = false;
    public string dialogueVerifierModelDigest = string.Empty;
    public string dialogueVerifierContractHash = string.Empty;
    // note: Model identity alone cannot qualify a changed prompt, sampler or reasoning transport.
    public string DialogueVerificationContractHash => YQRepairEpisode.Hash("local_review_chat_v3_span_patch" + YQSpeechPatch.ContractHash +
        YQDialogueGrounding.BuildPatchPrompt("{}", string.Empty, string.Empty) +
        JsonConvert.SerializeObject(GetProfile(LLMGenerationCategory.DialogueVerification)) +
        JsonConvert.SerializeObject(new { route = GetBackend(LLMGenerationCategory.DialogueVerification), ggufModelPath, llamaServerExecutablePath,
            dialogueVerifierModelDigest, contextSizeTokens, contextSafetyTokens, hardPromptCharacterLimit,
            emitQwenDirectModeToken, qwenDirectModeToken, preserveGameResponsiveness, promptBatchSize, reservedCpuThreads }));
    public bool HasQualifiedDialogueVerifier => enableBoundedRepair && SupportsQualification(LLMGenerationCategory.DialogueVerification) &&
        dialogueVerifierQualified && !string.IsNullOrWhiteSpace(dialogueVerifierModelDigest) &&
        string.Equals(dialogueVerifierContractHash, DialogueVerificationContractHash, StringComparison.Ordinal);
    public bool goddessVerifierQualified = false;
    public string goddessVerifierModelDigest = string.Empty;
    public string goddessVerifierContractHash = string.Empty;
    // note: Full authored speech and its reviewer are qualified independently and pinned to this profile and protocol.
    public bool goddessSpeechPlanQualified = false;
    public string goddessSpeechPlanModelDigest = string.Empty;
    public string goddessSpeechPlanContractHash = string.Empty;
    public bool goddessSpeechPlanCpuOnly = false;
    public string GoddessSpeechPlanContractHash => YQRepairEpisode.Hash(YQGoddessSpeechPlan.ContractHash +
        JsonConvert.SerializeObject(GetProfile(LLMGenerationCategory.GoddessCommentary)) +
        JsonConvert.SerializeObject(new { backend, route = GetBackend(LLMGenerationCategory.GoddessCommentary), ggufModelPath, llamaServerExecutablePath, contextSizeTokens, contextSafetyTokens, hardPromptCharacterLimit,
            emitQwenDirectModeToken, qwenDirectModeToken, goddessSpeechPlanModelDigest, goddessSpeechPlanCpuOnly, preserveGameResponsiveness, promptBatchSize, reservedCpuThreads,
            closeOwnedServerWhenIdle, ownedServerIdleTimeoutSeconds, ollamaApiUrl, YQGoddessSpeechPlan.ContextTokens }));
    public bool HasQualifiedGoddessSpeechPlan => enableBoundedRepair && SupportsQualification(LLMGenerationCategory.GoddessCommentary) &&
        goddessSpeechPlanQualified && !string.IsNullOrWhiteSpace(goddessSpeechPlanModelDigest) &&
        string.Equals(goddessSpeechPlanContractHash, GoddessSpeechPlanContractHash, StringComparison.Ordinal);
    public string GoddessVerificationContractHash => YQRepairEpisode.Hash(YQGoddessGrounding.ReviewContractHash +
        JsonConvert.SerializeObject(GetProfile(LLMGenerationCategory.GoddessVerification)) +
        JsonConvert.SerializeObject(new { backend, route = GetBackend(LLMGenerationCategory.GoddessVerification), ggufModelPath, llamaServerExecutablePath, contextSizeTokens, contextSafetyTokens, hardPromptCharacterLimit,
            emitQwenDirectModeToken, qwenDirectModeToken, goddessVerifierModelDigest, goddessSpeechPlanCpuOnly,
            preserveGameResponsiveness, promptBatchSize, reservedCpuThreads, YQGoddessGrounding.ReviewContextTokens }));
    // note: Explicit assets must opt in to the same model/prompt qualification; a stale contract cannot authorize speech.
    public bool HasQualifiedGoddessVerifier => enableBoundedRepair && SupportsQualification(LLMGenerationCategory.GoddessVerification) &&
        goddessVerifierQualified && !string.IsNullOrWhiteSpace(goddessVerifierModelDigest) &&
        string.Equals(goddessVerifierContractHash, GoddessVerificationContractHash, StringComparison.Ordinal);

    // note: Existing serialized digest fields remain compatible; GGUF routes interpret them as the independently qualified file SHA-256.
    private bool SupportsQualification(LLMGenerationCategory category) =>
        GetBackend(category) == YQLlmBackend.Ollama || GetBackend(category) == YQLlmBackend.LlamaCpp;

    [Header("Profiles")]
    public LLMGenerationProfile[] generationProfiles = CreateDefaultProfiles();

    public static LLMRuntimeConfig CreateRuntimeDefault()
    {
        LLMRuntimeConfig config = CreateInstance<LLMRuntimeConfig>();
        config.generationProfiles = CreateDefaultProfiles();
        // note: Preserve the gated Goddess/reviewer routes while canonical records use the configured local control model.
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
        // note: Qualify the NPC reviewer independently on the same reasoning-capable transport as Goddess review.
        config.GetProfile(LLMGenerationCategory.DialogueVerification).ollamaModel = "yourquest-qwen35-chat:latest";
        config.GetProfile(LLMGenerationCategory.DialogueVerification).directMode = false;
        config.GetProfile(LLMGenerationCategory.DialogueVerification).reasoningMode = true;
        config.GetProfile(LLMGenerationCategory.DialogueVerification).cpuOnly = false;
        config.GetProfile(LLMGenerationCategory.DialogueVerification).maxOutputTokens = 2048;
        config.GetProfile(LLMGenerationCategory.GoddessCommentary).ollamaModel = "yourquest-qwen35-chat:latest";
        // note: The tiny helper failed live reconstruction despite valid grammar; keep it available for explicit experiments while default summaries use the control model.
        SetBackendOverride(config, LLMGenerationCategory.Summarization, YQLlmBackend.LlamaCpp);
        config.GetProfile(LLMGenerationCategory.Summarization).ollamaModel = "small-test:latest";
        config.GetProfile(LLMGenerationCategory.Summarization).cpuOnly = false;
        // note: Qualification is renewed against free prose and independent review; a literal pin prevents settings from self-qualifying.
        config.goddessSpeechPlanModelDigest = "525e6ea01e8d23b5858ba7f64f3fb407052d469af616cb6dea88f50708a6ec61";
        config.goddessSpeechPlanCpuOnly = false;
        config.goddessSpeechPlanQualified = false;
        config.goddessSpeechPlanContractHash = string.Empty;
        config.GetProfile(LLMGenerationCategory.GoddessVerification).ollamaModel = "yourquest-qwen35-chat:latest";
        config.GetProfile(LLMGenerationCategory.GoddessVerification).directMode = false;
        config.GetProfile(LLMGenerationCategory.GoddessVerification).reasoningMode = true;
        config.GetProfile(LLMGenerationCategory.GoddessVerification).maxOutputTokens = 2048;
        config.goddessVerifierModelDigest = "525e6ea01e8d23b5858ba7f64f3fb407052d469af616cb6dea88f50708a6ec61";
        // note: Expanded semantic checks did not qualify a verifier; keep activation gated.
        config.dialogueVerifierQualified = false;
        config.dialogueVerifierModelDigest = "525e6ea01e8d23b5858ba7f64f3fb407052d469af616cb6dea88f50708a6ec61";
        ConfigureScreenedLocalRoles(config);
        return config;
    }

    private static void ConfigureScreenedLocalRoles(LLMRuntimeConfig config)
    {
        // note: Provision verified live copies on the NVMe SSD with Install-LiveRuntime.ps1. Never fall back to the HDD archive or a different same-named GGUF.
        const string root = @"C:\Ai\YourQuestRuntime\Models";
        const string engine = @"C:\Ai\YourQuestRuntime\Runtime\b10828-cuda13.3\llama-server.exe";
        string control = System.IO.Path.Combine(root, "Qwen3.5-4B-Q4_K_M.gguf");
        if (!System.IO.File.Exists(engine) || !System.IO.File.Exists(control) ||
            !System.IO.File.Exists(System.IO.Path.Combine(root, "LFM2-1.2B-RAG-Q4_K_M.gguf")))
        {
            config.enableRuntimeLlm = false;
            Debug.LogError("[LLMRuntimeConfig] Verified SSD runtime is missing. Provision C:\\Ai\\YourQuestRuntime with Tools/LLM8GBBenchmark/Install-LiveRuntime.ps1.");
            return;
        }
        config.llamaServerExecutablePath = engine;
        config.ggufModelPath = control;
        config.targetGpuHeadroomMb = 1536;
        config.useQuantizedKvCache = true;
        config.requireOwnedLlamaServer = true;
        config.promptCacheRamMb = 256;
        config.emitQwenDirectModeToken = false;
        config.ownedServerIdleTimeoutSeconds = 120;

        void Route(LLMGenerationCategory role, string file, int context, float temperature,
            float topP = .8f, int topK = 20, bool thinking = false, float repeat = 1f)
        {
            string path = System.IO.Path.Combine(root, file);
            if (!System.IO.File.Exists(path)) return;
            var profile = config.GetProfile(role);
            SetBackendOverride(config, role, YQLlmBackend.LlamaCpp);
            profile.ggufModelPath = path;
            profile.contextSizeTokens = context;
            profile.gpuLayerCount = 999;
            profile.temperature = temperature;
            profile.topP = topP;
            profile.topK = topK;
            profile.presencePenalty = 0;
            profile.repeatPenalty = repeat;
            profile.directMode = !thinking;
            profile.reasoningMode = thinking;
            profile.reasoningBudget = thinking ? 768 : 0;
            // note: Counts identify the tested GGUF architecture; placement is always full GPU.
            profile.modelBlockCount = file.StartsWith("Spark-", StringComparison.Ordinal) ? 36 :
                file.StartsWith("granite-", StringComparison.Ordinal) ? 40 :
                file.StartsWith("MiniCPM5-2B", StringComparison.Ordinal) ? 42 :
                file.StartsWith("LFM2.5-2.6B", StringComparison.Ordinal) ? 30 :
                file.StartsWith("LFM2-1.2B", StringComparison.Ordinal) ? 16 :
                file.StartsWith("Qwen-3.5-2b", StringComparison.Ordinal) ? 25 :
                file.StartsWith("Qwen3.5-2B", StringComparison.Ordinal) ? 24 : 33;
            profile.suppressLegacyDirectToken = true;
            profile.cpuOnly = false;
        }

        // note: Composite transactions retain the control. NPC, equipment and skill specialists failed integration semantics and are not promoted.
        foreach (var role in new[] { LLMGenerationCategory.Default, LLMGenerationCategory.OriginGeneration,
            LLMGenerationCategory.WorldGeneration, LLMGenerationCategory.QuestGeneration,
            LLMGenerationCategory.StructuredState, LLMGenerationCategory.Progression,
            LLMGenerationCategory.NpcPopulation, LLMGenerationCategory.Equipment, LLMGenerationCategory.SkillSpell,
            LLMGenerationCategory.TitleClass, LLMGenerationCategory.Consumable, LLMGenerationCategory.Lore })
            Route(role, "Qwen3.5-4B-Q4_K_M.gguf", 12288, .28f);
        // note: GPU retesting favored Qwen over LFM for titles/classes; MiniCPM invented lore history, so neither is promoted as a specialist.
        Route(LLMGenerationCategory.Summarization, "LFM2-1.2B-RAG-Q4_K_M.gguf", 8192, 0);
        // note: All speech lanes now use the supported full-GPU transport; configuring a lane never grants semantic qualification.
        Route(LLMGenerationCategory.GoddessCommentary, "Qwen3.5-4B-Q4_K_M.gguf", 12288, .28f);
        Route(LLMGenerationCategory.GoddessVerification, "Qwen3.5-4B-Q4_K_M.gguf", 12288, 0);
        Route(LLMGenerationCategory.DialogueVerification, "Qwen3.5-4B-Q4_K_M.gguf", 12288, 0);
        // note: The screened local control stays resident for authoring and one practical correction; no Spark/Qwen swap on each NPC turn.
        Route(LLMGenerationCategory.Dialogue, "Qwen3.5-4B-Q4_K_M.gguf", 12288, .28f);
        config.enableBestEffortSpeech = true;
        config.bestEffortSpeechModelSha256 = "3874209241c9a397e2f62cd3f70f80fd2dfbf0dfccb6838416bdb48a714e8630";
        config.GetProfile(LLMGenerationCategory.GoddessVerification).maxOutputTokens = 400;
        config.GetProfile(LLMGenerationCategory.DialogueVerification).maxOutputTokens = 400;
        config.GetProfile(LLMGenerationCategory.Dialogue).systemPrompt =
            "Speak only as the NPC identified in the supplied context. The player is a different person. " +
            "Use accepted facts as facts; rumors, proposals, plans and unknowns remain uncertain. " +
            "Do not invent observations, completed actions, player speech, history or promises. Follow the requested output contract.";
        // note: Granite failed the Goddess semantic cases; retain the pre-existing gated route instead of promoting that candidate.
    }

    public LLMRuntimeConfig CreateRequestConfig(LLMGenerationCategory category)
    {
        var profile = GetProfile(category);
        // note: Freeze launch settings for the whole request, including retries/cancellation, without mutating a shared asset.
        var copy = Instantiate(this);
        copy.hideFlags = HideFlags.HideAndDontSave;
        string selectedModel = GetBackend(category) == YQLlmBackend.LlamaCpp
            ? (string.IsNullOrWhiteSpace(profile.ggufModelPath) ? ggufModelPath : profile.ggufModelPath)
            : profile.ollamaModel;
        copy.cpuInferenceApproved = profile.cpuOnly && !string.IsNullOrWhiteSpace(profile.cpuSuitableModel) &&
            string.Equals(profile.cpuSuitableModel, selectedModel, StringComparison.OrdinalIgnoreCase);
        if (GetBackend(category) != YQLlmBackend.LlamaCpp) return copy;
        copy.ggufModelPath = selectedModel;
        if (profile.contextSizeTokens > 0) copy.contextSizeTokens = Mathf.Clamp(profile.contextSizeTokens, 2048, 32768);
        copy.gpuLayerCount = copy.cpuInferenceApproved ? 0 : 999;
        copy.reasoningBudget = profile.reasoningBudget;
        copy.modelBlockCount = profile.modelBlockCount;
        if (profile.suppressLegacyDirectToken) copy.emitQwenDirectModeToken = false;
        return copy;
    }

    public static LLMGenerationCategory ContentCategory(string kind)
    {
        // note: Callers already know the requested contract; no classifier or inference of intent is needed.
        switch ((kind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "skill": case "spell": return LLMGenerationCategory.SkillSpell;
            case "title": case "class": return LLMGenerationCategory.TitleClass;
            case "item": case "equipment": return LLMGenerationCategory.Equipment;
            case "consumable": return LLMGenerationCategory.Consumable;
            case "lore": return LLMGenerationCategory.Lore;
            case "quest": return LLMGenerationCategory.QuestGeneration;
            default: return LLMGenerationCategory.Progression;
        }
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
        if (category >= LLMGenerationCategory.Equipment && category <= LLMGenerationCategory.Lore)
            return GetProfile(LLMGenerationCategory.Progression);

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
            DefaultProfile(LLMGenerationCategory.GoddessVerification),
            DefaultProfile(LLMGenerationCategory.Equipment),
            DefaultProfile(LLMGenerationCategory.Consumable),
            DefaultProfile(LLMGenerationCategory.SkillSpell),
            DefaultProfile(LLMGenerationCategory.TitleClass),
            DefaultProfile(LLMGenerationCategory.Lore)
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
            case LLMGenerationCategory.Equipment:
            case LLMGenerationCategory.Consumable:
            case LLMGenerationCategory.SkillSpell:
            case LLMGenerationCategory.TitleClass:
            case LLMGenerationCategory.Lore:
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
