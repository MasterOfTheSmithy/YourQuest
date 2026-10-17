# G08-R1 blueprint integrity and copy removal

UTC: 2026-09-25T19:14:36.2969091Z
Evidence: Edit Mode contracts and microbenchmark; not a runtime frame-rate result.
PASS: Nested absent collections retain the canonical hash without source mutation.
PASS: Absent top-level collections retain the canonical hash without source mutation.
PASS: Changed accepted bytes reject cached preparation; restoring bytes recovers the original projection.
PASS: Selected saved blueprint retain the canonical hash without source mutation.
PASS: Persisted content hash 68ea565845b8a662; seed 166257f2.
PASS: 28 settlement/camp clearance comparisons match exactly; mean existing 6.568 ms, prepared 0.011 ms (reflection included; not FPS).

16 iterations per method, same artifact:

| Method | Mean ms/call | Allocated bytes/call |
| --- | ---: | ---: |
| JSON copy + canonical hash | 2.564 | unavailable |
| Readonly canonical hash | 1.196 | unavailable |
Allocation counter returned zero in this Unity runtime; zero is not evidence of allocation-free hashing.

Result: PASS
