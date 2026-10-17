# G08-R2 P14 — remove unused candidate scans from denied work

## Demonstrated cause and scope

D37 reexports four existing D36 native frames, retaining both already-recorded content-candidate markers below the old 1 ms filter. No new gameplay capture. The same explicit Unity frame IDs and native boundary timestamps identify the intervals; imported profiler indices changed after the externally reopened Editor. Raw capture GUID `15da98c9-1472-4cad-a3e3-6582b9ed3c2e`, capture MVID `d793300c-9066-4155-9ab4-ffe406a41c2c`. `Logs/G08_R2_D37_CandidateScan.raw` is a hard link to the original recording, not another capture or additional disk allocation. Original D36 exports remain intact.

At Unity4932/coast maximum70.334 ms, two separate activation owner passes perform450 candidate scans each:8.289 ms and5.156 ms. At Unity4930/54.418 ms,452 scans cost7.230 ms. These are disjoint sibling scan sums within inclusive parent samples; do not add their parent totals. Recorded durations are rounded per sample to0.001 ms. Exact attribution: `outputs/G08_R2_D37_CandidateScanAttribution_20261001.json`; reproduction: `outputs/G08_R2_D37_AnalyzeCandidateScans_20261001.cjs`.

`CanAdvanceAggregateWork` evaluated both ready-content searches after the budget was spent, before checking whether the request was `contentDispatch` and its reserve was available. Activation and other denied stages could never use the result. Both searches are read-only candidate queries. P14 moves them into the existing short-circuit dispatch condition, after stage and reserve checks. No admission rule, priority, reserve, concurrency, budget, demand set, cache capacity, stage readiness, cancellation, publication, accepted content, terrain visibility, movement, save or serialized contract changes.

This is measured redundant computation in the budget predicate, distinct from changing scheduling/reservation policy or visual setters. The scheduling/reservation loop remains EXHAUSTED; redundant visual-write closures remain TWO. P14 is the first applied repair of this candidate-scan cause. Its focused result is pending below; no completed repair or goal PASS is implied.

The clean P13 coast deficit was6 ms against50 ms plus an incomplete visible cell. The saved diagnostic confirms removable work, but does not measure clean P14 savings or prove earlier complete-cell publication. Expected result: zero unrelated candidate scans, identical admission decisions, cheaper deferred activation. Readiness remains a separate acceptance question.

## Review, compilation and detached regression

Changed runtime owner: `Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs`. Verifier adds the saved-export filter and a detached admission fixture with an Edit-only opt-in scan counter; clean gameplay leaves it disabled. Snapshots: `outputs/G08_R2_D37_StreamerBefore_20261001.cs.txt`, `...VerifierBefore...`; scoped streamer delta: `outputs/G08_R2_P14_StreamerDelta_20261001.json`. Senior review checks the read-only candidate queries and every existing early reservation return; moving an unused query does not change a selected worker or authority.

First fixture failed because its synthetic continuation lookup lacked the production resolver's authored TerrainData dependency. This was corrected on detached objects before the production repair. The failure is preserved at `Logs/G08_R2_D37_AdmissionBefore.tsv.error.txt`; it is not a runtime failure or a scheduling repair attempt. Analysis requests use absolute Windows paths and atomic closed-file publication. A native refresh shortcut timed out; the Editor was rediscovered, and fresh capture plus heartbeat established Edit/idle rather than assuming the input succeeded.

Before: `Logs/G08_R2_D37_AdmissionBefore.aggregateadmission.tsv`, MVID `68fb34ec-1076-4091-a7c6-1a1983242002`;288 expected decisions PASS,200 unrelated scans;450 denied benchmark calls/900 scans/3.261 ms. After: `Logs/G08_R2_P14_AdmissionAfter.aggregateadmission.tsv`, MVID `264960b7-721b-448c-858c-394d695edca8`;same288 decisions PASS,0 unrelated scans;450 calls/0 scans/0.058 ms. Case matrix covers six stages, absent/live/preparation candidates, spent/available budget, spent/available dispatch reserve, activation permission and open/expired lifecycle deadline. Before/after actual decisions match288/288. Benchmarks use the actual predicate delegate and256 queued detached coordinates; these are fixture measurements, not production frame estimates.

