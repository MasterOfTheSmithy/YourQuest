# YourQuest world-generation prompt templates

The current repository has active V1/V2 world planning, compiled-world materialization, semantic continuation streaming, boundary contracts, save/reload identity, and editor verification. Every template below still begins with owner/authority discovery because changes to these systems are senior-review work. Use the exact headings in each entry; replace task-specific values with verified symbols, seed, scene, and receipt before sending it to an agent.

## Adjacent-cell continuity defect

**WHEN TO USE**: A neighboring cell visibly or structurally contradicts an established boundary fact.
**WHEN NOT TO USE**: The issue is isolated decoration with no canonical boundary contract.
**REQUIRED INPUT**: World/cell owner, seed, coordinates, offending edge, boundary fact, reproduction evidence.
**OPTIONAL INPUT**: Plan hashes, screenshots, stage timings.
**AUTHORIZED SCOPE**: Boundary planner/validator and focused tests named by senior review.
**FORBIDDEN CHANGES**: Dropping the feature, changing seed semantics, bypassing validation, rewriting unrelated stages.
**REQUIRED INVESTIGATION**: Trace the existing persisted/seeded shared-edge authority, neighbor lookup, RNG scope, and materialization. Compare both traversal orders; no first-visited cell may invent canonical authority.
**IMPLEMENTATION CONTRACT**: The existing canonical shared-edge contract produces the same fact on both sides independently of cell traversal order.
**UNITY VERIFICATION**: Compile, fixed-seed generation, inspect both cells and Console, compare plan evidence.
**TEST REQUIREMENTS**: Two-cell boundary invariant plus traversal-order comparison.
**ACCEPTANCE CRITERIA**: Both cells honor the same required continuation across repeated runs.
**STOP CONDITIONS**: Root owner and invariant are proven; stop after three failed repairs.
**ESCALATION CONDITIONS**: No owner/contract exists or seed/generation order must change.
**REQUIRED FINAL REPORT**: Root cause, contract, files, seed/coordinates, tests, risks, next action.

## Biome/palette blending

**WHEN TO USE**: Adjacent palettes or biomes hard-cut or use incompatible transition rules.
**WHEN NOT TO USE**: An authored landmark intentionally creates a documented discontinuity.
**REQUIRED INPUT**: Palette/biome owner, neighbor pair, transition rule, seed, visual/plan evidence.
**OPTIONAL INPUT**: Blend weights, screenshots, material samples.
**AUTHORIZED SCOPE**: Palette selection/transition logic and tests.
**FORBIDDEN CHANGES**: Replacing project materials, adding arbitrary asset paths, weakening biome constraints.
**REQUIRED INVESTIGATION**: Locate palette data and neighbor comparison; determine whether transition is canonical or presentation-only.
**IMPLEMENTATION CONTRACT**: Compatible neighbors blend deterministically; deliberate discontinuity requires an explicit rule.
**UNITY VERIFICATION**: Compile, fixed-seed adjacent cells, inspect material/plan result, capture visual evidence when available.
**TEST REQUIREMENTS**: Symmetric boundary and fixed-seed blend tests.
**ACCEPTANCE CRITERIA**: Same inputs yield same transition and no unauthorized hard cut occurs.
**STOP CONDITIONS**: Transition contract is proven.
**ESCALATION CONDITIONS**: Palette asset/schema ownership is absent or material migration is required.
**REQUIRED FINAL REPORT**: Rule, evidence, files, tests, risks, next action.

## Road/path continuity

