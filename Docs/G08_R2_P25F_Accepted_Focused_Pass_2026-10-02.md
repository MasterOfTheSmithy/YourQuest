# G08-R2 P25F — accepted focused PASS

## Acceptance decision

The user explicitly accepted this run's reported 51 ms coast maximum: "51 ms can be called a pass", followed by "negligible". Therefore this witness is **focused PASS with a user-approved timing exception**. The original automatic result under the 50 ms threshold remains FAIL in the immutable raw receipt. No measurement, threshold code, input, visibility requirement or receipt was rewritten. This decision accepts this observed run; it does not establish an unrestricted tolerance for future measurements.

## Current evidence

Run: 2026-10-02 20:21:23.4714903–20:21:56.3587637 UTC, ordinary PlaySafe/title/existing accepted fixture/Continue. Receipt: `Docs/G08_R2_Speed260_2026-10-02_202156_P25F_Background_Diagnostic_Receipt.md`. Despite the inherited filename suffix, the receipt explicitly records profiler, binary logging, allocation stacks, deep profiling and trace request all false. Certification validity is VALID, with zero unfocused input/coast frames and all 378 measured frame records present.

Main assembly MVID `b8423c09-43e7-42cf-b0d1-36a1336eef97`; SHA256 `5DB31218E987F9D1C1678045AA91D13D6074F118D8C5C69EFB50F5E1B4F29FCE`. All 13 armed source/build hashes match after the run, no compile in Play, returned to Edit. Audit: `outputs/G08_R2_P25F_ReturnedAudit_20261002.json`; armed identity: `outputs/G08_R2_P25F_Armed_20261002.json`.

World `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted V2 hash `c1d6ce9d3e06f760`; Unity 6000.3.2f1, DX12, Ryzen 5800X, RTX 5060, viewport 616x264. Final source includes P25 plus the subsequent tree-grounding repair; this is not an isolated P25 speedup experiment.

| Focused requirement | Evidence/result |
|---|---|
| Waypoint staging | PASS, 17/17 |
| Abrupt 6→260 m/s transition and cell boundary | PASS |
| Direction changes, constant input, physical input travel | PASS |
| Fully loaded visible view and turn coverage | PASS |
| Complete coast visible coverage | PASS |
| Ground coverage and recovery | PASS, zero recovery or traversal blocks |
| Synchronous ground fallback | PASS, zero attempts |
| Full natural coast | PASS, 1151.778 m, settled after 9.183 s |
| Input frame time | PASS, maximum 45 ms; P50/P95 30/43 ms |
| Coast frame time | USER-ACCEPTED PASS, reported maximum 51 ms; P50/P95 29/39 ms; original 50 ms automatic gate FAIL |
| Focus/instrumentation/sample validity | PASS |

This is the first accepted complete focused result on this source. The earlier coast complete-cell failure did not recur in this witness. One run does not establish independent repeatability. The worst coast interval records a GC collection increment, but unpaired recorder values do not establish exact native ownership or justify adding inclusive durations.

## Remaining scope

An independent focused repeat and reconciliation of missing/affected full R2 rows remain unverified: cold/accepted startup coverage, sustained travel, bounded resources over the required windows, deliberate-stall recovery, unload/revisit and applicable full Goal-08 rows. Do not mark full R2 complete or claim R3 admission from this receipt alone.

The user also permits a partial handoff to prioritize terrain grounding, rivers, paths, settlement connections and mountain composition. This accepted focused result is the appropriate handoff boundary. Do not spend another repair cycle solely chasing the accepted 1 ms reported excess. Landscape work is tracked in `Docs/Terrain_Cohesion_Checkpoint_2026-10-02.md`.

Repair accounting: P25 remains the third applied hash-performance attempt after P11/P21. It did not satisfy the original automatic 50 ms coast criterion, but its current focused witness is accepted by explicit user decision. No fourth hash repair is authorized or needed to address this accepted excess.