Final compile occurred outside Play; fresh PlaySafe Console0 errors/0 warnings and idle compile/import heartbeat. Current Editor PID26608/Unity6000.3.2f1/DX12, maximized Game1278x637/scale0.79/Play Focused. The previous PID28040 is historical; the agent did not restart Unity. Final main DLL `11F1A5FE848CF276009025ACE9392BD1CE69C8D092E0585B1470C07722EEC549`, Editor DLL `1A44CE206BDA1411CDA6D9F72210CBD4045260E272D8211223D4FE2C9AAB22B2`, streamer `F0AA983F7799F516EB7681C57748F3C5AB3CE283746C9E805AB3BB9A5F74014E`, verifier `19D8C39D927224D86FB83E55F6DE99A1D387AF062CBBC6C24B77F8F3DBA23A33`.

## Focused acceptance and remaining matrix

One clean original PlaySafe/title/selected-save Continue witness is prepared in `outputs/G08_R2_P14_Armed_20261001.json`; profiler/binary/history/stacks/deep/GPU/visual and candidate observers/motion overrides off. No preparation time, view/content/quality reduction, Editor restart or cache clearing. Runtime identity must confirm the original disposable profile/world/seed/accepted V2. Governing input duration2.45 s in the verifier; input and coast maximum gates use `StreamingFrameBudgetSeconds=0.05`, without the separate inherited G07 tolerance. Capacity remains256.

Initial setup selected a different ordinary save after the externally reopened Editor: profile6f59f0599a2349b980f2113c685e7cb4/seed0a472740. The agent stopped before gameplay presentation or the focused witness, preserving `outputs/G08_R2_P14_InvalidSelectedSave_20261001.json`. The observer copied the old D36 report because no new witness existed; that copy is explicitly INVALID (`Docs/G08_R2_P14_INVALID_Stale_D36_Copy_2026-10-01.md`). The observer now rejects stale receipt timestamps. This is not a failed repair or a measurement retry. The next normal title session selected the existing fixture via its visible fifth save row, cross-checked against manifest index4/profile3ccfff4df9a1479884061e51940e4457/update11:18AM, then used the ordinary Continue marker. No fixture reset, regeneration, Editor restart or cache clearing was performed.

## Completed clean witness — FAIL

Runtime20:09:08.9635454–20:09:32.7847618UTC. Immutable receipt: [P14 clean witness](G08_R2_Speed260_2026-10-01_200932_P14_Clean_Receipt.md). Executing MVID/main DLL/source hashes match the final identity above. Profile3ccfff4df9a1479884061e51940e4457/worldyq-world-0284d4409a720743c13eb56e/seed4bb221dc/accepted V2c1d6ce9d3e06f760/schema7 match the original fixture. Profiler/binary/stacks/deep/trace/history/added observers off. Input and coast each have0 unfocused frames. Watch records112 observations/86 Play rows across the stopped setup and valid session,0 compiling-in-Play; native watcher is suspended during Play. Five watched source/assembly hashes match returned Edit; the two save-owner hashes remain unchanged. Owned watcher session21087 exited0; fresh native capture confirms returned Edit. No unchanged retry or second gameplay run follows this result.

| Phase | Frames | Median / P95 / maximum | Result |
| --- | --- | --- | --- |
| Original2.45s input |72|31/51/121ms|FAIL maximum and zero fallback |
| Entire natural coast |289|26/40/1452ms|FAIL maximum and complete visible readiness |

