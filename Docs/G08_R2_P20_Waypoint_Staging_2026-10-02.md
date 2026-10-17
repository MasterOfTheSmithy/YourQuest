# G08-R2 P20 — authorized waypoint staging

## Evidence and decision

P19 run 16:52:25.8493985–16:52:33.5252207 UTC ended INVALID_ROUTE, with both measured phases explicitly NOT_RUN. The actual six timing profiles of the turn sequence found clear lanes at z offsets 66, 74, 82, 90 and 106. The previous constant-height straight staging/east corridor queries rejected them. Their combined blocker count does not separately establish which segment or terrain height caused every contact. Receipt: `Docs/G08_R2_Speed260_2026-10-02_165233_P19_Background_Diagnostic_Receipt.md`; all eight source/DLL hashes matched after returning to Edit (`outputs/G08_R2_P19_ReturnedAudit_20261002.json`). MVID f2b02717-6958-4fe1-b719-79c9e382019e.

P16/P18/P19 did not close the complete physical route. The three-attempt rule stopped that loop. The user explicitly selected **Use waypoint staging (recommended)** after being told that a collision-checked waypoint walk would retain 6→260 m/s, natural coast and all acceptance limits. This is authorization for the new staging approach, not a reset of earlier repair counts or permission to weaken a gate.

## Bounded repair and expected result

Only the existing verifier, `Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs`, changes. It selects among the same loaded, same-row, turn-clear lanes, checks the eastward approach along sampled terrain, and searches a local 4-meter grid for a capsule-clear walk. Maximum grid size is 2145 nodes within 128x256 meters around the starting player. Path length is limited to 135 meters. Terrain is sampled at no more than one-meter intervals; unsupported slopes, missing terrain, physical side obstacles, nonterrain overlaps and saturated overlap queries reject an edge. Waypoint walking uses the authoritative motor through ordinary keyboard input. A 0.5-meter clearance margin supports the 0.4-meter waypoint arrival tolerance. The existing 25-second staging deadline now also includes planning; no additional wait or streaming request is added.

The expected observable improvement is a completed physical approach to a clear turn lane instead of zero-frame staging rejection. Readiness/movement/timing gates remain required after staging. Route version 5 distinguishes this setup. No production motor, streamer, world, save, camera, collider, speed, input duration or frame-limit change. No teleport or repositioning.

Six new detached cases cover obstacle detours, each reconstructed edge, endpoint, path/node bounds, completely blocked paths, missing targets and an out-of-budget target. These establish planner logic only. Live staging and the actual input/coast gates remain to be measured.

## Remaining

Fresh compilation and detached regression, then one clean focused witness. Last useful earlier clean timing was P17 input 43 ms/coast 67 ms with incomplete direction displacement; it is not final-source certification. After repairs, obtain complete focused PASS and independent repeat, then reconcile missing/affected full R2 rows. R3 remains gated.

## P20 live result

Run 17:09:23.6743190–17:09:56.3423866 UTC: `Docs/G08_R2_Speed260_2026-10-02_170956_P20_Background_Diagnostic_Receipt.md`. All eight source/DLL hashes matched after Edit return; no compilation during Play (`outputs/G08_R2_P20_ReturnedAudit_20261002.json`). Unprofiled and zero unfocused measured frames.

Staging PASS: 653 nodes searched, 17/17 waypoints reached, lane z66, 12.738 seconds including planning, final target error 0.294 m. Ground-following east corridor and boundary approach PASS; no staging/boundary readiness intervention. First boost reached 260 m/s and crossed the seam. Thus the authorized waypoint approach closed the direct staging failure.

Overall INVALID_ROUTE/FAIL: turn frame 6327 integrated 89.079 ms (23.161 m requested), beyond the preflight's unchanged accepted 50 ms movement range. It struck `Origin_Vey_WitchHouse/CompiledSiteContent/CompiledCell__yq_cell_witch_house_p1_p1_p00/SM_EntranceAreas_Cube_091/BoxColliderObject`, completing 20.797 m, at (1.121,62.432,36.952). Direction displacement mask45/63, two input obstruction frames. Do not claim all accepted timing combinations are safe from six sampled preflight paths.

Input max74 ms, coast max84 ms (limit50); input P50/P95 35/51 ms, coast32/41 ms. Natural coast completed1147.6 m and settled without obstruction, fallback, traversal block or recovery. Input visible/turn readiness PASS. Coast visible readiness FAIL: first cell(13,5), appearance incomplete, with appearance lanes denied15 frames (~0.50–0.55 s); complete first-failure state retained in receipt. No same-frame native ownership claim is made from recorder publication order. Scheduler/reservation loop remains exhausted; this new receipt does not reset it.

The next diagnostic targets the early over-budget movement interval and coast maximum on the waypoint route. Opt-in trace initialization is moved before the existing low-speed boundary approach to avoid D42's demonstrated first-boost Time.deltaTime perturbation. No extra warmup frame or preparation is introduced. Trace-only timing remains diagnostic, not certification.
