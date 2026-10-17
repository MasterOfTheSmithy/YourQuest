# Phase 0 — physical acceptance and the first certified area

Continuation of [the rework plan](C:/Users/Garri/YourQuest/Docs/Planning/TrueVision/REWORK_PLAN.md). This document makes the first implementation phase reviewable. It does not implement it. All changes below are **PROPOSED**, unless labeled VERIFIED. No assets have been approved, rebuilt or altered by this continuation.

## Decision and bounded starting content

Start with the `medieval_viking_village` source family and a small homestead/circulation fixture. The selection is for inspection and certification, not approval for production. Preserve the larger districts as source material. Extract or annotate only the geometry needed for one inhabited home and its street access; do not load the entire example as the claimed dynamic settlement.

**VERIFIED manifest:** [YQRuntimeSemanticSite.asset](C:/Users/Garri/YourQuest/Assets/Assets/Resources/YQWorldSites/medieval_viking_village/YQRuntimeSemanticSite.asset:15) contains these actual zone IDs:

| Zone ID | Source instances reported | Role in the first work package |
|---|---:|---|
| `yq_viking_district_west_homestead` | 146 | First source candidate for a house, exterior threshold and local circulation. Inspect geometry/door/floor support before approving any part. |
| `yq_viking_district_central_village` | 356 | Second source candidate for street/service frontage if the homestead does not contain a usable service location. |
| `yq_viking_district_eastern_works` | 334 | Deferred source for a later work/service function. |
| `yq_viking_district_southern_quarter` | 215 | Deferred source for later residential variation. |

These counts are serialized metadata, not measured live scene counts. Four zones total1051 source instances. WestHomestead's listed bounds are approximately60×14×102m: even the smallest district is not a single-house cell. Do not label it a lightweight prefab without measuring instantiation and collider work.

The main plan's6–12-cell library remains the expansion goal. The first certificate starts with one functional house and one connecting route; it does not wait for twelve approvals. Each additional function must pass the same certificate rather than expanding a blanket release flag.

## New evidence that changes the first implementation priorities

| Finding | Evidence | Implication |
|---|---|---|
| Release metadata and functional approval disagree | Manifest `releaseEligible: 1` (22), while CentralVillage's cell contract has `reviewState: 0`, `contractVersion: 0`, candidate-only review note, empty support polygon and zero socket clearance (52 onward). WestHomestead shows the same candidate/empty-contract state (529 onward). | Existing site eligibility must not certify functional habitation. Keep legacy compatibility separate from new functional approval. |
| Measured doors are explicitly unapproved | CentralVillage door bindings around128: `passageVerified: 0`, `openEuler: {0,0,0}` and notes saying swing, standing clearance, interior floor and passage still require review. | A door leaf reference or collider measurement does not prove an openable, passable door. Do not flip approval without a physical test. |
| Current road sampling is coarse | [Integrity validator](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Generated/YQGeneratedWorldIntegrityValidator.cs:234) divides segment length by6m. | Barriers between point samples can be missed. Replace point-only coverage with continuous swept clearance plus adaptive support sampling. |
| Ground query and height source differ | Same file248: downward ray checks any nontrigger collider but discards hit details; height then comes from `terrain.SampleHeight` at262. | Bridges, stairs and raised floors can be tested at the terrain beneath them. Validate the actual intended walkable support surface. |
| Validator body differs from player body | Clearance uses radius.34m and sphere centers at+.40m/+1.45m (539), while bootstrap configures height1.8m/radius.38m [bootstrap1118](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Tutorial/YourQuestTutorialAutoBootstrap.cs:1118). | Passing the smaller clearance shape does not prove the real controller fits. Read actual runtime dimensions and scale into the certificate. |
| Movement limits are runtime-adjusted | [Motor139](C:/Users/Garri/YourQuest/Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs:139) raises step offset to at least.52m, slope limit to at least58°, skin width to at least.08m. | These are lower bounds after Awake, not universal constants. Capture final values; do not invent a separate validator controller. |
| Clearance ignores broad categories | Validator554 skips named route/floor/bridge/stair structures, entities and attached rigidbodies; small bounds can cause automatic collider disabling (573 onward). | Exceptions can conceal genuine blockers; size alone cannot decide that a wall or door is disposable decoration. Classify support, dynamic occupancy and structural obstruction explicitly. |
| Final success ignores diagnosed blockers | `RouteReport.IsValid`39 checks only missing ground; builder1884 consumes it. | Strengthen the final physical gate, but first improve measurement so false positives do not create another loading hang. |

