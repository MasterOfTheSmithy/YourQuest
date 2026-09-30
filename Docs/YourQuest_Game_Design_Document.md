# YourQuest Game Design Document

## 1. High Concept

YourQuest is a single-player fantasy RPG built around a living procedural system that observes the player, interprets their behavior, and generates the game around them. The fantasy is not merely that the world is large or randomized. The fantasy is that the game understands the player's intent, habits, obsessions, failures, jokes, patience, cruelty, mercy, cowardice, ambition, laziness, curiosity, and improvisation, then turns those patterns into quests, titles, skills, spells, items, NPC memories, lore, monsters, regions, and long-term consequences.

The player is not choosing from a fixed class list. The player is being read by a system.

The world is not a static authored campaign. The world is a deterministic record of what the player has caused, discovered, avoided, named, damaged, protected, ignored, or repeated.

The game should feel like the beginning of a grand progression fantasy: a mortal player is seen by something immense, receives a system-like awakening, and slowly becomes a figure the world must adapt around. Inspirations include the scale and player-centric awakening fantasy of Solo Leveling and The Beginning After the End, while still leaving room for silly, strange, emergent paths that can evolve into real myth.

## 2. Core Design Rule

Nothing player-facing should be permanently hardcoded.

This includes:
- Item names.
- Item descriptions.
- Quest names.
- Quest descriptions.
- Quest objectives.
- Skill names.
- Skill descriptions.
- Spell names.
- Spell descriptions.
- Title names.
- Class names.
- NPC names, personalities, memory, dialogue, and lore.
- Monster names, descriptions, behavior profiles, abilities, and lore.
- Region names, descriptions, factions, histories, and conflicts.
- World canon.
- Tutorial narrative framing.
- Progression offers.
- Rewards.
- Rumors.
- Books, signs, notes, and readable text.
- Dialogue transcripts and summaries.
- Faction relationships.

Code may hardcode:
- Data schemas.
- Validation rules.
- Deterministic seed logic.
- Serialization and persistence.
- Gameplay execution systems.
- Safety limits.
- Asset binding rules.
- Mechanical formulas.
- Animation state contracts.
- VFX and SFX playback systems.
- Fallback scaffolding for catastrophic LLM failure.
- Prompt templates.
- Content quality gates.

Code should not hardcode the actual content the player experiences as canon, reward, identity, lore, or narrative truth. If code contains fallback examples, they must be clearly treated as fallback only and replaced by generated persisted records as soon as possible.

The correct architecture is:

1. The LLM generates structured content.
2. The game validates it.
3. The game normalizes it into safe records.
4. The game persists it into the save.
5. Runtime systems execute the persisted records deterministically.

The LLM should not be asked every frame for the same decision. Once content is accepted, it becomes part of the save state.

## 3. Target Player Experience

The player starts a new save inside a hut with Archivist Vey nearby. A goddess or system-like divine intelligence asks a questionnaire. The questionnaire is not just roleplay flavor. It generates the player's first identity package:

- Origin direction.
- Class.
- Title.
- Stat distribution.
- First skill or spell.
- First quest.
- Starting equipment.
- Initial relationship to the world.
- Initial tutorial framing.

If the player says they want to be a merchant, the game should not force them into warrior content with merchant flavor. It should give them a trade-aware beginning with risk reading, bargaining, inventory, item appraisal, contract hooks, and social leverage.

If the player says they want to be a lumberjack, the game should produce tool-based survival, timber, strength, forest labor, craft instincts, and possibly a godlike natural precursor such as Auralith, the First Green. The result should be about the player's relationship to tools, labor, nature, and survival, not simply "jungle region skill."

If the player says they want to be a demonlord, the game should interpret ambition, domination, forbidden authority, fear, pact logic, and controlled dark power. It should not simply give a generic fireball.

If the player sits on a path and refuses to move for hours, the game should notice. It should evolve that behavior into a path:

