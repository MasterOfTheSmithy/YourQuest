# G08-R2 P08 — suspend Editor source scanning during Play

**Status: FAIL. Watcher lifecycle regression PASS; clean focused coast timing FAIL. Full R2 and independent repeat remain incomplete; R3 gated.**

## Cause, change, and preserved contracts

[D24 allocation ownership](G08_R2_D24_Idle_Allocation_Ownership_2026-10-01.md) resolves the idle background allocations to Mono's recursive file watcher. Pausing the existing project watcher removed its file-metadata stacks while an in-progress directory recursion continued. This does not map historical native IDs across reloads or prove every remaining allocation came from that watcher.

`Assets/Assets/Scripts/Generated/Editor/YQEditorAutoRefreshBootstrap.cs` now creates its watcher inactive, suspends it before Play and throughout Play domain reload, explicitly disposes it on reload/quit, and resumes watching in Edit with one catch-up refresh for deferred edits. The existing balanced native auto-refresh hold and marker-driven startup/Continue/verification remain. `YQSemanticChunkRuntimeVerification.cs` adds only Edit diagnostic capture/extraction and an allocation-callstack configuration field to the receipt.

Player/profile/world owners, accepted state, deterministic terrain, schema, serialized fields/GUIDs, view512m/cell128m, owner256/concurrency/cache limits, complete-cell requirements, controls, original2.45s input, full normal coast and50ms threshold are unchanged. No C# edit or compilation during Play; no Editor restart/cache clearing. This is one new tooling-cause repair, not another scheduling/reservation repair.

## Compile and narrow regression

Fresh CSharp compile11:16:47.3705213UTC, Editor compile11:16:23.1180016UTC; fresh final Console0errors/0warnings. Intermediate diagnostic imports caught missing methods during the multi-part edit and the reload callback's delegate type; both were fixed before final compilation and capture. Final diff whitespace check passed.

- Ordinary title Play session: `outputs/G08_R2_P08_TitleWatchState_20261001.txt`, playingTrue, watcherEnabledFalse, scriptWatchSuspendedTrue, imports/compilationFalse. This is the shortest real lifecycle check and passed before arming ordinary Continue/focused input.
- `outputs/G08_R2_P08_WatchStates_20261001.jsonl`:44low-cadence external snapshots, all44playing, zero enabled-watcher observations; includes the measured window through ExitingPlayMode. These observations verify the actual flag, not native allocation freedom.
- Returned idle Edit: `outputs/G08_R2_P08_ReturnedEdit_20261001.txt`, watcherEnabledTrue, suspensionFalse, pending refreshFalse. The catch-up ForceUpdate completed in0.007s; source/assembly hashes remain matched. No source edit was injected during Play to test deferred compilation because project rules prohibit it.
- Runtime Console4errors match existing concave MeshCollider trigger diagnostics; fresh queried tail contains no compiler error or exception. Model request cancelled7.91s at shutdown; production response latency remains unverified.

## Clean focused result

Immutable [P08 clean receipt](G08_R2_Speed260_2026-10-01_111942_P08_Clean_FAIL.md),11:19:23.1052509–11:19:42.9350030UTC. Ordinary PlaySafe/title/Continue and accepted terrain restore/starting envelope PASS. Profiler/binary/allocation callstacks/deep/trace off; added history/GPU/motion capture absent. PID28040, Unity6000.3.2f1/DX12, Ryzen7 5800X/RTX5060/32648MB,1278x637/scale0.79; zero unfocused frames in both measured phases.

| Phase | Frames | Median | P95 | Rounded maximum | Gate |
|---|---:|---:|---:|---:|---|
| Original input | 74 | 32ms | 43ms | 50ms | Input-only classification NOT VERIFIED at this rounding; composite FAIL |
| Full normal coast | 281 | 26ms | 38ms | 56ms | FAIL against50ms |

