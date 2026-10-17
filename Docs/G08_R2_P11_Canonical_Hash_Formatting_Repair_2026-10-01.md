# G08-R2 P11 — canonical hash formatting allocation

**Narrow regression PASS; clean focused FAIL (56 ms input / 61 ms coast). Full R2 incomplete; R3 gated.**

## Cause and scope

[D29 saved native attribution](G08_R2_D29_Bounded_Allocation_Stacks_2026-10-01.md) resolves44 sampled allocation stacks to repeated accepted-blueprint hash formatting. Baseline diagnostics and builder callbacks own207900B/194424B in matched coast4926/native2115; their18.308/15.098ms include allocation-stack overhead. This proves actual production allocation, not clean speed or a driver fault. Latest clean P10 coast maximum53ms exceeds50ms by3ms. Exact hash formatting is the single active repair boundary; exhausted scheduling/reservation loop remains unchanged.

Production change: `Assets/Assets/Scripts/Generated/YQSpatialBlueprintV2Validation.cs`,33added/8removed lines. Float round-trip invariant tokens, signed invariant integers and current-culture length prefixes format into stack spans and feed the same length-delimited UTF-16 FNV character stream. String/null tokens, sorting, full current-content hashing on every resolver lookup, acceptance/validated evidence, mutation detection, schema/content hash, generation order and preparation authority remain unchanged. Original string formatting remains a buffer-failure fallback. No public API, serialized field, asset, save, resource limit, movement/view or scheduler change.

Verifier adds one explicitly armed Edit-only fixture and frozen original hasher reference inside UNITY_EDITOR; no live profile/world preparation. Clean witnesses never arm this fixture or the prior bounded-stack marker. Source before snapshots and fixture script are under outputs/G08_R2_D30_* and outputs/G08_R2_P11_HasherBefore_20261001.cs.txt.

## Compilation and identity

Fresh Unity Tundra build success4.22s, reload complete, native Console0errors and known unrelated CS0162 warning. Main DLL time14:00:16UTC. Final CSharp MVID `9e96b945-04ef-47ff-bccf-8284f593d1a7`, DLL SHA `3B350B3B4EC19B9EFFF672DDB55E1728DBABE2436758FF5BE9CD848141EFFE09`; Editor DLL SHA `B459B64BB4344FB850BE9EC9475E12C4103DC1FB0F7363BA2390428DFDD4C77A`. Hasher SHA before `1043B230105DFEF5FF68D8A04183BE5C5751C02DA8C36B7CA0F089AE63F0272F`, after `D7F12141E364D99E88A42D0A65305C4CB5B028D7ADAA24FD09F8F2A038C60BD1`; verifier `F5817C1E90BB5624D6A31AA1E5678CCF20173AC5CB3EBBCF24F75C1CFF01B14F`. `outputs/G08_R2_P11_Compiled_20261001.json` records complete identity and idle Edit status. Scoped patch/diff check PASS. No source edit or compile in Play.

## Matched narrow regression

Detached immutable accepted fixture: outputs/G08_R2_AcceptedTerrainAllocationFixture_20261001.json, world seed4bb221dc/V2c1d6ce9d3e06f760. **8288 cases PASS** before and after: invariant/en-US/fr-FR/ar-SA cultures;2048 seeded float-bit/signed-enum inputs per culture;NaN/infinities/subnormals/min/max/round-trip edges;null record/blueprint/collections/entries, UTF-16/delimiters, full accepted artifact, reordered unique-ID lists and changed-content detection. Exact serialized artifact/file bytes and live singleton identities unchanged. Actual current accepted hash remainsc1d6ce9d3e06f760. No resolver cache bypass or altered validation frequency.

Before **13:59:39.7159593UTC**, MVID92c07563-41fa-4b35-ab04-42cf460ab6d5. Current8hashes7.070500ms/mean0.883813; reference7.364000/0.920500. After14:01:08.2862088UTC, final MVID, current4.931200/0.616400 versus same-capture reference7.311000/0.913875. Setup/report/serialization excluded from named scopes.