**WHEN TO USE**: A road/path must continue through a cell boundary.
**WHEN NOT TO USE**: The request is an isolated decorative path with no cross-cell promise.
**REQUIRED INPUT**: Road owner, edge exits, seed, neighbor facts, terrain constraints.
**OPTIONAL INPUT**: POI/settlement anchors, route costs, screenshots.
**AUTHORIZED SCOPE**: Road planning and boundary contract only.
**FORBIDDEN CHANGES**: Changing settlement rules, skipping blocked edges, relying on traversal order.
**REQUIRED INVESTIGATION**: Trace whether endpoints are planned before roads; inspect edge encoding and terrain feasibility.
**IMPLEMENTATION CONTRACT**: Edge exits are explicit canonical data and are honored by both cells.
**UNITY VERIFICATION**: Generate fixed neighboring cells in both orders, inspect route topology and materialized path.
**TEST REQUIREMENTS**: Edge exit equivalence and route connectivity tests.
**ACCEPTANCE CRITERIA**: Required road is connected, deterministic, and terrain-valid.
**STOP CONDITIONS**: Contract passes or architecture must be designed first.
**ESCALATION CONDITIONS**: Road pass depends on later POI/settlement data without a prepass.
**REQUIRED FINAL REPORT**: Route evidence, boundary data, tests, risks, next action.

## Missing or disconnected roads

**WHEN TO USE**: A required route is absent or disconnected after generation.
**WHEN NOT TO USE**: The design permits no route and no acceptance rule requires one.
**REQUIRED INPUT**: Required endpoints/edges, cell coordinates, seed, generated plan, rejection reason.
**OPTIONAL INPUT**: Navigation graph, path cost, materializer logs.
**AUTHORIZED SCOPE**: Route planning/validation and diagnostics.
**FORBIDDEN CHANGES**: Marking a missing road as success or removing endpoint requirements.
**REQUIRED INVESTIGATION**: Compare planned vs instantiated route and find the first rejected/omitted segment.
**IMPLEMENTATION CONTRACT**: Unreachable requirements reject with a reason; valid plans materialize a connected route.
**UNITY VERIFICATION**: Compile, regenerate fixed seed, inspect route graph and scene evidence.
**TEST REQUIREMENTS**: Connectivity and rejection-reason tests.
**ACCEPTANCE CRITERIA**: Required route exists or an explicit rejection is surfaced.
**STOP CONDITIONS**: First omission is explained and repaired/ escalated.
**ESCALATION CONDITIONS**: Endpoint ownership or navigation contract is missing.
**REQUIRED FINAL REPORT**: Defect, route evidence, tests, risks, next action.

## Waterway continuity

**WHEN TO USE**: Rivers, channels, or waterways fail to continue across cells.
**WHEN NOT TO USE**: The body is fully contained and has no boundary fact.
**REQUIRED INPUT**: Water owner, edge crossings, elevation/terrain rule, seed, neighbor plan.
**OPTIONAL INPUT**: Flow direction, bank constraints, screenshots.
**AUTHORIZED SCOPE**: Water planning, boundary data, validation.
**FORBIDDEN CHANGES**: Filling/removing water to hide a seam, changing global seed rules.
**REQUIRED INVESTIGATION**: Trace crossing encoding, neighbor handoff, slope/bank validation, and materialization.
**IMPLEMENTATION CONTRACT**: Water boundary facts, flow, banks, and terrain remain compatible on both sides.
**UNITY VERIFICATION**: Fixed-seed adjacent generation, inspect water geometry and Console diagnostics.
**TEST REQUIREMENTS**: Crossing equivalence, flow/bank validity, traversal-order tests.
**ACCEPTANCE CRITERIA**: Waterway is continuous and valid at every required boundary.
**STOP CONDITIONS**: Contract is proven or no owner exists.
**ESCALATION CONDITIONS**: Water and terrain owners conflict or save identity must change.
**REQUIRED FINAL REPORT**: Boundary evidence, tests, risks, next action.

## Lakes/rivers/body-of-water generation

