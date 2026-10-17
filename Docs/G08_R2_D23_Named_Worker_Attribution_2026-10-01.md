# G08-R2 D23 — named worker attribution

**Status: diagnostic FAIL; full R2 remains FAIL and R3 stays gated.**

## Change and identity

Two Editor-only profiler labels wrap existing height and detail Task.Run bodies in `YQPlayerFollowingSemanticChunkStreamer.cs` and `YQGeneratedWorldEnvironment.cs`. Main-thread dispatch captures profiler enabled state; workers do not read Unity settings. Cancellation, results, scheduling, concurrency, caches, publication and accepted values are unchanged. No production repair was added.

Fresh compile/reload completed10:38:50UTC, Console0errors/1known warning. PlaySafe screenshot/window394112 preceded edits. Diagnostic run10:41:25.3942479–10:41:45.0483874UTC used ordinary title/Continue, intended profile and successful terrain restore; PID28040,1278x637/display0.79, original input/full normal coast. Binary on; cell history/GPU/motion override off. No unfocused measured frames, Editor restart, cache clearing or added preparation.

- Assembly MVID`0024ae44-7c35-4386-9ff4-db7cfb4135ed`, SHA256`7D2D1339557CC5192F08562C40C9EAFB866C4C08232369471E1FAEA7975EADD7`.
- Streamer SHA256`6E12AD10FA88A93310FEC98F120C65F8BB0BBDE9148DFBFDC7FC2821428C476A`.
- Environment SHA256`0592B982C7F82D74468B30D12829DF227E8B2C79D9864A5A2F66802C0159FB96`.
- Sampler/verifier/model sources unchanged from P06; all hashes in [arming receipt](../outputs/G08_R2_D23_Armed_20261001.json).
- Profile`3ccfff4df9a1479884061e51940e4457`, world`yq-world-0284d4409a720743c13eb56e`, seed`4bb221dc`, accepted/validatedV2`c1d6ce9d3e06f760`, state schema7.

## Focused result

[Immutable terminal receipt](G08_R2_Speed260_2026-10-01_104145_D23_NamedWorkers_FAIL.md).

| Phase | Frames | P50/P95/max ms | Content |
|---|---:|---:|---|
| Input | 66 | 34/53/72 | Complete visible and turn coverage PASS |
| Coast | 239 | 30/42/62 | Complete visible PASS |

Ordinary movement controls pass. Abrupt/real-elapsed transition, constant segments and direction fail in this profiled trajectory; coast lasts7.580s with0.9m net displacement versus1209m in clean P06. Ground coverage passes with zero fallback attempts/traversal blocks/fall recoveries. These differences prevent treating D23 as a matched clean timing improvement or substituting its trajectory for P06. Both maximum frame gates fail against50ms.

## Saved native attribution

Raw`Logs/G08_R2_FrameTrace_20261001_104134_841.raw`,397325083bytes; metadataGUID`0f6695ee-fab1-4f96-9742-8cb5c0c41a7f`. Default flat export contains main/render threads only; worker absence there is not absence of worker work. Four completed all-thread exports cover451,463:464,527:528,627:628. [Named worker analysis](../outputs/G08_R2_D23_NamedWorkers_20261001.json) retains scopes, byte events, native thread IDs and boundary matches. [Main/render analysis](../outputs/G08_R2_D23_RemainingFrameCost_20261001.json) retains304matched intervals.

- Input maximum Unity4816/profiler451:3277406bytes/32021events. Named height scopes allocate1384220bytes/30events across five workers. Height worker30744 collects10.923ms, nested in its sampling scope and overlapping the29.287ms provisional queue pass. Builder.Update also has a separate1.077ms collection. **The first maximum lacks a preceding native boundary in this capture**; these are profiler-frame/source-scope facts, not a complete reconstruction of its72.012ms wall interval.
- Representative Unity4829/profiler464:55.135ms wall,56.3859ms between native markers. Streamer.Update24.418ms overlaps this interval, without a recorded collection in selected463:464. Height scopes allocate2214752bytes/48events in464. This demonstrates large session allocation independently of shoreline arrays, not proof of exclusive streamer CPU or of a specific collection trigger.
- Coast maximum Unity4993/profiler628:61.844ms wall,62.6626ms between native markers. Unnamed native thread29452 collects12.633ms within the explicit interval, overlapping terrain preparation18.950ms. That thread allocates953448bytes/16329events in628; previous627 includes855244bytes/9events under named height scopes. The enclosing terrain coroutine cannot be treated as18.950ms exclusive CPU.
- Separate under-budget coast interval Unity4893:44.894ms wall,46.1356ms native interval; overlaps11.633ms collection during Console text preparation in preceding profiler527/Unity4892. This differs from coast maximum collection ownership.
- Five detail scope fragments are observed across selected frames. Absence of allocation under those selected fragments does not certify the entire detail worker allocation-free.

Inclusive scopes are not added. Allocation metadata identifies allocating scopes, not all causes of collection. Native IDs belong only to this capture. Managed allocated-byte counters remain INVALID after D22 calibration. Worker jobs dispatched before trace start may remain unlabelled; labels are deliberately gated at dispatch.

## Remaining bounded decision

The clean P06 frame deficits remain89ms input/20ms coast. D23 identifies height session allocations and collection overlap, while the persistent high-frequency allocator on thread29452 remains unowned. A third existing worker, biome alphamap preparation in `YQContinuousWorldFeatureAuthority`, was discovered in targeted source search; source presence alone does not map it to that thread. No cache pooling, retention, priority, budget or callback repair is justified by that mapping alone.

Next distinguish the remaining unlabelled allocator using source-scoped allocation evidence or recorded allocation stacks, and separately quantify useful height-session cache reuse before changing its lifetime. Do not repeat the focused run unchanged or change exhausted scheduling/reservation policies. First clean focused PASS, independent repeat and missing full R2 rows remain outstanding. All requests are consumed; Editor is idle Edit.
