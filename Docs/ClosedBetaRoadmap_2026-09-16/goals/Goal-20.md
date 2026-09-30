# /goal — Certify, package, and hand off the closed beta

## 1. GOAL
Certify the complete production experience and deliver a reproducible closed-beta build with tester guidance.

## 2. BINDING EXECUTION CONTRACT
This entire specification and H's acceptance matrix are binding. This is the sole closed-beta certification authority; earlier diagnostics or subsystem receipts cannot replace it.

## 3. PURPOSE
Prove the game delivers its defining promise through the ordinary player flow on a clean installation.

## 4. PREREQUISITES
G19 reliability candidate and all G01–G18 acceptance receipts, supported-scope/deferred-feature register, locked hardware/budgets and four-seed suite. No unresolved critical blocker.

## 5. SCOPE
Final normal-flow regression, evidence freshness, compatible-save replay, packaging/build identity, clean-install check, local diagnostics and tester handoff.

## 6. OUT OF SCOPE / DEFERRED
New features, changing beta scope to hide a failed gate, external distribution or uploading tester data without explicit authorization. A domain failure returns to its owner and invalidates affected evidence; do not rebuild that subsystem opportunistically inside certification.

## 7. AGENT ROUTING
Luna High owns planning, coding, integration, verification and completion. Luna High subagents may handle bounded independent engineering; the primary reviews all work and no subagent changes scope. Astra Light is limited to narrow search/reference/comparison/triage work, normally 5–10% at most. Prefer Luna Max or Extra High for unusually difficult bounded work; reserve Sol High for the explicitly named high-risk architecture, save, migration, determinism or lifecycle decisions. Use available equivalent configured routing if a named preset is unavailable, and record the substitution.
Luna High owns final decision after reviewing every receipt. Luna High subagents may independently execute seed/checklist subsets; Sol High audits only high-risk unresolved save/determinism evidence. No subagent may declare overall readiness.

## 8. ENGINEERING RULES
Use the actual working tree and GDD. Preserve working production code, GUIDs, serialized fields, scenes/prefabs, accepted saves and imported assets. Investigate target symbols and direct dependencies; do not repository-audit ordinary execution. Make the smallest coherent change and add basic `// note:` comments explaining new or changed executable chunks. Use one authority per responsibility; version/migrate persistent schemas and deterministic generation changes. Keep the project playable/recoverable or isolate a migration with rollback. Do not silently broaden scope.
Use an ordinary clean profile and the packaged production build, without developer grants/teleports/fallback overrides as substitutes for acceptance. Developer fault/stress evidence supplements the player flow. Record exact executable/source/schema/generation/manifest identities.

## 9. REQUIRED WORK
1. Execute H: clean install→creator/species/appearance→guided or authored origin→hut/world→travel→NPC/quest→combat/loot/equipment→progression/adaptive offer→trade/social consequence→continued streaming→save/quit/reload→revisit changed content→fresh frontier.
2. Verify both origin routes and confirmation/cancellation on ordinary profiles; verify two natural behavior paths including real successful model generation, supported mechanics and persistent response. Demonstrate continued movement and accepted content with the model unavailable.
3. Run primary plus three regression seeds, supported legacy migrations and representative extended play. Reuse current G19 soak traces only if the candidate and relevant settings are unchanged; rerun affected suites after fixes.
4. Reject any known routine crash, corruption, normal-play softlock, inaccessible required objective, missing collision, identity/appearance loss, duplicate rewards/residents, broken adaptive execution or severe recurring stall.
5. Build/package the exact candidate with beta/version/date and accessible schema/generation diagnostics. Remove ordinary access to developer-only fixtures; retain safe local bug-report collection.
6. Provide install/model setup/recovery instructions, controls, origin choices, adaptive premise, supported systems, limitations, save compatibility and local bug-report instructions. Diagnostic bundle includes non-sensitive profile token, seed/location/build and recent errors, with raw dialogue/custom text excluded by default; uploads require consent.