- Lazy.
- AFK.
- Waiting for Something.
- Master of Waiting.
- Like a Stone.
- Unmovable.
- God of Stillness.

This can start goofy, but if repeated long enough it should become real. It might unlock stillness skills, immovable defense, time dilation, meditation, ambush resistance, divine patience, or a faction that worships still figures.

Every repeated behavior should have the potential to become a system-recognized path if the player keeps doing it.

## 4. Design Pillars

### 4.1 Player-First Generation

Generated content must respond to the player first and the region second.

Bad:
- "Jungle Slash."
- "Frost Region Quest."
- "Cinderfall Skill."

Better:
- "Rootbound Patience" because the player keeps waiting in danger and surviving through terrain.
- "Debt-Eye Appraisal" because the player repeatedly trades, hoards, and avoids direct combat.
- "Quiet Dominion" because the player wants power but avoids noisy aggression.
- "Mercy Wall" because the player keeps protecting weaker NPCs.

Regions are pressure. The player response is identity.

### 4.2 Deterministic Persistence

Generated content must be deterministic once committed.

The same save seed, same origin answers, and same behavior history should produce the same committed records. If the LLM produces a result, that result is stored in the save and reused. The game must not regenerate names or mechanics unpredictably every time a menu opens or a scene reloads.

Determinism comes from:
- Save seed.
- Event ledger.
- Behavior counters.
- Prompt context.
- JSON output.
- Validation.
- Persisted accepted records.

### 4.3 Structured Content, Not Freeform Guessing

Prose is not enough. Every generated quest, skill, spell, item, monster, NPC, and region needs machine-readable fields.

Example quest fields:
- `questId`
- `name`
- `description`
- `tags`
- `objectives`
- `rewards`
- `failureConditions`
- `generationSource`
- `seed`
- `payloadJson`

Example objective fields:
- `type`
- `targetId`
- `counterKey`
- `counterPrefix`
- `requiredCount`
- `description`

The player can read beautiful text, but the game should complete quests using objective records, not by guessing whether the quest description contains "talk" or "kill."

### 4.4 Meaningful Progression

Titles, skills, spells, quests, and items must matter mechanically.

A title should not just be a label. It should eventually influence:
- Stats.
- Dialogue reactions.
- Faction reactions.
- Unlock conditions.
- Skill evolution.
- Quest generation.
- World rumors.
- NPC memory.

A skill should not just be a name. It needs:
- Activation condition.
- Resource cost.
- Cooldown.
- Targeting mode.
- Animation intent.
- VFX family.
- SFX family.
- Tags.
- Upgrade path.
- Failure cases.
- Interaction with player behavior.

A quest should not just be a paragraph. It needs:
- Clear objectives.
- Completion rules.
- Rewards.
- Consequences.
- Related NPCs, regions, items, and world state.

### 4.5 Goofy Can Become Mythic

Silly and strange player behavior is allowed. The game should not reject it automatically.

Examples:
- Standing still for absurd lengths of time.
- Collecting only spoons.
- Refusing to attack.
- Jumping before every dialogue.
- Opening every chest while crouched.
- Selling every weapon.
- Trying to be a demonlord accountant.

At first, the system may treat these as oddities. If repeated, they become patterns. If sustained, they become identity. If extreme, they become myth.

### 4.6 Curated, Not Random

Procedural does not mean messy.

The tutorial and world should feel curated, polished, and intentional. Generated content should be validated for:
- Relevance to player behavior.
- Mechanical clarity.
- Lore consistency.
- Tone.
- Novelty.
- Non-duplication.
- Absence of placeholder language.
- Absence of region-first naming.
- Absence of generic fluff.

The LLM is a generator, not an excuse for incoherence.

## 5. Genre and Camera

YourQuest is a third-person and first-person fantasy action RPG with:

