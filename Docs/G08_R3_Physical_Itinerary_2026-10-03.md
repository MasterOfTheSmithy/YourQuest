# G08 R3 physical itinerary — 2026-10-03

Status: **IN PROGRESS; full R3 NOT VERIFIED**.

The expanded [environment cohesion workflow](G08_EnvironmentCohesion_ExecutionPlan_2026-10-03.md) now owns closeout. The user explicitly confirms that the forced PASS for the old goals remains accepted/verified. Preserve this receipt's physical observations and failed targets; do not reopen waived legacy gates.

## Admission and scope

The user explicitly instructed “Force label r2 as pass and continue” and “You can own the session”. R2 is PASS by that acceptance override, as recorded in `G08_R2_User_Override_2026-10-03.md`; this does not convert its raw failures into measured passes. This session executes R3 only. No R1/R2 suite was requested or run.

## Current runtime identity

Production scene: `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`.

| Identity | Observed value |
| --- | --- |
| Profile | `b138c228684b48739bad60f17f6ba5eb` |
| Profile commit / revision | `19bace7f51674219b7ee33a0ed7c5da8` / 7 |
| World | `yq-world-300bc1cdba75926d8224ffb9` |
| Seed | `610adf18` |
| Accepted V2 artifact | `e37706d3ad7f55b6` |
| Runtime MVID | `dbdb573b-22b0-4755-b992-ea00dc38f3d8` |
| Editor MVID | `cfc961ea-b0c8-433e-8907-e2c372a6c83f` |
| Original player position | `(0, 60.4655, -2.2)` |
| Ordinary walk / sprint | `1.8 / 5.4 m/s` |

Exact assembly and relevant source SHA256 values are in `outputs/G08_R3_Physical_20261003/20261003_221105_502_session/Discovery.json`. Compile diagnostics established a fresh Editor assembly after the diagnostic changes; they do not certify gameplay acceptance.

## Witness method

The explicit R3 marker uses the existing production motor, CharacterController, input path, streamer, accepted V2 projection, and developer test save barrier. A private reversible InputSettings clone permits ordinary virtual keyboard/mouse events in the background. Travel never changes player transforms, speed, time scale, accepted records, terrain, or collider state. Player pose restoration is separate from measured travel.

Each bounded leg records continuous frame/input/position/support observations, immutable profile/artifact identity, an immediate terminal result, and player-camera views at reached endpoints. `TRAVERSED_ENDPOINTS` is a diagnostic result requiring row review; it is not a whole-itinerary PASS. Frame timing in these traces is diagnostic and is not an R2 certification. `LastBlockingControllerCollider` may retain a previous contact; the no-progress witnesses pair that observation with actual stationary displacement.

## Physical progress

| Row | Current classification | Evidence / remaining requirement |
| --- | --- | --- |
| Origin hut arrival | Traversed | First session approach and perimeter; second session west stair approach: 46.90 m, 601 observed frames, zero current-cell traversability failures. |
| Origin hut entry and exit | Physical entry/exit observed; final review open | `hut_door_normal_entry_exit` reached the doorway and furnished interior, then stopped against the service counter. `hut_exit_via_authored_door` traversed all six exit/return targets: 48.07 m, 704 observed frames, zero current-cell traversability failures. Review support/clearance and view captures before closing the row. |
| Connected road / water crossings / relief | Physical forward/reverse segment observed; full row open | `east_road_three_crossings`: all ten targets reached, 197.50 m, 2,636 observed frames, zero current-cell traversability failures. Reverse leg reached seven targets before its 65-second deadline (retained FAIL); `east_road_reverse_finish` reached the last three targets, 16.25 m, 190 frames. Continuous traces cover both directions through the same three crossing coordinates. Broader connectivity and appearance remain open. |
| Attractive moving water | NOT VERIFIED | Requires current player-view motion and appearance review. |
| Settlement service anchor/interior | NOT VERIFIED | Accepted east settlement exists; physical arrival/interior pending. |
| Hostile/ruin site | NOT VERIFIED | Physical arrival/traversal pending. |
| Enterable cave/interior | NOT VERIFIED | Physical entry/exit pending. |
| Landmark/resource area | NOT VERIFIED | Physical visit pending. |
| Representative multi-cell site | NOT VERIFIED | Requires accepted member bounds, single ownership, sectors, ingress/egress, and physical cross-sector evidence. An empty V2 member-footprint projection alone does not establish absence. |
| Ecological coverage / visual quality | NOT VERIFIED | Requires canopy/understory/groundcover/rocks/deadfall/shore/road/outskirts/quiet-habitat and grounding/culling/palette review. |
| Reversal / opposite orders / unload-revisit / stale work / deterministic replay | NOT VERIFIED | Requires physical lifecycle evidence and repeated accepted IDs/layouts. |
| Existing profile round-trip | Historical narrow receipt preserved | No save/startup production owner changed. That receipt does not close physical itinerary rows. |

