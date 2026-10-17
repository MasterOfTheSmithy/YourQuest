---
id: TQ-028
type: task
status: Verified
priority: p0
area: world-generation
wave: wg2
source:
  - "[[Sources/Planning Index]]"
tags:
  - type/task
  - status/verified
  - priority/p0
  - area/world-generation
  - wave/wg2
---

# TQ-028 — Deterministic layout math evidence

## Outcome
Preserve deterministic placement math and replayable layout outputs.

## Why
Spatial coherence depends on repeatable transforms and bounds.

## Owner and boundaries
Use the smallest coherent change inside the existing YourQuest architecture. Do not replace working generation systems or widen this card without a linked decision.

## Acceptance criteria
Layout checks remain reproducible after reload.

## Evidence
TrueVision notes record 256 deterministic layout checks.

## Next action
Use as a prerequisite for golden assemblies.

## Related work
- [[Boards/YourQuest Board]]
- [[Goals/Phase Roadmap]]
- [[Sources/Planning Index]]
