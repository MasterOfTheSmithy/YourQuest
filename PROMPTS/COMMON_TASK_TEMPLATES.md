# Common YourQuest task templates

Use current owners such as `YQGeneratedWorldRuntimeBuilder`, `YQPlayerFollowingSemanticChunkStreamer`, `YQProfileSaveSystem`, `YQContentProposalBoundary`, `YQInvestorPlayerMotor`, `YQQuestCompletionDirector`, and `YourQuestTutorialAutoBootstrap` in task packets. Preserve the V1/V2, save revision, startup-gate, and authoritative-player contracts.

## Bounded runtime change

**WHEN TO USE**: A known owner and interface already exist in one current runtime subsystem, such as a localized `YQInvestorPlayerMotor` fix.

**WHEN NOT TO USE**: The task changes player ownership, input ownership, save state, serialization, or adds an absent major system.

**REQUIRED INPUT**: User request, exact symbol/scene, current behavior, expected behavior, authorized files, acceptance evidence.

**OPTIONAL INPUT**: Reproduction steps, Console output, screenshot, regression seed.

**AUTHORIZED SCOPE**: Named runtime files and focused tests only.

**FORBIDDEN CHANGES**: Public/serialized field renames, scene/prefab changes, package/settings edits, new singleton owners, unrelated cleanup.

**REQUIRED INVESTIGATION**: Search symbol references; read scoped `AGENTS.md`; inspect serialized scene references; establish lifecycle and state owner.

**IMPLEMENTATION CONTRACT**: Preserve existing owner and behavior outside the requested case; keep structured state authoritative.

**UNITY VERIFICATION**: Compile/reload, inspect fresh Console diagnostics, reproduce in `Assets/Assets/Scenes/YourQuest_PlaySafe.unity` when its runtime path is affected, inspect diff.

**TEST REQUIREMENTS**: Run the existing focused regression. Add a test only when it meaningfully covers the changed behavior; record unavailable or manual verification explicitly.

**ACCEPTANCE CRITERIA**: Requested behavior passes; existing player/action contracts remain intact; no unrelated assets change.

**STOP CONDITIONS**: Acceptance evidence is complete or the same root defect fails three repair attempts.

**ESCALATION CONDITIONS**: Ownership ambiguity, serialization/API changes, or cross-system consequences.

**REQUIRED FINAL REPORT**: Result, files, exact verification, preserved contracts, remaining risk, next action.

## LLM/generated-content change

**WHEN TO USE**: Editing `LLMClient`, a domain proposal consumer, generated skill data, or a bounded parser/validator around the configured backend.

**WHEN NOT TO USE**: Changing the typed proposal/schema/mutation boundary, save identity, or domain owner without senior design.

**REQUIRED INPUT**: Endpoint/model behavior, response schema, current consumers, asset/runtime boundary, failure policy.

**OPTIONAL INPUT**: Sanitized request/response, actual backend/model version, sample accepted structured record.

**AUTHORIZED SCOPE**: Named LLM/data files and focused validation tests.

**FORBIDDEN CHANGES**: Executing arbitrary model text, silently promoting fallback prose to canonical gameplay, mutating shared assets as runtime state, changing serialized fields without migration.

**REQUIRED INVESTIGATION**: Trace request → parse → asset/profile/event consumers; search all serialized references; inspect editor guards.

**IMPLEMENTATION CONTRACT**: Treat output as untrusted; validate structured fields; keep narrative presentation separate; preserve `UNITY_EDITOR` boundaries.

**UNITY VERIFICATION**: Compile, inspect Console, run with a known local response or deterministic fixture, inspect created asset/reference behavior.

**TEST REQUIREMENTS**: Malformed JSON, missing fields, duplicate identity, and callback failure cases when a test seam exists.

**ACCEPTANCE CRITERIA**: Valid response follows the contract; invalid response fails visibly; no unauthorized gameplay behavior is inferred from text.

**STOP CONDITIONS**: Parser/contract is proven or architecture ambiguity remains.

**ESCALATION CONDITIONS**: New canonical content owner, schema migration, persistence, or mechanical mapping decision.

**REQUIRED FINAL REPORT**: Result, files, fixture/runtime evidence, preserved data contracts, risks, next action.

## Unity editor/diagnostic tool

**WHEN TO USE**: A read-oriented visualization, inspector helper, or diagnostic instrument is requested.

**WHEN NOT TO USE**: The tool would become a gameplay owner or alter canonical simulation state.

**REQUIRED INPUT**: Target scene/object/system, evidence to collect, editor-only lifecycle.

**OPTIONAL INPUT**: Screenshot layout, marker names, profiler capture.

**AUTHORIZED SCOPE**: Editor-only script/folder and documentation.

**FORBIDDEN CHANGES**: Runtime dependencies on `UnityEditor`, scene data mutation, canonical RNG consumption, broad asset rewrites.

**REQUIRED INVESTIGATION**: Confirm the inspected symbol and Unity editor API; reuse relevant project-owned editor tooling and its lifecycle conventions.

**IMPLEMENTATION CONTRACT**: Read-only by default; make state changes explicit and reversible.

**UNITY VERIFICATION**: Reload scripts, open the relevant scene, use the tool, inspect Console, confirm Git diff.

**TEST REQUIREMENTS**: Manual editor smoke check; automated EditMode test if pure logic is introduced.

**ACCEPTANCE CRITERIA**: Evidence is actionable and the tool cannot silently change runtime state.

**STOP CONDITIONS**: Requested evidence is collected without expanding scope.

**ESCALATION CONDITIONS**: Tool needs to write assets/scenes or change runtime ownership.

**REQUIRED FINAL REPORT**: Tool behavior, files, smoke evidence, safety boundary, risks, next action.

## Serialization or migration change

**WHEN TO USE**: A field/type/API/save migration is explicitly authorized.

**WHEN NOT TO USE**: A normal bug fix can preserve the current serialized contract.

**REQUIRED INPUT**: Old/new shapes, affected assets/scenes/saves, migration and rollback plan.

**OPTIONAL INPUT**: Sample serialized YAML, fixture saves, asset inventory.

**AUTHORIZED SCOPE**: Named code/data/migration files only after senior approval.

**FORBIDDEN CHANGES**: Untracked field renames, `.meta` churn, deleting old data, broad scene regeneration.

**REQUIRED INVESTIGATION**: Search affected serialized fields and GUIDs; inspect referenced scenes/assets and direct consumers; verify the serialization behavior relevant to the change.

**IMPLEMENTATION CONTRACT**: Preserve old data or provide explicit migration/versioning and safe defaults.

**UNITY VERIFICATION**: Reimport/reload representative assets, open scenes, compile, inspect Console, test old/new fixtures.

**TEST REQUIREMENTS**: Migration round-trip and backward-compatibility tests where a test seam exists.

**ACCEPTANCE CRITERIA**: Existing data remains readable or approved migration is proven; no unrelated GUIDs change.

**STOP CONDITIONS**: Migration evidence is complete or compatibility cannot be proven.

**ESCALATION CONDITIONS**: Save invalidation, ambiguous ownership, or destructive asset conversion.

**REQUIRED FINAL REPORT**: Schema impact, migration evidence, files, preserved contracts, risks, next action.