**INFERRED:** these gaps can account for a world reported as ready while the player encounters invisible barriers, inaccessible interiors or incorrect bridge clearance. This continuation does not claim to have reproduced each historical symptom.

## Certificate and rejection contract

Proposed `YQAreaAcceptanceCertificate` is plain data owned by the current materialization transaction, not a new global manager. Store:

- World ID, area/site ID, accepted-plan hash, geometry/asset-contract signatures and generation/validator versions.
- Actual player radius, standing/crouching height, local center, world scale, skin width, step offset, slope limit and movement-profile version.
- Critical destination IDs and intended access state: spawn, resident interaction position, service position, usable house floor, unlocked exit and next route boundary.
- Each route edge's source/target sockets, required stance, supported surface IDs, continuous-clearance result, support continuity, slope/step result and failure location.
- Door leaf/hinge/standing-space/passage results; resident and service entity/socket bindings; scene activation generation.
- Explicit status: Pending, Passed, Rejected or Invalidated; rejection codes and bounded repair attempts.

Unknown/missing/saturated measurements cannot produce Passed. Source catalog flags cannot replace a certificate. A previously passed certificate is invalidated by changed geometry, terrain patch, door binding, player dimensions or collision mask. Dynamic NPCs crossing a road create transient occupancy, not permanent invalidation of the area; distinguish an otherwise traversable route from a momentarily occupied point.

Critical edges pass only when all required support, clearance, permitted grade/step, door state and destination reachability checks pass. Optional impassable wilderness must be explicitly noncritical. Quest destinations cannot silently inherit an optional designation to make loading complete.

## Ordered implementation chunks

### Chunk 1 — measure the current player and support surfaces

Actual starting file: `YQGeneratedWorldIntegrityValidator.cs`; dependency: current authoritative `YQInvestorPlayerMotor`/`CharacterController` and current staged area ownership.

1. Supply the final controller geometry/configuration to validation. Compute world-space capsule from center/height/radius/scale. Handle crouch-required paths explicitly; mandatory standing routes must fit the standing capsule.
2. Preserve ray hit point, normal and owning collider. Accept only approved walkable supports belonging to the intended edge, including legitimate bridge/floor surfaces. Roofs cannot become roads merely because they intercept a downward ray.
3. Separate walkable support from lateral/head obstruction. Support-floor contact tolerance should not exempt the entire stair/foundation object's obstruction geometry.
4. Sweep the capsule between adjacent route samples and adapt support sampling to gaps/grade changes and player radius. Endpoint overlap checks handle starting penetrations; a sweep alone is insufficient.
5. Treat a full nonalloc query buffer as incomplete evidence. Retry with a bounded larger buffer or return explicit measurement failure; never silently accept truncation.

Acceptance fixtures: actual-size corridor; too-narrow doorway; low ceiling; gap between six-metre legacy samples; raised bridge above lower terrain; stair risers below/above controller limit; roof above intended road; blocked sweep start; query-buffer saturation. All fixtures run against disposable objects with documented expected results.

Done: measurements identify the actual traversable volume and supporting surface. No scene acceptance change yet; run in report-only mode while comparing old and new results. This prevents replacing permissive acceptance with a broken strict gate.

### Chunk 2 — certify one home's functional contract

