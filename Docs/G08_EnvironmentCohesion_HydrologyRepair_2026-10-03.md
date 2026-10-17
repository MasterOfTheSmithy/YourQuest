# G08 accepted-river internal-bend clipping

Status: **INCOMPLETE** for EC05. The bounded numeric repair and matched production depth recheck passed for the reproduced spur defect. Legacy user-accepted PASS decisions remain accepted. Final affected publication/streaming/environment regressions and full hydrology/visual coverage remain required.

## Reproduction and cause

The ordinary PlaySafe/title/Continue session on profile `b138c228684b48739bad60f17f6ba5eb`, accepted world `yq-world-300bc1cdba75926d8224ffb9`, seed `610adf18`, artifact `e37706d3ad7f55b6` produced [this water census](../outputs/G08_R3_Physical_20261003/20261004_003949_889_hydrology_census/Hydrology.json). It found 58 named surfaces, 49 active, 910 wet center samples, 25 below-ground center samples and two unavailable terrain samples. All four origin-level compiled water objects were inactive; active cell ribbons existed. This does not prove absent water globally or validate all water kinds.

The 25 below-ground centers belong to `ContinuousRiver_4_1_Span0` (13; minimum depth -13.5001755 m) and `ContinuousRiver_5_6_Span1` (12; minimum depth -11.5280914 m). Their material shader is supported `YourQuest/Generated Water`. Visibility/occlusion, flow and semantic parent identity are separate questions.

The existing `YQContinuousWorldFeatureMaterializer.AppendAcceptedSegmentPoints` can extend a segment whose endpoint lies within padded reach of a cell across that entire cell. The accepted-water caller enabled this generic option for every source segment, including internal bends. The retained five-point river has bends at z=250 and z=-250, six metres from cell borders at z=256 and z=-256. Extrapolating those middle segments produces the observed spurs across the next 128-metre cell; the accepted plan instead turns onto its next segment. Published terrain follows the accepted course, so the invented ribbons run below ground.

## Smallest causal repair and preserved contracts

The accepted-water caller now explicitly disables endpoint extrapolation for individual accepted segments. Real padded overlap and exact boundary samples remain. The separate `GetAcceptedWaterContinuations` / `AppendTerminalContinuationPoints` owner still emits accepted terminal continuation; area-water/sink filtering and terminal contracts were not changed.

No accepted point, terrain, topology, water level, asset binding, seed, schema, palette, world scale, save or streamer allowance was changed. The repair removes geometry invented beyond an internal accepted segment; it does not regenerate the accepted world or replace water rendering. Road callers retain their existing behavior and need separate investigation if a similar defect is observed.

## Fresh numeric evidence

[Six checks PASS](../outputs/G08_EnvironmentCohesion_20261003/20261004_005637_628_hydrology_segments/Receipt.json), executing runtime MVID `757967fb-d790-486a-b75f-9f2d08c7eaaf`, Editor MVID `36fe9e4d-4706-43ce-bc74-5524f7588a4c`, Unity `6000.3.2f1`. The receipt records assembly and source hashes. It invokes the existing internal numeric helper and creates no Unity objects, assets, profiles or alternative sampler.

Both permissive northern/southern cases reproduce the old spurs. Both bounded cases retain padded overlap without extending beyond the accepted endpoint. The genuine intersecting segment retains exact cell borders and its accepted elevation gradient. This is synthetic numeric evidence, not production wetness, whole-world hydrology, collision, terminal-continuation or visual certification.

Run entrypoint: `YQEnvironmentHydrologyVerification.Run()`; existing Editor bootstrap dispatches the newly implemented explicit Edit-only `Temp/YQ_HYDROLOGY_SEGMENT_CONTRACT.request`. The matched production census below supplements this numeric result; affected final regressions remain required.

## Matched production depth recheck

[After census](../outputs/G08_R3_Physical_20261003/20261004_010205_662_hydrology_census/Hydrology.json), [executing identity and ordinary camera](../outputs/G08_R3_Physical_20261003/20261004_010205_662_hydrology_census/Discovery.json). The same ordinary PlaySafe/title/Continue profile, world, seed, artifact, starting position, quality, pipeline and camera ran the repaired runtime MVID `757967fb-d790-486a-b75f-9f2d08c7eaaf` / Editor MVID `36fe9e4d-4706-43ce-bc74-5524f7588a4c`.

The named surface count changed from 58 to 56 and active surfaces from 49 to 47 as the two invented spurs disappeared. Active ribbon centers now contain 929 wet, **zero below-ground/dry**, and two unavailable samples, versus 910/25/2 before. Cell (4,1) has one accepted span with 35 wet samples and 4.39382–4.49998474 m depth. Cell (5,6) has one accepted span with 35 wet samples and 4.38509369–4.49776459 m depth. The canonical artifact was not edited; the genuine intersecting paths remain. This is current production geometry/depth evidence for this defect.

The two unavailable samples still lack a published terrain owner at their exact sample position. Inactive compiled origin objects, non-ribbon water depth, natural terminal/sink behavior, topology/confluences, both-camera bank traversal, reload/frontier seams, visual appearance and final R1/R2 are not certified by this census. No global hydrology or parity claim follows from zero dry samples in this bounded active set.

## Other runtime findings and preservation

[The adjacent short crossing diagnostic](../outputs/G08_EnvironmentCohesion_20261003/20261004_runtime_water_bridge_review.json) reached five endpoints over 69.03 m with 901 frames. Its zero legacy `unsafeFrames` means no streamer traversability failure, not no capsule warning: 12 MissingSupport and 14 UnsupportedFootprint frames remained, including five ungrounded warnings. Clearance stays NOT VERIFIED.

[The repaired-source movement leg](../outputs/G08_R3_Physical_20261003/20261004_010144_944_session/20261004_010301_191_first_bridge_after_bounded_water/Result.json) exercised the new separate warning counters: 948 frames, 69.1089 m and all five endpoints, zero streamer traversability failures, 26 probe-warning frames and 21 grounded warning frames. Raw frames match the counters: 11 MissingSupport, 14 UnsupportedFootprint and one combined SteepSupport/UnsupportedFootprint. `capsuleClearanceReviewRequired=true` is intentional. The maximum observed interval was 123.1223 ms in an unfocused diagnostic, not a qualifying performance result. The water repair does not clear the distinct bridge defect. Prior raw results are not rewritten.

The ordinary camera was 679×410, FOV 84, near/far 0.05/1000, pipeline `Assets/Settings/PC_RPAsset.asset`, quality index 0. Profile display name is `New Adventurer`; disposable-fixture status is not established. This protected diagnostic is not a qualifying R2/performance or full acceptance witness. All frames were unfocused. The target configuration still awaits the user's decision.

Restoration retained the persistence barrier through Play exit. All 12 recorded save fingerprints matched afterward. The first external comparison's two apparent differences were only `<absent>` versus `MISSING` sentinel spelling; its raw output is retained beside the corrected check. No saved bytes differed.

The repaired-source session also [restored all 12 fingerprints through Play exit](../outputs/G08_R3_Physical_20261003/20261004_010144_944_session/PersistedAfterPlayExit.json). Both sessions are terminal; the Editor is outside Play Mode. No accepted artifact, normal save or asset GUID was changed.
