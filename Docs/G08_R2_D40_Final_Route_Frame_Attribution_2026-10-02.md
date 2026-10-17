# G08-R2 D40 — final route frame attribution

Status: **diagnostic FAIL; full R2 incomplete**. No production repair is claimed from this capture.

## Identity and scope

One binary-only original-window witness, 2026-10-02 15:46:28.6484205–15:46:49.7533326 UTC, through the existing PlaySafe/title/existing fixture/Continue hooks. No Computer Use or focus manipulation. Source matches final P16; MVID `e2091b88-9600-49d8-b1b7-59dc453b2d2b`, main assembly SHA256 `0C18794964AD588863A5846534AA96E3BF4DC9C1B0F2460C31112D40FF049BDF`. Armed hashes: `outputs/G08_R2_D40_Armed_20261002.json`.

Receipt: `Docs/G08_R2_Speed260_2026-10-02_154649_D40_Background_Diagnostic_Receipt.md`. Existing profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted V2 hash `c1d6ce9d3e06f760` preserved. No compilation observed during Play; observer returned to idle Edit. Route v2 coast 1179.624 m over 9.274 seconds, zero obstructed frames and synchronous fallbacks. Both measured phases focused. Profiler overhead makes this diagnostic only, regardless of the receipt's route/focus validity field.

Raw trace `Logs/G08_R2_FrameTrace_20261002_154637_332.raw`, 226808927 bytes, metadata GUID `56650639-45ee-484c-a878-847217eb29a2`. 317 metadata records, 316 paired native intervals. Three saved native exports each retain three frames: `Logs/G08_R2_D40_Maximum.samplethreads.tsv`, `Logs/G08_R2_D40_Gc.samplethreads.tsv`, `Logs/G08_R2_D40_Representative.samplethreads.tsv`. No additional runtime replay for these exports.

## Same-boundary findings

| Phase/frame | Measured wall time | Native evidence |
| --- | ---: | --- |
| Input maximum, Unity 6514/native 1 | 176.376 ms | Initial preceding boundary missing. Exact interval ownership is **NOT VERIFIED**; the next largest frame is not a substitute for this maximum. |
| Largest paired input, Unity 6578/native 65 | 48.716 ms | Native interval 50.35 ms; motor 12.044 ms, streamer Update 7.355 ms; no overlapping collection. |
| Coast maximum, Unity 6735/native 222 | 75.942 ms | Native interval 76.100 ms; Editor hook 10.138 ms inclusive, nested status publication 7.039 ms; streamer Update callback 8.954 ms. No overlapping collection. |
| Slow coast with collection, Unity 6626/native 113 | 64.157 ms | Native interval 64.241 ms; Editor hook 27.591 ms inclusive contains GC.Collect 20.388 ms. Streamer Update callback 9.762 ms. Render wait overlaps main-thread work. |
| Representative coast, Unity 6731/native 218 | 52.065 ms | Native interval 52.15 ms; motor callback 13.090 ms, streamer Update callback 7.801 ms; no overlapping collection. |

Durations are inclusive and are not added across parent/child scopes or threads. Managed profiler markers and native Invoke callback wrappers have slightly different bounds. Exact clipped owner data and allocation totals are in `outputs/G08_R2_D40_NativeOwners_20261002.json`; interval selection in `outputs/G08_R2_D40_FrameCost_20261002.json`.

The three selected intervals respectively allocate 671812, 824962 and 693992 bytes across retained allocation leaves. The hook directly allocates 25464 bytes/418 events per observed tick, excluding status publication. The maximum export also contains 64841 bytes/826 events beneath status publication. These observations establish avoidable-work candidates, not that the hook alone causes all collection pressure or that removing its cost would meet 50 ms.

The profiled run also misses complete-visible coast readiness and the initial actual-speed gate (239.3 versus 240 m/s). No cell history was armed for this frame-only question, so this capture does not establish readiness causality. Exhausted scheduling/reservation attempts remain exhausted. No driver fault, GPU paging or Ollama causality is inferred from these samples.

## Next bounded question

D41 is an idle Edit-only comparison of the hook's identical relative versus pre-resolved absolute request paths, with explicit native sample labels and allocation-counter calibration. It reads absent requests without creating or dispatching gameplay markers, touching profiles, or preparing cells. Exit after one comparison and native export; implement a polling repair only if it demonstrates a material reduction. The first-boundary diagnostic gap remains explicit and must be closed before any future claim about a first-input maximum.

The unprofiled final P16 witness remains the usable failing baseline: input maximum 42 ms, coast maximum 87 ms, no route obstruction and observed readiness gates PASS. No clean focused PASS, independent repeat, full R2 completion or R3 admission is claimed.
