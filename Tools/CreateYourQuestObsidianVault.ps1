# note: Builds a reproducible Obsidian vault from the YourQuest design and planning authorities.
$ErrorActionPreference = 'Stop'
$root = Join-Path (Get-Location) 'YourQuest_Obsidian_Vault'
foreach ($d in @('.obsidian','Boards','Dashboards','Goals','Sources','Decisions','Templates','Tasks')) {
    New-Item -ItemType Directory -Force -Path (Join-Path $root $d) | Out-Null
}
function Write-VaultFile([string]$name, [string]$body) {
    # note: Writes one vault document and creates its parent folder.
    $path = Join-Path $root $name
    New-Item -ItemType Directory -Force -Path (Split-Path $path -Parent) | Out-Null
    Set-Content -LiteralPath $path -Value $body -Encoding utf8
}

Write-VaultFile '.obsidian/app.json' @'
{
  "alwaysUpdateLinks": true,
  "attachmentFolderPath": "Attachments",
  "newFileLocation": "folder",
  "newFileFolderPath": "Inbox",
  "showUnsupportedFiles": false,
  "strictLineBreaks": false,
  "readableLineLength": true,
  "showLineNumber": false
}
'@
Write-VaultFile '.obsidian/core-plugins.json' '["file-explorer","global-search","switcher","graph","backlink","outgoing-link","tag-pane","properties","page-preview","templates","daily-notes","bookmarks"]'
Write-VaultFile '.obsidian/community-plugins.json' '[]'

Write-VaultFile 'README.md' @'
# YourQuest project vault

This vault is the planning and evidence layer for YourQuest. It turns the game design document, open-beta plan, world-engine plan, procedural-generation plan, repair queue, asset work, and TrueVision contracts into linked goals, decisions, and implementation cards.

Start with:
- [[Boards/YourQuest Board]]
- [[Dashboards/Current Focus]]
- [[Goals/YourQuest North Star]]
- [[Goals/Phase Roadmap]]
- [[Sources/Design Authority]]
- [[Sources/Planning Index]]
- [[Decisions/DEC-001 Authority and status model]]

Use one primary status tag on every card:
status/backlog, status/specified, status/building, status/built, status/in-review, status/reviewed, status/merged, status/verified.

Type tags identify goal, task, decision, or evidence. Area, priority, and wave tags make searches and dashboards useful. Verified means current build, runtime, or artifact evidence passes. Merged means a real integration record exists.

The vault keeps endless chunk streaming, morality-driven world inversion, and additional biome expansion as separate backlog goals until the finite playable slice is coherent.
'@

Write-VaultFile 'Boards/Board Guide.md' @'
# Board guide

YourQuest Board uses the standard Kanban community-plugin format with basic settings. Without the plugin it remains readable Markdown with linked cards.

## Status definitions

- Backlog: problem or opportunity recorded, scope not ready.
- Specified: owner, boundaries, dependencies, and acceptance written.
- Building: implementation actively changing.
- Built: local implementation exists and is ready for focused review.
- In Review: runtime, asset, serialization, or design review is still required.
- Reviewed: reviewer examined it and recorded findings.
- Merged: a real merge or integration record exists.
- Verified: current build and relevant runtime or artifact evidence pass.

Keep active Building work at five cards or fewer. Never use Merged as a synonym for code exists.

## Evidence convention

Task notes hold acceptance criteria, evidence, next action, and related work. Screenshots are observations; logs, play-mode checks, serialized references, build output, and reproducible artifact checks are stronger evidence. When evidence is missing, keep the card in review.
'@

Write-VaultFile 'Boards/YourQuest Board.md' @'
---
kanban-plugin: basic
tags:
  - board/kanban
  - project/yourquest
---

# YourQuest delivery board

This board tracks the design-to-playable path. The status is evidence state, not a claim that a feature is finished.

