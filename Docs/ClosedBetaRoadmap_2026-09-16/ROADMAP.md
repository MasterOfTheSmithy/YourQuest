# YourQuest — authoritative closed-beta engineering roadmap

Revision: 2026-09-16. Status: planning specification, not implementation or beta certification.

This rebuild uses the complete `YourQuest_Closed_Beta_18_Goals.zip` supplied by the user, the current working tree, the Game Design Document, and the user's complete rebuilding specification. The archive is historical input to evaluate, not an instruction to execute its goals now. The vault's two loose goal files have different numbering and are not the 18-goal source. Existing source changes, assets, saves, and historical reports are preserved.

The current user request overrides the old roadmap where they conflict. The GDD remains the product authority. Its world design plan remains useful for construction contracts; this roadmap changes its LLM-first geography ordering and scopes its expansive content targets to closed beta. Accepted generated content remains save authority. Deterministic terrain is ordinary world generation, not emergency narrative fallback. Failed model work must not stop terrain or movement.

## Current execution and audit integration — 2026-09-28

**Continue [8 fix 4](<goals/8 fix 4.md>): R1 → R2 → R3.** Its original acceptance contract, including unrestricted movement, the distinct 6→260 m/s stress, visual fidelity, physical traversal and save/revisit, is unchanged. Fixes 1–3 retain historical requirements but are not concurrent execution workflows. Do not add later AI/content tasks to G08 unless a reproduced defect directly blocks an existing row.

The repository `goals/Goal-01.md`–`Goal-20.md` files are canonical; Desktop files at `C:/Users/Garri/Desktop/Game Dev/goals/` are synchronized execution copies. Initial comparison found all twenty numbered files equal. The Desktop-only G08 closeout files were preserved verbatim here. Section D and Goal-specifications.md are derived reading views, not competing editable specifications.

The [integration record](AUDIT_INTEGRATION.md) assigns every adopted audit finding to one owner and due point. It strengthens existing gates and bounded prerequisite follow-ups; it does not reset earlier goals or alter the dependency graph. Finish G08, then continue G09; close an earlier-owner follow-up only before the consumer acceptance that depends on it. G03 context/dependency checks precede G10/G12/G15/G16 generation acceptance; G09 mechanics precede G16 unlock proof. G19 integrated measurements do not become a prerequisite for basic earlier correctness.

Keep one local generative model with isolated roles and deterministic tools. Approximately 4B is a candidate, not a mandate. Retrieval starts with IDs/tags/lexical methods. Additional models/embeddings/adapters/backend changes require a measured benefit before adoption; optional unadopted experiments do not block release. Preserve accepted data and existing runtime authorities.

The [audit snapshot](LLM-Architecture-Audit_2026-09-28.md) retains its September 27–28 evidence date and all limitations. Its general next-action suggestion is sequenced by the current G08 instructions above. Section A below is the original **September 16 source snapshot**, not current runtime certification: schema/startup/save observations may have changed. Verify relevant current definitions during the owning task. No historical receipt or source-only finding establishes a fresh PASS.

## A — Current-state architecture summary

### Evidence and limits

This is a source-verified architecture audit of the working tree, including its substantial uncommitted work. No new Unity build, Play Mode run, save reset, migration, or gameplay implementation was performed for this documentation task. Historical runtime results below are attributed to the existing September 15 baseline report; they are not newly reproduced results. Source existence is not runtime acceptance.

Source paths below are relative to the project root, `C:/Users/Garri/YourQuest`. `Scripts/` abbreviates `Assets/Assets/Scripts/`. Line references identify inspected evidence at this revision and may move during implementation.

| Responsibility | Confirmed source evidence | Implication / remaining uncertainty |
|---|---|---|
| Production scene and startup | `ProjectSettings/EditorBuildSettings.asset`: enabled `Assets/Assets/Scenes/YourQuest_PlaySafe.unity` first, title environment second; asset test disabled. PlaySafe lines 125–159 serialize the runtime builder with GUID matching `Generated/YQGeneratedWorldRuntimeBuilder.cs.meta`. `Tutorial/YourQuestTutorialAutoBootstrap.cs:96–158,232–355` constructs startup, origin, gameplay and presentation services in stages. | Preserve this entry path. Do not invent a replacement test scene. Unity version is 6000.3.2f1 from ProjectVersion. |
| Player | Bootstrap `:612–718` chooses one motor; `:1155–1169` builds CharacterController, PlayerProfile, recorder, vitals, combat, motor and equipment presentation together. | `YQInvestor*` is live despite its historical name. `PlayerProfile` is attached in production, not proven dead; its mutable skill dictionaries and `UpgradeOfferManager` are compatibility risk requiring caller migration. |
| Durable state | `Data/State/Player State/PlayerState.cs:9–108` and `World State/WorldState.cs:7–80` define schema 6, player records, counters, currency, canon, NPCs and generated world plan. | Collection repair is present; version fields do not establish a complete migration framework. Species/people, continent authority, typed appearance and multi-axis alignment are not established by these inspected records. |
| Profile / saves | `Tutorial/YQProfileSaveSystem.cs:137–172,251–354` owns profile selection/copying and checks player ownership; state managers read/write shared active documents. WorldStateManager `:67–135` exposes TrySave and atomic replacement. | Per-file backups exist, but SaveProfile sequentially calls void Save APIs and copies player/world documents without a common revision commit. World preflight checks parseability/worldName, not paired profile/world revision identity. Interrupted mixed snapshots and hidden write failure are risks, not reproduced corruption claims. |
| Origin / appearance | PlayerState persists answers, generatedOrigin, pronouns/body-frame/appearance summary. ProfileSaveSystem `:516–535` commits text identity and character seed. Bootstrap stages origin services before gameplay. | Preserve guided origin and accepted packages. Text appearance summary is not proof of an asset-backed creator or appearance replay. Both origin routes need one normalized contract. |
| Spatial planning | Generated world plan stores V1 spatial data, nullable V2, generated NPCs, routes, sites and semantic chunks (`WorldState.cs:347–453`). Runtime architecture selects CompiledWorld and V2Preferred. | An accepted V2 artifact or persisted V1 is selected, not universally V2. Do not regenerate old accepted layouts or establish a second plan alongside them. |
| Continuation semantics | `GeneratedSemanticChunkRecord` and edge/portal records (`WorldState.cs:461–550`) contain seeds, feature/site IDs, terrain samples, biome weights, route/water crossings and delta references. Cell/feature authorities already exist. | Extend these boundaries; do not introduce a parallel endless-world manager. Coherent continents, destination-based road networks and watershed authority still require proof and targeted adaptation. |
| Terrain / streaming | `Generated/YQGeneratedWorldTerrain.cs:28–40,585,1718` uses Unity Terrain/TerrainData; the streamer uses 128 m continuation cells and main-thread Terrain publication. | Preserve Unity Terrain for surfaces, using local meshes for caves/overhangs. V2 sampling and the legacy 8×8 biome tile branch differ; neither proves an accepted global geography graph. Full behavioral acceptance remains unverified in this task. See E. |
| Asset pipeline | Intake catalogs, reviewed semantic manifests, site catalogs, runtime registries, V2 spatial compilers and compatibility binders exist. | Stage ownership and actually reachable approved assets need acceptance. Existing imported libraries must be reused; catalog presence is not a usable-site certificate. |
| Observation / adaptation | ActionRecorder → ActionRegistry/EventAccumulator → PlayerBehaviorRollup; bootstrap `:266–334` wires ProgressionDecisionApplier, ProgressionThinkCycle, LLMThinkCycle and YQInvestorDirector. | This is an existing adaptive foundation, not a greenfield feature. Evidence needs stable IDs, independent cursors and typed capability support before wider content. |
| LLM | `LLM/LLMClient.cs` is the queue owner; YQLlmRequest categories/options exist. | Strengthen it instead of adding a scheduler. Important queue admission can dequeue background work without its terminal callback (`:1190–1194`): audit cancellation/completion invariants. |
| Abilities / generated items | Persisted skill, item, offer and equipment records feed live combat/content services. | Verify executable capability allowlists. Item naming currently includes library/fallback construction (`GeneratedRpgContentService.cs:819–845`), not proof of the full GDD generation pipeline. |
| Quests / rewards | YQQuestCompletionDirector evaluates explicit objectives; regression fixtures test target-ID separation and partial progress. PlayerState still builds candidate objectives from supplied counter strings (`:1025–1058`) and computes quest rewards from prose keywords/current level (`:1301–1320`). | Preserve the objective evaluator. Replace prose-based reward mechanics and unvalidated objective wiring with committed typed contracts before expanding quests. |
| NPC / dialogue | WorldState holds NpcRecord and GeneratedNpcPlanRecord; runtime builder materializes plans. DialogueThinkService/NpcDialogueAgent and profile-scoped memory/session stores exist. | Loaded objects, generated plans and mutable NPC records require explicit ownership. Dialogue's constrained `action=none` path is not evidence of typed trade/quest commands. |
| Economy / normalization | PlayerState has currency/inventory; GeneratedRpgContentService baseline normalization replenishes nonpositive currency after origin (`:86–87`). | Normalization must not silently award money. Trade transactions and persisted reward receipts need a dedicated owner. |
| Tests / diagnostics | YQProductionBaselineDiagnostics, BetaDevelopmentFixture, BaselineRegression, ProductionSliceRegressionTests and specialized spatial/traversal harnesses exist. | BaselineRegression `:15–37` can log PASS outside Play Mode after deferring runtime checks. JSON round trips and snapshot checks are useful but are not clean start, quit/reload or traversal proof. |

### Existing work worth retaining

The September 15 `Docs/Production_Architecture_Beta_Baseline_2026-09-15.md` reports a playable canonical fixture (seed `76603739`, profile `beta-dev-canonical`), compiled materialization, four canonical NPCs, and zero missing-ground/route issue samples in its opening check. Its deeper failed run separately preserved 181 semantic records through save/reload/rebind. This is valuable historical evidence, not permission to certify the current tree.

That same report records streaming stress failures at 300 m/s: 2.20 cells/s forward production, diagonal minimum lead 0, readiness P95/max 6.480/6.491 s, maximum synchronous slice 0.168 s, one missed deadline; persistence commit 0.111 s against 0.100 s. Assign streaming work to G07 and growing-save performance to G19, while basic commit correctness belongs to G02. Re-measure supported player speed before treating 300 m/s as a release requirement; preserve the diagnostic stress case and explicitly report its status.

The canonical fixture precommits accepted origin and uses a development NPC fallback behavior. It cannot certify normal character creation, live model generation or the guided/custom origin experience. Production acceptance must also use ordinary fresh profiles.

### High-risk authority boundaries

1. Profile document set versus shared active copies: establish one committed revision with recoverable staging, not perpetual competing saves.
2. PlayerState versus mutable PlayerProfile; generated NPC plan versus NPC status/memory; generated factions versus attitude dictionaries: assign stable IDs and one writable owner, use compatibility views only.
3. Frozen accepted V1/V2 spatial artifacts versus new continuation algorithms: version their seam adapters; never silently reinterpret an old save.
4. LLM proposal versus content commit; overlapping progression/world/director consumers: use idempotent accepted records and independent event cursors.
5. Physical chunk lifetime versus semantic identities and mutations: unloading must never erase a named site, dead resident, opened chest or adaptive consequence.

## B — Failure analysis of the complete old roadmap

The old archive declares sequential execution but does not declare a formal prerequisite graph. Dependencies below are inferred from its required work and gates. Most cycles are acceptance/ownership cycles rather than an explicitly written graph cycle. This distinction matters: moving a gate or defining an interface often fixes the problem without rewriting the subsystem.

| Old goal | Useful purpose, responsibilities and outputs | Prerequisites / original completion gate | Misplacement, overlap, hidden dependency and disposition |
|---|---|---|---|
| 01 Production architecture/baseline | Runtime authority map, fixture, diagnostics, regression entry point consumed by all goals. | Requires source access; nevertheless gates on fresh gameplay, save/reload and passing main regression. | Certifies world/streaming before 06 and persistence before 17; origin before 03, NPCs before 07/12. Duplicate cleanup can become a migration project before 02. Keep discovery/instrumentation in G01, move repairs to named owners; final integrated pass G20. |
| 02 Canonical data/save schema | Stable species/people/location/NPC/quest IDs, versioning, validation and migration used by 03,06,07,10–13. | Needs 01 ownership; gate says no major beta system needs another identity representation. | Correct early position but universal-schema gate is unbounded. Missing explicit world coordinate/version identity, transaction boundaries and mutation receipts. G02 establishes extensible minimum contracts; domain fields added by owner under those contracts, never a competing identity. |
| 03 Character creation/origin | Asset-supported preview, guided/custom normalization, confirmation, persistent identity. | Needs 02, implicitly 04 prompt safety, 05 assets, 06 valid worlds, 08 avatar/equipment. Gate is creator→world→save/reload. | Runs before its dependencies and overlaps 14 polish; canonical species selection has a bootstrap cycle if species only exist after world generation. G10 follows foundation/world/avatar work; choose frozen supported canon registry before creator and validate against world constraints, no LLM geography prerequisite. |
| 04 LLM orchestration | One queue, priority/cancellation, bounded context, validation and proposal boundaries used everywhere generated. | Needs 02 IDs/state versions; gate claims no model subsystem can override authority. | Generic infrastructure cannot certify domain validators not built until 08–12. Origin already depended on this in 03. G03 owns transport/envelope/commit seam, each domain owns its validator; full cross-system failure suite G19/G20. |
| 05 Asset/content pipeline | Reviewed intake→catalog→runtime binding, reachability and variation outputs to world/actors. | Needs ownership and supported contracts; gate broad pool/site validation. | Mixes all-library reachability, site circulation and full-world selection before 06/07. Selection history can break exploration-order determinism. G04 certifies selected beta family and deterministic binder; G08 owns actual site traversal, G18 variation/density curation. |
| 06 Continuous semantic convergence | Macro geography, continents, cell contracts, routes/water/sites, ecology and safe traversal. | Needs IDs/assets, existing accepted spatial boundary; gate far-travel coherent RPG content and reload. | Giant catch-all; tests inhabited settlements before 07, functional RPG before 08/12, persistence optimization before 17. Route destination/site placement can become a planning cycle without reservations. Split into G05 semantic world, G06 terrain/crossings, G07 streaming/reconstruction, G08 physical sites. Semantic reservations precede roads; later buildings do not relocate geography. |
| 07 Settlement population | Persistent residents, roles, access, service/law/quest hooks. | Needs 02,06 physical places; gate inhabited persistent settlements. | Identity repeated in 12; building circulation overlaps 05/06; no distinction between implemented service and future hook. Geometry to G08, resident/status/knowledge authority G11, hooks only there; quest/economy/law behavior G12–14. |
| 08 Combat/loot/equipment/progression | Core player, enemy, reward and advancement loop; structured contextual events. | Needs assets, terrain, state; gate newcomer 30–60 minute comprehension. | Too much final UX before 14/15, quest rewards before 12 and generic adaptive recommendations without dedicated evidence/unlock work. G09 owns immediate execution and functional UI; G12 owns quest transactions; G15/G16 own adaptation; G17/G18 own comprehension and tuning. |
| 09 Economy | Currency, prices, merchants/services, validated trade and rewards. | Needs 07 residents,08 inventory; implicitly 12 quest reward authority and10 standing modifiers. | Normalizing quest rewards here can fight later quest work. G12 commits reward specifications first; G13 owns transactions/value policy and neutral standing query interface; G14 supplies modifiers later. No economy gate requires future reputation simulation. |
| 10 Standing/alignment | Separates social knowledge from moral/spiritual axes and history; outputs for merchants/dialogue/law. | Needs action evidence, identity, knowledge; gate many archetypes and reload. | Witness/propagation semantics belong to 11, dialogue/NPC knowledge to 12, visual manifestation to15. 10↔11↔12 implied ownership cycle. G11 establishes knowledge, G14 owns bounded law/standing/axes; derived archetypes are data queries, broad manifestations deferred or G18. Avoid continent-wide omniscience. |
| 11 Crime/law/rumors | Jurisdiction, actual witnesses, local response and persistent incidents. | Needs10 standing and07 residents; implicitly12 knowledge/actions. | Repeats witness propagation from10 and memory from12. Own one incident/knowledge pipeline in G14 using G11 facts; local witnessed/unwitnessed distinction is beta, global legal simulation deferred. |
| 12 NPC/dialogue/quests/reaction | Stable people, bounded memory, typed quest lifecycle and branching consequences. | Needs all earlier social/economy outputs, but supplies knowledge and reward contracts they need. | Catch-all integration hides NPC authority duplication and makes earlier acceptance depend on later work. Split resident knowledge G11 from quest/dialogue G12; later G13/G14 extend consequences through existing typed interfaces, G16 composes personalized reactions. |
| 13 Discovery/navigation | Persistent discovery and useful directions/map over unloaded territory. | Needs semantic identity, quests and streaming. Gate long exploration clarity. | Mostly well placed, but overlaps14 map/journal UI and16 pacing; lacks distinction between world truth and player knowledge. Combine discovery/UX in G17; semantic queries G05 and discovery event producer G09/G11/G12 interfaces first. |
| 14 UI/onboarding/settings | Tester-facing UI, modal safety, teaching and settings. | Needs all supported gameplay; gate unaided play across systems. | Reimplements creator UI03, input08, navigation13; early goals need functional controls long before this. G10 owns creator, domain goals minimal operable UI; G17 polishes integrated screens/onboarding/settings. Unsupported/deferred mechanics do not create empty UI requirements. |
| 15 Audio/VFX/game feel | Curated feedback, ambience, safe animation, performance. | Needs stable gameplay and asset families. | Animation correctness cannot wait until15 if combat08 is broken. G09 owns animation/execution safety; G18 owns presentation finish and budgets. Alignment spectacle is asset-conditional, not a reason to grow scope. |
| 16 Curation/balance/pacing | Real playstyles, multiple seeds, systemic start safety, pacing/variation. | Needs integrated features; gate hours of play and regression seeds. | Good late position; must not become second owner for systemic terrain/economy defects. G18 tunes accepted systems and hands root defects back. Missing explicit proof of behavior→mechanic, now G15/G16. |
| 17 Reliability/soak | Save growth, long traversal, faults, memory and resource lifecycle. | Needs completed production loops. Gate several stable hours. | Correct late endurance gate, but basic atomic saves, lifecycle and cancellation cannot wait until17. G02/G03/G07 own correctness early; G19 stress-tests and hardens their integration. Reuse evidence, do not replicate whole acceptance in every earlier goal. |
| 18 Final beta | Clean-install flow, seed matrix, migration, packaging, diagnostics, tester guide. | Needs all essential work; gate full playable beta without cheats. | Correct certification location but inherits oversized social/archetype scope and lacks a distinct adaptive proof. G20 remains sole release authority; tests the explicit retained beta scope and two meaningful behavior paths. |

Missing work now made explicit: paired save commits; world/version/coordinate identity; macro semantic ownership independent of loaded cells; supported capability/objective registries; event retention/cursors; generalized behavioral evidence; adaptive unlock acceptance; generated content provenance; feature-level overlays; final proof of accepted content replay without model regeneration.

## C — New dependency graph

Twenty goals replace eighteen. Four bounded world goals replace the old all-in-one world goal; two explicit adaptive goals close the central product gap. Consolidating related late discovery/UI and presentation/curation work, and unifying bounded social/law ownership, avoids excessive fragmentation. Character creation moves after asset/avatar/world interfaces it actually needs. No prerequisite points forward.

Numbers provide a safe sequential execution order. The table lists direct prerequisite edges; transitive prerequisites also apply. Independent branches may run in separate worktrees after their shared contracts pass. Shared-state/schema edits require one integrating owner.

| Goal | Phase | Direct prerequisites | Primary output | Direct consumers |
|---|---|---|---|---|
| G01 Production reality | Know what exists | none | authority map, truthful diagnostics, fixture, evidence ledger | G02 |
| G02 State/identity/lifecycle | Foundations | G01 | versioned identity, event/commit seams, coherent profile snapshots | G03,G04,G05,G09 |
| G03 LLM proposal boundary | Foundations | G02 | bounded scheduler, stale guards, validation/commit envelope | G10,G12,G15,G16 |
| G04 Approved asset pipeline | Foundations | G02 | versioned beta palette, asset/character capability registry | G06,G08,G09,G10 |
| G05 Semantic world | World/runtime | G02 | macro fields, feature graph, cell/edge plans, reservations | G06,G08,G11 |
| G06 Terrain and crossings | World/runtime | G04,G05 | continuous Unity Terrain, collision, water/road realization | G07,G08 |
| G07 Streaming and overlays | World/runtime | G06 | bounded lifecycle, reconstruction, mutation-safe unload | G08,G09,G11 |
| G08 Physical sites/ecology | World/runtime | G04,G05,G06,G07 | traversable settlement/site assemblies and layered dressing | G10,G11 |
| G09 Core RPG execution | Core RPG | G02,G04,G07 | single player, combat, inventory, equipment, executors, events | G10,G11,G12,G13,G15 |
| G10 Creator and origin | Core RPG | G03,G04,G08,G09 | both origin routes and asset-backed persistent character | G17 |
| G11 Residents and knowledge | Living world | G05,G07,G08,G09 | persistent NPCs/roles, faction membership, bounded knowledge | G12,G13,G14,G15 |
| G12 Quests and dialogue | Living world | G03,G09,G11 | typed objectives/rewards, memory, branching consequence seam | G13,G14,G16 |
| G13 Economy/services | Living world | G09,G11,G12 | atomic trade/services, stock/reward value policy | G14,G16 |
| G14 Social/law/relationships | Living world | G11,G12,G13 | local incidents, relationships, standing, independent alignment | G16 |
| G15 Behavior evidence | Adaptive | G03,G09,G11 | durable evidence/cursors, extensible validated candidates | G16 |
| G16 Adaptive content/evolution | Adaptive | G03,G12,G13,G14,G15 | meaningful generated unlocks and persistent world response | G17,G18 |
| G17 Discovery/onboarding/UX | Experience | G10,G16 | knowledge-aware navigation, complete usable UI/settings/tutorial | G18 |
| G18 Feedback/curation/pacing | Experience | G16,G17 | coherent audiovisual experience, balanced seed/playstyle matrix | G19 |
| G19 Reliability/endurance | Reliability | G18 | faults, budgets, migration/soak evidence on candidate build | G20 |
| G20 Closed-beta certification | Certification | G19 and acceptance receipts G01–G18 | packaged verified candidate and tester handoff | external beta, subsequent roadmap |

### Shared execution and handoff policy

Each goal below is a full binding prompt. Its short GOAL is a stable summary only. Do not launch all twenty automatically. An individual Work task receives its full goal text and the supporting roadmap (especially E/H), relevant predecessor receipts, and a project snapshot; a cloud task without this local working tree must report the missing input rather than pretend it inspected it. This redesign task does not create or launch future tasks.

