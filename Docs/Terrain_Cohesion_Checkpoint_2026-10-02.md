# Terrain cohesion — first repair and visual direction

## Scope and status

The user permits a partial R2 outcome so work can move forward, and reports ungrounded trees, rivers, dirt paths, settlement connections and overly sheer mountains. Four supplied Skyrim images establish a visual reference for intentional landscape composition. They are references, not claims that the current project meets that quality.

**R2: PAUSED at PARTIAL handoff, not full certification.** P25F has one user-accepted focused pass at 45 ms input / 51 ms coast. The independent P25R repeat failed at 62/52 ms and incomplete required ecology in coast cell (13,6). Remaining matrix coverage and repeatability are open. See `G08_R2_Current_Matrix_2026-10-02.md` for exact evidence boundaries. The original goal is not marked complete; no R3 itinerary is launched.

**Terrain cohesion: PARTIAL.** One demonstrated tree-placement defect repaired. Mountain shape, rivers and route connectivity remain open; visual quality is not yet accepted.

## Demonstrated cause and change

`GroundWildernessInstance` in `YQGeneratedWorldEnvironment.cs` recognized trees, but only retained their special placement path if a renderer name contained trunk/bark/stem/stump. Combined or generically named tree meshes fell through to the Vegetation category. That allowed shrub tilt and sampled the broad, sometimes asymmetric canopy as a support footprint.

- Retain Tree category when named trunk geometry is unavailable.
- In `YQGeneratedWorldTerrain.TryPlaceGroundedObject`, use the tree root anchor for terrain contact; retain visible bounds for vertical pivot correction. Preserve the original rotation and named-trunk path.
- Return grounding success to the existing wilderness finalizer so an unsupported candidate cannot be published as grounded. Existing explicit suspended-object exemptions remain.
- Add detached preview fixtures in `Editor/YQTreeGroundingVerification.cs`, dispatched by the existing `YQEditorAutoRefreshBootstrap` request/menu path. No independent persistent update callback.

No terrain heights, canonical plan, seed, hydrology, route coordinates, scene, prefab, import settings, save schema or accepted records were edited. Existing dirty changes were preserved. Ordinary Continue may perform normal profile-owned runtime saves; byte-for-byte save immutability is not claimed.

## Evidence

- Before: 73 checks, 20 failures, including wrong root elevation and accidental tilt. `outputs/Terrain_Cohesion_20261002/Tree_Grounding_Before.txt`.
- Intermediate assembly: 73 checks, 8 failures; preserved in `Tree_Grounding_After.txt`. Runtime MVID `9c23e12e-0d5b-41f7-bdd9-dea316813d93` did not contain the final combined patch. A later import changed runtime identity; do not treat the intermediate result as a same-build repeat.
- After both runtime edits imported: 73/73 PASS at 20:08:38 UTC, MVID `b8423c09-43e7-42cf-b0d1-36a1336eef97`. `Tree_Grounding_Diagnostic.txt`.
- Final expanded check: **77/77 PASS** at 20:15:39 UTC. Includes signed slopes on both axes, named and generic meshes, two scales, nonzero terrain origin, preserved yaw, missing geometry/terrain, out-of-owner rejection, unchanged rejected position and unchanged player/world owners. `Tree_Grounding_Final.txt`.
- Fresh Unity compilation succeeded. Existing unrelated CS0162 at `YQWorldGenerationV2ContractTests.cs:385` remains. Source/build hashes: `Final_Identity.json` in that output directory. Final patch whitespace check passed.
- Ordinary PlaySafe/title/Continue run used existing profile `3ccfff4df9a1479884061e51940e4457`; it did not reset the profile or arm a stress verifier. Started around 20:10:30 UTC; gameplay presentation released by 20:12:25. Fresh native screenshot showed first-person gameplay. Stop request returned Edit by 20:12:44, with no compile during observed Play. `Live_Startup_Released.status` preserves the release status.
- The live screenshot confirms broad flat-topped walls and abrupt terrain cuts. It does **not** show roots close enough to certify the imported tree assets' appearance. Ordinary load success is not grounding, river or route acceptance, nor a clean performance measurement.