## Backlog
- [ ] [[Tasks/TQ-020 Endless world chunk streaming]] #status/backlog #type/task #area/world-generation #priority/p2 #wave/wg5
- [ ] [[Tasks/TQ-021 Dynamic morality and faction alignment]] #status/backlog #type/task #area/progression #priority/p2 #wave/wg7
- [ ] [[Tasks/TQ-022 Multi-biome terrain and palette expansion]] #status/backlog #type/task #area/world-generation #priority/p2 #wave/p8
- [ ] [[Tasks/TQ-023 Post-beta living-world revisions]] #status/backlog #type/task #area/world-generation #priority/p3 #wave/p7

## Specified
- [ ] [[Tasks/TQ-014 Asset kit manifests and spatial records]] #status/specified #type/task #area/assets #priority/p1 #wave/wg1
- [ ] [[Tasks/TQ-015 Golden assembly library]] #status/specified #type/task #area/world-generation #priority/p1 #wave/wg2
- [ ] [[Tasks/TQ-016 World revision transaction boundary]] #status/specified #type/task #area/world-generation #priority/p1 #wave/p7
- [ ] [[Tasks/TQ-017 Behavior-driven capability paths]] #status/specified #type/task #area/combat #priority/p1 #wave/wg6

## Building
- [ ] [[Tasks/TQ-002 Structured generation and validation pipeline]] #status/building #type/task #area/world-generation #priority/p0 #wave/p0
- [ ] [[Tasks/TQ-003 Deterministic save and canon persistence]] #status/building #type/task #area/progression #priority/p0 #wave/p0
- [ ] [[Tasks/TQ-006 Deterministic settlement compiler]] #status/building #type/task #area/world-generation #priority/p1 #wave/wg3
- [ ] [[Tasks/TQ-011 Objective-record quest execution]] #status/building #type/task #area/quests #priority/p1 #wave/m5
- [ ] [[Tasks/TQ-012 NPC behavior profiles and movement]] #status/building #type/task #area/npc #priority/p1 #wave/m5
- [ ] [[Tasks/TQ-013 Dialogue memory and contextual response]] #status/building #type/task #area/dialogue #priority/p1 #wave/m5

## Built
- [ ] [[Tasks/TQ-007 V2 spatial planning foundation]] #status/built #type/task #area/world-generation #priority/p1 #wave/p2
- [ ] [[Tasks/TQ-008 Procedural site and settlement layout contracts]] #status/built #type/task #area/world-generation #priority/p1 #wave/wg2
- [ ] [[Tasks/TQ-009 Runtime world asset registry and lazy shards]] #status/built #type/task #area/assets #priority/p1 #wave/wg3
- [ ] [[Tasks/TQ-010 Paged URP conversion and material repair]] #status/built #type/task #area/assets #priority/p1 #wave/wg1

## In Review
- [ ] [[Tasks/TQ-004 Terrain, water, routes, and bridges]] #status/in-review #type/task #area/terrain #priority/p0 #wave/p3
- [ ] [[Tasks/TQ-005 Cave and interior terrain conformity]] #status/in-review #type/task #area/terrain #priority/p1 #wave/p4
- [ ] [[Tasks/TQ-018 Asset visibility, colliders, and navigation safety]] #status/in-review #type/task #area/assets #priority/p0 #wave/p4
- [ ] [[Tasks/TQ-019 One authoritative player and presentation]] #status/in-review #type/task #area/player #priority/p0 #wave/m1
- [ ] [[Tasks/TQ-024 Open-beta golden loop play-mode gate]] #status/in-review #type/goal #area/release #priority/p0 #wave/m8
- [ ] [[Tasks/TQ-025 Dialogue presentation acceptance]] #status/in-review #type/task #area/dialogue #priority/p1 #wave/m5

## Reviewed
- [ ] [[Tasks/TQ-026 TrueVision acceptance evidence and regression matrix]] #status/reviewed #type/evidence #area/release #priority/p0 #wave/wg0

## Merged
No card belongs here without a real merge or integration record.

