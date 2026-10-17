---
id: TQ-024
type: task
status: In Review
priority: p0
area: release
wave: m8
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/in-review
  - priority/p0
  - area/release
  - wave/m8
---

# TQ-024 — Open-beta golden loop play-mode gate

## Outcome
Pass hub, wilderness, dungeon, NPC, quest, merchant, shrine, gathering, and return loop.

## Why
The beta target is a reliable playable slice.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
No active compiler errors; local input responsive; state persists; dependent cards pass.

## Evidence
Open-beta plan and play-mode observations.

## Next action
Run the seeded golden loop.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
