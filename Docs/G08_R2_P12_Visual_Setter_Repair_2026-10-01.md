# G08-R2 P12 — guard already-correct visual setters

## Cause and bounded change

[D32](G08_R2_D32_Lifecycle_Subcall_Attribution_2026-10-01.md) measured hierarchy visual updates at8.925ms/190056allocationbytes within an11.800ms lifecycle pass;166800bytes belonged to material-property-drawer callback paths. [D33](G08_R2_D33_Visual_Write_Redundancy_2026-10-01.md) counted2443/6440coast renderer assignments and546/720terrain assignments already at their requested value. A matched interval had399/399already-correct renderer writes. The capture maximum had a separateGCcause; this repair does not claim that visual updates explain every slow frame.

P12 changes only `YQPlayerFollowingSemanticChunkStreamer.SetStreamedVisualsEnabled`: read current enabled state and invoke the native setter only when it differs. Full hierarchy enumeration, inactive descendants, hierarchy exposure transitions, renderer targets, terrain-specific preview reconciliation, collider state, activation, publication barriers, owner/concurrency/queue/cache limits, content, movement and accepted world remain unchanged. Existing per-owner exposure caching remains in place. No serialized field/public API/save contract change.

Diagnostic observer counters now distinguish considered assignments from performed native writes. Binary metadata/export remains opt-in and absent from clean witnesses; added clean property reads are part of the production repair. Original D33 source snapshots are `outputs/G08_R2_P12_StreamerBefore_20261001.cs.txt` and `...VerifierBefore...cs.txt`.

Expected result: remove already-correct native setter calls while matching final renderer/terrain state exactly. The narrow Edit fixture adds64cases covering mixed renderer/terrain states, inactive descendants, overlapping hierarchy roots, idempotent repeat and independent collider/active state. Existing42publication/component cases rerun because their gate method changed. The benchmark retains frozen old setters and current production calls; reflection/fixture overhead is not production CPU and must be separately qualified.

## Status

Source applied in confirmed idle Edit. Compilation PASS, main DLL timestamp14:34:01UTC, fresh native Console zero errors/known CS0162warning. Executing regression MVID `b55c1147-3126-4540-b0dc-1afad337c75d`. Narrow Edit receipt `Logs/G08_R2_P12_VisualGateRegression_20261001.optionalcomponents.tsv` at14:34:58.8219241UTC: **PASS106cases**, including64new visual setter parity/idempotence/actual-write-count cases and42existing publication/component cases. Fixture-generated primitives/data only; no accepted asset/profile/world mutation. Null roots, hierarchy-wide inactive renderer visibility, collision classification, cached/live ecology and hidden/disabled/destroyed roots retained.

## Clean focused result — FAIL

[Immutable P12 clean receipt](G08_R2_Speed260_2026-10-01_143741_P12_Clean_Receipt.md),14:37:18.9948586–14:37:41.6821389UTC, normal PlaySafe/title/Continue. Profiler/binary/observer/stacks/deep/history/GPU/motion overrides off, same accepted fixture, original input/full natural coast, zero unfocused input/coast frames.52Play observations/no compilation/watcher inactive; all five returned source/DLL hashes match arming. Back in Edit, requests consumed. Current complete identities/status: `outputs/G08_R2_P12_Compiled_20261001.json`, `...Armed...json`, `...WatchStates...jsonl`, `...ReturnedEdit...json`.

Final streamerSHA256 `24AE97E175576B2DA1BCFC536562AB77B59F9355C242CB8BDBF8671BB722FAF0`, verifier `753B7D10BF9C23DC0FCB4E6C69B27D4AEEF6AB0B16777BCBA8769345C61582D8`, mainDLL `89DC90F31C34DDB01572B64E61473F5991398D1067763CBEE0A787ED9B1995EE`, EditorDLL `5CD84660D8AB3E788F73C73B5F12D0C9E876BFC7DD5C8DA714DE67FC06776241`, MVID `b55c1147-3126-4540-b0dc-1afad337c75d`. Hasher unchangedP11 `D7F12141E364D99E88A42D0A65305C4CB5B028D7ADAA24FD09F8F2A038C60BD1`; player/profile IO unchanged. Same PID28040/Unity6000.3.2f1/DX12/Ryzen5800X/RTX5060/1278x637/scale0.79; profile3ccfff4df9a1479884061e51940e4457/worldyq-world-0284d4409a720743c13eb56e/seed4bb221dc/V2c1d6ce9d3e06f760/schema7/view512/owner256.