## Verified
- [ ] [[Tasks/TQ-001 URP asset conversion audit]] #status/verified #type/evidence #area/assets #priority/p0 #wave/wg1
- [ ] [[Tasks/TQ-027 URP assets in procedural palettes]] #status/verified #type/task #area/assets #priority/p0 #wave/wg1
- [ ] [[Tasks/TQ-028 Deterministic layout math evidence]] #status/verified #type/evidence #area/world-generation #priority/p0 #wave/wg2
'@

Write-VaultFile 'Dashboards/Current Focus.md' @'
# Current focus

The finite playable slice is the current gate. Start with the In Review and Building columns on [[Boards/YourQuest Board]].

Highest-risk acceptance gaps:
- river and lake topology, banks, flow, bridges, and route agreement;
- cave entrances, terrain cuts, interiors, and water occlusion;
- grounded NPC spawning, movement loops, roof avoidance, and enemy building exclusion;
- URP materials, colliders, navigation, and asset visibility;
- contextual dialogue identity, memory, world, player, and quest facts.

Screenshots are observations. Current runtime logs and play-mode checks are the verification source.
'@

Write-VaultFile 'Goals/YourQuest North Star.md' @'
---
type: goal
status: building
tags:
  - type/goal
  - status/building
  - area/world-generation
  - priority/p0
---

# YourQuest North Star

YourQuest is a deterministic LLM-driven procedural RPG where player behavior becomes structured identity, relationships, reputation, canon, quests, abilities, items, and world change. Generated content is accepted only after validation, normalization, curation, persistence, and runtime binding.

The first product target is a coherent finite playable slice: one authoritative player, responsive local actions while model work is pending, a hub, wilderness, dungeon or interior, meaningful NPCs, structured quests, compatible assets, and reliable terrain, water, routes, bridges, and interiors. Endless streaming is a later goal built on this accepted foundation.

Preserve one authoritative player, structured records and stable IDs, deterministic replay, semantic asset binding, meaningful progression, and truthful evidence labels: VERIFIED, INFERRED, PROPOSED, UNKNOWN.

Avoid generic region-only identity, arbitrary asset paths, prose-driven quest completion, repeated regeneration of accepted canon, disabled structural collision, and model waits that freeze local play.
'@

Write-VaultFile 'Goals/Phase Roadmap.md' @'
# Phase roadmap

This reconciles the GDD, open-beta plan, world-engine plan, AAA generation plan, and TrueVision work packages.

## P0 / WG0 / M0 — Truthful baseline
Playable path, capability records, deterministic run identity, evidence receipts, and acceptance vocabulary. Compile success is not runtime acceptance.

## P1 / WG1 / M1–M3 — Assets and contracts
Paced URP conversion, source preservation, asset-kit manifests, spatial records, and semantic binding to approved prefabs.

## P2 / WG2–3 / M2–M4 — Assemblies and settlements
Deterministic parcels, sites, foundations, entrances, streets, settlement roles, and replayable golden assemblies.

## P3–4 / WG4 / M4 — Terrain, water, routes, interiors
Terrain topology, hydrology, flow, banks, bridges, caves, interiors, vegetation, and navigation must agree.

## P5–6 / WG4–6 / M5–M6 — Living population and player-specific content
Ground NPCs, behavior loops, structured identity, dialogue, quests, combat, and progression.

## P7 / WG7 — Dynamic revisions
Apply accepted world changes transactionally with revision receipts and deterministic reload behavior.

## P8 / WG5 / WG8 / M7–M8 — Streaming, performance, release
After the finite slice passes, add deterministic chunk coordinates, seam contracts, async generation, persistence, navigation continuity, and performance budgets.
'@

Write-VaultFile 'Sources/Design Authority.md' @'
# Design authority

Primary sources:
- [Game Design Document](../Docs/YourQuest_Game_Design_Document.md)
- [Open Beta Game Doc and Build Plan](../Docs/YourQuest_Open_Beta_Game_Doc_and_Build_Plan.md)

The GDD defines product intent and architecture: player-first generation, structured content, deterministic persistence, meaningful progression, curated asset binding, one authoritative player, and the world, canon, NPC, dialogue, quest, ability, item, and monster systems.

