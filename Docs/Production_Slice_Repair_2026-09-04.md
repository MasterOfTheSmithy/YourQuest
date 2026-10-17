# Production slice repair — 2026-09-04

This bounded pass follows the supplied one-shot brief, the main GDD, and the existing vertical-slice requirements. Changes are local and unpushed. No desktop control, model request, or player-save mutation was used for verification.

## Repaired

- **Actual startup quest loss:** `SeedData` called legacy tutorial reseeding after the accepted origin. Without its old tutorial-version marker, that code removed `tutorial_main` quests, cleared equipment/dialogue/combat counters, and inserted fourteen fixed, objective-less demo quests. This path is retired. Bootstrap now preserves accepted quests, existing event evidence, completion state, and the user's active selection.
- **Previously lost origin quest:** If its accepted origin record remains but its quest was deleted, bootstrap restores the recorded quest name and available description with the existing three structured onboarding objectives. The recovery ID is stable; repeated startup does not duplicate it. Existing quests are not regenerated, nor are items or rewards re-awarded.
- **Delayed origin ownership:** Origin replies cannot change a different/reloaded player, retired questionnaire session, or already-accepted origin. The offline fallback uses the same single-use gate.
- **Continue corruption preflight:** Both selected-profile documents must deserialize before shared saves or live managers are replaced. Only that profile's backup is considered. Corrupt world JSON no longer silently reaches the default-world path through this entry point. This is validation-before-load, not a claim of an atomic two-file filesystem transaction.
- **Cross-profile saving:** Saving the active character into a different selected profile is rejected. New-profile activation failure no longer reports successful startup.
- **Quest steps:** Individual completed objectives trigger persistence even before the whole quest is complete. Explicit NPC/enemy/item targets cannot be bypassed by broad prefixes, and colon-delimited IDs do not confuse `npc_1` with `npc_10`. Invalid null-only objectives do not complete quests.
- **NPC conversation ownership:** Transcripts and long-term memory now live beneath the owning profile. NPCs rebind their in-memory session when player state changes. Overlapping submissions are blocked; unload/reset/profile change invalidates late replies and further repair attempts.
- **Quest guidance:** The world marker and its minimap consumer follow the current structured objective's exact entity/region ID. No prose-based target scoring or invented point ahead of the player is used. Non-spatial steps and unloaded targets do not show a fabricated destination.

## Improved

- Goddess prompts now follow the latest brief's restrained, ancient, observant voice rather than compulsory frantic incompetence or construction checklists. Short direct speech survives validation; a rigid greeting and an obligatory first-person pronoun in every field no longer cause wholesale replacement with fallback prose.
- Fallback lines are shorter and avoid several implementation jokes and unsupported claims that terrain/foliage are already finished. Generated prose remains primary. No live model audition was performed.
- Long questionnaire prompts contain an indexed excerpt of every supported answer, rather than up to 80,000 raw answer characters. Original answers and their full deterministic seed input are preserved. Recent ledger excerpts are bounded too.
- Missing quest targets use negative caching; non-spatial steps skip scene scans. The runtime quest marker releases its owned material when destroyed.

## Architectural changes and compatibility

The substantive change is retiring the competing demo tutorial seeder in favor of accepted quest records and the existing objective evaluator. No new quest authority, world generator, player, inventory, or LLM scheduler was introduced.

Save schemas and serialized references remain compatible. Profile-scoped dialogue paths intentionally do **not** automatically import the old shared transcript/memory files: those files have no reliable character ownership. They remain untouched for explicit migration if their owner can be established. This means previous unscoped conversation history is not automatically shown.

## World and asset work

Existing streaming, grounding, material, actor-visibility, and reviewed-provider contracts remain enforced. The real furnished eastern candidate was rechecked with imported bed/table/chair/chest geometry and the existing door/storage providers. No imported materials or prefabs were replaced, and no pending cell was promoted.

The current content limitation remains significant: the twelve reviewed settlement kits are exterior shells; the first furnished candidate still needs actual terrain approaches, remaining service providers, and rendered review. Passing code contracts does not make those kits finished functional settlements or establish the requested terrain/foliage quality.

## Verification

- Initial hidden Unity compilation and 67 regressions passed; the real candidate's repeat authoring and geometry/provider checks also passed.
- Final hidden Unity compilation and **68/68 tests passed**, including a direct call to the actual bootstrap quest routine covering first-start preservation, active selection, lost-origin recovery, counter retention, and completed-origin non-regeneration. Evidence: `Logs/Codex_ProductionSlice_Final.log`.
- The same final run passed the imported furnished-cell checks: floor support 187/187, reachable standing points 91/91, all three access sockets supported/clear/reachable, door and storage-provider initialization, and byte-identical repeated prefab/manifest authoring. Evidence: `Logs/V2_CandidateInteriorReadiness.md`. The pre-existing unreachable-code warning in the contract test harness remains; no compiler errors occurred.
- Tests use synthetic player/world records and isolated disposable disk fixtures, not the user's profiles. Save-schema round-trips test active quest and partial objective persistence.
- Static inspection traces title/profile activation → questionnaire → origin commitment → bootstrap services/player → world materialization gate → presentation release. The production bootstrap owns the player; this pass adds no player/controller/camera.
- No full new-save/Continue gameplay session, rendered material review, Vey interaction, real loot/save/reload sequence, lighting/audio audition, or frame-time capture was performed. Zero spikes/hangs and overall RPG visual quality are not verified.

## Remaining priorities

1. Finish and visually approve functional settlement content and actual terrain connections before promotion; do not bypass the gates to hide empty-world symptoms.
2. Play-test a new origin, Vey interaction and first quest, then Continue with a different active quest selected. Verify grounded streamed sites and canonical inhabitants.
3. Capture loading and streaming frame timings, and audition the revised Goddess prompts with the installed model. Headless tests cannot establish presentation quality or a maximum frame time.
4. The current bootstrap presents the questionnaire at the title/Goddess stage before constructing gameplay, rather than beginning the questionnaire inside the hut described by the GDD. This pass does not change that scene/camera sequence.
