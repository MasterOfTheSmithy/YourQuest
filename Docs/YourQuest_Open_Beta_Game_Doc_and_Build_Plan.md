# YourQuest: Open Beta Game Document and Build Plan

**Version:** 1.0  
**Date:** August 21, 2026  
**Audience:** Project owner, Unity engineers, content/system designers, and Work GPT agents  
**Status:** Delivery specification from early playable prototype to polished open beta

## 0. How to Use This Document

This is the working authority for the open-beta path. It turns the high-level YourQuest vision into a deliberately bounded product and implementation plan. Work GPT should use it as an execution contract: first identify the active milestone, then implement only the acceptance criteria and dependencies assigned to that milestone.

### Operating rules for Work GPT

1. Preserve the existing game design document's central contract: an LLM creates structured content; the game validates, normalizes, persists, and executes it deterministically.
2. Do not add a player-facing feature merely because it is a familiar RPG feature. Add it only when it belongs to the open-beta scope below or is required by an already-approved feature.
3. Do not make the LLM a real-time runtime authority. No freeform LLM decision may directly move a character, damage a target, grant an item, select an arbitrary asset, or complete a quest.
4. Treat the save as canon. Accepted generated records are never casually regenerated on load.
5. Ship vertical, playable slices. A subsystem is not complete until it works in play mode, survives save/load, handles model failure, and is visible to the player.
6. Prefer a small coherent extension to a replacement or parallel system. Preserve Unity serialized fields, prefab references, material bindings, and the one-authoritative-player rule.
7. Keep player-facing names, lore, quests, items, skills, NPC identities, and titles generated and persisted. Code may contain schemas, curated mechanical templates, semantic asset keys, validators, and deterministic fallbacks.
8. Use the milestone exit criteria as the definition of done. Do not advance a milestone because code compiles alone.

### Product truth in one sentence

YourQuest is a single-player fantasy action RPG where the player's repeated choices and play style become persistently generated identity, capabilities, relationships, quests, and world canon - while moment-to-moment gameplay remains fast, readable, and deterministic.

## 1. Product Thesis and Beta Promise

### The fantasy

The player begins unknown. The world watches what they actually do: who they spare, how they fight, what they trade, where they linger, what they harvest, which risks they take, and which compulsions they repeat. The game then responds with consequences that feel authored for that player: a title, a hidden craft, a rival's memory, a rumor, a quest, a skill evolution, a changed price, or a region that has learned their name.

The goal is not infinite random text. The goal is a convincing RPG world that recognizes evidence and turns it into durable gameplay.

### Open-beta promise

Open beta must deliver a polished **one-region, many-path** RPG experience rather than a technically large but shallow world. A new player can create an origin, learn the controls, explore a dense starter territory, fight, loot, trade, converse, discover behavior-driven unlocks, receive generated quests and progression offers, and return later to find their save and world memory intact.

Open beta is Skyrim-style in player freedom, world response, exploration density, and role expression. It is not Skyrim-scale in landmass, factions, quest count, or asset breadth. The beta earns expansion by proving that one procedural territory remains coherent and fun across many player identities.

### Open-beta target session

Within the first 60 to 90 minutes, a player should be able to:

- Complete the generated origin and first tutorial quest.
- Choose combat, social, exploration, trade, or gathering as a meaningful first direction.
- Discover at least one latent capability through behavior rather than a static skill tree.
- Meet memorable generated adults with persistent memory.
- Complete two or more generated objective-record quests.
- Fight a readable enemy family and a mini-boss/elite encounter.
- Use inventory, equipment, loot, a shrine, a chest/mimic, and a merchant.
- Save, reload, and see accepted content, world state, and relationships persist.
- Receive a reason to return: an unresolved consequence, a new offer, or a region change connected to their actions.

## 2. Current Stage, Constraints, and Product Decisions

### Confirmed current stage

YourQuest is an **early playable prototype moving toward a reliable vertical slice**. The immediate project focus is the responsive-world golden loop: act, receive a visible response, accept or decline a bounded offer, and retain that outcome across save/load. The existing game design document and vertical-slice requirements are the source for this assessment.

### Known technical risk areas

- Avatar/equipment alignment, first-/third-person presentation, animation state quality, and the duplicate-player risk.
- URP materials, shader compatibility, red/black placeholders, and texture-property errors.
- Interaction bounds and input ownership; left click must remain attack-only.
- Enemy grounding, readable movement modes, combat feedback, and audio noise.
- Generated-content validation, deduplication, persistence, and safe fallback behavior.
- Prototype tutorial clutter and weak onboarding polish.

### Product decisions that prevent feature bloat

**D1 - One starter territory, not a continent.** Open beta has one polished hub territory with a tutorial interior, one outdoor exploration zone, one dungeon/ruin, and controlled exits that can tease future regions. It may use generated location records, but it does not need seamless unlimited terrain.

**D2 - One authoritative avatar.** First-person and third-person are camera/presentation modes of the same player state, collider, inventory, combat receiver, and equipment ownership. No parallel controller or ghost model is permitted.

