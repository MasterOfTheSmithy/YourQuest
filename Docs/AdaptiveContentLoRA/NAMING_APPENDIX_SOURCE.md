# /appendix — YourQuest Adaptive Naming Schematic

## 1. PURPOSE

Create a consistent naming grammar for dynamically generated:

- abilities;
- skills;
- spells;
- techniques;
- titles;
- classes;
- class evolutions;
- items;
- weapons;
- armor;
- artifacts;
- quests;
- Unique Opportunities;
- legendary chains;
- NPC epithets;
- factions;
- discoveries;
- world events.

Names must feel authored, memorable and grounded in **why the player received them**.

The naming system must avoid both:

1. generic procedural-RPG names; and
2. overly dramatic AI-generated names that make every reward sound legendary.

The model generates identity.

Deterministic code validates uniqueness, conflicts, supported content type and persistence.

---

# 2. CORE NAMING EQUATION

Every generated adaptive name should derive from some subset of:
```diff
PLAYER BEHAVIOR
+
MECHANICAL CONSEQUENCE
+
WORLD CONTEXT
+
SYMBOL / MOTIF
+
RARITY
+
HISTORY
=
NAME
```

The model should be able to explain which components produced the identity.

Example:
```yaml
behavior:
repeatedly fights without weapons

behavior nuance:
protects allies rather than maximizing damage

mechanic:
interception / guard

world motif:
local culture associates oak with protection

rarity:
rare

history:
saved travelers during three unrelated encounters
```

Possible result:
```
Oakbound Hand
```

rather than:
```
Legendary Divine Fist Master
```

---

# 3. NAMING AXES

Internally represent names using semantic axes.
```arduino
IDENTITY
What is the player becoming?

METHOD
How do they act?

MOTIF
What image, element, creature, material or concept represents it?

CONSEQUENCE
What does the mechanic actually change?

CONTEXT
What place, faction, NPC, culture or event gave this identity meaning?

TONE
Practical / mystical / martial / religious / scholarly / infamous / folk.

RARITY
Ordinary / notable / rare / unique / legendary.
```

Not every name uses every axis.

More axes do NOT automatically produce a better name.

---

# 4. RARITY CONTROLS LANGUAGE

Naming intensity must correspond to actual rarity.

## COMMON / ORDINARY

Prefer practical functional names.

Patterns:
```css
[Technique]
[Tool] Handling
[Action] Practice
[Material] Working
[Weapon] Form
```

Examples:
```
Measured Cut
Field Dressing
Ashwood Working
Deflecting Step
Longbow Discipline
```

Avoid mythological grandeur.

---

## NOTABLE / PERSONALIZED

Names begin reflecting how this player specifically behaves.

Patterns:
```css
[Descriptor] + [Role]
[Motif] + [Technique]
[Method] of the [Motif]
[Behavioral Concept]
```

Examples:
```
Patient Hunter
Rootstep
Sheltering Hand
Quiet Pursuit
Wayfarer's Measure
```

These should feel personal without implying historical importance.

---

## RARE

May use metaphor, local lore and unusual identity.

Patterns:
```css
[Motif]bound [Role]
[Role] of [Specific Context]
[Metaphorical Identity]
[Verb/Noun] + unusual compound
```

Examples:
```
Oakbound Hand
Ashwalker
Keeper of Strays
Grave Mercy
Stonewake
Thread Between Blades
```

Rare names should become recognizable identities.

---

## UNIQUE

Names can imply that a genuinely unusual event occurred.

Prefer concise names with substantial semantic weight.

Patterns:
```css
The [Specific Identity]
[Unusual Compound]
[Role] of the [Canonical Event/Place]
[Phrase carrying unresolved mystery]
```

Examples:
```vbnet
The Unburied Friend
Rootless Warden
The Fifth Bell
Keeper of the Unclaimed
Heir to No Banner
The Road Beneath Storms
```

