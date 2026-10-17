# YourQuest — world-generation system dissection

Date: 2026-08-31. Scope: world generation only. Investigation was read-only; this report is the only file created by the audit. No Unity launch, desktop control, asset mutation, script edits, compilation, or push.

## Verdict

The user's description is consistent with the implementation. The current system is not yet the intelligently composed world generator described by the design document. It combines procedural terrain, semantic selection of authored scene fragments, heuristic grounding, and several independent dressing passes. That can produce technically populated terrain without producing a coherent place.

The main problem is not insufficient raw assets, nor a need for a more imaginative language model. It is the missing bridge between semantic intent and complete, functionally validated spatial assemblies. Recent loading and placement fixes address real defects but do not supply that bridge.

The authoritative [GDD explicitly delegates this subsystem](C:/Users/Garri/YourQuest/Docs/YourQuest_Game_Design_Document.md:406) to the [world-generation design plan](C:/Users/Garri/YourQuest/Docs/YourQuest_AAA_Procedural_World_Generation_Design_Plan.md:79). Its requirements include spatial ownership for every object, authored assembly grammar, streets and parcels, explicit support contracts, local repairs, persisted compiled output, and meaningful quality gates. The production path implements only parts of that design.

## Evidence boundary

- Findings below come from current project-owned source, runtime Resources manifests, generated intake evidence, and existing saved-world review reports.
- The current Unity Editor log's metadata was visible, but reading its contents was denied. The active Unity assembly and the exact branch taken during the user's latest Play session were not verified.
- Existing August 30 tests are not evidence that subsequent edits compile or that the current scene meets visual requirements. The saved-placement review itself was blocked by an unsupported spatial version; its detached repair proposal was not applied because it exceeded its migration scope.
- The runtime-data counts below describe the on-disk manifests inspected, not an exhaustive audit of every imported mesh/material.

## 1. What actually runs

The source selects `CompiledWorld` materialization and `V2Preferred` spatial planning. These are separate switches. “CompiledWorld” does not prove that V2 terrain, routes, or functional composition are active.

The execution path is:

1. Accept/load the semantic world plan and regional asset palettes.
2. Prepare a spatial authority; attempt V2 validation.
3. For V2, preflight selected site cells against required functions and footprint reserves.
4. If that fails and V1 is available, select the whole persisted V1 spatial path.
5. Build terrain; prepare construction pads and approaches; paint surfaces and road ribbons.
6. Add water, native vegetation, prefab wilderness dressing and cave visuals.
7. Materialize the fixed origin and selected authored settlement/site pieces.
8. Validate a subset of collision/traversal conditions and publish the world.

Sources: [architecture switches](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:29), [whole-world V1 fallback](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:1266), [construction sequence](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:1406).

### Critical data finding

The 30 `Resources/YQWorldSites/*/YQRuntimeSemanticSite.asset` manifests contain:

| On-disk measure | Count |
|---|---:|
| Runtime site manifests | 30 |
| Marked `releaseEligible` | 30 |
| Serialized V2 cell-function contracts | 4 |
| Approved V2 cell-function contracts | 0 |

All four contracts are in the medieval Viking runtime manifest. They are pending, have contract version zero and empty support polygons; the shown socket clearance sizes are zero. Other inspected runtime manifests have no serialized cell-function contracts.

The [functional validator](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQSiteFunctionContractsV2.cs:82) requires approved, version-compatible evidence for the selected cells. V2 settlements require habitation, circulation, service and, when applicable, commerce. Consequently, the currently published data cannot satisfy that V2 settlement preflight. Under `V2Preferred`, this is a direct route back to V1—not proof that the latest Play session used V2.

“Release eligible authored scene” and “approved functional V2 building/cell” are currently different levels of approval. The first is being mistaken for evidence of the second. Do not fix this by blanket-approving the pending contracts or weakening their checks.

Sources: [Viking runtime contract data](C:/Users/Garri/YourQuest/Assets/Assets/Resources/YQWorldSites/medieval_viking_village/YQRuntimeSemanticSite.asset:22), [representative Nordic manifest without functional evidence](C:/Users/Garri/YourQuest/Assets/Assets/Resources/YQWorldSites/nordic_village/YQRuntimeSemanticSite.asset:22), [V2 required functions](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQSpatialSiteNetworkCompilerV2.cs:140).

## 2. Streaming cells are not generative building cells

The authored-site streaming compiler buckets source root objects by horizontal position. Typical outdoor cells are 96 m grid buckets, with further splits by object count. This is a loading partition, not a building/room dependency solver.

