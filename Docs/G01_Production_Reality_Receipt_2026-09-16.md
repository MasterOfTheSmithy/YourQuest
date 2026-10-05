# YourQuest G01 — Production Reality Receipt

Revision: 2026-09-16  
Status: COMPLETE FOR G01 — discovery map, non-ambiguous diagnostic instrumentation, fixture procedure, and owner-assigned evidence ledger are established. Fresh Play Mode execution of the current checkout is explicitly NOT YET TESTABLE because the running Unity editor has no targetable desktop window in this session.

## Source and environment identity

- Project: `C:/Users/Garri/YourQuest`
- Source: `HEAD=49a10bc`, working tree uncommitted; pre-existing project changes were retained and are included in the source-state snapshot.
- Diagnostic build identity: `yourquest-beta-g01-2026.09.16`
- Unity: `6000.3.2f1` (`ProjectVersion.txt`)
- First enabled production scene: `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`
- Second enabled scene: `Assets/Assets/Scenes/YourQuest_TitleEnvironment.unity`
- Disabled asset test scene: `Assets/Assets/Scenes/YourQuest_AssetTest.unity`
- Canonical fixture profile: `beta-dev-canonical`
- Expected fixture seed: `76603739`
- Save schemas: `player=6;world=6`
- World generation: `spatial_world_plan_v1_causal|generated_terrain_v4_biome_tiles`
- Semantic chunk contract: `continuous_world_cell_v5|continuous_edge_v3`

The source marker is intentionally a working-tree identity, not a release claim. The archive's planning documents remain instructions for later goals; this receipt records only G01 evidence.

## Changed files in this G01 pass

- `Assets/Assets/Scripts/Generated/YQProductionBaselineDiagnostics.cs`
- `Assets/Assets/Scripts/Generated/Editor/YQProductionBaselineRegression.cs`
- `Assets/Assets/Scripts/Tutorial/YQProfileSaveSystem.cs`
- `Docs/G01_Production_Reality_Receipt_2026-09-16.md`
- `G01_PureRegression.log`

The observer and lifecycle counters are instrumentation only; no gameplay authority, serialized schema, scene reference, accepted save, or imported asset was migrated.

## Authority and compatibility map

| Boundary | Current authority | Evidence / classification |
|---|---|---|
| Production startup | `YourQuestTutorialAutoBootstrap` | `EditorBuildSettings.asset` selects PlaySafe first; source verified active production bootstrap. |
| Title/profile selection | `YQTitleScreenUI` → `YQProfileSaveSystem` | Source verified. Profile save/load counters now expose completed lifecycle observations only in memory. |
| Player state | `PlayerStateManager.state` | Source verified active owner. No migration performed. |
| World state | `WorldStateManager.State` | Source verified active owner. No migration performed. |
| Profile copies | `YQProfileSaveSystem` | Source verified profile-owned player/world copies plus recovery copies; paired transaction migration belongs to G02. |
| Authoritative player | `YQInvestorPlayerMotor.ActiveMotor` | Source verified bootstrap rejects duplicate player/motor paths. `PlayerProfile` remains a compatibility component. |
| Origin | `GeneratedRpgContentService` plus `YQOriginGenerationService` | Source verified accepted origin fields are persisted and reused. Canonical fixture origin is preaccepted and must not represent ordinary creation. |
| World plan | `YQWorldGenerationService` | Source verified deterministic fallback is persisted; optional LLM replacement is bounded. |
| Spatial authority | `YQWorldGenerationArchitecture` / `YQSpatialPlanVersionRouter` | Source verified `V2Preferred` chooses accepted V2 or persisted V1; it does not mix them. |
| Physical materialization | `YQGeneratedWorldRuntimeBuilder` | Source verified `CompiledWorld` is the active path; a streamer alone is not accepted materialization. |
| Continuation streaming | `YQPlayerFollowingSemanticChunkStreamer` | Source verified sole streamed terrain/semantic owner. Stress performance remains G06/G07 work. |
| Population | Persisted generated NPC plans materialized by the runtime builder | Source verified. Historical fixture evidence used deterministic fallback NPC batches. |
| Quests | Structured `QuestRecord` and `YQQuestCompletionDirector` | Source verified explicit objective evaluator; downstream reward/objective hardening belongs G12. |
| Dialogue | `NpcDialogueAgent` / `DialogueThinkService` and profile-scoped stores | Source verified. Typed command/reaction work belongs G12/G14. |
| Progression | `ProgressionThinkCycle` → `ProgressionDecisionApplier` | Source verified. `PlayerProfile` fields remain compatibility callers. |
| LLM scheduling | `LLMClient` and `YQLlmRequest` | Source verified queue/category/priority/terminal result surface. Current configured backend was not exercised in this session. |
| Adaptive world | `LLMThinkCycle`, `DirectorThinkCycle`, `WorldDeltaApplier` | Source verified existing path; `DirectorThinkCycle` is historically named and remains compatibility infrastructure. |

