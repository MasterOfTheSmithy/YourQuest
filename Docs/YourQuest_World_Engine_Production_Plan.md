# YourQuest World Engine — Production Implementation Plan

Status: proposed implementation backlog, grounded in targeted source inspection on 2026-09-05. No milestone is declared complete by this document.

## 1. Purpose and authority

Make the existing tailored procedural world engine produce coherent, functional, visually intentional environments, preserve them across saves, and evolve them through supported gameplay changes. Extend existing ownership boundaries rather than introduce parallel world, NPC, dialogue, combat, or persistence managers.

Product authority remains [YourQuest Game Design Document](YourQuest_Game_Design_Document.md). Spatial architecture authority remains [YourQuest AAA Procedural World Generation Design Plan](YourQuest_AAA_Procedural_World_Generation_Design_Plan.md), which the GDD explicitly delegates to. This document translates those requirements into ordered production work based on the current implementation. Proposed type names describe contracts, not instructions to create duplicate classes. Where suitable contracts already exist, extend them.

Target fidelity means convincing composition, terrain integration, architecture, interaction, presentation, and stable performance at player height. Asset count, catalogue coverage, compilation success, and successful loading are not substitutes for that result. A representative generated region must prove the quality bar before broad content expansion.

Scope covers world intent through construction, population behavior, persistence, streaming, revisions, presentation, and validation. Preserve the authoritative player, existing combat, generated abilities, quest event contracts, inventory, and dialogue. Fix integration defects in those systems only where they block this scope.

## 2. Evidence baseline and limits

Verified in the inspected source and serialized assets:

- The runtime site catalog contains 23 maps; 22 reference streaming manifests.
- `YQProceduralSettlementLayout.TryResolve` returns success without producing a new layout for streaming manifests. This protects dependent authored fragments, but prevents their procedural rearrangement.
- The streaming-cell branch in `YQCompiledWorldSiteInstance` places cells at authored coordinates. The non-streaming zone branch has procedural placement support.
- Fresh settlement records receive population/service-driven block targets. Streaming selection instead targets semantic roles/functions plus connective cells, capped at eight, with an instance budget.
- Runtime planning uses V2Preferred and can report a playable V1 fallback when V2 preparation fails.
- Accepted bindings, seeds, and procedural layouts have persistence protections. Continue intentionally does not opt old saves into fresh-world generation policy.
- `GeneratedNpcPlanRecord` already contains stable identity, role, archetype, routine prose, tags, and merchant/guard/hostile/boss flags. `YQGeneratedNpcPlanningService` generates canonical population records. `NpcDialogueAgent` and the memory/session stores already own dialogue continuity.
- Existing site function contracts, terrain support checks, streaming, spatial validation, and world-delta application provide implementation foundations.

Not established by this inspection: the exact cause of the reported floating trees in the latest run; live V2 rejection reason; total behavior executor/animation registry coverage; performance on target hardware; visual quality of every kit; comprehensive economy/schedule simulation coverage. These are bounded verification tasks, not proof those systems are absent.

No repository-wide audit, asset regeneration, or runtime build was performed for this plan. The Unity editor log was inaccessible in the investigation. Existing docs and scripts are evidence of contracts, not evidence that every runtime path passes.

## 3. Keep, extend, add, retire, optimize

| Area | Production action | Completion evidence |
|---|---|---|
| Canon, seeds, accepted generated records | Keep; add versioned provenance where missing | Reload reproduces accepted identity and geometry without a model request |
| Existing semantic catalogs and reviewed manifests | Extend with explicit construction capability and dependency contracts | Each published assembly declares what it can safely provide |
| Extraction/compiler tools | Extend to produce independent assemblies and preserve dependent fragments | Authored roof/wall/interior relationships survive compilation |
| Procedural block/street solver | Build on; support eligible streamed assemblies and demand-driven capacity | Different briefs yield valid differences in streets, capacity, and placement |
| Streaming site materialization | Build on; consume accepted assembly transforms | Loaded geometry equals the persisted plan |
| Terrain, routes, foundations | Integrate through one terrain revision and spatial plan | Supported foundations and walkable entrance-to-road connections |
| NPC planning, identity, dialogue, memory | Keep; add structured behavior compilation through existing records | One canonical identity and one behavior owner per NPC |
| Behavior/animation definitions | Reuse existing registries; add missing typed capabilities and schedules | Unsupported actions rejected before population acceptance |
| Runtime material recovery | Keep for compatibility; remove dependence from newly released kits | New kit passes material/import checks before gameplay |
| Scatter and palette fallback paths | Retire from new-world release acceptance after replacement passes | A camp or diagnostic assembly cannot satisfy a required town |
| V1 compatibility | Keep for accepted legacy saves; label explicitly | No silent save migration or false V2 success |
| Giant runtime/compiler classes | Extract only touched cohesive responsibilities after behavior parity | No broad rewrite or changed serialized API just for organization |
| Hot-path work | Profile, then reduce repeated discovery, loading, allocations, and model requests | Captures meet declared budgets without lost functionality |