## 10. VERIFICATION
Compile once after a coherent code edit batch, then verify the owned runtime behavior; compilation alone is not runtime proof. Before editing/recompiling exit Play Mode and wait for imports. Prepare only the named disposable profile, enter the actual enabled PlaySafe/title startup flow, create/select the correct save, run the designated checks, capture results, then exit before further edits. Current fixture-reset menus require a separate Play Mode preparation session; do not confuse its preaccepted origin with an ordinary new profile. Never invent a test scene or count skipped runtime checks as PASS.
Independently launch the packaged build from a clean directory/profile, complete the normal path, restart and compare consequences. Every H row needs expected/actual result and evidence. NOT YET TESTABLE, stale receipts and missing hardware/model access cannot count as PASS.

Audit integration acceptance: require the applicable owning-goal receipts, selected-model normal-generation proof, bounded per-role context/growing-state measurements and model-off accepted-content replay on the release candidate. Verify two ordinary-play adaptive outcomes and actual mechanics, NPC identity/knowledge, quest schema/objectives and truthful committed consequences. Synthetic 500-hour-equivalent fixtures do not replace H endurance; optional unadopted models/embeddings/adapters/backend experiments are not release blockers. No source-only audit, mock response, fallback-only fixture or documentation completion counts as runtime PASS.

## 11. DELIVERABLES
Versioned closed-beta package and checksum, complete acceptance matrix, seed/profile reproduction manifest, known limitations, local diagnostic bundle tool/instructions and concise tester guide.

## 12. HANDOFF TO NEXT GOALS
Publish a receipt with source/build identity, changed files, schemas/seed/profile, steps, expected/actual results, logs/captures and evidence level. Every deferred defect records ID, reproduction, evidence, severity, responsible goal, blocked consumers and next test. Use PASS/FAIL/BLOCKED/NOT YET TESTABLE per check. A future subsystem failure does not expand this goal; a failure preventing proof of this goal's own work does block its completion. Reopen the owning scope for an upstream regression and rerun affected acceptance.
External testers receive the package and guide when distribution is authorized. Feedback becomes new scoped work; deferred features remain post-beta. Preserve this candidate's source and artifacts for reproduction.

## 13. COMPLETION GATE
All essential H criteria pass on the identified build; fresh installation and real generated/adaptive play work; recovery/soak evidence is current; no release blocker remains; package and tester handoff are complete. Report READY or NOT READY with reasons, never readiness inferred from compilation or G01.

## Binding final acceptance appendix (H)

G20 is the final authority. Every row needs build/source ID, hardware/configuration, seed/profile, steps, expected/actual result, evidence link and PASS/FAIL/BLOCKED/NOT YET TESTABLE. Only PASS satisfies essential release criteria. Earlier valid receipts may be reused only for unchanged relevant code/configuration and an identified build; final ordinary-flow/clean-install evidence is fresh.

