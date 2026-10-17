# YourQuest developer console

Open the console with **backtick / ~** in released gameplay. Type `console key F2` to change the developer preference. The Inspector also exposes the toggle key on the runtime `YourQuest Developer Console` object. Enter runs a command, Tab cycles completions, Up/Down recall history, Escape closes, and Search filters the last 500 output lines. Input remains captured until closing keys/buttons are released. The current player/NPC target and **SESSION — SAVES BLOCKED** indicator stay visible.

## Guided browser

**Actors, Spawn, Items, Skills, Quests, Titles, Stats, Rules, Animations, Monsters, NPCs, Commands** provide searchable lists using actual runtime IDs. Hover a row to preview; click to select the entry used by action buttons. Find searches names, IDs, types, status and details. Prompt fields wrap text; stat/rule forms prefill actual current values with units/ranges. Pending offers stay separate from owned content.

- **Actors / Pick actor:** select a loaded actor from the list, or enable the docked scene picker in a Game view at least 900 pixels wide. Hover the visible world and click an actor's collider. The nearest solid obstruction prevents picking through walls. Player selection returns to the single authoritative player; NPC selection resolves its real stable ID for affinity. Unloaded targets fail without silently switching to player. Gameplay input remains captured while picking.
- **Stats / Rules:** select a variable and Set or Add the entered value. Rules exposes actual acquisition scores, cooldowns, confidence, evidence, context and evolution settings. Explicit force is required to disable admission checks or lower production floors. Overrides remain temporary service settings, never asset edits.
- **Spawn:** copy an accepted resident through the existing population binder, with optional synthetic name/persona fields and explicit override. It receives a unique temporary NPC ID and a real world NPC record; accepted world plans remain unchanged and transcript persistence is disabled. Create bound mechanical test items through the real generator, or choose a DOT catalog enemy for an explicit encounter test. Restore removes temporary residents/records, pickups and encounters. No second authoritative player is created.
- **Animations:** choose the selected actor's actual controller trigger/state and enable Force presentation preview. Existing animation/movement drivers are suspended and root motion disabled. Stop/Restore reinstates captured parameters, layer states, speed and enabled drivers; the command invokes no combat/spell service. Editor presets enumerate actual controller graphs; development players list triggers and accept validated typed state paths. Controller rebinding prevents restoring obsolete assets. Pending triggers and transitions are not exact animation checkpoints.
- **Titles:** generate a title or class proposal, review the pending offer, then Add through normal acceptance.

- **Items:** select an accepted item, enter a quantity, then Add or Remove. Non-stackable additions require quantity 1; stacks/capacity stay validated.
- **Skills:** Add follows normal acquisition rules. The explicit Force test checkbox exposes a rank and reports bypassed checks in Results. Revoke is available for owned skills. Generate can request a skill or spell proposal.
- **Quests:** Add a pending structured offer or select an accepted quest and Set active. Objective completion requires the explicit Force test checkbox.
- **Generate:** describe an item/skill/spell/quest idea and request a proposal through the lifecycle-aware LLM service and `ProgressionDecisionApplier`. Requests retain evidence/quality/context validation; skill/spell requests also retain the thinker score, cooldown and offer-cap checks. Results reports rejection or a queued pending offer. Review it, then Add separately. Missing services produce an error rather than placeholder content. Item binding remains with `GeneratedRpgContentService` on acceptance.
- **Monsters:** choose a loaded encounter template, enter 1–8, and Spawn near player. The existing `YQInvestorEnemySpawner` supplies approved model/faction/region and combat mechanics, retaining origin, proximity and source quest-counter gates. Spawned enemies use separate test slot identities and never respawn. Encounter IDs are session-local; loading/unloading can invalidate them. Restore removes these test enemies/corpses. Normal profile publication is blocked while a temporary encounter exists.
- **NPCs:** choose an existing canonical NPC as target. Inspect identity/faction/location/affinity; Spawn provides temporary copies of accepted resident templates with optional test name/persona.
- **Commands:** inspect command syntax or choose Use in Advanced to fill the prompt. Advanced retains all original stat/progression commands, force overrides, history and autocomplete. Browse and Advanced can be folded independently. Small Game views can scroll the panel to reach all controls.

Player-only actions stay disabled for an NPC target. The Player, Snapshot and Restore buttons provide the same typed commands as the prompt. No guided button implicitly publishes a normal save.

