# G08-R2 D28 — remaining coast frame ownership

**Diagnostic only. Latest clean authority remains P10 FAIL; full R2 incomplete and R3 gated.**

## Capture and conditions

Question: identify remaining coast wall/native cost on final P10 source and the location reporter after automatic-save IO repair. One original-window binary-only capture, 13:40:37.3317671–13:40:58.3083002 UTC, normal PlaySafe/title selected-save Continue, original input/full normal coast. [Immutable receipt](G08_R2_Speed260_2026-10-01_134058_D28_BinaryOnly_FAIL.md). No code changes, extra preparation, altered controls/camera, cache clearing or Editor restart.

P10 CSharp MVID `9f0bcd17-6b6e-4118-a0f7-6416d1a2b5a7`, DLL SHA `29B96B074ED3DB1D3975A099F65D63EBAAC3BCC75332EAD60EA361F6368EDBB6`; verifier SHA `D2EA17A906BFC6762C0471C9F6648EA60DC897E69E44BF105168451208205BE4`; streamer SHA `7EAB4A940ADB6D33E3E1E6F9687381FDC5DE911E1285C0D1908541827961AE7C`. Complete arming/source/Editor DLL record: `outputs/G08_R2_D28_Armed_20261001.json`.

Unity 6000.3.2f1 / DX12, same user-opened PID28040, 1278×637 / scale0.79 / Ryzen5800X / RTX5060. Profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted/validated V2 `c1d6ce9d3e06f760`, schema7/topology9/gate2. Owner256/view512m and every content/publication stage preserved. Profiler enabled only at the existing stress boundary; cell history, allocation call stacks, deep profiling, GPU observation and motion override off. Zero unfocused frames in input and coast. Native captures showed ordinary loading and returned Edit; external status recorder `outputs/G08_R2_D28_WatchStates_20261001.jsonl`.

Raw `Logs/G08_R2_FrameTrace_20261001_134046_832.raw`, 268571559 bytes; metadata GUID `c851ebd3-9f7c-4f3a-8098-e3b674976474`. Default metadata export matched356frames /355paired native boundary intervals. Raw timestamps establish overlap; recorder arrival alone does not.

## Phase results and limits

| Phase | Frames | Median / P95 / max wall |
|---|---:|---|
| Input |81|29 /40 /47.513ms|
| Coast |275|30 /44 /56.297ms|

Coast8.831s/1235.0m, original restored acceleration/deceleration28/34, settled0.7m/s. Complete visible/turn/ground checks passed with zero fallback/intervention/recovery. Profiled abrupt transition/directions/constant segments failed (first boost227.3m/s Unity versus812.9m/s wall, actual direction failure); diagnostic trajectory is not clean certification or matched capacity proof. Binary overhead is included. No independent clean repeat is authorized by this result.

## Matched intervals

`outputs/G08_R2_D28_RemainingFrameCost_20261001.json` and `outputs/G08_R2_D28_AllOwners_20261001.json` preserve actual boundaries, clipped full paths, native thread IDs and allocation callers. Saved exports: `Logs/G08_R2_D28_InputMax_20261001.samplethreads.tsv`1691:1692; `...CoastMax...`1810:1811; `...CoastRepresentative...`1972:1973; `...CoastAppearanceGC...`2029:2030. Each matched2frames; no new capture for these exports.

- Input maximum Unity4828/native1692, wall47.513/native boundary48.1339ms. StreamerUpdate14.085ms, content-start7.021ms nested, Late2.927ms; no collection. Do not sum nested scopes. Height worker570348B/6events and decorative callback157262B/2058events occur inside the explicit interval; Console/status UI allocation is also present.
- Coast maximum Unity4947/native1811, wall56.297/native57.0315ms. GC12.866ms is wholly inside the interval under `EditorApplication.update: UnityEditor.LevelPlay.LevelPlayInstallMenuItem.UpdateMenuItem`; this path does not establish an explicit forced collection or implicate a package defect. StreamerUpdate8.217ms, decorative2.247ms, Late1.853ms. Ground is not the maximum's dominant scope. StreamerUpdate466296B/11102events; decorative383614B/6014events. Shared GC pressure cannot be attributed to one caller merely from its collection site.
- Representative slow coast Unity5109/native1973, wall54.462/native55.3694ms. Motor14.266ms includes camera-ground admission13.161ms; no collection. That exact ground scope owns1070708B/25477allocation events. Decorative383668B/5997events; thread-pool unnamed callers retain their recorded IDs and are not assigned to an assumed task.
- Coast Unity5166/native2030, wall52.867/native53.6727ms. Painter22.291ms includes appearance-slice21.291ms and GC15.546ms; do not add them. Appearance74570B/12events in this interval; these are allocating callers, not proof of which bytes caused the collection.
- Location reporter exported periodic calls Unity5103=1.167ms and5155=1.537ms. Smaller callbacks are filtered by the default export's1ms cutoff. D26 had10–23ms calls before P10; different profiling/history/trajectory conditions prevent an isolated timing-effect estimate. The actual runtime cost now supports the narrow automatic-IO repair, without claiming zero serialization/allocation.

Render-thread waits overlap production work; their names are not GPU/driver attribution. Managed zero allocation counters remain invalid. Exact native durations and inclusive scopes are not substitutes for clean maximum gates.

## Investigation boundary and next action

Camera-ground admission directly calls full hard-view refresh, semantic-owner admission, safety-ground queue maintenance and live collider/publication checks. Source also contains allocating three/four-argument integer `Mathf.Max` capacity/retention getters. Neither their presence nor the whole scope's allocation proves which source dominates. No production patch was applied; the exhausted scheduler/reservation loop is unchanged. Do not change ordering, limits, readiness or generation from this diagnostic.

The remaining named question is the exact allocation call sites inside a coast camera-ground/admission pass, and whether they are redundant arithmetic/observer work or accepted semantic materialization. One further bounded diagnostic will enable allocation call stacks only for the first24coast frames (at most1second after activation), using the existing binary capture and ordinary unchanged input/full coast. Exact start/stop frames are recorded and settings restored on every exit. Analyse actual captured allocating calls before any production repair; no assumed repetition of5109 or broad unchanged clean retry.

No first full clean focused PASS, independent repeat or full R2 matrix exists. R1 was not recertified and R3 was not run.
