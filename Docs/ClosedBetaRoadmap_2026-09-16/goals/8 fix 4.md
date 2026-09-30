# /goal G08 — Evidence-led closeout (R1 → R2 → R3)

## Outcome

Complete the original G08 acceptance contract in the production PlaySafe game with current-source evidence. Preserve the accepted world, player control, visual quality, determinism, and save authority. Repair reproduced causes in order: truthful publication/recovery (R1), seamless playable streaming and measured throughput (R2), then the physical G08 world itinerary (R3).

This goal replaces the execution workflow in “8 fix 1.md”, “8 fix 2.md”, and “8 fix 3.md”. Their acceptance requirements remain in force, along with the authoritative Goal-08 design and applicable AGENTS.md files. Do not add goals outside that contract.

## Evidence snapshot at handoff — 2026-09-27

| Work | What evidence establishes | Status |
|---|---|---|
| R1 publication recovery | The latest runtime report passes cancellation/replacement, retry exhaustion, lag injection, missing-ground recovery, and completion-order checks. The full report ends FAIL. The Sep 24 R1 PASS is historical and applies only to that source/run. | Partial; obtain a current R1 PASS. |
| R2 startup and ordinary movement | The latest report passes accepted-save terrain startup, initial coverage, and short walk/run/dash/reversal/diagonal/rapid-turn probes. No standalone R2 PASS receipt was found. | Open. |
| R2 abrupt 6→260 speed stress | The Sep 27 report fails staging and boundary-approach evidence. Its route contacted the accepted waterfall-cave entrance around z=24.1 before reaching the original z=80 target. The verifier was changed at 04:32:17Z to stage near the east origin edge; the report ended at 04:24:33Z, so the latest staging edit has no runtime result yet. | Open; first short witness to run. |
| R2 throughput and resources | The report records failed coast/throughput gates, including a 104 ms coast peak against the existing 50 ms budget. Heartbeats sometimes show 257 physical owners against a configured capacity of 256; this alone does not prove a leak. | Open; profile the matching frame and reconcile ownership. |
| R2 unload/revisit | Candidate selection failed to find a published physical cell after 300 seconds; unload/revisit therefore never ran. | Open; fix candidate selection/diagnosis before retry. |
| R3 profile continuity | The Sep 25 disposable-profile receipt passes paired terrain save/load, restore after Play Mode restart, accepted graph identity, and New Journey/Continue presentation. It explicitly excludes the world itinerary, ecology, throughput, and unload/revisit. | Narrow rows passed; preserve and reuse this evidence. |
| R3 physical world | Static repairs and appearance captures exist. The ordinary player-view itinerary, complete site traversal, ecological coverage, and stable save/revisit proof have no complete PASS receipt. The G08 handoff remains PARTIAL. | Open. |

Audit basis: Docs/ClosedBetaRoadmap_2026-09-16/goals/Goal-08.md; Logs/YQSemanticChunkRuntimeVerification.md; Docs/G08_R1_Runtime_2026-09-24_090351_PASS.md; Logs/G08_R3_ProfileRoundTrip.md; Logs/YQ_G08_Handoff_Receipt.md; Assets/Assets/Scripts/Generated/YQSemanticChunkRuntimeVerification.cs; the three supplied “8 fix” goal files.

The Sep 24 R1 receipt explicitly leaves G07 speed/frame gates NOT_VERIFIED and reports a 2.541 second maximum content slice. The older handoff’s 160-owner limit is stale; inspect current source and use the current 256-owner contract. Keep 150/300 m/s inherited G07 probes separately classified unless the current accepted G07 contract requires them. The user-required 6→260 m/s edge case remains a distinct stress check.

The audit explains the repeated long runs: R3 work began while upstream R1/R2 evidence was still open; the long verifier couples independent checks; the 260 witness met cave collision geometry; and unload/revisit spent five minutes on a candidate that was never physically published. These are evidence-based contributors. Do not infer effort history or blame a model.

## Authority and scope

