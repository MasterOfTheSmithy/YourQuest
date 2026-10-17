# G08 library reconciliation coverage

Status: **INCOMPLETE — static source reconciliation, not production integration**.

The existing G04 master remains `Assets/Assets/GeneratedAssets/WorldIntake/YQWorldGenerationAssetInventory.json`. The [derived reconciliation](../outputs/G08_EnvironmentCohesion_20261003/world-asset-library-reconciliation.json) joins it to the DOT index/layout, domain catalogs, current prefab GUIDs, registry shards and recorded withdrawal evidence. It does not approve art or become a selectable runtime registry.

## Reproducible extraction

Run `node Tools/reconcile-world-asset-library.mjs --root .`. Use `--check-only` for the same accounting checks without writing. The tool header documents additional bounded family roots and output restrictions. It reads source manifests and metadata; binary payloads are not read or independently hashed.

The coordinator independently reran `--check-only`: **PASS, static extraction only**.

| Accounting unit | Observed total | Meaning |
| --- | ---: | --- |
| G04 and DOT source-file identities | 35,652 | Paths/GUIDs, including dependencies, source records, modules and LOD files; not a unique placeable-asset count. |
| Equipment domain bindings | 2,093 | Existing eligibility copied literally; 317 currently generation-eligible. |
| Creature/module domain bindings | 208 | Installed domain bindings; catalog membership alone does not certify approval or creature behavior. |
| Source links from domain bindings | 3,068 | Matched links to 2,416 distinct source-file identities. Multiple links do not inflate binding totals. |
| Existing DOT registry shards examined | 66 | All 2,301 domain bindings matched current prefab GUIDs, including moved wrapper folders with preserved stable keys. |
| Identical supplied-hash groups | 327 | Recorded as payload overlap; different paths/LODs/modules are not automatically extra variety. |
| Named additional family roots examined | 9 | Bounded metadata examination, not a claim that every available source root/archive was examined. |
| Files outside supplied source/domain coverage | 7,242 | Includes dependencies, audio and documents; not 7,242 missing placeable assets. |

The report found zero path/GUID conflicts, alias conflicts, unmatched layout records, disagreements between supplied hashes, or generation-eligible equipment missing its expected existing production-pool reference. These checks establish source accounting, not binary integrity or rendered use.

## Additional-family boundary

| Actual root | Non-meta files | Outside supplied catalogs | Next existing owner |
| --- | ---: | ---: | --- |
| `Assets/Forst` | 78 | 74 | G04 intake; retain conifer source/material/detail dependencies. |
| `Assets/GabrielAguiarProductions` | 3 | 3 | Documentation only in this checkout; effect payload unavailable. |
| `Assets/Grass And Flowers Pack 1` | 93 | 93 | G04 terrain/detail role intake; zero prefabs does not mean zero useful groundcover sources. |
| `Assets/Magic Pig Games (Infinity PBR)/Audio` | 3,861 | 3,861 | Existing audio owner and G04 provenance view; no new ambience system. |
| `Assets/Magic Pig Games (Infinity PBR)/Characters` | 3,636 | 1,747 | Existing actor catalogs/anchors; preserve covered source families. |
| `Assets/Magic Pig Games (Infinity PBR)/Weapons & Armor` | 1,114 | 1,114 | Existing item/equipment domain binders. |
| `Assets/YourQuest DOT Creatures` | 557 | 349 | Existing creature catalog and wrapper/source provenance. |
| `Assets/YourQuest DOT Equipment` | 2,093 | 0 | Already covered by current domain bindings; do not rediscover wrappers as missing new art. |
| `SourceAssets/DOT` | 1 | 1 | Source/provenance inspection; not a runtime asset count. |

## Approval and production-use gaps

[ArtDirection_Production_Tracker.md](ArtDirection_Production_Tracker.md) supplies exact withdrawal/hold evidence for Fairy v4, Avian v6 and both Kitsune v12 archives. Their four exact archive hashes and source lines remain in the report. No state was reversed. Archive availability and actual selection exclusion still need a complete canonical approval join and runtime admission audit.