Native before raw135939_282/GUID5e4e3a47-bafd-4109-80b7-01c109522987/frame2360/Unity23: reference and current each23904events/**777344B** for8hashes. After raw140107_822/GUID05e87bfb-e65a-4b15-bce7-de137328ae98/frame2406/Unity27: reference still23904events/777344B; current2960events/**204016B**. Reduction573328B (**73.75%**) across8actual complete hashes, not zero allocation. Native scope7.221→4.929ms in same after capture; independent before-current7.017ms. Full fixture allocation includes JSON construction/test work and must not be treated as runtime cost.

Logs/G08_R2_D30_HashBefore_20261001.blueprinthash.tsv and ...P11_HashAfter... retain parity/timing/build; native exports matched1frame each. outputs/G08_R2_D30_HashBefore_Allocation_20261001.json and ...P11_HashAfter_Allocation...json count disjoint native allocation events with no unknown sizes. Full native captures/exports are diagnostic only, not focused certification.

## Next acceptance

Narrow evidence supports one unchanged clean production PlaySafe/title/Continue focused witness on final source, profiler/binary/callstacks/deep/high-volume history/GPU/motion override disabled. Preserve original2.45s input/full normal coast, no extra preparation/cache clear/Editor restart. Runtime receipt and row ledger will determine whether3ms residual and complete-cell deadlines close. No independent repeat before one full clean focused PASS; full R2 rows remain unverified.

## Clean focused result

[Immutable clean receipt](G08_R2_Speed260_2026-10-01_140529_P11_Clean_Receipt.md),14:05:06.5836721–14:05:29.1587195UTC, same final source/DLL/MVID and accepted profile/world/seed/V2 above. Ordinary Continue, accepted terrain restore and initial512m/7cell preparation envelope PASS. Original input/full normal coast, no profiler/binary/callstacks/deep/trace/history/GPU/motion override, zero unfocusedframes in both phases.52externalPlay observations show no compilation and watcher inactive. ReturnedEdit and all five recorded source/DLL hashes match arming. No C# edit after compile or during Play. Native returned-Edit screenshot retains four known runtime collider errors; no new exception/save failure observed in inspected tail.

| Phase | Samples | Median / P95 / maximum wall | Result |
|---|---:|---|---|
| Input |75|31 /42 /56ms|FAIL (6ms over50ms)|
| Normal coast |357|28 /39 /61ms|FAIL (11ms over50ms)|

Coast10.588s/1333.7m, settled0.6m/s, restored28/34. Ordinary walk/run/dash/reversal/diagonal/rapidturns, abrupt6→260 transition/boundary/direction/constantinput checks, visible/turn/ground readiness PASS. Coast required visible content remains complete; zero fallback attempts/traversalblocks/recovery during movement. Composite CoastCoverageGate FAIL is timing despite ground/visible true. Input maximum4854 reports streamer28ms/contentDispatch16ms and no collection counters; coast5137 reports streamer6ms/Late4ms and collection16ms/counters1/1/1. These recorder arrivals do not prove exact native-frame ownership.

P10→P11 clean maxima44/53→56/61ms; narrow hashing allocation/time improved, overall measured maxima worsened. Actual demand/coast trajectories differ (P10coast1183.3m/8.547s), so no isolated overall timing-effect estimate. Count **one unsuccessful focused closure attempt** for the newly measured scalar hash-formatting cause; narrowPASS does not close it. No unchanged clean retry/independent repeat. Exhausted scheduling/reservation loop unchanged.

Terminal census5229:physical206/reservations218/cap256,8physical without reservation/20reservations without physical,active84/retained103/unloaded96. Sets overlap and include authored ownership; no additive total or resource-lifetime PASS. Worlddelta cancelled10.69s; completed production latency not verified.

## Current R2 rows

| Required row | Status on final P11 | Evidence / limit |
|---|---|---|
| Accepted-save normal startup / initial envelope |PASS for this witness|Ordinary Continue/accepted restore/full envelope|
| Cold startup |NOT VERIFIED|Not substituted by accepted-save restore|
| Ordinary controls / direction changes |PASS focused|Actual production motor matrix|
| Distinct6→260 stress / abrupt crossing |PASS controls;FAIL timing|Input maximum56ms exceeds50ms|
| Normal coast content / ecology / appearance / ground |PASS focused|Every-frame full visibility, zero fallback|
| Coast maximum frame |FAIL|61ms exceeds50ms|
| Full visible / turn readiness |PASS focused|Original view/demand/input|
| Extended sustained travel / swept / lookahead matrix |NOT VERIFIED in full|Focused witness is not full R2|
| Bounded resource lifetime |NOT VERIFIED|Terminal overlapping census only|
| Deliberate stall / honest recovery |NOT VERIFIED on final dependency|Separate required row|
| Natural unload / revisit |NOT VERIFIED on final dependency|Separate required row|
| First whole clean focused PASS |FAIL|All phase gates required|
| Independent clean repeat |NOT VERIFIED|First whole PASS missing|
| Full R2 / R3 admission |FAIL / gated|No current-source full matrix proof|

Next named question: same-interval native ownership of finalP11 input/remaining coast maxima and representative overbudget frame, especially production contentDispatch versus allocation/collection/editor/native work. One original-window binary-only diagnostic through ordinary Continue, no stack/history/GPU/movement overrides, analyse actual IDs before another patch; do not assume4854/5137 repeats or assign recorder values to them. No scheduler change follows from this content-ready clean run.
