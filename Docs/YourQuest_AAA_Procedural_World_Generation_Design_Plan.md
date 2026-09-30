# YourQuest AAA Procedural World Generation Design Plan

**Status:** Production architecture specification

**Audience:** Work GPT, Unity engineers, technical artists, world designers, and content curators

**Scope:** Dynamic world generation, asset preparation, spatial compilation, runtime streaming, world mutation, persistence, and quality assurance

**Relationship to the main GDD:** This document expands Sections 8, 20, 21, 22, and Production Phase 4 of the YourQuest Game Design Document. The main GDD and explicit user direction remain authoritative.

---

## 1. Executive Decision

YourQuest must stop treating world generation as randomized prefab placement.

The current screenshots are the predictable result of a flat scatter architecture:

- buildings are independent objects rather than parts of streets, parcels, and districts;
- paths do not determine entrances, frontage, commerce, or settlement shape;
- terrain is not graded around foundations, roads, drainage, and landmarks;
- asset packs are mixed by loose tags rather than an enforced visual grammar;
- complete buildings, modular fragments, props, and structural pieces are treated as interchangeable;
- there is no compositional hierarchy from skyline to street to doorway to clutter;
- invalid materials and unsuitable assets survive until runtime;
- random placement is accepted even when the result is navigationally or visually incoherent.

That approach cannot be polished into a Witcher 3- or Skyrim-quality environment. The replacement is a **hierarchical, constrained world compiler** using curated, authored building blocks.

The target pipeline is:

```text
Player history + world canon + generation seed
                    |
                    v
       LLM structured creative brief
                    |
                    v
       Schema validation and normalization
                    |
                    v
       Deterministic hierarchical compiler
 Terrain -> water -> roads -> districts -> parcels -> assemblies
 -> interiors -> population -> encounters -> story dressing
                    |
                    v
   Geometry, traversal, composition, lore, and budget gates
              | pass                 | fail
              v                      v
     Persisted WorldCell       Local constrained repair
              |
              v
       Asynchronous runtime streaming
```

The LLM authors meaning. The compiler authors space. Curated assets provide visual quality. Validation decides whether the result is allowed to exist.

---

## 2. What “AAA” Means for YourQuest

“AAA” cannot honestly mean generating the raw quantity, bespoke art, animation, cinematics, and hand iteration of a several-hundred-person studio. For YourQuest, the achievable and testable goal is **AAA-level environmental coherence within bounded generated locations**:

- a settlement has an immediately readable identity and silhouette;
- every building belongs to a district, parcel, road, and social function;
- entrances face traversable space and interiors correspond to exteriors;
- roads, terrain, walls, water, vegetation, and buildings agree spatially;
- districts display believable density, wealth, age, danger, and maintenance;
- landmarks produce views and navigation anchors;
- props tell local stories rather than filling empty coordinates;
- repeated content is disguised through variants, orientation, dressing, damage, vegetation, lighting, and narrative state;
- the frame rate and streaming behavior remain stable;
- generation is deterministic, persistent, debuggable, and repairable.

The production strategy is **small regions at this bar first, then expand the library**, not continent-scale randomness first.

---

## 3. Non-Negotiable Design Truths

1. **No raw prefab scattering.** Every placed object belongs to a spatial owner: terrain feature, road, parcel, assembly, room, encounter, or dressing socket.
2. **No LLM transforms or asset paths.** The model emits semantic intent and stable catalog IDs only where a bounded candidate list is provided.
3. **All accepted worlds are persisted.** Reloading never asks the model to recreate accepted content.
4. **The save is authoritative.** Generated briefs, selected recipes, exact transforms, world revisions, and canon are stored.
5. **Runtime is not an asset repair shop.** URP materials, pivots, scale, colliders, LODs, footprints, and sockets are prepared and validated before release.
6. **All packs may be catalogued; not all packs belong in one settlement.** A generated region selects a coherent primary kit and explicitly compatible accent kits.
7. **Modularity requires authored grammar.** Knowing an object’s bounds is not enough to know what it means, which side is the front, or what may connect to it.
8. **Failure is local.** A bad parcel is repaired or replaced; a whole world is not discarded.
9. **The game never waits on an LLM during ordinary movement or combat.** Previously compiled content streams while future content is generated in the background.
10. **Quality is a gate, not an aspiration.** A settlement that fails readability, traversal, collision, material, service, or performance checks is not committed.

---

## 4. Authority Boundaries

| Concern | Authority | Never authorized |
|---|---|---|
| Lore, culture, tensions, history, names, faction intent | LLM structured brief after validation | Direct runtime state mutation |
| Region topology, hydrology, roads, parcel layout, placement | Deterministic compiler | Freeform model coordinates |
| Visual asset choice | Curated binder and assembly library | Arbitrary project paths from the LLM |
| Collision, navigation, portals, streaming | Unity runtime/compiler | Prose interpretation |
| Quest and encounter meaning | Structured generated records | Completion inferred from narrative text |
| Accepted world truth | Save data and committed world revision | Re-generation on load |
| Visual quality | Authored kits plus automated and human golden-master review | “Best effort” random output |

