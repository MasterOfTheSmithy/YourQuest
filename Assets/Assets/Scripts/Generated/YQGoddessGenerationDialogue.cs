using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public sealed class YQGoddessGenerationVoiceDto
{
    /*
     * Generic request/response voice fields.
     *
     * completion:
     *     Reacts ONLY to the result produced by the current LLM request.
     *
     * nextPrelude:
     *     Talks about the next engine-known operation without predicting
     *     its unknown generated result.
     */
    public string completion;
    public string nextPrelude;

    /*
     * Short generated interstitial lines used while the next request runs.
     */
    public string[] ambientLines;

    /*
     * World-plan-only presentation.
     */
    public string terrain;
    public string environment;

    /*
     * Spoken before canonical NPC batching begins.
     */
    public string populationPrelude;

    /*
     * Spoken when canonical identities are physically instantiated.
     */
    public string populationMaterialization;

    /*
     * Final reveal.
     */
    public string reveal;

    /*
     * Exact generated settlement-specific physical-world narration.
     */
    public YQGoddessLocationVoiceDto[] locations;
}

[Serializable]
public sealed class YQGoddessLocationVoiceDto
{
    public string locationId;

    public string settlementMaterialization;

    public string buildingMaterialization;
}

public static class YQGoddessGenerationDialogue
{
    /*
     * Presentation state only.
     *
     * NONE of this participates in:
     *
     * - world seeds
     * - NPC IDs
     * - canonical save data
     * - terrain generation
     * - faction generation
     */
    private static string _originTransition =
        string.Empty;

    private static string _worldCompletion =
        string.Empty;

    private static string _nextNpcPrelude =
        string.Empty;

    private static string _terrain =
        string.Empty;

    private static string _environment =
        string.Empty;

    private static string _populationMaterialization =
        string.Empty;

    private static string _reveal =
        string.Empty;

    private static int _censoredLineSerial;

    private sealed class PendingSpeech
    {
        public string draft, task, locationId;
        public YQGoddessSpeechPlan.Purpose purpose;
        public bool reviewed;
    }
    private static readonly Queue<PendingSpeech> GeneratedDialogueBuffer = new Queue<PendingSpeech>();
    private static readonly HashSet<string> QueuedDraftKeys = new HashSet<string>(StringComparer.Ordinal);
    private static readonly Dictionary<string, string> DeliveryLocations = new Dictionary<string, string>(StringComparer.Ordinal);
    private static YQRepairEpisode _loadingReviewEpisode;

    private static bool _lastSelectionWasGenerated;
    private static int _voiceOwnerEpoch = -1;
    private static string _voiceOwnerProfileId = string.Empty;
    private static string _voiceOwnerWorldId = string.Empty;

    private static readonly HashSet<string> UsedGeneratedLineKeys =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> UsedGeneratedCadenceKeys =
        new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

    private static readonly string[] HollowPlayerImperativeOpenings =
    {
        "hold.",
        "hold ",
        "wait.",
        "wait ",
        "look.",
        "look ",
        "listen.",
        "listen ",
        "don't look",
        "do not look",
        "do not move",
        "don't move",
        "just wait",
        "just look"
    };

    private static readonly Dictionary<
        string,
        YQGoddessLocationVoiceDto>
        LocationVoice =
            new Dictionary<
                string,
                YQGoddessLocationVoiceDto>(
                    StringComparer.OrdinalIgnoreCase);

    /*
     * Used by OriginGeneration and individual NPC batch requests.
     */
    public const string BasicJsonSchema =
    "{" +
    "\"completion\":\"spoken Goddess line\"," +
    "\"nextPrelude\":\"spoken Goddess line\"," +
    "\"ambientLines\":[\"spoken Goddess line\"]" +
    "}";

    /*
     * Used by WorldPlanGeneration.
     *
     * locations must correspond to the settlements generated in the
     * SAME response.
     */
    public const string WorldJsonSchema =
        "{" +
        "\"completion\":\"original response to the accepted world design\"," +
        "\"terrain\":\"Goddess line spoken while terrain is physically materialized\"," +
        "\"environment\":\"Goddess line spoken while wilderness and environment are materialized\"," +
        "\"populationPrelude\":\"Goddess line before canonical inhabitants begin being generated\"," +
        "\"populationMaterialization\":\"Goddess line while completed canonical inhabitants are physically instantiated\"," +
        "\"reveal\":\"final Goddess line immediately before the player is allowed into the completed world\"," +
        "\"ambientLines\":[\"short grounded Goddess line\"]," +
        "\"locations\":[" +
        "{" +
        "\"locationId\":\"exact generated settlementId\"," +
        "\"settlementMaterialization\":\"line spoken while this exact settlement is placed\"," +
        "\"buildingMaterialization\":\"line spoken while this exact settlement's buildings are placed\"" +
        "}" +
        "]" +
        "}";