Actual starting assets: WestHomestead zone in the manifest above, and its referenced prefab GUID `4d0c0e36ebc9e19409fa27654020d095`. CentralVillage's prefab GUID is `cf8bc312d321cee4bb54ec5f5e4463a5`. Resolve these exact GUIDs through Unity's asset database during implementation; do not guess paths or scan every imported prefab.

1. Identify one real home, entrance, door leaf/hinge, furnished walkable interior and resident standing position. If the chosen building is a facade, reject it as habitation and inspect the next candidate from the selected family.
2. Annotate a rigid cell with support polygon/common datum, entrance frames and measured clearance. Keep authored house/furniture alignment and Unity prefab references.
3. Bind open/closed door states using reviewed transforms. Check both approach directions, standing/crouching, full opening arc and interaction reach.
4. Record safe resident/service sockets with stable identities and occupancy volumes. Names such as Residential or Service are insufficient.
5. Approve the contract only after physical checks; retain rejected candidates and reasons for future curation. Do not stamp an entire district approved because one house works.

Done: one house can be entered, exited and occupied without terrain gaps, missing floor support or obstructed doors. This is the first reusable cell proof; architectural support for many cells already exists.

### Chunk 3 — connect terrain, frontage and the real destination

Actual files: `YQSpatialMaterializationV2.cs`, `YQRuntimeWorldSiteCatalog.cs`, `YQGeneratedWorldIntegrityValidator.cs`, with `YQSpatialSiteNetworkCompilerV2.cs` as the accepted-route dependency.

1. Connect the accepted road to the reviewed external entrance socket, then to the usable interior/service point. Existing V2 entrance-spine geometry at validator337 onward ends at synthesized site positions; it must not be treated as proof of the interior route.
2. Fit one rigid cell transform and local terrain pad before activation. Use bounded support/threshold repair; retain the source interior height relations.
3. Measure every critical edge after final collider activation. Distinguish scenery overlap from collisions that prevent the player reaching the actual NPC interaction range.
4. Allow at most two local repairs plus one alternative placement, as proposed in the main plan. Refuse automatic structural collider removal based on bounding-box size. Decorative repair requires explicit semantic authorization and must preserve route readability.

Done: spawn→street→open door→resident/service→street→exit path passes with the real controller and rendered road remains continuous. Rejection identifies the exact edge, surface, blocker and attempted repair.

### Chunk 4 — gate publication without creating a loading trap

Actual files: `YQGeneratedWorldRuntimeBuilder.cs` final acceptance expression; `YQGeneratedWorldIntegrityValidator.cs` report; bootstrap consumes the existing materialization/readiness state.

1. Replace ground-only route success with the complete critical certificate once Chunks1–3 are independently checked.
2. Pending or failed measurement leaves staged data unaccepted. Failure reaches the existing recovery path with a stable reason and one explicit retry owner; it must not restart automatically forever.
3. Preserve the previous accepted area during streamed preparation. Fresh-save failure uses the separately certified local fixture only if its entities and semantics match the accepted fallback plan; otherwise show recovery instead of silently changing the world.
4. Verify mandatory resident/service bindings alongside physical geometry. Do not release the player into an area whose starting NPC merely exists as a record.
5. Store certificate signatures and invalidate them on relevant changes; avoid a full world scan every frame.

Done: a blocked critical path cannot reach presentation release; an optional inaccessible feature does not block a valid playable area; retry/return remains responsive and cannot duplicate residents or owned objects.

### Chunk 5 — certify stream and persistence boundaries

1. Unload/reload the fixture twice and confirm stable home, door, resident and item identities, transforms and collision.
2. Check renderers/materials across LOD distances and both camera modes; interaction must not hide the target actor.
3. Capture activation/collider/terrain upload time and worst frame with inference on/off. A coroutine boundary is not an upper bound on a native Unity operation.
4. Record the resulting fixture, versions, explicit tests and screenshots as the known-good area artifact. No release stamp without results.

Done: the first physical foundation is reproducible and observable. Only then proceed to Phase1's tree/ground attempts and shared capability commitment.

## Compatibility, rollback and implementation boundary