The local language model is a director and world writer. It is not a level editor, renderer, physics engine, or save authority.

---

## 5. Three Operating Timescales

### 5.1 Editor-time asset intake

Expensive one-time work performed before a build:

- discover and classify imported assets;
- repair and validate URP materials;
- normalize scale, pivots, orientation, and layer usage through project-owned wrappers;
- generate or author footprints, frontage, entrances, snap sockets, terrain-contact profiles, collider proxies, and navigation metadata;
- generate LOD/HLOD eligibility and performance costs;
- assemble modules into reviewed buildings, parcels, streets, rooms, and district cells;
- render catalog thumbnails and diagnostic views;
- reject broken or semantically ambiguous assets.

### 5.2 Generation-time compilation

Performed on new-game creation, travel planning, or background jobs:

- request structured high-level intent from the LLM;
- validate it against current canon and available kits;
- compile terrain, roads, districts, parcels, assemblies, interiors, and population;
- run quality gates;
- persist immutable accepted artifacts.

This may use background worker threads for pure data, but Unity object creation remains safely scheduled on the main thread.

### 5.3 Live runtime execution

Fast deterministic work only:

- stream compiled cells;
- pool and activate approved prefabs;
- restore saved state;
- run AI, quests, combat, dialogue, and local simulation;
- request future briefs asynchronously;
- stage validated world revisions outside the player’s view.

No large settlement is invented synchronously because the player crossed a trigger.

---

## 6. Hierarchical World Representation

The compiler must reason at several scales. Skipping a layer creates the “objects dropped in lines” failure.

### 6.1 World graph

Represents regions, major settlements, wilderness routes, political borders, watersheds, major dungeons, and long-distance dependencies. This is topology, not rendered geometry.

### 6.2 Region

Defines climate, elevation range, geology, biome, dominant culture, danger, construction families, resource economy, lighting mood, weather, and compatible asset kits.

### 6.3 Landscape cell

Defines terrain fields, waterways, roads, vistas, vegetation masks, encounter zones, landmark visibility, and streaming boundaries.

### 6.4 Location

A settlement, ruin, cave, temple, camp, dungeon, hospital, arena, island, or other coherent destination. Each location has a morphology recipe rather than a generic list of objects.

### 6.5 District or zone

Examples include market, harbor, sacred precinct, residential quarter, military ward, industrial edge, necropolis, sewer branch, dungeon tier, and cyberpunk nightlife block. A district owns density, street width, verticality, wealth, security, palette, service requirements, and prop grammar.

### 6.6 Street and parcel graph

Road splines produce frontage. Frontage is subdivided into parcels. Parcels own setbacks, entrances, yards, alleys, service access, walls, and building envelopes.

### 6.7 Assembly

A reviewed composition such as a complete smithy parcel, inn courtyard, rowhouse cluster, gate complex, cave chamber, hospital wing, shrine terrace, or sewer junction. An assembly can contain variants and nested sockets but already embodies sound level-design decisions.

### 6.8 Interior cell

A portal-connected room graph with compatible shell dimensions, entrances, vertical links, encounter capacity, furniture sockets, and gameplay function.

### 6.9 Microdressing

Props, decals, vegetation, damage, signs, lights, sound emitters, and story traces placed only through typed sockets and density rules.

### 6.10 Runtime state

NPCs, inventories, ownership, damage, discovered facts, quests, doors, containers, faction control, and world revisions. This layer changes without destroying the compiled spatial foundation.

---

## 7. The Asset Kit System

### 7.1 Raw assets are ingredients, not generation units

Every imported pack begins in quarantine. It becomes runtime eligible only after it has a project-owned catalog entry. Original third-party files remain untouched where practical; wrapper prefabs and metadata live under YourQuest-owned folders.

### 7.2 Required `YQAssetKitManifest`

Each visual kit records:

```text
kitId
displayName
sourcePack
genreTags                 fantasy, gothic, cyberpunk, historical, horror...
cultureTags               viking, imperial, rural, pirate...
environmentTags           mountain, coast, underground, urban...
compatiblePrimaryKits
compatibleAccentKits
forbiddenKitCombinations
materialProfileId
lightingProfileIds
terrainProfileIds
audioProfileIds
populationProfileIds
assemblyIds
validationVersion
contentHash
releaseEligible
```

### 7.3 Required `YQSpatialAssetRecord`

Every eligible prefab or assembly records:

```text
assetId                    stable project ID, never an LLM-authored path
kitId
semanticRole               house, gate, wall, roof, stall, altar, clutter...
compositionScale           atom, module, building, parcel, district, landmark
variantFamilyId
localBounds
footprintPolygon
clearanceVolume
frontDirection
entranceSockets[]
connectionSockets[]
dressingSockets[]
foundationProfile
allowedSlope
heightClass
allowedNeighbors[]
forbiddenNeighbors[]
roadRelationship
interiorCompatibility[]
colliderProfile
navigationProfile
rendererCost
memoryCost
lodProfile
materialValidationState
spawnWeight
rarityBudget
```

