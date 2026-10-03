# YourQuest project setup

This guide connects the Unity checkout, repository documentation, goal execution, and review workflow. It preserves the current game architecture and existing local work. It does not certify closed-beta readiness.

## Unity workspace

Open the root containing `Assets`, `Packages`, and `ProjectSettings` in Unity **6000.3.2f1**, as recorded in `ProjectSettings/ProjectVersion.txt`. Use the existing package manifest and URP assets; do not upgrade or reinstall packages as a setup step.

The production scene is `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`. The enabled build scenes currently also include `YourQuest_TitleEnvironment.unity`; preserve their order, GUIDs, and serialized references. `YourQuestTutorialAutoBootstrap` owns startup and the authoritative gameplay handoff. A Game view without a camera outside Play Mode does not establish a runtime camera failure.

Read [Unity access and verification](../UNITY_MCP_GUIDE.md) before driving the Editor. Discover actual tools each session. Only one operator may drive Unity. Exit Play Mode before executable source edits or recompilation; preserve unsaved scenes and ordinary profiles. Use the existing menu-driven checks in [the testing map](../TESTING_MAP.md). Compile executable changes once after a coherent patch, then run only the affected checks. A documentation change needs no Unity build.

Source inspected on 2026-09-30 declares player/world schema **7** in `YQStateContract` inside `Assets/Assets/Scripts/Data/State/YQStateFoundation.cs`. Continuous contracts remain `continuous_world_cell_v5` and `continuous_edge_v3`. These observations are separate from September 15–16 schema-6 receipts and must be rechecked before a sensitive change.

## Guidance and requirements

| Purpose | Canonical location |
|---|---|
| Product requirements | [Game Design Document](YourQuest_Game_Design_Document.md), subject to the current user request |
| Engineering rules | [Root AGENTS.md](../AGENTS.md) and applicable scoped files |
| Ownership and discovery | [Architecture index](../ARCHITECTURE_INDEX.md), then the relevant [context sheet](../AI_CONTEXT/README.md) |
| Full goal specifications | [Maintained roadmap goals](ClosedBetaRoadmap_2026-09-16/README.md) |
| Goal selection and evidence pointers | [GOAL_STATUS.json](ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json) |
| Change and verification procedure | [Development workflow](../AI_DEV_WORKFLOW.md), [change control](../AI_CHANGE_CONTROL.md), [testing map](../TESTING_MAP.md) |
| Reusable task packets | [Prompt library](../PROMPTS/README.md) |

The individual numbered goals are editable specifications. `Goal-specifications.md` and section D of `ROADMAP.md` are synchronized reading views. Update those views together with a goal change; the validator rejects divergence. The original attached ZIP is a historical import, not an overwrite source. Desktop copies and the Obsidian vault do not form a competing current goal authority.

## Goal execution

Run `node Tools/verify-project-guidance.mjs`, then `node Tools/project-goal.mjs G08` for the current active packet. Read the full specification, selected workflow, prerequisite receipts, and applicable engineering instructions. The packet includes file hashes so a handoff can identify which specification it used.

G08 fix 4 runs R1 publication/recovery, then R2 streaming/performance, then R3 physical traversal. The latest linked R2 receipt reports FAIL; it does not prove that every R1 row was rerun. Finish the existing gate before advancing to G09. Do not restart completed earlier scopes, remove original numerical gates, or adopt future audit follow-ups as incidental G08 work.

The ledger separates workflow position from acceptance. `historical` means prior work/evidence exists and should be preserved; it does not mean current PASS or require a blanket restart. `queued` means a goal is not selected for this session. Record one primary coordinator when work starts, the exact source/commit identity, changed contracts, receipt paths, evidence level, and the next failing row. Close a goal only when its complete acceptance contract is supported. Goal 20 alone certifies the beta.

## Codex and local tools

The registered local Codex project `Your Quest 2026` points to `C:\Users\Garri\YourQuest`. Start engineering chats from that root so `AGENTS.md`, source control, and the primary working directory resolve correctly. Project registration does not synchronize uncommitted files to GitHub.

Use the current configured model. [Model routing](../MODEL_ROUTING.md) describes responsibilities rather than mandatory presets. The optional OpenCode configuration and `.opencode/agents/` are adapters for that tool; Codex does not discover them as Codex skills. Game inference configuration is a separate runtime contract and is not changed by engineering-model selection.

Keep temporary receipts and generated handoff packets under `outputs/`. Preserve dated production evidence under `Docs/` when it is intended for review. Do not manually edit Unity caches or output under `Library`, `Temp`, `Logs`, or `obj`.

## GitHub review

The configured remote is [MasterOfTheSmithy/YourQuest](https://github.com/MasterOfTheSmithy/YourQuest). Inspect status and the exact patch before any commit. Use a `codex/` branch and stage an explicit file list; never include unrelated user work through a blanket add. A dirty production checkout is not permission to publish every imported asset, generated file, or save.

The project-guidance pull request intentionally publishes only curated documentation and repository tooling. It does not make the remote repository a complete copy of this dirty Unity checkout. Production scenes and imported assets still require a separate, explicit integration pass that preserves `.meta` pairs and asset distribution requirements.

The guidance workflow validates roadmap structure and handoff data with read-only repository permissions. It does not pretend to be Unity CI. Unity build/test automation needs the recorded Editor, complete approved assets, an actual test/build entry point, and the project's license setup before it can be enabled honestly.

Use the pull-request and issue templates to record a concrete behavior, goal/owner, evidence, and acceptance. Merging a documentation pull request does not close the corresponding gameplay goal.