The open-beta plan defines the first delivery slice: finite starter territory, hub, wilderness, dungeon or interior, NPCs, quests, merchant, shrine, gathering, local responsiveness, and truthful acceptance evidence.
'@

Write-VaultFile 'Sources/Planning Index.md' @'
# Planning index

- [World Engine Production Plan](../Docs/YourQuest_World_Engine_Production_Plan.md)
- [AAA Procedural World Generation Design Plan](../Docs/YourQuest_AAA_Procedural_World_Generation_Design_Plan.md)
- [Asset Integration Plan](../Docs/AssetIntegrationPlan.md)
- [Active Repair Queue](../Docs/Active_Repair_Queue.md)
- [TrueVision Rework Plan](../Docs/Planning/TrueVision/REWORK_PLAN.md)
- [TrueVision Phase 0](../Docs/Planning/TrueVision/PHASE_0_WORK_PACKAGE.md)
- [TrueVision Contracts](../Docs/Planning/TrueVision/CONTRACTS.md)
- [Procedural Settlement Implementation](../Docs/Planning/TrueVision/PROCEDURAL_SETTLEMENT_IMPLEMENTATION.md)

The world-engine plan owns waves P0–P8. The AAA plan defines WG0–WG8. TrueVision defines evidence language, stable identity, typed attempts, idempotent commits, capability records, and traversable-area acceptance. The board translates those authorities into reviewable work.
'@

Write-VaultFile 'Decisions/DEC-001 Authority and status model.md' @'
---
type: decision
status: verified
tags:
  - type/decision
  - status/verified
  - area/release
---

# DEC-001 — Authority and status model

The GDD defines product intent and architecture. The open-beta plan defines the finite playable delivery sequence. The world-engine and AAA plans define generation waves. TrueVision defines evidence vocabulary and acceptance contracts. Runtime logs, serialized saves, builds, and play-mode checks are the source of truth for actual state.

Board status is evidence state: Built means local implementation exists; Merged means an actual integration record exists; Verified requires current relevant build and runtime or artifact evidence. Endless streaming, morality-driven inversion, and expanded biome libraries remain separate post-slice goals.
'@

Write-VaultFile 'Templates/Task.md' @'
---
id: TQ-XXX
type: task
status: Backlog
priority: p1
area: world-generation
wave: p3
tags:
  - type/task
  - status/backlog
  - priority/p1
  - area/world-generation
  - wave/p3
---
# TQ-XXX — Title
## Outcome
## Why
## Owner and boundaries
## Acceptance criteria
## Evidence
## Next action
## Related work
'@
Write-VaultFile 'Templates/Goal.md' @'
---
type: goal
status: Backlog
tags:
  - type/goal
  - status/backlog
---
# Goal — Title
## Intended outcome
## Scope
## Exit criteria
## Evidence
## Related work
'@
Write-VaultFile 'Templates/Decision.md' @'
---
type: decision
status: Proposed
tags:
  - type/decision
  - status/backlog
---
# DEC-XXX — Decision title
## Context
## Decision
## Consequences
## Evidence
'@

