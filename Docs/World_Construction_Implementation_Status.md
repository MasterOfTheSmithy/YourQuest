# World construction implementation status

Updated 2026-09-06. This is an implementation checkpoint, not production certification.

## Implemented

- Added `requireV2ConstructionSuccess` to the existing runtime builder. Enable it on the builder for strict V2 construction verification. Its default is false so existing scene/save compatibility is preserved. It rejects V2 preparation/preflight fallback; it does not certify independently composable settlements or disable every later presentation fallback.
- Corrected the preflight failure branch to discard this build's uncommitted spatial candidate and clear transient selection/upgrade state before rejection.
- Added a structured `[WORLDGEN CONSTRUCTION]` report when a site is prepared. It identifies source kit/signature, selection seed, requested blocks, selected source units, source instance count, construction mode, and limitations.
- Reports distinguish authored streaming slices, authored zones, verified procedural block layouts, and unresolved combinations. Source IDs and layout identity/geometry are checked. Independent streaming is supported only through the new versioned, reviewed assembly path.
- Population capacity remains explicitly unverified: map fragments cannot be counted as independent buildings or inhabitants.
- Added the Unity menu test `Tools > YourQuest > Testing > Verify World Construction Evidence` using disposable metadata fixtures.
- Captured published catalog construction paths in `Logs/WorldConstructionCatalogBaseline.json` without editing source assets or saves.

## Independent streaming implementation

- Added source-bound independent-assembly metadata to existing cell function contracts: review state, complete structural dependencies, foundation verification and external connection paths. Missing fields default to pending, never implicit approval.
- Added v3 generation seeds while retaining v1/v2 behavior and layout version checks for existing saves.
- Adapted approved streaming assemblies to the existing block selector and layout solver. That path consumes the existing population/service block target instead of donor-map proximity. Raw unreviewed fragments do not become capacity evidence.
- Streaming now applies saved assembly positions and yaw; source children remain together. Residents resolve the selected assembly's placed bounds instead of the donor district's coordinates.
- Unconverted maps retain explicit authored compatibility behavior. Invalid published independent contracts are rejected. No production asset was automatically approved or relabelled.
- Added `Verify Independent Streaming Assemblies`: production selection, disconnected donor geometry, layout, transform application, resident anchoring, stable-ID casing, save round-trip, legacy compatibility and invalid-contract rejection.
- Added `Verify Generation Construction Readiness`: runs integration fixtures and the actual published Viking library test. Fixed the existing test runner so real-library failure produces a failed batch exit instead of a success exit with a FAIL text file.

## Observed production blocker

The real-asset check subsequently completed: the street-connected home passed 32 v3 layouts and existing door/storage/landing tests at four rotations, with its dependency hash matching the candidate. Central, eastern and southern district connections lie 4–36 metres inside their envelopes; west homestead connections satisfy the geometric frontage check.

Fresh v3 selection now filters out zones lacking usable external frontage before layout. Explicit v3 layout requests also reject missing portals instead of silently returning authored placement. Legacy selection remains unchanged. C# runtime/editor compilation passed with the existing unreachable-code warning in `YQWorldGenerationV2ContractTests`. After the editor refresh, the new 32-seed published-selector regression passed: invalid frontage was excluded and pending habitation/service functions remained rejected. The real home also passed 32 current-version layouts and provider/landing checks at four rotations. Evidence: `Logs/YQRealAssemblyConstruction.md`. This geometry filter does not approve habitation, services, or the unpublished home.

The engine integration fixtures pass. The real published library test fails:

`No declared external connection reaches the cell frontage: yq_viking_district_central_village`

The declared district connections do not satisfy the solver's external-frontage geometry contract. The first-kit functional readiness report also records pending habitation, circulation and service evidence. Passing synthetic fixtures cannot approve these assets.

Existing isolated-home candidates and prior door/storage probes provide a useful authoring starting point, but their prior reports explicitly exclude full player interaction, production catalog activation and whole-world certification. A previous home render was inspected; it is not evidence of a newly generated playable region.

## How to evaluate

Use an isolated fresh-world test save, enable `Require V2 Construction Success` on the active `YQGeneratedWorldRuntimeBuilder`, and generate. Review the existing spatial failure diagnostics and `[WORLDGEN CONSTRUCTION]` entries. An authored streaming slice is compatibility geometry, even if V2 terrain and routing succeeded. It is not evidence of a completed procedural town.

The metadata test does not run gameplay, certify visuals, verify population capacity, or prove full rollback/save behavior. Player-height terrain/vegetation checks and a fresh-world/legacy-save runtime test remain required.

## Next implementation boundary

### Live-run grounding correction (2026-09-06)

Semantic selection cache follow-up: keys now include source signature, loaded manifest identity and structural selection policy, with length-prefixed fields preserving tag order and avoiding separator collisions. Previously a replacement manifest with the same kit/seed/tags could receive cached IDs from another source. This cache remains transient and does not rewrite accepted saves. Compilation and direct compiled regression checks for cache identity and structural priority pass. In-place mutation of the same manifest without a signature change is not covered by this cache identity; manifests are treated as immutable during a build. A rebuilt-world visual check remains outstanding.

Follow-up selection fix: the fresh-world structural preference previously added only 40 points while tag position changes scores by 90 points per slot. Measured structure can now outrank decorative tag matches as a separate priority after approved functions. The affordable anchor pool uses the same comparison, preventing random anchor selection from undoing that priority. Non-opted-in legacy selection retains its original ordering; no saved fields or assets were changed. Compilation and the compiled `TestStructuralAnchorPriority` regression pass, alongside the grounding regression. This establishes ordering correctness, not that measured support certifies a complete building or that the current world has been rebuilt.

The current Unity log for seed `092e8606` confirms the visible fallback chain: V2 cell preflight rejects missing Encounter/Reward/Security evidence; V1 selects `town_smith` for `s_willow_haven`; its two authored fragments (five source instances) fail grounding at 55% supported weight; the runtime then constructs `SettlementBuilding_*` palette lots. The streamed `haunted_village` and `western_desert_town` sites also fail grounding. These observations do not establish that every grounding failure has the same cause.

Fixed reversed air-gap and penetration arithmetic in the shared compiled-cell foundation solver. Samples represent `terrain - foundation bottom`, so the applied correction minus the sample is the resulting air gap. Previously the 0.65m embedment allowance was accidentally used for air gaps, and the 0.18m air allowance for embedment. Thresholds and persisted data are unchanged.

Verification: runtime/editor C# compilation passed with the existing CS0162 warning. The targeted foundation regression was executed directly from the compiled assemblies and passed coherent contacts, incompatible contacts, a 0.4m floating secondary foundation, permitted 0.4m embedment, excessive 0.8m burial, and nonfinite input. This verifies the numerical solver, not Unity scene materialization. The current world has not been rebuilt or visually certified with this change. Missing reviewed assemblies and fallback replacement remain production blockers.

P1 content production: author and verify complete assemblies with reachable external approaches and actual function providers, then publish reviewed contracts through the existing source/runtime catalog builder. The current 22 streaming maps have not received independent approvals. The runtime path is implemented and tested with fixtures; the published content still fails the generation-readiness gate.

Population-based selection is wired for approved independent streaming assemblies; population capacity is not certified for the existing fragment library. Terrain visual fixes, full NPC behavior profiles, world revisions, and AAA presentation remain outstanding.

Existing uncommitted project changes were preserved. No scenes, prefabs, imported packs, accepted saves, or NPC systems were intentionally edited by this checkpoint.