Retirement means removing a path from new production acceptance first. Delete code/assets only after serialized references, callers, old-save needs, and replacement coverage are verified. Preserve third-party sources and GUIDs; archive superseded project tools according to the existing design plan.

## 4. Shared construction and state contracts

The authoritative flow is:

Player origin/history + committed canon → structured intent → validated feasible requirements → deterministic terrain/location/route/assembly plan → functional population and dressing → quality gates → atomic persisted acceptance → streamed execution.

The compiler may revise an unaccepted proposal within bounded constraints. It may not silently change committed geography or canon to make a candidate fit. Compilation randomness uses stable subsystem seeds and stable tie-breaking; accepted LLM responses are stored, not expected to reproduce from model sampling.

Extend existing records with these concepts where missing:

| Contract | Required information | Owner |
|---|---|---|
| Construction capability | Independent assembly, dependent fragment, rigid authored site, interior graph, prop, vegetation; supported placement modes | Reviewed asset metadata |
| Assembly geometry | Stable ID/version, dependency group, footprint, foundation contact samples, clearances, entrances, allowed orientation/slope, sockets, interior linkage | Editor compiler and reviewer |
| Functional capacity | Supported services, station IDs, navigation access, occupancy/capacity, profession affordances | Existing site function contracts |
| Placement artifact | Exact asset IDs, transforms, terrain revision, streets/plots, support solution, function bindings, validation provenance | Existing spatial compiler/save |
| Population demand | Required service coverage, representational population, active actor budget, resident/work/home requirements | Existing generated location/NPC records |
| Behavior recipe | Approved behavior IDs and versions, schedule rules, validated parameters, assigned stations and animation profile IDs | Existing NPC record extension |
| Mutable runtime state | NPC current task, world time, reservations, interruption state, inventory/relationship deltas, location mutations | Existing persistent state owners |

Separate immutable construction from mutable gameplay state. A kit registry update cannot change an accepted town. An unavailable kit/version must produce a precise compatibility failure or reviewed migration, not a new random binding.

## 5. Milestone execution plan

IDs below track implementation work against the existing WG0–WG8 design; they do not restart those milestones. Every milestone begins by checking the target symbols and direct dependencies for already completed work.

### P0 — Reproducible baseline and truthful acceptance (supports WG0/WG8)

Priority: immediate. Dependencies: none.

Work:
- Capture one problematic fresh-world seed and one legacy save in isolated test copies. Record active spatial authority, compiler/catalog versions, selected kit IDs, layout mode, requested/fitted capacity, fallback reasons, and materialization errors.
- Add missing stage-level diagnostics to the existing debug/report surfaces. Correlate reports with world, location, assembly, and NPC IDs.
- Use a strict new-world evaluation mode where V1 fallback is an explicit failed V2 evaluation. Preserve normal legacy save support and provide a usable failure/retry experience rather than an indefinite loading screen.
- Declare test hardware, resolution, quality settings, render pipeline configuration, and capture route. Include a slope, a door, a road transition, trees, and a streaming boundary.
- Reproduce floating trees and flat-terrain symptoms at those locations before choosing a fix; inspect final terrain revision, imported pivot, support sampling, parent transform, and activation order.

Deliverables: baseline report, fixed-seed save copies, player-height captures, error categories, hardware/budget sheet, ordered reproducible defect list.

Exit gate: the team can explain which authority and asset path produced every tested location; fallback cannot be confused with success. Known physical defects have reproducible IDs and evidence.

