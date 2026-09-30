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
