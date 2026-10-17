# G08 bridge footprint diagnosis and stopped repair loop

Status: **INCOMPLETE**. EC06 remains NOT VERIFIED. Legacy forced PASS decisions remain accepted; Astra stays stopped.

## Executed physical evidence

[20261004_whole_span_walk_review.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_whole_span_walk_review.json) independently checks raw frames, requests, terminal results, warnings, focus and hashes. Ordinary synthetic keyboard/mouse input traversed the connected local-origin whole span forward, exited onto dry terrain, then returned: **159.6442153 m, 86 UnsupportedFootprint warnings** (45 forward, zero dry-egress, 41 reverse). Every requested endpoint was reached; zero recorded streamer failures or unfocused frames. This does not establish a safe capsule crossing. No sweep was measured. The existing observed-home locality guard is explicit; semantic detour ownership and both cameras remain unverified.

Profile 3ccfff4df9a1479884061e51940e4457, world yq-world-0284d4409a720743c13eb56e, seed 4bb221dc, accepted artifact c1d6ce9d3e06f760, revision87, commit 3743f86b8ee74118b27d09b259e3262e. Executing runtime MVID a63612f1-1e30-4f0b-a920-3fa65a114fbd; Editor 58cd43bd-193d-431e-9734-4bb2d3c8fb6e. The review retains complete assembly hashes, per-leg captures, coordinates and raw evidence. In-memory restoration succeeded; all12 protected files and all9 original-profile paths matched after exit. No save was edited directly.

## Corrected bank correspondence

The prior blocked point near(-7.599,-23.524) is an internal section seam, not the first bank of the whole bridge. The recorded whole-span trace starts at(-21.114712,-3.827282); its tread is approximately0.400002m above terrain, within the recorded0.52m step height as a numeric observation only. Continuous walking from the true approach now reaches the other side and dry egress. The historical failed direct ingress and2.36m foot-to-tread gap remain valid observations at the submerged internal seam. A terminal collision section does not establish the full-span bank.

## Reproduced collision cause

[20261004_buffered_footprint_profile.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_buffered_footprint_profile.json) recovers23 original centerline anchors and180 recorded native capsule-footprint queries with provenance hashes. The baseline reproduces the captured fitted vertices/indices exactly. Long triangulation fans crossing the nonlinear grade create five fitted top faces above the capsule's58-degree limit, maximum67.94032degrees; wider support reaches63.58842degrees. The current fixture retains all119 earlier checks and adds7 checks, of which four fail on this actual recorded geometry. No slope, footprint, width or bank threshold was weakened.

## Three unsuccessful implementations

| Candidate | Focused verdict | Recorded result | Reason retired |
| --- | --- | --- | --- |
| [Receipt.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_053853_845_bridge_ownership/Receipt.json) |126PASS |4770/5494 fitted/support vertices; native footprint queries supported | Excessive repeated-cut geometry; no Play publication |
| [Receipt.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_054323_672_bridge_ownership/Receipt.json) |2FAIL |4188/4958 vertices;588/797ms construction; near90-degree faces | Failed slope and construction budget |
| [Receipt.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_054647_812_bridge_ownership/Receipt.json) |4FAIL |184/184 vertices;3.32/3.12ms construction; recorded faces and rays within26.15degrees | Four existing paired-width checks fail: support rises above tread, maximum approximately0.09912m |

All are Edit Mode synthetic/native-collider checks, not streaming certification or repaired ordinary traversal. The first functional PASS was not an acceptable production result because of its geometry expansion. No candidate was played. The third attempt ends this repair loop under the project three-attempt rule.

## Safe retirement and fresh evidence

[Retirement.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_grade_candidate_retirement/Retirement.json) preserves the failed candidate source as a text evidence file and removes only its new polygon-partition machinery. Original union geometry, centerline constraints, earlier collision repairs and terrain replay repair remain. No scenes, assets, GUIDs, schemas, packages, accepted records or save owners changed.

[CompilationDiagnostics.txt](../outputs/G08_EnvironmentCohesion_20261003/20261004_grade_candidate_retirement/CompilationDiagnostics.txt) records fresh successful Unity compilation. [Receipt.json](../outputs/G08_EnvironmentCohesion_20261003/20261004_055250_795_bridge_ownership/Receipt.json) identifies the current source and assemblies: runtime 408b3d45-3134-49f1-9f2b-e366065eeb3a, Editor f3080c83-0bca-4638-925d-f7690dd6d1dc, bridge SHA256 993cd0d1bd2099e9d38182a006ba26a5addf69de65bdcf660a65d039f50f44f9. **126checks, four expected unresolved slope/footprint failures; all119 earlier checks pass.** Captured fitted geometry again matches exactly. This source is compiled but does not repair the footprint defect. The preceding runtime traversal is historical source-scoped evidence, not fresh current-assembly acceptance.

## Remaining and next decision

EC06-D07 blocks safe crossing acceptance. Before another implementation loop, review one coherent height field shared by the fitted tread and wider support while retaining original anchors, full union width, bank planes, manifold topology,58-degree capsule limit and bounded construction. The approximately0.099m support protrusion must be reproduced and resolved alongside the recorded steep faces. Do not start a fourth ad hoc repair or hide a failed check. Independent library reconciliation and references can continue.

The requested worldwide ecology, eligible-library production integration, four-world coverage, both cameras, reconstruction/mutations, final affected R1/R2, locked-target performance and original-Skyrim-era visual floor remain incomplete. All15 EC verdicts remain NOT VERIFIED.