### P1 — Publish real construction units (WG1/WG2)

Dependencies: P0. Select one compatible primary family using existing catalog evidence; do not assume its legacy golden map is independently composable.

Work:
- Extend reviewed metadata with the construction capabilities in section 4. A streaming container is a loading detail, not a blanket answer to whether its content is movable.
- Produce complete rigid assemblies from existing imported geometry. Retain shared walls, roofs, furnishings, colliders, doors, and interiors in declared dependency groups.
- Measure foundations, envelopes, entrances, clearance, station sockets, scale, and nav traversal. Separate an exterior street connection from an interior doorway.
- Automate measurable extraction; require visual review where topology or intended function cannot be proven. Never grant a profession/service solely from a tag or filename.
- Separate release states: discovered, extracted, reviewed, rigid-site-ready, independently-composable, functional, and family-production-ready. Reuse current status infrastructure where possible.
- Validate source materials, shaders, colliders, animator/rig needs, prefab links, and LODs. Recover correct imported materials before considering substitutes.

Deliverables: first reviewed assembly set, capability report, compiler schema/version migration, authoring feedback for missing contracts.

Exit gate: assemblies can be moved/rotated independently without structural damage; every mandatory service has at least one complete accessible provider assembly. Unsupported maps remain honestly classified.

### P2 — Close the layout-to-streaming gap (WG3/WG5)

Dependencies: P1.

Work:
- Replace the blanket streaming layout bypass with explicit capability dispatch. Preserve rigid authored groups; pass eligible assemblies to the existing solver.
- Make one accepted placement record drive footprint reservation, street generation, terrain support, entrance bindings, streaming transforms, and reload.
- Use population/service demand in the relevant selection path. Measure capacity in supported functions and spaces; do not equate one streaming chunk with one building or one resident.
- Retain budgets, but report infeasible demand. Use bounded alternatives: compatible assembly variant, plot/layout variant, or pre-acceptance brief adjustment. Do not simply raise eight-cell limits or duplicate districts.
- Ensure deterministic retries use stable IDs and do not mutate the accepted world. Record rejection and repair history.

Deliverables: end-to-end independent assembly placement, persisted transforms, capacity diagnostics, compatibility route for dependent fragments.

Exit gate: at least three seeds and two materially different population/service briefs produce valid physical variation; reload/stream out-in preserve exact accepted placement. No roofs, interiors, or references are detached.

### P3 — Terrain, water, routes, and grounding (WG4)

Dependencies: P2 for integrated proof; targeted terrain defect fixes may begin after P0.

Work:
- Extend current macro terrain contracts to provide readable landforms, water/drainage constraints, and suitable site candidates. Site selection must consider slope and travel access.
- Solve roads and entrances with grade limits and connected traversable geometry. Compile bridges, steps, or retaining structures only when reviewed compatible assemblies exist.
- Apply bounded cut/fill and foundation solutions rather than flattening entire donor-map footprints. Respect water, cliffs, cave/interior boundaries, and tile seams.
- Freeze the final terrain revision before dependent vegetation and final station/navigation checks. Any terrain revision invalidates affected dependent checks.
- Fix verified tree grounding at its owner: use reviewed root/base contacts and final surface queries; constrain species tilt; reject invalid placement rather than burying or stretching arbitrary meshes.

Deliverables: coherent terrain-to-road-to-door solution, vegetation support contract, slope/seam regression scenes, bounded repair diagnostics.

Exit gate: tested travel routes and service entrances are traversable; required foundations meet support tolerances; no floating trees or unsupported buildings in the inspected routes. Automated support tests pass the release seed set.

### P4 — Settlement grammar, interiors, and environmental composition (WG2/WG3/WG4)

Dependencies: P2/P3; assembly authoring continues incrementally.

Work:
- Express settlement reason, morphology, districts, plots, streets, public nodes, and landmarks through existing structured plan records.
- Support several real morphologies, such as a river crossing, hillside settlement, and road market, only when terrain and library capabilities support them.
- Give every required building a role, accessible entrance, valid interior connection where required, and compatible architecture. Architectural variation follows a family grammar and repetition budget.
- Dress by function and reviewed sockets: smith work yards, inn seating/storage, fishing access, gardens, fencing, drainage edges, signs, lighting, and activity traces. Narrative layers follow saved local canon.
- Define biome-aware vegetation clustering, density transitions, sightline/traversal exclusions, and road/foundation clearance. Large silhouette composition precedes microclutter.