Every receipt records goal/revision, inspected authorities, changed files, fixture/profile/seed, generation/schema versions, commands or steps, expected/actual result, logs/captures, evidence level, blockers and downstream owner. Use PASS, FAIL, BLOCKED, NOT YET TESTABLE per check. NOT YET TESTABLE names the missing prerequisite; a downstream failure can be acceptable diagnostic output, but failure of this goal's own acceptance cannot be called complete. A downstream defect blocks the current goal only if it prevents proving its owned work. Use existing accepted data or a clearly labeled contract fixture where sufficient; do not bypass the production bootstrap to manufacture runtime success.

Defect record: `id; discovering goal; reproduction; seed/profile/build; expected/actual; source/log evidence; severity; owning goal; blocked consumers; next acceptance test`. Repair an upstream regression in its owning scope and rerun affected consumers; do not silently redefine ownership. If a domain adds persistent fields later, it owns the versioned migration using G02's framework.

Play Mode procedure: exit Play Mode before code edits; wait for compilation/import; reset only an isolated named test profile using a validated procedure; enter the real first enabled production scene and title flow; select/create the intended profile; run the designated check; capture results; exit before further recompilation. Current fixture menu commands only work in Play Mode, so until G01 improves preparation use a separate preparation session at title, exit, then start the measured production session. Do not claim this preaccepted fixture tested ordinary origin creation. Never open a nonexistent test scene or recompile repeatedly in Play Mode.

## D — Complete rewritten goal specifications

This is a synchronized reading view of the canonical individual files in `goals/`. Execute one complete individual goal at a time. Current G08 execution is [8 fix 4](<goals/8 fix 4.md>); see the [audit integration record](AUDIT_INTEGRATION.md) for bounded follow-ups. Do not edit this compiled section independently.

# /goal — Discover, document, and instrument production reality

## 1. GOAL
Discover, document, and instrument production reality so later goals can repair the correct owners.

## 2. BINDING EXECUTION CONTRACT
The short goal is a summary only. This entire specification is binding; normalization by Work must not replace it. This goal owns discovery and trustworthy measurement, not downstream repair or closed-beta certification.

## 3. PURPOSE
Replace the old baseline trap with an evidence map and repeatable diagnostic path, reusing the September 15 baseline work.

## 4. PREREQUISITES
No prior goal. Require the actual working tree, the GDD, this roadmap and existing baseline reports. Record uncommitted source state so results identify the tested revision.

## 5. SCOPE
Runtime authority/caller map; observational diagnostics; isolated fixture preparation; test result semantics; evidence and blocker ownership.

## 6. OUT OF SCOPE / DEFERRED
State migrations G02; model repair G03; assets G04; semantic/terrain/streaming/sites G05–08; player G09; creator G10; NPC/quest/economy/social G11–14; adaptation G15–16; polish G17–18; endurance G19; certification G20. Record their failures without repairing them. Fix only instrumentation defects and compilation errors directly preventing this diagnostic work; external source compile failures are attributable BLOCKED evidence.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High owns execution. Sol High reviews only ambiguous authority or diagnostic side effects; bounded source tracing may use Astra Light. Delegate independent map/test-result checks, not subsystem repair.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Diagnostics are read-only observers. Do not delete historically named code, consolidate state owners, reset ordinary profiles, alter accepted content, or use test scenes to bypass startup. Preserve existing tests and attach evidence level to each result.

## 9. REQUIRED WORK
1. Verify the PlaySafe serialized builder, bootstrap service construction, player/state owners, V2Preferred selection, origin, population, quests, dialogue, progression and LLM paths. Document PlayerProfile/director compatibility callers and save-copy boundaries without migrating them.
2. Reuse YQProductionBaselineDiagnostics, YQBetaDevelopmentFixture, BaselineRegression and specialized tests. Add separate result records for pure contracts, live snapshots, ordinary startup, traversal and reload. Replace ambiguous aggregate PASS when runtime checks did not run.
3. Record build/source identity, schemas, seed, profile, selected spatial artifact/version, model/fallback mode, service counts and readiness. Distinguish a streamer component from accepted playable materialization.
4. Provide a reproducible fixture procedure for `beta-dev-canonical`, expected seed `76603739`; reset only that disposable profile. Current menu reset is Play Mode-only: document a preparation session or add a safe isolated editor preparation path; exit before recompilation.
5. Attempt the actual title→profile→origin→world path and the existing diagnostic traversal/reload checks once per meaningful state. Include ordinary-profile checks separately from the preaccepted fixture. Publish PASS/FAIL/BLOCKED/NOT YET TESTABLE per subsystem, exact evidence, reproducibility and later owner.
6. Freeze the initial beta hardware/measurement proposal and distinguish supported movement from 150/300 m/s stress probes. Final calibration belongs to G19; do not tune systems here.

7. Audit integration (A20): maintain the finding-to-owner ledger without resetting completed goals. The September 27–28 audit is dated source inspection, an isolated context experiment and estimates, not a current runtime verdict. Revalidate only a finding needed for the next owning repair; retain unchanged valid receipts. Record the current schema/startup/caller definitions when relevant rather than copying historical baseline values.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Prove diagnostics distinguish intentionally failing pure checks, missing prerequisites and unavailable runtime from success. Confirm observations do not create an extra player/state manager or mutate canonical saves. A blocked startup is valid evidence if logs, earliest boundary, reproduction and repair owner are recorded. Runtime access itself unavailable must be stated, not simulated.

Audit follow-up check: every adopted finding has one primary owner, evidence level, affected consumers, focused test and due point. A source-only suspicion remains unverified until reproduced; documenting it must not reopen the whole roadmap.

## 11. DELIVERABLES
Updated production map, compatibility map, fixture instructions, structured check receipts, initial budget proposal, and assigned defect ledger.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G02 receives exact state/profile/lifecycle defects. G03–G19 receive owner-specific evidence. Later tasks may trust the diagnostic procedure, not assume downstream PASS.

## 13. COMPLETION GATE
Every required production boundary is mapped or has an exact blocked observation; fixture preparation and diagnostic result semantics are verified; every observed downstream failure has evidence, reproduction and an owner. Discovery completes even with failed world/streaming/NPC/reload checks. It cannot complete with a misleading diagnostic PASS or an unassigned unknown authority.

---

# /goal — Stabilize canonical state, identity, profile commits, and service ownership

## 1. GOAL
Establish versioned identity and recoverable state commits before dependent content expands.

## 2. BINDING EXECUTION CONTRACT
The short goal summarizes this full binding specification. Own foundation correctness; do not certify all future domain behavior.

## 3. PURPOSE
Prevent new world, character and adaptive content from multiplying incompatible identities or depending on mixed player/world snapshots.

## 4. PREREQUISITES
G01 authority map, fixture procedure and state/lifecycle evidence.

## 5. SCOPE
Canonical PlayerState/WorldState ownership; profile transaction/recovery boundary; schema migration; world identity; extensible stable entity IDs, event/commit envelopes; service profile lifecycle.

## 6. OUT OF SCOPE / DEFERRED
Macro geography G05; complete creator G10; detailed NPC/economy/social mechanics G11–14; observation semantics G15; large-frontier optimization G19. Declare extensible references, not speculative full records for every future feature.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High implements and integrates. Sol High reviews paired-file commits, migration strategy, rollback and deterministic identity. Independent migration fixtures may be delegated to Luna High.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Preserve schema-6 fixtures and accepted V1/V2 artifacts. Collection defaults are not a version migration. Unknown future versions fail safely. No second state store or permanent mutable PlayerProfile mirror. Introduce compatibility readers/adapters only with an explicit retirement condition.

## 9. REQUIRED WORK
1. Define stable IDs for player/profile, world, continent/region, settlement/site, route/water feature, NPC/faction, quest, item/content, species/people and origin. Separate species from cultural people; labels never determine identity. Add reference validation for duplicates, missing parents and illegal links.
2. Establish world seed, coordinate units/negative floor convention, cell size, generation/schema/hash versions and selected spatial artifact identity. Store absolute logical position separately from render origin. Preserve legacy ID aliases; never recompute accepted identity from mutable display text.
3. Version a profile revision/manifest committing player, world and registered auxiliary documents as one coherent snapshot. Stage writes, validate ownership/checksums, publish one commit pointer, retain prior complete revision; shared active documents are a working projection. Propagate write failure, reject mixed revisions, and never import another profile's backup.
4. Add ordered idempotent migrations from supported saved versions, explicit unsupported-version handling, backups and rollback. Include provenance and immutable accepted content references; add typed mutation/transaction receipt interfaces without serializing GameObjects.
5. Add an event envelope with event ID, actor/target IDs, logical location, context, outcome and session sequence; domain producers remain later owners. Add idempotent mutation commit keys and monotonic state revision for stale result checks.
6. Make startup/profile switching/shutdown own service teardown, event subscriptions, request epoch invalidation and static reset. Migrate actually reached PlayerProfile callers to PlayerState views; preserve serialized compatibility until references are eliminated.
7. Capture the existing authoritative player's collision envelope and supported speed contract for terrain/site tests, without retuning gameplay. Remove automatic currency grants from load/normalization; preserve explicit one-time origin grants by receipt.

8. Bounded audit follow-up (A08): distinguish a successful active-document mirror write from publishing a recoverable paired profile revision. Establish and document the dirty-state publication/freshness policy through YQProfileSaveSystem/YQProfileCommitStore, including registered auxiliary state and accepted partial content. Reproduce the crash window first; do not prescribe a second save store or force a full synchronous snapshot for every action. Preserve prior accepted payloads and propagate publication failure.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Contract tests cover negative coordinates, canonical ID stability, duplicate references, supported/unsupported versions and repeat migration. Fault-inject interruption between each snapshot write/publication; recover a complete previous or new revision, never a mix. In the real title flow create/switch two isolated profiles, save/quit/reload and verify identity, accepted origin/plan and no duplicate state/player authority. World defects may remain recorded if state checks can be proved independently.

For A08, fault-inject after an accepted mutation/mirror write and before paired publication, then at each publication step. Verify the documented recoverable revision/freshness behavior, auxiliary consistency, and no acknowledged durable grant disappearing silently. G05 owns semantic-authority overlay migration; use its receipt rather than duplicate that repair here.

## 11. DELIVERABLES
Schema/ID ownership map, migration and rollback fixtures, transaction API/result contract, lifecycle tests, compatibility retirement list.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
All later domains use stable references, the same commit protocol and state revision. Domain extensions own their own migrations. G05 receives world identity; G03 receives request ownership tokens; G09/G15 receive event envelope.

## 13. COMPLETION GATE
Foundation fixtures and real profile-switch/reload checks pass; write failures are observable and recoverable; supported old records retain identity and content. No requirement for future terrain, quests, alignment or long-session performance to pass.

---

# /goal — Harden the existing LLM scheduler and proposal boundary

## 1. GOAL
Make model work bounded, cancellable, observable, and incapable of committing stale or invalid proposals.

## 2. BINDING EXECUTION CONTRACT
This full specification governs execution; the short goal is only its summary. Certify generic orchestration and representative clients, not future domain implementations.

## 3. PURPOSE
Let gameplay continue through slow, failed or unavailable inference while preserving generated content as accepted save records.

## 4. PREREQUISITES
G02 profile/world identity, state revision, lifecycle and commit interfaces.

## 5. SCOPE
LLMClient/YQLlmRequest lifecycle, categories, fairness, startup exclusivity, bounded prompt/response envelopes, generic proposal validation and terminal outcomes.

## 6. OUT OF SCOPE / DEFERRED
Terrain/geography G05–07; character normalization semantics G10; quests/dialogue knowledge G12; social law G14; behavioral eligibility G15; adaptive content G16. Expose validators for these domains without claiming their correctness.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna Max/Extra High for bounded queue debugging; Sol High only for lifecycle/state race decisions. Luna High subagent may build scheduler fault fixtures.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Strengthen the existing queue. Every admitted request ends exactly once: accepted response, invalid response, failed, cancelled, superseded or evicted. Model output is data; it cannot select arbitrary assets, execute instructions or mutate state directly.

## 9. REQUIRED WORK
1. Trace actual request callers and category/priority ownership. Fix background eviction without terminal callback; bound queue size, retries, repair attempts, response size, context and deadlines. Prevent starvation; record active-request latency separately from queued latency.
2. Bind requests to profile, world, generation epoch, owner and relevant state revision; invalidate on profile switch/unload/shutdown. Revalidate at application, including requests that completed after cancellation.
3. Keep startup exclusivity bounded and release it on all terminal paths. Optional generation must never gate ground, collision, traversal or reconstruction; no blocking wait on the render/input thread.
4. Standardize parse→schema→domain validator→normalization→curation→commit; failed validation cannot partially mutate records. Persist source, prompt/generation hash, accepted normalized payload and version. Reuse accepted content after reload.
5. Delimit untrusted origin/dialogue text, bound history, exclude private NPC facts by provider interface, and prohibit scaffold/debug internals from becoming accepted lore. Distinguish minimal offline fallback from successful normal model generation.
6. Add queue depth, age, category, latency, cancellation/eviction/failure/malformed counts and application cost diagnostics. Avoid full private transcripts in default logs.

7. Bounded audit follow-up (A03): bind proposals to the relevant entity/candidate/evidence dependencies, not every unrelated player/world change. Preserve mandatory profile/world/epoch/owner cancellation and revalidate mutable preconditions at commit. Routine movement must not invalidate an otherwise valid proposal; a changed referenced target must. Do not simply disable stale-result checks globally.
8. Bounded audit follow-ups (A06, A11): construct compact task-specific projections with explicit required facts, schema, output reservation and tokenizer-aware budgets. Optional history/retrieval may be dropped before required facts. Never silently middle-truncate an objective, ID or schema; fail/defer explicitly if the required contract cannot fit. Keep private NPC knowledge scoped by provider. Give origin/world canonical fields priority over optional presentation without adding another model call by default.
9. Consolidate one opportunity-admission/deadline policy in the existing scheduler/director/progression owners (A12). Preserve domain ownership; avoid independent timers scheduling duplicate semantic work. Transport retries, parser repair and domain repair share observable end-to-end budgets and terminal outcomes. Domain validators still own actual acceptance.
10. Extend existing diagnostics (A15) with actual input/output tokens where available, role/context size, queue versus active latency, load/cache events where exposed, stale/structural/domain rejection, transport retry/model repair/fallback, and validation/application cost. Mark unavailable metrics honestly. Establish the inexpensive baseline harness now; integrated shared-GPU comparisons belong to G19.
11. Runtime architecture policy (A19): retain one local generative model with isolated role prompts and supported schemas, deterministic tools and accepted persistent records. Approximately 4B is a benchmark candidate, not a proven minimum or permanent mandate. Start retrieval with IDs/tags/lexical queries. Extra models, embeddings, adapters, speculative decoding or backend replacement require task-specific comparative evidence before adoption; no agent swarm, parallel IR or model-dependent movement/terrain.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Run deterministic queue tests for priority, fairness, eviction callback, cancellation, timeout, malformed JSON and stale ownership. In production startup and gameplay, disconnect the configured model, switch profiles mid-request and restore service; movement and safe existing world play continue, stale responses commit nothing, queues recover. Separately record a real successful configured-model response; mocks alone do not prove integration.

Add movement-during-inference versus changed-target cases, late cancellation/profile-switch results, oversized and multilingual required-fact prompts, output/schema reservation, dialogue behind background work, fairness and total retry/repair deadline exhaustion. Reproduce the audit sentinel-loss experiment against current source before repair, then verify required content survives or the request fails explicitly. These are bounded G03 follow-ups before their dependent generated-content acceptance; they do not restart G01–G07.

## 11. DELIVERABLES
Scheduler contract, request-source map, generic content commit adapter, telemetry, fault and live-model receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G10/G12/G15/G16 receive safe request/response lifecycle and must supply domain validators. G19 receives repeatable failure injection. No promise of every future content type working yet.

## 13. COMPLETION GATE
Representative origin/dialogue/background clients obey exactly-once terminal outcomes and stale rejection; optional model failure cannot corrupt state or block movement. Domain failures beyond the generic boundary are assigned, not hidden by fallback.

---

# /goal — Certify the approved beta asset and binding pipeline

## 1. GOAL
Make a bounded approved asset family reliably selectable, compatible, and versioned for beta generation.

## 2. BINDING EXECUTION CONTRACT
This full specification is binding. The short objective does not authorize an exhaustive imported-library cleanup or certify final generated settlements.

## 3. PURPOSE
Give world and character work dependable real assets without broad package edits or placeholder substitution.

## 4. PREREQUISITES
G02 stable asset/content references, versioning and migration policy.

## 5. SCOPE
Existing intake→reviewed manifests/site catalogs→runtime registries; semantic asset selection; one primary coherent beta family plus required transition, actor, equipment, water and effect assets.

## 6. OUT OF SCOPE / DEFERRED
World distribution G05; terrain G06; final site circulation G08; equipped animation G09; creator UI G10; density/visual curation G18. Other libraries are inventoried only when needed to fill a beta coverage gap.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary. Independent selected-library validation may use Luna High subagents; Astra Light handles bounded reference comparisons. Sol High only for persisted binding migration.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Preserve GUIDs, source materials and import settings. Recover correct imported materials before generating replacements. No LLM asset paths. No runtime mutation of shared ScriptableObjects. Runtime selection must not depend on registry enumeration or exploration history.

## 9. REQUIRED WORK
1. Trace actual intake/review/compiler/registry consumers and assign one owner per stage. Generate downstream views from reviewed records; stop manually synchronizing competing catalogs.
2. Publish stable keys and manifest versions with scale, footprint, ground/pivot datum, collider, entrance, interior, navigation, camera clearance, shader/material and semantic compatibility metadata.
3. Validate missing references, slot/tag mismatch, URP compatibility and required texture types. Verify selected prefabs and approved effects visually and physically; record exclusions and repairs.
4. Define deterministic compatibility-first weighted selection. Repetition suppression must derive from stable neighborhood candidates, not whichever chunk was explored first. Freeze accepted placements/bindings and support old manifest versions.
5. Publish character/species appearance capability records using only supported rigs, heads, materials and morphs. Publish a minimal supported mechanic/animation/effect family list for G09; a listed asset does not imply an implemented mechanic.
6. Report reachable approved assets, unused/orphaned entries, selected-family coverage and missing beta functions. Preserve editor-only tooling separation.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Run registry/reference validation and deterministic selection under reordered catalogs. Materialize representative selected assets through existing production binding in PlaySafe; verify scale, material, collision and camera clearance. Site-level circulation remains G08. Compare old accepted binding replay after registry reorder or added assets.

## 11. DELIVERABLES
Versioned beta manifests, coverage/reachability report, compatibility metadata, character capability catalog, migration fixtures and visual evidence.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G06/G08 can use certified physical ingredients; G09/G10 receive truthful actor/equipment capabilities. G18 receives remaining art/density limitations.

## 13. COMPLETION GATE
Every asset function required by the declared beta family has a validated approved candidate or an explicitly scoped supported alternative; selected bindings are deterministic and old accepted bindings replay. No requirement to certify every imported pack or every final settlement.

---

# /goal — Establish the deterministic semantic world and feature graph

## 1. GOAL
Make unloaded geography and world features queryable from one versioned semantic authority.

## 2. BINDING EXECUTION CONTRACT
The full specification is authoritative. This goal establishes semantic correctness and migration, not physical terrain or complete inhabited-world acceptance.

## 3. PURPOSE
Replace cell-local feature guesses with coherent world facts while preserving accepted finite V1/V2 layouts and existing continuation contracts.

## 4. PREREQUISITES
G02 world identity, coordinates, stable feature IDs, versioning and commit protocol.

## 5. SCOPE
Macro landform/climate/ecology/culture fields; continents/regions; site reservations; destination-based routes; basin/water networks; semantic cell/edge contracts and logical spatial queries.

## 6. OUT OF SCOPE / DEFERRED
Height/collision/mesh realization G06; streaming G07; buildings/ecology instances G08; NPCs G11; names/lore enrichment G12/G16. A semantic settlement exists before residents or buildings pass their gates.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Sol High reviews hash/version invariants, old-save adapters and cross-region ownership. Luna High subagents may independently verify order invariance and edge contracts.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Adapt V2 blueprint/portal and ContinuousWorldCellAuthority contracts; no parallel world manager. Same compatible seed+coordinate+generation version gives the same baseline. Accepted exceptions and mutations are additional explicit inputs. Player history may affect committed opportunities, not retroactively move baseline geography.

## 9. REQUIRED WORK
1. Implement the E architecture with an analytic macro field and bounded region feature generation. Keep landform, climate, ecology and culture independent. Stable continents are connected macro features, not arbitrary cell labels.
2. Define owner-region/feature IDs, deterministic candidate ordering and finite query neighborhoods. Eagerly establish the opening envelope; derive untouched cells lazily; persist accepted names/layout overrides and meaningful facts; evict pure caches safely.
3. Reserve settlements and large sites with immutable footprint/entrances before connecting routes. Build roads between real destinations and permitted terminals; no empty-cell road synthesis. Resolve shared ownership for multi-cell cities/castles/dungeons.
4. Build basin/source/downstream/sink records for rivers/streams/lakes/ponds/wetlands. Share crossing contracts and persistent IDs; do not independently roll edge water.
5. Publish cell plans containing terrain/elevation intent, ruggedness, moisture, ecology, civilization/danger/culture, palette context/density masks, site/feature IDs, seeds and route/water constraints. Separate authoritative facts from derived sampled fields.
6. Freeze accepted finite V1/V2 envelopes with a versioned seam adapter. Unify decisions about accepted, continued, absent and newly synthetic features before consumers carve terrain or build meshes. Never allow edge and cell fallbacks to disagree about feature existence.
7. Evaluate the old approximately-30-chunk huge-POI idea as a seeded spacing/encounter target, with footprint exclusion, geography, access and memory budgets; do not hard-place on coordinate modulo. Record beta/deferred types.

8. Bounded audit follow-up (A10): before a relevant semantic-authority fingerprint/topology/schema migration, reproduce whether replacement preserves accepted feature overlays, tombstones, receipts and named-content identity. Treat the audit observation as a source risk until tested. If it fails, migrate a detached candidate through existing authority/version gates and retain rollback; never regenerate accepted content or mix V1/V2 authorities.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Golden contract tests over positive/negative coordinates, four directions, diagonals, far regions and permuted/parallel query order; compare canonical hashes excluding timestamps/cache lifecycle. Verify sites exist unloaded, road endpoints resolve, water descends or reaches declared basin sinks, shared edge samples/portals agree, and multi-cell features have one owner. Run a short existing production traversal after wiring query adapters; downstream rendering failures remain assigned.

