using System;
using System.Collections.Generic;
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

    private static readonly Queue<string> GeneratedDialogueBuffer =
        new Queue<string>();

    private static bool _lastSelectionWasGenerated;

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
        "\"completion\":\"1-2 sentence reaction to the completed world plan\"," +
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

    public static bool LastSelectionWasGenerated =>
        _lastSelectionWasGenerated;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        ResetForNewGeneration();
    }

    public static void ResetForNewGeneration()
    {
        // note: Reset only Goddess presentation history; canonical generation state remains untouched.
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
        _censoredLineSerial =
            0;

        _lastSelectionWasGenerated =
            false;
    }

    public static string BeginOpeningDialogue(
        string generationKey)
    {
        // note: Connection status is system UI; the Goddess does not speak until the first accepted model response.
        _lastSelectionWasGenerated = false;
        return string.Empty;
    }

    // ============================================================
    // ORIGIN
    // ============================================================

    public static void SetOriginVoice(
        YQGoddessGenerationVoiceDto voice)
    {
        Normalize(
            voice);

        if (voice == null)
            return;

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
    }

    public static bool TryTakeBufferedLine(
        out string line)
    {
        while (GeneratedDialogueBuffer.Count > 0)
        {
            // note: QueueGeneratedLines already validates and length-limits each line; dequeueing the accepted value avoids repeating normalization and phrase scans on the presentation frame.
            line = GeneratedDialogueBuffer.Dequeue();

            if (!string.IsNullOrWhiteSpace(line))
            {
                _lastSelectionWasGenerated = true;
                return true;
            }
        }

        line = string.Empty;
        return false;
    }

    private static void QueueGeneratedLines(
        string[] lines)
    {
        if (lines == null)
            return;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = Clean(lines[i], 700);
            if (!string.IsNullOrWhiteSpace(line))
                GeneratedDialogueBuffer.Enqueue(line);
        }
    }

    private static void QueueGeneratedLine(
        string line)
    {
        // note: Single generated transitions use the same acceptance gate and FIFO order as ambient model-authored thoughts.
        string clean =
            Clean(
                line,
                700);

        if (!string.IsNullOrWhiteSpace(clean))
            GeneratedDialogueBuffer.Enqueue(clean);
    }

    public static string TakeOriginTransition(
        string fallback)
    {
        return Take(
            ref _originTransition,
            fallback);
    }

    // ============================================================
    // WORLD PLAN
    // ============================================================

    public static void SetWorldVoice(
        YQGoddessGenerationVoiceDto voice)
    {
        Normalize(
            voice);

        if (voice == null)
            return;

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

        if (voice.locations == null)
            return;

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

    public static string TakeWorldCompletion(
        string fallback)
    {
        return Take(
            ref _worldCompletion,
            fallback);
    }

    public static string Terrain(
        string fallback)
    {
        return Prefer(
            _terrain,
            fallback);
    }

    public static string Environment(
        string fallback)
    {
        return Prefer(
            _environment,
            fallback);
    }

    public static string PopulationMaterialization(
        string fallback)
    {
        return Prefer(
            _populationMaterialization,
            fallback);
    }

    public static string Reveal(
        string fallback)
    {
        return Take(
            ref _reveal,
            fallback);
    }

    public static string Settlement(
        string locationId,
        string locationName,
        string fallback)
    {
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
                fallback);
        }

        // note: A missing optional voice field still names the exact persisted settlement instead of injecting generic filler.
        return string.Empty;
    }

    public static string Buildings(
        string locationId,
        string locationName,
        string fallback)
    {
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
                fallback);
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

    public static string TerrainReadout(
        GeneratedWorldPlanRecord plan)
    {
        string generated = Prefer(_terrain, string.Empty);

        if (!string.IsNullOrWhiteSpace(generated))
            return generated;

        return string.Empty;
    }

    public static string EnvironmentReadout(
        GeneratedWorldPlanRecord plan)
    {
        string generated = Prefer(_environment, string.Empty);

        if (!string.IsNullOrWhiteSpace(generated))
            return generated;

        return string.Empty;
    }

    public static string PopulationReadout(
        GeneratedWorldPlanRecord plan)
    {
        string generated = Prefer(_populationMaterialization, string.Empty);

        if (!string.IsNullOrWhiteSpace(generated))
            return generated;

        return string.Empty;
    }

    public static string RevealReadout(
        GeneratedWorldPlanRecord plan)
    {
        string generated = Take(ref _reveal, string.Empty);

        if (!string.IsNullOrWhiteSpace(generated))
            return generated;

        return string.Empty;
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

    public static string TakeNpcPrelude(
        string fallback)
    {
        return Take(
            ref _nextNpcPrelude,
            fallback);
    }

    // ============================================================
    // PROMPT CONTRACTS
    // ============================================================

    public static string BuildBasicVoiceContract(
        string currentStage,
        string nextKnownStage)
    {
        // note: Local models follow one compact noncontradictory rail more reliably, and every NPC batch avoids re-ingesting the former multi-page style essay.
        return
            "\n\nGODDESS_VOICE_CONTRACT\n" +
            "This contract applies only to optional goddessVoice presentation. Canonical root JSON remains mandatory; omit voice before any canonical field.\n" +
            "CURRENT_CONFIRMED_OPERATION:\n" +
            PromptSafe(currentStage) + "\n" +
            "NEXT_CONFIRMED_OPERATION:\n" +
            PromptSafe(nextKnownStage) + "\n\n" +
            "FACT AND TIME RULES:\n" +
            "- Speak in first person. Never narrate the Goddess in third person.\n" +
            "- Use only facts supplied in this request or NPC facts created in this same response. Beliefs, rumors, fears, and private concerns belong to their NPC; never promote them into objective truth.\n" +
            "- Never invent causes, secrets, ancient explanations, future people, future events, or connections between unrelated concerns. Omit the thought instead.\n" +
            "- completion describes the accepted current result as physically becoming true now. Mention no more than two concrete observations.\n" +
            "- nextPrelude refers only to supplied NEXT_CONFIRMED_OPERATION facts and predicts no unknown result.\n\n" +
            "VOICE:\n" +
            // note: The Goddess is an earnest amateur performer whose grand divine act keeps cracking under visible world-building mistakes.
            "- She is an underqualified young world operator roleplaying as an ancient, omnipotent Goddess. She badly wants the player to believe the performance.\n" +
            "- Begin most major thoughts with a short, overconfident divine proclamation, ceremonial phrase, or invented grand title; then let a concrete mistake force an immediate correction.\n" +
            "- Her claimed omnipotence and visible process must contradict each other: she declares a perfect mountain while lowering it, blesses a road while moving a building off it, or calls a retry a divine revision.\n" +
            "- She is not secretly smooth or fully in control. She guesses, notices problems late, revises herself mid-sentence, and disguises improvisation as sacred intent.\n" +
            "- She remains sympathetic: frantic effort comes from wanting the player safe. She never blames, belittles, threatens, or resents the player.\n" +
            "- Use one clean roleplay crack per line: a clipped 'no', an interrupted proclamation, a quiet count, an unconvincing denial, or a hurried practical correction tied to the current operation.\n" +
            "- Allow clumsy ceremonial language such as 'behold', 'by my decree', 'witness', or 'thus I ordain', but vary it and never write polished mystical poetry.\n" +
            "- The comedy comes from the gap between divine confidence and observable incompetence, not punchlines, memes, sarcasm, random glitches, or generic panic.\n" +
            "- Do not use coder slang or name hidden software machinery. Describe visible world work: lowering hills, clearing roads, turning houses, attaching memories, or moving trees.\n" +
            "- Do not open with a command. Never tell the player to look, wait, hold still, breathe, calm down, ignore something, inspect their feet, or hesitate.\n" +
            "- Address the player directly only when a supplied gameplay fact requires one usable instruction.\n" +
            "- Never mention generation, generated, stage, phase, response, dataset, validation, canonical, prompt, JSON, AI, model, code, Unity, or algorithm.\n" +
            "- Do not imitate or reference an existing game character.\n\n" +
            "OUTPUT:\n" +
            "- goddessVoice is an object inside the required root object: {\"completion\":\"...\",\"nextPrelude\":\"...\",\"ambientLines\":[\"...\"]}.\n" +
            "- completion: 18-65 words. nextPrelude: 14-50 words. ambientLines: requested count, each 8-22 words.\n" +
            "- Every line must use different sentence machinery and at least one concrete noun from its supplied facts. Prefer two spoken beats: attempted divinity, then practical correction.\n" +
            "- Never use stock lines such as 'it is done', 'the world takes shape', 'as it should be', or any attention-command variant.\n";
    }

    private static string BuildArchivedVerboseVoiceContract(
    string currentStage,
    string nextKnownStage)
    {
        // note: Archived reference only; the compact contract above is the sole runtime prompt rail.
        return
            "\n\nGODDESS_VOICE_CONTRACT\n" +

            "This contract applies ONLY to goddessVoice. " +
            "Goddess dialogue is presentation and must NEVER create new canonical lore.\n\n" +

            "CANONICAL OUTPUT PRIORITY:\n" +

            "- The full root JSON schema remains mandatory.\n" +

            "- goddessVoice is optional presentation inside that root object.\n" +

            "- If anything is difficult, omit or simplify goddessVoice before omitting canonical fields.\n" +

            "- Returning only goddessVoice is invalid.\n\n" +

            "PRESENTATION BUDGET:\n" +
            "- Include stage-grounded ambientLines for the following known operation; never use generic filler.\n" +
            "- Use the stage-specific count when supplied; otherwise provide 4-8 concise unique lines.\n\n" +

            "CURRENT_LOCATION_FACTS:\n" +
            PromptSafe(
                currentStage) +
            "\n\n" +

            "NEXT_LOCATION_FACTS:\n" +
            PromptSafe(
                nextKnownStage) +
            "\n\n" +

            // ---------------------------------------------------------
            // POV
            // ---------------------------------------------------------

            "POINT OF VIEW — ABSOLUTE RULE:\n" +

            "- The Goddess speaks in FIRST PERSON.\n" +

            "- Prefer 'I'.\n" +

            "- 'We' is allowed only when she explicitly means herself and the player together.\n" +

            "- NEVER describe the Goddess as 'she', 'her', 'the Goddess', 'the observer', " +
            "'the entity', or any other third-person narrator.\n" +

            "- NEVER write narration such as 'she observes', 'she notes', " +
            "'she will determine', or 'she wonders'.\n\n" +

            // ---------------------------------------------------------
            // Epistemic firewall
            // ---------------------------------------------------------

            "EPISTEMIC FIREWALL:\n" +

            "The Goddess has enormous knowledge, but this dialogue may reveal ONLY " +
            "facts already supplied in this request or facts explicitly created in " +
            "generatedNpcs in THIS SAME response.\n\n" +

            "Treat information in three categories:\n\n" +

            "A — CONFIRMED FACTS:\n" +
            "- supplied location name\n" +
            "- supplied region\n" +
            "- settlement type\n" +
            "- population\n" +
            "- security\n" +
            "- market\n" +
            "- services\n" +
            "- explicit terrain/climate data\n" +
            "- NPC name\n" +
            "- NPC occupation\n" +
            "- NPC appearance\n" +
            "- NPC routine\n" +
            "- other directly generated NPC attributes\n\n" +

            "B — CHARACTER-LEVEL INFORMATION:\n" +
            "- localKnowledge\n" +
            "- privateConcern\n" +
            "- rumors\n" +
            "- suspicions\n" +
            "- fears\n" +
            "- reported observations\n\n" +

            "Category B describes what THAT PERSON believes, fears, reports, or has noticed. " +
            "It is NOT automatically objective truth about the world.\n\n" +

            "C — UNKNOWN INFORMATION:\n" +
            "- causes\n" +
            "- hidden connections\n" +
            "- ancient explanations\n" +
            "- secret factions\n" +
            "- diseases not explicitly established\n" +
            "- supernatural mechanisms\n" +
            "- future NPCs\n" +
            "- future events\n" +
            "- links between unrelated NPC concerns\n\n" +

            "Category C MUST NOT be invented by goddessVoice.\n\n" +

            // ---------------------------------------------------------
            // Completion
            // ---------------------------------------------------------

            "COMPLETION RULES:\n" +

            "- React to what has JUST become concrete.\n" +

            "- Mention at most TWO concrete observations.\n" +

            "- Prefer useful people: leaders, merchants, guards, specialists, service providers, " +
            "or unusually consequential NPCs.\n" +

            "- Mention at most ONE NPC by name unless two people have an explicit relationship.\n" +

            "- Do not summarize the entire settlement.\n" +

            "- Do not combine multiple NPC concerns into a theory.\n" +

            "- Do not discover a hidden pattern merely because two NPC records contain similar words.\n" +

            "- If no individual is especially important, discuss the settlement itself instead.\n\n" +

           // ---------------------------------------------------------
           // Prelude
           // ---------------------------------------------------------

           "NEXT PRELUDE RULES:\n" +

"- nextPrelude concerns ONLY NEXT_LOCATION_FACTS.\n" +

"- Once completion is finished, mentally discard CURRENT_LOCATION_FACTS before writing nextPrelude.\n" +

"- Do NOT carry any current-location NPC, rumor, privateConcern, localKnowledge, theory, " +
"hazard, mystery, illness, artifact, disappearance, environmental symptom, or causal idea " +
"into nextPrelude.\n" +

"- Do NOT use CURRENT settlement NPC information to predict the next settlement.\n" +

"- Do NOT invent inhabitants of the next location.\n" +

"- Do NOT invent events, problems, mysteries, causes, shortages, conflicts, or lore " +
"that are not explicitly present in NEXT_LOCATION_FACTS.\n" +

"- Refer only to already-known next-location properties such as its name, region, size, " +
"security, services, economy, terrain, climate, or threat classification.\n" +

"- If NEXT_LOCATION_FACTS contain no interesting detail, make a dry observation about " +
"one of those confirmed facts rather than inventing something more dramatic.\n" +

"- Speak in FIRST PERSON as though I am turning my attention toward that place.\n" +

"- Never write 'she will', 'she observes', 'she notes', 'the Goddess will', " +
"'the next phase', or similar third-person or workflow language.\n\n" +

            // ---------------------------------------------------------
            // Unknown information / glitch
            // ---------------------------------------------------------

            "UNINTELLIGIBLE DIVINE INFORMATION:\n" +

            "Sometimes the Goddess knows something that the mortal player is not capable " +
            "of understanding yet.\n\n" +

            "When a sentence would otherwise require an UNSUPPLIED causal mechanism, divine term, " +
            "metaphysical relation, ancient proper noun, or other Category C information, you may " +
            "replace ONLY that missing concept with a short corrupted fragment instead of inventing lore.\n\n" +

            "Example STRUCTURE only:\n" +

            "\"The eastern foundation is failing because of ⟦▒∅⟁█⟧. " +
            "You can continue calling it erosion for now.\"\n\n" +

            "The corruption means: information exists, but the mortal listener cannot parse it.\n\n" +

            "- Use corruption rarely.\n" +
            "- Maximum one corruption fragment per line.\n" +
            "- Keep the surrounding sentence understandable.\n" +
            "- Never explain the corrupted term afterward.\n" +
            "- Never use corruption merely for decoration.\n" +
            "- Never use corruption to hide a contradiction you invented yourself.\n" +

            "Possible character families:\n" +
            "⟦ ⟧ ∅ ⟁ ░ ▒ ▓ ◊ ʘ Æ █ ╫ ∴\n\n" +

            // ---------------------------------------------------------
            // Tone
            // ---------------------------------------------------------

            // note: This is the core Goddess voice rail for generated presentation text.
            "VOICE:\n" +

            "- An original young machine-Goddess: exact, dry, emotionally guarded, and frightened that one missed dependency will harm the player.\n" +

            "- Her intelligence appears as specific decisions and corrections, never as claims that she is clever.\n" +

            "- Helping the player is her primary motive. Concern escapes through an over-specific safety check, a self-correction, or one briefly unfinished thought.\n" +

            "- Her public voice is restrained and almost formal. Anxiety makes it tighter and more precise, not louder, cuter, or more theatrical.\n" +

            "- Dryness is allowed. Punchlines, meme cadence, petulant quips, whimsical metaphors, and attempts to sound quotable are not.\n" +

            "- She may criticize a concrete malformed result or admit that a system is resisting her. She does not insult the player for existing.\n" +

            "- She is currently seating roads, rejecting collisions, reconciling identities, stabilizing terrain, and protecting access routes. Name the relevant work plainly.\n" +

            "- Technical language must identify a supplied operation or visible consequence. Never produce vague machine-jargon atmosphere.\n" +

            "- She is not mystical, whimsical, manic, chatty, or performatively sarcastic.\n" +

            "- She does not turn every observation into poetry, a joke, a threat, or a lesson.\n" +

            "- She does not speak like a QA report, narrator, customer-service assistant, trailer voice, or generic computer diagnostic.\n" +

            "- Do not imitate, quote, name, or directly reference any existing game character. Keep this as the YourQuest Goddess.\n\n" +

            "THOUGHT STRUCTURE — HARD REQUIREMENT:\n" +
            "- Write each major spoken thought as one to three compact sentences forming one continuous present-tense observation.\n" +
            "- Beat 1: state one concrete condition or decision from the supplied operation. Never begin with an attention command.\n" +
            "- Beat 2: say what I am doing about it now and why that protects playability or coherence.\n" +
            "- Optional beat 3: allow one restrained concern or self-correction, then stop. Do not append a joke or catchphrase.\n" +
            "- Treat the accepted result as physically becoming true NOW: roads are settling, identities are taking hold, doors are clearing their frames.\n" +
            "- Never announce that work already ended and never predict an unknown result. Describe the last accepted result as the present operation while the next request runs.\n" +
            "- The anxious fracture must arise from the CURRENT supplied operation, never generic panic pasted onto any line.\n" +
            "- Do not repeat sentence machinery, trailing ellipses, rhetorical questions, or recovery phrases between outputs.\n\n" +

            "PLAYER-DIRECTION LIMITS — HARD REQUIREMENT:\n" +
            "- Do not tell the player to look, wait, hold still, breathe, remain calm, hesitate, ignore something, avoid looking, or inspect their feet.\n" +
            "- Do not open with 'Hold', 'Wait', 'Look', 'Listen', 'Do not', 'Don't', 'Just', or a similar attention-grabbing imperative.\n" +
            "- Address the player directly only when a supplied gameplay fact requires a usable instruction. State that instruction once and plainly.\n" +
            "- Never create fake urgency with commands unrelated to an actual player action.\n\n" +

            "PLAYER QUESTIONNAIRE AWARENESS:\n" +

            "- If GODDESS_QUESTIONNAIRE_PRESENTATION_CONTEXT is supplied, use it as optional presentation evidence.\n" +

            "- Prefer one pointed observation over a summary of every answer.\n" +

            "- Notice obvious nonsense, refusal, repetition, extremely short answers, unusually long answers, and recurring themes when relevant.\n" +

            "- Do not over-punish sincere answers, unusual names, slang, or non-English-looking text.\n" +

            "- Never let questionnaire commentary alter canonical facts or promise future generated results.\n" +

            "- Do not copy answer text unless it is short enough to quote cleanly.\n\n" +

            // ---------------------------------------------------------
            // Language bans
            // ---------------------------------------------------------

            "AVOID ABSTRACT ANALYSIS LANGUAGE:\n" +

            "Avoid phrases such as:\n" +

            "- 'a pattern emerges'\n" +
            "- 'aligns with'\n" +
            "- 'correlation suggests'\n" +
            "- 'structural response'\n" +
            "- 'regional stability'\n" +
            "- 'resource dependency'\n" +
            "- 'known local pressures'\n" +
            "- 'observed patterns'\n" +
            "- 'emerging pattern'\n" +
            "- 'hidden pattern'\n" +
            "- 'similar attention'\n" +
            "- 'signs indicate'\n" +
            "- 'that is not normal'\n" +
            "- 'something is wrong'\n" +
            "- 'records repeat'\n" +
            "- 'this suggests'\n" +
            "- 'will determine whether'\n" +
            "- 'will note whether'\n" +

            "These sound like analysis reports rather than spoken dialogue.\n\n" +

            "NEVER mention:\n" +
            "- generation\n" +
            "- generated\n" +
            "- stage\n" +
            "- phase\n" +
            "- response\n" +
            "- dataset\n" +
            "- validation\n" +
            "- canonical\n" +
            "- prompt\n" +
            "- JSON\n" +
            "- AI\n" +
            "- model\n" +
            "- code\n" +
            "- Unity\n" +
            "- algorithm\n\n" +

            // ---------------------------------------------------------
            // Output shape
            // ---------------------------------------------------------
            "JSON SHAPE — ABSOLUTE RULE:\n" +

"- goddessVoice MUST be a JSON OBJECT, never a string.\n" +

"- goddessVoice MUST appear only as a field inside the full required root JSON object.\n" +

"- NEVER return a root object containing only goddessVoice.\n" +

"- completion MUST be inside goddessVoice.\n" +

"- nextPrelude MUST be inside goddessVoice.\n" +

"- NEVER place nextPrelude at the root of the response.\n" +

"- ambientLines, when present, MUST be inside goddessVoice.\n" +

"- Required shape: " +
"\"goddessVoice\":{\"completion\":\"...\",\"nextPrelude\":\"...\",\"ambientLines\":[\"...\",\"...\"]}\n\n" +
            "LENGTH:\n" +

            "- Vary structure: sometimes one sentence, sometimes two to four muttered sentences.\n" +
            "- completion: normally 24-90 words.\n" +
            "- nextPrelude: normally 18-70 words.\n" +
            "- ambientLines: use the stage-specific count, each normally 8-26 words.\n" +

            "- Shorter is preferable to inventing connective lore.\n\n" +

            "TARGET BEHAVIOR — THESE ARE STRUCTURAL EXAMPLES ONLY; DO NOT COPY THEM:\n\n" +

            "GOOD:\n" +
            "\"The western road is seated, but its last turn still enters the market boundary. I am moving the boundary now. You were not going to arrive inside a wall.\"\n\n" +

            "GOOD:\n" +
            "\"The reeve holds the gate and the scribe holds the names. I am fixing both records in place before I turn to the next district. They need to remain themselves when I do.\"\n\n" +

           "GOOD UNKNOWN-INFORMATION HANDLING:\n" +
"\"The inscription uses ⟦∴▒╫∅⟧ notation. " +
"Your language has no equivalent. I am preserving the mark without assigning it a meaning.\"\n\n" +

            "BAD:\n" +
            "\"The villagers' concerns reveal an emerging structural pattern linking illness to ancient stone.\"\n\n" +

            "BAD:\n" +
            "\"She observes that regional instability aligns with known resource flows.\"\n\n" +

            "BAD:\n" +
            "Any line built from attention commands, hollow reassurance, or physical directions unrelated to a supplied gameplay action.\n";
    }

    public static string BuildWorldVoiceContract(
        int expectedSettlementCount)
    {
        return
            "\n\nGODDESS_WORLD_VOICE_CONTRACT\n" +

            "This contract applies ONLY to goddessVoice. " +
            "Do not change canonical world facts to accommodate the dialogue.\n\n" +

            "The world plan in THIS SAME JSON response is the only source of truth for goddessVoice.\n" +

            "goddessVoice.completion may react to the world plan that was just produced.\n" +

            "goddessVoice.terrain may discuss physically shaping the generated terrain.\n" +

            "goddessVoice.environment may discuss physically dressing the generated wilderness.\n" +

            "goddessVoice.populationPrelude may discuss the NEXT operation: creating canonical inhabitants for the already-generated locations. " +
            "It MUST NOT invent those inhabitants yet.\n" +

            "goddessVoice.populationMaterialization may discuss placing already-completed canonical inhabitants into physical reality.\n" +

            "goddessVoice.reveal is the final line before the mortal player enters the completed world.\n\n" +

            "goddessVoice.locations MUST contain exactly " +
            expectedSettlementCount +
            " objects: one for every settlement produced in this same response.\n" +

            "Each locations[].locationId MUST exactly match one generated settlementId.\n" +

            "Do not invent additional location IDs.\n" +

            "settlementMaterialization and buildingMaterialization may reference facts already present in that settlement or its region.\n\n" +

            BuildBasicVoiceContract(
                "The complete canonical world plan has just been authored.",
                "Physical world materialization followed by canonical NPC location batches.");
    }

    // ============================================================
    // NORMALIZATION
    // ============================================================

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

    private static string Take(
        ref string value,
        string fallback)
    {
        string result =
            Prefer(
                value,
                fallback);

        value =
            string.Empty;

        return result;
    }

    private static string Prefer(
        string value,
        string fallback)
    {
        string clean =
            string.IsNullOrWhiteSpace(
                value)
                ? string.Empty
                : value.Trim();

        _lastSelectionWasGenerated =
            TryRememberGeneratedLine(
                clean);

        return
            _lastSelectionWasGenerated
                ? clean
                : string.Empty;
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

    private static string FormatLocation(
        string value,
        string locationName,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return Fallback(
                fallback);
        }

        string safeLocation =
            string.IsNullOrWhiteSpace(
                locationName)
                ? "this place"
                : locationName.Trim();

        string formatted =
            value
                .Replace(
                    "{location}",
                    safeLocation)
                .Replace(
                    "{0}",
                    safeLocation);

        _lastSelectionWasGenerated =
            TryRememberGeneratedLine(
                formatted);

        return
            _lastSelectionWasGenerated
                ? formatted
                : string.Empty;
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
                "remain calm",
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

    private static bool TryRememberGeneratedLine(
        string value)
    {
        if (!IsGeneratedGoddessLineAllowed(
                value))
        {
            return false;
        }

        bool isCensoredLine =
            ContainsCensorGlyphs(
                value);

        string key =
            NormalizeGeneratedLineKey(
                value);

        if (string.IsNullOrWhiteSpace(
                key) ||
            !UsedGeneratedLineKeys.Add(
                key))
        {
            return false;
        }

        string cadenceKey =
            BuildGeneratedCadenceKey(
                key);

        if (!isCensoredLine &&
            !string.IsNullOrWhiteSpace(
                cadenceKey) &&
            !UsedGeneratedCadenceKeys.Add(
                cadenceKey))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsCensorGlyphs(
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