**D3 - Curated mechanics, generated expression.** The beta supports a finite library of reliable verbs and effect templates. The LLM generates player-specific identity, framing, combinations, rewards, and upgrades inside those contracts.

**D4 - The LLM works at meaningful beats.** LLM calls occur on new-save generation, dialogue turns/summaries, completed action batches, deliberate requests, milestone thresholds, and world-change beats. They never occur per frame, per attack, or simply because a menu opened.

**D5 - Natural unlocks are few but deep.** The beta launches a small number of behavior-to-capability paths that can combine. It does not attempt to simulate every profession, crime, romance dynamic, or political system on day one.

**D6 - Adult relationships are player-controlled and consent-safe.** Romance/seduction content applies only to adult, relationship-eligible NPCs with explicit compatibility, autonomy, boundaries, and rejection states. It must not be tied to a real-world gender requirement, coercion, child characters, or non-consensual control. Its mechanics are social outcomes, trust, access, or temporary distraction - never ownership of an NPC.

## 3. Experience Pillars and Non-Negotiables

### P1. Evidence becomes identity

The player earns identity through repeated meaningful behavior. A player who attacks trees, gathers fallen branches, and returns with timber may be offered a woodcraft path. A player who protects people and avoids killing may be offered a guardian path. A player who repeatedly waits in danger may become associated with stillness. The system should explain the evidence in fiction, but the underlying evidence is typed events and counters.

### P2. Generated content is consequential and legible

Every accepted generated record has a readable player-facing description and an explicit mechanical or world-state effect. Pure flavor is welcome only when it attaches to a concrete entity, relationship, memory, place, or consequence.

### P3. Action remains responsive without a model

Movement, melee, dodge, damage, looting, interaction, crafting actions, merchant transactions, objectives, and cooldowns are local deterministic systems. If generation is slow or unavailable, the player keeps playing and sees an honest pending/fallback status rather than a frozen game.

### P4. The world remembers without becoming incoherent

Every accepted fact joins the save's canon ledger. New dialogue, quests, regions, and offers read the ledger through a bounded context pack and cannot overwrite incompatible facts without an explicit, persisted retcon event approved by game logic.

### P5. A small amount of strange behavior can become mythic

The game takes playful repetition seriously. An odd action starts as an observation, becomes a tendency, then may become a recognized path if it remains sustained and mechanically meaningful. Not every action must create a new system; recognition needs confidence, novelty, and a supported effect family.

## 4. Open-Beta Scope

### In scope

#### Playable world

- Archivist Vey's finished tutorial study/hut as the controlled new-save start.
- A dense starter territory: safe hub, wilderness route, one hostile landmark/dungeon, one merchant/service point, one shrine, one gathering area, and one future-region gate.
- One coherent art direction for the slice, using curated approved assets and URP-verified materials.
- Three supported location families for generated framing: settlement, wilds, and ruin/den.

#### Player verbs

- Move, jump, crouch/sneak, dodge/dash, attack, block/secondary ability where equipped, cast/use skill, interact, talk, loot, equip, trade, use shrine, open chest, lockpick, harvest, craft a simple recipe, and rest/save.
- First-person/third-person toggle with the same underlying player.
- Small, supported spell-combination system for two compatible active effects (for example, surface effect + elemental projectile), not arbitrary spell programming.

#### RPG systems

- Origin questionnaire and committed origin package.
- Inventory, equipment, basic stats/resources, combat damage/resistance/status templates, merchant pricing, currency, loot, and consumables.
- Objective-record quests, quest tracker, rewards, failure/abandon states where appropriate, and generated narrative framing.
- Generated titles, skills/spells, items, NPCs, dialogue summaries, rumor/world notes, and limited region/location records.
- Behavior tracking and 6 to 8 supported latent capability paths.
- Relationship state for Vey, a merchant, two generated adult NPC anchors, and generated one-off NPCs.

#### Reliability

- Versioned save/load with accepted generated records and canonical world facts.
- Deterministic fallback content that preserves playability when the LLM fails.
- Model/director status shown in a non-intrusive debug or player-facing status panel during beta.
- Basic analytics/logging for generation quality, failed validation, performance, progression offers, and player blockers.

### Explicitly deferred until after open beta

- Multiple full regions/continents, seamless procedural overworld generation, mounts, multiplayer, modding, PvP, settlement building, fully simulated economies, marriage/children, factions with full territory war, voiced LLM dialogue, procedural animation generation, and unrestricted crafting.
- Every possible profession. Beta proves lumber/foraging, trade, stealth, social, combat, magic, and alchemy-adjacent experimentation as representative paths.
- Arbitrary player-written spells, arbitrary LLM-authored code, arbitrary Unity asset selection, or arbitrary behavior-tree generation.
- A promise that every unusual action unlocks something. The promise is that sustained, supported, meaningful behavior can be recognized.

## 5. The Open-Beta Golden Loop

