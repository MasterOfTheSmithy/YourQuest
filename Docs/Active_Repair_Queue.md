# YourQuest active repair queue

Updated: 2026-09-04. Work remains local and unpushed. Verification uses hidden, headless Unity runs; no desktop control or user gameplay session is used.

The existing procedural-world design plan and main GDD remain authoritative. This is an execution queue, not a replacement architecture or a declaration of runtime readiness.

## Latest checkpoint — 2026-09-04

Production-slice pass:

- Retired the live demo tutorial reseeder that deleted generated tutorial quests and cleared player evidence. Accepted quests/counters and active selection now survive bootstrap; missing origin quests can recover from the accepted origin record without generating another origin or awarding equipment again.
- Guarded delayed/duplicate origin completion, added read-only profile corruption preflight and save ownership checks, isolated NPC conversation storage by profile, and invalidated stale dialogue callbacks/repair requests.
- Partial objective completion is saved; exact target IDs cannot match similarly prefixed entities. World/minimap quest guidance now uses structured targets instead of prose scoring and an invented marker ahead of the player.
- Updated Goddess prompt/acceptance rules for concise, varied direct speech in the latest requested restrained voice. Bounded questionnaire evidence preserves all full answers in saved state and deterministic seed input.
- Detailed scope, compatibility implications (including untouched legacy shared dialogue files), verification limits and remaining content blockers: `Docs/Production_Slice_Repair_2026-09-04.md`. Nothing pushed; no desktop control or player-save edits.
- Final hidden Unity compilation and 68/68 regressions passed (`Logs/Codex_ProductionSlice_Final.log`). Real furnished-cell floor/access/door/storage checks passed; repeated prefab and candidate manifest remained byte-identical. Rendered world quality, actual new-save/Continue gameplay, live model prose and worst-frame timing remain unverified.

Storage-provider continuation:

- Added typed `YQCellLootBindingV2` records to exact-cell function contracts and installation through the existing cell-provider coroutine. Each approved slot validates its source revision, local pose, single authored box, measured access socket and exclusive interaction owner before installing `YQLockpickableLoot` on inactive staging.
- Generated loot IDs include length-prefixed location/cell/slot components. Existing consumption flags and reward seeds remain authoritative. Mechanical amounts use the stable slot identity and accepted site threat; no prefab has a fixed generated identity or stored reward instance baked into it. Storage retries preserve their existing owner and consumed state.
- Fixed reviewed small-chest activation: it now retains the authored collider rather than applying the legacy minimum chest envelope. Other generated/authored loot paths retain their current behavior. Unreviewed extra collision, stale access points, active installation, conflicting owners and draft bindings are rejected.
- The furnished eastern candidate now includes a pending personal-storage binding referencing the actual chest prefab and its measured access socket. Real-candidate initialization verifies unchanged collider center, size and count on an inactive disposable copy. This does not mark the candidate release eligible.
- Storage/reward function acceptance now requires a matching reviewed loot provider in addition to the typed affordance and access evidence. The active candidate remains pending, so no static furnishing is silently counted as a released functional settlement.
- Hidden Unity compilation and all 60 contract regressions passed (`Logs/Codex_CandidateStorage_Verified.log`). Expanded checks cover activation collision, repeated ownership, stale/draft/changed geometry, ambiguous ID components and required provider evidence. The real furnished-room, door/floor compatibility and byte-identical repeated authoring checks also passed.
- Remaining: actual terrain approaches, service interactions and rendered gameplay verification before candidate activation. Full open/loot/save/reload gameplay has not been exercised; this pass tests binding, initialization and identity/ownership without awarding items or writing a player save. Nothing pushed.

Furnished-interior continuation:

