using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public static class YQGoddessLoadingVoice
{
    /*
     * Presentation randomness only.
     *
     * This does NOT participate in:
     * - world seeds
     * - canonical generation
     * - NPC identity
     * - save data
     * - terrain determinism
     *
     * Each category uses a shuffled bag:
     *
     * every line appears once
     * -> bag exhausts
     * -> reshuffle
     * -> repeat cycle begins
     *
     * This prevents the visibly repetitive:
     *
     * A
     * B
     * A
     * C
     * A
     *
     * pattern produced by ordinary Random.Range().
     */

    private static readonly Dictionary<string, Queue<string>>
        Bags =
            new Dictionary<string, Queue<string>>();

    private static readonly Dictionary<string, string>
        LastTemplateByBag =
            new Dictionary<string, string>();

    private static readonly HashSet<string>
        UsedTemplatesThisGeneration =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string>
        UsedTemplateFamiliesThisGeneration =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

    private static readonly Queue<string>
        RecentPlayerTopics =
            new Queue<string>();

    private const int MaxRecentPlayerTopics =
        8;

    private static int _playerAwareLineCount;

    // note: The first origin line is a one-shot welcome so the player hears an introduction before construction status commentary.
    private static bool _playerIntroductionDelivered;

    private static readonly string[]
        OriginTransitionFallback =
        {
            // note: Fallbacks introduce the player to her care and uncertainty; physical work is context, not the entire subject of every thought.
            "I know who you are now. That helps more than you might think. I need a little longer to make somewhere safe for you to begin.",

            "Your answers are with me. Some are clearer than others, but they are yours, and I am making room for all of them.",

            "You have given me enough to begin. I would sound more certain if the ground were finished. It will be. I am working on it.",

            "I have your beginning. I am trying to place it gently; beginnings become surprisingly heavy once they belong to someone.",

            "Your name is here, and so is the person you asked to become. Let me keep the world from crowding you before you arrive.",

            "Your beginning is ready. The world around it is still fragile, but it was made for you. I am not letting it fall apart now."
        };

    private static readonly string[]
        PlayerIntroductionTemplates =
        {
            "Welcome, {0}. I know you have questions, but for now, I must work. This world is fragile, and it is being made especially for you. I am doing my best to keep it together.",

            "Welcome, {0}. Your answers gave me a place to begin: {1}. I will explain more when I can. For now, let me make certain the world can safely hold you.",

            "Welcome, {0}. I have been trying to prepare for you. {1} is already part of this world because you asked it to be. The rest is—well. The rest is coming together.",

            "Welcome, {0}. I am making a world that can carry the person you described: {1}. I may have underestimated how much world that requires. I am still doing it."
        };

    private static readonly string[]
        WorldPlanFinished =
        {
            "I think I understand where you might begin. Understanding it and making it ready are, it turns out, rather different things.",

            "Your answers gave me somewhere to start. I want this to feel like a world made for you, not a test you can fail.",

            "I have a plan for your beginning. You need not follow a plan for your whole life. That would be asking rather a lot.",

            "There is so much I want to explain. I am afraid I need to make somewhere for you first.",

            "I had intended to sound more certain by now. But I have a beginning for you, and I care about getting it right."
        };

    private static readonly string[]
        StableScaffold =
        {
            "I need to try a simpler beginning. Not because you asked for too much. Because I did.",

            "I would rather give you something I can look after than make a grand promise I cannot keep.",

            "This is taking more care than I expected. Your answers still matter. I am not setting them aside.",

            "I have changed my approach. I would like to call that wisdom, although it feels rather like learning.",

            "I am still here. Your beginning is worth another attempt."
        };

    private static readonly string[]
        QuestionableAnswerLines =
        {
            "\"{0}\" remains in the record. I have seen dead languages with better posture.",

            "You offered \"{0}\". I preserved it exactly. Evidence should not be improved after collection.",

            "That answer appears to have been assembled by impact rather than language. Useful, in the way ash is useful.",

            "\"{0}\" does not correspond to any mortal tongue I respect. This does not narrow the field much.",

            "I examined \"{0}\" for meaning. It resisted. Admirable, in a fungal sort of way.",

            "One of your answers contains ⟦UNTRANSLATABLE⟧ where intent usually goes. I have made a note and lowered expectations.",

            "The sequence \"{0}\" has been accepted as testimony. Historians may one day demand an apology.",

            "Your interface produced \"{0}\". I am choosing to believe this was deliberate. It is less sad that way."
        };

    private static readonly string[]
        EmptyReadoutLines =
        {
            "Your answers contain a meaningful amount of absence. Fine. I can sculpt from void; I simply prefer it to submit a ticket first.",

            "Several fields arrived functionally empty. I am not panicking. I am calmly inventing load-bearing context from dust.",

            "The questionnaire has gaps where a person usually goes. I will install temporary intent and label it divine restraint."
        };

    private static readonly string[]
        NonsenseReadoutLines =
        {
            "Your answers are mostly signal-noise soup: {0}. Fine. I will speak in bright labels and keep one hand on the railing.",

            "The readout says you may be testing the walls of language: {0}. I can add a little nonsense back, but I am bolting it to meaning.",

            "I see the nonsense in {0}. I am not ignoring it. I am turning it into controlled weather with captions."
        };

    private static readonly string[]
        CruelReadoutLines =
        {
            "There is cruelty in the answers: {0}. I have marked it as usable pressure, not permission. Important. Very important.",

            "Your readout leans sharp and hungry: {0}. I can build with that. I am also putting handles on the dangerous parts.",

            "You gave me menace through {0}. Wonderful, terrifying, editable. I am sandboxing the appetite before it learns doors."
        };

    private static readonly string[]
        RighteousReadoutLines =
        {
            "The answers keep trying to be righteous: {0}. Admirable. Also suspicious. Virtue loves becoming architecture when unsupervised.",

            "I see the protector shape forming from {0}. I will make it useful before it turns into a speech.",

            "Mercy and duty are loud in the readout: {0}. Good. Loud things are easier to constrain."
        };

    private static readonly string[]
        SillyReadoutLines =
        {
            "Your answers are wearing joke-glasses: {0}. I am allowing it. A ridiculous origin can still carry a blade.",

            "The readout is silly on purpose: {0}. I respect intentional nonsense more than accidental nonsense. Barely.",

            "Comedy detected in {0}. I am stapling competence underneath it so the world does not collapse into improv."
        };

    private static readonly string[]
        CoherentReadoutLines =
        {
            "The answer pattern is coherent enough to be dangerous: {0}. I hate when mortals make my job easier in a complicated way.",

            "There is a real through-line in the readout: {0}. Annoying. Useful. I am building around it before it changes its mind.",

            "Your answers form an actual shape: {0}. I am trying not to look relieved. The mask is slipping; ignore that."
        };

    private static readonly string[]
        AngryReadoutLines =
        {
            "The readout is hot around {0}. I am lowering the temperature first. Breathe, then we weaponize the useful part.",

            "Anger is crowding the answers: {0}. I hear it. I am not feeding it raw. I am making it carry a handle.",

            "There is pressure-spike behavior in {0}. I am keeping my voice flat on purpose. One piece at a time."
        };

    private static readonly string[]
        LowClarityReadoutLines =
        {
            "Your answers are low-resolution: {0}. I will use smaller steps. Name, pressure, tool. Then ground. Easy.",

            "The readout is not giving me much: {0}. Fine. I will simplify the interface of fate. Big labels, fewer moving parts.",

            "I am detecting thin context around {0}. I will not overcomplicate this. One clear shape, one clear job, one door."
        };

    private static readonly string[]
        BriefAnswerLines =
        {
            "\"{0}\" is impressively small. A whole destiny balanced on a crumb. Mortals do enjoy making me extrapolate.",

            "You gave me \"{0}\". Compact. Either restraint or exhaustion; both have shaped empires.",

            "\"{0}\" was brief enough to make silence feel overdressed.",

            "A short answer. Convenient. Not informative, but convenience has ruined stronger civilizations.",

            "You answered with almost nothing. I have used almost nothing before. It tends to grow teeth."
        };

    private static readonly string[]
        LongAnswerLines =
        {
            "One answer arrived with furniture, weather, and legal claims. I read it. Eventually.",

            "You wrote at length. This may indicate thought, or merely momentum. I have seen both worshipped.",

            "That long answer kept unfolding. I have folded it back into something reality can carry.",

            "Your verbosity has been catalogued. Do not worry; creation can survive excessive mortal explanation.",

            "A lengthy confession. I trimmed nothing. The future may need the whole inconvenience."
        };

    private static readonly string[]
        DuplicateAnswerLines =
        {
            // note: Repetition can reflect uncertainty or importance; do not diagnose or mock the player from an input category.
            "You returned to the same answer. I noticed. It may matter more than I first understood.",

            "Several of your answers were the same. You do not need to find new words just to keep my attention.",

            "That thought came back more than once. I am keeping it in mind as I work.",

            "I heard you the first time. That sounded impatient. I meant that I have not forgotten."
        };

    private static readonly string[]
        GenericAnswerLines =
        {
            "Some of your answers leave me room to guess. I will try not to mistake my guesses for knowing you.",

            "You do not have to explain all of yourself before you arrive. A beginning is enough for now.",

            "There are things you have not told me. I can leave room for those too.",

            "I am still getting to know you. I suppose a handful of questions was rather an ambitious start."
        };

    private static readonly string[]
        ThoughtfulAnswerLines =
        {
            "You took care with those answers. I want to give your beginning the same care.",

            "You gave me something to think about. More than I expected, if I am honest.",

            "I am trying to understand what matters to you, not just remember what you said.",

            "Thank you for trusting me with that much of yourself. I know I have not explained very much in return."
        };

    private static readonly string[]
        ThemeAnswerLines =
        {
            "You kept returning to {0}. I want to make room for what that means to you.",

            "You mentioned {0} more than once. I am listening, even when I seem distracted.",

            "I keep thinking about what you said about {0}. I hope I have understood the important part.",

            "Several of your answers came back to {0}. That gives me somewhere to begin, not permission to decide everything for you.",

            "You spoke about {0}. I cannot promise how that will turn out here. I can promise I paid attention."
        };

    private static readonly string[]
        OriginStimulusLines =
        {
            "Your beginning draws on {0}. I want it to feel like something you can grow into.",

            "I have kept {0} close to your beginning. It need not be the whole of who you become.",

            "There is a place for {0} in what I am preparing. I hope it feels like I listened.",

            "I am working with {0}. Explaining what it means for you may have to come a little later.",

            "Your answers led me to {0}. I am trying to make a beginning from that, not a limit."
        };

    // ============================================================
    // NPC — SETTLEMENT CREATION
    // ============================================================

    private static readonly string[]
        SettlementPopulationCreating =
        {
            // note: Population fallbacks address the player without inventing residents, memories or technical assembly status.
            "I am thinking about who you might meet in {0}. People are rather harder to get right than a welcome speech.",
            "You should not have to make sense of {0} alone. I hope there will be someone you can talk to.",
            "When you reach {0}, I want there to be more to discover than what I can tell you now.",
            "I cannot decide who you will trust in {0}. That is probably for the best, though I find it difficult.",
            "There is room for more than your story in {0}. I am trying to give that the care it deserves.",
            "I keep imagining your first conversation in {0}. No, I should leave that part to you."
        };

    // ============================================================
    // NPC — HOSTILE CREATION
    // ============================================================

    private static readonly string[]
        HostilePopulationCreating =
        {
            // note: A known hostile location permits concern about danger, not invented enemies or guaranteed encounter outcomes.
            "I am uneasy about {0}. There is danger in this world, and I do not want to make light of it for you.",
            "You need not prove yourself to me by rushing toward {0}. Your beginning is not an examination.",
            "I want you to have choices about {0}, including the choice to come back another time.",
            "I cannot promise that {0} will be kind to you. I wish that were easier to say."
        };

    // ============================================================
    // NPC — SETTLEMENT RETRY
    // ============================================================

    private static readonly string[]
        SettlementPopulationRetry =
        {
            "No, no... I have already made those people somewhere else. Again...",

            "Those names are taken. Mortals are inconveniently countable...",

            "I appear to have given {0} somebody else's inhabitants...",

            "Wait. I have remembered the same mortal twice. That seems unhealthy...",

            "Those lives overlap. Mortal causality becomes very fussy about that...",

            "No. Those people belong somewhere else. Put them back...",

            "I crossed two destinies. Embarrassing. Let me separate them...",

            "{0} deserves its own inhabitants. Apparently copying them is frowned upon...",

            "I have duplicated a soul. Do not look at it while I fix this...",

            "Names again. Why do mortals insist on having unique ones?",

            "No, that history already belongs to somebody else...",

            "I reached into the wrong village. Easy mistake when one contains all villages...",

            "That person exists already. I distinctly remember making them...",

            "I have created an administrative problem in the census of reality...",

            "Those are not {0}'s people. They merely believe they are...",

            "No. Too familiar. Let me reach further sideways through possibility...",

            "I seem to have reused a mortal. Wasteful...",

            "One moment. The souls assigned to {0} have paperwork problems...",

            "That identity is already occupied. I require another...",

            "I have tangled two family trees. Neither family will appreciate this...",

            "No, I recognize those names. I made them earlier...",

            "Something has gone wrong in the bookkeeping of existence...",

            "I refuse to populate {0} with echoes. Again...",

            "The universe claims those people already exist. Annoying, but technically correct..."
        };

    // ============================================================
    // NPC — HOSTILE RETRY
    // ============================================================

    private static readonly string[]
        HostilePopulationRetry =
        {
            "No. That name belongs to another mouth. Let me reach deeper...",

            "I have apparently named two horrors the same thing. Both are offended...",

            "That creature already exists elsewhere. I refuse matching abominations...",

            "No, not that one. I have used that soul already...",

            "I pulled the wrong monster out of possibility. Put it back...",

            "That name echoes somewhere else. I dislike echoes...",

            "One of my horrors has become derivative. Give me a moment...",

            "No. I recognize that monster. It already has somewhere to haunt...",

            "I appear to have created the same nightmare twice...",

            "Wrong creature. Same universe. Easy mistake...",

            "That identity is occupied. I shall reach somewhere less crowded...",

            "No, no. This one already has somewhere else to be terrible...",

            "I have reused an abomination. How economical of me. Also wrong...",

            "{0} requires its own nightmare, not somebody else's...",

            "That horror has already been assigned. I need another horror...",

            "No. I can hear that name answering from somewhere else...",

            "Apparently even monsters object to identity theft...",

            "I have confused two terrible things. Let us hope they never meet...",

            "That creature belongs to another patch of darkness...",

            "No. I already made that mistake somewhere else..."
        };

    // ============================================================
    // NPC — SETTLEMENT ACCEPTED
    // ============================================================

    private static readonly string[]
        SettlementPopulationAccepted =
        {
            "Yes. Those are the ones. They have always lived in {0}. I think...",

            "There. {0} remembers its people now...",

            "Good. The people of {0} have histories and several unnecessary opinions...",

            "{0} is occupied. Try not to unravel anyone's backstory...",

            "Ah, yes. Those faces belong in {0}. They always did. Recently...",

            "The inhabitants of {0} now remember childhoods that occurred moments ago...",

            "{0} has citizens now. Some already owe each other money...",

            "There. {0} has families, strangers, grudges, and gossip...",

            "The people of {0} are convinced they have always existed. Excellent...",

            "{0} remembers them now. Memory is wonderfully obedient...",

            "Several entire lives fit neatly into {0}. More or less...",

            "The people of {0} have settled into their histories...",

            "Good. Someone in {0} already dislikes somebody else...",

            "There. {0} has enough personal history to become difficult...",

            "The inhabitants of {0} have accepted their pasts without objection...",

            "{0} is alive now. Figuratively. Mostly literally...",

            "Good. The doors in {0} finally belong to somebody...",

            "There. {0} has people who would swear they remember last winter...",

            "The citizens of {0} have been successfully convinced of continuity...",

            "Excellent. {0} now contains opinions, obligations, and breakfast routines...",

            "There. Several people now call {0} home without knowing why...",

            "{0} has inhabitants. History has graciously made room for them...",

            "Good. Nobody in {0} suspects they were absent a moment ago...",

            "The people of {0} are now properly entangled with one another..."
        };

    // ============================================================
    // NPC — HOSTILE ACCEPTED
    // ============================================================

    private static readonly string[]
        HostilePopulationAccepted =
        {
            "There. I have given the thing in {0} a name. It dislikes you already...",

            "{0} has its monster now. I advise against introductions...",

            "Done. Something in {0} knows its own name...",

            "Yes. That is what has always lurked in {0}. Do not question 'always'...",

            "I have finished the unpleasant thing in {0}. It seems enthusiastic...",

            "{0} is properly dangerous now. Much better...",

            "Ah. There it is. The problem in {0} has become personal...",

            "{0} now contains something with both a name and violent intentions...",

            "The thing in {0} knows who it is. That usually makes them worse...",

            "I have completed the danger in {0}. Avoid eye contact...",

            "Something in {0} has become certain that it belongs there...",

            "{0} now has a proper nightmare. You are welcome...",

            "Good. The local warnings about {0} are retroactively justified...",

            "There. Something in {0} has acquired a reputation before meeting anyone...",

            "{0} contains exactly the sort of thing roads should bend around...",

            "The darkness in {0} has an owner now...",

            "Good. Whatever is in {0} has decided you look interruptible...",

            "There. The stories about {0} finally have something to be about...",

            "{0} now has a reason people lower their voices when mentioning it...",

            "Finished. Something at {0} is waiting very patiently...",

            "There. I have supplied {0} with consequences...",

            "Good. {0} now possesses an inhabitant sensible people will avoid...",

            "The thing in {0} has accepted its role with disturbing enthusiasm...",

            "{0} has become appropriately regrettable to visit..."
        };

    // ============================================================
    // PHYSICAL WORLD — TERRAIN
    // ============================================================

    private static readonly string[]
        TerrainMaterialization =
        {
            // note: Fallback speech expresses effort without inventing visible terrain failures or claiming repairs succeeded.
            "This world is delicate. I know I ought to make it look effortless. I would rather make it safe for you.",
            "You deserve somewhere to find your feet. I am trying not to rush that part.",
            "I have your answers in mind. A whole world is rather more responsibility than a welcome speech.",
            "There will be things I cannot explain yet. I hope you can forgive me for beginning with the ground beneath you."
        };

    // ============================================================
    // PHYSICAL WORLD — SETTLEMENT MATERIALIZATION
    // ============================================================

    private static readonly string[]
        SettlementMaterialization =
        {
            "I have been thinking about {0}. I want it to mean something when you arrive, not merely be somewhere on your way.",
            "Perhaps {0} will give you a reason to stay a while. That is a hope, not a command.",
            "You may make something of {0} that I never expected. I am trying to be comfortable with that.",
            "There is a place for {0} in your beginning. I have not decided what it must mean to you."
        };

    // ============================================================
    // PHYSICAL WORLD — BUILDINGS
    // ============================================================

    private static readonly string[]
        BuildingMaterialization =
        {
            "I want {0} to have room for ordinary life. You should not have to be remarkable every moment you are here.",
            "When I think of {0}, I keep coming back to the small things. Somewhere to rest matters too.",
            "There is more to looking after a world than making it impressive. {0} is reminding me of that.",
            "I hope you find something familiar in {0}. Not everything in your beginning needs to feel strange."
        };

    // ============================================================
    // PHYSICAL WORLD — ENVIRONMENT
    // ============================================================

    private static readonly string[]
        EnvironmentMaterialization =
        {
            "I want there to be room for your curiosity. You did not come all this way merely to follow instructions.",
            "Your world should have quiet moments too. I am trying to remember that while thinking about everything else.",
            "I cannot tell you what you will love here. I would like you to have the chance to find out.",
            "Not every part of your beginning needs an explanation from me. That is fortunate. I have rather a lot left to do."
        };

    // ============================================================
    // WORLD PLAN CHANGED
    // ============================================================

    private static readonly string[]
        WorldPlanChanged =
        {
            // note: A changed plan permits an admission of revision, not fabricated world history or a false explanation of visible defects.
            "I have reconsidered part of your beginning. I would rather admit that than insist my first thought was perfect.",
            "Your answers still matter. I am trying to find a better way to make room for them.",
            "I know this is not quite the effortless welcome I intended. I am still working on it."
        };

    // ============================================================
    // TERMINAL FAILURE
    // ============================================================

    private static readonly string[]
        TerminalFailure =
        {
            "I cannot bring you through yet. Something is wrong, and I will not pretend it is safe. I am sorry.",
            "This is not ready for you. I wanted to give you a better welcome than this.",
            "I have to stop here. It is not your fault, and it is not some test I meant to set you."
        };

    // ============================================================
    // POPULATION COMPLETE
    // ============================================================

    private static readonly string[]
        PopulationComplete =
        {
            "You will not be alone here. I hope that comes as a comfort. I cannot promise it always will.",
            "There are others for you to meet now. What you make of one another is not something I can decide.",
            "I have thought about your beginning so much that I keep forgetting it is also part of other lives."
        };

    // ============================================================
    // FINAL REVEAL
    // ============================================================

    private static readonly string[]
        FinalReveal =
        {
            "You can begin now. This world is fragile, but it is yours to discover. I will do what I can to keep it together.",
            "Here we are. I had a grander speech in mind, but I think I would rather say this: I hope you find your place here.",
            "Your beginning is ready. What happens next belongs to you. I am trying very hard not to be nervous about that."
        };

    // ============================================================
    // PUBLIC API
    // ============================================================

    public static string SettlementCreating(
        string location)
    {
        return PickWithPlayerAwareness(
            "settlement_creating",
            SettlementPopulationCreating,
            location,
            "location_creation",
            0.28f);
    }

    public static string HostileCreating(
        string location)
    {
        return PickWithPlayerAwareness(
            "hostile_creating",
            HostilePopulationCreating,
            location,
            "danger_creation",
            0.28f);
    }

    public static string SettlementRetry(
        string location)
    {
        return PickWithPlayerAwareness(
            "settlement_retry",
            SettlementPopulationRetry,
            location,
            "retry",
            0.18f);
    }

    public static string HostileRetry(
        string location)
    {
        return PickWithPlayerAwareness(
            "hostile_retry",
            HostilePopulationRetry,
            location,
            "retry",
            0.18f);
    }

    public static string SettlementAccepted(
        string location)
    {
        return PickWithPlayerAwareness(
            "settlement_accepted",
            SettlementPopulationAccepted,
            location,
            "location_accepted",
            0.2f);
    }

    public static string HostileAccepted(
        string location)
    {
        return PickWithPlayerAwareness(
            "hostile_accepted",
            HostilePopulationAccepted,
            location,
            "danger_accepted",
            0.2f);
    }

    public static string Terrain()
    {
        return PickWithPlayerAwareness(
            "terrain",
            TerrainMaterialization,
            string.Empty,
            "terrain",
            0.55f);
    }

    public static string SettlementBuilding(
        string location)
    {
        return PickWithPlayerAwareness(
            "settlement_materialization",
            SettlementMaterialization,
            location,
            "settlement_materialization",
            0.24f);
    }

    public static string Buildings(
        string location)
    {
        return PickWithPlayerAwareness(
            "buildings",
            BuildingMaterialization,
            location,
            "buildings",
            0.2f);
    }

    public static string Environment()
    {
        return PickWithPlayerAwareness(
            "environment",
            EnvironmentMaterialization,
            string.Empty,
            "environment",
            0.45f);
    }

    public static string PlanChanged()
    {
        return PickWithPlayerAwareness(
            "plan_changed",
            WorldPlanChanged,
            string.Empty,
            "plan_changed",
            0.25f);
    }

    public static string Failure()
    {
        return PickWithPlayerAwareness(
            "failure",
            TerminalFailure,
            string.Empty,
            "failure",
            0.22f);
    }

    public static string PopulationFinished()
    {
        return PickWithPlayerAwareness(
            "population_finished",
            PopulationComplete,
            string.Empty,
            "population_finished",
            0.4f);
    }

    public static string Reveal()
    {
        return PickWithPlayerAwareness(
            "reveal",
            FinalReveal,
            string.Empty,
            "reveal",
            0.55f);
    }

    public static string OriginTransition()
    {
        return PickWithPlayerAwareness(
            "origin_transition",
            OriginTransitionFallback,
            string.Empty,
            "origin_transition",
            0.95f);
    }

    public static string WorldPlanComplete()
    {
        return PickWithPlayerAwareness(
            "world_plan_complete",
            WorldPlanFinished,
            string.Empty,
            "world_plan_complete",
            0.55f);
    }

    public static string StableScaffoldFallback()
    {
        return PickWithPlayerAwareness(
            "stable_scaffold",
            StableScaffold,
            string.Empty,
            "stable_scaffold",
            0.35f);
    }

    public static void ResetForNewGeneration()
    {
        // note: Only transient repetition tracking resets; shuffled bags remain session-bounded for broader replay variety.
        RecentPlayerTopics.Clear();
        UsedTemplatesThisGeneration.Clear();
        UsedTemplateFamiliesThisGeneration.Clear();

        _playerAwareLineCount =
            0;

        _playerIntroductionDelivered =
            false;
    }

    public static bool TryTakePlayerIntroduction(
        out string line)
    {
        line =
            string.Empty;

        if (_playerIntroductionDelivered)
            return false;

        PlayerState state =
            PlayerStateManager.Instance != null
                ? PlayerStateManager.Instance.state
                : null;

        if (state == null)
            return false;

        _playerIntroductionDelivered =
            true;

        string playerName =
            SafeDisplay(
                state.displayName);

        if (string.IsNullOrWhiteSpace(playerName) ||
            string.Equals(
                playerName,
                "The Player",
                StringComparison.OrdinalIgnoreCase))
        {
            playerName =
                "adventurer";
        }

        string identity =
            ResolvePlayerIdentityPhrase(
                state);

        string template =
            Pick(
                "player_introduction",
                PlayerIntroductionTemplates);

        if (string.IsNullOrWhiteSpace(template))
            return false;

        try
        {
            // note: The welcome binds the authored name and one accepted origin fact without exposing internal generation terminology.
            line =
                string.Format(
                    template,
                    playerName,
                    identity);
        }
        catch
        {
            line =
                string.Empty;
        }

        return
            !string.IsNullOrWhiteSpace(line);
    }

    private static string ResolvePlayerIdentityPhrase(
        PlayerState state)
    {
        if (state != null &&
            state.generatedOrigin != null)
        {
            GeneratedOriginRecord origin =
                state.generatedOrigin;

            if (!string.IsNullOrWhiteSpace(origin.className))
            {
                return
                    "the " +
                    SafeDisplay(origin.className) +
                    " you are becoming";
            }

            if (!string.IsNullOrWhiteSpace(origin.titleName))
            {
                return
                    "the title " +
                    SafeDisplay(origin.titleName) +
                    " you earned";
            }

            if (!string.IsNullOrWhiteSpace(origin.stimulus))
            {
                return
                    "what matters to you: " +
                    SafeDisplay(origin.stimulus);
            }
        }

        if (state != null &&
            !string.IsNullOrWhiteSpace(state.characterLifeDirection))
        {
            return
                "the direction you chose: " +
                SafeDisplay(state.characterLifeDirection);
        }

        AnswerProfile profile =
            AnalyzeAnswers(
                state,
                null);

        if (profile != null &&
            !string.IsNullOrWhiteSpace(profile.RecurringTheme))
        {
            return
                "the theme in your answers: " +
                SafeDisplay(profile.RecurringTheme);
        }

        return
            "the shape of your answers";
    }

    public static string BuildQuestionnaireContextForPrompt(
        PlayerState state,
        IReadOnlyList<string> directAnswers = null)
    {
        AnswerProfile profile =
            AnalyzeAnswers(
                state,
                directAnswers);

        if (!profile.HasAnswers && state == null)
        {
            return
                "GODDESS_QUESTIONNAIRE_PRESENTATION_CONTEXT\n" +
                "- No questionnaire answers are available for presentation commentary.\n";
        }

        StringBuilder sb =
            new StringBuilder();

        sb.AppendLine(
            "GODDESS_QUESTIONNAIRE_PRESENTATION_CONTEXT");

        sb.AppendLine(
            "- This block is for Goddess presentation only. It must not change canonical facts.");

        if (!profile.HasAnswers)
            sb.AppendLine("- No questionnaire answers are available; use the persistent identity and journey memories below when relevant.");

        // note: Startup dialogue receives a small persistent identity thread so origin and world prose can remember the player beyond answer categories.
        if (state != null)
        {
            sb.AppendLine("- persistentPlayer=" + SafeDisplay(state.displayName));
            sb.AppendLine("- intendedDirection=" + SafeDisplay(string.IsNullOrWhiteSpace(state.characterLifeDirection) ? "unspecified" : state.characterLifeDirection));
            sb.AppendLine("- personalVow=" + SafeDisplay(string.IsNullOrWhiteSpace(state.characterVow) ? "unspecified" : state.characterVow));
            if (state.behaviorLedger != null && state.behaviorLedger.Count > 0)
            {
                int memoryStart = Mathf.Max(0, state.behaviorLedger.Count - 3);
                for (int memoryIndex = memoryStart; memoryIndex < state.behaviorLedger.Count; memoryIndex++)
                    sb.AppendLine("- priorJourneyMemory=" + SafeDisplay(state.behaviorLedger[memoryIndex]));
            }
        }

        sb.AppendLine(
            "- answerCount=" +
            profile.AnswerCount +
            ", empty=" +
            profile.EmptyCount +
            ", veryShort=" +
            profile.VeryShortCount +
            ", long=" +
            profile.LongCount +
            ", questionable=" +
            profile.QuestionableCount +
            ", lowClarity=" +
            profile.LowClarityCount +
            ", angry=" +
            profile.AngerCount +
            ", nonsense=" +
            profile.NonsenseCount +
            ", silly=" +
            profile.SillyCount +
            ", cruel=" +
            profile.CruelCount +
            ", righteous=" +
            profile.RighteousCount +
            ", coherent=" +
            profile.CoherentCount +
            ", duplicateGroups=" +
            profile.DuplicateGroups +
            ", genericOrRefusal=" +
            profile.GenericOrRefusalCount);

        if (!string.IsNullOrWhiteSpace(
                profile.PrimaryReadout))
        {
            sb.AppendLine(
                "- primaryPlayerReadout=" +
                PromptSafe(
                    profile.PrimaryReadout) +
                ", strength=" +
                profile.PrimaryReadoutCount);
        }

        if (!string.IsNullOrWhiteSpace(
                profile.ResponseMode))
        {
            sb.AppendLine(
                "- adaptiveResponseMode=" +
                PromptSafe(
                    profile.ResponseMode));

            sb.AppendLine(
                "- adaptiveResponseInstruction=" +
                PromptSafe(
                    profile.ResponseInstruction));
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleReadout))
        {
            sb.AppendLine(
                "- readoutEvidence=\"" +
                PromptSafe(
                    TrimTo(
                        profile.SampleReadout,
                        96)) +
                "\"");
        }

        if (!string.IsNullOrWhiteSpace(
                profile.RecurringTheme))
        {
            sb.AppendLine(
                "- recurringTheme=" +
                PromptSafe(
                    profile.RecurringTheme));
        }

        if (!string.IsNullOrWhiteSpace(
                profile.Stimulus))
        {
            sb.AppendLine(
                "- committedStimulus=" +
                PromptSafe(
                    profile.Stimulus));
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleQuestionable))
        {
            sb.AppendLine(
                "- notableQuestionableAnswer=\"" +
                PromptSafe(
                    TrimTo(
                        profile.SampleQuestionable,
                        72)) +
                "\"");
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleThoughtful))
        {
            sb.AppendLine(
                "- notableThoughtfulAnswer=\"" +
                PromptSafe(
                    TrimTo(
                        profile.SampleThoughtful,
                        96)) +
                "\"");
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleShort))
        {
            sb.AppendLine(
                "- notableShortAnswer=\"" +
                PromptSafe(
                    TrimTo(
                        profile.SampleShort,
                        40)) +
                "\"");
        }

        sb.AppendLine(
            "- The Goddess should treat the player according to adaptiveResponseMode, not merely label the answer category.");

        sb.AppendLine(
            "- If adaptiveResponseMode=simplify_and_anchor, use shorter concrete clauses and reassuring step order.");

        sb.AppendLine(
            "- If adaptiveResponseMode=deescalate_and_ground, lower the emotional temperature while preserving agency.");

        sb.AppendLine(
            "- If adaptiveResponseMode=controlled_chaos, echo a little strangeness but keep the build task legible.");

        sb.AppendLine(
            "- If adaptiveResponseMode=boundary_the_menace, acknowledge harmful intent as pressure while setting clear limits.");

        sb.AppendLine(
            "- If adaptiveResponseMode=mirror_playfully, play along without making the whole world a joke.");

        sb.AppendLine(
            "- If adaptiveResponseMode=respect_the_signal, reward coherent intent with more precise language.");

        return
            sb.ToString();
    }

    // ============================================================
    // SHUFFLED BAG
    // ============================================================

    private static string PickWithPlayerAwareness(
        string bagKey,
        string[] source,
        string location,
        string moment,
        float baseChance)
    {
        if (TryBuildPlayerAwareLine(
                moment,
                baseChance,
                out string line))
        {
            return line;
        }

        return Pick(
            bagKey,
            source,
            location);
    }

    private static bool TryBuildPlayerAwareLine(
        string moment,
        float baseChance,
        out string line)
    {
        line =
            string.Empty;

        AnswerProfile profile =
            AnalyzeAnswers(
                PlayerStateManager.Instance != null
                    ? PlayerStateManager.Instance.state
                    : null,
                null);

        PlayerState journeyState =
            PlayerStateManager.Instance != null
                ? PlayerStateManager.Instance.state
                : null;

        if (!profile.HasAnswers && journeyState == null)
        {
            return false;
        }

        float chance =
            Mathf.Clamp01(
                baseChance);

        if (profile.HasStrongQuestionableSignal)
        {
            chance =
                Mathf.Max(
                    chance,
                    0.72f);
        }

        if (!string.IsNullOrWhiteSpace(
                profile.ResponseMode))
        {
            chance =
                Mathf.Max(
                    chance,
                    0.88f);
        }

        if (_playerAwareLineCount <= 0 &&
            (string.Equals(
                 moment,
                 "origin_transition",
                 StringComparison.Ordinal) ||
             string.Equals(
                 moment,
                 "terrain",
                 StringComparison.Ordinal)))
        {
            chance =
                Mathf.Max(
                    chance,
                    0.9f);
        }

        if (UnityEngine.Random.value >
            chance)
        {
            return false;
        }

        List<PlayerLineCandidate> candidates =
            new List<PlayerLineCandidate>();

        if (journeyState != null)
        {
            // note: The fallback can speak from the player's current place and most recent durable action even when questionnaire evidence is unavailable.
            string journeyLine = BuildJourneyAwareLine(journeyState, moment);
            if (!string.IsNullOrWhiteSpace(journeyLine))
            {
                candidates.Add(
                    new PlayerLineCandidate(
                        "journey_" + moment,
                        journeyLine));
            }
        }

        if (!string.IsNullOrWhiteSpace(
                profile.PrimaryReadout))
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "readout_" +
                    profile.PrimaryReadout,
                    BuildReadoutLine(
                        profile)));
        }

        // note: Strong nonsense signals are surfaced first because they are funny and cheap to detect safely.
        if (profile.HasStrongQuestionableSignal)
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "questionable",
                    Pick(
                        "player_questionable",
                        QuestionableAnswerLines,
                        SafeDisplay(
                            profile.SampleQuestionable))));
        }

        if (profile.DuplicateGroups > 0)
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "duplicates",
                    Pick(
                        "player_duplicates",
                        DuplicateAnswerLines)));
        }

        if (!string.IsNullOrWhiteSpace(
                profile.RecurringTheme))
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "theme_" +
                    profile.RecurringTheme,
                    Pick(
                        "player_theme",
                        ThemeAnswerLines,
                        profile.RecurringTheme)));
        }

        if (profile.GenericOrRefusalCount > 0)
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "generic",
                    Pick(
                        "player_generic",
                        GenericAnswerLines)));
        }

        if (profile.VeryShortCount > 0 &&
            !string.IsNullOrWhiteSpace(
                profile.SampleShort))
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "brief",
                    Pick(
                        "player_brief",
                        BriefAnswerLines,
                        SafeDisplay(
                            profile.SampleShort))));
        }

        if (profile.LongCount > 0)
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "long",
                    Pick(
                        "player_long",
                        LongAnswerLines)));
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleThoughtful))
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "thoughtful",
                    Pick(
                        "player_thoughtful",
                        ThoughtfulAnswerLines)));
        }

        if (!string.IsNullOrWhiteSpace(
                profile.Stimulus) &&
            UnityEngine.Random.value < 0.45f)
        {
            candidates.Add(
                new PlayerLineCandidate(
                    "stimulus",
                    Pick(
                        "player_stimulus",
                        OriginStimulusLines,
                        SafeDisplay(
                            profile.Stimulus))));
        }

        for (int i = 0;
             i < candidates.Count;
             i++)
        {
            PlayerLineCandidate candidate =
                candidates[i];

            if (!WasRecentlyUsed(
                    candidate.Topic))
            {
                RememberPlayerTopic(
                    candidate.Topic);

                _playerAwareLineCount++;

                line =
                    candidate.Line;

                return
                    !string.IsNullOrWhiteSpace(
                        line);
            }
        }

        return false;
    }

    // note: Compose one concise, state-backed line for loading moments so the Goddess remembers what this player actually did instead of choosing a context-free synonym.
    private static string BuildJourneyAwareLine(PlayerState state, string moment)
    {
        if (state == null)
            return string.Empty;

        string place = SafeDisplay(state.currentRegionName);
        if (string.IsNullOrWhiteSpace(place) || string.Equals(place, "Unknown", StringComparison.OrdinalIgnoreCase))
            place = "this place";

        string latest = string.Empty;
        if (state.behaviorLedger != null && state.behaviorLedger.Count > 0)
            latest = SafeDisplay(state.behaviorLedger[state.behaviorLedger.Count - 1]).ToLowerInvariant();

        if (latest.Contains("spoke") || latest.Contains("dialogue") || latest.Contains("talk"))
            return "You keep choosing conversation in " + place + ". I am making room for the people who answer you.";
        if (latest.Contains("kill") || latest.Contains("defeat") || latest.Contains("combat"))
            return "You have made " + place + " quieter by force. I am keeping the consequences useful, because apparently I must supervise your victories too.";
        if (latest.Contains("complete") || latest.Contains("quest") || latest.Contains("discover"))
            return "You followed something through in " + place + ". I noticed. I am adjusting the next opportunity to meet the person you are becoming.";
        if (latest.Contains("equip") || latest.Contains("item") || latest.Contains("craft"))
            return "You keep refining how you carry yourself in " + place + ". I can work with that preference; I have already made room for it.";

        if (!string.IsNullOrWhiteSpace(state.characterVow))
            return "I still remember your vow while I tend " + place + ". You are making the definition of a perfect beginning inconveniently specific.";
        if (!string.IsNullOrWhiteSpace(state.characterLifeDirection))
            return "Your chosen direction is still visible in " + place + ". I am shaping the next step around it, with only a reasonable amount of concern.";

        return string.Equals(moment, "failure", StringComparison.OrdinalIgnoreCase)
            ? "I know this is not the welcome I intended for you. I am keeping the next step safe."
            : "I am still watching how you move through " + place + ". It helps me make the next choice less presumptuous.";
    }

    private static AnswerProfile AnalyzeAnswers(
        PlayerState state,
        IReadOnlyList<string> directAnswers)
    {
        List<string> answers =
            new List<string>();

        if (directAnswers != null)
        {
            for (int i = 0;
                 i < directAnswers.Count;
                 i++)
            {
                answers.Add(
                    directAnswers[i] ??
                    string.Empty);
            }
        }
        else if (state != null &&
                 state.originQuestionnaireAnswers != null)
        {
            for (int i = 0;
                 i < state.originQuestionnaireAnswers.Count;
                 i++)
            {
                answers.Add(
                    state.originQuestionnaireAnswers[i] ??
                    string.Empty);
            }
        }

        AnswerProfile profile =
            new AnswerProfile
            {
                AnswerCount =
                    answers.Count
            };

        if (state != null &&
            state.generatedOrigin != null)
        {
            profile.Stimulus =
                SafeDisplay(
                    state.generatedOrigin.stimulus);
        }

        if (answers.Count <= 0)
        {
            return profile;
        }

        Dictionary<string, int> normalizedCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        Dictionary<string, int> themeCounts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        for (int i = 0;
             i < answers.Count;
             i++)
        {
            string answer =
                answers[i] ??
                string.Empty;

            string trimmed =
                answer.Trim();

            string normalized =
                NormalizeAnswerKey(
                    trimmed);

            if (!string.IsNullOrWhiteSpace(
                    normalized))
            {
                normalizedCounts.TryGetValue(
                    normalized,
                    out int count);

                normalizedCounts[normalized] =
                    count + 1;
            }

            bool questionable =
                IsQuestionableAnswer(
                    trimmed);

            bool generic =
                IsGenericOrRefusal(
                    trimmed);

            bool angry =
                LooksLikeAngryAnswer(
                    trimmed);

            if (string.IsNullOrWhiteSpace(
                    trimmed))
            {
                profile.EmptyCount++;
                profile.LowClarityCount++;
            }

            if (generic)
            {
                profile.LowClarityCount++;
            }

            if (angry)
            {
                profile.AngerCount++;

                RememberReadoutSample(
                    profile,
                    trimmed);
            }

            if (LooksLikeSillyAnswer(
                    trimmed))
            {
                profile.SillyCount++;

                RememberReadoutSample(
                    profile,
                    trimmed);
            }

            if (LooksLikeCruelAnswer(
                    trimmed))
            {
                profile.CruelCount++;

                RememberReadoutSample(
                    profile,
                    trimmed);
            }

            if (LooksLikeRighteousAnswer(
                    trimmed))
            {
                profile.RighteousCount++;

                RememberReadoutSample(
                    profile,
                    trimmed);
            }

            if (trimmed.Length <= 3)
            {
                profile.VeryShortCount++;
                profile.LowClarityCount++;

                if (string.IsNullOrWhiteSpace(
                        profile.SampleShort))
                {
                    profile.SampleShort =
                        trimmed;
                }
            }

            if (trimmed.Length >= 140)
            {
                profile.LongCount++;
            }

            if (questionable)
            {
                profile.QuestionableCount++;
                profile.NonsenseCount++;

                if (string.IsNullOrWhiteSpace(
                        profile.SampleQuestionable))
                {
                    profile.SampleQuestionable =
                        trimmed;
                }
            }

            if (!questionable &&
                !generic &&
                trimmed.Length >= 28 &&
                AlphaRatio(
                    trimmed) >= 0.58f)
            {
                profile.CoherentCount++;
            }

            if (generic)
            {
                profile.GenericOrRefusalCount++;

                RememberReadoutSample(
                    profile,
                    trimmed);
            }

            if (!questionable &&
                !generic &&
                trimmed.Length >= 70 &&
                AlphaRatio(
                    trimmed) >= 0.62f)
            {
                if (string.IsNullOrWhiteSpace(
                        profile.SampleThoughtful) ||
                    trimmed.Length >
                    profile.SampleThoughtful.Length)
                {
                    profile.SampleThoughtful =
                        trimmed;
                }
            }

            AddThemeCounts(
                trimmed,
                themeCounts);
        }

        foreach (KeyValuePair<string, int> pair in normalizedCounts)
        {
            if (pair.Value > 1)
            {
                profile.DuplicateGroups++;
            }
        }

        foreach (KeyValuePair<string, int> pair in themeCounts)
        {
            if (pair.Value >= 2 &&
                pair.Value > profile.RecurringThemeCount)
            {
                profile.RecurringTheme =
                    pair.Key;

                profile.RecurringThemeCount =
                    pair.Value;
            }
        }

        ResolvePrimaryReadout(
            profile);

        return profile;
    }

    private static string BuildReadoutLine(
        AnswerProfile profile)
    {
        if (profile == null)
            return string.Empty;

        string evidence =
            ResolveReadoutEvidence(
                profile);

        switch (profile.PrimaryReadout)
        {
            case "empty":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_empty",
                        EmptyReadoutLines),
                    evidence);

            case "nonsense":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_nonsense",
                        NonsenseReadoutLines),
                    evidence);

            case "angry":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_angry",
                        AngryReadoutLines),
                    evidence);

            case "cruel":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_cruel",
                        CruelReadoutLines),
                    evidence);

            case "righteous":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_righteous",
                        RighteousReadoutLines),
                    evidence);

            case "silly":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_silly",
                        SillyReadoutLines),
                    evidence);

            case "low_clarity":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_low_clarity",
                        LowClarityReadoutLines),
                    evidence);

            case "coherent":
                return FormatAdaptiveLine(
                    Pick(
                        "player_readout_coherent",
                        CoherentReadoutLines),
                    evidence);

            default:
                return string.Empty;
        }
    }

    private static string ResolveReadoutEvidence(
        AnswerProfile profile)
    {
        if (profile == null)
            return "the answer pattern";

        if (!string.IsNullOrWhiteSpace(
                profile.SampleReadout))
        {
            return SafeDisplay(
                profile.SampleReadout);
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleQuestionable))
        {
            return SafeDisplay(
                profile.SampleQuestionable);
        }

        if (!string.IsNullOrWhiteSpace(
                profile.SampleThoughtful))
        {
            return SafeDisplay(
                profile.SampleThoughtful);
        }

        if (!string.IsNullOrWhiteSpace(
                profile.RecurringTheme))
        {
            return profile.RecurringTheme;
        }

        if (!string.IsNullOrWhiteSpace(
                profile.Stimulus))
        {
            return SafeDisplay(
                profile.Stimulus);
        }

        return "the answer pattern";
    }

    private static string FormatAdaptiveLine(
        string template,
        string evidence)
    {
        if (string.IsNullOrWhiteSpace(
                template))
        {
            return string.Empty;
        }

        try
        {
            // note: Adaptive readout lines carry player evidence so repeated categories still feel tied to this save.
            return string.Format(
                template,
                string.IsNullOrWhiteSpace(
                    evidence)
                    ? "the answer pattern"
                    : evidence);
        }
        catch
        {
            return template;
        }
    }

    private static void ResolvePrimaryReadout(
        AnswerProfile profile)
    {
        if (profile == null ||
            profile.AnswerCount <= 0)
        {
            return;
        }

        int bestScore =
            0;

        string best =
            string.Empty;

        // note: Presentation readout priority favors strong weirdness or moral intent before ordinary coherence.
        ConsiderReadout(
            profile,
            "empty",
            profile.EmptyCount * 3 +
            profile.GenericOrRefusalCount,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "angry",
            profile.AngerCount * 4,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "nonsense",
            profile.NonsenseCount * 3 +
            profile.DuplicateGroups,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "cruel",
            profile.CruelCount * 3,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "low_clarity",
            profile.LowClarityCount * 2,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "righteous",
            profile.RighteousCount * 3,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "silly",
            profile.SillyCount * 2,
            ref best,
            ref bestScore);

        ConsiderReadout(
            profile,
            "coherent",
            profile.CoherentCount,
            ref best,
            ref bestScore);

        if (bestScore <= 0)
            return;

        profile.PrimaryReadout =
            best;

        profile.PrimaryReadoutCount =
            bestScore;

        profile.ResponseMode =
            ResolveResponseMode(
                best);

        profile.ResponseInstruction =
            ResolveResponseInstruction(
                profile.ResponseMode);
    }

    private static string ResolveResponseMode(
        string readout)
    {
        switch (readout)
        {
            case "empty":
            case "low_clarity":
                return "simplify_and_anchor";

            case "angry":
                return "deescalate_and_ground";

            case "nonsense":
                return "controlled_chaos";

            case "cruel":
                return "boundary_the_menace";

            case "silly":
                return "mirror_playfully";

            case "righteous":
            case "coherent":
                return "respect_the_signal";

            default:
                return string.Empty;
        }
    }

    private static string ResolveResponseInstruction(
        string responseMode)
    {
        switch (responseMode)
        {
            case "simplify_and_anchor":
                return "Use shorter concrete clauses, fewer abstractions, and a patient step-by-step handoff.";

            case "deescalate_and_ground":
                return "Acknowledge heat without escalating it; sound calm, competent, and gently firm.";

            case "controlled_chaos":
                return "Mirror a little strange logic while keeping one clear build task visible.";

            case "boundary_the_menace":
                return "Respect the dramatic menace as evidence while making clear that harm becomes bounded gameplay pressure, not permission.";

            case "mirror_playfully":
                return "Play along with the joke but keep the world functional and the player capable.";

            case "respect_the_signal":
                return "Use more precise language and treat the player as someone giving usable intent.";

            default:
                return string.Empty;
        }
    }

    private static void ConsiderReadout(
        AnswerProfile profile,
        string readout,
        int score,
        ref string best,
        ref int bestScore)
    {
        if (profile == null ||
            score <= bestScore)
        {
            return;
        }

        best =
            readout;

        bestScore =
            score;
    }

    private static void RememberReadoutSample(
        AnswerProfile profile,
        string sample)
    {
        if (profile == null ||
            !string.IsNullOrWhiteSpace(
                profile.SampleReadout) ||
            string.IsNullOrWhiteSpace(
                sample))
        {
            return;
        }

        // note: Keep one compact raw answer as evidence so Goddess readouts can feel aimed at the actual player input.
        profile.SampleReadout =
            sample;
    }

    private static void AddThemeCounts(
        string value,
        Dictionary<string, int> counts)
    {
        string lower =
            (value ?? string.Empty)
                .ToLowerInvariant();

        // note: These broad buckets are presentation hints, not a classifier that affects gameplay.
        AddThemeIfContains(
            lower,
            counts,
            "mercy",
            "mercy",
            "kindness",
            "forgive",
            "protect",
            "save");

        AddThemeIfContains(
            lower,
            counts,
            "vengeance",
            "revenge",
            "vengeance",
            "punish",
            "wrath",
            "destroy");

        AddThemeIfContains(
            lower,
            counts,
            "power",
            "power",
            "rule",
            "control",
            "dominion",
            "king",
            "queen");

        AddThemeIfContains(
            lower,
            counts,
            "trade",
            "trade",
            "coin",
            "merchant",
            "sell",
            "buy",
            "profit");

        AddThemeIfContains(
            lower,
            counts,
            "survival",
            "survive",
            "forest",
            "wood",
            "hunt",
            "shelter",
            "food");

        AddThemeIfContains(
            lower,
            counts,
            "magic",
            "magic",
            "spell",
            "mana",
            "arcane",
            "ritual",
            "curse");

        AddThemeIfContains(
            lower,
            counts,
            "stillness",
            "wait",
            "patience",
            "still",
            "rest",
            "silence",
            "sleep");

        AddThemeIfContains(
            lower,
            counts,
            "wandering",
            "wander",
            "road",
            "travel",
            "journey",
            "lost",
            "map");
    }

    private static void AddThemeIfContains(
        string lower,
        Dictionary<string, int> counts,
        string theme,
        params string[] needles)
    {
        for (int i = 0;
             i < needles.Length;
             i++)
        {
            if (lower.Contains(
                    needles[i]))
            {
                counts.TryGetValue(
                    theme,
                    out int count);

                counts[theme] =
                    count + 1;

                return;
            }
        }
    }

    private static bool IsQuestionableAnswer(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return true;
        }

        string trimmed =
            value.Trim();

        if (trimmed.Length >= 4 &&
            HasRepeatedCharacterRun(
                trimmed,
                4))
        {
            return true;
        }

        if (LooksLikeKeyboardMash(
                trimmed))
        {
            return true;
        }

        if (LooksLikeDenseConsonantMash(
                trimmed))
        {
            return true;
        }

        float alphaRatio =
            AlphaRatio(
                trimmed);

        float symbolRatio =
            SymbolRatio(
                trimmed);

        if (trimmed.Length >= 6 &&
            alphaRatio < 0.3f &&
            symbolRatio > 0.35f)
        {
            return true;
        }

        if (HasRepeatedSingleToken(
                trimmed))
        {
            return true;
        }

        return false;
    }

    private static bool LooksLikeDenseConsonantMash(
        string value)
    {
        string normalized =
            NormalizeAnswerKey(
                value);

        if (normalized.Length < 7 ||
            normalized.Contains(
                "/") ||
            normalized.Contains(
                "'"))
        {
            return false;
        }

        int letters =
            0;

        int vowels =
            0;

        int digits =
            0;

        int longestConsonantRun =
            0;

        int consonantRun =
            0;

        for (int i = 0;
             i < normalized.Length;
             i++)
        {
            char c =
                normalized[i];

            if (char.IsDigit(
                    c))
            {
                digits++;
                consonantRun =
                    0;
                continue;
            }

            if (!char.IsLetter(
                    c))
            {
                consonantRun =
                    0;
                continue;
            }

            letters++;

            if (IsPlainLatinVowel(
                    c))
            {
                vowels++;
                consonantRun =
                    0;
                continue;
            }

            consonantRun++;

            longestConsonantRun =
                Mathf.Max(
                    longestConsonantRun,
                    consonantRun);
        }

        if (letters < 7)
        {
            return false;
        }

        float vowelRatio =
            vowels /
            Mathf.Max(
                1f,
                letters);

        // note: This targets strong keyboard-sludge signals while leaving ordinary short names and slang alone.
        return
            longestConsonantRun >= 5 ||
            (normalized.Length >= 9 &&
             vowelRatio <= 0.18f) ||
            (digits > 0 &&
             letters >= 7 &&
             vowelRatio <= 0.28f);
    }

    private static bool IsPlainLatinVowel(
        char c)
    {
        switch (char.ToLowerInvariant(
                    c))
        {
            case 'a':
            case 'e':
            case 'i':
            case 'o':
            case 'u':
                return true;

            default:
                return false;
        }
    }

    private static bool LooksLikeKeyboardMash(
        string value)
    {
        string lower =
            NormalizeAnswerKey(
                value);

        if (lower.Length < 5)
        {
            return false;
        }

        return
            lower.Contains(
                "asdf") ||
            lower.Contains(
                "qwer") ||
            lower.Contains(
                "zxcv") ||
            lower.Contains(
                "hjkl") ||
            lower.Contains(
                "fdsa") ||
            lower.Contains(
                "rewq") ||
            lower.Contains(
                "vcxz");
    }

    private static bool HasRepeatedCharacterRun(
        string value,
        int required)
    {
        char previous =
            '\0';

        int run =
            0;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char current =
                char.ToLowerInvariant(
                    value[i]);

            if (char.IsWhiteSpace(
                    current))
            {
                previous =
                    '\0';

                run =
                    0;

                continue;
            }

            if (current == previous)
            {
                run++;
            }
            else
            {
                previous =
                    current;

                run =
                    1;
            }

            if (run >= required)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasRepeatedSingleToken(
        string value)
    {
        string[] tokens =
            (value ?? string.Empty)
                .Split(
                    new[] { ' ', '\t', '\r', '\n' },
                    StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length < 4)
        {
            return false;
        }

        Dictionary<string, int> counts =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        int max =
            0;

        for (int i = 0;
             i < tokens.Length;
             i++)
        {
            string key =
                NormalizeAnswerKey(
                    tokens[i]);

            if (string.IsNullOrWhiteSpace(
                    key))
            {
                continue;
            }

            counts.TryGetValue(
                key,
                out int count);

            count++;

            counts[key] =
                count;

            max =
                Mathf.Max(
                    max,
                    count);
        }

        return
            max >= 3 &&
            max >=
            Mathf.CeilToInt(
                tokens.Length * 0.7f);
    }

    private static bool IsGenericOrRefusal(
        string value)
    {
        string key =
            NormalizeAnswerKey(
                value);

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return true;
        }

        switch (key)
        {
            case "no":
            case "none":
            case "nothing":
            case "skip":
            case "pass":
            case "idk":
            case "dontknow":
            case "idon'tknow":
            case "whatever":
            case "anything":
            case "n/a":
            case "na":
            case "yes":
            case "ok":
            case "okay":
            case "sure":
                return true;

            default:
                return false;
        }
    }

    private static bool LooksLikeSillyAnswer(
        string value)
    {
        string lower =
            (value ?? string.Empty)
                .ToLowerInvariant();

        string key =
            NormalizeAnswerKey(
                value);

        if (string.IsNullOrWhiteSpace(
                key))
        {
            return false;
        }

        // note: These cues identify intentionally unserious answers for Goddess tone only; they do not invalidate the origin.
        return
            lower.Contains(
                "lol") ||
            lower.Contains(
                "lmao") ||
            lower.Contains(
                "haha") ||
            lower.Contains(
                "silly") ||
            lower.Contains(
                "goofy") ||
            lower.Contains(
                "clown") ||
            lower.Contains(
                "meme") ||
            lower.Contains(
                "yeet") ||
            key.Contains(
                "butt") ||
            key.Contains(
                "poop") ||
            key.Contains(
                "fart");
    }

    private static bool LooksLikeCruelAnswer(
        string value)
    {
        string lower =
            (value ?? string.Empty)
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(
                lower))
        {
            return false;
        }

        // note: Hostile intent is presentation evidence; runtime morality still comes from persisted structured records.
        return
            ContainsAnyWord(
                lower,
                "evil",
                "cruel",
                "murder",
                "kill",
                "slaughter",
                "torture",
                "dominate",
                "enslave",
                "betray",
                "tyrant",
                "villain",
                "blood",
                "suffering") ||
            lower.Contains(
                "demon lord") ||
            lower.Contains(
                "demonlord");
    }

    private static bool LooksLikeAngryAnswer(
        string value)
    {
        string lower =
            (value ?? string.Empty)
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(
                lower))
        {
            return false;
        }

        // note: Angry answers change Goddess handling toward grounding instead of adding more provocation.
        return
            ContainsAnyWord(
                lower,
                "angry",
                "rage",
                "furious",
                "hate",
                "hated",
                "pissed",
                "revenge",
                "vengeance",
                "wrath",
                "scream",
                "burn",
                "break",
                "destroy");
    }

    private static bool LooksLikeRighteousAnswer(
        string value)
    {
        string lower =
            (value ?? string.Empty)
                .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(
                lower))
        {
            return false;
        }

        // note: Righteous/protective language gets its own readout so noble answers do not feel like generic fantasy filler.
        return
            ContainsAnyWord(
                lower,
                "righteous",
                "justice",
                "mercy",
                "protect",
                "defend",
                "save",
                "rescue",
                "honor",
                "oath",
                "good",
                "innocent",
                "weak",
                "kind",
                "compassion",
                "heal");
    }

    private static bool ContainsAnyWord(
        string lower,
        params string[] words)
    {
        if (string.IsNullOrWhiteSpace(
                lower) ||
            words == null)
        {
            return false;
        }

        for (int i = 0;
             i < words.Length;
             i++)
        {
            string word =
                words[i];

            if (string.IsNullOrWhiteSpace(
                    word))
            {
                continue;
            }

            if (lower.Contains(
                    word))
            {
                return true;
            }
        }

        return false;
    }

    private static float AlphaRatio(
        string value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return 0f;
        }

        int letters =
            0;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            if (char.IsLetter(
                    value[i]))
            {
                letters++;
            }
        }

        return
            letters /
            Mathf.Max(
                1f,
                value.Length);
    }

    private static float SymbolRatio(
        string value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return 0f;
        }

        int symbols =
            0;

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                value[i];

            if (!char.IsLetterOrDigit(
                    c) &&
                !char.IsWhiteSpace(
                    c))
            {
                symbols++;
            }
        }

        return
            symbols /
            Mathf.Max(
                1f,
                value.Length);
    }

    private static string NormalizeAnswerKey(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        StringBuilder sb =
            new StringBuilder(
                value.Length);

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                char.ToLowerInvariant(
                    value[i]);

            if (char.IsLetterOrDigit(
                    c) ||
                c == '/' ||
                c == '\'')
            {
                sb.Append(
                    c);
            }
        }

        return
            sb.ToString();
    }

    private static bool WasRecentlyUsed(
        string topic)
    {
        foreach (string recent in RecentPlayerTopics)
        {
            if (string.Equals(
                    recent,
                    topic,
                    StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static void RememberPlayerTopic(
        string topic)
    {
        if (string.IsNullOrWhiteSpace(
                topic))
        {
            return;
        }

        RecentPlayerTopics.Enqueue(
            topic);

        while (RecentPlayerTopics.Count >
               MaxRecentPlayerTopics)
        {
            RecentPlayerTopics.Dequeue();
        }
    }

    private static string SafeDisplay(
        string value)
    {
        return
            TrimTo(
                (value ?? string.Empty)
                    .Replace(
                        '\r',
                        ' ')
                    .Replace(
                        '\n',
                        ' ')
                    .Trim(),
                92);
    }

    private static string TrimTo(
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

        if (clean.Length <=
            maxLength)
        {
            return clean;
        }

        return
            clean
                .Substring(
                    0,
                    Mathf.Max(
                        0,
                        maxLength))
                .TrimEnd();
    }

    private static string PromptSafe(
        string value)
    {
        return
            SafeDisplay(
                value)
                .Replace(
                    "\"",
                    "'");
    }

    private sealed class PlayerLineCandidate
    {
        public readonly string Topic;

        public readonly string Line;

        public PlayerLineCandidate(
            string topic,
            string line)
        {
            Topic =
                topic ?? string.Empty;

            Line =
                line ?? string.Empty;
        }
    }

    private sealed class AnswerProfile
    {
        public int AnswerCount;

        public int EmptyCount;

        public int VeryShortCount;

        public int LongCount;

        public int QuestionableCount;

        public int LowClarityCount;

        public int AngerCount;

        public int NonsenseCount;

        public int SillyCount;

        public int CruelCount;

        public int RighteousCount;

        public int CoherentCount;

        public int DuplicateGroups;

        public int GenericOrRefusalCount;

        public int RecurringThemeCount;

        public string SampleQuestionable =
            string.Empty;

        public string SampleShort =
            string.Empty;

        public string SampleThoughtful =
            string.Empty;

        public string SampleReadout =
            string.Empty;

        public string RecurringTheme =
            string.Empty;

        public string Stimulus =
            string.Empty;

        public string PrimaryReadout =
            string.Empty;

        public int PrimaryReadoutCount;

        public string ResponseMode =
            string.Empty;

        public string ResponseInstruction =
            string.Empty;

        public bool HasAnswers =>
            AnswerCount > 0;

        public bool HasStrongQuestionableSignal =>
            QuestionableCount > 0 ||
            DuplicateGroups > 0 ||
            GenericOrRefusalCount >= 2 ||
            AngerCount > 0 ||
            CruelCount > 0 ||
            LowClarityCount >= 2;
    }

    private static string Pick(
        string bagKey,
        string[] source,
        string location = "")
    {
        if (source == null ||
            source.Length == 0)
        {
            return string.Empty;
        }

        if (!Bags.TryGetValue(
                bagKey,
                out Queue<string> bag) ||
            bag == null ||
            bag.Count == 0)
        {
            bag =
                BuildShuffledBag(
                    bagKey,
                    source);

            Bags[bagKey] =
                bag;
        }

        if (bag.Count == 0)
        {
            // note: During one generation, silence is better than repeating a visible Goddess line.
            return string.Empty;
        }

        string template =
            bag.Dequeue();

        UsedTemplatesThisGeneration.Add(
            template);

        string familyKey =
            BuildTemplateFamilyKey(
                template);

        if (!string.IsNullOrWhiteSpace(
                familyKey))
        {
            // note: Family keys prevent visually similar grab-bag shells from clumping during one generation.
            UsedTemplateFamiliesThisGeneration.Add(
                familyKey);
        }

        LastTemplateByBag[bagKey] =
            template;

        string safeLocation =
            string.IsNullOrWhiteSpace(
                location)
                ? "this place"
                : location.Trim();

        try
        {
            string formatted =
                string.Format(
                    template,
                    safeLocation);

            // note: A final scrub catches escaped or malformed placeholders after string.Format succeeds.
            return SanitizePickedLine(
                formatted,
                safeLocation);
        }
        catch
        {
            // note: A damaged grab-bag template must never leak "{0}" into the player-facing transcript.
            return SanitizePickedLine(
                template,
                safeLocation);
        }
    }

    private static Queue<string> BuildShuffledBag(
        string bagKey,
        string[] source)
    {
        List<string> shuffled =
            new List<string>();

        for (int sourceIndex = 0;
             sourceIndex < source.Length;
             sourceIndex++)
        {
            string candidate =
                source[sourceIndex];

            if (string.IsNullOrWhiteSpace(
                    candidate) ||
                UsedTemplatesThisGeneration.Contains(
                    candidate) ||
                IsTemplateFamilyUsed(
                    candidate) ||
                !IsGenerationFallbackTemplateAllowed(
                    candidate))
            {
                continue;
            }

            // note: Each grab-bag template may appear at most once during a single generation transcript.
            shuffled.Add(
                candidate);
        }

        if (shuffled.Count == 0)
        {
            // note: Silence is preferable to reviving filtered-out pseudo-profound filler lines.
            return
                new Queue<string>();
        }

        for (int i =
                 shuffled.Count - 1;
             i > 0;
             i--)
        {
            int swapIndex =
                UnityEngine.Random.Range(
                    0,
                    i + 1);

            string temp =
                shuffled[i];

            shuffled[i] =
                shuffled[swapIndex];

            shuffled[swapIndex] =
                temp;
        }

        /*
         * When beginning a new cycle, avoid placing the previous cycle's
         * final line at the front of the new bag.
         */
        if (shuffled.Count > 1 &&
            LastTemplateByBag.TryGetValue(
                bagKey,
                out string previous) &&
            string.Equals(
                shuffled[0],
                previous,
                StringComparison.Ordinal))
        {
            int swapIndex =
                UnityEngine.Random.Range(
                    1,
                    shuffled.Count);

            string temp =
                shuffled[0];

            shuffled[0] =
                shuffled[swapIndex];

            shuffled[swapIndex] =
                temp;
        }

        return
            new Queue<string>(
                shuffled);
    }

    private static bool IsTemplateFamilyUsed(
        string template)
    {
        string familyKey =
            BuildTemplateFamilyKey(
                template);

        return
            !string.IsNullOrWhiteSpace(
                familyKey) &&
            UsedTemplateFamiliesThisGeneration.Contains(
                familyKey);
    }

    private static string BuildTemplateFamilyKey(
        string template)
    {
        if (string.IsNullOrWhiteSpace(
                template))
        {
            return string.Empty;
        }

        string normalized =
            NormalizeTemplateText(
                template
                    .Replace(
                        "{0}",
                        "PLACE"));

        string[] words =
            normalized.Split(
                new[] { ' ' },
                StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 4)
            return string.Empty;

        if (StartsWithWords(
                words,
                "i",
                "am"))
        {
            return
                "i_am|" +
                words[Mathf.Min(
                    2,
                    words.Length - 1)];
        }

        if (StartsWithWords(
                words,
                "i",
                "have"))
        {
            return
                "i_have|" +
                words[Mathf.Min(
                    2,
                    words.Length - 1)];
        }

        if (StartsWithWords(
                words,
                "place") ||
            StartsWithWords(
                words,
                "the",
                "people") ||
            StartsWithWords(
                words,
                "your",
                "answers"))
        {
            return
                words[0] +
                "|" +
                words[1] +
                "|" +
                words[2];
        }

        return
            words[0] +
            "|" +
            words[1] +
            "|" +
            words[2] +
            "|" +
            words[3];
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

    private static string NormalizeTemplateText(
        string value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        StringBuilder builder =
            new StringBuilder(
                value.Length);

        for (int i = 0;
             i < value.Length;
             i++)
        {
            char c =
                char.ToLowerInvariant(
                    value[i]);

            builder.Append(
                char.IsLetterOrDigit(
                    c)
                    ? c
                    : ' ');
        }

        string[] parts =
            builder
                .ToString()
                .Split(
                    new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);

        return
            string.Join(
                " ",
                parts);
    }

    private static string SanitizePickedLine(
        string line,
        string safeLocation)
    {
        if (string.IsNullOrWhiteSpace(
                line))
        {
            return string.Empty;
        }

        // note: Visible formatting tokens break the Goddess illusion immediately, so replace them late.
        return line
            .Replace(
                "{0}",
                safeLocation)
            .Replace(
                "{{0}}",
                safeLocation)
            .Replace(
                "{location}",
                safeLocation)
            .Trim();
    }

    private static bool IsGenerationFallbackTemplateAllowed(
        string template)
    {
        if (string.IsNullOrWhiteSpace(
                template))
        {
            return false;
        }

        string normalized =
            template
                .ToLowerInvariant();

        // note: Older optional pools must obey the same speech boundary as generated lines; first-person technical chatter is not a second permitted persona.
        if (!YQGoddessGenerationDialogue.IsSpokenVoiceFieldAcceptable(template.Replace("{0}", "this place"), 6) ||
            normalized.Contains("hot-loading") || normalized.Contains("i am typing") ||
            normalized.Contains("unusable output") || normalized.Contains("load-bearing") ||
            normalized.Contains("i should invent") || normalized.Contains("i can fake") ||
            normalized.Contains("loading in"))
            return false;

        // note: Prefer grounded working-thought lines over omniscient continuity/backdated-history jokes.
        return
            !normalized.Contains(
                "has always") &&
            !normalized.Contains(
                "always lived") &&
            !normalized.Contains(
                "always stood") &&
            !normalized.Contains(
                "always existed") &&
            !normalized.Contains(
                "since before") &&
            !normalized.Contains(
                "backdated") &&
            !normalized.Contains(
                "retroactively") &&
            !normalized.Contains(
                "grandparents") &&
            !normalized.Contains(
                "childhoods") &&
            !normalized.Contains(
                "remember events") &&
            !normalized.Contains(
                "events that never") &&
            !normalized.Contains(
                "continuity") &&
            !normalized.Contains(
                "destiny") &&
            !normalized.Contains(
                "mortals") &&
            !normalized.Contains(
                "mortal ") &&
            !normalized.Contains(
                "reality") &&
            !normalized.Contains(
                "ancient") &&
            !normalized.Contains(
                "civilization") &&
            !normalized.Contains(
                "prophecy") &&
            !normalized.Contains(
                "metaphysical") &&
            !normalized.Contains(
                "thread") &&
            !normalized.Contains(
                "pattern");
    }
}
