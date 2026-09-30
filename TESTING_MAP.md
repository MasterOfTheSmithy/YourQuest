# YourQuest testing map

The current project uses menu-driven editor verification and runtime harnesses in addition to any package-provided test runner. The tracked production evidence is distributed across these suites:

| Coverage | Current entry point | Evidence contract |
|---|---|---|
| Spatial V2 contracts | `YQWorldGenerationV2ContractTests`, `YQSpatialBlueprintV2Tests`, `YQSemanticWorldAuthorityTests` | Schema/generation/validation/hash/authority acceptance. |
| Seed/determinism | `YQGeneratedWorldSpatialPlannerSeedTests`, `YQContinuousTerrainDeterminismTests`, terrain/V2 tests | Fixed seed, different seed, traversal-order and continuation equivalence. |
| Routes/water/terrain | `YQRouteTraversalProbeVerification`, `YQTerrainApproachV2Tests`, `YQProceduralSettlementLayoutVerification` | Ground/traversal, route/bridge, approach, settlement constraints. |
| Asset/content eligibility | asset-kit palette tests, binding certification, semantic-site review/compiler menus | Approved semantic asset/binding/source signatures, not merely catalog presence. |
| Runtime production slice | `YQProductionSliceRegressionTests`, `YQProductionBaselineRegression`, `YQProductionBaselineDiagnostics` | Startup, origin, materialization, player/NPC/terrain, quest/dialogue/profile observations. |
| Streaming/persistence | `YQSemanticChunkRuntimeVerification` | Continuation, current-cell safety, unload/revisit, save/reload/rebind, timing budgets. |
| Editor/content tools | intake/compiler/review workbench menus | Scoped artifact generation and validation; editor PASS is not runtime PASS. |

The September 15 baseline is a historical partial result. Consult `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json` for later G08 evidence and the selected workflow. See `TEST_COVERAGE_GAPS.md` for unresolved verification boundaries. Guidance CI and a task's completion status do not replace these runtime gates.
