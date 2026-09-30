# YourQuest AI development workflow

This workflow applies the canonical rules in `AGENTS.md`. Requirements come from the user and GDD; source and fresh evidence establish behavior. Dated receipts establish only their recorded results. `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json` selects the maintained engineering workflow without replacing full goal specifications.

## Lifecycle

1. Intake the request with target behavior, non-goals, owner, acceptance criteria, authorized files, and evidence. For roadmap work, read the ledger and complete selected goal; do not launch an archive's tasks merely because it was attached.
2. Route through `TASK_ROUTING_MATRIX.md` and `AI_CHANGE_CONTROL.md`.
3. Load only the nearest scoped instructions, the relevant `AI_CONTEXT/` sheet, and the direct code/data dependencies.
4. Identify affected ownership, save, V2, determinism, streaming, and cross-system contracts before implementation. The configured agent performs competent review; named presets are not prerequisites.
5. Make the smallest coherent patch with the current available tools/model. Delegate only when authorized and useful; give any authorized worker a disjoint boundary and explicit return contract.
6. Verify with Unity MCP or its documented fallback.
7. Inspect the final patch against acceptance, ownership, and preserved user changes. Use an independent read-only review when available and warranted. Reuse existing authorization; ask only for a new action that requires approval.
8. Update architecture/context docs only when verified repository truth changes. Record the exact receipt, source identity, evidence level, and next row in the goal ledger. Run `node Tools/verify-project-guidance.mjs` after guidance/goal changes.

## Current evidence entry points

- Production map and beta gates: `Docs/Production_Architecture_Beta_Baseline_2026-09-15.md`.
- Reality/diagnostic receipt: `Docs/G01_Production_Reality_Receipt_2026-09-16.md`.
- State/transaction contract: `Docs/G02_State_Identity_Transaction_Receipt_2026-09-16.md`.
- Spatial authority: `Docs/G05_Semantic_World_Authority_Receipt_2026-09-16.md`.
- World-generation audit and plans: `Docs/World_Generation_Full_System_Audit_2026-09-07.md`, `Docs/WG0_World_Generation_Baseline.md`, and `Docs/YourQuest_World_Engine_Production_Plan.md`.
- Top-level regression: `YourQuest > Beta Baseline > Run Production Regression` (`YQProductionBaselineRegression`).
- Deep streaming/persistence harness: `YQSemanticChunkRuntimeVerification`.

## Status discipline

The September 15 baseline is historical and partial. Later G08 receipts are linked from the goal ledger; the latest linked 260 m/s R2 witness reports FAIL. Keep workflow position separate from acceptance, and preserve `PASS`, `PARTIAL`, `FAIL`, `BLOCKED`, `NOT_VERIFIED`, and `NOT_YET_TESTABLE` meanings. This workflow does not certify any gameplay gate.
