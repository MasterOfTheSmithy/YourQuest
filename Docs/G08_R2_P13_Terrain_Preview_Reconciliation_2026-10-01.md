# G08-R2 P13 — avoid an intermediate Terrain visibility toggle

## Cause and scope

D34 actual lifecycle4763/native3866 has17Terrain writes6.785ms and material callbacks180700B/4238events. Source `SetChunkVisualsEnabled` first applies the full hierarchy flag, then independently applies existing full/ground-preview visibility to its mapped Terrain. When feature visibility is false but synchronized ground preview is already permitted, this performs false→true in the same synchronous call. D35 detached actual production-method checks confirm128such reconciliations produce128hierarchy plus128managed Terrain native writes with identical final state. This is a continuation of the P12 redundant visual-write cause, **not a new reset repair counter**. Scheduling/reservation loop remains exhausted.

P13 retains the standalone full hierarchy method and moves only the mapped Terrain's decision to its already-existing final independent gate. Every other Terrain and Renderer still receives the full hierarchy gate. No preview predicate, readiness stage, collider, active-object rule, terrain content/appearance/ecology, movement, camera, demand/resource limit, save, seed, artifact, GUID or serialization change. No alternate Terrain rendering API. This architecture-sensitive change was reviewed against `ShouldExposeTerrainPreview`, `HasPublishedTerrain`, per-chunk final gating and publication predicates: final flags and owner decisions remain identical, and no readiness barrier is bypassed. R1 is not rerun or recertified.

Files: `Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs`; verifier adds detached regression and an Edit-only observed managed-write counter. Exact pre-change snapshots `outputs/G08_R2_D35_*Before_20261001.cs.txt`; scoped patch `outputs/G08_R2_P13_StreamerPatch_20261001.diff`. The counter is off in clean play; it adds no gameplay authority. Fixture creates/destroys only transient owned objects/TerrainData, with no profile/world loading or state commit.

## Compilation and narrow verification

Before probe MVIDe5d46b98-dd91-4c48-8b08-5d00e8908049/mainDLLCC7FC7318DA4F008301EA07D3B76B861D8F1939C78EC33269D4C1AE4D712C798; streamer1930F7AA57DEBA5F50D1E7296F90B65A2E67BE57AD0222832F3EA42002137501. Final compile outside Play14:59:35UTC, fresh native PlaySafe Console0errors/known CS0162warning. Final MVID `d793300c-9066-4155-9ab4-ffe406a41c2c`; main DLL `AE05E6CFD0592B0C191123B68E3B6F2005EBD44173BE0FFD7EB4073247CD944B`; Editor DLL `A3A3E726EFAEDF0F51BCFE5F4A1212B21D0C94160B96CF9ADBCCB249F9348C4A`; streamer `F9EA9285B793793F6D359E1BA173DA1E553F84FF36096824BBA1CC03BAEAF2CA`; verifier `325E3F68F412319436D480580632850BCEE08B4BE9EF23D95150ECCE10A1D165`. Metadata `outputs/G08_R2_P13_Compiled_20261001.json`.

Both baseline14:58:30UTC and final14:59:55UTC **PASS362checks**: existing42publication/component cases,64strict guarded-setter cases,256new per-chunk final-state checks. New combinations cover active/inactive Terrain roots, enabled/disabled collider, collision stage readiness, guaranteed-view membership, desired visibility, initial component state, known/unknown visual state and idempotent repeat. Non-mapped Terrain and feature Renderer remain strictly gated; collider/active state and drawHeightmap/drawTreesAndFoliage values remain unchanged. Reflection is fixture overhead, not user-flow proof.

| Same128call detached benchmark | Before | P13 |
| --- | --- | --- |
| Hierarchy Terrain state changes |128|0|
| Final managed Terrain state changes |128|0|
| Inclusive wall time including reflection/checks |35.923ms|0.593ms|
| Native benchmark CPU sample |35.761ms|Filtered below1ms, not zero|
| Disjoint allocation bytes/events under benchmark |1081984/23040|15488/512|
| Material callback allocation bytes/events |934400/21376|0/0|

Native before raw145830_277/GUIDa66c2467-3aaa-4012-ba02-3cced9af69d5/native4118; after raw145955_192/GUID6557e43b-d27d-4493-bc1b-b7f0f2b6bc2b/native4166. One saved native frame exported each, complete footer. `outputs/G08_R2_D35_PreviewNative_20261001.json` contains reproducible metrics. Remaining after allocations are fixture overhead; no runtime zero-allocation claim. Detached evidence is not an isolated production frame-time estimate or focused PASS.

## Clean focused witness — FAIL

The user confirmed an uninterrupted foreground window. Fresh native Editor evidence showed PlaySafe/Edit, Game1278x637/scale0.79 and no errors before arming. Markers were written at15:10:04.5229309UTC, focused and Continue first/runtime start last. The earlier unarmed observer expired without starting a runtime; a new observer watched the same PID28040. No Editor restart, cache clearing or added preparation.

