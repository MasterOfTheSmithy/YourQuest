# G08-R2 D47 — handoff confirmation and current motor allocations

Status: **diagnostic only / INVALID_ROUTE for certification**. No new production repair in D47. Full R2 remains FAIL; R3 remains gated.

## Identity and conditions

Run 2026-10-02T19:20:16.6421481Z–19:20:51.8911844Z. Receipt: `Docs/G08_R2_Speed260_2026-10-02_192051_D47_Background_Diagnostic_Receipt.md`.

- Unity6000.3.2f1, DX12, Ryzen5800X, RTX5060, 616x264 Game viewport, Project dock, Editor PID48356.
- Main MVID `ff45d20d-4d64-4ab9-b9d2-5e6b3d10982a`, unchanged P24. All nine armed/returned source/build hashes match; no observed compilation in Play; idle Edit at return. `outputs/G08_R2_D47_Armed_20261002.json` and `outputs/G08_R2_D47_ReturnedAudit_20261002.json`.
- Same disposable fixture/profile `3ccfff4df9a1479884061e51940e4457`, world `yq-world-0284d4409a720743c13eb56e`, seed `4bb221dc`, accepted V2 hash `c1d6ce9d3e06f760`.
- Binary trace and bounded cell history enabled. Existing allocation-stack diagnostic enabled at coast boundary6298 and stopped6299: one recorded interval of the maximum24interval/1s option. Enabling stack capture itself perturbs the diagnostic; wall/native/phase-time measurements are not interchangeable.
- Raw `Logs/G08_R2_FrameTrace_20261002_192037_520.raw`, capture GUID `cab183b1-0a89-497f-98b0-cb73964287e4`. Exact metadata `Logs/G08_R2_FrameTrace_20261002_192037_520.tsv`.
- All304 input/coast intervals paired; zero unfocused frames. Direction displacement mask62/63,16 obstructed input frames, two obstructed coast frames, exit heading(0.979,0,-0.206): INVALID_ROUTE. No clean timing or regression claim from diagnostic maxima71/112ms. Ground, input visibility, turn buffer and zero fallback/recovery retained; coast visibility failed. Latest clean P24 maxima remain57/54ms against50ms.

## P24 local handoff result

Analysis `outputs/G08_R2_D47_AfterHandoff_20261002.json` uses the same same-cell/configuration/content-epoch pairing as D46 before the patch.

| Prepared-result handoff observations | D46 before | D47 after P24 |
|---|---:|---:|
| Coast paired observations |24|29|
| Visible at initial observation |24|23|
| Delayed into the next frame |24|0|
| Coast median observed handoff interval |49.231ms|1.042ms|
| Coast range |38.906–59.384ms|0.429–7.470ms|

All60 after-patch pairs (29approach,2input,29coast) complete in the same Unity frame; none unmatched. These sub-frame observation intervals include instrumentation and other intervening work, not isolated handoff CPU duration. Different trajectories forbid a paired whole-run speedup claim. The specific extra-frame publication defect is removed in the captured current runtime. Earlier structural/materialization deadlines remain independently unmet.

## Exact native frame ownership

Samples are clipped to explicit preceding/closing verifier boundaries. Inclusive motor/camera, Update/LateUpdate, GC and native waits overlap; never sum parent and child rows. Render waits do not prove a GPU or driver fault.

| Verifier frame | Native frame | Diagnostic wall | Main project work within interval |
|---|---:|---:|---|
|6281 input maximum|42|70.828ms|Motor31.155ms including camera-ground28.895ms; streamerUpdate7.283ms, LateUpdate8.861ms; no GC|
|6251 input representative|12|55.396ms|LateUpdate14.978ms, Update10.752ms, motor1.471ms; no GC|
|6299 coast maximum, stacks enabled|60|112.035ms|Motor36.467ms including camera-ground34.422ms; LateUpdate11.275ms, Update6.405ms, verifierUpdate7.434ms; no GC|
|6301 coast representative, stacks stopped|62|59.006ms|Motor13.527ms, LateUpdate6.587ms, Update5.526ms; no GC|
|6356 coast collection interval|117|75.743ms|Motor42.129ms **including GC24.403ms**; Update6.896ms, LateUpdate5.250ms|

Native exports retained in outputs: `G08_R2_D47_Input.samplethreads.tsv` (40:42), `G08_R2_D47_Coast.samplethreads.tsv` (58:62), `G08_R2_D47_Representative.samplethreads.tsv` (10:12), `G08_R2_D47_Gc.samplethreads.tsv` (115:117). All export completion footers matched requested counts. Derived exact intervals: `outputs/G08_R2_D47_FrameCost_20261002.json`, `outputs/G08_R2_D47_NativeOwners_20261002.json`.

## Current allocation ownership and source boundary

`outputs/G08_R2_D47_MotorNative_20261002.json` counts retained GC.Alloc events within the exact motor callback and matching verifier interval:

| Frame | Motor total | Direct camera-ground scope |
|---|---:|---:|
|6281|601674B /6949events|565982B /6402events|
|6299|465898B /5636events|410358B /4725events|
|6301|258724B /3067events|camera-ground itself is1.025ms;243486B are under the broader motor scope|