Bounds alone are insufficient. A house needs a front, a valid entrance, clearance, road relationship, foundation behavior, semantic function, and neighbors.

### 7.4 Composition ladder

The library is built upward:

```text
Atoms -> Modules -> Complete Buildings -> Parcel Cells
      -> Street Cells -> District Cells -> Location Recipes
```

Runtime generation should favor the highest reviewed composition scale that still permits meaningful variation. A complete curated parcel is safer than separately placing its house, fence, shed, cart, firewood, and plants.

### 7.5 Golden cells

Each supported kit requires human-reviewed golden cells demonstrating its intended use:

- arrival or gate;
- residential parcel;
- commercial parcel;
- civic or sacred parcel;
- service/back-of-house parcel;
- edge transition to wilderness;
- one hero landmark;
- one interior chain;
- damaged, abandoned, or hostile variant where applicable.

The compiler recombines and parameterizes these design patterns. It does not attempt to rediscover architecture from filenames.

### 7.6 Kit policy for the available libraries

The existing libraries should be registered as location families, not thrown into a universal pool:

- **Settlement families:** MedievalKingdom, ModularVikingVillage, NativeAmericanVillage, RuralTown, TownSmith, MilitaryCamp.
- **Sacred/monument families:** GothicCathedral, MountainTemple, OlympusTemple.
- **Dungeon/hostile families:** HallowedDepths, HauntedVillage, CaveOfHiddenTomb, MysticDungeon, TheSewers, WitchHouse, HouseOnaHill, HorrorHospital.
- **Special destination families:** CyberpunkCity, GladiatorArena, TheMessengerMountain, PirateIsland, VillaForge.

These are initial classifications, not automatic compatibility. Catalog review decides whether a pack is a primary kit, accent kit, interior kit, landmark kit, or encounter kit. For example, a Gothic Cathedral may be a landmark inside a compatible medieval district, while Cyberpunk City is never an unannounced accent in a medieval village.

---

## 8. Structured LLM Contracts

The model generates several small, typed documents instead of one giant “make a town” response.

### 8.1 `WorldIntentRecord`

- seed and revision parent;
- genre phase and allowed transitions;
- world themes and emotional register;
- major powers and conflicts;
- region count and topology constraints;
- required anchors from player origin and canon.

### 8.2 `RegionBrief`

- region identity and history;
- climate, geology, elevation, and biome intent;
- culture and economy;
- construction semantics;
- danger and encounter ecology;
- landmark requirements;
- compatible semantic kit tags;
- neighboring-region relationships.

### 8.3 `LocationBrief`

- location type and morphology archetype;
- population band and economic purpose;
- district list and adjacency requirements;
- required services and hero POIs;
- faction ownership and danger state;
- wealth, age, maintenance, damage, and occupation layers;
- local stories that must be visible spatially;
- connection requirements to roads, water, and nearby locations.

### 8.4 `DistrictBrief`

- district role;
- density and height range;
- street topology preference;
- primary and compatible accent palette semantics;
- mandatory parcel functions;
- landmark/view relationships;
- population and encounter needs;
- dressing themes and forbidden motifs.

### 8.5 `WorldMutationBrief`

- reason for the change;
- affected cell IDs;
- facts that must remain true;
- facts that may be retired;
- visual phase transition;
- gameplay migration requirements;
- inhabitants, quests, and inventories to preserve;
- urgency and whether the player may witness it.

All contracts have strict schemas, bounded enums, length limits, stable IDs, version fields, validation reports, and deterministic normalization. Narrative prose may accompany them but cannot substitute for required fields.

---

## 9. The Deterministic World Compiler

### Stage 0: Resolve authority

Load save state, accepted canon, player history, world revision, model configuration, available release-eligible kits, and the generation seed. Never overwrite committed facts silently.

### Stage 1: Compile topology

Create the world and region graphs: adjacency, travel routes, watersheds, borders, settlement roles, wilderness buffers, and long-range landmark relationships.

### Stage 2: Compile terrain

Generate layered height fields from macro landform, erosion-inspired masks, local flattening constraints, watercourses, cliff rules, and landmark pads. Apply biome material masks at macro, mid, and micro scales. Terrain is shaped before buildings are placed.

### Stage 3: Compile travel networks

