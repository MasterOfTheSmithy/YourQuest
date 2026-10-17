# G08-R2 D43 — waypoint-route frame attribution

Diagnostic only. R2 remains FAIL/incomplete; R3 gated.

## Run and identity

Ordinary PlaySafe/title/existing fixture Continue, 17:14:56.5934468–17:15:29.5006213 UTC. Receipt `Docs/G08_R2_Speed260_2026-10-02_171529_D43_Background_Diagnostic_Receipt.md`. Main assembly MVID `bd7f37aa-6dd1-49f1-b78d-e35ef0016556`. Eight source/DLL hashes matched on return to idle Edit; no compilation in Play (`outputs/G08_R2_D43_ReturnedAudit_20261002.json`). Production unchanged from P20; only opt-in trace initialization moved before the existing low-speed boundary approach. No added preparation frame. New compile/reload succeeded before Play.

Raw `Logs/G08_R2_FrameTrace_20261002_171516_905.raw`, 216900440 bytes, GUID `a3ce33c0-29be-447d-81f5-0b3b4e98e486`. All 307 input/coast intervals have preceding and closing metadata boundaries. Native owner exports cover 768:770, 987:989 and 824:826, three complete frames each. Raw aliases are hardlinks, not additional payload copies.

Staging again reached17/17 waypoints in12.742 s; boundary approach clean. Direction displacement, input physical travel and full coast physical travel PASS in this diagnostic. Input turn-buffer readiness and coast visible readiness FAIL. All52 input/255 coast frames unfocused: INVALID_FOCUS, with binary profiler overhead. Recorded input P50/P95/max42/67/115 ms; coast36/50/68 ms. These are neither clean timing nor proof that P20's collision has been eliminated under every timing sequence.

## Exact-interval ownership

`outputs/G08_R2_D43_FrameCost_20261002.json` pairs verifier boundaries; `outputs/G08_R2_D43_NativeOwners_20261002.json` clips native samples to those intervals. Inclusive parents/children and different threads overlap; do not add them.

- Input maximum Unity6263/native770, wall115.462 ms: interval includes prior native769 Editor hierarchy repaint with21.687 ms main-thread semaphore wait. Streamer Update10.853 ms. No GC. Console/status-bar text generation allocations also occur; this is not proof of a graphics-driver fault or an equivalent cost in P20's clean run.
- Coast maximum Unity6482/native989, wall68.266 ms: ecology coroutine35.619 ms inclusive contains20.392 ms GC. It owns371378 bytes/5862 direct allocation events in the interval (native scope, not exact source callsites). Whole interval retained allocations568135 bytes/7532 events. Render-thread wait for main-thread commands overlaps this CPU work and is not additional CPU time.
- Representative coast Unity6319/native826, wall55.772 ms: motor8.115 ms, streamerUpdate7.828 ms, streamerLate3.831 ms, verifierLate4.565 ms clipped from previous native frame. No GC in this interval; retained allocation1258465 bytes/6909 events.

The allocation source within the ecology callback remains unproved by binary scopes. Existing bounded allocation stacks (up to24 intervals/one second) are the next diagnostic; no scheduler/worker/reservation repair follows from this receipt. Exhausted scheduler counts and other prior attempts remain carried.

## Remaining

Identify allocating source lines before a production allocation repair; then focused regression and clean run. Still required: all movement/readiness gates within50 ms, independent clean repeat on final source, and missing/affected full R2 matrix rows. P20 waypoint staging is verified; whole-goal completion is not.
