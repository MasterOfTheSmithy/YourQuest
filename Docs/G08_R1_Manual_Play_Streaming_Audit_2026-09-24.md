# G08-R1 manual play and streaming audit

Date: 2026-09-24. Production checkout: `C:\Users\Garri\YourQuest`.

## Current conclusion

G08-R1's requested recovery/publication matrix **passed at 09:03:51 UTC** on current compiled code. Manual Continue reaches the real gameplay presentation boundary. The archived receipt is `G08_R1_Runtime_2026-09-24_090351_PASS.md`. Remaining lag, river/road geometry quality, and the sparse ordinary destination plan are unresolved; this recovery PASS does not certify those issues as fixed.

## Fresh evidence

- Normal selected profile `80b8f3aff47d4b159cc1a0074e3eced7`, world seed `06c4f7d8`, accepted V2 content hash `9261a657a4e87c9e`, generation `spatial_world_plan_v2_topology_8`.
- Continue released at 06:39:35 UTC with loading hidden, HUD visible, input enabled, gameplay camera active, one active audio listener, materialized builder, and no builder failure.
- A second normal Continue after the hash/width repair again had all those conditions at 07:13:41 UTC. Neither run selected/reset/saved a beta fixture through the editor controls.
- This does not certify a new questionnaire journey on every seed. The user subsequently confirmed movement and improved responsiveness but reported remaining lag and missing world content.
- Edit Mode hash checks passed against this saved artifact, including null collections, no source mutation, changed-content rejection, and restoration of the previous valid projection. See `G08_R1_Hash_Integrity_2026-09-24.md`.
- The replaced operation measured 2.601 ms/call for JSON-copy-plus-hash and 0.621 ms/call for the readonly hash in the latest 16-iteration sample. Earlier sample: 3.055 versus 1.149 ms. These are isolated operation costs, not game FPS. This Unity runtime's allocation counter returned unusable zero values.
- Streamed mesh checks, approved path texture preservation, and water shader input checks are recorded in `G08_R1_Ribbon_Integrity_2026-09-24.md`. The preview image is isolated material/shader evidence, not production gameplay acceptance.

## Root causes repaired

### Startup appeared released before it was playable

The title environment could disable its camera/listener before an authoritative gameplay camera existed. The builder's initial-generation release was also earlier than the bootstrap's final visible terrain/ecology release. The intermediate state could show no useful UI while input remained blocked. A final `WaitForEndOfFrame` also depended on the Game view being rendered.

The camera handoff now waits for the gameplay camera, including a listener when reusing a camera. The ordinary loading UI persists through final publication. HUD/modal/loading release occurs at one real bootstrap boundary after consecutive ready frames. Status evidence reports actual HUD, input, camera, and audio state.

### Retry checks did not exercise the production exception path

The previous exhaustion probe consumed its injected retry budget while actual appearance exceptions/detail failures could keep retrying without the same accounting. The revised painter catches nested iterator failures, counts a failed live attempt once, enforces the existing bounded retry budget, and revokes stale work before cancellation. The revised runtime probe throws through that production path and checks the explicit final failure and absence of work/timers. This source compiled; its complete revised runtime matrix is still pending.

### Blueprint cache access repeatedly copied the full accepted graph

Every cached preparation lookup serialized and deserialized the entire blueprint before recomputing its hash. The new readonly hash computes the identical canonical value without the JSON copy or mutation. Actual content is still hashed and checked on lookup; accepted claims are not blindly trusted.

### Streamed surfaces failed their presentation contracts

- Several accepted roads/rivers were flattened into one point buffer with a single maximum width. A narrow feature inherited a wider neighbor's width. Widths now remain attached to each feature; shared endpoints and break boundaries remain distinct.
- The root runtime asset registry has `entries: []` and lazy pack shards enabled. The streamed material search enumerated only the root, so accepted path materials could never be found there. Exact palette paths now resolve their pack shard and use the existing texture-preserving URP adapter.
- A single texture repeat was stretched over each whole road ribbon. Mesh UVs now use stable world-space repeats.
- The water shader requires vertex green to encode cross-bank position. Streamed river and area meshes supplied no vertex colors, allowing shoreline fading to erase their surfaces. Rivers now supply 0-to-1 bank coordinates; lake interiors supply 0.5, with full coverage. Water materials share the origin world's approved source policy.