Use splines for roads, paths, walls, rivers, pipes, rails, and dungeon spines. Major roads follow terrain and world topology. Secondary streets follow district morphology. Paths must reach every required entrance. Unity’s Splines package is appropriate for path representation and object placement along paths, while the project retains deterministic ownership of the generated graph ([Unity Splines](https://docs.unity3d.com/ja/current/Manual/com.unity.splines.html)).

### Stage 4: Choose location morphology

Select a bounded recipe matching terrain, culture, purpose, population, and kit availability. Examples:

- organic crossroads village;
- river market town;
- ridge fortress;
- walled radial city;
- coastal pirate settlement;
- terraced sacred complex;
- modular cyberpunk block network;
- hub-and-branch cave;
- looped dungeon with locked shortcuts;
- institutional hospital wing graph.

Each recipe defines graph rules, not fixed scenery.

### Stage 5: Build the district graph

Place district anchors and solve hard adjacencies before geometry. Markets require high accessibility; industry needs service access; elite areas favor elevation or defensibility; harbors need water frontage; hostile barracks require patrol and combat space.

### Stage 6: Generate streets and parcels

Streets form blocks. Blocks split into parcels based on frontage, slope, district density, and access. Every parcel has a buildable polygon, setbacks, front direction, service edge, entrance target, and maximum envelope.

### Stage 7: Solve assemblies

For each parcel, query compatible reviewed assemblies. Use deterministic weighted constraint solving with bounded backtracking.

Hard constraints include:

- footprint and clearance fit;
- entrance reaches navigable frontage;
- slope and foundation validity;
- no structural overlap;
- kit compatibility;
- required district function;
- wall/gate continuity;
- interior portal compatibility;
- quest and combat space requirements;
- performance budget.

Soft scores include:

- silhouette variety;
- repetition distance;
- sightline quality;
- landmark framing;
- coherent roofline rhythm;
- density gradient;
- sunlight and shadow balance;
- story relevance;
- variant diversity;
- proximity preferences.

The solver keeps the highest-scoring valid solution. If none exists, it repairs the parcel, changes the parcel split, or substitutes a reviewed empty-space assembly such as a garden, yard, square, ruin, or work lot. It never fills failure with random props.

### Stage 8: Conform terrain and foundations

Apply cut/fill rules, retaining walls, stilts, stairs, terraces, ramps, drainage, vegetation exclusion, and material blending. Floating buildings and buried doors fail validation.

### Stage 9: Compile interiors

Attach compatible authored room graphs to exterior entrance sockets. Large interiors may stream as separate cells. Interior generation uses room and corridor assemblies, portal rules, vertical circulation, encounter envelopes, and furniture sockets—not independent furniture scatter.

### Stage 10: Add functional population

Assign homes, workplaces, beds, schedules, merchants, services, guard routes, faction spaces, loot ownership, and encounter capacities. NPC spawning uses reserved anchors and occupancy checks, preventing stacked characters.

### Stage 11: Apply narrative state

Translate structured local stories into approved visual layers: damage, repairs, occupation, plague, prosperity, ritual, siege, abandonment, corruption, celebration, or technological conversion. These layers use compatible overlays and socket sets.

### Stage 12: Dress by grammar

Microdressing occurs only after the spatial and functional world passes. Typed sockets control signs, crates, foliage, tools, food, laundry, rubble, decals, lights, particles, and audio. Density follows gradients: focal, lived-in, service, transition, and rest zones.

### Stage 13: Build traversal and visibility data

Generate or update NavMesh data per compiled cell, validate links, doors, jumps, ladders, stairs, and off-mesh transitions. Unity’s AI Navigation package supports edit-time and runtime NavMesh workflows, obstacles, and links ([Unity AI Navigation](https://docs.unity.cn/6000.2/Documentation/Manual/com.unity.ai.navigation.html)).

### Stage 14: Run quality gates

Reject or locally repair invalid output. No cell is committed before all release gates pass.

### Stage 15: Persist compiled artifacts

Store the structured brief, normalized compiler inputs, chosen recipes, exact placements, hashes, kit versions, traversal records, population records, validation report, and world revision. Runtime loads this artifact; it does not repeat the solve.

---

## 10. Settlement Composition Rules

### 10.1 A settlement must have a reason to exist

Its economy and geography produce its form. A mining settlement follows extraction and freight routes. A temple complex organizes procession and sacred sightlines. A military camp prioritizes perimeter, command, supply, training, and defensible circulation. This causal chain begins in the `LocationBrief` and becomes compiler constraints.

### 10.2 Required hierarchy

Every verbose settlement contains:

- an arrival sequence;
- one dominant silhouette or landmark;
- a primary circulation route;
- at least one public node such as a square, dock, shrine, hall, or courtyard;
- distinct functional districts or zones;
- secondary paths and service routes;
- edge transitions into wilderness;
- deliberate negative space;
- authored interiors for required services;
- narrative traces tied to local canon;
- safe, social, commercial, and dangerous space where appropriate.

### 10.3 Palette discipline

Each district receives:

- one primary construction kit;
- zero to two compatible accent kits;
- one terrain and vegetation family;
- a controlled color/material range;
- a lighting and weather profile;
- a prop density curve;
- explicit exclusions.

All available assets can become eligible across the total world. Using every asset in each city is prohibited because it destroys coherence.

### 10.4 Repetition discipline

The compiler tracks variant family use, orientation, adjacency, roofline, facade, landmark visibility, dressing set, and recent spatial history. Repetition is allowed when architecture logically repeats, but exact hero assemblies cannot recur within their rarity radius.

### 10.5 Hostile location discipline

Enemy type follows location ecology, faction ownership, traversal scale, encounter envelopes, and kit semantics. A haunted village may use undead or spectral families; a military camp uses its faction roster; sewers use creatures and humanoids compatible with confined navigation. The palette binder does not choose enemies from a universal hostile list.

---

## 11. Landscape and Environmental Cohesion

World quality begins outside buildings.

- Terrain macroforms determine routes and settlement sites.
- Hydrology precedes roads and habitation.
- Roads modify terrain and vegetation masks.
- Buildings modify local grading and drainage.
- Vegetation respects biome, slope, water, roads, sightlines, and human land use.
- Clutter density falls away from authored focal areas.
- Distant landmarks are positioned through view-cone constraints.
- Audio zones, fog, weather, and lighting reinforce region identity.
- Terrain Tools may support authoring and sculpting workflows, but generated fields and masks remain project-owned deterministic data ([Unity Terrain Tools](https://docs.unity3d.com/cn/6000.0/Manual/com.unity.terrain-tools.html)).

The landscape compiler must produce at least three detail frequencies:

1. **Macro:** mountains, valleys, coast, watershed, settlement basin.
2. **Mid:** terraces, berms, cuts, fields, clearings, rocky bands, drainage.
3. **Micro:** surface blends, decals, stones, roots, ground cover, tracks.

---

## 12. Dynamic Genre and World Shifts

YourQuest may move from fantasy to cyberpunk, science fiction, horror, or another authored phase. This is a world revision, not a material swap.

### 12.1 Transactional revision process

1. The LLM proposes a `WorldMutationBrief` grounded in player actions and canon.
2. Validation identifies preserved facts, affected cells, migration rules, and compatible kits.
3. The compiler creates a candidate revision without changing the live save.
4. NPC homes, services, quests, inventories, doors, and navigation are migrated.
5. Geometry, traversal, visual, lore, and performance gates run.
6. The revision commits atomically to a new world version.
7. The old version remains recoverable until the save transaction succeeds.

### 12.2 What may change in view

Weather, sky, emissive signs, decals, VFX, audio, vegetation state, minor props, dialogue, and lightweight overlays may transition while witnessed.

### 12.3 What changes offscreen

Road topology, large buildings, district reconstruction, terrain deformation, interior replacement, navigation rebuilds, and population migration occur in unloaded or concealed cells. The Goddess can narrate the already-validated previous step while the next revision compiles asynchronously.

---

## 13. Runtime Streaming and Performance

### 13.1 Compiled world cells

The runtime streams immutable `YQWorldCellArtifact` records containing prefab references, transforms, state IDs, activation groups, navigation data, portals, light/audio zones, and budget metadata.

### 13.2 Streaming rings

- **Simulation ring:** full AI and gameplay around the player.
- **Presentation ring:** rendered environment with reduced simulation.
- **Vista ring:** HLOD/impostor landmarks and terrain only.
- **Unloaded world:** structured state and aggregate simulation only.

### 13.3 Asset loading

Use Addressables or an equivalent project-owned abstraction to group assets by kit and cell, load asynchronously, and release handles symmetrically. Addressables provides asynchronous location/loading and catalog-based content organization ([Unity Addressables](https://docs.unity.cn/Packages/com.unity.addressables%401.22/manual/index.html)); reference-counted handles must be released when no longer used ([Addressables memory management](https://docs.unity3d.com/ja/Packages/com.unity.addressables%401.20/manual/MemoryManagement.html)).

### 13.4 Rendering strategy

- use compatible shared materials, texture atlases where justified, SRP Batcher compatibility, GPU instancing, LOD groups, and HLOD/impostor cells;
- pool common runtime instances;
- cap shadows, lights, particles, decals, and animated props per cell;
- avoid runtime material duplication;
- use portal/room visibility for authored interiors;
- use distance, cell visibility, CullingGroup-style logic, and GPU culling for dynamic exterior geometry.

Unity’s baked occlusion system is not suitable as the primary solution for runtime-generated scene geometry because it expects baked static geometry ([Unity occlusion culling](https://docs.unity3d.com/es/current/Manual/OcclusionCulling.html)). On compatible URP configurations, GPU Resident Drawer and GPU occlusion can reduce submission and hidden rendering costs, but they require compatible renderers and project settings ([Unity URP GPU culling](https://docs.unity3d.com/cn/6000.0/Manual/urp/gpu-culling.html)).

### 13.5 Hot-path rules

- no LLM calls in `Update`, movement, combat, or cell activation;
- no per-frame scene-wide searches, LINQ, JSON serialization, or registry scans;
- no runtime URP repair loops;
- no settlement-wide Instantiate burst in one frame;
- main-thread activation is time-sliced and cancellable;
- background compilation uses pure data and immutable inputs;
- failed or slow future generation never blocks the current playable cell.

### 13.6 Initial performance budgets

Budgets are calibrated on declared target hardware and then locked per release:

- 60 FPS target with 16.6 ms frame budget in ordinary exploration;
- world-streaming work budgeted to approximately 1–2 ms of main-thread time per frame;
- no model request on the critical rendering or input path;
- no visible activation hitch above 50 ms during normal travel;
- deterministic spatial compilation, excluding model latency and Unity object activation, targeted below 5 seconds for a beta-scale settlement on development hardware;
- strict per-cell renderer, triangle, light, shadow, VFX, audio, NavMesh, and memory envelopes stored in the manifest and enforced by validation.

Exact content budgets must come from profiler captures; they are not guessed permanently in code.

---

## 14. LLM Scheduling

The LLM queue is separate from world streaming and ordinary NPC conversation.

### Priority classes

1. player-blocking only during explicit new-game/loading flows;
2. near-future world briefs;
3. quest/NPC content needed soon;
4. speculative distant content;
5. flavor enrichment and revision candidates.

### Scheduling rules

- request briefs well before the player reaches an uncompiled frontier;
- batch related semantic decisions instead of calling once per object or NPC;
- impose timeouts, cancellation, token limits, schema retries, and concurrency limits;
- cache valid responses by content hash;
- persist accepted output immediately through the save transaction;
- degrade to an already-compiled transition region when the model is late;
- never show a modal “Goddess shapes your world” window during ordinary walking;
- allow Goddess narration to type independently from the next background request;
- log latency, schema failures, retry causes, queue depth, and blocking incidents.

---

## 15. Quality Gates

### 15.1 Asset gate

- valid URP material and no magenta renderer;
- expected scale, orientation, pivot, and bounds;
- valid collider and navigation profile;
- required sockets and semantic metadata;
- acceptable LOD and renderer cost;
- catalog thumbnail renders successfully.

### 15.2 Geometry gate

- no forbidden overlaps or unsupported floating structures;
- entrances and doors remain clear;
- walls, roofs, stairs, bridges, and foundations connect;
- terrain deformation remains within thresholds;
- no object violates its parcel or clearance volume.

### 15.3 Traversal gate

- player spawn reaches every required public service and quest anchor;
- roads connect to entrances;
- NavMesh islands, impossible stairs, blocked doors, missing links, and stacked NPC anchors fail;
- combat locations have valid approach, cover, retreat, and enemy capacity.

### 15.4 Functional gate

- settlement archetype has all required districts and services;
- inhabitants have valid homes, workplaces, schedules, and spawn anchors;
- shops have owners, inventory contracts, access, and readable presentation;
- interiors correspond to exterior roles;
- hostile palettes and enemy rosters agree.

### 15.5 Composition gate

- landmark is visible from required approach views;
- arrival, primary path, public node, districts, and edges are legible;
- palette compatibility score passes;
- repetition, roofline, density, and clutter thresholds pass;
- empty space is deliberate and proportionate;
- generated diagnostic screenshots show no severe occlusion, material, clipping, or scale violations.

Automated scores catch known failures. Human review establishes golden masters and approves new kit grammar. No honest system can guarantee AAA composition from raw assets without this authored quality foundation.

### 15.6 Performance gate

- cell and active-ring memory budgets pass;
- renderer, triangle, draw-call, shadow, light, particle, and AI counts pass;
- activation capture passes hitch limits;
- no leaked Addressable handles or generated materials;
- navigation and physics costs remain inside profile targets.

---

## 16. Required Development Tools and Test Scenes

### 16.1 Asset Intake Workbench

Displays one asset or assembly with bounds, footprint, front direction, sockets, collider, LODs, materials, semantic tags, performance cost, and validation results.

### 16.2 Assembly Authoring Workbench

Allows a designer to construct and save reviewed buildings, parcels, streets, rooms, and district cells from imported packs without modifying source assets.

### 16.3 Settlement Golden Master Scene

Contains fixed seeds for one supported kit, diagnostic camera paths, traversal probes, composition views, profiler markers, and expected validation reports. This is the primary proof scene—not the live game scene.

### 16.4 Runtime Streaming Test Scene

Exercises cell loading/unloading, cancellation, fast travel, save/reload, pooling, memory release, NavMesh availability, and frame pacing.

### 16.5 World Revision Test Scene

Applies fantasy-to-hostile, prosperous-to-ruined, and fantasy-to-cyberpunk revisions while verifying canon, NPC, quest, inventory, and traversal migration.

### 16.6 Tool Lifecycle and Archive

Production menus contain only tools used by the current work stage. A completed one-off command must be deleted when it has no diagnostic value, or moved under `AAA World Generation > Archived Tools` when reproducibility or repair value remains. The archived-tool registry records the tool ID, retirement reason, and active successor. Archived tools never run automatically and are not treated as part of the normal production workflow.

---

## 17. Persistence and Reproducibility

Every committed location stores:

- world seed and compiler version;
- parent and current world revision IDs;
- accepted LLM brief IDs and content hashes;
- kit manifest versions;
- location, district, road, block, parcel, assembly, and interior records;
- exact stable asset IDs and transforms;
- runtime entity and spawn-anchor IDs;
- validation report and repair history;
- mutable gameplay state separate from immutable compiled layout.

If a kit changes between releases, an explicit migration either preserves old compiled references or upgrades them transactionally. The system never silently re-rolls a town because an asset registry order changed.

---

## 18. Production Content Minimum

The architecture cannot compensate for a missing curated composition library. Before calling one visual family production-ready, it should contain approximately:

- 3 location morphology recipes;
- 4–6 district/zone recipes;
- 20–40 reviewed parcel assemblies across functions and wealth states;
- 6–10 hero POI or landmark assemblies;
- 8–15 street, gate, bridge, edge, and transition assemblies;
- compatible interior chains for every required public service;
- 3–5 narrative state layers such as prosperous, poor, damaged, occupied, haunted, or transformed;
- sufficient facade, roof, dressing, vegetation, sign, light, and damage variants to satisfy repetition budgets.

These numbers are starting production targets, not universal laws. A cave kit needs chamber and connector grammar rather than urban parcels. A hospital needs ward, hall, stair, service, and secure-room graphs. Each family receives a domain-specific recipe set.

For beta, finish **one primary family** to the complete quality bar before expanding every pack. Cataloging every pack is compatible with this approach; claiming all of them are generation-ready before their grammar exists is not.

---

## 19. Migration from the Current Generator

### Preserve

- local LLM transport and health checks;
- structured parse/validate/normalize principles;
- save-as-authority architecture;
- stable semantic asset binding concepts;
- useful asset discovery metadata;
- existing player, quest, NPC, combat, and persistence systems that do not own layout.

### Replace or retire

- direct prefab scatter in `YQGeneratedWorldRuntimeBuilder`;
- position selection based primarily on rings, rows, jitter, or raw bounds;
- universal asset candidate pools;
- runtime material repair as the normal path;
- synchronous settlement creation after a proximity trigger;
- acceptance of fallback camps as successful towns;
- layout systems that have no roads, parcels, frontage, district graph, or authored assemblies.

### Introduce

```text
YQAssetKitManifest
YQSpatialAssetRecord
YQAssetIntakeProcessor             editor-only
YQAssemblyLibrary
YQWorldIntentRecord
YQRegionBrief
YQLocationBrief
YQWorldCompiler
YQTerrainCompiler
YQRoadGraphCompiler
YQSettlementCompiler
YQParcelSolver
YQInteriorCompiler
YQWorldQualityEvaluator
YQWorldCellArtifact
YQWorldCellStreamer
YQWorldRevisionService
YQWorldGenerationScheduler
```

These names describe ownership boundaries; implementation should reuse suitable existing classes rather than duplicate them.

---

## 20. Milestone Build Plan

### WG0 — Stop the scatter architecture

**Deliverables**

- freeze new features in the current settlement scatterer;
- document which existing class owns generation, registry binding, save data, and runtime streaming;
- add a feature flag so the legacy path remains available only for comparison;
- define one target kit and one fixed-seed settlement benchmark.

**Exit criteria**

- no more effort is spent tuning random spacing as the production solution;
- legacy output can be compared without becoming save authority.

### WG1 — Asset intake and validation

**Deliverables**

- implement kit and spatial asset records;
- build the Asset Intake Workbench;
- normalize one primary kit through wrappers;
- remove all magenta materials from its release-eligible catalog;
- author footprints, fronts, sockets, foundations, LOD/collider data, and thumbnails.

**Exit criteria**

- every eligible asset passes the asset gate;
- no raw unclassified prefab can enter generation.

### WG2 — Golden assembly library

**Deliverables**

- author the first buildings, parcels, street cells, edge cells, landmark, and interiors;
- define variant, compatibility, and narrative-state rules;
- create the Settlement Golden Master Scene.

**Exit criteria**

- a designer can assemble a coherent settlement manually using only registered assemblies;
- the kit has sufficient coverage for one complete settlement archetype.

### WG3 — Deterministic settlement compiler

**Deliverables**

- morphology recipe, district graph, road spline, block, parcel, and assembly solver;
- terrain conformance and entrance routing;
- deterministic local repair and validation report;
- fixed-seed regression tests.

**Exit criteria**

- at least 20 fixed seeds produce valid, recognizably related but meaningfully varied settlements;
- zero forbidden overlaps, unreachable required entrances, stacked NPC anchors, or magenta materials;
- save/reload reproduces exact placement.

### WG4 — Landscape and interiors

**Deliverables**

- macro/mid/micro terrain fields;
- roads, rivers, vegetation masks, vistas, and edge transitions;
- authored interior graph attachment and portal streaming;
- navigation generation and traversal probes.

**Exit criteria**

- settlement is integrated into land rather than sitting on it;
- all required interiors and services are traversable;
- approach views and skyline gates pass.

### WG5 — Runtime cell streaming

**Deliverables**

- compiled cell artifact format;
- asynchronous loading, activation budgets, pooling, cancellation, and memory release;
- simulation/presentation/vista rings;
- profiling and stress scene.

**Exit criteria**

- ordinary travel never opens a blocking generation modal;
- streaming, fast travel, save/reload, and repeated boundary crossing remain within locked budgets.

### WG6 — LLM semantic direction

**Deliverables**

- strict world, region, location, and district schemas;
- available-kit-aware prompting;
- validation, normalization, retries, caching, and persistence;
- asynchronous future-region queue.

**Exit criteria**

- different valid briefs produce spatially and narratively distinct locations without bypassing the compiler;
- invalid, late, or unavailable model responses do not block play;
- accepted content is never regenerated on load.

### WG7 — Dynamic world revisions

**Deliverables**

- transactional mutation briefs and candidate revisions;
- NPC, quest, inventory, service, and canon migration;
- visual overlay transitions and offscreen reconstruction;
- rollback and save-version tests.

**Exit criteria**

- three tested revision types preserve required state and pass all world gates;
- genre shifts are coherent transformations, not mixed-kit palette accidents.

### WG8 — Production hardening and expansion

**Deliverables**

- add additional kit families one at a time through the same intake and golden-cell process;
- seed diversity suite, screenshot regression suite, long-session streaming tests, memory tests, and telemetry;
- user-facing recovery for corrupted or incompatible cells;
- release documentation and profiler baselines.

**Exit criteria**

- every release-eligible family independently passes its domain gates;
- the beta target route contains multiple polished locations without frame, save, material, traversal, or blocking-model failures.

---

## 21. First Vertical Proof

The first proof should be deliberately narrow:

- one visually complete primary kit, preferably `ModularVikingVillage` or the best-authored medieval family after intake review;
- one valley or coastal region cell;
- one organic settlement with 2–3 districts;
- one arrival road, central public node, landmark, edge transition, commercial service, smith, inn/home interiors, and hostile nearby POI;
- 20 fixed seeds;
- LLM variation limited to culture, economy, tension, history, district emphasis, and narrative state;
- deterministic layout compile and exact persistence;
- full asset, traversal, composition, and performance validation.

This proof is successful only when screenshots look like different places made by the same competent world-design team—not different piles selected from the same folder.

---

## 22. Work GPT Execution Protocol

When implementing this plan, Work GPT must:

1. Work on one milestone and one ownership boundary at a time.
2. Inspect the current authoritative implementation and direct dependencies before editing.
3. Preserve source packs, GUIDs, serialized data, and working gameplay systems.
4. Prefer project-owned wrappers and metadata over editing third-party content.
5. Never solve a visual problem by adding more random objects.
6. Never let the LLM select arbitrary Unity paths, transforms, or executable behavior.
7. Add schemas, validators, deterministic tests, and profiler markers with each compiler stage.
8. Include basic `// note:` comments around non-obvious executable chunks in new or materially changed scripts.
9. Produce one fixed-seed before/after capture and validation report for every spatial milestone.
10. Compile once after the coherent change, run the narrow relevant test scene, and stop when exit criteria pass.
11. Report what remains honestly; do not label fallback camps, partial kits, or unvalidated layouts as finished settlements.

### Prohibited “fixes”

- increasing random object counts;
- shrinking spacing until the scene looks denser;
- lining buildings along arbitrary axes without a street/parcel graph;
- mixing all packs to demonstrate asset coverage;
- generating missing materials at runtime as the primary repair;
- replacing failed towns with a camp while reporting success;
- calling the LLM once per prop, building, or frame;
- rebuilding accepted worlds whenever the registry changes.

---

## 23. Definition of Done for a Production Generated Settlement

A settlement is done only when:

- its origin, economy, culture, and conflict are represented in structured records;
- it has a valid morphology, district graph, roads, blocks, parcels, and assembly ownership;
- it reads clearly from approach and contains a memorable landmark and public node;
- every building has a function, valid foundation, entrance, access, and compatible palette;
- required interiors match exterior functions;
- inhabitants, services, schedules, factions, enemies, and quest anchors have valid spatial homes;
- props and environmental storytelling derive from sockets and local canon;
- all materials, LODs, colliders, navigation, and traversal links pass;
- it stays within profiled rendering, physics, AI, memory, and streaming budgets;
- it survives save/reload exactly;
- the local LLM can be unavailable without breaking movement, combat, streaming, or accepted world state;
- a human reviewer would describe it as an intentional place rather than generated asset placement.

---

## 24. Final Product Principle

YourQuest’s unique promise is not that a language model places infinite objects. It is that the game understands what kind of world the player is creating, writes a coherent future for that world, and compiles that intent through a professional library of spatial grammar into places that feel authored.

The road to convincing procedural environments is therefore:

```text
Curated assets
+ authored composition grammar
+ deterministic hierarchical solving
+ strict validation
+ persistent world state
+ asynchronous LLM direction
= a world that can change without becoming nonsense
```

That is the production architecture capable of reaching the requested experience.
