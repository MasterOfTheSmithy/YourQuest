# YourQuest project guidance audit

The project now has a canonical setup entry point, reconciled engineering guidance, a twenty-goal status ledger, and read-only tools for validating and assembling complete goal handoffs. The maintained September 28 roadmap amendments and existing game work are preserved. This audit concerns project guidance and setup; it does not implement the roadmap or certify gameplay.

## Scope and evidence

The audit covers the root engineering guides, all seven root/scoped `AGENTS.md` files, all eleven existing `AI_CONTEXT` sheets, the prompt library, the twenty maintained numbered goals and four G08 workflows, their aggregate reading views, the attached September 16 ZIP, and relevant Unity/GitHub setup. Goal objectives, prerequisites, routing, authority, verification, and completion boundaries were inspected; complete files are also checked structurally. Detailed gameplay implementation and every historical log were not audited.

The starting checkout was on `main` at `a692190af046f2950507b98e80b74914825fc22d`, with substantial modified and untracked game/asset work. Guide snapshots and file identities were preserved under `outputs/project-setup-2026-09-30/` before editing. No project-owned runtime C#, scene, prefab, `.meta`, package, project setting, imported asset, or save was intentionally changed by this setup work.

## Findings and disposition

| Finding | Evidence and consequence | Disposition |
|---|---|---|
| No concise repository entry point | Engineering guidance and goals were spread across separate local guides | Added root README and PROJECT_SETUP.md with one navigation path |
| Root authority order disagreed with the user's replacement rules | Existing AGENTS.md placed implementation above requirements and retained the earlier DevKit policy | Reconciled the canonical rule set with the current user instructions; scoped rules and GUIDs preserved |
| Save-schema descriptions were stale | `YQStateContract.CurrentStateSchemaVersion` in `YQStateFoundation.cs` declares 7; active guides said 6 | Corrected live guidance to source-inspected schema 7; kept dated schema-6 receipts historical |
| The attached ZIP is behind the maintained roadmap | Of its 25 files, 20 differ from local counterparts and five are unchanged; local amendments include the September 28 audit | Preserved the amended local specifications; recorded archive provenance and hash without re-extracting over them |
| Multiple G08 workflows could be mistaken for current work | Local README selects fix 4 and preserves fixes 1–3 as historical references | Added one explicit active selector and preserved hashes for all four workflows |
| Workflow progress and acceptance had no structured shared ledger | Status depended on dated prose spread across receipts and handoffs | Added GOAL_STATUS.json with dependencies, full-specification hashes, workflow position, evidence pointers, and separate acceptance state |
| Model routing could stall setup or force extra work | Root guides required Luna/Qwen presets and a distinct reviewer; optional OpenCode adapters were mixed with general workflow | Routed by responsibility and available configuration; preserved optional adapters and game inference settings |
| A prompt targeted a removed sample scene | COMMON_TASK_TEMPLATES.md said to reproduce in SampleScene | Replaced it with the actual production PlaySafe scene when the affected path requires runtime proof |
| Prompt discovery was broader than the user's engineering rules | Templates directed searches through all scenes/assets or imported readme conventions | Restricted investigation to affected references and existing project-owned tools |
| A boundary template implied first-visited authority | The neighboring-cell template asked which cell first established the fact | Pointed it to existing persisted/seeded shared-edge authority and both traversal orders |
| Baseline timing was described as current | Architecture/context/testing guides repeated September 15 measurements without dates | Labeled those measurements historical and routed later evidence through the ledger |
| Historical vault goals competed with the maintained roadmap | Vault overview used a different backlog/scope description | Marked the vault as a preserved supporting view; cards were not rewritten |
| Local guidance was unavailable on GitHub's default branch | Root guidance and Docs were untracked; GitHub returned 404 for AGENTS.md | Prepared a curated documentation/tooling review branch, separate from dirty game changes |
| Local task snapshots could enter broad commits | Existing ignore rules omitted outputs and root export/log artifacts | Added focused ignore entries without removing existing files or suppressing canonical Docs evidence |
| Guidance had no repeatable integrity check | No shared command checked all twenty specifications and their derived views | Added Node-based structure, hash, dependency, link, receipt, and local scene/contract checks; CI runs guidance-only checks |

## Goal review

All twenty numbered goals retain their complete thirteen-section specification. Their prerequisites form a backward-only graph. Goal 01 owns discovery and truthful downstream failure reporting; Goal 20 owns final beta certification. The source files, world/final acceptance appendices, numerical gates, existing audit amendments, and beta exclusions are preserved.

G01–G07 are recorded as historical workflow positions, without a new PASS claim or blanket restart. G08 fix 4 remains selected in R1 → R2 → R3 order. G09–G20 are queued; this task launches none of them. The handoff tool prints complete files and hashes rather than reducing a goal to its short objective.

The linked `G08_R2_Speed260_2026-09-30_095333_P01_REGRESSION_FAIL.md` receipt reports a 260 m/s coast-coverage failure and a maximum coast frame of **1.298 seconds** against **0.050 seconds**. It explicitly says remaining R1/G08 rows were not run in that focused scope. This is inspected existing runtime evidence, not a test executed by this audit. It does not establish current R1 or R3 completion.

## Verification record

Fresh local verification passed **464 checks** across all twenty goals, their hashes and prerequisite edges, aggregate views, documentation links, linked receipt hashes/results, and local Unity version, schema, continuous contracts, and scene/build GUIDs. The guidance-only mode passed **451 checks** and explicitly excludes Unity source/runtime certification. The complete G08 handoff assembled successfully. Six focused cases passed, including rejection of forward/cyclic dependencies, duplicate active selections, unsupported PASS claims, missing completion gates, and diverged aggregate specifications. Intentional relative-link rebasing between goals and reading views is accepted. Both tooling entry points passed Node syntax checks.

Native Editor access was exercised using the installed Computer Use skill. The screenshot showed Unity 6000.3.2f1 in YourQuest_PlaySafe, outside Play Mode, with no blocking modal and zero visible Console counts. These are Editor-access observations. No fresh C# compile, Play Mode test, gameplay PASS, build, performance measurement, or save round trip is claimed for this documentation/tooling patch.

## Remaining integration boundary

The local checkout contains production scenes and imported assets that are not yet tracked in the remote baseline. A guidance PR does not produce a reproducible game clone. Integrate the intended game-source/scene/asset changes in a separate explicit review, preserving GUID pairs and approved asset distribution; do not blanket-commit the dirty checkout. Unity build CI remains unconfigured until the complete candidate, Editor/license setup, and actual build/test entry points are available.

The current gameplay next step remains the selected G08 acceptance workflow. The project-setup audit does not repair its streaming failure or broaden it into later content work.