- Exploration.
- Dialogue.
- Procedural quests.
- Player-specific progression.
- Combat.
- Spells.
- Skills.
- Inventory and equipment.
- Lockpicking.
- Looting.
- Monsters.
- NPC relationships.
- Region/world state.
- Dynamic lore.

The player can toggle first-person and third-person view. The third-person view must show the correct player avatar and equipment. There must be exactly one authoritative player model. Armor, weapons, and shields must follow the animated skeleton correctly.

## 6. Game Loop

### 6.1 Moment-to-Moment Loop

1. Player moves, observes, talks, fights, loots, trades, waits, crafts, explores, or performs any meaningful action.
2. Action recorder captures the event.
3. Behavior counters and ledger update.
4. Runtime systems respond immediately if simple and deterministic.
5. The LLM director may generate structured content when thresholds are met.
6. Generated content is validated, persisted, and surfaced as quests, offers, world changes, NPC memories, items, skills, spells, or titles.
7. Player accepts, rejects, completes, exploits, ignores, or evolves the content.
8. The world updates its memory.

### 6.2 Session Loop

1. Load save.
2. Restore player state, world state, generated content, quest objectives, NPC memory, and region state.
3. Enter current scene/region.
4. Continue from persisted procedural state.
5. Generate new content only when required by new behavior or missing committed data.
6. Save changes.

### 6.3 Long-Term Loop

1. Player develops repeated patterns.
2. System detects stable behavioral identity.
3. Titles, classes, skills, spells, NPC perceptions, and world lore evolve.
4. Regions adapt around player reputation and actions.
5. Generated quests become more specific.
6. The player becomes mythic in a direction earned through play.

## 7. New Save Flow

### 7.1 Starting Location

New saves begin inside Archivist Vey's hut. This is the controlled onboarding space.

The hut should feel finished:
- Warm lighting.
- Contained interior.
- Readable interactables.
- No overlapping trigger chaos.
- No visible demo clutter.
- Ambient hut sound.
- Subtle magical presence.
- Archivist Vey available after the origin ceremony.

### 7.2 Goddess Questionnaire

The goddess asks questions that reveal:
- Preferred response to danger.
- Desired life direction.
- Relationship to power.
- Relationship to labor.
- Relationship to mercy.
- Relationship to wealth.
- Relationship to secrets.
- Relationship to boredom and time.
- Preferred element or magic.
- Desired identity.

Modes:
- Casual: short questionnaire.
- In Depth: medium questionnaire.
- Hardcore: long questionnaire.

The questionnaire should support freeform answers. The player should not be limited to a class menu.

### 7.3 Origin Generation

After the final answer:

1. UI enters "The Goddess Is Reading" state.
2. Game sends structured prompt to LLM.
3. LLM returns origin JSON.
4. Game validates JSON.
5. If valid, commit to save.
6. If invalid or unavailable, use deterministic fallback.
7. Manifest starting equipment with VFX and SFX.
8. Award class, title, stats, skill/spell, quest.
9. Start first machine-readable quest.
10. Player speaks with Archivist Vey.

### 7.4 Origin Package Schema

The origin package should include:

