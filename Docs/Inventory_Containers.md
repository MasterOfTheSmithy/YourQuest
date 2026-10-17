# Unified inventory and containers

Implementation date: October 2, 2026. This change is architecture-sensitive because it extends paired persisted state. It does not change terrain planning, cell continuation, accepted spatial artifacts, world seeds, imported assets, or Goal 8 R2 receipts.

## Owners and integration gaps

| Boundary | Existing owner and inspected implementation | Integration |
|---|---|---|
| Player items/equipment | `Data/State/Player State/PlayerState.cs`: `inventoryItems`, `equippedItemBySlot`, `TryEquipItem` | `YQPlayerInventoryView` references these same records. No second backpack. Equipped items must be unequipped before deposits. |
| Item generation and assets | `Generated/GeneratedRpgContentService.cs`, `GeneratedRpgContentLibrary`, its Resources asset, `YQPlayerEquipmentVisual` | `GenerateContainerItem` calls the existing mechanical/asset generator with fixed source level, source element, controlled rarity and deterministic IDs. It does not use the player's current build. |
| World identity/entities | `WorldState`, `EntityInfo`, accepted generated NPC/site records, `YQCellLootBindingsV2.BuildLootId` | World containers are keyed by existing entity/loot IDs. Reviewed geometry remains reviewed. Legacy spawner slots previously collided across sites in one region; their IDs now include their existing deterministic spawner seed or authored hierarchy identity. Accepted generated NPC IDs stay unchanged. |
| Region/progression context | `GeneratedRegionRecord`, `GeneratedSpatialRegionRecord`, `GeneratedNpcPlanRecord`, `GeneratedEncampmentRecord`, `GeneratedSettlementRecord` | Region danger/tier, biome tags, source level, exact NPC/site relationships, faction, functional role and encounter threat feed the structured context. Player level does not determine opening rewards. |
| Persistence | `YQStateMigrations`, `YQStateReferenceValidator`, `YQProfileSaveSystem`, `YQProfileCommitStore` | Schema 8 adds `WorldState.containers`. Generation and transfers publish both documents through the existing paired commit. The manifest pointer is staged before publication so a failed save leaves the accepted in-memory revision intact. No separate loot save files. |
| Storage interaction | `YQLockpickableLoot`, reviewed `YQCellLootBindingsV2`, `YQInvestorCombat` | Locks, lockpick UI, lid feedback and mimics retain their interaction owner. Opening exposes saved contents rather than immediately granting an item and gold. `YQWorldContainer` also supports explicitly configured physical storage. |
| Hostiles/loot | `YQInvestorEnemy`, `YQInvestorEnemySpawner`, `YQInvestorLootableCorpse`, director callback `GrantEnemyLoot` | Death prepares one inventory; corpse UI withdraws from it. The former extra random/direct reward is removed. Defeated IDs restore corpse views instead of combat or loot rerolls. |
| UI/console | `RuntimeModalUiBlocker`, Input System, existing developer registry and rollback session | The storage panel uses the existing authoritative player and modal blocker. Console inspection and selected-source regeneration extend the current developer console. |

Previously there was no reusable world-container state, transfer UI, capacity/stack transaction boundary, context-constrained loot table, or persistent hostile inventory. Corpse loot was transient, and director death rewards could award an additional random copy. Those paths now share `IYQInventory` and `YQContainerInventory`.

## Contract and lifecycle

`YQContainerRecord` includes stable entity ID, profile ID, capacity, contents, currency, owner/access policy, lock/death state, generation state/version/seed, resolved profile, frozen generation context, revision, linked equipment slots, deposit history, restock receipts and durable authored-artifact claims.

Physical storage is generated on the first successful unlocked interaction. Hostile bonus contents are generated on death; accepted equipment may be bound earlier through `YQWorldContainer.TryBindEquipment`. Both paths persist before presentation or source destruction. Generation never waits for an LLM. Accepted records, including empty inventories, take precedence over new context or configuration.

