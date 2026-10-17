# G08-R2 D33 — real visual assignment redundancy

## Bounded question

D32 established that 29 hierarchy visual gates inside the largest lifecycle pass cost 8.925 ms and allocated 190056 bytes, with 166800 bytes in Editor material-property-drawer callback paths. Source already gates hierarchy scans on owner visual-state transitions. D32 cannot prove whether individual setters repeat a correct value or perform required transitions.

D33 counts the actual Renderer/Terrain assignments already at the requested value during one original-window binary-only PlaySafe/title/Continue run. Every original write, scan, order and readiness rule is preserved. Added property reads run only when the binary observer flag is enabled; clean witnesses do not enable it. The existing capture emits six cumulative counters under the same GUID/frame-boundary metadata, then disables the observer on disposal. Extraction reads saved metadata in Edit. No logging per assignment, extra movement, preparation, history, stacks, GPU collection, or resource-limit change.

Counters: gateCalls, rendererWrites, rendererAlreadyRequested, terrainWrites, terrainAlreadyRequested, rootScans. Difference successive cumulative rows for exact measured intervals; totals at the first boundary can include work before it and must not be called an input-phase interval. These counters measure assignment redundancy, not its isolated CPU saving. D32 paths establish the visual-update cost; a guarded-setter proposal still requires identical final hierarchy state and fresh clean validation.

## Status

Observer compilation passed outside Play, main DLL timestamp 14:27:06 UTC, fresh native Console zero errors/known CS0162 warning. Compiled/armed identity in `outputs/G08_R2_D33_Compiled_20261001.json` and `...Armed...json`. Current normal Continue run is being observed through PID 28040 and `outputs/G08_R2_D33_Watch_20261001.ps1`; no source editing in Play.

## Completed evidence

[Immutable receipt](G08_R2_Speed260_2026-10-01_143025_D33_BinaryOnly_Receipt.md), 14:30:02.9755352 UTC through terminal 14:30:25 UTC. Returned to Edit, all five source/DLL hashes matched arming. Actual native screenshot confirmed idle PlaySafe. Same PID/render/fixture/world/accepted V2 as D32, zero unfocused input/coast frames, no compilation in Play. MVID `0b3cd832-cf2e-4385-b45c-7dca74ac0330`, main DLL `17AC667411533A64BA52C9F46462605E29D5D9EF64305AFE23A4CFF0452DAA85`, streamer `AD5791874292DCE1E8F7778F5CF635E611994B41AC4248183776223CDEEB770F`, verifier `79C75B4CA7DBA5F76C3ECA05530C966F481E798BDA0EEF9A2F7D2F738A92BD18`.

Raw `Logs/G08_R2_FrameTrace_20261001_143012_506.raw`, GUID `cf5f5c43-725d-46ba-a571-00041dc71010`; `.visualwrites.tsv` contains 349 metadata boundaries/348 paired intervals. First boundary counts (20 gates/466 renderer writes) are excluded from measured totals. Offline results: `outputs/G08_R2_D33_VisualWriteCounts_20261001.json`.

| Paired phase intervals | Renderer assignments already correct | Terrain assignments already correct |
|---|---|---|
| Input 66 | 148 / 1119 (13.23%) | 52 / 73 (71.23%) |
| Coast 282 | **2443 / 6440 (37.93%)** | **546 / 720 (75.83%)** |

Matched interval Unity5033/native3476, wall49.878 ms: 33 hierarchy gates/399 renderer assignments, **all399 already correct**;31 terrain assignments/15 already correct. Two separate owner-pass scopes10.281 and8.221 ms exist in that frame; do not add them to wall time. Unity5095/native3538, wall61.955 ms:62/62 renderer assignments already correct,6 terrain assignments/4 already correct, owner scopes5.500 and7.042 ms. These counters demonstrate actual redundant native writes. They do not quantify their isolated CPU saving; D33 adds diagnostic getter reads.

Diagnostic input67frames34/51/52ms, coast282frames33/53/73ms,10.189s/1329.3m/settled0/restored28/34. Ground/visible/turn/zero fallback PASS, profiled transition/direction/constants and timing FAIL. Not clean certification or a new production repair attempt. Four known runtime collider errors; no new failure claimed from a stale Console count.

## Repair decision

D32 established hierarchy update cost and material-drawer allocation linkage; D33 now establishes real already-correct assignments. P12 will guard individual Renderer/Terrain enabled setters while retaining every hierarchy scan, target state, existing publication barrier, owner budget, and required readiness stage. Expected measurable reduction is native setter count for already-correct state, not a promised full frame-time PASS. The narrow regression must cover all mixed states, inactive descendants, overlapping roots, repeated application and independent collider state; changed properties still receive their real setter.

No more broad unchanged trace is justified. P11 clean remains FAIL, independent clean repeat and full R2 missing; R3 remains gated. Scheduling/reservation repair count unchanged.
