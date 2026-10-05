# /goal — Establish persistent residents, roles, and NPC knowledge

## 1. GOAL
Make settlements inhabited by stable residents whose status and knowledge survive physical unloading.

## 2. BINDING EXECUTION CONTRACT
The complete specification remains binding. Own NPC existence/status/knowledge and runtime presence, not full quests, trading or law.

## 3. PURPOSE
Resolve generated NPC plans, WorldState NPC records and loaded agents into one clear identity/status boundary before social content expands.

## 4. PREREQUISITES
G05 settlement/region/continent/faction context; G07 streaming/overlay; G08 physical homes/routes/anchors; G09 interaction and combat outcome contracts.

## 5. SCOPE
Resident identity, role/demographic assignment, status, faction membership, relationship references, bounded factual knowledge and lightweight loaded behavior.

## 6. OUT OF SCOPE / DEFERRED
Quest/dialogue semantics G12; stock/trade G13; standing/crime/relationship interpretation G14; generated personalized characterization G16. No always-on simulation of every unloaded citizen.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may verify navigation/role placement versus save/status separately. Sol High reviews generated-plan/status authority migration.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
One stable NPC ID across generated plan, mutable record, dialogue memory and GameObject. A plan defines origin/role/binding; mutable status lives in the canonical state. Scene agents are disposable views. Do not erase meaningful residents when their cell is evicted.

## 9. REQUIRED WORK
1. Adapt GeneratedNpcPlanRecord/NpcRecord/planning service/population builder with explicit immutable versus mutable fields and versioned links. Persist names/identity once accepted; prohibit reconstruction-time renaming.
2. Resolve home site, region, continent, species/people/faction, role and service anchors. Support coherent mixed populations; do not hardcode fantasy demographics to real-world categories.
3. Populate a beta settlement with civilian, service-provider, guard/local authority and quest-capable roles using approved assets and clearance. Use bounded wander/schedule/presence behavior with safe NavMesh or existing movement ownership.
4. Persist meaningful alive/dead/removed/relocated status, role changes and future service references. Unload/reload cannot duplicate or resurrect a changed resident. Far unloaded residents use durable records, not active AI.
5. Establish typed knowledge entries: source event, observer/report source, fact/proposition ID, locality, confidence/rumor marker, acquired time and retention policy. Separate world truth, private facts, witnessed facts and public knowledge.
6. Expose relationship and service query/command interfaces with neutral defaults. G14 owns score/law policy; G13 owns transactions. Register essential population readiness without holding terrain hostage to optional NPC prose.

7. Preserve every valid accepted identity when population coverage is incomplete (A07). Reconcile immutable GeneratedNpcPlanRecord fields with mutable NpcRecord status by stable ID; fill missing/rejected slots through a bounded validated transaction rather than clearing the accepted population. Persist accepted partial batches through the existing profile contract, with provenance/fallback identity intact; retry, crash and reload cannot rename, duplicate, resurrect or silently replace residents.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
In production visit the settlement, meet identified residents, cross/unload/revisit, save/quit/reload and compare IDs/status/home anchors. Change a resident through a real supported action and verify no duplication/resurrection. Verify two NPCs with different knowledge return different authorized context, and unloaded NPCs do not run expensive behavior.

Add incomplete-coverage and interrupted-batch fixtures containing already accepted and changed/dead residents. Verify only missing slots are generated and accepted IDs/status survive retry/reload. Test bounded knowledge selection with wrong-entity, expired/superseded and private facts; unloaded residents retain facts without continuous model inference. G12 tests conversational use of this knowledge.

## 11. DELIVERABLES
NPC authority migration, resident/role records, knowledge store/query rules, presence integration and runtime persistence receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G12 receives persistent people and authorized knowledge; G13 service identities; G14 witness/relationship primitives; G15 contextual events. Hooks are explicitly distinguished from implemented service mechanics.

## 13. COMPLETION GATE
One representative settlement has stable functioning resident presence, safe circulation and persistent status/knowledge. Trading, quest generation, rumor policy and final dialogue quality need not already pass.
