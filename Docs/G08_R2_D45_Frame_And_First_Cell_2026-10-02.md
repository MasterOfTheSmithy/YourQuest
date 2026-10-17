# G08-R2 D45 — final P21 frame and first-cell evidence

Status: **diagnostic only; focused and full R2 remain FAIL/incomplete.** R3 remains gated. Run 2026-10-02 17:47:32.4205603–17:48:06.2076305 UTC through PlaySafe/title/existing-fixture Continue. Unity PID25632, 6000.3.2f1 DX12, source assembly MVID `33a1f341-e48d-4378-8b62-443a70839c94`. Fixture `3ccfff4df9a1479884061e51940e4457`, accepted V2 hash `c1d6ce9d3e06f760`. All nine armed source/DLL hashes match after return to Edit; no observed compilation in Play. Audit: `outputs/G08_R2_D45_ReturnedAudit_20261002.json`.

Receipt: `Docs/G08_R2_Speed260_2026-10-02_174806_D45_Background_Diagnostic_Receipt.md`. Binary trace and complete-cell history were enabled, so these timings cannot certify performance. All measured input/coast frames stayed focused. Waypoint staging passed17/17 in12.915 s. Input motion/directions failed after the second turn collided with the witch-house box (mask45/63). Full natural coast1117.443 m in9.347 s settled without obstruction, synchronous fallback, traversal block or recovery.

## Exact frame attribution

Raw: `Logs/G08_R2_FrameTrace_20261002_174752_956.raw`, capture GUID `9818b4fc-ef3b-471d-8f2e-f029b09ce155`. All259 input/coast intervals have both matching native boundaries. Saved exports Input1454:1456, Coast1546:1548, Representative1639:1641 each matched three frames. Analysis: `outputs/G08_R2_D45_FrameCost_20261002.json`, `outputs/G08_R2_D45_NativeOwners_20261002.json`.

| Interval | Wall time | Same-interval evidence |
|---|---:|---|
| Input max, Unity6300/native1456 |86.265 ms|No GC. Streamer Update12.836 ms and LateUpdate11.686 ms include observer overhead. Preceding native1455 contains28.840 ms PostLateUpdate/CoroutinesDelayedCalls. No large Editor repaint wait in this interval.|
| Coast max, Unity6392/native1548 |82.930 ms|Motor41.448 ms includes nested GC25.617 ms. Streamer Update6.228 ms, LateUpdate6.210 ms. Render wait overlaps main-thread work.|
| Representative coast, Unity6485/native1641 |55.842 ms|No GC. Motor17.093 ms, streamer Update9.894 ms/LateUpdate5.734 ms.|

Input P50/P95/max53/78/86 ms; coast41/58/83 ms. Inclusive nested samples and render waits are not added. Allocation event totals for these retained intervals are572119B/3496 events,407559B/4575 events,903097B/7887 events respectively; they do not identify the source of all bytes. Native command-list labels do not prove GPU execution time or a driver fault.

The saved exporter originally filtered most sub-millisecond children. P22's Edit-only exporter now retains all delayed-coroutine children. Complete saved-frame extractions (no new live capture) resolve the recurring parent: D45 native1455 has319 direct `Destroy` calls totaling28.740 ms within28.840 ms; D43 native769 has322 totaling34.706 ms within34.817 ms. Inclusive descendants are not added. `outputs/G08_R2_D45_DelayedChildren_20261002.json` and `outputs/G08_R2_D43_DelayedChildren_20261002.json` retain the counts and scopes. D45 history shows76 cells leaving demand in6298 and64 distinct owners removing186 tracked roots in6299:64 full terrains,61 provisional terrains and61 semantic roots. The native sample alone does not name each object, but same-frame lifecycle teardown, object events and source establish the burst boundary. This is concrete deferred native cleanup cost, not an unnamed coroutine or driver inference.

## First failed cell: recover the actual input failure

History `Logs/G08_R2_CompleteCells_20261002_174806_127.tsv` contains49520 records, zero dropped. Its header incorrectly names first coast failure(11,5), because the observer was frozen only in the coast branch. The runtime receipt already identifies the earlier input visibility failure(11,6). The complete unfrozen history still contains that cell: `outputs/G08_R2_D45_InputFailureCell_20261002.json`.

| Cell(11,6) event | Unity frame | Monotonic seconds |
|---|---:|---:|
| Hard demand admitted, terrain queue rank12 |6298|144.922441|
| Same-frame reprioritization, rank2 |6298|144.928259|
| Terrain dispatch, rank0/work1091 |6325|146.472973|
| Collider ticket registered |6326|146.536056|
| Collision-ready terrain owned |6328|146.629469|
| Content/ecology work underway |6330|146.732775|
| First observed visible membership, content/ecology incomplete |6335|147.018004|
| Semantic content owned |6336|147.075888|
| Activation complete before visual publication |6342|147.443723|

Terrain queue delay1.550532 s; complete publication occurs approximately0.425719 s after observed exposure and2.521282 s after admission. A queue age alone does not prove lost work or an incorrect priority; the prior scheduler/reservation repair loop remains exhausted and is not reopened by this snapshot.

Observed maximum owners250, physical demand256, painters3, collider tickets2. These maxima are bounded-run observations, not independent lifetime/resource certification.

## Throughput limitations and deficit

`outputs/G08_R2_D45_CompleteCells_20261002.json` uses strict actual visual publication, including root activation. Hidden offscreen prepared cells are not counted as published. Demand classes overlap and cannot be added. This metric alone does not establish sustainable stage capacity.

| Phase/class | New unprepared demand | Useful unique completions | Pending start→end | Required/observed published cells per second |
|---|---:|---:|---:|---:|
| Input visible,2.475608 s |5|3|0→2|2.020 /1.212|
| Input turn |6|0|0→5|2.424 /0|
| Input swept |0|0|0→0|0 /0|
| Coast visible,9.335555 s |43|39|2→4|4.606 /4.178|
| Coast turn |26|0|5→10|2.785 /0|
| Coast swept |5|4|0→0|0.536 /0.428|

Input visible starts with30 prepared cells, coast visible with29 of31. Input predictive demand has283 unique entrants/134 unprepared, with94 discarded pending memberships: churn and overlapping classes prevent treating this as intrinsic required capacity. Physical pending backlog93→100 in input and100→174 in coast. Coast has42 observed incomplete visible cells; collision readiness remains separately assessed.

## P22 observer correction and outstanding gates

The first-failure bug is fixed in the input branch of `YQSemanticChunkRuntimeVerification` and made idempotent in `YQPlayerFollowingSemanticChunkStreamer.FreezeAppearanceBudgetObservation`. It does not alter production demand, work scheduling, movement, content or acceptance thresholds. Edit-only regression17:53:46 UTC, MVID `a61ae623-9236-4811-917a-23e18e84520b`, passes no-failure/no-freeze, input freeze, later coast cannot replace any target or prefix, and inactive disposable fixture cleanup. Receipt: `Logs/G08_R2_P22_FirstFailure_20261002.sentinel.tsv`. Live first-failure retention on this corrected source remains to be exercised.

The last clean baseline remains P21:89 ms input/60 ms coast, both above50 ms; coast ecology incomplete and input route invalid. P22 is diagnostic correctness, not a performance repair attempt. Hash repair count remains two; scheduler/reservation loop exhausted; other counters unchanged. First clean complete focused PASS, independent repeat and missing/affected full R2 rows remain outstanding.