**WHEN TO USE**: Creating or repairing a water body's topology and placement.
**WHEN NOT TO USE**: Only a presentation material needs changing.
**REQUIRED INPUT**: Water-body type, seed, terrain/elevation inputs, expected inlets/outlets, cell bounds.
**OPTIONAL INPUT**: Flow graph, biome/palette requirements, density targets.
**AUTHORIZED SCOPE**: Water planning and validation after senior design.
**FORBIDDEN CHANGES**: Inventing arbitrary water rules or omitting bodies to pass performance checks.
**REQUIRED INVESTIGATION**: Establish topology owner, stage order, boundaries, terrain dependency, and rejection semantics.
**IMPLEMENTATION CONTRACT**: Canonical water plan precedes materialization and records why placement is accepted/rejected.
**UNITY VERIFICATION**: Fixed-seed plan/materialization, scene inspection, timing evidence.
**TEST REQUIREMENTS**: Topology, boundary, deterministic, and rejection tests.
**ACCEPTANCE CRITERIA**: Required body is present, connected as specified, and reproducible.
**STOP CONDITIONS**: Plan contract is tested.
**ESCALATION CONDITIONS**: No water/terrain owner or incompatible stage ordering.
**REQUIRED FINAL REPORT**: Topology decision, evidence, tests, risks, next action.

## Elevation/mountain continuity

**WHEN TO USE**: Terrain height, ridges, cliffs, or mountains seam across cells.
**WHEN NOT TO USE**: A purely local prop sits at a boundary without terrain dependency.
**REQUIRED INPUT**: Terrain/elevation owner, edge samples, seed, height constraints, neighbor facts.
**OPTIONAL INPUT**: Slope limits, erosion rules, screenshots.
**AUTHORIZED SCOPE**: Elevation planning and seam validation.
**FORBIDDEN CHANGES**: Flattening terrain to hide a seam or changing canonical height scale silently.
**REQUIRED INVESTIGATION**: Find height source, edge sampling, interpolation, and materialization order.
**IMPLEMENTATION CONTRACT**: Shared edge samples or deterministic compatible interpolation produce continuous terrain.
**UNITY VERIFICATION**: Generate adjacent cells, inspect edge heights/normals and visible scene.
**TEST REQUIREMENTS**: Edge sample equality/tolerance and traversal-order tests.
**ACCEPTANCE CRITERIA**: Height/slope continuity meets the explicit tolerance.
**STOP CONDITIONS**: Seam evidence passes.
**ESCALATION CONDITIONS**: Terrain owner is absent or changing scale breaks saved coordinates.
**REQUIRED FINAL REPORT**: Tolerance, evidence, tests, risks, next action.

## Missing settlements

**WHEN TO USE**: A required settlement is absent after a valid plan.
**WHEN NOT TO USE**: Settlement density is not part of the current acceptance contract.
**REQUIRED INPUT**: Settlement rule, seed, region/cell, placement constraints, planned vs instantiated records.
**OPTIONAL INPUT**: POI/road anchors, rejection reasons, screenshots.
**AUTHORIZED SCOPE**: Settlement planning/materialization/validation after owner approval.
**FORBIDDEN CHANGES**: Reducing required density, treating decoration as a settlement, bypassing placement constraints.
**REQUIRED INVESTIGATION**: Determine whether the settlement was never planned, rejected, or failed to materialize.
**IMPLEMENTATION CONTRACT**: Required settlement intent is explicit and failure is surfaced.
**UNITY VERIFICATION**: Fixed-seed plan and scene inspection.
**TEST REQUIREMENTS**: Presence, placement constraint, determinism, rejection tests.
**ACCEPTANCE CRITERIA**: Required settlement exists with valid anchors or a reported rejection.
**STOP CONDITIONS**: First missing stage is known.
**ESCALATION CONDITIONS**: No settlement owner or save identity required.
**REQUIRED FINAL REPORT**: Stage diagnosis, evidence, tests, risks, next action.

## Missing POIs

**WHEN TO USE**: A required point of interest is missing or unbound.
**WHEN NOT TO USE**: The request is an unplanned decorative prop.
**REQUIRED INPUT**: POI type/identity, region/cell, seed, anchor constraints, plan/materialization records.
**OPTIONAL INPUT**: Road/settlement relationships, asset key, screenshot.
**AUTHORIZED SCOPE**: POI planning/materialization and validation.
**FORBIDDEN CHANGES**: Substituting a random prop, inventing asset paths, weakening validation.
**REQUIRED INVESTIGATION**: Trace identity, placement, asset binding, and rejection path.
**IMPLEMENTATION CONTRACT**: Semantic POI intent maps to an approved asset and survives materialization.
**UNITY VERIFICATION**: Fixed-seed generation, inspect asset binding and scene object.
**TEST REQUIREMENTS**: Identity/placement/binding/determinism tests.
**ACCEPTANCE CRITERIA**: Required POI is present, correctly bound, and reproducible.
**STOP CONDITIONS**: Missing stage is proven.
**ESCALATION CONDITIONS**: Asset contract or POI owner is absent.
**REQUIRED FINAL REPORT**: Identity and binding evidence, tests, risks, next action.

