# Beta progress — repair batch 02

## Status and baseline

This batch continues the chat repairs on `repair/chat-audit-2026-10-17`, based on the published commit `33cb3654e237f2dd43e6e8992b792f8a077c2dbf`. The baseline branch head was checked again before work. Two bounded scheduling and resource-lifetime repairs are implemented and pass the focused managed regressions. Native Unity acceptance remains outstanding.

The [initial report](Repair_Report.md) and [handoff](README.md) retain their original evidence. This report records the subsequent work; it does not replace historical measurements or advance a roadmap goal to PASS.

## Changed and why

### Canonical terrain makes progress during continuous provisional preparation

The previous repair moved provisional ground synchronization into the early streaming update. That exposed a scheduling regression: continual provisional work could consume the shared terrain sync in every frame before the canonical publication coroutine resumed. The canonical collider stayed disabled while its coroutine waited for a free sync opportunity.

The existing helper now selects the oldest eligible canonical ticket from the existing two-ticket capacity. It enables that ticket's exact owned collider before the shared native call and records a successful synchronization frame on that ticket. The canonical coroutine consumes this receipt and still waits for a subsequent frame before the existing collision-readiness and publication checks.

Eligibility observes the existing ordinary allowance, urgent terrain reserve, and aggregate ceiling. An older nonurgent ticket cannot conceal a newer urgent ticket when only the urgent allowance remains. The selector reads allowance without consuming a reserve; admission and work-cost accounting remain in the existing budget gate. Prior-frame spending is reset before candidate selection, including calls that occur before `Update`.

The shared canonical/provisional terrain lane still permits at most one native sync per frame. Ground dirtied after that call remains pending. A waiting unrelated canonical ticket does not withhold movement on already synchronized ground. Failed native synchronization or incorrect collider data grants no receipt and hands rejection back to the existing publication wrapper, cleanup, and retry owner. The separate existing physics-admission box sync path is outside this change.

### Cold unrelated assets can retire during another preload

The runtime registry previously refused all cold-cache retirement while any preload was active. Consequently, even an unrelated dormant prefab and its animation dependency remained strongly referenced while another selection loaded.

Each preload iterator now snapshots and normalizes its selected paths once, preserves first-seen order, and holds the existing residency lease for that complete selection before yielding. The lease protects both pending resources and already completed resources while later selected assets load. Completion or cancellation releases the implicit lease with the existing epoch checks.

Removing the registry-wide preload veto lets unrelated resources pass the existing per-resource pin, pending-request, age, and memory-policy checks. Explicit population/ecology leases still protect their assets beyond preload completion through preparation and activation. Canceled native requests retain their existing completion handling. The performance director, collection cooldown, and native asset-unload policy are unchanged.

This makes unrelated cache references reclaimable; it does not establish a measured native memory reduction.

## Before/after evidence

Both regressions were replayed against the previous production source and the repaired source. The exact source hashes, complete final assertion results, and terrain frame witnesses are in [Beta_Progress_02_Verification.json](Beta_Progress_02_Verification.json).

| Controlled scenario | Previous source | Repaired source |
| --- | --- | --- |
| Twelve successive frames with a provisional sync opportunity before canonical coroutine resumption | Twelve native terrain sync calls; canonical collider disabled and unpublished throughout | The same twelve calls; ticket registered at fixture frame 15, synchronized at 16, and published at 17 |
| Completed prefab A, pending prefab B, and unrelated cold prefab C with a distinct dependency | Retirement of C fails while B is pending | C's prefab cache and dependency retire; A and B's selected resources remain protected |

The previous-source movement replay reports 18 failed assertions out of 62. Some observe fields added by this repair, so they are not 18 independent defects. The principal behavioral reproduction is the canonical collider remaining disabled and unpublished throughout the twelve-frame workload. The previous registry compiles successfully, then fails the new unrelated-cache retirement assertion. All final current-source checks pass.

## Verified

| Final integrated check | Result |
| --- | --- |
| Complete changed project C# syntax, player and Editor configurations | 2 files; 0 syntax errors |
| Paired publication regressions | 37 passed; 0 failed |
| Population regressions | 28 passed; 0 failed |
| Movement and scheduling regressions | 62 passed; 0 failed |
| Runtime registry and approval contracts | 8 groups passed; 0 failed; all 8,174 manifest paths checked |
| Captured source identity before and after the final runs | Unchanged |
| Patch whitespace and affected references | Passed |

Movement verification compiles the actual registration, arbitration, synchronization, publication iterator, wrapper, and current-ticket methods. New cases cover both pending tickets with reversed coroutine resume order, ordinary/urgent eligibility, exhausted allowance, both frame boundaries, stale prior-frame spending, cancellation before and after synchronization, owner replacement, mismatched collider data, native failure handoff, and explicit collision verification holds.

Registry verification compiles the complete production registry against controlled Resources APIs. Cases cover normal warm-window retention, one-time input enumeration, implicit and external lease overlap, cancellation, delayed request completion, coalescing, and session resets. An independent agent reviewed both production changes; root reviewed the final diff and reran the integrated checks after source freeze.

The portable SDK emitted the already documented `CS1701` reference-unification warning and registry `CS0649` warnings for fields populated by JSON deserialization. The focused managed fixtures compiled without errors. These results are not a Unity compilation, native physics or memory measurement, or beta/travel acceptance.

Reproduce the current checks from a checkout containing this commit:

```sh
python Tools/ChatAuditVerification/run.py --sdk-root /path/to/dotnet --baseline 33cb3654e237f2dd43e6e8992b792f8a077c2dbf --output /path/to/verification-output
python Tools/ChatAuditVerification/run_assets.py --dotnet-root /path/to/dotnet
```

The chat checkout used a local bookkeeping commit with the identical baseline tree because its Git transport could not fetch the API-published commit object. The receipt records both identities. The next GitHub commit is based on the actual published baseline; the bookkeeping commit is not part of the remote change.

## Preserved quality and remaining acceptance

This batch changes two production C# files and the related verification/documentation. Terrain resolution, content density, authored assets, NPC population, materials, visual exposure rules, and readiness requirements remain intact. There are no schema, seed, accepted-content identity, V1/V2 routing, scene, prefab, GUID, project setting, or ownership changes. State schema remains 8 and profile manifest schema remains 2.

The complete local Unity checkout is still required for the next acceptance step. The initial batch's **Build Prefab Dependency Preloads** and **Validate Prefab Dependency Preloads** menu operations remain prerequisites where generated dependency assets are absent or stale. Then compile, run the existing movement admission regression, and exercise the production `YourQuest_PlaySafe` scene with cold and warm activation, continued travel, cancellation, profile switching, and revisits after retirement.

Measure canonical publication progress while provisional terrain continues to prepare. Measure actual memory before and after unrelated-cache retirement while another selection is pending. Retain real physics, visual coverage, and readiness holds during the sustained 200 m/s travel check and approximately 90 ms stall target. An indivisible Unity operation can still exceed its admitted scheduler slice; this batch does not certify those native targets.

A separate useful source follow-up is exact revision-aware save-directory lookup: callers that already know the revision can avoid repeated history scans while retaining all checksum proofs. This batch intentionally leaves save-store behavior unchanged so that future optimization can receive its own corruption/recovery regression.
