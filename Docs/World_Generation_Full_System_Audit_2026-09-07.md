# YourQuest full world-generation system audit

Date: 2026-09-07. Verdict: **not generation-ready for the intended product**.

This audit evaluates the current implementation, published semantic manifests, captured world output, and recent verification against `YourQuest_Game_Design_Document.md` and `YourQuest_AAA_Procedural_World_Generation_Design_Plan.md`. It supersedes earlier readiness impressions, not the design specification. No runtime code, assets, scenes, or saves were changed during this audit.

## Executive diagnosis

The project has substantial procedural infrastructure, but it does not yet have a connected, production-qualified asset-driven construction pipeline. Semantic intent, spatial planning, content qualification, materialization, and acceptance disagree about what constitutes a usable settlement. The result can be deterministic and still look broken.

The main failure chain is:

1. Imported maps are partitioned primarily by position and instance budget rather than complete architectural ownership.
2. Published metadata does not provide the reviewed independent assemblies and functional contracts required by the intended compiler.
3. V2Preferred can reject that content and use persisted V1 planning. Rejected compiled settlements can then use palette-driven fixed lots.
4. Terrain accommodation and grounding attempt to reconcile donor-map geometry or fallback buildings after selection. Shared elevation, renderer bounds, and support percentages do not establish usable foundations, doors, streets, or interiors.
5. Several checks called quality gates are warnings or incomplete predicates. A successful loading/build path is therefore weaker than the user's intended result.

More noise, more scattered assets, different prompts, and further isolated grounding adjustments will not resolve this chain. The intended architecture remains viable, but essential content production and compiler integration remain unfinished.

## Evidence and limits

- Inspected project-owned runtime generation, terrain, environment, population, spatial planning, semantic selection, compilation, validation, save, and delta-application paths; existing design/status/audit documents; and published site metadata.
- All **30** `Assets/Assets/Resources/YQWorldSites/*/YQRuntimeSemanticSite.asset` manifests declare `releaseEligible: 1`. Twenty-nine have no V2 cell-contract entries. Only `medieval_viking_village` has entries: four, with pending review values, version-zero function metadata, and empty source signatures. None of the 30 serializes an `independentAssembly` contract. These are resource-file counts, not proof that every resource is reachable through the active catalog.
- User screenshots show floating large structures, fragmented settlements, magenta props, poor vegetation contact, and broad uniform terrain. Their console shows a Vector3 JSON self-reference failure.
- `Logs/YQLiveWorldPlacement.md` captures seed `5a62c187` at 2026-09-06 08:01:32 UTC, on a 1024 × 140 × 1024 terrain. Its accompanying PNG visibly contains disconnected architectural pieces and broad bare ground. It is historical evidence, not a newly rebuilt world after every recent patch. The pause panel obscures part of the image.
- The live report explicitly warns that renderer clearance is not a defect verdict. Roofs and upper floors should be above terrain. Its selected roots do not cover all wilderness/native trees, and enabled LOD renderers do not prove current visual visibility.
- Prior targeted reports, including `Logs/YQEldwealdBuildingGrounding.md`, verify particular mathematical/prefab cases. They do not establish complete settlement or world quality.
- No fresh full-world Play Mode capture or target-hardware performance profile was obtained in this audit. No compilation was run because this audit changes documentation only. Current visible quality after all recent patches remains unverified; the architectural defects below are directly observable in current source/data.

## Findings by severity

### F01 — Critical: published content does not meet the construction contract

The 30 release-eligible manifests contain no reviewed independent-assembly payloads. Viking's four V2 bindings are pending, not production approvals. `YQSiteFunctionContractsV2.cs:206` requires approved bindings; `YQProceduralSettlementLayout.cs:314` and `:355` explicitly inspect independent review approval. An absent serialized field may receive a default object at load time; that does not create approved evidence.

The compiler cannot infer missing foundations, doors, room volumes, street frontage, or workstations from a semantic label. Existing contracts and isolated review candidates are useful groundwork, but they are not a published library. A legacy release flag must not be interpreted as V2 readiness.

**Required:** separate legacy compatibility from reviewed construction eligibility; publish source-signature-bound complete assemblies through the existing catalog pipeline. Make release eligibility a derived result of the appropriate contract version and validation evidence. Do not bulk mark assets approved or fabricate providers to satisfy validators.

### F02 — Critical: fallback changes the construction product

`YQGeneratedWorldRuntimeBuilder.cs:384` defaults `requireV2ConstructionSuccess` to false. At `:2344`, V2Preferred can choose persisted V1. Around `:1665–1728`, failed compiled settlement construction/loading can invoke the palette builder. Fixed `MarketLots`, `StreetGridLots`, and `DenseCityLots` remain at `:9606` onward.

These paths preserve compatibility but produce materially different worlds. A semantic settlement description does not become a solved district/parcels/buildings composition through these templates. The older live fallback observations and captured fragment-built lots are consistent with this path; they do not prove every currently loaded site follows it.

**Required:** retain legacy save compatibility explicitly, but prevent a newly requested production world from silently qualifying through fallback. Constrained repair should choose a compatible approved assembly, relocate/regrade its parcel, or reject that candidate before commitment. Do not simply enable strict mode globally while the approved library is empty: that would replace poor output with unavailable generation.

### F03 — Critical: acceptance predicates permit known failures

`YQGeneratedWorldRuntimeBuilder.cs:4050` defines `ValidateSettlementPresentation` as void. Missing buildings, overlapping footprints, or modular fallback assemblies emit a warning and return; the result does not fail final acceptance. Its successful message says “coherent buildings,” but dimensional bounds and overlap checks cannot prove architectural coherence.

`YQGeneratedWorldIntegrityValidator.cs:25` defines general validity as `missingColliders == 0`. Route validity at `:46` is only `missingGroundPoints == 0`; grade is a separate property. Traversal issues, blockers, incomplete measurements, and query saturation are not all expressed in that acceptance predicate. The runtime builder's final gate uses these `IsValid` values.

At `YQGeneratedWorldRuntimeBuilder.cs:1514`, only up to two settlements are required to be instantiated for the startup threshold. Other prepared sites may encounter geometry failures when streamed. Prepared-site counts and instantiated-site qualification are different claims.

**Required:** typed results for geometry, support, entrances, navigation, function capacity, rendering, persistence, and streaming. Missing or incomplete required evidence must not count as pass. Certify streamed content offline and check instance-specific placement before exposure. Keep “planned,” “prepared,” “loaded,” and “qualified” states distinct.

### F04 — High: streaming partitions are being asked to serve as architectural assemblies

`Editor/YQAuthoredSiteStreamingCompiler.cs:548–595` bins source objects using a spatial grid and partitions them into cells. `Editor/YQSemanticSiteProductionCompiler.cs:324` currently assigns unclassified identities requiring review; its generated zone records at `:493` initialize authored building count to zero. Existing published labels do not by themselves demonstrate re-review under this newer logic.

