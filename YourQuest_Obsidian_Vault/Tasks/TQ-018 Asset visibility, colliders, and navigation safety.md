---
id: TQ-018
type: task
status: In Review
priority: p0
area: assets
wave: p4
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/in-review
  - priority/p0
  - area/assets
  - wave/p4
---

# TQ-018 — Asset visibility, colliders, and navigation safety

## Outcome
Prevent water, props, NPCs, and enemies from rendering or moving through structures.

## Why
Screenshots show see-through assets and collision failures.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
Materials, depth, colliders, nav, and overlap checks match visible geometry.

## Evidence
Screenshots 196–203 and 207–220.

## Next action
Run an occlusion and collider probe.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
