# YourQuest Production Architecture and Beta Baseline

Status: PARTIAL — the authoritative runtime path is locked in code and the clean canonical fixture reaches playable compiled-world gameplay; semantic save/reload/rebind integrity passes independently, but the deep continuous-streaming and persistence-budget gates remain red.

## Authoritative system map

| Responsibility | Active production authority | Evidence | Classification |
|---|---|---|---|
| Startup / profile selection | `YourQuestTutorialAutoBootstrap` → `YQTitleScreenUI` → `YQProfileSaveSystem` | `Assets/Assets/Scripts/Tutorial/YourQuestTutorialAutoBootstrap.cs`, `YQTitleScreenUI.cs`, `YQProfileSaveSystem.cs` | Active Production |
| PlayerState | `PlayerStateManager.state` | `Assets/Assets/Scripts/Data/State/Player State/PlayerStateManager.cs`, `PlayerState.cs` | Active Production |
| WorldState | `WorldStateManager.State` | `Assets/Assets/Scripts/Data/State/World State/WorldStateManager.cs`, `WorldState.cs` | Active Production |
| Origin data | `PlayerState.generatedOrigin`, origin questionnaire/generation services | `YQOriginQuestionnaireUI.cs`, `YQOriginGenerationService.cs`, `GeneratedRpgContentService.cs` | Active Production |
| World generation | `YQWorldGenerationService.EnsureWorldPlan` with deterministic fallback; optional LLM replacement is bounded and persisted | `Assets/Assets/Scripts/Generated/YQWorldGenerationService.cs` | Active Production |
| Accepted spatial authority | `YQWorldGenerationArchitecture` + `YQSpatialPlanVersionRouter`; configured `V2Preferred`, selecting one complete accepted V2 artifact or persisted V1 | `YQGeneratedWorldRuntimeBuilder.cs`, `YQWorldGenerationV2Contracts.cs` | Active Production |
| Runtime materialization | `YQGeneratedWorldRuntimeBuilder` using `CompiledWorld` | `Assets/Assets/Scripts/Generated/YQGeneratedWorldRuntimeBuilder.cs` | Active Production |
| Continuous streaming | `YQPlayerFollowingSemanticChunkStreamer` backed by `YQContinuousWorldCellAuthority` and persisted semantic chunk records | `Assets/Assets/Scripts/Generated/YQPlayerFollowingSemanticChunkStreamer.cs`, `YQContinuousWorldCellAuthority.cs` | Active Production |
| NPC population | Persisted `GeneratedNpcPlanRecord` records materialized by `YQGeneratedWorldRuntimeBuilder`; optional planning owned by `YQGeneratedNpcPlanningService` | `WorldState.cs`, `YQGeneratedWorldRuntimeBuilder.cs`, `YQGeneratedNpcPlanningService.cs` | Active Production |
| Combat | `YQInvestorPlayerMotor` + `YQInvestorCombat` / `YQInvestorEnemy` | `Assets/Assets/Scripts/Tutorial/YQInvestorPlayerMotor.cs`, `YQInvestorCombat.cs`, `YQInvestorEnemy.cs` | Active Production but Historically Named |
| Inventory / equipment | `PlayerState` item/equipment records; `YQPlayerEquipmentVisual` presents the authoritative player state | `PlayerState.cs`, `YQPlayerEquipmentVisual.cs`, `GeneratedRpgContentService.cs` | Active Production |
| Progression | `ProgressionThinkCycle` → `ProgressionDecisionApplier`, persisted offers/skills/titles/quests in `PlayerState` | `ProgressionThinkCycle.cs`, `ProgressionDecisionApplier.cs` | Active Production |
| Quests | Structured `QuestRecord` / objective records evaluated by `YQQuestCompletionDirector` | `YQQuestCompletionDirector.cs`, `YQActiveQuestWorldHighlight.cs` | Active Production |
| Dialogue | `NpcDialogueAgent` + `DialogueThinkService`; profile-scoped memory/session stores | `NpcDialogueAgent.cs`, `DialogueThinkService.cs`, `NpcDialogueMemoryStore.cs`, `NpcDialogueSessionStore.cs` | Active Production |
| LLM scheduling | `LLMClient` request queue and category/priority contracts | `Assets/Assets/Scripts/LLM/LLMClient.cs`, `YQLlmRequest.cs` | Active Production |
| Adaptive world behavior | `LLMThinkCycle`, `DirectorThinkCycle`, `WorldDeltaApplier`, and generated NPC planning; all mutations land in persisted `WorldState` | `Gameplay/Systems/World`, `Gameplay/Systems/Director`, `YQGeneratedNpcPlanningService.cs` | Active Production / Historically Named |
| Persistence | `YQProfileSaveSystem` owns profile documents; `PlayerStateManager` and `WorldStateManager` own atomic active documents and recovery copies | `YQProfileSaveSystem.cs`, `PlayerStateManager.cs`, `WorldStateManager.cs` | Active Production |
| Player-facing UI | Title/origin/loading/pause/HUD systems created by `YourQuestTutorialAutoBootstrap` after the selected save and world are ready | `YourQuestTutorialAutoBootstrap.cs`, `YQTitleScreenUI.cs`, `YQStartupLoadingScreen.cs`, `YourQuestTutorialHud.cs` | Active Production |