Unique does not require:
```
Legendary
Ultimate
Supreme
Divine
God
Infinite
Eternal
```

Often a restrained unique name is more memorable.

---

## LEGENDARY

Legendary naming is reserved for identities whose history genuinely justifies mythic treatment.

Names may reference:

- historical precedent;
- unique world event;
- ancient office;
- major transformation;
- exceptional accomplishment;
- culturally important prophecy/tradition where canonically supported.

Examples of structural style:
```sql
Bearer of the Last Ember
The Crownless Saint
Warden of the Broken Gate
Voice Beneath the Mountain
The One the River Remembered
```

These examples define tone only.

Do not hard-code them as available player rewards.

Legendary language without legendary evidence is a naming failure.

---

# 5. CONTENT-TYPE GRAMMARS

Different content types must sound different.

## ABILITIES

Abilities describe **something the player can do**.

Prefer:
```vbnet
action
motion
impact
technique
brief metaphor tied to function
```

Examples:
```
Rootstep
Guard Exchange
Splinterguard
Second Draw
Ashen Reach
Quiet Landing
```

Avoid turning every ability into a sentence.

An ability name should usually be short enough to fit naturally in combat UI.

---

## SKILLS

Skills represent learned competency, specialization or passive mastery.

Prefer:
```csharp
[Domain] + Mastery/Practice/Instinct/Discipline
[Behavioral Concept]
[Role-derived Expertise]
```

Examples:
```
Trail Instinct
Patient Aim
Field Alchemy
Stone Sense
Measured Guard
Beast Rapport
```

---

## SPELLS

Spells may use somewhat stronger imagery than ordinary abilities.

Generate from:
```diff
effect
+
casting behavior
+
magical tradition
+
motif
```

Examples:
```
Cinder Thread
Lantern Ward
Winter's Grasp
Ash Veil
Glasswind
```

Spell names must not imply unsupported effects.

A healing spell called:
```
Resurrection of the Eternal Sun
```

must not merely restore 15 HP.

---

# 6. TITLES

A title answers:

&gt; What would people call this person because of something they actually did?

Good title sources:
```
observed accomplishment
persistent behavior
social reputation
rare discovery
survived event
relationship
profession
world consequence
```

Examples:
```vbnet
Friend of the Unburied
Keeper of Strays
The Roadless
Shield of Greyfen
The Last to Leave
Breaker of the Hollow Bell
```

Titles can have no mechanical effect when explicitly classified as reputation/presentation.

However, mechanically significant titles must reference a validated effect contract.

Never infer mechanics from the title string.

---

# 7. CLASSES

A class answers:

&gt; What has the player become?

Class names should represent an enduring playstyle identity rather than an isolated action.

Normal class shape:
```
Role
```

Examples:
```
Ranger
Alchemist
Warden
Duelist
```

Adaptive class:
```
behavioral modifier + role
unusual compound
world-linked role
```

Examples:
```
Wayward Warden
Grave Shepherd
Ash Ranger
Rootless Knight
Beastwarden
```

A class should generally require broader and more persistent evidence than a skill.

---

# 8. CLASS EVOLUTIONS

Evolution names should preserve semantic ancestry.

Bad:
```
Warden
→ Void Blood Dragon Emperor
```

unless gameplay has genuinely undergone that transformation.

Good:
```
Warden
→ Road Warden
→ Warden of the Far Paths
```

or:
```
Beast Tender
→ Beastkeeper
→ Keeper of the Unclaimed
```

The player should be able to perceive how the identity evolved.

Store:
```
parent_identity_id
evolution_reason
behavioral_delta
mechanical_delta
```

Do not create an unrelated class merely because its name sounds stronger.

---

# 9. UNIQUE CLASSES

Unique classes should usually have:
```diff
high behavioral specificity
+
meaningful mechanical distinction
+
world-context justification
+
persistent consequences
```

Their names should often be less generic than:
```
Legendary Swordsman
Ultimate Mage
Supreme Assassin
```

