# G08 finalized terrain replay repair

**G08: INCOMPLETE.** This repairs EC11-D02's repeated road-grading cause. It does not certify complete reconstruction, mutations, traversal, world coverage, visuals or performance. Accepted legacy forced PASS decisions remain accepted; Astra remains stopped.

## Reproduction and cause

Two ordinary Continue sessions on identical source and assemblies reproduced drift in the same disposable accepted world: [revision 86](../outputs/G08_R3_Physical_20261003/20261004_043254_948_session/Discovery.json) and [revision 87](../outputs/G08_R3_Physical_20261003/20261004_045151_665_session/Discovery.json). [The checked comparison](../outputs/G08_EnvironmentCohesion_20261003/20261004_same_source_reload_review.json) decoded and checksummed both 513×513 committed heightfields. **11,792 of 263,169 height samples changed, maximum 1.239033 m. Eighteen of forty uniquely matched bridge collision meshes changed height, maximum 0.181726 m.** Profile, world, seed, accepted artifact, terrain dimensions/version/fingerprint, quality and assembly/source identity matched.

`YQGeneratedWorldRuntimeBuilder` invoked `RepairLivedPathTerrainRoutine` again after restoring a finalized profile terrain snapshot. That routine solves road grades from its current input heightfield, blends shoulders and constrains adjacent slopes. Its previous output therefore became a new grading input at every Continue, even though the accepted world had not changed.

## Coherent patch and preserved contracts

Remove only that restored-terrain invocation. First-generation construction, bank/channel work and both final road passes remain in `PrepareDeterministicConstructionTerrainRoutine`. Surface painting, accepted river reconciliation, crossing construction, validation/readiness and checksum capture remain unchanged. No world regeneration, save rollback, schema/manifest change, asset change or new owner occurred. The existing finalized-terrain replay contract now applies to road grades as it already did to construction pads.

Fresh Unity 6000.3.2f1 compilation succeeded. Current runtime MVID is **a63612f1-1e30-4f0b-a920-3fa65a114fbd**, SHA-256 **29f7b648e04eca6bd08020b5cc7473ff809b47665980b24d4e4a5a9ecef10c42**. Editor MVID remains **58cd43bd-193d-431e-9734-4bb2d3c8fb6e**, SHA-256 **b3fa8df2a2ee5e9e2c7b8018ce4fd36c914a53e45ca37a7726f0c720add4b303**. Builder SHA-256 is **cc9ff07e21152283ff2e544bdb4be2351f5f3a7744bee9dff5c2a49d623e1494**. Historical receipts retain their original build identities.

The refreshed [bridge fixture](../outputs/G08_EnvironmentCohesion_20261003/20261004_050756_768_bridge_ownership/Receipt.json) passes **119 checks**, retaining its two expected negative-case errors. It remains Edit Mode evidence; it does not establish production bank access.

## Ordinary production replay

Profile **3ccfff4df9a1479884061e51940e4457**, world **yq-world-0284d4409a720743c13eb56e**, seed **4bb221dc**, accepted V2 artifact **c1d6ce9d3e06f760**. Ordinary title/Continue retained revision **87**, commit **3743f86b8ee74118b27d09b259e3262e**.

[The first patched session](../outputs/G08_R3_Physical_20261003/20261004_050646_043_session/Discovery.json), compared with the immediately preceding saved terrain, has **zero changed height samples and zero changed bridge meshes**, with all forty footprints and indices matched. [The comparison](../outputs/G08_EnvironmentCohesion_20261003/20261004_road_regrading_removed_review.json) explicitly records the changed builder/runtime assembly; it is a causal patch comparison, not a same-build repeatability claim. The channel reconciliation still reported changed intermediate samples, but final committed terrain and captured collision geometry matched. No additional water modification was justified by this result.

The first patched session restored successfully and exited. [Twelve protected files](../outputs/G08_R3_Physical_20261003/20261004_050646_043_session/PersistedAfterPlayExit.json) and [nine original-profile paths](../outputs/G08_R3_Physical_20261003/20261004_050646_043_session/OriginalProfilePreservationAudit.json) match their exact baselines.

## Retained restoration limitation

The prepatch revision-87 session could not restore its in-memory snapshot while successive `InvestorDirector:small_world_lore` requests kept the model busy. Bounded restoration waits ended; the existing persistence barrier remained retained. Unity's normal Play teardown cancelled the outstanding owned request, and the session exited. [Restored.txt](../outputs/G08_R3_Physical_20261003/20261004_045151_665_session/Restored.txt) remains **restored=False**. Do not describe that session as a successful in-memory restoration. Its [twelve protected files](../outputs/G08_R3_Physical_20261003/20261004_045151_665_session/PersistedAfterPlayExit.json) and [nine original-profile paths](../outputs/G08_R3_Physical_20261003/20261004_045151_665_session/OriginalProfilePreservationAudit.json) nevertheless remain unchanged after exit. No model service was killed or restarted.

## Same-build repeat and preservation

[The repeated ordinary Continue](../outputs/G08_R3_Physical_20261003/20261004_051005_135_session/Discovery.json) uses identical source and executing assemblies. [Its comparison](../outputs/G08_EnvironmentCohesion_20261003/20261004_same_build_terrain_replay_repair_review.json) verifies every committed payload against the live discovery fingerprint and raw-height checksum: **263,169 samples unchanged; forty uniquely matched collision meshes unchanged; indices unchanged**. Revision 87 and its existing commit remain authoritative. This is a bounded accepted-world height/bridge reconstruction PASS, not full EC11 acceptance.

Restoration succeeded with the persistence barrier retained and Play exited. [All twelve protected files](../outputs/G08_R3_Physical_20261003/20261004_051005_135_session/PersistedAfterPlayExit.json) and [all nine original-profile paths](../outputs/G08_R3_Physical_20261003/20261004_051005_135_session/OriginalProfilePreservationAudit.json) match their exact baselines. The Editor is outside Play and not compiling. [Compilation diagnostics](../outputs/G08_EnvironmentCohesion_20261003/20261004_terrain_replay_repair/CompilationDiagnostics.txt) retain the compiler success lines; executing build identity is independently recorded in both runtime discoveries and the refreshed fixture.

## Remaining

Existing EC06-D06 bank ingress fails with the tread approximately 2.36 m above the approaching player; removing cumulative reconstruction does not itself prove that crossing repaired. Measure the authoritative channel/bank envelope against the bridge's dry approach detector and post-carve terrain before changing geometry. Both cameras, connected bidirectional traversal, unload/revisit, mutations, primary plus three varied production worlds, final affected R1/R2 costs, eligible-library integration and the requested visual floor remain open. All EC01–EC15 verdicts remain NOT VERIFIED.


## Subsequent whole-span correspondence and footprint failure

[The current diagnosis](G08_EnvironmentCohesion_FootprintDiagnosis_2026-10-04.md) supersedes the earlier first-bank interpretation: the blocked point is an internal section seam. Connected true-bank forward/dry-egress/reverse walking covers159.644m with86 footprint warnings. Recorded faces reach67.94032degrees against the58-degree capsule. Three unsuccessful grade candidates are retained and retired; the loop is stopped. Fresh compilation and126 native checks restore the preceding geometry: original119PASS, four new slope/footprintFAIL. Full G08 remains incomplete; no failed candidate was played, legacy accepted PASS remains, and Astra stays stopped. Independent library/reference work is the next executable slice while a shared-width grade review is prepared.