## Landscape acceptance direction

The supplied design plan's landscape-cohesion rules already require macro terrain to govern routes/sites, water before roads/habitation, vegetation exclusions and multiple detail scales. Apply these concretely:

1. **Vegetation contact:** rooted trunks with no air gaps or buried crowns; appropriate slope/water/route exclusions; grouped canopies, clearings and understory with purposeful density. Close-view checks must include native terrain trees and streamed GameObjects across cell boundaries.
2. **Water:** continuous channels and receiving bodies, plausible downhill flow, banks containing the water datum, no floating ribbons, dry gaps or broken confluences. Check actual saved terrain and rendered meshes together.
3. **Paths and settlements:** continuous ground-conforming paths, manageable grades or deliberate switchbacks, bridges at genuine crossings, entrance connections and material shoulders that blend into adjacent soil. Validate travel to required entrances, not only line proximity or painted texels.
4. **Mountains:** broad connected mass, foothills, valleys, shoulders and selective crags; avoid large vertical cut walls and uniformly sharp ridges. Preserve distinctive peaks and cliffs where the composition calls for them. Snow/rock/vegetation distribution should follow elevation, slope and exposure.
5. **Composition:** readable landmarks, foreground/midground/background depth, coherent rock families and scale, deliberate sightlines and transitions. Increasing random scatter is not evidence of curated quality.

The user selected **pause R2 at partial and build a separate terrain preview** on 2026-10-02. The goal tool returned paused. P25F remains accepted at the user's 51 ms allowance; P25R independently failed at 62/52 ms with incomplete ecology, as documented in `G08_R2_P25R_Repeat_Result_2026-10-02.md`. No further R2 run was started for the preview. Existing-world reshaping must account for stored heightfields, foundation support, water datums, route continuity and cell seams; migration is not authorized by the preview choice.

## Separate preview implementation

`Assets/Assets/Scripts/Generated/Editor/YQTerrainCohesionPreview.cs` builds an explicit Editor-only design study through `YourQuest/Preview/Build Separate Terrain Cohesion Study`, or the existing dispatcher using `Temp/YQ_TERRAIN_COHESION_PREVIEW.request`. It creates timestamped assets under `Assets/YourQuestPreviews/` and reports/images under `outputs/Terrain_Cohesion_Preview_*`; `outputs/Terrain_Cohesion_Preview_Latest.txt` identifies the latest build. This is an authored deterministic composition fixture, not production world generation or an accepted plan replacement.

The scene has broad overlapping mountain masses, a shared river surface/bed profile, terrain-painted trail and settlement branch, grouped conifers excluded from water/routes/steep slopes, and a building scale study. Existing imported assets are reused. Private terrain-layer copies correct the preview palette by observed appearance; shared layer names currently do not reliably describe their textures. The scene is excluded from build settings and has no bootstrap, player, save or streaming owner. Scene capture reloads the saved artifact before rendering and restores the previous active scene.

The initial build exposed paint persistence and palette defects. Painting after TerrainData/terrain initialization and explicitly saving the preview-owned alphamap subassets preserved trail weight at 1.000 after reload. Water material tuning is private to the preview; conifers preserve their supplied URP materials, and Nordic rocks use the existing matching adapter. The first build used global `SaveAssets`, which can flush pre-existing dirty assets; this was corrected to saving only assets in the new preview folder. Do not infer byte-for-byte immutability of all pre-existing dirty assets from this workflow. No profile-loading or save-writing API is called.

See the latest preview's `Review.md` for fresh geometry and contact measurements. Passing those checks does not certify visual quality, authored doorway access, production seams, gameplay traversal or streaming performance. The preview is a reviewable starting point; it is not at the supplied Skyrim reference quality.

## Remaining

Close visual verification of the tree repair; actual river/path/settlement mismatch diagnosis; a reviewed mountain-shape preview or migration plan; integrated terrain/seam/grounding/travel checks and final visual review against the supplied references. Keep the original R2 deficits explicit when discussing the partial handoff.
