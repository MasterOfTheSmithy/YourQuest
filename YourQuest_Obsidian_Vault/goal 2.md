# /goal — Build the production character creator and unify player origin

Create a complete player-facing character creation flow suitable for beta and integrate it with the existing Goddess/origin/world-generation pipeline.

The result should feel closer to a traditional Bethesda-style RPG character creator than a questionnaire screen.

The player should first create **who they are**, then optionally define **where they came from**.

The existing Goddess sequence remains the default origin-authoring experience when the player does not provide a custom origin.

## Work model

Use **Luna High** as the primary work chat.

Use Astra Light only for orchestration and small mechanical tasks totaling no more than roughly 5–10% of the hourly goal.

Use Luna High subagents for contained implementation/review tasks.

Escalate to Luna Max/Extra High for difficult but bounded coding or integration work.

Use Sol High only when deeper architectural reasoning is necessary, particularly for save-schema migration, origin/world-generation authority, or difficult character-appearance persistence.

## Character creation flow

Create a polished creation sequence with clear stages.

Suggested production flow:

**Identity  
→ Species / ancestry  
→ Appearance  
→ Character details  
→ Origin  
→ Review  
→ Begin**

The exact UI arrangement may vary if a cleaner interaction is found.

## Species selection

Allow the player to choose from the species currently supported by the generated world's canonical content.

Species must be structured data, not merely cosmetic labels.

A player species record should be capable of providing:

- stable species ID
    
- display name
    
- description
    
- appearance constraints/options
    
- any legitimate baseline gameplay traits
    
- dialogue/world identity hooks
    
- reputation hooks
    
- generated-world compatibility information
    

Do not invent permanent species-specific mechanical bonuses without confirming they fit the existing game design.

Keep presentation extensible so generated or future species can be introduced without rewriting the creator.

## Appearance editing

Create an appearance editor appropriate to the character assets currently available.

Where the asset pipeline supports them, expose controls such as:

- body/presentation preset
    
- face/head variant
    
- skin tone
    
- hair style
    
- hair color
    
- eye color
    
- facial hair
    
- scars/markings
    
- cosmetic features
    
- body proportions or other safe supported morphs
    

Do not expose sliders that the underlying models cannot actually represent reliably.

Prefer a smaller set of polished working options over fake customization controls.

The character visible in creation must match the character spawned in gameplay.

Appearance must persist across save/reload.

## Identity details

Support appropriate basic details such as:

- character name
    
- species
    
- race/people where relevant
    
- pronouns or equivalent dialogue-reference data if the dialogue system can consume them safely
    

Do not hardwire real-world identity assumptions into generated fantasy species.

## Origin section

Add a curated **Origin** section to character creation.

The default path should clearly explain that the Goddess can establish the player's beginnings through the existing interactive origin sequence.

Also provide an optional **Write My Own Origin** path.

This should allow the player to provide structured free-text information about their beginnings.

Use a small number of well-presented fields rather than a giant blank prompt.

Suitable fields could include:

- upbringing / place of origin
    
- family or formative relationships
    
- former occupation or way of life
    
- important past event
    
- personal motivation
    
- optional additional background
    

The exact fields should match what the existing canonical origin model can actually use.

Do not blindly store arbitrary prose as authoritative gameplay truth.

Normalize player-written material into the same structured canonical origin data used by the Goddess sequence.

Preserve the player's original text separately for later narrative context if appropriate.

Never silently invent missing background facts in order to fill a schema.

## Custom-origin confirmation

When the player chooses to provide enough custom origin information to replace the Goddess questionnaire, explicitly warn them before continuing.

The confirmation should communicate, in natural player-facing language, that:

- their written background will become the foundation of this character's history
    
- the Goddess origin questions will therefore be skipped
    
- they can return and use the guided Goddess path instead
    

Provide:

**Use My Origin**

and

**Return to Guided Origin**

or equivalent clear actions.

Do not phrase this as an error or technical limitation.

## Goddess integration

If the player chooses the guided path:

- complete normal character creation first
    
- then enter the Goddess origin sequence
    
- write the resulting structured origin into the canonical player-origin state
    

If the player chooses the custom-origin path and confirms it:

- do not run the Goddess questionnaire
    
- normalize the custom origin
    
- commit it through the same canonical origin authority
    
- continue into world generation
    

Both paths must ultimately produce the same downstream origin contract.

No world-generation subsystem should need separate logic for “questionnaire character” versus “custom-background character.”

## World-generation integration

Ensure character identity/origin can safely influence the existing generated-world pipeline where intended.

The world generator may use canonical origin context.

It must not treat raw arbitrary player prose as executable instructions.

Sanitize and delimit free-text fields before incorporating them into LLM context.

The deterministic fallback world must remain valid even if custom-origin text is empty, malformed, extremely long, or unavailable.

## Save/load behavior

Character creation is one-time for a normal new save unless a future supported respec/change system explicitly allows otherwise.

Persist:

- species
    
- race/people identity
    
- appearance configuration
    
- basic identity details
    
- structured canonical origin
    
- original custom-origin text where appropriate
    
- whether the origin came from the guided Goddess process or player-authored background
    

Existing saves must receive safe migration defaults.

## Acceptance testing

Verify at least:

- guided Goddess origin path
    
- custom written-origin path
    
- cancellation from custom-origin confirmation
    
- species changes update appearance options correctly
    
- created appearance matches gameplay appearance
    
- save/reload preserves appearance
    
- save/reload preserves identity
    
- both origin paths generate valid worlds
    
- malicious/garbled/extremely long text cannot break prompt contracts
    
- missing optional origin fields do not cause the game to fabricate facts
    

## Completion gate

This goal is complete when a new tester can create a recognizable character, define their identity, either use the guided Goddess origin or author their own background, confirm the choice, enter the generated world, save, reload, and see the same character and canonical origin intact.

The result should feel like the beginning of an RPG, not a developer configuration screen.