## Production identity and diagnostics

The new `YQProductionBaselineDiagnostics` is created by the one startup bootstrap and logs:

- build version: `yourquest-beta-baseline-2026.09.15`
- save schema: `player=6;world=6`
- world generation: `spatial_world_plan_v1_causal|generated_terrain_v4_biome_tiles`
- semantic chunk schema: `continuous_world_cell_v5|continuous_edge_v3`
- materialization: `CompiledWorld`
- configured spatial mode: `V2Preferred`
- selected spatial authority: runtime-reported `AcceptedV2` or `PersistedV1`
- world seed and active profile identifier
- service, origin, plan, materialization, player, NPC, current terrain, streaming, and traversal state

The report is observational and does not regenerate saves or create a second authority. Duplicate diagnostics owners log an error and self-destruct.

The origin materialization boundary is now explicit: `origin_vey_witch_house` selects the reviewed structural showcase cell `yq_cell_witch_house_p1_p1_p00` only. Its 656 source instances are runtime-prefiltered to 641 coherent roots, producing 1,968 validated renderers and 602 enabled non-trigger colliders; the cell is within the 1,100-instance semantic budget and is not cloned wholesale as an unbounded fallback.

## Compatibility and deprecated map

| System/path | Decision | Classification |
|---|---|---|
| `YQGeneratedWorldRuntimeBuilder` legacy scatter branch | Compiled and retained for comparison, but `ActiveMaterializationPath` is `CompiledWorld`; production bootstrap never selects the legacy branch | Compatibility Layer / Deprecated runtime branch |
| `YQSpatialPlanVersionRouter` V1 fallback | Retained for existing saves and selected only when a complete accepted V2 artifact is unavailable under `V2Preferred` | Compatibility Layer |
| `YQInvestor*` naming | Runtime-owned player, combat, dialogue, and presentation code despite prototype-era naming | Active Production but Historically Named |
| `DirectorThinkCycle` and older director DTO path | Still referenced by editor/prototype construction and some runtime coordination; no deletion is justified yet | Compatibility Layer / unresolved historical path |
| `PlayerProfile` | Referenced by prototype scene construction and progression compatibility wiring; not the persisted player authority | Compatibility Layer |
| `YourQuestTestSceneRoot`, `PrototypeBuilder`, `YourQuestAssetTestSceneBuilder`, validation scene builders | Authoring/verification only; production bootstrap quarantines known test roots before presentation | Editor/Authoring Tooling or Verification/Test Infrastructure |
| `ScriptDump/*` and `ChatGPT_ProjectDump/*` | Documentation/export artifacts, not runtime sources | Confirmed Dead for runtime |

