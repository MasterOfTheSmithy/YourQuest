# G08-R2 P23 — share the native retirement allowance

Status: applied, clean focused verification pending. Full R2 is FAIL/incomplete and R3 gated.

## Causal finding and scope

Saved D45 native1455 contains319 direct `Destroy` calls totaling28.740 ms; D43 native769 contains322 totaling34.706 ms. Both occur in the second-turn measurement interval. D45 history proves64 streamed owners remove186 tracked terrain/provisional/content roots in6299 after76 cells leave demand. Source `ApplyLifecycle` limits deactivation but destroys every already-inactive unloaded owner in the same pass. `Object.Destroy` defers expensive native work until after LateUpdate, so elapsed C# lifecycle time does not bound this cost.

This is the first applied repair of the demonstrated burst of deferred native retirement. It is distinct from the exhausted terrain/content scheduling and reservation-priority loop: priority, admission demand, worker counts, content stages and owner capacity are unchanged. No prior counter is reset. Hash allocation closures remain two, redundant visual-write closures two, candidate scan one, request polling one.

## Patch and expected result

`Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs` shares the existing `SemanticEvictionsPerFrame=1` allowance across ordinary lifecycle unload, capacity-driven retirement and history pruning. Unfinished retirement stays `Unloading`, keeps its existing objects/physical ownership, and resumes later. Returning demand still takes the original active/retained branch. Queued native destruction is counted against the same256-owner reservation cap until the next frame. No new physical queue, increased limit, content reduction, save/schema/artifact mutation or second owner is introduced. Mandatory failure cleanup and profile/streamer teardown retain their immediate invalidation path.

Expected measurable change: the64-cell native destruction burst becomes at most one ordinary retirement per frame, lowering that specific29–35 ms interval cost. A single hierarchy remains indivisible; the allowance is not proof that every future frame fits50 ms. The clean witness must also retain full readiness, movement and resource safety. Slower cleanup cannot be accepted as a trade for missed exposure.

`YQSemanticChunkRuntimeVerification` adds an Edit-only regression through the existing analysis marker (`teardown-only`), preserving the gameplay verifier. Its first dispatch was rejected by the analysis whitelist before execution; the mode was then added to that whitelist. No gameplay run or test PASS resulted from the rejected dispatch.

## Verification

Patch inspection and `git diff --check` passed. Fresh Unity compilation completed outside Play, with the existing unrelated CS0162 warning in `YQWorldGenerationV2ContractTests`; no compiler error in the successful reload. Narrow regression and clean focused outcome are recorded below when completed.

P22 first-failure retention and saved-data export fixes remain in the same final build. Raw D43/D45 evidence and new complete child exports are preserved; see `Docs/G08_R2_D45_Frame_And_First_Cell_2026-10-02.md`.

## First P23 runtime — INVALID_FOCUS / diagnostic only

Run18:13:14.0807060–18:13:46.2757311 UTC, receipt Docs/G08_R2_Speed260_2026-10-02_181346_P23_Background_Diagnostic_Receipt.md. All added profiling/history modes disabled, but every measured input/coast frame was unfocused(36/228), so245/75ms maxima cannot establish the repair's clean timing or count as a performance certification failure. Direction mask61/63 and two obstacle contacts make the physical route invalid too. Staging17/17 in12.320s and full coast1122.7m/9.230s passed diagnostically; zero fallback/traversal block/recovery. Input visible(11,6), turn(11,3) and coast visible(11,5) were incomplete. All nine armed hashes matched, no observed compile in Play, returned Edit; outputs/G08_R2_P23_ReturnedAudit_20261002.json.

Narrow retirement regression18:09:43.5539567UTC PASS771 cases, MVIDf99d8b98-6f7a-480f-bfad-fcfe52f4d404. Terminal capacity census explicitly includes255 reservation owners plus1 pending native retirement=256; this snapshot is not lifetime certification.

Focus repair before the next run: add applicationFocused and focusedEditorWindow to the existing read-only Editor status, confirm actual focus at title before Continue, and arm the unchanged verifier before the ordinary loading handoff. Windows reported intervening user input on one activation attempt; refreshed native state and activation succeeded. No automatic repeated focus stealing or timing acceptance from an occlusion-safe screenshot. P23 clean closure remains pending; previous clean P21 baseline remains89/60ms FAIL.

## P23F focused clean result — FAIL

Run18:18:45.9110229–18:19:18.7699545UTC, Docs/G08_R2_Speed260_2026-10-02_181918_P23F_Background_Diagnostic_Receipt.md. Same runtime MVIDf99d8b98-6f7a-480f-bfad-fcfe52f4d404; Editor status addition compiled separately. No profiler/binary/allocation-stack/history instrumentation. All331 input/coast frame records retained, zero unfocused frames, validity VALID. Nine hashes match, no observed compile in Play, returned Edit (outputs/G08_R2_P23F_ReturnedAudit_20261002.json).

