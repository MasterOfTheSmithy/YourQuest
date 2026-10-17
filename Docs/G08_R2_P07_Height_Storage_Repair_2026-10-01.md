# G08-R2 P07 — reusable height-sampling storage

**Status: focused FAIL; full R2 remains FAIL. R3 is gated.**

## Changed and why

`YQGeneratedWorldTerrain.V2HeightSampler` now rents exclusive dictionary storage from a sampler-owned pool, clears before return, and retains at most eight empty buffers. Each active session keeps the existing4096initial capacity/8192entry limit. No accepted values or cache hits cross jobs. Double disposal is idempotent; sampling after disposal fails. Worker admission, view distance, ownership, pending queues, formulas, schemas and saves are unchanged.

D23 proved1,384,220/2,214,752bytes of allocations under named height scopes in selected slow input frames. A height collection10.923ms overlapped a provisional queue pass. P06's native four-job probe allocated2,222,444bytes after shoreline-array removal. Source allocated/discarded fresh dictionary backing arrays for every job. P07 addresses that storage churn; it does not establish every allocation caused the observed collection.

`YQContinuousWorldCellAuthority` gains a read-only cache-count accessor. `YQSemanticChunkRuntimeVerification` extends only the Edit regression with repeat and lease checks. Empty backing storage is a material lifetime change: up to eight cleared buffers remain until their sampler is collected, approximately4.5MB if all reach the original largest dictionary capacity. Full resource lifetime certification remains outstanding; active sample-entry and worker limits are unchanged.

## Fresh compile and narrow regression

Final compile/reload10:56:40UTC; Console0errors/1known warning. An intermediate automatic import saw a temporary helper reference while the coherent edit was being completed; final source removed it and subsequent compilation succeeded. No C# edit or compile in Play.

Assembly MVID`4d68155f-d576-419b-9b96-c6e39b79c216`, SHA256`82749CCEEDB73EB27DFE5AE3E297E18F86E92255F341E335D69AA8D55949DF1B`. [Build/source hashes](../outputs/G08_R2_P07_EditBuild_20261001.json).

Native Edit probe10:57:21UTC, saved captureGUID`36cdfbc4-146a-4837-be19-5dc9e9f99a2a`, profiler757. [Allocation analysis](../outputs/G08_R2_P07_EditAllocation_20261001.json).

| Identical four-job source scope | Native byte events | Bytes |
|---|---:|---:|
| P06 height sessions | 47 | 2,222,444 |
| P07 cold height sessions | 35 | 839,540 |
| P07 repeated height sessions | 8 | 352 |

Repeat events are four32-byte authority wrappers and four56-byte spatial wrappers; no dictionary backing arrays. Blueprint sample loop remains0events/0bytes. This is source-scope allocation evidence, not global allocation freedom. Managed allocation counters remain INVALID after calibration.

- All16,900nine-field raw samples and16,900authorityheights match immutable D22 bytes; repeat height bytes also match. Adjacent seams0mismatch;729maximum edge cases pass.
- Evaluations5001,4889,0,0; cache hits16112,13056,0,0; cached entries5001,4889,0,0. Counts match on repeat. Cache reuse is substantial in two grids, absent in the distant two.
-25lease cases pass:12simultaneous transient leases, actual eight-worker exclusive leases/512parallel height comparisons, eight-buffer overflow bound, double disposal, fault cleanup and use-after-dispose.
- Cold/repeat Edit height scopes221.255/205.974ms are not runtime timing certification.

## Clean focused result

[Immutable P07 terminal receipt](G08_R2_Speed260_2026-10-01_110213_P07_Clean_FAIL.md),11:01:50.8708920–11:02:13.4132202UTC. [Arming/setup/source hashes](../outputs/G08_R2_P07_Armed_20261001.json).

Ordinary PlaySafe/title/Continue restored the intended profile/world/accepted terrain. PID28040, Unity6000.3.2f1/DX12,1278x637/display0.79; same original input/full normal coast. Profiler/binary/deep/history/GPU/motionoff, zero unfocused input/coast frames. No Editor restart, cache clearing or added gameplay preparation.

| Phase | Frames | P50/P95/max ms | 50ms maximum | Complete visible |
|---|---:|---:|---|---|
| Input | 76 | 30/42/54 | FAIL | PASS |
| Coast | 296 | 33/51/69 | FAIL | FAIL |

Ordinary controls, abrupt/real-elapsed transition, boundary crossing, constant input, direction and turn buffer pass. Ground remains covered; fallback attempts/traversal blocks/fall recoveries are zero. Normal coast10.440s/1467.7m, acceleration/deceleration28/34, released input, settled0.6. `startGrounded=False` is not a separate failing predicate.

First coast visibility miss: cell(11,-2), frame5037. Collision/required structures/overlays/ecology are ready; appearance/root activation are incomplete. Biome/detail lanes have13budget denials over1.746/1.750s, detail worker wait recorded, retry scheduled. Owner/version/competitor snapshot is in the terminal receipt. High-volume history was disabled, so age/denials alone do not prove starvation, lost work or incorrect retention. This reproduces an appearance deadline class; it does not authorize another repair of the exhausted scheduling/reservation policy.

Input maximum frame4810records54ms wall/26ms streaming, no collection increment; another54ms frame4873has an increment and21ms LateUpdate. Coast maximum5106records69ms wall/19ms streaming, no collection increment. Recorder arrivals are not native same-frame attribution and must not be added. P07 final census203livephysical/255reservations/256capacity,8physicalwithoutreservation/60reservationwithoutphysical; sets overlap. Height task rate44.6/s is not complete-cell throughput.

P06→P07 clean observed input maxima139→54ms, coast70→69ms. Same process/render/setup for these two runs; actual coast trajectory/duration differs, and visible coast regressed. These observations do not isolate an exclusive CPU-speed effect. Narrow allocation/parity checks PASS; focused gate FAIL, including a new readiness miss. Record P07 as one unsuccessful focused closure attempt for storage churn; no repeat or second storage repair was attempted.

## Remaining

Current deficits are4ms input and19ms coast, plus the appearance deadline miss. First clean focused PASS, independent repeat, cold startup, sustained ordinary travel, full swept/lookahead readiness, complete resource lifetime, stall recovery and unload/revisit remain unsupported. R1 was not recertified and R3 was not run.

Next bounded frame-cost question: identify the persistent unnamed allocator seen both during D23 gameplay and a P07 Edit probe, without assuming thread-ID identity across reloads. Installed Unity docs confirm `Profiler.enableAllocationCallstacks`, `RawFrameDataView.GetSampleCallstack` and `FrameDataView.ResolveMethodInfo`; a short Edit-only idle allocation-stack capture can identify its source before another broad runtime trace. That capture would be diagnostic only and must restore profiler settings. No new scheduling change or unchanged lucky repeat is justified. Editor is idle Edit; all requests are consumed.
