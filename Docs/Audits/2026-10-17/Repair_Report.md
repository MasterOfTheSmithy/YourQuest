# Chat repair report — 2026-10-17

## Status and baseline

Source repairs were implemented in chat with three cooperating agents on `repair/chat-audit-2026-10-17`, based on commit `ee485e38ab6d66610e5ce3293b615b6b1a9dc1b8` from `codex/local-project-guidance-2026-09-30`. The branch is a reviewable repair candidate. Native Unity acceptance is outstanding.

The starting checkpoint's last verified push was **2026-10-17 16:08:10 UTC**; the commit timestamp was 16:07:29 UTC. These are distinct from the later repair branch publication. The handoff is [README.md](README.md); its historical profiler evidence remains unchanged.

## Changed and why

| Finding | Source repair | Acceptance boundary |
| --- | --- | --- |
| A disabled or replaced streamer could bypass player readiness | The authoritative motor retains its streamed-world admission dependency. Admission checks exact manager, world, plan, motor, active owner, and successful bind state. Disable, destruction, cancelled binding, and replacement close admission until a valid bind. Ragdoll entry and recovery use the same gate. | A genuinely fresh authored world without streaming retains its existing movement path. No second motor or streamer is introduced. |
| Held movement could feed false impacts or accumulated airborne integration | A wholly rejected movement step restores the captured integration state, retains accepted jump/dash impulses and real fall history, and skips synthetic impact/deceleration accounting. Real movement followed by a denied assist still processes its real collision. Prediction uses retained planar momentum. | No suppression of real collisions, fall damage, death, or physics is used to pass travel checks. |
| Urgent lanes and emergency ground could extend an already spent frame budget | All reserves now observe the aggregate ceiling. Cold emergency height sampling stays on the existing worker path; emergency publication consumes at most one completed heightfield per frame. Ground synchronization participates in the scheduler and remains an admission requirement. | An indivisible native operation can still overrun its admitted slice. Missing preparation is reported as unavailable ground rather than advertised as playable. |
| First actor activation pulled cold animation dependencies through synchronous native loading | A new explicit Editor builder records ordered clip/controller dependencies from the complete imported prefab closure, including authored LODs. Runtime preparation loads these descriptors before the selected prefab and retains them through population activation. | Generated dependency assets are absent from this source checkpoint and must be built locally. No runtime controller introspection or whole-registry warmup is used. |
| Prefab preparation combined several expensive operations | Resident/hostile preparation yields between native instantiate, authored visual-intent capture, material overrides, cooperative material repair, and text repair. An inactive temporary parent contains unfinished actors. Duplicate material/LOD passes are reduced; final scale-aware LOD stabilization remains. | Final canonical group activation remains atomic. Native instantiate, individual repair phases, final binding, and activation still require measurement. All authored NPCs and LOD animators are retained. |
| Population resumptions repeatedly serialized the entire accepted artifact | Resume validation compares the exact one-location payload, ordered NPC identities/bodies, life state, seed, provider context, and request epoch. Full accepted-artifact/replay validation remains at admission and immediately before activation. | Unchanged hash fields do not authorize changed location bodies. Accepted records and selection seeds are not rewritten. |
| Human fallback could load unrelated monster families before classification | Existing conservative semantic rules exclude proven nonhuman metadata before prefab loading. Loaded candidates are reused across requested/generic/opposite fallback searches. | Ambiguous paths retain full existing prefab inspection and ordering. No species rules or deterministic seeds change. |
| Fallback vegetation bypassed canonical approval and exact binding membership | Exact granular manifest membership replaces suffix-only membership. All eight historical fallback proposals must pass canonical spatial approval and exact binding. Approved same-pack/style low-flora alternatives can follow the accepted palette prefix. | All eight historical proposals currently fail published spatial approval. Required layers remain unready if no approved alternative exists; the patch does not fabricate approvals. Fallback decoration membership necessarily changes where the former bypass was used. |
| Ecology could resolve selected prefabs synchronously after eviction | Ecology uses selected-path asynchronous preparation and residency leases. Registry requests coalesce, completed requests survive consumer cancellation, leases protect active dependencies, and epochs prevent old iterators/callbacks from repopulating a reset cache. | The legacy synchronous API is preserved. Optional missing/malformed/stale dependency metadata warns that compatibility loading is unqualified for cold activation. |
| Adjacent profile preparation/publication repeated expensive proofs | An internal immediate prepare/publish entry point reuses only the final preparation proof when consumed in the same call. The public retained-token path preserves its revalidation. Runtime context is guarded around auxiliary capture and before final exact proof; immutable staging, durable pointer order, in-place apply, and recovery remain owned by the existing paired save system. | The successful immediate fixture uses four previous-revision disk proofs, two auxiliary captures, and one pending-writer join, compared with six/three/two for the retained path. Synchronous staging, final exact proofs, and projection I/O remain; this is not a complete elimination of the historical save stall. |

