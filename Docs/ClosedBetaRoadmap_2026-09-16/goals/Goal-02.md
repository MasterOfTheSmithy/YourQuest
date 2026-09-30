# /goal — Stabilize canonical state, identity, profile commits, and service ownership

## 1. GOAL
Establish versioned identity and recoverable state commits before dependent content expands.

## 2. BINDING EXECUTION CONTRACT
The short goal summarizes this full binding specification. Own foundation correctness; do not certify all future domain behavior.

## 3. PURPOSE
Prevent new world, character and adaptive content from multiplying incompatible identities or depending on mixed player/world snapshots.

## 4. PREREQUISITES
G01 authority map, fixture procedure and state/lifecycle evidence.

## 5. SCOPE
Canonical PlayerState/WorldState ownership; profile transaction/recovery boundary; schema migration; world identity; extensible stable entity IDs, event/commit envelopes; service profile lifecycle.

## 6. OUT OF SCOPE / DEFERRED
Macro geography G05; complete creator G10; detailed NPC/economy/social mechanics G11–14; observation semantics G15; large-frontier optimization G19. Declare extensible references, not speculative full records for every future feature.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High implements and integrates. Sol High reviews paired-file commits, migration strategy, rollback and deterministic identity. Independent migration fixtures may be delegated to Luna High.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Preserve schema-6 fixtures and accepted V1/V2 artifacts. Collection defaults are not a version migration. Unknown future versions fail safely. No second state store or permanent mutable PlayerProfile mirror. Introduce compatibility readers/adapters only with an explicit retirement condition.

## 9. REQUIRED WORK
1. Define stable IDs for player/profile, world, continent/region, settlement/site, route/water feature, NPC/faction, quest, item/content, species/people and origin. Separate species from cultural people; labels never determine identity. Add reference validation for duplicates, missing parents and illegal links.
2. Establish world seed, coordinate units/negative floor convention, cell size, generation/schema/hash versions and selected spatial artifact identity. Store absolute logical position separately from render origin. Preserve legacy ID aliases; never recompute accepted identity from mutable display text.
3. Version a profile revision/manifest committing player, world and registered auxiliary documents as one coherent snapshot. Stage writes, validate ownership/checksums, publish one commit pointer, retain prior complete revision; shared active documents are a working projection. Propagate write failure, reject mixed revisions, and never import another profile's backup.
4. Add ordered idempotent migrations from supported saved versions, explicit unsupported-version handling, backups and rollback. Include provenance and immutable accepted content references; add typed mutation/transaction receipt interfaces without serializing GameObjects.
5. Add an event envelope with event ID, actor/target IDs, logical location, context, outcome and session sequence; domain producers remain later owners. Add idempotent mutation commit keys and monotonic state revision for stale result checks.
6. Make startup/profile switching/shutdown own service teardown, event subscriptions, request epoch invalidation and static reset. Migrate actually reached PlayerProfile callers to PlayerState views; preserve serialized compatibility until references are eliminated.
7. Capture the existing authoritative player's collision envelope and supported speed contract for terrain/site tests, without retuning gameplay. Remove automatic currency grants from load/normalization; preserve explicit one-time origin grants by receipt.

8. Bounded audit follow-up (A08): distinguish a successful active-document mirror write from publishing a recoverable paired profile revision. Establish and document the dirty-state publication/freshness policy through YQProfileSaveSystem/YQProfileCommitStore, including registered auxiliary state and accepted partial content. Reproduce the crash window first; do not prescribe a second save store or force a full synchronous snapshot for every action. Preserve prior accepted payloads and propagate publication failure.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Contract tests cover negative coordinates, canonical ID stability, duplicate references, supported/unsupported versions and repeat migration. Fault-inject interruption between each snapshot write/publication; recover a complete previous or new revision, never a mix. In the real title flow create/switch two isolated profiles, save/quit/reload and verify identity, accepted origin/plan and no duplicate state/player authority. World defects may remain recorded if state checks can be proved independently.

For A08, fault-inject after an accepted mutation/mirror write and before paired publication, then at each publication step. Verify the documented recoverable revision/freshness behavior, auxiliary consistency, and no acknowledged durable grant disappearing silently. G05 owns semantic-authority overlay migration; use its receipt rather than duplicate that repair here.

## 11. DELIVERABLES
Schema/ID ownership map, migration and rollback fixtures, transaction API/result contract, lifecycle tests, compatibility retirement list.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
All later domains use stable references, the same commit protocol and state revision. Domain extensions own their own migrations. G05 receives world identity; G03 receives request ownership tokens; G09/G15 receive event envelope.

## 13. COMPLETION GATE
Foundation fixtures and real profile-switch/reload checks pass; write failures are observable and recoverable; supported old records retain identity and content. No requirement for future terrain, quests, alignment or long-session performance to pass.
