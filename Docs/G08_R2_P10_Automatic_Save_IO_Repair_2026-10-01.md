# G08-R2 D27 / P10 — automatic player-save IO

**Status: focused FAIL. Full R2 is incomplete; R3 remains gated.**

## Finding and owning boundary

[D26](G08_R2_D26_Frame_And_Readiness_2026-10-01.md) matched `PlayerLocationReporter.Update` to 10.423 ms at its input maximum and 22.949 ms at its coast maximum, with approximately 121 KB of allocations per callback. D26 was profiled and unfocused, so these are diagnostic attribution, not clean timing certification. The reporter calls the existing player and world state managers on its ordinary cadence.

D27 isolated those calls in inactive, detached Edit fixtures. World region/collection/identity normalization was approximately 0.15–0.17 ms per current-region call. Actual saved-player location without autosave was approximately 0.02 ms. Automatic player saving, including its synchronous atomic file write, took 7.611825 ms per fixture call. The earlier hypothesis that world normalization dominated was rejected. The demonstrated repair boundary is automatic player-save IO, distinct from the exhausted streaming scheduler/reservation loop.

Expected improvement: remove the automatic atomic disk write from movement callbacks while preserving snapshot capture, write ordering, backups and persistence boundaries. This does not eliminate snapshot serialization or prove every slow frame has the same cause.

## Production change and preserved contracts

- `Assets/Assets/Scripts/Data/State/Player State/PlayerStateManager.cs`: one automatic writer task accepts immutable JSON and captured primary/backup paths. Normalization and serialization stay on the Unity thread. The worker uses only `System.IO` and those strings. An in-flight writer prevents a competing automatic writer; the next ordinary due update captures current state after completion.
- Explicit save/snapshot, reload and teardown synchronously drain pending automatic IO. Completed-task errors are observed and reported on the Unity thread. File encoding, `.tmp`/`File.Replace`/`.bak` behavior, JSON settings, save schema and persisted fields are unchanged.
- `Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs`: the actual profile-load path drains the old player writer before replacing either active projection. Paired commits already use the snapshot boundary, which now drains the writer. A shared internal transition helper permits a detached regression of that same barrier.
- `YQSemanticChunkRuntimeVerification.cs`: Edit-only location/save attribution and barrier fixtures. Clean runtime instrumentation remains the required acceptance instrumentation; added binary/history/GPU/motion capture was disabled.

No new persistence authority, gameplay player, streaming owner or scheduler was introduced. Accepted identity/content, seed, V2 authority, view distance, stress input, camera freedom, publication stages and configured owner/concurrency/cache limits are preserved. Explicit persistence barriers may wait for IO; this is necessary for ordered completion. The state-level profile barrier fixture is not visible two-profile UI certification.

## D27 fixture evidence

The immutable accepted-world fixture was `outputs/G08_R2_AcceptedTerrainAllocationFixture_20261001.json`. Inactive objects did not execute production `Awake` or install singleton owners. The actual disposable profile's paired revision 38 / commit `9da2c34b86c746c3afb3629cf8130cf3` supplied a detached player document. Player checksum `c9141eed065a169171447ccb07ec320c1f5d247a808322d3d6ba4682505f6ae3`; world checksum `918b78c862c9b655f3816c00b4e61e779d229411739f603cbbcd9f71ee50aff4`.

Actual atomic test writes went only to checked absolute workspace paths under `outputs/`. Accepted plan JSON, persistent manifest/paired revision bytes, singleton owners and eight expected world touches were checked unchanged. No fixture write replaced an ordinary active profile document.

| Eight calls per scope | Before mean ms | P10 mean ms |
|---|---:|---:|
| Current world region | 0.154288 | 0.147650 |
| Actual player location without autosave | 0.023113 | 0.019750 |
| Snapshot-only serialization, including first-use serializer work | 3.215038 | 3.178138 |
| Actual automatic-save callback | 7.611825 | 0.630750 |

The P10 callback scope excludes its separate explicit flush; actual disk completion is still required and was checked. These isolated Edit means measure callback cost, not production frame maxima or an overall speedup.