The seed is SHA-256 over length-prefixed canonical world seed, entity ID, generation version and explicit restock cycle. Each output slot receives a deterministic item ID. Generation uses its own fixed integer random stream, never Unity random, frame time, current player level or traversal order. Bump both `GenerationVersion` and the JSON catalog's version whenever generation policy/mechanics change; existing accepted contents still remain intact.

Reviewed storage retains its existing stable ID and collider. `YQCellLootBindingV2.containerType` supports all physical storage types. Existing bindings default to Chest; legacy authored target names distinguish wardrobes, cupboards/cabinets, barrels, crates and caches. Generic storage can be configured with `YQWorldContainer` on an existing approved object/collider. Decorative geometry without an interaction contract is not silently converted into a loot provider; additional authored targets require the normal site review/binding workflow.

Authored sources without an explicit ID use scene path and hierarchy name/index. Their hierarchy must remain stable; assign an explicit ID before rearranging them. Procedurally materialized sources must supply their accepted entity/loot IDs or deterministic spawner seed.

## Context, eligibility and fallbacks

`YQContainerContext` carries region/biome, danger and progression tier, source level/type/rarity, faction/culture, owner role, exact site ID/purpose, element/theme, wealth, encounter difficulty, rarity budget and world seed. Explicit authored hints may supply metadata the current world schema lacks. Exact accepted NPC/site IDs and structured tags select policies; narrative lore and reward descriptions do not execute mechanics.

Fallbacks are recorded in `context.fallbacks`:

- Seed: accepted world-plan seed, then canonical world identity, then `yourquest_default_world` only when both are absent.
- Missing region: temperate biome and tier 1, retaining any explicit authored tier.
- Source level: explicit level, otherwise `EntityInfo.level`, otherwise region tier, minimum 1. Never the opener's level.
- Missing culture, profession, purpose, element, wealth: local, unknown, storage, neutral and 0. Unknown hostiles favor remains rather than unexplained weapons or valuables.
- Missing eligible profile: an accepted empty inventory marked `empty:missing-profile`. Missing/malformed catalog: fail access without accepting partial generation.
- Incompatible equipment: Common non-equippable salvage through the existing item generator. This logical supply has no fabricated model. No arbitrary asset path is accepted.

Source element controls equipment presentation using existing approved effect families. Fire/ember uses fire effects, storm electric effects, and water/ice the available water family; a missing matching family uses the existing neutral item effect. This does not add unsupported elemental combat mechanics. Food restores health/stamina through the existing consumable path, without potion-style mana restoration.

Profiles use source eligibility, required/excluded context tags, weighted entries, quantities, level ranges, priority, rarity weights/budgets and optional guaranteed slots. Household profiles reject unrelated equipment unless an explicit `authoredExceptionPolicyId` is supplied. Hostile gear requires humanoid/bandit eligibility. Player deposits are intentional contents and are not restricted to a source's procedural profile.

Rarity order is **Common, Uncommon, Rare, Epic, Unique, Legendary, Mythical**. Unique/Mythical random weights must be zero. Authored rarity requires a guaranteed entry, explicit `authoredPolicyId`, eligibility tags and sufficient budget. Durable source claims prevent the same authored artifact from reappearing after transfer or consumption. No default profile grants Unique or Mythical.

## Configuration examples using project bindings

Configuration: `Assets/Assets/Resources/YQContainerLootProfiles.json`.

