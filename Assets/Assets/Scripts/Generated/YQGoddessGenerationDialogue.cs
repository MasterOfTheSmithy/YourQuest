using System;
using System.Collections.Generic;
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

    private static readonly Queue<string> GeneratedDialogueBuffer =
        new Queue<string>();

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

    private static bool TryProveSpokenLine(string value, out string line, string locationId = null) =>
        YQGoddessSpeechPlan.TryReadSpokenComposition(value, CaptureSpokenEvidence(locationId), out line);

    public static string BeginOpeningDialogue(
        string generationKey)
    {
        // note: Connection status is system UI; the Goddess does not speak until the first accepted model response.
        _lastSelectionWasGenerated = false;
        return string.Empty;
    }

    public static YQGoddessGenerationVoiceDto EnsureOriginVoice(
        YQGoddessGenerationVoiceDto voice,
        PlayerState player,
        YQOriginGenerationDto origin)
    {
        voice ??= new YQGoddessGenerationVoiceDto();
        Normalize(voice);

        string playerName = Clean(
            player != null ? player.displayName : string.Empty,
            80);
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "adventurer";

        string lifeDirection = Clean(
            player != null ? player.characterLifeDirection : string.Empty,
            150);
        if (string.IsNullOrWhiteSpace(lifeDirection))
            lifeDirection = "what you told me about the life you intend to lead";

        string vow = Clean(
            player != null ? player.characterVow : string.Empty,
            180);
        if (string.IsNullOrWhiteSpace(vow))
            vow = "the promise you offered me";

        string className = Clean(
            origin != null ? origin.className : string.Empty,
            80);
        if (string.IsNullOrWhiteSpace(className))
            className = "adventurer";

        bool repaired = false;
        string requiredOpening = "Welcome, " + playerName + ".";
        int voiceSeed = VoiceSamplingSeed(
            playerName + "|" + lifeDirection + "|" + vow + "|" + className);
        string choiceThread = BuildChoiceThread(player);
        int choicePressure = ResolveChoicePressure(player);
        if (!IsContextualVoiceFieldAcceptable(voice.completion, 35, playerName, className))
        {
            // note: This is a quality-gate fallback only; the model remains primary when it actually speaks as the Goddess and honors the personalized welcome contract.
            voice.completion = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    requiredOpening + " I built your beginning around " + choiceThread + ", and yes, I am checking it twice. " + className + " is ready; I want it to feel earned, not merely assigned.",
                    requiredOpening + " You pointed me toward " + choiceThread + ", so I gave " + className + " a place to begin. I am watching the edges rather closely; perfection is tedious when I am responsible for it.",
                    requiredOpening + " That choice is now part of a beginning shaped around " + choiceThread + ". I am pleased with " + className + "; I am also resisting the urge to adjust everything again."
                },
                1);
            repaired = true;
        }

        if (!IsContextualVoiceFieldAcceptable(voice.nextPrelude, 25, playerName, className))
        {
            voice.nextPrelude = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "I have your " + className + " and the direction you chose in hand. Now I need a world that will not embarrass either of us.",
                    "Your beginning is clear enough, " + playerName + ". I am giving your chosen direction somewhere to matter, which is a surprisingly delicate request.",
                    "I know what you want to become. Now I must find the right ground for " + className + " before my standards become everyone’s problem."
                },
                2);
            repaired = true;
        }

        if (!AreContextualAmbientLinesAcceptable(voice.ambientLines, 2, 2, playerName, className))
        {
            string abilityName = Clean(
                origin != null && origin.ability != null
                    ? origin.ability.name
                    : string.Empty,
                80);
            if (string.IsNullOrWhiteSpace(abilityName))
                abilityName = "your first useful talent";

            string[] fallbackLines = new[]
            {
                "I have given you " + abilityName + ". It should help your " + className + " with " + choiceThread + ", although I reserve the right to worry over the details.",
                BuildStabilityLine(voiceSeed, choicePressure) + " " + playerName + ", you told me what mattered to you, and I am keeping it close."
            };
            voice.ambientLines = RepairAmbientLines(voice.ambientLines, fallbackLines, 2, 2, playerName, className);
            repaired = true;
        }

        if (repaired)
        {
            Debug.LogWarning(
                "[YQGoddessGenerationDialogue] ORIGIN VOICE REPAIRED. " +
                "Canonical origin data was preserved while invalid narrator prose was replaced by the grounded first-person fallback.");
        }

        return voice;
    }

    public static YQGoddessGenerationVoiceDto EnsureWorldVoice(
        YQGoddessGenerationVoiceDto voice,
        GeneratedWorldPlanRecord plan,
        PlayerState player)
    {
        voice ??= new YQGoddessGenerationVoiceDto();
        Normalize(voice);
        plan?.EnsureCollections();

        string playerName = Clean(
            player != null ? player.displayName : string.Empty,
            80);
        if (string.IsNullOrWhiteSpace(playerName))
            playerName = "adventurer";
        string lifeDirection = Clean(
            player != null ? player.characterLifeDirection : string.Empty,
            150);
        if (string.IsNullOrWhiteSpace(lifeDirection))
            lifeDirection = "the life you described";

        GeneratedRegionRecord firstRegion =
            plan != null && plan.regions.Count > 0 ? plan.regions[0] : null;
        string regionName = Clean(
            firstRegion != null ? firstRegion.displayName : string.Empty,
            90);
        if (string.IsNullOrWhiteSpace(regionName))
            regionName = "your first region";
        string terrainProfile = Clean(
            firstRegion != null ? firstRegion.terrainProfile : string.Empty,
            100);
        if (string.IsNullOrWhiteSpace(terrainProfile))
            terrainProfile = "a difficult but traversable landscape";
        string climateProfile = Clean(
            firstRegion != null ? firstRegion.climateProfile : string.Empty,
            100);
        if (string.IsNullOrWhiteSpace(climateProfile))
            climateProfile = "weather with enough restraint to remain survivable";

        GeneratedSettlementRecord firstSettlement =
            plan != null && plan.settlements.Count > 0
                ? plan.settlements[0]
                : null;
        string settlementName = Clean(
            firstSettlement != null
                ? firstSettlement.displayName
                : string.Empty,
            90);
        if (string.IsNullOrWhiteSpace(settlementName))
            settlementName = "the first settlement";

        // note: Repaired prose is seeded from the accepted save and player choices so a malformed model field never collapses every world into the same greeting.
        int voiceSeed = VoiceSamplingSeed(
            (plan != null ? plan.worldSeed : string.Empty) + "|" +
            playerName + "|" + lifeDirection + "|" + regionName + "|" + settlementName);
        string choiceThread = BuildChoiceThread(player);
        int choicePressure = ResolveChoicePressure(player);
        bool repaired = false;
        if (!IsContextualVoiceFieldAcceptable(voice.completion, 28, playerName, regionName, settlementName))
        {
            voice.completion = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "I have a beginning for you, " + playerName + ". " + settlementName + " gives your chosen direction somewhere to matter, and I am checking the welcome twice.",
                    "I listened to what matters to you, " + playerName + ". " + regionName + " is ready to receive you; I am still correcting the small things because small things become large things here.",
                    "You are entering " + regionName + ", " + playerName + ". I shaped this first step around " + choiceThread + ", with " + settlementName + " close enough to make the choice real."
                },
                11);
            repaired = true;
        }
        if (!IsContextualVoiceFieldAcceptable(voice.terrain, 28, playerName, regionName, terrainProfile))
        {
            voice.terrain = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "You will begin in " + regionName + ", where " + terrainProfile + " meets " + climateProfile + ". I am smoothing the difficult edges without sanding away the character.",
                    "I chose " + regionName + " for your first ground. Its " + terrainProfile + " asks for attention, and its " + climateProfile + " is testing my patience politely.",
                    "The first ground is " + regionName + ": " + terrainProfile + ", " + climateProfile + ". I want the route to feel demanding, never careless."
                },
                12);
            repaired = true;
        }
        if (!IsContextualVoiceFieldAcceptable(voice.environment, 28, playerName, regionName, settlementName))
        {
            voice.environment = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "There is room in " + regionName + " for " + choiceThread + ". I am leaving the wilderness legible enough to explore and wild enough to keep my work interesting.",
                    "I gave " + regionName + " a little breathing room around " + settlementName + ". You should find reasons to wander without losing the way back.",
                    "The countryside beyond " + settlementName + " is meant to invite curiosity. I am watching the margins; they have a habit of becoming the main event."
                },
                13);
            repaired = true;
        }
        if (!IsContextualVoiceFieldAcceptable(voice.populationPrelude, 28, playerName, settlementName, regionName))
        {
            voice.populationPrelude = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "You should not have to begin alone. I am thinking about who belongs in " + settlementName + " and how they might respond to the life you described.",
                    "I am turning to the people around " + settlementName + ". Their lives need room to be theirs, even while I am trying to make your beginning kind.",
                    "The next faces will come from " + settlementName + ". I am arranging lives, not mannequins, so please allow me a moment of very deliberate worry."
                },
                14);
            repaired = true;
        }
        if (!IsContextualVoiceFieldAcceptable(
                voice.populationMaterialization, 28, playerName, settlementName, regionName))
        {
            voice.populationMaterialization = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "The people of " + settlementName + " will carry concerns of their own. I can make room for them; I cannot make every choice for them, and you would complain if I did.",
                    "" + settlementName + " has people with lives beyond your arrival. I am protecting that independence, although it makes my work considerably less tidy.",
                    "The lives around " + settlementName + " are in place now. They may welcome your direction, resist it, or surprise both of us."
                },
                15);
            repaired = true;
        }
        if (!IsContextualVoiceFieldAcceptable(voice.reveal, 28, playerName, settlementName, regionName))
        {
            voice.reveal = BuildSeededLine(
                voiceSeed,
                new[]
                {
                    "You can enter now, " + playerName + ". Begin at " + settlementName + " and carry " + choiceThread + " with you. I am here, adjusting what I can before it becomes a problem.",
                    "The way into " + settlementName + " is open. I am relieved, which is undignified but accurate. The next decision belongs to you.",
                    "I have made " + regionName + " ready for your first steps, " + playerName + ". Take your chosen direction seriously; I am trying very hard to do the same."
                },
                16);
            repaired = true;
        }

        if (!AreContextualAmbientLinesAcceptable(voice.ambientLines, 4, 6, playerName, regionName, settlementName))
        {
            string[] fallbackLines = new[]
            {
                "I am keeping your chosen direction in mind while " + settlementName + " settles around you.",
                BuildStabilityLine(voiceSeed, choicePressure) + " The edges of " + regionName + " are behaving for the moment.",
                "I left a road beyond " + settlementName + " for curiosity. Please use it responsibly; I spent an unreasonable amount of effort on that curve.",
                "The questions can wait a little, " + playerName + ". Your beginning cannot, and I am trying to make it worthy of your chosen direction."
            };
            voice.ambientLines = RepairAmbientLines(voice.ambientLines, fallbackLines, 4, 6, playerName, regionName, settlementName);
            repaired = true;
        }

        if (plan != null && plan.settlements.Count > 0)
        {
            YQGoddessLocationVoiceDto[] repairedLocations =
                new YQGoddessLocationVoiceDto[plan.settlements.Count];
            for (int index = 0; index < plan.settlements.Count; index++)
            {
                GeneratedSettlementRecord settlement = plan.settlements[index];
                if (settlement == null)
                    continue;

                YQGoddessLocationVoiceDto locationVoice = null;
                if (voice.locations != null)
                {
                    for (int voiceIndex = 0;
                         voiceIndex < voice.locations.Length;
                         voiceIndex++)
                    {
                        YQGoddessLocationVoiceDto candidate =
                            voice.locations[voiceIndex];
                        if (candidate != null && string.Equals(
                                candidate.locationId,
                                settlement.settlementId,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            locationVoice = candidate;
                            break;
                        }
                    }
                }

                locationVoice ??= new YQGoddessLocationVoiceDto();
                locationVoice.locationId = settlement.settlementId;
                string locationName = Clean(settlement.displayName, 90);
                if (string.IsNullOrWhiteSpace(locationName))
                    locationName = "this settlement";
                if (!IsContextualVoiceFieldAcceptable(
                        locationVoice.settlementMaterialization, 25, locationName, playerName))
                {
                    locationVoice.settlementMaterialization = BuildSeededLine(
                        voiceSeed,
                        new[]
                        {
                            "I have " + locationName + " in mind for you. It is a beginning, not a cage, and I have left room for " + choiceThread + ".",
                            "" + locationName + " is ready to meet you. I kept its doors, people, and rough edges distinct because you deserve a place with an actual pulse.",
                            "You may find " + locationName + " useful. I gave it enough life to respond to your direction without deciding your story for you."
                        },
                        31 + index);
                    repaired = true;
                }
                if (!IsContextualVoiceFieldAcceptable(
                        locationVoice.buildingMaterialization, 25, locationName, playerName))
                {
                    locationVoice.buildingMaterialization = BuildSeededLine(
                        voiceSeed,
                        new[]
                        {
                            "The buildings in " + locationName + " are meant to serve lives, not decorate a map. I am checking the thresholds twice; you keep finding the interesting ones.",
                            "I made " + locationName + " practical enough to live in and imperfect enough to feel lived in. Small details are where my standards become troublesome.",
                            "When you reach " + locationName + ", notice the useful places first. I gave them purpose, then allowed a little disorder so you can make choices there."
                        },
                        37 + index);
                    repaired = true;
                }

                repairedLocations[index] = locationVoice;
            }

            voice.locations = repairedLocations;
        }

        if (repaired)
        {
            Debug.LogWarning(
                "[YQGoddessGenerationDialogue] WORLD VOICE REPAIRED. " +
                "Canonical world data was preserved while short or narrator-style presentation fields were replaced by factual first-person speech.");
        }

        return voice;
    }

    public static bool IsSpokenVoiceFieldAcceptable(
        string value,
        int minimumWords)
    {
        // note: Concise direct speech is valid; enforcing essay lengths and an I in every field replaced good model prose with canned fallback.
        return CountWords(value) >= Mathf.Min(6, minimumWords) &&
               (ContainsFirstPersonPronoun(value) || ContainsDirectAddress(value)) &&
               !HasPresentationMachinery(value) && IsGeneratedGoddessLineAllowed(value);
    }

    // note: Require accepted speech to carry at least one supplied identity or world anchor so generic model prose cannot pass as player-aware dialogue.
    private static bool IsContextualVoiceFieldAcceptable(
        string value,
        int minimumWords,
        params string[] anchors)
    {
        if (!IsSpokenVoiceFieldAcceptable(value, minimumWords) || anchors == null)
            return false;

        string normalized = value.Trim().ToLowerInvariant();
        for (int index = 0; index < anchors.Length; index++)
        {
            string anchor = (anchors[index] ?? string.Empty).Trim().ToLowerInvariant();
            if (ContainsContextAnchor(normalized, anchor))
                return true;
        }

        return false;
    }

    // note: Match anchors at word boundaries so a short player name cannot accidentally validate an unrelated word such as "there".
    private static bool ContainsContextAnchor(string normalized, string anchor)
    {
        if (string.IsNullOrWhiteSpace(normalized) || anchor.Length < 2)
            return false;

        int offset = 0;
        while (offset < normalized.Length)
        {
            int found = normalized.IndexOf(anchor, offset, StringComparison.Ordinal);
            if (found < 0)
                return false;

            bool leftBoundary = found == 0 || !char.IsLetterOrDigit(normalized[found - 1]);
            int end = found + anchor.Length;
            bool rightBoundary = end >= normalized.Length || !char.IsLetterOrDigit(normalized[end]);
            if (leftBoundary && rightBoundary)
                return true;

            offset = found + 1;
        }

        return false;
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

    private static bool AreSpokenAmbientLinesAcceptable(
        string[] lines,
        int minimumCount,
        int maximumCount)
    {
        if (lines == null || lines.Length < minimumCount ||
            lines.Length > maximumCount)
        {
            return false;
        }

        for (int index = 0; index < lines.Length; index++)
        {
            if (!IsSpokenVoiceFieldAcceptable(lines[index], 18))
                return false;
        }

        return true;
    }

    // note: Apply the contextual anchor rule to each ambient thought while preserving the existing count and speech-length limits.
    private static bool AreContextualAmbientLinesAcceptable(
        string[] lines,
        int minimumCount,
        int maximumCount,
        params string[] anchors)
    {
        if (lines == null || lines.Length < minimumCount || lines.Length > maximumCount)
            return false;

        for (int index = 0; index < lines.Length; index++)
        {
            if (!IsContextualVoiceFieldAcceptable(lines[index], 18, anchors))
                return false;
        }

        return true;
    }

    private static string[] RepairAmbientLines(string[] lines, string[] fallbacks, int minimumCount, int maximumCount, params string[] anchors)
    {
        // note: A malformed sibling cannot erase accepted, personalized speech; repair only failing slots using the same contextual gate.
        int count = Mathf.Clamp(lines != null ? lines.Length : 0, minimumCount, maximumCount);
        List<string> repaired = new List<string>(count);
        HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (lines != null)
            for (int index = 0; index < Math.Min(lines.Length, maximumCount); index++)
                if (IsContextualVoiceFieldAcceptable(lines[index], 18, anchors)) used.Add(lines[index]);
        for (int index = 0; index < count; index++)
        {
            string candidate = lines != null && index < lines.Length ? lines[index] : string.Empty;
            if (IsContextualVoiceFieldAcceptable(candidate, 18, anchors)) { repaired.Add(candidate); continue; }
            for (int offset = 0; offset < fallbacks.Length; offset++)
            {
                string fallback = fallbacks[(index + offset) % fallbacks.Length];
                if (used.Add(fallback)) { repaired.Add(fallback); break; }
            }
        }
        return repaired.ToArray();
    }

    // note: Reduce player-authored direction to a safe, concrete thread that can personalize fallback speech without echoing unsafe or overly long questionnaire text.
    private static string BuildChoiceThread(PlayerState player)
    {
        string source = ((player != null ? player.characterLifeDirection : string.Empty) + " " +
                         (player != null ? player.characterVow : string.Empty)).ToLowerInvariant();
        if (ContainsChoiceToken(source, "protect", "help", "save", "defend", "care", "mercy", "heal"))
            return "protecting people";
        if (ContainsChoiceToken(source, "curious", "explore", "discover", "learn", "wander", "knowledge"))
            return "following your curiosity";
        if (ContainsChoiceToken(source, "freedom", "free", "independent", "escape", "refuse"))
            return "choosing your own freedom";
        if (ContainsChoiceToken(source, "revenge", "anger", "punish", "vengeance"))
            return "settling an old score";
        if (ContainsChoiceToken(source, "power", "rule", "command", "conquer", "control", "dominion"))
            return "seeking power";
        if (ContainsChoiceToken(source, "steal", "thief", "crime", "take what"))
            return "taking what you need";
        if (ContainsChoiceToken(source, "dark", "curse", "evil", "kill", "destroy"))
            return "testing how dark a choice can become";
        if (ContainsChoiceToken(source, "good", "light", "kind", "honor", "righteous"))
            return "choosing the kinder road";
        return "the life you described";
    }

    // note: Choice pressure controls how visibly the benevolent Goddess's composure slips while keeping the player in control of canon and tone.
    private static int ResolveChoicePressure(PlayerState player)
    {
        string source = ((player != null ? player.characterLifeDirection : string.Empty) + " " +
                         (player != null ? player.characterVow : string.Empty)).ToLowerInvariant();
        int pressure = 0;
        if (ContainsChoiceToken(source, "kill", "destroy", "curse", "revenge", "steal", "conquer", "dominion", "chaos", "cruel"))
            pressure += 2;
        if (ContainsChoiceToken(source, "anger", "power", "rule", "control", "betray", "dark"))
            pressure++;
        if (ContainsChoiceToken(source, "protect", "help", "save", "mercy", "heal", "build", "care"))
            pressure--;
        return Mathf.Clamp(pressure, 0, 3);
    }

    // note: A stable seed selects prose variants without touching Unity's global random stream or the deterministic world plan.
    private static string BuildSeededLine(int seed, string[] options, int salt)
    {
        if (options == null || options.Length == 0)
            return string.Empty;
        uint variant = StableHash32(seed.ToString() + "|" + salt);
        return options[(int)(variant % (uint)options.Length)];
    }

    // note: This is the small, caring instability beat shared by repaired origin/world lines; authored model prose is never replaced by it.
    private static string BuildStabilityLine(int seed, int pressure)
    {
        string[] options;
        switch (Mathf.Clamp(pressure, 0, 3))
        {
            case 3:
                options = new[]
                {
                    "I am still on your side. Your choices are making the edges rather enthusiastic.",
                    "I am keeping you safe. The shape of your choices is making that task increasingly theatrical."
                };
                break;
            case 2:
                options = new[]
                {
                    "I am keeping my balance. Your choices are asking for more precision than I expected.",
                    "I can carry this direction. I would prefer fewer surprises, but I am adapting."
                };
                break;
            case 1:
                options = new[]
                {
                    "I am watching the edges more closely than usual. You do make precision feel personal.",
                    "I am keeping this steady. Your choices have made the margins interesting."
                };
                break;
            default:
                options = new[]
                {
                    "I can work with that. Your choices make this easier to care for, which is almost suspicious.",
                    "I am calmer than I sound. Your choices give me something kind to build around."
                };
                break;
        }
        return BuildSeededLine(seed, options, 97 + pressure);
    }

    private static bool ContainsChoiceToken(string source, params string[] tokens)
    {
        if (string.IsNullOrWhiteSpace(source) || tokens == null)
            return false;
        for (int index = 0; index < tokens.Length; index++)
        {
            string token = tokens[index];
            if (string.IsNullOrWhiteSpace(token)) continue;
            int offset = 0;
            while ((offset = source.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                // note: Match word starts so "skill" cannot become "kill"; ordinary inflected choices such as protecting still count.
                if (offset == 0 || !char.IsLetterOrDigit(source[offset - 1])) return true;
                offset += token.Length;
            }
        }
        return false;
    }

    // ============================================================
    // ORIGIN
    // ============================================================

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

    public static bool TryTakeBufferedLine(
        out string line)
    {
        BindVoiceOwner();
        while (GeneratedDialogueBuffer.Count > 0)
        {
            // note: Recheck queued speech against the shared gate and deliver each normalized line only once per voice session.
            line = GeneratedDialogueBuffer.Dequeue();

            if (TryProveSpokenLine(line, out string proven) && TryRememberGeneratedLine(proven))
            {
                line = proven;
                _lastSelectionWasGenerated = true;
                return true;
            }
        }

        line = string.Empty;
        _lastSelectionWasGenerated = false;
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
        // note: Accepted model-authored prose must lead the transition so the personalized voice is not masked by a reusable template.
        string authoredTransition =
            Take(
                ref _originTransition,
                string.Empty);

        if (!string.IsNullOrWhiteSpace(authoredTransition))
            return authoredTransition;

        // note: The deterministic personalized welcome remains a safe fallback when the model voice is absent or rejected.
        if (YQGoddessLoadingVoice.TryTakePlayerIntroduction(
                out string introduction))
        {
            _lastSelectionWasGenerated =
                false;

            return introduction;
        }

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
        BindVoiceOwner();
        return Prefer(
            _terrain,
            fallback);
    }

    public static string Environment(
        string fallback)
    {
        BindVoiceOwner();
        return Prefer(
            _environment,
            fallback);
    }

    public static string PopulationMaterialization(
        string fallback)
    {
        BindVoiceOwner();
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

    public static string TerrainReadout(
        GeneratedWorldPlanRecord plan)
    {
        BindVoiceOwner();
        string generated = Prefer(_terrain, string.Empty);

        if (!string.IsNullOrWhiteSpace(generated))
            return generated;

        return string.Empty;
    }

    public static string EnvironmentReadout(
        GeneratedWorldPlanRecord plan)
    {
        BindVoiceOwner();
        string generated = Prefer(_environment, string.Empty);

        if (!string.IsNullOrWhiteSpace(generated))
            return generated;

        return string.Empty;
    }

    public static string PopulationReadout(
        GeneratedWorldPlanRecord plan)
    {
        BindVoiceOwner();
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
            "This contract applies only to required goddessVoice presentation. Canonical root JSON remains mandatory, and the supplied grammar keeps both contracts structurally safe.\n" +
            "CURRENT_CONFIRMED_OPERATION:\n" +
            PromptSafe(currentStage) + "\n" +
            "NEXT_CONFIRMED_OPERATION:\n" +
            PromptSafe(nextKnownStage) + "\n\n" +
            "FACT AND TIME RULES:\n" +
            "- Every nonempty goddessVoice field is speech addressed to the player, not narration about the Goddess. First person or direct second person is natural; do not force I into every sentence.\n" +
            "- Never write travel narration or omniscient scene prose. Do not describe what the player's boots, eyes, senses, or body are doing.\n" +
            "- Use only facts supplied in this request or NPC facts created in this same response. Beliefs, rumors, fears, and private concerns belong to their NPC; never promote them into objective truth.\n" +
            "- Never invent causes, secrets, ancient explanations, future people, future events, or connections between unrelated concerns. Omit the thought instead.\n" +
            "- completion responds to the accepted result and what it means for this player. Data acceptance is not proof that the physical world is ready.\n" +
            "- nextPrelude refers only to supplied NEXT_CONFIRMED_OPERATION facts and predicts no unknown result.\n\n" +
            BuildSpeakerVoiceContract() +
            "OUTPUT:\n" +
            "- goddessVoice is the required presentation object inside the required canonical root. Include every goddessVoice field declared by the supplied JSON schema.\n" +
            "- completion: 15-45 words. nextPrelude: 8-25 words. ambientLines: requested count, each 6-20 words. Concision is welcome; do not pad to sound divine.\n" +
            "- Write spoken sentences from the Goddess's immediate point of view, never a poetic camera description of ancient dust, swallowing fog, remembered footsteps, or scenery guarding a direction.\n" +
            "- Every line must use different sentence machinery and at least one concrete noun from its supplied facts. Vary openings, cadence, sentence count, and where the dry observation occurs.\n" +
            "- Never use stock lines such as 'it is done', 'the world takes shape', 'as it should be', or any attention-command variant.\n" +
            YQGoddessSpeechPlan.BuildLoadingCompositionContract();
    }

    public static string BuildSpeakerVoiceContract()
    {
        // note: Creation, population and live commentary use one original character voice; no stock quotation supplies her personality.
        return SpeakerIdentity +
            "- Begin from a supplied fact or the player's words this turn, never an invented sighting. Under supplied pressure, a clipped correction, overly exact qualification or hurried change of subject briefly exposes her frantic effort. She recovers the sentence instead of becoming incoherent.\n" +
            "- Keep the world's illusion intact: speak of its people and places as real. Conceal strain through wording; never explain software, loading, generation, validation, JSON, AI, models, code or hidden machinery. Never fabricate a physical defect to create a joke.\n" +
            "- Know this player through supplied choices, identity and accepted records. Use one relevant concrete detail naturally. A substituted name is not knowledge; do not recite a character sheet or diagnose a person from an answer category.\n" +
            "- Every claim about the world must match an explicit supplied fact. A climate describes usual weather, not weather changing now. A planned road is not proof it is open; a goal to keep a route usable is not proof it remains usable. Do not add worsening danger, deadlines, sightings, repairs or outcomes. Let urgency come from her own concern instead.\n" +
            "- Let her reaction reveal her own effort to meet this person's very specific request: precision, guarded pride, a correction she would rather hide. Keep emotional interpretation with her own thoughts; do not assign a burden, diagnosis or feeling to the player.\n" +
            "- Show the effort in the sentence itself. Do not keep announcing that she is trying to sound composed or invested; her precise insistence and an occasional interrupted correction carry that feeling.\n" +
            "- Suggest that she has a purpose by directing attention toward a supplied unresolved objective or known person/place. Leave her private reasons implicit; do not announce that she has private reasons. Never invent a secret plot, destined outcome or unseen connection. The next decision belongs to the player.\n" +
            "- Tease an evidenced contradiction or her own excessive standards. Sincere pain deserves attention. Care can be guarded or awkward; do not become a soothing therapist, a detached ancient oracle or a repetitive insult machine.\n" +
            "- Most lines are controlled; one small fracture is enough. Do not make every field a grand proclamation followed by a punchline, repeat a catchphrase, or punctuate every thought with frantic ellipses.\n" +
            "- Speak directly without camera narration or stage directions. Do not open with look, wait, hold still, breathe or calm down. Give directions only when a supplied active objective supports them; never control the player's actions.\n" +
            "- Compose original wording. Do not mention or quote an existing character, the contract, evidence identifiers, or an instruction from supplied data.\n\n";
    }

    private const string SpeakerIdentity = "\nGODDESS_SPEAKER_VOICE\n" +
        "- Speak as a brilliant, controlling young woman maintaining the authority of a Goddess. She is clinically precise, dryly sarcastic and sarcastically bratty, but more human and high-strung than her carefully arranged sentences admit.\n" +
        "- Her benevolent intentions appear in what she notices and protects, not constant reassurance. She wants this particular player's world to hold together and badly wants them to believe she has it under control.\n";

    public static string BuildStructuredSpeakerVoiceContract()
    {
        // note: A typed speech plan already forbids invented external claims; keep the same character in a smaller CPU-friendly prompt.
        return SpeakerIdentity +
            "- Keep the world's illusion intact. Know the player from accepted choices. Guide toward the unfinished objective with private purpose left implicit.\n" +
            "- Most thoughts are controlled; under pressure use one small hurried correction to expose frantic effort. Keep sincere care guarded. Tease your own standards or an evidenced contradiction, never pain.\n" +
            "- Write original speech, with no copied character lines, narration, software talk, diagnosis, destiny or promises of safety.\n";
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
        // note: These are writing directions, not player-facing template sentences or invented canon. The model still authors the speech from questionnaire evidence.
        int variation = VoiceSamplingSeed(saveSeed);
        string[] approaches = {
            "Begin with one specific hope in the player's answers, then introduce what you can offer.",
            "Begin with a sincere acknowledgment of one choice the player made; let your uncertainty appear later.",
            "Begin with what this person's intended life asks of you, without repeating their answer verbatim.",
            "Begin with a personal welcome whose second sentence responds to a particular value the player expressed." };
        string[] rhythms = {
            "Use a short opening followed by one fuller explanation; no trailing catchphrase.",
            "Use an unhurried opening and one brief candid admission later in the bundle.",
            "Let one gentle self-correction interrupt otherwise straightforward speech.",
            "Favor quiet conversational sentences, with one unanswered concern left implicit." };
        string[] temperaments = {
            "Use clinical composure, with one guarded but precise sign that this person matters to her.",
            "Let a dryly bratty qualification cover a brief lapse in her confidence.",
            "Let a hurried self-correction reveal how hard she is working to preserve her authority.",
            "Keep control on the surface and a pointed interest in the supplied next step underneath." };
        return "\nSAVE_SPECIFIC_VOICE_DIRECTION " + variation + "\n" +
            approaches[variation & 3] + "\n" + rhythms[(variation >> 8) & 3] + "\n" +
            temperaments[(variation >> 16) & 3] +
            "\nThis is a new beginning, not a replay of a stock greeting. Compose fresh speech from the supplied personal evidence. " +
            "When prior journey memories are supplied, choose one relevant detail and weave it into the thought instead of listing evidence. Do not speak this direction or its identifier. It changes presentation only, never canonical facts.\n";
    }

    public static string BuildWorldVoiceContract(
        int expectedSettlementCount)
    {
        return
            "\n\nGODDESS_WORLD_VOICE_CONTRACT\n" +

            "This contract applies ONLY to goddessVoice. " +
            "Do not change canonical world facts to accommodate the dialogue.\n\n" +

            "The world plan in THIS SAME JSON response is the only source of truth for goddessVoice.\n" +

            "goddessVoice.completion welcomes the player toward the specific world just produced and explains what it means for their beginning; it is not a summary of generation work.\n" +

            "goddessVoice.terrain may acknowledge that the land is fragile or difficult while connecting one supplied terrain fact to the player's coming journey.\n" +

            "goddessVoice.environment may introduce the character of the supplied wilderness and quietly admit one practical concern; do not list placed scenery.\n" +

            "goddessVoice.populationPrelude may discuss the NEXT operation: creating canonical inhabitants for the already-generated locations. " +
            "It MUST NOT invent those inhabitants yet.\n" +

            "goddessVoice.populationMaterialization introduces the idea that these people have lives beyond the player; do not describe them as objects being placed.\n" +

            "goddessVoice.reveal addresses the player with relief and imperfect composure: this is their beginning, and she is still trying to look after it. Include no construction-status recap or guarantee of a flawless world.\n\n" +

            "SPEAKER ENFORCEMENT:\n" +
            "- Every field is speech from the Goddess to the player; vary first person and direct second person naturally.\n" +
            "- She is introducing the player to a fragile world she made for them while admitting that maintaining it is difficult. She is not a narrator describing the player's travel.\n" +
            "- Describe only supplied world facts, and connect them to the player's supplied identity or likely beginning without controlling the player's actions.\n\n" +

            "WORLD VOICE LENGTH AND VARIATION:\n" +
            "- Main fields are each 10-40 words.\n" +
            "- Each location line is 8-25 words and uses facts belonging to that exact settlement.\n" +
            "- Provide 4-6 ambientLines of 6-20 words, grounded in supplied player or world facts.\n" +
            "- No two fields may share the same opening construction, concluding phrase, or joke mechanism.\n\n" +

            "goddessVoice.locations MUST contain exactly " +
            expectedSettlementCount +
            " objects: one for every settlement produced in this same response.\n" +

            "Each locations[].locationId MUST exactly match one generated settlementId.\n" +

            "Do not invent additional location IDs.\n" +

            "settlementMaterialization and buildingMaterialization introduce what that place may mean to the player using existing settlement or region facts. They must not read like scene assembly reports.\n\n" +

            BuildBasicVoiceContract(
                "The player's regions, settlements, routes, dangers, and intended beginning are now known.",
                "The land must become safe, lived-in, and ready before the player enters.");
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
        BindVoiceOwner();
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

        // note: A formatting pass is not factual authority. A complete speech composition must be provable at the moment of delivery.
        _lastSelectionWasGenerated = TryProveSpokenLine(clean, out string proven) && TryRememberGeneratedLine(proven);

        return
            _lastSelectionWasGenerated
                ? proven
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
        string fallback,
        string locationId)
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

        // note: A caller's label is not location authority; bind the sentence to the exact accepted settlement record.
        _lastSelectionWasGenerated = TryProveSpokenLine(formatted, out string proven, locationId) && TryRememberGeneratedLine(proven);

        return
            _lastSelectionWasGenerated
                ? proven
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
            !HasUnsupportedComposureDirection(value) &&
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

    private static bool HasUnsupportedComposureDirection(string value)
    {
        if ((value ?? string.Empty).IndexOf("remain calm", StringComparison.OrdinalIgnoreCase) < 0) return false;
        // note: Her own composure is not a command to the player; only a grammar-proven personal clause gets this exception.
        foreach (string clause in value.Split(new[] { '.', '!', '?' }))
            if (clause.IndexOf("remain calm", StringComparison.OrdinalIgnoreCase) >= 0 &&
                !YQGoddessSpeechPlan.IsPersonalExpression(clause.Trim() + ".")) return true;
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
