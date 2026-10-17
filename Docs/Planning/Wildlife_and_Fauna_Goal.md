# Wildlife and fauna goal

**Status:** Queued future work, outside the current closed-beta G01–G20 acceptance sequence.

## Goal

Introduce world wildlife and fauna that make habitats feel alive while staying deterministic, bounded, and affordable on the supported beta hardware.

## Reason

The generated fish feature created moving, interactable objects during water publication. This goal defines any future wildlife work around measured cost and explicit lifecycle ownership. Removing that implementation does not establish that it was the cause of the observed lag; the future work must measure its own baseline and attribute cost to named owners.

## Prerequisites

- Complete the current closed-beta roadmap and preserve its existing acceptance gates.
- Reuse the accepted asset-binding, semantic-world, terrain, streaming, player, resident, presentation, and performance authorities rather than adding parallel owners.
- Set numerical CPU, memory, active-population, and hitch budgets from the accepted hardware profile before implementation begins.

## Scope

- Define typed species and habitat records with stable identity and project-approved asset bindings.
- Derive ambient placement deterministically from accepted world and cell authority, independent of traversal order; decoration must not consume canonical random streams.
- Give simulation, activation distance, update cadence, and unload/revisit behavior one bounded runtime owner integrated with the existing streamer.
- Persist only meaningful creature state that requires continuity; regenerate ambient presentation from accepted deterministic inputs.
- Route any accepted player interaction through existing gameplay, mutation, and save authorities.
- Measure representative populated and empty scenes against the agreed budgets, including peak-frame cost, allocations, active object counts, and unload/revisit stability.

## Out of scope

- Reinstating the removed generated-fish movement or catch implementation as-is.
- Changing terrain, hydrology, world-seed, cell, save, player, or inventory authority to support ambient fauna.
- Adding fishing, hunting, taming, or creature-combat mechanics without their own explicit acceptance criteria.
- Weakening population limits or performance budgets to make a content-density check pass.

Aquatic species may be considered as part of a future design, but their inclusion and any related interaction require explicit acceptance against the same budgets.

## Acceptance criteria

1. Species, habitat, asset, persistence, and runtime ownership contracts are documented before implementation; accepted content is not regenerated or reidentified.
2. Fixed canonical inputs reproduce the same fauna placement regardless of cell visitation order and do not alter world-generation hashes or random streams.
3. Active populations and simulation work remain within the agreed numeric budgets during supported traversal, streaming, unload, and revisit.
4. Fresh profiler evidence attributes fauna CPU time, allocations, and peak-frame cost to named owners and meets the approved budgets on the accepted hardware profile.
5. Any persistent creature changes survive the normal save/reload path; ambient fauna creates no unbounded save growth or duplicate actors.
6. No interaction bypasses the existing typed gameplay and persistence boundaries, and no second player, streamer, world authority, or save owner is introduced.
