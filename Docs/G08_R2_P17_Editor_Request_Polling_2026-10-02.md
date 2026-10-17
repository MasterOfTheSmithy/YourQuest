# G08-R2 P17 — Editor request polling

Status: compilation and focused request regression PASS; clean gameplay witness **FAIL**.

## Demonstrated cost and change

D40 identified 25464 bytes/418 allocation events directly beneath the Editor hook each idle request tick, plus a separate 20.388 ms collection inside that hook during slow coast frame6626. Collection pressure is shared with other systems; this does not assign all GC cost to the hook or guarantee the50 ms gate.

D41 isolated the hook's28 named request paths in Edit.64 rounds of absent-file polling allocate1430656 bytes/23424 events with relative paths and1113856 bytes/19840 events with absolute paths. Absolute paths were slower in that first capture, so path pre-resolution was not adopted as the repair. Native allocation metadata is authoritative here: the managed allocation counter failed its4096-byte calibration and its zero readings are invalid.

P17 changes `Assets/Assets/Scripts/Generated/Editor/YQEditorAutoRefreshBootstrap.cs`: after the unchanged heartbeat/import handling, one nonrecursive `Temp/YQ_*.request` census and the existing out-of-Temp terrain request check determine whether there is any request to dispatch. Empty ticks return before polling every handler. Requests still use the original handlers/order, file payloads, startup gates and every-Editor-tick cadence. No watcher, cached pending state, throttle, verifier-specific branch, new gameplay owner or additional preparation. A census IO/access failure falls through to the original checks. Source watcher suspension and Play refresh protections remain unchanged.

`YQSemanticChunkRuntimeVerification.cs` adds the detached `marker-poll-traced` comparison and12 disposable request-detection checks. The fixture uses a fresh directory under outputs, outside the live request directory; it never selects/loads/saves a profile or dispatches gameplay. No production motor, streaming, world, accepted content, quality, budget, controls or camera change.

## Fresh verification

Both assemblies compiled and reloaded in Edit at16:07:51 UTC, with no fresh compiler error. An earlier fixture ran the previous runtime assembly while the Editor assembly had updated; that receipt (`Logs/G08_R2_P17_MarkerPoll_20261002.markerpoll.tsv`) was rejected as stale and retained. A source-watcher refresh then compiled both changed assemblies before the final fixture.

Final fixture: `Logs/G08_R2_P17_FinalMarkerPoll_20261002.markerpoll.tsv`; all12 checks PASS (empty directory, unrelated file, create/delete, rename in/out, case-insensitive Windows request name, external terrain marker and missing inbox). Raw`Logs/G08_R2_FrameTrace_20261002_160833_627.raw`, GUID`9195a9a4-3ff2-4f2f-bdaa-4b37addf6e06`, saved native frame410 export complete. Native allocation totals in `outputs/G08_R2_P17_PollAllocations_20261002.json`:

| Identical idle workload,64 passes | Allocated bytes | Bytes/pass | Elapsed with profiler |
| --- | ---: | ---: | ---: |
| Original28 individual relative checks | 1430656 | 22354 | 32.436 ms |
| Same paths resolved absolute | 1113856 | 17404 | 29.247 ms |
| New pending-request census | 168320 | 2630 | 7.183 ms |

The census cuts this measured allocation workload by88.2%. The fixture is not a gameplay frame-time prediction. It excludes status publication and downstream handlers when a real request exists. The original actual dispatcher includes more than28 checks, so its25464 bytes/tick are not falsely substituted for this measured28-path baseline.

One clean focused P17 witness has been armed through normal PlaySafe/title/existing fixture/Continue, unchanged P16 route, original stress input duration and natural coast, no added binary/history/stack/GPU/motion capture. Identity: `outputs/G08_R2_P17_Armed_20261002.json`; observer `outputs/G08_R2_P17_BackgroundStates_20261002.jsonl`. Do not count this as PASS until its final receipt and returned hashes are audited. This is a distinct request-polling cost, not a reset of exhausted scheduler/reservation or prior visual-write repair counts.

## Clean gameplay outcome

Run16:12:25.4702054–16:12:46.0870979 UTC. Immutable receipt `Docs/G08_R2_Speed260_2026-10-02_161246_P17_Background_Diagnostic_Receipt.md`; filename describes hook dispatch, while instrumentation was unprofiled and both measured windows had zero unfocused frames. Same accepted fixture and route v2. MVID `f44ca6c1-fb49-4bc2-a4b6-eb44bf9771db`, mainDLL `2620556522F50A4B7D685C172C15EA4BEB7DB055707D6C6FFEF13CED9A798BCC`. All eight returned source/DLL hashes match; audit `outputs/G08_R2_P17_ReturnedAudit_20261002.json`. Unity returned to idle Edit; no compilation observed in Play.

Input85 frames median/P95/max27/39/43 ms; coast301 frames30/40/67 ms. Coast1180.706 m versus integrated1181.045 m,9.481 seconds, settled at0.0 m/s, zero obstructed frames. All386 frame records retained. Abrupt6→260, boundary crossing, visible/turn/coast coverage, zero synchronous fallback, zero traversal blocks and zero fall recovery PASS.

Remaining failures: coast maximum67 ms >50 ms, and direction displacement mask62 versus required63 (forward-right diagonal missing despite input mask63). The receipt has insufficient exact direction-frame data to distinguish physical contact, integrated motion or sampling at that turn; no motor defect or test false positive is yet proved. Worst coast6443 increments collection counts1/1/1, but recorder arrival order does not identify the native collection scope. Compared with final P16 input42/coast87 ms, this run observes43/67 ms; that is not isolated proof of a20 ms causal improvement.

Request-polling closure attempt1 did not close the full focused gate; retained narrow allocation reduction is proven. No independent PASS repeat. D42 adds bounded eight-turn motion/contact snapshots, formatted only after timing, and an initial native boundary before the first input yield. It changes no input, preparation, thresholds or motor behavior. One binary-only replay is authorized to answer the remaining direction and native frame-cost questions; no luck-based clean rerun.
