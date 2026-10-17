# G08-R2 D25 — remaining frame attribution

**Status: diagnostic FAIL; no R2 certification.** One original-window binary capture answered the next P08 question. No unchanged clean rerun preceded it.

## Identity and capture

Immutable run: [D25 diagnostic receipt](G08_R2_Speed260_2026-10-01_112611_D25_RemainingFrameCost_FAIL.md), 11:25:50.5664499–11:26:11.0529922 UTC. Unity PID28040, 6000.3.2f1/DX12, Ryzen7 5800X, RTX5060,32648MB;1278x637/display0.79. Ordinary PlaySafe/title/Continue and accepted terrain restore. Original2.45s6→260m/s input and full normal coast; zero unfocused measured frames. Binary profiler on, deep/allocation callstacks/history/GPU/motion override off. Editor script watcher inactive in Play.

Source/build match P08: CSharp MVID37e30007-f66e-42a3-b9e8-600cd2456d63; DLL SHA54F08AEEB8EE352B2F0F7981E89AB82201EA3F0DC67BF3F3E49C3BE5F29969CD. Streamer SHA6E12AD10FA88A93310FEC98F120C65F8BB0BBDE9148DFBFDC7FC2821428C476A; verifier SHA439EBB794BE582C7DAE0FAD45282CC7984DD2B27481915611DCCFB44E62D2C2A. Full arming hashes: `outputs/G08_R2_D25_Armed_20261001.json`.

Profile3ccfff4df9a1479884061e51940e4457; worldyq-world-0284d4409a720743c13eb56e; seed4bb221dc; acceptedV2c1d6ce9d3e06f760; schema7/topology9/gate2. No Editor restart, OS cache clearing, accepted-content regeneration, limit change, input change, or additional preparation.

Raw `Logs/G08_R2_FrameTrace_20261001_112559_971.raw`,234934136bytes, GUID51fc5592-1bb0-4541-9668-5324499f5c1e.289metadata frames/288 paired intervals. Saved all-thread exports: `G08_R2_D25_Input_20261001.samplethreads.tsv`958:960; `G08_R2_D25_CoastMax_20261001.samplethreads.tsv`1148:1149; `G08_R2_D25_CoastRepresentative_20261001.samplethreads.tsv`1161:1162. All ranges have matching identity. Analyses under `outputs/G08_R2_D25_{RemainingFrameCost,NamedWorkers,CoastAlloc,OptionalComponents}_20261001.json`.

## Matched wall intervals

| Phase / Unity frame | Native frame | Wall | Native boundary interval | Finding |
|---|---:|---:|---:|---|
| Input maximum4834 |959|58.293ms|59.9665ms|Streamer Update12.193ms, motor8.083ms, Late2.804ms; no collection inside interval |
| Coast maximum5024 |1149|71.607ms|71.7433ms|Coroutine lifecycle owner pass11.560ms and Late owner pass11.265ms; streamer Update9.108ms, motor3.386ms; no collection |
| Representative coast5037 |1162|53.893ms|54.0844ms|Coroutine lifecycle8.381ms, Late lifecycle7.834ms, streamer Update8.386ms; baseline observer4.988ms |
| First coast4835 |960|57.707ms|78.8191ms|Collection13.225ms under verifier visual coverage / GetComponentNullErrorMessage |

These are inclusive scopes, not an exclusive sum. Render-thread semaphore waits follow main-thread work and do not establish a driver or GPU fault. The full21.092ms verifier LateUpdate scope straddles the input4834 closing boundary; assigning it entirely to that input maximum would be wrong. The13.225ms collection has native frame label4834 but starts91637416064900ns, after the input closing boundary91637415670500ns. It belongs to the following boundary interval. Instrumentation between wall sampling and boundary emission explains why native interval and wall values differ.

## Allocation cause and limits

Saved GC.Alloc events directly record GetComponentNullErrorMessage as an allocating caller. At maximum coast boundary interval5024, motor/streamer Update/coroutine each have30events/19076bytes on that path. At representative5037, each implicated callback has31events/19714bytes. These are disjoint allocation-event sizes; allocation count is not lookup count. At4835, the allocating lookup scope includes the13.225ms collection. Missing optional Terrain/TerrainCollider queries in publication validation/activation are therefore a bounded allocation-repair candidate; the recorded path alone does not identify its generic component type or prove it explains all allocations.

The maximum71.607ms has no collection. Removing optional lookup allocations cannot by itself be claimed to close its21.607ms deficit. Repeated owner passes remain measured CPU work with unresolved inner attribution; do not skip a pass or reset the exhausted scheduling/reservation repair loop from this observation.

Height-worker events retain named sampling ownership and show4492/17036-byte groups rather than D23's multi-megabyte sampler storage groups. This does not establish allocation freedom or map native IDs across runs. All-thread exports filter CPU scopes under1ms except explicit categories and retain every GC event; absence of a short lookup scope is not zero CPU cost.

## Readiness and certification limits

Input66frames median/P9535/48ms; coast223frames36/52ms. Both exceed50ms maximum. Input visible/turn and zero fallback pass. Coast visible appearance misses(-3,-3); full receipt preserves first owner snapshot. No cell-history capture was enabled, so it cannot establish starvation, missing lead time, or unique useful complete-cell throughput.

Profiled first boost used Unitydelta0.136808s and encountered terrain side contact; direction qualification failed and actual trajectory differed. Coast8.400s/1188.1m. D25 is diagnostic-only and cannot substitute for clean P08's content-ready result or certify current repaired source. No R1 recertification/R3 run. First clean focused PASS, independent repeat and full R2 matrix remain missing.

## Next bounded repair

P09 changes only optional component lookups in live/cached renderer validation, visual gating and collision-preserving activation. Expected improvement: remove the measured Editor missing-component allocation path while preserving every live readiness predicate. Shortest regression: transient Edit fixtures covering absent/present/destroyed/disabled/inactive roots, cached/live ecology, renderer gate and collision classification, with saved native allocation comparison. Only after that passes is one clean original-contract focused witness justified. Remaining lifecycle CPU attribution stays unresolved if the witness fails.
