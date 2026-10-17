# Activation bottleneck checkpoint

This coordinator summary reconstructs findings from the reports read during this conversation. The original detailed report under the ignored `outputs/WorldAuthoringReview_20261015/` directory is no longer present at publication time. The copied native JSON and asset metadata are present and hash-verified; distinguish them from this reconstructed narrative.

The earlier matched PlaySafe capture on the disposable profile measured 1,461.18 ms in `YQ.Population.Activation`. Its 20 direct `Loading.ReadObject` children took 1,449.07 ms inclusive. The full activation subtree had 35,432 samples without scan truncation. Metadata retention was capped, so the 52 distinct imported `.anim` paths are retained evidence rather than an exhaustive asset inventory.

Native deserialization and file/header reads dominated the frame. Twenty distinct dwarf LOD0–LOD3 clips accounted for 607.90 ms inclusive among retained serialized-read samples. The project callbacks were cheap: six `NpcDialogueAgent.Awake` calls totaled about 0.55 ms, six `YQDotCreatureVisual.OnEnable` calls about 0.12 ms, and 24 `Animator.Rebind` calls about 5.87 ms. Inclusive parent and child totals overlap.

The production population owner builds an inactive accepted resident group and calls `staging.SetActive(true)` after canonical/context validation. All LOD animators are driven by the existing visual binder. Disabling LOD animators or weakening readiness would change behavior and is not an accepted repair.

The raw baseline remains local as `outputs/OriginLag_20261009/native-population-activation-20261016_213820_200.data`; previously verified SHA-256 was `A4F7B6D8BDED576A0026EFAD1DB1E2CAEB6607A6BB3C8507B5B12603CF7A04AC`. The copied compact exports are indexed by `Evidence_Provenance.json`.

This is Editor runtime evidence from normal-speed assisted travel. It does not prove player-build performance, actual sustained 200 m/s traversal, or a world-edge repair.