These changes preserve accepted world coordinates, hydrology/route authority, seed derivation, saved content hashes, save schema, and V1/V2 routing. They do not redesign the long straight terminal continuation geometry.

## Why ordinary travel has few destinations

The selected saved blueprint contains 12 sites: an origin, a waterfall/cave natural feature, four continuation anchors, two settlements, two hostile sites, and two POIs. Continuation anchors are not settlements or playable POIs.

| Accepted destination | Distance from origin |
| --- | ---: |
| Fenweald Market | 4,903 m |
| Jorvale Haven | 7,031 m |
| Hostile sites | 4,900 m and 7,002 m |
| Nordic hut POIs | 6,889 m and 6,933 m |
| Primary lake | approximately 7,045 m |

The user's last observed travel reached approximately 1.9 km north. The absence of a settlement there matches the accepted plan. This does not establish that distant settlement materialization works correctly; it explains why this trip encountered no planned destination.

`YQSpatialSiteNetworkCompilerV2` adds near/far travel sites and continuation waypoint fans only for the development fixture. Ordinary topology-8 journeys omit those fixtures. `YQSemanticWorldAuthority.GetSitesForCell` creates lazy synthetic sites only for V1 compatibility; ordinary V2 streaming uses the finite accepted site graph. More travel does not currently cause new canonical V2 destinations to be generated.

This is a production planning/expansion gap, not something the decoration streamer can correctly repair by scattering arbitrary buildings. Fixing destination pacing requires accepted structured content and a persisted expansion/placement contract. Existing saves have not been moved, invalidated, or filled with beta fixtures. The original G08-R1 contract explicitly excludes content expansion.

## Why the previous evidence was insufficient

1. Fixture-only destinations made repeated beta traversal unrepresentative of ordinary journeys.
2. Startup checks emphasized builder state while the player-visible HUD/input/audio handoff could still fail.
3. Retry injection tested special accounting rather than the actual failing iterator path.
4. Terrain/content object counts did not prove that a water shader displayed any water or that a road used an imported texture.
5. Small streamer `Update` timings excluded independent coroutine work and could not establish the total frame budget.
6. The longest verifier runs spent time on travel/return routing without a complete final PASS. Source changes and partial gates were progress, but not goal completion.

## Lag and delayed publication still requiring evidence

The second normal run still recorded a maximum content slice of **1.950 seconds** after removing the JSON copy. The preceding manual session recorded **2.359 seconds**. These are different runs and workloads, not a controlled whole-game benchmark. The current coarse label is `YQContinuousWorldFeatureMaterializer.BuildCellRoutine`; it does not establish the exclusive cost of a particular suboperation.

Multiple content, appearance, and ecology workers have independent budgets. Their combined frame cost and the effect of native uploads/rendering require CPU/GPU evidence. An active ecology work ID alone does not establish prompt completion; previous runs recorded very long ecology waits. A new bounded Unity CPU capture is prepared to measure ordinary released gameplay.

Read-only process evidence during the earlier normal run showed approximately 16.9 GB Unity working set, 29.3 GB private memory, and 4.5 GB free physical memory on a 31.9 GB machine. That suggests pressure but does not prove paging caused the lag.

## Goal acceptance status

### Follow-up repairs and evidence at 08:54 UTC

