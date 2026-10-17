# G08-R2 P16 — Valid physical stress and coast witness

## Confirmed verifier defect

The user reported that the test often runs into a mountain and explicitly requested a test repair. P15's matched capsule diagnostics confirm this: internal velocity started at260 m/s, but coast net travel was13.9 m, with3,809 side contacts against the accepted origin terrain at approximately(30,68,-3), normal(0.001,0.122,-0.992). Production terrain collision correctly prevented passage through that steep surface.

The verifier chose an eight-direction heading every frame using a modulo. Ending the2.45-second input window therefore selected a different coast direction at different frame counts. Its physical coast predicate required only one metre of displacement, allowing a mostly stationary coast to masquerade as a useful streaming window. The fixed12-second stop could also truncate a legitimate airborne coast: original acceleration28 and airControl0.55 brake at15.4 m/s², slower than grounded deceleration34.

## Repair — verifier only

Changed `Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs`:

- Keep the ordinary motor, capsule collision, initial6 m/s frame, abrupt260 m/s transition,2.45-second input window and all eight direction classes. Exercise the direction cycle once, then sustain the checked seam heading until the original input deadline. Release all input and allow normal braking. No teleport, collider removal, movement clamp, camera lock, speed change, terrain edit or extra streaming preparation.
- Require the existing capsule staging preflight to find a sampled corridor with zero side blockers. An absent/blocked candidate no longer silently becomes usable staging.
- Measure actual coast path, net displacement and forward progress against the motor's integrated planar displacement. Require at least one cell of net travel and agreement within accumulated capsule-skin tolerance. A contact that materially prevents progress records its first exact collider, frame, position and normal and marks the route invalid. A readiness rejection is not relabeled as a physical-route obstruction.
- Derive the coast deadline from the slower ordinary ground/air braking rate, preserving the previous12-second minimum plus enough time to settle. Invalid or non-braking configurations fail explicitly; malformed diagnostics remain bounded to60 seconds. This changes the verifier's timeout, never the motor's acceleration/deceleration/air control.
- Separate direction, coverage and timing results. Overall success still requires every original coverage, movement, fallback, recovery and50 ms gate. A failed turn buffer no longer falsely reports that all direction inputs failed; a slow frame no longer independently turns complete coast coverage into FAIL.
- Record focus validity and sample completeness. Unfocused or overflowed captures cannot certify success. Input/coast sample storage remains bounded at2048/4096 samples, with8192 frame records; these are observer capacities, not production streaming limits.

The route is version2. Earlier route-v1 results cannot be treated as matched performance comparisons. Fixed exit heading does not claim that every unseen future collider is clear: actual travel and contact gates establish that for each run, and an obstructed future run remains invalid rather than gaining a PASS.

## Verification

Background compilation succeeded, without Computer Use or focusing Unity. Detached regression: **537 checks PASS**, covering direction coverage and stable exit across10–512 input frames, natural ground/air coast integration at four frame rates, the historical13.9 m wall stall, circling without net progress, invalid numeric values and derived coast bounds. Receipt: `Logs/G08_R2_P16_FinalRoute_20261002.route.tsv`.

First route-v2 background diagnostic:15:30:58.9063272–15:31:19.5537992 UTC. Receipt: `Docs/G08_R2_Speed260_2026-10-02_153119_P16_Background_Diagnostic_Receipt.md`. MVID9cb11e45-8db6-476e-937c-e27584005b10, mainDLL961AD269411B85AE30D0AFDD1A7F92F8AC38B615ADF9A25FD09E1DEBD6BC5A8B. Correct existing profile/world/seed/accepted V2 retained.

Actual coast: **1182.094 m**, versus1182.489 m integrated request;9.459 seconds,284 frames, final speed0.6 m/s, **zero obstructed frames**, zero traversal blocks/fallbacks/recoveries. Coast ground and complete-visible coverage PASS. Bounded motion observer retained284 samples and dropped0. All direction masks63/63. A genuine input turn-buffer readiness failure occurred at(12,3); the old aggregate direction label was subsequently separated from that failure.

Input/coast maxima43/59 ms; all84/284 frames unfocused. **INVALID_FOCUS**, not clean certification or proof that the50 ms performance issue is fixed. The final timeout, separated verdicts and sample-completeness changes were compiled after this run; one final background witness is pending below. No production streaming/motor/terrain change is part of P16, and the exhausted scheduler/reservation repair count is unchanged.

## Remaining

The test-route defect is addressed. Full R2 remains incomplete: focused clean PASS and independent repeat are absent, and full R2 rows remain gated. Preserve the observed turn-buffer failure and50 ms miss for subsequent diagnosis; never turn them into a test PASS. R3 remains gated.

## Final-source witness

Final run15:40:12.4426772–15:40:33.0260284 UTC, immutable receipt `Docs/G08_R2_Speed260_2026-10-02_154033_P16_Final_Background_Diagnostic_Receipt.md`. Although launched entirely through background hooks, this run actually recorded **zero unfocused frames** in both measured phases; no tool focused Unity. Viewport616x264, profiler/binary/allocation-stack/deep tracing off, no optional coast-motion capture. The earlier filename's background label describes dispatch, not measured application focus.

MVID `e2091b88-9600-49d8-b1b7-59dc453b2d2b`; mainDLL `0C18794964AD588863A5846534AA96E3BF4DC9C1B0F2460C31112D40FF049BDF`. All eight armed source/DLL hashes matched after return to Edit; audit in `outputs/G08_R2_P16_Final_ReturnedAudit_20261002.json`. No compilation occurred during observed Play. Correct profile/world/seed/accepted V2 retained.

Coast **1177.629 m**, expected integrated1178.200 m,9.461 seconds,289 frames, settled at0.7 m/s, no obstructed frames. Derived timeout18.754 seconds was not reached. Complete frame capture375/375, input86/86 and coast289/289. Real/abrupt transition, boundary crossing, all direction classes, input visible/turn buffer, coast ground/visible coverage, zero fallback, zero traversal blocks and zero fall recovery all PASS in this run. The earlier background turn-buffer miss remains recorded; its absence here does not prove a separate repair or establish repeatability.

Input median/P95/max27/35/**42 ms**; coast31/42/**87 ms**. The final focused verdict remains **FAIL solely on the observed coast frame-time gate**. The route repair is runtime-verified, but full R2 is not. No matched P15→P16 performance inference: input route, viewport/focus conditions and source differ. This run is a usable current failing baseline, not a successful closure or independent PASS repeat.

Next bounded question (D40): identify the actual maximum and representative over-budget input/coast intervals on the final route/build, using explicit native frame boundaries. Distinguish project callback work, collection/JIT/editor work and native waits before any production patch. One original-window binary trace only; no additional preparation, cell history, stacks or GPU polling. Analyze that capture before considering another run. The existing exhausted scheduling/reservation loop stays closed.
