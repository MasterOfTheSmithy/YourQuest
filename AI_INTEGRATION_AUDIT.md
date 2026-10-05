# Earlier YourQuest AI integration audit

This document preserves the earlier integration's findings and provenance; it is not a fresh runtime receipt. Its schema-6 and performance observations are historical. Source now declares schema 7. Read `Docs/PROJECT_GUIDANCE_AUDIT_2026-09-30.md` for the current guidance audit and `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json` for workflow selection.

## Added

`AGENTS.md`, `AI_DEV_WORKFLOW.md`, `ARCHITECTURE_INDEX.md`, `TASK_ROUTING_MATRIX.md`, `MODEL_ROUTING.md`, `AI_CHANGE_CONTROL.md`, `UNITY_MCP_GUIDE.md`, `README_AI_ENGINEERING.md`, `TESTING_MAP.md`, `TEST_COVERAGE_GAPS.md`, `GOLDEN_PATH.md`, `SOURCES.md`, `opencode.json`, scoped instructions under `Assets/Assets/Scripts/`, `AI_CONTEXT/`, `PROMPTS/`, and `.opencode/agents/`.

## Modified

No gameplay/runtime C# file, scene, prefab, imported asset, package, project setting, save, or generated Unity folder was modified by this integration. Existing dirty working-tree changes were preserved.

## Generic kit material intentionally not copied wholesale

The attached kit's numbered prompt pack, generic unresolved domain sheets, and speculative names were used as workflow structure only. The active repository documents use verified owners: `YourQuestTutorialAutoBootstrap`, `YQProfileSaveSystem`, `YQWorldGenerationService`, `YQWorldGenerationArchitecture`, `YQSpatialPlanVersionRouter`, `YQGeneratedWorldRuntimeBuilder`, `YQPlayerFollowingSemanticChunkStreamer`, `YQContinuousWorldCellAuthority`, `YQContentProposalBoundary`, and the current state/quest/dialogue/UI systems.

## Architectural findings

- Unity `6000.3.2f1`; production PlaySafe/title scenes and a large project-owned/generated runtime/editor surface.
- One active compiled-world materialization authority and one semantic continuation streamer; V1 and legacy scatter remain compatibility paths with explicit routing.
- Seeded spatial V2 artifacts carry acceptance, schema/generation/validation versions, semantic/content hashes, bounded preflight, and route/water/terrain/site data.
- Paired profile persistence is owned by `YQProfileSaveSystem`/`YQProfileCommitStore`; player/world managers own active projections and schemas are 6.
- Origin, NPC, dialogue, progression, quest, faction, combat, and UI paths are implemented with structured state and typed LLM proposal boundaries.

## Risks and unresolved items

Deep streaming budget and persistence timing gates remain red in the latest receipt. Reviewed asset construction eligibility and historical compatibility-path retirement remain open. LLM backend availability and fresh Play Mode receipts must remain explicitly statused. MCP capabilities are not verified from repository files.

## Recommended next improvements

Use the golden path to attribute the streaming slice, then re-run the full baseline with fresh PlaySafe runtime evidence. Keep asset eligibility, paired-save timing, and compatibility retirement as separate reviewable goals.