| Profile ID | Source/context | Existing item/asset IDs used |
|---|---|---|
| `household_clothing_v1` | Wardrobe | `legs` and `boots`; Human Armor `Trousers.prefab`, `Boot_Left.prefab`. |
| `kitchen_cupboard_v1` | Cupboard | Existing `consumable` contract with Victorian Mansion `SM_RoundBread.prefab`; non-equippable `supply` with Container District `SM_Spoon.prefab`. |
| `provision_barrel_v1` / `supply_crate_v1` | Provisions/packed goods | Bread and utensils, bounded quantities. Crates may legitimately be empty. |
| `regional_chest_v1` | Chest/cache, ordinary region | Existing `weapon`, `boots`, `ring_left`, `consumable` kinds, approved library models and small gold range. |
| `dungeon_reward_v1` | Chest/cache, `encounter` tag, level 8+ | Guaranteed existing `weapon` kind, weighted `offhand`/`necklace`/rations, larger gold range, budget-limited rarity through Legendary. |
| `bandit_cache_v1` / `bandit_hostile_v1` | Structured `bandit` eligibility | Supported gear, food and stolen utensils. Bound equipped weapon suppresses the extra weapon entry. Consumable/stolen-goods bonus entries are explicit policy. |
| `hostile_remains_v1` | Other supported hostile corpses | Optional non-equippable remains; no unexplained gold or equipment. |
| `authored_empty_v1` | Explicit `loot:empty` tag | Empty inventory with zero currency. |

Equipment is validated against its actual slot's curated prefab list; clothing, food and utensils have closed semantic bindings. Editor checks also require an existing prefab and renderer. The current project has slot-bound pieces, not a general modular item assembler: this validation uses its actual asset boundary. Renderer presence does not certify material quality, rig fit or motion.

## Equipment, death and respawn policy

Hostiles use **one inventory with equipment slot references into that inventory**. Equipment binding imports the accepted item once; no extra loot copy is minted. Death exposes that same item ID. Withdrawal clears its equipment reference. Explicit bonus entries remain separate policy. Living hostiles cannot be opened, even when a component is misconfigured. Corpse deposits are disabled.

Current imported enemy models may have visual weapons without structured equipped-item records. They remain presentation; no item mechanics are inferred from mesh names. Production callers with an accepted weapon should bind it through `TryBindEquipment` and relinquish a separate ownership store only after the paired commit succeeds. Automatic visual-weapon-to-record conversion and a general enemy equipment/stat/animation service remain unsupported dependencies; the inventory change does not invent them.

Defeated stable hostile slots and mimics do not respawn or refill under the same ID. Streaming/reload restores a corpse projection at its saved logical death position. An encounter's completion gate blocks combat rather than hiding saved corpse loot. Exhausted loaded spawners stop repeated inventory work. A new authored encounter incarnation needs a new stable source identity; no implicit time-based enemy respawn exists. Existing revealed-but-living mimic streaming behavior is retained; persisting arbitrary live combat health/pose is outside the inventory contract.

All physical sources default to **Never** restock. A specifically configured `ExplicitWhenEmpty` source accepts an explicit unique supply event only when items/gold are empty and no player deposits have occurred. Delivery increments the persisted cycle, generating deterministic new contents; replayed event IDs fail. A source with deposit history never procedurally replaces those contents, even after the player later removes them. No automatic timer or opening-triggered refill exists.

## Transfers, access and migration review

Transactions validate quantities, item identity, strict template/mechanical/asset stack equivalence, stack limit 99, destination slots, equipment references, access and expected revisions before mutation. Partial stacks receive distinct deterministic IDs. Backpack capacity 0 preserves the existing unlimited legacy policy; positive values constrain shared transfers. Old oversized player stacks remain readable and may be moved in bounded portions.

All new mutations use the existing paired save owner. Failed or thrown publication restores item lists, currency, counters, ledger and revisions. The profile owner stages a detached manifest candidate and accepts it only after successful pointer publication; rejected complete revisions/projections cannot become the next in-memory selected revision. Withdrawals advance existing loot counters within that same transaction. Cross-store identity and equipment-reference validation participates in the current save/load validator. No server/network inventory owner was present in the inspected interaction/persistence path; multiplayer authority/reconnect reconciliation is unimplemented.

