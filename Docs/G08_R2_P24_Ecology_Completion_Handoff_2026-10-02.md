# G08-R2 P24 — prepared ecology completion handoff

Status: compiled and focused Edit regression PASS; gameplay result pending. Full R2 remains FAIL/incomplete. R3 remains gated.

## Demonstrated cause and scope

Latest clean P23Q receipt: `Docs/G08_R2_Speed260_2026-10-02_183239_P23Q_Background_Diagnostic_Receipt.md`. Movement, waypoint staging, all eight directions, input visibility and full natural coast passed. Input/coast maxima were 63/72 ms against unchanged 50 ms gates. First coast visibility miss was (13,7): structure/overlays finished in the observed frame, while already-prepared ecology remained `waitingForRequiredOwner`.

Existing same-source D46 history independently confirms the completion-order delay: `Logs/G08_R2_CompleteCells_20261002_182427_195.tsv`, MVID `f99d8b98-6f7a-480f-bfad-fcfe52f4d404`. A same-cell/configuration/content-epoch analysis found 24 visible coast receipts waiting exactly one frame after structure and overlays were ready (38.906–59.384 ms, median 49.231 ms). Cell (13,7) waited frame8271→8272 /49.593 ms with ecology progress unchanged at 7 steps/5.667 ms. Another 29 approach receipts had the same one-frame delay; none were visible at that sampled transition. All paired pending observations found a completion. Frame-end observations bound these delays; they do not attribute every earlier preparation delay.

Analysis: `outputs/G08_R2_P24_HandoffHistory_20261002.cjs`; result: `outputs/G08_R2_P24_BeforeHandoff_20261002.json`.

This is a prepared-result publication handoff defect. It does not reopen the exhausted terrain scheduler/reservation loop or establish that all deadline misses have this cause. The distinct frame-time failures remain unresolved.

Expected measurable change: remove the extra frame after structural/overlay completion when the same current ecology attempt already has an accepted required result. Do not advance provider iterations, admit extra workers, change priorities or budgets, or bypass publication barriers. Shortest regression: actual production publication helper with both completion orders, pending/invalid results, missing structure/overlay, holds, injected faults, stale ownership identities, duplicate publication, and cancellation after publication; then one clean focused witness.

## P24 implementation and review

- `Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs`: keep the required result and publication state in a transient attempt-local receipt shared with the structural completion path. The structure owner calls the same guarded publication helper immediately after its accepted structure and overlay pass. It reuses that just-completed overlay replay over the same hierarchy, with no intervening yield. Ecology completing later still binds targets and applies overlays itself. Both paths require current profile/world, configuration/overlay revision, owner epoch, dictionary ownership and work ID; preserve required-layer holds/faults and accepted result checks.
- The coroutine and its cleanup read the same `published` receipt. Cancellation clears the current worker registration and retains already-published required ecology; an unpublished attempt still follows existing root cleanup/retry behavior. Optional ecology remains in its existing iterator and bounded slot policy.
- The handoff runs inside the existing structural iteration and therefore its cost remains inside the existing required-content work accounting. It removes the redundant second hierarchy/overlay scan for ecology prepared before structures. One small transient receipt object is added per ecology attempt; no per-frame allocation or persistent schema is added.
- `Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs`: adds Edit-only `ecology-handoff-only` regression through the existing analysis marker handler. Stress input, route, measurement clocks, acceptance limits, and gameplay marker flow are unchanged.

Senior source review: no saved data, accepted generation/hash, scene/prefab/GUID, terrain representation, publication requirement, worker/owner limit, movement or camera contract changed. The R1 publication/cancellation barriers remain explicitly enforced; these narrow dependency checks are not an R1 recertification or a substitute for live R2 evidence.

## Fresh verification

- Unity runtime C# DLL rebuilt 2026-10-02T18:48:07.7483066Z; Editor finished reload in Edit with `compiling=False`, `updating=False`.
- Narrow fresh compile-log inspection showed completed reload and no `error CS` in that bounded tail; this does not assert absence of historical/runtime errors.
- Runtime MVID `ff45d20d-4d64-4ab9-b9d2-5e6b3d10982a`.
- `Logs/G08_R2_P24_EcologyHandoff_20261002.tsv`, 18:48:39.8912886Z: **205/205 PASS**. Production helper tested in isolated inactive owners; both completion orders, required barriers, all nine stale identity/registration cases, idempotence, hold release, missing hierarchy rejection and cancellation preserving the published layer.
- Patch whitespace check passed. Nine source/build hashes recorded in `outputs/G08_R2_P24_CompiledIdentity_20261002.json`.

