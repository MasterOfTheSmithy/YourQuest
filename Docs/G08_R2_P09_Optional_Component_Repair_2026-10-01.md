# G08-R2 P09 — optional component lookup repair

**Status: narrow regression PASS; clean focused FAIL. Full R2 incomplete; R3 gated.**

## Cause, scope and preserved contracts

[D25 attribution](G08_R2_D25_Remaining_Frame_Cost_2026-10-01.md) records Editor missing-component allocations in motor/streamer/verification callbacks and a13.225ms collection beneath one allocating lookup. This is a distinct allocation cause, not a new scheduler/reservation attempt. It did not establish the cause of D25's71.607ms maximum, which had no collection.

`YQPlayerFollowingSemanticChunkStreamer.cs` changes five optional queries: live-ecology renderer validation, renderer-cache discovery, collision-preserving owned-object activation, terrain renderer gating and complete visual gating. `TryGetComponent` avoids Editor missing-component allocation while preserving missing/present component classification. Every live renderer/terrain enabled and hierarchy check remains. No public API, cache invalidation, publication stage, activation budget, lifecycle ordering, owner/concurrency/queue/cache limit, deterministic output, seed/artifact, save/schema, asset/GUID, camera/control/input, or50ms gate change.

`YQSemanticChunkRuntimeVerification.cs` adds only the explicitly requested Edit diagnostic `optional-components-traced` and a transient fixture. This does not prepare the accepted world, change runtime acceptance or run in normal gameplay. Source before this patch is preserved in `outputs/G08_R2_P09_{StreamBefore,VerifierBefore}_20261001.cs.txt`; unrelated dirty changes are retained.

## Compile and narrow proof

Fresh main compile11:35:07.2916527UTC, Editor11:35:08.2875615UTC; zero compiler errors. One existing CS0162 warning inYQWorldGenerationV2ContractTests.cs:385 was inspected. The first offline extraction mistakenly requested native frames0:1, received no matching identity and logged an explicit analysis error. Corrected saved range1184:1184 matched; no capture or regression rerun was needed. Both diagnostics were preserved before clearing Console for fresh runtime observation.

`Logs/G08_R2_P09_OptionalComponents_20261001.optionalcomponents.tsv`:42cases PASS. Actual private renderer predicate exercised with cached and live ecology, absent/present/destroyed roots, inactive hierarchy, disabled renderers/terrain, full hide/show. Terrain collision remains ready with disabled renderer, rejects disabled collider/inactive hierarchy, and optional collider classification matches the old branch.

Native fixture trace: `Logs/G08_R2_FrameTrace_20261001_113532_008.raw`,14885776bytes; GUIDf763d264-17e7-46a6-9483-ef013ad030a4; native1184/EditUnity11. Saved `Logs/G08_R2_P09_OptionalNative_20261001.samplethreads.tsv` matched1frame. `outputs/G08_R2_P09_OptionalNative_20261001.json`:

| Scope | Missing-component events | Missing-component bytes | All allocation bytes |
|---|---:|---:|---:|
| Old optional Terrain reference,21absent roots ×128 |2688|1624320|1731840|
| Actual repaired live-ecology readiness predicate ×128 |0|0|2176|

The remaining128events are fixture reflection/return overhead. The reference measures old optional lookups; the repaired scope runs the whole actual predicate. This proves removal of that allocation path in the fixture, not an isolated CPU callback timing effect or global runtime allocation freedom. Managed allocation zeros are not used. Profiler off after two deferred Editor callbacks; prior log/binary configuration restored. All transient objects/data removed.

## Clean focused result

[Immutable P09 receipt](G08_R2_Speed260_2026-10-01_113944_P09_Clean_FAIL.md),11:39:22.9228026–11:39:44.3638710UTC. Same PID28040/Unity6000.3.2f1/DX12/Ryzen75800X/RTX5060/32648MB;1278x637/display0.79 restored before run. Ordinary PlaySafe/title/Continue, accepted-save terrain restore and initial complete envelope PASS. Profiler/binary/callstacks/deep/history/GPU/motion override off; both phases zero unfocused frames.55external Play snapshots show watcher inactive; returned Edit watcher resumes, no compilation/import/refresh pending. Final source and DLL hashes unchanged after runtime.

| Phase | Frames | Median | P95 | Rounded maximum | Gate |
|---|---:|---:|---:|---:|---|
| Original2.45s input |79|29ms|42ms|59ms|FAIL |
| Full normal coast |338|26ms|36ms|50ms|FAIL, actual predicate over50ms |

Unlike P08's input-only ambiguity, P09 has a separate coast frame gate explicitly FAIL. Rounded50ms does not make this a PASS. Input maximumUnity4797:59ms wall/20ms streamer, contentDispatch10ms, no collection-counter increment. Coast maximumUnity5128:rounded50ms wall/13ms streamer/Late lifecycle6ms, no collection increment. These are independent recorder observations, not matched native attribution.