- Before: `Logs/G08_R2_D27_LocationIO_20261001.locationnormalization.tsv`, 13:23:23.4853746 UTC, MVID `534a6baf-98bc-47fe-a96c-2aca792fec91`, DLL SHA `11FCEE59BBB5C0FBC7C02D0200ABE7F81EDD58C035DA6CF0FC2558811656EA06`.
- Before raw: `Logs/G08_R2_FrameTrace_20261001_132322_927.raw`, GUID `79b2e27c-b289-42c7-8bd0-e582b4361323`. Native frame 1593 / Unity 14; all-thread alias `Logs/G08_R2_D27_IONative_20261001.samplethreads.tsv`; analysis `outputs/G08_R2_D27_IONative_20261001.json`. Automatic-save scope: 4,003 events / 938,418 bytes across eight calls. Whole fixture allocations include detached world cloning/serialization; they do not describe gameplay or establish zero allocations after P10.
- After: `Logs/G08_R2_P10_LocationAfter_20261001.locationnormalization.tsv`, 13:28:39.8095422 UTC, final P10 MVID below. Raw GUID `fe34c8ca-4339-4a72-86ae-fca881f2e71c`, native 1639 / Unity 21, `Logs/G08_R2_FrameTrace_20261001_132839_217.raw`. Profiler/log settings were restored before clean runtime arming.
- Eighteen barrier cases PASS: immutable captured state, registered pending task, snapshot wait/slot cleanup, explicit-save ordering, previous-version recovery backup, actual profile transition helper, captured paths despite later name changes, replacement document validity, one in-flight slot, completion cleanup and observable injected IO failure. Arming/build receipts: `outputs/G08_R2_D27_IOArmed_20261001.json`, `outputs/G08_R2_P10_RegressionArmed_20261001.json`.

## Final source and compilation

Fresh main compile 13:28:04.7601503 UTC; Editor compile 13:28:05.7454646 UTC. Compilation/domain reload succeeded, zero compiler errors; known CS0162 warning in `YQWorldGenerationV2ContractTests.cs:385`. Relevant patch whitespace check passed. No C# edit or compile during Play; all source/DLL hashes matched again after runtime.

| Component | SHA256 / identity |
|---|---|
| PlayerStateManager | `CAF2799E816B5C801CCFAFF26CD461B1A27428814895A61458F3F9061D6DEC56` |
| YQProfileSaveSystem | `025455150082CD29A79313138CEFD061653BF86708CCF3A20E8D2B87ABA59CC9` |
| Verifier | `D2EA17A906BFC6762C0471C9F6648EA60DC897E69E44BF105168451208205BE4` |
| Streamer, unchanged from P09 | `7EAB4A940ADB6D33E3E1E6F9687381FDC5DE911E1285C0D1908541827961AE7C` |
| Assembly-CSharp DLL | `29B96B074ED3DB1D3975A099F65D63EBAAC3BCC75332EAD60EA361F6368EDBB6` |
| Executing MVID | `9f0bcd17-6b6e-4118-a0f7-6416d1a2b5a7` |
| Assembly-CSharp-Editor DLL | `849EBA7297D94870708628BB23385500BA76247259EB25F4633B1113EE73F48D` |

Before-file snapshots: `outputs/G08_R2_P10_PlayerBefore_20261001.cs.txt`, `...ProfileBefore...`, `...VerifierBefore...`. Existing unrelated dirty changes remain preserved.

## One clean focused result

[Immutable clean receipt](G08_R2_Speed260_2026-10-01_133239_P10_Clean_FAIL.md), 13:32:18.9968197–13:32:39.4227432 UTC. Ordinary PlaySafe/title selected-profile Continue through the existing marker handler; normal startup and accepted terrain restore, unchanged original 2.45-second stress input and full normal coast. No extra warmup/prefill, altered input, camera restriction or cache clearing. The user-opened Editor was not restarted.

Unity 6000.3.2f1 / DX12, PID 28040; Ryzen 7 5800X / 16 logical CPUs / 32648 MB RAM / RTX 5060; Game 1278×637 / scale 0.79. Required visible radius 4 / 512 m, prewarm 5 / prepared radius 7, owner capacity 256 unchanged. Profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted/validated V2 `c1d6ce9d3e06f760`, state schema 7 / topology 9 / gate 2.

Profiler, binary log, allocation call stacks, deep profiling, extra history, GPU observation and coast-motion override disabled. Both measured phases recorded zero unfocused frames. Native screenshots showed ordinary startup and returned Edit. External recorder captured 46 Play observations, all with source watcher inactive and compiling false; watcher resumed in Edit. Arming `outputs/G08_R2_P10_Armed_20261001.json`; status recorder `outputs/G08_R2_P10_WatchStates_20261001.jsonl`.