For A10, use a saved modified feature and accepted named site, apply the supported authority/version transition, then compare IDs, mutation state, receipts and deterministic signatures after reload and opposite traversal orders. Verify rejection/rollback leaves the old accepted artifact intact. G07 consumes the migrated record; this does not create another streamer or save owner.

## 11. DELIVERABLES
Versioned semantic contracts/query API, compatibility adapter, deterministic fixtures, site reservations, route/water graph and ownership diagram.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G06 receives geometry constraints; G08 reserved footprints; G11 stable settlement/faction context. No consumer may independently invent features because its cell looks empty.

## 13. COMPLETION GATE
Semantic determinism, graph validity and cross-border contracts pass; accepted old layouts remain unchanged; unloaded feature lookup works. Physical beauty, infinite traversal, population and full-game reload are not this gate.

## Binding world architecture appendix (E)

### Authority and data flow

`World identity → macro fields/feature graph → semantic cell plan → terrain/content materialization → streaming lifecycle → persistent mutation overlay before gameplay publication`.

This describes dependencies, not a second runtime stack. G02 owns identity/commits; G05 owns semantic facts; G06 realizes terrain/routes/water; G07 schedules and reconstructs; G08 builds sites/environment. Later domain goals attach durable gameplay state by feature ID. Physical GameObjects never become the authoritative world.

### World identity and deterministic seeding — G02/G05

Extend the existing save-owned plan envelope with world ID/seed, generator version, semantic schema, hash algorithm version, coordinate specification, accepted spatial artifact ID/hash and asset manifest versions. Continue selecting one complete accepted V2 or persisted V1 under V2Preferred until migration explicitly retires a version. The current V2 envelope already has seed, compiler, fingerprint and validation hashes; preserve its acceptance checks.

Use signed logical cell coordinates and a double-precision absolute position or integer cell plus bounded local offset. Current continuation cells are 128 m; preserve this for old saves. Floor division must handle negative positions consistently. Rendering-origin offsets are transient; rebasing updates transforms/physics together and never changes IDs, logical positions, quest destinations or seed derivation. A floating-origin implementation is required before supported travel exceeds demonstrated precision bounds; do not add an unused spatial layer merely for neatness.

Derive independent seeds as a versioned stable hash of world seed, domain tag, owner coordinate/feature ID and generator version. Canonicalize encoding, ordering and numeric serialization. Do not use process-dependent string hashes, time, random GUIDs, mutable registry order or a shared sequential random stream for baseline generation. Existing legacy hashing remains version-pinned; replacing it requires explicit migration. Quantized semantic geometry/hashes must agree across supported builds; define a physical floating-point tolerance separately.

The seed+coordinate+version invariant applies to untouched baseline. Accepted authored/V1/V2 layouts, persisted LLM content and mutation revisions are explicit authoritative exceptions; baseline regeneration must not overwrite them.

### Spatial hierarchy and storage — G05/G07

Use only World → macro region/feature ownership → semantic/streaming cell → optional site sector. Continents and administrative/cultural regions are features with stable membership/query rules, not necessarily additional loaded grids. Reuse one 128 m cell coordinate key for semantic lookup and streaming where possible. A site sector exists only to bound a large feature's materialization; it does not own another settlement identity.

Analytically derive low-frequency elevation, land/ocean tendency, climate/moisture and broad ecological suitability. Lazily compile a bounded macro neighborhood for watersheds, sites, routes and influence. Eagerly compile the opening region and origin seam envelope. Cache immutable results with bounded eviction. Persist accepted generated names, non-reproducible choices, immutable accepted site layouts/bindings, content provenance, discovery and mutations. Do not allocate a planet-sized raster or retain every untouched visited cell forever.

A feature owner is chosen from its stable anchor and versioned owner-region grid, not first discovery. Queries include a bounded halo sufficient for crossing features. Use deterministic tie-breaks for overlap/reservations. Long rivers/roads use linked regional segments with one parent identity and deterministic border contracts; avoid recursively generating unlimited upstream regions to answer one cell.

### Macro geography and semantic plans — G05

Generate coherent landmasses/coastlines, mountain/ridge systems, valleys/plains/basins and climate/moisture trends at frequencies larger than cells. Continent membership follows stable connected macro landmasses. Watershed and civilization pressure influence habitation; cultural and kingdom influence are separately seeded fields/records, not synonyms for biome. A bounded beta need not simulate global politics or ocean travel to retain these identities.

Adapt existing V2 terrain fields, route corridors, hydrology and site anchors into one semantic query result. A cell plan contains versioned identity, owner region/continent, landform regime, elevation intent/ruggedness, temperature/moisture, ecology weights, civilization/danger/cultural influence, palette/density inputs, intersecting feature IDs, reserved sites, local seeds and edge contracts. Derived heights/vegetation samples remain cacheable outputs; accepted topology, site identity and mutation references remain durable. Plan hashes exclude visit times and loading states.

The current continuation uses `continuous_world_cell_v5`, `continuous_edge_v3` and a finite-origin blend toward seeded macro relief; this is a migration starting point, not proof of a unified world graph. Preserve old accepted terrain and introduce a versioned transition collar. Resolve accepted/continued/absent/synthetic feature policy once, then give all consumers the same result. Current per-edge and per-cell fallback differences must not survive as competing ownership policies.

### Terrain and collision — G06

Retain Unity Terrain/TerrainData for the surface. Current origin terrain uses a 1024 m domain, 140 m height range and 513 samples; continuation sampling derives from origin spacing. Accepted V2 sampling bypasses the legacy 8×8 tile-profile height branch. Keep both versioned for compatible saves; do not blend their ownership accidentally.

Sample absolute coordinates through macro elevation → regional shape/ridges → valleys/basins → hydrology cuts → local detail → road grading → reserved site accommodation. Water, roads and sites consume the same constraints; bounded earthworks cannot invalidate basin flow or a neighboring edge. Shared edge positions produce identical sample values; compare normals, paint and collision as well as height. Use local approved meshes for caves, bridges and overhangs, with explicit entrances/holes and collision transitions. Avoid runtime erosion or global fluid simulation.

### Hydrology and routes — G05 topology, G06 realization

Hydrology records own basin, source, directed flow, confluence and sink IDs. Rivers/streams are linked cross-cell features; lakes/ponds are basin polygons/surfaces and marshes/wetlands ecological/wetness regions. Each crossing records parent water ID, position/tangent, width, bed/surface elevation, flow and upstream/downstream links. Terrain carving, water meshes, shore dressing and crossing validation consume this one contract. Preserve legitimate terminal behavior; lakes/wetlands/coasts are not blindly extended as rivers. Verify carving idempotence and no double application of accepted water.

Reserve plausible settlement/POI destinations first, then route a deterministic regional site graph using slope, water and cultural/transport constraints. Roads have real endpoints or declared boundary continuations, parent route IDs, regional segments and shared edge entrances/exits. Realize local geometry within the corridor and adapt terrain. Bridges/fords belong to the route-water intersection contract. Queries can describe destinations before their GameObjects load. Empty wilderness is valid; it does not automatically request a synthetic road.

### Sites, settlements and large features — G05/G08/G11

Support semantic villages/towns/cities, castles/forts, ruins, caves/dungeons, shrines, hostile/resource sites, natural/cultural landmarks, roadside structures and empty wilderness. Beta physical coverage is deliberately narrower and declared in G08. Each site reservation owns a stable ID, anchor, bounds/member cells, route entrances, exclusions, cultural intent and allowed expansion envelope. One immutable accepted layout references asset IDs/versions, transforms, structure/interior graph and gameplay anchors. Runtime sectors may unload independently, but one resident/loot/service owner exists per stable entity.

For multi-cell cities/castles/dungeons, compile the shared footprint and entrances before any member cell publishes structures. Persist chosen layout/bindings rather than rerolling from a changed palette. Current sparse GeneratedSemanticSiteRecord lacks these fields; migrate it. Current streamer retention based only on persistentDeltaIds is insufficient for accepted named sites.

Treat approximately one huge POI per 30 chunks as a provisional player-travel pacing target, not modulo placement. At 128 m cells, 30 traversed cells suggests roughly 3.84 km along a route, not a universal area density. Use seeded regional candidates, minimum-distance/exclusion rules, geography, access and performance envelopes. G18 tests travel time/quiet intervals; G08 proves one representative multi-cell feature. A full catalog of giant cities and dungeons is post-beta.

### Palettes and environmental density — G04/G08/G18

Select from approved versioned asset families by semantic compatibility, then deterministic variation. Separate landform, ecology, climate and culture. Neighbor-aware weights allow ecological transitions without forcing identical architecture. Stable neighborhood selection handles repetition; mutable recent-use history must not change baseline when loading order changes.

Layer canopy, understory, shrubs, ground vegetation, rock/boulders, deadfall/debris, shoreline, roadside, outskirts and storytelling. Density responds to moisture/slope/land use and budgets, not tiny fixed per-cell quotas. Site/route/water/camera masks constrain placement. Validate pivot/scale/grounding, colliders, materials, clipping, culling and sightlines. Preserve quiet space and readable routes. G18 tunes density only after G08 proves safe placement.

### Materialization, streaming and reconstruction — G07

The existing builder/streamer remains coordinator. Work progresses through planned → queued → generating → collision-ready → critical-content-ready → active → retained → unloading/unloaded; failures and cancellations are explicit. Plan/ground/collision precede route/site safety, essential gameplay providers, environment and decoration. Optional model work never gates these stages.

Lookahead depends on speed/direction, measured readiness latency and safety margin; diagonal travel/reversal have explicit coverage tests. Active/retained/predictive rings, queue limits and per-stage budgets are measured on declared hardware. Immutable numeric preparation may be background work; Unity objects and publication remain on the main thread. Epoch/profile/world/revision tokens reject stale results. Unload destroys only owned objects and releases TerrainData/material/mesh resources without discarding semantic truth.

Reconstruct pinned deterministic baseline and accepted artifacts; resolve stable feature bindings; load overlay revision; apply tombstones/state/added-feature records idempotently; publish collision and gameplay only when valid. Overlays cover destroyed/harvested resources, opened containers, NPC status, quest/settlement consequences and spawned accepted features. Player construction/terrain editing remains unsupported unless an explicit retained mechanic registers a typed mutation; do not imply arbitrary building is implemented.

Use G02's coherent snapshot manifest for player/world and auxiliary memory. Index mutations by feature and owner region. Compact replay history only while preserving current state, meaningful canon and needed audit/receipt IDs. G19 determines whether dirty shards/journals are justified; do not serialize every generated physical object or add a competing save store.

### LLM boundaries and verification — G03/G05–08/G16/G19/G20

Deterministic geography establishes facts first. The LLM names/interprets/enriches those facts and proposes player-responsive quests, identities, culture/lore, dialogue and progression through validators. It cannot choose arbitrary coordinates/assets, modify accepted spatial facts implicitly, or delay ground/collision/streaming. Preserve accepted content without regeneration; new narrative revisions are explicit commits.

Determinism tests compare semantic hashes, edges, graph IDs, accepted layouts and overlays across repeated runs, shuffled exploration, negative/diagonal coordinates, reload and compatible versions. Terrain comparisons use declared sample tolerances; load timestamps are excluded. Traverse actual player physics through origin boundaries, roads/water crossings, steep/quiet/dense cells, reversal and multi-cell sites. Report fall-safety intervention separately; teleport recovery is not proof of successful traversal. Each world goal owns bounded acceptance; G19 adds endurance and G20 certifies the complete normal path.

---

# /goal — Realize continuous terrain, hydrology, and connected routes

## 1. GOAL
Materialize the semantic world as continuous, collision-safe terrain with consistent waterways and routes.

## 2. BINDING EXECUTION CONTRACT
The complete specification governs execution. Own physical surface/feature correctness; continuous-stream scheduling and inhabited sites are later owners.

## 3. PURPOSE
Make terrain, collision, water, roads and semantic crossings agree before richer content uses them.

## 4. PREREQUISITES
G04 approved surface/water/road/bridge assets; G05 world fields, routes, water topology, footprints and shared edges; transitive G02 player clearance contract.

## 5. SCOPE
Existing Unity Terrain/TerrainData path, deterministic samples, grading/masks, road/water realization, collision and constrained local meshes for caves/overhang approaches.

## 6. OUT OF SCOPE / DEFERRED
Queue/predictive throughput G07; complete settlements/interiors G08; combat tuning G09; final density/aesthetics G18; hours-long traversal G19. Do not replace Terrain with a new terrain engine.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Sol High reviews seam/version changes; Luna Max/Extra High handles bounded sampling/collision problems. Independent hydrology/route fixture verification may be delegated.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
One absolute-coordinate surface query serves terrain, grounding, routes, water and validation. Unity object creation/publication remains on the main thread. Pure numeric sampling may run off-thread from immutable inputs. Version every changed baseline algorithm.

## 9. REQUIRED WORK
1. Adapt current V2 terrain sampling and continuation to macro elevation→regional ridges/valleys/basins→hydrology carve→bounded local detail→road/site accommodation. Avoid expensive runtime erosion.
2. Evaluate shared border samples from identical logical positions; stitch Terrain neighbors and compare height, slope, normals, paint and collision. Prevent finite-water double carving and contradictory accepted/synthetic fallbacks.
3. Realize rivers/streams with stable bed/surface/flow, basin lakes/ponds and wetland masks; honor sources, confluences and sinks. Existing lake/wetland exclusions must not become spurious river terminals.
4. Realize route centerlines and grade within reserved corridors, with passable bridges/fords and stable arrival positions. Route widths/slopes/clearances use the player's declared capsule; no decorative bridge that lacks safe collision.
5. Reserve site pads/entrances and exclusion masks before dressing. Bound earthworks; reject impossible sites back to deterministic semantic planning with a recorded revision rather than coordinate hacks.
6. Publish separate terrain-sampled, terrain-created and collision-ready states. Terrain presence alone is not safe player release. Preserve accepted artifacts and compatibility fixtures.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Compare repeated/reordered sample hashes and border values on four seeds, negative/diagonal edges, steep slopes, water confluences/sinks and finite-origin transitions. Walk the authoritative player across representative roads, bridges, banks and reserved entrances in the actual production scene with existing bounded loading. Record zero unsupported ground/contact gaps in the owned test envelope; cosmetic site/NPC failures are downstream.

## 11. DELIVERABLES
Unified sampling/realization contracts, Terrain adapters, road/water crossing implementations, readiness states and seam/collision receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G07 schedules a known-safe collision-first payload; G08 receives terrain/exclusion/entrance contracts. G18 may tune appearance without changing semantic feature ownership.

## 13. COMPLETION GATE
Declared terrain/road/water fixtures are deterministic, continuous and traversable with real collision. No requirement for sustained far-travel throughput, full settlement services or final world polish.

## Binding world architecture appendix (E)

### Authority and data flow

`World identity → macro fields/feature graph → semantic cell plan → terrain/content materialization → streaming lifecycle → persistent mutation overlay before gameplay publication`.

This describes dependencies, not a second runtime stack. G02 owns identity/commits; G05 owns semantic facts; G06 realizes terrain/routes/water; G07 schedules and reconstructs; G08 builds sites/environment. Later domain goals attach durable gameplay state by feature ID. Physical GameObjects never become the authoritative world.

### World identity and deterministic seeding — G02/G05

Extend the existing save-owned plan envelope with world ID/seed, generator version, semantic schema, hash algorithm version, coordinate specification, accepted spatial artifact ID/hash and asset manifest versions. Continue selecting one complete accepted V2 or persisted V1 under V2Preferred until migration explicitly retires a version. The current V2 envelope already has seed, compiler, fingerprint and validation hashes; preserve its acceptance checks.

Use signed logical cell coordinates and a double-precision absolute position or integer cell plus bounded local offset. Current continuation cells are 128 m; preserve this for old saves. Floor division must handle negative positions consistently. Rendering-origin offsets are transient; rebasing updates transforms/physics together and never changes IDs, logical positions, quest destinations or seed derivation. A floating-origin implementation is required before supported travel exceeds demonstrated precision bounds; do not add an unused spatial layer merely for neatness.

Derive independent seeds as a versioned stable hash of world seed, domain tag, owner coordinate/feature ID and generator version. Canonicalize encoding, ordering and numeric serialization. Do not use process-dependent string hashes, time, random GUIDs, mutable registry order or a shared sequential random stream for baseline generation. Existing legacy hashing remains version-pinned; replacing it requires explicit migration. Quantized semantic geometry/hashes must agree across supported builds; define a physical floating-point tolerance separately.

The seed+coordinate+version invariant applies to untouched baseline. Accepted authored/V1/V2 layouts, persisted LLM content and mutation revisions are explicit authoritative exceptions; baseline regeneration must not overwrite them.

### Spatial hierarchy and storage — G05/G07

Use only World → macro region/feature ownership → semantic/streaming cell → optional site sector. Continents and administrative/cultural regions are features with stable membership/query rules, not necessarily additional loaded grids. Reuse one 128 m cell coordinate key for semantic lookup and streaming where possible. A site sector exists only to bound a large feature's materialization; it does not own another settlement identity.

Analytically derive low-frequency elevation, land/ocean tendency, climate/moisture and broad ecological suitability. Lazily compile a bounded macro neighborhood for watersheds, sites, routes and influence. Eagerly compile the opening region and origin seam envelope. Cache immutable results with bounded eviction. Persist accepted generated names, non-reproducible choices, immutable accepted site layouts/bindings, content provenance, discovery and mutations. Do not allocate a planet-sized raster or retain every untouched visited cell forever.

A feature owner is chosen from its stable anchor and versioned owner-region grid, not first discovery. Queries include a bounded halo sufficient for crossing features. Use deterministic tie-breaks for overlap/reservations. Long rivers/roads use linked regional segments with one parent identity and deterministic border contracts; avoid recursively generating unlimited upstream regions to answer one cell.

### Macro geography and semantic plans — G05

Generate coherent landmasses/coastlines, mountain/ridge systems, valleys/plains/basins and climate/moisture trends at frequencies larger than cells. Continent membership follows stable connected macro landmasses. Watershed and civilization pressure influence habitation; cultural and kingdom influence are separately seeded fields/records, not synonyms for biome. A bounded beta need not simulate global politics or ocean travel to retain these identities.

Adapt existing V2 terrain fields, route corridors, hydrology and site anchors into one semantic query result. A cell plan contains versioned identity, owner region/continent, landform regime, elevation intent/ruggedness, temperature/moisture, ecology weights, civilization/danger/cultural influence, palette/density inputs, intersecting feature IDs, reserved sites, local seeds and edge contracts. Derived heights/vegetation samples remain cacheable outputs; accepted topology, site identity and mutation references remain durable. Plan hashes exclude visit times and loading states.

The current continuation uses `continuous_world_cell_v5`, `continuous_edge_v3` and a finite-origin blend toward seeded macro relief; this is a migration starting point, not proof of a unified world graph. Preserve old accepted terrain and introduce a versioned transition collar. Resolve accepted/continued/absent/synthetic feature policy once, then give all consumers the same result. Current per-edge and per-cell fallback differences must not survive as competing ownership policies.

### Terrain and collision — G06

Retain Unity Terrain/TerrainData for the surface. Current origin terrain uses a 1024 m domain, 140 m height range and 513 samples; continuation sampling derives from origin spacing. Accepted V2 sampling bypasses the legacy 8×8 tile-profile height branch. Keep both versioned for compatible saves; do not blend their ownership accidentally.

Sample absolute coordinates through macro elevation → regional shape/ridges → valleys/basins → hydrology cuts → local detail → road grading → reserved site accommodation. Water, roads and sites consume the same constraints; bounded earthworks cannot invalidate basin flow or a neighboring edge. Shared edge positions produce identical sample values; compare normals, paint and collision as well as height. Use local approved meshes for caves, bridges and overhangs, with explicit entrances/holes and collision transitions. Avoid runtime erosion or global fluid simulation.

### Hydrology and routes — G05 topology, G06 realization

Hydrology records own basin, source, directed flow, confluence and sink IDs. Rivers/streams are linked cross-cell features; lakes/ponds are basin polygons/surfaces and marshes/wetlands ecological/wetness regions. Each crossing records parent water ID, position/tangent, width, bed/surface elevation, flow and upstream/downstream links. Terrain carving, water meshes, shore dressing and crossing validation consume this one contract. Preserve legitimate terminal behavior; lakes/wetlands/coasts are not blindly extended as rivers. Verify carving idempotence and no double application of accepted water.

Reserve plausible settlement/POI destinations first, then route a deterministic regional site graph using slope, water and cultural/transport constraints. Roads have real endpoints or declared boundary continuations, parent route IDs, regional segments and shared edge entrances/exits. Realize local geometry within the corridor and adapt terrain. Bridges/fords belong to the route-water intersection contract. Queries can describe destinations before their GameObjects load. Empty wilderness is valid; it does not automatically request a synthetic road.

### Sites, settlements and large features — G05/G08/G11

Support semantic villages/towns/cities, castles/forts, ruins, caves/dungeons, shrines, hostile/resource sites, natural/cultural landmarks, roadside structures and empty wilderness. Beta physical coverage is deliberately narrower and declared in G08. Each site reservation owns a stable ID, anchor, bounds/member cells, route entrances, exclusions, cultural intent and allowed expansion envelope. One immutable accepted layout references asset IDs/versions, transforms, structure/interior graph and gameplay anchors. Runtime sectors may unload independently, but one resident/loot/service owner exists per stable entity.

For multi-cell cities/castles/dungeons, compile the shared footprint and entrances before any member cell publishes structures. Persist chosen layout/bindings rather than rerolling from a changed palette. Current sparse GeneratedSemanticSiteRecord lacks these fields; migrate it. Current streamer retention based only on persistentDeltaIds is insufficient for accepted named sites.

Treat approximately one huge POI per 30 chunks as a provisional player-travel pacing target, not modulo placement. At 128 m cells, 30 traversed cells suggests roughly 3.84 km along a route, not a universal area density. Use seeded regional candidates, minimum-distance/exclusion rules, geography, access and performance envelopes. G18 tests travel time/quiet intervals; G08 proves one representative multi-cell feature. A full catalog of giant cities and dungeons is post-beta.

### Palettes and environmental density — G04/G08/G18

Select from approved versioned asset families by semantic compatibility, then deterministic variation. Separate landform, ecology, climate and culture. Neighbor-aware weights allow ecological transitions without forcing identical architecture. Stable neighborhood selection handles repetition; mutable recent-use history must not change baseline when loading order changes.

Layer canopy, understory, shrubs, ground vegetation, rock/boulders, deadfall/debris, shoreline, roadside, outskirts and storytelling. Density responds to moisture/slope/land use and budgets, not tiny fixed per-cell quotas. Site/route/water/camera masks constrain placement. Validate pivot/scale/grounding, colliders, materials, clipping, culling and sightlines. Preserve quiet space and readable routes. G18 tunes density only after G08 proves safe placement.

