---
id: TQ-019
type: task
status: In Review
priority: p0
area: player
wave: m1
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/in-review
  - priority/p0
  - area/player
  - wave/m1
---

# TQ-019 — One authoritative player and presentation

## Outcome
Keep gameplay state, cameras, equipment, and animation bound to one player.

## Why
Duplicate state makes runtime evidence unreliable.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
One player owns movement, combat, inventory, and save state.

## Evidence
GDD authoritative-player contract.

## Next action
Verify bootstrap and reload ownership.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