1. **Observe and act.** The player explores, fights, talks, gathers, trades, rests, sneaks, or experiments.
2. **Record evidence.** The event recorder writes typed events; behavior rollups update bounded counters and recent-context summaries.
3. **Respond immediately.** Deterministic runtime systems resolve combat, loot, quest counters, relations, inventory, prices, and small world-state changes.
4. **Detect a meaningful beat.** The director checks cooldowns, novelty, confidence, and open hooks. It may schedule an LLM request or choose a deterministic response.
5. **Generate safely.** The model returns strict JSON constrained to the requested schema and supported mechanics.
6. **Validate and curate.** Schema, safety, canon, novelty, UI, supported-enum, asset-binding, and objective feasibility checks run before acceptance.
7. **Commit canon.** The normalized record, source metadata, raw payload, and outcomes are saved transactionally.
8. **Surface choice or consequence.** The player receives a quest, world note, dialogue response, rumor, item, relationship shift, or offer. Major identity changes are normally accept/decline/incubate choices.
9. **Persist and evolve.** The choice and follow-up evidence shape future context without replacing existing committed facts.

### Director scheduling rules

The director must obey all of these conditions before issuing a non-dialogue LLM generation request:

- No active request of the same family for the same target/context.
- A per-family cooldown has elapsed.
- The event batch meets a meaningful confidence threshold.
- The result can use a supported content schema and a currently available asset/mechanics binding.
- The context pack has a bounded token budget and contains the relevant canon, not raw unfiltered history.
- There is room under the active-content cap; otherwise the system updates an existing record or defers.

## 6. Core Runtime Architecture

### Authority boundaries

| System | Owns | Must not own |
|---|---|---|
| Player State | Stats, resources, inventory references, equipped ids, learned capability ids, current position/state | Narrative generation or duplicate avatar state |
| Event Recorder | Typed event append, event metadata, dedupe keys | Gameplay interpretation in prose |
| Behavior Rollup | Counters, trends, evidence windows, confidence scores | Final player-facing content |
| Director | Scheduling, context assembly, content-family routing, pending jobs | Direct damage, input, animation, asset paths |
| LLM Client | Request/response transport, retries, raw payload capture | Trusting or executing returned content |
| Validator/Normalizer | Schema parsing, enum checks, clamping, feasibility, repair/reject reason | Saving unapproved raw output as canon |
| Canon and Save Store | Versioned records, facts, relationships, pending/accepted states | Reconstructing content from a fresh model call |
| Quest System | Objective records, event matching, state transitions, rewards | Quest completion from narrative text |
| Ability Executor | Supported mechanical templates, costs, cooldowns, targets, animation/VFX/SFX intents | Interpreting a skill's display name as logic |
| Asset Binder | Semantic-to-approved prefab/material/icon/VFX/SFX mapping | Accepting arbitrary asset paths from generated output |
| UI Presenter | Safe display of accepted content and pending/error state | Mutating canonical records |

### Content pipeline

```text
typed player event -> event ledger -> behavior rollup -> director decision
    -> bounded context pack -> strict JSON request -> parser
    -> validator -> normalizer -> canon/duplicate/safety curator
    -> transactional save commit -> runtime binding -> UI presentation
```

### Required implementation pattern

Every generated content family must have:

1. A versioned request and response schema.
2. A schema parser with a precise reject reason.
3. A deterministic normalizer that clamps values and converts semantic fields to stable ids/enums.
4. A canon/duplicate check.
5. A gameplay feasibility check.
6. A deterministic fallback record for unavailable/invalid output.
7. A save record that retains source, request/prompt hash, seed, raw payload, normalized payload, status, and creation time.
8. A safe UI presentation path.
9. Play-mode verification and save/load verification.

## 7. Data Contracts

### 7.1 Stable identifiers and source metadata

Use stable IDs for all runtime targets and records. Player-facing names are presentation fields, never identifiers. Every generated entity stores:

```json
{
  "id": "stable-guid-or-deterministic-id",
  "schemaVersion": 1,
  "generationSource": "llm|fallback|system",
  "generationId": "request-id",
  "seed": 0,
  "promptHash": "hash",
  "rawPayloadJson": "original-json",
  "normalizedPayloadJson": "accepted-json",
  "createdUtc": "ISO-8601",
  "status": "pending|accepted|rejected|superseded"
}
```

### 7.2 Typed event record

```json
{
  "eventId": "evt_...",
  "eventType": "harvest_attempt",
  "actorId": "player_01",
  "targetId": "resource_tree_oak_12",
  "regionId": "region_starter_01",
  "locationId": "location_wilds_01",
  "tags": ["wood", "tool", "outdoors"],
  "numeric": {"amount": 1, "damage": 8},
  "timestampGame": 342.1,
  "dedupeKey": "harvest_attempt:resource_tree_oak_12:window_17"
}
```

Event types are code-owned enums. They include movement/travel, combat, damage, dodge, stealth, dialogue intent, trade, buy, sell, harvest attempt/success, craft, lockpick, loot, shrine use, rest, spell use, skill use, romance/social intent, quest state, region discovery, and death/recovery.

### 7.3 Behavior signal and latent capability contract