## Verification

The final counts and exact changed-source hashes are recorded in [Repair_Verification.json](Repair_Verification.json). Reproduction commands and fixture boundaries are documented in [Tools/ChatAuditVerification/README.md](../../../Tools/ChatAuditVerification/README.md).

| Final check | Result |
| --- | --- |
| Changed project C# syntax, player and Editor configurations | 17 files; 0 errors |
| Paired publication regressions | 37 passed; 0 failed |
| Population regressions | 28 passed; 0 failed |
| Movement/scheduling regressions | 34 passed; 0 failed |
| Runtime registry and approval contracts | 7 groups passed; 0 failed; all 8,174 manifest paths checked |
| Source identity during the final combined run | Unchanged |
| Existing `.meta` files / three new GUIDs | Existing files unchanged; new GUIDs unique |
| `git diff --check` | Passed |

Executed verification includes C# 9 syntax parsing of the changed production and Editor sources, focused compiled managed publication/population/movement fixtures, complete runtime-registry contracts against controlled Resources APIs, exact manifest membership checks, and patch whitespace/reference review. Publication fixtures use the real commit store and temporary immutable revisions, including stale context, corrupt prior files, failed pointer publication, and post-pointer recovery. Cross-review covered population lifetime/cancellation, asset residency, scheduling, and publication ordering.

These results are **not a full Unity compilation, Editor test execution, native activation measurement, or 200 m/s travel PASS**. The chat environment lacks Unity and the omitted imported libraries. The portable fixtures explicitly replace those boundaries; they cannot establish native loading behavior, Unity lifecycle callback timing, or real rendering/physics performance. The new Edit-mode movement fixture is supplied for local execution and has not been run here.

## Local completion steps

1. Bring the repair branch into the complete local checkout using the normal Git workflow, preserving any newer local work.
2. Open stable Edit mode and run **YourQuest > Assets > Build Prefab Dependency Preloads**, followed by **Validate Prefab Dependency Preloads**. Existing individual runtime leaves must be present and current. The builder stops before publication when imported sources/leaves are missing, preserves existing GUIDs, and emits new owned descriptors plus a string manifest. Commit those generated assets through the project's ordinary asset workflow.
3. Resolve any reported required foliage layer with reviewed, bound assets. The approval gate intentionally reports missing coverage.
4. Compile in Unity and run **YourQuest > Verification > Run Movement Admission Regression**. Check domain reload, disabled/re-enabled streamer, manager/world/plan replacement, ragdoll/recovery, denied motion, and deferred collider synchronization through the production owners.
5. In `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`, measure a genuinely cold load through final population activation and playable publication. Include the entire preparation interval so a moved stall cannot count as improvement. Exercise cancellation, profile switching, save/reload, and revisit after eviction.
6. Measure actual sustained 200 m/s travel with normal physics and damage. Current and approaching cells must remain playable, distant shells must satisfy the visual contract, and stalls must meet the approximately 90 ms ceiling. Preserve failure reasons and native captures for any remaining long operation.

The new pre-build guard checks granular dependency metadata without rewriting assets. A player build with absent, stale, incomplete, or incorrectly ordered dependencies now stops with the explicit build-menu instruction. This is a required one-time preparation step for this source repair, followed by rebuilds when bindings/imported dependency contents change.

## Preserved contracts and remaining work

The existing authoritative player, world builder, streamer, paired save owner, canonical accepted content, IDs, seed derivation, V1/V2 routing, serialized APIs, existing asset GUIDs, and readiness validators remain. State schema stays **8** and profile manifest schema stays **2**. No imported source assets, scenes, prefabs, project settings, package settings, personal profiles, or historical audit receipts were changed.

The source changes address the audited admission, preparation, validation, approval, residency, and scheduling defects. The dependency builder still needs the full local asset project; some palettes may need reviewed foliage bindings. Native performance and the complete travel outcome remain unqualified. In particular, indivisible Unity loading/instantiation/activation, required readiness holds, and synchronous paired-save I/O can still violate the target. The supplied mesh-blending archive remains review material and was not installed as an unqualified replacement pipeline.