Prefer identities such as:
```
Grave Mercy
Roadless Knight
The Unsworn
Keeper of Hollow Places
Ashbound Pilgrim
```

Only use this naming intensity when deterministic rarity validation permits it.

---

# 10. ITEMS

Item naming should separate:
```
ordinary equipment
named equipment
rare equipment
artifact
```

### Ordinary
```
Iron Longsword
Ashwood Bow
Padded Travelling Coat
```

### Crafted / personalized

May include maker, material, function or player history.
```python
Greyfen Hunter's Bow
Vey's Field Knife
Split-Oak Buckler
```

### Named rare item
```
Hollow Lantern
Wayfarer's Nail
Lastlight
```

### Artifact

Can use historical identity:
```
Bell of the Forgotten Crossing
Crown of the Third Warden
The Unopened Hand
```

Do not give random vendor trash legendary proper names.

---

# 11. QUESTS

Quest names should primarily communicate **situation**, not reward rarity.

Ordinary:
```vbnet
Missing Supplies
Tracks by the River
A Merchant's Request
```

Character-driven:
```css
What Vey Refuses to Say
A Debt in Greyfen
The Hunter Who Returned Alone
```

Rare opportunity:
```sql
Where the Wolves Will Not Hunt
The Door Beneath the Roots
A Bell With No Tower
```

Unique Opportunity:
```sql
The Woods Are Watching
The Shrine That Refused You
When the Dead Answered Kindly
The Road That Wasn't There
```

Mystery is permitted.

Meaningless vagueness is not.

---

# 12. UNIQUE OPPORTUNITY NAMES

Unique Opportunities should sound different from normal quests.

They should often describe:
```css
an impossible observation;
a contradiction;
an unresolved event;
a world reaction;
or a very specific consequence of player behavior.
```

Useful structural patterns:
```vbnet
The [Thing] That [Impossible/Unexpected Action]
When [Unexpected Event]
Where [Normal Rule] Does Not Apply
[Existing Entity]'s [Unexpected Response]
The [Place/Object] With [Contradiction]
```

Example structures:
```sql
The Wolf That Would Not Bite
When the Shrine Answered
Where the Dead Still Wait
The Bell With No Tower
The Road Beneath the River
```

Never randomly manufacture contradictions purely to sound mysterious.

The supplied world state must support the setup.

---

# 13. LEGENDARY CHAINS

A multi-stage chain should develop a naming family.

For example:
```
The Woods Are Watching
        ↓
Trial of the Rootless
        ↓
Keeper of the Unclaimed
        ↓
The Forest Remembers
```

The names become increasingly specific because the player's identity is becoming established.

Do not use:
```
Quest I
Quest II
Quest III
```

and do not generate four unrelated poetic titles.

Store a:
```
naming_lineage_id
```

so later content can preserve motifs and terminology.

---

# 14. MOTIF MEMORY

Accepted adaptive identities may establish motifs.

Example:
```diff
player_identity_motifs:
- ash
- mercy
- graves
```

Future generation may reuse or transform those motifs selectively:
```
Grave Mercy
Ashen Reprieve
Keeper of Quiet Graves
```

Do NOT repeat the same word in everything:
```
Grave Sword
Grave Armor
Grave Quest
Grave Potion
Grave Merchant
Grave Boots
```

Track motif saturation.

The model should preserve thematic identity without becoming repetitive.

---

# 15. CULTURAL NAMING

Names tied to a region, culture, faction or species must consume supplied cultural naming context.

The model may receive:
```
phonetic tendencies
word-length tendencies
common roots
titles/honorific rules
allowed compounds
religious vocabulary
taboo vocabulary
historical examples
```

It should generate within those patterns without copying an existing proper name.

Never infer real-world ethnicity from fantasy naming.

Never generate culturally important names without the relevant supplied context.

---

# 16. CHARACTER / NPC NAMES

NPC naming should use a separate generator/task from adaptive skill naming.