Generate also supports **title/class** and optional **synthetic test context**. Set context explicitly, then use Force proposal; normal generation rejects active fake context. Synthetic information never enters action counters or evidence ledgers. Force proposal bypasses thinker skill score/cooldown and behavior/calm/meaningful-context admission for that synchronous proposal, restoring exact applier flags even on rejection. Confidence, quality, similarity/evolution, objective, identity and offer-cap checks remain active. Review and Add are separate. Pending prompts cannot execute code or arbitrary asset paths.

Temporary NPCs, pickups, encounters and animation previews block normal profile publication until Restore, even after an animation is stopped. Restore also clears synthetic context and accepted test titles/classes. Existing normal world/time/position rollback limitations below still apply.

The console installs in the Unity Editor. A player build needs **both Development Build and the `YQ_DEVELOPER_CONSOLE` scripting define**. Public builds compile out the overlay, registry, test session and mutation APIs even if the define is accidentally present. No public admin mode or network admin transport is supplied. Inspected character/progression/inventory/profile paths use local authoritative state owners; this console does not grant server permissions.

## Commands

Names/IDs are exact; variable names use the case shown by `stat list` and `progression inspect`. Double quotes preserve spaces in an identifier. Arguments are typed finite numbers or integers; arbitrary code, JSON execution and arbitrary asset paths are not accepted.

| Command | Meaning |
|---|---|
| `help [command]` | Syntax and supported operations |
| `target player`, `target list`, `target inspect` | Select/list/inspect actual state IDs |
| `target npc <npcId>` | Select a persisted `WorldState.NpcRecord` |
| `actor list`, `target actor <actor:instanceId>`, `actor inspect` | Loaded actor selection; IDs from Actors/Pick actor expire on unload |
| `animation list`, `animation play <statePath> [layer 0..7] --force`, `animation trigger <name> --force`, `animation stop` | Actual controller inspection and reversible presentation preview |
| `stat list`, `stat get <name>` | Variables, values, units, safety ranges |
| `stat set <name> <value>`, `stat add <name> <delta>` | Edit through the player/world state owner |
| `skill list`, `ability list` | Accepted skill IDs, Editor committed assets, earned pending offer IDs |
| `skill grant <skillId\|pendingOfferId> [rank] [--force]` | Normal earned acquisition/acceptance; explicit force skips acquisition gates and reports them |
| `skill revoke <skillId>` | Lock the accepted record; clear equipped skill slots while retaining family identity |
| `skill rank set <skillId> <rank>` | Explicit direct test edit, integer 1–100 |
| `progression inspect` | Real acquisition settings, units, effective floors |
| `progression set <name> <value> [--force]` | Temporary runtime rule override; lowering production score floors/disabling requirements needs force |
| `inventory`, `item list` | Accepted item instance and template IDs |
| `item give <acceptedItemId> <count>` | Clone an already accepted/bound item; honor shared capacity, mechanical matching and stack cap 99 |
| `item remove <itemId> <count>` | Remove owned quantities; unequip before removing the last equipped copy |
| `quest list`, `quest activate <questId>` | Existing active-quest selection |
| `quest complete <questId> --force` | Existing completion/reward hook, explicitly bypassing objective checks |
| `offer accept <pendingOfferId>` | Add a validated pending item/skill/spell/quest/title/class through normal acceptance |
| `generate item\|skill\|spell\|quest\|title\|class "<brief>" [--force]` | Pending proposal, 1–400 characters; does not grant it automatically |
| `generation context "<fake context>" --force`, `generation context clear`, `generation inspect` | Explicit prompt-only synthetic context, up to 400 characters |
| `item create <kind> <level 1..40> [count 1..99] --force` | Bound mechanical test item through existing generator; equipment count 1 |
| `spawn item <acceptedItemId> [count 1..8]` | Temporary existing world pickups; collection retains inventory invariants |
| `spawn npc <acceptedNpcPlanId> ["test name" "synthetic persona" --force]` | Temporary resident; name 1–80/persona up to 400 characters |
| `spawn character <acceptedNpcPlanId> [...]` | NPC spawning alias; preserves one authoritative player |
| `spawn creature <DOT assetId> [count 1..8] --force` | Editor catalog monster through existing encounter owner; arbitrary paths rejected |
| `spawn monster <loadedEncounterId> [count]` | Temporary approved encounter, count 1–8; choose/refresh Monsters to discover session-local IDs |
| `test snapshot` | Capture rollback buffer; a second snapshot is rejected |
| `test restore`, `test reset` | Restore captured test values; no factory reset and no save write |
| `test persist --confirm` | Deliberately publish current state to the active normal profile through its paired/checksummed commit |

## Actual IDs and examples