`YQRuntimeWorldSiteCatalog.cs` selects bounded/connected semantic subsets and can choose curated defaults (`BuildCuratedDefaultCellIds`, `:4805`). Connectivity of bounds is not continuity of walls, roofs, floors, or entrances. Preserving donor transforms preserves their original dependencies too.

**Required:** an architectural ownership graph before streaming partitioning. A house owns its required shell, roof, foundation, collision, entrances, interiors, and supported attachments. Districts compose complete houses and public space. Streaming may subdivide or group these with explicit dependencies; it must not define what a house is.

### F05 — High: terrain adaptation still uses shared elevation and heuristic support

`YQGeneratedWorldRuntimeBuilder.cs:7088` computes one target height from the settlement center. The newer construction mask changes where grading occurs, but all affected cells still interpolate toward that shared height at `:7123`. Legacy circular grading remains when no construction layout is provided. This is not per-parcel terracing, cut/fill resolution, or road/door elevation solving.

Compiled grounding uses a minimum support ratio of 0.82 (`YQRuntimeWorldSiteCatalog.cs:793`) and a lower renderer band (`:6595`). A large low base can dominate the measurement while higher unsupported geometry falls outside the sampled band. Name-based structural filtering is not a authored support contract. A shared root correction cannot correct independent vertical mistakes within a donor slice.

The latest structure placement interval in `YQGeneratedWorldTerrain.cs:1029` rejects terrain that cannot fit its air/burial tolerance. That is useful rejection, but it does not supply a grading or relocation solution. Runtime builder failures at `:4855` and `:7440` can throw instead of repairing the parcel locally.

**Required:** solve each parcel's support plane, allowable earthworks, retaining structures, entrance landing, and street connection together. Validate actual declared supports and walkable surfaces against the final terrain, then persist the resolved placement. Preserve regional relief and inter-parcel slopes. Suspended architecture requires explicit structural support, not exemptions based only on appearance/name.

### F06 — High: asset intelligence is not the production selection authority

Searches of project-owned scripts found `YQAssetConstraintEvaluatorV2` and `YQDeterministicAssetSelectorV2` used by their own definitions and editor contract tests, not by the production world construction path. This does not mean no semantic rules run: the runtime catalog has its own tags, filters, functions, and selection logic. It means the newer typed constraint system is not the unified authority its presence suggests.

Recent filename tokenization fixes allow real numbered building prefabs to reach selection, but filename words and bounding dimensions still cannot certify a complete inhabited building. A ruin can be a valid ruin and an invalid residence.

**Required:** connect the existing typed constraints to the actual runtime/compiler selection boundary, with candidate rejection reasons and persisted selected IDs. Retire overlapping selection rules only after parity/migration checks. Keep semantic intent separate from structural qualification.

### F07 — High: landscape systems exist, but ecological and settlement integration remain weak

V2 has regional domains, geological fields, hydrology, site-network and terrain-approach code. It is incorrect to describe the project as having no terrain architecture. However, rejected V2 content can prevent that path from becoming the active authority.

`YQSpatialBlueprintCompilerV2.cs:567` constructs a primary basin lake and a river from valley control points, with prescribed levels and widths. This is a useful authored procedural template, not evidence of a general drainage/erosion solver or varied complete biome production. The materialized terrain still receives subsequent construction alterations.

`YQGeneratedWorldEnvironment.cs:5915` requires a LODGroup and a literal `/URP/` path for native tree eligibility. Valid materials outside that folder naming convention cannot use this route. Four explicit conifer supplements exist. Site reserve masks and separate settlement detail reserves continue to suppress vegetation independently of the new parcel grading mask. Keeping terrain in gaps does not automatically restore vegetation or meaningful land use there.

**Required:** capability-based approved vegetation eligibility; biome cohorts and density conditioned on moisture, slope, canopy, soil, and use; shared terrain/road/parcel exclusion masks; fields, yards, verges, woodland transitions, banks and rock integration. Qualify actual tree roots, LOD transitions, textures, and shadows. Do not treat density alone as landscape quality.

### F08 — High: road planning does not establish a traversable settlement

There is real route planning: V1 contains cost-aware grid routing and V2 contains site access/network compilation and orientation. This should be retained. The failure is the incomplete connection from those plans to final building entrances, final terrain grades, collision, and acceptance.

Fallback lots use template positions. Donor-map composition can preserve internal routes that do not match the new world approach. The weak route gate in F03 means the existence of ground along a route can pass without proving travel by the authoritative player or NPCs.

**Required:** a connected graph from regional roads to streets, parcels, entrance landings, doors, interior circulation and function providers. Check controller clearance, step/grade limits, headroom, door state and navigation after final placement. A road mesh or painted strip is presentation of that graph, not its proof.

### F09 — High: save ownership exists, but successful commitment is not observable

`WorldStateManager.cs:61–70` uses a void `Save`, serializes the state, and catches failures. Atomic temporary-file replacement and backup support are valuable. Callers cannot use the return value to distinguish durable success from a logged failure. `WorldDeltaApplier.cs:167` saves and then logs an applied delta; its in-memory mutations have already occurred.

`YQWorldAssetCatalog.cs:61–94` rebuilds/upserts regional palettes in `EnsureAssetPalettes`. Determinism against current catalogs is weaker than immutable accepted bindings across catalog updates. Fallback reconstruction also depends on current selection/placement code instead of a fully resolved assembly/parcel record.

The builder updates built-world bookkeeping before final qualification around `:1880–1903`, and final saving does not receive an explicit success result. This is not an end-to-end stage/qualify/persist/activate transaction.

**Required:** result-bearing save/commit, staged candidate records and scene roots, versioned accepted placements and source signatures, rollback on failure, and explicit migration for changed catalogs/terrain rules. Preserve existing identities, quests and accepted content. Never repair a legacy world by silently regenerating its canon.

### F10 — High: NPC descriptors are not executable schedules

`YQGeneratedNpcPlanningService.cs:3699` asks for `dailyRoutine` as one short sentence. `YQGeneratedWorldPopulation.cs:1198` places it into persona text. `CreateResident` (`:728` onward) establishes a visual, EntityInfo and NpcDialogueAgent and returns success. Imported prefabs may have additional behavior, but this path does not compile generated role/schedule contracts from the prose. No generated behavior-profile/schedule implementation was found through these symbols in project-owned scripts.

The existing NPC planning, identity, dialogue, and memory owners should remain. The user's ScriptableObject idea fits those systems when used as immutable behavior definitions, not independently mutable NPC state.

