---
id: TQ-002
type: task
status: Building
priority: p0
area: world-generation
wave: p0
source:
  - "[[Sources/Design Authority]]"
tags:
  - type/task
  - status/building
  - priority/p0
  - area/world-generation
  - wave/p0
---

# TQ-002 — Structured generation and validation pipeline

## Outcome
Keep generated records typed, validated, normalized, curated, deduplicated, and persisted.

## Why
The LLM is a content generator, not runtime authority.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
World, site, NPC, quest, asset, and capability schemas have validation gates.

## Evidence
GDD and TrueVision contracts.

## Next action
Tighten the next missing validation receipt.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