## Hostile-region generation

**WHEN TO USE**: A hostile region, encounter area, or danger topology is missing or invalid.
**WHEN NOT TO USE**: No hostile-region rule exists in the approved design.
**REQUIRED INPUT**: Hostile-region owner, seed, region bounds, encounter constraints, player safety rules.
**OPTIONAL INPUT**: POI/settlement relationships, population density, screenshots.
**AUTHORIZED SCOPE**: Planning/validation only after senior review.
**FORBIDDEN CHANGES**: Spawning arbitrary enemies to satisfy a count, bypassing safety or persistence rules.
**REQUIRED INVESTIGATION**: Identify canonical region intent, spawn/materialization stage, and rejection reasons.
**IMPLEMENTATION CONTRACT**: Hostile intent is structured, deterministic, and safe for runtime consumption.
**UNITY VERIFICATION**: Fixed-seed plan, scene/runtime reproduction, Console evidence.
**TEST REQUIREMENTS**: Presence, bounds, determinism, safety, and save identity tests.
**ACCEPTANCE CRITERIA**: Required hostile region exists with valid constraints.
**STOP CONDITIONS**: Contract proven or escalated.
**ESCALATION CONDITIONS**: No combat/encounter owner exists.
**REQUIRED FINAL REPORT**: Owner, evidence, tests, risks, next action.

## Decoration sparsity

**WHEN TO USE**: Structural content is present but decoration misses an explicit density/coverage contract.
**WHEN NOT TO USE**: The complaint is really missing terrain, roads, water, POIs, or settlements.
**REQUIRED INPUT**: Decoration pass owner, target density, seed, biome/palette, planned vs materialized counts.
**OPTIONAL INPUT**: Asset catalog, pooling metrics, screenshots.
**AUTHORIZED SCOPE**: Decoration pass and diagnostics.
**FORBIDDEN CHANGES**: Replacing structural content with decoration or changing density without product approval.
**REQUIRED INVESTIGATION**: Distinguish plan omission, placement rejection, asset binding failure, and pooling failure.
**IMPLEMENTATION CONTRACT**: Density is deterministic and measured without consuming structural RNG streams.
**UNITY VERIFICATION**: Fixed-seed scene capture, counts, timing and Console inspection.
**TEST REQUIREMENTS**: Density range, determinism, and no structural-RNG-consumption tests.
**ACCEPTANCE CRITERIA**: Decoration meets the stated range without masking missing structure.
**STOP CONDITIONS**: Cause and evidence are clear.
**ESCALATION CONDITIONS**: Density is undefined or asset policy must change.
**REQUIRED FINAL REPORT**: Counts, cause, tests, risks, next action.

## Over-repeated assets

**WHEN TO USE**: Repetition violates a stated biome/visual variation rule.
**WHEN NOT TO USE**: No repetition rule or asset catalog exists.
**REQUIRED INPUT**: Asset family/key, repetition threshold, seed, placement scope, approved alternatives.
**OPTIONAL INPUT**: Screenshot, usage histogram, pooling data.
**AUTHORIZED SCOPE**: Deterministic selection/policy only.
**FORBIDDEN CHANGES**: Arbitrary asset paths, removing required objects, changing canonical content identity silently.
**REQUIRED INVESTIGATION**: Trace selection seed, candidate list, fallback path, and pool reuse semantics.
**IMPLEMENTATION CONTRACT**: Selection is deterministic, bounded by approved assets, and does not hide scarcity.
**UNITY VERIFICATION**: Generate fixed seed, inspect histogram and visual sample.
**TEST REQUIREMENTS**: Candidate validity, repetition threshold, deterministic histogram tests.
**ACCEPTANCE CRITERIA**: Repetition remains within approved policy.
**STOP CONDITIONS**: Policy is proven.
**ESCALATION CONDITIONS**: Asset catalog or visual requirement is undefined.
**REQUIRED FINAL REPORT**: Policy, histogram, tests, risks, next action.

