# /goal — Stabilize structured quests, dialogue memory, and consequence commits

## 1. GOAL
Make generated quests and NPC conversations produce validated, persistent gameplay outcomes.

## 2. BINDING EXECUTION CONTRACT
The full specification is binding. Own objective/dialogue/quest consequence mechanics; future social and economy policy enter through declared interfaces.

## 3. PURPOSE
Preserve the existing objective evaluator while removing prose-driven rewards and unvalidated counter bindings.

## 4. PREREQUISITES
G03 proposal/scheduler boundary; G09 mechanics/events/inventory; G11 stable NPCs and knowledge. Transitive G02 commit protocol and G05 spatial lookup are available.

## 5. SCOPE
Quest lifecycle/objectives/rewards, bounded dialogue context/transcripts/memory, NPC-bound generated content, typed quest and dialogue commands and branching world-state consequences.

## 6. OUT OF SCOPE / DEFERRED
Merchant price/stock policy G13; law/standing/alignment policy G14; behavioral targeting G15/G16; final journal/navigation presentation G17. Current dialogue action=none must not be treated as an existing transaction system.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary with independent objective and memory test work. Sol High reviews cross-record consequence/reward commits and old completed-quest migration.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Prose presents; typed objective records, target IDs and event bindings execute. Persist approved rewards before quest acceptance; never calculate them from narrative words or current level on completion. Preserve already granted reward history.

## 9. REQUIRED WORK
1. Extend supported objective registry and allowlisted event bindings; reject arbitrary counter keys/prefixes, unknown targets, impossible routes and unsupported mechanics. Set objective baselines at acceptance so old lifetime counts do not auto-complete new work.
2. Implement available→accepted→active→resolved/failed/cancelled state transitions, multi-objective progress, explicit failure/recovery and exactly-once rewards/consequences. Reconcile origin quests with this authority.
3. Persist typed XP/currency/items and consequence receipts at proposal acceptance; migration preserves completed receipts and freezes unresolved legacy reward rules without retroactive double awards.
4. Implement beta quest patterns for talk, travel/discovery, retrieve/deliver and defeat/clear, plus one branching choice. Protect/investigate/negotiate variants require an existing supported executor; otherwise reject and list as deferred rather than ship impossible objectives.
5. Build dialogue from NPC-known facts, relevant relationships, location, quest and recent bounded memory. Preserve profile-scoped transcripts and summaries; validate any typed offer/accept/complete/inventory request before invoking domain APIs. Spoken claims alone cannot grant rewards or canon.
6. Generate names/text from committed people/places; absent entities require a separate accepted creation proposal. Persist promises, rescue/betrayal and outcomes as typed memory, not every line forever. Implement one branch with durable NPC/world flag differences; later G14 supplies standing/alignment consequences through the same seam.

7. Align every live quest producer, including director offers, with the same supported objective/reward acceptance contract (A04). A schema-valid response must still resolve current entities, reachable destinations, achievable prerequisites and acceptance-time counter baselines before being offered. Missing required objective data is a rejection, not permission to infer completion from prose.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Contract checks for target mismatch, prior-counter history, malformed objectives, missing/dead target, duplicate completion and reward retry. In production meet an NPC, receive/accept a generated task, complete a supported path, save/quit/reload/revisit and verify objective/reward/memory continuity. Execute both branches on separate profiles and verify differing NPC response without requiring future reputation policy.

Add a real director-produced quest through proposal → offer → accept → completion and reload, plus missing/dead/wrong target, impossible route/prerequisite and prior lifetime-counter cases. Retrieve an old promise after the recent transcript window has expired and verify its provenance and NPC knowledge scope (A13). For dialogue commands, rejection/staleness/duplicate delivery must not produce success narration or a grant (A16); speech follows the actual domain receipt.

## 11. DELIVERABLES
Objective/reward registries, migration fixtures, dialogue knowledge/command contract, persisted branching scenario and runtime receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G13 receives reward transactions; G14 extends social consequences; G16 can personalize valid quest templates; G17 displays objectives and authorized destinations.

## 13. COMPLETION GATE
Supported quest patterns and branching memory pass real play/reload without prose mechanics, impossible bindings or duplicate rewards. Future economic/social policy and every aspirational GDD objective are not required.
