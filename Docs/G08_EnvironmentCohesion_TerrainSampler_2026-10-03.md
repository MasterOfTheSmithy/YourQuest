# G08 numeric terrain sampler evidence

Status: **PARTIAL**; EC04 remains **NOT VERIFIED**. No production terrain algorithm or accepted world was changed.

The old fixture sorted coordinates before querying, so reversing its input never exercised reverse query order. It also compared two constructions of the same absolute coordinate under a label suggesting stronger origin/border coverage. The repaired fixture queries first, then sorts sampled results for comparison; non-finite values cannot pass through matching sentinel hashes. The coordinate identity row is now explicitly limited to that mathematical identity.

Broadening the fixture exposed a missing input: sampling the origin with neither terrain nor an accepted sampler throws `InvalidOperationException: continuous world authority has no origin terrain or accepted sampler`. The consumed request initially produced no receipt. Filtered Editor-log observation retained below records this failed attempt; it was a fixture construction defect, not evidence of a production origin failure.

The fixture now creates a disposable 33×33 sloped height grid in an isolated Editor preview scene. It closes the preview scene and destroys its TerrainData in `finally`; caught test exceptions produce a failed receipt. It neither loads an accepted world nor edits the production scene.

[Fresh PASS receipt](../outputs/G08_EnvironmentCohesion_20261003/20261003_234626_320_terrain_sampler/Receipt.md): four numeric fixture seeds, 295 coordinates per seed spanning signed axes/diagonals, origin/collar and ±65,536 m; forward, warm reverse, fresh forward, cold reverse, cold shuffled and parallel shuffled queries. All hashes matched per seed, all four seeds had distinct hashes, and failures were zero. Parallel work uses only the existing immutable off-main-thread numeric API. This is **EDIT_MODE_SYNTHETIC_TERRAIN_SAMPLER** evidence.

Executing runtime MVID `4bffde7f-87aa-4831-9e17-67636d8b582d`; Editor MVID `6308a8db-c7f1-4883-b123-fafe21dababa`. Receipt carries both assembly hashes plus authority/verifier source hashes. The authority source SHA-256 remains `533bf5cceaa74e49890d1db85515292d6bb94b8f728898846d95a3f3919fac13`.

These four numeric seeds are not the primary plus three varied accepted production worlds. No accepted-origin transition, independent neighboring TerrainData realization, paint, normals, physical collision, ecological composition or visual seam certificate is asserted. Those remain required production checks.

## Retained failed-attempt observation

Observed through filtered local Editor log on 2026-10-03 after the first expanded request, before adding the terrain fixture:

```text
InvalidOperationException: continuous world authority has no origin terrain or accepted sampler
YQContinuousWorldCellAuthority.SampleOriginNormalized ... YQContinuousWorldCellAuthority.cs:810
YQContinuousTerrainDeterminismTests.BuildSampleHash ... YQContinuousTerrainDeterminismTests.cs:171
YQContinuousTerrainDeterminismTests.RunFromMenu ... YQContinuousTerrainDeterminismTests.cs:102
```

No structured receipt or verifier source hash was preserved for that failed attempt. The excerpt is diagnostic observation, not a fabricated test receipt. The final implementation closes this evidence-loss path by writing caught failures.