## Deterministic-generation regression

**WHEN TO USE**: A fixed seed no longer reproduces the same canonical plan.
**WHEN NOT TO USE**: Only visual post-processing differs and canonical data is unchanged.
**REQUIRED INPUT**: Seed, canonical inputs, expected fingerprint/plan, engine/version, traversal order.
**OPTIONAL INPUT**: Serialized plans, RNG traces, profiler data.
**AUTHORIZED SCOPE**: Determinism bug and focused tests.
**FORBIDDEN CHANGES**: Updating the golden result without proving intended design change, using global random state.
**REQUIRED INVESTIGATION**: Compare plan hashes and RNG ownership across runs and cell orders.
**IMPLEMENTATION CONTRACT**: Canonical output is a pure function of approved inputs and owned RNG state.
**UNITY VERIFICATION**: Repeat generation, compare fingerprints, inspect Console.
**TEST REQUIREMENTS**: Same-seed equivalence, different-seed separation, traversal-order tests.
**ACCEPTANCE CRITERIA**: Fixed inputs reproduce the same canonical plan.
**STOP CONDITIONS**: First nondeterministic input is identified.
**ESCALATION CONDITIONS**: Seed schema or engine-version policy must change.
**REQUIRED FINAL REPORT**: Divergence, fix, hashes, tests, risks, next action.

## Cell-order dependence

**WHEN TO USE**: Generating cells in a different order changes canonical results.
**WHEN NOT TO USE**: A deliberately order-dependent streaming presentation effect is documented and non-canonical.
**REQUIRED INPUT**: Seed, cell set, two traversal orders, plan fingerprints, neighbor facts.
**OPTIONAL INPUT**: RNG traces, cache state.
**AUTHORIZED SCOPE**: Planning/state ownership and regression tests.
**FORBIDDEN CHANGES**: Sorting away evidence, treating presentation order as canonical input.
**REQUIRED INVESTIGATION**: Find shared mutable state, global RNG, cache mutation, and neighbor handoff.
**IMPLEMENTATION CONTRACT**: Canonical plan depends on coordinate/seed/approved inputs, not traversal history.
**UNITY VERIFICATION**: Run both orders and inspect plan diff.
**TEST REQUIREMENTS**: Permutation-based fixed-seed test over representative cells.
**ACCEPTANCE CRITERIA**: Canonical fingerprints match across orders.
**STOP CONDITIONS**: Shared state cause is proven.
**ESCALATION CONDITIONS**: Removing order dependence changes save identity or API.
**REQUIRED FINAL REPORT**: Order comparison, cause, tests, risks, next action.

## Materialization rejection

**WHEN TO USE**: A valid canonical plan is rejected or omitted during Unity object creation.
**WHEN NOT TO USE**: The plan itself violates a canonical constraint.
**REQUIRED INPUT**: Cell plan, materializer, rejection reason, object/asset constraints, seed.
**OPTIONAL INPUT**: Timing, pool state, Console logs, screenshot.
**AUTHORIZED SCOPE**: Materialization and validation diagnostics.
**FORBIDDEN CHANGES**: Converting rejection to success by omission, mutating the plan during presentation.
**REQUIRED INVESTIGATION**: Compare planned vs instantiated content and first rejected dependency.
**IMPLEMENTATION CONTRACT**: Rejection is explicit, actionable, and does not silently erase required structure.
**UNITY VERIFICATION**: Fixed-seed materialization, scene/object inspection, Console evidence.
**TEST REQUIREMENTS**: Plan/materialization parity and rejection reason tests.
**ACCEPTANCE CRITERIA**: Valid content materializes or failure is surfaced with the exact constraint.
**STOP CONDITIONS**: First rejected contract is known.
**ESCALATION CONDITIONS**: Asset, prefab, or save compatibility is implicated.
**REQUIRED FINAL REPORT**: Parity counts, rejection, tests, risks, next action.

