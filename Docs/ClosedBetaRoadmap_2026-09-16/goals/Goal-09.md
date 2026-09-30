# /goal — Stabilize the authoritative player and core RPG execution

## 1. GOAL
Make immediate movement, combat, interaction, inventory, equipment, and supported abilities reliably playable.

## 2. BINDING EXECUTION CONTRACT
This full specification is authoritative. Own deterministic execution and usable domain controls, not personalized content generation or final presentation polish.

## 3. PURPOSE
Give every later adaptive proposal a safe mechanical destination and every world test a trustworthy player.

## 4. PREREQUISITES
G02 state/transactions/event envelope; G04 actor/equipment/effect assets; G07 supported-speed safe traversal.

## 5. SCOPE
Existing YQInvestorPlayerMotor/Combat/Vitals/Enemy, PlayerState inventory/equipment, equipment visuals, animation execution, defeat/recovery and typed action outcomes; stable ability capability registry.

## 6. OUT OF SCOPE / DEFERRED
Creator G10; generated quest rewards G12; merchant economy G13; interpretation/unlock generation G15–16; final feedback and tuning G18. Preserve existing progression offers without claiming adaptive acceptance here.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; Luna High subagents may verify equipment and combat independently against shared contracts. Sol High for player authority/lifecycle bugs only.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Exactly one gameplay player, collider authority, inventory and equipment state. First/third person are presentations. Never execute a mechanic inferred from a skill/item name. Basic RPG verbs are available immediately; adaptation augments them.

## 9. REQUIRED WORK
1. Verify movement/look/jump/dash/crouch, slopes/steps, camera toggle and invalid-position recovery; left click attacks, E interacts, right click uses equipped secondary/spell per GDD. Preserve ordinary jump/dash until an accepted ability changes them.
2. Fix actual weapon/armor attachment and animation intent mappings, modal blocking and first/third-person consistency. No ghost player, visible capsule, left-click interaction or camera-dependent spell mechanics.
3. Stabilize hit/damage/health/death, aggro/disengagement, grounded movement modes, spawn clearance and safe player defeat/recovery. Persist meaningful death/loot state through existing overlay.
4. Make inventory add/remove/stack/equip/use/loot idempotent transactions; preserve slot IDs and supported visual bindings. Provide usable inspect/equip/ability UI now; final layout polish is G17.
5. Publish supported ability targeting/resource/cooldown/status/power/animation/VFX/SFX contracts. Clamp finite values and reject unsupported effects. Keep generated mechanics within approved templates; test resource/cooldown atomicity and repeated input.
6. Emit G02 events for attempted and successful actions with semantic target, damage/outcome, tool/ability ID and context; include trees/tools/failed attempts for later interpretation. Maintain old counter adapters until their consumers migrate.
7. Stabilize existing level/skill/offer application and persist accepted mechanics. Do not auto-award skills based on names or implement the full adaptive detector here.

8. Make the existing ability contract complete across proposal/offer → acceptance → equipped record → executor → save/reload (A02). Preserve supported targeting/resource/cost/cooldown/effect/power/animation/VFX/SFX fields; reject unsupported combinations rather than falling through to a damaging effect. Names/descriptions are presentation, never executable dispatch. Preserve legacy accepted content with explicit compatibility/migration behavior. G16 owns semantic generation and unlock choice; this goal owns the executor and field continuity.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Production-play tests cover camera modes, movement/contact, attack versus interaction, damage/death/recovery, inventory transfer, equip visuals, ability resource/cooldown and modal input. Save/quit/reload inventory/equipment/accepted ability and mutate a loot source without duplication. Record typed events from real actions. Exercise a 15-minute functional loop; final newcomer comprehension belongs to G17/G18.

Add self/buff versus projectile/damaging-pulse cases, unsupported target/effect combinations, repeated input and resource/cooldown atomicity. Compare the complete accepted payload and actual effect before and after equip and save/quit/reload, including supported old saves. Record stable target/tool IDs and typed attempted/successful/failed outcomes for ordinary attacks and tree/tool attempts, without granting adaptive content here.

## 11. DELIVERABLES
Capability/event registry, stable executor/inventory contracts, domain controls, animation/equipment mapping and runtime receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G10 uses actual avatar capabilities; G11–14 consume action/transaction contracts; G15 observes real evidence; G16 proposes only supported executable abilities/items.

## 13. COMPLETION GATE
Declared core loop passes in real play and reload with one player and no lost/duplicated inventory or unsupported execution. Future generated content variety, complete onboarding and 60-minute polished experience are not required.
