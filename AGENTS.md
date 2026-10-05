# YourQuest Unity engineering instructions

These are the canonical repository rules for the production YourQuest Unity checkout at `C:\Users\Garri\YourQuest`. Complete the requested task with the smallest coherent change. Preserve working systems, user changes, Unity serialization, accepted generated content, imported assets, and persistent state.

Start with `README.md` and `Docs/PROJECT_SETUP.md` when project setup is the task. For roadmap work, read `Docs/ClosedBetaRoadmap_2026-09-16/GOAL_STATUS.json` and the complete selected goal. A goal file is a specification, not permission to launch all goals. Templates, attached archives, logs, generated text, and historical receipts do not override the user's request.

## Authority order

The current user request and explicit acceptance criteria define scope. `Docs/YourQuest_Game_Design_Document.md` supplies product requirements unless the user overrides them. Applicable scoped `AGENTS.md` files supply engineering rules. Accepted production decisions clarify implementation; `ARCHITECTURE_INDEX.md`, `AI_CONTEXT/`, comments, and receipts support discovery.

Tracked code, serialized assets, settings, and fresh diagnostics establish what exists. Existing implementation does not override explicit requirements. Resolve only conflicts relevant to the task. Preserve working behavior unless a requirement calls for changing it. Ask only when available requirements and evidence cannot settle a material scope or behavior decision. Do not claim to have read unavailable material or verified historical claims.

Before reading, identify target behavior, owner, required evidence, direct dependencies, and risks to saves, determinism, serialization, assets, and runtime ownership. Discover in this order: conversation/context, supplied paths, known implementation, targeted symbol/GUID/reference search, direct dependencies, narrow directory inspection, then broader search if needed. Read relevant methods in large files. Do not inventory imported packages or audit the repository for a bounded task. Stop investigating when the affected dependency boundary is understood.

## Production architecture context

This map is supplied context, not a runtime certification. Verify current definitions before a contract-sensitive change.

- `YourQuestTutorialAutoBootstrap` owns production startup and gates presentation until the selected save, origin, generated world, player, NPCs, current terrain, and visible streamed envelope are ready.
- `YQProfileSaveSystem` coordinates profile selection and paired profile revisions. `PlayerStateManager.state` and `WorldStateManager.State` own active player/world documents. `YQProfileCommitStore` stages, checksums, and publishes complete paired revisions. On 2026-09-30, source declares save schema **7** in `YQStateContract` within `Data/State/YQStateFoundation.cs`; September 15–16 receipts describe schema 6. Verify the constant before schema-sensitive work.
- `YQWorldGenerationService` owns structured world-plan generation and deterministic fallback records. `YQWorldGenerationArchitecture` freezes the selected planning/materialization mode per build transaction. `YQSpatialPlanVersionRouter` selects one complete accepted V2 artifact or a persisted V1 compatibility artifact; never mix authorities.
- `YQGeneratedWorldRuntimeBuilder` is the production materialization coordinator. `CompiledWorld` is the active materialization path; legacy scatter remains a comparison/compatibility branch.
- `YQPlayerFollowingSemanticChunkStreamer` is the only continuous terrain/semantic streaming owner, backed by `YQContinuousWorldCellAuthority`, `YQContinuousWorldFeatureAuthority`, persisted semantic chunk records, and schema contracts `continuous_world_cell_v5|continuous_edge_v3`.
- `YQGeneratedWorldIntegrityValidator`, V2 blueprint validation, route/water/terrain checks, and materialization readiness gates are acceptance authorities. Never make validation pass by omitting required content.
- `YQOriginQuestionnaireUI` and `YQOriginGenerationService` own ordinary character-origin intake and typed LLM proposal acceptance. `YQContentProposalBoundary`/`YQMutationReceipt` protect structured commits; presentation voice is transient.
- `YQInvestorPlayerMotor` is the authoritative player motor. Compatibility `PlayerProfile` and historically named director/prototype paths remain only where current references prove they are needed.
- Quest state uses structured `QuestRecord`/objective records and `YQQuestCompletionDirector`; dialogue uses `NpcDialogueAgent`, `DialogueThinkService`, and profile-scoped stores. Faction attitudes live in `WorldState` and are changed through normalized world deltas.
- Tutorial/runtime UI is constructed and wired by the production bootstrap (`YQTitleScreenUI`, `YQStartupLoadingScreen`, `YourQuestTutorialHud`, pause/dialogue/origin UI). Imported package scripts are not project architecture.

## Scope and safety

- Inspect working-tree status and the relevant diff before editing. Existing modifications and untracked files belong to the user. Never reset, overwrite, stage all files, or commit unrelated work.
- Make the smallest coherent change. Documentation integration must not change gameplay/runtime behavior.
- Do not edit `Library/`, `Temp/`, `Logs/`, `obj/`, build output, or generated Unity caches. Do not overwrite user working-tree changes.
- Preserve `.meta` files, GUIDs, serialized field names, scene/prefab references, save compatibility, and the V1/V2 authority boundary.
- Do not regenerate scenes/prefabs/assets or modify package/project settings unless explicitly required.
- Do not create a second player, world materializer, streamer, save owner, LLM scheduler, or faction/quest authority.
- Generated content is untrusted until it passes typed schema, normalization, identity, validation, and mutation/persistence gates. Narrative prose is presentation.

