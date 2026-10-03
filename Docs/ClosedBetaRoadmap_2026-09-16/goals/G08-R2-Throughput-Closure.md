# /goal G08-R2 — Close complete-cell deadlines and measured frame throughput

## Outcome and scope

Complete the existing G08 R2 seamless-playable-streaming contract in the current YourQuest checkout. Use the production PlaySafe/title/Continue path, authoritative motor, real elapsed time, accepted world, and existing background marker workflow. Deliver repeatable evidence that complete visible cells are ready on time while the unchanged frame and resource limits hold.

This is a replacement execution brief for the repeatedly failing R2 task. Goal-08.md and 8 fix 4.md retain their acceptance authority. This brief changes the investigation and repair sequence, not the requirements. Do not execute R1 or R3. Keep R3 gated until every required R2 row is supported. A focused speed-witness PASS alone is not full R2 certification.

Preserve player/profile/world identity, seed, accepted artifact and hashes, save authority, deterministic geography, full visual quality, movement/camera controls, current view distance, physical capacity, zero synchronous fallback, and the existing 50 ms gate. Do not hide incomplete cells with fog, reduced view, camera constraints, movement throttling, waiting, teleportation, or omitted content. Preserve unrelated working-tree changes.

## Evidence to start from

Read these bounded references and the directly implicated methods; do not restart a repository audit:

- `Docs/G08_R2_Focused_Gate_2026-09-29_192245_FAIL.md` and its linked full witness: focus-valid, unprofiled. Startup and short ordinary controls, abrupt 260 m/s transition, boundary crossing, zero synchronous fallback, constant input, direction changes, input visibility, and turn coverage passed. Coast visibility and frame budgets failed. Coast P50/P95/max were 59/89/131 ms. First failed cell `(11,3)` had collision-ready terrain but incomplete required content, overlays, and ecology. Structure progress was budget-denied for 13 frames / 0.989 seconds.
- `Docs/G08_R2_Matching_Frame_2026-09-29_192903_FAIL.md` and `Logs/G08_R2_FrameTrace_20260929_192852_300.tsv`: diagnostic capture on the same tested source. It identifies streamer scheduling/lifecycle/content starts, motor camera-ground work, GC, and light-probe work. At frame 9699, camera-ground admission took 63.9 ms, including an 18.5 ms collection; streamer Update took 30.5 ms. Inclusive samples overlap: do not add nested times.
- `Docs/G08_R2_Astra_Audit_2026-09-29.md`: records earlier invalid attribution, distinct terrain/appearance failures, and invalid unfocused attempts. Request age did not prove queue-head starvation; process memory did not prove a leak; recorder LastValue did not identify the source frame.
- `Logs/G08_R1_PublicationRecoveryWitness.md` and `Logs/G08_R1_UnloadRevisitWitness.md` contain September 28 scoped PASS evidence. Preserve their scope and identity; do not rerun them or claim that they certify later source changes.

The September 27 fix-4 snapshot is historical. Do not keep repairing its old cave staging or absent-candidate failures without reproduction. Do not assume the remaining coast failure is still missing terrain: the latest clean receipt above failed required content/ecology instead.

At preparation of this brief, the streamer on disk already differed from the source hash in the 19:22/19:29 receipts. Its later edits include lifecycle scheduling changes. Establish the current diff and executing assembly before deciding whether those measured costs remain. Do not overwrite or redo those edits.

## 1. Establish one reproducible baseline

