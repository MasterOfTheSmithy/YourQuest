# YourQuest AI change-control policy

## LOCAL-SAFE

Localized fixes, read-only diagnostics, focused tests, editor tooling, and measured behavior-preserving changes may be implemented locally when owner, contract, and verification are explicit and serialized/save/generation semantics remain unchanged.

## SENIOR-REVIEW

Required for player/world/profile ownership, `YQWorldGenerationArchitecture` or `YQSpatialPlanVersionRouter`, deterministic seed/RNG/boundary rules, stage ordering, streaming/materialization, generated-content commit contracts, public/serialized APIs, scene/prefab/GUID changes, faction/quest/dialogue state, or cross-system dependencies.

## HUMAN-APPROVAL

Required when not already authorized for feature removal, destructive asset/scene migration, broad architecture replacement, save invalidation, or requirement changes. Prepare a concrete diff/plan first. Risk classification alone does not require another permission request; the configured engineer can perform the senior review role without a specific model or second agent.

## Source control

Inspect status before and after edits. Preserve the existing dirty working tree. Do not reset, checkout, delete, or rewrite user changes. Stage explicit files for a coherent commit or review branch and exclude Unity generated folders. Keep documentation/setup publication separate from the unrelated gameplay and asset changes.