    public static bool LastSelectionWasGenerated
    {
        get { BindVoiceOwner(); return _lastSelectionWasGenerated; }
    }

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ResetForNewGeneration();
    }

    public static void ResetForNewGeneration()
    {
        // note: Reset only Goddess presentation history; canonical generation state remains untouched.
        _voiceOwnerEpoch = YQServiceLifecycle.RequestEpoch;
        _voiceOwnerProfileId = CurrentVoiceProfileId();
        _voiceOwnerWorldId = CurrentVoiceWorldId();
        _originTransition =
            string.Empty;

        _worldCompletion =
            string.Empty;

        _nextNpcPrelude =
            string.Empty;

        _terrain =
            string.Empty;

        _environment =
            string.Empty;

        _populationMaterialization =
            string.Empty;

        _reveal =
            string.Empty;

        LocationVoice.Clear();
        UsedGeneratedLineKeys.Clear();
        UsedGeneratedCadenceKeys.Clear();
        GeneratedDialogueBuffer.Clear();
        QueuedDraftKeys.Clear(); DeliveryLocations.Clear();
        if (_loadingReviewEpisode != null && !_loadingReviewEpisode.IsTerminal)
        {
            // note: Retire the exact presentation episode before a new profile or journey can supply its dialogue.
            _loadingReviewEpisode.Finish("Superseded"); LLMClient.Instance?.CancelRepairEpisode(_loadingReviewEpisode);
        }
        _loadingReviewEpisode = null;
        _censoredLineSerial =
            0;

        _lastSelectionWasGenerated =
            false;
    }

    private static string CurrentVoiceProfileId()
    {
        // note: Before profile selection is available, the existing active player document still identifies the presentation owner.
        string profileId = YQProfileSaveSystem.Instance?.ActiveProfileId;
        return !string.IsNullOrWhiteSpace(profileId) ? profileId : PlayerStateManager.Instance?.state?.playerId ?? string.Empty;
    }

    private static void BindVoiceOwner()
    {
        // note: Static loading speech cannot survive a profile, world or session switch, even when journeys reuse location IDs.
        if (_voiceOwnerEpoch != YQServiceLifecycle.RequestEpoch ||
            !string.Equals(_voiceOwnerProfileId, CurrentVoiceProfileId(), StringComparison.Ordinal) ||
            !string.Equals(_voiceOwnerWorldId, CurrentVoiceWorldId(), StringComparison.Ordinal))
            ResetForNewGeneration();
    }

    private static string CurrentVoiceWorldId() => WorldStateManager.Instance?.State?.worldIdentity?.worldId ?? string.Empty;

    private static JObject CaptureSpokenEvidence(string locationId = null)
    {
        // note: Delivery uses the current accepted documents, never the uncommitted result that originally supplied a voice bundle.
        WorldState world = WorldStateManager.Instance?.State;
        JObject evidence = YQGoddessLoadingVoice.CaptureKnownContext(PlayerStateManager.Instance?.state, world);
        if (!string.IsNullOrWhiteSpace(locationId) && world?.generatedWorldPlan?.settlements != null)
            foreach (GeneratedSettlementRecord settlement in world.generatedWorldPlan.settlements)
                if (settlement != null && string.Equals(settlement.settlementId, locationId, StringComparison.OrdinalIgnoreCase))
                {
                    evidence["facts"]["plan.location.name"] = settlement.displayName;
                    break;
                }
        return evidence;
    }

    public static JObject CaptureDeliveryEvidence(string line)
    {
        DeliveryLocations.TryGetValue(line ?? string.Empty, out string locationId);
        return CaptureSpokenEvidence(locationId);
    }

    public static string BeginOpeningDialogue(
        string generationKey)
    {
        // note: Connection status is system UI; the Goddess does not speak until the first accepted model response.
        _lastSelectionWasGenerated = false;
        return string.Empty;
    }

    public static YQGoddessGenerationVoiceDto EnsureOriginVoice(YQGoddessGenerationVoiceDto voice, PlayerState player, YQOriginGenerationDto origin)
    {
        // note: Preserve the authored proposal; normalization cannot replace it with a preset sentence.
        voice ??= new YQGoddessGenerationVoiceDto(); Normalize(voice); return voice;
    }

    public static YQGoddessGenerationVoiceDto EnsureWorldVoice(YQGoddessGenerationVoiceDto voice, GeneratedWorldPlanRecord plan, PlayerState player)
    {
        // note: Speech acceptance is separate from canonical world acceptance and happens after records are committed.
        voice ??= new YQGoddessGenerationVoiceDto(); Normalize(voice); return voice;
    }

    public static bool IsSpokenVoiceFieldAcceptable(
        string value,
        int minimumWords)
    {
        // note: Sentence length and pronouns follow the exchange. Relevance, knowledge and character are assessed by independent review.
        return CountWords(value) > 0 && !HasPresentationMachinery(value) && IsGeneratedGoddessLineAllowed(value);
    }

    private static bool HasPresentationMachinery(string value)
    {
        // note: A first-person sentence can still be a leaked engine report. Reject clear technical tokens and stage directions without policing ordinary emotional language.
        string trimmed = (value ?? string.Empty).Trim();
        if (trimmed.StartsWith("*", StringComparison.Ordinal) ||
            trimmed.StartsWith("[", StringComparison.Ordinal) || trimmed.StartsWith("{", StringComparison.Ordinal))
            return true;
        for (int index = 0; index < trimmed.Length;)
        {
            if (!char.IsLetter(trimmed[index])) { index++; continue; }
            int start = index;
            while (index < trimmed.Length && char.IsLetter(trimmed[index])) index++;
            string token = trimmed.Substring(start, index - start).ToLowerInvariant();
            switch (token)
            {
                case "prefab": case "prefabs": case "navmesh": case "json": case "llm":
                case "collider": case "colliders": case "gameobject": case "serialized":
                case "pipeline": case "compile": case "staging": case "hotloading":
                case "ai": case "ollama": case "unity": case "algorithm": case "dataset":
                case "validation": case "canonical": case "generation": case "generated":
                case "model": case "code": case "director": case "queue":
                    return true;
            }
        }
        return false;
    }

    private static bool ContainsDirectAddress(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        // note: Token boundaries avoid treating scenery words such as courtyard as speech to the player.
        for (int index = 0; index < value.Length; index++)
        {
            if (index > 0 && char.IsLetter(value[index - 1]))
                continue;
            int end = index;
            while (end < value.Length && char.IsLetter(value[end])) end++;
            int length = end - index;
            if ((length == 3 && string.Compare(value, index, "you", 0, 3, StringComparison.OrdinalIgnoreCase) == 0) ||
                (length == 4 && string.Compare(value, index, "your", 0, 4, StringComparison.OrdinalIgnoreCase) == 0))
                return true;
            index = end;
        }
        return false;
    }

    public static void SetOriginVoice(
        YQGoddessGenerationVoiceDto voice)
    {
        BindVoiceOwner();
        Normalize(
            voice);

        if (voice == null)
        {
            Debug.LogWarning(
                "[YQGoddessGenerationDialogue] ORIGIN VOICE BUNDLE MISSING. " +
                "Canonical origin may continue, but no model-authored introduction was accepted.");
            return;
        }

        // note: Speak the accepted result by itself; the following operation can begin while that line is still typing.
        _originTransition =
            Clean(
                voice.completion,
                700);

        // note: The prelude remains a separate queued thought so past and future operations never become one muddled paragraph.
        QueueGeneratedLine(
            voice.nextPrelude);

        QueueGeneratedLines(
            voice.ambientLines);

        ReportOriginVoiceQuality(
            _originTransition,
            voice.nextPrelude);

        Debug.Log(
            "[YQGoddessGenerationDialogue] ORIGIN VOICE BUFFERED FOR DELIVERY VALIDATION\n" +
            "Completion words: " + CountWords(_originTransition) + "\n" +
            "Prelude words: " + CountWords(voice.nextPrelude) + "\n" +
            "Ambient thoughts: " + voice.ambientLines.Length);
    }

    public static bool TryTakeBufferedLine(out string line)
    {
        BindVoiceOwner(); line = string.Empty; _lastSelectionWasGenerated = false;
        if (_loadingReviewEpisode != null && !_loadingReviewEpisode.IsTerminal) return false;
        while (GeneratedDialogueBuffer.Count > 0)
        {
            PendingSpeech pending = GeneratedDialogueBuffer.Dequeue();
            JObject evidence = CaptureSpokenEvidence(pending.locationId);
            if (pending.reviewed)
            {
                if (!YQGoddessSpeech.IsApproved(pending.draft, evidence)) continue;
                // note: The screen applies the durable delivery gate after its cadence check, not when a queue item is selected.
                if (DeliveryLocations.Count >= 32) DeliveryLocations.Clear();
                DeliveryLocations[pending.draft] = pending.locationId;
                line = pending.draft; _lastSelectionWasGenerated = true; return true;
            }
            int epoch = _voiceOwnerEpoch;
            string profileId = _voiceOwnerProfileId, worldId = _voiceOwnerWorldId;
            bool Current() => epoch == YQServiceLifecycle.RequestEpoch && profileId == CurrentVoiceProfileId() && worldId == CurrentVoiceWorldId();
            void Complete(YQGoddessSpeech.Result result)
            {
                if (!Current() || !result.canDeliver) return;
                pending.draft = result.line; pending.reviewed = true;
                if (GeneratedDialogueBuffer.Count < 24) GeneratedDialogueBuffer.Enqueue(pending);
            }
            // note: Author every loading response anew. Persisted voice bundles and legacy samples cannot supply its wording.
            _loadingReviewEpisode = YQGoddessSpeech.Request(LLMClient.Instance, evidence, pending.task, "loading", Current, Complete, pending.purpose);
            return false;
        }
        return false;
    }

    private static void QueueGeneratedLines(string[] lines)
    {
        if (lines == null) return;
        foreach (string line in lines) if (!string.IsNullOrWhiteSpace(line)) QueueGeneratedLine(line);
    }

    private static void QueueGeneratedLine(string line, string task = null, string locationId = null,
        YQGoddessSpeechPlan.Purpose purpose = YQGoddessSpeechPlan.Purpose.Commentary)
    {
        if (string.IsNullOrWhiteSpace(line) && task == null) return;
        string request = task ?? "Offer one relevant original thought about accepted player or world facts while this journey is prepared. Omit unrelated commentary.";
        string draft = string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        string key = YQRepairEpisode.Hash(request + "|" + locationId + "|" + CaptureSpokenEvidence(locationId)["facts"]);
        // note: Polling a stage cannot enqueue repeated calls; the bounded queue contains only drafts, never preset substitutions.
        if (GeneratedDialogueBuffer.Count >= 24 || !QueuedDraftKeys.Add(key)) return;
        GeneratedDialogueBuffer.Enqueue(new PendingSpeech { draft = draft, task = request, locationId = locationId, purpose = purpose });
    }

    public static string TakeOriginTransition(string fallback)
    {
        return Take(ref _originTransition, fallback,
            "Welcome this particular player into their accepted origin. Respond to their chosen direction without inventing readiness, destiny or outcomes.",
            YQGoddessSpeechPlan.Purpose.Welcome);
    }

    public static void SetWorldVoice(
        YQGoddessGenerationVoiceDto voice)
    {
        BindVoiceOwner();
        Normalize(
            voice);

        if (voice == null)
        {
            Debug.LogWarning(
                "[YQGoddessGenerationDialogue] WORLD VOICE BUNDLE MISSING. " +
                "Canonical world may continue, but its authored loading narration was not accepted.");
            return;
        }

        _worldCompletion =
            Clean(
                voice.completion,
                700);

        _terrain =
            Clean(
                voice.terrain,
                700);

        _environment =
            Clean(
                voice.environment,
                700);

        _populationMaterialization =
            Clean(
                voice.populationMaterialization,
                700);

        _reveal =
            Clean(
                voice.reveal,
                700);

        /*
         * This becomes the preemptive line for NPC batch #1.
         */
        _nextNpcPrelude =
            Clean(
                voice.populationPrelude,
                700);

        // note: World-authored interstitials can cover population calls after the world response is accepted.
        QueueGeneratedLines(
            voice.ambientLines);

        LocationVoice.Clear();

        if (voice.locations != null)
        {
            for (int i = 0;
                 i < voice.locations.Length;
                 i++)
            {
                YQGoddessLocationVoiceDto location =
                    voice.locations[i];

                if (location == null)
                    continue;

                location.locationId =
                    Clean(
                        location.locationId,
                        180);

                location.settlementMaterialization =
                    Clean(
                        location.settlementMaterialization,
                        700);

                location.buildingMaterialization =
                    Clean(
                        location.buildingMaterialization,
                        700);

                if (string.IsNullOrWhiteSpace(
                        location.locationId))
                {
                    continue;
                }

                LocationVoice[
                    location.locationId] =
                        location;
            }
        }

        ReportWorldVoiceQuality(
            voice);

        Debug.Log(
            "[YQGoddessGenerationDialogue] WORLD VOICE BUFFERED FOR DELIVERY VALIDATION\n" +
            "Completion words: " + CountWords(_worldCompletion) + "\n" +
            "Terrain words: " + CountWords(_terrain) + "\n" +
            "Environment words: " + CountWords(_environment) + "\n" +
            "Population words: " + CountWords(_populationMaterialization) + "\n" +
            "Reveal words: " + CountWords(_reveal) + "\n" +
            "Ambient thoughts: " + voice.ambientLines.Length + "\n" +
            "Settlement voices accepted: " + LocationVoice.Count);
    }

    public static string TakeWorldCompletion(string fallback)
    {
        BindVoiceOwner(); return Take(ref _worldCompletion, fallback, "Respond to the accepted world design and what it offers this player. Do not assert that the physical world is ready.", YQGoddessSpeechPlan.Purpose.WorldDescription);
    }

    public static string Terrain(string fallback)
    {
        BindVoiceOwner(); return Prefer(_terrain, fallback, "Respond to the accepted terrain design while it is prepared, without inventing weather, safety or travel.", YQGoddessSpeechPlan.Purpose.WorldDescription);
    }

    public static string Environment(string fallback)
    {
        BindVoiceOwner(); return Prefer(_environment, fallback, "Respond to the accepted wilderness design while it is prepared. Distinguish design from live observation.", YQGoddessSpeechPlan.Purpose.WorldDescription);
    }

    public static string PopulationMaterialization(string fallback)
    {
        BindVoiceOwner(); return Prefer(_populationMaterialization, fallback, "Respond to the accepted inhabitants or objective relevant to this player. Do not invent secret motives or events.", YQGoddessSpeechPlan.Purpose.Commentary);
    }

    public static string Reveal(string fallback)
    {
        BindVoiceOwner(); return Take(ref _reveal, fallback, "Address the player about their accepted beginning before entry. Do not assert player release, readiness or future safety.", YQGoddessSpeechPlan.Purpose.Welcome);
    }

    public static string Settlement(
        string locationId,
        string locationName,
        string fallback)
    {
        BindVoiceOwner();
        if (!string.IsNullOrWhiteSpace(
                locationId) &&
            LocationVoice.TryGetValue(
                locationId,
                out YQGoddessLocationVoiceDto voice) &&
            voice != null)
        {
            return FormatLocation(
                voice.settlementMaterialization,
                locationName,
                fallback,
                locationId);
        }

        // note: A missing optional voice field still names the exact persisted settlement instead of injecting generic filler.
        return string.Empty;
    }

    public static string Buildings(
        string locationId,
        string locationName,
        string fallback)
    {
        BindVoiceOwner();
        if (!string.IsNullOrWhiteSpace(
                locationId) &&
            LocationVoice.TryGetValue(
                locationId,
                out YQGoddessLocationVoiceDto voice) &&
            voice != null)
        {
            return FormatLocation(
                voice.buildingMaterialization,
                locationName,
                fallback,
                locationId);
        }

        // note: Building work stays tied to the exact location record rather than a reusable sentence template.
        return string.Empty;
    }

    public static string Fallback(
        string fallback)
    {
        // note: The retired grab-bag never fills silence; the loading UI retains the last meaningful world record instead.
        _lastSelectionWasGenerated =
            false;

        return string.Empty;
    }

    public static void SetOriginReadout(
        YQOriginGenerationDto origin)
    {
        // note: Canonical origin data is never reformatted into counterfeit Goddess dialogue.
    }

    public static void SetWorldReadout(
        GeneratedWorldPlanRecord plan)
    {
        // note: Accepted records remain canonical data, but no formatter impersonates generated Goddess speech.
    }

    public static string TerrainReadout(GeneratedWorldPlanRecord plan)
    {
        // note: Compatibility readouts use the same confirmed-stage task and acceptance path.
        return Terrain(string.Empty);
    }

    public static string EnvironmentReadout(GeneratedWorldPlanRecord plan)
    {
        // note: Compatibility readouts use the same confirmed-stage task and acceptance path.
        return Environment(string.Empty);
    }

    public static string PopulationReadout(GeneratedWorldPlanRecord plan)
    {
        // note: Compatibility readouts use the same confirmed-stage task and acceptance path.
        return PopulationMaterialization(string.Empty);
    }

    public static string RevealReadout(GeneratedWorldPlanRecord plan)
    {
        // note: Compatibility readouts use the same confirmed-stage task and acceptance path.
        return Reveal(string.Empty);
    }

    // ============================================================
    // NPC BATCHES
    // ============================================================

    public static string Completion(
        YQGoddessGenerationVoiceDto voice,
        string fallback)
    {
        Normalize(
            voice);

        return
            voice != null
                ? Prefer(
                    voice.completion,
                    fallback)
                : Fallback(
                    fallback);
    }

    public static void SetNpcVoice(
        YQGoddessGenerationVoiceDto voice,
        bool includeCompletionInNextPrelude = false)
    {
        BindVoiceOwner();
        Normalize(
            voice);

        if (voice == null)
        {
            _nextNpcPrelude =
                string.Empty;

            return;
        }

        _nextNpcPrelude =
            includeCompletionInNextPrelude
                ? Clean(
                    voice.completion,
                    700)
                : Clean(
                    voice.nextPrelude,
                    700);

        if (includeCompletionInNextPrelude)
        {
            // note: The final population completion is spoken first; any model-authored placement transition follows as a distinct thought.
            QueueGeneratedLine(
                voice.nextPrelude);
        }

        // note: Each accepted NPC batch supplies the thought buffer for the following queued generation operation.
        QueueGeneratedLines(
            voice.ambientLines);
    }

    public static string TakeNpcPrelude(string fallback)
    {
        BindVoiceOwner(); return Take(ref _nextNpcPrelude, fallback, "Respond to the accepted player and world context as its inhabitants are prepared. A plan is not proof of their current activity.", YQGoddessSpeechPlan.Purpose.Commentary);
    }

    // ============================================================
    // PROMPT CONTRACTS
    // ============================================================

    public static string BuildBasicVoiceContract(string currentStage, string nextKnownStage, bool compactSpeaker = false)
    {
        // note: This build-shipped profile is the sole character contract for every generation role.
        return BuildSpeakerVoiceContract() + YQGoddessSpeechPlan.BuildLoadingCompositionContract() +
            "\nCONFIRMED_OPERATION_DATA\n" + JsonConvert.SerializeObject(new { currentStage, nextKnownStage }) +
            "\nPreserve the canonical root schema. Presentation fields may be empty when no relevant thought is available. " +
            "Do not treat data acceptance as physical readiness. The next operation predicts no outcome.\n";
    }

    public static string BuildSpeakerVoiceContract() => YQGoddessAgentProfile.Text + "\n";
    public static string BuildStructuredSpeakerVoiceContract() => BuildSpeakerVoiceContract();

    public static int VoiceSamplingSeed(string saveSeed)
    {
        // note: Use a stable presentation seed instead of Unity's global random state; new profile identity already participates in the supplied origin/world seed.
        unchecked
        {
            uint hash = 2166136261;
            foreach (char character in saveSeed ?? string.Empty)
                hash = (hash ^ character) * 16777619;
            return (int)(hash & 0x7fffffff);
        }
    }

    public static string BuildSaveVoiceVariationContract(string saveSeed)
    {
        // note: A save seed does not select a voice frame or consume canonical randomness. Context determines the response.
        return "\nCompose fresh dialogue from this player's actual choices and accepted records; use prior speech only to avoid repetition.\n";
    }

    public static string BuildWorldVoiceContract(int expectedSettlementCount)
    {
        return BuildBasicVoiceContract("A world design is being accepted for this player's chosen beginning.",
            "The accepted land and inhabitants will be prepared before entry.") +
            "\nEach location entry refers to exactly one settlement in this result. Use its exact settlementId. " +
            "Provide " + expectedSettlementCount + " location entries without inventing locations. " +
            "Terrain and environment fields concern the supplied design, not present weather, passage or player travel. " +
            "Population fields cannot invent inhabitants or their secrets. Reveal cannot assert physical readiness or future safety.\n";
    }

    public static void Normalize(
        YQGoddessGenerationVoiceDto voice)
    {
        if (voice == null)
            return;

        voice.completion =
            Clean(
                voice.completion,
                700);

        voice.nextPrelude =
            Clean(
                voice.nextPrelude,
                700);

        if (voice.ambientLines == null)
        {
            voice.ambientLines =
                Array.Empty<string>();
        }
        else
        {
            for (int i = 0;
                 i < voice.ambientLines.Length;
                 i++)
            {
                voice.ambientLines[i] =
                    Clean(
                        voice.ambientLines[i],
                        220);
            }
        }

        voice.terrain =
            Clean(
                voice.terrain,
                700);

        voice.environment =
            Clean(
                voice.environment,
                700);

        voice.populationPrelude =
            Clean(
                voice.populationPrelude,
                700);

        voice.populationMaterialization =
            Clean(
                voice.populationMaterialization,
                700);

        voice.reveal =
            Clean(
                voice.reveal,
                700);

        voice.locations ??=
            Array.Empty<
                YQGoddessLocationVoiceDto>();
    }

    private static string BuildRecordReadout(
        string category,
        string subject,
        string detail)
    {
        // note: Record-style narration exposes accepted deterministic facts without pretending an unknown result already exists.
        string safeCategory =
            Clean(category, 64).ToUpperInvariant();

        string safeSubject =
            Clean(subject, 180);

        string safeDetail =
            Clean(detail, 360);

        if (string.IsNullOrWhiteSpace(safeSubject))
            safeSubject = "unresolved record";

        string hiddenSignal =
            BuildGlitchBlock(
                safeCategory + "|" + safeSubject + "|" + safeDetail,
                12);

        return
            "I/O // " + safeCategory + "\n" +
            safeSubject + "\n" +
            safeDetail + "\n" +
            "[player-layer " + hiddenSignal + "]";
    }

    private static string CombineFacts(
        string first,
        string second)
    {
        if (string.IsNullOrWhiteSpace(first))
            return string.IsNullOrWhiteSpace(second)
                ? "canonical origin accepted"
                : second;

        return string.IsNullOrWhiteSpace(second)
            ? first
            : first + " | " + second;
    }

    private static string Take(ref string value, string fallback, string task = null,
        YQGoddessSpeechPlan.Purpose purpose = YQGoddessSpeechPlan.Purpose.Commentary)
    {
        BindVoiceOwner(); string result = Prefer(value, fallback, task, purpose); value = string.Empty; return result;
    }

    private static string Prefer(string value, string fallback, string task = null,
        YQGoddessSpeechPlan.Purpose purpose = YQGoddessSpeechPlan.Purpose.Commentary)
    {
        // note: Authored bundles remain untrusted until asynchronous review. Missing prose requests original writing, never filler.
        _lastSelectionWasGenerated = false;
        QueueGeneratedLine(value, task ?? "Address the player about a relevant accepted fact of the journey being prepared.", null, purpose);
        return string.Empty;
    }

    private static string Combine(
        string first,
        string second)
    {
        first =
            Clean(
                first,
                700);

        second =
            Clean(
                second,
                700);

        if (string.IsNullOrWhiteSpace(
                first))
        {
            return second;
        }

        if (string.IsNullOrWhiteSpace(
                second))
        {
            return first;
        }

        return
            first +
            "\n\n" +
            second;
    }

    private static string FormatLocation(string value, string locationName, string fallback, string locationId)
    {
        BindVoiceOwner(); _lastSelectionWasGenerated = false;
        // note: A caller label and placeholders cannot author speech; the exact accepted location supplies context for review or regeneration.
        QueueGeneratedLine(value, "Comment on the accepted design of this specific location while it is prepared. Do not assert readiness, unseen activity or completed travel.",
            locationId, YQGoddessSpeechPlan.Purpose.WorldDescription);
        return string.Empty;
    }

    private static string SanitizeFallbackLine(
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(
                fallback))
        {
            return string.Empty;
        }

        // note: The UI should never show an unfilled format slot even when a fallback caller forgot the location.
        return fallback
            .Replace(
                "{0}",
                "this place")
            .Replace(
                "{location}",
                "this place")
            .Trim();
    }

    private static string Clean(
        string value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        string clean =
            value.Trim();

        if (!IsGeneratedGoddessLineAllowed(
                clean))
        {
            // note: Rejected model prose becomes silence; fixed censor templates must never impersonate an Ollama-authored Goddess line.
            return string.Empty;
        }

        if (clean.Length <=
            maxLength)
        {
            return clean;
        }

        int cut =
            clean.LastIndexOf(
                ' ',
                Mathf.Max(
                    0,
                    maxLength - 2));

        // note: Keep the model's actual prose and trim at a word boundary; generated censor theatrics made ordinary length limits sound like canned characterization.
        return
            clean.Substring(
                    0,
                    cut > maxLength / 2
                        ? cut
                        : maxLength - 1)
                .TrimEnd(
                    ' ',
                    '.',
                    ',',
                    ';',
                    ':') +
            "...";
    }

    private static int CountWords(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        int words = 0;
        bool insideWord = false;
        for (int index = 0; index < value.Length; index++)
        {
            bool whitespace = char.IsWhiteSpace(value[index]);
            if (!whitespace && !insideWord)
                words++;
            insideWord = !whitespace;
        }

        // note: Allocation-free counts make dialogue degradation explicit in logs without copying player-specific generated prose into diagnostics.
        return words;
    }

    private static void ReportOriginVoiceQuality(
        string completion,
        string nextPrelude)
    {
        List<string> issues = new List<string>();
        AppendVoiceFieldIssue(issues, "completion", completion, 35);
        AppendVoiceFieldIssue(issues, "nextPrelude", nextPrelude, 25);
        if (issues.Count == 0)
            return;

        // note: Canonical origin data remains accepted, but voice drift is explicit in QA logs instead of silently presenting narrator prose as the Goddess.
        Debug.LogWarning(
            "[YQGoddessGenerationDialogue] ORIGIN VOICE QUALITY DRIFT: " +
            string.Join(", ", issues));
    }

    private static void ReportWorldVoiceQuality(
        YQGoddessGenerationVoiceDto voice)
    {
        List<string> issues = new List<string>();
        AppendVoiceFieldIssue(issues, "completion", voice.completion, 28);
        AppendVoiceFieldIssue(issues, "terrain", voice.terrain, 28);
        AppendVoiceFieldIssue(issues, "environment", voice.environment, 28);
        AppendVoiceFieldIssue(issues, "populationPrelude",
            voice.populationPrelude, 28);
        AppendVoiceFieldIssue(issues, "populationMaterialization",
            voice.populationMaterialization, 28);
        AppendVoiceFieldIssue(issues, "reveal", voice.reveal, 28);
        if (issues.Count == 0)
            return;

        // note: This diagnostic never rejects an otherwise valid persisted world plan; it identifies exactly which transient presentation fields ignored the speaker/length rail.
        Debug.LogWarning(
            "[YQGoddessGenerationDialogue] WORLD VOICE QUALITY DRIFT: " +
            string.Join(", ", issues));
    }

    private static void AppendVoiceFieldIssue(
        List<string> issues,
        string fieldName,
        string value,
        int minimumWords)
    {
        int wordCount = CountWords(value);
        bool hasFirstPerson = ContainsFirstPersonPronoun(value);
        if (IsSpokenVoiceFieldAcceptable(value, minimumWords))
            return;

        issues.Add(
            fieldName + " (" + wordCount + " words" +
            (hasFirstPerson ? string.Empty : ", no first-person speaker") +
            ")");
    }

    private static bool ContainsFirstPersonPronoun(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        for (int start = 0; start < value.Length;)
        {
            while (start < value.Length && !char.IsLetter(value[start]))
                start++;
            int end = start;
            while (end < value.Length && char.IsLetter(value[end]))
                end++;

            int length = end - start;
            if ((length == 1 &&
                 char.ToLowerInvariant(value[start]) == 'i') ||
                (length == 2 &&
                 char.ToLowerInvariant(value[start]) == 'm' &&
                 (char.ToLowerInvariant(value[start + 1]) == 'e' ||
                  char.ToLowerInvariant(value[start + 1]) == 'y')))
            {
                return true;
            }

            start = end + 1;
        }

        return false;
    }

    private static string BuildCensoredGoddessLine(
        string source,
        int maxLength)
    {
        string[] prefixes =
        {
            "[signal fracture // mortal layer]:",
            "<unresolved glyph packet>:",
            "[causal checksum refused]:",
            "// thought redacted by the bright side:",
            "<permission lattice desynchronized>:"
        };

        int serial =
            ++_censoredLineSerial;

        uint hash =
            StableHash32(
                (source ?? string.Empty) +
                "|censored-line|" +
                serial);

        string result =
            prefixes[
            (int)(hash %
                (uint)prefixes.Length)] +
            " " +
            BuildGlitchBlock(
                (source ?? string.Empty) +
                "|censored-block|" +
                serial,
                22);

        if (result.Length <=
            maxLength)
        {
            return result;
        }

        return
            result.Substring(
                0,
                Mathf.Max(
                    0,
                    maxLength));
    }

    private static string BuildGlitchBlock(
        string source,
        int length)
    {
        string[] glyphs =
        {
            "\u2588",
            "\u2593",
            "\u2592",
            "\u2591",
            "\u25A0",
            "\u25A1",
            "\u25CA",
            "\u2205"
        };

        uint hash =
            StableHash32(
                (source ?? string.Empty) +
                "|censor");

        char[] result =
            new char[
                Mathf.Max(
                    1,
                    length)];

        for (int i = 0;
             i < result.Length;
             i++)
        {
            // note: Deterministic-per-line censor glyphs keep forbidden thoughts readable as intentional corruption, not random UI failure.
            int index =
                (int)((hash +
                       (uint)(i *
                              17)) %
                      (uint)glyphs.Length);

            result[i] =
                glyphs[index][0];
        }

        string payload =
            new string(
                result);

        switch (hash % 4u)
        {
            case 0u:
                return "⟦" + payload + "⟧";

            case 1u:
                return "//" + payload + "::";

            case 2u:
                return "<" + payload + "/>";

            default:
                return "[" + payload + "]";
        }
    }

    private static uint StableHash32(
        string value)
    {
        unchecked
        {
            uint hash =
                2166136261u;

            if (!string.IsNullOrEmpty(
                    value))
            {
                for (int i = 0;
                     i < value.Length;
                     i++)
                {
                    // note: FNV-1a gives deterministic local variation without touching world-generation authority.
                    hash ^=
                        value[i];

                    hash *=
                        16777619u;
                }
            }

            return hash;
        }
    }

    private static bool IsGeneratedGoddessLineAllowed(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return false;
        }

        string normalized =
            value
                .Trim()
                .TrimStart(
                    '"',
                    '\'',
                    '>',
                    '-',
                    ' ')
                .ToLowerInvariant();

        // note: Reject only unmistakable prompt leakage, canned fantasy narration, and hollow player-direction; ordinary concrete vocabulary must remain available to the model.
        return
            !normalized.Contains("{0}") &&
            !normalized.Contains("{location}") &&
            !StartsWithHollowPlayerImperative(normalized) &&
            !ContainsAnyForbiddenPhrase(
                normalized,
                "don't look now",
                "dont look now",
                "do not look now",
                "look at your feet",
                "at your feet",
                "do not hesitate",
                "don't hesitate",
                "dont hesitate",
                "hold still",
                "prepare yourself",
                "brace yourself",
                "behold",
                "it is done",
                "the world takes shape",
                "as it should be",
                "your destiny",
                "fate awaits",
                "reality bends",
                "the threads of fate",
                "trust me",
                "everything is fine",
                "nothing to worry about",
                "the goddess says",
                "the goddess observes",
                "goddess voice",
                "goddessvoice",
                "json",
                "prompt",
                "language model",
                "ollama",
                "unity engine",
                "dataset",
                "canonical field");
    }

    private static bool ContainsAnyForbiddenPhrase(
        string normalized,
        params string[] phrases)
    {
        if (string.IsNullOrWhiteSpace(normalized) ||
            phrases == null)
        {
            return false;
        }

        for (int index = 0; index < phrases.Length; index++)
        {
            // note: Ordinal matching keeps the acceptance gate deterministic across local cultures and machines.
            if (!string.IsNullOrWhiteSpace(phrases[index]) &&
                normalized.IndexOf(
                    phrases[index],
                    StringComparison.Ordinal) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private static bool StartsWithHollowPlayerImperative(
        string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        for (int index = 0;
             index < HollowPlayerImperativeOpenings.Length;
             index++)
        {
            if (normalized.StartsWith(
                    HollowPlayerImperativeOpenings[index],
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool ContainsCensorGlyphs(
        string value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return false;
        }

        // note: Censored thoughts are allowed to share a grammar because their glyph stream is uniquely seeded per event.
        return
            value.IndexOf(
                '\u2588') >=
            0 ||
            value.IndexOf(
                '\u2593') >=
            0 ||
            value.IndexOf(
                '\u2592') >=
            0 ||
            value.IndexOf(
                '\u2591') >=
            0 ||
            value.IndexOf(
                '\u25A0') >=
            0 ||
            value.IndexOf(
                '\u25A1') >=
            0 ||
            value.IndexOf(
                '\u25CA') >=
            0 ||
            value.IndexOf(
                '\u2205') >=
            0;
    }

    private static string NormalizeGeneratedLineKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return
            CollapseGeneratedWhitespace(
                value
                    .Trim()
                    .Replace(
                        "\r",
                        " ")
                    .Replace(
                        "\n",
                        " ")
                    .ToLowerInvariant());
    }

    private static string BuildGeneratedCadenceKey(
        string normalized)
    {
        if (string.IsNullOrWhiteSpace(
                normalized))
        {
            return string.Empty;
        }

        string[] words =
            normalized.Split(
                new[]
                {
                    ' ',
                    ',',
                    '.',
                    ';',
                    ':',
                    '-',
                    '—',
                    '\'',
                    '"',
                    '(',
                    ')'
                },
                StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 5)
        {
            return string.Empty;
        }

        string boilerplateKey =
            BuildGeneratedBoilerplateKey(
                words);

        if (!string.IsNullOrWhiteSpace(
                boilerplateKey))
        {
            return boilerplateKey;
        }

        // note: Exact duplicate protection already runs above; only known boilerplate openings share a cadence key, so distinct model-authored thoughts are not silenced merely for starting similarly.
        return string.Empty;
    }

    private static string BuildGeneratedBoilerplateKey(
        string[] words)
    {
        if (words == null ||
            words.Length < 3)
        {
            return string.Empty;
        }

        // note: These openings became visible repetition when only the settlement/prop noun changed.
        if (StartsWithWords(
                words,
                "i",
                "ve",
                "noticed"))
        {
            return "opened:i_have_noticed";
        }

        if (StartsWithWords(
                words,
                "i",
                "have",
                "noticed"))
        {
            return "opened:i_have_noticed";
        }

        if (StartsWithWords(
                words,
                "i",
                "ve",
                "seen"))
        {
            return "opened:i_have_seen";
        }

        if (StartsWithWords(
                words,
                "i",
                "have",
                "seen"))
        {
            return "opened:i_have_seen";
        }

        if (StartsWithWords(
                words,
                "the",
                "same"))
        {
            return "opened:the_same";
        }

        if (StartsWithWords(
                words,
                "someone",
                "is") ||
            StartsWithWords(
                words,
                "someone",
                "s"))
        {
            return "opened:someone_is";
        }

        return string.Empty;
    }

    private static bool StartsWithWords(
        string[] words,
        params string[] prefix)
    {
        if (words == null ||
            prefix == null ||
            words.Length < prefix.Length)
        {
            return false;
        }

        for (int i = 0;
             i < prefix.Length;
             i++)
        {
            if (!string.Equals(
                    words[i],
                    prefix[i],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static string CollapseGeneratedWhitespace(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        string[] parts =
            value.Split(
                new[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries);

        return
            string.Join(
                " ",
                parts);
    }

    private static bool StartsWithWeakGeneratedOpening(
        string[] words)
    {
        if (words == null ||
            words.Length == 0)
        {
            return false;
        }

        return
            words[0] ==
                "this" ||
            words[0] ==
                "there" ||
            words[0] ==
                "another" ||
            words[0] ==
                "good" ||
            words[0] ==
                "now";
    }

    private static string PromptSafe(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return "<none>";
        }

        return
            value
                .Replace(
                    '\r',
                    ' ')
                .Replace(
                    '\n',
                    ' ')
                .Trim();
    }
}
