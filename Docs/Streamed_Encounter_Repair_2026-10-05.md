# Streamed encounter repair — 2026-10-05

## Changes

- Frontier inference binds profile/world identity, generation epoch, the accepted base artifact, candidate geometry and canonical region/faction inputs. Walking and unrelated world counters no longer discard a completed location proposal.
- Settlement conformance probes retain two metres of flat floor at reserve edges, inside the existing grading shoulder. Earthwork, water clearance, protected terrain and route admission limits remain enforced.
- Every configured base settlement/encampment receives an NPC population batch. Existing accepted characters are retained and only missing slots are requested.
- NPCs and humanoid enemies equip generated approved weapons/offhand items through `WorldState.containers`. Equipped references point to the same physical items used for hostile corpse loot. Friendly inventories are owner-only. Streaming cannot reroll accepted or looted equipment.
- Editor gear validation resolves the same approved prefab GUID binding as the build. Organized DOT prefab paths had caused valid weapons to fall back to supply items.
- New frontier hostile briefs choose a family supported by the actual installed creature binder. NPC appearance prompts include available approved races. Existing accepted families and identities remain unchanged.
- Road terrain paint/grades, accepted road terminal sampling, remote accepted construction pads, streamed area-water meshes/basins, ecology publication and deterministic mountain sampling were repaired in the existing authorities.
- Newer profile recovery publishes a complete paired revision while retaining its auxiliary documents. Only this recovery patch was committed from the otherwise modified save-system file.
- Progression inference freezes player position values using the existing Unity JSON converters. Travel had exposed a recursive `Vector3.normalized` serialization exception that interrupted those model requests.
- Actor activation recovers existing equipment presentation references so domain reload or streaming activation cannot duplicate visuals.

## Preserved contracts

The save schema remains 8. Existing container enum values 0–6 remain unchanged; NPC is appended as 7. Continuous-world cell/edge contract names, canonical seed, base V2 artifact, accepted site identities, authored assets/GUIDs and the player/stream/save/LLM authorities are preserved. No save reset, forced site admission or teleport was used.

## Fresh evidence

- Unity compilation passed.
- Focused detached streaming/construction contracts: **237/237 PASS**.
- Detached ownership, missing population coverage, NPC inventory, activation, progression snapshot and real equipment/family binding checks: **15/15 PASS**. All eight equipment sample seeds produced real approved weapons. Combined final result: **252/252 PASS**, owned Unity batch exit 0.
- The original active profile snapshot admitted 13 of 38 sampled frontier opportunities through the production physical planner.
- Production PlaySafe Continue accepted and persisted settlements, POIs and hostile sites after the stale-revision repair. A subsequent Continue reloaded accepted continuation providers and six equipped NPC inventories. The original base V2 hash remains `b4da47a6ac392ffd` for world seed `03c669fa`.
- The final saved continuation contains **17 settlements, 15 POIs and 15 hostile sites**. Eight base NPC identities remain accepted, including the original four; six NPC inventories contain equipped items.
- The third assisted motor traversal delivered actual keyboard/mouse input and crossed approximately 919 metres of streamed terrain. Its last sample confirmed the target POI owner loaded with enabled renderers and solid colliders at 663 metres from the player. No teleport, speed override or forced admission was used. The twelve-minute overall bound expired before close encounter; its result remains **FAIL**, with no site categories counted as visited.

Receipts: `outputs/YourQuest_Tandem_20261004/frontier-continuation-verification/20261005_074018_138.json`, `outputs/StreamedRepair_20261005/RepairContracts.json`, `DetachedPlacement.json`, `TravelResult.json`, and `TravelWitness.jsonl`. Logs: `Docs/Streamed_Spawn_Repair_Final_2026-10-05.log` and `Docs/Streamed_Travel_Repair_Third_2026-10-05.log`. Earlier bounded travel failures are retained in `CurrentProfileBeforeTravel` and `TravelAttempt2`.

## Verification limits

Detached checks establish contracts and placement eligibility, not visible gameplay. The initial automated travel attempts admitted saved sites but delivered no movement because headless Editor input routing suppressed the test keyboard. The third attempt corrected input routing and confirmed POI materialization on approach, but neither close encounters across all three categories nor visible equipped residents/enemies were established. The final progression repair is compile/fixture verified, not rerun in ordinary gameplay.

The existing protected opening collar is 1,152 metres; new frontier construction cannot alter it. The nearest frontier POI in this run was approximately 1.58 kilometres from startup. No global site count cap was introduced or removed from accepted records.

Full visual approval of river/lake surfaces, paths/grass, mountain scale, door interaction, fitted armor and unassisted human encounters is not established by these checks. The historical performance receipts are not a fresh performance PASS.

## Playtest handoff

Use the existing profile's Continue in `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`. No reset or New Journey is needed. The owned verification Editors have closed. Main repair commit: `c9ddb29`; the final follow-up covers actor activation, progression serialization, and this bounded travel fixture/receipt. Unrelated working-tree changes remain untouched.