The G04 master's 28,199 records do not carry explicit approval source/date or exact consumer links. The current equipment/creature catalogs also lack per-entry approval evidence. Existing generation-ready/eligible values remain technical/domain admission facts; unknown approval remains unknown. The master's 2,999 unreviewed-context exclusions need specific role/family review under the expanded objective.

The next integration boundary is the existing inventory emitter in `YQRuntimeWorldAssetRegistryBuilder.cs` and its intake/discovered record contracts. Add approval/provenance/version/supersession/consumer evidence there, ingest the joined source views, and derive downstream catalogs from that master. Do not introduce another registry or use this report to admit withdrawn/unknown assets.

## Existing G04 contract integration

A later [17-root metadata report](../outputs/G08_EnvironmentCohesion_20261003/world-asset-library-all-named-roots.json) widens examination to the full named purchased/generated roots. All 17 enumerations completed without caps, errors or skipped links. Their 56,435 non-meta file observations include 18,482 records outside the original source/domain identity views; these include dependencies, derived materials, catalogs and evidence, not 18,482 missing placeable assets. This does not add those files to the master or replace the historical nine-root report.

| Newly widened boundary | Outside prior source/domain identity views | Meaning |
| --- | ---: | --- |
| `Assets/Assets/GeneratedAssets` | 9,757 | Mostly existing material adapters plus assemblies, catalogs and evidence; preserve derivation rather than counting new art. |
| Full `Assets/Magic Pig Games (Infinity PBR)` | 7,818 | Includes shared files/tools as well as the previously observed character/equipment/audio roots. |
| Full `Assets/Tom's Terrain Tools` | 181 | Includes example/tool/dependency data outside the earlier terrain-only root. |
| Full `Assets/YughuesFreeBushes2018` | 37 | Mesh/material/texture dependencies outside prefab-only discovery. |
| `Assets/Assets/Art` | 5 | Two Quaternius animation FBXs and their README/license files; actor presentation ownership remains unchanged. |
| `Assets/Assets/Resources` | 154 | Catalog/registry/configuration/shader/image records, not independent placeable objects. |
| `Assets/Assets/Terrain` | 10 | Existing terrain-owned source/data requiring a traceable dependency role. |

Full Befour, HIVEMIND, HumbleBundleResources and ADG roots had no unmatched file identities in this join. Identity coverage does not establish visual, license, approval or production-use acceptance. External archives and other unexamined sources remain explicit gaps.

The existing intake/emitter now carries additive versioned approval, provenance, supersession and consumer evidence with unknown defaults. Outer intake v3, enum values and runtime admission are preserved. The pure v6 export preserves documentary unknowns under an explicit coverage policy; it does not change the production master until a reviewed publication is invoked. [Twenty-two Edit checks passed](G08_EnvironmentCohesion_DocumentaryContracts_2026-10-03.md), including preservation of all 28,199 historical classification/reason/policy tuples and counts. No source scan or catalog/master publication occurred.

The next source-ingestion slice must keep the 56 layout records with both baseline and installed corrective hashes distinct. An exact corrective approval cannot approve its original payload or every sibling variant.

## Published direct-source accounting

The [reviewed v6 master publication](G08_EnvironmentCohesion_DocumentaryContracts_2026-10-03.md) now accounts for 56,435 records from the actual declared index/layout and 17 examined roots, with 3,678 binding observations and 7,509 declared payload versions. Fifty-three focused checks passed on the complete fixture, preserving every historical classification/count and unchanged runtime admission. The preceding metadata-size failure and intermediate source-join receipts remain retained.

All 81 reported path/hash conflicts were resolved by exact version/export-root recovery. Another 162 relative exports on 54 equipment entries remain explicitly unresolved (27 technical-generation-eligible entries and 27 ineligible entries). Their direct primary sources and registry wrapper bindings remain separately recorded; relative siblings are not asserted as current runtime LODs or licensed/approved variants. Source hashing is based on supplied binary declarations and actual text bytes, not freshly verified binary payloads.

EC01 remains **NOT VERIFIED**. Required archive/source availability, exact approved-role coverage, remaining relative contract relations, dimensions/cost evidence, production palette reachability and verified materialization are outstanding. The 28,236 new review rows remain unknown rather than automatically approved or excluded.