### Compatibility callers retained

`PlayerProfile` is still referenced by production bootstrap compatibility wiring, `ProgressionDecisionApplier`, `UpgradeOfferManager`, `SkillCommiter`, debug skill generation, and editor/prototype scene builders. It was not migrated or deleted in G01. `DirectorThinkCycle` / `DirectorDecisionApplier` remain referenced by their runtime/editor coordination and prototype construction paths. These are recorded for G02/G03/G15 ownership review; no parallel authority was introduced.

## Diagnostic result semantics

`YQProductionBaselineDiagnostics` remains a read-only observer. It now reports build/source/schema identity, selected spatial artifact hash, model runtime state, active/required service counts, profile/seed, materialization, terrain, streaming, current chunk, failed chunks, and completed profile load/save counts.

`YQProductionBaselineRegression` now emits separate structured records:

| Check ID | Meaning |
|---|---|
| `pure.contracts` | Existing editor production-slice fixtures only. |
| `live.snapshot` | A runtime observer snapshot was actually captured. |
| `fixture.startup` | The preaccepted canonical fixture reached its accepted playable boundary. |
| `ordinary.startup` | An ordinary profile reached the normal title → profile → origin → world boundary. |
| `live.traversal` | Authored and streamed cell traversal was observed. |
| `live.reload` | The same profile loaded successfully at least twice. |

Every record carries `PASS`, `FAIL`, `BLOCKED`, or `NOT_YET_TESTABLE`, an evidence level, expected/actual text, reproduction steps, and an owner. Running the menu outside Play Mode no longer produces a runtime PASS; it emits `live.runtime = NOT_YET_TESTABLE`.

## Fixture preparation and reproduction

The reset menu remains intentionally Play Mode-only. This is the required preparation session:

1. Close any Play Mode session and wait for Unity imports/compilation to finish.
2. Open the first enabled scene, `Assets/Assets/Scenes/YourQuest_PlaySafe.unity`, and enter Play Mode.
3. Use `YourQuest > Beta Baseline > Reset Canonical Dev Profile` once. This deletes and recreates only `beta-dev-canonical`; it does not touch ordinary profiles.
4. Wait through title, profile, origin/loading, and world materialization. Do not use a test scene, developer teleport, or ordinary-profile reset.
5. Capture the `[YQProductionBaseline]` report and run `YourQuest > Beta Baseline > Run Production Regression`.
6. Save, return to title, Continue the same fixture, capture `live.reload`, then cross an authored and streamed boundary for `live.traversal`.
7. Exit Play Mode before any further edits or recompilation.

`Ensure Canonical Dev Profile` is the non-destructive alternative when the fixture should be reused. The canonical fixture's accepted origin and deterministic NPC fallback make it a baseline aid, not evidence for ordinary creator/model acceptance.

## Current result ledger

| Check | Status | Evidence | Exact observation / reproduction | Owner |
|---|---|---|---|---|
| PlaySafe serialized startup selection | PASS | STATIC_VERIFIED | `EditorBuildSettings.asset` selects PlaySafe first and title environment second. | G01 |
| Bootstrap authority construction | PASS | SOURCE_VERIFIED | Bootstrap creates the profile/state/world/LLM/origin/progression/dialogue/quest services and selects one player motor. | G01 |
| Pure production-slice contracts | BLOCKED | NOT_VERIFIED | A separate Unity batch editor exited with code 1 before invoking `YQProductionBaselineRegression.RunProductionRegression` because the original project editor process remained open; see `G01_PureRegression.log`. | G01 |
| Runtime snapshot records | NOT_YET_TESTABLE | NOT_VERIFIED | Unity process exists, but no Play Mode observation could be captured in this session. | G01 |
| Ordinary title → profile → origin → world | NOT_YET_TESTABLE | NOT_VERIFIED | No ordinary profile was reset or altered; fresh ordinary-flow run remains to be performed. | G10, rerun by G01 |
| Canonical fixture startup | NOT_YET_TESTABLE | NOT_VERIFIED | Preparation procedure is documented; current session did not enter the Unity UI. | G01 |
| Traversal and streamed boundary | NOT_YET_TESTABLE | NOT_VERIFIED | No current Play Mode traversal was performed. Historical evidence is listed separately below. | G06-G08 |
| Save/quit/reload/revisit | NOT_YET_TESTABLE | NOT_VERIFIED | Lifecycle counters are instrumented, but no current reload was performed. | G02 |
| V2Preferred selection and materialization distinction | PASS | SOURCE_VERIFIED | Router and builder expose accepted authority and `CompiledWorld`; streamer readiness is reported separately. | G05-G08 |
| LLM configured/fallback mode | BLOCKED | SOURCE_VERIFIED | `LLMClient` exposes runtime state/model/pending/failure data, but the current backend was not exercised. | G03 |

