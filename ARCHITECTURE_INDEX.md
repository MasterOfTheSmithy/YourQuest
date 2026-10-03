# YourQuest architecture index

This index records production ownership and boundaries; it does not replace product requirements or certify runtime behavior. Recheck the relevant symbols before sensitive changes. Workflow/evidence pointers live in `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json`.

## Ownership map

| Subsystem | Authoritative classes/data | Runtime/editor | State and persistence | Allowed writers / constraints |
|---|---|---|---|---|
| Startup and presentation gate | `YourQuestTutorialAutoBootstrap`, `RuntimeModalUiBlocker`, `YQStartupLoadingScreen` | Runtime | Reads selected profile/world | Only bootstrap releases the final presentation gate after materialization and visible coverage. |
| Profile/state | `YQProfileSaveSystem`, `YQProfileCommitStore`, `PlayerStateManager`, `WorldStateManager`, `PlayerState`, `WorldState` | Runtime | Paired player/world revisions, schema 7 declared in `YQStateContract` on 2026-09-30, checksums and recovery copies | Domain systems prepare state; profile owner publishes paired commits. Verify the current constant before migrations. |
| Identity | `YQStateFoundation`, `YQStateContract`, typed IDs and `YQEntityIdentityRecord` | Runtime/data | Stable IDs and parent relationships | Display labels never recreate accepted identity. |
| Origin/character creation | `YQOriginQuestionnaireUI`, `YQOriginGenerationService`, `GeneratedRpgContentService`, `YQContentProposalBoundary` | Runtime | Accepted origin fields in `PlayerState`; transient Goddess voice separate | Typed proposal/validation/mutation receipt; no direct prose-to-mechanics path. |
| Spatial world authority | `YQWorldGenerationService`, `YQWorldGenerationArchitecture`, `YQSpatialPlanVersionRouter`, `GeneratedWorldPlanRecord`, V2 contracts | Runtime/data | `worldSeed`, semantic fingerprint, content/validation hashes, acceptance state | One complete V1 or accepted V2 authority; no mixed artifacts. |
| Terrain/routes/water/sites | `YQSpatialBlueprintCompilerV2`, `YQSpatialMaterializationV2`, `YQGeneratedWorldTerrain`, `YQGeneratedWorldSpatialPlanner`, `YQGeneratedRiverBridge`, settlement/site contracts | Runtime/editor compiler | Persisted plan and compiled artifacts | Canonical plan first; materializer consumes accepted plan and approved assets. |
| World materialization | `YQGeneratedWorldRuntimeBuilder`, `YQGeneratedWorldEnvironment`, `YQGeneratedWorldPopulation` | Runtime | Compiled-world readiness and generated records | Sole production materialization coordinator; legacy scatter is compatibility only. |
| Continuous streaming | `YQPlayerFollowingSemanticChunkStreamer`, `YQContinuousWorldCellAuthority`, `YQContinuousWorldFeatureAuthority`, `YQSemanticChunkRuntimeVerification` | Runtime/editor verification | Semantic cell records `continuous_world_cell_v5`, edges `continuous_edge_v3` | Sole streamed terrain/semantic owner; current-cell and visible-ring readiness are hard contracts. |
| Validation/diagnostics | `YQGeneratedWorldIntegrityValidator`, V2 blueprint validators, `YQProductionBaselineDiagnostics`, editor verification menus | Runtime/editor | Receipts/logs; read-only observers | Must report rejection/failure; diagnostics do not become authority. |
| NPC/hostiles/combat | `YQGeneratedNpcPlanningService`, `YQGeneratedEnemyRuntimeSafety`, `YQInvestorEnemy`, `YQInvestorCombat`, tutorial combat scripts | Runtime | NPC/enemy records in world/player state as applicable | Generated data maps to curated runtime components; tutorial fixtures remain scoped. |
| Quests/narrative/dialogue | `QuestRecord`, objective records, `YQQuestCompletionDirector`, `NpcDialogueAgent`, `DialogueThinkService`, session/memory stores | Runtime | Player/world state and profile-scoped dialogue stores | Stable objective/identity IDs; prose is presentation. |
| Factions/reputation | `WorldState.factionAttitudes`, `FactionRecord`, `WorldDeltaApplier`, `LLMThinkCycle` | Runtime | World state persistence | Normalized faction deltas only; no display-name authority. |
| UI | `YQTitleScreenUI`, `YQOriginQuestionnaireUI`, `YQStartupLoadingScreen`, `YourQuestTutorialHud`, pause/dialogue/progression UI | Runtime | Reads authoritative state | UI cannot own canonical gameplay state or bypass modal/startup gates. |
| Editor/content pipeline | `Assets/Assets/Scripts/Generated/Editor/*`, asset intake/manifest/semantic compilers | Editor | Writes reviewed manifests/artifacts | Imported assets require review contracts, semantic keys, and validation before runtime eligibility. |

## Generation phases and contracts

The production ownership chain is: profile selection → origin acceptance → `YQWorldGenerationService` plan/fallback → semantic/spatial V2 compilation and validation → `YQSpatialPlanVersionRouter` authority selection → `YQGeneratedWorldRuntimeBuilder` compiled-world materialization → origin/current terrain/NPC publication → `YQPlayerFollowingSemanticChunkStreamer` continuation-cell planning/materialization → visible coverage/presentation release.

Seed derivation is persisted in the world plan and cell continuation uses stable hash inputs plus accepted semantic/route/water data. V2 records carry schema, generation, validation, semantic/content hashes, acceptance state, and bounded preflight limits. `YQContinuousWorldCellAuthority` captures immutable origin height data and accepted route/water envelopes before off-main-thread sampling. Boundary contracts cover edge samples, relief, biomes, route corridors, hydrology, landmark/semantic features, and persisted cell identity.

## Historical baseline evidence

The measurements below belong to the September 15 baseline. They are not current performance measurements. Consult the goal ledger for later G08 receipts and the selected workflow.

The documented beta fixture uses profile `beta-dev-canonical`, world seed `76603739`, origin seed `beta-origin-v1`, `CompiledWorld`, and accepted V2 or persisted V1 authority. Startup/materialization and semantic save/reload/rebind evidence are documented. Deep streaming currently reports a 300 m/s forward rate of 2.20 cells/second, diagonal minimum lead 0, readiness P95/max 6.480/6.491 s, one missed deadline, and a 0.168 s synchronous slice against a 0.050 s budget. Persistence measured 0.111 s against a 0.100 s budget. These remain blockers, not green gates.