| Acceptance area | Required proof |
|---|---|
| Clean install/startup | Packaged build launches through actual title path; clean profile creation and profile switch are correct; exactly one player/state/service owner; no development fixture required. |
| Character/origin | Guided and authored origin, explicit confirmation/cancellation, supported species/appearance, safe hut-first arrival; preview and reload match; grants occur once. |
| World determinism | Primary and three varied regression seeds; semantic hash/feature identity/accepted bindings stable after opposite exploration order and restart; compatible old V1/V2 content preserved or explicit supported migration. |
| Terrain/traversal | Continuous normal-speed walk/run/dash, diagonal/reversal/off-road, slopes, roads, water/bridge and origin seams; no missing collision, holes or fall-safety intervention counted as success. |
| Streaming/sites | Unload/revisit and fresh frontier; bounded live ownership; no stale publication/duplicate objects; representative multi-cell feature retains one identity/layout; no blocked required entrances. |
| Core RPG | Both camera modes; attack separate from interact; hit/damage/death/recovery; loot/use/equip/slots and supported abilities/resources/cooldowns; no lost/duplicated items or ghost player. |
| Population/quests/dialogue | Residents and dead/changed status persist; authorized knowledge differs by NPC; supported quest patterns and branching choice complete; rewards once; transcripts/memory and later reaction survive restart. |
| Economy/social | Earn/buy/sell/service and stock persistence; private versus witnessed incident produces plausible local knowledge; standing/awareness, relationship and independent moral/spiritual axes/history persist. Broad global social simulation is not claimed. |
| Defining adaptive loop | At least two ordinary-play behavior families produce explainable eligible candidates, real generated offers and useful accepted mechanics/opportunities; title/class effect and one persistent NPC/world response; decline/incubate/retry safe. No developer grants or fixed final-name pools substitute for generation. |
| LLM integration | Real configured-model success separately from offline fallback; bounded timeout/malformed/queue/cancel handling; profile-switch stale rejection; movement and accepted content continue without inference; recovery does not regenerate accepted records. |
| Persistence/reconstruction | Save→quit→reload→revisit preserves identity, accepted generated payloads, origin/appearance, inventory/equipment, abilities/offers, quest progress/rewards, NPC memory/status, social history, discovery and feature mutations. Test harvested/deleted/opened and spawned/changed content where retained. |
| Recovery/migration | Interrupted staging/commit, corrupt latest snapshot, stale shared copy and unsupported version yield complete known-good revision or explicit safe failure, not silent reset/mixed profile. Supported legacy saves migrate idempotently. |
| UX/accessibility | Normal tester can navigate opening and core/adaptive flows; long text fits; modal input safe; settings persist; supported readability/subtitle/motion options work; unknown map/knowledge stays hidden. |
| Performance/resources | Locked target hardware and budgets below; measured full frames, save/generation/apply/native resources; no runaway active memory, tasks, requests or log/history growth. |
| Packaging/handoff | Version/beta/date and schema/generator/manifest identity; package checksum; clean installation/restart; controls/model setup/recovery/limitations/reporting guide; local diagnostic bundle excludes sensitive text by default, no automatic uploads. |

Provisional budgets inherited from the world design plan: ordinary exploration targets 60 FPS (16.6 ms), ordinary main-thread streaming work approximately 1–2 ms/frame, and no normal-travel activation hitch above 50 ms. G19 records target hardware/OS/resolution/quality, sample windows and whether GPU/CPU bound; lock explicit p95/p99 frame, peak resident memory and save-latency limits before testing. Initial normal-travel p95 frame target is 16.6 ms and p99 33.3 ms on that declared configuration; disclose any accepted change with measured rationale and rerun. Do not use a 50 ms emergency slice ceiling as the ordinary streaming allocation. Save commit timing's historical 100 ms gate remains recorded, but total-frame stalls and recoverability determine the final design; moving work off-thread must preserve snapshot consistency.

Minimum endurance matrix: one four-hour primary-seed session and one two-hour different-seed session on the candidate, with combat, dialogue, quest, trade, adaptive generation, frontier travel and at least three save/quit/reload cycles in each; targeted opening/seam/reload checks on the remaining two seeds. Run repeated 30-minute unload/revisit loops after warm-up: live cell/entity/task counts remain within configured caps; resident memory returns within 10% of the comparable warmed baseline after settling, or an identified bounded cache growth is measured against its explicit cap. Durable on-disk history may grow with real events; unbounded live caches may not. Record cadence/resource samples and profiler traces, not just beginning/end screenshots.

Final blockers include any reproducible routine crash, save corruption, mixed identity, normal-play softlock, missing terrain/collision, impossible required objective, duplicated grant/NPC, lost appearance/accepted content, adaptive mechanic that does not execute, model outage blocking ordinary play, or severe recurring performance failure. Cosmetic limitations may ship only when documented and outside these criteria. Record final READY/NOT READY and the exact package; the roadmap itself makes no readiness claim.