Where a source hierarchy does not already group a complete building, related modules can land in different buckets or count-based parts. The compiler does not establish that a selected bucket contains a complete home, intact room, supported staircase or connected street before it becomes a selection unit.

Runtime semantic composition scores tagged candidates, selects a seeded anchor, and adds affordable nearby cells. It then instantiates them at their original authored offsets. It selects subsets of an existing layout; it does not solve a new street/parcel/building arrangement.

Connectivity checks between aggregate bounds cannot prove that two doors, stair landings or walkable streets actually connect. A changed cell-selection signature can produce a different subset without producing a well-designed settlement.

Sources: [grid/count partitioning](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/Editor/YQAuthoredSiteStreamingCompiler.cs:548), [semantic selector](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs:3859), [preserved authored transforms](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs:1689).

**Required correction:** separate complete compositional units from streaming chunks. First compose complete buildings, parcels and streets with explicit connections. Partition the accepted result for loading afterward. Existing extracted scenes remain useful references and donors, not a universal location grammar.

## 3. Grounding has the wrong unit of authority

Construction currently combines several different approximations:

- One broad circular flat pad per selected site's aggregate radius.
- An assembly-level vertical correction from structural metadata, authored datum or measured bounds.
- Independent vertical adjustment of each cell using a weighted median of low renderer bottoms and terrain footprint samples.
- Additional object/category-specific grounding for scenery, origin objects and actors.

A median is not a guarantee of support. A cell can include several buildings, stairs, terrain pieces and elevations. Moving the entire cell to align its median cannot put every foundation on the terrain. Independently moving adjacent cells can also break an authored connection across their boundary.

Some grounding exclusions are based on names such as rock/tree/grass/water rather than typed support ownership. Failed cell-grounding resolution can simply leave a cell unmoved; the later collision gate does not measure the resulting foundation gaps.

Sources: [site pad dimensions](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:6587), [radial flattening](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:6885), [whole-cell movement](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs:5579), [median-based support correction](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQRuntimeWorldSiteCatalog.cs:5695).

**Required correction:** solve support at complete-assembly/parcel level using a support polygon, floor datum, doorway height, allowed cut/fill and an explicit foundation solution. Foundations, stairs and retaining transitions must be part of the solution. Decoration may soften a valid transition; it must not hide missing support.

## 4. Terrain exists, but the landscape grammar remains rudimentary

There is genuine deterministic terrain work here. V2 contains geological fields, water relationships, site reserves and route masks; the environment correctly avoids applying its legacy radial landform pass over an active V2 world. These are worth retaining.

However, V2's macro template still starts with one primary ridge, valley, basin, river/lake chain and waterfall, plus regional relief. It is a limited landscape recipe, not the full range of causal landscape compositions described by the specification. The older path uses regional stamps. Broad site pads then flatten sizeable areas regardless of the detailed buildings that will occupy them.

The current terrain is 1,024 m square with a 513-sample heightmap: approximately 2 m between height samples. Surface/detail maps use 256 resolution, approximately 4 m per mask cell. This can support a macro landscape, but cannot by itself provide fine doorway grading, road crossfalls, rock ledges or rich foreground detail. Simply raising resolution would not repair the missing spatial rules.

Sources: [terrain dimensions](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldTerrain.cs:32), [V2 geological recipe](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQSpatialBlueprintCompilerV2.cs:393), [hydrology recipe](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQSpatialBlueprintCompilerV2.cs:567).

**Required correction:** extend the existing terrain compiler with a small set of coherent, parameterized landform recipes and mid-scale transitions. Preserve water/route/site causality. Use authored rock assemblies and terrain-compatible detail for the near field rather than expecting a heightmap to provide every feature.

## 5. Roads are not yet settlement street design

V2's route graph is useful, but route control points are generated from endpoint interpolation plus a seeded bend. Its settlement interior path is primarily a heading-based spine. V1 also adds template-based spines/cross-streets. Neither traced path derives a complete street/parcel network from the actual selected building entrances.

There is also a concrete visual defect in road construction: the ribbon samples terrain at its centre and assigns that same height to both lateral edges. On a cross-slope, one edge can be buried while the other floats. Longitudinal samples are also spaced relatively widely. Creating one road mesh is considered sufficient for the mandatory-road presentation gate; that does not establish coverage, visibility or doorway connectivity.

Sources: [route control points](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQSpatialSiteNetworkCompilerV2.cs:643), [site spine](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:3220), [ribbon height construction](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:1838), [road acceptance](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:389).

**Required correction:** route frontage must resolve to actual entrance connections. Streets and parcels must be compiled together. Road surfaces need terrain-conforming cross-sections or a graded roadbed, appropriate shoulders and crossing geometry—not just a line painted through an existing scene fragment.