1. Inspect working-tree status and relevant diffs. Read applicable AGENTS.md files. Identify the current streamer, environment/materialization iterator, motor camera-admission, and verifier ownership boundary; expand only when fresh evidence implicates a direct dependency.
2. Discover actual tool capabilities and follow the current `UNITY_MCP_GUIDE.md` and installed computer-use skill for Editor access. Use one Editor operator. Confirm the intended project, PlaySafe, compile/import state, and Play Mode state. Do not edit or compile during Play Mode.
3. Compile current executable changes through the existing Editor refresh workflow outside Play Mode. Inspect fresh compiler/Console diagnostics and record assembly MVID/hash plus relevant source hashes. If another task is editing these owners, settle ownership before patching or measuring; never certify a moving source baseline.
4. Use the existing disposable R2 profile and accepted artifact. Record profile/world/seed/hash, Unity version, relevant hardware, viewport/render configuration, focus, profiler state, and thresholds. If the profile is unavailable, explicitly establish a replacement baseline; do not silently compare different worlds as a before/after optimization.
5. Inspect the current marker handler before using it. The known focused entry is `Temp/YQ_SPEED260_FOCUSED.request`. Use the ordinary Continue flow with Game view Play Focused and verify focus throughout measured frames. Runtime request markers are inputs to the existing workflow; never hand-edit generated reports or caches.
6. Reuse a qualifying receipt only if affected source, executing assembly, fixture, and measurement setup match. Otherwise run one unprofiled focused baseline. Persist its first failed cell/stage, complete phase verdicts, frame distribution, and identity immediately. An unfocused or incomplete attempt is invalid for certification and must not be diagnosed as a gameplay regression.

## 2. Prove the active bottleneck and available completion time

Before changing priorities, concurrency, lookahead, pooling, or rendering, answer both questions with measured evidence:

- **What consumed the failing frames?** Use explicit frame IDs and timestamped native/project samples. If the current failure lacks attribution, arm the existing opt-in `Temp/YQ_R2_FRAME_TRACE.request` with the focused witness. Keep this diagnostic run separate from unprofiled certification. Attribute Update, coroutine resumes, motor camera admission, LateUpdate/lifecycle, Unity activation callbacks, GC, rendering/editor waits, and other actual contributors without double counting. Inspect a representative slow frame as well as the maximum: the previous unprofiled median already exceeded 50 ms.
- **Why was the first incomplete cell late?** Trace its original demand, deadline changes, epoch/work identity, worker dispatch, cancellation/requeue, terrain, structures, appearance, ecology, overlays, and activation. Distinguish missing work, dependency waits, denied budget, expensive execution, and late prediction. Request age alone is insufficient. Identify which completed-cell stage missed the exposure deadline and which work used the time it needed.

Calculate the required workload for the existing visible/turn/swept envelope and actual travel/coast trajectory. Use measured full-cell readiness latency and stage costs, not height-task counts alone. At the default 128 m cell size, 260 m/s crosses one cell length in about 0.492 seconds; a moving view can admit several cells during that interval. Verify runtime cell size rather than assuming the default. Use the motor's actual velocity and ordinary restored deceleration during coast; releasing input does not end travel demand.

Record a compact feasibility table: newly required cells per second, completed cells per second, readiness latency versus time until exposure, synchronous stage costs, base-frame cost, and live/retained/in-flight capacity. A queue-order change is justified only when the evidence shows usable capacity being allocated incorrectly. If demanded complete-cell work exceeds sustainable capacity, target the measured cost or preparation timing that creates that deficit; do not merely reshuffle starving stages.

## 3. Make the smallest coherent causal repair

Choose one demonstrated cause and state the expected measurable improvement before editing. Then patch its owner and necessary direct callers.

The existing source has a nominal 10 ms aggregate streaming allowance, but `CanAdvanceAggregateWork` admits several urgent lanes after that allowance is spent, and camera-ground work can execute from the motor. Audit the actual accounting and observed costs before calling this an enforced global cap. This is a specific investigation lead, not permission for a wholesale scheduler replacement.

Depending on the current evidence, a coherent repair may remove redundant demand/lifecycle scans, split an expensive iterator quantum, avoid measured allocations, share preparation already computed by the authoritative owner, or correct a deadline/worker eligibility defect. Preserve accepted output, readiness barriers, cancellation/version semantics, and ownership. Do not add another streaming authority.

For every affected synchronous lane:

- Include its cost in the same frame's streaming accounting, regardless of which callback invokes it.
- Bound useful work per invocation and stop admitting additional discretionary work when its allocation is spent. A post-call stopwatch cannot bound an indivisible operation; split or amortize the demonstrated expensive operation where output can be preserved.
- Preserve honest failure and safe recovery if preparation misses a deadline. Removing a fallback or hiding its counter does not prove zero fallback; the qualifying run must demonstrate that it was unnecessary.
- Keep complete-cell deadlines across all required stages. Terrain publication success cannot substitute for structure, appearance, ecology, overlay, or activation completion.

Do not mechanically delete all emergency allowances to meet a timing counter; that can trade a frame failure for a visible hole. Show how the repaired production path prepares the same complete result before exposure within available frame time. Similarly, adding another reserved slice, raising concurrency/capacity, or moving work to LateUpdate needs evidence of reduced total cost or corrected allocation, not just a newly passing subgate.

If a native/render/light-probe owner dominates, use its actual triggering relationship to choose a bounded repair. Do not infer a driver defect, change graphics settings, remove probes, or lower quality from a sample name. Diagnostic editor/profiler overhead must be reported separately; a different execution environment may diagnose it but cannot silently replace the agreed acceptance path.

Compile the coherent patch outside Play Mode, inspect fresh diagnostics, then run the shortest affected regression. Do not run another full suite after a known first-stage failure merely to collect another FAIL file.

## 4. Demonstrate repeatability and close the actual R2 rows

Once the narrow repair passes, run **three consecutive qualifying, unprofiled focused R2 witnesses** on identical final affected source and the same accepted world, each from a fresh PlaySafe/title/Continue session. This defines repeatability for this replacement brief; it is not a claim that older documents already required three runs. Do not add verifier-only warmup or prefill, grant extra scheduler work, alter stress parameters, or skip coast/turn phases. Preserve ordinary production startup work.

Each witness must independently pass every existing focused gate, including ordinary controls, abrupt transition, direction/turn coverage, full coast visibility, swept ground, zero synchronous fallback and readiness interventions, and the unchanged frame limits. Record maximum and distribution; do not replace the maximum gate with an average or percentile. Record stage backlog and live/retained/in-flight owner, worker, and painter lifetimes against their governing limits. A final below-capacity census alone is insufficient.

Retain failed attempts and reset the consecutive-pass count after any qualifying failure. A diagnostic profiler run is not a performance PASS. Stop invalid attempts promptly, repair the measurement setup, and preserve their invalid status rather than using them to tune gameplay.

Maintain a compact R2 acceptance matrix covering cold startup, accepted-save Continue, ordinary movement, diagonal/reversal/turns, sustained travel, the 260 stress and coast, visible/swept coverage, resources, deliberate-stall failure/recovery, and unload/revisit. Reuse valid existing evidence for unaffected rows. Identify source/dependency validity explicitly. Run only missing or affected R2 checks through existing supported entry points; do not invoke R1 or R3 under another label. If the required evidence is inseparable from an out-of-scope suite, report that row blocked rather than silently broadening scope.

If only the focused witness is proven, report **focused R2 PASS; full R2 incomplete**, name the missing rows, and keep R3 gated. If a changed dependency invalidates R1 evidence, identify that consequence without running R1 or representing its old receipt as current.

## Stop rules and delivery

- No blind optimization loop. After at most three unsuccessful repairs of the same demonstrated cause, stop that repair boundary. Continue only independent, already in-scope evidence work.
- If measured required work cannot fit the preserved frame/capacity/deadline envelope within a bounded repair, report the quantified deficit and a concrete next implementation boundary. Do not silently relax quality, speed, view, capacity, or thresholds, and do not undertake an architectural replacement under this goal.
- If access, compilation, fixture identity, focus, or concurrent edits prevent valid evidence, report the exact blocker and preserve the checkout/profile. Do not keep launching invalid runs.
- Finish with one receipt: result, causal finding, changed files, current source/build and world identity, before/after matched measurements, three-run results if reached, full R2 matrix, preserved contracts, exact remaining failures, and the next decision only if needed. Link full witnesses and traces. R3 remains gated unless full R2 is supported.

The task is complete when full R2 is proven under this contract, or when the bounded investigation/repair reaches a precisely evidenced stop condition. Reporting a stop condition is not a PASS.