**Required design:** curated behavior modules declare typed preconditions, effects, duration ranges, interrupt rules, required animation intents and provider types. A deterministic compiler composes work/sleep/eat/social/patrol modules from approved descriptors. Animation availability constrains presentation; it must not invent a profession just because a clip exists. Persist profile/version, home/work provider IDs, schedule parameters, needs, active activity and progress per NPC. Keep runtime state out of shared ScriptableObjects. Validate bed/work capacity and reachable paths before population acceptance. Start with smith, innkeeper, fisher, guard and resident day/night variants using the existing NPC owner.

### F11 — High: material and performance checks are not fidelity certification

Runtime shader/material repair and LOD/collider repair are active parts of the pipeline. Rejecting unsupported shaders prevents some magenta output, but a usable shader says nothing about correct source textures, UVs, alpha, bark appearance, lighting or asset-family consistency. The captured report's `invalidMaterials=0` cannot certify those properties.

`YQRuntimeWorldSiteCatalog.cs:758–782` uses loading distances and a source-instance budget (1100 for semantic settlement selection). A source instance may contain many renderers, materials, colliders and LODs. Instance counts are not measured frame, memory, triangle, shadow or draw-call budgets. Coroutine slicing helps responsiveness but is not proof of frame-time targets.

**Required:** qualify imported assets in the editor using their correct materials, representative lighting and LOD distances; publish validated variants without replacing art with generic materials. Establish target hardware and a measured frame/memory budget, then profile representative qualified sites and streaming transitions. No numerical AAA performance claim is supported by this audit.

## Recent-fix self-audit

The previous work addressed real individual defects, but its scope was too narrow to justify implying the world was approaching completion.

| Recent work | What it establishes | What remains / regression exposure |
|---|---|---|
| Signed grounding-gap correction | Correct numerical air/burial interpretation | Renderer sampling still does not establish all structural supports |
| Structural anchor priority and cache identity | Better ordering and separation of cached selections | Still selects from insufficiently qualified content; in-place manifest mutation needs version discipline |
| Vector3 JSON converters | Addresses the captured recursion on layout vectors; targeted roundtrip passed previously | No durable commit acknowledgement or complete world lifecycle proof |
| Per-cell grading mask | Avoids altering some empty inter-cell ground | Still one target elevation, old circular raster extent, separate vegetation exclusion; no general terrace solver |
| Native-tree base adjustment | Compensates measured prefab-base offsets | Real Terrain tree rendering, LOD roots and all biome assets remain visually unqualified |
| Owner terrain lookup | Reduces wrong-terrain sampling for compiled sites | Exact ownership/naming assumptions and remaining fallback sampling need one consistent terrain service |
| Building tokenization and alternate selection | Real numbered prefabs can pass selection and dimensional checks | Names and dimensions do not establish residential function or complete architecture |
| Reject unknown modular recipes / ungroundable buildings | Avoids knowingly accepting some malformed output | Exceptions can stop a partially built world; no constrained local repair/rollback solution |
| 48 isolated grounding cases | Four prefabs, rotations and synthetic terrain cases satisfy the tested rule | Does not test complete BuildBuildingLots flow, neighboring lots, doors, roads, interiors, controller traversal or actual generated landscape |
| Live capture tooling | Preserves useful actual output evidence | Historical/partial capture; cannot replace a new world walkthrough and full coverage |

Keep correct fixes, but test their integration through the real generator. Do not reinterpret fixture success as a production-world pass.

## Keep, remove from the production path, and build

| Keep and strengthen | Remove from new production acceptance | Build/connect next |
|---|---|---|
| Structured generation, stable IDs, normalization, canonical save owner | Silent V2-to-template success qualification | Contract-qualified selection and transactional acceptance |
| Existing semantic catalogs and reviewed candidate tooling | Grid cells treated as complete architecture | Complete assembly ownership, support/entrance/function evidence |
| Spatial terrain, route and approach infrastructure | Shared flat pads as general settlement solution | Joint parcel/earthworks/street/entrance solver |
| Existing NPC planning/dialogue/memory | Prose routine treated as implemented behavior | Typed module composition, schedules and provider reservations |
| Targeted regression tests and coroutine work budgets | Warning-only “quality gates” and count-only readiness | End-to-end generation, traversal, persistence, streaming and visual tests |
| Correct imported materials and art | Runtime generic repairs as content qualification | Editor qualification and coherent biome/architecture palettes |

“Remove” here means remove from the new-world production qualification path, not delete imported assets or break old saves. Avoid another parallel manager/compiler architecture.

## Production recovery sequence

### 1. Make readiness truthful

Return structured results from presentation/traversal checks; distinguish legacy/prepared/qualified states; propagate persistence failure. Produce one diagnostic record per rejected candidate showing the missing contract and fallback decision. Audit saved-world compatibility before changing defaults.

Exit: a deliberately broken house, blocked entrance, unsupported required foundation, missing material, failed save and incomplete traversal measurement each prevent production acceptance with a specific reason. Existing saves still load through an explicitly labeled compatibility path.

### 2. Qualify one real kit and one complete settlement slice

Use the existing reviewed-home candidate as a starting point only after confirming its geometry. Publish a small coherent set of complete residences, inn, smith/workshop, civic/service space and compatible street/yard pieces. Declare support polygons, entrances, walkable volumes, sockets, functions and capacities. Carry source signatures and review versions into runtime manifests. Include a compatible alternate for local repair.

Exit: published content passes independent placement and player traversal at representative rotations/elevations. Every generated household and service has valid physical capacity. No missing geometry is satisfied by a role tag.

### 3. Connect the compiler and terrain solution

Route typed asset constraints through actual selection. Solve streets and parcels against terrain, choose compatible assemblies, calculate per-parcel elevations/earthworks, connect entrances, and validate the resulting geometry. Persist resolved choices and transforms before activation. Replace failed candidates locally with bounded retries.

Exit: the real production entry point generates a coherent settlement on both gentle and sloped terrain without donor-slice floating, missing shells, inaccessible doors, or shared circular flattening. A failed parcel does not destroy accepted world state.

### 4. Produce the surrounding landscape

Integrate water, banks, paths, fields, woodland edges, ground cover and rocks using shared ownership/masks and approved assets. Check materials and tree contact in the actual render pipeline. Establish a reference scene made from the same assets to define attainable fidelity and distinguish art limitations from generator defects.

Exit: ground-level and overhead views show purposeful land use and terrain transitions, readable routes and grounded vegetation. Visual acceptance is against the reference, not an asset count.

### 5. Connect population to places

Implement the behavior composition in F10 through existing NPC ownership. Assign capacity-checked homes/workplaces and time schedules. Bind animation intents through the existing approved registry. Handle unavailable workstations and streamed-out simulation explicitly.

Exit: a full in-game day demonstrates travel, work, breaks, sleep and interruption; save/reload preserves state without duplicate residents or shared-profile mutation.

### 6. Certify variation, persistence and performance before expansion