### Materialization, streaming and reconstruction — G07

The existing builder/streamer remains coordinator. Work progresses through planned → queued → generating → collision-ready → critical-content-ready → active → retained → unloading/unloaded; failures and cancellations are explicit. Plan/ground/collision precede route/site safety, essential gameplay providers, environment and decoration. Optional model work never gates these stages.

Lookahead depends on speed/direction, measured readiness latency and safety margin; diagonal travel/reversal have explicit coverage tests. Active/retained/predictive rings, queue limits and per-stage budgets are measured on declared hardware. Immutable numeric preparation may be background work; Unity objects and publication remain on the main thread. Epoch/profile/world/revision tokens reject stale results. Unload destroys only owned objects and releases TerrainData/material/mesh resources without discarding semantic truth.

Reconstruct pinned deterministic baseline and accepted artifacts; resolve stable feature bindings; load overlay revision; apply tombstones/state/added-feature records idempotently; publish collision and gameplay only when valid. Overlays cover destroyed/harvested resources, opened containers, NPC status, quest/settlement consequences and spawned accepted features. Player construction/terrain editing remains unsupported unless an explicit retained mechanic registers a typed mutation; do not imply arbitrary building is implemented.

Use G02's coherent snapshot manifest for player/world and auxiliary memory. Index mutations by feature and owner region. Compact replay history only while preserving current state, meaningful canon and needed audit/receipt IDs. G19 determines whether dirty shards/journals are justified; do not serialize every generated physical object or add a competing save store.

### LLM boundaries and verification — G03/G05–08/G16/G19/G20

Deterministic geography establishes facts first. The LLM names/interprets/enriches those facts and proposes player-responsive quests, identities, culture/lore, dialogue and progression through validators. It cannot choose arbitrary coordinates/assets, modify accepted spatial facts implicitly, or delay ground/collision/streaming. Preserve accepted content without regeneration; new narrative revisions are explicit commits.

Determinism tests compare semantic hashes, edges, graph IDs, accepted layouts and overlays across repeated runs, shuffled exploration, negative/diagonal coordinates, reload and compatible versions. Terrain comparisons use declared sample tolerances; load timestamps are excluded. Traverse actual player physics through origin boundaries, roads/water crossings, steep/quiet/dense cells, reversal and multi-cell sites. Report fall-safety intervention separately; teleport recovery is not proof of successful traversal. Each world goal owns bounded acceptance; G19 adds endurance and G20 certifies the complete normal path.

---

# /goal — Stabilize streaming, reconstruction, and persistent mutation overlays

## 1. GOAL
Keep traversable ground ahead of the player while safely unloading and reconstructing changed world features.

## 2. BINDING EXECUTION CONTRACT
This complete specification is binding. Own normal-play streaming and feature reconstruction, not the final multi-hour full-game soak.

## 3. PURPOSE
Extend the existing streamer so physical lifetime cannot erase persistent identity and decoration cannot outrank safety.

## 4. PREREQUISITES
G06 deterministic terrain/collision/route payload; transitive G05 feature authority and G02 commit/mutation contracts.

## 5. SCOPE
YQPlayerFollowingSemanticChunkStreamer lifecycle, priority/lookahead, cancellation, epochs, budgets, unload, caches, accepted placement retention and generic feature overlay application.

## 6. OUT OF SCOPE / DEFERRED
New settlement grammar G08; NPC state semantics G11; quest/economy/social mutations G12–14; performance of the complete growing save and long soak G19. Domain owners register their payloads with this overlay, not a second persistence system.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary. Sol High reviews stale-work publication, ownership and reconstruction invariants. Luna Max/Extra High for difficult queue/lifecycle debugging; delegate reproducible traversal matrices only.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
One streamer and materialization coordinator. Token identity includes profile/world/generation epoch and feature revision. An unavailable model never delays collision. Physical eviction may discard reproducible caches but not accepted identity/binding or a mutation.

## 9. REQUIRED WORK
1. Preserve lifecycle states and improve priority: semantic plan→terrain→collision→critical route/water/site shell→essential gameplay providers→ecology→decoration. Define active, predictive and retained rings against supported speed and measured worst-case readiness.
2. Enforce bounded queues/work/memory, cancellation on unload/profile/rebuild, stale task rejection and ownership-safe cleanup. Use immutable background numeric work only; apply Terrain, GameObjects and colliders on main thread.
3. Refine current sliced work and lookahead for diagonal travel/reversal. Measure entire frames, including activation, Terrain sync, save and destruction; coroutine yields before synchronous save are not asynchronous persistence.
4. Persist accepted named sites and asset/layout versions independently of current `persistentDeltaIds` retention; current distant-record pruning must not reroll meaningful choices. Pure untouched baseline can regenerate only under pinned versions.
5. Apply overlays by feature ID and revision before gameplay publication: harvested/deleted objects, opened containers, spawned features and changed status. Include tombstones, idempotent receipts, stale-overlay rejection and schema migration.
6. Recover bounded generation failure to retained safe ground or explicit loading/recovery, without invisible holes, duplicate roots or teleport safety falsely counting as successful traversal. Register future population/site readiness providers without requiring their production implementation yet.

7. Bounded audit follow-up (A09): reproduce any claimed save-induced streaming stall with matching-frame serialization/publication costs and growing accepted state before optimizing. Preserve complete visual/collision coverage and durable named/delta records. G02 owns durability policy, G05 owns overlay migration, and G19 owns retention/scaling optimization; coordinate only the boundary needed for this streaming defect.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Real production traversal at normal walk/run/dash and supported maximum: straight, diagonal, reversal, edge oscillation, off-road, dense crossing, unload/revisit, save/quit/reload and rebuild/rebind. Assert zero fall-through, missing current collision, stale publication or duplicate feature ownership. Mutate a representative feature through the production overlay and verify it remains changed. Run 150/300 m/s harness separately with explicit diagnostic status, without velocity-specific shortcuts substituting for normal player physics.

Retain current G08 closeout coverage: the distinct user-required 6→260 m/s stress and its governing budgets remain mandatory in 8 fix 4; inherited 150/300 m/s probes keep their separate classification. Verify overlay reconstruction using the applicable G05 migration receipt and measure any save stall on the same frame. No threshold relaxation, readiness slowdown or reduced visual quality is a repair.

## 11. DELIVERABLES
Lifecycle/priority contract, full-frame telemetry, bounded resource budgets, overlay adapter and normal-travel/reconstruction receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G08/G11 register site/population providers and durable records; G09 can depend on safe ground; G19 receives stress cases and known scaling limits.

## 13. COMPLETION GATE
Supported-speed traversal and overlay round trips pass with bounded active resources and no stale/duplicate publication. Future NPC/quest correctness and hours-long endurance are not required. Unsupported-speed failures are documented with impact, never silently removed.

## Binding world architecture appendix (E)

### Authority and data flow

`World identity → macro fields/feature graph → semantic cell plan → terrain/content materialization → streaming lifecycle → persistent mutation overlay before gameplay publication`.

This describes dependencies, not a second runtime stack. G02 owns identity/commits; G05 owns semantic facts; G06 realizes terrain/routes/water; G07 schedules and reconstructs; G08 builds sites/environment. Later domain goals attach durable gameplay state by feature ID. Physical GameObjects never become the authoritative world.

### World identity and deterministic seeding — G02/G05

Extend the existing save-owned plan envelope with world ID/seed, generator version, semantic schema, hash algorithm version, coordinate specification, accepted spatial artifact ID/hash and asset manifest versions. Continue selecting one complete accepted V2 or persisted V1 under V2Preferred until migration explicitly retires a version. The current V2 envelope already has seed, compiler, fingerprint and validation hashes; preserve its acceptance checks.

Use signed logical cell coordinates and a double-precision absolute position or integer cell plus bounded local offset. Current continuation cells are 128 m; preserve this for old saves. Floor division must handle negative positions consistently. Rendering-origin offsets are transient; rebasing updates transforms/physics together and never changes IDs, logical positions, quest destinations or seed derivation. A floating-origin implementation is required before supported travel exceeds demonstrated precision bounds; do not add an unused spatial layer merely for neatness.

Derive independent seeds as a versioned stable hash of world seed, domain tag, owner coordinate/feature ID and generator version. Canonicalize encoding, ordering and numeric serialization. Do not use process-dependent string hashes, time, random GUIDs, mutable registry order or a shared sequential random stream for baseline generation. Existing legacy hashing remains version-pinned; replacing it requires explicit migration. Quantized semantic geometry/hashes must agree across supported builds; define a physical floating-point tolerance separately.

The seed+coordinate+version invariant applies to untouched baseline. Accepted authored/V1/V2 layouts, persisted LLM content and mutation revisions are explicit authoritative exceptions; baseline regeneration must not overwrite them.

### Spatial hierarchy and storage — G05/G07

Use only World → macro region/feature ownership → semantic/streaming cell → optional site sector. Continents and administrative/cultural regions are features with stable membership/query rules, not necessarily additional loaded grids. Reuse one 128 m cell coordinate key for semantic lookup and streaming where possible. A site sector exists only to bound a large feature's materialization; it does not own another settlement identity.

Analytically derive low-frequency elevation, land/ocean tendency, climate/moisture and broad ecological suitability. Lazily compile a bounded macro neighborhood for watersheds, sites, routes and influence. Eagerly compile the opening region and origin seam envelope. Cache immutable results with bounded eviction. Persist accepted generated names, non-reproducible choices, immutable accepted site layouts/bindings, content provenance, discovery and mutations. Do not allocate a planet-sized raster or retain every untouched visited cell forever.

A feature owner is chosen from its stable anchor and versioned owner-region grid, not first discovery. Queries include a bounded halo sufficient for crossing features. Use deterministic tie-breaks for overlap/reservations. Long rivers/roads use linked regional segments with one parent identity and deterministic border contracts; avoid recursively generating unlimited upstream regions to answer one cell.

### Macro geography and semantic plans — G05

Generate coherent landmasses/coastlines, mountain/ridge systems, valleys/plains/basins and climate/moisture trends at frequencies larger than cells. Continent membership follows stable connected macro landmasses. Watershed and civilization pressure influence habitation; cultural and kingdom influence are separately seeded fields/records, not synonyms for biome. A bounded beta need not simulate global politics or ocean travel to retain these identities.

Adapt existing V2 terrain fields, route corridors, hydrology and site anchors into one semantic query result. A cell plan contains versioned identity, owner region/continent, landform regime, elevation intent/ruggedness, temperature/moisture, ecology weights, civilization/danger/cultural influence, palette/density inputs, intersecting feature IDs, reserved sites, local seeds and edge contracts. Derived heights/vegetation samples remain cacheable outputs; accepted topology, site identity and mutation references remain durable. Plan hashes exclude visit times and loading states.

The current continuation uses `continuous_world_cell_v5`, `continuous_edge_v3` and a finite-origin blend toward seeded macro relief; this is a migration starting point, not proof of a unified world graph. Preserve old accepted terrain and introduce a versioned transition collar. Resolve accepted/continued/absent/synthetic feature policy once, then give all consumers the same result. Current per-edge and per-cell fallback differences must not survive as competing ownership policies.

### Terrain and collision — G06

Retain Unity Terrain/TerrainData for the surface. Current origin terrain uses a 1024 m domain, 140 m height range and 513 samples; continuation sampling derives from origin spacing. Accepted V2 sampling bypasses the legacy 8×8 tile-profile height branch. Keep both versioned for compatible saves; do not blend their ownership accidentally.

Sample absolute coordinates through macro elevation → regional shape/ridges → valleys/basins → hydrology cuts → local detail → road grading → reserved site accommodation. Water, roads and sites consume the same constraints; bounded earthworks cannot invalidate basin flow or a neighboring edge. Shared edge positions produce identical sample values; compare normals, paint and collision as well as height. Use local approved meshes for caves, bridges and overhangs, with explicit entrances/holes and collision transitions. Avoid runtime erosion or global fluid simulation.

### Hydrology and routes — G05 topology, G06 realization

Hydrology records own basin, source, directed flow, confluence and sink IDs. Rivers/streams are linked cross-cell features; lakes/ponds are basin polygons/surfaces and marshes/wetlands ecological/wetness regions. Each crossing records parent water ID, position/tangent, width, bed/surface elevation, flow and upstream/downstream links. Terrain carving, water meshes, shore dressing and crossing validation consume this one contract. Preserve legitimate terminal behavior; lakes/wetlands/coasts are not blindly extended as rivers. Verify carving idempotence and no double application of accepted water.

Reserve plausible settlement/POI destinations first, then route a deterministic regional site graph using slope, water and cultural/transport constraints. Roads have real endpoints or declared boundary continuations, parent route IDs, regional segments and shared edge entrances/exits. Realize local geometry within the corridor and adapt terrain. Bridges/fords belong to the route-water intersection contract. Queries can describe destinations before their GameObjects load. Empty wilderness is valid; it does not automatically request a synthetic road.

### Sites, settlements and large features — G05/G08/G11

Support semantic villages/towns/cities, castles/forts, ruins, caves/dungeons, shrines, hostile/resource sites, natural/cultural landmarks, roadside structures and empty wilderness. Beta physical coverage is deliberately narrower and declared in G08. Each site reservation owns a stable ID, anchor, bounds/member cells, route entrances, exclusions, cultural intent and allowed expansion envelope. One immutable accepted layout references asset IDs/versions, transforms, structure/interior graph and gameplay anchors. Runtime sectors may unload independently, but one resident/loot/service owner exists per stable entity.

For multi-cell cities/castles/dungeons, compile the shared footprint and entrances before any member cell publishes structures. Persist chosen layout/bindings rather than rerolling from a changed palette. Current sparse GeneratedSemanticSiteRecord lacks these fields; migrate it. Current streamer retention based only on persistentDeltaIds is insufficient for accepted named sites.

Treat approximately one huge POI per 30 chunks as a provisional player-travel pacing target, not modulo placement. At 128 m cells, 30 traversed cells suggests roughly 3.84 km along a route, not a universal area density. Use seeded regional candidates, minimum-distance/exclusion rules, geography, access and performance envelopes. G18 tests travel time/quiet intervals; G08 proves one representative multi-cell feature. A full catalog of giant cities and dungeons is post-beta.

### Palettes and environmental density — G04/G08/G18

Select from approved versioned asset families by semantic compatibility, then deterministic variation. Separate landform, ecology, climate and culture. Neighbor-aware weights allow ecological transitions without forcing identical architecture. Stable neighborhood selection handles repetition; mutable recent-use history must not change baseline when loading order changes.

Layer canopy, understory, shrubs, ground vegetation, rock/boulders, deadfall/debris, shoreline, roadside, outskirts and storytelling. Density responds to moisture/slope/land use and budgets, not tiny fixed per-cell quotas. Site/route/water/camera masks constrain placement. Validate pivot/scale/grounding, colliders, materials, clipping, culling and sightlines. Preserve quiet space and readable routes. G18 tunes density only after G08 proves safe placement.

### Materialization, streaming and reconstruction — G07

The existing builder/streamer remains coordinator. Work progresses through planned → queued → generating → collision-ready → critical-content-ready → active → retained → unloading/unloaded; failures and cancellations are explicit. Plan/ground/collision precede route/site safety, essential gameplay providers, environment and decoration. Optional model work never gates these stages.

Lookahead depends on speed/direction, measured readiness latency and safety margin; diagonal travel/reversal have explicit coverage tests. Active/retained/predictive rings, queue limits and per-stage budgets are measured on declared hardware. Immutable numeric preparation may be background work; Unity objects and publication remain on the main thread. Epoch/profile/world/revision tokens reject stale results. Unload destroys only owned objects and releases TerrainData/material/mesh resources without discarding semantic truth.

Reconstruct pinned deterministic baseline and accepted artifacts; resolve stable feature bindings; load overlay revision; apply tombstones/state/added-feature records idempotently; publish collision and gameplay only when valid. Overlays cover destroyed/harvested resources, opened containers, NPC status, quest/settlement consequences and spawned accepted features. Player construction/terrain editing remains unsupported unless an explicit retained mechanic registers a typed mutation; do not imply arbitrary building is implemented.

Use G02's coherent snapshot manifest for player/world and auxiliary memory. Index mutations by feature and owner region. Compact replay history only while preserving current state, meaningful canon and needed audit/receipt IDs. G19 determines whether dirty shards/journals are justified; do not serialize every generated physical object or add a competing save store.

### LLM boundaries and verification — G03/G05–08/G16/G19/G20

Deterministic geography establishes facts first. The LLM names/interprets/enriches those facts and proposes player-responsive quests, identities, culture/lore, dialogue and progression through validators. It cannot choose arbitrary coordinates/assets, modify accepted spatial facts implicitly, or delay ground/collision/streaming. Preserve accepted content without regeneration; new narrative revisions are explicit commits.

Determinism tests compare semantic hashes, edges, graph IDs, accepted layouts and overlays across repeated runs, shuffled exploration, negative/diagonal coordinates, reload and compatible versions. Terrain comparisons use declared sample tolerances; load timestamps are excluded. Traverse actual player physics through origin boundaries, roads/water crossings, steep/quiet/dense cells, reversal and multi-cell sites. Report fall-safety intervention separately; teleport recovery is not proof of successful traversal. Each world goal owns bounded acceptance; G19 adds endurance and G20 certifies the complete normal path.

---

# /goal — Build traversable sites, settlements, and semantic environmental density

## 1. GOAL
Turn reserved world features into coherent traversable places with stable multi-cell ownership.

## 2. BINDING EXECUTION CONTRACT
The short objective is only a summary. Own physical site composition and environment; inhabitants and services remain later work.

Current execution uses [8 fix 4](<goals/8 fix 4.md>): R1 publication/recovery → R2 streaming/performance → R3 physical world itinerary. Fixes 1–3 are historical workflows whose acceptance requirements remain binding through fix 4. The LLM audit does not add NPC simulation, dialogue, behavior interpretation, adaptive progression or model benchmarking to G08. Address an audit finding here only when fresh evidence demonstrates that it blocks an existing G08 acceptance row; otherwise route it to its named owner in the [integration record](AUDIT_INTEGRATION.md). No original G08 or fix-4 gate is relaxed.

## 3. PURPOSE
Extend reviewed assemblies and procedural settlement layout instead of treating each cell as independent decorative scatter.

## 4. PREREQUISITES
G04 approved assemblies; G05 reservations/feature IDs; G06 ground/routes/water; G07 streaming and durable binding/overlay lifecycle.

## 5. SCOPE
Physical settlements, POIs, interiors, service anchors, large-site member sectors, ecological layering, safe origin hut and route connections.

## 6. OUT OF SCOPE / DEFERRED
Residents/AI G11, service transactions G13, quests G12, social simulation G14, full pacing/visual polish G18. A service-ready counter/door is not a working merchant.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary with independent assembly and traversal review subagents. Sol High only for large-site ownership or persistent layout migration.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Reuse reviewed source assemblies and preserve prefab relationships. Publish one saved feature layout/binding, materialized by member cells; do not duplicate the city or its future resident when a neighboring sector loads. Do not recenter accepted geography to fit a prefab.

## 9. REQUIRED WORK
1. Adapt YQProceduralSettlementLayout, spatial blueprint/materialization and reviewed-site compiler around G05 reservations. Establish streets→parcels→assemblies→interiors→anchors, with stable entrance/circulation links.
2. Complete one coherent beta family: origin hut, serviceable settlement, hostile/ruin site, cave/dungeon/interior and meaningful landmark/resource area. Semantic types without approved physical content remain explicitly unavailable, not fake clickable sites.
3. Prove a representative large multi-cell site with single owner, serialized member footprint, sector activation, stable ingress/egress and bounds. Support city/castle/multi-cell dungeon contracts; expansive content production remains deferred.
4. Implement deterministic ecological layers—canopy, understory, shrubs, groundcover, rock/boulders, deadfall, shore/road edges, outskirts and storytelling—using slope/moisture/land-use masks and measured budgets. Preserve intentionally quiet wilderness.
5. Enforce palette blending and repetitions from semantic neighborhood context. Validate grounding, burial/clipping, water intersections, collider/camera clearance, culling and exclusions around routes/doors.
6. Supply essential-site readiness and future resident/service/spawn anchors to the existing streamer. Guarantee safe hut-first arrival and an exit route without requiring a generated NPC to complete this goal.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Walk the real authoritative capsule through hut, street, bridge approach, public service interior, cave entrance and both ends of the multi-cell site. Cross sector boundaries in opposite orders, unload/reload/save/revisit and compare layout/asset IDs. Use screenshots plus physical traversal; valid renderer counts alone do not pass. Verify ecology layers respect access and frame budgets.

## 11. DELIVERABLES
Versioned accepted layouts, functional anchor manifest, representative large site, ecological density rules and visual/traversal evidence.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G10 gets a safe origin presentation location; G11 gets persistent homes/roles/service anchors; G18 gets measured density and art limits.

## 13. COMPLETION GATE
Selected beta sites and large-site boundary proof are traversable, coherent and stable on replay; no blocked required entrance or duplicate owner. Population, dialogue, merchants and final long-session pacing are explicitly not this gate.

## Binding world architecture appendix (E)

### Authority and data flow

`World identity → macro fields/feature graph → semantic cell plan → terrain/content materialization → streaming lifecycle → persistent mutation overlay before gameplay publication`.

This describes dependencies, not a second runtime stack. G02 owns identity/commits; G05 owns semantic facts; G06 realizes terrain/routes/water; G07 schedules and reconstructs; G08 builds sites/environment. Later domain goals attach durable gameplay state by feature ID. Physical GameObjects never become the authoritative world.

### World identity and deterministic seeding — G02/G05

Extend the existing save-owned plan envelope with world ID/seed, generator version, semantic schema, hash algorithm version, coordinate specification, accepted spatial artifact ID/hash and asset manifest versions. Continue selecting one complete accepted V2 or persisted V1 under V2Preferred until migration explicitly retires a version. The current V2 envelope already has seed, compiler, fingerprint and validation hashes; preserve its acceptance checks.

Use signed logical cell coordinates and a double-precision absolute position or integer cell plus bounded local offset. Current continuation cells are 128 m; preserve this for old saves. Floor division must handle negative positions consistently. Rendering-origin offsets are transient; rebasing updates transforms/physics together and never changes IDs, logical positions, quest destinations or seed derivation. A floating-origin implementation is required before supported travel exceeds demonstrated precision bounds; do not add an unused spatial layer merely for neatness.

Derive independent seeds as a versioned stable hash of world seed, domain tag, owner coordinate/feature ID and generator version. Canonicalize encoding, ordering and numeric serialization. Do not use process-dependent string hashes, time, random GUIDs, mutable registry order or a shared sequential random stream for baseline generation. Existing legacy hashing remains version-pinned; replacing it requires explicit migration. Quantized semantic geometry/hashes must agree across supported builds; define a physical floating-point tolerance separately.

