# Data and persistence scope

`PlayerStateManager`, `WorldStateManager`, `YQProfileSaveSystem`, `YQProfileCommitStore`, `YQStateFoundation`, migrations, and serialization converters own persisted state. Preserve player/world schema versions, stable IDs, profile ownership, paired commit semantics, checksums, recovery copies, and load order. Field renames, new persisted records, or migration behavior require migration-guardian and senior review.
