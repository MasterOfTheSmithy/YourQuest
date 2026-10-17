# G08-R2 P25R independent repeat — FAIL; partial handoff retained

## Identity and conditions

Run 20:43:33.5338226–20:44:06.4512561 UTC, 2026-10-02. Existing accepted profile through ordinary PlaySafe/title/Continue, unchanged route-v5 and final runtime MVID `b8423c09-43e7-42cf-b0d1-36a1336eef97`. The Editor was not restarted and caches were not cleared. All 13 source/build hashes match P25F and the post-run audit. No compilation in Play; returned Edit. Profiling, binary log, allocation stacks, deep profiling and trace request all false. Focus VALID with zero unfocused input/coast frames, all397sample records present.

Raw receipt: `Docs/G08_R2_Speed260_2026-10-02_204406_P25R_Background_Diagnostic_Receipt.md`. Audit: `outputs/G08_R2_P25R_ReturnedAudit_20261002.json`; identity: `outputs/G08_R2_P25R_Armed_20261002.json`.

## Gate progression

| Gate | Repeat result |
|---|---|
| Waypoint staging | PASS17/17 |
| Abrupt6→260m/s transition/boundary/directions/constant input | PASS |
| Input complete visibility and turn buffer | PASS |
| Ground safety, no synchronous fallback, no intervention | PASS |
| Full natural coast | PASS1150.279m in9.203s, settled, zero obstruction |
| Input frame maximum | FAIL62ms; P50/P9532/51ms |
| Coast frame maximum | FAIL52ms under original50ms contract; P50/P9528/34ms |
| Coast complete visibility | FAIL, first cell(13,6) |
| Focus and clean instrumentation | VALID |

P25F remains accepted under the user's explicit51ms decision. That decision does not waive an incomplete cell or the repeat's62ms input maximum. Thus independent repeatability is now **tested and failed**, rather than merely missing. No additional rerun is queued.

## First failure evidence and limits

Cell(13,6) was traversable with collision, required content, overlays and appearance ready, but required ecology was false. Owner epoch2/attempt1, no restart or preemption. Structure16steps/2.549ms accumulated; ecology3steps/0.036ms, one budget denial0.006s, provider substage waterProjection. Physical owners193/256 at the miss. Hard demand age2.115s, collision age1.411s, content start age0.176s. This is a snapshot of the observed incomplete stage, not complete queue-latency attribution or proof that the ecology provider itself is slow.

The clean receipt contains no same-frame native trace. Its recorder timings are not sufficient to assign the62ms/52ms maxima to an exact native owner. Do not add overlapping inclusive samples or claim a new bottleneck from a label.

## Decision and remaining work

The existing hash-performance line has three applied repairs (P11/P21/P25); P25 improved a detached benchmark and yielded one user-accepted focused witness, but did not establish repeatable full closure. Stop that repair loop. The exhausted scheduler/reservation loop remains stopped. This repeat is not a fourth repair attempt.

Retain the user's permitted partial handoff and prioritize the separately requested terrain-cohesion work. Full R2 remains incomplete: repeatability failed; cold startup, extended ordinary travel, full lifetime resource bounds, deliberate-stall recovery and unload/revisit remain unverified on final affected source. No R3 admission or complete goal claim. A future return to strict closure needs a bounded decision addressing the remaining ecology publication/deadline evidence and frame ownership; do not begin an unchanged lucky rerun or silent fourth repair.