The input-only maximum is stored to three decimals in seconds: rounding cannot distinguish just below/above50ms. Do not call it an input PASS or independent FAIL from the combined predicate. The coast maximum is clearly over budget by approximately6ms. `highSpeedFrameBudgetPassed` combines input and coast; `highSpeedCoastObserved` also includes coast timing. Therefore `speed260CoastCoverageGate=FAIL` does not indicate missing content in this run.

Coast7.835s/156.2m, acceleration28/deceleration34, input released, settled0.9m/s. Input and coast complete visible content PASS; ground, turns, walk/run/dash/reversal/diagonal/rapid turns, abrupt transition/boundary/direction PASS. Zero synchronous fallback attempts/traversal blocks/fall recoveries. No first missing visible cell in the measured windows. These are focused claims; full swept/lookahead/sustained travel remain unverified.

Input maximumUnity5280: rounded50ms wall,17ms streaming,8ms content dispatch; no collection-counter increment. Coast maximumUnity5340:56ms wall,14ms streaming,4ms lifecycle stage; no collection increment. Another coast frame5417rounds50ms and has a collection increment/13ms GC recorder. Recorder arrival does not assign native samples to these wall frames. Managed allocation-counter zeros remain INVALID, as already calibrated in D22.

Terminal census211live physical/217reservations/capacity256,8physical without reservation/14reservations without physical. Overlapping sets must not be added. Full lifetime/resource row still missing. Height tasks322executed/294success/28cancel/0fault at31.2/s are not useful complete-cell throughput.

## Identity and comparison

Arming/source/build details: `outputs/G08_R2_P08_Armed_20261001.json`, `outputs/G08_R2_P08_Build_20261001.json`.

- CSharp executingMVID37e30007-f66e-42a3-b9e8-600cd2456d63, SHA54F08AEEB8EE352B2F0F7981E89AB82201EA3F0DC67BF3F3E49C3BE5F29969CD.
- EditorDLLSHA432CC3739E3758CB9F6238636922FE834E69B53C44D0CE5CC7B61FD7E353AE04; bootstrapSHA2E4570B081F8DAB9A7D73AE893492A1EFB8E7FCA1C735E301AA5BEEBD27182AD.
- VerifierSHA439EBB794BE582C7DAE0FAD45282CC7984DD2B27481915611DCCFB44E62D2C2A. Other production owners match P07 arming hashes.
- Profile3ccfff4df9a1479884061e51940e4457, worldyq-world-0284d4409a720743c13eb56e, seed4bb221dc, accepted/validatedV2c1d6ce9d3e06f760, schema7/topology9/gate2.

P07/P08 same process, render, accepted artifact and clean instrumentation: observed maxima54/69→about50/56ms; inputP95slightly worse42→43, coastP9551→38. P07coastappearance failure absent in P08. Different actual coast trajectory/duration means this is not an isolated timing-effect estimate or proof of universal readiness.

## Ledger and next bounded question

Count one unsuccessful focused closure attempt for the distinct watcher cause, with its narrow lifecycle regression PASS and residual frame gate FAIL. Do not apply another watcher change without new evidence, repeat an unchanged clean run, reset the exhausted scheduling loop, or start an independent certification repeat before the first clean PASS.

Next question: which native CPU/callback/wait scopes account for the remaining long wall interval while the watcher is inactive? Competing explanations are activation/detail-binding or another callback, native waiting/render work, or other allocation/GC. One original-window opt-in binary diagnostic with explicit frame-boundary metadata and existing named height/detail scopes can distinguish them; no deep/allocation-callstack/high-volume history or extra preparation. Exit after original input/full coast and analyze matched maximum plus representative over-budget frames before another run. Preserve raw precision in the next diagnostic extraction; do not infer a single-phase verdict from rounded50ms. No new scheduling change is justified by this content-ready run.

First clean focused PASS, independent fresh-session repeat, cold startup, sustained travel, full swept/lookahead, lifetime resources, deliberate-stall/recovery and unload/revisit remain missing. FullR2FAIL; R3gated.
