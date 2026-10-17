# Rejected cold animation-warm experiment

This coordinator summary reconstructs the worker report read in this conversation. The original report and detached compile receipt under the ignored `outputs/WorldAuthoringReview_20261015/` directory are no longer present at publication time. The rejected native-frame JSON is copied and hash-verified separately.

A cold Unity session used the disposable profile and the same accepted settlement as the baseline. Guarded normal-speed assisted legs reached approximately x=298 and x=553. Input/settings were restored. No 200 m/s acceptance claim follows from these legs.

The experiment enumerated the actual staged animators/controllers, queried their dependencies, and attempted one clip load per yielded step. Its first discovery slice cost 1,847.213 ms in a 1,922.297 ms frame. The following dependency-query slice cost 85.986 ms. Initial discovery combined `GetComponentsInChildren<Animator>(true)`, runtime controller access, and asset-path lookup; the exact individual managed call was not isolated. Native loading occurred in that frame.

The worker reported 20 controllers and 52 clip paths, zero load failures, discovery maximum 1,847.231 ms, clip maximum 0.073 ms, and total explicit clip loading 2.016 ms. The expensive loading had already happened during discovery. A later cast also reported an 889.119 ms discovery maximum. The approach was rejected because it moved a larger stall earlier.

Only three native frames were retained before recording stopped. The copied export has no completed site, no activation samples, and `callbackReturned=false`; activation improvement and elapsed time until playable publication are unverified. The 256 MB allocation limit was the inferred stop reason, not directly logged. The raw local file was 64,043,640 bytes with SHA-256 `8505C275664D91B726E3B2DBFC9FC4B481D3BA540917EC89AE1CC9819E3D59F3`.

Only the experiment was rolled back. `YQGeneratedWorldPopulation.cs` was restored to SHA-256 `1C207022EDB511E19866E3E2ACC548A1688B8589985614205E219CDB591528C0`; this hash was independently rechecked at publication. Its `.meta` was unchanged. The worker observed a fresh Unity domain reload/compile with zero errors/warnings after rollback. No new Unity runtime test was performed for this publication. Play Mode, assisted input, and recording had stopped.

Earlier separate measured costs remain: prefab scopes of 208–344 ms and paired-profile publication around 4.65 seconds. Those full original receipts were unavailable when this package was prepared, so these are conversation-derived findings rather than newly supplied raw evidence.

Prepare dependency paths without touching cold runtime controllers before considering another warming approach. Preserve the existing selection, population owner, cancellation, readiness, physics, saved authority, deterministic contracts, and all authored LOD controllers. A proposed repair must be cold-tested across preparation and activation, and must then pass actual 200 m/s travel separately.