Deliverables: generated region with several district purposes, working public interiors, meaningful approach silhouette, and coherent surrounding wilderness.

Exit gate: region reads as an intentional place from approach, street, doorway, and interior viewpoints across the seed set. Required services and quest anchors can be reached without debug movement.

### P5 — Structured NPC behavior profiles and execution (WG4 functional population)

Dependencies: P1 function/station contracts; P4 supplies final spaces. Profile schema and executor ownership checks can start during P2.

Work: implement section 6 through the existing NPC pipeline. Reuse any suitable behavior executor, movement owner, animation binder, clock, and reservation service found by targeted symbol inspection. Add missing responsibilities only when no existing owner satisfies them.

Deliverables: reviewed ScriptableObject definitions, deterministic recipe compiler, saved recipes/state, schedule/execution integration, behavior debugger, representative profession profiles.

Exit gate: smith, innkeeper, fisherman, and a nocturnal variant complete an entire world-day cycle; interruptions, station contention, blocked paths, save/load, and stream transitions behave correctly. No duplicate NPCs, dialogue identities, movement controllers, or shared-definition state mutation.

### P6 — Tailoring and gameplay integration (WG6)

Dependencies: P4/P5.

Work:
- Feed structured capability summaries into existing generation requests so intended services, habitats, professions, and location types are feasible.
- Preserve generated narrative identity while normalizing supported mechanics. Unsupported plans receive explicit constrained repair before acceptance.
- Connect player origin/history to geography constraints, threat/encounter emphasis, functions, exploration opportunities, and supported progression hooks. Respect the GDD rule that the player's response determines rewards rather than region identity alone.
- Bind existing quest objectives to stable NPC, station, item, enemy, and location IDs. Dialogue receives actual schedule/service/world state; prose never becomes behavior execution.
- Preserve one authoritative player and existing animation/combat ownership. Test first/third-person presentation, combat interruption, interaction reach, and loading-gate release in the finished region.
- Verify model unavailability does not break accepted content, movement, streaming, or behavior. Label initial scaffolding and limit generation retries.

Deliverables: two contrasting tailored briefs with traceable mechanical and physical differences, functioning tutorial-to-region progression, grounded dialogue/quest integrations.

Exit gate: differences are demonstrable beyond names, colors, or prose; every required generated objective/provider resolves and works; no repeated generation of accepted records.

### P7 — Explicit living-world revisions (WG7)

Dependencies: P6 and save/stream parity.

Work:
- Extend existing world-delta ownership with typed supported revisions. Start with service closure/reassignment and faction occupation; then add damage/repair or construction where reviewed state variants exist.
- Link each mutation to consequences: provider availability, NPC assignment/schedule, quest state, navigation, visuals, and canon. A faction number changing alone is not a physically evolved settlement.
- Stage revisions against the accepted parent revision, validate, persist transactionally, then activate safely. Detect stale proposals, protect occupied spaces/player traversal, and retain rollback data.
- Distinguish state-only changes from geometry changes. Avoid visible teleportation or destructive remodeling around the player. Persist offscreen revisions and apply them consistently on stream-in.
- Advance distant schedules/economy only to the fidelity needed for supported gameplay; avoid inventing a full economic simulation as a prerequisite.

Deliverables: revision staging/reporting and at least two end-to-end mutation examples with NPC/service consequences.

Exit gate: interrupted commit, reload, and revisiting locations preserve one coherent revision; destroyed/closed providers cannot keep serving; no duplicate rewards or entities.

### P8 — Presentation, performance, expansion, and retirement (WG8)

Dependencies: integrated P4–P7 proof. Profiling begins at P0, not here.