The seed+coordinate+version invariant applies to untouched baseline. Accepted authored/V1/V2 layouts, persisted LLM content and mutation revisions are explicit authoritative exceptions; baseline regeneration must not overwrite them.

### Spatial hierarchy and storage — G05/G07

Use only World → macro region/feature ownership → semantic/streaming cell → optional site sector. Continents and administrative/cultural regions are features with stable membership/query rules, not necessarily additional loaded grids. Reuse one 128 m cell coordinate key for semantic lookup and streaming where possible. A site sector exists only to bound a large feature's materialization; it does not own another settlement identity.

Analytically derive low-frequency elevation, land/ocean tendency, climate/moisture and broad ecological suitability. Lazily compile a bounded macro neighborhood for watersheds, sites, routes and influence. Eagerly compile the opening region and origin seam envelope. Cache immutable results with bounded eviction. Persist accepted generated names, non-reproducible choices, immutable accepted site layouts/bindings, content provenance, discovery and mutations. Do not allocate a planet-sized raster or retain every untouched visited cell forever.

A feature owner is chosen from its stable anchor and versioned owner-region grid, not first discovery. Queries include a bounded halo sufficient for crossing features. Use deterministic tie-breaks for overlap/reservations. Long rivers/roads use linked regional segments with one parent identity and deterministic border contracts; avoid recursively generating unlimited upstream regions to answer one cell.

### Macro geography and semantic plans — G05

Generate coherent landmasses/coastlines, mountain/ridge systems, valleys/plains/basins and climate/moisture trends at frequencies larger than cells. Continent membership follows stable connected macro landmasses. Watershed and civilization pressure influence habitation; cultural and kingdom influence are separately seeded fields/records, not synonyms for biome. A bounded beta need not simulate global politics or ocean travel to retain these identities.

Adapt existing V2 terrain fields, route corridors, hydrology and site anchors into one semantic query result. A cell plan contains versioned identity, owner region/continent, landform regime, elevation intent/ruggedness, temperature/moisture, ecology weights, civilization/danger/cultural influence, palette/density inputs, intersecting feature IDs, reserved sites, local seeds and edge contracts. Derived heights/vegetation samples remain cacheable outputs; accepted topology, site identity and mutation references remain durable. Plan hashes exclude visit times and loading states.

The current continuation uses `continuous_world_cell_v5`, `continuous_edge_v3` and a finite-origin blend toward seeded macro relief; this is a migration starting point, not proof of a unified world graph. Preserve old accepted terrain and introduce a versioned transition collar. Resolve accepted/continued/absent/synthetic feature policy once, then give all consumers the same result. Current per-edge and per-cell fallback differences must not survive as competing ownership policies.

### Terrain and collision — G06

Retain Unity Terrain/TerrainData for the surface. Current origin terrain uses a 1024 m domain, 140 m height range and 513 samples; continuation sampling derives from origin spacing. Accepted V2 sampling bypasses the legacy 8×8 tile-profile height branch. Keep both versioned for compatible saves; do not blend their ownership accidentally.

Sample absolute coordinates through macro elevation → regional shape/ridges → valleys/basins → hydrology cuts → local detail → road grading → reserved site accommodation. Water, roads and sites consume the same constraints; bounded earthworks cannot invalidate basin flow or a neighboring edge. Shared edge positions produce identical sample values; compare normals, paint and collision as well as height. Use local approved meshes for caves, bridges and overhangs, with explicit entrances/holes and collision transitions. Avoid runtime erosion or global fluid simulation.

### Hydrology and routes — G05 topology, G06 realization

Hydrology records own basin, source, directed flow, confluence and sink IDs. Rivers/streams are linked cross-cell features; lakes/ponds are basin polygons/surfaces and marshes/wetlands ecological/wetness regions. Each crossing records parent water ID, position/tangent, width, bed/surface elevation, flow and upstream/downstream links. Terrain carving, water meshes, shore dressing and crossing validation consume this one contract. Preserve legitimate terminal behavior; lakes/wetlands/coasts are not blindly extended as rivers. Verify carving idempotence and no double application of accepted water.

Reserve plausible settlement/POI destinations first, then route a deterministic regional site graph using slope, water and cultural/transport constraints. Roads have real endpoints or declared boundary continuations, parent route IDs, regional segments and shared edge entrances/exits. Realize local geometry within the corridor and adapt terrain. Bridges/fords belong to the route-water intersection contract. Queries can describe destinations before their GameObjects load. Empty wilderness is valid; it does not automatically request a synthetic road.

### Sites, settlements and large features — G05/G08/G11

Support semantic villages/towns/cities, castles/forts, ruins, caves/dungeons, shrines, hostile/resource sites, natural/cultural landmarks, roadside structures and empty wilderness. Beta physical coverage is deliberately narrower and declared in G08. Each site reservation owns a stable ID, anchor, bounds/member cells, route entrances, exclusions, cultural intent and allowed expansion envelope. One immutable accepted layout references asset IDs/versions, transforms, structure/interior graph and gameplay anchors. Runtime sectors may unload independently, but one resident/loot/service owner exists per stable entity.

For multi-cell cities/castles/dungeons, compile the shared footprint and entrances before any member cell publishes structures. Persist chosen layout/bindings rather than rerolling from a changed palette. Current sparse GeneratedSemanticSiteRecord lacks these fields; migrate it. Current streamer retention based only on persistentDeltaIds is insufficient for accepted named sites.

Treat approximately one huge POI per 30 chunks as a provisional player-travel pacing target, not modulo placement. At 128 m cells, 30 traversed cells suggests roughly 3.84 km along a route, not a universal area density. Use seeded regional candidates, minimum-distance/exclusion rules, geography, access and performance envelopes. G18 tests travel time/quiet intervals; G08 proves one representative multi-cell feature. A full catalog of giant cities and dungeons is post-beta.

### Palettes and environmental density — G04/G08/G18

Select from approved versioned asset families by semantic compatibility, then deterministic variation. Separate landform, ecology, climate and culture. Neighbor-aware weights allow ecological transitions without forcing identical architecture. Stable neighborhood selection handles repetition; mutable recent-use history must not change baseline when loading order changes.

Layer canopy, understory, shrubs, ground vegetation, rock/boulders, deadfall/debris, shoreline, roadside, outskirts and storytelling. Density responds to moisture/slope/land use and budgets, not tiny fixed per-cell quotas. Site/route/water/camera masks constrain placement. Validate pivot/scale/grounding, colliders, materials, clipping, culling and sightlines. Preserve quiet space and readable routes. G18 tunes density only after G08 proves safe placement.

### Materialization, streaming and reconstruction — G07

The existing builder/streamer remains coordinator. Work progresses through planned → queued → generating → collision-ready → critical-content-ready → active → retained → unloading/unloaded; failures and cancellations are explicit. Plan/ground/collision precede route/site safety, essential gameplay providers, environment and decoration. Optional model work never gates these stages.

Lookahead depends on speed/direction, measured readiness latency and safety margin; diagonal travel/reversal have explicit coverage tests. Active/retained/predictive rings, queue limits and per-stage budgets are measured on declared hardware. Immutable numeric preparation may be background work; Unity objects and publication remain on the main thread. Epoch/profile/world/revision tokens reject stale results. Unload destroys only owned objects and releases TerrainData/material/mesh resources without discarding semantic truth.

Reconstruct pinned deterministic baseline and accepted artifacts; resolve stable feature bindings; load overlay revision; apply tombstones/state/added-feature records idempotently; publish collision and gameplay only when valid. Overlays cover destroyed/harvested resources, opened containers, NPC status, quest/settlement consequences and spawned accepted features. Player construction/terrain editing remains unsupported unless an explicit retained mechanic registers a typed mutation; do not imply arbitrary building is implemented.

Use G02's coherent snapshot manifest for player/world and auxiliary memory. Index mutations by feature and owner region. Compact replay history only while preserving current state, meaningful canon and needed audit/receipt IDs. G19 determines whether dirty shards/journals are justified; do not serialize every generated physical object or add a competing save store.

### LLM boundaries and verification — G03/G05–08/G16/G19/G20

Deterministic geography establishes facts first. The LLM names/interprets/enriches those facts and proposes player-responsive quests, identities, culture/lore, dialogue and progression through validators. It cannot choose arbitrary coordinates/assets, modify accepted spatial facts implicitly, or delay ground/collision/streaming. Preserve accepted content without regeneration; new narrative revisions are explicit commits.

Determinism tests compare semantic hashes, edges, graph IDs, accepted layouts and overlays across repeated runs, shuffled exploration, negative/diagonal coordinates, reload and compatible versions. Terrain comparisons use declared sample tolerances; load timestamps are excluded. Traverse actual player physics through origin boundaries, roads/water crossings, steep/quiet/dense cells, reversal and multi-cell sites. Report fall-safety intervention separately; teleport recovery is not proof of successful traversal. Each world goal owns bounded acceptance; G19 adds endurance and G20 certifies the complete normal path.

---

# /goal — Stabilize the authoritative player and core RPG execution

## 1. GOAL
Make immediate movement, combat, interaction, inventory, equipment, and supported abilities reliably playable.

## 2. BINDING EXECUTION CONTRACT
This full specification is authoritative. Own deterministic execution and usable domain controls, not personalized content generation or final presentation polish.

## 3. PURPOSE
Give every later adaptive proposal a safe mechanical destination and every world test a trustworthy player.

## 4. PREREQUISITES
G02 state/transactions/event envelope; G04 actor/equipment/effect assets; G07 supported-speed safe traversal.

## 5. SCOPE
Existing YQInvestorPlayerMotor/Combat/Vitals/Enemy, PlayerState inventory/equipment, equipment visuals, animation execution, defeat/recovery and typed action outcomes; stable ability capability registry.

## 6. OUT OF SCOPE / DEFERRED
Creator G10; generated quest rewards G12; merchant economy G13; interpretation/unlock generation G15–16; final feedback and tuning G18. Preserve existing progression offers without claiming adaptive acceptance here.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may verify equipment and combat independently against shared contracts. Sol High for player authority/lifecycle bugs only.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Exactly one gameplay player, collider authority, inventory and equipment state. First/third person are presentations. Never execute a mechanic inferred from a skill/item name. Basic RPG verbs are available immediately; adaptation augments them.

## 9. REQUIRED WORK
1. Verify movement/look/jump/dash/crouch, slopes/steps, camera toggle and invalid-position recovery; left click attacks, E interacts, right click uses equipped secondary/spell per GDD. Preserve ordinary jump/dash until an accepted ability changes them.
2. Fix actual weapon/armor attachment and animation intent mappings, modal blocking and first/third-person consistency. No ghost player, visible capsule, left-click interaction or camera-dependent spell mechanics.
3. Stabilize hit/damage/health/death, aggro/disengagement, grounded movement modes, spawn clearance and safe player defeat/recovery. Persist meaningful death/loot state through existing overlay.
4. Make inventory add/remove/stack/equip/use/loot idempotent transactions; preserve slot IDs and supported visual bindings. Provide usable inspect/equip/ability UI now; final layout polish is G17.
5. Publish supported ability targeting/resource/cooldown/status/power/animation/VFX/SFX contracts. Clamp finite values and reject unsupported effects. Keep generated mechanics within approved templates; test resource/cooldown atomicity and repeated input.
6. Emit G02 events for attempted and successful actions with semantic target, damage/outcome, tool/ability ID and context; include trees/tools/failed attempts for later interpretation. Maintain old counter adapters until their consumers migrate.
7. Stabilize existing level/skill/offer application and persist accepted mechanics. Do not auto-award skills based on names or implement the full adaptive detector here.

8. Make the existing ability contract complete across proposal/offer → acceptance → equipped record → executor → save/reload (A02). Preserve supported targeting/resource/cost/cooldown/effect/power/animation/VFX/SFX fields; reject unsupported combinations rather than falling through to a damaging effect. Names/descriptions are presentation, never executable dispatch. Preserve legacy accepted content with explicit compatibility/migration behavior. G16 owns semantic generation and unlock choice; this goal owns the executor and field continuity.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Production-play tests cover camera modes, movement/contact, attack versus interaction, damage/death/recovery, inventory transfer, equip visuals, ability resource/cooldown and modal input. Save/quit/reload inventory/equipment/accepted ability and mutate a loot source without duplication. Record typed events from real actions. Exercise a 15-minute functional loop; final newcomer comprehension belongs to G17/G18.

Add self/buff versus projectile/damaging-pulse cases, unsupported target/effect combinations, repeated input and resource/cooldown atomicity. Compare the complete accepted payload and actual effect before and after equip and save/quit/reload, including supported old saves. Record stable target/tool IDs and typed attempted/successful/failed outcomes for ordinary attacks and tree/tool attempts, without granting adaptive content here.

## 11. DELIVERABLES
Capability/event registry, stable executor/inventory contracts, domain controls, animation/equipment mapping and runtime receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G10 uses actual avatar capabilities; G11–14 consume action/transaction contracts; G15 observes real evidence; G16 proposes only supported executable abilities/items.

## 13. COMPLETION GATE
Declared core loop passes in real play and reload with one player and no lost/duplicated inventory or unsupported execution. Future generated content variety, complete onboarding and 60-minute polished experience are not required.

---

# /goal — Complete persistent character creation and both origin routes

## 1. GOAL
Let a tester create a supported character and enter the same canonical origin/world pipeline through guided or authored background.

## 2. BINDING EXECUTION CONTRACT
The full specification is binding; the short summary cannot omit custom-origin confirmation, persistence, or asset compatibility.

## 3. PURPOSE
Finish the RPG beginning after its real dependencies exist, preserving the Goddess and Vey's hut.

## 4. PREREQUISITES
G03 model boundary; G04 character/species capability registry; G08 origin hut/exit; G09 one avatar/equipment executor; transitive G02 identity/save framework.

## 5. SCOPE
Creator stages, preview, supported species/people/appearance, canonical origin source/normalization, guided Goddess integration, custom-origin confirmation and one-time commit.

## 6. OUT OF SCOPE / DEFERRED
New rigs/species without compatible assets, full sculpting/morph tooling and later respec are deferred. Relationship consequences G14, generalized adaptation G16, integrated teaching/settings G17 and final audiovisual treatment G18.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; independent preview and origin validation work may use Luna High subagents. Sol High reviews identity/origin migration only.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
No fake sliders, raw-prose mechanical authority, forced class selection or duplicated gameplay avatar. Freeze supported canon/species options before character selection; later geography consumes validated identity constraints rather than inventing options after the player chooses.

## 9. REQUIRED WORK
1. Build Identity→Species/People→Appearance→Details→Origin→Review→Begin using actual supported assets. Preserve distinct species and cultural people IDs, names and safe dialogue-reference data; no invented racial bonuses.
2. Persist explicit asset/material/variant choices so creator preview matches the authoritative in-game avatar, equipment and both camera modes. Migrate old appearance summaries to truthful supported defaults while preserving original text.
3. Guided origin proceeds to the existing Goddess sequence; preserve supported questionnaire depth modes. Authored origin uses bounded fields for upbringing, formative relationships, former life, event and motivation, with unknown fields remaining unknown.
4. Before replacing the questionnaire show natural-language confirmation with Use My Origin / Return to Guided Origin. Cancellation must preserve edits without committing or starting generation.
5. Normalize both routes into one origin contract/source record, preserving user text separately as context. G03 validates envelope; this goal validates origin facts, references and supported starting mechanics. No raw text controls coordinates/assets.
6. Commit identity/origin/start grants once, then enter deterministic world materialization and Vey's hut. Offline failure uses clearly tracked minimal fallback; successful generation cannot reroll an already accepted origin.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Use ordinary new profiles, not the preaccepted diagnostic fixture: guided, authored, cancellation/backtracking, species-option changes, empty/malformed/oversized text and unavailable model. Compare preview/gameplay and save/quit/reload identity, appearance, source, origin and grants. Demonstrate one real valid model-generated origin and fallback separately. Existing world defects are handed to their owner rather than fixed inside the creator.

## 11. DELIVERABLES
Creator UI, canonical origin/appearance migration, confirmation flow and ordinary-profile acceptance receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G17 can teach from a stable RPG beginning. Every downstream consumer sees one accepted origin schema regardless of route.

## 13. COMPLETION GATE
Both routes produce a recognizable persistent character in the real generated world; confirmation/cancellation and failure recovery work; no duplicate grants or fabricated optional facts. Final tutorial pacing remains G17/G18.

---

# /goal — Establish persistent residents, roles, and NPC knowledge

## 1. GOAL
Make settlements inhabited by stable residents whose status and knowledge survive physical unloading.

## 2. BINDING EXECUTION CONTRACT
The complete specification remains binding. Own NPC existence/status/knowledge and runtime presence, not full quests, trading or law.

## 3. PURPOSE
Resolve generated NPC plans, WorldState NPC records and loaded agents into one clear identity/status boundary before social content expands.

## 4. PREREQUISITES
G05 settlement/region/continent/faction context; G07 streaming/overlay; G08 physical homes/routes/anchors; G09 interaction and combat outcome contracts.

## 5. SCOPE
Resident identity, role/demographic assignment, status, faction membership, relationship references, bounded factual knowledge and lightweight loaded behavior.

## 6. OUT OF SCOPE / DEFERRED
Quest/dialogue semantics G12; stock/trade G13; standing/crime/relationship interpretation G14; generated personalized characterization G16. No always-on simulation of every unloaded citizen.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may verify navigation/role placement versus save/status separately. Sol High reviews generated-plan/status authority migration.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
One stable NPC ID across generated plan, mutable record, dialogue memory and GameObject. A plan defines origin/role/binding; mutable status lives in the canonical state. Scene agents are disposable views. Do not erase meaningful residents when their cell is evicted.

## 9. REQUIRED WORK
1. Adapt GeneratedNpcPlanRecord/NpcRecord/planning service/population builder with explicit immutable versus mutable fields and versioned links. Persist names/identity once accepted; prohibit reconstruction-time renaming.
2. Resolve home site, region, continent, species/people/faction, role and service anchors. Support coherent mixed populations; do not hardcode fantasy demographics to real-world categories.
3. Populate a beta settlement with civilian, service-provider, guard/local authority and quest-capable roles using approved assets and clearance. Use bounded wander/schedule/presence behavior with safe NavMesh or existing movement ownership.
4. Persist meaningful alive/dead/removed/relocated status, role changes and future service references. Unload/reload cannot duplicate or resurrect a changed resident. Far unloaded residents use durable records, not active AI.
5. Establish typed knowledge entries: source event, observer/report source, fact/proposition ID, locality, confidence/rumor marker, acquired time and retention policy. Separate world truth, private facts, witnessed facts and public knowledge.
6. Expose relationship and service query/command interfaces with neutral defaults. G14 owns score/law policy; G13 owns transactions. Register essential population readiness without holding terrain hostage to optional NPC prose.

7. Preserve every valid accepted identity when population coverage is incomplete (A07). Reconcile immutable GeneratedNpcPlanRecord fields with mutable NpcRecord status by stable ID; fill missing/rejected slots through a bounded validated transaction rather than clearing the accepted population. Persist accepted partial batches through the existing profile contract, with provenance/fallback identity intact; retry, crash and reload cannot rename, duplicate, resurrect or silently replace residents.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
In production visit the settlement, meet identified residents, cross/unload/revisit, save/quit/reload and compare IDs/status/home anchors. Change a resident through a real supported action and verify no duplication/resurrection. Verify two NPCs with different knowledge return different authorized context, and unloaded NPCs do not run expensive behavior.

Add incomplete-coverage and interrupted-batch fixtures containing already accepted and changed/dead residents. Verify only missing slots are generated and accepted IDs/status survive retry/reload. Test bounded knowledge selection with wrong-entity, expired/superseded and private facts; unloaded residents retain facts without continuous model inference. G12 tests conversational use of this knowledge.

## 11. DELIVERABLES
NPC authority migration, resident/role records, knowledge store/query rules, presence integration and runtime persistence receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G12 receives persistent people and authorized knowledge; G13 service identities; G14 witness/relationship primitives; G15 contextual events. Hooks are explicitly distinguished from implemented service mechanics.

## 13. COMPLETION GATE
One representative settlement has stable functioning resident presence, safe circulation and persistent status/knowledge. Trading, quest generation, rumor policy and final dialogue quality need not already pass.

---

# /goal — Stabilize structured quests, dialogue memory, and consequence commits

## 1. GOAL
Make generated quests and NPC conversations produce validated, persistent gameplay outcomes.

## 2. BINDING EXECUTION CONTRACT
The full specification is binding. Own objective/dialogue/quest consequence mechanics; future social and economy policy enter through declared interfaces.

## 3. PURPOSE
Preserve the existing objective evaluator while removing prose-driven rewards and unvalidated counter bindings.

## 4. PREREQUISITES
G03 proposal/scheduler boundary; G09 mechanics/events/inventory; G11 stable NPCs and knowledge. Transitive G02 commit protocol and G05 spatial lookup are available.

## 5. SCOPE
Quest lifecycle/objectives/rewards, bounded dialogue context/transcripts/memory, NPC-bound generated content, typed quest and dialogue commands and branching world-state consequences.

## 6. OUT OF SCOPE / DEFERRED
Merchant price/stock policy G13; law/standing/alignment policy G14; behavioral targeting G15/G16; final journal/navigation presentation G17. Current dialogue action=none must not be treated as an existing transaction system.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary with independent objective and memory test work. Sol High reviews cross-record consequence/reward commits and old completed-quest migration.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Prose presents; typed objective records, target IDs and event bindings execute. Persist approved rewards before quest acceptance; never calculate them from narrative words or current level on completion. Preserve already granted reward history.

## 9. REQUIRED WORK
1. Extend supported objective registry and allowlisted event bindings; reject arbitrary counter keys/prefixes, unknown targets, impossible routes and unsupported mechanics. Set objective baselines at acceptance so old lifetime counts do not auto-complete new work.
2. Implement available→accepted→active→resolved/failed/cancelled state transitions, multi-objective progress, explicit failure/recovery and exactly-once rewards/consequences. Reconcile origin quests with this authority.
3. Persist typed XP/currency/items and consequence receipts at proposal acceptance; migration preserves completed receipts and freezes unresolved legacy reward rules without retroactive double awards.
4. Implement beta quest patterns for talk, travel/discovery, retrieve/deliver and defeat/clear, plus one branching choice. Protect/investigate/negotiate variants require an existing supported executor; otherwise reject and list as deferred rather than ship impossible objectives.
5. Build dialogue from NPC-known facts, relevant relationships, location, quest and recent bounded memory. Preserve profile-scoped transcripts and summaries; validate any typed offer/accept/complete/inventory request before invoking domain APIs. Spoken claims alone cannot grant rewards or canon.
6. Generate names/text from committed people/places; absent entities require a separate accepted creation proposal. Persist promises, rescue/betrayal and outcomes as typed memory, not every line forever. Implement one branch with durable NPC/world flag differences; later G14 supplies standing/alignment consequences through the same seam.