Recommended structured input:
```bash
species_id
people_id
culture_id
region_id
social_role
age_band
gender/pronoun context if relevant
local naming examples
forbidden existing names
```

Output:
```
given_name
family_name / clan_name when culturally applicable
honorific if applicable
pronunciation_hint if needed
```

Avoid randomly combining apostrophes, hyphens and exotic consonants merely to signal fantasy.

---

# 17. PLACE NAMES

Distinguish how places acquire names.
```
descriptive settlement
founder/person-derived
geographic feature
historic event
religious site
administrative name
folk nickname
```

Examples of structure:
```rust
Greyfen
Three Bridges
Northwatch
Vey's Crossing
Redwater
Saint Orra's Rest
```

Names should correspond to actual geography/history where provided.

The LLM cannot invent a “Three Rivers” settlement when the canonical world has no relevant rivers.

---

# 18. FACTION NAMES

Faction naming schema:
```javascript
function
+
cultural context
+
self-image
+
organizational type
```

Organizational forms might include:
```css
Order
Company
Circle
Guild
House
Council
Watch
Brotherhood
Conclave
Covenant
League
```

Use them only where culturally appropriate.

Avoid procedural combinations such as:
```
Shadow Dragon Blood Guild
```

unless all components have actual meaning.

---

# 19. ANTI-GENERIC FILTER

Track overused fantasy tokens.

Examples of words requiring stronger justification at higher frequencies:
```
eternal
infinite
divine
supreme
legendary
ultimate
shadow
void
chaos
blood
dragon
demon
god
celestial
abyss
fate
destiny
chosen
immortal
emperor
king
lord
```

These words are not banned.

They carry a **semantic cost**.

The model should use them when supported, not because they are easy fantasy vocabulary.

An ordinary poison dagger should not become:
```
Dagger of the Eternal Abyssal Blood God
```

---

# 20. LENGTH BUDGETS

Prefer approximate naming lengths:
```yaml
Ability:
1–4 words

Skill:
1–5 words

Title:
1–7 words

Class:
1–4 words

Item:
1–7 words

Quest:
2–9 words

Unique Opportunity:
2–10 words

Faction:
1–6 words

Settlement:
1–4 words
```

These are soft constraints.

Names must still fit UI.

---

# 21. DUPLICATE DETECTION

Before accepting a generated name, compare against:
```r
exact existing names
normalized names
token overlap
phonetic similarity where available
same-category names
important cross-category names
recent generated identities
```

Reject or regenerate:
```
Ash Warden
Ashen Warden
Warden of Ash
```

when they refer to unrelated identities.

Related evolution lineages are exempt when intentional.

---

# 22. SEMANTIC DUPLICATE DETECTION

Do not only compare strings.

These may also be effectively duplicates:
```vbnet
Beast Friend
Animal Ally
Friend of Beasts
Creature Companion
```

If they represent the same gameplay identity and are unrelated records, flag them.

The model should be given existing semantic identities before naming new major content.

---

# 23. PERSISTENCE

Once accepted:
```
generated_name
generated_identity
motifs
lineage
associated evidence
canonical references
```

become persistent content.

Do not silently rename them because:

- the model changed;
- the adapter changed;
- prompts changed;
- the naming algorithm improved;
- the player reloaded.

Renaming accepted content requires an explicit versioned migration or an intentional in-world evolution.

---

# 24. NAME + MECHANIC CONSISTENCY

Every major generated name must pass a semantic consistency check.

Examples:
```
"Blinkstep"
```

cannot describe a completely ordinary walking-speed buff unless the word "blink" has an established different meaning.
```
"Beastwarden"
```

should not represent increased merchant discounts.
```
"Grave Mercy"
```

should have some meaningful relationship to mercy, death, undead, funerary practice or relevant established context.

Reject names that advertise mechanics/lore the actual content does not provide.

---

# 25. THREE-STAGE NAME GENERATION

For important content, do not directly request one final name.

