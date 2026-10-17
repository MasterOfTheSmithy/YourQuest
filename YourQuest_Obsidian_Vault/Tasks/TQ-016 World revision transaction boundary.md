---
id: TQ-016
type: task
status: Specified
priority: p1
area: world-generation
wave: p7
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/specified
  - priority/p1
  - area/world-generation
  - wave/p7
---

# TQ-016 — World revision transaction boundary

## Outcome
Apply accepted world changes transactionally with deterministic receipts.

## Why
Dynamic worlds need durable inspectable changes.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
Dependent records commit together or the prior world remains intact.

## Evidence
World-engine P7 and TrueVision commit contract.

## Next action
Write the revision receipt schema.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