### Historical runtime evidence, not a current rerun

`Docs/Production_Architecture_Beta_Baseline_2026-09-15.md` records a prior canonical run with seed `76603739`, compiled-world materialization, four canonical NPCs, zero missing-ground/route samples, and 181 semantic records preserved through save/reload/rebind. The same report records streaming and persistence timing failures: 300 m/s forward production `2.20` cells/s, diagonal minimum lead `0`, readiness P95/max `6.480/6.491s`, one missed deadline, a `0.168s` synchronous slice against `0.050s`, and `0.111s` persistence against `0.100s`. Those results remain historical and are assigned to G06/G07 and G02; they are not promoted to current PASS.

## Initial beta measurement proposal

This is a frozen G01 proposal for later calibration, not a release gate:

- Reference environment: Windows 11 64-bit, Unity `6000.3.2f1`, current development machine with 32,648 MB reported physical memory. GPU/CPU model and supported hardware tier remain to be recorded before G19.
- Primary capture: PlaySafe production startup, ordinary profile, 1920×1080 windowed capture, production quality settings, fresh profile and one canonical fixture run.
- Normal movement: measure the actual configured player speed and test supported travel separately from stress probes.
- Stress probes: retain 150 m/s and 300 m/s as diagnostic stress labels only; they are not supported-player requirements and must not be tuned in G01.
- Provisional budgets to measure later: no missing current-cell ground/collision, no failed required chunk, synchronous slice ≤50 ms, profile/world persistence commit ≤100 ms, and bounded service/queue/resource counts. G19 owns final thresholds and calibration.

## Defect and handoff ledger

| ID | Evidence / reproduction | Severity | Owner | Blocked consumers | Next test |
|---|---|---:|---|---|---|
| G01-RT-001 | Current Unity process is live, but desktop automation returns no targetable Unity window; no Play Mode receipt can be captured. | P1 evidence gap | G01 | G01 current runtime receipt | Reopen PlaySafe in a targetable Unity window; run pure, fixture, ordinary, traversal, and reload records once. |
| G01-ED-001 | Batch verification of `YQProductionBaselineRegression.RunProductionRegression` exited code 1 before method output while the original Unity editor remained live; `G01_PureRegression.log` contains the exact invocation and terminal state. | P1 evidence gap | G01 | Pure-contract receipt | Close or expose the existing editor, then rerun the menu entry once in a single project owner. |
| G06-STREAM-001 | Historical deep harness: 300 m/s forward rate 2.20 cells/s, diagonal lead 0, readiness P95/max 6.480/6.491s, one missed deadline, 0.168s synchronous slice. | P1 | G06/G07 | Traversal and long-session acceptance | Rerun on supported-speed proposal and four-seed suite after owner repair. |
| G02-SAVE-001 | Historical persistence timing 0.111s against provisional 0.100s; paired profile/world commit boundaries remain sequential source paths. | P1 | G02 | Profile-switch/reload certification | Fault-inject copied fixture writes and verify complete revision recovery. |
| G03-LLM-001 | Runtime instrumentation can report model state and failures, but current backend/model acceptance was not exercised; canonical fixture intentionally uses deterministic fallback population. | P1 evidence gap | G03 | Ordinary generated-content acceptance | Run configured model path and unavailable-model continuation on separate ordinary profiles. |
| G04/G08-ASSET-001 | Existing reports distinguish catalog presence from approved playable construction; historical published-library readiness remains unresolved. | P1 | G04/G08 | Site/settlement acceptance | Run approved asset reachability and physical function checks in production materialization. |
| G02-LIFE-001 | `PlayerProfile` and historically named director callers remain compatibility paths; no second owner was introduced, but retirement proof is absent. | P2 | G02/G03 | Future migrations and lifecycle cleanup | Trace reached callers during profile switch/shutdown and define retirement conditions. |

## Verification record

- COMPILE VERIFIED: `dotnet build YourQuest.slnx --no-restore` completed with 0 errors and 1 pre-existing `CS0162` warning in `YQWorldGenerationV2ContractTests.cs:249`.
- STATIC VERIFIED: enabled scenes, bootstrap construction, state/profile owners, spatial router, materialization path, origin/population/quest/dialogue/progression/LLM symbols inspected.
- SOURCE VERIFIED: diagnostic observer and profile lifecycle counters are read-only/in-memory; no canonical save reset or accepted content mutation was performed by instrumentation.
- EDITOR/TOOL VERIFIED: the second Unity batch invocation was attempted and recorded its code-1 terminal result in `G01_PureRegression.log`; it did not execute the target method.
- NOT VERIFIED: current Unity Play Mode behavior, current pure-menu execution, ordinary profile creation/origin, current traversal, and current save/quit/reload/revisit.

G01 discovery is complete as a documented and instrumented boundary only when the NOT_YET_TESTABLE runtime receipt is preserved. Downstream failures are not silently repaired or counted as PASS.
