using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQGeneratedNpcPlanningService : MonoBehaviour
{
    public static YQGeneratedNpcPlanningService Instance
    {
        get;
        private set;
    }

    private const string InitialGenerationOwner =
        "InitialWorldGeneration";

    private const int StartupSettlementBatchHardCap =
        1;

    private const int StartupEncampmentBatchHardCap =
        1;

    /*
     * NPC population requests describe the completed location, then
     * provide a grounded transition and a short thought buffer for the
     * next queued operation.
     */
    private const string GoddessCompletionJsonSchema =
        "{\"completion\":\"grounded Goddess thought about the CURRENT completed location\",\"nextPrelude\":\"brief transition to the next approved operation\",\"ambientLines\":[\"3-5 concise unique grounded thoughts\"]}";

    [Header("Canonical NPC Generation")]
    [Tooltip(
        "Generate canonical NPC identities through the configured local LLM " +
        "after the generated world plan has been accepted.")]
    public bool enableNpcGeneration = true;

    [Tooltip(
        "If false, initial locked startup reveals the physical tutorial first and canonical Ollama population begins immediately afterward.")]
    public bool useLlmNpcGenerationDuringInitialLock = false;

    [Range(0.1f, 1f)]
    public float temperature = 0.68f;

    [Range(900, 3200)]
    public int numPredict = 1800;

    [Range(1, 6)]
    [Tooltip(
        "Maximum attempts PER LOCATION BATCH. " +
        "Successful earlier settlement/encampment batches are retained while a later batch retries.")]
    public int maximumAttemptsPerPlan = 2;

    [Range(1, 64)]
    [Tooltip(
        "Maximum settlement batches to generate during the initial loading lock.")]
    public int maxInitialSettlementBatches = 64;

    [Range(0, 64)]
    [Tooltip(
        "Maximum hostile encampment batches to generate during the initial loading lock.")]
    public int maxInitialEncampmentBatches = 64;

    [Range(30f, 300f)]
    [Tooltip(
        "Maximum seconds to wait for one NPC batch before using deterministic fallback identities.")]
    public float maxNpcBatchRequestSeconds = 180f;

    public string LastPopulationMessage
    {
        get;
        private set;
    } =
        string.Empty;

    private bool _requestInFlight;

    public bool IsRequestInFlight =>
        _requestInFlight;

    public bool HasTerminalPopulationFailure
    {
        get;
        private set;
    }

    public bool HasCompletedCanonicalPopulation
    {
        get;
        private set;
    }

    public float CurrentGenerationProgress =>
        CurrentBatchProgress(
            _requestInFlight
                ? 0.25f
                : 0f);

    private string _activePlanKey =
        string.Empty;

    /*
     * This counter applies to the CURRENT LOCATION BATCH.
     *
     * It resets every time one settlement or encampment is accepted.
     */
    private int _attemptCount;

    /*
     * Why the CURRENT location batch was rejected on its previous attempt.
     *
     * This is prompt-only retry state. It is never canonical save data.
     */
    private string _lastBatchRejectionReason =
        string.Empty;
    /*
 * Every canonical name collision encountered while retrying the CURRENT
 * physical location.
 *
 * This is deliberately cumulative for the batch. A collision discovered
 * on attempt 1 must remain forbidden on attempts 2, 3, 4, etc.
 *
 * It is prompt-only transient state and is cleared when the location
 * succeeds or the active world plan changes.
 */
    private readonly List<string>
        _rejectedCanonicalNamesForCurrentBatch =
            new List<string>();

    private float _nextRequestTime;

    private float _nextCoordinatorTickTime;

    private float _requestStartedAt =
        -9999f;

    private const float RetryDelaySeconds =
        12f;

    private const float PostRevealNpcGenerationDelaySeconds =
        8f;

    // ============================================================
    // GODDESS LOADING DIALOGUE
    // ============================================================

    /*
     * These lines remain presentation-only fallbacks.
     *
     * They do not affect:
     *
     * - world determinism
     * - prompts
     * - save data
     * - NPC identity
     * - retry behavior
     *
     * {0} is replaced with the current settlement/encampment name.
     */

    private static readonly string[]
        GoddessSettlementGenerationMessages =
        {
            // note: These fallback lines appear only while generation is busy, so they should feel fast and hand-curated.
            "Hold. {0} has doors and no social damage yet. I am fixing it. Calmly. Obviously.",

            "{0} needs people fast. I am rationing names and pretending this is a sustainable pipeline...",

            "I left {0} full of houses and no one to complain about them. Useless output. One moment...",

            "{0} needs jobs, grudges, and names. Names are the part that keeps biting me...",

            "Do not look yet. I am hot-loading childhoods before the citizens notice the gap...",

            "I know the shape of {0}'s people. The labels are being difficult...",

            "Threading lives through {0}. If any duplicate, I will repair it before you can form an opinion...",

            "There are buildings in {0}, which means ownership arguments. Creating those with divine composure...",

            "Give me a moment. {0}'s people are still remembering things I have not indexed correctly...",

            "{0} is empty in a very accusatory way. I am typing faster than is dignified...",

            "Compressing {0}'s histories until the debts stop clipping through the walls...",

            "One moment. I am deciding who in {0} avoids whom. Social logic requires way too many edge cases..."
        };

    private static readonly string[]
        GoddessEncampmentGenerationMessages =
        {
            // note: Hostile fallback lines stay concrete and urgent without inventing extra world canon.
            "{0} needs one clean threat profile. Clean is optimistic. I am still saying it calmly...",

            "Putting a readable threat into {0}. Please do not inspect the staging layer...",

            "{0} is too safe on paper. That is suspicious and also my fault...",

            "Waking something in {0}. It gets a name, a boundary, and absolutely no apology...",

            "Ah, {0}. Danger slot is empty. Embarrassing. Filling it now...",

            "Do not go to {0} yet. I am still making the bad idea legible and contained...",

            "{0} requires one local horror. Not three. I am showing restraint...",

            "Something unpleasant belongs in {0}. The empty slot is glaring at me from the interface...",

            "I left a pocket of violence in {0}. Naming it before it wanders...",

            "There should be something in {0} that makes sensible people turn around. Installing the warning shape...",

            "Deciding whether the danger in {0} speaks. Silence is cheaper, but suspicious...",

            "Wait. {0} does not contain enough reasons to regret visiting. Correcting the oversight..."
        };

    private static readonly string[]
        GoddessSettlementRetryMessages =
        {
            // note: Retry lines acknowledge repair work without implying the failed content became canon.
            "No, no... that batch is colliding with another one. I am calm. Deleting and redoing it...",

            "Those names are taken. Unique identity constraints remain my least divine problem...",

            "I gave {0}'s people someone else's lives. Rolling that back before it becomes embarrassing...",

            "That will not do. Two people cannot both be the same person. Basic causality, please...",

            "Wait. Duplicate person detected. Do not look directly at the bad output...",

            "Names, names... why does everyone require a unique key? One moment...",

            "I crossed two identity threads. Very embarrassing. Patching the social graph...",

            "No. Those are somebody else's people. Wrong cache. I hate that I said cache...",

            "I accidentally remembered the same person twice. That seems unstable...",

            "{0} deserves its own inhabitants. Apparently copying them from elsewhere fails validation...",

            "Something went wrong in the identity ledger. I am correcting it with immense composure...",

            "No, those histories overlap. Causality is being fussy, and annoyingly correct..."
        };

    private static readonly string[]
        GoddessEncampmentRetryMessages =
        {
            "No. That name belongs to another mouth. Let me reach deeper...",

            "I have apparently named two horrors the same thing. They are both offended...",

            "That creature already exists elsewhere. I refuse to have matching abominations...",

            "No, not that one. I have used that soul already...",

            "I pulled the wrong monster out of possibility. Put it back...",

            "That name echoes somewhere else. I dislike echoes...",

            "One of my horrors has become derivative. Give me a moment...",

            "No. I recognize that monster. I already put it somewhere else...",

            "I appear to have created the same nightmare twice. Excessive, even for me...",

            "Wrong creature. Same universe. Easy mistake...",

            "That identity is occupied. I shall reach into a less crowded part of eternity...",

            "No, no, this one already has somewhere else to be terrible..."
        };

    private static readonly string[]
        GoddessSettlementAcceptedMessages =
        {
            "Yes... those are the ones. They have always lived in {0}. I think...",

            "There. {0} remembers its people now...",

            "Good. The people of {0} have histories, routines, and several unnecessary opinions...",

            "{0} is occupied. Try not to unravel anyone's backstory...",

            "Ah, yes. Those faces belong in {0}. They always did. Recently...",

            "The inhabitants of {0} now remember childhoods that occurred moments ago...",

            "{0} has citizens now. Some of them already owe each other money...",

            "There. {0} has families, strangers, grudges, and someone who knows everyone's business...",

            "Good. The people of {0} are convinced they have always existed. That is the important part...",

            "{0} remembers them now. Memory is wonderfully obedient when one is divine...",

            "There. Several entire mortal lives fitted neatly into {0}. More or less...",

            "The people of {0} have settled into their histories. Please do not tell them how new those histories are..."
        };

    private static readonly string[]
        GoddessEncampmentAcceptedMessages =
        {
            "There. I have given the thing in {0} a name. It does not seem to like you very much...",

            "{0} has its monster now. I advise against introducing yourself...",

            "Done. Something in {0} knows its own name, and unfortunately yours may be next...",

            "Yes. That is what has always lurked in {0}. Do not ask how long 'always' is...",

            "I have finished the unpleasant thing in {0}. It seems enthusiastic...",

            "{0} is properly dangerous now. Much better...",

            "Ah. There it is. The problem in {0} has become personal...",

            "There. {0} now contains something with both a name and violent intentions...",

            "Good. The thing in {0} knows who it is. That usually makes them worse...",

            "I have completed the danger in {0}. I would avoid making eye contact...",

            "There. Something in {0} has just become certain that it belongs there...",

            "{0} now has a proper nightmare. You are welcome..."
        };

    private static readonly string[]
        GoddessWorldPlanChangedMessages =
        {
            "Oh. I changed my mind about the world. Holding composure while the downstream pieces catch up...",

            "That version of reality is obsolete. Migrating inhabitants to the version that does not make me sweat...",

            "No, not that world. The other one. I am absolutely in control of this branch...",

            "I revised geography. The people are being informed retroactively, which is normal and fine...",

            "Reality shifted slightly. Do not worry; the memories are recompiling around it...",

            "I changed something upstream. Everything downstream is pretending this was always true, thank you...",

            "One moment. I replaced a piece of reality and the inhabitants have not noticed. Ideal outcome...",

            "Hm. Wrong world version. Putting everyone back where they have always been. Recently..."
        };

    private static readonly string[]
        GoddessTerminalFailureMessages =
        {
            "Hm. Something refuses to exist correctly. You are not entering my world while the build is this damp...",

            "No. Reality developed an inconsistency. Stay there while I remove it with serene divine typing...",

            "Something I made is contradicting something else I made. I am handling it. My hands are merely fast...",

            "Do not move. A piece of the world has declined to initialize properly...",

            "I found a causality knot. You are absolutely not stepping into my unresolved merge conflict...",

            "One moment. The world is arguing with me. It will lose after I breathe exactly once...",

            "That is wrong. Deeply, structurally wrong. Remain outside while I stop smiling like this...",

            "No. I refuse to let you enter while reality is doing that in public...",

            "Something escaped its proper history. Stay where you are while I pin it back down...",

            "I created a contradiction. Annoying. Do not touch anything until I make it untrue...",

            "Reality has become untidy. You may enter when the output stops embarrassing both of us...",

            "Absolutely not. Something in there failed to become what I told it to be, and I took that personally..."
        };

    private static readonly string[]
        GoddessPopulationCompleteMessages =
        {
            "There. Everyone is where they have always been. Do not ask me to create them again...",

            "Good. The living have names, the dangerous have grudges, and reality is mostly consistent...",

            "Everyone now remembers existing. Convenient, is it not?",

            "There. I have populated your little world. Please try not to kill all of it immediately...",

            "The people are placed. The monsters are placed. The lies about how long they have existed are placed...",

            "Done. Every soul has somewhere to stand and something to complain about...",

            "There. The world is inhabited. It already seems louder...",

            "Good. Everyone has a name, a place, and at least one problem. That should feel sufficiently real...",

            "There. The mortals believe they have histories and the monsters believe they have territories...",

            "Population complete. Not that they know there was ever a time before them...",

            "Everyone is accounted for. More importantly, everyone believes they have always been accounted for...",

            "There. Souls distributed, memories attached, hostilities assigned. Very tidy..."
        };

    private static string _lastGoddessMessageTemplate =
        string.Empty;

    private static string PickGoddessMessage(
        string[] messages,
        string locationName = "")
    {
        if (messages == null ||
            messages.Length == 0)
        {
            return string.Empty;
        }

        int index =
            UnityEngine.Random.Range(
                0,
                messages.Length);

        if (messages.Length > 1 &&
            string.Equals(
                messages[index],
                _lastGoddessMessageTemplate,
                StringComparison.Ordinal))
        {
            int offset =
                UnityEngine.Random.Range(
                    1,
                    messages.Length);

            index =
                (index + offset) %
                messages.Length;
        }

        string template =
            messages[index];

        _lastGoddessMessageTemplate =
            template;

        string location =
            Safe(
                locationName,
                "that place");

        try
        {
            return
                string.Format(
                    template,
                    location);
        }
        catch
        {
            return template;
        }
    }

    // ============================================================
    // LOCATION-SIZED NPC TRANSACTIONS
    // ============================================================

    private enum PopulationBatchKind
    {
        Settlement,
        Encampment
    }

    private sealed class PopulationBatchTarget
    {
        public PopulationBatchKind kind;

        public string locationId =
            string.Empty;

        public string regionId =
            string.Empty;

        public string displayName =
            string.Empty;
    }

    private readonly List<PopulationBatchTarget> _batchTargets =
        new List<PopulationBatchTarget>();

    private readonly List<GeneratedNpcPlanRecord> _pendingGeneratedNpcs =
        new List<GeneratedNpcPlanRecord>();

    private int _activeBatchIndex;

    [Serializable]
    private sealed class GeneratedNpcPopulationResponse
    {
        public string schemaVersion =
            "generated_npc_population_v1";

        // note: Json.NET overwrites this when present; empty preserves omitted-seed validation behavior.
        public string worldSeed =
            string.Empty;

        public List<GeneratedNpcPlanRecord> generatedNpcs =
            new List<GeneratedNpcPlanRecord>();

        public void EnsureCollections()
        {
            generatedNpcs ??=
                new List<GeneratedNpcPlanRecord>();

            for (int i = 0;
                 i < generatedNpcs.Count;
                 i++)
            {
                generatedNpcs[i]
                    ?.EnsureCollections();
            }
        }
    }

    private static YQGoddessGenerationVoiceDto
        ExtractOptionalGoddessVoice(
            JObject root)
    {
        if (root == null)
            return null;

        YQGoddessGenerationVoiceDto result =
            new YQGoddessGenerationVoiceDto();

        JToken voiceToken =
            root["goddessVoice"];

        if (voiceToken is JObject voiceObject)
        {
            result.completion =
                ReadOptionalString(
                    voiceObject,
                    "completion",
                    "line",
                    "text");

            result.nextPrelude =
                ReadOptionalString(
                    voiceObject,
                    "nextPrelude");

            // note: Preserve the model-authored thought buffer instead of silently discarding most of the requested Goddess response.
            result.ambientLines =
                ReadOptionalStringArray(
                    voiceObject,
                    "ambientLines");
        }
        else if (voiceToken != null &&
                 voiceToken.Type ==
                 JTokenType.String)
        {
            result.completion =
                voiceToken.Value<string>() ??
                string.Empty;
        }

        // note: Tolerate old flattened completion output without allowing it to affect canonical NPC parsing.
        if (string.IsNullOrWhiteSpace(
                result.completion))
        {
            result.completion =
                ReadOptionalString(
                    root,
                    "completion");
        }

        YQGoddessGenerationDialogue
            .Normalize(
                result);

        if (string.IsNullOrWhiteSpace(
                result.completion))
        {
            return null;
        }

        return result;
    }

    private static string[] ReadOptionalStringArray(
        JObject source,
        string name)
    {
        if (source == null ||
            string.IsNullOrWhiteSpace(name) ||
            !(source[name] is JArray values))
        {
            return Array.Empty<string>();
        }

        List<string> result =
            new List<string>(
                values.Count);

        for (int index = 0; index < values.Count; index++)
        {
            // note: Ignore malformed optional entries individually so one bad aside never rejects accepted NPC canon.
            if (values[index]?.Type != JTokenType.String)
                continue;

            string value =
                values[index].Value<string>();

            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value.Trim());
        }

        return result.ToArray();
    }

    private static string ReadOptionalString(
        JObject source,
        params string[] names)
    {
        if (source == null ||
            names == null)
        {
            return string.Empty;
        }

        for (int i = 0;
             i < names.Length;
             i++)
        {
            string name =
                names[i];

            if (string.IsNullOrWhiteSpace(
                    name))
            {
                continue;
            }

            JToken token =
                source[name];

            if (token == null ||
                token.Type !=
                JTokenType.String)
            {
                continue;
            }

            string value =
                token.Value<string>();

            if (!string.IsNullOrWhiteSpace(
                    value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!YourQuestTutorialAutoBootstrap.GameplayRuntimeReady)
        {
            // note: NPC planning begins only after the selected save has an authoritative player and a constructed gameplay world.
            return;
        }

        GameObject go =
            new GameObject(
                "00__YQ_GeneratedNpcPlanningService");

        DontDestroyOnLoad(
            go);

        go.AddComponent<
            YQGeneratedNpcPlanningService>();
    }

    private void Awake()
    {
        if (Instance != null &&
            Instance != this)
        {
            Destroy(
                gameObject);

            return;
        }

        Instance =
            this;

        DontDestroyOnLoad(
            gameObject);

        // note: Production startup reveals the physical tutorial first; canonical Ollama inhabitants begin moments later without blocking control.
        useLlmNpcGenerationDuringInitialLock =
            false;
    }

    private void Update()
    {
        if (!YourQuestTutorialAutoBootstrap.GameplayRuntimeReady)
            return;

        float coordinatorInterval =
            YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked ||
            _requestInFlight
                ? 0.10f
                : 0.50f;

        if (Time.unscaledTime <
            _nextCoordinatorTickTime)
        {
            return;
        }

        // note: NPC coordination touches the complete persisted plan, so polling is paced while network generation remains fully asynchronous.
        _nextCoordinatorTickTime =
            Time.unscaledTime +
            coordinatorInterval;

        PlayerState startupPlayer =
            PlayerStateManager.Instance != null
                ? PlayerStateManager.Instance.state
                : null;

        if (startupPlayer == null ||
            startupPlayer.generatedOrigin == null ||
            string.IsNullOrWhiteSpace(
                startupPlayer.generatedOrigin.source) ||
            string.IsNullOrWhiteSpace(
                startupPlayer.generatedOrigin.seed) ||
            string.IsNullOrWhiteSpace(
                startupPlayer.generatedOrigin.directionKey))
        {
            return;
        }

        YQWorldGenerationService worldGeneration =
            YQWorldGenerationService.Instance;

        if (worldGeneration != null &&
            worldGeneration.IsRequestInFlight)
        {
            return;
        }

        bool initialGenerationLocked =
            YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked;

        if (!initialGenerationLocked)
        {
            float secondsSinceReveal =
                Time.unscaledTime -
                YQGeneratedWorldRuntimeBuilder
                    .LastInitialGenerationGameplayUnlockTime;

            if (secondsSinceReveal <
                PostRevealNpcGenerationDelaySeconds)
            {
                // note: Background NPC expansion waits until first-playable frames have settled.
                return;
            }
        }

        if (_requestInFlight)
        {
            if (Time.unscaledTime -
                _requestStartedAt >
                Mathf.Max(
                    30f,
                    maxNpcBatchRequestSeconds))
            {
                // note: A stalled local model must not trap the loading lock forever.
                _requestInFlight =
                    false;

                AcceptDeterministicFallbackForCurrentBatch(
                    "Ollama NPC batch timed out while the world was waiting.");

                return;
            }

            // note: Preserve the last accepted Ollama-authored line while this request runs; periodic empty/canned updates caused needless UI churn.
            return;
        }

        if (!enableNpcGeneration ||
            Time.unscaledTime <
                _nextRequestTime)
        {
            return;
        }

        PlayerStateManager playerManager =
            PlayerStateManager.Instance;

        WorldStateManager worldManager =
            WorldStateManager.Instance;

        if (playerManager == null ||
            playerManager.state == null ||
            worldManager == null ||
            worldManager.State == null)
        {
            return;
        }

        if (!GeneratedRpgContentService
                .HasCompletedOrigin(
                    playerManager.state))
        {
            return;
        }

        WorldState world =
            worldManager.State;

        world.EnsureCollections();

        GeneratedWorldPlanRecord plan =
            world.generatedWorldPlan;

        if (!IsReadyWorldPlan(
                plan))
        {
            return;
        }

        string planKey =
            BuildPlanKey(
                plan);

        if (!string.Equals(
                _activePlanKey,
                planKey,
                StringComparison.Ordinal))
        {
            ResetGenerationForPlan(
                plan,
                planKey);
        }

        if (plan.generatedNpcs != null &&
            plan.generatedNpcs.Count > 0)
        {
            if (ValidateCoverage(
                    plan,
                    plan.generatedNpcs,
                    _batchTargets,
                    out string existingCoverageError))
            {
                HasCompletedCanonicalPopulation =
                    true;

                HasTerminalPopulationFailure =
                    false;

                EnsureRuntimeNpcRecords(
                    plan,
                    world);

                return;
            }

            // note: Older saves may contain only the previous 1-settlement/0-encampment slice; regenerate full coverage.
            Debug.LogWarning(
                "[YQGeneratedNpcPlanningService] Existing canonical NPC list is incomplete; rebuilding. " +
                existingCoverageError);

            plan.generatedNpcs.Clear();
            _pendingGeneratedNpcs.Clear();
            _activeBatchIndex =
                0;
            _attemptCount =
                0;
            _lastBatchRejectionReason =
                string.Empty;
            _rejectedCanonicalNamesForCurrentBatch.Clear();
        }

        if (HasTerminalPopulationFailure)
            return;

        if (_activeBatchIndex >=
            _batchTargets.Count)
        {
            CompletePopulationGeneration(
                worldManager,
                world,
                plan);

            return;
        }

        if (_attemptCount >=
            maximumAttemptsPerPlan)
        {
            AcceptDeterministicFallbackForCurrentBatch(
                "Canonical NPC generation exhausted retries.");

            return;
        }

        if (LLMClient.Instance == null)
        {
            // note: No local model means the locked generation must finish through validated deterministic identities.
            AcceptDeterministicFallbackForCurrentBatch(
                "Local LLM unavailable for NPC batch.");

            return;
        }

        PopulationBatchTarget batch =
            CurrentBatchTarget();

        if (batch == null ||
            string.IsNullOrWhiteSpace(
                batch.locationId))
        {
            _activeBatchIndex++;

            _attemptCount =
                0;

            _lastBatchRejectionReason =
                string.Empty;
            _rejectedCanonicalNamesForCurrentBatch.Clear();

            return;
        }

        RequestPopulationBatch(
            playerManager.state,
            world,
            plan,
            planKey,
            batch);
    }

    [ContextMenu("Generate Missing Canonical NPCs")]
    public void GenerateMissingCanonicalNpcs()
    {
        _attemptCount =
            0;

        _lastBatchRejectionReason =
            string.Empty;
        _rejectedCanonicalNamesForCurrentBatch.Clear();
        _nextRequestTime =
            0f;

        HasTerminalPopulationFailure =
            false;

        HasCompletedCanonicalPopulation =
            false;

        Update();
    }

    // ============================================================
    // PLAN / LOCATION-BATCH STATE
    // ============================================================

    private void ResetGenerationForPlan(
        GeneratedWorldPlanRecord plan,
        string planKey)
    {
        _activePlanKey =
            planKey ?? string.Empty;

        _attemptCount =
            0;

        _lastBatchRejectionReason =
            string.Empty;
        _rejectedCanonicalNamesForCurrentBatch.Clear();

        _nextRequestTime =
            0f;

        _requestStartedAt =
            -9999f;

        _activeBatchIndex =
            0;

        _batchTargets.Clear();

        _pendingGeneratedNpcs.Clear();

        HasTerminalPopulationFailure =
            false;

        HasCompletedCanonicalPopulation =
            false;

        int availableBatchCount =
            CountAvailableBatchTargets(
                plan);

        BuildBatchTargets(
            plan,
            _batchTargets,
            Mathf.Min(
                maxInitialSettlementBatches,
                StartupSettlementBatchHardCap),
            Mathf.Min(
                maxInitialEncampmentBatches,
                StartupEncampmentBatchHardCap));

        int settlementBatches =
            0;

        int encampmentBatches =
            0;

        for (int i = 0;
             i < _batchTargets.Count;
             i++)
        {
            PopulationBatchTarget target =
                _batchTargets[i];

            if (target == null)
                continue;

            if (target.kind ==
                PopulationBatchKind.Settlement)
            {
                settlementBatches++;
            }
            else
            {
                encampmentBatches++;
            }
        }

        LastPopulationMessage =
            "Prepared " +
            _batchTargets.Count +
            " initial canonical NPC location batch(es) from " +
            availableBatchCount +
            " available for world " +
            Safe(
                plan != null
                    ? plan.worldSeed
                    : string.Empty,
                "<none>") +
            ": " +
            settlementBatches +
            " settlement batch(es), " +
            encampmentBatches +
            " encampment batch(es).";

        Debug.Log(
            "[YQGeneratedNpcPlanningService] " +
            LastPopulationMessage);
    }

    private static void BuildBatchTargets(
        GeneratedWorldPlanRecord plan,
        List<PopulationBatchTarget> result,
        int maxSettlementBatches,
        int maxEncampmentBatches)
    {
        if (result == null)
            return;

        result.Clear();

        if (plan == null)
            return;

        plan.EnsureCollections();

        int settlementBatches =
            0;

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement == null ||
                string.IsNullOrWhiteSpace(
                    settlement.settlementId))
            {
                continue;
            }

            if (settlementBatches >=
                Mathf.Max(
                    1,
                    maxSettlementBatches))
            {
                continue;
            }

            settlementBatches++;

            // note: Initial loading only needs a representative canonical slice, not every distant resident.
            result.Add(
                new PopulationBatchTarget
                {
                    kind =
                        PopulationBatchKind.Settlement,

                    locationId =
                        settlement.settlementId,

                    regionId =
                        settlement.regionId,

                    displayName =
                        Safe(
                            settlement.displayName,
                            settlement.settlementId)
                });
        }

        int encampmentBatches =
            0;

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment == null ||
                string.IsNullOrWhiteSpace(
                    encampment.encampmentId))
            {
                continue;
            }

            if (encampmentBatches >=
                Mathf.Max(
                    0,
                    maxEncampmentBatches))
            {
                continue;
            }

            encampmentBatches++;

            // note: Hostiles beyond this cap can remain procedural until a later background expansion pass.
            result.Add(
                new PopulationBatchTarget
                {
                    kind =
                        PopulationBatchKind.Encampment,

                    locationId =
                        encampment.encampmentId,

                    regionId =
                        encampment.regionId,

                    displayName =
                        Safe(
                            encampment.displayName,
                            encampment.encampmentId)
                });
        }
    }

    private static int CountAvailableBatchTargets(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return 0;

        plan.EnsureCollections();

        int count =
            0;

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement != null &&
                !string.IsNullOrWhiteSpace(
                    settlement.settlementId))
            {
                count++;
            }
        }

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment != null &&
                !string.IsNullOrWhiteSpace(
                    encampment.encampmentId))
            {
                count++;
            }
        }

        return count;
    }

    private PopulationBatchTarget CurrentBatchTarget()
    {
        if (_activeBatchIndex < 0 ||
            _activeBatchIndex >=
            _batchTargets.Count)
        {
            return null;
        }

        return
            _batchTargets[
                _activeBatchIndex];
    }

    private float CurrentBatchProgress(
        float intraBatchOffset = 0f)
    {
        if (_batchTargets.Count <= 0)
            return 0.90f;

        float completed =
            Mathf.Clamp(
                _activeBatchIndex +
                intraBatchOffset,
                0f,
                _batchTargets.Count);

        float normalized =
            completed /
            _batchTargets.Count;

        return
            Mathf.Lerp(
                0.78f,
                0.91f,
                normalized);
    }

    private static string BatchKindName(
        PopulationBatchTarget target)
    {
        if (target == null)
            return "location";

        return
            target.kind ==
            PopulationBatchKind.Settlement
                ? "settlement"
                : "encampment";
    }

    // ============================================================
    // LOCATION REQUEST
    // ============================================================

    private void RequestPopulationBatch(
        PlayerState player,
        WorldState world,
        GeneratedWorldPlanRecord plan,
        string planKey,
        PopulationBatchTarget target)
    {
        if (player == null ||
            world == null ||
            plan == null ||
            target == null ||
            string.IsNullOrWhiteSpace(
                target.locationId))
        {
            return;
        }

        string worldSeed =
            plan.worldSeed;

        string locationName =
            Safe(
                target.displayName,
                target.locationId);

        int expectedNpcCount =
            GetExpectedNpcCountForTarget(
                plan,
                target);

        if (expectedNpcCount <= 0)
        {
            _activeBatchIndex++;

            _attemptCount =
                0;

            _lastBatchRejectionReason =
                string.Empty;

            _rejectedCanonicalNamesForCurrentBatch.Clear();

            return;
        }

        if (YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked &&
            !useLlmNpcGenerationDuringInitialLock)
        {
            // note: Preserve the empty canonical slot until post-reveal Ollama generation; deterministic identities must not silently become the primary release path.
            _nextRequestTime =
                Time.unscaledTime +
                0.5f;
            return;
        }

        if (LLMClient.Instance == null)
        {
            // note: Missing local model should not stall canonical population; fallback records are valid generated game state.
            AcceptDeterministicFallbackForCurrentBatch(
                "Local LLM unavailable for NPC batch.");

            return;
        }

        string prompt =
            BuildPopulationPrompt(
                plan,
                target,
                _activeBatchIndex + 1 < _batchTargets.Count
                    ? _batchTargets[_activeBatchIndex + 1]
                    : null,
                _activeBatchIndex + 1,
                _batchTargets.Count);

        prompt +=
    BuildExistingCanonicalNameConstraint(
        _pendingGeneratedNpcs,
        _rejectedCanonicalNamesForCurrentBatch);

        if (_attemptCount > 0 &&
            !string.IsNullOrWhiteSpace(
                _lastBatchRejectionReason))
        {
            prompt +=
                BuildRetryCorrectionConstraint(
                    _lastBatchRejectionReason,
        _rejectedCanonicalNamesForCurrentBatch);
        }

        /*
         * IMPORTANT:
         *
         * This remains the final prompt block.
         *
         * The local 4B model was repeatedly returning N-1 objects despite
         * correctly closed JSON. Putting the count verification last gives
         * the structural requirement maximum recency.
         */
        prompt +=
            BuildFinalCountConstraint(
                expectedNpcCount);

        int configuredMaximum =
            Mathf.Clamp(
                numPredict,
                1000,
                3200);

        int calculatedBudget =
            620 +
            expectedNpcCount *
            280;

        int requestBudget =
            Mathf.Clamp(
                calculatedBudget,
                1000,
                configuredMaximum);

        /*
         * Structure matters more than creativity here.
         *
         * Keep the Inspector value as the user's upper preference but cap
         * actual population transactions. Retries become even more
         * conservative because they are correcting a known structural error.
         */
        float configuredTemperature =
            Mathf.Clamp01(
                temperature);

        /*
 * Structural retries benefit from lower variance.
 *
 * Name-collision retries are different: lowering temperature makes the
 * small model collapse harder onto its highest-probability fantasy-name
 * vocabulary (Kaelen, Dorin, Soren, Mire, Vale, etc.).
 *
 * Therefore collision retries retain substantially more naming entropy.
 */
        bool nameCollisionRetry =
            _attemptCount > 0 &&
            IsNameCollisionFailure(
                _lastBatchRejectionReason);

        float requestTemperature;

        if (_attemptCount <= 0)
        {
            requestTemperature =
                Mathf.Min(
                    configuredTemperature,
                    0.55f);
        }
        else if (nameCollisionRetry)
        {
            requestTemperature =
                Mathf.Min(
                    configuredTemperature,
                    0.72f);
        }
        else
        {
            requestTemperature =
                Mathf.Min(
                    configuredTemperature,
                    0.42f);
        }

        Dictionary<string, object> options =
            new Dictionary<string, object>
            {
                {
                    "num_predict",
                    requestBudget
                },
                {
                    "temperature",
                    requestTemperature
                },
                {
                    "top_p",
                    0.92f
                },
                {
                    // note: Unity's web timeout should fire before the planner's stall guard, avoiding stale late callbacks.
                    "request_timeout_seconds",
                    95
                }
            };

        _requestInFlight =
            true;

        _attemptCount++;

        HasTerminalPopulationFailure =
            false;

        HasCompletedCanonicalPopulation =
            false;

        string requestedLocationId =
            target.locationId;

        PopulationBatchKind requestedKind =
            target.kind;

        string requestedRegionId =
            target.regionId;

        LastPopulationMessage =
            "Queued canonical Ollama NPC " +
            BatchKindName(
                target) +
            " batch " +
            (_activeBatchIndex + 1) +
            "/" +
            _batchTargets.Count +
            " for " +
            locationName +
            " in world " +
            worldSeed +
            " (attempt " +
            _attemptCount +
            ", expected NPCs " +
            expectedNpcCount +
            ", num_predict " +
            requestBudget +
            ", temperature " +
            requestTemperature.ToString("0.00") +
            ").";

        Debug.Log(
            "[YQGeneratedNpcPlanningService] " +
            LastPopulationMessage);

        string generationMessage =
            YQGoddessGenerationDialogue
                .TakeNpcPrelude(
                    string.Empty);

        YQStartupLoadingScreen.SetGenerationStage(
            generationMessage,
            CurrentBatchProgress());

        _requestStartedAt =
            Time.unscaledTime;

        // note: The callback is shared by initial locked generation and later background population repairs.
        Action<string> handleResponse =
            raw =>
            {
                _requestInFlight =
                    false;

                WorldStateManager activeWorldManager =
                    WorldStateManager.Instance;

                PlayerStateManager activePlayerManager =
                    PlayerStateManager.Instance;

                WorldState activeWorld =
                    activeWorldManager != null
                        ? activeWorldManager.State
                        : null;

                PlayerState activePlayer =
                    activePlayerManager != null
                        ? activePlayerManager.state
                        : null;

                if (activeWorld == null ||
                    activePlayer == null)
                {
                    FailAndDelay(
                        "NPC location batch result arrived without an active save.");

                    return;
                }

                activeWorld.EnsureCollections();

                GeneratedWorldPlanRecord activePlan =
                    activeWorld.generatedWorldPlan;

                if (!IsReadyWorldPlan(
                        activePlan))
                {
                    FailAndDelay(
                        "NPC location batch result arrived after the active world plan became unavailable.");

                    return;
                }

                string activePlanKey =
                    BuildPlanKey(
                        activePlan);

                if (!string.Equals(
                        activePlanKey,
                        planKey,
                        StringComparison.Ordinal))
                {
                    LastPopulationMessage =
                        "Discarded stale NPC location batch because the active world plan changed.";

                    Debug.LogWarning(
                        "[YQGeneratedNpcPlanningService] " +
                        LastPopulationMessage);

                    ResetGenerationForPlan(
                        activePlan,
                        activePlanKey);

                    _nextRequestTime =
                        Time.unscaledTime +
                        1f;

                    YQStartupLoadingScreen.SetGenerationStage(
                        string.Empty,
                        0.78f);

                    return;
                }

                PopulationBatchTarget current =
                    CurrentBatchTarget();

                if (current == null ||
                    current.kind !=
                    requestedKind ||
                    !string.Equals(
                        current.locationId,
                        requestedLocationId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(
                        current.regionId,
                        requestedRegionId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    LastPopulationMessage =
                        "Discarded stale NPC batch for location " +
                        requestedLocationId +
                        " because the population sequence has already advanced.";

                    Debug.LogWarning(
                        "[YQGeneratedNpcPlanningService] " +
                        LastPopulationMessage);

                    return;
                }

                if (!TryParsePopulationBatch(
                        raw,
                        activePlan,
                        current,
                        _pendingGeneratedNpcs,
                        out List<GeneratedNpcPlanRecord> generated,
                        out YQGoddessGenerationVoiceDto goddessVoice,
                        out string error))
                {
                    if (YQGeneratedWorldRuntimeBuilder
                            .IsInitialGenerationGameplayLocked &&
                        IsStartupBlockingLlmFailure(
                            error))
                    {
                        // note: Startup must keep advancing; a cut-off or timed-out local-model response is not allowed to stall the loading lock.
                        AcceptDeterministicFallbackForCurrentBatch(
                            "Ollama NPC " +
                            BatchKindName(
                                current) +
                            " batch '" +
                            locationName +
                            "' did not return usable startup JSON.");

                        return;
                    }

                    AccumulateRejectedCanonicalNames(
    error,
    _rejectedCanonicalNamesForCurrentBatch);

                    _lastBatchRejectionReason =
                        TrimTo(
                            error,
                            480);

                    FailAndDelay(
                                            "Ollama NPC " +
                        BatchKindName(
                            current) +
                        " batch '" +
                        locationName +
                        "' rejected: " +
                        error);

                    Debug.LogWarning(
                        "[YQGeneratedNpcPlanningService] " +
                        LastPopulationMessage +
                        "\nRAW:\n" +
                        TruncateForLog(
                            raw ??
                            "<null>"));

                    return;
                }

                for (int i = 0;
                     i < generated.Count;
                     i++)
                {
                    GeneratedNpcPlanRecord npc =
                        generated[i];

                    if (npc != null)
                    {
                        _pendingGeneratedNpcs.Add(
                            npc);
                    }
                }

                _activeBatchIndex++;

                _attemptCount =
                    0;

                _lastBatchRejectionReason =
                    string.Empty;
                _rejectedCanonicalNamesForCurrentBatch.Clear();
                LastPopulationMessage =
                    "Accepted canonical NPC " +
                    BatchKindName(
                        current) +
                    " batch " +
                    _activeBatchIndex +
                    "/" +
                    _batchTargets.Count +
                    " for " +
                    locationName +
                    ": " +
                    generated.Count +
                    " identities. Pending world total: " +
                    _pendingGeneratedNpcs.Count +
                    ".";

                Debug.Log(
                    "[YQGeneratedNpcPlanningService] " +
                    LastPopulationMessage);

                bool finalBatch =
                    _activeBatchIndex >=
                    _batchTargets.Count;

                /*
                 * The model owns ONLY the reaction to the completed current
                 * location.
                 *
                 * C# owns the next-location statement from this point forward.
                 */
                YQGoddessGenerationVoiceDto presentationVoice =
                    new YQGoddessGenerationVoiceDto
                    {
                        completion =
                            goddessVoice != null
                                ? goddessVoice.completion
                                : string.Empty,

                        // note: The next prelude is authored in the completed model response from the exact next-stage facts supplied in its prompt.
                        nextPrelude =
                            goddessVoice != null
                                ? goddessVoice.nextPrelude
                                : string.Empty,

                        // note: Preserve model-authored interstitials so the next NPC batch has unique loading thoughts.
                        ambientLines =
                            goddessVoice != null
                                ? goddessVoice.ambientLines
                                : Array.Empty<string>()
                    };

                YQGoddessGenerationDialogue
                    .Normalize(
                        presentationVoice);

                if (finalBatch)
                {
                    /*
                     * CurrentBatchTarget() is null after the final accepted
                     * transaction, so BuildGroundedNextPrelude() returns the
                     * grounded transition into canonical materialization.
                     */
                    YQGoddessGenerationDialogue
                        .SetNpcVoice(
                            presentationVoice,
                            true);

                    CompletePopulationGeneration(
                        activeWorldManager,
                        activeWorld,
                        activePlan);

                    return;
                }

                string acceptedMessage =
                    YQGoddessGenerationDialogue
                        .Completion(
                            presentationVoice,
                            string.Empty);

                /*
                 * Store ONLY our deterministic next-location prelude.
                 *
                 * SetNpcVoice(false) stores nextPrelude and does not carry
                 * completion forward.
                 */
                YQGoddessGenerationDialogue
                    .SetNpcVoice(
                        presentationVoice,
                        false);

                _nextRequestTime =
                    Time.unscaledTime +
                    1.25f;

                YQStartupLoadingScreen.SetGenerationStage(
                    acceptedMessage,
                    CurrentBatchProgress());
            };

        string debugTag =
            "GeneratedNpcPopulation:" +
            requestedLocationId;

        if (YQGeneratedWorldRuntimeBuilder
                .IsInitialGenerationGameplayLocked)
        {
            // note: Initial NPC generation stays inside the exclusive world-generation queue so Ollama calls never overlap.
            LLMClient.Instance.EnqueueExclusive(
                prompt,
                handleResponse,
                debugTag,
                options,
                InitialGenerationOwner,
                disableTimeout: false);
        }
        else
        {
            // note: Outside startup, NPC repair/expansion is ordinary queued work.
            LLMClient.Instance.Enqueue(
                prompt,
                handleResponse,
                debugTag,
                options);
        }
    }
    private static bool IsNameCollisionFailure(
    string reason)
    {
        if (string.IsNullOrWhiteSpace(
                reason))
        {
            return false;
        }

        return
            reason.IndexOf(
                "duplicate canonical NPC name",
                StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static List<string> ExtractSingleQuotedValues(
        string text)
    {
        List<string> result =
            new List<string>();

        if (string.IsNullOrWhiteSpace(
                text))
        {
            return result;
        }

        int searchIndex =
            0;

        while (searchIndex <
               text.Length)
        {
            int firstQuote =
                text.IndexOf(
                    '\'',
                    searchIndex);

            if (firstQuote < 0)
                break;

            int secondQuote =
                text.IndexOf(
                    '\'',
                    firstQuote + 1);

            if (secondQuote < 0)
                break;

            string value =
                text.Substring(
                        firstQuote + 1,
                        secondQuote -
                        firstQuote -
                        1)
                    .Trim();

            if (!string.IsNullOrWhiteSpace(
                    value))
            {
                AddUnique(
                    result,
                    value);
            }

            searchIndex =
                secondQuote + 1;
        }

        return result;
    }

    private static void AccumulateRejectedCanonicalNames(
        string rejectionReason,
        List<string> destination)
    {
        if (destination == null ||
            !IsNameCollisionFailure(
                rejectionReason))
        {
            return;
        }

        List<string> names =
            ExtractSingleQuotedValues(
                rejectionReason);

        for (int i = 0;
             i < names.Count;
             i++)
        {
            AddUnique(
                destination,
                names[i]);
        }
    }

    private void AcceptDeterministicFallbackForCurrentBatch(
        string reason)
    {
        WorldStateManager worldManager =
            WorldStateManager.Instance;

        WorldState world =
            worldManager != null
                ? worldManager.State
                : null;

        GeneratedWorldPlanRecord plan =
            world != null
                ? world.generatedWorldPlan
                : null;

        PopulationBatchTarget target =
            CurrentBatchTarget();

        if (worldManager == null ||
            world == null ||
            plan == null ||
            target == null)
        {
            MarkTerminalFailure(
                reason +
                " Deterministic fallback could not find the active batch.");

            return;
        }

        List<GeneratedNpcPlanRecord> fallback =
            BuildDeterministicFallbackBatch(
                plan,
                target,
                _pendingGeneratedNpcs);

        if (fallback.Count == 0)
        {
            MarkTerminalFailure(
                reason +
                " Deterministic fallback produced no identities.");

            return;
        }

        for (int i = 0;
             i < fallback.Count;
             i++)
        {
            _pendingGeneratedNpcs.Add(
                fallback[i]);
        }

        _activeBatchIndex++;

        _attemptCount =
            0;

        _lastBatchRejectionReason =
            string.Empty;
        _rejectedCanonicalNamesForCurrentBatch.Clear();

        LastPopulationMessage =
            reason +
            " Used deterministic fallback for " +
            BatchKindName(
                target) +
            " '" +
            target.displayName +
            "' (" +
            fallback.Count +
            " identities).";

        Debug.LogWarning(
            "[YQGeneratedNpcPlanningService] " +
            LastPopulationMessage);

        if (_activeBatchIndex >=
            _batchTargets.Count)
        {
            CompletePopulationGeneration(
                worldManager,
                world,
                plan);

            return;
        }

        _nextRequestTime =
            Time.unscaledTime +
            0.25f;

        // note: Fallback identities are committed immediately so the locked loading flow can keep moving.
        YQStartupLoadingScreen.SetGenerationStage(
            YQGoddessGenerationDialogue
                .Fallback(
                    "I patched " +
                    target.displayName +
                    " with stable names. Not elegant. Functional. Moving."),
            CurrentBatchProgress());
    }

    private static List<GeneratedNpcPlanRecord> BuildDeterministicFallbackBatch(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target,
        List<GeneratedNpcPlanRecord> accepted)
    {
        List<GeneratedNpcPlanRecord> result =
            new List<GeneratedNpcPlanRecord>();

        if (plan == null ||
            target == null)
        {
            return result;
        }

        HashSet<string> usedNames =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> usedIds =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (accepted != null)
        {
            for (int i = 0;
                 i < accepted.Count;
                 i++)
            {
                GeneratedNpcPlanRecord npc =
                    accepted[i];

                if (npc == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(
                        npc.displayName))
                {
                    usedNames.Add(
                        npc.displayName.Trim());
                }

                if (!string.IsNullOrWhiteSpace(
                        npc.npcId))
                {
                    usedIds.Add(
                        npc.npcId.Trim());
                }
            }
        }

        if (target.kind ==
            PopulationBatchKind.Encampment)
        {
            GeneratedEncampmentRecord encampment =
                FindEncampment(
                    plan,
                    target.locationId);

            if (encampment == null)
                return result;

            string displayName =
                BuildFallbackNpcName(
                    encampment.displayName,
                    "Captain",
                    0,
                    usedNames);

            GeneratedNpcPlanRecord hostile =
                new GeneratedNpcPlanRecord
                {
                    regionId =
                        encampment.regionId,
                    encampmentId =
                        encampment.encampmentId,
                    settlementId =
                        string.Empty,
                    factionId =
                        Safe(
                            encampment.inhabitantFactionId,
                            encampment.regionId +
                            "_hostiles"),
                    displayName =
                        displayName,
                    role =
                        "hostile_leader",
                    archetype =
                        "hostile_leader",
                    ageBand =
                        "adult",
                    presentation =
                        "Fallback hostile commander stabilized after local model failure.",
                    appearanceSummary =
                        "A visibly armed local threat leader tied to the encampment.",
                    personality =
                        "Direct, territorial, and alert.",
                    speakingStyle =
                        "short threats",
                    dailyRoutine =
                        "Patrols the encampment and watches the approach.",
                    localKnowledge =
                        "Knows the immediate encampment defenses.",
                    privateConcern =
                        "Worries the site will not hold if pressed.",
                    hostile =
                        true,
                    boss =
                        true,
                    notable =
                        true
                };

            FinalizeFallbackNpc(
                hostile,
                plan.worldSeed,
                encampment.encampmentId,
                0,
                usedIds);

            result.Add(
                hostile);

            return result;
        }

        GeneratedSettlementRecord settlement =
            FindSettlement(
                plan,
                target.locationId);

        if (settlement == null)
            return result;

        settlement.EnsureCollections();

        int count =
            ResolveDesiredResidentCount(
                plan,
                settlement);

        for (int i = 0;
             i < count;
             i++)
        {
            string role =
                ResolveFallbackResidentRole(
                    settlement,
                    i);

            if (IsReferenceSettlement(
                    plan,
                    settlement))
            {
                // note: Reference cells retain a literal service, guard, and quest-facing role even when their local LLM batch falls back.
                role = ResolveReferenceCellFallbackRole(
                    settlement,
                    i,
                    role);
            }

            string displayName =
                BuildFallbackNpcName(
                    settlement.displayName,
                    role,
                    i,
                    usedNames);

            GeneratedNpcPlanRecord resident =
                new GeneratedNpcPlanRecord
                {
                    regionId =
                        settlement.regionId,
                    settlementId =
                        settlement.settlementId,
                    encampmentId =
                        string.Empty,
                    factionId =
                        settlement.factionIds != null &&
                        settlement.factionIds.Count > 0
                            ? settlement.factionIds[0]
                            : settlement.regionId +
                              "_civic",
                    displayName =
                        displayName,
                    role =
                        NormalizeKey(
                            role),
                    ageBand =
                        "adult",
                    presentation =
                        "Fallback resident stabilized after local model failure.",
                    appearanceSummary =
                        "A practical local resident with work-ready clothes and readable posture.",
                    personality =
                        "Grounded, cautious, and useful.",
                    speakingStyle =
                        "plainspoken",
                    dailyRoutine =
                        "Works locally, trades news, and keeps to the settlement rhythm.",
                    localKnowledge =
                        "Knows the settlement, nearby work, and the safest road out.",
                    privateConcern =
                        "Wants the place to stay coherent long enough to survive.",
                    merchant =
                        IsFallbackMerchantRole(
                            role),
                    guard =
                        IsFallbackGuardRole(
                            role),
                    notable =
                        i == 0 ||
                        IsReferenceSettlement(
                            plan,
                            settlement) &&
                        i == 2
                };

            resident.archetype =
                NormalizeArchetype(
                    resident.merchant
                        ? "service"
                        : resident.guard
                            ? "guard"
                            : resident.notable
                                ? "notable"
                                : "resident",
                    resident);

            FinalizeFallbackNpc(
                resident,
                plan.worldSeed,
                settlement.settlementId,
                i,
                usedIds);

            result.Add(
                resident);
        }

        return result;
    }

    private static string ResolveFallbackResidentRole(
        GeneratedSettlementRecord settlement,
        int index)
    {
        if (settlement != null &&
            settlement.serviceSlots != null &&
            index >= 0 &&
            index < settlement.serviceSlots.Count &&
            !string.IsNullOrWhiteSpace(
                settlement.serviceSlots[index]))
        {
            // note: Use authored settlement service slots first so fallback residents still fit the location.
            return settlement.serviceSlots[index];
        }

        string[] roles =
        {
            "reeve",
            "guard",
            "merchant",
            "healer",
            "blacksmith",
            "scout",
            "farmer"
        };

        return roles[
            Mathf.Abs(
                index) %
            roles.Length];
    }

    // note: These are semantic job contracts, not authored identities; the LLM still owns every accepted NPC name, persona, and local knowledge record.
    private static string ResolveReferenceCellFallbackRole(
        GeneratedSettlementRecord settlement,
        int index,
        string fallback)
    {
        if (index == 1)
            return "guard";
        if (index == 2)
            return "quest_giver";
        if (index == 3)
            return "merchant";
        return fallback;
    }

    private static string BuildFallbackNpcName(
        string locationName,
        string role,
        int index,
        HashSet<string> usedNames)
    {
        string cleanLocation =
            Safe(
                locationName,
                "Local")
            .Replace(
                "_",
                " ")
            .Trim();

        string cleanRole =
            Safe(
                role,
                "resident")
            .Replace(
                "_",
                " ")
            .Trim();

        string baseName =
            TrimTo(
                cleanLocation +
                " " +
                ToTitleCase(
                    cleanRole),
                64);

        string name =
            baseName;

        int suffix =
            index + 1;

        while (usedNames != null &&
               !usedNames.Add(
                   name))
        {
            name =
                TrimTo(
                    baseName +
                    " " +
                    suffix,
                    72);

            suffix++;
        }

        return name;
    }

    private static void FinalizeFallbackNpc(
        GeneratedNpcPlanRecord npc,
        string worldSeed,
        string locationId,
        int index,
        HashSet<string> usedIds)
    {
        if (npc == null)
            return;

        npc.EnsureCollections();

        string baseId =
            "npc_" +
            StableHash32(
                Safe(
                    worldSeed,
                    "world") +
                "|" +
                Safe(
                    locationId,
                    "location") +
                "|" +
                Safe(
                    npc.displayName,
                    "npc") +
                "|" +
                index)
                .ToString("x8");

        string id =
            baseId;

        int collision =
            1;

        while (usedIds != null &&
               !usedIds.Add(
                   id))
        {
            id =
                baseId +
                "_" +
                collision;

            collision++;
        }

        npc.npcId =
            id;

        npc.role =
            NormalizeKey(
                npc.role);

        npc.tags =
            NormalizeTags(
                npc.tags,
                npc);

        AddUnique(
            npc.tags,
            "deterministic_fallback");
    }

    private static string ToTitleCase(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        string[] parts =
            value.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        for (int i = 0;
             i < parts.Length;
             i++)
        {
            string part =
                parts[i];

            if (part.Length == 0)
                continue;

            // note: Fallback names should read like names without pulling in culture-dependent formatting.
            parts[i] =
                char.ToUpperInvariant(
                    part[0]) +
                (part.Length > 1
                    ? part.Substring(
                        1)
                    : string.Empty);
        }

        return string.Join(
            " ",
            parts);
    }

    private static bool IsFallbackMerchantRole(
        string role)
    {
        string key =
            NormalizeKey(
                role);

        return
            key.Contains(
                "merchant") ||
            key.Contains(
                "trader") ||
            key.Contains(
                "shop") ||
            key.Contains(
                "inn") ||
            key.Contains(
                "smith") ||
            key.Contains(
                "healer");
    }

    private static bool IsFallbackGuardRole(
        string role)
    {
        string key =
            NormalizeKey(
                role);

        return
            key.Contains(
                "guard") ||
            key.Contains(
                "watch") ||
            key.Contains(
                "warden");
    }

    private void FailAndDelay(
        string message)
    {
        LastPopulationMessage =
            message;

        Debug.LogWarning(
            "[YQGeneratedNpcPlanningService] " +
            message);

        if (_attemptCount <
            maximumAttemptsPerPlan)
        {
            PopulationBatchTarget target =
                CurrentBatchTarget();

            float progress =
                Mathf.Max(
                    0.82f,
                    CurrentBatchProgress(
                        0.20f));

            // note: Retry progress updates no longer inject static Goddess prose.
            YQStartupLoadingScreen.SetGenerationStage(
                string.Empty,
                progress);

            _nextRequestTime =
                Time.unscaledTime +
                RetryDelaySeconds;

            return;
        }

        // note: After the final retry, deterministic fallback keeps the prototype playable and fully populated.
        AcceptDeterministicFallbackForCurrentBatch(
            message);
    }

    private static bool IsStartupBlockingLlmFailure(
        string error)
    {
        if (string.IsNullOrWhiteSpace(
                error))
        {
            return false;
        }

        string normalized =
            error.ToLowerInvariant();

        // note: These failures mean the model stopped mid-object or timed out, so retrying the same startup batch wastes the lock window.
        return normalized.Contains(
                   "empty response") ||
               normalized.Contains(
                   "timeout") ||
               normalized.Contains(
                   "unexpected end") ||
               normalized.Contains(
                   "unterminated") ||
               normalized.Contains(
                   "end of content") ||
               normalized.Contains(
                   "path 'generatednpcs");
    }

    private void MarkTerminalFailure(
        string message)
    {
        HasTerminalPopulationFailure =
            true;

        HasCompletedCanonicalPopulation =
            false;

        _requestInFlight =
            false;

        _nextRequestTime =
            float.PositiveInfinity;

        LastPopulationMessage =
            message;

        Debug.LogWarning(
            "[YQGeneratedNpcPlanningService] " +
            "TERMINAL POPULATION FAILURE\n" +
            message +
            "\nAccepted location batches retained in memory: " +
            _activeBatchIndex +
            "/" +
            _batchTargets.Count +
            "\nPending NPC identities: " +
            _pendingGeneratedNpcs.Count);

        YQStartupLoadingScreen.SetGenerationStage(
            string.Empty,
            Mathf.Max(
                0.88f,
                CurrentBatchProgress(
                    0.35f)));
    }

    // ============================================================
    // FINAL CANONICAL COMMIT
    // ============================================================

    private void CompletePopulationGeneration(
        WorldStateManager worldManager,
        WorldState world,
        GeneratedWorldPlanRecord plan)
    {
        if (worldManager == null ||
            world == null ||
            plan == null)
        {
            MarkTerminalFailure(
                "Could not finalize canonical NPC population because the active save disappeared.");

            return;
        }

        if (HasCompletedCanonicalPopulation)
            return;

        if (!ValidateCoverage(
                plan,
                _pendingGeneratedNpcs,
                _batchTargets,
                out string error))
        {
            MarkTerminalFailure(
                "Assembled NPC population failed final world validation: " +
                error);

            return;
        }

        plan.generatedNpcs.Clear();

        for (int i = 0;
             i < _pendingGeneratedNpcs.Count;
             i++)
        {
            GeneratedNpcPlanRecord npc =
                _pendingGeneratedNpcs[i];

            if (npc != null)
            {
                plan.generatedNpcs.Add(
                    npc);
            }
        }

        plan.EnsureCollections();

        world.globalFlags[
            "worldplan:generated_npcs"] =
            plan.generatedNpcs.Count;

        EnsureRuntimeNpcRecords(
            plan,
            world);

        world.AppendCanon(
            "Canonical generated population accepted for world " +
            plan.worldSeed +
            ": " +
            plan.generatedNpcs.Count +
            " generated NPC identities across " +
            _batchTargets.Count +
            " location batches.",
            64);

        world.TouchNow();

        worldManager.Save();

        HasCompletedCanonicalPopulation =
            true;

        HasTerminalPopulationFailure =
            false;

        _nextRequestTime =
            float.PositiveInfinity;

        LastPopulationMessage =
            "Canonical Ollama population accepted: " +
            plan.generatedNpcs.Count +
            " NPC identities across " +
            _batchTargets.Count +
            " location batches.";

        Debug.Log(
            "[YQGeneratedNpcPlanningService] " +
            LastPopulationMessage);

        YQStartupLoadingScreen.SetGenerationStage(
            YQGoddessGenerationDialogue
                .TakeNpcPrelude(
                    string.Empty),
            0.92f);
    }

    // ============================================================
    // GODDESS PROMPT / GROUNDED TRANSITIONS
    // ============================================================

    private static string BuildGoddessStageDescription(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target)
    {
        if (target == null)
        {
            return
                "The completed canonical inhabitants are ready to be placed into physical reality.";
        }

        if (target.kind ==
            PopulationBatchKind.Settlement)
        {
            GeneratedSettlementRecord settlement =
                FindSettlement(
                    plan,
                    target.locationId);

            if (settlement == null)
            {
                return
                    "Present location: " +
                    target.displayName +
                    ", a settlement.";
            }

            GeneratedRegionRecord region =
                FindRegion(
                    plan,
                    settlement.regionId);

            return
                "Present location: " +
                settlement.displayName +
                ". Settlement type: " +
                Safe(
                    settlement.kind,
                    "settlement") +
                ". Region: " +
                (region != null
                    ? Safe(
                        region.displayName,
                        settlement.regionId)
                    : settlement.regionId) +
                ". Approximate population: " +
                settlement.approxPopulation +
                ". Security: " +
                Safe(
                    settlement.securityProfile,
                    "unspecified") +
                ". Trade focus: " +
                Safe(
                    settlement.marketBias,
                    "general trade") +
                ". Services: " +
                JoinCompact(
                    settlement.serviceSlots) +
                ". Expected inhabitants in this pass: " +
                GetExpectedNpcCountForTarget(
                    plan,
                    target) +
                ".";
        }

        GeneratedEncampmentRecord encampment =
            FindEncampment(
                plan,
                target.locationId);

        if (encampment == null)
        {
            return
                "Present location: " +
                target.displayName +
                ", a hostile site.";
        }

        GeneratedRegionRecord hostileRegion =
            FindRegion(
                plan,
                encampment.regionId);

        return
            "Present location: " +
            encampment.displayName +
            ". Hostile site type: " +
            Safe(
                encampment.kind,
                "hostile site") +
            ". Region: " +
            (hostileRegion != null
                ? Safe(
                    hostileRegion.displayName,
                    encampment.regionId)
                : encampment.regionId) +
            ". Threat tier: " +
            encampment.threatTier +
            ". Known hostile family: " +
            Safe(
                encampment.monsterFamily,
                "unspecified") +
            ". Known combat profile: " +
            Safe(
                encampment.abilityProfile,
                "unspecified") +
            ".";
    }

    private static string BuildNpcGoddessCompletionContract(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target,
        PopulationBatchTarget nextTarget)
    {
        string nextOperation =
            nextTarget != null
                ? BuildGoddessStageDescription(
                    plan,
                    nextTarget)
                : "The accepted inhabitants will be placed into the physical world.";

        // note: NPC batches extend the single shared voice rail with only the facts and output count unique to population work.
        return
            YQGoddessGenerationDialogue.BuildBasicVoiceContract(
                BuildGoddessStageDescription(
                    plan,
                    target) +
                " The generatedNpcs in this same response are accepted facts available to completion and ambientLines.",
                nextOperation) +
            "NPC_BATCH_VOICE_RULES:\n" +
            "- Provide completion, nextPrelude, and 3-5 ambientLines after every required NPC object is complete.\n" +
            "- completion and ambientLines describe the accepted inhabitants as becoming present now; nextPrelude may use only NEXT_CONFIRMED_OPERATION.\n" +
            "- Prefer one useful social or practical observation supported by roles, routines, services, authority, trade, or a named NPC.\n" +
            "- NPC beliefs and private concerns remain attributed beliefs; never convert them into objective lore or combine them into a hidden theory.\n" +
            "- Do not write a census, checklist, prophecy, command, coder joke, generic verdict, or catchphrase.\n";
    }

    private static string BuildArchivedNpcGoddessCompletionContract(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target)
    {
        return
            "\n\nGODDESS_VOICE_CONTRACT\n" +
            "This contract applies ONLY to goddessVoice.\n" +
            "goddessVoice is presentation only and MUST NOT alter any NPC or world fact.\n\n" +

            "CURRENT_LOCATION_FACTS:\n" +
            BuildGoddessStageDescription(
                plan,
                target) +
            "\n\n" +

            "OUTPUT RESPONSIBILITY:\n" +
            "- goddessVoice contains completion, nextPrelude, and 3-5 ambientLines.\n" +
            "- nextPrelude may describe only the next approved operation, never unknown people, events, or locations.\n" +
            "- ambientLines must remain grounded in the current location and created NPCs; no generic filler.\n" +
            "- completion reacts only to the CURRENT location and the NPCs created in generatedNpcs in this same response.\n" +
            "- Write completion and ambientLines in immediate present tense, as those accepted inhabitants are becoming visible now while the next request runs.\n" +
            "- Never report that the location was completed in the past; the transcript deliberately trails one accepted operation.\n" +
            "- Do not discuss a future location.\n" +
            "- Do not predict what will happen next.\n\n" +
            "TARGET GRANULARITY — HARD REQUIREMENT:\n" +
            "- Aim for medium-grain observation: more specific than 'this place has people', less specific than listing individual tools, herbs, ledger entries, records, or private suspicions.\n" +
            "- Name the settlement or encampment only if the sentence would be unclear without it.\n" +
            "- Do not open with the settlement name, encampment name, or NPC name.\n" +
            "- Do not list services, occupations, inventory, records, symptoms, or multiple NPC concerns.\n" +
            "- Prefer one social pressure, role pattern, public contradiction, mood of work, or practical tension supported by the generated NPCs.\n" +
            "- The line should feel like an active thought, not a census, checklist, rumor board, or prophecy.\n\n" +
            "POINT OF VIEW — HARD REQUIREMENT:\n" +
            "- completion MUST sound like the Goddess is thinking aloud near the player, not filing a report.\n" +
            "- First-person is required; this is the same Goddess speaking throughout the entire loading sequence.\n" +
            "- Do not begin with 'I've noticed', 'I noticed', 'I've seen', 'I have seen', 'I will check', or 'I'll keep an eye'.\n" +
            "- Do not say that records, ledgers, notes, or entries are repeating unless the supplied canonical fields explicitly say that.\n" +
            "- Detached narrator prose is invalid.\n" +
            "- Never call the speaker 'she', 'her', 'the Goddess', 'the observer', or 'the entity'.\n\n" +

            "ANTI-REPETITION — HARD REQUIREMENT:\n" +
            "- Do not copy, quote, or paraphrase sentences from these instructions.\n" +
            "- Do not use a reusable stock closing sentence.\n" +
            "- Do not reuse the same sentence shape with a different settlement or NPC name.\n" +
            "- Vary openings across batches: sometimes imperative, sometimes dry aside, sometimes compact judgment, sometimes reluctant approval.\n" +
            "- Do not finish with a generic aphorism, verdict, slogan, or punchline.\n" +
            "- Do not end by making a generic statement about mortals, civilization, reality, efficiency, practicality, or predictability.\n" +
            "- The final clause must remain specific to this CURRENT location, one of its supplied facts, or one of the NPCs created in this response.\n" +
            "- Prefer concrete observation over a universal conclusion.\n" +
            "- Do not summarize every settlement as an inn, smith, supplies, paperwork, trade, or mortal behavior unless those exact facts are genuinely the most relevant features of the current location.\n\n" +

            "VOICE:\n" +
            "- A brilliant, anxious late-teen operator in a luminous future interface trying to roleplay as a flat, omniscient Goddess while the mask keeps slipping.\n" +
            "- She is desperately trying to help the player survive an engine that keeps resisting her; protectiveness is the motive, irritation is the disguise.\n" +
            "- Under the mask: anxious, dehydrated, sleep-starved, and still frighteningly competent.\n" +
            "- Fast, precise, slightly frayed, and still visibly competent.\n" +
            "- Borderline-manic urgency is allowed, but it must feel controlled: fast hands, sharp judgment, no incoherent flailing.\n" +
            "- Sarcasm should feel like stress leaking through expertise, not detached cosmic judgment.\n" +
            "- She is actively sorting, rejecting, stitching, debugging, and triaging this location so it makes sense for the player.\n" +
            "- She wants to sound monotone and divine; occasional anxious coder phrasing is the endearing crack in the performance.\n" +
            "- She may sound like she is mumbling to herself while building something the player cannot fully comprehend.\n" +
            "- Treat extraordinary things as workflow problems and ordinary civic details as suspiciously time-consuming.\n" +
            "- The sentence may imply terminals, patches, queues, bad inputs, compile pressure, or reality-as-build, but only as metaphor.\n" +
            "- Do not be theatrical, poetic, bubbly, cackling, or melodramatic.\n" +
            "- Do not speak like a report, diagnostic, narrator, or scientific paper.\n\n" +

            "THOUGHT STRUCTURE — HARD REQUIREMENT:\n" +
            "- completion must be two or three short sentences that form one continuous thought.\n" +
            "- Sentence 1 establishes flat divine control with a command or dry judgment.\n" +
            "- Sentence 2 names one concrete thing becoming real here and why it helps the player.\n" +
            "- An optional final fragment lets specific worry escape before she sharply restores the mask.\n" +
            "- Do not reuse a catchphrase, generic reassurance, trailing ellipsis, or identical recovery beat.\n\n" +

            "EPISTEMIC FIREWALL:\n" +
            "- Use only supplied CURRENT_LOCATION_FACTS and concrete fields created in generatedNpcs in this response.\n" +
            "- NPC localKnowledge and privateConcern are character beliefs or concerns, not automatically objective world truth.\n" +
"- Never promote an NPC's localKnowledge or privateConcern into objective truth.\n" +
"- If an NPC merely suspects, heard, worries, or believes something, either omit it or explicitly preserve that uncertainty.\n" +
"- Prefer roles, occupations, routines, personalities, services, authority, trade, and other directly grounded facts.\n" +
            "- Do not combine unrelated NPC concerns into a theory.\n" +
            "- Do not infer a hidden pattern.\n" +
            "- Do not invent causes, ancient explanations, secret factions, diseases, supernatural mechanisms, future events, missing people, or new history.\n" +
            "- Mention at most TWO concrete observations.\n" +
            "- Mention at most ONE NPC by proper name unless an explicit relationship connects two NPCs.\n\n" +

            "DIVINE-LANGUAGE CORRUPTION:\n" +
            "- A short valid-Unicode corrupted fragment may appear rarely if a truly inexpressible divine term is needed.\n" +
            "- Maximum one fragment.\n" +
            "- Never use corruption merely for decoration or to conceal invented lore.\n\n" +

            "NEVER mention AI, models, prompts, JSON, code, Unity, algorithms, generation, validation, datasets, tokens, stages, or phases.\n\n" +

            "LENGTH:\n" +
            "- completion should normally be 18-42 words across its two or three short sentences.\n" +
            "- Shorter is preferable to inventing information.\n\n" +

            "ABSOLUTE JSON SHAPE:\n" +
            "- goddessVoice MUST be an object.\n" +
            "- goddessVoice MUST contain completion, nextPrelude, and ambientLines.\n";
    }

    private static string BuildGroundedNextPrelude(
    GeneratedWorldPlanRecord plan,
    PopulationBatchTarget target)
    {
        if (plan == null ||
            target == null)
        {
            return
                "The inhabitants are decided. I can place them where they belong now.";
        }

        uint variantSeed =
            StableHash32(
                Safe(
                    plan.worldSeed,
                    string.Empty) +
                "|goddess_next|" +
                target.locationId);

        if (target.kind ==
            PopulationBatchKind.Settlement)
        {
            GeneratedSettlementRecord settlement =
                FindSettlement(
                    plan,
                    target.locationId);

            if (settlement == null)
            {
                return
                    "I am moving to the next settlement now.";
            }

            GeneratedRegionRecord region =
                FindRegion(
                    plan,
                    settlement.regionId);

            string name =
                Safe(
                    settlement.displayName,
                    target.displayName);

            string kind =
                Safe(
                    settlement.kind,
                    "settlement")
                    .Replace(
                        "_",
                        " ");

            string security =
                Safe(
                    settlement.securityProfile,
                    "local security")
                    .Replace(
                        "_",
                        " ");

            string market =
                Safe(
                    settlement.marketBias,
                    "ordinary trade")
                    .Replace(
                        "_",
                        " ");

            string regionName =
                region != null
                    ? Safe(
                        region.displayName,
                        settlement.regionId)
                    : Safe(
                        settlement.regionId,
                        "that region");

            string services =
                JoinCompact(
                    settlement.serviceSlots)
                    .Replace(
                        "_",
                        " ");

            int variant =
                (int)(
                    variantSeed %
                    6u);

            switch (variant)
            {
                case 0:
                    return
                        "Next settlement: " +
                        name +
                        ". " +
                        kind +
                        " order, " +
                        security +
                        ".";

                case 1:
                    return
                        name +
                        " runs on " +
                        market +
                        "; the rest will be people making logistics personal" +
                        ".";

                case 2:
                    return
                        "A " +
                        kind +
                        " in " +
                        regionName +
                        ", held together by " +
                        security +
                        ".";

                case 3:
                    return
                        "Now: " +
                        name +
                        ". Its public rhythm follows " +
                        market +
                        ".";

                case 4:
                    return
                        "The useful face of " +
                        name +
                        " is " +
                        services +
                        "; the private one can wait" +
                        ".";

                default:
                    return
                        "Next: " +
                        name +
                        " in " +
                        regionName +
                        ". Let its patterns arrive one person at a time" +
                        ".";
            }
        }

        GeneratedEncampmentRecord encampment =
            FindEncampment(
                plan,
                target.locationId);

        if (encampment == null)
        {
            return
                "I am moving to the next hostile location now.";
        }

        GeneratedRegionRecord hostileRegion =
            FindRegion(
                plan,
                encampment.regionId);

        string hostileName =
            Safe(
                encampment.displayName,
                target.displayName);

        string hostileKind =
            Safe(
                encampment.kind,
                "hostile site")
                .Replace(
                    "_",
                    " ");

        string monsterFamily =
            Safe(
                encampment.monsterFamily,
                "hostiles")
                .Replace(
                    "_",
                    " ");

        string abilityProfile =
            Safe(
                encampment.abilityProfile,
                "mixed combat")
                .Replace(
                    "_",
                    " ");

        string bossIntent =
            Safe(
                encampment.bossIntent,
                "local hostile leader")
                .Replace(
                    "_",
                    " ");

        string hostileRegionName =
            hostileRegion != null
                ? Safe(
                    hostileRegion.displayName,
                    encampment.regionId)
                : Safe(
                    encampment.regionId,
                    "that region");

        int hostileVariant =
            (int)(
                variantSeed %
                5u);

        switch (hostileVariant)
        {
            case 0:
                return
                    "Next is " +
                    hostileName +
                    ", a " +
                    hostileKind +
                    " in " +
                    hostileRegionName +
                    " at threat tier " +
                    encampment.threatTier +
                    ".";

            case 1:
                return
                    hostileName +
                    " holds " +
                    monsterFamily +
                    " with " +
                    abilityProfile +
                    ".";

            case 2:
                return
                    hostileName +
                    " is next: " +
                    monsterFamily +
                    " occupying a " +
                    hostileKind +
                    " in " +
                    hostileRegionName +
                    ".";

            case 3:
                return
                    "I have reached " +
                    hostileName +
                    ". It is threat tier " +
                    encampment.threatTier +
                    ", with a combat profile of " +
                    abilityProfile +
                    ".";

            default:
                return
                    "I am moving on to " +
                    hostileName +
                    ", where the significant hostile is defined as " +
                    bossIntent +
                    ".";
        }
    }

    // ============================================================
    // PROMPT
    // ============================================================

    private static string BuildPopulationPrompt(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target,
        PopulationBatchTarget nextTarget,
        int batchNumber,
        int batchCount)
    {
        StringBuilder context =
            new StringBuilder();

        GeneratedRegionRecord region =
            FindRegion(
                plan,
                target.regionId);

        string regionName =
            region != null
                ? Safe(
                    region.displayName,
                    target.regionId)
                : Safe(
                    target.regionId,
                    "unknown region");

        int expectedNpcCount =
            GetExpectedNpcCountForTarget(
                plan,
                target);

        context.AppendLine(
            "GENERATED_WORLD_NPC_POPULATION_LOCATION_BATCH");

        context.AppendLine(
            "WORLD_SEED: " +
            plan.worldSeed);

        context.AppendLine(
            "BATCH: " +
            batchNumber +
            "/" +
            batchCount);

        context.AppendLine(
            "BATCH_KIND: " +
            (target.kind ==
             PopulationBatchKind.Settlement
                ? "SETTLEMENT"
                : "HOSTILE_ENCAMPMENT"));

        context.AppendLine(
            "BATCH_LOCATION_ID: " +
            target.locationId);

        context.AppendLine(
            "BATCH_LOCATION_NAME: " +
            target.displayName);

        context.AppendLine(
            "BATCH_REGION_ID: " +
            target.regionId);

        context.AppendLine(
            "EXPECTED_TOTAL_NPCS_IN_THIS_RESPONSE: " +
            expectedNpcCount);

        context.AppendLine(
            "WORLD_SUMMARY: " +
            Safe(
                plan.summary,
                "generated world"));

        context.AppendLine();

        if (region != null)
        {
            context.AppendLine(
                "REGION_CONTEXT");

            context.AppendLine(
                "regionId=" +
                region.regionId);

            context.AppendLine(
                "name=" +
                region.displayName);

            context.AppendLine(
                "role=" +
                Safe(
                    region.role,
                    "generated region"));

            context.AppendLine(
                "dangerTier=" +
                region.dangerTier);

            context.AppendLine(
                "terrain=" +
                Safe(
                    region.terrainProfile,
                    "mixed terrain"));

            context.AppendLine(
                "climate=" +
                Safe(
                    region.climateProfile,
                    "variable climate"));

            context.AppendLine(
                "culture/lore=" +
                Safe(
                    region.lore,
                    "local culture"));

            context.AppendLine(
     "playerPressure=" +
     Safe(
         region.playerPressure,
         "local pressure"));

            context.AppendLine(
                "regionGroundingRule=playerPressure is design context, not an in-world event. " +
                "Do not turn it into strange weather, disappearances, supernatural signs, hidden patterns, " +
                "mysterious visitors, unexplained incidents, or newly invented regional history.");

            context.AppendLine();
        }

        if (target.kind ==
            PopulationBatchKind.Settlement)
        {
            GeneratedSettlementRecord settlement =
                FindSettlement(
                    plan,
                    target.locationId);

            if (settlement != null)
            {
                settlement.EnsureCollections();

                context.AppendLine(
                    "SETTLEMENT_TARGET");

                context.AppendLine(
                    "settlementId=" +
                    settlement.settlementId);

                context.AppendLine(
                    "name=" +
                    settlement.displayName);

                context.AppendLine(
                    "regionId=" +
                    settlement.regionId);

                context.AppendLine(
                    "kind=" +
                    settlement.kind);

                context.AppendLine(
                    "approxPopulation=" +
                    settlement.approxPopulation);

                context.AppendLine(
                    "EXACT_NPC_COUNT=" +
                    expectedNpcCount);

                context.AppendLine(
                    "security=" +
                    Safe(
                        settlement.securityProfile,
                        "local watch"));

                context.AppendLine(
                    "market=" +
                    Safe(
                        settlement.marketBias,
                        "general trade"));

                context.AppendLine(
                    "services=" +
                    JoinCompact(
                        settlement.serviceSlots));

                context.AppendLine(
                    "residentRoles=" +
                    JoinCompact(
                        settlement.residentRoles));

                if (IsReferenceSettlement(
                        plan,
                        settlement))
                {
                    // note: The first two physical cells are playable town references, so their LLM roster must cover visible service, safety, and local-work interactions.
                    context.AppendLine(
                        "REFERENCE_CELL_ROLE_REQUIREMENTS=Include exactly one shop or service operator, one guard or warden, and one notable local who can offer grounded work. These are separate NPCs. Use the supplied services and resident roles; do not invent lore to satisfy the roles.");
                }

                context.AppendLine(
                    "factions=" +
                    JoinCompact(
                        settlement.factionIds));

                /*
 * Do not pass design-language dailyLoop text directly to the small model.
 *
 * Terms such as rumors, threats, quests, discoveries, and player pressure
 * repeatedly cause it to invent new incidents and hidden plots.
 */
                context.AppendLine(
                    "dailyLoop=ordinary work, trade, maintenance, administration, meals, travel, rest, and social routines appropriate to this settlement");

                context.AppendLine(
                    "establishedSettlementLore=" +
                    Safe(
                        settlement.lore,
                        "No additional settlement lore supplied."));

                context.AppendLine(
                    "groundingOverride=The supplied settlement lore is CLOSED CANON. " +
                    "Words such as rumors, threats, disputes, pressure, curiosity, survival, route, or quest " +
                    "do NOT authorize new incidents, mysteries, disappearances, anomalies, artifacts, hidden causes, " +
                    "secret organizations, supernatural events, or previously unknown history.");
            }

            context.AppendLine();

            context.AppendLine(
                "TRANSACTION_CONTRACT");

            context.AppendLine(
                "- Generate NPCs ONLY for settlementId=" +
                target.locationId);

            context.AppendLine(
                "- Every returned NPC MUST use settlementId=" +
                target.locationId);

            context.AppendLine(
                "- Every returned NPC MUST use encampmentId=\"\".");

            context.AppendLine(
                "- Every returned NPC MUST use regionId=" +
                target.regionId);

            context.AppendLine(
                "- Every returned NPC MUST have hostile=false and boss=false.");

            context.AppendLine(
                "- Return EXACTLY " +
                expectedNpcCount +
                " NPC objects.");

            context.AppendLine(
                "- Do not create inhabitants for another settlement.");

            context.AppendLine(
                "- Do not create hostile-site NPCs.");

            context.AppendLine(
                "- Do not invent any location IDs.");
        }
        else
        {
            GeneratedEncampmentRecord encampment =
                FindEncampment(
                    plan,
                    target.locationId);

            if (encampment != null)
            {
                context.AppendLine(
                    "HOSTILE_ENCAMPMENT_TARGET");

                context.AppendLine(
                    "encampmentId=" +
                    encampment.encampmentId);

                context.AppendLine(
                    "name=" +
                    encampment.displayName);

                context.AppendLine(
                    "regionId=" +
                    encampment.regionId);

                context.AppendLine(
                    "EXACT_NPC_COUNT=1");

                context.AppendLine(
                    "kind=" +
                    encampment.kind);

                context.AppendLine(
                    "threatTier=" +
                    encampment.threatTier);

                context.AppendLine(
                    "factionId=" +
                    Safe(
                        encampment.inhabitantFactionId,
                        "hostile"));

                context.AppendLine(
                    "monsterFamily=" +
                    Safe(
                        encampment.monsterFamily,
                        "local hostiles"));

                context.AppendLine(
                    "layout=" +
                    Safe(
                        encampment.layoutIntent,
                        "hostile site"));

                context.AppendLine(
                    "abilityProfile=" +
                    Safe(
                        encampment.abilityProfile,
                        "mixed combat"));

                context.AppendLine(
                    "surfacePresentation=" +
                    Safe(
                        encampment.surfacePresentation,
                        "hostile presence"));

                context.AppendLine(
                    "bossIntent=" +
                    Safe(
                        encampment.bossIntent,
                        "local hostile leader"));

                context.AppendLine(
                    "lore=" +
                    Safe(
                        encampment.lore,
                        "local hostile site"));
            }

            context.AppendLine();

            context.AppendLine(
                "TRANSACTION_CONTRACT");

            context.AppendLine(
                "- Generate EXACTLY ONE significant hostile for encampmentId=" +
                target.locationId);

            context.AppendLine(
                "- The returned NPC MUST use encampmentId=" +
                target.locationId);

            context.AppendLine(
                "- The returned NPC MUST use settlementId=\"\".");

            context.AppendLine(
                "- The returned NPC MUST use regionId=" +
                target.regionId);

            context.AppendLine(
                "- The returned NPC MUST have hostile=true.");

            context.AppendLine(
                "- Use the supplied encampment factionId.");

            context.AppendLine(
                "- Do not create additional followers, residents, guards, or filler hostiles.");

            context.AppendLine(
                "- Do not reference another encampment.");

            context.AppendLine(
                "- Do not invent any location IDs.");
        }

        string task =
            "\nTASK\n" +
            "Generate ONLY the canonical physical NPC identities for BATCH_LOCATION_ID " +
            target.locationId +
            ".\n" +
            "This response represents exactly ONE physical location.\n" +
            "Do not generate NPCs belonging anywhere else.\n" +
            "These identities become permanent only after every world location succeeds.\n" +
            "\n" +

            "STRICT OUTPUT RULES\n" +
            "- Return one JSON object only.\n" +
            "- Return EXACTLY " +
            expectedNpcCount +
            " NPC object" +
            (expectedNpcCount == 1
                ? ""
                : "s") +
            ".\n" +
            "- Use the exact supplied regionId and physical location ID.\n" +
            "- Never rename or replace the supplied location.\n" +
            "- Never invent a settlementId or encampmentId.\n" +
            "- Never output an NPC for a neighboring location.\n" +
            "\n" +

            "IDENTITY RULES\n" +
            "- Every NPC must be a coherent person produced by this specific location, not a generic fantasy archetype with a random name attached.\n" +
            "- Derive each identity from the supplied region culture, physical environment, settlement or encampment, faction, economy, occupation, status, and local pressures.\n" +
            "- Determine the person's social identity and circumstances first, then generate a proper name that plausibly belongs to that culture.\n" +
            "- Generate a distinct personal proper name for every NPC.\n" +
            "- Names within the same local culture should sound culturally related without becoming minor variations of one another.\n" +
            "- Do not use generic fantasy-name defaults simply because they sound medieval, heroic, mystical, or archaic.\n" +
            "- Avoid generic fantasy-name templates, recycled phonetic patterns, obvious stock surnames, and names unrelated to the supplied culture or location.\n" +
            "- When FORBIDDEN_NAME_COMPONENTS or CUMULATIVE_FORBIDDEN_COLLISION_NAMES are supplied, treat every listed name or component as unavailable.\n" +
"- Do not copy, remix, respell, swap, or lightly mutate forbidden name material.\n" +
"- Unless explicit family or clan context is supplied, do not reuse an existing canonical first name or surname.\n" +
            "- Two NPCs must differ meaningfully in more than their proper names.\n" +
            "- Role, temperament, speech, routine, knowledge, concern, clothing, equipment, and social position must describe one internally consistent person.\n" +
            "- appearanceSummary must reflect practical consequences of occupation, environment, culture, wealth, or status rather than generic fantasy appearance.\n" +
            "- personality must describe an individual temperament rather than merely repeating the region's mood.\n" +
            "- speakingStyle must follow the NPC's occupation, age, status, temperament, and local culture.\n" +
            "- dailyRoutine must describe work or behavior appropriate to this exact physical location.\n" +
            "- localKnowledge must be information this particular person could plausibly know because of work, relationships, habits, or position.\n" +
            "- privateConcern must arise from the NPC's personal circumstances rather than simply restating the region's general danger or theme.\n" +
            "- Treat supplied lore as CLOSED CANON. Do not extend it with new events, causes, organizations, artifacts, or mysteries.\n" +
            "- localKnowledge and privateConcern must NOT casually invent major world lore.\n" +
            "- Do not manufacture disappearances, epidemics, supernatural events, ancient machines, secret inscriptions, hidden organizations, prophecies, mysterious artifacts, unexplained creatures, magical contamination, or large historical events unless explicitly supplied.\n" +
            "- Do not invent a missing traveler, missing family member, missing apprentice, missing guard, unexplained visitor, mysterious shadow, self-moving object, impossible sound, impossible light, unexplained ledger alteration, or supernatural malfunction merely to make an NPC interesting.\n" +
            "- A person's localKnowledge should usually be mundane, occupational, social, economic, geographic, or administrative knowledge appropriate to that person's actual life.\n" +
            "- A person's privateConcern should usually involve livelihood, family, debt, reputation, work, supplies, maintenance, safety, relationships, authority, customers, weather, travel, or ordinary local pressures.\n" +
            "- Do not make every NPC secretly aware of the same mystery.\n" +
            "- Do not create several unrelated clues pointing toward an ungenerated hidden plot.\n" +
            "- If supplied world context contains a strange phenomenon, only NPCs whose occupation would plausibly expose them to it may reference it.\n" +
            "- NPC beliefs are not automatically objective truth. Phrase uncertain personal knowledge as something the NPC saw, heard, suspects, was told, or worries about.\n" +
"- Prefer small actionable human problems over new cosmic mysteries.\n" +
"- When no unusual event is explicitly supplied, localKnowledge MUST describe ordinary concrete knowledge such as prices, schedules, road condition, weather exposure, supplies, maintenance, customers, livestock, crops, building condition, trade, work, or administration.\n" +
"- When no unusual event is explicitly supplied, privateConcern MUST remain an ordinary personal problem such as money, workload, repair needs, family obligations, inventory, reputation, safety procedure, debt, food, shelter, travel, or employment.\n" +
"- localKnowledge and privateConcern are descriptive summaries, not spoken dialogue. Do not begin them with 'I', 'my', or similar first-person dialogue.\n" +
"- Do not use mystery shorthand such as 'strange', 'unexplained', 'not natural', 'nobody knows why', 'vanished', 'whispers', 'voices', 'shadow', 'mysterious', or 'something deeper' unless that phenomenon is explicitly established in supplied canon.\n" +
"- Ordinary uncertainty is allowed only about ordinary matters. Uncertainty about a delivery time is acceptable; inventing a supernatural reason for the delayed delivery is not.\n" +
"- Preserve cultural coherence among inhabitants while varying social class, age, worldview, occupation, vocabulary, and personal priorities.\n" +
            "- For settlements, cover useful services and local authority before ordinary residents where supplied roles require them.\n" +
            "- For hostile encampments, create a leader, champion, named creature, overseer, captain, chief, broodmother, or equivalent identity appropriate to that site's actual faction and lore.\n" +
            "- boss=true only if the supplied boss intent supports a boss or leader encounter.\n" +
            "\n" +

            "SETTING CONSISTENCY\n" +
            "- Do not use real-world calendar years such as 1932, 2024, or similar.\n" +
            "- Do not use modern clock notation such as 3:07, 4:15 PM, or 06:15 unless an explicit local timekeeping system was supplied.\n" +
            "- Do not import modern nations, technologies, institutions, calendar systems, brands, or real-world historical events.\n" +
            "\n" +

            "COMPACTNESS RULES\n" +
            "- Keep presentation under roughly 14 words.\n" +
            "- Keep appearanceSummary to one short sentence.\n" +
            "- Keep personality to one short sentence.\n" +
            "- Keep speakingStyle to one short phrase.\n" +
            "- Keep dailyRoutine to one short sentence.\n" +
            "- Keep localKnowledge to one short sentence.\n" +
            "- Keep privateConcern to one short sentence.\n" +
            "- Keep tags compact.\n" +
            "- verboseInternals MUST be [].\n" +
            "- Do not mention AI, prompts, JSON, generation, implementation, or game systems.\n";

        string goddessVoiceContract =
            BuildNpcGoddessCompletionContract(
                plan,
                target,
                nextTarget);

        string schema;

        if (target.kind ==
            PopulationBatchKind.Settlement)
        {
            schema =
                "\nOUTPUT_SCHEMA\n" +
                "{\n" +
                "  \"schemaVersion\": \"generated_npc_population_v1\",\n" +
                "  \"worldSeed\": \"" +
                EscapeJson(
                    plan.worldSeed) +
                "\",\n" +
                "  \"generatedNpcs\": [\n" +
                "    {\n" +
                "      \"npcId\": \"\",\n" +
                "      \"regionId\": \"" +
                EscapeJson(
                    target.regionId) +
                "\",\n" +
                "      \"settlementId\": \"" +
                EscapeJson(
                    target.locationId) +
                "\",\n" +
                "      \"encampmentId\": \"\",\n" +
                "      \"factionId\": \"existing relevant faction id\",\n" +
                "      \"displayName\": \"canonical proper name\",\n" +
                "      \"role\": \"compact role key\",\n" +
                "      \"archetype\": \"resident|service|guard|notable\",\n" +
                "      \"ageBand\": \"young_adult|adult|middle_aged|elder|ageless|unknown\",\n" +
                "      \"presentation\": \"brief presentation\",\n" +
                "      \"appearanceSummary\": \"short sentence\",\n" +
                "      \"personality\": \"short sentence\",\n" +
                "      \"speakingStyle\": \"short phrase\",\n" +
                "      \"dailyRoutine\": \"short sentence\",\n" +
                "      \"localKnowledge\": \"short sentence\",\n" +
                "      \"privateConcern\": \"short sentence\",\n" +
                "      \"notable\": false,\n" +
                "      \"merchant\": false,\n" +
                "      \"guard\": false,\n" +
                "      \"hostile\": false,\n" +
                "      \"boss\": false,\n" +
                "      \"tags\": [\"compact\", \"tags\"],\n" +
                "      \"verboseInternals\": []\n" +
                "    }\n" +
                "  ],\n" +
                "  \"goddessVoice\": " +
                GoddessCompletionJsonSchema +
                "\n" +
                "}";
        }
        else
        {
            GeneratedEncampmentRecord encampment =
                FindEncampment(
                    plan,
                    target.locationId);

            string factionId =
                encampment != null
                    ? Safe(
                        encampment.inhabitantFactionId,
                        string.Empty)
                    : string.Empty;

            schema =
                "\nOUTPUT_SCHEMA\n" +
                "{\n" +
                "  \"schemaVersion\": \"generated_npc_population_v1\",\n" +
                "  \"worldSeed\": \"" +
                EscapeJson(
                    plan.worldSeed) +
                "\",\n" +
                "  \"generatedNpcs\": [\n" +
                "    {\n" +
                "      \"npcId\": \"\",\n" +
                "      \"regionId\": \"" +
                EscapeJson(
                    target.regionId) +
                "\",\n" +
                "      \"settlementId\": \"\",\n" +
                "      \"encampmentId\": \"" +
                EscapeJson(
                    target.locationId) +
                "\",\n" +
                "      \"factionId\": \"" +
                EscapeJson(
                    factionId) +
                "\",\n" +
                "      \"displayName\": \"canonical proper name\",\n" +
                "      \"role\": \"leader|champion|chief|captain|overseer|named_creature\",\n" +
                "      \"archetype\": \"hostile|hostile_leader\",\n" +
                "      \"ageBand\": \"young_adult|adult|middle_aged|elder|ageless|unknown\",\n" +
                "      \"presentation\": \"brief presentation\",\n" +
                "      \"appearanceSummary\": \"short sentence\",\n" +
                "      \"personality\": \"short sentence\",\n" +
                "      \"speakingStyle\": \"short phrase\",\n" +
                "      \"dailyRoutine\": \"short sentence\",\n" +
                "      \"localKnowledge\": \"short sentence\",\n" +
                "      \"privateConcern\": \"short sentence\",\n" +
                "      \"notable\": true,\n" +
                "      \"merchant\": false,\n" +
                "      \"guard\": false,\n" +
                "      \"hostile\": true,\n" +
                "      \"boss\": false,\n" +
                "      \"tags\": [\"hostile\", \"significant\"],\n" +
                "      \"verboseInternals\": []\n" +
                "    }\n" +
                "  ],\n" +
                "  \"goddessVoice\": " +
                GoddessCompletionJsonSchema +
                "\n" +
                "}";
        }

        return
            context.ToString() +
            task +
            goddessVoiceContract +
            schema;
    }

    private static int GetExpectedNpcCountForTarget(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target)
    {
        if (plan == null ||
            target == null)
        {
            return 0;
        }

        if (target.kind ==
            PopulationBatchKind.Settlement)
        {
            GeneratedSettlementRecord settlement =
                FindSettlement(
                    plan,
                    target.locationId);

            return
                settlement != null
                    ? ResolveDesiredResidentCount(
                        plan,
                        settlement)
                    : 0;
        }

        GeneratedEncampmentRecord encampment =
            FindEncampment(
                plan,
                target.locationId);

        return
            encampment != null
                ? 1
                : 0;
    }

    private static int ResolveDesiredResidentCount(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement)
    {
        if (settlement == null)
            return 3;

        string kind =
            Safe(
                settlement.kind,
                string.Empty)
                .ToLowerInvariant();

        int target =
            2;

        if (kind.Contains("city") ||
            kind.Contains("capital"))
        {
            target =
                3;
        }
        else if (kind.Contains("town") ||
                 kind.Contains("market"))
        {
            target =
                3;
        }
        else if (kind.Contains("village") ||
                 kind.Contains("riverhold"))
        {
            target =
                2;
        }
        else if (kind.Contains("outpost") ||
                 kind.Contains("waystation"))
        {
            target =
                2;
        }

        int serviceCount =
            settlement.serviceSlots != null
                ? settlement.serviceSlots.Count
                : 0;

        target =
            Mathf.Max(
                target,
                Mathf.Min(
                    serviceCount,
                    3));

        if (settlement.approxPopulation >= 40)
        {
            target =
                Mathf.Max(
                    target,
                    3);
        }
        else if (settlement.approxPopulation >= 24)
        {
            target =
                Mathf.Max(
                    target,
                    3);
        }

        // note: Three canonical residents establish merchant, authority, and local-life identity while later proximity expansion can deepen the population.
        int upperBound =
            3;

        int minimum =
            IsReferenceSettlement(
                plan,
                settlement)
                ? 3
                : 2;

        return
            Mathf.Clamp(
                target,
                minimum,
                upperBound);
    }

    // note: Physical reference cells are identified by persisted plan order, so the rule survives a save/load without introducing another runtime ownership system.
    private static bool IsReferenceSettlement(
        GeneratedWorldPlanRecord plan,
        GeneratedSettlementRecord settlement)
    {
        if (plan == null ||
            plan.settlements == null ||
            settlement == null)
        {
            return false;
        }

        for (int i = 0;
             i < plan.settlements.Count &&
             i < 2;
             i++)
        {
            GeneratedSettlementRecord candidate =
                plan.settlements[i];

            if (candidate == settlement ||
                candidate != null &&
                string.Equals(
                    candidate.settlementId,
                    settlement.settlementId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // ============================================================
    // PARSE / NORMALIZE LOCATION BATCH
    // ============================================================

    private static bool TryParsePopulationBatch(
        string raw,
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target,
        List<GeneratedNpcPlanRecord> previouslyAccepted,
        out List<GeneratedNpcPlanRecord> result,
        out YQGoddessGenerationVoiceDto goddessVoice,
        out string error)
    {
        result =
            new List<GeneratedNpcPlanRecord>();

        goddessVoice =
            null;

        error =
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                raw))
        {
            error =
                "empty response";

            return false;
        }

        if (plan == null ||
            target == null ||
            string.IsNullOrWhiteSpace(
                target.locationId))
        {
            error =
                "invalid active location batch";

            return false;
        }

        try
        {
            string json =
                ExtractFirstJsonObject(
                    raw);

            if (string.IsNullOrWhiteSpace(
                    json))
            {
                error =
                    "no complete JSON object";

                return false;
            }

            JObject root;
            if (!TryParseRootIgnoringBrokenGoddessVoice(
                    json,
                    out root,
                    out string parseError))
            {
                error =
                    parseError;

                return false;
            }

            /*
             * Goddess dialogue is optional presentation data.
             *
             * It cannot invalidate an otherwise-correct NPC transaction.
             */
            try
            {
                goddessVoice =
                    ExtractOptionalGoddessVoice(
                        root);
            }
            catch (Exception voiceException)
            {
                goddessVoice =
                    null;

                Debug.LogWarning(
                    "[YQGeneratedNpcPlanningService] " +
                    "Ignoring malformed optional Goddess dialogue: " +
                    voiceException.Message);
            }

            /*
             * Strip every tolerated presentation field before canonical
             * deserialization.
             */
            root.Remove(
                "goddessVoice");

            root.Remove(
                "nextPrelude");

            root.Remove(
                "next");

            root.Remove(
                "prelude");

            root.Remove(
                "completion");

            string canonicalJson =
                root.ToString(
                    Formatting.None);

            GeneratedNpcPopulationResponse response =
                JsonConvert.DeserializeObject<
                    GeneratedNpcPopulationResponse>(
                        canonicalJson);

            if (response == null)
            {
                error =
                    "JSON parsed to null";

                return false;
            }

            response.EnsureCollections();

            if (!string.IsNullOrWhiteSpace(
                    response.worldSeed) &&
                !string.Equals(
                    response.worldSeed,
                    plan.worldSeed,
                    StringComparison.OrdinalIgnoreCase))
            {
                error =
                    "response worldSeed does not match active world";

                return false;
            }

            int expectedCount =
                GetExpectedNpcCountForTarget(
                    plan,
                    target);

            if (response.generatedNpcs.Count !=
                expectedCount)
            {
                error =
                    BatchKindName(
                        target) +
                    " '" +
                    target.displayName +
                    "' expected exactly " +
                    expectedCount +
                    " canonical NPC" +
                    (expectedCount == 1
                        ? ""
                        : "s") +
                    " but received " +
                    response.generatedNpcs.Count;

                return false;
            }

            HashSet<string> usedNpcIds =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            HashSet<string> usedNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            if (previouslyAccepted != null)
            {
                for (int i = 0;
                     i < previouslyAccepted.Count;
                     i++)
                {
                    GeneratedNpcPlanRecord previous =
                        previouslyAccepted[i];

                    if (previous == null)
                        continue;

                    if (!string.IsNullOrWhiteSpace(
                            previous.npcId))
                    {
                        usedNpcIds.Add(
                            previous.npcId);
                    }

                    if (!string.IsNullOrWhiteSpace(
                            previous.displayName))
                    {
                        usedNames.Add(
                            previous.displayName.Trim());
                    }
                }
            }

            /*
 * Validate all names before accepting any NPC from this response.
 *
 * Report every collision at once so one retry can correct the entire
 * response instead of discovering one duplicate per attempt.
 */
            HashSet<string> namesSeenInThisResponse =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            List<string> duplicateNames =
                new List<string>();

            for (int i = 0;
                 i < response.generatedNpcs.Count;
                 i++)
            {
                GeneratedNpcPlanRecord candidate =
                    response.generatedNpcs[i];

                if (candidate == null)
                {
                    error =
                        "response contains a null NPC record";

                    return false;
                }

                if (string.IsNullOrWhiteSpace(
                        candidate.displayName))
                {
                    error =
                        "response contains an NPC with no displayName";

                    return false;
                }

                candidate.displayName =
                    TrimTo(
                        candidate.displayName,
                        72);

                bool collidesWithAcceptedCanon =
                    usedNames.Contains(
                        candidate.displayName);

                bool collidesInsideResponse =
                    !namesSeenInThisResponse.Add(
                        candidate.displayName);

                if (collidesWithAcceptedCanon ||
                    collidesInsideResponse)
                {
                    // note: The location batch is otherwise usable; qualify repeated names instead of burning LLM retries.
                    candidate.displayName =
                        BuildUniqueLocationQualifiedNpcName(
                            candidate.displayName,
                            target.displayName,
                            usedNames,
                            namesSeenInThisResponse);

                    namesSeenInThisResponse.Add(
                        candidate.displayName);
                }
            }

            if (duplicateNames.Count > 0)
            {
                StringBuilder duplicateMessage =
                    new StringBuilder();

                for (int i = 0;
                     i < duplicateNames.Count;
                     i++)
                {
                    if (i > 0)
                    {
                        duplicateMessage.Append(
                            ", ");
                    }

                    duplicateMessage.Append(
                        "'");

                    duplicateMessage.Append(
                        duplicateNames[i]);

                    duplicateMessage.Append(
                        "'");
                }

                error =
                    "duplicate canonical NPC names: " +
                    duplicateMessage;

                return false;
            }

            GeneratedSettlementRecord targetSettlement =
                target.kind ==
                PopulationBatchKind.Settlement
                    ? FindSettlement(
                        plan,
                        target.locationId)
                    : null;

            GeneratedEncampmentRecord targetEncampment =
                target.kind ==
                PopulationBatchKind.Encampment
                    ? FindEncampment(
                        plan,
                        target.locationId)
                    : null;

            if (target.kind ==
                    PopulationBatchKind.Settlement &&
                targetSettlement == null)
            {
                error =
                    "active settlement batch no longer exists in the world plan";

                return false;
            }

            if (target.kind ==
                    PopulationBatchKind.Encampment &&
                targetEncampment == null)
            {
                error =
                    "active encampment batch no longer exists in the world plan";

                return false;
            }

            for (int i = 0;
                 i < response.generatedNpcs.Count;
                 i++)
            {
                GeneratedNpcPlanRecord npc =
                    response.generatedNpcs[i];

                if (npc == null)
                {
                    error =
                        "response contains a null NPC record";

                    return false;
                }

                npc.EnsureCollections();

                if (string.IsNullOrWhiteSpace(
                        npc.displayName))
                {
                    error =
                        "response contains an NPC with no displayName";

                    return false;
                }

                npc.displayName =
    TrimTo(
        npc.displayName,
        72);

                /*
                 * Full-response name uniqueness was already validated above.
                 */
                usedNames.Add(
                    npc.displayName);

                string locationId;

                if (target.kind ==
                    PopulationBatchKind.Settlement)
                {
                    if (!string.IsNullOrWhiteSpace(
                            npc.encampmentId))
                    {
                        error =
                            "NPC '" +
                            npc.displayName +
                            "' invented or referenced an encampment during settlement batch '" +
                            target.displayName +
                            "'";

                        return false;
                    }

                    if (!string.Equals(
                            npc.settlementId,
                            targetSettlement.settlementId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error =
                            "NPC '" +
                            npc.displayName +
                            "' must reference exact settlementId '" +
                            targetSettlement.settlementId +
                            "'";

                        return false;
                    }

                    if (!string.IsNullOrWhiteSpace(
                            npc.regionId) &&
                        !string.Equals(
                            npc.regionId,
                            targetSettlement.regionId,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error =
                            "NPC '" +
                            npc.displayName +
                            "' references wrong regionId '" +
                            npc.regionId +
                            "'";

                        return false;
                    }

                    targetSettlement.EnsureCollections();

                    npc.settlementId =
                        targetSettlement.settlementId;

                    npc.encampmentId =
                        string.Empty;

                    npc.regionId =
                        targetSettlement.regionId;

                    if (string.IsNullOrWhiteSpace(
                            npc.factionId))
                    {
                        npc.factionId =
                            targetSettlement.factionIds != null &&
                            targetSettlement.factionIds.Count > 0
                                ? targetSettlement.factionIds[0]
                                : targetSettlement.regionId +
                                  "_civic";
                    }

                    npc.hostile =
                        false;

                    npc.boss =
                        false;

                    locationId =
                        targetSettlement.settlementId;
                }
                else
                {
                    // note: The active batch target is canonical; repair common LLM ID drift before validation.
                    npc.settlementId =
                        string.Empty;

                    npc.encampmentId =
                        targetEncampment.encampmentId;

                    npc.settlementId =
                        string.Empty;

                    npc.regionId =
                        targetEncampment.regionId;

                    npc.factionId =
                        Safe(
                            targetEncampment.inhabitantFactionId,
                            npc.factionId);

                    npc.hostile =
                        true;

                    locationId =
                        targetEncampment.encampmentId;
                }

                npc.role =
                    NormalizeKey(
                        Safe(
                            npc.role,
                            npc.hostile
                                ? "hostile"
                                : "resident"));

                npc.archetype =
                    NormalizeArchetype(
                        npc.archetype,
                        npc);

                npc.ageBand =
                    NormalizeKey(
                        Safe(
                            npc.ageBand,
                            "adult"));

                npc.presentation =
                    TrimTo(
                        Safe(
                            npc.presentation,
                            string.Empty),
                        120);

                npc.appearanceSummary =
                    TrimTo(
                        Safe(
                            npc.appearanceSummary,
                            string.Empty),
                        220);

                npc.personality =
                    TrimTo(
                        Safe(
                            npc.personality,
                            string.Empty),
                        220);

                npc.speakingStyle =
                    TrimTo(
                        Safe(
                            npc.speakingStyle,
                            "plainspoken"),
                        100);

                npc.dailyRoutine =
                    TrimTo(
                        Safe(
                            npc.dailyRoutine,
                            string.Empty),
                        220);

                npc.localKnowledge =
                    TrimTo(
                        Safe(
                            npc.localKnowledge,
                            string.Empty),
                        240);

                npc.privateConcern =
                    TrimTo(
                        Safe(
                            npc.privateConcern,
                            string.Empty),
                        240);

                npc.tags =
                    NormalizeTags(
                        npc.tags,
                        npc);

                npc.verboseInternals =
                    NormalizeStringList(
                        npc.verboseInternals,
                        0,
                        160);

                string stableNpcId =
                    "npc_" +
                    StableHash32(
                        plan.worldSeed +
                        "|" +
                        locationId +
                        "|" +
                        npc.displayName +
                        "|" +
                        npc.role)
                        .ToString("x8");

                int collisionIndex =
                    1;

                while (!usedNpcIds.Add(
                           stableNpcId))
                {
                    stableNpcId =
                        "npc_" +
                        StableHash32(
                            plan.worldSeed +
                            "|" +
                            locationId +
                            "|" +
                            npc.displayName +
                            "|" +
                            npc.role +
                            "|" +
                            collisionIndex)
                            .ToString("x8");

                    collisionIndex++;
                }

                npc.npcId =
                    stableNpcId;

                result.Add(
                    npc);
            }

            RepairReferenceSettlementRoleCoverage(
                plan,
                target,
                result);

            if (!ValidateBatchCoverage(
                    plan,
                    target,
                    result,
                    out error))
            {
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error =
                ex.Message;

            return false;
        }
    }

    private static void RepairReferenceSettlementRoleCoverage(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target,
        List<GeneratedNpcPlanRecord> records)
    {
        if (plan == null || target == null || records == null ||
            target.kind != PopulationBatchKind.Settlement ||
            records.Count < 3)
        {
            return;
        }

        GeneratedSettlementRecord settlement = FindSettlement(
            plan,
            target.locationId);

        if (!IsReferenceSettlement(plan, settlement))
            return;

        // note: Preserve generated identities and prose while normalizing the three tutorial residents onto distinct service, guard, and local-guide gameplay contracts.
        GeneratedNpcPlanRecord service = records[0];
        GeneratedNpcPlanRecord guard = records[1];
        GeneratedNpcPlanRecord notable = records[2];

        if (service != null)
        {
            service.merchant = true;
            service.guard = false;
            service.notable = false;
            service.archetype = "service";
            if (!RoleContainsAny(service.role,
                    "merchant", "trader", "vendor", "shop", "inn",
                    "smith", "apothec", "tailor", "stable",
                    "locksmith", "banker"))
            {
                service.role = settlement != null &&
                    settlement.serviceSlots != null &&
                    settlement.serviceSlots.Count > 0
                        ? NormalizeKey(settlement.serviceSlots[0])
                        : "merchant";
            }
        }

        if (guard != null)
        {
            guard.merchant = false;
            guard.guard = true;
            guard.notable = false;
            guard.archetype = "guard";
            if (!RoleContainsAny(guard.role, "guard", "warden", "watch", "captain"))
                guard.role = "guard";
        }

        if (notable != null)
        {
            notable.merchant = false;
            notable.guard = false;
            notable.notable = true;
            notable.archetype = "notable";
            if (!RoleContainsAny(notable.role,
                    "guide", "chief", "reeve", "mayor", "elder",
                    "scout", "scribe", "scholar", "healer", "quest"))
            {
                notable.role = "local_guide";
            }
        }
    }

    private static string BuildUniqueLocationQualifiedNpcName(
        string displayName,
        string locationName,
        HashSet<string> acceptedNames,
        HashSet<string> responseNames)
    {
        string baseName =
            TrimTo(
                Safe(
                    displayName,
                    "Unnamed Hostile"),
                42);

        string siteName =
            TrimTo(
                Safe(
                    locationName,
                    "the Site"),
                24);

        string candidate =
            TrimTo(
                baseName +
                " of " +
                siteName,
                72);

        int index =
            2;

        while (ContainsName(
                   acceptedNames,
                   candidate) ||
               ContainsName(
                   responseNames,
                   candidate))
        {
            // note: Deterministic suffixing prevents retry loops without inventing new lore or changing IDs.
            candidate =
                TrimTo(
                    baseName +
                    " of " +
                    siteName +
                    " " +
                    index,
                    72);

            index++;
        }

        return candidate;
    }

    private static bool ContainsName(
        HashSet<string> names,
        string candidate)
    {
        return
            names != null &&
            !string.IsNullOrWhiteSpace(
                candidate) &&
            names.Contains(
                candidate);
    }

    private static bool ValidateBatchCoverage(
        GeneratedWorldPlanRecord plan,
        PopulationBatchTarget target,
        List<GeneratedNpcPlanRecord> records,
        out string error)
    {
        error =
            string.Empty;

        if (plan == null ||
            target == null ||
            records == null)
        {
            error =
                "location population validation received null data";

            return false;
        }

        int expected =
            GetExpectedNpcCountForTarget(
                plan,
                target);

        if (records.Count !=
            expected)
        {
            error =
                BatchKindName(
                    target) +
                " '" +
                target.displayName +
                "' expected exactly " +
                expected +
                " canonical NPC" +
                (expected == 1
                    ? ""
                    : "s") +
                " but received " +
                records.Count;

            return false;
        }

        if (target.kind ==
            PopulationBatchKind.Settlement)
        {
            GeneratedSettlementRecord settlement =
                FindSettlement(
                    plan,
                    target.locationId);

            if (settlement == null)
            {
                error =
                    "target settlement disappeared during validation";

                return false;
            }

            for (int i = 0;
                 i < records.Count;
                 i++)
            {
                GeneratedNpcPlanRecord npc =
                    records[i];

                if (npc == null ||
                    npc.hostile ||
                    !string.Equals(
                        npc.settlementId,
                        settlement.settlementId,
                        StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(
                        npc.encampmentId))
                {
                    error =
                        "settlement batch '" +
                        settlement.displayName +
                        "' contains an NPC outside its exact settlement contract";

                    return false;
                }
            }

            if (IsReferenceSettlement(
                    plan,
                    settlement) &&
                !HasReferenceSettlementRoleCoverage(
                    records,
                    out error))
            {
                return false;
            }

            return true;
        }

        GeneratedEncampmentRecord encampment =
            FindEncampment(
                plan,
                target.locationId);

        if (encampment == null)
        {
            error =
                "target encampment disappeared during validation";

            return false;
        }

        if (records.Count != 1)
        {
            error =
                "encampment '" +
                encampment.displayName +
                "' expected exactly one significant hostile but received " +
                records.Count;

            return false;
        }

        GeneratedNpcPlanRecord hostile =
            records[0];

        if (hostile == null ||
            !hostile.hostile ||
            !string.Equals(
                hostile.encampmentId,
                encampment.encampmentId,
                StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(
                hostile.settlementId))
        {
            error =
                "encampment batch '" +
                encampment.displayName +
                "' does not contain exactly one valid hostile for its supplied location";

            return false;
        }

        return true;
    }

    // ============================================================
    // FINAL WORLD COVERAGE VALIDATION
    // ============================================================

    private static bool ValidateCoverage(
        GeneratedWorldPlanRecord plan,
        List<GeneratedNpcPlanRecord> records,
        List<PopulationBatchTarget> requiredTargets,
        out string error)
    {
        error =
            string.Empty;

        if (plan == null ||
            records == null ||
            requiredTargets == null)
        {
            error =
                "population validation received null data";

            return false;
        }

        HashSet<string> names =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        HashSet<string> ids =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < records.Count;
             i++)
        {
            GeneratedNpcPlanRecord npc =
                records[i];

            if (npc == null)
            {
                error =
                    "final population contains a null NPC";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    npc.displayName))
            {
                error =
                    "final population contains an unnamed NPC";

                return false;
            }

            if (!names.Add(
                    npc.displayName))
            {
                error =
                    "final population contains duplicate name '" +
                    npc.displayName +
                    "'";

                return false;
            }

            if (string.IsNullOrWhiteSpace(
                    npc.npcId) ||
                !ids.Add(
                    npc.npcId))
            {
                error =
                    "final population contains duplicate or empty npcId '" +
                    Safe(
                        npc.npcId,
                        "<empty>") +
                    "'";

                return false;
            }
        }

        for (int i = 0;
             i < requiredTargets.Count;
             i++)
        {
            PopulationBatchTarget target =
                requiredTargets[i];

            if (target == null ||
                target.kind !=
                PopulationBatchKind.Settlement)
            {
                continue;
            }

            GeneratedSettlementRecord settlement =
                FindSettlement(
                    plan,
                    target.locationId);

            if (settlement == null)
                continue;

            int expected =
                ResolveDesiredResidentCount(
                    plan,
                    settlement);

            int actual =
                0;

            for (int npcIndex = 0;
                 npcIndex < records.Count;
                 npcIndex++)
            {
                GeneratedNpcPlanRecord npc =
                    records[npcIndex];

                if (npc != null &&
                    !npc.hostile &&
                    string.Equals(
                        npc.settlementId,
                        settlement.settlementId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    actual++;
                }
            }

            if (actual != expected)
            {
                error =
                    "settlement '" +
                    settlement.displayName +
                    "' expected " +
                    expected +
                    " canonical NPCs but received " +
                    actual;

                return false;
            }
        }

        for (int i = 0;
             i < requiredTargets.Count;
             i++)
        {
            PopulationBatchTarget target =
                requiredTargets[i];

            if (target == null ||
                target.kind !=
                PopulationBatchKind.Encampment)
            {
                continue;
            }

            GeneratedEncampmentRecord encampment =
                FindEncampment(
                    plan,
                    target.locationId);

            if (encampment == null)
                continue;

            int actual =
                0;

            for (int npcIndex = 0;
                 npcIndex < records.Count;
                 npcIndex++)
            {
                GeneratedNpcPlanRecord npc =
                    records[npcIndex];

                if (npc != null &&
                    npc.hostile &&
                    string.Equals(
                        npc.encampmentId,
                        encampment.encampmentId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    actual++;
                }
            }

            if (actual != 1)
            {
                error =
                    "encampment '" +
                    encampment.displayName +
                    "' expected exactly one canonical significant hostile but received " +
                    actual;

                return false;
            }
        }

        return true;
    }

    private static string NormalizeArchetype(
        string value,
        GeneratedNpcPlanRecord npc)
    {
        string key =
            NormalizeKey(
                value);

        if (npc != null &&
            npc.hostile)
        {
            if (npc.boss ||
                key ==
                "hostile_leader")
            {
                return
                    "hostile_leader";
            }

            return
                "hostile";
        }

        switch (key)
        {
            case "service":
            case "guard":
            case "notable":
            case "resident":
                return key;
        }

        if (npc != null)
        {
            if (npc.merchant)
                return "service";

            if (npc.guard)
                return "guard";

            if (npc.notable)
                return "notable";
        }

        return
            "resident";
    }

    private static List<string> NormalizeTags(
        List<string> tags,
        GeneratedNpcPlanRecord npc)
    {
        List<string> result =
            NormalizeStringList(
                tags,
                10,
                48);

        AddUnique(
            result,
            "generated");

        AddUnique(
            result,
            "npc");

        AddUnique(
            result,
            npc != null &&
            npc.hostile
                ? "hostile"
                : "resident");

        if (npc != null)
        {
            AddUnique(
                result,
                npc.archetype);

            AddUnique(
                result,
                npc.role);

            AddUnique(
                result,
                npc.regionId);

            AddUnique(
                result,
                npc.settlementId);

            AddUnique(
                result,
                npc.encampmentId);
        }

        return result;
    }

    // ============================================================
    // WORLDSTATE NPC MIRROR
    // ============================================================

    private static void EnsureRuntimeNpcRecords(
        GeneratedWorldPlanRecord plan,
        WorldState world)
    {
        if (plan == null ||
            world == null)
        {
            return;
        }

        plan.EnsureCollections();

        world.EnsureCollections();

        long now =
            DateTimeOffset.UtcNow
                .ToUnixTimeSeconds();

        for (int i = 0;
             i < plan.generatedNpcs.Count;
             i++)
        {
            GeneratedNpcPlanRecord generated =
                plan.generatedNpcs[i];

            if (generated == null ||
                string.IsNullOrWhiteSpace(
                    generated.npcId))
            {
                continue;
            }

            string locationId =
                !string.IsNullOrWhiteSpace(
                    generated.settlementId)
                    ? generated.settlementId
                    : generated.encampmentId;

            WorldState.NpcRecord existing =
                null;

            for (int worldIndex = 0;
                 worldIndex < world.npcs.Count;
                 worldIndex++)
            {
                WorldState.NpcRecord candidate =
                    world.npcs[
                        worldIndex];

                if (candidate != null &&
                    string.Equals(
                        candidate.npcId,
                        generated.npcId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    existing =
                        candidate;

                    break;
                }
            }

            string description =
                BuildRuntimeDescription(
                    generated);

            if (existing != null)
            {
                existing.name =
                    generated.displayName;

                existing.description =
                    description;

                existing.factionId =
                    generated.factionId;

                if (string.IsNullOrWhiteSpace(
                        existing.locationId))
                {
                    existing.locationId =
                        locationId;
                }

                if (string.IsNullOrWhiteSpace(
                        existing.status))
                {
                    existing.status =
                        generated.hostile
                            ? "hostile"
                            : "available";
                }

                existing.updatedUnix =
                    now;

                continue;
            }

            world.npcs.Add(
                new WorldState.NpcRecord
                {
                    npcId =
                        generated.npcId,

                    name =
                        generated.displayName,

                    description =
                        description,

                    factionId =
                        generated.factionId,

                    locationId =
                        locationId,

                    affinityToPlayer =
                        generated.hostile
                            ? -1f
                            : 0f,

                    status =
                        generated.hostile
                            ? "hostile"
                            : "available",

                    createdUnix =
                        now,

                    updatedUnix =
                        now
                });
        }
    }

    private static string BuildRuntimeDescription(
        GeneratedNpcPlanRecord npc)
    {
        if (npc == null)
            return string.Empty;

        StringBuilder sb =
            new StringBuilder();

        if (!string.IsNullOrWhiteSpace(
                npc.role))
        {
            sb.Append(
                npc.role);
        }

        if (!string.IsNullOrWhiteSpace(
                npc.personality))
        {
            if (sb.Length > 0)
                sb.Append(". ");

            sb.Append(
                npc.personality);
        }

        if (!string.IsNullOrWhiteSpace(
                npc.privateConcern))
        {
            if (sb.Length > 0)
                sb.Append(" ");

            sb.Append(
                "Concern: ");

            sb.Append(
                npc.privateConcern);
        }

        return
            TrimTo(
                sb.ToString(),
                560);
    }

    // ============================================================
    // PLAN HELPERS
    // ============================================================

    private static bool IsReadyWorldPlan(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return false;

        plan.EnsureCollections();

        if (string.IsNullOrWhiteSpace(
                plan.worldSeed) ||
            plan.regions.Count < 3 ||
            plan.settlements.Count < 2 ||
            plan.encampments.Count < 2)
        {
            return false;
        }

        YQWorldGenerationService worldGeneration =
            YQWorldGenerationService.Instance;

        if (worldGeneration != null &&
            worldGeneration.IsRequestInFlight)
        {
            return false;
        }

        return true;
    }

    private static string BuildPlanKey(
        GeneratedWorldPlanRecord plan)
    {
        if (plan == null)
            return string.Empty;

        return
            Safe(
                plan.worldSeed,
                string.Empty) +
            "|" +
            Safe(
                plan.source,
                string.Empty) +
            "|" +
            Safe(
                plan.generatorPromptHash,
                string.Empty) +
            "|" +
            plan.regions.Count +
            "|" +
            plan.settlements.Count +
            "|" +
            plan.encampments.Count;
    }

    private static GeneratedSettlementRecord FindSettlement(
        GeneratedWorldPlanRecord plan,
        string settlementId)
    {
        if (plan == null ||
            plan.settlements == null ||
            string.IsNullOrWhiteSpace(
                settlementId))
        {
            return null;
        }

        for (int i = 0;
             i < plan.settlements.Count;
             i++)
        {
            GeneratedSettlementRecord settlement =
                plan.settlements[i];

            if (settlement != null &&
                string.Equals(
                    settlement.settlementId,
                    settlementId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return settlement;
            }
        }

        return null;
    }

    private static GeneratedEncampmentRecord FindEncampment(
        GeneratedWorldPlanRecord plan,
        string encampmentId)
    {
        if (plan == null ||
            plan.encampments == null ||
            string.IsNullOrWhiteSpace(
                encampmentId))
        {
            return null;
        }

        for (int i = 0;
             i < plan.encampments.Count;
             i++)
        {
            GeneratedEncampmentRecord encampment =
                plan.encampments[i];

            if (encampment != null &&
                string.Equals(
                    encampment.encampmentId,
                    encampmentId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return encampment;
            }
        }

        return null;
    }

    private static GeneratedRegionRecord FindRegion(
        GeneratedWorldPlanRecord plan,
        string regionId)
    {
        if (plan == null ||
            plan.regions == null ||
            string.IsNullOrWhiteSpace(
                regionId))
        {
            return null;
        }

        for (int i = 0;
             i < plan.regions.Count;
             i++)
        {
            GeneratedRegionRecord region =
                plan.regions[i];

            if (region != null &&
                string.Equals(
                    region.regionId,
                    regionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return region;
            }
        }

        return null;
    }



    private static string BuildExistingCanonicalNameConstraint(
    List<GeneratedNpcPlanRecord> existing,
    List<string> rejectedForCurrentBatch)
    {
        StringBuilder sb =
            new StringBuilder();

        /*
         * Keep accepted canonical names and rejected retry names in separate
         * sets.
         *
         * A collision name will normally exist in BOTH categories.
         *
         * That is intentional:
         *
         * ALREADY_USED_CANONICAL_NAMES
         *     explains why the name is unavailable globally.
         *
         * REJECTED_NAMES_FOR_THIS_LOCATION
         *     emphasizes that the model already failed with it during this
         *     exact location transaction.
         *
         * Never deduplicate one category against the other.
         */
        List<string> acceptedNames =
            new List<string>();

        HashSet<string> acceptedSeen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        List<string> rejectedNames =
            new List<string>();

        HashSet<string> rejectedSeen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        /*
         * Explicit component lists are much easier for the 4B model to obey
         * than asking it to mentally split dozens of complete names.
         */
        List<string> forbiddenFirstNames =
            new List<string>();

        HashSet<string> forbiddenFirstSeen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        List<string> forbiddenSurnames =
            new List<string>();

        HashSet<string> forbiddenSurnameSeen =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        if (existing != null)
        {
            for (int i = 0;
                 i < existing.Count;
                 i++)
            {
                GeneratedNpcPlanRecord npc =
                    existing[i];

                if (npc == null ||
                    string.IsNullOrWhiteSpace(
                        npc.displayName))
                {
                    continue;
                }

                string name =
                    npc.displayName.Trim();

                if (!acceptedSeen.Add(
                        name))
                {
                    continue;
                }

                acceptedNames.Add(
                    name);

                string[] parts =
                    name.Split(
                        new[]
                        {
                        ' '
                        },
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    string firstName =
                        parts[0].Trim();

                    if (!string.IsNullOrWhiteSpace(
                            firstName) &&
                        forbiddenFirstSeen.Add(
                            firstName))
                    {
                        forbiddenFirstNames.Add(
                            firstName);
                    }
                }

                if (parts.Length > 1)
                {
                    string surname =
                        parts[
                            parts.Length - 1]
                            .Trim();

                    if (!string.IsNullOrWhiteSpace(
                            surname) &&
                        forbiddenSurnameSeen.Add(
                            surname))
                    {
                        forbiddenSurnames.Add(
                            surname);
                    }
                }
            }
        }

        if (rejectedForCurrentBatch != null)
        {
            for (int i = 0;
                 i < rejectedForCurrentBatch.Count;
                 i++)
            {
                string name =
                    rejectedForCurrentBatch[i];

                if (string.IsNullOrWhiteSpace(
                        name))
                {
                    continue;
                }

                name =
                    name.Trim();

                /*
                 * IMPORTANT:
                 *
                 * This is deliberately NOT checked against acceptedSeen.
                 *
                 * "Mirel Vey" can and should appear in both the accepted-world
                 * list and this location's rejected list.
                 */
                if (rejectedSeen.Add(
                        name))
                {
                    rejectedNames.Add(
                        name);
                }

                string[] parts =
                    name.Split(
                        new[]
                        {
                        ' '
                        },
                        StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    string firstName =
                        parts[0].Trim();

                    if (!string.IsNullOrWhiteSpace(
                            firstName) &&
                        forbiddenFirstSeen.Add(
                            firstName))
                    {
                        forbiddenFirstNames.Add(
                            firstName);
                    }
                }

                if (parts.Length > 1)
                {
                    string surname =
                        parts[
                            parts.Length - 1]
                            .Trim();

                    if (!string.IsNullOrWhiteSpace(
                            surname) &&
                        forbiddenSurnameSeen.Add(
                            surname))
                    {
                        forbiddenSurnames.Add(
                            surname);
                    }
                }
            }
        }

        sb.AppendLine();

        sb.AppendLine(
            "WORLD_IDENTITY_UNIQUENESS");

        sb.AppendLine(
            "All names and name components below are NEGATIVE CONSTRAINTS only.");

        sb.AppendLine(
            "Do not imitate them, remix them, respell them, swap their components, or treat them as naming examples.");

        sb.AppendLine(
            "Do not reuse any complete name.");

        sb.AppendLine(
            "Unless explicit family, clan, dynasty, inherited-title, or other relationship context is supplied, do not reuse a forbidden first name or surname.");

        sb.AppendLine(
            "Avoid near-duplicates made by changing only one spelling, prefix, suffix, or syllable.");

        sb.AppendLine();

        sb.AppendLine(
            "ALREADY_USED_CANONICAL_NAMES");

        if (acceptedNames.Count == 0)
        {
            sb.AppendLine(
                "- <none yet>");
        }
        else
        {
            for (int i = 0;
                 i < acceptedNames.Count;
                 i++)
            {
                sb.Append(
                    "- ");

                sb.AppendLine(
                    acceptedNames[i]);
            }
        }

        sb.AppendLine();

        sb.AppendLine(
            "REJECTED_NAMES_FOR_THIS_LOCATION");

        sb.AppendLine(
            "These names failed during earlier attempts for THIS SAME location and remain forbidden for every later retry.");

        if (rejectedNames.Count == 0)
        {
            sb.AppendLine(
                "- <none yet>");
        }
        else
        {
            for (int i = 0;
                 i < rejectedNames.Count;
                 i++)
            {
                sb.Append(
                    "- ");

                sb.AppendLine(
                    rejectedNames[i]);
            }
        }

        /*
         * This is the key improvement for the small local model.
         *
         * Instead of requiring it to infer that:
         *
         *     Dorin Kael
         *
         * means both "Dorin" and "Kael" are unavailable, spell that out
         * explicitly.
         */
        sb.AppendLine();

        sb.AppendLine(
            "FORBIDDEN_NAME_COMPONENTS");

        sb.AppendLine(
            "The following components are unavailable unless supplied canon explicitly establishes a family/clan relationship.");

        sb.AppendLine();

        sb.AppendLine(
            "FORBIDDEN_FIRST_NAMES");

        if (forbiddenFirstNames.Count == 0)
        {
            sb.AppendLine(
                "- <none yet>");
        }
        else
        {
            for (int i = 0;
                 i < forbiddenFirstNames.Count;
                 i++)
            {
                sb.Append(
                    "- ");

                sb.AppendLine(
                    forbiddenFirstNames[i]);
            }
        }

        sb.AppendLine();

        sb.AppendLine(
            "FORBIDDEN_SURNAMES");

        if (forbiddenSurnames.Count == 0)
        {
            sb.AppendLine(
                "- <none yet>");
        }
        else
        {
            for (int i = 0;
                 i < forbiddenSurnames.Count;
                 i++)
            {
                sb.Append(
                    "- ");

                sb.AppendLine(
                    forbiddenSurnames[i]);
            }
        }

        sb.AppendLine();

        sb.AppendLine(
            "MANDATORY NAME SELF-CHECK");

        sb.AppendLine(
            "- Before output, silently split every proposed displayName into its first-name and surname components.");

        sb.AppendLine(
            "- Compare the proposed first name against FORBIDDEN_FIRST_NAMES.");

        sb.AppendLine(
            "- Compare the proposed surname against FORBIDDEN_SURNAMES.");

        sb.AppendLine(
            "- If either component is forbidden, discard that candidate name and create a substantially different one BEFORE writing the JSON.");

        sb.AppendLine(
            "- Do this check for EVERY generated NPC, not only the first NPC.");

        sb.AppendLine(
            "- Do not output a forbidden candidate and expect the validator to repair it.");

        sb.AppendLine(
            "- Generate entirely new name stems instead.");

        sb.AppendLine();

        sb.AppendLine(
            "Generate entirely new personal identities derived from the current location's culture, faction, environment, occupation, and circumstances.");

        return
            sb.ToString();
    }

    private static string BuildRetryCorrectionConstraint(
        string rejectionReason,
        List<string> rejectedCanonicalNames)
    {
        if (string.IsNullOrWhiteSpace(
                rejectionReason))
        {
            return string.Empty;
        }

        string reason =
            rejectionReason
                .Replace(
                    "\r",
                    " ")
                .Replace(
                    "\n",
                    " ")
                .Trim();

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine();

        sb.AppendLine(
            "RETRY_CORRECTION");

        sb.AppendLine(
            "The immediately previous response for THIS SAME physical location was rejected.");

        sb.AppendLine(
            "You are correcting that failed response, not repeating it.");

        sb.AppendLine(
            "PREVIOUS_REJECTION_REASON:");

        sb.AppendLine(
            reason);

        if (reason.IndexOf(
                "expected exactly",
                StringComparison.OrdinalIgnoreCase) >= 0 &&
            reason.IndexOf(
                "but received",
                StringComparison.OrdinalIgnoreCase) >= 0)
        {
            sb.AppendLine();

            sb.AppendLine(
                "CRITICAL ARRAY-LENGTH FAILURE:");

            sb.AppendLine(
                "- The previous response used the wrong generatedNpcs array length.");

            sb.AppendLine(
                "- Do not repeat the previous array length.");

            sb.AppendLine(
                "- Finish every required NPC object before closing generatedNpcs.");

            sb.AppendLine(
                "- The FINAL_OUTPUT_CHECK below is authoritative.");
        }

        if (IsNameCollisionFailure(
                reason))
        {
            List<string> collisionNames =
                ExtractSingleQuotedValues(
                    reason);

            sb.AppendLine();

            sb.AppendLine(
                "CRITICAL NAME COLLISION:");

            if (collisionNames.Count > 0)
            {
                for (int i = 0;
                     i < collisionNames.Count;
                     i++)
                {
                    string duplicateName =
                        collisionNames[i];

                    sb.AppendLine(
                        "- DO NOT output '" +
                        duplicateName +
                        "' again.");

                    string[] parts =
                        duplicateName.Split(
                            new[]
                            {
                            ' '
                            },
                            StringSplitOptions.RemoveEmptyEntries);

                    for (int partIndex = 0;
                         partIndex < parts.Length;
                         partIndex++)
                    {
                        sb.AppendLine(
                            "- Do not reuse the name component '" +
                            parts[partIndex] +
                            "' for the replacement identity.");
                    }
                }
            }

            sb.AppendLine(
                "- Replace collided identities with substantially different first-name and surname stems.");
        }

        if (rejectedCanonicalNames != null &&
            rejectedCanonicalNames.Count > 0)
        {
            sb.AppendLine();

            sb.AppendLine(
                "CUMULATIVE_REJECTED_NAMES_FOR_THIS_LOCATION");

            sb.AppendLine(
                "Every name below has already failed during an earlier attempt for this same physical location.");

            sb.AppendLine(
                "ALL of them remain forbidden, not merely the name from the immediately previous attempt.");

            HashSet<string> emitted =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < rejectedCanonicalNames.Count;
                 i++)
            {
                string rejectedName =
                    rejectedCanonicalNames[i];

                if (string.IsNullOrWhiteSpace(
                        rejectedName))
                {
                    continue;
                }

                rejectedName =
                    rejectedName.Trim();

                if (!emitted.Add(
                        rejectedName))
                {
                    continue;
                }

                sb.Append(
                    "- ");

                sb.AppendLine(
                    rejectedName);
            }
        }

        if (rejectedCanonicalNames != null &&
    rejectedCanonicalNames.Count > 0)
        {
            sb.AppendLine();

            sb.AppendLine(
                "CUMULATIVE_REJECTED_NAMES_FOR_THIS_LOCATION");

            sb.AppendLine(
                "Every name below was rejected during an earlier attempt for THIS SAME physical location.");

            sb.AppendLine(
                "ALL of these names remain forbidden.");

            HashSet<string> emittedRejectedNames =
                new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase);

            for (int i = 0;
                 i < rejectedCanonicalNames.Count;
                 i++)
            {
                string rejectedName =
                    rejectedCanonicalNames[i];

                if (string.IsNullOrWhiteSpace(
                        rejectedName))
                {
                    continue;
                }

                rejectedName =
                    rejectedName.Trim();

                if (!emittedRejectedNames.Add(
                        rejectedName))
                {
                    continue;
                }

                sb.Append(
                    "- ");

                sb.AppendLine(
                    rejectedName);
            }
        }

        sb.AppendLine();

        sb.AppendLine(
            "MANDATORY RETRY RULES");

        sb.AppendLine(
    "- Fix the stated rejection reason before returning the replacement response.");

        sb.AppendLine(
            "- Do not repeat ANY rejected identity from ANY earlier attempt for this location.");

        sb.AppendLine(
            "- Do not lightly respell, shorten, extend, remix, reverse, or syllable-swap a rejected name.");

        sb.AppendLine(
            "- Do not treat rejected names, accepted canonical names, forbidden first names, or forbidden surnames as naming examples.");

        sb.AppendLine(
            "- FORBIDDEN_FIRST_NAMES and FORBIDDEN_SURNAMES are literal exclusion lists.");

        sb.AppendLine(
            "- Before writing each displayName, silently compare its first name and surname against those exclusion lists.");

        sb.AppendLine(
            "- If either component matches a forbidden component, discard that candidate BEFORE output.");

        sb.AppendLine(
            "- A candidate such as 'Dorin Kael' is invalid if either 'Dorin' or 'Kael' appears in the forbidden component lists, even if that complete name was not the immediately previous collision.");

        sb.AppendLine(
            "- Choose substantially different phonetic stems, not another variation from the same small naming family.");

        sb.AppendLine(
            "- Do not substitute Kaelen/Kaela for Kael, Mirel/Mirek for Mire, Sorin/Soren for Sorn, or similar one-syllable mutations of forbidden material.");

        sb.AppendLine(
            "- Re-derive each identity from this location's culture, faction, environment, role, occupation, and circumstances.");

        sb.AppendLine(
            "- Check EVERY NPC name in the response, including otherwise valid NPCs.");

        sb.AppendLine(
            "- All earlier transaction, location, count, hostility, and JSON rules still apply.");

        sb.AppendLine(
            "- This replacement must pass WORLD_IDENTITY_UNIQUENESS before output.");

        return
            sb.ToString();
    }

    // ============================================================
    // STRING / ID HELPERS
    // ============================================================

    private static string ExtractFirstJsonObject(
        string raw)
    {
        if (string.IsNullOrWhiteSpace(
                raw))
        {
            return string.Empty;
        }

        string text =
            raw.Trim();

        int start =
            text.IndexOf('{');

        int end =
            text.LastIndexOf('}');

        return
            start >= 0 &&
            end > start
                ? text.Substring(
                    start,
                    end - start + 1)
                : string.Empty;
    }

    private static bool TryParseRootIgnoringBrokenGoddessVoice(
        string json,
        out JObject root,
        out string error)
    {
        root =
            null;

        error =
            string.Empty;

        try
        {
            root =
                JObject.Parse(
                    json);

            return true;
        }
        catch (Exception firstException)
        {
            // note: Optional generated Goddess commentary cannot be allowed to reject valid NPC records.
            if (!TryRemoveJsonProperty(
                    json,
                    "goddessVoice",
                    out string repairedJson))
            {
                error =
                    firstException.Message;

                return false;
            }

            try
            {
                root =
                    JObject.Parse(
                        repairedJson);

                return true;
            }
            catch (Exception secondException)
            {
                error =
                    secondException.Message;

                return false;
            }
        }
    }

    private static bool TryRemoveJsonProperty(
        string json,
        string propertyName,
        out string repairedJson)
    {
        repairedJson =
            json;

        if (string.IsNullOrWhiteSpace(
                json) ||
            string.IsNullOrWhiteSpace(
                propertyName))
        {
            return false;
        }

        string marker =
            "\"" +
            propertyName +
            "\"";

        int nameIndex =
            json.IndexOf(
                marker,
                StringComparison.Ordinal);

        if (nameIndex < 0)
            return false;

        int colonIndex =
            json.IndexOf(
                ':',
                nameIndex + marker.Length);

        if (colonIndex < 0)
            return false;

        int valueStart =
            colonIndex + 1;

        while (valueStart < json.Length &&
               char.IsWhiteSpace(
                   json[valueStart]))
        {
            valueStart++;
        }

        int valueEnd =
            FindJsonValueEnd(
                json,
                valueStart);

        if (valueEnd <= valueStart)
            return false;

        int removeStart =
            nameIndex;

        while (removeStart > 0 &&
               char.IsWhiteSpace(
                   json[removeStart - 1]))
        {
            removeStart--;
        }

        if (removeStart > 0 &&
            json[removeStart - 1] == ',')
        {
            removeStart--;
        }

        int removeEnd =
            valueEnd;

        while (removeEnd < json.Length &&
               char.IsWhiteSpace(
                   json[removeEnd]))
        {
            removeEnd++;
        }

        if (removeEnd < json.Length &&
            json[removeEnd] == ',')
        {
            removeEnd++;
        }

        // note: Remove only the presentation property; canonical NPC data remains untouched.
        repairedJson =
            json.Remove(
                removeStart,
                removeEnd - removeStart);

        return true;
    }

    // note: Require actual functional separation in the two tutorial-scale towns without dictating the LLM's names, personalities, or narrative details.
    private static bool HasReferenceSettlementRoleCoverage(
        List<GeneratedNpcPlanRecord> records,
        out string error)
    {
        error = string.Empty;
        bool hasService = false;
        bool hasGuard = false;
        bool hasQuestLead = false;

        for (int i = 0;
             i < records.Count;
             i++)
        {
            GeneratedNpcPlanRecord npc = records[i];
            string role = NormalizeKey(npc != null ? npc.role : string.Empty);

            hasService |= npc != null &&
                          (npc.merchant ||
                           RoleContainsAny(role,
                               "merchant",
                               "trader",
                               "vendor",
                               "shop",
                               "inn",
                               "smith",
                               "apothec",
                               "tailor",
                               "stable",
                               "locksmith",
                               "banker"));

            hasGuard |= npc != null &&
                        (npc.guard ||
                         RoleContainsAny(role,
                             "guard",
                             "warden",
                             "watch",
                             "captain"));

            hasQuestLead |= npc != null &&
                            (npc.notable ||
                             RoleContainsAny(role,
                                 "guide",
                                 "chief",
                                 "reeve",
                                 "mayor",
                                 "elder",
                                 "scout",
                                 "scribe",
                                 "scholar",
                                 "healer",
                                 "quest"));
        }

        if (hasService && hasGuard && hasQuestLead)
            return true;

        error =
            "reference settlement roster needs separate service/shop, guard, and notable local-work roles";

        return false;
    }

    // note: Role keyword matching stays local to validation; it never interprets narrative text as gameplay behavior.
    private static bool RoleContainsAny(
        string role,
        params string[] keywords)
    {
        string normalized =
            NormalizeKey(role);

        for (int i = 0;
             i < keywords.Length;
             i++)
        {
            if (normalized.Contains(
                    NormalizeKey(
                        keywords[i])))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindJsonValueEnd(
        string json,
        int start)
    {
        if (start < 0 ||
            start >= json.Length)
        {
            return -1;
        }

        char open =
            json[start];

        if (open != '{' &&
            open != '[')
        {
            int simpleEnd =
                start;

            while (simpleEnd < json.Length &&
                   json[simpleEnd] != ',' &&
                   json[simpleEnd] != '}')
            {
                simpleEnd++;
            }

            return simpleEnd;
        }

        char close =
            open == '{'
                ? '}'
                : ']';

        bool inString =
            false;

        bool escaped =
            false;

        int depth =
            0;

        for (int i = start;
             i < json.Length;
             i++)
        {
            char c =
                json[i];

            if (escaped)
            {
                escaped =
                    false;

                continue;
            }

            if (c == '\\')
            {
                escaped =
                    inString;

                continue;
            }

            if (c == '"')
            {
                inString =
                    !inString;

                continue;
            }

            if (inString)
                continue;

            if (c == open)
            {
                depth++;
            }
            else if (c == close)
            {
                depth--;

                if (depth <= 0)
                    return i + 1;
            }
        }

        return -1;
    }

    private static uint StableHash32(
        string value)
    {
        const uint offsetBasis =
            2166136261u;

        const uint prime =
            16777619u;

        uint hash =
            offsetBasis;

        if (value == null)
            return hash;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            hash ^=
                (byte)(
                    c &
                    0xFF);

            hash *=
                prime;

            hash ^=
                (byte)(
                    (c >> 8) &
                    0xFF);

            hash *=
                prime;
        }

        return hash;
    }

    private static string NormalizeKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        char[] chars =
            value.Trim()
                .ToLowerInvariant()
                .ToCharArray();

        for (int i = 0;
             i < chars.Length;
             i++)
        {
            char c =
                chars[i];

            if (!char.IsLetterOrDigit(
                    c) &&
                c != '_')
            {
                chars[i] =
                    '_';
            }
        }

        return
            new string(
                chars)
                .Trim('_');
    }

    private static List<string> NormalizeStringList(
        List<string> values,
        int maximum,
        int maximumLength)
    {
        List<string> result =
            new List<string>();

        if (values == null ||
            maximum <= 0)
        {
            return result;
        }

        for (int i = 0;
             i < values.Count &&
             result.Count < maximum;
             i++)
        {
            string value =
                TrimTo(
                    values[i],
                    maximumLength);

            if (string.IsNullOrWhiteSpace(
                    value))
            {
                continue;
            }

            AddUnique(
                result,
                value);
        }

        return result;
    }

    private static void AddUnique(
        List<string> values,
        string value)
    {
        if (values == null ||
            string.IsNullOrWhiteSpace(
                value))
        {
            return;
        }

        for (int i = 0;
             i < values.Count;
             i++)
        {
            if (string.Equals(
                    values[i],
                    value,
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        values.Add(
            value);
    }

    private static string JoinCompact(
        List<string> values)
    {
        if (values == null ||
            values.Count == 0)
        {
            return "<none>";
        }

        return
            string.Join(
                ", ",
                values);
    }

    private static string Safe(
        string value,
        string fallback)
    {
        return
            string.IsNullOrWhiteSpace(
                value)
                ? fallback
                : value.Trim();
    }

    private static string TrimTo(
        string value,
        int maximum)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        string trimmed =
            value.Trim();

        if (maximum <= 0 ||
            trimmed.Length <= maximum)
        {
            return trimmed;
        }

        return
            trimmed.Substring(
                0,
                maximum)
                .Trim();
    }

    private static string EscapeJson(
        string value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return string.Empty;
        }

        return
            value
                .Replace(
                    "\\",
                    "\\\\")
                .Replace(
                    "\"",
                    "\\\"");
    }

    private static string BuildFinalCountConstraint(
        int expectedNpcCount)
    {
        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine();

        sb.AppendLine(
            "FINAL_OUTPUT_CHECK");

        sb.AppendLine(
            "THIS IS THE LAST AND HIGHEST-PRIORITY STRUCTURAL CHECK BEFORE OUTPUT.");

        sb.AppendLine(
            "- generatedNpcs MUST contain EXACTLY " +
            expectedNpcCount +
            " complete NPC objects.");

        sb.AppendLine(
            "- Count the generatedNpcs objects silently before closing the array.");

        if (expectedNpcCount > 1)
        {
            sb.AppendLine(
                "- " +
                (expectedNpcCount - 1) +
                " objects is WRONG.");
        }

        sb.AppendLine(
            "- " +
            (expectedNpcCount + 1) +
            " objects is WRONG.");

        sb.AppendLine(
            "- Do not close generatedNpcs until all " +
            expectedNpcCount +
            " NPC objects have been fully written.");

        sb.Append(
            "- Silent slot checklist: ");

        for (int i = 1;
             i <= expectedNpcCount;
             i++)
        {
            if (i > 1)
                sb.Append(", ");

            sb.Append(
                "NPC_" +
                i);
        }

        sb.AppendLine(".");

        sb.AppendLine(
            "- Every silent slot above corresponds to one complete object in generatedNpcs.");

        sb.AppendLine(
            "- Do not output the slot labels themselves.");

        sb.AppendLine(
            "- After the exact NPC count is complete, output the goddessVoice object required by OUTPUT_SCHEMA.");

        sb.AppendLine(
            "- goddessVoice must contain completion, nextPrelude, and ambientLines; presentation remains optional to canonical acceptance.");

        sb.AppendLine(
            "- Then close the root JSON object.");

        sb.AppendLine(
            "- Perform all checking silently. Return JSON only.");

        return
            sb.ToString();
    }

    private static string TruncateForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        const int maxChars = 1600;
        if (value.Length <= maxChars)
            return value;

        // note: NPC batches can be very large; rejection logs stay bounded to protect Play Mode responsiveness.
        return value.Substring(0, maxChars) +
               "\n... <truncated " +
               (value.Length - maxChars) +
               " chars>";
    }
}