- The eastern candidate's larger repaired house now contains four source-linked furnishings at authored scale: the NordicVillage bed and MedievalVikingVillage table, chair and closed chest. The layout preserves a continuous entrance aisle; an initial layout with four disconnected standing points was rejected before saving.
- `YQCandidateInteriorBuilderV2` owns the bounded first-kit authoring recipe and repeat verification. Support is measured beneath furnishings with their collision excluded from floor rays only; furniture remains solid for standing and traversal. Exact collision checks reject wall/furniture penetration deeper than 15mm, and each complete furnishing footprint must have structural support.
- Final furnished-room evidence: 187/187 floor samples supported; 91/91 standing-clear points connected to the entrance. Bed/table/chair/storage support checks passed at 66/66, 24/24, 9/9 and 12/12 samples. Three access sockets each passed support and clearance checks.
- Added `YQ_FurnishedEasternCandidate.asset` beside the candidate prefab, outside Resources and the active catalog. It references the actual furnished geometry revision, has a consistent 338-instance budget, and records the measured sleeping/storage/work sockets. All seven eastern door bindings now carry the fitted poses and safe opening directions. The existing runtime door binder was exercised on inactive disposable instances. Persisted review states remain pending and release eligibility is false.
- Final hidden Unity compilation and all 57 contract tests passed, including expanded furniture-support/penetration regressions (`Logs/Codex_CandidateInterior_Verified.log`). Existing room-floor and small-door repair routines also passed after furnishing, changing zero objects. Repeated interior authoring left both prefab and pending manifest byte-for-byte unchanged.
- Remaining for this cell: actual terrain approaches, service providers, furnishing material/appearance review, and deliberate runtime promotion. The newer storage checkpoint above completes the pending loot binding. The bed is a sleeping-space asset, not a new rest mechanic.

Door-clearance continuation:

- Added an isolated editor-only swing review using the runtime's local hinge rotation. Conservative angular envelopes cover the swept collision volume; envelope-only warnings receive a bounded 0.125-degree refinement. Overlap-buffer saturation and sheared transforms fail closed. Exact-pose and closed-pose counts distinguish confirmed collision-shape overlap from envelope-only warnings.
- Regression coverage includes both directions, an intermediate obstruction, a thin between-pose obstacle, repeated measurements, invalid inputs, sheared parents, and source-state preservation.
- The repaired central/eastern candidate prefabs now have a reproducible per-door report at `Logs/V2_CandidateDoorSwingReadiness.md`, tied to their asset dependency hashes. This does not change imported geometry, candidate prefabs, approved metadata or runtime behavior.
- Final hidden Unity compilation and all 57 regressions passed (`Logs/Codex_DoorSwingReview_Refined.log`). Refined sweep clears 10/12 leaves in both directions. The two House2 leaves (`SM_House2_Door_64` and `SM_House2_Door2`) overlap their front-wall collision even when closed and throughout the tested swings. The first coarse sweep's House4 warnings were conservative envelope artefacts, not confirmed collisions; refinement resolves them without changing geometry.
- House2 follow-up: the exact imported wall-mesh collision experiment still overlapped the leaves, so it was rejected without saving. Both hinges were seated in the middle of the wall thickness. The isolated eastern candidate now seats each leaf 0.17902 wall units outward on the measured frame face, preserving width, height, rotation, source prefab links, materials and all original wall colliders. No mesh-collider substitution was saved.
- All 12 candidate doors now have a clear swing direction. The two fitted House2 leaves require **-86 degrees**; +86 swings back into the wall and remains rejected. This direction must be carried into their exact-candidate door bindings during future review, not copied into the old active source manifests.
- All existing eastern entrance checks and room-floor gates passed after fitting: larger room 126/126 supported and reachable; smaller room 56/56 supported, with all 50 standing-clear points connected to its clear entry. Final hidden Unity compilation and all 57 regressions passed (`Logs/Codex_SmallHouseDoorFit_Verified.log`). Two repeated repairs changed zero leaves; the saved candidate remained byte-for-byte unchanged (SHA256 `8836114A7FAD90C31EB382EBA6D0B038775DE2EF05414CDFC93D761569325FF5`).
- The active catalog is unchanged. Furnishing and pending candidate door metadata are now recorded in the newer checkpoint above; terrain approaches, runtime providers and visual review remain. The ten cleared House4 leaves remain untouched.

