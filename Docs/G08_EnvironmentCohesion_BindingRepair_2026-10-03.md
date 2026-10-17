# G08 canonical site binding repair

Status: **focused contract PASS; expanded EC02 remains NOT VERIFIED**.

## Reproduction and owning boundary

The existing `YQCompiledWorldSiteBindingService` used the bindings already written into the plan to suppress repeated kits. A site's choice therefore depended on which site was requested first. `YQRuntimeWorldSiteCatalog.FindCompatibleSites` also returned serialized insertion order despite its comment promising kit order.

The retained [four-site reproduction](../outputs/G08_EnvironmentCohesion_20261003/20261003_230124_825_binding_contracts/Receipt.json) is a synthetic Edit Mode fixture, not a played world. It demonstrated settlement request-order differences across four seeds and catalog-view reordering. The earlier three-site fixture had symmetric forward/reverse results and was insufficient to expose the settlement defect.

## Coherent repair

- New bindings use `reviewed-site-binding-5-canonical-features`. Existing valid `reviewed-site-binding-4-semantic-slices` bindings replay without migration or reroll.
- New choices derive prior choices from stable typed feature IDs, canonical order and the feature/world seed. Earlier unbound choices are calculated without committing them. Compatibility, reviewed function requirements, style boundaries, exclusions and settlement variety preference remain enforced.
- Settlements and hostile sites share the existing binder's ordering rule; no second authority or runtime cache was added. Equal candidate score/hash uses stable kit ID as the final tie-break.
- Compatible query results sort their returned view; shared serialized catalogs remain unchanged.
- `YQGeneratedWorldRuntimeBuilder` preserves an accepted kit's previous binding version when revalidating that same kit. A genuinely new or replacement accepted kit receives version five only after existing composition acceptance.

Changed production owners: `YQRuntimeWorldSiteCatalog.cs` and the existing builder's accepted-binding commit callback. State schema 8, profile schema 2, accepted geography/V2 hash, site IDs, `.meta` GUIDs and serialized field names remain unchanged.

## Verification and limits

The [focused final receipt](../outputs/G08_EnvironmentCohesion_20261003/20261003_231301_682_binding_contracts/Receipt.json) passed **22 synthetic Edit Mode checks**, covering four seeds, catalog/request order, reordered plan storage and JSON round-trip, mixed settlement/encampment request order, accepted version-four replay after catalog additions/reorder, preserved commit version, selection-only non-mutation and exhausted candidate rejection. It records executing assembly hashes/MVIDs, source hash, Unity version and binding policy.

This is fixture evidence. [Production ordinary Continue](../outputs/G08_R3_Physical_20261003/20261003_231619_459_session/Discovery.json) completed on the same profile/world/seed and V2 artifact, followed by successful protected restore and 12 unchanged post-exit save fingerprints. It observed two settlements bound to `qualified_viking_home` and two hostile sites bound to `qualified_occupied_homestead`, all version five. It does not prove those distant sites physically materialized. The earlier snapshot lacked the new binding field, so live version-four-to-five preservation was not observed; version-four replay preservation is supported by the synthetic fixture only. No historical metadata is invented to fill that gap.

Parallel numeric query coverage, arbitrary input permutations, all compatibility families, candidate-addition/reload under actual production use, ecology selectors, newly discovered frontier feature sets and full old-layout replay remain open. These tests do not certify weighted ecology, complete library reachability, visual quality or streaming performance.

## Recovery

No explicit save-schema migration or catalog rewrite was performed. Ordinary startup commits remain owned by the profile system. Preserve all accepted version-four records. If this policy needs revision, keep version-five accepted kit/version pairs as replay authority; do not relabel them version four or regenerate their layouts. Diagnose a rejected candidate at its existing admission/composition owner and retain the failed evidence.
