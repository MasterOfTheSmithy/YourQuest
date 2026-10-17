# G08-R2 D26 — saved frame ownership and exposure history

**Status: INVALID certification; diagnostic findings only. Full R2 remains FAIL.**

The [immutable replay receipt](G08_R2_Speed260_2026-10-01_130859_D26_FrameAndAppearance_FAIL.md) covers 13:08:36.1379335–13:08:59.4735395 UTC. Binary profiling and bounded cell history were enabled; deep profiling, allocation call stacks, GPU observation and movement overrides were disabled. All 60 input frames recorded applicationFocused=false. This invalidates certification and matched performance comparisons, regardless of the Editor screenshot or title. The initial arming record preceded recovery from a minimized/resized window; recovery restored 1278x637/display 0.79 before measurement. No source, movement input, accepted content, owner limits, OS cache or Editor process was changed for this replay.

The latest clean authority remains [P09 FAIL](G08_R2_Speed260_2026-10-01_113944_P09_Clean_FAIL.md). D26 does not replace it. Native PID28040/Unity6000.3.2f1/DX12/Ryzen75800X/RTX5060/32648MB; source streamer SHA256 7EAB4A940ADB6D33E3E1E6F9687381FDC5DE911E1285C0D1908541827961AE7C, verifier FE6C3BEB1A91F9D7F1EA41088DFE558D83124EB8EDFF03814F66FDEC9B8FD7A9, CSharp DLL D03507A0A2C74795EE06F5C92B60BC8AF674CD86F7DD3481BFB3D95F8CDDADF2/MVID528b4f05-4a05-4cd1-a7d1-04e516a026e3. Profile3ccfff4df9a1479884061e51940e4457/world yq-world-0284d4409a720743c13eb56e/seed4bb221dc/accepted and validated V2 c1d6ce9d3e06f760 remain unchanged. Save schema7, topology9/gate2.

## Same-interval native ownership

Raw `Logs/G08_R2_FrameTrace_20261001_130845_781.raw`, 245702964bytes, GUID21531ed7-18f7-44ff-8bd4-e4abe47a5424. 310metadata frames, 309 intervals with both explicit boundaries. Saved all-thread exports: Input1236:1237, CoastMax1487:1488, CoastGC1475:1476; each matches two native frames. Analysis `outputs/G08_R2_D26_RemainingFrameCost_20261001.json` and `outputs/G08_R2_D26_AllOwners_20261001.json` clips native scopes to the explicit witness boundaries. Flat/inclusive ancestors overlap and are not added. Export filters CPU samples below1ms; GC.Alloc events and sizes are retained. Worker IDs are preserved. Allocation ownership does not identify which bytes caused GC.

| Witness interval | Wall | Demonstrated scope inside interval | Limit |
|---|---:|---|---|
| Input Unity4754/native1237 |61.277ms|Streamer Update15.625ms; content start7.833ms nested; location reporter10.423ms; no GC|Diagnostic, unfocused; not clean timing|
| Coast Unity5005/native1488 |88.591ms|Location reporter22.949ms; no GC|Diagnostic; no driver attribution|
| Coast Unity4993/native1476 |75.510ms|DecorativeScatter coroutine31.953ms includes GC21.023ms|Do not add the nested collection to its parent|

The location reporter allocated593events/121170bytes in input maximum and593events/121158bytes in coast maximum. The full saved trace also records reporter calls10.960ms atUnity4804,10.515ms4860,10.875ms4913,11.680ms4960 and11.101ms5050. Its2second cadence and direct call chain are production behavior: `PlayerLocationReporter.Update` calls `PlayerStateManager.SetLocation` and `WorldStateManager.SetCurrentRegion`, each invoking full normalization; world normalization recursively walks the accepted plan and re-registers identities using linear searches. This identifies a separate owning boundary to isolate in an inactive, detached Edit fixture; it does not yet prove which inner operation dominates or justify weakening state validation. No production repair follows from this document.

Other recorded allocation owners include opt-in history beneath ground/streamer work, Console text generation, named height workers and unnamed pool workers. History overhead prevents assigning those costs to clean P09. Render waits do not prove a GPU or driver defect.

## Complete-cell history

