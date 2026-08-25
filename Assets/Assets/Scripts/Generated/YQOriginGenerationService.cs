using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class YQOriginGenerationService : MonoBehaviour
{
    public static YQOriginGenerationService Instance { get; private set; }

    [Header("Origin LLM")]
    public bool enableLlmOriginGeneration = true;
    public int originNumPredict = 760;
    [Range(0f, 1f)] public float originTemperature = 0.35f;
    private const string InitialGenerationOwner =
    "InitialWorldGeneration";

    public string LastOriginGenerationMessage { get; private set; } = string.Empty;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindAnyObjectByType<YQOriginGenerationService>() != null)
            return;

        GameObject go = new GameObject("00__YQ_OriginGenerationService");
        DontDestroyOnLoad(go);
        go.AddComponent<YQOriginGenerationService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public bool TryRequestOrigin(PlayerState state, string mode, IReadOnlyList<string> answers, Action<YQOriginGenerationDto> onReady)
    {
        if (!enableLlmOriginGeneration || LLMClient.Instance == null)
            return false;

        string seed = BuildOriginSeed(state, mode, answers);
        string prompt = BuildPrompt(state, mode, answers, seed);
        Dictionary<string, object> options = new Dictionary<string, object>
        {
            // note: Origin generation should be a compact player readout, not a long-form essay.
            // note: Keep origin compact so optional presentation cannot chew through the response budget.
            // note: The model authors identity-bearing fields only; deterministic normalization supplies stable mechanical boilerplate after acceptance.
            { "num_predict", Mathf.Clamp(originNumPredict, 650, 900) },
            { "temperature", Mathf.Clamp01(originTemperature) },
            { "top_p", 0.9f },
            { "request_timeout_seconds", 75 }
        };

        LastOriginGenerationMessage = "The goddess is reading the player's answers.";
        YQGeneratedWorldRuntimeBuilder
    .BeginInitialGenerationGameplayLock();
        // note: Origin data is canonical startup state, so submit it through the typed JSON contract.
        LLMClient.Instance.Submit(
    new YQLlmRequest
    {
        prompt = prompt,
        debugTag = "OriginGeneration",
        category = LLMGenerationCategory.OriginGeneration,
        priority = YQLlmRequestPriority.StartupExclusive,
        // note: Keep JSON-mode transport enabled; the origin validator remains the authority for canonical acceptance and optional voice repair.
        requireJson = true,
        // note: The origin validator can discard malformed optional Goddess prose without discarding valid canonical player identity.
        deferJsonValidationToCaller = true,
        maxRetries = 0,
        exclusiveOwner = InitialGenerationOwner,
        optionsOverride = options
    },
    result =>
    {
        // note: Structured callers receive normalized JSON only after transport and format validation succeeds.
        string raw = result.success ? result.text : null;
        if (!TryParseOrigin(
                raw,
                seed,
                mode,
                out YQOriginGenerationDto dto,
                out string error))
        {
            LastOriginGenerationMessage =
                "Origin LLM result rejected: " +
                error;

            Debug.LogWarning(
                "[YQOriginGenerationService] " +
                LastOriginGenerationMessage +
                "\nRAW:\n" +
                TruncateForLog(raw ?? "<null>"));

            /*
             * Do not release exclusive generation here.
             *
             * The questionnaire has a deterministic origin fallback,
             * and world generation must continue without allowing
             * dialogue/background LLM work to interrupt the chain.
             */
            onReady?.Invoke(
                null);

            return;
        }

        LastOriginGenerationMessage =
    "Origin LLM result accepted: " +
    dto.className +
    " / " +
    dto.ability.name +
    ".";

        /*
         * Presentation voice is transient.
         * It is not part of canonical player/world state.
         */
        YQGoddessGenerationDialogue
            .SetOriginVoice(
                dto.goddessVoice);

        // note: When presentation voice is omitted to protect canonical JSON reliability, narrate the accepted origin record itself.
        YQGoddessGenerationDialogue
            .SetOriginReadout(
                dto);

        onReady?.Invoke(
            dto);
    });

        return true;
    }

    private static string BuildPrompt(PlayerState state, string mode, IReadOnlyList<string> answers, string seed)
    {
        StringBuilder recent = new StringBuilder();
        recent.AppendLine("ORIGIN_MODE: " + Safe(mode, "Unknown"));
        recent.AppendLine("ORIGIN_SEED: " + seed);
        recent.AppendLine("CHARACTER_CREATION");
        recent.AppendLine(BuildCharacterCreationBlock(state));
        recent.AppendLine("QUESTIONNAIRE_ANSWERS");
        if (answers != null)
        {
            for (int i = 0; i < answers.Count; i++)
                recent.AppendLine((i + 1) + ". " + Safe(answers[i], "<blank>"));
        }
        YQWorldGenerationService worldGenerator = YQWorldGenerationService.Instance;
        if (worldGenerator != null)
        {
            // note: The first model response knows the requested production scale so its buffered narration can lead into the world pass accurately.
            recent.AppendLine("REQUESTED_WORLD_SCALE");
            recent.AppendLine("regions=" + worldGenerator.targetRegionCount);
            recent.AppendLine("settlements=" + worldGenerator.targetSettlementCount);
            recent.AppendLine("hostile_sites=" + worldGenerator.targetEncampmentCount);
        }
        // note: This compact presentation-only summary helps Goddess prose notice answer quality without changing origin canon.
        recent.AppendLine(
            YQGoddessLoadingVoice
                .BuildQuestionnaireContextForPrompt(
                    state,
                    answers));
        // note: Origin generation chooses gameplay semantics only; the runtime art binder owns imported asset selection and should not consume the origin context budget.
        recent.AppendLine("AVAILABLE_LOADOUT_SEMANTICS");
        recent.AppendLine("- Item types: weapon, offhand, armor, trinket, consumable.");
        recent.AppendLine("- Weapon forms: sword, axe, mace, dagger, spear, staff, bow, crossbow, scythe, throwing axe.");
        recent.AppendLine("- Offhand/accessory forms: shield, ring, amulet, gem, bracer, charm, seal, focus.");
        recent.AppendLine("- Armor forms: cuirass, helm, gloves, boots, belt, cloak.");
        recent.AppendLine("- VFX families: physical, fire, frost, storm, poison, heal, shield, shadow, earth, air, arcane, blood.");
        recent.AppendLine("- Visual assets are bound later from item form and VFX family; never output Unity asset paths.");

        string task =
            "Generate one deterministic, player-facing origin package from the committed character and questionnaire evidence. " +
            "Invent every name from this player; do not reuse labels, examples, fallback identities, or region names. " +
            "Character creation affects tone, pronouns, stimulus, and equipment, while questionnaire behavior remains the main evidence. " +
            "The first quest must introduce manifested gear, equipping gear, and speaking with Archivist Vey inside the Goddess-threshold witch house. " +
            "directionKey is exactly one of merchant, lumberjack, hero, demonlord, arcanist, warden, wanderer, stillness, custom; choose the best evidence match and never combine keys. " +
            "Return exactly three distinct loadout entries with supported slots and player-derived names/descriptions. " +
            "Omit unspecified mechanical boilerplate; deterministic normalization supplies it. Keep every description under twelve words. Return one compact JSON object.";

        string schema =
    PromptContextBuilder.WrapJsonSchema(
        "{" +
        "\"source\":\"llm_origin_v1\"," +
        "\"directionKey\":\"custom\"," +
        "\"stimulus\":\"specific player stimulus\"," +
        "\"className\":\"...\"," +
        "\"titleName\":\"...\"," +
        "\"ability\":{" +
        "\"name\":\"...\"," +
        "\"kind\":\"skill|spell\"," +
        "\"type\":\"combat|movement|utility|craft|social|control\"," +
        "\"description\":\"...\"," +
        "\"vfxFamily\":\"physical|fire|frost|storm|poison|heal|shield|shadow|earth|air|arcane|blood\"" +
        "}," +
        "\"quest\":{" +
        "\"name\":\"...\"," +
        "\"description\":\"...\"" +
        "}," +
        "\"loadout\":[" +
        "{" +
        "\"slot\":\"weapon\"," +
        "\"nameHint\":\"...\"," +
        "\"descriptionHint\":\"...\"" +
        "}," +
        "{" +
        "\"slot\":\"offhand\"," +
        "\"nameHint\":\"...\"," +
        "\"descriptionHint\":\"...\"" +
        "}," +
        "{" +
        "\"slot\":\"boots\"," +
        "\"nameHint\":\"...\"," +
        "\"descriptionHint\":\"...\"" +
        "}" +
        "]," +
        "\"goddessVoice\":{" +
        "\"completion\":\"...\"," +
        "\"nextPrelude\":\"...\"," +
        "\"ambientLines\":[\"...\",\"...\"]" +
        "}" +
        "}");

        return PromptContextBuilder.BuildContext(task + BuildOriginGoddessVoiceContract(), schema, recent.ToString(), BuildLedger(state));
    }

    private static string BuildOriginGoddessVoiceContract()
    {
        // note: Origin uses the same authoritative voice rail as world and NPC generation so the character cannot change personality between requests.
        return
            YQGoddessGenerationDialogue.BuildBasicVoiceContract(
                "The player's origin, class, title, first ability, quest, and loadout in this response are being accepted now.",
                "The accepted origin will be persisted, then the first world plan will be requested.") +
            "- For this origin response, provide exactly 2 ambientLines grounded in the accepted origin fields.\n";
    }

    private static string TruncateForLog(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        const int maxChars = 1600;
        if (value.Length <= maxChars)
            return value;

        // note: Bad LLM responses are diagnostic, but full dumps can freeze the Console during generation.
        return value.Substring(0, maxChars) +
               "\n... <truncated " +
               (value.Length - maxChars) +
               " chars>";
    }

    private static bool TryParseOrigin(
     string raw,
     string seed,
     string mode,
     out YQOriginGenerationDto dto,
     out string error)
    {
        dto =
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

        try
        {
            string json =
                ExtractFirstJsonObject(
                    raw);

            if (string.IsNullOrWhiteSpace(
                    json))
            {
                error =
                    "no JSON object";

                return false;
            }

            /*
             * Goddess dialogue is presentation-only.
             *
             * Extract it before canonical origin JSON is persisted.
             */
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

            // note: Small local models occasionally place trailing sibling fields inside quest; promote only the two known schema fields before strict DTO validation.
            PromoteNestedQuestProperty(root, "loadout");
            PromoteNestedQuestProperty(root, "goddessVoice");

            YQGoddessGenerationVoiceDto goddessVoice =
                null;

            try
            {
                goddessVoice =
                    root["goddessVoice"]
                        ?.ToObject<
                            YQGoddessGenerationVoiceDto>();
            }
            catch
            {
                /*
                 * Broken optional presentation data must never reject
                 * an otherwise valid canonical origin.
                 */
                goddessVoice =
                    null;
            }

            root.Remove(
                "goddessVoice");

            string canonicalJson =
                root.ToString(
                    Formatting.None);

            dto =
                JsonConvert.DeserializeObject<
                    YQOriginGenerationDto>(
                        canonicalJson);

            if (dto == null)
            {
                error =
                    "JSON parsed to null";

                return false;
            }

            dto.goddessVoice =
                goddessVoice;

            if (!TryResolveCanonicalDirectionKey(
                    dto.directionKey,
                    dto.stimulus,
                    out string canonicalDirection,
                    out string directionError))
            {
                error =
                    directionError;

                return false;
            }

            dto.directionKey =
                canonicalDirection;

            /*
             * Persist only canonical origin content.
             */
            dto.rawJson =
                canonicalJson;

            dto.seed =
                seed;

            dto.mode =
                mode;

            Normalize(
                dto);

            if (!Validate(
                    dto,
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
    private static bool TryResolveCanonicalDirectionKey(
    string rawDirection,
    string stimulus,
    out string directionKey,
    out string error)
    {
        directionKey =
            string.Empty;

        error =
            string.Empty;

        string clean =
            NormalizeKey(
                rawDirection);

        /*
         * Normal expected case:
         *
         * directionKey = "warden"
         */
        if (IsAllowedDirectionKey(
                clean))
        {
            directionKey =
                clean;

            return true;
        }

        /*
         * Known small-model schema-copy failure:
         *
         * directionKey =
         * "wanderer|stillness|custom"
         *
         * Do NOT arbitrarily select the first item.
         *
         * We only repair it when the model's independently generated
         * stimulus unambiguously names one of the supplied valid
         * alternatives.
         */
        if (!string.IsNullOrWhiteSpace(
                rawDirection) &&
            rawDirection.IndexOf(
                '|') >= 0)
        {
            string stimulusKey =
                NormalizeKey(
                    stimulus);

            if (IsAllowedDirectionKey(
                    stimulusKey) &&
                !string.Equals(
                    stimulusKey,
                    "custom",
                    StringComparison.OrdinalIgnoreCase))
            {
                string[] alternatives =
                    rawDirection.Split(
                        '|');

                int matchingAlternatives =
                    0;

                for (int i = 0;
                     i < alternatives.Length;
                     i++)
                {
                    string candidate =
                        NormalizeKey(
                            alternatives[i]);

                    if (!IsAllowedDirectionKey(
                            candidate))
                    {
                        continue;
                    }

                    if (string.Equals(
                            candidate,
                            stimulusKey,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matchingAlternatives++;
                    }
                }

                if (matchingAlternatives ==
                    1)
                {
                    directionKey =
                        stimulusKey;

                    return true;
                }
            }

            error =
                "directionKey contained multiple alternatives instead of one canonical value: '" +
                rawDirection +
                "'";

            return false;
        }

        error =
            "invalid directionKey '" +
            Safe(
                rawDirection,
                "<missing>") +
            "'";

        return false;
    }

    private static bool IsAllowedDirectionKey(
        string value)
    {
        switch (NormalizeKey(
                    value))
        {
            case "merchant":
            case "lumberjack":
            case "hero":
            case "demonlord":
            case "arcanist":
            case "warden":
            case "wanderer":
            case "stillness":
            case "custom":
                return true;

            default:
                return false;
        }
    }
    private static void Normalize(YQOriginGenerationDto dto)
    {
        dto.source = Safe(dto.source, "llm_origin_v1");
        dto.directionKey = NormalizeKey(Safe(dto.directionKey, "custom"));
        dto.stimulus = TrimTo(Safe(dto.stimulus, "the player's repeated answers"), 260);
        dto.className = TrimTo(dto.className, 64);
        dto.classDescription = TrimTo(dto.classDescription, 420);
        dto.titleName = TrimTo(dto.titleName, 64);
        dto.titleDescription = TrimTo(dto.titleDescription, 360);
        dto.identityKeywords ??= Array.Empty<string>();
        dto.stats ??= new YQOriginGeneratedStatsDto();
        dto.ability ??= new YQOriginGeneratedAbilityDto();
        dto.quest ??= new YQOriginGeneratedQuestDto();
        dto.loadout ??= Array.Empty<YQOriginGeneratedItemDto>();
        YQGoddessGenerationDialogue.Normalize(dto.goddessVoice);

        dto.stats.vitality = Mathf.Clamp(dto.stats.vitality <= 0 ? 10 : dto.stats.vitality, 8, 22);
        dto.stats.strength = Mathf.Clamp(dto.stats.strength <= 0 ? 10 : dto.stats.strength, 8, 22);
        dto.stats.dexterity = Mathf.Clamp(dto.stats.dexterity <= 0 ? 10 : dto.stats.dexterity, 8, 22);
        dto.stats.intelligence = Mathf.Clamp(dto.stats.intelligence <= 0 ? 10 : dto.stats.intelligence, 8, 22);

        dto.ability.name = TrimTo(dto.ability.name, 72);
        dto.ability.kind = NormalizeAbilityKind(dto.ability.kind);
        dto.ability.type = NormalizeKey(Safe(dto.ability.type, dto.ability.kind == "spell" ? "control" : "utility"));
        dto.ability.description = TrimTo(dto.ability.description, 440);
        dto.ability.targetingMode = NormalizeKey(Safe(dto.ability.targetingMode, dto.ability.kind == "spell" ? "projectile" : "melee"));
        dto.ability.resourceType = NormalizeKey(Safe(dto.ability.resourceType, dto.ability.kind == "spell" ? "mana" : "stamina"));
        dto.ability.resourceCost = Mathf.Clamp(dto.ability.resourceCost <= 0 ? 15 : dto.ability.resourceCost, 0, 60);
        dto.ability.cooldownSeconds = Mathf.Clamp(dto.ability.cooldownSeconds <= 0f ? 0.65f : dto.ability.cooldownSeconds, 0.15f, 12f);
        dto.ability.vfxFamily = NormalizeKey(Safe(dto.ability.vfxFamily, "arcane"));
        dto.ability.animationIntent = NormalizeKey(Safe(dto.ability.animationIntent, dto.ability.kind == "spell" ? "cast" : "melee"));

        dto.quest.name = TrimTo(dto.quest.name, 90);
        dto.quest.description = TrimTo(dto.quest.description, 520);
        dto.quest.tags = EnsureTags(dto.quest.tags, dto.directionKey);
        dto.quest.objectives = EnsureOriginObjectives(dto.quest.objectives);

        for (int i = 0; i < dto.loadout.Length; i++)
        {
            YQOriginGeneratedItemDto item = dto.loadout[i];
            if (item == null)
                continue;

            item.slot = NormalizeKey(item.slot);
            item.role = TrimTo(item.role, 100);
            item.nameHint = TrimTo(item.nameHint, 90);
            item.descriptionHint = TrimTo(item.descriptionHint, 260);
        }
    }

    private static bool Validate(YQOriginGenerationDto dto, out string error)
    {
        error =
            string.Empty;

        if (!IsAllowedDirectionKey(
                dto.directionKey))
        {
            error =
                "directionKey is not one canonical allowed value";
        }
        else if (string.IsNullOrWhiteSpace(
                     dto.className))
        {
            error =
                "missing className";
        }
        else if (string.IsNullOrWhiteSpace(dto.titleName))
            error = "missing titleName";
        else if (dto.ability == null || string.IsNullOrWhiteSpace(dto.ability.name))
            error = "missing ability.name";
        else if (dto.quest == null || string.IsNullOrWhiteSpace(dto.quest.name))
            error = "missing quest.name";
        else if (!HasUsableGeneratedLoadout(dto.loadout))
            error = "loadout needs at least three named, described items";

        return string.IsNullOrWhiteSpace(error);
    }

    private static bool HasUsableGeneratedLoadout(
        YQOriginGeneratedItemDto[] loadout)
    {
        if (loadout == null || loadout.Length < 3)
            return false;

        int usable = 0;
        for (int i = 0; i < loadout.Length; i++)
        {
            YQOriginGeneratedItemDto item = loadout[i];
            if (item == null ||
                !IsSupportedLoadoutSlot(item.slot) ||
                string.IsNullOrWhiteSpace(item.nameHint) ||
                string.IsNullOrWhiteSpace(item.descriptionHint))
            {
                continue;
            }

            usable++;
        }

        return usable >= 3;
    }

    private static bool IsSupportedLoadoutSlot(
        string slot)
    {
        switch (NormalizeKey(slot))
        {
            case "weapon":
            case "offhand":
            case "head":
            case "chest":
            case "gloves":
            case "legs":
            case "boots":
            case "belt":
            case "cloak":
            case "ring_left":
            case "ring_right":
            case "earring_left":
            case "earring_right":
            case "necklace":
            case "trinket":
            case "consumable":
                return true;

            default:
                return false;
        }
    }

    private static YQOriginGeneratedObjectiveDto[] EnsureOriginObjectives(YQOriginGeneratedObjectiveDto[] objectives)
    {
        List<YQOriginGeneratedObjectiveDto> list = new List<YQOriginGeneratedObjectiveDto>();
        if (objectives != null)
        {
            for (int i = 0; i < objectives.Length; i++)
            {
                YQOriginGeneratedObjectiveDto objective = objectives[i];
                if (objective == null)
                    continue;
                objective.type = NormalizeKey(objective.type);
                objective.targetId = Safe(objective.targetId, string.Empty);
                objective.targetName = Safe(objective.targetName, string.Empty);
                objective.counterKey = Safe(objective.counterKey, string.Empty);
                objective.counterPrefix = Safe(objective.counterPrefix, string.Empty);
                objective.description = TrimTo(objective.description, 180);
                objective.requiredCount = Mathf.Max(1f, objective.requiredCount <= 0f ? 1f : objective.requiredCount);
                list.Add(objective);
            }
        }

        EnsureObjective(list, "origin_manifested", "origin:equipment_manifested", string.Empty, string.Empty, "The goddess manifests gear from the player's answers.");
        EnsureObjective(list, "equip_item", string.Empty, "item:equip", string.Empty, "Equip one item from the manifested loadout.");
        EnsureObjective(list, "talk_to_npc", string.Empty, "dialogue:npc_archivist_01", "npc_archivist_01", "Speak with Archivist Vey beside the Goddess statue and witch hut.");
        return list.ToArray();
    }

    private static void EnsureObjective(List<YQOriginGeneratedObjectiveDto> list, string type, string counterKey, string counterPrefix, string targetId, string description)
    {
        for (int i = 0; i < list.Count; i++)
        {
            YQOriginGeneratedObjectiveDto objective = list[i];
            if (objective == null)
                continue;
            if (!string.Equals(objective.type, type, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(targetId) && !string.Equals(objective.targetId, targetId, StringComparison.OrdinalIgnoreCase))
                continue;
            return;
        }

        list.Add(new YQOriginGeneratedObjectiveDto
        {
            type = type,
            targetId = targetId,
            targetName = targetId == "npc_archivist_01" ? "Archivist Vey" : string.Empty,
            counterKey = counterKey,
            counterPrefix = counterPrefix,
            requiredCount = 1f,
            description = description
        });
    }

    private static string[] EnsureTags(string[] tags, string direction)
    {
        List<string> result = new List<string>();
        AddTag(result, "origin_generated");
        AddTag(result, "tutorial_main");
        AddTag(result, "player_response");
        AddTag(result, direction);
        if (tags != null)
        {
            for (int i = 0; i < tags.Length; i++)
                AddTag(result, tags[i]);
        }
        return result.ToArray();
    }

    private static void AddTag(List<string> tags, string tag)
    {
        string clean = NormalizeKey(tag);
        if (string.IsNullOrWhiteSpace(clean))
            return;
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], clean, StringComparison.OrdinalIgnoreCase))
                return;
        }
        tags.Add(clean);
    }

    private static string ExtractFirstJsonObject(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        string text = raw.Trim();
        int start = text.IndexOf('{');
        if (start < 0)
            return string.Empty;

        int end = FindRootObjectEnd(text, start);
        // note: Preserve an unterminated root through the response end so the parser can remove a cut-off presentation-only Goddess tail.
        return end > start
            ? text.Substring(start, end - start + 1)
            : text.Substring(start);
    }

    private static int FindRootObjectEnd(
        string text,
        int start)
    {
        int depth = 0;
        bool inString = false;
        bool escaped = false;

        for (int i = start; i < text.Length; i++)
        {
            char character = text[i];

            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (character == '\\')
                    escaped = true;
                else if (character == '"')
                    inString = false;

                continue;
            }

            if (character == '"')
            {
                inString = true;
                continue;
            }

            if (character == '{')
                depth++;
            else if (character == '}' && --depth == 0)
                return i;
        }

        return -1;
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
            if (TryCloseTruncatedJsonObject(
                    json,
                    out string completedJson))
            {
                try
                {
                    // note: A response ending on a complete value may be repaired only by closing its still-open JSON containers; invented content is never supplied.
                    root = JObject.Parse(completedJson);
                    return true;
                }
                catch
                {
                    // note: Strict optional-tail removal below remains the final bounded repair path when structural closure alone is insufficient.
                }
            }

            // note: Goddess presentation is optional; malformed voice JSON must not reject a usable origin.
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

    private static void PromoteNestedQuestProperty(
        JObject root,
        string propertyName)
    {
        if (root == null ||
            string.IsNullOrWhiteSpace(propertyName) ||
            (root[propertyName] != null &&
             root[propertyName].Type != JTokenType.Null) ||
            !(root["quest"] is JObject quest) ||
            quest[propertyName] == null)
        {
            return;
        }

        root[propertyName] = quest[propertyName].DeepClone();
        quest.Remove(propertyName);
    }

    private static bool TryCloseTruncatedJsonObject(
        string json,
        out string completedJson)
    {
        completedJson = json;
        if (string.IsNullOrWhiteSpace(json))
            return false;

        Stack<char> expectedClosers = new Stack<char>(4);
        bool inString = false;
        bool escaped = false;
        for (int index = 0; index < json.Length; index++)
        {
            char current = json[index];
            if (inString)
            {
                if (escaped)
                    escaped = false;
                else if (current == '\\')
                    escaped = true;
                else if (current == '"')
                    inString = false;
                continue;
            }

            if (current == '"')
            {
                inString = true;
                continue;
            }

            if (current == '{')
                expectedClosers.Push('}');
            else if (current == '[')
                expectedClosers.Push(']');
            else if (current == '}' || current == ']')
            {
                if (expectedClosers.Count == 0 ||
                    expectedClosers.Pop() != current)
                {
                    return false;
                }
            }
        }

        string trimmed = json.TrimEnd();
        if (inString ||
            expectedClosers.Count == 0 ||
            expectedClosers.Count > 4 ||
            trimmed.EndsWith(",", StringComparison.Ordinal) ||
            trimmed.EndsWith(":", StringComparison.Ordinal))
        {
            return false;
        }

        StringBuilder repaired = new StringBuilder(trimmed);
        while (expectedClosers.Count > 0)
            repaired.Append(expectedClosers.Pop());
        completedJson = repaired.ToString();
        return true;
    }

    private static bool TryRemoveJsonProperty(
        string json,
        string propertyName,
        out string repairedJson)
    {
        repairedJson =
            json;

        if (string.IsNullOrWhiteSpace(json) ||
            string.IsNullOrWhiteSpace(propertyName))
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

        int valueEnd =
            FindJsonValueEnd(
                json,
                valueStart);

        if (valueEnd <= valueStart)
        {
            // note: A cut-off trailing goddessVoice may contain no closing brace at all; rebuild the root from the already-complete canonical prefix and let strict parsing validate it.
            repairedJson =
                json.Substring(
                    0,
                    removeStart).TrimEnd();

            if (repairedJson.EndsWith(
                    ",",
                    StringComparison.Ordinal))
            {
                repairedJson =
                    repairedJson.Substring(
                        0,
                        repairedJson.Length - 1);
            }

            repairedJson +=
                "}";

            return true;
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

        // note: Remove only the optional presentation property, leaving canonical origin fields intact.
        repairedJson =
            json.Remove(
                removeStart,
                removeEnd - removeStart);

        return true;
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
            else if (open == '{' &&
                     c == '}' &&
                     depth <= 1)
            {
                return i + 1;
            }
        }

        return -1;
    }

    private static string BuildLedger(PlayerState state)
    {
        if (state == null || state.behaviorLedger == null || state.behaviorLedger.Count == 0)
            return "No behavior recorded before origin.";

        int start = Mathf.Max(0, state.behaviorLedger.Count - 12);
        StringBuilder sb = new StringBuilder();
        for (int i = start; i < state.behaviorLedger.Count; i++)
            sb.AppendLine(state.behaviorLedger[i]);
        return sb.ToString();
    }

    private static string BuildCharacterCreationBlock(PlayerState state)
    {
        if (state == null)
            return "name: The Player\npronouns: unspecified\nbodyFrame: unspecified\nlifeDirection: unspecified\nvow: unspecified\nappearance: unspecified";

        return
            "name: " + Safe(state.displayName, "The Player") + "\n" +
            "pronouns: " + Safe(state.characterPronouns, "unspecified") + "\n" +
            "bodyFrame: " + Safe(state.characterBodyFrame, "unspecified") + "\n" +
            "lifeDirection: " + Safe(state.characterLifeDirection, "unspecified") + "\n" +
            "vow: " + Safe(state.characterVow, "unspecified") + "\n" +
            "appearance: " + Safe(state.characterAppearanceSummary, "unspecified") + "\n" +
            "characterCreationSeed: " + Safe(state.characterCreationSeed, "none");
    }

    private static string BuildOriginSeed(PlayerState state, string mode, IReadOnlyList<string> answers)
    {
        unchecked
        {
            int hash = 23;
            string text = (state != null ? state.playerId : "player") + "|" + Safe(mode, "mode");
            if (state != null)
            {
                text += "|" + Safe(state.displayName, string.Empty) +
                        "|" + Safe(state.characterPronouns, string.Empty) +
                        "|" + Safe(state.characterBodyFrame, string.Empty) +
                        "|" + Safe(state.characterLifeDirection, string.Empty) +
                        "|" + Safe(state.characterVow, string.Empty) +
                        "|" + Safe(state.characterAppearanceSummary, string.Empty);
            }
            for (int i = 0; i < text.Length; i++)
                hash = hash * 31 + text[i];
            if (answers != null)
            {
                for (int a = 0; a < answers.Count; a++)
                {
                    string answer = answers[a] ?? string.Empty;
                    for (int i = 0; i < answer.Length; i++)
                        hash = hash * 31 + answer[i];
                }
            }
            return Mathf.Abs(hash).ToString("x8");
        }
    }

    private static string NormalizeAbilityKind(string kind)
    {
        string clean = NormalizeKey(kind);
        return clean == "spell" ? "spell" : "skill";
    }

    private static string NormalizeKey(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant().Replace(' ', '_');
    }

    private static string Safe(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string TrimTo(string value, int max)
    {
        string clean = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return clean.Length <= max ? clean : clean.Substring(0, max).TrimEnd();
    }
}

[Serializable]
public sealed class YQOriginGenerationDto
{
    public string source;
    public string seed;
    public string mode;
    public string directionKey;
    public string stimulus;
    public string[] identityKeywords;
    public YQOriginGeneratedStatsDto stats;
    public string className;
    public string classDescription;
    public string titleName;
    public string titleDescription;
    public YQOriginGeneratedAbilityDto ability;
    public YQOriginGeneratedQuestDto quest;
    public YQOriginGeneratedItemDto[] loadout;

    public YQGoddessGenerationVoiceDto goddessVoice;
    public string rawJson;
}

[Serializable]
public sealed class YQOriginGeneratedStatsDto
{
    public int vitality = 10;
    public int strength = 10;
    public int dexterity = 10;
    public int intelligence = 10;
}

[Serializable]
public sealed class YQOriginGeneratedAbilityDto
{
    public string name;
    public string kind;
    public string type;
    public string description;
    public string targetingMode;
    public string resourceType;
    public int resourceCost;
    public float cooldownSeconds;
    public string vfxFamily;
    public string animationIntent;
}

[Serializable]
public sealed class YQOriginGeneratedQuestDto
{
    public string name;
    public string description;
    public string[] tags;
    public YQOriginGeneratedObjectiveDto[] objectives;
}

[Serializable]
public sealed class YQOriginGeneratedObjectiveDto
{
    public string type;
    public string targetId;
    public string targetName;
    public string counterKey;
    public string counterPrefix;
    public float requiredCount = 1f;
    public string description;
}

[Serializable]
public sealed class YQOriginGeneratedItemDto
{
    public string slot;
    public string role;
    public string nameHint;
    public string descriptionHint;
}