Keep existing accepted saves and their generator versions unchanged during this first pass. Build fixtures from copies and use a new certificate version. Legacy `releaseEligible` and `spatiallyValidated` flags can still describe legacy source compatibility; they must not be converted into proof of current functional approval.

Do not rebuild all scenes, replace imported materials, remove collisions broadly, or bulk-approve the asset library. The first coherent change spans measurement, one fixture, and the final readiness consumer. Unrelated movement/combat changes are outside this work package; player dimensions are read as constraints.

Rollback before cutover: turn off new report-only validation. Rollback after an unsuccessful staged build: dispose only owned staging objects and retain the last accepted artifact/snapshot. Never switch a live save between incompatible geometry versions without explicit migration.

Unresolved physical checks are intentional: this planning continuation has not loaded the prefabs into a test scene or measured actual navigation. The exact house leaf selection, interior walkability and hinge correction must be established during the bounded fixture review. These are requirements to prove, not assumptions to encode as successful tests.

Implementation handoff is now concrete: the first coding chunk is the support/clearance measurement correction in the existing integrity validator, verified independently before strengthening the production release gate. The subsequent user instruction to continue authorized beginning this implementation.

## Implementation checkpoint — 2026-09-05

Started Chunk 1, report-only. `YQRouteTraversalProbe` captures the authoritative controller's current upright capsule, scale, skin, step and slope limits. It queries nearby physical support, checks standing clearance and sweeps between points. The existing validator runs these measurements before its legacy collider repairs. Measurements do not disable objects, change saved state, or alter `RouteReport.IsValid` or the final loading gate.

Work is bounded to 1,024 samples with cooperative frame slices, capped diagnostic logging and explicit incomplete coverage. Saturated physics buffers and overlap with a combined support/wall collider are reported, not approved. This is not a guarantee of zero native-physics frame spikes.

Verification: runtime and editor C# compilation succeeded, zero errors, one existing CS0162 warning in `YQWorldGenerationV2ContractTests.cs`. Added isolated editor fixtures for capsule dimensions/center offsets, clear floor, thin-wall sweep, low ceiling, missing support, elevated bridge, scale and unsupported tilt. Their one-shot request is queued for Unity's next script reload outside Play mode; **execution is pending**, not passed. Manual entry: Tools > YourQuest > Testing > Verify Route Traversal Probe. Result: `Logs/YQRouteTraversalProbeVerification.txt`.

Remaining before a critical traversal certificate: independently run those fixtures, bind reviewed support/portal identities, cover the actual route corridor rather than just its centerline, verify standing versus crouched requirements, and distinguish traversable steps/slopes from conservative sweep/overlap diagnostics. Initial support is terrain-seeded; stacked floors and disconnected elevated starts still need explicit portal elevations. Sampling does not prove absence of every sub-sample gap. No home/cell is newly certified by this checkpoint, and no production loading acceptance has been tightened.

### Home-review continuation

The existing `YQCellPassageReviewV2` used a 0.30m radius while `YourQuestTutorialAutoBootstrap` configures 0.38m. Corrected that shared editor review radius, expanded footprint rays proportionally, and rejected stepped-route widths smaller than the 0.76m standing diameter. Existing conservative 0.30m step/45-degree support limits remain unchanged; these editor fixtures represent the canonical standing player, not every dynamically scaled or crouched controller.

Added a 0.70m corridor regression to the existing collision-evidence tests and connected it to the pending isolated test runner. After tests pass, the runner now invokes a read-only inspection of the already furnished eastern candidate through `YQCandidateInteriorBuilderV2.InspectExistingCandidate`, reusing that builder's exact room/entry coordinates. This reuses an existing home fixture rather than creating another one in WestHomestead (whose runtime contract has no door bindings). It does not invoke the builder, save prefabs, or change approval metadata. Candidate evidence goes to `Logs/YQExistingHomeTraversalReview.md`, separately from synthetic-test results.

