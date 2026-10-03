# WG0 World Generation Baseline

**Status:** Active architecture boundary

**Parent specification:** [YourQuest AAA Procedural World Generation Design Plan](YourQuest_AAA_Procedural_World_Generation_Design_Plan.md)

## Purpose

WG0 freezes the current physical generator as a legacy comparison path. It prevents further random-spacing work from being mistaken for progress toward the compiled-world architecture while keeping the existing game playable until the replacement owns startup, persistence, and reveal.

## Current ownership map

| Responsibility | Current owner | WG0 decision |
|---|---|---|
| Player origin request and initial-generation lock acquisition | `YQOriginGenerationService` | Preserve |
| Structured world-plan generation and deterministic fallback records | `YQWorldGenerationService` | Preserve as an input boundary |
| Physical terrain, settlement, decoration, vegetation, and population materialization | `YQGeneratedWorldRuntimeBuilder` | Freeze as legacy comparison only |
| Approved runtime prefab registry | `YQRuntimeWorldAssetRegistry` | Preserve for discovery evidence; do not treat raw entries as spatially generation-ready |
| Semantic palette derivation | `YQWorldAssetCatalog` | Preserve as migration input; replace loose universal selection with kit manifests |
| Generated world persistence | `WorldStateManager` and `GeneratedWorldPlanRecord` | Preserve save authority; extend through versioned compiled-cell artifacts |
| Startup loading/reveal presentation | `YQStartupLoadingScreen` and the builder readiness contract | Preserve until the compiled-world owner replaces the entire transaction |
| Player/UI generation lock consumers | `PlayerController`, `YQInvestorPlayerMotor`, `RuntimeModalUiBlocker`, and related presentation systems | Do not break or duplicate |

## Architecture gate

`YQWorldGenerationArchitecture.ActiveMaterializationPath` is the explicit switch between:

- `LegacyScatterComparison`: today’s playable baseline;
- `CompiledWorld`: the future deterministic compiler and cell streamer.

The project remains on `LegacyScatterComparison` during WG0 because disabling the builder alone would strand the initial-generation gameplay lock. The switch may change only when the compiled-world path owns construction, readiness, reveal, and failure recovery together.

Manual calls to the legacy builder also respect the gate, preventing an old context-menu command from rebuilding over a compiled world later.

## First fixed benchmark

| Field | Value |
|---|---|
| Benchmark ID | `WG0_VIKING_VALLEY_001` |
| World seed | `YQ-WG0-VIKING-VALLEY-001` |
| Primary kit tag | `medievalvikingvillage` |
| Confirmed source root | `Assets/BefourStudios/MedievalVikingVillage` |
| Target location | Valley or sheltered coastal Viking settlement |
| District target | Arrival/service edge, residential core, market/civic node |
| Required landmark | One dominant hall, shrine, gate, or watch structure selected during asset intake |

The seed is now a stable architecture constant. WG1’s benchmark harness will inject it into an isolated golden-master plan without changing normal player saves.

## Baseline capture procedure

Before replacing physical placement:

1. Open `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`.
2. Confirm the console reports `WG0 LEGACY SCATTER COMPARISON PATH ACTIVE`.
3. Use a disposable benchmark save; never overwrite a player’s canonical save.
4. Capture the arrival, skyline, primary-route, public-node, service-frontage, edge-transition, and interior views.
5. Record material failures, unsupported/floating structures, overlaps, blocked entrances, disconnected paths, stacked NPCs, generation time, frame-time spikes, active renderer count, and memory.
6. Label all captures `LEGACY_COMPARISON`; they are evidence, not golden-master targets.

## WG0 exit conditions

- The legacy builder is explicitly gated and clearly logged as non-production.
- Its startup dependencies are documented so the replacement cannot deadlock the game.
- One confirmed asset family and fixed benchmark seed are selected.
- The next work item is WG1 asset intake, not another scatter-layout adjustment.

## Next implementation task

Build `YQAssetKitManifest` and `YQSpatialAssetRecord`, then create the editor-only intake validator for the `medievalvikingvillage` family. No prefab becomes compiled-world eligible until its material, scale, front direction, footprint, clearance, foundation, sockets, collider, navigation profile, and cost metadata pass.
