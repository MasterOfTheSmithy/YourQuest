---
name: yourquest-evidence
description: Assemble current YourQuest roadmap, requirements, and evidence for a Space, handoff, or bounded goal review. Use for project status and integration work; gameplay implementation remains governed by the selected goal and AGENTS.md.
---

# YourQuest requirements and evidence

Read the repository `AGENTS.md` and `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json`. The index supplies all 20 goal names, dependencies, workflow states and evidence pointers. Read the complete selected `goals/Goal-NN.md`; include the index's active workflow when reviewing the active goal. `node Tools/project-goal.mjs GNN` assembles those complete sources without launching work.

Use `Docs/YourQuest_Game_Design_Document.md` for the intended game outcome. The numbered goal files are editable acceptance specifications; ROADMAP section D and Goal-specifications.md are derived reading views. The current user request determines which work is authorized. A goal packet, audit follow-up, dependency or historical routing label does not authorize starting another goal or thread. Use the configured model; these files do not select the current chat model.

For a Space or status handoff, preserve these distinctions:

- Accepted saves own generated canon. The Space, status index, scene views and MCP results do not own game state.
- Workflow selection and acceptance are separate. Historical work is not a current PASS; queued specifications are not started work. Full acceptance needs the complete selected goal's evidence and rationale. G20 certifies the identified beta package.
- Keep the receipt's build/source hashes, time, seed/profile/artifact, scope, instrumentation and individual verdicts. Distinguish clean acceptance from instrumented diagnosis. Reuse unchanged valid evidence only after checking affected dependencies.
- Warn when current hashes differ, receipts postdate the index, or required rows are absent. Newer evidence does not silently replace indexed acceptance. A hash match establishes identity, not compilation or runtime behavior.
- Preserve the existing state, player, generation, materialization, streaming, LLM, quest and faction owners, V1/V2 selection, schemas, seeds and asset GUIDs. Source constants establish current contracts; historical architecture snapshots support discovery.

Use the three tools documented in `Tools/yourquest-readonly-mcp/README.md` when connected. They expose named requirements and curated evidence, not general filesystem or shell access. Without a connection, use bounded local reads. Do not inspect hidden chat stores, credentials, saves or raw diagnostic captures for Space population.

For integration changes, test the local protocol and access boundaries. For documentation changes, use `node Tools/verify-project-guidance.mjs --guidance-only`. Unity compilation or Play Mode is required only for authorized affected executable/gameplay work, using `UNITY_MCP_GUIDE.md`. Local tests do not establish installation, host connectivity or runtime acceptance. Ask for the specific connection/install authorization only after a concrete package is ready; preserve the parked thread-access issue as a separate limitation.
