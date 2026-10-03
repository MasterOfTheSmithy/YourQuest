# G02 State, Identity, and Transaction Receipt

Source identity: `HEAD=49a10bc36f2438373e9635b4fbe7092e6de36ec7` with uncommitted working-tree changes.

## Ownership map

| Responsibility | Authority | Compatibility boundary |
| --- | --- | --- |
| Active player | `PlayerStateManager.state` / `PlayerState` | `PlayerProfile` methods and the debug skill commit adapter delegate here while serialized legacy fields remain |
| Active world | `WorldStateManager.State` / `WorldState` | Existing `state` alias remains |
| Profile selection and commit pointer | `YQProfileSaveSystem.ProfileManifest` | Legacy profile files are read only when no revision manifest exists |
| Paired profile revision | `YQProfileCommitStore` revision directory plus checksums | Shared `player_state.json` and `world_state.json` are working projections |
| Identity | `YQEntityIdentityRecord` and typed IDs | Display names are labels; accepted IDs are never recomputed from them |
| World coordinates | `YQWorldIdentityRecord` | Default units are metres, cell size is 128m, and cell index uses mathematical floor |
| Accepted generated content | `YQAcceptedContentReference` | Domain systems own their future extensions and migrations |
| Events and mutations | `YQEventEnvelope` and `YQMutationReceipt` | Domain producers remain responsible for event meaning |

## Stable identity contract

`YQStateContract.StableId` uses a version-independent SHA-256 prefix over an immutable seed and ordinal. Legacy missing IDs are assigned once from persisted parent, collection ordinal, and creation time; mutable display labels are not inputs. Species and cultural people are separate enum kinds. `YQStateReferenceValidator` rejects duplicate identities, missing parents, and NPC links to missing factions or locations.

World identity records the seed, metre units, `floor(world / cellSize)` convention, 128m cell size, generation/schema/hash versions, selected spatial artifact fields, and render origin. Player logical position is persisted separately from render origin.

The authoritative `YQInvestorPlayerMotor` captures its configured `CharacterController` envelope and supported walk/sprint speeds into `PlayerState.playerCollisionContract` after authority is claimed; no movement tuning is performed by the capture.

## Profile transaction and recovery

`SaveActiveProfile` prepares both canonical documents without writing either manager independently. `YQProfileCommitStore` stages both files plus any registered auxiliary snapshot providers, rejects auxiliary filename collisions, checksums them, publishes one complete revision directory, atomically refreshes the two shared projections, and then publishes `activeCommitId` and `activeRevision` in the manifest. Complete prior revision directories are retained. A failure is returned in `LastTransactionReceipt` and `LastFailure`; no unrelated profile backup is eligible for recovery. Auxiliary providers are registered with `RegisterAuxiliaryDocument` and are intentionally non-serialized service callbacks.

On load, complete revisions are tried newest-first with ownership and checksum validation. If no revision manifest exists, the bounded legacy primary/`.bak` reader is used. Unsupported future state schemas fail closed and do not overwrite the unreadable save with defaults.

## Migration and receipts

`YQStateMigrations` applies ordered, idempotent document migrations through schema 6 before typed deserialization, including the schema-1 player `xp`, array-position, and legacy-flag bridges. It then repairs collections and stable identities once and reports whether the in-memory record changed. Repeated migration is idempotent. Future versions are explicitly rejected. `PlayerState` and `WorldState` expose monotonic `stateRevision`, idempotent `TryApplyMutationCommit`, accepted mutation keys, and typed `YQEventEnvelope` append methods.

## Fixture procedure

1. Enter Play Mode through the enabled `YourQuest_PlaySafe` title flow.
2. Use the Beta Baseline preparation menu only for the disposable `beta-dev-canonical` profile.
3. Confirm the expected world seed remains `76603739` before running baseline checks.
4. Run `YourQuest > Beta Baseline > Run Production Regression`.
5. Exercise profile A and profile B separately, save, switch, quit, reload, and inspect the structured receipt lines. The old `new-world` path created profile B and returned to canonical A; the second profile's ordinary pre-origin state is recorded as a verification gap below.
6. Exit Play Mode before any source edit or recompilation.

The preparation fixture is preaccepted by design and is not evidence that an ordinary new profile completed origin creation.

## Compatibility retirement list

- Remove `PlayerProfile` serialized dictionaries and its adapter methods after all scene/prefab references and `SkillCommitter` editor callers are migrated to `PlayerState`.
- Remove legacy profile-root `player_state.json`/`world_state.json` fallback after every supported save has a manifest revision and the migration fixture has been retained for one release.
- Remove `WorldState.state` and `PlayerStateManager.GetPlayerState` aliases only after generated and tutorial callers no longer use them.
- Keep schema-6 readers and the unsupported-future gate permanently; domain extensions must add ordered migrations rather than changing accepted identities.

## Current evidence

- `COMPILE VERIFIED`: `dotnet build YourQuest.slnx` succeeds with one pre-existing unreachable-code warning in `YQWorldGenerationV2ContractTests.cs`.
- `STATIC VERIFIED`: contract fixtures cover negative coordinates, canonical ID stability, duplicate/missing/illegal/cyclic references, supported/future migrations, repeat migration, transaction interruption points, checksums, event envelopes, idempotent mutation keys, teardown ownership, and request-epoch invalidation.
- `RUNTIME/BEHAVIOR VERIFIED`: after a clean stop/start, the enabled `YourQuest_PlaySafe` title flow reached `titleFlowComplete=True`, `gameplayRuntimeReady=True`, `builderMaterialized=True`, `builderFailed=False`, `player=True`, `npc=True(4/4)`, `terrain=True`, `streaming=True`, and `traversed=True` for `beta-dev-canonical` with seed `76603739`.
- `RUNTIME RECEIPT`: the completed same-session receipt was `pure.contracts=PASS`, `live.snapshot=PASS`, `fixture.startup=PASS`, `live.traversal=PASS`, and `live.reload=PASS`. The clean restarted canonical receipt preserved seed `76603739`, accepted plan, world ID `yq-world-ec5ed7e019841599dcfd36e9`, commit `92fb444bf90846c8b5b6ec9e04f4afb6`, epoch `2`, and player contract `0.38x1.8m`, walk `6.8`, sprint `11.25`; its per-process reload counter was correctly `loads=1`. Evidence is in `C:\Users\Garri\AppData\Local\Unity\Editor\Editor.log`.
- `RUNTIME FIXES VERIFIED`: the old path now serializes `renderOrigin` safely, preserves accepted canonical state across restart, removes stale fixture commit records during clean reset, assigns new worlds profile-owned stable IDs, and rejects no transient region or guessed faction references during canonical save.
- `RUNTIME PROFILE ISOLATION`: `live.profile-isolation=PASS` through the existing title/origin owners. Canonical A was `beta-dev-canonical` / `yq-world-ec5ed7e019841599dcfd36e9` / `76603739`; profile B was `158b6ab089a840bc8e23ab7ea841e10e` / `yq-world-b4b1332ce47a8c7db9fcd03c` / `767b922f`. B completed accepted origin and plan, then the flow returned to A with exactly one player object and one `YQInvestorPlayerMotor`.
- `RUNTIME PROFILE RELOAD`: `live.profile-reload-b=PASS` after stopping and restarting the enabled PlaySafe session. B reloaded with `origin=True`, `plan=True`, its own player/world identity, one player object, one player motor, and `loads=1`; the canonical profile was restored before exiting Play Mode. Evidence is in `C:\Users\Garri\AppData\Local\Unity\Editor\Editor.log`.
