# Supporting world architecture

This supporting design retains the original world/final-acceptance contracts below. Descriptions of then-current implementation are the September 16 snapshot, not fresh runtime evidence. The canonical execution files are in [goals/](goals/); [audit integration](AUDIT_INTEGRATION.md) supplies bounded follow-ups without replacing these contracts. Current execution remains [8 fix 4](<goals/8 fix 4.md>).

<!-- WORLD -->
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

<!-- MIGRATION -->
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

<!-- CRITICAL -->
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

<!-- ACCEPTANCE -->
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
