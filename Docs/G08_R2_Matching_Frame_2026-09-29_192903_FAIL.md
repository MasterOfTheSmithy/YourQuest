# G08 R2 matching-frame diagnostic — 2026-09-29 19:29 UTC

**R2 remains FAIL; R3 remains gated.** This is an opt-in binary profiler run on the same patched source as the clean focused 19:22 witness. R1 and R3 were not run.

## Identity and evidence level

- PlaySafe selected-profile Continue, `Play Focused`, background `YQ_R2_FRAME_TRACE` plus `YQ_SPEED260_FOCUSED` markers. Input acknowledgment reports `applicationFocused=True`. Executing assembly SHA-256 `10A63348825840776C7CE9767CAA864F72833209ECB6D9A8C8AFF18E242C9231`; streamer source SHA-256 `233CA99DE7A8A905E7929F4D06DC57D263992038662ECC5760FCEE9FF3CEC0AD`.
- Profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted V2 content/validated hash `c1d6ce9d3e06f760`, unchanged from the clean focused witness.
- Full [traced witness](G08_R2_Speed260_2026-09-29_192842_TRACED_FAIL.md). Native trace: `Logs/G08_R2_FrameTrace_20260929_192852_300.tsv` with its `.raw` capture. Trace overhead makes this a **diagnostic** result, not a clean 50 ms certification.

## Timestamped frame attribution

| Unity frame | Coast wall interval | Matching main-thread samples |
| --- | ---: | --- |
| 9686 | 81.5 ms | streamer Update 36.5 ms, including lifecycle owner pass 18.6 ms; light-probe manager 16.0 ms |
| 9689 | 120.0 ms | streamer Update 36.8 ms, including two content-start calls at 6.6 and 7.7 ms; motor Update 23.9 ms, including camera-ground admission 20.6 ms; light-probe manager 19.0 ms |
| 9699 | 143.6 ms | motor Update 67.1 ms, including camera-ground admission 63.9 ms and an 18.5 ms GC.Collect; streamer Update 30.5 ms; light-probe manager 17.6 ms |
| 9703 | 120.8 ms | motor Update 47.8 ms with lifecycle owner pass 21.3 ms; streamer Update 18.1 ms, including another lifecycle pass 6.4 ms and LateUpdate lifecycle pass 7.6 ms; light-probe manager 18.5 ms |
| 9722 | 93.3 ms | streamer Update 31.3 ms, including content start 12.7 ms; light-probe manager 29.5 ms |

The trace identifies coupled project streaming/motor work, GC, and Unity light-probe work. It does **not** prove that changing one scheduler priority, reducing visible content, or altering graphics settings would satisfy the unchanged 50 ms limit. The trace also records a separate 35.3 ms native semaphore wait inside `FrameDebuggerWindow.Paint` at input frame 9635; its root cause is not established.

## Gate result and stop

The traced run failed coast visibility, zero synchronous fallback (two attempts), and frame budget: input P50/P95/max 69/90/108 ms; coast 74/104/144 ms. The unprofiled focused witness in [G08_R2_Focused_Gate_2026-09-29_192245_FAIL.md](G08_R2_Focused_Gate_2026-09-29_192245_FAIL.md) remains the clean throughput reference: input 64/87/89 ms and coast 59/89/131 ms, with zero fallback but incomplete visible coast content. Both runs contradict R2 completion. No new runtime change was made from this trace; R3 stays gated.