## 6. Foliage density and variety are constrained by several disconnected systems

The code already has native terrain trees, billboard ground cover, prefab scatter, habitat clustering, origin dressing and water/route/site exclusions. It is not accurate to say that vegetation generation is absent.

But the contract is fragmented:

- Native trees require a path containing `/URP/` and an LOD group. A valid tree elsewhere in the library cannot qualify through that path, regardless of its actual compatibility.
- The native supplement is a fixed four-conifer list, making repeated families likely across otherwise different regions.
- Ground cover is derived from selected prefab textures and rendered as billboard detail, with limited prototypes per region and globally.
- Broad site-reserve masks suppress vegetation around whole reserves, not just buildings and streets. This works against inhabited-edge detail and leaves cleared-looking land.
- Prefab scatter still applies independent seeded position/yaw/scale selection and runtime material/bounds repair.
- The richer typed asset constraint evaluator and deterministic selector exist, but the searched project-owned call sites are editor tests; they are not the production foliage/site-selection path.

Sources: [native compatibility filter](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:5686), [native conifer supplement](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:5093), [ground-cover contract and reserve masks](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:5641), [prefab scatter](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:7205), [typed selector](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQAssetIntelligenceV2.cs:905).

**Required correction:** make one biome/land-use palette authoritative for canopy, understory, grass, rocks and ground textures. Approve species by measured render/support contracts, not folder spelling. Exclude exact occupied footprints and corridors; deliberately generate verges, yards, gardens, clearings and wilderness transitions around them. Increasing the global count alone will make the same composition denser.

## 7. Cave semantics currently outrun cave functionality

V2 includes a waterfall/cave relationship and marks natural sites as requiring concealed access and transitions. The traced environment materializer selects a cave-like prefab, positions and grounds it, repairs materials/collision, then counts it as created.

That routine does not itself create an entrance-to-interior connection, terrain opening, traversable room graph, or reward/encounter ownership. A cave-looking mesh is not proof of a functioning dungeon behind a waterfall. This finding concerns the traced world-generation route, not a claim that no other dungeon-related code exists in the project.

Source: [regional cave materialization](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldEnvironment.cs:7870).

**Required correction:** define a complete natural-feature assembly containing its entrance, surrounding rock/terrain treatment, access route, interior/transition provider and optional encounter/reward sockets. Compile all required pieces as one functional contract.

## 8. Acceptance gates permit the reported defects

The strongest explanation for “it technically works” is what success currently means:

- General integrity is valid when `missingColliders == 0`.
- Critical-ground validation casts down at selected anchors; it does not measure every building's foundation contact or detect burial.
- Route validity is `missingGroundPoints == 0`. Excessive grade and unresolved structural blockers are recorded, but the final world gate does not use them to reject the world.
- Downward collision can be provided by terrain beneath a floating structure. Thus the world can pass while a house is visibly unsupported.
- A generated road object can exist without being a good or complete road.
- The traced worldgen route does not establish a full generated-cell navigation/connectivity proof.

Sources: [integrity success definitions](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs:12), [critical anchor checks](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs:131), [route checks](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs:179), [final world gate](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:1884).

**Required correction:** validate maximum support gaps and penetration, doorway clearance, complete assemblies, route continuity/grade, usable providers, palette compatibility and required population/encounter capacity. Reject or replace the local solution before publication. Do not merely harden the final gate against the same unsolved input and create another loading dead end.

## 9. Persistence stores intent more completely than physical composition

Settlement records retain semantic IDs, seed, kit/style binding and narrative/service fields. V2 persists a spatial blueprint. The selected cell variants are still resolved into a runtime dictionary that is cleared at the beginning of each build; cell transforms are reconstructed from current manifests and grounding code.

This is deterministic only relative to the current code and asset data. It is not the specification's persisted, revisioned physical composition. Changes to manifest contents, selection rules or grounding can alter an accepted save's appearance even if its seed stays the same.

Sources: [settlement save fields](<C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Data/State/World State/WorldState.cs:878>), [V2 saved artifact](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQWorldGenerationV2Contracts.cs:22), [transaction-local selection](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs:6669).

**Required correction:** persist approved composition identity, exact assembly/cell selections, transforms, support/approach solution and source/compiler versions. Keep migration explicit. Continue must stream the committed result rather than re-solve it against changed libraries.

## 10. What the library actually provides today

The latest on-disk intake report is dated August 31 and covers only the first benchmark kit: 279 prefabs and 45 materials, with zero compiled-world-eligible prefabs pending authored spatial review. That is not a measure of everything installed; it is evidence that broad asset ownership has not become a prepared compositional library.

