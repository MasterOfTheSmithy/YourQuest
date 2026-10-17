# Spell circles verification — October 2, 2026

The saved-world startup blocker is repaired. Fresh compilation, detached circle contracts, normal PlaySafe startup, and the Circle 1 live casting harness **PASS**. This file retains its original blocker filename so existing references remain valid.

## Changed behavior

Spells use circles 1–7; ordinary skills retain their tiers. Casting displays exactly one animated ring per circle. One shared formula ties duration to circle, mana cost, and delivered strength. Resources and cast counters commit only on successful release; interruption removes the rings without spending resources. The spell inspector groups by circle and shows circle, rank, cost, strength, and cast time together in the compact menu.

The persisted `tier` field still stores a spell's circle. Existing accepted IDs and records require no migration or regeneration.

## Startup prerequisite and senior review

The accepted road in chunk `(0, 8)` overlapped the padded cell by about 0.209 metres. Its long source segment put both clipped endpoints within the old normalized-parameter deduplication threshold of `0.0001`, dropping one endpoint and leaving no physical span. The production sampler now removes exact duplicate parameters; the existing world-space duplicate check continues to reject physically coincident vertices. This retains the accepted structural demand rather than bypassing the startup gate.

Focused senior review confirmed unchanged seed derivation, accepted blueprint/hash validation, route width, spatial authority, cell bounds, and runtime ownership. All **21 detached geometry checks PASS** at `2026-10-02T10:19:31.5972372Z`: [startup prerequisite receipt](Spell_Circles_Startup_Prerequisite_Receipt_2026-10-02.json). They use the saved seed `622eff02` and cover neighbouring seam endpoints, traversal-order independence, road/water triangles, finite vertices, rejection of zero-length geometry, unchanged detached blueprint hash, and unchanged saved-world bytes during the check. This Edit Mode receipt is geometry evidence only.

## Fresh spell evidence

- **Compile and detached contracts — PASS.** Unity 6000.3.2f1 compiled the final spell patch with no compiler errors. All **38 circle checks PASS** at `2026-10-02T10:27:39.8350071Z`: [contract receipt](Spell_Circles_Contract_Receipt_2026-10-01.json). Checks cover legacy save compatibility, spell-only limits, independent timing contributions, and ring count/charge for every circle 1–7. Tested loaded assembly SHA-256: `8f26f9af44ceedc712b9b8595925e01d6fd4f15520273cb378ce47c7375b3a30`.
- **Normal saved-world startup — PASS.** Visible title-screen selection and Continue loaded existing profile `85e6507c43004c39af986702206a1d49`, **G08-R2 Speed260 Disposable**, in PlaySafe. Fresh diagnostics reported gameplay presentation released, input available, one listener, and zero failed chunks. No G08 suite or high-speed traversal was run.
- **Live casting harness — PASS.** All **17 checks PASS** at `2026-10-02T10:31:42.9403757Z` on that tested assembly: [runtime receipt](Spell_Circles_Runtime_Receipt_2026-10-01.json). The already equipped, accepted **Invoke Starward Sign** (`e71e9e91b5cf429499e4f4165bb17ca4`) is Circle 1, rank 1, 15 mana, strength 22, and casts in 0.71 seconds. The witness checks one authoritative player, wind-up timing, one visible ring, no early spending/counters, one resource payment on release, Alt-menu interruption without payment, removal of interrupted rings, and unchanged accepted spell records/IDs. [First-person casting capture](Spell_Circles_Live_Cast_2026-10-01.png).
- **Visible spell menu — PASS with preparation assistance.** The verification shortcut opened the existing menu owner. A real mouse click on Skills then showed `Spells • Circle 1` and the selected spell's circle, rank, 15-mana cost, strength 22, and 0.71-second cast time together. Opening the menu through the shortcut is not an unassisted menu-opening claim.

## Preservation and limits

No spell was granted or regenerated, no accepted spell record was rewritten, and world readiness was not bypassed. Only the existing spell-bearing disposable profile was used for casting. Normal gameplay counters/resource changes remain owned by the existing player/save systems.

After testing, visible title-screen selection and Continue restored the user's previously selected **New Adventurer**, profile `1df9084b1a254ea5be0c4c030105ad7e`. A read-only manifest check confirmed that active ID, and Unity was returned to Edit Mode. This is selected-profile restoration evidence, not a new startup certification for that profile.

Shared source changes after the witness triggered another successful Unity compilation. The assembly observed during restoration was `1c13318db21c012c876c661e58b617167608d3210bd70b2ca8ec1ede8e9fdbc4`. The circle rules, combat, spell menu, and repaired road sampler still matched their tested source hashes. The 17-check live PASS applies to the hash recorded in its receipt; it does not certify subsequent shared integration. Rerun the same live menu operation after the other changes settle if certification of that combined build is required.

Live release/interruption was exercised through queued Input System events. Ordinary unassisted casting and third-person presentation remain unverified by this harness. Higher circles have detached ring/timing coverage; no higher-circle spell was inserted into a save to manufacture live evidence.
