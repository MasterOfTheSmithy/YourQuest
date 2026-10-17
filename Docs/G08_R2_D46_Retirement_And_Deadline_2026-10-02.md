# G08-R2 D46 — retirement result and remaining deadline

**Diagnostic only; full R2 remains FAIL/incomplete.** Run18:23:53.3108837–18:24:27.2758458UTC, ordinary PlaySafe/title/accepted-fixture Continue, runtime MVID`f99d8b98-6f7a-480f-bfad-fcfe52f4d404`. Nine source/DLL hashes match after idle Edit return; no observed compilation in Play. `outputs/G08_R2_D46_ReturnedAudit_20261002.json`. Existing application focus status caught Inspector focus at title; one supported Game-view click restored focus before Continue or world preparation. No focus input occurred during measurement.

Receipt: `Docs/G08_R2_Speed260_2026-10-02_182427_D46_Background_Diagnostic_Receipt.md`. Staging17/17 in12.733s passed; full coast1124.110m/9.330s settled without obstruction/fallback/traversal block/recovery. All measured frames focused. Profiler/history overhead changed turn timing; route INVALID_ROUTE, input/coast maxima73.482/71.060ms. These are not clean certification numbers. Latest clean baseline is P23F63/84ms, valid route, full visibility FAIL.

## Retirement hypothesis resolved

Raw `Logs/G08_R2_FrameTrace_20261002_182413_757.raw`, GUID`41cfb537-bca9-4301-94cb-ca8e10b52e0f`. All255 input/coast intervals paired. Saved Input1718:1720, Coast1770:1772 and Representative1807:1809 exports each matched three frames. Exact saved boundaries and native scopes: `outputs/G08_R2_D46_FrameCost_20261002.json`, `outputs/G08_R2_D46_NativeOwners_20261002.json`.

D46's comparable second-turn native1719/Unity8148 contains **four direct Destroy calls totaling0.344ms in a0.36ms parent**, compared with D45's319/28.740ms and D43's322/34.706ms. P23 removes the demonstrated mass-retirement burst. This local native comparison does not establish an overall clean frame PASS or constant cost for every hierarchy. Source resource limits are unchanged. P23's first complete clean closure remains unsuccessful because other frame/readiness gates fail.

## Remaining frame owners

| Interval | Wall | Exact retained ownership |
|---|---:|---|
| Input max Unity8149/native1720 |73.482ms|No GC. Streamer Update11.343ms/Late8.737ms; motor1.808ms. Previous native1719 ConsoleWindow.Paint includes8.048ms main-thread semaphore wait. Editor status callback4.291ms.|
| Coast max Unity8201/native1772 |71.060ms|No GC. Motor17.831ms, streamer Update11.390ms/Late6.944ms. Render waits overlap CPU work.|
| Representative coast Unity8238/native1809 |52.856ms|No GC in flat trace. Motor16.655ms, streamer Update8.390ms/Late5.871ms.|

Only recorded GC collection in the capture is1.042ms at Unity8393/native1964, wall44.749ms; this capture does not reproduce P23F's clean84ms ownership. Do not use D45's earlier GC to assign the new clean maximum. Inclusive parents/children and waits are not added.

Input interval has1221448B/5671 retained allocation events, including317480B in two Console text-layout branches. Coast has1107389B/10172 events;429472B/4872 leaves directly under motor's current scope. These byte counts locate scopes, not full managed callsites. `outputs/G08_R2_D46_AllocationScopes_20261002.txt` retains grouping. D44 pre-P21 sampled callsites are not automatically the current motor byte census.

## Correct first-failure history

`Logs/G08_R2_CompleteCells_20261002_182427_195.tsv`:48594 records, dropped0, frozen prefix24636, actual first failure **input(11,6), Unity8185,time182.376773**. Later coast(11,5) did not replace it. This is live evidence for P22's first-failure fix.

Cell(11,6) admitted180.310009/frame8147, terrain rank11→2. Terrain dispatched181.857738/frame8175 (1.547729s later), collider ticket181.860000, collision-ready terrain owned181.964876/frame8177. At failure, appearance ready; structures only6 steps/0.183ms with3 budget-denied frames, ecology1 step/0.003ms, content owner1140/ecology1141, no content preemption. Complete activation182.728413/frame8192, **351.640ms after failure**, total2.418404s after admission. At completion structure16 steps/3.819ms, ecology7/5.690ms; counts do not by themselves justify reopening the exhausted scheduling/reservation loop.

Resource observations: owners253, physical demand256, painters3, collider tickets2. Terminal/current census is not lifetime certification. Analysis `outputs/G08_R2_D46_CompleteCells_20261002.json` measures strict actual visual publication, so hidden prepared offscreen owners are not counted published and classes overlap. Input visible new-unprepared6/completed3, pending0→3; coast40/35, pending3→7 and42 observed incomplete visible cells. Input turn7/0 pending0→6; coast23/0 pending6→10. Input swept has no new unprepared cells; coast1/1 pending0→0. Predictive input130/4 pending0→46, coast47/35 pending46→57. These are observed published throughput/backlogs, not sustainable stage capacity estimates.

## Next bounded measurement condition

The captured Console repaint is unrelated Editor presentation overhead, yet currently part of the measured wall interval. Select the existing Project tab in the same bottom dock, preserving Game viewport616x264, scene, quality, all logging, controls and production source. Verify focus at title, then one unprofiled witness. Record this changed Editor-pane condition separately from code identity; do not claim an isolated production speedup or rerun unchanged for a lucky PASS. If it fails, use its exact remaining evidence before another change. Required full focused PASS, independent repeat and affected R2 matrix remain outstanding; R3 stays gated.
