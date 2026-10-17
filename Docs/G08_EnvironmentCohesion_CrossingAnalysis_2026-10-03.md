# G08 retained crossing-support analysis

Status: **diagnostic analysis; EC06 NOT VERIFIED**. This is analysis of a retained older-build movement trace, not a new traversal or a final-build certificate. Prior user-accepted legacy PASS decisions remain accepted/verified.

Source: `outputs/G08_R3_Physical_20261003/20261003_221105_502_session/20261003_221831_378_east_road_three_crossings/Frames.tsv`, SHA-256 `e4b51df396ec449e5ea60bf4e67f433e5b33b617158c066b7f17ac0c501d97b9`. [Machine-readable analysis](../outputs/G08_EnvironmentCohesion_20261003/retained_bridge_support_analysis.json) retains source identity, exact ranges, issue counts and support hierarchy paths.

The trace reached ten endpoints over 197.496 m. Of its 2,636 observed frames, 62 had probe flags: 15 MissingSupport, 44 UnsupportedFootprint, two SteepSupport + UnsupportedFootprint and one Occupied + UnsupportedFootprint. Fifty-four flagged frames also recorded the motor as grounded. A grounded motor and a missing center/footprint ray measure different things; that disagreement is not resolved by treating either as a universal PASS.

## Narrow reproduction targets

Adjacent flagged samples were grouped by X, splitting when the gap exceeded 10 m. These are diagnostic coordinate bands, not accepted feature boundaries.

| X range (m) | Z range (m) | Flagged frames | Actual reported support |
| --- | --- | ---: | --- |
| 12.816–25.527 | −37.334 to −37.133 | 30 | Stone bridge walk surface, origin terrain, or no support; includes 13 MissingSupport frames. |
| 86.642–87.515 | −35.573 to −35.548 | 9 | Wood bridge deck, its named stone walk surface, or terrain; one steep-support combination. |
| 99.075–99.468 | −35.218 to −35.206 | 6 | Wood deck or no support; two MissingSupport frames. |
| 130.584–134.561 | −34.318 to −34.205 | 17 | Stone walk surface or terrain; one occupied-support combination. |

The existing `YQRouteTraversalProbe` captures controller scale/radius/height/skin/step/slope and feet offset. Its center ray searches near expected feet, eight rim rays look for reachable treads, and four footprint rays check support and slope. MissingSupport returns before rim acquisition. This source behavior identifies a possible step-edge measurement sensitivity; it does not prove the flags are false or authorize changing thresholds.

Next shortest production reproduction: preserve the accepted profile/world/artifact, capture the actual capsule/camera, follow the first four original approach points and continue to `(35, approximately −37)` through the first flagged range. Inspect physical contact and actual bridge/terrain support, then return over the same approach. Keep live publication readiness, controller grounding, probe geometry and visible motion separate. Do not change the motor, probe tolerance or bridge geometry before reproducing the cause.

## Prepared diagnostic boundary

The existing itinerary tool now has a protected idle `hydrology` action that writes a read-only water census next to discovery and ordinary-pose images. It records renderer activity/layers/materials, bounds/frustum candidates, actual river-center depths against published streamer terrain owners and camera projection candidates. It does not stage movement, change rendering settings, invent fallback ground or infer a hydraulic identity from a mesh name. Non-ribbon depth and occlusion remain unverified.

Discovery additionally records actual capsule dimensions, ordinary camera settings, active profile name and host/quality/pipeline metadata. These fields executed in the fresh protected session below; they do not establish clearance or full acceptance.

## Fresh first-band reproduction

[Result](../outputs/G08_R3_Physical_20261003/20261004_003930_778_session/20261004_004138_160_first_bridge_support_reproduction/Result.json), [raw frames](../outputs/G08_R3_Physical_20261003/20261004_003930_778_session/20261004_004138_160_first_bridge_support_reproduction/Frames.tsv), [review with exact hashes](../outputs/G08_EnvironmentCohesion_20261003/20261004_runtime_water_bridge_review.json). Five endpoints were reached over 69.03 m in 901 observed frames, using walk speed 1.8 and the original approach followed by `(35,-37.043)`. No diagnostic staging contributed to travel.

The first bridge warning reproduced: 12 MissingSupport and 14 UnsupportedFootprint frames. Twenty-one warning frames were grounded and five ungrounded. The legacy result's `unsafeFrames=0` counts failed **streamer traversability**, not capsule probe warnings. It must not be read as zero support warnings or safe-clearance certification. The itinerary tool now includes separate warning counters and this definition. The later [repaired-water-source replay](../outputs/G08_R3_Physical_20261003/20261004_010144_944_session/20261004_010301_191_first_bridge_after_bounded_water/Result.json) exercised those counters and matched its raw frames: 26 warning frames, 21 grounded, zero streamer failures, explicit clearance-review requirement. It reached all five endpoints over 69.1089 m. Raw earlier receipts remain unchanged.

The actual capsule was radius 0.38 m, height 1.8 m, skin 0.08 m, step 0.52 m and slope limit 58 degrees, with unit scale and measured feet offset approximately zero. Camera FOV was 84 at 679×410. All frames were unfocused, and the observed maximum interval was 143.5566 ms; this is not R2/performance certification. The source/runtime identities are in adjacent discovery and review. No bridge, motor, probe tolerance or actual quality setting was changed for this witness.

The reverse first-band traversal and three further coordinate bands remain unverified. Protected restoration retained the save barrier through exit; all 12 fingerprints matched. Profile display name `New Adventurer` does not establish disposable-fixture provenance. EC06 remains NOT VERIFIED pending support/contact cause, both-direction clearance and full crossing coverage.

## Later causal repairs and retained limits

The subsequent [bridge repair report](G08_EnvironmentCohesion_BridgeRepair_2026-10-03.md) supersedes the prepared next reproduction step above. Four protected matched-world sessions and eight raw-count-checked legs identified and repaired three owning collision/grade causes. Twenty-one focused contracts pass. Final forward approach has zero probe warnings; reverse still has twenty, comprising one first-band stone-bank contact and nineteen origin-approach warnings. Both connected directions reached endpoints; clearance is still NOT VERIFIED. Remaining coordinate bands, both cameras, reconstruction and visual/performance gates stay open. [Machine-readable before/intermediate/final review](../outputs/G08_EnvironmentCohesion_20261003/20261004_bridge_repair_review.json) retains every result/hash and all four twelve-fingerprint disk proofs.