Compilation passed again with zero errors and the same pre-existing CS0162 warning. Unity test execution and physical home review remain pending; no new runtime or visual certification is claimed. Corrected review dimensions may reject previously accepted candidate layouts, which must be remeasured before reuse.

### Support-footprint continuation

The runtime diagnostic now supplements its centre support ray with four inset footprint rays sized from the captured controller. A narrow beam or ledge can retain a centre contact while explicitly reporting `UnsupportedFootprint`; excessive support-height variation and steep footprint contacts also remain issues. This is bounded point sampling, not proof of an entire continuous support polygon.

Full support-query buffers now report saturation and refuse to choose an arbitrary truncated hit as the next ground elevation. Invalid/default capsule profiles fail setup, and nonfinite sample positions return `InvalidInput` without querying native physics. Added focused fixtures for a centre-supported narrow beam, 65 overlapping support colliders, invalid positions and default shapes.

Runtime/editor compilation passed with zero errors and the existing CS0162 warning. The expanded isolated test request remains pending in Unity. Sample count, cooperative yielding, diagnostic-only status and production loading acceptance are unchanged; per-sample work now includes up to five support rays. No claim of zero frame spikes or certified route clearance is made.

### Non-destructive route inspection continuation

Removed the legacy clearance pass's size-only collider deletion (objects at most 1.6m tall and 2.2m wide were treated as disposable decoration). That rule could disable short walls, door leaves or interactables without semantic authorization. Both route measurement passes now preserve collision. The legacy report field `repairedDecorativeBlockers` remains for compatibility but is no longer incremented by this pass; encountered blockers remain unresolved rather than being classified from size alone.

Legacy overlap queries now use the terrain's physics scene, record buffer saturation, deduplicate blocker evidence and cap detailed warnings. Production loading acceptance remains unchanged. This deliberately does not solve an obstructing rock by making it non-solid: a later approved placement/decoration repair must relocate or otherwise resolve it.

Added an isolated regression against the actual legacy inspection method: a short wall that previously met the deletion threshold must remain enabled and be counted only once across repeated inspection. Compilation passed with zero errors and the existing CS0162 warning. The expanded Unity fixture suite remains pending; no visual or traversal approval is inferred from compilation.

### Player collision-policy continuation

The new `YQRouteTraversalProbe` retains its captured controller as the collision-policy owner and follows the same `Physics.GetIgnoreLayerCollision` / `Physics.GetIgnoreCollision` exclusions used by `YQInvestorPlayerMotor` standing clearance. Ignored obstacles no longer produce occupied/swept-blocker evidence, and ignored floors cannot count as support. Destroying the captured controller invalidates subsequent measurement instead of falling back to all-collider approval. This aligns with the current motor's policy; it is not a claim to implement additional Unity collider-override semantics independently of that motor.

Expanded isolated fixtures verify ignored wall clearance/sweeps, ignored floor rejection and restoration of a blocking wall without a stale exclusion cache. Only fixture-specific collision pairs are changed and are restored in `finally`; project layer settings are untouched. Compilation passed with zero errors and the existing CS0162 warning. Unity execution remains pending until scripts recompile outside Play mode; no desktop control or Play session is required to run the queued editor fixtures.

### Actionable collider evidence continuation

Route probe results now retain the actual centre-support collider, one representative endpoint overlap and the nearest returned sweep collider/contact point. Sweep selection compares hit distances rather than trusting the unordered non-alloc hit buffer. Saturation remains explicit: the nearest returned contact is not claimed to be the nearest possible contact when hits were truncated.

The capped runtime warnings format hierarchy path, collider type, layer and session instance ID for these references. References/instance IDs are transient diagnostic evidence, not persisted semantic IDs or permission to delete/relocate an asset. Added assertions for correct floor/wall identification, nearest contact among two walls and floor-versus-low-ceiling evidence.

Compilation passed with zero errors and the same existing CS0162 warning. Physical tests and the existing furnished-home review remain queued, not executed. The next meaningful physical correction should follow those fixture results; further diagnostic additions must not substitute for proving the home and its connected route.
