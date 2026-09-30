# WG1 All-Library Asset Intake

**Status:** Intake schema, scanner, unattended benchmark scheduling, and review workbench implemented; first report pending a safe Edit-mode boundary

**Parent specification:** [YourQuest AAA Procedural World Generation Design Plan](YourQuest_AAA_Procedural_World_Generation_Design_Plan.md)

## Decision

Every configured asset-library prefab and material is recorded. Nothing is silently discarded because a filename heuristic cannot classify it.

“All assets usable” means:

- every legitimate asset is attributable to a source kit;
- every prefab receives a stable GUID-backed ID;
- every prefab has a visible intake disposition and repair/review history;
- every safe asset can ultimately participate in an appropriate assembly, location, character, encounter, or dressing family;
- editor helpers and irreparable files remain recorded but intentionally non-spawnable;
- incompatible kits never become a universal runtime pool.

It does not mean every raw prefab may be placed directly into every settlement.

## Implemented records

### `YQAssetKitManifest`

Owns source-root attribution, genre/environment tags, compatibility lists, asset counts, validation counts, benchmark identity, and release eligibility.

### `YQSpatialAssetRecord`

Owns the stable asset ID, source GUID/key/path, semantic role, composition scale, local bounds, footprint, clearance, height, front direction, slope/foundation/road/navigation contracts, socket candidates, renderer/material/collider/LOD costs, missing scripts, disposition, and validation issues.

### `YQMaterialAssetRecord`

Owns material attribution, shader identity, URP compatibility state, release eligibility, and repair/review issues.

### `YQWorldAssetIntakeCatalog`

Stores one deterministic snapshot of kit, spatial-prefab, and material intake records. This complements the existing lightweight semantic catalog; it does not replace save authority or create another runtime registry.

## Intake dispositions

| Disposition | Meaning |
|---|---|
| `NeedsSpatialReview` | Asset loaded successfully but authored placement metadata is incomplete. |
| `Candidate` | Reserved for assets that pass automated checks and later authored review. The initial scanner never assigns it automatically. |
| `NeedsMaterialRepair` | One or more material slots use missing, unsupported, HDRP, Standard, or legacy shaders. |
| `MissingRenderer` | Prefab exists but has no visual renderer to classify. |
| `MissingScriptRepair` | Prefab hierarchy contains missing MonoBehaviour references. |
| `EditorOrDemoOnly` | Asset lives under an Editor-only path and must not enter runtime. |
| `Quarantined` | Asset could not be safely loaded or measured. |

## Editor commands

After leaving Play mode and allowing Unity to finish compiling:

- `Tools > YourQuest > AAA World Generation > Scan First Benchmark Kit`
- `Tools > YourQuest > AAA World Generation > Scan All Asset Libraries`
- `Tools > YourQuest > AAA World Generation > Open Asset Intake Workbench`

The first command scans `Assets/BefourStudios/MedievalVikingVillage`. The second merges every currently configured prefab and material discovery root and records all prefabs and materials without mutating source packages.

When no intake catalog exists, the benchmark scan also schedules itself for the first safe Edit-mode boundary. It never force-stops Play mode, starts a competing editor process, or scans while Unity is compiling/importing.

Results are written to:

- `Assets/Assets/Resources/YQWorldAssetIntakeCatalog.asset`
- `Assets/Assets/GeneratedAssets/WorldIntake/YQWorldAssetIntakeReport.md`

## Automated evidence versus authored authority

The scanner may safely determine:

- source GUID and root;
- mesh bounds and footprint evidence;
- renderer, material-slot, collider, LOD, and missing-script counts;
- obvious material pipeline failures;
- possible door, entrance, connection, and dressing transforms;
- tentative semantic role and composition scale.

The scanner may not authoritatively decide:

- which direction is architecturally “front”;
- whether a door-like transform is the public entrance;
- parcel frontage and setbacks;
- correct foundation and slope behavior;
- compatible neighboring architecture;
- whether a modular set forms a complete building;
- district-level visual compatibility;
- final collision/navigation suitability;
- production release eligibility.

Those fields require the WG1 intake workbench and reviewed metadata. This human-authored layer is the part that turns abundant raw assets into reliable procedural vocabulary.

## Next code slice

1. Run the benchmark scan after Play mode exits.
2. Inspect its generated counts and repair categories.
3. Open the Asset Intake Workbench and review one record at a time.
4. Add project-owned wrapper metadata without editing third-party prefabs.
5. Promote reviewed Viking assets from `NeedsSpatialReview` to `Candidate`; the workbench refuses approval while required materials, bounds, collision, front, foundation, frontage, or authored metadata are missing.
6. Begin WG2 assembly authoring only after the benchmark kit passes its intake gate.
