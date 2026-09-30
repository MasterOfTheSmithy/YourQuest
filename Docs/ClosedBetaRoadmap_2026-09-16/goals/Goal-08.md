# /goal — Build traversable sites, settlements, and semantic environmental density

## 1. GOAL
Turn reserved world features into coherent traversable places with stable multi-cell ownership.

## 2. BINDING EXECUTION CONTRACT
The short objective is only a summary. Own physical site composition and environment; inhabitants and services remain later work.

Current execution uses [8 fix 4](<8 fix 4.md>): R1 publication/recovery → R2 streaming/performance → R3 physical world itinerary. Fixes 1–3 are historical workflows whose acceptance requirements remain binding through fix 4. The LLM audit does not add NPC simulation, dialogue, behavior interpretation, adaptive progression or model benchmarking to G08. Address an audit finding here only when fresh evidence demonstrates that it blocks an existing G08 acceptance row; otherwise route it to its named owner in the [integration record](../AUDIT_INTEGRATION.md). No original G08 or fix-4 gate is relaxed.

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
