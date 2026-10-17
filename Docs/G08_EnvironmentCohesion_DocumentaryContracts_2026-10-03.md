# G08 existing G04 documentary contract integration

Status: **INCOMPLETE** for EC01/full environment integration. Prior user-accepted legacy PASS decisions remain accepted/verified. This slice prepares the existing intake and master emitter for complete source ingestion; it does not approve assets or add production palettes.

## Change and preserved contracts

`YQDiscoveredWorldAssetCatalog.cs` now defines evidence contract version 1 for approval, exact subject/package/version/hash, provenance/license references, delivery permission, supersession and consumer evidence. Missing state/version remains unknown/zero. A static reference is distinct from an Editor fixture or ordinary runtime witness. Technical eligibility remains independent of these documentary facts.

`YQWorldAssetIntakeCatalog.cs` retains outer `world_asset_intake_v3`, all existing fields and enum values. Its existing `SetRecords` signature retains documentary facts and kit/asset evidence links, including false/unreviewed records. GUID identity follows a moved asset; a different GUID at the same path does not inherit the old evidence. No runtime selector was changed.

The existing `YQWorldAssetIntakeBuilder` emitter now has an explicit `yq_world_generation_asset_inventory_v6` export with evidence contract version 1, stable asset identities, source keys and documentary links. Its pure projection operates on copies. Historical world-kit policy remains unchanged; an explicit `LibraryCoverage` row preserves unknown classification instead of acquiring an exclusion or representation through filename matching. Export is still published only by the existing intake boundary.

At the first contract batch the production master remained v5 and unchanged. The later direct-source batch and reviewed documentary publication are recorded below. This metadata contract does not itself stop a runtime alias selecting a withdrawn payload; that admission audit remains open.

## Fresh verification

| Evidence | Result and exact scope |
| --- | --- |
| Unity compilation outside Play | Updated runtime and Editor assemblies compiled; filtered current diagnostics contained no C# compiler errors. Unity `6000.3.2f1`. |
| First fixture attempt | [Retained FAIL](../outputs/G08_EnvironmentCohesion_20261003/20261003_234135_250_asset_library_documentary_contracts/Receipt.json): historical master preservation passed; generic fixture snapshot serialization followed `Vector3.normalized` recursively. |
| Fixture correction | Snapshot complete spatial fields through Unity's field serializer; keep evidence/source rows in their ordinary JSON form. Production export behavior was not changed for this correction. |
| Focused rerun | [22 checks PASS](../outputs/G08_EnvironmentCohesion_20261003/20261003_234246_582_asset_library_documentary_contracts/Receipt.json): old-v3 round-trip, enum values, unknown defaults, hold/withdrawal/scoped approval retention, GUID moves/replacements, non-mutating/reordered export, historical policy and all 28,199 master classification/reason/policy tuples/counts. |

The passing receipt records runtime MVID `4bffde7f-87aa-4831-9e17-67636d8b582d`, Editor MVID `1d2f15a2-fa75-4d4c-bd08-e5123668ef18`, both assembly hashes, four source hashes and master hash before/after. Master SHA-256 remains `a4c7cefc0ff5d733765abc2f473acc5537d4ef5b2cbd4a0cc5be6b235e84c722`. `runtimeVerified=false` is intentional. The later terrain-only fixture edit rebuilt the Editor assembly; it does not invalidate these unchanged documentary sources or establish production runtime acceptance.

Run entrypoint: `YQAssetLibraryEvidenceVerification.Run()` / **YourQuest → Verification → Run Asset Library Documentary Contracts**. Existing bootstrap accepts the explicit Edit-only `Temp/YQ_ASSET_LIBRARY_EVIDENCE.request`. Fixtures destroy their disposable objects and write timestamped receipts under `outputs`, without scanning or publishing production assets.

## Next integration and recovery

Ingest actual DOT index/layout/domain records and uncovered family sources through this same intake/emitter. Preserve baseline versus installed corrective hashes separately: 56 current layout entries declare both. Exact correction approval must not spread to the original payload or all sibling variants. Retain every historical classification and unknown/held/withdrawn fact, then review eligible role bindings through current production consumers.