Use a fixed multi-seed corpus including the known failing scenarios, multiple terrain conditions and constrained content shortages. Run actual generation and streaming, save/reload, interrupted generation and catalog-version compatibility. Measure the qualified slice on target hardware. Expand biomes and settlement families only after this passes.

Exit: reproducible accepted placements after reload; no unacknowledged fallback in production runs; all required sites qualified; bounded local repair; measured performance within agreed budgets; reviewed screenshots and authoritative-player walkthroughs.

This is dependency order, not a credible calendar estimate. Dates require staffing, approved content throughput, target hardware and an agreed visual reference. The environmental fidelity target is achievable only through both engineering and art/content production; a generic generator cannot manufacture missing architectural and material quality from labels.

## Definition of the next honest “ready” result

One real generated settlement and its surrounding terrain, using the production bootstrap and published catalog, must satisfy all of the following:

- Complete, culturally coherent buildings with declared supports and reachable entrances/interiors.
- Streets, parcels and terrain resolved together; no unsupported required geometry or penetrations beyond the approved asset contract.
- Correct materials, LODs, vegetation contact and readable landscape transitions at player height.
- Every required service has usable providers and capacity; residents can reach assigned locations.
- No ignored construction/traversal/persistence failure and no undisclosed template fallback.
- Exact accepted identities/bindings/placements survive save/reload and streaming without duplication.
- A recorded full-generation run, ground-level walkthrough, overhead view, rejection report and performance capture support the claim.

Until that evidence exists, pressing Play is another diagnostic run. It is not confirmation that the intended world engine is complete.

## Implementation checkpoint after the audit — 2026-09-07

The first acceptance corrections are now implemented. F03 above describes the audited baseline:

- Palette building construction returns its presentation result. A failed candidate is disabled and destroyed locally, returns without dressing or success bookkeeping, and cannot satisfy the settlement count. This does not repair the rejected architecture or roll back terrain earthworks.
- Route acceptance now requires samples, ground, acceptable recorded grade, no unresolved blockers or saturated clearance queries, and completed traversal measurements without issues. Diagnostic text identifies blocked acceptance instead of claiming acceptance is unchanged.
- The current 1024-sample traversal limit and missing player-capsule cases remain incomplete evidence and now correctly block acceptance. Scalable route certification is still needed; these changes deliberately do not claim to make current worlds pass.
- Compilation passed with the existing CS0162 warning in `YQWorldGenerationV2ContractTests.cs:249`. Eight compiled route-predicate cases passed: empty evidence, clear evidence, five independent failure categories, and incomplete coverage.
- Added `YQProductionSliceRegressionTests.RunReadinessBatch` to exercise route rejection and synthetic missing/overlapping/modular/valid settlement geometry. The Unity geometry portion has not run: Unity was already active and its session was preserved. No full-world visual verification was performed.

Persistence acknowledgement, qualified content publication, strict production/legacy separation, and complete transactional acceptance remain outstanding. This checkpoint is a gate correction, not visual or production certification.

### Parcel elevation implementation

New procedural layout records now carry an earthwork version and persisted parcel world elevations. Legacy records default to version zero and retain their prior shared-height behavior. Before heightmap mutation, grading resolves each new parcel's elevation from its own terrain location, honors the minimum dry height, and rejects invalid saved profiles or overlapping foundation cores with incompatible elevations. Successful profiles are retained in the layout; elevation data participates in its geometry signature.

The grading evaluator gives parcel cores their own flat planes, blends competing shoulders, and interpolates streets between flat endpoint approaches. Unowned ground keeps its original height. This replaces the single-center elevation only for the new profile path; it does not silently migrate existing worlds or fix unqualified donor assemblies.

Verification: C# compilation passed with the existing CS0162 warning. A compiled regression passed 16 samples across four headings (both parcel elevations, street midpoint, and untouched ground) plus JSON elevation roundtrip. Actual Unity heightmap application, terrain/door traversal, arbitrary street junctions, steep grade repair, visual output and reload through the full generator remain unverified. This is not a completed terrace/retaining-wall solver. The full goal remains incomplete.

Follow-up: saved-layout validation now rejects unsupported earthwork versions, mismatched elevation counts and nonfinite elevations. The expanded compiled regression starts with a valid layout and verifies these rejection cases; it passes. Added an isolated real `TerrainData` test for four headings, unchanged unowned terrain, persisted-profile reapplication onto a changed base surface, and atomic rejection of unknown versions. It is queued through the existing idle-editor runner (`Temp/YQParcelTerrainWrites.request`); no execution report existed at the last check. Compilation succeeded with the same existing warning. The queued test must not be reported as passed until `Logs/YQParcelTerrainWrites.md` contains its result.

### Unity verification and current execution boundary

`Logs/YQParcelTerrainWrites.md` subsequently reported PASS for all four headings: actual parcel planes, untouched ground, saved reapplication, and invalid-version rejection. This establishes isolated TerrainData behavior, not a complete playable settlement.

The street-connected home review ran with matching source signature `9fa64b5eb22392a73ddad7d433fd1847`; functional access checks completed and exterior views were captured. Subsequent structural-contact instrumentation was compiled externally, but Unity's loaded editor DLL remained dated 03:55:21 while source files were newer. Later consumed requests therefore do not verify that instrumentation. The user has been asked to refresh Unity. The request runner now conservatively checks source/assembly timestamps, and home reports record schema plus loaded module IDs. These guards themselves take effect only after Unity reloads scripts.

Removed the route probe's global 1024 diagnostic sample cutoff. Valid finite segments now finish through the existing frame-budget yields; invalid/nonfinite or excessive segments reject explicitly instead of silently reducing sample resolution. This avoids making ordinary long routes permanently incomplete solely because of an obsolete diagnostic cap. Compilation passed; actual long-route traversal and timing remain unverified pending current-script execution in Unity. Existing CS0162 warning remains unchanged.

### Explicit persistence acknowledgement

Further verification: filesystem commit tests passed initial creation, replacement, backup creation, and locked-primary rejection with unchanged primary/backup contents. Removed the non-atomic overwrite fallback after replacement failure; unsupported atomic replacement now reports failure and preserves the accepted file. Tests used unique workspace Temp fixtures, not player profiles.

Palette setup now retains an existing palette with the same nonempty style and ID instead of rebuilding it from current catalog definitions. A compiled check verified object/ID preservation. This does not validate or migrate an already-bad palette.

Native tree eligibility no longer requires `/URP/` in the asset path. Curated selection remains the entry point; eligibility now checks nonempty LOD levels, actual mesh/billboard assets, and existing pipeline-compatible material checks. Compilation passed. Real vegetation prototype rendering and LOD transitions remain unverified until Unity loads the current scripts; this is not a claim that every newly eligible tree renders correctly.

