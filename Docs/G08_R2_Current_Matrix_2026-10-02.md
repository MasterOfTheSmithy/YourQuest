# G08-R2 current evidence matrix

## Accepted boundary

**R2 PASS BY USER OVERRIDE on 2026-10-03; R3 admitted.** The user instructed "Force label r2 as pass and continue". [Decision receipt](G08_R2_User_Override_2026-10-03.md). Runtime evidence remains partial: P25F focused PASS with the explicit 51 ms exception; P25R independent repeat FAILED. The row classifications below describe measured evidence and are preserved.

Authoritative decision: `Docs/G08_R2_P25F_Accepted_Focused_Pass_2026-10-02.md`. Raw run: `Docs/G08_R2_Speed260_2026-10-02_202156_P25F_Background_Diagnostic_Receipt.md`. Build/world/conditions and all 13 matching source/build hashes are recorded there and in `outputs/G08_R2_P25F_ReturnedAudit_20261002.json`. The raw automatic 50 ms FAIL is preserved. No threshold or movement requirement changed in code.

## Row reconciliation

| Required R2 row | Current evidence classification | Result / missing evidence |
|---|---|---|
| Accepted-save Continue and initial required, prepared, visible coverage | Reusable P25F runtime evidence on identified source | PASS: acceptedSaveTerrainStartup, initialRequiredCoverage, initialPreparedViewEnvelope, initialVisualCoverage and initialTraversability |
| Cold/new startup | Missing current dedicated evidence | NOT VERIFIED by loading an accepted terrain snapshot |
| Walk, run, dash, reversal, diagonal, rapid turns | Reusable P25F runtime evidence | PASS: six production motor probes; one authoritative motor |
| Abrupt 6→260 m/s boundary input | Reusable P25F runtime evidence | PASS: transition, boundary, all directions and constant input segments |
| Complete visible and turn coverage during input | Reusable P25F runtime evidence | PASS on this run |
| Complete visible coast coverage and grounded traversal | Reusable P25F runtime evidence | PASS on this run; zero blocks, fallback attempts and recoveries |
| Natural coast distance/settling | Reusable P25F runtime evidence | PASS: 1151.778 m in 9.183 s; no shortened coast |
| Input/coast frame maxima | Reusable runtime evidence plus explicit user decision | Input45ms PASS; reported coast51ms USER-ACCEPTED PASS. P50/P95 input30/43ms, coast29/39ms |
| Independent focused repeat on final source | Current P25R clean runtime evidence | FAIL: input62ms/coast52ms and first coast cell(13,6) required ecology incomplete; source/build hashes match P25F |
| Sustained travel beyond the short focused scenario | Missing current dedicated evidence | NOT VERIFIED; the stress distance alone does not cover longer ordinary travel or all demand regimes |
| Complete physical-owner/work lifetime bounds, queues and painters | Diagnostic/partial observations | NOT VERIFIED for the full required window; heartbeats and configured capacity are not a complete lifetime census |
| Deliberate stall, explicit failure and recovery | Historical diagnostics / current local regressions only | NOT VERIFIED on final affected source; no injected-stall phase in P25F |
| Unload, return, republish and retained state | Missing current dedicated evidence | NOT VERIFIED by P25F |
| Publication/cancellation prerequisite after P23/P24 | Local checks plus normal-runtime evidence, not full fault witness | P23 retirement771 checks and P24 handoff205 checks provide narrow evidence; final-source fault/replacement proof is not supplied by these alone. Any unsupported R1 dependency must be reported to its owner rather than silently recertified here |
| Terrain/route/water/placement visual quality | Separate active defects | NOT VERIFIED: user reports mapping faults; live view confirms abrupt terrain walls. Tree fallback fixed with77 Edit checks, close visual grounding still open |
| R3 physical-world itinerary | Outside this R2 run | NOT RUN; full G08 completion not claimed |

The focused verifier explicitly exits after its motor witness and reports `remainingR1AndG08Rows: NOT_RUN_FOCUSED_SCOPE` (`YQSemanticChunkRuntimeVerification.cs`, focused branch). Thus no passing motor metric can certify the omitted phases. Older source identities and diagnostic runs remain evidence of local causes, not replacements for the missing final-source rows.

## Next work and scope

The user's partial-handoff decision makes the accepted focused result a usable stopping boundary for this performance investigation. The single independent repeat is now complete and failed; details are in `Docs/G08_R2_P25R_Repeat_Result_2026-10-02.md`. Do not restart the exhausted hash or scheduler loops or run another unchanged witness for a lucky result. Full R2 closure requires a bounded decision for the recurring ecology-readiness failure and current frame attribution, then the missing/affected rows above.

Current product priority is terrain cohesion under `Docs/Terrain_Cohesion_Checkpoint_2026-10-02.md`: close tree/root evidence, river/channel mapping, connected settlement paths and realistic mountain composition. Mountain reshaping awaits the user's separate-preview versus current-world-migration choice. Saves and accepted terrain remain intact pending that choice.
