# G08-R2 D44 — bounded allocation callsites

Diagnostic only; full R2 remains FAIL/incomplete. Same production source as D43 and route-v5 verifier. Fresh native access selected the Game tab before Play. Run 17:24:39.1525283–17:25:13.3664834 UTC, ordinary existing-fixture Continue. Receipt `Docs/G08_R2_Speed260_2026-10-02_172513_D44_Background_Diagnostic_Receipt.md`. All eight source/DLL hashes match, no observed compile in Play, returned Edit (`outputs/G08_R2_D44_ReturnedAudit_20261002.json`).

Staging17/17 in12.963 s. All input/coast frames focused. Input motion collided after an over-budget integrated step; direction mask45/63. Input/coast visibility failed. Full natural coast1117.8 m completed and settled without obstruction, fallback or traversal block. Recorded max85/104 ms includes profiler overhead; no certification.

Raw `Logs/G08_R2_FrameTrace_20261002_172459_697.raw` (212611833 bytes), GUID `3ba9da49-6df7-41a5-b74b-56171ee3c346`. Allocation stacks enabled at Unity9168 and restored at9169 after one interval, reaching the one-second diagnostic wall limit. Saved native1133:1134 callstack export matched two frames. Frame1134 main thread recorded11512 allocation events/930348 bytes, selected64 evenly spaced samples,60 with stacks,57 unique selected stacks. This is a sample, not a source-byte census.

## Findings

The desired D43 ecology allocation source was **not captured** in the bounded window; do not assign the previous371378 bytes to a guessed callsite.

The captured movement/camera admission path does prove repeated read-only accepted-blueprint collection allocations: `ValidateAndRememberCameraPose → RequestCameraViewAdmission → RefreshHardViewDemand → AdmitHardViewCoordinate → GetOrCreateChunk → BuildSemanticRecord → CanonicalizeAcceptedProjectionFeatureMetadata → TryGetPrepared → TryComputeCurrentHash → ComputeContentHashReadOnly`. Selected stacks include11 `AppendStrings`,5 `AppendRoutes`,1 `AppendSites` allocations. Current implementation creates fresh temporary tag lists, site-function lists and sorting buffers each full integrity check. Other captured sources include semantic-cell construction, palette normalization and two physical-capacity params arrays; no claim that hashing owns all930348 bytes.

## P21 bounded hypothesis

Preserve every hash/validation call, record sorting, canonical token, null/duplicate handling, accepted content and mutation detection. Remove temporary tag/function List allocations using empty/singleton paths and rented scratch arrays, returning/clearing references in finally. No cached hash, content mutation, scheduler/worker change or new authority. The prior P11 scalar-hash repair already has one incomplete focused closure; carry that count. P21 is a further bounded hash-allocation repair, not a restarted loop.

Before fixture:8288 parity cases PASS, accepted hash c1d6ce9d3e06f760, read-only and mutation detection PASS (`Logs/G08_R2_P21_Before_20261002.blueprinthash.tsv`). Native baseline raw172851_544, GUID57d7ee5c-2725-4d07-b518-3101f734f4cd. Compare its eight named Current hash calls against the same fixture after repair; managed allocation counter is known unreliable here. New focused parity cases cover null/empty/singleton, duplicates, signed functions, Unicode, alternating scratch sizes and source nonmutation. Compilation and after-regression must pass before another clean runtime.