| Phase | Frames | Median | P95 | Maximum wall | Governing 50 ms maximum |
|---|---:|---:|---:|---:|---|
| Original input | 77 | 31 ms | 41 ms | 44 ms | PASS |
| Normal coast | 291 | 28 ms | 39 ms | 53 ms | FAIL |

Ordinary walk/run/dash/reversal/diagonal/rapid turns, abrupt 6→260 transition, boundary crossing, direction changes, input complete visibility/turn coverage PASS. Coast ground and full visible appearance/content remained ready on every measured frame. Zero synchronous fallback attempts, traversal blocks or fall recoveries. Coast 8.547 seconds / 1183.3 m; restored acceleration/deceleration 28/34, settled at 1.0 m/s. `speed260CoastCoverageGate` is composite and failed for timing despite `ground=True visible=True`.

One retained over-budget coast interval at Unity 5049: wall 53 ms, main-thread recorder 27 ms, behaviour recorder 20 ms, streamer work 11 ms / lateLifecycle 5 ms; collection counters 0/0/0. Neighbours 5048/5050 measured 29/34 ms. This clean receipt lacks matched saved native boundaries. Recorder arrival does not establish exact same-interval CPU/native ownership; managed allocation counters returning zero remain invalid evidence of zero allocation. Do not subtract these overlapping/differently timed values to infer a driver fault or exact unexplained cost.

Terminal census at Unity 5204: 199 physical / 235 reservation owners / capacity 256; eight physical without reservations and 44 reservations without physical. These sets overlap and have distinct lifetimes; do not add them or certify the lifetime resource row from this sample. Height-task rate 54.3/s is not useful complete-cell throughput. Four existing concave collider trigger errors remained visible; inspected runtime log tail contained no new autosave/reload/profile failure or exception. The ordinary world-delta request was cancelled at run end after 8.54 seconds; completed response latency remains unverified.

P09 clean input/coast maxima were 59/about50 ms with a coast appearance miss; P10 is 44/53 ms without a content miss. Both were clean on the same accepted artifact, process and render setup, but final source, actual coast trajectory and persisted semantic inventory differ. This comparison cannot isolate overall patch speedup or attribute the remaining coast frame to automatic IO.

## Repair count, remaining rows and bounded next question

Count one unsuccessful focused closure attempt for this distinct automatic-save IO cause: narrow persistence checks PASS, full focused gate FAIL. No unchanged clean repeat is justified. The exhausted scheduler/reservation loop remains exhausted; this repair does not reset its count.

| R2 row | Status on P10 | Evidence / limitation |
|---|---|---|
| Accepted-save startup / initial envelope | PASS focused | Normal Continue/restore/initial readiness |
| Ordinary controls / abrupt260 / boundary / directions | PASS focused | Production motor witness |
| Input complete visible/turn/ground / zero fallback | PASS focused | Every-frame checks |
| Coast complete visible content/ecology / ground / zero fallback | PASS focused | Every-frame readiness, normal coast |
| Input maximum wall frame | PASS focused | 44 ms |
| Coast maximum wall frame / overall focused gate | FAIL | 53 ms versus 50 ms |
| First full focused PASS / independent repeat | NOT VERIFIED | No full focused PASS yet |
| Cold startup / sustained travel | NOT VERIFIED | Outside this focused scope |
| Full swept/lookahead readiness | NOT VERIFIED | Narrow turn/ground checks insufficient |
| Lifetime bounded resources | NOT VERIFIED | Terminal census only |
| Deliberate stall/honest recovery / unload-revisit | NOT VERIFIED on affected source | Missing final-source R2 evidence; no silent R1 reuse |

Next named question: what owns the remaining coast wall interval on final P10 source—another production callback/collection or Unity/native waiting? The clean maximum has no collection increment and only recorder-level attribution. One original-window binary-only diagnostic, with exact existing wall/native boundary metadata, can distinguish those explanations and verify whether the location reporter's IO removal appears in the actual runtime scope. Keep cell history/deep/call stacks/GPU/motion off; exit at original full coast; analyse actual captured maxima plus a sustained representative interval before any further repair. Do not assume Unity 5049 repeats, add preparation, revive scheduler work or repeat clean runs for a convenient PASS.

Full R2 remains FAIL. R1 was not recertified; R3 was not run.