`Logs/G08_R2_CompleteCells_20261001_130859_421.tsv`:46914records, dropped0, ignoredNoOwner292; actual first failure(13,0), frozen prefix30758. Analyses: `outputs/G08_R2_D26_Readiness_20261001.json`, `outputs/G08_R2_D26_FirstFailure_20261001.json`. Class bits overlap; rates cannot be added or treated as sustainable architectural capacity. Complete denotes live publication; stageReady is reported separately. Offscreen turn/physical publication counts can include deliberately hidden renderers. Pending episode latencies are observed lower bounds, not admission-to-deadline guarantees.

| Coast class,10.219898s | Prepared initial | New demand / newly unprepared | Useful unique completions | Pending start→end | Observed incomplete class cells | New-unprepared / useful rate |
|---|---:|---:|---:|---:|---:|---:|
| Visible |29|75/23|23|0→0|18|2.251/2.251 per second|
| Turn |17|58/31|6|0→11|31|3.033/0.587|
| Swept |1|16/1|1|0→0|1|0.098/0.098|
| Predictive |0|199/117|30|0→66|117|11.448/2.935|
| Physical |162|213/213|39|94→144|306|20.842/3.816|

Input2.479345s starts visible28/turn13/swept1 fully prepared; new visible5/turn16/swept2 all preprepared and zero observed misses. Predictive new287/unprepared137/completions4/final pending0 reflects changing membership, not completion of all demand. Physical initial256/prepared154, new94/unprepared88/completions8/pending102→94. Visible coast pending episode latency23samples median0.212113/P950.689290/max0.793210s; swept one0.283352s; predictive30samples median6.163117/P958.940935/max10.170617s. Eventually matching the visible average did not prevent exposure misses. 224cells have observed epoch transitions/preemptions; that is not224cancellations or automatically lost work.

## Actual first failed cell(13,0)

| Event | Unity frame | Time seconds | Owner evidence |
|---|---:|---:|---|
| Hard admission |4811|118.636493|content epoch1/terrain epoch1|
| Physical/predictive observation |4811|118.658616|prediction4.0615s|
| Collision ready |4868|120.647672|terrain publishing work1040|
| Content/overlay ready |4892|121.581902|content epoch4|
| Ecology ready |4895|121.711887|appearance still absent|
| Appearance work267 observed |4904|122.025859|content epoch4|
| Activation complete |4917|122.608322|appearance detailPayloadWorker unfinished|
| Capacity preemption |4918|122.656540|request(10,-2) arrival0.3692/victim0.7385; target appearance work267|
| Painter registration removed |4918|122.656567|target appearance work0|
| First observed visible membership |4927|123.059239|appearance false/work0; other required stages true|
| First failed assertion |4927|123.073902|appearance false,23denied frames|
| Later live complete |4934|123.385167|appearance ready; complete true|

Hard admission to exposure4.422746s; physical observation to exposure4.400623s; collision ready to exposure2.411567s; painter preemption to exposure0.402672s; exposure to later completion0.325928s. Prediction at failure remained0.7385s and is not the actual exposure lead. The lost registration is observed; exact discarded worker CPU/result bytes and counterfactual completion without preemption are unproven. Mid-pass owner counters are not complete resource censuses.

This belongs to the already exhausted scheduling/reservation loop. P02 already changed equal-arrival destructive preemption to strict earlier arrivals. D26 is not permission to rename the same cause or add another scheduling patch. A genuinely distinct payload-cost repair requires new owning evidence.

Terminal censusUnity5060:199live physical/256reservation owners/cap256,8physical without reservation/65reservations without physical. Sets overlap; no additive owner total or lifetime certification. Original coast10.226s/1423.4m, acceleration28/deceleration34, input released and settled0.4; diagnostic focus and first-boost/trajectory validity prevent certification.

## Next bounded action and outstanding gates

Use a detached inactive Edit fixture to separate actual player-location, world-region, world-collection and identity-normalization scopes, retaining accepted-plan identity and persistent state. No additional Play capture is needed for this attribution question. First clean focused PASS, independent repeat and full R2 rows remain missing: cold startup, sustained ordinary travel, full swept/turn/lookahead deadlines, resource lifetime, deliberate stall recovery and unload/revisit. R3 remains gated.