## Streaming hitching

**WHEN TO USE**: A reproducible streaming/materialization hitch exceeds the approved budget.
**WHEN NOT TO USE**: No baseline profiler evidence exists.
**REQUIRED INPUT**: Reproduction, cell/seed, frame budget, CPU/GC/GPU/IO profile.
**OPTIONAL INPUT**: Stage timings, object counts, pool statistics.
**AUTHORIZED SCOPE**: One measured performance bottleneck and its tests.
**FORBIDDEN CHANGES**: Reducing canonical content, removing validation, broad optimization by intuition.
**REQUIRED INVESTIGATION**: Classify dominant cost and identify stage/allocating call path.
**IMPLEMENTATION CONTRACT**: Bound work or allocation while preserving canonical content and ordering.
**UNITY VERIFICATION**: Before/after profiler capture, Console, runtime reproduction.
**TEST REQUIREMENTS**: Content parity plus timing/allocation regression check.
**ACCEPTANCE CRITERIA**: Budget improves without content or determinism regression.
**STOP CONDITIONS**: One experiment is measured; stop after three same-root failures.
**ESCALATION CONDITIONS**: Budget requires architecture/async/threading change.
**REQUIRED FINAL REPORT**: Baseline, change, after measurement, parity, risks, next action.

## Generation-stage ordering

**WHEN TO USE**: A stage reads data that is produced later or a reorder is proposed.
**WHEN NOT TO USE**: A local implementation bug leaves ordering unchanged.
**REQUIRED INPUT**: Stage graph, inputs/outputs, owner, seed, current failure.
**OPTIONAL INPUT**: Timings, plan snapshots, dependency diagram.
**AUTHORIZED SCOPE**: Architecture proposal and approved stage change.
**FORBIDDEN CHANGES**: Reordering passes without contract, hidden precomputation, weakening validation.
**REQUIRED INVESTIGATION**: Build the actual dependency graph and identify canonical vs presentation stages.
**IMPLEMENTATION CONTRACT**: Every stage input has a producer or explicit prepass; order is deterministic and documented.
**UNITY VERIFICATION**: Fixed-seed generation, stage diagnostics, Console and performance check.
**TEST REQUIREMENTS**: Stage-order, determinism, boundary, and content-parity tests.
**ACCEPTANCE CRITERIA**: No stage consumes unavailable data and outputs remain stable.
**STOP CONDITIONS**: Graph and acceptance evidence agree.
**ESCALATION CONDITIONS**: Cross-system ownership or save identity changes.
**REQUIRED FINAL REPORT**: Graph, decision, evidence, tests, risks, next action.

## Boundary-contract failure

**WHEN TO USE**: A cross-cell edge contract is missing, ambiguous, or violated.
**WHEN NOT TO USE**: The feature has no neighbor interaction.
**REQUIRED INPUT**: Edge type, owner, coordinate pair, seed, expected contract, observed values.
**OPTIONAL INPUT**: Plan hash, topology/height/water samples.
**AUTHORIZED SCOPE**: Boundary schema/implementation and focused invariants after senior review.
**FORBIDDEN CHANGES**: Implicit neighbor discovery, fallback omission, traversal-dependent values.
**REQUIRED INVESTIGATION**: Locate contract creation, storage, comparison, and serialization.
**IMPLEMENTATION CONTRACT**: Edge facts are typed, canonical, symmetric where required, and inspectable.
**UNITY VERIFICATION**: Generate both cells and inspect serialized/diagnostic edge facts.
**TEST REQUIREMENTS**: Contract round-trip, symmetry, and traversal-order tests.
**ACCEPTANCE CRITERIA**: Boundary facts are explicit and honored.
**STOP CONDITIONS**: Contract is stable and tested.
**ESCALATION CONDITIONS**: Schema migration or multiple owners.
**REQUIRED FINAL REPORT**: Contract shape, evidence, tests, risks, next action.