```json
{
  "source": "llm_origin_v1",
  "directionKey": "merchant|lumberjack|hero|demonlord|arcanist|warden|wanderer|stillness|custom",
  "stimulus": "specific player stimulus",
  "identityKeywords": ["origin_generated", "player_response"],
  "stats": {
    "vitality": 12,
    "strength": 11,
    "dexterity": 10,
    "intelligence": 13
  },
  "className": "...",
  "classDescription": "...",
  "titleName": "...",
  "titleDescription": "...",
  "ability": {
    "name": "...",
    "kind": "skill|spell",
    "type": "combat|movement|utility|craft|social|control",
    "description": "...",
    "targetingMode": "self|melee|projectile|pulse|buff",
    "resourceType": "mana|stamina|none",
    "resourceCost": 15,
    "cooldownSeconds": 0.65,
    "vfxFamily": "physical|fire|frost|storm|poison|heal|shield|shadow|earth|air|arcane",
    "animationIntent": "melee|cast|guard|dash|idle"
  },
  "quest": {
    "name": "...",
    "description": "...",
    "tags": ["origin_generated", "tutorial_main"],
    "objectives": [
      {
        "type": "origin_manifested",
        "description": "The goddess manifests gear from the player's answers.",
        "counterKey": "origin:equipment_manifested",
        "requiredCount": 1
      },
      {
        "type": "equip_item",
        "description": "Equip one manifested item.",
        "counterPrefix": "item:equip",
        "requiredCount": 1
      },
      {
        "type": "talk_to_npc",
        "targetId": "npc_archivist_01",
        "targetName": "Archivist Vey",
        "description": "Speak with Archivist Vey.",
        "counterPrefix": "dialogue:npc_archivist_01",
        "requiredCount": 1
      }
    ]
  },
  "loadout": [
    {
      "slot": "weapon",
      "role": "main hand identity item",
      "nameHint": "...",
      "descriptionHint": "..."
    }
  ]
}
```

## 8. World Model

The world is a generated memory graph, not a static list of zones.

The implementation authority for terrain, settlements, asset kits, spatial compilation, streaming, dynamic world revisions, and world-quality gates is [YourQuest AAA Procedural World Generation Design Plan](YourQuest_AAA_Procedural_World_Generation_Design_Plan.md).

### 8.1 World State Contains

- Global canon ledger.
- Active world era.
- Factions.
- Regions.
- Locations.
- NPCs.
- Monster ecologies.
- Rumors.
- Discovered facts.
- Player reputation.
- Open conflicts.
- Unresolved consequences.
- Generated world events.

### 8.2 World Generation Rules

World generation must:
- Use the save seed.
- Use player history.
- Use committed canon.
- Avoid contradicting persisted facts.
- Generate structured records first.
- Instantiate assets second.
- Update canon only after validation.

### 8.3 Regions

Regions should be generated as records:

- `regionId`
- `name`
- `description`
- `biome`
- `mood`
- `dangerProfile`
- `dominantFactions`
- `localMyths`
- `resourceProfile`
- `monsterFamilies`
- `npcAnchors`
- `questHooks`
- `audioProfile`
- `visualPalette`
- `navigationConstraints`
- `generationSource`
- `payloadJson`

Regions should never be the primary identity source for player rewards. A region can pressure the player, but the player response determines the generated skill/title/spell.

### 8.4 Locations

Locations are smaller generated records inside regions:

- Hut.
- Shrine.
- Cave.
- Ruin.
- Road.
- Village.
- Dungeon room.
- Merchant camp.
- Monster den.
- Hidden cache.
- Training yard.

Locations need:
- Gameplay purpose.
- Visual asset set.
- Interactables.
- NPCs.
- Monsters.
- Loot.
- Ambient sound.
- Completion state.

## 9. NPC Design

NPCs are generated characters with persisted memory.

### 9.1 NPC Record

Each NPC should have:

- `npcId`
- `name`
- `role`
- `description`
- `personality`
- `voiceRules`
- `factionId`
- `homeRegionId`
- `homeLocationId`
- `relationshipToPlayer`
- `knownPlayerTitles`
- `memory`
- `dialogueHistorySummary`
- `currentIntent`
- `questHooks`
- `services`
- `secrets`
- `fear`
- `desire`
- `generationSource`
- `payloadJson`

### 9.2 Dialogue

Dialogue must be generated by the LLM but constrained by:
- NPC personality.
- NPC memory.
- Current world state.
- Player actions.
- Player reputation.
- Active quest state.
- Tone rules.
- No fourth-wall language unless intentionally diegetic.

NPCs should remember conversations. Dialogue transcript or summary should update after each conversation.

### 9.3 Archivist Vey

Archivist Vey is the first tutorial anchor. Eventually even Vey's detailed dialogue and relationship to the player should be generated/persisted, but her role as a first onboarding anchor can remain structural.

