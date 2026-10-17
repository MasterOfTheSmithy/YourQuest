# G08-R2 D34 — actual visual operations and first deadline lineage

## Bounded question

P12 clean FAIL: input48ms/coast71ms, first coast incompleteappearance/activation at(13,-1), collision/content/overlay/ecologyready. Worst coast frame5069 reports13mslateLifecycle with no collection increment, but recorder arrival is not exact native ownership. P12 guarded setters passed106state/collider/native-write-count cases; the first focused closure attempt failed. Scheduling/reservation loop remains exhausted.

D32/D33 identified hierarchy visual cost and actual redundant writes. The remaining alternatives inside final P12 behavior are hierarchy enumeration, actual changed Renderer/Terrain setters and their native work, native component/state reads, or cumulative activation visits outside the hierarchy gate. D34 adds only six profiler scopes around these existing operations. Their original order and values remain unchanged. The existing saved exporter retains short named lifecycle samples. No source readiness, scheduling, content, movement, camera, visual quality, limits, save or world change.

One normal PlaySafe/title/Continue replay will use binary capture plus existing bounded demand-wide cell history. Original2.45second6→260input and full normal coast; no stacks/deep/GPU/motion override or extra preparation. Freeze and analyse the replay's actual first failed cell, which may differ fromP12's(13,-1). Inspect actual maximum and largest owner-pass intervals, not assumed frame5069. Exit after those exact operation/frame and deadline histories resolve the named alternatives. No broad unchanged recapture or clean repeat.

Historical P12 clean remains current failing acceptance evidence. D34 is diagnostic-only and does not reset/increment production repair counts. Compile/runtime attribution pending. R2 remainsFAIL/R3gated.

## Completed diagnostic — 14:45 UTC

Compilation completed outside Play at14:42:17UTC; fresh reload/Console showed no compiler errors. Only six profiler scopes were added around existing operations, preserving P12 behavior. Executing MVID `5202c4f6-273a-40b2-a075-5051bb317f83`; main DLL SHA256 `2FCFBFC2E537F80C60D31242F2A1AAE99CF069BE4AB816F4D5BFD9F9A7F4EF25`; Editor DLL `67261D10917092221B4D863FD74AC91B4C58C8681F904EB9B3EEAD9F0D245983`; streamer `22234D2AE8172D84E119EFE1517932F14A68D6EECB2A8755E0125A371EF8B76A`; verifier `753B7D10BF9C23DC0FCB4E6C69B27D4AEEF6AB0B16777BCBA8769345C61582D8`.

Ordinary PlaySafe/title/Continue,14:44:37.9483873–14:45:00.1824545UTC; immutable [runtime receipt](G08_R2_Speed260_2026-10-01_144500_D34_BinaryOnly_Receipt.md). PID28040/Unity6000.3.2f1/DX12/Ryzen5800X/RTX5060. Accepted profile3ccfff4df9a1479884061e51940e4457/worldyq-world-0284d4409a720743c13eb56e/seed4bb221dc/V2hashc1d6ce9d3e06f760 unchanged. Accepted terrain restore/initial envelope PASS; view512m/cell128m/physical cap256 unchanged. Binary and bounded cell history ON; stacks/deep/GPU/motion override OFF. Diagnostic only.

During loading before presentation release, a fresh native screenshot showed the Editor restored to1077×630/scale0.41. The observed Maximize control restored the original maximized window/scale0.79; fresh screenshot confirmed1278×637 Game render, followed by Game focus while loading. No UI input or viewport change during measured phases. Both phases zero unfocused frames. Watch52Play observations/no compilation/watcher inactive; all five watched source/DLL hashes match arming and returned Edit. Two additional armed save-owner hashes separately checked unchanged. Watch7491 completed exit0; no live run remains.

Input63frames maximum57.384ms; coast263frames maximum59.479ms,9.357s/1270.7m/finalspeed0.4/normal acceleration28/deceleration34 restored. Ordinary controls/boundary PASS. Diagnostic abrupt transition/direction/constants/timing/coast complete-view FAIL. Input visible/turn/ground coverage PASS, but **two synchronous ground fallbacks attempted and published: zero-fallback FAIL**. Actual first failed cell **(5,12), ecology incomplete**, rather than P12 appearance miss. No clean certification or new production repair attempt.

## Saved same-interval attribution

Raw `Logs/G08_R2_FrameTrace_20261001_144447_421.raw`,234772236bytes/GUID `f0f4d806-b7ac-412e-b6a6-902eec00a5cf`;325paired explicit boundary intervals. Four saved exports each matched2native frames. Analyses: `outputs/G08_R2_D34_RemainingFrameCost_20261001.json`, `...SelectedFrames...json`, `...AllOwners...json`, `...LifecycleSubcalls...json`. No extra runtime replay.

