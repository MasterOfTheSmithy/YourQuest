# Faction, reputation, and karma context

World faction state is owned by `WorldState.factionAttitudes` and `WorldState.factions`. `WorldDeltaApplier` accepts normalized faction operations from `LLMThinkCycle`; stable faction IDs are validated by `YQStateFoundation`. The repository does not expose a separate `KarmaManager`; do not invent one. Reputation-like behavior is represented through faction attitudes, player ledgers/behavior/progression, and typed world mutations, subject to the current design documents.
