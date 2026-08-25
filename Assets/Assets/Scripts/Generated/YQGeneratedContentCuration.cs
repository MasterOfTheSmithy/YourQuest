using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class YQGeneratedContentCuration
{
    public const string NaturePrecursorName = "Auralith, the First Green";

    private static readonly HashSet<string> ForbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ability",
        "class",
        "generated ability",
        "generated class",
        "generated quest",
        "generated skill",
        "item",
        "measured step",
        "measured movement",
        "movement action",
        "movement action step",
        "movement",
        "movement step",
        "movement skill",
        "move step",
        "n/a",
        "new action",
        "new ability",
        "new quest",
        "new skill",
        "none",
        "null",
        "placeholder",
        "quest",
        "skill",
        "spell",
        "step",
        "test",
        "test quest",
        "test skill",
        "title",
        "unknown",
        "unknown quest",
        "unknown skill"
    };

    private static readonly HashSet<string> GenericTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "action", "activity", "ability", "basic", "class", "combat", "generated", "generic", "item",
        "move", "movement", "new", "npc", "output", "placeholder", "progression", "quest", "skill",
        "spell", "step", "system", "test", "thing", "title", "unknown", "region", "zone", "biome",
        "area", "fluff", "lore"
    };

    private static readonly HashSet<string> GenericTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "generated", "llm_generated", "world", "region", "regions", "generic", "placeholder", "fluff"
    };

    private static readonly HashSet<string> AllowedSingleWordSkillNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "barrier", "blink", "dash", "fireball", "guard", "heal", "lunge", "parry", "roll", "slash", "vault"
    };

    private static readonly string[] RegionNameTokens =
    {
        "whisperroot", "frostglass", "cinderfall", "verdant", "tideglass",
        "origin forest", "origin_forest", "region ice", "region fire", "region jungle", "region water",
        "ice north", "fire east", "jungle south", "water west"
    };

    private static readonly string[] PlayerStimulusTokens =
    {
        "player", "you", "your", "recent", "observed", "after", "when", "while", "against", "under",
        "respond", "response", "answers", "converts", "turns", "practice", "earned", "instinct",
        "pressure", "threat", "survive", "survival"
    };

    private static readonly string[] NatureContextTokens =
    {
        "jungle", "forest", "wild", "wilds", "grass", "root", "leaf", "leaves", "thorn", "vine",
        "wood", "tree", "natural", "nature", "verdant", "green", "poison", "venom"
    };

    private static readonly string[] MeaningTriggerTokens =
    {
        "player", "you", "your", "when", "after", "while", "under", "against", "responds", "trigger",
        "pressure", "threat", "risk", "repeated", "choice", "choices", "habit", "pattern"
    };

    private static readonly string[] SkillEffectTokens =
    {
        "strike", "guard", "block", "counter", "recover", "recovery", "restore", "reduce", "increase",
        "control", "range", "timing", "stagger", "interrupt", "reveal", "detect", "dodge", "dash",
        "cleave", "pulse", "shield", "mana", "health", "stamina", "speed", "root", "poison", "damage"
    };

    private static readonly string[] QuestObjectiveTokens =
    {
        "leave", "reach", "return", "survive", "defeat", "open", "choose", "prove", "recover",
        "read", "mark", "complete", "stabilize", "protect", "take", "face"
    };

    private static readonly string[] QuestStakesTokens =
    {
        "risk", "danger", "threat", "prove", "survive", "reward", "choice", "pressure", "test", "contact"
    };

    private static readonly string[] IdentityConsequenceTokens =
    {
        "earned", "marks", "marked", "identity", "choices", "repeated", "habit", "pattern",
        "pressure", "survival", "origin", "proves", "curated", "class", "title"
    };

    private static readonly string[] OddityTokens =
    {
        "goofy", "silly", "joke", "jester", "clown", "banana", "spoon", "noodle", "bonk",
        "boop", "wobble", "rubber", "cheese", "sock", "meme", "absurd", "ridiculous", "prank"
    };

    public static string CuratePlayerFacingDescription(
        PlayerState state,
        string kind,
        string name,
        string description,
        string typeOrContext,
        bool isSpell,
        string stimulus = "",
        string loreAnchor = "")
    {
        string normalizedKind = NormalizeKind(kind);
        string clean = CompactWhitespace(description);
        string cleanName = CompactWhitespace(name);
        string cleanType = CompactWhitespace(typeOrContext).ToLowerInvariant();
        string cleanStimulus = CompactWhitespace(stimulus);
        string cleanLoreAnchor = CompactWhitespace(loreAnchor);

        bool needsFallback =
            string.IsNullOrWhiteSpace(clean) ||
            LooksLikeThinGenericDescription(clean, normalizedKind) ||
            LooksRegionSeededDescription(clean);

        if (needsFallback)
        {
            clean = BuildFallbackPlayerDescription(state, normalizedKind, cleanName, cleanType, isSpell, cleanStimulus);
        }

        // note: Accepted Llama prose remains authored player-facing content; only absent or unusable prose receives mechanical fallback scaffolding.
        if (!needsFallback)
            return clean;

        if (!ReferencesPlayerStimulus(clean))
        {
            string stimulusLine = string.IsNullOrWhiteSpace(cleanStimulus)
                ? SummarizePlayerStimulus(state, cleanType, isSpell)
                : cleanStimulus;
            clean = AppendSentence(clean, "It responds to " + stimulusLine + ".");
        }

        string combined = cleanName + " " + clean + " " + cleanType + " " + cleanStimulus + " " + cleanLoreAnchor;
        if (LooksLikeNatureContext(combined) && clean.IndexOf("Auralith", StringComparison.OrdinalIgnoreCase) < 0)
        {
            string anchor = string.IsNullOrWhiteSpace(cleanLoreAnchor) ? NaturePrecursorName : cleanLoreAnchor;
            clean = AppendSentence(clean, anchor + " is the old precursor name behind this natural pattern.");
        }

        if (!HasMeaningfulConsequence(clean, normalizedKind, isSpell, Array.Empty<string>()))
            clean = AppendSentence(clean, BuildMeaningfulConsequence(normalizedKind, cleanType, cleanStimulus, isSpell));

        if (IsOddityCandidate(cleanName, clean, Array.Empty<string>()) && clean.IndexOf("dormant oddity", StringComparison.OrdinalIgnoreCase) < 0)
            clean = AppendSentence(clean, "It remains a dormant oddity until the pattern repeats enough to evolve into a real technique.");

        return clean;
    }

    public static string[] BuildPlayerResponseTags(string[] tags, string typeOrContext, bool isSpell, string text = "")
    {
        List<string> clean = new List<string>(CleanTags(tags));
        AddTag(clean, "player_response");
        AddTag(clean, "earned");

        string type = CompactWhitespace(typeOrContext).ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(type))
            AddTag(clean, type);
        if (isSpell)
            AddTag(clean, "spell");
        if (LooksLikeNatureContext(type + " " + text))
        {
            AddTag(clean, "nature_precursor");
            AddTag(clean, "auralith");
        }
        if (IsOddityCandidate(text, string.Empty, clean.ToArray()))
            AddTag(clean, "oddity_seed");
        AddTag(clean, "deterministic");
        AddTag(clean, "meaningful");

        return clean.ToArray();
    }

    public static string[] AddProgressionTags(string[] tags, params string[] extraTags)
    {
        List<string> clean = new List<string>(CleanTags(tags));
        if (extraTags != null)
        {
            for (int i = 0; i < extraTags.Length; i++)
                AddTag(clean, extraTags[i]);
        }

        return clean.ToArray();
    }

    public static bool IsOddityCandidate(string name, string description, string[] tags)
    {
        string tagText = tags != null ? string.Join(" ", tags) : string.Empty;
        return ContainsAnyPhrase((name ?? string.Empty) + " " + (description ?? string.Empty) + " " + tagText, OddityTokens);
    }

    public static string CuratePlayerFacingName(
        PlayerState state,
        string kind,
        string name,
        string typeOrContext,
        bool isSpell,
        string stimulus = "")
    {
        string normalizedKind = NormalizeKind(kind);
        string clean = CompactWhitespace(name);
        string normalizedName = NormalizeText(clean);
        bool regionNamed = ContainsAnyPhrase(normalizedName, RegionNameTokens);

        if (LooksLikeBadName(clean, normalizedKind, out _) || regionNamed)
            return BuildFallbackPlayerFacingName(state, normalizedKind, typeOrContext, isSpell, stimulus);

        return clean;
    }

    public static bool PassesOfferQuality(
        PlayerState state,
        string kind,
        string name,
        string description,
        string[] tags,
        float confidence,
        bool checkDuplicates,
        out string reason)
    {
        if (!PassesBasicQuality(kind, name, description, tags, confidence, out reason))
            return false;

        if (checkDuplicates && IsTooSimilarToExisting(state, kind, name, description, tags, out string existingName))
        {
            reason = "Rejected near-duplicate " + NormalizeKind(kind) + " of " + existingName + ".";
            return false;
        }

        reason = "Accepted.";
        return true;
    }

    public static bool PassesBasicQuality(
        string kind,
        string name,
        string description,
        string[] tags,
        float confidence,
        out string reason)
    {
        string normalizedKind = NormalizeKind(kind);
        string cleanName = CompactWhitespace(name);
        string cleanDescription = CompactWhitespace(description);
        string[] cleanTags = CleanTags(tags);

        if (confidence < MinConfidenceForKind(normalizedKind))
        {
            reason = "Rejected low-confidence " + normalizedKind + ".";
            return false;
        }

        if (LooksLikeBadName(cleanName, normalizedKind, out reason))
            return false;

        if (LooksLikeBadDescription(cleanDescription, normalizedKind, cleanTags, out reason))
            return false;

        bool oddity = IsOddityCandidate(cleanName, cleanDescription, cleanTags);
        if (oddity && !HasTag(cleanTags, "evolved_oddity"))
        {
            reason = "Rejected unevolved oddity.";
            return false;
        }

        if (!HasMeaningfulConsequence(cleanDescription, normalizedKind, normalizedKind == "spell", cleanTags))
        {
            reason = "Rejected low-impact " + normalizedKind + ".";
            return false;
        }

        reason = "Accepted.";
        return true;
    }

    public static int CleanExistingState(PlayerState state)
    {
        if (state == null)
            return 0;

        state.EnsureCollections();
        int removed = 0;
        bool repaired = false;
        if (state.pendingOffers != null)
        {
            for (int i = state.pendingOffers.Count - 1; i >= 0; i--)
            {
                PendingProgressionOfferRecord offer = state.pendingOffers[i];
                if (offer == null)
                {
                    state.pendingOffers.RemoveAt(i);
                    removed++;
                    continue;
                }

                if (!offer.IsPending)
                    continue;

                string offerKind = offer.isSpell || string.Equals(offer.offerKind, "spell", StringComparison.OrdinalIgnoreCase)
                    ? "spell"
                    : offer.offerKind;
                string previousName = offer.name;
                offer.name = CuratePlayerFacingName(state, offerKind, offer.name, offer.skillType, offer.isSpell, offer.reason);
                repaired |= !string.Equals(previousName, offer.name, StringComparison.Ordinal);

                if (!PassesBasicQuality(offer.offerKind, offer.name, offer.description, offer.tags, offer.confidence, out _))
                {
                    state.pendingOffers.RemoveAt(i);
                    removed++;
                }
            }
        }

        HashSet<string> removedSkillIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (state.skills != null)
        {
            for (int i = state.skills.Count - 1; i >= 0; i--)
            {
                SkillRecord skill = state.skills[i];
                if (skill != null)
                {
                    string previousName = skill.name;
                    skill.name = CuratePlayerFacingName(state, skill.isSpell ? "spell" : "skill", skill.name, skill.type, skill.isSpell, skill.context);
                    repaired |= !string.Equals(previousName, skill.name, StringComparison.Ordinal);
                }
                if (ShouldCullCommittedSkill(skill))
                {
                    if (skill != null && !string.IsNullOrWhiteSpace(skill.skillId))
                        removedSkillIds.Add(skill.skillId);
                    state.skills.RemoveAt(i);
                    removed++;
                }
            }
        }

        if (removedSkillIds.Count > 0 && state.equippedSkillBySlot != null)
        {
            List<string> slotsToClear = new List<string>(state.equippedSkillBySlot.Count);
            foreach (KeyValuePair<string, string> pair in state.equippedSkillBySlot)
            {
                if (!string.IsNullOrWhiteSpace(pair.Value) && removedSkillIds.Contains(pair.Value))
                    slotsToClear.Add(pair.Key);
            }

            for (int i = 0; i < slotsToClear.Count; i++)
                state.equippedSkillBySlot[slotsToClear[i]] = string.Empty;
        }

        if (state.quests != null)
        {
            for (int i = state.quests.Count - 1; i >= 0; i--)
            {
                QuestRecord quest = state.quests[i];
                if (quest != null)
                {
                    string previousName = quest.name;
                    quest.name = CuratePlayerFacingName(state, "quest", quest.name, "quest", false, quest.description);
                    repaired |= !string.Equals(previousName, quest.name, StringComparison.Ordinal);
                }
                if (ShouldCullCommittedQuest(quest))
                {
                    if (quest != null && string.Equals(state.activeQuestId, quest.questId, StringComparison.OrdinalIgnoreCase))
                        state.activeQuestId = string.Empty;
                    state.quests.RemoveAt(i);
                    removed++;
                }
            }
        }

        state.GetActiveQuest();
        if (removed > 0 || repaired)
            state.Touch();
        return removed;
    }

    public static string[] CleanTags(string[] tags)
    {
        if (tags == null || tags.Length == 0)
            return Array.Empty<string>();

        List<string> clean = new List<string>(tags.Length);
        for (int i = 0; i < tags.Length; i++)
        {
            string tag = CompactWhitespace(tags[i]).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(tag))
                continue;
            if (GenericTags.Contains(tag) || LooksLikeRegionTag(tag))
                continue;
            if (tag.Length > 32)
                tag = tag.Substring(0, 32).Trim();
            if (!clean.Contains(tag))
                clean.Add(tag);
        }

        return clean.ToArray();
    }

    private static bool ShouldCullCommittedSkill(SkillRecord skill)
    {
        if (skill == null)
            return true;

        string kind = skill.isSpell ? "spell" : "skill";
        string name = CompactWhitespace(skill.name);
        string description = CompactWhitespace(skill.description);
        if (LooksLikeBadName(name, kind, out _))
            return true;

        string[] tags = BuildSkillTags(skill);
        if (LooksLikeBadDescription(description, kind, tags, out string reason))
            return reason.IndexOf("generic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   reason.IndexOf("malformed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   LooksLikeThinGenericDescription(description, kind);

        return false;
    }

    private static bool ShouldCullCommittedQuest(QuestRecord quest)
    {
        if (quest == null)
            return true;

        string name = CompactWhitespace(quest.name);
        string description = CompactWhitespace(quest.description);
        if (LooksLikeBadName(name, "quest", out _))
            return true;
        if (ContainsMarkupOrJson(description) || HasRepeatedCharacters(description, 5))
            return true;
        return LooksLikeThinGenericDescription(description, "quest");
    }

    private static bool LooksLikeBadName(string name, string kind, out string reason)
    {
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = "Rejected unnamed " + kind + ".";
            return true;
        }

        if (name.Length < 3 || name.Length > 64)
        {
            reason = "Rejected " + kind + " with invalid name length.";
            return true;
        }

        string normalized = NormalizeText(name);
        if (ForbiddenNames.Contains(normalized))
        {
            reason = "Rejected placeholder " + kind + " name.";
            return true;
        }

        if (ContainsMarkupOrJson(name) || HasTooManySymbols(name) || HasRepeatedCharacters(name, 4))
        {
            reason = "Rejected malformed " + kind + " name.";
            return true;
        }

        string[] tokens = Tokenize(name);
        if (tokens.Length == 0)
        {
            reason = "Rejected unreadable " + kind + " name.";
            return true;
        }

        int genericCount = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (GenericTokens.Contains(tokens[i]))
                genericCount++;
        }

        bool isSkillLike = kind == "skill" || kind == "spell";
        if ((isSkillLike || kind == "class" || kind == "title") && LooksLikeRegionNamedProgression(normalized))
        {
            reason = "Rejected region-named " + kind + ".";
            return true;
        }

        if (!isSkillLike && tokens.Length < 2)
        {
            reason = "Rejected under-specified " + kind + " name.";
            return true;
        }

        if (isSkillLike && tokens.Length == 1 && !AllowedSingleWordSkillNames.Contains(tokens[0]))
        {
            reason = "Rejected vague single-word skill name.";
            return true;
        }

        if (genericCount >= tokens.Length || (tokens.Length <= 2 && genericCount > 0 && !isSkillLike))
        {
            reason = "Rejected generic " + kind + " name.";
            return true;
        }

        if (isSkillLike && tokens.Length <= 4 && genericCount >= Mathf.Max(1, tokens.Length - 1))
        {
            reason = "Rejected generic " + kind + " name.";
            return true;
        }

        if (LooksLikeNumberedPlaceholder(tokens))
        {
            reason = "Rejected placeholder " + kind + " name.";
            return true;
        }

        return false;
    }

    private static bool LooksLikeBadDescription(string description, string kind, string[] tags, out string reason)
    {
        reason = string.Empty;
        int letters = CountLetters(description);
        bool isQuest = kind == "quest";
        bool isSkillLike = kind == "skill" || kind == "spell";
        int minimumLetters = isQuest ? 32 : isSkillLike ? 24 : 18;

        if (letters < minimumLetters)
        {
            reason = "Rejected under-described " + kind + ".";
            return true;
        }

        if (ContainsMarkupOrJson(description) || HasRepeatedCharacters(description, 5))
        {
            reason = "Rejected malformed " + kind + " description.";
            return true;
        }

        if (isQuest && (tags == null || tags.Length == 0))
        {
            reason = "Rejected quest without grounding tags.";
            return true;
        }

        if (LooksLikeThinGenericDescription(description, kind))
        {
            reason = "Rejected generic " + kind + " description.";
            return true;
        }

        if (LooksRegionDetachedDescription(description, kind))
        {
            reason = "Rejected region-detached " + kind + " description.";
            return true;
        }

        return false;
    }

    private static bool LooksLikeNumberedPlaceholder(string[] tokens)
    {
        if (tokens == null || tokens.Length == 0)
            return false;

        int genericCount = 0;
        int numberCount = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (GenericTokens.Contains(tokens[i]))
                genericCount++;
            if (int.TryParse(tokens[i], out _))
                numberCount++;
        }

        return numberCount > 0 && genericCount >= Mathf.Max(1, tokens.Length - numberCount - 1);
    }

    private static bool LooksLikeThinGenericDescription(string description, string kind)
    {
        string[] tokens = Tokenize(description);
        if (tokens.Length == 0)
            return true;

        int genericCount = 0;
        for (int i = 0; i < tokens.Length; i++)
        {
            if (GenericTokens.Contains(tokens[i]))
                genericCount++;
        }

        if (tokens.Length <= 8 && genericCount >= 2)
            return true;

        string normalized = NormalizeText(description);
        if (normalized.Contains("movement step") ||
            normalized.Contains("movement action") ||
            normalized.Contains("generated ability") ||
            normalized.Contains("generic ability") ||
            normalized.Contains("placeholder ability") ||
            normalized.Contains("placeholder quest") ||
            normalized.Contains("emergent technique inferred"))
            return true;

        if (normalized.Contains("generated from recent behavior") ||
            normalized.Contains("inferred from recent behavior") ||
            normalized.Contains("based on observed behavior"))
            return true;

        return false;
    }

    private static bool LooksRegionDetachedDescription(string description, string kind)
    {
        string normalizedKind = NormalizeKind(kind);
        if (normalizedKind == "quest")
            return false;

        string normalized = NormalizeText(description);
        if (LooksRegionSeededDescription(normalized))
            return true;

        return ContainsAnyPhrase(normalized, RegionNameTokens) && !ContainsAnyPhrase(normalized, PlayerStimulusTokens);
    }

    private static bool LooksRegionSeededDescription(string description)
    {
        string normalized = NormalizeText(description);
        return normalized.Contains("current region") ||
               normalized.Contains("region seed") ||
               normalized.Contains("region and level seed") ||
               normalized.Contains("this run s region") ||
               normalized.Contains("aligned to this run") ||
               normalized.Contains("generated from") ||
               normalized.Contains("generated by") ||
               normalized.Contains("generated main hand") ||
               normalized.Contains("generated armor") ||
               normalized.Contains("generated accessory") ||
               normalized.Contains("generated consumable") ||
               normalized.Contains("generated title") ||
               normalized.Contains("generated identity") ||
               normalized.Contains("investor prototype");
    }

    private static bool LooksLikeRegionNamedProgression(string normalizedName)
    {
        return ContainsAnyPhrase(normalizedName, RegionNameTokens);
    }

    private static bool LooksLikeRegionTag(string tag)
    {
        string normalized = NormalizeText(tag);
        if (normalized.StartsWith("region ", StringComparison.OrdinalIgnoreCase))
            return true;

        return ContainsAnyPhrase(normalized, RegionNameTokens);
    }

    private static string BuildFallbackPlayerFacingName(PlayerState state, string kind, string typeOrContext, bool isSpell, string stimulus)
    {
        string motif = ResolveCurationMotif(state, typeOrContext, stimulus, isSpell);
        string type = CompactWhitespace(typeOrContext).ToLowerInvariant();

        if (kind == "spell" || isSpell)
        {
            if (motif == "Auralith")
                return "Auralith's Green Pulse";
            if (motif == "Oathbound")
                return "Threshold Ward";
            if (motif == "Breakstep")
                return "Breakstep Pulse";
            if (motif == "Keenhand")
                return "Intent Lantern";
            return "Linebreak Pulse";
        }

        if (kind == "skill")
        {
            if (motif == "Auralith")
                return type.Contains("guard") || type.Contains("defense") ? "Greenhand Guard" : "Greenhand Recovery";
            if (motif == "Oathbound")
                return "Oathbound Guard";
            if (motif == "Breakstep")
                return "Breakstep Recovery";
            if (motif == "Keenhand")
                return "Keenhand Read";
            return "Linebreaker Timing";
        }

        if (kind == "class")
            return motif == "Auralith" ? "Greenhand Warden" : motif + " Warden";
        if (kind == "title")
            return motif == "Auralith" ? "Green-Witnessed" : motif + " Marked";
        if (kind == "quest")
            return "Prove the Pattern";

        return motif + " Response";
    }

    private static string ResolveCurationMotif(PlayerState state, string typeOrContext, string stimulus, bool isSpell)
    {
        string combined = BuildIdentityText(state) + " " + CompactWhitespace(typeOrContext).ToLowerInvariant() + " " + CompactWhitespace(stimulus).ToLowerInvariant();
        if (LooksLikeNatureContext(combined))
            return "Auralith";
        if (ContainsAnyPhrase(combined, "protect", "guard", "shield", "mercy", "ward"))
            return "Oathbound";
        if (ContainsAnyPhrase(combined, "dash", "dodge", "road", "mobile", "speed", "movement"))
            return "Breakstep";
        if (ContainsAnyPhrase(combined, "merchant", "market", "coin", "trade", "dialogue", "intent"))
            return "Keenhand";
        if (isSpell || ContainsAnyPhrase(combined, "spell", "magic", "mana", "rune"))
            return "Threshold";
        return "Linebreaker";
    }

    private static string BuildFallbackPlayerDescription(PlayerState state, string kind, string name, string typeOrContext, bool isSpell, string stimulus)
    {
        string trigger = string.IsNullOrWhiteSpace(stimulus)
            ? SummarizePlayerStimulus(state, typeOrContext, isSpell)
            : stimulus;

        switch (kind)
        {
            case "spell":
                return "A compact threshold spell shaped around " + trigger + ", small enough for the first tutorial road but clear enough for the Archive to recognize.";
            case "skill":
                return "A practiced technique shaped around " + trigger + ", turning a repeated response into something the old system can name.";
            case "class":
                return "A curated class identity shaped by your origin answers, repeated choices, and survival habits at the edge of a god-made threshold.";
            case "title":
                return "A title earned from the way your choices keep repeating under pressure, visible to mentors, quests, and future offers.";
            case "quest":
                return "A focused objective that asks you to prove what your recent choices already started, then bring the result back into the Archive of First Roads.";
            default:
                return string.IsNullOrWhiteSpace(name)
                    ? "A player-facing progression offer shaped by recent behavior at the threshold."
                    : name + " is shaped by recent behavior at the threshold.";
        }
    }

    private static string BuildMeaningfulConsequence(string kind, string typeOrContext, string stimulus, bool isSpell)
    {
        string trigger = string.IsNullOrWhiteSpace(stimulus)
            ? "your repeated pattern under pressure"
            : stimulus;
        string type = CompactWhitespace(typeOrContext).ToLowerInvariant();

        if (kind == "spell" || isSpell)
            return "When triggered by " + trigger + ", it creates a readable mana effect with a clear recovery or control window";
        if (kind == "skill")
        {
            if (type.Contains("movement"))
                return "When triggered by " + trigger + ", it improves recovery timing after a committed move";
            if (type.Contains("craft") || type.Contains("utility"))
                return "When triggered by " + trigger + ", it reveals a practical opening or stabilizes the next action";
            return "When triggered by " + trigger + ", it turns the next committed action into a clearer strike, guard, or recovery";
        }
        if (kind == "quest")
            return "Objective: prove the pattern through one concrete risk, then return with the result so the Archive can separate destiny from noise";
        if (kind == "class")
            return "It matters by shaping future offers toward the choices you repeatedly prove at the threshold";
        if (kind == "title")
            return "It matters by marking a repeated choice other systems, mentors, and quests can recognize later";

        return "It matters by creating a repeatable consequence instead of a one-off label";
    }

    private static bool HasMeaningfulConsequence(string description, string kind, bool isSpell, string[] tags)
    {
        string normalizedKind = NormalizeKind(kind);
        string normalized = NormalizeText(description + " " + (tags != null ? string.Join(" ", tags) : string.Empty));

        if (normalizedKind == "skill" || normalizedKind == "spell" || isSpell)
            return ContainsAnyPhrase(normalized, MeaningTriggerTokens) && ContainsAnyPhrase(normalized, SkillEffectTokens);

        if (normalizedKind == "quest")
            return ContainsAnyPhrase(normalized, QuestObjectiveTokens) && ContainsAnyPhrase(normalized, QuestStakesTokens);

        if (normalizedKind == "title" || normalizedKind == "class")
            return ContainsAnyPhrase(normalized, IdentityConsequenceTokens);

        return ContainsAnyPhrase(normalized, MeaningTriggerTokens);
    }

    private static string SummarizePlayerStimulus(PlayerState state, string typeOrContext, bool isSpell)
    {
        string type = CompactWhitespace(typeOrContext).ToLowerInvariant();
        string identity = BuildIdentityText(state);
        string combined = type + " " + identity;

        if (isSpell || combined.Contains("spell") || combined.Contains("magic") || combined.Contains("mana"))
            return "your instinct to answer pressure with shaped mana";
        if (combined.Contains("movement") || combined.Contains("mobile") || combined.Contains("dash") || combined.Contains("dodge"))
            return "your movement choices under pressure";
        if (combined.Contains("craft") || combined.Contains("tool") || combined.Contains("wood") || combined.Contains("forge"))
            return "your habit of turning tools and terrain into answers";
        if (combined.Contains("social") || combined.Contains("merchant") || combined.Contains("dialogue"))
            return "how you test intent before you commit";
        if (LooksLikeNatureContext(combined))
            return "your survival instinct around living terrain";

        return "your repeated close-range pressure and recovery windows";
    }

    private static string BuildIdentityText(PlayerState state)
    {
        if (state == null)
            return string.Empty;

        state.EnsureCollections();
        StringBuilder sb = new StringBuilder();
        if (state.identityKeywords != null)
        {
            for (int i = 0; i < state.identityKeywords.Count; i++)
                sb.Append(' ').Append(state.identityKeywords[i]);
        }
        if (state.originQuestionnaireAnswers != null)
        {
            int start = Mathf.Max(0, state.originQuestionnaireAnswers.Count - 6);
            for (int i = start; i < state.originQuestionnaireAnswers.Count; i++)
                sb.Append(' ').Append(state.originQuestionnaireAnswers[i]);
        }

        return sb.ToString().ToLowerInvariant();
    }

    private static bool ReferencesPlayerStimulus(string description)
    {
        return ContainsAnyPhrase(NormalizeText(description), PlayerStimulusTokens);
    }

    private static bool LooksLikeNatureContext(string text)
    {
        return ContainsAnyPhrase(NormalizeText(text), NatureContextTokens);
    }

    private static string AppendSentence(string description, string sentence)
    {
        string clean = CompactWhitespace(description);
        string addition = CompactWhitespace(sentence);
        if (string.IsNullOrWhiteSpace(addition))
            return clean;
        if (clean.IndexOf(addition, StringComparison.OrdinalIgnoreCase) >= 0)
            return clean;
        if (string.IsNullOrWhiteSpace(clean))
            return addition;
        return clean.TrimEnd('.', ' ') + ". " + addition;
    }

    private static void AddTag(List<string> tags, string tag)
    {
        string clean = CompactWhitespace(tag).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(clean) || GenericTags.Contains(clean) || LooksLikeRegionTag(clean))
            return;
        if (!tags.Contains(clean))
            tags.Add(clean);
    }

    private static bool HasTag(string[] tags, string expected)
    {
        if (tags == null || string.IsNullOrWhiteSpace(expected))
            return false;

        for (int i = 0; i < tags.Length; i++)
        {
            if (string.Equals(tags[i], expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsTooSimilarToExisting(PlayerState state, string kind, string name, string description, string[] tags, out string existingName)
    {
        existingName = string.Empty;
        if (state == null)
            return false;

        state.EnsureCollections();
        string normalizedKind = NormalizeKind(kind);
        float threshold = normalizedKind == "quest" ? 0.86f : normalizedKind == "skill" || normalizedKind == "spell" ? 0.90f : 0.88f;

        if (normalizedKind == "skill" || normalizedKind == "spell")
        {
            for (int i = 0; i < state.skills.Count; i++)
            {
                SkillRecord skill = state.skills[i];
                if (skill == null)
                    continue;
                float score = SkillSimilarity.Score(name, description, tags, skill.name, skill.description, BuildSkillTags(skill));
                if (score >= threshold || SameNormalizedName(name, skill.name))
                {
                    existingName = skill.name;
                    return true;
                }
            }
        }
        else if (normalizedKind == "quest")
        {
            for (int i = 0; i < state.quests.Count; i++)
            {
                QuestRecord quest = state.quests[i];
                if (quest == null)
                    continue;
                float score = SkillSimilarity.Score(name, description, tags, quest.name, quest.description, quest.tags);
                if (score >= threshold || SameNormalizedName(name, quest.name))
                {
                    existingName = quest.name;
                    return true;
                }
            }
        }
        else if (normalizedKind == "class")
        {
            for (int i = 0; i < state.classes.Count; i++)
            {
                ClassRecord record = state.classes[i];
                if (record == null)
                    continue;
                float score = SkillSimilarity.Score(name, description, tags, record.name, record.description, Array.Empty<string>());
                if (score >= threshold || SameNormalizedName(name, record.name))
                {
                    existingName = record.name;
                    return true;
                }
            }
        }
        else if (normalizedKind == "title")
        {
            for (int i = 0; i < state.titles.Count; i++)
            {
                TitleRecord record = state.titles[i];
                if (record == null)
                    continue;
                float score = SkillSimilarity.Score(name, description, tags, record.name, record.description, Array.Empty<string>());
                if (score >= threshold || SameNormalizedName(name, record.name))
                {
                    existingName = record.name;
                    return true;
                }
            }
        }

        for (int i = 0; i < state.pendingOffers.Count; i++)
        {
            PendingProgressionOfferRecord offer = state.pendingOffers[i];
            if (offer == null || !offer.IsPending)
                continue;
            if (!string.Equals(NormalizeKind(offer.offerKind), normalizedKind, StringComparison.OrdinalIgnoreCase))
                continue;

            float score = SkillSimilarity.Score(name, description, tags, offer.name, offer.description, offer.tags);
            if (score >= threshold || SameNormalizedName(name, offer.name))
            {
                existingName = offer.name;
                return true;
            }
        }

        return false;
    }

    private static string[] BuildSkillTags(SkillRecord skill)
    {
        if (skill == null)
            return Array.Empty<string>();

        List<string> tags = new List<string>(4);
        if (!string.IsNullOrWhiteSpace(skill.type))
            tags.Add(skill.type.Trim().ToLowerInvariant());
        if (skill.isSpell)
            tags.Add("spell");
        if (!string.IsNullOrWhiteSpace(skill.context))
            tags.Add(skill.context.Trim().ToLowerInvariant());
        return tags.ToArray();
    }

    private static float MinConfidenceForKind(string kind)
    {
        switch (kind)
        {
            case "quest": return 0.82f;
            case "class": return 0.82f;
            case "title": return 0.80f;
            case "skill":
            case "spell": return 0.78f;
            default: return 0.75f;
        }
    }

    private static bool SameNormalizedName(string a, string b)
    {
        string left = NormalizeText(a);
        string right = NormalizeText(b);
        return !string.IsNullOrWhiteSpace(left) && string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeKind(string kind)
    {
        string value = string.IsNullOrWhiteSpace(kind) ? string.Empty : kind.Trim().ToLowerInvariant();
        if (value == "spell")
            return "spell";
        if (value == "skill")
            return "skill";
        if (value == "quest" || value == "class" || value == "title")
            return value;
        return "offer";
    }

    private static string NormalizeText(string value)
    {
        string[] tokens = Tokenize(value);
        return string.Join(" ", tokens);
    }

    private static bool ContainsAnyPhrase(string text, params string[] hints)
    {
        if (string.IsNullOrWhiteSpace(text) || hints == null)
            return false;

        string normalized = NormalizeText(text);
        string padded = " " + normalized + " ";
        for (int i = 0; i < hints.Length; i++)
        {
            string hint = NormalizeText(hints[i]);
            if (string.IsNullOrWhiteSpace(hint))
                continue;
            if (padded.Contains(" " + hint + " "))
                return true;
            if (hint.Length >= 8 && normalized.Contains(hint))
                return true;
        }

        return false;
    }

    private static string CompactWhitespace(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder sb = new StringBuilder(value.Length);
        bool lastWasSpace = false;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace)
                    sb.Append(' ');
                lastWasSpace = true;
            }
            else
            {
                sb.Append(c);
                lastWasSpace = false;
            }
        }

        return sb.ToString().Trim();
    }

    private static string[] Tokenize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Array.Empty<string>();

        List<string> tokens = new List<string>(8);
        StringBuilder sb = new StringBuilder(20);
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
                continue;
            }

            FlushToken(sb, tokens);
        }

        FlushToken(sb, tokens);
        return tokens.ToArray();
    }

    private static void FlushToken(StringBuilder sb, List<string> tokens)
    {
        if (sb.Length == 0)
            return;
        tokens.Add(sb.ToString());
        sb.Length = 0;
    }

    private static int CountLetters(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return 0;

        int letters = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (char.IsLetter(value[i]))
                letters++;
        }
        return letters;
    }

    private static bool ContainsMarkupOrJson(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return value.IndexOf('{') >= 0 ||
               value.IndexOf('}') >= 0 ||
               value.IndexOf('[') >= 0 ||
               value.IndexOf(']') >= 0 ||
               value.IndexOf('"') >= 0 ||
               value.IndexOf("```", StringComparison.OrdinalIgnoreCase) >= 0 ||
               value.IndexOf("<script", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasTooManySymbols(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;

        int symbols = 0;
        int lettersOrDigits = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
                lettersOrDigits++;
            else if (!char.IsWhiteSpace(c) && c != '\'' && c != '-')
                symbols++;
        }

        return lettersOrDigits == 0 || symbols > Mathf.Max(2, lettersOrDigits / 3);
    }

    private static bool HasRepeatedCharacters(string value, int maxRun)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        char previous = '\0';
        int run = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (!char.IsLetterOrDigit(c))
            {
                previous = '\0';
                run = 0;
                continue;
            }

            if (c == previous)
                run++;
            else
            {
                previous = c;
                run = 1;
            }

            if (run >= maxRun)
                return true;
        }

        return false;
    }
}