| Gate | Current result |
|---|---|
| Waypoint staging |PASS17/17,12.986s,0.270m target error|
| Ordinary controls / abrupt6-to-260 / seam crossing |PASS|
| Input motion / all directions / constant segments |PASS, zero obstruction|
| Input full visibility |FAIL first(11,6), structures/ecology incomplete|
| Turn buffer / ground safety |PASS|
| Input frame maximum |FAIL63ms; P50/P9537/53ms|
| Full natural coast |PASS1146.637m,9.249s,settled0.3m/s|
| Coast full visibility |FAIL first(12,6), complete content/ecology incomplete|
| Coast frame maximum |FAIL84ms; P50/P9533/45ms|
| Focus / fallback / traversal blocks / recovery |PASS zero failures/interventions|
| Terminal resource census |255 reservation owners including0 pending native, cap256; lifetime NOT VERIFIED|
| Complete focused PASS / independent repeat |NOT OBTAINED / NOT RUN|
| Affected full R2 rows / R3 |INCOMPLETE / GATED|

This is P23's first unsuccessful complete focused closure. P23's earlier all-unfocused run is invalid certification and is not an extra performance attempt. Clean P21-to-P23F maxima89/60→63/84ms and route INVALID_ROUTE→VALID show observed movement improvement but are not an isolated total-frame speedup: coast trajectories/demand histories differ. Native cleanup cost after the repair is not yet directly measured.

First input cell(11,6) snapshot: hard demand age1.930s, collision age0.532s, content/ecology start age0.457s, appearance ready but content/ecology/overlays/activation incomplete. First coast cell(12,6): hard age1.653s, collision0.693s, content/ecology0.456s. These snapshots do not reopen the exhausted scheduling/reservation repair loop.

Next bounded diagnostic D46 on this source: confirm that the early-turn native Destroy burst is gone, then distinguish GC/motor/coroutine/native ownership of the input and coast maxima with matching verifier boundaries; collect first-failure history using P22's corrected input freeze. Competing explanations are residual destruction, managed allocation/collection, other project callbacks or external/native waits. Stop after one original-window binary+history witness, then analyse its exact maxima and first failed cell before another production change or clean test. No allocation stacks/deep profiling, changed inputs, extra preparation or increased limits.

## P23Q Project-dock comparison — current clean FAIL

Run18:32:07.2942207–18:32:39.2836754UTC, Docs/G08_R2_Speed260_2026-10-02_183239_P23Q_Background_Diagnostic_Receipt.md. Same runtime MVIDf99d8b98-6f7a-480f-bfad-fcfe52f4d404 and all nine source/build hashes; only bottom dock selection changed from Console to Project. Game viewport616x264 unchanged. Profiler/binary/history off, all351 input/coast frames recorded, focus VALID0/0, no observed compile in Play, returned idle Edit. Audit outputs/G08_R2_P23Q_ReturnedAudit_20261002.json.

| Gate | Latest clean result |
|---|---|
| Staging |PASS17/17,12.181s,0.379m target error|
| Ordinary movement / abrupt6-to-260 / seam / all directions / constant segments |PASS|
| Input full visibility / turn coverage / ground safety |PASS|
| Input max |FAIL63ms; P50/P9534/46ms|
| Full coast physical travel |PASS1153.556m,9.214s,settled within existing threshold|
| Coast full visibility |FAIL first(13,7), required ecology publication incomplete|
| Coast max |FAIL72ms; P50/P9531/42ms|
| Zero fallback / traversal block / recovery / measured focus |PASS|
| Complete focused PASS / independent repeat |NOT OBTAINED / NOT RUN|
| Final affected R2 matrix / R3 |INCOMPLETE / GATED|

The observed P23F-to-P23Q maxima63/84→63/72ms and P95 53/45→46/42ms coincide with a different Editor-pane condition and runtime demand history. No isolated production speedup claim. This is an environment comparison, not a second applied production retirement repair. P23 remains one unsuccessful complete closure despite its confirmed native burst reduction.

Worst clean input frame5859 has63ms wall; coast5995 has72ms wall and managed collection-count increments1/1/1. Recorder publication order does not prove exact native duration/ownership. D46 no-GC maxima do not attribute this clean GC-associated interval.

First coast cell(13,7) snapshot: hard demand age2.372s, collision1.320s, content/ecology start0.333s. Content/overlays/appearance are ready, contentReadyAt is current frame, ecology is not published. Its ecology phase is waitingForRequiredOwner, age0.098s; required ecology iterator7 steps/5.706ms, last step5909, no recorded ecology budget denial. Structures finish16 steps/2.789ms at5913, with5 prior budget-denied frames. This points to a possible completion handoff: prepared ecology yields until structure/overlay readiness, then its coroutine resumes on another frame. It does not prove all previous delay is redundant or justify bypassing publication/overlay/token gates. Inspect this existing owner boundary and historical repair evidence before any coherent handoff repair; the exhausted scheduling/reservation loop remains stopped.

No live session or pending diagnostic marker remains after this comparison. Full R2 remains FAIL/incomplete. Next bounded work is the required-owner/ecology completion boundary and current motor/camera allocation source (D46 motor429472B/4872 events); do not start another broad unchanged capture or clean retry. Preserve all existing acceptance limits and state.