Counts include all captured event rows under the indicated scope, including children; categories must not be summed as independent work. Editor material validation contributes13900B/326events under motor in6281 and27800B/652events in6299. It is a separate contributor, not automatically the primary cause.

Saved stacks `Logs/G08_R2_FrameTrace_20261002_192037_520.samplecallstacks.tsv` cover native59:60. In frame60 the existing exporter selects64 evenly spaced allocations from11177 main-thread events;57 selections have resolved stacks.29 selected stacks include the production motor. This is a **sample of callsites**, not a per-function byte census or CPU ranking.

`outputs/G08_R2_D47_SelectedMotorAllocations_20261002.json` retains those29 selected motor stacks. They identify:

- `TryEnsureCurrentCameraGround → RefreshHardViewDemand → AdmitHardViewCoordinate → GetOrCreateChunk → BuildSemanticRecord → PopulateCellRecord`: semantic cell/edge construction, biome samples, fingerprints, source-fingerprint construction, site normalization and cell/edge hashes.
- `CanonicalizeAcceptedProjectionFeatureMetadata → YQSpatialMaterializationResolverV2.TryGetPrepared → TryComputeCurrentHash → YQSpatialBlueprintHasherV2.ComputeContentHashReadOnly`: five sampled sites in route/site/string hashing. This is **current post-P21** evidence; it does not reuse the older D44 stacks.
- `PhysicalOwnerCapacity` uses the three-argument params `Mathf.Max`; two sampled sites. Failure-detail formatting also appears. Their byte/time shares are not yet isolated.

Source review confirms `GetOrCreateChunk` reuses existing and persisted records; only missing records call `BuildSemanticRecord`. Do not claim it rebuilds every cell each frame. `YQSemanticWorldAuthority` already has a512-core cache and clones query results. Its `QueryCell` and `GetSitesForCell` each call `Ensure`, which recomputes source fingerprints and normalizes accepted site ownership; `PopulateCellRecord` calls both. This establishes a potentially redundant same-call query boundary, but **its isolated cost has not been measured**. No query/cache/hash validation was skipped or modified. Frozen architecture selection is already reused during gameplay. Any next optimization must retain accepted hashes, mutation rejection, output/order parity, cache bounds and independent query results.

## First failed cell and useful throughput

History `Logs/G08_R2_CompleteCells_20261002_192051_827.tsv`:57805records,0dropped, first actual failure **coast(12,5)**, frozenPrefix26973. It is a different failure from clean P24(13,7); profiling changed the route and includes a long stack-enable interruption. Do not treat its absolute deadline latency as clean performance.

- Admitted frame6263 t136.427734.
- Terrain dispatch6280 t137.241657:0.813923s after admission; collision ownership6283 t137.399806.
- First content attempt6286 t137.552339, epoch2. One preemption; replacement visible by6290 t137.723300, epoch4.
- First failure6299 t139.492002: structures10steps/0.542ms with5 budget-denied frames; ecology5steps/2.609ms with3 denials; appearance also incomplete. Owner count206, demand227, painters3, colliders0; no capacity-starvation conclusion.
- Complete6303 t139.695921:203.919ms after observed failure; structures16steps/1.551ms with7 denials, ecology7steps with5 denials. Existing scheduling/reservation repair loop remains exhausted and unopened.

`outputs/G08_R2_D47_CompleteCells_20261002.json` reports strict actual published completions, not hidden prepared content or intrinsic sustainable capacity. Classes overlap.

| Phase/class | New unprepared demand | Useful unique completions | Pending start→end | Required /achieved per second |
|---|---:|---:|---|---|
|Input visible,2.495610s|4|4|0→0|1.603 /1.603|
|Input turn|2|0|0→2|0.801 /0|
|Input swept|0|0|0→0|0 /0|
|Input predictive|150|5|0→52|60.106 /2.004|
|Coast visible,10.334798s|45|45|0→0|4.354 /4.354|
|Coast turn|28|2|2→8|2.709 /0.194|
|Coast swept|3|2|0→0|0.290 /0.194|
|Coast predictive|64|43|52→52|6.193 /4.161|

Equal aggregate visible demand/completion rates do not mean deadlines passed:42 coast cells were incomplete at some observed exposure. Demand membership churn/discards also prevent interpreting simple backlog arithmetic as a closed queue. Observed owner/demand maxima253/256, painters3, colliders2 do not replace the remaining full lifetime resource gate.

## Remaining and next bounded action

P24's local handoff is runtime-confirmed; complete focused closure is still unsuccessful. Keep P24 at one unsuccessful complete-closure attempt and all prior cause counts unchanged. The clean input/coast50ms maxima and complete coast visibility remain failed; focused PASS, independent repeat and affected full R2 rows are still missing. No R1 recertification or R3 work.

Before another repair, quantify the redundant semantic query/formatting allocation work inside the now-identified camera-admission boundary, using the accepted detached fixture and/or saved capture rather than another broad runtime trace. Choose a coherent reduction only if the measured cost warrants it; preserve current validation and deterministic output. Existing Blueprint-hash attempts remain two, and the terrain scheduling/reservation loop remains stopped. No further runtime patch, test marker or live session is pending at this handoff.