No files were deleted or renamed in this lock pass. Existing saves, scenes, prefabs, ScriptableObjects, reflection targets, and compatibility APIs were preserved because reference elimination has not been proven for every historical branch.

Removed/migrated files: none. No safe removal or persistent-schema migration was justified by the targeted reference checks completed in this pass.

## Duplicate authority decisions

- `PlayerStateManager` and `WorldStateManager` remain the only live state managers. `YQProfileSaveSystem` coordinates profile-owned copies but rejects saves where active profile and `PlayerState.playerId` disagree.
- `YourQuestTutorialAutoBootstrap` remains the only production runtime construction owner. It quarantines prototype/test roots, rejects duplicate player objects, and forces one `YQInvestorPlayerMotor` authority.
- `YQGeneratedWorldRuntimeBuilder` remains the only production world materialization coordinator. The active materialization enum is `CompiledWorld`.
- `YQWorldGenerationArchitecture` freezes the chosen spatial authority per build transaction; terrain, sites, routes, population, and streaming consume the same plan.
- `YQPlayerFollowingSemanticChunkStreamer` remains the only continuous terrain/semantic streaming owner. Its current-cell priority, bounded required-ring, rotating repair admission, cached predictive corridor repair, and near-term diagonal-axis repairs are present. The latest current-source run reached `300.0` m/s, completed unload/revisit, and preserved `181` semantic records through save/reload/rebind, but its stress gates still reported 300 m/s forward production `2.20` cells/second, diagonal minimum lead `0`, diagonal readiness P95/maximum `6.480`/`6.491` seconds, one post-traversal missed deadline, and a maximum synchronous slice `0.168` seconds; the owner is not yet beta-certified.
- The new diagnostics object is intentionally a read-only observer, not another manager.

## Reproducible beta-development fixture

Canonical fixture constants:

- profile id: `beta-dev-canonical`
- display name: `Beta Dev`
- world name: `YourQuest Beta Fixture`
- origin seed: `beta-origin-v1`
- direction: `wanderer`
- expected world seed: `76603739`

Exact procedure:

1. Open the first enabled production scene, `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`, and enter Play Mode.
2. Use `YourQuest > Beta Baseline > Reset Canonical Dev Profile`. This affects only the explicitly named canonical fixture profile.
3. Wait for the title/origin/loading flow to finish. Do not bypass the loading gate. Confirm the `[YQProductionBaseline]` log reports the canonical profile id, expected seed, `CompiledWorld`, and `spatialAuthority=AcceptedV2` or `PersistedV1`.
4. Confirm the world reaches `materialized=True`, the authoritative player is active, an NPC population is present, and current-cell terrain is valid.
5. Use the in-game Save action, return to title, select the same canonical profile, and Continue. Confirm the same profile id, world seed, accepted spatial authority, and current world materialization are reported after reload.
6. Move across at least one authored-cell boundary and one streamed continuation-cell boundary. Confirm `traversed=True`, no failed chunks, and no current-cell terrain readiness error.
7. Run `YourQuest > Beta Baseline > Run Production Regression` in Play Mode. The command runs the consolidated deterministic fixtures plus the live baseline observer.

To create the fixture without resetting its current save, use `YourQuest > Beta Baseline > Ensure Canonical Dev Profile`. A clean reset removes only the profile folder for `beta-dev-canonical` and recreates its profile-owned player/world documents through `YQProfileSaveSystem`; it does not touch other profiles.

Development-only fixture behavior: after the initial compiled-world transaction releases gameplay, the canonical profile uses the existing validated deterministic NPC fallback batches instead of issuing optional Ollama population requests. Ordinary profiles retain the normal LLM scheduling path.

## Regression entry point

`Assets/Assets/Scripts/Generated/Editor/YQProductionBaselineRegression.cs` is the top-level entry point. It consolidates the existing production-slice checks for earthworks/readiness, origin ownership and prompt bounds, Goddess voice, quest progress/persistence, quest markers, accepted-origin bootstrap, dialogue profile isolation, and profile preflight. In Play Mode it also validates the live service/profile/origin/plan/spatial/materialization/player/NPC/terrain/streaming path through `YQProductionBaselineDiagnostics`.