`Assets/GeneratedSkills/Committed/Battlefield Explorer.asset` declares skill ID `d2726112a91448f69a1b1b0eb4b889af`. This template is discoverable in Editor; opted-in builds list their accepted runtime skills and pending offers. New normally accepted offers can receive different IDs; command results print the actual accepted ID. `skill list` is the authority for the selected profile.

That asset's stale script binding was repaired to the existing `SkillData.cs` GUID; its own `.meta` GUID and skill/family IDs are unchanged. Other committed assets with unresolved script references are excluded from discovery rather than parsed as executable content.

Actual DOT catalog IDs include `cairnback_earth_unique_heavy`, `cairnback_neutral_normal_standard` and `maw_fire_unique_elder`, declared in `Assets/Assets/Resources/Player/YQDotCreatureCatalog.asset`. Example: `spawn creature cairnback_earth_unique_heavy 1 --force`. `item create weapon 5 1 --force` creates a bound level-5 mechanical test weapon. Supported kinds: weapon, offhand, head, chest, gloves, legs, boots, belt, cloak, ring, earring, necklace, trinket, consumable. NPC spawn IDs come from the selected save's accepted `generatedNpcs` records shown in Spawn; a baseline NPC record alone is not a resident template.

The baseline world content declares `npc_archivist_01` (Archivist Vey), `npc_warden_01` (Warden Thorne), `npc_cinder_01`, `npc_root_sibyl_01` and `npc_tide_cartographer_01`. These are source baseline IDs, not a claim that every generated save contains them. Use `target list` to confirm.

```text
target player
test snapshot
skill grant d2726112a91448f69a1b1b0eb4b889af 3 --force
progression set skillCandidateScore 2 --force
progression set minimumScore 0 --force
progression set minSkillEvidenceScore 0.1
stat set maxHealth 170
stat add xp 100
stat set critChance 2
target npc npc_archivist_01
stat get affinityToPlayer
stat set affinityToPlayer 0.7
test restore
```

`critChance 2` is rejected. Item instance IDs are generated GUIDs; template IDs depend on generated context. Use `item list`, then `item give <the actual itemId> 2` for a stackable item, or count 1 for a non-stackable item. The console does not invent a static item catalogue or assume an asset name is an item ID. `skill grant` also accepts an actual pending offer ID shown by `skill list`.

## What “skill tolerances” means here

There is no unified tolerance, per-skill XP or learning-rate variable in `SkillRecord`. Abilities/spells use the same accepted skill records. Ranks are distinct from spell circles (1–7); ordinary skill tiers remain distinct.

| Console setting | Actual owner/mechanic | Units/range |
|---|---|---|
| `skillCandidateScore` | `ProgressionThinkCycle` candidate selection; normally max(config score, 24) | Earned-ness score points, 0–10000 test range |
| `minimumScore` | Thinker's pre-generation consideration gate; normally max(config score, 12) | Earned-ness score points, 0–10000 test range |
| `skillCooldown` | Thinker acquisition deadlines; normally max(config cooldown, 420) | Seconds, 0–86400 test range; below floor needs force; does not clear an existing deadline |
| `repeatPenaltyPerSameVerb` | `ProgressionMath` subtracts penalties for repeated verbs | Score points per repeat, 0.05–1 |
| `varietyBonusMultiplier` | Score bonus for at least three distinct verbs | Multiplier, 0–10 test range |
| `minConfidence`, `minSkillConfidence` | `ProgressionDecisionApplier` proposal acceptance | Probabilities, 0–1 |
| `minSkillEvidenceScore` | Matching-player-behavior evidence gate | Normalized score, 0–1 |
| `duplicateSkillThreshold`, `upgradeSkillThreshold` | Similarity/upgrade matching | Similarity, 0–1 |
| `evolutionStepsRequired` | Qualifying precursor evidence | Steps, integer 1–100 test range |
| `oddityEvolutionStepsRequired` | Oddity incubation prerequisite | Steps, integer 1–100 test range |
| `requirePlayerEvidenceForSkills`, `gateSkillsToCalmLowThreat` | Evidence and calm/low-threat/incoming-target prerequisites | Boolean 0/1; disable with force |
| `maxPendingOffers` | Thinker pending-offer cap | Count, integer 1–100 test range |

Evidence scoring uses recorded action significance, repetition and variety; it is not skill XP. `dangerBonusMultiplier` exists on the balance asset but is not applied by the inspected scorer, so it is not exposed as a working learning control. Temporary score overrides lower the *effective* gate rather than changing a config value that the production floor would immediately clamp. Other balance edits use disposable runtime clones, never authored assets.

