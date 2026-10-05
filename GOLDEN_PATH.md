# Golden path: streaming-hitch investigation

This worked example comes from the historical September 15 baseline: that run passed continuity, unload, revisit, and semantic persistence checks but failed timing/rate/lead gates. It is a diagnostic template, not the current execution selector. Use `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json` and G08 fix 4 for current work, including its separate 260 m/s stress contract.

## Intake and routing

Task: reduce a reproduced measured streaming hitch while preserving current-cell safety, deterministic continuation, semantic save/reload/rebind, and canonical content density. Review affected contracts with the configured coordinator because the change crosses `YQPlayerFollowingSemanticChunkStreamer`, `YQContinuousWorldCellAuthority`, materialization readiness, and performance budgets.

## Context packet

Read root/scoped instructions, the production baseline, `YQPlayerFollowingSemanticChunkStreamer`, `YQContinuousWorldCellAuthority`, `YQContinuousWorldFeatureAuthority`, `YQGeneratedWorldRuntimeBuilder`, `YQSemanticChunkRuntimeVerification`, and the current profiler/timing receipt. Identify whether the dominant slice is planning, terrain sampling, object creation, activation, ecology, overlay replay, unload, or persistence.

## Local-agent packet

Authorized scope: one measured stage and its focused diagnostics/tests after senior decision. Forbidden: lower required content, bypass `Traversable`, change seed/edge contracts, mix V1/V2 authority, alter save identity, or move player/streaming ownership. Required result: before/after timings, same-seed plan parity, current-cell/visible-ring safety, and save/reload/rebind parity.

## Unity verification

Run the deep semantic harness with the documented fixture/profile and speed, capture stage timing/allocation evidence, rerun continuity/order-independent tests, complete unload/revisit and save/reload/rebind, and inspect the final diff. Record `PASS`/`FAIL`/`BLOCKED`/`NOT_YET_TESTABLE` with seed, profile, cell, and owner.

## Review and escalation

The read-only reviewer checks that the optimization is measured, content-neutral, deterministic, and does not weaken readiness/validation. Escalate if the fix needs a new scheduler, authority, save format, stage order, or scene/bootstrap change. The task is complete only when the relevant gate passes; a faster but incomplete world is a regression.