- The 07:35:23 runtime run failed initial coverage: cell `(5, 2)` exhausted ecology retries with canopy `5/5`, understory `9/9`, and shrubs `0/6`. Its shrub search sampled only four cluster centers, then repeated those rejected locations on every retry. An empty required low-vegetation layer now gets a bounded additional deterministic search, retaining all habitat/clearance constraints and explicit failure on exhaustion. This is not yet a fresh full-matrix PASS.
- The 08:49 live water capture showed `normal=none` despite positive flow speed. The approved `M_Water` uses `_RefractionNormal`, which both material conversion paths missed. Both now retain that imported normal (`T_Water_01a_N`). The 08:53:39 focused mesh/material checks passed; live appearance after this repair remains pending below.
- Wilderness clearance repeatedly called the global settlement/camp anchor resolver, rehashing the entire accepted graph for every candidate. These XZ-only checks now reuse the same validated immutable projection already held by the scatter transaction. Missing/mismatched sites retain the existing resolver path. Twenty-eight exact boundary comparisons passed against the selected save: existing lookup mean 7.225 ms, reused projection 0.010 ms, including reflection. This microbenchmark does not measure gameplay FPS or change accepted positions, density, assets, radii, or hashes.
- The completed pre-reuse runtime CPU window at 08:49:49 recorded 341 gameplay frames over 15.007 seconds (22.72 FPS), focused throughout, timeScale 1. Mean main-thread time was 43.667 ms and coroutine time 28.418 ms; maximum coroutine sample 243.055 ms. This is an Editor observation, not a GPU profile. See `G08_R1_Streaming_CPU_2026-09-24_BeforePlacementReuse.md`.
- The 07:51 capture was interrupted by leaving Play Mode. Its negative frame count is invalid and its samples are partial evidence only. The recorder now labels such exits INTERRUPTED and omits reset frame-count differences. No successful performance acceptance is claimed for that capture.
- Runtime and Editor assemblies compiled after these source changes. The temporary editor-test access error was corrected by using the existing reflection boundary for internal runtime helpers; the subsequent focused checks passed.

### Live follow-up at 08:59 UTC

- Normal Continue again released HUD/input with one active listener. The new 15.035-second window immediately after release measured 275 gameplay frames (18.29 FPS), mean main-thread 53.901 ms, coroutine 34.277 ms. The earlier window started later in background generation, so these are not matched workloads. No overall frame-rate improvement is established by the local clearance speedup. `G08_R1_Streaming_CPU_2026-09-24_AfterPlacementReuse.md` preserves the sample.
- Actual water now binds `T_Water_01a_N`; two live captures separated by 1.072 seconds of shader time show changing ripples. However, the nearest origin-area streamed channel has 16 wet and 18 dry center samples, minimum water-to-terrain clearance -7.394 m. Terrain/road construction still covers parts of the accepted ribbon. This fails overall visual acceptance despite the repaired material. The material fix alone is not described as restored high-quality rivers.
- The fresh running recovery matrix passes cancellation/replacement, actual iterator-exception retry exhaustion (four retries, explicit terminal failure, no painter/retry timer), lag injection with observed movement, missing-ground handling, and both appearance/ecology completion orders. Natural unload/revisit and the final matrix result are still pending. Current partial evidence is persisted in `Logs/YQSemanticChunkRuntimeVerification.md`.

The retained 04:58:01 UTC report (`G08_R1_Runtime_2026-09-24_045801_FAIL.md`) passed its then-current cancellation/replacement, retry injection, lag injection, missing-ground, and completion-order checks. It failed natural unload/revisit because the outbound route did not cross the retention boundary. It predates the revised real-exception retry path and subsequent repairs; its individual PASS results are historical evidence only.

### Final G08-R1 result, 09:03:51 UTC

The current-code production run passed cancellation/replacement, real-exception retry exhaustion, lag injection, missing-ground handling, both completion orders, and natural unload/revisit. Cell `(4, 8)` progressed from publication version 11 to unloaded version 12 and rebuilt version 22. Outbound travel took 131.582 seconds and return travel 113.240 seconds, with observed input/movement and valid production motor traversal. The final demanded-publication work contract passed for 102 chunks. Semantic serialization round-trip passed. Unity returned to Edit Mode without source edits during the run.

The report explicitly leaves high-speed G07/frame-budget gates and G02 persistence-budget acceptance unverified. Its 2.541-second maximum content slice remains a performance concern. The PASS covers G08-R1 recovery and truthful measurement, not overall visual or performance quality.

Remaining follow-up: repair the measured terrain/river overlap and visible route geometry, verify complete river appearance, reduce remaining measured frame cost without lowering fidelity, and verify a fresh new-questionnaire journey. Ordinary destination expansion remains outside the original G08-R1 scope.
