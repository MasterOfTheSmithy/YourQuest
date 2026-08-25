// Assets/Assets/Scripts/Gameplay/Systems/Director/DirectorThinkCycle.cs

using UnityEngine;

public class DirectorThinkCycle : MonoBehaviour
{
    [Header("References")]
    public LLMClient llmClient;
    public DirectorPromptBuilder promptBuilder;
    public DirectorDecisionApplier decisionApplier;

    [Header("Timing")]
    public float thinkEverySeconds = 18f;

    [Header("Debug")]
    public bool logPrompt = false;
    public bool logRawResponse = false;
    public bool logApplyResult = true;

    private float nextThink;

    private WorldStateManager worldStateManager;
    private PlayerStateManager playerStateManager;

    private void Awake()
    {
        if (llmClient == null) llmClient = LLMClient.Instance;
        if (promptBuilder == null) promptBuilder = GetComponent<DirectorPromptBuilder>();
        if (decisionApplier == null) decisionApplier = GetComponent<DirectorDecisionApplier>();

        // Unity 2022+ : FindObjectOfType is obsolete
        if (worldStateManager == null) worldStateManager = Object.FindFirstObjectByType<WorldStateManager>();
        if (playerStateManager == null) playerStateManager = Object.FindFirstObjectByType<PlayerStateManager>();

        nextThink = Time.time + thinkEverySeconds;
    }

    private void Update()
    {
        if (Time.time < nextThink) return;
        nextThink = Time.time + thinkEverySeconds;

        TryThink();
    }

    private void TryThink()
    {
        if (llmClient == null || promptBuilder == null || decisionApplier == null) return;
        if (llmClient.IsBusy) return;

        // Build the director prompt
        string prompt = promptBuilder.BuildDirectorPrompt();
        if (string.IsNullOrWhiteSpace(prompt)) return;

        if (logPrompt)
            Debug.Log("[DirectorThinkCycle] PROMPT\n" + prompt);

        // Your LLMClient exposes Enqueue(prompt, onResponse, debugTag)
        llmClient.Enqueue(prompt, (resp) =>
        {
            if (string.IsNullOrWhiteSpace(resp)) return;

            if (logRawResponse)
                Debug.Log("[DirectorThinkCycle] RAW\n" + resp);

            // Your DirectorDecisionApplier exposes TryApplyDirectorJson(...)
            bool ok = decisionApplier.TryApplyDirectorJson(resp, out string applied, out string reason);

            if (logApplyResult)
                Debug.Log($"[DirectorThinkCycle] Apply ok={ok} applied={applied} reason={reason}");

        }, debugTag: "DirectorThink");
    }
}