No state-schema migration, save rewrite, asset GUID change or accepted-world regeneration occurred. Additive documentary fields are absent from existing serialized assets until an explicitly reviewed intake publication. Reverting this slice before publication needs only the bounded source changes; historical receipts and the unchanged production master should remain available.

## Direct-source ingestion batch

The existing builder now reads the actual DOT index/layout, equipment and creature YAML catalogs, preserved lazy-registry keys, item pools, material adapter references, provenance documents, exact tracker package facts and all 17 explicitly named source roots. The pure merge retains legacy review/admission tuples, separates baseline and installed corrective payload declarations, resolves exact source-path/alias/hash relations and carries documentary history through a later legacy rescan. Binding observations remain static or unknown evidence; no art approval or ordinary runtime use is inferred.

The first expanded run retained [35 passing checks and one extraction failure](../outputs/G08_EnvironmentCohesion_20261003/20261004_002203_044_asset_library_documentary_contracts/Receipt.json). The reader rejected `Human Male (v4).FBX.meta`, an existing 1,416,348-byte ModelImporter document, because it imposed a one-megabyte limit on the whole metadata file. Its GUID is on line two. This is a reader defect, not an invalid model or missing source.

The corrected reader examines a bounded 4,096-byte header, preserves file-length/timestamp stability checks and records metadata stamps for final input-stability validation. It neither modifies importer data nor hashes the model payload. Compilation/reload completed outside Play Mode; [the 50-check rerun passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_002512_721_asset_library_documentary_contracts/Receipt.json). Its candidate reported 81 exact-path/hash conflicts. Review found the source reader had anchored retained library contracts to revised main payloads and had omitted valid package anchors on parts-only records.

Export-root recovery now requires a matching declared export hash and explicit layout path/alias, including a declared parts source. Alias ordering is canonical; unmatched versions remain unresolved. [An intermediate 50-check receipt](../outputs/G08_EnvironmentCohesion_20261003/20261004_003056_172_asset_library_documentary_contracts/Receipt.json) used the revised reader but did not execute the three newly added fixture checks. It is retained as partial evidence, not the final regression result.

After a complete fixture recompile, [all 53 checks passed](../outputs/G08_EnvironmentCohesion_20261003/20261004_003329_230_asset_library_documentary_contracts/Receipt.json), with compiled verifier version `source-coverage-2-exact-export-anchor`. Runtime MVID remains `4bffde7f-87aa-4831-9e17-67636d8b582d`; the receipt records the executing Editor identity and both assembly hashes. The candidate contains 56,435 source-accounting records, 3,678 binding observations (2,093 equipment, 208 creature, 1,377 material adapters) and 7,509 declared source versions. All 28,199 historical classifications/counts and 2,878 generation-ready rows remain unchanged. No reported path/hash conflict remains, but 162 relative export relations on 54 domain entries remain explicitly unresolved; these are not silently treated as joined.

## Reviewed documentary publication

[Publication receipt and rollback copy](../outputs/G08_EnvironmentCohesion_20261003/20261004_003329_230_asset_library_documentary_contracts/Publication.json): root published the exact reviewed candidate bytes into the same canonical G04 `YQWorldGenerationAssetInventory.json` in idle Edit Mode, after checking the passing verifier version, assembly identity, text-source hashes and unchanged original master. Master SHA-256 is now `dfa52ba9e6041664f37a44a9f3b399122fbf96c42018369b8508ff3b0ccf140f`; its `.meta` SHA-256 remains `707c2d3463971b0c3b2858252581703668bf6e5cbe9bf65fab8810ca608bc39c`. The original v5 bytes remain in `BeforePublicationMaster.json` beside the receipt. Normal Unity import observes this JSON change; no imported source asset, registry, runtime palette or save was rewritten.

The master now carries export v6 / library coverage v1, with 28,236 additional unknown review rows. Two held and two withdrawn exact package records retain delivery denial. Existing technical eligibility is unchanged. Missing licenses, approved-role decisions, archive/member admission guards, dimensions/costs and actual production-use evidence remain open. This publication is documentary integration, not EC01 or world-wide environment completion. Rollback must preserve subsequent user edits and documentary history; the backup is evidence, not permission for an automatic reset.