- The original Goal-08 file and current accepted saves/artifacts define content and deterministic world authority. The player/world save remains canonical. Keep the V1/V2 routing and current streaming ownership intact.
- Use the existing authoritative player motor, physical collision, real elapsed time, and ordinary PlaySafe/title/profile flow. Keep movement unrestricted; do not stop the player at an unready boundary.
- Complete every visible cell and maintain a collision-ready lookahead so the player never sees a hole or waits at a streaming boundary. At 260 m/s, the visible, swept-footprint, turn, and lookahead coverage must keep up.
- Keep rivers visibly wet and flowing with their accepted natural appearance. Preserve visual fidelity while reducing cost. Do not revive edge foam, flat materialless channels, or waterless river/lake beds.
- Use the documented Unity access method in UNITY_MCP_GUIDE.md. Verify the actual Editor and PlaySafe state before acting. Only one operator drives the Editor. Do not ask the user to manually focus, expose, restart, or compile Unity when the documented local route can do it.
- Use a disposable G08 profile for destructive test setup. Preserve ordinary profiles, accepted artifacts, seed, IDs, and existing user changes. Continue must load the same saved world.
- Settlement service means the required physical service anchor/interior. Resident simulation, merchant transactions, dialogue population, long-session pacing, and broad content expansion remain out of scope.

## Execution

### 0. Establish one compact evidence ledger

Inspect the current working-tree diff, applicable AGENTS.md files, current source/build identity, the four named G08 receipts, and only direct owners implicated by the next failing row. Record every original G08, R1, and R2 acceptance row as PASS, FAIL, BLOCKED, or NOT VERIFIED, with its source version, profile/world/seed/artifact identity, evidence path, and reason.

Keep valid evidence when its implementation and dependencies are unchanged. The Sep 27 report may support its individually passing fault/movement rows; it cannot certify the post-04:32:17 verifier edit or an overall PASS. Do not rerun passed rows merely to refresh timestamps.

### 1. Repair the shortest failing physical witnesses and close R1

1. Compile the current coherent source outside Play Mode and verify the actual PlaySafe session. Run only the changed speed-260 staging/boundary witness first. Capture the true capsule position, collider/owner ID, input, real frame time, requested and actual displacement, cell coverage, and readiness state.
2. Determine whether the cave contact is an invalid verifier route or a real blocked playable entrance. Use a capsule-clear route for the witness. If the accepted entrance itself blocks traversal, repair its true geometry/ownership. Preserve required collision; never remove a collider, relax a gate, teleport, clip displacement, fake elapsed time, or grant the scheduler extra work.
3. Repair unload/revisit candidate discovery so it selects a currently published, traversable owner from authoritative streamer state. Reject an invalid candidate promptly with its exact reason and try only a small bounded candidate set. Never wait hundreds of seconds on the same absent owner. Exercise actual outward travel beyond retention, unload, return, and republish with lifecycle/version evidence.
4. Make each verifier phase persist its result and failure immediately. An independent failure must not erase later independent diagnostics. Bound every wait by the applicable readiness deadline; preserve explicit failure rather than converting a timeout into PASS.
5. Close R1 with its named publication contract: every demanded incomplete stage has live versioned work, bounded retry, or explicit failure; appearance cannot be missing without a painter; publication waits for required terrain/content/appearance/ecology/overlays/activation. Preserve current PASS evidence for unchanged cancellation/replacement, retry exhaustion, lag, missing-ground, and completion-order checks. R1 is PASS only when every R1 row is supported against current affected source.

Do not begin throughput redesign while R1 has a reproduced publication/recovery defect. Continue to the next phase when the R1 gate is complete.

### 2. Close R2 streaming, throughput, and resource accounting

After R1 passes, exercise cold startup and accepted-save Continue; ordinary walk/sprint/dash, diagonal movement, reversal, rapid turns, sustained travel; then the distinct abrupt 6→260 m/s stress and unload/revisit. Keep production motor, collision, frame clock, input, camera, and world authorities. Set speed as a documented stress input; report this separately from normal supported-control results.