The runtime also has 30 released authored-site manifests and many source packs, including several genres. These are valuable ingredients. Their existence does not establish usable building functions, matched foundations, entrances, ecology profiles or cross-kit compatibility.

Sources: [latest intake report](C:/Users/Garri/YourQuest/Assets/Assets/GeneratedAssets/WorldIntake/YQWorldAssetIntakeReport.md:1), [runtime catalog](C:/Users/Garri/YourQuest/Assets/Assets/Resources/YQRuntimeWorldSiteCatalog.asset:16).

The correct direction is to finish and connect the preparation pipeline—not buy another asset pack, auto-approve everything, or invent a second parallel registry.

## 11. Performance implications

Cooperative iteration, asynchronous instantiation, staged activation and a single site-load slot are useful. Keep them.

However, a yield after work cannot bound the cost of that work. Terrain pad processing, native terrain synchronization, tree publication, hierarchy/material repair and individual dense prefab operations can still cost a long frame. Source comments claiming that these paths can never spike are not measurements or guarantees.

The full initial terrain/wilderness construction is also distinct from later site streaming. Partitioning authored sites does not make the entire world a precompiled streamed landscape.

Move import/material/structural repair out of ordinary gameplay generation where possible. Compile reusable assets beforehand, publish bounded spatial chunks, and measure stage timings and worst-frame distributions in Unity. Do not reduce quality by silently skipping content just to shorten loading.

## Repair order

### A. Establish one truthful worldgen development target

Keep the existing save path recoverable. Build a separate, explicit validation fixture using one prepared kit and the current compiler, without silently falling back to V1. Report the chosen authority, contract versions, selected cells, missing functions and asset substitutions. This is a diagnostic/acceptance fixture, not a permanently hardcoded player world.

### B. Complete a small real compositional kit

Prepare several complete building variants, a service interior, a public space, street/entrance connectors and terrain-transition pieces from existing assets. Record measured support polygons, front/door transforms, clearance volumes, interior/function providers, compatibility and instance cost. Approve only what is actually verified. Preserve source assets and GUIDs.

### C. Connect semantic intent to physical layout

Extend the existing V2 path to choose a location morphology, build its circulation/parcel arrangement and fit approved assemblies. Use bounded retries/backtracking with explicit local alternatives. Keep typed functions genre-neutral: habitation, commerce, security, infrastructure, transition, ecology. Let approved kits provide fantasy, industrial or sci-fi appearance.

### D. Solve terrain, foundations and roads as one placement transaction

Replace whole-bucket median correction as the main solution. Grade only the required support/road footprints, create the selected foundation/retaining/stair transitions, conform road cross-sections, validate every required doorway connection, then commit the terrain and assembly transforms together.

### E. Apply coherent landscape dressing and functional features

Use one approved biome/land-use palette, multi-scale vegetation and rock assemblies, and exact exclusion masks. Integrate cave/waterfall/encounter/reward assemblies through their real providers. Dress after the functional geometry passes, including deliberate settlement-edge transitions.

### F. Persist and prove the result before expanding

Save the compiled placement artifact. Test several seeds for distinct layouts and identical reloads; inspect New Game, Continue and unload/reload behavior. Validate foundations, doors, roads, interiors, species/material variants and frame timings. Then add kits and landscape recipes. One working reference slice is evidence; dozens of approved-looking metadata records are not.

## Keep, change, retire from the primary path

| Keep | Change | Do not use as the main solution |
|---|---|---|
| Semantic save IDs and accepted canon | Persist exact physical composition | Re-solving accepted worlds after library changes |
| Existing V2 terrain/water/site/route representation | Add the missing layout/assembly stage | Treating site anchors as complete settlement design |
| Resource registry and asset provenance | Complete measured contracts and capability-aware publishing | Assuming a released scene has every gameplay function |
| Cooperative streaming and staged activation | Stream compiled functional chunks | Using loading buckets as building grammar |
| Imported art and useful authored arrangements | Reuse them as reviewed assemblies/donors | Seeded scene subsets as universal generation |
| Existing material/grounding diagnostics | Move preparation earlier and validate real support | Repeated runtime repair as a substitute for valid inputs |

## Immediate next implementation boundary

The next implementation should be worldgen-only and start with the smallest complete asset-to-layout-to-terrain-to-validation path. It should not start with more NPC offsets, global foliage multipliers, new goddess prompts or another broad framework. The end-to-end slice must prove that the generator can compose a place rather than merely instantiate assets.

No claim is made that the world is repaired by this audit. Its purpose is to establish why the current approach keeps producing the reported result and identify the concrete subsystem work needed to change that result.
