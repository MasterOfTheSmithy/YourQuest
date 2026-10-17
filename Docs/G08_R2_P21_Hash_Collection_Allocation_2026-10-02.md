# G08-R2 P21 — hash collection scratch storage

## Cause and scope

D44 recorded actual allocation callsites in `YQSpatialBlueprintHasherV2.AppendStrings`, `AppendSites` and `AppendRoutes` beneath repeated accepted-world integrity checks during motor/camera admission. This does not identify D43's separate ecology allocation burst. See `Docs/G08_R2_D44_Allocation_Stacks_2026-10-02.md`.

Production change only in `Assets/Assets/Scripts/Generated/YQSpatialBlueprintV2Validation.cs`: tag strings and site functions use empty/singleton paths and rented scratch arrays instead of a fresh List/backing array on every check. Multi-item sequences use the same ordinal-string or signed-int ordering, preserve duplicate/null semantics, and return buffers in finally; string references are cleared. The existing record-ID sorting and hash writer are unchanged. No cached hashes, reduced validation frequency, accepted-record mutation, schema/content/routing change, scheduler adjustment or worker/limit change.

Verifier adds76 detached collection parity cases to the existing frozen-reference regression. Senior review boundary: hash output and mutation detection are authoritative; temporary storage is local to each call and cannot alias accepted collections. P11's one unsuccessful focused closure remains counted; this is the next repair within the hash allocation line, not a reset.

## Compilation and isolated verification

Both assemblies compiled outside Play at17:30:26–27 UTC. Executing MVID `33a1f341-e48d-4378-8b62-443a70839c94`. Fresh native Editor showed zero errors and one existing CS0162 warning in unrelated contract-test code. Scoped diff check passed.

Before:8288 cases PASS (`Logs/G08_R2_P21_Before_20261002.blueprinthash.tsv`). After:8364 cases PASS (`Logs/G08_R2_P21_After_20261002.blueprinthash.tsv`). Checks include four cultures, float/int edge cases, random scalar cases, unique-ID reordering, null collections, mutation detection, accepted hash `c1d6ce9d3e06f760`, source and fixture nonmutation, unchanged singleton owners, duplicate/null/Unicode tags, signed function values and alternating scratch sizes.

Native allocations under the same named eight-call Current hash scope:

| Measure | Before | After |
|---|---:|---:|
| Bytes |204016|109936|
| Allocation events |2960|1128|
| Inclusive named scope ms |6.362|5.101|

Bytes fell46.1%, events61.9%. Frozen Reference control remained23904 events/777344 bytes in both. Isolated timing is profiled Edit evidence only, not gameplay performance. Native exports each completed one frame: before1363 GUID57d7ee5c-2725-4d07-b518-3101f734f4cd raw172851_544; after1406 GUID14e190be-7ced-49fe-b9d6-ab10f31c35b0 raw173229_033. Streaming extraction summaries are `outputs/G08_R2_P21_BeforeAllocations_20261002.json` and `outputs/G08_R2_P21_AfterAllocations_20261002.json`; managed allocation readings are not used.

## Live gate

One unprofiled route-v5 focused witness is armed after native Game-tab focus. Nine source/assembly hashes, including the changed hasher, are in `outputs/G08_R2_P21_Armed_20261002.json`. Runtime result pending. P20 staging is verified; full goal still requires all50 ms/readiness/motion gates, independent clean repeat and affected/missing full R2 rows. R3 remains gated.

## Clean runtime result — FAIL / INVALID_ROUTE

Run17:37:16.4104096–17:37:49.2813398 UTC, `Docs/G08_R2_Speed260_2026-10-02_173749_P21_Background_Diagnostic_Receipt.md`. No added binary, allocation-stack, GPU or history instrumentation; all measured frames focused. All nine armed hashes matched after return to idle Edit, no observed compilation in Play (`outputs/G08_R2_P21_ReturnedAudit_20261002.json`).

| Gate | Result |
|---|---|
| Waypoint staging |PASS17/17,12.837 s,0.221 m target error|
| Boundary approach / abrupt6→260 / seam crossing |PASS|
| Input visibility / turn buffer |PASS|
| Input physical travel / direction displacement |INVALID_ROUTE / FAIL61/63|
| Input frame maximum |FAIL89 ms, P50/P95 38/51 ms|
| Full natural coast travel |PASS1150.7 m,9.419 s, settled within existing threshold|
| Coast visibility |FAIL cell(13,6), required ecology incomplete|
| Coast frame maximum |FAIL60 ms, P50/P95 32/45 ms|
| Focus / zero fallback / zero traversal block / no recovery |PASS|
| Focused complete PASS / independent repeat |NOT OBTAINED / NOT RUN|
| Full affected R2 matrix / R3 |INCOMPLETE / GATED|

Turn frame9388 integrated83.406 ms,21.686 m requested,21.213 m forward before contacting the same witch-house box at(3.590,62.432,36.952). The following southeast turn lost direction fidelity. The 50 ms timing condition remains unmet; six sampled turn paths do not authorize accepting this collision.

First coast visibility miss(13,6): hard demand age2.107 s, collision age1.343 s, content/ecology start age0.527 s, required content just published, ecology incomplete. Required ecology iterator7 steps/4.725 ms accumulated, max2.173 ms, last step9453, no budget denial in that recorded iterator. This snapshot alone does not prove the full delay cause. Do not restart the exhausted scheduler/reservation loop from this data.

P20→P21 clean maxima74/84→89/60 ms are observations from different live demand histories and turn trajectories; they are not an isolated overall speedup estimate. The native same-fixture allocation reduction is the supported local result. P21 adds a second unsuccessful complete focused closure to the hash allocation line (P11 first); no lucky repeat is warranted.

The next unresolved performance question is the early second-turn interval and non-GC coast maximum on final P21 source. D43's prior second-turn interval contained a34.817 ms native PostLateUpdate CoroutinesDelayedCalls scope with only2.271 ms identified child Destroy calls, plus an overlapping Editor hierarchy21.687 ms wait; the remaining parent cost has not been assigned to an actual owner. Do not attribute it all to destruction, the streamer, or the driver. D44 did not capture the requested ecology allocator. A future targeted capture must answer an exact remaining owner question before another production patch. First clean full focused PASS, independent repeat and missing/affected R2 rows remain outstanding.