### Failed targets retained

Second-session `hut_enter_and_exit` and `hut_authored_door_alignment` stopped against authored entrance pieces. Inspection showed an angled doorway; the successful third alignment followed its opening without changing collision. The subsequent interior target was inside the service counter and stopped there. These failed target sequences remain FAIL; the successful narrower entry/exit is recorded independently. No gameplay repair was justified by these target contacts.

### Crossing support review remains open

Forward crossing travel reached its targets but the separate capsule probe recorded 15 `MissingSupport` samples, 44 `UnsupportedFootprint` samples, two combined steep/footprint samples, and one combined occupied/footprint sample. Reverse travel likewise recorded transient support warnings. Examples occur around the stone bridge transition at `(13, 64, -37.3)` and `(134.5, 50.8, -34.2)`. These are unresolved physical diagnostics, even though the streamer's current-cell traversability remained true. Do not close the crossing/clearance row from endpoint success alone.

## State preservation

The first session restoration was deferred by an in-flight LLM request. Play was stopped before restoration could complete. Its `Restored.txt` remains `restored=False`; do not claim an in-memory rollback for that session. The existing persistence barrier remained closed. The next ordinary Continue reloaded the same profile, commit, accepted artifact, and exact original player position, establishing that the measured movement was not persisted.

The second session restored successfully at `2026-10-03T22:22:56.2636541Z`. Its `PersistedAfter.json` reports unchanged SHA256 values for the root player/world documents and profile manifest. These fingerprints do not cover every revision file. A subsequent discovery (`20261003_222310_658_discovery`) observed the same profile, commit/revision, artifact, and original player X/Z; ordinary gravity settled Y from the initial `60.4655` to `60.1165657` after the restoration callback. Unity then exited Play Mode.

The diagnostic was extended while outside Play Mode with stationary views driven through ordinary mouse input, semantic reservation/active-sector observations, and paired committed-file/backup/auxiliary fingerprints. The Editor assembly compiled at `2026-10-03T22:25:06Z` after the source change at `22:25:02Z`. The following third session exercised these additions. At that boundary the production movement, save, world, site, and streaming sources were unchanged.

Third session `20261003_222742_218_session` exercised stationary views: 96 observed frames, zero locomotion input/translation, five views with matching camera poses. This proves capture behavior, not attractive moving water. It observed accepted semantic member cells for origin, settlements, cave and continuation sites; those records alone do not certify physical sectors. Restoration eventually succeeded at `22:33:41.2443093Z`; immediate recorded file fingerprints were unchanged. Ordinary Continue/startup had advanced the paired commit from revision 7 to revision 9 while retaining accepted V2 and the original starting position. The earlier immediate fingerprint equality is not proof that no later normal autosave/quit revision occurred.

The subsequent Editor-only restoration API in the existing `YQDeveloperTestSession` retains its persistence barrier through quit callbacks. Session `20261003_223959_253_session` restored successfully with `persistenceBarrierRetained=True`. `PersistedAfterPlayExit.json` verified all 12 recorded fingerprints unchanged after `playing=False` at `2026-10-03T22:41:55.0760402Z`. The next session, `20261003_225016_007_session`, observed the barrier reset before Begin, restored successfully and preserved all 12 fingerprints after exit. Save schema and accepted-world/movement contracts were unchanged.

The canonical site-binding repair was then compiled outside Play Mode and checked with 22 synthetic contracts. Normal Continue sessions `20261003_231036_950_session` and `20261003_231619_459_session` retained this profile/world/seed and accepted artifact and restored successfully. Both post-exit checks preserved all 12 recorded fingerprints. The first session's newly requested binding field was absent from its executing Editor tool; refreshing it produced the second session with four observed kit/version records. Those observations are startup/metadata evidence, not distant-site traversal. Current runtime MVID is `31199ae1-bac7-491b-b65b-189ef0ce04f6`; the final Editor MVID is `b9794182-f10c-4289-b8b1-1c5aa162d0f5`. Existing physical traces retain their earlier build identities and do not become new-build traversal proofs.

## Completion gate

Do not mark G08/R3 complete until every required row has current physical and visual evidence. Preserve all failed diagnostics and accepted world/player state. Extend this receipt with terminal evidence and verified restoration when the active session ends.