For each run, capture frame-time distribution and maximum, exact matching main-thread/streamer stage costs, allocation/GC, queue and painter depth, terrain and semantic work, current/visible/swept/turn coverage, readiness interventions, and every physical owner/work lifetime. Investigate the 257/256 observation by reconciling live owners, retained owners, in-flight publication, and census timing. Fix the owning lifetime/accounting defect. Do not raise capacity to hide over-retention without evidence.

Use the existing R2/G07 budgets and deadlines, including the current 50 ms coast gate where applicable. Record the governing source for every numeric threshold; never relax a threshold just to pass. Prior CPU notes about material repair and detail-prototype assignment are hypotheses until a fresh matching-frame profile ties them to a measured stall. Split indivisible work only when the profile confirms it, retaining identical materials, water, ecology, and detail quality.

R2 passes only when complete startup/visible coverage is ready before presentation, no visible or swept cell is incomplete, ordinary play and the 260 stress traverse without boundary waiting or readiness slowdowns, coast/frame budgets pass, owner/job/painter use stays within the declared bounds, and deliberate stalls fail honestly with recovery evidence. No freeze, clamp, trap, teleport, camera lock, fog, reduced view, or readiness-dependent speed penalty is allowed.

### 3. Prove the original G08 world in ordinary play

After R1 and R2 pass, use the accepted persisted feature IDs to plan one compact PlaySafe itinerary. Physically traverse and capture:

- Safe origin hut arrival and a clear exit route.
- Connected roads/routes, a real waterway and crossing, meaningful relief, and visibly attractive moving water.
- The required settlement service anchor/interior, hostile/ruin site, enterable cave/interior, landmark/resource area, and representative multi-cell site with stable single ownership, ingress/egress, sector activation, and bounds.
- Canopy variety, understory, shrubs, grass/groundcover, rocks, deadfall, shore/road dressing, outskirts, and intentionally quiet habitat.

Use the real player capsule and continuous input/position evidence across site and cell boundaries. Capture player-view evidence of visual quality and physical access; accepted records, renderer counts, screenshots without traversal, or synthetic markers cannot substitute. Verify grounding/burial, seam continuity, route/water connectivity, clearance, culling, colliders, no duplicate owners, stale-work rejection, both traversal orders, unload/revisit, reversal, and deterministic IDs/layout on replay. Reuse the passed profile round-trip receipt unless a relevant save/startup owner changes; then rerun only the affected round-trip rows.

Do not repair unobserved systems. For each reproduced issue, name the owning source and failure evidence, make one coherent patch, compile once outside Play Mode, run the narrow regression, then rerun only affected acceptance rows. Preserve all accepted visual fidelity and deterministic contracts.

## Time and stop rules

- One active defect and one owning boundary at a time. Use at most one bounded implementation agent for a disjoint scope; use Astra High only for an unresolved cross-owner decision and an independent reviewer only at a gate. The primary agent owns integration and every PASS verdict. Only one agent operates Unity.
- Do not repeat whole-suite runs to diagnose one failed row. Run the shortest failing witness, inspect evidence, then continue to independent rows if safe.
- After three unsuccessful repairs of the same demonstrated root cause, stop that loop. Report the exact evidence and decision/blocker, then complete unaffected rows.
- If Unity runtime access is unavailable, follow UNITY_MCP_GUIDE.md, finish safe source/evidence work, and report runtime rows NOT VERIFIED. Do not ask the user to manipulate the Editor.

## Completion and receipt

G08 completes only when every original Goal-08 row and all required R1/R2 regressions are PASS on the final affected source. Publish one concise receipt with build/source identity; schemas, profile/world/seed/artifact IDs; each row’s verdict; runtime steps; frame/resource measurements; logs and player-view captures; and any remaining deferred issue with reproduction, severity, owner, blocked consumers, and next test. A historical PASS, compile, partial movement probe, profile round-trip, static record, or a successful phase cannot substitute for a missing current runtime gate.
