# YourQuest model-routing guide

Route by responsibility and task risk using the current configured model. Verify availability before selecting any named model. This guide does not change the game's inference backend or the user's model settings.

- **Coordinator and senior review role:** architecture, ownership, V1/V2 authority, deterministic generation, streaming contracts, saves/migrations, cross-system diagnosis, requirements interpretation, and final integration.
- **Bounded engineering role:** implementation/debugging, focused exploration, behavior-preserving refactors, editor tooling, tests, and measured profiling within a named owner boundary.
- **Mechanical support role:** inventories, comparisons, formatting, and evidence cataloging. This role does not decide architecture or acceptance.
- **Autocomplete role:** proposed text/code completion reviewed by the engineer; it never certifies a change.

Numbered goals retain historical Luna/Astra/Sol routing language and already permit equivalent configured routing. Treat those names as role descriptions; record an actual substitution in the handoff when relevant. No task should stall merely because an old preset is absent. Use subagents only when authorized by the current session. The optional `opencode.json` model is an OpenCode preference, not a mandatory Codex dependency.

Escalate when a change moves canonical state ownership, changes seeds/RNG/boundaries/stage order, alters save/schema/public APIs, crosses player/world/LLM/streaming/persistence, modifies scene/prefab/GUID contracts, or fails three times at one root cause.
