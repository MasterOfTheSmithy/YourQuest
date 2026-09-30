# /goal — Make trade, rewards, and settlement services transactional

## 1. GOAL
Provide useful merchants and services with stable value, persistent stock, and duplication-safe transactions.

## 2. BINDING EXECUTION CONTRACT
This full specification is authoritative. Own economic execution/value policy; do not expand into a simulated global economy.

## 3. PURPOSE
Give rewards and settlements practical value using the existing inventory/currency authority.

## 4. PREREQUISITES
G09 item/inventory transactions; G11 merchant/service identity; G12 typed rewards and consequence receipts.

## 5. SCOPE
Player currency, item valuation, merchant stock, buy/sell, healing/rest/supplies where supported, reward value consistency and neutral standing modifier interface.

## 6. OUT OF SCOPE / DEFERRED
Standing policy G14; personalized trade opportunities G16; long-session inflation tuning G18/G19. Large crafting, housing, production chains and global scarcity simulation are deferred. Retained gathering/crafting templates use the same inventory contract only when required by G16's selected behavior path.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; independent arithmetic/transaction fixtures may use Luna High subagents. Sol High reviews save/commit consistency and exploit-prone cross-record changes.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
One currency/inventory authority. Prices/rewards use typed finite bounded values; dialogue or display text never spends money. No normalization-time handouts, negative/overflow balances or hidden stock rerolls.

## 9. REQUIRED WORK
1. Reuse PlayerState currency and inventory; formalize checked value arithmetic, quantity rules, ownership and transaction idempotency.
2. Persist merchant stock IDs, item identity, available currency if used, restock policy and version/time anchor. Loading/unloading must not reset stock; wall-clock changes must not duplicate restocks.
3. Implement atomic buy/sell/service transactions with validated quoted price, quantity, current revision, funds/capacity and final receipt. Cancel or retry safely after stale quotes, duplicate clicks or save failure.
4. Use base value plus bounded context/standing modifiers; expose a neutral query provider until G14 supplies actual standing. A future modifier is not a prerequisite for trade acceptance.
5. Integrate supported healing/rest/supplies and quest rewards through the same value/receipt contracts. Make useful basic UI and clear failure messages; final UI composition is G17.
6. Record economic events with actor, merchant, item and outcome for quest and behavioral consumers. Preserve generated identity and asset binding through purchases/resales.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Test zero/negative/overflow quantities, insufficient funds, duplicate submissions, stale quotes and failed commits. In production earn a quest reward, buy, sell, use a service, unload/reload and quit/restart; reconcile balances/stock/item IDs and verify no duplication. Model outage must not stop an already accepted merchant transaction.

## 11. DELIVERABLES
Economic authority/value policy, merchant/service records, transaction UI/API, restock contract and exploit/reload receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G14 can supply bounded standing modifiers; G16 can create validated contextual rewards/opportunities; G18 tunes value pacing without changing ownership.

## 13. COMPLETION GATE
A player earns/spends value and uses retained services in real play; repeated/stale/failed operations do not duplicate or lose state; stock and generated identity persist. No future social simulation or full economy balance is required.