Ordinary controls, abrupt6→260transition, first-boost direction, boundary crossing, input full visible/turn coverage, ground and constant-input/direction checks PASS. Zero synchronous fallback, traversal intervention or fall recovery. Coast9.383s/1290.3m, ordinary acceleration/deceleration28/34, released input and settled; ground PASS, complete visible FAIL.

First coast miss cell(7,11), snapshotUnity4959: appearance incomplete with live painter; other required flags/snapshot retained in immutable receipt. Painter age0.613s, biome19deniedframes/0.572s and detail20/0.579s; no demanded-cell history was enabled. These counters do not prove starvation or exact exposure lead. Previous ecology callback sample records18.522ms inSpawnSmallScatterAreaRoutine canopy cell(3,13); it is not native allocation/CPU ownership proof for the59ms maximum.

Terminal census191livephysical/255reservations/capacity256;8physical without reservation/72reservations without physical; overlap, not an additive count. Lifetime/resource row still unverified. Height588executed/538success/51cancel/0fault at49.5/s does not measure useful complete-cell throughput. Runtime Console4errors and many terrain-readiness outlier warnings retained; model worlddelta cancelled9.53s at shutdown, not a completed-latency result.

## Identity and comparison

`outputs/G08_R2_P09_Armed_20261001.json`, `...Build...json`, `...WatchStates...jsonl`, `...ReturnedEdit...txt` preserve arming/build/status.

- CSharp executingMVID528b4f05-4a05-4cd1-a7d1-04e516a026e3; DLLSHAD03507A0A2C74795EE06F5C92B60BC8AF674CD86F7DD3481BFB3D95F8CDDADF2.
- EditorDLLSHAE5328B3AD3FA3CA3D1100511F585DE3F0152289E6546013625FDA32BF5A66A22.
- StreamerSHA7EAB4A940ADB6D33E3E1E6F9687381FDC5DE911E1285C0D1908541827961AE7C.
- VerifierSHAFE6C3BEB1A91F9D7F1EA41088DFE558D83124EB8EDFF03814F66FDEC9B8FD7A9.
- Profile3ccfff4df9a1479884061e51940e4457; worldyq-world-0284d4409a720743c13eb56e; seed4bb221dc; acceptedV2c1d6ce9d3e06f760; schema7/topology9/gate2 unchanged.

P08→P09 clean medians/P95 coast26/38→26/36ms and maxima about50/56→59/about50ms. P09 travelled1290.3m versusP08's156.2m; coast exposure demand therefore differs substantially. This is not an isolated overall speed estimate or proof that the component patch caused the new appearance miss. No world/input settings were changed to force either trajectory.

## Repair ledger and next action

Count one unsuccessful focused closure attempt for the new optional-component cause, with semantic/native fixture PASS and remaining frame/readiness FAIL. Do not retry the same patch or reset the exhausted scheduling/reservation loop. First clean focused PASS and independent repeat remain missing.

Next bounded question: on final P09 source, what owns the same-ID input maximum and first missing coast cell's appearance deadline? Competing explanations: remaining synchronous/ecology/lifecycle CPU or another wait/collection; appearance gets inadequate useful lead versus loses admitted work. One original-window opt-in binary plus bounded demanded-cell history capture can distinguish them. Freeze the actual first failed cell; do not assume(7,11)repeats. Exit at original input/full coast; analyze paired maxima/representative intervals and unique demand/completions/backlog/latency/denials before another repair. No deep/allocation stacks/GPU/motion change, no special preparation or altered phase. Diagnostic results cannot certify performance.

## Current R2 rows

| Row | Status on P09 | Evidence |
|---|---|---|
| Accepted-save startup / starting envelope |PASS focused|Ordinary Continue receipt |
| Ordinary controls / abrupt260input / boundary/directions |PASS focused|Production motor receipt |
| Input complete visible/turn/ground / zero fallback |PASS focused|Every-frame checks |
| Input/coast maximum frame limit |FAIL|59ms / over50ms |
| Coast complete appearance/content |FAIL|(7,11)appearance |
| Independent fresh-session repeat |NOT VERIFIED|First clean PASS absent |
| Cold startup / sustained travel |NOT VERIFIED|Focused scope does not cover |
| Full swept/lookahead readiness |NOT VERIFIED|Narrow turn/ground is insufficient |
| Lifetime bounded resources |NOT VERIFIED|Terminal census only |
| Deliberate stall/honest recovery / unload-revisit |NOT VERIFIED on affected source|No new witness; do not import R1 proof silently |

Full R2 FAIL; R1 not recertified and R3 not run.