7. Align every live quest producer, including director offers, with the same supported objective/reward acceptance contract (A04). A schema-valid response must still resolve current entities, reachable destinations, achievable prerequisites and acceptance-time counter baselines before being offered. Missing required objective data is a rejection, not permission to infer completion from prose.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Contract checks for target mismatch, prior-counter history, malformed objectives, missing/dead target, duplicate completion and reward retry. In production meet an NPC, receive/accept a generated task, complete a supported path, save/quit/reload/revisit and verify objective/reward/memory continuity. Execute both branches on separate profiles and verify differing NPC response without requiring future reputation policy.

Add a real director-produced quest through proposal → offer → accept → completion and reload, plus missing/dead/wrong target, impossible route/prerequisite and prior lifetime-counter cases. Retrieve an old promise after the recent transcript window has expired and verify its provenance and NPC knowledge scope (A13). For dialogue commands, rejection/staleness/duplicate delivery must not produce success narration or a grant (A16); speech follows the actual domain receipt.

## 11. DELIVERABLES
Objective/reward registries, migration fixtures, dialogue knowledge/command contract, persisted branching scenario and runtime receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G13 receives reward transactions; G14 extends social consequences; G16 can personalize valid quest templates; G17 displays objectives and authorized destinations.

## 13. COMPLETION GATE
Supported quest patterns and branching memory pass real play/reload without prose mechanics, impossible bindings or duplicate rewards. Future economic/social policy and every aspirational GDD objective are not required.

---

# /goal — Make trade, rewards, and settlement services transactional

## 1. GOAL
Provide useful merchants and services with stable value, persistent stock, and duplication-safe transactions.

## 2. BINDING EXECUTION CONTRACT
This full specification is authoritative. Own economic execution/value policy; do not expand into a simulated global economy.

## 3. PURPOSE
Give rewards and settlements practical value using the existing inventory/currency authority.

## 4. PREREQUISITES
G09 item/inventory transactions; G11 merchant/service identity; G12 typed rewards and consequence receipts.

## 5. SCOPE
Player currency, item valuation, merchant stock, buy/sell, healing/rest/supplies where supported, reward value consistency and neutral standing modifier interface.

## 6. OUT OF SCOPE / DEFERRED
Standing policy G14; personalized trade opportunities G16; long-session inflation tuning G18/G19. Large crafting, housing, production chains and global scarcity simulation are deferred. Retained gathering/crafting templates use the same inventory contract only when required by G16's selected behavior path.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; independent arithmetic/transaction fixtures may use Luna High subagents. Sol High reviews save/commit consistency and exploit-prone cross-record changes.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
One currency/inventory authority. Prices/rewards use typed finite bounded values; dialogue or display text never spends money. No normalization-time handouts, negative/overflow balances or hidden stock rerolls.

## 9. REQUIRED WORK
1. Reuse PlayerState currency and inventory; formalize checked value arithmetic, quantity rules, ownership and transaction idempotency.
2. Persist merchant stock IDs, item identity, available currency if used, restock policy and version/time anchor. Loading/unloading must not reset stock; wall-clock changes must not duplicate restocks.
3. Implement atomic buy/sell/service transactions with validated quoted price, quantity, current revision, funds/capacity and final receipt. Cancel or retry safely after stale quotes, duplicate clicks or save failure.
4. Use base value plus bounded context/standing modifiers; expose a neutral query provider until G14 supplies actual standing. A future modifier is not a prerequisite for trade acceptance.
5. Integrate supported healing/rest/supplies and quest rewards through the same value/receipt contracts. Make useful basic UI and clear failure messages; final UI composition is G17.
6. Record economic events with actor, merchant, item and outcome for quest and behavioral consumers. Preserve generated identity and asset binding through purchases/resales.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Test zero/negative/overflow quantities, insufficient funds, duplicate submissions, stale quotes and failed commits. In production earn a quest reward, buy, sell, use a service, unload/reload and quit/restart; reconcile balances/stock/item IDs and verify no duplication. Model outage must not stop an already accepted merchant transaction.

## 11. DELIVERABLES
Economic authority/value policy, merchant/service records, transaction UI/API, restock contract and exploit/reload receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G14 can supply bounded standing modifiers; G16 can create validated contextual rewards/opportunities; G18 tunes value pacing without changing ownership.

## 13. COMPLETION GATE
A player earns/spends value and uses retained services in real play; repeated/stale/failed operations do not duplicate or lose state; stock and generated identity persist. No future social simulation or full economy balance is required.

---

# /goal — Establish bounded social knowledge, relationships, law, and alignment

## 1. GOAL
Make local social consequences persistent and knowledge-based while keeping moral and spiritual state independent.

## 2. BINDING EXECUTION CONTRACT
The complete specification is binding. This is a bounded beta social slice, not every archetype, relationship or legal simulation from the old roadmap.

## 3. PURPOSE
Unify the old standing/crime/memory dependency cycle under one policy owner using established NPC knowledge and transaction interfaces.

## 4. PREREQUISITES
G11 identity/knowledge/witness primitives; G12 quest/dialogue consequences; G13 merchant/service modifier interface; transitive G09 contextual actions.

## 5. SCOPE
Relationship/affinity, awareness and scoped standing; local witnessed incidents/jurisdiction/response; independent benevolence, purity/corruption and order/chaos state/history; typed consequence integration.

## 6. OUT OF SCOPE / DEFERRED
Global rumor simulation, universal species/continent standing propagation, full bounty/jail systems, faction wars, elaborate romance schedules and mythic transformations are post-beta. Preserve extensible target scopes and history; G16 can demonstrate a bounded consensual relationship opportunity, G18 optional supported visual cues.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may build knowledge and axis-history fixtures. Sol High reviews cross-domain consequence persistence and duplicate standing authority.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Unknown is not neutral. Reputation reflects who knows; moral state may change without witnesses. Do not conflate cultural identity with inherent morality, order with good, or corruption with hostility. LLM prose cannot set scores.

## 9. REQUIRED WORK
1. Migrate existing reputation/faction/affinity fields to one scoped query/commit policy with aliases. Track awareness separately from signed standing; presentation may map -100..100 to 0..100 with configurable non-overlapping tiers.
2. Define incidents with ID, actor/victim/actual witnesses, place/jurisdiction, context, severity, evidence/publicity and propagation policy. Cover supported theft/assault/murder/trespass; unsupported crime verbs remain rejected capabilities.
3. Implement a bounded local knowledge route: witnessed incident→report/guard or public knowledge→limited local rumor. Private acts do not become omniscient dialogue; major public events can create explicit regional reports without global simulation.
4. Provide warning/hostility/service denial and safe disengagement/recovery using supported NPC/combat behavior. Integrate a helpful action and harmful action with dialogue, service modifier and one quest eligibility/consequence.
5. Persist three independent alignment axes plus meaningful extrema/milestones for fall/redemption/corruption/cleansing. Derive archetype labels from data predicates, not boolean proliferation; an exhaustive transformation content catalog is deferred.
6. Persist bounded relationship changes and typed consent/interest context where relevant. Expose opportunity eligibility to G16 without forcing romance or interpreting every friendly line as romantic intent.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Compare unwitnessed, witnessed-local and explicitly public incidents. Verify different NPC knowledge, local standing/service reaction and independent moral changes across save/quit/reload. Prove revered-but-harmful and disliked-but-helpful states plus corruption/cleansing history can coexist. Exercise a branching quest with social consequences using G12 receipts, and ensure repeated events cannot double-apply scores.

Audit clarification (A17): repeated proximity or friendly dialogue alone must not become romantic intent. Test ambiguous/contradictory evidence, explicit player choices, NPC-scoped knowledge and duplicate incidents across reload; supported relationship opportunities remain bounded by existing policy. Full romance/family/autonomous-society simulation is not added.

## 11. DELIVERABLES
Social/incident policy and scope registry, migrated queries, history/relationship records, local response integration and persistence receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G16 gets validated relationship/social opportunity and consequence interfaces; G17 gets truthful known-state UI data; G18 tunes magnitudes and optional manifestations.

## 13. COMPLETION GATE
The bounded local slice produces distinct knowledge-based reactions and persistent independent alignment/history. No requirement for continent-wide simulation, every named archetype's art, or a full romance game.

---

# /goal — Build trustworthy behavioral evidence and adaptive candidates

## 1. GOAL
Turn real player actions into durable, extensible, validated evidence of intent.

## 2. BINDING EXECUTION CONTRACT
The full specification governs this goal. Own observation, evidence and candidate eligibility; final unlock application and generated experience belong to G16.

## 3. PURPOSE
Preserve existing observation/progression work while removing prose-based eligibility and lossy shared event consumption.

## 4. PREREQUISITES
G03 bounded interpretation requests; G09 real typed attempts/outcomes and supported mechanics; G11 semantic target/knowledge context; transitive G02 event identity/commit framework.

## 5. SCOPE
ActionRecorder/Registry/Accumulator, PlayerBehaviorRollup and ProgressionMath/ThinkCycle evidence boundaries, independent cursors, pattern records, hidden checks and candidate schema.

## 6. OUT OF SCOPE / DEFERRED
Granting new abilities/titles/classes/quests G16, social policy G14, final notifications G17 and pacing G18. Do not implement every possible player behavior as a hardcoded detector.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; independent event-replay and anti-farming tests may use Luna High subagents. Sol High reviews asynchronous consumption/state migration; Luna Max/Extra High for bounded scoring problems.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Basic attack/use/interact remains immediately available. Capture failed attempts as evidence without rewarding failure spam. Text labels are presentation; candidate eligibility references typed events, contexts and supported capability IDs.

## 9. REQUIRED WORK
1. Migrate immediate counters and delayed rollup to stable event IDs and explicit windows. Preserve old counter aliases without double-counting; document their evidence limitations.
2. Replace clearing the entire accumulator after asynchronous offer creation with acknowledging only the submitted event window. Give progression, world director, quests and summaries independent cursors; retention cannot drop unacknowledged required evidence.
3. Define evidence records with context, target/tool semantics, outcomes, repetition, diversity, recency, significance, confidence and provenance. Bound retained detail and persist meaningful aggregate/history without letting normal summaries erase active hypotheses.
4. Implement extensible pattern/query primitives with configurable constraints and cooldown/novelty/anti-farming policies. Model interpretation may propose a supported pattern association, but deterministic rules validate evidence and lore/context.
5. Replace ProgressionDecisionApplier's name/description keyword match as eligibility authority with explicit supported evidence requirements. Preserve novelty curation and incubation.
6. Persist candidate lifecycle: observed→hypothesis→eligible→proposed/deferred/rejected; record why and evidence IDs. Candidates reference supported mechanics/content kinds, budget and context; G16 owns offer/commit execution.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Replay identical event sequences and compare candidate/evidence records. Test events arriving during inference, two consumers, duplicate events, profile switch, reload, retention boundaries and farming repetitions. In real play demonstrate tree/tool interaction and a distinct combat/exploration/social pattern produce explainable candidates while ordinary one-off actions and irrelevant text do not. Confirm no new ability is silently granted here.

Audit regressions (A01, A05): freeze each submitted evidence window and test later-arriving events, multiple consumers with independent acknowledgement, duplicate event/response delivery, retention boundaries, movement while inference is pending, profile switch and reload. Preserve rare meaningful attempts, including failures, while distinguishing repetition from diversity and farming. Test false positives, false negatives, competing interpretations and explicit abstention; preserve provenance and unresolved hypotheses rather than deleting them to meet caps. Persist deferred/rejected/incubated candidate state; G16 owns the final offer-choice lifecycle. G03 validates request dependencies and G09 supplies typed actions; neither becomes a second evidence owner.

## 11. DELIVERABLES
Evidence/candidate schemas and migration, cursor/retention contract, extensible detector registry, replay fixtures and real-action receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G16 receives persisted eligible candidates and supporting facts, with supported mechanic bounds and no need to interpret generated names. G19 receives event-growth and replay stress cases.

## 13. COMPLETION GATE
Evidence survives asynchronous processing/reload without loss or double use, candidate eligibility is deterministic and typed, and two real behavior families are observed. A final generated unlock is deliberately not this gate.

---

# /goal — Deliver meaningful adaptive unlocks and persistent player-responsive content

## 1. GOAL
Prove behavior becomes meaningful generated mechanics, opportunities, and lasting world response.

## 2. BINDING EXECUTION CONTRACT
This entire specification is binding. This goal owns the defining adaptive product loop, not an unlimited catalog of possible RPG systems.

## 3. PURPOSE
Connect trustworthy evidence to safe generated offers and accepted execution, extending the existing progression/content services.

## 4. PREREQUISITES
G03 model lifecycle; G12 valid quests/dialogue; G13 rewards/services; G14 relationships/social policy; G15 durable eligible evidence; transitive G09 mechanical executors.

## 5. SCOPE
Evidence→context/lore validation→candidate→structured generated content→curation→player choice→atomic acceptance→mechanical integration→persistent evolution/world response.

## 6. OUT OF SCOPE / DEFERRED
Arbitrary new runtime mechanics, infinite profession trees, broad spell algebra, marriage/family simulation, empire management, genre/world rebuilding and every mythic path are post-beta. Unsupported proposals are rejected or incubated, never executed as code.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may verify one independent path each. Sol High reviews content/consequence commit idempotency and compatibility; Luna Max/Extra High for bounded validator/debug work.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
LLM generates identity/meaning using real world facts and player evidence. Deterministic code owns eligibility, prices, power, targeting and execution. Persist accepted records; do not regenerate them on load or replace accepted fallback identity silently.

## 9. REQUIRED WORK
1. Extend existing ProgressionDecisionApplier, pending offers and GeneratedRpgContentService through the common validators. Support generated items, ability/skill/spell, title/class evolution, quest/opportunity and NPC/lore/relationship content by declared capabilities—not separate freeform mutation paths.
2. Validate schema, canonical references, evidence, lore, supported mechanical effect, power/resource/cooldown budget, asset binding, length and novelty. Reject narrative-only rewards with no supported consequence.
3. Offer accept/decline/ignore/incubate with cooldown and persisted state. Idempotently commit content, inventory/ability changes, evidence acknowledgement and world/NPC consequence receipts. Historical accepted content remains stable after model/registry changes.
4. Ship at least two distinct behavior proofs: repeated meaningful tree/tool interaction unlocks a bounded useful harvesting/woodcraft action or opportunity; a combat/exploration/social pattern yields a different executable ability or personalized quest. At least one accepted title/class change has a measurable supported effect. Use generated names and descriptions, not a fixed final content list.
5. Preserve relationship-oriented interpretation through G14, with a bounded relationship opportunity when supported; a complete romance system is not required. Shovel/treasure and spell specialization may be supported variants, not mandatory parallel feature projects.
6. Enrich names, lore, settlements, regions, NPCs and cultural descriptions from committed geography/identity. Accepted factual enrichment cannot move roads/terrain or invent conflicting places. Offline fallback preserves play; when service returns, enrich only uncommitted fields or an explicit new accepted revision.

7. Replace fixed final-name pools and name/description-driven eligibility with generated identity backed by G15 evidence and G09-supported consequences (A14). Deterministic eligibility, mechanical templates, formulas and identifiable fallback remain allowed. Preserve accepted names/payloads across reload/model changes; do not silently rewrite existing titles/classes/items.
8. Coordinate typed domain commits through existing owners (A16, A18): revalidate dependencies and receive a success/decline/failure receipt before presentation announces a grant or world change. Lore enrichment must validate referenced entities, chronology and immutable accepted geography/identity; additive wording alone is not a validator. Repair only the rejected uncommitted field/record within the shared attempt budget. No partial grant, retcon, arbitrary asset path or generated C#.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Ordinary production play, without developer grants, must yield both behavior paths, a real configured-model proposal, validated player acceptance, visible mechanical change and one NPC/world response. Save/quit/reload/revisit and compare accepted record IDs/payloads/effects with model disabled. Test decline, duplicate response, stale profile, unsupported effect, contradictory lore and malformed output; no partial grants.

Extend the existing two natural-play proofs with decline/defer/incubate cooldown persistence, duplicate responses, movement during pending generation, profile changes, model outage and later recovery. Unsupported mechanic compositions must defer/reject, not become prose-only unlocks or generic damage. Demonstrate generated names with measurable title/class effects, real harvesting/woodcraft usefulness and the distinct second supported outcome. Verify accepted content and world/NPC receipts remain identical on reload with inference disabled; rejected actions must not announce success.

## 11. DELIVERABLES
Domain validators/generation integration, two reproducible natural-play paths, title/class effect proof, accepted content provenance and replay receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G17 can teach and display a real adaptive experience; G18 can tune discovery rate/novelty; G19 can stress accepted content and event growth.

## 13. COMPLETION GATE
Two different real behaviors lead to useful generated, accepted, persisted outcomes; at least one ability/action and one personalized opportunity/world response work, and titles/classes are mechanically grounded. Fallback-only demonstrations or prose-only rewards do not pass normal-generation acceptance.

---

# /goal — Finish discovery, onboarding, player UI, and settings

## 1. GOAL
Make the supported beta understandable and controllable without developer explanation.

## 2. BINDING EXECUTION CONTRACT
The full specification is authoritative. Own integrated usability; do not rebuild the domain systems or hide their failures behind UI.

## 3. PURPOSE
Unify the old exploration and UI goals around player knowledge and the now-working RPG/adaptive loop.

## 4. PREREQUISITES
G10 creator/origin; G16 adaptive loop and all transitive core/living-world dependencies. Domain goals already provide functional controls and truthful states.

## 5. SCOPE
Persistent discovery/navigation, journal/map/compass, HUD and generated text, integrated onboarding, modal behavior, settings and practical accessibility.

## 6. OUT OF SCOPE / DEFERRED
World topology G05–08; domain transaction/quest defects return to G09–16; final audio/art/pacing G18; new unsupported social/profession screens deferred. No infinite prerendered world map.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; independent accessibility/text/layout and newcomer-flow checks may use Luna High subagents. Sol High only if a discovered authority migration is unavoidable, under its owning goal.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Map knowledge is a player projection of semantic world truth. Known directions may reference unloaded features; undiscovered secrets are not revealed just because the generator knows them. UI edits never become gameplay authority.

## 9. REQUIRED WORK
1. Persist discovered settlement/site/region/continent IDs and source of learned directions. Build bounded map/frontier views and clear compass/journal guidance from existing queries, stable paths and authorized quest knowledge.
2. Integrate health/resources/status, interaction, inventory/equipment, skills/spells, offers/titles/classes, quests, dialogue transcript, known standing/awareness, retained alignment axes, navigation, pause/profile/save/load and defeat controls. Wrap/scale long generated names and show meaningful errors.
3. Teach progressively from supported creator/origin and Vey's hut: movement/look, E interaction, dialogue/objectives, combat/ability, loot/equip, shrine/recovery, lockpicking/mimic where retained, adaptive offer, first road. Narrative/rewards come from accepted origin/content.
4. Make cursor, pause, modal nesting, back/close and key ownership consistent. Prevent attacking/interacting through menus and stuck input after origin/dialogue/loading.
5. Persist master/music/effects volume, sensitivity, supported display/resolution/quality options; support keybind display and remapping where input stack permits. Provide readable text/UI scaling, subtitles/dialogue history and reduced camera-motion/flashing options. If a control cannot work, hide it and record the limit.
6. Present bounded recoverable loading/model/save failures in player language with actionable retry/back controls while retaining detailed local diagnostics.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
An unfamiliar tester or documented independent manual walkthrough completes the opening and both adaptive choice UI paths without developer explanation. Test long generated text, low/high supported resolutions, keyboard/mouse modal transitions, setting persistence and discovered/undiscovered/unloaded quest destinations. Save/quit/reload discovery and re-follow a route.

## 11. DELIVERABLES
Integrated UI/onboarding/navigation, settings/accessibility coverage list, discovery records and usability issue/acceptance receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G18 receives a complete player-facing experience to tune; G19/G20 receive exact normal UI-driven flows rather than debug-only paths.

## 13. COMPLETION GATE
A fresh tester can start, learn, fight, interact, trade, follow quests, understand offers, navigate, save/load and change supported settings without developer help. Root mechanics remain owned upstream; unresolved normal-play blockers prevent this gate.

---

# /goal — Curate audiovisual feedback, content density, balance, and opening pacing

## 1. GOAL
Turn the integrated beta into a coherent, responsive, well-paced RPG experience across multiple seeds.

## 2. BINDING EXECUTION CONTRACT
This full specification is binding. Own tuning and presentation finish of accepted systems; no new large feature architecture.

## 3. PURPOSE
Combine audiovisual and experiential curation without making polish a second owner for broken world or gameplay systems.

## 4. PREREQUISITES
G16 meaningful adaptive loop and G17 usable normal-player flow; all transitive domain acceptance receipts.

## 5. SCOPE
Combat/interaction/discovery/offer feedback, ambience, animation presentation, restrained supported alignment cues, world composition review, encounter/economy/progression pacing and seed/playstyle matrix.

## 6. OUT OF SCOPE / DEFERRED
Root terrain/streaming/quest/save defects go to their original goals; expanded cinematic art, all asset libraries, all archetype transformations, huge city content catalogs and new combat/profession systems are deferred. G19 owns exhaustive faults/soak, G20 release decision.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may run independent seed or presentation reviews. Astra Light only categorizes bounded reports; Sol High is not routine polish staffing.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Use approved semantic VFX/SFX/animation families and existing assets. Never make visual timing a second damage authority. Fix systemic placement/pacing rules, not hidden coordinate exceptions around one seed.

## 9. REQUIRED WORK
1. Finish attack/hit/damage/death, spell/ability, interact/loot/equip, quest/discovery/offer and meaningful social feedback. Avoid camera locks, constant audio loops and unreadable particle clutter.
2. Blend region/site ambience for settlement, wilderness, water, highlands and interiors; preserve subtitle/volume/accessibility controls. Profile dense combat and settlements against declared audiovisual budgets.
3. Review floating/buried/clipping props, blocked access, water intersections, palette seams, repetition, empty forests, overclutter and roads to nowhere. Assign algorithmic defects upstream; tune already-correct semantic density/palette rules here.
4. Curate a safe origin, first NPC/objective, manageable danger, useful reward/equipment decision, meaningful adaptive candidate/offer and social consequence. Preserve quiet travel and deliberate empty wilderness between discoveries.
5. Balance bounded prices, rewards, power, ability resources, offer cadence, detector thresholds and social magnitude. Do not reward trivial repetition with mythic status or globally propagate petty acts.
6. Select one primary beta seed and at least three regression seeds with varied geography/content. Review cooperative, aggressive, exploratory, quest-focused, off-road and supported socially disruptive playstyles using fresh profiles. Ensure the approximate large-POI pacing target respects geography and travel time rather than fixed quotas.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Record at least a two-hour primary-seed normal-play curation session and shorter targeted sessions on all three regression seeds, with opening/route/settlement/combat/adaptive evidence. Compare repeated starts, reward cadence, meaningful choice and visual/audio captures. This is experiential acceptance, not the complete multi-hour fault/soak suite.