Runtime15:11:35.8566647–15:11:56.1591732UTC. Immutable receipt: `Docs/G08_R2_Speed260_2026-10-01_151156_P13_Clean_Receipt.md`. Executing final MVID/source/main DLL match above. Profile3ccfff4df9a1479884061e51940e4457/worldyq-world-0284d4409a720743c13eb56e/seed4bb221dc/accepted V2c1d6ce9d3e06f760/schema7 unchanged. Profiler/binary/deep/allocation stacks/history/visual observer/GPU/motion override off. Both measured phases zero unfocused frames.51Play observations: no compiling, native watcher disabled and refresh suspended; returned Edit and all five watched hashes match. Two save-owner source hashes checked separately unchanged. Setup and returned metadata: `outputs/G08_R2_P13_Armed_20261001.json`, `outputs/G08_R2_P13_ReturnedEdit_20261001.json`, `outputs/G08_R2_P13_WatchStates_20261001.jsonl`.

| Phase | Frames | Median / P95 / maximum | Outcome |
| --- | --- | --- | --- |
| Original2.45s input |78|31/40/42ms|PASS timing and complete visible/turn/ground |
| Full natural coast |267|29/41/56ms|FAIL timing by6ms and complete visible |

Coast8.273s/1134.5m/final0.2m/s, input released/settled, ordinary acceleration28/deceleration34 restored. Ordinary walk/run/dash/reversal/diagonal/rapid turns, abrupt6→260 transition/boundary, constant segments/direction changes PASS. Ground coverage every input/coast frame, zero synchronous fallback attempts, movement interventions and fall recoveries PASS.

First coast miss `(11,9)` at frame4944: collision ready, required content/overlays/appearance/ecology/activation not ready, owner epoch6/attempt3/restarts2; recorded current content start0.014s before observation, prior epoch4 preempted0.558s ago by(5,11). Hard age2.589s and older collision age8.688s are not the replacement content lifetime. Snapshot is not proof of erroneous admission, cancellation or intrinsic capacity. No high-volume history in this clean run. Scheduling/reservation loop remains EXHAUSTED; this snapshot does not authorize another attempt at that cause.

Coast worst frame4924: wall56ms, recorder main26ms/streaming18ms/terrainDispatch4ms, GC counters1/1/1. Arrival-based recorder values are not exact native frame attribution; backend managed allocation zero remains invalid. No clean native cause inferred. Terminal census5124: physical196/reservations255/cap256,8physical without reservation/67reservations without physical, active83/retained109/unloaded305. These overlapping terminal sets do not certify all owner lifetimes.

P12→P13 clean maxima48/71→42/56ms are observed across different trajectories and inventories, not an isolated runtime effect estimate. The matched detached before/after evidence above establishes the removed toggle and allocation reduction. Count **TWO unsuccessful focused closures of the redundant visual-write cause** including P12; narrow362checks PASS, complete focused FAIL. No unchanged clean retry. First whole clean PASS, independent repeat and full matrix remain absent; R3 gated/R1 not recertified.

## Current R2 row ledger

| Required row | Status | Evidence boundary |
| --- | --- | --- |
| Accepted-save startup / initial complete envelope | PASS focused | Ordinary Continue/current final build |
| Cold startup | NOT VERIFIED | Separate required row |
| Ordinary controls / turns / stress transition | PASS focused | Actual production input and motor |
| Input complete visible / turn / ground | PASS focused | Every measured input frame |
| Coast complete visible content/ecology/appearance | FAIL | First cell(11,9), frame4944 |
| Input maximum frame | PASS |42ms against50ms |
| Coast maximum frame | FAIL |56ms against50ms |
| Ground safety / zero fallback / intervention / recovery | PASS focused | Every measured traversal frame, zero counters |
| Extended sustained travel / full swept/lookahead matrix | NOT VERIFIED | Short witness is not full R2 |
| Full resource ownership / lifetime limits | NOT VERIFIED | Terminal overlapping census only |
| Deliberate stall / honest recovery | NOT VERIFIED | Required final dependency row |
| Natural unload / revisit | NOT VERIFIED | Required final dependency row |
| Complete focused PASS / independent repeat | FAIL / NOT VERIFIED | First full PASS missing |
| Full R2 / R3 admission | FAIL / gated | Required rows incomplete |

Next bounded question is the already-saved D34 input-maximum LLMThinkCycle.Update19.504ms: determine the actual source subcall before considering a distinct repair. This is not a claim that it caused clean coast4924. Analyse the existing capture/source first; no broad unchanged replay. No priority, demand, budget or reservation repair follows from the clean first-cell snapshot.

## Saved D34 LLM callback clarification after P13

Bounded existing InputMax sample export inspected, no new capture/run/source edit. D34 native3851/Unity4748 LLMThinkCycle.Update19.504ms contains first synchronous coroutine starts: LLMClient.SendOnceCoroutine5.993ms (including Mono.JIT3.251ms) and LLMClient.ProbeLocalHealth4.485ms.790 allocation leaves beneath callback; filtered small children do not provide an exclusive full cost breakdown. ResolveReferences scene searches are candidates from source, not demonstrated dominant cost: bootstrap wires both required references, and no matching search sample established. Do not patch search or infer LLM/driver cause of clean P13 coast4924 from this unrelated profiled frame. Next source boundary is LLMClient request enqueue/first coroutine advance and local-health ownership; read its scoped LLM instructions and actual methods before any hypothesis/regression. No production repair/count increment from this inspection.