Coast11.621s/1293.2m/final0.4m/s/settled/input released; normal acceleration28/deceleration34 restored. Walk/run/dash/reversal/diagonal/rapid turns, abrupt and real-elapsed6→260 transition, boundary crossing, input visible/turn coverage and ground coverage PASS. Eight synchronous fallback attempts/publications FAIL; constant-input and direction-change gates FAIL despite both transition masks63. No traversal blocks or fall recoveries. This is a focus-valid clean failure, not a diagnostic performance certification.

First incomplete coast cell `(-2,11)` at frame8547 has collision and appearance ready, but required structure/overlays/ecology/activation incomplete. Its current content epoch10/attempt5/restarts4 began about0.011s before observation; prior preemption epoch6 was7.323s earlier by(1,11). Old collision age7.911s is not this current content lifetime. This snapshot cannot prove erroneous cancellation, queue starvation or intrinsic capacity, and does not reopen the exhausted scheduler loop.

Coast maximum wall frame8533 is1452ms; arrival-based recorder values show main25ms/streaming12ms/terrainDispatch4ms/no observed collection, but have no exact native association. Semaphore recorder peaks are likewise insufficient to attribute this wall interval or identify a driver fault. Next bounded question is the actual native/coroutine/Editor owners of final-P14 maximum8533 and representative over-budget intervals, plus the versioned lineage of whichever cell first fails in a diagnostic replay. Competing explanations are project work, allocation/collection and external/native waiting. One opt-in original witness with explicit boundary metadata and bounded cell history would distinguish these; analyze its selected maximum/representative intervals and first failed lineage before another capture. It has not been started.

Terminal census8749: live/cached physical155, reservations210, capacity256,8 physical without reservations/63 reservations without physical, appearanceWorkers3. Sets overlap and a terminal sample does not prove lifetime bounds. Fresh returned Console has4 runtime error records; inspected visible records report `Triggers on concave MeshColliders are not supported` at16:08:20 local, during startup before the witness. No compiler errors are inferred from these. Their exact caller and link to streaming failures are NOT VERIFIED; no incidental collider change was made.

P13→P14 clean distributions differ in trajectory, inventory and Editor session. The detached benchmark establishes removed work; it does not isolate runtime improvement, explain the new stall or prove deadline closure. Conservatively count ONE unsuccessful focused closure of the candidate-scan cause, with scan removal verified and residual failure attribution unresolved. Scheduling/reservation remains EXHAUSTED; visual-write closures remain TWO.

| Required R2 row | Final P14 status | Evidence boundary |
| --- | --- | --- |
| Accepted-save startup and starting envelope | PASS focused | Ordinary selected-save Continue/current executing identity |
| Cold startup | NOT VERIFIED | Separate required row |
| Ordinary controls and rapid turns | PASS focused | Production motor/input |
| Abrupt6→260 and boundary | PASS focused | Original input/real elapsed transition |
| Constant segments/direction contract | FAIL | Current focused receipt |
| Input complete visible/turn/ground | PASS focused | Every measured input frame |
| Coast complete visible content/ecology/activation | FAIL | First cell(-2,11),8547 |
| Input/coast maximum frame gates | FAIL |121/1452ms versus50ms |
| Zero synchronous fallback | FAIL |8 attempts/8 publications |
| No traversal block/fall recovery | PASS focused |0/0 counters; not zero-fallback proof |
| Sustained travel/full swept/lookahead matrix | NOT VERIFIED | Short witness is insufficient |
| Lifetime resource bounds | NOT VERIFIED | Only overlapping terminal census |
| Deliberate stall/honest recovery | NOT VERIFIED | Required final dependency row |
| Natural unload/revisit | NOT VERIFIED | Required final dependency row |
| Complete focused PASS/independent repeat | FAIL / NOT VERIFIED | First full PASS absent |
| Full R2/R3 admission | FAIL / gated | Multiple failing and missing rows |

Testing stops here following the user's gameplay stop boundary. The goal remains incomplete; no R1 recertification or R3 run. Preserve the current patch and receipts for the next bounded diagnosis.