For A12/A14, measure useful opportunities and accepted outcomes per playstyle, time from meaningful behavior to offer, perceived waiting, repetition/farming and player ability to explain the connection. Do not optimize raw call count or tokens/second as a substitute for useful gameplay. Confirm title/class consequences are perceptible and generated identities remain coherent. Instrumentation is supplied by G03; integrated hardware/retention benchmarking belongs to G19.

## 11. DELIVERABLES
Versioned beta balance/presentation settings, seed/playstyle matrix, curation captures, bounded known art limitations and upstream defect handoffs.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G19 receives a feature-complete candidate, fixed seed suite and measured provisional budgets. Feature additions stop; faults/performance and release work remain.

## 13. COMPLETION GATE
Primary opening and representative continued play are coherent, readable and purposeful; regression seeds demonstrate systemic generation and adaptive variety. No normal-play softlock or inaccessible required content is waived as polish debt.

---

# /goal — Harden persistence, performance, failure recovery, and long sessions

## 1. GOAL
Prove the complete beta remains recoverable and resource-bounded through extended play and expected failures.

## 2. BINDING EXECUTION CONTRACT
The entire specification is binding. Own cross-system reliability and performance; G20 alone certifies and packages the release.

## 3. PURPOSE
Exercise the integrated game at scale after individual correctness and the player experience are established.

## 4. PREREQUISITES
G18 feature-complete curated candidate and seed/hardware proposals; all earlier domain receipts and fault fixtures.

## 5. SCOPE
Growing saves/frontier, snapshots and auxiliary memory recovery, supported-speed/diagnostic stress, full-frame/resource budgets, failure injection, migration and multi-hour soak.

## 6. OUT OF SCOPE / DEFERRED
New features, redesign for elegance, broad imported-package warning cleanup and release publication. Return domain defects to their owner, integrate repairs and rerun affected acceptance; never silently weaken a gate to finish.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents run isolated seed/fault suites with separate profiles. Sol High reviews corruption, deterministic mismatch, migration or lifecycle root causes; Luna Max/Extra High handles bounded performance debugging.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Use actual production startup and built player as well as Editor evidence. Protect real user saves; fault injection operates on copied disposable fixtures. Measure total frames, native resources and disk costs, not only selected stopwatch scopes.

## 9. REQUIRED WORK
1. Declare target hardware/OS/resolution/quality, supported travel speed and workload. Lock H's provisional budgets before the acceptance run; record explicit evidence-based changes and rationale, never revise thresholds after observing a failure without rerunning.
2. Stress straight/diagonal/reversal/off-road travel, edge oscillation, dense sites, water crossings, distant frontier, unload/revisit and save/rebind. Keep 150/300 m/s probes separately classified; report any supported-speed implications.
3. Measure serialization, active/profile/auxiliary commit, load, overlay application and frontier growth. Optimize dirty snapshots/indexes/shards only if measurements justify it, using the existing commit protocol. One-frame delay before synchronous save does not solve a hitch.
4. Inject failed write, interrupted staging/publish, corrupt latest revision, stale profile copy, quit during generation, missing optional assets and unsupported save versions. Recover to an identified complete revision or a clear safe error without silently resetting accepted history.
5. Inject offline/timeout/malformed/cancelled/congested model work and shutdown/profile change; verify terminal outcomes, bounded queues and no stale application. Restore service and prove normal generated content resumes without rerolling accepted records.
6. Measure TerrainData, meshes/materials/GameObjects, colliders, native memory, tasks/coroutines/subscriptions, event/knowledge/canon histories, logs and prompts. Keep live caches/queues bounded; durable meaningful history may grow with indexed storage, never be erased merely to pass memory tests.
7. Run the H soak matrix with real combat, settlements, dialogue, quests, trade, adaptation and repeated save/quit/reload, including supported legacy migrations. Capture profiler traces and resource samples before/after repeated travel loops.

8. Add 10/50/100/500-hour-equivalent deterministic state-growth fixtures (A09, A13, A15): unique actions/targets, meaningful mutations, named sites, NPC facts/old promises, completed content and profile revisions. Measure per-role input tokens, relevant-fact recall/knowledge filtering, canonical/index/disk size, query latency, serialization/publication and reload cost. Context ceilings must stay independent of playtime without losing accepted truth or unresolved required evidence. Synthetic histories test scaling, not hundreds of hours of actual play; retain all existing H real endurance sessions.
9. Establish the actual configured-model/shared-game baseline: pin model/hash/quantization/tokenizer/backend, prompts/schemas, canonical states/seeds, assets and target hardware. Measure warm/cold time to valid output, queue/load/swap/prefill/decode where exposed, total tokens including failed attempts, structural/domain/stale rejections, retries/repairs/fallbacks, VRAM/RAM/CPU/GPU high-water, full-frame p95/p99/maximum and save cost. Missing model access/metrics is NOT VERIFIED, not a passed benchmark; model-off gameplay/replay is a separate test.
10. Before adopting an additional model/embedding/adapter/backend, compare it against the simpler existing model with isolated role prompts and deterministic/ID-tag-lexical tools on identical semantic facts, validators and quality requirements (A19). Include player interpretation, mechanic, region, settlement, quest, NPC, world-event and dialogue/memory cases relevant to the change; charge all repair/load/swap costs. Predeclare noninferiority, resource/headroom and latency/quality gates and report uncertainty. Optional challengers and the audit's illustrative rates/sample sizes are not universal release requirements; no change is needed merely to exercise all A–F variants.
11. Evaluate retained paired revisions and full-state write amplification only after measurement (A09). Any retention/coalescing/index/shard optimization stays under G02's existing commit protocol, preserves accepted content/meaningful history and a verified recoverable revision, and is tested with crash/reload/migration faults. No deletion solely to make growth tests pass and no parallel save owner.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Meet H's safety, budget, fault and soak criteria on the exact candidate source/build and four-seed set. Confirm recovery records and deterministic signatures, not just save return values. A compile pass is supplementary only. A failure remains FAIL/BLOCKED with owner, fix and targeted rerun; summarize repeated results once.

Report ordinary gameplay with concurrent inference and model-off baselines separately from synthetic growth, cold loads and diagnostic speed stress. Reuse domain correctness fixtures rather than silently repairing the comparison baseline. Reject an optimization if reduced model time hides poorer useful-content acceptance, extra failed decisions, higher frame stalls or lost history. Optional experiments are deferred unless adoption is proposed; actual selected-model integration and existing H release budgets remain required.

## 11. DELIVERABLES
Locked performance profile, fault/migration/soak matrix, raw evidence links, resource-growth analysis, repaired defect receipts and release-candidate limitations.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G20 receives an identified reproducible candidate with no unresolved release blocker and all evidence needed for independent final normal-flow regression.

## 13. COMPLETION GATE
H's reliability/soak subset passes: no corruption, supported-travel holes, stale/duplicate actors, normal-play softlocks, runaway resources or recurring budget failures. Remaining limitations are explicitly non-blocking. This is not permission to declare beta released.

## Binding final acceptance appendix (H)

G20 is the final authority. Every row needs build/source ID, hardware/configuration, seed/profile, steps, expected/actual result, evidence link and PASS/FAIL/BLOCKED/NOT YET TESTABLE. Only PASS satisfies essential release criteria. Earlier valid receipts may be reused only for unchanged relevant code/configuration and an identified build; final ordinary-flow/clean-install evidence is fresh.

| Acceptance area | Required proof |
|---|---|
| Clean install/startup | Packaged build launches through actual title path; clean profile creation and profile switch are correct; exactly one player/state/service owner; no development fixture required. |
| Character/origin | Guided and authored origin, explicit confirmation/cancellation, supported species/appearance, safe hut-first arrival; preview and reload match; grants occur once. |
| World determinism | Primary and three varied regression seeds; semantic hash/feature identity/accepted bindings stable after opposite exploration order and restart; compatible old V1/V2 content preserved or explicit supported migration. |
| Terrain/traversal | Continuous normal-speed walk/run/dash, diagonal/reversal/off-road, slopes, roads, water/bridge and origin seams; no missing collision, holes or fall-safety intervention counted as success. |
| Streaming/sites | Unload/revisit and fresh frontier; bounded live ownership; no stale publication/duplicate objects; representative multi-cell feature retains one identity/layout; no blocked required entrances. |
| Core RPG | Both camera modes; attack separate from interact; hit/damage/death/recovery; loot/use/equip/slots and supported abilities/resources/cooldowns; no lost/duplicated items or ghost player. |
| Population/quests/dialogue | Residents and dead/changed status persist; authorized knowledge differs by NPC; supported quest patterns and branching choice complete; rewards once; transcripts/memory and later reaction survive restart. |
| Economy/social | Earn/buy/sell/service and stock persistence; private versus witnessed incident produces plausible local knowledge; standing/awareness, relationship and independent moral/spiritual axes/history persist. Broad global social simulation is not claimed. |
| Defining adaptive loop | At least two ordinary-play behavior families produce explainable eligible candidates, real generated offers and useful accepted mechanics/opportunities; title/class effect and one persistent NPC/world response; decline/incubate/retry safe. No developer grants or fixed final-name pools substitute for generation. |
| LLM integration | Real configured-model success separately from offline fallback; bounded timeout/malformed/queue/cancel handling; profile-switch stale rejection; movement and accepted content continue without inference; recovery does not regenerate accepted records. |
| Persistence/reconstruction | Save→quit→reload→revisit preserves identity, accepted generated payloads, origin/appearance, inventory/equipment, abilities/offers, quest progress/rewards, NPC memory/status, social history, discovery and feature mutations. Test harvested/deleted/opened and spawned/changed content where retained. |
| Recovery/migration | Interrupted staging/commit, corrupt latest snapshot, stale shared copy and unsupported version yield complete known-good revision or explicit safe failure, not silent reset/mixed profile. Supported legacy saves migrate idempotently. |
| UX/accessibility | Normal tester can navigate opening and core/adaptive flows; long text fits; modal input safe; settings persist; supported readability/subtitle/motion options work; unknown map/knowledge stays hidden. |
| Performance/resources | Locked target hardware and budgets below; measured full frames, save/generation/apply/native resources; no runaway active memory, tasks, requests or log/history growth. |
| Packaging/handoff | Version/beta/date and schema/generator/manifest identity; package checksum; clean installation/restart; controls/model setup/recovery/limitations/reporting guide; local diagnostic bundle excludes sensitive text by default, no automatic uploads. |

Provisional budgets inherited from the world design plan: ordinary exploration targets 60 FPS (16.6 ms), ordinary main-thread streaming work approximately 1–2 ms/frame, and no normal-travel activation hitch above 50 ms. G19 records target hardware/OS/resolution/quality, sample windows and whether GPU/CPU bound; lock explicit p95/p99 frame, peak resident memory and save-latency limits before testing. Initial normal-travel p95 frame target is 16.6 ms and p99 33.3 ms on that declared configuration; disclose any accepted change with measured rationale and rerun. Do not use a 50 ms emergency slice ceiling as the ordinary streaming allocation. Save commit timing's historical 100 ms gate remains recorded, but total-frame stalls and recoverability determine the final design; moving work off-thread must preserve snapshot consistency.

Minimum endurance matrix: one four-hour primary-seed session and one two-hour different-seed session on the candidate, with combat, dialogue, quest, trade, adaptive generation, frontier travel and at least three save/quit/reload cycles in each; targeted opening/seam/reload checks on the remaining two seeds. Run repeated 30-minute unload/revisit loops after warm-up: live cell/entity/task counts remain within configured caps; resident memory returns within 10% of the comparable warmed baseline after settling, or an identified bounded cache growth is measured against its explicit cap. Durable on-disk history may grow with real events; unbounded live caches may not. Record cadence/resource samples and profiler traces, not just beginning/end screenshots.

Final blockers include any reproducible routine crash, save corruption, mixed identity, normal-play softlock, missing terrain/collision, impossible required objective, duplicated grant/NPC, lost appearance/accepted content, adaptive mechanic that does not execute, model outage blocking ordinary play, or severe recurring performance failure. Cosmetic limitations may ship only when documented and outside these criteria. Record final READY/NOT READY and the exact package; the roadmap itself makes no readiness claim.

---

# /goal — Certify, package, and hand off the closed beta

## 1. GOAL
Certify the complete production experience and deliver a reproducible closed-beta build with tester guidance.

## 2. BINDING EXECUTION CONTRACT
This entire specification and H's acceptance matrix are binding. This is the sole closed-beta certification authority; earlier diagnostics or subsystem receipts cannot replace it.

## 3. PURPOSE
Prove the game delivers its defining promise through the ordinary player flow on a clean installation.

## 4. PREREQUISITES
G19 reliability candidate and all G01–G18 acceptance receipts, supported-scope/deferred-feature register, locked hardware/budgets and four-seed suite. No unresolved critical blocker.

## 5. SCOPE
Final normal-flow regression, evidence freshness, compatible-save replay, packaging/build identity, clean-install check, local diagnostics and tester handoff.

## 6. OUT OF SCOPE / DEFERRED
New features, changing beta scope to hide a failed gate, external distribution or uploading tester data without explicit authorization. A domain failure returns to its owner and invalidates affected evidence; do not rebuild that subsystem opportunistically inside certification.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High owns final decision after reviewing every receipt. Luna High subagents may independently execute seed/checklist subsets; Sol High audits only high-risk unresolved save/determinism evidence. No subagent may declare overall readiness.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Use an ordinary clean profile and the packaged production build, without developer grants/teleports/fallback overrides as substitutes for acceptance. Developer fault/stress evidence supplements the player flow. Record exact executable/source/schema/generation/manifest identities.

## 9. REQUIRED WORK
1. Execute H: clean install→creator/species/appearance→guided or authored origin→hut/world→travel→NPC/quest→combat/loot/equipment→progression/adaptive offer→trade/social consequence→continued streaming→save/quit/reload→revisit changed content→fresh frontier.
2. Verify both origin routes and confirmation/cancellation on ordinary profiles; verify two natural behavior paths including real successful model generation, supported mechanics and persistent response. Demonstrate continued movement and accepted content with the model unavailable.
3. Run primary plus three regression seeds, supported legacy migrations and representative extended play. Reuse current G19 soak traces only if the candidate and relevant settings are unchanged; rerun affected suites after fixes.
4. Reject any known routine crash, corruption, normal-play softlock, inaccessible required objective, missing collision, identity/appearance loss, duplicate rewards/residents, broken adaptive execution or severe recurring stall.
5. Build/package the exact candidate with beta/version/date and accessible schema/generation diagnostics. Remove ordinary access to developer-only fixtures; retain safe local bug-report collection.
6. Provide install/model setup/recovery instructions, controls, origin choices, adaptive premise, supported systems, limitations, save compatibility and local bug-report instructions. Diagnostic bundle includes non-sensitive profile token, seed/location/build and recent errors, with raw dialogue/custom text excluded by default; uploads require consent.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Independently launch the packaged build from a clean directory/profile, complete the normal path, restart and compare consequences. Every H row needs expected/actual result and evidence. NOT YET TESTABLE, stale receipts and missing hardware/model access cannot count as PASS.

Audit integration acceptance: require the applicable owning-goal receipts, selected-model normal-generation proof, bounded per-role context/growing-state measurements and model-off accepted-content replay on the release candidate. Verify two ordinary-play adaptive outcomes and actual mechanics, NPC identity/knowledge, quest schema/objectives and truthful committed consequences. Synthetic 500-hour-equivalent fixtures do not replace H endurance; optional unadopted models/embeddings/adapters/backend experiments are not release blockers. No source-only audit, mock response, fallback-only fixture or documentation completion counts as runtime PASS.

## 11. DELIVERABLES
Versioned closed-beta package and checksum, complete acceptance matrix, seed/profile reproduction manifest, known limitations, local diagnostic bundle tool/instructions and concise tester guide.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
External testers receive the package and guide when distribution is authorized. Feedback becomes new scoped work; deferred features remain post-beta. Preserve this candidate's source and artifacts for reproduction.

## 13. COMPLETION GATE
All essential H criteria pass on the identified build; fresh installation and real generated/adaptive play work; recovery/soak evidence is current; no release blocker remains; package and tester handoff are complete. Report READY or NOT READY with reasons, never readiness inferred from compilation or G01.

## Binding final acceptance appendix (H)

G20 is the final authority. Every row needs build/source ID, hardware/configuration, seed/profile, steps, expected/actual result, evidence link and PASS/FAIL/BLOCKED/NOT YET TESTABLE. Only PASS satisfies essential release criteria. Earlier valid receipts may be reused only for unchanged relevant code/configuration and an identified build; final ordinary-flow/clean-install evidence is fresh.

| Acceptance area | Required proof |
|---|---|
| Clean install/startup | Packaged build launches through actual title path; clean profile creation and profile switch are correct; exactly one player/state/service owner; no development fixture required. |
| Character/origin | Guided and authored origin, explicit confirmation/cancellation, supported species/appearance, safe hut-first arrival; preview and reload match; grants occur once. |
| World determinism | Primary and three varied regression seeds; semantic hash/feature identity/accepted bindings stable after opposite exploration order and restart; compatible old V1/V2 content preserved or explicit supported migration. |
| Terrain/traversal | Continuous normal-speed walk/run/dash, diagonal/reversal/off-road, slopes, roads, water/bridge and origin seams; no missing collision, holes or fall-safety intervention counted as success. |
| Streaming/sites | Unload/revisit and fresh frontier; bounded live ownership; no stale publication/duplicate objects; representative multi-cell feature retains one identity/layout; no blocked required entrances. |
| Core RPG | Both camera modes; attack separate from interact; hit/damage/death/recovery; loot/use/equip/slots and supported abilities/resources/cooldowns; no lost/duplicated items or ghost player. |
| Population/quests/dialogue | Residents and dead/changed status persist; authorized knowledge differs by NPC; supported quest patterns and branching choice complete; rewards once; transcripts/memory and later reaction survive restart. |
| Economy/social | Earn/buy/sell/service and stock persistence; private versus witnessed incident produces plausible local knowledge; standing/awareness, relationship and independent moral/spiritual axes/history persist. Broad global social simulation is not claimed. |
| Defining adaptive loop | At least two ordinary-play behavior families produce explainable eligible candidates, real generated offers and useful accepted mechanics/opportunities; title/class effect and one persistent NPC/world response; decline/incubate/retry safe. No developer grants or fixed final-name pools substitute for generation. |
| LLM integration | Real configured-model success separately from offline fallback; bounded timeout/malformed/queue/cancel handling; profile-switch stale rejection; movement and accepted content continue without inference; recovery does not regenerate accepted records. |
| Persistence/reconstruction | Save→quit→reload→revisit preserves identity, accepted generated payloads, origin/appearance, inventory/equipment, abilities/offers, quest progress/rewards, NPC memory/status, social history, discovery and feature mutations. Test harvested/deleted/opened and spawned/changed content where retained. |
| Recovery/migration | Interrupted staging/commit, corrupt latest snapshot, stale shared copy and unsupported version yield complete known-good revision or explicit safe failure, not silent reset/mixed profile. Supported legacy saves migrate idempotently. |
| UX/accessibility | Normal tester can navigate opening and core/adaptive flows; long text fits; modal input safe; settings persist; supported readability/subtitle/motion options work; unknown map/knowledge stays hidden. |
| Performance/resources | Locked target hardware and budgets below; measured full frames, save/generation/apply/native resources; no runaway active memory, tasks, requests or log/history growth. |
| Packaging/handoff | Version/beta/date and schema/generator/manifest identity; package checksum; clean installation/restart; controls/model setup/recovery/limitations/reporting guide; local diagnostic bundle excludes sensitive text by default, no automatic uploads. |

Provisional budgets inherited from the world design plan: ordinary exploration targets 60 FPS (16.6 ms), ordinary main-thread streaming work approximately 1–2 ms/frame, and no normal-travel activation hitch above 50 ms. G19 records target hardware/OS/resolution/quality, sample windows and whether GPU/CPU bound; lock explicit p95/p99 frame, peak resident memory and save-latency limits before testing. Initial normal-travel p95 frame target is 16.6 ms and p99 33.3 ms on that declared configuration; disclose any accepted change with measured rationale and rerun. Do not use a 50 ms emergency slice ceiling as the ordinary streaming allocation. Save commit timing's historical 100 ms gate remains recorded, but total-frame stalls and recoverability determine the final design; moving work off-thread must preserve snapshot consistency.

Minimum endurance matrix: one four-hour primary-seed session and one two-hour different-seed session on the candidate, with combat, dialogue, quest, trade, adaptive generation, frontier travel and at least three save/quit/reload cycles in each; targeted opening/seam/reload checks on the remaining two seeds. Run repeated 30-minute unload/revisit loops after warm-up: live cell/entity/task counts remain within configured caps; resident memory returns within 10% of the comparable warmed baseline after settling, or an identified bounded cache growth is measured against its explicit cap. Durable on-disk history may grow with real events; unbounded live caches may not. Record cadence/resource samples and profiler traces, not just beginning/end screenshots.

Final blockers include any reproducible routine crash, save corruption, mixed identity, normal-play softlock, missing terrain/collision, impossible required objective, duplicated grant/NPC, lost appearance/accepted content, adaptive mechanic that does not execute, model outage blocking ordinary play, or severe recurring performance failure. Cosmetic limitations may ship only when documented and outside these criteria. Record final READY/NOT READY and the exact package; the roadmap itself makes no readiness claim.

## E — Target terrain / world architecture

### Authority and data flow

`World identity → macro fields/feature graph → semantic cell plan → terrain/content materialization → streaming lifecycle → persistent mutation overlay before gameplay publication`.

This describes dependencies, not a second runtime stack. G02 owns identity/commits; G05 owns semantic facts; G06 realizes terrain/routes/water; G07 schedules and reconstructs; G08 builds sites/environment. Later domain goals attach durable gameplay state by feature ID. Physical GameObjects never become the authoritative world.

### World identity and deterministic seeding — G02/G05

Extend the existing save-owned plan envelope with world ID/seed, generator version, semantic schema, hash algorithm version, coordinate specification, accepted spatial artifact ID/hash and asset manifest versions. Continue selecting one complete accepted V2 or persisted V1 under V2Preferred until migration explicitly retires a version. The current V2 envelope already has seed, compiler, fingerprint and validation hashes; preserve its acceptance checks.