Generate internally:

### Stage A — Semantic kernel
```
protective
beast affinity
social trust
nonlethal
wilderness
```

### Stage B — Candidate families
```
Keeper
Warden
Friend
Shepherd
Kin
```

### Stage C — Final candidates
```
Keeper of Strays
Wildwarden
Unclaimed Shepherd
```

Then rank using deterministic/basic model-visible criteria:
```
evidence fit
world fit
mechanical fit
rarity fit
distinctiveness
UI clarity
existing-name collision
motif saturation
```

Persist only the accepted final result.

---

# 26. GENERATED NAME RECORD

For important adaptive content, store metadata equivalent to:
```json
{
  "name": "Keeper of the Unclaimed",
  "content_type": "class_evolution",
  "rarity": "unique",

  "semantic_kernel": [
    "animal_protection",
    "nonlethal_resolution",
    "unowned_creatures",
    "persistent_behavior"
  ],

  "motifs": [
    "keeper",
    "unclaimed"
  ],

  "behavior_evidence_ids": [
    "evt_...",
    "pattern_..."
  ],

  "world_context_ids": [
    "region_...",
    "npc_..."
  ],

  "mechanical_template_id": "beast_pacification_v1",

  "lineage_id": "identity_beast_protector_01",

  "generation_version": "...",
  "model_version": "...",
  "adapter_version": "...",

  "accepted": true
}
```

The semantic metadata matters as much as the string.

Future generation should understand what the name means.

---

# 27. TRAINING EXAMPLES

The LoRA dataset must contain naming examples where the same mechanic receives different identities because player history differs.

Example:

Two characters unlock the same supported interception mechanic.

Player A:
```
repeatedly protects travelers on roads
```

Possible identity:
```
Road Warden
```

Player B:
```
protects settlement children during attacks
```

Possible identity:
```
Hearthguard
```

Player C:
```
repeatedly intercepts attacks against one companion
```

Possible identity:
```
Oathbound Guard
```

The **mechanical template can be shared while generated identity remains personal**.

This is essential.

---

# 28. NAMING EVALUATION

Evaluate naming separately from general output quality.

Measure:
```
behavioral relevance
mechanical relevance
world relevance
rarity calibration
distinctiveness
memorability
brevity
pronounceability where applicable
semantic consistency
duplicate rate
motif repetition
generic-fantasy-token frequency
unsupported implication rate
```

Human review should answer:

&gt; Could this name plausibly have been authored specifically for this player's history?

That is the key standard.

---

# 29. FAILURE EXAMPLES

Reject outputs such as:
```
Legendary Fire Master
Supreme Sword God
The Ultimate Chosen One
Shadow Void Dragon Emperor
Infinite Eternal Death Knight
```

when they arise merely because the model was asked for something cool.

Also reject excessively random generated names such as:
```rust
Xy'zarath-Khael
V'raxxion
Qytherael
```

unless the supplied culture genuinely supports that phonology.

Reject names whose only distinction is decorative adjective stacking.

---

# 30. TARGET FEEL

The naming system should make players believe:

&gt; “That name describes what I became.”

Not:

&gt; “An AI assembled fantasy words.”

The strongest adaptive identities should become personally meaningful enough that the player remembers:

- what they did to earn them;
- where they got them;
- who witnessed them;
- what changed afterward.

That relationship between **behavior and identity** is more important than linguistic extravagance.

---

# 31. FINAL ACCEPTANCE

The naming system passes when held-out behavior histories consistently produce:

- distinctive names;
- appropriate naming intensity;
- low duplicate rates;
- low generic-fantasy phrasing;
- strong correspondence with behavioral evidence;
- strong correspondence with actual mechanics;
- world-compatible terminology;
- persistent thematic continuity;
- useful class/title evolution lineages;
- materially different identities for materially different players.

The system fails if naming becomes interchangeable procedural fantasy text.

A generated name should feel like a compressed history of how the player played.
