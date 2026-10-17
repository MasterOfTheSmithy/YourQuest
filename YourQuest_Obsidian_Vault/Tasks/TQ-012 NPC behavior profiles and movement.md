---
id: TQ-012
type: task
status: Building
priority: p1
area: npc
wave: m5
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/building
  - priority/p1
  - area/npc
  - wave/m5
---

# TQ-012 — NPC behavior profiles and movement

## Outcome
Ground NPCs correctly, avoid roofs, and provide deterministic movement loops.

## Why
Screenshots show roof spawns, idle NPCs, and enemy clipping.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
Spawn checks, nav, colliders, roles, and threat behavior agree.

## Evidence
Screenshots 204–206.

## Next action
Run a settlement population check.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