Work:
- Finish primary-family lighting, materials, terrain blends, decals, vegetation edges, water, ambient sound, animation transitions, collision, LODs, and culling. Use existing imported assets where suitable.
- Profile generation, activation, traversal, dense-town AI, interiors, and world revisions. Optimize measured bottlenecks in loading, object integration, navigation, draw calls, shadows, physics, allocations, and save writes.
- Use existing performance budgets as initial targets: 60 FPS/16.6 ms exploration, approximately 1–2 ms streaming main-thread work, no normal-travel activation hitch above 50 ms, and beta settlement deterministic compile target below 5 seconds excluding model latency/activation. Calibrate and lock on declared hardware; do not claim these are currently achieved.
- Expand one additional family to prove the contracts generalize without kit-name special cases. Then scale regions and library breadth.
- Retire obsolete new-generation scatter/fallback acceptance and superseded tools only after replacement coverage and legacy compatibility are proven.

Deliverables: release candidate, profiler captures, content coverage matrix, migration evidence, archived obsolete tools, known-limitations list.

Exit gate: all mandatory functional/geometry/persistence checks pass; representative human visual review passes; measured hardware budgets pass; compatibility saves pass. No claim that every catalogued family is production-ready until its own gates pass.

## 6. NPC behavior design: refine the ScriptableObject proposal

### 6.1 Ownership and data separation

The proposed ScriptableObject approach is viable as a reusable authoring and capability layer. Avoid a giant collection of independent booleans that permits contradictory combinations or requires a unique controller for every profession.

Use three layers:

1. **Reviewed definitions:** immutable ScriptableObjects for supported actions, occupation capabilities, schedule templates, and animation bindings. Generate draft definitions with editor tooling where metadata supports it; review before release.
2. **Accepted per-NPC recipe:** serializable data inside the existing canonical NPC save record. Contains approved IDs/versions, validated parameters, schedule, assignments, and deterministic seed. The generator selects semantic intent; the compiler assembles the executable recipe.
3. **Runtime state:** current activity, phase, destination/station ID, elapsed/due time, interruption/resume information, and relevant outcomes. Store through existing state ownership. Never put mutable individual state on a shared ScriptableObject.

A runtime build need not create persistent Unity .asset files per generated NPC. If a runtime ScriptableObject wrapper is useful, treat it as a transient view reconstructed from the saved recipe; JSON/record data remains authority. Preserve old NPC identities and dialogue memory when adding recipes.

### 6.2 Proposed definition contracts

| Definition concept | Fields and constraints |
|---|---|
| Activity | Stable ID/version; semantic intent; typed preconditions; required station/tool; duration range; interruption policy; completion effect IDs; compatible animation intents |
| Profession capability | Supported activities/services; required stations; permitted tools; approved substitutions; minimum service coverage |
| Schedule template | World-time windows; day/nocturnal phase; work/rest/travel/social blocks; priorities; deadlines; deterministic variation bounds |
| Animation binding | Rig/avatar compatibility; action intent; clip/controller binding; posture; tool/socket requirements; loop/entry/exit rules; root-motion policy; event contract |
| Station/affordance | Stable instance ID; activity support; access pose; occupancy/capacity; reservation lifecycle; availability; owning location |
| NPC recipe | Occupation IDs; approved activities; resolved schedule; parameter overrides; home/work bindings; behavior/animation versions; validation result |

These are concepts to map onto existing types, not a requirement to add six new managers. Yes/no inspector toggles remain useful for simple independent capabilities; use enums for exclusive choices, typed records for timed actions, and validated combinations for occupations. Normalize tags such as dayworker/nocturnal/smith/innkeeper/fisherman once into explicit contracts.

### 6.3 Generation and validation

Inputs: existing NPC descriptor, personality, location services, actual station capacities, approved behavior library, rig/animation capabilities, world clock, and canon.

Process:
- Generate or normalize occupation, active-time preference, supported activity intentions, and bounded personality parameters alongside canonical identity.
- Reject conflicting schedules, unsupported IDs, invalid timers, impossible station capacity, missing traversal, and incompatible rig/tool bindings.
- Resolve work/home/service anchors against physical location IDs. Do not accept a fisherman without reachable suitable water access or a smith without a supported work station.
- Bind animations after choosing valid gameplay actions. A hammer clip alone does not establish smithing capability; smithing also requires space, a tool, station access, timing, and service semantics.
- Allow only reviewed substitutions that preserve function. If none exists, revise the unaccepted assignment or flag the required capability missing. Do not disguise idle animation as a working mandatory profession.
- Persist the compiled recipe. Keep `dailyRoutine` for dialogue/presentation; never parse it into executable behavior.

### 6.4 Runtime execution