- Missing LOD material slots now recover compatible materials from the same mesh/renderer family. Recovery no longer searches the entire editor material library, and color properties are checked for the correct shader property type before access.
- Both streamed-cell and prefab-zone selection now prioritize usable, current reviewed function evidence over descriptive district tags. Incomplete/stale providers cannot earn function priority; final approval and exterior-shell restrictions remain intact.
- Prefab-zone additions share streaming-cell connectivity and budget checks. Bounded provider passes run before decorative filling. Already-covered tags no longer force duplicate districts, and optional district counts vary deterministically by seed within the existing cap.
- Hidden Unity compilation and all 56 contract regressions passed, including grounding, collider, material, enemy-visibility, semantic variety and the two new function-selection cases. Final evidence: `Logs/Codex_FunctionSelectionTests_Final.log` reports `failures=0`. The initial run exposed lost variety after connectivity enforcement; optional-density selection repaired it without relaxing connectivity or function checks.
- Actual settlement content remains a blocker: the readiness report lists all 12 settlement kits as exterior shells, and the first kit's functional contracts are pending. Candidate room-floor geometry checks passed, but actual terrain approaches, furnishing/providers and visual approval remain outstanding. These code changes do not turn those candidates into approved homes or activate a new runtime authority.
- Runtime visual quality, loading-to-play behavior and worst-frame timings still require gameplay verification. Headless contracts do not establish zero frame spikes.

## 1. Loading and generation reliability — in progress

- Trace the selected runtime authority and distinguish V2 use from V1 fallback in diagnostics.
- Verify cancellation, retry, profile switching, and final gameplay-unlock ownership.
- Preserve failure details and a responsive recovery screen when a build is rejected.
- Check phase progress reporting against actual completed work; identify unbounded main-thread operations.
- Camera-stage timeout repair implemented: pending load/unload work prevents release, timeout offers handoff-only retry, and the loader no longer retains a completed coroutine handle or recursively retries missing scenes.
- Nested construction and in-place population now use owned iterator execution; cancellation explicitly disposes unfinished steps, and population exceptions expose an inhabitants-only retry during loading.
- Independent site load/unload now owns nested execution, releases its shared slot after cancellation, and quarantines interrupted content. Selected cell/zone count must fully instantiate before readiness is published.
- Required-site waiters now revalidate the exact active registered root on every resume and before completion. A disabled/replaced root fails instead of remaining queued or publishing stale readiness; already-completed sites do not wait behind unrelated streaming work.
- Site cancellation detaches handles and marks intentional interruption before cleanup. Both owned executions are disposed, and slot release runs even if cleanup throws; ordinary cancellation is not mislabeled as unexpected termination.
- Nested progress retains the parent phase; population now reports step 9 of 9. Completed site parts/population batches refresh the watchdog, not waiting labels.
- Next: inspect semantic composition blockers and canonical population coverage; compilation remains prerequisite to declaring these repairs runtime-safe.
- Acceptance: one current transaction owns construction and reveal; incomplete or superseded worlds never unlock gameplay.

Current batch: final reveal was started without an owned coroutine handle. Added cancellation with the build and live plan/root/sequence validation across the loading-screen handoff. Nine source checks and the targeted whitespace/error check passed. Compilation and runtime verification remain pending.

Follow-up batch: fixed title-loader transition ownership for synchronous completion and removed recursive state replay. A three-second handoff timeout now remains locked and exposes recovery instead of unlocking against an unfinished scene transition. Retry preserves the accepted world and retries presentation only. Nine targeted source checks and the whitespace check passed. Compilation and runtime verification remain pending.

Nested-execution batch: added guarded ordinary-iterator execution for the builder's world and population routines, with inner-to-outer cleanup and a bound on immediate iterator scheduling per frame. Exceptions are logged, world failure identity is retained, and unexpected population exceptions no longer repeat automatically. Custom Unity waits still use Unity scheduling; the guard cannot interrupt a long synchronous operation. Nine source checks and the whitespace check passed. Four regression cases were added to the existing harness (wait preservation/success, nested failure, cancellation, scheduling budget), but have NOT been compiled or executed yet.

Required-site lifecycle follow-up: eight targeted source checks and the whitespace check passed. Two regression cases were added for active/exact-owner readiness, completion behind an unrelated occupied slot, and nested cleanup-error aggregation. These cases have NOT been compiled or executed. This pass changes loading ownership only; it does not establish visual quality, resident-floor placement correctness, or worst-frame performance.

## 2. Semantic settlement composition

2026-08-31 streamed-site integrity checkpoint: legacy semantic selection now grows only through bounds-connected cells instead of admitting disconnected showcase buckets through a broad aggregate radius. Every staged site also requires complete usable material coverage before collision validation and publication; incomplete/magenta slices remain hidden and log their unresolved slot count. The editor C# project compiles with zero errors. Visual execution remains pending.