$tasks = @(
@{id='TQ-001';title='URP asset conversion audit';status='Verified';priority='p0';area='assets';wave='wg1';outcome='Identify and repair imported material conversion without freezing Unity.';why='URP compatibility is required for palette integration.';accept='5,265 variants and 572 approved prefabs checked; two unusable variants excluded; no usable catalog entries missing; editor build has zero new errors.';evidence='Conversion and palette audit evidence; one pre-existing warning is separate.';next='Keep verified and link any import regression.'}
@{id='TQ-002';title='Structured generation and validation pipeline';status='Building';priority='p0';area='world-generation';wave='p0';outcome='Keep generated records typed, validated, normalized, curated, deduplicated, and persisted.';why='The LLM is a content generator, not runtime authority.';accept='World, site, NPC, quest, asset, and capability schemas have validation gates.';evidence='GDD and TrueVision contracts.';next='Tighten the next missing validation receipt.'}
@{id='TQ-003';title='Deterministic save and canon persistence';status='Building';priority='p0';area='progression';wave='p0';outcome='Persist accepted canon and replay it deterministically across reloads.';why='Regeneration breaks continuity.';accept='Stable IDs, commit boundary, revision receipts, and reload checks exist.';evidence='GDD persistence and TrueVision commit contract.';next='Run a save and reload comparison.'}
@{id='TQ-004';title='Terrain, water, routes, and bridges';status='In Review';priority='p0';area='terrain';wave='p3';outcome='Make rivers, lakes, banks, flow, routes, and bridge variants continuous and traversable.';why='Screenshots show disconnected water edges, exposed slabs, mismatched flow, and broken bridge spans.';accept='River-to-lake joins, flow direction, levels, bridge variants, route geometry, and colliders agree.';evidence='Screenshots 207–220 and active repair notes.';next='Capture a seeded topology and bridge play-mode check.'}
@{id='TQ-005';title='Cave and interior terrain conformity';status='In Review';priority='p1';area='terrain';wave='p4';outcome='Carve cave entrances and interior floors into terrain with clearance and water occlusion.';why='Terrain cuts and water or asset bleed remain visible.';accept='Cave mouth follows terrain; floor is navigable; water is occluded; entrance assets meet clearance.';evidence='Screenshots 212–214.';next='Verify one cave and one water-adjacent interior.'}
@{id='TQ-006';title='Deterministic settlement compiler';status='Building';priority='p1';area='world-generation';wave='wg3';outcome='Compile settlements from typed parcels, streets, entrances, foundations, and palettes.';why='Procedural variety must remain coherent and replayable.';accept='Seeded settlement replay has valid layout, paths, and asset bindings.';evidence='AAA and TrueVision settlement plans.';next='Add and replay the next golden settlement fixture.'}
@{id='TQ-007';title='V2 spatial planning foundation';status='Built';priority='p1';area='world-generation';wave='p2';outcome='Provide deterministic terrain, hydrology, topology, site-network, materialization, and persistence contracts.';why='Runtime systems need one spatial source of truth.';accept='Typed planning stages compile and expose stable interfaces.';evidence='Current editor build evidence.';next='Keep interfaces stable while acceptance closes.'}
@{id='TQ-008';title='Procedural site and settlement layout contracts';status='Built';priority='p1';area='world-generation';wave='wg2';outcome='Represent site roles, providers, transforms, bounds, access, support, and palette constraints.';why='Placements must be classified rather than dumped into every pool.';accept='Generated sites carry placement and navigation metadata.';evidence='TrueVision contracts.';next='Use contracts for the next replay.'}
@{id='TQ-009';title='Runtime world asset registry and lazy shards';status='Built';priority='p1';area='assets';wave='wg3';outcome='Resolve approved assets by semantic key with lazy loading.';why='Generation must bind intents to known assets and protect performance.';accept='Registry entries are unique, categorized, URP-compatible, and loadable.';evidence='World-engine and asset plans.';next='Audit the next generated shard.'}
@{id='TQ-010';title='Paged URP conversion and material repair';status='Built';priority='p1';area='assets';wave='wg1';outcome='Convert imported assets in paced batches while preserving sources and editor responsiveness.';why='The one-time pass must not freeze Unity.';accept='Conversion is resumable, reports progress, and produces usable URP materials.';evidence='Conversion audit.';next='Record pack-specific exceptions.'}
@{id='TQ-011';title='Objective-record quest execution';status='Building';priority='p1';area='quests';wave='m5';outcome='Execute quests from stable objective records, events, and counters.';why='Prose is presentation, not gameplay authority.';accept='Talk, enter, gather, defeat, protect, craft, and discover objectives use records.';evidence='GDD quest contract and beta plan.';next='Verify a multi-step quest across reload.'}
@{id='TQ-012';title='NPC behavior profiles and movement';status='Building';priority='p1';area='npc';wave='m5';outcome='Ground NPCs correctly, avoid roofs, and provide deterministic movement loops.';why='Screenshots show roof spawns, idle NPCs, and enemy clipping.';accept='Spawn checks, nav, colliders, roles, and threat behavior agree.';evidence='Screenshots 204–206.';next='Run a settlement population check.'}
@{id='TQ-013';title='Dialogue memory and contextual response';status='Building';priority='p1';area='dialogue';wave='m5';outcome='Present dialogue grounded in identity, memory, world, player, and quest facts.';why='Dialogue still reads generic or contextually wrong.';accept='Accepted lines reference valid facts, persist memory, and pass presentation acceptance.';evidence='Dialogue repair notes and screenshots 213–220.';next='Run transcript acceptance before and after a quest event.'}
@{id='TQ-014';title='Asset kit manifests and spatial records';status='Specified';priority='p1';area='assets';wave='wg1';outcome='Describe style, role, scale, footprint, support, entrance, collider, and nav compatibility.';why='Palette assignment needs explicit classification.';accept='Manifest and spatial record fields cover generation decisions.';evidence='AAA asset-kit plan.';next='Specify bridge, hut, cave, vegetation, and interior fields.'}
@{id='TQ-015';title='Golden assembly library';status='Specified';priority='p1';area='world-generation';wave='wg2';outcome='Maintain replayable hut, bridge, road, cave, ruin, and interior assemblies.';why='Curated variety needs known-good compositions.';accept='Assemblies have placement, palette, navigation, material, and structural checks.';evidence='AAA generation gates.';next='Define intact and broken bridge fixtures.'}
@{id='TQ-016';title='World revision transaction boundary';status='Specified';priority='p1';area='world-generation';wave='p7';outcome='Apply accepted world changes transactionally with deterministic receipts.';why='Dynamic worlds need durable inspectable changes.';accept='Dependent records commit together or the prior world remains intact.';evidence='World-engine P7 and TrueVision commit contract.';next='Write the revision receipt schema.'}
@{id='TQ-017';title='Behavior-driven capability paths';status='Specified';priority='p1';area='combat';wave='wg6';outcome='Map behavior evidence to executable resource, cooldown, targeting, and status fields.';why='Generated abilities need stable runtime contracts.';accept='Capabilities are typed, validated, and bound to supported presentation families.';evidence='GDD ability contract.';next='Specify one capability record and validator.'}
@{id='TQ-018';title='Asset visibility, colliders, and navigation safety';status='In Review';priority='p0';area='assets';wave='p4';outcome='Prevent water, props, NPCs, and enemies from rendering or moving through structures.';why='Screenshots show see-through assets and collision failures.';accept='Materials, depth, colliders, nav, and overlap checks match visible geometry.';evidence='Screenshots 196–203 and 207–220.';next='Run an occlusion and collider probe.'}
@{id='TQ-019';title='One authoritative player and presentation';status='In Review';priority='p0';area='player';wave='m1';outcome='Keep gameplay state, cameras, equipment, and animation bound to one player.';why='Duplicate state makes runtime evidence unreliable.';accept='One player owns movement, combat, inventory, and save state.';evidence='GDD authoritative-player contract.';next='Verify bootstrap and reload ownership.'}
@{id='TQ-020';title='Endless world chunk streaming';status='Backlog';priority='p2';area='world-generation';wave='wg5';outcome='Stream deterministic chunks with seamless coordinates, persistence, nav continuity, and bounded background work.';why='Minecraft-like scale is a separate architecture goal.';accept='Chunk identity, seams, seed, persistence, nav, and async budgets are tested.';evidence='World-engine P8 and AAA WG5.';next='Start after the finite slice is verified.'}
@{id='TQ-021';title='Dynamic morality and faction alignment';status='Backlog';priority='p2';area='progression';wave='wg7';outcome='Make morality alter reputation, aura, faction allies or enemies, and palette compatibility.';why='The design calls for responsive good and evil alignment.';accept='Typed morality and faction records drive deterministic reactions.';evidence='User design direction and GDD identity requirements.';next='Specify morality reaction matrix.'}
@{id='TQ-022';title='Multi-biome terrain and palette expansion';status='Backlog';priority='p2';area='world-generation';wave='p8';outcome='Add forest, desert, snow, and complementary settlement and POI palettes.';why='Biomes must match terrain, vegetation, routes, and asset packs.';accept='Each biome has terrain, water, vegetation, settlement, bridge, and interior rules.';evidence='User biome direction and AAA palette constraints.';next='Define the forest baseline.'}
@{id='TQ-023';title='Post-beta living-world revisions';status='Backlog';priority='p3';area='world-generation';wave='p7';outcome='Let accepted actions revise settlements, routes, factions, resources, and narrative state.';why='A dynamic world needs durable consequences.';accept='Revisions are typed, transactional, replayable, and visible in save state.';evidence='World-engine P7 and GDD canon requirements.';next='Wait for the revision transaction boundary.'}
@{id='TQ-024';title='Open-beta golden loop play-mode gate';status='In Review';priority='p0';area='release';wave='m8';outcome='Pass hub, wilderness, dungeon, NPC, quest, merchant, shrine, gathering, and return loop.';why='The beta target is a reliable playable slice.';accept='No active compiler errors; local input responsive; state persists; dependent cards pass.';evidence='Open-beta plan and play-mode observations.';next='Run the seeded golden loop.'}
@{id='TQ-025';title='Dialogue presentation acceptance';status='In Review';priority='p1';area='dialogue';wave='m5';outcome='Make dialogue readable, contextual, non-generic, and correctly anchored.';why='Dialogue still seems off despite contextual gating.';accept='Identity, class, settlement, player, world, and quest facts are reflected and memory is consistent.';evidence='Dialogue repair work and screenshots 213–220.';next='Test three NPC contexts.'}
@{id='TQ-026';title='TrueVision acceptance evidence and regression matrix';status='Reviewed';priority='p0';area='release';wave='wg0';outcome='Keep VERIFIED, INFERRED, PROPOSED, and UNKNOWN claims separate.';why='Compile success and screenshots do not prove playability.';accept='Every gate has an observable artifact, stable identity, and reproducible result.';evidence='TrueVision rework, phase 0, and contracts.';next='Add terrain, NPC, and dialogue rows as verified.'}
@{id='TQ-027';title='URP assets in procedural palettes';status='Verified';priority='p0';area='assets';wave='wg1';outcome='Expose usable URP assets through the correct generation palettes.';why='Converted assets matter only when the binder can select them safely.';accept='Converted prefabs are classified without duplicates or obsolete references.';evidence='5,265 variants; 572 approved prefabs; two excluded; zero missing usable catalog entries.';next='Keep assignments stable.'}
@{id='TQ-028';title='Deterministic layout math evidence';status='Verified';priority='p0';area='world-generation';wave='wg2';outcome='Preserve deterministic placement math and replayable layout outputs.';why='Spatial coherence depends on repeatable transforms and bounds.';accept='Layout checks remain reproducible after reload.';evidence='TrueVision notes record 256 deterministic layout checks.';next='Use as a prerequisite for golden assemblies.'}
)

foreach ($t in $tasks) {
    # note: Converts each planning record into a linked Obsidian task note with its status and evidence.
    $statusTag = $t.status.ToLower().Replace(' ','-')
    $source = if ($t.wave -like 'wg*') { '[[Sources/Planning Index]]' } else { '[[Sources/Design Authority]]' }
    $body = @"
---
id: $($t.id)
type: task
status: $($t.status)
priority: $($t.priority)
area: $($t.area)
wave: $($t.wave)
source:
  - "$source"
tags:
  - type/task
  - status/$statusTag
  - priority/$($t.priority)
  - area/$($t.area)
  - wave/$($t.wave)
---

# $($t.id) — $($t.title)

## Outcome
$($t.outcome)

## Why
$($t.why)

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
$($t.accept)

## Evidence
$($t.evidence)

## Next action
$($t.next)

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
"@
    Write-VaultFile ("Tasks/{0} {1}.md" -f $t.id,$t.title) $body
}
Write-Host ("Created YourQuest Obsidian vault with {0} task notes." -f $tasks.Count)
