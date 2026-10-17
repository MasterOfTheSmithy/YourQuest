# G08-R2 D42 — input route and remaining frame cost

Status: **diagnostic FAIL / INVALID_FOCUS**, not clean certification. Returned to idle Edit. All eight source/assembly hashes matched; no compilation during observed Play (`outputs/G08_R2_D42_ReturnedAudit_20261002.json`).

## Identity

Receipt: `Docs/G08_R2_Speed260_2026-10-02_162251_D42_Background_Diagnostic_Receipt.md`. Existing PlaySafe/title/fixture Continue, route v2, original2.45-second input and full natural coast. MVID`38136141-be60-4f7a-9b2d-07173e9bde66`; mainDLL`A168D91E53E1558729A213D7041B486A81B249C7A0631183EA08058A0583DE7B`. Added eight fixed motion/contact records, formatted after timing, and an initial native boundary; no input change. Binary-only capture; no stacks, deep profiling, cell history or GPU polling.

Raw`Logs/G08_R2_FrameTrace_20261002_162239_986.raw`, GUID`85549998-ae21-44b6-8f6b-79651e1bdbfb`.306 measured intervals now all have paired native boundaries, including the first input frame. Saved three-frame exports: `Logs/G08_R2_D42_Maximum.samplethreads.tsv`, `Logs/G08_R2_D42_Input.samplethreads.tsv`, `Logs/G08_R2_D42_Representative.samplethreads.tsv`. Exact clipped owners: `outputs/G08_R2_D42_NativeOwners_20261002.json`.

## Gates and direction evidence

Input68 frames maximum49 ms; coast238 maximum78 ms. All68/238 frames unfocused, so this cannot establish clean performance. Coast1181.281 m versus integrated1182.224 m, no physical coast obstruction/fallback/block/recovery. Input visible and turn buffer PASS; coast complete-visible coverage FAIL. Direction input63/63 but displacement41/63. No cell history was armed, so readiness causality is not established.

The new samples prove physical input-route obstruction in this diagnostic: frame8354 southwest movement199.107 m/s/dot0.754 with two side contacts; frame8355 backward movement27.793 m/s with three side contacts; frame8356 southeast183.947 m/s/dot0.708 with four contacts. All involve `YQ_GENERATED_WORLD_RUNTIME/YQ_GENERATED_TERRAIN`, with no readiness rejection. The short eastward preflight does not cover the full turning area.

Do not attribute the previous P17 clean forward-right failure to these different failed directions. Forward-right passes in D42. Profiler setup enlarged the first integrated move: first boost wall46 ms versus Unity/motor delta175.576 ms, requested45.651 m and actual45.481 m, including a terrain side contact. This displacement shifts the subsequent loop. It is diagnostic perturbation, not proof of clean movement at983.8 m/s or a clean forward-right motor defect. The missing P17 direction-frame evidence cannot be reconstructed.

## Native frame cost

| Actual interval | Wall | Same-boundary evidence |
| --- | ---: | --- |
| Input maximum8362/native465 |49.171 ms|Streamer Update callback15.754 ms; decorative scatter coroutine3.427 ms; no collection;1512256 bytes in retained allocation leaves.|
| Coast maximum8497/native600 |78.402 ms|GC.Collect19.266 ms nested under UnityEditor.LevelPlay.LevelPlayInstallMenuItem.UpdateMenuItem; motor10.967 ms; streamer Update8.857 ms;1174581 allocation bytes.|
| Representative coast8443/native546 |58.241 ms|LLMThinkCycle.Update21.785 ms; streamer Update6.308 ms; no collection;961351 allocation bytes.|

All callback costs are inclusive. Parent/child and cross-thread samples are not summed. The location of GC inside an unrelated Editor callback does not establish that callback as the sole source of allocation pressure; do not remove/disable the package on that basis. This capture does not prove a GPU/driver or Ollama paging cause. The request-polling fixture remains the direct evidence for P17's narrow allocation reduction.

## Next repair boundary

P18 extends the existing same-row route preflight to the full bounded turning footprint, derived from unchanged260 m/s and50 ms accepted-frame lengths. It rejects steep sampled terrain and obstacle-filled turning areas, retains original input/thresholds, and explicitly invalidates physical input obstruction. It adds no scheduler work, preparation frames, world changes or collision bypass.540 detached route checks pass, including all512 extremal step-length combinations at three player orientations. One unprofiled witness is required; no repeat of an unchanged failing route.

Remaining full goal: valid route, all focused readiness/movement/50 ms gates, independent clean PASS repeat, then affected/missing full-R2 rows. Exhausted scheduling/reservation and existing visual-write counters are unchanged. R3 remains gated.