`WorldStateManager.TrySave(out string failure)` now returns success only after the existing write completes, while `Save()` remains a compatible wrapper for existing callers/events. The generated-world commit checks the save owner's identity and propagates a failed write into materialization failure instead of logging a successful world build. Compilation passed with the existing warning. Isolated failure injection and full runtime verification remain pending. This does not yet provide scene/state rollback, fix every Save caller, or replace the existing filesystem fallback behavior; it closes the previously unobservable commit-result boundary only.

### Current evidence checkpoint — grounded candidate review

This checkpoint supersedes the earlier stale-editor execution boundary above. Unity batch execution now loads current scripts and produces isolated rendered reviews. It does not certify a generated world.

- The stone-foundation experiment failed its continuous-support criterion: only 61 of 360 floor-footprint probes found sufficient coverage. It remains rejected for that purpose (`Logs/YQStoneFoundationCoverage.md`).
- The timber candidate uses four existing wooden water-tank foundation frames, retains native vertical scale, and remains unpublished (`releaseEligible: 0`, pending review). Its independent structural/foundation contract is still unverified. A visually plausible frame is not proof of load support.
- Offline door, swing, interior and activity-access probes pass at four headings (`Logs/YQTimberHomeFunctionalAccess.md`). They do not certify the actual player controller, NPC navigation or runtime provider binding.
- Grounded renders use actual flat TerrainData and a declared 0.12m clearance below the walking datum. Mesh samples showed the landing's lowest sampled top at 3.5839m versus the collider datum of 3.6795m. The new clearance distinguishes soil elevation from walking elevation; default zero preserves existing contracts. Sloped production placement is still unverified.
- Corrected the candidate's inherited storage dependency signature and the render diagnostic's comparison against the walking datum instead of actual soil. These are metadata/evidence fixes, not production publication.
- Earlier persistence prose describing an overwrite fallback is historical: atomic replacement failure now preserves the accepted file and reports failure. Full scene/state rollback remains missing.

The principal gap remains integration and qualified content. A reviewed test prefab is outside the production catalog; repairing it cannot by itself change the world the user sees. Before another readiness claim, trace the production bootstrap through catalog selection, accepted assembly placement, per-parcel grading, entrance construction, provider binding, save/reload and visible activation in one recorded run. Capture the selected asset IDs and any fallback reason. Qualify the required service buildings as well as residences, and reject incomplete assemblies rather than counting spawned pieces as completed settlements.

Outstanding acceptance evidence: an actual generated settlement on sloping terrain; grounded vegetation and correct materials; connected streets and entrances; live authoritative-player traversal; usable service capacity and NPC schedules; deterministic reload; interrupted-generation recovery; and measured multi-seed generation/streaming performance. None is established by the isolated house renders.

### Runtime grounding and acceptance verification

The real `TryResolveReviewedLandingDelta` and terrain-connection validator passed the timber candidate at four headings on flat terrain and slopes of +0.03/-0.03 (12 cases). Grounding now uses the shared terrain sampler, rejecting holes and unsupported terrain scaling before translating the assembly; both rejection cases passed in Unity. Report: `Logs/YQTimberTerrainIntegration.md`. These tests use detached approval records and do not publish the candidate.

Inspected the production caller: reviewed landing failure does not fall back to renderer minima; incomplete grounded-cell counts make the site fail its grounding gate. Whole authored slices still use their separate shared-datum path, so this does not certify their structural completeness.

Executed the previously pending `YQProductionSliceRegressionTests.RunReadinessBatch` in Unity. Its explicit PASS appears in `Logs/YQProductionReadinessBatch.log`: parcel evaluator/record roundtrip, route evidence rejection, missing buildings, overlapping footprints and modular-name rejection. Synthetic cubes represent footprint cases only. This test does not prove complete building geometry, terrain appearance, settlement composition or live traversal. Unity shutdown also reported a temporary-allocation warning; no performance certification is inferred from this run.

### Follow-up production evidence and remaining content boundary

Current Unity verification of the timber candidate now covers its own door/storage runtime binding at four rotations, repeated binding without duplicates, preserved colliders, stale-source/conflicting-owner rejection and explicit landing datum. See `Logs/YQTimberHomeProviders.txt`. These are inactive fixture instances, not actual player interaction or gameplay save/reload.

Foundation evidence was corrected: imported collision boxes report 248/360 floor contacts, while exact LOD0 mesh probes report only 80/360. The spatial map is in `Logs/YQTimberFoundationCoverage.md`. Neither contact percentage proves a beam foundation valid or invalid by itself; post contact and floor/beam spans remain unresolved. Do not publish the candidate based on collision envelopes.

Parcel grading now rejects missing terrain at its centre and flat-core corners rather than accepting clamped tile-edge samples. Saved elevations do not bypass these checks. Real TerrainData tests pass at seven headings (0, 15, 37, 90, 123, 180, 270), including holes at parcel centres, out-of-tile centres, partial-footprint boundaries, saved reapplication and unchanged unowned terrain. This does not check every hole inside a footprint or certify street-junction grades.

The published Viking catalog was exercised again across 32 residential selections: only west_homestead passed the tested frontage filter, and its pending habitation/service evidence remained rejected. Another 32 layouts with the street-connected home passed, along with its existing provider checks (`Logs/YQRealAssemblyConstruction.md`). These are selection/layout checks, not 32 fully generated worlds. The September 6 live capture predates these changes and is not current visual evidence.

Next production dependency: qualify structural supports and live traversal for a complete residence, then connect qualified residences and required service assemblies through the actual catalog/compiler. Do not spend further time repeating the passing isolated tests without a relevant code/content change. Full-generation execution, visuals, persistence, NPC activity and performance remain required.

### Play Mode motor verification and review isolation correction

Created the separate timber traversal scene with the existing production motor and door provider. Initial Play Mode exposed an omitted review guard in YQProfileSaveSystem: it reported recovering a newer shared snapshot into its matching profile. Do not describe that first run as save-free. Added guards before automatic bootstrap and Awake disk access; subsequent logs contained no recovery event and the harness checks that no profile/world/player save manager exists.

Early movement failures were harness failures: first only one frame ran due to cold startup consuming the wall-clock budget; then the headless editor filtered virtual keyboard input (zero delivered-key and motor-input frames). The harness now measures one second of simulated time with a 45-second watchdog and uses a disposable InputSettings copy configured for headless input, restoring the original settings afterward.

Current result in Logs/YQTimberMotorBatch.txt: PASS, 5.763944m horizontal movement and 0.9630924m rise, 2306 frames with delivered input and motor input, zero gameplay-lock frames. This exercises the production motor Update and CharacterController with virtual W input in Play Mode. It proves initial approach movement only, not doorway passage, return traversal, live interaction, full-world generation or performance. Frame counts in this unthrottled fixture are not a benchmark.

### Live rendering defect repaired

