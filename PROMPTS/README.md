# YourQuest prompt library

These prompts are repository-specific task scaffolds for the production YourQuest architecture. They are not project truth and must be filled with current symbols, scene paths, acceptance evidence, and the user's request before use. Use `AI_CONTEXT/` and the dated receipts under `Docs/` to fill the packet.

For roadmap work, start with [the goal handoff](GOAL_HANDOFF.md) and `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json`. `node Tools/project-goal.mjs G08` assembles the complete selected specification and workflow without launching work or removing acceptance requirements. Model names in historical goals describe roles; see `MODEL_ROUTING.md`.

Every template uses the same contract: `WHEN TO USE`, `WHEN NOT TO USE`, `REQUIRED INPUT`, `OPTIONAL INPUT`, `AUTHORIZED SCOPE`, `FORBIDDEN CHANGES`, `REQUIRED INVESTIGATION`, `IMPLEMENTATION CONTRACT`, `UNITY VERIFICATION`, `TEST REQUIREMENTS`, `ACCEPTANCE CRITERIA`, `STOP CONDITIONS`, `ESCALATION CONDITIONS`, and `REQUIRED FINAL REPORT`.

Use `COMMON_TASK_TEMPLATES.md` for current runtime/LLM/editor/data/debugging work and `WORLD_GENERATION_TEMPLATES.md` for procedural-world requests. The world-generation templates begin with architecture review because the active V1/V2 planning, compiled-world materialization, and semantic streaming contracts are high-risk.