2026-08-31 reliability checkpoint: resident/leader counts now require successful physical creation instead of counting void methods that can return without creating an actor. Eleven targeted source checks cover this batch's streaming ownership, cell completeness, and progress/counter contracts; compilation and runtime execution remain pending.

Canonical population checkpoint: preflight checks stable IDs, explicit site bindings and the existing one-commander-per-camp contract before replacing inhabitants. Final success requires the expected living canonical actor count; saved dead/removed records are excluded, and commander selection skips them. Six source checks passed. Two additional regression cases cover save-state preservation, duplicate IDs, orphan/ambiguous bindings and historical commanders; these are added but NOT compiled or executed.

V2 streaming continuity checkpoint: selected cell IDs now survive stream-out instead of being discarded and reselected by the older tag-only path. Missing prepared V2 selections are explicitly rejected. Foundation cache keys now include the exact selected cells, source manifest, seed and authored height datum rather than broad genre tags. Five source checks passed; an additional cache-identity regression case was added but not run.

Preparation parity checkpoint: generated V2 site preparation now passes the accepted site's typed functions and catalog structure policy into the same composition validator used by preflight. Previously these were omitted, changing acceptance back to tag-only rules. Curated origin landmarks keep their separate path. Four source checks passed; compilation and runtime validation are still pending.

- Identify the exact reviewed asset-contract/function failures preventing V2 materialization.
- Validate entrances, interiors, doors, furnishing, population sockets and distinct cell compositions.
- Keep accepted semantic/save data authoritative; do not turn example-cell reuse into the main generator.
- Acceptance: different semantic briefs produce functional, visibly distinct settlements, with an explicit reason for any rejected cell or fallback.

## 3. Grounding and routes

2026-08-31 physical-coherence checkpoint: lived-road ribbons now sample both lateral edges independently, use terrain normals, and report required/built/rejected segment counts; the world gate no longer treats one surviving mesh object as proof that every planned route was published. Streamed exterior cells now derive their correction from per-renderer terrain contacts with a weighted support threshold, reuse bounded sampling buffers, and remain hidden unless every selected cell resolves a coherent foundation correction. Two editor regressions cover road cross-slopes and incompatible foundation groups. The complete editor C# project builds with zero errors; the existing unrelated unreachable-code warning remains. The regressions and visual/runtime behavior have not yet been executed in Unity.

Collider checkpoint: streamed BoxColliders grossly larger than same-object renderers are tightened to measured visible bounds; explicit invisible/source boundary volumes, traversal clutter, and renderer-less colliders extending beyond the complete site are disabled. Generated-enemy root collision is likewise accepted only when it overlaps and remains proportionate to the visible combat body, otherwise it is replaced by the measured safety capsule.

Collider hierarchy follow-up: renderer-less colliders are now matched to the nearest visible hierarchy branch rather than the whole settlement envelope. Both disproportionate and spatially detached bounds are repaired or disabled, closing the case where an invisible blocker remained technically inside a large site's aggregate bounds.

- Check building footprints, foundations, doorway elevations, road frontage and path connectivity against final terrain.
- Verify local terrain repair preserves traversable edges rather than concealing gaps with decoration.
- Acceptance: curated non-floating assets contact their intended support, with walkable entrances and visible connecting paths.

## 4. Terrain and environmental density

- Inspect the active foliage selection/density constraints before increasing object counts.
- Validate available tree variants and materials at every LOD, grass/understory, rock clustering and settlement exclusions.
- Check terrain silhouettes, water banks, mountain/cave access and route-aware dressing.
- Acceptance: a coherent populated reference slice using approved assets, with measured streaming/performance limits.

## 5. Player movement, animation and combat

Enemy-visibility checkpoint: lack of a locomotion animation no longer implicitly activates burrowing. Burrow presentation is an explicit prefab/generated contract, and its surface cue never disables live combat renderers while the enemy continues moving, attacking, or receiving hits.

Enemy-visibility follow-up: imported LOD ownership, blank levels, transitions and bounds are stabilized after final resident/hostile scaling. A phased low-cost guard restores only the renderers that were originally visible, and only when every such live combat renderer has been disabled unexpectedly; intentionally hidden variants remain untouched.