Player-camera captures during the successful doorway round trip exposed a magenta bed despite GPU shader support. The bed references MedievalKingdom HDRP materials using Shader Graphs/S_BasicTextured with an empty active pipeline tag. The compatibility gate had accepted this graph by name/support alone. In URP it now requires Shader Graphs to declare UniversalPipeline; the existing material adapter converts rejected sources. The resulting capture shows a textured bed (`Logs/YQTimberInteriorPlayerView.png`), and `Logs/YQTimberBedMaterials.txt` records URP Lit adapters and retained wood/cloth textures. Imported materials were not overwritten. Exact texture/art fidelity beyond this visible recovery remains subject to review.

The compatibility regression completed successfully in Unity (`Logs/YQMaterialCompatibilityRegression.log`, `Logs/YQSettlementRepairBoundaries.txt`): untagged/HDRP graphs reject in URP, explicitly tagged URP graphs and neutral text remain accepted, and non-URP behavior is unchanged. The existing settlement-boundary fixtures also passed. This does not prove every Shader Graph in the project compatible or every world material visually correct.

The production motor doorway round trip still passes after material conversion. This is one route in an isolated flat-terrain candidate scene; it does not publish the candidate, establish structural support, or replace full-world multi-seed visual/performance verification.

### Joisted candidate rejection and current production fallback trace

The joisted house (`0e0c24d5c58de9b2ff3ea7754c46df79`) has 41 support members. Exact mesh probes find 68 frame crossings without gaps over 0.03m and no flat-datum post/soil failures, but 168 of 232 sampled floor/joist contacts exceed the gap tolerance. It is not structurally accepted. The original diagnostic returned success despite these failures. Its verifier now rejects missing evidence, insufficient member contacts and measured gaps, writes the report in its finally block, and throws on rejection. Unity compiled and executed the corrected verifier: batch exit code 1 with the expected verification exception. This is a correctly rejected candidate, not a passing foundation test. Floor samples without a ray hit remain outside the measured set; the report does not establish complete surface coverage.

Current source inspection confirms two independent routes back to legacy generation in `YQGeneratedWorldRuntimeBuilder`: spatial preparation can return success after selecting persisted V1 (around line 2353), and V2 cell preflight can restore V1 before replacing the world (around line 1289). Both permit fallback when `requireV2ConstructionSuccess` is false and the active mode is V2Preferred. Once using V1, rejected or unloadable compiled settlements can invoke palette civic construction (around lines 1670 and 1708). These are actual executable branches, not evidence that a particular current play session took them. Existing warning messages identify the downgrade, but a successful generation callback does not mean the intended semantic V2 system built the world.

Consequently, isolated candidate improvements cannot establish progress in the player's generated scene until a qualified manifest is selected and its runtime authority, selected asset IDs, materialization and activation are captured together. Do not publish this failed candidate or change an existing save's authority just to obtain a successful build. The next construction repair must address the measured floor support gaps; the next integration acceptance must explicitly distinguish preserved legacy playability from successful V2 construction.

### Measured floor fitting repair

The unpublished joisted prefab was fitted against actual floor-underside and joist-top meshes. The original placement used a combined renderer minimum and nominal timber radius; that did not describe the irregular timber's actual contact surface. `FitJoistedFrameToFloor` measures the required displacement, rejects missing evidence or a lift exceeding 0.1m, and moves the complete frame together to preserve its relative joints. Temporary probe colliders are removed before saving, retaining existing prefab links. The measured lift was 0.06629276m; posts remain embedded at the flat reviewed soil datum.

Unity compiled and completed the repair batch successfully. Updated dependency signature: `beb0cffc83637454e5bfa95de6569c24`. Exact probes now report 232 floor centreline contacts with zero gaps over tolerance, 68 frame joints with zero gaps, and zero flat-datum soil failures. Functional doorway, swing and furnished-room access checks pass at four rotations. Reports: `Logs/YQJoistedFrameFit.txt`, `Logs/YQJoistedFrameContacts.md`, and `Logs/YQJoistedHomeFunctionalAccess.md`. These measurements supersede the failed geometry above; they do not establish bracing, every unsampled surface, slope placement or live traversal for this changed prefab. The existing pending manifest deliberately retains its old signature, so it cannot inherit acceptance after geometry changes. Production publication and integration remain outstanding.

### Entrance grounding does not establish foundation grounding

The repaired joisted prefab was exercised through the existing runtime landing correction and terrain-connection validator at four headings on flat and +/-3% actual TerrainData slopes. Entrance checks pass, but a new foundation observation finds 12 floating post placements, with a maximum reported gap of 0.152276m on the ungraded slopes. The batch correctly fails (`Logs/YQJoistedTerrainIntegration.md` and `.log`). These are 12 post placements across the test matrix, not 12 distinct prefab posts. The fixture deliberately has no parcel grading, so this does not prove graded production placements fail; it proves the entrance checks alone are insufficient.

Source boundary: `TryResolveReviewedLandingDelta` in `YQRuntimeWorldSiteCatalog.cs` derives translation only from approved door approach datums. `YQIndependentAssemblyContract` currently contains a `foundationVerified` Boolean but no support sample positions or allowable soil contact ranges. Thus a flat-geometry review cannot express where runtime terrain must support the assembly. Required integration work: explicit reviewed foundation contact evidence tied to source identity, evaluated against final terrain after parcel grading, before visible activation. Construction should satisfy those contacts through owned grading or suitable foundations; lowering the whole house would invalidate its entrance alignment. Preserve the legacy contract pending an explicit compatible extension rather than treating the new diagnostic as a production fix.

### Runtime foundation contact extension

Implemented additive version-one contact data on the existing independent assembly contract: exact support paths, reviewed cell-local bottom points and minimum/maximum embedding depths. The runtime landing resolver evaluates these points at the proposed vertical translation against the shared hole-aware final-terrain sampler before applying movement. It rejects unsupported revisions, missing/unapproved/stale evidence, missing paths, nonfinite values, inverted ranges, absent terrain and unsupported embedding. Version zero retains the existing path and is not newly certified; production manifests still need migration and qualification.

Unity compiled and executed the joisted terrain matrix with detached version-one records. Across 12 slope/heading cases the runtime gate matched the measured post observations, rejecting the two placements responsible for all 12 floating-post observations while accepting supported cases. Stale source signatures rejected. Exit code 0 records the regression passing, including expected placement failures; it does not mean the ungraded slopes became buildable. Actual grading/foundation adaptation and published contact records remain required. The existing CS0162 warning is unchanged.

### Persisted candidate support proposals

Updated the existing joisted manifest in place with nine pending support contacts and current source identity `beb0cffc83637454e5bfa95de6569c24`; its GUID and prefab remain intact. The upper embedding allowance is derived from each post's rendered height minus 0.1m top-joint clearance (0.7504946m), rather than the diagnostic fixture's 1m allowance. Cell, assembly and storage signatures match current geometry. All approvals remain pending and `releaseEligible` remains zero. Unity compilation/save completed with exit code 0, and serialized contact entries were inspected. This is saved proposal data, not qualification against the revised allowance.