Player variables: vitality, strength, dexterity, intelligence (points 1–10000); maxHealth, maxStamina, maxMana (resource points 1–100000); attack/defense (power points 0–10000); critChance (probability 0–1); moveSpeed (meters/second 0.1–1000); level (integer 1–40); xp (current-level XP 0–1000000); currency (gold 0–1000000); rewardBudget (existing budget units 0–100000). These are console safety bounds, not new gameplay balance caps. XP addition uses `PlayerStateManager.GrantXp`; direct XP setting must remain below the current level's next-level cost. Level 40 protects the existing exponential integer XP formula from overflow. Changing attributes does not invent a new attribute-to-health formula; health/UI use the project's existing equipment-derived maximum service.

## Save and rollback boundary

The first mutation automatically captures a snapshot; `test snapshot` can capture it explicitly. Player `Save`/autosave, world `Save`, paired profile commits, profile loads/creation/deletion and canonical fixture resets reject writes/transitions while this buffer is active. Snapshotting drains the existing automatic player writer first. Startup/generation and in-flight LLM work block test mutations/restoration.

Restore repopulates the **same player document**, restores NPC affinities/timestamps, supported world container records and available runtime vitals, and removes temporary rule overrides and console-spawned test enemies/corpses. The concurrent container integration adds container snapshots to preserve the player/container item-conservation boundary. Restore retains the live scene/region/position and streamed world authority. It does not undo other combat deaths/loot visuals, travel, generated geometry, dialogue auxiliary stores, world generation or arbitrary non-console world changes. All player-state changes made during the test session are rolled back, including normal progression since the snapshot. Do not use this as a world/time-travel checkpoint. No snapshot file or normal-save overwrite occurs on restore.

`test persist --confirm` writes the active **normal** profile deliberately using `YQProfileSaveSystem.SaveActiveProfile`, including its checksum/reference validation and auxiliary documents. A failed commit retains the snapshot and barrier. Rule overrides are always temporary and reset after publishing. Quitting attempts restoration; failure keeps persistence blocked rather than silently saving test edits. Resolve the session before scene/domain reloads or deleting/disabling the console. Unexpected owner replacement fails closed rather than restoring across profiles.

Results are recorded in Unity's log and a per-session `Application.persistentDataPath/DeveloperConsole/commands-*.log`, with UTC time, command, target, result and player revision. The UI keeps a bounded searchable history. Logs remain outside normal save documents.

## Unsupported integrations and verification

NPCs have no player-style stats, abilities, XP, levels, currency or inventory in authoritative `NpcRecord`; only normalized affinity is writable. NPC spawning copies accepted resident templates, with explicitly synthetic name/persona overrides. A standalone LLM NPC/world-plan authoring transaction is not exposed. `skill xp add/set` rejects because no per-skill XP exists. Generation uses the typed proposal/acceptance pipeline. Catalog enemies are explicit mechanical tests, not newly accepted generated monster identities. Their imported-model spawner path is Editor-only; development players reject catalog creature spawning clearly rather than silently substituting a fallback. Loaded encounters retain their existing runtime representation. Quest completion uses the explicit existing test hook; no new objective or prose-based completion is introduced.

`Tools > YourQuest > Testing > Verify Developer Console Contracts` runs detached Edit Mode fixtures and writes `Docs/Developer_Console_Contract_Receipt.json`. It covers actual committed skill grant without gameplay, effective threshold overrides, stat owner notifications and the health service used by HUD, invalid values, XP level-up, accepted inventory stacking/removal/capacity, NPC target restrictions, snapshot restoration, save barriers, autocomplete/parser/audit and the build-gate matrix. Browser checks additionally cover canonical catalog IDs/search, escaped descriptions, pending structured quest acceptance/restoration, invalid spawn counts and the temporary-encounter publication barrier. It never loads/resets a real profile. A request at `Assets/Assets/EditorBuildRequests/VerifyDeveloperConsoleToolkit.request` runs this same check once after fresh successful compilation in idle Edit Mode. Freshness and compiler-error checks prevent a stale loaded assembly from producing a final PASS.

The current request marker is `Assets/Assets/EditorBuildRequests/VerifyDeveloperConsoleToolkit.request`; it runs only detached Edit Mode checks after fresh successful compilation. These fixtures do **not** establish ordinary PlaySafe startup, physical input capture, visible HUD, scene picking, successful live generation/spawning/animation, or launched-player gating. Play tests are deferred at the user's request. See `Developer_Console_Verification.md` for current compiler/fixture boundaries and queued checks.