OwnerOnly containers refuse access by a different owner; Public containers allow the existing local authoritative player. Existing chest locks remain enforced. No existing pickpocket or theft-adjudication service was found at this interaction boundary; ownership is enforced conservatively rather than silently granting theft or living-hostile access.

Schema 7→8 migration is additive and ordered through `YQStateMigrations`; schema 8 retains the profile manifest and paired commit format. Old `loot:opened:<persistentLootId>` counters lazily become empty, unlocked, consumed containers. Existing backpack items, equipped IDs, accepted world plans, hashes and origin state are retained. Previously transient corpse contents cannot be reconstructed from older saves; no replacement records are fabricated. Migration and senior review are covered by focused source review and detached preservation/conservation checks, without extra agents.

## Developer inspection and testing

- `container list`, `container select "<entityId>"`, `container inspect`: persisted context, resolved profile, seed/version/cycle, rules, linked equipment and contents. Inspection does not reroll loot.
- `test snapshot`, then select one physical test source. `container regenerate` displays the replacement warning without changing anything. `container regenerate --replace` requires the existing valid snapshot and explicit selection. Normal profile saves stay blocked; `test restore` restores both containers and backpack. Identity conflicts with already-held loot are rejected. Hostile regeneration is refused.
- `container restock "<unique-event-id>"` tests the explicit empty-source policy inside the same rollback session.

Automated entry points: `Tools/ContainerVerification/compile.ps1`, `run-managed.ps1`, and **YourQuest → Verification → Run Container Inventory Contracts**. The named one-shot `Tools/ContainerVerification/Run.request` runs detached Editor fixtures after the next source import. Fixture disk saves use unique temporary directories, never production profiles.

## Observed results

Fresh receipts record **17/17 Editor fixtures**, **13/13 managed production-contract checks**, and successful compilation of both runtime and Editor assemblies. These receipts identify timestamps, individual results and evidence levels. Ordinary production PlaySafe acceptance remains unverified.

| Acceptance area | Observed evidence | Scope/limit |
|---|---|---|
| Fresh executable compilation | PASS, runtime and Editor assemblies | Full real project sources against installed Unity references. |
| Low/high regions and several storage types | PASS, detached Editor fixtures | Actual item generator and curated prefab checks; not placed production interactions. |
| Deterministic contents and source/version seed | PASS, detached Editor fixtures | Repeated identical context independent of player level/name. |
| Hostile equipment→corpse transfers | PASS, managed and Editor fixtures | Same item ID, cleared equipped reference, repeated loot denied; imported body integration remains unverified. |
| Stack/capacity/access/rollback | PASS, managed and Editor fixtures | Actual production contract; injected failed/thrown publication. |
| Paired save/checksum/reload and rebind | PASS, managed and Editor fixtures | Real paired commit store in disposable folders; production title/streaming flow unverified. |
| Interrupted disk publication and manifest staging | PASS, managed checks | Injected failures after player/world projections and before pointer publication retain the accepted paired revision. |
| Deposits and explicit restock | PASS, detached Editor fixtures | Deposit protection, event replay and Never policy. |
| Missing metadata, empty loot, invalid combination | PASS, detached Editor fixtures | Explicit fallbacks and incompatible item→valid supply. |
| Save migration and reviewed storage collider/identity | PASS, detached Editor fixtures | Additive schema migration and existing binding/rejection regression. |
| Console inspection and regeneration/snapshot gates | PASS, detached Editor fixture | Explicit selection, warning without mutation, replacement refused without snapshot, two-owner restoration. |
| Ordinary visible PlaySafe storage/loot flow | NOT VERIFIED | Unity was occupied by a pre-existing paced material conversion; it was left running. |

The next live acceptance pass should use a disposable test snapshot after conversion finishes: open and transfer from representative storage; lockpick; defeat a hostile and take bound equipment; deposit/withdraw; save/reload through the ordinary title flow; unload/revisit storage/corpses; and explicitly restock an eligible empty source. Full gameplay completion is not inferred from these contracts.