The existing curation object also has general support fields (`supportPolygon`, `supportLocalY`, embedding range); they are empty/default on this candidate. Follow-up must reconcile the general support contract with discrete per-member contacts when connecting grading and selection, rather than leaving two unrelated sources of terrain authority. The new member contacts add exact support paths and individual bottom points, but do not themselves implement earthworks.

The terrain regression now reads the saved joisted manifest's contact points and actual 0.7504946m maximum embedding allowance, rather than reconstructing contacts with a test-only 1m allowance. Only a detached copy receives test approval. Unity execution passed the 12-case matrix with the same two rejected floating placements and stale-signature rejection. This verifies the saved proposals at those placements, not production release.

Grading ownership was traced to the existing settlement prepass: `GradeTerrainPad` receives the layout cached under `ResolveSemanticCompositionSeedV2` plus settlement heading. Its version-one parcel path exists and is independently tested, but the candidate's nine support contacts are not inputs to that grading call. The next integration check must combine that actual parcel grading path with the saved house contacts and entrance datum; separate passes cannot prove the same final surface supports both.

### Combined parcel grading and house placement

`VerifyGradedJoistedPlacement` now runs the existing production parcel writer followed by runtime landing/foundation resolution and entrance validation on the same TerrainData. It uses the saved pending manifest on a detached approved test record and one complete renderer footprint. All 25 cases passed: slopes -10%, -3%, flat, +3%, +10%, each at headings 0, 37, 90, 180, 270 degrees. The sampled distant terrain corner is unchanged. Report: `Logs/YQGradedJoistedPlacement.md`; Unity exit code 0.

The first run's distant-ground check compared Unity's stored terrain against unquantized input values and falsely failed. It now compares actual stored values immediately before and after grading. No runtime grading code was changed for this test.

This evidence refines the prior diagnosis: the existing parcel grader can satisfy both the saved foundation contacts and entrance for this complete footprint. Direct contact-driven earthworks are not yet proven necessary. The remaining production dependency is that catalog selection, layout footprint and contract identity must supply this same coherent assembly through the actual settlement path. This single-parcel fixture does not certify street joins, multiple parcels, generated seeds, full-world visuals or publication.

The pending manifest refresh now recomputes its complete geometry envelope after support repairs, preserving the asset identity. The combined test uses this serialized envelope and rejects an envelope that omits rendered horizontal geometry, instead of calculating a replacement solely for the fixture. The 25-case matrix passes with the saved footprint and saved contact records together. Both Unity executions completed successfully. Actual production selection, street-block packing and publication remain separate unverified boundaries.

### Two-house production layout and grading fixture

Extended the combined fixture to call `YQProceduralSettlementLayout.TryBuild` using two instances of the saved candidate envelope, authored datum and external StreetConnection. Its returned positions, rotations, streets, radius and parcel profile feed the existing terrain writer; both placements then pass runtime foundation and entrance checks. All 25 slope/heading/seed combinations pass (50 house placements), including +/-10% slopes and oblique heading. Report: `Logs/YQGradedJoistedStreet.md`; Unity exit code 0.

The test reuses one instance sequentially at the two planned positions. It proves placement/terrain compatibility for the generated layout, not simultaneous collision, street-surface traversal, catalog availability, service diversity, population, performance or a fully generated world. The candidate remains pending and unpublished. The original single-parcel test remains available through its unchanged public entry point.

### Street surface regression exposes a release blocker

Added final-heightfield longitudinal grade probes at 0.25m spacing along both street edges and centre. The first -10% slope, zero-heading layout fails with a 75.51135-degree maximum, despite both house placement checks passing. This supersedes any implication that the prior 25-layout result established walkable connecting roads. The acceptance limit is 24 degrees, matching the candidate walking-approach limit; crossfall and collision traversal are still separate checks.

Source inspection identifies competing per-road endpoint height planes and first-road priority at overlaps in ResolveParcelEarthwork. A trial shared inverse-distance parcel surface reduced the measured maximum to 28.78213 degrees but still failed. That runtime experiment was reverted; the added failing regression remains. The latest report records the rejected experiment, not a passing current-runtime result. Required fix remains a consistent junction/road grade solution that also preserves parcel cores and supported entrances, with persisted-version compatibility considered before changing accepted terrain semantics.

Re-executed the restored runtime grader with exact interval diagnostics. Current failure is confirmed at 75.51135 degrees on the spine's positive-width edge: x=3.50, z=1.12 to 1.37, soil elevation 15.06117 to 16.02527m. The two parcels sit at 13.47964 and 16.52098m. This is approximately a 0.964m rise across a 0.25m interval, consistent with competing road profile priority at a junction rather than merely the original 10% slope. `Logs/YQGradedJoistedStreet.md` now reflects current restored code and includes parcel geometry and the offending road interval. Unity compiled and deliberately exited 1 on this regression. No runtime fix is claimed; junction height continuity and bounded grade both remain required.

Implemented an opt-in earthwork version two using shared lower/upper distance envelopes from parcel cores. Roads extend the common surface mask instead of introducing competing height planes. A pairwise feasibility check rejects parcel elevations that cannot connect across the core gap at a 0.4 rise/run bound (about 21.8 degrees). Versions zero/one remain unchanged; normal layout creation still selects version one. The street fixture explicitly selects version two during development. Unity compiled and exercised the new surface: the original discontinuity is removed, but the matrix still rejects an infeasible +10%/180-degree layout. Parcel elevation reconciliation or layout relocation is required before enabling version two for new worlds. This is an incomplete opt-in implementation, not a production fix or a passing full regression.

Version-two unsaved proposals now reconcile pairwise elevation differences to the available core separation, splitting necessary correction between pads with bounded deterministic iterations. Changes over 0.5m or below the minimum construction height reject before writing terrain. Saved parcel levels are never reconciled; existing feasibility checks still validate them. Unity compiled and passed all 25 two-house street cases, including the prior +10%/180-degree rejection, with road edge/centre longitudinal grades under 24 degrees and retained foundation/entrance acceptance. The latest street report supersedes the preceding failed matrix. Larger layouts, reload consistency, crossfall, visual captures and actual catalog materialization remain unverified; normal generation still defaults to version one pending these checks.

The same 25-case fixture now serializes each accepted layout, restores the original base heightfield, deserializes and reapplies grading. It requires identical serialized layout data after replay and exact equality at all 257x257 terrain samples. Unity compiled and passed these replay checks along with the existing road longitudinal grade and house support checks. This establishes deterministic terrain reconstruction for this fixture; it does not exercise player profile saving, changed base terrain, streaming, or multiple simultaneous settlements.

