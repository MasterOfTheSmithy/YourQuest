# YourQuest — audit-integrated closed-beta goals

Twenty complete goals, originally rebuilt on 2026-09-16 and amended on 2026-09-28 to integrate the LLM architecture audit. This is a documentation update, not implementation or beta certification.

Use [GOAL_STATUS.json](GOAL_STATUS.json) for maintained workflow selection and evidence pointers. Run `node Tools/verify-project-guidance.mjs` from the project root before a handoff. `node Tools/project-goal.mjs G08` assembles the complete canonical goal and selected fix 4 workflow. See [project setup](../PROJECT_SETUP.md) for responsibilities and status semantics. The original September 16 ZIP must not overwrite these amended files.

**Current task: continue [8 fix 4](<goals/8 fix 4.md>), R1 publication/recovery → R2 streaming/performance → R3 physical world itinerary.** Its original requirements and numerical gates are unchanged, including the distinct 6→260 m/s stress. Do not add later AI/content work unless a reproduced defect blocks an existing G08 row.

The canonical editable specifications are `C:/Users/Garri/YourQuest/Docs/ClosedBetaRoadmap_2026-09-16/goals/`. Matching files at `C:/Users/Garri/Desktop/Game Dev/goals/` are execution copies, not a separate roadmap. Initial comparison found all twenty numbered files equal. The four Desktop-only G08 files are now preserved verbatim in the canonical set.

Read [AUDIT_INTEGRATION.md](AUDIT_INTEGRATION.md) for findings, owners, evidence levels, due points and tests. [ROADMAP.md](ROADMAP.md) retains the dependency graph, prior-roadmap analysis, target world architecture, migration map and final acceptance. Its section D and [Goal-specifications.md](Goal-specifications.md) are synchronized reading views of the individual goal files. Update them together after a canonical edit. [Supporting-architecture.md](Supporting-architecture.md) retains the shared design contracts. September 16 implementation descriptions are historical snapshots; the unchanged [audit](LLM-Architecture-Audit_2026-09-28.md) retains its own September 27–28 evidence and limitations.

Run one complete goal file at a time. Do not restart completed goals or run all audit follow-ups immediately. Finish G08, continue G09, and close each narrow earlier-owner follow-up before the dependent acceptance that needs it. Preserve valid unchanged receipts. No runtime work or future tasks were launched by this integration.

The default is one local generative model with isolated role prompts and deterministic tools; approximately 4B is a candidate baseline, not a mandate. Extra models/embeddings/adapters/backend changes require a measured benefit before adoption. Unadopted optional experiments are not release requirements.

## G08 workflow selection

- **Active:** [8 fix 4](<goals/8 fix 4.md>), together with [Goal-08](goals/Goal-08.md).
- **Historical workflow references; acceptance requirements retained by fix 4:** [8 fix 1](<goals/8 fix 1.md>), [8 fix 2](<goals/8 fix 2.md>), [8 fix 3](<goals/8 fix 3.md>). These files are preserved verbatim and must not be started as parallel replacement workflows.
- Next numbered goal after the G08 gate: [Goal-09](goals/Goal-09.md). The audit does not certify the G08 gate or authorize skipping a failed row.

## Package use

Extract the entire repaired-goals ZIP to keep its relative documentation links together. Use the complete selected file from `goals/` as the execution prompt. Source/evidence links inside the historical audit refer to the original YourQuest checkout and research pages; they are evidence references, not bundled production code. The package contains goals and supporting documents only, not game files or saves. See [VALIDATION.md](VALIDATION.md) for documentation and archive checks.

Goal 01 owns truthful discovery and instrumentation, including failed/blocked downstream results. Goal 20 alone certifies closed-beta readiness. Every individual prompt contains all thirteen required sections, expanded routing/safety/verification/handoff rules, and the world/final-acceptance appendix where directly required.

Evidence is source inspection plus explicitly attributed historical runtime reports; no new Unity build or Play Mode certification is claimed. Beta scope reductions and retained future extensions are explicit in sections B/G.

## Goal files
- [01 — Discover, document, and instrument production reality](goals/Goal-01.md)
- [02 — Stabilize canonical state, identity, profile commits, and service ownership](goals/Goal-02.md)
- [03 — Harden the existing LLM scheduler and proposal boundary](goals/Goal-03.md)
- [04 — Certify the approved beta asset and binding pipeline](goals/Goal-04.md)
- [05 — Establish the deterministic semantic world and feature graph](goals/Goal-05.md)
- [06 — Realize continuous terrain, hydrology, and connected routes](goals/Goal-06.md)
- [07 — Stabilize streaming, reconstruction, and persistent mutation overlays](goals/Goal-07.md)
- [08 — Build traversable sites, settlements, and semantic environmental density](goals/Goal-08.md)
- [09 — Stabilize the authoritative player and core RPG execution](goals/Goal-09.md)
- [10 — Complete persistent character creation and both origin routes](goals/Goal-10.md)
- [11 — Establish persistent residents, roles, and NPC knowledge](goals/Goal-11.md)
- [12 — Stabilize structured quests, dialogue memory, and consequence commits](goals/Goal-12.md)
- [13 — Make trade, rewards, and settlement services transactional](goals/Goal-13.md)
- [14 — Establish bounded social knowledge, relationships, law, and alignment](goals/Goal-14.md)
- [15 — Build trustworthy behavioral evidence and adaptive candidates](goals/Goal-15.md)
- [16 — Deliver meaningful adaptive unlocks and persistent player-responsive content](goals/Goal-16.md)
- [17 — Finish discovery, onboarding, player UI, and settings](goals/Goal-17.md)
- [18 — Curate audiovisual feedback, content density, balance, and opening pacing](goals/Goal-18.md)
- [19 — Harden persistence, performance, failure recovery, and long sessions](goals/Goal-19.md)
- [20 — Certify, package, and hand off the closed beta](goals/Goal-20.md)
