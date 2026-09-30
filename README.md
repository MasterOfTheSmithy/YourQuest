# YourQuest

YourQuest is a Unity RPG built around accepted procedural content and deterministic runtime execution. The production checkout uses Unity **6000.3.2f1** and the Universal Render Pipeline. Closed-beta acceptance is still in progress.

## Start here

1. Read [project setup](Docs/PROJECT_SETUP.md) for the local Editor, verification, and source-control workflow.
2. Read [AGENTS.md](AGENTS.md) for canonical engineering rules and [the architecture index](ARCHITECTURE_INDEX.md) for system ownership.
3. Read [goal status](Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json) before selecting roadmap work. The active workflow is [G08 fix 4](<Docs/ClosedBetaRoadmap_2026-09-16/goals/8 fix 4.md>), with [Goal 08](Docs/ClosedBetaRoadmap_2026-09-16/goals/Goal-08.md) supplying its content requirements.
4. Use [the development workflow](AI_DEV_WORKFLOW.md) and [goal handoff template](PROMPTS/GOAL_HANDOFF.md) for a bounded implementation task.

Open the project folder in the recorded Unity version, then open `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`. Production startup creates the runtime camera and services in Play Mode. The title environment and authored assets remain part of the existing scene and binding setup.

## Check project guidance

With Node.js 18 or newer installed:

```text
node Tools/verify-project-guidance.mjs
node Tools/project-goal.mjs G08
```

The first command checks documentation, all twenty numbered goals, their dependency graph, synchronized reading views, and the status ledger. The second prints the complete active goal packet. Neither compiles Unity, runs the game, changes a save, or certifies beta readiness.

See [the September 30 guidance audit](Docs/PROJECT_GUIDANCE_AUDIT_2026-09-30.md) for the findings and verification limits. The maintained roadmap is under `Docs/ClosedBetaRoadmap_2026-09-16/`; attached archives and Obsidian planning notes are supporting snapshots.