- Compile and test the recent jump wiring, procedural crouch fallback, headroom gates, and melee visibility/range changes.
- Check stairs/slopes, jump/landing, crouch release under ceilings, dash, first/third-person switching and equipment alignment.
- Review ability target/resource/cooldown contracts without replacing generated content with hardcoded identities.
- Acceptance: responsive controls with one authoritative player; no wall-through melee or duplicate damage events.

## 6. NPCs, interactions and quests

Resident support checkpoint (2026-08-31): fixed a direct overwrite of reviewed-floor placement by terrain-only grounding during imported resident creation. Creation and site reload now share feet-to-pivot correction, including scaled fallback capsules; controller enable state is restored after relocation. The site projection returns the actual contact point instead of an implicit 8 cm hover, and reload clearance ignores the resident's own hierarchy. Separation tests the anchor plus eight bounded, floor-validated offsets and rejects exhaustion instead of accepting an unchecked offset or dropping through to underlying terrain. Seven source checks, 36 contact-offset arithmetic cases and the targeted whitespace check passed. Two editor regression cases were added but have NOT been compiled or run. Actual floor/socket validity, imported character bounds, crowd spacing, and collision timing still require Unity verification; this is not a claim that every missing or misplaced NPC is repaired.

- Verify Vey and generated NPCs are physically placed at valid interior/exterior sockets.
- Test dialogue/door/loot interaction reach and explicit quest event/ID routing.
- Review repetition control while preserving conversation memory and role boundaries.
- Acceptance: required NPCs are reachable, dialogue progresses, and quest counters update once per qualifying action.

## 7. Goddess and presentation

- Evaluate generated introductory prose against the user's welcoming, fragile-world, slightly overwhelmed goddess direction.
- Check thought scrolling, truthful loading substeps, readable quest/player UI and restrained lighting.
- Validate the origin statue's curated surroundings and the loading-to-world camera continuity.
- Acceptance: presentation reflects the saved character/world and hides neither errors nor incomplete geometry.

## 8. Verification and handoff

- Run one relevant compilation pass when available; fix compiler errors before further expansion.
- Run targeted existing regressions and add coverage for repaired failure paths.
- Give the user a short New Game / Continue / retry / traversal / combat checklist.
- Record actual generation timings and worst-frame measurements; do not promise zero frame spikes from source inspection.
- Keep changed, source-checked, compiled and runtime-verified statuses distinct. Push only when authorized.

## Verification debt

The latest hidden Unity run compiles and passes all 57 contract regressions. The earlier standalone build passed with zero errors and one pre-existing unreachable-code warning; its temporary restore cache was subsequently cleared by Unity. These checks include synthetic Unity objects/geometry but are not rendered gameplay, full-world physics, or frame-time tests. The accumulated changes still require a user-run gameplay pass. No claim of playable V2 or screenshot-level visual quality is made by this queue.

### Next verification gate after the 2026-08-31 extended pass

- Run the existing V2 harness, including the newly added road cross-slope and compiled-foundation coherence cases plus the earlier coroutine/population/cache/site-lifecycle/resident-support regressions.
- New Game: confirm substeps follow cell preparation/grounding/activation without switching the phase total back to eight; rejected sites must not log as streamed in.
- Continue: confirm living canonical identities are present and saved dead/removed NPCs remain absent.
- Resident support: check an elevated building floor with differently scaled NPCs, then leave/re-enter streaming range; feet should remain on the floor without pivot-height jumps. Check crowded rooms for an explicit placement rejection rather than overlapped or terrain-buried residents.
- Walk away from and return to a settlement: confirm the same reviewed cells, doors and foundation placement return.
- Disable/cancel a site while it is streaming in a controlled test: confirm it releases the single-loader slot, leaves no partially active content, and does not resume orphaned work.
- Disable/remove/replace a required site while its caller is waiting behind another site: confirm the old request fails promptly, cannot accept the replacement under the same ID, and does not steal the other site's loading slot.
- Recovery: force a handoff timeout and a population failure in a controlled test; verify the corresponding retry preserves the completed world where intended.
- Capture loading duration and frame timings. Source-level scheduling limits cannot establish worst-frame performance or rule out native Unity stalls.
