# G08 R2 Astra audit — 2026-09-29

## Result

R2 remains **FAIL**. The repair in this audit is diagnostic: an opt-in binary profiler capture now embeds explicit Unity frame identifiers and exposes timestamped native main/render-thread samples. No scheduler, quality, capacity, acceptance threshold, save, or world-authority behavior was changed. R1 and R3 were not run.

## Corrected prior conclusions

- The prior cell `(-1,10)` had a request age of 3.999 seconds. This does not establish four seconds continuously at the queue head, or prove a dispatch deadlock. Its first-failure diagnostic counted one active-worker dispatch wait; coroutine completion can occur after the dispatch pass and before observation.
- `ProfilerRecorder.LastValue` has no explicit source-frame identifier in the existing collector. Assigning it to an assumed preceding frame produced inconsistent CPU/wall attribution. The witness now labels those legacy associations unverified. The new binary trace uses `Profiler.EmitFrameMetaData` plus native timestamped boundary samples.
- The old `completedHeightTasks` counter was incremented in `finally`, including canceled and faulted executions. The receipt now distinguishes executed, successful, canceled, and faulted samplers. Counts are independent atomic observations and can differ at the sampling boundary while workers complete.
- High process memory alone did not identify a leaking resource owner. TerrainData teardown and provisional pooling cleanup exist; no leak was established by this audit.

## Fresh evidence

- Unity version: `6000.3.2f1`. Both runtime and Editor assemblies compiled successfully through the existing Editor refresh workflow, with a fresh successful build/reload log. The parser's null-name guard was compiled and exercised against the saved trace.
- Focused witness: `Logs/G08_R2_Speed260MotorWitness.md`, started `2026-09-29T15:01:24.5760518Z`, finished `2026-09-29T15:01:47.2159389Z`.
- Preserved profile `d541c6b9060c4952afbb4e8b5ffa1d4e`, world seed `166257f2`, accepted V2 content/validated hash `9fd470fa6c88a651`.
- Input and coast had zero unfocused frames. Synchronous fallback, visible-input-view, turn-buffer, boundary-crossing, and fall-recovery gates passed. Abrupt/real-elapsed transition, constant-input, direction, coast-visible-coverage, and frame-budget gates failed.
- Input maximum wall interval: 70 ms. Coast maximum: 1.045 seconds against the unchanged 50 ms gate. Coast traveled 1,275.5 m with zero traversal blocks and zero synchronous fallbacks.
- First coast-visible miss: cell `(3,11)` had collision, structure, overlays, and required ecology ready; appearance was incomplete. Its biome/detail lanes recorded 25/29 budget-denied frames. This is a real remaining readiness failure, not a cause-specific repair proved by this trace.
- Binary trace: `Logs/G08_R2_FrameTrace_20260929_150133_368.raw` (696,043,867 bytes). Extracted trace: same stem `.tsv`, with **352 explicit frame matches**. Capture includes profiling overhead and is diagnostic evidence, not a clean performance certification.

## Timestamped stall attribution

| Witness wall interval | Main-thread frame before resumption | Main-thread blocking scope |
| --- | --- | --- |
| Unity frame 7159: 1,045 ms | 7158 | `GameView.Paint → Semaphore.WaitForSignal`: 981 ms |
| Unity frame 7193: 590 ms | 7192 | `GameView.Paint → Semaphore.WaitForSignal`: 542 ms |
| Unity frame 7232: 571 ms | 7231 | `GameView.Paint → Semaphore.WaitForSignal`: 518 ms |

The first wait overlaps a render-thread interval starting at native timestamp `612716698010400 ns`: `UIR.DrawChain → Gfx.InitializeBuffer → BufferD3D12.InitializeResource → BufferD3D12.CreateBufferResource → CreateResourceWithTag → Semaphore.WaitForSignal`, lasting 986.420 ms. The matching streamer Update was 4.982 ms, with two appearance slices near 3 ms each. No project-owned callback was identified inside that long native wait. The trace identifies native render-resource waiting; it does not establish its underlying GPU/driver/allocation cause or justify changing project graphics settings.

## Follow-up unprofiled focused run

- Focused witness: `Logs/G08_R2_Speed260MotorWitness.md`, started `2026-09-29T15:17:33.7615307Z`, finished `2026-09-29T15:17:54.0628234Z`. It used the same profile, seed, and accepted V2 hash above; input was focused for every measured frame. No binary profiler capture was armed.
- Input visible-view and turn-buffer gates passed. Transition, zero-fallback, constant-input, direction-change, coast-visibility, and frame-budget gates failed. Five synchronous ground fallbacks occurred in 40 speed frames. The maximum measured input interval was 98 ms; the coast maximum was 150 ms against 50 ms.
- The first coast-view failure was terrain readiness at `(-1,10)`, with `terrain=None`. At the failure snapshot the request was at the queue head with request age 3.695 seconds, physical residency was 189/256, and another height task was active at priority 2 while the target had priority 1. The single-frame dispatch-wait counter showed one active-worker wait. Request age is not proof that it occupied the queue head continuously for 3.695 seconds.
- The bounded appearance decision ring recorded predictive, nonvisible reserve winners such as `(-2,9)` with 0.738 seconds arrival. However, this run's first failed view cell was missing terrain and had no appearance iterator, so those appearance decisions do not establish that appearance reservation caused the terrain miss. The earlier `(3,11)` appearance miss was not reproduced as the first coast failure.
- At the time of this follow-up, no runtime scheduling policy had changed after the trace. The new evidence showed distinct, shifting terrain and appearance readiness misses, but did not isolate a concrete starvation or ownership defect that would justify a scheduler change without weakening the established gates.