| Actual interval | Native finding |
| --- | --- |
| Input4748/native3851,57.384ms | LLMThinkCycle.Update19.504ms; streamerUpdate11.924ms; no collection in the explicit interval. |
| Coast maximum4773/native3876,59.479ms | Terrain coroutine22.491ms **includes** GC.Collect17.373ms; lifecycle owner1.396ms. Collection site does not identify causal earlier allocations. |
| Largest lifecycle4763/native3866,39.554ms wall | Coroutine owner8.909ms includes activation8.663/reconciliation8.141/hierarchy8.080ms.22hierarchy gates;37root enumerations0.374ms;887actual renderer writes0.839ms;17actual Terrain writes6.785ms. |
| Representative4931/native4034,53.007ms | GameView.Paint10.596ms; owner1.141ms; no collection in the interval. No driver/quality-setting inference. |

Largest lifecycle hierarchy allocation203956bytes; **180700bytes/4238events beneath `LifecycleTerrainStateWrite > MaterialEditor.ApplyMaterialPropertyDrawersFromNative > GC.Alloc`** proves the production Terrain visual setter link. These are actual changed-state assignments, not P12-skipped redundant writes. Terrain native writes dominate this hierarchy pass; enumeration and outside-hierarchy activation visits do not. This does not prove package fault or sufficient whole-frame savings. Named totals are nested inclusive and must not be added. Lifecycle durations round0.001ms/sample; other short CPU callbacks remain filtered. Native read/lookup zero-rounded totals do not establish literal zero cost.

The same saved exports retain25464bytes/418allocation events under the Editor marker dispatcher despite native file-watcher suspension. Exact allocation callsite remains unknown; suspension does not disable required marker dispatch. No repair follows from that path alone. The matched input LLM callback and coast collection are separate remaining costs.

## Frozen first-cell lineage

`Logs/G08_R2_CompleteCells_20261001_144500_139.tsv`:48148rows/zero dropped/339ignored without owner, first failure frozen prefix26036. `outputs/G08_R2_D34_CompleteCells_20261001.json` preserves full prefix and later completion.

Cell(5,12) admitted92.674245; collision ready93.206238, content/appearance/ecology stage flags ready by94.227536. Hidden root/activation means this was **not strict visible publication**. Actual terrain-root removal95.576537 advances terrain/content epochs1/2→2/3; frame4489/95.584905 resets readiness. Replacement admissions101.265374/103.933198/104.124469. Observed teardown/rebuild is not proof that eviction violated preserved demand/resource policy.

Replacement hard/predictive/physical membership104.314435; dispatch105.811355; height-budget denial105.958603; collider ticket105.996825; replacement content epoch4 starts106.033887. Collision first observed ready106.087318; appearance106.245788; content106.271167. Activation completes106.515964 while ecology remains incomplete. Actual visible membership106.564512; verifier failure4810/106.580009 has required ecology work1047/six steps/1.729ms/two budget denials/no replacement-content preemption. Ecology and strict publication ready4812/106.650319: **70.310ms after verifier failure;85.807ms after observed visible membership**.

Receipt collision age13.347s is older lifetime history, not replacement-owner lead. Actual replacement collision-ready observation precedes visible membership by477.194ms. Observed hard membership lead2.250077s; content dispatch starts530.625ms before exposure; content-ready leads293.345ms. Late ecology and two denials do not prove general starvation or intrinsic capacity. Scheduling/reservation boundary remains exhausted; do not relabel it into another repair loop.

## Useful throughput and resource observations

Class sets overlap. Strict complete means actual view publication; hidden stage-ready cells are not published. Counts are diagnostic publication observations, not intrinsic generation capacity.

| Phase/class | Initial prepared/demand | New unprepared | Complete transitions | End backlog | Incomplete exposure cells |
| --- | --- | --- | --- | --- | --- |
| Input visible,2.484s |28/28|0|0|0|0|
| Input swept |1/1|0|0|0|0|
| Coast visible,9.348s |28/28|54|49|0|45|
| Coast swept |1/1|0|0|0|0|
| Coast predictive |36/81|112|63|75(+30)|Not an exposure gate|
| Coast physical |154/256|150|71|139(+37)|Not an exposure gate|

Coast visible new-unprepared demand5.777cells/s vs unique complete transitions5.242cells/s;45different cells observed incomplete despite zero terminal visible backlog. Predictive11.982vs6.740cells/s; physical16.047vs7.596cells/s. Visible uncensored publication latencyP95=1.549s/max3.151s. Observed runtime owners≤209/demand≤256/painters≤3/colliders≤2; not a full resource-lifetime census.

## Exit / remaining decision

Both D34 questions are resolved for this capture. No broad unchanged replay or clean retry. P12 one failed production closure and exhausted scheduling loop unchanged. Latest clean P12FAIL48/71ms; first whole focused clean PASS/repeat/full matrix still absent; R3gated.

Smallest next investigation: existing Terrain visual-gating contracts and a disposable Edit-only comparison of equivalent supported visual controls. Demonstrate cheaper operation with identical collider/content/quality semantics before production adoption; actual changed setters cannot simply be skipped. Separately inspect the exact LLMThinkCycle.Update source and saved19.504ms frame before any additional trace. No production patch was made in this analysis phase.