```json
{
  "signalId": "signal_woodcraft_01",
  "family": "woodcraft",
  "evidence": [
    {"eventType": "harvest_attempt", "count": 12, "windowHours": 3},
    {"eventType": "tool_attack_tree", "count": 18, "windowHours": 3},
    {"eventType": "wood_looted", "count": 8, "windowHours": 3}
  ],
  "confidence": 0.82,
  "novelty": 0.65,
  "state": "observed|eligible|offered|accepted|declined|incubating",
  "supportedOfferKeys": ["unlock_woodcutter", "upgrade_woodcutter"],
  "lastEvaluatedUtc": "ISO-8601"
}
```

The LLM may name and frame an eligible offer, but it cannot invent the unlock family or change the mechanical evidence thresholds. The capability registry owns supported keys, prerequisites, effect templates, and counter signals.

### 7.4 Generated ability contract

```json
{
  "abilityId": "ability_...",
  "familyId": "woodcraft|guard|shadow|arcane|trade|stillness|...",
  "displayName": "generated player-facing name",
  "kind": "skill|spell",
  "mechanicKey": "curated runtime template key",
  "targetingMode": "self|melee|projectile|pulse|ground_target|cone|buff|debuff|trap",
  "resourceType": "stamina|mana|none",
  "resourceCost": 12,
  "cooldownSeconds": 4,
  "range": 0,
  "areaRadius": 0,
  "statusEffectKeys": ["supported_status_key"],
  "animationIntent": "melee|cast|guard|dash|channel|emote",
  "vfxFamily": "physical|fire|frost|storm|poison|heal|shield|shadow|earth|air|arcane",
  "sfxFamily": "physical|magic|utility",
  "stimulusSummary": "why the player earned this",
  "upgradeRuleKeys": ["supported_upgrade_key"]
}
```

### 7.5 Quest contract

Quests contain an id, a player-facing name/description, tags, source/relevance evidence, state, reward records, consequences, and an ordered or unordered list of objective records. Objective completion is determined only by `objectiveType`, target IDs/tags, counter fields, and event matches; never by parsing descriptive prose.

Supported beta objective types: talk_to_npc, equip_item, origin_manifested, enter_region, discover_location, defeat_enemy, deal_damage, avoid_damage, use_skill, cast_spell, loot_item, harvest_resource, craft_item, buy_item, sell_item, open_lock, mimic_reveal, use_shrine, read_lore, wait_seconds, choose_dialogue_intent, protect_npc, and return_to_npc.

### 7.6 Canon fact contract

Facts are small, sourceable claims rather than a monolithic lore blob. A fact has `factId`, `subjectId`, `predicate`, `objectId/value`, `scope`, `confidence`, `sourceRecordId`, `createdUtc`, `supersedesFactId`, and `visibility`. Generation context includes only facts relevant to the requested subject, place, active quest, and player identity.

## 8. Natural Capability Discovery System

### Design model

Every latent capability is a three-layer system:

1. **Mechanical family (code-owned):** supported verbs, prerequisites, cooldown/resource rules, templates, assets, and effect limits.
2. **Evidence (runtime-owned):** typed events and confidence thresholds that demonstrate a player is naturally attempting or practicing the family.
3. **Expression (generated):** player-specific name, lore meaning, NPC reactions, quest framing, and choices that fit persisted canon.

This prevents both extremes: a static skill tree that ignores play, and an LLM that invents unshippable mechanics.

### Confidence gates

A candidate becomes eligible only if it passes all gates:

- **Frequency:** enough relevant events within the defined rolling window.
- **Intent:** the player attempted actions associated with the family, not accidental incidental contact.
- **Diversity:** at least two evidence types where appropriate (for example, chopping attempts plus wood collection).
- **Persistence:** behavior occurs across more than one encounter/time segment when the path is major.
- **Novelty:** the player has not recently received the same or overlapping offer.
- **Feasibility:** a supported mechanic, objective, and asset binding exist.
- **Context:** the offer does not conflict with active quests, canon, player state, or accessibility settings.

### Beta latent paths

| Family | Core evidence | First unlock | Example evolution direction |
|---|---|---|---|
| Woodcraft | Repeated tree/tool strikes, gathering wood, carrying timber | Woodcutter: can properly harvest approved trees and collect timber | Field carpentry, living-wood utility, terrain cover |
| Trade | Buy/sell repetition, price comparison, item inspection, currency saving | Appraiser: value cues and better contract/trade options | Bargain, debt sight, merchant network |
| Guard/Mercy | Interposing, protecting NPCs, avoiding lethal finishes, healing allies | Protector: guard pulse or rescue interaction | Sanctuary, retaliation guard, reputation trust |
| Stealth | Crouch travel, unseen takedowns, avoiding detection, escape | Shadowstep: short supported reposition or detection reduction | Ambush, concealment, decoy |
| Mobility | Dodge timing, jumps, vertical exploration, fall recovery | Surefoot: improved dodge window or landing recovery | Wall vault, dash enhancement |
| Stillness | Sustained intentional waiting in risk/exploration contexts, successful patience outcomes | Stillmind: focus/awareness or defensive stillness | Time resistance, immovable guard |
| Arcane experimentation | Distinct spell use, status pair experimentation, shrine/lore study | Resonance: one supported spell-combination modifier | Elemental conversion, controlled area effect |
| Social intimacy | Respectful recurring adult conversations, trust milestones, compatible choices, relationship consent | Rapport: relationship-specific social option | Seduction/distraction only where adult-compatible, consensual, and permitted |