The existing `YQSemanticChunkRuntimeVerification` remains the deeper stress harness for sustained speed, unload/revisit, save/reload, deterministic semantic reconstruction, and rebind. It is not silently replaced by the smaller top-level entry point.

The stress harness records threshold-only streaming failures and continues through unload, save/reload, deterministic reconstruction, and rebind; unsafe current-cell traversal failures remain immediately fatal. This keeps the overall result honest while preventing one performance gate from hiding independent persistence evidence.

## Verification evidence

- COMPILE VERIFIED: `dotnet build YourQuest.slnx --no-restore` succeeded after restoring Unity-generated project assets: `Assembly-CSharp` and `Assembly-CSharp-Editor` built with 0 errors and one pre-existing `CS0162` warning in `YQWorldGenerationV2ContractTests.cs:249`.
- STATIC VERIFIED: existing `YQProductionSliceRegressionTests` contains passing readiness, origin, quest, dialogue-isolation, and profile-preflight fixtures; the new top-level entry point reuses those fixtures.
- RUNTIME/BEHAVIOR VERIFIED: fresh canonical-fixture runs reach playable compiled-world gameplay with `worldPlanSeed=76603739`, `materialized=True`, `PresentationReleased=True`, 2 settlements, 90 initial semantic chunks, 4/4 canonical NPCs, zero missing ground, and zero route-traversal issue samples. The origin stream itself passed with 1,968 renderers, 6 walkable surfaces, 602 enabled non-trigger colliders, and zero missing colliders.
- RUNTIME/BEHAVIOR PARTIAL: the latest current-source deep harness passes continuation symmetry, edge contracts, distant synthesis, generation-order independence, rapid coverage, normal traversal, 150 m/s traversal, 300 m/s straight traversal, 300 m/s direction-change traversal, far required coverage, unload, and revisit. The 300 m/s straight production-rate gate failed at `2.20` cells/second; the diagonal lead gate failed at minimum lead `0`, with readiness P95/maximum `6.480`/`6.491` seconds; the frame-budget gate failed at `0.168` seconds against `0.050`, with one post-traversal missed deadline. This remains the streaming blocker.
- PERSISTENCE VERIFIED IN A FAILED REGRESSION: the same real run saved and reloaded `181` semantic chunks with matching counts/signatures, rebuilt through `RebuildGeneratedWorld()`, rebound the streamer, and passed rebound required coverage, current terrain, and active physical chunk checks. The persistence commit measured `0.111` seconds against its `0.100`-second budget, so the timing gate also remains red; the overall result is honestly `FAIL`.
- ENVIRONMENT VERIFIED: Unity editor startup was repaired by moving the exact stale `CurlRequestCache.db` to a recoverable `.bak` and starting the configured Unity Hub/licensing client. Subsequent fresh Play Mode runs published heartbeats and completed world materialization.

## Current beta blockers

1. Continuous streaming is not at the completion gate: the latest current-source run records a 300 m/s straight production rate of `2.20` cells/second, diagonal minimum lead `0`, diagonal readiness P95/maximum `6.480`/`6.491` seconds, one post-traversal missed deadline, and a `0.168` second maximum synchronous slice against the `0.050` second frame budget. The result is not yet stable enough to certify.
2. Save/reload/rebind data integrity is verified, but the persistence timing gate measured `0.111` seconds against `0.100`; it is not a full regression PASS until the timing and streaming gates also pass.
3. The project still carries historically named compatibility and prototype/editor paths. They are quarantined or non-authoritative where proven, but not deleted because full scene/prefab/reflection/save-migration elimination has not been demonstrated.

## Completion decision

Do not mark this goal complete yet. The architecture map and baseline controls are established, but the requested completion gate requires a passing continuous-streaming regression, explicit save/reload/rebind success, and a clean fixture run that is not delayed by unresolved local generation service availability.