Vey's purpose:
- Explain that the world responds to evidence, not prophecy.
- Introduce dialogue.
- Introduce quest tracking.
- Introduce generated origin consequences.
- Give context for the first trial without overriding the player's identity.

## 10. Quest System

Quests are generated records with objective lists.

### 10.1 Quest Types

- Origin quest.
- Tutorial quest.
- NPC request.
- Behavior-response quest.
- Region exploration quest.
- Monster hunt.
- Trade contract.
- Crafting request.
- Secret discovery.
- Faction task.
- Title evolution trial.
- Skill evolution trial.
- Spell control trial.
- Consequence quest.
- Failure recovery quest.
- Joke path escalation quest.

### 10.2 Objective Types

Core objective types:

- `talk_to_npc`
- `equip_item`
- `origin_manifested`
- `cast_spell`
- `use_skill`
- `defeat_enemy`
- `loot_item`
- `pickup_item`
- `open_lock`
- `mimic_reveal`
- `use_shrine`
- `enter_region`
- `discover_location`
- `wait_seconds`
- `craft_item`
- `sell_item`
- `buy_item`
- `protect_npc`
- `avoid_damage`
- `take_damage`
- `deal_damage`
- `sneak_past`
- `read_lore`
- `choose_dialogue_intent`

Each objective must be completable through counters, target ids, or explicit event records.

### 10.3 Quest Generation Rules

Generated quests must:
- Have a player-specific reason.
- Have clear objectives.
- Have relevant rewards.
- Avoid generic "go here, kill that" unless the player's behavior supports it.
- Persist into save.
- Avoid duplicating existing quests.
- Respect world canon.
- Update world state on completion.

## 11. Skills and Spells

Skills and spells are generated mechanics.

### 11.1 Skill/Spell Record

Required fields:

- `skillId`
- `familyId`
- `name`
- `kind`
- `type`
- `tier`
- `rank`
- `description`
- `stimulus`
- `targetingMode`
- `resourceType`
- `resourceCost`
- `cooldownSeconds`
- `range`
- `areaRadius`
- `powerFormula`
- `statusEffects`
- `vfxFamily`
- `sfxFamily`
- `animationIntent`
- `incompatibleAnimationTags`
- `upgradeRules`
- `generationSource`
- `payloadJson`

### 11.2 Targeting Modes

- `self`
- `melee`
- `projectile`
- `pulse`
- `ground_target`
- `cone`
- `beam`
- `summon`
- `buff`
- `debuff`
- `trap`

### 11.3 Animation Intents

- `idle`
- `melee`
- `cast`
- `guard`
- `dash`
- `jump`
- `crouch`
- `channel`
- `emote`

Animation intent is not the animation clip name. It is a stable gameplay contract that maps generated abilities to available animation layers.

### 11.4 Evolution

Skills and spells evolve through repeated use and player context.

Examples:
- Dash can evolve into dash leap only if the player develops acrobatics or mobility patterns.
- Stillness can evolve into defense, meditation, time, or immovable-body mechanics.
- Merchant appraisal can evolve into contract sight, debt binding, or value theft.
- Lumberjack tool skills can evolve into terrain shaping, root negotiation, or Auralith-linked living wood.

Evolution must be generated, validated, and persisted.

## 12. Item System

Items are generated records with asset bindings.

### 12.1 Item Record

Required fields:

- `itemId`
- `templateId`
- `displayName`
- `itemType`
- `equipSlot`
- `rarity`
- `description`
- `quantity`
- `stats`
- `effects`
- `iconKey`
- `prefabKey`
- `materialProfile`
- `vfxFamily`
- `sfxFamily`
- `lore`
- `origin`
- `ownerHistory`
- `generationSource`
- `payloadJson`

### 12.2 Equipment Slots