### Example: woodcraft unlock

The player repeatedly attacks a valid tree while equipped with a plausible tool, gathers fallen wood, and uses/keeps that material. The runtime first presents unobtrusive feedback: the tree shows chips, the player notes the work, and a behavior signal reaches eligibility. The director can generate a title/offer such as a local woodcraft recognition, but the actual unlock uses `unlock_woodcutter`. On acceptance, the gathering system enables approved tree harvest states, adds timber loot entries, and makes future woodcraft events meaningful. No generated text determines whether any arbitrary tree can be destroyed.

### Example: romance and seduction

Romance is relationship-specific rather than a universal minigame. Each adult NPC has compatibility, availability, boundaries, trust, attraction/rapport (if appropriate to the game setting), consent, and consequence fields. Repeated respectful conversations, shared quests, positive choices, and verified compatibility can unlock a romance route. A later social ability may be framed as seduction, charm, distraction, or allure, but it maps to a bounded target filter, chance/contest rules, cooldown, failure response, and consent-safe content policy. It must not bypass essential NPC agency, turn hostile enemies into permanent followers, or override a player's accessibility/content settings.

### Offer states and player control

Major capability offers use `eligible -> offered -> accepted | declined | incubating`. Declining suppresses that exact offer for a long cooldown, not the player's entire path. Incubating lets the player defer a decision and continue gathering evidence. Automatic recognition is reserved for factual records such as "survived X" or "known by Y," and must not impose a build-defining active ability without a player choice.

## 9. Gameplay Systems for Open Beta

### Combat and movement

Combat is readable third-/first-person fantasy action built on a single authority. The beta supports melee attack, secondary/equipped ability, dodge/dash, jump, crouch/sneak, blocking/guard where equipped, damage, simple status effects, enemies, and recovery. Input contract: left click attacks only; right click triggers the equipped secondary ability; E interacts only; Q dodges/dashes; Ctrl crouches; Space jumps; T toggles camera.

The ability executor checks resource, cooldown, valid target, animation compatibility, and player state before spawning curated VFX/SFX and applying a template effect. Generated fields select semantic families, never clip names, arbitrary damage formulas, or asset paths.

### Spell combination

Open beta supports a deliberately small composition matrix. An active effect can carry tags such as wet, burning, chilled, shocked, rooted, warded, or marked. A second compatible supported effect may create a named generated reaction while executing a pre-authored result such as burst, slow, area shock, reveal, or resistance change. The combination registry is code-owned and caps chain depth at two effects in beta.

### Inventory, equipment, loot, and crafting

Items are generated as structured records built from curated template categories and asset bindings. Beta supports weapon, offhand, armor, consumable, material, key/quest, trinket, and trade-good categories. Equipment must bind to verified sockets on the one authoritative avatar. Crafting is limited to simple, discoverable recipes and material conversions; it establishes the pipeline for later generated recipe expression without building a full crafting simulator.

### Bartering

Merchant inventory, currency, base price, disposition, and supported price modifiers remain deterministic. Generated content supplies merchant identity, preference, rumor, item names, contract framing, and player-specific reactions. The Trade latent path may unlock appraisal, limited bargaining choices, or contract quests, but it never relies on an unvalidated model-calculated price.

### Dialogue, relationships, and lore

Dialogue uses a bounded context: NPC record, relationship summary, relevant canon facts, current intent, active quest facts, and a recent conversation summary. After a conversation, the game persists a compact summary and explicit facts/relationship deltas. It does not need to retain every raw transcript in prompt context. Essential quest decisions use explicit dialogue intent options and deterministic effects; freeform generation enriches delivery.

### Enemies and encounters

Open beta needs three grounded enemy archetypes plus one elite/mini-boss configuration: a melee pursuer, a ranged/caster threat, an ambusher or guarded creature, and a boss variation. Each family uses supported movement/AI profiles: grounded walker, crawler, jumper, stationary caster, or intentional burrower/floater. No enemy may slide/fly accidentally because its prefab lacks a walk animation.

## 10. World, Content, and Asset Direction

### Starter territory structure

The beta territory contains:

- **Vey's Study/Hut:** origin ceremony, first conversation, safe load point, UI/tutorial anchor.
- **Crossroads/Hamlet:** merchant, two social anchors, notices/rumors, trade and relationship opportunities.
- **Whisperwood route:** harvestable wood, stealth/mobility terrain, creatures, environmental storytelling.
- **Old estate/ruin:** lock, chest/mimic, combat encounter, lore object, elite encounter, and consequence hook.
- **Boundary gate:** a visible promise of future region generation without pretending the beta already contains it.

The names and lore are generated/persisted where player-facing. These labels describe production roles only.

### Generation hierarchy

