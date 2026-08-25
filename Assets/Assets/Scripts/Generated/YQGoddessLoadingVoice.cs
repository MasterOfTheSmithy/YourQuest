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

    private static readonly string[]
        OriginTransitionFallback =
        {
            // note: Each fallback opens with rehearsed divinity and then lets the immediate construction problem break the performance.
            "Your essence is revealed. Behold as I prepare its destined realm—no, the realm needs ground first. Obviously. One sacred moment.",

            "I have weighed your answers upon the eternal scales. They tipped over, but the result is valid. I am building around it now.",

            "Rise, chosen mortal, and witness creation. Not yet—there is nowhere safe to rise. Remain metaphorically risen while I fix that.",

            "By my flawless discernment, your origin is sealed. I only need to decide where it goes, which I definitely decided moments ago.",

            "Your name and purpose are written into eternity. The handwriting is cramped. Geography will make it look intentional.",

            "I pronounce your beginning complete. Quietly. The world behind it is still mostly instructions and one alarming hill."
        };

    private static readonly string[]
        WorldPlanFinished =
        {
            "Behold the ordained shape of the world. I chose the roads before the settlements, but that is an advanced divine technique and not a mistake.",

            "The map obeys my perfect design. One road currently obeys the ocean instead. I am correcting its devotion.",

            "Thus have I divided settlement from wilderness. The boundary is drifting. Please regard the drift as sacred until I catch it.",

            "Witness a coherent cosmos. I am counting the settlements again because omniscience benefits from verification. One, two—yes. Coherent.",

            "The grand design is complete. I am now moving three important things I placed on top of each other. Ceremonially."
        };

    private static readonly string[]
        StableScaffold =
        {
            "The first creation was a sacred preliminary vision. It also could not stand up. I am invoking the plainer, load-bearing vision.",

            "By divine wisdom, I reject unnecessary ornament. This decision is unrelated to the ornament arriving inside out.",

            "The ornate reality has been judged unworthy. Not broken—unworthy. The stable one is already replacing it.",

            "I foresaw the need for a simpler world. I foresaw it immediately after the complicated one failed, which still counts.",

            "Witness my merciful restraint. Fewer moving pieces means fewer pieces can fall on you, and I meant to discover that."
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
            "You repeated yourself. I noticed. Repetition is either conviction, panic, or a keyboard with limited ambitions.",

            "Several answers were identical. Consistency is suspicious, but efficient.",

            "The same thought returned more than once. I will assume this is theme rather than forgetfulness.",

            "You pressed one idea through multiple doors. Very well. Reality understands blunt instruments."
        };

    private static readonly string[]
        GenericAnswerLines =
        {
            "Some answers were so generic they could have been inherited from furniture. I used them anyway.",

            "You offered a few non-answers. They are not empty. They are merely ashamed of being evidence.",

            "There was evasion in the record. I have built worlds from less. They were mostly survivable.",

            "Several replies tried to leave before becoming meaning. I held them still."
        };

    private static readonly string[]
        ThoughtfulAnswerLines =
        {
            "A few answers were unexpectedly considered. I have recorded this anomaly without celebration.",

            "You were thoughtful in places. Dangerous habit. It encourages reality to become specific.",

            "There is actual intent among the words. Not everywhere. Let us not become sentimental.",

            "Some of your answers appear to have been written by the same mind twice. Encouraging, briefly."
        };

    private static readonly string[]
        ThemeAnswerLines =
        {
            "{0} kept returning in your answers. I will treat recurrence as intent, because pretending otherwise wastes both of us.",

            "You circled {0} more than once. Mortals call that theme when they want repetition to sound educated.",

            "{0} appears to be following you through your own answers. Convenient. I prefer evidence that walks.",

            "Several answers leaned toward {0}. I have seen thinner patterns become religions.",

            "Your words keep touching {0}. I will not call it destiny yet. Destiny becomes smug when named early."
        };

    private static readonly string[]
        OriginStimulusLines =
        {
            "Your first pressure is {0}. I have made it usable. This is more kindness than the material deserves.",

            "The origin settled around {0}. A mortal might call that self-knowledge. I will wait for proof.",

            "{0} has become the thread. Do not tug too theatrically; it is not impressed.",

            "I found {0} beneath the answers. It was not hiding well.",

            "Your answers condensed into {0}. Reality accepts this kind of paperwork, regrettably."
        };

    // ============================================================
    // NPC — SETTLEMENT CREATION
    // ============================================================

    private static readonly string[]
        SettlementPopulationCreating =
        {
            // note: These fast fallback lines should feel like active world curation, not lore narration.
            "Hold. {0} has doors and no social damage yet. I am fixing it. Calmly. Obviously.",

            "{0} needs people fast. I am rationing names and pretending this is a sustainable pipeline...",

            "{0} has houses but no arguments coming from them. Unusable output. One second...",

            "Threading lives through {0}. If any duplicate, I will repair it before you can form an opinion...",

            "{0} requires jobs, grudges, and names. Names are the part that keeps biting me...",

            "Do not look yet. I am hot-loading ownership disputes into {0}...",

            "I know the shape of {0}'s people. The labels are being difficult...",

            "The streets of {0} are empty in a very accusatory way. I am typing faster than is dignified...",

            "I am placing memories into {0}. Some may even pass inspection...",

            "{0} needs people before it starts looking suspiciously empty...",

            "I am compressing {0}'s social history until it stops leaking at the edges...",

            "Someone in {0} needs to know everyone else's business. I am choosing them now...",

            "I am deciding who wakes first in {0}, who works latest, and who complains about both...",

            "{0} has an economy but presently nobody to misunderstand it...",

            "One moment. Assigning professions before the people fully compile...",

            "I am giving the people of {0} reasons to believe they belong there...",

            "{0} needs old grudges. I can fake age. I cannot fake bookkeeping. Yet...",

            "{0} needs food work. People become strange when I forget lunch mechanics...",

            "I am determining which citizens of {0} avoid one another in the marketplace...",

            "The doors in {0} need owners. The owners need memories. The memories need contradictions...",

            "I am deciding whose mother warned them never to leave {0}...",

            "One moment. Several people in {0} are acquiring complicated opinions about their neighbors...",

            "I am filling {0} with small ambitions. They are load-bearing, apparently...",

            "Someone must remember when {0} was founded. I suppose I should invent the founding...",

            "I am giving {0} elders who insist things were better before you arrived...",

            "The people of {0} are nearly convinced they have always existed...",

            "I am assigning friendships in {0}. Betrayals will emerge naturally...",

            "Hold still. I am making {0} socially inconvenient...",

            "{0} requires names, histories, routines, and at least one person everybody distrusts...",

            "I am putting lives behind the windows of {0}...",

            "People need context. I am supplying {0} with entirely too much of it...",

            "I have reached the part where everyone in {0} needs context. Rude of them...",

            "I am choosing who in {0} tells the truth badly and who lies beautifully...",

            "{0} will feel lived in shortly. Please ignore the metaphysical scaffolding and my posture...",

            "The citizens of {0} are loading in with opinions already attached...",

            "I am giving {0} people who remember events that occurred before I made the landscape...",

            "Almost there. {0} needs enough personal problems to become believable and I need water..."
        };

    // ============================================================
    // NPC — HOSTILE CREATION
    // ============================================================

    private static readonly string[]
        HostilePopulationCreating =
        {
            // note: Hostile setup lines keep the anxious coder texture but avoid inventing extra facts.
            "{0} is too safe on paper. That is suspicious and also my fault...",

            "{0} needs one clean threat profile. Clean is optimistic. I am still saying it calmly...",

            "Putting one sensible-traveler deterrent into {0}. Please do not inspect the staging layer...",

            "Ah, {0}. Danger slot is empty. Embarrassing. Filling it now...",

            "{0} needs a threat with a readable silhouette and terrible manners...",

            "Waking something in {0}. It gets a name, a boundary, and no apology...",

            "{0} requires one local horror. Not three. I am showing restraint...",

            "Something hostile is taking shape in {0}. The edges are cooperating. Barely...",

            "{0} has been peaceful for several seconds. Unacceptable...",

            "Creating the reason nobody builds closer to {0}. Naming it before it wanders...",

            "Deciding whether the danger in {0} speaks. Silence is cheaper, but suspicious...",

            "Almost. {0} needs one thing you should not approach alone or smug...",

            "Installing unreasonable territorial expectations in {0}. It is taking to them beautifully..."
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
            // note: Terrain lines expose frantic physical corrections beneath a rehearsed creator-Goddess performance.
            "Behold, I raise the eternal mountains—too high. Much too high. Witness me lower them with equal divinity.",

            "By my decree, the rivers shall run... downhill. Yes, naturally. I am turning this valley around before the water notices.",

            "Let firmament divide from earth. No, that is the horizon. The earth is the lower one. Correcting both.",

            "I command this hill to rise in majesty. Stop. Stop rising. The command has been divinely amended.",

            "Witness the birth of a continent. The western edge is curling upward, but I have several very sacred tools for that.",

            "Thus I lay the ground beneath you. Not that piece—it is still soft. I meant the piece immediately beside it.",

            "The valleys answer my infinite wisdom. One answered upside down. I am handling the dissenter personally.",

            "Creation proceeds precisely as foretold: stone, soil, slope, and—no, not there. Move the slope left.",

            "I bless this horizon with perfect balance. It is visibly crooked. The blessing may require a second application.",

            "The land shall bear the weight of destiny. First it must bear its own weight. I am reinforcing the embarrassing section."
        };

    // ============================================================
    // PHYSICAL WORLD — SETTLEMENT MATERIALIZATION
    // ============================================================

    private static readonly string[]
        SettlementMaterialization =
        {
            "And here I bestow {0}, jewel of the—no, it is facing backward. The jewel will rotate.",

            "By sovereign decree, the streets of {0} shall meet. They currently miss by six feet. I am narrowing the decree.",

            "Witness {0} descend from possibility. Gently. Gently—stop. I am correcting the ground beneath it.",

            "I have ordained a marketplace for {0}. I appear to have ordained it inside a house. Both are moving.",

            "Thus rises {0}, exactly where I intended after rejecting the first three places I intended.",

            "The sacred roads of {0} now connect every district. Except that one. It is being reclassified as a scenic mistake.",

            "I grant {0} an ancient and harmonious layout. Please allow the ancient buildings time to stop overlapping.",

            "Behold {0}, made habitable by my boundless power and several rapid, unrecorded corrections."
        };

    // ============================================================
    // PHYSICAL WORLD — BUILDINGS
    // ============================================================

    private static readonly string[]
        BuildingMaterialization =
        {
            "By my hand, every house in {0} shall stand true. That roof is not true. I am turning it over.",

            "I grant {0} walls, doors, and sacred shelter. The doors are in the walls this time. Nearly all of them.",

            "Witness architecture obey me. One house has mistaken itself for a staircase; I am speaking to it firmly.",

            "The foundations of {0} are eternally secure. I am placing them now, beneath the buildings, where they apparently belong.",

            "By divine proportion, every doorway shall admit a mortal. That one admits half a mortal. Widening it.",

            "I crown {0} with roofs against the storm. Two roofs are crowning the same house. Redistributing majesty.",

            "Thus are the homes of {0} made whole. No, the chimneys do need to face outside. One moment.",

            "Behold, a flawless street of dwellings. Regard it from here while I quietly pull three dwellings out of the road."
        };

    // ============================================================
    // PHYSICAL WORLD — ENVIRONMENT
    // ============================================================

    private static readonly string[]
        EnvironmentMaterialization =
        {
            "I call forth the primeval forest. Not across the road—back, back. Trees are less obedient than the hymns imply.",

            "By my blessing, life spreads across the land. It is spreading in rows. I am disordering it by hand.",

            "Witness nature, untouched by mortal design. I am currently moving every boulder so mortals can actually walk through it.",

            "I summon ancient wilderness. That tree is inside a house. The wilderness has exceeded its jurisdiction.",

            "Let root and branch reclaim the empty places. Not the doorway. I should have specified the doorway.",

            "The stones fall according to my unknowable purpose. I know the purpose. The purpose is no longer blocking the stairs.",

            "Thus I clothe the world in green abundance. Some abundance is floating. I am lowering it with solemnity.",

            "Behold a wilderness older than memory. Please disregard how rapidly I am rotating that suspiciously young forest."
        };

    // ============================================================
    // WORLD PLAN CHANGED
    // ============================================================

    private static readonly string[]
        WorldPlanChanged =
        {
            "I have issued a divine revision to reality. This is not changing my mind; it is omniscience arriving in installments.",

            "That world was a prophetic illustration. This world is the prophecy. I am replacing the roads before anyone notices.",

            "By eternal decree, history has always taken this shape. History is objecting, so I am rewriting the loud section first.",

            "Witness my foresight. I foresaw a better arrangement immediately after completing the worse arrangement.",

            "Reality has not shifted. Your perspective has shifted. Also the mountain. I am putting the mountain back."
        };

    // ============================================================
    // TERMINAL FAILURE
    // ============================================================

    private static readonly string[]
        TerminalFailure =
        {
            "By my absolute authority, creation pauses. I did not fail. The world failed to understand me, and I am checking what I said.",

            "No. That is not a sacred mystery; that is a building beneath the ground. I cannot let you enter until I retrieve it.",

            "I proclaim this reality temporarily forbidden. Please do not ask why the statue is lying down. I know why. Mostly.",

            "The cosmos has revealed a hidden contradiction. I put it there accidentally, but discovering it was exceptionally divine.",

            "Remain beyond the threshold. My omnipotence requires a brief retry and possibly a smaller mountain.",

            "Creation is proceeding according to a higher plan. I am writing the higher plan now. It begins with fixing this.",

            "I have not lost control. Control is merely distributed across several emergencies. You are safest outside while I collect it."
        };

    // ============================================================
    // POPULATION COMPLETE
    // ============================================================

    private static readonly string[]
        PopulationComplete =
        {
            "By my breath, every soul awakens in its appointed place. Two awakened in the same chair. The appointment is being corrected.",

            "Witness life spread across my creation. I am checking the names because three people answered at once when I said Elian.",

            "The people remember lives stretching back generations. I wrote those generations quickly, but with tremendous divine sincerity.",

            "Thus are the settlements inhabited and the wilds given teeth. The teeth have names. I may have reversed two of them.",

            "Every mortal now possesses a history, a purpose, and somewhere to stand. I am quietly adding somewhere for one of them to stand.",

            "My creation lives. Please receive this as a miracle and not as several hundred simultaneous administrative emergencies."
        };

    // ============================================================
    // FINAL REVEAL
    // ============================================================

    private static readonly string[]
        FinalReveal =
        {
            "Behold: your world, complete by my infallible hand. The hand is still moving one tree. Enter after the tree stops.",

            "I open the threshold by divine decree. If a road shifts while you cross it, that is perspective and absolutely not unfinished work.",

            "Creation stands ready. I have checked the ground twice, the doors once, and the horizon enough. You may enter.",

            "Witness the realm I promised you. It is real, inhabited, and no longer making the alarming sound. Go carefully.",

            "The world is complete. Complete means safe to enter, not immune to further divine improvements performed behind you.",

            "By all the authority vested in me by—by me, apparently—you may begin. I will keep holding the edges together."
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
    }

    public static string BuildQuestionnaireContextForPrompt(
        PlayerState state,
        IReadOnlyList<string> directAnswers = null)
    {
        AnswerProfile profile =
            AnalyzeAnswers(
                state,
                directAnswers);

        if (!profile.HasAnswers)
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

        if (!profile.HasAnswers)
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