## Files and operation

- `YQSemanticChunkRuntimeVerification.cs`: opt-in 45-second maximum raw capture, explicit frame metadata, post-Play trace extraction, null native-name tolerance, profiler cleanup on normal completion/finally/OnDisable, corrected diagnostic labels.
- `YQPlayerFollowingSemanticChunkStreamer.cs`: outcome-only atomic counters; sampling and cancellation semantics preserved.
- `Editor/YQEditorAutoRefreshBootstrap.cs`: post-Play analysis through the existing background update workflow.
- Opt in by placing `Temp/YQ_R2_FRAME_TRACE.request` before the existing focused-R2 request. An already active profiler/deep-profiler is not redirected. The raw writer closes and prior inactive profiler settings are restored before analysis. Parsing occurs after Play Mode; raw evidence is preserved.

At the end of the initial audit, no further runtime rerun or speculative gameplay patch had been made after this trace. The same-frame native trace, rather than legacy LastValue associations, was the next diagnostic reference.

## API evidence

- [Unity 6.3 binary profiler logging](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler-enableBinaryLog.html)
- [Unity 6.3 frame metadata](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.Profiler.EmitFrameMetaData.html)
- [Unity 6.3 raw frame view](https://docs.unity3d.com/6000.3/Documentation/ScriptReference/Profiling.RawFrameDataView.html)

## Latest focused R2 witness after appearance-reserve repair

- A bounded appearance-reserve repair was applied after the 15:17Z receipt: reserve arbitration now considers painters with an immediate hard-view/current-visible deadline before using the existing camera-deadline comparator and visible-painter denial-age fairness. It does not change the shared frame budget, appearance quality, or acceptance thresholds. Astra's follow-up found no additional terrain-priority patch justified by the next receipt.
- Current-source witness: `Logs/G08_R2_Speed260MotorWitness.md`, started `2026-09-29T16:17:34.7181542Z`, finished `2026-09-29T16:17:55.7467638Z`. It records Assembly-CSharp MVID `44da471a-0ff4-4e99-a0a3-3a508fded516`, assembly SHA-256 `7A545DB272B4D974742D941B815AC4F333DAA53AC1B85AE286DC167CA615E7B1`, and streamer source SHA-256 `9D645F49868E13E46CED410209C543443B457CABFC479E225F88B489A6ECA658`.
- The accepted-save startup, initial coverage, boundary-crossing, zero-synchronous-fallback, high-speed visible-view, and turn-buffer checks passed. The app was focused for all measured frames. The 6-to-260 m/s first-frame transition, direction-change, constant-input, coast-visible-coverage, coast-frame-budget, and combined frame-budget checks failed. R1 and R3 remained out of scope.
- The motor's ordinary acceleration is 28 m/s² and uses `Vector3.MoveTowards`; from rest to 260 m/s that takes about 9.29 seconds, or about 9.07 seconds from 6 m/s. The stress verifier deliberately overrides acceleration/deceleration to at least 1,000,000 m/s² before its 260 m/s segment. Therefore the failed 171.7 m/s first-frame actual speed against a 260 m/s request does not demonstrate an ordinary-acceleration limit. The sample records matching input, direction dot 0.999, zero side contacts, and a successful cell-boundary crossing. It does not record that transition frame's collision flags or traversal-gate rejection state, so the short actual displacement's physical cause remains unidentified. The abrupt-stress gate still fails as written.
- Input and coast frame maxima were 105 ms and 100 ms, respectively, against the unchanged 50 ms coast budget. This run did not capture a binary frame trace, and its recorder attribution is explicitly unverified; it does not prove which owner caused those exact wall-frame peaks. The earlier timestamped binary capture showed a long Unity GameView/render-thread semaphore wait, but that older match does not identify the current peaks.
- The first coast-visible miss was cell `(11,5)` with terrain `None`. Its earlier height task was preempted when direction changed and later re-entered at priority 1, dispatching immediately at frame 9277. The reported 4.709-second request age includes the canceled attempt; this receipt does not show a priority violation or support another terrain-scheduler change.
- Source already contains chunk-level frustum and radial visibility demand, including a 512 m guaranteed distance. Further reducing view distance would violate the preserved R2 coverage contract, and raster back-face culling does not address the measured terrain-readiness miss or unverified editor frame stalls.

R2 remains **FAIL**. The next repair decision needs new causal evidence for the 171.7 m/s actual displacement and the current frame-budget outliers; the latest terrain trace alone does not justify changing dispatch priorities.

## Transition diagnostics build and discarded unfocused run

- Added same-frame first-boost endpoint, `CharacterController` collision flags/grounded state, traversal-rejection state, and qualifying side-contact identity to the focused witness. No movement behavior or gate threshold changed.
- The new source compiled and executed as Assembly-CSharp MVID `74131347-559f-4dd6-9b83-c3dbe9199453`, assembly SHA-256 `86D5CFFAF41BB916DA44E11AACEE8DE7935601AE6D18758D6AB5FC3D517B18CC`, verifier source SHA-256 `284B331299759BA1066A300229FC83BD76EC010D262647D8FEC6299614661D06`.
- The attempted witness ran `2026-09-29T17:36:00.7039447Z`–`17:36:19.6907116Z`. Its input acknowledgment recorded `applicationFocused=False`; discard its speed, fallback, coverage, and frame-budget outcomes per the R2 witness protocol. It is not a valid R2 result and does not replace the 16:17Z focused failure above.
- Unity auto-returned to Edit Mode. A valid follow-up requires the Editor to be focused before the speed marker is dispatched, after production Continue has released gameplay input.