1. Curated scene geometry, traversal, encounter volumes, interactable classes, and asset packs are authored/approved.
2. Generated records select semantic visual and audio profiles from approved palette keys.
3. The asset binder maps keys to validated prefabs/materials/icons/VFX/SFX.
4. Runtime instantiation uses only the bound result and logs failures visibly.

### Asset gates

An asset cannot enter the beta route until it has correct scale, URP materials, non-broken textures, appropriate collider behavior, controlled memory/performance cost, predictable foldering, and a clear gameplay purpose. Import and test one pack/intake batch at a time. The first polished visual target remains the current curated mansion/estate encounter space and its supported RPG equipment/chest/combat feedback assets.

## 11. UX, Accessibility, and Content Safety

### UX principles

- Generated copy is concise by default; long lore lives behind deliberate optional reads.
- Every quest view shows clear objectives, progress, next action, and reward/consequence where known.
- Every offer tells the player what behavior was recognized in natural language, what changes mechanically, and whether it is permanent, temporary, or experimental.
- Pending generation never blocks movement or combat. Use a subtle "the world is considering" state only where it adds fiction; show honest retry/fallback state in beta diagnostics.
- Names must obey UI length limits, wrapping rules, and safe fallback display names.

### Accessibility baseline

Provide remappable controls, subtitles, adjustable text size, high-contrast/readability options, camera sensitivity options, reduced motion/VFX option, color-independent enemy/interaction cues, difficulty assistance that does not invalidate progression evidence, and content settings for romance/sexual themes and gore. Generated content must honor these settings.

### Content and model safety

Validate and reject output that contains player-targeted harassment, real-world hate, sexual content involving minors, non-consensual sexual content, disallowed self-harm instructions, copyrighted character/world impersonation, model/meta claims, unsupported claims about player data, or content incompatible with age/content settings. Keep a clear fallback/retry path. The player can report a generated record in beta, storing its generation id and raw payload for review.

## 12. Quality Bars and Telemetry

### Open-beta quality bar

- A new save reaches the first meaningful decision without a blocking error.
- The same player state is represented by exactly one gameplay authority in both camera modes.
- No persistent red/black materials, missing reference spam, giant interaction bounds, accidental left-click interaction, ungrounded enemies, or looping annoying audio on the main route.
- Every content family shipped in beta has validated JSON, deterministic fallback, persistence, UI, mechanics/world effect, deduplication, and at least one play-mode test path.
- A player can complete the golden loop three times in one session and once after a reload without content loss or duplicate rewards.
- A model outage/degraded response does not block the tutorial, combat, saving, or core objective completion.

### Essential beta telemetry

Track locally or through the approved analytics path:

- Tutorial step completion/drop-off and time-to-first-combat/offer.
- Generation request count, latency, success/reject/fallback rate, retry rate, and reject reasons.
- Offer eligibility, display, accept/decline/incubate rates, and duplicate suppression.
- Quest creation/completion/failure/abandon states and impossible-objective detections.
- Save/load migration results, record counts, corrupt-save recovery, and duplicate reward prevention.
- Errors by category: material, animation, player authority, input, asset bind, generation, validation, save, and null/missing reference.
- Performance snapshots for scene load, frame time, allocation spikes, and memory across a 60-minute session.

## 13. Milestone Build Plan

Milestones are ordered gates, not a permission to work on everything simultaneously. Finish the active gate, stabilize it, then proceed.

### M0 - Baseline and ownership map

**Goal:** establish a reproducible baseline without refactoring the game.

**Build:** document the authoritative player, save owner, event recorder, generation entry points, main tutorial scene/prefab, and relevant known failures. Add focused logging/diagnostics only where existing evidence is missing. Create a short test checklist for the golden loop.

**Exit criteria:** the team can name one owner for player state, save state, event recording, quests, abilities, and LLM transport; a clean compile/build result is captured; the major known runtime errors are categorized; and no broad redesign has occurred.

### M1 - Stabilize the playable body

**Goal:** make movement, camera, input, avatar, equipment, and basic combat trustworthy.

**Build:** enforce one authoritative player; repair first-/third-person presentation and sockets; ensure attack vs. interact input separation; correct jump/dash/crouch animation intent; ground enemy movement; remove obvious material failures in the main route; add readable melee/hit/dash/pickup audio.

**Exit criteria:** the tutorial route can be traversed and fought through in both camera modes with one player authority, visible held equipment, no accidental interactions, no ghost player, no fatal animation/material errors, and no enemy accidental flight.

### M2 - Deterministic responsive-world spine

**Goal:** prove act -> record -> deterministic world response -> save/load.

**Build:** typed event ledger, behavior rollups, region/activity state, objective evaluator, versioned save migration, clear director status, and deterministic fallback response records. Wire movement, combat, dodge, crouch, dialogue, loot/pickup, shrine, trade, harvesting attempt, craft, and quest events.

**Exit criteria:** a player performs meaningful action batches, receives an immediate visible world/quest/progression response without a model call, saves, reloads, and retains behavior memory, accepted state, inventory, and region/canon changes.

