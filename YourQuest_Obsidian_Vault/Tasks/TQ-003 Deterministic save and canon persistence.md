---
id: TQ-003
type: task
status: Building
priority: p0
area: progression
wave: p0
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/building
  - priority/p0
  - area/progression
  - wave/p0
---

# TQ-003 — Deterministic save and canon persistence

## Outcome
Persist accepted canon and replay it deterministically across reloads.

## Why
Regeneration breaks continuity.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
Stable IDs, commit boundary, revision receipts, and reload checks exist.

## Evidence
GDD persistence and TrueVision commit contract.

## Next action
Run a save and reload comparison.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