Added full-width crossfall measurements to the same final-heightfield street fixture, using the saved approach's 10-degree allowance. The first -10%/zero-heading case fails at 21.79607 degrees across the road; longitudinal maximum remains 21.82236 degrees. Unity compiled and correctly exited 1. This narrows the prior success: version two provides continuous height and bounded longitudinal grade in the tested layouts, but not acceptable road cross-sections. Its isotropic shared parcel surface slopes across the spine between unequal pads. Road-specific cross-section shaping with common junction elevations remains necessary; version two stays opt-in and must not be promoted on the earlier longitudinal/replay results. Full-width measurements also do not bound every intermediate transverse sample.

A centreline-projection trial with finite intersection blending lowered the first case's crossfall to 8.437929 degrees but failed the 24-degree longitudinal limit. The trial compiled after correcting a local-name conflict and was subsequently removed. The latest report records this rejected trial, not current restored surface performance. This reinforces the need to solve junction and pad-transition elevations together; post-hoc surface blending does not satisfy both constraints. No production-default change was made.

Current restored version-two surface has been recompiled and retested. Added transverse feasibility diagnostics for the two-parcel fixture: 4m total transition run outside a 7m spine, 3.04134m parcel rise versus an optimistic 3.015203m allowance combining 24-degree transition and 10-degree road crossfall. This allowance excludes plateau/easing distances, so a practical solution needs additional adjustment. The current reconciliation uses total core separation and therefore counts the road width as available climbing run; that does not model road crossfall. The latest report again reflects restored code, not the rejected projection trial. Next solver work must constrain pad elevations against actual corridor geometry and junction plateaus together with longitudinal profiles. Unity compiled successfully and the expected crossfall regression still fails.

The opt-in version-two reconciliation and saved-height feasibility check now share a road-width-aware rise bound for opposing cores separated by an axis-aligned street. It reserves the road width at ten-degree crossfall and applies the 0.4 transition rise/run only outside that width. This matches the current axis-aligned block-layout vocabulary; arbitrary diagonal road arrangements retain only the general distance bound and are not certified. Unity compiled and exercised the updated constraint: the first fixture's parcel rise becomes 2.834289m, within the available transition allowance. The existing shared surface still yields excessive crossfall, so the full regression correctly fails. Road-profile construction remains unfinished and the mode remains disabled by default.

The opt-in surface now deducts road-crossing distance at a lower crossfall rate when evaluating its shared height envelopes, with a fade beyond road ends. At a ten-degree design rate the oblique fixture measured 10.26292 degrees after terrain interpolation. Both surface and parcel allowance now use a nine-degree design rate, preserving the unchanged ten-degree test limit. The first three cases pass, but the -10%/180-degree case rejects during parcel grading; the complete matrix is still failing. Unity compilation succeeds. The current road-aware candidate requires further feasibility/earthwork-budget investigation and overlapping-corridor checks; it remains opt-in and is not production-ready. No elevation correction cap or acceptance limit was relaxed to force a pass.

Added targeted runtime rejection diagnostics for parcel earthwork budget and unresolved pairwise grade constraints. Unity reproduced the failing -10%/180-degree fixture: fixture-home-0 samples 16.27072m and requires 15.6248m, a 0.6459198m correction exceeding the 0.5m cap. This is an unsuitable layout under the current earthwork policy, not evidence of failed constraint convergence. The generation path needs bounded layout/site alternatives before committing terrain; changing the cap merely to pass this fixture would not address that placement dependency. Unity compiled and the expected budget rejection remained explicit in the batch log.

### Production acceptance ordering blocks terrain-aware alternatives

Traced the actual call sequence, not only the fixture. `PreflightSpatialCellsV2Routine` invokes `ResolveUniqueSemanticCompositionV2Routine` before terrain construction. That resolver tries up to twelve variants against functional coverage, footprint and composition uniqueness, then stores the selected seed in `_resolvedSemanticCompositionSeedsV2`. Subsequent calls restrict themselves to one variant when that seed exists. Terrain prepass calls the same resolver later, so it cannot retry a layout rejected by terrain feasibility.

Moreover, `DestroyRuntimeRootOnly` occurs before `YQGeneratedWorldTerrain.BuildRoutine`. Thus the real heightfield needed for validation is created only after the old world has already been removed. Simply adding retries to GradeTerrainPad would bypass the earlier selection/uniqueness commitments and would not restore the previous world on failure.

Required integration boundary: stage the candidate terrain independently, run bounded composition/layout alternatives against that terrain before locking the seed and uniqueness reservation, then publish the accepted terrain and assembly together. Preserve already persisted selections, clean up rejected staged data, and keep the current world alive until replacement acceptance. This ordering is established from current source; no staging/rollback implementation is claimed. It is a higher-priority production dependency than additional isolated house qualification.

Added BuildCandidateRoutine to the existing terrain owner. It requires an inactive staging parent, skips global DestroyExisting, and names the result as candidate terrain. The existing creation path now releases untransferred TerrainData and any created terrain when its iterator is disposed or the completion callback fails. Ordinary callers retain replace-existing behavior. Unity compilation passed before a final guard was added to recheck staging-parent existence/inactivity after yielded uploads; that final guard and cancellation behavior still need runtime verification. The world builder has not yet adopted this API, so old-world preservation and atomic replacement remain unfinished.

Unity compiled the final guard and passed a real candidate-upload cancellation fixture. The test advances nested creation iterators until candidate TerrainData exists, disposes the iterator stack, and verifies that data is destroyed, the inactive staging root has no terrain child, the completion callback did not run, and a separately owned terrain using the live terrain name remains intact. Report: `Logs/YQTerrainCandidateCancellation.txt`; batch exit code 0. This proves editor-mode cancellation during upload, not Play Mode deferred destruction, successful candidate handoff, profile switching or full builder rollback.

The successful candidate path now also passes in Unity: actual terrain synthesis/upload completes, exactly one callback receives the terrain, it remains inactive under the staging parent, and the existing live-named sentinel terrain/data survive. `Logs/YQTerrainCandidateCompletion.txt` records the result; batch exit code 0. BuildCandidateRoutine additionally rejects a missing completion callback so candidate data always has an explicit receiving owner. These isolated editor fixtures do not integrate staged terrain into the world builder or prove runtime replacement/rollback.

Integrated candidate terrain creation into BuildGeneratedWorldRoutine before DetachOriginObjectsForRebuild/DestroyRuntimeRootOnly. The existing world now remains during yielded terrain synthesis. On successful synthesis and current-plan revalidation, the builder replaces the root and reparents/renames the completed candidate; it does not build the heightfield twice. The build finally block destroys untransferred staging TerrainData/root on early exit. Unity compilation passed (`Logs/YQBuilderTerrainStagingCompile.log`). Full builder runtime testing remains outstanding. This protects terrain synthesis failure/cancellation only: settlement terrain prepass and asset materialization still occur after replacement, and composition selection still precedes terrain-aware acceptance. It is not full transactional rollback.