- Weapon.
- Offhand.
- Head.
- Chest.
- Gloves.
- Legs.
- Boots.
- Belt.
- Cloak.
- Ring left.
- Ring right.
- Earring left.
- Earring right.
- Necklace.
- Trinket.

### 12.3 Generated Item Requirements

Items must:
- Have a mechanical purpose.
- Use compatible assets.
- Render correctly.
- Avoid missing material placeholders.
- Fit the player's origin or source context.
- Persist after generation.
- Avoid duplicate spam.

## 13. Monsters and Enemy AI

Monsters should be generated families, not only prefabs.

### 13.1 Monster Record

- `monsterId`
- `name`
- `family`
- `description`
- `lore`
- `regionAffinities`
- `behaviorProfile`
- `movementMode`
- `abilities`
- `weaknesses`
- `resistances`
- `lootProfile`
- `animationProfile`
- `assetBinding`
- `generationSource`
- `payloadJson`

### 13.2 Movement Rules

Enemies should not float or fly unless their movement mode supports it.

Movement modes:
- Grounded walker.
- Crawler.
- Burrower.
- Jumper.
- Floater.
- Flyer.
- Teleporter.
- Stationary caster.

If a monster has no walk animation, it should not slide stiffly across the ground. It should use an alternate movement fantasy:
- Burrow underground.
- Surface particle trail.
- Pop out near the player.
- Root itself and attack at range.
- Hop with timed animation.

### 13.3 AI Behavior

Enemies should not only bum-rush the player.

AI should support:
- Approach.
- Retreat.
- Strafe.
- Evade.
- Cast.
- Guard.
- Ambush.
- Circle.
- Burrow.
- Summon.
- Flee.
- Call allies.
- Use terrain.

Enemy abilities can be generated, but must map to safe mechanical templates.

## 14. Combat

Combat should be readable, responsive, and connected to generated progression.

### 14.1 Player Inputs

- Left click: attack only.
- Right click: equipped spell or secondary ability.
- E: interact only.
- Q: dash.
- Ctrl: crouch.
- Space: jump.
- T: toggle first-person/third-person camera.

Left click must not interact.

### 14.2 Combat Goals

- Player weapons visibly swing.
- First-person weapons animate.
- Third-person avatar animates correctly.
- Armor follows skeleton.
- Shield and weapons stay in hands.
- No duplicate ghost player.
- No visible capsule.
- Movement animation maps correctly to actual motion.
- Dash is a simple dash unless evolved.
- Jump does not end with a roll unless an evolved skill requires it.

### 14.3 Ability Execution

Generated abilities must pass through a runtime executor:

1. Check resource.
2. Check cooldown.
3. Check animation compatibility.
4. Lock or blend animation.
5. Spawn VFX.
6. Play SFX.
7. Apply mechanical effect.
8. Record action.
9. Feed progression system.

## 15. Progression

Progression is behavior-based.

### 15.1 Tracked Behavior

Examples:
- Time standing still.
- Distance traveled.
- Damage dealt.
- Damage taken.
- Damage avoided.
- Kills.
- Mercy choices.
- Trades.
- Lockpicks.
- Dialogue frequency.
- Spell usage.
- Weapon usage.
- Crouch time.
- Jump count.
- Dash count.
- Deaths.
- Retreats.
- Looting habits.
- Waiting.
- Failure patterns.

### 15.2 Progression Outputs

The system can generate:
- Titles.
- Skill upgrades.
- New skills.
- Spell evolutions.
- Class evolutions.
- Quests.
- NPC reactions.
- Faction changes.
- Item mutations.
- World rumors.

### 15.3 Offer Model

Major progression changes should often be offered, not forced.

The player can:
- Accept.
- Decline.
- Ignore.
- Incubate.

Some factual titles may be automatic if the behavior is undeniable.

## 16. Lore and Canon

Lore is generated and committed.

The world should have grand scale:
- Gods.
- Precursors.
- Ancient systems.
- Lost eras.
- Awakenings.
- Mythic factions.
- Divine interpretation of mortal behavior.
- Player identity becoming canon.