Use a small deterministic priority scheduler plus supported activity state machines through the existing actor controller. Avoid both a controller per occupation and an unrestricted generated behavior tree.

Typical activity lifecycle: select due activity → reserve station → navigate → align → enter action → perform timed action → apply an idempotent outcome → exit → release reservation. Each phase has timeout/cancellation behavior.

Priority policy must be explicit: death/disabled state, existing combat or threat response, active interaction, urgent obligations, schedule activity, ambient idle. Confirm interaction/combat precedence against existing controllers. One owner controls movement/rotation and animation transitions at a time.

Use an authoritative world clock; save deadlines or simulation timestamps rather than relying on a live coroutine as persistence. Provide bounded catch-up for time skips. Near NPCs execute full motion; distant NPCs advance logical schedules without full navigation or animation. Stream-in resolves a valid current location without duplicating outcomes.

Reservations are runtime leases, not permanently saved locks. Release on cancellation, death, destruction, and stream-out; reconstruct valid assignments and reacquire safely on load. Stable NPC IDs prevent double materialization across site boundaries. Async completions must validate the current identity, revision, and object lifetime.

### 6.5 First behavior proof

Mechanical example, not permanently hardcoded NPC content: a generated smith recipe binds a day-work template, a reviewed forge station, compatible hammer/tool animation, customer-service behavior, rest/home anchor, and a threat response. Personality affects bounded timing and social preference. Generated name, history, dialogue, and motivation remain accepted content.

A nocturnal variation shifts valid time windows; an innkeeper selects service activities and stations; a fisherman requires shoreline access and an approved fishing action. Reuse navigation, reservations, schedules, dialogue, and interruption handling across all profiles.

Test missing animation, missing station, two NPCs claiming one station, a closed shop, blocked path, combat interrupt, conversation interrupt, midnight wrap, long time skip, save mid-task, streaming mid-task, death, and domain reload. Outcomes must not duplicate after reload. Verify no NPC changes another NPC's shared definition.

## 7. Acceptance and validation matrix

| Gate | Automated evidence | Human/runtime evidence |
|---|---|---|
| Asset | References, source versions, capability prerequisites, dependencies, materials and bounds | Complete structural assembly, scale, function and visual review |
| Geometry | Collision/clearance, foundation support, terrain revision, seam and slope constraints | No floating, buried, detached, or visibly implausible construction |
| Traversal | Route/door/station connectivity and reachable required providers | Walk full approach-to-service route with normal player controls |
| Function | Required service capacity, unique entity bindings, quest target resolution | Use doors, interiors, services and representative objectives |
| NPC | Recipe validation, timing, interruptions, leases, idempotence | Watch work/rest/service behavior and player interruptions |
| Determinism | Stable accepted artifact hashes and stable compilation under pinned inputs | No reshuffle on Continue, registry reorder, or stream transition |
| Persistence | Save/reload parity, stale revision rejection, migration and interrupted commit tests | Revisit changed locations and confirm continuity |
| Visual composition | Repetition/density/palette constraints where measurable | Approach, skyline, street, doorway, interior, wilderness, day/night review |
| Performance | Captures and stage metrics on declared hardware | Dense traversal, streaming, combat and interactions remain responsive |

Start with three contrasting fixed seeds and controlled brief variations for integration work. Expand to a proposed 20-seed release regression set after the first-family proof; calibrate runtime cost rather than running an expensive suite for every edit. Include deliberate invalid inputs and legacy fixtures. Every spatial milestone supplies same-seed before/after captures.

Do not certify aesthetics automatically. Do not replace meaningful thresholds with universal guesses: existing support limits are starting evidence; rig/species/assembly-specific tolerances need review. Required gate failures block new-world release; optional unavailable content is explicitly reported and must not undermine required services.

## 8. Content production and staffing responsibilities

Ownership describes accountable work, not a required headcount or delegated agents:

- Unity/world engineer: contracts, solver integration, persistence, streaming, instrumentation.
- Technical environment artist: independent assembly authoring, materials, pivots, foundations, sockets, LODs, terrain transitions.
- Gameplay/AI engineer: behavior compiler/executor integration, stations, services, quest and combat boundaries.
- Designer: generated requirements, morphology, occupation rules, tailoring outcomes, canon-safe revisions.
- QA/performance owner: fixed-seed routes, save fixtures, invalid cases, profiler evidence, visual review records.