### M3 - Generated origin and content safety pipeline

**Goal:** make a new save produce a valid, persisted, playable identity package.

**Build:** origin questionnaire, strict origin schema, parser, validator, normalizer, curated ability/item binders, fallback origin, save commit, reveal UI, generated origin quest, and reject reason telemetry. Include the canon seed and prompt hash.

**Exit criteria:** valid, malformed, timeout, and unavailable-model cases each lead to a playable origin; the accepted origin survives reload; equipment can be equipped; the generated quest uses explicit objectives; and no raw model field directly chooses code or asset paths.

### M4 - Polished starter territory and tutorial

**Goal:** turn the prototype route into a polished, reliable first hour.

**Build:** finish Vey's hut/study, crossroads, wilds, ruin/estate encounter, shrine, chest/mimic, practice lock, merchant, gathering node, enemy encounters, tutorial UI, and a clean route through tutorial beats. Integrate only vetted assets that serve these interactions.

**Exit criteria:** a fresh player completes origin, equip, dialogue, movement, combat, ability use, shrine, lock/chest risk, loot, merchant/gathering introduction, and first progression offer in one coherent session with no prototype clutter or blocking confusion.

### M5 - Quest, NPC memory, trade, and generated world response

**Goal:** make the territory feel responsive after the tutorial.

**Build:** quest generator for supported schemas, objective/reward/consequence validation, NPC records and dialogue summaries, merchant disposition and trade framing, rumor/world-note generation, canon fact checks, duplicate suppression, and a bounded context-pack builder.

**Exit criteria:** two generated quests with different player reasons can be completed; one NPC remembers a relevant prior choice after reload; a trade choice changes a supported social/economic response; invalid/generic/contradictory outputs are rejected or repaired visibly in diagnostics.

### M6 - Latent capabilities and combinations

**Goal:** prove the unique adaptive-progression promise with depth, not breadth.

**Build:** capability registry, evidence gates, offer state machine, 6 to 8 beta paths, generated framing/names, acceptance UI, initial upgrades, social relationship compatibility/boundaries, and limited spell-combination registry.

**Exit criteria:** playtest scripts for woodcraft, trade, guard/mercy, stealth, mobility, stillness, arcane experimentation, and social rapport can each earn or advance a valid offer; declining/incubating works; acquired capabilities have real mechanics; no unsupported behavior creates an empty or broken unlock.

### M7 - Content density, balancing, and beta hardening

**Goal:** make the vertical slice replayable across different identities.

**Build:** additional generated record variations inside the existing schemas, encounter/balance pass, tutorial pacing pass, UI polish, accessibility pass, audio/VFX mix, error cleanup, performance profiling, save migration tests, telemetry dashboards/reports, and beta feedback/report flow.

**Exit criteria:** at least three substantially different play styles can finish the first-hour route with distinct origins, at least one distinct generated quest/offer/relationship reaction, stable frame time targets set by the team, no known blocker defects, and a scripted regression pass succeeds after a clean build.

### M8 - Open-beta release gate

**Goal:** release a coherent, observable beta rather than an unfinished sandbox.

**Build:** release candidate branch/build process, crash/error review, first-run consent/privacy copy, feedback/report UI, known-issue list, recovery support for corrupted generated content, content safety review, compatibility matrix, and launch telemetry validation.

**Exit criteria:** the release checklist below is signed off; the golden loop survives model degradation; players can report generated content; triage data reaches the team; and the product clearly communicates that procedural depth will expand from the proven starter territory.

## 14. Work GPT Execution Playbook

### Before changing code

1. Identify the active milestone and its exit criteria.
2. State the exact subsystem owner and the smallest target behavior.
3. Search exact symbols/serialized fields first; read only the target implementation and direct dependencies.
4. Check whether the change can affect save compatibility, prefab/scene references, animation, material bindings, input ownership, or generated-record schema.
5. Make the smallest coherent change. Do not introduce an alternate manager, player, quest path, or content authority.

### When adding generated content

Use this checklist:

- [ ] Schema version and stable id defined.
- [ ] Request context is bounded and contains only relevant canon/evidence.
- [ ] Parser rejects malformed JSON with a reason.
- [ ] Validator checks required fields, enums, limits, UI length, safety, canon, duplicate, and gameplay feasibility.
- [ ] Normalizer maps all generated semantics to supported runtime keys.
- [ ] Deterministic fallback exists and is tagged as fallback.
- [ ] Accepted record is transactionally persisted with source metadata and raw payload.
- [ ] UI is safe for long/invalid text and visibly communicates pending/fallback status.
- [ ] Runtime effect is deterministic and mechanically meaningful.
- [ ] Save/load and play-mode verification are performed.

### When adding a natural unlock

- [ ] Define the code-owned capability key and supported mechanic first.
- [ ] Define typed evidence events, frequency/intention/diversity/persistence thresholds, cooldown, and novelty rule.
- [ ] Define prerequisites, incompatibilities, and player-choice state.
- [ ] Add a deterministic eligible/offered fallback path before generated flavor.
- [ ] Let generation explain/narrate/name the offer; never let it invent the capability mechanics.
- [ ] Test false positives, repeated attempts, decline/incubate, save/load, and overlap with similar paths.