## Save/load generation consistency

**WHEN TO USE**: A generated world differs after save/load or reload.
**WHEN NOT TO USE**: No save owner/schema exists yet.
**REQUIRED INPUT**: Save schema/version, world seed, generated cell identity, pre/post fingerprints, load order.
**OPTIONAL INPUT**: Migration fixtures, serialized plans, logs.
**AUTHORIZED SCOPE**: Approved persistence and generation integration only.
**FORBIDDEN CHANGES**: Regenerating accepted content silently, changing save format without migration.
**REQUIRED INVESTIGATION**: Trace seed/state serialization, load initialization, and canonical vs runtime-only data.
**IMPLEMENTATION CONTRACT**: Accepted generated state has stable identity and reloads to the same canonical result.
**UNITY VERIFICATION**: Save, reload, compare plan/materialization evidence and Console.
**TEST REQUIREMENTS**: Round-trip, old-version migration, fixed-seed consistency tests.
**ACCEPTANCE CRITERIA**: Pre/post fingerprints and required content match.
**STOP CONDITIONS**: All state owners are accounted for.
**ESCALATION CONDITIONS**: No schema/version owner or old saves become invalid.
**REQUIRED FINAL REPORT**: State map, fingerprints, tests, risks, next action.

## Pooling/allocation performance

**WHEN TO USE**: Profiler evidence shows allocation or pool churn during generation/materialization.
**WHEN NOT TO USE**: No measured allocation baseline exists.
**REQUIRED INPUT**: Baseline allocations, stage, object counts, pool policy, content parity target.
**OPTIONAL INPUT**: GC frames, reuse histogram, device profile.
**AUTHORIZED SCOPE**: One measured pool/allocation path.
**FORBIDDEN CHANGES**: Pooling objects with incompatible state, hiding leaks, reducing required content.
**REQUIRED INVESTIGATION**: Identify allocation site, lifetime, reset contract, and cleanup owner.
**IMPLEMENTATION CONTRACT**: Reuse is safe, deterministic, reset-complete, and content-neutral.
**UNITY VERIFICATION**: Before/after Profiler capture, runtime reproduction, Console.
**TEST REQUIREMENTS**: Reset/state isolation, content parity, and allocation regression tests.
**ACCEPTANCE CRITERIA**: Measured allocation improves without stale state or content loss.
**STOP CONDITIONS**: One measured experiment is complete.
**ESCALATION CONDITIONS**: Pool owner crosses subsystems or requires scene/prefab redesign.
**REQUIRED FINAL REPORT**: Baseline/after, reset proof, tests, risks, next action.

## Origin-to-streamed-world continuity

**WHEN TO USE**: The starting/origin area does not connect to later streamed cells.
**WHEN NOT TO USE**: No origin contract or streaming implementation exists.
**REQUIRED INPUT**: Origin owner, origin seed/cell coordinate, boundary facts, player anchor, streamed-cell plan.
**OPTIONAL INPUT**: Scene screenshot, save/reload evidence, navigation route.
**AUTHORIZED SCOPE**: Origin/streaming boundary design and approved implementation.
**FORBIDDEN CHANGES**: Teleporting or duplicating the player, special-casing visuals outside canonical planning.
**REQUIRED INVESTIGATION**: Trace origin creation, coordinate frame, seed derivation, neighbor handoff, and player placement.
**IMPLEMENTATION CONTRACT**: Origin is a canonical cell/plan participant and streamed neighbors honor its boundaries.
**UNITY VERIFICATION**: Start at origin, cross boundary, inspect plan/materialization and player continuity.
**TEST REQUIREMENTS**: Origin-neighbor determinism, route/terrain boundary, save/load tests.
**ACCEPTANCE CRITERIA**: Origin and streamed world share the same canonical continuity rules.
**STOP CONDITIONS**: Boundary and player anchor evidence pass.
**ESCALATION CONDITIONS**: Origin is an authored scene with incompatible coordinate/save semantics.
**REQUIRED FINAL REPORT**: Coordinate/seed contract, evidence, tests, risks, next action.