One person can perform multiple roles, but authoring/review time must be budgeted. Code cannot manufacture missing approved interiors or animation compatibility.

Use the existing design-plan family targets as planning ranges: approximately three morphologies, four to six district recipes, 20–40 reviewed parcel assemblies, six to ten landmarks, eight to fifteen transition assemblies, required public interiors, and three to five narrative state layers. These are production-family targets, not requirements before the first integration test. Begin with the minimum complete set needed for the chosen region and expand after P2/P3 pass.

## 9. Sequencing, estimation, and delivery control

Critical path: P0 → P1 → P2 → P3/P4 → P5/P6 → P7 → P8 release.

Independent work can overlap by ownership: definition schemas during assembly authoring; diagnostics and profiling throughout; presentation review during every spatial stage. Do not build population against imaginary stations or expand packs before placement parity is proven.

Effort cannot be responsibly scheduled in calendar weeks until P0 measures current failures and P1 establishes assembly authoring throughput. At those exits, estimate each backlog item in engineering days, authoring days, review/test days, and integration contingency. Distinguish finished reusable systems from draft contracts; count reviewed functional assemblies rather than raw assets. Reforecast after the first family instead of promising AAA scope from asset counts.

Every implementation ticket must name: exact symptom or capability; current owner; target symbols and direct dependencies; keep/add/change/remove decision; save/serialization risk; required content; acceptance check; rollback/migration; actual verification and remaining limitations. Add basic `// note:` comments to changed executable chunks as requested. Compile once after each coherent code change, run relevant tests, and repeat only after changes or unresolved failures justify it.

Suggested first ticket queue:
1. P0 authority/capacity/materialization report and fixed-seed reproduction.
2. P1 capability contract extension in reviewed metadata with safe legacy defaults.
3. P1 first complete independent assembly and function/entrance verification.
4. P2 persisted placement consumed by streaming, including reload parity.
5. P2 demand-aware selection and bounded infeasibility reporting.
6. P3 final-terrain grounding defect reproduction/fix and slope/road integration.
7. P5 existing behavior/animation ownership trace, then recipe schema/compiler.
8. P4/P5 functional settlement with working professions and player-height review.

## 10. Risks and controls

| Risk | Control |
|---|---|
| Streaming fragments are mistaken for independent buildings | Capability flags plus reviewed dependency groups; never just remove the bypass |
| Expanding cell budgets hides bad composition or overloads runtime | Capacity-based feasibility and measured budgets |
| Old saves are silently redesigned | Versioned accepted artifacts, legacy defaults, explicit transactional migration |
| NPC profile becomes boolean combinatorial chaos | Typed activities, exclusive enums, prerequisites and schedule validation |
| Animation availability invents invalid gameplay | Function/station first, animation compatibility second |
| Shared ScriptableObjects leak individual NPC state | Immutable definitions and separately persisted per-entity state |
| New scheduler duplicates movement, dialogue, or combat ownership | Targeted existing-owner check before implementation; one actor authority |
| LLM retries stall gameplay or alter accepted identities | Bounded generation queue, cancellation, persisted acceptance, no runtime dependence |
| Fallback conceals production failure | Explicit authority status and strict new-world test acceptance |
| Visual milestone becomes a hand-arranged showcase | Same compiler and several seeds/briefs; authored assets and recipes, generated layout |
| World mutations conflict with player location or stale saves | Parent revision check, staged validation, safe activation, rollback |
| Scope consumes effort before foundational defects are fixed | Gate order; defer new families and advanced simulation until integrated proof |

## 11. Production definition of done

The engine is ready for the declared release scope when a fresh tailored brief compiles into a coherent region with supported terrain, roads, independent construction, functional interiors, purposeful dressing, and actual NPC routines; the normal player can traverse and use it; the same accepted world survives reload and streaming; supported revisions have persistent visible and gameplay consequences; model unavailability does not break accepted gameplay; all required gates and measured hardware budgets pass.

The release manifest lists which families, morphologies, professions, mutations, and hardware settings are supported. Unknown or unsupported coverage remains explicit. The end state is one maintained world engine with reusable reviewed content and reliable contracts, not a second generator beside the current one.