But lore must remain consistent. The LLM cannot freely contradict committed canon. A world canon validator should check:
- Existing facts.
- NPC identities.
- Region state.
- Player choices.
- Timeline.
- Faction relationships.

## 17. Tutorial Design

The tutorial should be a polished introduction to all main systems, not a demo spawn.

### 17.1 Tutorial Beats

1. Start in Vey's hut.
2. Goddess questionnaire.
3. Origin manifestation.
4. Equip manifested gear.
5. Speak with Vey.
6. Learn interaction.
7. Learn quest tracking.
8. Learn movement.
9. Learn combat.
10. Learn spell/skill use.
11. Learn recovery shrine.
12. Learn lockpicking.
13. Learn chest/mimic risk.
14. Learn loot.
15. Learn progression offer.
16. Choose a direction or first road.

The exact text, rewards, and quest framing should be generated from the origin package.

### 17.2 Tutorial Area

The tutorial should include:
- Hut.
- Forest clearing.
- Paths.
- Rocks.
- Grass.
- Bushes.
- Flowers.
- Trees.
- Hills.
- Mountains in distance.
- Hidden chest.
- Cave.
- Practice lock.
- Shrine.
- Training enemy.
- NPC anchor.
- Region gates.

No cluttered prototype pads. No overlapping sign text. No giant interact bounds.

## 18. User Interface

UI must support generated content cleanly.

Required UI:
- Origin questionnaire.
- Generated result reveal.
- Quest tracker with objective checklist.
- Inventory.
- Equipment.
- Skills/spells.
- Titles/classes.
- Dialogue log.
- NPC transcript.
- Lockpick UI.
- Progression offers.
- Save/profile menu.
- Loading/title screen.

Generated text must fit UI containers. Long names need wrapping or scaling rules.

## 19. Audio and VFX

Audio and VFX should be data-driven.

Generated content selects families:
- Physical.
- Fire.
- Frost.
- Storm.
- Poison.
- Heal.
- Shield.
- Shadow.
- Earth.
- Air.
- Arcane.

Runtime maps those families to curated clips and effects.

No constant whining loops. Ambience should be region/location-specific and layered.

## 20. Technical Architecture

### 20.1 Major Systems

- `PlayerState`
- `WorldState`
- `LLMClient`
- Origin generation service.
- Progression generation service.
- World generation service.
- Dialogue generation service.
- Quest objective evaluator.
- Ability executor.
- Item generator and asset binder.
- NPC memory store.
- Event recorder.
- Behavior rollup.
- Director/orchestrator.
- Validation layer.

### 20.2 Content Generation Pipeline

1. Event occurs or missing content detected.
2. System builds prompt context.
3. LLM returns strict JSON.
4. Parser extracts JSON.
5. Validator checks schema.
6. Normalizer clamps values.
7. Curation checks player relevance.
8. Duplicate checker compares existing records.
9. Accepted content is persisted.
10. Runtime systems execute from persisted content.

### 20.3 Validation Requirements

Generated content must be rejected or repaired if:
- Missing required fields.
- Not valid JSON.
- Too generic.
- Mentions being generated.
- Names content after region ids instead of player behavior.
- Duplicates existing content.
- Has impossible objectives.
- Uses unsupported targeting mode.
- Uses unsupported asset key.
- Contradicts canon.
- Contains UI-breaking length.
- Has no mechanical effect.

### 20.4 Fallbacks

Fallbacks exist only to preserve playability.

Fallbacks should:
- Be deterministic.
- Be clearly marked as fallback.
- Use minimal content.
- Be replaced when LLM generation succeeds.

Fallbacks should not become the main content path.

## 21. Save and Determinism

Every generated record must save:

- Source.
- Seed.
- Prompt hash or generation id.
- Raw JSON payload.
- Accepted normalized fields.
- Creation timestamp.