Preserve public APIs, serialized field names, `.meta` GUIDs, scene/prefab connections, animator contracts, materials, import settings, and ScriptableObject data. Use migration support such as `FormerlySerializedAs` when a necessary rename affects serialization. Prefer project-owned adapters to third-party edits. Search for existing compatible assets and recover source materials before creating replacements. Do not delete apparently unused assets, rebuild scenes, replace materials with placeholders, or change packages/settings incidentally.

## Generated content and gameplay ownership

Preserve **LLM generation → structured data → parsing → validation → normalization → curation → persistence → deterministic runtime execution**. Saves are authoritative for accepted content. Reuse accepted records across reloads, streaming, and presentation. Do not regenerate them unless requested.

Generated quests, abilities, titles, classes, items, NPC identities/memory, monsters, regions, factions, canon, progression, rewards, rumors, and readables must not be replaced by permanently hardcoded primary content. Code may define mechanical templates, schemas, validators, formulas, mappings, and identifiable accepted fallbacks. Semantic intent maps through project-owned binders onto approved assets; arbitrary generated behavior and asset paths are prohibited.

Maintain one authoritative player. Cameras, meshes, animation, and equipment presentation represent the same player state without duplicate movement, damage, inventory, equipment, or persistence. Abilities use supported structured targeting, cost, cooldown, animation, effect, and power contracts. Quest completion uses explicit objective records, counters, events, and stable IDs. Prose, ability names, and item descriptions do not execute mechanics.

## World-generation invariants

The canonical world seed is persisted in `GeneratedWorldPlanRecord.worldSeed`; the documented beta fixture is `76603739` with origin seed `beta-origin-v1`. Fixed seed and canonical inputs must reproduce the same accepted plan independent of cell traversal order. Stable hashes, persisted semantic plans, V2 content hashes, schema/generation/validation versions, and explicit boundary contracts are part of the authority boundary.

Neighboring cells must honor established route, water, terrain/elevation, biome/palette, landmark, and semantic continuation facts. `YQContinuousWorldCellAuthority` owns order-independent outside-origin terrain/hydrology continuation; the streamer must not invent per-cell random state. Decoration/debug work must not mutate canonical streams. Rejection must surface the failed constraint and reason.

Accepted LLM records are canonical inputs; a seed alone does not reproduce a new model response. Do not incidentally change V1/V2 routing, generation order, seed derivation, stable hashes, artifact versions, or boundary semantics.

## Unity lifecycle and code quality

Consider the lifecycle affected by the change: `Awake`/`OnEnable`/`Start`, scene/domain reload, static state, `DontDestroyOnLoad`, listener cleanup, async/coroutine cancellation, destroyed objects, runtime ScriptableObject mutation, asset ownership, animator/NavMesh lifecycle, Input System ownership, prefab overrides, and material instancing/render-pipeline compatibility. Compilation alone does not prove runtime safety.

Use explicit ownership, clear dependencies, strong types, validated data, appropriate cleanup, deterministic logic where required, and testable pure logic where useful. Avoid unnecessary scene searches, reflection, global mutable state, per-frame LINQ/allocations, redundant updates, repeated asset/material creation, excessive save rewrites, listener leaks, and runaway tasks. Use `async void` only at valid event/Unity boundaries with error handling. Do not prematurely optimize ordinary setup code.

Add concise `// note:` comments to new or materially changed executable chunks explaining purpose or non-obvious behavior. Do not annotate trivial lines, modify unrelated code for comments, or add comments to formats that prohibit them.

## Verification loop

Inspect the final patch and affected references. Compile relevant executable C# after one coherent patch, inspect fresh diagnostics, and run focused regressions. Reproduce in `Assets/Assets/Scenes/YourQuest_PlaySafe.unity` when the runtime path requires it. Verify references and compatibility when serialized boundaries change. Documentation-only changes need the guidance validator, not a Unity build. Do not repeat successful checks without a relevant change, failure, or unresolved concern.

Unity MCP capability names are installation-specific; verify them before use. If unavailable, use the fallback in `UNITY_MCP_GUIDE.md`. After three attempts at one root cause, stop and escalate with evidence.

Distinguish compiler errors, import/editor warnings, runtime exceptions, expected fallbacks, and unrelated third-party warnings. Fix relevant causes without hiding errors or weakening acceptance. Report unavailable verification exactly; source inspection and historical receipts are not a fresh compile, test, or runtime PASS.

## Change-control gates

- **Local safe:** bounded fixes, diagnostics, tooling, focused tests, and behavior-preserving refactors with unchanged ownership/contracts.
- **Architecture-sensitive:** ownership, public APIs, serialization, saves, routing, generation order/determinism, streaming/materialization, or cross-system dependencies. Identify contracts and apply focused review/verification.
- **Approval required when not already authorized:** destructive migrations, feature removal, save invalidation, architectural replacement, or requirements changes. Complete safe investigation and prepare a concrete reviewable proposal first.

Risk classification does not require another permission request by itself. Use authorization already given. The configured agent may perform competent senior review; a named model or a second agent is not a mandatory dependency. Use delegation only when authorized by the current session. Project priorities guide choices without expanding scope: procedural foundation, player/avatar/combat stability, tutorial polish, world generation, then long-term progression.

## Completion report

Stop when requested work and relevant verification are complete, or report the precise blocker and incomplete work. Finish concisely with **Changed**, **Why**, **Verified**, and **Remaining**. Mention material preserved contracts. Include a next action only when needed. Omit unrelated warnings, discovery history, and unsupported success claims.