### When working on Unity presentation

- [ ] Reuse the authoritative player and existing scene/prefab structure.
- [ ] Use semantic asset keys through the binder; do not accept generated paths.
- [ ] Verify first and third person against the same player state.
- [ ] Check material/shader compatibility, scale, colliders, sockets, and interaction bounds in play mode.
- [ ] Validate main-route audio loops and VFX intensity.
- [ ] Compile once after edits and run the smallest relevant play-mode/build validation.

### When a generation call fails

1. Preserve the raw result/error and generation id.
2. Do not partially commit the record.
3. Log a categorized reason: transport, timeout, parse, schema, safety, canon, duplicate, unsupported mechanic, asset binding, or persistence.
4. Present the deterministic fallback or defer status without blocking play.
5. Retry only according to the director's bounded policy; do not spin a request loop.

## 15. Test Matrix

### Critical manual play tests

| Test | Pass condition |
|---|---|
| New save, valid model | Origin, equipment, objective quest, and reveal are accepted and playable |
| New save, model unavailable | Deterministic fallback origin and quest work without blocking |
| New save, malformed model JSON | Record rejected, fallback shown, raw/error retained, no corrupt save |
| Camera toggle | One player state; no ghost player; equipment and combat work in both modes |
| Input | Left click attacks only; E interacts only; secondary ability behaves correctly |
| Quest | Objective event progresses the intended quest only; reload retains exact state |
| Loot/trade | Item ids, quantity, pricing, inventory, and merchant response persist |
| Latent unlock | Repeated supported evidence produces one valid offer, with no duplicate spam |
| Decline/incubate | Choice survives reload and respects suppression/cooldown rules |
| Dialogue memory | Relevant NPC reaction/summary survives reload without contradicting canon |
| Spell combination | Compatible pair produces one supported result; incompatible pair fails cleanly |
| Material/asset | Main route has no red/black fallback or missing binding errors |
| Long session | 60-minute session avoids runaway generation, duplicate records, memory leak symptoms, and accumulating error spam |

### Automated-test priorities

1. Schema parse/validation/normalization for every content family.
2. Objective evaluator event matching and duplicate reward prevention.
3. Behavior confidence/novelty gates and capability offer state transitions.
4. Canon contradiction and duplicate detection.
5. Save serialization, migration, and round-trip identity.
6. Ability template limits and semantic asset binding validation.
7. Context-pack size and relevant-fact selection.

## 16. Release Checklist

### Product

- [ ] First hour is coherent for combat-first, trade-first, exploration-first, and behavior-experimenting players.
- [ ] At least six latent path families are genuinely playable and meaningful.
- [ ] Open-beta scope is reflected in menus, tutorial, messaging, and boundary gates; no deferred system is implied to be finished.
- [ ] Generated content has visible player relevance and does not repeatedly produce generic regional filler.

### Reliability

- [ ] Valid, slow, unavailable, malformed, unsafe, unsupported, duplicate, and contradictory model outputs have verified behavior.
- [ ] No accepted generated record is regenerated on load.
- [ ] Save/load works at origin, mid-quest, after offer choice, after trade, and after NPC-memory update.
- [ ] Main route has no release-blocking material, animation, input, authority, or missing-reference errors.

### Safety, privacy, and feedback

- [ ] Generated-content moderation/validation and report flow are live.
- [ ] Romance/sexual-theme controls and adult/consent boundaries are enforced.
- [ ] Player-facing generation-status and data/privacy language are clear.
- [ ] Beta feedback includes reproduction context, save/build version, and generation id when relevant.

### Operations

- [ ] A release candidate was playtested from a clean new save and a migrated existing save.
- [ ] Crash/error/telemetry review has an owner and response process.
- [ ] Known issues and rollback/recovery plan are prepared.
- [ ] The next post-beta milestone is chosen from evidence, not from a feature wish list.

## 17. Post-Beta Expansion Rule

After beta, expand in the order that strengthens the proven core:

1. Add depth and variants to the starter territory's strongest player-driven paths.
2. Add one second region using the same records, binders, canon, save, and generation pipeline.
3. Add a small number of new capability families only when their deterministic mechanic, evidence, and asset support are ready.
4. Expand factions, crafting, romances, advanced spell combinations, and world simulation only after the associated beta evidence shows players use and understand the current system.

Do not add breadth simply because it is expected of a fantasy RPG. Add it when it gives the player more ways to teach the world who they are.

## 18. Final Decision Standard

When choosing whether to add or cut a feature, ask:

1. Does it make the player feel observed, recognized, or consequential?
2. Can it be represented as structured persisted data and executed deterministically?
3. Does it fit the one-territory open-beta loop today?
4. Can a player understand its mechanical consequence without reading a design document?
5. Can the team test it under LLM success and failure?

If the answer to any is no, defer it. The beta succeeds by making a small world feel intelligently alive, not by claiming every possible RPG system before the core can carry it.