Gameplay is not yet verified on P24. User asked to let an active ComfyUI video-model job finish before continuing the gameplay measurement; no Play marker is queued while waiting. Next action is one clean focused PlaySafe/title/Continue run after that job finishes, using route-v5, 616x264 viewport, the same accepted disposable fixture and all unchanged acceptance settings. No diagnostic binary/history capture is enabled for that clean run. Report every test's advancement and remaining gates.

Repair count: P24 is applied with a passing narrow regression; no focused closure attempt yet. Previous cause counts remain unchanged.

## P24 clean focused result — FAIL

Run19:15:42.2119227–19:16:15.4087995UTC, receipt `Docs/G08_R2_Speed260_2026-10-02_191615_P24_Background_Diagnostic_Receipt.md`. Runtime MVIDff45d20d-4d64-4ab9-b9d2-5e6b3d10982a; all nine armed/returned hashes match, no observed compilation in Play, returned to Edit. Audit `outputs/G08_R2_P24_ReturnedAudit_20261002.json`.

The prior Editor PID25632 and ComfyUI Python processes had closed during the interruption; reopened PlaySafe PID48356 started19:07:39UTC. The agent did not restart Unity or clear caches. Record this changed session condition when comparing timing. Viewport616x264, DX12, Project dock, profiler/binary/stacks/history off, valid focus with0/0 unfocused frames and369/369 frame samples. Fixture/profile/world/seed/accepted V2 hash unchanged and terrain snapshot restoration succeeded.

| Gate | P24 clean result |
|---|---|
| Waypoint staging / ordinary controls |PASS|
| Abrupt6-to-260 / seam / all eight directions / constant segments |PASS|
| Input physical movement / full visibility / turn buffer |PASS|
| Input frame maximum |FAIL57ms; P50/P9534/47ms|
| Full natural coast physical movement |PASS1160.182m,9.282s,settled0.5m/s|
| Coast ground / zero fallback / zero recovery / zero traversal blocks |PASS|
| Coast full visibility |FAIL first(13,7), structures still incomplete|
| Coast frame maximum |FAIL54ms; P50/P9530/40ms|
| Complete focused PASS / independent repeat |NOT OBTAINED / NOT RUN|
| Final affected R2 rows / R3 |INCOMPLETE / GATED|

P23Q-to-P24 observed maxima63/72→57/54ms do not isolate P24's performance effect because the Editor process and demand timing changed. P24 is one unsuccessful complete closure of the handoff cause despite its205 passing narrow checks. Its local runtime delay removal remains to be measured; do not infer it from lower maxima.

First coast miss(13,7): contentRequired=False,overlay=False,ecology=False,appearance=True,CollisionReady. At the diagnostic snapshot structure is at BuildAcceptedSiteRoutine13steps/0.600ms,max0.445ms,lastFrame6567 with8 budget-denied frames/0.263s. Ecology has already prepared7steps/5.742ms,max3.104ms,last6565 and is waitingForRequiredOwner, one budget-denied frame/0.030s. Owner epoch2,attempt1,restarts0,preemptions0,age0.455s. This remaining failure is earlier than P24's new handoff: structures have not completed. It does not reopen the exhausted scheduling/reservation loop.

Worst clean input6481=57ms and coast6648=54ms, both no managed collection-count increment. Recorder arrival values do not attribute these exact wall intervals. D46 previously measured a17.831ms motor slice with429472B/4872 allocations, but current post-P21 managed source callsites remain unknown. D44 stacks predate P21 and cannot provide that attribution.

Next bounded diagnostic D47: one original-window binary/history witness with the existing coast allocation-stack option (maximum24intervals/1s) to identify current motor/camera managed allocation callsites, pair exact verifier maxima to native frames, and determine whether same-frame structural completion now removes prepared-ecology waits. Freeze the first actual failed cell and measure its remaining structural/budget history. Competing explanations for residual frame cost are motor/camera allocation work, other project callbacks and native/Editor waits. Exit after one capture and analyze it before another test or patch. No extra preparation, changed inputs, raised limits or scheduler repair.

## D47 confirmation

Current-source diagnostic history confirms the targeted extra-frame delay is gone: all60 matched prepared-ecology handoffs finish in the same frame, including29 coast observations. See `Docs/G08_R2_D47_Handoff_And_Motor_Allocations_2026-10-02.md`. Profiling invalidated the route, so D47 cannot certify performance or replace the clean P24 failure. No further P24 repair was applied; remaining structures/appearance readiness and frame costs are separate open work.