The save is the source of truth. Runtime should never rely on regenerating accepted content from scratch.

## 22. Asset Binding

The LLM should not directly choose arbitrary Unity asset paths.

Instead:
- LLM generates semantic fields.
- Game maps semantic fields to approved asset keys.
- Asset binder validates key availability.
- Fallback asset is neutral and non-broken.

Example:

LLM says:
- `vfxFamily: shadow`
- `itemType: weapon`
- `style: sovereign`

Asset binder chooses:
- approved shadow VFX
- approved weapon prefab
- valid material profile

## 23. Known Current Problems To Fix

Reported issues that remain important:

- Red/black runtime material errors.
- Placeholder models showing red/black gradients.
- Cube texture assigned to 2D texture property errors.
- Player model and armor alignment problems.
- Duplicate ghost player.
- Third-person model missing or wrong.
- Weapons and shield not in hands.
- Idle/walk/run animation swaps.
- Dash/jump/roll animation issues.
- Third-person spell behavior changing unexpectedly.
- Enemies floating/flying away.
- Interact bounds too large and overlapping.
- Left click must not interact.
- Third-person interaction unreliable.
- Lockpick UI needs polished asset integration.
- Sign text overlap.
- NPC transcripts not updating.
- Tutorial area still too prototype-like.
- Missing attack/ambience sounds.
- Annoying constant whining sound.
- Startup lag and large error counts.

## 24. Production Priorities

### Phase 1: Procedural Foundation

- Finish generated origin pipeline.
- Make quests objective-record based.
- Make skills/spells mechanically data-driven.
- Persist all generated records.
- Remove hardcoded baseline content paths.

### Phase 2: Player Avatar and Combat Stability

- One authoritative player.
- Correct third-person model.
- Correct first-person weapon view.
- Equipment attached to bones.
- Correct animation states.
- Grounded enemies.
- Ability animation locks.

### Phase 3: Tutorial Polish

- Hut-first tutorial.
- Vey dialogue.
- Generated origin quest.
- Combat yard.
- Lockpick UI.
- Mimic.
- Shrine.
- Loot.
- Progression offer.
- First generated region transition.

### Phase 4: World Generation

- Generate regions.
- Generate NPCs.
- Generate monster families.
- Generate faction state.
- Generate locations.
- Generate world lore.
- Instantiate scenes from generated records.

### Phase 5: Long-Term Progression

- Behavior titles.
- Skill evolution.
- Spell evolution.
- Class evolution.
- Item mutation.
- Faction reputation.
- Mythic consequences.

## 25. Non-Negotiable Implementation Rules

- Do not add more fixed player-facing quest names as the primary path.
- Do not add more fixed skill/spell/title/item/lore names as the primary path.
- Do not solve procedural design with bigger hardcoded word pools.
- Do not let generated content remain freeform text only.
- Do not complete quests by parsing prose when objective records exist.
- Do not allow LLM output to directly break runtime systems.
- Do not regenerate accepted content every load.
- Do not let region names override player identity.
- Do not ship broken placeholder materials.
- Do not hide errors that affect runtime.

## 26. Definition of Done For A Generated Content Feature

A generated content feature is done only when:

- It requests structured JSON from the LLM.
- It validates the response.
- It has deterministic fallback.
- It persists accepted output.
- It displays properly in UI.
- It has mechanical effect.
- It survives save/load.
- It avoids duplication.
- It respects player behavior.
- It respects canon.
- It has asset-safe bindings.
- It has a build check.
- It has play-mode validation.

## 27. Short Product Statement

YourQuest is a deterministic LLM-driven procedural RPG where every meaningful part of the game, including items, quests, skills, spells, NPCs, monsters, regions, lore, dialogue, and progression, is generated in response to the player's actual behavior and persisted as canon. The player does not simply pick a class or follow a static campaign. The player teaches the game who they are, and the world becomes a record of that answer.