Input76frames median/P95/max30/44/48ms: PASS timing and full visibility. Coast284frames35/56/71ms: FAIL timing and visibility;10.616s/1454.7m/settled0/normal28/34restored. All ordinary controls, abrupt6→260, constant segments, directions, boundary, ground safety and zero fallback/recovery passed. Coast maximum5069 reports lateLifecycle13ms/totalstreaming24ms, no collection increment; native ownership is not established from recorder arrival. Managed allocated-zero counter remains invalid.

First coast exposure miss `(13,-1)`, snapshot5061: collision/content/overlays/ecology ready, appearance/visual/activation false; hardage5.262s/collision1.547s/contentstart0.984s/content0.645s. Target painter active, no retry; active state-1/activationTarget-1. No full cell history was armed in this clean run. Existing budget/dispatch histories retain limited decisions, not the full prior stage lineage; no starvation, dropped-work or sustainable-capacity conclusion follows from the snapshot. Do not reopen the exhausted scheduling/reservation loop on this evidence.

Terminal owner census5182:220livePhysical/256,255reservationOwners,8physicalWithoutReservation,43reservationWithoutPhysical,81active/106retained/364unloaded. These are overlapping sets and one snapshot, not bounded lifetime PASS. Four known runtime collider errors; completed production LLM response latency not verified. No change to save/world/content authority.

| R2 row | Current evidence and status |
|---|---|
| Static/compile and affected component/visual regression | PASS compile plus106cases; exact hierarchy state, active/collider ownership and actual guarded-write counts. |
| Ordinary Continue / accepted terrain startup / starting envelope | PASS in focused witness; cold startup remains NOT VERIFIED. |
| Walk/run/dash/reversal/diagonal/rapid turns | PASS focused. |
| Distinct abrupt6→260/directions/constants/boundary | PASS focused. InheritedG07 speeds remain separately unverified here. |
| Input complete visible/turn/ground and50ms maximum | PASS76frames/max48ms. |
| Full natural coast complete visibility/appearance/ecology/activation | FAIL first(13,-1), incompleteappearance/activation; ground/requiredcontent/overlay/ecologyready. |
| Coast50ms maximum | FAIL284frames/max71ms. |
| Zero synchronous fallback/readiness intervention/recovery | PASS both phases; no traversal blocks. |
| Sustained extended travel/swept/lookahead/full demand classes | NOT VERIFIED by this focused witness; full useful complete-cell throughput/latency inventory remains missing. |
| Owner/concurrency/queue/cache resource lifetime | NOT VERIFIED; terminal physical/reservation census is not a lifetime proof. |
| Deliberate stall/honest recovery and natural unload/revisit | NOT VERIFIED on final affected source; focused recovery count0 does not exercise deliberate stall. |
| First complete focused clean PASS / independent repeat | FAIL / NOT RUN. |
| R1 recertification and R3 itinerary | OUT OF SCOPE; R3 remains gated. |

Observed P11→P12clean maxima56/61→48/71ms, but coast trajectories/inventory differ; no isolated whole-frame improvement claim. Count **one unsuccessful closure attempt** for the distinct redundant visual-setter cause:106narrow casesPASS/fullfocusedFAIL with renewedcoastvisibilitymiss. Diagnostic additions do not reset the exhausted scheduling/reservation loop.

## Next bounded attribution

Remain at the visual/lifecycle owner before another production repair: determine whether the remaining long hierarchy pass costs enumeration, actual changed renderer setters, Terrain lookup/read/setter/native initialization, or many budget-exhausted activation-slice visits. Existing D32/D33 do not distinguish those exact operations on P12. If a replay is needed, use one original-window binary diagnostic with those subcall scopes and bounded first-failed-cell history, freeze its actual first cell, and analyse before another patch. No unchanged clean repeat. Preserve all publication checks, content, controls and limits. Full R2 remains FAIL/R3 gated.
