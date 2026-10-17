# YourQuest Asset Integration Plan

## Goal
Use paid assets to raise perceived quality without letting the project become slow, unstable, or visually incoherent.

## Current Approved Palette
- Befour Studios: Victorian Mansion.
- Befour Studios: Asian Dynasty Environment.
- Befour Studios: Persepolis Empire Environment.
- Infinity PBR: Weapons and Armor Pack #1.
- Infinity PBR: Mimic Chests and regular chests.
- Battle Sound Library: Magic and melee.
- Human Character Fantasy RPG.
- Character Accessories Fantasy RPG.
- Gabriel Aguiar Prod: Unique AoE Magic Abilities Vol 1.
- Gabriel Aguiar Prod: Unique Projectiles Vol 1.
- Gabriel Aguiar Prod: Unique Toon Projectiles Vol 1.
- Gabriel Aguiar Prod: Unique Lasers Vol 1.
- Gabriel Aguiar Prod: VFX Graph Sci-Fi Shield Vol 1.

## Import Rule
Do not import every owned asset into the main project. Keep large packs outside the project until a specific scene, character, enemy, item, sound, or effect needs them.

Recommended flow:
- Keep a separate local asset vault for untouched Marketplace downloads.
- Import one pack at a time into a temporary Unity project.
- Extract only approved prefabs, materials, animations, sounds, VFX, and dependencies.
- Move curated assets into this project under `Assets/_Imported/<Vendor>/<Pack>/`.
- Wrap usable game-facing prefabs under `Assets/_Project/Art/` or `Assets/_Project/Prefabs/`.

## First Visual Target
Build one polished fantasy RPG slice before mixing every environment style.

Primary target:
- Victorian Mansion as the first hero environment.
- Infinity PBR weapons, armor, chests, and mimic chests for RPG interaction depth.
- Human Character Fantasy RPG plus accessories for player/NPC presentation.
- Battle Sound Library for immediate combat and UI feedback.
- Gabriel Aguiar AoE/projectile magic for readable ability feedback.

Defer:
- Asian Dynasty and Persepolis environments until the mansion slice is stable.
- Sci-fi shield VFX unless it is restyled as magical barrier feedback.
- Extra unlisted asset packs until there is a concrete content need.

## Integration Order
1. Create an asset intake scene for visual testing, scale checks, lighting checks, and prefab cleanup.
2. Import the Victorian Mansion into a temporary project, then migrate only the needed room/hallway pieces.
3. Replace the runtime prototype arena with one curated mansion encounter space.
4. Import Infinity PBR weapons/armor/chests and map them to existing equipment slots.
5. Add chest and mimic interactions to the world response loop.
6. Add combat/magic audio events for swing, hit, dash, cast, pickup, UI open, and UI confirm.
7. Add Gabriel Aguiar VFX to spells after gameplay events are already wired.
8. Add character art last enough to validate scale, camera, hitboxes, equipment sockets, and dialogue framing.

## Quality Gates
Before an asset enters the main playable path:
- It must have correct scale against the player controller.
- It must render correctly in URP.
- It must not add visible material/shader errors.
- It must not break build validation.
- It must have colliders simplified enough for traversal/combat.
- It must be named and foldered predictably.
- It must serve a current gameplay need.

## Next Step
Create a dedicated asset intake scene and start with the Victorian Mansion plus one Infinity PBR chest. This gives the slice a real location and one polished interaction without overwhelming the project.
