# /goal — Finish discovery, onboarding, player UI, and settings

## 1. GOAL
Make the supported beta understandable and controllable without developer explanation.

## 2. BINDING EXECUTION CONTRACT
The full specification is authoritative. Own integrated usability; do not rebuild the domain systems or hide their failures behind UI.

## 3. PURPOSE
Unify the old exploration and UI goals around player knowledge and the now-working RPG/adaptive loop.

## 4. PREREQUISITES
G10 creator/origin; G16 adaptive loop and all transitive core/living-world dependencies. Domain goals already provide functional controls and truthful states.

## 5. SCOPE
Persistent discovery/navigation, journal/map/compass, HUD and generated text, integrated onboarding, modal behavior, settings and practical accessibility.

## 6. OUT OF SCOPE / DEFERRED
World topology G05–08; domain transaction/quest defects return to G09–16; final audio/art/pacing G18; new unsupported social/profession screens deferred. No infinite prerendered world map.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High primary; independent accessibility/text/layout and newcomer-flow checks may use Luna High subagents. Sol High only if a discovered authority migration is unavoidable, under its owning goal.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Map knowledge is a player projection of semantic world truth. Known directions may reference unloaded features; undiscovered secrets are not revealed just because the generator knows them. UI edits never become gameplay authority.

## 9. REQUIRED WORK
1. Persist discovered settlement/site/region/continent IDs and source of learned directions. Build bounded map/frontier views and clear compass/journal guidance from existing queries, stable paths and authorized quest knowledge.
2. Integrate health/resources/status, interaction, inventory/equipment, skills/spells, offers/titles/classes, quests, dialogue transcript, known standing/awareness, retained alignment axes, navigation, pause/profile/save/load and defeat controls. Wrap/scale long generated names and show meaningful errors.
3. Teach progressively from supported creator/origin and Vey's hut: movement/look, E interaction, dialogue/objectives, combat/ability, loot/equip, shrine/recovery, lockpicking/mimic where retained, adaptive offer, first road. Narrative/rewards come from accepted origin/content.
4. Make cursor, pause, modal nesting, back/close and key ownership consistent. Prevent attacking/interacting through menus and stuck input after origin/dialogue/loading.
5. Persist master/music/effects volume, sensitivity, supported display/resolution/quality options; support keybind display and remapping where input stack permits. Provide readable text/UI scaling, subtitles/dialogue history and reduced camera-motion/flashing options. If a control cannot work, hide it and record the limit.
6. Present bounded recoverable loading/model/save failures in player language with actionable retry/back controls while retaining detailed local diagnostics.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
An unfamiliar tester or documented independent manual walkthrough completes the opening and both adaptive choice UI paths without developer explanation. Test long generated text, low/high supported resolutions, keyboard/mouse modal transitions, setting persistence and discovered/undiscovered/unloaded quest destinations. Save/quit/reload discovery and re-follow a route.

## 11. DELIVERABLES
Integrated UI/onboarding/navigation, settings/accessibility coverage list, discovery records and usability issue/acceptance receipts.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
G18 receives a complete player-facing experience to tune; G19/G20 receive exact normal UI-driven flows rather than debug-only paths.

## 13. COMPLETION GATE
A fresh tester can start, learn, fight, interact, trade, follow quests, understand offers, navigate, save/load and change supported settings without developer help. Root mechanics remain owned upstream; unresolved normal-play blockers prevent this gate.
