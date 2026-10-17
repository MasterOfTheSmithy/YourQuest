# YourQuest Vertical Slice Requirements

## Stage
Early playable prototype moving toward a reliable vertical slice.

## Design Principle
The world must acknowledge meaningful player behavior even when the LLM is unavailable, slow, repetitive, or wrong. LLM output may enrich the response, but runtime systems must provide a deterministic baseline.

## Required Golden Loop
- Spawn into the runtime-built tutorial world.
- Talk to Archivist Vey.
- Fight or evade hostiles.
- Interact with at least one shrine, pickup, or corpse.
- Receive a quest, world note, or progression offer.
- Accept or decline an offer.
- Save and reload player/world state.

## Must-Have Systems
- Broad action capture: movement, combat, jumping, dodging, crouching, dialogue, shrine use, looting, and pickups.
- World reaction baseline: every meaningful action batch updates region state, activity flags, tension, or canon.
- LLM enhancement path: valid model output can create richer world notes, quests, dialogue, and progression offers.
- Failure visibility: HUD shows latest director/world status instead of failing silently.
- Bounded progression: rewards are confidence-gated, deduped, and shown as player choices before acceptance.
- Persistence: accepted offers, inventory, behavior memory, region state, and canon survive reload.

## Current Focus
Stabilize the responsive-world loop before adding more content. Expand breadth only after the golden loop works repeatedly in Unity play mode.

## Asset Direction
Use curated paid assets to replace prototype primitives only after they serve the golden loop. See `Docs/AssetIntegrationPlan.md` for import order, foldering, and quality gates.
