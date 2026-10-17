# G08-R2 D32 — lifecycle subcall attribution

## Question and exit condition

D31 matched coast maximum Unity frame 5100 to native frame 2746: 56.671 ms wall time, 56.9093 ms native interval, no GC; streamer LateUpdate 10.457 ms included ApplyLifecycleOwnerPass 8.905 ms. Existing samples do not distinguish cumulative readiness checks, activation and renderer reconciliation, cancellation, or teardown. This does not assign ownership to the different P11 clean maximum.

One original-window binary-only diagnostic will measure those actual subcalls on current P11 behavior. Stop capture at the existing focused witness's terminal boundary, analyse its actual maximum and representative owner-pass frames, then choose a repair only if the measured operations demonstrate a cause. No repeated unchanged clean run.

## Source change

D32 adds six allocation-free profiler scopes to the streamer: LifecycleReadiness, LifecycleActivationSlice, LifecycleVisualReconciliation, LifecycleHierarchyVisualGate, LifecycleCancellation, and LifecycleTeardown. The saved-capture exporter retains these named samples below its ordinary 1 ms filter, allowing cumulative attribution. Nested scopes remain inclusive and must not be added to their parents.

No readiness predicate, content, operation order, lifecycle budget, demand, owner cap, concurrency, movement, camera, visual quality, save, or accepted-world change. Original source snapshots are `outputs/G08_R2_D32_StreamerBefore_20261001.cs.txt` and `outputs/G08_R2_D32_VerifierBefore_20261001.cs.txt`. Diagnostic source changes do not establish a production repair or reset any exhausted repair loop.

## Status

Compilation passed outside Play: 14.24 s build, DLL timestamp 14:19:20 UTC, no compiler errors and the existing unrelated CS0162 warning. Native screenshot confirmed idle PlaySafe before arming. D32 executed 14:21:54.8892626 UTC through the terminal receipt at 14:22:16 UTC, then returned to Edit. Every source/DLL hash matched arming after the run; no compilation during Play. Both measured phases had zero unfocused frames. Binary enabled; stacks, deep profiling, GPU/history/motion overrides off. Original movement and full natural coast retained.

Executing MVID `67a6aa01-02de-4d9e-a9b7-e534df876b73`, main DLL `6EFE464CD3BB2A0CDD39BBA5B3F1615C0851BC8FFDD0277019D739C0A76E78CD`; streamer `D2A3FC829FB862D998494D4EFCC8EB6D0BC538DDFCE55E093B9609A23B905712`; verifier `C167AA0BA79473B841BE8D5DC91B95138C1BF78AF8B72A6A0F3A45EF40C89ED0`. Complete identity/status: `outputs/G08_R2_D32_Armed_20261001.json`, `...Compiled...json`, `...WatchStates...jsonl`, `...ReturnedEdit...json`. Same profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted V2 hash `c1d6ce9d3e06f760`, schema 7, view 512 m, owner cap 256, PID 28040, Unity 6000.3.2f1/DX12, Ryzen 5800X/RTX 5060, 1278x637/scale 0.79.

## Diagnostic results

[Immutable receipt](G08_R2_Speed260_2026-10-01_142216_D32_BinaryOnly_Receipt.md). Input 59 frames, median/P95/max 39/54/59.546 ms. Coast 292 frames, 32/45/84.962 ms, 9.811 s/1328.6 m, settled at 0, normal acceleration/deceleration 28/34 restored. Visible/ground/turn/boundary/fallback/recovery gates PASS; zero synchronous fallback or traversal block. Profiled abrupt transition, direction and constant segments FAIL; not clean certification. Coast coverage composite FAIL reflects its frame budget despite complete visibility.

Raw `Logs/G08_R2_FrameTrace_20261001_142204_351.raw`, 272099402 bytes, GUID `01aeb2c4-9d9f-427e-a6f7-2c1b07b52267`. Four saved ranges each matched two native frames, without another capture: input maximum 2860:2861, coast maximum 3111:3112, actual largest lifecycle owner pass 3032:3033, representative slow coast 3051:3052. Exact clipped paths and allocations: `outputs/G08_R2_D32_AllOwners_20261001.json`; cumulative named subcalls: `outputs/G08_R2_D32_LifecycleSubcalls_20261001.json`. Named calls retain sub-1 ms samples; duration rounding is 0.001 ms per call. Other tiny CPU samples remain filtered. Nested totals below are inclusive and must not be added.

| Unity frame / wall interval | Actual owner evidence |
|---|---|
| Input maximum 4779 / 59.546 ms | Stream Update 17.996 ms; Late owner pass 5.384 ms includes 418 activation-slice calls totaling 4.898 ms. Readiness 207 calls/0.207 ms; visual reconciliation 281 calls/0.046 ms. No hierarchy gate or GC in this owner pass. |
| Coast maximum 5030 / 84.962 ms | Update 37.949 ms includes GC 22.407 ms. Late owner pass 1.588 ms includes activation 1.381 ms; readiness 0.067 ms, visual reconciliation 0.154 ms. Hierarchy updates are not the dominant operation in this maximum. Collection context does not identify which allocation caused it. |
| Largest lifecycle pass 4951 / 47.637 ms | Coroutine owner pass 11.800 ms includes activation 11.508 ms, visual reconciliation 9.915 ms, **29 hierarchy gates totaling 8.925 ms**. Hierarchy gate allocations 190056 bytes/4171 events, including MaterialEditor property-drawer paths 166800 bytes/3912 events. Readiness 0.166 ms, cancellation rounded below 0.001 ms per call; no teardown. A separate Late pass is 1.030 ms, not part of the 11.800 ms parent. |
| Representative coast 4970 / 54.295 ms | Motor camera-ground admission 12.836 ms; Late owner pass 8.082 ms includes activation 7.754 ms and one hierarchy gate 1.494 ms. Hierarchy allocation 30816 bytes/690 events, including MaterialEditor paths 27800 bytes/652 events. Readiness 0.168 ms, teardown 0.065 ms. No GC. |

MaterialEditor CPU callbacks under 1 ms are filtered, so an absent callback sample is not zero callback cost. Allocation paths provide the production link. Owner pass counts include semantic owners without physical roots; they do not by themselves demonstrate exceeding the 256 physical-owner cap. Motor ground allocations remain separately implicated, not repaired here.

## Next bounded decision

Actual hierarchy-wide renderer updates are a demonstrated lifecycle cost. Source already gates the hierarchy scan on `visualStateChanged`; skipping that existing publication operation is unjustified. The remaining question is whether individual Renderer/Terrain setters repeat a value already present or whether they are necessary publication transitions. Source writes every setter unconditionally, but this capture does not record the prior value. D33 will count actual already-correct assignments in one binary-only original-window run, retaining every original write and matched frame-boundary metadata. Stop after those counts resolve redundancy; no more broad unchanged trace. If redundant writes are materially present, a guarded-setter repair needs identical-final-state regression and final-source clean verification. No scheduling or content change is justified by D32.

P11 clean remains FAIL (input maximum 56 ms, coast maximum 61 ms). Full R2 and independent clean repeat remain unverified; R3 stays gated. D32 is diagnostic progress, not another production repair attempt.