Use signed logical cell coordinates and a double-precision absolute position or integer cell plus bounded local offset. Current continuation cells are 128 m; preserve this for old saves. Floor division must handle negative positions consistently. Rendering-origin offsets are transient; rebasing updates transforms/physics together and never changes IDs, logical positions, quest destinations or seed derivation. A floating-origin implementation is required before supported travel exceeds demonstrated precision bounds; do not add an unused spatial layer merely for neatness.

Derive independent seeds as a versioned stable hash of world seed, domain tag, owner coordinate/feature ID and generator version. Canonicalize encoding, ordering and numeric serialization. Do not use process-dependent string hashes, time, random GUIDs, mutable registry order or a shared sequential random stream for baseline generation. Existing legacy hashing remains version-pinned; replacing it requires explicit migration. Quantized semantic geometry/hashes must agree across supported builds; define a physical floating-point tolerance separately.

The seed+coordinate+version invariant applies to untouched baseline. Accepted authored/V1/V2 layouts, persisted LLM content and mutation revisions are explicit authoritative exceptions; baseline regeneration must not overwrite them.

### Spatial hierarchy and storage — G05/G07

Use only World → macro region/feature ownership → semantic/streaming cell → optional site sector. Continents and administrative/cultural regions are features with stable membership/query rules, not necessarily additional loaded grids. Reuse one 128 m cell coordinate key for semantic lookup and streaming where possible. A site sector exists only to bound a large feature's materialization; it does not own another settlement identity.

Analytically derive low-frequency elevation, land/ocean tendency, climate/moisture and broad ecological suitability. Lazily compile a bounded macro neighborhood for watersheds, sites, routes and influence. Eagerly compile the opening region and origin seam envelope. Cache immutable results with bounded eviction. Persist accepted generated names, non-reproducible choices, immutable accepted site layouts/bindings, content provenance, discovery and mutations. Do not allocate a planet-sized raster or retain every untouched visited cell forever.

A feature owner is chosen from its stable anchor and versioned owner-region grid, not first discovery. Queries include a bounded halo sufficient for crossing features. Use deterministic tie-breaks for overlap/reservations. Long rivers/roads use linked regional segments with one parent identity and deterministic border contracts; avoid recursively generating unlimited upstream regions to answer one cell.

### Macro geography and semantic plans — G05

Generate coherent landmasses/coastlines, mountain/ridge systems, valleys/plains/basins and climate/moisture trends at frequencies larger than cells. Continent membership follows stable connected macro landmasses. Watershed and civilization pressure influence habitation; cultural and kingdom influence are separately seeded fields/records, not synonyms for biome. A bounded beta need not simulate global politics or ocean travel to retain these identities.

Adapt existing V2 terrain fields, route corridors, hydrology and site anchors into one semantic query result. A cell plan contains versioned identity, owner region/continent, landform regime, elevation intent/ruggedness, temperature/moisture, ecology weights, civilization/danger/cultural influence, palette/density inputs, intersecting feature IDs, reserved sites, local seeds and edge contracts. Derived heights/vegetation samples remain cacheable outputs; accepted topology, site identity and mutation references remain durable. Plan hashes exclude visit times and loading states.

The current continuation uses `continuous_world_cell_v5`, `continuous_edge_v3` and a finite-origin blend toward seeded macro relief; this is a migration starting point, not proof of a unified world graph. Preserve old accepted terrain and introduce a versioned transition collar. Resolve accepted/continued/absent/synthetic feature policy once, then give all consumers the same result. Current per-edge and per-cell fallback differences must not survive as competing ownership policies.

### Terrain and collision — G06

Retain Unity Terrain/TerrainData for the surface. Current origin terrain uses a 1024 m domain, 140 m height range and 513 samples; continuation sampling derives from origin spacing. Accepted V2 sampling bypasses the legacy 8×8 tile-profile height branch. Keep both versioned for compatible saves; do not blend their ownership accidentally.

Sample absolute coordinates through macro elevation → regional shape/ridges → valleys/basins → hydrology cuts → local detail → road grading → reserved site accommodation. Water, roads and sites consume the same constraints; bounded earthworks cannot invalidate basin flow or a neighboring edge. Shared edge positions produce identical sample values; compare normals, paint and collision as well as height. Use local approved meshes for caves, bridges and overhangs, with explicit entrances/holes and collision transitions. Avoid runtime erosion or global fluid simulation.

### Hydrology and routes — G05 topology, G06 realization

Hydrology records own basin, source, directed flow, confluence and sink IDs. Rivers/streams are linked cross-cell features; lakes/ponds are basin polygons/surfaces and marshes/wetlands ecological/wetness regions. Each crossing records parent water ID, position/tangent, width, bed/surface elevation, flow and upstream/downstream links. Terrain carving, water meshes, shore dressing and crossing validation consume this one contract. Preserve legitimate terminal behavior; lakes/wetlands/coasts are not blindly extended as rivers. Verify carving idempotence and no double application of accepted water.

Reserve plausible settlement/POI destinations first, then route a deterministic regional site graph using slope, water and cultural/transport constraints. Roads have real endpoints or declared boundary continuations, parent route IDs, regional segments and shared edge entrances/exits. Realize local geometry within the corridor and adapt terrain. Bridges/fords belong to the route-water intersection contract. Queries can describe destinations before their GameObjects load. Empty wilderness is valid; it does not automatically request a synthetic road.

### Sites, settlements and large features — G05/G08/G11

Support semantic villages/towns/cities, castles/forts, ruins, caves/dungeons, shrines, hostile/resource sites, natural/cultural landmarks, roadside structures and empty wilderness. Beta physical coverage is deliberately narrower and declared in G08. Each site reservation owns a stable ID, anchor, bounds/member cells, route entrances, exclusions, cultural intent and allowed expansion envelope. One immutable accepted layout references asset IDs/versions, transforms, structure/interior graph and gameplay anchors. Runtime sectors may unload independently, but one resident/loot/service owner exists per stable entity.

For multi-cell cities/castles/dungeons, compile the shared footprint and entrances before any member cell publishes structures. Persist chosen layout/bindings rather than rerolling from a changed palette. Current sparse GeneratedSemanticSiteRecord lacks these fields; migrate it. Current streamer retention based only on persistentDeltaIds is insufficient for accepted named sites.

Treat approximately one huge POI per 30 chunks as a provisional player-travel pacing target, not modulo placement. At 128 m cells, 30 traversed cells suggests roughly 3.84 km along a route, not a universal area density. Use seeded regional candidates, minimum-distance/exclusion rules, geography, access and performance envelopes. G18 tests travel time/quiet intervals; G08 proves one representative multi-cell feature. A full catalog of giant cities and dungeons is post-beta.

### Palettes and environmental density — G04/G08/G18

Select from approved versioned asset families by semantic compatibility, then deterministic variation. Separate landform, ecology, climate and culture. Neighbor-aware weights allow ecological transitions without forcing identical architecture. Stable neighborhood selection handles repetition; mutable recent-use history must not change baseline when loading order changes.

Layer canopy, understory, shrubs, ground vegetation, rock/boulders, deadfall/debris, shoreline, roadside, outskirts and storytelling. Density responds to moisture/slope/land use and budgets, not tiny fixed per-cell quotas. Site/route/water/camera masks constrain placement. Validate pivot/scale/grounding, colliders, materials, clipping, culling and sightlines. Preserve quiet space and readable routes. G18 tunes density only after G08 proves safe placement.

### Materialization, streaming and reconstruction — G07

The existing builder/streamer remains coordinator. Work progresses through planned → queued → generating → collision-ready → critical-content-ready → active → retained → unloading/unloaded; failures and cancellations are explicit. Plan/ground/collision precede route/site safety, essential gameplay providers, environment and decoration. Optional model work never gates these stages.

Lookahead depends on speed/direction, measured readiness latency and safety margin; diagonal travel/reversal have explicit coverage tests. Active/retained/predictive rings, queue limits and per-stage budgets are measured on declared hardware. Immutable numeric preparation may be background work; Unity objects and publication remain on the main thread. Epoch/profile/world/revision tokens reject stale results. Unload destroys only owned objects and releases TerrainData/material/mesh resources without discarding semantic truth.

Reconstruct pinned deterministic baseline and accepted artifacts; resolve stable feature bindings; load overlay revision; apply tombstones/state/added-feature records idempotently; publish collision and gameplay only when valid. Overlays cover destroyed/harvested resources, opened containers, NPC status, quest/settlement consequences and spawned accepted features. Player construction/terrain editing remains unsupported unless an explicit retained mechanic registers a typed mutation; do not imply arbitrary building is implemented.

Use G02's coherent snapshot manifest for player/world and auxiliary memory. Index mutations by feature and owner region. Compact replay history only while preserving current state, meaningful canon and needed audit/receipt IDs. G19 determines whether dirty shards/journals are justified; do not serialize every generated physical object or add a competing save store.

### LLM boundaries and verification — G03/G05–08/G16/G19/G20

Deterministic geography establishes facts first. The LLM names/interprets/enriches those facts and proposes player-responsive quests, identities, culture/lore, dialogue and progression through validators. It cannot choose arbitrary coordinates/assets, modify accepted spatial facts implicitly, or delay ground/collision/streaming. Preserve accepted content without regeneration; new narrative revisions are explicit commits.

Determinism tests compare semantic hashes, edges, graph IDs, accepted layouts and overlays across repeated runs, shuffled exploration, negative/diagonal coordinates, reload and compatible versions. Terrain comparisons use declared sample tolerances; load timestamps are excluded. Traverse actual player physics through origin boundaries, roads/water crossings, steep/quiet/dense cells, reversal and multi-cell sites. Report fall-safety intervention separately; teleport recovery is not proof of successful traversal. Each world goal owns bounded acceptance; G19 adds endurance and G20 certifies the complete normal path.

## F — Migration map

| Existing system | Classification | Reason and migration owner |
|---|---|---|
| PlaySafe/title scenes and TutorialAutoBootstrap | PRESERVE + ADAPT | Serialized production connection exists. G01 instruments; G02 makes lifecycle ownership explicit. No scene reconstruction. |
| PlayerState/WorldState and managers | PRESERVE + MIGRATE | Actual state owners; G02 adds versioned references/paired commits and domain goals extend through migrations. |
| YQProfileSaveSystem shared copies/profile documents | MIGRATE | Preserve public flow but publish coherent snapshot revisions; sequential file copy is not a transaction. G02 owns correctness, G19 scale. |
| Mutable PlayerProfile/UpgradeOfferManager compatibility | WRAP → MIGRATE → DEPRECATE | Production bootstrap attaches PlayerProfile. Move real callers to PlayerState views; no permanent dual writable skill authority. Delete only after reference/serialization/migration evidence. |
| YQInvestor player/combat/equipment classes | PRESERVE + ADAPT | Live production code despite names. G09 fixes supported behavior; rename not required. |
| YQWorldGenerationService and accepted V1/V2 router | PRESERVE + ADAPT | Preserve accepted artifacts and bounded generation; G05 separates baseline geography from narrative enrichment. |
| V2 spatial envelope/blueprint/contracts | PRESERVE + MIGRATE | Existing seed/compiler/acceptance hashes are useful. Extend global feature ownership and versioned seams; never reinterpret accepted data. G02/G05. |
| GeneratedWorldTerrain/TilePlan | PRESERVE + ADAPT | Unity Terrain remains surface authority. Version-pin legacy non-V2 branch until compatible saves no longer require it. G06. |
| ContinuousWorldCellAuthority/FeatureAuthority | ADAPT; REPLACE local fallback policy | Keep coordinate/edge/query implementation, replace contradictory per-edge/per-cell feature synthesis with G05 unified decisions. G06 realizes those facts. |
| PlayerFollowingSemanticChunkStreamer | PRESERVE + ADAPT | Existing lifecycle/epochs are useful. G07 repairs budgets, durable bindings and overlay-safe eviction; G19 hardens growth. |
| Sparse semantic sites and delta-ID retention | MIGRATE | Add accepted layout/binding/version and multi-cell owner/member data before frontier population expands. G05/G07/G08. |
| Reviewed manifests, registry, site catalog, asset intake | PRESERVE + ADAPT | Assign one owner per stage and regenerate derived catalogs from reviewed source. G04. No imported pack deletion. |
| ProceduralSettlementLayout/spatial materialization | PRESERVE + ADAPT | Existing construction mechanisms; G08 integrates reserved footprints, circulation and sectors. |
| Generated NPC plans, NpcRecord, agents | MIGRATE + WRAP | G11 separates immutable generated identity/role from mutable status/knowledge; scene objects are views. |
| LLMClient/YQLlmRequest | PRESERVE + ADAPT | G03 fixes terminal outcomes/stale guards; no new scheduler. |
| ActionRecorder/Accumulator/Rollup/ProgressionMath | PRESERVE + MIGRATE | G15 adds stable event IDs and independent cursors, preserves old counter adapters temporarily. |
| ProgressionThinkCycle/DecisionApplier/offers | PRESERVE + ADAPT | G15 replaces prose eligibility; G16 integrates typed candidates/acceptance. Existing curation and offer states remain useful. |
| Prose-derived quest reward calculator | REPLACE after migration | G12 freezes typed reward contracts and preserves completed receipts; do not recalculate old rewards. |
| Baseline currency replenishment | REPLACE | G02 removes normalization side effects in favor of explicit one-time grants; G13 owns economy. |
| NpcDialogueAgent/DialogueThinkService/memory stores | PRESERVE + ADAPT | Keep ownership guards/profile scoping; G11/G12 introduce typed knowledge and validated actions. |
| Legacy scatter builder branch and prototype roots | DEPRECATE; DELETE AFTER MIGRATION only if proven unused | CompiledWorld is selected, but editor/test/reflection/save dependencies must be traced. Removal is optional, never a gate based on naming. |
| DirectorThinkCycle/older director DTOs | WRAP / DEPRECATE conditionally | Actual callers decide; retain useful adapters until G02/G15/G16 consolidate commit/evidence ownership. Do not assume obsolete from historical report. |
| Baseline diagnostics/menu/regression and specialized harnesses | PRESERVE + ADAPT | G01 fixes skipped-runtime PASS and fixture labeling; G19/G20 compose, not replace, meaningful specialized checks. |
| ScriptDump/project export artifacts | PRESERVE as documentation | Non-runtime classification is not deletion authorization. |

No unconditional asset/code deletion is recommended. DELETE AFTER MIGRATION requires zero runtime/scene/prefab/SO/editor/test/reflection/save compatibility dependents, a rollback artifact, and an owning goal's evidence.

## G — Closed-beta critical path and scope

The dependency-critical foundation is G01→G02. The world branch then runs G05→G06→G07→G08, with G04 assets feeding G06/G08; G09 core play can start after G07 and G04. G03 model infrastructure runs independently after G02. The living-world chain is G08+G09→G11→G12→G13→G14. G15 evidence can run after G03/G09/G11 while the quest/economy/social branch proceeds. G16 joins both branches. G10 creator runs after G03/G04/G08/G09 and joins G16 at G17. The finish is G17→G18→G19→G20. Without measured task durations there is no defensible single elapsed-time critical path; these are dependency-critical joins, not subjective importance rankings.

True blockers: uncertain writable state ownership; mixed profile snapshots; unstable world identity; terrain/collision discontinuity; nondurable sites/mutations; unsafe player executors; missing typed objective/reward/knowledge contracts; lossy behavioral evidence; adaptive outcomes that are only text; any unsupported normal-flow UI or routine data loss. Historical 300 m/s failures require diagnosis against supported speed, not automatic removal or automatic release rejection at an irrelevant speed.

Parallelizable work: G03/G04/G05 after G02; G09 and G08 after their prerequisites; G10 beside the living-world branch; G15 beside G12–14. Use isolated worktrees and stable interfaces; one primary owner integrates persistent schema changes. Later testing may parallelize by isolated seed/profile, not shared active saves.

| Scope class | Retained decision |
|---|---|
| Essential beta | Coherent generated/streamed world; one polished asset family; safe origin/creator; normal RPG verbs/combat/inventory/equipment/abilities; stable residents; supported quests/dialogue; useful trade; bounded local consequences/relationships; two natural adaptive behavior paths; generated grounded identities; persistent consequences; readable UI; fault recovery and measured performance. |
| Desirable, deferrable | Extra species/morphs, more asset families, additional quest patterns, elaborate spell combinations, shovel/treasure variant, larger romance content, alignment manifestations beyond supported subtle effects, numerous giant POIs. |
| Dangerous scope expansion | Global civilization/economy/law simulation, always-on unloaded NPC AI, every profession, unlimited procedural mechanics, cinematic transformations, giant city catalog, genre/world rebuilding, global erosion/fluid simulation, arbitrary player terrain editing. |
| Prototype-only / obsolete policy | Diagnostic shortcuts remain development-only; old scatter publication is deprecated where not selected; PlayerProfile duplication and prose-based mechanics migrate. Names alone never justify deletion. |

Old requirements deliberately narrowed: G10's full continental/species/people reputation spread becomes extensible IDs plus local beta scope (G14); exhaustive moral archetype manifestations are deferred while independent axes/history remain; old G12's full pattern list is capability-gated; old G05's broad library audit becomes selected-family certification; old G06's all-world convergence splits four ways; old G17's save correctness moves early while endurance stays late. The approximately-30-chunk huge-POI aspiration becomes contextual spacing plus one multi-cell proof. These are explicit closed-beta scope decisions, not silent omissions.

Non-blocking polish is limited to documented cosmetic shortcomings that do not obscure actions, impair navigation, break required assets or undermine the core promise. There is no shortcut from compile/fixture success to readiness. The shortest credible delivery path completes all retained contracts at their bounded beta scope; optional expansion never lies on that path.

## H — Final beta acceptance

G20 is the final authority. Every row needs build/source ID, hardware/configuration, seed/profile, steps, expected/actual result, evidence link and PASS/FAIL/BLOCKED/NOT YET TESTABLE. Only PASS satisfies essential release criteria. Earlier valid receipts may be reused only for unchanged relevant code/configuration and an identified build; final ordinary-flow/clean-install evidence is fresh.

| Acceptance area | Required proof |
|---|---|
| Clean install/startup | Packaged build launches through actual title path; clean profile creation and profile switch are correct; exactly one player/state/service owner; no development fixture required. |
| Character/origin | Guided and authored origin, explicit confirmation/cancellation, supported species/appearance, safe hut-first arrival; preview and reload match; grants occur once. |
| World determinism | Primary and three varied regression seeds; semantic hash/feature identity/accepted bindings stable after opposite exploration order and restart; compatible old V1/V2 content preserved or explicit supported migration. |
| Terrain/traversal | Continuous normal-speed walk/run/dash, diagonal/reversal/off-road, slopes, roads, water/bridge and origin seams; no missing collision, holes or fall-safety intervention counted as success. |
| Streaming/sites | Unload/revisit and fresh frontier; bounded live ownership; no stale publication/duplicate objects; representative multi-cell feature retains one identity/layout; no blocked required entrances. |
| Core RPG | Both camera modes; attack separate from interact; hit/damage/death/recovery; loot/use/equip/slots and supported abilities/resources/cooldowns; no lost/duplicated items or ghost player. |
| Population/quests/dialogue | Residents and dead/changed status persist; authorized knowledge differs by NPC; supported quest patterns and branching choice complete; rewards once; transcripts/memory and later reaction survive restart. |
| Economy/social | Earn/buy/sell/service and stock persistence; private versus witnessed incident produces plausible local knowledge; standing/awareness, relationship and independent moral/spiritual axes/history persist. Broad global social simulation is not claimed. |
| Defining adaptive loop | At least two ordinary-play behavior families produce explainable eligible candidates, real generated offers and useful accepted mechanics/opportunities; title/class effect and one persistent NPC/world response; decline/incubate/retry safe. No developer grants or fixed final-name pools substitute for generation. |
| LLM integration | Real configured-model success separately from offline fallback; bounded timeout/malformed/queue/cancel handling; profile-switch stale rejection; movement and accepted content continue without inference; recovery does not regenerate accepted records. |
| Persistence/reconstruction | Save→quit→reload→revisit preserves identity, accepted generated payloads, origin/appearance, inventory/equipment, abilities/offers, quest progress/rewards, NPC memory/status, social history, discovery and feature mutations. Test harvested/deleted/opened and spawned/changed content where retained. |
| Recovery/migration | Interrupted staging/commit, corrupt latest snapshot, stale shared copy and unsupported version yield complete known-good revision or explicit safe failure, not silent reset/mixed profile. Supported legacy saves migrate idempotently. |
| UX/accessibility | Normal tester can navigate opening and core/adaptive flows; long text fits; modal input safe; settings persist; supported readability/subtitle/motion options work; unknown map/knowledge stays hidden. |
| Performance/resources | Locked target hardware and budgets below; measured full frames, save/generation/apply/native resources; no runaway active memory, tasks, requests or log/history growth. |
| Packaging/handoff | Version/beta/date and schema/generator/manifest identity; package checksum; clean installation/restart; controls/model setup/recovery/limitations/reporting guide; local diagnostic bundle excludes sensitive text by default, no automatic uploads. |

Provisional budgets inherited from the world design plan: ordinary exploration targets 60 FPS (16.6 ms), ordinary main-thread streaming work approximately 1–2 ms/frame, and no normal-travel activation hitch above 50 ms. G19 records target hardware/OS/resolution/quality, sample windows and whether GPU/CPU bound; lock explicit p95/p99 frame, peak resident memory and save-latency limits before testing. Initial normal-travel p95 frame target is 16.6 ms and p99 33.3 ms on that declared configuration; disclose any accepted change with measured rationale and rerun. Do not use a 50 ms emergency slice ceiling as the ordinary streaming allocation. Save commit timing's historical 100 ms gate remains recorded, but total-frame stalls and recoverability determine the final design; moving work off-thread must preserve snapshot consistency.

Minimum endurance matrix: one four-hour primary-seed session and one two-hour different-seed session on the candidate, with combat, dialogue, quest, trade, adaptive generation, frontier travel and at least three save/quit/reload cycles in each; targeted opening/seam/reload checks on the remaining two seeds. Run repeated 30-minute unload/revisit loops after warm-up: live cell/entity/task counts remain within configured caps; resident memory returns within 10% of the comparable warmed baseline after settling, or an identified bounded cache growth is measured against its explicit cap. Durable on-disk history may grow with real events; unbounded live caches may not. Record cadence/resource samples and profiler traces, not just beginning/end screenshots.

Final blockers include any reproducible routine crash, save corruption, mixed identity, normal-play softlock, missing terrain/collision, impossible required objective, duplicated grant/NPC, lost appearance/accepted content, adaptive mechanic that does not execute, model outage blocking ordinary play, or severe recurring performance failure. Cosmetic limitations may ship only when documented and outside these criteria. Record final READY/NOT READY and the exact package; the roadmap itself makes no readiness claim.
